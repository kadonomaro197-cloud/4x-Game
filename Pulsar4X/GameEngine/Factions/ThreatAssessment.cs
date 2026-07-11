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
    }
}
