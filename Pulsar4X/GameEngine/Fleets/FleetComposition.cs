namespace Pulsar4X.Fleets
{
    /// <summary>
    /// How complete/deep a HOLISTIC fleet is against its composition template (developer's design, 2026-07-16). A
    /// fleet is never single-role — it AIMS to contain all the fighting jobs (Screen/Line/Artillery/Support) as
    /// sub-fleet FORMATIONS within it (via <see cref="FleetRoleComposer"/>). These tiers are about SIZE and DEPTH,
    /// not role: how many ships the fleet has grown to, and therefore how organised it can be.
    /// </summary>
    public enum FleetCompositionTier
    {
        /// <summary>Below the minimum — not yet worth deploying; stays home and keeps building.</summary>
        Forming,
        /// <summary>Met the minimum holistic core — deployable (patrol / escort).</summary>
        Deployable,
        /// <summary>Grown to the ideal size (resources permitting) — reorganised into role SUB-FLEETS.</summary>
        Ideal,
        /// <summary>Grown to the perfect size (plentiful resources or the situation demands it) — sub- and SUB-SUB-fleets.</summary>
        Perfect,
    }

    /// <summary>
    /// A per-faction fleet-composition template — the growth LADDER a holistic fleet climbs as the AI feeds it ships
    /// and resources allow. Three thresholds:
    ///   • <see cref="MinToDeploy"/> — the FEWEST ships before the fleet is worth sending out. Below it it's Forming.
    ///   • <see cref="IdealSize"/>   — the size the AI grows it to WHEN RESOURCES ALLOW; there it reorganises into role
    ///     sub-fleets (the "ideal configuration").
    ///   • <see cref="PerfectSize"/> — the size the AI grows it to ONLY when resources are plentiful OR the situation
    ///     demands it; sub-fleets and sub-sub-fleets (the "perfect configuration").
    ///
    /// The fleet stays HOLISTIC at every tier — the role split (Screen/Line/Artillery/Support) is emergent from the
    /// ships it actually holds, via <see cref="FleetRoleComposer"/>, not a per-role fleet. A fleet may EMPHASISE a
    /// strategy — that rides on its per-fleet DOCTRINE (offensive/defensive), a separate dial — while still aiming for
    /// a whole composition. Counts, not role quotas, in v1; a minimum-role-coverage refinement is a later slice.
    /// </summary>
    public class FleetCompositionTemplate
    {
        public string Name { get; }
        public int MinToDeploy { get; }
        public int IdealSize { get; }
        public int PerfectSize { get; }

        public FleetCompositionTemplate(string name, int minToDeploy, int idealSize, int perfectSize)
        {
            Name = name;
            MinToDeploy = minToDeploy;
            IdealSize = idealSize;
            PerfectSize = perfectSize;
        }

        /// <summary>The default holistic strike-fleet ladder (the developer's example numbers): 3 to deploy, 8 ideal,
        /// 18 perfect. A faction can override this with its own template later (per-faction compositions).</summary>
        public static readonly FleetCompositionTemplate DefaultStrikeFleet = new FleetCompositionTemplate("Strike Fleet", 3, 8, 18);

        /// <summary>Which tier a holistic fleet of <paramref name="shipCount"/> ships currently sits at.</summary>
        public FleetCompositionTier TierFor(int shipCount)
        {
            if (shipCount >= PerfectSize) return FleetCompositionTier.Perfect;
            if (shipCount >= IdealSize)   return FleetCompositionTier.Ideal;
            if (shipCount >= MinToDeploy) return FleetCompositionTier.Deployable;
            return FleetCompositionTier.Forming;
        }

        /// <summary>The ship count the fleet should GROW toward for a given ASPIRATION (the resource/urgency decision
        /// lives in the AI, not here). Deployable→MinToDeploy, Ideal→IdealSize, Perfect→PerfectSize; 0 for Forming
        /// (nothing to aim at below the deploy floor — the aim is always at least Deployable).</summary>
        public int TargetCountFor(FleetCompositionTier aspiration)
        {
            switch (aspiration)
            {
                case FleetCompositionTier.Deployable: return MinToDeploy;
                case FleetCompositionTier.Ideal:      return IdealSize;
                case FleetCompositionTier.Perfect:    return PerfectSize;
                default:                              return 0;
            }
        }
    }
}
