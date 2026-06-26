using System;
using Pulsar4X.Engine;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Ships;

namespace Pulsar4X.Client
{
    /// <summary>
    /// Session recorder — a readable play-by-play of the player's actions + periodic state, written to the
    /// captured log (Console is redirected to game_log.txt in the repo root; see Program.cs). Every line is
    /// FLUSHED immediately, so a crash or freeze still leaves the full trail up to that instant. Categories keep
    /// the log greppable: [ACTION] [VIEW] [TIME] [CAMERA] [SELECT] [DRAG] [STATE]. Toggle the whole thing with
    /// <see cref="Enabled"/>. This is the "flight recorder" — the point is that a future bug report IS the log,
    /// instead of "reproduce it and send a log."
    /// </summary>
    public static class SessionLog
    {
        public static bool Enabled = true;

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

        /// <summary>
        /// Periodic "situation snapshot" written every few seconds even when the player isn't doing anything —
        /// the equivalent of a watch-stander logging the gauges each round. Records the game clock, whether time
        /// is running or paused, the time step, what's currently selected, and how many ships are in the viewed
        /// system. So if a freeze or oddity happens between clicks, the log still shows the state right up to it.
        /// Caller passes the pieces it already has in hand; null/empty are tolerated.
        /// </summary>
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
        /// tells: (1) the position reads right on the system origin (sun-dist under ~1 Gm), or (2) the position's
        /// anchor (Parent) has gone null/invalid — the exact state that makes AbsolutePosition collapse to the
        /// star (see MoveState.cs:44). Also prints the ship's move mode (Orbit/Warp/...) — the smoking gun for
        /// whether it detached mid-warp. Returns how many it flagged.
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
                double sunDist = p.AbsolutePosition.Length();
                if (!parentBad && sunDist >= TeleportSunDistThreshold_m) continue; // looks fine

                found++;
                string name = sh.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : "?";
                string par = parent == null ? "ROOT/null"
                    : (parent.IsValid
                        ? (parent.TryGetDataBlob<NameDB>(out var pn) ? pn.OwnersName : "Entity#" + parent.Id)
                        : "INVALID");
                Line("STATE", "  ⚠ TELEPORT ship #" + sh.Id + " '" + name + "' faction=" + sh.FactionOwnerID
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
    }
}
