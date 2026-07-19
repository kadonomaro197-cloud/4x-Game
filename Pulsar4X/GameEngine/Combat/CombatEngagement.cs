using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Factions;   // FactionInfoDB — the sub-fleet-vs-top-level discriminator
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.People;
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

        /// <summary>M2-1b (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): how far the faction's Collectivism trait
        /// swings the retreat threshold off <see cref="RetreatCasualtyThreshold"/>. A collectivist force
        /// ("fights to the last for the whole") holds on through heavier losses; an individualist one
        /// ("flees to save the unit") breaks off early. Centered on the trait's neutral, so a neutral/absent
        /// personality changes nothing — byte-identical.</summary>
        public const double CollectivismRetreatSwing = 0.4;

        // --- dodge model tuning (docs/WEAPONS-AND-DODGE-DESIGN.md), all v1 stubs ----------------------------

        /// <summary>Shot velocity (m/s) at which a weapon half-defeats evasion. A light-speed beam is far above
        /// this (≈always hits); a finite-velocity slug is far below (its shots can be dodged).</summary>
        public const double VelocityReference_mps = CombatKernel.VelocityReference_mps;

        /// <summary>Saturation (tracks/sec) at which a weapon half-guarantees a hit regardless of dodge — high for
        /// flak (fills the sky), low for a single slow slug.</summary>
        public const double SaturationReference = CombatKernel.SaturationReference;

        /// <summary>Floor on the fraction of fire that lands, so even a perfect dodger eventually dies to enough
        /// volume of fire — nothing is truly untouchable.</summary>
        public const double MinLandedFraction = CombatKernel.MinLandedFraction;

        /// <summary>Flight time (seconds) at which a ballistic shot's RANGE penalty reaches half its max — the
        /// "accuracy falls off with distance" knob. A shot crossing the gap takes flightTime = gap/velocity; the
        /// longer it flies, the more the target can displace before it arrives, so it's easier to dodge at range.
        /// A light-speed beam has ~0 flight time (no range penalty); a slow slug accrues it fast. Guided weapons
        /// (high Tracking) correct for it. Only applies once a closing separation is set (Phase 1+); at separation
        /// 0 the term is inert, so the pre-closing resolve is byte-identical. Tunable like SalvoDamageScale.</summary>
        public const double FlightTimeReference_s = CombatKernel.FlightTimeReference_s;

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

        /// <summary>AMMO burn (Weapons pilot W3): kilograms of magazine drained per JOULE of ammo-fed fire a fleet
        /// delivers in a salvo. A fleet's Kinetic/Explosive (railgun/flak/missile) fire drains its
        /// <see cref="FleetCombatStateDB.AmmoPool_kg"/> by <c>(ammo J/s × dt) × this</c>; when the pool empties those
        /// weapons go silent. Flagged BALANCE value — it's calibrated against the base-mod magazine capacity (W3c) to
        /// set how many salvos a loadout sustains. Inert until a ship carries a magazine (capacity 0 → pool disabled).</summary>
        public static double AmmoBurnKgPerJoule = 1e-4;

        /// <summary>HEAT cooling (Weapons pilot W5): the fraction of a fleet's radiator capacity shed as cooling each
        /// salvo. A fleet's energy weapons add heat to <see cref="FleetCombatStateDB.HeatPool_kJ"/> (Σ HeatPerSecond ×
        /// dt); its radiators remove <c>HeatCapacity × this</c>. Flagged BALANCE value.</summary>
        public static double HeatDissipationFraction = 0.5;

        /// <summary>The floor on the energy-fire throttle when a fleet is overheating (Weapons pilot W5) — even a fleet
        /// with heat far past its radiators still fires this fraction of its energy weapons (they never fully shut off).
        /// Flagged BALANCE value.</summary>
        public static double HeatThrottleFloor = 0.1;

        // --- SHIELD layer (option B, docs/WEAPON-TAXONOMY-DESIGN.md §6) ----------------------------------------
        //
        // A depleting/regenerating energy POOL that soaks incoming fire BEFORE the hull's toughness. The
        // weapon-NATURE matchup is mirrored from the ground GroundDamageMatrix (kinetic soaked best, energy
        // bleeds through, area partly bypasses, exotic anti-shield ignores it) so the two systems stay consistent.
        // ADDITIVE: a fleet with no generator has a 0-capacity pool → the whole shield step is a no-op and combat
        // is byte-identical. ALL four fractions are flagged defaults for the developer's balance pass.

        /// <summary>Fraction of a KINETIC salvo the shield can absorb — mass-at-velocity is what a shield stops best
        /// (ground <c>ShieldEffVsPhysical</c> = 1.0).</summary>
        public const double ShieldSoakVsKinetic = CombatKernel.ShieldSoakVsKinetic;
        /// <summary>Fraction of an ENERGY salvo the shield can absorb — coherent energy overloads and BLEEDS through
        /// a shield (ground <c>ShieldEffVsEnergy</c> = 0.5). This is the Enterprise-vs-Galactica seam: an energy ship
        /// leaks past a kinetic-tuned shield.</summary>
        public const double ShieldSoakVsEnergy = CombatKernel.ShieldSoakVsEnergy;
        /// <summary>Fraction of an EXPLOSIVE (blast/warhead) salvo the shield can absorb — an area detonation partly
        /// bypasses (ground <c>ShieldEffVsArtillery</c> = 0.75).</summary>
        public const double ShieldSoakVsExplosive = CombatKernel.ShieldSoakVsExplosive;
        /// <summary>Fraction of an EXOTIC salvo the shield can absorb — v1 treats all Exotic as the ANTI-SHIELD
        /// archetype (ion/matter-strip), DESIGNED to ignore the shield: 0 = full bypass straight to the hull. (Finer
        /// exotic sub-effects — disable/stun — are the parked exotic-effects build, WEAPON-TAXONOMY §5.)</summary>
        public const double ShieldSoakVsExotic = CombatKernel.ShieldSoakVsExotic;

        // The nature→soak-fraction lookup lives in CombatKernel.ShieldSoakFraction now (resolver merge, slice 2);
        // SoakFractionOf below delegates to the kernel, so there is no CombatEngagement copy of it any more.

        /// <summary>Liveness counter (diagnostic only): how many trigger passes the battle engine has run across the
        /// whole game. The client logs this each heartbeat so a remote review can tell "no battle because nothing's
        /// hostile/in-range/detected" apart from "the battle trigger never fires on play" — a documented open
        /// question (the colony test harness doesn't reliably auto-fire it). Interlocked: systems tick in parallel.</summary>
        public static long TickCount;

        /// <summary>
        /// A SUB-FLEET (fleet component) — its parent is another FLEET, not the faction. Combat enrols only TOP-LEVEL
        /// fleets: a sub-fleet fights AS PART OF its parent, because the parent's recursive <see cref="GetCombatShips"/>
        /// already collects the sub-fleet's ships and applies the sub-fleet's OWN doctrine. Enrolling a sub-fleet
        /// separately would double-count its ships. A normal fleet's parent is the faction entity (carries a
        /// <see cref="FactionInfoDB"/>); a sub-fleet's parent is a plain fleet — that's the discriminator. A default
        /// game has no sub-fleets, so this reads false everywhere and combat is byte-identical.
        /// </summary>
        internal static bool IsSubFleet(Entity fleet)
        {
            if (fleet == null || !fleet.TryGetDataBlob<FleetDB>(out var db)) return false;
            var parent = db.Parent;
            return parent != null && parent.IsValid
                && parent.HasDataBlob<FleetDB>()          // parented to a FLEET...
                && !parent.HasDataBlob<FactionInfoDB>();  // ...not the faction root → it's a sub-fleet
        }

        /// <summary>One trigger pass over a system: engage/join hostile fleets, then step the engagement. Returns
        /// the number of fleets seen. Defensive — built not to throw on normal game state.</summary>
        public static int Tick(EntityManager manager, int deltaSeconds)
        {
            System.Threading.Interlocked.Increment(ref TickCount);
            var allFleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (allFleets.Count == 0) return 0;
            // Only TOP-LEVEL fleets are independent combat units — drop sub-fleets once, here, so the whole Tick
            // (engage pass, members, retreat sweep) ignores them (they fight via their parent's recursion). No-op
            // for a default game (no sub-fleets exist).
            var fleets = new List<Entity>(allFleets.Count);
            foreach (var f in allFleets) if (!IsSubFleet(f)) fleets.Add(f);
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
                    // Weapon-range trigger (client-on / test-off): SEEING isn't FIRING. A battle auto-starts only when
                    // someone's guns can actually reach — proximity alone (the 1 Gm EngagementRange bubble) no longer
                    // starts a fight. An explicit attack order bypasses this (OrderAttack). MUST match NewEngagementImminent.
                    if (RequireWeaponRangeToEngage && !WithinWeaponRange(a, b)) continue;
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
            var allFleets = manager.GetAllEntitiesWithDataBlob<FleetDB>();
            if (allFleets.Count == 0) return false;
            // Mirror Tick: only top-level fleets can form a new engagement (a sub-fleet fights via its parent), so a
            // sub-fleet pairing must NOT force the combat-interrupt fine-step. No-op for a default game.
            var fleets = new List<Entity>(allFleets.Count);
            foreach (var f in allFleets) if (!IsSubFleet(f)) fleets.Add(f);
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
                    // Weapon-range + weapons-release gates MUST mirror Tick's — a pair that can't yet FIRE (out of weapon
                    // range) or WON'T fire (both holding fire) can't form a battle, so it must not force fine-stepping.
                    // Same class of bug as the fog gate below: a mismatch made the master loop crawl at 5 s forever.
                    if (RequireWeaponRangeToEngage && !WithinWeaponRange(a, b)) continue;
                    if (RequireWeaponsReleaseToEngage
                        && FleetDoctrine.PostureOf(a) != EngagementPosture.WeaponsFree
                        && FleetDoctrine.PostureOf(b) != EngagementPosture.WeaponsFree)
                        continue;
                    // Match the engage pass's fog gate (see Tick): if combat is detection-gated, a pair that can't
                    // yet SEE each other is NOT imminent — a battle can't form, so it must not force fine-stepping.
                    // Without this, a fleet parked in range of UNDETECTED hostiles (e.g. yours at Earth, ~384,000 km
                    // from a fogged Luna squadron, inside the 1e9 m EngagementRange) makes this return true forever,
                    // so the master loop drops to 5 s sub-steps permanently and game-time grinds to a crawl — the
                    // "time stopped moving under fog of war" live bug (2026-06-27). The imminent-gate and the
                    // engage-gate MUST agree.
                    if (RequireDetectionToEngage && !(FleetDetects(a, b) || FleetDetects(b, a))) continue;
                    // In range + hostile + both manned + armed (+ detected, if fog-gated). If EITHER isn't yet in
                    // combat, a new entry — and so the interrupt — fires this tick. If both are already engaged, this
                    // pair is an ONGOING fight and does NOT force fine-stepping, so the player keeps their set step.
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

        /// <summary>When true, the auto-trigger only starts a battle once the fleets are within actual WEAPON range
        /// (<see cref="WithinWeaponRange(Entity,Entity)"/>) — NOT merely inside the coarse <see cref="EngagementRange_m"/>
        /// proximity bubble. This is the developer's rule and the v2 weapon-range gate <see cref="EngagementRange_m"/>
        /// was always flagged as the placeholder for: two fleets can SEE each other clear across the system and never
        /// fire; a battle auto-starts only when someone's guns can actually reach — or via an explicit
        /// <see cref="OrderAttack"/>, which bypasses this. Detection lets you SEE; this lets you FIRE. Pairs with
        /// <see cref="RequireWeaponsReleaseToEngage"/> (at least one side Weapons Free) → "in range AND willing to fire."
        /// Default FALSE so every headless fixture (co-located ships / no weapon profiles) is byte-identical; the client
        /// turns it on. MUST be gated in BOTH <see cref="Tick"/> and <see cref="NewEngagementImminent"/> or the clock
        /// and combat disagree (the same rule fog-of-war learned — see NewEngagementImminent).</summary>
        public static bool RequireWeaponRangeToEngage = false;

        /// <summary>When true, combat is a CLOSING fight (Phase 1, docs/FLEET-COMBAT-CLOSING-DESIGN.md): a weapon only
        /// fires if its <see cref="WeaponProfile.Range_m"/> reaches the current gap, and the gap CLOSES each step toward
        /// the FASTER (more maneuverable) side's preferred range — so a faster long-range fleet kites a slower
        /// short-range one, and a faster brawler forces the merge. Default FALSE so every existing combat fixture is
        /// byte-identical: with it off the gap stays 0 and the range-gate is a no-op (a 0-separation or 0/unbounded-range
        /// weapon always fires). v1: ONE shared range per engagement group (per-sub-fleet ranges are Phase 4).</summary>
        public static bool EnableClosingRange = false;

        /// <summary>When true, the closing fight runs on the 2D GROUP PLANE (docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md,
        /// slice S1): at engagement start a battle-local plane is seeded from the fleets' real 3D positions
        /// (<see cref="GroupPlane.SeedFrame"/>) and FROZEN, each fleet's ANCHOR is its projected 2D point (a joiner is
        /// placed with the SAME stored frame, so gaps don't jump as ships die), and <see cref="AdvanceClosing"/> slides
        /// the anchor along the enemy-facing direction instead of only sliding the scalar gap. The scalar
        /// <see cref="FleetCombatStateDB.Separation_m"/> is STILL maintained alongside the plane at S1 (nothing reads the
        /// 2D pair-distance yet — the S2 range gate is where <c>SeparationOf</c>/<c>WithinWeaponRange</c> start reading it),
        /// so this slice is a pure additive layer. Default FALSE so every existing fixture is byte-identical: with it off
        /// no plane data is seeded, <see cref="FleetCombatStateDB.HasFrame"/> stays false, and AdvanceClosing takes the
        /// unchanged scalar path. This EXTENDS <see cref="EnableClosingRange"/> (the plane is the 2D generalization of
        /// closing) — anchor MOVEMENT only runs when BOTH are on, because AdvanceClosing is only called under
        /// <see cref="EnableClosingRange"/>; seeding is gated on this flag alone so it can be tested in isolation.</summary>
        public static bool EnableGroupPlane = false;

        /// <summary>Closing-rate dial (m/s): the gap-change speed of a maximally-maneuverable fleet (evasion 1.0); a
        /// fleet changes the gap proportional to its maneuverability (min evasion over its ships — it moves as one).
        /// Tunable like <see cref="SalvoDamageScale"/>; set 0 to FREEZE the gap (gauge use). RAISED 10× 2026-06-27: at
        /// 100k a low-evasion (~0.05) fleet closed only ~25 km/salvo, so a 10,000 km gap never reached weapons range
        /// and the fight resolved at standoff via unbounded railguns (the developer's "no combat within weapons range"
        /// play-test). 1e6 closes the weapon envelope in a watchable handful of salvos. Live-calibration provisional.</summary>
        public static double ClosingSpeedScale_mps = 1_000_000.0;

        /// <summary>Fallback opening gap (m) when first contact has no usable fleet positions (the multi-party join
        /// path). The 2-fleet <see cref="StartEngagement"/> seeds the real distance instead. LOWERED 2026-06-27 from
        /// 10,000 km to ~1,000 km (missile range) so a fight that falls back to this default OPENS at the outer weapon
        /// envelope — missiles trade immediately, then flak/beam as the fleets close — instead of 10× beyond every
        /// weapon. The "combat happens at weapons range" fix, paired with the closing-rate bump + range falloff.</summary>
        public static double InitialSeparationDefault_m = 1_000_000.0;

        /// <summary>Evasion-INDEPENDENT range-accuracy falloff for ballistic fire (the developer's "a railgun has
        /// infinite range but the chance of a hit falls off with distance"). The pre-existing range term scaled ONLY
        /// by the target's evasion, so a zero-evasion battleship was hit perfectly at ANY range — which let unbounded
        /// railguns resolve a fight at 10,000 km, before anyone closed to beam/flak range. This adds a base miss that
        /// applies even to a sitting target (the firing solution + the target's own orbital drift degrade over a long
        /// flight time), scaled by flight-time and by (1−Tracking) so a beam (≈0 flight time) and a guided weapon
        /// (high Tracking) are barely affected — a dumb slug at long range is. 0 = old evasion-only behaviour.
        /// Forwards to <see cref="CombatKernel.RangeBaseMiss"/> (the resolver merge, slice 2) — the kernel owns the
        /// single dial storage so the ship path and the shared kernel can never read different values; this preserves
        /// the <c>CombatEngagement.RangeBaseMiss</c> read/write API the client/tuning uses.</summary>
        public static double RangeBaseMiss
        {
            get => CombatKernel.RangeBaseMiss;
            set => CombatKernel.RangeBaseMiss = value;
        }

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

        private static string ShipName(Entity ship)
            => ship != null && ship.TryGetDataBlob<NameDB>(out var n) ? n.OwnersName : ("ship #" + (ship?.Id ?? -1));

        /// <summary>Distinct weapon CLASSES in a fire mix, "Railgun + Beam" — the "which weapon" the player asked for.
        /// The resolve aggregates by class (≤4 entries), so this is the real weapon makeup, not an individual mount.</summary>
        private static string DescribeFireMix(List<WeaponProfile> fire)
        {
            if (fire == null || fire.Count == 0) return "no weapons in range";
            var seen = new List<WeaponClass>();
            foreach (var w in fire) if (w.DamagePerSecond > 0 && !seen.Contains(w.Class)) seen.Add(w.Class);
            return seen.Count == 0 ? "no weapons in range" : string.Join(" + ", seen);
        }

        private static string FmtEnergy(double j)
        {
            if (j >= 1e9) return (j / 1e9).ToString("0.##") + " GJ";
            if (j >= 1e6) return (j / 1e6).ToString("0.##") + " MJ";
            if (j >= 1e3) return (j / 1e3).ToString("0.##") + " kJ";
            return j.ToString("0") + " J";
        }

        private static string FmtDist(double m)
        {
            if (m >= 1e9) return (m / 1e9).ToString("0.##") + " Gm";
            if (m >= 1e6) return (m / 1e6).ToString("0.##") + " Mm";
            if (m >= 1e3) return (m / 1e3).ToString("0.#") + " km";
            return m.ToString("0") + " m";
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
            if (EnableGroupPlane)   // Slice S1: lay the 2D battle plane down from BOTH fleets' real positions and freeze it
                SeedPlaneForPair(fleetA, sa, fleetB, sb);
            fleetA.SetDataBlob(sa);
            fleetB.SetDataBlob(sb);
            CombatLog($"{FleetLabel(fleetA)} vs {FleetLabel(fleetB)} — engaged");
            RecordBattleEvent(fleetA, BattleEventType.Engaged, 0, GetFleetShips(fleetA).Count, 0, "vs " + FleetLabel(fleetB));
            RecordBattleEvent(fleetB, BattleEventType.Engaged, 0, GetFleetShips(fleetB).Count, 0, "vs " + FleetLabel(fleetA));
        }

        /// <summary>Player order: this fleet ATTACKS that one — the explicit "engage them" the player wanted when two
        /// fleets sat in range doing nothing (one holding fire, or an enemy that had broken off and the auto-trigger
        /// won't re-grab). COMMITS the attacker: clears any retreat it was on (it's back in the fight), sets it
        /// **Weapons Free** (or it would just keep holding fire), and forces BOTH fleets into combat now — the resolver
        /// + closing model then run the fight (closing to weapons range first if there's a gap). A **direct call**
        /// (like doctrine/EMCON), so it bypasses the auto-trigger's detection/posture/retreat gates: the player is
        /// taking the shot deliberately. No-ops on a friendly target, an empty fleet, or self. Idempotent if a side is
        /// already fighting (joins via <see cref="EnsureInCombat"/> instead of resetting a running engagement).</summary>
        public static void OrderAttack(Entity attacker, Entity target)
        {
            if (attacker == null || !attacker.IsValid || target == null || !target.IsValid || attacker == target) return;
            if (!AreHostile(attacker, target)) return;                       // no attacking your own
            if (GetFleetShips(attacker).Count == 0 || GetFleetShips(target).Count == 0) return;
            // Fog of war: you can't attack what you can't see. CanEngageTarget is "fog off → always; fog on → the
            // attacker DETECTS the target", so with detection on this no-ops on an undetected enemy (the order can't
            // conjure a target out of the dark) — matching the auto-trigger's detection gate.
            if (!CanEngageTarget(attacker, target)) return;

            if (attacker.HasDataBlob<FleetRetreatDB>()) attacker.RemoveDataBlob<FleetRetreatDB>(); // re-commit
            FleetDoctrine.SetEngagementPosture(attacker, EngagementPosture.WeaponsFree);           // actually shoot

            bool aIn = attacker.HasDataBlob<FleetCombatStateDB>();
            bool bIn = target.HasDataBlob<FleetCombatStateDB>();
            if (!aIn && !bIn)
                StartEngagement(attacker, target);   // fresh fight — seeds the closing gap from the real distance
            else
            {
                EnsureInCombat(attacker, target.Id); // one side already in a fight — join it (don't reset state)
                EnsureInCombat(target, attacker.Id);
            }
        }

        /// <summary>The Fleet-window "Engage" button: find the NEAREST hostile fleet in the same system and
        /// <see cref="OrderAttack"/> it. Solves the common "two fleets in range just staring at each other" case
        /// without map-click targeting (picking a SPECIFIC enemy fleet is the follow-up). Returns the fleet it
        /// engaged, or null if no hostile fleet with ships is present.</summary>
        public static Entity OrderAttackNearestHostile(Entity fleet)
        {
            if (fleet == null || !fleet.IsValid || fleet.Manager == null) return null;
            Entity best = null;
            double bestDist = double.MaxValue;
            foreach (var other in fleet.Manager.GetAllEntitiesWithDataBlob<FleetDB>())
            {
                if (other == fleet || other == null || !other.IsValid) continue;
                if (IsSubFleet(other)) continue;   // target the parent fleet, never a sub-fleet component
                if (!AreHostile(fleet, other)) continue;
                if (GetFleetShips(other).Count == 0) continue;
                if (!CanEngageTarget(fleet, other)) continue;   // fog: only target hostiles we actually DETECT
                double d = FleetSeparation(fleet, other);
                if (d < bestDist) { bestDist = d; best = other; }
            }
            if (best != null) OrderAttack(fleet, best);
            return best;
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
                // Seed the closing gap from the REAL distance to the representative opponent, so the resolver evaluates
                // the fight at the SAME range the engage gate (WithinWeaponRange) already approved. Seeding the 1000 km
                // default here — after the engage gate had confirmed the pair is within ACTUAL weapon range — made every
                // finite-range weapon (beam knife-range, flak 50 km, railgun 500 km; the scenario ships carry no 1000 km
                // missile) read OUT of range → an EMPTY fire mix → totalFire 0 → the engagement was released as "frozen"
                // the SAME tick it formed → next tick the (still-real-in-range) pair read imminent again → engage → freeze
                // → release: a per-tick THRASH that kept NewEngagementImminent true forever (re-arming the combat fine-
                // stepper every game-hour → the SensorScan "PERF freeze") and re-fired the auto-pause endlessly. Seeding
                // the real gap lets the fight actually resolve, so it PERSISTS and the pair stops reading imminent.
                // FleetSeparation self-guards (falls back to the default when a fleet has no usable position), and the
                // whole block is inert in CI (EnableClosingRange is off in every headless fixture → byte-identical).
                // (2026-07-16 freeze fix — the engage gate judged real distance while the resolver judged the default.)
                double gap = InitialSeparationDefault_m;
                var mgr = fleet.Manager;
                if (mgr != null && mgr.TryGetEntityById(representativeOpponentId, out var opponent)
                    && opponent != null && opponent.IsValid)
                    gap = FleetSeparation(fleet, opponent);
                st.Separation_m = gap;                                 // Phase 1: the real gap, not the default seed
                st.ManeuverBudget = FleetCombat.DeltaVFloor(fleet);    // Phase 2: the kiting clock
            }
            if (EnableGroupPlane)   // Slice S1: copy the frozen plane from an engaged sibling, or seed a fresh one (join path)
                SeedPlaneForJoiner(fleet, st, representativeOpponentId);
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

            // --- AMMO phase (W3): each fleet's AMMO-FED weapons (Kinetic/Explosive — railgun/flak/missile) drain its
            // magazine pool this salvo; when the pool is dry those weapons go SILENT (dropped from its outgoing fire)
            // and it fights on with energy weapons. Runs BEFORE the damage phase so a dry fleet's silenced fire never
            // reaches its targets this step. GATED on the fleet actually carrying a magazine (capacity > 0): a fleet
            // with no magazine keeps AmmoPool_kg disabled and its fire untouched, so combat is byte-identical (every
            // current ship, until the W3c base-mod magazine). One aggregate pool per fleet, mirroring the shield.
            for (int i = 0; i < n; i++)
            {
                if (!live[i].TryGetDataBlob<FleetCombatStateDB>(out var ammoState)) continue;
                double ammoCap = FleetAmmoCapacity(ships[i]);
                if (ammoCap <= 0) continue;                                  // no magazine → disabled → byte-identical
                if (ammoState.AmmoPool_kg < 0) ammoState.AmmoPool_kg = ammoCap;          // lazy seed: full at first contact
                if (ammoState.AmmoPool_kg > ammoCap) ammoState.AmmoPool_kg = ammoCap;    // ships lost this fight shrink the pool
                if (ammoState.AmmoPool_kg <= 0)
                {
                    SilenceAmmoWeapons(fire[i]);                             // dry → ammo weapons stop firing
                    continue;
                }
                double ammoJoules = AmmoFireDamage(fire[i]) * dt;           // ammo-fed damage delivered this salvo
                ammoState.AmmoPool_kg -= ammoJoules * AmmoBurnKgPerJoule;
                if (ammoState.AmmoPool_kg < 0) ammoState.AmmoPool_kg = 0;
            }

            // --- HEAT phase (W5): each fleet's ENERGY-weapon fire cooks the ship; its radiators shed heat each salvo.
            // When the heat pool outruns the radiators the energy weapons THROTTLE (burst-vs-sustained), toward the
            // floor. SELF-GATING: a fleet whose weapons generate no tracked heat (every current weapon → HeatPerSecond 0)
            // keeps HeatPool at 0 and is skipped, so combat is byte-identical. A HOT weapon (W5c) without enough
            // radiators throttles; more radiators sustain more fire. Runs before the damage phase so a throttled fleet's
            // reduced energy fire is what reaches its targets this step. Kinetic/explosive fire is untouched (it burns
            // ammo, not heat — the two limits are independent).
            for (int i = 0; i < n; i++)
            {
                if (!live[i].TryGetDataBlob<FleetCombatStateDB>(out var heatState)) continue;
                double heatGen = EnergyHeatGen(fire[i]);
                if (heatGen <= 0 && heatState.HeatPool_kJ <= 0) continue;   // no heat in play → byte-identical
                double heatCap = FleetHeatCapacity(ships[i]);
                heatState.HeatPool_kJ += heatGen * dt;                      // energy fire heats the ship
                heatState.HeatPool_kJ -= heatCap * HeatDissipationFraction; // radiators shed heat
                if (heatState.HeatPool_kJ < 0) heatState.HeatPool_kJ = 0;
                if (heatState.HeatPool_kJ > heatCap)                        // overheating → throttle the energy guns
                {
                    double throttle = heatState.HeatPool_kJ > 0 ? heatCap / heatState.HeatPool_kJ : 1.0;
                    if (throttle < HeatThrottleFloor) throttle = HeatThrottleFloor;
                    ThrottleEnergyFire(fire[i], throttle);
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
                // POINT-DEFENSE (W6): this fleet's PD screen intercepts a saturating fraction of the incoming GUIDED
                // (missile) fire, shooting those missiles out of the salvo before it's totalled — so a big anti-missile
                // screen shrugs off a light strike but a swarm saturates it and leaks through. GATED on the fleet
                // actually carrying PD (rating > 0): a PD-less fleet leaves `incoming` untouched, so combat is
                // byte-identical (every current ship, until the W6c PD mount faces a missile ship). Non-guided fire
                // (beams/slugs/flak) is never intercepted.
                double pdRating = FleetPointDefense(ships[i]);
                if (pdRating > 0) InterceptMissiles(incoming, pdRating);
                // SalvoDamageScale is the combat-pace dial: only this fraction of the raw salvo energy counts
                // toward kills, so battles play out over many salvos instead of ending in 2–4 (see the const).
                double dmgThisSalvo = TotalDamage(incoming) * dt * SalvoDamageScale;
                // SHIELD (option B): the fleet's shield pool soaks part of the salvo before it reaches the hull, with
                // the nature matchup (kinetic soaked, energy bleeds, exotic bypasses). No-op — and byte-identical —
                // for an unshielded fleet (0 capacity). The exact aggregate `dmgThisSalvo` above is preserved so the
                // unshielded path is untouched to the bit; the shield only ever SUBTRACTS what it absorbs.
                dmgThisSalvo = ApplyShield(live[i], ships[i], state, incoming, dt, dmgThisSalvo);
                // NATURE-HARDENED ARMOUR (⚙3, the ship mirror of the ground armour-nature): AFTER the shield, the
                // defending fleet's hardened plating soaks a fraction of what leaks through, by the incoming salvo's
                // NATURE — an ablative-clad fleet shrugs off an energy salvo, a composite one walls kinetic. GATED on the
                // fleet actually carrying hardening (fraction > 0); a plain-armour fleet leaves dmgThisSalvo untouched →
                // byte-identical (every current ship, until a nature-hardened plate is fitted).
                double armourSoak = FleetArmourSoakFraction(ships[i], incoming);
                if (armourSoak > 0) dmgThisSalvo *= (1.0 - armourSoak);
                state.DamageTakenPool += dmgThisSalvo;
                string attackerLabel = FleetLabel(live[attackersOf[i][0]])
                    + (attackersOf[i].Count > 1 ? " +" + (attackersOf[i].Count - 1) + " more" : "");
                // Pass the defender's gap so ballistic fire loses accuracy at range (the range term in HitFraction).
                // SeparationOf is 0 when closing is off, so this is inert for the pre-closing resolve. dmgThisSalvo +
                // attackerLabel feed the per-salvo play-by-play (which weapon / hit-rate / damage / which ship).
                ApplyCasualties(ships[i], state, incoming, SeparationOf(live[i]), dmgThisSalvo, attackerLabel); // prunes the dead
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
        private static void ApplyCasualties(List<CombatShip> ships, FleetCombatStateDB state, List<WeaponProfile> incomingFire, double separation_m = 0, double damageThisSalvo = 0, string attackerLabel = null)
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
                    double landed = LandedFraction(incomingFire, cv.Evasion, separation_m);
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

            // Ship-count-weighted average "fraction that got through the dodge" — the hit-vs-evade rate for the readout.
            double landedSum = 0; int shipCount = 0;
            foreach (var b in buckets.Values) { landedSum += b.Landed * b.Ships.Count; shipCount += b.Ships.Count; }
            double avgLanded = shipCount > 0 ? landedSum / shipCount : 0;

            var ordered = new List<CasualtyBucket>(buckets.Values);
            ordered.Sort((x, y) =>
            {
                int byRole = y.RoleWeight.CompareTo(x.RoleWeight);          // combatants before utility
                return byRole != 0 ? byRole : y.Landed.CompareTo(x.Landed); // then most-hittable first
            });

            int totalKilled = 0;
            var destroyedNames = new List<string>();   // the "which ship" detail the player asked for
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
                    for (int i = 0; i < kills; i++) { destroyedNames.Add(ShipName(b.Ships[i])); b.Ships[i].Destroy(); }
                    totalKilled += kills;
                }
                if (kills < b.Ships.Count) break; // pool ran out inside this bucket -> stop (matches per-ship break)
            }

            // Drop the dead (Destroy() flips IsValid synchronously) so the caller's survivor count is accurate.
            ships.RemoveAll(cs => !cs.Ship.IsValid);

            // The per-salvo PLAY-BY-PLAY (the developer's "salvo means nothing — tell me which weapon, hit/evade,
            // damage, which ship"). From the aggregate resolve we can honestly report: the weapon CLASSES firing, the
            // hit-vs-dodge RATE, the damage dealt, and the ships DESTROYED by name. Per-component loss + per-ship hull%
            // are NOT in this model (ships are whole-or-dead in v1) — that needs the parked per-component damage sim.
            //
            // VOLUME (the flooding fix): the FULL per-salvo play-by-play goes to the CONSOLE every salvo a fleet takes
            // fire (when NarrateToLog is on — the client sets it true, so game_logs has the blow-by-blow). The capped
            // (250) persistent Battle Report only keeps the BEATS — a salvo that DESTROYS a ship — with the rich note,
            // so a long battle's report shows the kills + how the fight opened/ended, not 240 "no losses" lines that
            // evict the Engaged event. Narration-off (headless tests + perf sims) records casualty salvos only =
            // unchanged. Note built only when something will use it.
            if (totalKilled > 0 || NarrateToLog)
            {
                string rangeStr = separation_m > 0 ? " at " + FmtDist(separation_m) : "";
                string outcome = totalKilled > 0
                    ? "destroyed " + string.Join(", ", destroyedNames.ConvertAll(d => "'" + d + "'"))
                    : "no losses";
                string note = "took " + DescribeFireMix(incomingFire) + " fire" + rangeStr
                    + " from " + (attackerLabel ?? "enemy")
                    + " — " + (avgLanded * 100).ToString("0") + "% on target ("
                    + ((1 - avgLanded) * 100).ToString("0") + "% dodged), " + FmtEnergy(damageThisSalvo) + " dealt; "
                    + outcome + "; " + ships.Count + " left";

                if (NarrateToLog)   // full blow-by-blow → console / game_logs
                    CombatLog($"salvo {state.StepsFought}: {FleetLabel(state.OwningEntity)} {note}");
                if (totalKilled > 0)   // only the casualty beats → the capped persistent Battle Report (no flooding)
                    RecordBattleEvent(state.OwningEntity, BattleEventType.Salvo, totalKilled, ships.Count, state.StepsFought, note);
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
        /// makes the range-gate a no-op). Slice S2 (2D group plane, docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §13):
        /// when <see cref="EnableGroupPlane"/> is on and the plane is live for BOTH this fleet and its representative
        /// opponent, the gap is the straight-line 2D pair-distance between their group anchors — the REAL per-fleet-pair
        /// gap (per-sub-fleet gaps become real, the substrate's deferred "Phase 4"), no longer one shared scalar. It
        /// falls back to the scalar <see cref="FleetCombatStateDB.Separation_m"/> otherwise (un-framed / opponent gone).
        /// The kernel stays 1-D: the plane only supplies the single scalar <c>d</c> the unchanged firing code reads.
        /// Still gated on <see cref="EnableClosingRange"/> FIRST, so every closing-off fixture — and a plane seeded with
        /// closing off — reads 0 and the range gate is a no-op → byte-identical.</summary>
        private static double SeparationOf(Entity fleet)
        {
            if (!EnableClosingRange || !fleet.TryGetDataBlob<FleetCombatStateDB>(out var st)) return 0;
            if (EnableGroupPlane && TryPlanePairDistance(fleet, st, out double d)) return d;
            return st.Separation_m;
        }

        /// <summary>Slice S2: the 2D pair-distance between a fleet's group anchor and its representative opponent's, on
        /// the frozen battle plane. Succeeds (setting <paramref name="distance"/>) only when the plane is live
        /// (<see cref="FleetCombatStateDB.HasFrame"/>) for BOTH fleets, so an un-framed fleet or a departed opponent
        /// falls back to the scalar gap. Reads only the two stored anchors — order-independent, never mutates state,
        /// never throws — and hands the caller a single scalar (the design's "the plane produces one distance; the 1-D
        /// kernel is unchanged"). S1/S2: one group per fleet, so the group-pair is the fleet-pair; S3 fills per-role.</summary>
        private static bool TryPlanePairDistance(Entity fleet, FleetCombatStateDB st, out double distance)
        {
            distance = 0;
            if (!st.HasFrame) return false;
            var mgr = fleet.Manager;
            if (mgr == null || !mgr.TryGetEntityById(st.OpponentFleetId, out var opp)
                || opp == null || !opp.IsValid) return false;
            if (!opp.TryGetDataBlob<FleetCombatStateDB>(out var os) || !os.HasFrame) return false;
            distance = GroupPlane.PairDistance(st.Anchor, os.Anchor);
            return true;
        }

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

            // Slice S1: capture the controller's PRE-move scalar gap so the plane knows how far its anchor slid this step
            // (the whole plane branch is inert unless EnableGroupPlane; otherwise this is the unchanged scalar closing).
            double ctrlGapBefore = 0;
            if (EnableGroupPlane && live[controller].TryGetDataBlob<FleetCombatStateDB>(out var ctrlPre))
                ctrlGapBefore = ctrlPre.Separation_m;

            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].IsValid || !live[i].TryGetDataBlob<FleetCombatStateDB>(out var st)) continue;
                double r = st.Separation_m;
                if (r > desired) r = System.Math.Max(desired, r - step);
                else if (r < desired) r = System.Math.Min(desired, r + step);
                st.Separation_m = r;
            }

            // Slice S1: slide the controller's ANCHOR along its enemy-facing direction to track the scalar gap change
            // ("the faster side closes the distance"). Additive — the scalar Separation_m above is still the number every
            // downstream reader uses at S1; the anchor geometry runs beside it until the S2 range gate reads the 2D gap.
            if (EnableGroupPlane)
                AdvanceAnchorPlane(live, controller, ctrlGapBefore);
        }

        /// <summary>Slice S1 (2D group plane): move the CONTROLLER fleet's anchor along the direction toward its nearest
        /// enemy anchor by however far its scalar gap just changed — closing toward the enemy (positive) or opening away
        /// (negative). Only the faster (controller) fleet moves, matching the design's "the faster side closes the
        /// distance," so for the 2-fleet case the controller↔enemy pair-distance tracks the scalar gap exactly. Reads all
        /// enemy anchors and mutates ONLY the controller's, so it is order-independent and deterministic (the direction
        /// itself has a lowest-id tie-break inside <see cref="GroupPlane.EnemyDirection"/>). Defensive — never throws.</summary>
        private static void AdvanceAnchorPlane(List<Entity> live, int controller, double ctrlGapBefore)
        {
            if (controller < 0 || controller >= live.Count) return;
            var ctrl = live[controller];
            if (ctrl == null || !ctrl.IsValid || !ctrl.TryGetDataBlob<FleetCombatStateDB>(out var cst) || !cst.HasFrame)
                return;

            // The enemy anchors on the frozen plane = the in-combat, hostile, already-seeded fleets.
            var enemies = new List<(int Id, Vector2 Anchor)>();
            for (int i = 0; i < live.Count; i++)
            {
                if (i == controller) continue;
                var e = live[i];
                if (e == null || !e.IsValid || !AreHostile(ctrl, e)) continue;
                if (e.TryGetDataBlob<FleetCombatStateDB>(out var est) && est.HasFrame)
                    enemies.Add((e.Id, est.Anchor));
            }

            Vector2 dir = GroupPlane.EnemyDirection(cst.Anchor, enemies);   // unit toward the nearest enemy (Zero if none)
            double moved = ctrlGapBefore - cst.Separation_m;               // +ve when the gap SHRANK (closing) → step toward enemy
            Vector2 newAnchor = GroupPlane.Place(cst.Anchor, dir * moved);  // −ve moved carries it AWAY from the enemy (kiting)
            cst.Anchor = newAnchor;
            if (cst.GroupPositions.Count > 0) cst.GroupPositions[0] = newAnchor;   // S1: the one whole-fleet group tracks the anchor
            else cst.GroupPositions = new List<Vector2> { newAnchor };
        }

        /// <summary>Slice S1 helper: reconstruct a <see cref="GroupPlane.BattleFrame"/> from a fleet-state's stored axes.</summary>
        private static GroupPlane.BattleFrame FrameOf(FleetCombatStateDB st)
            => new GroupPlane.BattleFrame(st.FrameOrigin, st.FrameXAxis, st.FrameYAxis);

        /// <summary>Slice S1 helper: store a frozen frame + this fleet's projected anchor onto its combat state, marking
        /// the plane live. The single whole-fleet group (S1) sits at the anchor.</summary>
        private static void StoreFrame(FleetCombatStateDB st, GroupPlane.BattleFrame frame, Vector2 anchor)
        {
            st.FrameOrigin = frame.Origin;
            st.FrameXAxis = frame.XAxis;
            st.FrameYAxis = frame.YAxis;
            st.Anchor = anchor;
            if (st.GroupPositions.Count > 0) st.GroupPositions[0] = anchor;
            else st.GroupPositions = new List<Vector2> { anchor };
            st.HasFrame = true;
        }

        /// <summary>Slice S1: seed the frozen 2D plane for a fresh two-fleet engagement — lay it down ONCE from BOTH
        /// fleets' real positions (order-independent, so it's the same board whichever fleet is A) and project each
        /// fleet's anchor onto it. Defensive — a positionless fleet gets a Zero anchor rather than throwing.</summary>
        private static void SeedPlaneForPair(Entity a, FleetCombatStateDB sa, Entity b, FleetCombatStateDB sb)
        {
            bool hasA = TryGetFleetPosition(a, out var pa);
            bool hasB = TryGetFleetPosition(b, out var pb);
            var seeds = new List<(int Id, Vector3 Position)>();
            if (hasA) seeds.Add((a.Id, pa));
            if (hasB) seeds.Add((b.Id, pb));
            var frame = GroupPlane.SeedFrame(seeds);
            StoreFrame(sa, frame, hasA ? GroupPlane.Project(frame, pa) : Vector2.Zero);
            StoreFrame(sb, frame, hasB ? GroupPlane.Project(frame, pb) : Vector2.Zero);
        }

        /// <summary>Slice S1: seed the plane for a fleet ENTERING an engagement via the join primitive. Prefers to COPY
        /// the FROZEN frame from an already-engaged sibling (one system = one battle → one shared board — the lowest-id
        /// engaged fleet wins, deterministically), projecting this fleet's own anchor onto it. If none is framed yet (this
        /// is the FIRST fleet into the fight through the auto-trigger's <see cref="Tick"/> path, which enrols BOTH sides
        /// via EnsureInCombat rather than StartEngagement), seed a fresh frame from THIS fleet + its representative
        /// opponent — because <see cref="GroupPlane.SeedFrame"/> is order-independent, the sibling that copies it later
        /// lands on the identical board. Defensive — never throws.</summary>
        private static void SeedPlaneForJoiner(Entity fleet, FleetCombatStateDB st, int representativeOpponentId)
        {
            var mgr = fleet.Manager;
            if (mgr != null)
            {
                Entity framed = null;
                FleetCombatStateDB framedState = null;
                foreach (var other in mgr.GetAllEntitiesWithDataBlob<FleetCombatStateDB>())
                {
                    if (other == null || other == fleet || !other.IsValid) continue;
                    if (!other.TryGetDataBlob<FleetCombatStateDB>(out var os) || !os.HasFrame) continue;
                    if (framed == null || other.Id < framed.Id) { framed = other; framedState = os; }   // lowest-id → deterministic
                }
                if (framedState != null)
                {
                    var frame = FrameOf(framedState);
                    StoreFrame(st, frame, TryGetFleetPosition(fleet, out var pf) ? GroupPlane.Project(frame, pf) : Vector2.Zero);
                    return;
                }
            }

            // First fleet into the fight: build the two-fleet board from THIS fleet + its representative opponent.
            bool hasSelf = TryGetFleetPosition(fleet, out var self);
            var seeds = new List<(int Id, Vector3 Position)>();
            if (hasSelf) seeds.Add((fleet.Id, self));
            if (mgr != null && mgr.TryGetEntityById(representativeOpponentId, out var opp)
                && opp != null && opp.IsValid && TryGetFleetPosition(opp, out var oppPos))
                seeds.Add((opp.Id, oppPos));
            var seeded = GroupPlane.SeedFrame(seeds);
            StoreFrame(st, seeded, hasSelf ? GroupPlane.Project(seeded, self) : Vector2.Zero);
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

        /// <summary>Pure weapon-range test: given the real separation and each fleet's longest weapon reach, can at
        /// least ONE side's guns reach the other? Uses the LONGER of the two reaches — a long-range fleet opens the
        /// fight at its range (the shorter side just can't shoot back until they close, which the directed-fire resolve
        /// already handles). An unbounded (rangeless) reach (∞) reaches any gap; a pair with no armed reach (0) can't
        /// start a fight. Split out pure so it's deterministically unit-testable (no positions), like HitFraction.</summary>
        internal static bool WithinWeaponRange(double separation_m, double reachA, double reachB)
        {
            double reach = System.Math.Max(reachA, reachB);
            if (double.IsPositiveInfinity(reach)) return true;   // an unbounded weapon reaches any gap (fallback/old-style)
            if (reach <= 0) return false;                        // neither side has a weapon with reach → nothing to start
            return separation_m <= reach;
        }

        /// <summary>Are the two fleets within actual weapon range of each other — the entity-level gate for
        /// <see cref="RequireWeaponRangeToEngage"/>. Resolves each fleet's <see cref="MaxReach"/> and their real
        /// <see cref="FleetSeparation"/> and defers to the pure overload. (FleetSeparation falls back to
        /// <see cref="InitialSeparationDefault_m"/> when a fleet has no usable position, so a positionless test pair is
        /// treated as ~missile range apart rather than co-located.)</summary>
        internal static bool WithinWeaponRange(Entity a, Entity b)
            => WithinWeaponRange(RangeBetween(a, b), MaxReach(GetCombatShips(a)), MaxReach(GetCombatShips(b)));

        /// <summary>Slice S2: the distance the weapon-range gate measures between two fleets. On the 2D group plane
        /// (<see cref="EnableGroupPlane"/> on) with BOTH fleets already framed, it is the anchor pair-distance — the real
        /// gap the fight has closed to on the plane; otherwise the real 3D <see cref="FleetSeparation"/>. Flag-off, or
        /// either fleet not yet framed (the engage-trigger runs BEFORE the plane is seeded, so a not-yet-in-combat pair
        /// falls straight through here), returns FleetSeparation unchanged → byte-identical. At engagement START the
        /// anchors are the projections of the same 3D positions, so the two distances agree until closing slides them.</summary>
        private static double RangeBetween(Entity a, Entity b)
        {
            if (EnableGroupPlane
                && a.TryGetDataBlob<FleetCombatStateDB>(out var sa) && sa.HasFrame
                && b.TryGetDataBlob<FleetCombatStateDB>(out var sb) && sb.HasFrame)
                return GroupPlane.PairDistance(sa.Anchor, sb.Anchor);
            return FleetSeparation(a, b);
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

        // Internal (not private) so the aggregation invariant is unit-testable (WeaponClassifierTests) — the codebase's
        // internal-for-test pattern (cf. HitFraction / SoakFractionOf / WithinWeaponRange).
        internal static List<WeaponProfile> BuildFireMix(List<CombatShip> ships, double separation_m = 0)
        {
            // (class, NATURE, DELIVERY) -> (total damage, damage-weighted velocity, tracking, saturation). Bucketing on
            // BOTH axes keeps the full two-axis identity alive through the aggregation: Nature drives the SHIELD matchup
            // (kinetic vs energy vs exotic soak differently), and Delivery is what the computed `WeaponProfile.Class`
            // reads — so the emergent corner survives aggregation; without it a missile aggregated to the default Slug
            // delivery would misclassify as a railgun.
            // class→nature→delivery is 1:1 today, so the bucket count is unchanged and the resolve is byte-identical.
            // Heat (W5) is SUMMED into the bucket (total waste-heat rate), NOT damage-weighted like vel/trk/sat — two
            // hot beams generate twice the heat. Carried through so the aggregated mix keeps HeatPerSecond (else the
            // heat throttle never sees it). 0 for every current weapon → byte-identical.
            var byClass = new Dictionary<(WeaponClass cls, WeaponNature nat, WeaponDelivery del), (double dmg, double velW, double trkW, double satW, double heat)>();
            void Add(WeaponClass cls, WeaponNature nat, WeaponDelivery del, double dmg, double vel, double trk, double sat, double heat)
            {
                if (dmg <= 0) return;
                var key = (cls, nat, del);
                byClass.TryGetValue(key, out var e);
                byClass[key] = (e.dmg + dmg, e.velW + vel * dmg, e.trkW + trk * dmg, e.satW + sat * dmg, e.heat + heat);
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
                        Add(w.Class, w.Nature, w.Delivery, w.DamagePerSecond * cs.FirepowerMult, w.Velocity, w.Tracking, w.Saturation, w.HeatPerSecond);
                    }
                }
                else if (cv.Firepower > 0)
                {
                    // Old-style combat value (firepower, no profiles): fires as a light-speed always-hits beam — an
                    // ENERGY beam, so it half-bleeds a shield (consistent with "fires as a beam"). No shield present in
                    // the pre-shield fixtures, so this nature choice can't change any existing outcome.
                    Add(WeaponClass.Beam, WeaponNature.Energy, WeaponDelivery.Beam, cv.Firepower * cs.FirepowerMult, FallbackBeamVelocity_mps, 1.0, double.PositiveInfinity, 0);
                }
            }

            var mix = new List<WeaponProfile>(byClass.Count);
            foreach (var kv in byClass)
            {
                double d = kv.Value.dmg;
                // penetration/perShotEnergy are ground-side (the ship salvo folds armour into Toughness), so 0 here;
                // heatPerSecond (W5) is carried so the aggregated mix still heats the ship.
                mix.Add(new WeaponProfile(d, kv.Value.velW / d, kv.Value.trkW / d, kv.Value.satW / d, 0, kv.Key.nat, kv.Key.del, 0, 0, kv.Value.heat));
            }
            return mix;
        }

        private static double TotalDamage(List<WeaponProfile> fire)
        {
            double sum = 0;
            foreach (var w in fire) sum += w.DamagePerSecond;
            return sum;
        }

        // ─── Shield layer (option B) ─────────────────────────────────────────────────────────────────────────────

        /// <summary>A fleet's total shield generator capacity + regen (joules, joules/sec), summed health-scaled over
        /// its ships' cached <see cref="ShipCombatValueDB"/>. 0/0 for an unshielded fleet. v1: raw capacity — NOT
        /// scaled by doctrine (a "shields hold better on the defensive" dial is a flagged follow-up).</summary>
        private static (double capacity, double regen) FleetShield(List<CombatShip> ships)
        {
            double cap = 0, regen = 0;
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                cap += cv.ShieldCapacity_J;
                regen += cv.ShieldRegen_Jps;
            }
            return (cap, regen);
        }

        // ─── Ammo layer (Weapons pilot W3) ───────────────────────────────────────────────────────────────────────────

        /// <summary>Is this weapon AMMO-FED (draws from a magazine) rather than powered? Kinetic (railgun/flak slugs) and
        /// Explosive (missiles/warheads) consume physical ammo; Energy (beams/plasma) and Exotic (disruptors) draw power,
        /// not ammo. So a fleet that runs dry keeps fighting with its energy weapons — the ship echo of the ground rule.</summary>
        internal static bool IsAmmoNature(WeaponNature nature) => nature == WeaponNature.Kinetic || nature == WeaponNature.Explosive;

        /// <summary>The damage/sec in a fire mix that comes from AMMO-FED weapons (the part that drains a magazine).</summary>
        internal static double AmmoFireDamage(List<WeaponProfile> fire)
        {
            double sum = 0;
            foreach (var w in fire) if (IsAmmoNature(w.Nature)) sum += w.DamagePerSecond;
            return sum;
        }

        /// <summary>A fleet's total ammo-magazine capacity (kg), summed health-scaled over its ships' cached
        /// <see cref="ShipCombatValueDB.AmmoCapacity_kg"/>. 0 for a fleet with no magazine (→ ammo pool disabled).</summary>
        private static double FleetAmmoCapacity(List<CombatShip> ships)
        {
            double cap = 0;
            foreach (var cs in ships) cap += CombatValue(cs.Ship).AmmoCapacity_kg;
            return cap;
        }

        /// <summary>Silence a DRY fleet's ammo-fed weapons — drop the Kinetic/Explosive profiles from its outgoing fire
        /// so only its energy weapons still contribute. Mutates the passed mix in place.</summary>
        private static void SilenceAmmoWeapons(List<WeaponProfile> fire) => fire.RemoveAll(w => IsAmmoNature(w.Nature));

        // ─── Heat layer (Weapons pilot W5) ───────────────────────────────────────────────────────────────────────────

        /// <summary>The waste-heat generation rate (kJ/s) of a fire mix — Σ each weapon's <see cref="WeaponProfile.HeatPerSecond"/>
        /// (only energy weapons carry it). 0 for a mix of "cool" weapons → the fleet never heats up (byte-identical).</summary>
        internal static double EnergyHeatGen(List<WeaponProfile> fire)
        {
            double sum = 0;
            foreach (var w in fire) sum += w.HeatPerSecond;
            return sum;
        }

        /// <summary>A fleet's total heat-radiator capacity (kJ), summed health-scaled over its ships' cached
        /// <see cref="ShipCombatValueDB.HeatCapacity_kJ"/>. 0 for a fleet with no radiator (→ energy fire throttles hard
        /// if it runs any hot weapon; but with no hot weapon there's no heat to throttle → byte-identical).</summary>
        private static double FleetHeatCapacity(List<CombatShip> ships)
        {
            double cap = 0;
            foreach (var cs in ships) cap += CombatValue(cs.Ship).HeatCapacity_kJ;
            return cap;
        }

        /// <summary>Throttle an OVERHEATING fleet's ENERGY-fed weapons — scale the Energy/Exotic profiles' damage by the
        /// throttle factor (kinetic/explosive fire is unaffected, it burns ammo not heat). Mutates the mix in place.</summary>
        private static void ThrottleEnergyFire(List<WeaponProfile> fire, double throttle)
        {
            foreach (var w in fire)
                if (!IsAmmoNature(w.Nature)) w.DamagePerSecond *= throttle;
        }

        // ─── Point-defense layer (Weapons pilot W6) ──────────────────────────────────────────────────────────────────

        /// <summary>Hard ceiling on the fraction of an incoming missile salvo point-defense can intercept — nothing is
        /// ever fully immune, so a big enough swarm always leaks something through (mirrors <see
        /// cref="ShipCombatValueDB.EvasionCap"/> and <see cref="MinLandedFraction"/>). Flagged BALANCE dial.</summary>
        public static double PointDefenseMaxIntercept = 0.95;

        /// <summary>Is this weapon INTERCEPTABLE by point-defense — a discrete GUIDED projectile (a missile) you can shoot
        /// down on its way in? A beam/slug/bolt is not a thing PD can knock out. Keyed on <see cref="WeaponDelivery.Guided"/>,
        /// which survives the fire-mix aggregation (BuildFireMix buckets on Delivery), so the missile fraction is legible
        /// in the aggregated incoming fire.</summary>
        internal static bool IsInterceptable(WeaponDelivery delivery) => delivery == WeaponDelivery.Guided;

        /// <summary>The damage/sec in a fire mix that comes from INTERCEPTABLE (guided/missile) weapons — the part
        /// point-defense can shoot down.</summary>
        internal static double MissileFireDamage(List<WeaponProfile> fire)
        {
            double sum = 0;
            foreach (var w in fire) if (IsInterceptable(w.Delivery)) sum += w.DamagePerSecond;
            return sum;
        }

        /// <summary>A fleet's total point-defense intercept rating (J/s), summed health-scaled over its ships' cached
        /// <see cref="ShipCombatValueDB.PointDefense_Jps"/>. 0 for a fleet with no PD (→ the intercept step is skipped and
        /// incoming fire is byte-identical).</summary>
        internal static double FleetPointDefense(List<CombatShip> ships)
        {
            double pd = 0;
            foreach (var cs in ships) pd += CombatValue(cs.Ship).PointDefense_Jps;
            return pd;
        }

        /// <summary>The fraction of an incoming missile salvo a fleet's point-defense intercepts — a SATURATING curve:
        /// <c>pdRating / (pdRating + missileDamage)</c>, capped at <see cref="PointDefenseMaxIntercept"/>. Lots of PD vs a
        /// light salvo → most is stopped; a swarm big enough to out-mass the PD → it saturates and leaks through. Returns
        /// 0 when there's no PD or no missile fire (→ no interception, byte-identical). Pure; internal so it's directly
        /// unit-testable like <see cref="HitFraction"/>.</summary>
        internal static double PointDefenseInterceptFraction(double pdRating, double missileDamage)
        {
            if (pdRating <= 0 || missileDamage <= 0) return 0;
            double frac = pdRating / (pdRating + missileDamage);
            return frac > PointDefenseMaxIntercept ? PointDefenseMaxIntercept : frac;
        }

        /// <summary>Reduce the GUIDED (missile) portion of an incoming fire mix by a fleet's point-defense intercept
        /// fraction — the PD shoots those missiles out of the salvo before they reach the hull. Mutates the mix in place;
        /// non-guided fire (beams/slugs/flak) is untouched. No-op when there's no guided fire OR no interception, so a
        /// non-missile salvo (every current fight, until a missile ship faces PD) is byte-identical. Returns the joules/sec
        /// of missile fire intercepted (0 if none) for the readout.</summary>
        private static double InterceptMissiles(List<WeaponProfile> incoming, double pdRating)
        {
            double missileDamage = MissileFireDamage(incoming);
            if (missileDamage <= 0) return 0;
            double frac = PointDefenseInterceptFraction(pdRating, missileDamage);
            if (frac <= 0) return 0;
            double survive = 1.0 - frac;
            foreach (var w in incoming)
                if (IsInterceptable(w.Delivery)) w.DamagePerSecond *= survive;
            return missileDamage * frac;
        }

        /// <summary>The damage-weighted fraction of an incoming fire mix a shield CAN stop — the nature matchup rolled
        /// up over the salvo (all-kinetic → 1.0, all-energy → 0.5, all-exotic → 0.0, mixes interpolate). Pure; internal
        /// so it's directly unit-testable like <see cref="HitFraction"/>.</summary>
        internal static double SoakFractionOf(List<WeaponProfile> incoming) => CombatKernel.SoakFractionOf(incoming);

        /// <summary>The fraction of an incoming salvo a defending fleet's NATURE-HARDENED plating soaks (⚙3, the ship
        /// mirror of the ground armour-nature) — the TOUGHNESS-weighted fleet average of the ships' per-nature armour
        /// soak, then weighted across the salvo's damage-by-nature mix (the same shape as <see cref="SoakFractionOf"/>).
        /// Returns 0 (no reduction, byte-identical) when no ship carries hardening, or the fleet/salvo is empty. Applied
        /// AFTER the shield step. Internal for direct unit testing.</summary>
        internal static double FleetArmourSoakFraction(List<CombatShip> ships, List<WeaponProfile> incoming)
        {
            if (ships == null || ships.Count == 0 || incoming == null || incoming.Count == 0) return 0;
            double totTough = 0, sK = 0, sE = 0, sX = 0, sO = 0;
            bool anyHardening = false;
            foreach (var cs in ships)
            {
                var cv = CombatValue(cs.Ship);
                double t = cv.Toughness;
                if (t <= 0) continue;
                totTough += t;
                sK += cv.ArmourSoakVsKinetic * t; sE += cv.ArmourSoakVsEnergy * t;
                sX += cv.ArmourSoakVsExplosive * t; sO += cv.ArmourSoakVsExotic * t;
                if (cv.ArmourSoakVsKinetic > 0 || cv.ArmourSoakVsEnergy > 0 || cv.ArmourSoakVsExplosive > 0 || cv.ArmourSoakVsExotic > 0)
                    anyHardening = true;
            }
            if (!anyHardening || totTough <= 0) return 0;
            double fK = sK / totTough, fE = sE / totTough, fX = sX / totTough, fO = sO / totTough;
            double total = 0, soak = 0;
            foreach (var w in incoming)
            {
                double d = w.DamagePerSecond;
                total += d;
                soak += d * (w.Nature switch
                {
                    WeaponNature.Kinetic => fK,
                    WeaponNature.Energy => fE,
                    WeaponNature.Explosive => fX,
                    WeaponNature.Exotic => fO,
                    _ => 0,
                });
            }
            return total > 0 ? soak / total : 0;
        }

        /// <summary>Pure shield-soak math (internal for direct unit testing, like <see cref="HitFraction"/> /
        /// <see cref="WithinWeaponRange(double,double,double)"/>). Given the pool's current charge, capacity, regen, the
        /// salvo's total hull-damage, the salvo's soakable FRACTION (<see cref="SoakFractionOf"/>) and dt: absorb the
        /// soakable part up to the charge, then regenerate toward capacity. Returns how much was ABSORBED and the pool
        /// AFTER. Assumes <paramref name="pool"/> ≥ 0 (the caller lazy-seeds the -1 sentinel). A 0-capacity (unshielded)
        /// pool absorbs nothing — byte-identical.</summary>
        internal static (double absorbed, double newPool) ResolveShield(
            double pool, double capacity, double regen, double salvoDamage, double soakFraction, double dt)
            => CombatKernel.ResolveShield(pool, capacity, regen, salvoDamage, soakFraction, dt);

        /// <summary>Drain a fleet's shield pool against this salvo and return the joules that reach the hull
        /// (<see cref="FleetCombatStateDB.DamageTakenPool"/>). Handles the lazy seed (-1 → full capacity, 0 for an
        /// unshielded fleet), the nature matchup (via <see cref="SoakFractionOf"/> + <see cref="ResolveShield"/>), and
        /// the "shields at X% … DOWN!" narration. ADDITIVE: an unshielded fleet (0 capacity) returns the salvo
        /// unchanged, so combat is byte-identical. v1: the fleet regenerates only on salvos it is under fire (the pool
        /// only matters when it's being shot) — a flagged simplification.</summary>
        private static double ApplyShield(Entity fleet, List<CombatShip> ships, FleetCombatStateDB state, List<WeaponProfile> incoming, double dt, double dmgThisSalvo)
        {
            var (capacity, regen) = FleetShield(ships);
            if (state.ShieldPool_J < 0) state.ShieldPool_J = capacity;   // lazy seed: full at first contact (0 if unshielded)
            if (capacity <= 0) return dmgThisSalvo;                       // no generator → nothing to soak (byte-identical)
            if (state.ShieldPool_J > capacity) state.ShieldPool_J = capacity;  // ships lost this fight shrink the pool

            double soakFraction = SoakFractionOf(incoming);
            double before = state.ShieldPool_J;
            var (absorbed, newPool) = ResolveShield(before, capacity, regen, dmgThisSalvo, soakFraction, dt);
            state.ShieldPool_J = newPool;

            // Narration (client-on): the Battle Report's "shields holding … DOWN!" beat — only when a shield actually
            // engaged the fire (soakFraction > 0), and called out ONCE on the salvo it collapses.
            if (NarrateToLog && soakFraction > 0)
            {
                double pct = capacity > 0 ? (newPool / capacity) * 100.0 : 0;
                if (newPool <= 0 && before > 0)
                    CombatLog($"{FleetLabel(fleet)} SHIELDS DOWN — {FmtEnergy(absorbed)} absorbed, fire now reaches the hull");
                else if (newPool > 0)
                    CombatLog($"{FleetLabel(fleet)} shields at {pct:0}% ({FmtEnergy(newPool)}/{FmtEnergy(capacity)}) — absorbed {FmtEnergy(absorbed)}");
            }

            return dmgThisSalvo - absorbed;
        }

        /// <summary>Append one fleet's class-aggregated fire to a combined incoming mix, scaling its damage by
        /// <paramref name="scale"/> (an attacker divides its fire among the enemy fleets it faces). Velocity /
        /// tracking / saturation are unchanged — only the amount of that flavor changes. Keeps the incoming list
        /// small (≤ a few classes per attacking fleet), so the per-target landed-fraction stays cheap.</summary>
        private static void AddScaledFire(List<WeaponProfile> into, List<WeaponProfile> fire, double scale)
        {
            if (fire == null || scale <= 0) return;
            foreach (var w in fire)
                into.Add(new WeaponProfile(w.DamagePerSecond * scale, w.Velocity, w.Tracking, w.Saturation, 0, w.Nature, w.Delivery));
        }

        /// <summary>The damage-weighted fraction of an incoming fire mix that LANDS on a ship with the given
        /// evasion. Beams (≈light-speed) land fully; ballistic slugs are dodged by the evasive; flak floors it.</summary>
        private static double LandedFraction(List<WeaponProfile> fire, double evasion, double separation_m = 0)
            => CombatKernel.LandedFraction(fire, evasion, separation_m);

        /// <summary>Fraction of one weapon's shots that land on a target with the given evasion, at the given
        /// engagement separation. Fast/guided weapons defeat evasion (a beam ignores it); high saturation floors the
        /// result (flak fills the sky); and RANGE degrades accuracy for ballistic weapons (the longer the shot
        /// flies, the more the target dodges) — guided weapons resist that via Tracking. <paramref name="separation_m"/>
        /// 0 = point blank / closing off, so the range term is inert and the result equals the pre-closing curve.
        /// Internal so the dodge curve can be unit-tested directly.</summary>
        internal static double HitFraction(WeaponProfile w, double evasion, double separation_m = 0)
            => CombatKernel.HitFraction(w, evasion, separation_m);

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
            // The nerve that decides "fight on or break off" is the fleet's Collectivism — the faction's doctrine,
            // BLENDED with the flagship officer's own character by that officer's tenure (a green officer follows
            // doctrine; a veteran runs on their own judgement). Neutral/green/absent officer → the faction value
            // exactly, so this is byte-identical until a seasoned officer carries a divergent personality.
            double collectivism = BlendedRetreatCollectivism(fleet, PersonalityOf(fleet));
            return lost >= state.InitialShipCount * RetreatThresholdForCollectivism(collectivism);
        }

        /// <summary>
        /// M2-1b: the casualty fraction a fleet endures before breaking off, tilted by its faction's Collectivism.
        /// A neutral (or absent) personality returns exactly <see cref="RetreatCasualtyThreshold"/> (byte-identical);
        /// high Collectivism raises it (fights on through heavier losses), low lowers it (flees to save the unit).
        /// Clamped so a fleet always both can and eventually must break off.
        /// </summary>
        public static double RetreatThresholdFor(Pulsar4X.Factions.PersonalityDB personality)
        {
            if (personality == null) return RetreatCasualtyThreshold;
            return RetreatThresholdForCollectivism(personality.TraitOf(Pulsar4X.Factions.PersonalityTrait.Collectivism));
        }

        /// <summary>The break-off casualty fraction for a given Collectivism value (0..1) — the shared core of
        /// <see cref="RetreatThresholdFor"/> and the officer-blended <see cref="ShouldRetreat"/> path. Neutral (0.5)
        /// → exactly <see cref="RetreatCasualtyThreshold"/>; high raises it, low lowers it; clamped so a fleet always
        /// both can and eventually must break off.</summary>
        public static double RetreatThresholdForCollectivism(double collectivism)
        {
            double threshold = RetreatCasualtyThreshold
                + (collectivism - Pulsar4X.Factions.PersonalityDB.Neutral) * 2.0 * CollectivismRetreatSwing;
            if (threshold < 0.05) return 0.05;
            if (threshold > 0.95) return 0.95;
            return threshold;
        }

        /// <summary>
        /// Phase-2.7-attach: the Collectivism the retreat decision runs on — the fleet's FLAGSHIP OFFICER's own nerve
        /// blended toward the faction's doctrine by the officer's tenure (<see cref="OfficerCharacter.Blend"/> over
        /// <see cref="OfficerCharacter.TenureWeight"/>). A green officer (0 tenure), an all-neutral officer, or no
        /// flagship officer at all → the faction's own value EXACTLY, so this is byte-identical until a seasoned
        /// officer carries an authored, divergent character. Defensive; never throws.
        /// </summary>
        internal static double BlendedRetreatCollectivism(Entity fleet, Pulsar4X.Factions.PersonalityDB factionPersonality)
        {
            double factionColl = factionPersonality == null
                ? Pulsar4X.Factions.PersonalityDB.Neutral
                : factionPersonality.TraitOf(Pulsar4X.Factions.PersonalityTrait.Collectivism);

            var officer = FlagshipCommanderOf(fleet);
            if (officer == null || officer.Personality == null) return factionColl;

            double officerColl = officer.Personality.TraitOf(Pulsar4X.Factions.PersonalityTrait.Collectivism);
            double tenure = Pulsar4X.People.OfficerCharacter.TenureWeight(officer.Experience, officer.ExperienceCap);
            return Pulsar4X.People.OfficerCharacter.Blend(officerColl, factionColl, tenure);
        }

        /// <summary>The <see cref="Pulsar4X.People.CommanderDB"/> of a fleet's flagship officer, or null if any link
        /// is missing (no flagship set, no commander aboard). Mirrors <see cref="FleetCommanderMult"/>'s
        /// <c>FlagShipID → ShipInfoDB.CommanderID → commander</c> chain. Defensive; never throws.</summary>
        internal static Pulsar4X.People.CommanderDB FlagshipCommanderOf(Entity fleet)
        {
            if (fleet == null || fleet.Manager == null || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB) || fleetDB.FlagShipID < 0)
                return null;
            if (!fleet.Manager.TryGetEntityById(fleetDB.FlagShipID, out var flagship) || flagship == null)
                return null;
            if (!flagship.TryGetDataBlob<ShipInfoDB>(out var shipInfo) || shipInfo.CommanderID < 0)
                return null;
            if (!fleet.Manager.TryGetEntityById(shipInfo.CommanderID, out var commander) || commander == null)
                return null;
            return commander.TryGetDataBlob<Pulsar4X.People.CommanderDB>(out var cmdr) ? cmdr : null;
        }

        /// <summary>The <see cref="Pulsar4X.Factions.PersonalityDB"/> of the fleet's owning faction, or null if the
        /// faction carries none (every faction today → null → byte-identical). Defensive like <see cref="AtPeace"/>:
        /// any missing manager/game/faction-entity/blob returns null.</summary>
        private static Pulsar4X.Factions.PersonalityDB PersonalityOf(Entity fleet)
        {
            var game = fleet.Manager?.Game;
            if (game == null) return null;
            if (!game.Factions.TryGetValue(fleet.FactionOwnerID, out var factionEntity) || factionEntity == null || !factionEntity.IsValid)
                return null;
            return factionEntity.TryGetDataBlob<Pulsar4X.Factions.PersonalityDB>(out var p) ? p : null;
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
        /// <summary>Hard cap on fleet-tree recursion depth. Real fleets nest a component or two deep; a walk that
        /// ever reaches this is a MALFORMED tree (a sub-fleet that is its own ancestor — a cycle). Without this cap
        /// such a cycle recurses forever and WEDGES the combat hotloop (game-time freezes, no throw) — the exact
        /// hang-class the FleetWindow parent-walk already had to guard (Client CLAUDE.md). Cheap (an int counter, no
        /// allocation, so CombatPerformanceTests is unaffected); it bails + logs ONCE instead of hanging. 64 is far
        /// above any legitimate fleet nesting.</summary>
        private const int MaxFleetTreeDepth = 64;
        private static bool _fleetCycleLogged;

        public static List<Entity> GetFleetShips(Entity fleet)
        {
            var result = new List<Entity>();
            CollectShips(fleet, result, 0, new HashSet<int>());
            return result;
        }

        private static void CollectShips(Entity fleet, List<Entity> into, int depth, HashSet<int> seen)
        {
            if (fleet == null || !fleet.IsValid || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            // Visit each fleet node AT MOST ONCE. A malformed tree (a sub-fleet that is its own ancestor, or two
            // parents sharing a sub-fleet) would otherwise re-walk the same subtree combinatorially — the depth cap
            // bounds DEPTH but not the number of PATHS, so a few diamonds explode into millions of calls and WEDGE
            // the combat hotloop (the frozen-clock live bug the SIM-STALL watchdog caught). Deduping fleet ids makes
            // the walk O(nodes), immune to any cycle/diamond. (Depth cap kept as a cheap backstop.)
            if (!seen.Add(fleet.Id)) return;
            if (depth >= MaxFleetTreeDepth)
            {
                if (!_fleetCycleLogged) { _fleetCycleLogged = true;
                    CombatLog($"WARNING: fleet tree exceeded depth {MaxFleetTreeDepth} at {FleetLabel(fleet)} — a CYCLIC/self-nested fleet was skipped (would otherwise hang combat). Fix the fleet hierarchy."); }
                return;
            }
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid) continue;
                if (child.Id == fleet.Id) continue;            // a fleet that lists itself as a child — skip the cycle
                if (child.HasDataBlob<ShipInfoDB>())
                    into.Add(child);
                else if (child.HasDataBlob<FleetDB>())
                    CollectShips(child, into, depth + 1, seen); // sub-fleet (fleet component)
            }
        }

        /// <summary>All live ships in a fleet, each tagged with the doctrine multipliers of the component
        /// (sub-fleet) it sits in — so per-component doctrine is applied where each ship actually fights. A ship
        /// directly in the fleet uses the fleet's own posture; a ship in a sub-fleet uses the sub-fleet's
        /// (component overrides fleet — there is no multiplicative inheritance in v1).</summary>
        public static List<CombatShip> GetCombatShips(Entity fleet)
        {
            var result = new List<CombatShip>();
            // The fleet's FLAGSHIP COMMANDER's competence scales the WHOLE fleet's firepower/toughness — the
            // rung-4 "a person's skill modifies an outcome" wire. 1.0 (no effect) when there's no flagship, no
            // commander, or no combat bonus, so this is BYTE-IDENTICAL to pre-commander combat until a commander
            // actually carries a Firepower/Toughness bonus (every existing combat fixture is the tripwire).
            double cmdrFire = FleetCommanderMult(fleet, BonusCategory.Firepower);
            double cmdrTough = FleetCommanderMult(fleet, BonusCategory.Toughness);
            CollectCombatShips(fleet, result, cmdrFire, cmdrTough, 0, new HashSet<int>());
            return result;
        }

        private static void CollectCombatShips(Entity fleet, List<CombatShip> into, double cmdrFire, double cmdrTough, int depth, HashSet<int> seen)
        {
            if (fleet == null || !fleet.IsValid || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            if (!seen.Add(fleet.Id)) return;   // visit each fleet node once — cycle/diamond-proof (see CollectShips)
            if (depth >= MaxFleetTreeDepth)   // cyclic/self-nested fleet — bail before it hangs combat (see CollectShips)
            {
                if (!_fleetCycleLogged) { _fleetCycleLogged = true;
                    CombatLog($"WARNING: fleet tree exceeded depth {MaxFleetTreeDepth} at {FleetLabel(fleet)} — a CYCLIC/self-nested fleet was skipped (would otherwise hang combat). Fix the fleet hierarchy."); }
                return;
            }
            // This node's posture applies to ships DIRECTLY in it; a sub-fleet (component) applies its OWN. The
            // flagship-commander multiplier rides on top of the doctrine posture for every ship in the fleet.
            double fpMult = FleetDoctrine.FirepowerMult(fleet) * cmdrFire;
            double toughMult = FleetDoctrine.ToughnessMult(fleet) * cmdrTough;
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid) continue;
                if (child.Id == fleet.Id) continue;            // a fleet that lists itself as a child — skip the cycle
                if (child.HasDataBlob<ShipInfoDB>())
                    into.Add(new CombatShip(child, fpMult, toughMult));
                else if (child.HasDataBlob<FleetDB>())
                    CollectCombatShips(child, into, cmdrFire, cmdrTough, depth + 1, seen); // sub-component → own doctrine, same commander
            }
        }

        /// <summary>
        /// The multiplier a fleet's FLAGSHIP COMMANDER contributes to the given combat category (rung-4 wire):
        /// <c>FleetDB.FlagShipID</c> → the flagship's <c>ShipInfoDB.CommanderID</c> → that commander's
        /// <c>BonusesDB</c> → <see cref="Pulsar4X.People.CommanderBonuses.CombatMultiplier"/>. Returns 1.0 (no
        /// effect) if any link is missing — no flagship set, no commander aboard, or no matching bonus — so combat
        /// is unchanged until a commander actually carries a competence bonus. Defensive; never throws.
        /// </summary>
        internal static double FleetCommanderMult(Entity fleet, BonusCategory category)
        {
            if (fleet == null || fleet.Manager == null || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB) || fleetDB.FlagShipID < 0)
                return 1.0;
            if (!fleet.Manager.TryGetEntityById(fleetDB.FlagShipID, out var flagship) || flagship == null)
                return 1.0;
            if (!flagship.TryGetDataBlob<ShipInfoDB>(out var shipInfo) || shipInfo.CommanderID < 0)
                return 1.0;
            if (!fleet.Manager.TryGetEntityById(shipInfo.CommanderID, out var commander) || commander == null)
                return 1.0;
            if (!commander.TryGetDataBlob<BonusesDB>(out var bonuses))
                return 1.0;
            return CommanderBonuses.CombatMultiplier(bonuses, category);
        }

        internal static bool AreHostile(Entity a, Entity b)
        {
            int fa = a.FactionOwnerID, fb = b.FactionOwnerID;
            // Same faction, or either side neutral → never hostile (the v1 rule, unchanged).
            if (fa == fb || fa == Game.NeutralFactionId || fb == Game.NeutralFactionId)
                return false;

            // A DECLARED WAR overrides every peace-suppression below (Phase-3.4a coalitions). If either side holds
            // an AtWar latch toward the other, they are hostile — full stop — no matter what stance or signed pact
            // sits underneath it. This is the developer's rule that "even alliance members are still allowed to
            // shoot each other": a pact is a promise, a declared war is a fact, and the fact wins. It's what lets a
            // coalition have teeth — once you and an ally both declare war on a shared threat, the fight is real,
            // and it also means a broken/betrayed pact instantly re-arms the two former allies. Checked BEFORE the
            // AtPeace suppression so war can never be silently disarmed by a lingering treaty flag.
            if (IsAtWar(a, fa, fb) || IsAtWar(b, fb, fa))
                return true;

            // Diplomacy can only SUPPRESS the default hostility, never create it: two different non-neutral
            // factions are hostile (the v1 rule) UNLESS *both* sides hold a Friendly/Allied stance toward the
            // other (a mutual peace). A one-sided friendly declaration does NOT disarm you — if either side is
            // still hostile, they fight. An unmet stranger has no stored relationship, so this falls straight
            // through to the old "different faction = hostile" result — every existing combat fixture unchanged.
            if (AtPeace(a, fa, fb) && AtPeace(b, fb, fa))
                return false;

            return true;
        }

        /// <summary>
        /// True if <paramref name="ownFactionId"/>'s faction holds a declared-war (<see cref="Pulsar4X.Factions.RelationshipState.AtWar"/>)
        /// latch toward <paramref name="otherFactionId"/> in its <see cref="Pulsar4X.Factions.DiplomacyDB"/>. Mirrors
        /// <see cref="AtPeace"/>'s ledger resolution exactly, reading the AtWar flag instead of the peace ones. Only a
        /// STORED relationship counts — an unmet faction (no record) returns false, so an ordinary different-faction
        /// pair falls through to the existing hostility rule (byte-identical). Defensive: any missing
        /// manager/game/faction-entity/blob returns false. The entity argument is only the handle to reach the shared Game.
        /// </summary>
        private static bool IsAtWar(Entity fromEntity, int ownFactionId, int otherFactionId)
        {
            var game = fromEntity.Manager?.Game;
            if (game == null) return false;
            if (!game.Factions.TryGetValue(ownFactionId, out var factionEntity) || factionEntity == null || !factionEntity.IsValid)
                return false;
            if (!factionEntity.TryGetDataBlob<Pulsar4X.Factions.DiplomacyDB>(out var dip))
                return false;
            if (!dip.HasMet(otherFactionId))   // no relationship on record → not a declared war
                return false;
            return dip.GetRelationship(otherFactionId).AtWar;
        }

        /// <summary>
        /// True if <paramref name="ownFactionId"/>'s faction holds a Friendly or Allied stance toward
        /// <paramref name="otherFactionId"/> in its <see cref="Pulsar4X.Factions.DiplomacyDB"/>. Only a STORED
        /// relationship counts — an unmet faction (no record) returns false, so it defaults to the v1 hostile
        /// rule. Defensive: any missing manager/game/faction-entity/blob returns false (stay hostile). The entity
        /// argument is only the handle used to reach the shared <see cref="Game"/>.
        /// </summary>
        private static bool AtPeace(Entity fromEntity, int ownFactionId, int otherFactionId)
        {
            var game = fromEntity.Manager?.Game;
            if (game == null) return false;
            if (!game.Factions.TryGetValue(ownFactionId, out var factionEntity) || factionEntity == null || !factionEntity.IsValid)
                return false;
            if (!factionEntity.TryGetDataBlob<Pulsar4X.Factions.DiplomacyDB>(out var dip))
                return false;
            if (!dip.HasMet(otherFactionId))   // no relationship on record → v1 default (hostile)
                return false;
            var rel = dip.GetRelationship(otherFactionId);
            // A signed non-aggression OR defensive pact stays your hand regardless of the raw score — a treaty is
            // the explicit promise not to shoot. (A defensive pact's "drag me into your wars" entanglement is a
            // separate, later slice; here it just means the two signatories don't fight each other.)
            if (rel.NonAggressionPact || rel.DefensivePact)
                return true;
            var stance = rel.CurrentStance();
            return stance == Pulsar4X.Factions.DiplomaticStance.Friendly
                || stance == Pulsar4X.Factions.DiplomaticStance.Allied;
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
