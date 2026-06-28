using System.Collections.Generic;

namespace Pulsar4X.Blueprints;

public class SystemBlueprint : Blueprint
{
    public struct SurveyRingValue
    {
        public uint RingRadiusInAU { get; set; }
        public uint Count { get; set; }
    }

    /// <summary>
    /// Declares a ring of procedurally-scattered asteroids to fill a band of the system
    /// (e.g. Sol's main belt between Mars and Jupiter, or the Kuiper belt past Neptune).
    /// The named, hand-entered asteroids in <see cref="Bodies"/> are the big real ones;
    /// this scatters many small ones around them so the band actually looks populated.
    /// </summary>
    public struct AsteroidBeltValue
    {
        public string? Name { get; set; }
        public double InnerRadiusInAU { get; set; }
        public double OuterRadiusInAU { get; set; }
        public int Count { get; set; }
    }

    public string Name { get; set; }
    public int? Seed { get; set; }
    public List<string> Stars { get; set; }
    public List<string> Bodies { get; set; }
    public List<SurveyRingValue> SurveyRings { get; set; }
    public List<AsteroidBeltValue>? AsteroidBelts { get; set; }
}