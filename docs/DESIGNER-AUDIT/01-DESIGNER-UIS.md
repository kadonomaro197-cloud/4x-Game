# Designer Audit — Part 1: The Designer UIs (Client Side)

*Scope: every place in `Pulsar4X.Client/` where a player designs, assembles, or places something buildable — and whether each designer is host-universal or locked to one host type.*

*As of 2026-07-08. Every claim is cited file:line. Read alongside the engine-side audit (the `ComponentMountType` model lives in `GameEngine/Engine/DataStructures/Enums.cs:125`).*

---

## The one-paragraph finding

There is exactly **one universal designer** in the client — the **Component Designer** (`ComponentDesignWindow`), which offers *every* template regardless of host and lets the template's own `MountType` data decide where the result can go. Everything downstream of it — the four **assemblers** that bundle those components into a deployable (ship, missile, colony installation, station module) — is **host-locked**: each one hardcodes a single `ComponentMountType` flag as its part filter, so a component that is legal on two hosts still only ever appears in the assembler whose flag it happens to match first. And two whole host classes have **no designer at all**: **ground units** bypass the component pipeline entirely (a separate `GroundUnitDesign` class, created only from a hardcoded DevTools preset), and **PDC** and **Fighter** mount flags exist in the enum but no UI ever offers or assembles them.

---

## The mount-type model (the thing designers should be filtering against)

`GameEngine/Engine/DataStructures/Enums.cs:125-143` — `[Flags] ComponentMountType`:

| Flag | Value | Description string |
|------|-------|--------|
| `None` | 0 | None |
| `ShipComponent` | 1<<0 | Ship |
| `ShipCargo` | 1<<1 | Cargo Hold |
| `PlanetInstallation` | 1<<2 | Colony |
| `PDC` | 1<<3 | PDC |
| `Fighter` | 1<<4 | Fighter |
| `Missile` | 1<<5 | Missle |
| `GroundUnit` | 1<<6 | Ground Unit |

It is a **`[Flags]`** enum: a single `ComponentDesign` can legally carry several mount flags at once (`ComponentDesign.ComponentMountType`, `GameEngine/Engine/Components/ComponentDesign.cs:56`). The whole point of the design — a radar that is `ShipComponent | PlanetInstallation | GroundUnit` — is defeated the moment an assembler filters with a single-flag `HasFlag(...)` and nothing shows the component in the other hosts' builders. That single-flag filter is the recurring defect below.

---

## Categorized inventory of every designer / maker / placer

| Designer | File | What it designs / makes | Host / mount targeted | How opened | Part-filtering mechanism | Universal? |
|----------|------|--------------------------|------------------------|------------|--------------------------|-----------|
| **Component Designer** | `Interface/Windows/ComponentDesignWindow.cs` (+ panel `Interface/Displays/ComponentDesignDisplay.cs`) | A `ComponentDesign` from a `ComponentTemplateBlueprint` — the atomic buildable (weapon, engine, sensor, reactor, cargo bay, installation, …) | **ANY** — host is whatever the template's `MountType` says; the designer is host-agnostic | Toolbar button "Design a new component or facility" (`Interface/HUD/ToolBarWindow.cs:43-51`) | Offers **all** faction `ComponentTemplates`, grouped by the template's `ComponentType` string, filterable only by that group name (`ComponentDesignWindow.cs:39-47, 108-118`). **No `ComponentMountType` filter at all.** Mount legality shown read-only as "Installs On or In" (`ComponentDesignDisplay.cs:280-300`) | **YES** — the shared substrate |
| **Ship Designer** | `Interface/Windows/ShipDesignWindow.cs` | A `ShipDesign` = ordered list of `ComponentDesign`s + armor | **Ship only** | Toolbar "Design a new Ship" (`ToolBarWindow.cs:53-61`) | Available-component list **skips anything without `ComponentMountType.ShipComponent`** (`ShipDesignWindow.cs:568-569`); further group-filter by `ComponentType` string (`:546-555`). Armor list from `factionData.Armor` (`:170-193`) | **NO — host-locked to `ShipComponent`** |
| **Ordnance (Missile) Designer** | `Interface/Windows/OrdnanceDesignWindow.cs` | An `OrdnanceDesign` = payload + electronics + inline-designed engine + fuel | **Missile only** | Right-click / order path; instance via `GetInstance()` (`OrdnanceDesignWindow.cs:47`). *(Not on the main toolbar.)* | Payload & electronics filtered to `ComponentMountType.Missile` (`OrdnanceDesignWindow.cs:87`) then by attribute type (`OrdnancePayloadAtb`, `OrdnanceExplosivePayload`, `SensorReceiverAtb`, …, `:89-115`). Engine list filtered to templates carrying `NewtonionThrustAtb` (`:135-144`) and designed **inline** via an embedded `ComponentDesigner` (`:205-206`) | **NO — host-locked to `Missile`** |
| **Colony "Local Construction"** | `Interface/Displays/ConstructionDisplay.cs` | Queues a `ComponentDesign` to be built + installed at a colony | **Colony installation only** | Construction tab of `ColonyManagementWindow` | `_availableDesigns` filtered to `IsValid && (mount & PlanetInstallation) != 0` (`ConstructionDisplay.cs:59-62`) | **NO — host-locked to `PlanetInstallation`** |
| **Colony Production queue** | `Interface/Displays/IndustryDisplay.cs` | Queues any faction `IndustryDesign` (refine job / component build / ship build) at a colony | **Colony host; multi-target install** | Production tab of `ColonyManagementWindow`; also embedded in `StationWindow` | Offers the faction's `IndustryDesigns`; uses mount type only to choose the **auto-install target** — `PlanetInstallation`→install on this colony, else `ShipComponent`/`ShipCargo`/`Fighter`/`Missile`→install on a chosen ship (`IndustryDisplay.cs:413-425`) | **Partial** — the queue itself is host-agnostic, but "auto-install" logic is a hardcoded mount switch |
| **Station module builder** | `Interface/Windows/StationWindow.cs` | Queues + installs modules on a deployed station | **Station (reuses colony industry)** | Map context menu "Manage Station" (gated on `StationInfoDB`) | Delegates entirely to the host-agnostic `IndustryDisplay.GetInstance(...).Display(...)` (`StationWindow.cs:68`) | **Partial** — most universal of the assemblers because it reuses `IndustryDisplay` rather than a bespoke filter |
| **Ground-map building placer** | `Interface/Windows/PlanetViewWindow.cs` | Places an already-designed installation onto a surface region/hex; queues a build on a city tile | **Colony installation only** | Planet context menu "Planet view (regions)" (gated on `PlanetRegionsDB`) | `PlaceableInstallations()` filters faction `IndustryDesigns` to `ComponentMountType.PlanetInstallation` (`PlanetViewWindow.cs:940-957`; also the inline filter at `:950`) | **NO — host-locked to `PlanetInstallation`** |
| **Ground-unit "Raise"** | `Interface/Windows/DevToolsWindow.cs` | Creates a `GroundUnitDesign` and raises units of it | **Ground unit** | **DevTools only** (SM/dev) — "Raise Ground Unit" (`DevToolsWindow.cs:809-830`) | **None — no designer.** Uses a hardcoded throwaway preset `MakeDevGroundDesign(type)` with uniform stats (`DevToolsWindow.cs:171-181`); type/count/region chosen by dev widget | **NO — not a real player designer; no component pipeline** |
| **Research / Tech selection** | `Interface/Windows/ResearchWindow.cs` | Not a buildable-designer — *unlocks* what templates/designs become designable | (gates all hosts) | Toolbar "Research" (`ToolBarWindow.cs:73-81`) | Lists `_factionData.Techs` where `IsResearchable`, filtered by tech category (`ResearchWindow.cs:82-100`); double-click queues via order | n/a (upstream gate, not a designer) |
| **Component Library** | `Interface/Windows/ComponentsWindow.cs` | Read-only browser of templates + faction designs | (all) | via window registry | Groups by `ComponentType`; shows `design.ComponentMountType.ToString()` (`ComponentsWindow.cs:578`) and template `MountType` (`:381`) read-only | n/a (viewer) |
| **Fire Control assignment** | `Interface/Windows/FireControlWindow.cs` | Assigns already-built weapons to fire-control — not a designer | Ship | context menu | n/a | n/a (assignment, not design) |
| **Mod / Blueprint editor** | `ModFileEditing/BluePrintsUI.cs`, `ComponentBluprintUI.cs`, `ShipDesignBlueprintUI.cs`, `ComponentPropertyBlueprintUI.cs` | Edits the base-data **JSON blueprints** (author-time), incl. a template's `MountType` | (authoring layer, all hosts) | Mod editor screens | `BluePrintsUI.cs:69` enumerates **all** `ComponentMountType` names for the mount picker; `ComponentBluprintUI.cs:157` reads `selectedItem.MountType` | n/a (data authoring, not in-game design) — *notably the only place all mount flags are exposed together* |

---

## Universality assessment

### The one universal designer, and why it works
`ComponentDesignWindow` + `ComponentDesignDisplay` is genuinely host-agnostic. It never inspects `ComponentMountType` to decide what to offer — it lists every template the faction has unlocked (`ComponentDesignWindow.cs:39-49`) and lets the **template data** carry the mount legality, which it only *displays* ("Installs On or In", `ComponentDesignDisplay.cs:280-300`). This is the correct pattern: the designer is universal, the *component* declares its own hosts. Every gap below is a **failure to carry that same principle into the assemblers.**

### Host-locked designers (each hardcodes ONE mount flag as its filter)
Four assemblers each pick components by a single `HasFlag`/`& mount` test, so a multi-host component only surfaces where its flag matches:

- **Ship Designer** → `ComponentMountType.ShipComponent` only (`ShipDesignWindow.cs:568`).
- **Ordnance Designer** → `ComponentMountType.Missile` only (`OrdnanceDesignWindow.cs:87`).
- **Colony Local Construction** → `ComponentMountType.PlanetInstallation` only (`ConstructionDisplay.cs:60`).
- **Ground-map placer** → `ComponentMountType.PlanetInstallation` only (`PlanetViewWindow.cs:950`).

Consequence (the developer's exact complaint): a sensor authored as a ship component will **never appear** in the colony or station builder even if its template also carried `PlanetInstallation`, because (a) the assemblers filter on a single flag and (b) in practice base-mod templates are authored with one mount flag, so the flags themselves are single-host. **The fix is two-sided:** author templates with multi-flag mounts *and* make each assembler offer any component whose mount flags intersect the host it targets (an `& hostMask != 0` test, not a single-flag `HasFlag`).

### Parallel / duplicated designers
The four assemblers are near-clones of the same workflow — *pick components from a filtered list → adjust counts/order → name → save a `*Design`* — differing only in (1) which single mount flag they filter by and (2) which design class they emit (`ShipDesign` vs `OrdnanceDesign` vs an installation build job). `ShipDesignWindow` and `OrdnanceDesignWindow` even re-implement the same component-list + group-filter + mass/stat rollup independently. `StationWindow` is the outlier that got it right by **reusing** `IndustryDisplay` instead of writing a new filter (`StationWindow.cs:68`) — the template for how the others should collapse toward one host-parameterized assembler.

### Whole host classes with NO designer
- **Ground units** — the largest gap. Ground units are a **separate `GroundUnitDesign` class** (not a `ComponentDesign`), so they get none of the component pipeline (research-gating, materials, mount reuse, the design UI). The *only* way to create one is the DevTools "Raise Ground Unit" button with a **hardcoded preset** (`DevToolsWindow.cs:171-181, 809-830`). There is no base-mod ground-unit template and no player-facing ground designer. The `ComponentMountType.GroundUnit` flag (1<<6) is defined but **no UI offers or assembles GroundUnit-flagged components.**
- **PDC** (`1<<3`) — flag exists, no designer, no assembler, no window offers it.
- **Fighter** (`1<<4`) — flag exists; `IndustryDisplay` knows how to auto-install a Fighter-mount component onto a ship (`IndustryDisplay.cs:421`), but there is **no fighter *designer*** — the ship designer filters to `ShipComponent`, so a fighter can't be assembled as its own small-craft class.
- **ShipCargo** (`1<<1`) — has no dedicated designer; only surfaces in cargo/auto-install logic.

---

## Open questions / gaps for the next parts of the audit

1. **Are base-mod templates authored single-mount?** The client filters are single-flag, but the real lock may be upstream in the JSON (`GameData/basemod/blueprints/components/`). Part 2 (engine/data audit) must check whether any template carries >1 mount flag today, or whether multi-host is purely theoretical. If templates are all single-mount, fixing the client filters alone changes nothing.
2. **Should the four assemblers collapse into one host-parameterized assembler?** `StationWindow`'s reuse of `IndustryDisplay` suggests a single "assemble a `<host>` from mount-matching components" component is feasible. Worth a design note.
3. **Ground units: fold into the `ComponentDesign` pipeline or keep `GroundUnitDesign` separate?** The `GroundUnit` mount flag implies the original intent was components-on-a-ground-unit (like a ship). Deciding this is the crux of "ground combat gets the same depth as space." (See `CONVENTIONS.md` §6 "abilities are components".)
4. **PDC and Fighter** — are these deferred-by-design or forgotten? The flags exist with zero UI. Confirm against `docs/MVP.md` scope.
5. **`IndustryDisplay` auto-install mount switch** (`:413-425`) — is the `PlanetInstallation`-vs-ship branch the right place for host routing, or should install targeting also become a mount-intersection test so a multi-host component can be installed on whichever valid host is selected?
6. **The Ordnance designer's inline engine designer** (`OrdnanceDesignWindow.cs:205-206`) is a second, embedded instance of `ComponentDesigner` — confirm it can't drift from the main Component Designer's behavior.
