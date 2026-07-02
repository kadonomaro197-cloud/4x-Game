using System;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Combat;

namespace Pulsar4X.Client
{
    /// <summary>
    /// A persistent readout of recent BATTLES — the "window into the engine room" for space combat, which is
    /// otherwise invisible auto-resolve math (no beams/explosions on the map; a lopsided fight is over in a blink).
    /// It reads the engine's <see cref="BattleLog"/> — a capped history of battle state-changes that SURVIVES after
    /// a fight ends, unlike the live <see cref="FleetCombatStateDB"/> which is removed on disengage — so the player
    /// can review a battle they blinked and missed: who engaged, each salvo's losses, who retreated, who was wiped.
    ///
    /// Opened from DevTools ("Open Battle Report"). A plain list/table window; mirrors the standard PulsarGuiWindow
    /// pattern (GetInstance factory + Display). Reads only — never mutates game state.
    /// </summary>
    public class BattleReportWindow : PulsarGuiWindow
    {
        private bool _newestFirst = true;

        private BattleReportWindow()
        {
            _flags = ImGuiWindowFlags.None;
        }

        internal static BattleReportWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(BattleReportWindow)))
                return new BattleReportWindow();
            return (BattleReportWindow)_uiState.LoadedWindows[typeof(BattleReportWindow)];
        }

        internal override void Display()
        {
            if (!IsActive) return;

            // ImGui rule: End() MUST be called for every Begin(), even when Begin() returns false (window
            // collapsed/clipped). The old code called ImGui.End() only INSIDE the if(Begin) block (and via an
            // early return), so a COLLAPSED Battle Report window skipped End() -> "Missing End()" error spam +
            // an unbalanced ImGui window stack. This window AUTO-OPENS on the combat interrupt, so that
            // corruption landed on an already-heavy combat frame. End() is now unconditional; the empty case is
            // an else (no early return); EndTable stays inside its own if (BeginTable is the "End only if true" kind).
            if (ImGui.Begin("Battle Report", ref IsActive, _flags))
            {
                var events = BattleLog.Recent();   // snapshot copy, oldest -> newest; safe on this thread
                ImGui.TextDisabled($"{events.Count} recent battle event(s). Space combat is auto-resolved — this is the play-by-play.");
                ImGui.Checkbox("Newest first", ref _newestFirst);
                ImGui.SameLine();
                if (ImGui.Button("Clear")) BattleLog.Clear();
                ImGui.Separator();

                if (events.Count == 0)
                {
                    ImGui.TextDisabled("No battles recorded yet. Spawn a hostile fleet (DevTools) and bring yours into range,");
                    ImGui.TextDisabled("then press play — a fight will show up here after it resolves.");
                }
                else
                {
                    var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
                    if (ImGui.BeginTable("battlelog", 5, tableFlags))
                    {
                        ImGui.TableSetupColumn("Game time");
                        ImGui.TableSetupColumn("Fleet");
                        ImGui.TableSetupColumn("Event");
                        ImGui.TableSetupColumn("Lost / Left");
                        ImGui.TableSetupColumn("Detail");
                        ImGui.TableHeadersRow();

                        int n = events.Count;
                        for (int i = 0; i < n; i++)
                        {
                            var e = _newestFirst ? events[n - 1 - i] : events[i];
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(e.When.ToString("yyyy-MM-dd HH:mm"));
                            ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted($"{e.FleetName} #{e.FleetId}");
                            ImGui.TableSetColumnIndex(2); ImGui.TextColored(ColorFor(e.Type), LabelFor(e));
                            ImGui.TableSetColumnIndex(3);
                            ImGui.TextUnformatted(e.Type == BattleEventType.Salvo ? $"{e.ShipsLost} / {e.ShipsLeft}" : $"- / {e.ShipsLeft}");
                            ImGui.TableSetColumnIndex(4); ImGui.TextUnformatted(e.Note ?? "");
                        }
                        ImGui.EndTable();
                    }
                }
            }
            ImGui.End();
        }

        private static string LabelFor(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Engaged:    return "engaged";
                case BattleEventType.Salvo:      return "salvo " + e.Step;
                case BattleEventType.Retreat:    return "retreats";
                case BattleEventType.Disengaged: return "disengaged";
                default:                         return e.Type.ToString();
            }
        }

        // Amber = a fight began; red = losses; blue = broke off; grey = left the fight.
        private static Vector4 ColorFor(BattleEventType t)
        {
            switch (t)
            {
                case BattleEventType.Engaged:    return new Vector4(1f, 0.85f, 0.4f, 1f);
                case BattleEventType.Salvo:      return new Vector4(1f, 0.45f, 0.45f, 1f);
                case BattleEventType.Retreat:    return new Vector4(0.6f, 0.8f, 1f, 1f);
                case BattleEventType.Disengaged: return new Vector4(0.7f, 0.7f, 0.7f, 1f);
                default:                         return new Vector4(1f, 1f, 1f, 1f);
            }
        }
    }
}
