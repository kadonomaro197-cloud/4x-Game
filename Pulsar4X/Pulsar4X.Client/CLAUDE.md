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
| `FleetWindow` | `FleetWindow.cs` | ✅ Functional | Fleet listing, selection, basic orders. **+ Combat tab (2026-06-25)** — see "Fleet Combat tab" below. |
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

**Testing caveat — sections 2 and 3 verify on an IDLE fleet (no enemy needed); section 1 needs a live battle.**
A fresh New Game has **no fleet at all** (gotcha 8) and **no hostile faction**. The enemy-spawn tooling now exists
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

8. **New Game builds a colony but NO starting fleet (found 2026-06-24).** `NewGameMenu.CreateGameCore` builds the start piecemeal: `FactionFactory.CreateBasicFaction` (blank faction, no fleet) + `ColonyFactory.CreateFromBlueprint`, which only creates ships from fleets **nested in the colony blueprint** (`colonyBlueprint.Fleets`). But the base-mod start data defines its fleets at the **faction level** (`GameData/basemod/ScenarioFiles/uef.json` top-level `"fleets"`: gunship/freighter/surveyor), which only the *scenario loader* `DefaultStartFactory.LoadFromJson` reads — **the wizard doesn't use it**. Net: a New Game gives you a colony and an **empty sky** (0 ships, 0 fleets) — empty Fleet window, nothing to control. Confirm with the new DevTools **"Dump State"** button (reads 0 fleets). Fix = build the intended fleet on the wizard path (nest the fleets under the colony blueprint, or have `CreateGameCore` create them). Note: a bare `ShipFactory.CreateShip` also adds the ship to **no fleet**, and the client **cannot** add it directly — `FleetDB.SetParent`/`AddChild` are engine-internal and `FlagShipID` is read-only (trying it from `DevToolsWindow` **broke the client build**, which CI can't catch). Client-side fleet changes go through the **order system** (`FleetOrder.AssignShip`/`CreateFleetOrder` → `OrderHandler.HandleOrder`, see `FleetWindow.cs`). The proper "controllable ships" fix is **engine-side** (build the fleet in the start path, where the fleet API is accessible).

9. **New Game wizard must guard against an empty `_modDataStore` (fixed 2026-06-22).** `NewGameMenu.DisplayModsPage()` "Next" handler calls `LoadEnabledMods()` then sets defaults with `_modDataStore.Species.First()` / `.Themes.First()` / `.Colonies.First()`. `LoadEnabledMods()` returns early (leaving the store empty) when **no mod is enabled** — so if the player presses Next with every Enable box unchecked, `.First()` throws `InvalidOperationException: Sequence contains no elements` and the **whole app crashes** (the wizard runs inside the render loop, not behind a try/catch). It now checks `.Any()` first and shows an inline error (`_modsPageError`) instead of advancing. `QuickstartGame()` already had the equivalent `.Any(...)` guard — keep the two paths in parity. Same class of bug as the unguarded `.First()` calls in `CreateGameCore` (`SystemBodies[BodyId]`, `Colonies[ColonyId]`); validate selections before dictionary/`.First()` access in any New Game step.

10. **SM (Space Master) mode switches the VIEWED faction to the Game Master faction** — `GlobalUIState`'s SM toggle calls `SetFaction(Game.GameMasterFaction)` on enable and `SetFaction(PlayerFaction)` on disable (`GlobalUIState.cs:498-508`). The Game Master faction owns **no fleets** and has **no unlocked armor/tech**. Consequences confirmed live 2026-06-24, all the same root cause: (a) the **Fleet window shows nothing in SM mode** — it filters by `_uiState.Faction` (= Game Master); your fleets aren't gone, **exit SM mode to see them**; (b) **spawned/own ships are invisible in SM mode** for the same reason — exit SM and they appear; (c) windows that read `_uiState.Faction` and assume player data **crash** — `ShipDesignWindow.RefreshArmor()` hard-indexed `factionData.Armor["plastic-armor"]`, which the Game Master lacks → `KeyNotFoundException` → whole-client crash (fixed: default to the first available armor, never hard-index). **`_uiState.PlayerFaction` stays the real player throughout** (only `_uiState.Faction` changes) — that's why DevTools spawns still correctly belong to the player. **Rule: any window usable in SM mode must tolerate the viewed faction having empty data — never hard-index a faction dictionary, and use `_uiState.PlayerFaction` when you specifically mean the player.**

> **DevTools "Faction Switcher (SM)"** generalises this beyond the GameMaster/Player toggle: it lists every entry in `Game.Factions` and a "View as" button calls the same `_uiState.SetFaction(faction)` to switch the *viewed* faction to any of them (with a "Back to player" → `SetFaction(PlayerFaction)`). It's the tool for watching an auto-resolved battle from either side's perspective (engine combat spine step 9). It inherits the caveat above — switching to a bare faction (GameMaster, an NPC with no known systems) shows empty Fleet/System views; that's expected, switch back. The switch is wrapped in try/catch so a faction missing `FactionInfoDB` reports an error instead of crashing the client.

11. **Inspecting a FOREIGN/NPC-owned entity hard-indexed that owner's locked faction data → whole-client crash (fixed 2026-06-25).** Same root cause as #10 (a faction dictionary indexed for data the faction doesn't have), but the trigger is the entity's **owner** faction, not the viewed faction — so it bites even outside SM mode. Confirmed live: the developer used DevTools to spawn 6 hostile "Cargo Courier" ships around Ceres (a bare faction from `CombatSandbox.SpawnHostileFleet`, whose `FactionDataStore.CargoTypes` is **empty** — all cargo types sit in `LockedCargoTypes` until tech unlocks them, see Factions gotcha #4), zoomed in, and clicked a ship. Because ships render at ~2× body radius they sit **on top of** the Ceres icon (gotcha #7), so the click opened the *ship's* `EntityWindow`. Its cargo-bar block did `factionInfoDB.Data.CargoTypes[sid].Name` on the **owner** (Hostiles) faction → `KeyNotFoundException` → the SDL `Run` loop has **no try/catch**, so the process crashed. The trace went to **stderr**, which is *not* in `game_log.txt` (Program.cs redirects stdout only), so the log just stopped after the spawn line — looking exactly like a freeze. **Fix:** three sibling sites now look the cargo type up defensively (unlocked `CargoTypes` → `LockedCargoTypes` → fall back to the id, never a hard index): `EntityWindow.cs:~1120` (DisplayShipContent cargo bars), `Interface/Displays/CargoStorageDBDisplay.cs:23-24`, `Interface/Windows/CreateTransferWindow.cs:178`. **Rule extends #10: never hard-index a faction dictionary for ANY entity whose owner might be foreign/NPC** — a spawned hostile, an NPC trader, anything not the player. The `CargoGoods.GetMaterial(...).Name` reads in `DebugWindow`/`ManuverNode` are the same class but are debug/uncommon paths; harden them if they ever fire.

12. **The render loop now has a visibility gauge — `PulsarMainWindow.SafeRender(context, action)`.** Each per-frame piece (map draw, name icons, every window's `Display()`, the maneuver panel) runs through `SafeRender`, which catches any exception, logs the **full** stack trace ONCE per unique error to the captured log via `Console.WriteLine` (→ `game_log.txt`, because stdout is redirected there), and **skips just that piece for the frame** instead of crashing the whole app. ImGui error recovery (`ConfigErrorRecovery = true`, set in `SDL3Window.Run`) cleans up any window/stack left half-open by the throw. This is the sensor the Visibility Gate demanded: before it, an unhandled render exception was an invisible hard crash (trace to stderr, not the log). After it, a faulting window names itself in `game_log.txt` and the game stays usable. **If you see `[RenderError] <context> threw…` in the log, that context (a window class name or a draw stage) is where the bug is** — the dedupe means it's logged once, so don't expect it to repeat. Don't "fix" a window by relying on SafeRender to swallow its faults — it's a safety net + a gauge, not a license to leave a Display() that throws.

> **Map granularity (added 2026-06-25 with the warp "fleets jumped to the Sun" investigation).** The whole-map `SafeRender("GalacticMap.Draw", …)` wrapper turned out too coarse: if ONE icon throws mid-draw (e.g. a NaN coordinate from a mid-warp/detached position hitting `Convert.ToInt32` → `OverflowException` in a transit/move icon), it aborted the **rest** of the map for that frame — orbit/transit lines (drawn first) survive, ship icons + labels (drawn after) vanish. That is exactly the live "stuck blue lines between Earth and the Sun, ships gone" symptom — a *render artifact masking* a movement bug, not the movement bug itself. Fix: `SystemMapRendering.DrawIcons` and the label loop now wrap **each item** in `SystemMapRendering.SafeDraw`, which logs `[RenderError] map item '<TypeName>' …` once and skips just that item so the rest of the map renders. The coarse `GalacticMap.Draw` wrapper stays as a backstop. **Lesson: put the gauge at the granularity of the thing that fails** — per-item names the culprit entity; per-map only says "the map broke." (The underlying warp bug — `WarpMoveProcessor` reparents a ship's position to the system Root/Sun on launch, so an intra-system hop like Earth→Luna can read as a jump to the Sun — is a separate, PRE-EXISTING movement issue, not the combat code; tracked in `SESSION_STATE.md`.)

13. **Session recorder — the "flight recorder" for live play (`SessionLog`, built 2026-06-26).** `Pulsar4X.Client.SessionLog` (`SessionLog.cs`) writes a readable, greppable play-by-play of the player's actions + periodic state to the captured log, so a bug report **is** the log instead of "reproduce it and send a log." Every line is **flushed immediately** (`Console.Out.Flush()`), so a freeze or hard crash still leaves the full trail up to that instant. Categories: `[ACTION] [VIEW] [TIME] [CAMERA] [SELECT] [DRAG] [STATE]`. Toggle the whole thing with `SessionLog.Enabled`.
    - **Where the log lives:** `game_log.txt` in the **repo root** (next to `console_output.txt` / `launch.bat`), NOT `%AppData%`. `Program.cs` redirects `Console.Out`/`Console.Error` there and walks up from the running exe to the folder holding `.git` or `launch.bat` (falls back to the exe dir, then `%AppData%`). This is a different file from `console_output.txt`, which `launch.bat` fills with **build** output. Runtime lines (`[ACTION]`/`[SELECT]`/`[Combat]` etc.) go to `game_log.txt`.
    - **The hooks (where state is captured):** time controls → `TimeControl.PausePlayPressed`/`OneStepPressed` (`[TIME]`); camera pan/zoom → `SystemMapRendering` `_camera.PanOccured`/`ZoomOccured`, **throttled ~400 ms** via `_lastCamLogTick` so a drag doesn't flood the log (`[CAMERA]`); entity click/select → `GlobalUIState.EntityClicked` (`[SELECT]`); **fleet/ship move/warp order → BOTH `FleetWindow.cs` move button (`[ACTION]` "move order: fleet #N -> 'Body'") and `WarpOrderWindow.cs` right-click "Warp to a new orbit" (`[ACTION]` "warp order: ship #N -> 'Body'")** — these are the two ways to issue a warp, the trigger for the teleport bug, logged right before the teleport check fires. (The teleport *detector* is trigger-agnostic — it scans all ships every heartbeat — so even an unhooked order path can't hide a teleport; these hooks just show the trigger in the log.) faction/view switch → `GlobalUIState.SetFaction`, which also auto-dumps ship positions (`[VIEW]` + `[STATE]`); periodic snapshot → `PulsarMainWindow.PostFrameUpdate` calls `SessionLog.Heartbeat(...)` **every ~3 s** (wall-clock `SDL.GetTicks()`, so cadence is steady regardless of game speed) reporting game clock / run-or-paused / step / selection / ship count (`[STATE]`).
    - **The teleport gauge — now automatic.** `SessionLog.CheckForTeleports(StarSystem)` runs **inside every heartbeat** (~3 s): it scans all ships and logs a `⚠ TELEPORT` line for any whose anchor (`Parent`) is null/invalid **or** whose distance from the Sun is under 1 Gm (`TeleportSunDistThreshold_m`; nothing real orbits that close — Mercury is ~58 Gm out), including the ship's `moveType` (Orbit/Warp) as the smoking gun. So the "teleport to Sun" bug now **announces itself within 3 s of happening, with no faction-view switch needed.** The older `SessionLog.DumpShipPositions(StarSystem, context)` (logs *every* ship's Sun-distance + parent) still exists for an explicit before/after snapshot and auto-fires on view switch. Diagnosis as of 2026-06-26: the **clean warp path is correct** (`WarpMoveProcessor.StartNonNewtTranslation` reparents to the system root but `MoveState.SetParent` preserves absolute position) — the teleport is an **interaction edge case** in a single time-step (warp + orbit + combat-destroying-ships) where a ship's `Parent` goes null/invalid while `RelativePosition` is still a small orbital offset, so `AbsolutePosition` (MoveState.cs:44 fallback) collapses to the origin/Sun. The detector exists to catch which path does it; **don't blind-fix the warp code** (no warp-position test exists; CI's smoke test only checks positions are finite, not correct).
    - **The heartbeat is wrapped in `SafeRender`** in `PostFrameUpdate` because `GlobalUIState.SelectedSystem` is a computed property (`StarSystemStates[SelectedStarSystemId].StarSystem`) that **throws** when no system is selected — a fault there logs once and the game keeps running rather than crashing on a diagnostic.
    - **`[DRAG]` is reserved, not wired — there is no drag-box/marquee multi-select in the game.** The system map hit-tests **one** interactable per click (`SystemMapRendering` `MouseButtonDown` → `item.Contains` → `OnPointerDown`); a mouse-drag on the map **pans the camera** (already logged as `[CAMERA]`). If true rubber-band multi-select is ever built, log it through `SessionLog.Drag(...)` — the category already exists.
    - **Rule:** any new player-facing action worth replaying should get a one-line `SessionLog.*` call at the point the action is committed (not where it's drawn) — cheap, flushed, and it makes the next "it froze / it did something weird" report self-diagnosing.

14. **Clicking a label/icon for a REMOVED entity hard-indexed the entity-state dictionary → whole-client crash (fixed 2026-06-26).** The click-path sibling of gotcha #11 (which was about *faction* dictionaries). `GlobalUIState.EntityClicked` and `EntitySelectedAsPrimary` did `StarSystemStates[starSys].EntityStatesWithNames[entityGuid]` — a hard index. When a ship is **destroyed in combat**, its entry is removed from `EntityStatesWithNames` (`SystemState.Update` → `_entitiesWithNames.Remove`), but its **clickable label can outlive it on screen** (the label-cleanup path lags the state removal — and a dead entity's `AbsolutePosition` collapses to the **origin/Sun**, so those stale labels pile up right on the star — this is the "ships teleported to the Sun" visual). Clicking one fed a now-missing key to the hard index → `KeyNotFoundException`, and the SDL `Run` loop has **no try/catch**, so the **whole process crashed**. Confirmed live 2026-06-26 (key `'676'`, a destroyed Earth-fleet ship; the crash only became visible because the developer's `launch.bat` now captures **stderr** — the trace is *not* in `game_log.txt`, which is stdout-only). **Fix:** both sites use `EntityStatesWithNames.TryGetValue(entityGuid, out var state)` and ignore the click (logging `[SELECT] ignored click on stale/removed entity #N`) if it's gone. **Rule extends #10/#11: never hard-index ANY runtime dictionary keyed by entity/faction id from a UI path — `TryGetValue` + bail. UI state (labels, icons) can lag engine state (entity removed) by a frame or more; the click handler must tolerate a stale id.** *Visual cleanup (done 2026-06-26):* `SystemMapRendering.PruneDeadEntities()` now runs every frame in `Update()` — it scans `_allLabels`, and for any whose `Entity.IsValid` is false (destroyed) calls `RemoveIconable(id)`, dropping the icon + label + interactable together. This is driven by the entity's `IsValid` flag (flipped **immediately** by `TagEntityForRemoval`), not the lagging `EntityRemoved` message that `OnSystemStateEntityRemoved` waits on — so the ghost vanishes the instant the ship dies instead of sliding to the Sun, even while the game is paused after a step. (Why both: the message path stays as the normal cleanup; the per-frame prune is the safety net for the gap.) The `SessionLog.CheckForTeleports` heartbeat (gotcha #13) still flags any dead-entity-at-origin that slips through. Logs `[STATE] pruned ghost icon/label for dead entity #N` once per cleanup.
