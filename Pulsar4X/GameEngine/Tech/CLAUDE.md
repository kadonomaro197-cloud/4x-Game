# Research & Tech Subsystem — Developer Reference

**What it does:** Manages technology research. Colonies with research labs generate "research points" over time. Scientists assigned to a colony multiply those points and steer them toward specific technologies. When a tech accumulates enough points, it unlocks — changing formulas, enabling new components, raising caps.

Think of it like a nuclear power plant's R&D department: the labs are the staff producing analysis, the scientist is the department head multiplying their output through expertise, and the tech tree is the list of upgrades to pursue.

**Why it matters for ground combat:** Ground combat units are unlocked by tech. The tech tree includes a Ground Combat category. Scientist bonuses (which currently work for space systems) will apply to ground unit research as well — we get this for free.

---

## Files

| File | Role |
|------|------|
| `ResearcherDB.cs` | **The LIVE per-lab research DataBlob.** Spawned onto its own entity when a research-lab component is installed (see `ResearchPointsAtbDB.OnComponentInstallation`). Holds `PointsPerDay` (a `ModifiableValue<int>` — base output plus all folded modifiers), `CostPerDay` (`ModifiableValue<decimal>` — daily money to run it), `FundingLevel` (0–5, the player's speed-vs-cost lever), `BonusCategories`, `ScientistId` (the assigned scientist entity, `-1` = none), `LocationId` (the colony the lab sits at), and `TechQueue` (the ordered list of tech IDs to research). `ResearchProcessor` is keyed to THIS blob. |
| `EntityResearchDB.cs` | **Vestigial — not the live path.** Holds a `Labs` dict but nothing writes to it in the live research loop; the processor keys off `ResearcherDB`, not this. Left in place; do not build on it. |
| `ResearchProcessor.cs` | `IHotloopProcessor` running every **1 day**, keyed to `ResearcherDB` (`GetParameterType => typeof(ResearcherDB)`). Each day, for each researcher: peeks its `TechQueue`, computes points-per-day (funding level × category bonuses × scientist `BonusesDB` × government `ResearchMultiplier`), **pays a daily money cost** out of the faction wallet, then adds the points to the tech. Fires the `ResearchCompleted` event on a level-up. |
| `ResearchPointsAtbDB.cs` | Component-design attribute on a research-lab component. Holds `PointsPerEconTick` (base points), `CostPerDay` (base money cost) and `BonusCategory` (the specialty it gives a default 10% bonus to). Its `OnComponentInstallation` is the key wiring: installing the lab **spawns a new entity carrying a `ResearcherDB`** (plus an `OrderableDB` so it can take orders) — that spawned entity is what the processor actually runs on. |
| `Scientist.cs` | A `TeamObject` subclass — the **legacy** scientist-object path. Has `Bonuses`, `MaxLabs`, `AssignedLabs`, `ProjectQueue`. The live loop does NOT read these; the live scientist bonus comes from the scientist ENTITY's `BonusesDB` via `ResearcherDB.ScientistId`. Kept for the default start, where `ScientistId == -1` and none of this fires. |
| `TeamObject.cs` | Base class for scientists and commanders. Holds `TeamType` and name. |
| `Tech.cs` | Represents one technology. Has `Level`, `ResearchProgress`, `ResearchCost` (computed from `CostFormula` via NCalc), `Category`, `Unlocks` list, `MaxLevel`. |
| `Orders/` | The **7 player orders** that drive research: `AddTechToQueueOrder`, `RemoveTechFromQueueOrder`, `MoveUpInQueueOrder`, `MoveDownInQueueOrder`, `AssignScientistOrder`, `UnassignScientistOrder`, `FundingChangedOrder`. These are how the player fills a lab's `TechQueue`, assigns a scientist, and sets the funding level. |

---

## How Research Works (DoResearch in ResearchProcessor)

Each day, for each entity with a `ResearcherDB` (one per installed research lab):
1. Peek the lab's `TechQueue`. If it's empty, or the front tech isn't currently researchable, skip (a non-researchable one gets dequeued).
2. Compute the points to add: `researcherDB.PointsPerDay.GetValue()` — the base output with every modifier already folded in (funding level, category bonus, scientist `BonusesDB`) — then multiply by the government's `ResearchMultiplier()` (×1.0 at the default Mid regime, so no effect until a non-Mid government is set).
3. Check the faction can afford the lab's daily `CostPerDay`. If the faction wallet is short, the lab does no work that day (no points, no charge). Otherwise **pay the money** (`Money.AddExpense`, `TransactionCategory.Research`).
4. `factionDataStore.AddTechPoints(tech, pointsToAdd)`. If that pushed the tech to a new level: dequeue it, sync any newly-unlocked `ProcessedMaterial`/`ComponentDesign` into `IndustryDesigns`, and fire the `ResearchCompleted` event.
5. Once a month (`Day == 1`), if a scientist entity is assigned (`ScientistId >= 0`), grow that scientist's experience and re-fold the point modifiers so the higher competence bites immediately (see below).

**How points get their modifiers (`RefreshPointModifiers`):** the funding level is a flat multiplier (level 0–5 → ×0–×5, `GetFundingPointModifier`); `ResearcherDB.BonusCategories` adds a percentage bump when the researched tech's category matches; and if a scientist entity is assigned, its `BonusesDB` bonuses matching the tech category are folded in (flat or percentage). Note the live path reads the scientist ENTITY's `BonusesDB` — NOT the legacy `Scientist.Bonuses` object.

**Funding level — the speed-vs-cost lever (`ResearcherDB.FundingLevel`, set by `FundingChangedOrder`):** 0 = off (no output, no cost); 1 = standard (×1 output, ×1 cost); rising to 5 = spared no expense (×5 output but ×22 cost). Output scales linearly, cost scales steeply — the player trades money for research speed. Costs are paid daily from the faction wallet in `DoResearch`.

**Scientist experience over time (2026-07-12, Exploration X.0-3).** `DoResearch` now grows an ASSIGNED scientist ENTITY's `CommanderDB.Experience` monthly (`ExperienceGainPerMonth` = 2, on `StarSysDateTime.Day == 1`) via `ResearchProcessor.GrowScientistExperience`, which stamps a growing `"Research Experience"` bonus on the scientist's `BonusesDB` in the category they research (capped at `ExperienceCap` — the school-set ceiling from the research academy, `People/CLAUDE.md`). `RefreshPointModifiers` is re-folded so the higher competence bites immediately. This is the entity-`BonusesDB` scientist path (`ResearcherDB.ScientistId` set by `AssignScientistOrder`), NOT the legacy `Scientist`-object path — so it is **byte-identical for the default start** (legacy scientists leave `ScientistId == -1`; nothing grows). Gauge: `ResearchExperienceTests`.

---

## Tech Data Model

```csharp
Tech
  Level             int                          — current level (0 = not started)
  MaxLevel          int                          — caps at this
  ResearchProgress  int                          — accumulated points toward next level
  ResearchCost      int                          — computed: CostFormula(Level) via NCalc
  Category          string                       — maps to a tech category (e.g. "ground-combat")
  Unlocks           Dictionary<int, List<string>> — items unlocked at each level (key = tech level)
```

**`Unlocks` is `Dictionary<int, List<string>>`.** The key is the tech level at which items unlock; the value is a list of IDs (material IDs, component IDs, etc.). `FactionDataStore.IncrementTechLevel()` iterates the list for the new level and calls `Unlock()` on each ID, moving it from `LockedCargoGoods` to `CargoGoods`.

**IndustryDesigns sync (fixed — this branch):** When a tech levels up and its `Unlocks` include `ProcessedMaterial` IDs, `ResearchProcessor.DoResearch()` now iterates `tech.Unlocks[tech.Level]` and syncs any newly available materials into `factionInfoDB.IndustryDesigns`. Previously, only `tech.Design` (a `ComponentDesign`) was synced, so material-unlocking techs were silently broken — the material reached `CargoGoods` but no colony could queue it for production.

Costs are formulas (NCalc), not flat values — e.g., `"1000 * (Level + 1)^2"`. This means each successive level costs more. Same NCalc engine used by component design.

Tech data lives in `Pulsar4X/GameData/basemod/TemplateFiles/techs.json` and `techCategories.json` (copied to the live `Mods` folder in AppData at client build).

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Research labs generate points | `ResearchPointsAtbDB` → spawns a `ResearcherDB` per lab | code exists & wired |
| Funding-level speed/cost lever | `ResearcherDB.FundingLevel` (0–5) → `RefreshPointModifiers`/`RefreshCostModifiers`; daily money paid in `DoResearch` | code exists & wired |
| Scientists multiply lab output | scientist ENTITY's `BonusesDB` folded in `RefreshPointModifiers` (via `ResearcherDB.ScientistId`) — NOT the legacy `Scientist.Bonuses` object | code exists & wired (default start uses no assigned scientist, so unexercised there) |
| Scientists assigned to projects | live queue is `ResearcherDB.TechQueue`, filled by `AddTechToQueueOrder` (the 7 Orders) — NOT `Scientist.ProjectQueue` | code exists & wired |
| Government modulates research pace | `GovernmentTools.Of(faction).ResearchMultiplier()` applied in `DoResearch` (×1.0 at default Mid) | code exists & wired |
| Multi-level technologies | `Tech.Level` / `MaxLevel`, cost recalculated per level | code exists & wired |
| Tech categories (9 in Aurora) | `Tech.Category` + `techCategories.json` | code exists & wired — check JSON for category list |
| Unlocks trigger new components/materials | `Tech.Unlocks`; `DoResearch` syncs unlocked materials + designs into `IndustryDesigns` | code exists & wired |
| Scientist in-field 4× bonus (Aurora: field research bonus) | Not found | ⚠️ not built |
| Prototype construction | Not found | ❌ not built |

**Verdict: research is substantially built and wired.** Labs spawn researchers, the funding lever and money cost are wired, category and scientist-entity bonuses fold into the point total, the NCalc formula system handles cost scaling, and unlocks flow through to production. Runtime is unverified here (CI can't run the client) — the engine paths exist and are wired, but the play-through gauge is a local run. The main unbuilt pieces are the in-field scientist bonus and prototypes — neither blocks ground combat.

---

## Phase 4 Relevance

A "Ground Combat" tech category must exist in `techCategories.json` for ground unit upgrades to unlock. Check before building ground unit designs — the category ID is the string key `Tech.Category` and the live `ResearcherDB.BonusCategories` (and a scientist entity's `BonusesDB.FilterId`) use. If the category doesn't exist yet, add it to the JSON file (no code change needed).

Ground unit weapons, armor, and sensor components will all be researchable exactly like ship components — same NCalc cost formula in the tech JSON, same unlock mechanism. We get this for free.
