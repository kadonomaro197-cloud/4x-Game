#if TRACE
#define DEBUG
#endif

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;
using ImGuiNET;
using SDL3;
using Microsoft.Extensions.Configuration;
using Pulsar4X.Client.Interface.Themes;
using Pulsar4X.Fleets;
using Pulsar4X.Combat;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pulsar4X.Client
{
    public enum MouseButtons
    {
        Primary,
        Alt,
        Middle
    }

    public class PulsarMainWindow : SDL3Window
    {
        public const string PreferencesFile = "preferences.ini";
        public const string UserOrbitSettingsFile = "orbit-settings.json";
        public const string SavesPath = "Saves";
        public static string ModsPath = "Mods";
        public static string ResourcesPath = "Resources";
        private readonly GlobalUIState _state;
        private ITheme _theme;

        float mouseX;
        float mouseY;

        int _debugSDLFontHeight;

        ulong _fpsFrames = 0;
        ulong _fpsLastMeasurementTime = 0;
        float _fpsLastMeasurement = 0;

        // Session-recorder heartbeat throttle. PostFrameUpdate runs every frame; we only want a STATE snapshot
        // every ~3 s. Measured in SDL wall-clock ticks (ms) so the cadence is steady regardless of game speed.
        ulong _lastHeartbeatTick = 0;

        // Combat-interrupt banner: when the engine auto-pauses time because a battle began, flash an on-screen
        // notice until this SDL tick (wall-clock ms). 0 = not showing.
        ulong _combatBannerUntilTick = 0;
        ulong _lastFrameMsTick = 0;       // [PERF] slow-frame gauge: SDL tick of the previous frame
        ulong _lastSlowFrameLogTick = 0;  // throttle so a sustained slowdown doesn't flood the log
        // [PERF] per-stage frame breakdown — filled each frame, logged on a slow frame so the next play-test names
        // WHICH stage is heavy (state-update/transforms vs map SDL-draw vs ImGui windows), not just "a slow frame".
        ulong _perfUpdateMs = 0;   // _state.Update() — window state + the map's per-frame transform loops
        ulong _perfMapDrawMs = 0;  // GalacticMap.Draw() — the SDL line rasterisation (orbits/rings/icons)
        ulong _perfUiMs = 0;       // RenderUI() — name icons + every ImGui window's Display()

        // Render-loop crash visibility: a window's Display() or the map draw can throw (e.g. inspecting a
        // foreign/NPC entity whose faction has locked/empty data). The SDL Run loop has no try/catch, so an
        // unhandled exception kills the process and its trace goes to stderr — invisible in game_log.txt,
        // which captures stdout only. SafeRender catches per-piece, logs the full exception ONCE to the
        // captured log, and continues. Set keyed by error signature so a per-frame fault logs once.
        private readonly HashSet<string> _loggedRenderErrors = new ();

        public PulsarMainWindow(string[] args)
            : base(AppName)
        {
            _state = new GlobalUIState(this);
            _state.GalacticMap = new GalacticMapRender(this, _state);

            // Narrate live battles to game_log.txt ([Combat] lines) so a fight is visible in the log, not just the
            // Fleet Combat tab. Off by default in the engine (keeps the timed battle tests fast); on for the game.
            Pulsar4X.Combat.CombatEngagement.NarrateToLog = true;

            // Aurora-style combat interrupt: stop the clock the instant a new battle begins, so a fight doesn't
            // resolve invisibly inside one hour-step/play-run. Off by default in the engine (deterministic tests);
            // on for the game. The auto-pause is surfaced by the banner + SessionLog line in Render/PostFrameUpdate.
            Pulsar4X.Combat.CombatEngagement.InterruptTimeOnNewEngagement = true;

            // The GROUND twin (4b): stop the clock the instant a NEW planetary battle begins, so an invasion/ground
            // fight doesn't resolve invisibly on fast-forward — the same auto-pause + CombatInterruptPending banner
            // as space, now for the surface war. Off by default in the engine (deterministic tests); on for the game.
            Pulsar4X.GroundCombat.GroundForcesProcessor.InterruptTimeOnNewBattle = true;

            // Narrate the warp lifecycle ([WARP] departure → arrival) so a ship's journey is legible in the log —
            // and a warp that departs but never arrives stands out right next to a ⚠ TELEPORT flag (the open
            // warp-detach bug). Off by default in the engine (tests stay quiet); on for the game.
            Pulsar4X.Movement.WarpMoveProcessor.NarrateWarpToLog = true;

            // Authentic closing fight: fleets fight at a real range — every weapon trades fire as they close, getting
            // more accurate the nearer they get (range degrades ballistic accuracy; beams/missiles/flak gate on their
            // ranges). Off by default in the engine (existing combat tests are byte-identical at separation 0); ON for
            // the game so battles are authentic (the developer's call). DevTools can still toggle it.
            Pulsar4X.Combat.CombatEngagement.EnableClosingRange = true;

            // Fog of war: visibility AND combat are gated by what your sensors actually detect — an undetected enemy
            // doesn't show on the map and can't pull you into a battle (the side that sees first shoots first). This is
            // the realistic detection-range behaviour (a contact at Venus only appears once you're in sensor reach of
            // it), the developer's deliberate call on 2026-06-27. Off by default in the engine (combat tests don't
            // stand up sensors); ON for the game. DevTools → Detection / Fog of War can still toggle it live.
            Pulsar4X.Combat.CombatEngagement.RequireDetectionToEngage = true;

            // Battle-start rule (the developer's, 2026-07-02): a fight auto-starts only when the fleets are within
            // actual WEAPON range AND at least one side is Weapons Free (fire-at-will). Seeing each other is NOT
            // enough — two fleets can sit in sensor range across the system and never fire; the player closes them
            // (navigation) or issues an explicit Attack order. WeaponRange replaces the coarse 1 Gm proximity trigger;
            // WeaponsRelease enforces the fire-at-will half (default posture is Weapons Free, so by default they DO
            // fight once in range). Off by default in the engine (fixtures are co-located / posture-free); ON here.
            Pulsar4X.Combat.CombatEngagement.RequireWeaponRangeToEngage = true;
            Pulsar4X.Combat.CombatEngagement.RequireWeaponsReleaseToEngage = true;

            // Fire control → tracking (Sensors ⚙3): a ship's beam-fire-control director now actually improves how well
            // its beams track an evasive target (the dead BeamFireControlAtbDB.TrackingSpeed knob, wired). Off by
            // default in the engine (the fire-control component already lives on the test ships with a non-neutral
            // value, so wiring it changes those fixtures — gated like the closing/detection flags); ON for the game.
            Pulsar4X.Combat.ShipCombatValueDB.EnableFireControlTracking = true;

            // Fire control → PD-only mode (Sensors ⚙3): a ship with a FinalFireOnly (CIWS) director routes its beams
            // into point-defense (missile interception) instead of anti-ship fire (the dead BeamFireControlAtbDB.
            // FinalFireOnly knob, wired). Off by default in the engine (byte-identical — no base ship has such a
            // director, but gated to match its sibling flags); ON for the game.
            Pulsar4X.Combat.ShipCombatValueDB.EnableFinalFireOnlyPD = true;

            // EW barrage jamming (Sensors ⚙3 ▸ EW): a jammer floods the band so hostile sensors detect at shorter range
            // (and lights the jammer itself up as a beacon). Byte-identical until a jammer exists; ON for the game.
            Pulsar4X.Sensors.JammerAtb.EnableJamming = true;

            // Reactor-load heat (Sensors ⚙3 / Power ⚙4): a ship's running reactor now adds to its emitted signature,
            // so a hot power plant lights up an otherwise-dark hull. Byte-identical off; ON for the game.
            Pulsar4X.Sensors.EmconActivityProcessor.EnableReactorHeat = true;

            try
            {
                string? appDataDirectory = GetAppDataPath();

                if(string.IsNullOrEmpty(appDataDirectory)) throw new NullReferenceException("App data directory cannot be null");

                // Set the deafault mods path
                ModsPath = Path.Combine(appDataDirectory, ModsPath);

                // Set the default resources path
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var exeDiretory = Path.GetDirectoryName(exePath);

                    if(string.IsNullOrEmpty(exeDiretory)) throw new NullReferenceException("exe path cannot be null");

                    ResourcesPath = Path.Combine(exeDiretory, ResourcesPath);
                }

                // Parse optional command line arguments
                ParseCommandLineArguments(args);

                // Create directories we need if they don't exist
                TryCreateDirectory(appDataDirectory, SavesPath);
                TryCreateDirectory(appDataDirectory, ModsPath);

                // Make sure the base game mod is copied over to the mod directory
                // string sourceData = "Data";
                // string modsDirectory = Path.Combine(appDataDirectory, ModsPath);
                // DeleteThenCopyToDirectory(sourceData, modsDirectory);

                // Load the available mods
                ModsState.RefreshModsList(ModsPath);

                // Read and apply any window preferences
                LoadPreferences();

                // Apply game settings (resolution, display mode, etc.) - this should override old preferences
                _state.GameSettings.ApplyDisplaySettings(this);

                // Apply UI scaling
                ImGui.GetStyle().FontScaleMain = _state.GameSettings.UIScale;

                // Apply any saved user orbit settings
                LoadUserOrbitSettings();

                PopulateStyles();

            }
            catch(Exception e)
            {
                Console.WriteLine($"Error setting up game data: {e.Message}");
                Trace.WriteLine($"Error setting up game data: {e}");
            }

            _debugSDLFontHeight = SDL3.TTF.GetFontHeight(Styles.SDLDefaultFont);
        }

        private void PopulateStyles()
        {
            // Load fonts - texture will be created automatically by the new texture system
            var defaultFont = "ProggyClean.ttf";
            var defaultFontPath = Path.Combine(ResourcesPath, defaultFont);
            var defaultFontSize = 13f;

            Trace.WriteLine("loading font: " + defaultFontPath);
            if (!File.Exists(defaultFontPath))
                Trace.WriteLine("WARNING: font file does not exist: " + defaultFontPath);
            Styles.SDLDefaultFont = SDL3.TTF.OpenFont(defaultFontPath, 16f); // FIXME: set this and imgui font to same size. 13f looks terrible.
            if (Styles.SDLDefaultFont == IntPtr.Zero)
                Trace.WriteLine("WARNING: TTF.OpenFont failed: " + SDL.GetError());
            Styles.DefaultFont = PlatformBackend.LoadFont(ResourcesPath, defaultFont, defaultFontSize);

            PlatformBackend.LoadFont(ResourcesPath, "DejaVuSans.ttf", 13f, "ΩωΝνΔδθΘϖ⚙", true);
            Styles.MonospaceFont = PlatformBackend.LoadFont(ResourcesPath, "JetBrainsMono-Regular.ttf", 14f);
            Styles.MediumFont = PlatformBackend.LoadFont(ResourcesPath, "Roboto-Medium.ttf", 14f);

            // Theme
            Styles.Theme = _theme;
            _theme.Apply();
        }

        internal event EventHandler<SDL.Event> MouseMoveOccured;
        internal event EventHandler<SDL.Event> MouseButtonDownOccured;
        internal event EventHandler<SDL.Event> MouseButtonUpOccured;
        internal event EventHandler<SDL.Event> MouseWheelOccured;

        public override void HandleEvent(SDL.Event e)
        {
            // Input-side sibling of SafeRender: SDL3Window.PollEvents has NO try/catch, so an exception in any
            // click/key handler used to crash the WHOLE process — and the trace reached only console_output.txt,
            // not the rotating game_logs pages. Catch it, log ONCE per signature as [InputError], and let the event
            // loop continue: a bad click does nothing instead of killing the game, and it names itself in the log.
            // (This is exactly what would have turned the 2026-06-26 StarSystemStates click crash into a logged,
            // survivable event — gotcha #14.)
            try
            {
                DispatchEvent(e);
            }
            catch (Exception ex)
            {
                string sig = "input|" + ((SDL.EventType)e.Type) + "|" + ex.GetType().Name + "|" + ex.Message;
                if (_loggedRenderErrors.Add(sig))
                {
                    Console.WriteLine("[InputError] event " + ((SDL.EventType)e.Type)
                        + " handler threw and was skipped (logged once per unique error). Fix the cause:");
                    Console.WriteLine(ex.ToString());
                    Console.Out.Flush();
                }
            }
        }

        void DispatchEvent(SDL.Event e)
        {
            (float mX, float mY, SDL.MouseButtonFlags mouseFlags) = GetMouseState();

            if (mX != mouseX || mY != mouseY)
                MouseMoveOccured?.Invoke(this, e);

            mouseX = mX;
            mouseY = mY;

            if(!_state.IsGameLoaded)
            {
                var compare = 0;
#if DEBUG
                // Debug builds have the git hash displayed in the bottom left corner
                compare = 1;
#endif
                // Open the main menu if no other windows are open
                if(ImGui.GetIO().MetricsRenderWindows == compare)
                    MainMenuItems.GetInstance().SetActive(true);
                return;
            }

            if (!PlatformBackend.WantsMouseCapture())
            {
                switch (e.Type)
                {
                    case (uint)SDL.EventType.MouseButtonDown:
                        MouseButtonDownOccured?.Invoke(this, e);
                        break;
                    case (uint)SDL.EventType.MouseButtonUp:
                        MouseButtonUpOccured?.Invoke(this, e);
                        break;
                    case (uint)SDL.EventType.MouseWheel:
                        MouseWheelOccured?.Invoke(this, e);
                        break;
                }
            }

            // The top of the hotkey stack should list for hotkeys
            _state.HotKeys.Peek().HandleEvent(e);
        }

        public override void Update()
        {
            base.Update();

            //update and refresh state for GameDateTimechange
            if(_state.Game != null)
            {
                DateTime curTime = _state.Game.TimePulse.GameGlobalDateTime;
                if (curTime != _state.LastGameUpdateTime)
                {
                    foreach (var item in _state.UpdateableWindows)
                    {
                        if (item.GetActive() == true)
                            item.OnGameTickChange(curTime);
                    }

                    _state.LastGameUpdateTime = curTime;
                }

                //update and refresh state for SystemDateTimechage
                curTime = _state.SelectedSystemTime;
                if (curTime != _state.SelectedSysLastUpdateTime)
                {
                    foreach (var item in _state.UpdateableWindows)
                    {
                        if (item.GetActive() == true)
                            item.OnSystemTickChange(curTime);
                    }

                    _state.SelectedSysLastUpdateTime = curTime;
                }
            }

            var tUpdate = SDL.GetTicks();
            SessionLog.CurrentStage = "state.Update";   // breadcrumb: the heavy un-SafeRender-wrapped stage
            _state.Update();
            _perfUpdateMs = SDL.GetTicks() - tUpdate;
        }

        public override void Render()
        {
            base.Render();

            // Render the game (isolated so a map-draw fault logs + is skipped, not a hard crash)
            var tMap = SDL.GetTicks();
            SafeRender("GalacticMap.Draw", () => _state.GalacticMap?.Draw());
            _perfMapDrawMs = SDL.GetTicks() - tMap;

            // Render the UI
            var tUi = SDL.GetTicks();
            RenderUI();
            _perfUiMs = SDL.GetTicks() - tUi;

            // If in DEBUG render the git hash as the version in the corner of the screen
#if DEBUG
            var version = "Version: " + AssemblyInfo.GetGitHash();
            RenderDebugText(this.Renderer, version, 50);

            var iver = "ImGui version: " + ImGui.GetVersion();
            RenderDebugText(this.Renderer, iver, 50 + _debugSDLFontHeight);

            var sver = "SDL version: " + SDL.GetRevision();
            RenderDebugText(this.Renderer, sver, 50 + _debugSDLFontHeight * 2);
#endif

            // Show FPS counter if enabled
            if (_state.GameSettings.ShowFPS)
            {
                _fpsFrames += 1;

                var currentTime = SDL.GetTicks();
                var elapsedTime = currentTime - _fpsLastMeasurementTime;

                if (elapsedTime >= 1000)
                {
                    _fpsLastMeasurement = _fpsFrames / (elapsedTime / 1000f);
                    _fpsFrames = 0;
                    _fpsLastMeasurementTime = currentTime;
                }

                var fps = "FPS: " + _fpsLastMeasurement.ToString();
                RenderDebugText(this.Renderer, fps, 50 + _debugSDLFontHeight * 4);
            }

            // Combat-interrupt banner: a visible reason for the auto-pause when a battle begins (armed in
            // PostFrameUpdate from MasterTimePulse.CombatInterruptPending). Shows near the top for ~8 s.
            if (SDL.GetTicks() < _combatBannerUntilTick)
                RenderDebugText(this.Renderer,
                    ">>> COMBAT HAS BEGUN -- time auto-paused. Open the Fleet window's Combat tab to steer the fight. <<<",
                    20);
        }

        public override void PostFrameUpdate()
        {
            base.PostFrameUpdate();
            SessionLog.FrameTick();   // stamp "the main loop is alive this frame" for the hang watchdog

            foreach (var (_, systemState) in _state.StarSystemStates)
            {
                systemState.PostFrameCleanup();
            }

            // Session-recorder heartbeat: a STATE snapshot (game clock, run/pause, step, selection, ship count)
            // every ~3 s so the log shows the situation even when the player isn't clicking. Wrapped in SafeRender
            // because _state.SelectedSystem THROWS when no system is selected — a fault here logs once and the
            // game keeps running rather than crashing on a diagnostic.
            var now = SDL.GetTicks();

            // [PERF] slow-frame gauge: time since the previous frame. A progressive slowdown (e.g. zooming until it
            // freezes) shows its CLIMB here — 50ms, 200ms, 900ms... — right up to the point the [HANG] watchdog trips
            // on a full stall. Throttled to ~1 line/sec so a sustained slow patch doesn't flood the pages.
            ulong frameMs = _lastFrameMsTick == 0 ? 0 : now - _lastFrameMsTick;
            _lastFrameMsTick = now;
            if (frameMs > 250 && now - _lastSlowFrameLogTick > 1000)
            {
                _lastSlowFrameLogTick = now;
                SessionLog.State("⏱ slow frame " + frameMs + "ms — stage breakdown: update(state+transforms) "
                    + _perfUpdateMs + " / map-draw(SDL lines) " + _perfMapDrawMs + " / ui(windows) " + _perfUiMs
                    + " ms — the biggest number is the culprit");
            }

            if (_state.IsGameLoaded && now - _lastHeartbeatTick >= 3000)
            {
                _lastHeartbeatTick = now;
                SafeRender("Heartbeat", () =>
                {
                    SessionLog.Heartbeat(_state.Game, _state.SelectedSystem, _state.LastClickedEntity?.Name);
                    // Detection + EMCON snapshot: what the player detects, the fog gap, and how loud their ships run.
                    SessionLog.DetectionSnapshot(_state.SelectedSystem, _state.PlayerFaction);
                    // Fault tally: if anything has thrown (render or input), keep the running count visible each beat
                    // so a session that's quietly accumulating faults says so at a glance. Silent when clean (0).
                    if (_loggedRenderErrors.Count > 0)
                        SessionLog.State("⚠ faults this session: " + _loggedRenderErrors.Count
                            + " unique (render+input — grep [RenderError]/[InputError])");
                });
            }

            // Combat interrupt: the engine sets CombatInterruptPending and stops the clock the instant a new battle
            // begins (see CombatEngagement.InterruptTimeOnNewEngagement). Surface it — record it + flash a banner —
            // so the auto-pause reads as "combat started," not a mystery stop. Read-and-clear (one-shot).
            if (_state.IsGameLoaded && _state.Game.TimePulse.CombatInterruptPending)
            {
                _state.Game.TimePulse.CombatInterruptPending = false;
                _combatBannerUntilTick = now + 8000; // show for ~8 s
                SessionLog.Action("COMBAT INTERRUPT — a battle began; time auto-paused. Combat view opened.");

                // Pop the combat view for the player automatically — no digging through SM / DevTools. The Battle
                // Report shows the live play-by-play (it reads BattleLog each frame as salvos land). Then bring up
                // the player's engaged fleet so the Combat tab (doctrine controls) is one click away. Uses the REAL
                // player faction, so it works whether or not SM mode is on. Defensive: SelectedSystem can throw when
                // nothing is selected, so the fleet lookup is guarded — the Battle Report still pops regardless.
                BattleReportWindow.GetInstance().SetActive(true);
                try
                {
                    var sys = _state.SelectedSystem;
                    var pf = _state.PlayerFaction;
                    if (sys != null && pf != null)
                    {
                        foreach (var f in sys.GetAllEntitiesWithDataBlob<FleetDB>())
                        {
                            if (f.FactionOwnerID == pf.Id && f.HasDataBlob<FleetCombatStateDB>())
                            {
                                var fw = FleetWindow.GetInstance();
                                fw.SelectFleet(f);
                                fw.SetActive(true);
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>A one-line end-of-session readback (faults + how far the clock got). Logged on a CLEAN exit, so
        /// its PRESENCE in the log = the game quit normally; its ABSENCE = it crashed/froze (cross-check [HANG]/[FATAL]).</summary>
        public string SessionSummary()
        {
            try
            {
                string clock = _state?.Game != null ? _state.Game.TimePulse.GameGlobalDateTime.ToString("yyyy-MM-dd HH:mm") : "n/a";
                return "faults=" + _loggedRenderErrors.Count + ", game-time=" + clock;
            }
            catch { return "faults=" + _loggedRenderErrors.Count; }
        }

        /// <summary>
        /// Runs one piece of per-frame rendering; if it throws, logs the full exception to the captured
        /// game log (Console is redirected to game_log.txt) and skips just that piece this frame, instead
        /// of letting it crash the whole app. ImGui error recovery (enabled in SDL3Window.Run) cleans up
        /// any half-open window/stack. Dedupes by error signature so a per-frame fault logs once, not 60×/s.
        /// </summary>
        private void SafeRender(string context, Action render)
        {
            // Breadcrumb for the hang watchdog: if render() freezes (a long/native/infinite call throws nothing),
            // the watchdog can name THIS context instead of us guessing. Cheap reference write on the main thread.
            SessionLog.CurrentStage = context;
            try
            {
                render();
            }
            catch (Exception e)
            {
                string signature = context + "|" + e.GetType().Name + "|" + e.Message;
                if (_loggedRenderErrors.Add(signature))
                {
                    Console.WriteLine("[RenderError] " + context + " threw and was skipped this frame (logged once per unique error). Fix the cause:");
                    Console.WriteLine(e.ToString());
                    Console.Out.Flush();
                }
            }
        }

        /// <summary>
        /// Render the UI
        /// </summary>
        public void RenderUI()
        {
            // ImGui helper windows
            if (_state.ShowMetrixWindow)
                ImGui.ShowMetricsWindow(ref _state.ShowMetrixWindow);

            if (_state.ShowDemoWindow)
            {
                ImGui.ShowDemoWindow();
                ImGui.ShowUserGuide();
            }

            // Render name icons
            SafeRender("GalacticMap.DrawNameIcons", () => _state.GalacticMap?.DrawNameIcons());

            // Render any windows that have registered themselves. Each Display() is isolated so one
            // faulting window logs + is skipped rather than crashing the whole app (see SafeRender).
            foreach (var item in _state.LoadedWindows.Values.ToArray())
            {
                SafeRender(item.GetType().Name, item.Display);
            }

            foreach (var entityWindow in _state.EntityWindows.Values.ToArray())
            {
                SafeRender("EntityWindow:" + entityWindow.GetType().Name, entityWindow.Display);
            }

            foreach (var item in _state.LoadedNonUniqueWindows.Values.ToArray())
            {
                SafeRender(item.GetType().Name, item.Display);
            }

            // Render the maneuver node panel overlay (if active)
            SafeRender("ManeuverNodePanel", () => _state.DisplayManeuverNodePanel());

            // Range-ring hover tooltip — last, so it reads the current WantCaptureMouse and draws over the map.
            SafeRender("RangeRingTooltip", () => _state.GalacticMap?.RangeRingTooltip());
        }

        public override void Exit()
        {
            // save the user orbit settings on exit
            SaveOrbitSettings();

            // save the game settings on exit
            _state.GameSettings.Save();

            // Cleanup SDL TTF
            SDL3.TTF.CloseFont(Styles.SDLDefaultFont);
        }

        /// <summary>
        /// If the given path & name don't exist create it
        /// </summary>
        /// <param name="path">A path to where to create the given name folder</param>
        /// <param name="name">The name of the folder to create</param>
        private void TryCreateDirectory(string path, string name)
        {
            string directory = Path.Combine(path, name);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Parse command line arguments to setup the data and
        /// resource paths
        /// </summary>
        /// <param name="args"></param>
        private void ParseCommandLineArguments(string[] args)
        {
            for(int i = 0; i < args.Length; i++)
            {
                switch(args[i].ToLower())
                {
                    case "--data":
                    case "-d":
                        if(i + 1 < args.Length)
                        {
                            Console.WriteLine($"Using {args[i].ToLower()} = {ModsPath}");
                            ModsPath = args[i + 1];
                            i++;
                        }
                        break;
                    case "--resources":
                    case "-r":
                        if(i + 1 < args.Length)
                        {
                            Console.WriteLine($"Using {args[i].ToLower()} = {ResourcesPath}");
                            ResourcesPath = args[i + 1];
                            i++;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Load the players preferences
        /// </summary>
        private void LoadPreferences()
        {
            string? appDataDirectory = GetAppDataPath();

            // If the app data path is bad here, just return its only the preferences
            if(string.IsNullOrEmpty(appDataDirectory)) return;

            string preferencesPath = Path.Combine(appDataDirectory, PreferencesFile);
            if(!File.Exists(preferencesPath))
            {
                File.Create(preferencesPath).Close();
            }

            IConfiguration preferences = new ConfigurationBuilder().AddIniFile(preferencesPath).Build();
            IConfigurationSection windowSection = preferences.GetSection("Window Settings");
            string? xPosition = windowSection["X"];
            string? yPosition = windowSection["Y"];
            string? width = windowSection["Width"];
            string? height = windowSection["Height"];
            string? maximized = windowSection["Maximized"];
            string? themeEnabled = windowSection["Theme"];

            if(xPosition != null) X = int.Parse(xPosition);
            if(yPosition != null) Y = int.Parse(yPosition);
            if(width != null) Width = int.Parse(width);
            if(height != null) Height = int.Parse(height);

            // if maximized is set to true it will override the other preferences
            if(maximized != null)
            {
                if(bool.Parse(maximized))
                    Maximize();
            }

            // TODO: more themes
            switch (themeEnabled)
            {
                case null:
                case "Default":
                    _theme = new DefaultTheme();
                    break;
                case "Futuristic":
                    _theme = new FuturisticTheme();
                    break;
                default:
                    Trace.WriteLine("WARNING: Unrecognized theme '" + themeEnabled + "', falling back to default");
                    _theme = new DefaultTheme();
                    break;
            }
        }

        /// <summary>
        /// Load the UserOrbitSettingsFile
        /// </summary>
        private void LoadUserOrbitSettings()
        {
            string? appDataDirectory = GetAppDataPath();

            if(string.IsNullOrEmpty(appDataDirectory))
                return;

            // Give up if the file doesn't exist
            string filePath = Path.Combine(appDataDirectory, UserOrbitSettingsFile);
            if(!File.Exists(filePath))
                return;

            string text = File.ReadAllText(filePath);
            var result = JsonConvert.DeserializeObject<List<List<UserOrbitSettings>>>(text);

            if(result != null)
                _state.UserOrbitSettingsMtx = result;
        }

        public void SaveOrbitSettings()
        {
            string? appDataDirectory = GetAppDataPath();
            if(appDataDirectory == null)
                return;

            string filePath = Path.Combine(appDataDirectory, UserOrbitSettingsFile);
            string output = JsonConvert.SerializeObject(_state.UserOrbitSettingsMtx);

            File.WriteAllText(filePath, output);
        }

        /// <summary>
        /// Deletes the contents of the destination directory and then copies the
        /// contents of the source directory to the destination directory.
        /// </summary>
        /// <param name="sourceDir">The directory to copy from</param>
        /// <param name="destinationDir">The directory to delete and then receive a copy of the source directory</param>
        public static void DeleteThenCopyToDirectory(string sourceDir, string destinationDir)
        {
            // Check if destination exists, if so delete it and all its contents
            if (Directory.Exists(destinationDir))
            {
                Directory.Delete(destinationDir, recursive: true);
            }

            // Create the destination directory fresh
            Directory.CreateDirectory(destinationDir);

            // Get all files and copy them
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }

            // Recursively copy all subdirectories
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destinationDir, subDirName);
                DeleteThenCopyToDirectory(subDir, destSubDir);
            }
        }

        private static void RenderDebugText(IntPtr renderer, string text, int y)
        {
            if (renderer == IntPtr.Zero)
                return;

            SDL.Color white = new () {
                R = 255,
                G = 255,
                B = 255,
                A = 255
            };

            IntPtr surface = SDL3.TTF.RenderTextSolid(
                    Styles.SDLDefaultFont,
                    text,
                    0,
                    white);

            if (surface == IntPtr.Zero) {
                Trace.WriteLine("RenderDebugText: failed to create surface");
                return;
            }

            IntPtr texture = SDL.CreateTextureFromSurface(renderer, surface);

            if (texture == IntPtr.Zero) {
                SDL.DestroySurface(surface);

                Trace.WriteLine("RenderDebugText: failed to create texture from surface");
                return;
            }

            int h;
            int w;
            SDL3.TTF.GetStringSize(Styles.SDLDefaultFont, text, 0, out w, out h);

            SDL.FRect frect = new () {
                X = 5,
                Y = y,
                W = w,
                H = h
            };

            SDL.RenderTexture(renderer, texture, IntPtr.Zero, ref frect);

            SDL.DestroyTexture(texture);
            SDL.DestroySurface(surface);
        }
    }
}
