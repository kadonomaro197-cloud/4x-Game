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

        [Test]
        [Description("The cap must reach the ACTUAL travel speed, not only the predicted ETA. WarpMovingDB stores the " +
                     "capped speed in WarpSpeed_mps; the warp processor reads THAT (not WarpAbilityDB.MaxSpeed) to set " +
                     "the transit velocity. This is the bug that let a fast ship race ahead even on a fleet move — the " +
                     "ETA was capped but the ship still flew at its own MaxSpeed. 0 cap = the ship's own speed.")]
        public void WarpMovingDB_StoresTheCappedSpeed_SoTheProcessorUsesIt()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var ship = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Aegis");

            if (!ship.TryGetDataBlob<WarpAbilityDB>(out var warp) || warp.MaxSpeed <= 0)
            {
                Assert.Ignore("ship has no usable warp drive — can't gauge the cap");
                return;
            }

            var target = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.HasDataBlob<PositionDB>() && b.HasDataBlob<OrbitDB>());
            Assert.That(target, Is.Not.Null, "need a second body in the system to warp toward");

            double cap = warp.MaxSpeed * 0.5;
            var capped = new WarpMovingDB(ship, target, new Pulsar4X.Orbital.Vector3(),
                default(Pulsar4X.Orbital.KeplerElements), cap);
            Log($"capped WarpSpeed_mps = {capped.WarpSpeed_mps:N0} (cap {cap:N0})");
            Assert.That(capped.WarpSpeed_mps, Is.EqualTo(cap).Within(1e-6),
                "a fleet move's cap must be stored as the actual travel speed the processor reads");

            var uncapped = new WarpMovingDB(ship, target, new Pulsar4X.Orbital.Vector3(),
                default(Pulsar4X.Orbital.KeplerElements), 0);
            Assert.That(uncapped.WarpSpeed_mps, Is.EqualTo(warp.MaxSpeed).Within(1e-6),
                "no cap (0) must fall back to the ship's own MaxSpeed — single-ship warps unchanged");
        }

        [Test]
        [Description("Regression (live, 2026-06-28): issuing a warp to a ship that is ALREADY mid-warp must NOT crash. " +
                     "A ship in warp has a DETACHED position (null parent), so PositionDB.Root walks up and resolves " +
                     "to the ship ITSELF. The warp-start reparent step used to do SetParent(Root) = SetParent(self), " +
                     "which throws 'Cannot set the parent entity equal to self' — and because the order runs inside " +
                     "the Fleet window's Display(), that throw corrupted the whole ImGui frame (the developer's 'moved " +
                     "two fleets at once and the UI broke'). The guard now skips the reparent when Root == self.")]
        public void WarpStart_OnAnAlreadyDetachedShip_DoesNotThrowSelfParent()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var ship = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Aegis");
            if (!ship.TryGetDataBlob<WarpAbilityDB>(out var warp) || warp.MaxSpeed <= 0)
            {
                Assert.Ignore("ship has no usable warp drive — can't gauge the reparent guard");
                return;
            }

            var target = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.HasDataBlob<PositionDB>() && b.HasDataBlob<OrbitDB>());
            Assert.That(target, Is.Not.Null, "need a second body in the system to warp toward");

            // Give the ship a real in-warp state, then DETACH its position to reproduce the mid-warp condition
            // (null parent → Root resolves to the ship itself) that a SECOND warp order hit.
            ship.SetDataBlob(new WarpMovingDB(ship, target, new Pulsar4X.Orbital.Vector3(),
                default(Pulsar4X.Orbital.KeplerElements), 0));
            var pos = ship.GetDataBlob<PositionDB>();
            pos.SetParent(null);
            Assert.That(pos.Root.Id, Is.EqualTo(ship.Id),
                "precondition: a detached (mid-warp) ship is its own position root — the trap the guard must dodge");

            Assert.DoesNotThrow(() => WarpMoveProcessor.StartNonNewtTranslation(ship),
                "warp-start must SKIP the reparent when the ship is already its own root, not call SetParent(self)");
        }

        [Test]
        [Description("REGRESSION (found via a committed game_logs/ [FATAL], 2026-07-04): ordering a FLEET to a body " +
                     "where a member has a WarpAbilityDB with MaxSpeed 0 drove distance/speed → ∞ in the intercept " +
                     "math, and `atDateTime + TimeSpan.FromSeconds(∞)` threw OverflowException on the BACKGROUND sim " +
                     "thread (an unobservable [FATAL] that can kill the clock). WarpMath must NOT overflow on a " +
                     "non-positive speed — it no-ops (returns the mover's position/time) so the caller is safe.")]
        public void GetInterceptPosition_ZeroSpeed_DoesNotOverflow()
        {
            var s = TestScenario.CreateWithColony();
            var target = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.HasDataBlob<OrbitDB>());
            Assert.That(target, Is.Not.Null, "need a body with an orbit as the intercept target");
            var targetOrbit = target.GetDataBlob<OrbitDB>();
            var now = s.Game.TimePulse.GameGlobalDateTime;
            var moverPos = new Pulsar4X.Orbital.Vector3(1e9, 0, 0);

            Assert.DoesNotThrow(() =>
            {
                var (pos, eti) = WarpMath.GetInterceptPosition_m(moverPos, 0.0, targetOrbit, now);
                Assert.That(pos, Is.EqualTo(moverPos), "a zero-speed intercept is a NO-OP: the mover stays put");
                Assert.That(eti, Is.EqualTo(now), "and no transit time elapses");
            }, "a 0-speed warp intercept must not overflow TimeSpan");

            // A negative / NaN speed must be equally safe.
            Assert.DoesNotThrow(() => WarpMath.GetInterceptPosition_m(moverPos, -5.0, targetOrbit, now));
            Assert.DoesNotThrow(() => WarpMath.GetInterceptPosition_m(moverPos, double.NaN, targetOrbit, now));
            Log("zero/negative/NaN warp speed → no-op, no TimeSpan overflow");
        }
    }
}
