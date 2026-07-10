using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons pilot W5 (heat → sustained-rate for energy weapons) — SLICE W5a, the byte-identical foundation. The heat
    /// twin of the ammo magazine (W3): the magazine limits how long the KINETIC guns fire, the radiator limits how hard
    /// the ENERGY guns fire SUSTAINED. W5a adds the pieces WITHOUT wiring the throttle: the <see cref="RadiatorAtb"/>
    /// component (the heat sink), <see cref="ShipCombatValueDB.HeatCapacity_kJ"/> (sum of installed radiators), and the
    /// fleet's <see cref="FleetCombatStateDB.HeatPool_kJ"/> (starts cold at 0).
    ///
    /// The invariant this pins: with NO radiator a ship reads 0 heat capacity → the heat step is skipped → combat is
    /// byte-identical (every current ship, until the W5c base-mod radiator). W5b wires the per-salvo heat accumulation +
    /// throttle; W5c adds the buildable base-mod radiator on a beam-heavy ship. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipHeatTests
    {
        private const string BeamShip = "default-ship-design-test-warship"; // Aegis — 4 lasers, no radiator yet
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-heat] " + m);

        [Test]
        [Description("RadiatorAtb (the heat sink) holds its kJ capacity and clamps a negative to 0; the fleet heat pool defaults to 0 (cold).")]
        public void RadiatorAtb_AndHeatPool_PinTheFoundation()
        {
            var rad = new RadiatorAtb(4000);
            Assert.That(rad.Capacity_kJ, Is.EqualTo(4000), "the radiator holds its kJ capacity");
            Assert.That(new RadiatorAtb(-10).Capacity_kJ, Is.EqualTo(0), "a negative capacity clamps to 0");
            Assert.That(((RadiatorAtb)rad.Clone()).Capacity_kJ, Is.EqualTo(4000), "clone preserves the capacity");

            Assert.That(new FleetCombatStateDB().HeatPool_kJ, Is.EqualTo(0), "the fleet heat pool starts cold (0)");
        }

        [Test]
        [Description("W5a additive/byte-identical: a real base-mod beam warship with NO radiator reads HeatCapacity_kJ == 0 (so its fleet heat step is skipped and the resolve is untouched), exactly as an unshielded/magazine-less ship reads 0.")]
        public void ARealBeamShip_WithNoRadiator_ReadsZeroHeatCapacity()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(BeamShip), Is.True, "the Aegis beam warship loads onto the faction");

            var ship = ShipFactory.CreateShip(designs[BeamShip], s.Faction, s.StartingBody, "Aegis");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Aegis (no radiator): firepower={cv.Firepower:0}, heatCapacity={cv.HeatCapacity_kJ:0} kJ");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Aegis carries its lasers (energy fire)");
            Assert.That(cv.HeatCapacity_kJ, Is.EqualTo(0),
                "no radiator → 0 heat capacity → the fleet heat step stays disabled → combat byte-identical until the W5c radiator");
        }
    }
}
