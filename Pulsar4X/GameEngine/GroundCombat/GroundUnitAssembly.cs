using System;
using System.Collections.Generic;
using Pulsar4X.Components;

namespace Pulsar4X.GroundCombat
{
    /// <summary>The computed result of assembling a ground unit from a frame + parts — the emergent stats plus whether
    /// the assembly is legal (fits the frame's carry budget). The ground echo of a ship's derived stats.</summary>
    public class GroundUnitAssemblyResult
    {
        public double Attack;
        public int Range;
        public double Defense;
        public double HitPoints;
        public double Evasion;
        public double Shield;
        public double Mass;            // total build mass (frame + parts) — feeds cost + transport carry-size
        public double CarryCapacity;   // frame strength + augment strength bonuses
        public double UsedCapacity;    // sum of mounted-part carry mass
        public double MaxItemWeight;   // heaviest single part the frame can bear
        public GroundCarryClass CarryClass = GroundCarryClass.Personnel;
        public bool Valid;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// THE ASSEMBLER — turns a frame + a list of parts into a ground unit's stats, the same way <c>ShipDesign.Recalculate</c>
    /// turns components into a ship: **everything emerges from the sum of the parts.** Attack = Σ weapons, reach = the
    /// longest weapon, HP = frame + Σ armour, defence/evasion/shield = Σ parts, mass + cost = Σ parts.
    ///
    /// The ONE rule ships don't have — the **carry gate** (the developer's "a human can't shoulder a 1000-lb autocannon,
    /// but a power-armoured one can" rule, and a Titan can carry a laspistol — silly but legal):
    ///   • total: Σ part carry-mass ≤ carry-capacity = frame <see cref="GroundChassisAtb.BaseStrength"/> + Σ augment
    ///     <see cref="GroundAugmentAtb.StrengthBonus"/>. **Augments raise the budget** — that's the whole unlock.
    ///   • per-item: each part's carry-mass ≤ <see cref="MaxItemFraction"/> of the capacity — so a single absurdly
    ///     heavy item is refused even when the total would fit.
    /// Pure + defensive (a bad/empty assembly just comes back Invalid with reasons, never throws). This is what the
    /// design UI (G-D4) shows live and what a built unit's raised stats come from. Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md.
    /// </summary>
    public static class GroundUnitAssembly
    {
        /// <summary>Heaviest single part = this fraction of the carry-capacity. NUMBER TO REVIEW (flagged): 0.5.</summary>
        public const double MaxItemFraction = 0.5;

        /// <summary>Compute a unit's emergent stats + carry-gate validity from a <paramref name="frame"/> (must carry a
        /// <see cref="GroundChassisAtb"/>) and its mounted <paramref name="parts"/> (weapons / armour / augments, with a
        /// count each — NOT the frame). Never throws.</summary>
        public static GroundUnitAssemblyResult Compute(ComponentDesign frame, IEnumerable<(ComponentDesign design, int count)> parts)
        {
            var r = new GroundUnitAssemblyResult();
            if (frame == null || !frame.HasAttribute<GroundChassisAtb>())
            {
                r.Problems.Add("no chassis — a unit needs exactly one frame");
                return r;
            }
            var chassis = frame.GetAttribute<GroundChassisAtb>();
            r.CarryClass = chassis.CarryClass;
            r.HitPoints = chassis.BaseHP;
            r.Mass = frame.MassPerUnit;
            double capacity = chassis.BaseStrength;

            var list = new List<(ComponentDesign design, int count)>();
            if (parts != null) foreach (var p in parts) if (p.design != null && p.count > 0) list.Add(p);

            // pass 1 — augments raise the carry budget (power armour is what lets a frame lug heavier gear)
            foreach (var (d, c) in list)
                if (d.HasAttribute<GroundAugmentAtb>())
                    capacity += d.GetAttribute<GroundAugmentAtb>().StrengthBonus * c;
            r.CarryCapacity = capacity;
            r.MaxItemWeight = capacity * MaxItemFraction;

            // pass 2 — sum stats + carry mass, check the per-item limit
            double used = 0;
            double toughness = 0;   // accumulated, applied to the final HP pool below (order-independent)
            foreach (var (d, c) in list)
            {
                double itemMass = 0;
                if (d.HasAttribute<GroundWeaponAtb>())
                {
                    var w = d.GetAttribute<GroundWeaponAtb>();
                    itemMass = w.Mass;
                    r.Attack += w.Attack * c;
                    if (w.Range > r.Range) r.Range = w.Range;   // reach = the longest weapon
                }
                if (d.HasAttribute<GroundArmorAtb>())
                {
                    var a = d.GetAttribute<GroundArmorAtb>();
                    itemMass = a.Mass;
                    r.HitPoints += a.HP * c;
                    r.Defense += a.Defense * c;
                }
                if (d.HasAttribute<GroundAugmentAtb>())
                {
                    var g = d.GetAttribute<GroundAugmentAtb>();
                    itemMass = g.Mass;
                    r.Evasion += g.EvasionBonus * c;
                    r.Shield += g.Shield * c;
                    toughness += g.ToughnessBonus * c;
                }
                used += itemMass * c;
                r.Mass += d.MassPerUnit * c;
                if (itemMass > r.MaxItemWeight)
                    r.Problems.Add($"{d.Name} (carry-mass {itemMass:0}) is too heavy for this frame — max single item is {r.MaxItemWeight:0}. Add an augment (e.g. power armour) or use a bigger frame.");
            }
            // toughness hardens the whole HP pool (frame + armour), applied once so it's order-independent
            if (toughness != 0) r.HitPoints *= 1 + toughness;
            r.UsedCapacity = used;
            if (used > capacity)
                r.Problems.Add($"over carry capacity: mounted {used:0} > budget {capacity:0}. Drop gear, add a strength augment, or use a bigger frame.");

            r.Valid = r.Problems.Count == 0;
            return r;
        }
    }
}
