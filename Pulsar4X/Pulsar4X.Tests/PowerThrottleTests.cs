using NUnit.Framework;
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Energy;      // EnergyGenAbilityDB
using Pulsar4X.Engine;      // Entity
using Pulsar4X.Extensions;  // GetTotalJobs
using Pulsar4X.Industry;    // IndustryTools

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TIER 2.5 gauge — the colony POWER-brownout throttle (developer ruling 2026-08-16, "do whatever fits best with what
    /// was planned"; docs/IMPLEMENTATION-CAMPAIGN-LOG.md ADJUDICATION QUEUE C-POWER). Proves that when the flag is ON, a
    /// colony's build rate is paced by whether its installed reactor/solar GENERATION can cover its installations' power
    /// DEMAND — the THIRD production-rate factor, parallel to infrastructure and staffing — and that with the flag OFF
    /// (the engine-suite default) the throttle never fires, so the rest of the suite is byte-identical.
    ///
    /// <para>Measured on a REAL start colony (TestScenario) and calibration-independent: it drives the colony's power
    /// SUPPLY relative to its own live power DEMAND (Σ installation crew × <see cref="IndustryTools.PowerDrawPerCrew_kW"/>)
    /// and asserts the RELATIONSHIP — full when generation covers demand, ~half at half the supply, inert with no power
    /// model or zero output, browns out when generation falls short (the grave rung) — so re-tuning the coefficient,
    /// reactor sizes, or building crews cannot make it lie. The absolute homeworld safety margin (one reactor's 75 MW vs
    /// the ~52 MW demand) is asserted separately by the commit that installs the reactor on Earth.</para>
    /// </summary>
    [TestFixture]
    public class PowerThrottleTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[power] " + m);

        // Both are process-global statics — always restore them so they can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlags()
        {
            IndustryTools.EnablePowerThrottle = false;
            IndustryTools.PowerDrawPerCrew_kW = 1.0;
        }

        [Test]
        [Description("With the power flag ON, a colony's production rate scales with min(1, generation supply / installation power demand): full when covered, ~half at half the supply, inert at zero output, and browns out when generation falls short (the grave rung); with the flag OFF it is always 1.0 (byte-identical). The pristine start colony is never power-throttled.")]
        public void PowerEfficiency_ScalesWithSupplyVsDemand_FlagGated_StartColonySafe()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;

            long jobs = colony.GetDataBlob<ComponentInstancesDB>().GetTotalJobs();
            Assert.That(jobs, Is.GreaterThan(1), "the start colony's installed buildings declare crew (GetTotalJobs > 1)");

            // The colony's live power DEMAND, in kW — the exact figure PowerEfficiency computes internally.
            double demand = jobs * IndustryTools.PowerDrawPerCrew_kW;
            Log($"start colony jobs {jobs:N0} · power demand {demand:N0} kW (at {IndustryTools.PowerDrawPerCrew_kW} kW/crew)");

            // (0) Flag ON, PRISTINE colony → 1.0. The menu-game safety assertion: the start colony is never brownout-
            //     throttled by this feature — true whether it carries no power model yet (supply 0 → inert) OR a reactor
            //     that covers demand (the install-reactor commit). Stable across both states.
            IndustryTools.EnablePowerThrottle = true;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9),
                "the pristine start colony is not power-throttled");

            // Attach a controllable power model so the ratio math is deterministic against the colony's real demand.
            // SetDataBlob replaces any reactor blob the colony already carries, so this OVERRIDES the start supply.
            var egen = new EnergyGenAbilityDB(colony.StarSysDateTime);
            colony.SetDataBlob(egen);

            // (1) Flag OFF → always 1.0, even with supply deliberately below demand — the byte-identical default.
            IndustryTools.EnablePowerThrottle = false;
            egen.MaxOutputFromReactor = 0.5 * demand;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9), "flag off → no throttle");

            // (2) Flag ON, generation covers demand (2× the demand) → 1.0.
            IndustryTools.EnablePowerThrottle = true;
            egen.MaxOutputFromReactor = 2.0 * demand;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9),
                "generation covers the power demand → full rate");

            // (3) Generation at exactly half the demand → ~0.5 (the brownout bite, proportional).
            egen.MaxOutputFromReactor = 0.5 * demand;
            double half = IndustryTools.PowerEfficiency(colony);
            Log($"supply at half demand → power efficiency {half:0.###}");
            Assert.That(half, Is.EqualTo(0.5).Within(1e-6), "generation at half the demand → ~half rate");

            // (4) A modelled colony with ZERO output → 1.0 (inert). Supply-0 is SAFE here — it can never brick
            //     production (the inverse of the food-supply-hardcoded-0 unwinnable trap).
            egen.MaxOutputFromReactor = 0.0;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9),
                "modelled but zero output → inert, never bricks production");

            // (5) GRAVE RUNG — start covered (1.0), then lose generation below demand → browns out (< 1.0). Damage or
            //     destroy a reactor and production suffers; power is a losable capability.
            egen.MaxOutputFromReactor = 2.0 * demand;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9), "fully powered → 1.0");
            egen.MaxOutputFromReactor = 0.5 * demand;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.LessThan(1.0),
                "losing generation below demand browns out production (grave rung)");
        }

        [Test]
        [Description("A host with NO power model (a bare/unmanaged entity, or any colony without an EnergyGenAbilityDB) is inert: the power throttle stays 1.0 without throwing — the safety guard that keeps a never-powered/legacy colony from being bricked.")]
        public void PowerEfficiency_NoPowerModel_IsInert()
        {
            IndustryTools.EnablePowerThrottle = true;

            // A bare (unmanaged) entity has no Manager and no power blob — PowerEfficiency must return 1.0 via the
            // Manager==null / no-EnergyGenAbilityDB guards without NRE-ing on TryGetDataBlob.
            var bare = Entity.Create();
            Assert.That(IndustryTools.PowerEfficiency(bare), Is.EqualTo(1.0).Within(1e-9),
                "no power model / unmanaged → inert (no throttle, no throw)");
        }

        [Test]
        [Description("THE MAKE-LIVE GAUGE (C-POWER-LIVE): the start colony now installs a fission reactor (energy.json reactor mount gains PlanetInstallation + earth.json Installations), so its REAL power supply (75 MW) covers its installations' power demand (~52 MW) with headroom — the throttle reads exactly 1.0 on the real New-Game colony. Proves the menu-game start is powered AND safe; CI is the calibration net (if demand ever exceeds supply this REDs before it ships).")]
        public void PowerEfficiency_StartColony_IsPoweredAndSafe()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;

            Assert.That(colony.TryGetDataBlob<EnergyGenAbilityDB>(out var egen), Is.True,
                "the start colony now installs a fission reactor → it carries an EnergyGenAbilityDB");
            double supply = egen.TotalOutputMax;
            long jobs = colony.GetDataBlob<ComponentInstancesDB>().GetTotalJobs();
            double demand = jobs * IndustryTools.PowerDrawPerCrew_kW;
            Log($"start colony power: supply {supply:N0} kW (reactor+solar) vs demand {demand:N0} kW " +
                $"(jobs {jobs:N0} × {IndustryTools.PowerDrawPerCrew_kW} kW/crew) → margin {(demand > 0 ? supply / demand : 0):0.00}×");

            Assert.That(supply, Is.GreaterThan(0.0), "the installed reactor gives real power output (75 MW)");
            Assert.That(supply, Is.GreaterThanOrEqualTo(demand),
                "the start colony's generation covers its installations' power demand → menu-game safe");

            IndustryTools.EnablePowerThrottle = true;
            Assert.That(IndustryTools.PowerEfficiency(colony), Is.EqualTo(1.0).Within(1e-9),
                "supply ≥ demand → the throttle does NOT throttle the real start colony (full production rate)");
        }
    }
}
