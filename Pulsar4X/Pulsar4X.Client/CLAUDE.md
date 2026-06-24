# Pulsar4X.Client — UI Reference

ImGui.NET + SDL2 immediate-mode UI. The only runnable application in the solution. Lives in `Pulsar4X/Pulsar4X.Client/`.

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
| `FleetWindow` | `FleetWindow.cs` | ✅ Functional | Fleet listing, selection, basic orders |
| `ColonyManagementWindow` | `ColonyManagementWindow.cs` | ✅ **Full economy UI** (verified in code 2026-06-24) | Colony picker + tabs: **Summary** (planet/pop/infra-efficiency/installed components/stockpile of raw+refined), **Production** (`IndustryDisplay` — queue refine/build jobs via `IndustryOrder2`: batch/repeat/auto-install/priority/cancel), **Construction**, **Mining** (per-mineral rate/annual production/years-to-depletion). The minerals→refined→components loop is fully see-and-do here. **Live-behaviour unverified** (CI can't build the client). |
| `PlanetaryWindow` | `PlanetaryWindow.cs` | ✅ (installations fixed 2026-06-24) | General info ✅ / Mineral deposits ✅ / **Installations ✅ — tab now gates on `ComponentInstancesDB` and renders via `componentsDB.Display(...)` (`:102,220`), NOT the dead `InstallationsDB`.** |
| `ShipDesignWindow` | `ShipDesignWindow.cs` | ✅ Functional | Ship design and component assignment |
| `ComponentDesignWindow` | `ComponentDesignWindow.cs` | ✅ Functional | Component designer with NCalc formulas |
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
| `Debug/*` | `Debug/*.cs` | ✅ Dev tools | Data viewer, blueprint inspector, entity inspector, performance monitor |

---

## GlobalUIState

The central hub for all UI state. Key fields:

```csharp
Engine.Game Game                          // reference to the running game
Entity Faction                            // the player's currently selected faction entity
Entity PlayerFaction                      // same (legacy duplicate, keep consistent)
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

### GroundCombatWindow — MISSING ENTIRELY

No window exists for ground combat. When `GroundCombatDB` (to be created) is present on a colony entity, a new `GroundCombatWindow` should be reachable from `PlanetaryWindow` tabs and from the system map context menu.

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

2. **`PlanetaryWindow.old.cs` is excluded from compilation.** The `.csproj` has `<Compile Remove="PlanetaryManagement\PlanetaryWindow.old.cs" />`. Do not reference it. It is a legacy file, possibly with stale API usage.

3. **`GalaxyMap.cs` in the Rendering directory is also excluded.** `<Compile Remove="MapRendering\GalaxyMap.cs" />`. Not the same as `GalaxyWindow.cs`.

4. **Window instances are keyed by string name.** `NonUniquePulsarGuiWindow` uses `"WindowType|entityId"` as the key in `LoadedNonUniqueWindows`. If you rename a window class, existing open window references in `GlobalUIState` become orphaned (harmless but leaks memory). Always use the static `GetInstance()` factory.

5. **The `Helpers.RenderImgUITextTable()` helper** renders a 2-column or N-column table from `List<string[]>` row data. Use it for consistent formatting across the info panels — it handles column alignment.

6. **Console output is BUFFERED when `launch.bat` redirects it to a file — it only flushes on game exit.** `launch.bat` runs `dotnet run > console_output.txt 2>&1`. With stdout redirected to a file (not a console), .NET buffers `Console.WriteLine` and does not flush until the process exits. So a mid-session `Console.WriteLine` (e.g. a DevTools action) is **absent from the file while the game is still open** — which made a spawn look like it "produced no log" (2026-06-24; the captured file was 100% build warnings, zero runtime lines). Two fixes: (a) **close the game fully before reading `console_output.txt`**, and (b) for diagnostics that must show up live, flush after writing — see `DevToolsWindow.DevLog()` (`Console.Out.Flush()`). Build-step warnings are *not* affected (the build flushes its own output), which is why a buffered capture shows warnings but nothing from the run.

7. **A spawned ship orbits at 2× the planet's RADIUS — it's hidden under the planet icon at the system view.** `ShipFactory.CreateShip(design, faction, parent, name)` places the ship in a circular orbit at `parent.RadiusInM * 2` (~12,000 km for Earth). At the zoomed-out system view (Earth's orbit is ~150 million km), that is sub-pixel **on top of the planet icon**, so a freshly-spawned ship looks like it didn't appear. It did — **zoom into the parent body, or open the Fleet window, to see it.** The icon chain itself is fine (`EntityManager.AddEntity` → `MessageTypes.EntityAdded` → `SystemState.OnEntityAddedMessage` → `OnEntityAdded` → `SystemMapRendering.AddIconable`). DevTools "Spawn Ship" now reports the system ship count after a spawn as proof it landed. (2026-06-24: this was the real cause of "the spawner didn't work".)

8. **New Game builds a colony but NO starting fleet (found 2026-06-24).** `NewGameMenu.CreateGameCore` builds the start piecemeal: `FactionFactory.CreateBasicFaction` (blank faction, no fleet) + `ColonyFactory.CreateFromBlueprint`, which only creates ships from fleets **nested in the colony blueprint** (`colonyBlueprint.Fleets`). But the base-mod start data defines its fleets at the **faction level** (`GameData/basemod/ScenarioFiles/uef.json` top-level `"fleets"`: gunship/freighter/surveyor), which only the *scenario loader* `DefaultStartFactory.LoadFromJson` reads — **the wizard doesn't use it**. Net: a New Game gives you a colony and an **empty sky** (0 ships, 0 fleets) — empty Fleet window, nothing to control. Confirm with the new DevTools **"Dump State"** button (reads 0 fleets). Fix = build the intended fleet on the wizard path (nest the fleets under the colony blueprint, or have `CreateGameCore` create them). Note: a bare `ShipFactory.CreateShip` also adds the ship to **no fleet** — DevTools "Spawn Ship" now joins spawned ships to a "Spawned Ships" fleet so they're controllable.

9. **New Game wizard must guard against an empty `_modDataStore` (fixed 2026-06-22).** `NewGameMenu.DisplayModsPage()` "Next" handler calls `LoadEnabledMods()` then sets defaults with `_modDataStore.Species.First()` / `.Themes.First()` / `.Colonies.First()`. `LoadEnabledMods()` returns early (leaving the store empty) when **no mod is enabled** — so if the player presses Next with every Enable box unchecked, `.First()` throws `InvalidOperationException: Sequence contains no elements` and the **whole app crashes** (the wizard runs inside the render loop, not behind a try/catch). It now checks `.Any()` first and shows an inline error (`_modsPageError`) instead of advancing. `QuickstartGame()` already had the equivalent `.Any(...)` guard — keep the two paths in parity. Same class of bug as the unguarded `.First()` calls in `CreateGameCore` (`SystemBodies[BodyId]`, `Colonies[ColonyId]`); validate selections before dictionary/`.First()` access in any New Game step.
