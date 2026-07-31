using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.Energy;
using Pulsar4X.Engine.Factories;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.GeoSurveys;
using Pulsar4X.JumpPoints;
using Pulsar4X.Logistics;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Orbits;
using Pulsar4X.People;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;
using Pulsar4X.Storage;
using Pulsar4X.Technology;
using Pulsar4X.Weapons;

namespace Pulsar4X.Engine
{
    /// <summary>
    /// The DevTest game-start orchestrator — the engine half of the "DevTest" button that replaces Quickstart.
    ///
    /// It stands the scenario up from WORKING pieces on TODAY's data, deliberately NOT reusing
    /// <see cref="DefaultStartFactory"/>.LoadFromJson (whose SYSTEM loader is bit-rotted — it reads a
    /// <c>sol/systemInfo.json</c> that no longer exists, since Sol is now a blueprint in the mod store):
    ///  1. Load Sol via the LIVE blueprint path (<see cref="StarSystemFactory.LoadFromBlueprint"/>) — exactly what
    ///     <c>NewGameMenu.CreateGameCore</c> uses.
    ///  2. Load each faction from its JSON via the modernized <see cref="FactionFactory.LoadFromJson"/> — which resolves
    ///     component/ship designs + species BY ID from the consolidated mod store, unlocks a faction-level
    ///     <c>startingItems</c> list ("everything enabled"), and parses <c>stations</c>/<c>colonies</c>/<c>fleets</c>.
    ///  3. Second pass: apply each faction's <c>openingRelations</c> (opening war) + <c>strain</c> once every faction
    ///     exists (a faction's opening relations name OTHER factions by name/abbr).
    ///  4. Third pass: raise each faction's HOME GARRISON so colony worlds start DEFENDED (a war scenario needs a
    ///     defended planet to take — the ground echo of the authored fleets; NOT done in the barebones New Game).
    ///  5. Kick the energy + sensor processors so power + first-contacts/surveys are live at t=0.
    ///
    /// The player faction is the FIRST file listed. Returns (player, startingSystemId) for the client to activate.
    /// Flipping the NPC AI action gates ("everything enabled" for the brain) is the CALLER's job (client-only static
    /// flags), kept out of here so the CI test can load the scenario without turning the AI loose.
    /// </summary>
    public static class DevTestStartFactory
    {
        public static (Entity? playerFaction, string startingSystemId) CreateDevTest(Game game, string scenarioDir, List<string> factionFiles)
        {
            // Designs authored in the scenario are pre-researched (this is a start, not mid-game progress).
            ComponentDesigner.StartResearched = true;

            // Sol via the live blueprint path (the sol/ folder has no systemInfo.json for the legacy JSON loader).
            // The System blueprint's UniqueID is "system-sol" (the bare "sol" is the STAR's id) — and that same id
            // becomes the runtime StarSystem.ID/ManagerID, so a scenario colony/station references it as
            // "systemId": "system-sol".
            var startingSystem = StarSystemFactory.LoadFromBlueprint(game, game.StartingGameData.Systems["system-sol"]);

            Entity? playerFaction = null;
            var loadedFactions = new List<(Entity faction, string path)>();
            foreach (var file in factionFiles)
            {
                var path = Path.Combine(scenarioDir, file);
                var faction = FactionFactory.LoadFromJson(game, path);
                faction.FactionOwnerID = faction.Id;
                faction.GetDataBlob<FactionInfoDB>().KnownSystems.Add(startingSystem.ID);
                loadedFactions.Add((faction, path));

                // First faction listed is the player (mirrors DefaultStartFactory.LoadFromJson's convention).
                if (playerFaction == null)
                    playerFaction = faction;
            }

            // Second pass — opening diplomacy + strain, now that every faction exists so cross-faction names resolve.
            var openingWhen = game.TimePulse.GameGlobalDateTime;
            foreach (var (faction, path) in loadedFactions)
            {
                var root = JObject.Parse(File.ReadAllText(path));

                var openingNode = root["openingRelations"];
                if (openingNode != null)
                    FactionFactory.ApplyOpeningRelations(game, faction, openingNode, openingWhen);

                var strainNode = root["strain"];
                if (strainNode != null)
                    FactionFactory.ApplyOpeningStrain(faction, strainNode);
            }

            // LOCATE the PLAYER's colony onto its planet surface so the city RENDERS: the DevTest colony path
            // (ColonyFactory.CreateColony) reveals the regions but never drops the installations into a home hex — the
            // step the New-Game path (ColonyFactory.CreateFromBlueprint:65-73,124,128) does. Without it the buildings
            // exist in ComponentInstancesDB but have no home, so the planet/city view draws terrain but no CITY (the
            // developer's "no city, just units in the ocean"). Done PLAYER-ONLY and HERE (not in FactionFactory for
            // every colony): the developer wants THEIR city to render, and generating the fine hex/terrain patches for
            // every NPC world (Mars/Luna/Venus/Ceres) on every DevTest load is a large, needless cost (it doubled the
            // CI test-shard time). NPC worlds keep the region map their garrison needs; their fine hexes stay lazy.
            // All calls are idempotent/defensive (a body with no region layer simply skips).
            if (playerFaction != null)
            {
                foreach (var colony in playerFaction.GetDataBlob<FactionInfoDB>().Colonies)
                {
                    if (colony == null || !colony.IsValid) continue;
                    var body = colony.GetDataBlob<Pulsar4X.Colonies.ColonyInfoDB>().PlanetEntity;
                    if (body == null || !body.IsValid) continue;
                    if (body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var homeRegions))
                    {
                        foreach (var r in homeRegions.Regions) r.OwnerFactionID = playerFaction.Id; // hold what you settle
                        Pulsar4X.Galaxy.PlanetHexFactory.EnsureHexesForBody(body);                  // lazy Planet→Region→Hex
                    }
                    Pulsar4X.GroundCombat.GroundInstallations.LocateColonyInstallations(colony); // installs → capital region (city draws)
                    Pulsar4X.GroundCombat.GroundBuildings.LocateFootprintsOnHexes(colony);       // forts → bombard/capture targets
                }
            }

            // Third pass — raise each NPC faction's HOME GARRISON so its colony worlds start DEFENDED. The DevTest is a
            // WAR scenario; an undefended NPC planet makes "take a planet" an unopposed walk-in. This is the ground echo
            // of the space fleets the scenario already authors ("everything enabled") — deliberately NOT done in the
            // barebones default New Game. The PLAYER is SKIPPED: they build their own ground forces through the
            // designer/assembler (the developer's "nothing pre-made through non-designer paths" — the "units in the
            // ocean" were exactly this code-built garrison). Idempotent + defensive: a faction with no region-mapped
            // colony body (e.g. the Kithrin's station-only holdings) simply raises nothing.
            foreach (var (faction, _) in loadedFactions)
            {
                if (faction == playerFaction) continue; // player designs + builds their own; no pre-made garrison
                Pulsar4X.GroundCombat.GroundStartGarrison.RaiseForFactionColonies(game, faction);
            }

            // Generate power and run the first sensor sweep so t=0 state is live (colonies power up, contacts/surveys
            // populate) — mirrors DefaultStartFactory.LoadFromJson's post-load processor kick.
            foreach (var entityItem in startingSystem.GetAllEntitiesWithDataBlob<EnergyGenAbilityDB>())
                game.ProcessorManager.GetInstanceProcessor(nameof(EnergyGenProcessor)).ProcessEntity(entityItem, game.TimePulse.GameGlobalDateTime);

            foreach (var entityItem in startingSystem.GetAllEntitiesWithDataBlob<SensorAbilityDB>())
                game.ProcessorManager.GetInstanceProcessor(nameof(SensorScan)).ProcessEntity(entityItem, game.TimePulse.GameGlobalDateTime);

            ComponentDesigner.StartResearched = false;
            return (playerFaction, startingSystem.ID);
        }
    }
}
