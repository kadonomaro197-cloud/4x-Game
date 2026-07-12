using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 4 gauge (docs/AI-BRAIN-BUILD-TRACKER.md — 🌌 The Galaxy + Crisis). Proves the crisis DETECTOR:
    /// (a) no faction is the Ascendant until one holds the ascension CAPABILITY (Phase-4.1 "a tech grants a
    /// capability" — routed through the F-D2 capability system), and (b) once a faction unlocks it, GalaxyCrisis
    /// names that faction as the galaxy crisis. Pure read → byte-identical. The crisis event + the coalition response
    /// (reuse Phase-3.4) is the next slice.
    /// </summary>
    [TestFixture]
    public class GalaxyCrisisTests
    {
        [Test]
        [Description("No Ascendant until a faction holds the ascension capability; unlocking it (the research path) names that faction the crisis.")]
        public void Ascendant_NoneUntilAFactionUnlocksTheAscensionCapability()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            Assert.That(GalaxyCrisis.Ascendant(s.Game), Is.EqualTo(Entity.InvalidEntity), "no crisis while nobody has ascended");
            Assert.That(GalaxyCrisis.IsCrisisActive(s.Game), Is.False);

            // Reds research the transcendent tech → the F-D2 capability-unlock routes the "capability-" id to the
            // capability set (this is the tech-grants-a-capability path, Phase-4.1).
            reds.GetDataBlob<FactionInfoDB>().Data.Unlock(GalaxyCrisis.AscensionCapability);

            Assert.That(reds.GetDataBlob<FactionInfoDB>().Data.HasCapability(GalaxyCrisis.AscensionCapability), Is.True,
                "the capability flag is set (a tech granted a capability, not a component)");
            Assert.That(GalaxyCrisis.Ascendant(s.Game), Is.EqualTo(reds), "the ascended faction IS the galaxy crisis");
            Assert.That(GalaxyCrisis.IsCrisisActive(s.Game), Is.True, "the galaxy crisis is live");
        }
    }
}
