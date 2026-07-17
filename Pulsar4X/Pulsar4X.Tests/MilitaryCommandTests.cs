using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;      // MilitaryCommand, PersonalityDB, PersonalityTrait, FactionInfoDB
using Pulsar4X.GroundCombat;  // GroundReinforcement, GroundForcesDB, GroundUnit (the integration read)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MILITARY-COMMAND POSTURE gauge — the ONE place that owns the "commit the force vs. keep a home reserve" call
    /// (<see cref="MilitaryCommand"/>). The four reserve guards (fleet: <c>ConquerResolver.HasHomeReserve</c> /
    /// <c>ShouldStopMassing</c>; ground: <c>GroundReinforcement.GarrisonReserveFor</c> / <c>WouldStripReserve</c>) all
    /// consult this posture, so an AGGRESSIVE/high-risk faction keeps a SMALLER reserve (commits more) and a CAUTIOUS/
    /// low-risk one keeps a BIGGER reserve.
    ///
    /// THE BYTE-IDENTITY PIN: a NEUTRAL personality (Aggression = Risk = 0.5) and a null/absent personality both yield
    /// a factor of exactly 1.0, so every guard's threshold is unchanged — that's why the existing ConquerResolver /
    /// FleetComposition / GroundReinforcement tests (whose factions carry no personality) stay green. The rest of these
    /// are pure math (no colony harness); one integration read shows the ground LOAD-guard shift live.
    /// </summary>
    [TestFixture]
    public class MilitaryCommandTests
    {
        /// <summary>A personality with the two traits the posture reads set to the given values (others stay neutral).</summary>
        private static PersonalityDB P(double aggression, double risk)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, aggression);
            p.SetTrait(PersonalityTrait.Risk, risk);
            return p;
        }

        // ── The byte-identity pin: neutral / absent personality → factor exactly 1.0 ──────────────────────────────

        [Test]
        [Description("A neutral (0.5/0.5) AND a null personality both yield posture 0.5 and a reserve factor of EXACTLY "
                   + "1.0 on both the fleet and ground axes — the pin that keeps every guard byte-identical by default.")]
        public void NeutralOrAbsentPersonality_YieldsFactorExactlyOne()
        {
            // Posture sits at the baseline for both the neutral blob and the absent (null) case.
            Assert.That(MilitaryCommand.ReservePosture(P(0.5, 0.5)), Is.EqualTo(MilitaryCommand.BaselinePosture),
                "a neutral personality reads the baseline posture");
            Assert.That(MilitaryCommand.ReservePosture(null), Is.EqualTo(MilitaryCommand.BaselinePosture),
                "an absent personality falls back to the baseline posture");
            Assert.That(MilitaryCommand.PostureFor(null), Is.EqualTo(MilitaryCommand.BaselinePosture),
                "the Entity seam is null-safe → baseline");

            // Both factor families are EXACTLY 1.0 (not just approximately) at neutral — the byte-identity guarantee.
            Assert.That(MilitaryCommand.FleetReserveFactor(P(0.5, 0.5)), Is.EqualTo(1.0), "neutral fleet factor is exactly 1.0");
            Assert.That(MilitaryCommand.GroundReserveFactor(P(0.5, 0.5)), Is.EqualTo(1.0), "neutral ground factor is exactly 1.0");
            Assert.That(MilitaryCommand.FleetReserveFactor((PersonalityDB)null), Is.EqualTo(1.0), "absent fleet factor is exactly 1.0");
            Assert.That(MilitaryCommand.GroundReserveFactor((PersonalityDB)null), Is.EqualTo(1.0), "absent ground factor is exactly 1.0");
            Assert.That(MilitaryCommand.FleetReserveFactor((Entity)null), Is.EqualTo(1.0), "absent-entity fleet factor is exactly 1.0");
            Assert.That(MilitaryCommand.GroundReserveFactor((Entity)null), Is.EqualTo(1.0), "absent-entity ground factor is exactly 1.0");
        }

        [Test]
        [Description("ScaleReserve at factor 1.0 returns EVERY base threshold unchanged (the arithmetic pin) — so a "
                   + "neutral posture leaves MinToDeploy / the garrison reserve exactly where it was.")]
        public void ScaleReserve_AtFactorOne_IsTheIdentity()
        {
            foreach (var n in new[] { 0, 1, 2, 3, 5, 6, 8, 18, 100 })
                Assert.That(MilitaryCommand.ScaleReserve(n, 1.0), Is.EqualTo(n), $"ScaleReserve({n}, 1.0) == {n}");
        }

        // ── The direction: aggressive keeps a smaller reserve, cautious a bigger one ──────────────────────────────

        [Test]
        [Description("An AGGRESSIVE/high-risk personality yields a factor BELOW 1.0 (a smaller reserve → commit more); "
                   + "posture rises above the baseline.")]
        public void AggressivePersonality_YieldsFactorBelowOne()
        {
            var aggressive = P(0.9, 0.9);
            Assert.That(MilitaryCommand.ReservePosture(aggressive), Is.GreaterThan(MilitaryCommand.BaselinePosture),
                "aggression + risk push the posture toward commit-more");
            Assert.That(MilitaryCommand.FleetReserveFactor(aggressive), Is.LessThan(1.0), "smaller fleet reserve");
            Assert.That(MilitaryCommand.GroundReserveFactor(aggressive), Is.LessThan(1.0), "smaller garrison reserve");

            // A smaller factor scales a concrete reserve DOWN (never below a 1-unit floor).
            Assert.That(MilitaryCommand.ScaleReserve(3, MilitaryCommand.FleetReserveFactor(aggressive)),
                Is.LessThan(3), "the aggressive faction keeps fewer than the neutral 3-fleet/unit reserve");
        }

        [Test]
        [Description("A CAUTIOUS/low-risk personality yields a factor ABOVE 1.0 (a bigger reserve → hold more); posture "
                   + "drops below the baseline.")]
        public void CautiousPersonality_YieldsFactorAboveOne()
        {
            var cautious = P(0.1, 0.1);
            Assert.That(MilitaryCommand.ReservePosture(cautious), Is.LessThan(MilitaryCommand.BaselinePosture),
                "low aggression + low risk pull the posture toward hold-more");
            Assert.That(MilitaryCommand.FleetReserveFactor(cautious), Is.GreaterThan(1.0), "bigger fleet reserve");
            Assert.That(MilitaryCommand.GroundReserveFactor(cautious), Is.GreaterThan(1.0), "bigger garrison reserve");

            // A bigger factor scales a concrete reserve UP.
            Assert.That(MilitaryCommand.ScaleReserve(3, MilitaryCommand.GroundReserveFactor(cautious)),
                Is.GreaterThan(3), "the cautious faction keeps more than the neutral 3-fleet/unit reserve");
        }

        // ── Monotonic + bounded across the whole trait range ──────────────────────────────────────────────────────

        [Test]
        [Description("As aggression+risk climb 0→1, the reserve factor DECREASES monotonically, stays within the "
                   + "clamp bounds, and the posture stays within 0..1.")]
        public void Factor_IsMonotonicallyDecreasing_AndBounded()
        {
            double previous = double.PositiveInfinity;
            for (int i = 0; i <= 10; i++)
            {
                double trait = i / 10.0;                       // 0.0 … 1.0
                var personality = P(trait, trait);

                double posture = MilitaryCommand.ReservePosture(personality);
                Assert.That(posture, Is.InRange(0.0, 1.0), $"posture stays in 0..1 at trait {trait}");

                double factor = MilitaryCommand.FleetReserveFactor(personality);
                Assert.That(factor, Is.InRange(MilitaryCommand.MinFactor, MilitaryCommand.MaxFactor),
                    $"factor stays within the clamp bounds at trait {trait}");
                Assert.That(factor, Is.LessThan(previous), $"factor strictly decreases as aggression/risk climb (trait {trait})");
                previous = factor;

                // Fleet and ground share the same math this slice (a future officer could diverge them).
                Assert.That(MilitaryCommand.GroundReserveFactor(personality), Is.EqualTo(factor),
                    "fleet and ground factors match while both derive from the same posture");
            }
        }

        // ── Integration: the ground LOAD-guard shifts with personality on an otherwise-identical setup ────────────

        [Test]
        [Description("Integration read: on ONE colony faction with a garrison target of 6 (neutral reserve 3), the "
                   + "LOAD-rung guard WouldStripReserve moves with the faction's posture — an AGGRESSIVE faction ships "
                   + "a unit off where a neutral one holds; a CAUTIOUS faction holds where a neutral one ships.")]
        public void WouldStripReserve_ShiftsWithPersonality()
        {
            var s = TestScenario.CreateWithColony();
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            fi.GarrisonComposition = new Dictionary<string, int> { { "Infantry", 3 }, { "Armor", 2 }, { "Artillery", 1 } }; // target 6, neutral reserve 3
            int id = s.Faction.Id;

            // NEUTRAL baseline (no personality attached): reserve 3.
            Assert.That(GroundReinforcement.WouldStripReserve(Roster(id, 3), fi, id, s.Faction), Is.True,
                "neutral: 3 units == reserve 3 → shipping one breaches the reserve, so hold");
            Assert.That(GroundReinforcement.WouldStripReserve(Roster(id, 4), fi, id, s.Faction), Is.False,
                "neutral: 4 units > reserve 3 → a surplus, a unit may ship off");

            // AGGRESSIVE: reserve scales DOWN (to 2) → the faction ships the 3rd unit the neutral one held.
            s.Faction.SetDataBlob(P(1.0, 1.0));
            Assert.That(GroundReinforcement.WouldStripReserve(Roster(id, 3), fi, id, s.Faction), Is.False,
                "aggressive: a smaller reserve → 3 units is now a surplus, ship one off");

            // CAUTIOUS: reserve scales UP (to 5) → the faction holds the 4th unit the neutral one shipped.
            s.Faction.SetDataBlob(P(0.0, 0.0));
            Assert.That(GroundReinforcement.WouldStripReserve(Roster(id, 4), fi, id, s.Faction), Is.True,
                "cautious: a bigger reserve → 4 units is at/under the reserve, hold the defenders");
        }

        /// <summary>A roster of <paramref name="count"/> units all owned by <paramref name="factionId"/>.</summary>
        private static GroundForcesDB Roster(int factionId, int count)
        {
            var forces = new GroundForcesDB();
            for (int i = 0; i < count; i++)
                forces.Units.Add(new GroundUnit { FactionOwnerID = factionId, UnitType = GroundUnitType.Infantry });
            return forces;
        }
    }
}
