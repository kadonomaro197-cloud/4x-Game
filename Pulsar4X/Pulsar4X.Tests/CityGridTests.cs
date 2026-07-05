using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// City sub-grid (C1) — the fine-grid data layer. A developed operational hex gets its own fine tile grid
    /// (`GroundHex.CityGrid`, lazy + save-safe), buildings sit on tiles 1:1, and the set of placed buildings is kept
    /// == the operational hex's `InstallationIds` (the roll-up invariant that ties the two zooms together). These
    /// gauges prove: lazy gen + scaling + clone-safety; place/remove keeps the roll-up in sync; develop lays a colony's
    /// buildings onto tiles; and a BOMBED operational hex clears the fine tile too. Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class CityGridTests
    {
        private static ComponentInstance InstallBunker(TestScenario s)
        {
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var design = (ComponentDesign)fi.IndustryDesigns["default-design-bunker"];
            var inst = new ComponentInstance(design);
            s.Colony.AddComponent(inst);
            return inst;
        }

        [Test]
        [Description("C1 (pure): the fine-city tile count is the hex-disk formula (3r²+3r+1); r=6 → 127 tiles.")]
        public void CityTileCount_IsTheHexDiskFormula()
        {
            Assert.That(CityGridFactory.CityTileCount(0), Is.EqualTo(1));
            Assert.That(CityGridFactory.CityTileCount(1), Is.EqualTo(7));
            Assert.That(CityGridFactory.CityTileCount(6), Is.EqualTo(127));
        }

        [Test]
        [Description("C1 (data): a GroundHex's city grid deep-copies on clone — a developed hex is save-safe, and mutating a clone's tile doesn't touch the original.")]
        public void CityGrid_DeepCopiesOnHexClone()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Plains);
            var grid = new CityGrid(0, 0, 0, 1);
            grid.Tiles.Add(new CityTile(0, 0, RegionFeatureType.Plains));
            hex.CityGrid = grid;
            hex.CityGrid.Tiles[0].BuildingInstanceId = 7;

            var clone = new GroundHex(hex);
            Assert.That(clone.CityGrid, Is.Not.Null);
            Assert.That(clone.CityGrid.Tiles[0].BuildingInstanceId, Is.EqualTo(7), "the clone carries the tile's building");
            clone.CityGrid.Tiles[0].BuildingInstanceId = 99;
            Assert.That(hex.CityGrid.Tiles[0].BuildingInstanceId, Is.EqualTo(7), "the copy is DEEP — mutating the clone doesn't touch the original");
        }

        [Test]
        [Description("C1 (gen): developing a colony's capital operational hex generates a fine city grid lazily (127 tiles at r=6, all buildable), idempotent (re-ensuring returns the same grid).")]
        public void EnsureCityForHex_GeneratesLazily_AndIsIdempotent()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var opHex = regionsDB.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            Assert.That(opHex.CityGrid, Is.Null, "an undeveloped hex has no fine grid (lazy — costs nothing)");

            var grid = CityGridFactory.EnsureCityForHex(body, 0, 0, 0);
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.Tiles.Count, Is.EqualTo(CityGridFactory.CityTileCount(CityGridFactory.CityPatchRadius)));
            Assert.That(grid.Tiles.All(t => t.Terrain == opHex.Terrain), Is.True, "v1 tiles inherit the operational hex's terrain");
            Assert.That(opHex.CityGrid, Is.SameAs(grid), "the grid hangs off the operational hex");

            var again = CityGridFactory.EnsureCityForHex(body, 0, 0, 0);
            Assert.That(again, Is.SameAs(grid), "idempotent — re-ensuring returns the same grid, doesn't rebuild");
        }

        [Test]
        [Description("C1 (the roll-up invariant): placing a building on a fine tile adds it to the operational hex's InstallationIds; removing it drops it from both; a second building can't take an occupied tile.")]
        public void PlaceAndRemove_KeepTheRollUpInSync()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var opHex = regionsDB.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            foreach (var h in regionsDB.Regions[0].Hexes) h.InstallationIds.Clear();

            const int buildingId = 123456;
            Assert.That(CityBuilder.PlaceBuildingOnTile(body, 0, 0, 0, 1, 0, buildingId), Is.True, "placed on an empty tile");
            Assert.That(opHex.InstallationIds, Does.Contain(buildingId), "roll-up: the building shows on the operational hex");
            Assert.That(opHex.CityGrid.TileAt(1, 0).BuildingInstanceId, Is.EqualTo(buildingId), "and sits on that fine tile");

            Assert.That(CityBuilder.PlaceBuildingOnTile(body, 0, 0, 0, 1, 0, 999), Is.False, "1:1 — an occupied tile refuses a second building");

            Assert.That(CityBuilder.RemoveBuildingFromTile(body, 0, 0, 0, 1, 0), Is.True, "removed");
            Assert.That(opHex.InstallationIds, Does.Not.Contain(buildingId), "roll-up: gone from the operational hex too");
            Assert.That(opHex.CityGrid.TileAt(1, 0).BuildingInstanceId, Is.EqualTo(-1), "and the tile is empty");
        }

        [Test]
        [Description("C1 (develop): DevelopColonyHex lays the capital hex's existing footprint buildings (W-track located them there) onto fine tiles — the coarse 'it's here' becomes the fine 'it's on THIS tile'. Idempotent.")]
        public void DevelopColonyHex_LaysFootprintBuildingsOntoTiles()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var opHex = regionsDB.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            foreach (var h in regionsDB.Regions[0].Hexes) h.InstallationIds.Clear();
            regionsDB.Regions[0].InstallationIds.Clear();

            var bunker = InstallBunker(s);
            regionsDB.Regions[0].InstallationIds.Add(bunker.ID);
            GroundBuildings.LocateFootprintsOnHexes(s.Colony);          // W-track: bunker onto the operational hex
            Assert.That(opHex.InstallationIds, Does.Contain(bunker.ID));

            int placed = CityBuilder.DevelopColonyHex(s.Colony);
            Assert.That(placed, Is.GreaterThanOrEqualTo(1), "the bunker is laid onto a fine tile");
            Assert.That(opHex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID), Is.True, "it now sits on a specific city tile");
            Assert.That(CityBuilder.DevelopColonyHex(s.Colony), Is.EqualTo(0), "idempotent — re-developing places nothing new");
        }

        [Test]
        [Description("C1 (grave rung / invariant): bombing the operational hex destroys the building AND empties the fine city tile it sat on — the two zooms stay honest (bomb-damages-contents reaches all the way down).")]
        public void BombardingOperationalHex_ClearsTheFineCityTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var opHex = regionsDB.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            foreach (var h in regionsDB.Regions[0].Hexes) h.InstallationIds.Clear();
            regionsDB.Regions[0].InstallationIds.Clear();

            var bunker = InstallBunker(s);
            regionsDB.Regions[0].InstallationIds.Add(bunker.ID);
            GroundBuildings.LocateFootprintsOnHexes(s.Colony);
            CityBuilder.DevelopColonyHex(s.Colony);
            Assert.That(opHex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID), Is.True, "bunker is on a fine tile before the strike");

            int destroyed = GroundBuildings.BombardHex(body, 0, 0, 0, strength: 5.0);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(opHex.InstallationIds, Is.Empty, "gone from the operational roll-up");
            Assert.That(opHex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID), Is.False,
                "and the fine city tile it sat on is empty — the roll-up invariant holds through the grave rung");
        }

        // ── GLOBAL grid (G4) — the same city/roll-up/grave-rung on the ONE continuous cylinder ─────────────────────

        [Test]
        [Description("G4 (global city gen): EnsureCityForGlobalHex resolves a hex by GLOBAL (Q,R) on the cylinder grid and builds its fine city (127 tiles at r=6, terrain inherited), idempotent, labelled with the correct column band.")]
        public void EnsureCityForGlobalHex_GeneratesOnTheCylinder_AndIsIdempotent()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            int rc = regionsDB.Regions.Count;

            var grid = PlanetGridFactory.EnsureGridForBody(body);
            Assert.That(grid, Is.Not.Null, "the cylinder grid generates");
            int gQ = PlanetGridFactory.BandCentreColumn(1, grid.Cols, rc);   // band 1's muster column
            int gR = grid.Rows / 2;
            var opHex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);
            Assert.That(opHex, Is.Not.Null, "the global hex resolves");
            Assert.That(opHex.CityGrid, Is.Null, "undeveloped — lazy, costs nothing");

            var city = CityGridFactory.EnsureCityForGlobalHex(body, gQ, gR);
            Assert.That(city, Is.Not.Null);
            Assert.That(city.Tiles.Count, Is.EqualTo(CityGridFactory.CityTileCount(CityGridFactory.CityPatchRadius)));
            Assert.That(city.Tiles.All(t => t.Terrain == opHex.Terrain), Is.True, "v1 tiles inherit the operational hex's terrain");
            Assert.That(city.RegionIndex, Is.EqualTo(PlanetGridFactory.RegionOfColumn(opHex.Q, grid.Cols, rc)),
                "the city is labelled with the column band its hex falls in");
            Assert.That(CityGridFactory.EnsureCityForGlobalHex(body, gQ, gR), Is.SameAs(city), "idempotent");
        }

        [Test]
        [Description("G4 (global roll-up + grave rung): placing a building on a global hex's fine tile rolls up to that hex's InstallationIds; bombing the global hex destroys the building AND empties the fine tile — the two-zoom invariant holds on the cylinder exactly as on the disks.")]
        public void GlobalPlaceRollsUp_AndGlobalBombardClearsTheTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            int rc = regionsDB.Regions.Count;
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int gQ = PlanetGridFactory.BandCentreColumn(0, grid.Cols, rc);
            int gR = grid.Rows / 2;
            var opHex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);

            var bunker = InstallBunker(s);
            Assert.That(CityBuilder.PlaceBuildingOnGlobalTile(body, gQ, gR, 1, 0, bunker.ID), Is.True, "placed on an empty global tile");
            Assert.That(opHex.InstallationIds, Does.Contain(bunker.ID), "roll-up: shows on the operational (global) hex");
            Assert.That(opHex.CityGrid.TileAt(1, 0).BuildingInstanceId, Is.EqualTo(bunker.ID), "and sits on that fine tile");
            Assert.That(CityBuilder.PlaceBuildingOnGlobalTile(body, gQ, gR, 1, 0, 999), Is.False, "1:1 — occupied tile refuses a second");

            int destroyed = GroundBuildings.BombardGlobalHex(body, gQ, gR, strength: 5.0);
            Assert.That(destroyed, Is.EqualTo(1), "the bunker is destroyed by the strike");
            Assert.That(opHex.InstallationIds, Does.Not.Contain(bunker.ID), "gone from the global roll-up");
            Assert.That(opHex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID), Is.False,
                "and the fine tile it sat on is empty — the invariant holds through the grave rung on the cylinder");
        }

        [Test]
        [Description("C-track (global develop): LocateFootprintsOnGlobalHexes drops a colony's footprint building onto its region band's muster hex on the cylinder; DevelopGlobalHex then lays it onto a fine city tile — the coarse 'it's in this band' becomes the fine 'it's on THIS mini-hex'.")]
        public void LocateAndDevelop_OnTheGlobalGrid_LaysFootprintsOntoMiniHexTiles()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int rc = regionsDB.Regions.Count;

            var bunker = InstallBunker(s);
            regionsDB.Regions[0].InstallationIds.Add(bunker.ID);   // the colony located it in the capital band

            int located = GroundBuildings.LocateFootprintsOnGlobalHexes(s.Colony);
            Assert.That(located, Is.GreaterThanOrEqualTo(1), "the footprint bunker lands on the global grid");
            int gQ = PlanetGridFactory.BandCentreColumn(0, grid.Cols, rc), gR = grid.Rows / 2;
            var musterHex = grid.HexAt(gQ, gR);
            Assert.That(musterHex.InstallationIds, Does.Contain(bunker.ID), "it sits on the capital band's muster hex");
            Assert.That(GroundBuildings.LocateFootprintsOnGlobalHexes(s.Colony), Is.EqualTo(0), "idempotent — already located");

            int laid = CityBuilder.DevelopGlobalHex(body, gQ, gR);
            Assert.That(laid, Is.GreaterThanOrEqualTo(1), "developing the hex lays the bunker onto a fine tile");
            Assert.That(musterHex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID), Is.True, "it now sits on a specific mini-hex tile");
            Assert.That(CityBuilder.DevelopGlobalHex(body, gQ, gR), Is.EqualTo(0), "idempotent — re-developing lays nothing new");
        }

        [Test]
        [Description("C-track (per-tile placement): LocateFootprintsOnGlobalHex puts a colony's footprint building on the SPECIFIC hex the player zoomed into (leaving it un-placed, not auto-laid), then PlaceBuildingOnGlobalTile drops it on a chosen mini-hex tile — the 'plot where I'll build' flow. Idempotent (a located building isn't re-located).")]
        public void LocateOnChosenHex_ThenPlaceOnChosenTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int rc = regionsDB.Regions.Count;
            int gQ = PlanetGridFactory.BandCentreColumn(2, grid.Cols, rc) + 1;   // a hex that ISN'T a band-centre muster hex
            int gR = grid.Rows / 2 + 1;
            var hex = grid.HexAt(gQ, gR);

            var bunker = InstallBunker(s);
            int located = GroundBuildings.LocateFootprintsOnGlobalHex(s.Colony, gQ, gR);
            Assert.That(located, Is.GreaterThanOrEqualTo(1), "footprint buildings land on the chosen hex");
            Assert.That(hex.InstallationIds, Does.Contain(bunker.ID), "the bunker is on THIS hex (the one we targeted), not the muster hex");
            bool onTile = hex.CityGrid != null && hex.CityGrid.Tiles.Any(t => t.BuildingInstanceId == bunker.ID);
            Assert.That(onTile, Is.False, "located but NOT auto-placed on a tile — per-tile placement is manual");
            Assert.That(GroundBuildings.LocateFootprintsOnGlobalHex(s.Colony, gQ, gR), Is.EqualTo(0), "idempotent — already located, not re-located");

            Assert.That(CityBuilder.PlaceBuildingOnGlobalTile(body, gQ, gR, 1, 0, bunker.ID), Is.True, "placed on the chosen tile (1,0)");
            Assert.That(hex.CityGrid.TileAt(1, 0).BuildingInstanceId, Is.EqualTo(bunker.ID), "it now sits on that specific mini-hex tile");
            Assert.That(hex.InstallationIds, Does.Contain(bunker.ID), "roll-up still holds");
        }
    }
}
