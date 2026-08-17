using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL C7 — capture-transfer (developer ruling A): "capture flips the colony + installations
    /// + surviving population + stockpiles to the conqueror, with a population/unrest hit." The bare owner-ID flip
    /// already carried the installations/population/stockpiles (they're datablobs on the colony entity); C7 adds the two
    /// things it missed — the faction REGISTRY move (add to captor's <see cref="FactionInfoDB.Colonies"/>, remove from
    /// the loser's — the capture-stale registry <c>FactionAssets</c>/B-S9a works around) and a POPULATION casualty
    /// (persistent, because population is a stored count — a durable morale/legitimacy "unrest" penalty is deferred to
    /// C7b since those recompute each cycle). Gated behind <see cref="GroundForcesProcessor.EnableCaptureTransfer"/>
    /// (default OFF → the bare flip, byte-identical; NewGameMenu turns it ON).
    /// </summary>
    [TestFixture]
    public class CaptureTransferTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[capture-transfer] " + m);

        // Stand up a colony (owned by s.Faction) on a region-mapped body, with a rival faction "Reds" registered.
        private static (TestScenario s, Entity body, PlanetRegionsDB regions, Entity reds) Setup()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            // Reds hold EVERY region — the uniform-hold condition TryCapturePlanet needs to flip the colony.
            foreach (var r in regions.Regions) r.OwnerFactionID = reds.Id;
            return (s, body, regions, reds);
        }

        private static long TotalPop(Entity colony) => colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
        private static List<Entity> ColonyList(Entity faction) => faction.GetDataBlob<FactionInfoDB>().Colonies;

        [Test]
        [Description("Flag ON: taking a planet flips the colony to the conqueror, MOVES it between the faction registries " +
                     "(joins the captor's Colonies list, leaves the loser's), takes a population casualty, and the " +
                     "installations ride the entity to the new owner.")]
        public void CaptureTransfer_FlagOn_MovesRegistry_TakesPopHit_KeepsInstallations()
        {
            var (s, body, regions, reds) = Setup();

            // Preconditions: the colony belongs to the player faction and sits in ITS registry, not Reds'.
            Assume.That(s.Colony.FactionOwnerID, Is.EqualTo(s.Faction.Id));
            Assume.That(ColonyList(s.Faction).Any(c => c.Id == s.Colony.Id), Is.True, "setup: player faction owns the colony in its registry");
            Assume.That(ColonyList(reds).Any(c => c.Id == s.Colony.Id), Is.False, "setup: Reds don't yet own it");
            long popBefore = TotalPop(s.Colony);
            Assume.That(popBefore, Is.GreaterThan(0), "setup: the colony has population");
            bool hadComponents = s.Colony.HasDataBlob<ComponentInstancesDB>();

            bool prev = GroundForcesProcessor.EnableCaptureTransfer;
            GroundForcesProcessor.EnableCaptureTransfer = true;
            try
            {
                GroundForcesProcessor.TryCapturePlanet(body, regions);
            }
            finally { GroundForcesProcessor.EnableCaptureTransfer = prev; }

            long popAfter = TotalPop(s.Colony);
            Log($"owner {s.Faction.Id}→{s.Colony.FactionOwnerID} (reds={reds.Id}); pop {popBefore}→{popAfter}");

            Assert.That(s.Colony.FactionOwnerID, Is.EqualTo(reds.Id), "the colony flips to the conqueror");
            Assert.That(ColonyList(reds).Any(c => c.Id == s.Colony.Id), Is.True, "the colony JOINS the captor's registry");
            Assert.That(ColonyList(s.Faction).Any(c => c.Id == s.Colony.Id), Is.False, "the colony LEAVES the loser's registry");
            Assert.That(popAfter, Is.LessThan(popBefore), "a population casualty is taken");
            Assert.That(popAfter, Is.GreaterThan(0), "…but the survivors transfer (not depopulated)");
            // ~15% casualty (per-species rounding) — a loose band so the flagged fraction can be tuned without breaking the gauge.
            Assert.That(popAfter, Is.LessThan((long)(popBefore * 0.95)).And.GreaterThan((long)(popBefore * 0.75)),
                "roughly the CaptureCasualtyFraction is lost");
            if (hadComponents)
                Assert.That(s.Colony.HasDataBlob<ComponentInstancesDB>(), Is.True, "installations ride the entity to the new owner");
        }

        [Test]
        [Description("Flag OFF (the engine default): the bare owner-flip still happens, but the registry and population " +
                     "are UNTOUCHED — the C7 additions are byte-identical when the flag is off.")]
        public void CaptureTransfer_FlagOff_OnlyOwnerFlips_RegistryAndPopUnchanged()
        {
            var (s, body, regions, reds) = Setup();
            long popBefore = TotalPop(s.Colony);

            bool prev = GroundForcesProcessor.EnableCaptureTransfer;
            GroundForcesProcessor.EnableCaptureTransfer = false;
            try
            {
                GroundForcesProcessor.TryCapturePlanet(body, regions);
            }
            finally { GroundForcesProcessor.EnableCaptureTransfer = prev; }

            Assert.That(s.Colony.FactionOwnerID, Is.EqualTo(reds.Id), "the bare owner-flip is unconditional (unchanged today)");
            Assert.That(TotalPop(s.Colony), Is.EqualTo(popBefore), "flag off → NO population casualty");
            Assert.That(ColonyList(reds).Any(c => c.Id == s.Colony.Id), Is.False, "flag off → the captor's registry is unchanged");
            Assert.That(ColonyList(s.Faction).Any(c => c.Id == s.Colony.Id), Is.True, "flag off → the loser's registry is unchanged (stale, as today)");
        }
    }
}
