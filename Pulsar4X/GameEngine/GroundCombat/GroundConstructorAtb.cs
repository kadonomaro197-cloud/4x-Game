using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A GROUND FIELD CONSTRUCTOR — the ability that turns an ordinary ground chassis into a COMBAT ENGINEER: a unit
    /// that can ASSEMBLE a footprint building (a bunker, a radar mast, a forward HQ) ON SITE out of crated parts hauled
    /// down to the surface, on ground it holds, with NO colony present. This is the beachhead enabler — the missing rung
    /// the A5 ground-campaign ledger flagged ("build a structure with no colony — MISSING; ground constructor component /
    /// combat engineer — MISSING"): today all three build paths are colony- or ship-bound, so an invader that has cleared
    /// a region has nothing to build a base with.
    ///
    /// It is a COMPONENT (CONVENTIONS §6) mounted in the Entity Assembler onto a ground chassis — NOT a bespoke "engineer
    /// unit" type (GroundCombat/CLAUDE.md LOCKED PRINCIPLE, 2026-07-17): the combat-engineer ROLE EMERGES from the part,
    /// so it is researched / built / mounted / lost like any gear, and shooting the constructor off the unit ends its
    /// ability (the grave rung). It is the ground echo of the ship <see cref="Pulsar4X.Construction.ConstructorAtb"/>
    /// (which assembles a space STATION from carried modules); this assembles a SURFACE building from surface-landed
    /// parts (<see cref="GroundParts"/>).
    ///
    /// <see cref="BuildRate"/> is the meaningful dial (CONVENTIONS §16 — the benefit AND the cost are apparent): the
    /// build-points a combat engineer lays down per day, so a bigger constructor (dearer + heavier to carry) raises a
    /// footprint building faster. INERT on install — the on-site-build order (a later G1 slice) reads it off the unit at
    /// build time (via <c>TryGetComponentsByAttribute&lt;GroundConstructorAtb&gt;</c>), so install/uninstall are no-ops
    /// like every ground part; it is NEVER read in combat. Single double-arg ctor for the JSON/NCalc binder — the binder
    /// is EXACT-ARITY (GroundCombat/CLAUDE.md gotcha 6), so the base-mod template feeds exactly one AtbConstrArgs value
    /// and a trailing dial added later must update the template in lockstep. Never throws (L4).
    /// </summary>
    public class GroundConstructorAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Build-points the combat engineer lays down per DAY when assembling a footprint building on site
        /// (clamped to 0 or more; 0 = a crippled design that builds nothing). Read by the on-site-build order at build
        /// time; never read by the combat resolver.</summary>
        [JsonProperty] public double BuildRate { get; internal set; }   // build-points/day

        public GroundConstructorAtb() { }

        // ONE double arg for the JSON/NCalc binder (gotcha L7 / GroundCombat gotcha 6): the base-mod template feeds a
        // SINGLE AtbConstrArgs(PropertyValue('BuildRate')) value, so the ctor takes exactly one, matching arity.
        public GroundConstructorAtb(double buildRate)
        {
            BuildRate = buildRate < 0 ? 0 : buildRate;
        }

        public override object Clone() => new GroundConstructorAtb(BuildRate);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Field Constructor";
        public string AtbDescription()
            => $"A combat-engineer field constructor — lets this unit assemble a footprint building on ground it holds, from parts hauled to the surface, at {BuildRate:0} build-points/day.";
    }
}
