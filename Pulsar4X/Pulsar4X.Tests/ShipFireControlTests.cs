using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensors ⚙3 — the Fire-Control CONNECT (the dossier's "purest CONNECT door: dials built, dead"). The
    /// <see cref="Weapons.BeamFireControlAtbDB"/> component carries a <c>TrackingSpeed</c> dial that NOTHING read —
    /// a ship's beams tracked purely on their own <c>BaseHitChance</c>, and the director was decoration. This wires
    /// that dead knob: <see cref="ShipCombatValueDB.Calculate"/> now raises a ship's BEAM
    /// <see cref="WeaponProfile.Tracking"/> toward 1.0 by its best installed director, so a better fire control lands
    /// more fire on an evasive target.
    ///
    /// Because the fire-control component ALREADY lives on the base-mod warships (with a non-neutral TrackingSpeed),
    /// wiring it is a behaviour change — so it's gated behind <see cref="ShipCombatValueDB.EnableFireControlTracking"/>
    /// (default OFF → every existing combat fixture is byte-identical; the client turns it ON), exactly like the
    /// closing-range / detection flags. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipFireControlTests
    {
        private const string Aegis = "default-ship-design-test-warship"; // 4 lasers + a beam-fire-control director
        private static void Log(string m) => TestContext.Progress.WriteLine("[fire-control] " + m);

        [TearDown]
        public void ResetFlag() => ShipCombatValueDB.EnableFireControlTracking = false; // never leak the static flag

        [Test]
        [Description("Pure: the director tracking factor is 0 with no director, 0.5 at the reference speed (5000), and rises monotonically — ts/(ts+reference).")]
        public void FireControlTrackingFactor_ZeroWithNone_HalfAtReference_Monotonic()
        {
            Assert.That(ShipCombatValueDB.FireControlTrackingFactor(0), Is.EqualTo(0), "no director → no tracking contribution");
            Assert.That(ShipCombatValueDB.FireControlTrackingFactor(ShipCombatValueDB.FireControlTrackingReference),
                Is.EqualTo(0.5).Within(1e-9), "at the reference speed the director half-closes the gap to a perfect track");
            Assert.That(ShipCombatValueDB.FireControlTrackingFactor(15000),
                Is.GreaterThan(ShipCombatValueDB.FireControlTrackingFactor(5000)), "a faster director tracks better");
        }

        [Test]
        [Description("The CONNECT, byte-identical-gated: a base-mod warship's beam tracking is UNCHANGED with the flag off (every fixture byte-identical) and BOOSTED with the flag on (its beam-fire-control director now improves the tracking) — never exceeding 1.0. The dead TrackingSpeed knob comes alive.")]
        public void AWarship_BeamTracking_IsBoostedByItsDirector_OnlyWhenEnabled()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            // Flag OFF (default) — the baseline every combat fixture sees.
            ShipCombatValueDB.EnableFireControlTracking = false;
            var off = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis-off");
            double trackOff = off.GetDataBlob<ShipCombatValueDB>().Weapons.First(w => w.Delivery == WeaponDelivery.Beam).Tracking;

            // Flag ON — the director now boosts the beams' tracking.
            ShipCombatValueDB.EnableFireControlTracking = true;
            var on = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis-on");
            double trackOn = on.GetDataBlob<ShipCombatValueDB>().Weapons.First(w => w.Delivery == WeaponDelivery.Beam).Tracking;

            Log($"Aegis beam tracking: flag-off={trackOff:0.000}, flag-on={trackOn:0.000}");
            Assert.That(trackOn, Is.GreaterThan(trackOff),
                "the beam-fire-control director boosts beam tracking when the wire is enabled (the dead knob is alive)");
            Assert.That(trackOn, Is.LessThanOrEqualTo(1.0), "tracking never exceeds a perfect track");
        }
    }
}
