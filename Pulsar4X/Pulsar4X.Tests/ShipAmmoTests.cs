using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons pilot W3 (mid-battle ammo depletion for the SHIP resolver) — SLICE W3a, the byte-identical foundation.
    /// The ground side already depletes ammo (<c>GroundAmmo</c>/<c>GroundMagazineAtb</c>); the space stepped resolve
    /// never dried a magazine. W3a adds the pieces WITHOUT wiring the drain: the <see cref="ShipMagazineAtb"/> component
    /// (the ammo store), <see cref="ShipCombatValueDB.AmmoCapacity_kg"/> (sum of installed magazines, health-scaled), and
    /// the fleet's <see cref="FleetCombatStateDB.AmmoPool_kg"/> pool (-1 = unseeded, mirroring the shield pool).
    ///
    /// The invariant this pins: with NO magazine, a ship reads 0 capacity → the pool stays disabled → combat is
    /// byte-identical (every current ship, until the W3c base-mod magazine). W3b wires the per-salvo drain + silence;
    /// W3c adds the buildable base-mod magazine. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipAmmoTests
    {
        private const string RailgunShip = "default-ship-design-test-railgun"; // Lancer — 4 railguns, no magazine yet
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-ammo] " + m);

        [Test]
        [Description("ShipMagazineAtb (the ammo store) holds its kg capacity and clamps a negative to 0; the fleet ammo pool defaults to -1 (unseeded), mirroring the shield pool's lazy-seed sentinel.")]
        public void ShipMagazineAtb_AndAmmoPool_PinTheFoundation()
        {
            var mag = new ShipMagazineAtb(2500);
            Assert.That(mag.Capacity_kg, Is.EqualTo(2500), "the magazine holds its kg capacity");
            Assert.That(new ShipMagazineAtb(-50).Capacity_kg, Is.EqualTo(0), "a negative capacity clamps to 0 (never negative ammo)");
            Assert.That(((ShipMagazineAtb)mag.Clone()).Capacity_kg, Is.EqualTo(2500), "clone preserves the capacity");

            Assert.That(new FleetCombatStateDB().AmmoPool_kg, Is.EqualTo(-1),
                "the fleet ammo pool defaults to -1 (not yet seeded) — the resolver lazy-fills it to capacity at first salvo");
        }

        [Test]
        [Description("W3a additive/byte-identical: a real base-mod ship with NO magazine reads AmmoCapacity_kg == 0 (so its fleet ammo pool is disabled and the resolve is untouched), exactly as an unshielded ship reads a 0 shield pool.")]
        public void ARealShip_WithNoMagazine_ReadsZeroAmmoCapacity()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(RailgunShip), Is.True, "the Lancer railgun cruiser loads onto the faction");

            var ship = ShipFactory.CreateShip(designs[RailgunShip], s.Faction, s.StartingBody, "Lancer");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Lancer (no magazine): firepower={cv.Firepower:0}, ammoCapacity={cv.AmmoCapacity_kg:0} kg");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Lancer carries its railguns");
            Assert.That(cv.AmmoCapacity_kg, Is.EqualTo(0),
                "no magazine → 0 ammo capacity → the fleet ammo pool stays disabled → combat byte-identical until the W3c magazine");
        }
    }
}
