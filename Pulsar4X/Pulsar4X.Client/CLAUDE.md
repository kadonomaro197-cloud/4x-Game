# Pulsar4X.Client — UI Reference

ImGui.NET + SDL2 immediate-mode UI. The only runnable application in the solution. Lives in `Pulsar4X/Pulsar4X.Client/`.

> **⚠ READ FIRST — this client is RUNTIME CI-blind (COMPILE is now covered, 2026-06-28).** `ci.yml` now has a **`build-client` job that COMPILES this client on every push** — so a typo, a wrong overload, a bad `internal` access, or a missing `using` turns CI **red** instead of ambushing the developer's local Windows build (the recurring pain in the gotchas below — "broke the client build, CI couldn't catch it" — is closed). **BUT CI still cannot RUN it** (display-coupled; headless CI can't open a window), so rendering bugs, click crashes, NaN positions, freezes, and all *behavior* are still invisible to CI and surface only in the developer's local build + the `game_logs/` gauges. **So: compile errors → CI catches them now; runtime/behavior → still the developer's local gauge.** The compile backstop does NOT make a `Display()` that throws safe — the **de-risk-by-structure** discipline still governs runtime (it's what carried the detection/EMCON/fog work):
> 1. **Push logic into the engine, which is CI-tested (not just compiled).** Need a value the client can't reach? Add a small computed accessor on the engine type (e.g. `SensorContact.SignalStrength_kW`/`PositionIsMemory`) instead of new client logic — the engine has real *tests*, so the logic is verified, not just compilable.
> 2. **Verify reachability BEFORE writing.** Check access modifiers (`internal` engine fields are invisible across the assembly boundary) and that the type/overload actually exists — read the exact source region. A guess costs a full pull→build→paste→fix round-trip with the developer.
> 3. **Mirror a proven pattern verbatim.** New SDL text? Copy `EntityLabel`'s `RenderTextSolid`→texture→`RenderTexture` path exactly (incl. the finalizer that frees the texture). Don't improvise native interop.
> 4. **Wrap every new draw in the fault-isolator** (`SystemMapRendering.SafeDraw` / `PulsarMainWindow.SafeRender`) and guard position reads (a NaN/null `AbsolutePosition` throws) — so a glitch logs once and skips instead of blanking the map (gotchas #12/#14).
> 5. **The gauge IS the test.** You can't run it; the developer can. Leave a log line (`SessionLog` / `[RenderError]`) at each new code path so the play-test's `game_logs/` pages name what happened.

---

## Entry Point and Boot Sequence

```
Program.cs
    → new PulsarMainWindow()       // inherits ImGuiSDL2CSWindow
        → new GlobalUIState(this)  // singleton UI state
        → GalacticMapRender(...)   // initialise map renderer
    → PulsarMainWindow.Run()       // SDL2 event loop
        → Layout() called every frame
            → renders all active windows via Display()
```

---

## Directory Map

| Directory | Contents |
|-----------|---------|
| `IMGUISDL/` | SDL2 event loop, OpenGL context, ImGui bindings (`ImGuiSDL2CSWindow`, `ImGuiSDL2CSHelper`) |
| `Interface/Windows/` | All top-level game windows (see Window Inventory below) |
| `Interface/Displays/` | Reusable display panels embedded inside windows |
| `Interface/HUD/` | Always-visible HUD elements: toolbar, time controls, selector |
| `Interface/Menus/` | Main menu, new/load/save game, settings |
| `Interface/Widgets/` | Small reusable ImGui widgets (FileDialog, TextModal, ImageButton, Window helper) |
| `Interface/Themes/` | Visual themes (`FuturisticTheme`, `ITheme` interface) |
| `Rendering/` | Camera, system map rendering, OpenGL renderer, icons |
| `Rendering/Icons/` | Per-entity-type icon renderers (orbit ellipses, ship icons, star icons, warp lines) |
| `Rendering/ManuverNodes/` | Maneuver node drawing (delta-V planning on system map) |
| `State/` | UI state objects (GlobalUIState, SystemState, EntityState, OrderRegistry) |
| `ModFileEditing/` | In-game blueprint editor (components, minerals, tech, etc.) |
| `Input/` | Hot-key handlers for the system map |
| `Demos/` | ImGui demo/test screens |
| `CrashReports/` | Discord crash reporter |

---

## Window System

### Base Classes

| Class | Purpose |
|-------|---------|
| `PulsarGuiWindow` | Base for all UI windows. Has `Display()`, `IsActive`, `_lookedAtEntity`, `_uiState`. Unique (one instance per type). |
| `NonUniquePulsarGuiWindow` | Allows multiple instances — one per entity. Keyed by `"WindowType|entityId"` in `LoadedNonUniqueWindows`. |

### Window Registration

Windows are registered in `GlobalUIState.NamesForMenus` (a static dict mapping Type → menu label). This is what populates the right-click context menu on entities. To add a new window to the context menu, add an entry here.

Windows are stored in:
- `GlobalUIState.LoadedWindows: Dictionary<Type, PulsarGuiWindow>` — unique windows.
- `GlobalUIState.LoadedNonUniqueWindows: Dictionary<string, NonUniquePulsarGuiWindow>` — per-entity windows.

A window is opened by calling `StartDisplay()` (sets `IsActive = true`). It is closed by setting `IsActive = false`.

### ImGui Pattern

Every window follows the same immediate-mode pattern:
```csharp
internal override void Display()
{
    if (!IsActive || _lookedAtEntity == null) return;

    if (ImGui.Begin("Window Title", ref IsActive, _flags))
    {
        // render content here
        ImGui.End();
    }
}
```
`Display()` is called every frame. There is no state retained between frames in ImGui beyond what you store in class fields.

---

## Window Inventory

| Window | File | Status | Notes |
|--------|------|--------|-------|
| `SystemWindow` | `SystemWindow.cs` | ✅ Functional | Star system selector and planet list |
| `FleetWindow` | `FleetWindow.cs` | ✅ Functional | Fleet listing, selection, basic orders. **+ Combat tab (2026-06-25)** — see "Fleet Combat tab" below. **+ Force Management retitle + Battalions tab (Earthfall C3.1, 2026-07-18)** — the window title is now **"Force Management"** (the class stays `FleetWindow` — renaming it orphans `LoadedWindows`/save refs, R1 ledger), and its content splits across a top-level tab bar: **Fleets** (the entire existing fleet manager, verbatim, inside its own tab — byte-identical) and **Battalions** (the new cross-body ground-formation manager). See "Battalions tab" below. **+ Embark / Land Troops order (Earthfall C5.1, 2026-07-19)** — a new `IssueOrderType.Troops` in the Fleets ▸ Issue Orders tab (gated on the fleet having a troop-bay ship): per bay ship, embark the player's ground units standing on the body it orbits (`LoadTroopsOrder`) and land loaded units onto a region-picked target once it holds the orbit (`LandTroopsOrder`). See "Embark / Land Troops" below. |
| `ColonyManagementWindow` | `ColonyManagementWindow.cs` | ✅ **Full economy UI** (verified in code 2026-06-24) | Colony picker + tabs: **Summary** (planet/pop/infra-efficiency/installed components/stockpile of raw+refined), **Society** (2026-07-02 — morale+factors / legitimacy+rebellion / manpower / sustenance / tax→income / government, colour-banded; see below), **Production** (`IndustryDisplay` — queue refine/build jobs via `IndustryOrder2`: batch/repeat/auto-install/priority/cancel), **Construction**, **Mining** (per-mineral rate/annual production/years-to-depletion). The minerals→refined→components loop is fully see-and-do here. **Live-behaviour unverified** (CI can't build the client). |
| `PlanetaryWindow` | `PlanetaryWindow.cs` | ✅ (installations fixed 2026-06-24) | General info ✅ / Mineral deposits ✅ / **Installations ✅ — tab now gates on `ComponentInstancesDB` and renders via `componentsDB.Display(...)` (`:102,220`), NOT the dead `InstallationsDB`.** |
| `PlanetViewWindow` | `PlanetViewWindow.cs` | ✅ Tactical map 2026-07-04 (ground-map slice 5e — compile-checked, runtime-unverified) | The **planet surface as a NAVIGABLE tactical map** — the flat 3-region view of the 4-slice ring (`PlanetRegionsDB`) upgraded from readout to "put your hands on the ground war." Per-body window (context menu "Planet view (regions)", gated on `PlanetRegionsDB`); centre region + its two ring neighbours, rotate with ◀/▶ or by clicking a region. On top of the terrain bands it draws **units** (`GroundForcesDB` grouped per faction+type into tokens: type initial + count + health bar, cyan=yours / red=hostile, `»`=marching), **hazards** (`PlanetEnvironmentsDB` chips), **terrain class** (Open/Cover/Rough), and **buildings** (⚙ per `Region.InstallationIds`). Interactions: click a token → select group; click an adjacent region or a March button → `GroundForces.OrderMove`; Build panel places a `PlanetInstallation` design at the centre region via `PlaceInstallationInRegionOrder` → `Game.OrderHandler.HandleOrder`. Thin defensive draw — reads off CI-tested blobs, orders through CI-tested engine paths, body wrapped so a throw logs `[RenderError]` once and still runs `Window.End`, no hard-indexing. **Runtime render/feel unverified (CI can't run the client).** Design: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`. |
| `StationWindow` | `StationWindow.cs` | ✅ New 2026-07-03 (Slice A/A2 — compile-checked, runtime-unverified) | The space-station front door's management half — a per-entity window opened from the map context menu ("Manage Station", gated on `StationInfoDB`). Header (hosting body / structural-integrity durability pool / population / operating cost) + the **host-agnostic `IndustryDisplay`** so a deployed platform with a constructor can queue+install modules in-situ. Thin defensive draw: `Window.Begin/End` wrapper + try/catch body so a throw can't skip `End`. **Deploy side (Slice A2): a "Deploy Station Here" action on a CONSTRUCTION SHIP's context menu** (`EntityContextMenu`, gated on `ShipInfoDB`+`CargoStorageDB`+own-faction) → ship-issued `DeployStationOrder` anchors a station at the ship's location (star/belt/planet). The old survey-gated `SystemWindow` "Deploy Station" button was removed (couldn't reach a star). |
| `ShipDesignWindow` | `ShipDesignWindow.cs` | ✅ Functional (+ GROUND-unit branch 2026-07-15, compile-checked, runtime-unverified) | **The ENTITY ASSEMBLER** (window title + toolbar tooltip retitled from "Ship Design" 2026-07-14 — the developer's naming: the Component Designer makes the PIECES, this ASSEMBLES them into any buildable entity — ship / station / building / ground unit). Assembles a chassis + a list of components; stats emerge from the sum. **Now assembles BOTH ships AND ground units in the SAME window, branching on the CHOSEN CHASSIS** (rides the additive `Pulsar4X.Interfaces.IChassisAtb` seam — `ShipHullAtb` → Ship, `GroundChassisAtb` → Ground). Detection: `SelectedChassis()` scans the design's components for a part carrying an `IChassisAtb`; `IsGroundAssembly()`/`ActivePartMount()` read its `BudgetKind`/`PartMount`. An **"Assembling: Ship / Ground Unit" combo** (default Ship) bootstraps an EMPTY design (a chassis, once mounted, LOCKS the kind). The parts-list filter now uses the chassis's `PartMount` (Ship → `ShipComponent` as before; Ground → `GroundUnit`), so a ground chassis shows ground frames/parts. Ground SAVE (`SaveGroundDesign`) routes to `GroundUnitAssembly.RegisterAssembledDesign(faction, id, name, frame, parts)` (registers a buildable `GroundUnitDesign` on `IndustryDesigns`, NOT a `ShipDesign`); ground STATS/validity come from `GroundUnitAssembly.Compute` (`DisplayGroundStats` — carry-budget readout + emergent Attack/Defense/HP/etc. + assembler `Problems`), skipping the ship thrust/warp/energy math + the armor block + the armor-null throw. **The SHIP path is byte-identical** (combo defaults to Ship, no ground chassis → old behaviour: same filter, same stats, same save). Crash-hardened: `RefreshExistingClasses`/`DisplayExistingDesigns` no longer hard-index `ShipDesigns[id]` (all `TryGetValue`-guarded, L14); ground draw wrapped so a throw logs `[RenderError]` once and can't skip `Window.End`. Logs `[ACTION] ground-unit design saved…`. v1: generalising to every entity kind is in-progress (**same window, never a new per-domain one** — `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §0). Class name still `ShipDesignWindow`. **+ C4.1 (Operation Earthfall, 2026-07-19):** `DisplayGroundStats` gained three readout rows off `GroundUnitAssembly.Compute` — **Training** (`TrainingMultiplier`, cadre veterancy, previously invisible), and ALWAYS-ON **Power (draw / supply)** (`EnergyDemand_W`/`ReactorSupply_W`, red UNDER on shortfall) + **Ammo Capacity** (`AmmoCapacity_kg`); the power/ammo gates were violation-only red text before. Display-only (no new numbers, Problems verdict unchanged). **+ "Units per build" dial (squad size, 2026-07-21):** an `InputInt` in the ground Details section (`_groundUnitsPerBuild`, clamped ≥1) passed to `RegisterAssembledDesign(..., unitsPerBuild)` — one build of the design raises that many units; the engine scales the design's build cost + time by it (no free multiplication). Ground-assembly only; default 1 → byte-identical (ship path untouched). |
| `ComponentDesignWindow` | `ComponentDesignWindow.cs` | ✅ Functional | Component designer with NCalc formulas. **Left panel = the "Make New Component" taxonomy (reorganised 2026-07-14 to the DESIGN doc).** Two-level tree: **Category (11) → Door (37) → templates**, per `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` (§2), via the `ComponentDoors` map (`ComponentDoors.cs`: template-id → Category ▸ Door, with an "Other"/raw-ComponentType fallback so nothing vanishes). Click **Weapons** → Energy/Ballistic/Melee/Guided/Exotic; a cargo hold and a fuel tank both sit under **Logistical ▸ Storage**; a hull is **Chassis ▸ Hull**. Each template is a "＋ New \<template\>" that starts a BLANK design (`SetTemplate`, not a copy). Middle "Current Component Designs" list edits an existing design (`SetFromComponent`), reset when a door with no designs is picked. **NOTE:** the window only shows templates the VIEWED faction has UNLOCKED (`Data.ComponentTemplates`); the DevTest player now has all design templates unlocked (uef-devtest `startingItems`) so every door is populated — the earlier "can't build a weapon" was the trimmed weapon templates being locked. **Crash-hardened:** the engine `Description` getter falls back to raw text on an NCalc-unparseable description (apostrophe); the tooltip Description lookup is `TryGetValue`-guarded. |
| `FireControlWindow` | `FireControlWindow.cs` | ✅ Functional | Weapon and fire control assignment |
| `OrdnanceDesignWindow` | `OrdnanceDesignWindow.cs` | ✅ Functional | Missile design |
| `ResearchWindow` | `ResearchWindow.cs` | ✅ Functional | Research queue and tech tree |
| `LogisticsWindow` | `LogisticsWindow.cs` | ✅ Partial | Automated cargo routes |
| `NavWindow` | `NavWindow.cs` | ✅ Functional | Navigation planning |
| `WarpOrderWindow` | `WarpOrderWindow.cs` | ✅ Functional | Warp order issuance |
| `NewtonOrderWindow` | `NewtonOrderWindow.cs` | ✅ Functional | Newtonian thrust orders |
| `EntityInfoWindow` | `EntityInfoWindow.cs` | ✅ Functional | Generic entity data display |
| `DamageViewerWindow` | `DamageViewerWindow.cs` | ⚠️ Partial | Ship damage visualization |
| `CommanderWindow` | `CommanderWindow.cs` | ✅ Basic | People/commander display |
| `GalaxyWindow` | `GalaxyWindow.cs` | ✅ Functional | Galaxy map and system list |
| `SMWindow` | `SMWindow.cs` | ✅ Functional | Space Master debug controls |
| `DiplomacyWindow` | `DiplomacyWindow.cs` | ✅ New 2026-07-02 | Player-facing relationship ledger — toolbar button (next to the distance ruler). A colour-banded table of every met faction's stance / score / treaties, read from the player's `DiplomacyDB` (same data as Dump Society). Read-only; defensive (body wrapped so a throw can't skip `Window.End()`). Placeholder icon (`Img_Select`). |
| `IntelligenceWindow` | `IntelligenceWindow.cs` | ✅ New 2026-07-12 (Espionage E4 — the button; compile-checked, runtime-unverified) | The player-facing ESPIONAGE panel — toolbar button (next to Diplomacy). Four thin, defensive sections over CI-tested engine state: **Capacity** (directorates' op capacity + counter-intel, agents fielded — from `IntelDirectorateDB`), **Agents** (each operative's tradecraft `EspionageSkill01` + status idle/on-op from `CovertOpDB`), **Assign** (combos of idle agent × met rival × `IntelFacet` + a "Task: Gather Intel" button → `Espionage.TaskAgent`, the CI-tested path), and **Intel ledger** (per rival × facet colour-banded level from `InformationLedgerDB`). Mirrors `DiplomacyWindow` exactly (GetInstance + `Window.Begin`/try/`End`). All writes go through `Espionage.TaskAgent`; the client stays a thin draw. **Live render/feel is the developer's local build (CI compiles the client, can't run it).** Placeholder icon (`Img_Select`). |
| `SiteWindow` | `SiteWindow.cs` | ✅ New 2026-07-13 (Site Engine SE-6b — compile-checked, runtime-unverified) | The player-facing **Field Sites** panel — toolbar button (next to Diplomacy/Intelligence). Lists every discovered `FieldSiteDB` across the player's known systems (`_uiState.StarSystemStates` → each `StarSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>()`): per site a collapsing header (name / space-or-surface / status) + Role/Shape/Yield, work banked + understanding accrued, who's working it, and — for a **branched** site (`FieldSiteDB.HasBranches`) — one **Commit: \<branch\>** button per branch (enabled only when `SiteMachine.IsBranchUnlocked` AND the player's faction worked it), issuing the CI-tested `Sites.CommitSiteBranchOrder` via `Game.OrderHandler.HandleOrder` — the piece that makes the SE-5 branch CHOICE reachable by a player (a branched site waits at Worked until you pick here). Single-path demo sites show a progress readout (watch understanding climb → resolve). Thin defensive draw, mirrors `DiplomacyWindow` (GetInstance + `Window.Begin`/try/`End`, no hard-index). Placeholder icon (`Img_Select`). **Runtime feel is the developer's local build (CI compiles the client, can't run it).** |
| `AIInspectorWindow` | `AIInspectorWindow.cs` | ✅ New 2026-07-14 (B4b — the observability spine; compile-checked, runtime-unverified) | The live view of the **AI flight recorder** — toolbar button (next to Diplomacy/Intelligence). For every NPC faction: its current objective + why (`PlanReadout.Faction`) and a scrolling **decision tape** (each cycle's `AIDecisionRecord` as a line — what it SENSED/DECIDED/ACTED, via `PlanReadout.DecisionLine`), read off the per-faction `AIDecisionRecordDB`. The SAME data the client flushes to `game_logs/` as `[AI]` lines (`SessionLog.AiDecisionSnapshot`, from the ~3 s heartbeat) — the log tape is primary, this is the convenience view. Thin/defensive, mirrors `DiplomacyWindow` (GetInstance + `Window.Begin`/try/`End`, unconditional `EndChild`, no hard-index). **The decision tape's child now carries `ImGuiWindowFlags.HorizontalScrollbar` (2026-07-17)** — each `[AI]` line is ~300 chars (sensed \| decided \| acted) drawn UNWRAPPED via `TextUnformatted`, so without it the right half was clipped; the scrollbar lets you scroll right to read the whole line (the developer's "see what the AIs are doing in their entirety"). **Runtime is the developer's local build (CI compiles, can't run).** |
| `BattleReportWindow` | `BattleReportWindow.cs` | ✅ New 2026-06-27 | Persistent recent-battles readout — reads the engine `Combat.BattleLog`; survives after a fight ends. The "review a battle you missed" window. **AUTO-OPENS on the combat interrupt** (`PulsarMainWindow.PostFrameUpdate` — pops it + selects the player's engaged fleet in `FleetWindow` so the Combat/doctrine tab is one click away; uses the real `PlayerFaction`, so it works outside SM). Also opens from DevTools → "Open Battle Report". |
| `Debug/*` | `Debug/*.cs` | ✅ Dev tools | Data viewer, blueprint inspector, entity inspector, performance monitor |

---

## GlobalUIState

The central hub for all UI state. Key fields:

```csharp
Engine.Game Game                          // reference to the running game
internal Entity? Faction                  // the currently VIEWED faction entity (internal + nullable; SM mode swaps it to the Game Master — see gotcha #10). GlobalUIState.cs:65
internal Entity? PlayerFaction            // the real player faction; stays put when Faction is swapped in SM mode. GlobalUIState.cs:77
StarSystem SelectedSystem                 // active star system view
string SelectedStarSysGuid               // key into StarSystemStates
Dictionary<string, SystemState> StarSystemStates
GalacticMapRender GalacticMap
SafeList<UpdateWindowState> UpdateableWindows
```

`EntityClickedEventHandler` fires when the player clicks an entity on the system map. Connected windows listen to this to update their displayed entity.

---

## Rendering Architecture

```
GalacticMapRender               ← galaxy-level (zoomed out, systems as dots)
    └─ SystemMapRendering       ← system-level (planets, ships, orbits)
           ├─ Camera            ← pan/zoom state, world↔screen coordinate transforms
           ├─ OpenGLRenderer    ← draws lines/triangles via SDL2+OpenGL
           └─ Icons/
               ├─ OrbitEllipseIcon   ← Kepler orbit visualization
               ├─ ShipIcon           ← ship position dot
               ├─ WarpMovingIcon     ← warp transit line
               ├─ NewtonMoveIcon     ← burn arc
               └─ SysBodyIcon        ← planet/star dot
```

Icons implement `IRenderer` and are created per-entity. They read their entity's `PositionDB` each frame to update position. Icon instances are stored in `SystemState`.

`ManuverNodes/` — the delta-V planning interface drawn on the system map when the user is creating maneuver orders.

---

## Display Panels (Interface/Displays/)

Reusable panels embedded in windows (not standalone windows):

| Panel | File | Purpose |
|-------|------|---------|
| `ColonyInfoDBDisplay` | `ColonyInfoDBDisplay.cs` | Population table |
| `ColonyPanel` | `ColonyPanel.cs` | Full colony overview panel |
| `IndustryDisplay` | `IndustryDisplay.cs` | Production queue display |
| `IndustryPanel` | `IndustryPanel.cs` | Full industry panel |
| `CargoStorageDBDisplay` | `CargoStorageDBDisplay.cs` | Cargo hold contents |
| `ComponentInstancesDBDisplay` | `ComponentInstancesDBDisplay.cs` | Installed components list |
| `MineralsDBDisplay` | `MineralsDBDisplay.cs` | Mineral deposits table |
| `AtmosphereDBDisplay` | `AtmosphereDBDisplay.cs` | Atmospheric composition |
| `CargoTransfer/` | `CargoTransferWindow.cs` etc. | Drag-and-drop cargo transfer |

---

## Adding a New Window

1. Create `Interface/Windows/MyNewWindow.cs` inheriting `PulsarGuiWindow` (unique) or `NonUniquePulsarGuiWindow` (per-entity).
2. Implement `Display()` with ImGui calls.
3. Add a static `GetInstance()` factory following the `PlanetaryWindow.GetInstance()` pattern.
4. Register in `GlobalUIState.NamesForMenus` to add it to the context menu, OR call `GetInstance(entity, state).StartDisplay()` from a toolbar button.
5. Ensure `IsActive` is set to `false` on close — ImGui does this automatically via the `ref IsActive` on `ImGui.Begin()`.

---

## Critical Gaps to Fill

### PlanetaryWindow installations — FIXED (2026-06-24); colony economy UI already exists

This section used to describe `PlanetaryWindow.RenderInstallations()` as empty and unreachable (gated on the
dead `InstallationsDB`). **Both are fixed in the current code:** the Installations tab gates on
`ComponentInstancesDB` and `RenderInstallations()` calls `componentsDB.Display(...)` (`PlanetaryWindow.cs:102,220`).
And the **full colony economy UI lives in `ColonyManagementWindow`** (Summary / Production / Construction /
Mining — see the Window Inventory). The minerals→refined→components loop is already see-and-do.

**So this is not a build task — it's a *verify* task.** CI can't build the client, and these docs were stale, so
the only way to know the real state is to run it (see `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` §5B). If something
is actually broken live, fix *that* — don't rebuild panels that already render. `InstallationsDB` itself remains
dead/vestigial; do not resurrect it.

### Fleet Combat tab (FleetWindow) — BUILT 2026-06-25 (the space-combat UI starting point)

The space auto-resolve engine had **no client UI** — battles ran invisibly and ships just vanished. The first
piece of the real combat UI is a **"Combat" tab on `FleetWindow`** (between Summary and Issue Orders), shown the
moment a fleet — or a sub-fleet "component" — is selected. It is the in-client realisation of COMBAT-DESIGN
System 4's "extend the Fleet panel; the table IS the interface." Three sections (`DisplayCombatTab` →
`DisplayCombatStatus` / `DisplayDoctrineSelector` / `DisplayFleetCombatSheet`):

1. **Status** — the live battle readout. Reads `FleetCombatStateDB`: "● IN COMBAT — salvo N", the representative
   opponent (`OpponentFleetId` → name + ship count), ships `alive of started (lost X)`, and the incoming
   `DamageTakenPool`. Falls back to `FleetRetreatDB` ("withdrew") or "Not engaged".
2. **Doctrine** — the player's lever. Shows the active `FleetDoctrineDB` and a dropdown of the moddable catalog
   (`Game.StartingGameData.CombatDoctrines`); **Set** calls `FleetDoctrine.TrySetDoctrine` (a **direct call, not an
   order**, so it bypasses the engagement lock and works mid-battle). The button greys out with a game-time
   countdown while `SwitchableAfter` (the switch cooldown) is in the future.
3. **Combat sheet** — fleet totals (firepower J/s, toughness J, combatant count), firepower broken down by weapon
   class (from each ship's `ShipCombatValueDB.Weapons`), and the per-ship table (role / firepower / toughness /
   evasion). Per-component doctrine falls out for free: selecting a sub-fleet in the tree makes it the selected
   fleet, so the tab then shows/sets THAT component's posture.

**Connections (Prime Directive):** reads `ShipCombatValueDB`, `FleetDoctrineDB`, `FleetCombatStateDB`,
`FleetRetreatDB`, the `CombatDoctrines` catalog; writes nothing directly except via `FleetDoctrine.TrySetDoctrine`.
All reads are defensive (`TryGet` + `IsValid` + snapshot-to-array) because the background combat processor mutates
this state on another thread — and a ship killed mid-battle lingers in the fleet's child list with `IsValid=false`
until cleanup, so alive/loss counts **filter on `IsValid`** (don't drop that filter).

**Update (2026-07-03) — a New Game now has fleets AND enemies by default.** Two things changed the "empty New Game" situation gotcha 8 described: (a) the start colony blueprint now nests its own `Fleets` (Freight/Military/Science), which `ColonyFactory` builds on the wizard path, so a New Game gives you controllable ships; and (b) `NewGameMenu.CreateGameCore` can auto-run `CombatSandbox.SpawnCombatScenario` on New Game/Quickstart (gated on `NewGameMenu.AutoSpawnCombatScenario`) — four rival factions + capital-led squadrons at Luna/Venus/Mercury/Mars plus two player task forces at Earth. **As of 2026-07-06 this defaults OFF (BAREBONES verification pass — the developer's call): a New Game is stripped to the core (faction + Earth colony + a fogged Sol) so a hands-on play-through surfaces real issues, not auto-injected content.** Companion flags ALSO default OFF: `AutoRaiseHomeGarrison` (no start garrison) and **`AutoBuildStartFleets`** (no start ships at all — the colony blueprint's Freight/Military/Science fleets are skipped; `CreateFromBlueprint(..., buildFleets)` gates them, default true so tests/benchmarks keep fleets). So a barebones New Game is genuinely minimal — build your own ships. Re-enable any per playtest via DevTools ("Auto-spawn this scenario on New Game" / "Auto-raise home garrison on New Game" / "Build start fleets on New Game"). So the manual spawn workflow below is the way to stand up a fight now. Enemies sit at other bodies, so nothing auto-engages on spawn — you sail out (or issue an Attack order) to start a fight.

**Testing caveat — sections 2 and 3 verify on an IDLE fleet (no enemy needed); section 1 needs a live battle.**
A fresh New Game has controllable fleets and (by default) hostile factions; the manual enemy-spawn tooling also exists
— **DevTools → "Combat Sandbox" → Spawn Hostile Fleet** (a thin wrapper over the CI-proven
`Combat.CombatSandbox.SpawnHostileFleet`), plus a **"Tick Combat (force a salvo)"** button that drives
`CombatEngagement.Tick` manually. To exercise the whole thing: Fleet window → *Create New Fleet* → DevTools (SM
mode) → *Spawn Ship* a few armed designs (Lancer/Bulwark/Wasp/Leviathan) into it → set "Orbit around" to that
body → **Spawn Hostile Fleet** (same body) → exit SM → select your fleet → **Combat tab**. Press play (or click
*Tick Combat*) and watch the **Status** section come alive: salvo counter, ships lost, damage pool — and switch
doctrine mid-fight to steer it. CI can't build the client, so this is a build → play → read `console_output.txt`
(look for `[FleetCombat]` + `[DevTools]` lines) loop.

**Open live question (CI can't settle):** the engine gauge `CombatSandboxTests` proved the spawned enemy *persists*
through a clock advance and *is engageable*, but the lightweight test harness didn't auto-fire the battle trigger
on a clock advance — so whether **pressing play** auto-starts the battle in the full game is unconfirmed. If it
doesn't, the **Tick Combat** button drives the fight manually (and tells us the trigger scheduling, not the combat
math, is what needs a look). See `GameEngine/Combat/CLAUDE.md` → "Combat sandbox".

### Battalions tab (FleetWindow) — BUILT Earthfall C3.1 (2026-07-18) — battalions beside fleets

The ground echo of the fleet manager, and the reason the window is now **"Force Management"**: a **"Battalions" tab**
on `FleetWindow` (beside "Fleets") that makes ground formations first-class citizens the player can command from ONE
place, across ALL their worlds — the same way the Fleets tab commands ships. `DisplayBattalions()`:

1. **Cross-body registry** — enumerates every world the player knows that carries a `GroundForcesDB`
   (`_uiState.StarSystemStates` → each `StarSystem.GetAllEntitiesWithDataBlob<GroundForcesDB>()` →
   `GroundFormationTools.FormationsFor(forces, factionId)`), the exact pure-client enumeration precedent `SiteWindow`
   uses. **There is no engine cross-body helper yet** (`GroundFormationTools.AllFormationsFor` is a GROUND follow-up);
   the client sums per-body `FormationsFor` itself. The engine contract is CI-pinned by
   `EfC3BattalionRegistryTests` (two formations on two bodies both collected + aggregated, enemy excluded).
2. **Table** — Battalion (name + member count) / World / Region (`GroundForces.LeaderRegion`) / Strength
   (`FormationStrength`) / Health (`FormationHealth`) / Reach (`FormationReachHexes` in hexes) / Stance / ROE, with
   **filters** (system combo / world combo / "with orders only" checkbox). A `Selectable` row selects the battalion
   (keyed by body-id + formation-id across bodies).
3. **Order surface (copies `PlanetViewWindow.DrawFormationPanel` idioms verbatim)** — for the selected battalion:
   **march** to an adjacent region (`GroundForces.OrderFormationMove`), a **queue** panel (`QueueFormationOrder` for
   MoveRegion/Hold/ROE waypoints + `ClearFormationOrders`), a **stance** selector (moddable `GroundStances` catalog →
   `GroundFormationDoctrine.TrySetStance`, game-time cooldown-gated), and an **ROE** selector
   (`GroundFormationDoctrine.SetEngagementStance` — Hold/Close/Stand-off). All **direct CI-tested engine calls** on an
   explicit player click, exactly what the planet view already does.
4. **Jump to the world** — an **"Open planet view"** button resolves the body's `EntityState` via its `SystemState`
   (`sysState.GetEntityById(body.Id)`) and opens the per-body `PlanetViewWindow` (`GetInstance` + `SetActive(true)` —
   the open-window-for-entity mechanic), so the manager and the surface tactical map are one click apart.

**Connections (Prime Directive):** reads `GroundForcesDB` / `GroundFormation` / `GroundFormationTools` (per-body
formations + aggregation), `PlanetRegionsDB` (region graph for march/queue), the `GroundStances` catalog; writes
nothing except through the CI-tested `GroundForces` / `GroundFormationDoctrine` order APIs — the same ones
`PlanetViewWindow` uses. **Thin + defensive:** the whole tab body is wrapped in a try/catch so a throw logs
`[RenderError]` once and still runs `EndTabItem` (the colony-window cascade lesson), and nothing is hard-indexed.
The **Fleets tab is byte-identical** — the existing fleet layout was moved verbatim into its own tab item; the class
stays `FleetWindow` (renaming orphans `LoadedWindows`/save refs). **CI compiles the client but can't run it — live
render/feel is the developer's build.**

> **PW.2 (2026-07-21) — the deferred cross-lane buttons LANDED (both windows).** The battalion order surface
> (`DrawBattalionOrders`) now has: (a) a **Rename** control (inline `ImGui.InputText` + button → `GroundForces.
> RenameFormation` — a formation is a DATA object so it can't use the entity-only `RenameWindow`, R1 gap 2; the buffer
> reseeds from the selection's name via `_battRenameForId`), and (b) **Infrastructure combat** buttons
> (`DrawBattalionInfraOrders`) — "Raze / Capture infrastructure" that QUEUE `GroundOrder.DestroyInfra` /
> `CaptureInfra(rally, 0, 0)` on the battalion's own leader region (footprints sit on the region-centre hex 0,0; the G3
> order's range gate needs a unit standing IN that region, so the leader region is the only valid target). Both gated on
> the region actually holding footprint buildings (read off `Region.Hexes` → the (0,0) hex's `InstallationIds`). The
> `PlanetViewWindow` formation panel + city-zoom got the twins (see the PlanetViewWindow section). Engine surface pinned
> by `EfPwInfraButtonContractTests` (rename + factory field wire + the QUEUE-path resolve). Runtime is the developer's
> local build (CLIENT-TEST-CHECKLIST "OPERATION EARTHFALL — PW.2").

### Embark / Land Troops (FleetWindow ▸ Issue Orders) — BUILT Earthfall C5.1 (2026-07-19) — the invasion control panel

The player half of the invasion **lift** step (MVP Stage 4). `LoadTroopsOrder`/`LandTroopsOrder` had **zero** client
surface — the engine, the AI (`ConquerResolver`), and the tests all issued them, but a *player* had no button (R2
ledger §6, "the missing control panel"). This adds the FleetWindow Issue-Orders path for it — the exact
`MoveToSystemBodyOrder` idiom (`GetFilteredEntities`/button → `CreateCommand` → `Game.OrderHandler.HandleOrder`), one
verb over.

A new **`IssueOrderType.Troops`** ("Embark / land troops ...") appears in the Fleets ▸ **Issue Orders** left list,
**gated on `HasAnyTroopBay(fleet)`** (a ship carrying a `GroundBayAtb` → non-zero `GroundTransport.BayCapacity`) — the
same show-only-if-capable gating the Geo/Grav-survey orders use. `DisplayTroopOrders()` lists each troop-bay ship in
the selected fleet as a collapsing header with:
- **Bay-capacity readout** (`DrawShipBayCapacity`) — used/free per class (Troop bay = Personnel, Vehicle bay =
  armour/artillery), summed on demand from the ship's installed bays (`GroundTransport.BayCapacity`/`UsedCapacity`).
- **Embark** (`DrawEmbarkSection`) — the player's own `GroundUnit`s standing on the **body the ship orbits** (its
  `PositionDB.Parent`), each with its carry-class + carry-size, and a **Load** button (greyed via
  `GroundTransport.CanLoad` when there's no room of that class, with a "needs N room, M free" note) issuing
  `LoadTroopsOrder.CreateCommand(ship, body, unit.UnitId)`.
- **Land** (`DrawLandSection`) — the units aboard (`GroundTransportDB.LoadedUnits`), a **region picker** combo over the
  target body's `PlanetRegionsDB.Regions` (default Region 1 when the body has no region layer; the choice rides on the
  order's `RegionIndex`, verified), an **orbital-control status** line (`GroundTransport.HasOrbitalControl` — a foreign
  ship over the body **disables** the Land buttons, "win the space first"), and a **Land** button per loaded unit issuing
  `LandTroopsOrder.CreateCommand(ship, targetBody, unit.UnitId, regionIndex)`.

**Connections (Prime Directive):** reads `GroundTransport` capacity helpers, `GroundForcesDB.Units`,
`GroundTransportDB.LoadedUnits`, `PlanetRegionsDB.Regions`; writes nothing except through the two CI-tested orders on
an explicit click. **Thin + defensive** by the client discipline — the orders themselves re-check every precondition
(at-body / bay room / orbital control), so a stale click is a safe no-op; nothing is hard-indexed. `[troops]`
SessionLog lines gauge each load/land. **Engine byte-identical** (a new order type + draw methods only; no engine file
touched). CI pins the exact contract the buttons draw against in `EfC5TroopLiftOrderTests` (overload arities, the
per-class capacity readout, the region-picker `RegionIndex` wire). **CI compiles the client but can't run it — the
full click-path (embark Earth marines → land on Mars) is on `docs/CLIENT-TEST-CHECKLIST.md`.** `C5b` (infra
Destroy/Capture buttons) **LANDED in PW.2** (needed GROUND's `DestroyInfrastructure`/`CaptureInfrastructure` enum) —
now in the battalion order surface (Force Management + PlanetViewWindow formation panel) AND the PlanetViewWindow
city-zoom infra section. See the "PW.2" notes on the Battalions-tab section above + the PlanetViewWindow section below.

### EMCON posture + fog-of-war UI (FleetWindow + DevTools) — BUILT 2026-06-26 (detection stack, slices A)

The engine-side detection/EMCON stack (fog of war, EMCON posture, activity heat, first-strike, grave rung) is
all CI-green; this is the client lever + toggle to **drive and observe** it. **CI can't build the client, so this
is build → play → read the rolling log pages under `game_logs/` (`[FleetCombat]`/`[DevTools]` lines) — unverified live until then.**

1. **EMCON posture selector** — `FleetWindow.DisplayEmconSelector()` (Combat tab, between Doctrine and the combat
   sheet). Mirrors the doctrine selector exactly: shows the fleet's current posture + signature multiplier, a
   Full/Cruise/Silent combo, and a **Set Posture** button that calls `FleetEmcon.SetPosture(SelectedFleet, posture)`
   (a **direct call**, like doctrine, so it works mid-battle). All reads are defensive (`FleetEmcon.PostureOf` /
   `MultiplierOf` return a Full/1.0 default for a fleet with no posture).

   **Engagement-posture selector (closing P3, added 2026-06-27)** — `FleetWindow.DisplayEngagementPostureSelector()`
   (Combat tab, between EMCON and the combat sheet). The PLAYER's half of the first-shot rule: a Weapons Free /
   Hold Fire / Return Fire combo + **Set Engagement** button calling `FleetDoctrine.SetEngagementPosture(SelectedFleet,
   posture)` (direct call, works mid-battle). Without it the player was stuck on the WeaponsFree default and could
   never *hold fire* — so the P3 **standoff** (two hold-fire fleets sit in range without a battle, `CombatEngagement.cs`
   first-shot gate) was unreachable from the player side. This is the lever that makes the standoff a real player
   decision. Only bites when DevTools' **First-shot trigger** (`RequireWeaponsReleaseToEngage`) is on; with it off,
   posture is inert and everyone fights on proximity. Mirrors the EMCON selector verbatim (CI-blind — local build only).
2. **Fog-of-war toggle** — `DevToolsWindow` "[ Detection / Fog of War ]" section: a checkbox bound to
   `CombatEngagement.RequireDetectionToEngage` (default off). On → combat is detection-gated and first-strike is
   live (the side that sees first shoots first). Plus a **live signature readout** of the clicked entity's
   `SensorProfileDB.ActivityMultiplier` (watch it climb when a ship runs hot / thrusts / fires, drop when Silent).
3. **Logging — the detection/EMCON state shows up in the rolling log pages under `game_logs/` (so a remote review can see what you saw).**
   `SessionLog.DetectionSnapshot(system, faction)` runs inside the **~3 s heartbeat** (and on demand via the DevTools
   **"Dump Detection (log)"** button), writing three lines:
   - `[ENGINE]` — **processor liveness**: `sensor scans N (+delta), battle-trigger passes M (+delta)`, read from
     `SensorScan.ScanCount` / `CombatEngagement.TickCount`. This is the load-bearing one: if these don't climb while
     ships are present, the engine is DEAD — it tells "the scan never fired / the trigger never ran on play" (both
     documented live unknowns) apart from "running but nothing to see." Without it, both look like "nothing happened."
   - `[DETECT]` — contacts held + the FOG GAP (how many other-faction ships are present vs how many you detect, rest
     "hidden from you").
   - `[EMCON]` — your ships' signature summary (how many run hot/dark/blind, plus loudest/quietest by name).
   Plus, on the engine side, `[Combat]` now narrates an explicit **FIRST-STRIKE** line when an asymmetric battle
   forms (one side blind). Read-only, wrapped in the heartbeat's `SafeRender`.
4. **Contact blips + unit fog of war — BUILT 2026-06-26 (closes the prior GAP).** The map now renders the viewed
   faction's DETECTED foreign units as limited-info **contact blips**, and HIDES undetected foreign units — the
   visual half of fog of war ("everyone sees the same star; not everyone sees the fleet around it"). Gated on the
   existing `CombatEngagement.RequireDetectionToEngage` flag (**ON in the client as of 2026-06-27** — set true in
   `PulsarMainWindow` after a play-test showed an undetected Venus contact was visible on a move order) — the same
   one switch as detection-gated combat, so "fog of war" is one toggle for both behaviour and visuals (DevTools ›
   Detection / Fog of War toggles it back off live).
   - **Blip:** `SensorContactIcon` (`Rendering/Icons/SensorContactIcon.cs`) — a real `Icon` subclass fed by the
     engine's `SensorContact` (position is the contact's last-known `SensorPositionDB`, which is an `IPosition`, so
     it drops straight into the `Icon(IPosition)` ctor). A diamond marker (red = rival; sized a touch by signal
     strength) + a name label drawn with the same SDL TTF path `EntityLabel` uses. When the target is gone and the
     contact coasts on its last-known ("memory") position, the blip fades and the label reads "(last known)" — the
     grave rung made visible.
   - **Refresh:** `SystemMapRendering.UpdateContactBlips()` rebuilds `_contactIcons` from `_sensorMgr.GetAllContacts()`
     every frame (cheap; contacts are few), skipping your OWN ships and neutrals; drawn via `DrawIcons` (SafeDraw-
     wrapped, so a glitchy blip logs once and skips). Cleared on faction/system switch (`ClearContactBlips`).
   - **Hide half:** a guard at the top of `AddIconable` skips the real icon + label + orbit/move trail for a
     foreign-faction MOBILE unit (ShipInfoDB/ProjectileInfoDB/BeamInfoDB) when fog is on — so a rival ship never
     draws as a full unit; it appears ONLY as a blip, and only once detected. Bodies (stars/planets/moons/JPs),
     your own units, and neutrals are unaffected. The engine accessors the blip needs (`SensorContact.PositionIsMemory`,
     `.SignalStrength_kW`) are CI-covered (the client can't reach the engine's internal detection fields directly).
   - **v1 limits (flagged):** every rival contact reads "hostile/unknown" (no IFF/diplomacy model yet — politics is
     a later problem); toggling the flag mid-session only affects entities added/updated AFTER the toggle (the real-
     icon hide is event-driven at add-time), so toggle fog BEFORE spawning for a clean test; the on-map ID never
     hides the *name* (the engine hands you the name on detection — true "unknown blip until you resolve it" needs
     the detection-QUALITY signal, which is currently degenerate — see `GameEngine/Sensors/CLAUDE.md` →
     "Detection-quality bug"). Built defensively given the map-render crash history (gotchas #12/#14): every blip
     draws through `SafeDraw`, and the blip's `OnFrameUpdate` swallows a bad-position throw so one stale contact
     can't abort the frame.

### Range/info readouts — engagement range, sensor reach, delta-V, ETA (BUILT 2026-06-27)

Closed part of the gap between what the sim KNOWS and what it tells the player (`docs/combat/INFORMATION-DELTA-DESIGN.md`). All reads go through CI-covered engine accessors (`WeaponUtils.GetMaxBeamRange_m` / `SensorTools.SelfDetectionRange_m`), so the client stays a thin draw — **CI can't build it, unverified until the local build.**
- **Fleet Combat tab** (`FleetWindow.DisplayFleetCombatSheet`): a fleet "Beam reach" row + per-ship "Beam Range" / "Sensor Reach" columns. Plus a **"Show range rings on map"** checkbox → `BuildRangeRings`/`ClearRangeRings`: draws **3 rings PER FLEET** (not per ship — the perf + clarity fix, 2026-06-27): beam reach (red) + sensor reach (green — blue is already used in-game) + detectability (amber), each sized off the ship with the HIGHEST of that range and centred on the fleet's first ship, as `SimpleCircle`s in `SystemMapRendering.UIWidgets` — **the exact DebugWindow "Draw SOI" mechanism, so no new SDL drawing code.** Radius is in **AU** (`SimpleCircle`'s unit — convert metres with `Pulsar4X.Orbital.Distance.MToAU`). Rings rebuild on fleet-selection change **AND when the fleet's loudness changes** (`FleetActivityFingerprint` — sum of member ships' `SensorTools.CurrentActivityMultiplier`, rounded), so flipping EMCON Silent/Full shrinks/grows the amber detectability ring **live** instead of needing a re-toggle. The same fingerprint invalidates the combat-sheet's per-ship range cache (`EnsureRangeCache`), so the **"Detectable at" number updates with posture too** — this was the developer's "I went Silent and nothing changed numbers-wise or visually" report (2026-06-27): the engine WAS dropping the signature to x0.15, the readout/ring just weren't re-reading it. Sensor reach (green) + beam reach (red) don't move with EMCON, but ride the same refresh. Note: `FleetWindow` imports `System.Numerics`, so `Distance`/`SDL.Color` are **fully-qualified** to dodge a `Vector2` ambiguity.
- **Fire Control** (`FireControlWindow.ShowRangeToTarget`): range-to-target vs. the ship's beam reach + a red **OUT OF RANGE** flag — fixes the silent no-fire (a weapon past `MaxRange` just didn't fire, no feedback). Position read wrapped in try/catch (a mid-warp/detached `AbsolutePosition` can throw).
- **Warp Order** (`WarpOrderWindow`): "Available Δv" + "ETA / arrive" at top level, from `_maxDV` + `_targetIntercept.eti` the window already computed but never printed.

### All-ranges always-on — every unit + place shows its reach rings (2026-06-28)

The per-fleet "Show range rings" checkbox (above) only drew the SELECTED fleet's rings, and only while the Combat
tab was open — the developer wanted "can all units and places just have their ranges on display and active." Now a
global, default-ON mode draws reach rings for **every own unit + place**, no selection needed.
`SystemMapRendering.UpdateAllRangeRings()` (called each frame from `Update()`, gated on `GlobalUIState.ShowAllRangeRings`,
default true; toggle off in DevTools › Detection / Fog of War if cluttered):
- **Units** = every own ship drawn as its OWN icon — lone ships + each fleet's representative (it reuses the
  per-frame `_collapsedFleetMembers` set, so rings land on exactly the ships that show as icons, one ring-set per
  fleet marker). Three rings each: beam reach (red, how far it can SHOOT), sensor reach (green, how far it can SEE),
  detectability (amber, how far it can BE SEEN).
- **Places** = every own colony — one green detection ring sized by `SensorTools.DetectionRangeAgainst(colony,
  referenceShip)` where the reference is a real foreign ship (else one of your own): "how far this place detects a
  ship LIKE THIS." That's Earth's ~230 Gm megasensor bubble that covers the inner system — what lets the homeworld
  see contacts at Mercury/Mars, the "colonies are system-wide early warning" decision made visible. **(Fixed
  2026-06-28: the first cut used `SensorReachRange_m(colony)`, which measures "detect a thing as loud as a COLONY",
  not "as loud as a ship" — so the ring came out tiny and didn't reach the inner planets. The reference-ship metric
  is the honest one.)**
- **Cheap by construction:** the ring centre is the entity's LIVE `PositionDB`, so each ring TRACKS its ship as it
  moves — **no per-frame rebuild**. Rebuilds only when the SET of units/places or their loudness (EMCON) changes,
  via a fingerprint (positions deliberately excluded). `SimpleCircle` culls off-screen segments (the zoom-stutter
  fix), so a huge colony ring costs nothing when out of view. Keys are `allrange_*`, distinct from the Combat tab's
  `rangering_*`, so the per-fleet checkbox (which adds the enemy-target detection bubble) still works alongside it.
- Values come from CI-covered engine accessors (`WeaponUtils.GetMaxBeamRange_m`, `SensorTools.SensorReachRange_m` /
  `DetectabilityRange_m` / `CurrentActivityMultiplier`); the client just enumerates + draws. **CI-blind — local
  build only.** A `[range-ring] all-ranges rebuilt: N unit(s) + M place(s)` SessionLog line gauges the wire.
- **v1 limits (flagged):** a fleet representative's rings are sized off the FLAGSHIP's own ranges, not the fleet
  max (the Combat tab's per-fleet builder does fleet-max; the fleet-max refinement here is a follow-up); enemy
  ranges are never drawn (fog — you don't know them).

> **Honest colony detection BAND (Earthfall C2.1, 2026-07-18).** The colony's green detection ring
> (`SystemMapRendering.UpdateAllRangeRings`) used to be sized against ONE arbitrary reference ship, so a LOUDER
> genuinely-detected contact rendered OUTSIDE it (the "saw them before sensor range" report, findings/A2-ghost-contacts).
> It now draws a min/max BAND sized against the ACTUAL foreign ships present: OUTER ring = max
> `DetectionRangeAgainst(colony, fs)` over foreign ships (nothing detected can fall outside it), INNER = min (drawn
> only when meaningfully tighter). The ring-rebuild fingerprint now includes the foreign set + loudness so it rebuilds
> on enemy appear/leave/EMCON. No foreign ship → the byte-identical pre-C2.1 single ring sized vs a ship-like reference
> (`DetectionRangeAgainst(colony, firstForeign ?? firstOwnShip)`, falling to `SensorReachRange_m(colony)` only when
> there are zero ships at all). Companion: `SessionLog.SplitHeldVsFresh` (default off) splits the `[DETECT]` log into
> fresh (LAGGED) vs held-stale (FROZEN) contacts — readout only, the hostile-engage gate is unchanged. No engine value
> changed; the 200 Gm horizon design call lives in `docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md`.

### "Attack" button — order a fleet to engage (FleetWindow Combat tab, 2026-06-27)

`FleetWindow.DisplayEngageButton` ("Attack nearest hostile fleet") gives the player the explicit **engage** order
that was missing — for when two fleets sit in range "staring at each other" (one holding fire, or an enemy that
broke off so the auto-trigger won't re-grab it). Calls `Pulsar4X.Combat.CombatEngagement.OrderAttackNearestHostile(
SelectedFleet)` (a **direct call**, like doctrine/EMCON), which clears any retreat, flips the fleet Weapons Free,
and forces the fight (the resolver closes to weapons range first). Engine logic is CI-tested (`OrderAttackTests`);
the button is a thin call + a `[attack]` SessionLog line + a one-line result message. **v1 targets the NEAREST
hostile**; picking a SPECIFIC enemy fleet by map-click is the follow-up (needs blip-clickability + enemy ship→fleet
resolution). See `GameEngine/Combat/CLAUDE.md` → "Order a fleet to ATTACK".

### Fleet-as-one-icon — BUILT 2026-06-27 (the map matches "a fleet is one unit")

The engine treats a fleet as a single unit (moves as one, fights as one, locks orders as one), but the map drew
**one icon per ship** — `AddIconable` gives every `ShipInfoDB`+position entity its own `ShipIcon`, and a `FleetDB`
is just a tree of ships with **no icon/position of its own**, so the renderer only ever saw the individual ships
(the developer's "why don't fleet units become one icon?"). Now a multi-ship fleet draws as **one marker** — its
flagship's icon — until it's broken up. Wiring:
- **Engine (CI-tested):** `FleetTools.CollapsedFleetMemberShipIds(manager, factionId)` returns the ship ids to HIDE
  — every ship in a 2+ ship fleet **except its representative** (the flagship, or the first member if the flagship
  is unset). A lone ship / one-ship fleet is never hidden. Stateless → recomputed each frame, so collapse/expand
  tracks membership **live** (break a fleet up and the ships reappear next frame). Gauge: `FleetCollapseTests`.
- **Client:** `SystemMapRendering.Update()` recomputes `_collapsedFleetMembers` each frame (wrapped in try/catch →
  "hide nothing" on a throw, never blanks the map). `Draw()` skips those ids in `_entityIcons` (ship icon),
  `_orbitRings` (orbit ellipse) and `_moveIcons` (warp/burn trail) via `DrawIconsExceptCollapsed`, and skips their
  labels — so a fleet is ONE marker, not a scattered cluster. Only own-faction ships collapse (foreign fleets are
  fog blips); the set only ever holds ship ids, so bodies/stars/contacts are never affected.
- **v1 limits (flagged, easy follow-ups):** the single marker shows the **flagship's** name/icon, not a "Fleet (N)"
  label (the `FleetTools.FleetShipCountFor` helper is already there for it); a hidden member's interactable still
  exists, so a click exactly where its (undrawn) label was can still select that ship — clicking the fleet marker
  selects the flagship, and individual ships are managed in the Fleet window; **expand-on-select / expand-on-zoom**
  (Aurora's tactical view) is not built — v1 is always-collapsed-until-broken-up (the developer's literal ask).

### Society tab — the player-facing M-ECON readout (ColonyManagementWindow, 2026-07-02)

The M-ECON / political numbers were reachable ONLY through DevTools "Dump Society" → a log line in SM mode — accurate, but useless to a *player* making a decision. The **Society tab** on `ColonyManagementWindow` (`EntityDisplay.DisplaySociety`, between Summary and Production) is the real player-facing instrument: colour-banded sections for **Morale** (+ the factor breakdown that explains WHY — the lever the player acts on), **Legitimacy** (+ a live rebellion-window countdown), **People** (workforce/talent free-of-total), **Sustenance** (power/food shortage), **Economy** (tax rate → monthly income), and **Government** (the owning faction's regime name+description, since it modulates all the above). It is a **thin, defensive DRAW** — every value is a public getter on the same blobs the CI-tested `SocietyReadout` formats (`ColonyMoraleDB`/`LegitimacyDB`/`RebellionDB`/`ColonySustenanceDB`/`ColonyManpowerDB`/`ColonyEconomyDB`/`GovernmentDB`), no new engine math; each section is `TryGetDataBlob`-guarded so a colony missing a blob just omits that section. Colour bands: `Band0to100` (morale/legitimacy green→red), `ShortageColor` (0=green→1=red). **Printf trap avoided:** values render through `ImGui.TextUnformatted` (a P0 percentage's literal `%` would be parsed as a format specifier by `ImGui.Text`), and tooltips/`TextWrapped` are kept `%`-free. Reachable in NORMAL play (toolbar → Colony Management), not SM-gated — the counterpart to the SM-only DevTools "Dump Society" log gauge. **CI compiles it; live render/feel is the developer's local build.** The diplomacy ledger now has a player-facing home too — the **`DiplomacyWindow`** (toolbar button, 2026-07-02): a read-only table of every met faction's stance (colour-banded) / relation score / treaties, reading the player's `DiplomacyDB` directly (same data `SocietyReadout.Diplomacy` formats). So both colony society AND external relations are visible in normal play, not just the SM-only log dump.

### ImGui Begin/End balance + the hang breadcrumb (2026-07-02 — freeze diagnosis)

A live freeze (`[HANG] main loop STALLED ~5s`) hit right as the **combat interrupt auto-opened `BattleReportWindow`** on a heavy frame (36 ships / 286 orbits / 20 contacts). Root defect found: **`BattleReportWindow.Display()` called `ImGui.End()` only INSIDE its `if(ImGui.Begin(...))` block** (plus an early `return` that did the same). Dear ImGui requires `End()` for **every** `Begin()` — *even when `Begin()` returns false* (window collapsed/clipped) — unlike `BeginTable`/`BeginChild`/`BeginMenu` (End only if it returned true). So a collapsed Battle Report window skipped `End()` → the `[imgui-error] … 'Battle Report': Missing End()` spam seen in the logs → an unbalanced ImGui window stack on the combat frame. **Fixed:** `End()` is now unconditional (moved after the `if`), the empty case is an `else` (no early return), `EndTable` stays inside its own `if`. **Rule: a top-level `if (ImGui.Begin(...)) { … ImGui.End(); }` is a bug — put `End()` after the block, always.** Windows that use the `Window.Begin`/`Window.End` wrapper (e.g. `FleetWindow`) are safe (the wrapper calls `End` unconditionally); the raw-`if (ImGui.Begin(` pattern is the risky one. **Follow-up audit flagged** (same pattern, not yet verified): `ColonyHexMapWindow`, `ManeuverNodePanel`, `Debug/DataViewerWindow`, `Debug/DebugGUIWindow`, `EntityUIWindowSelector`, `ColonyPanel`, and the `ModFileEditing/*` editors — check each puts `End()` after the `if`.

**Companion visibility fix — the hang watchdog now NAMES the wedged stage.** A freeze leaves no stack trace, so the old `[HANG]` line could only say "the lines above are where it wedged." Now `PulsarMainWindow.SafeRender` stamps `SessionLog.CurrentStage = context` before running each stage (map draw, name icons, every window `Display()`, maneuver panel, heartbeat), and `state.Update` is stamped too — so the next `[HANG]` reads `wedged in stage: '<context>'`, pinpointing the exact window/stage. This is the Visibility-Gate move: a hang was the one fault with no gauge; now it has one.

**RESOLVED (2026-07-03, 3rd attempt): the fleet-list freeze was `BeginPopupContextItem`, not drag-drop.** The sub-breadcrumb pinned it exactly: `[HANG] wedged in 'FleetWindow/List/ContextMenu'` — i.e. `DisplayContextMenu`, which ran `ImGui.BeginPopupContextItem(null, ImGuiPopupFlags.MouseButtonRight)` **every frame per fleet**. That convenience wrapper runs an ImGui-INTERNAL mouse-button query (`IsMouseReleased(mouse_button)` / needs a non-zero `LastItemData.ID`), and here it fired the native `button >= 0 && button < 5` assert whose MODAL dialog blocks the main thread (reading as a [HANG]). The tell: it triggers when selecting a fleet **through the menu/entity-list** (which renders the list) but NOT clicking a fleet on the **map** (window closed → list not rendered). Two earlier attempts MISSED (documented below) — the self-parent cycle-guard and the drag-drop `AcceptBeforeDelivery` — because I fixed the wrong ImGui call each time; the breadcrumb naming the exact sub-op is what finally localised it. **Fix (applied to ALL THREE context menus in the window — fleet-list, ship-list ×2 callers, standing-orders):** replace `BeginPopupContextItem(...)` with the explicit manual pattern — detect the right-click at the item with `ImGui.IsItemClicked(ImGuiMouseButton.Right)` (an explicit VALID button) → `ImGui.OpenPopup(uniqueId)`, and render with `ImGui.BeginPopup(uniqueId)` (which takes NO mouse button). Detected right AFTER the item's Selectable/TreeNode and BEFORE its tooltip, so it keys off the correct item (a tooltip can change `LastItemData`, which is also why the `null`-str_id `BeginPopupContextItem` was fragile). **Rule: prefer the explicit `IsItemClicked(Right)+OpenPopup(id)+BeginPopup(id)` pattern over `BeginPopupContextItem` in this codebase — the convenience wrapper's internal mouse-button query trips a native assert in the bundled ImGui build, and a native assert's modal reads as a [HANG] via the watchdog.** (drag-drop history / earlier misses below.)

**(superseded — the drag-drop mis-fix) Earlier this was thought to be ImGui drag-drop.** Two parallel agents + the sub-breadcrumbs settled it. **Agent (data):** the fleet tree from the auto-spawn scenario + the colony start path is provably single-parent / acyclic / depth-2, and NEITHER merged branch (`space-economy-morale`, `4x-game-testing-strategy`) touched `SetParent`/`AddChild`/`ChangeParent`/`AssignShip`/`FleetFactory`/`TreeHierarchyDB` — so it's not malformed-tree infinite recursion. **Agent (ImGui):** the crash is a native ImGui assert `button >= 0 && button < 5` (imgui.cpp) inside **`AcceptDragDropPayload`'s internal `IsMouseReleased(g.DragDropMouseButton)`** — the drop-target delivery in the fleet list (`FleetWindow.DisplayDropTarget`/`DisplayEmptyDropTarget`). Every EXPLICIT mouse-button arg the client passes is valid (`Left`/`Right`), so the out-of-range index is ImGui-internal; and a native assert pops a MODAL dialog that blocks the main thread, so the `[HANG]` watchdog mislabels it a freeze. The drag-drop code **predates both branches** — the auto-spawn scenario just gave the player fleets in the list to drag, finally reaching it. **Fix:** pass `ImGuiDragDropFlags.AcceptBeforeDelivery` to both `AcceptDragDropPayload("FLEET", …)` calls — that SKIPS the internal `IsMouseReleased(bad button)` while the client's own valid `IsMouseReleased(Left)` check still gates the drop on release (behaviour unchanged). **Rule: when a native ImGui assert names a mouse-button range and none of your explicit button args are bad, it's an ImGui-INTERNAL query (drag-drop delivery / active-id) — a native assert reads as a `[HANG]` because its modal dialog blocks the main thread; the finer breadcrumb is what localises it.** Legacy note (the miss): the earlier `GetVisibleParent` self-parent cycle-guard is a real latent-bug fix but was NOT this bug (the position chain is clean — `AbsolutePosition` throws on a self-parent and the ship renders fine). Historical detail below.

**(historical) The investigation path — `[HANG] wedged in stage: 'FleetWindow'`.** Clicking the 1st fleet reliably freezes the client; the hang watchdog names `FleetWindow` (no exception — it's a freeze, not a throw, so there's no stack trace — the breadcrumb is the only gauge). **First fix attempt MISSED:** `FleetWindow.GetVisibleParent` walks the flagship's `PositionDB.Parent` chain in a `while (parent != null)` loop with no cycle guard, and the engine self-parents root bodies (`Parent == OwningEntity`, which `MoveState`/`PositionDB.AbsolutePosition` special-case) — so a hardening `HashSet<int> visited` guard was added there. **But it did NOT stop the freeze** (it recurred on a fresh build): the flagship's position chain is provably clean (its `AbsolutePosition` *throws* on a self-parent, and the ship renders fine on the map every frame — so no position-chain cycle exists to hang that walk). The `visited` guard stays (a real latent-bug fix — any ancestor-walk should carry one), but it is **not** the cause of this freeze. **Where it actually is:** narrowed to `DisplayShips` / `DisplayOrders` / `DisplayTabs` (all only run once a fleet is selected; `DisplayFleetList` runs even unselected and doesn't hang). The `DisplayShipAssignmentOption` recursion is menu-gated (not the plain-select path). Not yet pinpointed. **Visibility-Gate move (shipped):** finer `SessionLog.CurrentStage` breadcrumbs now stamp each section (`FleetWindow/List|Ships|Orders|Tabs`) and each tab (`…/Tabs/Summary|Combat|IssueOrders|StandingOrders`), so the NEXT freeze names the exact sub-section instead of the whole window — then the fix can be precise. **Rule reaffirmed: after a fix misses, don't pile on a second guess — build the finer gauge first (this is that gauge).**

### A throwing TAB cascaded the whole UI — hard-index + containment fix (2026-07-02)

Live crash: opening a colony while in **SM mode** threw `KeyNotFoundException: 'electronics'` from `IndustryDisplay.ProductionLineDisplay` (`_factionInfoDB.IndustryDesigns[job.ItemGuid]`, a **hard index** — gotcha #10/#11: `_factionInfoDB` is the VIEWED faction, which in SM mode is the GameMaster with an **empty** design store, and a foreign colony's jobs reference designs the viewed faction lacks). Two problems, both fixed:
1. **Root:** the hard index → now `IndustryDesigns.TryGetValue` + `IndustryTypeRates.TryGetValue`, skipping just the rate readout when unresolved. (Sibling `IndustryDesigns[SelectedConstrucableID]` click-handlers at IndustryDisplay `:206/:357/:430` are lower-risk — the id comes from the faction's own list — but harden them if they ever fire.)
2. **The cascade (the scary part):** the throw happened MID-render, between `ImGui.BeginTabItem` and `EndTabItem`, so it propagated out of `ColonyManagementWindow.Display()` and **skipped `Window.End()`** — leaving "Manage Colonies" open, so **every other window that frame** failed with `Begin(...) called while already inside window "Manage Colonies"`. `SafeRender` caught the original throw but can't rebalance the ImGui stack mid-frame, and `ConfigErrorRecovery` only cleans up at frame-END — so with the throw recurring every frame, the whole UI stayed broken. **Fix:** `ColonyManagementWindow.SafeTab(label, body)` wraps each tab body in try/catch/finally so a tab throw is contained (logged once) and `EndTabItem`/`EndTabBar`/`EndChild`/`Window.End()` always run — one blank tab instead of a dead UI. **Rule: any window that renders sub-panels which can throw (tabs, foreign/NPC data) must guarantee its `End()` runs — wrap the risky body so a throw can't skip the ImGui balance calls.**

### DevTools — society / economy / politics levers (2026-06-29 → 2026-07-02)

The M-ECON + political systems have **no dedicated player UI yet**, so their observability + test levers live in `DevToolsWindow` (all thin callers over CI-tested engine logic — the runtime-blind discipline):
- **Dump Society (log)** → `SocietyReadout.Colony` per colony + `SocietyReadout.Government` + `SocietyReadout.Diplomacy` (2026-07-02) for the player faction. Prints morale (+factors) / legitimacy (+ rebellion window countdown) / workforce+talent / **power-food shortage** / tax→income, the government name, and the diplomacy ledger (stance/score/treaties). The engine formats it (CI-tested); this is an iterate-and-log wrapper. Reads via the flushed `game_logs/` pages.
- **Government (test regimes)** (2026-07-02) → three preset buttons (Federal Republic/Mid reset · Totalitarian War-State · Liberal Democracy) set the player faction's `GovernmentDB` dials via public setters, so a play-test can flip a non-Mid regime and watch the #30 wires bite (tax ceiling, crew policy, research speed, morale weight, war pride). Guarded (null faction / missing blob).
- **Age the galaxy (staged states)** (2026-07-02) → Early/Mid/Late buttons call the CI-tested engine `GameStageFactory.AgeTo` to layer the running game up so the late-triggering cluster is visible immediately: Early = a frontier colony, Mid = met rivals + a treaty, Late = an active war + a rebelling colony. Cumulative + convergent (click through the stages); logs the engine's summary. Then Dump Society to read it. (task #39)
- **Society levers (sustenance / manpower)** (2026-07-02) → a colony picker (`SyncColonies`, refreshes on count-change like `SyncFactions`) + two levers that switch the **neutral-when-absent** M-ECON wiring ON for one colony so the otherwise-invisible C2/C1 tracker rows are reachable on a short play-test: **Apply sustenance demand** (two InputFloats → `ColonySustenanceDB.SetDemand`, a NEW public engine setter since the demand fields are `internal set`) forces a power/food shortage → a morale factor; **Drain manpower pool** commits all available bulk via the already-public `ColonyManpowerDB.CommitBulk` so the next crewed ship build hits the #27 crew gate. Both guarded with `TryGetDataBlob`.
- **Diplomacy levers (stance / treaties / war)** (2026-07-02) → a faction picker + thin callers over the CI-tested engine acts so C6/D4 are drivable interactively (not just observable via Age→Late): **Warm/Cool** (`RelationshipState.AdjustScore` on BOTH ledgers so Dump Society reflects it), **Declare War / Make Peace** (`Diplomacy.DeclareWar`/`MakePeace` — war flips the legitimacy militarism term), **Sign Non-Aggression / Trade / Defensive Pact** (`Treaties.Propose` — score-gated, so warm first), and **set the OTHER faction's militarism High/Low** (`GovernmentDB.Militarism`) to drive D3 reactive drift. Needs a 2nd faction (Age→Mid/Late or Spawn Hostile Fleet). Guarded against acting on the player faction / a ledger-less faction.

These are the levers the TESTING-TRACKER C1/C2/C3/C6/D3 rows drive. **CI compiles them; runtime is the developer's local build.**

- **Raise Ground Unit** (2026-07-04) → the placement tool that makes the `PlanetViewWindow` tactical map (slice 5e) testable in a stock New Game — there's no build-a-ground-unit-from-industry path yet (the base-mod `GroundUnitDesign` JSON template is deferred). Uses the shared "Orbit around" body picker; a type/count/region and two buttons: **"Raise (your faction)"** and **"Raise (enemy)"** (thin callers over the CI-tested `GroundForces.RaiseUnit` with a throwaway `MakeDevGroundDesign` preset). The enemy button resolves a real rival faction id via `ResolveEnemyFactionId` (first non-player/non-GM faction — the auto-spawn scenario provides rivals — else a synthetic sentinel), so raising yours + an enemy in the SAME region drives the ground fight + capture. Guarded (body must carry a `PlanetRegionsDB`); region clamped to the layer's count.

### PlanetViewWindow — the planet SURFACE tactical map (BUILT 2026-07-03 slice 3 → tactical 2026-07-04 slice 5e)

The ground layer's UI. `PlanetRegionsDB` gives every major body a surface of regions (the 4-slice ring — see
`docs/ground/GROUND-SURFACE-MAP-DESIGN.md`); `PlanetViewWindow` is the player's window onto it. It's the developer's "flat
3-region view": the centre region flanked by its two ring neighbours, rotated with ◀/▶ or by clicking a region (the ring
has no seam — the topology that keeps the Pacific theatre whole). Regions paint as stacked terrain bands coloured by
feature (`RegionFeatureType` → colour table), sized by coverage; an unsurveyed region is fog until scanned. Reachable from
the planet's right-click context menu ("Planet view (regions)"), gated on the body carrying a `PlanetRegionsDB`.

**Slice 5e turned it from a readout into a NAVIGABLE tactical map** — the layer the developer was waiting on ("a map I can
navigate units on, plot where I'll make military bases, use topography to my advantage"). On top of the terrain it now draws:
- **Units** — every `GroundUnit` in the body's `GroundForcesDB`, grouped per region by faction+type into a token
  (`I ×3` + a health bar; `»` when marching), coloured by owner (cyan = yours = `_uiState.Faction.Id`, red = hostile,
  grey = unowned). Laid across the bottom strip of each region column, wrapping.
- **Click-to-move** — click a token to SELECT that group; with a group selected, click an ADJACENT region column *or* a
  "March to Region N" button and every orderable unit in the group is marched via `GroundForces.OrderMove(body, unit,
  toRegion)` (engine-validated: adjacency, in-transit, region bounds — the client just calls it). A unit token click
  wins over the column-background click (which navigates), resolved after the columns are drawn against gathered hit-rects.
  Only YOUR units (`FactionOwnerID == _uiState.Faction.Id`) are orderable.
- **Click-to-place a base** — a Build panel (shown when you have a colony on this world) with a combo of your placeable
  installation designs (faction `IndustryDesigns` filtered to `ComponentDesign` with `ComponentMountType.PlanetInstallation`)
  and a "Build here" button that issues `PlaceInstallationInRegionOrder.CreateCommand(colony, centreRegion, designId)`
  through `Game.OrderHandler.HandleOrder` — the real order path. This is the LOCKED PRINCIPLE's first cut: a building
  placed at a *region*, drawn on the map.
- **Terrain + hazards visible** — a terrain-class chip (Open/Cover/Rough via `GroundTerrain.Classify`), the region's
  `PlanetEnvironmentsDB` environments as coloured hazard chips (fire=red / corrosive=green / storm-jam=amber), ownership,
  and the ⚙ building count per `Region.InstallationIds`.

- **Formations** (2026-07-04, the fleet echo) — a "Formations" panel under the Build panel: **Form up** all your idle units
  in the centre region into a named `GroundFormation` (the ground echo of a fleet), then a selectable list of your
  formations (name · member count · rally region) with **March formation ◀/▶** (moves the whole block one hop via
  `GroundForces.OrderFormationMove`) and **Disband**. Selecting a formation navigates the map to its rally region. A
  **stance selector** (the ground echo of the Fleet-window doctrine selector) shows the formation's current stance +
  attack/damage-taken mults and a combo of the moddable `Game.StartingGameData.GroundStances` catalog (Offensive Push /
  Dig In / Balanced) + a **Set stance** button calling `GroundFormationDoctrine.TrySetStance` (game time from the body's
  `StarSysDateTime`, greyed out on cooldown — mirrors `FleetDoctrine.TrySetDoctrine`). All thin callers over the CI-tested
  `GroundForces`/`GroundFormationDoctrine` APIs + `GroundFormationTools` reads. **+ an ROE selector (2026-07-04):** a
  Hold Ground / Close to Engage / Stand Off (auto-kite) combo calling `GroundFormationDoctrine.SetEngagementStance` — the
  commander's maneuver intent (the ground echo of the Fleet-window engagement-posture selector), applied immediately (no
  cooldown). This is what makes the H3 range advantage automatic: set Stand Off on a long-range formation and it auto-kites.
  **+ an ORDER-QUEUE panel (O1b, 2026-07-04):** `DrawOrderQueue` lists the formation's queued plan (`GroundOrder.Describe()`
  each) with a Clear button, and buttons to queue non-spatial orders (Hold 6h, ROE Stand-off/Close) + a MoveToRegion
  waypoint to each visible ring neighbour. **Move waypoints are added by SHIFT-clicking a hex** in Hex view (RTS-style
  queueing — `HandleHexClick` checks `ImGui.GetIO().KeyShift` + the selected formation and calls
  `GroundForces.QueueFormationOrder(MoveHex)`); a plain click still moves-now. So you build "move → move → dig in" plans
  visually. All thin callers over the CI-tested `GroundForces.QueueFormationOrder`/`SetFormationOrder`/`ClearFormationOrders`.

Built to the CI-blind discipline: a thin draw over CI-tested engine blobs, all orders through CI-tested engine paths
(`GroundForces.OrderMove` / `OrderFormationMove` / `PlaceInstallationInRegionOrder`), the whole body wrapped so a throw logs
`[RenderError]` once and still runs `Window.End()`, nothing hard-indexed (colony/faction lookups are `TryGet`/guarded).
**Live render + feel is the developer's local build.**

**LOCKED PRINCIPLE for this window (2026-07-04): every buildable is a real building on the ground.** The developer's rule —
*"everything I build on a planet that's selectable in space is represented by an actual building on the planet itself."*
Slice 5e delivers the *placement* half (build at a region → drawn as ⚙), and **#5 (2026-07-04) closes the loop for the
start colony**: `GroundInstallations.LocateColonyInstallations` (hooked in `ColonyFactory.CreateFromBlueprint`) gives the
start colony's existing installations a home region (the capital, region 0), so they draw on the map + count for
fortification — the colony economy list and this map are now two views of the same physical things. (Follow-up: locate
DYNAMICALLY-built installations at their install site; v1 covers the start colony.) Full rationale + slice mapping:
`docs/ground/GROUND-SURFACE-MAP-DESIGN.md` → "LOCKED PRINCIPLE." **Next: the "40k" ground-combat depth pass + a per-unit combat
readout** (the ground echo of the Fleet Combat tab).

**Build a MINE on a located deposit hex (2026-07-07).** The Build panel gained a second action beside the region "Build here": the last hex you click on the globe is remembered (`_selGQ/_selGR`), and if it holds a mineral deposit (`GroundHex.DepositMineralId >= 0`, seeded by `Industry.HexMinerals`) the panel shows **"Build \<Mine\> on this \<mineral\> deposit (Q,R)"** → `PlaceInstallationOnHexOrder.CreateCommand(colony, gq, gr, mineDesignId)` through `Game.OrderHandler.HandleOrder` (the CI-tested engine path). The mine design is auto-picked from the placeable installs by `HasAttribute<MineResourcesAtbDB>()`; the mineral name comes from the slice-2 `BuildMineralNames()`. This is "build a mine ON that deposit" — the mine sits on the ore hex. **v1: the mine still draws the body-wide pool; per-hex depletion (the mine works ITS deposit) is the flagged engine follow-up.** Thin defensive draw (try/catch, no hard-index). CI compiles; live feel is the developer's build.

**The CONTINUOUS-GLOBE window is now the ONLY surface view (G6a, 2026-07-05).** The developer validated the globe view live and asked to **drop the old per-region "band" view**. `DrawTacticalMap` now always calls **`DrawGlobalHexWindow`** (G5) — a sliding window over the ONE cylinder grid (`PlanetRegionsDB.SurfaceGrid`): centred on a longitude column (`_centerCol`, ~2 region-bands wide — centre band full + neighbours bleeding in), **wrapping at the seam** so any place shows in every window whose longitude reaches it. Units draw at their **GLOBAL (Q,R)**; click a hex to select your group / march via `GroundForces.OrderMoveToGlobalHex` (wrapping A\*, no edge gates); ◀/▶ pans by band. Uses an **odd-r OFFSET** layout (`HexCenterOffset`) so the wide window reads rectangular. **The band view is GONE:** the `◑ Globe / ▦ Band` toggle, `DrawThreeRegionHexMap`, the H4 `DrawHexMap` drill-in, and their dead helpers (`HandleHexClick` / `MoveSelectedToHex` / `HexCenter`) are **removed**. A **detailed collapsible legend** now sits at the bottom (`DrawLegend` + `LegendRow`): unit ownership + type initials + selection ring, and every terrain colour grouped (Water/Lowland/Upland/Cold/Gas, Ocean flagged impassable) + the seam/label/pan cues. The region-based side panels (selection/build/formation/detail) still drive off `_centerRegion`, which the globe keeps in sync (◀/▶ + clicks recentre the band). **Engine disks are still generated** (dormant scaffold; G6b retires them). Design: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (G5/G6a). **Runtime render/feel is the developer's local build (CI compiles, can't run).** The superseded 3-region band view is described below for history.

> **The HOMEWORLD (Earth) rendered BLANK — survey-gate regression FIXED (2026-07-17).** Live symptom: the surface window opened on Earth but drew no hexes, only "This world isn't surveyed yet" — while the control strip read "surveyed 4/4" (the two survey concepts disagreeing was the tell). Root cause: `DrawTacticalMap` gates the globe view on `body.TryGetDataBlob<GeoSurveyableDB>(out geoDB) && geoDB.IsSurveyComplete(myFaction)` (`PlanetViewWindow.cs:190`), but **Earth carries NO `GeoSurveyableDB` at all** — its blueprint (`earth.json`) is the ONLY Sol body that omits `GeoSurveyPointsRequired`, because the homeworld was never meant to need surveying (`SystemBodyFactory.cs:217` only attaches the gauge when that node is present). With no gauge, `IsSurveyComplete` can never read true → the `&&` fogged Earth forever. This was a **regression** from moving the surface view off the retired band view (which fog-gated on the always-set faction-agnostic `Region.Surveyed`) onto the globe view (which reads the per-faction `GeoSurveyStatus`). **Fix (one line, client-only):** `bool playerSurveyed = !body.TryGetDataBlob<GeoSurveyableDB>(out var geoDB) || geoDB.IsSurveyComplete(myFaction);` — a body with NO survey gauge is treated as "no gate applies / already known" (renders immediately), while a body that HAS the gauge still requires the viewing faction to complete it (the anti-leak rule holds — every rival body carries the gauge, so it stays fogged until you scan/settle it). The `||` short-circuits, so `geoDB` is only read when `TryGet` succeeded — cannot NRE. **Runtime-unverified (CI compiles the client, can't run it) — the developer's local build confirms Earth's hexes now draw.**

**The CITY zoom — the mini-hex grid under one operational hex (C-track, 2026-07-05).** The infrastructure zoom the developer chose: **double-click an operational hex** on the globe → `DrawCityZoom` swaps in that hex's fine `CityGrid` (the mini-hex tiles, `CityGridFactory.EnsureCityForGlobalHex`), drawn as terrain-coloured mini hexes (axial layout via `AxialHexCenter` — the disk is centred, unlike the globe's odd-r window). A building on a tile draws as a ▧ marker (the roll-up: tile-buildings == `GroundHex.InstallationIds`); **click a tile** to inspect it (design name via `BuildingIndex`); a **"Develop hex"** button calls `GroundBuildings.LocateFootprintsOnGlobalHexes` + `CityBuilder.DevelopGlobalHex` to lay the hex's footprint buildings onto tiles; **"⤢ Back to globe"** clears the zoom (`_zoomQ/_zoomR = -1`). **Per-tile placement (C2b) + multi-tile footprints (C2c) + BUILD-here (C3b) are built:** "Bring buildings here" locates the colony's footprint buildings + a picker lays an already-built one on a clicked empty tile (multi-tile buildings occupy their `GroundFootprintAtb.TileFootprint` contiguous tiles); a **"build a new installation" combo** queues a REAL production job on a clicked empty tile via `GroundBuild.QueueBuildOnTile` (materials + build-time), and reserved tiles draw an **orange "under construction" ring** (`GroundBuild.ReservedTilesOn`) until the `GroundBuildQueueProcessor` lands the finished building there. Click precedence: build-new › place-existing › inspect. Thin defensive draw over CI-tested engine methods. **Scaling is LOCKED** (operational-hex count is area-scaled ~4× Earth; mini-hex grid is a fixed count — no footprint-km² number). Design: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`.

**(superseded by G6a) The 3-REGION HEX MAP was the DEFAULT + ONLY surface view (V3 re-apply, 2026-07-04).** The developer's call: the hex
grid — not a coverage/percentage readout — IS the surface view (the coarse terrain-band strip `DrawRegionColumn` and the
`⬡ Hex view` toggle are retired/dead). `DrawTacticalMap` now always calls **`DrawThreeRegionHexMap`**: **ONE seamless hex field** (the developer's exact call —
*"1 hex map that shows 3 regions at once, the centre shown in full, the 2 surrounding just a fraction where they overlap"*).
The **centre region is drawn in FULL** (sized to fill the height and ≤ ~62% of the width), its two ring neighbours **offset
by a full patch width `qStep = 2R+1` (flush, no gap)** and **culled to the visible canvas**, so each side region only shows
the border fraction that bleeds into the margins. V2's planet-wide coherent terrain (`GroundHex.Terrain`) **flows across the
seams** (continents span borders — it's one continuous field), and a **subtle seam line at each edge of the centre region +
margin labels** (`R{n} (centre)`, `◂ R{n}`, `R{n} ▸`) mark where the centre ends without breaking the seamless look. Solves
the developer's complaints — "can't tell regions apart" (seam lines + labels) and "terrain doesn't flow / 3 separate maps"
(one continuous culled field, centre full + neighbours bleeding in). Units draw on their `(HexQ,HexR)`
per region; **click a hex in the CENTRE region** = full hex ops (select / march via `GroundForces.OrderMoveToHex` /
Shift-queue a waypoint — `HandleHexClick`); **click a SIDE region** = coarse-march a selection there (if adjacent) or
recentre. Reuses `HexCenter`/`_featureColors`/`OwnerColor`/`TypeInitial`. The zoomed city/fortification grid (C-track) is
the separate deeper view. **Runtime render/feel is the developer's local build (CI compiles, can't run).** The old
single-region drill-in (below) is superseded.

**(superseded) HEX drill-in view (H4, 2026-07-04).** A **⬡ Hex view / ▦ Region view** toggle (shown when the centre region carries a
hex patch) swaps the 3-region strip for the centre region's **fine HEX grid** (Planet → Region → Hex). Hexes draw as
terrain-coloured hexagons (`AddNgonFilled(..., 6)`, reusing `_featureColors`); each `GroundUnit` sits on its `(HexQ,HexR)`
as an owner-coloured marker + type/count (`I3`, `»` when hex-marching); the selected group's hexes get a yellow ring.
**Click a hex** → select your units there (toggle), or with a group selected, march it to that hex via
`GroundForces.OrderMoveToHex` (A* per unit, ocean impassable) — the London→Paris move on the fine grid. Click resolves to
the nearest hex centre (robust vs. polygon hit-testing). The caption surfaces the **H3 range readout** —
`GroundRangeTools.RealReachKm(unit.Range, region)` — so the selected group shows "strike range 3 hex ≈ N km" (the
"1 hex ≠ same distance body-to-body" insight, visible). Same thin-defensive discipline: reads CI-tested blobs, orders
through CI-tested engine paths, inside the window's `[RenderError]` try/catch, no hard-indexing. **Runtime render/feel is
the developer's local build (CI compiles the client, can't run it).** Design: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (H4).

**RANGE OVERLAY on the globe (Operation Earthfall C4.1, 2026-07-19) — the ground echo of the space map's range rings.**
`DrawGlobalHexWindow` gained a highlight pass (right after the terrain loop, before the seam lines + unit tokens): when
the player has a unit group selected on the globe, it **tints the hexes that group can reach** — faint **red** = weapon
reach (`GroundUnit.Range` in hexes), faint **green** = radar reach (the unit's best `GroundSensorAtb.Range_km` ÷ the
region's `GroundRangeTools.HexPitchKm`, the `GroundSensors.cs:42` km→hex conversion, read through
`GroundUnitEntity.TryGetBacking` → `ComponentInstancesDB` exactly like `GroundSensors.RevealFromUnits`). The disk shape is
the **odd-r OFFSET** hex distance the globe is DRAWN in — new private helpers `GlobalHexDistance`/`OddRToAxial` convert the
cylinder (col,row) to axial before `Colonies.HexCoordinate.DistanceTo` (so it's a proper hex disk, not a sheared rhombus)
and pick the short way across the seam via the existing `WrapDelta`. **FOG-HONEST:** drawn ONLY for the player's OWN
selected units (`_selFaction == myFaction` — you never see an enemy's ranges, the same rule the space rings use), and only
the visible window's hexes; a unit with no radar component (garrison / DevTools-raised — no backing store) shows no green.
**Toggle MIRRORS the space map's global switch** (`GlobalUIState.ShowAllRangeRings`, default on; DevTools › Detection /
Fog of War). Thin/defensive (the radar read is its own try/catch; no state mutation, no engine value changed). Engine adds:
NONE (the optional `GroundSensors.RadarReachHexes` helper R1 flagged is a GROUND follow-up — the client computes km÷pitch
inline until it merges). CLIENT-TEST-CHECKLIST: "OPERATION EARTHFALL — C4.1." Runtime feel is the developer's local build.

**K-track real-distance client draws (compile-only, 2026-07-22) — units-on-mini-hexes + fog-honest enemy reach + formation plan lines.** Three additive draws over the K-track engine (`docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md`; weapon reach already reads the REAL `GroundUnit.Range_m` via `WeaponReachHexesFor`→`GroundRangeTools.HexesForMetres`):
- **C1** `DrawCityZoom` now draws the UNITS standing in the zoomed operational hex (`GlobalQ/GlobalR == _zoomQ/_zoomR`) on the mini-hex tiles — at their `MiniQ/MiniR` tile (the same axial space the tiles use) plus the K2 sub-tile real offset (`MiniOffX_km`/`MiniOffY_km` → px via the mini pitch), coloured by `OwnerColor`, fanned out when stacked. Fog-honest (own always; a SCOUTED enemy too; an un-detected enemy hidden). Own try/catch.
- **C2** the C4.1 range-overlay pass broadened: it now ALSO tints every SCOUTED enemy unit's weapon (orange) / radar (teal) reach — distinct from own (red/green) — drawn without needing a selection, bounded to the visible window's hexes; and the enemy TOKENS (section 3) are gated to the detected set (an un-scouted enemy garrison never draws). New engine accessor `GroundThreat.DetectedEnemyUnits(body, viewer)` (owned/scouted/**in-contact**) is the fog seam.
- **C3** `DrawGlobalHexWindow` draws the SELECTED formation's queued PLAN as path lines: the leader's `GlobalQ/R` → its in-flight `GlobalPath` → each queued `MoveToHex` waypoint (global cylinder coords), via the new instance helper `GlobalHexToScreen` (odd-r offset + seam-wrap) + `drawList.AddLine`. Per-unit fire-control target lines deferred (no engine state). All thin/defensive; CI compiles the client but can't run it — the look is the developer's local build.

**Entity Assembler readout rows (C4.1, same slice) — see the `ShipDesignWindow` row.** `DisplayGroundStats` gained three
rows off `GroundUnitAssembly.Compute`: **Training** (`TrainingMultiplier`, the cadre veterancy — previously computed but
invisible), and ALWAYS-ON **Power (draw / supply)** (`EnergyDemand_W` / `ReactorSupply_W`, red **UNDER** when the guns
out-draw the reactors) + **Ammo Capacity** (`AmmoCapacity_kg`) — the last two used to appear only as red "Problems" text on
violation; now the margin is a standing gauge. No new numbers (display of already-computed values); the Problems verdict is
unchanged. All four result fields are public on `GroundUnitAssemblyResult` — no engine getter request needed for G4c.

**FORMATION RENAME + INFRASTRUCTURE COMBAT (Operation Earthfall PW.2, 2026-07-21) — the deferred cross-lane buttons.**
The formation panel (`DrawFormationPanel`) now has: a **Rename** control on the selected formation (inline
`ImGui.InputText` + button → `GroundForces.RenameFormation`; the buffer reseeds from the selection via
`_formRenameForId`), and **Infrastructure combat** buttons (`DrawFormationInfraOrders`) — "Raze / Capture
infrastructure" that QUEUE `GroundOrder.DestroyInfra` / `CaptureInfra(rally, 0, 0)` on the formation's leader region
(footprints sit on the region-centre hex 0,0; the G3 order's range gate needs a unit standing IN that region). The
**city-zoom** (`DrawCityInfraOrders`, appended to `DrawCityZoom`) got the R4 city-tile-inspect hook: it razes/seizes
the operational hex's region BAND (`PlanetGridFactory.RegionOfColumn`) carried by a player battalion standing in that
region (a fog/coordinate note: the G3 order works at the region level — targets the region-centre hex 0,0 — so the
city zoom razes the whole band's footprints via a battalion present there, not a single global tile; if none of your
battalions is in the region it tells you to move one in). All gated on the region actually holding footprint buildings
(read off `Region.Hexes` → the (0,0) hex `InstallationIds`), all thin defensive draws through the CI-tested
`GroundForces` / `GroundOrder` APIs. Engine surface pinned by `EfPwInfraButtonContractTests`. Runtime is the
developer's local build (CLIENT-TEST-CHECKLIST "OPERATION EARTHFALL — PW.2").

### GroundCombatWindow — MISSING ENTIRELY

No window exists for ground *combat* yet (the `PlanetViewWindow` above is the surface MAP, not the battle). When
`GroundCombatDB` (to be created, slice 5) is present on a colony/region, a `GroundCombatWindow` — or a combat tab on
`PlanetViewWindow` — should show unit positions and orders.

#### Target Lines — Visual Design Spec

Ground combat units should have **target lines**: persistent lines drawn from attacker to target while a fire-control relationship is active. The line disappears when the relationship ends (target destroyed, order cancelled, out of range) — it does not fade on a timer, because game time is compressed and real-time fading is meaningless.

**Rendering:**
- Draw using `OpenGLRenderer` line primitives — same mechanism as `WarpMovingIcon` transit lines.
- Line exists as long as the unit's current target order is active. Driven by game state, not wall-clock time.
- Color table (starting point — can expand later):

| Color | Meaning |
|-------|---------|
| Red | Actively firing this tick |
| Amber/yellow | Targeted but not firing (out of range, suppressed, no ammo) |
| Grey | Fire control lock held but combat paused / ceasefire |

**Toggle:**
- `bool ShowTargetLines` on `GlobalUIState` (or the `GroundCombatWindow` instance).
- Toolbar button or checkbox in the combat window header — same pattern as orbit ellipse toggle.
- Default on. Players with large battles can turn it off to reduce clutter.

**Selection filtering:**
- When a formation/unit is selected, only draw target lines for that selection.
- Global toggle overrides to show all or none.
- This mirrors how the system map shows orbit ellipses only for the selected body when zoomed in.

**Coordinate space note:**
- Ground units live on the `ColonyHexMapDB` tile grid, not in 3D system-map space.
- Target lines for ground combat render inside the `GroundCombatWindow` 2D hex view, not on the system map.
- The OpenGL line-drawing call is the same; the coordinate transform is hex-tile-to-screen, not world-to-screen.

---

## Gotchas

1. **ImGui is immediate-mode.** There is no retained widget state between frames. All data to display must be read from game state on every `Display()` call. Avoid expensive computations inside `Display()` — cache them in fields and update only when the relevant game entity changes.

2. **`PlanetaryWindow.old.cs` is a dead legacy file — kept out of the build by being wholly commented out, NOT by a `.csproj` exclusion.** It lives at `Interface/Windows/PlanetaryWindow.old.cs`, and its entire contents sit inside a single `/* … */` block comment (verified 2026-07-13), so the compiler skips it. There is **no `<Compile Remove …>` line** in `Pulsar4X.Client.csproj` for it. Do not reference it — it may hold stale API usage.

3. **There is no `GalaxyMap.cs` in the client** (verified 2026-07-13 — no such file exists anywhere in `Pulsar4X.Client/`, and no `<Compile Remove …>` for it in the `.csproj`). The live galaxy-level window is `GalaxyWindow.cs`; don't confuse an imagined `GalaxyMap.cs` with it.

4. **Window instances are keyed by string name.** `NonUniquePulsarGuiWindow` uses `"WindowType|entityId"` as the key in `LoadedNonUniqueWindows`. If you rename a window class, existing open window references in `GlobalUIState` become orphaned (harmless but leaks memory). Always use the static `GetInstance()` factory.

5. **The `Helpers.RenderImgUITextTable()` helper** renders a 2-column or N-column table from `List<string[]>` row data. Use it for consistent formatting across the info panels — it handles column alignment.

6. **Console output is BUFFERED when `launch.bat` redirects it to a file — it only flushes on game exit.** `launch.bat` runs `dotnet run > console_output.txt 2>&1`. With stdout redirected to a file (not a console), .NET buffers `Console.WriteLine` and does not flush until the process exits. So a mid-session `Console.WriteLine` (e.g. a DevTools action) is **absent from the file while the game is still open** — which made a spawn look like it "produced no log" (2026-06-24; the captured file was 100% build warnings, zero runtime lines). Two fixes: (a) **close the game fully before reading `console_output.txt`**, and (b) for diagnostics that must show up live, flush after writing — see `DevToolsWindow.DevLog()` (`Console.Out.Flush()`). Build-step warnings are *not* affected (the build flushes its own output), which is why a buffered capture shows warnings but nothing from the run.

7. **A spawned ship orbits at 2× the planet's RADIUS — it's hidden under the planet icon at the system view.** `ShipFactory.CreateShip(design, faction, parent, name)` places the ship in a circular orbit at `parent.RadiusInM * 2` (~12,000 km for Earth). At the zoomed-out system view (Earth's orbit is ~150 million km), that is sub-pixel **on top of the planet icon**, so a freshly-spawned ship looks like it didn't appear. It did — **zoom into the parent body, or open the Fleet window, to see it.** The icon chain itself is fine (`EntityManager.AddEntity` → `MessageTypes.EntityAdded` → `SystemState.OnEntityAddedMessage` → `OnEntityAdded` → `SystemMapRendering.AddIconable`). DevTools "Spawn Ship" now reports the system ship count after a spawn as proof it landed. (2026-06-24: this was the real cause of "the spawner didn't work".) **Movement counterpart (2026-06-27): a spawned ship also wouldn't MOVE on a warp order** — `CreateShip` leaves the reactor at **0 stored energy**, and warp is paid from stored electricity, so a 0-charge ship sat still. The Spawn Ship path now calls **`ShipFactory.ChargeReactors(ship)`** right after `FillFuelTanks` (logged as `energy=+N KJ`), so a spawned ship is fuelled **and** charged = genuinely ready to fly. The precise "what the premade ships have that ours don't": the start fleet is hand-charged in `DefaultStartFactory`; a spawn wasn't. See `GameEngine/Movement/CLAUDE.md` → warp 0-energy gotcha.

8. **New Game builds a colony but NO starting fleet (found 2026-06-24).** `NewGameMenu.CreateGameCore` builds the start piecemeal: `FactionFactory.CreateBasicFaction` (blank faction, no fleet) + `ColonyFactory.CreateFromBlueprint`, which only creates ships from fleets **nested in the colony blueprint** (`colonyBlueprint.Fleets`). But the base-mod start data defines its fleets at the **faction level** (`GameData/basemod/ScenarioFiles/uef.json` top-level `"fleets"`: gunship/freighter/surveyor), which only the *scenario loader* `DefaultStartFactory.LoadFromJson` reads — **the wizard doesn't use it**. Net: a New Game gives you a colony and an **empty sky** (0 ships, 0 fleets) — empty Fleet window, nothing to control. Confirm with the new DevTools **"Dump State"** button (reads 0 fleets). Fix = build the intended fleet on the wizard path (nest the fleets under the colony blueprint, or have `CreateGameCore` create them). Note: a bare `ShipFactory.CreateShip` also adds the ship to **no fleet**, and the client **cannot** add it directly — `FleetDB.SetParent`/`AddChild` are engine-internal and `FlagShipID` is read-only (trying it from `DevToolsWindow` **broke the client build**, which CI can't catch). Client-side fleet changes go through the **order system** (`FleetOrder.AssignShip`/`CreateFleetOrder` → `OrderHandler.HandleOrder`, see `FleetWindow.cs`). The proper "controllable ships" fix is **engine-side** (build the fleet in the start path, where the fleet API is accessible).

9. **New Game wizard must guard against an empty `_modDataStore` (fixed 2026-06-22).** `NewGameMenu.DisplayModsPage()` "Next" handler calls `LoadEnabledMods()` then sets defaults with `_modDataStore.Species.First()` / `.Themes.First()` / `.Colonies.First()`. `LoadEnabledMods()` returns early (leaving the store empty) when **no mod is enabled** — so if the player presses Next with every Enable box unchecked, `.First()` throws `InvalidOperationException: Sequence contains no elements` and the **whole app crashes** (the wizard runs inside the render loop, not behind a try/catch). It now checks `.Any()` first and shows an inline error (`_modsPageError`) instead of advancing. `QuickstartGame()` already had the equivalent `.Any(...)` guard — keep the two paths in parity. Same class of bug as the unguarded `.First()` calls in `CreateGameCore` (`SystemBodies[BodyId]`, `Colonies[ColonyId]`); validate selections before dictionary/`.First()` access in any New Game step.

> **Quickstart loads only `DefaultEnabled` mods (2026-07-12).** `QuickstartGame()` used to load every mod flagged in the mutable `ModsState.IsModEnabled` — which the New-Game mod page can toggle and leaves set for the session. So if the player ever checked **`Pulsar4x-Testing`** (a test-only stub: `testData.json` = 3 themes + 1 armor, **no components/species/colonies**), a later Quickstart still loaded it → a faction with **no buildable components** and an NRE during colony build (the developer's "why can't I build original components / it crashes"). Quickstart now loops on `modMetadata.Mod.DefaultEnabled` (the base mod only — `Pulsar4x` has `"DefaultEnabled": true`, the testing mod defaults false), independent of any UI toggle. The full New Game wizard still honours explicit mod choices. **Rule: Quickstart = the sane-defaults path; never let it pick up a non-`DefaultEnabled` mod.**

10. **SM (Space Master) mode switches the VIEWED faction to the Game Master faction** — `GlobalUIState`'s SM toggle calls `SetFaction(Game.GameMasterFaction)` on enable and `SetFaction(PlayerFaction)` on disable (`GlobalUIState.cs:517,524`). The Game Master faction owns **no fleets** and has **no unlocked armor/tech**. Consequences confirmed live 2026-06-24, all the same root cause: (a) the **Fleet window shows nothing in SM mode** — it filters by `_uiState.Faction` (= Game Master); your fleets aren't gone, **exit SM mode to see them**; (b) **spawned/own ships are invisible in SM mode** for the same reason — exit SM and they appear; (c) windows that read `_uiState.Faction` and assume player data **crash** — `ShipDesignWindow.RefreshArmor()` hard-indexed `factionData.Armor["plastic-armor"]`, which the Game Master lacks → `KeyNotFoundException` → whole-client crash (fixed: default to the first available armor, never hard-index). **`_uiState.PlayerFaction` stays the real player throughout** (only `_uiState.Faction` changes) — that's why DevTools spawns still correctly belong to the player. **Rule: any window usable in SM mode must tolerate the viewed faction having empty data — never hard-index a faction dictionary, and use `_uiState.PlayerFaction` when you specifically mean the player.**

> **DevTools "Faction Switcher (SM)"** generalises this beyond the GameMaster/Player toggle: it lists every entry in `Game.Factions` and a "View as" button calls the same `_uiState.SetFaction(faction)` to switch the *viewed* faction to any of them (with a "Back to player" → `SetFaction(PlayerFaction)`). It's the tool for watching an auto-resolved battle from either side's perspective (engine combat spine step 9). It inherits the caveat above — switching to a bare faction (GameMaster, an NPC with no known systems) shows empty Fleet/System views; that's expected, switch back. The switch is wrapped in try/catch so a faction missing `FactionInfoDB` reports an error instead of crashing the client.

11. **Inspecting a FOREIGN/NPC-owned entity hard-indexed that owner's locked faction data → whole-client crash (fixed 2026-06-25).** Same root cause as #10 (a faction dictionary indexed for data the faction doesn't have), but the trigger is the entity's **owner** faction, not the viewed faction — so it bites even outside SM mode. Confirmed live: the developer used DevTools to spawn 6 hostile "Cargo Courier" ships around Ceres (a bare faction from `CombatSandbox.SpawnHostileFleet`, whose `FactionDataStore.CargoTypes` is **empty** — all cargo types sit in `LockedCargoTypes` until tech unlocks them, see Factions gotcha #4), zoomed in, and clicked a ship. Because ships render at ~2× body radius they sit **on top of** the Ceres icon (gotcha #7), so the click opened the *ship's* `EntityWindow`. Its cargo-bar block did `factionInfoDB.Data.CargoTypes[sid].Name` on the **owner** (Hostiles) faction → `KeyNotFoundException` → the SDL `Run` loop has **no try/catch**, so the process crashed. The trace went to **stderr**, which is *not* in `game_log.txt` (Program.cs redirects stdout only), so the log just stopped after the spawn line — looking exactly like a freeze. **Fix:** three sibling sites now look the cargo type up defensively (unlocked `CargoTypes` → `LockedCargoTypes` → fall back to the id, never a hard index): `EntityWindow.cs:~1120` (DisplayShipContent cargo bars), `Interface/Displays/CargoStorageDBDisplay.cs:23-24`, `Interface/Windows/CreateTransferWindow.cs:178`. **Rule extends #10: never hard-index a faction dictionary for ANY entity whose owner might be foreign/NPC** — a spawned hostile, an NPC trader, anything not the player. The `CargoGoods.GetMaterial(...).Name` reads in `DebugWindow`/`ManuverNode` are the same class but are debug/uncommon paths; harden them if they ever fire.

12. **The render loop now has a visibility gauge — `PulsarMainWindow.SafeRender(context, action)`.** Each per-frame piece (map draw, name icons, every window's `Display()`, the maneuver panel) runs through `SafeRender`, which catches any exception, logs the **full** stack trace ONCE per unique error to the captured log via `Console.WriteLine` (→ `game_log.txt`, because stdout is redirected there), and **skips just that piece for the frame** instead of crashing the whole app. ImGui error recovery (`ConfigErrorRecovery = true`, set in `SDL3Window.Run`) cleans up any window/stack left half-open by the throw. This is the sensor the Visibility Gate demanded: before it, an unhandled render exception was an invisible hard crash (trace to stderr, not the log). After it, a faulting window names itself in `game_log.txt` and the game stays usable. **If you see `[RenderError] <context> threw…` in the log, that context (a window class name or a draw stage) is where the bug is** — the dedupe means it's logged once, so don't expect it to repeat. Don't "fix" a window by relying on SafeRender to swallow its faults — it's a safety net + a gauge, not a license to leave a Display() that throws. **Input-side sibling (added 2026-06-26): `PulsarMainWindow.HandleEvent` now wraps event dispatch the same way.** The SDL event loop (`SDL3Window.PollEvents`) has no try/catch, so an exception in any click/key handler crashed the WHOLE process (gotchas #11 and #14 were both exactly this), and the managed trace died with it — reaching only `console_output.txt`, never the rotating pages. Now `HandleEvent` catches any handler throw, logs it ONCE as `[InputError] event <type> handler threw…` (shares SafeRender's dedupe set), and lets the event loop continue: a bad click does nothing instead of killing the game, and its trace lands in the `game_logs/` pages. **So render faults → `[RenderError]`, input faults → `[InputError]`, both isolated + logged.** This is the net that would have made the #14 click crash a one-line log entry instead of a hard kill. Same rule: it's a gauge + safety net, not a license to leave a handler that throws. **Third layer (2026-06-26): `[FATAL]` + `[HANG]`.** Some failures dodge both nets — an unhandled exception on a BACKGROUND thread (no main-thread try/catch covers it), and a FREEZE (the main loop stuck in a long/infinite op throws nothing; the log just stops, reading identically to a crash). So: `Program.cs` registers `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` → writes `[FATAL] …` + flush before the process dies (the trace lands in the pages, not only `console_output.txt`); and `SessionLog.StartHangWatchdog()` runs a daemon thread that, if `PostFrameUpdate`'s per-frame `SessionLog.FrameTick()` stamp goes stale >5 s, writes `[HANG] main loop STALLED …` from OUTSIDE the wedged main thread. Neither can catch a hard native (SDL/GL) access violation that kills every thread at once — but by **elimination** they narrow it: `[HANG]` fired → a freeze; `[FATAL]` fired → managed; neither, log just stops → native.

> **Map granularity (added 2026-06-25 with the warp "fleets jumped to the Sun" investigation).** The whole-map `SafeRender("GalacticMap.Draw", …)` wrapper turned out too coarse: if ONE icon throws mid-draw (e.g. a NaN coordinate from a mid-warp/detached position hitting `Convert.ToInt32` → `OverflowException` in a transit/move icon), it aborted the **rest** of the map for that frame — orbit/transit lines (drawn first) survive, ship icons + labels (drawn after) vanish. That is exactly the live "stuck blue lines between Earth and the Sun, ships gone" symptom — a *render artifact masking* a movement bug, not the movement bug itself. Fix: `SystemMapRendering.DrawIcons` and the label loop now wrap **each item** in `SystemMapRendering.SafeDraw`, which logs `[RenderError] map item '<TypeName>' …` once and skips just that item so the rest of the map renders. The coarse `GalacticMap.Draw` wrapper stays as a backstop. **Lesson: put the gauge at the granularity of the thing that fails** — per-item names the culprit entity; per-map only says "the map broke." (The underlying warp bug — `WarpMoveProcessor` reparents a ship's position to the system Root/Sun on launch, so an intra-system hop like Earth→Luna can read as a jump to the Sun — is a separate, PRE-EXISTING movement issue, not the combat code; tracked in `SESSION_STATE.md`.)

13. **Session recorder — the "flight recorder" for live play (`SessionLog`, built 2026-06-26).** `Pulsar4X.Client.SessionLog` (`SessionLog.cs`) writes a readable, greppable play-by-play of the player's actions + periodic state to the captured log, so a bug report **is** the log instead of "reproduce it and send a log." Every line is **flushed immediately** (`Console.Out.Flush()`), so a freeze or hard crash still leaves the full trail up to that instant. Categories: `[ACTION] [VIEW] [TIME] [CAMERA] [SELECT] [DRAG] [STATE] [DETECT] [EMCON] [ENGINE]` (plus engine-side `[Combat]`/`[FleetCombat]`/`[DevTools]`). Toggle the whole thing with `SessionLog.Enabled`.
    - **Where the log lives:** the managed log now **rolls into read-sized pages** under a `game_logs/` folder in the **repo root** (`game_log_000.txt`, `_001`, … — see `RotatingLogWriter.cs`), NOT `%AppData%`. Each page is capped (~1000 lines / ~120 KB) just under the "file too large" read wall, so a whole session can be read start-to-finish, one page at a time, with nothing lost — that wall was about to make the log unreadable for a remote review (the heartbeat alone writes ~5 lines/3 s). `Program.cs` redirects `Console.Out`/`Console.Error` into the rotating writer and walks up from the running exe to the folder holding `.git` or `launch.bat` (falls back to the exe dir, then `%AppData%`; and if the folder can't be created, falls back to a single `game_log.txt` so it ALWAYS logs). The folder starts fresh each launch (stale pages cleared), matching `console_output.txt`/the old single file. This is separate from `console_output.txt`, which `launch.bat` fills with **build + native/stderr** output. Runtime lines (`[ACTION]`/`[SELECT]`/`[Combat]` etc.) go to the `game_logs/` pages. **To review a session: read the pages in numeric order** (`game_log_000.txt` first); to grep, `Select-String -Path game_logs\*.txt -Pattern '[Combat]'`.
    - **The hooks (where state is captured):** time controls → `TimeControl.PausePlayPressed`/`OneStepPressed` (`[TIME]`); camera pan/zoom → `SystemMapRendering` `_camera.PanOccured`/`ZoomOccured`, **throttled ~400 ms** via `_lastCamLogTick` so a drag doesn't flood the log (`[CAMERA]`); entity click/select → `GlobalUIState.EntityClicked` (`[SELECT]`); **fleet/ship move/warp order → BOTH `FleetWindow.cs` move button (`[ACTION]` "move order: fleet #N -> 'Body'") and `WarpOrderWindow.cs` right-click "Warp to a new orbit" (`[ACTION]` "warp order: ship #N -> 'Body'")** — these are the two ways to issue a warp, the trigger for the teleport bug, logged right before the teleport check fires. (The teleport *detector* is trigger-agnostic — it scans all ships every heartbeat — so even an unhooked order path can't hide a teleport; these hooks just show the trigger in the log.) faction/view switch → `GlobalUIState.SetFaction`, which also auto-dumps ship positions (`[VIEW]` + `[STATE]`); periodic snapshot → `PulsarMainWindow.PostFrameUpdate` calls `SessionLog.Heartbeat(...)` **every ~3 s** (wall-clock `SDL.GetTicks()`, so cadence is steady regardless of game speed) reporting game clock / run-or-paused / step / selection / ship count (`[STATE]`).
    - **The teleport gauge — now automatic.** `SessionLog.CheckForTeleports(StarSystem)` runs **inside every heartbeat** (~3 s): it scans all ships and logs a `⚠ TELEPORT` line classified by reason — **AT-SUN** (distance from the Sun under 1 Gm: `TeleportSunDistThreshold_m`; nothing real orbits that close, Mercury is ~58 Gm out — the real collapse) or **ORPHANED** (anchor `Parent` null/invalid **while NOT warping**), with the ship's `moveType` (Orbit/Warp) as the smoking gun. **Warp-aware (2026-06-26):** a normal warp is reparented to the system root (null parent) on purpose and keeps its true absolute position, so a null-parent warping ship at a healthy distance is NO LONGER flagged — that was a false alarm on every warp (confirmed live: ships at 111 Gm correctly en route to Jupiter were being flagged). So the "teleport to Sun" bug now **announces itself within 3 s of happening, with no faction-view switch needed.** The older `SessionLog.DumpShipPositions(StarSystem, context)` (logs *every* ship's Sun-distance + parent) still exists for an explicit before/after snapshot and auto-fires on view switch. Diagnosis as of 2026-06-26: the **clean warp path is correct** (`WarpMoveProcessor.StartNonNewtTranslation` reparents to the system root but `MoveState.SetParent` preserves absolute position) — the teleport is an **interaction edge case** in a single time-step (warp + orbit + combat-destroying-ships) where a ship's `Parent` goes null/invalid while `RelativePosition` is still a small orbital offset, so `AbsolutePosition` (MoveState.cs:44 fallback) collapses to the origin/Sun. The detector exists to catch which path does it; **don't blind-fix the warp code** (no warp-position test exists; CI's smoke test only checks positions are finite, not correct).
    - **The heartbeat is wrapped in `SafeRender`** in `PostFrameUpdate` because `GlobalUIState.SelectedSystem` is a computed property (`StarSystemStates[SelectedStarSystemId].StarSystem`) that **throws** when no system is selected — a fault there logs once and the game keeps running rather than crashing on a diagnostic.
    - **`[DRAG]` is reserved, not wired — there is no drag-box/marquee multi-select in the game.** The system map hit-tests **one** interactable per click (`SystemMapRendering` `MouseButtonDown` → `item.Contains` → `OnPointerDown`); a mouse-drag on the map **pans the camera** (already logged as `[CAMERA]`). If true rubber-band multi-select is ever built, log it through `SessionLog.Drag(...)` — the category already exists.
    - **Rule:** any new player-facing action worth replaying should get a one-line `SessionLog.*` call at the point the action is committed (not where it's drawn) — cheap, flushed, and it makes the next "it froze / it did something weird" report self-diagnosing.

14. **Clicking a label/icon for a REMOVED entity hard-indexed the entity-state dictionary → whole-client crash (fixed 2026-06-26).** The click-path sibling of gotcha #11 (which was about *faction* dictionaries). `GlobalUIState.EntityClicked` and `EntitySelectedAsPrimary` did `StarSystemStates[starSys].EntityStatesWithNames[entityGuid]` — a hard index. When a ship is **destroyed in combat**, its entry is removed from `EntityStatesWithNames` (`SystemState.Update` → `_entitiesWithNames.Remove`), but its **clickable label can outlive it on screen** (the label-cleanup path lags the state removal — and a dead entity's `AbsolutePosition` collapses to the **origin/Sun**, so those stale labels pile up right on the star — this is the "ships teleported to the Sun" visual). Clicking one fed a now-missing key to the hard index → `KeyNotFoundException`, and the SDL `Run` loop has **no try/catch**, so the **whole process crashed**. Confirmed live 2026-06-26 (key `'676'`, a destroyed Earth-fleet ship; the crash only became visible because the developer's `launch.bat` now captures **stderr** — the trace is *not* in `game_log.txt`, which is stdout-only). **Fix:** both sites use `EntityStatesWithNames.TryGetValue(entityGuid, out var state)` and ignore the click (logging `[SELECT] ignored click on stale/removed entity #N`) if it's gone. **Rule extends #10/#11: never hard-index ANY runtime dictionary keyed by entity/faction id from a UI path — `TryGetValue` + bail. UI state (labels, icons) can lag engine state (entity removed) by a frame or more; the click handler must tolerate a stale id.** *Visual cleanup (done 2026-06-26):* `SystemMapRendering.PruneDeadEntities()` now runs every frame in `Update()` — it scans `_allLabels`, and for any whose `Entity.IsValid` is false (destroyed) calls `RemoveIconable(id)`, dropping the icon + label + interactable together. This is driven by the entity's `IsValid` flag (flipped **immediately** by `TagEntityForRemoval`), not the lagging `EntityRemoved` message that `OnSystemStateEntityRemoved` waits on — so the ghost vanishes the instant the ship dies instead of sliding to the Sun, even while the game is paused after a step. (Why both: the message path stays as the normal cleanup; the per-frame prune is the safety net for the gap.) The `SessionLog.CheckForTeleports` heartbeat (gotcha #13) still flags any dead-entity-at-origin that slips through. Logs `[STATE] pruned ghost icon/label for dead entity #N` once per cleanup. **Completion (2026-06-26, SECOND live crash — the fix above was half-done):** it guarded the inner `EntityStatesWithNames` but left the OUTER `StarSystemStates[starSys]` a hard index. Clicking a label whose **star system** isn't in the current `StarSystemStates` (a faction switch rebuilds it; a system can leave `KnownSystems`) threw `KeyNotFoundException` on the OUTER dictionary and crashed the process the same way — confirmed live (key `'50cad7a5-…'`, a system not in the active view; caught because the new rotating `game_logs/` pages + `console_output.txt` captured the trace, the gauge proving its worth). Both sites now `StarSystemStates.TryGetValue(starSys, out var sysState)` first, then the inner entity lookup. **Sharpened rule: guard EVERY level of a nested dictionary access from a UI path, not just the leaf** — `a[x].b[y]` has two hard indexes, and either can throw.

15. **Orbit rendering froze the game at extreme zoom (fixed 2026-06-26).** Zooming far in on a ship orbiting a small body (a Jupiter moon) made the game progressively SLOWER until a full freeze — no crash, no exception, the log just stopped (the third no-trace case; caught by the new `[HANG]` watchdog + the developer's repro). Cause: `OrbitEllipseIcon` transforms a FIXED 181 points and draws ~180 line segments **every frame for every orbit, with no on-screen-size cull.** At extreme zoom the big orbits (Jupiter's around the Sun, the moon's around Jupiter) become MILLIONS of pixels across — pure off-screen clutter — but their full transform+draw still runs, and `SDL.RenderLine` chokes rasterizing lines whose endpoints are astronomically off-screen; the further you zoom, the more extreme those coordinates, so frame time climbs until a frame never finishes. **Fix:** `OrbitEllipseIcon.OnFrameUpdate` (which runs EVERY frame) computes the orbit's on-screen radius (`SemiMaj*(1+e) * 6.6859e-12 * camera.ZoomLevel`, the same scale the transform uses) and, if it exceeds `_maxOrbitScreenRadiusPx` (50000 px ≈ 25 screens), sets `_offScreenSkip` and returns early — skipping both the transform and the draw (`Draw` checks the flag first). **Reversible by construction: because the flag is recomputed per frame from the CURRENT zoom, zooming back out makes the very next frame see a small radius, clear the flag, re-run the transform, and the ring reappears — it's a per-frame "worth drawing right now?" decision, not a permanent removal.** The orbit you zoomed in to SEE is screen-sized, so it always draws; only the absurd off-screen rings are skipped. **Gauges:** `[HANG]` (watchdog, gotcha #12 third layer) catches a full freeze; `[PERF] ⏱ slow frame Nms` (`PostFrameUpdate`, throttled) logs the slowdown CLIMB before it. **Rule: any per-frame render cost that scales with zoom/extreme coordinates must be bounded — cull by on-screen size; don't trust SDL to clip extreme lines cheaply.** (Other trajectory icons — `HyperbolicIcon`, Newton trails — may need the same cull; flagged.)

> **Extended to CONTACT BLIPS (2026-07-18, Operation Earthfall C1.1 — the 2050-04-22 freeze).** The gotcha-#15 cull was applied only to `OrbitEllipseIcon`; the fog-of-war **contact blips** (`SensorContactIcon`, drawn from the engine's last-known `SensorPositionDB`) were the ONE unculled path left, and they're exactly the one that goes degenerate — a lost track anchor gives a NaN/infinite `AbsolutePosition`, and a `fogLag` drift (the log showed it jump to ~25,000 km right before the real session froze) parks the blip astronomically off-screen. The base `Icon.Draw` **clamps** such a coordinate to `int.Min/MaxValue` rather than **throwing**, so the per-item `SafeDraw` try/catch never fires — instead `SDL.RenderLine` chokes rasterising the astronomically off-screen line, frame time climbs, and the client FREEZES with no exception (findings/A1-freeze.md H1). **Fix:** `SensorContactIcon.OnFrameUpdate` now computes the blip's world + on-screen position and, if either is non-finite or the screen coordinate is beyond `_maxBlipScreenCoordPx` (1,000,000 px — matches `SimpleCircle.MaxSafeCoordPx`), sets `_offScreenSkip` and returns early (skips transform AND draw; `Draw` checks the flag first). Recomputed every frame, so a contact that becomes valid/on-screen again redraws next frame — a per-frame "worth drawing right now?" decision, same as the orbit cull. A normal on-screen finite blip takes the unchanged base path. **Companion breadcrumb: `SystemMapRendering.Draw` now stamps a per-LIST sub-stage** (`SessionLog.CurrentStage = "GalacticMap.Draw/contacts"` | `/orbits` | `/ships` | `/labels` …) before each icon list, so if a native hang ever survives this it names the exact list instead of the coarse `'GalacticMap.Draw'`. And the hang watchdog threshold dropped **5000 → 3500 ms** (`SessionLog.HangThresholdMs`) — the worst legit frame ever seen was ~2100 ms (startup), so a freeze is now named ~1.5 s sooner without false positives. Runtime-unverified (CI can't run the client); the local check lives in `docs/CLIENT-TEST-CHECKLIST.md` under "OPERATION EARTHFALL — C1.1".

> **Slow-frame STAGE breakdown (added 2026-06-27).** A live play-test showed a steady ~337 ms/frame (~3 FPS) even *zoomed out and paused* with a 35-ship combat scenario — a constant cost the per-frame `⏱ slow frame Nms` gauge flagged but couldn't localise (it timed the whole frame, not which part). So the slow-frame line now appends a **three-stage split** — `update(state+transforms) X / map-draw(SDL lines) Y / ui(windows) Z ms` — timed in `PulsarMainWindow` around `_state.Update()` (window state + the map's per-frame `OnFrameUpdate` transform loops), `GalacticMap.Draw()` (SDL line rasterisation), and `RenderUI()` (name icons + every ImGui window's `Display()`). The biggest of the three is the culprit; this narrows the next play-test from "a slow frame" to one of three subsystems before any fix is attempted (the Visibility Gate: build the better gauge before theorising). Cheap (three `SDL.GetTicks()` deltas) and only formats the string on an already-slow, throttled frame.
>
> **Per-CATEGORY map breakdown (added 2026-06-28 — finer gauge, the "how full can a system be" question).** The three-stage split says "map-draw heavy" but not WHICH icon list. `SystemMapRendering.MaybeLogMapPerf` now times each list separately in `Update()` (the per-frame transform) and `Draw()` (the SDL draw) and logs a throttled (~2 s) `⏱ map breakdown ms — orbits u../d.. (N) | rings(widgets) .. | ships .. | bodies .. | moves .. | contacts .. | labels .. (N)` line whenever map work exceeds ~8 ms. `u`=transform (Update stage), `d`=SDL draw (Draw stage), `(N)`=that list's entity count. **Static-analysis hypothesis to confirm:** ORBITS — `OrbitEllipseIcon` re-transforms a fixed **181 points** AND issues **~180 individual `SDL.RenderLine` calls** (the ghost ring) per orbit **every frame** (plus `OnPhysicsUpdate`'s 181-point index search, also per-frame per the file's own TODO). Aurora draws an orbit as one cheap ellipse primitive + caches; we redraw 180 segments/body/frame — so hundreds of orbiting bodies is exactly where we'd diverge. **The fix once the gauge confirms it (ranked):** batch the single-colour ghost ring into one `SDL.RenderLines` call (180→1, as `WarpMovingIcon` already does — but it needs `_fullOrbitDrawPoints` changed from `SDL.Point[]` to `SDL.FPoint[]`, a base-class ripple, so verify before doing it blind); LOD the segment count by on-screen orbit size; cache the transform when the camera is static; move `OnPhysicsUpdate` off the per-frame path. **One play-test names the number; don't pre-optimise blind.**
