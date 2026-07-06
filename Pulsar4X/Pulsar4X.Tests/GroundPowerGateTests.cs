using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-unification P2b — the SUPPLY GATE: a ground unit's guns can't draw more power than its reactors make
    /// ("a Titan can, infantry can't"). The developer's locked model (2026-07-06): power is a mounted reactor COMPONENT,
    /// the gate is HARD (an under-powered design is illegal), and supply mode (Energy / Ammo / Both) is a per-weapon
    /// setting with smart defaults — a laser draws energy, flak draws ammo (no reactor), a railgun draws both.
    ///
    /// Calibration-INDEPENDENT by construction: it reads the assembler's new EnergyDemand_W / ReactorSupply_W gauges and
    /// derives how many reactors are "enough" from those numbers, so it proves the GATE LOGIC without depending on the
    /// base-mod tuning (whether a stock laser needs 1 reactor or 5). Engine-only → runs in CI. Uses the real faction
    /// designs (the same laser/flak/reactor a ship uses — the unification payoff). Design: docs/WEAPON-UNIFICATION-DESIGN.md P2.
    /// </summary>
    [TestFixture]
    public class GroundPowerGateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[power-gate] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        private static bool HasPowerProblem(GroundUnitAssemblyResult r)
            => r.Problems.Any(p => p.Contains("under-powered"));

        [Test]
        [Description("Supply mode has teeth: a laser (Energy) draws reactor power, flak (Ammo) draws none, a reactor supplies it.")]
        public void SupplyMode_LaserDrawsPower_FlakDoesnt_ReactorSupplies()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-walker-frame");

            var laser = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-laser-weapon"), 1) });
            var flak  = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-flak-weapon"), 1) });
            var react = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-reactor-2t"), 1) });
            Log($"laser demand={laser.EnergyDemand_W:0} W | flak demand={flak.EnergyDemand_W:0} W | reactor supply={react.ReactorSupply_W:0} W");

            Assert.That(laser.EnergyDemand_W, Is.GreaterThan(0), "a laser is Energy-mode — it draws reactor power");
            Assert.That(flak.EnergyDemand_W, Is.EqualTo(0), "flak is Ammo-mode — it draws NO reactor power (pellets, not watts)");
            Assert.That(react.ReactorSupply_W, Is.GreaterThan(0), "a reactor supplies power");

            // and the mode helper agrees on the classification
            Assert.That(WeaponSupply.DefaultModeFor(Part("default-design-laser-weapon")), Is.EqualTo(WeaponSupplyMode.Energy));
            Assert.That(WeaponSupply.DefaultModeFor(Part("default-design-flak-weapon")), Is.EqualTo(WeaponSupplyMode.Ammo));
            Assert.That(WeaponSupply.DefaultModeFor(Part("default-design-railgun-weapon")), Is.EqualTo(WeaponSupplyMode.Both));
        }

        [Test]
        [Description("The hard gate: a laser with no reactor is under-powered (illegal); enough reactors clear it. An ammo weapon needs none.")]
        public void PowerGate_LaserNeedsAReactor_FlakDoesnt()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-walker-frame");
            var laser = Part("default-design-laser-weapon");
            var reactor = Part("default-design-reactor-2t");

            // laser, no reactor → under-powered, and that makes the design illegal (hard gate)
            var unpowered = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (laser, 1) });
            Assert.That(unpowered.EnergyDemand_W, Is.GreaterThan(unpowered.ReactorSupply_W), "guns draw more than the (zero) supply");
            Assert.That(HasPowerProblem(unpowered), Is.True, "the gate flags it under-powered");
            Assert.That(unpowered.Valid, Is.False, "an under-powered design is illegal (hard gate)");

            // derive how many reactors are 'enough' from the gauges — calibration-independent
            double perReactor = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (reactor, 1) }).ReactorSupply_W;
            Assert.That(perReactor, Is.GreaterThan(0));
            int n = (int)Math.Ceiling(unpowered.EnergyDemand_W / perReactor);
            var powered = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (laser, 1), (reactor, n) });
            Log($"laser needs {n} x reactor-2t: supply={powered.ReactorSupply_W:0} W >= demand={powered.EnergyDemand_W:0} W");
            Assert.That(powered.ReactorSupply_W, Is.GreaterThanOrEqualTo(powered.EnergyDemand_W), "reactors now meet the draw");
            Assert.That(HasPowerProblem(powered), Is.False, "the power gate is satisfied once reactors meet the draw");

            // an ammo weapon needs no reactor at all — no power problem even bare
            var flakBare = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-flak-weapon"), 1) });
            Assert.That(HasPowerProblem(flakBare), Is.False, "flak (Ammo) fires without a reactor");
        }
    }
}
