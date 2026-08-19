using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Docking;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;   // NewtonThrustAbilityDB (fuel type)
using Pulsar4X.Ships;
using Pulsar4X.Storage;    // CargoStorageDB, CargoMath (fuel readout for the rearm gauge)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E12 — CARRIER SORTIE (Operation Blueprint-to-Steel, 2026-08-18). A carrier LAUNCHES a carried craft into the
    /// fight by UNDOCKING it and RECOVERS it by DOCKING it — reusing the existing Docking verbs, no new order. The
    /// engine wire is ONE filter: when <see cref="CombatEngagement.EnableCarrierSortie"/> is on, the resolver's
    /// ship-collect walks SKIP a docked craft (<see cref="DockTools.IsDocked"/>), so a docked fighter is held in the
    /// hangar (not a combatant) and rejoins the fight the moment it undocks. Byte-identical off (no stock ship mounts a
    /// bay → nothing is ever docked). Slice 1 = this launch/recover wire; rearm/refuel-on-recovery is slice 2.
    /// </summary>
    [TestFixture]
    public class CarrierSortieTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[carrier-sortie] " + m);

        private const string HeavyBerth = "default-design-heavy-berth";   // one wide 60 t door — anything light fits

        private static Entity Spawn(TestScenario s, ShipDesign d, string name)
            => ShipFactory.CreateShip(d, s.Faction, s.StartingBody, name);

        private static ComponentDesign BayDesign(TestScenario s)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(HeavyBerth), Is.True, $"'{HeavyBerth}' should be built for the start faction");
            return designs[HeavyBerth];
        }

        private static double Mass(Entity e) => e.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.MassTotal : 0;

        private static ShipDesign Lightest(TestScenario s, List<ShipDesign> designs)
        {
            ShipDesign best = designs[0]; double bestMass = double.MaxValue;
            foreach (var d in designs) { double m = Mass(Spawn(s, d, "Weigh " + d.Name)); if (m > 0 && m < bestMass) { bestMass = m; best = d; } }
            return best;
        }

        [Test]
        [Description("A DOCKED craft is held in the hangar (not enrolled in combat) when EnableCarrierSortie is ON; "
                     + "undock = launch (it fights again), dock = recover. Flag OFF → a docked craft still fights "
                     + "(byte-identical). The carrier itself always fights.")]
        public void DockedCraft_HeldInHangar_WhenSortieOn_LaunchesOnUndock()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            Assert.That(designs, Is.Not.Empty, "the start faction has ship designs to spawn");

            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Carrier Group");
            var carrier = Spawn(s, designs[0], "Carrier");
            carrier.AddComponent(BayDesign(s));   // install a dock bay so it can carry a craft
            var fighter = Spawn(s, Lightest(s, designs), "Fighter");

            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, carrier));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, fighter));

            List<int> Ids() => CombatEngagement.GetFleetShips(fleet).Select(e => e.Id).ToList();

            // Sanity: before docking, both the carrier and the fighter are combatants.
            Assert.That(Ids(), Does.Contain(fighter.Id), "before docking, the fighter is a combatant");
            Assert.That(Ids(), Does.Contain(carrier.Id), "the carrier is a combatant");

            // Dock the fighter INSIDE the carrier (docking touches only position, not fleet membership).
            Assert.That(DockTools.TryDock(carrier, fighter, out var why), Is.True, "the fighter docks in the carrier (" + why + ")");
            Assert.That(DockTools.IsDocked(fighter), Is.True, "IsDocked sees the docked fighter");

            bool saved = CombatEngagement.EnableCarrierSortie;
            try
            {
                // Flag OFF: a docked craft STILL fights → byte-identical (the pre-E12 behaviour).
                CombatEngagement.EnableCarrierSortie = false;
                Assert.That(Ids(), Does.Contain(fighter.Id), "flag OFF: a docked craft is still enrolled (byte-identical)");

                // Flag ON: the docked fighter is held in the hangar; the carrier still fights.
                CombatEngagement.EnableCarrierSortie = true;
                Assert.That(Ids(), Does.Not.Contain(fighter.Id), "flag ON: a DOCKED fighter is held in reserve, not a combatant");
                Assert.That(Ids(), Does.Contain(carrier.Id), "the carrier itself still fights");

                // Undock = LAUNCH: the fighter rejoins the fight.
                Assert.That(DockTools.Undock(carrier, fighter), Is.True, "undock the fighter (launch)");
                Assert.That(DockTools.IsDocked(fighter), Is.False, "no longer docked");
                Assert.That(Ids(), Does.Contain(fighter.Id), "undock = launch: the fighter is a combatant again");
                Log("docked fighter held in hangar (flag on); undock relaunches it into the fight");
            }
            finally { CombatEngagement.EnableCarrierSortie = saved; }
        }

        private const string FuelTank = "default-design-fuel-tank-1000";   // a fuel-storage hold (the Wasp has none)

        private static ComponentDesign FuelTankDesign(TestScenario s)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(FuelTank), Is.True, $"'{FuelTank}' should be built for the start faction");
            return designs[FuelTank];
        }

        /// <summary>Units of the ship's own fuel type currently stored aboard (resolved the same way DockTools does).</summary>
        private static long FuelUnits(TestScenario s, Entity ship)
        {
            if (!ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var thrust) || string.IsNullOrEmpty(thrust.FuelType)) return 0;
            if (!ship.TryGetDataBlob<CargoStorageDB>(out var cargo)) return 0;
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var fuel = fi.Data.CargoGoods.GetAny(thrust.FuelType) ?? fi.Data.LockedCargoGoods.GetAny(thrust.FuelType);
            return fuel == null ? 0 : cargo.GetUnitsStored(fuel, false);
        }

        [Test]
        [Description("E12 slice 2 — REARM ON RECOVERY: DockTools.RefuelFromCarrier tops off a recovered craft's fuel "
                     + "from the CARRIER's own stock. The craft's fuel rises and the carrier's drops by exactly the same "
                     + "amount (conservation); a second pass adds no more once the tank is full (take-what's-available); "
                     + "a full craft draws nothing (no-op). Driven directly (no berth door) so the transfer is proven "
                     + "deterministically. Carrier + parasite share ONE light design → the same fuel type, so the "
                     + "transfer can never silently mismatch materials.")]
        public void RefuelFromCarrier_MovesFuel_ConservesIt_TakesWhatFits()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var designs = factionInfo.ShipDesigns.Values.ToList();
            var lightDesign = Lightest(s, designs);   // both hulls share this design → identical fuel type

            // Carrier: the light hull + a fuel-storage hold, filled so it has fuel to hand out.
            var carrier = Spawn(s, lightDesign, "Carrier");
            carrier.AddComponent(FuelTankDesign(s));
            ShipFactory.FillFuelTanks(carrier, factionInfo);
            long carrierBefore = FuelUnits(s, carrier);
            Assert.That(carrierBefore, Is.GreaterThan(0), "the carrier holds fuel to hand out");

            // Parasite: the SAME light hull + a fuel-storage hold (freshly built → empty tank).
            var fighter = Spawn(s, lightDesign, "Fighter");
            fighter.AddComponent(FuelTankDesign(s));
            long fighterBefore = FuelUnits(s, fighter);
            Assert.That(fighterBefore, Is.EqualTo(0), "a freshly built parasite starts with empty tanks");

            // NO-OP: a FULL craft (the carrier itself) draws nothing — the take-what-fits guard, and it never throws.
            DockTools.RefuelFromCarrier(carrier, carrier);
            Assert.That(FuelUnits(s, carrier), Is.EqualTo(carrierBefore), "a full/self craft draws nothing (no-op)");

            // THE TRANSFER: fuel flows carrier → parasite, conserved.
            DockTools.RefuelFromCarrier(carrier, fighter);
            long fighterAfter = FuelUnits(s, fighter);
            long carrierAfter = FuelUnits(s, carrier);
            Log($"rearm: fighter {fighterBefore}→{fighterAfter}, carrier {carrierBefore}→{carrierAfter}");
            Assert.That(fighterAfter, Is.GreaterThan(fighterBefore), "the recovered craft gained fuel from the carrier");
            Assert.That(carrierAfter, Is.LessThan(carrierBefore), "the carrier's fuel dropped by what it handed over");
            Assert.That(carrierBefore - carrierAfter, Is.EqualTo(fighterAfter - fighterBefore),
                "conservation: exactly what the carrier lost, the craft gained");

            // TAKE-WHAT'S-AVAILABLE: a second pass adds nothing more than free space allows (never a duplication).
            long fighterFull = FuelUnits(s, fighter);
            DockTools.RefuelFromCarrier(carrier, fighter);
            Assert.That(FuelUnits(s, fighter), Is.LessThanOrEqualTo(fighterFull + 1),
                "a second rearm tops off no further than free space allows (bounded)");
        }

        [Test]
        [Description("The rearm is FLAG-GATED at TryDock: with EnableCarrierRearm ON a docked craft is refuelled; with "
                     + "it OFF the dock only re-parents the position and the craft's fuel is unchanged (byte-identical). "
                     + "Assume-guarded on the berth door — if the parasite+tank is too big for the 60t berth this is "
                     + "inconclusive, not a failure (the door math is DockBayTests' job; this gauge is the flag).")]
        public void TryDock_RefuelsOnlyWhenRearmFlagOn()
        {
            long DockAndReadFuel(bool rearmOn, out bool docked)
            {
                var s = TestScenario.CreateWithColony();
                var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
                var designs = factionInfo.ShipDesigns.Values.ToList();
                var lightDesign = Lightest(s, designs);   // carrier + parasite share the design → identical fuel type

                var carrier = Spawn(s, lightDesign, "Carrier");
                carrier.AddComponent(BayDesign(s));         // the bay makes it a carrier
                carrier.AddComponent(FuelTankDesign(s));    // a hold to hand fuel from
                ShipFactory.FillFuelTanks(carrier, factionInfo);
                var fighter = Spawn(s, lightDesign, "Fighter");
                fighter.AddComponent(FuelTankDesign(s));

                DockTools.EnableCarrierRearm = rearmOn;
                try
                {
                    docked = DockTools.TryDock(carrier, fighter, out _);
                    return docked ? FuelUnits(s, fighter) : -1;
                }
                finally { DockTools.EnableCarrierRearm = false; }
            }

            long onFuel = DockAndReadFuel(true, out bool fitOn);
            Assume.That(fitOn, "the parasite+tank must fit the 60t berth to exercise the rearm-on-dock path");
            long offFuel = DockAndReadFuel(false, out bool fitOff);
            Assume.That(fitOff, "the parasite+tank must fit the 60t berth");

            Assert.That(onFuel, Is.GreaterThan(0), "flag ON: docking refuelled the recovered craft");
            Assert.That(offFuel, Is.EqualTo(0), "flag OFF: docking left the craft's fuel untouched (byte-identical)");
            Log($"flag-gated rearm on dock — on={onFuel} off={offFuel}");
        }
    }
}
