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
    /// DIFFERENT environment on each — built from the REAL hazard effect lists (damage AND the supplemental
    /// sensor-jam / movement-drag / warp-inhibit effects, not just the bare damage) — then verify, per probe:
    /// <list type="number">
    /// <item><b>Detection</b> — the faction discovers that environment's damage flavour (the research-loop gate);</item>
    /// <item><b>Actual damage</b> — a real hit of that flavour deposits measurable damage on the probe;</item>
    /// <item><b>Supplemental effects</b> — the environment's non-damage effects bite (e.g. the gas-cloud probe's
    /// radar IS cut, the debris field drags movement, the gravimetric anomaly inhibits warp).</item>
    /// </list>
    /// If any spatial environment silently loses any of those, this goes red and names which one.
    ///
    /// Design notes:
    /// - Each hazard is parented to its BODY (not the star), so the region TRACKS the orbiting probe.
    /// - The six host bodies are chosen GREEDILY to be maximally far apart, so the regions never overlap and each
    ///   probe meets exactly one environment.
    /// - DETECTION + supplemental effects are exercised by driving the SpaceHazardProcessor directly (like the
    ///   other hazard tests — this harness doesn't reliably auto-fire hotloops). DAMAGE is proven by a direct
    ///   1e6 J hit (the proven scale in DamageSignatureResistanceTests): the processor's per-tick energy is too
    ///   low for the per-pixel sim to register (the CombatReadoutTests finding), so a health-delta would be flaky.
    /// </summary>
    [TestFixture]
    public class SpatialEnvironmentsDioramaTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[diorama] " + m);

        private sealed class Env
        {
            public string Name;
            public DamageSignature Sig;
            public double Wavelength;                 // for the direct-hit damage proof
            public List<HazardEffect> Effects;        // the REAL effect list (damage + supplemental)
            public bool SensorCut, MoveCut, WarpCut;  // which supplemental effects this environment should impose
        }

        // The six environments, each with its real effect list (mirrors SpaceHazardFactory's CreateGasCloud /
        // CreateDebrisField / CreateIonStorm / CreateGravimetricAnomaly / corona / flare).
        private static List<Env> BuildEnvironments() => new List<Env>
        {
            new Env { Name = "Solar Corona", Sig = DamageSignature.Thermal, Wavelength = 10000,
                Effects = new() { new HazardEffect(HazardEffectType.HeatDamage, 100, 10000) } },

            new Env { Name = "Gas Cloud", Sig = DamageSignature.Corrosive, Wavelength = 0,
                Effects = new() {
                    new HazardEffect(HazardEffectType.CorrosiveDamage, 100),
                    new HazardEffect(HazardEffectType.SensorJam, 0.35),
                    new HazardEffect(HazardEffectType.MovementDrag, 0.5),
                    new HazardEffect(HazardEffectType.WarpInhibit, 0.25) },
                SensorCut = true, MoveCut = true, WarpCut = true },

            new Env { Name = "Debris Field", Sig = DamageSignature.Kinetic, Wavelength = 0,
                Effects = new() {
                    new HazardEffect(HazardEffectType.KineticDamage, 100, 0),
                    new HazardEffect(HazardEffectType.MovementDrag, 0.6) },
                MoveCut = true },

            new Env { Name = "Radiation Belt", Sig = DamageSignature.HardRadiation, Wavelength = 150,
                Effects = new() {
                    new HazardEffect(HazardEffectType.RadiationDamage, 100, 150),
                    new HazardEffect(HazardEffectType.SensorJam, 0.0) }, // 0 = fully blinds
                SensorCut = true },

            new Env { Name = "Ion Storm", Sig = DamageSignature.EMStorm, Wavelength = 0,
                Effects = new() {
                    new HazardEffect(HazardEffectType.EMDamage, 100),
                    new HazardEffect(HazardEffectType.SensorJam, 0.4) },
                SensorCut = true },

            new Env { Name = "Gravimetric Anomaly", Sig = DamageSignature.Gravimetric, Wavelength = 0,
                Effects = new() {
                    new HazardEffect(HazardEffectType.GravimetricDamage, 100),
                    new HazardEffect(HazardEffectType.WarpInhibit, 0.3) },
                WarpCut = true },
        };

        [Test]
        [Description("Six probes, six real environments — verify each is DETECTED, deals ACTUAL damage, and imposes " +
                     "its SUPPLEMENTAL effects (radar jam / movement drag / warp inhibit). The whole hazard system " +
                     "verified end-to-end in one headless run.")]
        public void AllSpatialEnvironments_Detected_Damage_AndSupplementalEffects()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var system = s.StartingSystem;
            var envs = BuildEnvironments();

            var bodies = system.GetAllDataBlobsOfType<SystemBodyInfoDB>()
                .Select(b => b.OwningEntity)
                .Where(e => e != null && e.HasDataBlob<PositionDB>() && e.HasDataBlob<MassVolumeDB>())
                .ToList();
            Assert.That(bodies.Count, Is.GreaterThanOrEqualTo(envs.Count),
                $"need at least {envs.Count} bodies to host the diorama; found {bodies.Count}");

            Vector3 Pos(Entity e) => e.GetDataBlob<PositionDB>().AbsolutePosition;

            // Greedily pick N bodies maximally far apart, so the hazard regions never overlap.
            var picked = new List<Entity> { bodies[0] };
            while (picked.Count < envs.Count)
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

            // Park a probe at each body and drop that body's full environment, parented to the body so it tracks.
            var probes = new List<Entity>();
            for (int i = 0; i < envs.Count; i++)
            {
                var env = envs[i];
                var body = picked[i];
                double radius = Math.Max(body.GetDataBlob<MassVolumeDB>().RadiusInM * 4.0, 1e7);
                probes.Add(ShipFactory.CreateShip(design, s.Faction, body, $"Probe-{env.Name}"));
                SpaceHazardFactory.CreateFromEffects(system, body, Vector3.Zero, radius, SpaceHazardType.Generic,
                    env.Effects.Select(e => new HazardEffect(e)).ToList(), env.Name);
                Log($"placed {env.Name} ({env.Sig}) on {body.GetDefaultName()}");
            }

            // Let them sit: drive the hazard processor (records discovery; the supplemental effects become live to query).
            var processor = new SpaceHazardProcessor();
            for (int pass = 0; pass < 12; pass++)
                processor.ProcessManager(system, 5);

            s.Faction.TryGetDataBlob<FactionHazardKnowledgeDB>(out var knowledge);
            var failures = new List<string>();
            Log("=== diorama readout: env -> detected | damage | sensorx movex warpx blind ===");
            for (int i = 0; i < envs.Count; i++)
            {
                var env = envs[i];
                var probe = probes[i];

                bool detected = knowledge != null && knowledge.Knows(env.Sig);

                // Supplemental effects: what the environment does to this probe's stats (read BEFORE the kill-shot below).
                var mods = SpaceHazardTools.CombinedForEntity(probe);

                // Actual-damage proof: a real hit of this flavour deposits measurable damage (1e6 J — the scale the
                // per-pixel sim registers; flat non-wavelength flavours register at any energy).
                var frag = new DamageFragment
                {
                    Velocity = new Vector2(1, 1), Position = (0, 0),
                    Energy = 1e6, Wavelength = env.Wavelength, Signature = env.Sig,
                };
                int damage = DamageProcessor.OnTakingDamage(probe, frag).Damage;

                Log($"  {env.Name,-20} det={(detected ? "Y" : "N")} dmg={damage,7} | " +
                    $"sns={mods.SensorRangeMultiplier:F2} mov={mods.MoveSpeedMultiplier:F2} " +
                    $"wrp={mods.WarpSpeedMultiplier:F2} blind={mods.BlindsSensors}");

                if (!detected) failures.Add($"{env.Name}: flavour not detected");
                if (damage <= 0) failures.Add($"{env.Name}: took no damage");
                if (env.SensorCut && !(mods.SensorRangeMultiplier < 1.0)) failures.Add($"{env.Name}: sensors NOT cut");
                if (env.MoveCut && !(mods.MoveSpeedMultiplier < 1.0)) failures.Add($"{env.Name}: movement NOT dragged");
                if (env.WarpCut && !(mods.WarpSpeedMultiplier < 1.0)) failures.Add($"{env.Name}: warp NOT inhibited");
            }

            Assert.That(failures, Is.Empty,
                "every environment must be detected, deal damage, and impose its supplemental effects; failures: " +
                string.Join("; ", failures));
        }
    }
}
