# Play-to-Mars — the full "New Game → invade Mars" walkthrough + gap analysis

**As of:** 2026-07-07 · branch `claude/sol-playtest-earth-map-8r59j6` (verification pass)

**What this is:** the honest, button-by-button trace of everything a player must click to go from a fresh
barebones New Game all the way to **taking Mars** — using ONLY the normal game (no Space Master, no DevTools debug
panel). Every button below is a real ImGui label quoted from the client source, with the file it lives in. Each
phase is marked:

- ✅ **works** — a real player button exists and does the job.
- ⚠️ **works, but rough** — reachable, but the path is fragile / hidden / not first-class.
- ❌ **gap** — no player button and/or no order at all; the engine may be built and tested, but a normal player
  can't reach it.

The point: **the two ENDS of the chain are playable (build an economy/army/fleet; fight and capture on the
surface), but the space-to-ground BRIDGE in the middle — bombard, load, land — is where a normal playthrough
breaks.** Three gaps block a hands-on invasion; the rest is polish.

> This is a *verification* artifact, not a build order committed to. What gets built and when is the developer's
> call (the near-term milestone is `docs/MVP.md`'s "you can take a planet"). See the fix list at the bottom.

---

## The chain at a glance

| Phase | What the player does | State |
|-------|----------------------|-------|
| A | Start a New Game | ✅ |
| B | Build the economy (mine → refine → build) | ✅ (minor friction) |
| C | Research (unlocks what you can design) | ✅ (queue is a double-click, not a button) |
| D | Design & build ships | ✅ (built ship doesn't auto-join a fleet) |
| E | Organize a fleet | ✅ |
| F | **Build the invasion army** | ✅ (this WORKS now — corrects an earlier "DevTools-only" note) |
| G | Scout & geo-survey Mars | ✅ (needs a survey-component ship) |
| H | Move the fleet to Mars | ✅ |
| I | **Have an enemy on Mars to fight** | ❌ gated off (the paused Earth-Mars war) |
| J | Win the orbit (space combat) | ✅ |
| K | **Bombard Mars to soften the garrison** | ⚠️ no first-class order; incidental only |
| L | **Load troops → land them on Mars** | ❌ the invasion bridge — no button, no order |
| M | Fight & capture the surface | ✅ (fight + capture are automatic) |

---

## Phase-by-phase, button by button

### A. Start a New Game ✅
- Main menu **"New Game..."** (`Interface/Menus/MainMenuItems.cs:45`) opens the 3-page wizard, or **"Quickstart"**
  (`:52`) skips straight in with defaults. Both build the *same* barebones world.
- Wizard page 1 **"Select Mods to Enable"** → tick the base mod's **Enable** box → **"Next"**
  (`NewGameMenu.cs:145,167,199`).
- Page 2 **"Select pre-configured Systems to include"** (Sol ticked) → **"Next"** (`:243,283`).
- Page 3 corporation name / **"Select Species"** / **"Select Theme"** / colony / **"Select Starting System"** /
  **"Select Starting Location"** / **"Starting Funds"** → **"Create Game!"** (`:294-421`).
- **What you have the instant you're in game:** one faction ("United Earth Corp"), one Earth colony (population,
  installations, cargo, a scientist + admin commander), ~100M funds — and **nothing else**. No ships, no fleets,
  no enemies, no ground garrison. The full Sol system exists but is **fogged except Earth** (only your home world
  is surveyed). Camera opens centered on Earth.

### B. Build the economy ✅
- Toolbar **"Colony Management"** (`HUD/ToolBarWindow.cs:65`) → pick the colony in the left list.
- **"Mining"** tab — per-mineral rate / annual production / years-to-depletion (readout).
- **"Production"** tab (`Displays/IndustryDisplay.cs`): expand a production line → **"+ New Job"** (`:199`) →
  **"Select a design:"** combo (`:360` — a refined material, a component, or an installation) →
  **"Enter the quantity:"** (`:377`) → optional **"Repeat this job?"** (`:394`) / **"Auto-install on
  completion?"** (`:404`) → **"Queue the job to <line>"** (`:409`). Reorder/cancel with **"^" / "v" / "x"**.
- **Friction:** the **"Upgrade <line>"** button is a dead stub (empty body, `:210-212`). Refining a material sits
  at "MissingResources" until its mineral inputs are stocked.

### C. Research — gates what you can design ✅ (gesture, not a button)
- Toolbar **"Research"** → select a lab → **"Assign Scientist"** → funding slider → **double-click a tech** to
  queue it (`ResearchWindow.cs:464`). There is **no "Queue" button** — the only cue is a header line "Double click
  a tech to add it to this lab's queue."
- **Why it's in the chain:** designing a component (phase D) auto-creates a *"<Name> Design Research"* tech
  (`ComponentDesigner.cs:130-148`); until you research it, the new component can't be built. So the real loop is
  **design → research the auto-tech → it becomes buildable.**

### D. Design & build ships ✅ (the built ship doesn't auto-join a fleet)
- Toolbar **"Design a new component or facility"** → pick a template → set attributes → **"Name"** → **"Save"**
  (`Displays/ComponentDesignDisplay.cs:154`). (The **"Create Template"** button is a disabled stub,
  `ComponentDesignWindow.cs:168-173`.)
- Research the new *"…Design Research"* tech (phase C).
- Toolbar **"Design a new Ship"** → **"Create New Design"** (`ShipDesignWindow.cs:364`) → **"+ Add"** each
  component (`:592`) — including a **"Troop Bay"** if this is an invasion transport → armor combo → **"Design
  Name"** → **"Save Design"** (`:263`). The design lands in the faction's build list.
- Build it like anything else: Colony Management → **"Production"** → a **shipyard** line → **"+ New Job"** →
  **"Select a design:"** (your ship) → **"Queue the job to <line>"**.
- **Gap:** when the ship finishes it spawns in orbit of the colony's body (`ShipDesign.cs:87`) but does **not**
  reliably join a fleet — it appears as a lone "Unattached Ship". You organize it by hand in phase E.

### E. Organize a fleet ✅
- Toolbar **"Fleet Management"** → **"Create New Fleet"** (`FleetWindow.cs:1355`).
- Assign a ship: drag-drop in the fleet tree, or right-click a ship → **"Re-assign ships"** → click a target
  fleet (`:1506,1543`). Unattached ships sit under an **"Unattached Ships"** header.

### F. Build the invasion army ✅ **(this works — corrects the earlier "DevTools-only" claim)**
- The base mod ships start-unlocked ground-unit designs **infantry / armor / artillery** (templates in
  `GameData/basemod/TemplateFiles/installations.json`, unlocked in `earth.json:234-236`). They mount as
  `PlanetInstallation`, so they appear in the normal **Colony Management → Production** list.
- Queue one → materials consumed → it auto-installs at the colony → `GroundUnitAtb.OnComponentInstallation`
  (`GroundCombat/GroundUnitAtb.cs:77`) **raises a `GroundUnit` on the planet**. No DevTools needed.
- **Gap (not blocking):** there's no *custom* ground-unit designer window — you get the 3 pre-authored types only.
  The assembler that would build a custom unit (`GroundUnitAssembly.RegisterAssembledDesign`) is called only from
  tests today.

### G. Scout & geo-survey Mars ✅ (needs a survey ship)
- Build a ship carrying a geo-survey component; put it in a fleet.
- Fleet **"Issue Orders"** tab → **"Geo Survey ..."** (this row only appears if the fleet has survey ability,
  `FleetWindow.cs:337`) → click **"Mars"** → the fleet flies there and surveys. Completing it reveals Mars's
  surface regions **and now seeds Mars's mineral deposits** (this branch's work).

### H. Move the fleet to Mars ✅
- Fleet **"Issue Orders"** → **"Move to ..."** → **"Mars"** (`FleetWindow.cs:329,1095`).

### I. Have an enemy on Mars to fight ❌ **(gated off — the paused Earth-Mars war)**
- A barebones New Game puts **nothing** on Mars. The Mars beachhead (`CombatSandbox.SpawnMarsBeachhead`) and the
  rival-faction auto-scenario (`NewGameMenu.AutoSpawnCombatScenario`) both default **false** — your "hold off on
  the Earth-Mars war" call. So in a normal game there is **no target to invade**. Only DevTools can place an enemy
  garrison/colony on Mars.

### J. Win the orbit (space combat) ✅
- Fleet **"Combat"** tab → **"Attack nearest hostile fleet"** (`FleetWindow.cs:614`), plus the **"Set Doctrine" /
  "Set Posture" / "Set Engagement"** levers (`:705,745,778`). Winning the orbit = destroying the enemy fleet.

### K. Bombard Mars to soften the garrison ⚠️ **(works, but no first-class order)**
- There is **no "Bombard" button**. The only way: right-click your warship → **"Fire Control"** → **"Set
  Target:"** → the enemy **colony** shows up as a plain red name in the target list (because it's a hostile entity
  with a position) → drag it onto a fire-control group → **"Open Fire"** (`FireControlWindow.cs:327,116`). The
  engine then applies population / installation / **garrison** damage (`DamageProcessor.OnColonyDamage` →
  `ApplyGroundBombardment`, which is fully built and tested).
- **Gaps:** you can't target the **planet body** or a **specific region** (bodies are neutral-owned and filtered
  out); the colony is targetable only incidentally, with no cue it's a bombardment; base beam range is tiny (~km,
  so point-blank orbit), and missile delivery/energy-scaling is only partially wired.

### L. Load troops → land them on Mars ❌ **(the invasion bridge — no button, no order)**
- You *can* build a **"Troop Bay"** onto a ship (base-mod `troop-bay`, start-unlocked, mounts as a ship component).
- But there is **no button and no order** to **load** a ground unit onto that bay, and **none** to **land** it on
  Mars. The engine primitives exist and pass tests — `GroundTransport.TryLoadUnit` / `TryLandUnit` /
  `HasOrbitalControl` (`GroundCombat/GroundTransport.cs:82,104,120`) — but their **only callers are the unit
  tests**. Nothing in the game (order class or client button) calls them.
- **This is the single hardest blocker:** you can build the troops and fly an empty transport to Mars, but you can
  never put troops on it or off it in a normal game. (Even a DevTools-assembled test sidesteps this by *raising*
  your units directly on Mars.)

### M. Fight & capture the surface ✅ (automatic)
- Right-click Mars → **"Planet view (regions)"** → **"March to Region N"** / **"Form up"** / **"Set stance"** /
  ROE combo (`PlanetViewWindow.cs:804,930,1084,1049`).
- Once your units share a region with the enemy, the **fight resolves automatically** each hour, and the region
  (then the whole planet) **flips owner automatically** when one side is left standing (`GroundForcesProcessor.cs:188,
  200,217`). No capture button needed.
- **Gap (visibility, not blocking):** there's no ground-combat window / per-unit battle readout — the fight is
  invisible beyond the tokens on the map.

---

## Where we fall short — the gap list, ranked

### Blocking (a normal playthrough CANNOT finish the invasion without these)
1. **L — Load/Land transport has no player interface.** The whole space-to-ground bridge. Engine + tests done;
   no order, no button. **Highest value: it's the one thing that makes an invasion physically possible.**
2. **I — No enemy on Mars in a normal game.** Gated off by the paused war. Without a target there's nothing to
   invade. This is a *decision* (reopen the war) more than a build.
3. **K — Bombardment has no first-class order.** Reachable only by accident through Fire Control; can't aim at a
   planet/region; range/delivery rough. "Soften before you land" is a core step and today it's a hack.

### Non-blocking (friction / polish — the game works without them, but they hurt)
4. **D — a built ship doesn't auto-join a fleet** (extra manual step every build).
5. **C — research is queued by double-click, not a button** (undiscoverable).
6. **Dead stubs:** **"Upgrade <line>"** and **"Create Template"** render but do nothing — cut or implement.
7. **F — no custom ground-unit designer** (only the 3 pre-authored units).
8. **M — no ground-combat window / per-unit readout** (the surface fight is invisible).
9. **Stale docs:** the client CLAUDE.md still says ground units are DevTools-only — the industry path now exists.

---

## Proposed fixes (scoped; not committed to — the developer picks order)

**To make "New Game → take Mars" playable end-to-end with no DevTools, three slices, in order:**

- **Fix L1 — Transport orders + buttons (the bridge).** Add two order classes — `LoadGroundUnitOrder` and
  `LandGroundUnitOrder` (an `EntityCommand` each, routed through `Game.OrderHandler.HandleOrder`, opting into the
  engagement rules as needed) — that wrap the already-built, already-tested `GroundTransport.TryLoadUnit` /
  `TryLandUnit` / `HasOrbitalControl`. Wire buttons: **Load** on the ship/fleet (a troop-bay panel, like cargo
  transfer) when the ship is at a friendly world; **Land** on the `PlanetViewWindow` (or fleet orders) when the
  ship is in orbit and holds orbital control, with a region picker. *Engine half is done; this is mostly UI +
  two thin orders.*

- **Fix K1 — a first-class "Bombard" order.** A `BombardColonyOrder` (or region-targeted strike) that a player
  issues against an enemy colony/region and that routes into the built `OnColonyDamage` / `ApplyGroundBombardment`
  path — with a clear ground-region target and a UI cue. Pairs with checking beam range / leaning on missiles so
  the strike actually reaches from orbit.

- **Fix I1 — a normal-game target on Mars.** Either un-gate a Mars defender for the playtest (a flag flip that
  reopens your Earth-Mars war), or — the better long-term answer — an NPC faction/colony that a normal game can
  actually find and take. This is your call to make, not a pure build task.

**Then the polish pass** (independent, any order): auto-assign a built ship to a fleet (or a "build into fleet"
pick); an explicit research **"Queue"** button; delete/implement the **"Upgrade"** and **"Create Template"** stubs;
a `GroundUnitDesignWindow` for custom units; a `GroundCombatWindow` for the surface fight; refresh the stale docs.

**What is already solid and needs no work:** the New Game wizard, the economy/mining/refine/build loop, research,
ship + component design, fleet creation/movement, geo-survey, space combat, the ground army build, and the
surface march/formation/stance/capture. The engine even has a green end-to-end test for the whole capture chain
(`TakeAPlanetIntegrationTests`) — the missing pieces are the *player's hands on it*, not the mechanics.
