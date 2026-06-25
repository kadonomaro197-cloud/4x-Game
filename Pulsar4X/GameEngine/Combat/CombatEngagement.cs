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

        /// <summary>v1 stub: a fleet that has lost at least this fraction of its starting ships breaks off
        /// (retreats). Per System 5 the real threshold comes from doctrine — this is a flat default until
        /// per-doctrine thresholds are wired.</summary>
        public const double RetreatCasualtyThreshold = 0.5;

        // --- dodge model tuning (docs/WEAPONS-AND-DODGE-DESIGN.md), all v1 stubs ----------------------------

        /// <summary>Shot velocity (m/s) at which a weapon half-defeats evasion. A light-speed beam is far above
        /// this (≈always hits); a finite-velocity slug is far below (its shots can be dodged).</summary>
        public const double VelocityReference_mps = 1_000_000.0;

        /// <summary>Saturation (tracks/sec) at which a weapon half-guarantees a hit regardless of dodge — high for
        /// flak (fills the sky), low for a single slow slug.</summary>
        public const double SaturationReference = 50.0;

        /// <summary>Floor on the fraction of fire that lands, so even a perfect dodger eventually dies to enough
        /// volume of fire — nothing is truly untouchable.</summary>
        public const double MinLandedFraction = 0.02;

        /// <summary>An old-style combat value with no <c>WeaponProfile</c>s fires as if a light-speed beam that
        /// always lands — so the dodge model degrades exactly to the pre-dodge behaviour. Backward-compat.</summary>
        public const double FallbackBeamVelocity_mps = 100_000_000.0;

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

        /// <summary>Attach combat state to both fleets, each pointing at the other and recording its starting
        /// ship count (the denominator for the casualty-fraction retreat threshold).</summary>
        public static void StartEngagement(Entity fleetA, Entity fleetB)
        {
            fleetA.SetDataBlob(new FleetCombatStateDB(fleetB.Id, GetFleetShips(fleetA).Count));
            fleetB.SetDataBlob(new FleetCombatStateDB(fleetA.Id, GetFleetShips(fleetB).Count));
        }

        /// <summary>Advance one engagement by dt game-seconds: trade fire, remove casualties, end if decided.</summary>
        public static void StepEngagement(Entity fleetA, Entity fleetB, double dt)
        {
            if (!fleetA.TryGetDataBlob<FleetCombatStateDB>(out var stateA)
                || !fleetB.TryGetDataBlob<FleetCombatStateDB>(out var stateB))
                return;

            // Per-component doctrine: each ship carries the firepower/toughness multipliers of the component
            // (sub-fleet) it sits in, so a fleet's Front Line and Rear Guard can fight with different postures.
            // A ship directly in the fleet uses the fleet's own posture (1.0 if none). docs/COMBAT-DESIGN.md System 4.
            var shipsA = GetCombatShips(fleetA);
            var shipsB = GetCombatShips(fleetB);

            // A side with no ships has already lost.
            if (shipsA.Count == 0 || shipsB.Count == 0)
            {
                EndEngagement(fleetA, fleetB);
                return;
            }

            // Each side's outgoing fire as a mix of weapon flavors (doctrine firepower applied). The dodge model
            // reads this to decide WHO in the other fleet gets hit. docs/WEAPONS-AND-DODGE-DESIGN.md.
            var fireA = BuildFireMix(shipsA);
            var fireB = BuildFireMix(shipsB);
            double strA = TotalDamage(fireA);
            double strB = TotalDamage(fireB);

            stateA.StepsFought++;
            stateB.StepsFought++;

            // Each side pours strength x dt (joules) into the other's damage pool, then casualties land — hittable
            // ships first, and an evasive ship's effective toughness is inflated by however much fire it dodges.
            stateB.DamageTakenPool += strA * dt;
            stateA.DamageTakenPool += strB * dt;

            ApplyCasualties(shipsB, stateB, fireA); // A shoots B
            ApplyCasualties(shipsA, stateA, fireB); // B shoots A

            // Retreat (System 5, v1 = math outcome): a side breaks off if it flies a withdraw posture or has lost
            // too large a fraction of its ships. Breaking off records a retreat vector and ends the engagement; it
            // does NOT issue a move order (that's a v2 movement-system layer). A wiped side (count 0) is destroyed,
            // not retreating.
            bool aRetreats = ShouldRetreat(fleetA, stateA, shipsA.Count);
            bool bRetreats = ShouldRetreat(fleetB, stateB, shipsB.Count);
            if (aRetreats) RecordRetreat(fleetA, fleetB);
            if (bRetreats) RecordRetreat(fleetB, fleetA);

            bool frozen = strA <= 0 && strB <= 0;
            bool timedOut = stateA.StepsFought >= MaxSteps;
            if (shipsA.Count == 0 || shipsB.Count == 0 || aRetreats || bRetreats || frozen || timedOut)
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

        /// <summary>A ship in combat, tagged with the firepower/toughness multipliers of the component (sub-fleet)
        /// it belongs to. Per-component doctrine lives here: collect once, and each ship already knows its posture.</summary>
        public readonly struct CombatShip
        {
            public readonly Entity Ship;
            public readonly double FirepowerMult;
            public readonly double ToughnessMult;
            public CombatShip(Entity ship, double firepowerMult, double toughnessMult)
            {
                Ship = ship;
                FirepowerMult = firepowerMult;
                ToughnessMult = toughnessMult;
            }
        }

        // Whole-or-destroyed, DODGE-AWARE. The incoming fire mix decides what fraction lands on each ship — a
        // fighter dodges ballistic slugs, nobody dodges a beam, flak floors it. A ship's EFFECTIVE toughness is
        // its raw toughness (× its doctrine posture) ÷ that landed fraction, so a dodgy ship needs far more fire
        // to kill. Targets fall combatants-first, then most-hittable-first, so the big slow hulls die before the
        // evasive screen. Degrades EXACTLY to the old behaviour with no weapon profiles / no evasion (landed = 1).
        // O(ships × weapons): each ship's landed fraction is computed once, not per comparison.
        private static void ApplyCasualties(List<CombatShip> ships, FleetCombatStateDB state, List<WeaponProfile> incomingFire)
        {
            // Pair each ship with its (computed-once) landed fraction + role weight, then order the kills.
            var ordered = new List<(CombatShip cs, double landed, double roleWeight)>(ships.Count);
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                ordered.Add((cs, LandedFraction(incomingFire, cv.Evasion), cv.RoleWeight));
            }
            ordered.Sort((x, y) =>
            {
                int byRole = y.roleWeight.CompareTo(x.roleWeight);          // combatants before utility
                return byRole != 0 ? byRole : y.landed.CompareTo(x.landed); // then most-hittable first
            });

            foreach (var item in ordered)
            {
                double effToughness = CombatValue(item.cs.Ship).Toughness * item.cs.ToughnessMult / item.landed;
                if (state.DamageTakenPool < effToughness) break;
                state.DamageTakenPool -= effToughness;
                item.cs.Ship.Destroy();
            }

            // Drop the dead (Destroy() flips IsValid synchronously) so the caller's survivor count is accurate.
            ships.RemoveAll(cs => !cs.Ship.IsValid);
        }

        /// <summary>A fleet's outgoing fire, AGGREGATED BY WEAPON CLASS — at most a handful of entries no matter
        /// how many ships fire. This is what keeps the dodge resolve O(ships), not O(ships²), for 100s-of-ship
        /// battles: the per-target landed-fraction then iterates ≤4 classes, not every weapon. Each class entry
        /// carries the class's TOTAL damage (doctrine-scaled) and a damage-weighted velocity/tracking/saturation.
        /// A ship with no weapon profiles but real firepower (old-style combat value) fires as a light-speed
        /// always-hits beam, so dodge degrades to the old behaviour.</summary>
        private static List<WeaponProfile> BuildFireMix(List<CombatShip> ships)
        {
            // class -> (total damage, damage-weighted velocity, tracking, saturation)
            var byClass = new Dictionary<WeaponClass, (double dmg, double velW, double trkW, double satW)>();
            void Add(WeaponClass cls, double dmg, double vel, double trk, double sat)
            {
                if (dmg <= 0) return;
                byClass.TryGetValue(cls, out var e);
                byClass[cls] = (e.dmg + dmg, e.velW + vel * dmg, e.trkW + trk * dmg, e.satW + sat * dmg);
            }

            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                if (cv.Weapons != null && cv.Weapons.Count > 0)
                {
                    foreach (var w in cv.Weapons)
                        Add(w.Class, w.DamagePerSecond * cs.FirepowerMult, w.Velocity, w.Tracking, w.Saturation);
                }
                else if (cv.Firepower > 0)
                {
                    Add(WeaponClass.Beam, cv.Firepower * cs.FirepowerMult, FallbackBeamVelocity_mps, 1.0, double.PositiveInfinity);
                }
            }

            var mix = new List<WeaponProfile>(byClass.Count);
            foreach (var kv in byClass)
            {
                double d = kv.Value.dmg;
                mix.Add(new WeaponProfile(kv.Key, d, kv.Value.velW / d, kv.Value.trkW / d, kv.Value.satW / d));
            }
            return mix;
        }

        private static double TotalDamage(List<WeaponProfile> fire)
        {
            double sum = 0;
            foreach (var w in fire) sum += w.DamagePerSecond;
            return sum;
        }

        /// <summary>The damage-weighted fraction of an incoming fire mix that LANDS on a ship with the given
        /// evasion. Beams (≈light-speed) land fully; ballistic slugs are dodged by the evasive; flak floors it.</summary>
        private static double LandedFraction(List<WeaponProfile> fire, double evasion)
        {
            double total = 0, landed = 0;
            foreach (var w in fire)
            {
                total += w.DamagePerSecond;
                landed += w.DamagePerSecond * HitFraction(w, evasion);
            }
            return total > 0 ? landed / total : 1.0;
        }

        /// <summary>Fraction of one weapon's shots that land on a target with the given evasion. Fast/guided
        /// weapons defeat evasion (a beam ignores it); high saturation floors the result (flak fills the sky).
        /// Internal so the dodge curve can be unit-tested directly.</summary>
        internal static double HitFraction(WeaponProfile w, double evasion)
        {
            double velocityTerm = w.Velocity / (w.Velocity + VelocityReference_mps);                  // beam → ~1, slug → low
            double trackingEffectiveness = velocityTerm > w.Tracking ? velocityTerm : w.Tracking;     // guided tracks even when slow
            double dodgeChance = evasion * (1.0 - trackingEffectiveness);
            double saturationFloor = double.IsInfinity(w.Saturation) ? 1.0 : w.Saturation / (w.Saturation + SaturationReference);
            if (saturationFloor < MinLandedFraction) saturationFloor = MinLandedFraction;

            double hit = 1.0 - dodgeChance;
            if (hit < saturationFloor) hit = saturationFloor;
            if (hit > 1.0) hit = 1.0;
            return hit;
        }

        private static ShipCombatValueDB CombatValue(Entity ship)
        {
            return ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) ? cv : ShipCombatValueDB.Calculate(ship);
        }

        /// <summary>A fleet retreats if it still has ships AND either flies a withdraw posture (a doctrine with
        /// IsRetreat, e.g. fighting-withdrawal) or has lost at least <see cref="RetreatCasualtyThreshold"/> of its
        /// starting ships.</summary>
        private static bool ShouldRetreat(Entity fleet, FleetCombatStateDB state, int currentShipCount)
        {
            if (currentShipCount <= 0) return false;            // wiped — destroyed, not retreating
            if (FleetDoctrine.IsRetreat(fleet)) return true;    // withdraw posture = a standing retreat order
            if (state.InitialShipCount <= 0) return false;
            int lost = state.InitialShipCount - currentShipCount;
            return lost >= state.InitialShipCount * RetreatCasualtyThreshold;
        }

        /// <summary>Flag a fleet as retreated and record the direction it would withdraw (a unit vector away from
        /// the enemy). v1 records the outcome only — no movement order is issued (that's a v2 layer).</summary>
        private static void RecordRetreat(Entity fleet, Entity enemy)
        {
            Vector3 vector = Vector3.Zero;
            if (TryGetFleetPosition(fleet, out var myPos) && TryGetFleetPosition(enemy, out var enemyPos))
            {
                var diff = myPos - enemyPos; // from the enemy toward us = the way we'd run
                double len = diff.Length();
                if (len > 1e-6 && !double.IsNaN(len) && !double.IsInfinity(len))
                    vector = diff / len;
            }
            fleet.SetDataBlob(new FleetRetreatDB(vector, enemy.Id));
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

        /// <summary>All live ships in a fleet, each tagged with the doctrine multipliers of the component
        /// (sub-fleet) it sits in — so per-component doctrine is applied where each ship actually fights. A ship
        /// directly in the fleet uses the fleet's own posture; a ship in a sub-fleet uses the sub-fleet's
        /// (component overrides fleet — there is no multiplicative inheritance in v1).</summary>
        public static List<CombatShip> GetCombatShips(Entity fleet)
        {
            var result = new List<CombatShip>();
            CollectCombatShips(fleet, result);
            return result;
        }

        private static void CollectCombatShips(Entity fleet, List<CombatShip> into)
        {
            if (fleet == null || !fleet.IsValid || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            // This node's posture applies to ships DIRECTLY in it; a sub-fleet (component) applies its OWN.
            double fpMult = FleetDoctrine.FirepowerMult(fleet);
            double toughMult = FleetDoctrine.ToughnessMult(fleet);
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid) continue;
                if (child.HasDataBlob<ShipInfoDB>())
                    into.Add(new CombatShip(child, fpMult, toughMult));
                else if (child.HasDataBlob<FleetDB>())
                    CollectCombatShips(child, into); // sub-component → recurse with its own doctrine
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
