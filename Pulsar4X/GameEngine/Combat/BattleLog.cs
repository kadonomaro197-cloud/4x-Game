using System;
using System.Collections.Generic;

namespace Pulsar4X.Combat
{
    /// <summary>The kind of battle state-change a <see cref="BattleEvent"/> records.</summary>
    public enum BattleEventType
    {
        /// <summary>A fleet entered the battle.</summary>
        Engaged,
        /// <summary>A salvo landed and this fleet lost ship(s).</summary>
        Salvo,
        /// <summary>This fleet broke off (retreated).</summary>
        Retreat,
        /// <summary>This fleet left the battle (wiped, retreated, or no enemy left).</summary>
        Disengaged,
    }

    /// <summary>
    /// One structured state-change in a battle — the same play-by-play the engine narrates to the log as
    /// <c>[Combat]</c> lines, but captured as DATA the UI can read and that SURVIVES after the fight ends. This is
    /// what makes a "battle report you can review afterward" possible: the live <see cref="FleetCombatStateDB"/> is
    /// removed the moment a fleet disengages, so without this trail a fight you blinked and missed leaves nothing
    /// behind. Per-fleet (a battle has no single entity): an engagement shows up as several fleets' events sharing
    /// a time window.
    /// </summary>
    public readonly struct BattleEvent
    {
        /// <summary>Game time the event happened (not wall-clock).</summary>
        public readonly DateTime When;
        public readonly int FleetId;
        public readonly string FleetName;
        public readonly int FactionId;
        public readonly BattleEventType Type;
        /// <summary>Ships this fleet lost in this event (Salvo only; 0 otherwise).</summary>
        public readonly int ShipsLost;
        /// <summary>Ships this fleet had left right after the event.</summary>
        public readonly int ShipsLeft;
        /// <summary>Salvo number, for a Salvo event (0 otherwise).</summary>
        public readonly int Step;
        /// <summary>Freeform context — opponent label / "enters combat" / "disengages".</summary>
        public readonly string Note;

        public BattleEvent(DateTime when, int fleetId, string fleetName, int factionId,
                           BattleEventType type, int shipsLost, int shipsLeft, int step, string note)
        {
            When = when;
            FleetId = fleetId;
            FleetName = fleetName ?? "Fleet";
            FactionId = factionId;
            Type = type;
            ShipsLost = shipsLost;
            ShipsLeft = shipsLeft;
            Step = step;
            Note = note ?? "";
        }
    }

    /// <summary>
    /// Append-only, capped, thread-safe history of battle state-changes — the data source for the client's
    /// persistent Battle Report. The combat engine records an event at each state change (engage / salvo loss /
    /// retreat / disengage); the UI reads <see cref="Recent"/> to list recent fights even after they end.
    ///
    /// Capture is UNCONDITIONAL (not gated on <see cref="CombatEngagement.NarrateToLog"/>) so the report works
    /// regardless of the console-log flag — it's a cheap struct append under a lock. Thread-safe because combat
    /// ticks run per-system in parallel (<c>MasterTimePulse</c> Task.Parallel). Capped to the most recent
    /// <see cref="MaxEvents"/> so it never grows without bound. Runtime-only (not part of save/load): it's a
    /// "recent battles" readout, not persistent game state.
    /// </summary>
    public static class BattleLog
    {
        /// <summary>Keep at most this many of the most-recent events (ring-buffer cap).</summary>
        public const int MaxEvents = 250;

        private static readonly List<BattleEvent> _events = new List<BattleEvent>();
        private static readonly object _lock = new object();

        /// <summary>Append one event, trimming the oldest if over the cap. Thread-safe.</summary>
        public static void Record(BattleEvent e)
        {
            lock (_lock)
            {
                _events.Add(e);
                if (_events.Count > MaxEvents)
                    _events.RemoveRange(0, _events.Count - MaxEvents);
            }
        }

        /// <summary>A snapshot copy of the recorded events, oldest → newest. Safe to read/iterate on any thread
        /// (returns a fresh array, so the caller never holds the lock or sees a mid-write list).</summary>
        public static IReadOnlyList<BattleEvent> Recent()
        {
            lock (_lock) { return _events.ToArray(); }
        }

        /// <summary>How many events are currently held.</summary>
        public static int Count
        {
            get { lock (_lock) { return _events.Count; } }
        }

        /// <summary>Drop all recorded events (new game / test reset).</summary>
        public static void Clear()
        {
            lock (_lock) { _events.Clear(); }
        }
    }
}
