using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Blueprints;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Extensions;
using Pulsar4X.DataStructures;
using Pulsar4X.Energy;
using Pulsar4X.Factions;
using Pulsar4X.Damage;
using Pulsar4X.Ships;
using Pulsar4X.Storage;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using SDL3;

namespace Pulsar4X.Client
{
    public class ShipDesignWindow : PulsarGuiWindow
    {
        private bool ShowNoDesigns = false;
        private byte[] SelectedDesignName =  Utils.BytesFromString("foo", 32);
        private List<string> _existingShipDesignNames = new();
        private List<string> _existingShipDesignIDs = new();
        private string SelectedExistingDesignID = String.Empty;
        private ShipDesign _workingDesign;
        private bool SelectedDesignObsolete;
        bool _imagecreated = false;

        private List<ComponentDesign> AvailableShipComponents = new();
        private List<ComponentDesign> AllShipComponents = new();
        private static string[]? _sortedComponentNames;
        private int _componentFilterIndex = 0;

        List<(ComponentDesign design, int count)> SelectedComponents = new List<(ComponentDesign design, int count)>();

        // --- Entity-kind (Ship vs Ground Unit) assembly seam (2026-07-15) -------------------------------------------
        // The Entity Assembler builds BOTH ships and ground units in ONE window, branching on the CHASSIS. The KIND is
        // read from the mounted chassis component's IChassisAtb (ShipHullAtb -> Ship, GroundChassisAtb -> Ground). Before
        // a chassis is mounted (an empty design) this combo bootstraps which parts to show. Index 0 = Ship is the
        // DEFAULT, so with NO ground chassis present the whole window is byte-identical to the ship-only original.
        private static readonly string[] _assemblyKindNames = { "Ship", "Ground Unit", "Station", "Building" };
        private const int GroundKindIndex = 1;
        private const int StationKindIndex = 2;
        private const int BuildingKindIndex = 3;
        private int _assemblyKindIndex = 0;

        private IntPtr _shipImgPtr;

        //TODO: armor, temporary, maybe density should be an "equvelent" and have a different mass? (damage calcs use density for penetration)
        List<ArmorBlueprint> _armorSelection = new List<ArmorBlueprint>();
        private string[]? _armorNames;
        private int _armorIndex = 0;
        private float _armorThickness = 10;
        private ArmorBlueprint? _armor;
        private double _armorMass = 0;

        private int rawimagewidth;
        private int rawimageheight;




        //energy
        private double _estor;
        private double _egen;

        //mass
        private double _massDry;
        private double _massWet;
        private double _grossTonnage;
        //warp
        private double _wcc;
        private double _wsc;
        private double _wec;
        private double _wspd;
        //newt
        private double _tn;
        private double _ttwr;
        private double _dv;
        //fuel
        private double _fuelStoreMass;
        private double _fuelStoreVolume;
        private ICargoable? _fuelType;
        //cargo
        private double _cvol = 0;
        private double _trnge = 0;
        private double _trate = 0;


        bool displayimage = true;
        private EntityDamageProfileDB? _profile;
        private bool existingdesignsstatus = true;
        bool DesignChanged = false;

        private FactionInfoDB _factionInfoDB;

        private ShipDesignWindow()
        {
            //_flags = ImGuiWindowFlags.NoCollapse;
            if(_uiState.Faction == null)
                throw new NullReferenceException("_uiState.Faction cannot be null");

            _factionInfoDB = _uiState.Faction.GetDataBlob<FactionInfoDB>();

            RefreshComponentDesigns();
            RefreshArmor();
            RefreshExistingClasses();
        }

        public override void OnSystemTickChange(DateTime newDateTime)
        {
            RefreshComponentDesigns();
            RefreshExistingClasses();
        }

        internal static ShipDesignWindow GetInstance()
        {
            ShipDesignWindow thisitem;
            if (!_uiState.LoadedWindows.ContainsKey(typeof(ShipDesignWindow)))
            {
                thisitem = new ShipDesignWindow();
                thisitem.RefreshComponentDesigns();
                thisitem.RefreshExistingClasses();
            }
            else
                thisitem = (ShipDesignWindow)_uiState.LoadedWindows[typeof(ShipDesignWindow)];

            return thisitem;
        }

        void RefreshComponentDesigns()
        {
            AllShipComponents = _factionInfoDB.ComponentDesigns.Values.ToList();
            AllShipComponents.Sort((a, b) => a.Name.CompareTo(b.Name));

            var templatesByGroup = AllShipComponents.GroupBy(t => t.ComponentType);
            var groupNames = templatesByGroup.Select(g => g.Key).ToList();
            var sortedTempGroupNames = groupNames.OrderBy(name => name).ToArray();
            _sortedComponentNames = new string[sortedTempGroupNames.Length + 1];
            _sortedComponentNames[0] = "All";
            Array.Copy(sortedTempGroupNames, 0, _sortedComponentNames, 1, sortedTempGroupNames.Length);

            if(_componentFilterIndex == 0)
            {
                AvailableShipComponents = new List<ComponentDesign>(AllShipComponents);
            }
            else
            {
                AvailableShipComponents = AllShipComponents.Where(t => t.ComponentType.Equals(_sortedComponentNames[_componentFilterIndex])).ToList();
            }
        }

        void RefreshExistingClasses()
        {
            var designs = _factionInfoDB.ShipDesigns.Values.Where(d => !d.IsObsolete).ToList();
            designs.Sort((a, b) => a.Name.CompareTo(b.Name));
            _existingShipDesignNames = new List<string>();
            _existingShipDesignIDs = new List<string>();
            foreach (var design in designs)
            {
                _existingShipDesignIDs.Add(design.UniqueID);
                _existingShipDesignNames.Add(design.Name);
            }

            if(_existingShipDesignNames.Count == 0)
            {
                ShowNoDesigns = true;
                return;
            }
            if(SelectedExistingDesignID.IsNullOrEmpty() && _existingShipDesignNames.Count > 0
               && _factionInfoDB.ShipDesigns.TryGetValue(_existingShipDesignIDs[0], out var firstDesign))
                Select(firstDesign);

            ShowNoDesigns = false;
        }

        void RefreshArmor()
        {
            var factionData = _uiState.Faction.GetDataBlob<FactionInfoDB>().Data;
            _armorNames = new string[factionData.Armor.Count];
            int i = 0;
            foreach (var kvp in factionData.Armor)
            {
                var armorMat = factionData.CargoGoods.GetAny(kvp.Value.ResourceID);
                _armorSelection.Add(kvp.Value);

                _armorNames[i]= armorMat?.Name ?? "Unknown";

                // Pick a default armor WITHOUT hard-indexing a key that might not exist. The currently-viewed
                // faction can lack "plastic-armor" — notably the SpaceMaster faction that SM mode switches the
                // view to has no unlocked armor — and `factionData.Armor["plastic-armor"]` then threw
                // KeyNotFoundException and crashed the whole client when the Ship Design window opened in SM
                // mode. Default to plastic-armor when present, otherwise just the first armor available.
                if (kvp.Key == "plastic-armor" || i == 0)
                    _armor = kvp.Value;
                i++;
            }
            //TODO: bleed over from mod data to get a default armor...
            _armorThickness = 3;
        }

        void Select(ShipDesign design)
        {
            _workingDesign = design.Clone(_factionInfoDB);
            SelectedExistingDesignID = _workingDesign.UniqueID;
            SelectedDesignName = Utils.BytesFromString(_workingDesign.Name, 32);
            SelectedComponents = _workingDesign.Components;
            SelectedDesignObsolete = _workingDesign.IsObsolete;
            _armor = _workingDesign.Armor.type;
            _armorIndex = _armorSelection.IndexOf(_armor);
            _armorThickness = _workingDesign.Armor.thickness;
            // A loaded design is always a ShipDesign (ground designs live in IndustryDesigns, never selected here), so
            // reset the kind combo to Ship — the mounted-hull reflection in DisplayComponentSelection confirms it, and
            // this guards the hull-less edge case so a loaded ship can never render on the ground path.
            _assemblyKindIndex = 0;
            DesignChanged = true;
            UpdateShipStats();
        }

        // --- Chassis-kind detection (the ship / ground branch, 2026-07-15) --------------------------------------------

        /// <summary>The chassis component mounted in the current design (the one part carrying an IChassisAtb — a ship
        /// hull or a ground frame), or null if none is mounted yet. The chassis is identified by the additive IChassisAtb
        /// seam, not a per-kind flag, so this generalises to stations/buildings later.</summary>
        private (ComponentDesign design, Pulsar4X.Interfaces.IChassisAtb chassis)? SelectedChassis()
        {
            foreach (var (design, count) in SelectedComponents)
            {
                if (design == null || count <= 0) continue;
                foreach (var atb in design.AttributesByType.Values)
                {
                    if (atb is Pulsar4X.Interfaces.IChassisAtb ch)
                        return (design, ch);
                }
            }
            return null;
        }

        /// <summary>True when the current design is a GROUND unit (its mounted chassis budgets in carry-strength); else it
        /// is a SHIP (the existing, byte-identical path). With no chassis mounted yet, falls back to the kind combo (which
        /// defaults to Ship), so an empty/hull-only ship design is unchanged.</summary>
        private bool IsGroundAssembly()
        {
            var c = SelectedChassis();
            if (c.HasValue)
                return c.Value.chassis.BudgetKind == Pulsar4X.Interfaces.ChassisBudgetKind.Carry;
            return _assemblyKindIndex == GroundKindIndex;
        }

        /// <summary>True when the current design is a STATION (its mounted chassis budgets in STRUCTURE — the off-world
        /// frame). Falls back to the kind combo when no chassis is mounted. Same shape as <see cref="IsGroundAssembly"/>,
        /// so the ship path stays byte-identical (a station chassis is the only Structure-budget frame).</summary>
        private bool IsStationAssembly()
        {
            var c = SelectedChassis();
            if (c.HasValue)
                return c.Value.chassis.BudgetKind == Pulsar4X.Interfaces.ChassisBudgetKind.Structure;
            return _assemblyKindIndex == StationKindIndex;
        }

        /// <summary>True when the current design is a BUILDING (its mounted foundation budgets in FOOTPRINT — the
        /// planet-side base). Falls back to the kind combo when no foundation is mounted. Same shape as the ship/ground/
        /// station branches, so those paths stay byte-identical (a building foundation is the only Footprint-budget frame).</summary>
        private bool IsBuildingAssembly()
        {
            var c = SelectedChassis();
            if (c.HasValue)
                return c.Value.chassis.BudgetKind == Pulsar4X.Interfaces.ChassisBudgetKind.Footprint;
            return _assemblyKindIndex == BuildingKindIndex;
        }

        /// <summary>Which ComponentMountType the available-parts list is filtered to: the mounted chassis's PartMount if
        /// one is present (ship hull -> ShipComponent, ground frame -> GroundUnit), else the kind combo's mount. Ship
        /// resolves to ShipComponent, so the ship parts list is byte-identical to the original hardcoded filter.</summary>
        private ComponentMountType ActivePartMount()
        {
            var c = SelectedChassis();
            if (c.HasValue)
                return c.Value.chassis.PartMount;
            if (_assemblyKindIndex == GroundKindIndex) return ComponentMountType.GroundUnit;
            if (_assemblyKindIndex == StationKindIndex) return ComponentMountType.Station;
            if (_assemblyKindIndex == BuildingKindIndex) return ComponentMountType.PlanetInstallation;
            return ComponentMountType.ShipComponent;
        }

        internal override void Display()
        {
            if(!IsActive) return;

            // The ENTITY ASSEMBLER (not a "ship designer"): the Component Designer makes the pieces; this window
            // ASSEMBLES those pieces into any buildable ENTITY — a ship, a station, an installation/building, a
            // ground unit — a chassis + a list of components (docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md §0, the one
            // designer + one assembler). v1 assembles ship-class designs; generalising to every entity kind is the
            // in-progress work (same window, no new window — see UNIVERSAL-ASSEMBLY §0).
            if (Window.Begin("Entity Assembler", ref IsActive, _flags))
            {
                if(_existingShipDesignNames.Count != _uiState.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.Count)
                {
                    RefreshExistingClasses();
                }
                if (AllShipComponents.Count != _uiState.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns.Values.Count)
                {
                    RefreshComponentDesigns();
                }

                DisplayExistingDesigns();
                ImGui.SameLine();
                ImGui.SetCursorPosY(27f);

                // NOTE: no early `return` here. A bare `return` inside the Window.Begin(...) block skips Window.End()
                // below, leaving "Entity Assembler" open — which corrupts the ImGui window stack and cascades a
                // 'Begin(...) called while already inside "Entity Assembler"' error onto EVERY other window that frame (the
                // whole-UI break seen in the playtest). The DevTest starts with NO ship designs, so ShowNoDesigns is
                // ALWAYS true here — this path is the common case, not an edge. Use if/else so End() always runs.
                if(ShowNoDesigns)
                {
                    ImGui.Text("Create a new design to begin editing.");
                }
                else
                {
                    Vector2 windowContentSize = ImGui.GetContentRegionAvail();
                    var firstChildSize = new Vector2(windowContentSize.X * 0.33f, windowContentSize.Y);
                    var secondChildSize = new Vector2(windowContentSize.X * 0.33f, windowContentSize.Y);
                    var thirdChildSize = new Vector2(windowContentSize.X * 0.33f - (windowContentSize.X * 0.01f), windowContentSize.Y);
                    if(ImGui.BeginChild("ShipDesign1", firstChildSize, ImGuiChildFlags.Borders))
                    {
                        DisplayComponentSelection();
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(27f);
                    if(ImGui.BeginChild("ShipDesign2", secondChildSize, ImGuiChildFlags.Borders))
                    {
                        DisplayComponents();
                    }
                    ImGui.EndChild();
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(27f);
                    if(ImGui.BeginChild("ShipDesign3", thirdChildSize, ImGuiChildFlags.Borders))
                    {
                        DisplayStats();
                    }
                    ImGui.EndChild();
                }
            }
            Window.End();
        }

        internal void NewShipButton()
        {
            if (ImGui.Button("Save Design"))
            {
                int version = 0;
                var name = Utils.StringFromBytes(SelectedDesignName);

                if(name.IsNotNullOrEmpty())
                {

                    //we're using version 0 if the design is not built yet.
                    if(_existingShipDesignNames.Contains(name) && _workingDesign.DesignVersion > 0)
                    {
                        _workingDesign.DesignVersion += 1;
                    }
                    else
                    {
                        _workingDesign.Name = name;
                        if (_factionInfoDB.ShipDesigns.ContainsKey(_workingDesign.UniqueID))
                        {
                            _factionInfoDB.ShipDesigns.Remove(_workingDesign.UniqueID);
                        }
                    }

                    if(_armor == null)
                        throw new NullReferenceException();
                    _workingDesign.Armor = (_armor, _armorThickness);
                    _workingDesign.IsObsolete = SelectedDesignObsolete;


                    _workingDesign.Initialise(_factionInfoDB);


                    if(_workingDesign.IsObsolete)
                    {
                        // If the design is obsolete mark it is invalid so it can't be produced
                        _workingDesign.IsValid = false;
                    }
                    else
                    {
                        _workingDesign.IsValid = IsDesignValid();
                    }

                    if(_workingDesign.IsObsolete)
                    {
                        SelectedExistingDesignID = String.Empty;
                    }

                    RefreshExistingClasses();
                    // var shipDesign = new ShipDesign(_uiState.Faction.GetDataBlob<FactionInfoDB>(), name, SelectedComponents, (_armor, _armorThickness))
                    // {
                    //     DesignVersion = version
                    // };
                }
            }
        }

        // --- GROUND unit assembly (2026-07-15) -----------------------------------------------------------------------

        /// <summary>The right-panel readout for a GROUND unit — its emergent stats + carry-budget validity come from the
        /// engine assembler (GroundUnitAssembly.Compute), NOT the ship thrust/warp/energy math. Draws a budget readout
        /// (carry used / capacity, over-budget in red), the emergent combat stats, the assembler's validity Problems, and
        /// the name + Save. Never routes through _workingDesign / _armor (ship-only state).</summary>
        internal void DisplayGroundStats()
        {
            DisplayHelpers.Header("Ground Unit", "A ground unit's stats emerge from the chassis (frame) plus the parts you mount on it.");

            var frame = SelectedChassis()?.design;
            if (frame == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.TerribleColor);
                ImGui.TextUnformatted("Add a chassis (frame) from the parts list to begin.");
                ImGui.PopStyleColor();
            }
            else
            {
                // Everything that isn't the frame is a "part" fed to the assembler.
                var parts = new List<(ComponentDesign design, int count)>();
                foreach (var (d, c) in SelectedComponents)
                {
                    if (d == null || c <= 0 || ReferenceEquals(d, frame)) continue;
                    parts.Add((d, c));
                }

                var r = Pulsar4X.GroundCombat.GroundUnitAssembly.Compute(frame, parts);

                if (ImGui.BeginTable("GroundStatsTable", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableSetupColumn("Attribute", ImGuiTableColumnFlags.None);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                    ImGui.TableHeadersRow();

                    void Row(string k, string v)
                    {
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(k);
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(v);
                    }

                    // BUDGET readout — carry-strength consumed vs the frame+augment capacity, over-budget in red.
                    bool over = r.UsedCapacity > r.CarryCapacity;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted("Carry Budget");
                    ImGui.TableNextColumn();
                    if (over) ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted(r.UsedCapacity.ToString("0") + " / " + r.CarryCapacity.ToString("0") + (over ? "  OVER" : ""));
                    if (over) ImGui.PopStyleColor();

                    Row("Attack", r.Attack.ToString("0"));
                    Row("Defense", r.Defense.ToString("0"));
                    Row("Hit Points", r.HitPoints.ToString("0"));
                    Row("Range (hex)", r.Range.ToString());
                    Row("Evasion", r.Evasion.ToString("0.00"));
                    Row("Shield", r.Shield.ToString("0"));
                    // TRAINING — the best mounted cadre's veterancy multiplier (baked into Attack + toughness at raise;
                    // 1.00× = green/untrained). Previously computed but invisible in this readout.
                    Row("Training", r.TrainingMultiplier.ToString("0.00") + "×");
                    // POWER supply-vs-demand — ALWAYS-ON now (was a red "Problems" line only on violation): energy weapons
                    // draw vs reactors supply, over-budget in red so the margin is visible before it becomes a violation.
                    bool underPowered = r.EnergyDemand_W > r.ReactorSupply_W;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted("Power (draw / supply)");
                    ImGui.TableNextColumn();
                    if (underPowered) ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted(r.EnergyDemand_W.ToString("0") + " / " + r.ReactorSupply_W.ToString("0") + " W" + (underPowered ? "  UNDER" : ""));
                    if (underPowered) ImGui.PopStyleColor();
                    // AMMO capacity — ALWAYS-ON: the Σ magazine store an ammo-fed weapon (flak / railgun) draws from
                    // (0 kg = no magazine → an ammo weapon can't be fed, surfaced in Problems below).
                    Row("Ammo Capacity", r.AmmoCapacity_kg.ToString("0") + " kg");
                    Row("Build Mass", Stringify.Mass(r.Mass));
                    Row("Damage Type", r.DamageType.ToString());

                    ImGui.EndTable();
                }

                // Validity — the carry / power / ammo gates the assembler computes. Any problem = not buildable.
                if (!r.Valid)
                {
                    ImGui.NewLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted("Current design is invalid:");
                    ImGui.PopStyleColor();
                    foreach (var p in r.Problems)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.MediocreColor);
                        ImGui.TextWrapped(p.Replace("%", "%%")); // escape % — TextWrapped is printf (client CLAUDE.md printf trap)
                        ImGui.PopStyleColor();
                    }
                }
            }

            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
            ImGui.TextUnformatted("Details");
            ImGui.PopStyleColor();
            ImGui.Separator();

            ImGui.TextUnformatted("Design Name:");
            ImGui.InputText("###Design Name", SelectedDesignName, (uint)SelectedDesignName.Length);
            ImGui.NewLine();
            SaveGroundDesign();
        }

        /// <summary>Register the assembled ground unit as a buildable faction design via the engine assembler
        /// (GroundUnitAssembly.RegisterAssembledDesign) — it rides the normal industry rails, NOT the ShipDesign path.
        /// No armor block required, so it never hits the ship-save armor-null throw. Re-saving under the same name
        /// UPDATES the existing design in place (id reused via a guarded lookup — never hard-indexes the faction store).</summary>
        internal void SaveGroundDesign()
        {
            if (!ImGui.Button("Save Ground Unit"))
                return;

            var name = Utils.StringFromBytes(SelectedDesignName);
            if (name.IsNullOrEmpty())
                return;

            var chassis = SelectedChassis();
            if (chassis == null)
                return; // no frame — the panel already prompts for one

            var frame = chassis.Value.design;
            var parts = new List<(ComponentDesign design, int count)>();
            foreach (var (d, c) in SelectedComponents)
            {
                if (d == null || c <= 0 || ReferenceEquals(d, frame)) continue;
                parts.Add((d, c));
            }

            // Reuse an existing ground design's id if we've already saved one under this name (update in place),
            // else mint a new one. Guarded iteration — no hard-index of the faction store.
            string id = null;
            foreach (var kvp in _factionInfoDB.IndustryDesigns)
            {
                if (kvp.Value is Pulsar4X.GroundCombat.GroundUnitDesign g && g.Name == name)
                {
                    id = kvp.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(id))
                id = "grounddesign-" + Guid.NewGuid().ToString();

            var design = Pulsar4X.GroundCombat.GroundUnitAssembly.RegisterAssembledDesign(_factionInfoDB, id, name, frame, parts);
            SessionLog.Action("ground-unit design saved: '" + name + "' (id " + id + ") frame='" + frame.Name
                + "' parts=" + parts.Count + " atk=" + design.Attack.ToString("0") + " hp=" + design.HitPoints.ToString("0"));
        }

        /// <summary>The right-panel readout for a STATION — its totals + structure-budget validity come from the engine
        /// assembler (StationAssembly.Compute), NOT the ship math. Draws the budget readout (structure used / provided,
        /// over-budget in red — the "chassis gives the budget, modules consume it" rule), the totals, the validity
        /// Problems, and the name + Save. Never routes through _workingDesign / _armor (ship-only state).</summary>
        internal void DisplayStationStats()
        {
            DisplayHelpers.Header("Station", "A station's totals emerge from the chassis (frame) plus the modules you mount on it. The chassis provides the structure budget; each module consumes it.");

            var chassisSel = SelectedChassis();
            if (chassisSel == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.TerribleColor);
                ImGui.TextUnformatted("Add a station chassis (frame) from the parts list to begin.");
                ImGui.PopStyleColor();
            }
            else
            {
                var chassis = chassisSel.Value.design;

                // Everything that isn't the chassis is a "module" fed to the assembler.
                var modules = new List<(ComponentDesign design, int count)>();
                foreach (var (d, c) in SelectedComponents)
                {
                    if (d == null || c <= 0 || ReferenceEquals(d, chassis)) continue;
                    modules.Add((d, c));
                }

                var r = Pulsar4X.Stations.StationAssembly.Compute(chassis, modules);

                if (ImGui.BeginTable("StationStatsTable", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableSetupColumn("Attribute", ImGuiTableColumnFlags.None);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                    ImGui.TableHeadersRow();

                    void Row(string k, string v)
                    {
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(k);
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(v);
                    }

                    // BUDGET readout — structure consumed vs the chassis-provided budget, over-budget in red.
                    bool over = r.UsedStructure > r.StructuralBudget;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted("Structure Budget");
                    ImGui.TableNextColumn();
                    if (over) ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted(r.UsedStructure.ToString("0") + " / " + r.StructuralBudget.ToString("0") + (over ? "  OVER" : ""));
                    if (over) ImGui.PopStyleColor();

                    Row("Modules", r.ModuleCount.ToString());
                    Row("Build Mass", Stringify.Mass(r.BuildMass));
                    Row("Crew Required", r.CrewRequired.ToString());

                    ImGui.EndTable();
                }

                if (!r.Valid)
                {
                    ImGui.NewLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted("Current design is invalid:");
                    ImGui.PopStyleColor();
                    foreach (var p in r.Problems)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.MediocreColor);
                        ImGui.TextWrapped(p.Replace("%", "%%")); // escape % — TextWrapped is printf (client CLAUDE.md printf trap)
                        ImGui.PopStyleColor();
                    }
                }
            }

            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
            ImGui.TextUnformatted("Details");
            ImGui.PopStyleColor();
            ImGui.Separator();

            ImGui.TextUnformatted("Design Name:");
            ImGui.InputText("###Design Name", SelectedDesignName, (uint)SelectedDesignName.Length);
            ImGui.NewLine();
            SaveStationDesign();
        }

        /// <summary>Register the assembled station as a buildable faction design via the engine assembler
        /// (StationDesign.RegisterStationDesign) — it rides the normal industry rails, and on build-completion DEPLOYS a
        /// station at the building colony's body with these modules installed. No armor block (never hits the ship-save
        /// armor-null throw). Re-saving under the same name UPDATES the existing station design in place (id reused via a
        /// guarded lookup — never hard-indexes the faction store).</summary>
        internal void SaveStationDesign()
        {
            if (!ImGui.Button("Save Station"))
                return;

            var name = Utils.StringFromBytes(SelectedDesignName);
            if (name.IsNullOrEmpty())
                return;

            var chassisSel = SelectedChassis();
            if (chassisSel == null)
                return; // no frame — the panel already prompts for one

            var chassis = chassisSel.Value.design;
            var modules = new List<(ComponentDesign design, int count)>();
            foreach (var (d, c) in SelectedComponents)
            {
                if (d == null || c <= 0 || ReferenceEquals(d, chassis)) continue;
                modules.Add((d, c));
            }

            // Reuse an existing station design's id if we've already saved one under this name (update in place), else mint
            // a new one. Guarded iteration — no hard-index of the faction store.
            string id = null;
            foreach (var kvp in _factionInfoDB.IndustryDesigns)
            {
                if (kvp.Value is Pulsar4X.Stations.StationDesign st && st.Name == name)
                {
                    id = kvp.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(id))
                id = "stationdesign-" + Guid.NewGuid().ToString();

            var design = Pulsar4X.Stations.StationDesign.RegisterStationDesign(_factionInfoDB, id, name, chassis, modules);
            SessionLog.Action("station design saved: '" + name + "' (id " + id + ") chassis='" + chassis.Name
                + "' modules=" + modules.Count + " costItems=" + design.ResourceCosts.Count);
        }

        /// <summary>The right-panel readout for a BUILDING — its totals + footprint-budget validity come from the engine
        /// assembler (BuildingAssembly.Compute). Draws the budget readout (footprint used / provided, over in red — the
        /// "foundation gives the budget, modules consume it" rule), the totals, the validity Problems, and the name +
        /// Save. Never routes through _workingDesign / _armor (ship-only state).</summary>
        internal void DisplayBuildingStats()
        {
            DisplayHelpers.Header("Building", "A building's totals emerge from the foundation plus the modules you mount on it. The foundation provides the footprint budget; each module consumes it.");

            var chassisSel = SelectedChassis();
            if (chassisSel == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.TerribleColor);
                ImGui.TextUnformatted("Add a building foundation from the parts list to begin.");
                ImGui.PopStyleColor();
            }
            else
            {
                var foundation = chassisSel.Value.design;

                var modules = new List<(ComponentDesign design, int count)>();
                foreach (var (d, c) in SelectedComponents)
                {
                    if (d == null || c <= 0 || ReferenceEquals(d, foundation)) continue;
                    modules.Add((d, c));
                }

                var r = Pulsar4X.Colonies.BuildingAssembly.Compute(foundation, modules);

                if (ImGui.BeginTable("BuildingStatsTable", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableSetupColumn("Attribute", ImGuiTableColumnFlags.None);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                    ImGui.TableHeadersRow();

                    void Row(string k, string v)
                    {
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(k);
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(v);
                    }

                    bool over = r.UsedFootprint > r.FootprintBudget;
                    ImGui.TableNextColumn(); ImGui.TextUnformatted("Footprint Budget");
                    ImGui.TableNextColumn();
                    if (over) ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted(r.UsedFootprint.ToString("0") + " / " + r.FootprintBudget.ToString("0") + (over ? "  OVER" : ""));
                    if (over) ImGui.PopStyleColor();

                    Row("Modules", r.ModuleCount.ToString());
                    Row("Build Mass", Stringify.Mass(r.BuildMass));
                    Row("Crew Required", r.CrewRequired.ToString());

                    ImGui.EndTable();
                }

                if (!r.Valid)
                {
                    ImGui.NewLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                    ImGui.TextUnformatted("Current design is invalid:");
                    ImGui.PopStyleColor();
                    foreach (var p in r.Problems)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.MediocreColor);
                        ImGui.TextWrapped(p.Replace("%", "%%")); // escape % — TextWrapped is printf (client CLAUDE.md printf trap)
                        ImGui.PopStyleColor();
                    }
                }
            }

            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
            ImGui.TextUnformatted("Details");
            ImGui.PopStyleColor();
            ImGui.Separator();

            ImGui.TextUnformatted("Design Name:");
            ImGui.InputText("###Design Name", SelectedDesignName, (uint)SelectedDesignName.Length);
            ImGui.NewLine();
            SaveBuildingDesign();
        }

        /// <summary>Register the assembled building as a buildable faction design via the engine assembler
        /// (BuildingDesign.RegisterBuildingDesign) — it rides the normal industry rails, and on build-completion installs
        /// its foundation + modules on the building colony. No armor block. Re-saving under the same name UPDATES the
        /// existing building design in place (id reused via a guarded lookup — never hard-indexes the faction store).</summary>
        internal void SaveBuildingDesign()
        {
            if (!ImGui.Button("Save Building"))
                return;

            var name = Utils.StringFromBytes(SelectedDesignName);
            if (name.IsNullOrEmpty())
                return;

            var chassisSel = SelectedChassis();
            if (chassisSel == null)
                return; // no foundation — the panel already prompts for one

            var foundation = chassisSel.Value.design;
            var modules = new List<(ComponentDesign design, int count)>();
            foreach (var (d, c) in SelectedComponents)
            {
                if (d == null || c <= 0 || ReferenceEquals(d, foundation)) continue;
                modules.Add((d, c));
            }

            string id = null;
            foreach (var kvp in _factionInfoDB.IndustryDesigns)
            {
                if (kvp.Value is Pulsar4X.Colonies.BuildingDesign b && b.Name == name)
                {
                    id = kvp.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(id))
                id = "buildingdesign-" + Guid.NewGuid().ToString();

            var design = Pulsar4X.Colonies.BuildingDesign.RegisterBuildingDesign(_factionInfoDB, id, name, foundation, modules);
            SessionLog.Action("building design saved: '" + name + "' (id " + id + ") foundation='" + foundation.Name
                + "' modules=" + modules.Count + " costItems=" + design.ResourceCosts.Count);
        }

        internal void DisplayExistingDesigns()
        {
            Vector2 windowContentSize = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("ComponentDesignSelection", new Vector2(Styles.LeftColumnWidth, windowContentSize.Y - 24f), ImGuiChildFlags.Borders, ImGuiWindowFlags.ChildWindow))
            {
                DisplayHelpers.Header("Existing Designs", "Select an existing ship design to edit it.");
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, Styles.LeftColumnWidth - 24);
                ImGui.SetColumnWidth(1, 24);
                for (int index = 0; index < _existingShipDesignNames.Count; index++)
                {
                    string? designID = _existingShipDesignIDs[index];
                    string designName = _existingShipDesignNames[index];
                    // Never hard-index ShipDesigns[designID] — the list is rebuilt from ShipDesigns, but UI state can lag
                    // engine state (a design removed between refreshes), and this window may show a foreign/mismatched
                    // store (SM mode / faction switch). TryGetValue + skip. (Root CLAUDE.md L14 — guard EVERY level.)
                    if (ImGui.Selectable(designName + "###existing-design-" + designID, designID.Equals(SelectedExistingDesignID)))
                    {
                        if (_factionInfoDB.ShipDesigns.TryGetValue(designID, out var picked))
                            Select(picked);
                    }

                    if (ImGui.BeginPopupContextItem())
                    {
                        if (ImGui.MenuItem("Delete###delete-" + designID))
                        {
                            _factionInfoDB.ShipDesigns.Remove(designID);
                            SelectedExistingDesignID = String.Empty;
                            RefreshExistingClasses();
                        }
                        if (ImGui.MenuItem("Obsolete###obsolete-" + designID))
                        {
                            if (_factionInfoDB.ShipDesigns.TryGetValue(designID, out var toObsolete))
                                toObsolete.IsObsolete = true;
                            SelectedExistingDesignID = String.Empty;
                            RefreshExistingClasses();
                        }

                        ImGui.EndPopup();
                    }
                    ImGui.NextColumn();
                    string versionText = "P";
                    if(_factionInfoDB.ShipDesigns.TryGetValue(designID, out var verDesign) && verDesign.DesignVersion > 0)
                        versionText = verDesign.DesignVersion.ToString();
                    ImGui.Text(versionText);
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
            }
            ImGui.EndChild();

            if(ImGui.Button("Create New Design", new Vector2(204f, 0f)))
            {
                string originalName = NameFactory.GetShipName(_uiState.Game), name = originalName;
                int counter = 1;
                while(_factionInfoDB.ShipDesigns.Values.Any(d => d.Name.Equals(name)))
                {
                    name = originalName + " " + counter.ToString();
                    counter++;
                }
                SelectedDesignName = Utils.BytesFromString(name);
                SelectedComponents = new List<(ComponentDesign design, int count)>();
                _assemblyKindIndex = 0; // a new design starts in Ship mode (the default); switch to Ground Unit to assemble one
                GenImage();
                RefreshArmor();
                DesignChanged = true;

                if(_armor == null)
                    throw new NullReferenceException();

                ShipDesign design = new(_factionInfoDB, name, SelectedComponents, (_armor, _armorThickness))
                {
                    IsValid = false
                };
                RefreshExistingClasses();
                SelectedExistingDesignID = design.UniqueID;
            }
        }

        internal void DisplayComponents()
        {
            DisplayHelpers.Header("Current Design");

            if(SelectedComponents.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.TerribleColor);
                ImGui.Text("Add components from the available components list");
                ImGui.PopStyleColor();
            }
            else
            {
                DisplayComponentsTable();
            }

            // A ground unit has NO ship armor block — its armour is a mounted GroundArmorAtb part (summed in the assembler),
            // and it has no _armor selection / _armorNames. Skipping this block also dodges the armor-null throws below on
            // the ground path. (Safe return — DisplayComponents runs inside a BeginChild/EndChild in Display(), so EndChild
            // still runs after this returns.) Ship path is untouched.
            if (IsGroundAssembly() || IsStationAssembly() || IsBuildingAssembly())
                return;

            ImGui.NewLine();
            DisplayHelpers.Header("Armor");
            if(ImGui.BeginTable("CurrentShipDesignTable", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Attribute", ImGuiTableColumnFlags.None, 0.6f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None, 0.4f);
                ImGui.TableHeadersRow();

                ImGui.TableNextColumn();
                ImGui.Text("Type");
                ImGui.TableNextColumn();

                if(_armorNames == null)
                    throw new NullReferenceException();

                if (ImGui.Combo("##Armor Selection", ref _armorIndex, _armorNames, _armorNames.Length))
                {
                    _armor = _armorSelection[_armorIndex];
                    DesignChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.Text("Density");
                ImGui.TableNextColumn();
                ImGui.Text(_armorSelection[_armorIndex].Density.ToString());

                ImGui.TableNextColumn();
                ImGui.Text("Thickness");
                ImGui.TableNextColumn();
                ImGui.Text(_armorThickness.ToString());

                ImGui.SameLine();
                if (ImGui.SmallButton("+##armor")) //todo: imagebutton
                {
                    _armorThickness++;
                    DesignChanged = true;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("-##armor") && _armorThickness > 0) //todo: imagebutton
                {
                    _armorThickness--;
                    DesignChanged = true;
                }

                ImGui.TableNextColumn();
                ImGui.Text("Mass");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Mass(_armorMass));

                ImGui.SameLine();
                ImGui.EndTable();
            }
        }

        internal void DisplayComponentsTable()
        {
            if(ImGui.BeginTable("CurrentShipDesignTable", 3, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.None, 0.25f);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.None, 0.25f);
                ImGui.TableHeadersRow();

                int selectedItem = -1;
                for (int i = 0; i < SelectedComponents.Count; i++)
                {
                    string name = SelectedComponents[i].design.Name;
                    int number = SelectedComponents[i].count;

                    ImGui.TableNextColumn();
                    ImGui.Text(name);

                    bool hovered = ImGui.IsItemHovered();
                    if (hovered)
                    {
                        selectedItem = i;
                        DisplayHelpers.DescriptiveTooltip(SelectedComponents[i].design.Name, SelectedComponents[i].design.TemplateName, SelectedComponents[i].design.Description);
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(number.ToString());

                    ImGui.SameLine();
                    if (ImGui.SmallButton("+##" + i)) //todo: imagebutton
                    {
                        SelectedComponents[i] = (SelectedComponents[i].design, SelectedComponents[i].count + 1);
                        DesignChanged = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("-##" + i) && number > 0) //todo: imagebutton
                    {
                        SelectedComponents[i] = (SelectedComponents[i].design, SelectedComponents[i].count - 1);
                        DesignChanged = true;
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton("x##" + i)) //todo: imagebutton
                    {
                        SelectedComponents.RemoveAt(i);
                        DesignChanged = true;
                    }

                    if (i > 0)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("^##" + i)) //todo: imagebutton
                        {

                            (ComponentDesign design, int count) item = SelectedComponents[i];
                            SelectedComponents.RemoveAt(i);
                            SelectedComponents.Insert(i - 1, item);

                            DesignChanged = true;
                        }
                    }
                    if (i < SelectedComponents.Count - 1)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("v##" + i)) //todo: imagebutton
                        {
                            (ComponentDesign design, int count) item = SelectedComponents[i];
                            SelectedComponents.RemoveAt(i);
                            SelectedComponents.Insert(i + 1, item);
                            DesignChanged = true;
                        }
                    }
                }

                ImGui.EndTable();
            }
        }

        internal void DisplayComponentSelection()
        {
            if(_sortedComponentNames == null)
                throw new NullReferenceException();

            DisplayHelpers.Header("Available Components");

            var availableSize = ImGui.GetContentRegionAvail();

            // Entity-kind selector (Ship / Ground Unit) — the branch point. A mounted chassis LOCKS the kind (we reflect
            // it here so the combo can't fight the design); an empty design uses this combo to bootstrap which parts to
            // show. Index 0 = Ship, so the ship path is byte-identical (same filter, same list).
            var mountedChassis = SelectedChassis();
            if (mountedChassis.HasValue)
            {
                switch (mountedChassis.Value.chassis.BudgetKind)
                {
                    case Pulsar4X.Interfaces.ChassisBudgetKind.Carry:     _assemblyKindIndex = GroundKindIndex; break;
                    case Pulsar4X.Interfaces.ChassisBudgetKind.Structure: _assemblyKindIndex = StationKindIndex; break;
                    case Pulsar4X.Interfaces.ChassisBudgetKind.Footprint: _assemblyKindIndex = BuildingKindIndex; break;
                    default:                                              _assemblyKindIndex = 0; break;
                }
            }
            ImGui.SetNextItemWidth(availableSize.X);
            ImGui.Combo("###assembly-kind", ref _assemblyKindIndex, _assemblyKindNames, _assemblyKindNames.Length);

            ImGui.SetNextItemWidth(availableSize.X);
            if(ImGui.Combo("###component-filter", ref _componentFilterIndex, _sortedComponentNames, _sortedComponentNames.Length))
            {
                if(_componentFilterIndex == 0)
                {
                    AvailableShipComponents = new List<ComponentDesign>(AllShipComponents);
                }
                else
                {
                    AvailableShipComponents = AllShipComponents.Where(t => t.ComponentType.Equals(_sortedComponentNames[_componentFilterIndex])).ToList();
                }
                ImGui.EndCombo();
            }

            if(ImGui.BeginTable("DesignStatsTables", 3, Styles.TableFlags | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.3f);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 0.2f);
                ImGui.TableHeadersRow();

                // Filter the parts list by the ACTIVE chassis's PartMount (was hardcoded ShipComponent). Ship -> ShipComponent
                // (byte-identical); Ground -> GroundUnit, so a ground chassis shows ground frames + parts.
                var activeMount = ActivePartMount();
                for (int i = 0; i < AvailableShipComponents.Count; i++)
                {
                    if(!AvailableShipComponents[i].ComponentMountType.HasFlag(activeMount))
                        continue;

                    var design = AvailableShipComponents[i];
                    string name = design.Name;

                    ImGui.TableNextColumn();
                    ImGui.Text(name);
                    if(ImGui.IsItemHovered())
                    {
                        void TooltipExtension()
                        {
                            ImGui.Text("Mass: " + Stringify.Mass(AvailableShipComponents[i].MassPerUnit));
                            ImGui.Text("Volume: " + Stringify.Volume(AvailableShipComponents[i].VolumePerUnit));
                            ImGui.Text("Crew Required: " + AvailableShipComponents[i].CrewReq);
                        }

                        DisplayHelpers.DescriptiveTooltip(AvailableShipComponents[i].Name, AvailableShipComponents[i].TemplateName, AvailableShipComponents[i].Description, TooltipExtension);
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text(design.ComponentType);
                    ImGui.TableNextColumn();
                    ImGui.InvisibleButton($"{i}", new Vector2(4, 8));
                    ImGui.SameLine();
                    if(ImGui.SmallButton("+ Add###add-component-" + i))
                    {
                        SelectedComponents.Add((AvailableShipComponents[i], 1));
                        DesignChanged = true;
                    }
                }

                ImGui.EndTable();
            }
        }

        internal void GenImage()
        {
            if(_profile == null)
                throw new NullReferenceException();

            Textures.CreateTexture(_uiState.ViewPort.Renderer, _profile.DamageProfile, ref _shipImgPtr, SDL.PixelFormat.ARGB8888);
            rawimagewidth = _profile.DamageProfile.Width;
            rawimageheight = _profile.DamageProfile.Height;
            _imagecreated = true;
        }

        internal void DisplayStats()
        {
            // GROUND branch: a ground unit's stats + validity come from GroundUnitAssembly.Compute, NOT the ship
            // thrust/warp/energy math. Route it to its own panel and skip the whole ship stats path (which also reads
            // _workingDesign/_armor — ship-only state a ground design doesn't have). Wrapped so a throw can't skip the
            // window's End (mirrors SafeRender). Safe return — DisplayStats runs inside a BeginChild/EndChild in Display().
            if (IsGroundAssembly())
            {
                try { DisplayGroundStats(); }
                catch (Exception ex) { Console.WriteLine("[RenderError] ShipDesignWindow.DisplayGroundStats threw: " + ex); }
                return;
            }

            // STATION branch: a station's totals + structure-budget validity come from StationAssembly.Compute, NOT the
            // ship thrust/warp/energy math (nor _workingDesign/_armor, which a station has no use for). Same wrapped-return
            // shape as the ground branch. Ship path below is untouched.
            if (IsStationAssembly())
            {
                try { DisplayStationStats(); }
                catch (Exception ex) { Console.WriteLine("[RenderError] ShipDesignWindow.DisplayStationStats threw: " + ex); }
                return;
            }

            // BUILDING branch: a planet-side building's totals + footprint-budget validity come from
            // BuildingAssembly.Compute. Same wrapped-return shape; ship path below is untouched.
            if (IsBuildingAssembly())
            {
                try { DisplayBuildingStats(); }
                catch (Exception ex) { Console.WriteLine("[RenderError] ShipDesignWindow.DisplayBuildingStats threw: " + ex); }
                return;
            }

            DisplayHelpers.Header("Statisitcs", "The attributes of the ship are calculated based on the components you have added to the design.");

            UpdateShipStats();
            if(ImGui.BeginTable("DesignStatsTables", 2, Styles.TableFlags | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableSetupColumn("Attribute", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None);
                ImGui.TableHeadersRow();

                ImGui.TableNextColumn();
                ImGui.Text("Gross Tonnage");
                ImGui.TableNextColumn();
                ImGui.Text(_grossTonnage.ToString(Styles.IntFormat));

                ImGui.TableNextColumn();
                ImGui.Text("Mass (Dry)");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Mass(_massDry));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Wet: " + Stringify.Mass(_massDry + _fuelStoreMass));
                }

                ImGui.TableNextColumn();
                ImGui.Text("Total Thrust");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Thrust(_tn));

                ImGui.TableNextColumn();
                ImGui.Text("Thrust to Mass Ratio");
                ImGui.TableNextColumn();
                ImGui.Text(_ttwr.ToString(Styles.DecimalFormat));

                ImGui.TableNextColumn();
                var fuelName = _fuelType?.Name ?? "Unknown";
                ImGui.Text("Fuel Capacity (" + fuelName + ")");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Mass(_fuelStoreMass));
                ImGui.SameLine();
                ImGui.Text(Stringify.VolumeLtr(_fuelStoreVolume));

                ImGui.TableNextColumn();
                ImGui.Text("Delta V");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Velocity(_dv));

                ImGui.TableNextColumn();
                ImGui.Text("Warp Speed");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Velocity(_wspd));

                ImGui.TableNextColumn();
                ImGui.Text("Warp Bubble Creation");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Power(_wcc));

                ImGui.TableNextColumn();
                ImGui.Text("Warp Bubble Sustain");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Power(_wsc));

                ImGui.TableNextColumn();
                ImGui.Text("Warp Bubble Collapse");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Power(_wec));

                ImGui.TableNextColumn();
                ImGui.Text("Energy Output");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Power(_egen));

                ImGui.TableNextColumn();
                ImGui.Text("Energy Storage");
                ImGui.TableNextColumn();
                ImGui.Text(Stringify.Energy(_estor));

                if (_cvol > 0)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text("Cargo Storage");
                    ImGui.TableNextColumn();
                    ImGui.Text(Stringify.VolumeLtr(_cvol));


                    ImGui.TableNextColumn();
                    ImGui.Text("Cargo Transfer Rate");
                    ImGui.TableNextColumn();
                    if(_trate == 0)
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.MediocreColor);
                    ImGui.Text(Stringify.Mass(_trate));
                    if(_trate == 0)
                        ImGui.PopStyleColor();
                    ImGui.TableNextColumn();
                    ImGui.Text("Cargo Transfer Range");
                    ImGui.TableNextColumn();
                    if(_trnge == 0)
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.MediocreColor);
                    ImGui.Text(Stringify.Velocity(_trnge));
                    if(_trnge == 0)
                        ImGui.PopStyleColor();

                }

                ImGui.EndTable();
            }

            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
            ImGui.Text("Details");
            ImGui.PopStyleColor();
            ImGui.Separator();

            ImGui.Text("Design Name:");
            ImGui.InputText("###Design Name", SelectedDesignName, (uint)SelectedDesignName.Length);
            ImGui.NewLine();
            ImGui.Text("Is Obsolete?");
            ImGui.Checkbox("###IsObsolete", ref SelectedDesignObsolete);

            if(!IsDesignValid())
            {
                ImGui.NewLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.BadColor);
                ImGui.Text("Current design is invalid!");
                // TODO: tell the player what is invalid about their design
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("You will not be able to construct ships with an invalid design.");
                ImGui.PopStyleColor();
            }

            foreach (var warning in Warnings())
            {
                ImGui.Text(warning);
            }

            ImGui.NewLine();
            NewShipButton();
            ImGui.SameLine();
            ImGui.Checkbox("Show Pic", ref displayimage);
            ImGui.NewLine();

            var size = ImGui.GetContentRegionAvail();
            DisplayImage(size.X, size.Y);
        }

        private void UpdateShipStats()
        {
            if(!DesignChanged) return;

            if(_armor == null || _uiState.Faction == null)
                throw new NullReferenceException();

            _profile = new EntityDamageProfileDB(SelectedComponents, (_armor, _armorThickness));
            if(displayimage)
            {
                GenImage();
            }

            long mass = 0;
            double fu = 0;
            double tn = 0;
            double ev = 0;

            double wp = 0;
            double wcc = 0;
            double wsc = 0;
            double wec = 0;
            double egen = 0;
            double estor = 0;
            string thrusterFuel = String.Empty;
            Dictionary<string, double> cstore = new Dictionary<string, double>();

            double volume = 0;

            foreach (var component in SelectedComponents)
            {
                mass += component.design.MassPerUnit * component.count;
                volume += component.design.VolumePerUnit * component.count;
                if (component.design.HasAttribute<NewtonionThrustAtb>())
                {
                    var atb = component.design.GetAttribute<NewtonionThrustAtb>();
                    ev = atb.ExhaustVelocity;
                    fu += atb.FuelBurnRate * component.count;
                    tn += ev * atb.FuelBurnRate * component.count;
                    thrusterFuel = atb.FuelType;
                }

                if (component.design.HasAttribute<WarpDriveAtb>())
                {
                    var atb = component.design.GetAttribute<WarpDriveAtb>();
                    wp += atb.WarpPower * component.count;
                    wcc += atb.BubbleCreationCost * component.count;
                    wsc += atb.BubbleSustainCost * component.count;
                    wec += atb.BubbleCollapseCost * component.count;

                }

                if (component.design.HasAttribute<EnergyGenerationAtb>())
                {
                    var atb = component.design.GetAttribute<EnergyGenerationAtb>();
                    egen += atb.PowerOutputMax * component.count;

                }

                if (component.design.HasAttribute<EnergyStoreAtb>())
                {
                    var atb = component.design.GetAttribute<EnergyStoreAtb>();
                    estor += atb.MaxStore * component.count;
                }

                /*
                if (component.design.HasAttribute<CargoStorageAtb>())
                {
                    var atb = component.design.GetAttribute<CargoStorageAtb>();
                    var typeid = atb.StoreTypeID;
                    var amount = atb.MaxVolume * component.count;
                    if (!cstore.ContainsKey(typeid))
                        cstore.Add(typeid, amount);
                    else
                        cstore[typeid] += amount;
                }

                if (component.design.HasAttribute<CargoTransferAtb>())
                {
                    var atb = component.design.GetAttribute<CargoTransferAtb>();
                    //atb.TransferRange_ms

                }*/
            }

            cstore = StorageSpaceProcessor.CalculatedMaxStorage(_workingDesign);
            var cargoTransfer = StorageSpaceProcessor.CalcRateAndRange(_workingDesign);




            _armorMass = ShipDesign.GetArmorMass(_profile, _uiState.Faction.GetDataBlob<FactionInfoDB>().Data.CargoGoods);
            mass += (long)Math.Round(_armorMass);

            var K = 0.2 + 0.02 * Math.Log10(volume);

            _grossTonnage = volume * K; // GT = V * K from: https://en.wikipedia.org/wiki/Gross_tonnage
            _massDry = mass;
            _tn = tn;
            _ttwr = (tn / mass) * 0.01;
            _wcc = wcc;
            _wec = wec;
            _wsc = wsc;
            _wspd = WarpMath.MaxSpeedCalc(wp, mass);
            _egen = egen;
            _estor = estor;
            _trate = cargoTransfer.rate;
            if(double.IsNaN(cargoTransfer.range))
                _trnge = 0;
            else
                _trnge = cargoTransfer.range;
            //double fuelMass = 0;
            if (thrusterFuel.IsNotNullOrEmpty())
            {
                _fuelType = _uiState.Faction.GetDataBlob<FactionInfoDB>().Data.CargoGoods.GetAny(thrusterFuel);
                if (_fuelType != null && cstore.ContainsKey(_fuelType.CargoTypeID))
                {
                    _fuelStoreVolume = cstore[_fuelType.CargoTypeID];
                    var fuelDensity = _fuelType.MassPerUnit / _fuelType.VolumePerUnit;
                    _fuelStoreMass = _fuelStoreVolume * fuelDensity;

                }
            }

            _cvol = 0;
            foreach (var store in cstore)
            {
                if (_fuelType == null || store.Key != _fuelType.CargoTypeID)
                    _cvol += store.Value;
            }

            _massWet = _massDry + _fuelStoreMass;
            _dv = OrbitMath.TsiolkovskyRocketEquation(_massWet, _massDry, ev);

            DesignChanged = false;
        }

        private bool IsDesignValid()
        {
            return _massDry > 0 &&
                    _tn > 0 &&
                    _ttwr > 0 &&
                    _egen > 0 &&
                    _estor > 0;
        }

        internal bool CheckDisplayImage(float maxwidth, float maxheight, float checkwidth)
        {
            if (_shipImgPtr != IntPtr.Zero && displayimage)
            {

                maxwidth = ImGui.GetWindowWidth();// ImGui.GetColumnWidth();;//
                int maxheightint = (int)(maxheight / 4);
                maxheight = maxheightint * 4;//ImGui.GetWindowHeight() * _imageratio;
                float scalew = 1;
                float scaleh = 1;
                float scale;
                scalew = maxwidth / rawimagewidth;
                scaleh = maxheight / rawimageheight;

                scale = Math.Min(scaleh, scalew);

                if (rawimagewidth * scale < checkwidth)
                {
                    return true;
                }
            }
            return false;
        }

        internal void DisplayImage(float maxwidth, float maxheight)
        {
            if (_shipImgPtr != IntPtr.Zero && displayimage)
            {
                int maxheightint = (int)(maxheight / 4);
                maxheight = maxheightint*4;//ImGui.GetWindowHeight() * _imageratio;
                float scalew = 1;
                float scaleh = 1;
                float scale;

                scalew = maxwidth / rawimagewidth;
                scaleh = maxheight / rawimageheight;

                scale = Math.Min(scaleh, scalew);

                ImGui.Image(_shipImgPtr.ToTextureRef(), new System.Numerics.Vector2(rawimagewidth * scale, rawimageheight * scale));
            }
        }

        private List<string> Warnings()
        {
            List<string> warnings = new List<string>();
            if (_cvol > 0 && _trate == 0 || _trnge == 0)
            {
                warnings.Add("This ship has cargo space but no way to transfer cargo by itself");
            }
            if (_wspd == 0)
            {
                warnings.Add("This ship has no warp ability");
            }

            if (_ttwr == 0)
            {
                warnings.Add("This ship has no newtonion thrust");
            }
            return warnings;
        }
    }
}
