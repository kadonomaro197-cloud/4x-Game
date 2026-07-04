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
using Pulsar4X.GroundCombat;

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
        // Each "Spawn Hostile Fleet" press stands up a NEW faction; rotate distinct names so repeated spawns are
        // genuinely DIFFERENT factions (not four things all called "Hostiles") — multi-faction combat / IFF material.
        private int _hostileSpawnIndex = 0;
        private static readonly string[] _hostileFactionNames =
            { "Crimson Fleet", "Iron Legion", "Black Sun Cartel", "Ashen Concord", "Void Reavers", "Kestrel Syndicate" };

        // ── Government (test regimes) ──────────────────
        private string _governmentStatus = "";

        // ── Age the galaxy (staged states) ─────────────
        private string _stageStatus = "";

        // ── Society levers (sustenance / manpower) — colony picker shared by both ──
        private string[] _colonyNames = Array.Empty<string>();
        private Entity[] _colonyEntities = Array.Empty<Entity>();
        private int _selectedColony = 0;
        private float _perCapitaPower = 1.0f;
        private float _perCapitaFood = 1.0f;
        private string _sustenanceStatus = "";
        private string _manpowerStatus = "";

        // ── Diplomacy levers (stance / treaties / war) ──
        private int _selectedDipFaction = 0;
        private string _diploStatus = "";

        // ── Raise Ground Unit (populate the tactical map to test it) ──
        private int _groundUnitType = 0;   // index into _groundTypeNames
        private int _groundCount = 3;
        private int _groundRegion = 0;
        private string _groundStatus = "";
        private static readonly string[] _groundTypeNames = { "Infantry", "Armor", "Artillery" };

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

            // Colonies in the current system (target for the sustenance / manpower levers)
            var colonyList = _uiState.SelectedSystem.GetAllEntitiesWithDataBlob<ColonyInfoDB>();
            _colonyEntities = colonyList.ToArray();
            _colonyNames = colonyList.Select(GetEntityName).ToArray();
            _selectedColony = 0;

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

        /// <summary>A throwaway ground-unit design for the DevTools raise buttons (there's no base-mod JSON template
        /// yet — deferred in slice 5a to keep the six-point registration off the New-Game path). Uniform stats; the
        /// terrain/triangle math differentiates the types. Stats snapshot onto the raised unit like a real build.</summary>
        static GroundUnitDesign MakeDevGroundDesign(GroundUnitType type) => new GroundUnitDesign
        {
            UniqueID = "dev-ground-" + type,
            Name = "Dev " + type,
            UnitType = type,
            Attack = 100,
            Defense = 10,
            HitPoints = 500,
            MovementSpeed = type == GroundUnitType.Armor ? 2.0 : (type == GroundUnitType.Artillery ? 1.2 : 1.0),   // V1 speed
            IndustryPointCosts = 0,
            IndustryTypeID = "installation",
        };

        /// <summary>Pick a faction to own "enemy" ground units: the first registered faction that isn't the player or
        /// the Game Master (the auto-spawn combat scenario provides rivals), else a synthetic sentinel id so the fight
        /// still has two sides. Keeps the map colours/labels honest (own = cyan, everything else = hostile red).</summary>
        int ResolveEnemyFactionId()
        {
            int playerId = _uiState.PlayerFaction?.Id ?? _uiState.Faction.Id;
            int gmId = _uiState.Game?.GameMasterFaction?.Id ?? int.MinValue;
            if (_uiState.Game != null)
            {
                foreach (var kv in _uiState.Game.Factions)
                    if (kv.Key != playerId && kv.Key != gmId) return kv.Key;
            }
            return -7777;   // synthetic "ground hostiles" — combat groups by faction id, so a distinct int is enough
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

        // The instrument panel for the M-ECON systems (morale / manpower / economy / government) — they have no
        // on-screen readout yet, so this dumps each colony's state (and the player's government) to the flushed
        // log so a play-test can WATCH the numbers move. The formatting lives in the engine (SocietyReadout, which
        // is CI-tested); this is a thin iterate-and-log wrapper.
        // Set the player faction's government dials to a preset regime (public setters; guarded). Logs the
        // classified name + description so the play-test sees which regime is active. The dials themselves are
        // read live by the engine processors (#30) — this is just the lever to move them off the Mid default.
        void SetGovernment(GovNotch authority, GovNotch economy, GovNotch openness, GovNotch militarism)
        {
            try
            {
                var faction = _uiState.PlayerFaction;
                if (faction == null || !faction.TryGetDataBlob<GovernmentDB>(out var gov))
                {
                    _governmentStatus = "No player faction / GovernmentDB to set.";
                    return;
                }
                gov.Authority = authority; gov.Economy = economy; gov.Openness = openness; gov.Militarism = militarism;
                _governmentStatus = $"Government -> {gov.Name()}";
                DevLog($"Government set -> {gov.Name()} ({gov.Description()})");
            }
            catch (Exception ex)
            {
                _governmentStatus = $"Government error: {ex.Message}";
            }
        }

        // Age the running game to a staged state (thin wrapper over the CI-tested engine GameStageFactory).
        // Logs the engine's summary of what it layered on so a play-test can see it in the flushed log.
        void AgeGalaxy(GameStage stage)
        {
            try
            {
                if (_uiState.Game == null || _uiState.PlayerFaction == null)
                {
                    _stageStatus = "Age galaxy: no game / player faction.";
                    return;
                }
                string summary = GameStageFactory.AgeTo(_uiState.Game, _uiState.PlayerFaction, stage);
                _stageStatus = summary;
                DevLog(summary);
                DevLog("  (Dump Society to read the new colonies / diplomacy / rebellion state.)");
            }
            catch (Exception ex)
            {
                _stageStatus = $"Age galaxy error: {ex.Message}";
            }
        }

        void DumpSociety()
        {
            var sys = _uiState.SelectedSystem;
            if (sys == null) { _spawnStatus = "Dump Society: no system selected."; return; }

            var colonies = sys.GetAllEntitiesWithDataBlob<ColonyInfoDB>();
            DevLog($"SOCIETY DUMP — {colonies.Count} colony(ies) in this system:");
            foreach (var c in colonies)
                DevLog("  " + SocietyReadout.Colony(c));
            DevLog("  government: " + SocietyReadout.Government(_uiState.PlayerFaction));
            DevLog("  " + SocietyReadout.Diplomacy(_uiState.PlayerFaction));

            _spawnStatus = $"Society: dumped {colonies.Count} colony(ies) to console_output.txt (close the game to read it).";
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
            if (_selectedDipFaction >= _factionNames.Length)
                _selectedDipFaction = 0;
        }

        // Keeps the colony picker (used by the sustenance + manpower levers) current. Colonies appear via DevTools
        // "Create Colony" (which calls HardRefresh) and "Age the galaxy" (which does not) — so, like SyncFactions,
        // rebuild whenever the count changes, making it safe to call every frame from Display().
        void SyncColonies()
        {
            if (_uiState.SelectedSystem == null) return;
            var cols = _uiState.SelectedSystem.GetAllEntitiesWithDataBlob<ColonyInfoDB>();
            if (cols.Count == _colonyEntities.Length) return;
            _colonyEntities = cols.ToArray();
            _colonyNames = cols.Select(GetEntityName).ToArray();
            if (_selectedColony >= _colonyNames.Length) _selectedColony = 0;
        }

        // C2 lever: switch the M5b power/food sustenance wiring ON for the selected colony by setting its per-capita
        // demand (it ships neutral — demand defaults to 0, so shortage is always 0). With demand set and no supply
        // modelled, the next SustenanceProcessor cycle computes a real shortage → a morale factor (and, severe, a
        // starvation death term). Thin caller over the CI-tested engine setter. Then Dump Society to read it.
        void ApplySustenanceDemand()
        {
            try
            {
                if (_colonyEntities.Length == 0) { _sustenanceStatus = "No colony selected."; return; }
                var colony = _colonyEntities[_selectedColony];
                if (!colony.TryGetDataBlob<ColonySustenanceDB>(out var sust))
                {
                    _sustenanceStatus = $"'{GetEntityName(colony)}' has no ColonySustenanceDB.";
                    return;
                }
                sust.SetDemand(_perCapitaPower, _perCapitaFood);
                _sustenanceStatus = $"Set demand on '{GetEntityName(colony)}': power {_perCapitaPower}, food {_perCapitaFood}/capita. Advance time, then Dump Society for the power/food factor.";
                DevLog($"Sustenance demand set on '{GetEntityName(colony)}': power={_perCapitaPower} food={_perCapitaFood} per-capita");
            }
            catch (Exception ex) { _sustenanceStatus = $"Error: {ex.Message}"; }
        }

        // C1 lever: drain the selected colony's bulk manpower pool to ~empty so the next crewed SHIP build hits the
        // crew gate (blocks under a consent regime; conscripts understaffed under Authority-High). Commits all
        // currently-available bulk (Available = workforce − committed). CommitBulk is a public engine method, so no
        // engine change was needed here. Then try to build a ship and watch it block / Dump Society to see the draw.
        void DrainManpower()
        {
            try
            {
                if (_colonyEntities.Length == 0) { _manpowerStatus = "No colony selected."; return; }
                var colony = _colonyEntities[_selectedColony];
                if (!colony.TryGetDataBlob<ColonyManpowerDB>(out var mp) ||
                    !colony.TryGetDataBlob<ColonyInfoDB>(out var info))
                {
                    _manpowerStatus = $"'{GetEntityName(colony)}' has no manpower pool.";
                    return;
                }
                long pop = info.Population.Values.Sum();
                long avail = mp.AvailableBulk(pop);
                mp.CommitBulk(avail);   // leaves AvailableBulk == 0
                _manpowerStatus = $"Drained '{GetEntityName(colony)}': committed {avail:N0} bulk manpower (available now ~0). A crewed ship build should block now.";
                DevLog($"Manpower drained on '{GetEntityName(colony)}': committed {avail:N0} bulk (pop {pop:N0})");
            }
            catch (Exception ex) { _manpowerStatus = $"Error: {ex.Message}"; }
        }

        // C6 / D4 levers: drive interactive diplomacy against the selected faction so the IFF/legitimacy/treaty
        // wiring is reachable without waiting for NPC drift. All are thin callers over CI-tested engine acts
        // (RelationshipState.AdjustScore, Diplomacy.DeclareWar/MakePeace, Treaties.Propose). MUTUAL where the engine
        // act isn't already symmetric (warm/cool nudge both ledgers) so Dump Society reflects it from the player side.
        Entity? SelectedDipFaction()
        {
            if (_selectedDipFaction < 0 || _selectedDipFaction >= _factionEntities.Length) return null;
            var f = _factionEntities[_selectedDipFaction];
            if (_uiState.PlayerFaction != null && f.Id == _uiState.PlayerFaction.Id) return null; // can't do diplomacy with yourself
            return f;
        }

        void WarmCoolRelation(int delta)
        {
            var other = SelectedDipFaction();
            if (other == null) { _diploStatus = "Pick a NON-player faction first."; return; }
            var player = _uiState.PlayerFaction;
            if (player == null || !player.TryGetDataBlob<DiplomacyDB>(out var pDip) || !other.TryGetDataBlob<DiplomacyDB>(out var oDip))
            { _diploStatus = "A faction is missing its DiplomacyDB."; return; }

            int nP = pDip.GetOrCreateRelationship(other.Id).AdjustScore(delta);
            oDip.GetOrCreateRelationship(player.Id).AdjustScore(delta);   // keep both views in step
            _diploStatus = $"{(delta >= 0 ? "Warmed" : "Cooled")} relations with {GetEntityName(other)} → your score {nP}.";
            DevLog($"Diplomacy: adjusted score with '{GetEntityName(other)}' by {delta} → {nP}");
        }

        void DoDeclareWar()
        {
            var other = SelectedDipFaction();
            if (other == null) { _diploStatus = "Pick a NON-player faction first."; return; }
            bool ok = Diplomacy.DeclareWar(_uiState.PlayerFaction, other, CasusBelli.ConfrontRival, _uiState.Game.TimePulse.GameGlobalDateTime);
            _diploStatus = ok ? $"Declared WAR on {GetEntityName(other)}. Advance a month → Dump Society: legitimacy shifts by your militarism."
                              : $"Declare war failed (already at war, or missing DiplomacyDB).";
            DevLog($"Diplomacy: DeclareWar on '{GetEntityName(other)}' -> {ok}");
        }

        void DoMakePeace()
        {
            var other = SelectedDipFaction();
            if (other == null) { _diploStatus = "Pick a NON-player faction first."; return; }
            bool ok = Diplomacy.MakePeace(_uiState.PlayerFaction, other, _uiState.Game.TimePulse.GameGlobalDateTime);
            _diploStatus = ok ? $"Made peace with {GetEntityName(other)}." : $"Make peace: no active war to end.";
            DevLog($"Diplomacy: MakePeace with '{GetEntityName(other)}' -> {ok}");
        }

        void DoProposeTreaty(TreatyType t)
        {
            var other = SelectedDipFaction();
            if (other == null) { _diploStatus = "Pick a NON-player faction first."; return; }
            bool ok = Treaties.Propose(_uiState.PlayerFaction, other, t, _uiState.Game.TimePulse.GameGlobalDateTime);
            _diploStatus = ok ? $"{t} SIGNED with {GetEntityName(other)}."
                              : $"{t} refused — warm relations first (needs a high enough score; no ordinary treaty mid-war).";
            DevLog($"Diplomacy: Propose {t} to '{GetEntityName(other)}' -> {ok}");
        }

        void SetFactionMilitarism(Entity? faction, GovNotch notch)
        {
            if (faction == null) { _diploStatus = "Pick a NON-player faction first."; return; }
            if (!faction.TryGetDataBlob<GovernmentDB>(out var gov))
            { _diploStatus = "Faction has no GovernmentDB."; return; }
            gov.Militarism = notch;
            _diploStatus = $"{GetEntityName(faction)} militarism → {notch} ({gov.Name()}). Advance months → its relations drift.";
            DevLog($"Diplomacy: set '{GetEntityName(faction)}' militarism -> {notch}");
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
                ImGui.SameLine();
                if (ImGui.Button("Dump Society (log)"))
                    DumpSociety();

                // ── Government (test regimes) ─────────────────────
                // Set the player faction's GovernmentDB to a preset regime so a play-test can watch the #30
                // dials bite (tax ceiling, crew policy, research speed, morale weight, war pride). No UI existed
                // to leave the neutral Mid default, so C3 was untestable; these three cover the extremes. Then
                // Dump Society to read the effects.
                ImGui.Separator();
                ImGui.Text("[ Government (test regimes) ]");
                ImGui.TextDisabled("Flip the player regime, then Dump Society / advance time to watch the dials bite.");
                if (ImGui.Button("Federal Republic (Mid — neutral)"))
                    SetGovernment(GovNotch.Mid, GovNotch.Mid, GovNotch.Mid, GovNotch.Mid);
                ImGui.SameLine();
                if (ImGui.Button("Totalitarian War-State"))
                    SetGovernment(GovNotch.High, GovNotch.High, GovNotch.Low, GovNotch.High);
                ImGui.SameLine();
                if (ImGui.Button("Liberal Democracy"))
                    SetGovernment(GovNotch.Low, GovNotch.Low, GovNotch.High, GovNotch.Low);
                if (_governmentStatus.Length > 0) ImGui.TextDisabled(_governmentStatus);

                // ── Age the galaxy (staged states) ────────────────
                // Jump the running game to a later stage so the late-triggering political cluster is visible
                // without playing for hours: Early = a frontier colony; Mid = met rivals + a treaty; Late = an
                // active war + a rebelling colony. Cumulative + convergent (safe to click through Early→Mid→Late).
                // Thin wrapper over the CI-tested engine GameStageFactory; then Dump Society to read the result.
                ImGui.Separator();
                ImGui.Text("[ Age the galaxy (staged states) ]");
                ImGui.TextDisabled("Layer the game up so diplomacy / war / rebellion are visible now. Then Dump Society.");
                if (ImGui.Button("→ Early (frontier colony)"))
                    AgeGalaxy(GameStage.Early);
                ImGui.SameLine();
                if (ImGui.Button("→ Mid (rivals + treaty)"))
                    AgeGalaxy(GameStage.Mid);
                ImGui.SameLine();
                if (ImGui.Button("→ Late (war + rebellion)"))
                    AgeGalaxy(GameStage.Late);
                if (_stageStatus.Length > 0) ImGui.TextDisabled(_stageStatus);

                // ── Society levers (sustenance / manpower) ────────
                // The M5b sustenance (#29) and M3 crew-gate (#27) wiring ship NEUTRAL/INERT (demand 0, billions of
                // start pop) so New Game is unchanged — which also means they're invisible on a short play-test.
                // These two levers switch them ON for one colony so C2/C1 are reachable: set per-capita power/food
                // demand (→ a shortage → a morale factor), and drain the crew pool (→ a ship build blocks). Then
                // Dump Society / try a build to see the effect.
                ImGui.Separator();
                ImGui.Text("[ Society levers (sustenance / manpower) ]");
                SyncColonies();
                if (_colonyNames.Length == 0)
                {
                    ImGui.TextDisabled("No colonies in this system yet (Create Colony below, or Age the galaxy).");
                }
                else
                {
                    ImGui.Combo("Colony##devsoccolony", ref _selectedColony, _colonyNames, _colonyNames.Length);

                    ImGui.TextDisabled("C2 — power/food demand per capita (0 = inert). Set > 0 with no supply to force a shortage.");
                    ImGui.SetNextItemWidth(140f);
                    ImGui.InputFloat("Power/cap##devsocpower", ref _perCapitaPower);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(140f);
                    ImGui.InputFloat("Food/cap##devsocfood", ref _perCapitaFood);
                    if (_perCapitaPower < 0f) _perCapitaPower = 0f;
                    if (_perCapitaFood < 0f) _perCapitaFood = 0f;
                    if (ImGui.Button("Apply sustenance demand##devsocapply"))
                        ApplySustenanceDemand();
                    if (_sustenanceStatus.Length > 0) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), _sustenanceStatus);

                    ImGui.TextDisabled("C1 — drain the crew pool so the next crewed ship build hits the manpower gate.");
                    if (ImGui.Button("Drain manpower pool##devsocdrain"))
                        DrainManpower();
                    if (_manpowerStatus.Length > 0) ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), _manpowerStatus);
                }

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

                // ── Diplomacy levers (stance / treaties / war) ────
                // Drive interactive diplomacy against another faction so the IFF/legitimacy/treaty wiring (C6/D4)
                // is reachable without waiting for NPC drift. Needs a second faction to exist (Age the galaxy →
                // Mid/Late, or Spawn Hostile Fleet). Warm/cool nudges the score, treaties are score-gated (warm
                // first), war flips the legitimacy militarism term. Militarism High/Low on the OTHER faction feeds
                // D3's reactive drift. Then Dump Society to read the ledger.
                ImGui.Separator();
                ImGui.Text("[ Diplomacy levers (stance / treaties / war) ]");
                if (_factionNames.Length < 2)
                {
                    ImGui.TextDisabled("Only one faction — Age the galaxy (Mid/Late) or Spawn a Hostile Fleet to get a rival.");
                }
                else
                {
                    ImGui.Combo("Faction##devdipfaction", ref _selectedDipFaction, _factionNames, _factionNames.Length);
                    ImGui.TextDisabled("Acts are between YOUR faction and the one picked above (pick a non-player faction).");

                    if (ImGui.Button("Warm +25##devdipwarm")) WarmCoolRelation(25);
                    ImGui.SameLine();
                    if (ImGui.Button("Cool -25##devdipcool")) WarmCoolRelation(-25);
                    ImGui.SameLine();
                    if (ImGui.Button("Declare War##devdipwar")) DoDeclareWar();
                    ImGui.SameLine();
                    if (ImGui.Button("Make Peace##devdippeace")) DoMakePeace();

                    if (ImGui.Button("Sign Non-Aggression##devdipnap")) DoProposeTreaty(TreatyType.NonAggression);
                    ImGui.SameLine();
                    if (ImGui.Button("Sign Trade##devdiptrade")) DoProposeTreaty(TreatyType.TradeAgreement);
                    ImGui.SameLine();
                    if (ImGui.Button("Sign Defensive Pact##devdippact")) DoProposeTreaty(TreatyType.DefensivePact);

                    ImGui.TextDisabled("Make the OTHER faction militarist/pacifist (drives D3 reactive drift):");
                    if (ImGui.Button("Their militarism → High##devdiphawk"))
                        SetFactionMilitarism(SelectedDipFaction(), GovNotch.High);
                    ImGui.SameLine();
                    if (ImGui.Button("→ Low##devdipdove"))
                        SetFactionMilitarism(SelectedDipFaction(), GovNotch.Low);

                    if (_diploStatus.Length > 0) ImGui.TextColored(new Vector4(0.6f, 0.9f, 1f, 1f), _diploStatus);
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
                            // A distinct faction name per press → each spawn is its own rival faction.
                            var factionName = _hostileFactionNames[_hostileSpawnIndex % _hostileFactionNames.Length];
                            _hostileSpawnIndex++;
                            // CombatSandbox builds a registered enemy faction + fleet + ships (owner-flipped) — the
                            // CI-proven engine helper, so this client call stays a thin wrapper.
                            var fleet = CombatSandbox.SpawnHostileFleet(
                                _uiState.Game, _uiState.SelectedSystem, _uiState.PlayerFaction,
                                design, _hostileCount, body, factionName);
                            _hostileStatus = $"Spawned {_hostileCount}x '{design.Name}' as the '{factionName}' fleet orbiting {GetEntityName(body)}. "
                                + "Put your fleet at the same body, then press play (or click 'Tick Combat') to fight.";
                            DevLog($"Spawn Hostile Fleet OK: {_hostileCount}x '{design.Name}' as '{factionName}' around '{GetEntityName(body)}', fleet id={fleet.Id}");
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

                // ── Raise Ground Unit (populate the tactical map) ──
                ImGui.Separator();
                ImGui.Text("[ Raise Ground Unit ]");
                ImGui.TextDisabled("Drop ground units onto a body's surface so the Planet View tactical map has something");
                ImGui.TextDisabled("to navigate. Raise YOURS + an ENEMY in the same region to test the fight + capture.");

                if (_bodyEntities.Length == 0)
                {
                    ImGui.TextDisabled("No body selected (see the 'Orbit around' picker under Spawn Ship).");
                }
                else
                {
                    var groundBody = _bodyEntities[_selectedSpawnParent];
                    bool hasRegions = groundBody.TryGetDataBlob<PlanetRegionsDB>(out var groundRegions) && groundRegions.Regions.Count > 0;
                    ImGui.Text($"Body: {GetEntityName(groundBody)}");
                    if (!hasRegions)
                    {
                        ImGui.TextDisabled("This body has no region layer (not a major body) — pick a planet/moon.");
                    }
                    else
                    {
                        int regionCount = groundRegions.Regions.Count;
                        ImGui.SetNextItemWidth(160f);
                        ImGui.Combo("Type##devgroundtype", ref _groundUnitType, _groundTypeNames, _groundTypeNames.Length);
                        ImGui.SameLine(); ImGui.SetNextItemWidth(90f);
                        ImGui.InputInt("Count##devgroundcount", ref _groundCount);
                        if (_groundCount < 1) _groundCount = 1;
                        ImGui.SameLine(); ImGui.SetNextItemWidth(90f);
                        ImGui.InputInt($"Region (0-{regionCount - 1})##devgroundregion", ref _groundRegion);
                        _groundRegion = Math.Max(0, Math.Min(_groundRegion, regionCount - 1));

                        var design = MakeDevGroundDesign((GroundUnitType)_groundUnitType);

                        if (ImGui.Button("Raise (your faction)##devgroundmine"))
                        {
                            try
                            {
                                int fac = _uiState.PlayerFaction?.Id ?? _uiState.Faction.Id;
                                for (int i = 0; i < _groundCount; i++)
                                    GroundForces.RaiseUnit(groundBody, design, fac, _groundRegion);
                                _groundStatus = $"Raised {_groundCount}x {design.UnitType} (YOURS) in region {_groundRegion} of {GetEntityName(groundBody)}. Open its Planet View.";
                                DevLog($"Raise Ground Unit OK: {_groundCount}x {design.UnitType} faction={fac} region={_groundRegion} body='{GetEntityName(groundBody)}'");
                            }
                            catch (Exception ex) { _groundStatus = $"Error: {ex.Message}"; DevLog($"Raise Ground Unit FAILED: {ex}"); }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Raise (enemy)##devgroundenemy"))
                        {
                            try
                            {
                                int enemyFac = ResolveEnemyFactionId();
                                for (int i = 0; i < _groundCount; i++)
                                    GroundForces.RaiseUnit(groundBody, design, enemyFac, _groundRegion);
                                _groundStatus = $"Raised {_groundCount}x {design.UnitType} (ENEMY faction {enemyFac}) in region {_groundRegion}. Put YOUR units in the same region, then press play to fight/capture.";
                                DevLog($"Raise Ground Unit (ENEMY) OK: {_groundCount}x {design.UnitType} faction={enemyFac} region={_groundRegion} body='{GetEntityName(groundBody)}'");
                            }
                            catch (Exception ex) { _groundStatus = $"Error: {ex.Message}"; DevLog($"Raise Ground Unit (enemy) FAILED: {ex}"); }
                        }
                        if (!string.IsNullOrEmpty(_groundStatus))
                            ImGui.TextColored(new Vector4(0.4f, 1f, 0.6f, 1f), _groundStatus);
                    }
                }

                // ── Battle Report ─────────────────────────────────
                ImGui.Separator();
                ImGui.Text("[ Battle Report ]");
                ImGui.TextDisabled("Space combat is auto-resolved math (no on-map fight, and a lopsided one is over in a blink).");
                ImGui.TextDisabled("This is the play-by-play that SURVIVES the fight, so you can review a battle you missed.");
                if (ImGui.Button("Open Battle Report##devbattlereport"))
                    BattleReportWindow.GetInstance().SetActive(true);

                // ── Closing Combat (the range/standoff model) ─────
                ImGui.Separator();
                ImGui.Text("[ Closing Combat ]");
                ImGui.TextDisabled("The closing-fight model: a weapon only fires if it REACHES the gap, and the gap closes toward");
                ImGui.TextDisabled("the faster side's range — so a fast long-range fleet kites, a fast brawler forces the merge.");
                ImGui.TextDisabled("Both OFF by default (the live game is unchanged). Turn them on to test. Watch the [Combat] log.");
                if (ImGui.Checkbox("Closing range — weapons gate on range + the gap closes##devclosing", ref CombatEngagement.EnableClosingRange))
                    DevLog($"Closing range (EnableClosingRange) = {CombatEngagement.EnableClosingRange}");
                if (ImGui.Checkbox("First-shot trigger — weapons-hold fleets sit in a standoff##devfirstshot", ref CombatEngagement.RequireWeaponsReleaseToEngage))
                    DevLog($"First-shot trigger (RequireWeaponsReleaseToEngage) = {CombatEngagement.RequireWeaponsReleaseToEngage}");

                // Auto-spawn-on-New-Game toggle (ALPHA): when on, every New Game/Quickstart builds this same
                // scenario automatically (no button press needed). Toggling here affects the NEXT new game, not
                // the current one.
                if (ImGui.Checkbox("Auto-spawn this scenario on New Game##devautoscenario", ref NewGameMenu.AutoSpawnCombatScenario))
                    DevLog($"Auto-spawn combat scenario on New Game = {NewGameMenu.AutoSpawnCombatScenario} (applies to the next New Game)");

                // Premade combat scenario: 2 well-rounded player task forces at Earth + hostile squadrons at
                // Luna/Venus/Mercury/Mars — for generating rich live combat/closing data in one click.
                if (ImGui.Button("Spawn Combat Scenario##devscenario"))
                {
                    try
                    {
                        var enemies = CombatSandbox.SpawnCombatScenario(_uiState.Game, _uiState.SelectedSystem, _uiState.PlayerFaction);
                        _hostileStatus = $"Spawned: 2 player task forces (Earth) + {enemies.Count} rival factions with capital-led squadrons at Luna/Venus/Mercury/Mars.";
                        DevLog($"Spawn Combat Scenario OK: {enemies.Count} hostile factions (ids {string.Join(", ", enemies.Select(e => e.Id))}); player fleets at Earth, enemies at Luna/Venus/Mercury/Mars");
                    }
                    catch (System.Exception ex)
                    {
                        _hostileStatus = "Spawn Combat Scenario FAILED — see log.";
                        DevLog($"Spawn Combat Scenario FAILED: {ex}");
                    }
                }

                // On-demand snapshot of every active engagement (gap / reach / reserve / posture / pool) to the log —
                // the "send me a picture of the fight" tool. Works regardless of the narration flag.
                if (ImGui.Button("Dump Combat (log)##devdumpcombat"))
                {
                    try
                    {
                        CombatEngagement.DumpActiveCombat(_uiState.SelectedSystem);
                        System.Console.Out.Flush();
                        DevLog("Dump Combat: wrote active-engagement snapshot to the log");
                    }
                    catch (System.Exception ex)
                    {
                        DevLog($"Dump Combat FAILED: {ex}");
                    }
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

                // All-ranges always-on toggle. On (default) draws reach rings for every own unit + place on the map.
                if (ImGui.Checkbox("Show all ranges — every own unit + place draws its reach rings##devallrings", ref _uiState.ShowAllRangeRings))
                    DevLog($"Show all range rings = {_uiState.ShowAllRangeRings}");
                ImGui.TextDisabled("Red = weapons reach · Green = sensor reach (how far you SEE) · Amber = detectability (how far you're SEEN).");
                ImGui.TextDisabled("A colony shows one green ring — its detection bubble (e.g. Earth's covers the inner system). Off = declutter.");

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
