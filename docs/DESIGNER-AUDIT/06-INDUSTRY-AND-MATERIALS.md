# 06 — Industry & Materials (the "MAKE" layer)

**Scope:** everything a player can MAKE via the economy — refining, component/installation/ship/ordnance construction, mining — and the exact chain that turns a design into something buildable, plus whether those "make" abilities are the same on every host (colony vs station vs ship).

---

## 1. The five IndustryTypeID categories

An `IndustryTypeID` is a **string tag** on a buildable design (`IConstructableDesign.IndustryTypeID`, `Engine/Interfaces/IConstructableDesign.cs:21`). It says *which kind of production line* can work the job. The tags are data, defined in `GameData/basemod/TemplateFiles/industryTypes.json` (five entries). A **production line** (`IndustryAbilityDB.ProductionLine`, `Industry/IndustryAbilityDB.cs:12`) carries a `Dictionary<string,int> IndustryTypeRates` — build-points-per-day it can spend **per tag**. A facility (installation/component) grants a line via the `IndustryAtb` attribute (`Industry/IndustryAtb.cs`); the rate dictionary is authored in the facility's template JSON (`DataDict` → `Pulsar4X.Industry.IndustryAtb`).

The daily job loop (`IndustryProcessor` → `IndustryTools.ConstructStuff`, `Industry/IndustryTools.cs:117-218`) walks each line, and for each queued job spends `industryPointsRemaining[design.IndustryTypeID]` (`IndustryTools.cs:126`) — so a job only advances on a line that has points for **its** tag. Delivery on completion is dispatched to the design's own `OnConstructionComplete` (`IndustryTools.cs:214`) — a single dispatch point, each design type decides where its output goes.

| IndustryTypeID | What it builds | Facility / ability that grants the line | Host |
|---|---|---|---|
| `refining` | **Refined materials** (`ProcessedMaterial`) from raw minerals | **Refinery** (`installations.json:204` `refinery` → `IndustryAtb` rate `refining = Size×0.1`) | colony / station (any host with the component) |
| `component-construction` | **Ship components** — weapons, engines, electronics, energy, ordnance parts (24 templates) | **Factory** (`installations.json:257`) and **Shipyard** (`installations.json:327`) both grant `component-construction` points | colony / station |
| `installation-construction` | **Installations + ground units** — mines, refineries, factories, shipyards, sensors, weapons, ground frames/units (43 templates — the biggest bucket) | **Factory** (`installation-construction` points) | colony / station |
| `ordnance-construction` | **Assembled missiles/ordnance** (`OrdnanceDesign`) | **Factory** (`ordnance-construction` points) | colony / station |
| `ship-assembly` | **Ships** (`ShipDesign`) | **Shipyard** (`ship-assembly = Crew×0.02`, gated by a `Slip Size` max-tonnage cap) | colony / station |

Facility → tag map (authored `DataDict` in `installations.json`):
- **Refinery** (`:249`): `refining`.
- **Factory** (`:319`): `component-construction`, `installation-construction`, `ordnance-construction` (also a `Fighter Construction Points = 0` — a latent `fighter-construction` tag that is **not** defined in `industryTypes.json`; dead rate).
- **Shipyard** (`:388`): `component-construction`, `ship-assembly`, with a per-slip tonnage limit (`Slip Size`, the one facility with a size gate on what it can assemble).

**Where each design's tag comes from:**
- `ProcessedMaterial` and every `ComponentDesign` read `IndustryTypeID` from JSON — the material blueprint (`ProcessedMaterialBlueprint.IndustryTypeID`) or the component **template** (`ComponentTemplateBlueprint.IndustryTypeID`, `Engine/Blueprints/ComponentTemplateBlueprint.cs:25`). So a component's build-lane is decided by its template file (e.g. everything in `weapons.json`/`engines.json`/`electronics.json`/`energy.json`/`ordnance.json` = `component-construction`; everything in `installations.json` = `installation-construction`).
- `OrdnanceDesign.IndustryTypeID` and `ShipDesign.IndustryTypeID` are **hard-coded in C#** (`Weapons/OrdnanceDesign.cs:54` = `"ordnance-construction"`; `Ships/ShipDesign.cs:61` = `"ship-assembly"`) — not data. Minor non-uniformity: ships/ordnance can't be re-tagged via a mod file the way a component or material can.

> **Note — dead enum vestige:** `IndustryOrder2.IsValidCommand` (`Industry/IndustryOrder.cs:164`) has commented-out code calling `_design.IndustryTypeID.HasFlag(IndustryTypeID.Installations)` — a reference to an old **`[Flags] enum IndustryTypeID`** that no longer exists (the tag is a string now). It is dead/commented, but it's the code that *used to* set `job.InstallOn` for installation jobs. See §5 open questions.

---

## 2. Refined materials & minerals

### Raw minerals (mined) — `TemplateFiles/minerals.json`, 15 entries
`hydrocarbons, iron, aluminium, copper, titanium, lithium, chromium, fissionables, silicon, graphite, tungsten, nickel, water, rare-earth-elements, regolith`. Each `Mineral` carries a `CargoTypeID` (all `general-storage`), a `CreditValue`, and an `Abundance` table per body-type (Terrestrial/GasGiant/IceGiant/Asteroid/Comet/DwarfPlanet/Moon/GasDwarf) used at system-gen to seed deposits.

### Refined materials (produced) — `TemplateFiles/materials.json`, 22 `ProcessedMaterial` entries
Every one has `IndustryTypeID: "refining"`, a `ResourceCosts` dict (minerals consumed), `IndustryPointCosts`, and an `OutputAmount` (units produced per job). Categories:
- **Structural / hull metals:** `stainless-steel`, `stainless-steel-d`, `stainless-steel-a`, `nickel-steel`, `space-crete`.
- **Armor / plating materials:** `tungsten-plating`, `ablative-composite`, `corrosion-resistant-alloy`, `em-shielding-mesh`, `reinforced-trusswork`.
- **Rocket fuels:** `rp-1`, `methalox`, `hydrolox`, `ntp` (fuel-storage cargo type; carry `Formulas` for ExhaustVelocity/FuelType).
- **Electronics / power:** `electronics`, `electronics-d`, `electronics-a`, `electricity`, `lithium-battery`, `ree-magnetics`.
- **Chemicals / nuclear:** `plastic`, `fissile-fuels`.

A `ProcessedMaterial` (`Industry/ProcessedMaterial.cs`) is both an `ICargoable` **and** an `IConstructableDesign` — refining IS a construction job whose `OnConstructionComplete` (`ProcessedMaterial.cs:35`) just `storage.AddCargoByUnit(material, OutputAmount)`. So a refined material lands in the same cargo hold minerals do, ready to be consumed by the next build.

### Mining (the input)
- `MineralsDB` on the **body** (planet/asteroid) — `mineralID → MineralDeposit { Amount, Accessibility }` (`Industry/MineralsDB.cs`). Depletes permanently as mined.
- `MineResourcesAtbDB` on an installed **mine component** (`Industry/MineResourcesAtbDB.cs`) — which minerals, base rate. On install it bumps `MiningDB.NumberOfMines` on the host (`:38`).
- `MineResourcesProcessor` (daily, keys on `MiningDB`) runs `MiningHelper.CalculateActualMiningRates` — sums component rates, scales by `Accessibility`, moves units from body's `MineralsDB` into the host's `CargoStorageDB`.
- **Located deposits (new):** `HexMinerals.SeedDeposits` places a share of the body pool onto surface hexes (view-only in v1; the colony still mines the body-wide pool).

---

## 3. Mining / refining / factory capacity are themselves COMPONENTS

This is the key tie-back to the component model: the *ability to make* is not a host property — it's an **installed component** carrying an `*Atb` attribute, exactly like a ship's gun.
- **Mine** = `mine`/`automine` template (`installations.json:5,78`) with a `MineResourcesAtbDB` attribute → grants mining.
- **Refinery** = `refinery` template with `IndustryAtb` (refining points).
- **Factory** = `factory` template with `IndustryAtb` (component/installation/ordnance points).
- **Shipyard** = `shipyard` template with `IndustryAtb` (component + ship-assembly points, slip-size gated).
- **Infrastructure** (`infrastructure`/`space-habitat` templates) carries `InfrastructureCapacityAtb` — the utility grid that scales ALL production. `IndustryTools.ConstructStuff` multiplies every line's rate by `InfrastructureProcessor.GetEfficiency(host)` (`IndustryTools.cs:115,121`), capped at 1.0.

When any of these components is installed, its `*Atb.OnComponentInstallation` attaches/extends the matching ability DataBlob **on the parent entity** — `IndustryAtb` adds a `ProductionLine` to `IndustryAbilityDB` (`IndustryAtb.cs:47-64`), `MineResourcesAtbDB` sets up `MiningDB`. There is **no host-type check** in any of these install hooks — they act on `parentEntity`, whatever it is.

---

## 4. The six-point registration chain — how a design becomes buildable

To make one new buildable thing appear in the game, six things must line up. Missing any one either crashes New Game or leaves the item un-queueable. Enforced/observed at these exact points:

1. **Template exists** — a `ComponentTemplate` in `TemplateFiles/*.json` (or a `ProcessedMaterial`/`Mineral`/`IndustryType` blueprint). The template's `IndustryTypeID` sets the build-lane.
2. **Design exists** — a `ComponentDesignBlueprint` (in `ScenarioFiles/designs/componentDesigns.json` or a `componentDesigns/*.json` file) whose `TemplateId` points at (1).
3. **Template UNLOCKED for the faction** — the *template* id must be in the colony blueprint's `StartingItems` (or a starting tech's `Unlocks`). Enforced by `ComponentDesignFromJson.Create` (`Engine/Components/ComponentDesignFromJson.cs:18-21`): if `!factionDataStore.ComponentTemplates.ContainsKey(TemplateId)` it throws **`"<TemplateId> was not found in the faction data store"`**. `ColonyFactory.CreateFromBlueprint` unlocks `StartingItems` first (`Colonies/ColonyFactory.cs:31`), THEN builds each `ComponentDesigns` entry (`:49-51`).
4. **Design INSTANTIATED for the faction** — the *design* id must be in the colony blueprint's `ComponentDesigns` list (`ColonyBlueprint.ComponentDesigns`, `Engine/Blueprints/ColonyBlueprint.cs:12`). `ColonyFactory` loops it and calls `ComponentDesignFromJson.Create`, which registers into `FactionInfoDB.InternalComponentDesigns` (`ComponentDesigner.cs:217`). Miss this → the design is never built, and a ship referencing it throws `KeyNotFoundException`.
5. **Materials UNLOCKED** — every mineral/material in the design's `ResourceCosts` must itself be in `StartingItems` (or tech-unlocked). `ComponentDesigner` looks up each cost material in the **unlocked** `CargoGoods`; a missing one crashes colony creation. (This is the `electronics`/`ree-magnetics` class of crash — gotcha #10 / Factions CLAUDE #4.)
6. **Listed in `IndustryDesigns`** — the "things we can build right now" dictionary (`FactionInfoDB.IndustryDesigns`, `Factions/FactionInfoDB.cs:95`), populated by `SetIndustryDesigns` (`:177-193`) from **materials + component designs + ship classes**. `IndustryJob` resolves its cost/tag by `factionInfo.IndustryDesigns[itemID]` (`IndustryJob.cs:28`) and `ConstructStuff` re-reads it (`IndustryTools.cs:125`). A mid-game tech unlock must re-sync this dict or the item stays un-queueable (handled in `ResearchProcessor.DoResearch`).

**The sensor that guards this:** `Pulsar4X.Tests/Modding/BaseModIntegrityTests.cs` reproduces the real unlock logic. `StartingColonies_CanBuildEveryStartingComponentDesign` (`:36`) asserts point (3) — every `ComponentDesigns` entry's template is unlocked by `StartingItems` (fail message names the missing template, `:80`) — and point (5) — every `ResourceCost` material is unlocked (`:92`). `BaseMod_LoadsWithNoSkippedEntries` (`:110`) asserts nothing was silently dropped (null/empty `UniqueID`). This is the automated gate; the tests build the colony in C# but this test walks the **JSON** path that the real game uses.

Worked confirmation in data (`earth.json`): the four industry facilities are registered end-to-end — templates `refinery`/`factory`/`shipyard`/`mine`/`automine` in `StartingItems` (`earth.json:119-124`), and designs `default-design-refinery`/`-factory`/`-shipyard`/`-mine`/`-auto-mine` in `ComponentDesigns` (`:196-223`). The five **IndustryType ids themselves** (`refining`, `component-construction`, `installation-construction`, `ship-assembly`) also appear in `StartingItems` (`:115-118`).

---

## 5. Host-uniformity of industry abilities — **YES, the MAKE verbs are host-uniform**

The core finding: unlike some UI designers, the **engine's production/mining/refining/research is host-agnostic by construction.** Every economy processor discovers its work by an **ability DataBlob**, never by host type:

| Verb | Processor | Keys on | Host-type branch? |
|---|---|---|---|
| Build / refine / assemble | `IndustryProcessor` (`Industry/IndustryProcessor.cs:16,30`) | `IndustryAbilityDB` | **None** — any entity with the blob is processed |
| Mine | `MineResourcesProcessor` | `MiningDB`; body resolved via `MiningHelper.TryGetMiningBody` | Resolver handles colony→`PlanetEntity`, station→`HostingBodyEntity` — one helper, no branch in the processor |
| Research | `ResearchProcessor` | `ResearcherDB` (spawned by a lab component) | **None** |

The blob only exists because a **component granted it** (`IndustryAtb.OnComponentInstallation` acts on `parentEntity` with no host check). So:
- A **station** carrying a factory/refinery/shipyard/mine builds, refines, assembles and mines **exactly like a colony**, with zero station-aware code (confirmed in `Stations/CLAUDE.md` §"Shared chassis" and the `StationFactoryTests` gauges: `Station_WithMiningModule_MinesItsHostingBody`, `Station_WithConstructorModule_IsAnInSituBuilder`, `ResearchStation_AccruesResearchTowardAQueuedTech`).
- A **colony** is not special — it's just the host that happens to start with the components installed.

**Where uniformity stops (the real asymmetries):**
1. **Ships are NOT industry hosts in practice.** `IndustryAbilityDB`'s own comment says "attached to colonies and possibly ships with fabrication bays" — but **no ship component in the data grants `IndustryAtb`**, so a ship/construction-vessel can't build or refine in flight. A "mobile shipyard/fabrication ship" is latent-possible (install an `IndustryAtb` component on a ship) but unbuilt. The only ship role in the economy is the **hauler** that carries a station's frame materials (`DeployStationOrder`).
2. **`ship-assembly` is slip-size gated**, the only lane with a per-facility size cap — a shipyard slip limits max tonnage. Refining/component/installation lanes have no such gate.
3. **Ordnance/ship tags are C#-hardcoded**, not data (see §1) — a small modding non-uniformity vs components/materials.
4. **Infrastructure is the shared throttle** — every host's whole output scales by `InfrastructureProcessor.GetEfficiency`. Uniform, but means a station with no infra module produces at reduced/zero efficiency the same way an over-built colony does.

**Bottom line:** the "make" ability is a **universal component**, not a host attribute. What you can build WHERE is decided entirely by which industry **components** are installed on the host — and any host that can carry components (colony, station) can carry any of them. This is the *opposite* of the designer-uniformity problem: the production layer is already the universal model the designers should mirror.

---

## 6. Open questions / gaps

- **Who sets `IndustryJob.InstallOn` for a UI-queued installation?** The code that set it (`IndustryTaTypeID.HasFlag(...Installations)`) is **commented out and references a deleted enum** (`IndustryOrder.cs:164`). `ComponentDesign.OnConstructionComplete` (`ComponentDesign.cs:70`) only installs the built component if `InstallOn != null`, else it drops it into cargo as a stockpiled component. So a plain installation queued through `IndustryOrder2` may **land in the stockpile, not get installed**, unless something else sets `InstallOn`. Ground builds do set it (`GroundBuild.QueueBuildOnTile`). Needs a live check: does the colony-window "build installation" path auto-install or stockpile?
- **`fighter-construction` is a phantom tag** — the factory authors `Fighter Construction Points = 0` but no `fighter-construction` IndustryType is defined in `industryTypes.json`. Dead/aspirational; a fighter-assembly lane was planned and never wired.
- **`ordnance-construction` has no dedicated facility** — it rides on the general **factory**. Fine, but worth noting there is no "magazine/ordnance plant" the way there's a shipyard for ships.
- **Ships as fabrication hosts** — genuinely latent. If a "construction ship builds modules in flight" is desired, it's one `IndustryAtb` component away, but nothing tests or ships it.
- **`InstallationsDB` is dead** (no `[JsonProperty]`, never attached) — installations are components in `ComponentInstancesDB`. Confirmed corpse; do not build on it.
- **Alpha starting stockpiles are enormous** (`earth.json` Cargo stocks every mineral + common refined materials at 50,000,000) so nothing is resource-gated during testing — the refining loop is real but currently bypassed by the stockpile. The economy pace won't be felt until these are trimmed.
