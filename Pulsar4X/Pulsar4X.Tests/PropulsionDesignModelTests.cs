using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the PROPULSION door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="PropulsionDesignModel"/> (the engine half of the propulsion form) REPRODUCES every
    /// base-mod propulsion component's produced <c>*Atb</c> from its family choice + dials — every hand-authored engine
    /// falls out of the one parametric form (the DESIGNER-NORTH-STAR reproduction claim, made executable). Pure (no
    /// colony harness → fast, not the slow CI shard); it constructs the REAL <c>*Atb</c> objects and asserts their real
    /// fields, so a drift in the model's per-family arithmetic (or a swapped ctor arg) fails here. Byte-identical to the
    /// live game — nothing calls the model yet.
    ///
    /// THE PURE-GAUGE SCOPE (honest): the reaction/warp arithmetic reads a fuel's exhaust-velocity + grade and the tech
    /// adds/mults from the faction DATA STORE, which a pure value type cannot touch — so those are INPUTS to the model.
    /// This gauge therefore proves the FORMULA reproduction: given each base-mod design's authored dials + the fuel
    /// constants (verified against <c>materials.json</c>: rp-1 3510/1.15 · methalox 3615/1.05 · hydrolox 4462/0.85 ·
    /// ntp 7000/0.75 · antimatter 60000/0.05) at a stated tech baseline (adds 0 / mults 1.0), the model computes the
    /// EXACT <c>*Atb</c> args the template formula does. A byte-identical match to a LIVE game's tech-added numbers is
    /// the non-pure colony-harness gauge's job (see the campaign risk register) — this pins the arithmetic itself.
    /// </summary>
    [TestFixture]
    public class PropulsionDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[propulsion-model] " + m);

        // ---- REACTION -------------------------------------------------------------------------------------------

        private static void AssertReaction(string name, PropulsionDesignModel m, string fuelType,
            double expectedEv, double expectedFbr, double expectedThrust)
        {
            var p = m.Compute();
            Log($"{name}: reaction fuel={fuelType} EV={p.ExhaustVelocity} FBR={p.FuelBurnRate} thrust={p.Thrust}");
            Assert.That(p.Family, Is.EqualTo(PropulsionFamily.Reaction), $"{name} Family");
            Assert.That(p.Reaction, Is.Not.Null, $"{name} produced a NewtonionThrustAtb");
            Assert.That(p.Warp, Is.Null, $"{name} no warp atb");
            Assert.That(p.Reactionless, Is.Null, $"{name} no reactionless atb");
            Assert.That(p.Surface, Is.Null, $"{name} no surface atb");
            // the produced *Atb args
            Assert.That(p.Reaction.ExhaustVelocity, Is.EqualTo(expectedEv).Within(1e-6), $"{name} ExhaustVelocity");
            Assert.That(p.Reaction.FuelType, Is.EqualTo(fuelType), $"{name} FuelType (pass-through)");
            Assert.That(p.Reaction.FuelBurnRate, Is.EqualTo(expectedFbr).Within(1e-6), $"{name} FuelBurnRate");
            // derived scalars
            Assert.That(p.ExhaustVelocity, Is.EqualTo(expectedEv).Within(1e-6), $"{name} derived EV");
            Assert.That(p.FuelBurnRate, Is.EqualTo(expectedFbr).Within(1e-6), $"{name} derived FBR");
            Assert.That(p.Thrust, Is.EqualTo(expectedThrust).Within(1e-6), $"{name} thrust = EV*FBR");
            // the PAIRED thrust signature every reaction engine emits (3500 K band, magnitude = thrust)
            Assert.That(p.Signature, Is.Not.Null, $"{name} produced a SensorSignatureAtb");
            Assert.That(p.Signature.PartWaveFormMag, Is.EqualTo(expectedThrust).Within(1e-6), $"{name} signature magnitude = thrust");
        }

        [Test]
        [Description("Every base-mod CONVENTIONAL rocket (F1 / Merlin / Raptor / RS-25) falls out of the Reaction family: EV = fuel base (+tech), fuel-burn = Mass × 0.3 × grade, thrust = EV × burn, and the paired 3500 K signature = thrust.")]
        public void ConventionalRockets_ReproduceFromTheForm()
        {
            // chemical coefficient 0.3, no tech add/mult at the baseline.
            AssertReaction("F1",
                PropulsionDesignModel.Reaction("rp-1", driveMass: 8400, exhaustVelocityBase: 3510, fuelGrade: 1.15, burnCoefficient: 0.3),
                "rp-1", 3510, 8400 * 0.3 * 1.15, 3510 * (8400 * 0.3 * 1.15));

            AssertReaction("Merlin",
                PropulsionDesignModel.Reaction("rp-1", driveMass: 470, exhaustVelocityBase: 3510, fuelGrade: 1.15, burnCoefficient: 0.3),
                "rp-1", 3510, 470 * 0.3 * 1.15, 3510 * (470 * 0.3 * 1.15));

            AssertReaction("Raptor",
                PropulsionDesignModel.Reaction("methalox", driveMass: 2000, exhaustVelocityBase: 3615, fuelGrade: 1.05, burnCoefficient: 0.3),
                "methalox", 3615, 2000 * 0.3 * 1.05, 3615 * (2000 * 0.3 * 1.05));

            AssertReaction("RS-25",
                PropulsionDesignModel.Reaction("hydrolox", driveMass: 3200, exhaustVelocityBase: 4462, fuelGrade: 0.85, burnCoefficient: 0.3),
                "hydrolox", 4462, 3200 * 0.3 * 0.85, 4462 * (3200 * 0.3 * 0.85));
        }

        [Test]
        [Description("The nuclear-thermal (NERVA) and antimatter reaction drives use the OTHER per-family constants: burn coefficient 0.017 (not 0.3) and NO burn-rate tech mult. NERVA's EV adds a nuclear tech term (×250); antimatter adds nothing. Antimatter has no shipped default design — the gauge synthesizes one (Mass 3000).")]
        public void NuclearAndAntimatter_ReproduceWithTheirOwnConstants()
        {
            // NERVA: ntp 7000/0.75, coef 0.017, tech add 0 at baseline.
            AssertReaction("NERVA",
                PropulsionDesignModel.Reaction("ntp", driveMass: 1800, exhaustVelocityBase: 7000, fuelGrade: 0.75, burnCoefficient: 0.017),
                "ntp", 7000, 1800 * 0.017 * 0.75, 7000 * (1800 * 0.017 * 0.75));

            // antimatter: 60000/0.05, coef 0.017, no tech add (template only — synthesized Mass 3000).
            AssertReaction("antimatter",
                PropulsionDesignModel.Reaction("antimatter", driveMass: 3000, exhaustVelocityBase: 60000, fuelGrade: 0.05, burnCoefficient: 0.017),
                "antimatter", 60000, 3000 * 0.017 * 0.05, 60000 * (3000 * 0.017 * 0.05));
        }

        [Test]
        [Description("The tech terms flow: an exhaust-velocity tech ADD raises EV, a burn-rate tech MULT raises the fuel-burn — proving both are wired (a live game with non-zero starting tech reproduces through the same inputs).")]
        public void ReactionTechTerms_Flow()
        {
            // NERVA at nuclear tech level 3 → EV add = 3 × 250 = 750.
            AssertReaction("NERVA-tech3",
                PropulsionDesignModel.Reaction("ntp", driveMass: 1800, exhaustVelocityBase: 7000, fuelGrade: 0.75,
                    burnCoefficient: 0.017, exhaustVelocityTechAdd: 750),
                "ntp", 7750, 1800 * 0.017 * 0.75, 7750 * (1800 * 0.017 * 0.75));

            // F1 with an EV tech add (+100) AND a burn-rate tech mult (×1.2).
            AssertReaction("F1-tech",
                PropulsionDesignModel.Reaction("rp-1", driveMass: 8400, exhaustVelocityBase: 3510, fuelGrade: 1.15,
                    burnCoefficient: 0.3, exhaustVelocityTechAdd: 100, burnRateTechMult: 1.2),
                "rp-1", 3610, 8400 * 0.3 * 1.2 * 1.15, 3610 * (8400 * 0.3 * 1.2 * 1.15));
        }

        // ---- WARP -----------------------------------------------------------------------------------------------

        private static void AssertWarp(string name, PropulsionDesignModel m,
            double expectedPower, double expectedCreation, double expectedSustain, double expectedCollapseNeg)
        {
            var p = m.Compute();
            Log($"{name}: warp power={p.EnginePower} create={p.BubbleCreationCost} sustain={p.BubbleSustainCost} collapse={p.BubbleCollapseCost}");
            Assert.That(p.Family, Is.EqualTo(PropulsionFamily.WarpFtl), $"{name} Family");
            Assert.That(p.Warp, Is.Not.Null, $"{name} produced a WarpDriveAtb");
            Assert.That(p.Reaction, Is.Null, $"{name} no reaction atb");
            // WarpPower is stored as (int) — the model must reproduce the cast.
            Assert.That(p.Warp.WarpPower, Is.EqualTo((int)expectedPower), $"{name} WarpPower ((int) cast)");
            Assert.That(p.Warp.EnergyType, Is.EqualTo("electricity"), $"{name} EnergyType");
            Assert.That(p.Warp.BubbleCreationCost, Is.EqualTo(expectedCreation).Within(1e-6), $"{name} BubbleCreationCost");
            Assert.That(p.Warp.BubbleSustainCost, Is.EqualTo(expectedSustain).Within(1e-6), $"{name} BubbleSustainCost");
            Assert.That(p.Warp.BubbleCollapseCost, Is.EqualTo(expectedCollapseNeg).Within(1e-6), $"{name} BubbleCollapseCost (negative)");
            // derived + the paired signature (magnitude = sustain × 1000)
            Assert.That(p.EnginePower, Is.EqualTo(expectedPower).Within(1e-6), $"{name} derived EnginePower (pre-cast)");
            Assert.That(p.Signature, Is.Not.Null, $"{name} produced a SensorSignatureAtb");
            Assert.That(p.Signature.PartWaveFormMag, Is.EqualTo(expectedSustain * 1000).Within(1e-6), $"{name} signature magnitude = sustain*1000");
        }

        [Test]
        [Description("The base-mod Alcubierre warp drives (2k / 500) reproduce at the neutral split: engine power = EvP × Mass × 1000, creation = power × tech × 0.5 × SvE, sustain = power × 0.001 × tech / SvE, collapse stored negative, signature = sustain × 1000.")]
        public void WarpDrives_ReproduceFromTheForm()
        {
            // stated tech baseline: creation 1.0, sustain 1.0, collapse 0.5.
            AssertWarp("alcubierre-2k",
                PropulsionDesignModel.WarpFtl(driveMass: 2000, warpCreationCostTech: 1.0, warpSustainCostTech: 1.0, warpCollapseEfficiencyTech: 0.5),
                expectedPower: 2_000_000, expectedCreation: 1_000_000, expectedSustain: 2000, expectedCollapseNeg: -500_000);

            AssertWarp("alcubierre-500",
                PropulsionDesignModel.WarpFtl(driveMass: 500, warpCreationCostTech: 1.0, warpSustainCostTech: 1.0, warpCollapseEfficiencyTech: 0.5),
                expectedPower: 500_000, expectedCreation: 250_000, expectedSustain: 500, expectedCollapseNeg: -125_000);
        }

        [Test]
        [Description("The ONE base-mod design that moves the split off neutral — the endurance 2k (SvE 2.0) — reproduces byte-identically, proving creation ×= SvE and sustain ÷= SvE (creation doubles, sustain halves vs the 2k above).")]
        public void WarpEnduranceSplit_ReproducesTheNonNeutralDial()
        {
            AssertWarp("alcubierre-2k-endurance",
                PropulsionDesignModel.WarpFtl(driveMass: 2000, warpCreationCostTech: 1.0, warpSustainCostTech: 1.0, warpCollapseEfficiencyTech: 0.5, sve: 2.0),
                expectedPower: 2_000_000, expectedCreation: 2_000_000, expectedSustain: 1000, expectedCollapseNeg: -1_000_000);
        }

        // ---- REACTIONLESS ---------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod reactionless drives (Reactionless 200 kN / High-Thrust 600 kN): thrust set directly, exhaust velocity a fixed 1e6, NO signature, and the component mass is a FLOOR that rises above the 200,000 N baseline (5000 → 7000 for 600 kN).")]
        public void ReactionlessDrives_ReproduceFromTheForm()
        {
            var basic = PropulsionDesignModel.Reactionless(thrust: 200000, driveMass: 5000).Compute();
            Log($"reactionless: thrust={basic.Thrust} mass={basic.ComponentMass}");
            Assert.That(basic.Family, Is.EqualTo(PropulsionFamily.Reactionless));
            Assert.That(basic.Reactionless, Is.Not.Null, "produced a ReactionlessThrustAtb");
            Assert.That(basic.Reactionless.ThrustInNewtons, Is.EqualTo(200000).Within(1e-6));
            Assert.That(basic.Reactionless.ExhaustVelocity, Is.EqualTo(1_000_000).Within(1e-6), "fixed exhaust velocity");
            Assert.That(basic.ComponentMass, Is.EqualTo(5000).Within(1e-6), "mass floor at baseline thrust");
            Assert.That(basic.Signature, Is.Null, "reactionless emits NO SensorSignatureAtb (matches source)");

            var high = PropulsionDesignModel.Reactionless(thrust: 600000, driveMass: 5000).Compute();
            Log($"high-thrust: thrust={high.Thrust} mass={high.ComponentMass}");
            Assert.That(high.Reactionless.ThrustInNewtons, Is.EqualTo(600000).Within(1e-6));
            // 5000 + (600000 - 200000)/200 = 5000 + 2000 = 7000
            Assert.That(high.ComponentMass, Is.EqualTo(7000).Within(1e-6), "mass floor rises with thrust");
            Assert.That(high.Signature, Is.Null);
        }

        // ---- SURFACE --------------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod ground locomotion reproduces: GroundLocomotionAtb(1.5, 0.5, false) through its clamping ctor, and NO signature. Also proves the ctor clamps (speedFactor ≥ 0.1, roughHandling ∈ [0,1], amphibious ≥ 0.5).")]
        public void GroundLocomotion_ReproducesFromTheForm()
        {
            var loco = PropulsionDesignModel.Surface(speedFactor: 1.5, roughHandling: 0.5, amphibious: false).Compute();
            Log($"ground-locomotion: speed={loco.Surface.SpeedFactor} rough={loco.Surface.RoughHandling} amph={loco.Surface.Amphibious}");
            Assert.That(loco.Family, Is.EqualTo(PropulsionFamily.Surface));
            Assert.That(loco.Surface, Is.Not.Null, "produced a GroundLocomotionAtb");
            Assert.That(loco.Surface.SpeedFactor, Is.EqualTo(1.5).Within(1e-6));
            Assert.That(loco.Surface.RoughHandling, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(loco.Surface.Amphibious, Is.False);
            Assert.That(loco.Signature, Is.Null, "surface locomotion emits NO SensorSignatureAtb");

            // the ctor clamp is reproduced (out-of-range inputs are pinned by GroundLocomotionAtb's ctor)
            var clamped = PropulsionDesignModel.Surface(speedFactor: 0.01, roughHandling: 2.0, amphibious: true).Compute();
            Assert.That(clamped.Surface.SpeedFactor, Is.EqualTo(0.1).Within(1e-6), "speedFactor clamped up to 0.1");
            Assert.That(clamped.Surface.RoughHandling, Is.EqualTo(1.0).Within(1e-6), "roughHandling clamped to 1.0");
            Assert.That(clamped.Surface.Amphibious, Is.True, "amphibious true (≥ 0.5)");
        }
    }
}
