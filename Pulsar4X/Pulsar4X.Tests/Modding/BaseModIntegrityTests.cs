using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Integrity checks on the shipped base-mod JSON data, exercising the data the GAME loads on New Game
    /// (the JSON colony-blueprint path through ColonyFactory.CreateFromBlueprint).
    ///
    /// The rest of the suite builds its starting colony in C# via DefaultStartFactory.DefaultHumans, so the
    /// JSON colony-blueprint path is otherwise UNTESTED. That coverage gap is exactly how a missing
    /// "electronics" unlock shipped green through `dotnet test` and only crashed when a human clicked New Game.
    /// </summary>
    [TestFixture]
    [Description("Base-mod JSON data integrity — covers the New Game colony-blueprint path the rest of the suite skips.")]
    internal class BaseModIntegrityTests
    {
        private ModDataStore _baseMod;
        private ModLoader _modLoader;

        [SetUp]
        public void Setup()
        {
            _baseMod = new ModDataStore();
            _modLoader = new ModLoader();
            _modLoader.LoadModManifest("Data/basemod/modInfo.json", _baseMod);
        }

        [Test]
        [Description("Every material a starting colony's ComponentDesigns require must be unlocked by that " +
                     "colony's StartingItems. Regression for the Quickstart crash where the laser weapon, " +
                     "Ship Yard and Research Lab needed 'electronics'/'ree-magnetics' that StartingItems " +
                     "never unlocked, faulting deep in ComponentDesigner.")]
        public void StartingColonies_CanBuildEveryStartingComponentDesign()
        {
            var failures = new List<string>();

            foreach (var kvp in _baseMod.Colonies)
            {
                var colonyId = kvp.Key;
                var colony = kvp.Value;
                if (colony.ComponentDesigns == null)
                    continue;

                // Reproduce what ColonyFactory.CreateFromBlueprint does before it builds the designs:
                // everything starts locked, then the colony's StartingItems are unlocked (and any listed
                // tech is researched, which can unlock further items). This reuses the real Unlock logic,
                // so the check matches the game exactly — no guessing about the unlock closure.
                var factionData = new FactionDataStore(_baseMod);
                foreach (var id in colony.StartingItems ?? new List<string>())
                {
                    factionData.Unlock(id);
                    if (factionData.Techs.ContainsKey(id))
                        factionData.IncrementTechLevel(id);
                }

                foreach (var designId in colony.ComponentDesigns)
                {
                    if (!_baseMod.ComponentDesigns.TryGetValue(designId, out var design))
                    {
                        failures.Add($"colony '{colonyId}' lists ComponentDesign '{designId}', which is not defined");
                        continue;
                    }
                    if (!_baseMod.ComponentTemplates.TryGetValue(design.TemplateId, out var template))
                    {
                        failures.Add($"design '{designId}' uses TemplateId '{design.TemplateId}', which is not defined");
                        continue;
                    }
                    // The template must also be UNLOCKED into the faction store (via StartingItems or a starting
                    // tech) — ComponentDesigner reads factionData.ComponentTemplates (unlocked), NOT the global set.
                    // A design listed in ComponentDesigns whose template isn't unlocked throws "not found in the
                    // faction data store" deep in colony creation (crashed New Game for the ground-unit designs,
                    // 2026-07-05: they were in ComponentDesigns but not StartingItems). This is the 6th registration
                    // point (gotcha #10) the material check below can't see.
                    if (!factionData.ComponentTemplates.ContainsKey(design.TemplateId))
                        failures.Add(
                            $"colony '{colonyId}': starting design '{designId}' uses template '{design.TemplateId}', " +
                            $"which its StartingItems never unlock — add '{design.TemplateId}' to StartingItems " +
                            $"(ComponentDesigner throws 'not found in the faction data store' at New Game otherwise)");
                    if (template.ResourceCost == null)
                        continue;

                    // ComponentDesigner looks each ResourceCost key up in the faction's UNLOCKED CargoGoods.
                    foreach (var materialId in template.ResourceCost.Keys)
                    {
                        if (factionData.CargoGoods.GetAny(materialId) == null)
                        {
                            failures.Add(
                                $"colony '{colonyId}': starting design '{designId}' (template '{design.TemplateId}') " +
                                $"requires '{materialId}', but it is not unlocked by the colony's StartingItems");
                        }
                    }
                }
            }

            Assert.That(failures, Is.Empty,
                "Starting colonies require materials not unlocked at game start — this crashes New Game in " +
                "ComponentDesigner. Add each missing material to that colony's StartingItems:\n  "
                + string.Join("\n  ", failures));
        }

        [Test]
        [Description("The base mod must load with zero skipped entries. ModLoader silently skips any blueprint " +
                     "whose UniqueID is null/empty (a guard so a malformed USER mod can't crash the load); for " +
                     "the BASE mod a skip means a blueprint was dropped and the data is quietly broken. " +
                     "Regression for the DamageResistBlueprint null-UniqueID bug, which the skip-guard hid from " +
                     "the suite (loading went green again) until the root cause was fixed.")]
        public void BaseMod_LoadsWithNoSkippedEntries()
        {
            Assert.That(_modLoader.SkippedEntries, Is.Empty,
                "ModLoader silently dropped base-mod blueprints with a null/empty UniqueID — the loaded data is " +
                "incomplete even though nothing threw:\n  " + string.Join("\n  ", _modLoader.SkippedEntries));
        }
    }
}
