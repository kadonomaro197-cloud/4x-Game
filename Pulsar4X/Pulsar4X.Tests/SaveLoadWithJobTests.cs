using NUnit.Framework;
using Pulsar4X.Engine;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Regression for the 2026-07-03 "save didn't work" bug: LOADING any save that contained a QUEUED PRODUCTION JOB
    /// threw NullReferenceException. Cause: <see cref="Pulsar4X.Industry.IndustryJob"/> had no parameterless ctor, so
    /// Newtonsoft deserialized it through the <c>(FactionInfoDB, string)</c> ctor with a null factionInfo →
    /// <c>factionInfo.IndustryDesigns[itemID]</c> NRE. The base <see cref="SaveLoadSmokeTests"/> missed it because its
    /// universe has no colony and therefore no jobs. This one queues a real job FIRST, so the save actually contains an
    /// IndustryJob to round-trip. Fixed by a [JsonConstructor] parameterless ctor on IndustryJob.
    /// </summary>
    [TestFixture]
    public class SaveLoadWithJobTests
    {
        [Test]
        [Description("A save that contains a queued production job must round-trip Game.Save -> Game.Load without throwing.")]
        public void SaveLoad_WithQueuedProductionJob_RoundTrips()
        {
            var s = TestScenario.CreateWithColony();
            s.QueueProductionJob("space-crete", count: 1, repeat: true);   // puts an IndustryJob in the save

            string json = null;
            Assert.DoesNotThrow(() => json = Game.Save(s.Game), "Game.Save threw.");
            Assert.That(json, Is.Not.Null.And.Not.Empty, "Game.Save produced no JSON.");

            Game reloaded = null;
            Assert.DoesNotThrow(() => reloaded = Game.Load(json),
                "Game.Load threw on a save that contains a queued production job (the IndustryJob deserialization NRE).");
            Assert.That(reloaded, Is.Not.Null, "Game.Load returned null.");

            // Re-saving the reloaded game catches state that loads but is internally broken.
            Assert.DoesNotThrow(() => Game.Save(reloaded), "Re-saving the reloaded game threw.");
        }
    }
}
