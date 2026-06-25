using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The battle trigger's engine logic, separated from the processor (<see cref="BattleTriggerProcessor"/>)
    /// so it can be tested directly.
    ///
    /// Each tick: (1) find pairs of hostile fleets that are in range and not already engaged, and start an
    /// engagement; (2) step each active engagement forward a little (game-time), removing whole-ship casualties
    /// (combatants first); when one fleet is wiped — or the fight stalls — end the engagement, which removes the
    /// combat state from both fleets and so clears the engagement lock.
    ///
    /// v1 stubs (flagged): hostility = "different non-neutral faction" (no diplomacy/relations system exists yet);
    /// detection = mutual (no sensor/IFF gate — sensors are a v2 layer); range = a flat huge distance. Casualties
    /// are removed with the lightweight <c>Entity.Destroy()</c> (sets IsValid=false immediately, so the fleet's
    /// ship list excludes them at once and there is no order re-entrancy); commander death and debris are v2.
    /// </summary>
    public static class CombatEngagement
    {
        /// <summary>v1 stub: fleets within this distance (metres) auto-engage. Real value = weapon range once
        /// sensors/range are built. 1 million km — large enough that two fleets parked at the same body fight.</summary>
        public const double EngagementRange_m = 1_000_000_000.0;

        /// <summary>v1 stub: an engagement that runs this many steps without a decision ends (stalemate backstop).</summary>
        public const int MaxSteps = 5000;

        /// <summary>One trigger pass over a system: start new engagements, then step active ones. Returns the
        /// number of fleets seen. Defensive — built not to throw on normal game state.</summary>
        public static int Tick(EntityManager manager, int deltaSeconds)
        {
            var fleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (fleets.Count == 0) return 0;

            double dt = deltaSeconds > 0 ? deltaSeconds : 1;

            // 1) Start engagements: unengaged hostile fleets within range that both still have ships.
            for (int i = 0; i < fleets.Count; i++)
            {
                var a = fleets[i];
                if (!a.IsValid || a.HasDataBlob<FleetCombatStateDB>()) continue;
                if (GetFleetShips(a).Count == 0) continue;

                for (int j = i + 1; j < fleets.Count; j++)
                {
                    var b = fleets[j];
                    if (!b.IsValid || b.HasDataBlob<FleetCombatStateDB>()) continue;
                    if (!AreHostile(a, b)) continue;
                    if (GetFleetShips(b).Count == 0) continue;
                    if (!InRange(a, b)) continue;

                    StartEngagement(a, b);
                    break; // a is engaged now; stop pairing it this tick
                }
            }

            // 2) Step active engagements once each. The lower-Id fleet drives, so each pair steps exactly once.
            foreach (var fleet in fleets)
            {
                if (!fleet.IsValid || !fleet.TryGetDataBlob<FleetCombatStateDB>(out var state)) continue;

                if (!manager.TryGetEntityById(state.OpponentFleetId, out var opponent)
                    || opponent == null || !opponent.IsValid
                    || !opponent.TryGetDataBlob<FleetCombatStateDB>(out var oppState)
                    || oppState.OpponentFleetId != fleet.Id)
                {
                    // Opponent gone or the pairing is inconsistent — release this fleet from combat.
                    EndEngagement(fleet);
                    continue;
                }

                if (fleet.Id < opponent.Id)
                    StepEngagement(fleet, opponent, dt);
            }

            return fleets.Count;
        }

        /// <summary>Attach combat state to both fleets, each pointing at the other.</summary>
        public static void StartEngagement(Entity fleetA, Entity fleetB)
        {
            fleetA.SetDataBlob(new FleetCombatStateDB(fleetB.Id));
            fleetB.SetDataBlob(new FleetCombatStateDB(fleetA.Id));
        }

        /// <summary>Advance one engagement by dt game-seconds: trade fire, remove casualties, end if decided.</summary>
        public static void StepEngagement(Entity fleetA, Entity fleetB, double dt)
        {
            if (!fleetA.TryGetDataBlob<FleetCombatStateDB>(out var stateA)
                || !fleetB.TryGetDataBlob<FleetCombatStateDB>(out var stateB))
                return;

            var shipsA = GetFleetShips(fleetA);
            var shipsB = GetFleetShips(fleetB);

            // A side with no ships has already lost.
            if (shipsA.Count == 0 || shipsB.Count == 0)
            {
                EndEngagement(fleetA, fleetB);
                return;
            }

            // Doctrine is a read-time multiplier on each fleet's effective strength/toughness (its active
            // FleetDoctrineDB posture; 1.0 if none). docs/COMBAT-DESIGN.md System 4.
            double strA = TotalFirepower(shipsA) * FleetDoctrine.FirepowerMult(fleetA);
            double strB = TotalFirepower(shipsB) * FleetDoctrine.FirepowerMult(fleetB);

            stateA.StepsFought++;
            stateB.StepsFought++;

            // Each side pours strength x dt (joules) into the other's damage pool, then casualties land.
            stateB.DamageTakenPool += strA * dt;
            stateA.DamageTakenPool += strB * dt;

            ApplyCasualties(shipsB, stateB, FleetDoctrine.ToughnessMult(fleetB)); // A shoots B
            ApplyCasualties(shipsA, stateA, FleetDoctrine.ToughnessMult(fleetA)); // B shoots A

            bool frozen = strA <= 0 && strB <= 0;
            bool timedOut = stateA.StepsFought >= MaxSteps;
            if (shipsA.Count == 0 || shipsB.Count == 0 || frozen || timedOut)
                EndEngagement(fleetA, fleetB);
        }

        /// <summary>Remove the combat state from both fleets (ends the lock).</summary>
        public static void EndEngagement(Entity fleetA, Entity fleetB)
        {
            EndEngagement(fleetA);
            EndEngagement(fleetB);
        }

        public static void EndEngagement(Entity fleet)
        {
            if (fleet != null && fleet.IsValid && fleet.HasDataBlob<FleetCombatStateDB>())
                fleet.RemoveDataBlob<FleetCombatStateDB>();
        }

        // --- helpers -------------------------------------------------------

        // Whole-or-destroyed: drain the pool by removing lead ships (combatants first). Kills are real
        // (Entity.Destroy() => IsValid=false immediately), and the ship is dropped from this local list so the
        // survivor count is accurate within the step.
        private static void ApplyCasualties(List<Entity> ships, FleetCombatStateDB state, double toughnessMult)
        {
            ships.Sort((x, y) => CombatValue(y).RoleWeight.CompareTo(CombatValue(x).RoleWeight));
            while (ships.Count > 0 && state.DamageTakenPool >= CombatValue(ships[0]).Toughness * toughnessMult)
            {
                state.DamageTakenPool -= CombatValue(ships[0]).Toughness * toughnessMult;
                ships[0].Destroy();
                ships.RemoveAt(0);
            }
        }

        private static double TotalFirepower(List<Entity> ships)
        {
            double sum = 0;
            foreach (var ship in ships) sum += CombatValue(ship).Firepower;
            return sum;
        }

        private static ShipCombatValueDB CombatValue(Entity ship)
        {
            return ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) ? cv : ShipCombatValueDB.Calculate(ship);
        }

        /// <summary>All live ships in a fleet, recursing into sub-fleets (fleet components).</summary>
        public static List<Entity> GetFleetShips(Entity fleet)
        {
            var result = new List<Entity>();
            CollectShips(fleet, result);
            return result;
        }

        private static void CollectShips(Entity fleet, List<Entity> into)
        {
            if (fleet == null || !fleet.IsValid || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid) continue;
                if (child.HasDataBlob<ShipInfoDB>())
                    into.Add(child);
                else if (child.HasDataBlob<FleetDB>())
                    CollectShips(child, into); // sub-fleet (fleet component)
            }
        }

        private static bool AreHostile(Entity a, Entity b)
        {
            int fa = a.FactionOwnerID, fb = b.FactionOwnerID;
            return fa != fb && fa != Game.NeutralFactionId && fb != Game.NeutralFactionId;
        }

        private static bool InRange(Entity a, Entity b)
        {
            // v1: a real weapon-range gate is a v2 layer (EngagementRange_m is the placeholder). Tick already runs
            // per star system, so "same system" is the working v1 stub. We still apply a distance gate WHEN both
            // positions are available and finite, but DEFAULT TO IN-RANGE otherwise — a freshly-spawned fleet's
            // position isn't finite until the orbit processor runs, and a fragile position read must never stop a
            // battle from triggering.
            if (TryGetFleetPosition(a, out var pa) && TryGetFleetPosition(b, out var pb))
            {
                double dist = (pa - pb).Length();
                if (!double.IsNaN(dist) && !double.IsInfinity(dist) && dist > EngagementRange_m)
                    return false;
            }
            return true;
        }

        private static bool TryGetFleetPosition(Entity fleet, out Vector3 position)
        {
            position = default;
            foreach (var ship in GetFleetShips(fleet))
            {
                if (ship.TryGetDataBlob<PositionDB>(out var posDB))
                {
                    position = posDB.AbsolutePosition;
                    return true;
                }
            }
            return false;
        }
    }
}
