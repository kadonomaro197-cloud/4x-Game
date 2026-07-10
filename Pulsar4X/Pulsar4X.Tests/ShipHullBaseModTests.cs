using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the §0b mass-budget cap — SLICES D1 + D2a (dossier ⚙11, the Chassis keystone). The hull is the
    /// ship's structural frame — the space echo of the ground <c>GroundChassisAtb</c>. D1 made it a real component;
    /// D2a splits it into three GRADUATED TIERS (light / medium / heavy) off one <c>ship-hull</c> template, each
    /// carrying (a) its own frame MASS (<c>Hull Mass</c>, decoupled from the budget — every hulled ship carries it,
    /// so a bigger hull is heavier and dodges worse) and (b) a mass BUDGET (the ceiling mounted components fit
    /// within). D2b then fits each base-mod ship its tier hull.
    ///
    /// This is the gotcha-#10 JSON→atb sensor for the hull: it proves the registration is complete and the
    /// per-design dial overrides bind through reflection. Nothing mounts a hull yet (D2b), so ship combat is still
    /// byte-identical.
    /// </summary>
    [TestFixture]
    public class ShipHullBaseModTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-hull] " + m);

        // (designId, expected frame mass kg, expected budget kg)
        private static readonly (string id, double frameMass, double budget)[] Tiers =
        {
            ("default-design-ship-hull-light",  2_500,   25_000),
            ("default-design-ship-hull",        10_000,  90_000),   // template defaults = the MEDIUM tier
            ("default-design-ship-hull-heavy",  25_000,  180_000),
        };

        [Test]
        [Description("D2a: the three graduated hull tiers (light/medium/heavy) load onto the start faction, each binds a ShipHullAtb from JSON with its overridden Mass Budget, and its own frame mass (Hull Mass) is decoupled from and smaller than that budget — light < medium < heavy on both axes.")]
        public void ShipHullTiers_LoadFromJson_WithGraduatedFrameMassAndBudget()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            double prevMass = 0, prevBudget = 0;
            foreach (var tier in Tiers)
            {
                Assert.That(designs.ContainsKey(tier.id), Is.True,
                    $"hull tier '{tier.id}' loads — its template is unlocked (StartingItems) and the design is in ComponentDesigns (gotcha-10 both ends)");
                var design = designs[tier.id] as ComponentDesign;
                Assert.That(design, Is.Not.Null, $"{tier.id} is a ComponentDesign");

                Assert.That(design.HasAttribute<ShipHullAtb>(), Is.True,
                    $"{tier.id}: the JSON shipHullAtbArgs bound a ShipHullAtb (template → atb reflection works)");
                var atb = design.GetAttribute<ShipHullAtb>();
                Log($"{tier.id,-34} frame {design.MassPerUnit,8:N0} kg   budget {atb.MassBudget,10:N0} kg   mount={design.ComponentMountType}");

                // The per-design dial override bound the expected budget (light 25 t / medium 90 t / heavy 180 t).
                Assert.That(atb.MassBudget, Is.EqualTo(tier.budget).Within(1),
                    $"{tier.id}: Mass Budget dial override bound correctly");
                // The frame mass (Hull Mass) is decoupled from the budget and is what the ship actually carries.
                Assert.That(design.MassPerUnit, Is.EqualTo(tier.frameMass).Within(1),
                    $"{tier.id}: the hull's own frame mass (Hull Mass) bound correctly — decoupled from the budget");
                // A hull's frame is far lighter than its budget (the budget is a ceiling, not the frame's weight).
                Assert.That(design.MassPerUnit, Is.LessThan(atb.MassBudget),
                    $"{tier.id}: the frame mass is well under the budget ceiling");

                Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.ShipComponent), Is.True,
                    $"{tier.id}: the hull is a ship component");

                // Graduated: each tier is heavier and roomier than the one below it.
                Assert.That(design.MassPerUnit, Is.GreaterThan(prevMass), $"{tier.id}: frame mass climbs with the tier");
                Assert.That(atb.MassBudget, Is.GreaterThan(prevBudget), $"{tier.id}: budget climbs with the tier");
                prevMass = design.MassPerUnit;
                prevBudget = atb.MassBudget;
            }
            Log("three graduated hull tiers — decoupled frame mass, climbing budgets — the §0b hulls the fleet mounts in D2b");
        }
    }
}
