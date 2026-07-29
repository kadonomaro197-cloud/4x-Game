using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Movement;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// FUEL GRADE — "make the level of refined options for fuel affect the engine" (developer, 2026-07-29;
    /// docs/economy/DESIGNER-NORTH-STAR.md §26).
    ///
    /// <para><b>The problem it fixes.</b> A fuel used to affect exactly one number: <c>Exhaust Velocity</c>, via
    /// <c>ExhaustVelocityLookup</c>. But the engine template computes <c>Thrust = Exhaust Velocity × Fuel
    /// Consumption</c>, so a higher-exhaust-velocity fuel raised Δv-per-kg AND thrust at once — a pure dominance
    /// ladder ("burn the best fuel you've researched"), which is not a decision (§26.3). Worse, RP-1 was STRICTLY
    /// DOMINATED by Methalox: identical inputs, industry points, output and credits, but Methalox had higher exhaust
    /// velocity AND better density. A dead recipe.</para>
    ///
    /// <para><b>The fix.</b> Each fuel material carries a <c>FuelGrade</c> formula — its refinement level's effect on
    /// the engine — which multiplies the engine's MASS FLOW (<c>Fuel Consumption</c>), and therefore its thrust. Grade
    /// and exhaust velocity are set so <c>grade × exhaust velocity</c> is nearly CONSTANT across the three chemical
    /// fuels: one power budget (§20's <c>T·v = 2P</c>), split differently. That is also real rocketry — RP-1 is the
    /// dense high-thrust booster fuel, hydrolox the high-Isp low-thrust upper stage.</para>
    ///
    /// <code>
    ///   RP-1      ev 3510 × grade 1.15 ≈ 4036     ← most PUSH per kg of engine
    ///   Methalox  ev 3615 × grade 1.05 ≈ 3796
    ///   Hydrolox  ev 4462 × grade 0.85 ≈ 3793     ← most Δv per kg of FUEL
    ///   NTP       ev 7000 × grade 0.75 ≈ 5250     ← genuinely more powerful: it is a reactor,
    ///                                               paid for in fissionables + 125× the credits
    /// </code>
    ///
    /// <para>The base mod already ships real historical engines on different fuels — Merlin/F1 (RP-1), Raptor
    /// (methalox), RS-25 (hydrolox) — so this is gauged through the REAL JSON → NCalc → <see cref="NewtonionThrustAtb"/>
    /// path with no new data.</para>
    ///
    /// <para><b>Defensive:</b> <c>FuelGradeLookup</c> returns 1.0 for any fuel with no <c>FuelGrade</c> formula (a mod's
    /// fuel, an old save), which reproduces the pre-grade arithmetic exactly.</para>
    /// </summary>
    [TestFixture]
    public class FuelGradeTests
    {
        private const string Merlin = "default-design-merlin";   // RP-1     — grade 1.15
        private const string Raptor = "default-design-raptor";   // methalox — grade 1.05
        private const string Rs25   = "default-design-rs-25";    // hydrolox — grade 0.85

        private static void Log(string m) => TestContext.Progress.WriteLine("[fuel-grade] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True, $"base-mod engine design '{id}' should be built for the start faction");
            return designs[id];
        }

        private static NewtonionThrustAtb Thruster(ComponentDesign d)
        {
            Assert.That(d.TryGetAttribute<NewtonionThrustAtb>(out var atb), Is.True,
                $"'{d.Name}' should carry a NewtonionThrustAtb from the JSON template");
            return atb;
        }

        /// <summary>
        /// Burn rate per kg of engine = <c>0.3 × tech × grade</c> for every <c>conventional-engine</c>, so the RATIO of
        /// that quantity between two engines is EXACTLY the ratio of their fuel grades — independent of each engine's
        /// mass and of the faction's tech level. That makes this an exact assertion rather than a tuned one.
        /// </summary>
        [Test]
        [Description("The fuel's refinement level reaches the engine: burn-rate-per-kg-of-engine is proportional to FuelGrade, so RP-1 : methalox : hydrolox come out at exactly 1.15 : 1.05 : 0.85 through the real JSON → NCalc → NewtonionThrustAtb path.")]
        public void FuelGrade_ReachesTheEngine_ExactlyAsAuthored()
        {
            var s = TestScenario.CreateWithColony();

            var merlin = Design(s, Merlin); var mAtb = Thruster(merlin);
            var raptor = Design(s, Raptor); var rAtb = Thruster(raptor);
            var rs25   = Design(s, Rs25);   var hAtb = Thruster(rs25);

            Assert.That(mAtb.FuelType, Is.EqualTo("rp-1"));
            Assert.That(rAtb.FuelType, Is.EqualTo("methalox"));
            Assert.That(hAtb.FuelType, Is.EqualTo("hydrolox"));

            // burn rate per kg of engine == 0.3 * tech * grade  →  the tech term cancels in a ratio.
            double mPerKg = mAtb.FuelBurnRate / merlin.MassPerUnit;
            double rPerKg = rAtb.FuelBurnRate / raptor.MassPerUnit;
            double hPerKg = hAtb.FuelBurnRate / rs25.MassPerUnit;
            Log($"burn/kg — Merlin(rp-1) {mPerKg:0.######}  Raptor(methalox) {rPerKg:0.######}  RS-25(hydrolox) {hPerKg:0.######}");

            Assert.That(mPerKg / rPerKg, Is.EqualTo(1.15 / 1.05).Within(1e-6), "RP-1 vs methalox grade ratio");
            Assert.That(mPerKg / hPerKg, Is.EqualTo(1.15 / 0.85).Within(1e-6), "RP-1 vs hydrolox grade ratio");
            Assert.That(rPerKg / hPerKg, Is.EqualTo(1.05 / 0.85).Within(1e-6), "methalox vs hydrolox grade ratio");
        }

        /// <summary>
        /// The point of the whole change: no fuel is best at everything, so choosing one is a decision.
        /// </summary>
        [Test]
        [Description("No fuel dominates: hydrolox gives the most exhaust velocity (Δv per kg of fuel) while RP-1 gives the most thrust per kg of engine (push). Before FuelGrade a better fuel won BOTH, which is a ladder, not a choice — and RP-1 in particular was strictly dominated by methalox.")]
        public void NoFuelDominates_HydroloxBuysRange_RP1BuysPush()
        {
            var s = TestScenario.CreateWithColony();

            var merlin = Design(s, Merlin); var mAtb = Thruster(merlin);
            var raptor = Design(s, Raptor); var rAtb = Thruster(raptor);
            var rs25   = Design(s, Rs25);   var hAtb = Thruster(rs25);

            // Thrust = exhaust velocity × mass flow; normalise by engine mass so the comparison is per kg of engine.
            double mPush = mAtb.ExhaustVelocity * mAtb.FuelBurnRate / merlin.MassPerUnit;
            double rPush = rAtb.ExhaustVelocity * rAtb.FuelBurnRate / raptor.MassPerUnit;
            double hPush = hAtb.ExhaustVelocity * hAtb.FuelBurnRate / rs25.MassPerUnit;
            Log($"exhaust velocity — rp-1 {mAtb.ExhaustVelocity:0} | methalox {rAtb.ExhaustVelocity:0} | hydrolox {hAtb.ExhaustVelocity:0}");
            Log($"thrust per kg of engine — rp-1 {mPush:0.##} | methalox {rPush:0.##} | hydrolox {hPush:0.##}");

            // RANGE: hydrolox wins on exhaust velocity, which is what sets Δv per kg of fuel.
            Assert.That(hAtb.ExhaustVelocity, Is.GreaterThan(mAtb.ExhaustVelocity), "hydrolox out-ranges RP-1");
            Assert.That(hAtb.ExhaustVelocity, Is.GreaterThan(rAtb.ExhaustVelocity), "hydrolox out-ranges methalox");

            // PUSH: RP-1 wins on thrust per kg of engine — the dense booster fuel.
            Assert.That(mPush, Is.GreaterThan(hPush), "RP-1 out-pushes hydrolox");
            Assert.That(mPush, Is.GreaterThan(rPush), "RP-1 out-pushes methalox — so it is no longer a dead recipe");

            // The trade, stated as one assertion: the range winner and the push winner are different fuels.
            Assert.That(hPush, Is.LessThan(mPush),
                "the highest-exhaust-velocity fuel must NOT also be the highest-thrust one, or fuel choice is a ladder again");
        }
    }
}
