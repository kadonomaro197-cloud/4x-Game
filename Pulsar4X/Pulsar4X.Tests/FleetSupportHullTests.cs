using System;
using NUnit.Framework;
using Pulsar4X.Combat;      // ShipCombatValueDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;    // FactionInfoDB, FactionState, MilitaryComposition, ConquerResolver
using Pulsar4X.Fleets;      // FleetDB, FleetAssembly, FleetCompositionDB, FleetRole, FleetRoleComposer
using Pulsar4X.Ships;       // ShipFactory, ShipDesign, ShipInfoDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Support/utility hulls — the AI now FIELDS the unarmed TENDERS it used to build and never use.
    ///
    /// The gap: a built support hull (an unarmed fleet oiler/collier — the base-mod Freighter with its cargo-transfer
    /// shuttlebay) gets parked flat under the faction ROOT fleet by <c>ShipDesign.OnConstructionComplete</c>, exactly
    /// like a warship — but <see cref="FleetAssembly.AssembleBuiltWarships"/> only sweeps ARMED hulls (Firepower &gt; 0),
    /// so the tender was left loose forever, never in a fleet, never used. <see cref="FleetAssembly.AssembleSupportHulls"/>
    /// closes that: a loose tender folds into the faction's strike fleet as a SUPPORT member (an unarmed hull classifies
    /// as <see cref="FleetRole.Support"/>), so it travels with the fleet.
    ///
    /// These gauges drive the assembly directly (no sim advance) and prove: (a) a built tender is recognised + folded
    /// into the strike fleet without changing the armed count, (b) with no fleet to escort the tender stays loose, and
    /// (c) with no tender the support pass is a no-op — the byte-safety tripwire (the NPC processor only calls it behind
    /// the default-off EnableOrderEmission gate, and it must not perturb the ConquerResolver/MilitaryComposition path).
    /// </summary>
    [TestFixture]
    public class FleetSupportHullTests
    {
        /// <summary>First armed design the faction can build (mounts a beam or railgun) — the hull we mass.</summary>
        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>First UNARMED design that carries a field cargo-transfer ability (a fleet TENDER — the base-mod
        /// Freighter, whose shuttlebay binds a <see cref="Pulsar4X.Storage.CargoTransferAtb"/>). A plain Cargo Courier
        /// (storage only, no transfer) and the Surveyor are deliberately excluded.</summary>
        private static ShipDesign FirstTenderDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (!ConquerResolver.IsWarship(d) && d.TryGetComponentsByAttribute<Pulsar4X.Storage.CargoTransferAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>Build a ship and park it LOOSE under the faction root fleet — exactly what
        /// <c>ShipDesign.OnConstructionComplete</c> does when an NPC's build finishes.</summary>
        private static Entity BuildLoose(TestScenario s, ShipDesign design)
        {
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody);
            s.Faction.GetDataBlob<FleetDB>().AddChild(ship);
            return ship;
        }

        /// <summary>How many loose (flat, not in a sub-fleet) children of the faction root fleet match <paramref name="pred"/>.</summary>
        private static int LooseUnderRoot(TestScenario s, Func<Entity, bool> pred)
        {
            int n = 0;
            foreach (var c in s.Faction.GetDataBlob<FleetDB>().GetChildren())
                if (c != null && c.IsValid && !c.HasDataBlob<FleetDB>() && pred(c)) n++;
            return n;
        }

        private static Entity TheFormingFleet(TestScenario s)
        {
            foreach (var f in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                if (f.FactionOwnerID == s.Faction.Id) return f;
            return null;
        }

        [Test]
        [Description("A built TENDER (unarmed + cargo-transfer) folds into the strike fleet as a Support member — fielded, "
                   + "not left loose — WITHOUT changing the armed count (a tender is never a warship).")]
        public void AssembleSupportHulls_FoldsTenderIntoTheStrikeFleet_AsSupport()
        {
            var s = TestScenario.CreateWithColony();
            var warship = FirstWarshipDesign(s.Faction);
            var tenderDesign = FirstTenderDesign(s.Faction);
            Assert.That(warship, Is.Not.Null, "the start faction has an armed design to mass");
            Assert.That(tenderDesign, Is.Not.Null, "the start faction has an unarmed tender design (Freighter w/ shuttlebay)");

            for (int i = 0; i < 3; i++) BuildLoose(s, warship);
            var tender = BuildLoose(s, tenderDesign);
            Assert.That(FleetAssembly.IsSupportHull(tender), Is.True,
                "the built tender reads as a support hull (unarmed + a field cargo-transfer ability, not a troop transport)");

            // Warships assemble first (creating the forming fleet), THEN the tenders escort it — the live call order.
            FleetAssembly.AssembleBuiltWarships(s.Faction);
            var fleet = TheFormingFleet(s);
            Assert.That(fleet, Is.Not.Null, "the warships formed a strike fleet");
            int armedBefore = MilitaryComposition.WarshipCount(fleet);

            int moved = FleetAssembly.AssembleSupportHulls(s.Faction);

            Assert.That(moved, Is.EqualTo(1), "the loose tender was folded into a fleet");
            Assert.That(LooseUnderRoot(s, c => FleetAssembly.IsSupportHull(c)), Is.EqualTo(0),
                "no tender is left loose under the root fleet");

            // The tender now travels WITH the strike fleet, classifies as Support, and did NOT change the armed count.
            bool tenderInFleet = false;
            foreach (var child in fleet.GetDataBlob<FleetDB>().GetChildren())
                if (child != null && child.Id == tender.Id) tenderInFleet = true;
            Assert.That(tenderInFleet, Is.True, "the tender is a child of the strike fleet (it travels with it)");
            Assert.That(FleetRoleComposer.ClassifyRole(tender), Is.EqualTo(FleetRole.Support),
                "an unarmed tender classifies as a Support-role hull");
            Assert.That(MilitaryComposition.WarshipCount(fleet), Is.EqualTo(armedBefore),
                "a tender is NOT counted a warship — the strike threshold / Deployed latch are unchanged");

            // THE UNBLOCK stays intact: the fleet is still a ready strike group (the tender didn't break the mass read).
            var ready = MilitaryComposition.ReadyStrikeFleet(FactionState.Snapshot(s.Faction));
            Assert.That(ready.IsValid, Is.True, "the fleet with its tender is still a ready strike group");
        }

        [Test]
        [Description("With no strike fleet formed yet, a loose tender is LEFT LOOSE (a tender needs a fleet to escort) — "
                   + "a byte-safe no-op.")]
        public void AssembleSupportHulls_NoStrikeFleet_LeavesTenderLoose()
        {
            var s = TestScenario.CreateWithColony();
            var tenderDesign = FirstTenderDesign(s.Faction);
            Assert.That(tenderDesign, Is.Not.Null);

            // Build a tender but assemble NO warships → no FleetCompositionDB fleet exists to escort it.
            BuildLoose(s, tenderDesign);

            int moved = FleetAssembly.AssembleSupportHulls(s.Faction);

            Assert.That(moved, Is.EqualTo(0), "no forming (FleetCompositionDB) fleet → the tender is left loose");
            Assert.That(LooseUnderRoot(s, c => FleetAssembly.IsSupportHull(c)), Is.EqualTo(1),
                "the tender is still loose under the root fleet");
        }

        [Test]
        [Description("With only warships (no tender built), the support pass is a no-op — the byte-safety tripwire that "
                   + "keeps the ConquerResolver/MilitaryComposition path unchanged.")]
        public void AssembleSupportHulls_NoTender_IsANoOp()
        {
            var s = TestScenario.CreateWithColony();
            var warship = FirstWarshipDesign(s.Faction);
            Assert.That(warship, Is.Not.Null);

            for (int i = 0; i < 3; i++) BuildLoose(s, warship);
            FleetAssembly.AssembleBuiltWarships(s.Faction);

            int moved = FleetAssembly.AssembleSupportHulls(s.Faction);
            Assert.That(moved, Is.EqualTo(0), "no tender built → nothing for the support pass to fold");
        }
    }
}
