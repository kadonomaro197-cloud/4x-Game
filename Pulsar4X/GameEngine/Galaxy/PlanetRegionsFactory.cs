using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates the REGION layer (<see cref="PlanetRegionsDB"/>) for the planets in a star system — the strategic
    /// ground map. v1: four longitude slices in a RING per major body, each carrying a bundle of "logical" features
    /// (random, but consistent with the world's own nature — a wet world gets ocean, a rocky/active world gets
    /// mountains). Authored worlds (Sol / a loaded blueprint) start SURVEYED (we all know Earth); procedurally
    /// generated worlds start UNKNOWN until scanned — that's where exploration meets the map.
    ///
    /// Mirrors <see cref="LagrangeFactory"/>: runs inside New-Game-critical system generation, so every path is
    /// defensively guarded and idempotent — a bad body or a math edge case skips just that body and can never crash
    /// generation. Hooked into <see cref="StarSystemFactory"/> at the procedural + blueprint + Sol gen paths.
    ///
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public static class PlanetRegionsFactory
    {
        /// <summary>v1: four slices in a ring.</summary>
        public const int RegionCount = 4;

        /// <summary>PLACEHOLDER surface march speed (km per game-second) used to turn a region's width into a
        /// crossing time. ~72 km/h. Tune later — it's the "units take thousands of miles / logical time" dial.</summary>
        private const double PlaceholderMarch_KmPerSec = 0.02;

        /// <summary>
        /// Attach a region layer to every major body in the system that lacks one. <paramref name="surveyed"/> is
        /// TRUE for authored worlds (their geography is known), FALSE for procedurally generated worlds (unknown
        /// until scanned). Never throws. Idempotent (a body that already has regions is skipped).
        /// </summary>
        public static void GenerateForSystem(StarSystem system, bool surveyed)
        {
            if (system == null) return;

            List<Entity> bodies;
            try { bodies = new List<Entity>(system.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()); }
            catch { return; }

            foreach (var body in bodies)
            {
                try
                {
                    if (body.HasDataBlob<PlanetRegionsDB>()) continue;            // idempotent
                    if (!body.TryGetDataBlob<SystemBodyInfoDB>(out var info)) continue;
                    if (!IsMajorBody(info.BodyType)) continue;                    // planets/giants/dwarfs, not asteroids
                    var regionsDB = BuildRegions(system, body, info, surveyed);
                    if (regionsDB != null) body.SetDataBlob(regionsDB);
                }
                catch { /* skip a bad body — never crash generation */ }
            }
        }

        private static bool IsMajorBody(BodyType t)
            => t == BodyType.Terrestrial || t == BodyType.GasGiant || t == BodyType.IceGiant
            || t == BodyType.DwarfPlanet || t == BodyType.GasDwarf
            || t == BodyType.Moon;   // moons are ground-combat places too (Luna, Ganymede, …) — surveyable + fightable

        private static PlanetRegionsDB BuildRegions(StarSystem system, Entity body, SystemBodyInfoDB info, bool surveyed)
        {
            double radiusM = body.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.RadiusInM : 6.371e6;
            double radiusKm = radiusM / 1000.0;
            double surface_km2 = 4.0 * Math.PI * radiusKm * radiusKm;
            double sliceWidthKm = (2.0 * Math.PI * radiusKm) / RegionCount; // a quarter of the circumference

            // Feature inputs — reliable scalars only (unit-safe): hydrosphere %, tectonic activity, gas vs. rock.
            body.TryGetDataBlob<AtmosphereDB>(out var atmo);
            double hydro = atmo != null ? (double)atmo.HydrosphereExtent : 0.0; // 0..100 (% surface water)
            bool gas = info.BodyType == BodyType.GasGiant || info.BodyType == BodyType.GasDwarf || info.BodyType == BodyType.IceGiant;
            bool tectonic = info.Tectonics != TectonicActivity.Dead && info.Tectonics != TectonicActivity.Unknown;

            var regions = new List<Region>();
            for (int i = 0; i < RegionCount; i++)
            {
                var r = new Region
                {
                    Index = i,
                    Surveyed = surveyed,
                    Area_km2 = surface_km2 / RegionCount * (0.85 + system.RNGNextDouble() * 0.30),
                    CrossingTimeSeconds = sliceWidthKm / PlaceholderMarch_KmPerSec,
                };
                r.Neighbors.Add((i + RegionCount - 1) % RegionCount); // west
                r.Neighbors.Add((i + 1) % RegionCount);               // east
                r.Features = RollFeatures(system, gas, hydro, tectonic);
                regions.Add(r);
            }

            EnsureLogicalMinimums(regions, gas, hydro);
            return new PlanetRegionsDB(regions);
        }

        private static List<RegionFeature> RollFeatures(StarSystem system, bool gas, double hydro, bool tectonic)
        {
            var feats = new List<RegionFeature>();
            if (gas)
            {
                feats.Add(new RegionFeature(RegionFeatureType.GasLayers, 1.0));
                return feats;
            }
            int count = 2 + (int)(system.RNGNextDouble() * 2.99); // 2..4 features
            for (int k = 0; k < count; k++)
                feats.Add(new RegionFeature(PickFeature(system, hydro, tectonic), 0.2 + system.RNGNextDouble() * 0.5));
            return feats;
        }

        private static RegionFeatureType PickFeature(StarSystem system, double hydro, bool tectonic)
        {
            double hydroFrac = Math.Max(0.0, Math.Min(1.0, hydro / 100.0));
            // water-driven
            if (system.RNGNextDouble() < hydroFrac * 0.7)
                return system.RNGNextDouble() < 0.6 ? RegionFeatureType.Ocean : RegionFeatureType.Coast;
            // relief-driven
            if (tectonic && system.RNGNextDouble() < 0.4)
                return system.RNGNextDouble() < 0.5 ? RegionFeatureType.Mountains : RegionFeatureType.Highlands;
            // the rest is climate-of-water: wet worlds get green, dry worlds get barren
            double r = system.RNGNextDouble();
            if (hydroFrac > 0.25)
                return r < 0.45 ? RegionFeatureType.Plains : (r < 0.8 ? RegionFeatureType.Forest : RegionFeatureType.Wetland);
            return r < 0.5 ? RegionFeatureType.Barren : (r < 0.85 ? RegionFeatureType.Desert : RegionFeatureType.Plains);
        }

        /// <summary>Keep generation "logical": a genuinely wet world must have at least one ocean somewhere.</summary>
        private static void EnsureLogicalMinimums(List<Region> regions, bool gas, double hydro)
        {
            if (gas || regions.Count == 0 || hydro < 40) return;
            bool anyOcean = regions.Exists(r => r.Features.Exists(f => f.Type == RegionFeatureType.Ocean));
            if (!anyOcean) regions[0].Features.Insert(0, new RegionFeature(RegionFeatureType.Ocean, 0.6));
        }
    }
}
