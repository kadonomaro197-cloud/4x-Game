using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Sensors;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-B1b (docs/AI-BRAIN-BUILD-TRACKER.md, the "eyes" foundation): a FOG-LIMITED estimate of a RIVAL's military
    /// strength — the enemy-side of the eyes (the own-side is <see cref="FactionRollup.MilitaryStrength"/>).
    ///
    /// Built on signal STRENGTH (the SignalQuality path was design-cut): it sums the loudness of the observer's LIVE
    /// sensor contacts that belong to the rival. Fog-limited by construction — only what the observer has actually
    /// DETECTED counts, so a dark, cloaked, or out-of-range fleet is invisible and you UNDER-read a hidden enemy
    /// (exactly the intended fog: going quiet hides your strength; a homeworld megasensor sees more than a picket).
    ///
    /// Stale "memory" contacts (you're coasting on a last-known position, the real entity is gone) do NOT count
    /// toward CURRENT strength — that remembered-over-time, decaying estimate is the persistent Information Ledger
    /// (F-B1c, next). This is a pure read over the existing per-faction contact track → byte-identical; 0 for a rival
    /// the observer can't currently see.
    /// </summary>
    public static class ThreatAssessment
    {
        /// <summary>
        /// The observer's fog-limited read of <paramref name="rivalFactionId"/>'s strength: the summed signal strength
        /// (kW) of the observer's live (non-memory) sensor contacts owned by that rival. 0 if it can't see any of them.
        /// </summary>
        public static double DetectedStrengthOf(Entity observer, int rivalFactionId)
        {
            if (observer == null || !observer.TryGetDataBlob<FactionInfoDB>(out var info))
                return 0;

            double total = 0;
            foreach (var kvp in info.SensorContacts)
            {
                var contact = kvp.Value;
                if (contact?.ActualEntity == null) continue;
                if (contact.PositionIsMemory) continue;                       // stale last-known → not a CURRENT read
                if (contact.ActualEntity.FactionOwnerID != rivalFactionId) continue;
                total += contact.SignalStrength_kW;                            // loudness = the fog-limited size proxy
            }
            return total;
        }

        /// <summary>
        /// Phase-3.2 (docs/AI-BRAIN-BUILD-TRACKER.md — the Ecosystem): from the rival strengths an observer can SEE,
        /// the one it should fear most — the strongest DETECTED rival whose strength EXCEEDS the observer's own (a real
        /// threat, not just any neighbour). Pure ranking (no entity graph) so it's testable without a sensor scenario;
        /// <see cref="GreatestThreatTo"/> wires the fog-limited reads in. Returns (-1, 0) if nobody out-muscles the
        /// observer. v1 is CURRENT-state ("the strongest right now"); a RISING-over-time read rides the persistent
        /// Information Ledger (F-B1c / 3.1), which this deliberately does not require.
        /// </summary>
        public static (int rivalId, double strength) PickGreatestThreat(
            IReadOnlyDictionary<int, double> detectedStrengthByRival, double ownStrength)
        {
            int worstId = -1;
            double worstStr = ownStrength;   // a rival must be STRONGER than us to count as a threat
            if (detectedStrengthByRival != null)
                foreach (var kvp in detectedStrengthByRival)
                    if (kvp.Value > worstStr) { worstStr = kvp.Value; worstId = kvp.Key; }
            return (worstId, worstId == -1 ? 0.0 : worstStr);
        }

        /// <summary>
        /// The rival the observer most fears right now: the strongest DETECTED rival stronger than the observer itself,
        /// or (-1, 0) if none. Fog-limited via <see cref="DetectedStrengthOf"/> (an undetected/dark rival can't be
        /// feared); own strength via <see cref="FactionRollup.MilitaryStrength"/>. Skips self + the neutral catch-all.
        /// Pure read → byte-identical (nothing consumes it yet; the 3.3 "propose a DefensivePact against a shared
        /// threat" sharpening does next).
        /// </summary>
        public static (int rivalId, double strength) GreatestThreatTo(Entity observer)
        {
            if (observer == null || observer.Manager?.Game == null) return (-1, 0.0);
            var game = observer.Manager.Game;
            double own = FactionRollup.MilitaryStrength(observer);

            var byRival = new Dictionary<int, double>();
            foreach (var kvp in game.Factions)
            {
                int fid = kvp.Key;
                if (fid == observer.Id || fid == Game.NeutralFactionId) continue;
                double s = DetectedStrengthOf(observer, fid);
                if (s > 0) byRival[fid] = s;
            }
            return PickGreatestThreat(byRival, own);
        }

        /// <summary>
        /// Phase-3.1 (the RISING-over-time read that <see cref="PickGreatestThreat"/> deliberately deferred): is the
        /// observer's DETECTED read of <paramref name="rivalFactionId"/> trending UP across the persistent
        /// <see cref="InformationLedgerDB"/>'s last two samples? This is what turns "the strongest RIGHT NOW" into "the
        /// one who is GROWING" — the trigger an alliance-against-a-riser should read. Pure/read-only: the samples are
        /// recorded by the monthly ledger driver, this only reads them. <c>false</c> for a null ledger or fewer than
        /// two samples (no trend yet).
        /// </summary>
        public static bool IsRising(InformationLedgerDB ledger, int rivalFactionId, IntelFacet facet = IntelFacet.Military)
        {
            return ledger != null && ledger.IsRising(rivalFactionId, facet);
        }
    }
}
