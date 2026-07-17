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
        /// <summary>The region's HEX patch (Planet → Region → Hex) — the fine tiles terrain/units live on. Empty until
        /// <see cref="PlanetHexFactory"/> generates them LAZILY (only when the body becomes a theatre). Save-safe.</summary>
        [JsonProperty] public List<GroundHex> Hexes { get; internal set; } = new List<GroundHex>();
        /// <summary>Entity ids of installations placed in this region (populated by the build-at-a-region slice).</summary>
        [JsonProperty] public List<int> InstallationIds { get; internal set; } = new List<int>();
        /// <summary>Which faction holds this region on the ground (-1 = unowned/uncontested). Ground combat flips it
        /// when a garrison is cleared (slice 5d); when every region flips to one invader the planet's colony is taken.</summary>
        [JsonProperty] public int OwnerFactionID { get; internal set; } = -1;

        public Region() { }
        public Region(Region other)
        {
            Index = other.Index;
            Area_km2 = other.Area_km2;
            CrossingTimeSeconds = other.CrossingTimeSeconds;
            Surveyed = other.Surveyed;
            OwnerFactionID = other.OwnerFactionID;
            Neighbors = new List<int>(other.Neighbors);
            InstallationIds = new List<int>(other.InstallationIds);
            Features = new List<RegionFeature>();
            foreach (var f in other.Features) Features.Add(new RegionFeature(f));
            Hexes = new List<GroundHex>();
            foreach (var h in other.Hexes) Hexes.Add(new GroundHex(h));
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

        /// <summary>The ONE continuous cylinder hex grid (G-track) — null until generated (lazy). Regions are column
        /// BANDS over this. Additive alongside the per-region <c>Region.Hexes</c> disks during the migration; the disks
        /// are retired once consumers ride the grid (docs/GLOBAL-HEX-GRID-DESIGN.md, G6).</summary>
        [JsonProperty] public SurfaceGrid SurfaceGrid { get; internal set; }

        /// <summary>PER-FACTION region fog — the foundation from `docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md` (path A,
        /// slice 1). Which region indices each faction has revealed (scouted / space-surveyed). The world-level
        /// <see cref="Region.Surveyed"/> flag above STAYS for backward-compat; this is the ADDITIVE per-faction layer
        /// consumers switch to (so an NPC's survey doesn't reveal a world to YOU, and the honest landing score reads only
        /// what the attacker has scouted). Mirrors <c>GeoSurveyableDB.GeoSurveyStatus</c> (body-level, per-faction) at
        /// region granularity. factionId → the set of revealed region indices; an absent faction has revealed nothing
        /// (full fog by default). Nothing consumes this yet — later slices wire the survey + the landing intel.</summary>
        [JsonProperty] public Dictionary<int, HashSet<int>> PerFactionRevealed { get; internal set; } = new Dictionary<int, HashSet<int>>();

        public PlanetRegionsDB() { }
        public PlanetRegionsDB(List<Region> regions) { Regions = regions; }
        public PlanetRegionsDB(PlanetRegionsDB other)
        {
            Regions = new List<Region>();
            foreach (var r in other.Regions) Regions.Add(new Region(r));
            SurfaceGrid = other.SurfaceGrid != null ? new SurfaceGrid(other.SurfaceGrid) : null;
            PerFactionRevealed = new Dictionary<int, HashSet<int>>();
            if (other.PerFactionRevealed != null)
                foreach (var kv in other.PerFactionRevealed)
                    PerFactionRevealed[kv.Key] = new HashSet<int>(kv.Value);   // deep copy — an independent per-faction set
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

        /// <summary>Reveal ONE region by index (the per-region counterpart to <see cref="RevealAll"/>): a ground radar
        /// reveals the region(s) within its reach one at a time. Returns true if newly revealed (idempotent +
        /// bounds-safe — an out-of-range or already-surveyed index is a no-op).</summary>
        public bool RevealRegion(int index)
        {
            if (index < 0 || index >= Regions.Count) return false;
            if (Regions[index].Surveyed) return false;
            Regions[index].Surveyed = true;
            return true;
        }

        // ── PER-FACTION reveal (ground fog, slice 1) — additive; the world-level Surveyed flag above is untouched ──

        /// <summary>Reveal region <paramref name="index"/> to <paramref name="factionId"/> only (a scout enters it, or a
        /// space survey reveals the world's geography to the SURVEYING faction). Returns true if newly revealed
        /// (idempotent + bounds-safe — out-of-range is a no-op). Does NOT flip <see cref="Region.Surveyed"/>.</summary>
        public bool RevealRegionFor(int factionId, int index)
        {
            if (index < 0 || index >= Regions.Count) return false;
            if (!PerFactionRevealed.TryGetValue(factionId, out var set))
            {
                set = new HashSet<int>();
                PerFactionRevealed[factionId] = set;
            }
            return set.Add(index);
        }

        /// <summary>Has <paramref name="factionId"/> revealed region <paramref name="index"/>? False for an unknown
        /// faction or an un-scouted region (full fog by default). Bounds-agnostic (a bad index just reads false).</summary>
        public bool IsRegionRevealedFor(int factionId, int index)
        {
            return PerFactionRevealed.TryGetValue(factionId, out var set) && set.Contains(index);
        }

        /// <summary>PER-FACTION counterpart to <see cref="RevealAll"/>: reveal EVERY region to one faction (a completed
        /// space survey reveals the whole world's geography — to the surveying faction ONLY). Returns true if anything
        /// was newly revealed.</summary>
        public bool RevealAllRegionsFor(int factionId)
        {
            if (!PerFactionRevealed.TryGetValue(factionId, out var set))
            {
                set = new HashSet<int>();
                PerFactionRevealed[factionId] = set;
            }
            bool changed = false;
            for (int i = 0; i < Regions.Count; i++)
                changed |= set.Add(i);
            return changed;
        }

        public new static List<Type> GetDependencies() => new List<Type>() { typeof(NameDB) };
    }
}
