using System;
using System.Linq;
using System.Text;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Names;
using Pulsar4X.Ships;
using Pulsar4X.Colonies;
using Pulsar4X.Storage;
using Pulsar4X.Industry;

namespace Pulsar4X.Client
{
    public class DevToolsWindow : PulsarGuiWindow
    {
        // ── Spawn Ship ─────────────────────────────────
        private string[] _shipDesignNames = Array.Empty<string>();
        private ShipDesign[] _shipDesignValues = Array.Empty<ShipDesign>();
        private int _selectedDesign = 0;
        private string[] _bodyNames = Array.Empty<string>();
        private Entity[] _bodyEntities = Array.Empty<Entity>();
        private int _selectedSpawnParent = 0;
        private byte[] _shipNameBuffer = new byte[64];
        private string _spawnStatus = "";

        // ── Create Colony ──────────────────────────────
        private string[] _planetNames = Array.Empty<string>();
        private Entity[] _planetEntities = Array.Empty<Entity>();
        private int _selectedPlanet = 0;
        private int _popMillions = 0;
        private string _colonyStatus = "";

        // ── Add Minerals ───────────────────────────────
        private string[] _cargoEntityNames = Array.Empty<string>();
        private Entity[] _cargoEntities = Array.Empty<Entity>();
        private int _selectedCargoEntity = 0;
        private string[] _mineralNames = Array.Empty<string>();
        private Mineral[] _minerals = Array.Empty<Mineral>();
        private int _selectedMineral = 0;
        private int _mineralAmount = 1000;
        private string _mineralStatus = "";

        private DevToolsWindow()
        {
            _flags = ImGuiWindowFlags.AlwaysAutoResize;
            HardRefresh();
            _uiState.OnStarSystemChanged += _ => HardRefresh();
        }

        public static DevToolsWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(DevToolsWindow)))
                return new DevToolsWindow();
            return (DevToolsWindow)_uiState.LoadedWindows[typeof(DevToolsWindow)];
        }

        void HardRefresh()
        {
            if (_uiState.PlayerFaction == null || _uiState.Game == null) return;
            if (_uiState.StarSystemStates.Count == 0) return;

            var factionInfo = _uiState.PlayerFaction.GetDataBlob<FactionInfoDB>();

            // Ship designs from the player faction
            var designs = factionInfo.ShipDesigns.Values.ToArray();
            _shipDesignValues = designs;
            _shipDesignNames = designs.Select(d => d.Name).ToArray();
            _selectedDesign = 0;

            // All named bodies in the current system (used as spawn parent and colony target)
            var allEntities = _uiState.SelectedSystem.GetAllEntites()
                .Where(e => e.HasDataBlob<MassVolumeDB>() && e.HasDataBlob<NameDB>())
                .ToList();
            _bodyEntities = allEntities.ToArray();
            _bodyNames = allEntities.Select(GetEntityName).ToArray();
            _selectedSpawnParent = 0;

            _planetEntities = _bodyEntities;
            _planetNames = _bodyNames;
            _selectedPlanet = 0;

            // Entities with cargo holds (for mineral injection)
            var cargoEntities = _uiState.SelectedSystem.GetAllEntites()
                .Where(e => e.HasDataBlob<CargoStorageDB>() && e.HasDataBlob<NameDB>())
                .ToList();
            _cargoEntities = cargoEntities.ToArray();
            _cargoEntityNames = cargoEntities.Select(GetEntityName).ToArray();
            _selectedCargoEntity = 0;

            // Minerals the faction knows about
            var mineralList = factionInfo.Data.CargoGoods.GetMineralsList().ToArray();
            _minerals = mineralList;
            _mineralNames = mineralList.Select(m => m.Name).ToArray();
            _selectedMineral = 0;
        }

        static string GetEntityName(Entity e)
        {
            if (e.TryGetDataBlob<NameDB>(out var nameDB))
                return nameDB.OwnersName;
            return $"Entity {e.Id}";
        }

        internal override void Display()
        {
            if (!IsActive || !_uiState.SMenabled || _uiState.PlayerFaction == null) return;

            if (Window.Begin("Dev Tools", ref IsActive, _flags))
            {
                if (ImGui.Button("Refresh Lists"))
                    HardRefresh();

                // ── Spawn Ship ────────────────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Spawn Ship ]");

                if (_shipDesignNames.Length == 0)
                {
                    ImGui.TextDisabled("No ship designs. Use Ship Design window first.");
                }
                else
                {
                    ImGui.Combo("Design##devship", ref _selectedDesign, _shipDesignNames, _shipDesignNames.Length);
                    if (_bodyNames.Length > 0)
                        ImGui.Combo("Orbit around##devspawnparent", ref _selectedSpawnParent, _bodyNames, _bodyNames.Length);
                    ImGui.InputText("Name##devshipname", _shipNameBuffer, (uint)_shipNameBuffer.Length);

                    if (_bodyEntities.Length > 0 && ImGui.Button("Spawn Ship##devspawnbtn"))
                    {
                        try
                        {
                            string rawName = Encoding.UTF8.GetString(_shipNameBuffer).TrimEnd('\0').Trim();
                            string? shipName = string.IsNullOrEmpty(rawName) ? null : rawName;
                            var design = _shipDesignValues[_selectedDesign];
                            var parent = _bodyEntities[_selectedSpawnParent];
                            ShipFactory.CreateShip(design, _uiState.PlayerFaction, parent, shipName);
                            _spawnStatus = $"Spawned {design.Name}";
                            Array.Clear(_shipNameBuffer, 0, _shipNameBuffer.Length);
                            HardRefresh();
                        }
                        catch (Exception ex)
                        {
                            _spawnStatus = $"Error: {ex.Message}";
                        }
                    }
                    if (!string.IsNullOrEmpty(_spawnStatus))
                        ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _spawnStatus);
                }

                // ── Create Colony ─────────────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Create Colony ]");

                if (_planetNames.Length == 0)
                {
                    ImGui.TextDisabled("No bodies found in this system.");
                }
                else
                {
                    ImGui.Combo("Planet##devcolplanet", ref _selectedPlanet, _planetNames, _planetNames.Length);
                    ImGui.InputInt("Population (millions)##devcolpop", ref _popMillions);
                    if (_popMillions < 0) _popMillions = 0;

                    if (ImGui.Button("Create Colony##devcolbtn"))
                    {
                        try
                        {
                            var factionInfo = _uiState.PlayerFaction.GetDataBlob<FactionInfoDB>();
                            if (factionInfo.Species.Count == 0)
                            {
                                _colonyStatus = "Error: faction has no species.";
                            }
                            else
                            {
                                var planet = _planetEntities[_selectedPlanet];
                                var species = factionInfo.Species[0];
                                long pop = (long)_popMillions * 1_000_000L;
                                ColonyFactory.CreateColony(_uiState.PlayerFaction, species, planet, pop);
                                _colonyStatus = $"Colony created on {GetEntityName(planet)}";
                                HardRefresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            _colonyStatus = $"Error: {ex.Message}";
                        }
                    }
                    if (!string.IsNullOrEmpty(_colonyStatus))
                        ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _colonyStatus);
                }

                // ── Add Minerals ──────────────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Add Minerals ]");

                if (_cargoEntityNames.Length == 0)
                {
                    ImGui.TextDisabled("No entities with cargo storage in this system.");
                }
                else if (_mineralNames.Length == 0)
                {
                    ImGui.TextDisabled("No minerals available to this faction yet.");
                }
                else
                {
                    ImGui.Combo("Target##devmintarget", ref _selectedCargoEntity, _cargoEntityNames, _cargoEntityNames.Length);
                    ImGui.Combo("Mineral##devmintype", ref _selectedMineral, _mineralNames, _mineralNames.Length);
                    ImGui.InputInt("Amount##devminamt", ref _mineralAmount);
                    if (_mineralAmount < 1) _mineralAmount = 1;

                    if (ImGui.Button("Add Minerals##devminbtn"))
                    {
                        try
                        {
                            var entity = _cargoEntities[_selectedCargoEntity];
                            var mineral = _minerals[_selectedMineral];
                            CargoTransferProcessor.AddCargoItems(entity, mineral, _mineralAmount);
                            _mineralStatus = $"Added {_mineralAmount:N0} {mineral.Name}";
                        }
                        catch (Exception ex)
                        {
                            _mineralStatus = $"Error: {ex.Message}";
                        }
                    }
                    if (!string.IsNullOrEmpty(_mineralStatus))
                        ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _mineralStatus);
                }
            }
            Window.End();
        }
    }
}
