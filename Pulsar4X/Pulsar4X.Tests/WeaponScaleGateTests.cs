using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-designer RESEARCH GATE for the SPACE weapons — the top of each weapon's scale is earned by research, not
    /// given. Mirrors the ground-weapon gate (`GroundUnitPartsBaseModTests.GroundWeapon_AttackCeiling_RisesWithResearch`):
    /// a cap that was a flat number now reads `TechData('...')`, so incrementing the tech RAISES the ceiling. Each gauge
    /// also proves the tech is UNLOCKED at start (TechData resolves without the KeyNotFound crash). Engine-only → CI.
    /// Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md §6a-ii (weapon-designer scale span, task #2).
    /// </summary>
    [TestFixture]
    public class WeaponScaleGateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[wpn-gate] " + m);

        // Build the template fresh and read one knob's tech-driven ceiling at the faction's CURRENT tech level.
        private static double Ceiling(TestScenario s, string templateId, string propertyName)
        {
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var tmpl = data.ComponentTemplates.ContainsKey(templateId)
                ? data.ComponentTemplates[templateId]
                : data.LockedComponentTemplates[templateId];
            var dz = new ComponentDesigner(tmpl, data, s.Faction.GetDataBlob<FactionTechDB>());
            var p = dz.ComponentDesignProperties[propertyName];
            p.SetMax();   // evaluate MaxFormula = TechData('...') at the current tech level
            return p.MaxValue;
        }

        [Test]
        [Description("Weapon designer is ONE category, not split by setting (developer's call: 'ground weapons isnt a design component category'). Ground weapons and space weapons now share the same ComponentType ('Weapon'), so ComponentDesignWindow groups them under a single 'Weapon' tab — you pick a weapon and spec it, you don't choose 'ground weapon' vs 'space weapon'. The MOUNT (GroundUnit vs ShipComponent) decides where each can go, not the designer category.")]
        public void Weapons_ShareOneDesignerCategory_NotSplitBySetting()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            string TypeOf(string id) => (data.ComponentTemplates.ContainsKey(id)
                ? data.ComponentTemplates[id]
                : data.LockedComponentTemplates[id]).ComponentType;

            Assert.That(TypeOf("laser-weapon"), Is.EqualTo("Weapon"), "the beam is a 'Weapon'");
            Assert.That(TypeOf("ground-rifle"), Is.EqualTo("Weapon"), "the rifle is a 'Weapon' too — NOT a separate 'Ground Weapon' category");
            Assert.That(TypeOf("ground-cannon"), Is.EqualTo(TypeOf("railgun-weapon")),
                "ground and space weapons live in the SAME designer category — one weapon designer, pick a type");
        }

        [Test]
        [Description("BEAM range: the laser's Range ceiling is TechData-driven (tech-beam-range), so RESEARCH raises how far a beam reaches — the developer's 'long range is earned, not given' principle. Level 0 == the previous flat cap (start unchanged); +1 research level raises it. Starting cap + growth are flagged tunables in techs.json.")]
        public void BeamRangeCeiling_RisesWithResearch()
        {
            var s = TestScenario.CreateWithColony();
            double before = Ceiling(s, "laser-weapon", "Range");
            Assert.That(before, Is.GreaterThan(0),
                "the beam Range ceiling resolves — tech-beam-range is unlocked at start (no KeyNotFound crash from TechData)");

            s.Faction.GetDataBlob<FactionInfoDB>().Data.IncrementTechLevel("tech-beam-range");
            double after = Ceiling(s, "laser-weapon", "Range");
            Assert.That(after, Is.GreaterThan(before),
                "researching Beam Focusing Range raises the reach — long range is earned, not handed out at game start");
            Log($"beam range ceiling: {before:0} m (start) -> {after:0} m (after +1 research level)");
        }
    }
}
