using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Names;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Tags an entity as a LAGRANGE POINT marker — a named, stable point in space near a two-body pair where a
    /// station can be anchored, instead of a random empty-space spot (the developer's ask, 2026-07-03).
    ///
    /// v1 generates only the stable TROJAN points L4/L5 (60° ahead / behind the secondary on its orbit) for
    /// star–planet pairs. The marker is built with the secondary's own orbit offset ±60° in mean anomaly, so it
    /// CO-ORBITS for free via the existing orbit processor — it genuinely sits at L4/L5, not a static drift. L4/L5
    /// are the stable points where a permanent station belongs; the collinear (unstable) L1/L2/L3 and planet–moon
    /// pairs are a documented refinement.
    /// </summary>
    public class LagrangePointDB : BaseDataBlob
    {
        /// <summary>The body being orbited (e.g. the star).</summary>
        [JsonProperty] public Entity Primary { get; internal set; } = Entity.InvalidEntity;
        /// <summary>The body that defines the point (e.g. the planet whose orbit the L-point rides).</summary>
        [JsonProperty] public Entity Secondary { get; internal set; } = Entity.InvalidEntity;
        /// <summary>Which Lagrange point: 4 or 5 (v1).</summary>
        [JsonProperty] public int PointIndex { get; internal set; }

        public LagrangePointDB() { }

        public LagrangePointDB(Entity primary, Entity secondary, int pointIndex)
        {
            Primary = primary;
            Secondary = secondary;
            PointIndex = pointIndex;
        }

        public LagrangePointDB(LagrangePointDB other)
        {
            Primary = other.Primary;
            Secondary = other.Secondary;
            PointIndex = other.PointIndex;
        }

        public override object Clone() => new LagrangePointDB(this);

        public new static List<Type> GetDependencies() => new List<Type>() { typeof(NameDB) };
    }
}
