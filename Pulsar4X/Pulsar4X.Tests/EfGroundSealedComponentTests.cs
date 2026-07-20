using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;           // PlanetRegionsFactory
using Pulsar4X.DataStructures;   // ComponentMountType lives here (Engine/DataStructures/Enums.cs)
using Pulsar4X.Hazards;          // HazardEffectType
using Pulsar4X.GroundCombat;     // PlanetEnvironmentsDB, RegionEnvironment, GroundSealAtb, the assembler

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall G4 — SEALED SYSTEMS, the last Space-Marine blocker as a real COMPONENT (not a hand-authored
    /// design). Proves the seal cradle-to-grave in a STOCK game: the base-mod `sealed-systems` binds a
    /// <see cref="GroundSealAtb"/> from JSON (the gotcha-10 sensor — catches a template/ctor-arity drift in CI instead of
    /// a player's New Game), mounts on a ground unit, and an ASSEMBLED unit carrying it fields a SEALED force — the
    /// assembler folds the mounted seal's dial into the design's <c>EnvironmentalResistance</c>, so it SURVIVES a vacuum +
    /// toxic world that BLEEDS its unsealed twin (the E4 attrition counter). The design-authored path is already gauged by
    /// <c>GroundForcesTests.SealedGear_SurvivesVacuumAndToxicAtmosphere</c>; THIS proves the component→assembler→design
    /// wire that makes a player able to DESIGN sealing in the unit designer. Engine-only → CI. Byte-identical absent a
    /// mounted seal (an unsealed assembled unit gets an empty EnvironmentalResistance, so it bleeds exactly as before).
    /// </summary>
    [TestFixture]
    public class EfGroundSealedComponentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[seal] " + m);

        /// <summary>The template's default Sealing dial (installations.json `sealed-systems`).</summary>
        private const double TemplateSealing = 0.9;

        [Test]
        [Description("G4: the base-mod sealed-systems loads onto the start faction, binds a GroundSealAtb from JSON with the template's default Sealing, and mounts on a ground unit — the six-point gotcha-10 sensor.")]
        public void SealedSystems_LoadsFromJson_BindsItsAtb_AndMountsOnGroundUnits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-sealed-systems"), Is.True,
                "the seal loads (template + component design + earth.json StartingItems + ComponentDesigns wired up)");

            var seal = (ComponentDesign)designs["default-design-sealed-systems"];
            Assert.That(seal.HasAttribute<GroundSealAtb>(), Is.True,
                "the JSON sealedSystemsArgs bound a GroundSealAtb (template→atb arity path works)");

            var sa = seal.GetAttribute<GroundSealAtb>();
            Log($"seal: sealing {sa.Sealing:P0}");
            Assert.That(sa.Sealing, Is.EqualTo(TemplateSealing).Within(1e-9), "template default Sealing bound through");
            Assert.That(seal.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, "the seal mounts on a ground unit");
        }

        [Test]
        [Description("G4: an assembled unit carrying the base-mod seal gets EnvironmentalResistance {Vacuum, ToxicAtmosphere} from the assembler (an unsealed twin gets an empty map) and, fielded on a vacuum + toxic world, bleeds a fraction of what the unsealed twin does.")]
        public void AssembledSealedUnit_SurvivesAVacuumWorld_AnUnsealedTwinBleedsOn()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // UNSEALED: human frame + rifle. SEALED: the same, PLUS the sealed-systems component (×0.9 seal).
            var open = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-open-squad", "Open Squad",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });
            var sealedDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-sealed-squad", "Sealed Squad",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-sealed-systems"), 1),
                });

            // The assembler folds the mounted seal into the design; the open design's map stays empty (byte-identical).
            Assert.That(open.EnvironmentalResistance, Is.Empty, "no seal → the open design carries no environmental resistance");
            Assert.That(sealedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.Vacuum), Is.True, "the seal folds Vacuum in");
            Assert.That(sealedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.ToxicAtmosphere), Is.True, "and ToxicAtmosphere");
            Assert.That(sealedDesign.EnvironmentalResistance[HazardEffectType.Vacuum], Is.EqualTo(TemplateSealing).Within(1e-9),
                "the mounted seal's 0.9 flows to the design's Vacuum resistance");
            Assert.That(sealedDesign.EnvironmentalResistance[HazardEffectType.ToxicAtmosphere], Is.EqualTo(TemplateSealing).Within(1e-9),
                "and its ToxicAtmosphere resistance");

            // Field both on region 0 of a vacuum + toxic world (both hazards 3/hr → an unsealed unit takes 6/hr).
            if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                envDB = new PlanetEnvironmentsDB();
                body.SetDataBlob(envDB);
            }
            envDB.Environments.Add(new RegionEnvironment(0, "Vacuum Exposure", HazardEffectType.Vacuum, 3.0));
            envDB.Environments.Add(new RegionEnvironment(0, "Toxic Atmosphere", HazardEffectType.ToxicAtmosphere, 3.0));

            var openUnit = GroundForces.RaiseUnit(body, open, s.Faction.Id, 0);
            var sealedUnit = GroundForces.RaiseUnit(body, sealedDesign, s.Faction.Id, 0);
            Assert.That(sealedUnit.ResistanceTo(HazardEffectType.Vacuum), Is.EqualTo(TemplateSealing).Within(1e-9),
                "the design's resistance snapshots onto the raised unit");

            double open0 = openUnit.Health, sealed0 = sealedUnit.Health;
            new GroundForcesProcessor().ProcessEntity(body, 3600);   // one hour of attrition

            double openBleed = open0 - openUnit.Health;
            double sealedBleed = sealed0 - sealedUnit.Health;
            Assert.That(openBleed, Is.EqualTo(6.0).Within(1.0), "unsealed: full 3+3 = 6/hr from both hazards");
            // 0.9 sealing → only 10% of the attrition lands: 6 × (1−0.9) = 0.6/hr.
            Assert.That(sealedBleed, Is.EqualTo(0.6).Within(0.2), "sealed: 0.9 seal negates 90% → ~0.6/hr");
            Assert.That(sealedUnit.Health, Is.GreaterThan(openUnit.Health), "the sealed unit outlasts the unsealed one");
            Log($"assembled seal: unsealed -{openBleed:0.0}, sealed -{sealedBleed:0.0} hp/hr");
        }
    }
}
