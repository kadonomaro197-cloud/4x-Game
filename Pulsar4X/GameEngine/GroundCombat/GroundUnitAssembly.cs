using System;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Interfaces;

namespace Pulsar4X.GroundCombat
{
    /// <summary>The computed result of assembling a ground unit from a frame + parts — the emergent stats plus whether
    /// the assembly is legal (fits the frame's carry budget). The ground echo of a ship's derived stats.</summary>
    public class GroundUnitAssemblyResult
    {
        public double Attack;
        public int Range;
        public double Defense;
        // ⚙3 Defense — armour NATURE tuning: the Defense-weighted combination of the mounted plating's per-nature soak
        // effectiveness. 1.0 = plain plate (no armour, or all plain plate) → byte-identical.
        public double ArmourVsKinetic = 1.0;
        public double ArmourVsEnergy = 1.0;
        public double ArmourVsExplosive = 1.0;
        public double ArmourVsExotic = 1.0;
        public double HitPoints;
        public double Evasion;
        public double Shield;
        // ⚙3 Defense — shield RECHARGE: the Shield-weighted combination of the mounted augments' recharge dials. Default
        // 0.34 (the old global constant; no shield → stays 0.34, moot) → byte-identical.
        public double ShieldRegenFraction = 0.34;
        public GroundWeaponMode DamageType = GroundWeaponMode.Ballistic;   // the heaviest weapon's flavour (System ①)
        public double Mass;            // total build mass (frame + parts) — feeds cost + transport carry-size
        public double CarryCapacity;   // frame strength + augment strength bonuses
        public double UsedCapacity;    // sum of mounted-part carry mass
        public double MaxItemWeight;   // heaviest single part the frame can bear
        public double EnergyDemand_W;  // Σ power draw of the mounted energy weapons (P2 supply gate)
        public double ReactorSupply_W; // Σ sustained output of the mounted reactors (P2 supply gate)
        public double AmmoCapacity_kg; // Σ magazine capacity (kg) — the ammo store (P2c ammo gate)
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

        /// <summary>A ground weapon's effective carry-mass is floored at Attack × this — so firepower ALWAYS costs
        /// carry-weight and can't be dialed up for free. Set to the tightest stock CarryMass/Attack ratio (the claw's
        /// 2/20 = 0.1) so every base-mod weapon sits at or below its dialed CarryMass → byte-identical; only a
        /// heavier-hitting-than-stock design is floored up. NUMBER TO REVIEW (flagged): 0.1.</summary>
        public const double AttackCarryFactor = 0.1;

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

            // pass 2 — sum stats + carry mass, check the per-item limit, and the P2 supply gate (power in vs power out)
            double used = 0;
            double toughness = 0;   // accumulated, applied to the final HP pool below (order-independent)
            double topWeaponAttack = 0;   // the heaviest hitter sets the unit's damage flavour
            double energyDemand = 0;   // Σ watts drawn by energy weapons (WeaponSupply)
            double reactorSupply = 0;  // Σ watts supplied by mounted reactors (WeaponSupply)
            double ammoCapacity = 0;   // Σ magazine capacity kg (WeaponSupply) — the ammo store
            bool anyAmmoWeapon = false;// is an ammo-fed weapon mounted? (needs a magazine)
            // ⚙3 armour nature: Defense-weighted sums of each plating part's per-nature soak, averaged after the loop.
            double armWeight = 0, armVsK = 0, armVsE = 0, armVsX = 0, armVsO = 0;
            // ⚙3 shield recharge: Shield-weighted sum of each augment's recharge dial, averaged after the loop.
            double shieldWeight = 0, shieldRegenSum = 0;
            foreach (var (d, c) in list)
            {
                double itemMass = 0;
                if (d.HasAttribute<GroundWeaponAtb>())
                {
                    var w = d.GetAttribute<GroundWeaponAtb>();
                    // Attack costs CARRY-WEIGHT — un-bypassable (the "Attack free dial" fix, Option A). The weapon's
                    // effective bulk is the greater of its dialed CarryMass and a floor driven by its Attack, so a
                    // designer can't dial firepower to the ceiling while keeping the weapon feather-light. Anchored so
                    // every stock ground weapon (Attack×AttackCarryFactor ≤ its CarryMass) is byte-identical; only a
                    // heavier-hitting-than-stock design pays. Pairs with the build-cost coupling in the JSON Mass formula.
                    itemMass = Math.Max(w.Mass, w.Attack * AttackCarryFactor);
                    r.Attack += w.Attack * c;
                    if (w.Range > r.Range) r.Range = w.Range;   // reach = the longest weapon
                    if (w.Attack > topWeaponAttack) { topWeaponAttack = w.Attack; r.DamageType = w.Mode; }
                }
                if (d.HasAttribute<GroundArmorAtb>())
                {
                    var a = d.GetAttribute<GroundArmorAtb>();
                    itemMass = a.Mass;
                    r.HitPoints += a.HP * c;
                    r.Defense += a.Defense * c;
                    // ⚙3 nature tuning: weight each plating's per-nature soak by the Defense it contributes, so a mostly-
                    // ablative unit reads as ablative. A plain plate contributes 1.0 → pulls the average toward neutral.
                    double w2 = a.Defense * c;
                    armWeight += w2;
                    armVsK += a.VsKinetic * w2; armVsE += a.VsEnergy * w2; armVsX += a.VsExplosive * w2; armVsO += a.VsExotic * w2;
                }
                if (d.HasAttribute<GroundAugmentAtb>())
                {
                    var g = d.GetAttribute<GroundAugmentAtb>();
                    itemMass = g.Mass;
                    r.Evasion += g.EvasionBonus * c;
                    r.Shield += g.Shield * c;
                    toughness += g.ToughnessBonus * c;
                    // ⚙3 shield recharge: weight each augment's dial by the shield capacity it contributes, so a fast
                    // ward reads fast. An augment with no shield contributes 0 weight → doesn't pollute the average.
                    double sw = g.Shield * c;
                    shieldWeight += sw;
                    shieldRegenSum += g.ShieldRegenFraction * sw;
                }
                // A part that isn't one of the ground-specific kinds (a universal weapon or a reactor, P1/P2a) has no
                // ground carry-mass field — count its real component mass so it still consumes the carry budget. This is
                // what makes the two gates COMPOSE: infantry can't power the big laser because it can't CARRY the reactor.
                bool hasGroundAtb = d.HasAttribute<GroundWeaponAtb>() || d.HasAttribute<GroundArmorAtb>() || d.HasAttribute<GroundAugmentAtb>();
                if (!hasGroundAtb) itemMass = d.MassPerUnit;
                // P2 supply gate — accumulate power drawn (energy weapons) and power supplied (reactors)
                energyDemand += WeaponSupply.PowerDraw_W(d) * c;
                reactorSupply += WeaponSupply.ReactorOutput_W(d) * c;
                // P2c ammo gate — accumulate magazine capacity + note whether any weapon needs feeding
                ammoCapacity += WeaponSupply.MagazineCapacity_kg(d) * c;
                if (WeaponSupply.DrawsAmmo(d)) anyAmmoWeapon = true;
                used += itemMass * c;
                r.Mass += d.MassPerUnit * c;
                if (itemMass > r.MaxItemWeight)
                    r.Problems.Add($"{d.Name} (carry-mass {itemMass:0}) is too heavy for this frame — max single item is {r.MaxItemWeight:0}. Add an augment (e.g. power armour) or use a bigger frame.");
            }
            // toughness hardens the whole HP pool (frame + armour), applied once so it's order-independent
            if (toughness != 0) r.HitPoints *= 1 + toughness;
            // ⚙3 armour nature: finish the Defense-weighted average of the mounted plating (unarmoured / all-plain → the
            // 1.0 defaults stay → byte-identical). This is the unit's "armour against WHAT" profile the resolver reads.
            if (armWeight > 0)
            {
                r.ArmourVsKinetic = armVsK / armWeight;
                r.ArmourVsEnergy = armVsE / armWeight;
                r.ArmourVsExplosive = armVsX / armWeight;
                r.ArmourVsExotic = armVsO / armWeight;
            }
            // ⚙3 shield recharge: finish the Shield-weighted average (no shield → the 0.34 default stays → byte-identical).
            if (shieldWeight > 0)
                r.ShieldRegenFraction = shieldRegenSum / shieldWeight;
            r.UsedCapacity = used;
            r.EnergyDemand_W = energyDemand;
            r.ReactorSupply_W = reactorSupply;
            r.AmmoCapacity_kg = ammoCapacity;
            if (used > capacity)
                r.Problems.Add($"over carry capacity: mounted {used:0} > budget {capacity:0}. Drop gear, add a strength augment, or use a bigger frame.");
            // The SUPPLY gate (P2b, hard — the developer's call): the guns can't draw more power than the reactors make.
            if (energyDemand > reactorSupply)
                r.Problems.Add($"under-powered: energy weapons draw {energyDemand:0} W but reactors supply {reactorSupply:0} W — mount a reactor (or a bigger one), or drop an energy weapon.");
            // The AMMO gate (P2c-a, hard): an ammo-fed weapon (flak, railgun, ...) needs a magazine (mass) to feed it.
            if (anyAmmoWeapon && ammoCapacity <= 0)
                r.Problems.Add("no magazine: an ammo-fed weapon (flak / railgun) needs an ammo magazine to feed it — mount a magazine.");

            r.Valid = r.Problems.Count == 0;
            return r;
        }

        /// <summary>Bridge an assembly to a BUILDABLE unit: turn a <paramref name="frame"/> + <paramref name="parts"/>
        /// into a <see cref="GroundUnitDesign"/> whose combat stats and costs are the assembly's computed totals, so it
        /// rides the existing industry rails and its build raises a <see cref="GroundUnit"/> with the EMERGENT stats.
        /// This is the connect that makes the assembler real — design parts → buildable unit → raised unit. The caller
        /// should check <see cref="Compute"/>'s <c>Valid</c> first (the carry gate); this builds the design regardless
        /// (the gate is a UI/order concern). Never throws.</summary>
        public static GroundUnitDesign ToGroundUnitDesign(string uniqueId, string name, ComponentDesign frame,
            IEnumerable<(ComponentDesign design, int count)> parts)
        {
            var r = Compute(frame, parts);
            var design = new GroundUnitDesign
            {
                UniqueID = uniqueId,
                Name = name,
                UnitType = DeriveType(frame, parts),
                Attack = r.Attack,
                Defense = r.Defense,
                ArmourVsKinetic = r.ArmourVsKinetic,
                ArmourVsEnergy = r.ArmourVsEnergy,
                ArmourVsExplosive = r.ArmourVsExplosive,
                ArmourVsExotic = r.ArmourVsExotic,
                HitPoints = r.HitPoints,
                Range = r.Range,
                Evasion = r.Evasion,
                Shield = r.Shield,
                ShieldRegenFraction = r.ShieldRegenFraction,
                AmmoCapacity_kg = r.AmmoCapacity_kg,
                DamageType = r.DamageType,
                IndustryTypeID = string.IsNullOrEmpty(frame?.IndustryTypeID) ? "installation-construction" : frame.IndustryTypeID,
            };
            // costs = frame + every part (× count) — the same sum the ship designer does
            if (frame != null) { AddCosts(design.ResourceCosts, frame.ResourceCosts); design.IndustryPointCosts += frame.IndustryPointCosts; }
            if (parts != null)
                foreach (var (d, c) in parts)
                {
                    if (d == null || c <= 0) continue;
                    for (int i = 0; i < c; i++) { AddCosts(design.ResourceCosts, d.ResourceCosts); design.IndustryPointCosts += d.IndustryPointCosts; }
                }

            // KEEP the component list (units-as-entities slice 1) — the mounted component-design ids → count, so a
            // raised unit can later be built as an entity carrying these as real ComponentInstances and every ability
            // falls out. Additive: the flattened stats above stay the combat read-model. Frame + each part.
            if (frame != null && !string.IsNullOrEmpty(frame.UniqueID))
                design.ComponentDesignIds[frame.UniqueID] = design.ComponentDesignIds.TryGetValue(frame.UniqueID, out var fc) ? fc + 1 : 1;
            if (parts != null)
                foreach (var (d, c) in parts)
                {
                    if (d == null || c <= 0 || string.IsNullOrEmpty(d.UniqueID)) continue;
                    design.ComponentDesignIds[d.UniqueID] = design.ComponentDesignIds.TryGetValue(d.UniqueID, out var pc) ? pc + c : c;
                }
            return design;
        }

        /// <summary>SLICE A — the CONNECT: assemble a frame + parts into a <see cref="GroundUnitDesign"/> AND register it
        /// on the faction as a buildable design, so it rides the normal industry rails (queue → consume materials →
        /// <see cref="GroundUnitDesign.OnConstructionComplete"/> raises the fielded unit on the colony's planet). This is
        /// the single entry point a unit-designer UI (later) and tests call to make a player-designed unit real. Returns
        /// the registered design. Never throws on the assembly itself; the caller may check <see cref="Compute"/>'s
        /// <c>Valid</c> first (the gates) — registration is a UI/order concern, so this registers regardless.</summary>
        public static GroundUnitDesign RegisterAssembledDesign(FactionInfoDB faction, string uniqueId, string name,
            ComponentDesign frame, IEnumerable<(ComponentDesign design, int count)> parts)
        {
            var design = ToGroundUnitDesign(uniqueId, name, frame, parts);
            if (faction != null)
                faction.IndustryDesigns[design.UniqueID] = (IConstructableDesign)design;
            return design;
        }

        // A vehicle frame → Armor; a personnel frame whose reaching weapon is indirect → Artillery; else Infantry.
        // (v1 mapping so the combat triangle keeps working; a per-frame UnitType hint is a later refinement.)
        private static GroundUnitType DeriveType(ComponentDesign frame, IEnumerable<(ComponentDesign design, int count)> parts)
        {
            if (frame != null && frame.HasAttribute<GroundChassisAtb>()
                && frame.GetAttribute<GroundChassisAtb>().CarryClass == GroundCarryClass.Vehicle)
                return GroundUnitType.Armor;
            if (parts != null)
                foreach (var (d, c) in parts)
                    if (d != null && d.HasAttribute<GroundWeaponAtb>()
                        && d.GetAttribute<GroundWeaponAtb>().Mode == GroundWeaponMode.Artillery)
                        return GroundUnitType.Artillery;
            return GroundUnitType.Infantry;
        }

        private static void AddCosts(Dictionary<string, long> into, Dictionary<string, long> from)
        {
            if (from == null) return;
            foreach (var kv in from)
                into[kv.Key] = (into.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
        }
    }
}
