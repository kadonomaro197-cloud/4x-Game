namespace Pulsar4X.Weapons
{
    /// <summary>
    /// PHYSICAL ORDNANCE MAGAZINE — the pure two-tier ready-magazine math (Operation Blueprint-to-Steel, the
    /// "physical-supply" mechanic, Phase A, 2026-08-18).
    ///
    /// <para>Today a missile launcher fires straight from the ship's bulk <c>ordnance-storage</c> cargo hold
    /// (<see cref="MissileProcessor.LaunchMissile"/> checks the hold has ≥1 round and removes 1 on launch), gated only
    /// by an abstract per-launcher rate counter (<c>GenericFiringWeaponsDB.InternalMagQty</c>) that reloads out of
    /// NOTHING. There is no intermediate READY magazine — the naval "ready-service locker" that holds a few rounds at
    /// the mount, depletes as the launcher fires, and refills from the deep magazine (the bulk hold) over time.</para>
    ///
    /// <para>This class is the PURE, unit-testable core of that locker — no <c>Entity</c>, no cargo store, no
    /// processor — so it can be gauged in CI even though the LIVE firing path it will eventually wire into cannot be
    /// run headless. <b>Phase A = this math + its gauge, byte-identical (NOTHING calls it yet).</b> Phase B wires it
    /// into <c>GenericFiringWeaponsProcessor</c>'s reload loop (pull from the bulk hold instead of from nothing) and
    /// <see cref="MissileProcessor.LaunchMissile"/> (fire from the ready-mag, so the hold is touched only by reload),
    /// behind <see cref="EnableOrdnanceMagazine"/> — and is verified on the developer's live build, since CI can
    /// compile but not run the missile-firing path.</para>
    /// </summary>
    public static class OrdnanceMagazineTools
    {
        /// <summary>Phase-B activation flag (declared here; the firing path reads it once wired). Default OFF → a
        /// launcher fires straight from the bulk hold exactly as today (byte-identical); ON → the two-tier
        /// ready-magazine below. The developer flips it on and verifies live, because CI can't run the firing path.</summary>
        public static bool EnableOrdnanceMagazine = false;

        /// <summary>Can a launcher whose ready-magazine holds <paramref name="readyRounds"/> fire a shot that needs
        /// <paramref name="roundsPerShot"/>? Pure. A ready-mag that can't cover a shot HOLDS FIRE even when the bulk
        /// hold is full — that is the whole point: you fire from the locker, not the deep magazine, so a drained locker
        /// silences the mount until it reloads.</summary>
        public static bool CanFire(long readyRounds, long roundsPerShot)
            => roundsPerShot > 0 && readyRounds >= roundsPerShot;

        /// <summary>Deplete the ready-magazine by one shot of <paramref name="roundsPerShot"/> rounds. Pure — returns
        /// the new ready count, floored at 0 (a negative <paramref name="roundsPerShot"/> is treated as 0, never a
        /// refill).</summary>
        public static long Fire(long readyRounds, long roundsPerShot)
        {
            long spent = roundsPerShot < 0 ? 0 : roundsPerShot;
            long r = readyRounds - spent;
            return r < 0 ? 0 : r;
        }

        /// <summary>Refill the ready-magazine from the bulk hold over one tick. Pulls up to
        /// <paramref name="reloadRate"/> × <paramref name="dtSeconds"/> rounds (floored to whole rounds) from the
        /// <paramref name="holdAvailable"/> bulk store, capped by the ready-magazine's free space
        /// (<paramref name="readyCapacity"/> − current). Returns <c>(newReady, pulledFromHold)</c> — the caller removes
        /// exactly <c>pulledFromHold</c> from the bulk hold, so no round is created or lost (conservation). Pure. An
        /// EMPTY hold pulls 0 (the locker runs dry once the deep magazine is spent — the grave rung); a FULL ready-mag
        /// pulls 0 (no free space). Negative inputs are clamped to 0.</summary>
        public static (long newReady, long pulledFromHold) Reload(
            long readyRounds, long readyCapacity, long holdAvailable, double reloadRate, double dtSeconds)
        {
            if (readyRounds < 0) readyRounds = 0;
            if (readyCapacity < 0) readyCapacity = 0;
            if (holdAvailable < 0) holdAvailable = 0;

            long freeSpace = readyCapacity - readyRounds;
            if (freeSpace <= 0) return (readyRounds, 0);   // ready-mag full → nothing to pull

            double want = reloadRate * dtSeconds;
            long pull = want <= 0 ? 0 : (long)want;        // floor to whole rounds
            if (pull > freeSpace) pull = freeSpace;        // don't overfill the locker
            if (pull > holdAvailable) pull = holdAvailable; // can't pull more than the deep magazine holds
            if (pull < 0) pull = 0;

            return (readyRounds + pull, pull);
        }

        /// <summary>
        /// The CHARGE-BASED reload (Phase B, the developer's ruling 2026-08-18: "REUSE the charge-counter with a
        /// conversion"). Instead of the rounds-based <see cref="Reload"/> above — whose <c>reloadRate × dt</c> FLOORS to
        /// 0 for a base-mod launcher that reloads 1 charge-point/sec (= 1 round per 120 s), silencing it forever — this
        /// reuses the launcher's existing abstract charge counter (<c>GenericFiringWeaponsDB.InternalMagQty</c>) as the
        /// ready-locker and converts charge↔whole rounds: the SAME charge accumulation as today drives timing, but a
        /// round only COMPLETES into the locker (crosses an <paramref name="amountPerShot"/> boundary) when a physical
        /// round is pulled from the bulk <c>ordnance-storage</c> hold; with an empty hold the charge is HELD just below
        /// the next boundary (never completing an unbacked round) until ammo arrives — the grave rung, no floor-trap.
        /// PURE (charge units in, charge units + whole rounds-pulled out) so the conversion is CI-gauged even though the
        /// live firing path it wires into cannot be run headless.
        /// </summary>
        /// <returns><c>(newCharge, roundsPulledFromHold)</c> — the caller sets <c>InternalMagQty = newCharge</c> and
        /// removes EXACTLY <c>roundsPulledFromHold</c> whole rounds from the bulk hold (conservation: a round leaves the
        /// deep magazine exactly when it enters the ready-locker).</returns>
        public static (int newCharge, int roundsPulledFromHold) ReloadCharge(
            int currentCharge, int reloadPerTick, int magSize, int amountPerShot, long holdAvailable)
        {
            if (amountPerShot <= 0) return (currentCharge, 0);            // no per-shot cost → no round conversion possible
            if (currentCharge < 0) currentCharge = 0;
            if (holdAvailable < 0) holdAvailable = 0;

            int wouldBe = currentCharge + (reloadPerTick < 0 ? 0 : reloadPerTick);
            if (wouldBe > magSize) wouldBe = magSize;
            if (wouldBe <= currentCharge) return (currentCharge, 0);      // already full / no gain this tick

            int roundsBefore = currentCharge / amountPerShot;            // whole rounds already ready in the locker
            int roundsAfter  = wouldBe / amountPerShot;
            int newRounds = roundsAfter - roundsBefore;                  // whole rounds this reload would newly COMPLETE
            if (newRounds <= 0) return (wouldBe, 0);                     // only partial progress toward the next round → free, no ammo needed

            int pulled = (int)(holdAvailable < newRounds ? holdAvailable : newRounds);
            if (pulled >= newRounds) return (wouldBe, pulled);           // the hold backs every new round → charge as the reload rate allowed

            // The hold couldn't back all the new rounds: complete only (roundsBefore + pulled), and hold the charge just
            // below the next (unbacked) round boundary so no round completes without ammo — and no reload progress is lost.
            int cap = (roundsBefore + pulled) * amountPerShot + (amountPerShot - 1);
            if (cap > wouldBe) cap = wouldBe;
            return (cap, pulled);
        }
    }
}
