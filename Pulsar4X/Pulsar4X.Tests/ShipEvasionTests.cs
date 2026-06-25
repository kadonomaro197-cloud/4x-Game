using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth — ship EVASION (how hard a ship is to HIT), the input the dodge model rides on. Derived in
    /// <see cref="ShipCombatValueDB.CalculateEvasion"/> from a ship's size (small = hard to hit) and the
    /// acceleration it can pull (thrust ÷ mass = how fast it changes vector). This is "maneuverability," kept
    /// separate from Toughness (which is soaking what lands). Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipEvasionTests
    {
        // A bare entity carrying just the mass/size + thrust the evasion calc reads (mirrors the OrbitTests
        // construction pattern). ManagerID is passed as the thrust DB's fuel-type string, same as OrbitTests.
        private static Entity MakeBody(EntityManager mgr, double volume_m3, double massDry, double thrustN)
        {
            var blobs = new BaseDataBlob[2];
            blobs[0] = new MassVolumeDB() { MassDry = massDry, Volume_m3 = volume_m3 };
            blobs[1] = new NewtonThrustAbilityDB(mgr.ManagerID) { ThrustInNewtons = thrustN };
            var e = Entity.Create();
            mgr.AddEntity(e, blobs);
            return e;
        }

        [Test]
        [Description("A small, light, high-thrust fighter dodges far better than a huge, heavy, sluggish battleship.")]
        public void Fighter_DodgesBetterThan_Battleship()
        {
            var s = TestScenario.CreateWithColony();

            // fighter: tiny + light + strong engine => high acceleration => high evasion.
            var fighter = MakeBody(s.StartingSystem, volume_m3: 50, massDry: 2_000, thrustN: 80_000);
            // battleship: huge + heavy + modest engine => almost no acceleration => easy to hit.
            var battleship = MakeBody(s.StartingSystem, volume_m3: 50_000, massDry: 5_000_000, thrustN: 200_000);

            double fE = ShipCombatValueDB.Calculate(fighter).Evasion;
            double bE = ShipCombatValueDB.Calculate(battleship).Evasion;
            TestContext.Progress.WriteLine($"[evasion] fighter={fE:0.###} battleship={bE:0.###}");

            Assert.That(fE, Is.GreaterThan(bE), "a small nimble fighter must dodge better than a battleship");
            Assert.That(fE, Is.GreaterThan(0.3), "the fighter should have meaningful evasion");
            Assert.That(bE, Is.LessThan(0.1), "the battleship should be easy to hit");
            Assert.That(fE, Is.LessThan(1.0), "evasion is capped below 1 — nothing is untouchable");
        }

        [Test]
        [Description("A ship with no engine (no thrust) can't dodge at all — evasion is zero.")]
        public void NoEngine_CannotDodge()
        {
            var s = TestScenario.CreateWithColony();

            var blobs = new BaseDataBlob[1];
            blobs[0] = new MassVolumeDB() { MassDry = 2_000, Volume_m3 = 50 }; // small, but it can't maneuver
            var hulk = Entity.Create();
            s.StartingSystem.AddEntity(hulk, blobs);

            Assert.That(ShipCombatValueDB.Calculate(hulk).Evasion, Is.EqualTo(0),
                "no thrust means no vector change means no dodge");
        }
    }
}
