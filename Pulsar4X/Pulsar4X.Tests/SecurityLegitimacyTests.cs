using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;   // ComponentInstance, ComponentDesign
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Colonies;     // LegitimacyDB, LegitimacyInputs, LegitimacyProcessor, SecurityAtbDB
using Pulsar4X.Extensions;   // GetTotalSecurity

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The SECURITY civic dial — a colony's ORDER institutions (precincts carrying <see cref="SecurityAtbDB"/>) prop up
    /// its province LEGITIMACY (docs/assembler/01-IO-civic.md — the Security option's "-N unrest", expressed as
    /// legitimacy points). The BUILD sibling of the employment (A1) and food (M5c) civic terms: a NEW component
    /// attribute summed into a NEW, flag-gated input on the well-tested legitimacy formula. Flag OFF (the engine
    /// default) → byte-identical (no colony ships a precinct, and the term adds no factor at 0). Cradle-to-grave:
    /// designed/built → raises legitimacy → destroyed (bombardment) drops it.
    /// </summary>
    [TestFixture]
    public class SecurityLegitimacyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[security] " + m);

        // The consumer flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => LegitimacyProcessor.EnableSecurityLegitimacy = false;

        [Test]
        [Description("THE PURE MATH: LegitimacyDB.ComputeLegitimacy's security term. 0 security (the default / flag-off / an unpoliced colony) adds NO factor and no change — byte-identical. A positive order rating adds itself to legitimacy, capped at MaxSecurityBonus, so policing can hold a province but never manufacture loyalty outright.")]
        public void SecurityTerm_PureMath_NeutralAtZero_CappedAndAdditive()
        {
            var factors = new Dictionary<string, double>();

            // 0 security → no factor recorded, no legitimacy change (the byte-identical default).
            var inp = LegitimacyInputs.FromMorale(50.0);
            double baseLegit = LegitimacyDB.ComputeLegitimacy(inp, factors);
            Assert.That(factors.ContainsKey("security"), Is.False, "0 security → no security factor (byte-identical)");
            Assert.That(baseLegit, Is.EqualTo(50.0).Within(1e-9), "morale-only baseline unchanged by an absent security term");

            // A positive order rating adds itself to legitimacy (under the cap) and records the factor.
            inp.SecurityStrength = 10.0;
            double withSec = LegitimacyDB.ComputeLegitimacy(inp, factors);
            Assert.That(factors["security"], Is.EqualTo(10.0).Within(1e-9), "the security factor = the order strength (under the cap)");
            Assert.That(withSec - baseLegit, Is.EqualTo(10.0).Within(1e-9), "security adds its points to legitimacy");

            // Capped at MaxSecurityBonus — policing alone can't manufacture loyalty.
            inp.SecurityStrength = 100.0;
            LegitimacyDB.ComputeLegitimacy(inp, factors);
            Assert.That(factors["security"], Is.EqualTo(LegitimacyDB.MaxSecurityBonus).Within(1e-9),
                "the security bonus is capped — order holds a province, it doesn't manufacture loyalty");
        }

        [Test]
        [Description("CRADLE-TO-GRAVE end-to-end through the real processor: the security-precinct is a registered buildable start design (six-point registration); installing one makes GetTotalSecurity read its order rating; with the flag ON the province's legitimacy rises by that order (capped), recorded in the Factors gauge; with the flag OFF it's byte-identical (no security factor). THE GRAVE RUNG: destroying the precinct drops the order to 0 and legitimacy falls back to the unpoliced baseline.")]
        public void Precinct_RaisesLegitimacy_FlagGated_CradleToGrave()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var legitimacy = s.Colony.GetDataBlob<LegitimacyDB>();

            // Registration (L6 six-point): the precinct is a registered buildable design on the start faction.
            Assert.That(factionInfo.ComponentDesigns.ContainsKey("default-design-security-precinct"), Is.True,
                "the security-precinct is a registered buildable start design");
            var precinctDesign = factionInfo.ComponentDesigns["default-design-security-precinct"];

            // Baseline: the start colony ships NO precinct → zero security (neutral).
            Assert.That(comps.GetTotalSecurity(), Is.EqualTo(0.0), "the start colony ships no precinct → no security");

            // Flag OFF → the morale-driven baseline, no security factor (byte-identical).
            LegitimacyProcessor.EnableSecurityLegitimacy = false;
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            double legitUnpoliced = legitimacy.Legitimacy;
            Assert.That(legitimacy.Factors.ContainsKey("security"), Is.False, "flag off → no security factor (byte-identical)");
            Log($"unpoliced legitimacy = {legitUnpoliced:0.0}");

            // Build + install a precinct → GetTotalSecurity reads its rating (10) at full health.
            var instance = new ComponentInstance(precinctDesign);
            s.Colony.AddComponent(instance);
            Assert.That(comps.GetTotalSecurity(), Is.EqualTo(10.0).Within(0.01), "the installed precinct provides its order rating");

            // Flag ON → legitimacy rises by the (capped) order, recorded in the gauge.
            LegitimacyProcessor.EnableSecurityLegitimacy = true;
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            double legitPoliced = legitimacy.Legitimacy;
            Assert.That(legitimacy.Factors["security"], Is.EqualTo(10.0).Within(0.01), "the security factor = the precinct's order (under the 15 cap)");
            Assert.That(legitPoliced, Is.GreaterThan(legitUnpoliced), "a policed province reads higher legitimacy");
            Log($"policed legitimacy = {legitPoliced:0.0} (+{legitPoliced - legitUnpoliced:0.0})");

            // THE GRAVE RUNG: destroy the precinct (an orbital-bombardment installation-kill) → order drops to 0 →
            // legitimacy falls back to the unpoliced baseline. Security is a real, LOSABLE hold on order.
            comps.RemoveComponentInstance(instance);
            Assert.That(comps.GetTotalSecurity(), Is.EqualTo(0.0), "destroyed precinct → no security (the grave rung)");
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            Assert.That(legitimacy.Legitimacy, Is.EqualTo(legitUnpoliced).Within(0.01),
                "with the precinct gone, legitimacy falls back to the unpoliced baseline");
            Assert.That(legitimacy.Factors.ContainsKey("security"), Is.False, "and the security factor is gone");
        }
    }
}
