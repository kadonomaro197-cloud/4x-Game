using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Espionage E6 — broadened live effects beyond gather. Proves the STEAL-FUNDS op (the clean Ledger→Ledger wire, the
    /// BP "steal funds" verb): a clean op siphons a fixed fraction of the target's treasury into the actor's, and a
    /// CAUGHT op steals nothing (the effect only lands on a non-caught run). The deeper catalog effects
    /// (steal-tech/sabotage/sow-unrest) are their own follow-on slices; this locks in the money wire.
    /// </summary>
    [TestFixture]
    public class CovertEffectsTests
    {
        private static Entity MakeAgent(TestScenario s, int cap)
        {
            var agentDB = CommanderFactory.CreateAgent(s.Game);
            agentDB.ExperienceCap = cap;
            var agent = CommanderFactory.Create(s.StartingSystem, s.Faction.Id, agentDB);
            foreach (var b in CommanderBonuses.RollEspionageCompetence(cap))
                agent.GetDataBlob<BonusesDB>().Bonuses.Add(b);
            return agent;
        }

        [Test]
        [Description("E6: a CLEAN steal-funds op siphons StealFundsFraction of the target's treasury into the actor's.")]
        public void StealFunds_Clean_TransfersLoot()
        {
            var s = TestScenario.CreateWithColony();
            var target = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var now = s.Colony.StarSysDateTime;
            var actorInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var targetInfo = target.GetDataBlob<FactionInfoDB>();

            // Seed the target's treasury explicitly so the test is self-contained.
            targetInfo.Money.AddIncome(now, TransactionCategory.InitialInvestment, "test seed", 10000m);
            decimal targetBefore = targetInfo.Money.GetCurrentFunds();
            decimal actorBefore = actorInfo.Money.GetCurrentFunds();
            Assert.That(targetBefore, Is.GreaterThan(0m), "the target has funds to steal");

            var agent = MakeAgent(s, 200);
            Espionage.TaskAgent(agent, target.Id, CovertAction.StealFunds, IntelFacet.Economy, now);
            var op = agent.GetDataBlob<CovertOpDB>();
            EspionageProcessor.ApplyOutcome(agent, op, s.Game, CovertOutcome.Clean, now);

            decimal expectedLoot = targetBefore * (decimal)EspionageProcessor.StealFundsFraction;
            Assert.That(targetInfo.Money.GetCurrentFunds(), Is.EqualTo(targetBefore - expectedLoot),
                "the rival loses the siphoned funds");
            Assert.That(actorInfo.Money.GetCurrentFunds(), Is.EqualTo(actorBefore + expectedLoot),
                "the actor gains exactly what the rival lost");
            TestContext.Progress.WriteLine($"[covert-effect] stole {expectedLoot} of {targetBefore} from the rival");
        }

        [Test]
        [Description("E6: a CAUGHT steal-funds op steals nothing — the effect only lands on a non-caught run.")]
        public void StealFunds_Caught_StealsNothing()
        {
            var s = TestScenario.CreateWithColony();
            var target = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var now = s.Colony.StarSysDateTime;
            var targetInfo = target.GetDataBlob<FactionInfoDB>();
            targetInfo.Money.AddIncome(now, TransactionCategory.InitialInvestment, "test seed", 10000m);
            decimal targetBefore = targetInfo.Money.GetCurrentFunds();

            var agent = MakeAgent(s, 0);
            Espionage.TaskAgent(agent, target.Id, CovertAction.StealFunds, IntelFacet.Economy, now);
            var op = agent.GetDataBlob<CovertOpDB>();
            EspionageProcessor.ApplyOutcome(agent, op, s.Game, CovertOutcome.Caught, now);

            Assert.That(targetInfo.Money.GetCurrentFunds(), Is.EqualTo(targetBefore),
                "a caught op steals nothing — the treasury is untouched");
        }
    }
}
