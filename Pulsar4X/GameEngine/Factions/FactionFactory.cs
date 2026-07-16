using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Auth;
using Pulsar4X.Engine.Factories;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Fleets;
using Pulsar4X.GeoSurveys;
using Pulsar4X.Interfaces;
using Pulsar4X.Names;
using Pulsar4X.People;
using Pulsar4X.Ships;
using Pulsar4X.Stations;
using Pulsar4X.Storage;
using Pulsar4X.Technology;
using Pulsar4X.Weapons;

namespace Pulsar4X.Factions
{

    public static class FactionFactory
    {
        /*
         *Stuff a faction needs to know:
         *name (nameDB)
         *password (AuthDB)
         *researched tech. (techDB)
         *
         *Owned Entites
         *
         *Sensor Contacts - these will be owned entites anyway.
         *  -System Bodies
         *  -Non Owned Entites
         *      -Colones
         *      -Ships
         *
         *Sensor Types
         *  - Grav, ie detecting anomalies in paths of known objects. - slow, but will find large dark planets.
         *  - Passive EM Spectrum:
         *      - Emited visable light (suns)
         *      - Reflected visable light (planets, moons)
         *      - Emitted IR (colonies, ship drives)
         *      - Reflected IR
         *      - Comms emmisions (colonies, ships)
         *  - Active EM:
         *      - Emmitting EM and looking for an echo. (radar)
         *
         * Owned Enties and Sensor Contacts need to be broken down by system.
         *
         *
         */

        public static Entity LoadFromJson(Game game, string filePath)
        {
            string fileContents = File.ReadAllText(filePath);
            var rootDirectory = (string?)Path.GetDirectoryName(filePath) ?? "Data/basemod/";
            var rootJson = JObject.Parse(fileContents);

            var name = rootJson["name"].ToString();
            var faction = CreateFaction(game, name);
            var factionInfoDB = faction.GetDataBlob<FactionInfoDB>();
            var factionDataStore = factionInfoDB.Data;

            // CreateFaction (unlike CreateBasicFaction) leaves Abbreviation empty — but the UI/readouts key off the
            // abbreviation (the [VIEW] log, the diplomacy ledger, faction labels), so a scenario faction printed as
            // "()". Read an optional "abbreviation" so a data-driven faction shows its short code like every other.
            var abbreviation = rootJson["abbreviation"]?.ToString();
            if (!string.IsNullOrEmpty(abbreviation))
                factionInfoDB.Abbreviation = abbreviation;

            if (rootJson["isNPC"] != null)
                factionInfoDB.IsNPC = rootJson["isNPC"].Value<bool>();

            var doctrineNode = rootJson["doctrine"];
            if (doctrineNode != null)
            {
                factionInfoDB.Doctrine = new DoctrineVector
                {
                    Economic  = doctrineNode["economic"]?.Value<float>()  ?? 0.25f,
                    Military  = doctrineNode["military"]?.Value<float>()  ?? 0.25f,
                    Tech      = doctrineNode["tech"]?.Value<float>()      ?? 0.25f,
                    Expansion = doctrineNode["expansion"]?.Value<float>() ?? 0.25f
                };
            }

            // Per-faction FLEET COMPOSITION: a scenario can author the ladder its AI grows fleets to (a Martian battle-
            // line vs a Kithrin raid-swarm) via a "fleetComposition" node { name, minToDeploy, idealSize, perfectSize }.
            // Absent → the FactionInfoDB defaults (3/8/18) stand, so every existing scenario is byte-identical. FleetAssembly
            // reads these back via TemplateFor(faction) when it forms a fleet.
            var fleetCompNode = rootJson["fleetComposition"];
            if (fleetCompNode != null)
            {
                factionInfoDB.FleetTemplateName = fleetCompNode["name"]?.Value<string>()      ?? factionInfoDB.FleetTemplateName;
                factionInfoDB.FleetMinToDeploy  = fleetCompNode["minToDeploy"]?.Value<int>()  ?? factionInfoDB.FleetMinToDeploy;
                factionInfoDB.FleetIdealSize    = fleetCompNode["idealSize"]?.Value<int>()    ?? factionInfoDB.FleetIdealSize;
                factionInfoDB.FleetPerfectSize  = fleetCompNode["perfectSize"]?.Value<int>()  ?? factionInfoDB.FleetPerfectSize;
            }

            // Phase 5.1a — AUTHORED PERSONALITY: a scenario can hand a faction its 12-trait identity (the model the whole
            // brain reads — retreat nerve, treaty tolerance, aggression, honour…). Only attach a PersonalityDB when the
            // scenario names one; with no "personality" node the faction carries none and every trait read falls back to
            // Neutral (0.5) exactly as today → byte-identical for every existing scenario file.
            var personalityNode = rootJson["personality"];
            if (personalityNode != null)
                faction.SetDataBlob(PersonalityFromJson(personalityNode));

            // DevTest (2026-07-13) — "everything ENABLED": a faction can author a STARTING-ITEMS unlock list (the same
            // kind of list earth.json uses) so the player can DESIGN and BUILD anything from turn one. LoadFromJson used
            // to unlock nothing (its colony path is the bare ColonyFactory.CreateColony, with no StartingItems pass like
            // CreateFromBlueprint has), so a faction had only the tech behind its pre-built designs. This mirrors that
            // unlock loop: unlock each id into CargoGoods, research any listed tech, and sync unlocked materials into
            // IndustryDesigns. Runs BEFORE componentDesigns so a design's ResourceCost materials are already unlocked
            // when ComponentDesigner reads them (gotcha #4). Byte-identical for a faction with no "startingItems" node.
            var startingItemsToLoad = (JArray?)rootJson["startingItems"];
            if(startingItemsToLoad != null)
            {
                foreach(var item in startingItemsToLoad)
                {
                    string id = item.ToString();
                    factionDataStore.Unlock(id);
                    if(factionDataStore.Techs.ContainsKey(id))
                        factionDataStore.IncrementTechLevel(id);
                    if(factionDataStore.CargoGoods.IsMaterial(id))
                        factionInfoDB.IndustryDesigns[id] = (IConstructableDesign)factionDataStore.CargoGoods[id];
                }
            }

            // DevTest (2026-07-13) — a faction can reference designs by ID from the consolidated mod store
            // (game.StartingGameData), the SAME store the live colony-blueprint path (ColonyFactory.CreateFromBlueprint)
            // resolves from. The old per-design files under componentDesigns/ were consolidated into designs/*.json, so
            // ID-by-store is the current, un-rotted way to author a faction (and the whole point of the "author factions
            // from data" base). Falls back to the legacy file/dir path when the entry isn't a known ID (backward
            // compatible). StartResearched is already true for the whole scenario load (set by DefaultStartFactory).
            var componentDesignsToLoad = (JArray?)rootJson["componentDesigns"];
            if(componentDesignsToLoad != null)
            foreach(var componentDesignToLoad in componentDesignsToLoad)
            {
                string entry = componentDesignToLoad.ToString();
                if(game.StartingGameData.ComponentDesigns.ContainsKey(entry))
                {
                    ComponentDesignFromJson.Create(faction, factionDataStore, game.StartingGameData.ComponentDesigns[entry]);
                    continue;
                }

                string fullPath = Path.Combine(rootDirectory, entry);
                if(Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
                    foreach(var file in files)
                        ComponentDesignFromJson.Create(faction, factionDataStore, file);
                }
                else
                {
                    ComponentDesignFromJson.Create(faction, factionDataStore, fullPath);
                }
            }

            var ordnanceDesignsToLoad = (JArray?)rootJson["ordnanceDesigns"];
            if(ordnanceDesignsToLoad != null)
            foreach(var ordnanceDesignToLoad in ordnanceDesignsToLoad)
            {
                string path = ordnanceDesignToLoad.ToString();
                string fullPath = Path.Combine(rootDirectory, path);

                if(Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
                    foreach(var file in files)
                    {
                        OrdnanceDesignFromJson.Create(faction, file);
                    }
                }
                else
                {
                    OrdnanceDesignFromJson.Create(faction, fullPath);
                }
            }

            // Ship designs: same ID-from-store-first, file-fallback pattern (a ship design references component designs
            // by id, so it MUST be authored after the componentDesigns above — which it is).
            var shipDesignsToLoad = (JArray?)rootJson["shipDesigns"];
            if(shipDesignsToLoad != null)
            foreach(var shipDesignToLoad in shipDesignsToLoad)
            {
                string entry = shipDesignToLoad.ToString();
                if(game.StartingGameData.ShipDesigns.ContainsKey(entry))
                {
                    ShipDesignFromJson.Create(faction, factionDataStore, game.StartingGameData.ShipDesigns[entry]);
                    continue;
                }

                string fullPath = Path.Combine(rootDirectory, entry);
                if(Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
                    foreach(var file in files)
                        ShipDesignFromJson.Create(faction, factionDataStore, file);
                }
                else
                {
                    ShipDesignFromJson.Create(faction, factionDataStore, fullPath);
                }
            }

            // Species: reference a species by ID from the mod store (the base mod ships species-human + species-xenos),
            // creating it via the blueprint path and linking it to the faction (FactionOwnerID + FactionInfoDB.Species),
            // exactly as CreateGameCore does. Falls back to the flat per-faction species FILE for the legacy form.
            var speciesToLoad = (JArray?)rootJson["species"];
            if(speciesToLoad != null)
            foreach(var toLoad in speciesToLoad)
            {
                string entry = toLoad.ToString();
                if(game.StartingGameData.Species.ContainsKey(entry))
                {
                    // CreateFromBlueprint hosts the species entity in a StarSystem (as CreateGameCore does); the
                    // scenario loads systems before factions, so the starting system exists. A species entity isn't
                    // location-bound (a colony in any system references it), matching the legacy GlobalManager form.
                    var hostSystem = game.Systems[0];
                    var speciesEntity = SpeciesFactory.CreateFromBlueprint(hostSystem, game.StartingGameData.Species[entry]);
                    speciesEntity.FactionOwnerID = faction.Id;
                    faction.GetDataBlob<FactionInfoDB>().Species.Add(speciesEntity);
                    continue;
                }

                string fullPath = Path.Combine(rootDirectory, entry);
                if(Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
                    foreach(var file in files)
                        SpeciesFactory.CreateFromJson(faction, game.GlobalManager, file);
                }
                else
                {
                    SpeciesFactory.CreateFromJson(faction, game.GlobalManager, fullPath);
                }
            }

            var coloniesToLoad = (JArray?)rootJson["colonies"];
            if(coloniesToLoad != null)
            {
                foreach(var colonyToLoad in coloniesToLoad)
                {
                    var systemId = colonyToLoad["systemId"].ToString();

                    var system = game.Systems.Find(s => s.ID.Equals(systemId));
                    if(system == null) throw new NullReferenceException("invalid systemId in json");
                    var location = NameLookup.GetFirstEntityWithName(system, colonyToLoad["location"].ToString());

                    // Mark the colony location as geo surveyed
                    if(location.TryGetDataBlob<GeoSurveyableDB>(out var geoSurveyableDB))
                    {
                        geoSurveyableDB.GeoSurveyStatus[faction.Id] = 0;
                    }

                    var speciesName = colonyToLoad["species"]["name"].ToString();
                    var species = faction.GetDataBlob<FactionInfoDB>().Species.Find(s => s.GetOwnersName().Equals(speciesName));
                    if(species == null) throw new NullReferenceException("invalid species name in json");
                    var population = (long?)colonyToLoad["species"]["population"] ?? 0;

                    var colony = ColonyFactory.CreateColony(faction, species, location, population);

                    var installationsToAdd = (JArray?)colonyToLoad["installations"];
                    if(installationsToAdd != null)
                    {
                        foreach(var install in installationsToAdd)
                        {
                            var installId = install["id"].ToString();
                            var amount = (int?)install["amount"] ?? 1;

                            colony.AddComponent(
                                factionInfoDB.InternalComponentDesigns[installId],
                                amount
                            );
                        }
                    }

                    LoadCargo(colony, factionDataStore, (JArray?)colonyToLoad["cargo"]);

                    //TODO: optionally set this from json
                    Scientist scientistEntity = CommanderFactory.CreateScientist(faction, colony);
                    colony.GetDataBlob<TeamsHousedDB>().AddTeam(scientistEntity);

                    ReCalcProcessor.ReCalcAbilities(colony);
                }
            }

            // DevTest (2026-07-13): a faction can be authored with SPACE STATIONS the same declarative way as
            // colonies (the Kithrin's outer-system developed outpost). Mirrors the colonies block above but hosts on
            // a body via StationFactory.CreateStation + AddComponent (the station is a generic chassis; its modules
            // ARE its "developed" capability). A station is manned only when a species + population are given, else it
            // is an automated platform. Additive/byte-identical: no existing scenario file has a "stations" node.
            var stationsToLoad = (JArray?)rootJson["stations"];
            if(stationsToLoad != null)
            {
                foreach(var stationToLoad in stationsToLoad)
                {
                    var systemId = stationToLoad["systemId"].ToString();
                    var system = game.Systems.Find(s => s.ID.Equals(systemId));
                    if(system == null) throw new NullReferenceException("invalid systemId in json");
                    var location = NameLookup.GetFirstEntityWithName(system, stationToLoad["location"].ToString());

                    Entity species = null;
                    long population = 0;
                    var speciesNode = stationToLoad["species"];
                    if(speciesNode != null)
                    {
                        var speciesName = speciesNode["name"].ToString();
                        species = faction.GetDataBlob<FactionInfoDB>().Species.Find(s => s.GetOwnersName().Equals(speciesName));
                        if(species == null) throw new NullReferenceException("invalid species name in json");
                        population = (long?)speciesNode["population"] ?? 0;
                    }

                    var station = (species != null && population > 0)
                        ? StationFactory.CreateStation(faction, location, population, species)
                        : StationFactory.CreateStation(faction, location);

                    var modulesToAdd = (JArray?)stationToLoad["installations"];
                    if(modulesToAdd != null)
                    {
                        foreach(var install in modulesToAdd)
                        {
                            var installId = install["id"].ToString();
                            var amount = (int?)install["amount"] ?? 1;
                            station.AddComponent(factionInfoDB.InternalComponentDesigns[installId], amount);
                        }
                    }

                    LoadCargo(station, factionDataStore, (JArray?)stationToLoad["cargo"]);
                    ReCalcProcessor.ReCalcAbilities(station);
                }
            }

            var fleetsToLoad = (JArray?)rootJson["fleets"];
            if(fleetsToLoad != null)
            {
                foreach(var fleetToLoad in fleetsToLoad)
                {
                    var fleetName = (string?)fleetToLoad["name"] ?? NameFactory.GetFleetName(game);
                    var systemId = fleetToLoad["location"]["systemId"].ToString();
                    var system = game.Systems.Find(s => s.ID.Equals(systemId));
                    if(system == null) throw new NullReferenceException("invalid systemId in json");
                    var location = NameLookup.GetFirstEntityWithName(system, fleetToLoad["location"]["body"].ToString());

                    var fleet = FleetFactory.Create(system, faction.Id, fleetName);
                    var fleetDB = fleet.GetDataBlob<FleetDB>();
                    fleetDB.SetParent(faction);

                    var shipsInFleet = (JArray?)fleetToLoad["ships"];
                    if(shipsInFleet != null)
                    {
                        foreach(var shipToLoad in shipsInFleet)
                        {
                            var designId = shipToLoad["designId"].ToString();
                            var shipName = (string?)shipToLoad["name"] ?? NameFactory.GetShipName(game);
                            var ship = ShipFactory.CreateShip(factionInfoDB.ShipDesigns[designId], faction, location, shipName);
                            fleetDB.AddChild(ship);

                            var commanderDB = CommanderFactory.CreateShipCaptain(game);
                            commanderDB.CommissionedOn = game.TimePulse.GameGlobalDateTime - TimeSpan.FromDays(365.25 * 10);
                            commanderDB.RankedOn = game.TimePulse.GameGlobalDateTime - TimeSpan.FromDays(365);
                            var commander = CommanderFactory.Create(system, faction.Id, commanderDB);
                            ship.GetDataBlob<ShipInfoDB>().CommanderID = commander.Id;

                            if(fleetDB.FlagShipID < 0)
                                fleetDB.FlagShipID = ship.Id;

                            LoadCargo(ship, factionDataStore, (JArray?)shipToLoad["cargo"]);
                        }
                    }
                }
            }

            return faction;
        }

        /// <summary>
        /// Phase 5.1a — build a <see cref="PersonalityDB"/> from a JSON object of <c>traitName → 0..1</c> (e.g.
        /// <c>{ "aggression": 0.8, "honor": 0.2 }</c>). Trait names match <see cref="PersonalityTrait"/> case-insensitively;
        /// an unknown name or a null value is skipped, and any trait the scenario omits stays <see cref="PersonalityDB.Neutral"/>.
        /// Public + static so a scenario loader OR a test can author a personality without a file (the acceptance-test rig).
        /// </summary>
        public static PersonalityDB PersonalityFromJson(JToken node)
        {
            var personality = new PersonalityDB();
            if (node is JObject obj)
            {
                foreach (var kv in obj)
                {
                    if (kv.Value == null || kv.Value.Type == JTokenType.Null) continue;
                    if (Enum.TryParse<PersonalityTrait>(kv.Key, ignoreCase: true, out var trait))
                        personality.SetTrait(trait, kv.Value.Value<double>());
                }
            }
            return personality;
        }

        /// <summary>
        /// Phase 5.1b — apply a faction's authored OPENING DIPLOMACY. <paramref name="openingNode"/> is a JSON array of
        /// <c>{ "target": "&lt;name or abbreviation&gt;", "score": &lt;int&gt;, "atWar": &lt;bool&gt; }</c> — e.g.
        /// <c>[{ "target": "TER", "atWar": true, "score": -80 }]</c> to start the game already at war. Targets are
        /// resolved by faction NAME or ABBREVIATION against <see cref="Game.Factions"/>, so this MUST run only after
        /// every faction is loaded (the scenario loader's second pass) — a not-yet-loaded / unknown target is skipped,
        /// never thrown on. Sets the relation score on BOTH ledgers (war + relationships are symmetric) and latches war
        /// via the Phase-3.4 <see cref="Diplomacy.DeclareWar"/> machinery. Public/static so a test can drive it directly.
        /// Defensive/no-throw.
        /// </summary>
        public static void ApplyOpeningRelations(Game game, Entity faction, JToken openingNode, DateTime when)
        {
            if (game == null || faction == null || openingNode is not JArray entries) return;
            foreach (var entry in entries)
            {
                var targetKey = entry["target"]?.ToString();
                if (string.IsNullOrEmpty(targetKey)) continue;

                var target = ResolveFactionByNameOrAbbr(game, targetKey);
                if (!target.IsValid || target == faction) continue;

                int score = entry["score"]?.Value<int>() ?? 0;
                SetRelationScore(faction, target, score);      // both directions — a relationship is two-sided
                SetRelationScore(target, faction, score);

                if (entry["atWar"]?.Value<bool>() ?? false)
                    Diplomacy.DeclareWar(faction, target, CasusBelli.ConfrontRival, when);
            }
        }

        /// <summary>
        /// DevTest (2026-07-13) — apply a faction's authored OPENING STRAIN to every colony it owns. The war-strain
        /// gauges (morale/legitimacy/sustenance) are DERIVED monthly from inputs, so you cannot seed the output number;
        /// this sets the INPUTS the processors read, so the strain STICKS: a high war-<c>taxRate</c> (morale drag +
        /// capped income), a sustenance <c>powerDemandPerCapita</c>/<c>foodDemandPerCapita</c> squeeze (shortage →
        /// morale hit + starvation), and <c>committedBulk</c> workforce tied up in the war effort (fewer hands to build
        /// with). Reads a faction-level <c>"strain"</c> object; called as a SECOND PASS beside <see cref="ApplyOpeningRelations"/>.
        /// Defensive/no-throw. Byte-identical for any faction with no <c>"strain"</c> node.
        /// </summary>
        public static void ApplyOpeningStrain(Entity faction, JToken strainNode)
        {
            if (faction == null || strainNode is not JObject) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            double taxRate       = strainNode["taxRate"]?.Value<double>() ?? -1;
            double powerDemand   = strainNode["powerDemandPerCapita"]?.Value<double>() ?? 0;
            double foodDemand    = strainNode["foodDemandPerCapita"]?.Value<double>() ?? 0;
            long   committedBulk = strainNode["committedBulk"]?.Value<long>() ?? 0;

            foreach (var colony in factionInfo.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;

                if (taxRate >= 0 && colony.TryGetDataBlob<ColonyEconomyDB>(out var econ))
                    econ.TaxRate = taxRate;

                if ((powerDemand > 0 || foodDemand > 0) && colony.TryGetDataBlob<ColonySustenanceDB>(out var sust))
                    sust.SetDemand(powerDemand, foodDemand);

                if (committedBulk > 0 && colony.TryGetDataBlob<ColonyManpowerDB>(out var manpower))
                    manpower.CommitBulk(committedBulk);
            }
        }

        /// <summary>Find a faction by its NAME or ABBREVIATION (case-insensitive), or <see cref="Entity.InvalidEntity"/>.</summary>
        private static Entity ResolveFactionByNameOrAbbr(Game game, string key)
        {
            foreach (var kvp in game.Factions)
            {
                var f = kvp.Value;
                if (f == null || !f.IsValid) continue;
                if (f.TryGetDataBlob<FactionInfoDB>(out var info)
                    && string.Equals(info.Abbreviation, key, StringComparison.OrdinalIgnoreCase))
                    return f;
                try { if (string.Equals(f.GetName(f.Id), key, StringComparison.OrdinalIgnoreCase)) return f; } catch { }
            }
            return Entity.InvalidEntity;
        }

        /// <summary>Converge <paramref name="from"/>'s view of <paramref name="to"/> to an absolute target score
        /// (idempotent — same shape as GameStageFactory.SetRelation). No-op if the faction has no ledger.</summary>
        private static void SetRelationScore(Entity from, Entity to, int targetScore)
        {
            if (!from.TryGetDataBlob<DiplomacyDB>(out var dip)) return;
            var rel = dip.GetOrCreateRelationship(to.Id);
            rel.AdjustScore(targetScore - rel.RelationScore);
        }

        private static void LoadCargo(Entity target, FactionDataStore factionDataStore, JArray? cargoArray)
        {
            if(cargoArray == null) return;

            foreach(var toAdd in cargoArray)
            {
                var cargoId = toAdd["id"].ToString();
                var amount = (int?)toAdd["amount"] ?? 1;
                var type = (string?)toAdd["type"] ?? "byMass";

                switch(type)
                {
                    case "byVolume":
                        CargoTransferProcessor.AddRemoveCargoVolume(target, factionDataStore.CargoGoods[cargoId], amount);
                        break;
                    case "byCount":
                        CargoTransferProcessor.AddCargoItems(target, factionDataStore.CargoGoods[cargoId], amount);
                        break;
                    default:
                        CargoTransferProcessor.AddRemoveCargoMass(target, factionDataStore.CargoGoods[cargoId], amount);
                        break;
                }
            }
        }


        public static Entity CreateFaction(Game game, string factionName)
        {
            var name = new NameDB(factionName);

            //var facinfo = new FactionInfoDB(new List<Entity>(), new List<Guid>(), );
            var factionInfo = new FactionInfoDB();
            factionInfo.Data = new FactionDataStore(game.StartingGameData);
            factionInfo.FactionMaskIndex = game.AllocateFactionMaskIndex();

            var factionTechDB = new FactionTechDB();

            var blobs = new List<BaseDataBlob> {
                name,
                factionInfo,
                new FactionAbilitiesDB(),
                factionTechDB,
                new FactionOwnerDB(),
                new FleetDB(),
                new OrderableDB(),
                new DiplomacyDB(),
                new GovernmentDB(),
                new InformationLedgerDB(),
            };
            var factionEntity = Entity.Create();
            game.GlobalManager.AddEntity(factionEntity, blobs);

            factionInfo.EventLog = FactionEventLog.Create(factionEntity.Id, game.TimePulse);
            factionInfo.EventLog.Subscribe();

            // Need to unlock the starting data in the game
            // foreach(var id in game.StartingGameData.DefaultItems["player-starting-items"].Items)
            // {
            //     factionInfo.Data.Unlock(id);

            //     // Research any tech that is listed
            //     if(factionInfo.Data.Techs.ContainsKey(id))
            //     {
            //         factionInfo.Data.IncrementTechLevel(id);
            //     }

            //     if(factionInfo.Data.CargoGoods.IsMaterial(id))
            //     {
            //         factionInfo.IndustryDesigns[id] = (IConstructableDesign)factionInfo.Data.CargoGoods[id];
            //     }
            // }

            // Add this faction to the SM's access list.
            game.SpaceMaster.SetAccess(factionEntity.Id, AccessRole.SM);
            name.SetName(factionEntity.Id, factionName);
            game.Factions.Add(factionEntity.Id, factionEntity);
            return factionEntity;
        }

        public static Entity CreateBasicFaction(Game game, string factionName, string abbreviation, int startingFunds)
        {
            var name = new NameDB(factionName);

            //var facinfo = new FactionInfoDB(new List<Entity>(), new List<Guid>(), );
            var factionInfo = new FactionInfoDB()
            {
                Abbreviation = abbreviation,
            };
            factionInfo.Data = new FactionDataStore(game.StartingGameData);
            factionInfo.FactionMaskIndex = game.AllocateFactionMaskIndex();
            factionInfo.Money.AddIncome(
                game.TimePulse.GameGlobalDateTime,
                TransactionCategory.InitialInvestment,
                "Add initial investments funds",
                startingFunds);

            var factionTechDB = new FactionTechDB();

            var blobs = new List<BaseDataBlob> {
                name,
                factionInfo,
                new FactionAbilitiesDB(),
                factionTechDB,
                new FactionOwnerDB(),
                new FleetDB(),
                new OrderableDB(),
                new DiplomacyDB(),
                new GovernmentDB(),
                new InformationLedgerDB(),
            };
            var factionEntity = Entity.Create();
            game.GlobalManager.AddEntity(factionEntity, blobs);

            factionInfo.EventLog = FactionEventLog.Create(factionEntity.Id, game.TimePulse);
            factionInfo.EventLog.Subscribe();

            // Add this faction to the SM's access list.
            game.SpaceMaster.SetAccess(factionEntity.Id, AccessRole.SM);
            name.SetName(factionEntity.Id, factionName);
            game.Factions.Add(factionEntity.Id, factionEntity);
            return factionEntity;
        }


        public static Entity CreatePlayerFaction(Game game, Player owningPlayer, string factionName)
        {
            Entity faction = CreateFaction(game, factionName);


            if (!Equals(owningPlayer, game.SpaceMaster))
            {
                owningPlayer.SetAccess(faction.Id, AccessRole.Owner);
            }

            return faction;
        }

        public static Entity CreateSpaceMasterFaction(Game game, Player owningPlayer, string factionName)
        {
            Entity faction = CreatePlayerFaction(game, owningPlayer, factionName);

            var factionInfo = faction.GetDataBlob<FactionInfoDB>();
            factionInfo.EventLog.Unsubscribe();
            factionInfo.EventLog = SpaceMasterEventLog.Create();
            factionInfo.EventLog.Subscribe();

            return faction;
        }


    }
}