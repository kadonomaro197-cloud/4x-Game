using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// ONE standing intra-planet haul route (DS-T1) — a save-safe record of "keep moving up to <see cref="PerTickCap"/>
    /// units of good <see cref="CargoableId"/> from the source hex to the dest hex, every tick." The game already has a
    /// ONE-SHOT haul order (<see cref="Pulsar4X.Galaxy.HexHaulOrder"/>, hex→hex): you click it once and the ore moves
    /// once. This is the STANDING version — you set it once and <see cref="GroundHaulRouteProcessor"/> re-issues the same
    /// conserved hex→hex move every tick, so ore keeps flowing from a mining hex to a depot hex without re-clicking (the
    /// ground echo of an automated space cargo route, <c>Logistics</c>).
    ///
    /// The move itself is NOT re-implemented here — the processor calls the SAME pure conserved core the one-shot order
    /// uses (<c>HexHaulOrder.HaulBetweenHexes</c>): dst gains exactly what src loses, capped by what's on hand, no ore
    /// created or destroyed. Save-safe: plain fields, all <c>[JsonProperty]</c>, deep-copied by the ctor (L12).
    ///
    /// v1 is hex→hex only (the direction <c>HaulBetweenHexes</c> factors out as a pure, reusable core). The hex→colony-
    /// cargo direction (<c>HexHaulOrder.CreateHexToColony</c>) is a flagged follow-up: its move logic lives INSIDE the
    /// one-shot order's <c>internal Execute</c> (coupled to the colony entity + cargo-hold dance) and isn't factored out,
    /// so reusing it would mean editing an existing file. Design: docs/ground/UNITS-ON-THE-MAP-DESIGN.md §3.
    /// </summary>
    public class GroundHaulRoute
    {
        /// <summary>Which faction set this route (the ground echo of an order's requesting faction). Carried for
        /// ownership/readout; the conserved move itself is owner-agnostic (it moves whatever is on the source hex).</summary>
        [JsonProperty] public int FactionOwnerID { get; internal set; }
        /// <summary>Source hex global column Q (on the body's <c>SurfaceGrid</c> — the same coord <c>HexAt</c> takes).</summary>
        [JsonProperty] public int SrcQ { get; internal set; }
        /// <summary>Source hex global row R.</summary>
        [JsonProperty] public int SrcR { get; internal set; }
        /// <summary>Destination hex global column Q.</summary>
        [JsonProperty] public int DstQ { get; internal set; }
        /// <summary>Destination hex global row R.</summary>
        [JsonProperty] public int DstR { get; internal set; }
        /// <summary>The good to move, as an <c>ICargoable.ID</c> (the same int the hex <see cref="Pulsar4X.Galaxy.GroundHex.Stockpile"/>
        /// is keyed by, and the same int <c>DepositMineralId</c> uses — one language, no translation).</summary>
        [JsonProperty] public int CargoableId { get; internal set; }
        /// <summary>Max units to move PER TICK; ≤ 0 = move ALL of that good on hand each tick (matching the one-shot
        /// order's amount convention). The conserved core caps this at what's actually present.</summary>
        [JsonProperty] public long PerTickCap { get; internal set; }

        public GroundHaulRoute() { }
        public GroundHaulRoute(int factionOwnerId, int srcQ, int srcR, int dstQ, int dstR, int cargoableId, long perTickCap)
        {
            FactionOwnerID = factionOwnerId;
            SrcQ = srcQ; SrcR = srcR; DstQ = dstQ; DstR = dstR;
            CargoableId = cargoableId; PerTickCap = perTickCap;
        }
        public GroundHaulRoute(GroundHaulRoute o)
        {
            FactionOwnerID = o.FactionOwnerID;
            SrcQ = o.SrcQ; SrcR = o.SrcR; DstQ = o.DstQ; DstR = o.DstR;
            CargoableId = o.CargoableId; PerTickCap = o.PerTickCap;
        }
    }

    /// <summary>
    /// The body's list of STANDING haul routes — a small DataBlob on the planet body (like <see cref="GroundBuildQueueDB"/>
    /// and <see cref="GroundForcesDB"/>: things that are OF the planet). Created on demand when the first route is added
    /// (<see cref="GroundHaulRoutes.AddRoute"/>); iterated hourly by <see cref="GroundHaulRouteProcessor"/> (keyed on its
    /// OWN blob — no other processor owns it, landmine L9). Save-safe (deep-copied; <c>[JsonProperty]</c>; a real
    /// <see cref="Clone"/> — L12). <b>INERT until a route is added</b>: no default entity carries this blob, so the
    /// processor never iterates and a stock game is byte-identical.
    /// </summary>
    public class GroundHaulRouteDB : BaseDataBlob
    {
        [JsonProperty] public List<GroundHaulRoute> Routes { get; internal set; } = new List<GroundHaulRoute>();

        public GroundHaulRouteDB() { }
        public GroundHaulRouteDB(GroundHaulRouteDB o)
        {
            Routes = new List<GroundHaulRoute>(o.Routes?.Count ?? 0);
            if (o.Routes != null) foreach (var r in o.Routes) Routes.Add(new GroundHaulRoute(r));
        }

        public override object Clone() => new GroundHaulRouteDB(this);
    }

    /// <summary>
    /// The "set/clear a standing haul route" primitive — the ground echo of setting up an automated cargo route. Static
    /// helpers mirroring <c>GroundForces</c>/<c>GroundBuild</c> style: create the body's <see cref="GroundHaulRouteDB"/>
    /// on demand. This is the engine surface a player button OR the AI issues against (the same call for both seats —
    /// One Verb, Both Seats); a route once set is re-run every tick by the processor with no further input.
    /// </summary>
    public static class GroundHaulRoutes
    {
        /// <summary>Add a standing hex→hex haul route to <paramref name="body"/> (creating the route roster on demand),
        /// moving up to <paramref name="perTickCap"/> (≤ 0 = all) of <paramref name="cargoableId"/> from the source hex
        /// (<paramref name="srcQ"/>,<paramref name="srcR"/>) to the dest hex (<paramref name="dstQ"/>,<paramref name="dstR"/>)
        /// each tick. Returns the created route (null on a null body). Adding the blob wakes the hotloop for this body
        /// (via <c>SetDataBlob</c> → <c>AddSystemInterupt</c>).</summary>
        public static GroundHaulRoute AddRoute(Entity body, int factionId, int srcQ, int srcR, int dstQ, int dstR, int cargoableId, long perTickCap)
        {
            if (body == null) return null;
            if (!body.TryGetDataBlob<GroundHaulRouteDB>(out var db))
            {
                db = new GroundHaulRouteDB();
                body.SetDataBlob(db);
            }
            var route = new GroundHaulRoute(factionId, srcQ, srcR, dstQ, dstR, cargoableId, perTickCap);
            db.Routes.Add(route);
            return route;
        }

        /// <summary>Clear ALL standing routes on <paramref name="body"/> (a no-op if it has no route roster). The blob is
        /// left in place — an empty route list means the processor iterates the body once and moves nothing (harmless).</summary>
        public static void ClearRoutes(Entity body)
        {
            if (body != null && body.TryGetDataBlob<GroundHaulRouteDB>(out var db))
                db.Routes.Clear();
        }
    }
}
