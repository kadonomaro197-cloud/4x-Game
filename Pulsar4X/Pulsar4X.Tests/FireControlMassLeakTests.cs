using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE FIRE-CONTROL MASS LEAK — closed 2026-07-30 (docs/economy/DESIGNER-NORTH-STAR.md §34.4, slice S2).
    ///
    /// <para><b>What was wrong.</b> The <c>beam-fire-control</c> template carried two player dials,
    /// <c>Size vs Range</c> and <c>Size vs TrackingSpeed</c> (both 0.25–4, default 1), which appeared in
    /// <b>exactly one place</b> — the mass formula:</para>
    /// <code>
    ///   Mass   = (Range + TrackingSpeed/100) × Size vs Range × Size vs TrackingSpeed
    ///   DBargs = AtbConstrArgs(Range, TrackingSpeed)          ← NEITHER dial reaches the attribute
    /// </code>
    /// <para>So they wrote no simulation variable at all and only ever multiplied mass. Setting both to their
    /// 0.25 minimum divided a director's mass by <b>sixteen</b> with <b>no loss of range or tracking speed</b> —
    /// not a dead dial but a free 16× mass saving, and mass is the currency the Chassis door builds its whole
    /// budget on (<c>ShipMassBudgetEnforcementTests</c>). That is why this had to be fixed before Chassis is
    /// derived, rather than after.</para>
    ///
    /// <para><b>The fix.</b> Both dials are deleted and the mass formula loses the multiplication:
    /// <c>Mass = Range + TrackingSpeed/100</c>. <c>Range</c> and <c>Tracking Speed</c> were already the real
    /// dials and already cost mass, so nothing of value is removed — the phantom pair simply duplicated them
    /// while offering a discount. The design's own <c>"Size vs Range": 1</c> override is removed in the same
    /// change: <c>ComponentDesignFromJson</c> indexes <c>ComponentDesignProperties[property.Key]</c> with no
    /// guard, so leaving a design override pointing at a deleted template property would throw
    /// <c>KeyNotFoundException</c> on New Game (root <c>CLAUDE.md</c> gotcha #10 — check the OTHER end).</para>
    ///
    /// <para><b>Byte-identical.</b> Every shipped design used 1 for both dials, and 1 × 1 = 1, so every mass in
    /// the game is unchanged. That is what this fixture pins.</para>
    /// </summary>
    [TestFixture]
    public class FireControlMassLeakTests
    {
        private const string Director = "default-design-beam-fire-control";
        private const string PdDirector = "default-design-pd-director";

        private static void Log(string m) => TestContext.Progress.WriteLine("[fc-mass] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True,
                $"base-mod design '{id}' should be built for the start faction");
            return designs[id];
        }

        /// <summary>
        /// The leak is closed STRUCTURALLY — the dials no longer exist, so no design can set them and no future
        /// design can re-open the discount. Asserting on the template (not just on a design) is what makes this
        /// a regression guard rather than a snapshot.
        /// </summary>
        [Test]
        [Description("The two phantom dials are GONE from the beam-fire-control template, and its mass formula no longer multiplies by them — so the free 16x mass saving cannot be re-opened by any design.")]
        public void ThePhantomDials_AreGone_AndMassNoLongerMultipliesByThem()
        {
            var s = TestScenario.CreateWithColony();
            var templates = s.Faction.GetDataBlob<FactionInfoDB>().Data.ComponentTemplates;

            Assert.That(templates.ContainsKey("beam-fire-control"), Is.True,
                "the beam-fire-control template should be unlocked for the start faction");
            var tmpl = templates["beam-fire-control"];
            var names = tmpl.Properties.Select(p => p.Name).ToList();
            Log("beam-fire-control dials: " + string.Join(" · ", names));

            Assert.That(names, Does.Not.Contain("Size vs Range"),
                "the phantom mass-only dial must not exist — it wrote no sim variable and only divided mass");
            Assert.That(names, Does.Not.Contain("Size vs TrackingSpeed"),
                "same for the tracking-speed twin");

            // The real dials are untouched: they are what a player was always meant to set.
            Assert.That(names, Does.Contain("Range"));
            Assert.That(names, Does.Contain("Tracking Speed"));

            var mass = tmpl.Properties.First(p => p.Name == "Mass").PropertyFormula;
            Log("mass formula: " + mass);
            Assert.That(mass, Does.Not.Contain("Size vs"),
                "the mass formula must not reference the deleted dials (it would throw at design time)");
            Assert.That(mass, Does.Contain("Range").And.Contain("Tracking Speed"),
                "mass must still be paid for by the two capabilities the director actually provides");
        }

        /// <summary>
        /// Byte-identity, on the two shipped directors. Every existing design used 1 for both phantom dials, so
        /// removing a multiply-by-one changes nothing — and this is the assertion that proves it rather than
        /// assuming it.
        /// </summary>
        [Test]
        [Description("Byte-identical: the shipped Beam Fire Control still weighs exactly 150 kg (Range 100 + Tracking 5000/100) and still binds Range 100 / TrackingSpeed 5000, and the PD Director — which never carried the phantom dials — is untouched.")]
        public void TheShippedDirectors_AreByteIdentical()
        {
            var s = TestScenario.CreateWithColony();

            var fc = Design(s, Director);
            Assert.That(fc.TryGetAttribute<BeamFireControlAtbDB>(out var atb), Is.True,
                "the director must still bind a BeamFireControlAtbDB from the JSON template");
            Log($"{Director}: mass {fc.MassPerUnit:0.###} kg · range {atb.Range} · tracking {atb.TrackingSpeed}");

            // Range 100 + 5000/100 = 150.  The OLD formula was (100 + 50) x 1 x 1 = 150 — the same number.
            Assert.That(fc.MassPerUnit, Is.EqualTo(150).Within(1e-6),
                "removing a multiply-by-one must not move the shipped director's mass");
            Assert.That(atb.Range, Is.EqualTo(100), "the capability the player set is unchanged");
            Assert.That(atb.TrackingSpeed, Is.EqualTo(5000));

            // The PD director never had the phantom dials, so it is the control case: if the change had leaked
            // beyond beam-fire-control, this would move too.
            var pd = Design(s, PdDirector);
            Assert.That(pd.TryGetAttribute<BeamFireControlAtbDB>(out var pdAtb), Is.True);
            Log($"{PdDirector}: mass {pd.MassPerUnit:0.###} kg · range {pdAtb.Range} · tracking {pdAtb.TrackingSpeed}");
            Assert.That(pd.MassPerUnit, Is.GreaterThan(0), "the control design still builds");
            Assert.That(pdAtb.TrackingSpeed, Is.EqualTo(20000), "and its authored tracking speed is unchanged");
        }

        /// <summary>
        /// The point of the whole change, stated as one assertion: mass is now monotonic in the capability, so
        /// there is no setting that buys the same director for less. The ONLY way to make a lighter director is
        /// to accept less range or less tracking speed.
        /// </summary>
        [Test]
        [Description("No free lunch: a director's mass is now exactly its range plus a hundredth of its tracking speed, so every kilogram saved costs capability. Checked against the PD director too, which reaches the same law by a different authored size factor.")]
        public void MassIsNowPaidForEntirelyByCapability()
        {
            var s = TestScenario.CreateWithColony();
            var fc = Design(s, Director);
            fc.TryGetAttribute<BeamFireControlAtbDB>(out var atb);

            double expected = atb.Range + atb.TrackingSpeed / 100.0;
            Log($"capability-derived mass {expected:0.###} vs actual {fc.MassPerUnit:0.###}");
            Assert.That(fc.MassPerUnit, Is.EqualTo(expected).Within(1e-6),
                "mass must be a pure function of the two capabilities — nothing else may discount it");

            // And it is strictly increasing in both, so a cheaper director is always a worse director.
            Assert.That(expected, Is.GreaterThan(atb.Range),
                "tracking speed contributes mass, so it cannot be had for free");
            Assert.That(expected, Is.GreaterThan(atb.TrackingSpeed / 100.0),
                "and neither can range");
        }
    }
}
