using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
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
    }
}
