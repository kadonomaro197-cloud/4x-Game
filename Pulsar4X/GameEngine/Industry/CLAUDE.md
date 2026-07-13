# Industry — Subsystem Reference

Production, mining, and material processing. Lives in `GameEngine/Industry/`.

---

## File Map

| File | Purpose |
|------|---------|
| `IndustryAbilityDB.cs` | DataBlob: production lines with job queues. Attached to colonies and possibly ships with fabrication bays. |
| `IndustryAtb.cs` | Component design attribute that grants a production line with a given build-point rate. |
| `IndustryJob.cs` | One production job: what to build, how many, priority, repeat flag. |
| `IndustryOrder.cs` | Player order to add/remove/reprioritize production jobs. **The class is named `IndustryOrder2`** (not `IndustryOrder`) — the file name and the C# type name differ. |
| `IndustryProcessor.cs` | `IHotloopProcessor` (daily). Calls `IndustryTools.ConstructStuff()`. |
| `IndustryTools.cs` | Static helpers: `ConstructStuff()`, `AddJob()`, `EditExsistingJob()`, `CancelExsistingJob()`, `ChangeJobPriority()`. |
| `LocalConstructionDB.cs` | **DataBlob — a SECOND, fully-wired construction path.** Attached to an entity that can build things locally. Holds `PointsPerDay` and a `BuildQueue` of `LocalConstructionJob`. Distinct from the `IndustryAbilityDB` production-line path above. |
| `LocalConstructionAtb.cs` | Component design attribute that grants local-construction ability (points per day). |
| `LocalConstructionJob.cs` | One local-construction job: the `Design` to build plus `PointsAccumulated` so far. |
| `LocalConstructionProcessor.cs` | `IHotloopProcessor` (daily, `FirstRunOffset` 6 h). Auto-discovered. Applies `PointsPerDay` to the head of `BuildQueue`; on completion installs the component via `Entity.AddComponent()` and publishes `ProductionCompleted`. Code exists and is wired; runtime unverified here (CI can't run the client). |
| `Orders/` | Player orders for the local-construction queue: `AddToConstructionQueueOrder`, `RemoveFromConstructionQueueOrder`, `MoveUpInConstructionQueueOrder`, `MoveDownInConstructionQueueOrder`. |
| `InstallationsDB.cs` | **DEAD/vestigial DataBlob** — never attached to any colony, no `[JsonProperty]`. Installations are really components in `ComponentInstancesDB`. Do not build on this. |
| `InfrastructureDB.cs` | **NEW (DevBranch)** DataBlob on a colony. Tracks `CapacityProvided` (from infra installations) and `CapacityRequired` (from all other installations). `Efficiency` = Provided/Required, capped at 1.0. |
| `InfrastructureCapacityAtb.cs` | **NEW (DevBranch)** Component design attribute. Marks a component as "infrastructure." `Capacity` = units of support provided per installed unit. Filtered by gravity/pressure tolerance. |
| `InfrastructureProcessor.cs` | **NEW (DevBranch)** Static class. `RecalcCapacity(colony)` — sums provided vs required capacity and writes to `InfrastructureDB`. Called on `ComponentInstancesDB` recalc (whenever an installation changes). `GetEfficiency(colony)` — returns the output multiplier. |
| `JobBase.cs` | Abstract base for job types. |
| `MineResourcesAtbDB.cs` | Component attribute DataBlob: grants mining ability (which minerals, at what rate). |
| `MineResourcesProcessor.cs` | `IHotloopProcessor` (daily) **and** `IRecalcProcessor`. Two clocks: `CalculateActualMiningRates()` runs at **recalc time** (`CalcMaxRate`/`RecalcEntity`) and caches the result in `MiningDB.ActualMiningRate`; the **daily tick** (`MineResources`) reads that cached rate, scales it by infrastructure efficiency (`InfrastructureProcessor.GetEfficiency`), and transfers the minerals into cargo (see `MineResourcesProcessor.cs:64,71`). |
| `HexMinerals.cs` | **NEW (located deposits, 2026-07-06)** Seeds a body's mineral deposits onto specific surface **hexes** (`GroundHex.DepositMineralId`/`DepositAmount`) so "there are resources HERE" is a PLACE you scan/see/build a mine on — the LOCKED PRINCIPLE (`docs/GROUND-COMBAT-MAP-DESIGN.md`) applied to minerals. `SeedDeposits(body, grid)` picks hexes **terrain-flavored** (mountains/volcanic/highlands rich; never ocean/ice), one mineral per deposit hex, partitioning a share (`LocatableFraction` 0.6) of the body's real `MineralsDB` amount; deterministic (system RNG), idempotent, defensive. Hooked in `PlanetGridFactory.EnsureGridForBody` (runs when a body's surface grid is generated — post-survey), so **every planet, whole game**. **v1 is the LOCATED VIEW only — the colony still mines the body-wide pool (`MineResourcesProcessor` unchanged); per-hex mining (a mine works the deposit it SITS on, and that hex depletes) is the follow-up that promotes these to the source of truth.** Next slices: planet-view flags deposit hexes post-scan → build-a-mine-on-a-deposit → per-hex mining. Gauge: `HexMineralsTests`. |
| `MineralsDB.cs` | DataBlob on a planet: mineral deposits (type → `MineralDeposit { Amount, Accessibility }`). |
| `MiningDB.cs` | DataBlob on a colony: active mining configuration. |
| `MiningHelper.cs` | `CalculateActualMiningRates()` — determines effective mining rate per mineral per host. `TryGetMiningBody(entity, out body)` — the host-agnostic resource-body resolver (colony→`PlanetEntity`, station→`StationInfoDB.HostingBodyEntity`); used by `MineResourcesProcessor` so a **station mines its hosting body exactly like a colony** (no host-type branch). |
| `MineralDepositFactory.cs` | Creates mineral deposits during system generation. |
| `Mineral.cs` | Mineral type definition (name, ID). |
| `ProcessedMaterial.cs` | Processed material definition (refined output from raw minerals). |

---

## Production System

### Data Model

`IndustryAbilityDB` holds a dictionary (`ProductionLines`) of named `ProductionLine` objects (`IndustryAbilityDB.cs:12-18`), each with:
- `Name` — the line's name.
- `MaxVolume` — the line's volume capacity.
- `IndustryTypeRates: Dictionary<string, int>` — output rate **per industry type** (not a single build-point number). There is **no** `BuildPoints` field; a line's throughput is this per-type rate table.
- `Jobs: List<IndustryJob>` — ordered job queue.

`IndustryAtb` (component design attribute) contributes a production line when installed on an entity.

### Flow

```
IndustryProcessor (daily)
    → IndustryTools.ConstructStuff(entity)
        → for each ProductionLine in IndustryAbilityDB:
            → process top job in Jobs queue
            → apply the line's rate toward job completion
            → on completion: ONE polymorphic call, designInfo.OnConstructionComplete(...) (IndustryTools.cs:219)
                — the design delivers ITSELF; the processor does NOT switch on type.
                  (A ProcessedMaterial adds to cargo, a ComponentDesign installs/stockpiles,
                   a GroundUnitDesign raises a unit — each design decides. See gotcha #7.)
```

### Adding a Job (player action)
```
IndustryOrder → IndustryTools.AddJob(entity, productionLineID, job)
    → locks ProductionLine and appends job
```

### Job reprioritization
`IndustryTools.ChangeJobPriority(entity, prodLine, jobID, delta)` — moves a job up or down in the list by `delta` positions.

---

## Mining System

### Data Model

- `MineralsDB` on the **planet**: maps mineral ID → `MineralDeposit { Amount (long), Accessibility (double 0–1) }`.
- `MiningDB` on the **colony**: active mining setup (which minerals are being extracted).
- `MineResourcesAtbDB` on installed **components**: specifies which minerals can be mined and the base rate.

### Flow

```
MineResourcesProcessor (daily)
    → MiningHelper.CalculateActualMiningRates(colonyEntity)
        → sums rates from all MineResourcesAtbDB components
        → scales by mineral Accessibility
        → returns Dict<mineralID, long rate>
    → for each mineral:
        → removes Amount from MineralsDB (planet's deposit depletes)
        → adds Amount to CargoStorageDB (colony's cargo hold)
```

Mineral `Accessibility` ranges 0.0–1.0. Low accessibility deposits are harder to extract — they produce less per mining unit. Deposits deplete as they are mined.

**ALPHA starting stockpiles (2026-07-03; storage-cap bug FIXED 2026-07-10).** The start colony's stockpiles are stocked LARGE so building isn't resource-gated during testing (the developer's "picket couldn't build — stockpiles emptied"). In `GameData/basemod/ScenarioFiles/systems/sol/earth.json` the colony's `Cargo` block stocks **20 general-storage goods** (every raw mineral + the common refined ship-build materials `stainless-steel`/`electronics`/`plastic`/`space-crete`/`ree-magnetics`) at **1,000,000 each**, with the `default-design-warehouse` count at **10**. **⚠️ THE ORIGINAL 50M AMOUNT SILENTLY OVERFLOWED STORAGE (the storage-cap bug):** the `EconomyReadoutTests` CI readout (run #786) showed only the **first ~6 minerals** actually landing at 50M; then Lithium partial, **Titanium at -3**, and the rest (Silicon/Regolith/Water/Space-Crete/…) **clamped to 0** — jamming the whole economy (mined output had nowhere to go, deposits depleted ~11 units/year; the refinery starved and never completed a batch). **Root cause:** each warehouse *requests* 1,000,000 m³ but the `warehouse-facility` template `MaxFormula` (`TemplateFiles/storage.json:32`) **clamped Storage Volume to 10,000 m³** — so 10 warehouses = **100,000 m³**, not the assumed 10M. 50M × ~17 goods (~3M m³) is a ~31× overflow; cargo storage is volume-capped (`CargoMath.AddRemoveCargoByMass` caps each add at `FreeVolume`, `CargoMath.cs:73,75` — the `-3` is a negative-FreeVolume rounding artifact). **Fix taken:** dropped the per-good amount to **1,000,000** — verified (exact `Σ VolumePerUnit × amount`) at **62,319 m³ = ~62% of the 100,000 m³ cap**, so all 20 goods FIT with ~38% headroom for a year of mining+refining output (2M each would overflow at 125%). This is the **lowest-blast-radius** fix: single-file (`earth.json`), no template change, no infra shift. *(The alternative — unclamp the warehouse template `MaxFormula 10000 → 2000000` to keep 50M — is a GLOBAL change: every warehouse in the game gets 100× volume AND 100× crew/mass; feasible on the Earth colony's infra budget but broad, so it's parked as a deliberate choice, not taken autonomously.)* **Data-drift guard (gotcha #10):** every `Cargo` Id must be **unlocked** in that colony's `StartingItems` (regolith/water/rare-earth-elements are) — `ColonyFactory.LoadCargo` hard-indexes `CargoGoods[id]`, so an un-unlocked id crashes New Game. **Latent bug flagged (separate ticket):** `CargoMath.cs:72` computes `MassPerUnit * mass` where it looks like it should be `mass / MassPerUnit` — only `space-crete` (MassPerUnit 2.4) is affected; not the storage-cap cause.

**Mine output rate (tunable, one constant).** The base `Mine` installation's per-mineral daily rate comes from the `mine` template's `MiningAmount` property in `GameData/basemod/TemplateFiles/installations.json`: `MiningAmount = Area × 0.00001`. So the default 1,000,000 m² mine = **10 units/mineral/day**, and a max-size 100,000,000 m² mine = 1,000/day; `ActualMiningRate` then scales that by accessibility (and rounds). Bumped 10× from the original `× 0.000001` (which gave a near-useless 1/mineral/day) on 2026-06-23 to restore the historical design scale the old `MiningTests` encoded (~9.5/mineral per mine). This single constant sets the early economy pace — dial it there. Sensor: `EconomyReadoutTests` prints `BaseMiningRate`/`ActualMiningRate` and asserts the planet's deposits deplete over a game-year. (The `automine` template uses a separate formula, `Size × 0.005`.)

---

## Installations

**Installations are components.** A colony's installations are `ComponentInstance`s in its `ComponentInstancesDB`, added with `colonyEntity.AddComponent(design, count)` (see `DefaultStartFactory.cs:212-225`). Each installation type is a `ComponentDesign` carrying `*Atb` ability attributes (mining, industry, population support, etc.), and processors find them via `TryGetComponentsByAttribute<TAtb>()`. This is the **same** framework ships use for their components.

`InstallationsDB` (this directory) looks like an installation registry — `Dictionary<string,float> Installations`, `WorkingInstallations`, `EmploymentList`, plus commented-out `ConstructJob` lists — but it is **abandoned**: never attached to a colony, no `[JsonProperty]` fields. It is an earlier design superseded by the component approach. **Do not** use it, extend it, or render it.

**The installations UI gap:** `PlanetaryWindow.RenderInstallations()` is empty *and* its tab is gated on `HasDataBlob<InstallationsDB>()` (always false), so the tab never even shows. Phase 2a fix = render from `ComponentInstancesDB` (reuse `ComponentInstancesDBDisplay`). See `docs/aurora/PLANETARY-INFRASTRUCTURE.md` §6 and `CONVENTIONS.md` §6.

---

## Infrastructure System (DevBranch — Active)

Infrastructure is the **limiting factor on all colony production**. Think of it as the colony's utility grid: power, roads, comms, water. Every installation except infrastructure itself draws from the grid; infrastructure installations add to it. When demand exceeds supply, efficiency drops and all production scales down.

### How capacity is calculated

`InfrastructureProcessor.RecalcCapacity(colonyEntity)`:

**Provided** — sum across all `InfrastructureCapacityAtb` components. Each installed unit contributes `Capacity` units of support, but only if the colony's body gravity and atmospheric pressure are within the component's `GravityToleranceAtb` / `PressureToleranceAtb` limits (out-of-tolerance infrastructure provides nothing — matches the population support model).

**Required** — sum across every OTHER installed component:
```
capacity demanded = (design.MassPerUnit / 1000) + design.CrewReq
```
i.e., 1 unit per tonne of installation mass + 1 unit per crew member.

**Efficiency** = min(1.0, Provided / Required). Written to `InfrastructureDB.Efficiency`. Production processors read this via `InfrastructureProcessor.GetEfficiency(colony)`.

### Note on two parallel systems

`InfrastructureCapacityAtb` (production efficiency) and `PopulationSupportAtbDB` (population cap) are **separate**. An infrastructure installation provides BOTH — it adds to the production efficiency grid AND supports population up to a tolerance-filtered cap. They are different attributes on the same component design, read by different processors.

---

## Key Extension Points for Ground Combat

1. **New `IndustryJob` subtype** — add `GroundUnitConstructionJob` to allow colonies to build ground units through the existing production system.
2. **New `IConstructableDesign`** — `GroundUnitDesign` must implement `IConstructableDesign` (the interface checked by `IndustryTools.ConstructStuff()`).
3. **Installation specialization** — model a "military installation" / GFCC as a new `ComponentDesign` with a production-multiplier `*Atb` attribute (NOT by extending the dead `InstallationsDB`). The Ground Force Construction Complex in Aurora is just another installation = component. See `docs/aurora/GROUND-COMBAT.md` §7.

---

## Gotchas

1. **`IndustryTools.ConstructStuff()` uses `lock` on the production line.** New job types must respect this lock or risk race conditions when orders arrive mid-tick.

2. **`InstallationsDB` is dead — don't confuse it for the installation store.** Its `float Installations` / `int WorkingInstallations` fields suggest partial-vs-complete tracking, but nothing writes to them and nothing attaches the blob. Construction delivers installations as components via `Entity.AddComponent()` into `ComponentInstancesDB`. Partial-construction progress (if needed) lives in the industry job, not here.

3. **Mineral depletion is permanent.** `MineralsDB` is on the planet entity with `[JsonProperty]`. Once depleted, deposits do not regenerate. This is by design (mirrors Aurora 4x) but has save implications — mining a body to zero is irreversible.

4. **`MineResourcesProcessor` and `IndustryProcessor` both run daily** but have a `FirstRunOffset` of 3 hours for Industry, so there is no exact same-tick race. If adding a processor that must run after mining and before production, set `FirstRunOffset` between 0 and 3 hours.

5. **`IndustryJob` needs its `[JsonConstructor]` parameterless ctor — deleting it breaks LOAD of any save with a queued job (2026-07-03 "save didn't work").** `IndustryJob`'s only public ctor is `(FactionInfoDB, string)`, which does `factionInfo.IndustryDesigns[itemID]`. On **load**, Newtonsoft picks a constructor to build the object; with no parameterless one it used that ctor and passed **null** for `factionInfo` → `NullReferenceException`, so LOADING any save that contained a production job threw (the base `SaveLoadSmokeTests` missed it — its universe has no colony, so no jobs). Fix: a `[JsonConstructor] private IndustryJob() { }` — Json.NET uses it and then sets the serialized fields directly, no design lookup. **Rule: a class whose real ctor reaches into other game state (a dictionary, a manager) must ship a parameterless `[JsonConstructor]`, or its own load path re-runs that lookup with nulls.** Gauge: `SaveLoadWithJobTests` (queues a real job FIRST so the save actually contains an `IndustryJob` to round-trip).

6. **A production job REJECTS a zero-cost design (`resources can't cost 0`, 2026-07-06).** `IndustryTools.ConstructStuff` throws if a queued design's `ResourceCosts` is empty/zero. If a *test* wants to isolate the queue→complete mechanic from mineral availability, don't zero the cost — use a **minimal non-zero** cost of a bulk-stocked material (e.g. `{ "stainless-steel": 1 }`; the start colony stocks the common refined materials at 50M — see the mining section).

7. **On job completion the processor dispatches to the design's OWN `OnConstructionComplete` — single point, nothing stockpiles alongside** (`IndustryTools.cs:219`). Each `IConstructableDesign` decides its own delivery: a `ProcessedMaterial` adds to cargo, a `ComponentDesign` installs/stockpiles, a `GroundUnitDesign` **raises a unit on the colony's planet** (no `InstallOn` needed — it ignores it). **Gauging a ground-unit build→raise: drive `OnConstructionComplete` directly** rather than the full queue — the `installation-construction` queue-to-completion path is NOT reliably driven by `TestScenario.CreateWithColony` + `AdvanceTime` (no green test exercises it), so a queued-build assertion is flaky; the completion hook is the exact call the processor makes and is deterministic (the generic queue itself is proven by `ProductionBuildTests`). Gauge: `GroundUnitFieldingTests`.
