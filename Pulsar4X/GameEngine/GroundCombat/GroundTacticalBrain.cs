using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;      // PlanetRegionsDB, Region
using Pulsar4X.Colonies;    // ColonyInfoDB (homeland-defender derive)
using Pulsar4X.Factions;    // FactionInfoDB, PersonalityDB, PersonalityTrait, MilitaryCommand (READ-ONLY cross-lane)
using Pulsar4X.Ships;       // ShipInfoDB (orbital-support read)
using Pulsar4X.Blueprints;  // GroundStanceBlueprint

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// THE WIRE (Operation Earthfall G2.2c) — the step that runs the pure <see cref="GroundTactics.DecidePosture"/>
    /// brain against live AI battalions each ground tick and ACTS on it through the real levers. Invoked from
    /// <c>GroundForcesProcessor.ProcessBody</c> (a STEP in the existing hotloop — NO new processor, landmine L9) behind
    /// <see cref="GroundForcesProcessor.EnableGroundTacticalAI"/> (default OFF; CORE flips it on the menu path in PW).
    /// Flag off ⇒ this never runs ⇒ byte-identical.
    ///
    /// Per AI-owned (<see cref="FactionInfoDB.IsNPC"/>) battalion it: forms loose AI units up first (reusing G2.1's
    /// <see cref="GroundAssembly.FormUpLoose"/> — so the brain always has battalions to command), reads the fog-honest
    /// odds, applies the decided Stance (<c>TrySetStance</c>, respecting the switch cooldown — with a BREAK-GLASS on a
    /// survival shift so a dying unit is never locked offensive), the ROE (<c>SetEngagementStance</c>), and the Intent
    /// (a MoveRegion order marked <see cref="GroundOrderIssuer.Ai"/>). ORDER OWNERSHIP: a battalion holding ANY
    /// <see cref="GroundOrderIssuer.Player"/> order is left ENTIRELY alone — the human always overrides. Every decision's
    /// Reason is recorded on the formation (the AI-tape rule; the client shows it). Defensive/no-throw (L4).
    /// </summary>
    public static class GroundTacticalBrain
    {
        /// <summary>Drive the brain over every AI battalion on <paramref name="body"/>. Called once per ground tick from
        /// the processor (behind the flag). <paramref name="now"/> is the body's game time (stance cooldown clock).</summary>
        public static void Run(Entity body, GroundForcesDB forces, PlanetRegionsDB regionsDB, DateTime now)
        {
            if (body == null || forces == null || regionsDB == null) return;
            var game = body.Manager?.Game;
            if (game?.Factions == null) return;

            try
            {
                // 1) FORM UP loose AI units into battalions (reuse G2.1) so the brain has hands to command. Idempotent
                //    (a formed unit is skipped), NPC-only (the player forms their own units in the UI), gated by the
                //    caller's flag → byte-identical when off.
                var npcLooseFactions = new HashSet<int>();
                foreach (var u in forces.Units)
                    if (u != null && u.FormationId < 0 && IsNpcFaction(game, u.FactionOwnerID))
                        npcLooseFactions.Add(u.FactionOwnerID);
                foreach (var fid in npcLooseFactions)
                    GroundAssembly.FormUpLoose(body, fid);

                var catalog = game.StartingGameData?.GroundStances;

                // 2) DRIVE each AI-owned battalion. Snapshot the list — form-up above may have added to it.
                foreach (var formation in forces.Formations.ToArray())
                {
                    if (formation == null) continue;
                    if (!TryGetNpcFaction(game, formation.FactionOwnerID, out var factionEntity, out var fi)) continue;
                    if (FormationHasPlayerOrder(formation)) continue;   // §3.5 — a player order queue always overrides

                    var ctx = BuildContext(body, forces, regionsDB, formation, factionEntity, fi);
                    var posture = GroundTactics.DecidePosture(ctx);
                    // Audit M2 — the fog-honest own/enemy odds ratio the posture hysteresis is measured from (999 = no
                    // detected enemy). Passed into ApplyStance so a stance holds against tick-to-tick jitter.
                    double oddsRatio = ctx.EnemyStrength > 1e-6 ? ctx.OwnStrength / ctx.EnemyStrength : 999.0;
                    ApplyPosture(body, forces, formation, posture, catalog, now, oddsRatio);

                    formation.TacticalReason = posture.Reason;   // the AI-tape explain (client reads it)
                    formation.TacticalIntent = posture.Intent;
                }
            }
            catch { /* the brain is a convenience layer — never break the ground hotloop over it (L4) */ }
        }

        // ───────────────────────── context assembly ─────────────────────────

        private static GroundTacticsContext BuildContext(Entity body, GroundForcesDB forces, PlanetRegionsDB regionsDB,
            GroundFormation formation, Entity factionEntity, FactionInfoDB fi)
        {
            int factionId = formation.FactionOwnerID;
            int region = GroundForces.LeaderRegion(forces, formation);

            var ctx = new GroundTacticsContext
            {
                OwnStrength = GroundFormationTools.FormationStrength(forces, formation),
                EnemyStrength = GroundThreat.DetectedEnemyStrength(forces, regionsDB, factionId, region),
                RiskTrait = TraitOf(factionEntity, PersonalityTrait.Risk),
                AggressionTrait = TraitOf(factionEntity, PersonalityTrait.Aggression),
                IsHomelandDefender = IsHomelandOf(body, factionId),
                HasOrbitalSupport = HasOrbitalSupport(body, factionId),
                Blind = GroundThreat.IsBlind(body, factionId, region),
            };

            // Fortification + terrain of the battalion's region (only meaningful when the battalion HOLDS it).
            if (region >= 0 && region < regionsDB.Regions.Count)
            {
                var reg = regionsDB.Regions[region];
                var terrain = GroundTerrain.Classify(reg);
                ctx.DefensibleTerrain = GroundTerrain.CoverDefenseMult(terrain) > 1.0;
                ctx.FortificationMult = (reg.OwnerFactionID == factionId)
                    ? GroundFortification.DefenseMult(reg, regionsDB.Regions, factionId, GroundFortification.BuildResolver(body))
                    : 1.0;
            }
            else ctx.FortificationMult = 1.0;

            // Ammo: worst-case fraction across the battalion's ammo-fed members (dry ⇒ never Offensive).
            (ctx.HasAmmoWeapons, ctx.AmmoFraction) = BattalionAmmo(forces, formation);

            // Reserve: does the faction still hold its home-defense reserve on this body (a healthy rear)?
            ctx.ReserveIntact = GroundReinforcement.CurrentGarrison(forces, factionId)
                                >= GroundReinforcement.GarrisonReserveFor(fi, factionEntity);

            // Advance target: the nearest adjacent enemy-held / un-held region — but ONLY once the current region is
            // clear of live enemies (you clear before you march past a foe that's shooting you).
            double enemyHere = GroundThreat.EnemyStrengthInRegion(forces, region, factionId);
            if (enemyHere <= 0.0)
                (ctx.HasAdvanceTarget, ctx.AdvanceRegion) = FindAdvanceTarget(regionsDB, factionId, region);
            else { ctx.HasAdvanceTarget = false; ctx.AdvanceRegion = -1; }

            // Fallback: the nearest adjacent friendly-held region (the line of retreat / beachhead rally).
            (ctx.HasFallback, ctx.FallbackRegion) = FindFallback(regionsDB, factionId, region);

            return ctx;
        }

        /// <summary>The lowest-index adjacent region NOT owned by the faction — an enemy-held or un-held region to take.
        /// v1 is one hop at a time (re-evaluated each tick); a live-enemy own-region gates this off in BuildContext.</summary>
        private static (bool has, int region) FindAdvanceTarget(PlanetRegionsDB regionsDB, int factionId, int region)
        {
            if (region < 0 || region >= regionsDB.Regions.Count) return (false, -1);
            int best = -1;
            foreach (var n in regionsDB.Regions[region].Neighbors)
            {
                if (n < 0 || n >= regionsDB.Regions.Count) continue;
                if (regionsDB.Regions[n].OwnerFactionID == factionId) continue;   // already mine → not a target
                if (best < 0 || n < best) best = n;                              // deterministic: lowest index
            }
            return best >= 0 ? (true, best) : (false, -1);
        }

        /// <summary>The lowest-index adjacent region OWNED by the faction — the friendly ground to fall back onto (v1
        /// treats a friendly-held region as the beachhead rally; a multi-hop nearest-friendly BFS is a documented
        /// refinement). No adjacent friendly region ⇒ nowhere to run ⇒ the brain digs in (cornered).</summary>
        private static (bool has, int region) FindFallback(PlanetRegionsDB regionsDB, int factionId, int region)
        {
            if (region < 0 || region >= regionsDB.Regions.Count) return (false, -1);
            int best = -1;
            foreach (var n in regionsDB.Regions[region].Neighbors)
            {
                if (n < 0 || n >= regionsDB.Regions.Count) continue;
                if (regionsDB.Regions[n].OwnerFactionID != factionId) continue;   // must be my own ground
                if (best < 0 || n < best) best = n;
            }
            return best >= 0 ? (true, best) : (false, -1);
        }

        /// <summary>Whether the battalion fields ANY ammo-fed weapon, and its WORST-case ammo fraction (0..1; 1 if it
        /// fields none). A dry battalion (fraction ≤ threshold) is barred from Offensive.</summary>
        private static (bool hasAmmo, double fraction) BattalionAmmo(GroundForcesDB forces, GroundFormation formation)
        {
            bool has = false;
            double worst = 1.0;
            foreach (var u in GroundFormationTools.MembersOf(forces, formation))
            {
                if (u.Health <= 0 || u.MaxAmmo_kg <= 0) continue;
                has = true;
                double frac = u.CurrentAmmo_kg / u.MaxAmmo_kg;
                if (frac < worst) worst = frac;
            }
            return has ? (true, worst) : (false, 1.0);
        }

        // ───────────────────────── applying the decision ─────────────────────────

        private static void ApplyPosture(Entity body, GroundForcesDB forces, GroundFormation formation,
            GroundPosture posture, IReadOnlyDictionary<string, GroundStanceBlueprint> catalog, DateTime now, double oddsRatio)
        {
            ApplyStance(formation, posture.StanceFamily, catalog, now, posture.BreakGlass, oddsRatio);
            GroundFormationDoctrine.SetEngagementStance(formation, posture.Roe);

            switch (posture.Intent)
            {
                case GroundIntent.Advance:
                case GroundIntent.PullBack:
                case GroundIntent.Retreat:
                    if (posture.MoveTargetRegion >= 0) EnsureAiMove(formation, posture.MoveTargetRegion);
                    else ClearAiOrders(formation);
                    break;

                default:   // Hold — stand and fight / probe; cancel any pending AI move so the ROE micro takes over
                    ClearAiOrders(formation);
                    break;
            }
        }

        /// <summary>Queue a MoveRegion order marked <see cref="GroundOrderIssuer.Ai"/> — UNLESS the same AI move is
        /// already the front order (don't thrash an in-progress march). Replaces the queue (which holds only AI/empty
        /// orders here — a player-order battalion was skipped upstream).</summary>
        private static void EnsureAiMove(GroundFormation formation, int targetRegion)
        {
            if (formation.Orders != null && formation.Orders.Count > 0)
            {
                var front = formation.Orders[0];
                if (front.Issuer == GroundOrderIssuer.Ai && front.Type == GroundOrderType.MoveToRegion
                    && front.TargetRegion == targetRegion)
                    return;   // already advancing/retreating to this region — let it run
            }
            var order = GroundOrder.MoveRegion(targetRegion);
            order.Issuer = GroundOrderIssuer.Ai;
            GroundForces.SetFormationOrder(formation, order);
        }

        /// <summary>Cancel the battalion's AI order plan (its queue holds only AI/empty orders here) so the ROE
        /// auto-maneuver takes over for a Hold.</summary>
        private static void ClearAiOrders(GroundFormation formation)
        {
            if (formation.Orders != null && formation.Orders.Count > 0)
                GroundForces.ClearFormationOrders(formation);
        }

        /// <summary>Set the battalion's stance to the named FAMILY. Normal switches go through
        /// <c>TrySetStance</c> (which respects the switch cooldown — that IS the posture hysteresis). A
        /// <paramref name="breakGlass"/> survival shift bypasses the cooldown (set directly), so a unit under sudden
        /// pressure can dig in / retreat IMMEDIATELY — the design's "no time-lock without a release." No-op if already
        /// on that family or the catalog lacks it.</summary>
        private static void ApplyStance(GroundFormation formation, string family,
            IReadOnlyDictionary<string, GroundStanceBlueprint> catalog, DateTime now, bool breakGlass, double oddsRatio)
        {
            if (string.IsNullOrEmpty(family) || catalog == null) return;
            if (string.Equals(formation.StanceFamily, family, StringComparison.OrdinalIgnoreCase)) return;   // already there
            var bp = FindStanceByFamily(catalog, family);
            if (bp == null) return;

            if (breakGlass)
            {
                // Bypass BOTH the cooldown and the hysteresis hold — a survival shift must never be time-locked (the
                // P3.3 lesson at design time). Stamp the hold clock so the next non-survival change respects the hold.
                formation.StanceId = bp.UniqueID;
                formation.StanceFamily = bp.Family;
                formation.AttackMult = bp.AttackMult;
                formation.DamageTakenMult = bp.DamageTakenMult;
                formation.SwitchableAfter = now + TimeSpan.FromSeconds(bp.CooldownSeconds);
                formation.LastStanceChange = now;
                formation.LastStanceOdds = oddsRatio;
                return;
            }

            // Audit M2 — posture hysteresis: once a stance is set, HOLD it against tick-to-tick jitter. The stance
            // catalog's 60-300s cooldown always expires before the hourly ground tick, so it never actually binds —
            // THIS is the real hysteresis. A non-survival change is suppressed until MinHoldHours have elapsed OR the
            // odds moved past the band (a genuine swing still turns the line). First assignment (empty family) always
            // passes — there's no hold reference yet — and LastStanceChange=MinValue makes the first change pass too.
            if (GroundTactics.ShouldHoldStance(!string.IsNullOrEmpty(formation.StanceFamily),
                    formation.LastStanceChange, formation.LastStanceOdds, oddsRatio, now))
                return;   // hold the current stance — don't flip on tick-to-tick jitter

            if (GroundFormationDoctrine.TrySetStance(formation, bp, now))   // honours the switch cooldown; true = changed
            {
                formation.LastStanceChange = now;
                formation.LastStanceOdds = oddsRatio;
            }
        }

        private static GroundStanceBlueprint FindStanceByFamily(IReadOnlyDictionary<string, GroundStanceBlueprint> catalog, string family)
        {
            foreach (var bp in catalog.Values)
                if (bp != null && string.Equals(bp.Family, family, StringComparison.OrdinalIgnoreCase)) return bp;
            return null;
        }

        // ───────────────────────── reads ─────────────────────────

        /// <summary>True if the battalion holds ANY player-issued order — the brain then leaves it entirely alone.</summary>
        private static bool FormationHasPlayerOrder(GroundFormation formation)
        {
            if (formation.Orders == null) return false;
            foreach (var o in formation.Orders)
                if (o != null && o.Issuer == GroundOrderIssuer.Player) return true;
            return false;
        }

        private static bool IsNpcFaction(Game game, int factionId)
            => TryGetNpcFaction(game, factionId, out _, out _);

        private static bool TryGetNpcFaction(Game game, int factionId, out Entity factionEntity, out FactionInfoDB fi)
        {
            factionEntity = null; fi = null;
            if (game?.Factions == null || !game.Factions.TryGetValue(factionId, out var ent) || ent == null) return false;
            if (!ent.TryGetDataBlob<FactionInfoDB>(out var info) || !info.IsNPC) return false;
            factionEntity = ent; fi = info;
            return true;
        }

        private static double TraitOf(Entity factionEntity, PersonalityTrait trait)
            => (factionEntity != null && factionEntity.TryGetDataBlob<PersonalityDB>(out var p))
               ? p.TraitOf(trait) : PersonalityDB.Neutral;

        /// <summary>Does <paramref name="body"/> host a colony owned by <paramref name="factionId"/> — i.e. is this the
        /// faction's own homeland (bias defensive)? Read-only over the body's manager.</summary>
        private static bool IsHomelandOf(Entity body, int factionId)
        {
            var mgr = body?.Manager;
            if (mgr == null) return false;
            foreach (var colony in mgr.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                if (colony.FactionOwnerID == factionId
                    && colony.TryGetDataBlob<ColonyInfoDB>(out var ci)
                    && ci.PlanetEntity != null && ci.PlanetEntity.Id == body.Id)
                    return true;
            return false;
        }

        /// <summary>Do the faction's own warships hold the orbit above <paramref name="body"/> (bombard-then-advance
        /// support)? Reuses the transport orbital-control read: a friendly ship AT the body that isn't contested by a
        /// foreign ship. Defensive/no-throw.</summary>
        private static bool HasOrbitalSupport(Entity body, int factionId)
        {
            var mgr = body?.Manager;
            if (mgr == null) return false;
            foreach (var ship in mgr.GetAllEntitiesWithDataBlob<ShipInfoDB>())
            {
                if (ship.FactionOwnerID != factionId) continue;
                if (GroundTransport.ShipIsAtBody(ship, body))
                    return GroundTransport.HasOrbitalControl(ship, body);   // a friendly ship here that holds the orbit
            }
            return false;
        }
    }
}
