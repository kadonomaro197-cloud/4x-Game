using System;
using System.Linq;
using System.Text;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Ships;
using Pulsar4X.Fleets;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Colonies;
using Pulsar4X.Storage;
using Pulsar4X.Industry;
using Pulsar4X.Galaxy; // MassVolumeDB lives here on this branch (namespace drifted from the branch this file was written on)
using Pulsar4X.Combat;
using Pulsar4X.Sensors;

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

        // ── Faction Switcher (SM) ──────────────────────
        private string[] _factionNames = Array.Empty<string>();
        private Entity[] _factionEntities = Array.Empty<Entity>();
        private int _selectedFactionView = 0;
        private string _factionStatus = "";

        // ── Combat Sandbox ─────────────────────────────
        private int _hostileCount = 3;
        private string _hostileStatus = "";

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
            {
                // Position gauge for the "ships teleport to the Sun" bug. sun-dist = the ship's distance from the
                // system origin (the Sun) in Gm (millions of km); Earth orbit ≈ 150 Gm. A ship reading ≈ 0 has
                // moved to the Sun in ENGINE state; if it reads ≈ 150 but still DRAWS at the Sun, it's render-only.
                // parent = what its position hangs off (Earth normally; null/INVALID = the anchor broke).
                string posStr = "no-PositionDB";
                if (sh.TryGetDataBlob<PositionDB>(out var p))
                {
                    var parent = p.Parent;
                    string parentStr = parent == null ? "ROOT/null" : (parent.IsValid ? GetEntityName(parent) : "INVALID");
                    posStr = $"sun-dist={p.AbsolutePosition.Length() / 1e9:0.##}Gm parent={parentStr}";
                }
                DevLog($"  ship  id={sh.Id} '{GetEntityName(sh)}' faction={sh.FactionOwnerID} {posStr}");
            }
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

        // Keeps the faction-switcher list current. Factions rarely change, so (like SyncShipDesigns) this only
        // rebuilds when the count changes, making it safe to call every frame from Display().
        void SyncFactions()
        {
            if (_uiState.Game == null) return;
            if (_factionEntities.Length == _uiState.Game.Factions.Count) return;

            _factionEntities = _uiState.Game.Factions.Values.ToArray();
            _factionNames = _factionEntities.Select(GetEntityName).ToArray();
            if (_selectedFactionView >= _factionNames.Length)
                _selectedFactionView = 0;
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

                // ── Faction Switcher (SM) ─────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Faction Switcher (SM) ]");
                ImGui.TextDisabled("View/act as any faction. Switch sides to watch a battle from either perspective;");
                ImGui.TextDisabled("the Fleet/System windows then show that faction's ships. 'Back to player' restores yours.");

                SyncFactions();

                string viewingName = _uiState.Faction != null ? GetEntityName(_uiState.Faction) : "(none)";
                string playerName = _uiState.PlayerFaction != null ? GetEntityName(_uiState.PlayerFaction) : "(none)";
                ImGui.Text($"Viewing: {viewingName}");
                ImGui.SameLine();
                ImGui.TextDisabled($"(your faction: {playerName})");

                if (_factionNames.Length == 0)
                {
                    ImGui.TextDisabled("No factions found.");
                }
                else
                {
                    ImGui.Combo("Faction##devfactionview", ref _selectedFactionView, _factionNames, _factionNames.Length);
                    if (ImGui.Button("View as##devfactionviewbtn"))
                    {
                        try
                        {
                            var faction = _factionEntities[_selectedFactionView];
                            _uiState.SetFaction(faction);
                            _factionStatus = $"Now viewing {GetEntityName(faction)} (id {faction.Id}).";
                            DevLog($"Faction view -> '{GetEntityName(faction)}' id={faction.Id}");
                        }
                        catch (Exception ex)
                        {
                            _factionStatus = $"Error: {ex.Message}";
                            DevLog($"Faction view FAILED: {ex}");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Back to player##devfactionback") && _uiState.PlayerFaction != null)
                    {
                        _uiState.SetFaction(_uiState.PlayerFaction);
                        _factionStatus = $"Back to your faction ({GetEntityName(_uiState.PlayerFaction)}).";
                        DevLog("Faction view -> player faction");
                    }
                    if (!string.IsNullOrEmpty(_factionStatus))
                        ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _factionStatus);
                }

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

                            // Spawn ready to fly: CreateShip leaves tanks empty (production-built ships aren't
                            // free-fuelled), so top them off here the way the start fleet is fuelled.
                            double fuelUnits = ShipFactory.FillFuelTanks(ship, _uiState.PlayerFaction.GetDataBlob<FactionInfoDB>());
                            // ...and CHARGE THE REACTOR. CreateShip also leaves stored energy at 0, and WARP is paid
                            // from stored electricity (not fuel) — an uncharged ship given a move order just sits
                            // there. This is the energy half of "ready to fly" (the start fleet gets it in
                            // DefaultStartFactory); without it a freshly-spawned ship can't warp until it slowly charges.
                            double energyKJ = ShipFactory.ChargeReactors(ship);

                            // Put the ship into one of the player's fleets so it appears in the Fleet window and
                            // can be ordered. A bare CreateShip parents the ship to the PLANET, not the faction's
                            // fleet tree — so it never shows in fleet view (this is why a spawned ship is missing
                            // there while the launch-queue courier, which IS under the faction, shows up). This
                            // goes through the ORDER system (FleetOrder.AssignShip → OrderHandler), the only
                            // fleet API the client may use — FleetDB's mutators are engine-internal (poking them
                            // is what broke the build earlier). Uses _uiState.PlayerFaction (the REAL player) so
                            // it works even while SM mode is viewing the Game Master faction.
                            string fleetNote = "but found no player fleet to put it in";
                            var playerFleet = parent.Manager.GetAllEntitiesWithDataBlob<FleetDB>()
                                .FirstOrDefault(f => f.FactionOwnerID == _uiState.PlayerFaction.Id);
                            if (playerFleet != null)
                            {
                                _uiState.Game.OrderHandler.HandleOrder(
                                    FleetOrder.AssignShip(_uiState.PlayerFaction.Id, playerFleet, ship));
                                fleetNote = $"added to fleet '{GetEntityName(playerFleet)}'";
                            }

                            // The ship orbits the planet at ~2x its radius — sub-pixel on the planet icon at
                            // system zoom, so zoom into the body to see it on the map.
                            int shipsInSystem = parent.Manager.GetAllEntitiesWithDataBlob<ShipInfoDB>().Count;
                            _spawnStatus = $"Spawned '{design.Name}' (id {ship.Id}) orbiting {GetEntityName(parent)}, {fleetNote}. "
                                + $"Exit SM mode + open the Fleet window to command it (zoom into {GetEntityName(parent)} to see it on the map).";
                            DevLog($"Spawn Ship OK: '{design.Name}' id={ship.Id} around '{GetEntityName(parent)}', {fleetNote}, fuel=+{fuelUnits:0} units, energy=+{energyKJ:0} KJ, shipsInSystem={shipsInSystem}");
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

                // ── Combat Sandbox (spawn an enemy to fight) ──────
                ImGui.Separator();
                ImGui.Text("[ Combat Sandbox ]");
                ImGui.TextDisabled("Stand up a HOSTILE fleet using the Design + 'Orbit around' body picked above,");
                ImGui.TextDisabled("so the battle trigger can engage your fleet. Put YOUR fleet at the same body.");

                if (_shipDesignValues.Length == 0 || _bodyEntities.Length == 0)
                {
                    ImGui.TextDisabled("Need a ship design and a body (see Spawn Ship above).");
                }
                else
                {
                    ImGui.SetNextItemWidth(120f);
                    ImGui.InputInt("Count##devhostilecount", ref _hostileCount);
                    if (_hostileCount < 1) _hostileCount = 1;

                    if (ImGui.Button("Spawn Hostile Fleet##devhostilespawn"))
                    {
                        try
                        {
                            var design = _shipDesignValues[_selectedDesign];
                            var body = _bodyEntities[_selectedSpawnParent];
                            // CombatSandbox builds a registered enemy faction + fleet + ships (owner-flipped) — the
                            // CI-proven engine helper, so this client call stays a thin wrapper.
                            var fleet = CombatSandbox.SpawnHostileFleet(
                                _uiState.Game, _uiState.SelectedSystem, _uiState.PlayerFaction,
                                design, _hostileCount, body, "Hostiles");
                            _hostileStatus = $"Spawned {_hostileCount}x '{design.Name}' as a HOSTILE fleet orbiting {GetEntityName(body)}. "
                                + "Put your fleet at the same body, then press play (or click 'Tick Combat') to fight.";
                            DevLog($"Spawn Hostile Fleet OK: {_hostileCount}x '{design.Name}' around '{GetEntityName(body)}', fleet id={fleet.Id}");
                        }
                        catch (Exception ex)
                        {
                            _hostileStatus = $"Error: {ex.Message}";
                            DevLog($"Spawn Hostile Fleet FAILED: {ex}");
                        }
                    }
                    ImGui.SameLine();
                    // Manual driver / diagnostic: force one combat tick over the current system. If pressing play
                    // doesn't auto-start the fight, clicking this drives it salvo by salvo (watch the Combat tab).
                    // Tick returns the fleet count it scanned — a quick check that both fleets are in this system.
                    if (ImGui.Button("Tick Combat (force a salvo)##devhostiletick"))
                    {
                        try
                        {
                            int seen = CombatEngagement.Tick(_uiState.SelectedSystem, 5);
                            _hostileStatus = $"Ticked combat: scanned {seen} fleet(s) in this system. Click again to drive the battle; watch the Combat tab on your fleet.";
                            DevLog($"Combat Tick: scanned {seen} fleet(s) in the current system");
                        }
                        catch (Exception ex)
                        {
                            _hostileStatus = $"Error: {ex.Message}";
                            DevLog($"Combat Tick FAILED: {ex}");
                        }
                    }
                    if (!string.IsNullOrEmpty(_hostileStatus))
                        ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), _hostileStatus);
                }

                // ── Detection / Fog of War ────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Detection / Fog of War ]");
                ImGui.TextDisabled("You only see foreign units your sensors DETECT: undetected enemy fleets are HIDDEN on the");
                ImGui.TextDisabled("map (the star + planets always show), and detected ones appear as limited-info contact blips");
                ImGui.TextDisabled("(name + last-known position, fading to a ghost when the track is lost). Combat is gated too:");
                ImGui.TextDisabled("fleets only engage hostiles they detect, the side that sees first shoots first. Off by default.");
                if (ImGui.Checkbox("Fog of War — hide undetected foreign units + detection-gated combat##devfow", ref CombatEngagement.RequireDetectionToEngage))
                    DevLog($"Fog of war (RequireDetectionToEngage) = {CombatEngagement.RequireDetectionToEngage}");

                // On-demand detection/EMCON snapshot to the log (the same thing the ~3 s heartbeat writes) — for
                // grabbing the picture at a precise moment. The heartbeat already logs it periodically.
                if (ImGui.Button("Dump Detection (log)##devdumpdetect"))
                {
                    try
                    {
                        SessionLog.DetectionSnapshot(_uiState.SelectedSystem, _uiState.PlayerFaction);
                        DevLog("Dumped detection/EMCON snapshot to the log.");
                    }
                    catch (Exception ex) { DevLog($"Dump Detection FAILED: {ex.Message}"); }
                }

                // Live signature readout for the clicked entity — watch it climb when a ship runs hot / thrusts /
                // fires (the EMCON activity model) and drop when it goes Silent. Defensive: tolerates no selection.
                var clickedEnt = _uiState.LastClickedEntity;
                if (clickedEnt?.Entity != null && clickedEnt.Entity.IsValid &&
                    clickedEnt.Entity.TryGetDataBlob<SensorProfileDB>(out var sigProfile))
                    ImGui.TextDisabled($"Selected #{clickedEnt.Entity.Id}: emitted signature x{sigProfile.ActivityMultiplier:0.##} (posture base x{sigProfile.SignatureBaseMultiplier:0.##})");
                else
                    ImGui.TextDisabled("Click a ship on the map to read its live emitted-signature multiplier.");

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
