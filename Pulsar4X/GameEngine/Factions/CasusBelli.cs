using System;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// A justification for war (docs/DIPLOMACY-DESIGN.md "Casus belli — war needs a REASON"). Anything other than
    /// <see cref="None"/> is a reason your own population will accept; <see cref="None"/> is a naked war of
    /// aggression your people question.
    /// </summary>
    public enum CasusBelli
    {
        None,            // no justification — a war of naked aggression (the legitimacy bleed)
        BorderDispute,   // a contested frontier
        BrokenTreaty,    // they broke a pact with you (the betrayal that frees your hand)
        AllyDefense,     // a defensive-pact partner was attacked — you're dragged in, justified
        ConfrontRival,   // your own Militarists raised a "Confront [Rival]" demand — the population already backs it
        Retaliation      // answering a raid/attack they started
    }

    /// <summary>
    /// The militarism GATE on going to war (task #33). You cannot declare war for free: a war WITHOUT a
    /// <see cref="CasusBelli"/> is a legitimacy/morale hit ("why are we dying for this?"), a war WITH one is
    /// accepted — and the size of that swing is set by the regime's MILITARISM dial (<see cref="GovernmentDB"/>).
    /// A militarist state takes pride in war (even a thin pretext costs little, a just war is a morale BONUS); a
    /// pacifist state tires of it (even a just war costs, an unjustified one can topple you). This is the exact
    /// seam where EXTERNAL war meets INTERNAL legitimacy — and why the militarism dial earns its place.
    ///
    /// Pure/static, reads only a <see cref="GovernmentDB"/> and a <see cref="CasusBelli"/> — the value returned is
    /// the ONE-TIME morale/legitimacy delta applied to the aggressor on declaring war (positive = the war is
    /// popular, negative = it costs you at home). Wiring it into legitimacy/morale is a later slice (#31); this is
    /// the rule the wiring will call. Where a casus belli COMES FROM (a broken-treaty event, a Confront-Rival
    /// demand) is the INTERNAL⟷EXTERNAL handoff, built once demands exist.
    /// </summary>
    public static class CasusBelliRules
    {
        /// <summary>Approval a JUSTIFIED war opens with, before the regime's temperament (a mild popular nod).</summary>
        public const double JustifiedApproval = 5.0;
        /// <summary>The morale/legitimacy hit an UNJUSTIFIED war opens with, before temperament (naked aggression).</summary>
        public const double UnjustifiedPenalty = -30.0;
        /// <summary>How far the militarism dial swings the impact up (militarist) or down (pacifist).</summary>
        public const double MilitarismSwing = 20.0;

        /// <summary>True if <paramref name="cb"/> is a real justification (anything but <see cref="CasusBelli.None"/>).</summary>
        public static bool IsJustified(CasusBelli cb) => cb != CasusBelli.None;

        /// <summary>
        /// The one-time morale/legitimacy impact on the aggressor of declaring this war. Justified vs. naked sets
        /// the baseline; the regime's militarism swings it (High → +, Low → −). So a militarist + justified war is
        /// a morale bonus, a pacifist + unjustified war is a regime-threatening hit — exactly the design's four
        /// corners. A null government reads as the neutral (Mid) case.
        /// </summary>
        public static double WarDeclarationMoraleImpact(GovernmentDB gov, CasusBelli cb)
        {
            double baseImpact = IsJustified(cb) ? JustifiedApproval : UnjustifiedPenalty;

            double militarismShift = 0.0;
            if (gov != null)
            {
                militarismShift = gov.Militarism switch
                {
                    GovNotch.High => +MilitarismSwing,   // martial pride: war is legitimate
                    GovNotch.Low  => -MilitarismSwing,   // pacifist: even a just war tires them
                    _ => 0.0
                };
            }
            return baseImpact + militarismShift;
        }

        /// <summary>
        /// Convenience read for AI/UI: is this war "cheap" for this regime — i.e. does declaring it leave the
        /// aggressor at or above break-even morale? A militarist with a casus belli says yes; a pacifist without
        /// one says a hard no.
        /// </summary>
        public static bool IsWarPopular(GovernmentDB gov, CasusBelli cb)
            => WarDeclarationMoraleImpact(gov, cb) >= 0.0;
    }
}
