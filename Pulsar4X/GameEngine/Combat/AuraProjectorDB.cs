using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>ONE installed aura projector's field, snapshotted from its <see cref="AuraAtb"/> when the component
    /// is installed (the same "snapshot the atb dials onto a host roster" shape <c>CommandBerth</c> uses). Carrying a
    /// snapshot — not a live atb reference — is what makes the sweep gaugeable without a full component harness and
    /// what survives save/load; the grave rung is the uninstall hook removing this entry by <see cref="ComponentName"/>.</summary>
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
    /// MARKER DB — an entity that mounts one or more aura projectors carries this, listing each projector's field
    /// (radius/magnitude/effect/target). Its two jobs (E14 auras, slice 2):
    /// <list type="number">
    /// <item>it is the DataBlob type <see cref="AuraSweepProcessor"/> keys to — a fresh marker so it collides with no
    /// other hotloop (gotcha L9; the same reason <c>StarFlareSourceDB</c> is its own marker off <c>StarInfoDB</c>);</item>
    /// <item>its presence wakes the sweep for this entity and its absence lets the sweep sleep (the empty-system
    /// optimisation, gotcha L5) — so <see cref="AuraAtb.OnComponentUninstallation"/> drops it when the last projector
    /// leaves (the grave rung).</item>
    /// </list>
    /// Mirrors <c>CommandBerthDB</c> exactly (a host roster of snapshotted component records). <c>[JsonProperty]</c> +
    /// deep-copy <c>Clone()</c> (gotcha L12 — a DB that forgets Clone silently becomes a bare object when its entity
    /// moves managers).
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
