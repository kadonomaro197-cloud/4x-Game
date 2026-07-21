namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The sub-formation ROLE a ground unit plays in a closing fight — the ground twin of
    /// <see cref="Pulsar4X.Fleets.FleetRoleComposer"/>'s <c>FleetRole</c>. A unit's role EMERGES from its own stats
    /// (speed/evasion, reach, firepower), exactly as a ship's role emerges from its hull, so the same "fighters move
    /// differently than a line ship" behaviour falls out on the ground: a light fast unit LEADS, a long-reach unit
    /// STANDS OFF, a gun-line HOLDS at its range, a support unit STAYS BACK.
    /// </summary>
    public enum GroundRole
    {
        /// <summary>Fast/light — leads to contact and screens (the fighter/recon echo).</summary>
        Screen,
        /// <summary>The gun-line — closes to its firing range and holds (the battleship echo).</summary>
        Line,
        /// <summary>Long reach — kites to keep its standoff, whittling as the enemy closes (the artillery echo).</summary>
        Artillery,
        /// <summary>No offensive punch (engineers/medics/spotters) — kept out of the fight (the tender echo).</summary>
        Support,
    }

    /// <summary>
    /// Classifies a <see cref="GroundUnit"/> into a <see cref="GroundRole"/> from its stats, and decides how that role
    /// wants to maneuver relative to the nearest enemy — the ground twin of <c>Fleets.FleetRoleComposer</c>. Pure and
    /// deterministic (no entity/manager reads) so it is directly unit-testable and safe to call every tick.
    ///
    /// W-TRACK W3: <see cref="GroundForcesProcessor.ApplyEngagementManeuvers"/> reads these (behind the default-OFF
    /// <see cref="GroundForcesProcessor.EnableGroundRoleManeuver"/> flag) so each unit in an auto-maneuvering formation
    /// moves to ITS role's ideal range band instead of all units marching uniformly.
    /// </summary>
    public static class GroundRoleComposer
    {
        // ── Classification thresholds (FLAGGED — a balance pass tunes these) ──────────────────────────────────────────
        /// <summary>At/above this evasion a unit is nimble enough to LEAD as a <see cref="GroundRole.Screen"/> (a light
        /// scout/skirmisher). Mirrors the space <c>FleetRoleComposer.ScreenEvasionThreshold</c> idea.</summary>
        public const double ScreenEvasionThreshold = 0.5;
        /// <summary>At/above this hex reach a unit STANDS OFF as <see cref="GroundRole.Artillery"/> (the base-mod
        /// artillery/cannon default of 3 hexes — a long gun that whittles from range).</summary>
        public const int ArtilleryRangeThreshold = 3;

        /// <summary>A unit with no offensive punch is <see cref="GroundRole.Support"/> (kept out of the fight); a nimble
        /// unit LEADS as <see cref="GroundRole.Screen"/>; a long-reach unit STANDS OFF as <see cref="GroundRole.Artillery"/>;
        /// everything else is the gun-line, <see cref="GroundRole.Line"/>. Same precedence as the space classifier
        /// (support → screen → artillery → line).</summary>
        public static GroundRole ClassifyRole(GroundUnit unit)
        {
            if (unit == null || unit.Attack <= 0)          return GroundRole.Support;   // no punch — kept out of the fight
            if (unit.Evasion >= ScreenEvasionThreshold)    return GroundRole.Screen;    // fast/light — leads
            if (unit.Range >= ArtilleryRangeThreshold)     return GroundRole.Artillery; // long reach — stands off
            return GroundRole.Line;                                                      // ordinary unit — the gun-line
        }

        /// <summary>How a <paramref name="role"/> wants to maneuver given the hex distance <paramref name="best"/> to the
        /// nearest enemy, this unit's strike reach <paramref name="myRange"/>, and the enemy's reach
        /// <paramref name="enemyRange"/>: <c>false</c> = close the gap, <c>true</c> = open it (kite), <c>null</c> = hold
        /// and fire (already at its ideal band). This is the whole "each role moves differently" decision:
        /// <list type="bullet">
        /// <item><b>Screen</b> — lead to contact: close while any gap remains.</item>
        /// <item><b>Line</b> — close to its firing range, then HOLD (the anvil; it never backs off).</item>
        /// <item><b>Artillery</b> — maintain its (long) range: close if the enemy is beyond reach, KITE if the enemy
        /// gets inside it, hold at the edge (the standoff whittler).</item>
        /// <item><b>Support</b> — stay beyond the enemy's reach: back off if the enemy can hit it, else hold.</item>
        /// </list></summary>
        public static bool? RoleMoveAway(GroundRole role, int best, int myRange, int enemyRange)
        {
            switch (role)
            {
                case GroundRole.Screen:
                    return best > 0 ? (bool?)false : null;                 // push to contact
                case GroundRole.Artillery:
                    if (best > myRange) return false;                      // out of my reach → close into it
                    if (best < myRange) return true;                       // enemy inside my reach → kite to keep standoff
                    return null;                                           // at max range → hold and whittle
                case GroundRole.Support:
                    return best <= enemyRange ? (bool?)true : null;        // enemy can hit me → back off, else hold
                default: // Line
                    return best > myRange ? (bool?)false : null;           // close to firing range, then hold
            }
        }
    }
}
