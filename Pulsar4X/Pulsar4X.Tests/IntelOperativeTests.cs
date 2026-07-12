using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Espionage E2 — the OPERATIVE (the agent-as-person) and the Intelligence Directorate's recruitment of them. Proves
    /// (a) an operative's tradecraft (<see cref="CommanderBonuses.RollEspionageCompetence"/>) scales with their ceiling
    /// and reads back through <see cref="CommanderBonuses.EspionageSkill01"/> (the skill the E3 detection roll consumes);
    /// (b) <see cref="CommanderFactory.CreateAgent"/> mints a <see cref="CommanderTypes.Intelligence"/> commander; and
    /// (c) <see cref="IntelDirectorateProcessor"/> recruits agents onto the faction UP TO its op capacity and no further
    /// (build more directorates → more agents; the grave rung stops it when the HQ falls). The recruit rung of the
    /// espionage cradle-to-grave.
    /// </summary>
    [TestFixture]
    public class IntelOperativeTests
    {
        [Test]
        [Description("E2: an operative's tradecraft scales with ExperienceCap (200→full, 100→half, 0→none) and reads back as a 0..1 skill.")]
        public void RollEspionageCompetence_ScalesWithCap_AndReadsBack()
        {
            var full = CommanderBonuses.RollEspionageCompetence(200);
            Assert.That(full.Count, Is.EqualTo(1), "a maxed recruit rolls one tradecraft bonus");
            Assert.That(full[0].Category, Is.EqualTo(BonusCategory.Espionage), "the bonus is in the Espionage category");
            Assert.That(full[0].Value, Is.EqualTo(CommanderBonuses.MaxEspionageCompetenceBonus).Within(1e-9),
                "cap 200 → the full espionage competence");

            var half = CommanderBonuses.RollEspionageCompetence(100);
            Assert.That(half[0].Value, Is.EqualTo(CommanderBonuses.MaxEspionageCompetenceBonus / 2.0).Within(1e-9),
                "cap 100 → half the competence");

            Assert.That(CommanderBonuses.RollEspionageCompetence(0).Count, Is.EqualTo(0), "cap 0 → no tradecraft");

            var bonusesDB = new BonusesDB();
            foreach (var b in full) bonusesDB.Bonuses.Add(b);
            Assert.That(CommanderBonuses.EspionageSkill01(bonusesDB),
                Is.EqualTo(CommanderBonuses.MaxEspionageCompetenceBonus).Within(1e-9), "the skill reads back through the reader");
            Assert.That(CommanderBonuses.EspionageSkill01(null), Is.EqualTo(0.0), "no DB → skill 0");
            Assert.That(CommanderBonuses.EspionageSkill01(new BonusesDB()), Is.EqualTo(0.0), "no tradecraft → skill 0");
        }

        [Test]
        [Description("E2: CreateAgent mints an Intelligence-type commander (the spy twin of CreateScientist).")]
        public void CreateAgent_IsIntelligenceType()
        {
            var s = TestScenario.CreateWithColony();
            var agentDB = CommanderFactory.CreateAgent(s.Game);
            Assert.That(agentDB.Type, Is.EqualTo(CommanderTypes.Intelligence), "an agent is a CommanderTypes.Intelligence");
        }

        [Test]
        [Description("E2: the Intelligence Directorate recruits operatives up to op capacity and no further; each carries real tradecraft.")]
        public void Directorate_RecruitsAgents_UpToCapacity_NotBeyond()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            Assert.That(IntelDirectorateProcessor.CountAgents(factionInfo), Is.EqualTo(0),
                "no operatives before a directorate exists");

            // Build a directorate with op capacity 2, then run the recruiting cycle by hand (deterministic — no sim).
            var atb = new IntelDirectorateAtb(opCapacity: 2, counterIntelRating: 20);
            atb.OnComponentInstallation(colony, null);

            var proc = new IntelDirectorateProcessor();
            proc.ProcessEntity(colony, colony.StarSysDateTime);
            Assert.That(IntelDirectorateProcessor.CountAgents(factionInfo), Is.EqualTo(1), "first recruit cycle → 1 agent");

            proc.ProcessEntity(colony, colony.StarSysDateTime);
            Assert.That(IntelDirectorateProcessor.CountAgents(factionInfo), Is.EqualTo(2), "second cycle → 2 agents (op capacity)");

            proc.ProcessEntity(colony, colony.StarSysDateTime);
            Assert.That(IntelDirectorateProcessor.CountAgents(factionInfo), Is.EqualTo(2),
                "capacity reached → no over-recruiting");

            // The recruited operatives are real Intelligence agents carrying a tradecraft bonus.
            Entity agent = Entity.InvalidEntity;
            foreach (var commander in factionInfo.Commanders)
                if (commander.TryGetDataBlob<CommanderDB>(out var cdb) && cdb.Type == CommanderTypes.Intelligence)
                {
                    agent = commander;
                    break;
                }
            Assert.That(agent.IsValid, Is.True, "at least one recruited operative exists");
            Assert.That(agent.HasDataBlob<BonusesDB>(), Is.True, "the operative has a BonusesDB");
            double skill = CommanderBonuses.EspionageSkill01(agent.GetDataBlob<BonusesDB>());
            Assert.That(skill, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(CommanderBonuses.MaxEspionageCompetenceBonus + 1e-9),
                "the recruit's tradecraft skill sits in [0, max]");
            TestContext.Progress.WriteLine($"[intel-operative] recruited 2 agents at op capacity; sample skill={skill:F3}");
        }
    }
}
