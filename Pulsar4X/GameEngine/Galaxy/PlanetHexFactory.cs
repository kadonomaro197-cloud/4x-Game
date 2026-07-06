using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates a body's HEX patches (Planet → Region → Hex) — LAZILY. The coarse <see cref="PlanetRegionsDB"/> layer
    /// (4 regions) is built for every major body at galaxy-gen; the fine hexes are built ONLY when a body becomes a
    /// theatre (colonized / garrisoned / the tactical view opened), so a galaxy never carries millions of hexes. Each
    /// region gets a hex DISK whose radius scales with the planet's size (bigger world → more hexes), and each hex is
    /// assigned a terrain drawn from that region's coarse <see cref="Region.Features"/> mix — so the fine map is a
    /// faithful realization of the coarse one. Idempotent, deterministic (system RNG), defensive. Save-safe.
    ///
    /// Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md.
    /// </summary>
    public static class PlanetHexFactory
    {
        private const double EarthRadiusM = 6.371e6;
        private const int BaseHexRadius = 12;   // Earth → a radius-12 patch per region ≈ 469 hexes ×4 ≈ 1876 total
        private const int MinHexRadius = 2;
        private const int MaxHexRadius = 24;     // bound the cost on giant worlds

        /// <summary>The hex-patch radius for one region of a body this size. Scales linearly with planet radius (so hex
        /// COUNT scales with surface AREA), clamped so a tiny moon still has a usable patch and a giant world doesn't
        /// explode. Pure — unit-testable.</summary>
        public static int HexPatchRadiusFor(double bodyRadiusM)
        {
            if (bodyRadiusM <= 0) return MinHexRadius;
            int r = (int)Math.Round(BaseHexRadius * bodyRadiusM / EarthRadiusM);
            return Math.Max(MinHexRadius, Math.Min(MaxHexRadius, r));
        }

        /// <summary>Number of hexes in a disk of the given radius (3r² + 3r + 1). Pure.</summary>
        public static int HexDiskCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        /// <summary>Generate the hex patches for a body's regions if not already done (idempotent). No-op on a body with
        /// no region layer (gas giant / non-major). Never throws. Terrain comes from a PLANET-WIDE coherent field (V2)
        /// so continents/oceans/mountain ranges span region borders instead of a per-hex random smatter.</summary>
        public static void EnsureHexesForBody(Entity body)
        {
            try
            {
                if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                    return;

                bool anyNeed = false;
                foreach (var region in regionsDB.Regions)
                    if (region.Hexes == null || region.Hexes.Count == 0) { anyNeed = true; break; }
                if (!anyNeed) return;   // idempotent — don't redraw the RNG field if every patch already exists

                double radiusM = body.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.RadiusInM : EarthRadiusM;
                int patchRadius = HexPatchRadiusFor(radiusM);
                var system = body.Manager as StarSystem;

                // Build the PLANET-WIDE terrain field ONCE (V2), driven by the body's real scalars — so terrain is a
                // coherent world (continents + coastlines that span borders), not a random smatter, for EVERY planet.
                var world = WorldTerrain.ForBody(body, system, regionsDB.Regions.Count);

                for (int i = 0; i < regionsDB.Regions.Count; i++)
                {
                    var region = regionsDB.Regions[i];
                    if (region.Hexes != null && region.Hexes.Count > 0) continue;
                    region.Hexes = BuildPatch(i, regionsDB.Regions.Count, patchRadius, world);
                }
            }
            catch { /* hex gen is a nicety — never break the game over it */ }
        }

        /// <summary>Build one region's hex disk, terrain sampled from the planet-wide field at each hex's global position.</summary>
        private static List<GroundHex> BuildPatch(int regionIndex, int regionCount, int radius, WorldTerrain world)
        {
            var hexes = new List<GroundHex>();
            for (int q = -radius; q <= radius; q++)
            {
                int rLo = Math.Max(-radius, -q - radius);
                int rHi = Math.Min(radius, -q + radius);
                for (int r = rLo; r <= rHi; r++)
                    hexes.Add(new GroundHex(q, r, world.TerrainAt(regionIndex, regionCount, q, r, radius)));
            }
            return hexes;
        }
    }

    /// <summary>
    /// The PLANET-WIDE terrain generator (V2) — one coherent world, reused for EVERY planet at system-gen. Instead of
    /// rolling each hex independently (the old "random smatter"), it samples a smooth ELEVATION + MOISTURE field at the
    /// hex's GLOBAL position on the planet, so oceans, continents, coastlines and mountain ranges form contiguously and
    /// SPAN region borders — a wet world reads as oceans+islands, a dry one as deserts, a cold one as ice/tundra.
    ///
    /// The field is a small sum-of-sines, PERIODIC in longitude so it wraps the 4-region ring seamlessly, seeded
    /// per-world from the system RNG (deterministic). Sea level, climate and relief are driven by the body's REAL
    /// scalars — hydrosphere (how much ocean), surface temperature (icy vs scorching), tectonics (how mountainous). The
    /// threshold constants are tunable "should-make-sense" dials, not physics. Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md.
    /// </summary>
    internal sealed class WorldTerrain
    {
        private const int Octaves = 5;                 // sum-of-sines detail levels (big continents + coastline wiggle)
        private const double CoastBand = 0.03;         // elevation band just above sea level rendered as Coast
        private const double HighlandThresh = 0.72;    // land elevation → highlands
        private const double MountainThresh = 0.85;    // → mountains
        private const double ColdC = -10.0;            // ≤ this surface °C → an icy world
        private const double HotC = 50.0;              // ≥ this → a scorching world
        // Cold-world lowland biome dials (tunable classification thresholds, like the temperate ones below): keep a
        // frozen world from collapsing to ONE biome. A dry cold world (Mars/Luna) is rock+dust, not tundra.
        private const double DryWorldHydro = 0.2;      // < this hydrosphere → a DRY world (rock/dust, not tundra)
        private const double ColdDryMoist = 0.5;       // dry cold lowland: > this moisture → dusty desert, else barren rock
        private const double ColdDampMoist = 0.4;      // damp cold lowland: > this moisture → tundra, else barren
        private const int MusterCoreRadius = 1;        // patch centre + its immediate ring = guaranteed passable land
        private const double TwoPi = 2.0 * Math.PI;

        private readonly bool _gas, _tectonic;
        private readonly double _seaLevel, _temp, _hydroFrac;
        private readonly double[] _ePhaseLon, _ePhaseLat, _mPhaseLon, _mPhaseLat;
        private readonly System.Func<double, double, RegionFeatureType> _authored;   // real map (Earth/Mars/…) or null = noise field

        private WorldTerrain(bool gas, double seaLevel, double temp, bool tectonic, double hydroFrac,
            double[] ePhaseLon, double[] ePhaseLat, double[] mPhaseLon, double[] mPhaseLat,
            System.Func<double, double, RegionFeatureType> authored)
        {
            _gas = gas; _seaLevel = seaLevel; _temp = temp; _tectonic = tectonic; _hydroFrac = hydroFrac;
            _ePhaseLon = ePhaseLon; _ePhaseLat = ePhaseLat; _mPhaseLon = mPhaseLon; _mPhaseLat = mPhaseLat;
            _authored = authored;
        }

        public static WorldTerrain ForBody(Entity body, StarSystem system, int regionCount)
        {
            body.TryGetDataBlob<AtmosphereDB>(out var atmo);
            body.TryGetDataBlob<SystemBodyInfoDB>(out var info);

            // The KNOWN worlds (Earth, Mars, …) sample their REAL baked map instead of the noise field, so the Sol
            // playtest bodies look like themselves. Keyed on the body's default name via RealSurfaceMaps, gated to a
            // real (non-gas) body so an oddly-named gas giant can't trip a surface map. Any other world → null → noise.
            bool gasBody = info != null && (info.BodyType == BodyType.GasGiant || info.BodyType == BodyType.GasDwarf || info.BodyType == BodyType.IceGiant);
            System.Func<double, double, RegionFeatureType> authored =
                (!gasBody && body.TryGetDataBlob<Pulsar4X.Names.NameDB>(out var nameDb))
                    ? RealSurfaceMaps.SamplerForName(nameDb.DefaultName)
                    : null;
            double hydroFrac = atmo != null ? Math.Max(0.0, Math.Min(1.0, (double)atmo.HydrosphereExtent / 100.0)) : 0.0;
            double temp = atmo != null ? atmo.SurfaceTemperature : 15.0;
            bool gas = info != null && (info.BodyType == BodyType.GasGiant || info.BodyType == BodyType.GasDwarf || info.BodyType == BodyType.IceGiant);
            bool tectonic = info != null && info.Tectonics != TectonicActivity.Dead && info.Tectonics != TectonicActivity.Unknown;

            var el = new double[Octaves]; var et = new double[Octaves]; var ml = new double[Octaves]; var mt = new double[Octaves];
            for (int k = 0; k < Octaves; k++)
            {
                el[k] = Rnd(system) * TwoPi; et[k] = Rnd(system) * TwoPi;
                ml[k] = Rnd(system) * TwoPi; mt[k] = Rnd(system) * TwoPi;
            }

            // Sea level = the elevation below which `hydroFrac` of the surface lies (the hydrosphere QUANTILE of the
            // actual elevation field). So ocean coverage TRACKS the hydrosphere — a 71%-water world is ~71% ocean —
            // instead of the old `0.12 + 0.72*hydro` linear guess, which drowned ~82% because the sum-of-sines field
            // bunches near its mean. Read from el/et (already drawn — NO extra RNG), so world regen stays deterministic.
            double seaLevel = SeaLevelForHydrosphere(hydroFrac, el, et);
            return new WorldTerrain(gas, seaLevel, temp, tectonic, hydroFrac, el, et, ml, mt, authored);
        }

        /// <summary>The sea-level elevation for a target ocean fraction: the <paramref name="hydroFrac"/>-quantile of the
        /// elevation field, sampled on a coarse grid (once per world — cheap). hydro≤0 → below the field (no ocean);
        /// hydro≥1 → above it (all ocean). Pure; reads only the already-drawn phases, so it adds no RNG draws.</summary>
        private static double SeaLevelForHydrosphere(double hydroFrac, double[] ePhaseLon, double[] ePhaseLat)
        {
            if (hydroFrac <= 0.0) return -1.0;   // dry world → nothing is below sea level → no ocean
            if (hydroFrac >= 1.0) return 2.0;    // waterworld → everything below sea level → all ocean
            const int SC = 48, SR = 24;          // coarse sample grid — enough to pin the quantile
            var samples = new double[SC * SR];
            int idx = 0;
            for (int r = 0; r < SR; r++)
            {
                double lat = r / (double)(SR - 1);
                for (int c = 0; c < SC; c++)
                    samples[idx++] = Field(c / (double)SC, lat, ePhaseLon, ePhaseLat);
            }
            System.Array.Sort(samples);
            int k = (int)(hydroFrac * samples.Length);
            if (k >= samples.Length) k = samples.Length - 1;
            return samples[k];
        }

        // ── Test seam (InternalsVisibleTo Pulsar4X.Tests) — build + sample a NOISE world from explicit scalars, no
        //    Entity scaffolding, deterministically seeded, so the generator's calibration is directly gauge-able. ──
        internal static WorldTerrain ForTest(double hydroFrac, double temp, bool tectonic, int seed)
        {
            var rng = new System.Random(seed);
            var el = new double[Octaves]; var et = new double[Octaves]; var ml = new double[Octaves]; var mt = new double[Octaves];
            for (int k = 0; k < Octaves; k++)
            {
                el[k] = rng.NextDouble() * TwoPi; et[k] = rng.NextDouble() * TwoPi;
                ml[k] = rng.NextDouble() * TwoPi; mt[k] = rng.NextDouble() * TwoPi;
            }
            return new WorldTerrain(false, SeaLevelForHydrosphere(hydroFrac, el, et), temp, tectonic, hydroFrac, el, et, ml, mt, null);
        }
        internal RegionFeatureType ClassifyForTest(double lon, double lat) => Classify(lon, lat, false);

        /// <summary>Terrain for one hex from its GLOBAL position: region+q give longitude (continuous across region
        /// borders, wrapping the ring); r gives latitude. So the field is one coherent world, not per-region.</summary>
        public RegionFeatureType TerrainAt(int region, int regionCount, int q, int r, int radius)
        {
            if (radius <= 0 || regionCount <= 0) return _gas ? RegionFeatureType.GasLayers : RegionFeatureType.Plains;

            double lon = (region + (q + radius) / (2.0 * radius)) / regionCount;   // 0..1 around the ring (wraps)
            double lat = (r + radius) / (2.0 * radius);                             // 0..1 pole → pole

            // The muster/landing CORE — the patch centre (0,0), where units are raised, plus its immediate ring — is
            // guaranteed passable land: a colony's landing zone sits on solid ground. Without this, a coherent ocean
            // world (Earth floods ~62% of the elevation range) can put OPEN WATER on the muster hex, which the
            // pathfinder treats as impassable — stranding a raised garrison with no reachable hex.
            bool inMusterCore = (Math.Abs(q) + Math.Abs(q + r) + Math.Abs(r)) / 2 <= MusterCoreRadius;
            return Classify(lon, lat, inMusterCore);
        }

        /// <summary>Terrain at a raw GLOBAL (lon, lat) on the planet — the cylinder-grid entry point (G1). Same field +
        /// climate rules as the per-region path, without the disk's muster-core land guard (the global grid guarantees
        /// land at a region BAND's centre column instead — a G3 concern). <paramref name="lon"/> wraps 0..1.</summary>
        public RegionFeatureType TerrainForLonLat(double lon, double lat) => Classify(lon, lat, false);

        /// <summary>Classify one (lon,lat) sample: sea level → ocean/coast, then CLIMATE (temp) → relief + moisture.
        /// <paramref name="musterCoreLand"/> promotes a below-sea-level sample to Coast (a beachhead) so a landing core
        /// is never impassable water. The shared core of the per-region and global-grid terrain (identical output).</summary>
        private RegionFeatureType Classify(double lon, double lat, bool musterCoreLand)
        {
            if (_gas) return RegionFeatureType.GasLayers;

            // A known world (Earth/Mars/…) samples its real baked map, not the noise field. The muster-core guard still
            // applies — an ocean sample on a landing core is promoted to Coast so a raised garrison never starts on
            // impassable water (a no-op on dry worlds like Mars, which have no ocean).
            if (_authored != null)
            {
                var a = _authored(lon, lat);
                return (a == RegionFeatureType.Ocean && musterCoreLand) ? RegionFeatureType.Coast : a;
            }

            double elev = Field(lon, lat, _ePhaseLon, _ePhaseLat);
            double moist = Field(lon, lat, _mPhaseLon, _mPhaseLat);

            if (elev < _seaLevel) return musterCoreLand ? RegionFeatureType.Coast : RegionFeatureType.Ocean;
            if (elev < _seaLevel + CoastBand) return RegionFeatureType.Coast;

            // Land — the world's CLIMATE first (temperature), then relief + moisture.
            if (_temp <= ColdC)
            {
                if (elev > MountainThresh) return RegionFeatureType.Ice;         // frozen peaks
                if (elev > HighlandThresh) return RegionFeatureType.Highlands;
                // Lowland cold: a DRY cold world (Mars/Luna) is rock and dust, not tundra; a damp cold world gets
                // tundra where moist, barren where not — so a frozen world isn't one flat grey biome.
                if (_hydroFrac < DryWorldHydro)
                    return moist < ColdDryMoist ? RegionFeatureType.Barren : RegionFeatureType.Desert;
                return moist > ColdDampMoist ? RegionFeatureType.Tundra : RegionFeatureType.Barren;
            }
            if (_temp >= HotC)
                return elev > MountainThresh ? RegionFeatureType.Volcanic
                     : (moist > 0.6 ? RegionFeatureType.Plains : RegionFeatureType.Desert);

            double mtn = _tectonic ? MountainThresh - 0.08 : MountainThresh;   // tectonic worlds are more mountainous
            if (elev > mtn) return RegionFeatureType.Mountains;
            if (elev > HighlandThresh) return RegionFeatureType.Highlands;

            // Lowland by moisture (a wet world greens, a dry one browns).
            if (moist > 0.66) return _temp > 28.0 ? RegionFeatureType.Jungle : RegionFeatureType.Forest;
            if (moist > 0.48) return RegionFeatureType.Forest;
            if (moist > 0.34) return RegionFeatureType.Plains;
            if (moist > 0.18) return _hydroFrac < 0.2 ? RegionFeatureType.Desert : RegionFeatureType.Plains;
            return _hydroFrac < 0.2 ? RegionFeatureType.Barren : RegionFeatureType.Desert;
        }

        /// <summary>A smooth 0..1 field: sum of sines, PERIODIC in longitude (wraps the ring), amplitude decaying per
        /// octave (big continents + finer coastline detail).</summary>
        private static double Field(double lon, double lat, double[] phaseLon, double[] phaseLat)
        {
            double v = 0, norm = 0;
            for (int k = 0; k < Octaves; k++)
            {
                int f = k + 1;
                double amp = 1.0 / f;
                v += amp * Math.Sin(TwoPi * f * lon + phaseLon[k]) * Math.Cos(Math.PI * f * lat + phaseLat[k]);
                norm += amp;
            }
            return 0.5 + 0.5 * (v / norm);   // → 0..1
        }

        private static double Rnd(StarSystem system) => system != null ? system.RNGNextDouble() : 0.5;
    }
}
