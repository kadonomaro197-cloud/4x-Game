using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Blueprints;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;

namespace Pulsar4X.Client
{
    public class ComponentDesignWindow : PulsarGuiWindow
    {
        private static List<ComponentTemplateBlueprint> templates = new();

        // The design's Category ▸ Door taxonomy (docs/economy/COMPONENT-DESIGNER-CATEGORIES.md, via ComponentDoors):
        // ordered categories, each holding its ordered doors, each holding the "Make New" templates behind that door.
        private sealed class DoorGroup { public string Door; public List<ComponentTemplateBlueprint> Templates = new(); }
        private sealed class CategoryGroup { public string Category; public List<DoorGroup> Doors = new(); }
        private static List<CategoryGroup> templatesByCategory = new();
        private static ComponentTemplateBlueprint? selectedTemplate;
        // The DOOR the player has opened (Energy / Ballistic / …). The tree now selects a DOOR, not a named
        // template — the specific type within a door is a "Type" dropdown in the design panel.
        private static DoorGroup? _selectedDoor;


        private static Dictionary<string, ComponentDesign> componentDesigns = new();
        private static List<ComponentDesign> componentsOfType = new();
        private static string[]? componentNames = new string[0];
        private static ComponentDesign selectedComponent;

        private ComponentDesignWindow() { }

        internal static ComponentDesignWindow GetInstance()
        {
            ComponentDesignWindow thisitem;
            if (!_uiState.LoadedWindows.ContainsKey(typeof(ComponentDesignWindow)))
            {
                thisitem = new ComponentDesignWindow();

                // FIXME: doing this here is efficient but it will never update the list if new templates are available
                templates = _uiState.Faction.GetDataBlob<FactionInfoDB>().Data.ComponentTemplates.Select(kvp => kvp.Value).ToList();
                templates.Sort((a, b) => a.Name.CompareTo(b.Name));

                // Classify every unlocked template into its designed Category ▸ Door (ComponentDoors), then lay them out
                // in the design's category/door order so the panel reads as the taxonomy (Weapons → Energy/Ballistic/…,
                // Logistical → Storage where cargo AND fuel live, Chassis → Hull, …). Unmapped templates fall into
                // "Other" keyed by their raw ComponentType, so nothing ever vanishes.
                var byCatDoor = new Dictionary<string, Dictionary<string, List<ComponentTemplateBlueprint>>>();
                foreach (var t in templates)
                {
                    var (cat, door) = ComponentDoors.Classify(t.UniqueID, t.ComponentType);
                    if (!byCatDoor.TryGetValue(cat, out var doors)) { doors = new(); byCatDoor[cat] = doors; }
                    if (!doors.TryGetValue(door, out var list)) { list = new(); doors[door] = list; }
                    list.Add(t);
                }

                templatesByCategory = new List<CategoryGroup>();
                // Categories in the design's order first; any stray category (shouldn't happen — "Other" is the sink) after.
                var orderedCats = ComponentDoors.CategoryOrder
                    .Where(byCatDoor.ContainsKey)
                    .Concat(byCatDoor.Keys.Where(c => System.Array.IndexOf(ComponentDoors.CategoryOrder, c) < 0));
                foreach (var cat in orderedCats)
                {
                    var doors = byCatDoor[cat];
                    var cg = new CategoryGroup { Category = cat };
                    foreach (var door in doors.Keys.OrderBy(d => ComponentDoors.DoorRank(cat, d)).ThenBy(d => d))
                        cg.Doors.Add(new DoorGroup { Door = door, Templates = doors[door].OrderBy(t => t.Name).ToList() });
                    templatesByCategory.Add(cg);
                }

                componentDesigns = _uiState.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns.ToDictionary();
            }
            thisitem = (ComponentDesignWindow)_uiState.LoadedWindows[typeof(ComponentDesignWindow)];

            return thisitem;
        }

        internal override void Display()
        {
            if(!IsActive) return;

            if(Window.Begin("Component Designer", ref IsActive, _flags))
            {
                Vector2 windowContentSize = ImGui.GetContentRegionAvail();
                // Left panel is a two-level Category ▸ Door tree now, so give it a little more room.
                var firstChildSize = new Vector2(windowContentSize.X * 0.22f, windowContentSize.Y);
                var secondChildSize = new Vector2(windowContentSize.X * 0.15f, windowContentSize.Y);
                var thirdChildSize = new Vector2(windowContentSize.X * 0.63f - (windowContentSize.X * 0.01f), windowContentSize.Y);

                if(ImGui.BeginChild("ComponentDesignSelection", firstChildSize, ImGuiChildFlags.Borders))
                {
                    DisplayTemplateSelection();
                }
                ImGui.EndChild();
                ImGui.SameLine();
                if (ImGui.BeginChild("ComponentSelection", secondChildSize, ImGuiChildFlags.Borders))
                {
                    DisplayComponentList();
                }
                ImGui.EndChild();
                ImGui.SameLine();
                if (ImGui.BeginChild("ComponentDesign", thirdChildSize, ImGuiChildFlags.None))
                {
                    if(selectedTemplate != null)
                    {
                        ComponentDesignDisplay.GetInstance().Display(_uiState);
                    }
                }
                ImGui.EndChild();


                ImGui.SameLine();
                //ImGui.SetCursorPosY(27f); // FIXME: this should somehow be calculated


                Window.End();
            }
        }

        void DisplayTemplateSelection()
        {
            DisplayHelpers.Header("Make New Component",
                                  "Start a BRAND-NEW component design from scratch. Open a category, then click a\n" +
                                  "\"door\" (e.g. Weapons → Energy / Ballistic / Guided). The design panel opens with a\n" +
                                  "\"Type\" dropdown for the specific kind within that door, and its dials — you set every\n" +
                                  "attribute yourself and press Save to create the design; nothing here is a copy of an\n" +
                                  "existing design.\n\n" +
                                  "To edit or re-open a design you already made, open its door, then use the\n" +
                                  "\"Current Component Designs\" list to the right.");

            // One collapsible header per CATEGORY (the 11 designed categories), and inside it a labelled section per
            // DOOR (Weapons ▸ Energy / Ballistic / …). Categories open by default so the panel reads as the taxonomy.
            foreach (var category in templatesByCategory)
            {
                if (!ImGui.CollapsingHeader(category.Category + "###cat-" + category.Category, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;

                ImGui.Indent();
                foreach (var door in category.Doors)
                {
                    // The DOOR is the clickable leaf now (Energy / Ballistic / Guided / …) — NOT a list of named
                    // example templates ("＋ New Rending Claws"). Click a door to start a blank design; the specific
                    // kind within the door (Ballistic → Railgun / Flak / …) is a "Type" dropdown in the panel —
                    // "roles are dials, not doors" (docs/economy/COMPONENT-DESIGNER-CATEGORIES.md).
                    bool doorSelected = _selectedDoor == door;
                    if (ImGui.Selectable(door.Door + "###door-" + category.Category + "-" + door.Door, doorSelected))
                    {
                        SelectDoor(door);
                    }
                    // Tooltip names the types behind the door, so nothing is hidden — just not a tree leaf each.
                    if (door.Templates.Count > 0)
                    {
                        string types = string.Join(", ", door.Templates.Select(t => t.Name));
                        DisplayHelpers.DescriptiveTooltip(door.Door, category.Category, "Types: " + types);
                    }
                }
                ImGui.Unindent();
            }
        }

        // Open a DOOR: start a FRESH design on the door's first template (the "Type" dropdown in the panel switches
        // among the door's templates), and refresh the "existing designs behind this door" list shown beside it.
        void SelectDoor(DoorGroup door)
        {
            _selectedDoor = door;
            if (door.Templates.Count == 0) return;

            selectedTemplate = door.Templates[0];                 // keeps the panel rendering (Display gate) + a default
            ComponentDesignDisplay.GetInstance().SetDoor(door.Templates, _uiState);

            // The middle list shows every existing design behind this door (across all its types), not just one
            // template — "your existing Ballistic designs", matching the door-level navigation.
            var doorTemplateNames = new HashSet<string>(door.Templates.Select(t => t.Name));
            componentsOfType = new List<ComponentDesign>();
            foreach (var cd in componentDesigns)
            {
                if (doorTemplateNames.Contains(cd.Value.TemplateName))
                    componentsOfType.Add(cd.Value);
            }

            if (componentsOfType.Count > 0)
            {
                componentNames = new string[componentsOfType.Count];
                for (int c = 0; c < componentsOfType.Count; c++)
                    componentNames[c] = componentsOfType[c].Name;
            }
            else
            {
                // Reset — otherwise the middle panel keeps showing the previous door's designs (a latent bug).
                componentNames = new string[0];
            }
        }

        void DisplayComponentList()
        {
            DisplayHelpers.Header("Current Component Designs of this type");

            var availableSize = ImGui.GetContentRegionAvail();
            ImGui.SetNextItemWidth(availableSize.X);
            if (componentNames.Length > 0)
            {
                for (int index = 0; index < componentsOfType.Count; index++)
                {
                    ComponentDesign? component = componentsOfType[index];
                    bool isSelected = componentsOfType[index] == component;
                    if (ImGui.Selectable(component.Name + "###component-" + component.UniqueID, isSelected))
                    {
                        ComponentDesignDisplay.GetInstance().SetFromComponent(component, _uiState);
                    }
                }
            }

            ImGui.BeginDisabled();
            if(ImGui.Button("Create Template", new Vector2(204f, 0f)))
            {

            }
            ImGui.EndDisabled();

        }

        public override void OnGameTickChange(DateTime newDate)
        {
        }
    }
}