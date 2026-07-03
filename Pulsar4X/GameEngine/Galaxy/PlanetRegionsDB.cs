using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Names;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The kinds of geography a region can contain. A region is NOT one of these — it holds a BUNDLE of them
    /// (a region can be mountains + forest + coast at once). New feature types are added here; nothing hard-codes
    /// the count. Temperature-driven biomes (ice/desert by climate) are a documented refinement — v1 drives
    /// features off the reliable inputs (hydrosphere, tectonics, body type).
    /// </summary>
    public enum RegionFeatureType : byte
    {
        Unknown, Ocean, Coast, Wetland, Plains, Forest, Jungle, Desert, Barren,
        Highlands, Mountains, Volcanic, Tundra, Ice, GasLayers
    }

    /// <summary>
    /// One geographic feature inside a region — its kind and how much of the region it covers (0..1).
    /// Plain serializable value object (parameterless ctor for load; copy ctor for the region deep-clone).
    /// </summary>
    public class RegionFeature
    {
        [JsonProperty] public RegionFeatureType Type { get; internal set; }
        /// <summary>Fraction of the region this feature covers, 0..1.</summary>
        [JsonProperty] public double Coverage { get; internal set; }

        public RegionFeature() { }
        public RegionFeature(RegionFeatureType type, double coverage) { Type = type; Coverage = coverage; }
        public RegionFeature(RegionFeature other) { Type = other.Type; Coverage = other.Coverage; }
    }

    /// <summary>
    /// One region of a planet's surface — a "place" you can build at and (later) march units between. v1: one of
    /// four longitude slices in a ring. Carries its own AREA (so the map can be honest about true size), a
    /// CROSSING TIME (the distance datum movement will read — great-circle-honest, not flat-map pixels), the
    /// ring ADJACENCY, a SURVEYED flag (features are only "known" once scanned), the FEATURE bundle, and the ids
    /// of installations placed here (populated by later slices).
    /// </summary>
    public class Region
    {
        [JsonProperty] public int Index { get; internal set; }
        /// <summary>Real surface area of this region in km² — the "Africa is actually huge" datum.</summary>
        [JsonProperty] public double Area_km2 { get; internal set; }
        /// <summary>Time (game-seconds) to traverse this region end-to-end — the distance datum for movement.</summary>
        [JsonProperty] public double CrossingTimeSeconds { get; internal set; }
        /// <summary>Has this region been surveyed? Its features are only "known" once true. Authored worlds
        /// (Earth) start surveyed; procedurally-generated worlds start UNKNOWN until scanned.</summary>
        [JsonProperty] public bool Surveyed { get; internal set; }
        /// <summary>Indices of adjacent regions. v1 is a 4-slice RING, so each region has an east and a west neighbour.</summary>
        [JsonProperty] public List<int> Neighbors { get; internal set; } = new List<int>();
        /// <summary>The geography of this region — a bundle of features, not a single type.</summary>
        [JsonProperty] public List<RegionFeature> Features { get; internal set; } = new List<RegionFeature>();
        /// <summary>Entity ids of installations placed in this region (populated by the build-at-a-region slice).</summary>
        [JsonProperty] public List<int> InstallationIds { get; internal set; } = new List<int>();

        public Region() { }
        public Region(Region other)
        {
            Index = other.Index;
            Area_km2 = other.Area_km2;
            CrossingTimeSeconds = other.CrossingTimeSeconds;
            Surveyed = other.Surveyed;
            Neighbors = new List<int>(other.Neighbors);
            InstallationIds = new List<int>(other.InstallationIds);
            Features = new List<RegionFeature>();
            foreach (var f in other.Features) Features.Add(new RegionFeature(f));
        }
    }

    /// <summary>
    /// A planet's surface as a set of REGIONS — the strategic ground-map layer. Attached to the PLANET body
    /// entity (the parallel to how a colony/station is a host: the regions are OF the planet and persist whether
    /// or not anyone has colonised it, so oceans and empty continents exist to fight over). Generated at
    /// system-gen by <see cref="PlanetRegionsFactory"/>. v1: four longitude slices in a ring (topology-correct —
    /// no seam, so the "Pacific theatre" survives), each carrying a feature bundle discovered by exploration.
    /// Fully persistent (<see cref="Clone"/> + [JsonProperty]) from day one — the flaw that killed the earlier
    /// colony hex map was that it could not survive a save; we do not repeat it.
    ///
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public class PlanetRegionsDB : BaseDataBlob
    {
        [JsonProperty] public List<Region> Regions { get; internal set; } = new List<Region>();

        public PlanetRegionsDB() { }
        public PlanetRegionsDB(List<Region> regions) { Regions = regions; }
        public PlanetRegionsDB(PlanetRegionsDB other)
        {
            Regions = new List<Region>();
            foreach (var r in other.Regions) Regions.Add(new Region(r));
        }

        public override object Clone() => new PlanetRegionsDB(this);

        /// <summary>
        /// Reveal every region — its geography is now KNOWN. Called when a geological survey of this body
        /// completes (the exploration→map link, slice 4). Returns true if anything actually changed, so a caller
        /// can skip a no-op (a re-survey of an already-known world).
        ///
        /// v1 reveal is WORLD-LEVEL and faction-agnostic: a single <see cref="Region.Surveyed"/> bool per region,
        /// matching the design's "scanning a world reveals its geography." Per-faction region fog (so an NPC's
        /// survey doesn't reveal the world to YOU) is a documented refinement — the same pattern the mineral
        /// partial-access mask already uses.
        /// </summary>
        public bool RevealAll()
        {
            bool changed = false;
            foreach (var region in Regions)
            {
                if (!region.Surveyed)
                {
                    region.Surveyed = true;
                    changed = true;
                }
            }
            return changed;
        }

        public new static List<Type> GetDependencies() => new List<Type>() { typeof(NameDB) };
    }
}
