using System;
using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// A CLOAK / signature-damping device — the Electronic-Warfare stealth component (Sensors ⚙3). It is a stronger,
    /// COMPONENT-based EMCON: where the posture lever (<see cref="FleetEmcon"/>) and activity (<see cref="EmconActivityProcessor"/>)
    /// set how loud a ship runs, a cloak multiplies that emitted signature DOWN further, so the ship is picked up only
    /// at much shorter range (detection range ≈ √signature, so a 0.2 cloak roughly halves how far off you can be seen).
    /// The stealth-ship maker — sneak past a picket, ambush from close, scout unseen.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6): designed / researched / built / mounted /
    /// lost like any part — cradle to grave. <see cref="EmconActivityProcessor"/> multiplies a ship's
    /// <c>SensorProfileDB.ActivityMultiplier</c> by <see cref="CloakFactor"/> (the best installed cloak, health-scaled),
    /// so a shot-off cloak (the grave rung) drops you back to your normal signature — you light up. <b>No cloak → factor
    /// 1.0 → the signature is unchanged → detection is byte-identical</b> (every current ship). Inert on install (the
    /// processor reads the number); install/uninstall are no-ops. Never throws.
    ///
    /// v1 scope (flagged): a cloak damps the EMITTED signature (the EMCON substrate), NOT the RADAR reflection — an
    /// ACTIVE ping still bounces off the hull (going dark doesn't shrink your hull, the engine's standing rule). A true
    /// "invisible to active radar" cloak that also scales `ReflectedEMSpectra` is a follow-up.
    /// </summary>
    public class CloakAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Floor on the applied signature factor — a cloak is never PERFECT invisibility (mirrors EMCON's
        /// Silent 0.15 ≠ 0). Flagged BALANCE dial.</summary>
        public const double MinSignatureFactor = 0.02;

        /// <summary>The fraction of its normal emitted signature the ship shows when this cloak is at full health
        /// (0..1, lower = stealthier). 0.2 = seen as if 20% as loud → detected at ≈√0.2 ≈ 0.45× the range. Clamped to
        /// [<see cref="MinSignatureFactor"/>, 1].</summary>
        [JsonProperty] public double SignatureMultiplier { get; internal set; } = 1.0;

        public CloakAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod cloak template feeds AtbConstrArgs(PropertyValue(...)).
        public CloakAtb(double signatureMultiplier)
        {
            SignatureMultiplier = signatureMultiplier < MinSignatureFactor ? MinSignatureFactor
                                : signatureMultiplier > 1.0 ? 1.0 : signatureMultiplier;
        }

        public override object Clone() => new CloakAtb(SignatureMultiplier);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Cloak Device";
        public string AtbDescription() => $"A cloak damping the ship's emitted signature to {SignatureMultiplier * 100:0}% — detected only at ≈{Math.Sqrt(SignatureMultiplier):0.00}× the normal range. Damps the EMITTED signature (an active radar ping still bounces off the hull).";

        /// <summary>The signature factor a ship's best installed cloak applies (1.0 = no cloak → byte-identical). The
        /// STRONGEST cloak wins (lowest factor), each scaled by its component health so a damaged cloak conceals less
        /// (health 0 → factor 1.0, the grave rung). Defensive: no components / no cloak → 1.0. Pure over the entity.</summary>
        public static double CloakFactor(Entity ship)
        {
            if (ship == null || !ship.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 1.0;
            double best = 1.0;
            if (instances.TryGetComponentsByAttribute<CloakAtb>(out var cloaks))
            {
                foreach (var comp in cloaks)
                {
                    if (comp.Design.TryGetAttribute<CloakAtb>(out var cloak))
                    {
                        // A damaged cloak lerps back toward 1.0 (no concealment) with lost health.
                        double eff = 1.0 - (1.0 - cloak.SignatureMultiplier) * comp.HealthPercent;
                        if (eff < best) best = eff;
                    }
                }
            }
            return best;
        }
    }
}
