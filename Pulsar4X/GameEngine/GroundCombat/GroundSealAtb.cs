using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// SEALED SYSTEMS — the component that lets a ground unit fight on a world its unsealed twin can't survive: an
    /// airless rock (Vacuum) or a poison/corrosive sky (ToxicAtmosphere). This is the "sealed power armour matters" rung
    /// (GroundCombat/CLAUDE.md → PlanetEnvironmentFactory E4): an unsealed garrison standing on the Moon or Venus BLEEDS
    /// every hour on the shared environmental-attrition counter; a unit whose design carries sealing negates that bleed.
    /// It is the last Space-Marine blocker — a chapter can hold a vacuum world; a militia levy cannot.
    ///
    /// It is a COMPONENT (CONVENTIONS §6) mounted in the Entity Assembler onto a ground chassis — NOT a bespoke "sealed
    /// unit" type (GroundCombat/CLAUDE.md LOCKED PRINCIPLE, 2026-07-17): the sealed ROLE EMERGES from the part, so it is
    /// researched / built / mounted / lost like any gear, and shooting the seal off ends the protection (the grave rung).
    /// It is the ground echo of a ship's <c>HazardResistanceAtb</c>, but folded into the unit's design-time
    /// <see cref="GroundUnitDesign.EnvironmentalResistance"/> map (E4, v1) instead of a per-instance install hook.
    ///
    /// <see cref="Sealing"/> is the ONE dial (CONVENTIONS §16 — the benefit AND the cost are apparent): the fraction of
    /// the two surface-support hazards' attrition negated, 0..1. One dial covers BOTH Vacuum and ToxicAtmosphere — the
    /// same suit that holds air in holds poison out (the developer's call: sealing is sealing). INERT on install — the
    /// assembler (<see cref="GroundUnitAssembly.Compute"/>) reads the BEST mounted seal's Sealing and writes it into the
    /// design's <c>EnvironmentalResistance</c> at build time (like it reads the best training cadre), so install/uninstall
    /// are no-ops like every ground part; it is NEVER read in combat. Single double-arg ctor for the JSON/NCalc binder —
    /// the binder is EXACT-ARITY (GroundCombat/CLAUDE.md gotcha 6), so the base-mod template feeds exactly one
    /// AtbConstrArgs value and a trailing dial added later must update the template in lockstep. Never throws (L4).
    /// </summary>
    public class GroundSealAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The fraction of Vacuum + ToxicAtmosphere attrition this seal negates (clamped 0..1; 0 = an open
        /// design that protects nothing, 1 = a fully sealed suit immune to both). Read at assembly, folded into the
        /// design's <see cref="GroundUnitDesign.EnvironmentalResistance"/> map; never read by the combat resolver.</summary>
        [JsonProperty] public double Sealing { get; internal set; }   // 0..1 fraction of surface-support attrition negated

        public GroundSealAtb() { }

        // ONE double arg for the JSON/NCalc binder (gotcha L7 / GroundCombat gotcha 6): the base-mod template feeds a
        // SINGLE AtbConstrArgs(PropertyValue('Sealing')) value, so the ctor takes exactly one, matching arity.
        public GroundSealAtb(double sealing)
        {
            Sealing = sealing < 0 ? 0 : (sealing > 1 ? 1 : sealing);
        }

        public override object Clone() => new GroundSealAtb(Sealing);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Sealed Systems";
        public string AtbDescription()
            => $"A sealed life-support envelope — lets this unit survive a vacuum or a toxic/corrosive atmosphere, negating {Sealing:P0} of that surface-support attrition. The higher the seal, the dearer the unit to field.";
    }
}
