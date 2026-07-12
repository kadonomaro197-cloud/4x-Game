using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Propulsion ⚙2 ▸ EXOTIC (reactionless) — the THRUST dial, cradle-to-grave (S13). A reactionless drive's Thrust
    /// feeds <see cref="NewtonThrustAbilityDB.ThrustInNewtons"/> → acceleration → EVASION (agility) and who-dictates
    /// range. But it was FREE — the drive's Mass was just the settable <c>Drive Mass</c> dial, independent of Thrust,
    /// so a designer could dial huge thrust into a featherweight housing for a free agility win.
    ///
    /// The fix is the "impactful-dial-drives-a-mass-FLOOR" pattern, done in pure NCalc (no engine change): the drive's
    /// Mass = <c>Max(Drive Mass, 5000 + Max(0, Thrust - 200000) / 200)</c>. At the default (Drive Mass 5000, Thrust
    /// 200000) the floor equals 5000 → byte-identical; a high-thrust design can't weigh less than the thrust demands,
    /// and dialing Drive Mass low gets clamped UP to the floor — so it's un-bypassable (unlike a plain additive term
    /// on a settable mass dial). This is the clean resolution of the "settable mass-dial" pattern flagged for the
    /// exotic drives + ground weapons (see Weapons/CLAUDE.md).
    ///
    /// The base-mod <c>default-design-high-thrust-drive</c> dials Thrust to 600,000 N (3× the 200,000 baseline) on the
    /// new Sprint High-Thrust Cruiser (the Nomad's loadout, drive swapped), and:
    ///   (1) AGILITY PAYOFF — the Sprint's thrust reaches the drive (600,000 N) and it out-dodges an identical
    ///       default-drive Nomad (higher <see cref="ShipCombatValueDB.Evasion"/>).
    ///   (2) THE COST — the drive weighs exactly +2,000 more than the default (the mass floor), default untouched.
    ///
    /// Additive / byte-identical (new design + new ship; every existing reactionless drive uses the 200,000 baseline).
    /// Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipHighThrustDriveTests
    {
        private const string Sprint = "default-ship-design-test-sprint";
        private const string Nomad = "default-ship-design-test-nomad";
        private static void Log(string m) => TestContext.Progress.WriteLine("[high-thrust-drive] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        [Test]
        [Description("Agility payoff: the Sprint's high-thrust reactionless drive produces 600,000 N (vs the Nomad's 200,000) and gives it MORE evasion than the otherwise-identical default-drive Nomad — the Thrust dial reaches acceleration → evasion, through the real design → ReactionlessThrustAtb → NewtonThrustAbilityDB → ShipCombatValueDB path.")]
        public void TheHighThrustDrive_OutDodgesTheDefault()
        {
            var s = TestScenario.CreateWithColony();
            var sprint = Build(s, Sprint, "Sprint");
            var nomad = Build(s, Nomad, "Nomad");
            double sThrust = sprint.GetDataBlob<NewtonThrustAbilityDB>().ThrustInNewtons;
            double nThrust = nomad.GetDataBlob<NewtonThrustAbilityDB>().ThrustInNewtons;
            double sEva = sprint.GetDataBlob<ShipCombatValueDB>().Evasion;
            double nEva = nomad.GetDataBlob<ShipCombatValueDB>().Evasion;
            Log($"Sprint thrust={sThrust:0} N evasion={sEva:0.###}; Nomad thrust={nThrust:0} N evasion={nEva:0.###}");
            Assert.That(sThrust, Is.EqualTo(600_000).Within(1), "the Thrust dial reached the drive");
            Assert.That(nThrust, Is.EqualTo(200_000).Within(1), "the default reactionless drive is untouched (byte-identical)");
            Assert.That(sEva, Is.GreaterThan(nEva),
                "more thrust → more agility → the high-thrust cruiser dodges better than the identical default-drive one");
        }

        [Test]
        [Description("The cost: thrust is EARNED via a mass FLOOR — the high-thrust drive weighs exactly the floor term more than the default (5000 + Max(0, 600000-200000)/200 = 7000, i.e. +2000), and the default drive (at the 200,000 baseline → floor 5000) is byte-identical. The floor is un-bypassable (dialing Drive Mass low clamps up to it).")]
        public void TheThrustDial_CostsMass_ViaAnUnbypassableFloor()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-reactionless-drive"].MassPerUnit;
            long fastMass = designs["default-design-high-thrust-drive"].MassPerUnit;
            Log($"default drive mass = {stockMass}, high-thrust = {fastMass}, delta = {fastMass - stockMass}");
            Assert.That(fastMass, Is.GreaterThan(stockMass),
                "thrust now costs mass via the floor — a bigger drive can't be a featherweight");
            Assert.That(fastMass - stockMass, Is.EqualTo(2000),
                "the extra mass is exactly the floor term (5000 + (600000-200000)/200 = 7000) − the default 5000 = 2000; the default drive pays nothing (byte-identical)");
        }
    }
}
