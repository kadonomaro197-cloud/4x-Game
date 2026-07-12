using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Names;
using Pulsar4X.People;

namespace Pulsar4X.Client
{
    /// <summary>
    /// Espionage E4 — the player-facing INTELLIGENCE window: the button to access, assign, and ready up your agents.
    /// Reachable from the toolbar (next to Diplomacy). Four thin, defensive sections over CI-tested engine state:
    ///   1. CAPACITY — your intelligence directorates' op capacity + counter-intel rating, and how many agents you field.
    ///   2. AGENTS — each operative's tradecraft skill and current status (idle / running an op with its resolve date).
    ///   3. ASSIGN — pick an idle agent + a met rival + a facet and TASK a Gather-Intel op (via Espionage.TaskAgent).
    ///   4. INTEL LEDGER — per met rival, per facet, your intel level (Inferred / Confirmed / Stale).
    /// Mirrors the DiplomacyWindow pattern exactly (GetInstance + Window.Begin/try/End so a body throw can't skip End).
    /// All writes go through the CI-tested Espionage.TaskAgent path; the client stays a thin draw (runtime CI-blind).
    /// </summary>
    public class IntelligenceWindow : PulsarGuiWindow
    {
        private bool _errorLogged;

        // Combo selections for the assign panel (clamped to the live lists each frame).
        private int _agentIdx;
        private int _rivalIdx;
        private int _facetIdx;

        private static readonly IntelFacet[] Facets = (IntelFacet[])Enum.GetValues(typeof(IntelFacet));

        private IntelligenceWindow()
        {
            _flags = ImGuiWindowFlags.None;
        }

        internal static IntelligenceWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(IntelligenceWindow)))
                return new IntelligenceWindow();
            return (IntelligenceWindow)_uiState.LoadedWindows[typeof(IntelligenceWindow)];
        }

        internal override void Display()
        {
            if (!IsActive) return;

            if (Window.Begin("Intelligence", ref IsActive))
            {
                try { RenderBody(); }
                catch (Exception e)
                {
                    ImGui.TextUnformatted("Intelligence view hit an error (logged).");
                    if (!_errorLogged)
                    {
                        Console.WriteLine("[RenderError] IntelligenceWindow body threw (logged once): " + e);
                        Console.Out.Flush();
                        _errorLogged = true;
                    }
                }
            }
            Window.End();
        }

        private void RenderBody()
        {
            var player = _uiState.PlayerFaction;
            if (player == null || !player.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
            {
                ImGui.TextDisabled("No intelligence data for your faction.");
                return;
            }

            ImGui.TextDisabled("Your spy network: build Intelligence Directorates for capacity + counter-intel, recruit agents, and task covert ops against rivals. Every active op risks being caught.");
            ImGui.Separator();

            RenderCapacity(factionInfo);
            ImGui.Separator();

            var agents = GatherAgents(factionInfo);
            RenderAgents(agents);
            ImGui.Separator();

            RenderAssignPanel(player, factionInfo, agents);
            ImGui.Separator();

            RenderIntelLedger(player);
        }

        // ---- 1. Capacity summary ----
        private void RenderCapacity(FactionInfoDB factionInfo)
        {
            int opCapacity = 0, counterIntel = 0, directorates = 0;
            foreach (var colony in factionInfo.Colonies)
                if (colony.TryGetDataBlob<IntelDirectorateDB>(out var d))
                {
                    opCapacity += d.OpCapacity;
                    counterIntel += d.CounterIntelRating;
                    directorates++;
                }

            int agentCount = CountAgents(factionInfo);

            if (directorates == 0)
            {
                ImGui.TextColored(Styles.BadColor, "No Intelligence Directorate built.");
                ImGui.TextDisabled("Build an Intelligence Directorate on a colony (Production tab) to start recruiting agents and running covert ops.");
                return;
            }

            ImGui.TextUnformatted($"Directorates: {directorates}    Op capacity: {agentCount} / {opCapacity} agents    Counter-intel: {counterIntel}");
        }

        // ---- 2. Agents ----
        private void RenderAgents(List<Entity> agents)
        {
            ImGui.TextUnformatted("Agents");
            if (agents.Count == 0)
            {
                ImGui.TextDisabled("No operatives yet — a directorate recruits them over time (up to your op capacity).");
                return;
            }

            if (ImGui.BeginTable("agents", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Operative", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableSetupColumn("Tradecraft", ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableHeadersRow();

                foreach (var agent in agents)
                {
                    string name = agent.TryGetDataBlob<NameDB>(out var nameDB) ? nameDB.OwnersName : ("Agent " + agent.Id);
                    double skill = agent.TryGetDataBlob<BonusesDB>(out var b) ? CommanderBonuses.EspionageSkill01(b) : 0.0;

                    string status = "Idle";
                    if (agent.TryGetDataBlob<CovertOpDB>(out var op))
                        status = $"{op.Action} vs {RivalName(op.TargetFactionId)} — resolves {op.ResolveOn:yyyy-MM-dd}";

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(name);
                    ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(skill.ToString("0.00"));
                    ImGui.TableSetColumnIndex(2); ImGui.TextUnformatted(status);
                }
                ImGui.EndTable();
            }
        }

        // ---- 3. Assign panel ----
        private void RenderAssignPanel(Entity player, FactionInfoDB factionInfo, List<Entity> agents)
        {
            ImGui.TextUnformatted("Task a covert operation");

            // Idle agents only (a busy agent can't be re-tasked).
            var idle = new List<Entity>();
            var idleNames = new List<string>();
            foreach (var agent in agents)
                if (!agent.HasDataBlob<CovertOpDB>())
                {
                    idle.Add(agent);
                    idleNames.Add(agent.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : ("Agent " + agent.Id));
                }

            // Met rivals (from the diplomacy ledger).
            var rivalIds = new List<int>();
            var rivalNames = new List<string>();
            if (player.TryGetDataBlob<DiplomacyDB>(out var dip))
                foreach (var rel in dip.Relationships.Values)
                {
                    rivalIds.Add(rel.OtherFactionId);
                    rivalNames.Add(RivalName(rel.OtherFactionId));
                }

            if (idle.Count == 0) { ImGui.TextDisabled("No idle operatives to task."); return; }
            if (rivalIds.Count == 0) { ImGui.TextDisabled("No rivals met yet (first contact happens when your sensors detect a foreign faction)."); return; }

            _agentIdx = Math.Clamp(_agentIdx, 0, idle.Count - 1);
            _rivalIdx = Math.Clamp(_rivalIdx, 0, rivalIds.Count - 1);
            _facetIdx = Math.Clamp(_facetIdx, 0, Facets.Length - 1);

            var facetNames = new string[Facets.Length];
            for (int i = 0; i < Facets.Length; i++) facetNames[i] = Facets[i].ToString();

            ImGui.SetNextItemWidth(200);
            ImGui.Combo("Agent###intel-agent", ref _agentIdx, idleNames.ToArray(), idleNames.Count);
            ImGui.SetNextItemWidth(200);
            ImGui.Combo("Rival###intel-rival", ref _rivalIdx, rivalNames.ToArray(), rivalNames.Count);
            ImGui.SetNextItemWidth(200);
            ImGui.Combo("Facet###intel-facet", ref _facetIdx, facetNames, facetNames.Length);

            if (ImGui.Button("Task: Gather Intel"))
            {
                var now = _uiState.Game.TimePulse.GameGlobalDateTime;
                bool ok = Espionage.TaskAgent(idle[_agentIdx], rivalIds[_rivalIdx], CovertAction.GatherIntel, Facets[_facetIdx], now);
                Console.WriteLine(ok
                    ? $"[intel] tasked agent {idleNames[_agentIdx]} to gather {facetNames[_facetIdx]} on {rivalNames[_rivalIdx]}"
                    : "[intel] task rejected (agent busy / invalid target)");
                Console.Out.Flush();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("The op takes ~90 days, then rolls detection. Caught = a soured relation + your agent is lost.");
        }

        // ---- 4. Intel ledger ----
        private void RenderIntelLedger(Entity player)
        {
            ImGui.TextUnformatted("Intel ledger — what you know about each rival");
            if (!player.TryGetDataBlob<InformationLedgerDB>(out var ledger) || ledger.Ledger.Count == 0)
            {
                ImGui.TextDisabled("No intel gathered yet. Task a Gather-Intel op to sharpen a facet from Inferred to Confirmed.");
                return;
            }

            if (ImGui.BeginTable("intelledger", 1 + Facets.Length, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Rival");
                foreach (var f in Facets) ImGui.TableSetupColumn(f.ToString());
                ImGui.TableHeadersRow();

                foreach (var rivalId in ledger.Ledger.Keys)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(RivalName(rivalId));
                    for (int i = 0; i < Facets.Length; i++)
                    {
                        var level = ledger.LevelOf(rivalId, Facets[i]);
                        ImGui.TableSetColumnIndex(1 + i);
                        ImGui.PushStyleColor(ImGuiCol.Text, LevelColor(level));
                        ImGui.TextUnformatted(level.ToString());
                        ImGui.PopStyleColor();
                    }
                }
                ImGui.EndTable();
            }
        }

        // ---- helpers ----
        private static List<Entity> GatherAgents(FactionInfoDB factionInfo)
        {
            var agents = new List<Entity>();
            foreach (var commander in factionInfo.Commanders)
                if (commander.TryGetDataBlob<CommanderDB>(out var cdb) && cdb.Type == CommanderTypes.Intelligence)
                    agents.Add(commander);
            return agents;
        }

        private static int CountAgents(FactionInfoDB factionInfo)
        {
            int count = 0;
            foreach (var commander in factionInfo.Commanders)
                if (commander.TryGetDataBlob<CommanderDB>(out var cdb) && cdb.Type == CommanderTypes.Intelligence)
                    count++;
            return count;
        }

        private static string RivalName(int factionId)
        {
            if (_uiState.Game != null && _uiState.Game.Factions.TryGetValue(factionId, out var other)
                && other.TryGetDataBlob<NameDB>(out var nameDB))
                return nameDB.OwnersName;
            return "Faction " + factionId;
        }

        private static Vector4 LevelColor(IntelLevel level) => level switch
        {
            IntelLevel.Confirmed => Styles.GoodColor,
            IntelLevel.Stale     => Styles.NeutralColor,
            _                    => Styles.BadColor   // Inferred = the fuzzy default
        };
    }
}
