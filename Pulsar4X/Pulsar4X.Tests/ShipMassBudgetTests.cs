using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the §0b mass-budget cap — SLICE A (dossier ⚙11, the Chassis keystone). The mass cap that the
    /// whole component designer leans on is LIVE for ground units (`GroundUnitAssembly.Compute` rejects an
    /// over-budget design) but absent for ships — `ShipDesign.Recalculate` summed component mass uncapped.
    /// Slice A adds the machinery: `ShipDesign.MassBudget` + `OverMassBudget`, computed & exposed, NOT enforced
    /// (sourced from the design's own mass at 1.0 headroom, so nothing is ever over budget — byte-identical to
    /// pre-cap, `IsValid` untouched, no engine construction path gated).
    ///
    /// This gauge proves two things and produces one deliverable:
    ///  1) the machinery computes (every base-mod ship gets a finite MassBudget ≥ its MassPerUnit),
    ///  2) the byte-identical guarantee holds (no design reads OverMassBudget == true),
    ///  3) DELIVERABLE — it prints every ship's MassPerUnit and the HEAVIEST, which is the calibration number
    ///     the enforcement slice (C) needs to size a real hull ceiling ABOVE the heaviest base-mod ship so the
    ///     hard cap breaks no existing design. Read the [mass-budget] lines in the CI log for that number.
    /// </summary>
    [TestFixture]
    public class ShipMassBudgetTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[mass-budget] " + m);

        [Test]
        [Description("Slice A: every base-mod ship design gets a computed MassBudget ≥ its mass, and none reads over-budget (byte-identical). Also prints the calibration readout.")]
        public void ShipDesigns_HaveComputedMassBudget_AndNoneOverBudget()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            Assert.That(factionInfo.ShipDesigns, Is.Not.Empty,
                "The faction has no ship designs — the colony blueprint should unlock some.");

            ShipDesign heaviest = null;
            Log("================ SHIP MASS READOUT (calibration for the enforcement slice) ================");
            foreach (var design in factionInfo.ShipDesigns.Values.OrderByDescending(d => d.MassPerUnit))
            {
                Log($"  {design.Name,-34} mass {design.MassPerUnit,14:N0} kg   budget {design.MassBudget,14:N0} kg   over? {design.OverMassBudget}");

                // The machinery computed a sane budget for every design.
                Assert.That(design.MassBudget, Is.GreaterThanOrEqualTo(design.MassPerUnit),
                    $"Design '{design.Name}' has a MassBudget below its own mass — Slice A must never invalidate an existing design.");

                // Byte-identical guarantee: Slice A never flags a design over budget (nothing is enforced yet).
                Assert.That(design.OverMassBudget, Is.False,
                    $"Design '{design.Name}' reads OverMassBudget == true in Slice A — the additive slice must be byte-identical (no design over budget until the calibrated enforcement slice).");

                if (heaviest == null || design.MassPerUnit > heaviest.MassPerUnit)
                    heaviest = design;
            }

            Log($">>> HEAVIEST base-mod ship: '{heaviest.Name}' at {heaviest.MassPerUnit:N0} kg — the enforcement slice's hull ceiling must sit ABOVE this.");
        }
    }
}
