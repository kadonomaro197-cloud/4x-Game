using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using ImGuiNET;
using Pulsar4X.Blueprints;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Modding;
using Pulsar4X.Names;
using Pulsar4X.People;

namespace Pulsar4X.Client;

enum Page
{
    SelectMods,
    ConfigureGalaxy,
    SelectDetails
}

static class Helper
{
    public static byte[] ToByteArray(this string str)
    {
        return System.Text.Encoding.UTF8.GetBytes(str);
    }
}

public class NewGameMenu : PulsarGuiWindow
{
    private const int NAME_BUFFER_SIZE = 32;
    private const int SHORTNAME_BUFFER_SIZE = 5;
    private const string DEFAULT_NAME = "United Earth Corp";
    private const string DEFAULT_ABBREVIATION = "UEC";
    private const int MIN_STARTING_FUNDS = 1_000_000;
    private const int MAX_STARTING_FUNDS = 1_000_000_000;

    // BAREBONES VERIFICATION (2026-07-06, developer's call): a New Game is stripped to the CORE so a hands-on
    // play-through surfaces the real issues instead of being buried under auto-injected content. Both auto-spawns
    // now default OFF — no rival factions/fleets, no ground garrison; you get your faction + the Earth colony (with
    // its nested Freight/Military/Science fleets) and the rest of a fogged Sol. Re-enable per playtest in DevTools
    // ("Auto-spawn this scenario on New Game") or by flipping these back. See CreateGameCore.
    public static bool AutoSpawnCombatScenario = false;

    // BAREBONES: no default home garrison (see above). Flip true (or DevTools) to garrison the start colony.
    public static bool AutoRaiseHomeGarrison = false;

    // BAREBONES: build the start colony's nested fleets (Freight/Military/Science) on New Game. Default OFF — a truly
    // minimal start is your faction + the Earth colony and NO ships (build your own). Flip true (or DevTools) for the
    // start fleets. Passed to ColonyFactory.CreateFromBlueprint; tests keep fleets (that call defaults buildFleets=true).
    public static bool AutoBuildStartFleets = false;

    Page _currentPage = Page.SelectMods;
    ModLoader _modLoader = new ModLoader();
    ModDataStore _modDataStore = new ModDataStore();
    string _selectedSpeciesId = "";
    string _selectedThemeId = "";
    string _selectedSystemId = "";
    string _selectedBodyId = "";
    string _selectedColonyId = "";
    string _modsPageError = "";
    private bool _eleStart = true;

    List<string> _enabledSystems = new ();

    enum GameType { Nethost, Standalone }
    int _gameTypeButtonGrp = 0;
    GameType _selectedGameType = GameType.Standalone;
    byte[] _netPortInputBuffer = new byte[8];
    string _netPortString { get { return System.Text.Encoding.UTF8.GetString(_netPortInputBuffer); } }
    int _maxSystems = NewGameSettings.DEFAULT_NUM_SYSTEMS;
    int _startingFunds = 100_000_000;

    byte[] _corporationNameBuffer = Utils.BytesFromString(DEFAULT_NAME, NAME_BUFFER_SIZE);
    byte[] _corporationAbbreviationBuffer = Utils.BytesFromString(DEFAULT_ABBREVIATION, SHORTNAME_BUFFER_SIZE);
    byte[] _passInputBuffer = Utils.BytesFromString("", 16);

    byte[] _smPassInputbuffer = Utils.BytesFromString("", 16);

    int _masterSeed = 12345678;

    Vector2 _contentRegion = new Vector2();
    Vector2 _windowPos = new Vector2();
    Vector2 _windowSize = new Vector2();
    float _footerHeight = 0f;
    float _contentHeight = 0f;
    float _buttonWidth = 100f;
    private NewGameMenu()
    {
        _masterSeed = RandomNumberGenerator.GetInt32(999999999);
    }
    internal static NewGameMenu GetInstance()
    {
        if (!_uiState.LoadedWindows.ContainsKey(typeof(NewGameMenu)))
        {
            return new NewGameMenu();
        }
        return (NewGameMenu)_uiState.LoadedWindows[typeof(NewGameMenu)];
    }

    internal override void Display()
    {
        if(!IsActive) return;

        if (Window.Begin("New Game Setup", _flags | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse))
        {
            _contentRegion = ImGui.GetContentRegionAvail();
            // Get window dimensions
            _windowPos = ImGui.GetWindowPos();
            _windowSize = ImGui.GetContentRegionAvail();
            _footerHeight = ImGui.GetFrameHeightWithSpacing();

            // Calculate content area height (window height minus footer)
            _contentHeight = _windowSize.Y - _footerHeight;// - ImGui.GetFrameHeightWithSpacing();

            switch(_currentPage)
            {
                case Page.SelectMods:
                    DisplayModsPage();
                    break;
                case Page.ConfigureGalaxy:
                    DisplayConfigureGalaxy();
                    break;
                case Page.SelectDetails:
                    DisplayDetailsPage();
                    break;
            }
            Window.End();
        }
    }

    private void DisplayModsPage()
    {
        ImGui.BeginChild("ScrollingRegion", new Vector2(0, _contentHeight), ImGuiChildFlags.None);

        DisplayHelpers.Header("Select Mods to Enable");
        if(ImGui.BeginTable("ModsList", 4, Styles.TableFlags))
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader("Mod Name");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Version");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Hash");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Enable?");

            foreach(var modMetadata in ModsState.AvailableMods)
            {
                ImGui.TableNextColumn();
                ImGui.Text(modMetadata.Mod.ModName);
                ImGui.TableNextColumn();
                ImGui.Text(modMetadata.Mod.Version);
                ImGui.TableNextColumn();
                ImGui.Text(modMetadata.ManifestHash);
                var isEnabled = ModsState.IsModEnabled[modMetadata.Mod.ModName];
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("###" + modMetadata.Mod.ModName + "-checkbox", ref isEnabled))
                {
                    ModsState.IsModEnabled[modMetadata.Mod.ModName] = !ModsState.IsModEnabled[modMetadata.Mod.ModName];
                }
            }

            ImGui.EndTable();
        }

        if (!string.IsNullOrEmpty(_modsPageError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.35f, 0.35f, 1f));
            ImGui.TextWrapped(_modsPageError);
            ImGui.PopStyleColor();
        }

        // if (ImGui.RadioButton("Host Network Game", ref _gameTypeButtonGrp, 1))
        //     _selectedGameType = gameType.Nethost;
        // if (ImGui.RadioButton("Start Standalone Game", ref _gameTypeButtonGrp, 0))
        //     _selectedGameType = gameType.Standalone;
        // if (_selectedGameType == gameType.Nethost)
        //     ImGui.InputText("Network Port", _netPortInputBuffer, 8);

        ImGui.EndChild();
        ImGui.BeginChild("Footer", new Vector2(0, _footerHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        CancelButton();
        ImGui.SameLine();

        // Right-align the button by calculating its position
        float buttonX = _windowSize.X - _buttonWidth - ImGui.GetStyle().WindowPadding.X;
        ImGui.SetCursorPosX(buttonX);
        if (ImGui.Button("Next", new Vector2(_buttonWidth, 0)) || _uiState.debugnewgame)
        {
            _uiState.debugnewgame = false;
            LoadEnabledMods();

            // Guard the wizard defaults. If no mod is enabled (or the enabled mods provide no
            // species/theme/colony) the .First() calls below throw "Sequence contains no elements" and
            // crash the whole app -- LoadEnabledMods() returns early when nothing is enabled (see its
            // FIXME). Stay on this page and tell the player instead of crashing. This is the New-Game
            // equivalent of the guard QuickstartGame already has.
            if (!_modDataStore.Species.Any() || !_modDataStore.Themes.Any() || !_modDataStore.Colonies.Any())
            {
                _modsPageError = "Enable at least one mod that provides a species, a theme, and a starting "
                               + "colony before continuing (the base mod 'Pulsar4x' provides all three). "
                               + "Tick its Enable box above, then press Next.";
            }
            else
            {
                _modsPageError = "";
                _selectedSpeciesId = _modDataStore.Species.First().Key;
                _selectedThemeId = _modDataStore.Themes.First().Key;
                _selectedColonyId = _modDataStore.Colonies.First().Key;

                // Enable all the systems by default
                _enabledSystems.Clear();
                foreach(var (id, system) in _modDataStore.Systems)
                {
                    if(!_modDataStore.SystemBodies.Any(kvp => kvp.Value.CanStartHere && _modDataStore.Systems[id].Bodies.Contains(kvp.Key)))
                        continue;
                    _enabledSystems.Add(id);
                }
                _selectedSystemId = _enabledSystems.Any() ? _enabledSystems.First() : "";
                ResetSelectedBodyId();

                _currentPage = Page.ConfigureGalaxy;
            }
        }
        ImGui.EndChild();
    }

    private void DisplayConfigureGalaxy()
    {
        ImGui.BeginChild("ScrollingRegion", new Vector2(0, _contentHeight), ImGuiChildFlags.None);

        DisplayHelpers.Header("Select pre-configured Systems to include");

        if(ImGui.BeginTable("SystemsSelection", 2, Styles.TableFlags))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Include");
            ImGui.TableHeadersRow();

            foreach(var (id, system) in _modDataStore.Systems)
            {
                ImGui.TableNextColumn();
                ImGui.Text(system.Name);
                ImGui.TableNextColumn();
                bool enabled = _enabledSystems.Contains(id);
                if(ImGui.Checkbox("###" + id, ref enabled))
                {
                    if(!enabled)
                        _enabledSystems.Remove(id);
                    else
                        _enabledSystems.Add(id);
                }

            }
            ImGui.EndTable();
        }

        ImGui.EndChild();
        ImGui.BeginChild("Footer", new Vector2(0, _footerHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        CancelButton();
        ImGui.SameLine();

        if (ImGui.Button("Back", new Vector2(_buttonWidth, 0)))
        {
            _currentPage = Page.SelectMods;
        }
        ImGui.SameLine();
        // Right-align the button by calculating its position
        float buttonX = _windowSize.X - _buttonWidth - ImGui.GetStyle().WindowPadding.X;
        ImGui.SetCursorPosX(buttonX);
        if (ImGui.Button("Next", new Vector2(_buttonWidth, 0)))
        {
            _currentPage = Page.SelectDetails;
        }
        ImGui.EndChild();
    }

    private void DisplayDetailsPage()
    {
        ImGui.BeginChild("ScrollingRegion", new Vector2(0, _contentHeight), ImGuiChildFlags.None);

        DisplayHelpers.Header("CORPORTATION SETUP");
        ImGui.InputText("Corporation Name", _corporationNameBuffer, NAME_BUFFER_SIZE);
        ImGui.InputText("Corporation Abbreviation", _corporationAbbreviationBuffer, SHORTNAME_BUFFER_SIZE);

        var display = _modDataStore.Species.TryGetValue(_selectedSpeciesId, out var speciesBlueprint) ? speciesBlueprint.Name : "";
        if(ImGui.BeginCombo("Select Species", display))
        {
            foreach(var (id, species) in _modDataStore.Species)
            {
                if(!species.Playable) continue;

                if(ImGui.Selectable(species.Name, _selectedSpeciesId.Equals(id)))
                {
                    _selectedSpeciesId = id;
                }
            }
            ImGui.EndCombo();
        }

        display = _modDataStore.Themes.TryGetValue(_selectedThemeId, out var themeBlueprint) ? themeBlueprint.Name : "";
        if(ImGui.BeginCombo("Select Theme", display))
        {
            foreach(var (id, theme) in _modDataStore.Themes)
            {
                if(ImGui.Selectable(theme.Name, _selectedThemeId.Equals(id)))
                {
                    _selectedThemeId = id;
                }
            }
            ImGui.EndCombo();
        }

        display = _modDataStore.Colonies.TryGetValue(_selectedColonyId, out var colonyBlueprint) ? colonyBlueprint.Name : "";
        if(ImGui.BeginCombo("Starting Corporation Configuration", display))
        {
            foreach(var (id, colony) in _modDataStore.Colonies)
            {
                if(ImGui.Selectable(colony.Name, _selectedColonyId.Equals(id)))
                {
                    _selectedColonyId = id;
                }
            }
            ImGui.EndCombo();
        }

        display = _modDataStore.Systems.TryGetValue(_selectedSystemId, out var systemBlueprint) ? systemBlueprint.Name : _selectedSystemId.Equals("random") ? "Randomly Generated" : "";
        if(ImGui.BeginCombo("Select Starting System", display))
        {
            foreach(var id in _enabledSystems)
            {
                if(ImGui.Selectable(_modDataStore.Systems[id].Name, _selectedSystemId.Equals(id)))
                {
                    _selectedSystemId = id;
                    ResetSelectedBodyId();
                }
            }
            ImGui.Separator();
            if(ImGui.Selectable("Randomly Generated", _selectedSystemId.Equals("random")))
            {
                _selectedSystemId = "random";
            }
            ImGui.EndCombo();
        }

        if(!_selectedSystemId.Equals("random") && _selectedSystemId.IsNotNullOrEmpty())
        {
            display = _modDataStore.SystemBodies.TryGetValue(_selectedBodyId, out var bodyBlueprint) ? bodyBlueprint.Name : "";
            if(ImGui.BeginCombo("Select Starting Location", display))
            {
                foreach(var (id, body) in _modDataStore.SystemBodies.Where(kvp => _modDataStore.Systems[_selectedSystemId].Bodies.Contains(kvp.Key)))
                {
                    if(!body.CanStartHere) continue;
                    if(ImGui.Selectable(body.Name, _selectedBodyId.Equals(id)))
                    {
                        _selectedBodyId = id;
                    }
                }
                ImGui.EndCombo();
            }
        }

        int tempStartingFunds = _startingFunds;

        if (ImGui.SliderInt("Starting Funds", ref tempStartingFunds,
                            MIN_STARTING_FUNDS, MAX_STARTING_FUNDS,
                            tempStartingFunds.ToString("C0", CultureInfo.CurrentCulture),
                            ImGuiSliderFlags.ClampOnInput))
        {
            // Round to the nearest million when the value changes
            _startingFunds = (int)Math.Round(tempStartingFunds / 1000000.0) * 1000000;
        }

        ImGui.NewLine();
        DisplayHelpers.Header("GAME OPTIONS");

        ImGui.InputInt("Game Seed", ref _masterSeed);
        ImGui.InputInt("Galaxy Size", ref _maxSystems);
        if(ImGui.IsItemHovered())
        {
            DisplayHelpers.DescriptiveTooltip(
                "Galaxy Size",
                "",
                "How many playable star systems the galaxy will have.");
        }
        ImGui.Checkbox("Include ELE", ref _eleStart);
        if(ImGui.IsItemHovered())
        {
            DisplayHelpers.DescriptiveTooltip(
                "End of Life Event",
                "",
                "Adds an end of life event the player must endeavor to discover and prevent.");
        }

        ImGui.EndChild();
        ImGui.BeginChild("Footer", new Vector2(0, _footerHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        CancelButton();
        ImGui.SameLine();

        if (ImGui.Button("Back", new Vector2(_buttonWidth, 0)))
        {
            _currentPage = Page.ConfigureGalaxy;
        }
        ImGui.SameLine();
        // Right-align the button by calculating its position
        float buttonX = _windowSize.X - _buttonWidth - ImGui.GetStyle().WindowPadding.X;
        ImGui.SetCursorPosX(buttonX);
        if (ImGui.Button("Create Game!", new Vector2(_buttonWidth, 0)))
        {
            CreateNewGame();
        }
        ImGui.EndChild();
    }

    private void LoadEnabledMods()
    {
        List<string> enabledMods = new ();

        foreach(var modMetadata in ModsState.AvailableMods)
        {
            if(ModsState.IsModEnabled[modMetadata.Mod.ModName])
            {
                enabledMods.Add(modMetadata.Path);
            }
        }

        // FIXME: this is show some error in the UI if no mods are selected
        if(enabledMods.Count == 0)
            return;

        _modLoader.LoadedMods.Clear();
        _modDataStore = new ModDataStore();
        foreach (var mod in enabledMods)
        {
            _modLoader.LoadModManifest(mod, _modDataStore);
        }
    }

    void CreateNewGame()
    {
        // Mirror QuickstartGame's safety net. Without this, any exception thrown while building the game
        // (e.g. an empty or invalid selected id hitting a dictionary lookup in CreateGameCore) is UNHANDLED
        // and takes the whole application down with nothing written to game_log.txt and no clean shutdown —
        // which is exactly what a silent New Game crash looks like. Quickstart has this try/catch; New Game
        // did not. Catch it, log it to the console (same as Quickstart), and return to the menu instead of
        // crashing. The id dump makes an empty/invalid selection obvious in the console output.
        try
        {
            var p = new GameCreationParams
            {
                ModDataStore = _modDataStore,
                FactionName = Utils.StringFromBytes(_corporationNameBuffer),
                FactionAbbreviation = Utils.StringFromBytes(_corporationAbbreviationBuffer),
                SpeciesId = _selectedSpeciesId,
                ColonyId = _selectedColonyId,
                SystemId = _selectedSystemId,
                BodyId = _selectedBodyId,
                EnabledSystems = _enabledSystems,
                MaxSystems = _maxSystems,
                MasterSeed = _masterSeed,
                StartingFunds = _startingFunds,
                EleStart = _eleStart,
                SMPassword = Utils.StringFromBytes(_smPassInputbuffer),
                PlayerPassword = Utils.StringFromBytes(_passInputBuffer)
            };

            Console.WriteLine($"New Game: SpeciesId='{p.SpeciesId}' ColonyId='{p.ColonyId}' " +
                              $"SystemId='{p.SystemId}' BodyId='{p.BodyId}' EnabledSystems={p.EnabledSystems.Count}");

            var result = CreateGameCore(p);
            if (result == null)
            {
                Console.WriteLine("New Game Error: CreateGameCore returned null — a starting system, body, or "
                                  + "faction could not be resolved from the current selection.");
                return;
            }

            var (game, playerFaction, startingSystem, startingBody) = result.Value;
            ActivateGameUI(game, playerFaction, startingSystem, startingBody);
            IsActive = false;
            _currentPage = Page.SelectMods;
        }
        catch (Exception ex)
        {
            // Same handling Quickstart uses: log message + stack trace to the console and stay on the menu.
            Console.WriteLine($"New Game Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private struct GameCreationParams
    {
        public ModDataStore ModDataStore;
        public string FactionName;
        public string FactionAbbreviation;
        public string SpeciesId;
        public string ColonyId;
        public string SystemId;
        public string BodyId;
        public List<string> EnabledSystems;
        public int MaxSystems;
        public int MasterSeed;
        public int StartingFunds;
        public bool EleStart;
        public string SMPassword;
        public string PlayerPassword;
    }

    private static (Game game, Entity faction, StarSystem system, Entity body)? CreateGameCore(GameCreationParams p)
    {
        var gameSettings = new NewGameSettings
        {
            MaxSystems = Math.Min(p.MaxSystems, NewGameSettings.DEFAULT_MAX_SYSTEMS),
            SMPassword = p.SMPassword,
            CreatePlayerFaction = true,
            DefaultFactionName = p.FactionName,
            DefaultPlayerPassword = p.PlayerPassword,
            DefaultSolStart = true,
            MasterSeed = p.MasterSeed,
            EleStart = p.EleStart
        };

        Game game = GameFactory.CreateGame(p.ModDataStore, gameSettings);
        game.CreatedOnGitHash = AssemblyInfo.GetGitHash();
        game.LastSaveGitHash = AssemblyInfo.GetGitHash();

        // AI ON by default for a real game (developer's call, 2026-07-15). The NPC brain's action-arms — emit orders
        // (build / mass / move / invade), seek treaties, populate the intel ledger, spy back — ship gated OFF by default
        // so the ENGINE test suite stays byte-identical (a game built via a factory, not this menu, is unchanged). But a
        // GAME the player actually starts should have living opponents, so every menu-started game turns them on here,
        // exactly as the DevTest sandbox does. The flight recorder (B4) is always-on, so every AI action is taped
        // ([AI] lines in game_logs/ + the AI Inspector). Runtime behaviour is the PC gauge (CI can't run the client);
        // the acting-AI CI sensor (NPCActingSensorTests) proves the loop ACTS with these flags on, without the client.
        Pulsar4X.Factions.NPCDecisionProcessor.EnableOrderEmission = true;
        Pulsar4X.Factions.NPCDecisionProcessor.EnableDiplomaticProposals = true;
        Pulsar4X.Factions.NPCDecisionProcessor.EnableEspionageMirror = true;
        Pulsar4X.Factions.NPCDecisionProcessor.EnableIntelLedger = true;
        // Audit M1 (2026-07-22): activate the P3 legitimacy fixes in a live menu game. They ship gated OFF so the
        // ENGINE test suite stays byte-identical (a factory-built game never runs this menu); a game the player
        // actually starts should run the FRESH-morale legitimacy read (kills the stale-morale echo) and the rebellion
        // debounce (one bad sample can't trigger a revolt) — the A3 objective-flip fix, live. One line each to revert.
        Pulsar4X.Colonies.LegitimacyProcessor.ReadCurrentMorale = true;
        Pulsar4X.Colonies.LegitimacyProcessor.EnableRebellionDebounce = true;
        // Operation Earthfall — the GROUND invasion on-switch (PW). The ground tactical brain (puts battalions in
        // postures the ConquerResolver's infra-raze rung reads) and auto-form-up (loose landed/raised units become
        // commandable battalions) default OFF so the engine suite stays byte-identical; a real menu-started game turns
        // them on so an invasion actually plays out — exactly as EnableGroundTacticalAI's own doc says CORE does on the
        // menu path. One-line revert each if the mechanic should stay dormant. Runtime feel is the PC live-test.
        Pulsar4X.GroundCombat.GroundForcesProcessor.EnableGroundTacticalAI = true;
        Pulsar4X.GroundCombat.GroundAssembly.AutoFormUp = true;
        // W-track W3 — sub-formation role maneuver: a screen unit leads, a line unit holds at range, an artillery unit
        // kites to standoff, support stays back (the ground echo of space sub-fleet roles). OFF in the engine suite
        // (byte-identical), ON here so a menu game's closing ground fights show the role differentiation.
        Pulsar4X.GroundCombat.GroundForcesProcessor.EnableGroundRoleManeuver = true;

        // Generate random systems up to the number of "Galaxy Size" minus the
        // number of included pre-made systems
        int numberToGenerate = p.MaxSystems - p.EnabledSystems.Count;
        if (numberToGenerate > 0)
        {
            for (int i = 0; i < numberToGenerate; i++)
            {
                string systemName = NameFactory.GetSystemName(game);
                var seed = game.GlobalManager.RNG.Next();
                game.GalaxyGen.GenerateSystem(game, systemName, seed);
            }
        }

        // Load in the pre-made systems
        foreach (var id in p.EnabledSystems)
        {
            StarSystemFactory.LoadFromBlueprint(game, p.ModDataStore.Systems[id]);
        }

        StarSystem? startingSystem = null;
        Entity? startingBody = null;

        if (p.SystemId.Equals("random"))
        {
            // Pick a random system that has a terrestrial planet
            var candidates = new List<(StarSystem system, Entity body)>();
            foreach (var system in game.Systems)
            {
                foreach (var bodyInfo in system.GetAllDataBlobsOfType<SystemBodyInfoDB>())
                {
                    if (bodyInfo.BodyType == BodyType.Terrestrial && bodyInfo.OwningEntity != null)
                    {
                        candidates.Add((system, bodyInfo.OwningEntity));
                    }
                }
            }

            if (candidates.Count == 0) return null;

            var pick = candidates[RandomNumberGenerator.GetInt32(candidates.Count)];
            startingSystem = pick.system;
            startingBody = pick.body;
        }
        else
        {
            var startingBodyBlueprint = p.ModDataStore.SystemBodies[p.BodyId];

            foreach (var system in game.Systems)
            {
                if (system.ManagerID != p.SystemId) continue;

                startingSystem = system;
                foreach (var systemBody in system.GetAllDataBlobsOfType<SystemBodyInfoDB>())
                {
                    if (systemBody.OwningEntity?.GetDefaultName()?.Equals(startingBodyBlueprint.Name) == true)
                    {
                        startingBody = systemBody.OwningEntity;
                    }
                }
            }
        }

        if (startingSystem == null || startingBody == null) return null;

        // Create the player's faction
        var playerFaction = FactionFactory.CreateBasicFaction(
            game,
            p.FactionName,
            p.FactionAbbreviation,
            p.StartingFunds);

        if (playerFaction == null) return null;

        playerFaction.FactionOwnerID = playerFaction.Id;
        playerFaction.GetDataBlob<FactionInfoDB>().KnownSystems.Add(startingSystem.ID);

        var playerSpecies = SpeciesFactory.CreateFromBlueprint(startingSystem, p.ModDataStore.Species[p.SpeciesId]);
        playerSpecies.FactionOwnerID = playerFaction.Id;
        playerFaction.GetDataBlob<FactionInfoDB>().Species.Add(playerSpecies);

        // Setup the starting colony
        ColonyFactory.CreateFromBlueprint(game, playerFaction, playerSpecies, startingSystem, startingBody, p.ModDataStore.Colonies[p.ColonyId], AutoBuildStartFleets);
        if (p.EleStart && !p.SystemId.Equals("random"))
            AsteroidFactory.CreateAsteroid(startingSystem, startingBody, game.TimePulse.GameGlobalDateTime + TimeSpan.FromDays(365));

        // Create starting people
        var scientistDB = CommanderFactory.CreateScientist(game);
        var scientist = CommanderFactory.Create(startingSystem, playerFaction.Id, scientistDB);

        var adminDB = CommanderFactory.CreateAdmin(game);
        CommanderFactory.Create(startingSystem, playerFaction.Id, adminDB);

        if (scientist.TryGetDataBlob<BonusesDB>(out var bonusesDB))
        {
            bonusesDB.Bonuses.Add(new Bonus(
                "Research Points",
                0.1,
                BonusType.Perentage,
                BonusCategory.ResearchPoints,
                "tech-category-power-propulsion"
            ));
        }

        // ALPHA: stand up the premade combat scenario by default (see AutoSpawnCombatScenario above) — the four
        // rival factions + capital-led squadrons at Luna/Venus/Mercury/Mars, plus two player task forces at Earth.
        // The exact same engine call the DevTools "Spawn Combat Scenario" button makes, run here so a New Game
        // already has enemies to fight. Wrapped so a scenario-spawn hiccup can NEVER break New Game itself — a
        // failure just logs and you get the normal (enemy-less) start. Runs BEFORE PostNewGameInitialization so
        // the first sensor scan it schedules already sees the spawned ships. Enemies sit at other bodies (not
        // Earth), so nothing auto-engages on spawn — you close to fight.
        if (AutoSpawnCombatScenario)
        {
            try
            {
                var enemyFactions = CombatSandbox.SpawnCombatScenario(game, startingSystem, playerFaction);
                Console.WriteLine($"[NewGame] Auto-spawned combat scenario: {enemyFactions.Count} rival faction(s) at Luna/Venus/Mercury/Mars + 2 player task forces at Earth.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewGame] Auto-spawn combat scenario FAILED (New Game continues without it): {ex}");
            }
        }

        // Give the player's home colony (Earth) a starting ground garrison so the tactical Planet View isn't empty on
        // a fresh game — the ground echo of the auto-spawned space scenario. Engine call (CI-tested); wrapped so a
        // hiccup never breaks New Game.
        if (AutoRaiseHomeGarrison)
        {
            try
            {
                int garrison = Pulsar4X.GroundCombat.GroundStartGarrison.RaiseForFactionColonies(game, playerFaction);
                Console.WriteLine($"[NewGame] Raised home garrison: {garrison} ground unit(s) on the player's colony(ies).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewGame] Home garrison raise FAILED (New Game continues without it): {ex}");
            }
        }

        // Site Engine (SE-4e): flag-gated auto-seed of a live surface INCIDENT on the player's home world — the
        // switch that makes the whole SE-4 incident build (a menace holds a region, bleeds your units, spreads if
        // ignored) reachable in a real game. Default OFF, so New Game stays byte-identical; MaybeSpawnForNewGame is
        // a no-op unless the flag is on. Engine call (CI-tested); wrapped so a hiccup never breaks New Game.
        try
        {
            int incidents = Pulsar4X.Sites.IncidentScenario.MaybeSpawnForNewGame(game);
            if (incidents > 0)
                Console.WriteLine($"[NewGame] Auto-spawned {incidents} home-world incident(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewGame] Incident auto-spawn FAILED (New Game continues without it): {ex}");
        }

        // Site Engine (SE-6a): flag-gated auto-seed of a couple of workable DEMO sites (a space anomaly + a surface
        // ruin) at the player's home world — the switch that makes the whole Site Engine (SE-1..SE-5) reachable in a
        // real game. Default OFF, so New Game stays byte-identical. Engine call (CI-tested); wrapped so a hiccup never
        // breaks New Game.
        try
        {
            int sites = Pulsar4X.Sites.SiteScenario.MaybeSpawnForNewGame(game);
            if (sites > 0)
                Console.WriteLine($"[NewGame] Auto-spawned {sites} demo site(s) at the home world.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewGame] Demo-site auto-spawn FAILED (New Game continues without it): {ex}");
        }

        game.PostNewGameInitialization();

        return (game, playerFaction, startingSystem, startingBody);
    }

    private static void ActivateGameUI(Game game, Entity playerFaction, StarSystem startingSystem, Entity startingBody)
    {
        _uiState.ClearGameState();
        _uiState.Game = game;
        _uiState.SetFaction(playerFaction, true);
        _uiState.SetActiveSystem(startingSystem.ManagerID);
        _uiState.Camera.CenterOnEntity(startingBody);
        _uiState.Camera.ZoomLevel = 2_245_000f;

        DebugWindow.GetInstance().SetGameEvents();
        TimeControl.GetInstance().SetActive();
        ToolBarWindow.GetInstance().SetActive();
        Selector.GetInstance().SetActive();
        EntityFilterBar.GetInstance().SetActive();
        EventTickerWindow.GetInstance().SetActive();   // the Event Logger strip (top-centre)
    }

    private void ResetSelectedBodyId()
    {
        if(_modDataStore.Systems.TryGetValue(_selectedSystemId, out var systemBlueprint))
        {
            var candidates = _modDataStore.SystemBodies.Where(kvp => kvp.Value.CanStartHere && systemBlueprint.Bodies.Contains(kvp.Key));
            _selectedBodyId = candidates.Any() ? candidates.First().Key : "";
        }
        else
        {
            _selectedBodyId = "";
        }
    }

    private void CancelButton()
    {
        if(ImGui.Button("Cancel", new Vector2(_buttonWidth, 0)))
        {
            IsActive = false;
            MainMenuItems.GetInstance().SetActive(true);
        }
    }

    /// <summary>
    /// Creates a new game instantly with default settings, bypassing the wizard
    /// </summary>
    public static void QuickstartGame()
    {
        try
        {
            // Initialize mod loader and data store
            ModLoader modLoader = new ModLoader();
            ModDataStore modDataStore = new ModDataStore();

            // Quickstart is the "just start with the sane defaults" path, so load ONLY the mods whose manifest marks
            // them DefaultEnabled (the base mod) — never a test-only stub, and independent of whatever the New-Game
            // mod page happened to toggle in this session. The Pulsar4x-Testing mod ships incomplete data (a few
            // themes + one armor, no components) and breaks colony build / leaves nothing buildable if it loads, so a
            // Quickstart must never pick it up. The full New Game wizard still honours the player's explicit mod choices.
            foreach (var modMetadata in ModsState.AvailableMods)
            {
                if (modMetadata.Mod.DefaultEnabled)
                {
                    modLoader.LoadModManifest(modMetadata.Path, modDataStore);
                }
            }

            if (!modDataStore.Species.Any(kvp => kvp.Value.Playable))
            {
                Console.WriteLine("Quickstart Error: No playable species found in loaded mods");
                return;
            }

            if (!modDataStore.Colonies.Any())
            {
                Console.WriteLine("Quickstart Error: No colonies found in loaded mods");
                return;
            }

            // Select defaults
            string selectedSpeciesId = modDataStore.Species.First(kvp => kvp.Value.Playable).Key;
            string selectedColonyId = modDataStore.Colonies.First().Key;

            // Find all systems with CanStartHere bodies
            List<string> enabledSystems = new();
            foreach (var (id, system) in modDataStore.Systems)
            {
                if (modDataStore.SystemBodies.Any(kvp =>
                    kvp.Value.CanStartHere && system.Bodies.Contains(kvp.Key)))
                {
                    enabledSystems.Add(id);
                }
            }

            if (enabledSystems.Count == 0)
            {
                Console.WriteLine("Quickstart Error: No compatible starting systems found");
                return;
            }

            string selectedSystemId = enabledSystems.First();
            var selectedSystemBlueprint = modDataStore.Systems[selectedSystemId];
            string selectedBodyId = modDataStore.SystemBodies
                .Where(kvp => kvp.Value.CanStartHere && selectedSystemBlueprint.Bodies.Contains(kvp.Key))
                .First().Key;

            var p = new GameCreationParams
            {
                ModDataStore = modDataStore,
                FactionName = DEFAULT_NAME,
                FactionAbbreviation = DEFAULT_ABBREVIATION,
                SpeciesId = selectedSpeciesId,
                ColonyId = selectedColonyId,
                SystemId = selectedSystemId,
                BodyId = selectedBodyId,
                EnabledSystems = enabledSystems,
                MaxSystems = NewGameSettings.DEFAULT_NUM_SYSTEMS,
                MasterSeed = RandomNumberGenerator.GetInt32(999999999),
                StartingFunds = 100_000_000,
                EleStart = true,
                SMPassword = "",
                PlayerPassword = ""
            };

            var result = CreateGameCore(p);
            if (result == null)
            {
                Console.WriteLine("Quickstart Error: Could not create game");
                return;
            }

            var (game, playerFaction, startingSystem, startingBody) = result.Value;
            ActivateGameUI(game, playerFaction, startingSystem, startingBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Quickstart Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// The DevTest game-start — the "DevTest" button that replaces Quickstart. Instead of the wizard's single
    /// pre-provisioned player faction, it stands up the whole data-driven CONQUEST SANDBOX from JSON: the player
    /// (UEF) with "everything enabled" to design/build but NOTHING pre-built, plus two developed NPC factions —
    /// the United Martian Federation (inner-system war economy, at war with Earth) and the Kithrin Collective
    /// (an outer-system developed outpost station). Only Sol, and only Earth surveyed for the player.
    ///
    /// It reuses the SAME engine orchestrator the CI test drives (<see cref="DevTestStartFactory.CreateDevTest"/>),
    /// so what CI proves green is exactly what boots here. Wrapped end-to-end so a data hiccup logs and returns to
    /// the menu rather than killing the client (the Quickstart discipline).
    ///
    /// The AI plays for REAL here (B5): after the world loads, the NPC brain's ACTION gates (order emission /
    /// diplomatic proposals / espionage / intel ledger) are flipped ON — so the factions build, mass, move, invade,
    /// and spy. That's paired with the always-on AI flight recorder (B4): every decision is taped as <c>[AI]</c> lines
    /// in <c>game_logs/</c> and shown live in the AI Inspector window, so when the AI acts you SEE what it did and why
    /// (the developer's first requirement). The engine gates default OFF everywhere else, so only DevTest turns them on.
    /// </summary>
    public static void DevTestGame()
    {
        try
        {
            // Load ONLY the DefaultEnabled mods (the base mod) — same sane-defaults rule as Quickstart.
            ModLoader modLoader = new ModLoader();
            ModDataStore modDataStore = new ModDataStore();
            string? baseModDir = null;
            foreach (var modMetadata in ModsState.AvailableMods)
            {
                if (modMetadata.Mod.DefaultEnabled)
                {
                    modLoader.LoadModManifest(modMetadata.Path, modDataStore);
                    baseModDir ??= System.IO.Path.GetDirectoryName(modMetadata.Path);
                }
            }

            if (baseModDir == null)
            {
                Console.WriteLine("DevTest Error: no DefaultEnabled mod found to load the scenario from.");
                return;
            }

            // The scenario faction files live beside the base mod manifest (GameData/basemod/ScenarioFiles),
            // copied to the runtime Mods folder with the rest of the mod.
            string scenarioDir = System.IO.Path.Combine(baseModDir, "ScenarioFiles");

            // Build the game with NO auto-created player faction — DevTest authors all three factions from JSON.
            var gameSettings = new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,
                DefaultSolStart = true,
                MasterSeed = RandomNumberGenerator.GetInt32(999999999),
                EleStart = true,
                SMPassword = "",
                DefaultPlayerPassword = ""
            };
            Game game = GameFactory.CreateGame(modDataStore, gameSettings);
            game.CreatedOnGitHash = AssemblyInfo.GetGitHash();
            game.LastSaveGitHash = AssemblyInfo.GetGitHash();

            var (playerFaction, startingSystemId) = DevTestStartFactory.CreateDevTest(
                game, scenarioDir,
                new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            if (playerFaction == null)
            {
                Console.WriteLine("DevTest Error: CreateDevTest returned no player faction.");
                return;
            }

            // B5 — turn the AI LOOSE. "Everything enabled" means the NPC brain actually ACTS: emit orders (build /
            // mass / move / invade), seek treaties, and spy back. These engine gates default OFF (byte-identical) for
            // a normal game; DevTest is the sandbox where we watch the AI play for real. The flight recorder (B4) is
            // always-on, so every one of these actions is taped ([AI] lines in game_logs/ + the AI Inspector window)
            // — the whole point: turn it on, then watch WHAT it did and WHY. Runtime behaviour is what this PC test
            // verifies (CI can't run the client), which is exactly why the recorder ships first.
            Pulsar4X.Factions.NPCDecisionProcessor.EnableOrderEmission = true;
            Pulsar4X.Factions.NPCDecisionProcessor.EnableDiplomaticProposals = true;
            Pulsar4X.Factions.NPCDecisionProcessor.EnableEspionageMirror = true;
            Pulsar4X.Factions.NPCDecisionProcessor.EnableIntelLedger = true;
            // Audit M1 (2026-07-22): activate the P3 legitimacy fixes (fresh-morale read + rebellion debounce) in a
            // menu game, same as CreateGameCore. Default OFF (engine byte-identical); one line each to revert.
            Pulsar4X.Colonies.LegitimacyProcessor.ReadCurrentMorale = true;
            Pulsar4X.Colonies.LegitimacyProcessor.EnableRebellionDebounce = true;
            // Operation Earthfall — the GROUND invasion on-switch (same as CreateGameCore): the ground tactical brain +
            // auto-form-up, default OFF (engine byte-identical), ON for a DevTest sandbox so the invasion plays out.
            Pulsar4X.GroundCombat.GroundForcesProcessor.EnableGroundTacticalAI = true;
            Pulsar4X.GroundCombat.GroundAssembly.AutoFormUp = true;
            Pulsar4X.GroundCombat.GroundForcesProcessor.EnableGroundRoleManeuver = true;   // W3 role-based maneuver

            var startingSystem = game.Systems.Find(s => s.ID.Equals(startingSystemId));
            if (startingSystem == null)
            {
                Console.WriteLine("DevTest Error: the starting system did not load.");
                return;
            }

            // The player's homeworld is Earth (the only surveyed body) — centre the camera there.
            Entity? startingBody = null;
            foreach (var bodyInfo in startingSystem.GetAllDataBlobsOfType<SystemBodyInfoDB>())
            {
                if (bodyInfo.OwningEntity?.GetDefaultName()?.Equals("Earth") == true)
                {
                    startingBody = bodyInfo.OwningEntity;
                    break;
                }
            }
            if (startingBody == null)
            {
                Console.WriteLine("DevTest Error: Earth (the starting body) was not found in Sol.");
                return;
            }

            game.PostNewGameInitialization();
            ActivateGameUI(game, playerFaction, startingSystem, startingBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DevTest Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}