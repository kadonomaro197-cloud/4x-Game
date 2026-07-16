using System;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Writes one <see cref="AIDecisionRecord"/> to a faction's <see cref="AIDecisionRecordDB"/> tape each monthly
    /// Tick — the capture half of the AI flight recorder (docs/ai/DEVTEST-CONQUEST-SANDBOX-DESIGN.md §4). Called from
    /// <see cref="NPCDecisionProcessor.Tick"/> right after the objective settles (and the gated planner runs), so the
    /// record reflects the decision AND (when the order gate is on) the step taken.
    ///
    /// Reads the DECIDED half off <see cref="StrategicObjectiveDB"/> (objective/tier/reason/last-action) and the
    /// SENSED half off the existing perception helpers (<see cref="FactionRollup"/> own-side gauges +
    /// <see cref="ThreatAssessment.GreatestThreatTo"/> fog-limited enemy read). Pure observability, always-on,
    /// defensive/no-throw (it runs in a hotloop — a recorder fault must never break the sim).
    /// </summary>
    public static class AIDecisionRecorder
    {
        public static void Record(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
            try
            {
                if (factionEntity == null || factionInfoDB == null) return;

                // The DECIDED half — settled this cycle by UpdateStrategicObjective. No objective yet → nothing to tape.
                if (!factionEntity.TryGetDataBlob<StrategicObjectiveDB>(out var obj)) return;

                var rec = new AIDecisionRecord
                {
                    When         = factionEntity.Manager?.Game?.TimePulse.GameGlobalDateTime ?? DateTime.MinValue,
                    Tier         = obj.Tier,
                    Objective    = obj.Objective,
                    Reason       = obj.DecisionReason ?? "",
                    ActionKind   = string.IsNullOrEmpty(obj.LastActionKind) ? "(order gate off)" : obj.LastActionKind,
                    ActionDetail = obj.LastActionDetail ?? "",
                };

                // The SENSED half — the fog-limited picture the decision was made against.
                rec.OwnStrength = FactionRollup.MilitaryStrength(factionEntity);
                var (threatId, threatStr) = ThreatAssessment.GreatestThreatTo(factionEntity);
                rec.ThreatFactionId = threatId;
                rec.ThreatStrength  = threatStr;
                rec.Morale     = FactionRollup.MeanMorale(factionEntity);
                rec.Legitimacy = FactionRollup.MeanLegitimacy(factionEntity);
                rec.Balance    = (double)FactionRollup.Balance(factionEntity);
                rec.ColonyCount = FactionRollup.ColonyCount(factionEntity);
                rec.Contacts    = factionInfoDB.SensorContacts?.Count ?? 0;

                if (!factionEntity.TryGetDataBlob<AIDecisionRecordDB>(out var tape))
                {
                    tape = new AIDecisionRecordDB();
                    factionEntity.SetDataBlob(tape);
                }
                tape.Append(rec);
            }
            catch
            {
                // Observability must never break the sim — swallow (the tape simply misses a frame).
            }
        }
    }
}
