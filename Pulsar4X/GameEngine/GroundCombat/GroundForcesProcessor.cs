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

        /// <summary>Ammo a magazine-fed unit burns each salvo it FIRES (G2.3a). A unit with no magazine
        /// (<c>GroundAmmo.CarriesAmmo</c> false — every default/garrison unit today) never drains, so live combat is
        /// byte-identical until an ammo-fed unit is fielded. FLAGGED balance value.</summary>
        public const double AmmoPerSalvo_kg = 1.0;   // FLAGGED balance value

        /// <summary>Bombard strength (in `ComponentInstance.HealthPercent` units) a DestroyInfrastructure order lays on a
        /// hex per tick, per point of the attacking units' summed Attack (G3) — the "staged drain default" (a building is
        /// razed over several ticks, not one-shot). Attack 100 × 0.1 = 10 HealthPercent/tick → ~10 ticks to raze a
        /// full-health building. FLAGGED balance value.</summary>
        public const double InfraDestroyStrengthPerAttack = 0.1;   // FLAGGED balance value

        /// <summary>Ground COMBAT INTERRUPT flag (4b) — the ground mirror of
        /// <c>Combat.CombatEngagement.InterruptTimeOnNewEngagement</c>. When true, the first tick a NEW planetary
        /// battle forms on a body, the processor calls <c>MasterTimePulse.RequestCombatHalt()</c> so the clock stops
        /// and the player is notified (the client already surfaces <c>CombatInterruptPending</c> as an on-screen
        /// banner — space and ground share it). Default FALSE so headless tests advance deterministically; the client
        /// turns it on at startup next to the space combat flags.</summary>
        public static bool InterruptTimeOnNewBattle = false;

        /// <summary>GROUND TACTICAL BRAIN gate (Operation Earthfall G2.2c) — when true, a STEP in this hotloop
        /// (<see cref="GroundTacticalBrain.Run"/>, NOT a second processor — L9) drives every AI-owned battalion's
        /// posture (Stance / ROE / Intent) from the fog-honest odds each tick: it decides WHEN the AI is offensive vs
        /// defensive, digs in, or retreats (the answer to "is the AI smart enough…"). Default FALSE so the engine test
        /// suite + a factory game are byte-identical (no AI touches a stance/order); CORE flips it ON alongside the other
        /// AI gates on the New-Game / menu path (PW). A PLAYER order on a battalion always overrides the brain.</summary>
        public static bool EnableGroundTacticalAI = false;

        /// <summary>W-TRACK W3 — SUB-FORMATION ROLE MANEUVER gate. When true, a unit auto-maneuvering under its
        /// formation's ROE moves to ITS OWN ROLE's ideal range band instead of all units marching uniformly:
        /// a SCREEN (fast/light) unit leads to contact, a LINE unit closes to its firing range and holds, an ARTILLERY
        /// (long-reach) unit kites to keep its standoff, a SUPPORT (no-attack) unit stays back — the ground echo of
        /// space sub-fleet roles ("fighters move differently than a line ship"). Roles come from
        /// <see cref="GroundRoleComposer.ClassifyRole"/>. Default FALSE → the maneuver step is byte-identical (the
        /// uniform ROE close/stand-off path runs unchanged); CORE flips it ON alongside the other AI gates on the
        /// New-Game / menu path. A HoldGround formation still never auto-maneuvers, and a player queued order still wins.</summary>
        public static bool EnableGroundRoleManeuver = false;

        /// <summary>MINI-HEX real-distance combat gate (docs/combat/MINI-HEX-TACTICAL-GRID-DESIGN.md, M2). When true, the
        /// resolver's range gate fires a weapon when the REAL metre gap between two units
        /// (<see cref="GroundMiniHex.RealGapMetres(GroundUnit,GroundUnit,Pulsar4X.Engine.Entity)"/>, measured on the
        /// continuous coarse-global-hex + mini-hex field) is within the weapon's real <c>Range_m</c> — the developer's
        /// "the km on the gun is the truth, the hex is only the ruler." Two units in the SAME coarse global hex read gap 0
        /// → they fight ("same hex = combat"); different coarse hexes are a real (large) distance apart → no fight until
        /// they close (mini-hex movement is M3). Default FALSE so the engine test suite is byte-identical (the resolver
        /// keeps the legacy local-patch HEX gate <c>HexDist ≤ Range</c>, on which every existing ClosingFight/RangeCombat/
        /// ROE gauge is calibrated); CORE flips it ON on the New-Game / menu path so a real game gets real distances
        /// on-by-default (the developer's "keep the real gate on, no flag" — the flag exists only to keep the hex-
        /// calibrated CI gauges valid, not to hide the feature from players).</summary>
        public static bool EnableMiniHexCombat = false;

        /// <summary>INITIAL ENGAGEMENT SPREAD (docs/combat/MINI-HEX-TACTICAL-GRID-DESIGN.md, M3 — the ground twin of space's
        /// <see cref="Pulsar4X.Combat.CombatEngagement"/> seeding <c>Separation_m</c> at battle start). When true, the FIRST
        /// tick a region becomes newly contested the processor pushes the two sides APART on the region's hex patch — the
        /// holder (the region's owner, or the longest-ranged faction on neutral ground) stays at its muster hex, every other
        /// faction's units are placed the holder's longest weapon-range away — so a fight OPENS at range and CLOSES over
        /// ticks (the existing <see cref="ApplyEngagementManeuvers"/> machinery does the closing), letting a longer-ranged
        /// unit thin the closing force during the approach (the "mobile artillery eliminates 50% before they close" fight).
        /// Without it, both sides muster at the same region-centre hex → gap 0 → point-blank, no approach.
        /// Operates on the per-region HEX grid (<c>HexQ/HexR</c>) — the ONE space where the range gate, the closing
        /// maneuver, AND differentiated weapon ranges (1 vs 3 hexes) already work together; the mini-hex metre grid needs
        /// real-km weapon ranges + mini-hex movement (M3b/S4) before it can host the same fight. Default FALSE so every CI
        /// gauge (which musters co-located and asserts point-blank fire) stays byte-identical; the menu turns it on. Pure /
        /// deterministic (no RNG) so fast-forward == watch.</summary>
        public static bool EnableInitialEngagementSpread = false;

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

            // 0b) UPKEEP: bill each owning faction the standing cost of its ground units, once a month (the ground echo
            //     of a station's operating cost). Folded here — NOT a second processor (L9). No-op for free (0-upkeep)
            //     units, so byte-identical until a design sets UpkeepCredits. Never throws (guarded inside).
            GroundUpkeep.BillIfDue(body, body.StarSysDateTime);

            // 0c) BEACHHEAD ON-SITE BUILD (G1.2): a landed COMBAT ENGINEER (a unit whose design mounts a
            //     GroundConstructorAtb) standing on FRIENDLY-HELD, enemy-free ground with landed footprint parts erects a
            //     footprint building ON SITE — with NO colony present, hosted in the invader's beachhead outpost. The
            //     placed bunker fortifies (the GroundDefenseAtb path) + is a bombard/capture target on the war map, and
            //     the region becomes a resupply point (consumed in G2). Folded here — NOT a second processor (L9). A
            //     no-op until an engineer unit exists and lands, so a stock game is byte-identical. Never throws.
            GroundBeachhead.TickBuilds(body, deltaSeconds);

            // 0d) RESUPPLY (G2.3b): a magazine-fed unit standing at a DEPOT — a friendly-HELD region that holds a base
            //     (a G1 beachhead bunker or the region's colony installations — Region.InstallationIds non-empty) —
            //     auto-rearms to full (GroundForces.ResupplyUnit, previously caller-less). No depot / enemy ground / no
            //     magazine → no-op, so a default game (no ammo-fed unit) is byte-identical. Never throws (guarded).
            if (forces.Units != null && body.TryGetDataBlob<PlanetRegionsDB>(out var supplyRegions))
                foreach (var su in forces.Units)
                {
                    if (su == null || su.Health <= 0 || su.MaxAmmo_kg <= 0 || su.CurrentAmmo_kg >= su.MaxAmmo_kg) continue;
                    if (IsResupplyDepot(supplyRegions, su.FactionOwnerID, su.RegionIndex))
                        GroundForces.ResupplyUnit(body, su);
                }

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

            // 1b1) GROUND TACTICAL BRAIN (G2.2c) — the officer of the deck. Behind EnableGroundTacticalAI (default off →
            //      byte-identical), read the fog-honest odds for every AI-owned battalion and set its Stance / ROE /
            //      Intent (dig in when outnumbered, press an edge, retreat when losing — the "when defensive vs
            //      offensive" decision). A STEP here, not a second processor (L9). It queues AI-issued MoveRegion orders
            //      that the queue step below then executes; a PLAYER order on a battalion always overrides it. Defensive.
            if (EnableGroundTacticalAI && regionsDB != null)
                GroundTacticalBrain.Run(body, forces, regionsDB, body.StarSysDateTime);

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

            // 1e) INITIAL ENGAGEMENT SPREAD (M3): the FIRST tick a region becomes contested, open a real gap between the
            // sides so the closing fight has an approach (the ground twin of space seeding Separation_m at StartEngagement).
            // Detected HERE — before the combat loop below — so the OPENING salvo already respects the gap (the :307
            // WasInBattle edge fires AFTER this loop, too late for the first salvo). Flag-gated → byte-identical when off.
            if (EnableInitialEngagementSpread)
                SpreadNewlyContestedRegions(forces, regionsDB, byRegion);

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
        /// <summary>Is region <paramref name="regionIndex"/> a RESUPPLY DEPOT for <paramref name="factionId"/> — a region
        /// it HOLDS that contains a base (colony installations or a G1 beachhead bunker → <c>Region.InstallationIds</c>
        /// non-empty)? A unit standing here rearms; empty ground or enemy ground offers nothing to draw from. Bounds/
        /// null-safe (G2.3b).</summary>
        private static bool IsResupplyDepot(PlanetRegionsDB regionsDB, int factionId, int regionIndex)
        {
            if (regionsDB == null || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return false;
            var reg = regionsDB.Regions[regionIndex];
            if (reg.OwnerFactionID != factionId) return false;                       // must hold the ground
            return reg.InstallationIds != null && reg.InstallationIds.Count > 0;     // a base/depot (colony or beachhead) is here
        }

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

            // SHARED SALVO MATH (one definition for both the collapsed single-weapon path and the W2 per-weapon-band
            // path): fire ALREADY-SCALED <paramref name="pool"/> from weapon <paramref name="profile"/> at the targets it
            // can reach, health-weighted, through the shared kernel — dodge (HitFraction) → depleting shield pool →
            // flat-per-source armour (penetration + shot-count + nature). Accumulates into `incoming` (pre-salvo state →
            // simultaneous). Arithmetic per target is byte-identical to the pre-W2 resolver.
            void FireWeaponAtReachable(GroundUnit attacker, List<GroundUnit> reachable, double pool, Pulsar4X.Combat.WeaponProfile profile)
            {
                double totalH = 0.0;
                foreach (var t in reachable) totalH += t.Health;
                if (totalH <= 0) return;
                double shieldSoakFrac = Pulsar4X.Combat.CombatKernel.ShieldSoakFraction(profile.Nature);
                foreach (var t in reachable)
                {
                    // DODGE (kernel HitFraction): an aimed slug is dodged ~(1−evasion); an area/beam shot lands ~fully.
                    // RANGE-AWARE (mini-hex M3a): pass the REAL metre gap to the SAME kernel the space closing fight uses,
                    // so a shot that must cross a real distance loses accuracy exactly as it does in space (a long-range
                    // artillery round harasses inaccurately from afar; a beam/guided shot barely cares) — the ground half
                    // of "the mobile artillery thins the enemy before they close." Off (or at gap 0, the co-located common
                    // case) → separation 0 → the range term is inert → byte-identical to the pre-M3a resolver. Its full
                    // payoff awaits closing MOVEMENT (units spread on the 2D plane + close over time — the S4 slice).
                    double sep_m = EnableMiniHexCombat ? GroundMiniHex.RealGapMetres(attacker, t, forces?.OwningEntity) : 0.0;
                    double contribution = pool * (t.Health / totalH)
                        * Pulsar4X.Combat.CombatKernel.HitFraction(profile, t.Evasion, sep_m);
                    // SHIELD POOL (3c): the target's depleting shield soaks the soakable fraction (by weapon nature —
                    // kinetic fully, energy half-bleeds, exotic bypasses) up to its CURRENT charge, BEFORE armour.
                    if (t.CurrentShield > 0 && shieldSoakFrac > 0 && contribution > 0)
                    {
                        double absorbed = System.Math.Min(contribution * shieldSoakFrac, t.CurrentShield);
                        t.CurrentShield -= absorbed;
                        contribution -= absorbed;
                    }
                    // ARMOUR: flat-per-source plating bounces what's left (the swarm-vs-alpha identity) — the shared
                    // CombatKernel armour soak, reduced by the weapon's PENETRATION (W1b), split into its SHOT COUNT
                    // (W2b), scaled by the target's armour-NATURE tuning (⚙3). Defaults → byte-identical.
                    int shotCount = Pulsar4X.Combat.CombatKernel.BurstShotCount(profile);
                    double natureFactor = t.ArmourResistFor(profile.Nature);
                    contribution = GroundDamageMatrix.ArmourSoak(t.Defense, contribution, shotCount, profile.Penetration, natureFactor);
                    incoming.TryGetValue(t, out var acc);
                    incoming[t] = acc + contribution;
                }
            }

            foreach (var f in byFaction.Keys)
            {
                foreach (var g in byFaction.Keys)
                {
                    if (f == g) continue;
                    bool gIsDefender = (g == defenderFaction);
                    foreach (var u in byFaction[f])
                    {
                        if (u.Health <= 0) continue;

                        // Terrain affinity = the unit TYPE's innate edge (TerrainAttackMult) × its DESIGNED locomotion's
                        // edge (LocomotionTerrainMult, Propulsion ⚙2). RoughHandlingForUnit reads the unit's designed
                        // locomotion (0.5 neutral → ×1.0). The Armor▸Inf▸Arty TRIANGLE has DISSOLVED (resolver merge 3b-ii):
                        // the type edge emerges from raw stats × weapon-nature vs the target's evasion/shield/armour.
                        double roughHandling = GroundMobility.RoughHandlingForUnit(forces.OwningEntity, u);

                        // ── W-TRACK W2 — PER-WEAPON RANGE BANDING ──────────────────────────────────────────────────────
                        // An assembled unit carries a per-weapon LOADOUT (W1). Each mounted weapon fires in ITS OWN hex
                        // range band with ITS OWN nature — so a long-range cannon (undodgeable Artillery) reaches a
                        // CLOSING enemy while a short-range rifle (dodgeable Ballistic) is still silent (the ground echo of
                        // a ship's per-weapon range bands). Ammo is a unit-level pool (v1): a dry magazine silences the
                        // whole unit; a fed one burns ONE salvo if it fires at least one weapon. A unit with exactly ONE
                        // weapon yields a mount whose Attack/Mode/Range equal the collapsed values (same operands, same
                        // multiply order below) → this reproduces the collapsed path bit-for-bit. A monolithic / garrison /
                        // DevTools unit (empty loadout) takes the collapsed path → byte-identical.
                        if (u.WeaponLoadout != null && u.WeaponLoadout.Count > 0)
                        {
                            if (GroundAmmo.CarriesAmmo(u) && GroundAmmo.IsDry(u)) continue;   // dry → this unit's fire is silent (v1: whole unit)
                            bool firedAny = false;
                            foreach (var m in u.WeaponLoadout)
                            {
                                // The g-units THIS WEAPON can reach (its own hex range). Co-located (dist 0) → all of g.
                                List<GroundUnit> reachable = null;
                                foreach (var t in byFaction[g])
                                {
                                    if (t.Health <= 0) continue;
                                    if (!WeaponReaches(u, t, m.RangeHexes, m.Range_m, forces)) continue;   // out of THIS weapon's band this salvo
                                    (reachable ??= new List<GroundUnit>()).Add(t);
                                }
                                if (reachable == null) continue;                   // this weapon reaches nothing this salvo
                                firedAny = true;

                                // Same multiply ORDER as the collapsed path (byte-identity for a single-weapon unit,
                                // where m.Attack == u.Attack): m.Attack × terrain × locomotion × stance × SalvoScale.
                                double atk = m.Attack
                                    * GroundTerrain.TerrainAttackMult(u.UnitType, terrain)
                                    * GroundTerrain.LocomotionTerrainMult(roughHandling, terrain)
                                    * GroundFormationDoctrine.AttackMult(forces, u);
                                double pool = atk * SalvoScale;
                                if (gIsDefender && coverFort > 0) pool /= coverFort;
                                FireWeaponAtReachable(u, reachable, pool, GroundCombatant.ToWeaponProfile(u, m));
                            }
                            // Burn one salvo of ammo iff the unit fired at least one weapon (a magazine-fed unit only).
                            if (firedAny && deltaSeconds > 0 && GroundAmmo.CarriesAmmo(u)) GroundAmmo.Consume(u, AmmoPerSalvo_kg);
                            continue;
                        }

                        // ── COLLAPSED single-weapon path (empty loadout) — byte-identical to the pre-W2 resolver ──────
                        // The g-units THIS attacker can reach (within its collapsed hex Range). Co-located → all of g.
                        List<GroundUnit> reachableC = null;
                        foreach (var t in byFaction[g])
                        {
                            if (t.Health <= 0) continue;
                            if (!WeaponReaches(u, t, u.Range, u.Range_m, forces)) continue;   // out of this attacker's reach → it can't hit t
                            (reachableC ??= new List<GroundUnit>()).Add(t);
                        }
                        if (reachableC == null) continue;            // nothing in range → this unit fires nothing this salvo

                        // AMMO (G2.3a): a magazine-fed unit goes SILENT once dry; else it burns a salvo as it fires. A unit
                        // with no magazine (every default/garrison unit) never drains → byte-identical.
                        if (GroundAmmo.CarriesAmmo(u))
                        {
                            if (GroundAmmo.IsDry(u)) continue;                       // dry → this attacker's ammo weapons are silent
                            if (deltaSeconds > 0) GroundAmmo.Consume(u, AmmoPerSalvo_kg);   // burn a salvo as it fires
                        }

                        // Output = attack × terrain affinity × stance × SalvoScale (same operand order as the W2 path).
                        double atkC = u.Attack
                            * GroundTerrain.TerrainAttackMult(u.UnitType, terrain)
                            * GroundTerrain.LocomotionTerrainMult(roughHandling, terrain)
                            * GroundFormationDoctrine.AttackMult(forces, u);
                        double poolC = atkC * SalvoScale;
                        if (gIsDefender && coverFort > 0) poolC /= coverFort;

                        // Route the collapsed weapon through the SHARED KERNEL (resolver merge 3c) — dodge + shield +
                        // armour, exactly as before.
                        FireWeaponAtReachable(u, reachableC, poolC, GroundCombatant.ToWeaponProfile(u));
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

        /// <summary>Whether attacker <paramref name="u"/>'s weapon (hex reach <paramref name="rangeHexes"/> / real reach
        /// <paramref name="range_m"/> metres) can hit target <paramref name="t"/> this salvo — THE range gate, in one
        /// place so the two fire paths (collapsed + per-weapon-band) can't diverge. Default
        /// (<see cref="EnableMiniHexCombat"/> off): the legacy local-patch HEX gate <c>HexDist ≤ rangeHexes</c>, on which
        /// every existing combat gauge is calibrated → byte-identical. On (M2): the REAL metre gap on the continuous
        /// mini-hex field (<see cref="GroundMiniHex.RealGapMetres(GroundUnit,GroundUnit,Pulsar4X.Engine.Entity)"/>) ≤ the
        /// weapon's real <c>Range_m</c> — two units in the same coarse global hex read gap 0 (fight), different coarse
        /// hexes are a real distance apart (no fight until they close, M3). Never throws.</summary>
        private static bool WeaponReaches(GroundUnit u, GroundUnit t, int rangeHexes, double range_m, GroundForcesDB forces)
        {
            // Both branches route the reach decision through the SHARED CombatKernel.WithinReach (the resolver-merge
            // range gate — "can this weapon hit that target" now lives in ONE place for space AND ground). WithinReach is
            // the plain `gap <= reach` compare, so a reach-0 melee weapon stays CONTACT-ONLY (not unbounded — the ground
            // convention, unlike space's beam). Byte-identical to the old `RealGapMetres <= range_m` / `HexDist <= rangeHexes`.
            if (EnableMiniHexCombat)
                return Pulsar4X.Combat.CombatKernel.WithinReach(range_m, GroundMiniHex.RealGapMetres(u, t, forces?.OwningEntity));
            return Pulsar4X.Combat.CombatKernel.WithinReach(rangeHexes, HexDist(u, t));
        }

        /// <summary>INITIAL ENGAGEMENT SPREAD (M3): the first tick a region becomes contested, place the sides a real hex
        /// gap apart so the closing fight has an approach — the ground twin of space seeding <c>Separation_m</c> at
        /// StartEngagement. The HOLDER (the region's owner, or the longest-ranged faction on neutral ground) stays at its
        /// muster hex; every other faction's units are pushed the holder's longest weapon-range (<c>unit.Range</c> hexes)
        /// away along +Q, snapped to the nearest in-patch passable hex. So the holder's longest gun opens fire immediately
        /// and the shorter-ranged sides must CLOSE (<see cref="ApplyEngagementManeuvers"/> does the closing from the next
        /// tick — a longer-ranged unit thins the closer during the approach). Pure/deterministic — holder + gap are integer
        /// functions of unit ranges + faction ids, the snap is a fixed inward search, NO RNG (fast-forward == watch). A
        /// region is spread ONCE per contest (the save-safe <see cref="GroundForcesDB.SpreadRegions"/> guard); a fight that
        /// ends clears the guard so a fresh one re-spreads. Never throws (L4).</summary>
        private static void SpreadNewlyContestedRegions(GroundForcesDB forces, PlanetRegionsDB regionsDB,
            Dictionary<int, List<GroundUnit>> byRegion)
        {
            if (regionsDB == null || forces?.SpreadRegions == null) return;
            var spread = forces.SpreadRegions;   // save-safe per-region "already opened this contest" guard

            // Prune the guard: a region no longer contested (fight ended / one side wiped) clears, so a fresh battle
            // there re-spreads next time it forms.
            if (spread.Count > 0)
            {
                var stillContested = new HashSet<int>();
                foreach (var kv in byRegion)
                {
                    var fset = new HashSet<int>();
                    foreach (var u in kv.Value) if (u.Health > 0) fset.Add(u.FactionOwnerID);
                    if (fset.Count >= 2) stillContested.Add(kv.Key);
                }
                spread.RemoveWhere(ri => !stillContested.Contains(ri));
            }

            foreach (var kv in byRegion)
            {
                int ri = kv.Key;
                if (spread.Contains(ri)) continue;                       // already opened this contest
                if (ri < 0 || ri >= regionsDB.Regions.Count) continue;

                // Each present faction's max hex range (unit.Range == Max(mount.RangeHexes) by the W1 invariant).
                var factionMaxRange = new Dictionary<int, int>();
                foreach (var u in kv.Value)
                {
                    if (u.Health <= 0) continue;
                    int r = u.Range > 0 ? u.Range : 1;
                    if (!factionMaxRange.TryGetValue(u.FactionOwnerID, out var cur) || r > cur)
                        factionMaxRange[u.FactionOwnerID] = r;
                }
                if (factionMaxRange.Count < 2) continue;                 // not a two-sided fight

                // The HOLDER: the region owner if it's present, else the longest-ranged faction. Iterate a SORTED id
                // list (not dict order) so the tie-break — lowest faction id on equal range — is deterministic.
                var region = regionsDB.Regions[ri];
                int holder;
                if (factionMaxRange.ContainsKey(region.OwnerFactionID)) holder = region.OwnerFactionID;
                else
                {
                    var fids = new List<int>(factionMaxRange.Keys);
                    fids.Sort();
                    holder = fids[0];
                    int bestR = factionMaxRange[holder];
                    foreach (var fid in fids)
                        if (factionMaxRange[fid] > bestR) { bestR = factionMaxRange[fid]; holder = fid; }
                }

                int gap = factionMaxRange[holder];                       // the holder's longest gun sets the opening range

                // Holder units stay at their muster hex; push every non-holder unit `gap` hexes along +Q, snapped in-patch.
                foreach (var u in kv.Value)
                {
                    if (u.Health <= 0 || u.FactionOwnerID == holder) continue;
                    var placed = SnapSpreadHex(region, u.HexQ + gap, u.HexR);
                    if (placed != null) { u.HexQ = placed.Value.q; u.HexR = placed.Value.r; }
                }
                spread.Add(ri);
            }
        }

        /// <summary>Deterministically snap a spread target (<paramref name="q"/>,<paramref name="r"/>) to the nearest
        /// in-patch PASSABLE hex, stepping the column inward toward the muster origin until one is found — so an
        /// off-patch or ocean target never strands a unit. Null if no passable hex exists on that row (defensive → the
        /// caller leaves the unit co-located). Same passability predicate as <see cref="PickStepHex"/>/SnapToPassable.</summary>
        private static (int q, int r)? SnapSpreadHex(Region region, int q, int r)
        {
            if (region?.Hexes == null || region.Hexes.Count == 0) return null;
            var byCoord = new Dictionary<(int, int), GroundHex>();
            foreach (var h in region.Hexes) byCoord[(h.Q, h.R)] = h;
            int step = q >= 0 ? 1 : -1;                                  // walk the offset back toward the muster column (0)
            for (int x = q; step > 0 ? x >= 0 : x <= 0; x -= step)
            {
                if (byCoord.TryGetValue((x, r), out var hx) && !HexPathfinder.IsImpassable(hx.Terrain))
                    return (x, r);
            }
            return null;
        }

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
                if (EnableGroundRoleManeuver)
                {
                    // W3 — each unit maneuvers to ITS ROLE's ideal range band (screen leads to contact, line closes to
                    // its firing range and holds, artillery kites to keep its standoff, support stays back): the ground
                    // echo of space sub-fleet roles — "fighters move differently than a line ship." The formation ROE
                    // still gates WHETHER it auto-maneuvers (a HoldGround formation was already skipped above); the ROLE
                    // decides the target band. Byte-identical when the flag is off.
                    var role = GroundRoleComposer.ClassifyRole(unit);
                    bool? mv = GroundRoleComposer.RoleMoveAway(role, best, unit.Range, enemy.Range);
                    if (mv == null) continue;                    // already at its ideal band → hold and fire
                    moveAway = mv.Value;
                }
                else if (stance == GroundEngagementStance.CloseToEngage)
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

                    case GroundOrderType.DestroyInfrastructure:
                        // G3: raze the footprint building(s) on the target hex — gated on a formation unit standing in
                        // the region within Range of the hex; staged drain (razes over several ticks). Done when the hex
                        // is empty (razed) or nothing is in reach (pop, never wedge).
                        done = ResolveInfraOrder(body, forces, regionsDB, f, order, destroy: true);
                        break;

                    case GroundOrderType.CaptureInfrastructure:
                        // G3: seize the hex — flip GroundHex.OwnerFactionID so its buildings stop fortifying the defender
                        // (the first consumer that makes hex ownership matter). Range-gated; instant (v1).
                        done = ResolveInfraOrder(body, forces, regionsDB, f, order, destroy: false);
                        break;

                    default:
                        done = true;   // unknown order → drop it (never wedge the queue)
                        break;
                }
                if (done) f.Orders.RemoveAt(0);   // pop → the next order starts next tick
            }
        }

        /// <summary>G3 — resolve a DestroyInfrastructure / CaptureInfrastructure order for a formation against a target
        /// hex. RANGE-GATED: the formation must have a living unit standing IN the target region within its Range of the
        /// hex (the resolver's own HexDist ≤ Range rule; footprints sit on the region centre hex 0,0). DESTROY: Σ in-range
        /// Attack × <see cref="InfraDestroyStrengthPerAttack"/> bombards the hex (staged — done when the hex is razed).
        /// CAPTURE: flip the hex owner (instant). Returns true when the order is DONE (razed / seized / nothing to do /
        /// out of reach) so the queue pops it — never wedges. Defensive/no-throw (runs in the ground hotloop, L4).</summary>
        private static bool ResolveInfraOrder(Entity body, GroundForcesDB forces, PlanetRegionsDB regionsDB,
            GroundFormation f, GroundOrder order, bool destroy)
        {
            if (regionsDB?.Regions == null || order.TargetRegion < 0 || order.TargetRegion >= regionsDB.Regions.Count) return true;
            var region = regionsDB.Regions[order.TargetRegion];
            GroundHex hex = null;
            if (region.Hexes != null)
                foreach (var h in region.Hexes) if (h.Q == order.TargetQ && h.R == order.TargetR) { hex = h; break; }
            if (hex == null || hex.InstallationIds == null || hex.InstallationIds.Count == 0) return true;   // nothing to raze/seize

            // RANGE GATE: Σ Attack of the formation's living units standing in the target region that reach the hex.
            double reachAttack = 0.0;
            var targetHex = new HexCoordinate(order.TargetQ, order.TargetR);
            foreach (var u in GroundFormationTools.MembersOf(forces, f))
            {
                if (u.Health <= 0 || u.RegionIndex != order.TargetRegion) continue;
                if (new HexCoordinate(u.HexQ, u.HexR).DistanceTo(targetHex) > u.Range) continue;
                reachAttack += u.Attack;
            }
            if (reachAttack <= 0.0) return true;   // no unit in reach → can't fire; pop cleanly (never wedge)

            if (destroy)
            {
                GroundBuildings.BombardHex(body, order.TargetRegion, order.TargetQ, order.TargetR, reachAttack * InfraDestroyStrengthPerAttack);
                return hex.InstallationIds.Count == 0;   // done once the hex is razed; else keep draining next tick (staged)
            }
            hex.OwnerFactionID = f.FactionOwnerID;   // seize — its buildings stop counting for the defender's fortification
            return true;
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
            || t == HazardEffectType.EMDamage || t == HazardEffectType.GravimetricDamage
            // Ground-only surface hazards (2026-07-17): an unsealed unit bleeds in vacuum / a toxic atmosphere. The
            // unit's EnvResistance (a sealed suit) negates it via the same E4 path — so sealing is a real decision.
            || t == HazardEffectType.Vacuum || t == HazardEffectType.ToxicAtmosphere;

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
