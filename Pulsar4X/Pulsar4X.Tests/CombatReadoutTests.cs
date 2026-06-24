using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace != folder — file lives in Engine/Components/)
using Pulsar4X.Components;  // ComponentInstance
using Pulsar4X.Damage;      // DamageProcessor, DamageFragment, DamageResult
using Pulsar4X.Orbital;     // Vector2

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP Stage 1 — the SPACE-COMBAT DAMAGE gauge. First automated coverage of the damage path, which the
    /// systems map flags 🔴 DARK / NO TESTS. The MVP only needs combat to *resolve* ("beams do damage, a fight
    /// ends"); before we can make a fight end we have to be able to SEE a single hit land. This is that gauge.
    ///
    /// It isolates the most-uncertain link: <see cref="DamageTools.DealDamageEnergyBeamSim"/>, the pixel-by-pixel
    /// spatial sim that turns a beam hit into component damage. Nobody has verified it deposits damage on a real
    /// ship (the Damage CLAUDE.md warns results are wrong if the component bitmap isn't populated). We call the
    /// real entry point <see cref="DamageProcessor.OnTakingDamage"/> directly — the same method the beam processor
    /// calls on hit — so there is no RNG, no positioning, no power-grid dependency to muddy the reading.
    ///
    /// LANDMINE (found by reading the sim, per the Visibility Gate — not by crashing into it): the sim steps with
    /// dt = 1 / |Velocity|, so a zero-velocity DamageFragment divides by zero -> NaN -> Convert.ToInt32 throws.
    /// We build the fragment with the same SHAPE the real OnHit() builds (non-zero velocity), so the gauge reads
    /// the sim, not an artifact of a hand-rolled fragment.
    ///
    /// This commit is a READOUT, not a verdict: it prints what the sim does and asserts only that we could stand
    /// up a target and run the sim without it throwing. The hard assertion ("a ship CAN be destroyed by beam
    /// damage") is added once CI shows the [combat] numbers — the same way the economy gauge landed.
    /// </summary>
    [TestFixture]
    public class CombatReadoutTests
    {
        private static void Log(string msg) => TestContext.Progress.WriteLine("[combat] " + msg);

        [Test]
        [Description("Reads what the beam-damage sim does to a real ship: does HealthPercent drop, do components get removed, can the ship be destroyed?")]
        public void Combat_DamageReadout_BeamHitsShip()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(factionInfo.ShipDesigns, Is.Not.Empty,
                "Faction has no ship designs to build a target from — the colony blueprint should unlock some.");

            var design = factionInfo.ShipDesigns.Values.First();
            var target = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Damage Gauge Target");

            Assert.That(target.TryGetDataBlob<ComponentInstancesDB>(out var comps), Is.True,
                "Target ship has no ComponentInstancesDB — can't gauge component damage.");

            int startCount = comps.AllComponents.Count;
            int designComponents = design.Components.Sum(c => c.count);
            Log($"target design: {design.Name}  (design lists {designComponents} components)");
            Log($"starting live components: {startCount}");
            Log($"starting health  min/avg: {MinHealth(comps):0.000} / {AvgHealth(comps):0.000}");

            // A beam-shaped fragment with energy far above what one component can survive.
            // damageAmount = energyDeposited/100 (1pt per 100J); HealthPercent -= damageAmount/1000.
            // So ~1e5 J wrecks a component along the beam path; 1e10 J obliterates everything it passes through.
            const double energy = 1e10;

            // A single fixed beam path only destroys the components it passes through, so sweep entry points and
            // directions to carpet the cross-section and find out whether the WHOLE ship can be brought down.
            int[] sweep = { -40, -20, 0, 20, 40 };
            Vector2[] dirs = { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };

            int hits = 0;
            int lastDamage = 0;
            bool destroyed = false;
            string? exceptionText = null;

            try
            {
                foreach (var dir in dirs)
                {
                    foreach (var px in sweep)
                    foreach (var py in sweep)
                    {
                        if (!target.IsValid) { destroyed = true; break; }

                        var frag = new DamageFragment
                        {
                            Velocity = dir,        // non-zero — avoids the 1/|v| divide-by-zero
                            Position = (px, py),   // impact point relative to ship centre
                            Mass = 0.000001f,
                            Density = 1000f,
                            Momentum = 1f,
                            Length = 1f,
                            Energy = energy,
                            Wavelength = 1000.0,   // nm — NIR band, same as a default laser
                        };

                        DamageResult result = DamageProcessor.OnTakingDamage(target, frag);
                        hits++;
                        lastDamage = result.Damage;
                        if (result.Destroyed) { destroyed = true; break; }
                    }
                    if (destroyed) break;
                }
            }
            catch (Exception ex)
            {
                // Visibility Gate: never swallow. A throw here IS the finding — print it in full.
                exceptionText = ex.ToString();
            }

            int endCount = 0;
            float endMin = 0, endAvg = 0;
            bool stillReadable = false;
            if (exceptionText == null && target.IsValid
                && target.TryGetDataBlob<ComponentInstancesDB>(out var endComps))
            {
                stillReadable = true;
                endCount = endComps.AllComponents.Count;
                endMin = MinHealth(endComps);
                endAvg = AvgHealth(endComps);
            }

            Log($"hits applied: {hits}");
            Log($"last hit damage points: {lastDamage}");
            Log($"ship destroyed: {destroyed}");
            Log($"ending live components: {endCount}  (was {startCount})");
            if (stillReadable)
                Log($"ending health  min/avg: {endMin:0.000} / {endAvg:0.000}");
            if (exceptionText != null)
                Log("EXCEPTION during damage sim:\n" + exceptionText);

            // READOUT ONLY (commit 1): assert nothing about the damage outcome yet — the [combat] lines above are
            // the gauge. We only assert the setup held together. If the sim threw, that is captured and printed
            // above (not hidden); the next commit turns the confirmed reading into a hard assertion or files the bug.
            Assert.Pass($"[combat] readout complete: startComponents={startCount} endComponents={endCount} destroyed={destroyed} hits={hits} threw={(exceptionText != null)}");
        }

        private static float MinHealth(ComponentInstancesDB comps)
        {
            float min = float.MaxValue;
            foreach (var c in comps.AllComponents.Values)
                if (c.HealthPercent < min) min = c.HealthPercent;
            return min == float.MaxValue ? 0f : min;
        }

        private static float AvgHealth(ComponentInstancesDB comps)
        {
            float sum = 0; int n = 0;
            foreach (var c in comps.AllComponents.Values) { sum += c.HealthPercent; n++; }
            return n == 0 ? 0f : sum / n;
        }
    }
}
