using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;    // FactionInfoDB, FactionState, ConquerResolver, StrategicObjective(DB)
using Pulsar4X.Fleets;      // FleetAssembly, FleetCompositionTier, FleetCompositionDB, FleetDB
using Pulsar4X.Ships;       // ShipFactory, ShipDesign

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Fleet-composition slice 3 — the AI sizes its fleet to its RESOURCES + war footing AND keeps a home-defense
    /// RESERVE (the developer's "don't pull all fleets at the same time" — a placeholder for the system military
    /// commander delegate). Gauges: (1) the pure aspiration decision scales with treasury + bumps a tier at war; (2) the
    /// massing rung keeps building PAST one fleet until it has a strike fleet AND a reserve, then stops; (3) the reserve
    /// guard won't let the AI commit its LAST organized combat fleet on an offensive.
    /// </summary>
    [TestFixture]
    public class FleetCompositionEscalationTests
    {
        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>Build <paramref name="n"/> warships loose under the root fleet then assemble them — since a single
        /// AssembleBuiltWarships call folds a whole batch into ONE fleet, this yields one fresh organized fleet of n.</summary>
        private static void AssembleFleetOf(TestScenario s, ShipDesign design, int n)
        {
            for (int i = 0; i < n; i++)
                s.Faction.GetDataBlob<FleetDB>().AddChild(ShipFactory.CreateShip(design, s.Faction, s.StartingBody));
            FleetAssembly.AssembleBuiltWarships(s.Faction);
        }

        private static int OrganizedFleetCount(TestScenario s)
        {
            int n = 0;
            foreach (var f in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                if (f.FactionOwnerID == s.Faction.Id) n++;
            return n;
        }

        private static Entity FirstOrganizedFleet(TestScenario s)
        {
            foreach (var f in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                if (f.FactionOwnerID == s.Faction.Id) return f;
            return null;
        }

        private static string ResolveConquerKind(TestScenario s)
        {
            var state = FactionState.Snapshot(s.Faction);
            return new ConquerResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Conquer }).Kind;
        }

        [Test]
        [Description("The resource-gated aspiration scales with treasury; being at war bumps the aim up one tier.")]
        public void AspirationFor_ScalesWithTreasuryAndWar()
        {
            Assert.That(FleetAssembly.AspirationFor(0m, false), Is.EqualTo(FleetCompositionTier.Deployable),
                "broke + at peace → just the deployable core");
            Assert.That(FleetAssembly.AspirationFor(FleetAssembly.IdealWealth, false), Is.EqualTo(FleetCompositionTier.Ideal),
                "money in the bank → grow to the ideal configuration");
            Assert.That(FleetAssembly.AspirationFor(FleetAssembly.PerfectWealth, false), Is.EqualTo(FleetCompositionTier.Perfect),
                "plentiful resources → the perfect configuration");
            Assert.That(FleetAssembly.AspirationFor(0m, true), Is.EqualTo(FleetCompositionTier.Ideal),
                "at war, even broke → field at least an ideal fleet");
            Assert.That(FleetAssembly.AspirationFor(FleetAssembly.IdealWealth, true), Is.EqualTo(FleetCompositionTier.Perfect),
                "at war + money → the perfect configuration");
            Assert.That(FleetAssembly.AspirationFor(FleetAssembly.PerfectWealth, true), Is.EqualTo(FleetCompositionTier.Perfect),
                "already perfect → stays perfect (no tier above)");
        }

        [Test]
        [Description("With only ONE complete fleet and no reserve, the AI KEEPS massing — it builds a reserve before it's done.")]
        public void Conquer_KeepsMassing_ForAReserve_WhenOnlyOneFleetIsComplete()
        {
            var s = TestScenario.CreateWithColony();
            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null);

            AssembleFleetOf(s, design, 3);                                     // one fleet at the Deployable core, NO reserve
            FleetAssembly.SetAspiration(s.Faction, FleetCompositionTier.Deployable);
            Assert.That(OrganizedFleetCount(s), Is.EqualTo(1), "one organized fleet so far");

            Assert.That(ResolveConquerKind(s), Is.EqualTo("QueueWarship"),
                "a single complete fleet with no reserve → keep massing (build the home-defense reserve, don't commit everything)");
        }

        [Test]
        [Description("Once there's a complete strike fleet AND a deployable reserve, the AI STOPS massing (sized).")]
        public void Conquer_StopsMassing_WhenAStrikeFleetAndAReserveBothExist()
        {
            var s = TestScenario.CreateWithColony();
            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null);

            AssembleFleetOf(s, design, 3);                                     // fleet #1 (the strike group)
            FleetAssembly.SetAspiration(s.Faction, FleetCompositionTier.Deployable);
            AssembleFleetOf(s, design, 3);                                     // fleet #1 is complete → this overflows into fleet #2 (the reserve)
            FleetAssembly.SetAspiration(s.Faction, FleetCompositionTier.Deployable);
            Assert.That(OrganizedFleetCount(s), Is.EqualTo(2), "the overflow formed a SECOND fleet (the reserve)");

            Assert.That(ResolveConquerKind(s), Is.Not.EqualTo("QueueWarship"),
                "a complete strike fleet + a reserve fleet → the AI is sized and stops massing");
        }

        [Test]
        [Description("The reserve guard won't let the AI commit its LAST organized fleet; a reserve fleet unlocks the strike.")]
        public void ReserveGuard_HoldsTheLastOrganizedFleet_ReleasesWhenAReserveExists()
        {
            var s = TestScenario.CreateWithColony();
            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null);

            AssembleFleetOf(s, design, 3);
            var strike = FirstOrganizedFleet(s);
            Assert.That(strike, Is.Not.Null);

            // ONE organized fleet → committing it would strip home → the guard holds it.
            Assert.That(ConquerResolver.HasHomeReserve(FactionState.Snapshot(s.Faction), strike), Is.False,
                "the AI won't send its LAST organized combat fleet on an offensive");

            // Add a second organized fleet (the reserve) → the strike is released.
            FleetAssembly.SetAspiration(s.Faction, FleetCompositionTier.Deployable);
            AssembleFleetOf(s, design, 3);
            Assert.That(OrganizedFleetCount(s), Is.EqualTo(2));
            Assert.That(ConquerResolver.HasHomeReserve(FactionState.Snapshot(s.Faction), strike), Is.True,
                "with a home reserve fleet standing by, the strike fleet may sail");

            // A hand-made / non-organized fleet (no FleetCompositionDB — e.g. a player or sandbox fleet) isn't gated,
            // so existing tests that mass a CombatSandbox fleet still sail as before (byte-identity).
            var plainFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Plain Fleet");
            Assert.That(ConquerResolver.HasHomeReserve(FactionState.Snapshot(s.Faction), plainFleet), Is.True,
                "a fleet with no composition memo is not subject to the reserve doctrine");
        }
    }
}
