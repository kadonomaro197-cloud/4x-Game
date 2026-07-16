using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Ships;
using Pulsar4X.Sensors;
using Pulsar4X.Combat;

namespace Pulsar4X.Client
{
    /// <summary>
    /// Session recorder — a readable play-by-play of the player's actions + periodic state, written to the
    /// captured log (Console is redirected into rolling read-sized pages under game_logs/ in the repo root, so a
    /// whole session reads start-to-finish without a "file too large" wall; see Program.cs / RotatingLogWriter.cs).
    /// Every line is FLUSHED immediately, so a crash or freeze still leaves the full trail up to that instant.
    /// Categories keep the log greppable: [ACTION] [VIEW] [TIME] [CAMERA] [SELECT] [DRAG] [STATE] [DETECT] [EMCON]
    /// [ENGINE] (plus the fault gauges [InputError]/[RenderError]/[HANG]/[FATAL] and engine-side [Combat]/[WARP]/[FleetCombat]/[DevTools]). Toggle the whole thing with
    /// <see cref="Enabled"/>. This is the "flight recorder" — the point is that a future bug report IS the log,
    /// instead of "reproduce it and send a log."
    /// </summary>
    public static class SessionLog
    {
        public static bool Enabled = true;

        // Previous values of the engine liveness counters, so the heartbeat can report the per-beat DELTA (is the
        // processor still climbing = alive, or stuck = dead).
        private static long _lastScanCount, _lastTickCount;

        // Per-faction newest-flushed decision timestamp, so the heartbeat writes each [AI] record ONCE (the tape is a
        // ring buffer, so an index count would break once it caps — timestamps are monotonic per monthly tick).
        private static readonly Dictionary<int, DateTime> _lastAiWhen = new();
        // Newest AI-Tick error count we've already surfaced, so a swallowed Tick throw is logged ONCE per occurrence.
        private static int _lastAiErrorCount;

        public static void Line(string category, string message)
        {
            if (!Enabled) return;
            Console.WriteLine("[" + category + "] " + message);
            Console.Out.Flush();
        }

        public static void Action(string m) => Line("ACTION", m);
        public static void View(string m)   => Line("VIEW", m);
        public static void Time(string m)   => Line("TIME", m);
        public static void Camera(string m) => Line("CAMERA", m);
        public static void Select(string m) => Line("SELECT", m);
        public static void Drag(string m)   => Line("DRAG", m);
        public static void State(string m)  => Line("STATE", m);

        // ── Hang watchdog ──────────────────────────────────────────────────────────────────────────────────
        // The per-event ([InputError]) and per-draw ([RenderError]) nets only catch THROWS on the main thread. A
        // FREEZE — the main loop stuck in a long or infinite operation — throws nothing: the log just stops, which
        // reads identically to a crash (this is the 2026-06-26 "no trace anywhere" case). This watchdog runs on its
        // OWN thread, so it keeps logging even while the main thread is wedged: PostFrameUpdate stamps _lastFrameTick
        // every frame; if that stamp goes stale by more than HangThresholdMs, the main loop is hung and we write a
        // [HANG] line ONCE (it re-arms if the loop recovers). The lines just ABOVE the [HANG] are where it wedged.
        static volatile int _lastFrameTick;
        public static int HangThresholdMs = 5000;   // a single "frame" this long = frozen, not just a slow tick

        /// <summary>The render/update stage currently executing — set by PulsarMainWindow.SafeRender (and around the
        /// state update) so the hang watchdog can NAME the wedged stage instead of leaving us to guess from the lines
        /// above. A hang leaves no stack trace, so this breadcrumb is the difference between "it froze somewhere" and
        /// "it froze in window X". Volatile: written on the main thread, read on the watchdog thread.</summary>
        public static volatile string CurrentStage = "(startup)";

        /// <summary>Called once per frame by the render loop — stamps "the main loop is still alive" for the watchdog.</summary>
        public static void FrameTick() => _lastFrameTick = Environment.TickCount;

        /// <summary>Start the background hang watchdog (a daemon thread; call once at startup). It cannot catch a
        /// hard native crash that kills every thread at once, but it WILL name a freeze the main thread is stuck in.</summary>
        public static void StartHangWatchdog()
        {
            _lastFrameTick = Environment.TickCount;
            try
            {
                var t = new System.Threading.Thread(() =>
                {
                    bool reported = false;
                    while (true)
                    {
                        try { System.Threading.Thread.Sleep(1000); } catch { }
                        int stall = Environment.TickCount - _lastFrameTick;
                        if (stall >= HangThresholdMs)
                        {
                            if (!reported)
                            {
                                Line("HANG", "main loop STALLED ~" + (stall / 1000) + "s (frozen, or stuck in a long native/render call) — "
                                    + "wedged in stage: '" + CurrentStage + "' (the lines just above this are the last activity before it)");
                                reported = true;
                            }
                        }
                        else if (reported)
                        {
                            Line("HANG", "main loop recovered after the stall");
                            reported = false;
                        }
                    }
                });
                t.IsBackground = true;   // never blocks process exit
                t.Name = "Pulsar4X-HangWatchdog";
                t.Start();
            }
            catch (Exception e) { Line("HANG", "watchdog failed to start: " + e.Message); }
        }

        /// <summary>
        /// Periodic "situation snapshot" written every few seconds even when the player isn't doing anything —
        /// the equivalent of a watch-stander logging the gauges each round. Records the game clock, whether time
        /// is running or paused, the time step, what's currently selected, and how many ships are in the viewed
        /// system. So if a freeze or oddity happens between clicks, the log still shows the state right up to it.
        /// Caller passes the pieces it already has in hand; null/empty are tolerated.
        /// </summary>
        // Sim-stall gauge — the FREEZE case the MAIN-thread hang watchdog can't see: the sim/background thread wedges
        // inside a processor while the UI keeps rendering + heartbeating, so game-time stops advancing but the log keeps
        // printing "RUNNING" (exactly the 2026-07-14 freeze — clock stuck at 18:00, heartbeats fine). We watch the game
        // clock across heartbeats: unchanged while RUNNING = the sim is wedged → NAME the processor from the engine gauge.
        static DateTime _lastRunningGameTime = DateTime.MinValue;
        static int _simStallBeats;
        static bool _simStallReported;
        // Dedicated previous-beat liveness counters for the stall check (independent of DetectionSnapshot's copies,
        // whose update ordering vs the heartbeat is not guaranteed), so "still climbing?" compares to the LAST beat.
        static long _stallPrevScan, _stallPrevTick;

        public static void Heartbeat(Game game, StarSystem system, string selectedName)
        {
            if (!Enabled || game == null) return;
            try
            {
                var tp = game.TimePulse;
                int shipCount = system != null ? system.GetAllEntitiesWithDataBlob<ShipInfoDB>().Count : 0;
                Line("STATE", "heartbeat t=" + tp.GameGlobalDateTime.ToString("yyyy-MM-dd HH:mm")
                    + " " + (tp.IsRunning ? "RUNNING" : "paused")
                    + " step=" + tp.Ticklength.TotalSeconds.ToString("0") + "s"
                    + " selected=" + (string.IsNullOrEmpty(selectedName) ? "(none)" : selectedName)
                    + " ships-in-view=" + shipCount);

                // Sim-stall detection: RUNNING but the clock isn't moving = a wedged processor (a freeze the frame
                // watchdog misses, because the UI thread is alive). Name the culprit from ManagerSubPulse's global gauge.
                if (tp.IsRunning)
                {
                    if (tp.GameGlobalDateTime == _lastRunningGameTime)
                    {
                        _simStallBeats++;
                        if (_simStallBeats >= 2 && !_simStallReported)   // ~2 quiet heartbeats of a frozen clock
                        {
                            // The DECISIVE discriminator: are the engine liveness counters STILL MOVING? If the
                            // battle-trigger / sensor-scan counts changed since the last beat, the sim is GRINDING
                            // through a heavy tick (slow-at-scale — a perf problem); if they're frozen too, it's a
                            // TRUE WEDGE (deadlock / infinite loop) in the named processor. Plus the SCALE (fleets /
                            // in-combat / ships) so a recurrence tells us how big the fight was when it stalled.
                            long scans = SensorScan.ScanCount, ticks = CombatEngagement.TickCount;
                            bool climbing = scans != _stallPrevScan || ticks != _stallPrevTick;
                            int fleets = 0, inCombat = 0, ships = 0;
                            try
                            {
                                if (system != null)
                                {
                                    var fl = system.GetAllEntitiesWithDataBlob<Pulsar4X.Fleets.FleetDB>();
                                    fleets = fl.Count;
                                    foreach (var f in fl)
                                        if (f.IsValid && f.HasDataBlob<FleetCombatStateDB>()) inCombat++;
                                    ships = system.GetAllEntitiesWithDataBlob<ShipInfoDB>().Count;
                                }
                            }
                            catch { /* a diagnostic must never throw */ }

                            Line("SIM-STALL", "game-time FROZEN at " + tp.GameGlobalDateTime.ToString("yyyy-MM-dd HH:mm")
                                + " while RUNNING — the sim thread is WEDGED (a freeze the frame watchdog can't see). "
                                + "Last processor the sim entered: '" + Pulsar4X.Engine.ManagerSubPulse.GlobalCurrentProcess + "'. "
                                + "scans=" + scans + " ticks=" + ticks + " ("
                                + (climbing ? "STILL CLIMBING → SLOW-AT-SCALE heavy tick, not a true wedge — a PERF problem"
                                            : "FROZEN too → a TRUE WEDGE, deadlock/infinite loop in that processor") + "). "
                                + "scale: fleets=" + fleets + " in-combat=" + inCombat + " ships=" + ships + " in view.");
                            _simStallReported = true;
                        }
                    }
                    else
                    {
                        _lastRunningGameTime = tp.GameGlobalDateTime;
                        _simStallBeats = 0;
                        if (_simStallReported) { Line("SIM-STALL", "sim recovered — clock advancing again."); _simStallReported = false; }
                    }
                    // Snapshot the liveness counters each beat so the NEXT stall check compares to THIS beat's values.
                    _stallPrevScan = SensorScan.ScanCount;
                    _stallPrevTick = CombatEngagement.TickCount;
                }

                CheckForTeleports(system);
            }
            catch (Exception e)
            {
                Line("STATE", "heartbeat failed: " + e.Message);
            }
        }

        /// <summary>
        /// Distance from the Sun (the system origin), in metres, below which a ship is certainly "teleported" —
        /// nothing real orbits this close to the star (the innermost body, Mercury, is ~58 Gm = 5.8e10 m out).
        /// 1 Gm = 1e9 m. A detached ship draws at ~its old orbital offset from origin (~12,000 km = 1.2e7 m).
        /// </summary>
        const double TeleportSunDistThreshold_m = 1e9;

        /// <summary>
        /// Scan every ship in the system for the "teleported to the Sun" signature and log each offender, so the
        /// recorder catches the warp/Sun-jump bug the instant it happens — no faction-view switch needed. Two
        /// tells: (1) AT-SUN — the position reads right on the system origin (sun-dist under ~1 Gm), the collapse;
        /// or (2) ORPHANED — the anchor (Parent) has gone null/invalid **while the ship is NOT warping**. A normal
        /// warp is reparented to root (null parent) ON PURPOSE and carries its true absolute position, so warp is
        /// excluded to avoid false alarms (learned live 2026-06-26: ships at 111 Gm correctly en route to Jupiter
        /// were being flagged). Prints the reason + move mode (Orbit/Warp/...). Returns how many it flagged.
        /// </summary>
        public static int CheckForTeleports(StarSystem sys)
        {
            if (!Enabled || sys == null) return 0;
            int found = 0;
            foreach (var sh in sys.GetAllEntitiesWithDataBlob<ShipInfoDB>())
            {
                if (!sh.TryGetDataBlob<PositionDB>(out var p)) continue;
                var parent = p.Parent;
                bool parentBad = parent == null || !parent.IsValid;
                bool warping = p.MoveType == PositionDB.MoveTypes.Warp;
                double sunDist = p.AbsolutePosition.Length();

                // A REAL teleport is the position collapsing to the origin/Sun: an orbiting ship that loses its
                // parent falls back to its small RelativePosition (MoveState.cs:44) and draws on the star. A
                // WARPING ship, though, is INTENTIONALLY reparented to the system root (parent null) and carries its
                // true absolute coords in RelativePosition — so a null parent at a healthy distance is NORMAL warp,
                // NOT a teleport. (Confirmed live 2026-06-26: ships flagged here at 111 Gm were correctly en route
                // to Jupiter — the old null-parent trigger cried wolf on every warp.) Flag only the real cases:
                // AT-SUN (collapsed to origin), or ORPHANED (null/invalid parent while NOT warping).
                bool atSun = sunDist < TeleportSunDistThreshold_m;
                bool orphaned = parentBad && !warping;
                if (!atSun && !orphaned) continue; // looks fine (incl. a normally-warping ship with a root/null parent)

                found++;
                string name = sh.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : "?";
                string par = parent == null ? "ROOT/null"
                    : (parent.IsValid
                        ? (parent.TryGetDataBlob<NameDB>(out var pn) ? pn.OwnersName : "Entity#" + parent.Id)
                        : "INVALID");
                string reason = atSun ? "AT-SUN (pos collapsed to origin)" : "ORPHANED (null parent, not warping)";
                Line("STATE", "  ⚠ TELEPORT " + reason + " ship #" + sh.Id + " '" + name + "' faction=" + sh.FactionOwnerID
                    + " sun-dist=" + (sunDist / 1e9).ToString("0.###") + "Gm parent=" + par
                    + " moveType=" + p.MoveType);
            }
            return found;
        }

        /// <summary>
        /// Dump every ship in a system: distance from the Sun (the system origin) in Gm (millions of km) + the
        /// entity its position hangs off. This is the "teleport" gauge — a ship reading ~0 Gm, or parent
        /// ROOT/null/INVALID, has detached from its anchor and will draw on the Sun. Earth orbit ≈ 150 Gm.
        /// </summary>
        public static void DumpShipPositions(StarSystem sys, string context)
        {
            if (!Enabled || sys == null) return;
            try
            {
                var ships = sys.GetAllEntitiesWithDataBlob<ShipInfoDB>();
                Line("STATE", "ship positions (" + context + "): " + ships.Count + " ship(s)");
                foreach (var sh in ships)
                {
                    string name = sh.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : "?";
                    string pos = "no-PositionDB";
                    if (sh.TryGetDataBlob<PositionDB>(out var p))
                    {
                        var parent = p.Parent;
                        string par = parent == null ? "ROOT/null"
                            : (parent.IsValid
                                ? (parent.TryGetDataBlob<NameDB>(out var pn) ? pn.OwnersName : "Entity#" + parent.Id)
                                : "INVALID");
                        pos = "sun-dist=" + (p.AbsolutePosition.Length() / 1e9).ToString("0.##") + "Gm parent=" + par;
                    }
                    Line("STATE", "  ship #" + sh.Id + " '" + name + "' faction=" + sh.FactionOwnerID + " " + pos);
                }
            }
            catch (Exception e)
            {
                Line("STATE", "ship-position dump failed: " + e.Message);
            }
        }

        /// <summary>
        /// Detection + EMCON situation snapshot for one faction — the "what can I see, and how loud am I" gauge the
        /// detection/EMCON stack is all about. Logs three things the rest of the recorder can't show:
        ///   • <c>[DETECT]</c> — how many sensor contacts this faction holds, and the FOG GAP: how many
        ///     other-faction ships are in the system vs how many of them it actually detects (the rest are hidden).
        ///   • <c>[EMCON]</c> — a summary of this faction's own ships' emitted-signature multiplier
        ///     (<see cref="SensorProfileDB.ActivityMultiplier"/>): how many run hot/loud (&gt;1.2), dark/quiet
        ///     (&lt;0.8), or are blind (sensors shot off), plus the loudest and quietest ship by name.
        /// So when you play with fog of war on, the log shows what you detect, what's hidden, and which of your
        /// ships are lit up — not just the actions and battles. Called from the ~3 s heartbeat. Read-only; nulls OK.
        /// </summary>
        public static void DetectionSnapshot(StarSystem system, Entity faction)
        {
            if (!Enabled || system == null || faction == null) return;
            try
            {
                // Processor liveness FIRST — proves the detection (scan) + combat (trigger) ENGINES are firing live.
                // If these don't climb while ships are present, the processor is dead: that's "the scan never ran"
                // (or "the trigger never fires on play"), NOT "nothing to detect" — both documented live unknowns.
                long scans = SensorScan.ScanCount, ticks = CombatEngagement.TickCount;
                Line("ENGINE", "sensor scans " + scans + " (+" + (scans - _lastScanCount)
                    + "), battle-trigger passes " + ticks + " (+" + (ticks - _lastTickCount) + ") since last beat");
                _lastScanCount = scans;
                _lastTickCount = ticks;

                int fid = faction.Id;
                var contacts = system.GetSensorContacts(fid);
                int held = contacts != null ? contacts.GetAllContacts().Count : 0;
                string fname = faction.TryGetDataBlob<NameDB>(out var fn) ? fn.OwnersName : ("faction#" + fid);

                var ships = system.GetAllEntitiesWithDataBlob<ShipInfoDB>();

                // Other-faction ships present vs how many this faction detects — the fog-of-war gap.
                int others = 0, detected = 0;
                foreach (var sh in ships)
                {
                    if (sh.FactionOwnerID == fid) continue;
                    others++;
                    if (contacts != null && contacts.SensorContactExists(sh.Id)) detected++;
                }
                Line("DETECT", fname + " holds " + held + " contact(s); " + others + " other-faction ship(s) in-system, detects "
                    + detected + " (" + (others - detected) + " hidden from you)");

                // REACH gauge (detection-tuning diagnostic): our BEST sensor reach (how far we'd see a ship like us)
                // vs the distance to the NEAREST enemy ship — and whether that enemy is actually detected. If the
                // nearest enemy sits well BEYOND our reach, detection is simply too short for combat to trigger (the
                // "sat at Luna and saw nothing" case) — this is the number that tells "tune the sensors" from "a bug".
                double bestReach = 0; string reachShip = "";
                foreach (var sh in ships)
                {
                    if (sh.FactionOwnerID != fid) continue;
                    double r = SensorTools.SensorReachRange_m(sh);
                    if (r > bestReach) { bestReach = r; reachShip = sh.TryGetDataBlob<NameDB>(out var rn) ? rn.OwnersName : ("#" + sh.Id); }
                }
                double nearestEnemy = double.MaxValue; bool nearestDetected = false;
                foreach (var en in ships)
                {
                    if (en.FactionOwnerID == fid) continue;
                    if (!en.TryGetDataBlob<PositionDB>(out var ep)) continue;
                    var epos = ep.AbsolutePosition;
                    foreach (var myShip in ships)
                    {
                        if (myShip.FactionOwnerID != fid) continue;
                        if (!myShip.TryGetDataBlob<PositionDB>(out var mp)) continue;
                        double d = (epos - mp.AbsolutePosition).Length();
                        if (d < nearestEnemy)
                        {
                            nearestEnemy = d;
                            nearestDetected = contacts != null && contacts.SensorContactExists(en.Id);
                        }
                    }
                }
                if (bestReach > 0 || nearestEnemy < double.MaxValue)
                {
                    string reachStr = (bestReach / 1e9).ToString("0.###") + " Gm";
                    string nearStr = nearestEnemy < double.MaxValue
                        ? (nearestEnemy / 1e9).ToString("0.###") + " Gm (" + (nearestDetected ? "DETECTED" : "beyond reach / undetected") + ")"
                        : "no enemy ships";
                    Line("REACH", "best sensor reach " + reachStr + " (" + reachShip + "); nearest enemy " + nearStr);
                }

                // This faction's own ships' EMCON signature (1.0 = as-designed; <1 quiet/Silent; >1 loud/running hot).
                int mine = 0, hot = 0, dark = 0, blind = 0;
                double loudest = double.MinValue, quietest = double.MaxValue;
                string loudName = "", quietName = "";
                foreach (var sh in ships)
                {
                    if (sh.FactionOwnerID != fid) continue;
                    if (!sh.TryGetDataBlob<SensorProfileDB>(out var sp)) continue;
                    mine++;
                    double sig = sp.ActivityMultiplier;
                    if (sig > 1.2) hot++;
                    else if (sig < 0.8) dark++;
                    if (!sh.TryGetDataBlob<SensorAbilityDB>(out var ab) || ab.InstanceStates.Count == 0) blind++;
                    string nm = sh.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : ("#" + sh.Id);
                    if (sig > loudest) { loudest = sig; loudName = nm; }
                    if (sig < quietest) { quietest = sig; quietName = nm; }
                }
                if (mine > 0)
                    Line("EMCON", "your " + mine + " ship(s): hot(loud)=" + hot + " dark(quiet)=" + dark + " blind=" + blind
                        + "; loudest " + loudName + " x" + loudest.ToString("0.##") + ", quietest " + quietName + " x" + quietest.ToString("0.##"));
            }
            catch (Exception e)
            {
                Line("DETECT", "detection snapshot failed: " + e.Message);
            }
        }

        /// <summary>
        /// B4b — flush the AI FLIGHT RECORDER to the log. For every NPC faction, writes each NEW decision on its
        /// <see cref="AIDecisionRecordDB"/> tape as an <c>[AI]</c> line (via the CI-tested <see cref="PlanReadout.DecisionLine"/>),
        /// so the whole game's AI reasoning reads back in the rolling <c>game_logs/</c> pages — the SAME data the AI
        /// Inspector window shows live. Called from the ~3 s heartbeat; a per-faction newest-timestamp gate writes each
        /// record ONCE. Defensive/no-throw (an observability flush must never break the frame).
        /// </summary>
        public static void AiDecisionSnapshot(Game game)
        {
            if (!Enabled || game == null) return;
            try
            {
                foreach (var kvp in game.Factions)
                {
                    var faction = kvp.Value;
                    if (faction == null || !faction.IsValid) continue;
                    if (!faction.TryGetDataBlob<AIDecisionRecordDB>(out var tape) || tape.Records.Count == 0) continue;

                    string name = faction.TryGetDataBlob<NameDB>(out var fn) ? fn.OwnersName : ("faction#" + faction.Id);
                    DateTime lastWhen = _lastAiWhen.TryGetValue(faction.Id, out var w) ? w : DateTime.MinValue;
                    DateTime newest = lastWhen;

                    foreach (var rec in tape.Records)
                    {
                        if (rec.When <= lastWhen) continue;                   // already flushed
                        Console.WriteLine(PlanReadout.DecisionLine(rec, name));
                        if (rec.When > newest) newest = rec.When;
                    }
                    _lastAiWhen[faction.Id] = newest;
                }

                // Surface any AI Tick exception. The engine-side freeze-guard swallows it so the sim clock never wedges;
                // here we make it VISIBLE (the Visibility Gate) — one [AI] ⚠ line per occurrence, so a bad decision/order
                // path shows up in game_logs instead of a faction silently going quiet.
                if (NPCDecisionProcessor.TickErrorCount > _lastAiErrorCount)
                {
                    Line("AI", $"⚠ tick error #{NPCDecisionProcessor.TickErrorCount}: {NPCDecisionProcessor.LastTickError}");
                    _lastAiErrorCount = NPCDecisionProcessor.TickErrorCount;
                }

                Console.Out.Flush();
            }
            catch (Exception e)
            {
                Line("AI", "decision snapshot failed: " + e.Message);
            }
        }
    }
}
