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
            }
            catch (Exception e)
            {
                Line("STATE", "heartbeat failed: " + e.Message);
            }
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
