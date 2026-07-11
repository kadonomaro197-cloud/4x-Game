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
        {
            switch (tier)
            {
                case NeedTier.Survive:   return StrategicObjective.Defend;
                case NeedTier.Stabilize: return StrategicObjective.Consolidate;
                case NeedTier.Thrive:    return DominantGrowth(doctrine);
                case NeedTier.Ambition:  return AmbitionAim(doctrine, personality);
                default:                 return StrategicObjective.None;
            }
        }

        /// <summary>At Thrive, the strongest of the three peaceful growth axes decides. Military weight doesn't buy a
        /// war from the Thrive tier (that's an Ambition move) — it folds into building the economy that funds one.</summary>
        private static StrategicObjective DominantGrowth(DoctrineVector d)
        {
            float econ = d.Economic, tech = d.Tech, expand = d.Expansion;
            if (expand > econ && expand >= tech) return StrategicObjective.Expand;
            if (tech > econ && tech >= expand) return StrategicObjective.AdvanceTech;
            return StrategicObjective.GrowEconomy;   // Economic-led, all-zero, or a tie → the base
        }

        /// <summary>At Ambition, a Military-led OR Aggressive faction goes for Conquer; otherwise it presses its
        /// strongest peaceful axis (Expand / AdvanceTech / GrowEconomy).</summary>
        private static StrategicObjective AmbitionAim(DoctrineVector d, PersonalityDB personality)
        {
            float aggression = personality == null ? (float)PersonalityDB.Neutral
                : (float)personality.TraitOf(PersonalityTrait.Aggression);
            bool militaryLed = d.Military >= d.Economic && d.Military >= d.Tech && d.Military >= d.Expansion && d.Military > 0f;

            if (militaryLed || aggression > (float)PersonalityDB.Neutral)
                return StrategicObjective.Conquer;

            return DominantGrowth(d);   // a peaceful ambition still grows — just from a position of dominance
        }
    }
}
