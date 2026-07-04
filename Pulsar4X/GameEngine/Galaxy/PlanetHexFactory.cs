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
        private const double TwoPi = 2.0 * Math.PI;

        private readonly bool _gas, _tectonic;
        private readonly double _seaLevel, _temp, _hydroFrac;
        private readonly double[] _ePhaseLon, _ePhaseLat, _mPhaseLon, _mPhaseLat;

        private WorldTerrain(bool gas, double seaLevel, double temp, bool tectonic, double hydroFrac,
            double[] ePhaseLon, double[] ePhaseLat, double[] mPhaseLon, double[] mPhaseLat)
        {
            _gas = gas; _seaLevel = seaLevel; _temp = temp; _tectonic = tectonic; _hydroFrac = hydroFrac;
            _ePhaseLon = ePhaseLon; _ePhaseLat = ePhaseLat; _mPhaseLon = mPhaseLon; _mPhaseLat = mPhaseLat;
        }

        public static WorldTerrain ForBody(Entity body, StarSystem system, int regionCount)
        {
            body.TryGetDataBlob<AtmosphereDB>(out var atmo);
            body.TryGetDataBlob<SystemBodyInfoDB>(out var info);
            double hydroFrac = atmo != null ? Math.Max(0.0, Math.Min(1.0, (double)atmo.HydrosphereExtent / 100.0)) : 0.0;
            double temp = atmo != null ? atmo.SurfaceTemperature : 15.0;
            bool gas = info != null && (info.BodyType == BodyType.GasGiant || info.BodyType == BodyType.GasDwarf || info.BodyType == BodyType.IceGiant);
            bool tectonic = info != null && info.Tectonics != TectonicActivity.Dead && info.Tectonics != TectonicActivity.Unknown;

            // Sea level rises with hydrosphere: a ~70%-water world floods ~70% of the elevation range → mostly ocean;
            // a dry world barely any. Bounded so an ocean world keeps some land and a desert world some lowland.
            double seaLevel = 0.12 + hydroFrac * 0.72;

            var el = new double[Octaves]; var et = new double[Octaves]; var ml = new double[Octaves]; var mt = new double[Octaves];
            for (int k = 0; k < Octaves; k++)
            {
                el[k] = Rnd(system) * TwoPi; et[k] = Rnd(system) * TwoPi;
                ml[k] = Rnd(system) * TwoPi; mt[k] = Rnd(system) * TwoPi;
            }
            return new WorldTerrain(gas, seaLevel, temp, tectonic, hydroFrac, el, et, ml, mt);
        }

        /// <summary>Terrain for one hex from its GLOBAL position: region+q give longitude (continuous across region
        /// borders, wrapping the ring); r gives latitude. So the field is one coherent world, not per-region.</summary>
        public RegionFeatureType TerrainAt(int region, int regionCount, int q, int r, int radius)
        {
            if (_gas) return RegionFeatureType.GasLayers;
            if (radius <= 0 || regionCount <= 0) return RegionFeatureType.Plains;

            double lon = (region + (q + radius) / (2.0 * radius)) / regionCount;   // 0..1 around the ring (wraps)
            double lat = (r + radius) / (2.0 * radius);                             // 0..1 pole → pole
            double elev = Field(lon, lat, _ePhaseLon, _ePhaseLat);
            double moist = Field(lon, lat, _mPhaseLon, _mPhaseLat);

            if (elev < _seaLevel) return RegionFeatureType.Ocean;
            if (elev < _seaLevel + CoastBand) return RegionFeatureType.Coast;

            // Land — the world's CLIMATE first (temperature), then relief + moisture.
            if (_temp <= ColdC)
                return elev > MountainThresh ? RegionFeatureType.Ice
                     : (elev > HighlandThresh ? RegionFeatureType.Highlands : RegionFeatureType.Tundra);
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
