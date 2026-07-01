# Colonies — Subsystem Reference

Population, colony lifecycle, life support. Lives in `GameEngine/Colonies/`.

> **Before building colony progression, read `docs/COLONY-PROGRESSION-DESIGN.md`** — the developer's captured vision for the growth ladder every infrastructure site (player + NPC) climbs: **Outpost** (the only automatable tier) → **Colony** (has flavors) → **World** → **Minor → Hub → Major → Capitol**, each step cost↑/yield↑ *if done right*, all scaling. Captured 2026-06-28; a vision, not a build spec (open questions listed there).

---

## File Map

| File | Purpose |
|------|---------|
| `ColonyInfoDB.cs` | Core colony DataBlob: population per species, stockpiles, parent planet reference, component dictionary for bombardment. |
| `ColonyLifeSupportDB.cs` | Tracks `MaxPopulation` — the carrying capacity ceiling calculated from infrastructure. |
| `ColonyMoraleDB.cs` | **NEW (M1, 2026-06-29)** Morale (0–100, 50 = neutral) — the level-control valve on the population "tank". Recalced each population tick from conditions + overcrowding (M1) + employment + housing comfort (M2); drives migration. All weights are named coefficients (government-ready). Host-agnostic (station-attachable later). See `docs/MORALE-AND-POPULATION-DESIGN.md`. |
| `LegitimacyDB.cs` | **NEW (#31, 2026-07-01)** The regime's health bar for ONE province (0–100) — the INTERNAL-politics counterpart to morale, and the locked "LOCAL not empire-wide" decision (so the whole empire can't rebel at once). DERIVED each cycle (like morale, not a parallel resource): v1 baseline = the province's hosts' average morale, adjusted by demand track-record + war outcomes + governor competence + connectivity (each neutral when unwired). `LegitimacyDB.IsCollapsing(v)` (< `CollapseThreshold` 20) is the REBELLION trigger (#38 hook). Pure math (`ComputeLegitimacy` + `LegitimacyInputs`), named coefficients (government-ready). **LIVE as of 2026-07-01:** attached to every colony (`ColonyFactory`, both paths) + station (`StationFactory`), recomputed monthly by `LegitimacyProcessor`. Tests: `LegitimacyTests` (math) + `LegitimacyProcessorTests` (wiring). See `docs/GOVERNMENT-AND-POLITICS-DESIGN.md`. |
| `LegitimacyProcessor.cs` | **NEW (#31 wiring, 2026-07-01)** `IHotloopProcessor` keyed on **`LegitimacyDB`** (its own blob — NOT ColonyInfoDB/ColonyMoraleDB, the one-hotloop-per-blob rule L9), monthly. Recomputes each province's `LegitimacyDB.Legitimacy` from the sibling `ColonyMoraleDB.Morale` (v1 morale-only driver). Host-agnostic (colonies + stations). Defensive (never throws — L4). Mirrors `ColonyEconomyProcessor`'s "keyed on its own blob, reads a sibling, runs colony-side" shape. **Now reads the WAR term too (2026-07-01):** `WarTermFor(province)` looks up the owning faction's `DiplomacyDB.IsAtWarWithAnyone()` and, if at war, feeds the government's `WarMoraleFactor()` as legitimacy's `WarOutcome` — a pacifist regime's provinces bleed loyalty during war, a militarist's take pride (the casus-belli militarism gate, live on the province; no `GovernmentDB` → neutral Mid default). **v1: morale + war-standing driver; the demand/governor/connectivity inputs + government re-weighting wire in later.** Gauge: `LegitimacyProcessorTests` (incl. `War_TaxesLegitimacy_ByMilitarism`). |
| `ColonySustenanceDB.cs` + `SustenanceProcessor.cs` | **NEW (M5b, #29, 2026-07-01)** Power/food shortage gauges + the monthly processor that computes them (demand = pop × per-capita coefficient, vs supply). Feeds morale (`PopulationProcessor` reads `PowerShortage`/`FoodShortage` into `MoraleInputs`) + a starvation death term. **NEUTRAL-WHEN-ABSENT: the per-capita demand coefficients default to 0, so every shortage computes to 0 until calibrated on the local build** — the deliberate guard against the "default deficit tanks every colony" trap. Processor keyed on its own blob (`ColonySustenanceDB`), host-agnostic, defensive. **v1: the wiring is CI-green + inert; the demand rates + a food-supply cargo good (doesn't exist yet → food supply reads 0) are the local calibration (tracker C2).** Tests: `SustenanceTests`. |
| `RebellionDB.cs` | **NEW (#38 foundation, 2026-07-01)** A province's REBELLION state — legitimacy collapse made a PROCESS you can fight, not an instant loss. When `LegitimacyProcessor` sees legitimacy fall below `LegitimacyDB.CollapseThreshold` it BEGINS a rebellion (`IsRebelling` + a `ReactionWindowEnds` clock); restoring legitimacy to `RecoveryThreshold` (35, hysteresis above the collapse line) QUELLS it. `WindowExpired(now)` is the cue the (later) resolution slice reads to secede/defect. Attached to every province at factory time (no dynamic blob-adds). **v1: begin/quell + the window clock; the window-expiry resolution (secession→new-faction, espionage defection) and the ground-combat suppress wire are the follow-on #38 slices — no ownership change yet.** Tests: `RebellionTests`. See `docs/GOVERNMENT-AND-POLITICS-DESIGN.md`. |
| `EmploymentAtbDB.cs` | **NEW (M2)** Component attribute: jobs (worker slots) an installation provides. Summed via `ComponentInstancesDBExtensions.GetTotalJobs`. Morale: jobs-vs-pop is two-sided (unemployment debuff / full-employment buff). |
| `HousingAtbDB.cs` | **NEW (M2)** Component attribute: housing **comfort** (a morale bonus) — the quality "tier" above bare life-support capacity (`PopulationSupportAtbDB` keeps people alive; this keeps them content). Summed via `GetHousingComfort`. |
| `ColonyEconomyDB.cs` | **NEW (M4)** The tax lever: a player-set `TaxRate` (0..1, default 10%). Pure `MonthlyTaxIncome(pop, taxRate, morale)` (income scales with morale — a happy colony pays more). Read by `PopulationProcessor` as a morale penalty; read by `ColonyEconomyProcessor` to bill income. Coefficients are named (government-ready). |
| `ColonyEconomyProcessor.cs` | **NEW (M4)** `IHotloopProcessor` keyed on **`ColonyEconomyDB`** (NOT ColonyInfoDB — PopulationProcessor owns that; one hotloop per blob type). Monthly: bills colony tax into the owning faction's `Ledger` (`TransactionCategory.ColonyTax`). Runs colony-side because MasterTimePulse never iterates the GlobalManager where factions live. Finally plugs the colony economy into faction money (only research moved funds before). |
| `ColonyManpowerDB.cs` | **NEW (M3 sub-slice 1)** People as a finite resource. Two pools derived from population: **bulk** workforce (`pop × WorkforceFraction`, crew/workers) and scarce **talent** (`pop × TalentFraction`, officers/scientists/governors). Tracks `CommittedBulk`/`CommittedTalent`; `Available = pool − committed`. `ResolveConstructionCrew(available, crewReq, policy)` is the pure build decision (Block/BuildUnderstaffed). Also fixes M2's employment denominator (jobs vs workforce, not raw pop). |
| `ManpowerTools.cs` | **NEW (M3 sub-slice 2b, #27)** The crew ENFORCEMENT bridge — turns the pool math into a real gate on **ship** construction. `ResolveBuild(host, crewReq)` (reads the host's `ColonyManpowerDB` + owning government's `CrewPolicy()`), `CommitCrew(host, n)`, `ReleaseCrew(sourceColony, n)`. **INERT WHEN ABSENT** (a host with no pool — a station — always builds). Wired at the ship lifecycle: the gate in `IndustryTools.ConstructStuff` (blocks a ship you can't crew, before resources are spent); the commit at `ShipDesign.OnConstructionComplete` (both the direct-launch and the launch-complex-queue paths, since a colony with a `LaunchComplexDB` — e.g. Earth — queues the ship instead of spawning it); the `ShipInfoDB.CrewSourceColonyId` provenance stamp at each real creation point (`OnConstructionComplete` for direct, `LaunchComplexProcessor.TryLaunchShip` for the launch queue); and the release in `ShipFactory.DestroyShip` (frees the source colony's crew). **Start-safe:** the start fleet spawns via `ShipFactory.CreateShip` directly (bypasses the queue → provenance stays -1 → destroy is a no-op), and start pop is billions. Feel + the parked casualties-shrink-population sting are local PC-tests. Gauge: `ManpowerTests`. |
| `ColonyBonusesDB.cs` | Modifier DataBlob for production/research/mining bonuses on a colony. |
| `ColonizeableDB.cs` | Marker DataBlob on planets that can be colonized. |
| `ColonyFactory.cs` | `CreateColony()` — creates a colony entity and attaches all required DataBlobs. |
| `CreateColonyOrder.cs` | Player order to found a colony on a planet. |
| `PopulationProcessor.cs` | `IHotloopProcessor` (monthly). Runs `GrowPopulation()` for each colony. |
| `ColonyHexMapDB.cs` | **NEW (DevBranch)** DataBlob: hex-grid layout for a colony. `MaxRadius` scales with admin building office space. `HexTiles` — dict of coordinate → tile. |
| `ColonyHexMapProcessor.cs` | **NEW (DevBranch)** Processor that updates the hex map when colony state changes. |
| `HexTile.cs` | **NEW (DevBranch)** One hex cell on the colony map. Holds terrain type, what's built there. |
| `HexCoordinate.cs` | **NEW (DevBranch)** Cube-coordinate system for the hex grid. |

---

## Colony Entity DataBlob Composition

A fully-formed colony entity typically holds:

`ColonyFactory.CreateColony()` attaches these blobs **directly** (verified in `ColonyFactory.cs`):

| DataBlob | Purpose |
|----------|---------|
| `NameDB` | Colony name |
| `ColonyInfoDB` | Core: population, stockpiles, parent planet |
| `ColonyBonusesDB` | Modifier tracking |
| `MiningDB` | Mining setup |
| `OrderableDB` | Marks the colony able to receive orders |
| `MassVolumeDB` | Mass / volume |
| `CargoStorageDB` | Mineral and goods storage |
| `PositionDB` | Location (on the planet surface) |
| `TeamsHousedDB` | Scientists / commanders housed here |
| `ComponentInstancesDB` | **Installations live here** (added via `AddComponent()`) |

> **NOT attached by `ColonyFactory`** (corrects the earlier recon):
> - **`InstallationsDB` is never attached to a colony** — it is vestigial/abandoned (no `[JsonProperty]` fields; only dead-code references). Installations are **components** in `ComponentInstancesDB` (see `DefaultStartFactory.cs:212-225`: `AddComponent(mineDesign)`, `AddComponent(facEntity)`, …).
> - Capability blobs such as `IndustryAbilityDB` are **granted by installed components** carrying the matching `*Atb` attribute, not added by the factory.
> - `ColonyLifeSupportDB.MaxPopulation` is (re)computed by `PopulationProcessor`, not added at creation.
>
> Verify the exact grant path in code before assuming a specific blob is present. See `CONVENTIONS.md` §6 (components = abilities).

---

## ColonyInfoDB Fields

```csharp
Dictionary<int, long> Population           // species entity ID → population count
Dictionary<string, int> ComponentStockpile // built component ID → count in stockpile
Dictionary<string, float> OrdinanceStockpile // ordnance ID → count
List<Entity> FighterStockpile             // fighter entities built but not launched
Entity PlanetEntity                        // the planet body this colony is on
Dictionary<Entity, double> ColonyComponentDictionary // for bombardment targeting
```

`ColonyComponentDictionary` is the hook for orbital bombardment damage. It maps installation entities to their volume (used to calculate random hit probability). Currently empty in practice — not populated during colony construction.

---

## Population Processor

`PopulationProcessor` runs monthly (`RunFrequency = TimeSpan.FromDays(30)`).

`GrowPopulation(colony)`:
1. Gets `popSupportValue = ComponentInstancesDB.GetPopulationSupportValue()` — total support from installed infrastructure.
2. For each species in `Population`:
   - If `species.ColonyCost(planet) > 0` (hostile environment — needs support):
     - `maxPopulation = popSupportValue / needsSupport / colonyCost`
     - If current pop > max: population decays at −50.0% growth rate (very harsh).
     - If current pop ≤ max: growth rate = `20 / (pop ^ (1/3))`, capped at 10%.
   - If `colonyCost == 0` (species is native to this environment): uncapped growth, same formula.
3. Publishes `EventType.PopulationChanged` event.

**Morale (M1, 2026-06-29):** if the colony has a `ColonyMoraleDB`, `GrowPopulation` now recomputes morale from the worst resident-species `ColonyCost` (conditions) and `totalPop / capacity` (overcrowding, only on support-capped hostile worlds), then adds `ColonyMoraleDB.MigrationRate(morale)` to the growth rate at the two former `@todo: external factors` hooks. Morale < 50 → emigration; > 50 → immigration; 50 → no change. The math is pure/static on `ColonyMoraleDB` (`ComputeMorale`, `MigrationRate`) and unit-tested in `MoraleTests`. Guarded by `TryGetDataBlob` so a colony without the blob grows as before. Roadmap (jobs/unemployment, hard people-draw, money+tax, energy+food): `docs/MORALE-AND-POPULATION-DESIGN.md`.

**Current gaps (remaining after M1):**
- No jobs/unemployment input yet (M2); carrying capacity still a single lump (housing/jobs/food not split)
- Population is not yet a *drawable* resource — crew/officers/scientists/army still spawn free (M3)
- No money/tax wiring (M4) or energy/food (M5)
- No radiation effect on growth
- The `−50.0` over-hard-cap die-off rate is still a placeholder, not a formula

---

## Life Support and Carrying Capacity

`ColonyLifeSupportDB.MaxPopulation` = total population support value from all installed infrastructure components that have a `PopulationSupportAtbDB` component attribute.

`ReCalcMaxPopulation()` in `PopulationProcessor` recomputes this from `ComponentInstancesDB.GetPopulationSupportValue()`.

`Species.ColonyCost(planet)` — returns a floating-point cost multiplier based on atmosphere compatibility, temperature, gravity, etc. Higher cost = more life support infrastructure needed per capita. Zero = native planet, no support needed.

---

## Installation System

**Installations are components, not a separate registry.** A colony's installations are `ComponentInstance`s in its `ComponentInstancesDB`, added via `colonyEntity.AddComponent(design, count)`. Query them by ability with `componentInstances.TryGetComponentsByAttribute<TAtb>(...)` or via `DesignsAndComponentCount`.

`InstallationsDB` (in `Industry/InstallationsDB.cs`) *looks* like the installation store — `Dictionary<string,float> Installations`, `WorkingInstallations`, `EmploymentList` — but it is **dead code**: never attached to any colony, no `[JsonProperty]` fields, its `ConstructJob` lists commented out. Treat it as abandoned. Do not build on it; do not "fill in" the UI against it.

**The installations UI is doubly broken.** `PlanetaryWindow.RenderInstallations()` not only has an empty body — its tab is gated on `HasDataBlob<InstallationsDB>()` (`PlanetaryWindow.cs:107`), which is always false, so the tab never appears. The correct fix renders from `ComponentInstancesDB` (reuse the existing `ComponentInstancesDBDisplay`). See `docs/aurora/PLANETARY-INFRASTRUCTURE.md` §6.

---

## Colony Hex Map (DevBranch — New)

`ColonyHexMapDB` gives each colony a hex-tile grid — the same spatial layout pattern used in games like Civilization. This is significant for ground combat: it establishes that the colony ALREADY has a spatial model. Ground units, fortifications, and terrain could all live on hex tiles.

Key fields:
```csharp
MaxRadius     int  — how many rings of hexes are available (driven by admin building size)
CurrentRadius int  — actively used radius
HexTiles      Dictionary<HexCoordinate, HexTile>  — the actual map
```

`UpdateMaxRadius(officeSpace)` — formula: `sqrt(officeSpace / 100)` rings. So a larger administration center expands the colony's spatial footprint.

**Phase 4 note:** Before designing a ground combat spatial model from scratch, study `ColonyHexMapDB` carefully. It may already be the right substrate for placing ground units and resolving combat by tile.

---

## Ground Combat Hook Points

The colony system is the target for all ground combat. Key integration points:

| What | Where | Status |
|------|-------|--------|
| Orbital bombardment damage to population | `DamageProcessor.cs` lines ~101–181 | Commented out |
| Orbital bombardment damage to installations | Same block | Commented out |
| `ColonyComponentDictionary` for randomized targeting | `ColonyInfoDB.cs` | Exists but never populated |
| Ground unit garrison attachment | *(not yet)* | New `GroundUnitDB` needed |
| Active combat state | *(not yet)* | New `GroundCombatDB` needed |

---

## Gotchas

1. **`ColonyComponentDictionary` is never populated.** `ColonyFactory.CreateColony()` constructs the colony entity but never fills `ColonyComponentDictionary`. The bombardment damage code that reads it would always find an empty dictionary. Fix this when implementing orbital bombardment.

2. **`PopulationSupportValue` is computed from `ComponentInstancesDB`, not `InstallationsDB`.** Installations that provide population support must be physical components attached via `ComponentInstancesDB` (with `PopulationSupportAtbDB`), not just entries in `InstallationsDB.Installations`. This is a design subtlety — track it when adding new installation types.

3. **PopulationProcessor runs on entities with `ColonyInfoDB`, not `InstallationsDB`.** A body with installations but no `ColonyInfoDB` (bare infrastructure, no population) will not be processed by `PopulationProcessor`.

4. **Colony entities live in a `StarSystem` manager like any other entity.** They are not sub-objects of the planet — they are sibling entities at the same level, linked to a planet via `ColonyInfoDB.PlanetEntity`.
