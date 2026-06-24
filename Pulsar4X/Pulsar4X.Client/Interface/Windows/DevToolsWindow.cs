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
using Pulsar4X.Fleets;
using Pulsar4X.Colonies;
using Pulsar4X.Storage;
using Pulsar4X.Industry;
using Pulsar4X.Galaxy; // MassVolumeDB lives here on this branch (namespace drifted from the branch this file was written on)

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
        // Reused "Spawned Ships" fleet so DevTools-spawned ships land in a controllable fleet (show in Fleet window).
        private Entity? _devFleet;

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

            // Logged so the console capture shows whether a just-designed ship actually made it into the
            // spawn list (it reads factionInfo.ShipDesigns). Called on open / Refresh / system-change / after
            // a spawn -- never per frame -- so it does not spam.
            DevLog($"Refresh: {_shipDesignNames.Length} ship design(s), {_bodyNames.Length} body(ies), "
                + $"{_cargoEntityNames.Length} cargo target(s), {_mineralNames.Length} mineral(s)");
        }

        static string GetEntityName(Entity e)
        {
            if (e.TryGetDataBlob<NameDB>(out var nameDB))
                return nameDB.OwnersName;
            return $"Entity {e.Id}";
        }

        // Writes a DevTools diagnostic line and FLUSHES immediately. When launch.bat redirects the game's
        // output to console_output.txt, .NET buffers Console output and only flushes it when the process
        // EXITS — so a mid-session action (like a spawn) never showed up in the file until the game was
        // closed (this is why a spawn "produced no log"). Flushing here lands the line right away.
        static void DevLog(string msg)
        {
            Console.WriteLine("[DevTools] " + msg);
            Console.Out.Flush();
        }

        // Dumps the live ship/fleet counts for the current system to the (flushed) log and the on-screen status.
        // This is the gauge that answers "are there any ships/fleets at all?" — a fresh New Game builds a colony
        // but NO fleet, so it reads 0 fleets here. Spawned-but-fleetless ships show up as ships with no fleet.
        void DumpState()
        {
            var sys = _uiState.SelectedSystem;
            if (sys == null) { _spawnStatus = "Dump State: no system selected."; return; }

            var ships = sys.GetAllEntitiesWithDataBlob<ShipInfoDB>();
            var fleets = sys.GetAllEntitiesWithDataBlob<FleetDB>();
            int pf = _uiState.PlayerFaction?.Id ?? -999;

            DevLog($"STATE DUMP — playerFaction={pf}: {ships.Count} ship(s), {fleets.Count} fleet(s) in this system");
            foreach (var sh in ships)
                DevLog($"  ship  id={sh.Id} '{GetEntityName(sh)}' faction={sh.FactionOwnerID}");
            foreach (var fl in fleets)
                DevLog($"  fleet id={fl.Id} '{GetEntityName(fl)}' faction={fl.FactionOwnerID}");

            _spawnStatus = $"State: {ships.Count} ship(s), {fleets.Count} fleet(s) in this system (player faction {pf}). Full list in console_output.txt.";
        }

        // Keeps the Spawn Ship dropdown in step with the player's ship designs every frame, so a ship you just
        // made in the Ship Design window shows up here immediately instead of only after "Refresh Lists".
        // Deliberately lighter than HardRefresh(): it touches ONLY the ship-design arrays (not bodies/minerals)
        // and only rebuilds when the design count actually changes, so it is safe to call from Display().
        void SyncShipDesigns()
        {
            if (_uiState.PlayerFaction == null) return;
            var factionInfo = _uiState.PlayerFaction.GetDataBlob<FactionInfoDB>();
            if (factionInfo.ShipDesigns.Count == _shipDesignValues.Length) return;

            _shipDesignValues = factionInfo.ShipDesigns.Values.ToArray();
            _shipDesignNames = _shipDesignValues.Select(d => d.Name).ToArray();
            if (_selectedDesign >= _shipDesignNames.Length)
                _selectedDesign = 0;
        }

        internal override void Display()
        {
            if (!IsActive || !_uiState.SMenabled || _uiState.PlayerFaction == null) return;

            if (Window.Begin("Dev Tools", ref IsActive, _flags))
            {
                if (ImGui.Button("Refresh Lists"))
                    HardRefresh();
                ImGui.SameLine();
                if (ImGui.Button("Dump State (log)"))
                    DumpState();

                // ── Spawn Ship ────────────────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Spawn Ship ]");

                // Pick up any ship designed since this window was opened, WITHOUT needing "Refresh Lists"
                // (the 2026-06-22 "I designed a ship but it isn't in the spawn list" report). Cheap — only
                // rebuilds when the design count changes.
                SyncShipDesigns();

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
                            var ship = ShipFactory.CreateShip(design, _uiState.PlayerFaction, parent, shipName);

                            // Put the ship into a controllable FLEET. A bare CreateShip() leaves it in no fleet,
                            // so it never shows in the Fleet window and can't be ordered — which (one level up)
                            // is also why the would-be starting fleet is invisible. Reuse one "Spawned Ships"
                            // fleet per system for the player faction. (Same fleet API as ColonyFactory.)
                            if (_devFleet == null || !_devFleet.IsValid || _devFleet.Manager != parent.Manager)
                            {
                                _devFleet = FleetFactory.Create(parent.Manager, _uiState.PlayerFaction.Id, "Spawned Ships");
                                _devFleet.GetDataBlob<FleetDB>().SetParent(_uiState.PlayerFaction);
                            }
                            var devFleetDB = _devFleet.GetDataBlob<FleetDB>();
                            devFleetDB.AddChild(ship);
                            if (devFleetDB.FlagShipID < 0) devFleetDB.FlagShipID = ship.Id;

                            // CreateShip puts the ship in orbit at ~2x the planet's RADIUS, which at the
                            // zoomed-out system view is sub-pixel right on top of the planet icon — so it
                            // looks like nothing happened even though the spawn succeeded. Report the
                            // system ship count as proof it landed, and say where to look.
                            int shipsInSystem = parent.Manager.GetAllEntitiesWithDataBlob<ShipInfoDB>().Count;
                            _spawnStatus = $"Spawned '{design.Name}' (id {ship.Id}) into fleet 'Spawned Ships', orbiting {GetEntityName(parent)}. "
                                + $"{shipsInSystem} ship(s) in system — open the Fleet window to control it (or zoom into {GetEntityName(parent)} to see it).";
                            DevLog($"Spawn Ship OK: '{design.Name}' id={ship.Id} around '{GetEntityName(parent)}' into fleet 'Spawned Ships', shipsInSystem={shipsInSystem}");
                            Array.Clear(_shipNameBuffer, 0, _shipNameBuffer.Length);

                            // Deliberately NOT calling HardRefresh() here. It reset the Design dropdown to
                            // index 0, so a second click silently re-spawned the FIRST design — which is the
                            // "the previous name stayed" behaviour. Spawning a ship changes neither the design
                            // list nor the body list, so nothing in this window needs rebuilding.
                        }
                        catch (Exception ex)
                        {
                            _spawnStatus = $"Error: {ex.Message}";
                            DevLog($"Spawn Ship FAILED: {ex}");
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
                                Console.WriteLine($"[DevTools] Create Colony OK: on '{GetEntityName(planet)}' pop {pop:N0}");
                                HardRefresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            _colonyStatus = $"Error: {ex.Message}";
                            Console.WriteLine($"[DevTools] Create Colony FAILED: {ex}");
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
                            Console.WriteLine($"[DevTools] Add Minerals OK: {_mineralAmount:N0} {mineral.Name} to '{GetEntityName(entity)}'");
                        }
                        catch (Exception ex)
                        {
                            _mineralStatus = $"Error: {ex.Message}";
                            Console.WriteLine($"[DevTools] Add Minerals FAILED: {ex}");
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
