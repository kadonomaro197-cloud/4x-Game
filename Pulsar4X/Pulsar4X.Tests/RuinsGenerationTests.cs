using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Exploration X.1 gauge — the ruins-eligibility TAUTOLOGY fix. For as long as the code existed, ruins never
    /// generated at all: <c>SystemBodyFactory.GenerateRuins</c> gated on <c>bodyType != Terrestrial || bodyType != Moon</c>,
    /// which is ALWAYS true (a body can't be both types at once), so every body bailed before a single ruin rolled.
    /// This pins the corrected eligibility — a ruin needs solid ground (a terrestrial world or a moon), and nothing
    /// else qualifies — so the tautology can never silently come back. (Airless/thin-atmosphere worlds like Luna and
    /// Mars DO qualify: ancient ruins need ground, not air — the Exploration content vision.) The RNG roll itself is
    /// exercised by the system-generation smoke tests, which now run the ruins code path without throwing.
    /// </summary>
    [TestFixture]
    public class RuinsGenerationTests
    {
        [Test]
        [Description("Only a terrestrial world or a moon can hold ruins — the fix for the always-true tautology that suppressed ALL ruins.")]
        public void CanBodyHaveRuins_OnlyTerrestrialAndMoon_QualifyForRuins()
        {
            // The two that CAN hold ruins — solid ground, atmosphere irrelevant (Mars thin, Luna airless).
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.Terrestrial), Is.True, "a terrestrial world can hold ruins");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.Moon), Is.True, "a moon can hold ruins (Luna, Europa)");

            // Everything else cannot — and critically, at least one of these returning true was IMPOSSIBLE under the
            // old tautology; more importantly, under the old bug ALL of the above ALSO bailed, so nothing generated.
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.GasGiant), Is.False, "a gas giant has no surface");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.IceGiant), Is.False, "an ice giant has no surface");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.GasDwarf), Is.False, "a gas dwarf has no surface");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.DwarfPlanet), Is.False, "dwarf planets are out of scope for v1");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.Asteroid), Is.False, "an asteroid is not a ruin world");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.Comet), Is.False, "a comet is not a ruin world");
            Assert.That(SystemBodyFactory.CanBodyHaveRuins(BodyType.Unknown), Is.False, "an unknown body is not a ruin world");
        }
    }
}
