using System;
using System.Collections.Generic;
using System.Linq;
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
    /// caught in the DevTest sandbox (2026-07-16): game-time crawls/sticks while <see cref="SensorScan.ScanCount"/>
    /// explodes and NO combat forms. Every base-mod sensor scans every 3600 s, so a healthy game should log about
    /// (sensor-entities × game-hours) scans — a few dozen per hour, NOT tens of thousands. This loads the real DevTest
    /// scenario, advances the clock a few game-hours under the client's combat-flag regime, and:
    ///   • asserts the scan rate is BOUNDED (the regression guard), and
    ///   • prints a PER-ENTITY scan breakdown (id · owner · kind · ScanTime · count) so a storm is pinned on the
    ///     offending entity/design in the CI log, not left as a mystery (the Visibility Gate — build the gauge).
    /// [Timeout] turns a true busy-loop into a fast failure instead of a wedged shard.
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

        [Test, Timeout(120000)]
        [Description("Advancing the DevTest scenario a few game-hours must not explode SensorScan.ScanCount; prints the per-entity scan breakdown so a storm names its culprit.")]
        public void DevTestScenario_AdvancingTheClock_DoesNotStormSensorScans()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(game, ScenarioDir,
                new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            // The sim only processes NON-Stasis systems (Sensors/GameEngine gotcha 6) — promote every system, mirroring
            // "the player is watching", so scans actually fire on the clock advance.
            foreach (var sys in game.Systems) sys.IncrementExternalObserver(priority: true);

            // Mirror the client combat-flag regime (this freeze only shows in the client) so the test is faithful.
            bool pNarrate = CombatEngagement.NarrateToLog, pInterrupt = CombatEngagement.InterruptTimeOnNewEngagement,
                 pClosing = CombatEngagement.EnableClosingRange, pDetect = CombatEngagement.RequireDetectionToEngage,
                 pWpnRange = CombatEngagement.RequireWeaponRangeToEngage, pRelease = CombatEngagement.RequireWeaponsReleaseToEngage;

            int sensorEntities = game.Systems.Sum(s => s.GetAllEntitiesWithDataBlob<SensorAbilityDB>().Count);
            const int hours = 3;

            SensorScan.AttributeScans = true;
            SensorScan.ScansByEntity.Clear();
            long scansBefore = SensorScan.ScanCount;

            try
            {
                CombatEngagement.InterruptTimeOnNewEngagement = true;
                CombatEngagement.EnableClosingRange = true;
                CombatEngagement.RequireDetectionToEngage = true;
                CombatEngagement.RequireWeaponRangeToEngage = true;
                CombatEngagement.RequireWeaponsReleaseToEngage = true;

                game.Settings.EnforceSingleThread = true;
                game.TimePulse.Ticklength = TimeSpan.FromHours(1);
                for (int i = 0; i < hours; i++) game.TimePulse.TimeStep();
            }
            finally
            {
                CombatEngagement.InterruptTimeOnNewEngagement = pInterrupt;
                CombatEngagement.EnableClosingRange = pClosing;
                CombatEngagement.RequireDetectionToEngage = pDetect;
                CombatEngagement.RequireWeaponRangeToEngage = pWpnRange;
                CombatEngagement.RequireWeaponsReleaseToEngage = pRelease;
                CombatEngagement.NarrateToLog = pNarrate;
                SensorScan.AttributeScans = false;
                game.Settings.EnforceSingleThread = false;
            }

            long scans = SensorScan.ScanCount - scansBefore;
            double perHour = scans / (double)hours;
            TestContext.WriteLine($"[scan-storm] {sensorEntities} sensor-entities · {hours} game-hours · {scans} scans ({perHour:F0}/hr). "
                + $"Healthy ≈ sensor-entities/hr (3600 s ScanTime).");

            // Per-entity breakdown — the culprit namer. Resolve each scanning id to its owner/kind/ScanTime.
            foreach (var kv in SensorScan.ScansByEntity.OrderByDescending(k => k.Value).Take(15))
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

            // Regression bound: a sensor scans once per 3600 s, so per game-hour the whole game should log on the order of
            // (sensor-entities) scans. Allow generous slack (multiple receivers per entity + the bootstrap first-scan
            // double-kick): 20× the sensor count per hour. The captured freeze was ~170×/hr — far past this — so this
            // FAILS loudly (with the breakdown above) if the storm reproduces, and passes for a healthy scan cadence.
            long bound = Math.Max(50, (long)sensorEntities * 20L * hours);
            Assert.That(scans, Is.LessThanOrEqualTo(bound),
                $"SensorScan STORM: {scans} scans in {hours} h ({perHour:F0}/hr) over {sensorEntities} sensor-entities "
                + $"far exceeds the healthy bound {bound} — see the per-entity breakdown above for the culprit.");
        }
    }
}
