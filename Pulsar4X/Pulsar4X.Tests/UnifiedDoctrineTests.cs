using System.Linq;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the UNIFIED doctrine catalog (D1) — "one doctrine catalog used in all combat for all scenarios"
    /// (the developer's ruling, 2026-07-24).
    ///
    /// What this slice is: the blueprint SHAPE + the catalog data + the pure reader. Nothing is wired into either
    /// resolver yet, so live combat is byte-identical — these tests pin the data and the math so the wiring slices
    /// (D2 = ground reads this catalog, D3 = the resolvers read the new behaviour dials) land on a verified base.
    ///
    /// The load-bearing thing being guarded here is the RECIPROCAL: space authored "ToughnessMult 1.4" (tougher)
    /// and ground authored "DamageTakenMult 0.75" (takes less). Those are the same posture encoded inversely, so a
    /// naive merge would silently turn a dug-in formation into a fragile one. If a future edit breaks that
    /// derivation, these go red.
    /// </summary>
    [TestFixture]
    public class UnifiedDoctrineTests
    {
        // CreateWithColony() re-parses the whole mod tree and is the suite's main slowness (Tests/CLAUDE.md),
        // so the catalog is loaded ONCE for the whole fixture rather than per-test.
        private static ModDataStore _store;

        [OneTimeSetUp]
        public void LoadCatalogOnce() => _store = TestScenario.CreateWithColony().Game.StartingGameData;

        // ── the pure math (no game needed) ──────────────────────────────────────────────────────────────────

        [Test]
        [Description("The reciprocal holds both ways: authoring EITHER encoding yields a consistent pair, so a "
                   + "defensive posture can never be merged into a fragile one.")]
        public void Reciprocal_EitherEncoding_YieldsAConsistentPair()
        {
            // Space encoding: 1.4x tougher  =>  takes 1/1.4 of the damage.
            var spaceStyle = new CombatDoctrineBlueprint { ToughnessMult = 1.4 };
            Assert.That(CombatDoctrine.EffectiveToughnessMult(spaceStyle), Is.EqualTo(1.4).Within(1e-9));
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(spaceStyle), Is.EqualTo(1.0 / 1.4).Within(1e-9));

            // Ground encoding: takes 0.75x damage  =>  is 1/0.75 as tough.
            var groundStyle = new CombatDoctrineBlueprint { DamageTakenMult = 0.75 };
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(groundStyle), Is.EqualTo(0.75).Within(1e-9));
            Assert.That(CombatDoctrine.EffectiveToughnessMult(groundStyle), Is.EqualTo(1.0 / 0.75).Within(1e-9));

            // The direction that matters: BOTH encodings of "defensive" must read as tougher-than-neutral.
            Assert.That(CombatDoctrine.EffectiveToughnessMult(spaceStyle), Is.GreaterThan(1.0),
                "space-encoded defensive posture must read as TOUGHER");
            Assert.That(CombatDoctrine.EffectiveToughnessMult(groundStyle), Is.GreaterThan(1.0),
                "ground-encoded defensive posture must ALSO read as tougher - this is the merge trap");
        }

        [Test]
        [Description("An unauthored DamageTakenMult (0) means 'derive me', not 'I take zero damage'.")]
        public void UnauthoredDamageTaken_DerivesFromToughness_RatherThanReadingAsZero()
        {
            var bp = new CombatDoctrineBlueprint { ToughnessMult = 2.0, DamageTakenMult = 0.0 };
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(bp), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(bp), Is.GreaterThan(0.0),
                "a 0 in the data must never become a literal invulnerability multiplier");
        }

        [Test]
        [Description("Garbage in the data can never produce a divide-by-zero, a NaN, or a negative multiplier.")]
        public void MalformedBlueprint_NeverProducesGarbageMultipliers()
        {
            foreach (var bad in new[] { 0.0, -5.0, double.NaN, double.PositiveInfinity })
            {
                var bp = new CombatDoctrineBlueprint { ToughnessMult = bad, FirepowerMult = bad };
                var tough = CombatDoctrine.EffectiveToughnessMult(bp);
                var taken = CombatDoctrine.EffectiveDamageTakenMult(bp);
                var fire = CombatDoctrine.EffectiveFirepowerMult(bp);

                Assert.That(double.IsFinite(tough) && tough > 0, $"toughness went bad for input {bad}: {tough}");
                Assert.That(double.IsFinite(taken) && taken > 0, $"damage-taken went bad for input {bad}: {taken}");
                Assert.That(double.IsFinite(fire) && fire > 0, $"firepower went bad for input {bad}: {fire}");
            }

            // A null blueprint is neutral, never a crash.
            Assert.That(CombatDoctrine.EffectiveToughnessMult(null), Is.EqualTo(1.0));
            Assert.That(CombatDoctrine.EffectiveFirepowerMult(null), Is.EqualTo(1.0));
        }

        [Test]
        [Description("Unrecognised or absent dial strings fall back to the LEGACY behaviour, so a typo degrades "
                   + "gracefully instead of silently changing how a force fights.")]
        public void UnknownDialStrings_FallBackToLegacyBehaviour()
        {
            Assert.That(CombatDoctrine.ParsePosture(null), Is.EqualTo(EngagementPosture.WeaponsFree));
            Assert.That(CombatDoctrine.ParsePosture("nonsense"), Is.EqualTo(EngagementPosture.WeaponsFree));
            Assert.That(CombatDoctrine.ParsePosture("weaponshold"), Is.EqualTo(EngagementPosture.WeaponsHold),
                "parsing must be case-insensitive");

            Assert.That(CombatDoctrine.ParseTargetPriority(null), Is.EqualTo(TargetPriority.Balanced));
            Assert.That(CombatDoctrine.ParseTargetPriority("nonsense"), Is.EqualTo(TargetPriority.Balanced));
            Assert.That(CombatDoctrine.ParseTargetPriority("finishwounded"), Is.EqualTo(TargetPriority.FinishWounded));

            Assert.That(CombatDoctrine.ParseDomain("nonsense"), Is.EqualTo(DoctrineDomain.Both),
                "an unrecognised domain must stay PERMISSIVE - a typo should never make a doctrine vanish");
        }

        [Test]
        [Description("The retreat threshold defers to the engine default unless a doctrine deliberately authors one.")]
        public void RetreatThreshold_DefersToEngineDefault_UnlessAuthored()
        {
            var unauthored = new CombatDoctrineBlueprint { RetreatCasualtyThreshold = -1.0 };
            Assert.That(CombatDoctrine.EffectiveRetreatThreshold(unauthored, 0.5), Is.EqualTo(0.5),
                "an unauthored threshold must keep today's flat engine default");

            var authored = new CombatDoctrineBlueprint { RetreatCasualtyThreshold = 0.8 };
            Assert.That(CombatDoctrine.EffectiveRetreatThreshold(authored, 0.5), Is.EqualTo(0.8));

            var silly = new CombatDoctrineBlueprint { RetreatCasualtyThreshold = 5.0 };
            Assert.That(CombatDoctrine.EffectiveRetreatThreshold(silly, 0.5), Is.EqualTo(1.0),
                "a threshold above 1.0 is clamped - you cannot lose more than everything");
        }

        [Test]
        [Description("Domain gating: a Ground-only entry is not offered to fleets, and 'Both' is offered to everyone.")]
        public void DomainGate_KeepsGroundOnlyEntriesOffTheFleetList()
        {
            var both = new CombatDoctrineBlueprint { Domain = "Both" };
            var ground = new CombatDoctrineBlueprint { Domain = "Ground" };
            var space = new CombatDoctrineBlueprint { Domain = "Space" };

            Assert.That(CombatDoctrine.IsSelectableBy(both, DoctrineDomain.Space), Is.True);
            Assert.That(CombatDoctrine.IsSelectableBy(both, DoctrineDomain.Ground), Is.True);
            Assert.That(CombatDoctrine.IsSelectableBy(ground, DoctrineDomain.Space), Is.False);
            Assert.That(CombatDoctrine.IsSelectableBy(ground, DoctrineDomain.Ground), Is.True);
            Assert.That(CombatDoctrine.IsSelectableBy(space, DoctrineDomain.Ground), Is.False);
        }

        // ── the catalog data (loads through the real mod pipeline) ──────────────────────────────────────────

        [Test]
        [Description("The unified catalog loads from JSON, and every entry is well-formed: parseable dials, sane "
                   + "multipliers, no duplicate ids.")]
        public void UnifiedCatalog_LoadsAndEveryEntryIsWellFormed()
        {
            var store = _store;

            Assert.That(store.CombatDoctrines, Is.Not.Null.And.Not.Empty, "the doctrine catalog failed to load");

            foreach (var kvp in store.CombatDoctrines)
            {
                var bp = kvp.Value;
                Assert.That(bp.UniqueID, Is.Not.Null.And.Not.Empty);
                Assert.That(bp.DisplayName, Is.Not.Null.And.Not.Empty, $"{bp.UniqueID} has no display name");

                var tough = CombatDoctrine.EffectiveToughnessMult(bp);
                var taken = CombatDoctrine.EffectiveDamageTakenMult(bp);
                var fire = CombatDoctrine.EffectiveFirepowerMult(bp);
                Assert.That(double.IsFinite(tough) && tough > 0, $"{bp.UniqueID} toughness {tough}");
                Assert.That(double.IsFinite(taken) && taken > 0, $"{bp.UniqueID} damage-taken {taken}");
                Assert.That(double.IsFinite(fire) && fire > 0, $"{bp.UniqueID} firepower {fire}");

                // The reciprocal must hold for every authored entry.
                Assert.That(tough * taken, Is.EqualTo(1.0).Within(1e-9),
                    $"{bp.UniqueID}: toughness and damage-taken must be reciprocals of each other");

                // The dial strings must all round-trip - a typo in the data is a silent behaviour change.
                Assert.That(bp.EngagementPosture, Is.Not.Null.And.Not.Empty);
                Assert.That(System.Enum.TryParse<EngagementPosture>(bp.EngagementPosture, true, out _), Is.True,
                    $"{bp.UniqueID} has an unparseable EngagementPosture '{bp.EngagementPosture}'");
                Assert.That(System.Enum.TryParse<TargetPriority>(bp.TargetPriority, true, out _), Is.True,
                    $"{bp.UniqueID} has an unparseable TargetPriority '{bp.TargetPriority}'");
                Assert.That(System.Enum.TryParse<DoctrineDomain>(bp.Domain, true, out _), Is.True,
                    $"{bp.UniqueID} has an unparseable Domain '{bp.Domain}'");

                Assert.That(bp.BreakAwaySeconds, Is.GreaterThanOrEqualTo(0), $"{bp.UniqueID} negative break-away");
            }
        }

        [Test]
        [Description("BYTE-IDENTITY GUARD: the four legacy SPACE doctrines keep the exact multipliers the live "
                   + "resolver has always read, so this slice cannot have changed a single space battle.")]
        public void LegacySpaceDoctrines_KeepTheirExactMultipliers()
        {
            var store = _store;

            void Check(string id, double fire, double tough, double speed, double cooldown, bool retreat)
            {
                Assert.That(store.CombatDoctrines.ContainsKey(id), Is.True, $"legacy doctrine '{id}' went missing");
                var bp = store.CombatDoctrines[id];
                Assert.That(bp.FirepowerMult, Is.EqualTo(fire).Within(1e-9), $"{id} FirepowerMult drifted");
                Assert.That(bp.ToughnessMult, Is.EqualTo(tough).Within(1e-9), $"{id} ToughnessMult drifted");
                Assert.That(bp.SpeedMult, Is.EqualTo(speed).Within(1e-9), $"{id} SpeedMult drifted");
                Assert.That(bp.CooldownSeconds, Is.EqualTo(cooldown).Within(1e-9), $"{id} CooldownSeconds drifted");
                Assert.That(bp.IsRetreat, Is.EqualTo(retreat), $"{id} IsRetreat drifted");
            }

            Check("balanced", 1.0, 1.0, 1.0, 60, false);
            Check("all-out-attack", 1.4, 0.85, 1.1, 300, false);
            Check("defensive-line", 0.85, 1.4, 0.8, 300, false);
            Check("fighting-withdrawal", 0.6, 1.1, 1.2, 120, true);
        }

        [Test]
        [Description("The three GROUND stances survived the merge with their ±25% trade-off intact, and their "
                   + "damage-taken encoding still means what it meant in groundStances.json.")]
        public void GroundStances_SurviveTheMerge_WithTheirTradeOffIntact()
        {
            var store = _store;

            Assert.That(store.CombatDoctrines.ContainsKey("ground-offensive"), Is.True);
            Assert.That(store.CombatDoctrines.ContainsKey("ground-defensive"), Is.True);

            var push = store.CombatDoctrines["ground-offensive"];
            Assert.That(push.FirepowerMult, Is.EqualTo(1.25).Within(1e-9));
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(push), Is.EqualTo(1.25).Within(1e-9),
                "Offensive Push must still TAKE more damage - that is the trade-off");

            var digIn = store.CombatDoctrines["ground-defensive"];
            Assert.That(digIn.FirepowerMult, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(CombatDoctrine.EffectiveDamageTakenMult(digIn), Is.EqualTo(0.75).Within(1e-9),
                "Dig In must still take LESS damage");
            Assert.That(CombatDoctrine.EffectiveToughnessMult(digIn), Is.GreaterThan(1.0),
                "and it must therefore read as tougher, not weaker, once merged into the shared shape");
        }

        [Test]
        [Description("The catalog actually delivers the behaviours the rulings asked for: something pursues, "
                   + "something holds fire, and something finishes wounded targets instead of spreading fire.")]
        public void Catalog_CoversTheRuledBehaviours()
        {
            var store = _store;
            var all = store.CombatDoctrines.Values.ToList();

            Assert.That(all.Any(CombatDoctrine.Pursues), Is.True,
                "ruling #15: pursuit must be expressible as a doctrine");
            Assert.That(all.Any(b => CombatDoctrine.ParsePosture(b.EngagementPosture) == EngagementPosture.WeaponsHold),
                Is.True, "ruling #19: an engage decision means SOME doctrine must hold fire");
            Assert.That(all.Any(b => CombatDoctrine.ParseTargetPriority(b.TargetPriority) == TargetPriority.FinishWounded),
                Is.True, "ruling #18: target priority must be able to finish cripples rather than spread fire");
            Assert.That(all.Any(b => CombatDoctrine.EffectiveBreakAwaySeconds(b) > 0), Is.True,
                "ruling #15: a retreat must be able to cost a break-away timer");
            Assert.That(all.Count(b => CombatDoctrine.IsSelectableBy(b, DoctrineDomain.Ground)), Is.GreaterThan(3),
                "ground must gain access to the shared doctrines, not just its original three");
        }
    }
}
