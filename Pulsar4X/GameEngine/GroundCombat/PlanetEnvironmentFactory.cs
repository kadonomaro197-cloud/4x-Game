using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Galaxy;
using Pulsar4X.DataStructures;
using Pulsar4X.Hazards;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Generates a world's DYNAMIC environmental hazards (<see cref="PlanetEnvironmentsDB"/>) from its PHYSICS — the
    /// "engine must be intelligent about where hazards occur" requirement. It reads the scalars the engine already
    /// computes (`AtmosphereDB` surface temperature / pressure / hydrosphere / composition, `SystemBodyInfoDB` body
    /// type / tectonics) and emits ONLY environments that make sense there — so a world's menaces are a read-out of
    /// what it IS, never Earth-copied or flat RNG.
    ///
    /// **The load-bearing gate: a gas/ice giant has NO SURFACE → NO surface environments** (the developer's explicit
    /// call). Mirrors <see cref="PlanetRegionsFactory"/>: defensive, idempotent, seeded by the system RNG, hooked
    /// into New-Game-critical generation so it must never throw or double-generate.
    ///
    /// v1 wires a first, honest slice of the catalog (fire / cryo / corrosive / ash / dust / lightning). The rest of
    /// the exotic sweep + the 6 meta-mechanics (moving storms, seasonal cycles, tidal-lock terminator, the biosphere,
    /// the map changing) are DATA/RULE additions on this same core — never new engine code. Design: docs/ENVIRONMENTS-DESIGN.md.
    /// </summary>
    public static class PlanetEnvironmentFactory
    {
        // --- physics thresholds (tunable dials; provisional, calibrate on real generated worlds) ---
        private const double ScorchingC = 400.0;     // above this °C: fire / molten
        private const double FrozenC = -120.0;       // below this °C: cryo / ice
        private const double ThickAtm = 5.0;         // atm pressure above this: lightning superstorms
        private const double DryHydro = 10.0;        // hydrosphere % below this (with air): dust storms

        public static void GenerateForSystem(StarSystem system)
        {
            if (system == null) return;

            List<Entity> bodies;
            try { bodies = new List<Entity>(system.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()); }
            catch { return; }

            foreach (var body in bodies)
            {
                try
                {
                    if (body.HasDataBlob<PlanetEnvironmentsDB>()) continue;                 // idempotent
                    if (!body.TryGetDataBlob<SystemBodyInfoDB>(out var info)) continue;
                    if (!HasSurface(info.BodyType)) continue;                               // ★ gas/ice giant → no surface hazards
                    if (!body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0) continue;

                    var envDB = BuildEnvironments(system, body, info, regionsDB);
                    if (envDB != null) body.SetDataBlob(envDB);
                }
                catch { /* skip a bad body — never crash generation */ }
            }
        }

        /// <summary>The gate: a gas/ice giant / gas-dwarf has no solid surface, so no surface environments.</summary>
        private static bool HasSurface(BodyType t)
            => t == BodyType.Terrestrial || t == BodyType.Moon || t == BodyType.DwarfPlanet
            || t == BodyType.Asteroid || t == BodyType.Comet;

        private static PlanetEnvironmentsDB BuildEnvironments(StarSystem system, Entity body, SystemBodyInfoDB info, PlanetRegionsDB regionsDB)
        {
            body.TryGetDataBlob<AtmosphereDB>(out var atmo);
            double tempC = atmo != null ? atmo.SurfaceTemperature : (info.BaseTemperature - 273.15);
            double pressure = atmo != null ? atmo.Pressure : 0.0;
            double hydro = atmo != null ? (double)atmo.HydrosphereExtent : 0.0;
            bool hasAir = pressure > 0.05;
            bool tectonic = info.Tectonics != TectonicActivity.Dead && info.Tectonics != TectonicActivity.Unknown;
            bool corrosiveAir = atmo != null && HasCorrosiveGas(atmo);

            // The PHYSICS decides the MENU of menaces this world can have; the RNG decides their coverage. A world
            // that qualifies for a menace ALWAYS gets it in at least one region (so a scorching world definitely has
            // fire somewhere), then it spreads to more regions by chance — so regions differ (some clear, some deadly).
            var menaces = new List<(string name, HazardEffectType effect, double mag)>();
            if (tempC > ScorchingC)                 menaces.Add(("Fire Tornadoes",       HazardEffectType.HeatDamage,      ScaleHeat(tempC)));
            else if (tempC < FrozenC)               menaces.Add(("Cryostorms",           HazardEffectType.HeatDamage,      15.0));   // cold attrition = thermal flavour
            if (corrosiveAir)                        menaces.Add(("Corrosive Superstorm", HazardEffectType.CorrosiveDamage, 25.0));
            if (tectonic)                            menaces.Add(("Ash Storm",            HazardEffectType.SensorJam,       0.5));
            if (hasAir && hydro < DryHydro)          menaces.Add(("Dust Storm",           HazardEffectType.SensorJam,       0.4));
            if (pressure > ThickAtm)                 menaces.Add(("Lightning Superstorm", HazardEffectType.SensorJam,       0.6));

            var envs = new List<RegionEnvironment>();
            int regionCount = regionsDB.Regions.Count;
            foreach (var m in menaces)
            {
                int seed = (int)(system.RNGNextDouble() * regionCount) % regionCount;
                if (seed < 0) seed = 0;
                envs.Add(new RegionEnvironment(seed, m.name, m.effect, m.mag));            // guaranteed in ≥1 region
                for (int i = 0; i < regionCount; i++)                                       // and spreads by chance
                    if (i != seed && system.RNGNextDouble() < 0.35)
                        envs.Add(new RegionEnvironment(i, m.name, m.effect, m.mag));
            }

            return new PlanetEnvironmentsDB { Environments = envs };
        }

        /// <summary>Corrosive atmosphere = a corrosive gas is present (sulphur / chlorine / acid ids).</summary>
        private static bool HasCorrosiveGas(AtmosphereDB atmo)
        {
            if (atmo.Composition == null) return false;
            foreach (var gasId in atmo.Composition.Keys)
            {
                if (string.IsNullOrEmpty(gasId)) continue;
                string g = gasId.ToLowerInvariant();
                if (g.Contains("sulph") || g.Contains("sulf") || g.Contains("chlorine") || g.Contains("acid"))
                    return true;
            }
            return false;
        }

        /// <summary>Hotter → more fire attrition, capped so it can't one-shot a garrison in a tick.</summary>
        private static double ScaleHeat(double tempC) => Math.Min(50.0, 20.0 + (tempC - ScorchingC) * 0.05);
    }
}
