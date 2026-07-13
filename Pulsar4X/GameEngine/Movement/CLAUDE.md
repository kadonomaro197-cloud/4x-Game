# Movement — Subsystem Reference

Newton physics, warp drive, inter-system jumps, pathfinding. Lives in `GameEngine/Movement/`.

---

## File Map

| Directory / File | Purpose |
|-----------------|---------|
| `NewtonMove/NewtonMoveDB.cs` | DataBlob: current velocity vector, parent body for relative position. Attached when an entity is in free-flight Newtonian motion. |
| `NewtonMove/NewtonSimpleMoveDB.cs` | Simplified version of NewtonMoveDB for certain cases. |
| `NewtonMove/NewtonThrustAbilityDB.cs` | DataBlob: the entity's propulsion capability (thrust, Isp, fuel capacity, fuel current). |
| `NewtonMove/NewtonThrustCommand.cs` | Order: apply a delta-V maneuver at a specific datetime. `CreateCommands(cargoLibrary, entity, manuvers)` builds a sequence of thrust commands. |
| `NewtonMove/NewtonSimpleCommand.cs` | Simplified thrust command. |
| `NewtonMove/NewtonionThrustAtb.cs` | Component design attribute that grants `NewtonThrustAbilityDB`. |
| `NewtonMove/ReactionlessThrustAtb.cs` | **NEW (Propulsion ⚙2 · P2, 2026-07-10)** The Exotic REACTIONLESS drive component — sets `NewtonThrustAbilityDB.ThrustInNewtons` DIRECTLY (not Ve×burnRate) + marks `NewtonThrustAbilityDB.Reactionless` true, giving **no-propellant, unlimited-Δv** thrust. `Reactionless` (default false → byte-identical) pins `DeltaV = ReactionlessDeltaV` (1e12) and is honoured at the single fuel recompute funnel `NewtonThrustAbilityDB.SetFuel` (which `CargoTransferProcessor.UpdateMassFuelAndDeltaV` routes through — also early-returns for a reactionless drive to skip the fuel lookup). Combat payoff: `FleetCombat.DeltaVFloor` → `ManeuverBudget` never depletes (kite forever). Base-mod `reactionless-drive` (`engines.json`) on the **Nomad** cruiser. **v1 = the combat/closing payoff + strategic Δv readout; the in-space burn model (`NewtonianMovementProcessor` burning without consuming fuel) is a flagged follow-up.** Gauge `ShipReactionlessDriveTests`. |
| `NewtonMove/NewtonianMovementProcessor.cs` | `IHotloopProcessor` (1 sec). Integrates velocity over delta-time, calls `MoveStateProcessor`. **Note: the class inside this file is spelled `NewtonionMovementProcessor` (misspelled in source) — match that name in code, not the file name.** |
| `NewtonMove/NewtonSimpleProcessor.cs` | Simpler movement processor for non-thrust entities. |
| `WarpMove/WarpAbilityDB.cs` | DataBlob: warp drive capability (speed, fuel). |
| `WarpMove/WarpMovingDB.cs` | DataBlob: entity currently in warp transit (has destination, ETA). |
| `WarpMove/WarpMoveProcessor.cs` | `IHotloopProcessor`. Advances warp transit, delivers entity to destination orbit. |
| `WarpMove/WarpMoveCommand.cs` | Order: begin warp to a target orbit. |
| `WarpMove/WarpDriveAtb.cs` | Component attribute granting warp drive ability. |
| `WarpMove/WarpMath.cs` | `CalcMaxWarpSpeed()`, transit time calculations. |
| `NavSequence/NavSequenceDB.cs` | DataBlob: `List<Manuver> ManuverNodes` — an ordered list of Newtonian delta-v maneuver nodes (the orbital maneuver planner's output), NOT generic waypoints/orders. The list is a plain public field, not `[JsonProperty]`-persisted. |
| `NavSequence/NavSequenceProcessor.cs` | `IInstanceProcessor`. Executes the next item in the nav sequence queue. |
| `NavSequence/NavSequenceCommand.cs` | Order: append or replace the nav sequence. |
| `Pathfinding/PathfindingManager.cs` | Graph-based pathfinder over the jump point network. |
| `Pathfinding/Graph.cs`, `Node.cs`, etc. | Standard A* graph data structures. |
| `InterSystemJumpProcessor.cs` | **Static class** (not an `IHotloopProcessor`/`IInstanceProcessor`). Would move an entity through a jump point between star systems via `JumpOut` (`game.GlobalManager.Transfer`) / `JumpIn` (`JumpSystem.Transfer`), scheduled by `SetJump`. **Currently unwired: `SetJump` has no live caller (its only reference is a commented-out test line), so this path does not run in-game today.** |
| `InterceptCalcs.cs` | `OrbitPhasingManuvers()`, intercept calculations used by missile guidance. |
| `MoveMath.cs` | `GetRelativeFutureVelocity()`, vector helpers. |
| `MoveState.cs`, `MoveStateProcessor.cs` | State machine for entity movement modes (orbiting / thrusting / warping). |
| `MoveToNearestAction.cs` et al. | Fleet action helpers: move to nearest colony, anomaly, geo survey target. |

---

## Movement Modes

An entity's movement mode is determined by which DataBlobs it currently holds:

| Mode | DataBlob | Processor |
|------|----------|-----------|
| Stable orbit | `OrbitDB` or `OrbitUpdateOftenDB` | `OrbitProcessor` (in `Orbits/`) |
| Free-flight (Newtonian) | `NewtonMoveDB` | `NewtonianMovementProcessor` (1 sec) |
| Warp transit | `WarpMovingDB` | `WarpMoveProcessor` |
| Idle / docked | none of the above | — |

Transitions between modes work by adding/removing DataBlobs:
- Issuing a warp order → adds `WarpMovingDB`, may remove `OrbitDB`.
- Completing warp → removes `WarpMovingDB`, adds `OrbitDB` at destination.
- Thrusting → `NewtonMoveDB` present; after burn-out the entity coasts.

---

## Newtonian Movement

`NewtonianMovementProcessor.ProcessManager()`:
1. Gets all entities with `NewtonMoveDB`.
2. Calls `NewtonMove(db, toDateTime)` — integrates position using current velocity.
3. Calls `MoveStateProcessor.ProcessForType(nmdb, toDateTime)` — checks for state transitions (orbit capture, thrust execution).

`NewtonThrustCommand` encodes a delta-V to apply at a specific datetime. The `NewtonianMovementProcessor` applies it when the scheduled time is reached.

**Fuel is tracked in `NewtonThrustAbilityDB`.** Thrust commands consume fuel. When fuel = 0 the entity can no longer maneuver but still coasts.

---

## Warp Movement

Warp is an abstraction that moves an entity from one orbit to another without simulating the full transit physics.

`WarpMoveCommand` → adds `WarpMovingDB` with target body and ETA → `WarpMoveProcessor` counts down → at ETA: removes `WarpMovingDB`, creates appropriate orbit at destination.

Warp speed is calculated by `WarpMath.CalcMaxWarpSpeed()` based on `WarpAbilityDB` properties.

**Fleet moves as ONE — the slowest unit sets the pace (added 2026-06-27).** A fleet move used to issue a
SEPARATE warp command per ship, each at its OWN `WarpAbilityDB.MaxSpeed` — so a fast fighter raced ahead of the
heavies and the fleet **scattered** (the developer's "they all split up" report). Now a fleet move caps every
ship to the fleet's **slowest** warp speed, so they arrive together. Wiring (an optional `speedCap_m`/`speedOverride_m`
threaded leaf-to-root, default 0 = the ship's own speed, so **every existing single-ship/missile caller is byte-
identical**): `WarpMath.GetInterceptPosition(…, speedOverride_m=0)` caps `spd_m` → `WarpMovingDB` ctor
(`speedCap_m`) bakes it into `PredictedExitTime` → `WarpMoveCommand.SpeedCap_m` (a `[JsonProperty]`, so a reloaded
mid-warp order keeps the capped ETA) + `CreateCommandEZ(…, speedCap_m=0)`. Both fleet-move orders compute the floor
(min `MaxSpeed` over warp-capable children) and pass it: `MoveToSystemBodyOrder` (the Fleet window "Move to…"
button) and `WarpFleetTowardsTargetOrder`. Gauge: `WarpFleetMoveTests.WarpSpeedCap_SlowerCap_ArrivesLater` (a
slower cap → a strictly later arrival, tested on the math fn directly — warp transit itself has no harness). v1
caps SPEED only (so they arrive together); true formation-keeping / follow-the-leader's-path is a later layer.

> **The cap reached the ETA but NOT the actual velocity — fixed 2026-06-27 (the "Aegis lagged a day behind" bug).**
> The cap was baked into `WarpMovingDB.PredictedExitTime` (the ctor calls `GetInterceptPosition` with `speedCap_m`),
> but `WarpMoveProcessor.StartNonNewtTranslation` — which sets the **real** transit velocity
> (`CurrentNonNewtonionVectorMS`, the thing the per-tick `WarpMove` actually advances the ship by) — read
> `WarpAbilityDB.MaxSpeed` directly, ignoring the cap. So every ship flew at its OWN speed and arrived at its own-
> speed time: a live play-test showed five fleet-mates with five different ETAs (the heavy Aegis a full day behind
> the flak escort), exactly the "they start together then the slow one never catches up" report. Root cause: the cap
> was never **stored** on `WarpMovingDB` for the processor to read. Fix: new `WarpMovingDB.WarpSpeed_mps` (set in all
> three ctors = `speedCap_m > 0 ? speedCap_m : MaxSpeed`, copied in the copy-ctor, `[JsonProperty]` so a reloaded
> mid-warp keeps it), and `StartNonNewtTranslation` now uses `moveDB.WarpSpeed_mps` (fallback to `MaxSpeed` for old
> saves where it's 0) for both the velocity AND the ETA. Gauge: `WarpFleetMoveTests.
> WarpMovingDB_StoresTheCappedSpeed_SoTheProcessorUsesIt` (capped → stores the cap; 0 → own MaxSpeed). **Lesson: a
> "predicted" value and the value the sim actually integrates are two different numbers — cap BOTH, or the
> prediction lies.**

> **Warp-start must not reparent a ship to ITSELF — fixed 2026-06-28 (the "moved two fleets at once and the UI
> broke" crash).** `WarpMoveProcessor.StartNonNewtTranslation` detaches the ship to the top of its position tree
> before warp (so absolute position is preserved). The old guard was `if(Parent != Root) SetParent(Root)` — which
> does NOT actually prevent setting the parent to self: a ship **already mid-warp** has a **null** position parent,
> so `PositionDB.Root` (`TreeHierarchyDB.cs`: `ParentDB?.Root ?? OwningEntity`) walks up and resolves to the **ship
> itself**. Then `SetParent(Root) = SetParent(self)` throws `ArgumentException: Cannot set the parent entity equal
> to self` (the `Parent` setter forbids it). Live trigger: issuing a fleet move while those ships were **already in
> warp** (easy when moving two fleets at once — the second order hits ships the first order just launched). Because
> the order is **executed synchronously inside the Fleet window's `Display()`** (between ImGui `Begin`/`End`), the
> throw corrupted the whole ImGui frame → a cascade of "already inside window Fleet Management" render errors and a
> broken-looking UI. Fix: skip the reparent when `Root` is the entity itself (already at the top — nothing to
> detach): `if(root != entity && root != Entity.InvalidEntity && Parent != root) SetParent(root)`. Defence-in-depth
> (engine side, so CI covers it): `StandAloneOrderHandler.HandleOrder` now wraps the synchronous order execution in
> a logged `try/catch` (`[OrderError] …`) so **no** future order-throw can blank the UI again. Gauge:
> `WarpFleetMoveTests.WarpStart_OnAnAlreadyDetachedShip_DoesNotThrowSelfParent` (a detached ship is its own root;
> warp-start must not throw). **Lesson: `Root` of a detached node is the node itself — any "reparent to root" must
> guard the already-root case, and any order run inside a render frame must not be allowed to throw into it.**

**Diagnostic — `[WARP]` log (added 2026-06-26).** `WarpMoveProcessor.NarrateWarpToLog` (static bool, default false; the client sets it true in `PulsarMainWindow`) narrates the warp lifecycle to the captured log: `[WARP] ship #N 'Name' departing → 'Target' (distance Gm, ETA datetime)` when `StartNonNewtTranslation` fires, and `[WARP] … arrived at 'Target'` when it reaches the destination. **Why:** a warp that DEPARTS but never ARRIVES stands out right next to a `⚠ TELEPORT` flag — the open warp-detach bug (gotcha-tracked in `SESSION_STATE.md`: a ship reparented to root mid-warp whose position then collapses to origin). Off by default so tests/headless stay quiet; mirrors `Combat.CombatEngagement.NarrateToLog`. Engine-side (`System.Console.WriteLine`), so it lands in the client's rotating `game_logs/` pages via the console redirect.

**Silent warp failures are now loud (`WarpBlocked`, added 2026-06-26).** `WarpMoveCommand.Execute` used to `return` with no message (the literal `// FIXME: alert the player?`) when a ship couldn't warp — the "I gave a move order and the ship just sat there" symptom. It now logs `[WARP] ship #N 'Name' CAN'T WARP — <reason>` via `WarpMoveProcessor.WarpBlocked` for each blocker: **no warp drive** on the design, **no reactor/power**, or **not enough stored energy** (with the numbers needed vs held). No-drive/no-reactor are permanent (`_permanentlyBlocked` → the order clears instead of hanging the move lane); the energy case keeps the order pending so it warps the instant the reactor charges. **Gotcha it surfaces — and the spawn-side fix (`ChargeReactors`, 2026-06-27):** a freshly built ship starts with **0 stored reactor energy** (`EnergyStored` is initialised to 0 in `EnergyGenerationAtb`/`EnergyStoreAtb`), and the warp bubble is paid from **stored electricity** (not fuel) — so a 0-charge ship handed a move order just sits there. This was THE "I spawned a ship, ordered it to move, and nothing happened" bug, and the precise answer to *"what do the premade ships have that ours don't?"*: the **start fleet is hand-charged** (`DefaultStartFactory` sets `EnergyStored = 2,750,000`) so it warps instantly, but a **DevTools/sandbox-spawned ship was never charged**. Fixed by **`ShipFactory.ChargeReactors(ship)`** — the energy sibling of `FillFuelTanks` — now called by the DevTools "Spawn Ship" path and `CombatSandbox.SpawnHostileFleet` right after the fuel fill. It tops `EnergyStored` up to the ship's own `EnergyStoreMax` (a charged base-mod battery holds **2×–4× one warp bubble** at starting tech, so a charged ship can always warp; also lets a spawned ship FIRE, since weapons draw stored energy too). A ship built the *normal* way (production at a colony) still earns its charge over game-time — **advance time and it goes.** Sensor: `CombatTestShipsTests.ChargeReactors_FillsStoredEnergy_SoASpawnedShipCanWarp`. (Block reason still logged once per order, not every tick.)

---

## Nav Sequence

`NavSequenceDB` holds `List<Manuver> ManuverNodes` — an ordered list of Newtonian delta-v maneuver nodes (each `Manuver` is a struct: a `ManuverType`, start/end datetimes, start/end Kepler elements, and start/end SOI-parent entities). It is the output of the orbital maneuver planner, NOT a generic queue of mission actions. **There is no `INavAction` type anywhere in the repo** — the earlier "move-to-body / warp / refuel / resupply / survey anomaly" list was wrong. (Refuel/resupply/survey-anomaly do exist, but as `EntityCommand` subclasses — see `Fleets/CLAUDE.md` — and they are stubs, not nav-sequence items.) `NavSequenceProcessor` (instance processor) works the maneuver nodes.

---

## Inter-System Jumps

`InterSystemJumpProcessor` is a **static class** that would handle the cross-system transit. Entry point `SetJump(game, exitTime, entrySystem, entryTime, jumpingEntity)`:
1. Schedules two `game.TimePulse.AddSystemInteractionInterupt(...)` events — a `JumpOutProcessor` at `exitTime` and a `JumpInProcessor` at `entryTime`.
2. `JumpOut` moves the entity to the global manager via `game.GlobalManager.Transfer(entity)`.
3. `JumpIn` moves it into the destination via `jumpPair.JumpSystem.Transfer(entity)`.

**There is no `ManagerSubPulse.TransferEntity()` method — transfer is `GlobalManager.Transfer` / `StarSystem.Transfer`.** And this whole path is **currently unwired: `SetJump` has no live caller** (its only reference is a commented-out line in a test), so inter-system jumps via this processor do not run in-game today.

---

## Pathfinding

`PathfindingManager` builds a graph over the jump point network (nodes = star systems, edges = jump points) and runs A* to find shortest routes. Used by UI to calculate travel routes and by automated fleet orders (`MoveToNearestColonyAction`, etc.).

---

## Relevance to Ground Combat

Ground combat requires landing troops, which requires:
1. Transport ship in orbit (using `WarpAbilityDB` / `OrbitDB` to arrive).
2. Descent from orbit to surface — currently no "landing" action exists. This will be a new `EntityCommand` (e.g. `LandTroopsAction`), NOT a nav-sequence item — there is no `INavAction` type in the repo, and `NavSequenceDB` holds Newtonian maneuver nodes, not mission actions.
3. Ground unit entities, once landed, do **not** need `NewtonMoveDB` — they move at tactical scale on the planet, not the star system scale.

---

## Gotchas

1. **Missile guidance uses direct attack (fixed 2026-06-21).** `MissleProcessor.cs` now sets `directAttack = true`, so missiles use the Newtonian thrust-to-intercept path (`ThrustToTargetCmd`) — direct pursuit, not the old phasing maneuvers. (This gotcha previously claimed `directAttack = false`; that is stale — see root CLAUDE.md gotcha #3.)

2. **`NewtonSimpleProcessor` vs `NewtonianMovementProcessor`.** Both exist. Simple is for projectiles / beams that don't thrust; full is for ships and missiles. Don't attach `NewtonMoveDB` to a non-thrusting entity expecting the simple behavior — use `NewtonSimpleMoveDB`.

3. **`Temporal Anomaly Exception` when scheduling movement.** If a movement command is scheduled for a datetime already past in the manager's timeline, `ManagerSubPulse.AddEntityInterupt()` throws. Always use `entity.StarSysDateTime` as the base when constructing future datetimes for commands.

4. **`MoveToNearestAction.cs` family** — these ARE player orders: each is an `EntityCommand` subclass (`MoveToNearestAction : EntityCommand`, and the colony/anomaly/geo-survey variants), commanding a fleet to move to the nearest colony/anomaly/geo-survey target. (Earlier text called them "automation helpers, not player orders" — that's wrong; they're issuable orders.)

5. **A 0-speed warp intercept overflows `TimeSpan` → a background-thread `[FATAL]` (fixed 2026-07-04).** `WarpMath.GetInterceptPosition_m` computes `tt = distance / speed`; a `speed` of 0 (or negative/NaN) makes `tt = ∞`, and the loops feed that into `atDateTime + TimeSpan.FromSeconds(∞)` → `OverflowException`. Because the fleet-move order executes on the **background sim thread** (`SimulateTimeAsync`), that throw is *unobserved* → `[FATAL]` (and the synchronous copy shows as `[OrderError]`) — it can kill the clock. **Trigger (found in a committed `game_logs/` crash):** ordering a **FLEET** to a body when a member ship has a `WarpAbilityDB` with `MaxSpeed == 0` (a hull with a warp blob but no effective speed) — `MoveToSystemBodyOrder`'s only guard was `HasDataBlob<WarpAbilityDB>()`, so the 0-speed ship reached the intercept math. **Fix (two layers):** `GetInterceptPosition_m` now bails to a no-op `(moverAbsolutePos, atDateTime)` when `speed <= 0` / non-finite (and likewise for a non-finite orbital period), so **no** caller can overflow it; and `MoveToSystemBodyOrder` skips ships whose `MaxSpeed <= 0`. Gauge: `WarpFleetMoveTests.GetInterceptPosition_ZeroSpeed_DoesNotOverflow`. **Lesson: any `distance/speed → TimeSpan.FromSeconds` path must guard a non-positive speed — and an order that runs on the sim thread turns an unguarded throw into a clock-killing `[FATAL]`, not a caught error.**
