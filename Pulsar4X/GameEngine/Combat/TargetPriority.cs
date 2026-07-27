namespace Pulsar4X.Combat
{
    /// <summary>
    /// WHO a force shoots first — the target-selection half of a doctrine (the developer's ruling: the player picks
    /// targets, "but also dependent on the DOCTRINE of the battalion").
    ///
    /// Why this exists: today BOTH resolvers spread fire across every reachable enemy weighted by their CURRENT
    /// health, which means damage preferentially lands on the HEALTHIEST target and a cripple is never finished —
    /// the opposite of how anyone actually fights. This enum is the dial that fixes it, authored per doctrine so the
    /// choice is a standing posture rather than per-shot micromanagement.
    ///
    /// Shared by SPACE and GROUND — one doctrine catalog serves all combat (the developer's call, 2026-07-24).
    /// </summary>
    public enum TargetPriority
    {
        /// <summary>Spread fire by current health — the LEGACY behaviour, kept as the default so nothing changes
        /// until a doctrine deliberately authors something else.</summary>
        Balanced,

        /// <summary>Finish the cripples first (lowest current health). Converts damage into KILLS fastest, because a
        /// dead unit stops shooting back — but it wastes overkill on targets already falling.</summary>
        FinishWounded,

        /// <summary>Shoot the biggest threat first (highest firepower). Cuts incoming damage soonest; slowest to
        /// actually reduce enemy numbers.</summary>
        BiggestThreat,

        /// <summary>Shoot whatever is nearest. The brawler's choice — pairs with a closing posture.</summary>
        Closest,

        /// <summary>Reach past the front line for the enemy's long-range/support units (artillery, missile boats).
        /// High payoff, but only reachable if your own reach or position allows it.</summary>
        Backfield,

        /// <summary>Concentrate on the heavily-armoured. Wants penetration to be worth anything.</summary>
        Heaviest,
    }

    /// <summary>Which combat domain a doctrine entry may be selected for.</summary>
    public enum DoctrineDomain
    {
        /// <summary>Selectable by fleets AND ground formations.</summary>
        Both,
        /// <summary>Fleets only.</summary>
        Space,
        /// <summary>Ground formations only.</summary>
        Ground,
    }
}
