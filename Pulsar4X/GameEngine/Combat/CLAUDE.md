# Combat — Subsystem Reference (Auto-Resolve Engine)

The **one** combat engine for this fork: a math loop that resolves fleet battles off each ship's "spec sheet," with **doctrine** as the player's only lever. Lives in `GameEngine/Combat/`. This is the v1 spine described in `docs/COMBAT-DESIGN.md` -> "What we're building (v1)".

It deliberately does **not** use the per-pixel damage sim (`Damage/DamageComplex` / `DamageVeryComplex`). That path deposits ~0 damage today (see `Damage/CLAUDE.md`) and is parked as a v2 visual skin. The auto-resolver decides casualties by **strength math**, not by simulating hits.

> **Status: under construction.** Built piece-by-piece, each under a test, in the order in `docs/COMBAT-DESIGN.md` -> "Build order". This file grows as each piece lands.

---

## File Map

| File | Purpose | Status |
|------|---------|--------|
| `ShipCombatValueDB.cs` | DataBlob: a ship's **Firepower** (joules/sec from beams + a missile-launcher stub) and **Toughness** (live components + armour), plus a **RoleWeight**. Computed once at build time. `ShipCombatValueDB.Calculate(Entity)` is the calculator. | ✅ built (spine step 2) |
| `AutoResolve.cs` | The salvo-exchange resolver: `AutoResolve.Resolve(sideA, sideB, config)` runs the math loop (strength → damage pools → whole-ship casualties, combatants first) until one side is gone, both are, or a frozen fight hits the round cap. Returns an `AutoResolveResult` (outcome + casualty lists); **pure** — it reports casualties, it does not destroy them. Plus `AutoResolveConfig`, `BattleOutcome`. | ✅ built (spine step 3) |
| `FleetCombatStateDB.cs` | DataBlob marking a fleet as **engaged** (opponent fleet id, accumulated damage pool, steps fought, starting ship count for the retreat threshold). On both fleets during a battle; removed when it ends. Its presence is the "in combat" flag the engagement lock (step 11) keys on. | ✅ built (spine steps 4, 7) |
| `CombatEngagement.cs` | The trigger's engine logic: `Tick(manager, dt)` finds hostile fleets in range, starts engagements, and steps active ones (incremental over game-time) until one side is wiped. `GetCombatShips(fleet)` collects ships tagged with their **component's** doctrine multipliers (step 6); `GetFleetShips` is the flat list (counts/detection). Hostility/range/detection are v1 stubs. Testable directly. | ✅ built (spine steps 4, 6) |
| `BattleTriggerProcessor.cs` | `IHotloopProcessor` (every 5 s) that calls `CombatEngagement.Tick` per star system. Keyed to `StarInfoDB` (FleetDB is already taken). | ✅ built (spine step 4) |
| `FleetDoctrineDB.cs` | DataBlob: a fleet's **active** combat posture (doctrine id, firepower/toughness/speed multipliers, retreat flag, switch-cooldown clock). Read by the engagement as a strength/toughness modifier. | ✅ built (spine step 5) |
| `FleetDoctrine.cs` | Helpers: `FirepowerMult`/`ToughnessMult`/`IsRetreat(fleet)` reads; `TrySetDoctrine(fleet, blueprint, now)` sets a posture from the catalog, honouring the cooldown. | ✅ built (spine step 5) |
| `CombatDoctrineBlueprint` (`Engine/Blueprints/`) | The moddable **catalog** of postures (JSON → `ModDataStore.CombatDoctrines`): family, display name, the multipliers, cooldown, retreat flag. | ✅ built (spine step 5) |
| `FleetRetreatDB.cs` | DataBlob recording that a fleet **broke off** (flag + a withdraw vector away from the enemy + who it fled from). Attached when a fleet retreats; persists after the engagement ends (so the outcome stays visible). v1 records the vector only — no move order. | ✅ built (spine step 7) |

*Fleet components (step 6) reuse `FleetDB` sub-fleets + `FleetDoctrineDB` — no new file; see "Fleet components" below. (Retreat, step 7, adds rows as it lands.)*

---

## ShipCombatValueDB — the spec sheet

**What it is.** Two numbers that rate a ship for the auto-resolver, read from the ship's REAL parts the moment it's built and cached on the entity:

- **`Firepower`** — hurt-per-second. Each beam weapon contributes `Energy ÷ ChargePeriod` (joules/sec), scaled by that component's `HealthPercent`. Each missile launcher adds a flat `MissileLauncherFirepowerStub` (v1 stub).
- **`Toughness`** — how much it can take, **in joules absorbed**. Each live component contributes `HealthPercent × ComponentHitPoints_J` (1e5 J kills a component — straight from the damage tuning: 1000 dmg-points × 100 J), plus `armour.thickness × ArmorHitPointsPerThickness_J`. Same currency as `Firepower × time`, so the salvo loop's time-to-kill comes out in seconds.
- **`RoleWeight`** — `1.0` for anything that can shoot, `UtilityRoleWeight` (0.25) for a utility hull. The auto-resolver uses it so utility/transport ships are low-priority targets (absorb casualties last) and contribute less strength. v1 stub.

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

1. **Detects** pairs of hostile fleets in the same system that are in range and not already fighting, and **starts an engagement** — attaches `FleetCombatStateDB` to both (this is also what trips the engagement lock, step 11).
2. **Steps** each active engagement forward by `dt` game-seconds: both sides add `strength × dt` joules to the other's damage pool and lose whole ships (combatants first) — the same math as `AutoResolve`, but spread across game-time so a battle plays out instead of resolving instantly.
3. **Ends** the engagement when one fleet is wiped (or the fight stalls), removing `FleetCombatStateDB` from both (clearing the lock).

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

**Test:** `Pulsar4X.Tests/CombatTestShipsTests.cs` — the two designs load onto the faction and rate strong-vs-weak
(warship out-guns + out-armours the corvette); a 3v3 auto-resolve is a decisive `SideAVictory` with all corvettes lost.

---

## Model-coupled / tuning constants

| Constant | Value | Meaning | Where |
|----------|-------|---------|-------|
| `MissileLauncherFirepowerStub` | 100,000 | flat firepower (J/s) per missile launcher until ordnance warhead energy is wired (v2) | `ShipCombatValueDB.cs` |
| `UtilityRoleWeight` | 0.25 | combat-value role weight of a hull with no weapons | `ShipCombatValueDB.cs` |
| `ComponentHitPoints_J` | 100,000 | joules one component absorbs before destruction (= the damage tuning's "100 kJ kills a component") | `ShipCombatValueDB.cs` |
| `ArmorHitPointsPerThickness_J` | 100,000 | joules of toughness added per unit of armour thickness | `ShipCombatValueDB.cs` |
| `AutoResolveConfig.RoundSeconds` | 5.0 | game-seconds of fire per salvo round | `AutoResolve.cs` |
| `AutoResolveConfig.MaxRounds` | 2000 | round-cap backstop; hitting it = Stalemate | `AutoResolve.cs` |
| `CombatEngagement.EngagementRange_m` | 1e9 (1M km) | v1 flat auto-engage distance (real value = weapon range, v2) | `CombatEngagement.cs` |
| `CombatEngagement.MaxSteps` | 5000 | per-engagement step cap (stalemate backstop) | `CombatEngagement.cs` |
| `CombatEngagement.RetreatCasualtyThreshold` | 0.5 | fraction of starting ships a fleet must lose to break off (v1 flat; real value = per-doctrine, v2) | `CombatEngagement.cs` |
| `BattleTriggerProcessor` run frequency | 5 s | how often each system is scanned for battles | `BattleTriggerProcessor.cs` |

---

## Gotchas

1. **This engine does not touch the per-pixel damage sim.** Casualties are whole-ship removal driven by strength math, not `DamageProcessor.OnTakingDamage`. Do not wire combat value into the pixel sim — that path is broken and parked for v2.
2. **Combat value is computed once at build (v1).** Recalc-on-damage is a v2 refinement; in v1 a ship is alive at full value or removed whole, so a value cached at build is sufficient.
3. **`ComponentInstancesDB.AllComponents` is `internal`.** Combat code reads it because it lives in the same `GameEngine` assembly; tests reach it via `InternalsVisibleTo("Pulsar4X.Tests")`.
4. **Firepower mixes precise beam J/s with a flat missile stub.** The number is a *relative* strength figure for the salvo math, not a physical unit — don't read absolute meaning into it until missile ordnance energy is wired.
5. **The battle trigger is the one combat piece with live side effects.** `BattleTriggerProcessor` runs every 5 s on every star system and **destroys ships** when hostile fleets fight. It is keyed to `StarInfoDB` (not `FleetDB`, which `FleetOrderProcessor` already owns — one processor per DataBlob type) and returns `>= 1` so it never sleeps. Keep its constructor trivial and `CombatEngagement.Tick` defensive (no throws) — a throwing hotloop processor crashes the whole game loop and CI's `GameLoopSmokeTests`.
6. **Hostility is "different non-neutral faction" (v1 stub).** No diplomacy/relations system exists, and the engine's `EntityFilter.Hostile` also requires a sensor contact (stubbed away in v1). When relations/sensors are built, route `CombatEngagement` hostility through them.
7. **`FleetDoctrineDB` (fleet posture) ≠ `FactionInfoDB.Doctrine` (strategic AI vector).** Same word, different systems. Doctrine effects are read-time multipliers (`BonusesDB` pattern) applied in `StepEngagement` — never bake them into ship stats. `AutoResolve` (pure ship-list) intentionally has no doctrine; it lives in the engagement where fleets do.
