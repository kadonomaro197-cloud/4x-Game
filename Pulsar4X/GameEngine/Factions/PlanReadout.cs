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

        private static string SafeName(Entity faction)
        {
            try { return faction.GetName(faction.Id); } catch { return "a faction"; }
        }
    }
}
