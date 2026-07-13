using System;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-2c — the posting-danger model (docs/SITE-ENGINE-DESIGN.md §5: "where a leader is posted sets
    /// their incident/death risk, mitigated by the Command Berth's Survivability"). A field-site's Hook sets how
    /// dangerous the posting is; the berth's Survivability dial buys that risk down. Pure/static/deterministic (no
    /// clock/RNG) so the roll itself stays in the processor and this is exactly testable.
    ///
    /// The base anomaly is <see cref="SiteHook.Benign"/> → zero danger, so SE-1/SE-2b are byte-identical: a benign
    /// site never rolls an incident (and never touches the RNG). Danger only bites on the hostile hooks.
    /// </summary>
    public static class SiteHazard
    {
        /// <summary>The base chance PER DAY that a leader posted to a site of this Hook suffers an incident, before the
        /// berth's Survivability reduces it. Benign = 0 (the base anomaly is safe). v1 tunables.</summary>
        public static double BaseIncidentChancePerDay(SiteHook hook)
        {
            switch (hook)
            {
                case SiteHook.Guardian:  return 0.02; // something guards it and lashes out
                case SiteHook.Cursed:    return 0.03; // the site itself harms those who study it
                case SiteHook.Contested: return 0.015; // a rival presence
                case SiteHook.Reactive:  return 0.01;  // it responds to being worked
                case SiteHook.Gated:     return 0.005; // mostly a lock, low lethality
                case SiteHook.Benign:
                case SiteHook.MoralFork: // a choice, not a lethal hazard
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// The effective incident chance for one work step of <paramref name="days"/>: the Hook's base per-day chance,
        /// reduced by the berth's <paramref name="survivability"/> (0 = no protection, ≥100 = immune), times the days
        /// this step covered. Clamped to [0, 1]. A Survivability that meets/exceeds 100 zeroes the risk.
        /// </summary>
        public static double IncidentChance(SiteHook hook, int survivability, double days)
        {
            double baseChance = BaseIncidentChancePerDay(hook);
            if (baseChance <= 0 || days <= 0) return 0.0;

            double protection = Math.Clamp(survivability / 100.0, 0.0, 1.0);
            double chance = baseChance * (1.0 - protection) * days;
            return Math.Clamp(chance, 0.0, 1.0);
        }
    }
}
