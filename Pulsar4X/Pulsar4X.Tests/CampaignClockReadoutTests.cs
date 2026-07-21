using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pulsar4X.Combat;    // CombatEngagement
using Pulsar4X.Engine;    // Game, GameFactory, NewGameSettings, MasterTimePulse, ManagerSubPulse, DevTestStartFactory, StarSystem, SystemActivityState
using Pulsar4X.Factions;  // NPCDecisionProcessor
using Pulsar4X.Modding;   // ModDataStore, ModLoader
using Pulsar4X.Sensors;   // SensorScan

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL P0.1 — the CAMPAIGN-CLOCK CI repro fixture (findings/A1-freeze.md, the MISSING gauge).
    ///
    /// The developer's real DevTest game FROZE. The freeze findings ruled the fine-step machinery OUT (the strike fleet
    /// was ~21-22 Gm from Earth's fleets — far outside the 1 Gm <see cref="CombatEngagement.EngagementRange_m"/> — for the
    /// whole transit, so no NEW engagement was ever imminent and <see cref="MasterTimePulse.FineStepCount"/> stayed FLAT;
    /// the real freeze was a NATIVE CLIENT render/hang that no headless CI fixture can reproduce). This fixture is the
    /// standing GAUGE that pins the ENGINE half: it loads the exact DevTest war sandbox (UEF + UMF + Kithrin), OPENS the
    /// four NPC action gates AND the three client combat flags — i.e. the FULL "everything enabled" state a real DevTest
    /// game runs, with the combat interrupt's fine-step machinery ARMED — then drives the REAL master clock hour by hour
    /// across a bounded transit window and asserts the sim does NOT crawl:
    ///   • <b>FineStepCount FLAT</b> on every game-day where no hostile pair is within <see cref="CombatEngagement.EngagementRange_m"/>
    ///     (i.e. no new engagement is imminent) — the fine-stepper never runs during deep transit;
    ///   • <b>wall-ms per game-day bounded</b> — a generous ceiling that a healthy clock clears easily but the
    ///     "PERF freeze" crawl (a <see cref="SensorScan"/> per 5 s sub-step) blows past by orders of magnitude;
    ///   • <b>zero NPC tick errors</b> — the acting brain never threw across the whole run.
    ///
    /// It drives <see cref="MasterTimePulse"/> DIRECTLY in 3600 s steps rather than via <c>TestScenario.AdvanceTime</c>,
    /// which forces the sim single-threaded internally: the freeze findings ask for the MULTITHREADED code path
    /// (<c>EnableMultiThreading</c>) so the parallel-sim race hypotheses (A1 H2/H3) are on the table. It still needs each
    /// step to BLOCK so the per-day gauges can be read, so <c>EnforceSingleThread</c> makes <see cref="MasterTimePulse.TimeStep()"/>
    /// wait on its task while the inner system pass runs on <c>Parallel.ForEach</c>. (With the Sol-only DevTest there is
    /// one active star system, so this drives the multithreaded path but yields no cross-system parallelism yet — H2/H3
    /// need ≥2 active systems, a future extension. See docs/earthfall/LANE-CORE-NOTES.md.)
    ///
    /// The four NPC gates + three combat flags are process-global statics, captured and RESTORED in a finally so every
    /// other test in the shard stays byte-identical. A full per-game-day readout is written to
    /// TestResults/campaign-clock-readout.txt. <c>[Timeout]</c> turns a real hang into a fast FAILURE (best-effort on
    /// .NET, matching <see cref="CombatFleetTreeSafetyTests"/>).
    /// </summary>
    [TestFixture]
    public class CampaignClockReadoutTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        // FLAGGED balance value: game-days of transit to drive. 40 days from the 2050-01-01 start keeps the strike fleet
        // in deep transit (arrival ~05-18 per findings/A1-freeze.md), so no hostile pair closes to EngagementRange_m and
        // FineStepCount must stay flat the whole window.
        private const int TransitGameDays = 40;              // FLAGGED balance value

        // FLAGGED balance value: generous per-game-day wall-clock ceiling (ms). A healthy clock runs ~10 game-hours per
        // wall-second (findings/A1-freeze.md), so a game-day (~2.4 s) sits far under this; the crawl would blow past it.
        private const long MaxWallMsPerGameDay = 30_000;     // FLAGGED balance value

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            var modLoader = new ModLoader();
            modLoader.LoadModManifest("Data/basemod/modInfo.json", modDataStore);

            var gameSettings = new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,   // DevTest authors its own factions from JSON
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            };
            return GameFactory.CreateGame(modDataStore, gameSettings);
        }

        /// <summary>Any active (non-Stasis) star system with a brand-NEW engagement about to fire — the direct
        /// "a hostile pair is within EngagementRange_m" read, and the exact condition under which the master loop's
        /// fine-stepper is allowed to run. Sampled on the idle test thread between (blocking) TimeSteps, so no race.</summary>
        private static bool AnyImminent(Game game) => game.Systems
            .Where(s => s.ActivityState != SystemActivityState.Stasis)
            .Any(s => CombatEngagement.NewEngagementImminent(s));

        [Test, Timeout(600000)]
        [Description("Drives the REAL master clock across a bounded DevTest transit window with the four NPC gates + three "
                   + "combat flags OPEN (fine-step machinery armed): asserts the combat fine-stepper stays FLAT while no "
                   + "fleets are within EngagementRange_m, per-game-day wall-time is bounded, and the NPC brain throws "
                   + "zero times. The engine-side campaign-clock repro gauge (findings/A1-freeze.md).")]
        public void CampaignClock_DrivesTransit_FineStepFlat_WallBounded_NoTickErrors()
        {
            // Capture every process-global static we flip, so nothing leaks to a sibling test in the shard.
            bool gOrder = NPCDecisionProcessor.EnableOrderEmission;
            bool gDip = NPCDecisionProcessor.EnableDiplomaticProposals;
            bool gEsp = NPCDecisionProcessor.EnableEspionageMirror;
            bool gIntel = NPCDecisionProcessor.EnableIntelLedger;
            bool cInterrupt = CombatEngagement.InterruptTimeOnNewEngagement;
            bool cDetect = CombatEngagement.RequireDetectionToEngage;
            bool cWeaponRange = CombatEngagement.RequireWeaponRangeToEngage;

            var sb = new StringBuilder();
            try
            {
                var game = NewGame();
                var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                // Promote the DevTest Sol out of Stasis, or MasterTimePulse skips it and NO system-level processor
                // (sensors / combat trigger / movement) ever runs — the freeze we're gauging lives in those. The Game
                // ctor does no galaxy-gen, so DevTest's LoadFromBlueprint added exactly this one system → the ID lookup
                // is unambiguous. Foreground (priority) = the player-watched full-rate clock, the most faithful repro.
                var sol = game.Systems.FirstOrDefault(s => s.ID == startingSystemId);
                Assert.That(sol, Is.Not.Null, "DevTest starting system '" + startingSystemId + "' not found in game.Systems.");
                sol.IncrementExternalObserver(priority: true);

                // OPEN the four NPC gates + the three client combat flags — exactly what a real DevTest game runs
                // ("everything enabled" for the brain, the combat interrupt armed). This is what makes the fixture a
                // real repro: the fine-step machinery is LIVE, so a flat FineStepCount actually proves it didn't crawl.
                NPCDecisionProcessor.EnableOrderEmission = true;
                NPCDecisionProcessor.EnableDiplomaticProposals = true;
                NPCDecisionProcessor.EnableEspionageMirror = true;
                NPCDecisionProcessor.EnableIntelLedger = true;
                CombatEngagement.InterruptTimeOnNewEngagement = true;
                CombatEngagement.RequireDetectionToEngage = true;
                CombatEngagement.RequireWeaponRangeToEngage = true;

                // Drive the REAL master clock directly. EnableMultiThreading = the parallel sim path the freeze findings
                // ask for; EnforceSingleThread makes TimeStep() BLOCK on its task so the per-day gauges are deterministic
                // (the two flags are orthogonal — one gates the inner Parallel.ForEach, the other the outer .Wait()).
                game.Settings.EnableMultiThreading = true;
                game.Settings.EnforceSingleThread = true;
                game.TimePulse.Ticklength = TimeSpan.FromSeconds(3600);   // 3600 s (1 game-hour) steps

                // Baselines: the liveness counters are cumulative across the shard's tests, so we read DELTAS.
                long baseFine = MasterTimePulse.FineStepCount;
                long baseTick = CombatEngagement.TickCount;
                long baseScan = SensorScan.ScanCount;
                int baseTickErr = NPCDecisionProcessor.TickErrorCount;
                DateTime startDate = game.TimePulse.GameGlobalDateTime;

                sb.AppendLine("OPERATION EARTHFALL — P0.1 campaign-clock readout (findings/A1-freeze.md)");
                sb.AppendLine("generated " + DateTime.UtcNow.ToString("o"));
                sb.AppendLine("start=" + startDate.ToString("o") + "  system=" + startingSystemId + "  activity=" + sol.ActivityState);
                sb.AppendLine("gates: order/diplomacy/espionage/intel = ON");
                sb.AppendLine("combat flags: interrupt/require-detection/require-weapon-range = ON  (EngagementRange_m=" + CombatEngagement.EngagementRange_m + ")");
                sb.AppendLine("threading: EnableMultiThreading=on  EnforceSingleThread(wait)=on  Ticklength=3600s");
                sb.AppendLine("window=" + TransitGameDays + " game-days  maxWallMsPerGameDay=" + MaxWallMsPerGameDay + "  MaxConsecutiveFineSteps=" + MasterTimePulse.MaxConsecutiveFineSteps);
                sb.AppendLine();
                sb.AppendLine(string.Format("{0,4} | {1,-11} | {2,8} | {3,10} | {4,7} | {5,8} | {6,8} | {7,4} | {8}",
                    "day", "date", "wall_ms", "fineStep+", "trig+", "scan+", "imminent", "err+", "globalCurrentProcess"));

                long prevFine = baseFine, prevTick = baseTick, prevScan = baseScan;
                bool everImminent = false;
                var flatViolations = new List<string>();
                var wallViolations = new List<string>();

                for (int day = 1; day <= TransitGameDays; day++)
                {
                    bool imminentToday = false;
                    var swDay = Stopwatch.StartNew();
                    for (int hour = 0; hour < 24; hour++)
                    {
                        // Sample BEFORE each step: a fine-step in this hour requires imminent to have been true at the
                        // hour's start, which this catches — so imminentToday soundly covers any FineStepCount rise today.
                        imminentToday |= AnyImminent(game);
                        game.TimePulse.TimeStep();   // one 3600 s step, blocking (EnforceSingleThread)
                    }
                    imminentToday |= AnyImminent(game);   // catch imminence that arose on the final step
                    swDay.Stop();

                    long fineNow = MasterTimePulse.FineStepCount;
                    long tickNow = CombatEngagement.TickCount;
                    long scanNow = SensorScan.ScanCount;
                    long dFine = fineNow - prevFine;
                    long dTick = tickNow - prevTick;
                    long dScan = scanNow - prevScan;
                    int errDelta = NPCDecisionProcessor.TickErrorCount - baseTickErr;
                    if (imminentToday) everImminent = true;

                    sb.AppendLine(string.Format("{0,4} | {1,-11} | {2,8} | {3,10} | {4,7} | {5,8} | {6,8} | {7,4} | {8}",
                        day, game.TimePulse.GameGlobalDateTime.ToString("yyyy-MM-dd"), swDay.ElapsedMilliseconds,
                        dFine, dTick, dScan, imminentToday ? "yes" : "no", errDelta,
                        ManagerSubPulse.GlobalCurrentProcess));

                    // The crawl catcher (collected, asserted after the readout is captured).
                    if (swDay.ElapsedMilliseconds > MaxWallMsPerGameDay)
                        wallViolations.Add("day " + day + ": " + swDay.ElapsedMilliseconds + " ms (> " + MaxWallMsPerGameDay + ")");

                    // FineStepCount FLAT while no fleets are within EngagementRange_m: on a day with nothing imminent,
                    // the fine-stepper must not have run at all.
                    if (!imminentToday && dFine != 0)
                        flatViolations.Add("day " + day + " (" + game.TimePulse.GameGlobalDateTime.ToString("yyyy-MM-dd") + "): fineStep+" + dFine + " with nothing imminent");

                    prevFine = fineNow; prevTick = tickNow; prevScan = scanNow;
                }

                int tickErrDelta = NPCDecisionProcessor.TickErrorCount - baseTickErr;
                long totalFine = MasterTimePulse.FineStepCount - baseFine;
                sb.AppendLine();
                sb.AppendLine("SUMMARY  end=" + game.TimePulse.GameGlobalDateTime.ToString("o"));
                sb.AppendLine("  totalFineSteps=" + totalFine
                    + "  totalTriggerPasses=" + (CombatEngagement.TickCount - baseTick)
                    + "  totalScans=" + (SensorScan.ScanCount - baseScan));
                sb.AppendLine("  everImminent=" + everImminent + "  tickErrors=" + tickErrDelta + "  lastTickError='" + NPCDecisionProcessor.LastTickError + "'");
                sb.AppendLine("  wallViolations=" + wallViolations.Count + "  flatViolations=" + flatViolations.Count);
                foreach (var v in wallViolations) sb.AppendLine("    WALL  " + v);
                foreach (var v in flatViolations) sb.AppendLine("    FLAT  " + v);

                // ── ASSERTIONS (the readout is written in the finally, so it captures the run even if these fail) ──

                // (1) zero NPC tick errors — the acting brain never threw across the whole transit.
                Assert.That(tickErrDelta, Is.EqualTo(0),
                    "NPCDecisionProcessor threw " + tickErrDelta + " time(s) during transit; last: '" + NPCDecisionProcessor.LastTickError + "'");

                // (2) wall-ms per game-day under the (generous) crawl ceiling.
                Assert.That(wallViolations, Is.Empty,
                    "game-day wall-time exceeded " + MaxWallMsPerGameDay + " ms — the sim is crawling (PERF-freeze regression): " + string.Join("; ", wallViolations));

                // (3) FineStepCount FLAT while no fleets are within EngagementRange_m.
                Assert.That(flatViolations, Is.Empty,
                    "the combat fine-stepper ran on days with NO imminent engagement (a hostile pair within EngagementRange_m " +
                    "would justify it — none was present): " + string.Join("; ", flatViolations));

                // (4) The A1 finding, made a hard gauge: across the whole transit window no hostile pair closed to
                //     EngagementRange_m, so the fine-stepper never ran — the freeze is NOT a sim crawl.
                if (!everImminent)
                    Assert.That(totalFine, Is.EqualTo(0),
                        "no new engagement was ever imminent over " + TransitGameDays + " game-days, yet FineStepCount rose by " + totalFine);
            }
            finally
            {
                NPCDecisionProcessor.EnableOrderEmission = gOrder;
                NPCDecisionProcessor.EnableDiplomaticProposals = gDip;
                NPCDecisionProcessor.EnableEspionageMirror = gEsp;
                NPCDecisionProcessor.EnableIntelLedger = gIntel;
                CombatEngagement.InterruptTimeOnNewEngagement = cInterrupt;
                CombatEngagement.RequireDetectionToEngage = cDetect;
                CombatEngagement.RequireWeaponRangeToEngage = cWeaponRange;

                WriteReadout(sb.ToString());
                TestContext.Progress.WriteLine(sb.ToString());
            }
        }

        /// <summary>
        /// Write the readout to TestResults/campaign-clock-readout.txt at the REPO ROOT — the same TestResults/ folder
        /// CI's `dotnet test --results-directory TestResults` writes the TRX to (uploaded as an artifact and cat'd by
        /// P0.3's readout-cat ci.yml step, next to P0.2's self-sufficiency-readout.txt). Resolved by walking UP from the
        /// test-assembly dir to the folder holding `.github`, so it's independent of the test host's working directory
        /// (which is the bin output dir). Never fails the gauge on a file-system hiccup — the content is also in the
        /// runner output via TestContext.Progress.
        /// </summary>
        private static void WriteReadout(string content)
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
                    dir = dir.Parent;
                string root = dir?.FullName ?? Directory.GetCurrentDirectory();
                string resultsDir = Path.Combine(root, "TestResults");
                Directory.CreateDirectory(resultsDir);
                string path = Path.Combine(resultsDir, "campaign-clock-readout.txt");
                File.WriteAllText(path, content);
                TestContext.Progress.WriteLine("[campaign-clock] wrote readout -> " + path);
            }
            catch (Exception ex)
            {
                TestContext.Progress.WriteLine("[campaign-clock] could not write readout file: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
