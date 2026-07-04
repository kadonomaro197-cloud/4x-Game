using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using Pulsar4X.DataStructures;
using Pulsar4X.Blueprints;
using Pulsar4X.Industry;
using Pulsar4X.Damage;

namespace Pulsar4X.Modding
{
    public class ModLoader
    {
        public Dictionary<string, ModManifest> LoadedMods { get; private set; } = new Dictionary<string, ModManifest>();

        /// <summary>
        /// Blueprints that ApplyModGeneric skipped because their UniqueID was null/empty (an illegal dictionary
        /// key). The skip keeps a malformed USER mod from crashing the whole load — but for the BASE mod a skip
        /// means a blueprint was silently dropped and the data is quietly broken, so tests should assert this is
        /// empty for base data (BaseModIntegrityTests). Accumulates across every LoadModManifest call on this instance.
        /// </summary>
        public List<string> SkippedEntries { get; } = new List<string>();

        public void LoadModManifest(string modManifestPath, ModDataStore baseData)
        {
            var manifestJson = File.ReadAllText(modManifestPath);
            var modManifest = JsonConvert.DeserializeObject<ModManifest>(manifestJson);

            if(LoadedMods.ContainsKey(modManifest.Namespace))
            {
                throw new DuplicateNameException("A mod with the namespace " + modManifest.Namespace + " has already been loaded.");
            }

            // Get the directory of the mod manifest
            string? modDirectory = Path.GetDirectoryName(modManifestPath);

            if(string.IsNullOrEmpty(modDirectory)) throw new DirectoryNotFoundException($"Could not find {modManifestPath}");

            modManifest.ModDirectory = modDirectory;

            foreach (var modDataFile in modManifest.DataFiles)
            {
                // Combine the directory with the mod data file name
                string modDataFilePath = Path.Combine(modDirectory, modDataFile);

                var modInstructions = JsonConvert.DeserializeObject<List<ModInstruction>>(
                    File.ReadAllText(modDataFilePath),
                    new JsonSerializerSettings { Converters = new List<JsonConverter> { new ModInstructionJsonConverter(), new WeightedListConverter() } });

                foreach (var mod in modInstructions)
                {
                    mod.Data.JsonFileName =  modDataFile;
                    ApplyMod(baseData, mod, modManifest.Namespace);
                }
            }

            baseData.ModManifests.Add(modManifest);

            LoadedMods.Add(modManifest.Namespace, modManifest);
        }

        private void ApplyMod(ModDataStore baseData, ModInstruction mod, string modNamespace)
        {
            switch (mod.Type)
            {
                case ModInstruction.DataType.Armor:
                    ApplyModGeneric<ArmorBlueprint>(baseData.Armor, mod, modNamespace);
                    break;
                case ModInstruction.DataType.CargoType:
                    ApplyModGeneric<CargoTypeBlueprint>(baseData.CargoTypes, mod, modNamespace);
                    break;
                case ModInstruction.DataType.ComponentTemplate:
                    ApplyModGeneric<ComponentTemplateBlueprint>(baseData.ComponentTemplates, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Gas:
                    ApplyModGeneric<GasBlueprint>(baseData.AtmosphericGas, mod, modNamespace);
                    break;
                case ModInstruction.DataType.IndustryType:
                    ApplyModGeneric<IndustryTypeBlueprint>(baseData.IndustryTypes, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Mineral:
                    ApplyModGeneric<Mineral>(baseData.Minerals, mod, modNamespace);
                    break;
                case ModInstruction.DataType.ProcessedMaterial:
                    ApplyModGeneric<ProcessedMaterial>(baseData.ProcessedMaterials, mod, modNamespace);
                    break;
                case ModInstruction.DataType.SystemGenSettings:
                    ApplyModGeneric<SystemGenSettingsBlueprint>(baseData.SystemGenSettings, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Tech:
                    ApplyModGeneric<TechBlueprint>(baseData.Techs, mod, modNamespace);
                    break;
                case ModInstruction.DataType.TechCategory:
                    ApplyModGeneric<TechCategoryBlueprint>(baseData.TechCategories, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Theme:
                    ApplyModGeneric<ThemeBlueprint>(baseData.Themes, mod, modNamespace);
                    break;
                case ModInstruction.DataType.DamageResistance:
                    ApplyModGeneric<DamageResistBlueprint>(baseData.DamageResists, mod, modNamespace);
                    break;
                case ModInstruction.DataType.PartMat:
                    ApplyModGeneric<ParticleMaterialBlueprint>(baseData.ParticleMaterials, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Species:
                    ApplyModGeneric<SpeciesBlueprint>(baseData.Species, mod, modNamespace);
                    break;
                case ModInstruction.DataType.System:
                    ApplyModGeneric<SystemBlueprint>(baseData.Systems, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Star:
                    ApplyModGeneric<StarBlueprint>(baseData.Stars, mod, modNamespace);
                    break;
                case ModInstruction.DataType.SystemBody:
                    ApplyModGeneric<SystemBodyBlueprint>(baseData.SystemBodies, mod, modNamespace);
                    break;
                case ModInstruction.DataType.Colony:
                    ApplyModGeneric<ColonyBlueprint>(baseData.Colonies, mod, modNamespace);
                    break;
                case ModInstruction.DataType.ComponentDesign:
                    ApplyModGeneric<ComponentDesignBlueprint>(baseData.ComponentDesigns, mod, modNamespace);
                    break;
                case ModInstruction.DataType.ShipDesign:
                    ApplyModGeneric<ShipDesignBlueprint>(baseData.ShipDesigns, mod, modNamespace);
                    break;
                case ModInstruction.DataType.CombatDoctrine:
                    ApplyModGeneric<CombatDoctrineBlueprint>(baseData.CombatDoctrines, mod, modNamespace);
                    break;
                case ModInstruction.DataType.GroundStance:
                    ApplyModGeneric<GroundStanceBlueprint>(baseData.GroundStances, mod, modNamespace);
                    break;
            }
        }


        private void ApplyModGeneric<T>(Dictionary<string, T> dataDict, ModInstruction instruction, string modNamespace) where T : Blueprint
        {
            // A null/empty UniqueID would throw ArgumentNullException from Dictionary.TryGetValue below (null keys
            // are illegal). Skip the entry rather than crash the whole mod load — but say so, because a silently
            // dropped blueprint is hard to diagnose downstream. Base-game data should never trip this.
            if (string.IsNullOrEmpty(instruction.Data?.UniqueID))
            {
                var skipped = $"'{instruction.Type}' entry in '{instruction.Data?.JsonFileName ?? "unknown file"}'";
                SkippedEntries.Add(skipped);
                Console.WriteLine($"[ModLoader] Skipping a {skipped} because its UniqueID is null or empty.");
                return;
            }
            if (dataDict.TryGetValue(instruction.Data.UniqueID, out var existingData))
            {
                if (instruction.Operation == ModInstruction.OperationType.Default)
                {
                    // Update the namespace
                    existingData.SetFullIdentifier(modNamespace);

                    // Use reflection to overwrite specific properties
                    foreach (var property in instruction.Data.GetType().GetProperties())
                    {
                        var modValue = property.GetValue(instruction.Data);
                        if (modValue != null)
                        {
                            // If property is a collection and CollectionOperation is specified
                            if (property.PropertyType.IsGenericType
                                && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)
                                && instruction.CollectionOperation.HasValue)
                            {
                                var originalList = (IList?)property.GetValue(existingData);
                                var modList = (IList)modValue;

                                switch(instruction.CollectionOperation.Value)
                                {
                                    case ModInstruction.CollectionOperationType.Add:
                                        // If the base blueprint never defined this list there is nothing to
                                        // merge into, so the mod's list simply becomes the value. Throwing here
                                        // (the old behaviour) took down the whole mod load — and the game.
                                        if (originalList == null)
                                        {
                                            property.SetValue(existingData, modValue);
                                        }
                                        else
                                        {
                                            foreach(var item in modList)
                                            {
                                                originalList.Add(item);
                                            }
                                        }
                                        break;
                                    case ModInstruction.CollectionOperationType.Remove:
                                        // Nothing to remove from a list the base never defined.
                                        if (originalList != null)
                                        {
                                            foreach(var item in modList)
                                            {
                                                originalList.Remove(item);
                                            }
                                        }
                                        break;
                                    case ModInstruction.CollectionOperationType.Overwrite:
                                        property.SetValue(existingData, modValue);
                                        break;
                                }
                            }
                            // If property is a dictionary and CollectionOperation is specified
                            else if (property.PropertyType.IsGenericType
                                && property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                                && instruction.CollectionOperation.HasValue)
                            {
                                var originalDict = (IDictionary?)property.GetValue(existingData);
                                var modDict = (IDictionary)modValue;

                                switch(instruction.CollectionOperation.Value)
                                {
                                    case ModInstruction.CollectionOperationType.Add:
                                        // If the base blueprint never defined this dictionary there is nothing to
                                        // merge into, so the mod's dictionary simply becomes the value. Throwing here
                                        // (the old behaviour) took down the whole mod load — and the game.
                                        if (originalDict == null)
                                        {
                                            property.SetValue(existingData, modValue);
                                        }
                                        else
                                        {
                                            foreach(DictionaryEntry entry in modDict)
                                            {
                                                originalDict[entry.Key] = entry.Value;
                                            }
                                        }
                                        break;
                                    case ModInstruction.CollectionOperationType.Remove:
                                        // Nothing to remove from a dictionary the base never defined.
                                        if (originalDict != null)
                                        {
                                            foreach(DictionaryEntry entry in modDict)
                                            {
                                                originalDict.Remove(entry.Key);
                                            }
                                        }
                                        break;
                                    case ModInstruction.CollectionOperationType.Overwrite:
                                        property.SetValue(existingData, modValue);
                                        break;
                                }
                            }
                            // Check if the property is of type Guid and if it's equal to Guid.Empty
                            else if (property.PropertyType == typeof(Guid) && (Guid)modValue == Guid.Empty)
                            {
                                // Skip overwriting for empty Guid values
                                continue;
                            }
                            else
                            {
                                property.SetValue(existingData, modValue);
                            }
                        }
                    }
                }
                else if (instruction.Operation == ModInstruction.OperationType.Remove)
                {
                    dataDict.Remove(instruction.Data.UniqueID);
                    return;
                }
            }
            else
            {
                instruction.Data.SetFullIdentifier(modNamespace);
                dataDict[instruction.Data.UniqueID] = (T)instruction.Data;
            }
        }
    }
}