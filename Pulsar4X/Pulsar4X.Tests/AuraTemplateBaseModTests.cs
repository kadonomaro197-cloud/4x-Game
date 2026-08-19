using NUnit.Framework;
using Pulsar4X.Combat;        // AuraAtb, AuraEffect, AuraTarget
using Pulsar4X.Components;    // ComponentDesign, ComponentInstance
using Pulsar4X.Datablobs;     // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E14 auras — the BUILDABLE aura template (base-mod), through the REAL data path. The starting colony now lists a
    /// buildable <c>default-design-aura-command-post</c>; this proves it loads onto the faction and binds its
    /// <see cref="AuraAtb"/> from JSON via the ComponentDesigner (template → NCalc → atb, gotcha #10 — the six-point
    /// registration), including the enum Effect/Target dials, AND that the real building feeds the ground command-aura
    /// fold (<see cref="GroundCommandAura"/>). The client is CI-blind, so a mis-ordered <c>AtbConstrArgs</c>, a wrong
    /// AttributeType namespace, a bad ctor, or a wrong enum <c>MaxFormula</c> fails HERE, not in a player's New Game.
    /// </summary>
    [TestFixture]
    public class AuraTemplateBaseModTests
    {
        [Test]
        [Description("The Command Aura Post loads onto the start faction and binds its AuraAtb from JSON with the "
                     + "Magnitude + enum dials (Effect=Command, Target=Friends).")]
        public void AuraCommandPost_LoadsFromJson_BindsItsAtb_WithDials()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-aura-command-post"), Is.True,
                "the aura command post loads onto the faction — the six-point registration is wired (template in "
                + "StartingItems, design in ComponentDesigns, materials stocked)");

            var design = designs["default-design-aura-command-post"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-aura-command-post is a ComponentDesign");

            Assert.That(design.HasAttribute<AuraAtb>(), Is.True,
                "the design binds an AuraAtb — the AttributeType FQN resolved and the 4 ctor args matched");

            var atb = design.GetAttribute<AuraAtb>();
            TestContext.Progress.WriteLine(
                $"[aura-post] effect={atb.Effect} target={atb.Target} magnitude={atb.Magnitude} radius={atb.Radius_m}");

            Assert.That(atb.Effect, Is.EqualTo(AuraEffect.Command), "the enum Effect dial bound from the template default (index 2 = Command)");
            Assert.That(atb.Target, Is.EqualTo(AuraTarget.Friends), "the enum Target dial bound from the template default (index 0 = Friends)");
            Assert.That(atb.Magnitude, Is.EqualTo(0.5).Within(1e-9), "magnitude bound from the template default");
        }

        [Test]
        [Description("The REAL built aura post feeds the ground fold: installed on the colony, GroundCommandAura.MultFor "
                     + "reads a >1 firepower buff for that faction — flag-gated (off → byte-identical).")]
        public void AuraCommandPost_Installed_FeedsTheGroundFold()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var design = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns["default-design-aura-command-post"] as ComponentDesign;
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            comps.AddComponentInstance(new ComponentInstance(design));

            bool saved = GroundCommandAura.EnableGroundCommandAura;
            try
            {
                GroundCommandAura.EnableGroundCommandAura = true;
                Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Command), Is.EqualTo(1.5).Within(1e-9),
                    "a built Command aura post (+0.5) → ×1.5 firepower for its faction's battalions");
                Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Ward), Is.EqualTo(1.0).Within(1e-9),
                    "a Command post doesn't answer a Ward (toughness) query");

                GroundCommandAura.EnableGroundCommandAura = false;
                Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Command), Is.EqualTo(1.0).Within(1e-9),
                    "flag off → no buff (byte-identical)");
                TestContext.Progress.WriteLine("[aura-post] the built base-mod post feeds the ground command-aura fold");
            }
            finally { GroundCommandAura.EnableGroundCommandAura = saved; }
        }

        [Test]
        [Description("The SPACE Command Aura Projector (aura-projector) loads onto the start faction and binds its "
                     + "AuraAtb from JSON with the Magnitude + enum dials (Effect=Command, Target=Friends) — the ship "
                     + "twin of the colony command post, same gotcha-10 six-point registration.")]
        public void AuraProjector_LoadsFromJson_BindsItsAtb_WithDials()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-aura-projector"), Is.True,
                "the aura projector loads onto the faction — its template is in StartingItems + its design in ComponentDesigns");

            var design = designs["default-design-aura-projector"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-aura-projector is a ComponentDesign");

            Assert.That(design.HasAttribute<AuraAtb>(), Is.True,
                "the design binds an AuraAtb — the AttributeType FQN resolved and the 4 ctor args matched");

            var atb = design.GetAttribute<AuraAtb>();
            TestContext.Progress.WriteLine(
                $"[aura-projector] effect={atb.Effect} target={atb.Target} magnitude={atb.Magnitude} radius={atb.Radius_m}");

            Assert.That(atb.Effect, Is.EqualTo(AuraEffect.Command), "the enum Effect dial bound from the template default (index 2 = Command)");
            Assert.That(atb.Target, Is.EqualTo(AuraTarget.Friends), "the enum Target dial bound from the template default (index 0 = Friends)");
            Assert.That(atb.Magnitude, Is.EqualTo(0.5).Within(1e-9), "magnitude bound from the template default");
        }
    }
}
