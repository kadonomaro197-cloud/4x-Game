using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The STANDING haul-route runner (DS-T1) — an hourly hotloop that drains each body's <see cref="GroundHaulRouteDB"/>:
    /// for every standing route, it re-issues the conserved hex→hex move so ore keeps flowing from a mining hex to a
    /// depot hex without the player re-clicking. Keyed on its OWN blob (<see cref="GroundHaulRouteDB"/> — no other
    /// processor owns it, landmine L9), so it processes ONLY bodies that carry a route and sleeps otherwise (the empty-
    /// system re-arm, GameEngine gotcha 5). Trivial ctor + a try/catch body: a hotloop must never throw or it crashes
    /// the whole game loop (landmine L4 / gotcha #1).
    ///
    /// It REUSES the one-shot haul's pure core (<see cref="HexHaulOrder.HaulBetweenHexes"/>) — it does not re-implement
    /// hauling. Mirrors <see cref="GroundBuildQueueProcessor"/>'s shape exactly. <b>INERT until a route is added</b>: no
    /// default entity carries the route blob, so a stock game is byte-identical.
    /// </summary>
    public class GroundHaulRouteProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromHours(1);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromHours(1);
        public Type GetParameterType { get; } = typeof(GroundHaulRouteDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            try { RunRoutes(entity); }
            catch { /* never throw in a hotloop (L4) — a bad body is skipped, the sim keeps running */ }
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var bodies = manager.GetAllEntitiesWithDataBlob<GroundHaulRouteDB>();
            foreach (var body in bodies)
                ProcessEntity(body, deltaSeconds);
            return bodies.Count;
        }

        /// <summary>Run every standing route on <paramref name="body"/> once. Ensures the body's surface grid on demand
        /// (idempotent, the same call the one-shot order makes), then applies each route via <see cref="ApplyRoute"/>.
        /// Returns how many routes actually moved something. No-op (returns 0) on a body with no route roster / empty
        /// routes / no grid. Never throws. Testable directly (internal).</summary>
        internal static int RunRoutes(Entity body)
        {
            if (body == null || !body.TryGetDataBlob<GroundHaulRouteDB>(out var db) || db.Routes == null || db.Routes.Count == 0)
                return 0;
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            if (grid == null) return 0;
            int movedRoutes = 0;
            foreach (var route in db.Routes)
                if (ApplyRoute(grid, route) > 0) movedRoutes++;
            return movedRoutes;
        }

        /// <summary>Apply ONE standing route on <paramref name="grid"/>: resolve the source + dest hexes and re-issue the
        /// conserved move via the one-shot order's pure core (<see cref="HexHaulOrder.HaulBetweenHexes"/>). Returns the
        /// amount moved. CONSERVED (dst gains exactly what src loses), CAPPED at what's on hand (and at
        /// <see cref="GroundHaulRoute.PerTickCap"/>), and a safe NO-OP (returns 0, never throws) when the source is empty
        /// or either hex is missing. Pure (no entity read) → unit-testable with a hand-built grid.</summary>
        internal static long ApplyRoute(SurfaceGrid grid, GroundHaulRoute route)
        {
            if (grid == null || route == null) return 0;
            var src = grid.HexAt(route.SrcQ, route.SrcR);
            var dst = grid.HexAt(route.DstQ, route.DstR);
            if (src == null || dst == null) return 0;   // a missing hex is a no-op (the world may not have that tile)
            return HexHaulOrder.HaulBetweenHexes(src, dst, route.CargoableId, route.PerTickCap);
        }
    }
}
