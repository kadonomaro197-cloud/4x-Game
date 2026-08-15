# B-Orders → Client Routing — Source-Verified Execute-Ready Spec

> **Provenance:** the `b-orders-recon` Workflow (2026-08-15, 7 agents, all claims re-verified against HEAD). Drives the remaining B-orders commits. Companion to `docs/IMPLEMENTATION-CAMPAIGN-LOG.md` (slice board + NEXT ACTION). Status of each item is tracked on the slice board, not here.

All load-bearing claims verified against HEAD. The recon is accurate: `SetLogisticsOrder` structure and its throwing `Clone()`, `ShipRoleTools.CanIssue`/`AbilitiesOf`/`OrderAbilityTable` (:173-220), `CargoTransferOrder.CreateRefuelFleetCommand` (:136), no `DockOrder` class exists, and the `ToggleInheritOrders` dead wire at FleetWindow.cs:436-447 with its stale identifiers. Here is the consolidated execute-ready spec.

---

# B-Orders → Client Routing: Execute-Ready Spec

**Scope:** route the DATA-graded orders into the Pulsar4X client. The organizing constraint: **every slice that touches `FleetWindow.cs` serializes behind CI** (~33 min per gate, SDK can't build locally), so the plan front-loads everything that is *file-disjoint from `FleetWindow.cs`* — engine order classes and non-FleetWindow client files — which can land in parallel while the FleetWindow stack drains one commit at a time.

**Two facts re-verified in source, both load-bearing:**
- The availability gate the categorized menu needs is **already engine-side and CI-testable**: `ShipRoleTools.AbilitiesOf` / `OrderAbilityTable` / `CanIssue` (`GameEngine/Ships/ShipRoleTools.cs:173-220`), read by both the window and the AI (one-verb-both-seats already satisfied).
- The order-submit idiom is uniform: `CreateCommand(...)` builds the `EntityCommand`, then `_uiState.Game.OrderHandler.HandleOrder(cmd)` submits it. `CreateCommand` never auto-submits (the missile path discards the return at `MissleProcessor.cs:107`).

---

## Section 1 — File-disjoint engine setters to build NOW

These are buildable immediately because they live in engine files disjoint from the CI-blocked `FleetWindow.cs` stack. Each is gauge-before-UI: land the engine class + a CI test, gate green, then the client slice in Section 2 consumes it. **Ranked by value/effort.**

### RANK 1 — `SetLogisticsOrder.SetDesiredLevels` (stockpile min/max target write path)
- **Why #1:** small effort, immediate value. The **reader is already live** — `LogisticsProcessor.UpdateListings` (`LogisticsProcessor.cs:112-145`) already prices shortfall/surplus 0.25×–4× off `DesiredLevels`; only the *write* path is missing (no UI, no order). Same slice fixes a latent save bug (below).
- **Engine file:** `GameEngine/Logistics/SetLogisticsOrder.cs` (verified structure: `OrderTypes` enum :11, `CreateCommand_SetBaseItems` :70, `Execute` switch :111 — mirror the `SetBaseItems` case exactly).
- **Proposed setter (mirror `CreateCommand_SetBaseItems` verbatim):**
  ```csharp
  // add to OrderTypes enum (SetLogisticsOrder.cs:11): SetDesiredLevels
  // add field:  private Dictionary<ICargoable,(int minVal,int maxVal)> _desiredChanges;
  public static void CreateCommand_SetDesiredLevels(
      Entity entity, Dictionary<ICargoable,(int minVal,int maxVal)> changes)
  // Execute case: var db = EntityCommanding.GetDataBlob<LogiBaseDB>();
  //   foreach kv in _desiredChanges: (min==0 && max==0) ? db.DesiredLevels.Remove(kv.Key)
  //                                                     : db.DesiredLevels[kv.Key] = kv.Value;
  ```
- **Why the order (not direct field write) is mandatory:** `DesiredLevels` is iterated by `LogiBaseProcessor` on the **sim thread** (`UpdateListings` foreach, `LogisticsProcessor.cs:119`). A UI-thread add/remove mid-iteration throws "collection was modified" — *unlike* the harmless torn double-write of `TaxRate`. Deferring the write into `Execute` (sim thread) is what makes it race-free. AI parity also requires the order to exist (`AI-DECISION-ENGINE-DESIGN.md` assumes the AI sets `DesiredLevels` via this order).
- **Same-slice bug fix:** `LogiBaseDB.Clone` copy-ctor (`LogiBaseDB.cs:35-44`) **omits `DesiredLevels` and `ItemsInTransit`** — an automated route silently forgets its min/max after a Clone/save-load. Add both fields to the copy-ctor here.
- **CI-test sketch:** issue `CreateCommand_SetDesiredLevels` on a colony → assert `DesiredLevels` set → run `LogiBaseProcessor` → assert `ListedItems` advertises the shortfall/surplus. (+ a round-trip Clone test asserting `DesiredLevels` survives.)
- **Path proven:** `LogiBaseDB` carries `OrderableDB` and `CreateCommand_SetBaseItems` already runs live on it — the new order rides the identical `UseActionLanes=true` / `InstantOrder` path. Host-agnostic: valid on a **colony OR a station** (gate on `LogiBaseDB` + `OrderableDB`, never colony-only blobs).

### RANK 2 — `CargoTransferOrder.CreateRearmFleetCommand` (fleet ordnance-rearm helper) — OPTIONAL
- **Status:** `needsEngineSetter=false`. The generic `CargoTransferOrder.CreateCommands` (`CargoTransferOrder.cs:72`, public) **already works today** with zero engine change — ordnance is `ICargoable` (`OrdnanceDesign : ICargoable, CargoTypeID="ordnance-storage"`). This helper is the clean *fleet analog of Refuel* and is **recommended** for parity + to keep the "what ordnance is missing" math in the CI-tested engine rather than the render thread.
- **Engine file:** `GameEngine/Storage/CargoTransferOrder.cs` — mirror `CreateRefuelFleetCommand` (verified at :136).
- **Proposed setter:**
  ```csharp
  public static void CreateRearmFleetCommand(Entity cargoFromEntity, Entity fleet)
  // for each ship with an ordnance-storage hold: enumerate its OrdnanceDesigns +
  // magazine free space (CargoMath.GetFreeUnitSpace), issue CreateCommands(..., Conditionals.WaitTillFull)
  ```
- **Gotchas baked in:** `Conditionals.WaitTillEmpty` (:222) and `TakeAvailible` (:225) **throw NotImplementedException** — use only `WaitTillFull` or `TakeAvailibleAtOrder`. There is **no** "how much ordnance is missing" helper (the refuel twin's `GetFuelInfo` is fuel-only), so the helper must enumerate holds itself.
- **CI-test sketch:** stand up a fleet with a missile ship holding a partial ordnance load + a base holding matching ordnance → call `CreateRearmFleetCommand` → run the order → assert the ship's ordnance-storage hold filled toward capacity.

### RANK 3 — `DockOrder.cs` (carrier dock/undock EntityCommand) — content-gated
- **Why it's a BUILD not a wire:** grep confirms **no `DockOrder` class exists** anywhere. The engine *capability* (`DockTools.TryDock`/`Undock`/`UndockAll`, all `public static`, CI-gauged by `DockBayTests.cs`) is done; the **queued order wrapper is the next slice**, and `DockTools.cs:15-18` explicitly says so ("it will be ONE verb both seats issue... because it is one method here").
- **Why the order, not a client DirectCall:** `DockTools` is callable from the client today, but a client-only DirectCall would lock the AI out — violating one-verb-both-seats. The order is the design-correct answer.
- **Engine file (new):** `GameEngine/Docking/DockOrder.cs`, mirroring `FleetOrder.cs`.
- **Proposed setters:**
  ```csharp
  public static DockOrder Dock(int requestingFaction, Entity carrier, Entity ship)
  public static DockOrder Undock(int requestingFaction, Entity carrier, Entity ship)
  // UseActionLanes=true, ActionLanes=InstantOrder; Execute() calls DockTools.TryDock/Undock (already public);
  // IsValidCommand() checks faction ownership + resolves entities via TryGetEntityById.
  ```
- **Commanding entity carries `OrderableDB`:** ship for Dock, carrier for Undock — both are ships (`ShipFactory.cs:113`). If a station is ever a carrier, verify it carries `OrderableDB` first.
- **CI-test sketch:** two ships same manager, carrier with a `DockBayAtb` admitting the other's hull mass → issue `DockOrder.Dock` → run OrderableProcessor → assert `DockTools.DockedShips(carrier)` contains the ship and its `PositionDB` re-parented to the carrier; then `Undock` → assert removed + re-parented to SOI.
- **⚠ Content gate (surprising — see Section 4):** **no base-mod ship mounts a `DockBayAtb` yet**, so `DockTools.Capacity==0` for every hull and the order has no valid carrier. The Section-2 UI gate (`Capacity>0`) simply hides it, so the game stays byte-identical — but the feature isn't *exercisable* until a carrier hull installs a bay via the six-point/cradle-to-grave chain. Build the order + test now; the feature lights up when content lands.

### Note — file-disjoint CLIENT slice (not an engine setter): Edit Production Job
`buildableNowFileDisjoint=true` here means the **client** work is disjoint from `FleetWindow.cs` — it lives in `IndustryDisplay.cs` / `ColonyManagementWindow`, so it can proceed in parallel with the FleetWindow stack. The engine order + execution are **fully built** (`IndustryOrder2.CreateEditJobOrder`, `IndustryOrder.cs:97` → `IndustryTools.EditExsistingJob`, `:59`). Detail in Section 2 (slice EP). **Optional engine gap-fix** if auto-install editing is wanted: `EditExsistingJob` (`IndustryTools.cs:59-75`) **ignores the `autoInstall` arg** (the `ConstructJob.InstallOn` block is commented out :69-73) — a UI "edit auto-install" would be a silent no-op until that's fixed. Count + repeat editing works today.

### Dropped from Section 1
- **Set Target Priority** — recon hint said "DirectCall / build the selector," but it is **ALREADY BUILT AND WIRED** (`FleetWindow.cs:665`, selector at `:683-706`; engine `FleetDoctrine.SetTargeting` CI-gauged). No work remains (Section 4).

---

## Section 2 — Client slices, ordered

All FleetWindow.cs slices **serialize** (one commit, gate CI green, next). File-disjoint slices are flagged `[PARALLEL]` and can run alongside the FleetWindow queue. Ordered smallest/most-cohesive first.

### C1 — Toggle Inherit Orders `[FleetWindow.cs, trivial, no picker, no step-1 dep]`
- **Order:** `FleetOrder.ToggleInheritOrders(factionID, fleet)` (`FleetOrder.cs:121`) — a **complete, wired EntityCommand**; engine half done and CI-testable today.
- **Surface + widget:** the UI already exists as **dead/commented code at `FleetWindow.cs:436-447`** — a checkbox in the Standing Orders tab, gated to appear only for a **sub-fleet** (parent ≠ faction root). State already synced at `:163`. Un-comment and adapt.
- **Fix the stale identifiers (verified in source):** `selectedFleet` → `SelectedFleet`; `.Parent.Guid != factionID` → use `.Parent` + the **int `Id`** (Entity is keyed by int now, not Guid — verify `FleetDB.Parent`'s id property); `StaticRefLib.OrderHandler.HandleOrder` → `_uiState.Game.OrderHandler.HandleOrder(order)`. The current code below the block already uses `selectedFleetDB?.StandingOrders` — align to that.
- **Gotchas:** keep the parent≠faction guard (inherit-orders is meaningless for a top-level fleet); `InheritOrders` defaults **true**, so first toggle turns it OFF. Fixed-literal label → no printf `%` trap.

### C2 — Resupply / Rearm Ground Unit `[FleetWindow.cs, trivial, no picker, no step-1 dep]`
- **Call:** `GroundForces.ResupplyUnit(body, unit)` (`GroundForcesDB.cs:670`, `public static`, returns kg refilled). DirectCall — already has a live auto-caller (`GroundForcesProcessor` step 0d) + gauge (`GroundAmmoTests.cs:80,87`). Nothing to build engine-side.
- **Surface + widget:** a "Rearm" **button** per unit in `DrawGroundUnitDetail` (`FleetWindow.cs:2154`), showing ammo fraction (`GroundAmmo.Fraction(unit)`). Optionally a "Rearm all" in `DrawBattalionOrders` (`:1387`) over `GroundFormationTools.MembersOf`. No picker — unit + body already in hand (`RosterEntry.Body` / `GUnit`).
- **Gotchas:** returns **0 (silent no-op)** when the unit is **not on friendly-held ground** (region owner ≠ unit faction) or has no magazine (`MaxAmmo_kg<=0`) — show the fraction + a "must be on your own ground" note, and hide/disable for `MaxAmmo_kg==0`. Unit names via `ImGui.TextUnformatted` (printf `%` trap). **Exclude** the parked `Fleets/ResupplyAction` stub (de-fanged no-op).

### C3 — Set Formation Order (Replace Queue) `[FleetWindow.cs, trivial, no picker, no step-1 dep]`
- **Call:** `GroundForces.SetFormationOrder(formation, order)` (`GroundForcesDB.cs:1060`, `public static`). Sibling `ClearFormationOrders` already called at `FleetWindow.cs:1610` — API surface proven reachable.
- **Surface + widget:** in `DrawBattalionOrderQueue` (`FleetWindow.cs:1602`), beside "Clear plan" (`:1610`) — either a "Replace-mode" **checkbox** that flips the existing `+ ...` buttons from `QueueFormationOrder` to `SetFormationOrder`, or dedicated "Set (replace) → …" buttons. Reuses the exact `GroundOrder` factories the queue buttons already feed (MoveRegion/MoveHex/Hold/Roe/Stance). No picker of its own.
- **Gotchas:** `SetFormationOrder` **replaces the whole queue** with one order (the "do THIS now, forget the rest" verb) vs `QueueFormationOrder`'s append. A **null order clears** the queue — guard the UI so replace always feeds a real order. Empties the *queue* only; a unit already in transit finishes its current march.

### C4 — Queue Waypoint: Move to Planetary Hex `[PlanetViewWindow preferred = PARALLEL; or FleetWindow, small, GLOBAL hex picker, no step-1 dep]`
- **Call:** `GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(q,r))` (`GroundForcesDB.cs:1052` + factory `:342`). One-verb-both-seats **compliant** — it rides the O1 formation order queue (the same queue the AI uses); the processor routes queued `MoveToHex` to the **GLOBAL cylinder** (`GroundForcesProcessor.cs:918-922`). Already-proven idiom: `FleetWindow.cs:1623` does `QueueFormationOrder(f, GroundOrder.MoveRegion(n))`.
- **Surface + PICKER:** needs a **GLOBAL hex picker (cylinder Q,R)**. **Cleanest home is `PlanetViewWindow.DrawOrderQueue`** — the globe already remembers the last-clicked global hex in `_selGQ/_selGR`, so a "+ Move to this hex" button reuses it with **no new picker** (and this file is disjoint from FleetWindow → `[PARALLEL]`). In FleetWindow (no globe) it needs numeric Q/R `InputInt` fields or an "open planet view and click" flow.
- **Gotchas:** ⚠ **GLOBAL coords** — do **not** share this picker with C-tree/region-local moves (Section 4). It's a "then" waypoint — fires only when the leader idles (O1 pops one order at a time). `Describe()` renders "→ hex (Q,R)" (safe fixed format).

### C5 — Set Stockpile Min/Max Target `[LogisticsWindow = PARALLEL, or FleetWindow; small; ITEM picker; BLOCKED on RANK-1 engine setter]`
- **Blocked on:** Section 1 RANK 1 (`SetLogisticsOrder.SetDesiredLevels`). Land that + its CI test first.
- **Surface + PICKER:** a **cargo-good picker combo** (reuse `ColonyLogisticsDisplay._allResources/_allResourceNames`) + two `InputInt` (min/max) + a Set button + a Remove for an existing entry. **Item picker, not hex/entity.** Homes: `FleetWindow.cs DrawHoldingDetail` (`:2241`, beside the Tax slider `:2263`) **or** `LogisticsWindow` / `ColonyLogisticsDisplay` Imports/Exports panels. **Prefer LogisticsWindow** to keep it off the serialized FleetWindow stack (`[PARALLEL]`).
- **Engine call:** `SetLogisticsOrder.CreateCommand_SetDesiredLevels(host, changes)` → `HandleOrder`.
- **Gotchas:** gate on `LogiBaseDB` + `OrderableDB` (host may be **station**, no colony economy). Cargo names via `ImGui.TextUnformatted`; the `InputInt` fixed formats are fine.

### C6 — Intercept / Ram Target `[FleetWindow.cs or system-map context menu; medium; ENTITY picker; no step-1 dep]`
- **Order:** `ThrustToTargetCmd.CreateCommand(factionId, orderEntity, actionDateTime, targetEntity)` (`NewtonThrustCommand.cs:284`; class at `:252`) — a **fully-built EntityCommand**, no engine change. Commands a **single ship** (Newtonian thrust is per-ship). Submit via `HandleOrder` (CreateCommand does not auto-submit).
- **Surface + PICKER:** needs a **target-ENTITY picker** (not a body/hex picker). Two homes: **(a)** a new `IssueOrderType` in the Fleets-tab Issue-Orders sublist (mirror `MoveToSystemBodyOrder`) — but it needs *both* a ship-selector and an enemy-entity picker, and **FleetWindow has no enemy-entity picker today** (fog blips aren't clickable); **(b)** a **system-map right-click context action** on the clicked enemy ("Intercept/Ram this target") commanding the selected ship — the more natural home. Build the picker as an entity combo scoped to in-system detected entities (or the map click on b).
- **Gotchas:** **GATE the UI on the ship having `NewtonThrustAbilityDB`** — `Execute()` calls `GetDataBlob<NewtonThrustAbilityDB>()` with **no TryGet** (`:306`); a driveless ship **throws** → caught as `[OrderError]`, order silently dropped. It is a **RAM/kinetic kill**, not match-orbit-and-stop — burns half its Δv to close, then collides (designed for missiles; on a crewed ship it flies it into the target — see Section 4 design question). `Clone()` throws `NotImplementedException` (`:464`) but `HandleOrder` doesn't Clone live orders (fine); save/load of an in-flight order would throw (acceptable — combat transient). Works mid-battle (a ship is not a fleet, so the engagement lock doesn't apply). Do **not** wire the incomplete `Thrust90ToTargetCmd` sibling (`:476`).

### C7 — Reload / Rearm Ordnance at Base `[FleetWindow.cs; medium; base ENTITY picker; cleaner WITH RANK-2 helper but not blocked]`
- **Engine call:** reuses `CargoTransferOrder.CreateCommands` (`:72`, works today, zero engine change) — or the cleaner `CargoTransferOrder.CreateRearmFleetCommand` from Section 1 RANK 2 if landed.
- **Surface + PICKER:** a new **`IssueOrderType.Rearm`** cloned from the **`RefuelAt` case** (`FleetWindow.cs:1805-1828`): a friendly/neutral colony+station **entity picker** (`GetFilteredEntities` with `CargoStorageDB`) → warp-to + transfer. **Gate the entry** on the fleet holding a missile ship (ordnance-storage hold / launcher), the way Troops gates on `HasAnyTroopBay` (`:415`). Base/colony entity picker like RefuelAt — no hex picker.
- **Gotchas:** **two-stage** — this tops up the ship's ordnance-storage *hold*; the launcher's internal magazine then reloads from that hold via `GenericFiringWeaponsProcessor` — label it "top up ordnance," not "set launcher mag." No ready "missing ordnance" helper — the client (or the RANK-2 helper) must enumerate `OrdnanceDesigns` + hold free space (`CargoMath.GetFreeUnitSpace`). Use only `WaitTillFull` / `TakeAvailibleAtOrder` (the other two Conditionals throw). **Do NOT** route through `OrdnanceDesignWindow` (dead/unreachable) or the parked `RefuelAction`/`ResupplyAction` stubs. Base names via `ImGui.TextUnformatted`.

### C8 — Dock / Undock Vessel `[FleetWindow.cs; medium; carrier/docked-ship ENTITY picker; BLOCKED on RANK-3 DockOrder; content-gated]`
- **Blocked on:** Section 1 RANK 3 (`DockOrder.cs` + CI test).
- **Surface + PICKER:** a new `IssueOrderType` in the Fleets-tab Issue-Orders sublist, gated like `IssueOrderType.Troops` on the fleet containing a carrier with `DockTools.Capacity>0`. **Entity picker:** for Dock, a combo over in-system ships whose `DockTools.Capacity>0` and whose `LargestBerth` admits the ship's hull mass, showing `DockTools.CanDock(...,out reason)` to grey the button **with its reason** (Visibility Gate); for Undock, list `DockTools.DockedShips(carrier)` with a per-ship Undock button. Alternatively an `EntityContextMenu` action mirroring StationWindow's "Deploy Station Here."
- **Gotchas:** `CanDock` has **two gates** returning a human-readable `reason` (`DockTools.cs:107-142`): GATE1 the door (per-bay `MaxHullMass` — small bays never combine), GATE2 free budget — surface `reason`, not a bare disabled button. Carrier + ship must be **same manager/system** — scope the picker to in-system. Consequence to communicate: `TryDock` re-parents the ship (`PositionDB.SetParent(carrier)`) — it travels with the carrier and stops being independently selectable; Undock hands it to the carrier's SOI parent; `UndockAll` is the grave rung. Read the registry only via `DockTools.DockedShips()` (`DockedShipIds` is internal-set). Names via `TextUnformatted`; resolve via TryGet (a foreign carrier could appear). See content gate in Section 1 RANK 3 — until a hull mounts a bay, the gate hides the entry and the game is byte-identical.

### EP — Edit Production Job `[IndustryDisplay.cs = PARALLEL; small; no picker; engine fully built]`
- **Engine call:** `IndustryOrder2.CreateEditJobOrder(_factionID, entity, productionLineID, jobID, newCount, newRepeat)` → `state.Game.OrderHandler.HandleOrder` — fully built (`IndustryOrder.cs:97`); already imported/used by the client.
- **Surface + widget:** the **live** colony surface is `ColonyManagementWindow` → Production tab → `IndustryDisplay.Display()`; today its per-job `ActionButtons` row (`IndustryDisplay.cs:552-605`) draws only ▲/▼/✕. Add an **edit control** here — an "e" SmallButton or inline `InputInt` quantity + a repeat checkbox — reading the job's `NumberOrdered`/`Auto` and issuing `CreateEditJobOrder` via the existing `CreateChangePriorityOrder`/`CreateCancelJobOrder` idiom. No target picker (self-interact). The same `IndustryDisplay` is reused by `StationWindow.cs:68`, so it lights up on deployed stations too. **File-disjoint from the FleetWindow stack → runs in parallel.**
- **Gotchas:** wire into the LIVE `IndustryDisplay`, **not** the legacy Economy panel (`IndustryPanel.cs:254`, has only a partial repeat-toggle). Resolve the job's design via `_factionInfoDB.IndustryDesigns.TryGetValue` (never a hard index — a GameMaster-viewed colony references designs the viewed faction lacks). **Engine GAP:** the `autoInstall` arg is ignored (`EditExsistingJob`, `IndustryTools.cs:69-73` commented out) — offer only count + repeat editing unless you fix the engine helper first. Class is `IndustryOrder2` but the file is `IndustryOrder.cs`.

### C9 — Categorized-menu first cut `[FleetWindow.cs; see Section 3]`
The framework's smallest first cut (reorganize the Issue-Orders left panel into CollapsingHeaders). Detailed in Section 3. **Sequencing:** land it after the trivials (C1-C3) so it proves the pattern on a stable base; then the picker-bearing additions (C6, C7, C8) slot **into** the category tree instead of a flat list.

### Serialized-order recommendation (FleetWindow lane)
`C1 → C2 → C3 → C9 (menu first cut) → C6 → C7 → C8` on the FleetWindow lane; **in parallel:** EP (IndustryDisplay), C4 (PlanetViewWindow), C5 (LogisticsWindow), and the three Section-1 engine setters (each own file + CI test). C5 and C8 wait on their RANK-1 / RANK-3 engine setters landing green.

---

## Section 3 — The categorized-menu framework

**Headline:** an **organization layer, not a reimplementation**. The 9 category headers route into panels that already exist (most already split into per-order methods), gated by force-kind + capability. **No engine change for the first cut.**

### Data
```csharp
private enum OrderCategory { Movement, Combat, Survey, LogisticsCargo, FleetOps,
                            FormationOps, ConstructionIndustry, Special, StandingConditional }

private static readonly (OrderCategory cat, string label)[] _orderCategories = {
    (OrderCategory.Movement,"Movement"), (OrderCategory.Combat,"Combat"),
    (OrderCategory.Survey,"Survey"), (OrderCategory.LogisticsCargo,"Logistics & Cargo"),
    (OrderCategory.FleetOps,"Fleet Ops"), (OrderCategory.FormationOps,"Formation Ops"),
    (OrderCategory.ConstructionIndustry,"Construction & Industry"),
    (OrderCategory.Special,"Special"), (OrderCategory.StandingConditional,"Standing & Conditional"),
};
```
(`OrderCategory` is a free name — grep clean. Categories fixed by `docs/combat/FORCES-WINDOW-DESIGN.md` §10.)

### Dispatcher + switch
```csharp
private void DrawCategorizedOrders(ForceRef sel) {
    foreach (var (cat,label) in _orderCategories) {
        int n = CountApplicable(cat, sel);        // orders this kind can even see
        if (n == 0) continue;                      // battalion never shows "Fleet Ops"
        if (ImGui.CollapsingHeader($"{label} ({n})###ordcat{(int)cat}"))
            DrawCategory(cat, sel);                // routes into EXISTING methods
    }
}
```
`DrawCategory` is a `switch` on `cat` that calls existing methods gated by `sel.Kind`. Nothing inside is new drawing code:
- **Combat / Fleet:** `DisplayDoctrineSelector` → `DisplayEmconSelector` → `DisplayEngagementPostureSelector` → `DisplayTargetPrioritySelector` → `DisplayEngageButton` (already discrete, `:659-667`).
- **Combat / Ship:** `BeginDisabled(!ship.CanIssue("FireControl"))` + "Open Fire Control" button.
- **Combat / Battalion:** `DrawBattalionStance` + `DrawBattalionRoe` + `DrawBattalionInfraOrders`.
- **Movement / Battalion:** march buttons + `DrawBattalionOrderQueue`.

### Category → surface map (reuse = code exists · jump = open owning window · grey = DATA/BUILD row shown disabled)

| Category | Fleet | Ship | Battalion | Holding |
|---|---|---|---|---|
| Movement | Move-to-body + Jump lists (`:1751/:1818`) | jump→ Warp/Newton order window | march + `DrawBattalionOrderQueue` (`:1602`) | — |
| Combat | Doctrine/Emcon/EngagementPosture/TargetPriority/Engage (`:659-667`) | jump→ FireControlWindow, gate `CanIssue("FireControl")` | `DrawBattalionStance`+`Roe`+`InfraOrders` | — |
| Survey | Geo+JP survey (`:1771/:1793`), gate `CanIssue("GeoSurvey"/"GravSurvey")` | (civ) same | — | — |
| Logistics & Cargo | RefuelAt (`:1839`) + Embark/Land (`:2489`), gate `HasAnyTroopBay` | jump→ Cargo/Logistics window | — | imports/exports → jump LogisticsWindow |
| Fleet Ops | Create/Disband/Assign/Set-flagship (`FleetOrder.*`) | Assign-to-fleet | — | — |
| Formation Ops | reparent (drag-drop) | — | `DrawBattalionFormationOps` (`:1505`) + jump PlanetViewWindow | — |
| Construction & Industry | — | Deploy/Construct station (context menu) | — | jump→ ColonyManagementWindow / PlanetViewWindow |
| Special | Rename | Rename | Rename inline (`:1443`) | `DrawHoldingAdminPosts` (`:2331`) + tax slider (`:2297`) |
| Standing & Conditional | Standing Orders tab (`:426`) | — | queue hold/ROE/stance waypoints (`:1627-1647`) | — |

### Availability gating — the three-way rule (the crux)
1. **Kind mismatch → HIDE.** Drive off `sel.Kind`; `CountApplicable` counts it so an inapplicable category's header never appears.
2. **Capability absent → DIM (not hide).** `ImGui.BeginDisabled(!e.CanIssue(key))` + a tooltip naming the missing part. **This is the deliberate change from today** — the current flat list *hides* Geo/Grav survey (`&&` short-circuit at `:400/:404`); the design *dims* so the player learns the order exists and what unlocks it (cradle-to-grave made legible). Keys already seeded in `OrderAbilityTable` (verified :199-206): `GeoSurvey`, `GravSurvey`, `Jump`, `EmbarkTroops`, `FireControl`.
3. **Grade DATA/BUILD → DIM "not yet available."** Optionally behind a `_showUnbuiltOrders` checkbox (mirror `_rosterShowUnits` `:1274`).

Orders with no capability gate (Refuel, Move-to-body, Rename, Fleet-ops) are always enabled when the kind matches.

### ImGui mechanics
`CollapsingHeader(label###ordcat{id})` — open/closed state kept by ImGui on the stable `###id` (survives a renamed force). `({n})` badge from `CountApplicable` (cheap per-frame). `BeginDisabled`/`EndDisabled` + `IsItemHovered→SetTooltip` for the dim state. **All user-renamable names through `ImGui.TextUnformatted`** (printf `%` trap); `SliderFloat` fixed formats are fine. Everything runs inside the tab's existing try/catch (`:251`/`:232`) so a throw logs `[RenderError]` once and still runs `EndTabItem`; resolve fleets/battalions/holdings via TryGet.

### Smallest first cut (one bounded, engine-untouched, file-local slice)
Reorganize the **Fleets-tab Issue-Orders LEFT panel** from a flat 6-item list into a CollapsingHeader tree, leaving `IssueOrdersDisplay` (the right detail panel) **byte-identical**:
- Replace the left-child body at `FleetWindow.cs:390-418` with a call to a new `DrawCategorizedOrderList(SelectedFleet)`.
- Add `DrawCategorizedOrderList(Entity fleet)` next to `IssueOrdersDisplay` (~`:1736`) rendering **Movement / Survey / Logistics & Cargo** headers, each populated with the existing `Selectable`s that set `selectedIssueOrderType` (unchanged targets → right panel keeps working):
  - Movement → "Move to…" (`MoveTo`), "Jump…" (`Jump`)
  - Survey → "Geo Survey"/"Grav Survey" **wrapped in `BeginDisabled(!fleet.CanIssue("GeoSurvey"/"GravSurvey"))`** instead of hidden
  - Logistics & Cargo → "Refuel at…" (`RefuelAt`), "Embark/land troops…" (`Troops`, `BeginDisabled(!HasAnyTroopBay(fleet))`)
- No engine edit, no new order, no change to `IssueOrdersDisplay`/`IssueOrderType`. Compiles clean (`CanIssue`/`AbilitiesOf` already public).
- **Follow-ups:** F2 re-shelve `DrawBattalion*` under headers; F3 make `DrawRosterDetail` (`:2045`) call `DrawCategorizedOrders(e.Ref)` so every kind gets the tree; F4 grow `OrderAbilityTable` engine-side (each with a `ShipRoleToolsTests` row) for newly-surfaced keys (e.g. a `Reload`/`DeployStation` key), then dim on it.

**Files:** all client work in `Pulsar4X.Client/Interface/Windows/FleetWindow.cs`; the gate is `GameEngine/Ships/ShipRoleTools.cs:173-220`; taxonomy in `docs/combat/FORCES-WINDOW-DESIGN.md` §10. Add a `docs/CLIENT-TEST-CHECKLIST.md` row — CI compiles but can't run the client.

---

## Section 4 — Surprising / wrong / needs-a-decision

**Already built — drop from the build list (recon hints were stale):**
- **Set Target Priority** — the hint said "DirectCall, build the selector, blocked behind the FleetWindow stack." **VERIFIED already built and wired:** `FleetWindow.DisplayCombatTab` calls `DisplayTargetPrioritySelector` at `:665`; selector at `:683-706`; engine `FleetDoctrine.SetTargeting` (`:48-59`) CI-gauged by `FleetDoctrineTests.SetTargeting_SetsCreatesAndPreservesPosture`. **No work remains.** (Honesty note for the tooltip backlog: only 2 of 6 modes — Heaviest, BiggestThreat — actually reorder aggregate casualties; the other 4 fall back to Balanced because the whole-or-dead pool has no per-ship health/position. A nicety, not a gap.)
- **Pause-on-Action (auto-halt)** — **AlreadyHasUI:** `OrdersListWindow.cs:119` already binds `ref order.PauseOnAction` per queued order. It's a per-**EntityCommand** flag (checked at `OrderableProcessor.cs:61`) — do NOT try to wire it into the ground/formation queue; `GroundOrder` has no such field.

**Parked — need a developer ruling before routing (do not build fresh):**
- **Move Unit to Hex within Region** (`OrderMoveToHex`, `GroundForcesDB.cs:785`) and **Move Formation to Region-Hex** (`OrderFormationMoveToHex`, `:864`) — both drive the **region-LOCAL hex march layer scheduled for DELETION** under the 2026-07-28 M1 "one movement layer" ruling (GroundCombat/CLAUDE.md CANON OVERRIDE), and both are **direct calls that bypass the order queue** → the **AI cannot issue them** (one-verb-both-seats violation — the same reason the client's existing direct `OrderMoveToGlobalHex` call is *also* flagged for deletion). **Question for the developer:** given M1 deletes the region-local layer, do you want *any* per-region hex-move UI, or should hex moves route exclusively through the queued **global** path (`GroundOrder.MoveHex` → C4)? Recommendation: build C4 (queued/global) and leave these alone.
- **Move Formation Tree** (`OrderFormationTreeMoveToHex`, `GroundForcesDB.cs:966`) — deferred in the client on purpose (`FleetWindow.cs:1503`, "needs a hex target picker"). It is a **direct, immediate, non-queued** whole-tree march in **region-LOCAL** coords — so it both bypasses the queue (AI can't issue) **and** uses a different coordinate space than the global queued MoveHex (C4). **Question:** is an immediate whole-tree march wanted at all, and if so should it be re-expressed as a *queued* order in *global* coords so both seats can drive it? Don't wire the region-local direct version without that call.
- **Intercept / Ram semantics** — `ThrustToTargetCmd` is a literal **kinetic-kill ram** (burns half Δv to close, then collides), designed for missiles. On a crewed ship it flies the ship into the target. **Question:** is a literal ram the intended player verb, or do you want a "match orbit and hold at weapons range" intercept instead (a different engine order)? Build C6 as-is only if the ram is intended; otherwise it needs a new order.

**Latent bugs to fix in-slice (not blockers, but cheap while you're there):**
- `LogiBaseDB.Clone` copy-ctor (`LogiBaseDB.cs:35-44`) **omits `DesiredLevels` + `ItemsInTransit`** — routes forget their min/max on Clone/save-load. Fix in the RANK-1 slice.
- `EditExsistingJob` (`IndustryTools.cs:59-75`) **silently ignores `autoInstall`** (block commented `:69-73`) — a UI "edit auto-install" would be a no-op. Fix engine-side first if that edit is wanted (EP ships count+repeat regardless).
- `FORCES-WINDOW-DESIGN.md:317` says Edit Production Job has "no wired caller" — **STALE:** `IndustryPanel.cs:282` *is* a wired caller (legacy Economy repeat toggle). The real gap is that the **live** `IndustryDisplay` has no edit at all.

**Content gate (not a bug, a prerequisite):**
- **Dock/Undock is unexercisable until a carrier hull mounts a `DockBayAtb`.** No base-mod ship carries one, so `DockTools.Capacity==0` everywhere and the UI gate hides the order (game byte-identical). Build the `DockOrder` + test now; add a carrier hull via the six-point chain to actually play it.

**Do-not-touch traps confirmed in source:**
- `Thrust90ToTargetCmd` (`NewtonThrustCommand.cs:476`) — incomplete alternate ("never fully completed. Delete?"), do not wire.
- `OrdnanceDesignWindow` — dead/unreachable UI; do not route rearm through it.
- `Fleets/RefuelAction` + `Fleets/ResupplyAction` stubs — de-fanged no-ops (`ResupplyAction` has no `CreateResupplyFleetCommand`); the real refuel/rearm reuses `CargoTransferOrder`. Exclude both.
- `SetLogisticsOrder.Clone()` (`:170-173`) and `ThrustToTargetCmd.Clone()` (`:464`) both throw `NotImplementedException` — acceptable, since `StandAloneOrderHandler` doesn't Clone instant/live orders; only save/load of an in-flight order would throw (combat is transient).
