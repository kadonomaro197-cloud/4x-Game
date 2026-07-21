using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Modding;
using Pulsar4X.Movement;
using Pulsar4X.Ships;
using Pulsar4X.Storage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE FIX GAUGE for task #36 — "the UMF invasion never lands." The diagnosis: a scenario-loaded fleet's ships
    /// (built by <see cref="FactionFactory"/>.LoadFromJson → <see cref="ShipFactory.CreateShip"/>) boot with ZERO
    /// stored reactor energy and EMPTY fuel tanks — by design (a production-built ship earns its charge/fuel at a
    /// colony). But a warp bubble is paid out of STORED electricity, so <see cref="MilitaryReach.FleetHasWarpRange"/>
    /// (the exact <c>WarpMoveCommand</c> gate: <c>EnergyStored[warp.EnergyType] >= warp.BubbleCreationCost</c>) reads
    /// FALSE for an un-charged fleet — which is precisely why the NPC conquest chain's massed strike fleet could never
    /// launch (ConquerResolver Rung 1's <c>reach.HasRange</c>). The player's start fleet doesn't hit this because
    /// <see cref="DefaultStartFactory"/> hand-charges it; a JSON-loaded NPC fleet never was.
    ///
    /// The fix mirrors the start setup in the fleet loader: <see cref="ShipFactory.ChargeReactors"/> +
    /// <see cref="ShipFactory.FillFuelTanks"/> right after <c>LoadCargo</c>. This test proves the postcondition on the
    /// real DevTest conquest sandbox: EVERY warp-capable NPC fleet ship boots with enough stored energy for a bubble
    /// (so <c>FleetHasWarpRange</c> reads true and the strike can sail), and its fuel tank is non-empty. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class NpcFleetReadyToSailTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";
        private static void Log(string m) => TestContext.Progress.WriteLine("[ready-to-sail] " + m);

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1, CreatePlayerFaction = false, DefaultSolStart = true, MasterSeed = 12345, EleStart = true
            });
        }

        [Test]
        [Description("Every warp-capable ship in the DevTest NPC fleets boots CHARGED (stored energy >= its warp-bubble "
                   + "cost) and FUELLED — so MilitaryReach.HasRange reads true and the conquest strike fleet can sail. "
                   + "The regression gauge for the FactionFactory charge/fuel fix (task #36).")]
        public void JsonLoadedNpcFleets_BootChargedAndFuelled_SoTheStrikeCanSail()
        {
            var game = NewGame();
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

            // Every warp-capable ship owned by an NPC faction (UMF/Kithrin) — across every star system.
            var npcIds = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().IsNPC)
                .Select(f => f.Id).ToHashSet();

            var warpShips = game.Systems
                .SelectMany(sys => sys.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                .Where(s => npcIds.Contains(s.FactionOwnerID)
                         && s.TryGetDataBlob<WarpAbilityDB>(out var w) && w.MaxSpeed > 0
                         && s.HasDataBlob<EnergyGenAbilityDB>())
                .ToList();

            Assert.That(warpShips.Count, Is.GreaterThan(0),
                "expected at least one warp-capable NPC fleet ship in the DevTest sandbox (the strike-fleet hulls).");

            int charged = 0;
            foreach (var ship in warpShips)
            {
                var warp = ship.GetDataBlob<WarpAbilityDB>();
                var power = ship.GetDataBlob<EnergyGenAbilityDB>();
                string eType = warp.EnergyType;
                double stored = (eType != null && power.EnergyStored.TryGetValue(eType, out var es)) ? es : 0;

                Assert.That(stored, Is.GreaterThanOrEqualTo(warp.BubbleCreationCost),
                    $"NPC warp ship '{ship.Id}' booted with {stored:0} stored energy — below its {warp.BubbleCreationCost:0} "
                    + "bubble cost. The fleet loader must ChargeReactors so the strike fleet can warp (task #36 regression).");
                charged++;
            }
            Log($"{charged}/{warpShips.Count} NPC warp-capable ships boot charged >= bubble cost — the strike can sail.");

            // At least one such ship also carries fuel (FillFuelTanks ran) — a fuelled fleet can maneuver, not just warp.
            var fuelled = warpShips.Count(s => s.TryGetDataBlob<Pulsar4X.Storage.CargoStorageDB>(out var cargo)
                                            && cargo.TotalStoredMass > 0);
            Log($"{fuelled}/{warpShips.Count} of those also carry cargo/fuel after FillFuelTanks.");
            Assert.That(fuelled, Is.GreaterThan(0),
                "no NPC warp ship carries any fuel/cargo after load — FillFuelTanks did not run on the JSON fleet loader.");
        }

        [Test]
        [Description("The P4.2 built-hull gauge (sibling to the scenario-loaded case above). A production-BUILT "
                   + "NPC hull boots CHARGED + fuelled (built-ship provisioning policy ON for NPCs) so the AI's "
                   + "home-built sealift can warp the instant it exists — while an IDENTICAL player-owned built hull "
                   + "is left un-charged + empty (policy OFF: the developer's 'a production ship earns its charge over "
                   + "game-time'). Proves the exact owner split ShipDesign.ProvisionBuiltShip enforces on both engine "
                   + "build paths (OnConstructionComplete + LaunchComplexProcessor.TryLaunchShip).")]
        public void BuiltShips_ChargedForNpc_NotForPlayer_PerProvisioningPolicy()
        {
            bool npcWas = ShipDesign.ChargeBuiltNpcShips;
            bool playerWas = ShipDesign.ChargeBuiltPlayerShips;
            try
            {
                // Pin the policy under test (the defaults) so this can't be perturbed by test ordering.
                ShipDesign.ChargeBuiltNpcShips = true;      // NPC-built hulls boot ready to fly
                ShipDesign.ChargeBuiltPlayerShips = false;  // player-built hulls earn their charge over time

                var game = NewGame();
                var (player, _) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                // Grab a real warp-capable NPC ship from the loaded sandbox — its DESIGN + orbited BODY let us
                // stand up FRESH hulls the exact way the industry build path does (ShipFactory.CreateShip leaves
                // stored energy at 0 + tanks empty, mirroring OnConstructionComplete BEFORE ProvisionBuiltShip
                // runs). Require a cargo bay so FillFuelTanks has somewhere to put the fuel.
                var npcIds = game.Factions.Values
                    .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().IsNPC)
                    .Select(f => f.Id).ToHashSet();

                var sample = game.Systems
                    .SelectMany(sys => sys.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                    .FirstOrDefault(s => npcIds.Contains(s.FactionOwnerID)
                                      && s.TryGetDataBlob<WarpAbilityDB>(out var w) && w.MaxSpeed > 0
                                      && s.HasDataBlob<EnergyGenAbilityDB>()
                                      && s.HasDataBlob<CargoStorageDB>());
                Assert.That(sample, Is.Not.Null,
                    "expected a warp-capable, cargo-carrying NPC ship in the DevTest sandbox to source a design + body from.");

                var npcFaction = game.Factions[sample.FactionOwnerID];
                var design = sample.GetDataBlob<ShipInfoDB>().Design;
                var body = sample.GetSOIParentEntity();
                Assert.That(body, Is.Not.Null, "sample NPC ship has no SOI-parent body to build fresh hulls at.");

                // Same hull design, two owners — the ONLY variable is faction ownership, so any difference is
                // the provisioning policy, not the hull.
                var npcBuilt = ShipFactory.CreateShip(design, npcFaction, body!);
                var playerBuilt = ShipFactory.CreateShip(design, player, body!);

                // Both boot un-charged (stored energy < their warp-bubble cost) straight out of CreateShip —
                // the true post-construction state ProvisionBuiltShip is handed.
                Assert.That(StoredEnergy(npcBuilt), Is.LessThan(BubbleCost(npcBuilt)),
                    "a freshly-built hull should boot below its bubble cost (CreateShip leaves stored energy at 0).");
                Assert.That(StoredEnergy(playerBuilt), Is.LessThan(BubbleCost(playerBuilt)),
                    "a freshly-built hull should boot below its bubble cost (CreateShip leaves stored energy at 0).");

                // Apply the EXACT policy call both engine build paths make.
                ShipDesign.ProvisionBuiltShip(npcBuilt, npcFaction);
                ShipDesign.ProvisionBuiltShip(playerBuilt, player);

                Log($"NPC built hull:    stored={StoredEnergy(npcBuilt):0} / bubble={BubbleCost(npcBuilt):0} / cargo={CargoMass(npcBuilt):0}");
                Log($"player built hull: stored={StoredEnergy(playerBuilt):0} / bubble={BubbleCost(playerBuilt):0} / cargo={CargoMass(playerBuilt):0}");

                Assert.That(StoredEnergy(npcBuilt), Is.GreaterThanOrEqualTo(BubbleCost(npcBuilt)),
                    "an NPC-owned production-built hull must boot CHARGED (>= its bubble cost) — the sealift can't sail otherwise (P4.2).");
                Assert.That(CargoMass(npcBuilt), Is.GreaterThan(0),
                    "an NPC-owned built hull should be fuelled — FillFuelTanks ran under the ON policy (P4.2).");

                Assert.That(StoredEnergy(playerBuilt), Is.LessThan(BubbleCost(playerBuilt)),
                    "a player-owned production-built hull must NOT be auto-charged — the developer's 'earn the charge over time' policy (P4.2).");
                Assert.That(CargoMass(playerBuilt), Is.EqualTo(0),
                    "a player-owned built hull should have empty tanks — the OFF policy skips FillFuelTanks (P4.2).");
            }
            finally
            {
                ShipDesign.ChargeBuiltNpcShips = npcWas;
                ShipDesign.ChargeBuiltPlayerShips = playerWas;
            }
        }

        // How much stored electricity the ship holds in its warp-drive's energy type — the number the
        // WarpMoveCommand gate (EnergyStored >= BubbleCreationCost) reads. Mirrors the first test's pattern.
        private static double StoredEnergy(Entity ship)
        {
            var warp = ship.GetDataBlob<WarpAbilityDB>();
            var power = ship.GetDataBlob<EnergyGenAbilityDB>();
            string eType = warp.EnergyType;
            return (eType != null && power.EnergyStored.TryGetValue(eType, out var es)) ? es : 0;
        }

        private static double BubbleCost(Entity ship) => ship.GetDataBlob<WarpAbilityDB>().BubbleCreationCost;

        private static double CargoMass(Entity ship)
            => ship.TryGetDataBlob<CargoStorageDB>(out var cargo) ? cargo.TotalStoredMass : 0;
    }
}
