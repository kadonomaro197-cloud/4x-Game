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
        // Templates grouped by ComponentType (the "Doors" of component design) — each group becomes one collapsible
        // category header in the Make New panel. Sorted by category name; templates sorted within each.
        private static List<KeyValuePair<string, List<ComponentTemplateBlueprint>>> templatesByCategory = new();
        private static ComponentTemplateBlueprint? selectedTemplate;


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

                // Group the templates by their ComponentType — the "Doors" the Make New panel organises by — so
                // every category (Weapons, Sensors, Power, …) is its own collapsible section of blank templates.
                templatesByCategory = templates
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.ComponentType) ? "Other" : t.ComponentType)
                    .OrderBy(g => g.Key)
                    .Select(g => new KeyValuePair<string, List<ComponentTemplateBlueprint>>(
                        g.Key, g.OrderBy(t => t.Name).ToList()))
                    .ToList();

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
                var firstChildSize = new Vector2(windowContentSize.X * 0.15f, windowContentSize.Y);
                var secondChildSize = new Vector2(windowContentSize.X * 0.15f, windowContentSize.Y);
                var thirdChildSize = new Vector2(windowContentSize.X * 0.7f - (windowContentSize.X * 0.01f), windowContentSize.Y);

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
                                  "Start a BRAND-NEW component design from scratch. Pick a category below (a \"door\"),\n" +
                                  "then a template — the blank framework for that kind of component. You set every\n" +
                                  "attribute yourself and press Save to create the design; nothing here is a copy of\n" +
                                  "an existing design.\n\n" +
                                  "To edit or re-open a design you already made, pick its category, then use the\n" +
                                  "\"Current Component Designs\" list to the right.");

            // One collapsible header per ComponentType — the "Doors" of component design. Open by default so the
            // panel reads as an organised menu of what can be built, not a wall of closed sections.
            foreach (var category in templatesByCategory)
            {
                if (!ImGui.CollapsingHeader(category.Key + "###door-" + category.Key, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;

                ImGui.Indent();
                foreach (var template in category.Value)
                {
                    bool isSelected = selectedTemplate == template;
                    if (ImGui.Selectable("＋ New " + template.Name + "###component-" + template.UniqueID, isSelected))
                    {
                        SelectTemplate(template);
                    }

                    // Guard the Description lookup: a template missing the "Description" formula must not throw in the
                    // designer (we just fixed a whole-client crash rooted in this window).
                    string desc = template.Formulas != null && template.Formulas.TryGetValue("Description", out var d) ? d : "";
                    DisplayHelpers.DescriptiveTooltip(template.Name, template.ComponentType, desc);
                }
                ImGui.Unindent();
            }
        }

        // Start a FRESH design from a template (not a copy of an existing design) and refresh the "existing designs
        // of this type" list shown beside it.
        void SelectTemplate(ComponentTemplateBlueprint template)
        {
            selectedTemplate = template;
            ComponentDesignDisplay.GetInstance().SetTemplate(selectedTemplate, _uiState);

            componentsOfType = new List<ComponentDesign>();
            foreach (var cd in componentDesigns)
            {
                if (cd.Value.TemplateName == template.Name)
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
                // Reset — otherwise the middle panel keeps showing the previous category's designs (a latent bug).
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