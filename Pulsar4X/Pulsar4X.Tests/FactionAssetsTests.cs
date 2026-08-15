using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// B-S9 (OPERATION BLUEPRINT-TO-STEEL) — the live-owner cross-check for the Force-Management roster's colony/station
    /// rows (FORCES-WINDOW-DESIGN §S9). <see cref="FactionAssets.OwnedColonies"/> / <see cref="FactionAssets.OwnedStations"/>
    /// filter a faction's registry (<c>FactionInfoDB.Colonies</c>/<c>Stations</c>) down to the assets whose LIVE
    /// <c>Entity.FactionOwnerID</c> still equals the faction — the registry is capture-stale (a taken planet flips its
    /// live owner but lingers in the old faction's registry), so the roster must verify ownership, not trust the list.
    /// </summary>
    [TestFixture]
    public class FactionAssetsTests
    {
        [Test]
        [Description("OwnedColonies returns a faction's own colonies, and DROPS one whose live FactionOwnerID has flipped away (capture-stale) even though it still lingers in the FactionInfoDB.Colonies registry.")]
        public void OwnedColonies_ReturnsLiveOwned_DropsCapturedAwayEvenIfStillInRegistry()
        {
            var s = TestScenario.CreateWithColony();
            var registry = s.Faction.GetDataBlob<FactionInfoDB>().Colonies;

            // Sanity: the start colony is in the registry AND live-owned by the faction.
            Assert.That(registry, Does.Contain(s.Colony), "the start colony is in the faction's registry");
            Assert.That(FactionAssets.OwnedColonies(s.Faction), Does.Contain(s.Colony),
                "a live-owned colony is returned");

            // CAPTURE it away: flip the colony's live owner (as GroundForcesProcessor does on a planet capture) WITHOUT
            // touching the registry (which that path leaves stale).
            int otherFaction = s.Faction.Id + 1000;
            s.Colony.FactionOwnerID = otherFaction;

            // The registry STILL lists it (proving the stale-registry condition), but the live-owner filter drops it.
            Assert.That(registry, Does.Contain(s.Colony), "the registry is unchanged by a capture (the stale-registry trap)");
            Assert.That(FactionAssets.OwnedColonies(s.Faction), Does.Not.Contain(s.Colony),
                "a colony whose live owner flipped away is NOT returned, even though the registry still names it");

            // Restore ownership → it's back.
            s.Colony.FactionOwnerID = s.Faction.Id;
            Assert.That(FactionAssets.OwnedColonies(s.Faction), Does.Contain(s.Colony),
                "restoring the live owner re-includes it");

            TestContext.Progress.WriteLine($"[faction-assets] registry={registry.Count} colony live-owner filter drops a captured-away colony");
        }

        [Test]
        [Description("OwnedStations is the Stations sibling — empty for a station-less faction (the default scenario builds no station), and defensive.")]
        public void OwnedStations_EmptyForStationlessFaction()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(FactionAssets.OwnedStations(s.Faction), Is.Empty,
                "the default scenario builds no station");
        }

        [Test]
        [Description("Both accessors are defensive: a null entity and a bare (unmanaged) Entity.Create() both return an empty list and never throw.")]
        public void FactionAssets_Defensive_NullAndUnmanaged()
        {
            Assert.That(FactionAssets.OwnedColonies(null), Is.Empty, "null → empty, no throw");
            Assert.That(FactionAssets.OwnedStations(null), Is.Empty, "null → empty, no throw");

            var bare = Entity.Create();   // unmanaged (Manager == null) → TryGetDataBlob would NRE without the guard
            Assert.That(FactionAssets.OwnedColonies(bare), Is.Empty, "unmanaged entity → empty, no throw");
            Assert.That(FactionAssets.OwnedStations(bare), Is.Empty, "unmanaged entity → empty, no throw");
        }
    }
}
