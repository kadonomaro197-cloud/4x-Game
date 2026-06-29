# Client-Side Test Checklist — what only YOUR local build can verify

**Why this exists:** CI now *compiles* the client (the `build-client` job, added 2026-06-28), so compile breaks are caught automatically. But CI still **cannot run** the client — it's display-coupled, headless CI can't open a window. So **runtime behavior, rendering, and clicks are verified only by your local build** + the `game_logs/` pages. This is the running list of client things awaiting that local check. Tick them off; tell me what's broken (send the relevant `game_logs/` page) and I'll fix.

> **How to test:** pull the branch → `launch.bat` (captures `console_output.txt` + rolls `game_logs/` pages) → play → for anything weird, send me the page. Close the game fully before reading `console_output.txt` (it's buffered until exit).

---

## ⚠ FIRST — confirm the build config change didn't break your local build
- [ ] **The game still builds AND launches locally.** I changed `Pulsar4X.Client.csproj` (fixed two `HintPath`s: `Libs\ImGui.NET.dll` and `Libs\SDL3-CS.dll` — they were pointing at non-existent subfolders, so a clean checkout couldn't build). CI proved it *compiles* on Linux; you confirm it *runs* on Windows. If the build or launch breaks, that's the first thing to fix.

## Fleet UX
- [ ] **Left-click a fleet selects it immediately** — no menu, no dead-click, no "click elsewhere first." Click straight from one fleet to another and it just selects.
- [ ] **Right-click shows the context menu** (the menu moved to right-click only).

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
