using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 5c: the two base-mod SCOUT PARTS (`ground-locomotion` + `ground-radar`)
    /// that make the developer's core ask — "a unit that's fast, needs minimal support, uses a radar to reveal the map,
    /// all built from designed components" — buildable in a STOCK game. This is the gotcha-10 JSON→atb sensor for both
    /// (the `GroundUnitBaseModTests` equivalent): if the template args or an atb constructor drift, this goes red in CI
    /// instead of crashing the developer's New Game.
    ///
    /// It also proves the whole cradle rung end-to-end: assemble a scout from ONLY base-mod parts (human frame +
    /// locomotion drive + radar), raise it, and watch the abilities FALL OUT of the component store — the drive sets
    /// its march speed, the radar carries its reveal range — exactly the way a ship's parts do. Engine-only → runs in CI.
    /// Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundScoutPartsBaseModTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[scout-parts] " + m);

        [Test]
        [Description("The base-mod ground-locomotion + ground-radar designs load onto the start faction, each binds its atb from JSON with the template's default dials, and each mounts as a GroundUnit part — the gotcha-10 sensor for the scout parts.")]
        public void ScoutParts_LoadFromJson_BindTheirAtbs_AndMountOnGroundUnits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-ground-locomotion"), Is.True,
                "the locomotion drive loads (template + component design + earth.json entry wired up)");
            Assert.That(designs.ContainsKey("default-design-ground-radar"), Is.True,
                "the radar loads (six-point registration)");

            var loco = (ComponentDesign)designs["default-design-ground-locomotion"];
            var radar = (ComponentDesign)designs["default-design-ground-radar"];

            Assert.That(loco.HasAttribute<GroundLocomotionAtb>(), Is.True,
                "the JSON groundLocomotionArgs bound a GroundLocomotionAtb (template→atb path works)");
            Assert.That(radar.HasAttribute<GroundSensorAtb>(), Is.True,
                "the JSON groundRadarArgs bound a GroundSensorAtb");

            var la = loco.GetAttribute<GroundLocomotionAtb>();
            var ra = radar.GetAttribute<GroundSensorAtb>();
            Log($"locomotion: speed ×{la.SpeedFactor:0.0}, rough {la.RoughHandling:0.00}, amphib {la.Amphibious}");
            Log($"radar: range {ra.Range_km:0} km");

            Assert.That(la.SpeedFactor, Is.EqualTo(1.5).Within(1e-9), "template default SpeedFactor bound through");
            Assert.That(la.RoughHandling, Is.EqualTo(0.5).Within(1e-9), "template default RoughHandling bound through");
            Assert.That(ra.Range_km, Is.EqualTo(300).Within(1e-9), "template default radar Range bound through");

            Assert.That(loco.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, "the drive mounts on a ground unit");
            Assert.That(radar.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, "the radar mounts on a ground unit");
        }

        [Test]
        [Description("Cradle-to-grave: a scout assembled from ONLY base-mod parts (human frame + locomotion drive + radar) raises a unit whose SPEED and RADAR RANGE fall out of its component store — the designed drive overrides Foot, the radar carries its reveal range.")]
        public void ScoutFromBaseModParts_HasSpeedAndRadarFallOut()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var scout = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-basemod-scout", "Recon Scout",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-locomotion"), 1),
                    (Part("default-design-ground-radar"), 1),
                });

            var unit = GroundForces.RaiseUnit(body, scout, s.Faction.Id, 0);

            // Speed falls out of the designed drive (×1.5), overriding the Foot chassis (×1.0).
            Assert.That(GroundMobility.SpeedMultForUnit(body, unit), Is.EqualTo(1.5).Within(1e-9),
                "the base-mod locomotion drive's speed factor falls out and beats Foot");
            Assert.That(GroundMobility.RoughHandlingForUnit(body, unit), Is.EqualTo(0.5).Within(1e-9),
                "rough handling falls out of the same drive");

            // Radar falls out of the backing store, exactly the way a ship finds its sensors.
            Assert.That(GroundUnitEntity.TryGetBacking(body, unit, out var backing), Is.True,
                "the assembled scout has a backing entity carrying its components");
            Assert.That(backing.TryGetDataBlob<Pulsar4X.Datablobs.ComponentInstancesDB>(out var cidb), Is.True);
            Assert.That(cidb.TryGetComponentsByAttribute<GroundSensorAtb>(out var radars) && radars.Count > 0, Is.True,
                "the radar component falls out of the scout's store — the reveal ability is reachable");
            Log($"scout: speed ×{GroundMobility.SpeedMultForUnit(body, unit):0.0}, carries {radars.Count} radar(s)");
        }
    }
}
