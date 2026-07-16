using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Fleet capability aggregation — Root B of the closing-fight model (docs/FLEET-COMBAT-CLOSING-DESIGN.md). Pure
    /// read-models over data that already exists; NO behaviour change. These are the numbers the closing resolve
    /// (Phase 1+) and the battle readout will read:
    ///   • a fleet moves as one icon → it's bound by its SLOWEST, shortest-legged ship (the floors);
    ///   • it can SHOOT as far as its longest gun that reaches the gap (the firepower-vs-range curve);
    ///   • it can SEE as far as its BEST sensor — sensors run in PARALLEL, so the envelope is the max, never the sum.
    /// </summary>
    public static class FleetCombat
    {
        /// <summary>Every ship under a fleet, recursing sub-fleet components, excluding the sub-fleet nodes themselves.</summary>
        public static List<Entity> Ships(Entity fleet)
        {
            var ships = new List<Entity>();
            Collect(fleet, ships, new HashSet<int>(), 0);
            return ships;
        }

        /// <summary>Bound on the fleet-tree recursion — a cheap backstop beside the seen-set (see <see cref="Collect"/>).</summary>
        private const int MaxFleetTreeDepth = 64;

        // Visit each fleet node AT MOST ONCE. This walk feeds the WHOLE closing model (WarpSpeedFloor / DeltaVFloor /
        // FirepowerAtRange / SensorReach / ShieldCapacity / ShieldRegen) AND FleetAssembly.ArmedShipCount, and it runs
        // INSIDE CombatEngagement.Tick (the StarInfoDB hotloop). A malformed fleet tree — a sub-fleet that is its own
        // ancestor (a CYCLE, producible by a player drag-drop before the FleetOrder.ChangeParent guard) or a sub-fleet
        // reachable by two parents (a DIAMOND) — would otherwise re-walk the same subtree combinatorially and WEDGE the
        // combat hotloop (the frozen-clock "StarInfoDB TRUE WEDGE" the SIM-STALL watchdog caught). The seen-set makes
        // the walk O(nodes), immune to any cycle/diamond; the depth cap is a cheap secondary backstop. This is the same
        // guard CombatEngagement.CollectShips already carries — it was simply missing on THIS walk (2026-07-16 fix).
        private static void Collect(Entity node, List<Entity> into, HashSet<int> seen, int depth)
        {
            if (node == null || !node.IsValid || depth >= MaxFleetTreeDepth) return;
            if (!node.TryGetDataBlob<FleetDB>(out var fleet)) return;
            if (!seen.Add(node.Id)) return;                                 // already walked this fleet node — cycle/diamond-proof
            foreach (var child in fleet.GetChildren())
            {
                if (child == null || !child.IsValid) continue;
                if (child.Id == node.Id) continue;                          // a fleet that lists itself — skip the cycle
                if (child.HasDataBlob<FleetDB>()) Collect(child, into, seen, depth + 1);   // a sub-fleet component → recurse
                else into.Add(child);                                       // a ship (each fleet node is visited once → its ships added once)
            }
        }

        /// <summary>
        /// The fleet's strategic speed FLOOR — the SLOWEST ship sets the pace, because the fleet moves together. Value
        /// is the minimum <see cref="WarpAbilityDB.MaxSpeed"/> across the fleet's ships (in MaxSpeed's own units). A
        /// ship with no warp drive contributes 0 (the group can't warp-travel as one around it) — a true, useful
        /// signal, not a bug. Returns 0 for an empty fleet.
        /// </summary>
        public static double WarpSpeedFloor(Entity fleet)
        {
            double floor = double.PositiveInfinity;
            foreach (var ship in Ships(fleet))
            {
                double s = ship.TryGetDataBlob<WarpAbilityDB>(out var warp) ? warp.MaxSpeed : 0;
                if (s < floor) floor = s;
            }
            return double.IsInfinity(floor) ? 0 : floor;
        }

        /// <summary>
        /// The fleet's Δv FLOOR (m/s) — the shortest-legged ship limits how far the group can maneuver together (and,
        /// in the closing model, how long it can hold/kite a range before its fuel clock runs out). Minimum
        /// <see cref="NewtonThrustAbilityDB.DeltaV"/> across the ships; 0 for a ship with no thruster, 0 for an empty fleet.
        /// </summary>
        public static double DeltaVFloor(Entity fleet)
        {
            double floor = double.PositiveInfinity;
            foreach (var ship in Ships(fleet))
            {
                double dv = ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var nt) ? nt.DeltaV : 0;
                if (dv < floor) floor = dv;
            }
            return double.IsInfinity(floor) ? 0 : floor;
        }

        /// <summary>
        /// The fleet's firepower (J/s) that can REACH a target at the given separation — the firepower-vs-range curve.
        /// At range 0 every weapon counts (full firepower); as the gap grows, weapons whose finite <see cref="WeaponProfile.Range_m"/>
        /// is below it drop out, until only the longest guns reach. `Range_m <= 0` = unbounded (always counts). This is
        /// what the closing resolve sums each step against the current separation. Reads cached `ShipCombatValueDB.Weapons`.
        /// </summary>
        public static double FirepowerAtRange(Entity fleet, double range_m)
        {
            double fp = 0;
            foreach (var ship in Ships(fleet))
            {
                if (!ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) || cv.Weapons == null)
                    continue;
                foreach (var w in cv.Weapons)
                    if (w.Range_m <= 0 || w.Range_m >= range_m)
                        fp += w.DamagePerSecond;
            }
            return fp;
        }

        /// <summary>
        /// The fleet's sensor envelope (m) — how far the BEST sensor reaches. Sensors run in PARALLEL, so the fleet's
        /// reach is the MAX over ships, NEVER the sum (two identical sensors are redundant; diverse ones are
        /// complementary — the layered-coverage point). v1 proxy: each ship's <see cref="SensorTools.SelfDetectionRange_m"/>
        /// ("a ship like me"); a target-specific bubble uses <see cref="SensorTools.DetectionRangeAgainst"/> at combat time.
        /// </summary>
        public static double SensorReach(Entity fleet)
        {
            double reach = 0;
            foreach (var ship in Ships(fleet))
            {
                double r = SensorTools.SelfDetectionRange_m(ship);
                if (r > reach) reach = r;
            }
            return reach;
        }

        /// <summary>
        /// The fleet's total SHIELD pool (joules) — the SUM of every ship's installed shield-generator capacity
        /// (docs/WEAPON-TAXONOMY-DESIGN.md §6). The defensive twin of <see cref="FirepowerAtRange"/>: firepower is
        /// what the fleet dishes out, this is the buffer it soaks before its hulls take hits. Summing (not max) is
        /// correct here — the resolver pools shields per fleet, so this read-model matches how combat actually spends
        /// them. 0 = an unshielded fleet. What the Fleet Combat tab shows as "Shields". Reads cached
        /// <see cref="ShipCombatValueDB.ShieldCapacity_J"/>.
        /// </summary>
        public static double ShieldCapacity(Entity fleet)
        {
            double cap = 0;
            foreach (var ship in Ships(fleet))
                if (ship.TryGetDataBlob<ShipCombatValueDB>(out var cv))
                    cap += cv.ShieldCapacity_J;
            return cap;
        }

        /// <summary>
        /// The fleet's total shield RECHARGE rate (joules/sec) — how fast its pooled shields refill between salvos.
        /// Sum over ships; 0 for an unshielded fleet. Pairs with <see cref="ShieldCapacity"/> for the defensive
        /// readout (a big pool that recharges slowly holds a different fight than a small one that snaps back).
        /// </summary>
        public static double ShieldRegen(Entity fleet)
        {
            double regen = 0;
            foreach (var ship in Ships(fleet))
                if (ship.TryGetDataBlob<ShipCombatValueDB>(out var cv))
                    regen += cv.ShieldRegen_Jps;
            return regen;
        }
    }
}
