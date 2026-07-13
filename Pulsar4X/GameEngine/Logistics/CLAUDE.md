# Logistics Subsystem — Developer Reference

**What it does:** Moves cargo between colonies and ships autonomously. A colony or station can advertise what it needs (imports) and what it has excess of (exports). Ships with a `LogiShipperDB` participate in a bidding system, taking the most profitable run available. Think of it like an automated freight market — the ship takes the job with the best profit margin.

**Why it matters for ground combat:** Supply lines for ground forces (GSP — Ground Support Points) are distinct from this logistics system. Ground supply is a per-unit pool depleted in combat, not a cargo-movement system. However, transporting ground units between colonies IS a logistics operation — troops in cargo bays move exactly like any other cargo via transport ships.

---

## Files

| File | Role |
|------|------|
| `LogiBaseDB.cs` | DataBlob on a colony/station acting as a trade hub. Holds `DesiredLevels` (what it wants to maintain), `ListedItems` (what it has available), `ItemsInTransit`, `ItemsWaitingPickup`, `TradeShipBids`. |
| `LogiShipperDB.cs` | DataBlob on a ship that participates in the logistics market. Tracks current state — the `States` enum is `Bidding, MoveToSupply, Loading, MoveToDestination, Unloading, ResuplySelf, Waiting`. **Only `Waiting` and `Bidding` are actually driven today** (see note below); the movement/load/unload states are declared but nothing advances the ship into them yet. |
| `LogiBaseAtb.cs` | Component design attribute (`IComponentDesignAttribute`). Installing a component with this attribute is what creates/grows the `LogiBaseDB` on a colony/station and sets its `Capacity` — the cradle-to-grave entry point (design → build → install → the trade hub exists; uninstall the last one and the `LogiBaseDB` is removed). |
| `LogisticsProcessor.cs` | Contains two `IHotloopProcessor` classes: `LogiBaseProcessor` (`RunFrequency` = **1 hour** — runs bidding for bases) and `LogiShipProcessor` (`RunFrequency` = **1 hour** — ships find and bid on jobs). **Also holds the `LogisticsCycle` static class** (bottom of the same file, no separate `LogisticsCycle.cs`): the actual bidding logic `UpdateListings()`, `LogiBaseBidding()`, and `LogiShipBidding()`, the faction-access gate `LogisticsAccessAllowed()`, and the nested `CargoTask` class (source, destination, item, profit, travel time, fuel cost). |
| `LogisticsSimple.cs` | Helper calculating travel time and Δv for a ship to reach a source or destination using Newtonian/Warp physics. Used to estimate job cost before bidding. |
| `LogisticsNewtonion.cs` | Newtonian-specific logistics calculations (Hohmann-style transfer math). |
| `SetLogisticsOrder.cs` | Player-issued order to configure a ship's logistics participation. |
| `ShipLogisticsOrders.cs` | Order types for logistics state transitions (start job, abort job, etc.). |

---

## How Logistics Works

> **Build-state caveat (verified 2026-07-13):** the **base/bidding half is wired** — bases recompute listings and pick bids, ships find bases, estimate profit, and place bids (`UpdateListings` / `LogiBaseBidding` / `LogiShipBidding` all run on the 1-hour hotloop). But the **ship state machine past `Bidding` is NOT driven.** `LogiShipBidding` only flips `Waiting → Bidding`; `LogiBaseBidding` accepts a bid and issues the cargo-transfer + movement commands directly, but nothing ever sets the ship's `CurrentState` to `MoveToSupply`/`Loading`/`MoveToDestination`/`Unloading`/`ResuplySelf` — those enum states are declared, referenced only by `ShipLogisticsOrders.UpdateDetailString` for display, and otherwise unused. And `ShipLogisticsOrders.Execute()` is an **empty stub** (`ShipLogisticsOrders.cs` ~line 34). So the order-driven per-ship lifecycle is scaffolding; the working path is the base-side bid/accept loop calling `CargoTransferOrder.CreateCommands` + `Manuvers` inline. Runtime behaviour is unverified here (CI can't run the client).

**Base side (every 1 hour — `LogiBaseBidding`):**
- Colony checks `DesiredLevels` vs current stock.
- If short on an item, it lists a demand with a price signal.
- If over-stocked, it lists surplus for pickup.
- Selects the best bid from `TradeShipBids`.

**Ship side (every 1 hour — `LogiShipBidding`):**
- Ship looks for all `LogiBaseDB` entries in the system.
- For each pair of (source, destination, item), estimates profit: sell price minus fuel cost minus transit time cost.
- Builds a `CargoTask` list sorted by profit.
- Places bid on the most profitable run.

**Travel:** Once a bid is accepted, the ship gets a movement order to the source colony, loads cargo, then moves to the destination and unloads. The existing `Movement/` system handles the actual flying.

---

## Key Data Structures

```csharp
LogiBaseDB.DesiredLevels  — Dictionary<ICargoable, (int min, int max)>
    // "I want between 1000 and 5000 units of iron"

LogiBaseDB.ListedItems    — Dictionary<ICargoable, (int count, int demandSupplyWeight)>
    // "I have 3000 units of iron available, weight 0.8 means slight surplus"

LogisticsCycle.CargoTask
    Profit         double   — estimated profit of this run
    Source         Entity   — where to pick up
    Destination    Entity   — where to deliver
    Item           ICargoable
    NumberOfItems  long
    timeInSeconds  double   — estimated travel time
    fuelUseDV      double   — Δv cost
```

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Automated cargo ships | `LogiShipperDB` + `LogisticsProcessor` bidding | ✅ functional |
| Supply/demand levels per colony | `LogiBaseDB.DesiredLevels` | ✅ functional |
| Profit-based route selection | `LogisticsCycle.CargoTask.Profit` ranking | ✅ functional |
| Fuel-aware routing | `LogisticsSimple.TravelTimeToSource` uses ship Warp/Newton physics | ✅ functional |
| Maintenance Supply Points (MSP) | Not found — MSP is Aurora's per-ship maintenance resource | ❌ missing |
| Fuel consumption tracking (Sorium) | Fuel exists in cargo system; MSP-style maintenance clock | ⚠️ partially — check Industry/Storage |
| Supply ships (dedicated maintenance vessel) | Not found as a specific type | ❌ unknown |

**Verdict: core automated cargo logistics is functional.** The Aurora-style Maintenance Supply Point system (ships have a clock that consumes MSP to stay operational) is the main known gap. MSP isn't on the ground-combat critical path, but matters for the "logistics depth" goal.

**Faction access (diplomacy-gated as of 2026-07-01):** `LogiShipBidding()` gates which bases a ship may service through `LogisticsCycle.LogisticsAccessAllowed(baseFactionId, shipFactionId, game)`. A ship serves a base if it's the **same faction** (unchanged) OR the **base owner** has granted the ship's faction `RelationshipState.LogisticsAccess` in its `DiplomacyDB` (the "run your freighters through my supply network" treaty flag — granted by the base owner, their network their call). The flag defaults **false** and nothing sets it until a treaty does, so with no deal in place this is **identical to the old same-faction-only guard** (every existing logistics fixture unchanged). Gauge: `DiplomacyLogisticsAccessTests` (same=open, foreign gated on a stored grant, directional). Full design: `docs/society/DIPLOMACY-DESIGN.md`. **Next commerce step:** wire the `TradeAgreement` flag into the inter-faction *market/price* path (this slice covers logistics *access*; a trade-agreement gate on cross-faction buying/selling is the follow-up).

---

## Phase 4 Relevance

Ground unit transport is cargo movement. A transport ship carries ground units as cargo items (same `ICargoable` interface). Loading at source colony and unloading at destination is already handled by the logistics or direct order system — no new movement code is needed. The new work is:
1. Making a `GroundUnitDesign` implement `ICargoable` so it can be loaded.
2. An `InvasionOrder` that triggers unloading at a hostile colony (instead of a friendly one).
