using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Industry;
using Pulsar4X.Modding;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The DevTest game-start gauge (the "DevTest" button that replaces Quickstart). Proves the data-driven start
    /// stands up end-to-end through the WORKING pieces this branch built:
    ///   Sol via StarSystemFactory.LoadFromBlueprint  →  DevTestStartFactory.CreateDevTest  →
    ///   FactionFactory.LoadFromJson (design/species BY ID + the "startingItems" unlock + the inline colony/station parser).
    /// The first fixture loads the PLAYER faction alone, so a gotcha-#10 failure (a design/species/body id that
    /// doesn't resolve) is isolated to one file. The second loads the WHOLE conquest sandbox (UEF + United Martian
    /// Federation + Kithrin Collective) and asserts the scenario's shape: an inner-system war, war-strain on the
    /// aggressor's colonies, and the Kithrin's outer-system station.
    /// </summary>
    [TestFixture]
    public class DevTestScenarioTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

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

        [Test]
        [Description("The DevTest player faction (UEF) loads from JSON with its Earth colony and its full startingItems "
                     + "unlock — everything ENABLED to design/build, nothing pre-built. Exercises the modernized "
                     + "FactionFactory.LoadFromJson (designs by id, startingItems unlock, inline colony parser).")]
        public void DevTest_PlayerFaction_LoadsWithColonyAndUnlocks()
        {
            var game = NewGame();

            var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json" });

            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");
            Assert.That(player.IsValid, Is.True, "player faction is not valid.");
            Assert.That(startingSystemId, Is.Not.Null.And.Not.Empty, "no starting system id returned.");

            var info = player.GetDataBlob<FactionInfoDB>();
            Assert.That(info, Is.Not.Null, "player faction has no FactionInfoDB.");
            Assert.That(info.Colonies.Count, Is.GreaterThan(0), "player faction has no colony (Earth).");

            // The "startingItems" unlock ran: a listed material was unlocked into CargoGoods AND synced into
            // IndustryDesigns (what makes it buildable). If this is empty, the unlock pass didn't run.
            Assert.That(info.IndustryDesigns.Count, Is.GreaterThan(0),
                "startingItems unlock produced no buildable IndustryDesigns.");
            Assert.That(info.IndustryDesigns.ContainsKey("stainless-steel"), Is.True,
                "a startingItems material (stainless-steel) was not unlocked into IndustryDesigns — the unlock pass "
                + "or the material sync didn't run.");
        }

        [Test]
        [Description("The WHOLE DevTest conquest sandbox loads: UEF (player) + United Martian Federation (NPC, inner-system "
                     + "war economy) + Kithrin Collective (NPC, outer-system developed station). Asserts the scenario's "
                     + "shape — three factions, the UMF authored as an NPC at war with the player with war-strain on its "
                     + "colonies, and the Kithrin holding an outer-system station. This is the gotcha-#10 sensor for the "
                     + "NPC files (war/strain/station parsing) the way the player test is for the player file.")]
        public void DevTest_FullSandbox_ThreeFactionsWarStrainAndStation()
        {
            var game = NewGame();

            var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");
            var playerInfo = player.GetDataBlob<FactionInfoDB>();
            Assert.That(playerInfo.IsNPC, Is.False, "the player faction (first file) should not be an NPC.");

            // Collect every loaded faction's info blob. Classify the two NPCs by their authored shape rather than by
            // name: the UMF is the NPC with the inner-system colony cluster; the Kithrin is the NPC with a station.
            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .ToList();

            Assert.That(infos.Count(i => i.IsNPC), Is.GreaterThanOrEqualTo(2),
                "expected at least two NPC factions (UMF + Kithrin) loaded from JSON.");

            var umf = infos.FirstOrDefault(i => i.IsNPC && i.Colonies.Count >= 4);
            Assert.That(umf, Is.Not.Null,
                "the United Martian Federation (an NPC with its four inner-system colonies) did not load.");

            var kithrin = infos.FirstOrDefault(i => i.IsNPC && i.Stations.Count > 0);
            Assert.That(kithrin, Is.Not.Null,
                "the Kithrin Collective (an NPC with an outer-system station) did not load — the 'stations' parser "
                + "or the station's modules didn't resolve.");

            // The UMF opened the game already at war with the player (openingRelations, applied second-pass).
            var umfEntity = umf.OwningEntity;
            Assert.That(umfEntity, Is.Not.Null, "UMF FactionInfoDB has no owning entity.");
            var umfDiplomacy = umfEntity.GetDataBlob<DiplomacyDB>();
            Assert.That(umfDiplomacy.GetRelationship(player.Id).AtWar, Is.True,
                "the UMF should have opened the game at war with the player (openingRelations atWar).");

            // The war-strain landed: the UMF's colonies carry the authored high war-tax (ApplyOpeningStrain sets the
            // INPUT the economy processor reads, so the strain sticks and degrades morale over time).
            var strainedColony = umf.Colonies.FirstOrDefault(c =>
                c != null && c.IsValid && c.HasDataBlob<ColonyEconomyDB>()
                && c.GetDataBlob<ColonyEconomyDB>().TaxRate > 0.0);
            Assert.That(strainedColony, Is.Not.Null,
                "no UMF colony carries the authored war-tax strain — ApplyOpeningStrain didn't run or found no economy blob.");
        }

        [Test]
        [Description("DevTest colony worlds start DEFENDED: after the full sandbox loads, the UMF's colony worlds (and the "
                     + "player's Earth) carry a GroundForcesDB with their owner's home garrison — so an invasion is a real "
                     + "fight, not an unopposed capture, and the AI's own worlds aren't free for the taking. The garrison "
                     + "is the ground echo of the authored fleets, raised for every DevTest faction (unlike the barebones "
                     + "New Game). Also the gauge that the DevTest Sol generates the region maps the garrison needs.")]
        public void DevTest_ColonyWorlds_StartWithAHomeGarrison()
        {
            var game = NewGame();
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>()).ToList();
            var umf = infos.First(i => i.IsNPC && i.Colonies.Count >= 4);
            int umfId = umf.OwningEntity.Id;

            // Every UMF colony body should carry a GroundForcesDB with UMF-owned units (its home garrison).
            int garrisonedBodies = 0, umfUnits = 0;
            foreach (var colony in umf.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                var body = colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
                if (body != null && body.IsValid && body.TryGetDataBlob<GroundForcesDB>(out var forces))
                {
                    int mine = forces.Units.Count(u => u.FactionOwnerID == umfId);
                    if (mine > 0) { garrisonedBodies++; umfUnits += mine; }
                }
            }
            Assert.That(garrisonedBodies, Is.GreaterThan(0),
                "no UMF colony world carries a home garrison — RaiseForFactionColonies didn't run or the bodies have no region map.");
            Assert.That(umfUnits, Is.GreaterThanOrEqualTo(6),
                "expected at least one UMF world's combined-arms garrison (3 inf + 2 armor + 1 arty = 6).");

            // The player's Earth is defended too, so a UMF invasion (once the AI can land troops) meets resistance.
            var earthBody = player.GetDataBlob<FactionInfoDB>().Colonies
                .Where(c => c != null && c.IsValid)
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity)
                .FirstOrDefault(b => b != null && b.IsValid);
            Assert.That(earthBody, Is.Not.Null, "player has no colony body.");
            Assert.That(earthBody.TryGetDataBlob<GroundForcesDB>(out var earthForces)
                && earthForces.Units.Any(u => u.FactionOwnerID == player.Id), Is.True,
                "the player's Earth should start with a home garrison in the DevTest.");
        }

        [Test]
        [Description("B5-1: the UMF can build a TROOP TRANSPORT — the ship that lifts ground units to an invasion. The "
                     + "base mod had no ship mounting a troop-bay, so the AI (or player) could never carry troops off-world. "
                     + "Asserts UMF's ShipDesigns holds 'default-ship-design-trooper' (so every component id resolved — the "
                     + "gotcha-#10 sensor for a scenario ship design) AND that it mounts a GroundBayAtb (Personnel carry room) "
                     + "so it can actually lift infantry. The prerequisite for the B5 conquest loop's load/land step.")]
        public void DevTest_UMF_CanBuildATroopTransport_ThatCarriesABay()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umf = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .First(i => i.IsNPC && i.Colonies.Count >= 4);

            Assert.That(umf.ShipDesigns.ContainsKey("default-ship-design-trooper"), Is.True,
                "UMF can't build the troop transport — the ship design didn't load (a component id didn't resolve, "
                + "or it's missing from UMF's shipDesigns list).");

            var trooper = umf.ShipDesigns["default-ship-design-trooper"];
            Assert.That(trooper.TryGetComponentsByAttribute<GroundBayAtb>(out var bays) && bays.Count > 0, Is.True,
                "the troop transport mounts no GroundBayAtb — it can't actually carry ground units.");
        }

        [Test]
        [Description("B5-2 helpers: ConquerResolver.IsTroopTransport recognises the trooper (mounts a bay) and rejects the "
                     + "gunship (no bay); and FactionOwnsTransport is FALSE for UMF right after load — it has the trooper "
                     + "DESIGN but no transport SHIP yet (its start fleet is gunships). These are the two reads the "
                     + "BuildTransport rung turns on.")]
        public void DevTest_ConquerResolver_TransportDetection_Helpers()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
            var umf = umfEntity.GetDataBlob<FactionInfoDB>();

            Assert.That(ConquerResolver.IsTroopTransport(umf.ShipDesigns["default-ship-design-trooper"]), Is.True,
                "the trooper mounts a troop bay → IsTroopTransport true");
            Assert.That(ConquerResolver.IsTroopTransport(umf.ShipDesigns["default-ship-design-gunship"]), Is.False,
                "the gunship has no troop bay → IsTroopTransport false");

            var state = FactionState.Snapshot(umfEntity);
            Assert.That(ConquerResolver.FactionOwnsTransport(state), Is.False,
                "UMF owns the trooper DESIGN but no transport SHIP yet (its start fleet is gunships) → false");
        }

        [Test]
        [Description("B5-2: the DECISION INPUTS of the BuildTransport rung are all satisfied for an at-war UMF — it holds "
                     + "a scored enemy target, the trooper is a buildable design in IndustryDesigns, and it owns no "
                     + "transport ship yet. So the rung's LOGIC selects BuildTransport whenever the economy permits the "
                     + "queue. (The actual queue also passes FeasibilityOracle.CanQueue — see the flagged UMF ship-build "
                     + "economy note in DEVTEST-AI-BUILD-LOG.md: a crowded hostile Mars may compute infra-efficiency below "
                     + "the 1/tick capacity floor, which would gate ALL ship builds. That economy check is decoupled here "
                     + "so this gauge stays a robust test of the rung's wiring, not of Mars's infrastructure.)")]
        public void DevTest_UMF_AtWar_HasTheInputsToBuildATransport()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
            var umf = umfEntity.GetDataBlob<FactionInfoDB>();

            // Input 1: a scored enemy target (UMF opened at war with the player, who holds Earth).
            Assert.That(MilitaryTarget.BestEnemyTarget(umfEntity).IsValid, Is.True,
                "UMF (at war with the player) should score a valid enemy target world.");

            // Input 2: the transport is a BUILDABLE design (in IndustryDesigns), and it reads as a troop carrier.
            Assert.That(umf.IndustryDesigns.ContainsKey("default-ship-design-trooper"), Is.True,
                "the trooper transport should be a buildable IndustryDesign on UMF.");
            Assert.That(ConquerResolver.IsTroopTransport((Pulsar4X.Ships.ShipDesign)umf.IndustryDesigns["default-ship-design-trooper"]),
                Is.True, "the buildable trooper reads as a troop transport (mounts a bay).");

            // Input 3: UMF owns no transport SHIP yet → the rung's "build one" condition holds.
            Assert.That(ConquerResolver.FactionOwnsTransport(FactionState.Snapshot(umfEntity)), Is.False,
                "UMF owns no transport ship yet, so the BuildTransport rung's precondition holds.");
        }

        [Test]
        [Description("MARS-AS-CAPITAL + the ship-build fix (the developer's 'develop up Mars … give it gas', and the "
                     + "resolution of the flagged 'UMF may not be able to build ships' finding). The UMF capital Mars is "
                     + "now sized as a real war homeworld — many shipyards/foundries/mines/refineries, research labs + "
                     + "universities, an intelligence directorate, a governance ministry — with ENOUGH infrastructure to "
                     + "run it near full efficiency. This is the live test of the colony-infrastructure system: it asserts "
                     + "(1) Mars runs at high infra efficiency (provided ≥ required), (2) its shipyards provide a real "
                     + "ship-assembly production line, and (3) FeasibilityOracle.CanQueue now PASSES for both a warship and "
                     + "the troop transport — so the AI can actually build the navy and the invasion carrier a conquest "
                     + "needs. Prints the gauge readings so a red run names the exact gate (efficiency / line / CanQueue).")]
        public void DevTest_UMF_CapitalMars_RunsAtHighEfficiency_AndCanBuildShips()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
            var umf = umfEntity.GetDataBlob<FactionInfoDB>();

            // Mars is the capital: the UMF colony with the largest population (120 M — far above Luna/Venus/Ceres).
            var mars = umf.Colonies.Where(c => c != null && c.IsValid)
                .OrderByDescending(c => c.GetDataBlob<ColonyInfoDB>().Population.Values.Sum())
                .First();

            // Gauge 1 — the infrastructure system: the capital is sized to run near full (provided ≥ required → 1.0).
            Assert.That(mars.TryGetDataBlob<InfrastructureDB>(out var infra), Is.True,
                "Mars has no InfrastructureDB — the recalc didn't run on load.");
            TestContext.WriteLine($"[mars-infra] provided={infra.CapacityProvided:N0} required={infra.CapacityRequired:N0} "
                + $"efficiency={infra.Efficiency:F3}");
            Assert.That(infra.Efficiency, Is.GreaterThan(0.9),
                $"Mars capital efficiency {infra.Efficiency:F3} is low — its infrastructure is undersized for the "
                + "industry placed on it (raise default-design-infrastructure amount in umf.json).");

            // Gauge 2 — the shipyards yield a ship-assembly line (the industry the conquest loop mass-produces on).
            Assert.That(mars.TryGetDataBlob<IndustryAbilityDB>(out var industry), Is.True,
                "Mars has no IndustryAbilityDB — no installation created a production line.");
            int shipAssemblyLines = 0, totalAssemblyRate = 0;
            foreach (var line in industry.ProductionLines.Values)
                if (line.IndustryTypeRates.TryGetValue("ship-assembly", out var r)) { shipAssemblyLines++; totalAssemblyRate += r; }
            TestContext.WriteLine($"[mars-industry] lines={industry.ProductionLines.Count} "
                + $"ship-assembly-lines={shipAssemblyLines} total-assembly-rate={totalAssemblyRate}");
            Assert.That(shipAssemblyLines, Is.GreaterThan(0),
                "no shipyard ship-assembly line on Mars — the capital can't assemble ships.");

            // Gauge 3 — the fix: FeasibilityOracle.CanQueue passes for a warship AND the troop transport on Mars.
            Assert.That(umf.IndustryDesigns.ContainsKey("default-ship-design-gunship"), Is.True,
                "the gunship isn't a buildable IndustryDesign on UMF.");
            Assert.That(umf.IndustryDesigns.ContainsKey("default-ship-design-trooper"), Is.True,
                "the trooper isn't a buildable IndustryDesign on UMF.");
            var marsState = ColonyState.Of(mars);
            bool canGunship = FeasibilityOracle.CanQueue(marsState, umf.IndustryDesigns["default-ship-design-gunship"], umf);
            bool canTrooper = FeasibilityOracle.CanQueue(marsState, umf.IndustryDesigns["default-ship-design-trooper"], umf);
            TestContext.WriteLine($"[mars-canqueue] gunship={canGunship} trooper={canTrooper}");
            Assert.That(canGunship, Is.True,
                "UMF Mars cannot queue a WARSHIP — the ship-build gate (crew/capacity after infra scaling) still blocks it.");
            Assert.That(canTrooper, Is.True,
                "UMF Mars cannot queue the TROOP TRANSPORT — the invasion carrier is unbuildable, so the conquest loop stalls.");
        }

        [Test]
        [Description("PROPORTIONAL KITHRIN (the developer's 'the same logic should PROPORTIONALLY apply to the Kithrin'). "
                     + "The Kithrin Collective's Titan outpost is a developed alien capital-in-miniature: its own foundries, "
                     + "deep bores, research labs + a Collective Nexus university, an intelligence directorate, and a naval "
                     + "yard. Asserts the station loaded with a real industrial base — an IndustryAbilityDB with a "
                     + "ship-assembly line from its shipyard — proving the proportional buildout's modules resolved and "
                     + "wired (the gotcha-#10 sensor for the Kithrin capital designs).")]
        public void DevTest_Kithrin_Titan_IsAProportionalDevelopedCapital()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var kithrin = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .First(i => i.IsNPC && i.Stations.Count > 0);

            var titan = kithrin.Stations.First(s => s != null && s.IsValid);
            Assert.That(titan.TryGetDataBlob<IndustryAbilityDB>(out var industry), Is.True,
                "the Kithrin Titan station has no IndustryAbilityDB — its industry modules didn't wire.");
            int shipAssemblyLines = industry.ProductionLines.Values
                .Count(l => l.IndustryTypeRates.ContainsKey("ship-assembly"));
            TestContext.WriteLine($"[kithrin-industry] lines={industry.ProductionLines.Count} ship-assembly-lines={shipAssemblyLines}");
            Assert.That(industry.ProductionLines.Count, Is.GreaterThan(3),
                "the Kithrin outpost has too few production lines — the proportional industrial buildout didn't land.");
            Assert.That(shipAssemblyLines, Is.GreaterThan(0),
                "the Kithrin outpost has no ship-assembly line — its naval yard module didn't resolve.");
        }

        [Test]
        [Description("KITHRIN AT THE SAME LEVEL (proportionally) — the alien Collective is now a full MILITARY faction, "
                     + "not just a developed economy. It fields its own ALIEN warship (Hive Cruiser — disruptors + "
                     + "deflector shields) and troop carrier (Spore Lander), a Titan home fleet, and buildable ground "
                     + "forces. The load-bearing engine change: the AI can now ACT FROM A STATION — FactionState.Snapshot "
                     + "includes a station carrying an industry line as a build host, so a station-only faction's "
                     + "economy/conquest AI is no longer inert (it used to snapshot an empty colony list and no-op). "
                     + "Asserts the alien ships loaded (gotcha-#10 sensor), the Spore Lander lifts troops, the snapshot "
                     + "includes the station, and FeasibilityOracle.CanQueue PASSES for the Hive Cruiser on the station — "
                     + "Kithrin builds its navy exactly as UMF builds from Mars.")]
        public void DevTest_Kithrin_IsAFullMilitaryFaction_ThatCanBuildFromItsStation()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var kithrinEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Stations.Count > 0);
            var kithrin = kithrinEntity.GetDataBlob<FactionInfoDB>();

            // The alien ships loaded (every alien component id resolved) and the lander can lift troops.
            Assert.That(kithrin.ShipDesigns.ContainsKey("default-ship-design-hive-cruiser"), Is.True,
                "the Kithrin Hive Cruiser didn't load — an alien component id didn't resolve.");
            Assert.That(kithrin.ShipDesigns.ContainsKey("default-ship-design-spore-lander"), Is.True,
                "the Kithrin Spore Lander (troop carrier) didn't load.");
            Assert.That(kithrin.ShipDesigns["default-ship-design-spore-lander"]
                    .TryGetComponentsByAttribute<GroundBayAtb>(out var bays) && bays.Count > 0, Is.True,
                "the Spore Lander carries no troop bay — Kithrin can't lift an invasion.");

            // The AI snapshot now includes the STATION as a build host (was empty for a station-only faction).
            var state = FactionState.Snapshot(kithrinEntity);
            Assert.That(state, Is.Not.Null, "Kithrin snapshot is null.");
            Assert.That(state.Colonies.Count, Is.GreaterThan(0),
                "FactionState.Snapshot excluded the Kithrin station — its economy/conquest AI would be inert.");

            // Kithrin can BUILD its warship from the station (parity with UMF building from Mars).
            Assert.That(kithrin.IndustryDesigns.ContainsKey("default-ship-design-hive-cruiser"), Is.True,
                "the Hive Cruiser isn't a buildable IndustryDesign on Kithrin.");
            var station = state.Colonies.First();
            bool canBuildCruiser = FeasibilityOracle.CanQueue(
                station, kithrin.IndustryDesigns["default-ship-design-hive-cruiser"], kithrin);
            TestContext.WriteLine($"[kithrin-canqueue] hive-cruiser={canBuildCruiser} snapshot-hosts={state.Colonies.Count}");
            Assert.That(canBuildCruiser, Is.True,
                "Kithrin cannot queue its warship from the station — the station-aware snapshot or the ship-assembly line regressed.");
        }

        [Test]
        [Description("B5-3 (the conquest keystone, LOAD rung): with a UMF troop transport sitting at Mars — which holds a "
                     + "UMF home garrison — the three reads the ConquerResolver LOAD rung turns on are all satisfied: "
                     + "FindOwnedTransport returns the transport, it's AT Mars with free troop-bay room, and "
                     + "AvailableGroundUnitAt finds a loadable garrison unit. So the rung selects LoadInvasion whenever the "
                     + "resolve reaches it. Decoupled from the strike-fleet priority (the B5-2 lesson — assert the rung's "
                     + "INPUTS, not a priority-gated outcome). This is the first rung of load→sail→land→capture; sailing the "
                     + "loaded transport and landing it are the next slices.")]
        public void DevTest_UMF_Invasion_HasTheInputsToLoadTroops()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
            var umf = umfEntity.GetDataBlob<FactionInfoDB>();

            // UMF owns no transport at the start (its fleet is gunships) — the rung's precondition to FILL.
            Assert.That(ConquerResolver.FindOwnedTransport(FactionState.Snapshot(umfEntity)), Is.Null,
                "UMF should own no transport ship at start.");

            // Mars = the largest-pop UMF colony; it carries a home garrison (raised by the DevTest start).
            var mars = umf.Colonies.Where(c => c != null && c.IsValid)
                .OrderByDescending(c => c.GetDataBlob<ColonyInfoDB>().Population.Values.Sum())
                .First();
            var marsBody = mars.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            Assert.That(marsBody != null && marsBody.IsValid, Is.True, "Mars has no planet body.");

            // Spawn a UMF troop transport at Mars — the ship the AI would have built (BuildTransport rung).
            var trooperDesign = (ShipDesign)umf.ShipDesigns["default-ship-design-trooper"];
            var trooper = ShipFactory.CreateShip(trooperDesign, umfEntity, marsBody, "MFS Test Lander");
            Assert.That(trooper != null && trooper.IsValid, Is.True, "failed to spawn the test transport.");

            // Input 1: FindOwnedTransport now returns the transport (the rung's precondition is met).
            var state = FactionState.Snapshot(umfEntity);
            Assert.That(ConquerResolver.FindOwnedTransport(state), Is.Not.Null,
                "FindOwnedTransport should return the spawned troop transport.");

            // Input 2: the transport is AT Mars with free Personnel bay room.
            Assert.That(GroundTransport.ShipIsAtBody(trooper, marsBody), Is.True, "the transport should be at Mars.");
            Assert.That(GroundTransport.FreeCapacity(trooper, GroundCarryClass.Personnel), Is.GreaterThan(0),
                "the transport should have free troop-bay room.");

            // Input 3: Mars has a garrison unit the TROOPER can actually load (CanLoad-filtered — a Personnel unit its
            // troop bay has room for, NOT the Vehicle armour/artillery it can't carry). This is the LOAD rung's third read.
            Assert.That(ConquerResolver.AvailableLoadableUnit(trooper, marsBody, umfEntity.Id), Is.Not.Null,
                "Mars should hold a garrison unit the troop transport can load (an infantry unit).");
        }
    }
}
