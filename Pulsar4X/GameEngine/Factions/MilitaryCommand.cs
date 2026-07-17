using System;
using Pulsar4X.Engine;   // Entity

namespace Pulsar4X.Factions
{
    /// <summary>
    /// MILITARY-COMMAND POSTURE — the ONE place that owns the "commit the force vs. keep a home reserve" call.
    ///
    /// Until now that call was FOUR hardcoded guards, each holding the same implicit "keep about half at home"
    /// default: the fleet reserve (<see cref="ConquerResolver.HasHomeReserve"/> / <c>ShouldStopMassing</c>) and the
    /// ground/garrison reserve (<c>GroundCombat.GroundReinforcement.GarrisonReserveFor</c> / <c>WouldStripReserve</c>).
    /// A bold, aggressive faction and a cautious, timid one drew the SAME line. This layer replaces that fixed line
    /// with a single reserve POSTURE the four guards all consult, so the line moves with the faction's character —
    /// exactly like a fleet or army commander who's told "hold a reserve" but decides HOW MUCH from their own nerve.
    ///
    /// Think of it as one dial on the wall of the war room. Turn it toward AGGRESSIVE and every guard keeps a SMALLER
    /// reserve (the faction commits more of what it has); turn it toward CAUTIOUS and every guard keeps a BIGGER one
    /// (the faction holds more back). The dial's default position — a NEUTRAL personality — leaves every threshold
    /// exactly where it was, so a default game plays byte-for-byte identically (<see cref="ScaleReserve"/> returns the
    /// unchanged number at a factor of 1.0).
    ///
    /// Today the dial is read from the faction's own <see cref="PersonalityDB"/> (its Aggression and Risk traits — the
    /// same two a real fleet/army commander's boldness would come from).
    ///
    /// ── SEAM (the single entry point for a future officer) ─────────────────────────────────────────────────────────
    /// <see cref="PostureFor(Entity)"/> is the one function that turns a faction into a posture, and it is deliberately
    /// the ONLY place the four guards get their posture from. A LATER slice will seat a real military-commander officer
    /// (an <c>AdministratorDB</c> in the delegate layer, see docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md /
    /// docs/ai/AI-COMMAND-AND-COMMUNICATION-DESIGN.md) who OWNS this decision from their own reserve/aggression stance.
    /// To wire that officer, override the posture HERE — check for a seated commander's stance first, fall back to the
    /// faction personality below. Nothing else has to change: the four guards already funnel through this method, so a
    /// seated commander automatically re-tunes every reserve at once. This slice does NOT build officer-seating; it only
    /// lays the clean seam the officer bolts onto. Pure/deterministic/defensive (never throws — safe from the hotloop).
    /// </summary>
    public static class MilitaryCommand
    {
        /// <summary>The middle of the 0..1 posture range — the neutral position that leaves every reserve threshold
        /// UNCHANGED (the byte-identity pin). A <see cref="PersonalityDB"/> whose Aggression + Risk are both
        /// <see cref="PersonalityDB.Neutral"/> (0.5), and a null/absent personality, both land here.</summary>
        public const double BaselinePosture = PersonalityDB.Neutral;   // 0.5

        /// <summary>How far the reserve threshold swings at the trait extremes — ±50% at posture 0.0 / 1.0 (FLAGGED
        /// tunable). A max-aggressive faction scales its reserve to ×0.5 (commit-more), a max-cautious one to ×1.5
        /// (hold-more), and neutral to ×1.0 (no change).</summary>
        public const double MaxReserveSwing = 0.5;

        /// <summary>Hard clamp on the returned multiplier (pure defence — with the traits clamped to 0..1 and the swing
        /// at 0.5 the natural range is already [0.5, 1.5], so these never bite unless the swing is widened later).</summary>
        public const double MinFactor = 0.25;
        public const double MaxFactor = 2.0;

        // ── The SEAM: faction → posture (the single entry point a future seated officer overrides) ──────────────────

        /// <summary>
        /// The faction's reserve POSTURE (0..1, <see cref="BaselinePosture"/> = today's baseline; HIGHER = commit-more /
        /// keep a smaller reserve, LOWER = hold-more / keep a bigger reserve). This is the SEAM: today it derives from
        /// the faction's <see cref="PersonalityDB"/>; a future seated military-commander officer will set it here
        /// instead (see the class note). Defensive: a null faction / a faction with no <see cref="PersonalityDB"/>
        /// returns the neutral <see cref="BaselinePosture"/>, so the guards stay byte-identical for the default player
        /// and every faction that has no authored personality.
        /// </summary>
        public static double PostureFor(Entity faction)
            => faction != null && faction.TryGetDataBlob<PersonalityDB>(out var personality)
                ? ReservePosture(personality)
                : BaselinePosture;

        /// <summary>
        /// The pure personality → posture core: blends the two traits a commander's boldness comes from —
        /// <see cref="PersonalityTrait.Aggression"/> (bias toward attack) and <see cref="PersonalityTrait.Risk"/> (risk
        /// appetite) — equally, mirroring the <c>NPCDecisionProcessor.RunWarPolicy</c> style (0.5·Aggression +
        /// 0.5·Ambition). A neutral (all-0.5) or null personality returns exactly <see cref="BaselinePosture"/>.
        /// </summary>
        public static double ReservePosture(PersonalityDB personality)
        {
            if (personality == null) return BaselinePosture;
            return 0.5 * personality.TraitOf(PersonalityTrait.Aggression)
                 + 0.5 * personality.TraitOf(PersonalityTrait.Risk);
        }

        // ── The MULTIPLIER the guards apply to their reserve threshold ─────────────────────────────────────────────

        /// <summary>The FLEET reserve multiplier for a faction — what <see cref="ConquerResolver.HasHomeReserve"/> /
        /// <c>ShouldStopMassing</c> scale <c>FleetCompositionDB.MinToDeploy</c> by. Exactly 1.0 for a neutral/absent
        /// personality (byte-identical). Routes through the <see cref="PostureFor(Entity)"/> seam.</summary>
        public static double FleetReserveFactor(Entity faction) => FactorFromPosture(PostureFor(faction));

        /// <summary>The GROUND/garrison reserve multiplier for a faction — what
        /// <c>GroundReinforcement.GarrisonReserveFor</c> scales the authored garrison reserve by. Exactly 1.0 for a
        /// neutral/absent personality (byte-identical). Routes through the <see cref="PostureFor(Entity)"/> seam.</summary>
        public static double GroundReserveFactor(Entity faction) => FactorFromPosture(PostureFor(faction));

        /// <summary>The FLEET reserve multiplier from a personality directly (the pure API — the gauge exercises this).
        /// Identical math to <see cref="FleetReserveFactor(Entity)"/>; kept separate from the ground factor so a future
        /// officer could tune fleet vs. ground postures independently without touching the guards.</summary>
        public static double FleetReserveFactor(PersonalityDB personality) => FactorFromPosture(ReservePosture(personality));

        /// <summary>The GROUND/garrison reserve multiplier from a personality directly (the pure API — the gauge
        /// exercises this). Identical math to <see cref="GroundReserveFactor(Entity)"/>.</summary>
        public static double GroundReserveFactor(PersonalityDB personality) => FactorFromPosture(ReservePosture(personality));

        // ── Applying the multiplier to a concrete integer threshold ────────────────────────────────────────────────

        /// <summary>
        /// Scale a reserve threshold (a whole number of fleets/ships/units) by a posture <paramref name="factor"/>,
        /// rounding to the nearest whole unit. THE BYTE-IDENTITY PIN: at <paramref name="factor"/> == 1.0 this returns
        /// the base UNCHANGED (a positive integer × 1.0 is exact in IEEE-754, and rounds back to itself), so a neutral
        /// posture leaves every guard's threshold exactly as it was. A positive base never scales below a 1-unit
        /// reserve (a garrisoned world / an organized fleet always keeps at least one). A non-positive base (nothing to
        /// keep) passes through unchanged.
        /// </summary>
        public static int ScaleReserve(int baseThreshold, double factor)
        {
            if (baseThreshold <= 0) return baseThreshold;
            int scaled = (int)Math.Round(baseThreshold * factor, MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : scaled;
        }

        /// <summary>The posture → multiplier map: neutral (0.5) → 1.0, max-aggressive (1.0) → 1 − <see cref="MaxReserveSwing"/>
        /// (commit-more), max-cautious (0.0) → 1 + <see cref="MaxReserveSwing"/> (hold-more). Strictly DECREASING in
        /// posture (monotonic) and clamped to <see cref="MinFactor"/>..<see cref="MaxFactor"/> (bounded).</summary>
        private static double FactorFromPosture(double posture)
        {
            double factor = 1.0 - (posture - BaselinePosture) * 2.0 * MaxReserveSwing;
            if (factor < MinFactor) return MinFactor;
            if (factor > MaxFactor) return MaxFactor;
            return factor;
        }
    }
}
