using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Galaxy;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 3, the RADAR PAYOFF: a unit the player DESIGNED with a radar component
    /// (GroundSensorAtb) reveals the map within the radar's reach. The ability falls out of the unit's component store
    /// (slice 2) — GroundSensors reads it via TryGetComponentsByAttribute, exactly like a ship's sensors — and the
    /// radar's real km range is translated to hexes → region bands on the map. No "scout type"; a scout is any unit you
    /// build with a radar. Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundSensorsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[radar] " + m);

        [Test]
        [Description("A unit designed with a radar reveals its own region (and neighbours within reach) from a fogged world; the reveal falls out of the unit's component store.")]
        public void RadarUnit_RevealsGroundWithinReach()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // A player-designed RADAR component (long range so it reaches past its own region), registered so the unit's
            // backing entity can instantiate it. AttributesByType is the same wiring the JSON loader fills.
            var radar = new ComponentDesign { UniqueID = "test-radar", Name = "Radar" };
            radar.AttributesByType[typeof(GroundSensorAtb)] = new GroundSensorAtb(1_000_000_000);   // huge reach
            faction.IndustryDesigns["test-radar"] = radar;

            // A unit designed AS frame + radar — the ability rides along on the component, not a special unit type.
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-radar-scout", "Radar Scout",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (radar, 1) });

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            foreach (var r in regions.Regions) r.Surveyed = false;   // fog the whole world (internal set via InternalsVisibleTo)
            Assert.That(regions.Regions.Count, Is.GreaterThan(1), "the world has multiple regions to reveal into");

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(unit.BackingEntityId, Is.GreaterThanOrEqualTo(0), "the radar unit has a backing entity carrying the radar");

            int revealed = GroundSensors.RevealFromUnits(body);

            Assert.That(regions.Regions[unit.RegionIndex].Surveyed, Is.True, "the radar reveals the region the unit stands in");
            Assert.That(regions.Regions.Count(r => r.Surveyed), Is.GreaterThanOrEqualTo(3),
                "a long-range radar reveals into neighbouring regions too (real km -> hex -> region bands)");
            Log($"radar revealed {revealed} region(s); {regions.Regions.Count(r => r.Surveyed)}/{regions.Regions.Count} now known");
        }

        [Test]
        [Description("A unit with NO radar reveals nothing — the reveal is keyed off the mounted component, not the unit.")]
        public void UnitWithoutRadar_RevealsNothing()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-noradar", "No Radar",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            foreach (var r in regions.Regions) r.Surveyed = false;
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);

            int revealed = GroundSensors.RevealFromUnits(body);
            Assert.That(revealed, Is.EqualTo(0), "no radar mounted -> nothing revealed");
            Assert.That(regions.Regions.All(r => !r.Surveyed), Is.True, "the world stays fogged");
        }

        [Test]
        [Description("Ground-fog slice 3: a radar scout reveals the region PER-FACTION and unmasks the deposit ASSAY (exact tonnage) in the region it stands in — for its faction only; a faction with no scout there sees neither.")]
        public void RadarScout_RevealsPerFaction_AndUnmasksDepositAssay()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int myId = s.Faction.Id;
            int myMask = faction.FactionMask;
            Assert.That(myMask, Is.Not.Zero, "the start faction must carry a real bit mask for the assay grant to bite");
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // A radar scout, exactly as the reveal test builds one.
            var radar = new ComponentDesign { UniqueID = "test-radar-assay", Name = "Radar" };
            radar.AttributesByType[typeof(GroundSensorAtb)] = new GroundSensorAtb(1_000_000_000);
            faction.IndustryDesigns["test-radar-assay"] = radar;
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-radar-scout-assay", "Radar Scout",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (radar, 1) });

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            foreach (var r in regions.Regions) r.Surveyed = false;

            // Seed a KNOWN masked deposit on a hex in region 0 (where the scout will stand), overriding any auto-seed.
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int regionCount = regions.Regions.Count;
            const long tonnes = 5_000;
            var depositHex = grid.Hexes.First(h => PlanetGridFactory.RegionOfColumn(h.Q, grid.Cols, regionCount) == 0);
            depositHex.DepositMineralId = 7;
            depositHex.DepositAmount = tonnes;
            depositHex.DepositAssay = new Masked<long>(tonnes, AccessLevel.None);

            var unit = GroundForces.RaiseUnit(body, design, myId, 0);
            Assert.That(unit.RegionIndex, Is.EqualTo(0), "the scout stands in region 0, where the deposit is");

            // Full fog before the scout reports.
            Assert.That(regions.IsRegionRevealedFor(myId, 0), Is.False);
            Assert.That(depositHex.AssayFor(myMask), Is.Null, "the assay starts hidden");

            GroundSensors.RevealFromUnits(body);

            // PER-FACTION geography: my faction sees region 0; a phantom faction with no scout does not.
            const int otherId = 987654, otherMask = 1 << 20;
            Assert.That(regions.IsRegionRevealedFor(myId, 0), Is.True, "my scout revealed region 0 to me");
            Assert.That(regions.IsRegionRevealedFor(otherId, 0), Is.False, "a faction with no scout there is still fogged");

            // ASSAY unmasked to FULL for me (boots on the deposit) — but hidden to the scout-less faction.
            Assert.That(depositHex.DepositAssay.Resolve(myMask).IsExact, Is.True, "a ground scout gives the exact assay");
            Assert.That(depositHex.AssayFor(myMask), Is.EqualTo(tonnes), "I now read the real located tonnage");
            Assert.That(depositHex.AssayFor(otherMask), Is.Null, "the scout-less faction still can't read the assay");
        }
    }
}
