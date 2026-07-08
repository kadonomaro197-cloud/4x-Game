# 07 — Research & Unlocks: what gates and unlocks what a player may design

**Scope:** the RESEARCH/TECH layer and the unlock/availability plumbing — how a tech makes a template or design available to a faction, how researched tech *scales* a component's stats, and whether that gating is uniform across host types (ship / colony / station / ground / fighter). Companion to `01-DESIGNER-UIS.md` and `02-DESIGNABLE-TYPES.md`.

---

## The one-paragraph answer

The unlock/availability layer is **completely host-uniform**. There is ONE per-faction store (`FactionDataStore`); when a tech unlocks a radar template, the template lands in that faction's single `ComponentTemplates` dictionary and is designable — full stop, with no per-host copy and no host tag on the unlock. **Tech gating decides *whether* you may design a thing; it never decides *where* the resulting component may be mounted.** The "designers aren't universal" problem the developer found lives entirely in a *different* field — each template's `ComponentMountType` flag list in the JSON — which is **orthogonal** to research. Research does not help or hurt host-universality; it is uniform, and it is not the source of the mount-type entanglement.

---

## How a tech unlocks a designable — the data flow

There are **two kinds of Tech**, and the distinction is the crux of the whole system.

### Kind 1 — authored techs (the tech tree in `techs.json`)
Loaded from `GameData/basemod/TemplateFiles/techs.json` as `TechBlueprint`s. Each has: `UniqueID`, `Category`, `MaxLevel`, `DataFormula` (the value `TechData()` reads — see next section), `CostFormula` (NCalc, scales per level), and an optional `Unlocks` map of `level → [id, id, …]`.

The whole tree hangs off a **cost-0 root**, `tech-modern-technology` (`techs.json:483`), whose `Unlocks["1"]` list (`techs.json:490-527`) unlocks ~33 other techs at once — mining, engines, warheads, sensors, the two weapon-scale techs `tech-beam-range`/`tech-kinetic-yield`, factory/shipyard size caps, admin level, and the habitat-tolerance techs. So "research this one free tech" opens the starting tech set.

Flow when a tech levels up (`FactionDataStore.IncrementTechLevel`, `FactionDataStore.cs:187-208`):
1. `tech.Level++`, reset progress, recompute cost.
2. For each id in `tech.Unlocks[newLevel]`, call `Unlock(id)` (`FactionDataStore.cs:103-156`) — which *moves* the item from its `Locked*` store to the matching unlocked store (armor, cargo type, component template, industry type, tech, or cargo good = mineral/material/design).
3. If an unlocked id is itself a tech, its cost is recomputed (so a tech can unlock the next tech in a chain).

The daily research engine (`ResearchProcessor.DoResearch`, `ResearchProcessor.cs:58-149`) is what drives that level-up over time: it pulls the front tech off a lab's queue, checks `factionDataStore.IsResearchable(tech)` (`FactionDataStore.cs:176-180` — `Techs.ContainsKey && Level < MaxLevel`), pays the daily cost from the faction ledger, and calls `AddTechPoints` (`FactionDataStore.cs:210-225`) which calls `IncrementTechLevel` on rollover. On level-up it also syncs any newly-unlocked **materials** into `IndustryDesigns` (`ResearchProcessor.cs:128-135`) — the gotcha-#2 fix so a researched material is actually buildable.

### Kind 2 — auto-generated per-design "prototype" techs
This is the mechanism most people miss. **Designing a component *creates a Tech*.** In `ComponentDesigner.CreateDesign` (`ComponentDesigner.cs:130-148`) every new design mints a Tech:

```
UniqueID = "tech-" + design.UniqueID
Category = "tech-category-designs"
MaxLevel = 1
CostFormula = design.ResearchCostValue
Unlocks = { 1: [ design.UniqueID ] }
```

So a finished design is locked behind its *own* one-level research project. `CreateDesign` then branches (`ComponentDesigner.cs:206-215`):
- If `ResearchCostValue == 0` **or** the static flag `StartResearched == true` → the design-tech is set to max level, and the design is dropped straight into `IndustryDesigns` (buildable) + `CargoGoods` (unlocked).
- Otherwise → the design goes to `LockedCargoGoods`, and the player must research `tech-<designId>` before it can be built.

`ColonyFactory.CreateFromBlueprint` sets `ComponentDesigner.StartResearched = true` around the loop that builds the start colony's designs (`ColonyFactory.cs:48-53`), which is why starting components are buildable immediately.

**Net:** a template → (design step) → a per-design tech → (research) → the design becomes buildable. Research gates *designs* twice-over: the template must be unlocked to open the designer, and the finished design must be researched to build it.

---

## Is research itself a "designer"? — No.

The player does **not** design or author tech. The `ResearchWindow` (`Pulsar4X.Client/Interface/Windows/ResearchWindow.cs`) is a **queue + progress-bar picker**, not a designer:
- `DisplayTechs()` (`:421-`) lists the faction's researchable techs as progress bars; double-clicking one issues `AddTechToQueueOrder` to the selected lab (`:464-469`).
- It shows each tech's `Unlocks` for the next level as a tooltip (`:442-448`) — i.e. "what will this give me."
- There is no tech-tree editor, no branch-picking, no player-set formulas. Techs are fixed JSON data; the only player decision is *ordering the queue* and *funding level*.

(The in-game `ModFileEditing` blueprint editor *can* edit tech JSON — `FunctionEditWidget.cs` even offers a `TechData(techID)` inserter — but that is a modding tool, not gameplay.)

So: **research is a picker over a fixed tree, not a designer.** The only true "designers" are the component/ship/ordnance windows covered in `01-DESIGNER-UIS.md`; research merely gates them.

---

## TechData stat-scaling — one template, better components as you research

A template formula can call `TechData('tech-id')`; at evaluation the NCalc engine resolves it to that tech's current data value. The wiring:

- `ChainedExpression.cs:485-489` — the `"TechData"` NCalc function case: `args.Result = _factionDataStore.Techs[techID].TechDataFormula()`.
- `Tech.TechDataFormula()` (`Tech.cs:48-56`) — evaluates the tech's `DataFormula` with `[Level]` bound to the current level.
- Sibling `"TechLevel"` (`ChainedExpression.cs:492-497`) returns the raw integer level (defaults to 0 if the tech is missing — no crash).

Because the value is read live from the faction's `Techs[id].Level`, **the same template yields a stronger component every time the tech levels up** — no new template needed.

### JSON examples
| Template field | File:line | Formula | Backing tech `DataFormula` | Effect of research |
|---|---|---|---|---|
| Beam weapon **Range** ceiling | `weapons.json:38` | `MaxFormula: "TechData('tech-beam-range')"` | `tech-beam-range`: `10000 * Pow(2,[Level])` (`techs.json:551`) | Each level *doubles* the max range the design slider allows — "long range is earned." |
| Railgun **Kinetic-Energy-Per-Shot** ceiling | (per `WeaponScaleGateTests`) | `MaxFormula: "TechData('tech-kinetic-yield')"` | `tech-kinetic-yield`: `10000000 * Pow(2,[Level])` (`techs.json:563`) | Each level doubles the max kinetic yield — ship *or* ground mount (a per-TYPE tech, host-agnostic). |
| Engine attribute / efficiency bounds | `ShipComponentTests.cs:191,200-201` | `TechData('…guid…')` in AttributeFormula/Max/Min | authored engine techs | Higher engine tech raises the design's achievable efficiency band. |
| Fuel consumption | `ShipComponentTests.cs:224` | `TechData('…') * Pow(Ability(2), 2.25)` | authored tech | Tech scales the coefficient of a formula that also depends on other design properties. |

Two usage shapes:
1. **As a Max/Min bound** (most common): `TechData(...)` is the *ceiling* of a `GuiSelectionMaxMin` slider, so research widens the design envelope (`weapons.json:38` beam range). The starting value stays put; the *headroom* grows.
2. **As a tech-selection dropdown**: a property with `GuiHint.GuiTechSelectionList` builds a `GuidDictionary` where each option's value is that tech's `TechDataFormula()` (`ComponentDesignProperty.cs:62-71`); picking an option calls `SetValueFromGuidList` → rewrites the formula to `TechData('<guid>')` (`ComponentDesignProperty.cs:167-171`). This is how a design "selects which tech generation of X to use."

---

## Availability rule — StartingItems vs research, and the code that enforces it

`FactionDataStore` is a **two-store (Locked / unlocked) model**, one instance per faction (`FactionInfoDB.Data`). Constructed with everything Locked (`FactionDataStore.cs:63-84`): `LockedArmor`, `LockedCargoTypes`, `LockedComponentTemplates`, `LockedIndustryTypes`, `LockedTechs`, `LockedCargoGoods` all seeded from the mod store; the unlocked twins start empty. `Unlock(id)` (`:103-156`) is the only door between them.

**Two independent unlock ENDS (do not conflate — gotcha #10):**
1. **Materials / templates / techs** come unlocked either by being in a colony's `StartingItems` or by a researched tech's `Unlocks`. `ColonyFactory.CreateFromBlueprint` (`ColonyFactory.cs:30-45`) loops `colonyBlueprint.StartingItems`, calling `Unlock(id)`, and — if the id is a tech — `IncrementTechLevel(id)`, and — if a material — pushing it into `IndustryDesigns`.
2. **A component TEMPLATE is available to DESIGN iff it is in the unlocked `ComponentTemplates` dict.** Enforced at the *only* two design entry points:
   - `ComponentDesignFromJson.Create(…blueprint…)` — `ComponentDesignFromJson.cs:18-21`: `if(!factionDataStore.ComponentTemplates.ContainsKey(TemplateId)) throw`.
   - `ComponentDesignFromJson.Create(…filePath…)` — `ComponentDesignFromJson.cs:67-74`: same check, publishes a `DataParseError` event then throws.
   - The designer UI only ever lists `factionInfoDB.Data.ComponentTemplates` (`ComponentsWindow.cs:51,156,169`) — a locked template simply isn't in the list.
   - (This is the exact "`X was not found in the faction data store`" crash from root gotcha #10 — a design whose `TemplateId` is not an unlocked template.)
3. **A finished DESIGN is buildable iff it's in `IndustryDesigns`** — put there by `CreateDesign` only when research-free or `StartResearched` (`ComponentDesigner.cs:209`); otherwise it waits in `LockedCargoGoods` until `tech-<designId>` is researched.

So the availability chain for a player weapon is: material unlocked (StartingItems or tech) → template unlocked (StartingItems or tech) → design created in the designer → design-tech researched → design in `IndustryDesigns` → buildable. This matches the "SIX registration points" / cradle-to-grave rule in the root CLAUDE.md.

---

## Research as a component ability — and its host-uniformity

Research **capacity** is a **component attribute**, exactly per `CONVENTIONS.md` §6 ("abilities are components"):

- `ResearchPointsAtbDB` (`Tech/ResearchPointsAtbDB.cs`) — fields `PointsPerEconTick`, `CostPerDay`, `BonusCategory` (the "specialty" that gets a +10% category bonus, `:76-77`). It is the `AttributeType` on the "Research Lab" installation template (`installations.json:150-196`, `AtbConstrArgs(Research Points, Cost Per Day, Research Specialty)`).
- On install, `OnComponentInstallation` (`ResearchPointsAtbDB.cs:56-86`) **spawns a separate entity** carrying a `ResearcherDB` (with `LocationId = host`), not a blob on the host itself.
- `ResearchProcessor` keys on **`ResearcherDB`** (`GetParameterType => typeof(ResearcherDB)`, `ResearchProcessor.cs:21`; `ProcessManager` iterates `GetAllEntitiesWithDataBlob<ResearcherDB>()`, `:43`). It does **not** key on `ColonyInfoDB`.

**Host implication — research is host-agnostic at the engine level.** Because the processor follows `ResearcherDB` (spawned by the component), research runs on *any* host that can mount a research-lab component — it is not colony-locked in code. `StationFactory.cs:91,106` confirms stations host research: its teardown explicitly "tears down SPAWNED SUB-ENTITIES (a research lab's `ResearcherDB` …)" so a dead station stops researching. The only thing that decides whether a station/ship/ground unit *can* host a lab is that template's `ComponentMountType` (the Research Lab installation is authored `PlanetInstallation`) — again a mount-type data question, not a tech question.

(Note `EntityResearchDB` (`Tech/EntityResearchDB.cs`) exists with a `Labs` dictionary but is a display/legacy shell — the live path is the spawned-`ResearcherDB` model above.)

---

## Universality assessment

**The unlock/availability system is host-uniform. Tech gating does NOT interact with the mount-type non-universality.** Evidence:

- **One store, no host partition.** `FactionDataStore` has a single `ComponentTemplates` dict (`FactionDataStore.cs:35`). `Unlock()` moves a template into it with no host tag and no per-host duplication (`:117-122`). Researching a radar makes it available to the faction, period — there is no "unlocked-for-ships" vs "unlocked-for-colonies" state anywhere.
- **The host restriction lives in a different, orthogonal field.** `ComponentTemplateBlueprint.MountType` (`ComponentTemplateBlueprint.cs:24`) is a `[Flags] ComponentMountType` (`Enums.cs:125-144`: `ShipComponent / ShipCargo / PlanetInstallation / PDC / Fighter / Missile / GroundUnit`), hand-authored per template in the JSON (e.g. `electronics.json:200` sensor = `"ShipComponent, ShipCargo"` — no colony/ground flag; `energy.json:24` reactor = `"ShipComponent, ShipCargo, Fighter, GroundUnit"`). Nothing in the tech/unlock path reads, sets, or widens `MountType`. `ComponentDesigner.cs:50,54` copies it verbatim from the template and only uses the `PlanetInstallation` flag to set a `CanBeInstalled` GUI hint.
- **The master designer lists templates without a host filter** (`ComponentsWindow.cs:156`, grouped by `ComponentType`, not mount) — so the non-universality the developer sees is enforced *downstream*, where a ship-design or installation-placement path checks `MountType` to decide whether a design may attach to *this* host, and by which templates a given host's designer chooses to surface. That filtering is a **data + UI** concern (the template's authored flag list), untouched by research.

**Conclusion:** research is the wrong place to look for the "designers aren't universal" bug and is not entangled with it. Fixing universality means widening template `MountType` flag lists (and any host-side designer filter), not changing the tech layer. Research *is* uniform, correctly.

---

## Open questions / gaps

1. **`MountType` is hand-authored per template with no consistency rule.** A sensor is `ShipComponent, ShipCargo` (`electronics.json:200`) — no `PlanetInstallation`, no `GroundUnit` — so the same radar cannot be a colony early-warning array or a ground unit's sensor even though the tech that unlocks it is single and universal. This asymmetry is the real non-universality; it is a data audit (see `04-BASEMOD-TEMPLATES.md`), not a tech fix.
2. **The two Tech kinds share one namespace and one category bucket.** Auto-generated per-design techs all get `Category = "tech-category-designs"` (`ComponentDesigner.cs:135`); confirm the `ResearchWindow` and scientist-bonus logic handle a potentially huge auto-generated tech list gracefully (every design ever made adds one).
3. **`StartResearched` is a static flag** (`ComponentDesigner.cs:23`) toggled around the colony-build loop (`ColonyFactory.cs:48,53`). It is process-global; if two factions were built concurrently this could race. Low risk today (serial startup), worth a note.
4. **Prototype/repeat research not modelled.** Aurora's "build a prototype" and in-field scientist bonus are absent (Tech `CLAUDE.md` status table) — a design's per-design tech is a single flat cost, not a prototype-then-cheaper-repeat curve.
5. **`gallicite` latent data bug** (root gotcha #10) — `ordnance.json` references a mineral that is not defined; unlockable-but-undefined ids fault only when that design is built. A general reminder that `Unlock(id)` does not validate the id exists as a real good.
6. **`EntityResearchDB` vs the spawned-`ResearcherDB` model** — the former looks live (has a `Labs` dict + ability description) but the processor uses the latter. Confirm `EntityResearchDB` is dead/vestigial and cannot mislead future work.
