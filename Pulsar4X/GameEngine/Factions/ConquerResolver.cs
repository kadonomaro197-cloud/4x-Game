using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Industry;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the CONQUER resolver — the Ambition-tier "go to war for
    /// gain" brain (chosen when the faction is dominant, secure, and aggressive). It had NO resolver and no-oped; this
    /// is the SIXTH and last objective to get one, so every strategic objective now resolves.
    ///
    /// v1 does the FIRST rung only: BUILD UP a war fleet (queue an armed hull on a free ship-construction line — the
    /// same lever <see cref="DefendResolver"/>'s Rung A pulls). You cannot conquer without a fleet, so massing one is
    /// step one. The rest of the chain — picking WHICH rival to hit, checking you can physically REACH it, fuelling and
    /// charging the built ships (they spawn empty), composing the strike group, and the STRIKE itself — is the deferred
    /// P-3 military sub-subsystem (needs rival factions to exercise and three new helpers: MilitaryTarget /
    /// MilitaryComposition / MilitaryReach, plus a multi-jump auto-router). A deliberate, labelled deferral, not a gap.
    ///
    /// One step per monthly cycle. Pure decision (builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c>
    /// runs it). Byte-identical while order emission is off.
    /// </summary>
    public sealed class ConquerResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.Conquer;

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            // The scored enemy target (a colony of a faction we're at war with), computed ONCE and reused by every war
            // rung below. Invalid for any faction not at war → all the war rungs skip and we fall to the mass-fleet
            // build, so a default (peacetime) game is byte-identical.
            var target = MilitaryTarget.BestEnemyTarget(state.Faction);

            // Rung 0 (B5-b — LAND the invasion, the CULMINATION): if we own a transport that is AT the target world,
            // still CARRYING troops, and HOLDS the orbit there (no foreign ship over it — the space fight is won),
            // land a unit into region 0 (the enemy capital). This is the highest-priority rung on purpose: once boots
            // can hit the ground, nothing the resolver could do matters more — a landed unit drops straight into the
            // region's fight, and GroundForcesProcessor flips the region owner (then the whole planet) when the garrison
            // is cleared, which IS "take a planet". Placed FIRST so a loaded transport that has ridden the warfleet's
            // won orbit lands at once, before the resolver considers sailing/loading/building anything else. The front
            // door is LandTroopsOrder → GroundTransport.TryLandUnit, which re-checks at-body + orbital control inside the
            // helper (defensive). Byte-identical while emission is off (runs only inside the gated EmitOrders) AND for
            // any faction not at war (no target → skipped).
            if (target.IsValid && state.Game != null)
            {
                var (landShip, landUnit) = FindLandableTransport(state, target.ColonyBody);
                if (landShip != null && landUnit != null)
                {
                    var game = state.Game;
                    var s = landShip;
                    var body = target.ColonyBody;
                    int uid = landUnit.UnitId;
                    string uname = landUnit.Name;
                    return new PlannerAction(
                        "LandInvasion",
                        $"land '{uname}' from transport {s.Id} onto enemy world {body.Id} region 0",
                        () =>
                        {
                            var cmd = Pulsar4X.GroundCombat.LandTroopsOrder.CreateCommand(s, body, uid, 0);
                            game.OrderHandler.HandleOrder(cmd);   // the ONE step (lands the unit into the region fight)
                        });
                }
            }

            // Rung 1 (P-3 REACH — the STRIKE, 2026-07-12): if we hold a scored enemy target (a colony of a faction
            // we're at war with, MilitaryTarget) AND a MASSED strike fleet (MilitaryComposition) that isn't already
            // sailing AND that fleet can actually GET THERE (MilitaryReach), order that fleet to sail at the enemy
            // world. This is the "do" that turns the coalition into a war: 3.4b DECLARES war → MilitaryTarget SCORES
            // the world → MilitaryComposition confirms a mass fleet → MilitaryReach confirms the fleet can REACH it →
            // here we SAIL it. Reuses the player MoveToSystemBodyOrder, which guards the warp landmines (skips
            // 0-speed ships; the order handler try/catches). Byte-identical while order emission is off (this whole
            // resolver runs only inside the gated EmitOrders), AND for any faction not at war (no target → skip).
            if (target.IsValid && state.Game != null)
            {
                var strikeFleet = MilitaryComposition.ReadyStrikeFleet(state);
                // REACH GATE: this SAME-SYSTEM rung sails only when the target is a DIRECT (same-system) warp the fleet
                // has the fuel/range for. MoveToSystemBodyOrder warps within one system — it can't cross a jump — so a
                // cross-system target is handled by the MULTI-JUMP STRIKE ROUTING rung below (Rung 1b), which sails the
                // fleet one gate at a time along a discovered route. A drained/drive-less fleet or an UNREACHABLE target
                // (no discovered route) falls through to the build rung and keeps massing until a route/range exists.
                var reach = strikeFleet != null && strikeFleet.IsValid
                    ? MilitaryReach.Assess(strikeFleet, target.ColonyBody)
                    : MilitaryReach.ReachResult.None;
                // FIGHT-or-FLEE (Phase A-1): commit the fleet ONLY if the odds meet this faction's RISK appetite.
                // CombatRisk reads own strength vs a fog-limited estimate of the enemy and its Risk trait (a bold
                // faction engages at parity, a cautious one demands 2× the enemy). An UNDETECTED enemy (no sensor
                // contacts → estimate 0) always clears, so a faction that can't yet SEE the defender still strikes —
                // graceful, and it keeps the existing MilitaryCompositionTests byte-identical (their rival is
                // undetected). If the odds are bad, the strike falls through to the build rungs (keep massing).
                // NOTE (Phase A-3): the two strength reads are not yet in the SAME units (own = combat-value,
                // enemy = detected signal-kW) — this wires the fight/flee LEVER; the calibration is the A-3 tuning pass.
                int enemyFactionId = target.Colony != null && target.Colony.IsValid ? target.Colony.FactionOwnerID : Game.NeutralFactionId;
                bool oddsFavorAttack = CombatRisk.WouldEngage(state.Faction, enemyFactionId);
                // RESERVE GUARD (slice 3): the AI never sends its LAST organized combat fleet on an offensive — a
                // home-defense reserve must remain (HasHomeReserve). The developer's "don't pull all fleets at once";
                // a placeholder for the system military-commander delegate that will own the commit-vs-reserve call.
                if (oddsFavorAttack && strikeFleet != null && strikeFleet.IsValid && !FleetIsMoving(strikeFleet)
                    && reach.Tier == MilitaryReach.ReachTier.SameSystem && reach.HasRange
                    && HasHomeReserve(state, strikeFleet))
                {
                    var game = state.Game;
                    int factionId = state.FactionId;
                    var fleet = strikeFleet;
                    var body = target.ColonyBody;
                    return new PlannerAction(
                        "StrikeFleet",
                        $"sail strike fleet {fleet.Id} at enemy world {body.Id} (target score {target.Score:F0})",
                        () =>
                        {
                            var cmd = Pulsar4X.Movement.MoveToSystemBodyOrder.CreateCommand(factionId, fleet, body);
                            game.OrderHandler.HandleOrder(cmd);   // the ONE step (the only side effect)
                        });
                }

                // Rung 1b (MULTI-JUMP STRIKE ROUTING, 2026-07-17): the scored target is in ANOTHER star system, but the
                // faction has a DISCOVERED jump route to it (OneJump or MultiJump — JumpRouter walked the discovered
                // jump graph). Sail the NEXT LEG toward it: order the massed fleet through the FIRST gate on that route,
                // one gate per monthly cycle (least-commitment, matching the resolver's one-step-per-cycle style).
                // JumpOrder is the player's own fleet-jump lever — it warps each ship to the gate (if not already there)
                // then transits it through — so a single order advances one leg; next cycle the fleet re-routes from the
                // new system until the target is same-system (then the StrikeFleet rung above sails at the body). Same
                // commit gates as the same-system strike (odds favour it, fleet massed + charged + not already moving,
                // a home reserve remains). No discovered route (Unreachable) or no resolvable gate → falls through to
                // the build rungs (keep massing), exactly as before. Byte-identical while order emission is off (this
                // resolver runs only inside the gated EmitOrders), for any faction not at war (no target → skip), AND
                // for a same-system target (this block requires Tier != SameSystem, so it never fires there).
                if (oddsFavorAttack && strikeFleet != null && strikeFleet.IsValid && !FleetIsMoving(strikeFleet)
                    && reach.IsReachable && reach.Tier != MilitaryReach.ReachTier.SameSystem
                    && reach.HasRange && HasHomeReserve(state, strikeFleet))
                {
                    var fromSystem = strikeFleet.Manager;
                    Entity nextGate = Entity.InvalidEntity;
                    if (fromSystem != null)
                        nextGate = JumpRouter.NextGateToward(fromSystem, target.ColonyBody.Manager, state.FactionId);
                    // A resolvable gate in the fleet's current system to head for (InvalidEntity → no leg to emit this
                    // cycle; fall through to the build rungs).
                    if (nextGate != null && nextGate.IsValid
                        && nextGate.TryGetDataBlob<Pulsar4X.JumpPoints.JumpPointDB>(out var gateDB))
                    {
                        var game = state.Game;
                        var factionEntity = state.Faction;
                        var fleet = strikeFleet;
                        var db = gateDB;
                        return new PlannerAction(
                            "StrikeJump",
                            $"jump strike fleet {fleet.Id} through gate {nextGate.Id} toward enemy world "
                                + $"{target.ColonyBody.Id} ({reach.Hops} hops, target score {target.Score:F0})",
                            () =>
                            {
                                // The ONE step (the only side effect): warp-to-gate + transit through, as a fleet.
                                Pulsar4X.JumpPoints.JumpOrder.CreateAndExecute(game, factionEntity, fleet, db);
                            });
                    }
                }
            }

            // Rung 1.3 (B5-a — SAIL the invasion): we hold a war target and own a transport that is CARRYING troops but
            // is NOT yet at the target world → sail it there. Placed AFTER the strike-fleet sail (the warfleet launches
            // first to win the orbit) and BEFORE the LOAD rung (a loaded transport should move out before we load the
            // next one). Uses the per-ship WarpMoveCommand directly (the exact per-ship warp MoveToSystemBodyOrder
            // issues internally) — NOT MoveToSystemBodyOrder, which bails on anything that isn't a FLEET, and a lone
            // freshly-loaded transport isn't in one. The transport arrives in orbit and waits; the LAND rung (Rung 0)
            // fires once it's at the target AND holds the orbit (the warfleet having cleared it). Gated on a real warp
            // drive (MaxSpeed>0) and not-already-warping so it can't spam a no-op sail. Byte-identical while emission is
            // off (gated EmitOrders) AND for any faction not at war (no target → skipped).
            if (target.IsValid && state.Game != null)
            {
                var sailShip = FindSailableTransport(state, target.ColonyBody);
                if (sailShip != null)
                {
                    var game = state.Game;
                    var ship = sailShip;
                    var body = target.ColonyBody;
                    return new PlannerAction(
                        "SailTransport",
                        $"sail loaded transport {ship.Id} toward enemy world {body.Id}",
                        () =>
                        {
                            var cmd = Pulsar4X.Movement.WarpMoveCommand.CreateCommandEZ(ship, body, ship.StarSysDateTime);
                            game.OrderHandler.HandleOrder(cmd);   // the ONE step (launch the transport at the target)
                        });
                }
            }

            // Rung 1.5 (B5-3 — LOAD the invasion): we hold a war target and own a transport that is sitting AT one of our
            // own bodies which has a standing ground unit, with free troop-bay room → order that unit loaded (the front
            // door to LoadTroopsOrder → GroundTransport.TryLoadUnit). Placed AFTER the strike-fleet sail (so the warfleet
            // launches to win the orbit first) and BEFORE BuildTransport (which only fires when we own NO transport, so
            // the two are mutually exclusive). Loading a garrison unit strips it off the home world to carry the invasion
            // — the aggressive "throw the army at them" v1 (a reserve-vs-expedition split is a later refinement). Sailing
            // the loaded transport to the target + landing it are the next B5-3 rungs. Byte-identical while emission is off
            // (runs only inside the gated EmitOrders), AND for any faction not at war (no target → skipped).
            if (target.IsValid && state.Game != null)
            {
                var transport = FindOwnedTransport(state);
                if (transport != null && transport.IsValid)
                {
                    var shipBody = ShipBody(transport);
                    // CanLoad-filtered: only a unit a bay of its own class has room for (skips the Vehicle armour/artillery
                    // a Personnel-only trooper can't carry — the anti-no-op-loop guard the blast-radius check surfaced).
                    var unit = shipBody != null ? AvailableLoadableUnit(transport, shipBody, state.FactionId) : null;
                    // GROUND RESERVE GUARD (slice 3, the ground echo of HasHomeReserve): don't ship a garrison unit off
                    // if loading it would drop this world's garrison below its home-defense RESERVE floor — the developer's
                    // "a planetary garrison shouldn't ship its WHOLE defense off on an invasion." A world with a SURPLUS
                    // above the reserve still loads; one already at/under the reserve holds its defenders (the rung falls
                    // through to BuildTransport / the garrison-rebuild rung / mass). Byte-identical where there's no
                    // garrison: WouldStripReserve is consulted only when a loadable unit exists, and a garrison-less world
                    // never reaches here (unit == null → the whole condition short-circuits false, exactly as before).
                    if (unit != null
                        && !Pulsar4X.GroundCombat.GroundReinforcement.WouldStripReserve(shipBody, state.Info, state.FactionId))
                    {
                        var game = state.Game;
                        var t = transport;
                        var b = shipBody;
                        int uid = unit.UnitId;
                        string uname = unit.Name;
                        return new PlannerAction(
                            "LoadInvasion",
                            $"load '{uname}' aboard transport {t.Id} at body {b.Id} to carry the invasion",
                            () =>
                            {
                                var cmd = Pulsar4X.GroundCombat.LoadTroopsOrder.CreateCommand(t, b, uid);
                                game.OrderHandler.HandleOrder(cmd);   // the ONE step
                            });
                    }
                }
            }

            // Rung 2 (B5-2 — the invasion CARRIER): we hold a war target but own no ship that can LIFT troops → build a
            // troop transport. Placed AFTER the sail rung (a ready/charged/reachable fleet still sails FIRST, so
            // MilitaryCompositionTests stays byte-identical) and BEFORE massing more warships. It only runs when Rung 1
            // did NOT (no ready fleet, or the fleet is already en route). One transport (repeat: false — not a standing
            // mass); the load/land steps are B5-3. Byte-identical while EnableOrderEmission is off, and for any faction
            // not at war (no target → this whole block is skipped, so a default game still hits QueueWarship below).
            if (target.IsValid && !FactionOwnsTransport(state))
            {
                foreach (var colony in state.ColoniesWithFreeLine())
                {
                    if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                    foreach (var designKvp in state.Info.IndustryDesigns)
                    {
                        if (!(designKvp.Value is ShipDesign transport) || !IsTroopTransport(transport)) continue;
                        if (!FeasibilityOracle.CanQueue(colony, transport, state.Info)) continue;

                        string tLineId = FreeLineFor(colony.Industry, transport.IndustryTypeID);
                        if (tLineId == null) continue;

                        var colonyEntity = colony.Colony;
                        var info = state.Info;
                        var designId = designKvp.Key;
                        var designName = transport.Name;
                        return new PlannerAction(
                            "BuildTransport",
                            $"build troop transport '{designName}' on colony {colonyEntity.Id} to carry the invasion",
                            () =>
                            {
                                var job = new IndustryJob(info, designId);
                                job.InitialiseJob(1, false);                     // ONE transport, not a standing mass
                                IndustryTools.AddJob(colonyEntity, tLineId, job);
                                IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                            });
                    }
                }
            }

            // Rung 2.5 (GROUND REINFORCEMENT — the garrison echo of the warship-mass rung, Rung 3): a home world whose
            // garrison has been ground BELOW its authored composition target (combat losses, or units shipped off on an
            // invasion) → REBUILD it. Queues ONE buildable ground-unit design on a free line — the SAME industry lever
            // the transport/warship rungs pull (a base-mod infantry/armor/artillery component that mounts PlanetInstallation
            // → build auto-installs → raises a fielded unit, or an assembler-registered GroundUnitDesign). The developer's
            // rule: "a depleted garrison should be REBUILT" (and, with the LOAD-rung reserve guard above, "don't ship your
            // whole defense off"). Placed BELOW the invasion EXECUTION rungs (LAND/STRIKE/SAIL/LOAD/BuildTransport) but
            // ABOVE massing MORE offensive warships — restore the home defense before expanding the offense. Fires ONLY
            // for a world we ACTUALLY garrison (NeedsReinforcement requires ≥1 standing friendly unit), so a garrison-less
            // faction/world (the default player, a station-only faction, a bare test colony with no GroundForcesDB) is a
            // NO-OP → byte-identical, incl. the ConquerResolver/MilitaryComposition byte-identity tests (whose factions
            // carry no home garrison). Byte-identical off (this whole resolver runs inside the gated EmitOrders). The
            // reserve/target derive from the faction's GarrisonComposition, so this needs NO new data.
            foreach (var colony in state.ColoniesWithFreeLine())
            {
                if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB
                var garrisonBody = Pulsar4X.GroundCombat.GroundReinforcement.GarrisonBodyOf(colony.Colony);
                if (garrisonBody == null
                    || !Pulsar4X.GroundCombat.GroundReinforcement.NeedsReinforcement(garrisonBody, state.Info, state.FactionId))
                    continue;   // no garrison here, or it's already at strength → nothing to rebuild on this world

                foreach (var designKvp in state.Info.IndustryDesigns)
                {
                    if (!Pulsar4X.GroundCombat.GroundReinforcement.IsBuildableGroundUnit(designKvp.Value)) continue;
                    if (!FeasibilityOracle.CanQueue(colony, designKvp.Value, state.Info)) continue;

                    string gLineId = FreeLineFor(colony.Industry, designKvp.Value.IndustryTypeID);
                    if (gLineId == null) continue;

                    var colonyEntity = colony.Colony;
                    var info = state.Info;
                    var designId = designKvp.Key;
                    var designName = designKvp.Value.Name;
                    return new PlannerAction(
                        "RebuildGarrison",
                        $"build ground unit '{designName}' on colony {colonyEntity.Id} to rebuild its depleted home garrison",
                        () =>
                        {
                            var job = new IndustryJob(info, designId);
                            job.InitialiseJob(1, false);                     // ONE unit toward the target (not a standing line)
                            IndustryTools.AddJob(colonyEntity, gLineId, job);
                            IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                        });
                }
            }

            // Rung 3 (v1 + Phase A-2b — MASS the strike fleet, BEST not first): queue an armed hull on a free
            // ship-construction line. (Reached while we have no target yet, or the strike group isn't massed / is
            // already en route.) Instead of taking the FIRST buildable warship in dictionary order, it now SCORES the
            // buildable warships with the shared DecisionScorer and masses the highest — so the AI builds its strongest
            // hull, not whatever happens to be listed first (the developer's "best not first"). v1 features a warship
            // only on MilitarySolve = its weapon-mount count (a coarse firepower proxy) → the pick is "most-armed", the
            // same for every faction; the PERSONALITY-differentiated ship choice (a bold faction's evasive glass-cannon
            // vs a cautious faction's armoured brawler) needs per-design combat values (firepower/evasion/toughness),
            // which only exist on a BUILT ship — that richer featurization is the flagged tuning refinement. Byte-
            // identical while EnableOrderEmission is off (this whole resolver is gated).
            //
            // Slice 3 — the RESOURCE-GATED, RESERVE-AWARE CAP: stop massing once the faction has a COMPLETE strike fleet
            // (grown to the size its treasury + war footing warrant) AND a second deployable fleet held back as a home
            // RESERVE. So a poor faction sizes at the Deployable core + a reserve; a wealthy/at-war one grows to Ideal/
            // Perfect + a reserve; and it never stops at a single all-in fleet ("don't commit everything"). No forming
            // fleet yet (nothing assembled) → no cap → mass the first fleet as before, so the ConquerResolver byte-
            // identity tests (which drive the resolver with no FleetCompositionDB present) stay green.
            if (ShouldStopMassing(state)) return PlannerAction.None;

            foreach (var colony in state.ColoniesWithFreeLine())
            {
                if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                // Gather the warships THIS colony can actually build right now (each with its free line).
                var buildable = new List<(ShipDesign design, string key, string line)>();
                foreach (var designKvp in state.Info.IndustryDesigns)
                {
                    if (!(designKvp.Value is ShipDesign ship) || !IsWarship(ship)) continue;
                    if (!FeasibilityOracle.CanQueue(colony, ship, state.Info)) continue;
                    string lineId = FreeLineFor(colony.Industry, ship.IndustryTypeID);
                    if (lineId == null) continue;
                    buildable.Add((ship, designKvp.Key, lineId));
                }
                if (buildable.Count == 0) continue;

                // BEST not first: score the buildable warships by this faction's personality and mass the winner.
                var candidates = new List<ShipDesign>(buildable.Count);
                foreach (var b in buildable) candidates.Add(b.design);
                var personality = state.Faction != null && state.Faction.TryGetDataBlob<PersonalityDB>(out var p) ? p : null;
                var chosen = PickWarship(candidates, personality);
                if (chosen == null) continue;

                string chosenKey = null, chosenLine = null, chosenName = null;
                foreach (var b in buildable)
                    if (ReferenceEquals(b.design, chosen)) { chosenKey = b.key; chosenLine = b.line; chosenName = b.design.Name; }
                if (chosenKey == null) continue;   // defensive (should never miss — chosen came from buildable)

                var colonyEntity = colony.Colony;
                var info = state.Info;
                return new PlannerAction(
                    "QueueWarship",
                    $"build '{chosenName}' (best of {buildable.Count}) on colony {colonyEntity.Id} to mass a strike fleet",
                    () =>
                    {
                        var job = new IndustryJob(info, chosenKey);
                        job.InitialiseJob(1, true);                    // repeat: keep massing the fleet
                        IndustryTools.AddJob(colonyEntity, chosenLine, job);
                        IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                    });
            }

            return PlannerAction.None;
        }

        /// <summary>A ship design is a WARSHIP if any component design it mounts carries a weapon attribute. (Mirrors
        /// <see cref="DefendResolver"/>'s helper — a later MilitaryComposition slice can extract the shared build.)
        /// Internal for the Phase A-2b gauge.</summary>
        internal static bool IsWarship(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<RailgunWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<FlakWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<MissileLauncherAtb>(out _);

        /// <summary>The number of WEAPON MOUNTS a design carries (summed across every weapon type) — the coarse
        /// firepower proxy the Phase A-2b <see cref="PickWarship"/> scores a warship on (MilitarySolve). A per-design
        /// proxy because true firepower (the built <c>ShipCombatValueDB</c>) needs a built ship; richer featurization is
        /// the flagged tuning refinement. Internal for the gauge.</summary>
        internal static int WeaponMountCount(ShipDesign ship)
        {
            int n = 0;
            if (ship.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out var a)) foreach (var c in a) n += c.count;
            if (ship.TryGetComponentsByAttribute<RailgunWeaponAtb>(out var b)) foreach (var c in b) n += c.count;
            if (ship.TryGetComponentsByAttribute<FlakWeaponAtb>(out var d)) foreach (var c in d) n += c.count;
            if (ship.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out var e)) foreach (var c in e) n += c.count;
            if (ship.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out var f)) foreach (var c in f) n += c.count;
            if (ship.TryGetComponentsByAttribute<MissileLauncherAtb>(out var g)) foreach (var c in g) n += c.count;
            return n;
        }

        /// <summary>Slice 3 (reserve-aware) — TRUE when the faction has built ENOUGH: a COMPLETE strike fleet (a forming
        /// fleet at/above its aspiration target) AND a second DEPLOYABLE fleet to keep home as a RESERVE. Only then does
        /// the massing rung stop building hulls — so the AI keeps building PAST one fleet (the developer's "don't commit
        /// everything; keep a reserve" — the home-defense fleet the system military commander holds back). No forming
        /// fleet (no <see cref="Pulsar4X.Fleets.FleetCompositionDB"/>) → false (mass to create the first, byte-identical
        /// for the ConquerResolver tests which drive the resolver with no such blob). Reads the same
        /// <see cref="FactionState.OwnedFleets"/> the strike rung uses.</summary>
        private static bool ShouldStopMassing(FactionState state)
        {
            int completeStrikeFleets = 0;   // forming fleets at/above their aspiration target (a finished strike group)
            int deployableFleets = 0;       // forming fleets at/above the deploy core (a candidate strike OR reserve)
            foreach (var fleet in state.OwnedFleets())
            {
                if (!fleet.TryGetDataBlob<Pulsar4X.Fleets.FleetCompositionDB>(out var comp)) continue;
                int armed = MilitaryComposition.WarshipCount(fleet);
                int target = comp.Template.TargetCountFor(comp.Aspiration);
                if (target > 0 && armed >= target) completeStrikeFleets++;
                if (armed >= comp.MinToDeploy)      deployableFleets++;
            }
            return completeStrikeFleets >= 1 && deployableFleets >= 2;   // a finished strike fleet + a reserve → sized, stop
        }

        /// <summary>Slice 3 — the RESERVE GUARD (a placeholder for the system military-commander delegate that will own
        /// the commit-vs-reserve call). TRUE = the strike fleet may sail because a home-defense RESERVE remains. An
        /// ORGANIZED fleet (one the AI assembled — carries a <see cref="Pulsar4X.Fleets.FleetCompositionDB"/>) may sail
        /// ONLY if the faction keeps ANOTHER organized combat fleet (≥ its deploy core, not this fleet, not already
        /// committed/moving) at home — so the AI never sends its LAST organized fleet on an offensive. A fleet with NO
        /// FleetCompositionDB (a hand-made / sandbox / test fleet) isn't subject to the doctrine and sails as before.
        /// Internal for the CI gauge.</summary>
        internal static bool HasHomeReserve(FactionState state, Entity strikeFleet)
        {
            if (strikeFleet == null || !strikeFleet.HasDataBlob<Pulsar4X.Fleets.FleetCompositionDB>())
                return true;   // not an AI-organized fleet → the reserve doctrine doesn't gate it
            foreach (var fleet in state.OwnedFleets())
            {
                if (fleet.Id == strikeFleet.Id) continue;
                if (!fleet.TryGetDataBlob<Pulsar4X.Fleets.FleetCompositionDB>(out var comp)) continue;
                if (FleetIsMoving(fleet)) continue;   // already committed elsewhere — not a home reserve
                if (MilitaryComposition.WarshipCount(fleet) >= comp.MinToDeploy)
                    return true;   // a home reserve remains → the strike may sail
            }
            return false;   // no reserve → hold this fleet home (the AI keeps massing a reserve first)
        }

        /// <summary>An armed hull presented to the shared <see cref="DecisionScorer"/>: v1 scores it on
        /// <see cref="DecisionFeature.MilitarySolve"/> = its <see cref="WeaponMountCount"/> (firepower proxy).</summary>
        private sealed class WarshipOption : IScoredOption
        {
            public ShipDesign Design { get; }
            private readonly Dictionary<DecisionFeature, double> _f;
            public WarshipOption(ShipDesign d)
            {
                Design = d;
                _f = new Dictionary<DecisionFeature, double> { { DecisionFeature.MilitarySolve, WeaponMountCount(d) } };
            }
            public IReadOnlyDictionary<DecisionFeature, double> Features => _f;
        }

        /// <summary>Phase A-2b — the "best not first" warship pick: SCORE the candidate warships with the shared
        /// <see cref="DecisionScorer"/> (weighted by the faction's personality) and return the winner, or null for an
        /// empty set. v1's single MilitarySolve feature makes this "mass the most-armed hull"; the personality split
        /// (brawler vs standoff vs evasive) rides the richer per-design featurization refinement. Internal for the gauge.</summary>
        internal static ShipDesign PickWarship(IEnumerable<ShipDesign> candidates, PersonalityDB personality)
        {
            var options = new List<WarshipOption>();
            foreach (var c in candidates) if (c != null) options.Add(new WarshipOption(c));
            return DecisionScorer.PickBest(options, personality)?.Design;
        }

        /// <summary>A ship design that can LIFT ground troops — it mounts a troop bay (<see cref="Pulsar4X.GroundCombat.GroundBayAtb"/>).
        /// The transport twin of <see cref="IsWarship"/>. The build rung finds ANY buildable transport design this way,
        /// not a hardcoded id (the base-mod trooper is faction-specific). Internal for the gauge.</summary>
        internal static bool IsTroopTransport(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<Pulsar4X.GroundCombat.GroundBayAtb>(out _);

        /// <summary>True if the faction already OWNS a ship that can carry troops (a Personnel troop bay). Internal for the gauge.</summary>
        internal static bool FactionOwnsTransport(FactionState state) => FindOwnedTransport(state) != null;

        /// <summary>The faction's owned transport SHIP (mounts a Personnel troop bay), or null. Scans ALL owned ships across
        /// every system — NOT just fleet members, because a freshly-built ship isn't auto-added to a fleet, so a fleet-only
        /// scan would miss it and re-build a transport every cycle. The entity twin of <see cref="FactionOwnsTransport"/>
        /// that the LOAD rung acts on. Internal for the gauge.</summary>
        internal static Entity FindOwnedTransport(FactionState state)
        {
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                    if (ship != null && ship.IsValid && ship.FactionOwnerID == state.FactionId
                        && Pulsar4X.GroundCombat.GroundTransport.BayCapacity(ship, Pulsar4X.GroundCombat.GroundCarryClass.Personnel) > 0)
                        return ship;
            }
            return null;
        }

        /// <summary>The faction's own transport that is AT <paramref name="targetBody"/>, still CARRYING a loaded unit,
        /// and HOLDS the orbit there (no foreign ship present), paired with the first such unit to land — what the LAND
        /// rung acts on. Returns (null, null) when no transport is in position to land, so the rung falls through to the
        /// sail/load/build rungs. Scans ALL owned ships across every system (like <see cref="FindOwnedTransport"/>), not
        /// just fleet members, because a transport that just arrived may not be in a fleet. Internal for the gauge.</summary>
        internal static (Entity ship, Pulsar4X.GroundCombat.GroundUnit unit) FindLandableTransport(FactionState state, Entity targetBody)
        {
            if (targetBody == null || !targetBody.IsValid) return (null, null);
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                {
                    if (ship == null || !ship.IsValid || ship.FactionOwnerID != state.FactionId) continue;
                    if (!ship.TryGetDataBlob<Pulsar4X.GroundCombat.GroundTransportDB>(out var transport)) continue;
                    if (transport.LoadedUnits.Count == 0) continue;
                    if (!Pulsar4X.GroundCombat.GroundTransport.ShipIsAtBody(ship, targetBody)) continue;
                    if (!Pulsar4X.GroundCombat.GroundTransport.HasOrbitalControl(ship, targetBody)) continue;
                    return (ship, transport.LoadedUnits[0]);
                }
            }
            return (null, null);
        }

        /// <summary>The faction's own transport that is CARRYING troops but is NOT yet at <paramref name="targetBody"/>,
        /// is NOT already in warp transit, and CAN actually warp (a drive with a real speed) — what the SAIL rung sends
        /// at the target. Returns null when no such transport exists (so the rung falls through to LOAD/BUILD/MASS). The
        /// warp-drive + not-warping guards stop a driveless or in-transit transport from being re-issued a no-op sail
        /// every cycle. Scans ALL owned ships across every system, like <see cref="FindLandableTransport"/>.</summary>
        internal static Entity FindSailableTransport(FactionState state, Entity targetBody)
        {
            if (targetBody == null || !targetBody.IsValid) return null;
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                {
                    if (ship == null || !ship.IsValid || ship.FactionOwnerID != state.FactionId) continue;
                    if (!ship.TryGetDataBlob<Pulsar4X.GroundCombat.GroundTransportDB>(out var transport)) continue;
                    if (transport.LoadedUnits.Count == 0) continue;                                     // carrying troops
                    if (Pulsar4X.GroundCombat.GroundTransport.ShipIsAtBody(ship, targetBody)) continue; // NOT already there (that's the LAND rung)
                    if (ship.HasDataBlob<Pulsar4X.Movement.WarpMovingDB>()) continue;                   // not already en route
                    if (!ship.TryGetDataBlob<Pulsar4X.Movement.WarpAbilityDB>(out var warp) || !(warp.MaxSpeed > 0)) continue; // can actually warp
                    return ship;
                }
            }
            return null;
        }

        /// <summary>The body a ship is currently at (its <see cref="Pulsar4X.Movement.PositionDB"/> parent), or null — the
        /// staging body the LOAD rung loads from (and the same read <c>GroundTransport.ShipIsAtBody</c> uses).</summary>
        private static Entity ShipBody(Entity ship)
            => ship != null && ship.TryGetDataBlob<Pulsar4X.Movement.PositionDB>(out var pos) ? pos.Parent : null;

        /// <summary>A standing ground unit of <paramref name="factionId"/> on <paramref name="body"/> that the
        /// <paramref name="transport"/> can ACTUALLY load — i.e. a bay of the unit's class has room (<c>CanLoad</c>).
        /// Filtering by <c>CanLoad</c> is load-bearing: a Personnel-only troop transport must SKIP the Vehicle-class
        /// armour/artillery in the garrison (otherwise the rung would pick a unit it can't carry, the load would no-op,
        /// and it would pick that same unit forever — an infinite no-op). Returns null when nothing loadable remains,
        /// so the rung falls through (and slice 2's SAIL takes over). Internal for the gauge.</summary>
        internal static Pulsar4X.GroundCombat.GroundUnit AvailableLoadableUnit(Entity transport, Entity body, int factionId)
        {
            if (body == null || !body.IsValid || !body.TryGetDataBlob<Pulsar4X.GroundCombat.GroundForcesDB>(out var forces))
                return null;
            foreach (var u in forces.Units)
                if (u != null && u.FactionOwnerID == factionId
                    && Pulsar4X.GroundCombat.GroundTransport.CanLoad(transport, u))
                    return u;
            return null;
        }

        /// <summary>True if any ship in the fleet is already in warp transit — so the strike rung doesn't re-issue the
        /// sail order every monthly cycle (which would thrash the fleet's warp). A cheap en-route guard; a fuller
        /// "already ordered to this target" check rides the later reach polish.</summary>
        private static bool FleetIsMoving(Entity fleet)
        {
            foreach (var ship in Pulsar4X.Combat.FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid && ship.HasDataBlob<Pulsar4X.Movement.WarpMovingDB>())
                    return true;
            return false;
        }

        /// <summary>The id of a free (empty-queue) production line on this colony that runs the given industry type.</summary>
        private static string FreeLineFor(IndustryAbilityDB industry, string industryTypeId)
        {
            foreach (var lineKvp in industry.ProductionLines)
                if (lineKvp.Value.IndustryTypeRates.ContainsKey(industryTypeId) && lineKvp.Value.Jobs.Count == 0)
                    return lineKvp.Key;
            return null;
        }
    }
}
