using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// GROUND-UNIT gear on a buildable component — the attribute that makes a <see cref="ComponentDesign"/> a
    /// designable, researchable, buildable GROUND FORCE (infantry / armor / artillery). This is the "shared designer"
    /// model (the developer's call, 2026-07-05): a ground unit rides the SAME component designer ships use — template +
    /// NCalc formulas + tech-gating + cost math + the design window — for FREE, because it's just another component with
    /// an ability attribute (CONVENTIONS §6). The trade knobs (armor↔toughness, firepower↔attack, mobility, range,
    /// gear, size↔cost) live in the JSON template's Properties+Formulas; those formulas compute the args below.
    ///
    /// The ONE thing a unit does differently from an installed component: a unit is a MOBILE FORCE, not a colony
    /// installation. So <see cref="OnComponentInstallation"/> IS the "build → deploy" step — when a built ground-unit
    /// component installs on a colony, it RAISES a <see cref="GroundUnit"/> on that colony's planet from these stats and
    /// then removes the transient component so it doesn't linger as an installation. (Verified safe: <c>Entity.AddComponent</c>
    /// fires this hook while iterating the DESIGN's attributes, so removing the instance from the colony's component
    /// store here doesn't disturb that loop, and the following <c>ReCalcAbilities</c> just sees it gone.) Defensive:
    /// never throws in the install/industry path (landmine L4). Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public class GroundUnitAtb : BaseDataBlob, IComponentDesignAttribute
    {
        [JsonProperty] public GroundUnitType UnitType { get; internal set; } = GroundUnitType.Infantry;
        [JsonProperty] public double Attack { get; internal set; }
        [JsonProperty] public double Defense { get; internal set; }
        [JsonProperty] public double HitPoints { get; internal set; }
        /// <summary>Strike RANGE in hexes (H3). 0 → a per-type default at raise (<see cref="GroundRangeTools.DefaultRangeFor"/>).</summary>
        [JsonProperty] public int Range { get; internal set; }
        /// <summary>ARMOUR PENETRATION (Weapons pilot W1c) — how much of an enemy's flat armour (Defense) this unit's
        /// weapon IGNORES before the per-source soak. 0 = a normal round; a high value is an AP/sabot cracker (a tank's
        /// main gun). Flows to <see cref="GroundUnit.Penetration"/> at raise and bites in the shared armour soak.</summary>
        [JsonProperty] public double Penetration { get; internal set; }

        public GroundUnitAtb() { }

        // double args mirror the other ground atbs — the JSON binder feeds AtbConstrArgs(PropertyValue(...)) values as
        // doubles (NCalc), so the ctor must accept doubles for the base-mod component to bind (gotcha L7). Arg ORDER is
        // the template's PropertyFormula order: (unitType, attack, defense, hitPoints, range, penetration). Penetration
        // is LAST so it's an additive arg — a template that omits it (older mod data) simply doesn't pass it, but the
        // base-mod templates all supply it (0 for infantry, an AP value for armor).
        public GroundUnitAtb(double unitType, double attack, double defense, double hitPoints, double range, double penetration = 0)
        {
            UnitType = (GroundUnitType)(int)unitType;
            Attack = attack;
            Defense = defense;
            HitPoints = hitPoints;
            Range = range < 0 ? 0 : (int)range;
            Penetration = penetration < 0 ? 0 : penetration;
        }

        public override object Clone()
            => new GroundUnitAtb((double)(int)UnitType, Attack, Defense, HitPoints, Range, Penetration);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            try
            {
                if (parentEntity == null || !parentEntity.TryGetDataBlob<ColonyInfoDB>(out var colony)) return;
                var body = colony.PlanetEntity;
                if (body == null || body == Entity.InvalidEntity || !body.IsValid) return;

                int region = 0;   // v1: muster into the capital region (a chosen muster point is a refinement)
                if (body.TryGetDataBlob<PlanetRegionsDB>(out var regions) && regions.Regions.Count > 0)
                    region = 0;

                // A unit is deployed, not installed — build a transient GroundUnitDesign from these stats and raise it,
                // reusing the proven RaiseUnit(design) snapshot path. The unit remembers which design built it.
                var design = new GroundUnitDesign
                {
                    UniqueID = componentInstance.Design?.UniqueID ?? "ground-unit",
                    Name = componentInstance.Design?.Name ?? "Ground Unit",
                    UnitType = UnitType,
                    Attack = Attack,
                    Defense = Defense,
                    HitPoints = HitPoints,
                    Range = Range,
                    Penetration = Penetration,
                };
                GroundForces.RaiseUnit(body, design, parentEntity.FactionOwnerID, region, design.Name);

                // Don't let the ground-unit component persist as a colony installation — it became a force on the ground.
                if (parentEntity.TryGetDataBlob<ComponentInstancesDB>(out var comps))
                    comps.RemoveComponentInstance(componentInstance);
            }
            catch { /* never throw in the install/industry path (L4) — a bad raise is skipped */ }
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Unit";
        public string AtbDescription()
            => $"A buildable ground force ({UnitType}) — attack {Attack:0}, defense {Defense:0}, HP {HitPoints:0}, strike range {Range} hex" +
               (Penetration > 0 ? $", armour-pen {Penetration:0}" : "") + ". Building it raises a unit on the planet.";
    }
}
