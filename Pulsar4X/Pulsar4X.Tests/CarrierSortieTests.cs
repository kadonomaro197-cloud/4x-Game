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
using Pulsar4X.Ships;

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
    }
}
