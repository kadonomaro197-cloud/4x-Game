using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components.Designers;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the WEAPONS door parametric designer, slice-1b FIDELITY cross-check.
    ///
    /// WHAT THIS ADDS OVER THE PURE GAUGE (and why it exists). The pure gauge
    /// <see cref="WeaponsDesignModelTests"/> asserts the model against numbers TRANSCRIBED into that test file — it
    /// proves the model's arithmetic is internally consistent, but it can't catch a case where BOTH the model and its
    /// transcribed twin drifted away from what the LIVE engine actually computes for the shipped weapon. This fixture
    /// closes that gap: it builds every base-mod warship the GAME way (JSON template → `*Atb` via reflection →
    /// <see cref="ShipCombatValueDB.Calculate"/>), READS the real <see cref="WeaponProfile"/> the combat resolver will
    /// fight with, and proves <see cref="WeaponsDesignModel.BuildProfile"/> REPRODUCES that live profile field-by-field.
    /// The expected value in every assertion is READ FROM THE LIVE DESIGN, never a literal — so a real reproduction bug
    /// (the model deriving a velocity/tracking/range the engine doesn't) fails here instead of shipping green.
    ///
    /// THE LOAD-BEARING ASSERTIONS are the CHOICE-DERIVED fields the model computes WITHOUT being told them:
    ///   • a BEAM (laser)      → the model derives velocity=light-speed + tracking=0.95 from the (Energy,Beam) choice;
    ///                            this cross-checks that the base-mod laser's real BeamSpeed==c and BaseHitChance==0.95f.
    ///   • an EXOTIC BEAM (ion) → velocity + tracking + range ALL derived (light-speed / 1.0 / DisruptorRange).
    ///   • a GUIDED missile    → velocity + tracking + range all derived (the 5000/0.9/1000 km stubs).
    ///   • a FINITE weapon (railgun/flak/plasma) → its class RANGE is derived (RailgunRange/FlakRange); velocity+tracking
    ///                            are the genuine per-weapon dials (the honesty caveat) fed through and asserted equal.
    /// damage/saturation/penetration/per-shot/heat pass straight through — fed from the live profile, so they can only
    /// match; their value here is proving the model doesn't silently DROP or MANGLE a pass-through field.
    ///
    /// Rides the colony harness (builds real ships) → the slow CI shard; the colony is stood up ONCE in
    /// <see cref="OneTimeSetUp"/>. Byte-identical to the live game (nothing calls the model yet).
    /// </summary>
    [TestFixture]
    public class WeaponsDesignModelFidelityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[weapons-fidelity] " + m);

        // The base-mod warships that mount each weapon type (all in earth.json ShipDesigns → buildable via the harness).
        private const string LaserShip     = "default-ship-design-test-warship";   // Aegis   — lasers   (Energy × Beam)
        private const string RailgunShip   = "default-ship-design-test-railgun";   // Lancer  — railguns (Kinetic × Slug)
        private const string FlakShip      = "default-ship-design-test-flak";      // Bulwark — flak     (Kinetic × Cloud)
        private const string DisruptorShip = "default-ship-design-test-disruptor"; // Ravager — ion      (Exotic × Beam)
        private const string PlasmaShip    = "default-ship-design-test-plasma";    // Vanguard— plasma   (Energy × Bolt)
        private const string MissileShip   = "default-ship-design-test-missile";   // Javelin — missile  (Explosive × Guided)

        // The real, live-computed weapon profile per delivery, captured once from the built ships.
        private WeaponProfile _laser, _railgun, _flak, _disruptor, _plasma, _missile;
        private int _assertions;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Pin the combat flags that would perturb the built profile to their engine defaults, so the ships we build
            // and read are the byte-identical baseline (fire-control tracking/range OFF → beam tracking = BaseHitChance,
            // beam range = design MaxRange; guided-warhead OFF → missile firepower = the flat stub). All default false.
            ShipCombatValueDB.EnableFireControlTracking = false;
            ShipCombatValueDB.EnableFireControlRange = false;
            ShipCombatValueDB.EnableFinalFireOnlyPD = false;
            ShipCombatValueDB.EnableGuidedWarheadFirepower = false;

            var s = TestScenario.CreateWithColony();               // the slow path — called ONCE
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            _laser     = FirstProfile(s, designs, LaserShip,     WeaponNature.Energy,    WeaponDelivery.Beam,   "laser");
            _railgun   = FirstProfile(s, designs, RailgunShip,   WeaponNature.Kinetic,   WeaponDelivery.Slug,   "railgun");
            _flak      = FirstProfile(s, designs, FlakShip,      WeaponNature.Kinetic,   WeaponDelivery.Cloud,  "flak");
            _disruptor = FirstProfile(s, designs, DisruptorShip, WeaponNature.Exotic,    WeaponDelivery.Beam,   "disruptor");
            _plasma    = FirstProfile(s, designs, PlasmaShip,    WeaponNature.Energy,    WeaponDelivery.Bolt,   "plasma");
            _missile   = FirstProfile(s, designs, MissileShip,   WeaponNature.Explosive, WeaponDelivery.Guided, "missile");
        }

        /// <summary>Build a base-mod ship the game way and return the first weapon profile of the given nature/delivery
        /// (the LIVE value <see cref="ShipCombatValueDB.Calculate"/> produced). Fails loudly if the design or the weapon
        /// isn't there — the gotcha-10 registration sensor.</summary>
        private static WeaponProfile FirstProfile(TestScenario s, IReadOnlyDictionary<string, ShipDesign> designs,
            string designId, WeaponNature nature, WeaponDelivery delivery, string label)
        {
            Assert.That(designs.ContainsKey(designId), Is.True,
                $"base-mod ship '{designId}' ({label}) is unlocked on the start faction (JSON registration chain intact)");
            var ship = ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, label + " hull");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            var p = cv.Weapons.FirstOrDefault(w => w.Nature == nature && w.Delivery == delivery);
            Assert.That(p, Is.Not.Null,
                $"'{designId}' produced a {nature}/{delivery} weapon profile (JSON template → Atb → ShipCombatValueDB is wired for {label})");
            Log($"LIVE {label}: {p.Nature}/{p.Delivery} dps={p.DamagePerSecond:0.###} vel={p.Velocity:0} " +
                $"trk={p.Tracking:0.########} sat={p.Saturation:0.###} range={p.Range_m:0} heat={p.HeatPerSecond:0} " +
                $"pen={p.Penetration:0.###} perShot={p.PerShotEnergy:0}");
            return p;
        }

        // ── field-by-field comparison of a model's output against a LIVE profile ────────────────────────────────────

        /// <summary>Assert one field of the model's built profile equals the value the LIVE engine computed. A
        /// magnitude-relative tolerance (floor 1e-6) absorbs the one honest precision gap — the engine stores a beam's
        /// BaseHitChance as a <c>float</c> 0.95f (≈0.9499999881 as a double) while the model uses the double literal
        /// 0.95 — a ~1.2e-8 difference that is NOT a reproduction bug.</summary>
        private void Close(double actual, double expected, string field, string weapon)
        {
            double tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected).Within(tol), $"{weapon}: {field} (model vs LIVE)");
        }

        /// <summary>Build the reproducing model for a live profile (letting the CHOICE-derived fields fall out where the
        /// model derives them) and assert <see cref="WeaponsDesignModel.BuildProfile"/> matches the live profile on
        /// every field. The model construction mirrors how the designer form would reproduce each weapon:
        ///   • Beam(non-exotic): feed dps/sat/range/heat, let velocity+tracking DERIVE (light-speed / 0.95).
        ///   • Beam(exotic) + Guided: feed dps/sat only, let velocity+tracking+range DERIVE.
        ///   • Slug/Bolt/Cloud: feed the genuine velocity+tracking dials + dps/sat/pen/perShot, let RANGE derive.</summary>
        private void AssertReproduces(string weapon, WeaponProfile p)
        {
            WeaponsDesignModel model;
            switch (p.Delivery)
            {
                case WeaponDelivery.Beam when p.Nature == WeaponNature.Exotic:
                    // Exotic ion beam: velocity + tracking + range are ALL forced by the (Exotic, Beam) choice.
                    model = new WeaponsDesignModel(p.Nature, p.Delivery, p.DamagePerSecond, p.Saturation,
                        heatPerSecond: p.HeatPerSecond, penetration: p.Penetration, perShotEnergy: p.PerShotEnergy);
                    break;

                case WeaponDelivery.Beam:
                    // Laser/pulse beam: light-speed + 0.95 tracking DERIVE; the beam carries its own reach + heat.
                    model = new WeaponsDesignModel(p.Nature, p.Delivery, p.DamagePerSecond, p.Saturation,
                        range_m: p.Range_m, heatPerSecond: p.HeatPerSecond,
                        penetration: p.Penetration, perShotEnergy: p.PerShotEnergy);
                    break;

                case WeaponDelivery.Guided:
                    // Missile: the slow-long-tracker stubs (velocity/tracking/range) DERIVE from the (Explosive, Guided)
                    // choice; saturation passes through (the stub the engine used).
                    model = new WeaponsDesignModel(p.Nature, p.Delivery, p.DamagePerSecond, p.Saturation,
                        heatPerSecond: p.HeatPerSecond, penetration: p.Penetration, perShotEnergy: p.PerShotEnergy);
                    break;

                default:
                    // Finite-velocity (Slug/Bolt/Cloud): velocity + tracking are the genuine per-weapon dials (fed
                    // through, the honesty caveat); the class RANGE is what the model DERIVES and this proves.
                    model = new WeaponsDesignModel(p.Nature, p.Delivery, p.DamagePerSecond, p.Saturation,
                        velocity: p.Velocity, tracking: p.Tracking,
                        penetration: p.Penetration, perShotEnergy: p.PerShotEnergy, heatPerSecond: p.HeatPerSecond);
                    break;
            }

            var built = model.BuildProfile();
            Log($"MODEL {weapon}: {built.Nature}/{built.Delivery} dps={built.DamagePerSecond:0.###} vel={built.Velocity:0} " +
                $"trk={built.Tracking:0.########} sat={built.Saturation:0.###} range={built.Range_m:0} heat={built.HeatPerSecond:0}");

            Assert.That(built.Nature, Is.EqualTo(p.Nature), $"{weapon}: Nature");
            Assert.That(built.Delivery, Is.EqualTo(p.Delivery), $"{weapon}: Delivery");
            Close(built.DamagePerSecond, p.DamagePerSecond, "DamagePerSecond", weapon);
            Close(built.Velocity,        p.Velocity,        "Velocity",        weapon);
            Close(built.Tracking,        p.Tracking,        "Tracking",        weapon);
            Close(built.Saturation,      p.Saturation,      "Saturation",      weapon);
            Close(built.Range_m,         p.Range_m,         "Range_m",         weapon);
            Close(built.HeatPerSecond,   p.HeatPerSecond,   "HeatPerSecond",   weapon);
            Close(built.Penetration,     p.Penetration,     "Penetration",     weapon);
            Close(built.PerShotEnergy,   p.PerShotEnergy,   "PerShotEnergy",   weapon);
            _assertions += 2; // Nature + Delivery
        }

        [Test]
        [Description("The base-mod LASER (Aegis) — the model derives light-speed + 0.95 tracking from the (Energy,Beam) choice, and that matches the live beam's BeamSpeed + BaseHitChance; range/heat pass through.")]
        public void Laser_ModelReproducesLiveProfile()
        {
            AssertReproduces("laser", _laser);
            // Pin the two derived beam facts against the live profile so the cross-check reads clearly:
            Assert.That(_laser.Velocity, Is.EqualTo(ShipCombatValueDB.LightSpeed_mps).Within(1),
                "LIVE laser BeamSpeed IS light-speed — what the model derives");
            Assert.That(_laser.Tracking, Is.EqualTo(0.95).Within(1e-6),
                "LIVE laser BaseHitChance IS 0.95 (float→double) — what the model derives");
        }

        [Test]
        [Description("The base-mod RAILGUN (Lancer) — velocity + tracking are the genuine dials (fed through); the model DERIVES the mid class range and it matches the live RailgunRange.")]
        public void Railgun_ModelReproducesLiveProfile()
        {
            AssertReproduces("railgun", _railgun);
            Assert.That(_railgun.Range_m, Is.EqualTo(ShipCombatValueDB.RailgunRange_m).Within(1),
                "LIVE railgun uses the mid class range — what the model derives from the Slug delivery");
        }

        [Test]
        [Description("The base-mod FLAK (Bulwark) — the model DERIVES the short class range (FlakRange) and it matches the live profile; saturation = rounds×pellets passes through.")]
        public void Flak_ModelReproducesLiveProfile()
        {
            AssertReproduces("flak", _flak);
            Assert.That(_flak.Range_m, Is.EqualTo(ShipCombatValueDB.FlakRange_m).Within(1),
                "LIVE flak uses the short class range — what the model derives from the Cloud delivery");
        }

        [Test]
        [Description("The base-mod ION DISRUPTOR (Ravager) — the (Exotic,Beam) choice forces velocity + tracking + range ALL by derivation; each matches the live exotic profile (light-speed / 1.0 / DisruptorRange).")]
        public void Disruptor_ModelReproducesLiveProfile()
        {
            AssertReproduces("disruptor", _disruptor);
            Assert.That(_disruptor.Velocity, Is.EqualTo(ShipCombatValueDB.LightSpeed_mps).Within(1), "LIVE disruptor is light-speed");
            Assert.That(_disruptor.Tracking, Is.EqualTo(1.0).Within(1e-9), "LIVE disruptor tracks perfectly (1.0)");
            Assert.That(_disruptor.Range_m, Is.EqualTo(ShipCombatValueDB.DisruptorRange_m).Within(1), "LIVE disruptor uses its own class range");
        }

        [Test]
        [Description("The base-mod PLASMA (Vanguard) — the two-axis corner: a finite Energy bolt. Velocity+tracking fed through; the model DERIVES the mid class range and it matches the live bolt.")]
        public void Plasma_ModelReproducesLiveProfile()
        {
            AssertReproduces("plasma", _plasma);
            Assert.That(_plasma.Range_m, Is.EqualTo(ShipCombatValueDB.RailgunRange_m).Within(1),
                "LIVE plasma reuses the mid class range — what the model derives from the Bolt delivery");
        }

        [Test]
        [Description("The base-mod MISSILE LAUNCHER (Javelin) — the (Explosive,Guided) choice derives the slow-long-tracker stubs (velocity 5000 / tracking 0.9 / 1000 km range); each matches the live profile, firepower fed through.")]
        public void Missile_ModelReproducesLiveProfile()
        {
            AssertReproduces("missile", _missile);
            Assert.That(_missile.Velocity, Is.EqualTo(ShipCombatValueDB.MissileVelocityStub_mps).Within(1e-6), "LIVE missile velocity is the stub");
            Assert.That(_missile.Tracking, Is.EqualTo(ShipCombatValueDB.MissileTrackingStub).Within(1e-9), "LIVE missile tracking is the stub");
            Assert.That(_missile.Range_m, Is.EqualTo(ShipCombatValueDB.MissileRange_m).Within(1), "LIVE missile uses the long class range");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => Log($"total field-by-field assertions across all 6 base-mod weapons: {_assertions}");
    }
}
