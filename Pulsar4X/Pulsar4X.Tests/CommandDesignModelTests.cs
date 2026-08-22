using NUnit.Framework;
using GameEngine.People;
using Pulsar4X.Sites;
using Pulsar4X.Components.Designers;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the COMMAND door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="CommandDesignModel"/> (the engine half of the command-seat form) REPRODUCES every
    /// base-mod command component's <c>*Atb</c> ctor args AND its emergent stats from the door's two choices (seat class ×
    /// command line) + its sliders — i.e. every hand-authored command part falls out of the one parametric form (the
    /// DESIGNER-NORTH-STAR reproduction claim, made executable). The command door is honestly TWO sub-components today (an
    /// <see cref="AdminSpaceAtb"/> office/bridge and a <see cref="CommandBerthAtb"/> berth), so the collapse is 2 choices +
    /// 4 sliders with the applicable sliders GATED by the seat class.
    ///
    /// PURE — no colony harness (no <c>TestScenario.CreateWithColony</c>), so this stays out of the slow CI shard. The
    /// reproduction VALUES are the ones the base-mod JSON templates compute (verified against
    /// <c>GameData/basemod/TemplateFiles/installations.json</c> for admin-complex + command-berth and <c>storage.json</c>
    /// for ship-command), so a drift in the model's per-class arithmetic fails here. Byte-identical to the live game
    /// (nothing calls the model yet). This mirrors <c>WeaponsDesignModelTests</c>' <c>AssertProfile</c> shape.
    /// </summary>
    [TestFixture]
    public class CommandDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[command-model] " + m);

        /// <summary>Assert the emergent stats + resource costs (identical across all three templates: all a pure function of
        /// mass) and the shared metadata. The seat-class-specific *Atb args are asserted by the two callers below.</summary>
        private static void AssertStats(string name, CommandProfile p, string attrType, string templateId,
            double mass, double volume, double crew, double research, double credit, double buildPoints)
        {
            Log($"{name}: {p.SeatClass} atb={p.AttributeTypeName} tmpl={p.TemplateId} mass={p.Mass} vol={p.Volume} crew={p.CrewReq} rp={p.ResearchCost} cr={p.CreditCost} bp={p.BuildPointCost} htk={p.HTK} " +
                $"[iron={p.IronCost} al={p.AluminiumCost} cu={p.CopperCost} pl={p.PlasticCost} ss={p.StainlessSteelCost}]");
            Assert.That(p.AttributeTypeName, Is.EqualTo(attrType), $"{name} AttributeTypeName");
            Assert.That(p.TemplateId, Is.EqualTo(templateId), $"{name} TemplateId");
            Assert.That(p.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass");
            Assert.That(p.Volume, Is.EqualTo(volume).Within(1e-6), $"{name} Volume (Mass*0.1)");
            Assert.That(p.CrewReq, Is.EqualTo(crew).Within(1e-6), $"{name} CrewReq");
            Assert.That(p.ResearchCost, Is.EqualTo(research).Within(1e-6), $"{name} ResearchCost");
            Assert.That(p.CreditCost, Is.EqualTo(credit).Within(1e-6), $"{name} CreditCost (template constant)");
            Assert.That(p.BuildPointCost, Is.EqualTo(buildPoints).Within(1e-6), $"{name} BuildPointCost ([Mass])");
            Assert.That(p.HTK, Is.EqualTo(0.5).Within(1e-6), $"{name} HTK (template constant)");
            // ResourceCost = [Mass] * {0.5, 0.2, 0.1, 0.1, 0.1} in all three templates.
            Assert.That(p.IronCost, Is.EqualTo(mass * 0.5).Within(1e-6), $"{name} iron");
            Assert.That(p.AluminiumCost, Is.EqualTo(mass * 0.2).Within(1e-6), $"{name} aluminium");
            Assert.That(p.CopperCost, Is.EqualTo(mass * 0.1).Within(1e-6), $"{name} copper");
            Assert.That(p.PlasticCost, Is.EqualTo(mass * 0.1).Within(1e-6), $"{name} plastic");
            Assert.That(p.StainlessSteelCost, Is.EqualTo(mass * 0.1).Within(1e-6), $"{name} stainless-steel");
        }

        /// <summary>An Office/Bridge design feeds AdminSpaceAtb(level, space); its berth args read the -1 "not-applicable"
        /// sentinel (proving the two families are cleanly partitioned).</summary>
        private static void AssertAdminArgs(string name, CommandProfile p, int levelOrdinal, int consoleSpace)
        {
            Assert.That(p.AdminLevelOrdinal, Is.EqualTo(levelOrdinal), $"{name} AdminSpaceAtb arg1 (level)");
            Assert.That(p.ConsoleSpace, Is.EqualTo(consoleSpace), $"{name} AdminSpaceAtb arg2 (space)");
            Assert.That(p.RoleIndex, Is.EqualTo(-1), $"{name} RoleIndex not-applicable");
            Assert.That(p.Grade, Is.EqualTo(-1), $"{name} Grade not-applicable");
            Assert.That(p.Support, Is.EqualTo(-1), $"{name} Support not-applicable");
            Assert.That(p.Survivability, Is.EqualTo(-1), $"{name} Survivability not-applicable");
            Assert.That(p.Span, Is.EqualTo(-1), $"{name} Span not-applicable");
        }

        /// <summary>A Berth design feeds CommandBerthAtb(roleIndex, grade, support, survivability, span); its admin args
        /// read the -1 sentinel.</summary>
        private static void AssertBerthArgs(string name, CommandProfile p, int roleIndex, int grade, int support,
            int survivability, int span)
        {
            Assert.That(p.RoleIndex, Is.EqualTo(roleIndex), $"{name} CommandBerthAtb arg1 (roleIndex)");
            Assert.That(p.Grade, Is.EqualTo(grade), $"{name} CommandBerthAtb arg2 (grade)");
            Assert.That(p.Support, Is.EqualTo(support), $"{name} CommandBerthAtb arg3 (support)");
            Assert.That(p.Survivability, Is.EqualTo(survivability), $"{name} CommandBerthAtb arg4 (survivability)");
            Assert.That(p.Span, Is.EqualTo(span), $"{name} CommandBerthAtb arg5 (span)");
            Assert.That(p.AdminLevelOrdinal, Is.EqualTo(-1), $"{name} AdminLevelOrdinal not-applicable");
            Assert.That(p.ConsoleSpace, Is.EqualTo(-1), $"{name} ConsoleSpace not-applicable");
        }

        [Test]
        [Description("Every base-mod OFFICE (planetary Administrative Complex) falls out of the form: an Office seat + an Admin Level + a Command Space slider reproduces AdminSpaceAtb(level, space) and the admin-complex stats exactly.")]
        public void OfficeComplexes_ReproduceFromTheForm()
        {
            const string atb = CommandDesignModel.AdminAttributeType;
            const string tmpl = CommandDesignModel.OfficeTemplateId;

            // default-design-city-hall: AdminSpaceAtb(5, 1000); Mass = 1000*100 = 100,000.
            var cityHall = CommandDesignModel.Office(AdminLevel.Colony, officeSpace: 1000).BuildProfile();
            AssertStats("city-hall", cityHall, atb, tmpl,
                mass: 100000, volume: 10000, crew: 250, research: 500, credit: 120, buildPoints: 100000);
            AssertAdminArgs("city-hall", cityHall, levelOrdinal: 5, consoleSpace: 1000);

            // default-design-federation-ministry: AdminSpaceAtb(5, 2000); only the Office Space slider moved.
            var ministry = CommandDesignModel.Office(AdminLevel.Colony, officeSpace: 2000).BuildProfile();
            AssertStats("federation-ministry", ministry, atb, tmpl,
                mass: 200000, volume: 20000, crew: 500, research: 1000, credit: 120, buildPoints: 200000);
            AssertAdminArgs("federation-ministry", ministry, levelOrdinal: 5, consoleSpace: 2000);
        }

        [Test]
        [Description("The ship-command bridge template (a coverage point — no shipped ComponentDesign mounts it) falls out of a Bridge seat at Admin Level Ship + a small Console Space: AdminSpaceAtb(0, 5) and the ship-command stats. Proves the model covers the bridge class + the dead-ConsoleSpace path.")]
        public void ShipBridge_ReproducesFromTheForm()
        {
            // ship-command template defaults: Admin Level = Ship(0), Console Space = 5; Mass = 5*100 = 500.
            var bridge = CommandDesignModel.Bridge(AdminLevel.Ship, consoleSpace: 5).BuildProfile();
            AssertStats("ship-bridge", bridge, CommandDesignModel.AdminAttributeType, CommandDesignModel.BridgeTemplateId,
                mass: 500, volume: 50, crew: 1.25, research: 2.5, credit: 120, buildPoints: 500);
            AssertAdminArgs("ship-bridge", bridge, levelOrdinal: 0, consoleSpace: 5);
        }

        [Test]
        [Description("Every base-mod COMMAND BERTH falls out of the form: a Berth seat + a Site Role + the Grade/Support/Survivability sliders reproduces CommandBerthAtb(roleIndex, grade, support, survivability, span) and the command-berth stats exactly. Span is emitted as the fixed constant (a dead dial, never a slider).")]
        public void CommandBerths_ReproduceFromTheForm()
        {
            const string atb = CommandDesignModel.BerthAttributeType;
            const string tmpl = CommandDesignModel.BerthTemplateId;

            // default-design-command-berth 'Science Command Berth': CommandBerthAtb(0,2,10,20,1); Mass = 2*200 = 400.
            var berth = CommandDesignModel.BerthSeat(SiteRole.Science, grade: 2, support: 10, survivability: 20).BuildProfile();
            AssertStats("command-berth", berth, atb, tmpl,
                mass: 400, volume: 40, crew: 4, research: 20, credit: 150, buildPoints: 400);
            AssertBerthArgs("command-berth", berth, roleIndex: 0, grade: 2, support: 10, survivability: 20, span: 1);

            // default-design-mars-high-command 'Martian High Command': Grade=10 override, rest default → CommandBerthAtb(0,10,10,20,1).
            var mars = CommandDesignModel.BerthSeat(SiteRole.Science, grade: 10, support: 10, survivability: 20).BuildProfile();
            AssertStats("mars-high-command", mars, atb, tmpl,
                mass: 2000, volume: 200, crew: 20, research: 100, credit: 150, buildPoints: 2000);
            AssertBerthArgs("mars-high-command", mars, roleIndex: 0, grade: 10, support: 10, survivability: 20, span: 1);
        }

        [Test]
        [Description("STRUCTURAL (North Star, cannot rot): Span is ALWAYS emitted as the fixed constant on a berth, never exposed as a slider (no dead dial ships as a slider); and the two seat families are cleanly partitioned — an Office/Bridge writes only the admin args and a Berth only the berth args.")]
        public void Structural_SpanIsFixed_AndFamiliesArePartitioned()
        {
            // Span is fixed at 1 regardless of the other dials (it writes to nothing — the model never varies it).
            var b1 = CommandDesignModel.BerthSeat(SiteRole.Tactical, grade: 1, support: 0, survivability: 0).BuildProfile();
            var b2 = CommandDesignModel.BerthSeat(SiteRole.Engineering, grade: 10, support: 50, survivability: 100).BuildProfile();
            Assert.That(b1.Span, Is.EqualTo(CommandDesignModel.FixedSpan), "berth1 span is the fixed constant");
            Assert.That(b2.Span, Is.EqualTo(CommandDesignModel.FixedSpan), "berth2 span is the fixed constant");

            // A Berth carries a SiteRole (Tactical = ordinal 1) in the roleIndex arg, and NO admin args.
            Assert.That(b1.RoleIndex, Is.EqualTo((int)SiteRole.Tactical), "berth roleIndex = SiteRole ordinal");
            Assert.That(b1.AdminLevelOrdinal, Is.EqualTo(-1), "a berth writes no admin level");
            Assert.That(b1.ConsoleSpace, Is.EqualTo(-1), "a berth writes no console space");

            // An Office carries an AdminLevel (Empire = ordinal 10) and NO berth args.
            var office = CommandDesignModel.Office(AdminLevel.Empire, officeSpace: 5000).BuildProfile();
            Assert.That(office.AdminLevelOrdinal, Is.EqualTo((int)AdminLevel.Empire), "office admin level = AdminLevel ordinal");
            Assert.That(office.RoleIndex, Is.EqualTo(-1), "an office writes no role");
            Assert.That(office.Grade, Is.EqualTo(-1), "an office writes no grade");

            // The two admin templates share the SAME arithmetic but differ in template id + mount.
            Assert.That(office.AttributeTypeName, Is.EqualTo(CommandDesignModel.AdminAttributeType));
            var bridge = CommandDesignModel.Bridge(AdminLevel.Ship, consoleSpace: 5).BuildProfile();
            Assert.That(bridge.AttributeTypeName, Is.EqualTo(CommandDesignModel.AdminAttributeType));
            Assert.That(office.TemplateId, Is.Not.EqualTo(bridge.TemplateId), "office and bridge are distinct templates");
        }
    }
}
