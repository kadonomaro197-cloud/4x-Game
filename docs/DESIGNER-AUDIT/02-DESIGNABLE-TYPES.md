# Designer Audit 02 — The Designable / Buildable DATA TYPES (engine side)

**Scope:** Every engine-side type the player designs, makes, or builds — what class models it, how it's created, where it's stored, how it becomes a real thing, and whether it is host-generic or host-specific. This is the *type-level* view of why the designers aren't universal.

> Companion to `01-*` (mount legality) and later UI-side audits. Everything here is `IConstructableDesign` or the machinery around it. All file:line references are as of 2026-07-08.

---

## The one-paragraph finding

There is **one** shared "buildable" seam — the interface `IConstructableDesign` (`Pulsar4X/GameEngine/Engine/Interfaces/IConstructableDesign.cs:9`). Exactly **five** concrete classes implement it. Of those, **only `ComponentDesign` carries a `ComponentMountType`** (the flags that say *which host a thing may mount on*). The other four — `ShipDesign`, `OrdnanceDesign`, `GroundUnitDesign`, `ProcessedMaterial` — bake the host into the **C# class itself**: a ship is a `ShipDesign`, a missile is an `OrdnanceDesign`, a ground unit is a `GroundUnitDesign`. So at the type level, "what host can build/carry this" is decided by *which class wraps it and which factory registered it*, not by a portable mount flag. That is the root of the non-universality. On top of that, three of these classes (`ShipDesign`, `OrdnanceDesign`, `GroundUnitDesign`) are near-duplicate "assembly of components" types that share no base class and each re-implement component-summing and cost roll-up their own way, through three different assembly mechanisms.

---

## The master table

| Design type | File | Represents | Created by | Stored in | Built via (`OnConstructionComplete` does…) | Universal or host-specific |
|---|---|---|---|---|---|---|
| **`IConstructableDesign`** (interface) | `Engine/Interfaces/IConstructableDesign.cs:9` | The shared "a thing industry can build" contract: `UniqueID`, `Name`, `ResourceCosts`, `IndustryPointCosts`, `IndustryTypeID`, `OutputAmount`, `OnConstructionComplete(...)`. | — | — | Dispatched by `IndustryTools.ConstructStuff` at `Industry/IndustryTools.cs:125` (looks the design up in `factionInfo.IndustryDesigns`), then calls the design's own `OnConstructionComplete` (`IndustryTools.cs:214`). | **The universal seam.** Everything below is a leaf of this. |
| **`ComponentDesign`** | `Engine/Components/ComponentDesign.cs:14` | A single installable/mountable part (engine, laser, mine, reactor, habitat, bunker, ground weapon…). Carries `AttributesByType` (its `IComponentDesignAttribute` abilities) **and `ComponentMountType`** (`ComponentDesign.cs:56`). | `ComponentDesigner.CreateDesign(factionEntity)` (`Engine/Components/ComponentDesigner.cs:123`) from a `ComponentTemplateBlueprint`; or `ComponentDesignFromJson` for base-mod designs. | `FactionInfoDB.InternalComponentDesigns` (`Factions/FactionInfoDB.cs:87`), plus `IndustryDesigns` + `Data.CargoGoods` when researched (`ComponentDesigner.cs:209-210,217`). | `ComponentDesign.cs:63` — if `batchJob.InstallOn != null`: build a `ComponentInstance` and `AddComponent` onto the host (install); else drop into cargo as a unit. | **The only host-GENERIC design.** Mount legality lives here (`ComponentMountType`), so *in principle* one part could mount on ship/colony/PDC/fighter/missile/ground. |
| **`ShipDesign`** | `Ships/ShipDesign.cs:22` | A whole ship class = an ordered list of `ComponentDesign`s + armor. Derives mass/crew/cost/damage-profile. | `new ShipDesign(faction, name, components, armor)` (`ShipDesign.cs:124`) then `Initialise(faction)` (`ShipDesign.cs:178`); UI: `ShipDesignWindow`. | `FactionInfoDB.ShipDesigns` (`FactionInfoDB.cs:76`) + `IndustryDesigns` (`ShipDesign.cs:181-182`). | `ShipDesign.cs:64` — build a real ship entity via `ShipFactory.CreateShip`, add to fleet, commit crew; or queue in a `LaunchComplexDB`. | **Host-SPECIFIC** (ship). Baked class; no mount flag. |
| **`OrdnanceDesign`** | `Weapons/OrdnanceDesign.cs:19` | A missile/ordnance design = a component list filtered to `ComponentMountType.Missile` + fuel, deriving wet/dry mass, burn rate, exhaust velocity. | `new OrdnanceDesign(faction, name, fuel, components, …)` (`OrdnanceDesign.cs:77`); or `OrdnanceDesignFromJson`; UI: `OrdnanceDesignWindow`. | `FactionInfoDB.MissileDesigns` (`FactionInfoDB.cs:79`) + `IndustryDesigns` (`OrdnanceDesign.cs:82-83`) + `Data.CargoGoods`/`LockedCargoGoods` (`OrdnanceDesign.cs:134-141`). | `OrdnanceDesign.cs:60` — **only batch bookkeeping; delivers nothing itself.** (The built missile stockpiles as a cargo good elsewhere; the design's hook is nearly empty.) | **Host-SPECIFIC** (missile). Near-duplicate of `ShipDesign`. |
| **`GroundUnitDesign`** | `GroundCombat/GroundUnitDesign.cs:25` | A buildable ground unit. Holds **flattened combat stats** (Attack/Defense/HP/Range/Evasion/Shield…) *and* a `ComponentDesignIds` map (`GroundUnitDesign.cs:81`) — a different shape from the ship's `(design,count)` tuple list. | `GroundUnitAssembly.RegisterAssembledDesign(faction, …)` (`GroundCombat/GroundUnitAssembly.cs:201`) via `ToGroundUnitDesign` (`GroundUnitAssembly.cs:153`); or the `GroundUnitAtb` install path (a unit-as-component). | `FactionInfoDB.IndustryDesigns` **only** (`GroundUnitAssembly.cs:206`) — no dedicated `GroundUnitDesigns` dict, unlike ships/missiles. | `GroundUnitDesign.cs:89` — `GroundForces.RaiseUnit` places a `GroundUnit` (a plain data object, not an entity) on the colony's planet region. | **Host-SPECIFIC** (planet surface). Third parallel assembly type. |
| **`ProcessedMaterial`** | `Industry/ProcessedMaterial.cs:9` | A refined material (steel, electronics…) — a buildable *cargo good*, not a mounted thing. Subclass of the JSON `ProcessedMaterialBlueprint`. | Constructed from a `ProcessedMaterialBlueprint` (`ProcessedMaterial.cs:16`); the player doesn't "design" it — it's authored in JSON, unlocked by tech. | `FactionInfoDB.Data.CargoGoods` + `IndustryDesigns` (via `SetIndustryDesigns` `FactionInfoDB.cs:181-183` and the tech-unlock sync). | `ProcessedMaterial.cs:35` — `storage.AddCargoByUnit(material, OutputAmount)` (adds refined units to the colony's hold). | **Host-agnostic output** (goes to any cargo hold), but **not player-designed** — a data template, not a designer product. |

### Things that are NOT their own design class (important negatives)

- **Stations** have **no design class at all.** A station is *deployed* bare via `DeployStationOrder` (`Stations/DeployStationOrder.cs`) → `StationFactory.CreateStation` (`Stations/StationFactory.cs:31`), then built up *in-situ* from ordinary `ComponentDesign`s installed on it. There is no `StationDesign : IConstructableDesign`. Asymmetry: a ship is designed-then-built; a station is deployed-then-furnished.
- **Fighters** and **PDCs** are only `ComponentMountType` flags (`Engine/DataStructures/Enums.cs:138,136`). No `FighterDesign`/`PDCDesign` class exists — a fighter would be a `ShipDesign` of fighter-mount parts, a PDC a colony/`PlanetInstallation` thing. The flags exist; no type consumes them as a distinct buildable.
- **`ComponentInstance`** (`Engine/Components/ComponentInstance.cs:13`) is the *runtime* copy of a `ComponentDesign` once installed on an entity — not a design, not an `IConstructableDesign`. It reads its stats off `Design` (its backing `ComponentDesign`).
- **`ResearcherDB.Design` / `AdministratorDB.Design`** (`Tech/ResearcherDB.cs:71`, `People/AdministratorDB.cs:67`) hold an `IConstructableDesign` reference but are *spawned sub-entities* of an installed component, not designs themselves.

---

## Class hierarchy — what's shared vs duplicated

**Shared (the real universal spine):**

```
IConstructableDesign  (Engine/Interfaces/IConstructableDesign.cs:9)
├── ComponentDesign     — the ONLY one carrying ComponentMountType
├── ShipDesign          — also ICargoable, ISerializable
├── OrdnanceDesign      — also ICargoable, ISerializable
├── GroundUnitDesign
└── ProcessedMaterial   — also : ProcessedMaterialBlueprint, ICargoable
```

That interface is the whole of the shared contract. **There is no shared base class** among the five — no `DesignBase`, no `AssembledDesign`. Each reimplements `UniqueID`, `Name`, `ResourceCosts`, `IndustryPointCosts`, `IndustryTypeID`, `OutputAmount`, and its own `OnConstructionComplete`.

**Duplicated — the three "assembly of components" types:**

`ShipDesign`, `OrdnanceDesign`, and `GroundUnitDesign` are three parallel takes on the same idea ("a buildable made of a list of components, with summed stats and costs"), and they do **not** share code:

| Concern | `ShipDesign` | `OrdnanceDesign` | `GroundUnitDesign` |
|---|---|---|---|
| Component list shape | `List<(ComponentDesign design, int count)> Components` (`ShipDesign.cs:49`) | Same tuple list `Components` (`OrdnanceDesign.cs:42`) | `Dictionary<string,int> ComponentDesignIds` (ids, not designs) (`GroundUnitDesign.cs:81`) + flattened stats |
| Where stat/cost summing lives | `Recalculate(faction)` on the class (`ShipDesign.cs:137`) | inline in the constructor (`OrdnanceDesign.cs:94-132`) | in a **separate** static assembler `GroundUnitAssembly.Compute` (`GroundUnitAssembly.cs:54`) |
| Cost dictionaries | `MineralCosts`/`MaterialCosts`/`ComponentCosts`/`ShipInstanceCost` | **identical** four dictionaries (`OrdnanceDesign.cs:45-48`) | just `ResourceCosts` (rolled up in `ToGroundUnitDesign` `GroundUnitAssembly.cs:172`) |
| Damage profile | `EntityDamageProfileDB DamageProfileDB` | **identical** field (`OrdnanceDesign.cs:70`) | none |
| Armor | `(ArmorBlueprint, float) Armor` | **identical** field (`OrdnanceDesign.cs:43`) | none |

`ShipDesign` and `OrdnanceDesign` are almost line-for-line the same class with different `OnConstructionComplete` bodies and a fuel calc. `GroundUnitDesign` is a third variant that (deliberately) keeps *flattened* combat stats as its read-model and only additively keeps the component-id list.

**Four different creation mechanisms for "assemble components into a buildable":**

1. `ComponentDesigner` (`Engine/Components/ComponentDesigner.cs:20`) — the real *component* designer: takes a `ComponentTemplateBlueprint`, evaluates NCalc formulas against faction tech, instantiates `IComponentDesignAttribute`s via reflection (`ComponentDesigner.cs:94`), sets up a research unlock, and registers the `ComponentDesign`.
2. `ShipDesign` ctor + `Recalculate` — ship assembly on the class itself.
3. `OrdnanceDesign` ctor — missile assembly on the class itself.
4. `GroundUnitAssembly` (`GroundCombat/GroundUnitAssembly.cs:46`) — a *separate static assembler* with a carry-gate + power gate + ammo gate, producing a `GroundUnitDesign`.

So there are effectively **four "designers"** (component, ship, ordnance, ground) and they share no assembly base.

**`ComponentDesign` ↔ `ComponentTemplate` ↔ `ComponentInstance` (the one place the model IS clean):**

- `ComponentTemplateBlueprint` (`Engine/Blueprints/ComponentTemplateBlueprint.cs:6`) = the **JSON template / mold**: formulas, resource-cost formulas, `MountType`, `IndustryTypeID`, tunable `Properties`. Loaded into `FactionDataStore.ComponentTemplates`.
- `ComponentDesign` (`ComponentDesign.cs:14`) = the **player's filled-in design** from that mold — concrete numbers + concrete `IComponentDesignAttribute`s, carrying the `ComponentMountType` copied from the template (`ComponentDesigner.cs:50`).
- `ComponentInstance` (`ComponentInstance.cs:13`) = a **built, installed copy** of a design on a specific entity; reads mass/volume/cargo-type off its `Design`.

This template → design → instance chain is exactly the universal shape the *other* buildables lack: ships, missiles, and ground units have no equivalent JSON "mount type" seam on their design class.

---

## Universality assessment

**Host-generic (1):**
- `ComponentDesign` — the only design whose host is data (`ComponentMountType` flags), not its class. The whole universality *substrate* already exists here.

**Host-specific by CLASS (3):**
- `ShipDesign` (ship), `OrdnanceDesign` (missile), `GroundUnitDesign` (planet surface). Each answers "where can this be built / what does it become" by *being that class* and by *which factory dict it's registered in*, not by a flag. `OnConstructionComplete` is the hard-coded host behavior: ship→spawn entity, missile→(nothing/stockpile), ground unit→raise on planet.

**Not a player design (1):**
- `ProcessedMaterial` — output-only, authored in JSON. Host-agnostic where it lands (any cargo hold) but not something the player *designs*.

**Duplication verdict:** `ShipDesign` and `OrdnanceDesign` are the clearest duplicate pair — the same "component-list + armor + cost-dicts + damage-profile" class twice. `GroundUnitDesign` is a third, deliberately-flattened parallel. All three could sit on a shared `AssembledDesign` base that owns component-summing and cost roll-up; today they don't.

**Storage inconsistency (a symptom of the same disease):** every design type is *also* registered in a bespoke faction dictionary in addition to the shared `IndustryDesigns`:
- Components → `InternalComponentDesigns` (+ `CargoGoods`) (`FactionInfoDB.cs:87`)
- Ships → `ShipDesigns` (`FactionInfoDB.cs:76`)
- Missiles → `MissileDesigns` (+ `CargoGoods`/`LockedCargoGoods`) (`FactionInfoDB.cs:79`)
- Ground units → **`IndustryDesigns` only** (no dedicated dict)
- Materials → `CargoGoods` (+ `IndustryDesigns`)

The one place they all converge is `FactionInfoDB.IndustryDesigns` (`FactionInfoDB.cs:95`), which is what `IndustryTools.ConstructStuff` actually reads. So the *build* path is already universal; the *design registry* is fragmented per host.

---

## Top type-level universality gaps (the headline)

1. **Mount legality lives on only one of five design classes.** `ComponentMountType` is on `ComponentDesign` alone. A capability's host is otherwise encoded as *which class* (`ShipDesign` vs `OrdnanceDesign` vs `GroundUnitDesign`) — so a part designed for one host can't be freely re-hosted, because the *containing design class* changes, not just a flag.
2. **No shared `AssembledDesign` base.** `ShipDesign`/`OrdnanceDesign`/`GroundUnitDesign` re-implement component-summing and cost roll-up independently, through four unshared assembly mechanisms (`ComponentDesigner`, two class ctors, `GroundUnitAssembly`). Duplication guarantees drift.
3. **Stations have no design type at all** — an asymmetry vs ships (deploy-then-furnish vs design-then-build), so a "station class" can't be designed or reused the way a ship class can.
4. **Fighter/PDC are dangling mount flags with no consuming buildable type** — the enum promises host targets that no design class fulfills.
5. **Per-host design registries** (`ShipDesigns`/`MissileDesigns`/`InternalComponentDesigns`) fork what `IndustryDesigns` already unifies — the storage layer mirrors the class-level fragmentation.

---

## Open questions / gaps for the fix

- Should the fix hoist `ComponentMountType` (or an analog) onto a shared `IConstructableDesign`/`AssembledDesign` so *every* buildable declares its legal hosts as data — collapsing the class-per-host split?
- Can `ShipDesign` and `OrdnanceDesign` be unified onto one "assembled vehicle" base (they already share ~90% of fields), with `OnConstructionComplete` the only real difference?
- Where does the `GroundUnitDesign` flattened-stats read-model fit if units migrate to entities (see `docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md`)? A unification should not re-flatten what that migration is un-flattening.
- Is a `StationDesign` warranted (design-then-deploy), or is deploy-then-furnish the intended permanent asymmetry?
- The per-host faction dictionaries: safe to collapse to `IndustryDesigns` + typed views, or do downstream UIs (`ShipDesignWindow`, `OrdnanceDesignWindow`) depend on the dedicated dicts? (UI-side audit territory.)
