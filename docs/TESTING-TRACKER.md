# Global Testing Tracker

**The standing ledger of every test in the project — past, present, and future, across all branches.** This is the one place that answers, for any piece of work: *is it tested, how, what does a pass look like, what's the likely failure, what's been done about it, and what does passing let us move on to.* It is a living document — add a row when you build something testable; update its status when you test it.

> **Why this exists.** CI proves the engine is *correct*; it cannot prove the game *runs, feels right, or renders* — that's the developer's local build, and those checks are easy to lose track of. This doc is the gauge board for the whole test effort so nothing untested ships silently and no pending check is forgotten. Companion to `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` (the systems map) and the per-branch `docs/CLIENT-TEST-CHECKLIST.md` (this indexes and outlives those).

---

## The three test layers (the pyramid for this project)

| Layer | Runs | Proves | Blind to |
|---|---|---|---|
| **1. Engine tests** (NUnit) | CI, every push (+ local `dotnet test`) | engine **correctness** — logic, math, data integrity | anything display-coupled; "does it feel right" |
| **2. Client compile** (`build-client` job) | CI, every push | the client **compiles** (no typo / bad overload / bad `using`) | whether it **runs** |
| **3. Local runtime / play** | the developer's Windows build only | it **boots, runs, renders, performs, and feels right** | nothing — this is the final gate; but it's manual |

**Rule:** Layer 1+2 are automated and self-maintaining (CI is red or green). **Layer 3 is the backlog this doc mainly tracks** — every item there needs a human at a running build.

### Status vocabulary
🔵 **CI-GREEN** (automated, correctness only) · 🟢 **PASSED** (verified live) · 🟡 **PENDING** (built, needs a run) · 🔴 **FAILING** · ⚫ **NOT-YET** (feature unbuilt; test designed in advance) · ⏸ **BLOCKED** (waiting on a prerequisite)

### Each Layer-3 row carries seven fields
**What** · **Why** · **Method (most efficient)** · **What right looks like** (the normal reading) · **Most likely failure** · **Mitigation in place** · **Unblocks** (what passing lets us move on to).

---

## Layer 1 — Engine tests (CI-green, automated). *Correctness only.*

These are self-maintaining (CI gates them red/green every push). Listed so we know what IS and ISN'T covered. Full inventory: `Pulsar4X.Tests/CLAUDE.md`.

| Suite / area | Guards | Status |
|---|---|---|
| `GameLoopSmokeTests`, `StateIntegritySmokeTests` | sim advances on a generated universe; positions stay finite (NaN catch) | 🔵 |
| `BaseModIntegrityTests`, `NewGameStartSmokeTests`, `ScenarioHarnessTests` | the real New-Game JSON path builds + a colony runs a year without throwing (gotcha #10 guard) | 🔵 |
| Combat suite (`AutoResolve`, `BattleTrigger`, doctrine, retreat, weapon-triangle, `CombatStressLab`…) | the auto-resolve space-combat spine | 🔵 |
| Detection (`SensorDetection`, `SensorEmcon`, `RangeReadout`…) | fog/EMCON/first-strike engine | 🔵 |
| Hazards (`SpaceHazard`, `HazardResearchLoop`, `SpatialEnvironmentsDiorama`…) | all six hazard flavours cradle-to-grave | 🔵 |
| **M-ECON** (`MoraleTests`, `ManpowerTests`, `FactionEconomyTests`, `GovernmentTests`, `DiplomacyTests`) | morale math, manpower pools, crew rule, tax→ledger, government dials/classifier, society readout, diplomacy relationship-track substrate | 🔵 |
| **POLITICS-EXTERNAL** (`DiplomacyIffTests`, `DiplomacyFirstContactTests`, `DiplomacyLogisticsAccessTests`, `DiplomacyTreatyTests`, `DiplomacyCasusBelliTests`, `DiplomacyWarTests`) | IFF reads diplomacy (incl. signed-pact suppression), first-contact records mutual rows, logistics gated on a grant, treaty propose/accept trust-ladder, casus-belli militarism gate, declare-war/make-peace latch | 🔵 |
| **POLITICS-INTERNAL** (`LegitimacyTests`, `LegitimacyProcessorTests`, `RebellionTests`) | per-province legitimacy derived from morale + war-standing (militarism-gated); live processor recompute; collapse → rebellion state machine (begin/quell + reaction-window clock) | 🔵 |
| **POLITICS-REACTIVE** (`DiplomacyReactiveTests`, `ExchangeCatalogTests`) | the "Are we good?" observation→overture engine (stance-gated) + the data-driven exchange catalog (broad coverage, unique keys, every row routed into a system) | 🔵 |
| economy / mining / orbits / save-load / modding | the economy substrate + infrastructure | 🔵 |

**What this layer does NOT cover (the reason Layer 3 exists):** the client running at all; any rendering; player-facing reachability (a system with no UI/data wiring); calibration *feel*; performance; save/load of a *played* game; the New-Game path *from the menu* (the harness mirrors it but isn't it).

---

## Layer 3 — Local runtime / play backlog (the tracked work)

### 📋 Live-run results — 2026-07-02 (main @ Society-tab merge, the `test 1 actual` session)

The developer merged the testing levers + Society tab to main and ran one dense session (logs committed to the repo: `game_log_000..002.txt` + `console_output.txt`). **One session exercised most of the backlog.** The gauge readings (the Visibility-Gate rule — record the actual value, not "it passed"):

| Item | Result | Actual reading from the log |
|---|---|---|
| **T0 / D0** boot + clock + politics blobs | 🟢 **PASS** | boots, clock ran to `2050-12-27`, no `[FATAL]`; `LegitimacyDB`/`DiplomacyDB` attached via Age→Late, no crash |
| **A1** Society readout (Dump Society) | 🟢 **PASS** | `Earth HQ: morale 70.0 [comfort +20.0…] legitimacy 70.0 · workforce 5.8B · tax 0%→0/mo` — real numbers, incl. the M2 housing-comfort +20 live |
| **#39** Age→Late staged state | 🟢 **PASS** | `5 player colonies · Vega Combine 2 (+45+treaty) · Crimson Hegemony 2 (-40) · Terran League 1 (+65+treaty) · WAR · frontier REBELLING` |
| **D2** legitimacy tracks morale | 🟢 **PASS** | `Mercury Colony: morale 10.0 · legitimacy 10.0 !REBELLING (180d left)` — collapse band + rebellion window live |
| **C6** diplomacy ledger + interactive levers | 🟢 **PASS** | ledger: `Vega Friendly (+55) [Trade] · Crimson War (-100) [WAR] · Terran Friendly (+65)`; warmed Vega `80→100`, **DefensivePact signed → True** |
| **C1/C2/C3/D3** the new DevTools levers **fire** | 🟢 **PASS (lever)** | sustenance `power=1 food=1 set`; manpower `committed 5.8B`; government `→ Totalitarian War-State`; diplomacy `Vega militarism → High` |
| **Create Colony** | 🟢 **PASS** | `Create Colony OK: on 'Venus' pop 100,000,000` |
| **B1** combat auto-triggers on play + interrupt | 🟢 **PASS (trigger)** | 7× `enters combat` + `COMBAT INTERRUPT` fired on play (auto-trigger + auto-pause confirmed live) |
| **B2** teleport-to-Sun | 🟢 **PASS** | **zero** `⚠ TELEPORT` lines all session |
| **B5** perf | 🟡 **DATA** | slow frames `509ms` / `1219ms` were **entirely `ui(windows)`** (map-draw ~0) — a WINDOW is the cost, not the map |

**Still open after this run (honest gaps):**
- **Effects-over-time not captured** — the C1/C2/C3/D3 levers *fire*, but there was no second Dump Society *after a time-advance*, so the *bite* (dials cap tax, shortage sours morale, drained pool blocks a build, militarist neighbour drifts hostile) isn't yet gauged live. Next run: apply lever → advance months → Dump again.
- **Society TAB (player UI) unverified** — the dev used DevTools "Dump Society" (the log), not the `ColonyManagementWindow` → Society tab render. That tab's live render/colours still need eyes.
- **A2 magnitude, D1 save/load, B3 economy-UI tabs** — not exercised this run.
- **B1 real fight** — the engagement that triggered was the OUT-OF-RANGE one (752 km gap, 500 km guns, `0 J dealt`) — a *spurious* battle, not a real exchange.

**Two bugs found → FIXED on branch `claude/4x-game-testing-strategy-19xw8q` (await merge + re-test):**
1. **FREEZE** (`[HANG]` … wedged as the combat interrupt auto-opened Battle Report) → `BattleReportWindow` ImGui `Begin/End` imbalance fixed (`f2d16c8`) + the `[HANG]` line now names the wedged stage. **The B5 "`ui(windows)` 1219ms" finding is almost certainly this same window's ImGui-recovery churn — re-measure perf after the fix.**
2. **Combat started out of weapon range** (the spurious 0-damage battle above) → weapon-range trigger (`e86a8e1`): a fight now auto-starts only within actual weapon range + one side Weapons Free.

*Re-test priority next session (on the merged fixes): the effects-over-time re-dump, the Society tab render, a real in-weapon-range fight, and a perf re-measure.*

### ⭐ The always-first test, every branch

#### T0 — New Game boots and the clock runs — 🟢 PASSED 2026-07-02 (main @ Society-tab merge; RE-RUN after the freeze + weapon-range fixes merge)
- **What:** launch the client, start a New Game, let the clock advance a few months, close cleanly.
- **Why:** every branch adds DataBlobs/processors/UI; the #1 risk is a startup crash or a sim that throws — and CI cannot open a window. This is the cheapest, highest-value check and gates *everything* else.
- **Method (most efficient):** pull branch → `launch.bat` → New Game → press play, wait → close → read `console_output.txt` (and `game_logs/` pages).
- **What right looks like:** window opens, a colony exists, the clock advances, no `[RenderError]`/`[InputError]`/`[FATAL]`/`[HANG]` lines, no unhandled exception; closing is clean.
- **Most likely failure:** a new auto-discovered `IHotloopProcessor` with a non-trivial constructor crashes startup (`Activator.CreateInstance` NRE, GameEngine gotcha #1); or a new blob/JSON breaks the New-Game build (gotcha #10).
- **Mitigation in place:** processor constructors kept trivial; `BaseModIntegrityTests` + `NewGameStartSmokeTests` cover the JSON/blob path in CI; the SafeRender/SafeEvent/FATAL/HANG nets capture+log faults instead of silent death.
- **Unblocks:** confirms the branch is *runtime-safe* — the precondition for testing anything in it. If this fails, send `console_output.txt`; it's the first fix.

### A. M-ECON branch (`claude/space-economy-morale`) — current

#### A1 — Society readout (the instrument panel) — 🟢 PASSED 2026-07-02 (Dump Society; the player-facing Society TAB render still PENDING)
- **What:** DevTools → **Dump Society (log)** prints each colony's pop · morale (+factor breakdown) · workforce/talent · tax→income, plus the player government.
- **Why:** the gauge for every later M-ECON test (a greppable log snapshot).
- **PLAYER-FACING READOUT NOW EXISTS (2026-07-02):** the same numbers now render in a colour-banded **Society tab on ColonyManagementWindow** (toolbar → Colony Management → Society), visible in NORMAL play (not SM). So there are two gauges: the DevTools log dump (diagnostic, greppable) and the in-game Society tab (player-facing, at-a-glance). Verify the tab renders live + the colours read right. The **diplomacy** ledger now has its own player-facing panel too — the **`DiplomacyWindow`** (toolbar button, 2026-07-02): stance / score / treaties per met faction. So both society AND relations are visible in normal play.
- **Method:** SM mode → Dev Tools → "Dump Society (log)" → close → read `console_output.txt` for `[DevTools] SOCIETY DUMP`.
- **What right looks like:** on the Earth start, **neutral** numbers — morale ~50 (factors all ~0), tax 0% → 0/mo, workforce ~half of pop, "no government set". Neutral is correct; it proves the gauge reads real state.
- **Most likely failure:** a `TryGetDataBlob` miss prints fewer fields than expected (a colony built by an older path lacks a blob) — degrades gracefully, not a crash.
- **Mitigation:** `SocietyReadout` is CI-tested (`FactionEconomyTests.SocietyReadout_DumpsColonyState`); every blob read is guarded; the button is a thin caller (logic in the tested engine helper).
- **Unblocks:** makes A2/A3 and all morale/economy/government calibration observable.

#### A2 — Morale valve moves population — 🟡 PENDING
- **What:** a hostile-world colony reads lower morale and loses population to emigration over time.
- **Why:** proves M1 (the valve) actually does its job live, not just in the unit test.
- **Method:** DevTools → Create Colony on Venus/Mercury (hostile) → Dump Society (lower morale than Earth) → advance several months → Dump Society again (population dropped).
- **What right looks like:** Venus colony morale clearly below 50 (a negative `conditions` factor); after time, its pop is lower than it started (negative migration). Earth stays neutral/stable.
- **Most likely failure:** miscalibration — morale too harsh (colony collapses instantly) or too soft (no visible change). A *feel* problem, not a crash.
- **Mitigation:** all morale weights are named coefficients (easy to tune); the unit tests fix the *direction*, this checks the *magnitude*.
- **Unblocks:** the M2-data **base-low calibration** (#25) — you can't tune the base without seeing morale move.
- **M2-data housing half DONE (2026-07-01, #25):** infrastructure + space-habitat templates now declare a "Housing Comfort" property bound to `HousingAtbDB` (a capped positive morale bonus), so the M2 comfort machinery has real numbers on the starting colony. Additive-only (no new resource cost). Gauge `MoraleTests.StartingColony_Infrastructure_ProvidesHousingComfort`. The **employment half stays parked** — jobs measured against the billions-strong homeworld workforce would trip the unemployment penalty on New Game, so those numbers need the local base-low pass, not a blind commit.

#### A3 — Hazards live (carried into this branch) — 🟡 PENDING
- See `docs/CLIENT-TEST-CHECKLIST.md` "Hazards" section: corona discovery fires, the research→armor loop pays off, normal orbit = zero hazard damage, new environments appear in generated systems. Engine side is CI-green (diorama); this is the live feel.

### B. Carried / prior-branch runtime items (in the build, not yet confirmed live)

#### B1 — Space combat starts on "play" — 🟢 TRIGGER CONFIRMED 2026-07-02 (auto-trigger + interrupt fire on play); a real IN-RANGE fight still PENDING (the one seen was out-of-weapon-range → 0 damage → the weapon-range fix)
- **What:** two hostile fleets in range auto-engage when the player presses play (not only via the manual "Tick Combat" button).
- **Why:** the auto-resolve engine is CI-green, but the *trigger scheduling on a real clock advance* is unconfirmed (the test harness doesn't auto-fire the battle-trigger hotloop).
- **Method:** DevTools → Spawn Combat Scenario (or Spawn Hostile Fleet at your fleet's body) → press play → watch the Fleet → Combat tab + `[Combat]`/`[FleetCombat]` log lines.
- **What right looks like:** the Status section comes alive (salvo counter climbs, ships lost, damage pool); a `[Combat]` engagement line appears without clicking Tick Combat.
- **Most likely failure:** play advances time but no battle starts → the trigger isn't scheduled on the master loop (scheduling bug, not combat math).
- **Mitigation:** the **Tick Combat** button drives it manually (isolates trigger-scheduling from combat-math); `[ENGINE]` heartbeat logs battle-trigger pass count so a dead trigger is visible.
- **Unblocks:** declaring space combat (MVP must-have B) live-done; it's the template ground combat mirrors.

#### B2 — Teleport-to-Sun movement defect — 🟢 PASSED 2026-07-02 (zero `⚠ TELEPORT` all session; keep the heartbeat detector as the standing sensor)
- **What:** ships sometimes jump to the system origin (the Sun) — an engine-state collapse, or a render artifact masking one.
- **Why:** it corrupts movement/combat and blanks the map; a Stage-4 live-drive blocker.
- **Method:** play normally with fleets moving/warping; the heartbeat auto-flags it within ~3 s — grep `game_logs/` for `⚠ TELEPORT`.
- **What right looks like:** no `⚠ TELEPORT` lines; ships keep healthy Sun-distances (Earth ≈ 150 Gm), warps reparent to root but hold true position.
- **Most likely failure:** a single time-step edge case (warp + orbit + a ship destroyed) leaves `Parent` null while `RelativePosition` is a small offset → `AbsolutePosition` collapses to origin.
- **Mitigation:** the heartbeat teleport detector (warp-aware, auto-classifies AT-SUN vs ORPHANED); per-item `SafeDraw` so one bad icon can't blank the map; `PruneDeadEntities` drops ghost labels immediately.
- **Unblocks:** trusting the system map during live combat/movement testing.

#### B3 — Colony economy UI works live — 🟡 PENDING (VERIFY, not build)
- **What:** `ColonyManagementWindow` tabs (Summary/Production/Construction/Mining) display and the queue-a-job lever works.
- **Why:** the docs were stale (claimed broken); the UI exists in code — only a live run settles it. MVP Stage-0 item.
- **Method:** New Game → open a colony → click each tab → queue a refine/build job → advance time → confirm it produced.
- **What right looks like:** tabs render real numbers; a queued job consumes minerals and installs/produces over time.
- **Most likely failure:** a hard-indexed faction dictionary throws for some data (gotchas #10/#11 class), or a tab renders empty.
- **Mitigation:** the defensive cargo-lookup fixes (#11); SafeRender names a faulting window in the log.
- **Unblocks:** MVP Stage-0 "economy is real and visible" → ground-unit build path.

#### B4 — Fleet UX + detection/fog live — 🟡 PENDING
- See `docs/CLIENT-TEST-CHECKLIST.md` "Fleet UX" + the client `CLAUDE.md` detection sections: left-click selects a fleet immediately; fog hides undetected foreign units; EMCON Silent shrinks the amber ring live. Engine green; UI feel unverified.

#### B5 — Performance / the map-breakdown number — 🟡 PENDING (DATA NEEDED)
- **What:** the `⏱ map breakdown` per-icon-list timing on a busy frame.
- **Why:** to find what eats frame time (suspected: per-frame orbit re-draw, 180 `RenderLine` calls/orbit) and set the real "how many entities can the lemon PC handle" budget.
- **Method:** run a dense system / 35-ship combat; when a frame is slow, grab the `⏱ map breakdown ms — orbits …` line from `console_output.txt`/`game_logs/` and send it.
- **What right looks like:** a readable breakdown naming the heaviest list; ideally interactive frame-times.
- **Most likely failure:** orbits dominate (transform+draw 180 segments/body/frame) → the targeted fix is batching `RenderLines` + LOD.
- **Mitigation:** the staged + per-category perf gauges already added; the extreme-zoom orbit cull + `[HANG]` watchdog already shipped.
- **Unblocks:** the targeted render-perf fix and the entity-count budget for "fill the system."

### C. Future tests (designed in advance; write the gauge when the feature lands)

> For each, the test is **designed now** so the slice ships *with* its gauge (the no-untested-system rule). ⚫ NOT-YET until built.

#### C1 — M3-2b crew enforcement (#27) — 🔵 WIRED (CI); FEEL is a PC-test
- **DONE (2026-07-01):** `ManpowerTools` gates SHIP construction on available bulk manpower at three sites — the gate in `IndustryTools.ConstructStuff` (blocks before resources are spent, government `CrewPolicy` decides Block vs conscript), the commit + `ShipInfoDB.CrewSourceColonyId` provenance stamp in `ShipDesign.OnConstructionComplete`, and the release in `ShipFactory.DestroyShip`. The open `CrewSourceColonyId` design call landed (an int on `ShipInfoDB`, set at build, read at destroy). **Start-safe:** INERT on a pool-less host (a station), start fleet bypasses the queue (provenance -1 → destroy no-op), billions of start pop. Gauge `ManpowerTests` (CrewPolicy end-to-end + inert-without-pool).
- **PC-test (the FEEL):** drain a real colony's pool (or build a large fleet) → confirm a ship build **blocks** under the default (consent) regime and Dump Society shows the pool drawn down; flip Authority to High → the same build **conscripts** (understaffed). Watch a destroyed ship return its crew.
- **LEVER NOW EXISTS (2026-07-02):** DevTools → **Society levers → Drain manpower pool** commits all available bulk on the picked colony (via the already-public `ColonyManpowerDB.CommitBulk`) so you don't need billions-scale play to reach the gate — drain, then queue a crewed ship build and watch it block (consent) / conscript (Authority-High, via the Government test-regime lever).
- **⚠ USE THE CHEAP SHIP (found live 2026-07-02):** queue the **Picket Test Corvette**, NOT the Aegis Test Warship, for this test. The gate fires on ANY ship build, and the Aegis is the deliberately-beefy "strong side" combat ship (4 lasers + 2 reactors + 4 engines + 4 batteries + 6 armor + warp) → **very mineral-hungry**, so the Aegis makes you fight the mineral economy instead of the crew gate (the developer had to shovel in Fissionables). You don't even need the build to *finish* — the gate blocks it before resources are spent, and Dump Society shows the pool drawn down. *(Aegis mineral cost = a design-pass tuning note: it's a test ship, so consider whether a test ship should be that expensive.)*
- **Parked (needs local calibration):** the harsher **casualties-shrink-population** sting on destroy (release-to-pool only for now), and the officer/scientist **talent** draw (academy/CommanderFactory) — its own follow-up slice.
- **Most likely failure:** the build gate blocks the New-Game start fleet → **mitigated** (start fleet bypasses the construction queue); crew-provenance loss bleeds the wrong colony → **mitigated** (`CrewSourceColonyId` remembers the source pool).
- **Unblocks:** "people are finite" actually biting; the army/unit consumer.

#### C2 — M5b energy/food wiring (#29) — 🔵 WIRED (inert, CI); calibration is a PC-test
- **DONE (2026-07-01):** `ColonySustenanceDB` + `SustenanceProcessor` compute power/food shortage → morale (`MoraleInputs`) + a starvation death term. Built **neutral-when-absent** (per-capita demand defaults to 0 → 0 shortage), so New Game is unchanged. Gauge `SustenanceTests`.
- **PC-test (the calibration):** set a colony's `PerCapitaFoodDemand`/`PerCapitaPowerDemand` > 0 and give it supply (a power reactor; a food good — **not yet defined**, so food supply reads 0). Confirm Dump Society shows a `power`/`food` morale factor and, at severe shortage, a population drop. **Watch for the default-deficit class of bug** — with demand set but no supply modeled, a colony reads total shortage and starves; that's why the coefficients ship at 0 and the food-supply good is a deliberate local follow-up.
- **LEVER NOW EXISTS (2026-07-02):** DevTools → **Society levers → Apply sustenance demand** sets the two per-capita demands on the picked colony (via the new public `ColonySustenanceDB.SetDemand`), so this is reachable without a code edit. Set demand > 0 → advance time → Dump Society for the `power`/`food` factor.
- **⚠ TEST-METHOD GOTCHA (found live 2026-07-02):** demand = **pop × per-capita**, so a colony with **0 population reads 0% shortage no matter what you set** — and the Age→Late frontier colonies (Venus/Mercury/…) DEPOPULATE to 0 (harsh conditions → morale 0 → full emigration), which is why "Venus stayed 0%". **Apply the lever to a POPULATED colony — Earth HQ (~10 B pop) is the reliable target.** Also the shortage only recomputes on the **monthly** `SustenanceProcessor` tick, so advance ≥1 game-month after setting demand before dumping.
- *(original placeholder below — superseded)*
- **What right looks like:** a power/food deficit shows a non-zero `power`/`food` factor in Dump Society and (severe) a death term; ship energy unaffected.
- **Method:** Dump Society on a colony with consumption > supply; assert the morale factor + population drop.
- **Most likely failure:** attaching `EnergyGenAbilityDB` to colonies disturbs the entity-agnostic energy processors, or a default deficit tanks every colony (the tax-default class of bug) → **mitigate** with neutral-when-absent defaults + the M1 starting-colony assertion as a guard.
- **Unblocks:** the full morale input set live.

#### C3 — Government wiring (#30) — 🔵 WIRED (CI); FEEL is a PC-test
- **DONE (2026-07-01):** `GovernmentDB` is attached to every faction (default all-Mid) and its dials are read live — `MoraleWeight`→migration, `ResearchMultiplier`→research, `TaxCeiling`→capped tax income+morale, and (2026-07-01, #27) `CrewPolicy`→the ship-construction crew gate. Neutral at Mid (New Game unchanged); gauges `GovernmentWiringTests` + `GovernmentTests` + `ManpowerTests`. Only-deferred wire: `MilitaryBuildMultiplier` (needs military-item tagging).
- **PC-test (the FEEL, not the wiring):** the **DevTools "Government (test regimes)" lever now exists (2026-07-02)** — DevTools → pick Totalitarian War-State or Liberal Democracy → Dump Society shows the regime name → advance time and watch the dials bite (over-tax past the ceiling is capped; open/closed research speed changes; morale pull scales; a dictatorship conscripts crew for a build the consent regime would block). The reset button (Federal Republic/Mid) restores neutral.
- *(original placeholder below — superseded by the above)*
- **What right looks like:** Dump Society shows a real government name; the Authority dial visibly flips the crew rule and tax ceiling; morale weight/research/build multipliers apply.
- **Method:** set a faction's dials → Dump Society (name + effects) → exercise a lever (try to over-tax past the ceiling; build with no crew under a dictatorship).
- **Most likely failure:** the GlobalManager-not-iterated trap if any lever is read by a faction-level processor → **mitigated** (design says read per-colony, pass the regime down).
- **Unblocks:** the politics layer (regime change, popular demands).

#### C4 — Stations reachable + economic (#17/#18) — ⚫ NOT-YET
- **What right looks like:** a station is buildable/placeable from the UI and, carrying the shared blobs, shows up in Dump Society with morale/manpower/economy like a colony; mining/research run on it.
- **Method:** spawn/build a station → Dump Society → advance time → confirm it mines/researches/grows.
- **Most likely failure:** `PopulationProcessor` (keyed on `ColonyInfoDB`) skips a `StationInfoDB` host → station has no morale/population (the known wiring gap).
- **Unblocks:** the BSG-nomad pillar; the space-economy alternative to planets.

#### C5 — Ground combat loop (MVP M2) — ⚫ NOT-YET
- **What right looks like:** build a ground unit → transport/drop on a cleared planet → ground battle attrites to a winner → colony owner flips. Mirrors the space-combat gauges.
- **Method:** engine test per link (mirror the auto-resolve fixtures) + the whole-loop gauge once stitched; live drive from the (to-build) `GroundCombatWindow`.
- **Most likely failure:** the complex `DamageProcessor` (stubbed) is a prerequisite; ground combat has no tests yet (don't compound the no-combat-tests pattern).
- **Unblocks:** **v1 ships** (`docs/MVP.md` §1).

#### C6 — Diplomacy behaves (IFF + first-contact + commerce wiring, #32/#33) — 🔵 IFF + FIRST-CONTACT + LOGISTICS-ACCESS DONE (CI); trade-market gate ⚫ NOT-YET
- **What right looks like:** the relationship score actually *decides things* — a War/Hostile stance flips IFF so combat engages; a TradeAgreement/LogisticsAccess flag gates inter-faction commerce/supply; first contact creates the relationship row and fires an event.
- **DONE (2026-07-01): the IFF half.** Combat hostility (`CombatEngagement.AreHostile`) now reads `DiplomacyDB` — a MUTUAL Friendly/Allied stance suppresses the fight; the v1 "different faction = hostile" default is preserved for unmet strangers (so all existing combat stays green). Gauge **`DiplomacyIffTests`** (CI-green): unmet→hostile, mutual-peace→suppressed, one-sided-peace→still hostile, same-faction→never hostile.
- **DONE (2026-07-01): first contact.** `SensorScan` → `FirstContact.OnDetection` records a MUTUAL Neutral relationship row (stamped `LastContact`) + fires a first-contact event the first time a faction detects a foreign entity (once per pair, via `HasMet`). Gauge **`DiplomacyFirstContactTests`** (CI-green): mutual contact recorded, idempotent per pair, neutral/own-faction skipped. The *live* effect (an event actually surfacing to the player on a New Game with a foreign faction) is a local-build check.
- **DONE (2026-07-01): logistics access.** `LogisticsCycle.LogisticsAccessAllowed` gates cross-faction freight on the base owner's `LogisticsAccess` grant (was hardcoded same-faction-only). Gauge **`DiplomacyLogisticsAccessTests`** (CI-green): same=open, foreign gated on a stored grant, directional (base owner grants).
- **Still NOT-YET:** the `TradeAgreement` market/price gate on cross-faction buying/selling, and `MilitaryAccess` transit (a foreign warship in your space not being an act of war). **Method:** engine integration test (two factions, set a flag → assert commerce/transit responds); live drive from a diplomacy panel.
- **Most likely failure:** the GlobalManager-not-iterated trap for any faction-level diplomatic processor → **mitigated** (keystone #34 fixed). The default-stranger risk is closed: `AtPeace` only suppresses on a STORED mutual Friendly/Allied, so a default-Neutral never accidentally disarms combat.
- **Unblocks:** the external-politics "teeth" layer (treaties-as-levers, casus belli, the reactive "Are we good?" engine) and NPC diplomatic AI.

---

### D. Political cluster — built 2026-07-01 (what to check at the PC)

> **Read this first, the honest framing (updated 2026-07-02):** the political cluster is **CI-green as engine logic**, and a **first layer now BREATHES live** — legitimacy runs on every colony (D2), relationships DRIFT each month (D3), and the readouts + the government test-lever make it all observable/steerable from DevTools. The rest is split cleanly:
> - **D0–D3 = test NOW** — legitimacy live, reactive drift live, plus the save/load and boot regressions any new blob/processor risks.
> - **D4+ = still dormant** — built + CI-green but no live effect until its wiring/UI lands. Listed so you know they exist; don't hunt for them live yet.
> - **GENUINELY DESIGN/PC-GATED (not cloud gaps — flagged so nobody "finishes" them blind):** ① **Secession** (an unquelled rebellion spawning a breakaway faction) — the trigger (`RebellionDB.WindowExpired`) is built, but the resolution is a real subsystem change: a colony carries ownership in THREE places (`FactionOwnerID` + the faction's `FactionOwnerDB.OwnedEntities` + `FactionInfoDB.Colonies`) AND its economy processors assume a valid owning faction *with a data store* (unlocked designs/tech/mineral-access), so a breakaway's viability is a design decision with runtime-only-visible failure modes. ② **NPC treaty-proposal policy** + the **fleet-near-border** observation (needs a territory model). ③ **M2 employment numbers** (base-low calibration). ④ The three mega-systems (delegation/ministers, espionage D–H, the late-game crisis). These need your design input or your runtime, not more cloud code.

#### D0 — New Game still boots + clock runs with the new politics blobs/processor — 🟡 PENDING ⭐ (this is T0 for this work)
- **What:** the standard T0 boot, specifically confirming the new `LegitimacyProcessor` (a new monthly `IHotloopProcessor`) and the new blobs on every colony (`LegitimacyDB`) + every faction (`DiplomacyDB`) don't crash startup or the sim.
- **Why:** this session added the first auto-discovered processor + per-colony/per-faction blobs of the cluster — exactly the shape that can crash boot (GameEngine gotcha #1) or the monthly tick.
- **Method:** `launch.bat` → New Game → press play → advance several months → close; read `console_output.txt`.
- **What right looks like:** boots, clock advances past a month boundary (so the monthly `LegitimacyProcessor` fires ≥ once), no exception, clean close.
- **Most likely failure:** `LegitimacyProcessor` throws in the monthly tick, or `LegitimacyDB`/`DiplomacyDB` attachment breaks the New-Game build.
- **Mitigation in place:** processor is defensive (`RecalcLegitimacy` no-ops on missing blobs, never throws); trivial ctor; `LegitimacyProcessorTests` + `GameLoopSmokeTests` cover it in CI. **Unblocks:** D1/D2.

#### D1 — Save/load round-trips the politics blobs — 🟡 PENDING
- **What:** start a game, save, load, confirm no crash and colonies/factions still hold sensible legitimacy/relationship state.
- **Why:** `LegitimacyDB` (colony/station), `DiplomacyDB` (faction), and the new `RelationshipState` treaty flags are all `[JsonProperty]` — save/load with `TypeNameHandling.Objects` is the standard risk (gotcha #7).
- **Method:** New Game → save → load → Dump Society / no exception in `console_output.txt`.
- **What right looks like:** load succeeds; legitimacy values persist; no missing-type/serialization error. **Most likely failure:** a serialization snag on a new blob. **Mitigation:** all new state is plain `[JsonProperty]` doubles/bools/dicts + a Dictionary of value objects; CI save/load tests cover the base path.
- **Unblocks:** confidence the cluster survives a played game.

#### D2 — Legitimacy tracks morale, live — 🟢 PASSED 2026-07-02 (`Mercury: morale 10 · legitimacy 10 !REBELLING (180d left)`; Earth 70/70 — legitimacy ≈ morale confirmed)
- **What:** confirm each colony's legitimacy moves with its morale over time (a content colony ~loyal, a miserable one drifts toward the < 20 collapse band, shown as `!REBELLING`).
- **Why:** the one live gameplay behavior added; proves the processor runs and reads morale in the full sim, not just the unit test.
- **Method:** **the readout is now wired** — DevTools → **Dump Society (log)** prints a `legitimacy NN.N` field per colony (in `SocietyReadout.Colony`, CI-tested), plus `!REBELLING` if it's in the collapse band. So: New Game → advance a few months → Dump Society → read `console_output.txt`; then tank a colony's morale (overcrowd / crank tax) → advance → Dump again and watch legitimacy fall.
- **What right looks like:** legitimacy ≈ morale each month; tank a colony's morale (overcrowd / high tax) and watch legitimacy fall under 20. **Most likely failure:** legitimacy stuck at 50 (processor not firing — check the monthly tick / that the colony has both blobs). **Mitigation:** `LegitimacyProcessorTests` proves the recompute; A1's Dump Society is the natural home for the readout.
- **Unblocks:** the rebellion trigger (#38) — a visible collapse band is the cue a province is about to rebel.

#### D3 — Reactive diplomacy drift (LIVE, 2026-07-02) — 🔵 WIRED (CI); FEEL is a PC-test
- **DONE:** `NPCDecisionProcessor.RunDiplomaticDrift` (fires monthly on every faction since the GlobalManager keystone) nudges each MET relationship by the locked `RelationDelta` values — a **militarist neighbour** cools relations, a **standing treaty** warms them — so relationships now MOVE on their own and cross the stance thresholds combat-IFF/logistics already read. `SocietyReadout.Diplomacy` prints the ledger (stance/score/treaties). Gauges `DiplomacyDriftTests` + `SocietyReadoutTests`.
- **PC-test (the FEEL):** needs a 2nd faction that's met yours (DevTools "Spawn Hostile Fleet" → first contact, or Age→Mid/Late), then set that faction militarist → advance several months → **Dump Society** and watch your view of them cool toward Hostile; sign no treaty and it keeps sliding. Cadence/magnitude are the calibration knobs.
- **LEVER NOW EXISTS (2026-07-02):** the Government test-regime buttons set only the PLAYER's regime, so a NEW lever was added — DevTools → **Diplomacy levers → "Their militarism → High/Low"** sets the *picked* (rival) faction's `GovernmentDB.Militarism`, which is exactly the input `RunDiplomaticDrift` reads. Pick the rival, set High, advance months, Dump Society.
- **Most likely failure:** drift too fast/slow (feel) — the direction is gauge-locked, the monthly step is the knob. Requires a met faction to show anything (single-faction start = inert, by design).
- **Design/PC-gated, NOT built (flagged so it's not mistaken for a cloud gap):** NPC **treaty-proposal** policy, **fleet-near-border** reactions (need a territory model), the overture→**commitment** executor. These are decisions for the developer to shape, not plumbing.

#### D4+ — DORMANT until wired (do NOT test live yet; CI-green, no runtime effect)
These are built + CI-verified but have **no live effect** until their wiring/UI slice lands — each needs the noted hook before it's exercisable at the PC:
- **IFF suppression (allies/pacts hold fire)** — the engine loop is now CLOSED (a signed non-aggression/defensive pact OR a mutual Friendly/Allied stance stops the fight; gauge `DiplomacyIffTests.SignedPact_StopsTheFight`). **NOW DRIVABLE (2026-07-02):** DevTools → **Diplomacy levers** — Warm relations to a high score, then **Sign Defensive Pact**, and the two factions should stop engaging. (Promoted toward a D-numbered live test once run.)
- **First-contact event** — fires when your sensors first detect a foreign faction's ship. Requires a foreign faction present + detected. The `CombatSandbox`/DevTools "Spawn Hostile Fleet" is the way to trigger it once a first-contact *notification* is surfaced (event is published; no UI panel reads it yet).
- **Treaty proposals + declare war** — the ENGINE acts exist and their consequences are wired: `Treaties.Propose` (→ IFF/commerce), `Diplomacy.DeclareWar`/`MakePeace` (→ latches war → **legitimacy shifts by militarism**, gauge `LegitimacyProcessorTests.War_TaxesLegitimacy_ByMilitarism`). **NOW DRIVABLE (2026-07-02):** DevTools → **Diplomacy levers** has Declare War / Make Peace / Sign-treaty buttons — declare war on a met rival, advance a month, Dump Society and watch D2's legitimacy move by your militarism.
- **Reactive "Are we good?" engine** — **first live wiring landed (2026-07-02, see D3 below):** `NPCDecisionProcessor.RunDiplomaticDrift` now drifts relationships each month from state (militarist neighbour cools, standing treaty warms), and `SocietyReadout.Diplomacy` prints the ledger. **Still design/PC-gated (NOT built):** the fleet-position / border-proximity observations (need a territory model), NPC treaty-**proposal** policy, and the overture→commitment executor. Those are design decisions, not cloud gaps.
- **Commerce (logistics access)** — cross-faction freight opens only if a base owner grants `LogisticsAccess`; nothing grants it yet, so behavior == old same-faction-only. Needs the treaty UI to set the flag.
- **Exchange catalog** — data only; needs the commitment model (execute an exchange) + a trade UI.
- **When any of these gets its wiring slice, MOVE it up to a D-numbered live test** with the seven fields.

---

## The process (how this doc stays true)

1. **Build something testable → add its row here** (CI row if automated; a Layer-3 entry with the seven fields if it needs a run). Designing the test *before/with* the build is the no-untested-system rule.
2. **Test it → update status + record the reading** (paste the actual gauge value when it passes, per the Visibility Gate: "here is the gauge reading when it passed").
3. **Branch lifecycle:** a branch isn't "done" until its Layer-3 rows are 🟢 or consciously deferred (note why). Per-branch `CLIENT-TEST-CHECKLIST.md` is the working scratchpad; this tracker is the durable index across branches.
4. **A failure is data, not a setback** — record the failure mode under "most likely failure" so the next branch inherits the lesson.

### Staged game-state generator (task #39) — 🔵 engine BUILT (2026-07-02); DevTools button = next slice
- **DONE:** `GameStageFactory.AgeTo(game, playerFaction, GameStage.{Early|Mid|Late})` (engine, CI-tested) layers a New-Game start into a real galaxy-at-a-stage: Early = the player goes multi-world (several colonies); Mid = two rival EMPIRES each with their OWN species + colonies (one Friendly + a trade treaty, one hostile); Late = the player at ~5 colonies, a third rival (an ally via defensive pact), an active war with the hostile rival, and a frontier colony in open rebellion — populations scaled up per stage. GENERATED (rides current factories → never rots on a new blob), cumulative + convergent (re-aging grows a rival 1→2 colonies, neutral→allied, no duplicates). Creating a rival's colony from birth is a clean ColonyFactory call — NOT the secession ownership blast radius (that's transferring an existing colony). Gauge `GameStageTests`.
- **Next slice:** a DevTools "Age the galaxy → Early/Mid/Late" button (thin wrapper) so the developer loads a rich state instantly at the PC. Then: save-file fixtures once the DataBlob schema stabilises (post-MVP), with a load-old-save regression test.
- **PC-test:** 🟢 **PASSED 2026-07-02** — DevTools → Age → Late → Dump Society showed exactly this live: `5 player colonies`, a rebelling frontier (`Mercury … !REBELLING (180d left)`), and the ledger with `Vega Friendly+Trade`, `Crimson War`, `Terran Friendly+treaty`.

*Where we're at (2026-07-02): the M-ECON + politics + staged-galaxy layer is **confirmed live** — boot, Society readout, legitimacy/rebellion, the full diplomacy ledger, and every new DevTools lever fired in one session (see "Live-run results" at the top of Layer 3). Two bugs the run surfaced — a Battle-Report freeze and out-of-weapon-range combat — are FIXED on `claude/4x-game-testing-strategy-19xw8q`, awaiting merge. **Next session (on the merged fixes):** (1) re-run T0 + read `game_logs/` for a clean, hang-free session; (2) the effects-over-time re-dump (apply a lever → advance months → Dump again to see it BITE); (3) the Society-tab render; (4) a real in-weapon-range fight; (5) a perf re-measure (the 1219ms `ui(windows)` frame should drop with the Battle-Report fix).*
