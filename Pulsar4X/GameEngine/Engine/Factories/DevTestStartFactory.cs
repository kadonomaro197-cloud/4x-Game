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
    ///  4. Kick the energy + sensor processors so power + first-contacts/surveys are live at t=0.
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
