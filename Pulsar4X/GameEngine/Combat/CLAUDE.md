# Combat — Subsystem Reference (Auto-Resolve Engine)

The **one** combat engine for this fork: a math loop that resolves fleet battles off each ship's "spec sheet," with **doctrine** as the player's only lever. Lives in `GameEngine/Combat/`. This is the v1 spine described in `docs/COMBAT-DESIGN.md` -> "What we're building (v1)".

It deliberately does **not** use the per-pixel damage sim (`Damage/DamageComplex` / `DamageVeryComplex`). That path deposits ~0 damage today (see `Damage/CLAUDE.md`) and is parked as a v2 visual skin. The auto-resolver decides casualties by **strength math**, not by simulating hits.

> **Status: under construction.** Built piece-by-piece, each under a test, in the order in `docs/COMBAT-DESIGN.md` -> "Build order". This file grows as each piece lands.

---

## File Map

| File | Purpose | Status |
|------|---------|--------|
| `ShipCombatValueDB.cs` | DataBlob: a ship's **Firepower** (joules/sec from beams + **railguns** + **flak** + a missile-launcher stub), **Toughness** (live components + armour), **RoleWeight**, **Evasion** (size + agility), and a **`Weapons`** list of per-weapon flavor profiles. Computed once at build. `Calculate(Entity)` + `CalculateEvasion(Entity)`. | ✅ built (spine step 2; +Evasion/Weapons in the depth pass; +railgun P3, +flak P4) |
| `WeaponProfile.cs` | The per-weapon flavor: `WeaponClass` enum (Beam/Railgun/Missile/Flak) + `WeaponProfile` (damage/velocity/tracking/saturation). The breakdown the dodge model + weapon triangle read. | ✅ built (depth pass P2) |
| `AutoResolve.cs` | The salvo-exchange resolver: `AutoResolve.Resolve(sideA, sideB, config)` runs the math loop (strength → damage pools → whole-ship casualties, combatants first) until one side is gone, both are, or a frozen fight hits the round cap. Returns an `AutoResolveResult` (outcome + casualty lists); **pure** — it reports casualties, it does not destroy them. Plus `AutoResolveConfig`, `BattleOutcome`. | ✅ built (spine step 3) |
| `FleetCombatStateDB.cs` | DataBlob marking a fleet as **engaged** (a *representative* opponent id for readout, accumulated damage pool, steps fought, starting ship count for the retreat threshold). On **every** fleet in a battle (state is per-fleet); removed from a fleet when it leaves. Its presence is the "in combat" flag the engagement lock (step 11) keys on. | ✅ built (spine steps 4, 7; multi-party) |
| `CombatEngagement.cs` | The trigger's engine logic: `Tick(manager, dt)` engages/JOINS hostile fleets in range, then steps the **multi-party** engagement (every in-combat fleet in the system, sides = factions) incrementally over game-time. `StepEngagementGroup(members, dt)` is the resolver; `StepEngagement(a,b,dt)` is its n=2 special case; `EnsureInCombat` is the join primitive. `GetCombatShips` tags ships with their **component's** doctrine mults (step 6); `GetFleetShips` is the flat list (counts/detection). Hostility/range/sides are v1 stubs. Testable directly. | ✅ built (spine steps 4, 6; multi-party) |
| `BattleTriggerProcessor.cs` | `IHotloopProcessor` (every 5 s) that calls `CombatEngagement.Tick` per star system. Keyed to `StarInfoDB` (FleetDB is already taken). | ✅ built (spine step 4) |
| `FleetDoctrineDB.cs` | DataBlob: a fleet's **active** combat posture (doctrine id, firepower/toughness/speed multipliers, retreat flag, switch-cooldown clock). Read by the engagement as a strength/toughness modifier. | ✅ built (spine step 5) |
| `FleetDoctrine.cs` | Helpers: `FirepowerMult`/`ToughnessMult`/`IsRetreat(fleet)` reads; `TrySetDoctrine(fleet, blueprint, now)` sets a posture from the catalog, honouring the cooldown. | ✅ built (spine step 5) |
| `CombatDoctrineBlueprint` (`Engine/Blueprints/`) | The moddable **catalog** of postures (JSON → `ModDataStore.CombatDoctrines`): family, display name, the multipliers, cooldown, retreat flag. | ✅ built (spine step 5) |
| `FleetRetreatDB.cs` | DataBlob recording that a fleet **broke off** (flag + a withdraw vector away from the enemy + who it fled from). Attached when a fleet retreats; persists after the engagement ends (so the outcome stays visible). v1 records the vector only — no move order. | ✅ built (spine step 7) |
| `CombatSandbox.cs` | Dev/test utility: `SpawnHostileFleet(game, system, playerFaction, design, count, body, name)` stands up a **registered hostile faction + fleet + ships** (built from the player's designs, owner-flipped) at a body so the trigger auto-engages them. The DevTools "Spawn Hostile Fleet" button + `CombatSandboxTests` use it. Lives in the ENGINE so the survival-through-a-clock-advance question is CI-verified, not client-only. | ✅ built (combat-test enabler) |

*Fleet components (step 6) reuse `FleetDB` sub-fleets + `FleetDoctrineDB` — no new file; see "Fleet components" below. (Retreat, step 7, adds rows as it lands.)*

---

## ShipCombatValueDB — the spec sheet

**What it is.** Two numbers that rate a ship for the auto-resolver, read from the ship's REAL parts the moment it's built and cached on the entity:

- **`Firepower`** — hurt-per-second. Each beam weapon contributes `Energy ÷ ChargePeriod` (joules/sec), scaled by that component's `HealthPercent`. Each missile launcher adds a flat `MissileLauncherFirepowerStub` (v1 stub).
- **`Toughness`** — how much it can take, **in joules absorbed**. Each live component contributes `HealthPercent × ComponentHitPoints_J` (1e5 J kills a component — straight from the damage tuning: 1000 dmg-points × 100 J), plus `armour.thickness × ArmorHitPointsPerThickness_J`. Same currency as `Firepower × time`, so the salvo loop's time-to-kill comes out in seconds.
- **`RoleWeight`** — `1.0` for anything that can shoot, `UtilityRoleWeight` (0.25) for a utility hull. The auto-resolver uses it so utility/transport ships are low-priority targets (absorb casualties last) and contribute less strength. v1 stub.
- **`Evasion`** — how hard the ship is to **hit** (0 = a sitting brick, capped at `EvasionCap` 0.95 = a nimble fighter), from `CalculateEvasion`: size (small = hard to hit, via `MassVolumeDB.Volume_m3`) × agility (acceleration = `NewtonThrustAbilityDB.ThrustInNewtons ÷ MassDry`, the *rate it changes vector*). Distinct from Toughness — toughness soaks what lands, evasion is not getting hit, and (unlike toughness) it depends on the **weapon** (you can't dodge a beam). A ship with no engine can't dodge (evasion 0). This is the input the dodge model uses; v1 stub leaves sensors + crew experience out (flagged for v2).
- **`Weapons`** (`List<WeaponProfile>`) — each weapon's flavor: `Class` (Beam/Railgun/Missile/Flak), `DamagePerSecond`, `Velocity` (m/s — beam ≈ light-speed), `Tracking` (0..1), `Saturation` (tracks/sec, = rate-of-fire). `Firepower` is the SUM of these profiles' damage (so the old number is unchanged), but the per-weapon breakdown is what the dodge model + weapon triangle read in the resolve. Beams are read real (`BeamSpeed`/`Energy`/`ChargePeriod`/`BaseHitChance`); **railguns** are read real from `RailgunWeaponAtb` (`MuzzleVelocity_mps`/`KineticEnergyPerShot_J`/`RoundsPerSecond`/`Tracking` → finite-velocity ballistic kinetic; dps = energy×rof, saturation = rof — so it's dodgeable, unlike a beam); **flak** is read real from `FlakWeaponAtb` (`MuzzleVelocity_mps`/`DamagePerPellet_J`/`RoundsPerSecond`/`PelletsPerShot`/`Tracking` → high saturation = rof×pellets, low per-pellet damage; the saturation FLOORS the dodge → the fighter/missile killer); missiles are a v1 stub. See `WeaponProfile.cs` + `docs/WEAPONS-AND-DODGE-DESIGN.md`.

**Where it's computed.** `ShipFactory.CreateShip()` calls `ship.SetDataBlob(ShipCombatValueDB.Calculate(ship))` after the components are installed. `Calculate` is defensive — a part-less ship rates 0/0 and never throws.

**Prime Directive — connections:**
- **Feeds IN:** `ComponentInstancesDB.AllComponents` (live components + `HealthPercent`); `GenericBeamWeaponAtb` (`Energy`, `ChargePeriod`) via `TryGetComponentsByAttribute`; `MissileLauncherAtb` (presence only, v1); `EntityDamageProfileDB.Armor.thickness`.
- **Feeds OUT:** the auto-resolve loop (spine step 3) sums `Firepower`/`Toughness`/`RoleWeight` over a fleet's ships to get fleet strength. Nothing else reads it yet.
- **Shares STATE:** lives on the ship entity alongside `ComponentInstancesDB` and `EntityDamageProfileDB` (reads them; does not write them).
- **Triggers:** nothing — it's a passive cached value.

**Test:** `Pulsar4X.Tests/ShipCombatValueTests.cs` — builds every starting design, asserts each gets a `ShipCombatValueDB` with toughness > 0, logs firepower (`[combat-value]`), and asserts firepower > 0 for any design carrying a beam weapon.

---

## AutoResolve — the salvo loop

**What it is.** `AutoResolve.Resolve(IList<Entity> sideA, IList<Entity> sideB, AutoResolveConfig)` fights two flat lists of ships and returns an `AutoResolveResult`. Per round:

1. Each side's strength = Σ `Firepower` of its surviving ships (later × doctrine/commander/range — stubbed at ×1 for now).
2. Each side adds `strength × RoundSeconds` joules to the **other** side's damage pool.
3. The pool removes WHOLE ships, **combatants first** (highest `RoleWeight`), then utility hulls. Leftover damage stays in the pool for next round, so a weaker fleet still grinds kills over time.
4. Repeat until one side is empty (victory), both empty (mutual destruction), neither can deal damage, or `MaxRounds` (stalemate).

**Pure by design.** It does the math and *reports* casualties (`DestroyedA`/`DestroyedB` entity lists) — it does **not** destroy entities, advance the clock, RNG, or touch the per-pixel damage sim. The battle trigger (step 4) flattens fleets into ship lists, calls `Resolve`, then destroys the reported casualties.

**Why joules.** `Firepower` is J/s and `Toughness` is J (see `ShipCombatValueDB`), so `Firepower × RoundSeconds` is joules and subtracts cleanly from toughness — time-to-kill is in seconds.

**Connections (Prime Directive):**
- **Feeds IN:** `ShipCombatValueDB` per ship (falls back to `Calculate` if a ship somehow lacks one).
- **Feeds OUT:** `AutoResolveResult` → the battle trigger destroys casualties, applies retreat (step 7), writes the event log.
- **Triggers:** nothing itself — pure function. The *caller* destroys ships.

**Test:** `Pulsar4X.Tests/AutoResolveTests.cs` — stronger fleet wins & wipes the weaker; zero-firepower = stalemate; combatants die before utility hulls. Deterministic (ships stamped with known combat values).

**Not yet (later steps):** doctrine/commander/range multipliers on strength (steps 4–6); retreat threshold ending a side early with a vector (step 7); `FleetCombatStateDB` to mark fleets "engaged" and optionally spread rounds across game-time (steps 4 / 11).

---

## The battle trigger — hostile fleets auto-engage

**What it is.** `CombatEngagement.Tick(manager, dt)` is run every ~5 s of game-time per star system by `BattleTriggerProcessor`. It:

1. **Engages / joins.** Any two hostile fleets in range are both put "in combat" — `EnsureInCombat` attaches `FleetCombatStateDB` to each (this is also what trips the engagement lock, step 11). This is *also* the JOIN path: a fleet arriving in range of an enemy gains combat state here, on its faction's side — there is no separate "reinforce" call (see "Multi-party engagements" below).
2. **Steps** the engagement forward by `dt` game-seconds via `StepEngagementGroup`: every in-combat fleet in the system fights at once (sides = factions); each fleet adds `strength × dt` joules to the pools of the fleets hostile to it and loses whole ships (combatants first) — the same math as `AutoResolve`, but spread across game-time so a battle plays out instead of resolving instantly.
3. **Releases** each fleet as it drops out (wiped, breaks off, or no enemy left), and ends the whole engagement when fewer than two hostile sides remain (or it stalls) — removing `FleetCombatStateDB` (clearing the lock).

Battles spanning game-time is what makes "watch a battle / change doctrine mid-fight / orders freeze while engaged" real — instant resolution would give none of that.

**v1 stubs (flagged):**
- **Hostility** = different non-neutral faction. There is **no diplomacy/relations system** in the engine yet, and the engine's own `EntityFilter.Hostile` additionally requires a *sensor contact* — which the v1 plan stubs as "everyone sees everyone." So the trigger ignores sensors and treats any two different-faction fleets as enemies. Real IFF/relations is a v2 layer.
- **Range** = a flat `EngagementRange_m` (1 million km). Real value = weapon range (combat steps 1–2, v2).
- **Casualties** use `Entity.Destroy()` (lightweight: flips `IsValid` false at once, no order re-entrancy). Commander death, debris, and fleet-roster cleanup are v2.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB.Children` (a fleet's ships, recursing into sub-fleets); `ShipCombatValueDB` per ship; `PositionDB` (a fleet's position = its first ship's position — a fleet entity has none of its own).
- **Feeds OUT:** destroys casualty ships; sets/clears `FleetCombatStateDB` (read by the engagement lock, step 11).
- **Triggers:** ship destruction — this is the one combat piece with *side effects on the live game*.

**Behaviour change to be aware of:** any two hostile fleets that get within range now **fight automatically** — including in an existing save the moment it loads. That's the feature, but it is new behaviour. In a single-faction game nothing happens (no hostile pairs).

**Test:** `Pulsar4X.Tests/BattleTriggerTests.cs` — hostile fleets in range auto-engage and the weaker is wiped (state cleared); same-faction fleets never engage. Integration tests (advance the clock, let the real processor run) so they also prove the live-loop hook doesn't throw.

---

## Multi-party engagements — any number of fleets, either side, any time

**What it is.** A battle is **not** locked to two fleets. Any number of fleets fight at once, and a fleet can **join a fight already underway** just by coming into range — the developer's "all combat can be multi party at anytime… I can send in another fleet to assist." `StepEngagementGroup(List<Entity> members, dt)` is the resolver; the old two-fleet `StepEngagement(a, b, dt)` is just its **n = 2 special case** (it calls the group resolver with `[a, b]`), so a 1-v-1 and a 10-fleet melee run the **same** code.

**The model (one step):**
1. **Snapshot** every in-combat fleet's ships, outgoing fire mix (`BuildFireMix`), and **enemy set** (the in-combat fleets hostile to it, with ships) — *before* any casualties, so the exchange is simultaneous.
2. **Damage.** Each fleet takes the **combined** fire of all fleets hostile to it. An attacker facing several enemy fleets **divides** its fire across them (`1 / enemyCount` to each) — outnumbering a side doesn't multiply your guns (firepower is conserved). Within a target fleet, the bucketed dodge resolve still concentrates on the most-hittable ships.
3. **Resolution.** A fleet drops out (its `FleetCombatStateDB` is removed) when it is **wiped**, **breaks off** (retreat), or has **no enemy left**. When fewer than two mutually-hostile fleets remain (battle decided), or the fight is frozen / timed out, everyone still in is released.

**Sides = factions (v1 stub).** Two fleets are on the same side iff same faction; different non-neutral factions are hostile. There is no alliance/diplomacy model yet (v2). Because hostility is computed per-pair, a true 3-way free-for-all already resolves (each fleet simply has enemies on more than one "side") — it's only the *fire-division* and *side* heuristics that are first-pass.

**One system = one battlefield (v1 stub).** `Tick` runs per star system, and every in-combat fleet in that manager is in the **same** engagement (v1 range ≈ whole system). Distinct simultaneous battles in one system — clustering by real weapon range — is a v2 layer; `InRange` today only gates *joining*.

**Joining is idempotent.** `EnsureInCombat(fleet, repId)` only attaches state if the fleet doesn't already have it, so a reinforcement that's been in the fight for many ticks keeps its accumulated damage pool / step count — joining never resets an in-progress fight.

**Reduces exactly to the old behaviour for n = 2** (verified against every existing combat fixture — `BattleTrigger`, `Retreat`, `Performance`, `Dodge` all drive the 2-fleet path and stay green): one enemy → no fire division, one pool each, both released when the loser is wiped/retreats.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB` (membership = all in-combat fleets in the manager); per-fleet `FleetCombatStateDB`; `GetCombatShips` (per-component doctrine); `AreHostile` (sides).
- **Feeds OUT:** destroys casualty ships; sets/clears `FleetCombatStateDB` per fleet (engagement lock); records `FleetRetreatDB` on a fleet that breaks off.
- **Triggers:** ship destruction and engagement end — same side effects as the two-fleet trigger, now per fleet.

**Test:** `Pulsar4X.Tests/MultiPartyEngagementTests.cs` — **assist** (two fleets ganging up beat a lone equal enemy), **same-faction side** (a friendly reinforcement shares a side — no friendly fire, both disengage cleanly when the enemy dies; sized so an ally would die if friendly fire ever regressed in), **join** (a reinforcement pulled in mid-battle through the real `Tick` tips a 1-v-1 it then wins), **fire-split** (one fleet vs two enemies divides its fire — can't kill both in the step it could kill one, but the fire reaches both).

---

## Switchable doctrine

**What it is.** Each fleet can fly an active **combat posture** — its doctrine — set by the player (or NPC). The auto-resolver reads it as a read-time multiplier on that fleet's strength and toughness, so the *same* fleet fights differently under a different posture. Two pieces:
- `FleetDoctrineDB` (on a fleet) = the **active selection**: the chosen posture's id + its `FirepowerMult` / `ToughnessMult` / `SpeedMult` / `IsRetreat`, plus `SwitchableAfter` (the switch-cooldown clock).
- `CombatDoctrineBlueprint` (in `ModDataStore.CombatDoctrines`, loaded from `GameData/basemod/TemplateFiles/combatDoctrines.json`) = the **moddable catalog** of selectable postures. Wired through the standard mod pipeline: a `DataType.CombatDoctrine` case in `ModInstruction` + `ModLoader`, a dict in `ModDataStore`, an entry in `modInfo.json`.

**Setting a posture.** `FleetDoctrine.TrySetDoctrine(fleet, blueprint, now)` copies the blueprint's effects into the fleet's `FleetDoctrineDB` and starts the cooldown; it returns `false` (no change) if the fleet is still within `SwitchableAfter`. Effects apply **at read time** (the `BonusesDB` pattern) — never baked into ship stats, so switching is reversible.

**How combat reads it.** `CombatEngagement.StepEngagement` works off `GetCombatShips(fleet)`, which tags every ship with the firepower/toughness multipliers of the **component it sits in** (see "Fleet components" below) — so a posture set on the whole fleet applies to the ships directly in it, and a posture set on a sub-fleet applies to that sub-fleet's ships. A fleet/component with no `FleetDoctrineDB` reads ×1.0 (neutral), so doctrine is purely additive over step 4. `AutoResolve` (the pure ship-list variant) stays doctrine-free — doctrine lives where fleets do (the engagement).

**Don't confuse with `FactionInfoDB.Doctrine`** — that's the strategic Economic/Military/Tech/Expansion AI vector (a different system). Same word.

**Base catalog (4 postures):** `balanced` (Utilitarian), `all-out-attack` (Offensive: +firepower / −toughness), `defensive-line` (Defensive: −firepower / +toughness), `fighting-withdrawal` (Defensive, `IsRetreat=true` — the withdraw posture for step 7).

**Tests:** `Pulsar4X.Tests/FleetDoctrineTests.cs` — the catalog loads from JSON; `TrySetDoctrine` applies the multipliers and honours the cooldown; an aggressive (×2 firepower) fleet beats the identical enemy that has none. `BaseModIntegrityTests` (existing) also validates the JSON loads with zero skipped entries.

**Not yet:** a player-facing order to change doctrine mid-fight (the engagement lock, step 11, will allow that one order during combat); `SpeedMult` is stored but not yet applied to movement.

---

## Fleet components — per-component doctrine

**What it is.** A fleet can be split into named **components** — Front Line, Flank, Rear Guard, Artillery — so different parts of one fleet fight with different postures in the *same* engagement (docs/COMBAT-DESIGN.md System 4, detailed design). A component is just a **sub-fleet**: `FleetDB` already nests via `TreeHierarchyDB`, so ship assignment, movement, and detach/reattach all already work — the only new part is that each sub-fleet can carry its own `FleetDoctrineDB`.

**How it works.** `CombatEngagement.GetCombatShips(fleet)` walks the fleet tree and returns a `List<CombatShip>` — each `CombatShip` is `{ Entity Ship, double FirepowerMult, double ToughnessMult }`, where the multipliers come from the doctrine of the **immediate component** that ship sits in. A ship directly in the top fleet gets the top fleet's posture; a ship in a sub-fleet gets the sub-fleet's. `StepEngagement` then sums `Firepower × FirepowerMult` for strength and scales each ship's casualty-toughness by its own `ToughnessMult`.

**v1 stacking rule: component overrides fleet, no inheritance.** A sub-fleet with no `FleetDoctrineDB` reads ×1.0 (neutral) — it does **not** inherit the parent fleet's multiplier. This keeps the math predictable (one posture per ship, no multiplicative stacking). Revisit if v2 wants fleet-wide buffs that layer onto component postures.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB.Children` (the tree — ships and sub-fleets); `FleetDoctrineDB` per fleet-node (via `FleetDoctrine.FirepowerMult`/`ToughnessMult`).
- **Feeds OUT:** `GetCombatShips` is consumed by `StepEngagement` (strength + casualties). `GetFleetShips` (the flat `List<Entity>`, no multipliers) is unchanged and still used for count checks, the battle-trigger detection, range, and tests.
- **Triggers:** nothing new — same casualty side effects as the battle trigger.

**Test:** `Pulsar4X.Tests/FleetComponentTests.cs` — a ship in an offensive sub-component reads ×2 while a ship directly in the fleet reads ×1 (doctrine is per-component, not whole-fleet); and a component's ×2 firepower flips a battle a raw 6k-vs-10k hull would have lost.

---

## Retreat — breaking off a fight

**What it is.** A fleet can **break off** an engagement instead of fighting to extinction (docs/COMBAT-DESIGN.md System 5). v1 is a **math outcome**: breaking off attaches a `FleetRetreatDB` (the flag + a withdraw vector + who it fled from) and ends the engagement. It does **not** issue a movement order — ships don't physically run yet; that's a v2 movement-system layer. `FleetRetreatDB` is the hook that layer will read.

**Two triggers (both in `CombatEngagement.ShouldRetreat`):**
- **Posture** — the fleet flies a withdraw doctrine (a `FleetDoctrineDB` with `IsRetreat=true`, e.g. the base catalog's `fighting-withdrawal`). The posture *is* a standing retreat order, so the fleet breaks off after one salvo window.
- **Threshold** — the fleet has lost at least `RetreatCasualtyThreshold` (v1 flat **0.5**) of the ship count it started the engagement with (`FleetCombatStateDB.InitialShipCount`, captured at `StartEngagement`).

A **wiped** fleet (0 ships) is destroyed, not retreated — `ShouldRetreat` returns false at count 0, so a retreat always leaves survivors.

**The withdraw vector.** `RecordRetreat` sets a unit vector pointing from the enemy fleet toward the retreating fleet (the way it would run). If fleet positions aren't available or coincide (common in a headless test where ships share a body), it records `Vector3.Zero` — best-effort; the flag and `FledFromFleetId` are always recorded.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDoctrine.IsRetreat(fleet)` (posture); `FleetCombatStateDB.InitialShipCount` + current survivor count (threshold); fleet positions (vector).
- **Feeds OUT:** `FleetRetreatDB` on the fleet (persists past the engagement) — the v2 movement layer and any "did this fleet retreat?" readout/UI consume it. Ends the engagement (clears `FleetCombatStateDB`, releasing the engagement lock, step 11).
- **Triggers:** engagement end (same path as a wipe).

**Test:** `Pulsar4X.Tests/FleetRetreatTests.cs` — a fleet on `fighting-withdrawal` breaks off intact (posture); a 4-ship fleet that loses half retreats with its survivors (threshold). Both assert the `FleetRetreatDB` is recorded and the engagement ended.

**Not yet (v2):** the actual withdraw **movement** (Breaking Off → Withdrawing → Safe state machine, rally point, pursuit); per-doctrine retreat thresholds (v1 uses one flat constant); commander-triggered early retreat (System 6).

---

## Engagement lock — engaged fleets can't be re-tasked

**What it is.** Once a fleet is in a battle, you can't re-task it — its regular orders are refused until the fight ends. The *only* thing you can still do is change its **doctrine**. This is what makes the combat model "set the fight up, then steer it with doctrine, not micromanagement" (the developer's requirement, step 11).

**How it works.** The lock lives in the order handler — `StandAloneOrderHandler.HandleOrder` — not in the Combat subsystem, but it keys on a combat blob, so it's documented here too. After an order passes `IsValidCommand`, `IsEngagementLocked` rejects it (silently, no execute) when:
- the order's `EntityCommanding` is a fleet that has a `FleetCombatStateDB` (i.e. it's engaged — the battle trigger attaches this on both fleets), **and**
- the order is not flagged `EntityCommand.IsAllowedDuringEngagement` (default false).

**Why doctrine still works.** Doctrine changes go through a **direct call** — `FleetDoctrine.TrySetDoctrine` — not an `EntityCommand`, so they never reach the order handler and are unaffected by the lock. That's the v1 mechanism for "only doctrine changes apply." The `IsAllowedDuringEngagement` hook is the path for any *future* combat-time order (e.g. an explicit retreat order) to opt back in.

**Scope (v1).** The lock is fleet-level: it blocks orders whose commanding entity is an engaged fleet (`FleetCombatStateDB` is only ever on fleets). Orders on an individual ship inside an engaged fleet are not blocked by this check — re-tasking individual ships mid-battle is a v2 tightening. The refusal is silent at the engine level; surfacing a player-facing "fleet is engaged — orders locked" message is the UI's job (it reads `FleetCombatStateDB` to show the locked state).

**Connections (Prime Directive):**
- **Feeds IN:** `FleetCombatStateDB` presence on the commanding fleet (set/cleared by the battle trigger + retreat); `EntityCommand.IsAllowedDuringEngagement`.
- **Feeds OUT:** order acceptance/refusal — every `FleetOrder` / movement order routed through `StandAloneOrderHandler` is now gated by it.
- **Triggers:** nothing — it only gates existing order execution.

**Test:** `Pulsar4X.Tests/EngagementLockTests.cs` — a fleet with a `FleetCombatStateDB` refuses an AssignShip order (ship count unchanged); a `TrySetDoctrine` on the same engaged fleet still applies; removing the combat state lets the order through.

---

## Example combat-test ships (testing enabler)

Two purpose-built armed designs ship in the base mod for setting up a fight (spine step 10), in
`GameData/basemod/ScenarioFiles/designs/shipDesigns.json` and listed in `colony-earth`'s `ShipDesigns` so the
starting faction has them (spawnable from DevTools):

| Design id | Name | Build | Role |
|-----------|------|-------|------|
| `default-ship-design-test-warship` | Aegis Test Warship | 4 lasers, plastic armour ×6, 2 reactors/4 batteries/4 engines | the **strong** side |
| `default-ship-design-test-corvette` | Picket Test Corvette | 1 laser, plastic armour ×1, 1 reactor/1 battery/1 engine | the **weak** side |

Both reuse only gunship-proven components, so they stay buildable (`BaseModIntegrityTests`) and are auto-rated by
`ShipCombatValueTests`. The existing `default-ship-design-gunship` / `-dropship` are also armed (2 lasers each) if
you want an even matchup. Spawn an Aegis fleet for one faction and a Picket fleet for the other (DevTools faction
switcher, step 9) to watch the auto-resolver decide it.

**Weapon-flavor + triangle roster (added with the depth pass P3/P4/P6) — all in `shipDesigns.json` + `earth.json`:**

| Design id | Name | Mounts | Triangle role |
|-----------|------|--------|---------------|
| `default-ship-design-test-railgun` | Lancer Railgun Cruiser | 4 railguns | finite-velocity kinetic — brutal vs slow, dodged by fast |
| `default-ship-design-test-flak` | Bulwark Flak Escort | 4 flak guns | high-saturation PD — the fighter/missile killer |
| `default-ship-design-test-fighter` | Wasp Strike Fighter | 1 railgun, 4 engines | small + agile = **evasive** (dodges railguns) |
| `default-ship-design-test-capital` | Leviathan Battleship | 4 railguns, 8 armour, 2 engines | big + sluggish = **tanky, can't dodge** |

These compose already-registered weapon components (no new `weapons.json` templates), so a new such design needs
only its entry in `shipDesigns.json` + the `ShipDesigns` list in `earth.json` — NOT the full six-point chain
below (that's only for a brand-new weapon TYPE). `WeaponTriangleTests` proves the dodge edges off the Wasp/
Leviathan real combat values; `RailgunWeaponTests` / `FlakWeaponTests` prove the Lancer/Bulwark weapon profiles.

**Warp drives — these are combat ships, but they couldn't travel (added 2026-06-27).** Every design above also
mounts an **Alcubierre warp drive**: `default-design-alcubierre-2k` on the four heavies (Aegis / Lancer / Bulwark /
Leviathan), the lighter `default-design-alcubierre-500` on the Picket corvette and Wasp fighter. It's the **same
drive the utility ships** (Courier / Freighter / Surveyor) already carry, so its materials are already in the
colony's `StartingItems` and it stays buildable (`BaseModIntegrityTests` covers it). Without a warp drive,
`WarpMoveCommand` now logs `[WARP] … CAN'T WARP — no warp drive` and the ship just sits there on a move order — the
exact "I ordered it to move and nothing happened" symptom (see `Movement/CLAUDE.md`). The drive is **travel-only**
and is deliberately **not** in the weapon/armour/engine counts in the tables above — those counts are the sublight
NTR1.8 thrusters that set in-system evasion, which the warp drive does not change. The lighter 500-class drive went
on the **Wasp on purpose**: any added mass nudges evasion down, and the Wasp's evasion is exactly what
`WeaponTriangleTests` measures — the small drive was chosen to keep that calibration intact (CI is the check).

**Test:** `Pulsar4X.Tests/CombatTestShipsTests.cs` — the two designs load onto the faction and rate strong-vs-weak
(warship out-guns + out-armours the corvette); a 3v3 auto-resolve is a decisive `SideAVictory` with all corvettes lost.

**Adding a new player-buildable weapon + armed design — it touches SIX registration points; miss any and New
Game / `CreateWithColony` crashes (gotcha-10, learned the hard way twice on railgun P3).** The colony build
chains template → component design → ship design, each looked up in the faction's data store, so ALL of:

1. **`*Atb` C# class** (`GameEngine/Weapons/Weapon<X>/`) — implements `IComponentDesignAttribute`; ctor arg order MUST match the JSON `AtbConstrArgs(...)`.
2. **`ComponentTemplate`** in `weapons.json` — `UniqueID` + the `AtbConstrArgs` property with the exact `AttributeType` namespace, ResourceCost keys that are all defined materials.
3. **`ComponentDesign`** in `componentDesigns.json` — `UniqueId` (lowercase d) + `TemplateId` pointing at #2.
4. **`earth.json` `StartingItems`** — add the **template** id (e.g. `"railgun-weapon"`). *Unlocks the template for the faction.* Miss this → `ComponentDesignFromJson`: `"<template> was not found in the faction data store"`.
5. **`earth.json` `ComponentDesigns`** — add the **component-design** id (e.g. `"default-design-railgun-weapon"`). *Builds it into `InternalComponentDesigns`.* Miss this → `ShipDesignFromJson`: `KeyNotFoundException: '<component-design-id>'`.
6. **`earth.json` `ShipDesigns`** + the ship design in `shipDesigns.json` — the design that mounts the weapon (its `Components[].Id` = the #5 design id).

The two failures are distinct and both crash **every** `CreateWithColony` test at setup (not just one). `BaseModIntegrityTests` did **not** catch either (it checks the mod store + industry buildability, not the faction StartingItems-unlock → ComponentDesigns-build → ShipDesigns-resolve chain); the harness itself is the standing sensor — once a design is in `earth.json`, every harness test builds it end-to-end, so a missing registration fails loudly and immediately.

---

## Dodge in the resolve (weapon flavor decides WHO gets hit)

**What it is.** `StepEngagement` no longer pours one flat firepower number into the enemy's pool. It builds each
side's **fire mix** (`BuildFireMix` → a list of `WeaponProfile`s, each weapon's damage scaled by its doctrine
firepower mult), and `ApplyCasualties` is now **dodge-aware**: a ship's *effective* toughness is its raw
toughness ÷ the **landed fraction** of the incoming fire, and ships fall **most-hittable first**. So the big
slow hull dies while the nimble fighter holds — the developer's acceptance test.

**The math (all in `CombatEngagement`, see `docs/WEAPONS-AND-DODGE-DESIGN.md`):**
- `HitFraction(weapon, evasion)` (internal, unit-tested): `velocityTerm = velocity/(velocity+VelocityReference)`;
  `trackingEffectiveness = max(velocityTerm, tracking)`; `dodgeChance = evasion × (1 − trackingEffectiveness)`;
  result `= clamp(1 − dodgeChance, saturationFloor, 1)`. A beam (≈light-speed) → ~1 (can't dodge light); a slug
  (finite, ballistic) → low vs the evasive; flak's high saturation floors it up.
- `LandedFraction(fireMix, evasion)` = damage-weighted average `HitFraction` over the mix.
- Effective toughness in `ApplyCasualties` = `Toughness × ToughnessMult ÷ LandedFraction`.

**Backward-compatible (the green spine stays green).** A ship with **no** weapon profiles but real firepower
(old-style combat value) fires as a `FallbackBeamVelocity` always-hit beam, and a target with **0 evasion** has
`LandedFraction = 1` — so an all-old-style fight (every existing combat test) behaves EXACTLY as before. Dodge
only changes outcomes once ships carry weapon profiles + evasion.

**Performance — bucketed both ways.** Outgoing: `BuildFireMix` aggregates fire **by weapon class** (≤4 entries),
so each `LandedFraction` iterates a handful of classes, not every enemy weapon. Incoming: `ApplyCasualties`
buckets defenders by their **combat value** — key `(doctrine toughness mult, evasion, toughness, role)`, i.e.
everything that decides how a ship dies — and computes the landed fraction + effective toughness ONCE per
bucket, killing whole ships as a count (`CasualtyBucket`). So **500 identical fighters cost the same as 5**; the
costly work is O(buckets), not O(ships). Proven by `CombatPerformanceTests` (200 real warships resolve in
milliseconds) — **the tripwire if either aggregation ever breaks.** Aggregating outgoing fire uses a
damage-weighted velocity/tracking/saturation (a fine v1 approximation; same-class weapons are similar).

**The casualty bucket is the seam for "degraded" condition tiers.** Because ships bucket by combat value, a
damaged ship with a degraded combat value lands in a *different* bucket automatically — so the
aggregate-force-condition model (Pristine / Lightly / Moderately / Severely Degraded, per-tier debuffs, "launch
only Non-Degraded" orders) needs **no new resolve code**, only the parked v2 "recalc combat value on damage"
hook. Design + connected systems: `docs/WEAPONS-AND-DODGE-DESIGN.md` → "Future depth — aggregate force
condition." Principle: *simulate at the granularity of the decision, not the entity.*

**v1 scope.** This delivers the dodge-driven triangle edges (Beam▸Fighter, Fighter▸ballistic-Capital). The
explicit `TriangleBonus` (a tunable class-vs-class modifier) and the Capital▸Beam edge (which needs weapon
**range**, a v1 stub) are refinements on top. `AutoResolve` (the pure ship-list variant) stays dodge-free, like
it stays doctrine-free.

**Combat pace — the hot-damage rebalance (2026-06-25).** The raw numbers were "hot": a hull is built from
~100 kJ components but a gun pours ~1 MJ/sec, so ships died in ~1 second of fire and whole fleet battles ended in
**2–4 salvos (10–20 game-seconds)** — faster than the default 1-hour master tick, i.e. over before you could
watch or steer them. `StepEngagementGroup` now multiplies each salvo's pooled damage by **`SalvoDamageScale`**
(0.1), so only a tenth of the raw energy counts toward kills per step — the same fight now plays out over **~10×
more salvos** (a couple of game-minutes you can watch and change doctrine inside). Because the scale is **uniform**
(every fleet's incoming fire is scaled by the same factor), it changes battle **duration, not the outcome**: who
wins, the exchange ratio, and every weapon-triangle/dodge/doctrine finding are unchanged — only the salvo count
to get there rises. It is the single tunable for the "per-shot-energy ÷ hull-toughness" balance the
`WeaponTriangleBattleTests` docstring flagged as a v2 pass. It lives ONLY on the stepped (live, watchable)
resolve; `AutoResolve` (the instant off-screen resolver) stays unscaled on purpose — a battle nobody watches
needn't be paced out. Measured before/after numbers: `CombatStressLab` + `CombatBattleSims`.

**Test:** `Pulsar4X.Tests/DodgeResolveTests.cs` — the `HitFraction` curve (beams ignore evasion, slugs are
dodged, flak floors it); and through the resolve, slug fire kills the un-evasive battleship while the fighter
(same toughness, only evasion differs) dodges and survives.

---

## Combat sandbox — spawning an enemy to fight (the live-test enabler)

**What it is.** `CombatSandbox.SpawnHostileFleet(...)` stands up the OTHER side of a battle: a fresh registered
faction + a fleet + N ships (built from the player's designs, owner-flipped to the enemy), parked at a body so
the `BattleTriggerProcessor` auto-engages them. A fresh game has only the player and an empty sky, so without this
there's nothing to fight — it's what makes "spawn an enemy and press play" possible. The DevTools "Spawn Hostile
Fleet" button is a thin wrapper over it.

**Why it's in the ENGINE, not the client.** The combat test fixtures (`BattleTriggerTests`, `MultiParty…`) drive
`CombatEngagement.Tick` DIRECTLY and never advance the game clock, with a standing note that a *bare* enemy
faction's owner-flipped ships "don't survive movement processing across a clock advance." That made the live path
(spawn enemy → press play → full per-tick processor sweep) an unproven, CI-blind unknown. Putting the spawn in the
engine lets **`CombatSandboxTests` advance the REAL clock (`game.TimePulse.TimeStep()`) and assert the spawned
enemy survives + engages** — so the live button is proven in CI, not on the developer's Windows build. The recipe
that makes the flipped ships persist like a real NPC's: `CreateBasicFaction` (registers it in `game.Factions` +
gives a root `FleetDB`), add the system to the faction's `KnownSystems`, and copy the player's `ShipDesigns`. (The
ships are still built under the player faction because `ComponentDesigns` is a read-only view and the enemy hasn't
unlocked components; combat only reads `FactionOwnerID`, so the flip is enough.)

**Fuelling (added 2026-06-25).** `ShipFactory.CreateShip` builds ships with **empty** tanks **on purpose** —
production-built ships are meant to be fuelled at a colony, so CreateShip must not hand out free fuel (that would
break the fuel economy). The natural start fleet is fuelled by the start setup; **manually-spawned ships are not**,
which is why a DevTools-spawned ship showed 0 fuel. The shared fix is **`ShipFactory.FillFuelTanks(ship,
factionInfo)`** (returns units stored): `SpawnHostileFleet` calls it **before the owner flip** (fuel resolves
through the ship's *faction* library — `UpdateMassFuelAndDeltaV` → `GetFactionCargoDefinitions`, unlocked
`CargoGoods` — and only the player has fuel unlocked, so it must run while the ship is still player-owned), and the
DevTools **"Spawn Ship"** button calls the same helper. It reads the ship's own `NewtonThrustAbilityDB.FuelType`
and fills the tank via `CargoTransferProcessor.AddCargoItems` (`CargoMath.AddCargoByUnit` caps at tank free volume).
**Key gotcha:** an engine may burn a fuel the faction hasn't *unlocked* — the Leviathan's NTR burns **`ntp`**
("Nuclear Thruster Propellant", an `"ntr"`-category fuel). All fuels share `CargoTypeID "fuel-storage"`, so the
tank accepts any of them; `FillFuelTanks` looks the fuel material up in `CargoGoods` **then falls back to
`LockedCargoGoods`** so a not-yet-researched fuel still stocks. A ship with no thruster / no fuel-tank bay / a
`FuelType` that resolves to no material is left empty (returns 0, no crash). (The auto-resolve battle doesn't burn
fuel — v1 combat is math, not maneuvering — this is for realism + a future maneuver/retreat layer.)

**Charging (added 2026-06-27) — the sibling fix that actually makes a spawned ship MOVE.** Fuel alone wasn't
enough: **warp is paid from stored electricity, not fuel**, and `CreateShip` leaves stored energy at **0** too
(`EnergyStoreAtb` inits `EnergyStored = 0`). A spawned ship had full tanks but a dead battery, so a move order did
nothing — `WarpMoveCommand` blocks while `EnergyStored < BubbleCreationCost`. The start fleet dodges this because
`DefaultStartFactory` hand-charges each ship to `EnergyStored = 2,750,000` — *that* was "what the premade ships have
that a spawned one doesn't." Fix: **`ShipFactory.ChargeReactors(ship)`** (the energy sibling of `FillFuelTanks`),
called by both spawn paths right after the fuel fill (`SpawnHostileFleet` here + DevTools "Spawn Ship"). It tops
`EnergyStored` to the ship's own `EnergyStoreMax`; a charged base-mod battery holds **2×–4× one warp bubble** at
starting tech (battery-2t = 1,000,000 KJ each; alcubierre-2k bubble = 1,000,000 KJ, alcubierre-500 = 250,000 KJ), so
a charged ship can always warp — and can FIRE (weapons draw stored energy too). Sensor:
`CombatTestShipsTests.ChargeReactors_FillsStoredEnergy_SoASpawnedShipCanWarp`; full warp chain in `Movement/CLAUDE.md`.

**Test:** `Pulsar4X.Tests/CombatSandboxTests.cs` proves three things separately: **(1) persistence** — spawn 3
hostiles, advance the real clock, assert they're still there (3/3); **(2) engageable** — drive
`CombatEngagement.Tick` over the system (the proven path) and assert the unarmed player ship is destroyed (only
possible if the spawned hostiles are real, in-range enemies the trigger fights); and **(3) fuelled** — it spawns
the **Leviathan** (the `ntp`-burning NTR design, the trickiest fuel path) and asserts every ship's `FuelType`
**resolves to a real fuel material** (catches a category-vs-material mapping bug that would otherwise silently
no-op) and that every fuel-capable ship comes out with `TotalFuel_kg > 0` (asserts only for ships with a matching
fuel bay, so a no-bay design can't falsely fail it).

**Live battle narration (added 2026-06-25).** `CombatEngagement.NarrateToLog` (a `public static bool`, default
**false**) makes the engine write plain-language `[Combat]` lines to the captured log (`game_log.txt`) on each
**state change** — `enters combat`, `salvo N: <fleet> lost K ship(s), M left`, `breaks off — retreats`,
`disengages` — so a live fight is visible in the log, not only the Fleet Combat tab. It's logged only on
transitions (never per-tick), so it reads like a play-by-play without flooding. **Default off so it never slows
the timed battle tests** (`CombatPerformanceTests`, `CombatStressLab`, the 1000-ship `B10`); the client turns it
**on** at startup (`PulsarMainWindow` ctor: `CombatEngagement.NarrateToLog = true`). Helpers `CombatLog`/`FleetLabel`
are in `CombatEngagement.cs`; the per-salvo line guards on `NarrateToLog` before building its string so the hot
casualty path allocates nothing when narration is off. **Bonus diagnostic:** because the live auto-trigger
(`BattleTriggerProcessor` → `CombatEngagement.Tick`) runs this, an `[Combat] … enters combat` line on PLAY confirms
the auto-trigger fired; its absence (while DevTools "Tick Combat" *does* produce lines) localises the open
"does PLAY auto-start combat live?" question to the trigger/scheduler, not the resolve.

**Gotchas the gauge surfaced (two, both load-bearing for the live test):**
1. **The flipped-faction enemy ships DO persist through a clock advance** with the sandbox's faction setup
   (`CreateBasicFaction` + `KnownSystems` + copied `ShipDesigns`) — the old "don't survive movement processing"
   warning was about a *bare* faction. Proven: 3/3 survive.
2. **The lightweight colony test harness does NOT auto-fire hotloop processors on `TimeStep`.** Setting the system
   Foreground (`IncrementExternalObserver(true)`) was *not* enough to make the battle trigger run on the clock
   advance in the test — so the gauge drives `CombatEngagement.Tick` DIRECTLY for the engagement proof (same as
   every other combat fixture). Whether the trigger auto-fires on a clock advance in the **full live game** is a
   separate question the test can't settle — `MasterTimePulse` only processes systems whose `ActivityState !=
   Stasis` (a colony system is Background, a watched one Foreground), but the harness's `TimeStep` path didn't
   exercise it here. **Live-test implication: confirm in the running client whether pressing play auto-starts the
   battle; if not, the fallback is a "Force Engagement" control that calls `CombatEngagement.Tick`.** The gauge
   logs whether the system clock even advanced, to tell harness-quirk from a real "trigger never fires" bug.
3. **The spawned hostiles are the FIRST foreign-faction entities a player can click — they expose latent
   client crashes that assume player-owned data (found + fixed live 2026-06-25).** The enemy faction is bare:
   `FactionDataStore.CargoTypes` is empty (everything sits in `LockedCargoTypes` until tech unlock). Clicking a
   hostile "Cargo Courier" opened its `EntityWindow`, which hard-indexed the OWNER faction's `CargoTypes[sid]` →
   `KeyNotFoundException` → whole-client crash (invisible in `game_log.txt` because the trace goes to stderr).
   Fixed client-side (defensive cargo-type lookup at three sites + a `SafeRender` render-loop gauge) — see
   `Pulsar4X.Client/CLAUDE.md` gotchas #11–#12. **Implication for this tool: any new "show me data about an
   entity" client panel must tolerate a foreign/NPC owner with empty unlocked data; the spawn button is the way
   to flush these out before real NPCs exist.**

---

## Model-coupled / tuning constants

| Constant | Value | Meaning | Where |
|----------|-------|---------|-------|
| `MissileLauncherFirepowerStub` | 100,000 | flat firepower (J/s) per missile launcher until ordnance warhead energy is wired (v2) | `ShipCombatValueDB.cs` |
| `UtilityRoleWeight` | 0.25 | combat-value role weight of a hull with no weapons | `ShipCombatValueDB.cs` |
| `ComponentHitPoints_J` | 100,000 | joules one component absorbs before destruction (= the damage tuning's "100 kJ kills a component") | `ShipCombatValueDB.cs` |
| `ArmorHitPointsPerThickness_J` | 100,000 | joules of toughness added per unit of armour thickness | `ShipCombatValueDB.cs` |
| `SizeReference_m3` | 1,000 | ship volume (m³) at which the size half-contributes to evasion (bigger = easier to hit) | `ShipCombatValueDB.cs` |
| `AgilityReference_mps2` | 5.0 | acceleration (m/s²) at which agility half-contributes to evasion (thrust ÷ mass) | `ShipCombatValueDB.cs` |
| `EvasionCap` | 0.95 | hard ceiling on Evasion — nothing is ever fully untouchable | `ShipCombatValueDB.cs` |
| `AutoResolveConfig.RoundSeconds` | 5.0 | game-seconds of fire per salvo round | `AutoResolve.cs` |
| `AutoResolveConfig.MaxRounds` | 2000 | round-cap backstop; hitting it = Stalemate | `AutoResolve.cs` |
| `CombatEngagement.EngagementRange_m` | 1e9 (1M km) | v1 flat auto-engage distance (real value = weapon range, v2) | `CombatEngagement.cs` |
| `CombatEngagement.MaxSteps` | 5000 | per-engagement step cap (stalemate backstop) | `CombatEngagement.cs` |
| `CombatEngagement.RetreatCasualtyThreshold` | 0.5 | fraction of starting ships a fleet must lose to break off (v1 flat; real value = per-doctrine, v2) | `CombatEngagement.cs` |
| `CombatEngagement.VelocityReference_mps` | 1e6 | shot velocity at which a weapon half-defeats evasion (beam ≫ this, slug ≪ this) | `CombatEngagement.cs` |
| `CombatEngagement.SaturationReference` | 50 | saturation (tracks/sec) at which a weapon half-guarantees a hit regardless of dodge (flak ≫ this) | `CombatEngagement.cs` |
| `CombatEngagement.MinLandedFraction` | 0.02 | floor on fire that lands — enough volume kills even a perfect dodger | `CombatEngagement.cs` |
| `CombatEngagement.FallbackBeamVelocity_mps` | 1e8 | an unarmed-profile (old-style) ship fires as this light-speed always-hit beam → dodge degrades to old behaviour | `CombatEngagement.cs` |
| `CombatEngagement.SalvoDamageScale` | 0.1 | **the combat-pace dial** — fraction of a salvo's raw energy that counts toward kills; <1 stretches a battle over more salvos (effectively "every hull is 1/scale tougher"). Uniform, so it changes battle DURATION, not who wins. The "per-shot-energy ÷ hull-toughness" knob (hot-damage rebalance 2026-06-25) | `CombatEngagement.cs` |
| `BattleTriggerProcessor` run frequency | 5 s | how often each system is scanned for battles | `BattleTriggerProcessor.cs` |

---

## Combat interrupt — stop the clock at first contact (added 2026-06-26)

**The problem it fixes.** Combat is paced in **game-time** — one salvo every 5 s (`BattleTriggerProcessor`), stretched over minutes by `SalvoDamageScale`. But the player advances game-time in **chunks** (default 1-hour step / continuous play). Advancing an hour silently runs all ~720 of the 5-second sub-pulses inside one button press, so a whole battle — first contact to total loss — resolves *invisibly* between two frames. The developer's report (2026-06-26): "I had no notice of when combat began and no time to intervene; all fleets destroyed in one tick." The pacing was fine; there was **no interrupt** to hand control back when fighting started.

**The fix has TWO halves — request the halt, and honour it at first contact.**

*Half 1 — request the halt.* `CombatEngagement.EnsureInCombat` (the JOIN primitive — fires once per fleet's first entry, it's guarded) calls `fleet.Manager.Game.TimePulse.RequestCombatHalt()` when `CombatEngagement.InterruptTimeOnNewEngagement` is true. `RequestCombatHalt` (`MasterTimePulse`) sets `CombatInterruptPending` and cancels `_timeSimulationCts` — the **exact** cancellation `PauseTime` uses — and the next `StartTime`/`TimeStep` restarts cleanly (each makes a fresh CTS).

*Half 2 — honour it at first contact (the granularity fix, 2026-06-26 round 2).* Cancelling the token is **not enough on its own**: `MasterTimePulse.SimulateTimeUntil` only checks the token at its master-loop boundary, and when nothing schedules a cross-system interrupt a whole `Ticklength` (default **1 game-hour**) is processed inside ONE `ManagerSubpulses.ProcessSystem` call. So the first version still ran the entire battle before the cancel was seen — the developer's round-2 report: *"combat initiation still happened suddenly… the second round between both leviathans should have given me more time."* The fix: when the interrupt is armed **and a brand-NEW engagement is about to fire**, the master loop advances in **fine sub-steps** of `MasterTimePulse.CombatReactionStep` (**5 s**, matched to the battle-trigger frequency) instead of a full Ticklength — so the loop returns to its boundary, sees the cancelled token, and stops **within one 5-second sub-pulse of first contact**. The gate is `CombatEngagement.NewEngagementImminent(manager)` (read-only: any two hostile fleets with ships in `EngagementRange_m` where **at least one is not yet in combat** — i.e. an entry/join would fire on the next trigger pass), checked per active system via `MasterTimePulse.AnyNewEngagementImminent()`. The clock stops **after the engaging tick's first salvo** (Tick engages *and* steps once), so the player lands in a barely-scratched battle with full control: change doctrine, assess, then drive forward at whatever speed they choose.

**Combat runs at the player's set speed — the engine only takes the wheel as a fight is BORN (the design choice, developer's call 2026-06-26).** The gate is keyed on a *new* engagement, **not** "any combat active". So the fine 5-second stepping kicks in only on the iteration a battle is forming — just long enough to land the auto-pause — and then the clock is handed straight back at the player's chosen `Ticklength` for the ONGOING exchange. Set a 1-second step to crawl through it salvo by salvo; set 10 minutes or an hour to resolve it fast. This was a deliberate reversal of the first cut (which forced 5 s through the *whole* battle and so overrode the time slider mid-fight — the developer correctly flagged that as a hidden second clock). The auto-pause is the only special behaviour: it guarantees you are never blindsided by a fight's START, then gets out of the way. A fresh fleet JOINING later (round 2) is itself a new engagement, so it re-pauses the same way.

**Why fine-stepping is cheap away from combat.** The gate is only consulted when a cap could apply (`GameGlobalDateTime + CombatReactionStep < target`) **and** the interrupt flag is on — and with no new engagement near, `NewEngagementImminent` returns at once and the loop takes a **full Ticklength** step exactly as before. So peacetime fast-forward *and* an ongoing battle both run at the set speed; the 5-second granularity only appears for the instant a fight is born. Lockstep is preserved — all systems still advance to the same (capped or full) target each iteration, so there is **no cross-system time desync** (a system left behind would trip the `Temporal Anomaly` guard).

**The flag (mirrors `NarrateToLog`).** `InterruptTimeOnNewEngagement` defaults **false** so every headless/combat test advances deterministically (a halt mid-`AdvanceTime` would break the timed sims, and the fine-step gate stays inert); the **client turns it on** at startup (`PulsarMainWindow` ctor, next to `NarrateToLog = true`). The client surfaces the halt: `PulsarMainWindow.PostFrameUpdate` reads+clears `TimePulse.CombatInterruptPending`, writes a `[ACTION] COMBAT INTERRUPT` SessionLog line, and arms an on-screen banner (`RenderDebugText`, ~8 s) — so the auto-pause reads as "combat started," not a mystery stop.

**v1 stubs / follow-ups.** (a) Halts on **any** new engagement, not only the player's — the engine has no client-side "player faction" concept; "only interrupt *my* battles" is a v2 refinement once that's known engine-side. (b) One salvo of damage lands before the halt (engage + step share a Tick); halting *before* the first salvo needs engage-this-tick / step-next-tick separation (v2). (c) The fine-step gate keys on **present, in-range** state, so a fleet that crosses from out of range to engaged inside a single step (e.g. a long warp resolved under a big Ticklength) is resolved without a pause — a closing-prediction lookahead is v2; the common case (fleets already parked together, e.g. at the same body, or a player stepping carefully through a fight) is fully covered. (d) Because the gate only fine-steps a *new* engagement, an ONGOING fight runs at the player's set step — so a big step resumed mid-battle resolves a lot of it in one click (their choice); to watch it unfold they set a small step. That also means a JOIN that occurs *mid-big-step* (rather than at step start) won't pause until the step ends — same root as (c).

**Connections (Prime Directive):** **Feeds IN** — `InterruptTimeOnNewEngagement` flag; `EnsureInCombat` join event; `fleet.Manager.Game.TimePulse`; `NewEngagementImminent` reads every active system's fleets/combat state. **Feeds OUT** — `MasterTimePulse.RequestCombatHalt` (cancels the sim) + `CombatInterruptPending` (UI notice); `SimulateTimeUntil` caps its per-iteration target only while a new engagement is forming. **Triggers** — the time loop stops (same mechanism as `PauseTime`) and, for the instant a fight is born, runs at 5-second granularity; otherwise the player's set step. **Tests:** `BattleTriggerTests.Tick_NewEngagement_RequestsCombatHalt_WhenInterruptEnabled` (flag on → a new engagement sets `CombatInterruptPending`; asserts the flag defaults off; try/finally so the static flag never leaks) and `NewEngagementImminent_*` (the gate reads true for two hostile un-engaged-in-range and for one-engaged-one-joining; **false for BOTH-already-engaged** — the ongoing fight runs at the set speed — and for friendly-only / no-fleets). The *clock-stops-early* integration is verified **live** (CI's colony harness doesn't reliably auto-fire the battle-trigger hotloop on `TimeStep`, the same reason the combat fixtures drive `Tick` directly).

---

## Fog of war — combat only acts on what you DETECT (added 2026-06-26, detection slice 2)

**The seam that makes detection × weapons real.** The battle trigger used to read *every* fleet present (`GetAllEntitiesWithDataBlob<FleetDB>()`) — omniscient. With `CombatEngagement.RequireDetectionToEngage` on, the engage pass also requires the two hostile fleets to **detect** each other: `FleetDetects(a, b)` checks the per-faction track table (`EntityManager.GetSensorContacts(factionId).SensorContactExists(shipId)`) the sensor scan populates. An undetected hostile can't pull you into a battle — you fight what you *see*.

- **A fight forms once EITHER side detects the other (updated — first-strike, slice 5).** The engage pass now gates on `FleetDetects(a, b) || FleetDetects(b, a)` (was `&&` / mutual). Both fleets enter combat so the resolver has the target present to be shot; the BLIND one simply doesn't shoot back. (The old mutual rule deliberately avoided one-way thrash, but the directed-fire resolver below handles the one-sided case cleanly, so either-detects is now correct.)
- **Live diagnostics (for the CI-blind client play-test).** `CombatEngagement.TickCount` (a `public static long`, Interlocked-incremented at the top of `Tick`) is a liveness counter the client's `SessionLog` heartbeat logs as `[ENGINE] battle-trigger passes M (+delta)` — so a remote review can tell "no battle because nothing's hostile/in-range/detected" from "the trigger never fired on play" (the documented open question). And when fog is on and a NEW engagement forms with one side blind, `Tick` writes a one-time `[Combat] FIRST-STRIKE: …` line (gated on `NarrateToLog` + `RequireDetectionToEngage`, so it's inert in tests and never re-logs once both fleets hold combat state). Sibling counter: `SensorScan.ScanCount` for the detection engine.
- **First-strike — directed fire in the resolver (slice 5).** `StepEngagementGroup` no longer builds a symmetric enemy set. It builds **directed** `targetsOf[i]` (who i can shoot) and `attackersOf[i]` (who can shoot i) via `CanEngageTarget(attacker, target)` = *fog-off → always; fog-on → attacker DETECTS target*. So the side that sees first shoots first, and a fleet that hasn't detected its attacker takes fire without returning it (until its own scan finds the shooter, or it's wiped). **With fog OFF, `CanEngageTarget` is always true, so both directions fill and this is byte-identical to the old symmetric exchange — every existing combat fixture is unchanged.** A fleet drops out only when it can neither shoot anyone NOR be shot (a one-sided aggressor stays in to keep firing; a blind victim stays in to keep taking fire); whole-engagement end is unchanged (hostility-based, so the fight runs until one side is gone).
- **The flag (mirrors `NarrateToLog` / `InterruptTimeOnNewEngagement`).** Default **FALSE** so every existing combat fixture stays deterministic (they don't stand up sensors). **Currently OFF in the client too, by choice** — it changes *when* combat triggers (you wait for a sensor sweep), so it's left off until the developer live-tests detection deliberately; flipping it on is one line in `PulsarMainWindow` next to the other two flags.
- **Tests:** `BattleTriggerTests.Tick_RequireDetection_NoBattleUntilDetected` — flag on, two hostile *sensor-capable* fleets in range: **no scan → no battle**; fire the scan → **battle**. And `FirstStrike_SeerWipesBlindEnemy_Unscathed` — two EQUAL armed fleets, the enemy BLINDED (sensors shot off — the grave-rung path), fog on: the player wipes it taking **zero** losses (equal forces both firing would be mutual destruction, so player-intact + enemy-wiped is the proof). Both assert the flag defaults off + try/finally so it never leaks.
- **Connections (Prime Directive / cradle-to-grave):** contacts come from sensor **components** (researched/built/installed) → a **destroyed** sensor (the grave rung — **now wired**: `SensorTools.SetInstances` via `ReCalcProcessor` on damage empties the ship's receiver cache so it stops scanning; see `Sensors/CLAUDE.md` → "Grave rung") = you go blind. **Stacks with** the combat interrupt (you're paused when a *detected* battle begins) and doctrine (detect-first = set posture before contact).

---

## Gotchas

1. **This engine does not touch the per-pixel damage sim.** Casualties are whole-ship removal driven by strength math, not `DamageProcessor.OnTakingDamage`. Do not wire combat value into the pixel sim — that path is broken and parked for v2.
2. **Combat value is computed once at build (v1).** Recalc-on-damage is a v2 refinement; in v1 a ship is alive at full value or removed whole, so a value cached at build is sufficient.
3. **`ComponentInstancesDB.AllComponents` is `internal`.** Combat code reads it because it lives in the same `GameEngine` assembly; tests reach it via `InternalsVisibleTo("Pulsar4X.Tests")`.
4. **Firepower mixes precise beam J/s with a flat missile stub.** The number is a *relative* strength figure for the salvo math, not a physical unit — don't read absolute meaning into it until missile ordnance energy is wired.
5. **The battle trigger is the one combat piece with live side effects.** `BattleTriggerProcessor` runs every 5 s on every star system and **destroys ships** when hostile fleets fight. It is keyed to `StarInfoDB` (not `FleetDB`, which `FleetOrderProcessor` already owns — one processor per DataBlob type) and returns `>= 1` so it never sleeps. Keep its constructor trivial and `CombatEngagement.Tick` defensive (no throws) — a throwing hotloop processor crashes the whole game loop and CI's `GameLoopSmokeTests`.
6. **Hostility is "different non-neutral faction" (v1 stub).** No diplomacy/relations system exists, and the engine's `EntityFilter.Hostile` also requires a sensor contact (stubbed away in v1). When relations/sensors are built, route `CombatEngagement` hostility through them.
7. **`FleetDoctrineDB` (fleet posture) ≠ `FactionInfoDB.Doctrine` (strategic AI vector).** Same word, different systems. Doctrine effects are read-time multipliers (`BonusesDB` pattern) applied in `StepEngagementGroup` — never bake them into ship stats. `AutoResolve` (pure ship-list) intentionally has no doctrine; it lives in the engagement where fleets do.
8. **`StepEngagement(a, b, dt)` is the n = 2 special case of `StepEngagementGroup` — they MUST stay behaviourally identical for two fleets.** Every existing combat fixture (`BattleTrigger`, `Retreat`, `Performance`, `Dodge`) drives the two-fleet path and is the tripwire: if a change to the group resolver diverges from the old two-fleet exchange (fire division, pool carry-over, combatants-first casualties, retreat/end ordering), those go red. The reduction holds because with one enemy: fire is divided by 1 (= full), each fleet has its own pool, and "fewer than two hostile sides remain" releases both exactly when the loser is wiped/retreats.
9. **`FleetCombatStateDB.OpponentFleetId` is a *representative* opponent, not the sole one (multi-party).** State is per-fleet; a fleet can face many hostiles. The resolver keeps `OpponentFleetId` pointed at one live enemy for the readout — do not treat it as "the" opponent or rebuild pairing from it (the old lower-Id-drives pairing is gone). Membership is "all in-combat fleets in the system."
10. **Multi-party sides = factions, and one system = one engagement (v1 stubs).** Hostility is per-pair (`AreHostile`), so a 3-way already resolves, but fire-division and "side" are first-pass. Every in-combat fleet in a manager is one battle (range ≈ whole system); real weapon-range clustering into distinct simultaneous battles is v2. When sensors/diplomacy land, route sides + clustering through them.
