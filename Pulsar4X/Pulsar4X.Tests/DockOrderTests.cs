using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Docking;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;   // the LIVE PositionDB (the one in Engine/Datablobs is commented out entirely)
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE DOCK ORDER — the queued wrapper that lets a player OR the AI ask a carrier to berth/release a whole vessel
    /// through the ONE order path both seats share (the developer's law). <see cref="DockTools"/> (the capability) is
    /// already gauged by <see cref="DockBayTests"/>; this fixture proves the ORDER on top of it does the right thing:
    /// it validates (own the carrier), and its <c>Execute</c> actually docks / undocks + re-parents.
    ///
    /// <para>Drives the order the way the OrderableProcessor does — <c>IsValidCommand</c> then <c>Execute</c> — reachable
    /// from the test assembly via <c>InternalsVisibleTo</c>. The assertions measure real spawned ships and real installed
    /// bays, so re-tuning a hull or a bay can't make the fixture lie.</para>
    /// </summary>
    [TestFixture]
    public class DockOrderTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[dock-order] " + m);

        private const string HeavyBerth = "default-design-heavy-berth";   // one wide 60 t door — anything that fits at all fits

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True,
                $"'{id}' should be built for the start faction (template id in StartingItems, design id in ComponentDesigns)");
            return designs[id];
        }

        private static Entity Spawn(TestScenario s, ShipDesign design, string name)
            => ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);

        private static void InstallBay(Entity host, ComponentDesign bay) => host.AddComponent(bay);

        private static double Mass(Entity e)
            => e.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.MassTotal : 0;

        /// <summary>The lightest shipped design by real spawned mass (weighs each design exactly once).</summary>
        private static ShipDesign Lightest(TestScenario s, List<ShipDesign> designs)
        {
            ShipDesign best = designs[0];
            double bestMass = double.MaxValue;
            foreach (var d in designs)
            {
                double m = Mass(Spawn(s, d, "Weigh " + d.Name));
                if (m > 0 && m < bestMass) { bestMass = m; best = d; }
            }
            return best;
        }

        /// <summary>
        /// The happy path: a valid Dock order berths the ship and re-parents it to the carrier — the same consequence
        /// DockTools guarantees, now reached through the order wrapper the player/AI actually issue.
        /// </summary>
        [Test]
        [Description("A Dock order from the carrier's owner validates and, when executed, berths the ship in the carrier and re-parents its position to the carrier — so it travels with it. The order is the one verb both seats issue.")]
        public void DockOrder_FromOwner_Validates_AndBerthsAndReParents()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            Assert.That(designs, Is.Not.Empty, "the start faction has ship designs to spawn");

            var carrier = Spawn(s, designs[0], "Carrier");
            InstallBay(carrier, Design(s, HeavyBerth));
            var craft = Spawn(s, Lightest(s, designs), "Small Craft");

            var cPos = carrier.GetDataBlob<PositionDB>();
            var fPos = craft.GetDataBlob<PositionDB>();
            Assert.That(fPos.Parent?.Id, Is.Not.EqualTo(carrier.Id), "it starts out not parented to the carrier");

            var order = DockOrder.Dock(s.Faction.Id, carrier, craft);
            Assert.That(order.IsValidCommand(s.Game), Is.True, "the carrier's owner may issue the dock");
            order.Execute(carrier.StarSysDateTime);

            Assert.That(order.GetIsFinished, Is.True, "an instant order completes on execute");
            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.True, "the carrier now records the craft as docked");
            Assert.That(fPos.Parent?.Id, Is.EqualTo(carrier.Id),
                "🔑 its position hangs off the carrier — that is 'travels with it', reached through the order");
            Log($"docked via order · used {DockTools.Used(carrier):N0} kg of {DockTools.Capacity(carrier):N0} kg");
        }

        /// <summary>The Undock order is the return trip: after a docked craft, an Undock order releases it ALIVE and hands
        /// its position back to the carrier's own SOI.</summary>
        [Test]
        [Description("After docking through the order, an Undock order releases the craft — it is no longer recorded aboard, it is still valid (released, not destroyed), and its position is handed back to whatever the carrier orbits.")]
        public void UndockOrder_ReleasesTheCraft_Alive()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var carrier = Spawn(s, designs[0], "Carrier");
            InstallBay(carrier, Design(s, HeavyBerth));
            var craft = Spawn(s, Lightest(s, designs), "Small Craft");

            DockOrder.Dock(s.Faction.Id, carrier, craft).Execute(carrier.StarSysDateTime);
            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.True, "precondition: it is docked");

            var cPos = carrier.GetDataBlob<PositionDB>();
            var fPos = craft.GetDataBlob<PositionDB>();

            var undock = DockOrder.Undock(s.Faction.Id, carrier, craft);
            Assert.That(undock.IsValidCommand(s.Game), Is.True);
            undock.Execute(carrier.StarSysDateTime);

            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.False, "the carrier no longer holds it");
            Assert.That(craft.IsValid, Is.True, "released, NOT destroyed");
            Assert.That(fPos.Parent?.Id, Is.EqualTo(cPos.Parent?.Id),
                "handed back to whatever the carrier itself orbits");
            Assert.That(DockTools.Used(carrier), Is.EqualTo(0), "and the berth is free again");
            Log("released via order · berth free again");
        }

        /// <summary>The validity gate: an order whose requesting faction does not own the carrier is refused — you cannot
        /// berth into someone else's hangar.</summary>
        [Test]
        [Description("A Dock order whose requesting faction does not own the carrier fails IsValidCommand, so a foreign or bogus faction cannot dock into a carrier it does not own.")]
        public void DockOrder_FromNonOwner_IsRefused()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var carrier = Spawn(s, designs[0], "Carrier");
            InstallBay(carrier, Design(s, HeavyBerth));
            var craft = Spawn(s, Lightest(s, designs), "Small Craft");

            // A faction id that is not the carrier's owner (and almost certainly not in the game at all) — either gate
            // (unknown faction OR non-owner) must refuse it; both are the correct answer for an order nobody authorised.
            int notTheOwner = s.Faction.Id + 987654;
            var order = DockOrder.Dock(notTheOwner, carrier, craft);
            Assert.That(order.IsValidCommand(s.Game), Is.False, "a non-owning faction may not dock into this carrier");
            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.False, "and nothing was berthed");
            Log("refused a non-owner dock order");
        }

        /// <summary>The additive guarantee: a Dock order against a hull with NO bay is a safe no-op — nothing berths,
        /// no blob is attached — so every existing design in the game stays byte-identical.</summary>
        [Test]
        [Description("A Dock order aimed at a carrier with no docking bay executes as a harmless no-op: nothing is berthed and the hull is never given a docked-ships blob just for being asked — so stock ships (none of which mount a bay) are untouched.")]
        public void DockOrder_AgainstNoBayHull_IsASafeNoOp()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var plain = Spawn(s, designs[0], "Plain");   // NO bay installed
            var other = Spawn(s, designs[0], "Other");

            var order = DockOrder.Dock(s.Faction.Id, plain, other);
            Assert.That(order.IsValidCommand(s.Game), Is.True, "the order is well-formed (owner-issued) even if it can't berth");
            order.Execute(plain.StarSysDateTime);

            Assert.That(DockTools.IsDockedIn(plain, other), Is.False, "no bay → nothing berths");
            Assert.That(plain.HasDataBlob<DockedShipsDB>(), Is.False,
                "and asking never attaches the registry blob — a refused dock must not mutate the hull");
            Log("no-bay dock order was a safe no-op");
        }
    }
}
