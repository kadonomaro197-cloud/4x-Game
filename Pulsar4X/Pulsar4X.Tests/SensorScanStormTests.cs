using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;      // combat flags (client regime)
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Modding;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// DIAGNOSTIC + regression gauge for the SensorScan "STILL CLIMBING" freeze the developer's SIM-STALL watchdog
    /// caught in the DevTest sandbox (2026-07-16): game-time crawls while <see cref="SensorScan.ScanCount"/> explodes
    /// and NO combat forms. Every base-mod sensor scans every 3600 s, so a healthy game logs about
    /// (sensor-entities × game-hours) scans — a few dozen per hour, NOT thousands. This loads the real DevTest scenario,
    /// advances the clock a little under the client's combat-flag regime, and:
    ///   • asserts the scan rate is BOUNDED (the regression guard), and
    ///   • prints a PER-ENTITY scan breakdown (id · owner · kind · ScanTime · count) so a storm names its culprit in the
    ///     CI log (the Visibility Gate — build the gauge).
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
        [Description("Advancing the DevTest scenario a little must not explode SensorScan.ScanCount; prints the per-entity scan breakdown so a storm names its culprit. Hang-proof (background task + wall cap).")]
        public void DevTestScenario_AdvancingTheClock_DoesNotStormSensorScans()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(game, ScenarioDir,
                new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            // Only NON-Stasis systems are processed (gotcha 6) — promote every system so scans fire on the advance.
            foreach (var sys in game.Systems) sys.IncrementExternalObserver(priority: true);

            int sensorEntities = game.Systems.Sum(s => s.GetAllEntitiesWithDataBlob<SensorAbilityDB>().Count);
            const double gameHours = 1.0;
            // Healthy ≈ one scan per 3600 s per sensor-entity → ~sensorEntities scans per game-hour. Allow 20× slack
            // (multiple receivers + the bootstrap double-kick). The captured freeze was ~170×/hr — far past this.
            long bound = Math.Max(50, (long)(sensorEntities * 20 * gameHours));

            SensorScan.AttributeScans = true;
            SensorScan.ScansByEntity.Clear();
            long before = SensorScan.ScanCount;

            bool p1 = CombatEngagement.InterruptTimeOnNewEngagement, p2 = CombatEngagement.EnableClosingRange,
                 p3 = CombatEngagement.RequireDetectionToEngage, p4 = CombatEngagement.RequireWeaponRangeToEngage,
                 p5 = CombatEngagement.RequireWeaponsReleaseToEngage;
            bool finished = false;
            try
            {
                CombatEngagement.InterruptTimeOnNewEngagement = true;
                CombatEngagement.EnableClosingRange = true;
                CombatEngagement.RequireDetectionToEngage = true;
                CombatEngagement.RequireWeaponRangeToEngage = true;
                CombatEngagement.RequireWeaponsReleaseToEngage = true;
                game.Settings.EnforceSingleThread = true;

                // Advance on a background task so a sim busy-loop can NEVER hang the shard; BAIL as soon as the storm is
                // evident so we never feed it. Small 5-game-minute steps keep the work tiny in the healthy case.
                var advance = Task.Run(() =>
                {
                    game.TimePulse.Ticklength = TimeSpan.FromMinutes(5);
                    int steps = (int)(gameHours * 60 / 5);   // 12 × 5 game-min = 1 game-hour
                    for (int i = 0; i < steps; i++)
                    {
                        if (SensorScan.ScanCount - before > bound * 4) break;   // storm already proven — stop feeding it
                        game.TimePulse.TimeStep();
                    }
                });
                finished = advance.Wait(TimeSpan.FromSeconds(45));
                if (!finished) game.TimePulse.PauseTime();   // ask the sim to stop at its next master-loop boundary
            }
            finally
            {
                CombatEngagement.InterruptTimeOnNewEngagement = p1;
                CombatEngagement.EnableClosingRange = p2;
                CombatEngagement.RequireDetectionToEngage = p3;
                CombatEngagement.RequireWeaponRangeToEngage = p4;
                CombatEngagement.RequireWeaponsReleaseToEngage = p5;
                SensorScan.AttributeScans = false;
                game.Settings.EnforceSingleThread = false;
            }

            long scans = SensorScan.ScanCount - before;
            TestContext.WriteLine($"[scan-storm] {sensorEntities} sensor-entities · ~{gameHours} game-hr · {scans} scans · advanceFinished={finished} · healthy-bound={bound}");
            foreach (var kv in SensorScan.ScansByEntity.ToArray().OrderByDescending(k => k.Value).Take(15))
            {
                Entity e = null;
                foreach (var sys in game.Systems) if (sys.TryGetEntityById(kv.Key, out e)) break;
                string kind = e == null ? "?" : e.HasDataBlob<ShipInfoDB>() ? "ship"
                    : e.HasDataBlob<ColonyInfoDB>() ? "colony" : "other";
                int scanTime = -1;
                if (e != null && e.TryGetDataBlob<SensorAbilityDB>(out var sab) && sab.InstanceAtributes.Count > 0)
                    scanTime = sab.InstanceAtributes[0].ScanTime;
                int owner = e?.FactionOwnerID ?? -999;
                TestContext.WriteLine($"[scan-storm]   entity {kv.Key,-6} owner={owner,-6} {kind,-7} ScanTime={scanTime,-6} scans={kv.Value}");
            }

            Assert.That(finished, Is.True,
                "the clock advance did NOT finish within the wall-clock cap — the DevTest sim is wedging/crawling (the freeze). See the per-entity scan breakdown above for the culprit.");
            Assert.That(scans, Is.LessThanOrEqualTo(bound),
                $"SensorScan STORM: {scans} scans in ~{gameHours} game-hr over {sensorEntities} sensor-entities far exceeds the healthy bound {bound} — see the per-entity breakdown above for the culprit.");
        }
    }
}
