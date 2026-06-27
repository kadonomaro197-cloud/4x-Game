using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;
using Pulsar4X.Names;

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

        // --- combat PACE (the hot-damage rebalance, 2026-06-25) -----------------------------------------------

        /// <summary>
        /// THE COMBAT-PACE DIAL — how much of a salvo's raw weapon energy actually counts toward kills each step.
        ///
        /// Plain English: with the raw numbers, weapons were "hot" — a hull is built from components worth ~100 kJ
        /// each, but a single gun pours ~1 MJ/sec, so a ship died in about one second of fire and whole fleet
        /// battles were over in 2–4 salvos (10–20 game-seconds). That's faster than the game clock's default
        /// 1-hour tick — the fight was finished before you could watch it or change a doctrine. Think of this dial
        /// as "every hull is secretly 1/scale times tougher than its parts add up to": at 0.1 a ship soaks 10× the
        /// punishment, so the SAME battle plays out over ~10× more salvos (a couple of minutes of game-time you can
        /// actually watch and steer) — WITHOUT changing who wins or the exchange ratio, because it scales every
        /// fleet's incoming fire by the same factor. Turn it DOWN to make battles drag out longer, UP to make
        /// them deadlier. It is the single knob for the "per-shot-energy ÷ hull-toughness" balance.
        ///
        /// It lives here, on the STEPPED resolve (the live, watchable battle path the trigger drives), on purpose:
        /// <see cref="AutoResolve"/> is the pure "instant" resolver for an off-screen fight nobody is watching, so
        /// it stays unscaled (a battle no one sees needn't be paced out). If a future version routes live battles
        /// through AutoResolve, give it the same dial.
        /// </summary>
        public const double SalvoDamageScale = 0.1;

        /// <summary>Liveness counter (diagnostic only): how many trigger passes the battle engine has run across the
        /// whole game. The client logs this each heartbeat so a remote review can tell "no battle because nothing's
        /// hostile/in-range/detected" apart from "the battle trigger never fires on play" — a documented open
        /// question (the colony test harness doesn't reliably auto-fire it). Interlocked: systems tick in parallel.</summary>
        public static long TickCount;

        /// <summary>One trigger pass over a system: engage/join hostile fleets, then step the engagement. Returns
        /// the number of fleets seen. Defensive — built not to throw on normal game state.</summary>
        public static int Tick(EntityManager manager, int deltaSeconds)
        {
            System.Threading.Interlocked.Increment(ref TickCount);
            var fleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (fleets.Count == 0) return 0;

            double dt = deltaSeconds > 0 ? deltaSeconds : 1;

            // A fleet that BROKE OFF (has FleetRetreatDB) stays withdrawn while a threat is present — the engage
            // pass below skips it, so it isn't yanked back into the fight every tick (the engage/disengage thrash).
            // Once no hostile is in range (the enemy died, left, or this fleet was moved away) the flag is stale, so
            // drop it here and the fleet can fight again later. This is the v1 "re-commit" path (no move-order wiring
            // yet — the v2 movement layer that actually sails the withdraw vector will supersede this).
            foreach (var rf in fleets)
                if (rf.IsValid && rf.HasDataBlob<FleetRetreatDB>() && !AnyHostileInRange(rf, fleets))
                    rf.RemoveDataBlob<FleetRetreatDB>();

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
                    // Don't re-grab a fleet that broke off — it stays withdrawn while the enemy is in range (the
                    // top-of-Tick sweep clears the flag once the threat is gone). Stops the retreat thrash.
                    if (a.HasDataBlob<FleetRetreatDB>() || b.HasDataBlob<FleetRetreatDB>()) continue;
                    if (GetFleetShips(b).Count == 0) continue;
                    if (!InRange(a, b)) continue;
                    // Don't start a fight neither side can resolve (both unarmed): it would enter, freeze (no damage
                    // dealt), and disengage every tick — a thrash. A fight needs at least one side that can deal damage.
                    if (!FleetHasFirepower(a) && !FleetHasFirepower(b)) continue;
                    // Fog of war (client-on / test-off): a fight forms once EITHER side detects the other — the
                    // detector can open fire even if its target is still blind (first-strike). Both fleets enter
                    // combat so the resolver has the target present to be shot; the BLIND one simply doesn't shoot
                    // back (the directed-fire resolve handles that — see StepEngagementGroup / CanEngageTarget).
                    if (RequireDetectionToEngage && !(FleetDetects(a, b) || FleetDetects(b, a))) continue;

                    // First-shot trigger (Phase 3, client-on / test-off): a battle erupts only if someone will RELEASE
                    // a shot. If BOTH fleets are holding fire (neither WeaponsFree), they sit in a tense STANDOFF — no
                    // engagement forms. Default posture is WeaponsFree, so with the flag off this never blocks anything.
                    if (RequireWeaponsReleaseToEngage
                        && FleetDoctrine.PostureOf(a) != EngagementPosture.WeaponsFree
                        && FleetDoctrine.PostureOf(b) != EngagementPosture.WeaponsFree)
                        continue;

                    // Weapons-release narration: when the first-shot rule is on and a NEW battle forms, name who
                    // opened fire (the standoff breaking). Once-only — after EnsureInCombat both hold state.
                    if (NarrateToLog && RequireWeaponsReleaseToEngage &&
                        (!a.HasDataBlob<FleetCombatStateDB>() || !b.HasDataBlob<FleetCombatStateDB>()))
                    {
                        var shooter = FleetDoctrine.PostureOf(a) == EngagementPosture.WeaponsFree ? a : b;
                        var target = shooter == a ? b : a;
                        CombatLog($"WEAPONS RELEASE: {FleetLabel(shooter)} opens fire on {FleetLabel(target)} — the standoff breaks");
                    }

                    // First-strike narration: when a NEW engagement forms with one side blind to the other, call it
                    // out ONCE in the combat log. Gated on NarrateToLog + fog-on (inert in tests), and on at least
                    // one side not yet in combat — after EnsureInCombat both hold state, so it never re-logs.
                    if (NarrateToLog && RequireDetectionToEngage &&
                        (!a.HasDataBlob<FleetCombatStateDB>() || !b.HasDataBlob<FleetCombatStateDB>()))
                    {
                        bool aSeesB = FleetDetects(a, b), bSeesA = FleetDetects(b, a);
                        if (aSeesB && !bSeesA)
                            CombatLog($"FIRST-STRIKE: {FleetLabel(a)} detects {FleetLabel(b)}, which is BLIND — it takes fire it can't return");
                        else if (bSeesA && !aSeesB)
                            CombatLog($"FIRST-STRIKE: {FleetLabel(b)} detects {FleetLabel(a)}, which is BLIND — it takes fire it can't return");
                    }

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

        /// <summary>
        /// Read-only pre-step gate for the time loop: is a brand-NEW engagement about to fire in this star system
        /// on the next trigger pass? True iff some hostile pair, both with ships, sits within
        /// <see cref="EngagementRange_m"/> AND at least one of them is NOT already in combat — because
        /// <see cref="EnsureInCombat"/> is guarded, so it (and the time-halt it requests) only fires for a fleet
        /// that lacks <see cref="FleetCombatStateDB"/>. So this reads TRUE at first contact and when a fresh fleet
        /// JOINS a fight, and FALSE once every in-range hostile is already mutually engaged.
        ///
        /// That last part is deliberate and is the whole point of the narrow test: the master time loop only drops
        /// to fine sub-steps while this is true — just long enough to land the auto-pause at the new engagement —
        /// and then hands the clock back at the player's chosen step size for the ONGOING exchange. So combat runs
        /// at the speed you set (1 s, 10 min, 1 hr), and the engine only "takes the wheel" for the split second a
        /// fight is being born so you are never blindsided by its START. Mirrors the engage/join pass of
        /// <see cref="Tick"/> but mutates NOTHING. O(fleets^2); fleet counts are small. Defensive — never throws.
        ///
        /// LIMIT (v1): gates on present, in-range state only. A fleet that crosses from out of range to engaged
        /// inside a single step (e.g. a long warp resolved under a big step) is still resolved without a pause —
        /// a closing-prediction lookahead is a v2 layer. The common case — fleets already parked together, e.g. at
        /// the same body, or a player stepping carefully through a battle — is fully covered.
        /// </summary>
        public static bool NewEngagementImminent(EntityManager manager)
        {
            var fleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (fleets.Count == 0) return false;

            for (int i = 0; i < fleets.Count; i++)
            {
                var a = fleets[i];
                if (!a.IsValid || GetFleetShips(a).Count == 0) continue;
                for (int j = i + 1; j < fleets.Count; j++)
                {
                    var b = fleets[j];
                    if (!b.IsValid) continue;
                    if (!AreHostile(a, b)) continue;
                    if (a.HasDataBlob<FleetRetreatDB>() || b.HasDataBlob<FleetRetreatDB>()) continue; // withdrawn — won't form a new engagement
                    if (GetFleetShips(b).Count == 0) continue;
                    if (!InRange(a, b)) continue;
                    if (!FleetHasFirepower(a) && !FleetHasFirepower(b)) continue; // no fight two unarmed fleets can resolve
                    // In range + hostile + both manned. If EITHER isn't yet in combat, a new entry — and so the
                    // interrupt — fires this tick. If both are already engaged, this pair is an ONGOING fight and
                    // does NOT force fine-stepping, so the player keeps their set step size for it.
                    if (!a.HasDataBlob<FleetCombatStateDB>() || !b.HasDataBlob<FleetCombatStateDB>())
                        return true;
                }
            }
            return false;
        }

        /// <summary>When true, the engine narrates each fight (engage / casualties / retreat / disengage) to the
        /// captured log (game_log.txt) as plain-language <c>[Combat]</c> lines, so a live battle is visible there and
        /// not only in the Fleet Combat tab. Default FALSE so it never slows the timed battle tests; the client turns
        /// it on at startup. Logged on STATE CHANGES only (never per-tick) so it reads like a play-by-play.</summary>
        public static bool NarrateToLog = false;

        /// <summary>When true, a NEW battle beginning (a fleet first entering combat) HALTS the game clock at that
        /// sub-pulse — the Aurora-style combat interrupt. So an hour-step or a play-run stops at first contact and
        /// hands the player notice + a chance to steer (change doctrine) instead of the whole fight resolving
        /// invisibly inside one step. Default FALSE so headless/combat tests advance deterministically; the client
        /// turns it on at startup. Fires once per battle/joiner (EnsureInCombat is guarded — an ongoing fight does
        /// not re-halt every tick); only a brand-new engagement or a fresh fleet joining stops the clock.</summary>
        public static bool InterruptTimeOnNewEngagement = false;

        /// <summary>When true, combat is FOG-OF-WAR gated: a fight is driven by what each side DETECTS (a sensor
        /// contact in the per-faction track table, <see cref="EntityManager.GetSensorContacts"/>), not merely by
        /// presence in the system. This is the seam that makes "what you can see decides the fight" real —
        /// detection × weapons. A fight forms once EITHER side detects the other (the detector can open fire on a
        /// still-blind target), and the resolver's fire is DIRECTED by <see cref="CanEngageTarget"/> — a fleet shoots
        /// only the hostiles it detects. That gives FIRST-STRIKE: the side that sees first shoots first, and a blind
        /// fleet can't return fire until its own sensors find the shooter. Default FALSE so the existing combat
        /// tests stay deterministic (they don't stand up sensors/contacts) — and with it off, <c>CanEngageTarget</c>
        /// is always true, so the resolver is exactly the old symmetric exchange. The client turns it on when
        /// detection is live, next to <see cref="NarrateToLog"/> / <see cref="InterruptTimeOnNewEngagement"/>.</summary>
        public static bool RequireDetectionToEngage = false;

        /// <summary>When true, a battle only ERUPTS if someone will release a shot — the first-shot trigger (Phase 3,
        /// docs/FLEET-COMBAT-CLOSING-DESIGN.md). Two hostile fleets that are BOTH non-WeaponsFree (weapons-hold /
        /// return-fire) sit in a tense STANDOFF — proximity no longer auto-starts a fight. At least one WeaponsFree
        /// fleet (the default posture) starts it. Default FALSE so existing fixtures (no posture set = WeaponsFree
        /// anyway) are unchanged; the client turns it on when ROE is live.</summary>
        public static bool RequireWeaponsReleaseToEngage = false;

        /// <summary>When true, combat is a CLOSING fight (Phase 1, docs/FLEET-COMBAT-CLOSING-DESIGN.md): a weapon only
        /// fires if its <see cref="WeaponProfile.Range_m"/> reaches the current gap, and the gap CLOSES each step toward
        /// the FASTER (more maneuverable) side's preferred range — so a faster long-range fleet kites a slower
        /// short-range one, and a faster brawler forces the merge. Default FALSE so every existing combat fixture is
        /// byte-identical: with it off the gap stays 0 and the range-gate is a no-op (a 0-separation or 0/unbounded-range
        /// weapon always fires). v1: ONE shared range per engagement group (per-sub-fleet ranges are Phase 4).</summary>
        public static bool EnableClosingRange = false;

        /// <summary>Closing-rate dial (m/s): the gap-change speed of a maximally-maneuverable fleet (evasion 1.0); a
        /// fleet changes the gap proportional to its maneuverability (min evasion over its ships — it moves as one).
        /// Tunable like <see cref="SalvoDamageScale"/>; set 0 to FREEZE the gap (gauge use). v1 calibration provisional.</summary>
        public static double ClosingSpeedScale_mps = 100_000.0;

        /// <summary>Fallback opening gap (m) when first contact has no usable fleet positions (the multi-party join
        /// path). The 2-fleet <see cref="StartEngagement"/> seeds the real distance instead. v1 stub.</summary>
        public static double InitialSeparationDefault_m = 10_000_000.0;

        /// <summary>Phase 2 (kiting counter): Δv (m/s) a fleet spends per game-second it CONTROLS the range. A kiter
        /// holding the gap burns this each step; when its <see cref="FleetCombatStateDB.ManeuverBudget"/> runs dry it can
        /// no longer dictate the range and the enemy closes on it — so you can't kite forever. Tunable; 0 = free maneuver
        /// (kiting never runs out). v1 calibration provisional.</summary>
        public static double ManeuverBurnRate = 5.0;

        private static void CombatLog(string msg)
        {
            if (NarrateToLog) System.Console.WriteLine("[Combat] " + msg);
        }

        /// <summary>Capture a structured battle event for the persistent Battle Report (<see cref="BattleLog"/>).
        /// UNCONDITIONAL — not gated on <see cref="NarrateToLog"/> — so the report works regardless of the
        /// console-log flag (it's a cheap struct append under a lock). Defensive: never throws on game state.</summary>
        private static void RecordBattleEvent(Entity fleet, BattleEventType type, int shipsLost, int shipsLeft, int step, string note)
        {
            if (fleet == null) return;
            System.DateTime when = default;
            try { when = fleet.Manager?.Game?.TimePulse?.GameGlobalDateTime ?? default; } catch { }
            string name = fleet.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : "Fleet";
            BattleLog.Record(new BattleEvent(when, fleet.Id, name, fleet.FactionOwnerID, type, shipsLost, shipsLeft, step, note));
        }

        private static string FleetLabel(Entity fleet)
        {
            if (fleet == null || !fleet.IsValid) return "(gone)";
            return (fleet.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : "Fleet") + " #" + fleet.Id;
        }

        /// <summary>Attach combat state to both fleets, each pointing at the other as its (representative) opponent
        /// and recording its starting ship count (the denominator for the casualty-fraction retreat threshold).
        /// The proven entry point for a controlled two-fleet matchup; multi-party joins go through
        /// <see cref="EnsureInCombat"/> in <see cref="Tick"/>.</summary>
        public static void StartEngagement(Entity fleetA, Entity fleetB)
        {
            var sa = new FleetCombatStateDB(fleetB.Id, GetFleetShips(fleetA).Count);
            var sb = new FleetCombatStateDB(fleetA.Id, GetFleetShips(fleetB).Count);
            if (EnableClosingRange)   // Phase 1: seed the opening gap from the real distance between the two fleets
            {
                double gap = FleetSeparation(fleetA, fleetB);
                sa.Separation_m = gap;
                sb.Separation_m = gap;
                sa.ManeuverBudget = FleetCombat.DeltaVFloor(fleetA);   // Phase 2: the kiting clock = the fleet's Δv floor
                sb.ManeuverBudget = FleetCombat.DeltaVFloor(fleetB);
            }
            fleetA.SetDataBlob(sa);
            fleetB.SetDataBlob(sb);
            CombatLog($"{FleetLabel(fleetA)} vs {FleetLabel(fleetB)} — engaged");
            RecordBattleEvent(fleetA, BattleEventType.Engaged, 0, GetFleetShips(fleetA).Count, 0, "vs " + FleetLabel(fleetB));
            RecordBattleEvent(fleetB, BattleEventType.Engaged, 0, GetFleetShips(fleetB).Count, 0, "vs " + FleetLabel(fleetA));
        }

        /// <summary>Put a fleet "in combat" if it isn't already — the JOIN primitive. Idempotent: a fleet already
        /// engaged keeps its running state (damage pool, steps, initial count) untouched, so a reinforcement
        /// arriving each tick doesn't reset the fight. Records its starting ship count for the retreat threshold
        /// and a representative opponent for readout (the real membership is every hostile fleet in the system).</summary>
        public static void EnsureInCombat(Entity fleet, int representativeOpponentId)
        {
            if (fleet == null || !fleet.IsValid || fleet.HasDataBlob<FleetCombatStateDB>()) return;
            var st = new FleetCombatStateDB(representativeOpponentId, GetFleetShips(fleet).Count);
            if (EnableClosingRange)
            {
                st.Separation_m = InitialSeparationDefault_m;          // Phase 1 v1: join path seeds the default gap
                st.ManeuverBudget = FleetCombat.DeltaVFloor(fleet);    // Phase 2: the kiting clock
            }
            fleet.SetDataBlob(st);
            CombatLog($"{FleetLabel(fleet)} enters combat ({GetFleetShips(fleet).Count} ship(s))");
            RecordBattleEvent(fleet, BattleEventType.Engaged, 0, GetFleetShips(fleet).Count, 0, "enters combat");

            // Aurora-style combat interrupt: a NEW fleet just entered a battle — stop the clock here (if enabled)
            // so the player is handed control at first contact instead of the whole fight resolving inside one
            // step/play-run. The early-return guard above makes this fire once per battle/joiner, not every tick.
            if (InterruptTimeOnNewEngagement)
                fleet.Manager?.Game?.TimePulse?.RequestCombatHalt();
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
            // DIRECTED engagement (first-strike): fleet i can shoot k only if i can ENGAGE k — hostile, both manned,
            // and (with fog-of-war on) i DETECTS k. So a fleet that hasn't detected its attacker doesn't shoot back.
            // With fog off, CanEngageTarget is always true, so every hostile pair fills BOTH directions and this is
            // exactly the old symmetric enemy set — the existing combat fixtures are unchanged.
            var targetsOf = new List<int>[n];   // fleets live[i] can shoot
            var attackersOf = new List<int>[n]; // fleets that can shoot live[i]
            for (int i = 0; i < n; i++)
            {
                ships[i] = GetCombatShips(live[i]);
                fire[i] = BuildFireMix(ships[i], SeparationOf(live[i]));   // Phase 1: only weapons reaching the gap fire
                targetsOf[i] = new List<int>();
                attackersOf[i] = new List<int>();
            }
            for (int i = 0; i < n; i++)
                for (int k = 0; k < n; k++)
                {
                    if (i == k) continue;
                    if (ships[i].Count == 0 || ships[k].Count == 0) continue;
                    if (!AreHostile(live[i], live[k])) continue;
                    if (CanEngageTarget(live[i], live[k]))
                    {
                        targetsOf[i].Add(k);
                        attackersOf[k].Add(i);
                    }
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
                if (attackersOf[i].Count == 0) continue; // nobody shooting at this fleet this step (it may still be shooting others)

                state.OpponentFleetId = live[attackersOf[i][0]].Id; // keep the readout pointing at a fleet shooting it

                var incoming = new List<WeaponProfile>();
                foreach (int g in attackersOf[i])
                {
                    int split = targetsOf[g].Count; // attacker g divides its fire across the targets IT can engage
                    if (split <= 0) continue;
                    AddScaledFire(incoming, fire[g], 1.0 / split);
                }
                // SalvoDamageScale is the combat-pace dial: only this fraction of the raw salvo energy counts
                // toward kills, so battles play out over many salvos instead of ending in 2–4 (see the const).
                state.DamageTakenPool += TotalDamage(incoming) * dt * SalvoDamageScale;
                ApplyCasualties(ships[i], state, incoming); // prunes the dead from ships[i]
            }

            // --- CLOSING phase (Phase 1): the gap moves toward the faster side's preferred range, AFTER this step's
            // salvo landed at the current gap. Off by default (flag) so the pre-closing resolve is untouched.
            if (EnableClosingRange)
            {
                AdvanceClosing(live, ships, dt);
                if (NarrateToLog) NarrateClosing(live, ships);   // live visibility into the closing fight
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
                // Done here only if it can neither shoot anyone NOR be shot: a one-sided aggressor (targets, no
                // attackers) stays IN so it keeps firing on its blind victim; a blind victim (attackers, no targets)
                // stays IN so it keeps taking fire until it's wiped or its own scan finds the shooter.
                if (attackersOf[i].Count == 0 && targetsOf[i].Count == 0) { EndEngagement(f); continue; }
                if (ShouldRetreat(f, state, aliveCount))
                {
                    // Break off away from a fleet that's actually shooting it; fall back to one it was shooting.
                    var from = attackersOf[i].Count > 0 ? live[attackersOf[i][0]] : live[targetsOf[i][0]];
                    RecordRetreat(f, from);
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
            {
                CombatLog($"{FleetLabel(fleet)} disengages ({GetFleetShips(fleet).Count} ship(s) left)");
                RecordBattleEvent(fleet, BattleEventType.Disengaged, 0, GetFleetShips(fleet).Count, 0, "disengages");
                fleet.RemoveDataBlob<FleetCombatStateDB>();
            }
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

            int totalKilled = 0;
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
                    totalKilled += kills;
                }
                if (kills < b.Ships.Count) break; // pool ran out inside this bucket -> stop (matches per-ship break)
            }

            // Drop the dead (Destroy() flips IsValid synchronously) so the caller's survivor count is accurate.
            ships.RemoveAll(cs => !cs.Ship.IsValid);

            if (totalKilled > 0)
            {
                if (NarrateToLog)
                    CombatLog($"salvo {state.StepsFought}: {FleetLabel(state.OwningEntity)} lost {totalKilled} ship(s), {ships.Count} left");
                RecordBattleEvent(state.OwningEntity, BattleEventType.Salvo, totalKilled, ships.Count, state.StepsFought, "");
            }
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
        // ─── Phase 1 — closing distance ────────────────────────────────────────────────────────────────────────

        /// <summary>This fleet's current gap to the opposing side — 0 when closing is off or it has no state (which
        /// makes the range-gate a no-op).</summary>
        private static double SeparationOf(Entity fleet)
            => EnableClosingRange && fleet.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.Separation_m : 0;

        /// <summary>Advance the engagement gap one step. The FASTER (more maneuverable) side dictates the range: the
        /// gap moves toward the controller's preferred standoff (its longest weapon range; 0 = close in). v1 keeps ONE
        /// shared gap for the whole group (per-sub-fleet ranges are Phase 4). Deterministic — pure arithmetic on dt.</summary>
        private static void AdvanceClosing(List<Entity> live, List<CombatShip>[] ships, double dt)
        {
            int controller = -1;
            double best = -1;
            for (int i = 0; i < live.Count; i++)
            {
                if (ships[i].Count == 0) continue;
                // Phase 2 (kiting counter): only a fleet with maneuver budget LEFT can dictate the range. A burned-out
                // fleet drops out of contention, so a dry kiter loses control and the enemy closes. ManeuverBurnRate 0
                // = free maneuver (budget never matters), the pre-P2 behaviour.
                if (ManeuverBurnRate > 0 &&
                    (!live[i].TryGetDataBlob<FleetCombatStateDB>(out var bs) || bs.ManeuverBudget <= 0))
                    continue;
                double m = FleetManeuver(ships[i]);
                if (m > best) { best = m; controller = i; }
            }
            if (controller < 0) return;

            // The controller SPENDS maneuver budget to dictate the range — the kiting clock that makes "kite forever"
            // impossible. When it hits 0 it's no longer eligible above, so next step the enemy takes the wheel.
            if (ManeuverBurnRate > 0 && live[controller].TryGetDataBlob<FleetCombatStateDB>(out var ctrlState))
            {
                double before = ctrlState.ManeuverBudget;
                ctrlState.ManeuverBudget = System.Math.Max(0, before - ManeuverBurnRate * dt);
                if (NarrateToLog && before > 0 && ctrlState.ManeuverBudget <= 0)
                    CombatLog($"{FleetLabel(live[controller])} has spent its maneuver reserve — it can no longer dictate the range, the enemy will close");
            }

            double desired = FleetDesiredRange(ships[controller]);      // the controller's preferred standoff
            double step = best * ClosingSpeedScale_mps * dt;            // metres it can change the gap this step
            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].IsValid || !live[i].TryGetDataBlob<FleetCombatStateDB>(out var st)) continue;
                double r = st.Separation_m;
                if (r > desired) r = System.Math.Max(desired, r - step);
                else if (r < desired) r = System.Math.Min(desired, r + step);
                st.Separation_m = r;
            }
        }

        /// <summary>A fleet's maneuver speed proxy = the MIN evasion over its ships (it moves as one, bound by its
        /// least-agile ship). Higher = it controls the range. Reuses the already-computed Evasion (no new data).</summary>
        private static double FleetManeuver(List<CombatShip> ships)
        {
            double floor = double.PositiveInfinity;
            foreach (var cs in ships)
            {
                double e = CombatValue(cs.Ship).Evasion;
                if (e < floor) floor = e;
            }
            return double.IsInfinity(floor) ? 0 : floor;
        }

        /// <summary>The fleet's preferred standoff = its longest FINITE weapon range (so a long-range fleet wants to
        /// hold far; a fleet with only unbounded/short guns wants 0 = close in).</summary>
        private static double FleetDesiredRange(List<CombatShip> ships)
        {
            double maxFinite = 0;
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                if (cv.Weapons == null) continue;
                foreach (var w in cv.Weapons)
                    if (w.Range_m > maxFinite) maxFinite = w.Range_m;   // unbounded (0) never raises it
            }
            return maxFinite;
        }

        /// <summary>The real-space distance between two fleets, for seeding the opening gap; the default if either has
        /// no usable position.</summary>
        private static double FleetSeparation(Entity a, Entity b)
        {
            if (TryGetFleetPosition(a, out var pa) && TryGetFleetPosition(b, out var pb))
                return (pa - pb).Length();
            return InitialSeparationDefault_m;
        }

        /// <summary>The fleet's longest weapon reach — <see cref="double.PositiveInfinity"/> if it has any unbounded
        /// (rangeless) weapon, else the max finite <see cref="WeaponProfile.Range_m"/>, 0 if it has no weapons.</summary>
        private static double MaxReach(List<CombatShip> ships)
        {
            double max = 0;
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                if (cv.Weapons == null) continue;
                foreach (var w in cv.Weapons)
                {
                    if (w.Range_m <= 0) return double.PositiveInfinity;   // an unbounded weapon = infinite reach
                    if (w.Range_m > max) max = w.Range_m;
                }
            }
            return max;
        }

        /// <summary>One live line per fleet per closing step: its gap, whether its guns REACH it, its longest reach,
        /// and its maneuver reserve — so a play-test log shows the standoff/closing play out (the gauge CI can't run).</summary>
        private static void NarrateClosing(List<Entity> live, List<CombatShip>[] ships)
        {
            for (int i = 0; i < live.Count; i++)
            {
                if (ships[i].Count == 0 || !live[i].TryGetDataBlob<FleetCombatStateDB>(out var st)) continue;
                double gap = st.Separation_m;
                double reach = MaxReach(ships[i]);
                bool reaches = reach >= gap;            // PositiveInfinity >= gap is true (unbounded reaches)
                string reachStr = double.IsInfinity(reach) ? "unlimited" : $"{reach:N0}m";
                CombatLog($"closing: {FleetLabel(live[i])} gap {gap:N0}m — {(reaches ? "IN RANGE" : "OUT OF RANGE")} " +
                          $"(reach {reachStr}), maneuver reserve {st.ManeuverBudget:N0}");
            }
        }

        /// <summary>On-demand snapshot of every active engagement in a system — the "send me a screenshot of the
        /// fight" tool. Prints, per in-combat fleet: ships, gap, reach/IN-or-OUT, maneuver reserve, posture, and the
        /// damage pool against it. Writes <c>[Combat] DUMP</c> lines UNCONDITIONALLY (not gated on <see cref="NarrateToLog"/>)
        /// so a DevTools button works even with narration off. Read-only.</summary>
        public static void DumpActiveCombat(EntityManager system)
        {
            if (system == null) return;
            var inCombat = system.GetAllEntitiesWithDataBlob<FleetCombatStateDB>();
            System.Console.WriteLine($"[Combat] DUMP — {inCombat.Count} fleet(s) in combat in this system "
                + $"(closing {(EnableClosingRange ? "ON" : "OFF")}, weapons-release {(RequireWeaponsReleaseToEngage ? "ON" : "OFF")}, fog {(RequireDetectionToEngage ? "ON" : "OFF")})");
            foreach (var fleet in inCombat)
            {
                if (!fleet.TryGetDataBlob<FleetCombatStateDB>(out var st)) continue;
                var combatShips = GetCombatShips(fleet);
                double reach = MaxReach(combatShips);
                string reachStr = double.IsInfinity(reach) ? "unlimited" : $"{reach:N0}m";
                bool reaches = reach >= st.Separation_m;
                System.Console.WriteLine($"[Combat] DUMP   {FleetLabel(fleet)}: {combatShips.Count} ship(s), "
                    + $"gap {st.Separation_m:N0}m {(reaches ? "IN RANGE" : "OUT")} (reach {reachStr}), "
                    + $"reserve {st.ManeuverBudget:N0}, posture {FleetDoctrine.PostureOf(fleet)}, "
                    + $"salvo {st.StepsFought}, incoming pool {st.DamageTakenPool:N0}");
            }
        }

        private static List<WeaponProfile> BuildFireMix(List<CombatShip> ships, double separation_m = 0)
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
                    {
                        // RANGE GATE (Phase 1): a FINITE-range weapon only fires if it reaches the current gap.
                        // separation 0 (flag off / point blank) or a 0/unbounded weapon range => always fires, so
                        // this is a no-op in the pre-closing path (every existing fixture is unchanged).
                        if (separation_m > 0 && w.Range_m > 0 && w.Range_m < separation_m) continue;
                        Add(w.Class, w.DamagePerSecond * cs.FirepowerMult, w.Velocity, w.Tracking, w.Saturation);
                    }
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
            CombatLog($"{FleetLabel(fleet)} breaks off — retreats from {FleetLabel(enemy)}");
            RecordBattleEvent(fleet, BattleEventType.Retreat, 0, GetFleetShips(fleet).Count, 0, "from " + FleetLabel(enemy));
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

        /// <summary>True if any ship in the fleet can deal damage (has a weapon profile or nonzero Firepower). A
        /// hostile pair with NO firepower on either side can't resolve a fight — engaging them just thrashes
        /// (enter → frozen, no damage → disengage, every tick), so the trigger skips such pairs.</summary>
        private static bool FleetHasFirepower(Entity fleet)
        {
            foreach (var ship in GetFleetShips(fleet))
            {
                var cv = CombatValue(ship);
                if (cv.Firepower > 0) return true;
                if (cv.Weapons != null && cv.Weapons.Count > 0) return true;
            }
            return false;
        }

        /// <summary>True if any hostile, manned fleet is in range of this one. Used to expire a stale
        /// <see cref="FleetRetreatDB"/> once the threat is gone — so a withdrawn fleet can fight again later, and
        /// moving it out of range re-commits it.</summary>
        private static bool AnyHostileInRange(Entity fleet, IReadOnlyList<Entity> fleets)
        {
            foreach (var other in fleets)
            {
                if (other == null || !other.IsValid || other == fleet) continue;
                if (!AreHostile(fleet, other)) continue;
                if (GetFleetShips(other).Count == 0) continue;
                if (InRange(fleet, other)) return true;
            }
            return false;
        }

        /// <summary>Fog-of-war check (the detection × weapons seam): does the <paramref name="detector"/> fleet's
        /// faction hold a sensor contact for ANY ship in the <paramref name="target"/> fleet? Reads the per-faction
        /// track table the sensor scan populates (<see cref="EntityManager.GetSensorContacts"/>). Defensive — false
        /// if there's no manager or the faction has no contact list yet. Only consulted when
        /// <see cref="RequireDetectionToEngage"/> is on.</summary>
        private static bool FleetDetects(Entity detector, Entity target)
        {
            if (detector?.Manager == null) return false;
            var contacts = detector.Manager.GetSensorContacts(detector.FactionOwnerID);
            if (contacts == null) return false;
            foreach (var ship in GetFleetShips(target))
                if (contacts.SensorContactExists(ship.Id)) return true;
            return false;
        }

        /// <summary>Can <paramref name="attacker"/> bring fire on <paramref name="target"/> this step? Always yes
        /// when fog-of-war is off (<see cref="RequireDetectionToEngage"/> false → the existing symmetric behaviour,
        /// so every combat fixture is unchanged). When on, only if the attacker DETECTS the target. This is the
        /// engine of FIRST-STRIKE: the side that sees first shoots first, and a fleet that hasn't detected its
        /// attacker can't return fire on it until its own sensors find it.</summary>
        private static bool CanEngageTarget(Entity attacker, Entity target)
            => !RequireDetectionToEngage || FleetDetects(attacker, target);

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
