using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.People;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for foundation slice 3b — the rung-4 competence RESOLVER WIRE (dossiers ⚙6/⚙10). A fleet's flagship
    /// commander's Firepower/Toughness competence (their <see cref="BonusesDB"/>) now scales the WHOLE fleet's
    /// effective firepower/toughness in the auto-resolver (<see cref="CombatEngagement.GetCombatShips"/>), riding
    /// on top of doctrine. Byte-identical to before when no commander carries a bonus
    /// (<c>FleetCommanderMult -> 1.0</c>). Built on the same TestScenario/FleetFactory pattern as
    /// <c>FleetComponentTests</c> (which covers the doctrine mults this layers onto).
    /// </summary>
    [TestFixture]
    public class CommanderCombatWireTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[commander-wire] " + m);

        private static Entity AddShip(TestScenario s, Entity fleet, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.SetDataBlob(new ShipCombatValueDB(10_000, 1_000_000, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship));
            return ship;
        }

        private static Entity SeatCommanderOn(TestScenario s, Entity ship, params Bonus[] bonuses)
        {
            var commander = CommanderFactory.Create(s.StartingSystem, s.Faction.Id,
                new CommanderDB("Adm. Test", 1, CommanderTypes.Navy));
            var bonusesDB = commander.GetDataBlob<BonusesDB>();
            foreach (var b in bonuses)
                bonusesDB.Bonuses.Add(b);
            ship.GetDataBlob<ShipInfoDB>().CommanderID = commander.Id;
            return commander;
        }

        [Test]
        [Description("A flagship commander with a Firepower bonus scales the fleet's firepower in the resolver — the slice-3b wire.")]
        public void FlagshipCommanderFirepowerBonus_ScalesFleetFirepower()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Task Force");
            var flagship = AddShip(s, fleet, "Flagship");
            fleet.GetDataBlob<FleetDB>().FlagShipID = flagship.Id;

            SeatCommanderOn(s, flagship, new Bonus("Fighting Admiral", 0.5, BonusType.Perentage, BonusCategory.Firepower));

            var ships = CombatEngagement.GetCombatShips(fleet);
            var fs = ships.First(c => c.Ship.Id == flagship.Id);
            Log($"flagship fp×{fs.FirepowerMult}, tough×{fs.ToughnessMult}");

            Assert.That(fs.FirepowerMult, Is.EqualTo(1.5).Within(1e-9), "a +50% firepower admiral scales the fleet's firepower");
            Assert.That(fs.ToughnessMult, Is.EqualTo(1.0).Within(1e-9), "no toughness bonus -> toughness unchanged");
        }

        [Test]
        [Description("No commander (or a null fleet) leaves the multipliers at 1.0 — byte-identical to pre-commander combat.")]
        public void NoCommander_LeavesMultipliersNeutral()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Task Force");
            var flagship = AddShip(s, fleet, "Flagship");
            fleet.GetDataBlob<FleetDB>().FlagShipID = flagship.Id;
            // no commander seated on the flagship

            var ships = CombatEngagement.GetCombatShips(fleet);
            var fs = ships.First(c => c.Ship.Id == flagship.Id);
            Assert.That(fs.FirepowerMult, Is.EqualTo(1.0), "no commander -> no change");
            Assert.That(fs.ToughnessMult, Is.EqualTo(1.0));
            Assert.That(CombatEngagement.FleetCommanderMult(null, BonusCategory.Firepower), Is.EqualTo(1.0), "null fleet is safe");
            Log("no commander / null fleet -> neutral");
        }

        [Test]
        [Description("The flagship commander buffs the WHOLE fleet, not just the flagship (fleet-level command).")]
        public void FlagshipCommander_BuffsEveryShipInTheFleet()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Task Force");
            var flagship = AddShip(s, fleet, "Flagship");
            AddShip(s, fleet, "Escort"); // a second, non-flagship ship in the same fleet
            fleet.GetDataBlob<FleetDB>().FlagShipID = flagship.Id;

            SeatCommanderOn(s, flagship, new Bonus("Iron Discipline", 0.2, BonusType.Perentage, BonusCategory.Toughness));

            var ships = CombatEngagement.GetCombatShips(fleet);
            Assert.That(ships.Count, Is.EqualTo(2));
            foreach (var c in ships)
                Assert.That(c.ToughnessMult, Is.EqualTo(1.2).Within(1e-9), "the flagship admiral toughens the whole fleet");
            Log("whole-fleet toughness ×1.2 from the flagship commander");
        }
    }
}
