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
        /// The WAR-FOOTING overload: same as <see cref="SelectWithReason(NeedTier,DoctrineVector,PersonalityDB)"/>, but
        /// a faction that is AT WAR AND WINNING (<paramref name="atWarAndWinning"/>) doesn't wait for prosperity to
        /// attack. A military-led or aggressive belligerent presses the offensive (<see cref="StrategicObjective.Conquer"/>)
        /// from the <see cref="NeedTier.Stabilize"/> tier — AND from <see cref="NeedTier.Survive"/> too, as long as its
        /// homeland is NOT in open rebellion (<paramref name="homelandInRebellion"/> false). This is deliberate: a
        /// faction based on HOSTILE worlds (Mars/Venus) carries a permanent conditions morale penalty that can pin it at
        /// the Survive tier forever, so gating Conquer behind "morale recovered" means it could never invade at all —
        /// the DevTest UMF, exactly. A dominant, warlike power that is WINNING presses its war despite domestic strain;
        /// only a LOSING war (excluded by <paramref name="atWarAndWinning"/>) or an actual REBELLION at home keeps it on
        /// defense (recover first — you don't invade while your capital is in open revolt). Without the war footing,
        /// Conquer is reachable ONLY from Ambition (thriving + rich + dominant), so a battered-but-strong aggressor never
        /// attacks. A faction not (at war and winning) is byte-identical to the 3-arg call.
        /// </summary>
        public static (StrategicObjective objective, string reason) SelectWithReason(
            NeedTier tier, DoctrineVector doctrine, PersonalityDB personality, bool atWarAndWinning, bool homelandInRebellion = false)
        {
            bool warFootingTier = tier == NeedTier.Stabilize
                || (tier == NeedTier.Survive && !homelandInRebellion);
            if (warFootingTier && atWarAndWinning && WantsWar(doctrine, personality, out var why))
            {
                string tierNote = tier == NeedTier.Survive ? "Survive tier (strained but winning + no revolt)" : "Stabilize tier";
                return (StrategicObjective.Conquer, $"{tierNote} + at war and winning: {why} → press the offensive (Conquer)");
            }
            return SelectWithReason(tier, doctrine, personality);
        }

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
            if (WantsWar(d, personality, out var why))
                return (StrategicObjective.Conquer, $"Ambition tier: {why} → Conquer");

            var (obj, axis) = DominantGrowth(d);   // a peaceful ambition still grows — just from a position of dominance
            return (obj, $"Ambition tier (peaceful): {axis} → {obj}");
        }

        /// <summary>Does this faction WANT war — is it military-led by doctrine, or aggressive by personality? Shared by
        /// the Ambition aim and the Stabilize war-footing so both read "warlike" the same way. <paramref name="why"/>
        /// names what tipped it (the Military weight or the Aggression trait), null when it doesn't want war.</summary>
        private static bool WantsWar(DoctrineVector d, PersonalityDB personality, out string why)
        {
            bool militaryLed = d.Military >= d.Economic && d.Military >= d.Tech && d.Military >= d.Expansion && d.Military > 0f;
            if (militaryLed) { why = $"Military-led doctrine ({d.Military:0.00})"; return true; }

            float aggression = personality == null ? (float)PersonalityDB.Neutral
                : (float)personality.TraitOf(PersonalityTrait.Aggression);
            if (aggression > (float)PersonalityDB.Neutral) { why = $"Aggression {aggression:0.00} > neutral"; return true; }

            why = null;
            return false;
        }
    }
}
