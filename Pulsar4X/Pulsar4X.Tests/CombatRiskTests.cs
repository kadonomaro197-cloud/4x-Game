using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.6 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the eyes → Risk trait.
    /// Proves the strength-ratio bar scales with Risk (bold engages at parity, cautious demands 2×, neutral 1.5×) and
    /// that the engage decision reads it — so an NPC's willingness to fight tracks what it can see of the enemy.
    /// </summary>
    [TestFixture]
    public class CombatRiskTests
    {
        [Test]
        [Description("Required strength ratio: 1.0 at Risk 1 (parity), 2.0 at Risk 0 (overwhelming), 1.5 at neutral.")]
        public void RequiredStrengthRatio_ScalesWithRisk()
        {
            Assert.That(CombatRisk.RequiredStrengthRatio(1.0), Is.EqualTo(CombatRisk.ParityRatio).Within(1e-9), "bold → parity");
            Assert.That(CombatRisk.RequiredStrengthRatio(0.0), Is.EqualTo(CombatRisk.CautiousRatio).Within(1e-9), "cautious → 2×");
            Assert.That(CombatRisk.RequiredStrengthRatio(0.5), Is.EqualTo(1.5).Within(1e-9), "neutral → a comfortable 1.5× margin");
        }

        [Test]
        [Description("A bold faction engages at parity where a cautious one refuses; a big enough edge wins over even the cautious; no threat always engages.")]
        public void WouldEngage_RiskDecidesAtTheSameOdds()
        {
            // Equal strength: the bold commit, the cautious hold.
            Assert.That(CombatRisk.WouldEngage(100, 100, riskTrait: 1.0), Is.True, "bold engages at parity");
            Assert.That(CombatRisk.WouldEngage(100, 100, riskTrait: 0.0), Is.False, "cautious refuses at parity");
            Assert.That(CombatRisk.WouldEngage(100, 100, riskTrait: 0.5), Is.False, "neutral wants a margin, refuses at parity");

            // Double strength: even the cautious commit.
            Assert.That(CombatRisk.WouldEngage(200, 100, riskTrait: 0.0), Is.True, "cautious commits at 2×");
            Assert.That(CombatRisk.WouldEngage(150, 100, riskTrait: 0.5), Is.True, "neutral commits at 1.5×");

            // No detected threat → always engage (nothing to fear).
            Assert.That(CombatRisk.WouldEngage(0, 0, riskTrait: 0.0), Is.True, "a non-positive enemy estimate always engages");
        }
    }
}
