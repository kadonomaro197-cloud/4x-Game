using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E1 — the CAPACITY SEAT of a faction's spy network, the ability blob an
    /// <see cref="IntelDirectorateAtb"/> installs on a colony (the geo-survey/research-academy ability-blob pattern).
    /// It is the "build the gear" rung of the espionage cradle-to-grave: an Intelligence Directorate installation
    /// projects two numbers onto its host colony —
    ///   • <see cref="OpCapacity"/> — how many concurrent covert operations the faction can run (the scarce budget
    ///     the covert-op processor will spend, E3), and
    ///   • <see cref="CounterIntelRating"/> — the faction's defensive strength against enemy agents on its soil (the
    ///     shield the detection roll reads when the mirror runs ops against you, E5).
    /// Both are ADDITIVE across every installed directorate (two directorates → summed capacity/rating), so building
    /// more spy infrastructure literally buys more spy capacity — and destroying it (the grave rung) takes it away.
    /// This slice is the seat + the numbers only; nothing spends the capacity yet, so it is byte-identical / additive
    /// (no colony carries the blob until a directorate is built).
    /// </summary>
    public class IntelDirectorateDB : BaseDataBlob
    {
        /// <summary>Concurrent covert operations this colony's directorates support (summed across installs).</summary>
        [JsonProperty] public int OpCapacity { get; set; }

        /// <summary>Counter-intelligence rating against enemy agents (summed across installs) — the defensive shield.</summary>
        [JsonProperty] public int CounterIntelRating { get; set; }

        public IntelDirectorateDB() { }

        public IntelDirectorateDB(IntelDirectorateDB other)
        {
            OpCapacity = other.OpCapacity;
            CounterIntelRating = other.CounterIntelRating;
        }

        public override object Clone() => new IntelDirectorateDB(this);
    }
}
