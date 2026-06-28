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

    /// <summary>One typed effect of a JSON-authored hazard. <see cref="Type"/> is a <c>HazardEffectType</c> name
    /// ("SensorJam", "HeatDamage", "MovementDrag", "WarpInhibit", …).</summary>
    public struct HazardEffectValue
    {
        public string Type { get; set; }
        public double Magnitude { get; set; }
        public double Wavelength_nm { get; set; }
        public bool ScalesWithProximity { get; set; }
    }

    /// <summary>A JSON-authored space hazard (gas cloud, nebula, radiation belt, …). Placed at a polar offset
    /// (<see cref="DistanceInAU"/>, <see cref="AngleInDegrees"/>) from the primary star, of <see cref="RadiusInAU"/>,
    /// with a list of typed <see cref="Effects"/>. <see cref="Type"/> is a <c>SpaceHazardType</c> name (for map
    /// colour); it defaults to "Generic".</summary>
    public struct HazardValue
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public double DistanceInAU { get; set; }
        public double AngleInDegrees { get; set; }
        public double RadiusInAU { get; set; }
        public List<HazardEffectValue> Effects { get; set; }
    }

    public string Name { get; set; }
    public int? Seed { get; set; }
    public List<string> Stars { get; set; }
    public List<string> Bodies { get; set; }
    public List<SurveyRingValue> SurveyRings { get; set; }
    public List<AsteroidBeltValue>? AsteroidBelts { get; set; }
    public List<HazardValue>? Hazards { get; set; }
}