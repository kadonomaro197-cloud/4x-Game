using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Hazards;
using Pulsar4X.Damage;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Extensions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The spatial-environments DIORAMA — the developer's "fill the solar system with test ships, each around a
    /// body, each sitting in a different environment; let them sit; ensure the game detects it all" — as a
    /// permanent, headless CI gauge. Park a probe ship at each of six well-separated bodies in Sol, drop a
    /// DIFFERENT hazard on each (Thermal corona / Corrosive cloud / Kinetic debris / HardRadiation belt / EMStorm
    /// ion-storm / Gravimetric anomaly), advance the clock, then assert the player faction has DISCOVERED all six
    /// damage flavours — i.e. the engine detected every environment. If any spatial environment silently stops
    /// working, this goes red and names which one.
    ///
    /// Design notes:
    /// - Each hazard is parented to its BODY (not the star), so the region TRACKS the orbiting ship — a star-fixed
    ///   hazard would drift off a moving ship over the days. Radius is a few body-radii (covers the ship's ~2x-radius
    ///   spawn orbit) yet tiny next to inter-body gaps.
    /// - The six host bodies are chosen GREEDILY to be maximally far apart, so no two regions overlap and each ship
    ///   meets exactly one flavour (otherwise a ship near two hazards would discover both).
    /// - Discovery fires on the processor's first 5 s pass; the remaining sit-time just confirms it runs steadily
    ///   (and accrues real damage — the probes are sacrificial). Knowledge is recorded on the FACTION, so it persists
    ///   even after a probe is destroyed by its environment.
    /// </summary>
    [TestFixture]
    public class SpatialEnvironmentsDioramaTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[diorama] " + m);

        // The six spatial environments: display name, the hazard effect kind it emits, that effect's wavelength
        // (nm; 0 for the non-photon flavours), and the shared DamageSignature the faction should end up knowing.
        private static readonly (string Name, HazardEffectType Effect, double Wavelength, DamageSignature Sig)[] Environments =
        {
            ("Solar Corona",        HazardEffectType.HeatDamage,        10000, DamageSignature.Thermal),
            ("Corrosive Gas Cloud", HazardEffectType.CorrosiveDamage,   0,     DamageSignature.Corrosive),
            ("Debris Field",        HazardEffectType.KineticDamage,     0,     DamageSignature.Kinetic),
            ("Radiation Belt",      HazardEffectType.RadiationDamage,   150,   DamageSignature.HardRadiation),
            ("Ion Storm",           HazardEffectType.EMDamage,          0,     DamageSignature.EMStorm),
            ("Gravimetric Anomaly", HazardEffectType.GravimetricDamage, 0,     DamageSignature.Gravimetric),
        };

        [Test]
        [Description("Six probe ships, six different spatial environments, one system — let it run and assert the " +
                     "faction discovers all six damage flavours. The whole hazard system verified in one headless run.")]
        public void AllSpatialEnvironments_AreDetected_WhenShipsSitInThem()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var system = s.StartingSystem;

            // Candidate hosts: any body with a position and a radius.
            var bodies = system.GetAllDataBlobsOfType<SystemBodyInfoDB>()
                .Select(b => b.OwningEntity)
                .Where(e => e != null && e.HasDataBlob<PositionDB>() && e.HasDataBlob<MassVolumeDB>())
                .ToList();
            Assert.That(bodies.Count, Is.GreaterThanOrEqualTo(Environments.Length),
                $"need at least {Environments.Length} bodies to host the diorama; found {bodies.Count}");

            Vector3 Pos(Entity e) => e.GetDataBlob<PositionDB>().AbsolutePosition;

            // Greedily pick N bodies that are maximally far apart, so the hazard regions never overlap.
            var picked = new List<Entity> { bodies[0] };
            while (picked.Count < Environments.Length)
            {
                Entity best = null;
                double bestMin = -1;
                foreach (var b in bodies)
                {
                    if (picked.Contains(b)) continue;
                    double minDist = picked.Min(p => (Pos(b) - Pos(p)).Length());
                    if (minDist > bestMin) { bestMin = minDist; best = b; }
                }
                picked.Add(best);
            }

            // Park a probe at each body and drop that body's hazard, parented to the body so it tracks the probe.
            for (int i = 0; i < Environments.Length; i++)
            {
                var env = Environments[i];
                var body = picked[i];
                double bodyRadius = body.GetDataBlob<MassVolumeDB>().RadiusInM;
                double hazardRadius = Math.Max(bodyRadius * 4.0, 1e7); // covers the ship's ~2x-radius orbit; tiny vs inter-body gaps

                ShipFactory.CreateShip(design, s.Faction, body, $"Probe-{env.Name}");
                var effects = new List<HazardEffect> { new HazardEffect(env.Effect, 100.0, env.Wavelength) };
                SpaceHazardFactory.CreateFromEffects(system, body, Vector3.Zero, hazardRadius,
                    SpaceHazardType.Generic, effects, env.Name);
                Log($"placed {env.Name} ({env.Sig}) on {body.GetDefaultName()} — hazard r={hazardRadius:E1} m");
            }

            // Let them sit in their environments. Drive the hazard processor over simulated time the way the
            // project's other hazard tests do (Flare_BlindsThenExpires calls ProcessEntity directly) rather than
            // leaning on AdvanceTime — this colony harness doesn't reliably auto-fire hotloops, so a direct drive
            // keeps the gauge deterministic. Each pass = 5 s of sitting; the processor finds each probe inside its
            // region and records the discovery on the first pass, the rest just confirms it runs steadily.
            var processor = new SpaceHazardProcessor();
            for (int pass = 0; pass < 12; pass++)
                processor.ProcessManager(system, 5);

            // Read the gauge: what did the faction LEARN? (Knowledge persists on the faction even if a probe died.)
            s.Faction.TryGetDataBlob<FactionHazardKnowledgeDB>(out var knowledge);
            Log("=== diorama readout: environment -> detected? ===");
            var missing = new List<string>();
            foreach (var env in Environments)
            {
                bool known = knowledge != null && knowledge.Knows(env.Sig);
                Log($"  {env.Name,-22} [{env.Sig,-13}]  {(known ? "DETECTED" : "MISSED")}");
                if (!known) missing.Add($"{env.Name} ({env.Sig})");
            }

            Assert.That(missing, Is.Empty,
                "every spatial environment must be detected by the faction; missed: " + string.Join(", ", missing));
        }
    }
}
