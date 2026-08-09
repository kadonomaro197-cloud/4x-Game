using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;           // PlanetRegionsFactory
using Pulsar4X.DataStructures;   // ComponentMountType
using Pulsar4X.Hazards;          // HazardEffectType
using Pulsar4X.GroundCombat;     // PlanetEnvironmentsDB, RegionEnvironment, GroundHardeningAtb, the assembler

namespace Pulsar4X.Tests
{
    /// <summary>
    /// ENVIRONMENTAL HARDENING — the second surface-hazard armour, the twin of the G4 sealed-systems component. The
    /// planet-view study found the gap: the seal covers only Vacuum + ToxicAtmosphere, so a fully SEALED marine still
    /// BURNS on Venus (fire = HeatDamage) and FREEZES on Ganymede, and dissolves in a corrosive superstorm
    /// (CorrosiveDamage). This proves the fix cradle-to-grave in a STOCK game: the base-mod `environmental-hardening`
    /// binds a <see cref="GroundHardeningAtb"/> from JSON (the gotcha-10 sensor), mounts on a ground unit, and an
    /// ASSEMBLED unit carrying it fields a HARDENED force — the assembler folds the mounted dial into the design's
    /// <c>EnvironmentalResistance {HeatDamage, CorrosiveDamage}</c>, so it SURVIVES a fire + corrosive world that BLEEDS
    /// its unhardened twin AND its merely-SEALED twin (the seal negates neither heat nor corrosion — the exact finding
    /// this component closes). Engine-only → CI. Byte-identical absent a mounted hardening.
    /// </summary>
    [TestFixture]
    public class GroundHardeningComponentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[harden] " + m);

        /// <summary>The template's default Hardening dial (installations.json `environmental-hardening`).</summary>
        private const double TemplateHardening = 0.9;

        [Test]
        [Description("The base-mod environmental-hardening loads onto the start faction, binds a GroundHardeningAtb from JSON with the template's default Hardening, and mounts on a ground unit — the six-point gotcha-10 sensor.")]
        public void EnvironmentalHardening_LoadsFromJson_BindsItsAtb_AndMountsOnGroundUnits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-environmental-hardening"), Is.True,
                "the hardening loads (template + component design + earth.json StartingItems + ComponentDesigns wired up)");

            var hard = (ComponentDesign)designs["default-design-environmental-hardening"];
            Assert.That(hard.HasAttribute<GroundHardeningAtb>(), Is.True,
                "the JSON environmentalHardeningArgs bound a GroundHardeningAtb (template→atb arity path works)");

            var ha = hard.GetAttribute<GroundHardeningAtb>();
            Log($"hardening: {ha.Hardening:P0}");
            Assert.That(ha.Hardening, Is.EqualTo(TemplateHardening).Within(1e-9), "template default Hardening bound through");
            Assert.That(hard.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, "the hardening mounts on a ground unit");
        }

        [Test]
        [Description("An assembled hardened unit gets EnvironmentalResistance {HeatDamage, CorrosiveDamage} and, on a fire + corrosive world, outlasts BOTH its unhardened twin AND its merely-sealed twin (the seal negates neither heat nor corrosion).")]
        public void HardenedUnit_SurvivesFireAndCorrosive_WhereSealedAndOpenTwinsBleed()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // OPEN: frame + rifle. SEALED: + the seal (vacuum/toxic only). HARDENED: + the hardening (heat/corrosive).
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
            var hardenedDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-hardened-squad", "Hardened Squad",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-environmental-hardening"), 1),
                });

            // The assembler folds the mounted hardening into the design; the seal writes DIFFERENT keys (not heat/corrosive).
            Assert.That(open.EnvironmentalResistance, Is.Empty, "no kit → the open design carries no environmental resistance");
            Assert.That(sealedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.HeatDamage), Is.False,
                "the SEAL does NOT cover fire/cryo (HeatDamage) — the gap this component closes");
            Assert.That(sealedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.CorrosiveDamage), Is.False,
                "nor corrosion — a sealed marine still burns/dissolves");
            Assert.That(hardenedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.HeatDamage), Is.True, "hardening folds HeatDamage in");
            Assert.That(hardenedDesign.EnvironmentalResistance.ContainsKey(HazardEffectType.CorrosiveDamage), Is.True, "and CorrosiveDamage");
            Assert.That(hardenedDesign.EnvironmentalResistance[HazardEffectType.HeatDamage], Is.EqualTo(TemplateHardening).Within(1e-9),
                "the mounted hardening's 0.9 flows to the design's HeatDamage resistance");
            Assert.That(hardenedDesign.EnvironmentalResistance[HazardEffectType.CorrosiveDamage], Is.EqualTo(TemplateHardening).Within(1e-9),
                "and its CorrosiveDamage resistance");

            // Field all three on region 0 of a fire + corrosive world (a Venus-flavoured surface): 4/hr fire + 6/hr acid.
            if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                envDB = new PlanetEnvironmentsDB();
                body.SetDataBlob(envDB);
            }
            envDB.Environments.Add(new RegionEnvironment(0, "Fire Tornadoes", HazardEffectType.HeatDamage, 4.0));
            envDB.Environments.Add(new RegionEnvironment(0, "Corrosive Superstorm", HazardEffectType.CorrosiveDamage, 6.0));

            var openUnit = GroundForces.RaiseUnit(body, open, s.Faction.Id, 0);
            var sealedUnit = GroundForces.RaiseUnit(body, sealedDesign, s.Faction.Id, 0);
            var hardUnit = GroundForces.RaiseUnit(body, hardenedDesign, s.Faction.Id, 0);
            Assert.That(hardUnit.ResistanceTo(HazardEffectType.HeatDamage), Is.EqualTo(TemplateHardening).Within(1e-9),
                "the design's resistance snapshots onto the raised unit");

            double open0 = openUnit.Health, sealed0 = sealedUnit.Health, hard0 = hardUnit.Health;
            new GroundForcesProcessor().ProcessEntity(body, 3600);   // one hour of attrition

            double openBleed = open0 - openUnit.Health;
            double sealedBleed = sealed0 - sealedUnit.Health;
            double hardBleed = hard0 - hardUnit.Health;
            Log($"one hour: open -{openBleed:0.0}, sealed -{sealedBleed:0.0}, hardened -{hardBleed:0.0} hp");

            Assert.That(openBleed, Is.EqualTo(10.0).Within(1.0), "unhardened: full 4+6 = 10/hr from fire + corrosive");
            Assert.That(sealedBleed, Is.EqualTo(openBleed).Within(0.01), "SEALED bleeds the SAME as open — the seal negates neither heat nor corrosion");
            // 0.9 hardening → only 10% of the attrition lands: 10 × (1−0.9) = 1.0/hr.
            Assert.That(hardBleed, Is.EqualTo(1.0).Within(0.3), "hardened: 0.9 shell negates 90% → ~1.0/hr");
            Assert.That(hardUnit.Health, Is.GreaterThan(sealedUnit.Health), "the hardened unit outlasts the merely-sealed one on a fire/corrosive world");
        }
    }
}
