using System;
using System.Collections.Generic;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Names;
using Pulsar4X.Movement;
using Pulsar4X.Orbits;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates LAGRANGE-POINT anchor markers (<see cref="LagrangePointDB"/>) for a star system — stable points
    /// in space where a station can be deployed instead of a random spot. v1: the TROJAN points L4/L5 for each
    /// star–planet pair, built as the planet's own orbit offset ±60° in mean anomaly so they co-orbit for free.
    ///
    /// This runs inside system generation (New-Game-critical), so EVERY path is defensively guarded — a bad body
    /// or a math edge case skips just that marker and can never crash generation.
    /// </summary>
    public static class LagrangeFactory
    {
        // L4 is 60° ahead of the secondary on its orbit, L5 is 60° behind (in mean anomaly).
        private const double SixtyDegrees = Math.PI / 3.0;

        /// <summary>Generate L4/L5 markers for every star-orbiting planet in the system. Never throws.</summary>
        public static void GenerateForSystem(StarSystem system)
        {
            if (system == null) return;

            // Idempotent — never double-generate for a system (some gen paths run more than once).
            try { if (system.GetAllEntitiesWithDataBlob<LagrangePointDB>().Count > 0) return; }
            catch { return; }

            // Snapshot the planets first (we're about to add entities to the same manager).
            var planets = new List<Entity>();
            try
            {
                foreach (var e in system.GetAllEntitiesWithDataBlob<OrbitDB>())
                {
                    try
                    {
                        if (!e.TryGetDataBlob<SystemBodyInfoDB>(out var body)) continue;
                        if (!IsMajorBody(body.BodyType)) continue; // planets/giants/dwarfs only — not asteroids/comets
                        var parent = e.GetDataBlob<OrbitDB>().Parent;
                        if (parent == null || !parent.HasDataBlob<StarInfoDB>()) continue; // orbits a STAR (skip moons)
                        planets.Add(e);
                    }
                    catch { /* skip a bad body */ }
                }
            }
            catch { return; }

            foreach (var planet in planets)
            {
                TryCreateTrojan(system, planet, 4, +SixtyDegrees);
                TryCreateTrojan(system, planet, 5, -SixtyDegrees);
            }
        }

        private static bool IsMajorBody(BodyType t)
            => t == BodyType.Terrestrial || t == BodyType.GasGiant || t == BodyType.IceGiant
            || t == BodyType.DwarfPlanet || t == BodyType.GasDwarf;

        private static void TryCreateTrojan(StarSystem system, Entity planet, int index, double anomalyOffset)
        {
            try
            {
                var planetOrbit = planet.GetDataBlob<OrbitDB>();
                var star = planetOrbit.Parent;

                // Same orbit as the planet, offset in mean anomaly → sits 60° ahead/behind = L4/L5, and the orbit
                // processor moves it along that orbit for free (co-orbiting).
                var ke = new KeplerElements
                {
                    SemiMajorAxis = planetOrbit.SemiMajorAxis,
                    Eccentricity = planetOrbit.Eccentricity,
                    Inclination = planetOrbit.Inclination,
                    LoAN = planetOrbit.LongitudeOfAscendingNode,
                    AoP = planetOrbit.ArgumentOfPeriapsis,
                    MeanAnomalyAtEpoch = planetOrbit.MeanAnomalyAtEpoch + anomalyOffset,
                };
                var markerOrbit = OrbitDB.FromKeplerElements(star, 1.0, ke, planetOrbit.Epoch);
                var initialPos = OrbitMath.GetAbsolutePosition(markerOrbit, planetOrbit.Epoch);

                string planetName = planet.GetDataBlob<NameDB>().DefaultName;
                var blobs = new List<BaseDataBlob>
                {
                    new NameDB($"{planetName} L{index}"),
                    markerOrbit,
                    new PositionDB(initialPos, star),
                    MassVolumeDB.NewFromMassAndRadius_m(1, 1), // token — a point, not a body (satisfies StationFactory's radius read)
                    new VisibleByDefaultDB(),
                    new LagrangePointDB(star, planet, index),
                };

                var marker = Entity.Create();
                system.AddEntity(marker, blobs);
            }
            catch { /* skip this marker — never crash generation */ }
        }
    }
}
