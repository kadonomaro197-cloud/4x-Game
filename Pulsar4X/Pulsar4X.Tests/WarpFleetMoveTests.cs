using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Orbits;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges the "fleet moves as ONE" speed cap: a fleet warp caps every ship to the SLOWEST unit's warp speed
    /// (via the new <c>speedOverride_m</c> on <see cref="WarpMath.GetInterceptPosition"/> threaded through
    /// <see cref="WarpMoveCommand.SpeedCap_m"/>), so the fleet arrives together instead of the fast ships racing
    /// ahead and scattering. This tests the core mechanism (a slower cap = a later arrival) directly on the math
    /// function — deterministic, no warp processor / clock advance needed (warp transit itself has no test harness).
    /// </summary>
    [TestFixture]
    public class WarpFleetMoveTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[warp-fleet] " + m);

        [Test]
        [Description("A slower speed cap yields a LATER warp arrival than the ship's own (faster) speed — the lever " +
                     "that makes a fleet move at the slowest unit's pace. Default (override 0) is byte-identical to " +
                     "the old per-ship behaviour, so single-ship warps and missiles are unaffected.")]
        public void WarpSpeedCap_SlowerCap_ArrivesLater()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();

            // A warp-capable hull (the Aegis carries an alcubierre drive → WarpAbilityDB on build).
            Assert.That(info.ShipDesigns.ContainsKey("default-ship-design-test-warship"), Is.True,
                "the base mod's warp-capable test warship must be unlocked");
            var ship = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Aegis");

            if (!ship.TryGetDataBlob<WarpAbilityDB>(out var warp) || warp.MaxSpeed <= 0)
            {
                Assert.Ignore("ship has no usable warp drive — can't gauge the cap");
                return;
            }
            Log($"ship MaxSpeed = {warp.MaxSpeed:N0} m/s");

            // A target body distinct from the ship's parent, with a position to warp to.
            var target = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.HasDataBlob<PositionDB>() && b.HasDataBlob<OrbitDB>());
            Assert.That(target, Is.Not.Null, "need a second body in the system to warp toward");
            Log($"target body = {target.GetDefaultName()}");

            var now = ship.StarSysDateTime;

            // Same ship, same target — once at the ship's own speed (override 0), once at HALF that (a slower
            // fleet-mate dictating the pace). The slower run must arrive strictly later.
            var fast = WarpMath.GetInterceptPosition(ship, target, now, new Pulsar4X.Orbital.Vector3(), 0);
            var capped = WarpMath.GetInterceptPosition(ship, target, now, new Pulsar4X.Orbital.Vector3(), warp.MaxSpeed * 0.5);

            Log($"arrival full-speed = {fast.etiDateTime:o}");
            Log($"arrival half-speed = {capped.etiDateTime:o}");

            Assert.That(capped.etiDateTime, Is.GreaterThan(fast.etiDateTime),
                "capping to half speed must make the ship arrive later — the fleet-moves-as-one lever");
        }
    }
}
