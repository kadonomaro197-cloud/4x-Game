using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;   // MassVolumeDB
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons pilot W4 (RECOIL → tracking) — SLICE W4a, the byte-identical mechanism. A kinetic weapon's recoil kicks
    /// its own ship, so a heavy gun on a light hull tracks worse than the same gun on a battleship. This is applied at
    /// BUILD time in <see cref="ShipCombatValueDB.Calculate"/> (where the firing ship's mass is known), reducing the
    /// railgun/flak <see cref="WeaponProfile.Tracking"/> by <see cref="ShipCombatValueDB.RecoilTrackingFactor"/> — no
    /// resolver/kernel change, so it can't touch the dodge math.
    ///
    /// The invariant this pins: every base-mod kinetic weapon has Recoil 0 (an undialled/recoilless mount) → the factor
    /// is 1.0 → tracking is unchanged → combat is byte-identical. W4b adds a base-mod high-recoil weapon (a new siege
    /// gun on a new ship, like the ammo magazine's Sabre) so the penalty is reachable without perturbing any fixture.
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipRecoilTests
    {
        private const string RailgunShip = "default-ship-design-test-railgun"; // Lancer — 4 railguns, recoil 0
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-recoil] " + m);

        [Test]
        [Description("RecoilTrackingFactor pins the math: no recoil → factor 1.0 (no penalty); unknown mass → 1.0 (defensive); the SAME recoil penalises a LIGHT hull more than a heavy one, and both fall below 1.0.")]
        public void RecoilTrackingFactor_PinsTheMath()
        {
            Assert.That(ShipCombatValueDB.RecoilTrackingFactor(0, 50_000), Is.EqualTo(1.0), "no recoil → no penalty (byte-identical)");
            Assert.That(ShipCombatValueDB.RecoilTrackingFactor(20_000, 0), Is.EqualTo(1.0), "unknown mass → no penalty (defensive)");

            double light = ShipCombatValueDB.RecoilTrackingFactor(20_000, 50_000);   // 50000/70000 ≈ 0.714
            double heavy = ShipCombatValueDB.RecoilTrackingFactor(20_000, 200_000);  // 200000/220000 ≈ 0.909
            Log($"recoil 20000: light hull factor={light:0.000}, heavy hull factor={heavy:0.000}");
            Assert.That(light, Is.EqualTo(50_000.0 / 70_000.0).Within(1e-9));
            Assert.That(heavy, Is.EqualTo(200_000.0 / 220_000.0).Within(1e-9));
            Assert.That(light, Is.LessThan(heavy), "the same gun shakes a light hull off aim more than a heavy one");
            Assert.That(heavy, Is.LessThan(1.0), "even a battleship loses a little tracking to a recoiling gun");
        }

        [Test]
        [Description("W4a byte-identical: the base-mod railgun carries Recoil 0, so the built Lancer's railgun WeaponProfile keeps the atb's tracking unchanged (the recoil factor is 1.0). Proves the mechanism is inert until a weapon is dialled with recoil (W4b).")]
        public void BaseModRailgun_HasNoRecoil_SoTrackingIsUnchanged()
        {
            var s = TestScenario.CreateWithColony();
            var fac = s.Faction.GetDataBlob<FactionInfoDB>();

            var rgAtb = ((ComponentDesign)fac.IndustryDesigns["default-design-railgun-weapon"]).GetAttribute<RailgunWeaponAtb>();
            Assert.That(rgAtb.Recoil, Is.EqualTo(0), "the base-mod railgun is recoilless in v1 (no recoil dial yet — W4b)");

            var ship = ShipFactory.CreateShip(fac.ShipDesigns[RailgunShip], s.Faction, s.StartingBody, "Lancer");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            var railgunProfile = cv.Weapons.First(w => w.Delivery == WeaponDelivery.Slug && w.Nature == WeaponNature.Kinetic);
            Log($"Lancer railgun: atb.Tracking={rgAtb.Tracking}, profile.Tracking={railgunProfile.Tracking}");

            Assert.That(railgunProfile.Tracking, Is.EqualTo(rgAtb.Tracking).Within(1e-12),
                "recoil 0 → factor 1.0 → the built profile's tracking equals the atb's, so combat is byte-identical");
        }

        [Test]
        [Description("W4b cradle-to-grave: the base-mod Bombard Siege Cruiser builds from JSON, its siege-railgun binds a real Recoil (the 5-arg AtbConstrArgs → the recoil ctor — the gotcha-#0 sensor), and on the Bombard's light hull that recoil CUTS the built weapon's tracking by exactly chassisMass/(chassisMass+Recoil). So a player-built high-recoil gun tracks worse — designed → built → installed → the aim suffers.")]
        public void BuildingTheBombard_HighRecoilSiegeGun_CutsTrackingOnALightHull()
        {
            var s = TestScenario.CreateWithColony();
            var fac = s.Faction.GetDataBlob<FactionInfoDB>();

            var siegeAtb = ((ComponentDesign)fac.IndustryDesigns["default-design-siege-railgun"]).GetAttribute<RailgunWeaponAtb>();
            Assert.That(siegeAtb.Recoil, Is.GreaterThan(0),
                "the siege railgun binds a real Recoil from JSON (5-value AtbConstrArgs → the recoil-carrying ctor; the exact-arity binder gotcha)");

            var ship = ShipFactory.CreateShip(fac.ShipDesigns["default-ship-design-test-bombard"], s.Faction, s.StartingBody, "Bombard");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            var siegeProfile = cv.Weapons.First(w => w.Delivery == WeaponDelivery.Slug && w.Nature == WeaponNature.Kinetic);
            double mass = ship.GetDataBlob<MassVolumeDB>().MassDry;
            double expected = siegeAtb.Tracking * ShipCombatValueDB.RecoilTrackingFactor(siegeAtb.Recoil, mass);
            Log($"Bombard mass={mass:0} kg, siege recoil={siegeAtb.Recoil:0}, atb.Tracking={siegeAtb.Tracking}, profile.Tracking={siegeProfile.Tracking:0.0000}, expected={expected:0.0000}");

            Assert.That(siegeProfile.Tracking, Is.LessThan(siegeAtb.Tracking),
                "the built siege gun tracks worse than its raw spec — recoil throws off a light hull's aim (cradle-to-grave)");
            Assert.That(siegeProfile.Tracking, Is.EqualTo(expected).Within(1e-9),
                "the reduction is exactly the recoil factor applied to the ship's real mass");
        }
    }
}
