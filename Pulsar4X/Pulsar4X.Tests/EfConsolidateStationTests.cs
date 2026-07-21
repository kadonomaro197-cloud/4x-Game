using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;       // Game, NewGameSettings, GameFactory, Entity, DevTestStartFactory
using Pulsar4X.Factions;     // FactionInfoDB, FactionState, ConsolidateResolver, StrategicObjective(DB), PlannerAction
using Pulsar4X.Industry;     // IndustryAbilityDB (the queued job proof)
using Pulsar4X.Modding;      // ModDataStore, ModLoader

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall D2.1 (b) — CONSOLIDATE IS STATION-LEGAL: a station-only faction is no longer frozen in a
    /// crisis.
    ///
    /// Plain English: the Consolidate crisis-brain's one lever was "ease tax on the most restless colony", which needs
    /// a <c>ColonyEconomyDB</c> — a lever a STATION doesn't carry. So the Kithrin (station-only) hit Consolidate, found
    /// NO legal step, and did nothing (A6 finding — "acts in good times, does nothing when the house is on fire"). This
    /// slice gives Consolidate a station-legal fall-through: when no host carries the colony tax lever, it delegates to
    /// the host-agnostic GrowEconomy build rungs (growing infrastructure is a real stabilization move). The gauge:
    /// loads the DevTest Kithrin (a real station-only faction) and proves Consolidate now emits a REAL, EXECUTABLE
    /// action — never a guaranteed no-op. A normal colony faction's Consolidate stays byte-identical (guarded by
    /// <see cref="ConsolidateResolverTests"/>' content-colony gauge — a content colony still returns None).
    /// </summary>
    [TestFixture]
    public class EfConsolidateStationTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewDevTestGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,   // DevTest authors its own factions from JSON
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            });
        }

        [Test]
        [Description("A station-only faction (the DevTest Kithrin, colonies EMPTY, Titan station only) under the "
                     + "Consolidate objective emits a REAL action (not None), and that action is executable — it queues "
                     + "a build on the station. Before this slice Consolidate had no station-legal lever and returned "
                     + "None (A6: the crisis brain froze for a station faction).")]
        public void StationOnlyFaction_UnderConsolidate_EmitsARealAction()
        {
            var game = NewDevTestGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            // The Kithrin: an NPC that is STATION-ONLY (no colonies).
            var kithrinEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Stations.Count > 0);
            var kithrin = kithrinEntity.GetDataBlob<FactionInfoDB>();
            Assert.That(kithrin.Colonies.Count(c => c != null && c.IsValid), Is.EqualTo(0),
                "precondition: the Kithrin are station-only (no colonies) — the case A6 found frozen under Consolidate");

            var state = FactionState.Snapshot(kithrinEntity);
            Assert.That(state, Is.Not.Null, "Kithrin snapshot is null.");
            Assert.That(state.Colonies.Count, Is.GreaterThan(0),
                "the station-aware snapshot should fold the Titan station in as a build host");

            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Consolidate };
            var action = new ConsolidateResolver().Resolve(state, objective);

            // The fix: Consolidate now finds a station-legal step instead of the old guaranteed None.
            Assert.That(action.Kind, Is.Not.EqualTo(PlannerAction.None.Kind),
                "a station-only faction under Consolidate must emit a REAL action, not the old None no-op");

            // And it's a genuine, executable action — running it queues a real build on the station.
            var stationHost = state.Colonies.First(c => c.Colony.HasDataBlob<IndustryAbilityDB>()).Colony;
            int jobsBefore = TotalJobs(stationHost);
            action.Execute();
            Assert.That(TotalJobs(stationHost), Is.GreaterThan(jobsBefore),
                "executing the Consolidate action should queue a build on the station (a real, host-agnostic step)");
        }

        private static int TotalJobs(Entity host)
        {
            if (!host.TryGetDataBlob<IndustryAbilityDB>(out var ind)) return 0;
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }
    }
}
