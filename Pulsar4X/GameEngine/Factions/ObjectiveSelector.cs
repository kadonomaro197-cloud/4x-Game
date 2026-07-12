namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.4a (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the OBJECTIVE SELECTOR. Given
    /// the faction's current needs-tier (2.2), its strategic <see cref="DoctrineVector"/>, and its
    /// <see cref="PersonalityDB"/>, it names the one concrete <see cref="StrategicObjective"/> the brain pursues this
    /// cycle. The tier decides the FAMILY (survive → defend, stabilize → consolidate, thrive → grow, ambition →
    /// reach); doctrine + personality decide WHICH growth or grand aim.
    ///
    /// Pure/stateless — the Tick (2.4b) calls it then runs the transition engine. No side effects → byte-identical.
    ///
    /// Phase-5.2 (decision-log): <see cref="SelectWithReason"/> also returns a legible REASON tracing the choice back
    /// to the input that drove it (the tier, and the winning doctrine axis / personality trait) — so a run's decisions
    /// are checkable against the authored inputs, not a black box. <see cref="SelectObjective"/> is the same call
    /// without the reason (byte-identical — it just discards the string).
    /// </summary>
    public static class ObjectiveSelector
    {
        /// <summary>
        /// The objective for a faction at <paramref name="tier"/> with these <paramref name="doctrine"/> weights and
        /// <paramref name="personality"/>. Survive → Defend; Stabilize → Consolidate; Thrive → the strongest of the
        /// GROWTH axes (Economic→GrowEconomy / Tech→AdvanceTech / Expansion→Expand, ties → GrowEconomy); Ambition →
        /// the grand aim (a Military-led or Aggressive faction → Conquer, else Expansion→Expand / Tech→AdvanceTech /
        /// else GrowEconomy).
        /// </summary>
        public static StrategicObjective SelectObjective(NeedTier tier, DoctrineVector doctrine, PersonalityDB personality)
            => SelectWithReason(tier, doctrine, personality).objective;

        /// <summary>
        /// Phase-5.2 decision-log: the objective AND a one-line reason tracing it to the driving input — e.g.
        /// <c>"Ambition tier: Aggression 0.85 &gt; neutral → Conquer"</c> or <c>"Thrive tier: Expansion 0.60 leads growth →
        /// Expand"</c>. Pure. The reason names the tier plus the doctrine axis or trait that decided the family/aim, so
        /// an authored personality's fingerprint is visible in what its brain does.
        /// </summary>
        public static (StrategicObjective objective, string reason) SelectWithReason(
            NeedTier tier, DoctrineVector doctrine, PersonalityDB personality)
        {
            switch (tier)
            {
                case NeedTier.Survive:
                    return (StrategicObjective.Defend, "Survive tier (existential — losing a war / starving / rebellion) → Defend");
                case NeedTier.Stabilize:
                    return (StrategicObjective.Consolidate, "Stabilize tier (internal health — unrest / low legitimacy / debt) → Consolidate");
                case NeedTier.Thrive:
                {
                    var (obj, axis) = DominantGrowth(doctrine);
                    return (obj, $"Thrive tier: {axis} → {obj}");
                }
                case NeedTier.Ambition:
                    return AmbitionAim(doctrine, personality);
                default:
                    return (StrategicObjective.None, "no doctrine weight → None");
            }
        }

        /// <summary>At Thrive, the strongest of the three peaceful growth axes decides. Military weight doesn't buy a
        /// war from the Thrive tier (that's an Ambition move) — it folds into building the economy that funds one.
        /// Returns the objective and a reason fragment naming the winning axis + its weight.</summary>
        private static (StrategicObjective obj, string axis) DominantGrowth(DoctrineVector d)
        {
            float econ = d.Economic, tech = d.Tech, expand = d.Expansion;
            if (expand > econ && expand >= tech) return (StrategicObjective.Expand, $"Expansion {expand:0.00} leads growth");
            if (tech > econ && tech >= expand) return (StrategicObjective.AdvanceTech, $"Tech {tech:0.00} leads growth");
            return (StrategicObjective.GrowEconomy, $"Economic {econ:0.00} leads growth (or tie/all-zero)");
        }

        /// <summary>At Ambition, a Military-led OR Aggressive faction goes for Conquer; otherwise it presses its
        /// strongest peaceful axis (Expand / AdvanceTech / GrowEconomy). Returns the objective + a reason naming what
        /// tipped it to war (Military doctrine or the Aggression trait) or which peaceful axis led. Behaviour is
        /// identical to the original combined check — Conquer if military-led OR aggressive.</summary>
        private static (StrategicObjective objective, string reason) AmbitionAim(DoctrineVector d, PersonalityDB personality)
        {
            float aggression = personality == null ? (float)PersonalityDB.Neutral
                : (float)personality.TraitOf(PersonalityTrait.Aggression);
            bool militaryLed = d.Military >= d.Economic && d.Military >= d.Tech && d.Military >= d.Expansion && d.Military > 0f;

            if (militaryLed)
                return (StrategicObjective.Conquer, $"Ambition tier: Military-led doctrine ({d.Military:0.00}) → Conquer");
            if (aggression > (float)PersonalityDB.Neutral)
                return (StrategicObjective.Conquer, $"Ambition tier: Aggression {aggression:0.00} > neutral → Conquer");

            var (obj, axis) = DominantGrowth(d);   // a peaceful ambition still grows — just from a position of dominance
            return (obj, $"Ambition tier (peaceful): {axis} → {obj}");
        }
    }
}
