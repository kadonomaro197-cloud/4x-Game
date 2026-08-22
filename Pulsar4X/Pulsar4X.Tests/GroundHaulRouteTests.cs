using NUnit.Framework;
using Newtonsoft.Json;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// DS-T1 STANDING intra-planet haul route (`docs/ground/UNITS-ON-THE-MAP-DESIGN.md` §3). The game already has a
    /// ONE-SHOT haul (<see cref="HexHaulOrder"/>): click once, ore moves once. DS-T1 adds the STANDING version — set a
    /// route once (<see cref="GroundHaulRoutes.AddRoute"/>) and <see cref="GroundHaulRouteProcessor"/> re-issues the same
    /// conserved hex→hex move every tick, so ore keeps flowing from a mining hex to a depot hex without re-clicking.
    ///
    /// The processor does NOT re-implement hauling — it calls the one-shot order's pure core
    /// (<c>HexHaulOrder.HaulBetweenHexes</c>). These gauges pin: the per-tick conserved+capped move (pure
    /// <c>ApplyRoute</c>), the stop-when-empty / missing-hex no-ops, the DataBlob save-safety (Clone deep-copy + JSON
    /// round-trip, L12), and — end-to-end on a REAL body/grid — that a set route flows ore each tick while a body with NO
    /// route is a byte-identical no-op (the processor never touches it).
    /// </summary>
    [TestFixture]
    public class GroundHaulRouteTests
    {
        private const int Iron = 7;      // an ICargoable.ID (the same int GroundHex.Stockpile / DepositMineralId use)
        private const int Copper = 12;

        /// <summary>Build a small row-major cylinder grid by hand (Cols×Rows, all Plains) — the lightest surface a pure
        /// route test can run on: <c>HexAt(q,r)</c> resolves index r*Cols+q, so hexes must be added in row-major order.</summary>
        private static SurfaceGrid MakeGrid(int cols, int rows)
        {
            var grid = new SurfaceGrid(cols, rows);
            for (int r = 0; r < rows; r++)
                for (int q = 0; q < cols; q++)
                    grid.Hexes.Add(new GroundHex(q, r, RegionFeatureType.Plains));
            return grid;
        }

        [Test]
        [Description("A standing route re-issued each tick moves up to its cap, CONSERVED (dst gains exactly what src loses) — so running it twice keeps the ore flowing (the whole point vs the one-shot order).")]
        public void ApplyRoute_MovesEachTick_Conserved_AndCapped()
        {
            var grid = MakeGrid(3, 1);
            var src = grid.HexAt(0, 0);
            var dst = grid.HexAt(2, 0);
            src.AddToStockpile(Iron, 500);
            src.AddToStockpile(Copper, 40);   // a second good the route must not touch

            var route = new GroundHaulRoute(factionOwnerId: 1, srcQ: 0, srcR: 0, dstQ: 2, dstR: 0, cargoableId: Iron, perTickCap: 200);

            // Tick 1: moves exactly the cap, conserved.
            long tick1 = GroundHaulRouteProcessor.ApplyRoute(grid, route);
            Assert.That(tick1, Is.EqualTo(200), "tick 1 moved the cap");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(300), "source fell by exactly the moved amount");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(200), "destination gained exactly that (conserved)");

            // Tick 2: a STANDING route keeps flowing (the one-shot order would be done after tick 1).
            long tick2 = GroundHaulRouteProcessor.ApplyRoute(grid, route);
            Assert.That(tick2, Is.EqualTo(200), "tick 2 moved another cap-load");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(100));
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(400), "ore keeps accumulating at the depot");

            // Conservation holds across the whole run and the other good is untouched.
            Assert.That(src.StockpileOf(Iron) + dst.StockpileOf(Iron), Is.EqualTo(500), "no ore created or destroyed");
            Assert.That(src.StockpileOf(Copper), Is.EqualTo(40), "a good not on the route is untouched");
            Assert.That(dst.StockpileOf(Copper), Is.EqualTo(0), "and never appears at the destination");
        }

        [Test]
        [Description("The last tick is capped at what's actually present, then the route STOPS when the source is empty — no negative, no throw, nothing conjured at the destination.")]
        public void ApplyRoute_StopsWhenSourceEmpty_NoNegativeNoThrow()
        {
            var grid = MakeGrid(2, 1);
            var src = grid.HexAt(0, 0);
            var dst = grid.HexAt(1, 0);
            src.AddToStockpile(Iron, 150);

            var route = new GroundHaulRoute(1, 0, 0, 1, 0, Iron, 200);   // cap (200) > on hand (150)

            long moved = GroundHaulRouteProcessor.ApplyRoute(grid, route);
            Assert.That(moved, Is.EqualTo(150), "capped at what's on hand — take-what-fits");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(0), "source drained");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(150), "destination holds exactly the drained amount");

            // Now empty: the standing route keeps running each tick but moves nothing (no negative, no throw).
            long emptyTick = GroundHaulRouteProcessor.ApplyRoute(grid, route);
            Assert.That(emptyTick, Is.EqualTo(0), "an empty source moves nothing");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(0), "source stays at 0 — never negative");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(150), "destination unchanged — nothing conjured");
        }

        [Test]
        [Description("Missing-hex / null guards: a route pointing off the grid (or a null grid/route) moves nothing and never throws.")]
        public void ApplyRoute_MissingHexOrNull_IsASafeNoOp()
        {
            var grid = MakeGrid(2, 1);
            grid.HexAt(0, 0).AddToStockpile(Iron, 100);

            // Dest row out of range → HexAt returns null → no-op.
            var offGrid = new GroundHaulRoute(1, 0, 0, 0, 99, Iron, 50);
            Assert.That(GroundHaulRouteProcessor.ApplyRoute(grid, offGrid), Is.EqualTo(0), "a route to a nonexistent hex moves nothing");
            Assert.That(grid.HexAt(0, 0).StockpileOf(Iron), Is.EqualTo(100), "the source is untouched");

            Assert.That(GroundHaulRouteProcessor.ApplyRoute(null, offGrid), Is.EqualTo(0), "a null grid is a safe no-op");
            Assert.That(GroundHaulRouteProcessor.ApplyRoute(grid, null), Is.EqualTo(0), "a null route is a safe no-op");
        }

        [Test]
        [Description("Save-safety (L12): the route DataBlob Clones as an independent DEEP copy — mutating the clone's route list or a route's fields must not touch the original.")]
        public void GroundHaulRouteDB_Clone_IsAnIndependentDeepCopy()
        {
            var db = new GroundHaulRouteDB();
            db.Routes.Add(new GroundHaulRoute(1, 0, 0, 3, 2, Iron, 200));
            db.Routes.Add(new GroundHaulRoute(1, 3, 2, 5, 4, Copper, 0));

            var clone = (GroundHaulRouteDB)db.Clone();
            Assert.That(clone.Routes, Has.Count.EqualTo(2), "the clone carries every route");
            Assert.That(clone.Routes[0].SrcQ, Is.EqualTo(0));
            Assert.That(clone.Routes[0].CargoableId, Is.EqualTo(Iron));
            Assert.That(clone.Routes[1].PerTickCap, Is.EqualTo(0));

            // Mutate the CLONE — the original must be untouched (a shallow copy would fail this).
            clone.Routes.Add(new GroundHaulRoute(1, 9, 9, 9, 9, Iron, 1));
            clone.Routes[0].PerTickCap = 9999;
            Assert.That(db.Routes, Has.Count.EqualTo(2), "the original's list is unchanged by the clone's add");
            Assert.That(db.Routes[0].PerTickCap, Is.EqualTo(200), "the original's route is unchanged by the clone's edit");
            Assert.That(ReferenceEquals(db.Routes, clone.Routes), Is.False, "the two lists are distinct objects");
            Assert.That(ReferenceEquals(db.Routes[0], clone.Routes[0]), Is.False, "and so are the route records");
        }

        [Test]
        [Description("Save-safety: the route DataBlob survives a JSON round-trip (the real save path uses [JsonProperty] — every field must persist).")]
        public void GroundHaulRouteDB_SurvivesJsonRoundTrip()
        {
            var db = new GroundHaulRouteDB();
            db.Routes.Add(new GroundHaulRoute(factionOwnerId: 42, srcQ: 1, srcR: 2, dstQ: 3, dstR: 4, cargoableId: Iron, perTickCap: 321));

            var json = JsonConvert.SerializeObject(db);
            var loaded = JsonConvert.DeserializeObject<GroundHaulRouteDB>(json);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Routes, Has.Count.EqualTo(1));
            var r = loaded.Routes[0];
            Assert.That(r.FactionOwnerID, Is.EqualTo(42));
            Assert.That(r.SrcQ, Is.EqualTo(1));
            Assert.That(r.SrcR, Is.EqualTo(2));
            Assert.That(r.DstQ, Is.EqualTo(3));
            Assert.That(r.DstR, Is.EqualTo(4));
            Assert.That(r.CargoableId, Is.EqualTo(Iron));
            Assert.That(r.PerTickCap, Is.EqualTo(321));
        }

        [Test]
        [Description("END-TO-END on a REAL body/grid: a body with NO route is a byte-identical no-op; once a route is set via GroundHaulRoutes.AddRoute, the processor's ProcessEntity re-issues the conserved haul each tick; ClearRoutes stops the flow.")]
        public void Processor_OnARealBody_FlowsOreEachTick_And_NoRouteIsAByteIdenticalNoOp()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            Assert.That(grid, Is.Not.Null, "the starting body has a surface grid");

            // Pick two distinct real hexes on the cylinder (a band centre and its eastward neighbour).
            int q0 = PlanetGridFactory.BandCentreColumn(0, grid.Cols, regionsDB.Regions.Count);
            int r0 = grid.Rows / 2;
            var src = grid.HexAt(q0, r0);
            var dst = grid.HexAt(grid.WrapCol(q0 + 1), r0);
            Assert.That(src, Is.Not.Null); Assert.That(dst, Is.Not.Null);
            Assert.That(ReferenceEquals(src, dst), Is.False, "source and destination are distinct hexes");

            src.AddToStockpile(Iron, 1000);
            long srcSeed = src.StockpileOf(Iron);
            long dstSeed = dst.StockpileOf(Iron);

            var processor = new GroundHaulRouteProcessor();

            // (1) NO ROUTE yet → ProcessEntity is a byte-identical no-op (the body carries no route blob).
            Assert.That(body.HasDataBlob<GroundHaulRouteDB>(), Is.False, "no default body carries the route blob (byte-identical)");
            processor.ProcessEntity(body, 3600);
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(srcSeed), "with no route the processor moves nothing");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(dstSeed));

            // (2) Set a STANDING route → each tick re-issues the conserved haul.
            GroundHaulRoutes.AddRoute(body, s.Faction.Id, src.Q, src.R, dst.Q, dst.R, Iron, perTickCap: 200);
            Assert.That(body.HasDataBlob<GroundHaulRouteDB>(), Is.True, "setting a route created the route roster");

            processor.ProcessEntity(body, 3600);
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(srcSeed - 200), "tick 1 hauled a cap-load off the source");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(dstSeed + 200), "and delivered it to the depot (conserved)");

            processor.ProcessEntity(body, 3600);
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(srcSeed - 400), "tick 2 kept the ore flowing (STANDING route)");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(dstSeed + 400));

            // (3) ClearRoutes stops the flow — the next tick moves nothing.
            GroundHaulRoutes.ClearRoutes(body);
            processor.ProcessEntity(body, 3600);
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(srcSeed - 400), "a cleared route moves nothing further");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(dstSeed + 400));
        }
    }
}
