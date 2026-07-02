using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the CASUS BELLI militarism gate (docs/DIPLOMACY-DESIGN.md "Casus belli — war needs a REASON",
    /// task #33): the one-time morale/legitimacy impact of declaring war depends on (a) whether you have a
    /// justification and (b) the regime's MILITARISM dial. Proves the design's four corners — militarist+justified
    /// is a morale BONUS, pacifist+unjustified is a regime-threatening hit — and that a justified war always
    /// beats a naked one under the same regime.
    /// </summary>
    [TestFixture]
    public class DiplomacyCasusBelliTests
    {
        private static GovernmentDB Militarist() => new GovernmentDB(GovNotch.Mid, GovNotch.Mid, GovNotch.Mid, GovNotch.High);
        private static GovernmentDB Pacifist()   => new GovernmentDB(GovNotch.Mid, GovNotch.Mid, GovNotch.Mid, GovNotch.Low);
        private static GovernmentDB Balanced()   => new GovernmentDB(GovNotch.Mid, GovNotch.Mid, GovNotch.Mid, GovNotch.Mid);

        [Test]
        [Description("A militarist regime with a justification takes PRIDE in war — a positive morale impact.")]
        public void Militarist_Justified_IsAMoraleBonus()
        {
            double impact = CasusBelliRules.WarDeclarationMoraleImpact(Militarist(), CasusBelli.BrokenTreaty);
            Assert.That(impact, Is.GreaterThan(0.0));
            Assert.That(CasusBelliRules.IsWarPopular(Militarist(), CasusBelli.BrokenTreaty), Is.True);
        }

        [Test]
        [Description("A pacifist regime declaring a NAKED war of aggression takes a regime-threatening hit.")]
        public void Pacifist_Unjustified_IsAHeavyHit()
        {
            double impact = CasusBelliRules.WarDeclarationMoraleImpact(Pacifist(), CasusBelli.None);
            Assert.That(impact, Is.LessThan(CasusBelliRules.UnjustifiedPenalty), "worse than the base naked-war penalty");
            Assert.That(CasusBelliRules.IsWarPopular(Pacifist(), CasusBelli.None), Is.False);
        }

        [Test]
        [Description("Under the SAME regime, a justification always softens the blow vs. a naked war; and militarism always beats pacifism for the same casus belli.")]
        public void Justification_AlwaysBeatsNaked_AndMilitarismBeatsPacifism()
        {
            // Same regime: justified > naked.
            Assert.That(CasusBelliRules.WarDeclarationMoraleImpact(Balanced(), CasusBelli.BorderDispute),
                Is.GreaterThan(CasusBelliRules.WarDeclarationMoraleImpact(Balanced(), CasusBelli.None)));

            // Same casus belli: militarist > balanced > pacifist.
            double m = CasusBelliRules.WarDeclarationMoraleImpact(Militarist(), CasusBelli.AllyDefense);
            double b = CasusBelliRules.WarDeclarationMoraleImpact(Balanced(), CasusBelli.AllyDefense);
            double p = CasusBelliRules.WarDeclarationMoraleImpact(Pacifist(), CasusBelli.AllyDefense);
            Assert.That(m, Is.GreaterThan(b));
            Assert.That(b, Is.GreaterThan(p));
        }

        [Test]
        [Description("A null government reads as the neutral (Mid) case — no militarism swing, just the base impact.")]
        public void NullGovernment_IsNeutral()
        {
            Assert.That(CasusBelliRules.WarDeclarationMoraleImpact(null, CasusBelli.None),
                Is.EqualTo(CasusBelliRules.UnjustifiedPenalty));
            Assert.That(CasusBelliRules.WarDeclarationMoraleImpact(null, CasusBelli.BrokenTreaty),
                Is.EqualTo(CasusBelliRules.JustifiedApproval));
        }
    }
}
