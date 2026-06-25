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
    /// MULTI-PARTY by design: any number of fleets can fight on either side, and a fleet can JOIN a battle in
    /// progress at any moment. Each tick: (1) the engage/join pass — any two hostile fleets in range are both
    /// put "in combat" (a fleet reinforcing a fight simply gains combat state here when it comes into range of
    /// an enemy); (2) the step pass — every in-combat fleet in this star system fights as one engagement, the
    /// fleets partitioned into SIDES by faction. Each fleet takes the combined fire of all fleets hostile to it;
    /// an attacker facing several enemy fleets DIVIDES its fire across them (outnumbering a side doesn't multiply
    /// your guns). Casualties are whole ships, removed combatants-first then most-hittable-first; a fleet that is
    /// wiped, breaks off (retreat), or is left with no enemy drops out, and when fewer than two hostile sides
    /// remain the engagement ends — each fleet's combat state (and so its engagement lock) clears as it leaves.
    /// A two-fleet fight is just the n=2 special case of this (see <see cref="StepEngagement"/>).
    ///
    /// v1 stubs (flagged): a SIDE is a faction — there is no alliance/diplomacy model yet, so "different
    /// non-neutral faction" = hostile and same faction = same side (real alliances are a v2 layer). The whole
    /// star system is one battlefield: every in-combat fleet in the manager is in the same engagement (real
    /// weapon-range CLUSTERING — distinct simultaneous battles in one system — is v2; <see cref="InRange"/> only
    /// gates JOINING). Detection is mutual (no sensor/IFF gate). Casualties are removed with the lightweight
    /// <c>Entity.Destroy()</c> (sets IsValid=false immediately, so the fleet's ship list excludes them at once and
    /// there is no order re-entrancy); commander death and debris are v2.
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

        /// <summary>One trigger pass over a system: engage/join hostile fleets, then step the engagement. Returns
        /// the number of fleets seen. Defensive — built not to throw on normal game state.</summary>
        public static int Tick(EntityManager manager, int deltaSeconds)
        {
            var fleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (fleets.Count == 0) return 0;

            double dt = deltaSeconds > 0 ? deltaSeconds : 1;

            // 1) Engage / JOIN. Any two hostile fleets in range are BOTH put in combat. This is also how a fleet
            //    joins a battle already underway: it comes into range of an enemy and gains combat state here, on
            //    its faction's side — no special "reinforce" path. A fleet with no enemy in range never engages.
            //    O(fleets^2), but fleet counts are small and this is the trigger, not the per-ship resolve.
            for (int i = 0; i < fleets.Count; i++)
            {
                var a = fleets[i];
                if (!a.IsValid || GetFleetShips(a).Count == 0) continue;

                for (int j = i + 1; j < fleets.Count; j++)
                {
                    var b = fleets[j];
                    if (!b.IsValid) continue;
                    if (!AreHostile(a, b)) continue;
                    if (GetFleetShips(b).Count == 0) continue;
                    if (!InRange(a, b)) continue;

                    EnsureInCombat(a, b.Id);
                    EnsureInCombat(b, a.Id);
                }
            }

            // 2) Step the engagement. In v1 a star system IS the battlefield (range ~ whole system), so every
            //    in-combat fleet in this manager fights in ONE multi-party engagement, partitioned into sides by
            //    faction inside the resolver. (Real weapon-range clustering — distinct simultaneous battles in one
            //    system — is a v2 layer.) One group step per tick; the resolver releases fleets as they drop out.
            var members = new List<Entity>();
            foreach (var fleet in fleets)
                if (fleet.IsValid && fleet.HasDataBlob<FleetCombatStateDB>())
                    members.Add(fleet);

            if (members.Count > 0)
                StepEngagementGroup(members, dt);

            return fleets.Count;
        }

        /// <summary>Attach combat state to both fleets, each pointing at the other as its (representative) opponent
        /// and recording its starting ship count (the denominator for the casualty-fraction retreat threshold).
        /// The proven entry point for a controlled two-fleet matchup; multi-party joins go through
        /// <see cref="EnsureInCombat"/> in <see cref="Tick"/>.</summary>
        public static void StartEngagement(Entity fleetA, Entity fleetB)
        {
            fleetA.SetDataBlob(new FleetCombatStateDB(fleetB.Id, GetFleetShips(fleetA).Count));
            fleetB.SetDataBlob(new FleetCombatStateDB(fleetA.Id, GetFleetShips(fleetB).Count));
        }

        /// <summary>Put a fleet "in combat" if it isn't already — the JOIN primitive. Idempotent: a fleet already
        /// engaged keeps its running state (damage pool, steps, initial count) untouched, so a reinforcement
        /// arriving each tick doesn't reset the fight. Records its starting ship count for the retreat threshold
        /// and a representative opponent for readout (the real membership is every hostile fleet in the system).</summary>
        public static void EnsureInCombat(Entity fleet, int representativeOpponentId)
        {
            if (fleet == null || !fleet.IsValid || fleet.HasDataBlob<FleetCombatStateDB>()) return;
            fleet.SetDataBlob(new FleetCombatStateDB(representativeOpponentId, GetFleetShips(fleet).Count));
        }

        /// <summary>Advance a two-fleet engagement by dt game-seconds. This is the n=2 special case of
        /// <see cref="StepEngagementGroup"/> — kept as the proven, directly-tested entry point — so a 1-v-1 fight
        /// and a 10-fleet melee run the exact same resolve code.</summary>
        public static void StepEngagement(Entity fleetA, Entity fleetB, double dt)
        {
            StepEngagementGroup(new List<Entity> { fleetA, fleetB }, dt);
        }

        /// <summary>Advance one MULTI-PARTY engagement by dt game-seconds: every in-combat member fleet trades
        /// fire with the fleets hostile to it, casualties land, and fleets that are wiped / break off / have no
        /// enemy left drop out; when fewer than two hostile sides remain (or the fight is frozen / timed out) the
        /// engagement ends. Reduces exactly to the old two-fleet exchange for n=2. docs/COMBAT-DESIGN.md System 4,
        /// docs/WEAPONS-AND-DODGE-DESIGN.md.</summary>
        public static void StepEngagementGroup(List<Entity> members, double dt)
        {
            // Only valid, in-combat fleets take part. (A caller may hand us a fleet that just lost its state.)
            var live = new List<Entity>();
            foreach (var f in members)
                if (f != null && f.IsValid && f.HasDataBlob<FleetCombatStateDB>())
                    live.Add(f);
            int n = live.Count;
            if (n == 0) return;

            // --- SNAPSHOT phase: compute every fleet's ships, outgoing fire, and enemy set BEFORE any casualties,
            // so the exchange is SIMULTANEOUS — everyone fires from full strength, then everyone takes losses.
            // Per-component doctrine rides on each CombatShip (a sub-fleet fights with its own posture).
            var ships = new List<CombatShip>[n];
            var fire = new List<WeaponProfile>[n];
            var enemies = new List<int>[n]; // indices into `live` of the fleets hostile to live[i] (with ships)
            for (int i = 0; i < n; i++)
            {
                ships[i] = GetCombatShips(live[i]);
                fire[i] = BuildFireMix(ships[i]);
                enemies[i] = new List<int>();
            }
            for (int i = 0; i < n; i++)
                for (int k = i + 1; k < n; k++)
                    if (ships[i].Count > 0 && ships[k].Count > 0 && AreHostile(live[i], live[k]))
                    {
                        enemies[i].Add(k);
                        enemies[k].Add(i); // hostility is symmetric — i and k are each other's enemy
                    }

            // --- DAMAGE phase: each fleet takes the COMBINED fire of all fleets hostile to it. An attacker that
            // faces several enemy fleets DIVIDES its fire across them (conserves firepower — outnumbering a side
            // doesn't multiply your guns). Within a target fleet the dodge/bucket resolve still concentrates on the
            // most-hittable ships. Pools carry between ticks so a weaker attacker still grinds kills over time.
            double totalFire = 0;
            for (int i = 0; i < n; i++)
            {
                if (!live[i].TryGetDataBlob<FleetCombatStateDB>(out var state)) continue;
                state.StepsFought++;
                totalFire += TotalDamage(fire[i]);
                if (enemies[i].Count == 0) continue; // nobody shooting at this fleet this step

                state.OpponentFleetId = live[enemies[i][0]].Id; // keep the readout pointing at a live enemy

                var incoming = new List<WeaponProfile>();
                foreach (int g in enemies[i])
                {
                    int split = enemies[g].Count; // how many targets attacker g divides its fire across
                    if (split <= 0) continue;
                    AddScaledFire(incoming, fire[g], 1.0 / split);
                }
                state.DamageTakenPool += TotalDamage(incoming) * dt;
                ApplyCasualties(ships[i], state, incoming); // prunes the dead from ships[i]
            }

            // --- RESOLUTION phase. Per-fleet: a fleet drops out if it is wiped, breaks off (retreat — System 5,
            // v1 math outcome: record a withdraw vector + end, no move order), or has no enemy here. Then, if the
            // battle is decided (fewer than two mutually-hostile fleets remain in combat) or stalled (frozen / any
            // fleet timed out), release everyone still in.
            bool frozen = totalFire <= 0;
            bool timedOut = false;
            for (int i = 0; i < n; i++)
                if (live[i].IsValid && live[i].TryGetDataBlob<FleetCombatStateDB>(out var st) && st.StepsFought >= MaxSteps)
                    timedOut = true;

            for (int i = 0; i < n; i++)
            {
                var f = live[i];
                if (!f.IsValid || !f.TryGetDataBlob<FleetCombatStateDB>(out var state)) continue;
                int aliveCount = ships[i].Count; // post-casualty
                if (aliveCount == 0) { EndEngagement(f); continue; }       // wiped — destroyed, not retreating
                if (enemies[i].Count == 0) { EndEngagement(f); continue; } // no enemy here — done fighting
                if (ShouldRetreat(f, state, aliveCount))
                {
                    RecordRetreat(f, live[enemies[i][0]]); // break off away from a fleet it was fighting
                    EndEngagement(f);
                }
            }

            // Whole-engagement end: gather who's still in combat with ships, and look for any remaining hostile
            // pair. None left (battle decided), or frozen, or timed out -> release every remaining fleet.
            var stillIn = new List<Entity>();
            for (int i = 0; i < n; i++)
                if (live[i].IsValid && live[i].HasDataBlob<FleetCombatStateDB>() && ships[i].Count > 0)
                    stillIn.Add(live[i]);

            bool anyHostilePair = false;
            for (int i = 0; i < stillIn.Count && !anyHostilePair; i++)
                for (int k = i + 1; k < stillIn.Count; k++)
                    if (AreHostile(stillIn[i], stillIn[k])) { anyHostilePair = true; break; }

            if (frozen || timedOut || !anyHostilePair)
                foreach (var f in stillIn)
                    EndEngagement(f);
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

        // Whole-or-destroyed, DODGE-AWARE and CLASS-BUCKETED. Interchangeable ships — same doctrine posture and
        // same combat value (evasion / toughness / role) — die identically, so they're processed as a COUNTED
        // group: the landed fraction and effective toughness are computed ONCE per bucket, not per ship. That
        // makes the costly part O(buckets), independent of ship count (500 identical fighters cost the same as
        // 5). The bucket key is everything that decides HOW a ship dies, which is ALSO the seam for future
        // "degraded" condition tiers — a damaged ship gets a different combat value => a different bucket, with
        // no new code here (docs/WEAPONS-AND-DODGE-DESIGN.md "aggregate force condition"). Behaviour matches the
        // old per-ship loop: buckets are killed combatants-first then most-hittable-first, and the pool stops at
        // the first bucket it can't finish.
        private static void ApplyCasualties(List<CombatShip> ships, FleetCombatStateDB state, List<WeaponProfile> incomingFire)
        {
            if (ships.Count == 0) return;

            // Bucket by (doctrine toughness mult, evasion, toughness, role) — the fields that set effective
            // toughness + kill order. Same-design undamaged ships share a bucket; degraded ones would split off.
            var buckets = new Dictionary<(double tm, double ev, double tough, double role), CasualtyBucket>();
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                var key = (cs.ToughnessMult, cv.Evasion, cv.Toughness, cv.RoleWeight);
                if (!buckets.TryGetValue(key, out var b))
                {
                    double landed = LandedFraction(incomingFire, cv.Evasion);
                    b = new CasualtyBucket
                    {
                        RoleWeight = cv.RoleWeight,
                        Landed = landed,
                        EffToughness = cv.Toughness * cs.ToughnessMult / landed,
                        Ships = new List<Entity>(),
                    };
                    buckets[key] = b;
                }
                b.Ships.Add(cs.Ship);
            }

            var ordered = new List<CasualtyBucket>(buckets.Values);
            ordered.Sort((x, y) =>
            {
                int byRole = y.RoleWeight.CompareTo(x.RoleWeight);          // combatants before utility
                return byRole != 0 ? byRole : y.Landed.CompareTo(x.Landed); // then most-hittable first
            });

            foreach (var b in ordered)
            {
                // How many WHOLE ships from this bucket the pool can afford (guard div-by-zero + int overflow).
                int kills;
                if (b.EffToughness <= 0)
                    kills = b.Ships.Count;
                else
                {
                    double afford = state.DamageTakenPool / b.EffToughness;
                    kills = afford >= b.Ships.Count ? b.Ships.Count : (int)afford;
                }

                if (kills > 0)
                {
                    state.DamageTakenPool -= kills * b.EffToughness;
                    for (int i = 0; i < kills; i++) b.Ships[i].Destroy(); // reify the count back to real entities
                }
                if (kills < b.Ships.Count) break; // pool ran out inside this bucket -> stop (matches per-ship break)
            }

            // Drop the dead (Destroy() flips IsValid synchronously) so the caller's survivor count is accurate.
            ships.RemoveAll(cs => !cs.Ship.IsValid);
        }

        /// <summary>A group of interchangeable ships in the casualty step — same doctrine posture + same combat
        /// value, so they share one landed fraction + effective toughness and are killed as a count. This is the
        /// unit the "aggregate force condition" / degraded-tier model would extend. See ApplyCasualties.</summary>
        private sealed class CasualtyBucket
        {
            public double RoleWeight;
            public double Landed;
            public double EffToughness;
            public List<Entity> Ships;
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

        /// <summary>Append one fleet's class-aggregated fire to a combined incoming mix, scaling its damage by
        /// <paramref name="scale"/> (an attacker divides its fire among the enemy fleets it faces). Velocity /
        /// tracking / saturation are unchanged — only the amount of that flavor changes. Keeps the incoming list
        /// small (≤ a few classes per attacking fleet), so the per-target landed-fraction stays cheap.</summary>
        private static void AddScaledFire(List<WeaponProfile> into, List<WeaponProfile> fire, double scale)
        {
            if (fire == null || scale <= 0) return;
            foreach (var w in fire)
                into.Add(new WeaponProfile(w.Class, w.DamagePerSecond * scale, w.Velocity, w.Tracking, w.Saturation));
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
