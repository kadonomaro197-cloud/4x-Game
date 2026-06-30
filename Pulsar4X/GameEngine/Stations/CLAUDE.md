# Stations — Subsystem Reference

Space stations — the **cheap, fast, flexible, FRAGILE alternative to a planetary colony**. Lives in `GameEngine/Stations/`. New as of 2026-06-29.

> **Read `docs/SPACE-STATIONS-DESIGN.md` before touching this.** It holds the locked architecture decision (PARALLEL host, NOT a generalized colony) and the open tuning questions (cost-gradient curve, durability/invasion numbers, manning). This subsystem is the foundation slice of that design.

---

## The one idea

A station does the same off-world jobs a colony does — mine, refine, research, trade, house people — by carrying the **same component-equipment chassis** a colony carries. But it is its **own host type** so it can own its own cost curve, durability, and invasion math. That distinctness is the whole point: a station is cheap while focused and expensive as a planet-replacement, and a fraction of the effort to destroy compared to a planet's long ground war ("blowing on a flower vs. pushing a Seawolf-class submarine").

**Why parallel and not a planet-less colony:** those two trade-offs only exist if stations and planets are mechanically distinct hosts. See the design doc's "Why PARALLEL" section.

---

## File Map

| File | Purpose |
|------|---------|
| `StationInfoDB.cs` | Core station DataBlob — the parallel to `ColonyInfoDB`. Fields: `Population` (species ID → count, manned stations only), `ComponentStockpile`, `HostingBodyEntity` (the body/belt/anomaly it orbits — the parallel to `ColonyInfoDB.PlanetEntity`). Copy-ctor + `Clone()`; `GetDependencies()` → `NameDB`. |
| `StationFactory.cs` | `CreateStation(faction, hostingBody, initialPopulation=0, species=null)` — creates the station entity, attaches the SHARED blob set (incl. `ColonyMoraleDB`), registers it on `FactionInfoDB.Stations`, and grants the faction mineral access on the hosting body. The parallel to `ColonyFactory.CreateColony`. |
| `StationPopulationProcessor.cs` | `IHotloopProcessor` keyed on `StationInfoDB` (parallel to `Colonies/PopulationProcessor`). Grows/starves a MANNED station's population against its **habitat-module life-support cap** (sealed-habitat model — no planet `ColonyCost`); reuses the host-agnostic `ColonyMoraleDB` math. Keyed on its own blob type so it never collides with the colony hotloop. |

---

## The shared chassis (what `StationFactory` attaches)

Mirrors `ColonyFactory.CreateColony` exactly, because the economy processors discover their work by these ability blobs, not by host type:

`NameDB`, `StationInfoDB`, `MiningDB`, `OrderableDB`, `MassVolumeDB`, `CargoStorageDB`, `PositionDB` (relative to the hosting body), `TeamsHousedDB`, `ComponentInstancesDB` (installed modules live here), `InfrastructureDB`.

**Deliberately NOT attached: `ColonyBonusesDB`.** Its `GetDependencies()` hard-requires `ColonyInfoDB` (which a station doesn't have), so attaching it fails `EntityManager.AddEntity`'s dependency validation (`AreAllDependenciesPresent()` → `InvalidOperationException`). How a station carries production/research/mining bonuses is a decision for the economy-wiring slice (task #17) — relax that dependency, or give the station a station-compatible bonuses blob. This was caught by CI on the first push (the foundation's first red → green).

It is then registered: `factionInfo.Stations.Add(station)` + `FactionOwnerDB.SetOwned(station)`.

---

## The key finding — economy comes for FREE, population does NOT

The economy processors query by **ability component**, not by `ColonyInfoDB`:
- `MineResourcesProcessor` → `GetAllEntitiesWithDataBlob<MiningDB>()`
- `IndustryProcessor` → `GetAllEntitiesWithDataBlob<IndustryAbilityDB>()` (auto-attached when an industry module is installed)
- `ResearchProcessor` → `GetAllEntitiesWithDataBlob<ResearcherDB>()` (spawned by a research-lab component)

So a station that carries the matching components is mined / built / researched **without any station-aware code**. The exceptions:
1. **Mining — WIRED 2026-06-30 (task #17, slice 1).** `MineResourcesProcessor` was hard-coded to read `MineralsDB` off `colonyInfoDB.PlanetEntity` in three spots (`ProcessEntity`, `CalcMaxRate`, and `MiningHelper.CalculateActualMiningRates`). All three now resolve the resource body through the host-agnostic helper **`MiningHelper.TryGetMiningBody(entity, out bodyEntity)`** — colony → `PlanetEntity`, station → `HostingBodyEntity` (guards an unset/invalid body so it can't NPE). A station that carries a mine component + a cargo hold now mines its hosting body exactly like a colony. `InfrastructureProcessor.GetEfficiency` and the recalc path were already host-agnostic. Mining bonuses come from `ColonyBonusesDB` (a station has none → defaults to ×1.0). Gauge: `StationFactoryTests.Station_WithMiningModule_MinesItsHostingBody` (a station carrying the colony's own mine + cargo designs accrues `CargoStorageDB.TotalStoredMass`) + `TryGetMiningBody_ResolvesColonyAndStation`.
2. **Population — WIRED 2026-06-30 (task #17, population half) via a PARALLEL processor.** `PopulationProcessor` stays hard-keyed to `ColonyInfoDB` (untouched → zero colony-regression risk); a new **`StationPopulationProcessor`** keyed on `StationInfoDB` grows/starves a manned station's `Population`. The key difference, and why it's a separate processor not a shared path: a station is a **SEALED HABITAT** — its population cap is the life support its habitat modules (`PopulationSupportAtbDB`) provide, **NOT** the body's habitability (`ColonyCost`). So a station with no habitat modules is a **tomb** (population dies off); one with ample habitat grows toward that cap (a native world like Earth grows un-capped — a station never does, which is its fragility). The **morale machinery is shared/host-agnostic**: `StationFactory` now attaches `ColonyMoraleDB`, and the processor feeds it station-appropriate inputs (no planet cost; crowding vs the habitat cap; jobs/comfort from the same module extensions). Gauge: `StationFactoryTests.MannedStation_WithNoLifeSupport_LosesPopulation`.

---

## Gotchas

1. **`PositionDB`'s active class is in `Pulsar4X.Movement`, not `Datablobs`** (the Datablobs copy is commented out). `StationFactory` imports `Pulsar4X.Movement` for it — the same trap that bit the test harness.
2. **`FactionInfoDB.Stations` must be added to the FactionInfoDB copy-ctor**, or it's dropped on `Clone()` / save-load round-trip. It is (mirrors the `Colonies` line). Any new faction-level list needs the same treatment.
3. **A bare station does nothing yet.** This foundation slice builds the host; the economy wiring (task #17) and the first real flavor — the research station (task #18) — are what make it a cradle-to-grave decision. Don't ship a station the player can build but that has no product/loss — that's the "pretty, not a decision" anti-pattern.

---

## Tests

**The void habitat (data, 2026-06-30).** A manned station needs life-support modules or it starves (the sealed-habitat model). The base game had NO population-support design that works off-planet — the only one, `default-design-infrastructure` ("Earth-Standard Infrastructure"), is gravity/pressure-toleranced to Earth (8.8–10.8 m/s², 0.9–1.1 atm), so it provides ZERO support in microgravity. Added a **`space-habitat` ComponentTemplate** (`TemplateFiles/installations.json`) + a **`default-design-space-habitat`** design ("Space Habitat") — the same `PopulationSupportAtbDB` + `InfrastructureCapacityAtb` + `CargoStorageAtb` as infrastructure, but with **NO `GravityToleranceAtb`/`PressureToleranceAtb` at all**, so a sealed self-contained habitat supports population on ANY body. (Why a new template, not the infrastructure one with tolerance values set to 0: the template's gravity/pressure values are clamped to the tech-formula bounds at instantiation — ~9.81 m/s² / ~1.0 atm with no tech — so an override to 0 gets pulled back to Earth-ish. `GetPopulationSupportValue` only applies a tolerance gate when the attribute is *present*, so omitting it is the clean fix. Caught by CI: the first cut set tolerance values to 0 and the habitat still gave 0 support in microgravity.) Unlocked at start (added to the Earth colony blueprint's `ComponentDesigns` in `systems/sol/earth.json`). This is the data that makes an asteroid station viable. Gauge: `StationFactoryTests.SpaceHabitat_SupportsPopulationInMicrogravity_WhereEarthInfraCannot` (Earth infra → 0 support at an asteroid, space habitat → >0, and a habitat-supported station grows).

**Build model — deploy bare, build IN-SITU (LOCKED 2026-06-30, see `docs/SPACE-STATIONS-DESIGN.md`).** A station is a generic chassis; "mining/research station" is emergent from the modules you bolt on. You deploy a bare platform (frame + power + control + minimal infra + a small constructor) and build modules ON it on location. The whole in-situ loop (`IndustryProcessor` → `ConstructStuff` → `ComponentDesign.OnConstructionComplete` → `InstallOn.AddComponent`) is **already host-agnostic** — a station with a constructor module builds + installs its own modules for free. Remaining to build is the PLAYER front: a deploy order/UI + queue-module-on-a-station UI + materials shipping. Gauge: `Station_WithConstructorModule_IsAnInSituBuilder`.

`Pulsar4X.Tests/StationFactoryTests.cs` (rides `TestScenario.CreateWithColony`):
- `CreateStation_WiresSharedChassis_AndRegistersOnFaction` — the station carries the shared blob set, hosting body is set, it's in `Stations` (not `Colonies`), owned by the faction, in the body's manager.
- `CreateStation_PopulationOptional` — automated = unmanned; manned houses the given species.
- `StationInfoDB_ClonesDeeply` — `Clone()` deep-copies the collections (survives entity transfer / save-load).
