using NUnit.Framework;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// PHYSICAL ORDNANCE MAGAZINE — Phase A gauge (Operation Blueprint-to-Steel, "physical-supply", 2026-08-18).
    /// The pure two-tier ready-magazine math (<see cref="OrdnanceMagazineTools"/>): a launcher fires from a small READY
    /// locker that depletes as it fires and refills from the bulk ordnance hold over time. These pin the locker rules
    /// so Phase B can wire them into the live missile-firing path (which CI can compile but not run) with the behaviour
    /// already proven. Byte-identical: nothing calls this class yet, and the activation flag defaults OFF.
    /// </summary>
    [TestFixture]
    public class OrdnanceMagazineTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ordnance-mag] " + m);

        [Test]
        [Description("The ready-magazine gates firing, NOT the bulk hold: a locker that can't cover a shot holds fire "
                     + "even with rounds still in the deep magazine — you fire from the locker.")]
        public void CanFire_ReadyMagGatesTheShot()
        {
            Assert.That(OrdnanceMagazineTools.CanFire(5, 1), Is.True, "5 ready covers a 1-round shot");
            Assert.That(OrdnanceMagazineTools.CanFire(1, 1), Is.True, "exactly enough");
            Assert.That(OrdnanceMagazineTools.CanFire(0, 1), Is.False,
                "an empty locker holds fire — even though the bulk hold (not consulted here) may be full");
            Assert.That(OrdnanceMagazineTools.CanFire(3, 5), Is.False, "3 ready can't cover a 5-round shot");
            Assert.That(OrdnanceMagazineTools.CanFire(5, 0), Is.False, "a zero-round shot is not a valid fire");
        }

        [Test]
        [Description("Firing depletes the ready-magazine and floors at 0; a negative shot never refills.")]
        public void Fire_DepletesAndFloors()
        {
            Assert.That(OrdnanceMagazineTools.Fire(5, 1), Is.EqualTo(4));
            Assert.That(OrdnanceMagazineTools.Fire(1, 1), Is.EqualTo(0));
            Assert.That(OrdnanceMagazineTools.Fire(0, 1), Is.EqualTo(0), "already empty → stays 0");
            Assert.That(OrdnanceMagazineTools.Fire(2, 5), Is.EqualTo(0), "over-fire floors at 0, never negative");
            Assert.That(OrdnanceMagazineTools.Fire(3, -1), Is.EqualTo(3), "a negative shot spends nothing (no refill)");
        }

        [Test]
        [Description("Reload pulls from the bulk hold up to the reload rate, capped by the locker's free space and by "
                     + "what the hold actually holds; an EMPTY hold pulls 0 (the grave rung), a FULL locker pulls 0, "
                     + "and every pull conserves rounds (newReady − before == pulledFromHold).")]
        public void Reload_PullsFromHold_CappedAndConserved()
        {
            // Plain pull: rate 2/s over 1 s from a deep hold into an empty locker.
            var a = OrdnanceMagazineTools.Reload(0, 10, 100, 2, 1);
            Assert.That(a.newReady, Is.EqualTo(2L), "pulls 2 rounds into the locker");
            Assert.That(a.pulledFromHold, Is.EqualTo(2L), "and draws 2 from the hold");

            // Capped by the locker's free space (8/10 → only 2 free).
            var b = OrdnanceMagazineTools.Reload(8, 10, 100, 5, 1);
            Assert.That(b.newReady, Is.EqualTo(10L), "reload tops the locker off");
            Assert.That(b.pulledFromHold, Is.EqualTo(2L), "pulling only the 2 that fit");

            // Capped by the bulk hold (only 3 rounds left in the deep magazine).
            var c = OrdnanceMagazineTools.Reload(0, 10, 3, 5, 1);
            Assert.That(c.newReady, Is.EqualTo(3L));
            Assert.That(c.pulledFromHold, Is.EqualTo(3L), "can't pull more than the hold has");

            // GRAVE RUNG: empty hold → the locker can't refill.
            var d = OrdnanceMagazineTools.Reload(0, 10, 0, 5, 1);
            Assert.That(d.newReady, Is.EqualTo(0L));
            Assert.That(d.pulledFromHold, Is.EqualTo(0L), "empty deep magazine → the locker runs dry");

            // Full locker → nothing to pull.
            var e = OrdnanceMagazineTools.Reload(10, 10, 100, 5, 1);
            Assert.That(e.newReady, Is.EqualTo(10L));
            Assert.That(e.pulledFromHold, Is.EqualTo(0L), "a full locker pulls nothing");

            // Conservation across all cases: what the locker gains equals what the hold gives up.
            foreach (var (before, cap, hold, rate) in new[] { (0L, 10L, 100L, 2.0), (8L, 10L, 100L, 5.0),
                                                              (0L, 10L, 3L, 5.0), (0L, 10L, 0L, 5.0), (10L, 10L, 100L, 5.0) })
            {
                var (newReady, pulled) = OrdnanceMagazineTools.Reload(before, cap, hold, rate, 1);
                Assert.That(newReady - before, Is.EqualTo(pulled),
                    $"conservation: locker gain must equal hold draw (before={before} cap={cap} hold={hold} rate={rate})");
            }
            Log("two-tier reload: pull=rate, capped by free space + hold, empty hold → 0, conserved");
        }

        [Test]
        [Description("Phase B — the CHARGE-BASED reload (ReloadCharge) reuses the launcher's abstract charge counter as "
                     + "the ready-locker, converting charge↔whole rounds and pulling from the bulk hold only when a round "
                     + "COMPLETES. Pins: (a) partial progress toward a round pulls nothing; (b) crossing a round boundary "
                     + "with ammo pulls exactly one; (c) an EMPTY hold holds the charge just below the boundary (no unbacked "
                     + "round, no progress lost); (d) the base-mod floor-trap case (reload 1 / amountPerShot 120) still "
                     + "completes a round over 120 ticks instead of flooring to 0; (e) conservation + a full mag pulls 0.")]
        public void ReloadCharge_ConvertsChargeToRounds_PullsFromHold_NoFloorTrap()
        {
            // (a) partial progress — well short of the next round boundary → no pull, charge just accrues.
            var (c1, p1) = OrdnanceMagazineTools.ReloadCharge(currentCharge: 0, reloadPerTick: 10, magSize: 1000, amountPerShot: 100, holdAvailable: 5);
            Assert.That(c1, Is.EqualTo(10), "partial charge accrues");
            Assert.That(p1, Is.EqualTo(0), "no whole round completed → nothing pulled from the hold");

            // (b) crossing a round boundary with ammo → completes one round, pulls exactly one.
            var (c2, p2) = OrdnanceMagazineTools.ReloadCharge(currentCharge: 95, reloadPerTick: 10, magSize: 1000, amountPerShot: 100, holdAvailable: 5);
            Assert.That(c2, Is.EqualTo(105), "charge crosses the 100 boundary");
            Assert.That(p2, Is.EqualTo(1), "one whole round completed → one pulled");

            // (c) crossing a boundary with an EMPTY hold → held just below the boundary, no round completes, no pull.
            var (c3, p3) = OrdnanceMagazineTools.ReloadCharge(currentCharge: 95, reloadPerTick: 10, magSize: 1000, amountPerShot: 100, holdAvailable: 0);
            Assert.That(p3, Is.EqualTo(0), "empty hold → nothing pulled (grave rung)");
            Assert.That(c3 / 100, Is.EqualTo(0), "no whole round completed without ammo");
            Assert.That(c3, Is.GreaterThanOrEqualTo(95), "no reload progress lost (charge held just below the boundary)");

            // (d) the floor-trap case: a base-mod-style launcher (reload 1 charge/tick, 120 charge/round) must still
            //     complete a round over 120 ticks — the rounds-based Reload floored 1/120 rounds/sec to 0.
            int charge = 0; int pulls = 0;
            for (int t = 0; t < 120; t++)
            {
                var (nc, np) = OrdnanceMagazineTools.ReloadCharge(charge, reloadPerTick: 1, magSize: 120, amountPerShot: 120, holdAvailable: 3);
                charge = nc; pulls += np;
            }
            Assert.That(charge / 120, Is.EqualTo(1), "after 120 ticks a whole round is ready (no floor-trap)");
            Assert.That(pulls, Is.EqualTo(1), "exactly one round pulled from the hold for the one completed round (conserved)");

            // (e) full magazine → no pull.
            var (c5, p5) = OrdnanceMagazineTools.ReloadCharge(currentCharge: 1000, reloadPerTick: 10, magSize: 1000, amountPerShot: 100, holdAvailable: 5);
            Assert.That(c5, Is.EqualTo(1000), "full mag stays full");
            Assert.That(p5, Is.EqualTo(0), "full mag pulls nothing");
            Log("ReloadCharge: charge↔round conversion, hold-gated completion, no floor-trap, conserved");
        }

        [Test]
        [Description("Byte-identity: the ordnance-magazine activation flag defaults OFF, so the live firing path is "
                     + "untouched until the developer turns it on and verifies live (CI can't run the firing path).")]
        public void OrdnanceMagazine_DefaultsOff()
        {
            Assert.That(OrdnanceMagazineTools.EnableOrdnanceMagazine, Is.False,
                "the physical ordnance-magazine must default OFF so the live missile path is byte-identical");
        }
    }
}
