using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 4 FINISH gauge (docs/AI-BRAIN-BUILD-TRACKER.md — 🌌 The Galaxy + Crisis). The earlier `GalaxyCrisisTests`
    /// proved the DETECTOR + COALITION but FORCED the capability with a direct `Data.Unlock(capability-ascension)` — so
    /// it never proved the crisis was REACHABLE in a real game. This gauge closes the cradle-to-grave first rung: a
    /// faction that RESEARCHES the real base-mod `tech-ascension` ("Transcendence") tech is granted the
    /// `capability-ascension` capability THROUGH the tech's Unlocks (not a hand-forced flag), the detector names it, and
    /// the NPC coalition forms against it. Together with `BaseModIntegrityTests` (the tech loads) this makes the galaxy
    /// crisis a thing that can actually happen in a played game, not just a unit-test fixture.
    /// </summary>
    [TestFixture]
    public class GalaxyCrisisReachabilityTests
    {
        [Test]
        [Description("Researching the base-mod Transcendence tech grants the ascension capability; the detector names it and the NPC coalition forms.")]
        public void ResearchingTheAscensionTech_GrantsTheCapability_AndFormsTheCoalition()
        {
            var s = TestScenario.CreateWithColony();                                   // s.Faction = the (non-NPC) player
            var ascendant = FactionFactory.CreateBasicFaction(s.Game, "Ascendant", "ASC", 0);
            var npc = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RVL", 0);
            npc.GetDataBlob<FactionInfoDB>().IsNPC = true;
            var data = ascendant.GetDataBlob<FactionInfoDB>().Data;
            var when = s.Game.TimePulse.GameGlobalDateTime;

            // Not ascended before research.
            Assert.That(data.HasCapability(GalaxyCrisis.AscensionCapability), Is.False, "no capability before research");
            Assert.That(GalaxyCrisis.Ascendant(s.Game), Is.EqualTo(Entity.InvalidEntity));

            // The REAL research path: the tech starts LOCKED; a prerequisite makes it available (Unlock moves it
            // LockedTechs → Techs), then researching level 1 fires its Unlocks → "capability-ascension".
            data.Unlock("tech-ascension");
            Assert.That(data.Techs.ContainsKey("tech-ascension"), Is.True,
                "the base-mod Transcendence tech is real and becomes researchable when its prerequisite unlocks it");
            data.IncrementTechLevel("tech-ascension");

            Assert.That(data.HasCapability(GalaxyCrisis.AscensionCapability), Is.True,
                "researching Transcendence grants the ascension capability THROUGH the tech (the missing cradle rung)");

            // The detector names the ascended faction, and the coalition forms through the real machinery.
            Assert.That(GalaxyCrisis.Ascendant(s.Game), Is.EqualTo(ascendant), "the researched faction IS the galaxy crisis");
            int declared = GalaxyCrisis.FormCoalitionAgainstAscendant(s.Game, when);
            Assert.That(declared, Is.GreaterThanOrEqualTo(1), "the NPC unites against the ascendant");
            Assert.That(npc.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(ascendant.Id).AtWar, Is.True,
                "the NPC declared war on the ascendant");
            // The player is not auto-committed.
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(ascendant.Id).AtWar, Is.False,
                "the player keeps agency");
        }
    }
}
