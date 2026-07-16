using NUnit.Framework;
using Pulsar4X.Combat;      // ShipCombatValueDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;    // FactionInfoDB, FactionState, MilitaryComposition
using Pulsar4X.Fleets;      // FleetDB, FleetAssembly, FleetCompositionDB
using Pulsar4X.Ships;       // ShipFactory, ShipDesign, ShipInfoDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Fleet-composition slice 2 — the AI assembles its BUILT warships into ONE holistic FORMING fleet.
    ///
    /// The gap: when an NPC finishes a warship, <c>ShipDesign.OnConstructionComplete</c> parks it flat under the
    /// faction ROOT fleet (which lives in the GlobalManager, not a star system). But the offensive logic
    /// (<see cref="MilitaryComposition.ReadyStrikeFleet"/>) only sees in-SYSTEM fleets — so a loose warship is
    /// invisible and the strike fleet never masses. These gauges drive <see cref="FleetAssembly.AssembleBuiltWarships"/>
    /// directly (no sim advance): loose warships fold into a real, faction-parented, flagship-bearing in-system fleet
    /// tagged with a <see cref="FleetCompositionDB"/>; a second sweep GROWS that fleet rather than starting a new one;
    /// and with no loose warships it's a no-op (the byte-safety tripwire — the NPC processor only calls it behind the
    /// default-off EnableOrderEmission gate).
    /// </summary>
    [TestFixture]
    public class FleetAssemblyTests
    {
        /// <summary>First armed design the faction can build (mounts a beam or railgun) — the hull we "build".</summary>
        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>Build a warship and park it LOOSE under the faction root fleet — exactly what
        /// <c>ShipDesign.OnConstructionComplete</c> does when an NPC's build finishes.</summary>
        private static Entity BuildLooseWarship(TestScenario s, ShipDesign design)
        {
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody); // lands in the system + gets its ShipCombatValueDB
            s.Faction.GetDataBlob<FleetDB>().AddChild(ship);                       // flat child of the ROOT fleet (the missing link)
            return ship;
        }

        /// <summary>How many ARMED warships sit LOOSE (flat, not in a sub-fleet) under the faction root fleet.</summary>
        private static int LooseArmedUnderRoot(TestScenario s)
        {
            int n = 0;
            foreach (var c in s.Faction.GetDataBlob<FleetDB>().GetChildren())
                if (c != null && c.IsValid && !c.HasDataBlob<FleetDB>() && c.HasDataBlob<ShipInfoDB>()
                    && c.TryGetDataBlob<ShipCombatValueDB>(out var cv) && cv.Firepower > 0)
                    n++;
            return n;
        }

        /// <summary>The faction's forming fleets (tagged <see cref="FleetCompositionDB"/>) in the starting system.</summary>
        private static int FormingFleetCount(TestScenario s)
        {
            int n = 0;
            foreach (var f in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                if (f.FactionOwnerID == s.Faction.Id) n++;
            return n;
        }

        private static Entity TheFormingFleet(TestScenario s)
        {
            foreach (var f in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                if (f.FactionOwnerID == s.Faction.Id) return f;
            return null;
        }

        [Test]
        [Description("Loose built warships fold into ONE real in-system fleet (flagship set, tagged, Deployable) that the strike logic can now find.")]
        public void AssembleBuiltWarships_FoldsLooseWarshipsIntoADeployableFleet()
        {
            var s = TestScenario.CreateWithColony();
            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null, "the start faction has an armed design to build");

            for (int i = 0; i < 3; i++) BuildLooseWarship(s, design);
            Assert.That(LooseArmedUnderRoot(s), Is.EqualTo(3), "the 3 built warships start LOOSE under the faction root fleet");
            Assert.That(FormingFleetCount(s), Is.EqualTo(0), "no forming fleet exists yet");

            int moved = FleetAssembly.AssembleBuiltWarships(s.Faction);

            Assert.That(moved, Is.EqualTo(3), "all 3 loose warships were folded into a fleet");
            Assert.That(LooseArmedUnderRoot(s), Is.EqualTo(0), "no armed warship is left loose under the root fleet");
            Assert.That(FormingFleetCount(s), Is.EqualTo(1), "exactly ONE forming fleet was created");

            var fleet = TheFormingFleet(s);
            Assert.That(fleet.GetDataBlob<FleetDB>().FlagShipID, Is.GreaterThanOrEqualTo(0),
                "the forming fleet has a flagship (MoveToSystemBodyOrder needs FlagShipID != -1)");
            Assert.That(MilitaryComposition.WarshipCount(fleet), Is.EqualTo(3), "the forming fleet holds all 3 warships");
            Assert.That(fleet.GetDataBlob<FleetCompositionDB>().Deployed, Is.True,
                "crossing the min-to-deploy core (3) flips the fleet Deployable");

            // THE UNBLOCK: the massed fleet is now visible to the offensive AI (it was invisible while loose under root).
            var ready = MilitaryComposition.ReadyStrikeFleet(FactionState.Snapshot(s.Faction));
            Assert.That(ready.IsValid, Is.True, "the assembled fleet is now a READY strike group the AI can find + sail");
            Assert.That(MilitaryComposition.WarshipCount(ready), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        [Description("A later sweep GROWS the existing under-strength forming fleet rather than starting a second one.")]
        public void AssembleBuiltWarships_GrowsTheExistingFormingFleet_NotANewOne()
        {
            var s = TestScenario.CreateWithColony();
            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null);

            for (int i = 0; i < 3; i++) BuildLooseWarship(s, design);
            FleetAssembly.AssembleBuiltWarships(s.Faction);
            Assert.That(FormingFleetCount(s), Is.EqualTo(1), "the first sweep formed ONE fleet");
            int firstFleetId = TheFormingFleet(s).Id;

            // Aim the fleet at its IDEAL size (target 8) so the next hulls GROW it rather than overflowing into a reserve
            // (a fleet fills to its aspiration target, then overflows into a second fleet — the reserve seam).
            FleetAssembly.SetAspiration(s.Faction, FleetCompositionTier.Ideal);

            // two more warships roll off the line next cycle
            for (int i = 0; i < 2; i++) BuildLooseWarship(s, design);
            FleetAssembly.AssembleBuiltWarships(s.Faction);

            Assert.That(FormingFleetCount(s), Is.EqualTo(1), "the second sweep GREW the existing fleet (still below the Ideal target), not started a new one");
            var fleet = TheFormingFleet(s);
            Assert.That(fleet.Id, Is.EqualTo(firstFleetId), "it is the SAME forming fleet");
            Assert.That(MilitaryComposition.WarshipCount(fleet), Is.EqualTo(5), "the grown fleet now holds all 5 warships");
        }

        [Test]
        [Description("With no loose warships (the default state), assembly is a no-op — no fleet is created (byte-safe).")]
        public void AssembleBuiltWarships_NoLooseWarships_IsANoOp()
        {
            var s = TestScenario.CreateWithColony();

            int moved = FleetAssembly.AssembleBuiltWarships(s.Faction);

            Assert.That(moved, Is.EqualTo(0), "nothing loose to assemble");
            Assert.That(FormingFleetCount(s), Is.EqualTo(0), "no forming fleet is created when there are no loose warships");
        }
    }
}
