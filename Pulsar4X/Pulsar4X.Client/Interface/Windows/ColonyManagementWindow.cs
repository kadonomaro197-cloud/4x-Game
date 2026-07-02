using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Engine;
using Pulsar4X.Colonies;
using Pulsar4X.People;

namespace Pulsar4X.Client
{
    public class ColonyManagementWindow : PulsarGuiWindow
    {
        private Dictionary<string, bool> isExpanded = new();
        public EntityState? SelectedEntity { get; private set; } = null;

        internal static ColonyManagementWindow GetInstance()
        {
            ColonyManagementWindow thisitem;
            if (!_uiState.LoadedWindows.ContainsKey(typeof(ColonyManagementWindow)))
            {
                thisitem = new ColonyManagementWindow()
                {
                    SelectedEntity = null
                };
            }
            thisitem = (ColonyManagementWindow)_uiState.LoadedWindows[typeof(ColonyManagementWindow)];

            return thisitem;
        }

        public void SelectEntity(EntityState entityState)
        {
            SelectedEntity = entityState;
        }

        // Tracks tab errors already logged so a per-frame throw doesn't spam the log (mirrors SafeRender's dedupe).
        private readonly HashSet<string> _loggedTabErrors = new();

        // Render one tab's body isolated: BeginTabItem is only true for the ACTIVE tab (unchanged behaviour), and a
        // throw inside the body is caught + logged ONCE so it can't skip ImGui.EndTabItem() and unbalance the tab
        // bar / child / window — which is exactly what cascaded every window when IndustryDisplay threw (2026-07-02).
        private void SafeTab(string label, Action body)
        {
            if(!ImGui.BeginTabItem(label)) return;
            try
            {
                body();
            }
            catch(Exception e)
            {
                if(_loggedTabErrors.Add(label + "|" + e.GetType().Name))
                {
                    Console.WriteLine($"[RenderError] ColonyManagement tab '{label}' threw and was skipped (logged once): {e}");
                    Console.Out.Flush();
                }
            }
            finally
            {
                ImGui.EndTabItem();
            }
        }

        internal override void Display()
        {
            if(!IsActive) return;

            if(Window.Begin("Manage Colonies", ref IsActive))
            {
                Vector2 windowContentSize = ImGui.GetContentRegionAvail();
                if(ImGui.BeginChild("Colonies", new Vector2(Styles.LeftColumnWidth, windowContentSize.Y), ImGuiChildFlags.Borders))
                {
                    DisplayHelpers.Header("Select Colony to Manage");
                    foreach(var (id, systemState) in _uiState.StarSystemStates)
                    {
                        if(!isExpanded.ContainsKey(id)) isExpanded.Add(id, true);
                        ImGui.SetNextItemOpen(isExpanded[id], ImGuiCond.Appearing);
                        if(ImGui.TreeNode(systemState.StarSystem.NameDB.DefaultName))
                        {
                            foreach(var (c_id, colony) in systemState.EntityStatesColonies)
                            {
                                var population = colony.Entity.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();

                                if(SelectedEntity == colony)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.75f, 0.25f, 0.25f, 1f));
                                }
                                else
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0f));
                                }

                                if(ImGui.SmallButton(colony.Name + " (" + Stringify.Quantity(population) + ")"))
                                {
                                    SelectEntity(colony);
                                }
                                ImGui.PopStyleColor();
                            }
                            ImGui.TreePop();
                        }
                    }
                }
                ImGui.EndChild();

                if (SelectedEntity == null)
                {
                    Window.End();
                    return;
                }

                ImGui.SameLine();

                if(ImGui.BeginChild("ColoniesTabs"))
                {
                    ImGui.BeginTabBar("EconomicsTabBar", ImGuiTabBarFlags.None);

                    // Each tab body goes through SafeTab so a throw INSIDE a tab (e.g. the IndustryDisplay hard-index
                    // crash, 2026-07-02) can't skip EndTabItem() and cascade the WHOLE UI — it's contained to that one
                    // tab, EndTabBar/EndChild/Window.End() still run, and the fault is logged once. Defense-in-depth on
                    // top of the actual fix; mirrors the SafeRender/SafeDraw discipline.
                    SafeTab("Summary", () => SelectedEntity.Entity.DisplaySummary(SelectedEntity, _uiState));
                    SafeTab("Society", () => SelectedEntity.Entity.DisplaySociety(SelectedEntity, _uiState));
                    SafeTab("Production", () => SelectedEntity.Entity.DisplayIndustry(SelectedEntity, _uiState));
                    SafeTab("Construction", () => SelectedEntity.Entity.DisplayConstruction(SelectedEntity, _uiState));
                    SafeTab("Mining", () => SelectedEntity.Entity.DisplayMining(_uiState));
                    // SafeTab("Logistics", () => SelectedEntity.Entity.DisplayLogistics(SelectedEntity, _uiState));
                    if(SelectedEntity.Entity.HasDataBlob<NavalAcademyDB>())
                        SafeTab("Naval Academy", () => SelectedEntity.Entity.DisplayNavalAcademy(SelectedEntity, _uiState));

                    ImGui.EndTabBar();
                }
                ImGui.EndChild();
            }
            Window.End();
        }
    }
}