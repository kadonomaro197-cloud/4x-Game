using System;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Names;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The AI INSPECTOR — the live view of the AI flight recorder (docs/ai/DEVTEST-CONQUEST-SANDBOX-DESIGN.md §4, the
    /// observability SPINE). Toolbar window (alongside Diplomacy / Intelligence). For every NPC faction it shows the
    /// current objective + WHY (from <see cref="StrategicObjectiveDB"/> via <see cref="PlanReadout.Faction"/>) and the
    /// scrolling DECISION TAPE — each past cycle's <see cref="AIDecisionRecord"/> rendered as a line (what it SENSED,
    /// DECIDED, and ACTED on). It reads the SAME per-faction <see cref="AIDecisionRecordDB"/> the client flushes to
    /// <c>game_logs/</c> as <c>[AI]</c> lines — the log tape is primary (reviewable anywhere), this is the convenience
    /// view. Pair with DevTools "View as faction" to see the map through its fog AND its mind.
    ///
    /// A thin, read-only, defensive DRAW (mirrors <see cref="DiplomacyWindow"/>): all values are CI-tested engine reads,
    /// the body is wrapped so a throw can't skip <c>Window.End()</c>, nothing is hard-indexed.
    /// </summary>
    public class AIInspectorWindow : PulsarGuiWindow
    {
        private bool _errorLogged;

        private AIInspectorWindow()
        {
            _flags = ImGuiWindowFlags.None;
        }

        internal static AIInspectorWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(AIInspectorWindow)))
                return new AIInspectorWindow();
            return (AIInspectorWindow)_uiState.LoadedWindows[typeof(AIInspectorWindow)];
        }

        internal override void Display()
        {
            if (!IsActive) return;

            ImGui.SetNextWindowSize(new Vector2(640, 460), ImGuiCond.FirstUseEver);
            if (Window.Begin("AI Inspector", ref IsActive))
            {
                try { RenderBody(); }
                catch (Exception e)
                {
                    ImGui.TextUnformatted("AI Inspector hit an error (logged).");
                    if (!_errorLogged)
                    {
                        Console.WriteLine("[RenderError] AIInspectorWindow body threw (logged once): " + e);
                        Console.Out.Flush();
                        _errorLogged = true;
                    }
                }
            }
            Window.End();
        }

        private void RenderBody()
        {
            var game = _uiState.Game;
            if (game == null) { ImGui.TextUnformatted("No game loaded."); return; }

            ImGui.TextDisabled("What each AI faction sees, decides, and does — its brain-tape. Same data as the [AI] lines in game_logs/.");
            ImGui.Separator();

            int shown = 0;
            foreach (var kvp in game.Factions)
            {
                var faction = kvp.Value;
                if (faction == null || !faction.IsValid) continue;
                if (!faction.TryGetDataBlob<FactionInfoDB>(out var info) || !info.IsNPC) continue;

                shown++;
                string name = faction.TryGetDataBlob<NameDB>(out var fn) ? fn.OwnersName : ("faction#" + faction.Id);

                if (ImGui.CollapsingHeader(name + "###ai" + faction.Id, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // The current objective + why (one line, the settled decision).
                    string current = PlanReadout.Faction(faction);
                    ImGui.TextWrapped(string.IsNullOrEmpty(current) ? "  (no objective settled yet)" : "  " + current);

                    // The decision tape — the scrolling history, newest at the bottom (matches the log order).
                    // EndChild is unconditional (the codebase convention, e.g. AdminWindow) — it must always run.
                    // SNAPSHOT the tape before iterating: the sim thread appends to AIDecisionRecordDB.Records on the
                    // parallel decision tick while we draw, so a live `foreach` over it throws "Collection was modified".
                    // Worse, that throw lands INSIDE the BeginChild block below, skipping EndChild → the ImGui child
                    // stack goes unbalanced and corrupts EVERY window drawn after this one (the cascade seen in the
                    // playtest logs). ToArray() once = a stable copy that can't throw, so EndChild always runs.
                    AIDecisionRecord[] records = null;
                    if (faction.TryGetDataBlob<AIDecisionRecordDB>(out var tape))
                    {
                        try { records = tape.Records.ToArray(); }
                        catch { records = null; }   // ring buffer mutated mid-copy on a bad frame — skip it, never throw
                    }

                    if (records != null && records.Length > 0)
                    {
                        if (ImGui.BeginChild("###aitape" + faction.Id, new Vector2(0, 150), ImGuiChildFlags.Borders))
                        {
                            foreach (var rec in records)
                                ImGui.TextUnformatted(PlanReadout.DecisionLine(rec, name));
                            // Keep the view pinned to the newest line as the tape grows.
                            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
                                ImGui.SetScrollHereY(1.0f);
                        }
                        ImGui.EndChild();
                    }
                    else
                    {
                        ImGui.TextDisabled("  (no decisions taped yet — advance the clock a month)");
                    }
                }
            }

            if (shown == 0)
                ImGui.TextDisabled("No AI factions in this game.");
        }
    }
}
