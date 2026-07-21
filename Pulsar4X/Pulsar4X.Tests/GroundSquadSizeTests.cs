using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Factions;      // FactionInfoDB
using Pulsar4X.GroundCombat;  // GroundUnitAssembly, GroundForcesDB
using Pulsar4X.Industry;      // IndustryJob
using Pulsar4X.Storage;       // CargoStorageDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SQUAD SIZE — the Entity Assembler's "units per build" dial (the developer's ask: a dial that dictates how many
    /// units are made when the design is built at a production facility). One build of a squad-of-N design raises N
    /// GroundUnits into the muster region, and the build COST + build-TIME scale with N so there's no free
    /// multiplication (CONVENTIONS §16). Default 1 → exactly one unit, byte-identical (every existing design + old save).
    /// Engine-only → CI. Drives <c>OnConstructionComplete</c> directly (the deterministic fielding idiom of
    /// PlayerGroundChainRailsTests / GroundUnitFieldingTests, gotcha 7).
    /// </summary>
    [TestFixture]
    public class GroundSquadSizeTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[squad-size] " + m);

        [Test]
        [Description("A squad-of-5 assembled design: one build raises FIVE units (default design raises ONE), and its "
                   + "build cost + time are exactly 5× the single-unit design's — the dial scales cost, no free units.")]
        public void SquadSize_OneBuildRaisesNUnits_AndCostsNTimes()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) };

            // the SAME parts assembled two ways: one unit per build, and a squad of five per build.
            var solo = GroundUnitAssembly.RegisterAssembledDesign(faction, "sq-solo", "Lone Trooper", frame, parts);      // default squad 1
            var squad = GroundUnitAssembly.RegisterAssembledDesign(faction, "sq-five", "Rifle Squad", frame, parts, 5);   // squad of five

            Assert.That(solo.UnitsPerBuild, Is.EqualTo(1), "default squad size is 1 (byte-identical)");
            Assert.That(squad.UnitsPerBuild, Is.EqualTo(5), "the dial sets the squad size");

            // COST scales with the squad — no free multiplication (CONVENTIONS §16).
            Assert.That(squad.IndustryPointCosts, Is.EqualTo(solo.IndustryPointCosts * 5), "build-time is 5× for a squad of 5");
            Assert.That(solo.ResourceCosts.Count, Is.GreaterThan(0), "precondition: the design has material costs to compare");
            foreach (var kv in solo.ResourceCosts)
                Assert.That(squad.ResourceCosts[kv.Key], Is.EqualTo(kv.Value * 5), "material " + kv.Key + " costs 5× for the squad");

            var body = s.StartingBody;
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();

            // ONE build completion of the squad design raises FIVE units (RaiseUnit creates the roster on demand).
            squad.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, squad.UniqueID), squad);
            var forces = body.GetDataBlob<GroundForcesDB>();
            int raised = forces.Units.Count(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == squad.UniqueID);
            Assert.That(raised, Is.EqualTo(5), "one build of a squad-of-5 design fields five units into the muster region");

            // the default (squad 1) design raises exactly ONE — byte-identical to the pre-dial behaviour.
            solo.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, solo.UniqueID), solo);
            int soloRaised = forces.Units.Count(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == solo.UniqueID);
            Assert.That(soloRaised, Is.EqualTo(1), "a default design raises exactly one unit per build (byte-identical)");
            Log($"squad of 5 → {raised} units in one build (time {squad.IndustryPointCosts} = 5× {solo.IndustryPointCosts}); solo → {soloRaised}");
        }

        /// <summary>
        /// THE COMBAT INVARIANT the developer locked: "100 individual-order zerglings is the same as 100 bulk
        /// single-instance-built zerglings — this becomes important during combat." Bulk-built and individually-built
        /// units must be INDISTINGUISHABLE to the engine. The dial scales only build COST + build-TIME (proved above);
        /// it must never touch a per-unit combat stat. This gauge builds the SAME parts two ways — one solo unit and a
        /// squad of five in one build — and asserts every bulk unit's combat stats (Attack, HP, Defense, Range, damage
        /// type, evasion, shield, penetration, per-shot energy, ammo pool, armour-by-nature, weapon loadout) EXACTLY
        /// equal the solo unit's, and equal each other's. If a future change to the squad dial ever scaled a stat
        /// instead of just the cost, this reds. Engine-only → CI.
        /// </summary>
        [Test]
        [Description("A bulk-built squad unit is stat-identical to an individually-built unit from the same parts — "
                   + "the dial scales cost/time only, never a combat stat (the developer's '100 bulk == 100 individual').")]
        public void BulkBuilt_IsStatIdenticalTo_IndividuallyBuilt()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) };

            // the SAME parts, assembled two ways: one unit per build, and a squad of five per build.
            var solo = GroundUnitAssembly.RegisterAssembledDesign(faction, "id-solo", "Lone Trooper", frame, parts);      // individual order
            var squad = GroundUnitAssembly.RegisterAssembledDesign(faction, "id-five", "Rifle Squad", frame, parts, 5);   // bulk build of five

            var body = s.StartingBody;
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();

            squad.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, squad.UniqueID), squad);   // one build → five bulk units
            solo.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, solo.UniqueID), solo);      // one build → one individual unit
            var forces = body.GetDataBlob<GroundForcesDB>();

            var individual = forces.Units.Single(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == solo.UniqueID);
            var bulk = forces.Units.Where(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == squad.UniqueID).ToList();
            Assert.That(bulk.Count, Is.EqualTo(5), "precondition: the bulk build fielded five units to compare");

            // asserts every combat stat the resolver reads is byte-for-byte equal between the two build paths.
            void AssertCombatIdentical(GroundUnit bulkUnit, string why)
            {
                Assert.That(bulkUnit.Attack,           Is.EqualTo(individual.Attack),           why + " — Attack");
                Assert.That(bulkUnit.Defense,          Is.EqualTo(individual.Defense),          why + " — Defense");
                Assert.That(bulkUnit.MaxHealth,        Is.EqualTo(individual.MaxHealth),        why + " — MaxHealth");
                Assert.That(bulkUnit.Health,           Is.EqualTo(individual.Health),           why + " — Health");
                Assert.That(bulkUnit.Range,            Is.EqualTo(individual.Range),            why + " — Range");
                Assert.That(bulkUnit.DamageType,       Is.EqualTo(individual.DamageType),       why + " — DamageType");
                Assert.That(bulkUnit.Evasion,          Is.EqualTo(individual.Evasion),          why + " — Evasion");
                Assert.That(bulkUnit.Shield,           Is.EqualTo(individual.Shield),           why + " — Shield");
                Assert.That(bulkUnit.Penetration,      Is.EqualTo(individual.Penetration),      why + " — Penetration");
                Assert.That(bulkUnit.PerShotEnergy,    Is.EqualTo(individual.PerShotEnergy),    why + " — PerShotEnergy");
                Assert.That(bulkUnit.MaxAmmo_kg,       Is.EqualTo(individual.MaxAmmo_kg),       why + " — MaxAmmo_kg");
                Assert.That(bulkUnit.ArmourVsKinetic,  Is.EqualTo(individual.ArmourVsKinetic),  why + " — ArmourVsKinetic");
                Assert.That(bulkUnit.ArmourVsEnergy,   Is.EqualTo(individual.ArmourVsEnergy),   why + " — ArmourVsEnergy");
                Assert.That(bulkUnit.ArmourVsExplosive,Is.EqualTo(individual.ArmourVsExplosive),why + " — ArmourVsExplosive");
                Assert.That(bulkUnit.ArmourVsExotic,   Is.EqualTo(individual.ArmourVsExotic),   why + " — ArmourVsExotic");
                Assert.That(bulkUnit.WeaponLoadout.Count, Is.EqualTo(individual.WeaponLoadout.Count), why + " — WeaponLoadout count");
            }

            // every one of the five bulk units matches the individually-built unit exactly — indistinguishable to combat.
            for (int i = 0; i < bulk.Count; i++)
                AssertCombatIdentical(bulk[i], $"bulk unit #{i + 1} vs individually-built");

            Log($"all {bulk.Count} bulk-built units are combat-stat-identical to the individually-built unit "
              + $"(Attack {individual.Attack}, HP {individual.MaxHealth}, Range {individual.Range}, {individual.DamageType})");
        }
    }
}
