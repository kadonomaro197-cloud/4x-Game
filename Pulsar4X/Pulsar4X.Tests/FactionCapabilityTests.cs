using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-D2 gauge (docs/AI-BRAIN-BUILD-TRACKER.md): a tech can grant a CAPABILITY (an ability the sim reads), not
    /// only move a buildable id. Proves (a) a fresh faction has no capabilities, (b) unlocking a "capability-"
    /// prefixed id grants that capability, (c) unlocking an ordinary id grants none (only the prefix routes to the
    /// capability set → byte-identical for every existing unlock), and (d) capabilities survive a clone (save/load,
    /// entity transfer).
    /// </summary>
    [TestFixture]
    public class FactionCapabilityTests
    {
        [Test]
        [Description("A capability-prefixed unlock grants a capability; an ordinary unlock grants none; clones carry it.")]
        public void Unlock_RoutesCapabilityIds_ToTheCapabilitySet()
        {
            var store = new FactionDataStore();

            Assert.That(store.Capabilities, Is.Empty, "a fresh faction has no capabilities");
            Assert.That(store.HasCapability("capability-star-to-matter"), Is.False);

            // A capability-prefixed unlock grants the ABILITY (not a buildable).
            store.Unlock("capability-star-to-matter");
            Assert.That(store.HasCapability("capability-star-to-matter"), Is.True,
                "a capability-prefixed unlock is routed to the capability set");
            Assert.That(store.Capabilities, Has.Count.EqualTo(1));

            // An ordinary id (no prefix, not a known buildable) grants NO capability — the byte-identical no-op path.
            store.Unlock("some-ordinary-material");
            Assert.That(store.HasCapability("some-ordinary-material"), Is.False);
            Assert.That(store.Capabilities, Has.Count.EqualTo(1),
                "only capability-prefixed ids reach the capability set — every existing unlock is unaffected");

            // Capabilities survive a clone (the copy-ctor path used on save/load + entity transfer).
            var clone = new FactionDataStore(store);
            Assert.That(clone.HasCapability("capability-star-to-matter"), Is.True, "capabilities survive a clone");
        }
    }
}
