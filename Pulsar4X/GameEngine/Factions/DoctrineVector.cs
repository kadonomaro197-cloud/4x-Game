using Newtonsoft.Json;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Weights describing an NPC faction's strategic priorities.
    /// Values are unitless and relative — a faction acts on the highest-weight goal
    /// each decision cycle. They do not need to sum to 1.
    /// </summary>
    public struct DoctrineVector
    {
        [JsonProperty]
        public float Economic;    // weight toward industry and colony growth

        [JsonProperty]
        public float Military;    // weight toward fleet building and weapons

        [JsonProperty]
        public float Tech;        // weight toward research

        [JsonProperty]
        public float Expansion;   // weight toward colonizing new systems
    }
}
