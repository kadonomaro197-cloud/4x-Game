using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-unification P2c-a — the AMMO GATE (the ammo-axis twin of the P2b power gate): an ammo-fed weapon (flak
    /// pellets, railgun slugs) needs a MAGAZINE to be a legal ground-unit design. The developer's ammo model (2026-07-06):
    /// magazines are measured in MASS, ammo depletes in combat, and units resupply from ships/units/bases — this slice is
    /// the first rung, the design-time "must carry a magazine" gate. Combat depletion + resupply + a buildable base-mod
    /// magazine are the following slices (there is no base-mod magazine yet, so the gate BITES for every ammo weapon —
    /// which is correct until the magazine component ships; no live designer UI can hit this yet).
    ///
    /// Isolates the ammo gate with FLAK (pure Ammo — draws no reactor power, so the only problem is the missing magazine).
    /// Engine-only → runs in CI. Uses the real faction flak/laser/railgun. Design: docs/WEAPON-UNIFICATION-DESIGN.md P2.
    /// </summary>
    [TestFixture]
    public class GroundAmmoGateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ammo-gate] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        private static bool HasAmmoProblem(GroundUnitAssemblyResult r) => r.Problems.Any(p => p.Contains("magazine"));
        private static bool HasPowerProblem(GroundUnitAssemblyResult r) => r.Problems.Any(p => p.Contains("under-powered"));

        [Test]
        [Description("The ammo gate: flak (Ammo) needs a magazine to be legal; an energy weapon (laser) doesn't. Supply mode drives it.")]
        public void AmmoGate_FlakNeedsAMagazine_LaserDoesnt()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-walker-frame");

            // flak is pure Ammo — no reactor power needed, so the ONLY thing wrong is the missing magazine
            var flak = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-flak-weapon"), 1) });
            Log($"flak: ammoCap={flak.AmmoCapacity_kg:0} kg valid={flak.Valid} ammoProblem={HasAmmoProblem(flak)} powerProblem={HasPowerProblem(flak)}");
            Assert.That(flak.AmmoCapacity_kg, Is.EqualTo(0), "no magazine mounted yet");
            Assert.That(HasAmmoProblem(flak), Is.True, "flak (Ammo) with no magazine is flagged");
            Assert.That(flak.Valid, Is.False, "an ammo weapon with no magazine is an illegal design (hard gate)");
            Assert.That(HasPowerProblem(flak), Is.False, "flak draws no reactor power — the gate is about ammo, not power");

            // an energy weapon needs no magazine (it's fed by a reactor, not a magazine)
            var laser = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-laser-weapon"), 1) });
            Assert.That(HasAmmoProblem(laser), Is.False, "a laser (Energy) needs no magazine");

            // supply mode is the driver: Ammo & Both draw ammo, Energy doesn't
            Assert.That(WeaponSupply.DrawsAmmo(Part("default-design-flak-weapon")), Is.True, "flak = Ammo");
            Assert.That(WeaponSupply.DrawsAmmo(Part("default-design-railgun-weapon")), Is.True, "railgun = Both (also draws ammo)");
            Assert.That(WeaponSupply.DrawsAmmo(Part("default-design-laser-weapon")), Is.False, "laser = Energy (no ammo)");
        }
    }
}
