# Fleets Subsystem — Developer Reference

**What it does:** Groups ships into named fleets so the player gives orders to the fleet, not to individual ships. A fleet is just an entity with a `FleetDB` blob — ships are assigned to it by entity ID. The fleet can have sub-fleets (tree structure), a flagship, and standing conditional orders.

**Why it matters for ground combat:** Ground forces need transport ships. Transport ships are in fleets. The invasion order ("land troops") is issued by a fleet in orbit over a target colony. Ground forces that travel between systems ride inside fleet-managed transport ships. **Phase 4 wires directly into this subsystem — read this before building the landing order.**

---

## Files

| File | Role |
|------|------|
| `FleetDB.cs` | The DataBlob. Holds flagship ID, parent fleet ID (tree hierarchy via `TreeHierarchyDB`), `StandingOrders` list, and `InheritOrders` flag. |
| `FleetFactory.cs` | Creates fleet entities. `FleetFactory.Create(manager, factionID, name)` — creates the entity with `NameDB`, `FleetDB`, `OrderableDB`, and sets `entity.FactionOwnerID` directly (no `OwnedDB` blob). |
| `FleetOrder.cs` | All player-issued fleet commands — `Create`, `Disband`, `ChangeParent`, `AssignShip`, `UnassignShip`, `SetFlagShip`, `ToggleInheritOrders`. Each is an `EntityCommand` with `ActionLaneTypes.InstantOrder`. These are executed by the generic `OrderableProcessor` (the order-execution path), which calls each command's `Execute()` — that's where ship membership actually changes (`AssignShip`/`UnassignShip`). |
| `FleetOrderProcessor.cs` | An `IHotloopProcessor` (`RunFrequency` = 1 hr) keyed to `FleetDB`. It does NOT execute `FleetOrder` commands — it evaluates a fleet's `StandingOrders` (conditional orders): when a standing order's condition is true and the fleet has no pending actions, it clones that order's actions onto the fleet's `OrderableDB` action list. |
| `FleetTools.cs` | Read-only UI helpers so the MAP can draw a fleet as ONE icon (matching how the engine already treats it as one unit). `CollapsedFleetMemberShipIds(manager, factionId)` = the ship ids to hide (every ship in a 2+ ship fleet except its flagship/first representative); `FleetShipCountFor(...)` backs a "Fleet (N)" label. Pure queries, defensive (render-thread safe). Gauge: `FleetCollapseTests`. Client wiring: `Pulsar4X.Client/CLAUDE.md` → "Fleet-as-one-icon". |
| `FleetRoleComposer.cs` | **Phase B-2a (sub-fleet roles, Q7)** — sorts a fleet's ships into the four fighting jobs `FleetRole { Screen, Line, Artillery, Support }` by reading each ship's `Combat.ShipCombatValueDB`. `ClassifyRole(ship)` decides in priority order: utility hull (RoleWeight < 1.0) → Support; nimble (Evasion ≥ 0.5) → Screen; long-reach (`MaxWeaponRange` ≥ 400 km, i.e. railgun/missile class) → Artillery; else the gunline → Line; an unrated hull → Support. `PlanRoleSubFleets(fleet)` buckets `FleetCombat.Ships` into a Dict with all four roles keyed. **Pure READ — mutates no tree, issues no order — so a running game is byte-identical; nothing in the loop calls it yet.** A later slice (B-2b+) takes this bucketing and forms real sub-fleets, each with its own doctrine. Reads the new `[JsonIgnore] ShipCombatValueDB.MaxWeaponRange` (max `WeaponProfile.Range_m`, 0-range ignored). Gauge: `FleetRoleCompositionTests`. |
| `RefuelAction.cs` | `EntityCommand` subclass — fleet-level refueling action. **Stub:** `Execute()` is empty (does nothing yet). |
| `ResupplyAction.cs` | `EntityCommand` subclass — fleet-level resupply action. **Stub:** `Execute()` is empty (does nothing yet). |
| `ServeyAnomalyAction.cs` | `EntityCommand` subclass — survey action (anomaly investigation). **Stub:** `Execute()`/`IsValidCommand()`/`Clone()` all throw `NotImplementedException`. |

---

## Data Model

```
FleetDB extends TreeHierarchyDB
  FlagShipID         int        — entity ID of the flagship ship
  InheritOrders      bool       — if true, uses parent fleet's orders
  StandingOrders     SafeList<ConditionalOrder>
```

A fleet entity has these DataBlobs: `FleetDB`, `OrderableDB`, `NameDB`. (No `OwnedDB` — ownership is carried by `entity.FactionOwnerID`, set directly in `FleetFactory.Create`.)

Ships are assigned to fleets through `FleetOrder.AssignShip()`. The membership change happens inside `FleetOrder.Execute()` (the `AssignShip`/`UnassignShip` cases call `navyDB.AddChild`/`RemoveChild` on the fleet's `FleetDB`) — NOT in `FleetOrderProcessor`, which only handles standing orders. The tree hierarchy (`TreeHierarchyDB`) tracks parent/child, so membership is expressed as the fleet's children rather than a separate member list.

---

## How Orders Work

All fleet commands go through `FleetOrder` (an `EntityCommand`). The pattern:

```csharp
var order = FleetOrder.AssignShip(factionId, fleetEntity, shipEntity);
StaticRefLib.OrderHandler.HandleOrder(order);
```

`FleetOrder` commands are executed by the generic `OrderableProcessor` (the order-execution path) — each command's `Execute()` runs when the order is processed. Orders are instant (`ActionLaneTypes.InstantOrder`), so they fire immediately. `FleetOrderProcessor` is a separate thing: an `IHotloopProcessor` (fires every game-hour) that evaluates a fleet's *standing/conditional* orders, not the one-shot `FleetOrder` commands above.

---

## Sub-Fleets (Tree Hierarchy)

`FleetDB` extends `TreeHierarchyDB`, which means fleets nest. A transport group can be a sub-fleet of the invasion fleet. When the parent fleet moves, sub-fleets with `InheritOrders = true` follow. Sub-fleets can be detached into independent fleets via `ChangeParent`.

This is the mechanism ground combat needs: the assault echelon (transports + troops) starts as a sub-fleet of the invasion fleet, detaches to land, then the escort holds orbit.

---

## Fleet Actions (Refuel / Resupply / Survey)

`RefuelAction`, `ResupplyAction`, and `ServeyAnomalyAction` are `EntityCommand` subclasses (NOT `INavAction`), and all three are **stubs** — `RefuelAction`/`ResupplyAction` have empty `Execute()` bodies, `ServeyAnomalyAction` throws `NotImplementedException` in `Execute`/`IsValidCommand`/`Clone`. So the classes exist as placeholders but do nothing yet. The landing action for ground combat will be a **new command** — `LandTroopsAction` or similar — that triggers when the fleet is in orbit over a target colony and the player issues the invasion order.

See `Movement/CLAUDE.md` for the movement/action pattern.

---

## Pulsar Status

Fleet management is **fully functional**. Ships can be created, assigned to fleets, given movement orders, and the fleet tree hierarchy works.

**What does NOT exist yet (Phase 4 will add):**
- Any ground-force-related fleet action (there is no "load troops" or "land troops" action)
- Any cargo-loading order that specifically tracks ground units as cargo (though the cargo system exists — `Storage/`)
- Fleet composition checks for "orbital dominance" (no concept of controlling the space over a colony)

---

## Phase 4 Hook Points

| Ground combat need | Hook here | Notes |
|-------------------|-----------|-------|
| Load ground units into transport | `FleetOrder` + cargo system | Ground unit entities go into the transport ship's `CargoStorageDB` |
| Move to target system | Fleet movement (already works) | No new code — fleet moves like any other fleet |
| Establish orbital dominance | New check in invasion order | Query enemy ships in same orbit zone over target colony |
| Issue landing order | New `LandTroopsAction : EntityCommand` (same shape as `RefuelAction`) | Fires when fleet in orbit, checks dominance, creates `GroundCombatDB` on colony |
| Detach escort sub-fleet | `FleetOrder.ChangeParent()` (already works) | Transports detach, escorts remain in orbit |

---

## Proposed Future Work — Fleet Combat Doctrine

A design (not built) splits a fleet into named **components** (Front Line / Flank / Rear Guard / Artillery) — most cheaply as **sub-fleets of this tree hierarchy** — each running a switchable **combat doctrine** (Offensive / Defensive / Utilitarian) with situational trade-offs, a game-time switch cooldown, and optional commander "operational discretion." Full design (integrated with combat System 4 — Fleet Doctrine — and System 6 — Commander Discretion): **`docs/combat/COMBAT-DESIGN.md`** → "System 4 (detailed design) — Fleet Components & Switchable Doctrine." Read it before adding any component/posture concept to `FleetDB`.
