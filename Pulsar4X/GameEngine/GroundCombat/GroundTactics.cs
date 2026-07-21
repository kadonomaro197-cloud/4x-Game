using System;
using Pulsar4X.Factions;   // CombatRisk (READ-ONLY cross-lane reference — the SAME odds curve the fleet AI uses)

namespace Pulsar4X.GroundCombat
{
    /// <summary>The abstract STRATEGIC intent the brain decides for a battalion — translated into a real region move by
    /// the wire (<c>GroundTacticalBrain</c>). Enums serialize by int — APPEND, never reorder (<see cref="GroundFormation.TacticalIntent"/>).</summary>
    public enum GroundIntent : byte
    {
        Hold,      // stand where you are (dig in, or probe with the range advantage) — no strategic move
        Advance,   // push toward the nearest adjacent enemy-held / un-held region (take ground)
        PullBack,  // give ground deliberately toward a friendly region (a measured withdrawal)
        Retreat,   // flee toward the nearest friendly region / beachhead (a fighting withdrawal under heavy loss)
    }

    /// <summary>The brain's decision for ONE battalion — the OUTPUT of <see cref="GroundTactics.DecidePosture"/>.
    /// <see cref="StanceFamily"/> ("Offensive"/"Defensive"/"Balanced") is looked up in the moddable stance catalog and
    /// applied via <c>GroundFormationDoctrine.TrySetStance</c>; <see cref="Roe"/> via <c>SetEngagementStance</c>;
    /// <see cref="Intent"/> via the order queue. <see cref="Reason"/> is the plain-English explain (the AI-tape rule).</summary>
    public struct GroundPosture
    {
        public string StanceFamily;
        public GroundEngagementStance Roe;
        public GroundIntent Intent;
        /// <summary>The region to move to for Advance/PullBack/Retreat (-1 for Hold / no move).</summary>
        public int MoveTargetRegion;
        /// <summary>Break the stance-switch cooldown for THIS decision — a survival shift (dig-in / retreat under heavy
        /// pressure) must never be time-locked (the design's "no 180-day lock without a release"). Normal offensive↔
        /// balanced switching still respects the cooldown (that IS the hysteresis).</summary>
        public bool BreakGlass;
        public string Reason;
    }

    /// <summary>Everything <see cref="GroundTactics.DecidePosture"/> reads — every field an existing or named read the
    /// wire (<c>GroundTacticalBrain.BuildContext</c>) fills from live state. A plain value struct so the decision is a
    /// PURE function of its inputs (deterministic, no entity graph, unit-testable with hand-built numbers).</summary>
    public struct GroundTacticsContext
    {
        /// <summary>This battalion's firepower (Σ living-member Attack — <c>GroundFormationTools.FormationStrength</c>).</summary>
        public double OwnStrength;
        /// <summary>DETECTED enemy firepower in this region + adjacent (fog-honest — <see cref="GroundThreat.DetectedEnemyStrength(Pulsar4X.Engine.Entity,int,int)"/>). An undetected enemy is 0.</summary>
        public double EnemyStrength;
        /// <summary>Faction Risk trait 0..1 (bold=1 fights at parity / cautious=0 wants 2×) — read through the SAME
        /// <see cref="CombatRisk.RequiredStrengthRatio"/> curve the fleet AI uses (personality consistency across domains).</summary>
        public double RiskTrait;
        /// <summary>Faction Aggression trait 0..1 (gates a homeland defender's optional counterattack sally).</summary>
        public double AggressionTrait;
        /// <summary>Is this body the battalion's own homeland (it hosts a colony of this faction)? Biases defensive.</summary>
        public bool IsHomelandDefender;
        /// <summary>Do own warships hold the orbit above (bombard-then-advance support)? Biases offensive.</summary>
        public bool HasOrbitalSupport;
        /// <summary>Fortification multiplier of the battalion's region (&gt;1 = a fortified line — <c>GroundFortification.DefenseMult</c>).</summary>
        public double FortificationMult;
        /// <summary>Is the region defensible terrain (cover/rough — <c>GroundTerrain.CoverDefenseMult</c>&gt;1)?</summary>
        public bool DefensibleTerrain;
        /// <summary>Does the battalion field any ammo-fed weapons at all (else the dry-ammo rule doesn't apply)?</summary>
        public bool HasAmmoWeapons;
        /// <summary>Worst-case ammo fraction across the battalion's ammo units (0..1; 1 if it fields none). Dry ⇒ never Offensive.</summary>
        public double AmmoFraction;
        /// <summary>Does the faction still hold its home garrison RESERVE on this body (a healthy rear — <c>GroundReinforcement</c>)?
        /// Gates a homeland defender's counterattack (don't sally if it strips the reserve).</summary>
        public bool ReserveIntact;
        /// <summary>Is there a friendly region / beachhead to fall back to (the line of retreat exists)?</summary>
        public bool HasFallback;
        /// <summary>The fallback region for Retreat/PullBack (-1 if none).</summary>
        public int FallbackRegion;
        /// <summary>Is there an adjacent enemy-held / un-held region to advance INTO (and is the current region clear of
        /// live enemies)? False while a live enemy shares the battalion's region — you clear before you advance.</summary>
        public bool HasAdvanceTarget;
        /// <summary>The region to advance into (-1 if none).</summary>
        public int AdvanceRegion;
        /// <summary>Is the battalion blind (an adjacent region is un-scouted)? Biases cautious (treat unknown as risk).</summary>
        public bool Blind;
    }

    /// <summary>
    /// THE GROUND TACTICAL BRAIN — the pure, deterministic decision model (Operation Earthfall G2.2b). Given a
    /// battalion's fog-honest picture (<see cref="GroundTacticsContext"/>), it returns the posture a competent officer
    /// of the deck would set: WHEN to be offensive vs defensive, WHEN to dig in, WHEN to retreat — the answer to the
    /// developer's question "is the AI smart enough to know when to be defensive vs offensive." No randomness, no wall
    /// clock, no entity graph → fast-forward == watch, replay-stable, CI-testable in isolation.
    ///
    /// It MIRRORS the space AI it doesn't reinvent: the odds bar is the fleet's own
    /// <see cref="CombatRisk.RequiredStrengthRatio"/> curve (bold commits at parity, cautious demands 2×), read through
    /// the faction's <c>PersonalityDB</c> Risk trait — so the UMF is recognisably the UMF on the ground as in space.
    /// Design + acceptance gauges: <c>docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md</c> §2/§4.
    /// </summary>
    public static class GroundTactics
    {
        // ── FLAGGED balance values — the developer sets the numbers (GROUND-TACTICAL-AI-DESIGN.md §5). ──
        /// <summary>Ammo fraction at/below which a battalion is DRY → it will never choose Offensive (a silent gun line
        /// doesn't charge). FLAGGED balance value.</summary>
        public const double DryAmmoThreshold = 0.05;   // FLAGGED balance value
        /// <summary>Loss ratio that triggers RETREAT: enemy ≥ own × this ("losing 1:4"). FLAGGED balance value.</summary>
        public const double RetreatLossRatio = 4.0;    // FLAGGED balance value
        /// <summary>own/enemy odds at/above which the fight is "roughly even" (NOT outnumbered). Below it an attacker
        /// digs in rather than charging; the parity band feeds the Balanced+StandOff probe. FLAGGED balance value.</summary>
        public const double ParityFloor = 0.8;         // FLAGGED balance value
        /// <summary>Orbital-support boldness: multiplies the required odds ratio DOWN (easier to commit) when own
        /// warships hold the orbit — the soften-then-advance bias. FLAGGED balance value.</summary>
        public const double OrbitalRequiredFactor = 0.85;   // FLAGGED balance value
        /// <summary>Blind caution: multiplies the required odds ratio UP (harder to commit) when a neighbour is
        /// un-scouted — what you can't see can hurt you. FLAGGED balance value.</summary>
        public const double BlindCautionFactor = 1.5;  // FLAGGED balance value
        /// <summary>Minimum Aggression trait for a strong homeland defender to SALLY (counterattack) instead of holding
        /// its prepared line. FLAGGED balance value.</summary>
        public const double CounterattackAggression = 0.7;  // FLAGGED balance value

        // Stance family labels — must match the base-mod groundStances.json "Family" values.
        public const string Offensive = "Offensive";
        public const string Defensive = "Defensive";
        public const string Balanced  = "Balanced";

        /// <summary>Decide a battalion's posture from its fog-honest context. Ordered most-severe-first: LOSING HARD
        /// (retreat or last stand) → HOMELAND DEFENDER (bias defensive, optional sally) → ATTACKER (press the edge your
        /// personality demands, else dig in / probe). Every branch returns a plain-English <see cref="GroundPosture.Reason"/>.</summary>
        public static GroundPosture DecidePosture(GroundTacticsContext ctx)
        {
            bool dry = ctx.HasAmmoWeapons && ctx.AmmoFraction <= DryAmmoThreshold;
            double own = ctx.OwnStrength;
            double enemy = ctx.EnemyStrength;
            bool haveThreat = enemy > 0.0;

            // The odds bar this faction demands (the fleet AI's own curve), nudged by orbital support (down) + blindness (up).
            double required = CombatRisk.RequiredStrengthRatio(ctx.RiskTrait);
            if (ctx.HasOrbitalSupport) required *= OrbitalRequiredFactor;
            if (ctx.Blind) required *= BlindCautionFactor;

            double oddsRatio = haveThreat ? own / enemy : double.PositiveInfinity;
            bool losingHard = haveThreat && own * RetreatLossRatio <= enemy;      // enemy ≥ own × 4
            bool outnumbered = haveThreat && oddsRatio < ParityFloor;            // clearly fewer guns than the foe
            bool canCommit = haveThreat ? (own >= enemy * required)             // I have the edge my personality demands
                                        : !ctx.Blind;                           // no threat + scouted = free to press; blind = don't

            // ── 1) LOSING HARD (both roles, most severe) — fighting withdrawal, or a cornered last stand. ──
            if (losingHard)
            {
                if (ctx.HasFallback)
                    return new GroundPosture
                    {
                        StanceFamily = Defensive, Roe = GroundEngagementStance.StandOff,
                        Intent = GroundIntent.Retreat, MoveTargetRegion = ctx.FallbackRegion, BreakGlass = true,
                        Reason = $"losing badly ({own:0} vs {enemy:0}) — fighting withdrawal to region {ctx.FallbackRegion + 1}",
                    };
                return new GroundPosture
                {
                    StanceFamily = Defensive, Roe = GroundEngagementStance.HoldGround,
                    Intent = GroundIntent.Hold, MoveTargetRegion = -1, BreakGlass = true,
                    Reason = $"cornered and outmatched ({own:0} vs {enemy:0}) — dig in, no line of retreat",
                };
            }

            // ── 2) HOMELAND DEFENDER — bias defensive (home turf + prepared positions). ──
            if (ctx.IsHomelandDefender)
            {
                bool fortified = ctx.FortificationMult > 1.0 || ctx.DefensibleTerrain;

                // A strong, aggressive, well-reserved defender with somewhere to push MAY sally to clear the invaders.
                if (haveThreat && !dry && canCommit && !outnumbered && !fortified
                    && ctx.AggressionTrait >= CounterattackAggression && ctx.ReserveIntact && ctx.HasAdvanceTarget)
                    return new GroundPosture
                    {
                        StanceFamily = Offensive, Roe = GroundEngagementStance.CloseToEngage,
                        Intent = GroundIntent.Advance, MoveTargetRegion = ctx.AdvanceRegion, BreakGlass = false,
                        Reason = $"home defense but strong enough to counterattack ({own:0} vs {enemy:0}) — pushing the invaders",
                    };

                if (fortified || outnumbered)
                    return new GroundPosture
                    {
                        StanceFamily = Defensive, Roe = GroundEngagementStance.HoldGround,
                        Intent = GroundIntent.Hold, MoveTargetRegion = -1, BreakGlass = outnumbered,
                        Reason = fortified
                            ? "defending fortified homeland — hold the line"
                            : $"outnumbered on the homeland ({own:0} vs {enemy:0}) — dig in",
                    };

                // Home turf at (or better than) parity but no decisive edge → hold prepared positions and probe.
                return new GroundPosture
                {
                    StanceFamily = Defensive, Roe = GroundEngagementStance.StandOff,
                    Intent = GroundIntent.Hold, MoveTargetRegion = -1, BreakGlass = false,
                    Reason = "home defense at parity — holding prepared positions",
                };
            }

            // ── 3) ATTACKER (on hostile / neutral ground). ──
            if (outnumbered)
                return new GroundPosture
                {
                    StanceFamily = Defensive, Roe = GroundEngagementStance.HoldGround,
                    Intent = GroundIntent.Hold, MoveTargetRegion = -1, BreakGlass = true,
                    Reason = $"outnumbered on hostile ground ({own:0} vs {enemy:0}) — dig in",
                };

            if (canCommit && !dry)
            {
                bool advance = ctx.HasAdvanceTarget;
                return new GroundPosture
                {
                    StanceFamily = Offensive, Roe = GroundEngagementStance.CloseToEngage,
                    Intent = advance ? GroundIntent.Advance : GroundIntent.Hold,
                    MoveTargetRegion = advance ? ctx.AdvanceRegion : -1, BreakGlass = false,
                    Reason = haveThreat
                        ? $"odds favour the assault ({own:0} vs {enemy:0}, need x{required:0.0}) — pressing"
                        : "no enemy in reach — advancing to take ground",
                };
            }

            // Below the required edge but not outnumbered (or dry, or blind) → probe at parity: Balanced + StandOff, and
            // advance only cautiously (a scouted target, not dry). This is where the personality curve BITES — a bold
            // faction reached the Offensive branch above at odds a cautious one refuses and lands here instead.
            bool cautiousAdvance = ctx.HasAdvanceTarget && !ctx.Blind && !dry;
            return new GroundPosture
            {
                StanceFamily = Balanced, Roe = GroundEngagementStance.StandOff,
                Intent = cautiousAdvance ? GroundIntent.Advance : GroundIntent.Hold,
                MoveTargetRegion = cautiousAdvance ? ctx.AdvanceRegion : -1, BreakGlass = false,
                Reason = dry ? "ammo dry — probing, not assaulting"
                       : ctx.Blind ? "blind on the enemy — probing cautiously"
                       : $"odds not yet decisive ({own:0} vs {enemy:0}, need x{required:0.0}) — probing with the range advantage",
            };
        }
    }
}
