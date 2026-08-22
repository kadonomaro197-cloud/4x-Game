using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components.Designers;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the WEAPONS door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="WeaponsDesignModel"/> (the engine half of the two-choice/four-slider weapon form)
    /// REPRODUCES every base-mod weapon's <see cref="WeaponProfile"/> from its two choices (Nature × Delivery) + dials —
    /// i.e. every hand-authored weapon falls out of the one parametric form (the DESIGNER-NORTH-STAR reproduction claim,
    /// made executable). Pure (no colony harness → fast, not the slow CI shard); the reproduction VALUES are the ones
    /// <see cref="ShipCombatValueDB.Calculate"/> computes for each weapon (verified against source), so a drift in the
    /// model's per-delivery derivation fails here. Byte-identical to the live game (nothing calls the model yet).
    ///
    /// The load-bearing assertions are the DERIVED fields (velocity / tracking / range) the model computes from the
    /// (Delivery, Nature) choice — damage/saturation/penetration/heat pass straight through, so the model's value is the
    /// FORCED-by-delivery knowledge (a beam is light-speed at its own reach; a railgun takes the mid class range; a
    /// missile is a slow long tracker). This is the reference the other 11 door models mirror.
    /// </summary>
    [TestFixture]
    public class WeaponsDesignModelTests
    {
        private const double C = ShipCombatValueDB.LightSpeed_mps;          // 299,792,458
        private const double RailRange = ShipCombatValueDB.RailgunRange_m;  // 500,000
        private const double FlakRange = ShipCombatValueDB.FlakRange_m;     // 50,000
        private const double DisRange = ShipCombatValueDB.DisruptorRange_m; // 400,000
        private const double MslRange = ShipCombatValueDB.MissileRange_m;   // 1,000,000
        private const double MslVel = ShipCombatValueDB.MissileVelocityStub_mps;   // 5,000
        private const double MslTrk = ShipCombatValueDB.MissileTrackingStub;       // 0.9

        private static void Log(string m) => TestContext.Progress.WriteLine("[weapons-model] " + m);

        private static void AssertProfile(string name, WeaponsDesignModel m,
            WeaponNature nature, WeaponDelivery delivery, double dps, double vel, double trk, double sat, double range,
            double heat = 0, double pen = 0, double perShot = 0)
        {
            var p = m.BuildProfile();
            Log($"{name}: {p.Nature}/{p.Delivery} dps={p.DamagePerSecond} vel={p.Velocity} trk={p.Tracking} sat={p.Saturation} range={p.Range_m} heat={p.HeatPerSecond}");
            Assert.That(p.Nature, Is.EqualTo(nature), $"{name} Nature");
            Assert.That(p.Delivery, Is.EqualTo(delivery), $"{name} Delivery");
            Assert.That(p.DamagePerSecond, Is.EqualTo(dps).Within(1e-6), $"{name} DamagePerSecond (pass-through)");
            Assert.That(p.Velocity, Is.EqualTo(vel).Within(1e-6), $"{name} Velocity (delivery-derived)");
            Assert.That(p.Tracking, Is.EqualTo(trk).Within(1e-9), $"{name} Tracking (delivery-derived)");
            Assert.That(p.Saturation, Is.EqualTo(sat).Within(1e-9), $"{name} Saturation (pass-through)");
            Assert.That(p.Range_m, Is.EqualTo(range).Within(1e-6), $"{name} Range_m (delivery class range / beam MaxRange)");
            Assert.That(p.HeatPerSecond, Is.EqualTo(heat).Within(1e-6), $"{name} HeatPerSecond");
            Assert.That(p.Penetration, Is.EqualTo(pen).Within(1e-9), $"{name} Penetration");
            Assert.That(p.PerShotEnergy, Is.EqualTo(perShot).Within(1e-6), $"{name} PerShotEnergy");
        }

        [Test]
        [Description("Every base-mod BEAM weapon (laser / long-range laser / pulse laser / ion disruptor) falls out of the form: the beam delivery forces light-speed + its own reach, and the exotic nature turns a beam into the disruptor.")]
        public void BeamWeapons_ReproduceFromTheForm()
        {
            // laser: Energy×Beam, only (dps, sat, range) needed — velocity/tracking fall out (light-speed, 0.95).
            AssertProfile("laser",
                new WeaponsDesignModel(WeaponNature.Energy, WeaponDelivery.Beam, damagePerSecond: 96904.6, saturation: 0.1, range_m: 5000),
                WeaponNature.Energy, WeaponDelivery.Beam, 96904.6, C, 0.95, 0.1, 5000);

            // long-range laser: same, only the reach slider moves.
            AssertProfile("long-range-laser",
                new WeaponsDesignModel(WeaponNature.Energy, WeaponDelivery.Beam, damagePerSecond: 96904.6, saturation: 0.1, range_m: 10000),
                WeaponNature.Energy, WeaponDelivery.Beam, 96904.6, C, 0.95, 0.1, 10000);

            // pulse laser: a hot beam — heat is a real input, everything else falls out.
            AssertProfile("pulse-laser",
                new WeaponsDesignModel(WeaponNature.Energy, WeaponDelivery.Beam, damagePerSecond: 500000, saturation: 1.0, range_m: 5000, heatPerSecond: 300000),
                WeaponNature.Energy, WeaponDelivery.Beam, 500000, C, 0.95, 1.0, 5000, heat: 300000);

            // ion disruptor: EXOTIC×Beam — same delivery, different nature → light-speed, tracks 1.0, its own 400 km reach.
            AssertProfile("disruptor",
                new WeaponsDesignModel(WeaponNature.Exotic, WeaponDelivery.Beam, damagePerSecond: 300000, saturation: 2),
                WeaponNature.Exotic, WeaponDelivery.Beam, 300000, C, 1.0, 2, DisRange);
        }

        [Test]
        [Description("Every base-mod FINITE-velocity weapon (railgun + its high-velocity variant, siege railgun, flak + heavy flak, plasma + its high-velocity variant): velocity is a REAL dial (the honesty caveat — the variants differ ONLY in it) and each delivery takes its fixed class range.")]
        public void FiniteVelocityWeapons_ReproduceFromTheForm()
        {
            // railgun: Kinetic×Slug — velocity + tracking are supplied (the finite-weapon inputs); range = mid class.
            AssertProfile("railgun",
                new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Slug, damagePerSecond: 1_000_000, saturation: 5, velocity: 50000, tracking: 0.05),
                WeaponNature.Kinetic, WeaponDelivery.Slug, 1_000_000, 50000, 0.05, 5, RailRange);

            // high-velocity railgun: the SAME form, only the velocity dial moved (proves the caveat — one dial, a new weapon).
            AssertProfile("high-velocity-railgun",
                new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Slug, damagePerSecond: 1_000_000, saturation: 5, velocity: 200000, tracking: 0.05),
                WeaponNature.Kinetic, WeaponDelivery.Slug, 1_000_000, 200000, 0.05, 5, RailRange);

            // siege railgun: the design tracking is 0.05 (recoil→tracking is a mount-time penalty, not a design property).
            AssertProfile("siege-railgun",
                new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Slug, damagePerSecond: 1_600_000, saturation: 2, velocity: 50000, tracking: 0.05),
                WeaponNature.Kinetic, WeaponDelivery.Slug, 1_600_000, 50000, 0.05, 2, RailRange);

            // flak: Kinetic×Cloud — short class range; saturation = rounds/sec × pellets is a supplied dial.
            AssertProfile("flak",
                new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Cloud, damagePerSecond: 300000, saturation: 300, velocity: 20000, tracking: 0.1),
                WeaponNature.Kinetic, WeaponDelivery.Cloud, 300000, 20000, 0.1, 300, FlakRange);

            // heavy flak: only the total damage moved (more per pellet).
            AssertProfile("heavy-flak",
                new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Cloud, damagePerSecond: 1_500_000, saturation: 300, velocity: 20000, tracking: 0.1),
                WeaponNature.Kinetic, WeaponDelivery.Cloud, 1_500_000, 20000, 0.1, 300, FlakRange);

            // plasma: ENERGY×Bolt — the two-axis corner: a finite dodgeable bolt (like a slug) but energy nature (bleeds
            // shields). Reuses the mid class range.
            AssertProfile("plasma",
                new WeaponsDesignModel(WeaponNature.Energy, WeaponDelivery.Bolt, damagePerSecond: 300000, saturation: 3, velocity: 200000, tracking: 0.1),
                WeaponNature.Energy, WeaponDelivery.Bolt, 300000, 200000, 0.1, 3, RailRange);

            // high-velocity plasma: only the bolt velocity moved.
            AssertProfile("high-velocity-plasma",
                new WeaponsDesignModel(WeaponNature.Energy, WeaponDelivery.Bolt, damagePerSecond: 300000, saturation: 3, velocity: 600000, tracking: 0.1),
                WeaponNature.Energy, WeaponDelivery.Bolt, 300000, 600000, 0.1, 3, RailRange);
        }

        [Test]
        [Description("The missile launcher: Explosive×Guided → the slow long-range tracker stubs fall out (velocity 5000, tracking 0.9, 1000 km reach) from just the two choices + firepower.")]
        public void GuidedMissile_ReproducesFromTheForm()
        {
            AssertProfile("missile",
                new WeaponsDesignModel(WeaponNature.Explosive, WeaponDelivery.Guided, damagePerSecond: 100000, saturation: 1.0),
                WeaponNature.Explosive, WeaponDelivery.Guided, 100000, MslVel, MslTrk, 1.0, MslRange);
        }

        [Test]
        [Description("The armour dials (penetration / per-shot energy) pass through unchanged — an AP or alpha weapon is the same form with those sliders raised.")]
        public void ArmourDials_PassThrough()
        {
            var m = new WeaponsDesignModel(WeaponNature.Kinetic, WeaponDelivery.Slug, damagePerSecond: 800000, saturation: 2,
                velocity: 50000, tracking: 0.05, penetration: 40, perShotEnergy: 400000);
            AssertProfile("ap-slug", m, WeaponNature.Kinetic, WeaponDelivery.Slug, 800000, 50000, 0.05, 2, RailRange,
                pen: 40, perShot: 400000);
        }
    }
}
