using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-2a — the base-mod Command Berth, through the REAL data path. The starting colony now lists a
    /// buildable <c>default-design-command-berth</c>; this proves it loads onto the faction and binds its
    /// <see cref="CommandBerthAtb"/> from JSON via the ComponentDesigner (template → NCalc → atb, gotcha #10 — the
    /// six-point registration), including the enum Role dial (a <c>GuiEnumSelectionList</c>, like the ship bridge's
    /// Admin Level). It also proves install/uninstall correctly seeds and drops the host's <see cref="CommandBerthDB"/>
    /// roster (build the gear / lose the gear — the grave rung). The client is CI-blind, so a mis-ordered
    /// <c>AtbConstrArgs</c>, a wrong AttributeType namespace, or a bad ctor fails HERE, not in a player's New Game.
    /// </summary>
    [TestFixture]
    public class CommandBerthBaseModTests
    {
        [Test]
        [Description("SE-2a: the command berth loads onto the start faction and binds its CommandBerthAtb from JSON with the Role + quality dials.")]
        public void CommandBerth_LoadsFromJson_BindsItsAtb_WithDials()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-command-berth"), Is.True,
                "the command berth loads onto the faction — the six-point registration is wired (template in " +
                "StartingItems, design in ComponentDesigns, materials stocked)");

            var design = designs["default-design-command-berth"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-command-berth is a ComponentDesign");

            Assert.That(design.HasAttribute<CommandBerthAtb>(), Is.True,
                "the design binds a CommandBerthAtb — the AttributeType FQN resolved and the ctor args matched");

            var atb = design.GetAttribute<CommandBerthAtb>();
            TestContext.Progress.WriteLine(
                $"[command-berth] role={atb.Role} grade={atb.Grade} support={atb.Support} " +
                $"survivability={atb.Survivability} span={atb.Span}");

            Assert.That(atb.Role, Is.EqualTo(SiteRole.Science), "the enum Role dial bound from the template default (index 0 = Science)");
            Assert.That(atb.Grade, Is.EqualTo(2), "grade bound from the template default");
            Assert.That(atb.Support, Is.EqualTo(10), "support bound from the template default");
            Assert.That(atb.Survivability, Is.EqualTo(20), "survivability bound from the template default");
            Assert.That(atb.Span, Is.EqualTo(1), "span bound from the template default");
        }

        [Test]
        [Description("SE-2a: installing the berth atb seeds the host's CommandBerthDB roster; uninstalling removes it, dropping the blob when the last berth goes (build/lose the gear).")]
        public void CommandBerthAtb_Install_SeedsRoster_Uninstall_DropsIt()
        {
            var s = TestScenario.CreateWithColony();
            var host = s.Colony;
            var design = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns["default-design-command-berth"] as ComponentDesign;
            var atb = design.GetAttribute<CommandBerthAtb>();
            var instance = new ComponentInstance(design);

            Assert.That(host.HasDataBlob<CommandBerthDB>(), Is.False,
                "a host with no berth built carries no roster (byte-identical / additive)");

            atb.OnComponentInstallation(host, instance);
            Assert.That(host.HasDataBlob<CommandBerthDB>(), Is.True, "installing seeds the berth roster");
            var roster = host.GetDataBlob<CommandBerthDB>();
            Assert.That(roster.Berths.Count, Is.EqualTo(1), "one berth built → one roster entry");

            var berth = roster.BestBerthFor(SiteRole.Science);
            Assert.That(berth, Is.Not.Null, "the science berth is findable by its Role");
            Assert.That(berth.Grade, Is.EqualTo(2), "the berth carries its Grade dial");
            Assert.That(roster.BestBerthFor(SiteRole.Tactical), Is.Null, "a science berth does not answer a tactical query");

            atb.OnComponentUninstallation(host, instance);
            Assert.That(host.HasDataBlob<CommandBerthDB>(), Is.False,
                "the last berth torn down leaves the host with no roster (the grave rung)");
            TestContext.Progress.WriteLine("[command-berth] install→seed→drop verified");
        }

        [Test]
        [Description("SE-2a: BestBerthFor returns the highest-Grade berth matching a Role, or null when none match (pure).")]
        public void BestBerthFor_PicksHighestGradeMatchingRole()
        {
            var roster = new CommandBerthDB();
            roster.Berths.Add(new CommandBerth { Role = SiteRole.Science, Grade = 1, ComponentName = "a" });
            roster.Berths.Add(new CommandBerth { Role = SiteRole.Science, Grade = 3, ComponentName = "b" });
            roster.Berths.Add(new CommandBerth { Role = SiteRole.Tactical, Grade = 9, ComponentName = "c" });

            Assert.That(roster.BestBerthFor(SiteRole.Science).Grade, Is.EqualTo(3), "the higher-grade science berth wins");
            Assert.That(roster.BestBerthFor(SiteRole.Tactical).Grade, Is.EqualTo(9));
            Assert.That(roster.BestBerthFor(SiteRole.Diplomatic), Is.Null, "no diplomatic berth → null");

            // Clone deep-copies the roster (save/load + move-between-managers safety).
            var copy = (CommandBerthDB)roster.Clone();
            copy.Berths.Clear();
            Assert.That(roster.Berths.Count, Is.EqualTo(3), "clearing the copy must not touch the original");
        }
    }
}
