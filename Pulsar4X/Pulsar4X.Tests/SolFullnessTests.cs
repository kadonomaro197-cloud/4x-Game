using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Galaxy;
using Pulsar4X.Extensions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Guards that Sol actually feels FULL: the hand-built Sol (loaded from the base-mod blueprint, the live
    /// New Game path) used to contain zero asteroids — no main belt, no Kuiper belt — so the system looked
    /// empty. These tests assert (a) the named real big asteroids are present, and (b) the declared belts
    /// scatter enough rocks that the bands read as populated belts. Engine-only, so it runs in CI (the visual
    /// half — colours, sizes — is client-side and CI-blind).
    /// </summary>
    [TestFixture]
    public class SolFullnessTests
    {
        [Test]
        [Description("Sol's declared asteroid belts (main belt + Kuiper belt) populate with many visible rocks.")]
        public void Sol_HasPopulatedAsteroidBelts()
        {
            var s = TestScenario.CreateWithColony();

            var bodyInfos = s.StartingSystem.GetAllDataBlobsOfType<SystemBodyInfoDB>().ToList();
            int asteroids = bodyInfos.Count(b => b.BodyType == BodyType.Asteroid);

            TestContext.WriteLine($"[sol] asteroid bodies in Sol: {asteroids} (of {bodyInfos.Count} total bodies)");

            // sol.json declares a 140-rock main belt + a 90-rock Kuiper belt plus 10 named asteroids.
            // A fraction of the scattered rocks roll dwarf-planet, so assert a healthy floor, not the exact total.
            Assert.That(asteroids, Is.GreaterThanOrEqualTo(150),
                "Sol's asteroid belts did not populate — expected the main belt + Kuiper belt + named asteroids.");
        }

        [Test]
        [Description("The big real named main-belt asteroids are present in Sol.")]
        public void Sol_HasNamedRealAsteroids()
        {
            var s = TestScenario.CreateWithColony();

            var names = s.StartingSystem.GetAllDataBlobsOfType<SystemBodyInfoDB>()
                .Select(b => b.OwningEntity?.GetDefaultName())
                .Where(n => n != null)
                .ToList();

            foreach (var expected in new[] { "Vesta", "Pallas", "Hygiea", "Psyche" })
                Assert.That(names, Does.Contain(expected), $"Named asteroid '{expected}' missing from Sol.");
        }
    }
}
