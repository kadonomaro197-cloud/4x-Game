using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// "What the player can SEE" gauges — the engine half of closing the gap between what the simulation KNOWS and
    /// what it tells the player. The client can't be tested in CI, so the rule (Pulsar4X.Client/CLAUDE.md) is to
    /// push the real logic into engine accessors and gauge THOSE here; the client ring/readout becomes a thin draw.
    ///
    /// Two numbers the engine computed but never surfaced:
    ///   • ENGAGEMENT RANGE — how far a ship can land a beam hit. The MaxRange the firing processor already enforces
    ///     (GenericBeamWeaponAtb.IsInRange); WeaponUtils.GetMaxBeamRange_m / GetBeamWeaponRanges expose it so the UI
    ///     can draw a range ring instead of the gun silently refusing to fire.
    ///   • DETECTION RANGE — how far a ship can SEE. The scan loop only ever asks "is this target's faded signal
    ///     above threshold at its current distance?" (a yes/no), so no "how far can I see" number existed. SensorTools
    ///     .RangeForSignal / DetectionRange_m / SelfDetectionRange_m run that same attenuation BACKWARDS to produce one.
    /// </summary>
    [TestFixture]
    public class RangeReadoutTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[range-readout] " + m);

        // ─── Engagement range (beam weapons) ───────────────────────────────────────────────────────────────────

        [Test]
        [Description("A ship's engagement range is the largest MaxRange among its installed beam weapons. The Aegis " +
                     "warship carries lasers, so the readout reports a positive reach and the per-weapon breakdown " +
                     "lines up with the aggregate — the number the firing processor enforces, now visible.")]
        public void BeamRange_AggregatesAcrossInstalledWeapons()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey("default-ship-design-test-warship"), Is.True,
                "the base mod must define the Aegis warship (a beam-armed design) for this gauge");

            var ship = ShipFactory.CreateShip(designs["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Aegis");

            double maxRange = WeaponUtils.GetMaxBeamRange_m(ship);
            var rows = WeaponUtils.GetBeamWeaponRanges(ship);
            Log($"Aegis beam reach = {maxRange:N0} m across {rows.Count} beam design row(s)");
            foreach (var r in rows)
                Log($"  {r.name}: max={r.maxRange:N0} m, optimal={r.optimalRange:N0} m");

            Assert.That(maxRange, Is.GreaterThan(0), "a beam-armed ship must report a finite engagement range");
            Assert.That(rows.Count, Is.GreaterThan(0), "the per-weapon breakdown must list at least the laser design");
            Assert.That(maxRange, Is.EqualTo(rows.Max(r => r.maxRange)).Within(1e-9),
                "the aggregate reach must equal the largest per-weapon MaxRange");
            foreach (var r in rows)
            {
                Assert.That(r.maxRange, Is.GreaterThan(0), "a reported beam row must have a finite (non-legacy) MaxRange");
                // NOTE: optimalRange (focal length) and maxRange (hard cutoff) are INDEPENDENT design knobs — the
                // base-mod laser ships focal length 1,000,000 m vs MaxRange 5,000 m, so optimal can exceed max (the
                // whole firing envelope is then inside optimal = no falloff). So we only assert it's reported, not
                // any ordering between the two.
                Assert.That(r.optimalRange, Is.GreaterThanOrEqualTo(0), "optimal range is reported (a real, non-negative number)");
            }
        }

        [Test]
        [Description("A ship with no beam weapons reports zero engagement range — the UI's signal to draw no beam ring.")]
        public void BeamRange_NoBeamWeapons_ReportsZero()
        {
            var s = TestScenario.CreateWithColony();
            // The colony itself has component instances but no beam weapon — a clean "nothing to draw" case.
            double range = WeaponUtils.GetMaxBeamRange_m(s.Colony);
            Log($"colony beam reach = {range:N0} m (expect 0)");
            Assert.That(range, Is.EqualTo(0), "an entity with no beam weapon must report zero engagement range");
            Assert.That(WeaponUtils.GetBeamWeaponRanges(s.Colony), Is.Empty);
        }

        // ─── Detection range (the reverse-solve) ───────────────────────────────────────────────────────────────

        [Test]
        [Description("RangeForSignal is the exact inverse of the attenuation the scan uses: the distance it returns " +
                     "is the distance at which AttenuationCalc fades the source back down to the threshold. Round-trip.")]
        public void RangeForSignal_InvertsAttenuation()
        {
            const double source = 5e8;   // kW at the source
            const double threshold = 2.0; // kW the sensor needs to register

            double d = SensorTools.RangeForSignal(source, threshold);
            double fadedBack = SensorTools.AttenuationCalc(source, d);
            Log($"source {source:E2} kW fades to threshold {threshold} kW at {d:N0} m (back-calc = {fadedBack:E3} kW)");

            Assert.That(d, Is.GreaterThan(0), "a real source/threshold must yield a finite range");
            Assert.That(fadedBack, Is.EqualTo(threshold).Within(1e-6).Percent,
                "the range must be exactly where the signal fades to threshold — the reverse-solve must invert the forward math");

            // Guards: nothing to detect, or a degenerate perfect sensor → 0 (caller treats as 'don't draw').
            Assert.That(SensorTools.RangeForSignal(0, 1), Is.EqualTo(0), "no source = no range");
            Assert.That(SensorTools.RangeForSignal(1, 0), Is.EqualTo(0), "a zero/perfect threshold = unbounded, returned as 0");
        }

        [Test]
        [Description("DetectionRange_m takes the loudest band, scales EMITTED by the target's activity (run hot = " +
                     "seen farther, go dark = seen closer) but NOT reflected, and the louder band wins. The gameplay " +
                     "truth of the dark-vs-loud lever, as a drawable number.")]
        public void DetectionRange_LoudestBandWins_ActivityScalesEmittedOnly()
        {
            // BestSensitivity_kW = 1.0 (constructor takes watts, ×0.001 → kW).
            var receiver = new SensorReceiverAtb(peakWaveLength: 500, bandwidth: 400,
                bestSensitivity: 1000, worstSensitivity: 2000, resolution: 1, scanTime: 3600);
            Assert.That(receiver.BestSensitivity_kW, Is.EqualTo(1.0).Within(1e-12), "sanity: 1000 W threshold = 1.0 kW");

            var profile = new SensorProfileDB();
            profile.EmittedEMSpectra.Add(new EMData { WaveForm = new EMWaveForm(400, 500, 600), Magnitude = 1e9 });

            profile.ActivityMultiplier = 1.0;
            double full = SensorTools.DetectionRange_m(receiver, profile);
            double expectedFull = Math.Sqrt(1e9 / (4 * Math.PI * 1.0));
            Assert.That(full, Is.EqualTo(expectedFull).Within(1e-6).Percent, "full-power detection range follows the reverse-solve");

            // Go dark: emitted signature drops, so you're first seen closer — range scales by sqrt(activity).
            profile.ActivityMultiplier = 0.15;
            double dark = SensorTools.DetectionRange_m(receiver, profile);
            Log($"detection range vs a ship like this: full={full:N0} m  dark(0.15)={dark:N0} m  ratio={dark / full:F3}");
            Assert.That(dark, Is.LessThan(full), "going dark must shrink how far off this target is first seen");
            Assert.That(dark, Is.EqualTo(full * Math.Sqrt(0.15)).Within(1e-6).Percent, "emitted range scales by sqrt(activity)");

            // Add a LOUDER reflected (radar-return) band. Reflected is NOT scaled by activity, so even at dark 0.15
            // it now dominates — the loudest band wins and the activity dial no longer moves the range.
            profile.ReflectedEMSpectra.Add(new EMData { WaveForm = new EMWaveForm(1400, 1500, 1600), Magnitude = 1e10 });
            double withReflect = SensorTools.DetectionRange_m(receiver, profile);
            double expectedReflect = Math.Sqrt(1e10 / (4 * Math.PI * 1.0));
            Assert.That(withReflect, Is.EqualTo(expectedReflect).Within(1e-6).Percent,
                "a louder reflected band sets the range, and (unlike emitted) it ignores the activity dial");
        }

        [Test]
        [Description("SelfDetectionRange_m on a real sensor-bearing ship answers 'a ship like me, I'd first see at " +
                     "range R'. Going Silent shrinks that ring; going back to Full restores it — the EMCON lever made " +
                     "visible as a number, end to end on a real ship + the real posture path.")]
        public void SelfDetectionRange_ShrinksWhenGoingDark()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Scout Force");

            // The capital design carries BOTH a sensor receiver and a reactor signature, so it can see and be seen.
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue("default-ship-design-test-capital", out var cap) ? cap : designs.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Scout");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship));

            Assert.That(ship.HasDataBlob<SensorAbilityDB>(), Is.True, "the scout must carry a sensor receiver to have any reach");

            FleetEmcon.SetPosture(fleet, EmconPosture.Full);
            double full = SensorTools.SelfDetectionRange_m(ship);

            FleetEmcon.SetPosture(fleet, EmconPosture.Silent);
            double dark = SensorTools.SelfDetectionRange_m(ship);

            FleetEmcon.SetPosture(fleet, EmconPosture.Full);
            double restored = SensorTools.SelfDetectionRange_m(ship);

            Log($"self detection ring: Full={full:N0} m  Silent={dark:N0} m  back-to-Full={restored:N0} m");
            Assert.That(full, Is.GreaterThan(0), "a sensing, emitting ship must have a non-zero self-detection ring at Full");
            Assert.That(dark, Is.LessThan(full), "running Silent shrinks the self-detection ring (you and a ship like you are both quieter)");
            Assert.That(restored, Is.EqualTo(full).Within(1e-6).Percent, "back to Full restores the ring — the lever is reversible");
        }

        [Test]
        [Description("DetectionRangeAgainst(detector, target) reads the TARGET's real signature: the same enemy ship " +
                     "is picked up farther off when it runs hot (Full) than when it goes Silent. This is the honest " +
                     "'detectability bubble' a ring-against-the-selected-enemy draws — range depends on the specific target.")]
        public void DetectionRangeAgainst_LoudTargetSeenFartherThanQuiet()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue("default-ship-design-test-capital", out var cap) ? cap : designs.Values.First();

            var watcher = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Watcher");   // carries the receiver

            var bogey = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Bogey");        // emits a signature
            bogey.FactionOwnerID = enemyFaction.Id;
            var enemyFleet = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Red Squadron");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(enemyFaction.Id, enemyFleet, bogey));

            FleetEmcon.SetPosture(enemyFleet, EmconPosture.Full);
            double loud = SensorTools.DetectionRangeAgainst(watcher, bogey);

            FleetEmcon.SetPosture(enemyFleet, EmconPosture.Silent);
            double quiet = SensorTools.DetectionRangeAgainst(watcher, bogey);

            Log($"detection bubble vs the bogey: Full={loud:N0} m  Silent={quiet:N0} m  ratio={quiet / loud:F3}");
            Assert.That(loud, Is.GreaterThan(0), "a sensing ship must detect a loud emitting target at some range");
            Assert.That(quiet, Is.LessThan(loud), "the same target running Silent is picked up closer in — range reads the target's signature");
        }
    }
}
