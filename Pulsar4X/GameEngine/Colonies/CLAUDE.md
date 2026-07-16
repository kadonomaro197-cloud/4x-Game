# Colonies — Subsystem Reference

Population, colony lifecycle, life support. Lives in `GameEngine/Colonies/`.

> **Before building colony progression, read `docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md`** — the developer's captured vision for the growth ladder every infrastructure site (player + NPC) climbs: **Outpost** (the only automatable tier) → **Colony** (has flavors) → **World** → **Minor → Hub → Major → Capitol**, each step cost↑/yield↑ *if done right*, all scaling. Captured 2026-06-28; a vision, not a build spec (open questions listed there).

---

## File Map

| File | Purpose |
|------|---------|
| `ColonyInfoDB.cs` | Core colony DataBlob: population per species, stockpiles, parent planet reference, component dictionary for bombardment. |
| `ColonyLifeSupportDB.cs` | **DEAD CODE — do not build on it.** Has a single `MaxPopulation` field (no `[JsonProperty]`, so it wouldn't even survive save/load) and is **never attached to any colony** (not in either `ColonyFactory` path). `PopulationProcessor.ReCalcMaxPopulation()` would write to it, but that method is **never called** — its only registration (`RecalcProcessor.cs:32`) is commented out. Live carrying capacity is `ComponentInstancesDB.GetPopulationSupportValue()` (read directly by `GrowPopulation`); the live shortage gauge is `ColonySustenanceDB`. |
| `ColonyMoraleDB.cs` | **NEW (M1, 2026-06-29)** Morale (0–100, 50 = neutral) — the level-control valve on the population "tank". Recalced each population tick from conditions + overcrowding (M1) + employment + housing comfort (M2); drives migration. All weights are named coefficients (government-ready). Host-agnostic (station-attachable later). See `docs/society/MORALE-AND-POPULATION-DESIGN.md`. |
| `LegitimacyDB.cs` | **NEW (#31, 2026-07-01)** The regime's health bar for ONE province (0–100) — the INTERNAL-politics counterpart to morale, and the locked "LOCAL not empire-wide" decision (so the whole empire can't rebel at once). DERIVED each cycle (like morale, not a parallel resource): v1 baseline = the province's hosts' average morale, adjusted by demand track-record + war outcomes + governor competence + connectivity (each neutral when unwired). `LegitimacyDB.IsCollapsing(v)` (< `CollapseThreshold` 20) is the REBELLION trigger (#38 hook). Pure math (`ComputeLegitimacy` + `LegitimacyInputs`), named coefficients (government-ready). **LIVE as of 2026-07-01:** attached to every colony (`ColonyFactory`, both paths) + station (`StationFactory`), recomputed monthly by `LegitimacyProcessor`. Tests: `LegitimacyTests` (math) + `LegitimacyProcessorTests` (wiring). See `docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`. |
| `LegitimacyProcessor.cs` | **NEW (#31 wiring, 2026-07-01)** `IHotloopProcessor` keyed on **`LegitimacyDB`** (its own blob — NOT ColonyInfoDB/ColonyMoraleDB, the one-hotloop-per-blob rule L9), monthly. Recomputes each province's `LegitimacyDB.Legitimacy` from the sibling `ColonyMoraleDB.Morale` (v1 morale-only driver). Host-agnostic (colonies + stations). Defensive (never throws — L4). Mirrors `ColonyEconomyProcessor`'s "keyed on its own blob, reads a sibling, runs colony-side" shape. **Now reads the WAR term too (2026-07-01):** `WarTermFor(province)` looks up the owning faction's `DiplomacyDB.IsAtWarWithAnyone()` and, if at war, feeds the government's `WarMoraleFactor()` as legitimacy's `WarOutcome` — a pacifist regime's provinces bleed loyalty during war, a militarist's take pride (the casus-belli militarism gate, live on the province; no `GovernmentDB` → neutral Mid default). **v1: morale + war-standing driver; the demand/governor/connectivity inputs + government re-weighting wire in later.** Gauge: `LegitimacyProcessorTests` (incl. `War_TaxesLegitimacy_ByMilitarism`). |
| `ColonySustenanceDB.cs` + `SustenanceProcessor.cs` | **NEW (M5b, #29, 2026-07-01)** Power/food shortage gauges + the monthly processor that computes them (demand = pop × per-capita coefficient, vs supply). Feeds morale (`PopulationProcessor` reads `PowerShortage`/`FoodShortage` into `MoraleInputs`) + a starvation death term. **NEUTRAL-WHEN-ABSENT: the per-capita demand coefficients default to 0, so every shortage computes to 0 until calibrated on the local build** — the deliberate guard against the "default deficit tanks every colony" trap. Processor keyed on its own blob (`ColonySustenanceDB`), host-agnostic, defensive. Public `SetDemand(power, food)` (2026-07-02) lets another assembly (the client's DevTools "Society levers") switch the wiring on for a play-test — the demand fields are `internal set`, so a cross-assembly caller needs this. **FOOD SUPPLY IS NOW REAL (M5c, 2026-07-14):** `SustenanceProcessor` reads food supply from the host's installed `FoodProductionAtbDB` components (`ComponentInstancesDB.GetTotalFoodOutput()`, health-scaled) — it was hardcoded 0, which made ANY food demand an unwinnable 100% shortage → a permanent −40 morale floor on hostile-world colonies. A colony that builds enough food output now ends its shortage. Tests: `SustenanceTests` (math) + `FoodProductionTests` (the supply side end-to-end). |
| `FoodProductionAtbDB.cs` | **NEW (M5c food SUPPLY, 2026-07-14)** The component attribute that makes food a buildable — the supply half of the sustenance loop. Two dials: `FoodOutput` (units/day — summed vs demand to end starvation) and `FoodQuality` (≈0.5 subsistence … 3.0 gourmet; 1.0 = adequate baseline; above 1.0 an active MORALE BONUS, EXPONENTIALLY expensive to build via the template's cubic-quality cost). Summed on demand (the `HousingAtbDB` pattern) via `ComponentInstancesDBExtensions.GetTotalFoodOutput` (SustenanceProcessor supply) + `GetAverageFoodQuality` (output-weighted mean → the morale bonus). Host-agnostic (colony or station). Cradle-to-grave: designed in the component designer (**Civic ▸ Development** door) → built from materials (stainless-steel/plastic/water/aluminium) at a colony → produces food + lifts morale → destroyed (bombardment) drops the supply and morale falls. Base mod: `food-production` template (`installations.json`, dials Food Output / Food Quality / Automation) + `default-design-agri-complex` (quality 1.0) + `default-design-hydroponics-arcology` (quality 2.5) (`componentDesigns.json`); DevTest player + UMF colonies + Kithrin's Titan station carry one (StartingItems + ComponentDesigns + a start installation). Tests: `FoodProductionTests`. |
| `RebellionDB.cs` | **NEW (#38 foundation, 2026-07-01)** A province's REBELLION state — legitimacy collapse made a PROCESS you can fight, not an instant loss. When `LegitimacyProcessor` sees legitimacy fall below `LegitimacyDB.CollapseThreshold` it BEGINS a rebellion (`IsRebelling` + a `ReactionWindowEnds` clock); restoring legitimacy to `RecoveryThreshold` (35, hysteresis above the collapse line) QUELLS it. `WindowExpired(now)` is the cue the (later) resolution slice reads to secede/defect. Attached to every province at factory time (no dynamic blob-adds). **v1: begin/quell + the window clock; the window-expiry resolution (secession→new-faction, espionage defection) and the ground-combat suppress wire are the follow-on #38 slices — no ownership change yet.** Tests: `RebellionTests`. See `docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md`. |
| `EmploymentAtbDB.cs` | **NEW (M2)** Component attribute: jobs (worker slots) an installation provides. Summed via `ComponentInstancesDBExtensions.GetTotalJobs`. Morale: jobs-vs-pop is two-sided (unemployment debuff / full-employment buff). |
| `HousingAtbDB.cs` | **NEW (M2)** Component attribute: housing **comfort** (a morale bonus) — the quality "tier" above bare life-support capacity (`PopulationSupportAtbDB` keeps people alive; this keeps them content). Summed via `GetHousingComfort`. |
| `ColonyEconomyDB.cs` | **NEW (M4)** The tax lever: a player-set `TaxRate` (0..1, default 10%). Pure `MonthlyTaxIncome(pop, taxRate, morale)` (income scales with morale — a happy colony pays more). Read by `PopulationProcessor` as a morale penalty; read by `ColonyEconomyProcessor` to bill income. Coefficients are named (government-ready). |
| `ColonyEconomyProcessor.cs` | **NEW (M4)** `IHotloopProcessor` keyed on **`ColonyEconomyDB`** (NOT ColonyInfoDB — PopulationProcessor owns that; one hotloop per blob type). Monthly: bills colony tax into the owning faction's `Ledger` (`TransactionCategory.ColonyTax`). Runs colony-side because MasterTimePulse never iterates the GlobalManager where factions live. Finally plugs the colony economy into faction money (only research moved funds before). |
| `ColonyManpowerDB.cs` | **NEW (M3 sub-slice 1)** People as a finite resource. Two pools derived from population: **bulk** workforce (`pop × WorkforceFraction`, crew/workers) and scarce **talent** (`pop × TalentFraction`, officers/scientists/governors). Tracks `CommittedBulk`/`CommittedTalent`; `Available = pool − committed`. `ResolveConstructionCrew(available, crewReq, policy)` is the pure build decision (Block/BuildUnderstaffed). Also fixes M2's employment denominator (jobs vs workforce, not raw pop). |
| `ManpowerTools.cs` | **NEW (M3 sub-slice 2b, #27)** The crew ENFORCEMENT bridge — turns the pool math into a real gate on **ship** construction. `ResolveBuild(host, crewReq)` (reads the host's `ColonyManpowerDB` + owning government's `CrewPolicy()`), `CommitCrew(host, n)`, `ReleaseCrew(sourceColony, n)`. **INERT WHEN ABSENT** (a host with no pool — a station — always builds). Wired at the ship lifecycle: the gate in `IndustryTools.ConstructStuff` (blocks a ship you can't crew, before resources are spent); the commit at `ShipDesign.OnConstructionComplete` (both the direct-launch and the launch-complex-queue paths, since a colony with a `LaunchComplexDB` — e.g. Earth — queues the ship instead of spawning it); the `ShipInfoDB.CrewSourceColonyId` provenance stamp at each real creation point (`OnConstructionComplete` for direct, `LaunchComplexProcessor.TryLaunchShip` for the launch queue); and the release in `ShipFactory.DestroyShip` (frees the source colony's crew). **Start-safe:** the start fleet spawns via `ShipFactory.CreateShip` directly (bypasses the queue → provenance stays -1 → destroy is a no-op), and start pop is billions. Feel + the parked casualties-shrink-population sting are local PC-tests. **TALENT HALF WIRED (2026-07-11) — the scarce-pool draw for Enhancers ⚙6.2 Unit Caliber:** `HasTalentToBuild`/`CommitTalent`/`ReleaseTalent` mirror the crew half against the scarce `TalentPool` (officers). A ship's `ShipDesign.TalentReq` (summed crew of its caliber modules) is drawn from talent instead of bulk at the same three sites, so an elite hull ties up scarce veterans and can't be spammed. HARD wall (no conscript-understaffed — you can't fake a veteran crew). Inert/byte-identical for any non-caliber ship (TalentReq 0). Gauge: `ManpowerTests` (talent commit/release + gate) + `ShipCaliberTests`. |
| `ColonyBonusesDB.cs` | Modifier DataBlob for production/research/mining bonuses on a colony. |
| `ColonizeableDB.cs` | Marker DataBlob on planets that can be colonized. |
| `ColonyFactory.cs` | Two creation paths, both attach the same blob set: **`CreateFromBlueprint()`** (`ColonyFactory.cs:26`) is the **real New-Game path** (builds the start colony from JSON); `CreateColony()` (`ColonyFactory.cs:188`) is the in-game "found a colony" order path. |
| `CreateColonyOrder.cs` | Player order to found a colony on a planet. |
| `PopulationProcessor.cs` | `IHotloopProcessor` (monthly). Runs `GrowPopulation()` for each colony. |
| `ColonyHexMapDB.cs` | **Built and wired.** DataBlob: hex-grid layout for a colony. `MaxRadius` scales with admin building office space. `HexTiles` — dict of coordinate → tile. |
| `ColonyHexMapProcessor.cs` | **Built and wired.** An `IInstanceProcessor`, but it is **not driven by the instance-processor queue** — it's invoked by **direct static call** to `ColonyHexMapProcessor.ForceUpdateColonyHexMap(colony)` from `AdminSpaceProcessor.cs:21` (when admin space recalcs) and from the client `ColonyHexMapWindow.cs:68`. Rebuilds the hex map size from the colony's admin-building office space. (Runtime behavior unverified — CI can't run the client.) |
| `HexTile.cs` | One hex cell on the colony map. Holds terrain type, what's built there. |
| `HexCoordinate.cs` | Cube-coordinate system for the hex grid. |

---

## Colony Entity DataBlob Composition

A fully-formed colony entity typically holds:

**Both `ColonyFactory` paths — `CreateFromBlueprint()` (the New-Game/JSON path) and `CreateColony()` (the found-a-colony order) — attach the SAME blob set directly** (verified in `ColonyFactory.cs:84-99` and `:205-220`):

| DataBlob | Purpose |
|----------|---------|
| `ColonyInfoDB` | Core: population, stockpiles, parent planet |
| `ColonyMoraleDB` | Morale valve (M1) — see file map |
| `LegitimacyDB` | Province regime health (internal politics) — see file map |
| `RebellionDB` | Rebellion state (legitimacy-collapse process) — see file map |
| `ColonySustenanceDB` | Power/food shortage gauges — see file map |
| `ColonyManpowerDB` | People-as-a-resource pools (crew/talent) — see file map |
| `ColonyEconomyDB` | Tax lever — see file map |
| `ColonyBonusesDB` | Modifier tracking |
| `MiningDB` | Mining setup |
| `OrderableDB` | Marks the colony able to receive orders |
| `MassVolumeDB` | Mass / volume |
| `CargoStorageDB` | Mineral and goods storage |
| `PositionDB` | Location (on the planet surface) |
| `TeamsHousedDB` | Scientists / commanders housed here |
| `ComponentInstancesDB` | **Installations live here** (added via `AddComponent()`) |
| `InfrastructureDB` | Capacity summed from installations as they're added |

> **NOT attached by `ColonyFactory`** (corrects the earlier recon):
> - **`InstallationsDB` is never attached to a colony** — it is vestigial/abandoned (no `[JsonProperty]` fields; only dead-code references). Installations are **components** in `ComponentInstancesDB` (see `DefaultStartFactory.cs:212-225`: `AddComponent(mineDesign)`, `AddComponent(facEntity)`, …).
> - Capability blobs such as `IndustryAbilityDB` are **granted by installed components** carrying the matching `*Atb` attribute, not added by the factory.
> - **`ColonyLifeSupportDB` is never attached** (dead code — see file map). Carrying capacity is read live from `ComponentInstancesDB.GetPopulationSupportValue()`, not stored on a colony blob.
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

**Morale (M1, 2026-06-29):** if the colony has a `ColonyMoraleDB`, `GrowPopulation` now recomputes morale from the worst resident-species `ColonyCost` (conditions) and `totalPop / capacity` (overcrowding, only on support-capped hostile worlds), then adds `ColonyMoraleDB.MigrationRate(morale)` to the growth rate at the two former `@todo: external factors` hooks. Morale < 50 → emigration; > 50 → immigration; 50 → no change. The math is pure/static on `ColonyMoraleDB` (`ComputeMorale`, `MigrationRate`) and unit-tested in `MoraleTests`. Guarded by `TryGetDataBlob` so a colony without the blob grows as before. Roadmap (jobs/unemployment, hard people-draw, money+tax, energy+food): `docs/society/MORALE-AND-POPULATION-DESIGN.md`.

**Roadmap status (M2–M5 are now BUILT; verify each blob in the file map above):**
- Jobs/unemployment + housing comfort — **built** (M2): `EmploymentAtbDB`, `HousingAtbDB`, summed and read into morale.
- Population as a *drawable* resource — **built** (M3): `ColonyManpowerDB` + `ManpowerTools` gate ship construction on crew (and elite hulls on talent). Some pieces are calibration-inert; see the file map.
- Money/tax — **built and wired** (M4): `ColonyEconomyDB` + `ColonyEconomyProcessor` bill tax into the faction ledger.
- Energy/food — **built; food SUPPLY now real** (M5/M5c): `ColonySustenanceDB` + `SustenanceProcessor` compute shortages. Per-capita demand still defaults to 0 (neutral-when-absent), set per-scenario via the faction `strain` node. The food SUPPLY side is built (2026-07-14): `FoodProductionAtbDB` food buildings feed `SustenanceProcessor` (was hardcoded 0). Power supply reads a host `EnergyGenAbilityDB`; a power-supply component is the remaining gap.

**Still open:**
- No radiation effect on growth
- The `−50.0` over-hard-cap die-off rate is still a placeholder, not a formula
- Runtime feel of the M2–M5 wiring is unverified (CI can't run the client)

---

## Life Support and Carrying Capacity

Carrying capacity is the total population support value from all installed infrastructure components that have a `PopulationSupportAtbDB` component attribute. It is read **live, on demand** by `GrowPopulation` via `ComponentInstancesDB.GetPopulationSupportValue()` — it is **not stored** on a colony blob.

> **Do NOT use `ColonyLifeSupportDB` for this.** That blob (and `PopulationProcessor.ReCalcMaxPopulation()`, which would write its `MaxPopulation`) is **dead code**: the blob is never attached to a colony and `ReCalcMaxPopulation()` is never called (its `RecalcProcessor.cs:32` registration is commented out). See the file map.

`Species.ColonyCost(planet)` — returns a floating-point cost multiplier based on atmosphere compatibility, temperature, gravity, etc. Higher cost = more life support infrastructure needed per capita. Zero = native planet, no support needed.

---

## Installation System

**Installations are components, not a separate registry.** A colony's installations are `ComponentInstance`s in its `ComponentInstancesDB`, added via `colonyEntity.AddComponent(design, count)`. Query them by ability with `componentInstances.TryGetComponentsByAttribute<TAtb>(...)` or via `DesignsAndComponentCount`.

`InstallationsDB` (in `Industry/InstallationsDB.cs`) *looks* like the installation store — `Dictionary<string,float> Installations`, `WorkingInstallations`, `EmploymentList` — but it is **dead code**: never attached to any colony, no `[JsonProperty]` fields, its `ConstructJob` lists commented out. Treat it as abandoned. Do not build on it; do not "fill in" the UI against it.

**The installations UI is FIXED (was doubly broken).** The old broken version — empty body, tab gated on the always-false `HasDataBlob<InstallationsDB>()` — now survives only in the dead `PlanetaryWindow.old.cs` (not compiled into the live path). The live `PlanetaryWindow.cs` gates the Installations tab on `HasDataBlob<ComponentInstancesDB>()` (`:102`) and renders from `componentsDB` in `RenderInstallations()` (`:215`). (Compiles under CI's `build-client`; runtime appearance unverified — CI can't run the client.) See root gotcha #4 and `docs/aurora/PLANETARY-INFRASTRUCTURE.md` §6.

---

## Colony Hex Map (built and wired)

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
| Orbital bombardment damage to population | `DamageComplex/DamageProcessor.cs` `OnColonyDamage()` (routed from `OnTakingDamage()` at `:54`) | **Wired** (beam hits reach it; missiles deliver via `MissileImpactProcessor`). Energy divisor is a placeholder. See `Damage/CLAUDE.md`. |
| Orbital bombardment damage to installations | Same `OnColonyDamage()` (`ApplyInstallationDamage`) | **Wired** (shared with the station damage path) |
| Space→ground garrison softening | `OnColonyDamage()` → `ApplyGroundBombardment(...)` | **Wired** (2026-07-06) — softens the defending garrison; damage coefficient is a flagged placeholder |
| `ColonyComponentDictionary` for randomized targeting | `ColonyInfoDB.cs` | Exists but never populated |
| Ground unit garrison / active combat state | `GameEngine/GroundCombat/` | **Built** — ground combat is its own subsystem now (`GroundForcesDB` etc.); see `GroundCombat/CLAUDE.md`. Most of it ships gated/uncalibrated — runtime unverified. |

---

## Gotchas

1. **`ColonyComponentDictionary` is never populated.** `ColonyFactory.CreateColony()` constructs the colony entity but never fills `ColonyComponentDictionary`. The bombardment damage code that reads it would always find an empty dictionary. Fix this when implementing orbital bombardment.

2. **`PopulationSupportValue` is computed from `ComponentInstancesDB`, not `InstallationsDB`.** Installations that provide population support must be physical components attached via `ComponentInstancesDB` (with `PopulationSupportAtbDB`), not just entries in `InstallationsDB.Installations`. This is a design subtlety — track it when adding new installation types.

3. **PopulationProcessor runs on entities with `ColonyInfoDB`, not `InstallationsDB`.** A body with installations but no `ColonyInfoDB` (bare infrastructure, no population) will not be processed by `PopulationProcessor`.

4. **Colony entities live in a `StarSystem` manager like any other entity.** They are not sub-objects of the planet — they are sibling entities at the same level, linked to a planet via `ColonyInfoDB.PlanetEntity`.
