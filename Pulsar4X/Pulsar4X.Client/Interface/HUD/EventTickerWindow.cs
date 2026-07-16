using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Events;
using Pulsar4X.Datablobs;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The Event Logger — a compact strip at the top-centre of the screen. Collapsed it shows the last 5 events in
    /// order; click the "Events" header to expand it downward for more. It reads the PLAYER faction's event log — the
    /// same log `SensorScan` writes "Enemy Fleet detected at [body]" into. That event HALTS the clock and (engine
    /// slice 1) resets the fast-forward step back to 1-hour steps, so the player can react instead of blasting past
    /// the danger. This window also OPTS the player's log into halting on `NewHostileContact` the first time it
    /// initialises (the halt set is per-faction and starts empty). Mirrors `EntityFilterBar` (a borderless top HUD).
    /// </summary>
    public class EventTickerWindow : PulsarGuiWindow
    {
        private IEventLog _eventLog;
        private bool _expanded;
        private const int CollapsedCount = 5;
        private const int ExpandedCount = 20;

        private EventTickerWindow()
        {
            _flags = ImGuiWindowFlags.NoTitleBar
                   | ImGuiWindowFlags.NoResize
                   | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoFocusOnAppearing
                   | ImGuiWindowFlags.AlwaysAutoResize;
            InitLog();
            _uiState.OnFactionChanged += _ => InitLog();
        }

        private void InitLog()
        {
            _eventLog = null;
            if (_uiState.Faction != null && _uiState.Faction.TryGetDataBlob<FactionInfoDB>(out var fi))
                _eventLog = fi.EventLog;

            // Opt the player's log into pausing on an enemy-fleet detection (once — ToggleHaltsOn flips the flag, so
            // guard against re-toggling it back off on a faction-change re-init).
            if (_eventLog != null && !_eventLog.HaltsOn(EventType.NewHostileContact))
                _eventLog.ToggleHaltsOn(EventType.NewHostileContact);
        }

        internal static EventTickerWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(EventTickerWindow)))
                return new EventTickerWindow();
            return (EventTickerWindow)_uiState.LoadedWindows[typeof(EventTickerWindow)];
        }

        internal override void Display()
        {
            if (!IsActive || _uiState.Faction == null) return;
            if (_eventLog == null) { InitLog(); if (_eventLog == null) return; }

            var events = _eventLog.GetEvents();
            int total = events.Count;

            const float width = 560f;
            float x = (ImGui.GetMainViewport().WorkSize.X - width) * 0.5f;
            ImGui.SetNextWindowPos(new Vector2(x < 0 ? 0 : x, 2));
            ImGui.SetNextWindowSize(new Vector2(width, 0));   // 0 height = auto-fit the rows shown
            ImGui.SetNextWindowBgAlpha(0.85f);

            if (Window.Begin("###event-ticker", _flags))
            {
                if (ImGui.SmallButton(_expanded ? "v Events" : "> Events"))
                    _expanded = !_expanded;
                ImGui.SameLine();
                ImGui.TextDisabled($"({total})");

                if (total == 0)
                {
                    ImGui.TextDisabled("(no events)");
                }
                else
                {
                    int show = _expanded ? ExpandedCount : CollapsedCount;
                    int start = total > show ? total - show : 0;   // the last `show` events, oldest→newest
                    for (int i = start; i < total; i++)
                    {
                        var e = events[i];
                        ImGui.TextWrapped($"{e.StarDate:yyyy-MM-dd HH:mm}  {e.Message}");
                    }
                }
            }
            Window.End();
        }
    }
}
