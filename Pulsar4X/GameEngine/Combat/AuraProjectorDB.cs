using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>ONE installed aura projector's field, snapshotted from its <see cref="AuraAtb"/> when the component
    /// is installed (the same "snapshot the atb dials onto a host roster" shape <c>CommandBerth</c> uses). Carrying a
    /// snapshot — not a live atb reference — survives save/load; the grave rung is the uninstall hook removing this
    /// entry by <see cref="ComponentName"/>.</summary>
    public class AuraProjectorField
    {
        [JsonProperty] public double Radius_m { get; internal set; }
        [JsonProperty] public double Magnitude { get; internal set; }
        [JsonProperty] public AuraEffect Effect { get; internal set; }
        [JsonProperty] public AuraTarget Target { get; internal set; }
        /// <summary>The installing component's name — the save/load-robust key the uninstall hook matches on
        /// (a held atb reference would not survive load; a name does — the <c>CommandBerth.ComponentName</c> rule).</summary>
        [JsonProperty] public string ComponentName { get; internal set; } = "";

        public AuraProjectorField() { }

        public AuraProjectorField(AuraProjectorField o)
        {
            Radius_m = o.Radius_m;
            Magnitude = o.Magnitude;
            Effect = o.Effect;
            Target = o.Target;
            ComponentName = o.ComponentName;
        }

        public AuraProjectorField Clone() => new AuraProjectorField(this);
    }

    /// <summary>
    /// ROSTER DB — a ship that mounts one or more aura projectors carries this, listing each projector's field
    /// (radius/magnitude/effect/target). It is the per-ship record the COMBAT RESOLVER reads: an aura is a
    /// FLEET-WIDE command buff (the developer's call, 2026-08-18 — "flagship/fleet-wide command buff which also
    /// applies to planetary combat"), so <see cref="CombatEngagement.FleetAuraMult"/> scans a fleet's ships for this
    /// roster and folds the strongest projector's magnitude into the fleet-wide firepower/toughness multiplier —
    /// NOT a per-ship radius sweep. A destroyed projector is simply not found on the next combat-collect (the grave
    /// rung, for free); a torn-down last projector drops the roster via
    /// <see cref="AuraAtb.OnComponentUninstallation"/>. Mirrors <c>CommandBerthDB</c> exactly (a host roster of
    /// snapshotted component records). <c>[JsonProperty]</c> + deep-copy <c>Clone()</c> (gotcha L12 — a DB that
    /// forgets Clone silently becomes a bare object when its entity moves managers).
    ///
    /// <para>The <c>Radius_m</c> dial on each field is LATENT under the fleet-wide v1 (a fleet is co-located in the
    /// auto-resolve model, so distance does not gate a fleet-wide buff); it is kept for a future per-proximity
    /// refinement and does not affect combat today.</para>
    /// </summary>
    public class AuraProjectorDB : BaseDataBlob
    {
        [JsonProperty] public List<AuraProjectorField> Projectors { get; internal set; } = new List<AuraProjectorField>();

        public AuraProjectorDB() { }

        public AuraProjectorDB(AuraProjectorDB other)
        {
            Projectors = new List<AuraProjectorField>();
            foreach (var p in other.Projectors)
                Projectors.Add(p.Clone());
        }

        public override object Clone() => new AuraProjectorDB(this);
    }
}
