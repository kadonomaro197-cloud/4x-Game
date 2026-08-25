using NUnit.Framework;
using Pulsar4X.Combat;       // AuraAtb, AuraEffect, AuraTarget
using Pulsar4X.Components;   // ComponentDesign, ComponentInstance
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E14 — AURAS, the RALLY / DREAD steadiness effects (OPERATION BLUEPRINT-TO-STEEL, the un-shelved shelved effects).
    /// Rally/Dread act on a battalion's combat MORALE, not its firepower/toughness (those are Command/Ward): a friendly
    /// RALLY building lifts a faction's <see cref="GroundCommandAura.SteadinessMultFor"/> above 1, an ENEMY DREAD building
    /// drops it below 1, and that steadiness scales the ground tactical brain's PERCEIVED odds
    /// (<see cref="GroundTactics.DecidePosture"/>) — so a rallied battalion HOLDS at odds a neutral one flees, and a
    /// dreaded one BREAKS at odds a neutral one holds. No new rout state machine — it rides the existing retreat
    /// decision. Flag-gated (<see cref="GroundCommandAura.EnableGroundCommandAura"/>) default OFF, and an unset
    /// steadiness reads neutral 1.0 → byte-identical. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class GroundSteadinessAuraTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-steadiness] " + m);

        private const int EnemyFaction = 900002;

        [TearDown]
        public void ResetFlag() => GroundCommandAura.EnableGroundCommandAura = false;

        /// <summary>Install an aura building carrying an <see cref="AuraAtb"/> onto the scenario's colony (owned by
        /// s.Faction) — the ground aura source, scanned off the body's component stores.</summary>
        private static void InstallAura(TestScenario s, AuraEffect effect, double magnitude, AuraTarget target)
        {
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var design = new ComponentDesign { UniqueID = "test-aura-" + effect + "-" + target + "-" + magnitude, Name = "Aura Beacon" };
            design.AttributesByType[typeof(AuraAtb)] = new AuraAtb(0, magnitude, (double)(int)effect, (double)(int)target);
            comps.AddComponentInstance(new ComponentInstance(design));
        }

        // ─────────────────────────── the pure SteadinessMultFor read ───────────────────────────

        [Test]
        [Description("SteadinessMultFor: a friendly RALLY building lifts the owner's steadiness above 1; the enemy who "
                     + "doesn't own it is unaffected (a Friends field lands on its own side). Flag-gated.")]
        public void FriendlyRally_LiftsOwnerSteadiness_NotTheEnemy_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            InstallAura(s, AuraEffect.Rally, 0.5, AuraTarget.Friends);   // the colony faction's own Rally beacon

            GroundCommandAura.EnableGroundCommandAura = true;
            Assert.That(GroundCommandAura.SteadinessMultFor(body, s.Faction.Id), Is.EqualTo(1.5).Within(1e-9),
                "a friendly Rally 0.5 → steadiness ×1.5 for its owner");
            Assert.That(GroundCommandAura.SteadinessMultFor(body, EnemyFaction), Is.EqualTo(1.0).Within(1e-9),
                "the enemy doesn't get our Friends-targeted Rally");

            GroundCommandAura.EnableGroundCommandAura = false;
            Assert.That(GroundCommandAura.SteadinessMultFor(body, s.Faction.Id), Is.EqualTo(1.0).Within(1e-9),
                "flag off → neutral 1.0 (byte-identical)");
        }

        [Test]
        [Description("SteadinessMultFor: an ENEMY DREAD building (Foes-targeted) drops the steadiness of the faction it "
                     + "lands on, but does NOT shake its own owner (a faction's own Dread doesn't frighten itself).")]
        public void EnemyDread_DropsTargetSteadiness_NotItsOwner()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            InstallAura(s, AuraEffect.Dread, 0.4, AuraTarget.Foes);   // the colony faction projects Dread AT its foes

            GroundCommandAura.EnableGroundCommandAura = true;
            // The ENEMY faction reads this as an enemy Dread field aimed at it → steadiness drops.
            Assert.That(GroundCommandAura.SteadinessMultFor(body, EnemyFaction), Is.EqualTo(0.6).Within(1e-9),
                "an enemy Dread 0.4 aimed at foes → steadiness ×0.6 for the foe");
            // The OWNER of the Dread building isn't shaken by its own field.
            Assert.That(GroundCommandAura.SteadinessMultFor(body, s.Faction.Id), Is.EqualTo(1.0).Within(1e-9),
                "a faction's own Dread building doesn't frighten itself");
        }

        [Test]
        [Description("SteadinessMultFor clamps: an overwhelming rally can't exceed MaxSteadiness, an overwhelming enemy "
                     + "dread can't drop below MinSteadiness (a shaken force is timid, never a zero-strength paper tiger).")]
        public void Steadiness_IsClamped_BothWays()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            GroundCommandAura.EnableGroundCommandAura = true;

            InstallAura(s, AuraEffect.Rally, 5.0, AuraTarget.Friends);   // absurd rally
            Assert.That(GroundCommandAura.SteadinessMultFor(body, s.Faction.Id),
                Is.EqualTo(GroundCommandAura.MaxSteadiness).Within(1e-9), "rally clamps at MaxSteadiness");

            InstallAura(s, AuraEffect.Dread, 5.0, AuraTarget.Foes);      // absurd dread aimed at the enemy
            Assert.That(GroundCommandAura.SteadinessMultFor(body, EnemyFaction),
                Is.EqualTo(GroundCommandAura.MinSteadiness).Within(1e-9), "dread clamps at MinSteadiness");
        }

        [Test]
        [Description("Take-the-BEST-not-sum: two friendly Rally beacons don't stack — the strongest wins (the MultFor "
                     + "guard-rail applied to steadiness).")]
        public void TwoRallyBeacons_TakeTheBest_NotTheSum()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            InstallAura(s, AuraEffect.Rally, 0.3, AuraTarget.Friends);
            InstallAura(s, AuraEffect.Rally, 0.5, AuraTarget.Friends);

            GroundCommandAura.EnableGroundCommandAura = true;
            Assert.That(GroundCommandAura.SteadinessMultFor(body, s.Faction.Id), Is.EqualTo(1.5).Within(1e-9),
                "two rally beacons (0.3 + 0.5) → 1 + best(0.5) = 1.5, not 1 + 0.8");
        }

        // ─────────────────────────── the decision consequence (pure DecidePosture) ───────────────────────────

        /// <summary>An attacker on hostile ground with a line of retreat, ammo full, mid personality.</summary>
        private static GroundTacticsContext Attacker(double own, double enemy, double steadiness) => new GroundTacticsContext
        {
            OwnStrength = own,
            EnemyStrength = enemy,
            RiskTrait = 0.5,
            AggressionTrait = 0.5,
            IsHomelandDefender = false,
            HasOrbitalSupport = false,
            FortificationMult = 1.0,
            DefensibleTerrain = false,
            HasAmmoWeapons = false,     // no dry-ammo gate
            AmmoFraction = 1.0,
            ReserveIntact = true,
            HasFallback = true,
            FallbackRegion = 1,
            HasAdvanceTarget = false,
            AdvanceRegion = -1,
            Blind = false,
            Steadiness = steadiness,
        };

        [Test]
        [Description("RALLY holds the line: at odds where a NEUTRAL battalion is losing-hard and retreats, a rallied "
                     + "battalion (steadiness > 1) fights as if stronger and does NOT flee.")]
        public void Rally_KeepsABattalionFromFleeing()
        {
            var neutral = GroundTactics.DecidePosture(Attacker(own: 100, enemy: 450, steadiness: 1.0));
            Assert.That(neutral.Intent, Is.EqualTo(GroundIntent.Retreat),
                "a neutral 100-vs-450 battalion is losing hard → fighting withdrawal");

            var rallied = GroundTactics.DecidePosture(Attacker(own: 100, enemy: 450, steadiness: 1.5));
            Assert.That(rallied.Intent, Is.Not.EqualTo(GroundIntent.Retreat),
                "the SAME odds, rallied ×1.5 → no longer losing hard → it holds instead of fleeing");
            Log($"neutral → {neutral.Intent} ; rallied → {rallied.Intent}");
        }

        [Test]
        [Description("DREAD breaks the line: at odds where a NEUTRAL battalion digs in and holds, a dreaded battalion "
                     + "(steadiness < 1) fights as if weaker and retreats.")]
        public void Dread_MakesABattalionBreakSooner()
        {
            var neutral = GroundTactics.DecidePosture(Attacker(own: 200, enemy: 450, steadiness: 1.0));
            Assert.That(neutral.Intent, Is.Not.EqualTo(GroundIntent.Retreat),
                "a neutral 200-vs-450 battalion is outnumbered but not losing hard → dig in, not flee");

            var dreaded = GroundTactics.DecidePosture(Attacker(own: 200, enemy: 450, steadiness: 0.5));
            Assert.That(dreaded.Intent, Is.EqualTo(GroundIntent.Retreat),
                "the SAME odds, dreaded ×0.5 → now losing hard → fighting withdrawal");
            Log($"neutral → {neutral.Intent} ; dreaded → {dreaded.Intent}");
        }

        [Test]
        [Description("Byte-identity: an UNSET steadiness (0, the struct default every existing context uses) decides "
                     + "exactly as steadiness 1.0 — so the aura wire changes nothing until an aura sets it.")]
        public void UnsetSteadiness_IsNeutral_ByteIdentical()
        {
            var unset = GroundTactics.DecidePosture(Attacker(own: 200, enemy: 450, steadiness: 0.0));
            var one   = GroundTactics.DecidePosture(Attacker(own: 200, enemy: 450, steadiness: 1.0));
            Assert.That(unset.Intent, Is.EqualTo(one.Intent), "unset steadiness (0) == neutral 1.0 (Intent)");
            Assert.That(unset.StanceFamily, Is.EqualTo(one.StanceFamily), "unset steadiness (0) == neutral 1.0 (Stance)");
        }
    }
}
