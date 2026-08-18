using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>WHAT an aura does to the units in its field (E14). Rally/Dread act on a unit MORALE/steadiness field
    /// that does not exist yet (so they wait on that field); Command/Ward feed the read-time <c>BonusesDB</c> channel
    /// (Firepower / Toughness); Jamming cuts enemy detection+accuracy (the detection channel).</summary>
    public enum AuraEffect : byte
    {
        Rally = 0,   // friendly morale/steadiness UP     (needs a unit-morale field — pending)
        Dread,       // enemy    morale/steadiness DOWN    (needs a unit-morale field — pending)
        Command,     // friendly firepower/coordination UP (feeds BonusesDB Firepower)
        Jamming,     // enemy    detection/accuracy DOWN   (feeds the detection/accuracy channel)
        Ward         // friendly damage-mitigation UP      (feeds BonusesDB Toughness)
    }

    /// <summary>WHO an aura's field lands on (IFF).</summary>
    public enum AuraTarget : byte
    {
        Friends = 0,
        Foes,
        Everyone
    }

    /// <summary>
    /// An AURA PROJECTOR — a component that projects a command buff/debuff (a commander's rally, a synapse/psionic
    /// ward, a jamming bubble). A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed
    /// / researched / built / mounted / lost like any part — cradle to grave. Mirrors <see cref="ShipMagazineAtb"/>'s
    /// save-safe shape exactly (parameterless ctor + one NCalc double-arg ctor + <c>[JsonProperty]</c> dials + a real
    /// <c>Clone</c>).
    ///
    /// <para><b>Delivery = FLEET-WIDE (the developer's call, 2026-08-18 — "flagship/fleet-wide command buff which also
    /// applies to planetary combat").</b> On install the field is snapshotted onto the host's
    /// <see cref="AuraProjectorDB"/> roster; the combat resolver's <see cref="CombatEngagement.FleetAuraMult"/> scans a
    /// fleet's ships for that roster and folds the STRONGEST projector's magnitude into the fleet-wide
    /// firepower/toughness multiplier (Command→Firepower, Ward→Toughness) — the take-the-best-not-sum guard-rail
    /// (<see cref="AuraTools.BestOf"/>). NOT a per-ship radius sweep (an earlier slice built one; the developer's
    /// fleet-wide call superseded it). A destroyed projector is simply not found on the next combat-collect (the grave
    /// rung, for free); the last projector torn down drops the roster (the uninstall hook). ⚠ Rally/Dread need a
    /// unit-morale field that doesn't exist yet, and Jamming a detection channel — so only Command/Ward are wired.
    /// The battalion-wide GROUND fold is the next slice. Inert on install; never throws.</para>
    /// </summary>
    public class AuraAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The field's reach in metres — units within this distance of the projector are affected.</summary>
        [JsonProperty] public double Radius_m { get; internal set; }
        /// <summary>The field's strength at its centre (the buff/debuff magnitude the sweep applies, tapering by
        /// distance via <see cref="AuraTools.MagnitudeAt"/>).</summary>
        [JsonProperty] public double Magnitude { get; internal set; }
        /// <summary>WHAT the field does.</summary>
        [JsonProperty] public AuraEffect Effect { get; internal set; }
        /// <summary>WHO it lands on.</summary>
        [JsonProperty] public AuraTarget Target { get; internal set; }

        public AuraAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7) — a base-mod aura template feeds AtbConstrArgs(PropertyValue(...)).
        // The enum args arrive as doubles and are mapped back; radius is clamped non-negative.
        public AuraAtb(double radius_m, double magnitude, double effect, double target)
        {
            Radius_m = radius_m < 0 ? 0 : radius_m;
            Magnitude = magnitude;
            Effect = (AuraEffect)(int)effect;
            Target = (AuraTarget)(int)target;
        }

        public override object Clone() => new AuraAtb(Radius_m, Magnitude, (double)(int)Effect, (double)(int)Target);

        /// <summary>Install snapshots this projector's field onto the host's <see cref="AuraProjectorDB"/> roster
        /// (seeding it if absent) so the combat resolver's fleet-wide read (<see cref="CombatEngagement.FleetAuraMult"/>)
        /// finds it — the exact <c>CommandBerthAtb.OnComponentInstallation</c> shape. Never throws (it runs inside ship
        /// construction, L4).</summary>
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (parentEntity == null)
                return;

            var field = new AuraProjectorField
            {
                Radius_m = Radius_m,
                Magnitude = Magnitude,
                Effect = Effect,
                Target = Target,
                ComponentName = componentInstance?.Name ?? "",
            };

            if (parentEntity.TryGetDataBlob<AuraProjectorDB>(out var roster))
            {
                roster.Projectors.Add(field);
            }
            else
            {
                roster = new AuraProjectorDB();
                roster.Projectors.Add(field);
                parentEntity.SetDataBlob(roster);
            }
        }

        /// <summary>Uninstall removes THIS component's field by name (matching survives save/load, unlike a held
        /// reference); the last projector torn down drops the marker so the sweep sleeps for this entity again (the
        /// grave rung — a destroyed projector stops projecting). Mirrors <c>CommandBerthAtb.OnComponentUninstallation</c>.</summary>
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (parentEntity == null || !parentEntity.TryGetDataBlob<AuraProjectorDB>(out var roster))
                return;

            string name = componentInstance?.Name ?? "";
            roster.Projectors.RemoveAll(p => p.ComponentName == name);

            if (roster.Projectors.Count == 0)
                parentEntity.RemoveDataBlob<AuraProjectorDB>();
        }

        public string AtbName() => "Aura Projector";
        public string AtbDescription() => $"Projects a {Effect} field ({Target}) out to {Radius_m:0} m at strength {Magnitude:0.##}.";
    }
}
