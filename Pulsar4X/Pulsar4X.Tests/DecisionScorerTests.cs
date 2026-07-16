using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase A-2a gauge — the shared cross-domain SCORER (the developer's Q1 "best not first"). This IS the fingerprint
    /// proof from docs/ai/AI-PERSONALITY-IMPLEMENTATION-SPEC.md §3 in miniature: the SAME <see cref="DecisionScorer"/>
    /// function, given two options (a warship vs a refinery), makes a WARLIKE faction build the warship and a
    /// MERCANTILE faction build the refinery — purely from the personality dials. If two contrasting factions DON'T
    /// split here, a trait is unwired. Pure math, no colony harness. Additive/unwired in the engine → byte-identical.
    /// </summary>
    [TestFixture]
    public class DecisionScorerTests
    {
        private sealed class Opt : IScoredOption
        {
            private readonly Dictionary<DecisionFeature, double> _f;
            public Opt(Dictionary<DecisionFeature, double> f) { _f = f; }
            public IReadOnlyDictionary<DecisionFeature, double> Features => _f;
        }

        private static readonly Opt Warship = new Opt(new Dictionary<DecisionFeature, double>
        {
            { DecisionFeature.MilitarySolve, 1.0 },
            { DecisionFeature.RiskLevel, 0.3 },
        });
        private static readonly Opt Refinery = new Opt(new Dictionary<DecisionFeature, double>
        {
            { DecisionFeature.EconGain, 1.0 },
        });

        private static PersonalityDB Person(double aggression, double ambition, double risk)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, aggression);
            p.SetTrait(PersonalityTrait.Ambition, ambition);
            p.SetTrait(PersonalityTrait.Risk, risk);
            return p;
        }

        [Test]
        [Description("The worked proof: a warlike faction (Aggr .9, Risk .8) builds the warship (1.08 > 0.30); a mercantile one (Aggr .2, Amb .8) builds the refinery (0.80 > 0.08). Same function, opposite pick — the personality fingerprint.")]
        public void SameFunction_OppositeChoice_FromTheDialsAlone()
        {
            // Warlike ("Klingon"): Aggr 0.9, Amb 0.5, Risk 0.8.
            var warlike = Person(0.9, 0.5, 0.8);
            double wWar = DecisionScorer.Score(Warship, warlike);
            double wRef = DecisionScorer.Score(Refinery, warlike);
            Assert.That(wWar, Is.EqualTo(1.08).Within(1e-9), "warship for a warlike faction: 1·Aggr(0.9) + 0.3·RiskLevel(0.6)");
            Assert.That(wRef, Is.EqualTo(0.30).Within(1e-9), "refinery for a warlike faction: Amb·0.5(0.25) + (1−Aggr)·0.5(0.05)");
            Assert.That(DecisionScorer.PickBest(new[] { Warship, Refinery }, warlike), Is.SameAs(Warship),
                "the warlike faction picks the warship");

            // Mercantile ("Ferengi"): Aggr 0.2, Amb 0.8, Risk 0.3.
            var mercantile = Person(0.2, 0.8, 0.3);
            double mWar = DecisionScorer.Score(Warship, mercantile);
            double mRef = DecisionScorer.Score(Refinery, mercantile);
            Assert.That(mWar, Is.EqualTo(0.08).Within(1e-9), "warship for a mercantile faction: Aggr(0.2) + 0.3·RiskLevel(−0.4)");
            Assert.That(mRef, Is.EqualTo(0.80).Within(1e-9), "refinery for a mercantile faction: Amb·0.5(0.40) + (1−Aggr)·0.5(0.40)");
            Assert.That(DecisionScorer.PickBest(new[] { Warship, Refinery }, mercantile), Is.SameAs(Refinery),
                "the mercantile faction picks the refinery");
        }

        [Test]
        [Description("A neutral (all-0.5 / null) personality weights every feature the same middling amount — no crash, deterministic pick, and null reads neutral identically.")]
        public void NeutralAndNull_AreSafe_AndEquivalent()
        {
            var neutral = new PersonalityDB();   // all traits default to 0.5

            Assert.That(DecisionScorer.Score(Warship, neutral), Is.EqualTo(DecisionScorer.Score(Warship, null)).Within(1e-12),
                "a null personality reads neutral — the same score");
            Assert.That(DecisionScorer.PickBest(new[] { Warship, Refinery }, neutral), Is.Not.Null,
                "a neutral faction still makes a (deterministic) pick");
            // Empty set → default (null), never throws.
            Assert.That(DecisionScorer.PickBest(new Opt[0], neutral), Is.Null, "an empty option set picks nothing");
        }
    }
}
