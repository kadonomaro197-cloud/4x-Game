using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine.Orders;
using Pulsar4X.DataStructures;
using Pulsar4X.Extensions;
using Pulsar4X.Fleets;
using Pulsar4X.GeoSurveys;
using Pulsar4X.JumpPoints;
using Pulsar4X.Names;
using Pulsar4X.Ships;
using Pulsar4X.Storage;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Combat;
using Pulsar4X.Blueprints;
using Pulsar4X.Sensors;
using Pulsar4X.Weapons;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Client
{
    public class FleetWindow : PulsarGuiWindow
    {
        private enum IssueOrderType
        {
            MoveTo,
            GeoSurvey,
            JPSurvey,
            Jump,
            RefuelAt,
            Troops,   // Earthfall C5.1 — embark ground units onto a troop-bay ship / land them on a target world
        }

        private IssueOrderType selectedIssueOrderType = IssueOrderType.MoveTo;

        private FleetDB? factionRoot;
        private int factionID;
        private Entity dragEntity = Entity.InvalidEntity;
        public Entity? SelectedFleet { get; private set; } = null;
        private Entity? selectedFleetFlagship = null;
        private Entity? selectedFleetSystem = null;
        private FleetDB? selectedFleetDB = null;
        private bool selectedFleetInheritOrders = false;
        int nameCounter = 1;
        private Dictionary<Entity, bool> selectedShips = new ();
        private Dictionary<Entity, bool> selectedUnattachedShips = new ();

        private ConditionalOrder? selectedOrder = null;

        private Dictionary<ConditionItem, int> orderConditionIndexes = new Dictionary<ConditionItem, int>();
        private int orderComparisonIndex = 0;
        private string[] orderComparisons;
        private int orderActionsIndex = 0;
        private int orderConditionsIndex = 0;
        private string[] orderActionDescriptions = OrderRegistry.Actions.Keys.ToArray();
        private string[] orderConditionDescriptions = OrderRegistry.Conditions.Keys.ToArray();
        private byte[] orderNameBuffer = new byte[32];

        private List<EntityState> moveToList = new ();
        private List<EntityState> geoSurveyList = new ();
        private List<EntityState> gravSurveyList = new ();
        private List<EntityState> colonyList = new ();
        private List<EntityState> jumpPointList = new ();

        // ── Combat tab — doctrine selector state ──
        // The catalog of selectable postures (ModDataStore.CombatDoctrines). Cached and only rebuilt when the
        // count changes (the cheap-sync pattern DevToolsWindow uses), so it's safe to touch every frame.
        private CombatDoctrineBlueprint[] _doctrineBlueprints = Array.Empty<CombatDoctrineBlueprint>();
        private string[] _doctrineNames = Array.Empty<string>();
        private int _selectedDoctrine = 0;
        private string _doctrineStatus = "";

        // EMCON posture lever (detection slice 3b): the three fixed postures the player can fly. Index-aligned with
        // _emconPostureNames so the combo selection maps straight to a posture.
        private readonly EmconPosture[] _emconPostures = { EmconPosture.Full, EmconPosture.Cruise, EmconPosture.Silent };
        private readonly string[] _emconPostureNames = { "Full", "Cruise", "Silent" };
        private int _selectedEmconPosture = 0;
        private string _emconStatus = "";

        // Engagement-posture lever (closing P3): whether this fleet will START a fight. Index-aligned with
        // _engagePostureNames so the combo maps straight to a posture. This is the player's half of the first-shot
        // rule — Weapons Free starts battles, Hold Fire never shoots first (two hold-fire fleets sit in a standoff),
        // Return Fire defends but won't open. The enemy postures are set by the scenario; this lets the PLAYER hold.
        private readonly EngagementPosture[] _engagePostures = { EngagementPosture.WeaponsFree, EngagementPosture.WeaponsHold, EngagementPosture.ReturnFire };
        private readonly string[] _engagePostureNames = { "Weapons Free", "Hold Fire", "Return Fire" };
        private int _selectedEngagePosture = 0;
        private string _engageStatus = "";

        private FleetWindow()
        {
            FactionChanged(_uiState);

            _uiState.OnFactionChanged += FactionChanged;

            orderComparisons = new string[5];
            orderComparisons[0] = ComparisonType.LessThan.ToDescription();
            orderComparisons[1] = ComparisonType.LessThanOrEqual.ToDescription();
            orderComparisons[2] = ComparisonType.EqualTo.ToDescription();
            orderComparisons[3] = ComparisonType.GreaterThan.ToDescription();
            orderComparisons[4] = ComparisonType.GreaterThanOrEqual.ToDescription();
        }
        internal static FleetWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(FleetWindow)))
            {
                return new FleetWindow();
            }
            return (FleetWindow)_uiState.LoadedWindows[typeof(FleetWindow)];
        }

        private void FactionChanged(GlobalUIState uiState)
        {
            if(uiState.Faction == null)
                throw new NullReferenceException();

            factionID = uiState.Faction.Id;
            factionRoot = uiState.Faction.GetDataBlob<FleetDB>();

            SelectFleet(factionRoot.Children.FirstOrDefault(c => c.HasDataBlob<FleetDB>()));
        }

        public void SelectFleet(Entity? fleet)
        {
            SelectedFleet = fleet;
            selectedShips = new ();
            SelectOrder(null);

            SelectedFleet?.TryGetDataBlob<FleetDB>(out selectedFleetDB);
            if(selectedFleetDB == null || selectedFleetDB.FlagShipID == -1)
            {
                selectedFleetFlagship = null;
                selectedFleetSystem = null;
            }
            else
            {
                selectedFleetDB.OwningEntity?.Manager?.TryGetEntityById(selectedFleetDB.FlagShipID, out selectedFleetFlagship);
                if(selectedFleetFlagship != null && selectedFleetFlagship.IsValid && selectedFleetFlagship.TryGetDataBlob<PositionDB>(out var positionDB))
                {
                    selectedFleetSystem = positionDB?.Root;
                }
                else
                {
                    // If the above condition failed the selectedFleetFlagship needs to be set to null
                    selectedFleetFlagship = null;
                }
                selectedFleetInheritOrders = selectedFleetDB.InheritOrders;
            }
        }

        private void SelectOrder(ConditionalOrder? order)
        {
            selectedOrder = order;

            if(selectedOrder != null)
            {
                orderNameBuffer = selectedOrder.Name.IsNullOrEmpty() ? new byte[32] : Utils.BytesFromString(selectedOrder.Name, 32);
            }
        }

        // Per-section perf gauge — CI can't see client frame time, so this is the instrument to localize a "fleet
        // manager is laggy" report: when this window's frame work is heavy it logs the section breakdown (throttled),
        // so the play-test log names WHICH part (list / ships / tabs) is eating the time instead of just "it's slow".
        private const double FleetWindowSlowMs = 4.0;
        private int _lastPerfLogTick;

        internal override void Display()
        {
            if(!IsActive) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long tList = 0, tShips = 0, tOrders = 0, tTabs = 0, t0;

            // Retitled "Fleet Management" → "Force Management" (Earthfall C3.1): this window now commands BOTH fleets
            // (ships) and battalions (ground formations) — the two are first-class siblings, split across a top-level
            // tab bar. The class stays FleetWindow (renaming it would orphan LoadedWindows/save refs — R1 ledger).
            if(Window.Begin("Force Management", ref IsActive, _flags))
            {
                if(ImGui.BeginTabBar("ForceMgmtTopTabs"))
                {
                    // ── Fleets tab: the EXISTING fleet manager, unchanged (byte-identical inside its own tab) ──
                    if(ImGui.BeginTabItem("Fleets"))
                    {
                        // Finer hang breadcrumbs (2026-07-03): the [HANG] watchdog reads SessionLog.CurrentStage, which
                        // SafeRender set to just 'FleetWindow' — too coarse to tell WHICH section wedged. Stamping the
                        // sub-section here means the next freeze names List/Ships/Orders/Tabs (and DisplayTabs stamps the
                        // tab) instead of the whole window. Cheap volatile write; no behaviour change.
                        SessionLog.CurrentStage = "FleetWindow/List";
                        t0 = sw.ElapsedTicks; DisplayFleetList(); tList = sw.ElapsedTicks - t0;

                        if(SelectedFleet != null)
                        {
                            ImGui.SameLine();
                            ImGui.SetCursorPosY(27f);
                            var ysize = ImGui.GetContentRegionAvail().Y;
                            SessionLog.CurrentStage = "FleetWindow/Ships";
                            t0 = sw.ElapsedTicks; DisplayShips(); tShips = sw.ElapsedTicks - t0;
                            ImGui.SetCursorPosY(ysize * 0.5f);
                            SessionLog.CurrentStage = "FleetWindow/Orders";
                            t0 = sw.ElapsedTicks; DisplayOrders(); tOrders = sw.ElapsedTicks - t0;

                            ImGui.SameLine();
                            ImGui.SetCursorPosY(27f);

                            SessionLog.CurrentStage = "FleetWindow/Tabs";
                            t0 = sw.ElapsedTicks; DisplayTabs(); tTabs = sw.ElapsedTicks - t0;
                        }
                        ImGui.EndTabItem();
                    }

                    // ── Battalions tab: cross-body ground-formation manager (C3) ──
                    // Body wrapped so a throw between BeginTabItem/EndTabItem can't unbalance the ImGui stack (the
                    // colony-window cascade lesson): the fault is caught + logged once and EndTabItem still runs.
                    if(ImGui.BeginTabItem("Battalions"))
                    {
                        SessionLog.CurrentStage = "FleetWindow/Battalions";
                        try { DisplayBattalions(); }
                        catch(Exception ex)
                        {
                            ImGui.TextUnformatted("Battalion view hit an error (logged).");
                            if(!_battErrorLogged)
                            {
                                Console.WriteLine("[RenderError] FleetWindow Battalions threw (logged once): " + ex);
                                Console.Out.Flush();
                                _battErrorLogged = true;
                            }
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                SessionLog.CurrentStage = "FleetWindow";
            }
            Window.End();

            double totalMs = sw.Elapsed.TotalMilliseconds;
            if (SessionLog.Enabled && totalMs > FleetWindowSlowMs)
            {
                int now = Environment.TickCount;
                if (now - _lastPerfLogTick >= 1000)   // throttle to ~1/sec so a sustained slow frame doesn't flood
                {
                    _lastPerfLogTick = now;
                    double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;   // stopwatch-ticks -> ms
                    SessionLog.Action($"[perf] FleetWindow {totalMs:0.0}ms — list {tList * f:0.0} / ships {tShips * f:0.0}"
                        + $" / orders {tOrders * f:0.0} / tabs {tTabs * f:0.0} ms"
                        + $" (fleet '{(SelectedFleet != null ? SelectedFleet.GetName(factionID) : "none")}')");
                }
            }
        }

        private void DisplayTabs()
        {
            if(SelectedFleet == null) return;

            if(ImGui.BeginChild("FleetTabs"))
            {
                ImGui.BeginTabBar("FleetTabBar", ImGuiTabBarFlags.None);

                if(ImGui.BeginTabItem("Summary"))
                {
                    SessionLog.CurrentStage = "FleetWindow/Tabs/Summary";
                    Vector2 windowContentSize = ImGui.GetContentRegionAvail();
                    var firstChildSize = new Vector2(windowContentSize.X * 0.99f, windowContentSize.Y);
                    var secondChildSize = new Vector2(windowContentSize.X * 0.5f - (windowContentSize.X * 0.01f), windowContentSize.Y);
                    if (ImGui.BeginChild("FleetSummary1", firstChildSize, ImGuiChildFlags.Borders))
                    {
                        if (ImGui.CollapsingHeader("Fleet Information", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            ImGui.Columns(2);
                            DisplayHelpers.PrintRow("Name", SelectedFleet.GetName(factionID));

                            if (selectedFleetFlagship != null)
                            {
                                DisplayHelpers.PrintRow("Flagship", selectedFleetFlagship.GetName(factionID));

                                string commanderName = "None";
                                if (selectedFleetFlagship.TryGetDataBlob<ShipInfoDB>(out var shipInfoDB)
                                    && shipInfoDB.CommanderID != -1)
                                {
                                    if(shipInfoDB.OwningEntity != null && shipInfoDB.OwningEntity.Manager != null)
                                    {
                                        shipInfoDB.OwningEntity.Manager.TryGetEntityById(shipInfoDB.CommanderID, out var commanderEntity);
                                        commanderName = commanderEntity.GetName(factionID);
                                    }
                                }
                                DisplayHelpers.PrintRow("Commander", commanderName);
                            }
                            else
                            {
                                DisplayHelpers.PrintRow("Flagship", "-");
                                DisplayHelpers.PrintRow("Commander", "-");
                            }

                            // Current system
                            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                            ImGui.Text("Current System");
                            ImGui.PopStyleColor();
                            ImGui.NextColumn();
                            if (selectedFleetFlagship != null && selectedFleetSystem != null && selectedFleetFlagship.TryGetDataBlob<PositionDB>(out var positionDB))
                            {
                                StarSystem? starSystem = (StarSystem?)positionDB.OwningEntity?.Manager;
                                if (ImGui.SmallButton(starSystem?.NameDB.OwnersName ?? "Unknown"))
                                {
                                    if(starSystem != null)
                                        _uiState.SetActiveSystem(starSystem.ManagerID);
                                }
                                ImGui.NextColumn();
                                ImGui.Separator();

                                ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                                ImGui.Text("Orbiting");
                                ImGui.PopStyleColor();
                                ImGui.NextColumn();
                                // Find the first visible parent (hidden entities like surveyed anomalies shouldn't show)
                                var visibleParent = GetVisibleParent(positionDB, starSystem);
                                if (ImGui.SmallButton(visibleParent?.GetName(factionID) ?? "Unknown"))
                                {
                                    if(visibleParent != null && starSystem != null)
                                        _uiState.EntityClicked(visibleParent.Id, starSystem.ManagerID, MouseButtons.Primary);
                                }
                            }
                            else
                            {
                                ImGui.Text("Unknown");
                                ImGui.NextColumn();
                                ImGui.Separator();
                                ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                                ImGui.Text("Orbiting");
                                ImGui.PopStyleColor();
                                ImGui.NextColumn();
                                ImGui.Text("Unknown");
                            }
                            ImGui.NextColumn();
                            ImGui.Separator();
                            int shipCount = SelectedFleet.TryGetDataBlob<FleetDB>(out var summaryFleetDB)
                                ? summaryFleetDB.GetChildren().Count(x => !x.HasDataBlob<FleetDB>()) : 0;
                            DisplayHelpers.PrintRow("Ships", shipCount.ToString());
                        }
                        ImGui.Columns(1);
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    ImGui.EndTabItem();
                }

                DisplayCombatTab();

                if (ImGui.BeginTabItem("Issue Orders"))
                {
                    SessionLog.CurrentStage = "FleetWindow/Tabs/IssueOrders";
                    var size = ImGui.GetContentRegionAvail();
                    var firstChildSize = new Vector2(size.X * 0.27f, size.Y);
                    var secondChildSize = new Vector2(size.X * 0.73f - (size.X * 0.01f), size.Y);
                    if(ImGui.BeginChild("IssueOrders-List", firstChildSize, ImGuiChildFlags.Borders))
                    {
                        DisplayHelpers.Header("Available Orders");

                        if(ImGui.Selectable("Move to ...", selectedIssueOrderType == IssueOrderType.MoveTo))
                        {
                            selectedIssueOrderType = IssueOrderType.MoveTo;
                        }
                        if(ImGui.Selectable("Refuel at ...", selectedIssueOrderType == IssueOrderType.RefuelAt))
                        {
                            selectedIssueOrderType = IssueOrderType.RefuelAt;
                        }
                        if(SelectedFleet.HasGeoSurveyAbility() && ImGui.Selectable("Geo Survey ...", selectedIssueOrderType == IssueOrderType.GeoSurvey))
                        {
                            selectedIssueOrderType = IssueOrderType.GeoSurvey;
                        }
                        if(SelectedFleet.HasJPSurveyAbililty() && ImGui.Selectable("Grav Survey ...", selectedIssueOrderType == IssueOrderType.JPSurvey))
                        {
                            selectedIssueOrderType = IssueOrderType.JPSurvey;
                        }
                        if(ImGui.Selectable("Jump...", selectedIssueOrderType == IssueOrderType.Jump))
                        {
                            selectedIssueOrderType = IssueOrderType.Jump;
                        }
                        // Earthfall C5.1 — only offered when a ship in the fleet actually carries a troop/vehicle bay
                        // (mirrors the HasGeoSurveyAbility/HasJPSurveyAbililty gating above), so the order doesn't
                        // clutter the list for a fleet that can't lift ground units.
                        if(HasAnyTroopBay(SelectedFleet) && ImGui.Selectable("Embark / land troops ...", selectedIssueOrderType == IssueOrderType.Troops))
                        {
                            selectedIssueOrderType = IssueOrderType.Troops;
                        }
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    IssueOrdersDisplay(secondChildSize);
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Standing Orders"))
                {
                    SessionLog.CurrentStage = "FleetWindow/Tabs/StandingOrders";
                    var size = ImGui.GetContentRegionAvail();
                    var firstChildSize = new Vector2(size.X * 0.33f, size.Y);
                    var secondChildSize = new Vector2(size.X * 0.67f - (size.X * 0.01f), size.Y);
                    if(ImGui.BeginChild("StandingOrders-List", firstChildSize, ImGuiChildFlags.Borders))
                    {
                        var sizeAvailable = ImGui.GetContentRegionAvail();
                        DisplayHelpers.Header("Order List");
                        // if(selectedFleet.GetDataBlob<FleetDB>().Parent.Guid != factionID)
                        // {
                        //     if(ImGui.Checkbox("Inherit Orders###fleet-inherit-orders", ref selectedFleetInheritOrders))
                        //     {
                        //         var order = FleetOrder.ToggleInheritOrders(factionID, selectedFleet);
                        //         StaticRefLib.OrderHandler.HandleOrder(order);
                        //     }
                        //     if(ImGui.IsItemHovered())
                        //     {
                        //         ImGui.SetTooltip("If checked the fleet will inherit it's orders from the fleet above it in the command heirarchy.");
                        //     }
                        // }
                        if(selectedFleetDB?.StandingOrders.Count > 0)
                        {
                            var count = selectedFleetDB.StandingOrders.Count;
                            var orders = selectedFleetDB.StandingOrders.ToArray();
                            for(int i = 0; i < count; i++)
                            {
                                ImGui.PushID("###" + i);
                                bool isSelected = selectedOrder == orders[i];
                                var name = orders[i].Name.IsNullOrEmpty() ? "<un-named>" : orders[i].Name;
                                if(ImGui.Selectable((i + 1) + ". " + name, ref isSelected))
                                {
                                    SelectOrder(orders[i]);
                                }
                                if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                    ImGui.OpenPopup("orderctx");   // scoped by the PushID above; opened explicitly
                                if(ImGui.BeginPopup("orderctx"))
                                {
                                    if(i > 0 && ImGui.MenuItem("Move Up"))
                                    {
                                        var temp = selectedFleetDB.StandingOrders[i - 1];
                                        selectedFleetDB.StandingOrders[i - 1] = selectedFleetDB.StandingOrders[i];
                                        selectedFleetDB.StandingOrders[i] = temp;
                                    }
                                    if(i < count - 1 && ImGui.MenuItem("Move Down"))
                                    {
                                        var temp = selectedFleetDB.StandingOrders[i + 1];
                                        selectedFleetDB.StandingOrders[i + 1] = selectedFleetDB.StandingOrders[i];
                                        selectedFleetDB.StandingOrders[i] = temp;
                                    }
                                    if(ImGui.MenuItem("Delete Order"))
                                    {
                                        selectedFleetDB.StandingOrders.Remove(orders[i]);
                                        if(isSelected)
                                            SelectOrder(null);
                                    }
                                    ImGui.EndPopup();
                                }
                                ImGui.PopID();
                            }
                        }
                        else
                        {
                            ImGui.Text("No orders");
                        }

                        ImGui.SetCursorPosY(sizeAvailable.Y - 12f);
                        if(ImGui.Button("Create New Order", new Vector2(sizeAvailable.X, 0)))
                        {
                            var order = new ConditionalOrder();
                            selectedFleetDB?.StandingOrders.Add(order);

                            // if this is the first order, select it
                            if(selectedFleetDB?.StandingOrders.Count == 1)
                                SelectOrder(order);
                        }
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    if(ImGui.BeginChild("StandingOrders-edit", secondChildSize, ImGuiChildFlags.Borders) && selectedOrder != null)
                    {
                        var sizeAvailable = ImGui.GetContentRegionAvail();
                        DisplayHelpers.Header("Order Name");
                        ImGui.InputText("###order-name-input", orderNameBuffer, 32);
                        ImGui.NewLine();
                        DisplayHelpers.Header("Conditions", "If the conditions listed are true, the actions will execute.");

                        var count = selectedOrder.Condition.ConditionItems.Count;
                        var items = selectedOrder.Condition.ConditionItems.ToArray();
                        for(int i = 0; i < count; i++)
                        {
                            var conditionItem = items[i];
                            ImGui.PushID(conditionItem.UniqueID);
                            if(!orderConditionIndexes.ContainsKey(conditionItem)) orderConditionIndexes.Add(conditionItem, 0);
                            var index = orderConditionIndexes[conditionItem];
                            var condition = conditionItem.Condition;
                            ImGui.Button(OrderRegistry.ConditionDescriptions[conditionItem.Condition.GetType()], new Vector2(Math.Max(sizeAvailable.X * 0.4f, 128f), 0f));

                            switch(condition.DisplayType)
                            {
                                case ConditionDisplayType.Comparison:
                                    ComparisonCondition comparisonCondition = (ComparisonCondition)condition;
                                    int value = (int)comparisonCondition.Threshold;
                                    int comparisonIndex = Array.IndexOf(orderComparisons, comparisonCondition.ComparisionType.ToDescription());
                                    ImGui.SameLine();
                                    ImGui.SetNextItemWidth(Math.Max(sizeAvailable.X * 0.075f, 16f));
                                    if(ImGui.Combo("###orderComparison", ref comparisonIndex, orderComparisons, orderComparisons.Length))
                                    {
                                        ComparisonType? comparisonType = (ComparisonType?)Enum.GetValues(typeof(ComparisonType)).GetValue(comparisonIndex);
                                        if(comparisonType != null)
                                            comparisonCondition.ComparisionType = comparisonType.Value;
                                    }
                                    ImGui.SameLine();
                                    ImGui.SetNextItemWidth(Math.Max(sizeAvailable.X * 0.15f, 32f));
                                    if(ImGui.InputInt(comparisonCondition.Description + "###orderValue", ref value, 1, 5))
                                    {
                                        if(value < comparisonCondition.MinValue) value = (int)comparisonCondition.MinValue;
                                        if(value > comparisonCondition.MaxValue) value = (int)comparisonCondition.MaxValue;

                                        comparisonCondition.Threshold = value;
                                    }
                                    break;
                            }

                            // Show the logical operators UI on all but the last item
                            ImGui.SameLine();
                            var position = ImGui.GetCursorPos();
                            if(i < count - 1)
                            {
                                if(conditionItem.LogicalOperation == null)
                                    conditionItem.LogicalOperation = LogicalOperation.And;

                                ImGui.SetCursorPosY(position.Y + 12f);
                                if(conditionItem.LogicalOperation == LogicalOperation.And)
                                {
                                    ImGui.SetCursorPosX(sizeAvailable.X - 82f);
                                    if(ImGui.Button("AND"))
                                    {
                                        conditionItem.LogicalOperation = LogicalOperation.Or;
                                    }
                                }
                                else
                                {
                                    ImGui.SetCursorPosX(sizeAvailable.X - 48f);
                                    if(ImGui.Button("OR"))
                                    {
                                        conditionItem.LogicalOperation = LogicalOperation.And;
                                    }
                                }
                            }
                            ImGui.SameLine();
                            ImGui.SetCursorPos(position);
                            ImGui.SetCursorPosX(sizeAvailable.X - 12f);
                            if(ImGui.Button("x"))
                            {
                                selectedOrder.Condition.ConditionItems.Remove(conditionItem);
                            }
                            ImGui.PopID();
                        }

                        if(ImGui.Button("Add Condition"))
                        {
                            if(orderConditionsIndex >= 0 && orderConditionsIndex < orderConditionDescriptions.Length)
                            {
                                ConditionItem item = OrderRegistry.Conditions[orderConditionDescriptions[orderConditionsIndex]]();
                                selectedOrder.Condition.ConditionItems.Add(item);
                            }
                        }
                        ImGui.SameLine();
                        if(ImGui.Combo("###order-add-condition-list", ref orderConditionsIndex, orderConditionDescriptions, orderConditionDescriptions.Length))
                        {
                        }

                        ImGui.NewLine();
                        DisplayHelpers.Header("Actions", "The actions listed will execute in the order in which they are listed.");

                        foreach(var action in selectedOrder.Actions.ToArray())
                        {
                            DisplayActionItem(action);
                        }

                        if(ImGui.Button("Add Action"))
                        {
                            if(orderActionsIndex >= 0 && orderActionsIndex < orderActionDescriptions.Length)
                            {
                                var selectedAction = OrderRegistry.Actions[orderActionDescriptions[orderActionsIndex]](factionID, SelectedFleet);
                                selectedOrder.Actions.Add(selectedAction);
                            }
                        }
                        ImGui.SameLine();
                        if(ImGui.Combo("###order-add-action-list", ref orderActionsIndex, orderActionDescriptions, orderActionDescriptions.Length))
                        {
                        }

                        ImGui.SetCursorPosY(sizeAvailable.Y - 12f);
                        if(ImGui.Button("Save", new Vector2(sizeAvailable.X, 0)))
                        {
                            string name = Utils.StringFromBytes(orderNameBuffer);
                            if(name.IsNotNullOrEmpty())
                            {
                                selectedOrder.Name = name;
                            }
                        }
                    }
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            ImGui.EndChild();
        }

        // ───────────────────────────── Combat tab ─────────────────────────────
        // Everything a fleet (or a selected sub-fleet "component") brings to and does in a battle, in one place:
        // its live engagement status, the doctrine lever, and the per-ship combat sheet. Reads the combat
        // DataBlobs the auto-resolve engine maintains (ShipCombatValueDB / FleetDoctrineDB / FleetCombatStateDB /
        // FleetRetreatDB) and drives doctrine through FleetDoctrine.TrySetDoctrine (a direct call, NOT an order —
        // so it works even while the engagement lock has the fleet's regular orders frozen). All reads are
        // defensive (TryGet + IsValid + snapshot-to-array) because the background combat processor mutates this
        // state on another thread.
        private void DisplayCombatTab()
        {
            if (!ImGui.BeginTabItem("Combat")) return;
            SessionLog.CurrentStage = "FleetWindow/Tabs/Combat";

            if (SelectedFleet != null && selectedFleetDB != null)
            {
                if (ImGui.BeginChild("CombatTab"))
                {
                    DisplayCombatStatus();
                    ImGui.Separator();
                    DisplayDoctrineSelector();
                    ImGui.Separator();
                    DisplayEmconSelector();
                    ImGui.Separator();
                    DisplayEngagementPostureSelector();
                    ImGui.Separator();
                    DisplayEngageButton();
                    ImGui.Separator();
                    DisplayFleetCombatSheet();
                }
                ImGui.EndChild();
            }
            ImGui.EndTabItem();
        }

        // The ATTACK lever — the player's "go get them" when two fleets sit in range doing nothing (one holding fire,
        // or an enemy that broke off so the auto-trigger won't re-grab it). Calls the engine OrderAttackNearestHostile
        // (a DIRECT call, like doctrine/EMCON), which clears any retreat, flips this fleet Weapons Free, and forces the
        // fight now — the resolver/closing model then closes to weapons range and fights. v1 targets the NEAREST
        // hostile; picking a specific enemy fleet by map-click is the follow-up.
        private string _engageMsg = "";
        private void DisplayEngageButton()
        {
            DisplayHelpers.Header("Engage",
                "Order this fleet to ATTACK the nearest hostile fleet in the system NOW — clears any retreat, sets Weapons Free, and forces the fight (it will close to weapons range first if there's a gap). For when two fleets sit in range staring at each other.");
            if (ImGui.Button("Attack nearest hostile fleet"))
            {
                var target = Pulsar4X.Combat.CombatEngagement.OrderAttackNearestHostile(SelectedFleet);
                _engageMsg = target != null
                    ? "Attacking '" + target.GetName(factionID) + "'"
                    : "No hostile fleet in this system to attack.";
                SessionLog.Action($"[attack] {(SelectedFleet != null ? SelectedFleet.GetName(factionID) : "fleet")} -> "
                    + (target != null ? $"'{target.GetName(factionID)}' #{target.Id}" : "no hostile in system"));
            }
            if (!string.IsNullOrEmpty(_engageMsg))
                ImGui.TextWrapped(_engageMsg);
        }

        // The live battle readout: is this fleet fighting, who is it fighting, how many ships has it lost, and how
        // much not-yet-lethal damage is pooled against it. Absent any combat blob it just reads "Not engaged".
        private void DisplayCombatStatus()
        {
            if (SelectedFleet == null) return;
            DisplayHelpers.Header("Status");

            if (SelectedFleet.TryGetDataBlob<FleetCombatStateDB>(out var combat))
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), $"● IN COMBAT — salvo {combat.StepsFought}");

                string oppName = "an enemy fleet";
                if (combat.OpponentFleetId != -1 && SelectedFleet.Manager != null
                    && SelectedFleet.Manager.TryGetEntityById(combat.OpponentFleetId, out var opp) && opp.IsValid)
                {
                    int oppShips = opp.TryGetDataBlob<FleetDB>(out var oppDB)
                        ? oppDB.GetChildren().Count(x => x.IsValid && !x.HasDataBlob<FleetDB>()) : 0;
                    oppName = $"{opp.GetName(factionID)} ({oppShips} ships)";
                }
                ImGui.Text($"Engaging: {oppName}");

                // A ship killed mid-battle lingers in the child list with IsValid=false until cleanup, so filter it.
                int started = combat.InitialShipCount;
                int alive = selectedFleetDB.GetChildren().Count(x => x.IsValid && !x.HasDataBlob<FleetDB>());
                ImGui.Text($"Ships: {alive} of {started} (lost {Math.Max(0, started - alive)})");
                ImGui.TextDisabled($"Incoming damage pool: {combat.DamageTakenPool:N0} J");

                ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                ImGui.TextWrapped("Regular orders are locked while engaged — only a doctrine change applies. Set the fight up, then steer it with doctrine.");
                ImGui.PopStyleColor();
            }
            else if (SelectedFleet.TryGetDataBlob<FleetRetreatDB>(out _))
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Broke off the last engagement (withdrew).");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Not engaged.");
            }
        }

        // The doctrine lever: shows the active posture, and lets the player pick a new one from the moddable
        // catalog. The Set button greys out with a game-time countdown while the switch cooldown runs.
        private void DisplayDoctrineSelector()
        {
            if (SelectedFleet == null || _uiState.Game == null) return;
            DisplayHelpers.Header("Doctrine", "The fleet's combat posture. Applied as a read-time multiplier on strength/toughness — reversible, and switchable mid-battle.");

            SelectedFleet.TryGetDataBlob<FleetDoctrineDB>(out var active);
            if (active != null)
            {
                ImGui.Text($"Current: {active.DoctrineId}  [{active.Family}]");
                ImGui.TextDisabled($"Firepower x{active.FirepowerMult:0.##}, Toughness x{active.ToughnessMult:0.##}, Speed x{active.SpeedMult:0.##}{(active.IsRetreat ? "  · WITHDRAW" : "")}");
            }
            else
            {
                ImGui.Text("Current: none (neutral x1.0)");
            }

            SyncDoctrines();
            if (_doctrineNames.Length == 0)
            {
                ImGui.TextDisabled("No doctrines in the catalog.");
                return;
            }

            ImGui.SetNextItemWidth(Math.Max(ImGui.GetContentRegionAvail().X * 0.5f, 160f));
            ImGui.Combo("###doctrine-combo", ref _selectedDoctrine, _doctrineNames, _doctrineNames.Length);

            // Game-time cooldown gate. now comes from the fleet's star system; if the fleet has no manager yet
            // (shouldn't happen for a real fleet) we can't time the cooldown, so disable the switch.
            bool haveTime = SelectedFleet.Manager != null;
            DateTime now = haveTime ? SelectedFleet.StarSysDateTime : DateTime.MinValue;
            bool onCooldown = haveTime && active != null && now < active.SwitchableAfter;
            double secsLeft = onCooldown ? (active.SwitchableAfter - now).TotalSeconds : 0;

            ImGui.SameLine();
            if (!haveTime || onCooldown) ImGui.BeginDisabled();
            if (ImGui.Button("Set Doctrine"))
            {
                var bp = _doctrineBlueprints[_selectedDoctrine];
                bool ok = FleetDoctrine.TrySetDoctrine(SelectedFleet, bp, now);
                _doctrineStatus = ok ? $"Set posture: {bp.DisplayName}" : "On cooldown — can't switch yet.";
                Console.WriteLine($"[FleetCombat] Set doctrine '{bp.UniqueID}' on fleet {SelectedFleet.Id}: {(ok ? "OK" : "blocked (cooldown)")}");
                Console.Out.Flush();
            }
            if (!haveTime || onCooldown) ImGui.EndDisabled();
            if (onCooldown)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"switch in {secsLeft:0}s (game time)");
            }

            // Preview the highlighted posture's effects, so the player sees the trade before committing.
            var sel = _doctrineBlueprints[_selectedDoctrine];
            ImGui.TextDisabled($"{sel.DisplayName} [{sel.Family}] — Firepower x{sel.FirepowerMult:0.##}, Toughness x{sel.ToughnessMult:0.##}, Speed x{sel.SpeedMult:0.##}{(sel.IsRetreat ? ", WITHDRAW posture" : "")}, cooldown {sel.CooldownSeconds:0}s");

            if (!string.IsNullOrEmpty(_doctrineStatus))
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _doctrineStatus);
        }

        // The EMCON lever (detection slice 3b): run hot / cruise / go dark. Sets the fleet's posture, which scales
        // every member ship's EMITTED signature — how far off you can be detected. Mirrors the doctrine selector;
        // a DIRECT call (FleetEmcon.SetPosture, not an order), so it works mid-battle. Note: going Silent does NOT
        // hide a hot drive plume — the activity processor still lights you up when you thrust or fire.
        private void DisplayEmconSelector()
        {
            if (SelectedFleet == null || _uiState.Game == null) return;
            DisplayHelpers.Header("EMCON Posture", "How loud the fleet runs. Full = as designed; Silent = go dark (harder to detect) at the cost of running cold. Hard activity — thrust, weapons fire — still lights you up regardless of posture.");

            var posture = FleetEmcon.PostureOf(SelectedFleet);
            double mult = FleetEmcon.MultiplierOf(SelectedFleet);
            ImGui.Text($"Current: {posture}  (signature x{mult:0.##})");

            ImGui.SetNextItemWidth(Math.Max(ImGui.GetContentRegionAvail().X * 0.5f, 160f));
            ImGui.Combo("###emcon-combo", ref _selectedEmconPosture, _emconPostureNames, _emconPostureNames.Length);

            ImGui.SameLine();
            if (ImGui.Button("Set Posture"))
            {
                var chosen = _emconPostures[_selectedEmconPosture];
                FleetEmcon.SetPosture(SelectedFleet, chosen);
                _emconStatus = $"Set EMCON: {chosen} (signature x{FleetEmcon.MultiplierFor(chosen):0.##})";
                Console.WriteLine($"[FleetCombat] Set EMCON posture '{chosen}' on fleet {SelectedFleet.Id}");
                Console.Out.Flush();
            }

            // Preview the highlighted posture's signature scale, so the player sees the trade before committing.
            var sel = _emconPostures[_selectedEmconPosture];
            ImGui.TextDisabled($"{sel} — emitted signature x{FleetEmcon.MultiplierFor(sel):0.##}");

            if (!string.IsNullOrEmpty(_emconStatus))
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _emconStatus);
        }

        // The engagement-posture lever (closing P3): whether the fleet will OPEN a fight. The player's half of the
        // first-shot rule — only bites when the "First-shot trigger" flag (RequireWeaponsReleaseToEngage) is on.
        // Mirrors the EMCON/doctrine selectors: a DIRECT call (FleetDoctrine.SetEngagementPosture, not an order), so
        // it works mid-battle. Defensive: PostureOf returns WeaponsFree for a fleet with no doctrine blob.
        private void DisplayEngagementPostureSelector()
        {
            if (SelectedFleet == null || _uiState.Game == null) return;
            DisplayHelpers.Header("Engagement Posture", "Whether this fleet STARTS a fight. Weapons Free = open fire on a detected enemy in range; Hold Fire = never shoot first (two hold-fire fleets sit in a tense standoff); Return Fire = won't start it, but shoots back once fired upon. Only takes effect when the 'First-shot trigger' is enabled (DevTools).");

            var posture = FleetDoctrine.PostureOf(SelectedFleet);
            ImGui.Text($"Current: {posture}");

            ImGui.SetNextItemWidth(Math.Max(ImGui.GetContentRegionAvail().X * 0.5f, 160f));
            ImGui.Combo("###engage-combo", ref _selectedEngagePosture, _engagePostureNames, _engagePostureNames.Length);

            ImGui.SameLine();
            if (ImGui.Button("Set Engagement"))
            {
                var chosen = _engagePostures[_selectedEngagePosture];
                FleetDoctrine.SetEngagementPosture(SelectedFleet, chosen);
                _engageStatus = $"Set engagement posture: {chosen}";
                Console.WriteLine($"[FleetCombat] Set engagement posture '{chosen}' on fleet {SelectedFleet.Id}");
                Console.Out.Flush();
            }

            if (!string.IsNullOrEmpty(_engageStatus))
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _engageStatus);
        }

        // ── Range rings on the map ────────────────────────────────────────────────────────────────────────────
        // Draws each selected-fleet ship's beam reach (red) and sensor reach (green) as circles on the system map,
        // so "how close to shoot / how far I can see" is visible in space, not just as a number in the table. Uses
        // the existing SimpleCircle widget + UIWidgets dict — the exact mechanism DebugWindow's "Draw SOI" uses, so
        // no new SDL drawing code. Radius is in AU (SimpleCircle's unit), converted from the engine's metres.
        private bool _showRangeRings;
        private readonly List<string> _rangeRingKeys = new();
        private FleetDB? _ringsFleet;   // which fleet's ships currently own rings — rebuild when this changes
        private Entity? _ringsTarget;   // which enemy the detection bubble is drawn for — rebuild when selection changes
        private double _ringsActivity = double.NaN; // the fleet's loudness when the rings were built — rebuild on change

        // Per-ship range cache for the combat sheet. The range accessors walk component/sensor datablobs, and the
        // sheet redraws EVERY frame (ImGui immediate mode) — so without this we re-walked every ship every frame.
        // Compute once per fleet selection AND whenever the fleet's loudness changes (so an EMCON Silent/Full flip
        // refreshes the detectability number + amber ring live — the developer's "I went Silent and nothing moved"
        // report; the engine DOES drop the signature, the readout just wasn't re-reading it).
        private FleetDB? _rangeCacheFleet;
        private double _rangeCacheActivity = double.NaN;
        private readonly Dictionary<int, (double beam, double reach, double detect)> _shipRangeCache = new();

        // Sum of member ships' live emitted-signature activity (EMCON posture × heat). Discrete + cheap (a field read
        // per ship), rounded to swallow float jitter. Changes when the player flips EMCON or a ship runs hot/cold —
        // the trigger to recompute "how far we can BE SEEN" (detectability + amber ring). Sensor REACH (green) and
        // beam reach (red) don't depend on it, but they're recomputed in the same pass — cheap for a fleet's ships.
        private double FleetActivityFingerprint()
        {
            if (selectedFleetDB == null) return 0;
            double sum = 0;
            foreach (var ship in selectedFleetDB.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()))
                sum += SensorTools.CurrentActivityMultiplier(ship);
            return Math.Round(sum, 3);
        }

        private void EnsureRangeCache()
        {
            double activity = FleetActivityFingerprint();
            if (ReferenceEquals(_rangeCacheFleet, selectedFleetDB) && activity == _rangeCacheActivity) return;
            _rangeCacheFleet = selectedFleetDB;
            _rangeCacheActivity = activity;
            _shipRangeCache.Clear();
            if (selectedFleetDB == null) return;
            foreach (var ship in selectedFleetDB.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()))
                _shipRangeCache[ship.Id] = (
                    WeaponUtils.GetMaxBeamRange_m(ship),        // beam reach (how far it can SHOOT)
                    SensorTools.SensorReachRange_m(ship),       // sensor reach (how far it can SEE — stable vs your EMCON)
                    SensorTools.DetectabilityRange_m(ship));    // detectability (how far it can BE SEEN — moves with activity)
        }

        private (double beam, double reach, double detect) ShipRanges(int shipId)
            => _shipRangeCache.TryGetValue(shipId, out var v) ? v : (0, 0, 0);

        private void ClearRangeRings()
        {
            var render = _uiState.SelectedSysMapRender;
            if (render != null)
                foreach (var key in _rangeRingKeys)
                    render.UIWidgets.Remove(key);
            _rangeRingKeys.Clear();
            _ringsFleet = null;
            _ringsTarget = null;
        }

        private void BuildRangeRings()
        {
            ClearRangeRings();
            var render = _uiState.SelectedSysMapRender;
            if (render == null || selectedFleetDB == null) return;
            _ringsFleet = selectedFleetDB;
            _ringsTarget = _uiState.LastClickedEntity?.Entity;
            _ringsActivity = FleetActivityFingerprint();   // baseline loudness; rebuild when EMCON changes it

            // THREE rings PER FLEET (not per ship): the fleet moves and fights as one, so draw one ring of each
            // kind sized off the ship with the HIGHEST of that range (max beam reach / max sensor reach / max
            // detectability), centred on the fleet's representative position (its first ship). This is both the
            // clean readout AND the perf fix — 3 circles regardless of fleet size, instead of 3×N.
            double maxBeam = 0, maxReach = 0, maxDetect = 0;
            PositionDB center = null;
            foreach (var ship in selectedFleetDB.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()))
            {
                if (center == null && ship.HasDataBlob<PositionDB>()) center = ship.GetDataBlob<PositionDB>();
                double beam = WeaponUtils.GetMaxBeamRange_m(ship);
                double reach = SensorTools.SensorReachRange_m(ship);
                double detect = SensorTools.DetectabilityRange_m(ship);
                if (beam > maxBeam) maxBeam = beam;
                if (reach > maxReach) maxReach = reach;
                if (detect > maxDetect) maxDetect = detect;
            }
            if (center != null)
            {
                void Ring(string suffix, double range_m, byte r, byte g, byte b, byte a)
                {
                    if (range_m <= 0) return;
                    string key = "rangering_" + suffix;
                    render.UIWidgets[key] = new SimpleCircle(center, Pulsar4X.Orbital.Distance.MToAU(range_m),
                        new SDL3.SDL.Color { R = r, G = g, B = b, A = a });
                    _rangeRingKeys.Add(key);
                }
                Ring("beam", maxBeam, 225, 90, 70, 90);     // red: fleet's longest reach to SHOOT
                Ring("sensor", maxReach, 80, 210, 110, 70); // green: fleet's widest reach to SEE
                Ring("detect", maxDetect, 240, 160, 40, 80);// amber: fleet's loudest ship = how far it can BE SEEN
            }

            // Detection bubble vs the currently-selected ENEMY: one cyan ring around that target = how far the
            // fleet's BEST sensor picks it up (your ship inside the ring → you see it). Range reads the target's
            // real signature, so the bubble shrinks when the enemy goes dark — the honest, target-specific version
            // of the self-ring. LastClickedEntity is the player's current map selection.
            var target = _uiState.LastClickedEntity?.Entity;
            if (target != null && target.IsValid && target.FactionOwnerID != factionID
                && target.HasDataBlob<SensorProfileDB>() && target.HasDataBlob<PositionDB>())
            {
                double bestReach = 0;
                foreach (var ship in selectedFleetDB.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()))
                {
                    double r = SensorTools.DetectionRangeAgainst(ship, target);
                    if (r > bestReach) bestReach = r;
                }
                if (bestReach > 0)
                {
                    string key = "rangering_target_" + target.Id;
                    render.UIWidgets[key] = new SimpleCircle(target.GetDataBlob<PositionDB>(), Pulsar4X.Orbital.Distance.MToAU(bestReach),
                        new SDL3.SDL.Color { R = 90, G = 215, B = 215, A = 100 });   // cyan: the enemy's detectability bubble
                    _rangeRingKeys.Add(key);
                    SessionLog.Action($"[range-ring] detection bubble vs '{target.GetName(factionID)}' #{target.Id}: fleet best sensor reach {Stringify.Distance(bestReach)}");
                }
            }

            // Live-test gauge (CI is blind to the SDL client): name what the ring build actually did, so the
            // developer's play-test log shows whether the wire found ships + ranges. 0 rings when rings were
            // expected = the wire, not the toggle, is the bug.
            SessionLog.Action($"[range-ring] built {_rangeRingKeys.Count} ring(s) for the selected fleet"
                + (target != null && target.FactionOwnerID != factionID ? $" (+ bubble vs #{target.Id})" : ""));
        }

        // The combat sheet: fleet totals + firepower-by-weapon-type + the per-ship table ("the table IS the
        // interface", per COMBAT-DESIGN System 4). Reads each ship's cached ShipCombatValueDB.
        private void DisplayFleetCombatSheet()
        {
            if (selectedFleetDB == null) return;
            DisplayHelpers.Header("Combat Strength");

            // Range-ring toggle: beam reach + sensor reach drawn on the map for this fleet's ships. Rebuilt when the
            // player switches fleet/target OR when the fleet's loudness changes (EMCON Silent/Full), so the amber
            // detectability ring shrinks/grows live with the posture instead of needing a re-toggle.
            if (ImGui.Checkbox("Show range rings on map", ref _showRangeRings))
            {
                if (_showRangeRings) BuildRangeRings();
                else ClearRangeRings();
            }
            if (_showRangeRings && (!ReferenceEquals(_ringsFleet, selectedFleetDB)
                    || !ReferenceEquals(_ringsTarget, _uiState.LastClickedEntity?.Entity)
                    || FleetActivityFingerprint() != _ringsActivity))
                BuildRangeRings();

            EnsureRangeCache();   // walk component/sensor blobs once per fleet selection, not per frame
            var ships = selectedFleetDB.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()).ToArray();
            double totalFp = 0, totalTough = 0;
            int combatants = 0;
            double fleetBeamReach = 0;   // the fleet's longest beam reach — how close it must get to open fire
            double fleetSensorReach = 0; // the fleet's widest sensor reach — how far it can SEE (max = sensors parallel)
            double fleetDetect = 0;      // the fleet's LOUDEST ship's detectability — how far the fleet can BE SEEN
            var classDps = new Dictionary<WeaponClass, double>();
            foreach (var ship in ships)
            {
                if (!ship.IsValid) continue;
                var (beam, reach, detect) = ShipRanges(ship.Id);   // cached (was a per-frame engine walk)
                if (beam > fleetBeamReach) fleetBeamReach = beam;
                if (reach > fleetSensorReach) fleetSensorReach = reach;
                if (detect > fleetDetect) fleetDetect = detect;
                if (!ship.TryGetDataBlob<ShipCombatValueDB>(out var cv)) continue;
                totalFp += cv.Firepower;
                totalTough += cv.Toughness;
                if (cv.Firepower > 0) combatants++;
                if (cv.Weapons != null)
                    foreach (var w in cv.Weapons)
                    {
                        classDps.TryGetValue(w.Class, out var d);
                        classDps[w.Class] = d + w.DamagePerSecond;
                    }
            }

            ImGui.Columns(2);
            DisplayHelpers.PrintRow("Ships", ships.Length.ToString());
            DisplayHelpers.PrintRow("Combatants", combatants.ToString());
            DisplayHelpers.PrintRow("Total firepower", $"{totalFp:N0} J/s");
            DisplayHelpers.PrintRow("Total toughness", $"{totalTough:N0} J");
            // Engagement range: the longest beam reach in the fleet. This is the distance the firing processor
            // enforces (a weapon won't fire past it), now visible so the player knows how close to close.
            DisplayHelpers.PrintRow("Beam reach", fleetBeamReach > 0 ? Stringify.Distance(fleetBeamReach) : "—");
            // The two sensor numbers, kept distinct: "Sensor reach" = how far the fleet can SEE (does NOT move with
            // your EMCON); "Detectable at" = how far the fleet can BE SEEN (its loudest ship), which DOES move with
            // what you're doing — go Silent / stop burning and it shrinks.
            DisplayHelpers.PrintRow("Sensor reach", fleetSensorReach > 0 ? Stringify.Distance(fleetSensorReach) : "—");
            DisplayHelpers.PrintRow("Detectable at", fleetDetect > 0 ? Stringify.Distance(fleetDetect) : "—");
            ImGui.Columns(1);

            // Why the detectability number is what it is: the loudest ship's live activity scale (EMCON × heat).
            var loudest = ships.Where(sh => sh.IsValid)
                .OrderByDescending(sh => SensorTools.CurrentActivityMultiplier(sh)).FirstOrDefault();
            if (loudest != null && loudest.IsValid)
            {
                double act = SensorTools.CurrentActivityMultiplier(loudest);
                string how = act > 1.01 ? "running HOT (thrusting / firing)" : act < 0.99 ? "running quiet" : "as designed";
                ImGui.TextDisabled($"Signature activity x{act:0.00} — {how}. Set EMCON above / stop burning to go quieter.");
            }
            ImGui.TextDisabled("Rings: red = your reach (shoot), green = you can SEE, amber = you can BE SEEN.");

            if (classDps.Count > 0)
            {
                ImGui.TextDisabled("Firepower by weapon type:");
                foreach (var kv in classDps)
                    ImGui.BulletText($"{kv.Key}: {kv.Value:N0} J/s");
            }

            var subFleets = selectedFleetDB.GetChildren().Count(c => c.HasDataBlob<FleetDB>());
            if (subFleets > 0)
                ImGui.TextDisabled($"+ {subFleets} component(s) — select one in the tree to set its own doctrine.");

            ImGui.Separator();
            DisplayHelpers.Header("Ships");
            if (ships.Length == 0)
            {
                ImGui.Text("No ships in this fleet.");
                return;
            }
            if (ImGui.BeginTable("CombatShipTable", 8, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Ship", ImGuiTableColumnFlags.None, 0.20f);
                ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.None, 0.11f);
                ImGui.TableSetupColumn("Firepower J/s", ImGuiTableColumnFlags.None, 0.14f);
                ImGui.TableSetupColumn("Toughness J", ImGuiTableColumnFlags.None, 0.14f);
                ImGui.TableSetupColumn("Evasion", ImGuiTableColumnFlags.None, 0.07f);
                ImGui.TableSetupColumn("Beam Range", ImGuiTableColumnFlags.None, 0.11f);
                ImGui.TableSetupColumn("Can See", ImGuiTableColumnFlags.None, 0.11f);
                ImGui.TableSetupColumn("Seen At", ImGuiTableColumnFlags.None, 0.11f);
                ImGui.TableHeadersRow();
                foreach (var ship in ships)
                {
                    if (!ship.IsValid) continue;
                    ImGui.TableNextColumn(); ImGui.Text(ship.GetName(factionID));
                    if (ship.TryGetDataBlob<ShipCombatValueDB>(out var cv))
                    {
                        ImGui.TableNextColumn(); ImGui.Text(cv.Firepower > 0 ? "Combatant" : "Utility");
                        ImGui.TableNextColumn(); ImGui.Text($"{cv.Firepower:N0}");
                        ImGui.TableNextColumn(); ImGui.Text($"{cv.Toughness:N0}");
                        ImGui.TableNextColumn(); ImGui.Text($"{cv.Evasion:0.00}");
                    }
                    else
                    {
                        ImGui.TableNextColumn(); ImGui.TextDisabled("—");
                        ImGui.TableNextColumn(); ImGui.TextDisabled("—");
                        ImGui.TableNextColumn(); ImGui.TextDisabled("—");
                        ImGui.TableNextColumn(); ImGui.TextDisabled("—");
                    }
                    // Beam reach (how far it can SHOOT), "Can See" (sensor reach — stable vs your own EMCON), and
                    // "Seen At" (detectability — how far this ship can BE detected, which moves with what it's
                    // doing). From the per-fleet cache (was a per-frame engine walk); "—" when none.
                    var (beam, reach, detect) = ShipRanges(ship.Id);
                    ImGui.TableNextColumn(); ImGui.Text(beam > 0 ? Stringify.Distance(beam) : "—");
                    ImGui.TableNextColumn(); ImGui.Text(reach > 0 ? Stringify.Distance(reach) : "—");
                    ImGui.TableNextColumn(); ImGui.Text(detect > 0 ? Stringify.Distance(detect) : "—");
                }
                ImGui.EndTable();
            }
        }

        // Rebuilds the doctrine dropdown from the moddable catalog only when its size changes (cheap to call
        // every frame). Same lazy-sync pattern as DevToolsWindow's ship-design / faction lists.
        private void SyncDoctrines()
        {
            if (_uiState.Game == null) return;
            var catalog = _uiState.Game.StartingGameData.CombatDoctrines;
            if (_doctrineBlueprints.Length == catalog.Count) return;

            _doctrineBlueprints = catalog.Values.ToArray();
            _doctrineNames = _doctrineBlueprints.Select(d => $"{d.DisplayName} [{d.Family}]").ToArray();
            if (_selectedDoctrine >= _doctrineNames.Length) _selectedDoctrine = 0;
        }

        // ═══════════════════════════════ BATTALIONS (Earthfall C3.1) ═══════════════════════════════
        // Ground formations as first-class citizens BESIDE fleets. This tab is a CROSS-BODY registry: it enumerates
        // ALL the player's ground formations across every known world (the engine has no cross-body helper yet, so
        // this mirrors the ship-enumeration precedent — walk _uiState.StarSystemStates → each system's bodies with a
        // GroundForcesDB → GroundFormationTools.FormationsFor). It shows a location/strength/health/reach/stance/ROE
        // table with filters, and — on selection — the SAME order surface PlanetViewWindow.DrawFormationPanel gives a
        // single world (march to region / queue waypoints / stance / ROE), plus a jump to that world's tactical map.
        //
        // THIN + DEFENSIVE by the client discipline: every value is read off CI-tested engine blobs, and every
        // mutation goes through a CI-tested engine API on an explicit player click (GroundForces.OrderFormationMove /
        // QueueFormationOrder / ClearFormationOrders, GroundFormationDoctrine.TrySetStance / SetEngagementStance) — the
        // exact calls PlanetViewWindow already makes. No new client logic, nothing hard-indexed.

        // Filters (rebuilt each frame from what's present).
        private int _battSystemFilter = 0;   // 0 = all systems; else 1-based index into the frame's systems list
        private int _battBodyFilter = 0;     // 0 = all worlds;  else 1-based index into the frame's bodies list
        private bool _battHasOrdersOnly = false;

        // Selection — a formation is identified across bodies by (body id, formation id).
        private int _selBattalionBodyId = -1;
        private int _selBattalionFormationId = -1;
        private int _battStanceChoice = 0;
        private string _battStatus = "";
        private bool _battErrorLogged;

        private void DisplayBattalions()
        {
            if(_uiState.Faction == null) { ImGui.TextDisabled("No faction loaded."); return; }
            int myFaction = _uiState.Faction.Id;

            DisplayHelpers.Header("Battalions",
                "Your ground formations across every world — the ground echo of the fleet list. Select one to command it.");

            // Gather every player formation across all known systems/bodies (pure client; mirrors the ship-enumeration
            // precedent in SystemMapRendering — the engine's AllFormationsFor helper is a GROUND follow-up).
            var all = new List<(Entity body, StarSystem system, GroundForcesDB forces, GroundFormation formation)>();
            foreach(var (_, sysState) in _uiState.StarSystemStates)
            {
                var system = sysState?.StarSystem;
                if(system == null) continue;
                foreach(var body in system.GetAllEntitiesWithDataBlob<GroundForcesDB>())
                {
                    if(!body.TryGetDataBlob<GroundForcesDB>(out var forces)) continue;
                    foreach(var f in GroundFormationTools.FormationsFor(forces, myFaction))
                        all.Add((body, system, forces, f));
                }
            }

            if(all.Count == 0)
            {
                ImGui.TextDisabled("No battalions yet. Raise ground units and form them up on a world (Planet View)");
                ImGui.TextDisabled("to command them here.");
                return;
            }

            // ── Filters: system / body / has-orders ──
            var systems = all.Select(r => r.system).Distinct().ToList();
            string[] systemNames = new string[systems.Count + 1];
            systemNames[0] = "All systems";
            for(int i = 0; i < systems.Count; i++)
                systemNames[i + 1] = systems[i].NameDB?.OwnersName ?? "Unknown";
            if(_battSystemFilter >= systemNames.Length) _battSystemFilter = 0;
            ImGui.SetNextItemWidth(180f);
            ImGui.Combo("System##battsys", ref _battSystemFilter, systemNames, systemNames.Length);
            StarSystem systemFilter = _battSystemFilter > 0 ? systems[_battSystemFilter - 1] : null;

            var bodies = all.Where(r => systemFilter == null || r.system == systemFilter)
                            .Select(r => r.body).Distinct().ToList();
            string[] bodyNames = new string[bodies.Count + 1];
            bodyNames[0] = "All worlds";
            for(int i = 0; i < bodies.Count; i++)
                bodyNames[i + 1] = bodies[i].GetName(myFaction);
            if(_battBodyFilter >= bodyNames.Length) _battBodyFilter = 0;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160f);
            ImGui.Combo("World##battbody", ref _battBodyFilter, bodyNames, bodyNames.Length);
            Entity bodyFilter = _battBodyFilter > 0 ? bodies[_battBodyFilter - 1] : null;

            ImGui.SameLine();
            ImGui.Checkbox("With orders only##battorders", ref _battHasOrdersOnly);

            if(!string.IsNullOrEmpty(_battStatus))
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _battStatus);

            var rows = all.Where(r =>
                (systemFilter == null || r.system == systemFilter) &&
                (bodyFilter == null || r.body == bodyFilter) &&
                (!_battHasOrdersOnly || (r.formation.Orders != null && r.formation.Orders.Count > 0)))
                .ToList();

            ImGui.Separator();

            // ── The table (name / world / region / strength / health / reach / stance / ROE) ──
            (Entity body, StarSystem system, GroundForcesDB forces, GroundFormation formation) selected = default;
            bool haveSelected = false;

            if(ImGui.BeginTable("BattalionTable", 8, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Battalion", ImGuiTableColumnFlags.None, 0.20f);
                ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.None, 0.15f);
                ImGui.TableSetupColumn("Region", ImGuiTableColumnFlags.None, 0.09f);
                ImGui.TableSetupColumn("Strength", ImGuiTableColumnFlags.None, 0.11f);
                ImGui.TableSetupColumn("Health", ImGuiTableColumnFlags.None, 0.13f);
                ImGui.TableSetupColumn("Reach", ImGuiTableColumnFlags.None, 0.08f);
                ImGui.TableSetupColumn("Stance", ImGuiTableColumnFlags.None, 0.12f);
                ImGui.TableSetupColumn("ROE", ImGuiTableColumnFlags.None, 0.12f);
                ImGui.TableHeadersRow();

                foreach(var r in rows)
                {
                    var f = r.formation;
                    int members = GroundFormationTools.MemberCount(r.forces, f);
                    int rally = GroundForces.LeaderRegion(r.forces, f);
                    double strength = GroundFormationTools.FormationStrength(r.forces, f);
                    var (curHp, maxHp) = GroundFormationTools.FormationHealth(r.forces, f);
                    int reach = GroundFormationTools.FormationReachHexes(r.forces, f);
                    bool isSel = r.body.Id == _selBattalionBodyId && f.FormationId == _selBattalionFormationId;

                    ImGui.TableNextColumn();
                    string label = $"{f.Name} ({members})##batt{r.body.Id}_{f.FormationId}";
                    if(ImGui.Selectable(label, isSel, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selBattalionBodyId = r.body.Id;
                        _selBattalionFormationId = f.FormationId;
                        isSel = true;
                    }

                    ImGui.TableNextColumn(); ImGui.Text(r.body.GetName(myFaction));
                    ImGui.TableNextColumn(); ImGui.Text(rally >= 0 ? "R" + (rally + 1) : "—");
                    ImGui.TableNextColumn(); ImGui.Text($"{strength:N0}");
                    ImGui.TableNextColumn(); ImGui.Text($"{curHp:N0} / {maxHp:N0}");
                    ImGui.TableNextColumn(); ImGui.Text(reach > 0 ? reach + " hex" : "—");
                    ImGui.TableNextColumn(); ImGui.Text(string.IsNullOrEmpty(f.StanceId) ? "Balanced" : f.StanceId);
                    ImGui.TableNextColumn(); ImGui.Text(f.Engagement.ToString());

                    if(isSel) { selected = r; haveSelected = true; }
                }
                ImGui.EndTable();
            }

            ImGui.Separator();
            if(haveSelected)
                DrawBattalionOrders(selected.body, selected.forces, selected.formation);
            else
                ImGui.TextDisabled("Select a battalion above to command it (march / queue / stance / ROE), or jump to its world.");
        }

        // The order surface for the selected battalion — the same verbs PlanetViewWindow.DrawFormationPanel gives a
        // single world, but reachable from the cross-body manager. All direct CI-tested engine calls.
        private void DrawBattalionOrders(Entity body, GroundForcesDB forces, GroundFormation f)
        {
            int myFaction = _uiState.Faction?.Id ?? -1;
            int members = GroundFormationTools.MemberCount(forces, f);
            int rally = GroundForces.LeaderRegion(forces, f);
            double strength = GroundFormationTools.FormationStrength(forces, f);
            var (curHp, maxHp) = GroundFormationTools.FormationHealth(forces, f);

            DisplayHelpers.Header(f.Name, "Command this battalion — the same orders the planet view gives.");
            ImGui.Text($"On {body.GetName(myFaction)}"
                + (rally >= 0 ? $", Region {rally + 1}" : "")
                + $" — {members} unit(s), strength {strength:N0}, health {curHp:N0}/{maxHp:N0}");

            // (d) Jump to this world's tactical map (the "take me there").
            if(ImGui.Button("Open planet view##battjump"))
                JumpToPlanetView(body);
            ImGui.SameLine();
            ImGui.TextDisabled("(opens the surface tactical map for this world)");

            // March + queue need the region graph.
            if(!body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions == null)
            {
                ImGui.TextDisabled("This world has no region map — can't march here.");
                return;
            }
            var regions = regionsDB.Regions;
            var rallyRegion = (rally >= 0 && rally < regions.Count) ? regions[rally] : null;

            // ── March the whole formation to an adjacent region (direct OrderFormationMove) ──
            ImGui.Separator();
            ImGui.TextDisabled("March the battalion (one ring-hop to an adjacent region):");
            if(rallyRegion != null && members > 0 && rallyRegion.Neighbors != null)
            {
                bool any = false;
                foreach(int n in rallyRegion.Neighbors)
                {
                    if(n < 0 || n >= regions.Count) continue;
                    if(ImGui.Button($"March to Region {n + 1}##bm{body.Id}_{f.FormationId}_{n}"))
                        MarchBattalion(body, f, n);
                    ImGui.SameLine();
                    any = true;
                }
                if(any) ImGui.NewLine();
            }
            else
            {
                ImGui.TextDisabled("(No adjacent region to march to.)");
            }

            // ── Queue waypoints (QueueFormationOrder) + stance + ROE ──
            DrawBattalionOrderQueue(body, forces, f, regions, rallyRegion);
            ImGui.Separator();
            DrawBattalionStance(body, f);
            DrawBattalionRoe(f);
        }

        private void MarchBattalion(Entity body, GroundFormation f, int target)
        {
            try
            {
                int moved = GroundForces.OrderFormationMove(body, f, target);
                _battStatus = moved > 0 ? $"'{f.Name}' marches {moved} unit(s) to Region {target + 1}"
                                        : "battalion couldn't march (adjacency/transit)";
            }
            catch(Exception ex) { _battStatus = "march failed (logged)"; Console.WriteLine($"[RenderError] FleetWindow battalion march threw: {ex}"); }
        }

        // The order-queue panel — copies PlanetViewWindow.DrawOrderQueue's idioms (build a "then" plan of moves/hold/ROE).
        private void DrawBattalionOrderQueue(Entity body, GroundForcesDB forces, GroundFormation f, List<Region> regions, Region rallyRegion)
        {
            ImGui.Separator();
            if(f.Orders != null && f.Orders.Count > 0)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.9f, 1f, 1f), $"Plan ({f.Orders.Count} order{(f.Orders.Count == 1 ? "" : "s")}):");
                for(int i = 0; i < f.Orders.Count; i++)
                    ImGui.TextDisabled($"   {i + 1}. {f.Orders[i].Describe()}");
                if(ImGui.Button($"Clear plan##bq{f.FormationId}")) { GroundForces.ClearFormationOrders(f); _battStatus = "plan cleared"; }
            }
            else
            {
                ImGui.TextDisabled("No queued plan. Add move/hold/ROE waypoints below (they run in sequence).");
            }

            if(rallyRegion != null && rallyRegion.Neighbors != null)
            {
                foreach(int n in rallyRegion.Neighbors)
                {
                    if(n < 0 || n >= regions.Count) continue;
                    if(ImGui.Button($"+ March → R{n + 1}##bq{body.Id}_{f.FormationId}_{n}"))
                    { GroundForces.QueueFormationOrder(f, GroundOrder.MoveRegion(n)); _battStatus = $"queued → region {n + 1}"; }
                    ImGui.SameLine();
                }
            }
            if(ImGui.Button($"+ Hold 6h##bq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Hold(6 * 3600)); _battStatus = "queued hold 6h"; }
            ImGui.SameLine();
            if(ImGui.Button($"+ ROE Stand-off##bq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Roe(GroundEngagementStance.StandOff)); _battStatus = "queued ROE stand-off"; }
            ImGui.SameLine();
            if(ImGui.Button($"+ ROE Close##bq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Roe(GroundEngagementStance.CloseToEngage)); _battStatus = "queued ROE close"; }
        }

        // The stance selector — the ground echo of the Fleet-window doctrine selector (copies PlanetViewWindow.DrawStanceSelector).
        private void DrawBattalionStance(Entity body, GroundFormation f)
        {
            if(_uiState.Game == null) return;
            var catalog = _uiState.Game.StartingGameData.GroundStances;
            if(catalog == null || catalog.Count == 0) return;

            var stances = catalog.Values.ToArray();
            var names = stances.Select(st => $"{st.DisplayName} [{st.Family}]").ToArray();
            _battStanceChoice = Math.Clamp(_battStanceChoice, 0, stances.Length - 1);

            string current = string.IsNullOrEmpty(f.StanceId) ? "Balanced (none)" : $"{f.StanceId} [{f.StanceFamily}]";
            ImGui.TextDisabled($"Stance: {current}  (atk x{f.AttackMult:0.00}, dmg-taken x{f.DamageTakenMult:0.00})");

            ImGui.SetNextItemWidth(200f);
            ImGui.Combo($"##battstance{f.FormationId}", ref _battStanceChoice, names, names.Length);

            bool haveTime = body.Manager != null;
            DateTime now = haveTime ? body.StarSysDateTime : DateTime.MinValue;
            bool onCooldown = haveTime && now < f.SwitchableAfter;

            ImGui.SameLine();
            if(!haveTime || onCooldown) ImGui.BeginDisabled();
            if(ImGui.Button($"Set stance##bst{f.FormationId}"))
            {
                try
                {
                    var bp = stances[_battStanceChoice];
                    bool ok = GroundFormationDoctrine.TrySetStance(f, bp, now);
                    _battStatus = ok ? $"stance: {bp.DisplayName}" : "on cooldown — can't switch yet";
                }
                catch(Exception ex) { _battStatus = "set stance failed (logged)"; Console.WriteLine($"[RenderError] FleetWindow battalion stance threw: {ex}"); }
            }
            if(!haveTime || onCooldown) ImGui.EndDisabled();
            if(onCooldown)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"switch in {(f.SwitchableAfter - now).TotalSeconds:0}s");
            }
        }

        // The ROE selector — the commander's maneuver intent (copies PlanetViewWindow.DrawRoeSelector).
        private void DrawBattalionRoe(GroundFormation f)
        {
            var stances = new[] { GroundEngagementStance.HoldGround, GroundEngagementStance.CloseToEngage, GroundEngagementStance.StandOff };
            var names = new[] { "Hold Ground", "Close to Engage", "Stand Off (auto-kite)" };
            int cur = Array.IndexOf(stances, f.Engagement);
            if(cur < 0) cur = 0;

            ImGui.TextDisabled("ROE:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(190f);
            int choice = cur;
            if(ImGui.Combo($"##battroe{f.FormationId}", ref choice, names, names.Length) && choice != cur && choice >= 0 && choice < stances.Length)
            {
                try
                {
                    GroundFormationDoctrine.SetEngagementStance(f, stances[choice]);
                    _battStatus = $"ROE: {names[choice]}";
                }
                catch(Exception ex) { _battStatus = "set ROE failed (logged)"; Console.WriteLine($"[RenderError] FleetWindow battalion ROE threw: {ex}"); }
            }
        }

        // (d) Jump the PlanetViewWindow to a battalion's world — resolve the body's EntityState via its SystemState
        // (the open-window-for-entity mechanic: GetInstance registers/points the per-body window, SetActive shows it).
        private void JumpToPlanetView(Entity body)
        {
            try
            {
                foreach(var (_, sysState) in _uiState.StarSystemStates)
                {
                    if(sysState?.StarSystem == null) continue;
                    var es = sysState.GetEntityById(body.Id);
                    if(es == null) continue;
                    var window = PlanetViewWindow.GetInstance(es, _uiState);
                    window.SetActive(true);
                    _battStatus = $"opened planet view for {body.GetName(_uiState.Faction?.Id ?? -1)}";
                    return;
                }
                _battStatus = "couldn't open planet view (body not in a known system)";
            }
            catch(Exception ex) { _battStatus = "open planet view failed (logged)"; Console.WriteLine($"[RenderError] FleetWindow jump-to-planet threw: {ex}"); }
        }

        private void IssueOrdersDisplay(Vector2 size)
        {

            if(ImGui.BeginChild("IssueOrders", size, ImGuiChildFlags.Borders))
            {
                if(SelectedFleet == null || SelectedFleet.Manager == null
                    || _uiState.Faction == null || _uiState.Game == null)
                {
                    ImGui.EndChild();
                    return;
                }

                switch(selectedIssueOrderType)
                {
                    case IssueOrderType.MoveTo:
                        moveToList = _uiState.StarSystemStates[SelectedFleet.Manager.ManagerID].GetFilteredEntities(
                            EntityFilter.Friendly | EntityFilter.Neutral,
                            _uiState.Faction.Id,
                            new List<Type>() {
                                typeof(SystemBodyInfoDB),
                                typeof(PositionDB)
                            });

                        foreach(var bodyState in moveToList)
                        {
                            var name = bodyState.Name;
                            if(ImGui.Button(name + "###movement-button-" + name))
                            {
                                var order = MoveToSystemBodyOrder.CreateCommand(_uiState.Faction.Id, SelectedFleet, bodyState.Entity);
                                _uiState.Game.OrderHandler.HandleOrder(order);
                                Pulsar4X.Client.SessionLog.Action("move order: fleet #" + SelectedFleet.Id
                                    + " -> '" + bodyState.Name + "' (warp). Watch next heartbeat for teleport check.");
                            }
                        }
                        break;
                    case IssueOrderType.GeoSurvey:
                        geoSurveyList = _uiState.StarSystemStates[SelectedFleet.Manager.ManagerID].GetFilteredEntities(
                            EntityFilter.Friendly | EntityFilter.Neutral,
                            _uiState.Faction.Id,
                            typeof(GeoSurveyableDB));

                        foreach(var bodyState in geoSurveyList)
                        {
                            if(!bodyState.Entity.TryGetDataBlob<GeoSurveyableDB>(out var geoSurveyableDB)) continue;
                            if(geoSurveyableDB.IsSurveyComplete(_uiState.Faction.Id)) continue;

                            var name = bodyState.Name;
                            if(ImGui.Button(name + "###geosurvey-button-" + name))
                            {
                                var order = WarpFleetTowardsTargetOrder.CreateCommand(SelectedFleet, bodyState.Entity);
                                _uiState.Game.OrderHandler.HandleOrder(order);

                                var order2 = GeoSurveyOrder.CreateCommand(_uiState.Faction.Id, SelectedFleet, bodyState.Entity);
                                _uiState.Game.OrderHandler.HandleOrder(order2);
                            }
                        }
                        break;
                    case IssueOrderType.JPSurvey:
                        gravSurveyList = _uiState.StarSystemStates[SelectedFleet.Manager.ManagerID].GetFilteredEntities(
                            EntityFilter.Friendly | EntityFilter.Neutral,
                            _uiState.Faction.Id,
                            typeof(JPSurveyableDB));

                        foreach(var jpBody in gravSurveyList)
                        {
                            if(!jpBody.Entity.TryGetDataBlob<JPSurveyableDB>(out var jpSurveyableDB)) continue;
                            if(jpSurveyableDB.IsSurveyComplete(_uiState.Faction.Id)) continue;

                            var name = jpBody.Name;
                            if(ImGui.Button(name + "###jpsurvey-button-" + name))
                            {
                                if(jpSurveyableDB.OwningEntity != null)
                                {
                                    var order = WarpFleetTowardsTargetOrder.CreateCommand(SelectedFleet, jpSurveyableDB.OwningEntity);
                                    _uiState.Game.OrderHandler.HandleOrder(order);

                                    var order2 = JPSurveyOrder.CreateCommand(_uiState.Faction.Id, SelectedFleet, jpSurveyableDB.OwningEntity);
                                    _uiState.Game.OrderHandler.HandleOrder(order2);
                                }
                            }
                        }
                        break;
                    case IssueOrderType.Jump:
                        jumpPointList = _uiState.StarSystemStates[SelectedFleet.Manager.ManagerID].GetFilteredEntities(
                            EntityFilter.Friendly | EntityFilter.Neutral,
                            _uiState.Faction.Id,
                            typeof(JumpPointDB));

                        foreach(var jumpGate in jumpPointList)
                        {
                            if(!jumpGate.Entity.TryGetDataBlob<JumpPointDB>(out var jumpGateDB)) continue;
                            if(!jumpGateDB.IsDiscovered.Contains(_uiState.Faction.Id)) continue;

                            var name = jumpGate.Name;
                            if(ImGui.Button(name + "###jump-gate-button-" + name))
                            {
                                if(jumpGateDB.OwningEntity != null)
                                {
                                    JumpOrder.CreateAndExecute(_uiState.Game, _uiState.Faction, SelectedFleet, jumpGateDB);
                                }
                            }
                        }
                        break;
                    case IssueOrderType.RefuelAt:
                        colonyList = _uiState.StarSystemStates[SelectedFleet.Manager.ManagerID].GetFilteredEntities(
                            EntityFilter.Friendly | EntityFilter.Neutral,
                            _uiState.Faction.Id,
                            typeof(ColonyInfoDB));

                        foreach(var colony in colonyList)
                        {
                            if(!colony.Entity.TryGetDataBlob<CargoStorageDB>(out var storageDB)) continue;

                            var name = colony.Name;
                            if(ImGui.Button(name + "###refuelAt-button-" + name))
                            {

                                //var order = MoveFleetTowardsTargetOrder.CreateCommand(SelectedFleet, jpSurveyableDB.OwningEntity);
                                var order = WarpFleetTowardsTargetOrder.CreateCommand(SelectedFleet, colony.Entity);
                                _uiState.Game.OrderHandler.HandleOrder(order);

                                 CargoTransferOrder.CreateRefuelFleetCommand(colony.Entity,  SelectedFleet );
                                //_uiState.Game.OrderHandler.HandleOrder(order2);

                            }
                        }
                        break;
                    case IssueOrderType.Troops:
                        DisplayTroopOrders();
                        break;
                }
            }
            ImGui.EndChild();
        }

        // ═══════════════════════════ EMBARK / LAND TROOPS (Earthfall C5.1) ═══════════════════════════
        // The player half of the invasion lift step (MVP Stage 4 — LoadTroopsOrder/LandTroopsOrder had ZERO client
        // surface, the "missing control panel"). For each troop-bay ship in the selected fleet:
        //   EMBARK — list the player's ground units standing on the body the ship orbits, with bay-capacity-vs-unit-size,
        //            and load each via LoadTroopsOrder.CreateCommand(ship, body, unitId) → Game.OrderHandler.HandleOrder.
        //   LAND   — once units are aboard and the ship holds the orbit over a target body, pick a REGION and drop each
        //            via LandTroopsOrder.CreateCommand(ship, targetBody, unitId, regionIndex) → HandleOrder.
        // THIN + DEFENSIVE (the client discipline): every value is read off GroundTransport's own CI-tested capacity
        // helpers, every mutation goes through the CI-tested order path on an explicit click, and the orders themselves
        // re-check every precondition (at-body / bay room / orbital control) — so a stale click is a safe no-op.

        private readonly Dictionary<int, int> _landRegionChoice = new();   // ship id -> chosen land region index
        private string _troopStatus = "";

        // True if any ship in the fleet carries a troop OR vehicle bay (a GroundBayAtb → non-zero BayCapacity) — the
        // gate for offering the order in the list.
        private bool HasAnyTroopBay(Entity? fleet)
        {
            if(fleet == null || !fleet.TryGetDataBlob<FleetDB>(out var fdb)) return false;
            foreach(var ship in fdb.GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()))
                if(GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel) > 0
                    || GroundTransport.BayCapacity(ship, GroundCarryClass.Vehicle) > 0)
                    return true;
            return false;
        }

        private void DisplayTroopOrders()
        {
            if(selectedFleetDB == null || _uiState.Faction == null || _uiState.Game == null) return;
            int myFaction = _uiState.Faction.Id;

            DisplayHelpers.Header("Embark / Land Troops",
                "Load ground units onto a troop-bay ship at a garrisoned world, fly them to the target, win the orbit, and land them on a region — the lift step of an invasion.");

            if(!string.IsNullOrEmpty(_troopStatus))
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _troopStatus);

            var bayShips = selectedFleetDB.GetChildren()
                .Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()
                    && (GroundTransport.BayCapacity(c, GroundCarryClass.Personnel) > 0
                        || GroundTransport.BayCapacity(c, GroundCarryClass.Vehicle) > 0))
                .ToList();

            if(bayShips.Count == 0)
            {
                ImGui.TextDisabled("No ship in this fleet has a troop or vehicle bay.");
                ImGui.TextDisabled("Design a ship with a Troop Bay / Vehicle Bay component to lift ground units.");
                return;
            }

            foreach(var ship in bayShips)
            {
                ImGui.PushID(ship.Id);
                if(ImGui.CollapsingHeader(ship.GetName(myFaction) + "###troopship" + ship.Id, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    DrawShipBayCapacity(ship);

                    // The body the ship is currently at (its position parent) — the embark SOURCE and the land TARGET.
                    Entity? body = null;
                    if(ship.TryGetDataBlob<PositionDB>(out var pos)) body = pos.Parent;

                    DrawEmbarkSection(ship, body, myFaction);
                    ImGui.Separator();
                    DrawLandSection(ship, body, myFaction);
                }
                ImGui.PopID();
            }
        }

        // The bay-capacity readout (the "show bay capacity vs unit size" requirement, ship half) — used/free per class,
        // summed on demand from the ship's installed bays.
        private void DrawShipBayCapacity(Entity ship)
        {
            double pCap = GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel);
            double vCap = GroundTransport.BayCapacity(ship, GroundCarryClass.Vehicle);
            if(pCap > 0)
            {
                double used = GroundTransport.UsedCapacity(ship, GroundCarryClass.Personnel);
                ImGui.TextDisabled($"Troop bay (infantry): {used:0}/{pCap:0} used, {pCap - used:0} free");
            }
            if(vCap > 0)
            {
                double used = GroundTransport.UsedCapacity(ship, GroundCarryClass.Vehicle);
                ImGui.TextDisabled($"Vehicle bay (armour/artillery): {used:0}/{vCap:0} used, {vCap - used:0} free");
            }
        }

        // EMBARK — the player's own units standing on the body the ship orbits, each with its carry-class + size and a
        // Load button (greyed when the ship has no room of that class). LoadTroopsOrder.CreateCommand(ship, body, unitId).
        private void DrawEmbarkSection(Entity ship, Entity? body, int myFaction)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.9f, 1f, 1f), "Embark — load units standing here");
            if(body == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                ImGui.TextDisabled("   Ship isn't over a garrisoned world (park it in orbit of a body with your ground units).");
                return;
            }

            var loadable = forces.Units.Where(u => u.FactionOwnerID == myFaction).ToList();
            if(loadable.Count == 0)
            {
                ImGui.TextDisabled($"   No units of yours on {body.GetName(myFaction)} to load.");
                return;
            }

            foreach(var unit in loadable)
            {
                var cls = GroundTransport.CarryClassOf(unit.UnitType);
                double size = GroundTransport.CarrySizeOf(unit);
                bool canLoad = GroundTransport.CanLoad(ship, unit);

                if(!canLoad) ImGui.BeginDisabled();
                if(ImGui.Button($"Load {unit.Name} ({unit.UnitType}, size {size:0}, {cls} bay)##load{ship.Id}_{unit.UnitId}"))
                {
                    var order = LoadTroopsOrder.CreateCommand(ship, body, unit.UnitId);
                    _uiState.Game.OrderHandler.HandleOrder(order);
                    _troopStatus = $"Loading {unit.Name} onto {ship.GetName(myFaction)}";
                    SessionLog.Action($"[troops] load order: unit #{unit.UnitId} '{unit.Name}' -> ship #{ship.Id} at '{body.GetName(myFaction)}'");
                }
                if(!canLoad)
                {
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    ImGui.TextDisabled($"(needs {size:0} {cls} room, {GroundTransport.FreeCapacity(ship, cls):0} free)");
                }
            }
        }

        // LAND — the units aboard the ship, dropped onto a chosen region of the body it's over. Gated on orbital control
        // (win the space before boots on the ground). LandTroopsOrder.CreateCommand(ship, targetBody, unitId, regionIndex).
        private void DrawLandSection(Entity ship, Entity? body, int myFaction)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.9f, 1f, 1f), "Land — drop loaded units onto this world");
            if(!ship.TryGetDataBlob<GroundTransportDB>(out var transport) || transport.LoadedUnits.Count == 0)
            {
                ImGui.TextDisabled("   No units loaded aboard this ship.");
                return;
            }
            if(body == null)
            {
                ImGui.TextDisabled("   Ship isn't over a body — move it to the target world first.");
                return;
            }

            // Orbital-control gate: a foreign ship over the body blocks the drop (the order re-checks this too).
            bool holdsOrbit = GroundTransport.HasOrbitalControl(ship, body);
            if(holdsOrbit)
                ImGui.TextDisabled($"   Holding the orbit over {body.GetName(myFaction)} — clear to land.");
            else
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), $"   A foreign ship holds the orbit over {body.GetName(myFaction)} — clear it before you can land.");

            // Region picker — RegionIndex rides on LandTroopsOrder (verified). Default region 0 when the body has no
            // region layer.
            int regionCount = 1;
            string[] regionNames = { "Region 1" };
            if(body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) && regionsDB.Regions != null && regionsDB.Regions.Count > 0)
            {
                regionCount = regionsDB.Regions.Count;
                regionNames = new string[regionCount];
                for(int i = 0; i < regionCount; i++) regionNames[i] = "Region " + (i + 1);
            }
            if(!_landRegionChoice.TryGetValue(ship.Id, out int region)) region = 0;
            region = Math.Clamp(region, 0, regionCount - 1);
            ImGui.SetNextItemWidth(160f);
            ImGui.Combo($"Land in##landreg{ship.Id}", ref region, regionNames, regionNames.Length);
            _landRegionChoice[ship.Id] = region;

            foreach(var unit in transport.LoadedUnits)
            {
                if(!holdsOrbit) ImGui.BeginDisabled();
                if(ImGui.Button($"Land {unit.Name} ({unit.UnitType}) in Region {region + 1}##land{ship.Id}_{unit.UnitId}"))
                {
                    var order = LandTroopsOrder.CreateCommand(ship, body, unit.UnitId, region);
                    _uiState.Game.OrderHandler.HandleOrder(order);
                    _troopStatus = $"Landing {unit.Name} on {body.GetName(myFaction)} region {region + 1}";
                    SessionLog.Action($"[troops] land order: unit #{unit.UnitId} '{unit.Name}' from ship #{ship.Id} -> '{body.GetName(myFaction)}' region {region + 1}");
                }
                if(!holdsOrbit) ImGui.EndDisabled();
            }
        }

        private void DisplayOrders()
        {
            if(SelectedFleet == null)
                return;

            var xPosition = ImGui.GetCursorPosX();
            Vector2 windowContentSize = ImGui.GetContentRegionAvail();

            if (ImGui.BeginChild("Fleet Orders", new Vector2(Styles.LeftColumnWidthLg, windowContentSize.Y), ImGuiChildFlags.Borders))
            {
                var orderableDB = SelectedFleet.GetDataBlob<OrderableDB>();
                DisplayHelpers.Header("Fleet Orders");
                if (orderableDB.ActionList.Count == 0)
                {
                    ImGui.Text("None");
                }
                else
                {
                    if (ImGui.BeginTable("FleetOrdersTable", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.None, 0.1f);
                        ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.None, 0.9f);
                        ImGui.TableHeadersRow();

                        var actions = orderableDB.ActionList.ToArray();
                        for (int i = 0; i < actions.Length; i++)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text((i + 1).ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(actions[i].Name);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("IsRunning: " + actions[i].IsRunning);
                                ImGui.Text("IsFinished: " + actions[i].GetIsFinished);
                                ImGui.EndTooltip();
                            }
                        }

                        ImGui.EndTable();
                    }
                }
            }
            ImGui.EndChild();
            ImGui.SetCursorPosX(xPosition);
        }

        private void DisplayShips()
        {
            if(SelectedFleet == null) return;

            var xPosition = ImGui.GetCursorPosX();
            Vector2 windowContentSize = ImGui.GetContentRegionAvail();
            if (ImGui.BeginChild("FleetSummary2", new Vector2(Styles.LeftColumnWidthLg, windowContentSize.Y * 0.5f - 24f), ImGuiChildFlags.Borders))
            {
                DisplayHelpers.Header("Assigned Ships");

                ImGui.PushStyleColor(ImGuiCol.FrameBg, Styles.InvisibleColor);
                var contentSizeAvail = ImGui.GetContentRegionAvail();
                if (ImGui.BeginListBox("###assigned-ships", new Vector2(contentSizeAvail.X, contentSizeAvail.Y - Styles.ButtonVerticalOffset)))
                {
                    var fleet = SelectedFleet.GetDataBlob<FleetDB>();
                    foreach (var ship in fleet.GetChildren())
                    {
                        // Only display ships
                        if (ship.HasDataBlob<FleetDB>()) continue;

                        if (!selectedShips.ContainsKey(ship))
                        {
                            selectedShips.Add(ship, false);
                        }

                        string name = ship.GetName(factionID);
                        if (fleet.FlagShipID == ship.Id)
                        {
                            name = "(F) " + name;
                        }
                        if (ImGui.Selectable(name, selectedShips[ship], ImGuiSelectableFlags.SpanAllColumns))
                        {
                            selectedShips[ship] = !selectedShips[ship];
                        }
                        // Open the ship context menu on right-click of THIS row (explicit valid button), before the
                        // tooltip renders — replaces BeginPopupContextItem() in DisplayShipContextMenu (same
                        // internal-mouse-query assert as the fleet menu, 2026-07-03).
                        if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            ImGui.OpenPopup("shipctx##" + ship.Id);
                        DisplayHelpers.ShipTooltip(ship, factionID);
                        DisplayShipContextMenu(selectedShips, ship);
                    }
                    ImGui.EndListBox();
                }
                ImGui.PopStyleColor();

                if(ImGui.Button("Select All/None", new Vector2(contentSizeAvail.X, 0)))
                {
                    bool selectAll = !selectedShips.Values.Any(v => v == true);
                    foreach(var (ship, selected) in selectedShips)
                    {
                        selectedShips[ship] = selectAll;
                    }
                }
            }
            ImGui.EndChild();
            ImGui.SetCursorPosX(xPosition);
        }

        private void DisplayFleetList()
        {
            if(factionRoot == null) return;

            Vector2 windowContentSize = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("FleetListSelection", new Vector2(Styles.LeftColumnWidthLg, windowContentSize.Y - 24f), ImGuiChildFlags.Borders))
            {
                DisplayHelpers.Header("Fleets", "Select a fleet to manage it.");

                // We need a drop target here so nested items can be un-nested to the root of the tree
                DisplayEmptyDropTarget();

                foreach(var fleet in factionRoot.GetChildren())
                {
                    DisplayFleetItem(fleet);
                }

                var sizeLeft = ImGui.GetContentRegionAvail();
                ImGui.InvisibleButton("invis-droptarget", new Vector2(sizeLeft.X, 32f));
                DisplayEmptyDropTarget();

                if(factionRoot.GetChildren().Any(x => !x.HasDataBlob<FleetDB>()))
                {
                    DisplayHelpers.Header("Unattached Ships");

                    foreach(var ship in factionRoot.GetChildren())
                    {
                        if(ship.HasDataBlob<FleetDB>()) continue;

                        if(!selectedUnattachedShips.ContainsKey(ship))
                        {
                            selectedUnattachedShips.Add(ship, false);
                        }

                        if(ImGui.Selectable(ship.GetName(factionID), selectedUnattachedShips[ship]))
                        {
                            selectedUnattachedShips[ship] = !selectedUnattachedShips[ship];
                        }
                        if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            ImGui.OpenPopup("shipctx##" + ship.Id);
                        DisplayHelpers.ShipTooltip(ship, factionID);
                        DisplayShipContextMenu(selectedUnattachedShips, ship, isUnattached: true);
                    }
                }
            }
            ImGui.EndChild();

            if(ImGui.Button("Create New Fleet", new Vector2(Styles.LeftColumnWidthLg, 0f)))
            {
                if(_uiState.Game != null && _uiState.Faction != null)
                {
                    string name = NameFactory.GetFleetName(_uiState.Game);
                    var order = FleetOrder.CreateFleetOrder(name, _uiState.Faction, _uiState.SelectedSystem);
                    _uiState.Game.OrderHandler.HandleOrder(order);
                }
            }
        }

        private void DisplayFleetItem(Entity fleet)
        {
            if(!fleet.TryGetDataBlob<FleetDB>(out var fleetInfo))
            {
                return;
            }

            ImGui.PushID(fleet.Id.ToString());
            string name = fleet.GetName(factionID);
            var flags = ImGuiTreeNodeFlags.DefaultOpen;

            if(!fleetInfo.GetChildren().Any(x => x.HasDataBlob<FleetDB>()))
            {
                flags |= ImGuiTreeNodeFlags.Leaf;
            }

            if(SelectedFleet == fleet)
            {
                flags |= ImGuiTreeNodeFlags.Selected;
            }

            string description = "";

            fleet.TryGetDataBlob<OrderableDB>(out var orderableDB);

            if(orderableDB == null || orderableDB.ActionList.Count == 0)
            {
                description = "No Orders";
            }
            else
            {
                foreach(var order in orderableDB.ActionList)
                {
                    description += order.Name + "\n";
                }
            }

            bool isTreeOpen = ImGui.TreeNodeEx(name, flags);

            // LEFT-click selects the fleet IMMEDIATELY — whether or not the node is expanded, with no popup in the
            // way. (Selection used to live inside `if(isTreeOpen)`, so picking a fleet was a two-step dance and you
            // had to click elsewhere before a different fleet would take — the reported "a menu blocks selection".)
            // Right-click is the context menu only (DisplayContextMenu → BeginPopupContextItem with MouseButtonRight).
            if(ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                SelectFleet(fleet);
            }
            // Open the context menu on a RIGHT-click of THIS tree node, using explicit valid button args. This
            // replaces BeginPopupContextItem(null, MouseButtonRight) in DisplayContextMenu, whose ImGui-INTERNAL
            // IsMouseReleased(g...MouseButton) query was firing the native `button >= 0 && button < 5` assert every
            // frame — the modal that read as [HANG] wedged in FleetWindow/List/ContextMenu (2026-07-03). Detected
            // HERE (right after the node) so it keys off the tree node, not the tooltip's last-item.
            if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup("fleetctx##" + fleet.Id);

            if(ImGui.IsItemHovered())
                DisplayHelpers.DescriptiveTooltip(name, "Fleet", description);

            // Context menu (RIGHT-click) + drag/drop — once per fleet, regardless of open/closed.
            // Sub-breadcrumbs (2026-07-03): a native ImGui mouse-button ASSERT in the fleet list pops a MODAL
            // dialog that blocks the main thread, so the watchdog reports [HANG] at whatever stage was active —
            // 'FleetWindow/List'. Stamp the exact list op so the next occurrence names ContextMenu/DragSource/
            // DropTarget (the drag-drop delivery — AcceptDragDropPayload's internal IsMouseReleased(bad button) —
            // is the prime suspect). Cheap volatile writes; no behaviour change.
            SessionLog.CurrentStage = "FleetWindow/List/ContextMenu";
            DisplayContextMenu(fleet);
            SessionLog.CurrentStage = "FleetWindow/List/DragSource";
            DisplayDropSource(fleet, name);
            SessionLog.CurrentStage = "FleetWindow/List/DropTarget";
            DisplayDropTarget(fleet);
            SessionLog.CurrentStage = "FleetWindow/List";

            if(isTreeOpen)
            {
                foreach(var child in fleetInfo.GetChildren())
                {
                    DisplayFleetItem(child);
                }
                ImGui.TreePop();
            }
            ImGui.PopID();
        }

        private void DisplayContextMenu(Entity fleet)
        {
            // The popup is OPENED by the explicit right-click check in DisplayFleetItem (ImGui.OpenPopup). Here we
            // only RENDER it via BeginPopup(id) — which takes NO mouse button, so it can't hit the ImGui-internal
            // IsMouseReleased(bad button) assert that BeginPopupContextItem(null, MouseButtonRight) was tripping
            // every frame (the fleet-list freeze, 2026-07-03). Same id string the OpenPopup call uses.
            if(ImGui.BeginPopup("fleetctx##" + fleet.Id))
            {
                if(ImGui.MenuItem("Rename"))
                {
                    RenameWindow.GetInstance().SetEntity(fleet);
                    RenameWindow.GetInstance().SetActive(true);
                }
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.TerribleColor);
                if(ImGui.MenuItem("Disband###delete-" + fleet.Id))
                {
                    var order = FleetOrder.DisbandFleet(factionID, fleet);
                    _uiState.Game?.OrderHandler.HandleOrder(order);
                    SelectFleet(null);
                }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }
        }

        private void DisplayShipContextMenu(Dictionary<Entity, bool> selected, Entity ship, bool isUnattached = false)
        {
            if(SelectedFleet == null || factionRoot == null) return;

            // Rendered via BeginPopup(id) — opened by the explicit right-click check at the ship row. No internal
            // mouse-button query (unlike BeginPopupContextItem), so it can't hit the native button assert.
            if(ImGui.BeginPopup("shipctx##" + ship.Id))
            {
                if(ImGui.MenuItem("View Ship"))
                {
                    _uiState.EntityClicked(ship.Id, _uiState.SelectedStarSystemId, MouseButtons.Primary);
                }
                if(!isUnattached)
                {
                    if(selectedFleetFlagship != null && ship.Id == selectedFleetFlagship.Id)
                    {
                        ImGui.BeginDisabled();
                    }
                    if(ImGui.MenuItem("Promote to Flagship"))
                    {
                        var setFlagshipOrder = FleetOrder.SetFlagShip(factionID, SelectedFleet, ship);
                        _uiState.Game?.OrderHandler.HandleOrder(setFlagshipOrder);
                        SelectFleet(SelectedFleet);
                    }
                    if(selectedFleetFlagship != null && ship.Id == selectedFleetFlagship.Id)
                    {
                        ImGui.EndDisabled();
                    }
                }
                ImGui.Separator();

                if(ImGui.BeginMenu("Re-assign ships"))
                {
                    ImGui.Text("Re-assign ships to:");
                    ImGui.Separator();
                    foreach(var fleet in factionRoot.GetChildren())
                    {
                        DisplayShipAssignmentOption(selected, ship, fleet, isUnattached: isUnattached);
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }
        }

        private void DisplayShipAssignmentOption(Dictionary<Entity, bool> selected, Entity ship, Entity fleet, int depth = 0, bool isUnattached = false)
        {
            if(!fleet.HasDataBlob<FleetDB>()
                || factionRoot == null
                || factionRoot.OwningEntity == null
                || SelectedFleet == null)
                return;

            for(int i = 0; i < depth; i++)
            {
                ImGui.InvisibleButton("invis", new Vector2(8, 8));
                ImGui.SameLine();
            }

            if(fleet == SelectedFleet && !isUnattached)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                ImGui.Text(fleet.GetName(factionID));
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushID(fleet.Id.ToString());
                if(ImGui.MenuItem(fleet.GetName(factionID)))
                {
                    if(!selected.Any(x => x.Value))
                    {
                        var unassignFrom = factionRoot.Children.Contains(ship) ? factionRoot.OwningEntity : SelectedFleet;
                        var unassignOrder = FleetOrder.UnassignShip(factionID, unassignFrom, ship);
                        _uiState.Game?.OrderHandler.HandleOrder(unassignOrder);

                        var assignOrder = FleetOrder.AssignShip(factionID, fleet, ship);
                        _uiState.Game?.OrderHandler.HandleOrder(assignOrder);
                    }
                    else
                    {
                        foreach(var (selectedShip, isSelected) in selected)
                        {
                            if(!isSelected) continue;

                            var unassignFrom = factionRoot.Children.Contains(selectedShip) ? factionRoot.OwningEntity : SelectedFleet;
                            var unassignOrder = FleetOrder.UnassignShip(factionID, unassignFrom, selectedShip);
                            _uiState.Game?.OrderHandler.HandleOrder(unassignOrder);

                            var assignOrder = FleetOrder.AssignShip(factionID, fleet, selectedShip);
                            _uiState.Game?.OrderHandler.HandleOrder(assignOrder);
                        }
                        // Clean up the selections
                        selected.Clear();
                    }
                }
                ImGui.PopID();
            }

            foreach(var child in fleet.GetDataBlob<FleetDB>().GetChildren())
            {
                DisplayShipAssignmentOption(selected, ship, child, depth + 1, isUnattached);
            }
        }

        private void DisplayEmptyDropTarget()
        {
            if(ImGui.BeginDragDropTarget())
            {
                // AcceptBeforeDelivery (2026-07-03 fix): ImGui's DEFAULT AcceptDragDropPayload path runs an
                // internal `IsMouseReleased(g.DragDropMouseButton)`, and when that button field is out of the
                // valid 0..4 range it fires the native assert `button >= 0 && button < 5` (imgui.cpp) — a modal
                // dialog that blocks the main thread and reads as a freeze in the fleet list. AcceptBeforeDelivery
                // SKIPS that internal mouse query; the drop-on-release is still gated by the client's own valid
                // IsMouseReleased(Left) check just below, so behaviour is unchanged. Diagnosed via two agents +
                // the FleetWindow/List/DropTarget breadcrumb. The drag-drop code predates this branch.
                ImGui.AcceptDragDropPayload("FLEET", ImGuiDragDropFlags.AcceptBeforeDelivery);
                if(ImGui.IsMouseReleased(ImGuiMouseButton.Left) && dragEntity != Entity.InvalidEntity)
                {
                    if(factionRoot != null && factionRoot.OwningEntity !=null)
                    {
                        var order = FleetOrder.ChangeParent(factionID, dragEntity, factionRoot.OwningEntity);
                        _uiState.Game?.OrderHandler.HandleOrder(order);
                    }
                }
                ImGui.EndDragDropTarget();
            }
        }

        private void DisplayDropTarget(Entity fleet)
        {
            // Begin Drag Target
            if (ImGui.BeginDragDropTarget())
            {
                // AcceptBeforeDelivery (2026-07-03 fix): ImGui's DEFAULT AcceptDragDropPayload path runs an
                // internal `IsMouseReleased(g.DragDropMouseButton)`, and when that button field is out of the
                // valid 0..4 range it fires the native assert `button >= 0 && button < 5` (imgui.cpp) — a modal
                // dialog that blocks the main thread and reads as a freeze in the fleet list. AcceptBeforeDelivery
                // SKIPS that internal mouse query; the drop-on-release is still gated by the client's own valid
                // IsMouseReleased(Left) check just below, so behaviour is unchanged. Diagnosed via two agents +
                // the FleetWindow/List/DropTarget breadcrumb. The drag-drop code predates this branch.
                ImGui.AcceptDragDropPayload("FLEET", ImGuiDragDropFlags.AcceptBeforeDelivery);
                if(ImGui.IsMouseReleased(ImGuiMouseButton.Left) && dragEntity != Entity.InvalidEntity)
                {
                    var order = FleetOrder.ChangeParent(factionID, dragEntity, fleet);
                    _uiState.Game?.OrderHandler.HandleOrder(order);
                }
                ImGui.EndDragDropTarget();
            }
        }

        private void DisplayDropSource(Entity fleet, string name)
        {
            // Begin drag source
            if(ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
            {
                dragEntity = fleet;

                ImGui.SetDragDropPayload("FLEET", IntPtr.Zero, 0);
                ImGui.Text(name);
                ImGui.EndDragDropSource();
            }
        }

        private void DisplayActionItem(EntityCommand action)
        {
            ImGui.PushID(action.GetHashCode());
            var size = ImGui.GetContentRegionAvail();
            ImGui.Text(OrderRegistry.ActionDescriptions[action.GetType()]);
            ImGui.SameLine();
            ImGui.SetCursorPosX(size.X - 12f);
            if(ImGui.Button("x"))
            {
                selectedOrder?.Actions.Remove(action);
            }
            ImGui.PopID();
        }

        /// <summary>
        /// Finds the first visible parent entity in the hierarchy.
        /// If the immediate parent is hidden (e.g., a surveyed anomaly), walks up the tree
        /// to find the next visible ancestor.
        /// </summary>
        private Entity? GetVisibleParent(PositionDB positionDB, StarSystem? starSystem)
        {
            if (starSystem == null)
                return positionDB.Parent;

            if (!_uiState.StarSystemStates.TryGetValue(starSystem.ManagerID, out var systemState))
                return positionDB.Parent;

            var parent = positionDB.Parent;
            // Cycle guard (fixes the "click a fleet -> game freezes in FleetWindow" HANG, 2026-07-03). The engine
            // SELF-PARENTS root bodies — a root star's PositionDB.Parent points at ITSELF, not null (MoveState/
            // PositionDB.AbsolutePosition special-case `Parent == OwningEntity`). This walk only stops on null, a
            // visible parent, or a parent with no PositionDB — so if it reaches a self-parented node (or any
            // parent-chain cycle) that ISN'T in AllEntities, `parent = parentPositionDB.Parent` returns the same
            // entity forever and the main loop wedges. `visited.Add` returns false the moment we revisit an id,
            // breaking the loop. (The engine guards this same self-parent case in its own position math; this UI
            // walk didn't.)
            var visited = new HashSet<int>();
            while (parent != null && visited.Add(parent.Id))
            {
                // Check if this parent is visible to the faction
                if (systemState.AllEntities.ContainsKey(parent.Id))
                    return parent;

                // Walk up to the next parent
                if (parent.TryGetDataBlob<PositionDB>(out var parentPositionDB))
                    parent = parentPositionDB.Parent;
                else
                    break;
            }

            // If no visible parent found, return the root or null
            return positionDB.Root != positionDB.OwningEntity ? positionDB.Root : null;
        }
    }
}