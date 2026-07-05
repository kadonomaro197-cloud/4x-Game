using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// ONE pending tile-targeted build (C-track economy wire) — the record that ties a REAL industry job to the exact
    /// mini-hex tile the player chose to build on. When the queued installation finishes construction (materials +
    /// build-time, through the normal production line), <see cref="GroundBuild.ReconcileBody"/> finds the freshly-built
    /// footprint building and drops it on (<see cref="GQ"/>,<see cref="GR"/>) tile (<see cref="TileQ"/>,<see cref="TileR"/>).
    /// Save-safe (plain fields, deep-copied). Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    public class GroundBuildReservation
    {
        [JsonProperty] public int ColonyId { get; internal set; }
        [JsonProperty] public string DesignId { get; internal set; }
        [JsonProperty] public int GQ { get; internal set; }
        [JsonProperty] public int GR { get; internal set; }
        [JsonProperty] public int TileQ { get; internal set; }
        [JsonProperty] public int TileR { get; internal set; }

        public GroundBuildReservation() { }
        public GroundBuildReservation(int colonyId, string designId, int gq, int gr, int tileQ, int tileR)
        { ColonyId = colonyId; DesignId = designId; GQ = gq; GR = gr; TileQ = tileQ; TileR = tileR; }
        public GroundBuildReservation(GroundBuildReservation o)
        { ColonyId = o.ColonyId; DesignId = o.DesignId; GQ = o.GQ; GR = o.GR; TileQ = o.TileQ; TileR = o.TileR; }
    }

    /// <summary>
    /// The body's queue of pending tile-targeted builds — a small DataBlob on the planet body (like
    /// <see cref="GroundForcesDB"/>). Created on demand when the first "build here" is issued; drained by
    /// <see cref="GroundBuildQueueProcessor"/> as builds complete. Save-safe (deep-copied). Nothing else shares it, so
    /// its own reconciler processor is legal (landmine L9).
    /// </summary>
    public class GroundBuildQueueDB : BaseDataBlob
    {
        [JsonProperty] public List<GroundBuildReservation> Reservations { get; internal set; } = new List<GroundBuildReservation>();

        public GroundBuildQueueDB() { }
        public GroundBuildQueueDB(GroundBuildQueueDB o)
        {
            Reservations = new List<GroundBuildReservation>(o.Reservations?.Count ?? 0);
            if (o.Reservations != null) foreach (var r in o.Reservations) Reservations.Add(new GroundBuildReservation(r));
        }

        public override object Clone() => new GroundBuildQueueDB(this);
    }
}
