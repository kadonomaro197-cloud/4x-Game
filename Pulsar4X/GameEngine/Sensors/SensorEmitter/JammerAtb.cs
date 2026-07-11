using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Extensions;

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// A BARRAGE JAMMER — the offensive Electronic-Warfare component (Sensors ⚙3 ▸ EW, dossier §3.4-A). Where a cloak
    /// (<see cref="CloakAtb"/>) hides YOU by damping your own signature, a jammer blinds THE ENEMY: it floods the band
    /// with noise so hostile sensors need a much stronger signal to resolve anything, shrinking how far off they can
    /// detect you and your fleet. The combat resolver already rewards seeing first (a blinded fleet takes fire without
    /// returning it — the FirstStrike gauge), so manufacturing that blindness is a real weapon.
    ///
    /// The catch, and why it's a decision not a free win: a barrage jammer is a loud ACTIVE emitter — it lights the
    /// jammer ship up like a beacon (<see cref="SelfSignatureBoost"/> raises its own <c>ActivityMultiplier</c>), so
    /// the enemy can't resolve you well but they sure know something loud is out there. Blind them, and paint a target
    /// on yourself.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6): researched / built / installed / lost like
    /// any part. It acts on the SAME detection substrate Detection uses — the barrage divides down the signal a hostile
    /// receiver gets in <see cref="SensorTools.GetDetectedEntites"/> (equivalent to raising their sensitivity threshold),
    /// exactly as a hazard already degrades an observer's scan. <b>No jammer → divisor 1.0 / boost 1.0 → detection is
    /// byte-identical</b> (every current ship). Gated behind <see cref="EnableJamming"/> (default off; the client turns it
    /// on) so the whole effect is inert until enabled — the same flag discipline as the closing/detection wires.
    ///
    /// v1 scope (flagged): barrage only (degrades a receiver GLOBALLY within range) — TARGETED jam (blind one specific
    /// fire-control) and the ECCM counter both defer per the dossier; a jammer degrades OTHER factions' receivers, never
    /// its owner's (indiscriminate self-noise is a later balance question).
    /// </summary>
    public class JammerAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Master gate. Default OFF → <see cref="JammingDivisorAgainst"/> is never consulted and
        /// <see cref="SelfSignatureFactor"/> returns 1.0, so detection is byte-identical. The client turns it on. Gated
        /// like the closing-range / detection / fire-control flags because it changes the detection picture.</summary>
        public static bool EnableJamming = false;

        /// <summary>How much a hostile receiver's usable signal is DIVIDED within range (≥ 1; higher = blinder). Because
        /// detection range scales with the square root of signal, a 4× degrade roughly halves how far the enemy detects.
        /// Clamped ≥ 1 (a jammer can't help the enemy see).</summary>
        [JsonProperty] public double SensitivityDegrade { get; internal set; } = 1.0;

        /// <summary>Reach of the jamming, metres. A hostile receiver within this distance of the jammer is degraded;
        /// beyond it, unaffected (a hard cutoff — barrage falloff is a later refinement).</summary>
        [JsonProperty] public double Range_m { get; internal set; } = 0.0;

        /// <summary>The beacon catch (≥ 1): a live jammer multiplies its OWN ship's emitted signature UP by this, so an
        /// active jammer is easy to find (just hard to resolve past). 1 = silent jammer (unrealistic; the honest value
        /// is > 1). Clamped ≥ 1.</summary>
        [JsonProperty] public double SelfSignatureBoost { get; internal set; } = 1.0;

        public JammerAtb() { }

        // 3-double ctor for the JSON/NCalc binder (gotcha L7 / exact-arity): the template feeds
        // AtbConstrArgs(SensitivityDegrade, Range, SelfSignatureBoost).
        public JammerAtb(double sensitivityDegrade, double range_m, double selfSignatureBoost)
        {
            SensitivityDegrade = sensitivityDegrade < 1.0 ? 1.0 : sensitivityDegrade;
            Range_m = range_m < 0.0 ? 0.0 : range_m;
            SelfSignatureBoost = selfSignatureBoost < 1.0 ? 1.0 : selfSignatureBoost;
        }

        public override object Clone() => new JammerAtb(SensitivityDegrade, Range_m, SelfSignatureBoost);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Barrage Jammer";
        public string AtbDescription()
            => $"An active barrage jammer: divides a hostile receiver's usable signal by {SensitivityDegrade:0.#}× out to {Range_m / 1000:0} km (≈ ×{1.0 / Math.Sqrt(SensitivityDegrade):0.00} their detection range). Loud: raises this ship's own signature ×{SelfSignatureBoost:0.#} (a beacon).";

        /// <summary>The combined jamming divisor a receiver at <paramref name="receiverPos"/> (faction
        /// <paramref name="receiverFactionId"/>) suffers from every HOSTILE, in-range, undestroyed jammer in
        /// <paramref name="entities"/> (≥ 1; 1 = no jamming). The receiver's usable signal is divided by this. Each
        /// jammer's contribution is health-scaled (a shot-off jammer contributes 1.0 — the grave rung), and multiple
        /// jammers compound. A jammer never blinds its OWN faction. Pure; defensive (no jammers → 1.0).</summary>
        public static double JammingDivisorAgainst(Vector3 receiverPos, int receiverFactionId, IReadOnlyList<Entity> entities)
        {
            double divisor = 1.0;
            if (entities == null) return divisor;
            foreach (var e in entities)
            {
                if (e == null || e.FactionOwnerID == receiverFactionId) continue;   // own jammer doesn't blind you
                if (!e.TryGetDataBlob<ComponentInstancesDB>(out var comps)) continue;
                if (!comps.TryGetComponentsByAttribute<JammerAtb>(out var jammers)) continue;
                if (!e.TryGetDataBlob<PositionDB>(out var pos)) continue;
                double dist = pos.GetDistanceTo_m(receiverPos);
                foreach (var comp in jammers)
                {
                    if (comp.Design.TryGetAttribute<JammerAtb>(out var j) && j.SensitivityDegrade > 1.0 && dist <= j.Range_m)
                        divisor *= 1.0 + (j.SensitivityDegrade - 1.0) * comp.HealthPercent;
                }
            }
            return divisor;
        }

        /// <summary>The self-signature boost a ship's own active jammer imposes (≥ 1; 1 = none). The STRONGEST installed
        /// jammer wins, health-scaled (a damaged jammer emits less → quieter beacon). Multiplies the ship's
        /// <c>ActivityMultiplier</c> in <see cref="EmconActivityProcessor"/>. Off unless <see cref="EnableJamming"/> is on
        /// → byte-identical. Defensive: no jammer → 1.0.</summary>
        public static double SelfSignatureFactor(Entity ship)
        {
            if (!EnableJamming || ship == null || !ship.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 1.0;
            double best = 1.0;
            if (instances.TryGetComponentsByAttribute<JammerAtb>(out var jammers))
            {
                foreach (var comp in jammers)
                {
                    if (comp.Design.TryGetAttribute<JammerAtb>(out var j))
                    {
                        double eff = 1.0 + (j.SelfSignatureBoost - 1.0) * comp.HealthPercent;
                        if (eff > best) best = eff;
                    }
                }
            }
            return best;
        }
    }
}
