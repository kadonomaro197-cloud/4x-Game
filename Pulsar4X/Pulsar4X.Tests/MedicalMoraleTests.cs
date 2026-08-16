using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;   // ComponentInstance, ComponentDesign
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Colonies;     // ColonyMoraleDB, MoraleInputs, PopulationProcessor, MedicalAtbDB
using Pulsar4X.Extensions;   // GetTotalMedical

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The MEDICAL civic dial — a colony's HOSPITALS (components carrying <see cref="MedicalAtbDB"/>) lift its MORALE
    /// (docs/assembler/01-IO-civic.md — the Medical option's "+N health", a NEW morale consumer, not one of the original
    /// six inputs). The BUILD sibling of the Security→legitimacy dial and the food/employment terms: a NEW component
    /// attribute summed into a NEW, flag-gated input on the well-tested morale formula. Flag OFF (the engine default) →
    /// byte-identical (no colony ships a hospital, and the term adds no factor at 0). Cradle-to-grave: designed/built →
    /// lifts morale → destroyed (bombardment) drops it.
    /// </summary>
    [TestFixture]
    public class MedicalMoraleTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[medical] " + m);

        private bool _savedMedical, _savedEmployment;

        [SetUp]
        public void SaveFlags()
        {
            _savedMedical = PopulationProcessor.EnableMedicalMorale;
            _savedEmployment = PopulationProcessor.EnableEmploymentMorale;
            // Isolate the health term: keep employment OFF so a hospital's operating crew can't shift the employment term.
            PopulationProcessor.EnableEmploymentMorale = false;
        }

        [TearDown]
        public void RestoreFlags()
        {
            PopulationProcessor.EnableMedicalMorale = _savedMedical;
            PopulationProcessor.EnableEmploymentMorale = _savedEmployment;
        }

        [Test]
        [Description("THE PURE MATH: ColonyMoraleDB.ComputeMorale's health term. 0 health (the default / flag-off / an un-hospitalled colony) adds NO factor and no change — byte-identical. A positive care rating adds itself to morale, capped at MaxHealthBonus, so hospitals lift a colony but medicine alone can't make a miserable world happy.")]
        public void HealthTerm_PureMath_NeutralAtZero_CappedAndAdditive()
        {
            var factors = new Dictionary<string, double>();

            // 0 health → no factor recorded, no morale change (the byte-identical default).
            var inp = new MoraleInputs { EmploymentRatio = -1.0 };   // -1 = "no job data" neutral, everything else 0
            double baseMorale = ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors.ContainsKey("health"), Is.False, "0 health → no health factor (byte-identical)");

            // A positive care rating adds itself to morale (under the cap) and records the factor.
            inp.HealthStrength = 10.0;
            double withHealth = ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors["health"], Is.EqualTo(10.0).Within(1e-9), "the health factor = the care rating (under the cap)");
            Assert.That(withHealth - baseMorale, Is.EqualTo(10.0).Within(1e-9), "health adds its points to morale");

            // Capped at MaxHealthBonus — medicine alone can't manufacture happiness.
            inp.HealthStrength = 100.0;
            ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors["health"], Is.EqualTo(ColonyMoraleDB.MaxHealthBonus).Within(1e-9),
                "the health bonus is capped — good care lifts a colony, it doesn't manufacture happiness");
        }

        [Test]
        [Description("CRADLE-TO-GRAVE end-to-end through the real morale reader: the hospital is a registered buildable start design (six-point registration); installing one makes GetTotalMedical read its care rating; with the flag ON the colony's morale rises by that care (capped); with the flag OFF it's byte-identical (the hospital present but its care unread). THE GRAVE RUNG: destroying the hospital drops the care to 0 and morale falls back.")]
        public void Hospital_LiftsMorale_FlagGated_CradleToGrave()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();

            // Registration (L6 six-point): the hospital is a registered buildable design on the start faction.
            Assert.That(factionInfo.ComponentDesigns.ContainsKey("default-design-hospital"), Is.True,
                "the hospital is a registered buildable start design");
            var hospitalDesign = factionInfo.ComponentDesigns["default-design-hospital"];

            // The start colony ships NO hospital → zero medical (neutral).
            Assert.That(comps.GetTotalMedical(), Is.EqualTo(0.0), "the start colony ships no hospital → no care");

            // Install a hospital → GetTotalMedical reads its rating (10) at full health.
            var instance = new ComponentInstance(hospitalDesign);
            s.Colony.AddComponent(instance);
            Assert.That(comps.GetTotalMedical(), Is.EqualTo(10.0).Within(0.01), "the installed hospital provides its care rating");

            // Flag OFF → the hospital's care is UNREAD → byte-identical morale.
            PopulationProcessor.EnableMedicalMorale = false;
            double moraleCareUnread = PopulationProcessor.ComputeCurrentMorale(s.Colony);
            Log($"morale (hospital installed, care unread) = {moraleCareUnread:0.0}");

            // Flag ON → the care lifts morale (the hospital is installed in BOTH reads, so the delta is the health term alone).
            PopulationProcessor.EnableMedicalMorale = true;
            double moraleCared = PopulationProcessor.ComputeCurrentMorale(s.Colony);
            Log($"morale (hospital installed, care read) = {moraleCared:0.0} (+{moraleCared - moraleCareUnread:0.0})");
            Assert.That(moraleCared, Is.GreaterThan(moraleCareUnread), "a cared-for colony reads higher morale");

            // THE GRAVE RUNG: destroy the hospital (an orbital-bombardment installation-kill) → care drops to 0 → morale
            // falls back to the uncared baseline. Health care is a real, LOSABLE lift on morale.
            comps.RemoveComponentInstance(instance);
            Assert.That(comps.GetTotalMedical(), Is.EqualTo(0.0), "destroyed hospital → no care (the grave rung)");
            double moraleAfterLoss = PopulationProcessor.ComputeCurrentMorale(s.Colony);   // flag still on, but no hospital
            Assert.That(moraleAfterLoss, Is.EqualTo(moraleCareUnread).Within(0.01),
                "with the hospital gone, morale falls back to the uncared baseline");
        }
    }
}
