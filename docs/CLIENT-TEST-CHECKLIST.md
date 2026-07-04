# Client-Side Test Checklist — what only YOUR local build can verify

**Why this exists:** CI now *compiles* the client (the `build-client` job, added 2026-06-28), so compile breaks are caught automatically. But CI still **cannot run** the client — it's display-coupled, headless CI can't open a window. So **runtime behavior, rendering, and clicks are verified only by your local build** + the `game_logs/` pages. This is the running list of client things awaiting that local check. Tick them off; tell me what's broken (send the relevant `game_logs/` page) and I'll fix.

> **How to test:** pull the branch → `launch.bat` (captures `console_output.txt` + rolls `game_logs/` pages) → play → for anything weird, send me the page. Close the game fully before reading `console_output.txt` (it's buffered until exit).

---

## ⚠ FIRST — confirm the build config change didn't break your local build
- [ ] **The game still builds AND launches locally.** I changed `Pulsar4X.Client.csproj` (fixed two `HintPath`s: `Libs\ImGui.NET.dll` and `Libs\SDL3-CS.dll` — they were pointing at non-existent subfolders, so a clean checkout couldn't build). CI proved it *compiles* on Linux; you confirm it *runs* on Windows. If the build or launch breaks, that's the first thing to fix.

## ⭐ THIS BRANCH — `claude/4x-space-stations-design-t0a4b0` (space stations + the ground map) — added 2026-07-03

Everything below is engine-CI-green; these are the *runtime/render* checks only your local build can do. Full seven-field detail is in `docs/TESTING-TRACKER.md` (rows **G3**, **G4**). Work top to bottom — the ground-map items are the headline.

### Ground map — the planet surface (slices 3 + 4, the new work)
- [ ] **G3 — the Planet View window opens and draws.** Start a New Game → click **Earth** on the system map → right-click it → context menu → **"Planet view (regions)"**. A window should open showing **three region columns** (the centre region plus its two ring neighbours), each painted as **stacked terrain bands** (blue ocean, green forest, grey mountains, etc.) with a label per band. Below is a detail strip: area, crossing-time, installations. *What right looks like:* three coloured columns, sensible terrain, no blank window. *If it's wrong:* send me the `game_logs/` page — a draw fault logs `[RenderError] PlanetViewWindow` once instead of blanking the UI.
- [ ] **G3 — the ring rotates (no seam).** In that window, click **◀ West** / **East ▶** (or click a side column). The centre should swing to the next region, and rotating past the last region should **wrap back to the first** — no dead end, no gap. This is the "Pacific survives" topology: the four regions are a ring, and you're always looking at three of them.
- [ ] **G3 — Earth known, LUNA is fog (the corrected model).** Earth (your home colony) should show real terrain. **Now open the Planet view of Luna** — it should show grey **"? UNSURVEYED"** fog. (Fixed 2026-07-04: nothing is pre-surveyed except the world you colonise, and moons now get a region layer, so Luna is a real survey target. Before, all of Sol was surveyed and there was nothing to do.)
- [ ] **G4 — surveying Luna lifts the fog LIVE (the money shot).** You already start with a **"Surveyor I"** ship in your **Science Fleet** (it carries the geo-survey component — no need to build one). Open **Luna's** Planet view (fog) and leave it open → order the Surveyor I to Luna and let the geological survey finish → Luna's fog columns should turn into **real terrain the moment the survey completes**, in the open window, no reopen. (Engine reveal is CI-proven; this confirms the window re-reads it live.) *Snag to report:* if a body can't be surveyed at all it's missing `GeoSurveyableDB` — tell me and I'll add it.
### Ground map — the TACTICAL layer (slice 5e, 2026-07-04 — the "map I can navigate" you were waiting on)
The Planet View is no longer just a readout. These checks need **units on the ground** — the fastest way to get some is DevTools/SM (raise a unit) or, once a JSON template exists, build one; until then a garrison may only appear where combat/spawn tooling placed it. Report what you see either way.
- [ ] **G5 — units draw in their region.** Open the Planet view of a world that has ground units. Each region column should show **unit tokens** along the bottom — a coloured box reading e.g. `I x3` (I=Infantry, A=Armor, R=Artillery) with a little **health bar**. **Cyan = yours, red = hostile.** *What right looks like:* tokens sit in the right region, colour matches owner, count/health look sane. *If wrong:* `game_logs/` — a fault logs `[RenderError] PlanetViewWindow` once.
- [ ] **G5 — click-to-move a unit.** Click one of **your** (cyan) tokens — it should highlight (yellow border) and a selection bar appears below with **"March to Region N"** buttons. Click a March button (or click an **adjacent** region column). The status line should read "marched N× … to Region N", and after time advances the units should **arrive** in the new region (a `»` marks them in transit). Non-adjacent / not-your-units should refuse. 
- [ ] **G5 — click-to-place a base.** On a world where you have a **colony**, the Build panel (below the map) should list your installation designs. Pick one → **"Build here"** → the status line confirms it and the centre region's **⚙ building count** ticks up. (This is the "what I build in space is a real building on the ground" rule — the building is placed at that region.) *If no panel:* you have no colony there, or no `PlanetInstallation` design yet — expected.
- [ ] **G5 — terrain + hazards read.** Each region shows a **terrain-class chip** (OPEN / COVER / ROUGH, top-right) and, on hostile worlds, **hazard chips** (e.g. red "Fire Tornadoes", green "Corrosive Superstorm", amber "Dust Storm"). Confirm they match the world (a scorching world shows fire, a calm one shows none).
- [ ] **G5 — heads-up (still owed).** Normal colony buildings (placed via the colony economy UI, no region chosen) don't yet carry a region, so they **won't** draw on the map yet — only ones placed *through* the Build panel do. Giving every installation a home region is the next reconciliation step.

### Space stations (slices A / A2 / D / E / F — earlier this branch)
- [ ] **Deploy a station from a construction ship.** Right-click one of your **cargo/construction ships** → **"Deploy Station Here"**. A station should anchor at the ship's current spot (works even at a star or belt, nowhere you'd colonise), and the ship survives to do it again. Confirm a station entity appears.
- [ ] **Manage the deployed station.** Right-click the new station → **"Manage Station"**. The window should show its host body / structural-integrity (durability) / population / operating cost, and — if it has a constructor module — let you queue+install modules in place.
- [ ] **Materials are pooled from the fleet on deploy.** Deploying consumes frame material (stainless-steel) from the *fleet's* cargo holds, not just the one ship. With enough steel in the fleet it deploys; with too little it should **refuse** (no station, a message). (Engine `StationFactoryTests` prove the math; this is the live feel.)
- [ ] **(Optional) Lagrange markers + listening outpost.** L4/L5 Trojan points should appear as stable named points near a planet's orbit (a deploy can anchor to them). A listening-outpost station flavor runs a sensor scan after deploy.

## Space economy / morale / politics (branch `claude/space-economy-morale`, added 2026-07-02)
All engine-green; these are the *runtime/feel* checks. Full detail + what-right-looks-like is in `docs/TESTING-TRACKER.md` (rows T0, A2, C1, C2, C3, D0–D3).
- [ ] **T0 — New Game boots + clock runs** (the gate): New Game → press play → advance several months → close; `console_output.txt` clean, no exception. Confirms the new blobs/processors (legitimacy, sustenance, manpower, government, diplomacy) don't crash boot or the tick.
- [ ] **Dump Society reads sensibly.** DevTools → **Dump Society (log)** → close → read a `game_logs/` page: each colony shows pop / morale (+factors) / legitimacy / workforce+talent / **pwr-short/food-short** / tax; plus the **government** name and the **diplomacy** ledger line.
- [ ] **Government dials bite (C3).** DevTools → **Government (test regimes)** → *Totalitarian War-State* → Dump Society (name changes) → advance time and watch the effects (tax ceiling higher, research slower under low openness, crew conscripted for a build a consent regime would block). *Liberal Democracy* is the mirror; *Federal Republic* resets.
- [ ] **Morale moves population (A2).** DevTools → Create Colony on Venus/Mercury (hostile) → Dump Society (lower morale) → advance months → Dump again (population emigrated).
- [ ] **Crew gate (C1).** Build a large fleet (or drain the pool) → a ship build **blocks** under the default regime; flip to Totalitarian → it **conscripts** (builds understaffed).
- [ ] **Reactive diplomacy drift (D3).** Spawn a hostile fleet (first contact) → set that faction militarist → advance several months → Dump Society: your view of them cools toward Hostile on its own.

## Fleet UX — ✅ PASSED 2026-07-03 (after the fleet-menu freeze fix)
- [x] **Left-click a fleet selects it immediately** — no menu, no dead-click, no "click elsewhere first." **PASSED.**
- [x] **Right-click shows the context menu** (right-click only). **PASSED.**
- [x] **The Fleet Management window is USABLE.** It used to hard-freeze the instant its list rendered a fleet — a native ImGui assert in `BeginPopupContextItem` (its internal mouse-button query), which reads as a `[HANG]` because the assert's modal blocks the main thread. Fixed by converting all three fleet-window context menus to the explicit `IsItemClicked(Right)+OpenPopup(id)+BeginPopup(id)` pattern. Confirmed live ("it worked").

## ⭐ Branch `claude/4x-game-testing-strategy-19xw8q` — confirmed live 2026-07-03
- [x] **T0 — New Game boots + clock runs** with the full auto-spawn scenario (43 ships, 4 rival factions), no `[FATAL]`/`⚠ TELEPORT`.
- [x] **New Game auto-spawns the combat scenario by default** — 2 player task forces + 4 rival factions (capital-led) at Luna/Venus/Mercury/Mars, no SM button. Toggle off in DevTools › Detection/Fog for a clean start.
- [x] **Large Earth stockpiles** — building isn't resource-gated (50 M minerals + refined materials, warehouse ×10).
- [x] **Visual pass** — planets deeper shades, space darker.
- [ ] **Save/load a PLAYED game** (D1) — the one remaining "survives a session" risk. Play a bit → Save → Load → confirm no exception + state persists. (Engine `SaveLoadWithJobTests` covers the queued-job NRE that was fixed; the full played-game round-trip is the live check.)
- [ ] **Range-ring hover tooltips render** — hover a weapons/sensor/EMCON ring line → a label names the unit + which ring. CI-green; live render unconfirmed.

## Hazards — the headline (the whole cradle-to-grave loop)
- [ ] **Hazards render on the system map.** Corona = faint red-orange ring at the star; solar flare = bright orange (transient). *Note:* gas cloud, debris field, ion storm, and gravimetric anomaly currently **all render the same green** — distinct colors per type is a flagged follow-on, not built yet. So you'll see green blobs; that's expected for now.
- [ ] **Fly a ship into the corona → discovery fires.** You should get an "Environmental hazard discovered: thermal (extreme heat)…" notification, and the counter-research opens.
- [ ] **The research loop pays off.** Research the unlocked counter (Stellar Science → Thermal Shielding) → build **nickel-steel armor** → re-enter the corona → it takes noticeably **less** damage than an unarmored hull.
- [ ] **Normal orbit = ZERO hazard damage.** Park in a normal planetary orbit (Earth, Mercury) → no hazard damage accumulates. Only a genuine close dive toward the star should cook you (inverse-square calibration).
- [ ] **The new environments actually appear in generated systems** — debris field (~25% of systems), ion storm (~15%), gravimetric anomaly (~8%). Explore a few systems and confirm they show up. (Gas clouds are now *corrosive*, not heat.)
- [ ] **(Optional) Supplemental effects feel right** — inside a gas cloud your sensor range should shrink and movement/warp drag; inside an ion storm sensors shrink; a solar flare blinds. (The engine asserts these in CI now via the diorama test; this is just the live feel.)

## ⚠ M-ECON branch (`claude/space-economy-morale`) — the new economy/people/government systems
- [ ] **BOOT TEST (do this first — the highest-value check).** Pull the branch, launch, start a **New Game**, let the clock run a few months, then close and read `console_output.txt`. Confirms the one thing CI can't: that adding three new DataBlobs to every colony (`ColonyMoraleDB` / `ColonyManpowerDB` / `ColonyEconomyDB`) and a new auto-discovered processor (`ColonyEconomyProcessor`) didn't break **startup or the sim**. If it boots and the clock advances with no exception, the foundations are runtime-safe. (Risk it catches: a startup crash from processor auto-discovery, or a blob breaking the New Game JSON path.)
- [ ] **The instrument panel — DevTools › "Dump Society (log)".** Enable SM mode → open Dev Tools → click **Dump Society (log)**. Close the game and read `console_output.txt` for `[DevTools] SOCIETY DUMP` lines: each colony's **pop · morale (+ factor breakdown) · workforce/talent pools · tax → income/mo**, plus the player's **government**. This is how you SEE everything this branch built (most of it has no other UI yet). On the default Earth start the numbers are *neutral* (morale ~50, tax 0%, no government) — expected; it proves the readout works.
- [ ] **Watch a lever move.** DevTools › Create Colony on a HOSTILE world (e.g. Venus/Mercury) → Dump Society → that colony should read **lower morale** (conditions penalty) than Earth, and (after advancing time) **lose population** to emigration. The morale valve, made visible.

## Performance — the number I need
- [ ] **The `⏱ map breakdown` perf number.** Run a busy scenario (a combat with a few dozen ships, or a dense system), and when a frame is slow watch `console_output.txt` / `game_logs/` for a line like `⏱ map breakdown ms — orbits u../d.. (N) | …`. **Send me that line** — it tells me which icon list eats the frame so I can make the targeted render fix and give you the real "how many entities can the lemon PC handle" budget.

---

*Maintenance: when a client feature ships that CI can't runtime-verify, add a line here. Remove a line once you've confirmed it live. This is the standing "runtime gauge is the developer" list — the companion to CI's compile gate.*
