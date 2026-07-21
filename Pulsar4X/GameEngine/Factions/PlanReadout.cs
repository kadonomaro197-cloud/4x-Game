using Pulsar4X.Engine;
using Pulsar4X.Extensions;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// P1 Visibility Gate (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the plan/queue READOUT that makes the NPC brain's
    /// reasoning OBSERVABLE. Every planner failure is otherwise silent — a stalled NPC with no on-screen cue reads as
    /// broken even when it's correctly stuck on a prerequisite. This surfaces what the brain decided and why. Pure,
    /// side-effect-free, missing-blob-tolerant (the <c>SocietyReadout</c> convention); an engine test AND the client's
    /// "Dump Plan" both read it.
    /// </summary>
    public static class PlanReadout
    {
        /// <summary>
        /// One line for a faction's current plan: its objective/tier, WHY it chose that (the Phase-5.2 decision-log
        /// reason tracing to the driving input), and the last step the planner emitted (or why it's idle). Empty
        /// string for a faction with no <see cref="FactionInfoDB"/>. Example:
        /// <c>Directorate: obj GrowEconomy/Thrive | why: Thrive tier: Economic 0.40 leads growth → GrowEconomy | last: QueueMine — build a Mine on colony 42 to feed stalled 'iron'</c>.
        /// </summary>
        public static string Faction(Entity faction)
        {
            if (faction == null || !faction.TryGetDataBlob<FactionInfoDB>(out _)) return "";
            string name = SafeName(faction);

            if (!faction.TryGetDataBlob<StrategicObjectiveDB>(out var obj))
                return $"{name}: no strategic objective settled";

            string last = string.IsNullOrEmpty(obj.LastActionKind)
                ? "—"
                : (string.IsNullOrEmpty(obj.LastActionDetail) ? obj.LastActionKind : $"{obj.LastActionKind}: {obj.LastActionDetail}");

            string why = string.IsNullOrEmpty(obj.DecisionReason) ? "—" : obj.DecisionReason;

            return $"{name}: obj {obj.Objective}/{obj.Tier} | why: {why} | last: {last}";
        }

        /// <summary>
        /// The AI flight recorder TAPE (docs/ai/DEVTEST-CONQUEST-SANDBOX-DESIGN.md §4) — the last <paramref name="maxLines"/>
        /// decisions a faction made, newest last, one <c>[AI]</c> line each. Reads the per-faction
        /// <see cref="AIDecisionRecordDB"/>. Empty string for a faction with no tape (a player, or an NPC that hasn't
        /// ticked yet). The client flushes these to the rolling <c>game_logs/</c> pages and shows them in the Inspector —
        /// the SAME data on both surfaces. Pure, missing-blob-tolerant.
        /// </summary>
        public static string DecisionTape(Entity faction, int maxLines = 12)
        {
            if (faction == null || !faction.TryGetDataBlob<AIDecisionRecordDB>(out var tape) || tape.Records.Count == 0)
                return "";

            string name = SafeName(faction);
            var sb = new System.Text.StringBuilder();
            int start = System.Math.Max(0, tape.Records.Count - System.Math.Max(1, maxLines));
            for (int i = start; i < tape.Records.Count; i++)
                sb.AppendLine(DecisionLine(tape.Records[i], name));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Format one <see cref="AIDecisionRecord"/> as a single readable <c>[AI]</c> line: WHEN, WHO, the objective
        /// it DECIDED and why, what it ACTED on, and the fog-limited picture it SENSED — so a decision is reviewable
        /// standalone ("it chose Defend because it saw a threat 2× its strength").
        /// </summary>
        public static string DecisionLine(AIDecisionRecord r, string factionName)
        {
            if (r == null) return "";
            string why = string.IsNullOrEmpty(r.Reason) ? "—" : r.Reason;
            string act = string.IsNullOrEmpty(r.ActionDetail) ? r.ActionKind : $"{r.ActionKind}: {r.ActionDetail}";
            string threat = r.ThreatFactionId >= 0
                ? $"threat #{r.ThreatFactionId} {r.ThreatStrength:0.#}"
                : "no threat";
            return $"[AI] {r.When:yyyy-MM-dd} {factionName}: {r.Objective}/{r.Tier} — why: {why} | act: {act} "
                 + $"| saw: str {r.OwnStrength:0.#} vs {threat}, morale {r.Morale:0}, legit {r.Legitimacy:0}, "
                 + $"bal {r.Balance:0}, colonies {r.ColonyCount}, stations {r.StationCount}, contacts {r.Contacts}";
        }

        private static string SafeName(Entity faction)
        {
            try { return faction.GetName(faction.Id); } catch { return "a faction"; }
        }
    }
}
