using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The ground layer's one hotloop — it does everything that happens ON the surface each tick: units MARCH
    /// (5b), garrisons FIGHT (5c), and cleared regions are CAPTURED (5d). It is a SINGLE processor on purpose:
    /// only one hotloop may be keyed to a DataBlob type (landmine L9), so movement + combat + capture all live
    /// here rather than as three processors on <see cref="GroundForcesDB"/>.
    ///
    /// Keyed on <see cref="GroundForcesDB"/> (its own blob — no other processor owns it), so it processes every
    /// planet body that has ground forces and sleeps on those that don't. Defensive to the core: a throw in a
    /// hotloop crashes the whole game loop (landmine L4), so <see cref="ProcessEntity"/> swallows and moves on.
    ///
    /// Combat model (mirrors the space <c>AutoResolve</c> salvo loop, but over <see cref="GroundUnit"/> data
    /// objects, not entities): each tick is ONE salvo — every faction in a contested region takes the COMBINED
    /// attack of all other factions there, focus-fired across its units; units at 0 health are removed. Simple,
    /// deterministic (no RNG), and cheap. Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slices 5b–5d).
    /// </summary>
    public class GroundForcesProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromHours(1);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromHours(1);
        public Type GetParameterType { get; } = typeof(GroundForcesDB);

        /// <summary>Fraction of a faction's attack that lands per salvo — the ground combat-pace dial (mirrors the
        /// space <c>SalvoDamageScale</c>). 1.0 = full; lower stretches a battle over more ticks. Tune live.</summary>
        public const double SalvoScale = 1.0;

        /// <summary>Ground COMBAT INTERRUPT flag (4b) — the ground mirror of
        /// <c>Combat.CombatEngagement.InterruptTimeOnNewEngagement</c>. When true, the first tick a NEW planetary
        /// battle forms on a body, the processor calls <c>MasterTimePulse.RequestCombatHalt()</c> so the clock stops
        /// and the player is notified (the client already surfaces <c>CombatInterruptPending</c> as an on-screen
        /// banner — space and ground share it). Default FALSE so headless tests advance deterministically; the client
        /// turns it on at startup next to the space combat flags.</summary>
        public static bool InterruptTimeOnNewBattle = false;

        // Shield pool regeneration is now a PER-UNIT designed rate (GroundUnit.ShieldRegenFraction, ⚙3), defaulting to
        // 0.34/game-hour (≈ full recharge in ~3 hours) for every unit until a ward dials it — see the recharge step in
        // ProcessBody. The old global ShieldRegenPerHourFraction constant was removed (2026-07-11): it was dead (the
        // 0.34 default lives on the field initializers), so a "single anchor" it wasn't. 0 = a knocked-down shield
        // stays down for the fight.

        // FORTIFICATION (5h) is now DESIGN-DRIVEN — a building fortifies its region (and projects to adjacent friendly
        // regions) only if its design carries a GroundDefenseAtb (a Bunker, not a solar panel). The math + the
        // colony→component resolver live in GroundFortification; ResolveRegionCombat applies it to the defender.

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            try { ProcessBody(entity, deltaSeconds); }
            catch { /* a hotloop must never throw (L4) — a bad body is skipped, the sim keeps running */ }
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var bodies = manager.GetAllEntitiesWithDataBlob<GroundForcesDB>();
            foreach (var body in bodies)
                ProcessEntity(body, deltaSeconds);
            return bodies.Count;
        }

        private static void ProcessBody(Entity body, int deltaSeconds)
        {
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units.Count == 0)
                return;

            // 0) RADAR: a unit carrying a GroundSensorAtb reveals the map within its reach (the ability falls out of the
            //    unit's component store — units-as-entities). Real range → hexes → region bands. Idempotent + defensive.
            GroundSensors.RevealFromUnits(body);

            // 1) MOVEMENT: advance in-transit units.
            //    (a) COARSE region march (5b): a whole-region hop, arrives when the region's crossing time has elapsed.
            //    (b) FINE hex march (H2): walk the stored A* path one hex at a time — each step costs the region's
            //        per-hex base × the entered hex's terrain multiplier, so a march through mountains takes longer.
            //        A single tick can clear several cheap hexes (the while-loop carries the leftover time forward).
            //    (c) GLOBAL cylinder march (G3): walk the stored global A* path; band (RegionIndex) updates by column.
            body.TryGetDataBlob<PlanetRegionsDB>(out var gRegions);
            int gCols = gRegions?.SurfaceGrid?.Cols ?? 0;
            int gRc = gRegions?.Regions.Count ?? 0;
            foreach (var unit in forces.Units)
            {
                if (unit.MovingToRegion >= 0)   // (a) coarse region hop takes priority (a unit isn't doing both)
                {
                    unit.TransitSecondsRemaining -= deltaSeconds;
                    if (unit.TransitSecondsRemaining <= 0)
                    {
                        unit.RegionIndex = unit.MovingToRegion;
                        unit.MovingToRegion = -1;
                        unit.TransitSecondsRemaining = 0;
                    }
                    continue;
                }

                if (unit.HexPath != null && unit.HexPath.Count > 0)   // (b) fine hex march
                {
                    unit.HexTransitSecondsRemaining -= deltaSeconds;
                    while (unit.HexPath.Count > 0 && unit.HexTransitSecondsRemaining <= 0)
                    {
                        var step = unit.HexPath[0];
                        unit.HexQ = step.Q;
                        unit.HexR = step.R;
                        unit.HexPath.RemoveAt(0);
                        // Carry the leftover (negative) time into the next step so a fast tick can cross several hexes.
                        unit.HexTransitSecondsRemaining += (unit.HexPath.Count > 0)
                            ? GroundMobility.StepSecondsFor(body, unit, unit.HexStepBaseSeconds, unit.HexPath[0].Terrain)
                            : 0.0;
                    }
                    if (unit.HexPath.Count == 0) { unit.HexPath = null; unit.HexTransitSecondsRemaining = 0; }
                }

                if (unit.GlobalPath != null && unit.GlobalPath.Count > 0)   // (c) GLOBAL cylinder march (G3) — no edge gates
                {
                    unit.GlobalTransitSecondsRemaining -= deltaSeconds;
                    while (unit.GlobalPath.Count > 0 && unit.GlobalTransitSecondsRemaining <= 0)
                    {
                        var step = unit.GlobalPath[0];
                        unit.GlobalQ = step.Q;
                        unit.GlobalR = step.R;
                        if (gCols > 0 && gRc > 0) unit.RegionIndex = PlanetGridFactory.RegionOfColumn(step.Q, gCols, gRc);   // band = region
                        unit.GlobalPath.RemoveAt(0);
                        unit.GlobalTransitSecondsRemaining += (unit.GlobalPath.Count > 0)
                            ? GroundMobility.StepSecondsFor(body, unit, unit.GlobalStepBaseSeconds, unit.GlobalPath[0].Terrain)
                            : 0.0;
                    }
                    if (unit.GlobalPath.Count == 0) { unit.GlobalPath = null; unit.GlobalTransitSecondsRemaining = 0; }
                }
            }

            // 1b) ENVIRONMENTAL ATTRITION (E3): a unit STANDING in a region with a DAMAGING environmental hazard
            //     (fire tornadoes, corrosive superstorm, radiation zone) bleeds health each tick — the ground twin
            //     of a ship taking damage inside a space hazard. Per-hour magnitude, scaled by the tick. The stat
            //     effects (SensorJam / MovementDrag) are carried by the data + generator but applied in a later wire
            //     (ground detection/movement hooks) — this slice lands the attrition.
            if (body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                foreach (var unit in forces.Units)
                {
                    if (unit.MovingToRegion >= 0 || unit.Health <= 0) continue;
                    foreach (var env in envDB.ForRegion(unit.RegionIndex))
                    {
                        if (!IsDamageEffect(env.Effect)) continue;
                        // E4: the unit's environmental GEAR negates a fraction of the hazard's attrition (heat-shielding,
                        // hazmat sealing…) — the ground echo of a ship's HazardResistanceAtb. 1.0 resistance = immune.
                        double resist = unit.ResistanceTo(env.Effect);
                        unit.Health -= env.Magnitude * (deltaSeconds / 3600.0) * (1.0 - resist);
                        if (unit.Health < 0) unit.Health = 0;
                    }
                }
            }

            body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB);

            // 1b2) FORMATION ORDER QUEUE (O1) — run each formation's queued plan one order at a time, in sequence
            //      ("move to London, THEN Paris, THEN dig in"). A move order is kicked off once and popped when the
            //      formation's leader arrives; a hold counts down; a stance/ROE order applies instantly. This runs
            //      BEFORE the ROE maneuver so an explicit queued order takes precedence over auto-kite/close.
            ProcessFormationOrders(body, forces, regionsDB, deltaSeconds);

            // 1c) RULES OF ENGAGEMENT (the commander's maneuver intent) — before the fight, auto-move each ROE
            //     formation's IDLE units one hex toward (Close) or away (Stand-off) from the nearest enemy, so the H3
            //     range advantage is used without micro (the clone auto-kites the zerg). The ground echo of the space
            //     closing model. Only issues to units not already moving; a hex-marching unit still FIGHTS this tick
            //     (below), so a kiting unit fires WHILE it repositions.
            ApplyEngagementManeuvers(forces, regionsDB, body);

            // Group the units that are STANDING FOR BATTLE — alive and not on a strategic REGION hop. A fine HEX march
            // (a battlefield reposition) still fights: a unit fires from its current hex as it moves (H3 range + the
            // ROE kite). Only a whole-region journey (MovingToRegion) takes a unit out of the fight.
            var byRegion = new Dictionary<int, List<GroundUnit>>();
            foreach (var unit in forces.Units)
            {
                if (unit.MovingToRegion >= 0 || unit.Health <= 0) continue;
                if (!byRegion.TryGetValue(unit.RegionIndex, out var list))
                {
                    list = new List<GroundUnit>();
                    byRegion[unit.RegionIndex] = list;
                }
                list.Add(unit);
            }

            // Build the design-driven fortification resolver once (installation id → GroundDefenseAtb, from the body's
            // colonies) — so a Bunker in a region hardens its defender (and shields adjacent friendly regions).
            var allRegions = regionsDB?.Regions;
            var fortResolve = GroundFortification.BuildResolver(body);

            // 2) COMBAT (5c) + 3) REGION CAPTURE (5d), per region.
            bool anyFightThisTick = false;
            foreach (var kv in byRegion)
            {
                var units = kv.Value;

                var factions = new HashSet<int>();
                foreach (var u in units) factions.Add(u.FactionOwnerID);
                if (factions.Count >= 2)
                {
                    Region region = (regionsDB != null && kv.Key >= 0 && kv.Key < regionsDB.Regions.Count)
                        ? regionsDB.Regions[kv.Key] : null;
                    if (ResolveRegionCombat(forces, units, region, allRegions, fortResolve, deltaSeconds)) anyFightThisTick = true;
                }

                // Whoever holds the only live units here now owns the region.
                var holders = new HashSet<int>();
                foreach (var u in units) if (u.Health > 0) holders.Add(u.FactionOwnerID);
                if (holders.Count == 1 && regionsDB != null && kv.Key >= 0 && kv.Key < regionsDB.Regions.Count)
                {
                    int captor = holders.First();
                    var reg = regionsDB.Regions[kv.Key];
                    if (reg.OwnerFactionID != captor)
                    {
                        reg.OwnerFactionID = captor;
                        // War-map layer (W1): taking the region takes the strategic buildings on its hexes —
                        // "capturing the hex captures what's on it" (docs/GROUND-CITY-AND-WARMAP-DESIGN.md).
                        GroundBuildings.CaptureRegionHexContents(regionsDB, kv.Key, captor);
                    }
                }
            }

            // GROUND COMBAT INTERRUPT (4b): the first tick a NEW planetary battle forms (not-fighting → fighting),
            // halt the clock so the player is notified and can watch/steer — the ground mirror of the space
            // combat-pause (CombatEngagement.EnsureInCombat → RequestCombatHalt). The WasInBattle latch means an
            // ONGOING fight doesn't re-halt every tick; it clears when the fighting stops, so a later fresh battle
            // pauses again. Gated by the flag (client-on) so headless tests advance deterministically. Defensive.
            if (anyFightThisTick && !forces.WasInBattle && InterruptTimeOnNewBattle)
                body.Manager?.Game?.TimePulse?.RequestCombatHalt();
            forces.WasInBattle = anyFightThisTick;

            // Remove destroyed units.
            forces.Units.RemoveAll(u => u.Health <= 0);

            // FORMATIONS: a formation whose LEADER just died reassigns leadership to a surviving member (fleet-like —
            // the flagship echo; no combat penalty, per the locked design). An empty formation keeps LeaderUnitId = -1.
            MaintainFormations(forces);

            // 4) WHOLE-PLANET CAPTURE (5d — the "take a planet" moment): if EVERY region is held by a single
            //    faction that isn't the colony's current owner, the planet's colony flips to that faction.
            TryCapturePlanet(body, regionsDB);
        }

        /// <summary>One salvo in a contested region — TERRAIN-, TYPE- and now RANGE-aware (5f/5g/H3). Directed fire: an
        /// attacker only damages enemies within its own strike <see cref="GroundUnit.Range"/> (in hexes), so a
        /// longer-ranged unit hits a shorter-ranged one as it CLOSES without being hit back until the enemy reaches ITS
        /// range — the ground echo of the space first-strike ("a clone trooper has the advantage over a zerg swarm
        /// until they reach him"). Each attacker's output is still scaled by the weapon TRIANGLE (vs the enemies it can
        /// actually reach) and its TERRAIN affinity, and a defender in the region owner's seat divides incoming by
        /// terrain COVER × FORTIFICATION. Simultaneous (all incoming computed from the pre-salvo state, then applied),
        /// deterministic. <b>Co-located units (same hex, distance 0) are always in range of each other, so a stacked
        /// fight is identical to the pre-hex region resolver</b> — the migration adds range without changing a
        /// same-hex battle. (Terrain here is still the region's dominant feature; reading terrain from the DEFENDER's
        /// own hex is the H3b follow-on.) Reads <see cref="GroundTerrain"/> — the ground twin of SpaceHazardTools.</summary>
        /// <summary>Returns TRUE if a real exchange happened (any damage was dealt to a target this salvo) — the signal
        /// the ground combat-interrupt (4b) reads to detect a battle.</summary>
        private static bool ResolveRegionCombat(GroundForcesDB forces, List<GroundUnit> units, Region region,
            List<Region> allRegions, System.Func<int, GroundDefenseAtb> fortResolve, int deltaSeconds)
        {
            var terrain = GroundTerrain.Classify(region);
            int defenderFaction = region != null ? region.OwnerFactionID : -1;
            // Terrain cover × design-driven fortification — the defender's incoming is divided by this (per-region).
            double coverFort = GroundTerrain.CoverDefenseMult(terrain)
                * GroundFortification.DefenseMult(region, allRegions, defenderFaction, fortResolve);

            var byFaction = new Dictionary<int, List<GroundUnit>>();
            foreach (var u in units)
            {
                if (!byFaction.TryGetValue(u.FactionOwnerID, out var l)) { l = new List<GroundUnit>(); byFaction[u.FactionOwnerID] = l; }
                l.Add(u);
            }

            // SHIELD POOL regen (resolver merge 3c): between salvos a unit's shield recharges toward its capacity, so a
            // shield is burst-resistant then brittle (the ship model), not a permanent % discount. Unshielded units
            // (Shield 0) are untouched → byte-identical.
            if (deltaSeconds > 0)
                foreach (var u in units)
                    if (u.Shield > 0 && u.CurrentShield < u.Shield)
                        // ⚙3 Defense: recharge at the UNIT's designed rate (a fast ward vs a slow big shield), not a
                        // global constant. ShieldRegenFraction defaults to 0.34 for every unit until a ward is
                        // fitted → byte-identical with the old global-constant behaviour.
                        u.CurrentShield = System.Math.Min(u.Shield, u.CurrentShield + u.Shield * u.ShieldRegenFraction * (deltaSeconds / 3600.0));

            // Per-TARGET incoming (not per-faction) so range gating lands damage on exactly the units an attacker can
            // reach. Computed entirely from the PRE-salvo state (Health read here, applied below) → simultaneous.
            var incoming = new Dictionary<GroundUnit, double>();

            foreach (var f in byFaction.Keys)
            {
                foreach (var g in byFaction.Keys)
                {
                    if (f == g) continue;
                    bool gIsDefender = (g == defenderFaction);
                    foreach (var u in byFaction[f])
                    {
                        if (u.Health <= 0) continue;

                        // The g-units THIS attacker can actually reach (within its hex Range). Co-located → all of g.
                        List<GroundUnit> reachable = null;
                        foreach (var t in byFaction[g])
                        {
                            if (t.Health <= 0) continue;
                            if (HexDist(u, t) > u.Range) continue;   // out of this attacker's reach → it can't hit t
                            (reachable ??= new List<GroundUnit>()).Add(t);
                        }
                        if (reachable == null) continue;             // nothing in range → this unit fires nothing this salvo

                        // Output = attack × terrain affinity × stance. The Armor▸Infantry▸Artillery TRIANGLE has
                        // DISSOLVED (resolver merge, slice 3b-ii — docs/RESOLVER-MERGE-DESIGN.md §7, developer's call
                        // 2026-07-08): the type edge is no longer a flat ×1.5/×0.67 multiplier — it now emerges from a
                        // unit's raw stats (attack / armour / HP) × weapon-nature vs the target's evasion/shield/armour,
                        // the same way a ship's does. (`GroundTerrain.TriangleMult` is retained as a readout/helper but
                        // no longer scales the fight.)
                        // Terrain affinity = the unit TYPE's innate edge (TerrainAttackMult) × its DESIGNED
                        // locomotion's edge (LocomotionTerrainMult, Propulsion ⚙2 — an all-terrain drive fights better
                        // on constrained ground). RoughHandlingForUnit reads the unit's designed locomotion (0.5 neutral
                        // for a unit with none → ×1.0 → byte-identical). Body = the planet the roster sits on.
                        double roughHandling = GroundMobility.RoughHandlingForUnit(forces.OwningEntity, u);
                        double atk = u.Attack
                            * GroundTerrain.TerrainAttackMult(u.UnitType, terrain)
                            * GroundTerrain.LocomotionTerrainMult(roughHandling, terrain)
                            * GroundFormationDoctrine.AttackMult(forces, u);
                        double pool = atk * SalvoScale;
                        if (gIsDefender && coverFort > 0) pool /= coverFort;

                        // Spread this attacker's pool across its reachable targets, health-weighted (matches the old
                        // resolver's focus-fire: triangle is already in the total via the avg, distribution is by health).
                        double totalH = 0.0;
                        foreach (var t in reachable) totalH += t.Health;
                        if (totalH <= 0) continue;

                        // Route this attacker's fire through the SHARED KERNEL (resolver merge 3c): its ground weapon
                        // becomes a WeaponProfile, and dodge + shield + armour resolve the same way a ship's do — the
                        // ground triangle/dodge/shield now EMERGE from the weapon spec (no bolted-on GroundDamageMatrix
                        // multiplier). The profile is built once per attacker.
                        var profile = GroundCombatant.ToWeaponProfile(u);
                        double shieldSoakFrac = Pulsar4X.Combat.CombatKernel.ShieldSoakFraction(profile.Nature);
                        foreach (var t in reachable)
                        {
                            // DODGE (kernel HitFraction): an aimed slug is dodged ~(1−evasion); an area/beam shot lands
                            // ~fully — the same curve a ship uses.
                            double contribution = pool * (t.Health / totalH)
                                * Pulsar4X.Combat.CombatKernel.HitFraction(profile, t.Evasion);
                            // SHIELD POOL (3c): the target's depleting shield soaks the soakable fraction (by weapon
                            // nature — kinetic fully, energy half-bleeds, exotic bypasses) up to its CURRENT charge,
                            // BEFORE armour. Draining per-source is deterministic (stable iteration → fast-forward==watch).
                            if (t.CurrentShield > 0 && shieldSoakFrac > 0 && contribution > 0)
                            {
                                double absorbed = System.Math.Min(contribution * shieldSoakFrac, t.CurrentShield);
                                t.CurrentShield -= absorbed;
                                contribution -= absorbed;
                            }
                            // ARMOUR: flat-per-source plating bounces what's left (the swarm-vs-alpha identity) — the
                            // shared CombatKernel armour soak (via GroundDamageMatrix's delegator, slice 3a), reduced by
                            // the weapon's PENETRATION (W1b) and split into the weapon's SHOT COUNT (W2b): an AP round
                            // cracks plate a normal one bounces off, and a big-alpha weapon punches while a chip-swarm of
                            // equal total is mostly bounced. PerShotEnergy 0 → shotCount 1 (every unit until the W2c dial)
                            // → the flat single-lump soak, byte-identical to before.
                            int shotCount = Pulsar4X.Combat.CombatKernel.BurstShotCount(profile);
                            // ARMOUR NATURE (⚙3): the target's plating soaks the incoming weapon's NATURE by its tuning —
                            // ablative shrugs off energy, thins vs a slug. natureFactor 1.0 for a plain-plated unit (every
                            // unit until a nature-tuned plating is fitted) → byte-identical to the pre-nature soak.
                            double natureFactor = t.ArmourResistFor(profile.Nature);
                            contribution = GroundDamageMatrix.ArmourSoak(t.Defense, contribution, shotCount, profile.Penetration, natureFactor);
                            incoming.TryGetValue(t, out var acc);
                            incoming[t] = acc + contribution;
                        }
                    }
                }
            }

            // Apply each unit's accumulated incoming, scaled by its formation STANCE's damage-taken mult (defensive
            // soaks more health per point; offensive dies faster — the ground echo of the space per-ship ToughnessMult),
            // capped at its health.
            foreach (var kv in incoming)
            {
                var t = kv.Key;
                if (t.Health <= 0) continue;
                double dtm = GroundFormationDoctrine.DamageTakenMult(forces, t);
                if (dtm <= 0) dtm = 1.0;
                t.Health -= Math.Min(t.Health, kv.Value * dtm);
            }

            return incoming.Count > 0;   // a real exchange happened this salvo (the combat-interrupt signal, 4b)
        }

        /// <summary>Hex distance between two units on their region's patch (0 = same hex). Reused for the range gate.</summary>
        private static int HexDist(GroundUnit a, GroundUnit b)
            => new HexCoordinate(a.HexQ, a.HexR).DistanceTo(new HexCoordinate(b.HexQ, b.HexR));

        /// <summary>
        /// RULES OF ENGAGEMENT maneuver (the ground echo of the space closing model): for each formation whose
        /// <see cref="GroundEngagementStance"/> isn't HoldGround, order each of its IDLE units one hex toward
        /// (CloseToEngage) or away (StandOff) from the nearest enemy in its region — so the H3 range advantage is used
        /// automatically. A unit already moving is left alone (it re-evaluates when it arrives), so a kite is a steady
        /// step-back-and-fire, not thrash. Issues through <see cref="GroundForces.OrderMoveToHex"/> (which enforces
        /// patch bounds + ocean-impassability), so it never places a unit somewhere illegal.
        /// </summary>
        private static void ApplyEngagementManeuvers(GroundForcesDB forces, PlanetRegionsDB regionsDB, Entity body)
        {
            if (regionsDB == null || forces.Formations == null || forces.Formations.Count == 0) return;
            bool anyRoe = false;
            foreach (var f in forces.Formations)
                if (f.Engagement != GroundEngagementStance.HoldGround) { anyRoe = true; break; }
            if (!anyRoe) return;

            foreach (var unit in forces.Units)
            {
                if (unit.Health <= 0 || unit.MovingToRegion >= 0) continue;
                if (unit.HexPath != null && unit.HexPath.Count > 0) continue;   // already repositioning — let it arrive
                if (FormationHasOrders(forces, unit)) continue;                 // an explicit queued plan overrides auto-ROE
                var stance = GroundFormationDoctrine.EngagementOf(forces, unit);
                if (stance == GroundEngagementStance.HoldGround) continue;
                if (unit.RegionIndex < 0 || unit.RegionIndex >= regionsDB.Regions.Count) continue;
                var region = regionsDB.Regions[unit.RegionIndex];
                if (region.Hexes == null || region.Hexes.Count == 0) continue;

                // Nearest enemy in the same region.
                GroundUnit enemy = null; int best = int.MaxValue;
                foreach (var e in forces.Units)
                {
                    if (e.Health <= 0 || e.FactionOwnerID == unit.FactionOwnerID || e.RegionIndex != unit.RegionIndex) continue;
                    int d = HexDist(unit, e);
                    if (d < best) { best = d; enemy = e; }
                }
                if (enemy == null) continue;

                bool moveAway;
                if (stance == GroundEngagementStance.CloseToEngage)
                {
                    if (best <= unit.Range) continue;            // already in my range → hold and fire
                    moveAway = false;                            // close the gap
                }
                else // StandOff — keep the enemy beyond ITS reach
                {
                    if (best <= enemy.Range) moveAway = true;    // enemy can hit me → open the gap (kite)
                    else if (best > unit.Range) moveAway = false;// I can't hit them → close into my range
                    else continue;                               // in my range, out of theirs → hold + fire (the sweet spot)
                }

                var step = PickStepHex(region, unit.HexQ, unit.HexR, enemy.HexQ, enemy.HexR, moveAway);
                if (step != null) GroundForces.OrderMoveToHex(body, unit, step.Value.q, step.Value.r);
            }
        }

        /// <summary>The adjacent in-patch, PASSABLE hex that most decreases (toward) / increases (away) the hex distance
        /// to (<paramref name="toQ"/>,<paramref name="toR"/>). Null if no neighbour improves on standing still.</summary>
        private static (int q, int r)? PickStepHex(Region region, int fromQ, int fromR, int toQ, int toR, bool away)
        {
            var byCoord = new Dictionary<(int, int), GroundHex>();
            foreach (var h in region.Hexes) byCoord[(h.Q, h.R)] = h;

            var to = new HexCoordinate(toQ, toR);
            int score = new HexCoordinate(fromQ, fromR).DistanceTo(to);   // current distance = the bar to beat
            (int q, int r)? bestHex = null;
            foreach (var nb in new HexCoordinate(fromQ, fromR).GetNeighbors())
            {
                if (!byCoord.TryGetValue((nb.Q, nb.R), out var hx)) continue;   // off the patch
                if (HexPathfinder.IsImpassable(hx.Terrain)) continue;          // can't stand on open water
                int d = nb.DistanceTo(to);
                if (away ? d > score : d < score) { score = d; bestHex = (nb.Q, nb.R); }
            }
            return bestHex;
        }

        /// <summary>True if the unit's formation has a non-empty order queue (a queued plan overrides auto-ROE).</summary>
        private static bool FormationHasOrders(GroundForcesDB forces, GroundUnit unit)
        {
            if (unit.FormationId < 0 || forces.Formations == null) return false;
            foreach (var f in forces.Formations)
                if (f.FormationId == unit.FormationId) return f.Orders != null && f.Orders.Count > 0;
            return false;
        }

        /// <summary>
        /// FORMATION ORDER QUEUE (O1): run each formation's FRONT order; pop it when it completes so the next begins —
        /// sequential "then" semantics (a real waypoint chain). A move order is kicked off once (<c>Issued</c>) and
        /// completes when the leader arrives; a hold counts down; a stance/ROE order applies instantly. Defensive: an
        /// order that can't make progress (missing region layer, empty formation) is dropped so the queue never wedges.
        /// </summary>
        private static void ProcessFormationOrders(Entity body, GroundForcesDB forces, PlanetRegionsDB regionsDB, int deltaSeconds)
        {
            if (forces.Formations == null) return;
            foreach (var f in forces.Formations)
            {
                if (f.Orders == null || f.Orders.Count == 0) continue;
                var order = f.Orders[0];
                bool done;
                switch (order.Type)
                {
                    case GroundOrderType.MoveToRegion:
                        if (!order.Issued) { GroundForces.OrderFormationMove(body, f, order.TargetRegion); order.Issued = true; done = false; }
                        else done = LeaderIdle(forces, f);   // resolved when the leader stops moving (arrived, or couldn't)
                        break;

                    case GroundOrderType.MoveToHex:
                        // G6b-2: a queued formation move now marches on the GLOBAL planetary grid (TargetQ/TargetR are
                        // global cylinder coords), not the per-region fine disk — the developer's "troops move on the
                        // planetary hexes." Uses the CI-green OrderFormationMoveToGlobalHex (G6b-1).
                        if (!order.Issued) { GroundForces.OrderFormationMoveToGlobalHex(body, f, order.TargetQ, order.TargetR); order.Issued = true; done = false; }
                        else done = LeaderIdle(forces, f);
                        break;

                    case GroundOrderType.HoldFor:
                        order.SecondsRemaining -= deltaSeconds;
                        done = order.SecondsRemaining <= 0;
                        break;

                    case GroundOrderType.SetStance:
                        TryApplyStanceOrder(body, f, order.StanceId);
                        done = true;
                        break;

                    case GroundOrderType.SetEngagement:
                        GroundFormationDoctrine.SetEngagementStance(f, order.Engagement);
                        done = true;
                        break;

                    default:
                        done = true;   // unknown order → drop it (never wedge the queue)
                        break;
                }
                if (done) f.Orders.RemoveAt(0);   // pop → the next order starts next tick
            }
        }

        /// <summary>A move is resolved when the formation's LEADER has stopped moving — it either arrived at the target
        /// or couldn't proceed (unreachable). Popping on idle (not on exact arrival) means a blocked move never wedges
        /// the queue. Empty formation → treated as idle (nothing to move).</summary>
        private static bool LeaderIdle(GroundForcesDB forces, GroundFormation f)
        {
            var leader = GroundFormationTools.Leader(forces, f) ?? FirstMember(forces, f);
            if (leader == null) return true;
            // Idle = not marching on ANY grid: not doing a coarse region hop, no per-region fine path (ROE step),
            // and no GLOBAL planetary path (G6b-2 formation move). Checking both grids keeps the queue correct while
            // the ROE micro still uses the per-region path (retired in a later G6b slice).
            bool hexMarching = leader.HexPath != null && leader.HexPath.Count > 0;
            bool globalMarching = leader.GlobalPath != null && leader.GlobalPath.Count > 0;
            return leader.MovingToRegion < 0 && !hexMarching && !globalMarching;
        }

        private static GroundUnit FirstMember(GroundForcesDB forces, GroundFormation f)
        {
            foreach (var u in forces.Units) if (u.FormationId == f.FormationId) return u;
            return null;
        }

        /// <summary>Apply a queued SetStance order by looking the id up in the body's mod stance catalog (defensive).</summary>
        private static void TryApplyStanceOrder(Entity body, GroundFormation f, string stanceId)
        {
            if (string.IsNullOrEmpty(stanceId)) return;
            var catalog = body.Manager?.Game?.StartingGameData?.GroundStances;
            if (catalog == null || !catalog.TryGetValue(stanceId, out var bp)) return;
            GroundFormationDoctrine.TrySetStance(f, bp, body.StarSysDateTime);
        }


        /// <summary>Keep each formation's leader valid after casualties: if the leader unit is gone, leadership passes
        /// to a surviving member (or -1 if the formation was wiped). Fleet-like reassignment, no penalty.</summary>
        private static void MaintainFormations(GroundForcesDB forces)
        {
            if (forces.Formations == null || forces.Formations.Count == 0) return;
            foreach (var f in forces.Formations)
            {
                bool leaderAlive = false;
                int firstMemberId = -1;
                foreach (var u in forces.Units)
                {
                    if (u.FormationId != f.FormationId) continue;
                    if (firstMemberId < 0) firstMemberId = u.UnitId;
                    if (u.UnitId == f.LeaderUnitId) { leaderAlive = true; break; }
                }
                if (!leaderAlive) f.LeaderUnitId = firstMemberId;   // -1 if the formation is now empty
            }
        }

        private static bool IsDamageEffect(HazardEffectType t)
            => t == HazardEffectType.HeatDamage || t == HazardEffectType.RadiationDamage
            || t == HazardEffectType.KineticDamage || t == HazardEffectType.CorrosiveDamage
            || t == HazardEffectType.EMDamage || t == HazardEffectType.GravimetricDamage;

        private static void TryCapturePlanet(Entity body, PlanetRegionsDB regionsDB)
        {
            if (regionsDB == null || regionsDB.Regions.Count == 0) return;

            int owner = regionsDB.Regions[0].OwnerFactionID;
            if (owner < 0) return;
            foreach (var r in regionsDB.Regions)
                if (r.OwnerFactionID != owner) return;   // not uniformly held → not taken

            var manager = body.Manager;
            if (manager == null) return;
            foreach (var colony in manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
            {
                if (colony.TryGetDataBlob<ColonyInfoDB>(out var info)
                    && info.PlanetEntity != null && info.PlanetEntity.Id == body.Id
                    && colony.FactionOwnerID != owner)
                {
                    colony.FactionOwnerID = owner;   // the planet is taken (v1: ownership flip; deeper transfer later)
                }
            }
        }
    }
}
