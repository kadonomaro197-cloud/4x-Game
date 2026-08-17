using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SPACE-HABITAT PRICING — OPERATION BLUEPRINT-TO-STEEL Phase C, slice C8 (adjudication: "price it").
    /// Design: docs/assembler/01-IO-civic.md §D + civicderived.html (the proposed <c>fix</c> formula).
    ///
    /// <para><b>What was wrong.</b> The <c>space-habitat</c> template's <c>Mass</c> was a flat <c>1000</c> — a
    /// station module housing up to <b>1,000,000</b> colonists cost the same one tonne (and same materials / build
    /// points) as one housing nobody. Capacity was free: the whole point of a habitat — how many people it holds —
    /// had zero bearing on what it cost to build.</para>
    ///
    /// <para><b>The fix (developer-ruled "price it").</b> Mass now reads the two dials that describe the module:
    /// <c>Support Colonists</c> (the capacity, 0..1,000,000) and <c>Housing Comfort</c> (0..50):
    /// <c>Mass = 1000 * (1 + Colonists/500*0.5 + Comfort/10)</c>. Build points follow the priced mass
    /// (<c>[Mass]/10</c>), and the material/volume costs already scaled off <c>[Mass]</c>. So a bigger, comfier
    /// habitat costs proportionally more to build — the ladder:
    /// <list type="bullet">
    ///   <item>empty (0 colonists, 0 comfort) → 1000 (the floor, unchanged)</item>
    ///   <item>shipped default (500 colonists, 5 comfort) → <b>2000</b></item>
    ///   <item>Kithrin hive (200,000 colonists) → ~201,500 (≈200× the standard, on the same template)</item>
    /// </list></para>
    ///
    /// <para><b>Capability is untouched.</b> Mass feeds only Volume / material cost / build points — NOT the
    /// population support or comfort the habitat provides. So every gameplay read (population capacity, morale
    /// comfort, crew = 10, and the station operating cost, which counts modules + population, not mass) is
    /// unchanged. This is a cost-side re-pricing, not a balance change to what a habitat does.</para>
    /// </summary>
    [TestFixture]
    public class SpaceHabitatPricingTests
    {
        private const string HabitatDesign = "default-design-space-habitat";
        private const string HabitatTemplate = "space-habitat";

        private static void Log(string m) => TestContext.Progress.WriteLine("[habitat-price] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True,
                $"base-mod design '{id}' should be built for the start faction");
            return designs[id];
        }

        [Test]
        [Description("The shipped Space Habitat is now priced by its dials: mass = 1000*(1 + 500/500*0.5 + 5/10) = 2000 "
                     + "(a flat constant would read 1000), while what it PROVIDES — its population support — is unchanged.")]
        public void TheShippedHabitat_IsPricedByItsDials_NotAFlatConstant()
        {
            var s = TestScenario.CreateWithColony();
            var hab = Design(s, HabitatDesign);

            Log($"{HabitatDesign}: mass {hab.MassPerUnit:0.###} kg");

            // 1000 * (1 + 500/500 * 0.5 + 5/10) = 1000 * (1 + 0.5 + 0.5) = 2000.
            // The OLD flat formula read a constant 1000, so 2000 proves both dial terms are actually priced in.
            Assert.That(hab.MassPerUnit, Is.EqualTo(2000).Within(1e-6),
                "the shipped habitat (Support Colonists 500, Housing Comfort 5) must read the priced 2000, not the old flat 1000");

            // Capability preserved: pricing changed the COST, not what the habitat provides.
            Assert.That(hab.TryGetAttribute<PopulationSupportAtbDB>(out var pop), Is.True,
                "the habitat must still bind a PopulationSupportAtbDB from the JSON template");
            Assert.That(pop.PopulationCapacity, Is.EqualTo(500),
                "population support (the capability) is unchanged — mass feeds cost, not capacity");
        }

        [Test]
        [Description("Structural / can't-rot: the space-habitat template's Mass formula reads the capacity + comfort "
                     + "dials (so no future design can restore a flat constant), and build points follow the priced mass.")]
        public void TheMassFormula_ReadsTheDials_Structurally()
        {
            var s = TestScenario.CreateWithColony();
            var templates = s.Faction.GetDataBlob<FactionInfoDB>().Data.ComponentTemplates;

            Assert.That(templates.ContainsKey(HabitatTemplate), Is.True,
                "the space-habitat template should be unlocked for the start faction");
            var tmpl = templates[HabitatTemplate];

            var mass = tmpl.Formulas["Mass"];
            Log("mass formula: " + mass);
            Assert.That(mass, Does.Contain("Support Colonists"),
                "mass must be paid for by the capacity dial — a habitat's cost scales with how many it holds");
            Assert.That(mass, Does.Contain("Housing Comfort"),
                "and by the comfort dial");
            Assert.That(mass, Is.Not.EqualTo("1000"),
                "the flat constant must be gone so the capacity dial can never again be free");

            var bp = tmpl.Formulas["BuildPointCost"];
            Log("build-point formula: " + bp);
            Assert.That(bp, Does.Contain("[Mass]"),
                "build points follow the priced mass, so a bigger habitat also takes longer to build");
        }
    }
}
