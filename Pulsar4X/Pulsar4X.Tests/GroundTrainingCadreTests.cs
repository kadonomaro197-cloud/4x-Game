using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// VETERANCY / TRAINING — Slice B: the costed TRAINING CADRE component (the litmus "elite units, with a realistic
    /// training cost"; Blood Angels = veterans). Proves the cadre cradle-to-grave in a STOCK game: the base-mod
    /// `ground-training-cadre` binds a <see cref="GroundTrainingAtb"/> from JSON (the gotcha-10 sensor — the exact check
    /// that catches a template/ctor-arity drift in CI instead of a player's New Game), mounts on a ground unit, and an
    /// assembled unit carrying it fields a VETERAN — its Attack AND toughness multiplied by the cadre's dial vs an
    /// identical green unit — via the shared assembler → <see cref="GroundForces.RaiseUnit"/> bake (Slice A). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class GroundTrainingCadreTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[training] " + m);

        [Test]
        [Description("The base-mod ground-training-cadre loads onto the start faction, binds a GroundTrainingAtb from JSON with the template's default multiplier, and mounts on a ground unit — the six-point gotcha-10 sensor.")]
        public void TrainingCadre_LoadsFromJson_BindsItsAtb_AndMountsOnGroundUnits()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-ground-training-cadre"), Is.True,
                "the training cadre loads (template + component design + earth.json StartingItems + ComponentDesigns wired up)");

            var cadre = (ComponentDesign)designs["default-design-ground-training-cadre"];
            Assert.That(cadre.HasAttribute<GroundTrainingAtb>(), Is.True,
                "the JSON trainingCadreArgs bound a GroundTrainingAtb (template→atb arity path works)");

            var ta = cadre.GetAttribute<GroundTrainingAtb>();
            Log($"cadre: training ×{ta.TrainingMultiplier:0.00}");
            Assert.That(ta.TrainingMultiplier, Is.EqualTo(1.2).Within(1e-9), "template default TrainingMultiplier bound through");
            Assert.That(cadre.ComponentMountType.HasFlag(ComponentMountType.GroundUnit), Is.True, "the cadre mounts on a ground unit");
        }

        [Test]
        [Description("An assembled unit carrying the base-mod training cadre fields a VETERAN — attack + toughness multiplied by the cadre's dial vs an identical green unit; the cadre adds no attack of its own.")]
        public void AssembledUnitWithCadre_IsAVeteran_OutfightsAnIdenticalGreenUnit()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // GREEN: human frame + rifle. VETERAN: the same, PLUS a training cadre (×1.2).
            var green = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-green-squad", "Green Squad",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });
            var veteran = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-veteran-squad", "Veteran Squad",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-ground-training-cadre"), 1),
                });

            // The design carries the cadre's multiplier; the green design is untouched (1.0).
            Assert.That(green.TrainingMultiplier, Is.EqualTo(1.0).Within(1e-9), "no cadre → green design stays 1.0");
            Assert.That(veteran.TrainingMultiplier, Is.EqualTo(1.2).Within(1e-9), "the mounted cadre's ×1.2 flows to the design");

            var greenUnit = GroundForces.RaiseUnit(body, green, s.Faction.Id, 0);
            var vetUnit = GroundForces.RaiseUnit(body, veteran, s.Faction.Id, 0);

            // The cadre is not a weapon — both squads carry the same rifle, so the ONLY difference is the ×1.2 bake.
            Assert.That(greenUnit.Attack, Is.GreaterThan(0), "the rifle gives the green squad real attack");
            Assert.That(vetUnit.Attack, Is.EqualTo(greenUnit.Attack * 1.2).Within(1e-6), "the veteran hits exactly ×1.2 harder");
            Assert.That(vetUnit.MaxHealth, Is.EqualTo(greenUnit.MaxHealth * 1.2).Within(1e-6), "and is exactly ×1.2 tougher");
            Assert.That(vetUnit.TrainingMultiplier, Is.EqualTo(1.2).Within(1e-9), "the readout records the veterancy");
            Log($"green {greenUnit.Attack:0}/{greenUnit.MaxHealth:0}  veteran {vetUnit.Attack:0}/{vetUnit.MaxHealth:0} (×{vetUnit.TrainingMultiplier})");
        }
    }
}
