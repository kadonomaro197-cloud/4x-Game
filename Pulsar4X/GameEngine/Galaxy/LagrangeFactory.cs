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
    /// Generates LAGRANGE-POINT anchor markers (<see cref="LagrangePointDB"/>) for a star system — stable, named
    /// points in space where a station can be deployed instead of a random spot. v1: the TROJAN points L4/L5 for
    /// each star–planet pair (60° ahead / behind the planet on its orbit — the stable points where a permanent
    /// station belongs).
    ///
    /// The marker is a STATIC point (mirrors the proven <see cref="Pulsar4X.JumpPoints"/> survey-point recipe:
    /// PositionDB with MoveType.None, NO OrbitDB), so it never enters the orbit processor. (A first cut gave the
    /// marker the planet's orbit offset ±60° to co-orbit "for free," but that crashed the parallel orbit processor
    /// with a PositionDB lookup on a worker thread — so v1 is a static point at the epoch L-point; making it
    /// co-orbit is a documented refinement, e.g. a tiny LagrangeProcessor that recomputes the position each cycle.)
    ///
    /// This runs inside New-Game-critical system generation, so EVERY path is defensively guarded — a bad body or a
    /// math edge case skips just that marker and can never crash generation.
    /// </summary>
    public static class LagrangeFactory
    {
        private const double SixtyDegrees = Math.PI / 3.0;

        /// <summary>Generate L4/L5 markers for every star-orbiting planet in the system. Never throws. Idempotent.</summary>
        public static void GenerateForSystem(StarSystem system)
        {
            if (system == null) return;

            try { if (system.GetAllEntitiesWithDataBlob<LagrangePointDB>().Count > 0) return; }
            catch { return; }

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

        private static void TryCreateTrojan(StarSystem system, Entity planet, int index, double angle)
        {
            try
            {
                var planetOrbit = planet.GetDataBlob<OrbitDB>();
                var star = planetOrbit.Parent;

                // L4/L5 sit on the planet's orbit, 60° ahead/behind: rotate the star→planet vector by ±60° (in the
                // orbital/XY plane) about the star. Static point at the epoch position (see class note).
                Vector3 starPos = star.TryGetDataBlob<PositionDB>(out var sp) ? sp.AbsolutePosition : new Vector3(0, 0, 0);
                Vector3 planetPos = OrbitMath.GetAbsolutePosition(planetOrbit, planetOrbit.Epoch);
                Vector3 r = planetPos - starPos;
                double c = Math.Cos(angle), s = Math.Sin(angle);
                Vector3 lPos = starPos + new Vector3(r.X * c - r.Y * s, r.X * s + r.Y * c, r.Z);

                var posDB = new PositionDB(lPos.X, lPos.Y, lPos.Z, null);
                posDB.MoveType = PositionDB.MoveTypes.None; // a fixed point — not orbit-processed

                string planetName = planet.GetDataBlob<NameDB>().DefaultName;
                var blobs = new List<BaseDataBlob>
                {
                    new NameDB($"{planetName} L{index}"),
                    posDB,
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
