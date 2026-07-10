using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the §0b mass-budget cap — SLICE D1 (dossier ⚙11, the Chassis keystone). The hull is the ship's
    /// structural frame — the space echo of the ground <c>GroundChassisAtb</c> — and it carries the ship's mass
    /// budget (<c>ShipHullAtb.MassBudget</c>). D1 registers the hull as a real base-mod component; a later slice
    /// (D2) adds one to each ship, and <c>ShipDesign.Recalculate</c> already reads a mounted hull's budget.
    ///
    /// This is the gotcha-#10 JSON→atb sensor for the hull (the <c>ShieldBaseModTests</c> / <c>RailgunWeaponTests</c>
    /// equivalent): it proves the six-point registration is complete — the template (<c>ship-hull</c>) is unlocked
    /// in the start faction's <c>StartingItems</c>, the design (<c>default-design-ship-hull</c>) loads via
    /// <c>ComponentDesigns</c>, and the template's <c>AtbConstrArgs</c> binds a <c>ShipHullAtb</c> through
    /// reflection with a generous <c>MassBudget</c>. If any registration end were missing, every
    /// <c>CreateWithColony</c> would crash at setup — so this fixture (plus <c>BaseModIntegrityTests</c>) is the net.
    /// </summary>
    [TestFixture]
    public class ShipHullBaseModTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-hull] " + m);

        [Test]
        [Description("D1: the base-mod ship-hull design loads onto the start faction and binds a ShipHullAtb from JSON (template ship-hull → NCalc AtbConstrArgs → Pulsar4X.Ships.ShipHullAtb via reflection), carrying a generous MassBudget and the ship-component mount flag.")]
        public void ShipHullDesign_LoadsFromJson_BindsTheAtb_WithAGenerousMassBudget()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-ship-hull"), Is.True,
                "the ship-hull design loads — the template is in StartingItems and the design in ComponentDesigns (gotcha-10 both ends wired)");

            var design = designs["default-design-ship-hull"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-ship-hull is a ComponentDesign");

            Assert.That(design.HasAttribute<ShipHullAtb>(), Is.True,
                "the JSON shipHullAtbArgs bound a ShipHullAtb (the template → atb reflection path works)");
            var atb = design.GetAttribute<ShipHullAtb>();
            Log($"default-design-ship-hull: massBudget={atb.MassBudget:0} kg, mount={design.ComponentMountType}");

            Assert.That(atb.MassBudget, Is.GreaterThan(0), "the hull carries a mass budget");
            Assert.That(atb.MassBudget, Is.GreaterThanOrEqualTo(50_000_000),
                "a GENEROUS budget (>= 50,000 t) so nothing is ever over budget once ships mount it (the developer's call)");

            Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.ShipComponent), Is.True,
                "the hull is a ship component (mountable on a hull, not a colony installation)");
        }
    }
}
