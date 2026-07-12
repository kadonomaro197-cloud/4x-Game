using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensors ⚙3 — the EW ▸ CLOAK / signature-damping device (S2). The EMCON posture lever (Full/Cruise/Silent) and
    /// the activity processor set how loud a ship runs; a cloak is a stronger, COMPONENT-based damper that multiplies
    /// that emitted signature DOWN further, so the ship is picked up only at much shorter range. Because detection
    /// range ≈ √signature, a 0.2 cloak roughly halves how far off you can be seen — the stealth-ship maker.
    ///
    /// This slice does it cradle-to-grave: the <see cref="CloakAtb"/> component, the wire in
    /// <see cref="EmconActivityProcessor"/> (final ActivityMultiplier × <see cref="CloakAtb.CloakFactor"/>), and a
    /// buildable base-mod cloak-device on a NEW stealth ship, the Wraith. Byte-identical for every current ship
    /// (no cloak → factor 1.0 → the signature is unchanged); the Wraith is a new example ship, so no detection
    /// fixture is perturbed. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipCloakTests
    {
        private const string Aegis = "default-ship-design-test-warship"; // beam warship, no cloak
        private const string Wraith = "default-ship-design-test-wraith"; // stealth cruiser WITH the cloak device
        private static void Log(string m) => TestContext.Progress.WriteLine("[cloak] " + m);

        [Test]
        [Description("CloakAtb holds its signature multiplier and clamps to [MinSignatureFactor, 1] (a cloak is never PERFECT invisibility, and never LOUDER than normal); clone preserves it.")]
        public void CloakAtb_PinsTheFoundation()
        {
            var c = new CloakAtb(0.2);
            Assert.That(c.SignatureMultiplier, Is.EqualTo(0.2), "the cloak holds its signature multiplier");
            Assert.That(new CloakAtb(0.0).SignatureMultiplier, Is.EqualTo(CloakAtb.MinSignatureFactor),
                "a factor below the floor clamps up to MinSignatureFactor (never perfect invisibility)");
            Assert.That(new CloakAtb(1.5).SignatureMultiplier, Is.EqualTo(1.0),
                "a factor above 1 clamps down to 1 (a cloak never makes you louder)");
            Assert.That(((CloakAtb)c.Clone()).SignatureMultiplier, Is.EqualTo(0.2), "clone preserves the multiplier");
        }

        [Test]
        [Description("Byte-identical: a real base-mod ship with NO cloak reads a CloakFactor of 1.0, so its emitted signature is unchanged — the detection math is untouched (every current ship, until a hull mounts a cloak).")]
        public void ARealShip_WithNoCloak_ReadsFactorOne()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var ship = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis");

            Assert.That(CloakAtb.CloakFactor(ship), Is.EqualTo(1.0),
                "no cloak → factor 1.0 → signature unchanged → detection byte-identical");
            Log($"Aegis (no cloak): CloakFactor={CloakAtb.CloakFactor(ship):0.00}");
        }

        [Test]
        [Description("Cradle-to-grave: the base-mod Wraith Stealth Cruiser builds from JSON and its cloak device projects a real signature factor below 1 — the JSON cloak-device template + design + earth.json entries wired up (six-point registration).")]
        public void TheWraith_ProjectsARealCloakFactor()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Wraith), Is.True,
                "the Wraith loads onto the faction — the JSON cloak-device template + design + earth.json entries wired up");

            var wraith = ShipFactory.CreateShip(designs[Wraith], s.Faction, s.StartingBody, "Wraith");
            double factor = CloakAtb.CloakFactor(wraith);
            Log($"Wraith (cloaked): CloakFactor={factor:0.00}");

            Assert.That(factor, Is.LessThan(1.0),
                "the Wraith's cloak device damps its emitted signature below normal (JSON cloak-device → CloakAtb → CloakFactor is wired)");
            Assert.That(factor, Is.GreaterThanOrEqualTo(CloakAtb.MinSignatureFactor),
                "but never below the floor — a cloak is never perfect invisibility");
        }

        [Test]
        [Description("The WIRE, cradle-to-grave payoff: the EmconActivityProcessor multiplies the Wraith's final ActivityMultiplier by its CloakFactor, so the cloaked ship's emitted signature — and therefore how far off it can BE SEEN — is strictly LOWER than the same ship with no cloak. Detection range ≈ √signature, so a 0.2 cloak roughly halves the detectability range.")]
        public void TheCloak_ShrinksHowFarTheWraithCanBeSeen()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var wraith = ShipFactory.CreateShip(designs[Wraith], s.Faction, s.StartingBody, "Wraith");

            var profile = wraith.GetDataBlob<SensorProfileDB>();
            double baseMult = profile.SignatureBaseMultiplier;

            // A freshly built ship parked at a body is idle (OrbitDB, not NewtonMoveDB; no firing kit engaged), so the
            // heat factor is 1 — the clean state to isolate the cloak's contribution.
            Assert.That(EmconActivityProcessor.IsBurning(wraith), Is.False, "the parked Wraith is not burning");
            Assert.That(EmconActivityProcessor.IsFiring(wraith), Is.False, "the parked Wraith is not firing");

            // Reference: the same ship's signature WITHOUT the cloak applied (base × idle heat, factor 1.0).
            profile.ActivityMultiplier = EmconActivityProcessor.ComputeActivityMultiplier(baseMult, false, false);
            double reachUncloaked = SensorTools.DetectabilityRange_m(wraith);

            // Now the REAL processor runs — it composes posture × activity AND multiplies by the cloak factor.
            new EmconActivityProcessor().ProcessEntity(wraith, 5);
            double cloakedMult = profile.ActivityMultiplier;
            double reachCloaked = SensorTools.DetectabilityRange_m(wraith);
            Log($"ActivityMultiplier: uncloaked={EmconActivityProcessor.ComputeActivityMultiplier(baseMult, false, false):0.000}, cloaked={cloakedMult:0.000} (CloakFactor {CloakAtb.CloakFactor(wraith):0.00})");
            Log($"detectability range: uncloaked={reachUncloaked/1e6:0.0} Mm, cloaked={reachCloaked/1e6:0.0} Mm");

            Assert.That(cloakedMult, Is.EqualTo(baseMult * CloakAtb.CloakFactor(wraith)).Within(1e-9),
                "idle (heat 1) ⇒ the processor leaves the ship at base × cloak factor — the cloak wire composed correctly");
            Assert.That(cloakedMult, Is.LessThan(baseMult),
                "the cloak makes the Wraith emit LESS than its normal idle signature");
            Assert.That(reachCloaked, Is.LessThan(reachUncloaked),
                "so the cloaked Wraith can be SEEN from a shorter range than the same ship uncloaked — the stealth payoff");
        }
    }
}
