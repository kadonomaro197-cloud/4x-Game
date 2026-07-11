using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensors ⚙4 — FIRE-CONTROL RANGE, the last dead knob in the fire-control door. The director is the SCOPE: it
    /// extends how far a ship's beams can ENGAGE, out past their rated MaxRange (accuracy still falls off with distance,
    /// so the extra reach is chancy — "hit at X, and beyond if you're lucky"). The dead `BeamFireControlAtbDB.Range`
    /// (verified zero reads) comes alive: <see cref="ShipCombatValueDB.EnableFireControlRange"/> (default OFF →
    /// byte-identical; the client turns it on) makes a ship's beam reach = MaxRange × (1 + bestDirectorRange/100).
    /// Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipFireControlRangeTests
    {
        private const string Aegis = "default-ship-design-test-warship"; // lasers + a beam-fire-control director
        private static void Log(string m) => TestContext.Progress.WriteLine("[fc-range] " + m);

        /// <summary>The best (largest Range_m) BEAM weapon reach the auto-resolver reads off a built ship.</summary>
        private static double MaxBeamRange(Entity ship)
        {
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            return cv.Weapons.Where(w => w.Delivery == WeaponDelivery.Beam).Select(w => w.Range_m).DefaultIfEmpty(0).Max();
        }

        [Test]
        [Description("The range-extension factor reads the director's Range dial as a percent of the weapon's rated range: 0 → ×1.0 (no director, no extension), 20 → ×1.20, 100 → ×2.0.")]
        public void FireControlRangeFactor_ReadsRangeAsPercent()
        {
            Assert.That(ShipCombatValueDB.FireControlRangeFactor(0), Is.EqualTo(1.0), "no director → no extension");
            Assert.That(ShipCombatValueDB.FireControlRangeFactor(20), Is.EqualTo(1.20).Within(1e-9));
            Assert.That(ShipCombatValueDB.FireControlRangeFactor(100), Is.EqualTo(2.0).Within(1e-9));
            Assert.That(ShipCombatValueDB.FireControlRangeFactor(-5), Is.EqualTo(1.0), "a non-positive range never shrinks reach");
        }

        [Test]
        [Description("End-to-end: with the flag OFF a ship's beam reach is exactly its rated MaxRange (byte-identical); flip the flag ON and the same hull's installed director (the scope) extends that reach — a bigger director = open fire from further. The base-mod Aegis (lasers + a beam-fire-control) reads +20% reach (Range 20).")]
        public void Director_ExtendsBeamEngagementRange_WhenOn_ByteIdenticalOff()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns[Aegis];

            bool prev = ShipCombatValueDB.EnableFireControlRange;
            try
            {
                // OFF → the beam carries its rated range (the byte-identity contract).
                ShipCombatValueDB.EnableFireControlRange = false;
                double rangeOff = MaxBeamRange(ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "AegisOff"));

                // ON → the installed director extends the reach.
                ShipCombatValueDB.EnableFireControlRange = true;
                double rangeOn = MaxBeamRange(ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "AegisOn"));
                Log($"beam reach: off {rangeOff:N0} m → on {rangeOn:N0} m (×{rangeOn / rangeOff:0.00})");

                Assert.That(rangeOff, Is.GreaterThan(0), "the Aegis's lasers have a finite rated range to extend");
                Assert.That(rangeOn, Is.GreaterThan(rangeOff), "the director (scope) opens fire from further out");
                Assert.That(rangeOn, Is.EqualTo(rangeOff * 1.20).Within(rangeOff * 1e-6),
                    "the base-mod director's Range 20 → +20% engagement reach");
            }
            finally { ShipCombatValueDB.EnableFireControlRange = prev; }
        }
    }
}
