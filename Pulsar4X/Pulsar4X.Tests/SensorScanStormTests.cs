using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Modding;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// REGRESSION gauge for the SensorScan "STILL CLIMBING" freeze the developer's SIM-STALL watchdog caught in the
    /// DevTest sandbox (2026-07-16): game-time crawled to a standstill while <see cref="SensorScan.ScanCount"/> exploded
    /// and NO combat formed.
    ///
    /// ROOT CAUSE (fixed 2026-07-17): the scan RESCHEDULE sat INSIDE the per-receiver loop in
    /// <see cref="SensorScan.ProcessEntity"/>, so an entity with K sensor receivers queued K fresh scan events every time
    /// it scanned — and each of those queued K more the next cycle. That's EXPONENTIAL growth with base K: a colony with
    /// 2 receivers reached ~2^18 = 305,426 scans in ~18 cycles and drowned the instance queue. Every 1-receiver entity
    /// (K=1) stayed flat, which is why only the multi-sensor colony detonated. The fix reschedules EXACTLY ONCE per
    /// invocation (outside the loop), so K receivers → 1 reschedule → each entity scans ~once per cycle, flat.
    ///
    /// This test loads the REAL DevTest scenario (which has the multi-receiver colonies that triggered it) and advances the
    /// clock a handful of game-hours. The KEY assertion is PER-ENTITY: with the fix every sensor-entity scans ~once per hour
    /// no matter how many receivers it carries, so a healthy run has each entity in the single digits — while the old
    /// exponential would put the offending multi-receiver colony in the hundreds within ~8 cycles. That per-entity signal is
    /// far more sensitive than the global count (the reason 1 game-hour = 2^1 was too short to catch it before), and it
    /// needs only a few game-hours, so the healthy case stays fast.
    ///
    /// The exponential is combat-INDEPENDENT (it's purely the reschedule multiplying), so this deliberately does NOT turn
    /// on the client's combat flags — that isolates the SensorScan mechanism under test and keeps the per-hour cost low and
    /// predictable.
    ///
    /// HANG-PROOF: the clock advance runs on a background task with a hard WALL-CLOCK cap (NUnit [Timeout] can't abort a
    /// synchronous CPU hang on .NET 8), and it BAILS the instant the storm is evident — so a wedging/crawling sim fails
    /// the test fast instead of pinning a CI runner for hours.
    /// </summary>
    [TestFixture]
    public class SensorScanStormTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1, CreatePlayerFaction = false, DefaultSolStart = true, MasterSeed = 12345, EleStart = true
            });
        }

        [Test, Timeout(90000)]
        [Description("Advancing the DevTest scenario a few game-hours must not explode ANY entity's scan count; prints the per-entity scan breakdown so a storm names its culprit. Hang-proof (background task + wall cap).")]
        public void DevTestScenario_AdvancingTheClock_DoesNotStormSensorScans()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(game, ScenarioDir,
                new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            // Only NON-Stasis systems are processed (gotcha 6) — promote every system so scans fire on the advance.
            foreach (var sys in game.Systems) sys.IncrementExternalObserver(priority: true);

            int sensorEntities = game.Systems.Sum(s => s.GetAllEntitiesWithDataBlob<SensorAbilityDB>().Count);
            const double gameHours = 8.0;   // ~8 scan cycles: enough that the old 2-receiver exponential (2^9=512) is unmissable
            // Healthy per-entity: ~1 scan per 3600 s = ~gameHours scans, plus a couple of bootstrap kicks (the install-kick +
            // the DevTest manual kick can each schedule one). Allow 4× + a pad — a fixed entity stays in the single digits;
            // the exponential blows PAST this by orders of magnitude (a 2-receiver colony would be in the hundreds by cycle 8).
            long perEntityBound = Math.Max(20, (long)(gameHours * 4) + 12);
            // Global backstop: ~sensorEntities scans per game-hour, 20× slack.
            long globalBound = Math.Max(50, (long)(sensorEntities * 20 * gameHours));

            SensorScan.AttributeScans = true;
            SensorScan.ScansByEntity.Clear();
            long before = SensorScan.ScanCount;

            bool finished = false;
            try
            {
                game.Settings.EnforceSingleThread = true;

                // Advance on a background task so a sim busy-loop can NEVER hang the shard; BAIL as soon as the storm is
                // evident so we never feed it. Small 5-game-minute steps keep the work tiny in the healthy case and give
                // the between-step early-bail frequent chances to trip if the exponential ever regresses.
                var advance = Task.Run(() =>
                {
                    game.TimePulse.Ticklength = TimeSpan.FromMinutes(5);
                    int steps = (int)(gameHours * 60 / 5);   // 96 × 5 game-min = 8 game-hours
                    for (int i = 0; i < steps; i++)
                    {
                        if (SensorScan.ScanCount - before > globalBound * 4) break;   // storm already proven — stop feeding it
                        game.TimePulse.TimeStep();
                    }
                });
                finished = advance.Wait(TimeSpan.FromSeconds(60));
                if (!finished) game.TimePulse.PauseTime();   // ask the sim to stop at its next master-loop boundary
            }
            finally
            {
                SensorScan.AttributeScans = false;
                game.Settings.EnforceSingleThread = false;
            }

            long scans = SensorScan.ScanCount - before;
            var byEntity = SensorScan.ScansByEntity.ToArray();
            long worstEntityScans = byEntity.Length == 0 ? 0 : byEntity.Max(k => k.Value);

            TestContext.WriteLine($"[scan-storm] {sensorEntities} sensor-entities · ~{gameHours} game-hr · {scans} scans total · worst-entity={worstEntityScans} · advanceFinished={finished} · per-entity-bound={perEntityBound} · global-bound={globalBound}");
            foreach (var kv in byEntity.OrderByDescending(k => k.Value).Take(15))
            {
                Entity e = null;
                foreach (var sys in game.Systems) if (sys.TryGetEntityById(kv.Key, out e)) break;
                string kind = e == null ? "?" : e.HasDataBlob<ShipInfoDB>() ? "ship"
                    : e.HasDataBlob<ColonyInfoDB>() ? "colony" : "other";
                int scanTime = -1;
                int receivers = -1;
                if (e != null && e.TryGetDataBlob<SensorAbilityDB>(out var sab) && sab.InstanceAtributes.Count > 0)
                {
                    scanTime = sab.InstanceAtributes[0].ScanTime;
                    receivers = sab.InstanceAtributes.Count;
                }
                int owner = e?.FactionOwnerID ?? -999;
                TestContext.WriteLine($"[scan-storm]   entity {kv.Key,-6} owner={owner,-6} {kind,-7} receivers={receivers,-3} ScanTime={scanTime,-6} scans={kv.Value}");
            }

            Assert.That(finished, Is.True,
                "the clock advance did NOT finish within the wall-clock cap — the DevTest sim is wedging/crawling (the freeze). See the per-entity scan breakdown above for the culprit.");
            Assert.That(worstEntityScans, Is.LessThanOrEqualTo(perEntityBound),
                $"SensorScan STORM: one entity scanned {worstEntityScans} times in ~{gameHours} game-hr (healthy per-entity bound {perEntityBound}) — the reschedule-in-loop exponential has regressed; the offending entity (a multi-receiver colony) is named in the per-entity breakdown above.");
            Assert.That(scans, Is.LessThanOrEqualTo(globalBound),
                $"SensorScan STORM: {scans} scans total in ~{gameHours} game-hr over {sensorEntities} sensor-entities far exceeds the healthy global bound {globalBound} — see the per-entity breakdown above for the culprit.");
        }
    }
}
