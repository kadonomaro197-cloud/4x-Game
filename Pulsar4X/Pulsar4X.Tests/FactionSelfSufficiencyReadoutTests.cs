using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB, ColonySustenanceDB
using Pulsar4X.Datablobs;     // ComponentInstancesDB
using Pulsar4X.Energy;        // EnergyGenAbilityDB
using Pulsar4X.Engine;        // Game, Entity, StarSystem, DevTestStartFactory, GameFactory, NewGameSettings
using Pulsar4X.Extensions;    // GetTotalFoodOutput / GetAverageFoodQuality (ComponentInstancesDB extensions)
using Pulsar4X.Factions;      // FactionInfoDB, Ledger, TransactionCategory
using Pulsar4X.Industry;      // MiningDB, MineralsDB, MiningHelper, IndustryAbilityDB, IndustryJob
using Pulsar4X.Modding;       // ModDataStore, ModLoader
using Pulsar4X.Names;         // NameDB
using Pulsar4X.Stations;      // StationInfoDB, StationEconomyDB
using Pulsar4X.Storage;       // CargoStorageDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The PER-FACTION SELF-SUFFICIENCY board (Operation Earthfall slice P0.2).
    ///
    /// Plain English: it loads the DevTest sandbox the developer actually plays (UEF + UMF + Kithrin all in Sol),
    /// runs the game clock forward ~120 days, and for EVERY faction — and every colony AND station it owns — prints
    /// whether that place can feed itself, power itself, dig its own minerals, build anything, and hold its cargo,
    /// plus the whole faction's money ledger (income by category, and balance start -> end). It is a READOUT first
    /// (so a fresh session can SEE what "self-sufficient" looks like per faction) and a light regression sensor
    /// second (it asserts only STRUCTURAL truths — it ran for every host, the ledgers actually recorded money moves,
    /// no numbers baked in — never a balance value that would rot when the developer tunes the economy).
    ///
    /// Why it exists: A6 (docs/earthfall/findings/A6-faction-development.md) found the Kithrin are structurally
    /// BANKRUPT — their Titan station has upkeep but NO income, so a monotonic drain empties the treasury in a bit
    /// over a month. Nothing in CI would have caught that: the existing EconomyReadoutTests watches ONE player
    /// colony, and DevTestFleetRoleReadoutTests loads the sandbox but never advances the clock, so the drain never
    /// happens. This fixture advances the clock across the whole sandbox and prints each faction's ledger — the
    /// Kithrin's StationUpkeep bleed and shrinking balance land right in the readout. It would have caught the
    /// Kithrin drain on day one.
    ///
    /// It is a pure ADDITION: it only READS game state and steps the clock through the ordinary public TimeStep
    /// path, and the DevTest factory does NOT flip the NPC AI action gates (EnableOrderEmission &amp; siblings default
    /// false), so the AI decides but emits no orders — the sim runs byte-identical to a gate-off DevTest.
    ///
    /// The full readout is written to TestResults/self-sufficiency-readout.txt (uploaded as a CI artifact / cat by
    /// the readout-cat ci.yml step) AND mirrored to the test runner output, so it is visible whether the run is
    /// green or red. Heavy multi-faction sim → guarded with [Timeout].
    /// </summary>
    [TestFixture]
    public class FactionSelfSufficiencyReadoutTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        // ~120 game-days is the slice spec — long enough for ~4 monthly economy cycles (tax income + station
        // upkeep both bill at 30-day cadence), so every faction's ledger has real moves to read.
        private const int AdvanceDays = 120;

        // CI budget guard, not a gameplay value: a multi-faction 120-day sim should finish well inside this; if it
        // ever WEDGES (the freeze class P0.1 chases) the fixture fails fast instead of hanging a CI shard forever.
        private const int TimeoutMs = 25 * 60 * 1000; // 25 minutes

        private static Game NewDevTestGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            });
        }

        [Test]
        [Timeout(TimeoutMs)]
        [Description("Loads the DevTest sandbox (UEF+UMF+Kithrin), advances ~120 days, and prints each faction's "
                     + "per-host food/power/mining/industry/cargo self-sufficiency + its money ledger (would have "
                     + "caught the Kithrin station-upkeep drain). Asserts structural truths only.")]
        public void EveryFaction_SelfSufficiency_Over120Days_Readout()
        {
            var game = NewDevTestGame();

            // CreateDevTest loads all three faction files (uef/umf/kithrin) through FactionFactory.LoadFromJson.
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");
            int playerId = player!.Id;

            // Promote Sol out of Stasis, or MasterTimePulse skips it and the clock advances over a FROZEN universe
            // (nothing mines/refines/bills). This is the harness equivalent of "the player is looking at their home
            // system" — exactly what TestScenario.CreateWithColony does with IncrementExternalObserver.
            foreach (var sys in game.Systems)
                sys.IncrementExternalObserver(priority: true);

            var sb = new StringBuilder();
            void Log(string line)
            {
                sb.AppendLine(line);
                TestContext.Progress.WriteLine(line);
            }

            // The factions that carry a FactionInfoDB (skips the neutral faction / any invalid handle) — same filter
            // DevTestFleetRoleReadoutTests uses.
            var factions = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .ToList();

            // ---- START snapshot (before the clock moves) ----
            var startBalance = new Dictionary<int, decimal>();  // factionId -> funds
            var startCargoMass = new Dictionary<int, double>(); // hostId -> stored mass
            var startDeposits = new Dictionary<int, long>();    // hostId -> resource-body deposit total

            foreach (var faction in factions)
            {
                var info = faction.GetDataBlob<FactionInfoDB>();
                startBalance[faction.Id] = info.Money.GetCurrentFunds();
                foreach (var host in HostsOf(info))
                {
                    startCargoMass[host.Id] = CargoMassOf(host);
                    startDeposits[host.Id] = DepositTotalFor(host);
                }
            }

            // ---- ADVANCE the clock ~120 days (single-threaded so any processor throw surfaces on this thread) ----
            game.Settings.EnforceSingleThread = true;
            game.TimePulse.Ticklength = TimeSpan.FromDays(1);
            for (int i = 0; i < AdvanceDays; i++)
                game.TimePulse.TimeStep();

            // ---- END readout ----
            Log("================ PER-FACTION SELF-SUFFICIENCY READOUT ================");
            Log($"DevTest sandbox | advanced {AdvanceDays} game-days | factions with FactionInfoDB: {factions.Count}");
            Log("");

            int hostsIterated = 0;
            int readoutErrors = 0;
            bool allCargoFinite = true;
            long totalLedgerTransactions = 0;
            int playerColonyCount = 0;
            // The biggest station (the Kithrin's 6M Titan hive) — its life-support CAPACITY vs its POPULATION.
            // Before the hive-habitat fix, Titan's 40 space-habitats supported only 20,000, so the 6M hive was
            // ~300x overcrowded → morale cratered → income ~0 → the monotonic bankruptcy. The fix dials the
            // Kithrin habitat's Support Colonists up (reusing the space-habitat component), so capacity >= pop.
            long biggestStationPop = 0, biggestStationCap = 0;

            foreach (var faction in factions)
            {
                var info = faction.GetDataBlob<FactionInfoDB>();
                var hosts = HostsOf(info).ToList();

                Log($"======== FACTION {info.Abbreviation} (id {faction.Id}) ========");
                Log($"  colonies: {ValidCount(info.Colonies)}   stations: {ValidCount(info.Stations)}   " +
                    $"hosts iterated: {hosts.Count}");

                if (faction.Id == playerId)
                    playerColonyCount = ValidCount(info.Colonies);

                foreach (var host in hosts)
                {
                    hostsIterated++;
                    try
                    {
                        string name = host.TryGetDataBlob<NameDB>(out var nm) ? nm.GetName(faction.Id) : $"entity {host.Id}";
                        string kind = host.HasDataBlob<StationInfoDB>() ? "STATION" : "COLONY";
                        long pop = PopulationOf(host);
                        Log($"  ---- {kind} '{name}' (id {host.Id})  pop {pop:N0} ----");

                        // Track the biggest station's life-support capacity (the same value StationPopulationProcessor
                        // reads for crowding) so we can assert it isn't over-crowded — the Kithrin hive-habitat gauge.
                        if (host.HasDataBlob<StationInfoDB>() && pop >= biggestStationPop
                            && host.TryGetDataBlob<ComponentInstancesDB>(out var stComps)
                            && host.TryGetDataBlob<StationInfoDB>(out var stInfo))
                        {
                            biggestStationPop = pop;
                            biggestStationCap = stComps.GetPopulationSupportValue(stInfo.HostingBodyEntity);
                            Log($"    life-support capacity: {biggestStationCap:N0}  (population {pop:N0}, " +
                                $"{(biggestStationCap >= pop ? "OK — not overcrowded" : "OVERCROWDED")})");
                        }

                        ReportFood(Log, host, pop);
                        ReportPower(Log, host, pop);
                        ReportMining(Log, host);
                        ReportDepositDepletion(Log, host, startDeposits);
                        ReportIndustry(Log, host);

                        double massEnd = CargoMassOf(host);
                        double massStart = startCargoMass.TryGetValue(host.Id, out var ms) ? ms : massEnd;
                        if (!double.IsFinite(massEnd) || !double.IsFinite(massStart)) allCargoFinite = false;
                        Log($"    cargo: stored mass {massStart:N1} -> {massEnd:N1}   (delta {massEnd - massStart:N1})");
                    }
                    catch (Exception ex)
                    {
                        readoutErrors++;
                        Log($"    [READOUT ERROR on host {host.Id}] {ex.GetType().Name}: {ex.Message}");
                    }
                }

                // The faction money ledger — the line that makes the Kithrin drain visible.
                var ledger = info.Money;
                var totals = ledger.GetTotalsByCategory();
                int txCount = ledger.GetAllTransactions().Count;
                totalLedgerTransactions += txCount;
                decimal balStart = startBalance.TryGetValue(faction.Id, out var b) ? b : 0m;
                decimal balEnd = ledger.GetCurrentFunds();

                Log($"  LEDGER ({txCount} transaction(s) recorded):");
                if (totals.Count == 0)
                    Log("    (no transactions by category)");
                foreach (var kv in totals.OrderBy(k => k.Key.ToString()))
                    Log($"    {kv.Key,-18} {kv.Value,18:N2}");
                Log($"  BALANCE start -> end: {balStart:N2} -> {balEnd:N2}   (delta {balEnd - balStart:N2})");
                Log("");
            }

            Log("================ SUMMARY ================");
            Log($"hosts iterated: {hostsIterated}   readout errors: {readoutErrors}   " +
                $"total ledger transactions: {totalLedgerTransactions}");

            // Write the readout BEFORE the asserts, so it's always available even if an assertion below fails.
            WriteReadout(sb.ToString());

            // ---- STRUCTURAL asserts only (no balance numbers — they'd rot when the economy is tuned) ----
            Assert.That(factions.Count, Is.GreaterThan(0), "No factions carried a FactionInfoDB — the DevTest load is broken.");
            Assert.That(hostsIterated, Is.GreaterThan(0), "No colonies or stations were iterated across any faction.");
            Assert.That(readoutErrors, Is.EqualTo(0), "A per-host readout threw — see the [READOUT ERROR] lines above.");
            Assert.That(allCargoFinite, Is.True, "A host's cargo mass read as NaN/Infinity (silent state corruption).");
            Assert.That(playerColonyCount, Is.GreaterThan(0), "The player faction owns no colony after the DevTest load.");
            Assert.That(totalLedgerTransactions, Is.GreaterThan(0),
                "No faction ledger recorded a single transaction over 120 days — the economy processors never billed " +
                "(check the starting system is out of Stasis, and that ColonyEconomy/StationUpkeep processors ran).");

            // THE KITHRIN HIVE-HABITAT GAUGE: the biggest station (Titan, ~6M pop) must have life-support capacity
            // >= its population — otherwise it's overcrowded, morale craters, income dies, and the faction goes
            // structurally bankrupt (the A6 drain the developer reported). Red before the fix (cap 20,000 << 6M),
            // green after (the dialed-up hive habitat supports 8M). Guarded so a run with no big station is a no-op.
            if (biggestStationPop > 100_000)
                Assert.That(biggestStationCap, Is.GreaterThanOrEqualTo(biggestStationPop),
                    $"The biggest station (pop {biggestStationPop:N0}) is OVERCROWDED — life-support capacity is only " +
                    $"{biggestStationCap:N0}. Its housing (space/hive habitats) doesn't cover its population, so morale " +
                    "collapses and it bankrupts. Dial up the Kithrin hive-habitat Support Colonists (kithrin.json).");
        }

        // ---- host enumeration ----

        /// <summary>Every valid host (colony + station) owned by a faction — the two parallel registries.</summary>
        private static IEnumerable<Entity> HostsOf(FactionInfoDB info)
        {
            foreach (var c in info.Colonies)
                if (c != null && c.IsValid) yield return c;
            foreach (var st in info.Stations)
                if (st != null && st.IsValid) yield return st;
        }

        private static int ValidCount(IEnumerable<Entity> entities) => entities.Count(e => e != null && e.IsValid);

        // ---- per-host gauges ----

        /// <summary>Food SUPPLY (installed food-production output) vs DEMAND (pop × per-capita) + the computed shortage.</summary>
        private static void ReportFood(Action<string> log, Entity host, long pop)
        {
            if (!host.TryGetDataBlob<ColonySustenanceDB>(out var sust))
            {
                log("    food: (no ColonySustenanceDB)");
                return;
            }
            double demand = pop * sust.PerCapitaFoodDemand;
            double supply = host.TryGetDataBlob<ComponentInstancesDB>(out var comps) ? comps.GetTotalFoodOutput() : 0.0;
            double quality = comps != null ? comps.GetAverageFoodQuality() : 0.0;
            log($"    food: supply {supply:N1}/day  vs  demand {demand:N1}/day  " +
                $"(perCapita {sust.PerCapitaFoodDemand})  shortage {sust.FoodShortage:P0}  quality {quality:F2}");
        }

        /// <summary>Power: reactor+solar max output vs the per-capita demand + the computed shortage.</summary>
        private static void ReportPower(Action<string> log, Entity host, long pop)
        {
            double demand = 0.0;
            if (host.TryGetDataBlob<ColonySustenanceDB>(out var sust))
                demand = pop * sust.PerCapitaPowerDemand;

            if (host.TryGetDataBlob<EnergyGenAbilityDB>(out var egen))
                log($"    power: max output {egen.TotalOutputMax:N1} kW (reactor {egen.MaxOutputFromReactor:N1} + " +
                    $"solar {egen.MaxOutputFromSolar:N1})  demand {demand:N1}  " +
                    (sust != null ? $"shortage {sust.PowerShortage:P0}" : "shortage n/a"));
            else
                log($"    power: (no EnergyGenAbilityDB — no reactor/solar)  demand {demand:N1}  " +
                    (sust != null ? $"shortage {sust.PowerShortage:P0}" : "shortage n/a"));
        }

        /// <summary>Mining rates — how much this host pulls out of its resource body per day.</summary>
        private static void ReportMining(Action<string> log, Entity host)
        {
            if (!host.TryGetDataBlob<MiningDB>(out var mining))
            {
                log("    mining: (no MiningDB)");
                return;
            }
            log($"    mining: NumberOfMines {mining.NumberOfMines}  " +
                $"base-rate total {SumLong(mining.BaseMiningRate)} across {mining.BaseMiningRate.Count} mineral(s)  " +
                $"actual-rate total {SumLong(mining.ActualMiningRate)} across {mining.ActualMiningRate.Count}");
        }

        /// <summary>Deposit depletion: total remaining deposits on the resource body, start -> end (mined = the drop).</summary>
        private static void ReportDepositDepletion(Action<string> log, Entity host, Dictionary<int, long> startDeposits)
        {
            long end = DepositTotalFor(host);
            if (!startDeposits.TryGetValue(host.Id, out var start)) start = end;
            log($"    deposits (resource body): {start:N0} -> {end:N0}   (mined {start - end:N0})");
        }

        /// <summary>Industry line job statuses — which lines exist and what's queued (idle-by-design if 0 jobs).</summary>
        private static void ReportIndustry(Action<string> log, Entity host)
        {
            if (!host.TryGetDataBlob<IndustryAbilityDB>(out var industry))
            {
                log("    industry: (no IndustryAbilityDB)");
                return;
            }
            int totalJobs = industry.ProductionLines.Sum(l => l.Value.Jobs.Count);
            log($"    industry: {industry.ProductionLines.Count} line(s), {totalJobs} job(s) queued");
            foreach (var kv in industry.ProductionLines)
            {
                var line = kv.Value;
                if (line.Jobs.Count == 0) continue;
                foreach (var job in line.Jobs)
                    log($"        line '{line.Name}' job '{job.Name}' [{job.Status}]  " +
                        $"{job.ProductionPointsCost - job.ProductionPointsLeft}/{job.ProductionPointsCost} pts  " +
                        $"done {job.NumberCompleted}/{job.NumberOrdered}");
            }
        }

        // ---- shared readers ----

        private static long PopulationOf(Entity host)
        {
            long pop = 0;
            if (host.TryGetDataBlob<ColonyInfoDB>(out var ci))
                foreach (var kv in ci.Population) pop += kv.Value;
            else if (host.TryGetDataBlob<StationInfoDB>(out var si))
                foreach (var kv in si.Population) pop += kv.Value;
            return pop;
        }

        private static double CargoMassOf(Entity host)
            => host.TryGetDataBlob<CargoStorageDB>(out var cargo) ? cargo.TotalStoredMass : 0.0;

        /// <summary>Total remaining mineral deposits on the host's resource body (colony planet / station body).</summary>
        private static long DepositTotalFor(Entity host)
        {
            if (!MiningHelper.TryGetMiningBody(host, out var body)) return 0;
            if (!body.TryGetDataBlob<MineralsDB>(out var minerals)) return 0;
            long total = 0;
            foreach (var kv in minerals.Minerals)
                total += kv.Value.Amount.Actual;
            return total;
        }

        private static long SumLong(Dictionary<int, long> d)
        {
            long total = 0;
            foreach (var v in d.Values) total += v;
            return total;
        }

        // ---- artifact write ----

        /// <summary>
        /// Write the readout to TestResults/self-sufficiency-readout.txt at the REPO ROOT — the same TestResults/
        /// folder CI's `dotnet test --results-directory TestResults` writes the TRX to (uploaded as an artifact and
        /// cat'd by the readout-cat ci.yml step). Resolved by walking UP from the test-assembly dir to the folder
        /// holding `.github`, so it's independent of the test host's working directory (which is the bin output dir).
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
                string path = Path.Combine(resultsDir, "self-sufficiency-readout.txt");
                File.WriteAllText(path, content);
                TestContext.Progress.WriteLine($"[self-sufficiency] wrote readout -> {path}");
            }
            catch (Exception ex)
            {
                // Never let an artifact-write hiccup red an otherwise-good readout — the content is already in the
                // runner output (mirrored via Log).
                TestContext.Progress.WriteLine($"[self-sufficiency] could not write readout file: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
