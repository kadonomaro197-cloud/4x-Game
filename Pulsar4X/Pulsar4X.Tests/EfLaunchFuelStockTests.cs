using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;      // Game, Entity, DevTestStartFactory, GameFactory, NewGameSettings
using Pulsar4X.Factions;    // FactionInfoDB
using Pulsar4X.Modding;     // ModDataStore, ModLoader
using Pulsar4X.Names;       // NameDB
using Pulsar4X.Ships;       // LaunchComplexDB
using Pulsar4X.Storage;     // CargoStorageDB, TypeStore

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P4.3 — the LAUNCH-FUEL gauge (findings/A4-sealift.md cause 2).
    ///
    /// Plain English: a colony that owns a launch complex puts a finished ship into orbit by BURNING fuel from its own
    /// fuel tanks — <see cref="LaunchComplexProcessor.TryLaunchShip"/> calls <c>TryDeductFuel</c>, which first checks the
    /// colony has a "fuel-storage" cargo store AT ALL, then deducts the lift-to-orbit fuel from it. A4 found the UMF's
    /// Mars colony had a launch complex but NO fuel-storage store and NO fuel cargo authored, so a built troop transport
    /// would sit on the pad forever — the launch silently returned false every day. This gauge loads the DevTest sandbox
    /// (UEF + UMF + Kithrin, the same one <see cref="FactionSelfSufficiencyReadoutTests"/> loads) and asserts the invariant
    /// that would have caught the trap: EVERY host (colony or station) that owns a launch complex also stocks its launch
    /// fuel — i.e. it has a "fuel-storage" TypeStore with a positive amount of fuel in it, the exact two preconditions
    /// TryDeductFuel needs to succeed.
    ///
    /// No clock advance: the fuel is stocked at colony build (installation adds the fuel-storage store via
    /// CargoStorageAtb, then LoadCargo fills it), so the state to check exists the moment the sandbox is built — this
    /// keeps the fixture fast (it lands in the `rest` CI shard) and independent of any processor running.
    ///
    /// It is a pure READ of game state + a structural assert; it steps no clock and flips no flag, so it cannot perturb
    /// any other fixture.
    /// </summary>
    [TestFixture]
    public class EfLaunchFuelStockTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";
        private const string FuelStoreTypeId = "fuel-storage";

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
        [Description("Every colony/station that owns a launch complex stocks its launch fuel (a non-empty 'fuel-storage' "
                     + "cargo store) — the two preconditions TryDeductFuel needs. Guards findings/A4 cause 2 (Mars had a "
                     + "launch complex but no fuel-storage store and no fuel).")]
        public void EveryLaunchComplexHost_StocksItsLaunchFuel()
        {
            var game = NewDevTestGame();

            // Loads all three faction files (uef-devtest/umf/kithrin) through FactionFactory.LoadFromJson — the same
            // sandbox the self-sufficiency readout uses.
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

            var factions = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .ToList();
            Assert.That(factions.Count, Is.GreaterThan(0), "No factions carried a FactionInfoDB — the DevTest load is broken.");

            int launchHostsChecked = 0;

            foreach (var faction in factions)
            {
                var info = faction.GetDataBlob<FactionInfoDB>();
                foreach (var host in HostsOf(info))
                {
                    // Only hosts that actually own a launch complex are on the hook here.
                    if (!host.TryGetDataBlob<LaunchComplexDB>(out var launchDB))
                        continue;

                    launchHostsChecked++;
                    string name = host.TryGetDataBlob<NameDB>(out var nm) ? nm.GetName(faction.Id) : $"entity {host.Id}";

                    // Precondition 1 — the host can even STORE fuel (TryDeductFuel's first check).
                    Assert.That(host.TryGetDataBlob<CargoStorageDB>(out var storage), Is.True,
                        $"[{info.Abbreviation}] launch-complex host '{name}' has no CargoStorageDB at all.");
                    Assert.That(storage.TypeStores.ContainsKey(FuelStoreTypeId), Is.True,
                        $"[{info.Abbreviation}] launch-complex host '{name}' has NO '{FuelStoreTypeId}' store — a launch would " +
                        "fail TryDeductFuel's ContainsKey check (the A4 trap). Install a fuel-storage component (e.g. a fuel farm).");

                    // Precondition 2 — there is actually fuel in that store (positive stored units → positive mass).
                    var store = storage.TypeStores[FuelStoreTypeId];
                    long fuelUnits = 0;
                    foreach (var units in store.CurrentStoreInUnits.Values)
                        fuelUnits += units;
                    Assert.That(fuelUnits, Is.GreaterThan(0),
                        $"[{info.Abbreviation}] launch-complex host '{name}' has a '{FuelStoreTypeId}' store but it is EMPTY — " +
                        "a launch would deduct 0 fuel and TryDeductFuel returns false (the ship never leaves the pad). " +
                        "Author fuel cargo (e.g. methalox/ntp) on this colony.");

                    TestContext.Progress.WriteLine(
                        $"[launch-fuel] {info.Abbreviation} '{name}': fuel-storage store OK, {fuelUnits:N0} fuel units stocked.");
                }
            }

            // Non-vacuous: the sandbox must contain at least one launch-complex host, or this gauge proves nothing.
            // (UMF's Mars owns the only launch complex in the base DevTest sandbox.)
            Assert.That(launchHostsChecked, Is.GreaterThan(0),
                "No launch-complex host was found in the DevTest sandbox — the fixture asserted nothing. If the scenario " +
                "changed, point it at a scenario that fields a launch complex.");
        }

        /// <summary>Every valid host (colony + station) owned by a faction — the two parallel registries.</summary>
        private static IEnumerable<Entity> HostsOf(FactionInfoDB info)
        {
            foreach (var c in info.Colonies)
                if (c != null && c.IsValid) yield return c;
            foreach (var st in info.Stations)
                if (st != null && st.IsValid) yield return st;
        }
    }
}
