using NUnit.Framework;
using Pulsar4X.Engine;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Smoke test for the save/load pipeline. A save/load corruption is a player-facing disaster — it silently
    /// loses an entire game — and there is currently NO active test covering it: SavingAndLoadingTests and
    /// SerializationManagerTests are both commented out. This is the only guard that Game.Save → Game.Load
    /// round-trips without throwing. Broad by design (asserts "round-trips without throwing", not field-level
    /// equality); a deep equality check is a worthwhile follow-up.
    /// </summary>
    [TestFixture]
    public class SaveLoadSmokeTests
    {
        [Test]
        [Description("Game.Save then Game.Load on a generated universe must round-trip without throwing.")]
        public void SaveThenLoad_RoundTripsWithoutThrowing()
        {
            var game = TestingUtilities.CreateTestUniverse(1, generateDefaultHumans: false);

            string json = null;
            Assert.DoesNotThrow(() => json = Game.Save(game), "Game.Save threw.");
            Assert.That(json, Is.Not.Null.And.Not.Empty, "Game.Save produced no JSON.");

            Game reloaded = null;
            Assert.DoesNotThrow(() => reloaded = Game.Load(json),
                "Game.Load threw on the JSON that Game.Save just produced.");
            Assert.That(reloaded, Is.Not.Null, "Game.Load returned null.");

            // Re-saving the reloaded game catches state that loads but is internally broken.
            Assert.DoesNotThrow(() => Game.Save(reloaded), "Re-saving the reloaded game threw.");
        }
    }
}
