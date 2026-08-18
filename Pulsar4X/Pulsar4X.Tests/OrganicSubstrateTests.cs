using NUnit.Framework;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E13 — ORGANIC / BIO CHASSIS SUBSTRATE (Operation Blueprint-to-Steel, 2026-08-18). A ground unit's chassis is
    /// made of something (Mechanical / Organic / Synthetic), and an Organic (living/bio) hull SELF-REPAIRS over time.
    /// Slice 1 wires the design→unit `Substrate` snapshot + the one consequence (organic self-repair). Byte-identical:
    /// Mechanical is the default and the regen is flag-gated (so both the flag AND the substrate must be set for any
    /// unit to heal). These gauges pin the pure regen rule (only a wounded, living, Organic unit heals, capped) + the
    /// design→unit snapshot + the flag default.
    /// </summary>
    [TestFixture]
    public class OrganicSubstrateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[substrate] " + m);

        [Test]
        [Description("Organic self-repair (pure): an Organic, wounded, alive unit knits MaxHealth×fraction×(dt/3600) "
                     + "back, capped at MaxHealth; a Mechanical unit, an already-full unit, and a dead unit never regen.")]
        public void OrganicRegenTick_HealsOnlyWoundedLivingOrganicUnits()
        {
            // Organic + wounded → regens MaxHealth × fraction × (dt/3600) = 1000 × 0.05 × 1 = 50 over one hour.
            var organic = new GroundUnit { Substrate = GroundSubstrate.Organic, MaxHealth = 1000, Health = 400 };
            double gained = GroundForcesProcessor.OrganicRegenTick(organic, 0.05, 3600);
            Assert.That(gained, Is.EqualTo(50).Within(1e-6), "5% of 1000 over one game-hour = 50 hp knitted back");
            Assert.That(organic.Health, Is.EqualTo(450).Within(1e-6));

            // Cap at MaxHealth — a near-full unit only tops up the remaining space, and reports just that gain.
            var nearFull = new GroundUnit { Substrate = GroundSubstrate.Organic, MaxHealth = 1000, Health = 980 };
            double topUp = GroundForcesProcessor.OrganicRegenTick(nearFull, 0.05, 3600);   // wants 50, only 20 fits
            Assert.That(nearFull.Health, Is.EqualTo(1000).Within(1e-6), "capped at MaxHealth (never overheals)");
            Assert.That(topUp, Is.EqualTo(20).Within(1e-6), "returns only the health actually gained");

            // Mechanical → never regens (byte-identical).
            var mech = new GroundUnit { Substrate = GroundSubstrate.Mechanical, MaxHealth = 1000, Health = 400 };
            Assert.That(GroundForcesProcessor.OrganicRegenTick(mech, 0.05, 3600), Is.EqualTo(0.0),
                "a steel hull doesn't knit itself back");
            Assert.That(mech.Health, Is.EqualTo(400.0), "mechanical health unchanged");

            // Already-full Organic → no regen.
            var full = new GroundUnit { Substrate = GroundSubstrate.Organic, MaxHealth = 1000, Health = 1000 };
            Assert.That(GroundForcesProcessor.OrganicRegenTick(full, 0.05, 3600), Is.EqualTo(0.0), "already full");

            // Dead Organic (0 hp) → no self-resurrect.
            var dead = new GroundUnit { Substrate = GroundSubstrate.Organic, MaxHealth = 1000, Health = 0 };
            Assert.That(GroundForcesProcessor.OrganicRegenTick(dead, 0.05, 3600), Is.EqualTo(0.0), "0 hp = destroyed, no revive");
            Assert.That(dead.Health, Is.EqualTo(0.0));
            Log("organic regen: wounded heals + caps; mechanical / full / dead unchanged");
        }

        [Test]
        [Description("The Substrate dial snapshots design → raised unit (RaiseUnit); a default design is Mechanical.")]
        public void Substrate_SnapshotsFromDesignToRaisedUnit()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            var organicDesign = new GroundUnitDesign { UniqueID = "test-organic", Name = "Bio Swarm",
                                                       HitPoints = 500, Substrate = GroundSubstrate.Organic };
            var organic = GroundForces.RaiseUnit(body, organicDesign, s.Faction.Id, 0);
            Assert.That(organic.Substrate, Is.EqualTo(GroundSubstrate.Organic), "an Organic design raises an Organic unit");

            var plainDesign = new GroundUnitDesign { UniqueID = "test-mech", Name = "Rifles", HitPoints = 500 };
            var plain = GroundForces.RaiseUnit(body, plainDesign, s.Faction.Id, 0);
            Assert.That(plain.Substrate, Is.EqualTo(GroundSubstrate.Mechanical),
                "a default design is Mechanical (byte-identical)");
        }

        [Test]
        [Description("Byte-identity: organic self-repair defaults OFF, so no unit regenerates until the menu turns it on "
                     + "(and even then only Organic units do).")]
        public void OrganicRegen_DefaultsOff()
        {
            Assert.That(GroundForcesProcessor.EnableOrganicRegen, Is.False,
                "organic self-repair must default OFF so every existing ground fight is byte-identical");
        }
    }
}
