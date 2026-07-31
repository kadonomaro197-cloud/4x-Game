# Docs-vs-Code Audit — 2026-07-27 (OPERATION GROUND TRUTH)

**Status: 🏗 IN PROGRESS — Phase A partially complete.** This run was launched from
`OPERATION-GROUND-TRUTH-PROMPT.md` (repo root) on branch `claude/operation-ground-truth-prompt-l9729h`
(started from `claude/faction-design-audit-bb3tqz`). It follows `docs/DOCS-AUDIT-PROCESS.md`.

**Why it stopped where it did — read this first.** A 19-agent evidence fan-out was launched and **every
agent died on the account's usage limit** after ~2M tokens (`"You've hit your session limit · resets 9pm
UTC"`). That is the exact failure the handoff prompt's §7 warned about. Per that instruction this file is
the checkpoint. **Nothing is lost:** all 19 assignment briefs plus the shared agent briefing are written to
disk and a resumed session can re-fire them in one call (see §5 Resume).

Everything recorded below in §2–§4 was verified **first-hand by the session's own reads**, not by an
agent, and every line carries a `file:line`. Where a claim could not be settled without running code, it
says so instead of guessing.

---

## 1. Session setup (done, and green)

| Step | Result |
|---|---|
| Branch | `claude/operation-ground-truth-prompt-l9729h`, reset to `origin/claude/faction-design-audit-bb3tqz` |
| `main` reconciliation | audit branch is **55 ahead / 0 behind** `main` — `main` is fully contained, **no merge needed** (`git merge-base --is-ancestor origin/main HEAD` passes) |
| Inherited CI — `b218acf` (doctrine D1b) | ✅ **success**, all jobs |
| Inherited CI — `255bc52` (docs) | ✅ **success**, all 7 jobs (6 test shards + `build-client`) |
| Tip `390deab` | 6/7 green; the `rest` shard was still running at time of writing |

### ⚠ CORRECTION to a handoff-prompt assumption: CI is ~33 minutes, not ~13

Measured on run `30290497412` (`255bc52`): `campaign-clock` 2 min · `selfsufficiency` 4 min ·
`economy-readout` 8 min · `economy` 11 min · `stations` 13 min · **`rest` 33 min** · `build-client` 0.5 min.
The **`rest` shard is the critical path at ~33 min**, and `ci.yml`'s complement filter
(`.github/workflows/ci.yml:68-69`) means **every new fixture lands in `rest` by default** — so each new
ground gauge makes the slowest shard slower. Plan slice cadence against ~33 min, and treat "does this new
fixture need its own shard" as a real question per slice.

---

## 2. VERIFIED findings — the ground battle is invisible *and the clock already stops for it*

This is the single most important thing this run established, and it makes the sequencing argument
self-proving.

| # | Finding | Evidence |
|---|---|---|
| F1 | **A new ground battle HALTS the player's clock.** | `GroundCombat/GroundForcesProcessor.cs:329-330` — `if (anyFightThisTick && !forces.WasInBattle && InterruptTimeOnNewBattle) body.Manager?.Game?.TimePulse?.RequestCombatHalt();` |
| F2 | **…and the entire 1,078-line file emits NOTHING.** No `Console.WriteLine`, no `Publish`, no event, no battle record anywhere in `GroundForcesProcessor.cs`. | grep of the whole file for `Console.WriteLine\|Publish\|Event` returns **zero** hits |
| F3 | **The dead are then silently deleted.** | `GroundForcesProcessor.cs:334` — `forces.Units.RemoveAll(u => u.Health <= 0);` |
| F4 | **Capture flips the REGION, not the hex.** | `GroundForcesProcessor.cs:307-310` — holders are collected per region and `regionsDB.Regions[kv.Key]` owner is set |

**So the interrupt does not merely stay silent — it misleads.** The clock stops, the UI opens the *space*
combat report, and that report contains nothing about the ground fight, because nothing ever recorded one.
Ruling #24 ("the ground battle readout is a LOG, first") is therefore not a nice-to-have: it is the missing
half of a halt that is **already built and already firing**.

### F5 — the log slice is CHEAP, because space already has the exact pattern to copy

| Piece | Where | Note |
|---|---|---|
| Narration, flag-gated | `Combat/CombatEngagement.cs:452-455` — `CombatLog(msg)` → `if (NarrateToLog) Console.WriteLine("[Combat] " + msg)` | the static is `CombatEngagement.cs:352`; the **client** turns it on at `Pulsar4X.Client/PulsarMainWindow.cs:77` |
| Structured, persistent trail | `Combat/BattleLog.cs` + `RecordBattleEvent` (`CombatEngagement.cs:458+`) | capture is **UNCONDITIONAL** (not gated on the log flag), thread-safe, ring-capped `MaxEvents = 250` (`BattleLog.cs:71`) |

**Two honest cautions for the build (design forks, not blockers):**
1. `BattleEvent`'s fields are **ship-shaped** — `FleetId`, `FleetName`, `ShipsLost`, `ShipsLeft`
   (`BattleLog.cs:29-41`). Ground reuse means either living with ship names for battalions, or adding a
   domain discriminator. **Pick deliberately; don't let it happen by accident.**
2. `BattleLog` is explicitly **runtime-only, not part of save/load** (`BattleLog.cs:66-68`). Ruling #22
   wants a damage ledger *totalled at battle end*; if the after-action report must survive a save, that is
   a save-safety decision (and `TypeNameHandling` applies), not a free ride.
3. `MaxEvents = 250` is sized for fleets. A ground fight has many more participants — a ground battle could
   evict the space history. **FLAGGED as a balance/sizing number.**

---

## 3. VERIFIED findings — reachability: the premise was WRONG, and that is good news

### F6 — the ground layer is NOT DevTools-only. There is already a main-menu button.

The audit and the handoff both frame ground content as reachable "only via DevTools." **That is refuted.**

| Evidence | What it shows |
|---|---|
| `Pulsar4X.Client/Interface/Menus/MainMenuItems.cs:51-53` | `if (ImGui.Button("DevTest", _buttonSize)) NewGameMenu.DevTestGame();` — a plain, **ungated** main-menu button |
| `NewGameMenu.cs:895-911` (doc comment) + `DevTestGame()` | stands up the player (UEF, everything unlocked, nothing pre-built) **plus two developed NPC factions** — UMF (inner-system war economy, **at war with Earth**) and Kithrin — from JSON, via the same `DevTestStartFactory.CreateDevTest` orchestrator CI drives |
| `NewGameMenu.cs:979-986` | that path flips ON `EnableGroundTacticalAI`, `GroundAssembly.AutoFormUp`, `EnableGroundRoleManeuver`, `EnableMiniHexCombat`, `EnableInitialEngagementSpread` |

**Why this matters for ruling #27b.** The developer ruled that a stock New Game must **not** raise a
garrison or place an enemy. The audit's P1 wanted a takeable target from the front door. Those look
contradictory — but they are not, because **the "deliberate scenario start" that resolves them already
exists and is already on the menu.** It is only *named* like a debug toy. The recommended answer is
therefore **promote and rename the existing DevTest start into a first-class Scenario/Skirmish game mode**,
not build new machinery. → this is an open question for the developer (§4).

### F7 — the five behaviour flags: the real defect is LOAD, not New Game

| Flag | Declaration |
|---|---|
| `EnableGroundTacticalAI` | `GroundForcesProcessor.cs:62` |
| `EnableGroundRoleManeuver` | `GroundForcesProcessor.cs:72` |
| `EnableMiniHexCombat` | `GroundForcesProcessor.cs:85` |
| `EnableInitialEngagementSpread` | `GroundForcesProcessor.cs:100` |
| `GroundAssembly.AutoFormUp` | (5th of the set) |

All are `public static bool … = false`. They are set to `true` in **two** places in `NewGameMenu.cs` — the
**normal menu New Game path** at `:562-581` *and* the DevTest path at `:979-986`.

**Refinement of ruling #27a's premise:** it is *not* true that only DevTest turns them on — a normal menu
New Game does too. The defect is exactly and only that **loading a save never sets them**, so the same save
plays differently depending on whether a New Game happened earlier in the same process. That is what
moving them into the save fixes.

### F8 — the ground-design reopen wall is real, and the code says so out loud

| Evidence | What it shows |
|---|---|
| `Pulsar4X.Client/Interface/Windows/ShipDesignWindow.cs:166` | the picker loads `_factionInfoDB.ShipDesigns.Values.Where(d => !d.IsObsolete)` — **ShipDesigns only** |
| `ShipDesignWindow.cs:223` (comment) | *"A loaded design is always a ShipDesign (ground designs live in IndustryDesigns, never selected here)"* |
| `ShipDesignWindow.cs:592` | a saved ground design goes to `GroundUnitAssembly.RegisterAssembledDesign(...)` → `IndustryDesigns` |

So a player can save a ground design and **can never reopen it**. The code comment documents the wall as
intended behaviour, which is why no test caught it.

---

## 4. The A2 seed list — verdicts reached so far

Three-state vocabulary. "Fix" = the Phase C action.

| Seed item | Verdict | Evidence / fix |
|---|---|---|
| `docs/aurora/GROUND-COMBAT.md:6` claims Pulsar has **no ground combat at all** ("no ground unit entity, no formation concept, no ground combat processor, no invasion order, no UI") | **REFUTED — all five wrong** | A 56-file subsystem exists: `GroundUnitEntity.cs`, formations in `GroundForcesDB.cs`, `GroundForcesProcessor.cs`, `LandTroopsOrder.cs`, and client Force Management / `PlanetViewWindow`. **Aggravating factor: root `CLAUDE.md` sends every ground designer to this doc.** Fix = rewrite the line to "reference spec; Pulsar's as-built ground layer is `GameEngine/GroundCombat/CLAUDE.md`." |
| `LoadTroopsOrder.cs:17` / `LandTroopsOrder.cs:17` — *"nothing issues this yet (a client button is the UI slice)"* | **STALE (false)** | Player issues both: `FleetWindow.cs:1756` (`LoadTroopsOrder.CreateCommand`), `FleetWindow.cs:1814` (`LandTroopsOrder.CreateCommand`). AI issues land: `ConquerResolver.cs:63`. Fix = correct both comments (Phase C, comment-only). |
| `GroundForcesDB.cs:88` — `Speed_kmh` *"**Byte-identical** — nothing reads it yet"* | **STALE (false)** | Read by the resolver at `GroundForcesProcessor.cs:855`: `(unit.Speed_kmh > 0 ? unit.Speed_kmh : GroundMobility.BaseMarchSpeed_kmh) * (deltaSeconds / 3600.0)`. Also `GroundForcesTests.cs:1251` still describes it as "ADDITIVE/UNREAD by the resolver". Fix = correct both. |
| `docs/ground/EARTHFALL-CAMPAIGN-OPS.md` — "the hex is the unit of everything" | **OVERSTATED** | Combat groups **by region** and capture flips the **region** (`GroundForcesProcessor.cs:294-310`). Per-hex infra combat exists, but the hex is not the unit of *everything*. Fix = qualify to "per-hex for infrastructure; region for combat grouping and capture." |
| **Surface scale: 560 km/47 km vs 477 km/37 km** | **BOTH arithmetically self-consistent — they measure DIFFERENT THINGS.** See below. | — |
| `docs/ground/PLANETARY-GAMEPLAY-AUDIT-2026-07-24.md:151` "two open design questions" | **STALE** | Both were LOCKED the same day by `GROUND-SURFACE-MAP-DESIGN.md:424-453` (fine terrain = affinity + bends-the-fight + the "nothing is impossible, just costs" law; weather/hazards at BOTH zooms). Fix = replace with a pointer to Layer 6. |
| Same doc's **P1 "default `AutoRaiseHomeGarrison` on"** (`:158-165`) | **SUPERSEDED by ruling #27b** | The ruling forbids a default garrison. Fix = re-point P1 at the scenario-start route (§F6), not a default flip. |

### The scale contradiction, settled as far as code allows

The **load-bearing runtime numbers** are formulas, not constants:

- **Coarse (operational) pitch — per REGION, per BODY:**
  `GroundRangeTools.cs:39-44` → `HexPitchKm(region) = √(2·(region.Area_km2 / region.Hexes.Count) / √3)`.
  *This is why a hex is a different real size on Earth vs Io.* There is **no global constant.**
- **Mini pitch — exactly coarse ÷ 13:**
  `GroundMiniHex.cs:28-34` → `coarsePitchKm / (2·cityRadius + 1)`, with `CityPatchRadius = 6`
  (`CityGridFactory.cs:20`). The resolver measures with this (`GroundForcesProcessor.cs:612, 749`).

Why the docs disagree — **two different derivations, both internally valid:**

| Doc | Number | How it was derived | Is it what the code measures? |
|---|---|---|---|
| Layer 5 (`GROUND-SURFACE-MAP-DESIGN.md:301,324`) | 477 km / **37 km** | pitch ÷ 13 across | ✅ **YES — this is the load-bearing one** (and `GroundMiniHex.cs:25-26` says "~37 km under a ~477 km Earth coarse hex") |
| Layer 4 (`GROUND-SURFACE-MAP-DESIGN.md:276`) | 560 km / **47 km** | op-hex **area** ~271,000 km² ÷ 127 tiles ≈ 2,100 km² → ~47 km "across" | ❌ no — an **area-equivalent** figure no code uses |

Cross-check: 47 × 13 = 611 ≠ 560, so the 560/47 *pair* is not self-consistent under the ÷13 rule, while
477/13 = 36.7 ≈ 37 is. And the coarse difference (560 vs 477) is an assumed **Earth hex count** —
510.1 M km² ÷ 2,600 hexes → 476 km (matches Layer 5); 560 km implies ≈1,880 hexes.

**⚠ HONEST LIMIT — do not paper over this.** Which Earth hex count the generator actually produces cannot
be settled by reading (no .NET SDK in this container, and two hex models still coexist — per-region disks
and the global cylinder grid). **The correct fix is a gauge, not a chosen number:** a tiny CI readout that
prints the real `HexPitchKm` and `MiniPitchKm` for Earth / Mars / Luna, then make all three docs quote the
measurement. Recorded as a plan slice. *(Visibility Gate: build the gauge, then state the number.)*

---

## 7. THE BIG ONE — 327 dead doc pointers in the code (found, swept, closed)

**This was not on the handoff's seed list. It is the largest doc-truth defect in the repo, and it was
hiding in plain sight.** The 2026-07-13 audit moved `docs/` into subject subfolders and repointed the
`.md` files, but **never repointed the `.cs` files** — `DOCS-INDEX.md` known-debt item 3(a) even admitted
the sweep was deferred. Nobody re-checked.

The residual-grep gauge (now recorded in `DOCS-INDEX.md` item 3(a) so it can be re-run cold):

```
grep -rhoE "docs/[A-Za-z0-9_/-]+\.md" Pulsar4X/ --include=*.cs | sort -u \
  | while read p; do [ -f "$p" ] || echo "DEAD: $p"; done
```

**Reading:** 48 distinct doc paths cited from code → **25 dead (over half)**, totalling **327 pointer
sites across 249 files**. Every one was a `Design: docs/X.md` comment sending a reader — the developer, or
a future session doing exactly the pre-flight the root `CLAUDE.md` demands — to a 404.

| Class | Count | Examples → where they went |
|---|---|---|
| **Moved** by the subfolder reorg | 13 paths | `docs/ai/AI-BRAIN-BUILD-TRACKER.md` (61 sites!) → `docs/ai/…`; `docs/explore/SITE-ENGINE-DESIGN.md` (35) → `docs/explore/…`; `docs/society/DIPLOMACY-DESIGN.md` (17) → `docs/society/…` |
| **Consolidated** into a survivor | 10 paths | `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (27), `GROUND-CITY-AND-WARMAP-DESIGN.md` (13), `HEX-GROUND-AND-ORDERS-DESIGN.md` (9), `GLOBAL-HEX-GRID-DESIGN.md` (9) → `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`; `WEAPON-TAXONOMY` + `WEAPONS-AND-DODGE` (23) → `docs/combat/WEAPONS-DESIGN.md`; `AI-MEANS-ENDS-PLANNER` + `AI-OBJECTIVE-ENGINE` (23) → `docs/ai/AI-DECISION-ENGINE-DESIGN.md`; `RESOLVER-MERGE` (6) → `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §14.1`; `SPACE-STATIONS` (8) → `docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` |
| **Superseded outright** | 2 paths | `WEAPON-UNIFICATION-DESIGN.md` (8), `GROUND-UNITS-AS-ENTITIES-DESIGN.md` (9) → `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` |

**Swept 2026-07-27.** Re-grep returns **zero** dead paths. Provably **comment-only**: 327 insertions /
327 deletions, and a diff filter for changed lines that are *not* comments returns 0 — so the engine is
byte-identical by construction, not by claim. One judgement call recorded: `GroundForcesDB.cs:314`
(the order-queue comment) was pointed at `docs/ground/GROUND-ORDERS-CATALOG-DESIGN.md` rather than the map
doc, because the consolidation split that source doc's *order* half into the catalog.

**Left alone deliberately:** the ~22 dead `docs/…md` mentions inside `.md` files. Most are **provenance**
— historical names of merged-away docs, which `GROUND-SURFACE-MAP-DESIGN.md`'s header explicitly says must
stay **plain text**, because the 2026-07-13 sweep once rewrote them into the surviving doc's own path and
destroyed the history. Repointing those would repeat that damage. A future pass should convert them from
path-shaped strings to plain names, one at a time, by hand.

### Also fixed in this slice (each verified in §4)

| Fix | File |
|---|---|
| The "Pulsar has no ground combat at all" opener, replaced with a correction banner + a pointer to the as-built subsystem | `docs/aurora/GROUND-COMBAT.md` |
| "Ground units live on the `ColonyHexMapDB` tile grid" → the real `HexQ/HexR` + `GlobalQ/GlobalR` + `MiniQ/MiniR` fields; plus **`GroundCombatWindow` does not exist anywhere in the client** (a second dead reference in the same passage) | `Pulsar4X/Pulsar4X.Client/CLAUDE.md` |
| "nothing issues this yet" → names the real issuers (`FleetWindow` buttons; `ConquerResolver` for landing) | `GroundCombat/LoadTroopsOrder.cs`, `LandTroopsOrder.cs` |
| `Speed_kmh` "nothing reads it yet" → names the resolver closing-step read | `GroundCombat/GroundForcesDB.cs` |
| "ADDITIVE/UNREAD by the resolver" in a test description → corrected, noting the fixture's own assertions remain true | `Pulsar4X.Tests/GroundForcesTests.cs:1251` |
| "the hex is the unit of everything" → qualified to hex-for-infrastructure / region-for-the-fight-and-capture | `docs/ground/EARTHFALL-CAMPAIGN-OPS.md:15` |

---

## 8. Ruling #21 — the capture-transfer inventory (a DECISION AID, nothing decided)

Ruling #21 is OPEN and the developer said "still thinking," so **nothing here is a decision.** This is the
fact base so the ruling can be made with the code in view. Verified 2026-07-27 **[V2]**.

**Capture is one statement:** `colony.FactionOwnerID = owner` (`GroundForcesProcessor.cs:1073`, reached from
`TryCapturePlanet` at `:1056`).

| Candidate | What capture does today | Consequence |
|---|---|---|
| Colony ownership | **MOVES** | the only thing that changes |
| Population (`ColonyInfoDB.Population`) | **IGNORED** — rides along intact | captor inherits a full foreign-species population, no casualties, no unrest |
| Stockpiles, raw + refined (`CargoStorageDB`) | **IGNORED** — rides along | captor inherits everything |
| Component / ordnance / fighter stockpiles | **IGNORED** — ride along | captor inherits |
| Installations (`ComponentInstancesDB`) | **IGNORED** — ride along | captor inherits **every building at full health** |
| Located buildings on the war map | hex **owner MOVES** per region (`GroundBuildings.cs:187-199`); ids untouched | a captured hex stops fortifying the old defender |
| Production queue | rides along, then **STALLS** — jobs whose design the new owner lacks are marked `MissingResources` (`IndustryTools.cs:131-135`; its comment names colony capture as the reason) | captor holds a queue it cannot build, with no cleanup and no notification |
| **`FactionInfoDB.Colonies` registry** | **NOT TOUCHED — and there is no removal path anywhere** | **the live bug in §7/C1: the loser keeps defending it, the captor never sees it, and the captor can target its own world** |
| Designs (component + industry) | **NOT transferred** (faction-level) | captor cannot rebuild what it captured |
| Research / tech | **NOT transferred** | no tech-loot mechanic |
| Money / treasury | **NOT transferred** — but future tax income **does** follow the flip (`ColonyEconomyProcessor.cs:66`) | income moves, treasury doesn't |
| Ground units + formations on the body | **NOT touched** — each keeps its own owner | looks right (units aren't property), but there is no surrender, no POWs, no disband |
| Morale / legitimacy / rebellion | **IGNORED** — ride along at pre-capture values | a just-conquered world keeps the loser's legitimacy score, and the processors then silently re-target the numbers at the new owner |
| Manpower pools (`ColonyManpowerDB`) | **IGNORED** — committed/available ride along | workforce stays committed to a ship the captor can't build |
| Beachhead outposts, surface parts, fog masks, upkeep bookkeeping | **NOT touched** | |

**The one thing here that is NOT a #21 question:** the `FactionInfoDB.Colonies` registry. That is hygiene —
the registry must reflect reality regardless of what the developer decides transfers — so it is scheduled as
plan slice **S1b** and is **not** blocked on the ruling.

---

## 9. GAME-LOG FORENSICS — the 2026-07-23 session, as an incident review

Three agents read `game_logs/game_log_000.txt` + `_001.txt` + `console_output.txt` in full. This is the
evidence the orders called *"the most important"*, and it is the most valuable finding of the run: **it shows
the gauges themselves lying.**

### The session in one line

**9 minutes wall-clock** (16:26:20 → 16:35:20), **144 days game-time**, **two mouse clicks**, **zero ships**,
**one colony**, **no ground content reached at all** — and the final **30 %** of it was the developer pressing
play **seven times** at a simulation that had already died. Then the game printed **`faults=0`** and quit
cleanly.

### ⛔ G1 — THE CLOCK DIED AND EVERY GAUGE SAID IT WAS FINE (the worst finding)

A warp-arrival `Speed Result is NaN` faulted the simulation Task at 2050-05-25 06:00. Then:

| Gauge | What it reported | Why it was blind |
|---|---|---|
| `[TIME]` / the play button | "paused" | `MasterTimePulse.IsRunning` is **derived from task completion** (`MasterTimePulse.cs:49`), so a **faulted** task reads *paused*. `StartTime()` just re-throws — six more presses, nothing. |
| **SIM-STALL detector** | silent | gated on `tp.IsRunning` (`SessionLog.cs:158`) — which is false for a dead task, so it can **never** fire for this failure class |
| **`[HANG]` watchdog** | silent | watches the **UI thread**, which was healthy |
| `[RenderError]` / `[InputError]` | silent | only catch main-thread throws |
| **the ~3 s heartbeat** | kept printing | **54 consecutive beats** at the same frozen timestamp with `+0` on both counters |
| **the clean-exit summary** | **`faults=0`** | `SessionSummary()` returns `_loggedRenderErrors.Count` (`PulsarMainWindow.cs:485`) — render/input only. **Nothing counts a `[FATAL]`.** |
| `[FATAL]` | fired ×7 — **late** | arrives on GC finalization of the faulted task |

**There is no gauge for "the simulation is dead."** That is a Visibility-Gate failure of the first order: the
developer sat pressing play for 2.7 minutes while every instrument read normal. **The NaN itself is fixed at
HEAD** (both throw shapes bail to a finite value — `496a5d1`); **the blindness is not.**

### G2 — a SECOND silent clock stop, from a different cause

At 2050-03-28 07:00 the clock stopped mid-beat, on the exact beat UEF's contacts jumped 2 → 5, with **no
`[TIME]` paused line**. Cause: `SensorEvents.cs:39` publishes `NewHostileContact` → `FactionEventLog.cs:50`
calls `PauseTime()`, and the player is opted in at `EventTickerWindow.cs:47`. **It writes zero log lines**, so
a legitimate, useful auto-pause reads as an unexplained stop.

### ⛔ G3 — THE HEARTBEAT COUNTERS ARE PLACEBO GAUGES

Both counters climbed impressively — sensor scans **148,894**, battle-trigger passes **4,985,827** — and a
prior session read that as proof both engines "fire live on play." **It proves much less than that:**

- `TickCount` is incremented as the **first statement** of `Tick`, **before** the fleet query and **before**
  the "no fleets" early return — *so it climbs to millions on a completely empty galaxy.*
- `ScanCount` likewise increments at the top of `SensorScan.ProcessEntity`, **before** the faction-registry
  guard and the `SensorAbilityDB` gate.

**A climb proves only that the hotloop is scheduled.** It says nothing about fleets, hostility, range, or
whether any detection math ran. Treat both as *"the processor is being invoked"*, never as *"the system works."*

### G4 — ZERO battles formed, and the invasion that arrived and did nothing

The Martian AI selected its objective, built a lander, and warped **three warships 250.6 Gm to Earth**
— against a homeworld with **zero defending ships** — and the combat system produced **nothing**: no battle,
no bombardment, no interrupt, no alert. Only a rising contact count showed anything had happened.
Root cause is mundane and total: the player had **0 ships**, and `CombatEngagement.Tick` enrols only
`FleetDB` entities, so **no hostile pair existed**. A colony is not a combatant.

### ⛔ G5 — A LIVE BUG: the AI re-orders a fleet to a body it is already orbiting

**This is what caused the NaN.** Five identical *"sail strike fleet 292 at enemy world 4"* orders produced
**six warp departures at 0 Gm / 0.5 Gm**, and the co-located arrivals are what threw.
**`ConquerResolver.cs:172` and `:202` guard only on `!FleetIsMoving(strikeFleet)` — there is no at-target
check** (verified 2026-07-27). A fleet that has arrived is "not moving", so it is re-issued a sail order to
where it already is, every cycle. The NaN symptom is patched; **this cause is still live.**

### G6 — the AI is idle while at war

- **UMF:** **135 of 144** decision cycles returned *"None: goal met or no legal step"* — while at war, with a
  transport built and its fleet parked over an undefended enemy homeworld. **Never landed a soldier in five
  months.** None of the five recent fix commits touches this path.
- **Kithrin:** 1 survey, 28 days awaiting a geo-survey, then **108 cycles of "no legal step."** `fd37692`
  raised survey speed 1→10 and `181130a` fixed the debt source, but **the 108-cycle no-op is untouched**.

### G7 — the ground UI is invisible to the flight recorder

`PlanetViewWindow.cs` contains **zero `SessionLog` calls**. There is no window-open gauge anywhere in the
client. **So a play-test cannot even prove the surface map was opened** — which is why "did the developer
reach ground content?" had to be answered from the *absence* of every other ground gauge.

### G8 — `console_output.txt` held nothing, again

**1667 of 1667 lines are `dotnet build` compiler warnings.** Zero runtime lines, zero `SessionLog` tags. The
client's designated diagnostic channel had **no forensic value for this session** — the documented
stdout-buffering trap recurring.

### G9 — `[FleetCombat]` is a player-input echo, not a battle readout

The tag has **exactly three emitters, all client button handlers** (set doctrine / set EMCON / set posture).
It can **never** confirm a battle happened. The battle channel is `[Combat]`. Several docs list it among
combat-observation channels — corrected in `Pulsar4X.Client/CLAUDE.md`.

### ⛔ G10 — THE AI IS BLIND, AND ITS RISK APPETITE NEVER EVALUATED ANYTHING (added 2026-07-27)

**Missed in the first write-up of this section; it may be the widest-blast-radius finding of the whole run.**

**All 288 AI decision-tape lines read `vs no threat`.** That string is `ThreatAssessment.GreatestThreatTo`
returning nothing, and the log shows why in one glance:

```
[DETECT-CONTACT] 'MFS Deimos'  src=LAGGED fogLag=84297km  sig=0kW
[DETECT-CONTACT] 'MFS Ares'    src=LAGGED fogLag=50909km  sig=0kW
[DETECT-CONTACT] 'MFS Olympus' src=LAGGED fogLag=50909km  sig=0kW
[DETECT-CONTACT] 'MFS Tharsis' src=LAGGED fogLag=50909km  sig=0kW
[DETECT-CONTACT] 'Sol A G0V'   src=LAGGED fogLag=0km      sig=1397071.3kW   ← the STAR
```

**The star reads 1.4 million kW; every ship reads 0 kW.** `GreatestThreatTo` sums
`contact.SignalStrength_kW` (`Factions/ThreatAssessment.cs:39`), which resolves to
`SensorInfo.LatestDetectionQuality.SignalStrength_kW` (`Sensors/SensorContacts/SensorContact.cs:54`). A contact
only exists if it passed `SignalStrength_kW > 0.0` at scan time (`SensorScan.cs:132`) — so
`HighestDetectionQuality` *was* non-zero while **`LatestDetectionQuality` — the one every consumer reads — is
zeroed.** This is the concrete, observed form of the **"degenerate detection-quality"** landmine that
`docs/society/DIPLOMACY-DESIGN.md` already names as a **keystone prerequisite**.

**The precise reading, because the distinction decides the fix.** `CombatRisk.WouldEngage` opens with
`if (enemyStrength <= 0.0) return true;` and its own comment says *"A non-positive enemy estimate (nothing
detected / no threat) always engages"* (`Factions/CombatRisk.cs:41`). **That fallback is deliberate and
sensible.** The defect is that its **input is always zero**, so the escape hatch is the **only branch ever
taken** — which means the AI's entire risk appetite is inert in practice:

| Consumer | What it should do | What it does |
|---|---|---|
| `ThreatAssessment.GreatestThreatTo` (`:70-85`) | name the biggest rival threat | **never names one** |
| **`CombatRisk.WouldEngage`** (`:41`) | commit only if own ≥ enemy × the Risk-scaled ratio | **always returns true.** The Risk trait and the whole `CautiousRatio`↔`ParityRatio` band **never evaluate anything** — including at the commit gate `ConquerResolver.cs:168` |
| `RunTreatyPolicy` Pass 1 | propose a defensive pact against a shared threat | **can never fire** |
| `ThreatAssessment.IsRising` (`:95-98`) | trigger an alliance against a riser | **can never fire** |

**So "the AI sailed into an undefended homeworld" understates it: the AI would have sailed into a meat grinder
exactly the same way**, because it has no working notion of how strong anyone else is. Any future work on AI
caution, personality-driven aggression, or defensive pacts is decorating a value that is always zero.

**Consequence: CONFIRMED (log + code). Root cause: UNVERIFIED** — why `LatestDetectionQuality` is zeroed while
the contact persists was not traced. `SESSION_STATE.md` records a related suspicion (a 0–100 quality value
crammed into a 0–1 slot). **Scheduled as plan slice S1e.** Size: medium.

### Also confirmed working (worth knowing)

- **Detection + fog are genuinely healthy at runtime:** 179 `[DETECT]` + 679 `[DETECT-CONTACT]` lines,
  contacts 2→5, detected 1-of-8 → 4-of-8, and **all 679 contacts read `src=LAGGED` with zero `LIVE`** and real
  fog lag up to ~90,000 km — the 2026-07-17 scan-snapshot fix is confirmed live.
- One `[PERF]` 2117 ms frame at startup, correctly below the hang threshold.
- 24 missing `Resources\*.bmp` at boot: the texture pre-load runs as the first statement of the
  `PulsarMainWindow` constructor, ~90 lines **before** `ResourcesPath` is combined with the exe directory.

---

## 11. THE A3 WALLS THAT NOBODY HAD CHECKED (closed 2026-07-27 during the slow walk of the orders)

Of A3's 23 named walls, most were verified by the A4 rulings matrix or directly. **Five had been checked by
nobody.** All five are now verified — and one is a **live AI bug**.

### ⛔ W14 — THE AI'S GARRISON REBUILD PRODUCES CARGO, NOT SOLDIERS (live bug, cheap-wire)

`IndustryJob.InstallOn` **is** read: `ComponentDesign.cs:70,73` installs the finished component **only when it
is non-null**. Every other path sets it — `IndustryPanel.cs:302,377`, `IndustryDisplay.cs:417,424` (the
auto-install checkbox), `IndustryOrder.cs:165`, and even the costed ground tile queue (`GroundBuild.cs:63`,
`job.InstallOn = colony`).

**The AI's garrison rebuild does not.** `ConquerResolver.cs:388-396`:

```csharp
var job = new IndustryJob(info, designId);
job.InitialiseJob(1, false);
IndustryTools.AddJob(colonyEntity, gLineId, job);
IndustryTools.AutoAddSubJobs(colonyEntity, job);   // ← no job.InstallOn = colonyEntity
```

And there is **no generic default to fall back on** — the defaulting in `IndustryTools.cs:66-75` is
**commented out**. A ground unit is only raised when its component is *installed* (that is what fires
`GroundUnitAtb`), so with `InstallOn` null the finished infantry is **added to storage as an item and never
raised.** The AI detects a depleted garrison, spends the materials, and gets a crate. Every time.
**Fix: one line.** Gauge: run the rung to completion and assert a *unit* appears on the roster, not cargo.

### The other four, all CONFIRMED

| Wall | Verdict | Evidence |
|---|---|---|
| **W17 — `Amphibious` read by nothing** | **CONFIRMED — and it is a cradle-to-grave violation.** The dial is declared (`GroundLocomotionAtb.cs:32`), settable from JSON (`:40`), copy-ctor'd (`:43`) and **described to the player in the designer blurb** (`:51`, *"…, amphibious"*). The only other mention in the engine is a **comment** in `HexPathfinder.cs:39` noting amphibious/transport gating as *not* implemented — water is impassable for everyone. So a player can design, cost, and build an amphibious unit and it does nothing. | `GroundLocomotionAtb.cs:32,40,43,51`; `HexPathfinder.cs:39` |
| **W21 — `OrderFormationTreeMoveToHex` zero callers** | **CONFIRMED** — only the definition exists, no caller anywhere. | `GroundForcesDB.cs:960` |
| **W20 — `BombardGlobalHex` test-only** | **CONFIRMED** — definition plus **exactly one caller, a test.** Per-hex building bombardment is real and unreachable in play. | `GroundBuildings.cs:227`; sole caller `CityGridTests.cs:194` |
| **W7 — a Production-tab building is located NOWHERE on the map** | **CONFIRMED by mechanism** — `BuildingDesign.OnConstructionComplete` *installs* the building on the colony (`BuildingDesign.cs:52-57`) but nothing **places it on a hex**; only the tile side-car carries a destination. So a colony that grows after game start gains buildings the war map cannot see. | `BuildingDesign.cs:52-57`; `GroundBuild.cs:63` (the one path that does locate) |

---

## 10. THE COMPLETE A2 SEED-SWEEP LEDGER (orders §8: *"the dated audit doc records the sweep"*)

All 13 named seed items, plus what the sweep turned up beyond them. **Three seed claims were themselves wrong**
— recorded here so a future pass doesn't re-hunt them.

| # | Seed item | Verdict | Where the fix landed |
|---|---|---|---|
| 1 | `docs/aurora/GROUND-COMBAT.md:6` — "Pulsar has no ground combat at all" | **REFUTED** (all five sub-claims false vs a ~56-file subsystem) | correction banner + pointer to the as-built subsystem |
| 2 | `MVP.md` + `PLAY-TO-MARS` — the invade-from-orbit panel is the #1 blocker | **REFUTED** — built 2026-07-19 (`FleetWindow.cs:1756,1814`; AI at `ConquerResolver.cs:63`) | §L rewritten ✅; MVP row D + Stage 4 **re-pointed** at the 3 real gaps |
| 3 | `SYSTEMS-STATUS-AND-TEST-PLAN.md` retirement vs root `CLAUDE.md` mandating it | **CONFIRMED** (4 mandates vs 1 retirement note, same file) | retirement finished; 4 refs repointed; **then archived** (the full archive record is **§15d** below) |
| 4 | `Colonies/CLAUDE.md` — `ColonyHexMapDB` "built and wired" | **CONFIRMED landmine** — save-unsafe, still `SetDataBlob`-attached by a window **and** a processor | row rewritten; 2 "build ground combat on it" notes killed; **my own first correction later found OVERSTATED and re-fixed** |
| 5 | `Pulsar4X.Client/CLAUDE.md` — ground units live on the `ColonyHexMapDB` tile grid | **REFUTED** — they live on `HexQ/HexR` + `GlobalQ/GlobalR` + `MiniQ/MiniR` | corrected; also killed a reference to `GroundCombatWindow`, **which does not exist** |
| 6 | `GroundCombat/CLAUDE.md` — the upkeep source | **BACKWARDS** — assembler (`:299`) and garrison (`:101`) DO bill; the base-mod path (never mentioned) does not | replaced with a 3-row table so it cannot invert again |
| 7 | `GroundCombat/CLAUDE.md` — the "C3 FULL path" test | **CONFIRMED ABSENT** — the fixture has exactly 2 tests, covering the halves separately | corrected; logged as **GH8**; flagged that plan slice S5 builds on the untested joint |
| 8 | The surface-scale contradiction (560/47 vs 477/37) | **BOTH self-consistent, measuring different things** — it is a formula, not a constant | one canonical "SCALE — THE ONE TRUE ANSWER" box; Earth's coarse pitch sent to a **gauge** (slice S0), not guessed |
| 9 | `DOCS-INDEX.md` disagrees with itself (stamp vs rows) | **CONFIRMED** ×3 (MVP, PLAY-TO-MARS, GROUND-UNIT-VARIABLES) | all three rows reconciled with the stamp |
| 10 | Stale "nothing calls this yet" `.cs` comments | **CONFIRMED** — `LoadTroopsOrder`/`LandTroopsOrder` are issued by player *and* AI; `Speed_kmh` is read at `GroundForcesProcessor.cs:855` | comments corrected (in `eee5664`) |
| 11 | `EARTHFALL-CAMPAIGN-OPS.md` — "the hex is the unit of everything" | **OVERSTATED** — combat groups by *region*, capture flips the *region* | qualified: hex for infrastructure, region for the fight |
| 12 | `SURFACE-FOG-AND-RECON-DESIGN.md` under-reports itself | **CONFIRMED** — slices 5 **and** 6 are built (in `GroundThreat` / `ExpandResolver`) | both annotated ✅; the real remaining gap named: **client** deposit fog is still omniscient |
| 13 | `PLANETARY-GAMEPLAY-AUDIT` part-stale (2 open questions + P1) | **CONFIRMED** — both questions were LOCKED the same day; **P1 is now forbidden by ruling #27b** | both struck through with the correction beside them; header banner added |

### The three seed claims that were WRONG (client "dead code")

The seed list named *token health bars · hazard chips · the "Held:" line · Shift-click waypointing* as dead.
**Only the last is dead.** Health bars draw at `PlanetViewWindow.cs:1115`; hazard chips are real (`:184` reads
`PlanetEnvironmentsDB`, passed to `:987`/`:1538`); `Held:` draws unconditionally at `:1057-1059`. All three are
**built-but-runtime-unverified**, which is a different thing from dead — calling them dead would have sent
someone to rebuild working code. *(Method note: my own first grep "confirmed" hazard chips missing because I
searched `hazard chip` while the code says `chips`. **A negative grep is not evidence until you have tried the
words the code would actually use.**)*

**Shift-click IS dead, and worse than stated:** there is **no `HandleHexClick` method anywhere in the client**,
and `KeyShift` appears only in `WarpOrderWindow.cs` (space-side). The client doc described that method twice in
detail. And `PlanetViewWindow.cs:1433` **advertises the control to the player** — a hint with no handler. Fixing
the hint is a behaviour change, so it is scheduled (plan slice **S6**), not done here.

### Beyond the seed list — found while sweeping

| Finding | Verdict | Landed |
|---|---|---|
| **327 dead doc pointers** across 249 `.cs` files (25 of 48 distinct paths) | the largest doc defect in the repo | all repointed, residual grep 0; the grep recorded as a standing gauge (§7) |
| `BaseDataBlob.Clone()` is **virtual with a garbage default, not abstract** | a general trap — a `*DB` without `Clone()` silently becomes `System.Object` | **root `CLAUDE.md` landmine L12** |
| `Combat/CLAUDE.md:107` — "no diplomacy/relations system in the engine yet" | **REFUTED inside the function it describes** (`AreHostile` reads `DiplomacyDB` both ways) | corrected |
| `Industry/CLAUDE.md:111` — the "installations UI gap" | **REFUTED** — root gotcha #4 retired it; never swept from that file | corrected |
| `GroundCombat/CLAUDE.md:52` — garrison "so a fresh New Game has ground units" | **OVERSTATED + now against canon** (#27b) | reconciled |
| **Ruling #22 has ZERO code** — 7 named types, 0 files each; 1 of 9 slices exists | quantifies "designed, not built" | recorded at the top of the design's own build order |
| **Ground damage is flat per-tick, space is per-second** (`* dt` present in space, absent on ground) | the C2 tick trap, **independently re-confirmed** | recorded in the combat doc, not just the audit |
| `WEAPONS-DESIGN` — "saturation is derived from rate-of-fire, never hand-set" | **ship-only**; ground uses two hardcoded constants and has **no rate dial at all** | scoped to ships |
| `REAL-DISTANCE-COMBAT-DESIGN` header — "slices 2–5 planned" | **STALE** — slice 2 shipped as K1+K3 under a different flag; two more claims refuted | header corrected *(that doc was consolidated away 2026-07-29; the finding now lives in `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §12.3 + §19 S-8)* |
| `GROUND-UNIT-VARIABLES` — 5 stale `GroundForcesProcessor.cs` line refs + "multi-weapon plurality missing" | **REFUTED** — the W-track built it (`:472` loops the loadout, range-gating each weapon) | drift table + row flipped |

---

## 12. THE RULINGS-COMPLIANCE MATRIX — all 27 + doctrine + the tick, in ONE table (orders §2/A4)

**Why this section exists:** A4's three agents all ran and delivered verdicts *with* dependency edges — but in
three separate **ephemeral scratchpad files**. The repo had **no single place** showing all 27 rulings' status,
which is what A4 actually asked for. Consolidated here so it survives, 2026-07-27.

Legend: **DONE** · **PARTIAL** · **MISSING** · **INERT** (code exists, nothing reads it) · **OPEN** (undecided).

| # | Ruling (short) | Verdict | The load-bearing fact | Size |
|---|---|---|---|---|
| 1 | ONE designer; delete the 3 prebuilts | **MISSING** | Doc's stated blocker was wrong (the garrison builds its own C# designs). **Real blocker: exactly 3 base-mod templates carry `GroundUnitAtb`, so they are the AI's only buildable ground unit.** | large |
| 2 | Penetration + per-shot energy → the WEAPON | **MISSING** | Both live on the *unit*; `ToGroundUnitDesign` never sets them, so an **assembled unit is always 0/0**. Even per-mount fire reads `unit.Penetration`. | medium |
| 3 | Invalid design blocked from SAVING | **MISSING** | **Resolved dispute:** validity **computes + displays** but `SaveGroundDesign` never reads `r.Valid` → an invalid design registers as buildable. | **cheap-wire** |
| 4 | Ground parts cost research, scaling w/ complexity | **PARTIAL** | Gate mechanism real, but **17/22 templates cost 0**, **all 22 start-unlocked**, the 5 that cost all use the same `[Mass]*2` (no complexity relation), and the assembled unit design has no tech gate at all. | medium |
| 5 | Every hazard gets a specific counter | **PARTIAL** | 5 hazard types / 8 menaces; only Vacuum + ToxicAtmosphere have a designable counter. **The developer's own dust-storm example has nothing to counter — ground `SensorJam` is generated and read by NO ground code.** And "one hazard hits several stats" is **not expressible** (`HazardEffect` = one Type + one Magnitude). | large |
| 6 | Rally point = a building setting (space + ground) | **MISSING** | **No `Rally*` symbol exists anywhere.** Muster is hardcoded region 0; `DefaultRegionIndex` has zero writers; no building carries a muster field; the space half has no orbit field either. | medium |
| 7 | Units cost PEOPLE, no return | **MISSING** | Worse than "computed and never read": **`GroundUnitDesign` has no crew field at all**, and the assembly result does not even sum crew (its station/building siblings do). No death path touches population. | large |
| 8 | Upkeep + magazine; ammo must bite | **PARTIAL/INERT** | Upkeep bills for assembled + garrison units (base-mod path free). Ammo drain helpers exist but are **called only from tests**; the flat 1 kg/salvo makes a magazine never a trade. | medium |
| 9 | NOTHING IS FREE | **MISSING** | **TWO free paths, not one** — the "Build here" order *and* the `LocalConstruction` queue (spends only `PointsPerDay`, never `ResourceCosts`, yet lists infantry/armor/artillery). **And the trap: fortification value sums only from `Region.InstallationIds`, which the costed queue never writes** — so cutting the free path alone leaves no buildable fortification, CI green. | large |
| 10 | ONE build queue, destinations, visible progress | **MISSING** | **FIVE live build paths.** Only the tile side-car carries a planetary destination; the only real progress bar is on the *free* queue. | large |
| 11 | Every building occupies ground = war-map objective | **premise REFUTED** | `GroundFootprintAtb` is **already the single attribute** (presence = objective, `TileFootprint` = occupancy, both read live). **The gap is DATA — and BIGGER than first recorded: `2` of `51` installation templates, not 2 of 26** (Phase B recounted by `UniqueID` entries in `installations.json`; the old denominator counted the wrong thing). Phase B also confirmed all four live readers: `ColonyFactory.cs:126` (drops footprint buildings onto hexes at colony creation), `CityBuilder.cs:52` (spends the tiles), `GroundBuildings.cs:28` (the `HasAttribute` predicate), `:324` (reads the count) — plus a proper `Clone()` at `GroundFootprintAtb.cs:38`, so no L12 exposure. | data |
| 12 | Fund employment + power | **INERT** | Both wires complete end-to-end; both inputs **structurally zero** — zero templates declare `EmploymentAtbDB`, `powerDemandPerCapita` authored 0, and `uef.json` has no strain node. | **cheap-wire** |
| 13 | Semantic tile bonuses | **MISSING** | No per-tile bonus mechanism of any kind. **`CityTile.Terrain` is populated and read by nobody but its copy-ctor and two tests.** | medium |
| 14 | DELETE march-to-region → two-layer coordinate | **MISSING** ⚠ **RE-SIZED DOWN by Phase B — see §17** | `MoveToRegion` is the only verb wired **end-to-end** (issuer→processor→readout→save) ✅ — but *"the ONLY fully-wired move verb"* is **refuted**: `MoveToHex` is already wired at **four of seven ends** (enum `:292`, factory `:342`, formatter `:355`, **processor** `GroundForcesProcessor.cs:918`) and **the client already draws its waypoint path** on **global cylinder coords** (`PlanetViewWindow.cs:505,477`) — the global half of the two-layer scheme. It lacks only **ISSUERS**. **Four** coordinate systems confirmed; **no combined two-layer formatter** confirmed absent. ⚠ `MoveToRegion` carries the only save/load gauge of ground movement. | ~~large~~ → **cheap-wire + a retirement** |
| 15 | Fight is COMMITTED; retreat + break-away; pursuit | **PARTIAL** | **Ground has none of the four, and walking out is FREE** (a region march just removes the unit from the roster). Space has the lock + retreat but prices the exit with a hardcoded `const RetreatCasualtyThreshold = 0.5` while the doctrine's own `BreakAwaySeconds`/`Pursue` sit unread. | large |
| 16 | Mini-hex movement as a player order | **MISSING** | Mini coords are written **only** by the engine's auto-spread/closing steps. No order type, and the city-zoom click handler has **no move branch**. | medium |
| 17 | Show destination · distance · ETA · speed | **PARTIAL** | **All four ingredients exist as engine state; ZERO are displayed.** No accessor computes distance-remaining or ETA. | **cheap-wire** |
| 18 | Player target selection, gated by doctrine | **INERT** | `TargetPriority` enum + blueprint field + parser + **all 25 JSON values exist — and every caller is a unit test.** Ground fire still spreads by *current health*, so a cripple is never finished. | medium |
| 19 | Battle scoped to REAL DISTANCE + engage decision | **PARTIAL** | The real-metre **fire gate** is built and menu-ON; the battle **container** is still the region band; the **engage decision is MISSING**. | medium |
| 20 | Battalion composed like a fleet | **PARTIAL** | Structural parity is high (same four roles, same 0.5 evasion threshold). Missing: the composition **ladder** and **role sub-formation FORMING** — no `FormRoleSubFormations`, no `GroundFormationRoleDB`; role is computed transiently at maneuver time and never stored. | medium |
| 21 | What capture transfers | **OPEN — NOT DECIDED** | Capture is one statement (`GroundForcesProcessor.cs:1073`). Full 20-row decision aid in §8. | blocked |
| 22 | `CasualtyTier` + damage ledger + Training dial | **PARTIAL** | Training dial **built + gauged**. But of the design's 9 slices only **1** exists, and **all 7 Part-B types return ZERO files** (`CasualtyTier`, `ModelCount`, `BattleLedger`, `WeaponKey`, `VictimSnapshot`, `WeaponTally`, `SideReport`). | large |
| 23 | Fire rate CALCULATED + shorten the tick | **MISSING** | No ground weapon carries a rate; tick = 1 h; **and the salvo pool is NOT `dt`-scaled while space's IS** — so shortening the tick *multiplies* ground damage. **A committed CI spec already pins a 5 s ground quantum and nothing implements it.** | large |
| 24 | Ground battle readout = a LOG | **MISSING** | The whole `GroundCombat/` folder publishes **two** events, both in the troop-lift orders. And the interrupt pops the **space-only** report. | **cheap-wire** |
| 25 | Hover tooltip + Force-Management detail | **PARTIAL** ⚠ **WALKED BACK by Phase B — the first draft was right and my "correction" to MISSING was the error.** | *"Zero tooltips anywhere" is FALSE* — the client has **101 `SetTooltip` + 12 `BeginTooltip`** across 20+ files. Aggregated ground strength already renders (count + Σ Health/MaxHealth per faction×type×region, `PlanetViewWindow.cs:1081-1087`; token type+count+`»` at `:466`; live stance multipliers at `:1497`), and hover is already detected 3× for clicks (`:295,296,791`). **True claim, scoped:** zero tooltip *calls* in either ground window, and no per-INDIVIDUAL-unit drill-down. | **cheap-wire** (was medium) |
| 26 | Fading last-known contact marker | **MISSING on ground** | Space is built (`SensorContactIcon`); the ground read is live-only. | medium |
| 27a | Five behaviour flags into the SAVE | **MISSING** | Five process statics, set on **two New-Game paths**; `LoadGame.LoadFile` sets **none**. | medium |
| 27b | No default garrison/enemy in a stock New Game | ✅ **DONE** | All three auto-spawns default `false` (`NewGameMenu.cs:52,55,60`). **The one ruling already satisfied.** | — |

### Doctrine + the tick (the frame that governs D2/D3)

| Slice | Verdict | The fact |
|---|---|---|
| **D1** catalog + reader + reciprocal guard | **data BUILT_AND_GAUGED / reader INERT** | **9 of the reader's 10 functions have zero non-test engine callers**, and the one live path (`FleetDoctrine.TrySetDoctrine`) **drops 4 fields silently** and overrides a 5th deliberately. |
| **D1b** 25-entry role catalog + client filter | **data green / filter space-only** | The **ground** client still reads `groundStances.json`. |
| **D2** ground reads the unified catalog | **MISSING** | 5 live readers of `GroundStances` remain. |
| **D3a** movement steering | **MISSING** | `ClosingIntent` does not exist; `SpeedMult` is read only by a client *label*. |
| **D3b** fire behaviour | **MISSING** | No ground retreat, no pursuit, no priority list — **but the dials are already authored on all 25 entries**, so much of it is cheap-wire. |
| **Leader modulation** | **space BUILT / ground MISSING** | Zero `CommanderDB` reads anywhere in `GroundCombat/`. |
| **The tick** | **MISSING, with a spec waiting** | `Resolver2DJointsSpecTests.cs:210` pins a 5 s ground quantum + the 720-per-hour and determinism invariants; no engine code implements it. |

### Dependency edges (the orders name three; all three CONFIRMED, one sharpened)

1. **#1 needs #2 and #4 first** ✅ — and the *reason* in the canon doc was wrong; the real one is that the prebuilts are the AI's only buildable ground unit.
2. **#9 and #10 must land together** ✅ — **sharpened: CI would not catch the breakage.** Fortification sums only from `Region.InstallationIds`, which the costed queue never writes.
3. **#23 needs the tick slice** ✅ — **sharpened: they must be the SAME slice**, because the pool is not `dt`-scaled, so a tick change alone multiplies damage.
4. *(added)* **#18 should precede #23's overkill rule** — "sequential down the priority list" needs a priority list to exist, which is D3b.
5. *(added)* **D0 precedes D2/D3a/D3b** — otherwise every doctrine slice decorates fields `TrySetDoctrine` discards.

---

## 5. Resume — everything the next pass needs is already on disk

The 19 assignment briefs and the shared briefing survive at
`/tmp/claude-0/-home-user-4x-Game/<session>/scratchpad/` (`BRIEFING.md`, `assignments/<id>.md`,
`run-evidence.js`). If that scratch is gone, regenerate from `OPERATION-GROUND-TRUTH-PROMPT.md` §2.

| Batch | Assignments | State |
|---|---|---|
| A1 log forensics | `A1a-timeline` ✅ · `A1b-failures` ✅ · `A1c-combat` ✅ · `A1d-ai-silent` ✅ | ✅ **all four returned** — the incident review is §9 |
| A2 doc verification | `A2b-docs-combat` ✅ · `A2c-docs-subsystems` ✅ · `A2f-scale-comments` (covered solo) · `A2d` (covered solo) — **`A2a-docs-ground` + `A2e-docs-dashboards` STILL UN-RUN** | ⚠ partly done; see §10 for everything that WAS swept |
| A3 reachability walls | `A3a-start-state`, `A3b-designer-wall`, `A3c-build-queue-wall`, `A3d-movement-verbs-wall`, `A3e-observability-wall` | ❌ died — **partially covered solo in §3** |
| A4 rulings matrix | `A4a-rulings-1-9` ✅ · `A4b-rulings-10-18` ✅ · `A4c-rulings-19-27` ✅ | ✅ **all three returned** — findings C1–C4 + the ledger re-verdicts |
| A5 gauge coverage | `A5-gauges` | ❌ died — **CI shard reality covered solo in §1** |

**Operational lesson for the next run (worth keeping):** the agent concurrency cap is
`min(16, cores-2)` and this container has **4 cores → only 2 agents run per workflow**. Splitting one
19-agent workflow into 4 concurrent workflows raised throughput ~4×. Do that from the start — but note
that 8 concurrent agents burned ~2M tokens in ~5 minutes and tripped the account limit, so **pace the
fan-out** (a batch at a time) rather than firing everything at once.

## 6. Still owed (unchanged from the handoff's Definition of Done)

Phase A completion (the five batches above) → Phase B adversarial verify → Phase C doc
consolidate/correct/delete → Phase D `docs/ground/PLANETARY-FUNCTIONAL-PLAN-<date>.md` (the
functional/accessible/observable delta ledger + ordered slices) → Phase E the committed, validated,
never-invoked `close-planetary-delta` workflow. Open developer questions are collected in the plan doc;
**ruling #21 (what a planet capture transfers) stays OPEN and must not be decided.**

---

## 13. TEST / GAUGE COVERAGE — the A5 sweep (orders §2/A5)

The one Phase A batch whose agents never ran. Done in the main loop, 2026-07-27. Four questions, in the
orders' own order.

### 13a. Is `docs/TESTING-TRACKER.md` telling the truth? — **YES. Zero phantom tests.**

The C3 defect (a doc claiming a test that does not exist) **does not recur in the tracker.**

- **97** backticked fixture names appear in the tracker. **94** resolve to a real `class` in
  `Pulsar4X/Pulsar4X.Tests/`. The three that don't are the tracker's own **family abbreviations** inside
  `…`-ellipsis lists, and each is legitimate: `RangeReadoutTests.cs` and `SpatialEnvironmentsDioramaTests.cs`
  both exist, and **`SocietyReadout` is an engine class**, correctly named as the thing under test rather than
  as a fixture.
- All **7** `Fixture.Method` claims verified present in the named fixture:
  `DiplomacyIffTests.SignedPact_StopsTheFight` · `EconomyReadoutTests.Economy_BaselineReadout_OverOneYear` ·
  `FactionEconomyTests.SocietyReadout_DumpsColonyState` · `LegitimacyProcessorTests.War_TaxesLegitimacy_ByMilitarism` ·
  `MoraleTests.StartingColony_Infrastructure_ProvidesHousingComfort` · `PlanetRegionsTests.SurveyReveal_` ·
  `StationFactoryTests.Station_TakesDamage_`.

### 13b. The tracker's real blind spot is the **INVERSE** of C3 — 6 `[Ignore]`d tests, and it indexes **one**

Not phantom tests: **real tests that are not running.** There are **6** `[Ignore]` attributes at HEAD. The
tracker mentions exactly one — and only to record that it was *resolved* (line 31) — then states *"No
deliberately-red engine gaps remain."* That is true **about reds**, but an `[Ignore]` is a gauge that is
switched off, and **three of the six encode live defects:**

| Where | What it parks | Bearing on THE PLAN |
|---|---|---|
| `NewGameStartSmokeTests.cs:24` | base + **testingmod** New Game colony build throws `NullReferenceException` (the testing mod ships incomplete Armor/Theme data) | **the ACCESSIBLE leg.** Reachability checked: `testingmod` **ships** (`Pulsar4X/GameData/testingmod/`) and the New Game *"Select Mods to Enable"* page lists **every** discovered mod with a checkbox (`NewGameMenu.cs:157-171`) — so this is **one tick away from a player**. Mitigating: its manifest has **no `DefaultEnabled` field**, so it reads `false` (`ModsState.cs:62`) → **reachable, not default.** It is in **neither** `TESTING-TRACKER.md` nor `CLIENT-TEST-CHECKLIST.md` |
| `EfKithrinExpandArcTests.cs:322` | an AI-founded colony starts **0-population AND 0-tax-rate**, so it never pays tax. In-code note: *"a real finding, not a broken test"* | **S1b.** The registry fix makes a captured/founded world join a faction's `Colonies` — this says such a world contributes **nothing economically** until pop *and* rate are seeded |
| `MidCampaignSaveLoadTests.cs:173` | the 2D group-plane battle-frame anchors are **not** exercised through save/load | the TWOD track, not this plan — but it is an unrun save-safety gauge |

The other three: `SystemGenTests.cs:297` and `:341` are legitimate exclusions (long-running / manual
statistical). **`PathfindingTests.cs:102` — `[Ignore("Incomplete Test")]` — carries no reason, no owner and
no date.** It is the weakest of the six and the only one with nothing to act on.

**Owed to the tracker:** an *"off-but-real"* section listing all six with a reason and an owner. Six switched-off
gauges are invisible today.

### 13c. Which planned slices have NO gauge — **three, and only one is a real gap**

**17 of 19** slice headings in `docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md` carry an explicit
**Gate:** line. S7's four sub-slices are covered (D0 has its own gate; the rest by *"Gate per sub-slice"*).
The three without:

- **S11 (Depth) — THE REAL GAP.** ~13 named depth items (#7 `CrewReq` · #8 ammo bites · #5 hazard gear ·
  #22 `CasualtyTier` · #12 employment/power · #13 tile bonuses · M4 terrain · Layer-6 naming · client fog ·
  the grave rung · hex deposits · G6b) and **only the pulled-forward `SYSTEM-GENERATION` G1 round-trip carries
  a gauge.** S11 must never be entered as a unit — each item gets its Gate line written when it is scheduled.
- **S12 (what capture transfers) — gauge-BLOCKED, not gauge-missing.** Blocked on Q1; the assertion cannot be
  written until the ruling says what transfers. Recorded as blocked.
- **M1 (the live cradle-to-grave sitting) — ungauged by design** (it *is* the Layer-3 gauge) **but missing from
  `TESTING-TRACKER.md`**, the one doc that owns Layer 3.

**The wider tracker gap:** the tracker has **zero rows** for any of THE PLAN's 19 slices and does not reference
the plan at all. Upkeep rule for the build: **add M1 as a Layer-3 row now; add each slice's Layer-1 row in the
same commit that ships the slice.**

### 13d. Shard pressure — **two slices need their own shard, and there is an undocumented double-run trap**

`rest` is the complement filter, so **every fixture this plan creates lands in the slowest shard by default.**
Measured load using `TestScenario.CreateWithColony` — the call `ci.yml` itself names as the dominant cost
(it re-parses **all** the mod JSON on every call):

| Shard | Fixtures | `CreateWithColony` calls |
|---|---|---|
| `stations` (isolated **because** it was the ~11-min bottleneck) | 1 | **18** |
| `rest` (the complement) | **234** | **665** |
| — of which `GroundForcesTests` alone | 1 | **48** across 61 tests |

`GroundForcesTests` is the **single heaviest fixture in the suite** by this measure — and it is the natural
host for the S1 and S8 assertions.

- **S1 (ground battle log)** — fights a battle to completion with assertions per phase. **Give it its own
  fixture name and its own shard, in the same commit.**
- **S8 (tick + rate model)** — its own gate requires *the same fight run at two tick lengths*, plus a
  reference fight for byte-identity: **≥3 fights.** **Own shard.**
- S0 (three bodies' hex readout) · S5 (a build queue to completion) · S1f (drive the rung to completion) ·
  S9 (bombardment + AI rung) — moderate. **Watch the TRX duration column; do not pre-carve.**

**⚠ THE TRAP, documented nowhere:** `rest` is a **hand-maintained** complement — `ci.yml:69` is `!~` of all
seven named shards. Adding shard `X` **without** also adding `FullyQualifiedName!~X` to the `rest` filter makes
`X` run **TWICE** — once in its own shard and once in `rest` — so the "isolation" costs more than it saves and
`rest` never shrinks. `docs/earthfall/IMPLEMENTATION-AUDIT-2026-07-22.md:83` calls the sharding *"gap-proof by
construction,"* which is true for **coverage** (nothing can be excluded from every shard) but says nothing
about this **duplication** direction. **Every new-shard commit edits `ci.yml` in TWO places.**

---

## 14. PHASE B — ADVERSARIAL VERIFICATION, ROUND 2 (orders §3)

Round 1 (**§15a** below) checked 10 claims and left **two ⚠ rows accepted on one agent's word**. This round
targets the verdicts where **being wrong means a true thing was deleted** — the orders' actual reason for Phase B.

### 14a. Method, and an honest cost note

A 15-agent workflow (5 claims × 3 diverse lenses: counter-evidence / scope-auditor / consequence-checker,
default-to-refuted) was launched and **killed at the developer's third budget intervention**. It had started 2
agents (the concurrency cap on this 4-core box) and finished **none** — ~150–200 k tokens, **zero verdicts**.
The arithmetic was already recorded (now **§15b**) and was ignored because a session flag asked for
fan-out. **The standing instruction outranks the flag.** Round 2 was then done in the main loop for a small
fraction of that, using the same three lenses applied by hand.

### 14b. Results — 1 REFUTED (a real walk-back), 4 CONFIRMED, and **4 NEW defects found**

| # | Claim under test | Outcome |
|---|---|---|
| **B1** | `march-to-region` is the only fully-wired move verb (#14) | **carried forward — still not re-verified.** Honest residue. |
| **B2** | Unit inspection is MISSING; "zero tooltips anywhere" (#25) | 🔴 **REFUTED — the operation's first genuine walk-back** |
| **B3** | The invade-from-orbit panel is BUILT, so the "#1 blocker" claim is refuted | ✅ **CONFIRMED in code** — but the **remediation was incomplete** (defect N1) |
| **B4** | `GroundFootprintAtb` is already the single attribute; gap is data-only (#11) | ✅ **CONFIRMED** — count corrected, gap is **bigger** |
| **B5** | `Combat/CLAUDE.md:107` "no diplomacy system" is refuted by `AreHostile` | ✅ **CONFIRMED** — but **my framing was overreach** (defect N2), and it exposed defects N3/N4 |

#### 🔴 B2 — REFUTED. Ruling #25 goes back to PARTIAL, and my own "correction" was the error.

*"There are zero tooltips anywhere"* is **flatly false**: the client has **101 `SetTooltip` + 12 `BeginTooltip`**
calls across 20+ files. *"Zero per-UNIT stat readout in any surface"* is also too strong — `PlanetViewWindow`
already renders **aggregated** strength (count + Σ Health/MaxHealth per faction × unit-type × region, `:1081-1087`),
a map token with type-initial + count + a `»` moving marker (`:466`), and **live stance multipliers** (`:1497`).
Hover is already **detected** three times, for click handling (`:295,296,791`).

**The true, scoped claim:** **zero tooltip calls in either ground window** (`SetTooltip`/`BeginTooltip` = 0 in both
`PlanetViewWindow.cs` and `PlanetaryWindow.cs`), and **no per-INDIVIDUAL-unit drill-down**. ⇒ **#25 re-sized
medium → cheap-wire**: the numbers exist, the hover plumbing exists, only the tooltip body and a per-unit view are
missing. *The first plan draft said PARTIAL; the session "corrected" it to MISSING; Phase B restored PARTIAL.*
**Lesson: an absolute quantifier is the most refutable thing in any finding — and this session produced two of them.**

#### ✅ B3 — the code is right, the doc fix was half-done (**defect N1, fixed**)

`FleetWindow.cs:1754/1812` issue real `LoadTroopsOrder`/`LandTroopsOrder` commands through
`_uiState.Game.OrderHandler.HandleOrder`, gated on `CanLoad`/`holdsOrbit`, each with a `SessionLog.Action` trace.
The panel is real. **But** `docs/PLAY-TO-MARS-WALKTHROUGH.md` had §L rewritten while **table row L (line 39) still
read `❌ no button, no order`** — the doc contradicted itself 95 lines apart — and the header still said *"Three
gaps block a hands-on invasion."* **Both fixed**: row L flipped to ✅ with a pointer, and the count corrected to
**two** (K bombardment = slice S9; I no-enemy-on-normal-start, with the DevTest qualifier). *A correct verdict with
an incomplete remediation reads exactly like a wrong verdict to the next reader.*

#### ✅ B4 — confirmed, and the data gap is **bigger** than recorded

All four live (non-test) readers verified: `ColonyFactory.cs:126` drops footprint buildings onto hexes at colony
creation, `CityBuilder.cs:52` spends the tiles, `GroundBuildings.cs:28` is the `HasAttribute` predicate, `:324`
reads the count. It carries a proper `Clone()` (`GroundFootprintAtb.cs:38`) — no L12 exposure. **Correction: 2 of
51** installation templates carry it, not 2 of 26; the old denominator had counted attribute *declarations*
(62 in that file) rather than templates.

#### ✅ B5 — confirmed, my framing withdrawn, and **three more defects found**

`AreHostile` (`CombatEngagement.cs:1847-1874`) does read `DiplomacyDB` **both ways** — a declared-war latch in
either direction forces hostility, and a **mutual** Friendly/Allied stance suppresses it. Precise shape, which the
audit's phrasing missed: **diplomacy can only SUPPRESS default hostility or FORCE war — it never creates hostility
from a score**, and an unmet stranger falls through to "different faction = hostile."

- **N2 — my own overreach, withdrawn.** I framed this as *"changes a documented build order."* It does not:
  `DIPLOMACY-DESIGN.md:7` already recorded keystone 3 as *"substantially DONE"* on **2026-07-07**, twenty days
  earlier. Only `Combat/CLAUDE.md:107` was stale.
- **N3 — `DIPLOMACY-DESIGN.md` contradicted itself (fixed).** Its status banner said keystone 3 substantially done;
  its blast-radius table ~460 lines below still said *"hostility isn't diplomacy-driven."* Row corrected.
- **N4 — a doc claim that invited deleting live code (fixed).** `DIPLOMACY-DESIGN.md:458` said `SignalQuality`
  **"was CUT."** **REFUTED:** it is **live in 9 files** and gates **survey reveal** at `> 0.20` / `> 0.80`
  (`SystemBodyInfoDB.cs:154-160`, `StarInfoDB.cs:130`). What was cut is its *role as the hidden-info gradient*, not
  the field. Read literally, the old wording is an instruction to break survey accuracy.

**Two consequences for slice S1e**, both landed in THE PLAN:
1. **A justification withdrawn** — S1e is **not** "the keystone prerequisite `DIPLOMACY-DESIGN` names"; that
   keystone was *dissolved*. The slice stands on its own evidence (288 of 288 AI decisions read `vs no threat`).
2. **A concrete root-cause lead**, where the plan had said *"unverified — start there, do not guess"*:
   `ThreatAssessment.cs:11` documents that it *deliberately* uses signal **STRENGTH** because the `SignalQuality`
   path was design-cut — so the field being read is the intended one and it is the one reading zero; and
   `SensorTools.cs:69` carries a **commented-out** `if(detectionValue.SignalStrength_kW > 0)` guard ~150 lines above
   the setter that assigns it (`:220`). **Start at those two lines.**

### 14c. The lower-stakes verdicts, also re-checked this round (all CONFIRMED, three sharpened)

| Verdict | Outcome |
|---|---|
| `GroundCombatWindow` does not exist (§10 #5) | ✅ **CONFIRMED** — zero references under any spelling; the real surfaces are `PlanetViewWindow.cs` / `PlanetaryWindow.cs`. *Bonus:* `PlanetaryWindow.old.cs` is a **fully commented-out corpse** (`/*` from the top) — dead weight, but sealed, so **not** an L1 dead-code-that-looks-live risk |
| Multi-weapon plurality is BUILT (§11, `GroundForcesProcessor.cs:472`) | ✅ **CONFIRMED, stronger** — it loops `WeaponLoadout` **and** range-gates **per weapon per target** (`WeaponReaches(u,t,m.RangeHexes,m.Range_m,…)`), preserves a documented byte-identical collapsed path for single-weapon units, and interlocks with ammo (dry ⇒ silent; one salvo burned iff any weapon fired) |
| Combat groups by REGION, capture flips the REGION (§10 #11) | ✅ **CONFIRMED, sharpened** — `byRegion` at `:272-275`, `ResolveRegionCombat` per region at `:304`, faction grouping *within* a region at `:379`. **Added precision:** the region is a **derived band of hex columns** (`:210` `PlanetGridFactory.RegionOfColumn`), so the hex is the movement substrate *and* the region is computed from it; and **planet** capture is **all-or-nothing** — every region must be uniformly held (`:1056-1073`) |
| Upkeep is BACKWARDS in the doc (§10 #6) | ✅ **CONFIRMED HARD** — exactly two production writers (`GroundUnitAssembly.cs:299` mass-scaled, `GroundStartGarrison.cs:101` HP-scaled); `GroundUpkeep.cs:57` skips `<= 0`. **Stronger:** **zero** JSON files in all of `GameData/` mention upkeep, and **`GroundUnitAtb` has no upkeep parameter at all** (`:52`) — a base-mod ground unit is *structurally* unable to bill, not merely unconfigured. ⚠ **THE SELF-CORRECTION BELOW WAS ITSELF WRONG — RETRACTED 2026-07-28. DO NOT ACT ON IT.** ~~*Self-correction: I first called this an exact-arity breaking change; the constructor already uses optional trailing parameters, so adding `upkeep = 0` would not break the 3 existing declarations. Cheaper than I first said.*~~ **The ORIGINAL claim was right: adding an `upkeep` parameter IS an exact-arity breaking change that crashes New Game.** `ComponentDesigner.cs:94` binds via `Activator.CreateInstance(Type, object[])` **without `BindingFlags.OptionalParamBinding`**, so `RuntimeType.CreateInstanceImpl` **culls any constructor whose parameter count exceeds the supplied argument count BEFORE the default-value logic is ever reached** — a C# optional parameter does not count. `GroundUnitAtb` has exactly ONE constructor, 7 parameters (`GroundUnitAtb.cs:52-53`, two of them defaulted), and all three base-mod templates supply exactly 7 values (`installations.json:1775, 1880, 1985`). Add an 8th parameter and those three 7-value `AtbConstrArgs` calls match **no** constructor → `MissingMethodException` → rethrown as a plain `Exception` at `ComponentDesigner.cs:111` → **New Game dies on the ground stack.** The correct pattern is the one `GroundArmorAtb.cs:44-55` already uses: **keep the old constructor and add a NEW overload at the new arity** (landmine **L7**). Verified against source by the 2026-07-28 agent sweep; consistent with `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md` D2-3. |
| `Industry/CLAUDE.md:111` installations-UI gap is refuted (§11) | ✅ **CONFIRMED** — `PlanetaryWindow.cs:102` gates on `ComponentInstancesDB`, `:218` renders through it; the doc already carries the retraction |

### 14d. Honest residue — what Phase B still owes

- **B1 (#14, "the only fully-wired move verb")** — still resting on one agent, still not independently re-checked.
  It sizes slice **S6 (large)** and ruling #14 says **DELETE** that verb, so a wrong verdict here is expensive.
  **Highest-value single check for the next session.**
- **§10 #1** (the five sub-claims behind "Pulsar has no ground combat at all"), the **`WEAPONS-DESIGN` saturation
  ship-only** scoping, the **`REAL-DISTANCE` header STALE** verdict, and **`GroundCombat/CLAUDE.md:52` OVERSTATED** —
  all four still stand on round-1 or agent evidence.
- **Standing lesson from this round, worth more than any single verdict:** three of the five defects found were not
  wrong *findings* — they were **correct findings with incomplete remediation** (a section fixed but not its index
  row, a banner fixed but not its table row). **A half-applied fix is indistinguishable from a wrong verdict.**
  When a verdict lands, grep the doc for every *other* place that states the same thing.

---

## 15. THE OPERATION'S PROCESS RECORD — migrated from the deleted compliance tracker

`docs/DOCS-AUDIT-2026-07-27.md` was **deleted 2026-07-27** at the developer's instruction, after its
job was taken over by verified-done annotations written directly into `OPERATION-GROUND-TRUTH-PROMPT.md`. It had
been created because the session drifted; the developer then asked the right question — *"why are we doing the
compliance doc over the orders?"* — and the answer was that a derived checklist standing in for its source will
quietly drop whatever it failed to copy. **That is exactly what happened:** the tracker had no row for the
mission's `DELETE` verb, so for most of the run nothing was deleted or archived and the omission was invisible.
**A summary that outranks its source is the failure mode this whole operation exists to fix.** The load-bearing
content is preserved below; the rest was duplication of the orders. *(Full text remains in git history.)*

### 15a. PHASE B — ROUND 1 (10 claims, main-loop self-verification)

The original design was 10 claims × 3 voters = **30 agents ≈ 11.1 M tokens** (at the ~370 k/agent measured from
this session's own completed batches). It was **killed mid-flight** on the developer's budget intervention and
redone in the main loop for **~30 k**. The deviation is recorded as an engineering decision, not a quiet downgrade.

**Why self-verification is legitimate here, and where it is weaker.** The rule exists to stop *one agent's* error
becoming canon. The session did not produce those claims and applied the same discipline — open every cited line,
try to refute, default to refuted. It is weaker in exactly one way: **it shares this session's blind spots.** Any
claim below that a future session finds wrong should be treated as a failure of *this method*, and re-run with
three real lenses. *(Round 2, §14, then found a walk-back that round 1 had missed — evidence the caveat is real.)*

| Claim | Verdict | What the check found |
|---|---|---|
| **C1** registry never updated on capture | ✅ **CONFIRMED (hard)** | The *only* production writes to `FactionInfoDB.Colonies` are two `.Add` calls (`ColonyFactory.cs:104,226`). Every other hit is a test, a copy-ctor (`FactionInfoDB.cs:163,178`) or an unrelated client dict. **No removal path exists in production code.** |
| **C2** salvo pool not `deltaSeconds`-scaled | ✅ **CONFIRMED** | `double pool = atk * SalvoScale;` (`GroundForcesProcessor.cs:491`) — no `deltaSeconds` term. Shortening the tick multiplies output. |
| **C3** a 5 s spec already exists | ✅ **CONFIRMED, stronger than claimed** | The spec *declares* `TheaterGroundQuantum = 5` and asserts `3600 % 5 == 0`, `720` steps, and equality with `SpaceQuantumSeconds` (`Resolver2DJointsSpecTests.cs:206-220`). The earlier 60 s suggestion was rightly withdrawn. |
| **C4** doctrine keystone drops fields | ✅ **CONFIRMED, corrected** | **FOUR** fields dropped silently (`TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds`, `Pursues`); a **fifth**, `EngagementPosture`, is *deliberately* overridden with a comment saying why. "Silently drops `EngagementPosture`" was wrong. |
| **CANON-1** garrison does not use the prebuilts | ✅ **CONFIRMED, corrected** | `MakeGarrisonDesign` builds `new GroundUnitDesign` in C# (`GroundStartGarrison.cs:90-103`). But "the AI's **only** buildable ground unit" was too strong: `IsBuildableGroundUnit` is a **generic** predicate; the real constraint is that **exactly 3 base-mod templates carry `GroundUnitAtb`**. |
| **CANON-9** two free build paths | ✅ **CONFIRMED** | `LocalConstructionProcessor` spends only `PointsPerDay` (`:33`) then calls `AddComponent` (`:50`) — no `ResourceCosts` anywhere in the file. |
| **CANON-fortification** trap | ✅ **CONFIRMED, stronger** | Fortification **value** sums only from `Region.InstallationIds` (`SumLocal :56-57`, `SumAdjacent :88-89`); hex ids are read **only subtractively** (`CapturedBuildingIds :79-80`) ⇒ **writing hexes alone can never fortify.** |
| **CANON-14** march-to-region is a working verb | ⚠ **accepted, not independently re-checked** | Still owed — see §14d, where it is the highest-value remaining check. |
| **PLAN-25** unit inspection MISSING not partial | 🔴 **later REFUTED by round 2** | See §14b. This is the row the round-1 caveat above predicted. |
| **F6** the ungated DevTest main-menu button | ✅ **CONFIRMED** | `MainMenuItems.cs:51-53`; `NewGameMenu.cs:979-986`. |

### 15b. Agent-budget measurements (the numbers, so nobody re-derives them)

| Measurement | Value |
|---|---|
| Tokens per deep discovery agent | **~370 k** (A4a+A4b = 720,905 for 2; A4c+A1d = 770,644 for 2) |
| Concurrency cap on this container | **2** — `min(16, cores-2)` on 4 cores, so N agents = **N/2 sequential rounds** |
| 8 concurrent agents, one burst | **~2 M tokens in ~5 min**, tripped the account limit |
| The 19-agent Phase A fan-out | died on the usage limit, **zero findings returned** |
| The 15-agent Phase B fan-out (§14a) | 2 started, **none finished**, ~150–200 k, **zero verdicts** |
| The same Phase B work in the main loop | a small fraction of one agent's budget |

**The pacing rule that came out of it:** one batch at a time (≤4 agents), checkpoint to this dated doc after each.
**And the shape rule (§14a):** fan-out earns its cost when work is **wide, specifiable and genuinely parallel**
(the log forensics — 3,400 log lines, split cleanly, correctly kept running). It is the wrong shape when the work
is **one thread that keeps branching**, because a subagent's fixed output form has nowhere to put a branch — and
4 of the 5 defects in §14 were found *beside* the question asked, not inside it.

### 15c. Phase A coverage — which agents actually ran

✅ returned: `A1a` `A1b` `A1c` `A1d` (all four log-forensics, the batch the orders call most important) ·
`A2b` `A2c` · `A4a` `A4b` `A4c` (the only fully-complete batch).
❌ never ran: `A2a` `A2d` `A2e` · `A3a`–`A3e` · `A5`.
⚠ covered by other means and annotated as such in the orders: `A2f` (solo), `A3a`–`A3e` (via the A4 rulings pass
+ solo), `A5` (done solo in the main loop — see §13).

**Assignment briefs for every row are preserved on disk** (`scratchpad/assignments/<id>.md` + `BRIEFING.md` +
`run-evidence.js`), so any row re-fires in one call; regenerate from the orders §2 if the scratch is gone.
The deferral of the un-run batches (~12 agents × ~370 k ≈ **4.4 M tokens**) is **chosen, not drifted into.**

### 15d. THE DELETION / ARCHIVE PASS (the orders §4 obligation) — done 2026-07-27

This is the record of the mission verb that the deleted tracker had no row for.

**Archived** (superseded-but-historical → `docs/archive/` with a banner):
`SYSTEMS-STATUS-AND-TEST-PLAN.md` **MOVED** to `docs/archive/`, banner intact — **23 sites across 17 files**
repointed, `.md` **and** `.cs`, residual grep **0**. Earlier in the run it had been bannered *in place* with "so
old line references stay resolvable" as the reason; that was the rule being softened — the orders say *archive it*,
and moving it keeps every line resolvable at its new path anyway. *(The orders file itself still points at the old
path on purpose: it is a historical record of the task as issued, not a live pointer.)*

**Debt closed:** `DOCS-INDEX.md` known-debt **3(c)** (relocate the three bannered superseded docs — `PLAN`,
`AURORA-GAP-ANALYSIS`, `HAZARD-DISCOVERY`) — **all three were already in `docs/archive/`.**

**`CLIENT-TEST-CHECKLIST.md` pruned:** 103 items, **7 already confirmed live** and interleaved among the 96 open
ones. The confirmed block was **folded** under a RETIRED summary rather than deleted — a passed runtime check is
*evidence*, and deleting it would lose the only record that the fleet-menu freeze fix was ever verified.

**Deletions of design docs: NONE — a considered verdict, not an omission.** Every remaining `docs/` file is live
design, external reference (`aurora/`), a dated point-in-time record (the audits — valuable *because* they are
snapshots), or historical-with-a-banner in `docs/archive/`. Specific candidates a careless pass would have deleted,
and why they stay: `SYSTEMS-STATUS-AND-TEST-PLAN.md` (archived — its narrative is the only record of that era's
system map); `ColonyHexMapDB`-adjacent notes (the blob is a **live landmine** — the warning must stay loud, not
vanish); `OPERATION-GROUND-TRUTH-PROMPT.md` (the orders — mark superseded when the plan it produced is accepted,
never delete). **The ~22 `.md` provenance mentions of merged-away docs stay untouched** — deleting exactly those is
what destroyed history in the 2026-07-13 sweep. *(The one doc deleted this operation is the compliance tracker
itself, at the developer's instruction, with its content migrated here.)*

### 15e. THE HANDOFF (orders §8, Definition of Done #7)

**What changed.** Documentation and code *comments* only — **no behaviour, no data, no test logic.** Highlights:
**327 dead doc pointers** across 249 code files repointed (over half of all doc paths cited from code led to a
404; one doc was cited 61 times at a path that no longer exists); the `SYSTEMS-STATUS-AND-TEST-PLAN` retirement
finished and root `CLAUDE.md`'s four contradictory mandates repointed; **THE PLAN** with the
functional/accessible/observable delta ledger; the **`close-planetary-delta`** workflow (authored, statically
validated, **never invoked**); the log forensics that caught the instruments lying; and Phase B's walk-back (§14).

**Red/green.** All C# changes live in **one** commit (`eee5664`, the comment sweep), **green on all 7 jobs**.
Every commit after it is **markdown-only** — verified by `git show --name-only`, 0 non-`.md` files in each — so
CI risk is nil. Inherited `b218acf`/`255bc52` were both already green. **Nothing is red.**

**What to rule on first, in order:**
1. **Q2 — the scenario start.** Cheapest decision, biggest payoff: the thing you need already exists as an
   ungated **"DevTest"** main-menu button that boots you plus two developed rivals with all five ground flags on.
   Promoting it to a supported *Scenario/Skirmish* start is a rename, not a build, and it satisfies **#27b**
   exactly because your stock New Game stays clean.
2. **Q3/Q4/Q5 — the tick.** Unblocks the whole fire-rate slice. The repo **already contains a committed spec
   pinning 5 s**; and finding **C2** means the tick and the rate model must land in the **same** slice or damage
   scales by the shortening factor.
3. **Q1 — ruling #21 (capture transfer).** Left **OPEN** as instructed. The decision aid is §8 — 20 rows of what
   capture moves / destroys / ignores today, each with `file:line`.

**Recommended first BUILD, once you give the go:** **S1 (the ground battle log)**, then **S1c (sim-health gauges)**.
Both cheap-wire. The argument is not preference: a ground battle **already halts your clock and says nothing**, and
a dead simulation currently reads as "paused" on every instrument. **Fix the windows before building more room.**

### 15f. Live obligations carried forward (do not lose these when this doc is next read)

1. **Phase C must be re-swept against late evidence.** Phases ran out of order (A partial → C → D → E → A resumed),
   so some doc corrections were made against **incomplete** evidence. Anything the A4/A5/Phase-B passes changed
   needs a second look at the docs it touched.
2. **Five Phase-B verdicts still owed** — §14d, led by **#14 "the only fully-wired move verb"** (it sizes a *large*
   slice and the ruling says **DELETE** that verb).
3. **Four Phase-A batches never ran** — §15c. Carried as an explicit, costed deferral.
4. **Definition of Done row 3** (*"the doc tree contains no claim a grep of the code refutes"*) is **true for
   everything swept, not provable tree-wide** — the un-run batches are the unswept remainder. Stated as a limit,
   never as a pass.

---

## 16. THE PHASE C RE-SWEEP (the obligation from §15f) — 2026-07-28

Phase C ran **before** Phase A finished, so doc corrections were made against incomplete evidence. This is the
second pass, applying **Phase B's own standing lesson** (§14d) systematically: *when a verdict lands, grep the doc
tree for every OTHER place that states the same thing.* Four late findings were traced; **three had propagated
nowhere and one had propagated a dangerous claim into six documents.**

### 16a. Stale numbers still sitting in THE PLAN — fixed

Ruling #11's footprint denominator was corrected in the audit but **not** in THE PLAN, where it appeared **twice**
(the delta ledger row and the S-slice body): *"2 of 26"* → **2 of 51** installation templates. Exactly the
half-applied-fix pattern Phase B named. Residual grep for `2 of 26`: **0**.

### 16b. ⭐ THE BIG ONE — a design DECISION recorded as an accomplished FACT, echoed into six docs

`docs/combat/DETECTION-DESIGN.md:5` is a dated **decision** banner: cut `SignalQuality`, collapse detection to
**strength only**. It states the field *"is deleted"* and claims the cut *"deletes three bugs by deletion."*
**The decision was never executed in code.** Verified:

| The banner says | The code says |
|---|---|
| the field *"is deleted"* | **live in 9 files** |
| its *"only consumer was a redundant survey body-ID path"* | that consumer is **load-bearing**: survey reveal gates on it at hard thresholds — body type `> 0.20`, tectonics/star detail `> 0.80` (`SystemBodyInfoDB.cs:154-160`, `StarInfoDB.cs:130`), with `RndSigmoid` noise scaled by it |
| *"deletes three bugs by deletion (byte-overflow, multi-band overwrite, range-invariance)"* | the **byte-overflow was already FIXED** 2026-06-28 and is CI-gauged (`SensorQualityTests`: a perfectly-tuned signal resolves at 1.0; pre-fix ~0.74, a wrapped byte). So it is no longer an argument for deletion. The **multi-band overwrite is still open**, as a flagged bounded follow-up |

**Why this is dangerous rather than untidy:** read literally, the banner authorises deleting a live field that
controls how accurate the player's surveys are. Six documents echoed *"CUT"* — `DIPLOMACY-DESIGN` (fixed in Phase B),
`ESPIONAGE-AND-INTELLIGENCE-DESIGN`, `GOVERNANCE-AND-DELEGATION-DESIGN`, `AI-BRAIN-BUILD-TRACKER`, three
`DOCS-INDEX` rows, and `COMPONENT-DESIGNER-DIAL-LEDGER`. **Exactly one got it right** —
`COMPONENT-DESIGNER-DIALS.md:1489`: *"`SignalQuality` is DESIGN-CUT (**the engine field survives**, but detection
collapses to strength only)."* That is the correct, safe phrasing and it is now the model. The origin banner and the
two unqualified echoes are corrected; the right one was left alone.

### 16c. ⭐⭐ AND THE CONSEQUENCE NOBODY TRACED — the root cause of the blind AI, proven

The re-sweep's real payoff. **Two facts, both proven in source:**

1. **`SignalStrength_kW` is not loudness — it is a detection MARGIN.** Both assignment sites subtract the
   receiver's own noise floor: `SensorTools.cs:193` (`intersectPointY - recever.BestSensitivity_kW`) and `:197`
   (`signalWaveSpectraMagnatude_kW - recever.BestSensitivity_kW`). A ship at realistic range clears the floor by
   almost nothing ⇒ **~0**; a star clears it by a vast amount ⇒ **1.4 M kW**. **That is the entire observed
   symptom** — not a wrapped byte, not an uninitialised field.
2. **`ThreatAssessment.cs:39` sums that margin believing it is size**, in its own words: *"loudness = the
   fog-limited size proxy."*

**And the doc chain that caused it, which is the part worth keeping:** `DETECTION-DESIGN.md` decided detection
would *"collapse to strength only"* → `AI-BRAIN-BUILD-TRACKER.md` (F-A1/F-B1) duly built the AI's **eyes** on
**strength** → `ThreatAssessment` sums strength as a size proxy → strength is a threshold margin. **The design
deliberately routed all NPC threat perception onto the one field whose semantics cannot carry it.** That is why
**all 288 AI decisions in the real play log read `vs no threat`**, and why `CombatRisk.WouldEngage` — which
returns `true` whenever the estimate is non-positive (`CombatRisk.cs:41`) — never evaluates its risk band at all.

**A compounding factor, already documented and still open:** the per-band loop **overwrites** both
`detectedMagnatude` and `quality` each iteration and returns whatever the **last** detectable band left
(`SensorTools.cs:218-222`) — the "multi-band overwrite" quirk flagged in `Sensors/CLAUDE.md`. A marginal band can
clobber a strong one. `HighestDetectionQuality` (a max over time) smooths it; `LatestDetectionQuality` does not —
**which is exactly why Latest reads 0 while Highest does not**, the asymmetry S1e was written to explain.

**Consequences landed:** slice **S1e** changes shape from *"trace an unverified bug"* to a **design decision** —
give the contact an explicit loudness field from the **pre-subtraction** `signalWaveSpectraMagnatude_kW` (leaving
the margin semantics untouched so nothing else shifts), and treat max-across-bands as a separate
behaviour-changing fix with its own test. `AI-BRAIN-BUILD-TRACKER`'s **F-B1 is now marked blocked on S1e**. The
connection map's threat-read row carries the mechanism. And a standing warning is on the slice: **do not "fix"
this by deleting `SignalQuality`.**

### 16d. What the re-sweep did NOT find

- **Ruling #25's walk-back had propagated correctly** — every remaining "no tooltips" mention is a corrected one.
- **The keystone-3 / hostility correction** needed no further sites beyond the two fixed in Phase B.
- **The `SYSTEMS-STATUS-AND-TEST-PLAN` archive** holds: residual grep still **0** after all later commits.

### 16e. The lesson, sharpened

Phase B's lesson was *"a half-applied fix is indistinguishable from a wrong verdict."* This pass adds a second,
worse failure mode: **a design DECISION written in the past tense reads as an accomplished fact, and then
propagates.** `DETECTION-DESIGN.md` said the field *"is deleted"* when it meant *"we have decided to delete it"* —
and six documents inherited it, one of which pointed the entire NPC brain at a field that cannot do the job.
**Write decisions as decisions.** When a doc records intent, say *"decided, not yet built"*; the three-state
vocabulary this operation already uses (built-and-gauged / built-but-runtime-unverified / built-but-INERT) needs a
fourth state for exactly this: **DECIDED-NOT-BUILT**.

---

## 17. PHASE B — B1 RESOLVED: the highest-value owed verdict, and it makes slice S6 SMALLER

The one verdict §14d flagged as most expensive to get wrong: *"`march-to-region` is the ONLY fully-wired planetary
move verb"* — because ruling **#14 says DELETE that verb**, and deleting the only working one is a serious call.
It had rested on a single agent since Phase A. **Resolved 2026-07-28. Verdict: CONFIRMED in substance, MATERIALLY
OVERSTATED in its quantifier — and the correction is good news.**

### 17a. What is actually wired, end by end

| End | `MoveToRegion` | `MoveToHex` |
|---|---|---|
| Order-enum member | ✅ `GroundForcesDB.cs:293` | ✅ `:292` (*"march to a **GLOBAL** planetary-grid hex"*) |
| Factory | ✅ `MoveRegion(region)` `:343` | ✅ `MoveHex(q,r)` `:342` |
| Readout formatter | ✅ `:356` `"→ region N"` | ✅ `:355` `"→ hex (q,r)"` |
| **Processor executes it** | ✅ `GroundForcesProcessor.cs:913` | ✅ **`:918`** |
| **Client ISSUES it** | ✅ `PlanetViewWindow.cs:1442,1445` (two buttons) | ❌ **no button anywhere** |
| **Client RENDERS it** | — | ✅ **`PlanetViewWindow.cs:505`** draws each queued waypoint as a dot on the path overlay (`:477`: *"`GlobalPath` → each queued `MoveToHex` waypoint"*) |
| **AI issues it** | ✅ `GroundTacticalBrain.cs:205`, marked `Ai` | ❌ none |
| Save/load fixture | ✅ `MidCampaignSaveLoadTests` | ❌ none |

### 17b. The verdict, precisely

- **CONFIRMED:** `MoveToRegion` is the only verb wired **end-to-end** — issuer → processor → readout → save.
- **REFUTED as written:** *"the ONLY fully-wired move verb"* implies the alternative is absent. It is not.
  **`MoveToHex` is wired at four of seven ends** — enum, factory, formatter, **processor execution**, and the
  client's **path rendering**. What it lacks is **ISSUERS**: no client button, no AI call, no test.
- **CONFIRMED:** **four coordinate systems** coexist on the ground state — `RegionIndex` (`:46`), `HexQ/HexR`
  (`:155,157`), `GlobalQ/GlobalR` (`:174,176`, defaulting to `-1`), `MiniQ/MiniR` (`:185,187`) — plus the order's
  own `TargetQ/TargetR` vs `TargetRegion` split (`:328-330`).
- **CONFIRMED:** **no formatter prints a combined two-layer coordinate.** The two that exist print one layer each
  (`"→ hex (q,r)"`, `"→ region N"`). Nothing renders the `(17,09)(22,47)` shape ruling #14 asks for.

### 17c. Why this makes S6 smaller — the load-bearing consequence

The old reading of #14 was *"delete the only working move verb,"* which sizes S6 as **build a replacement movement
system, then switch to it.** That is wrong. **The replacement's hard half already exists:** `MoveToHex` has its
primitive, its **processor execution**, and — the part nobody had noticed — the client already **draws its
waypoint path**, on **global cylinder coordinates**, which is precisely the *global* half of the two-layer scheme
#14 asks for.

So #14 is really three smaller pieces:
1. **Wire `MoveToHex`'s issuers** — a client button (the city/planet view already renders the result) and an AI
   call in `GroundTacticalBrain` beside the existing `MoveRegion(…)`.
2. **Add the two-layer formatter** — genuinely absent, and cheap: one function, then point both readouts at it.
3. **Then** retire `MoveToRegion` (with its AI and client issuers migrated) — last, not first.

**⇒ S6 should be RE-SIZED and RE-ORDERED**: it is not one large slice but a cheap-wire issuer/formatter slice
followed by a retirement. And a caution for whoever builds it: **`MoveToRegion` currently carries the only
save/load coverage of ground movement**, so retiring it without moving that fixture to `MoveToHex` drops the only
gauge watching movement survive a save.

*(Distinct symbol, do not conflate: the separately-recorded `OrderFormationTreeMoveToHex` — a formation-**tree**
variant — does have zero callers. That is not the `MoveToHex` order type audited here.)*

### 17d. Phase B's remaining residue after this

Four lower-stakes verdicts still stand on round-1/agent evidence: §10 #1's five sub-claims ("Pulsar has no ground
combat at all"), the `WEAPONS-DESIGN` saturation ship-only scoping, the `REAL-DISTANCE` STALE header, and
`GroundCombat/CLAUDE.md:52`'s OVERSTATED garrison note. All four are **doc-scoping** verdicts whose failure mode is
a mis-worded doc, not a mis-sized build — which is why they rank below B1 and are carried, not rushed.

---

## 18. PHASE B COMPLETE — the last four verdicts, and the one that re-shapes S8

The four doc-scoping verdicts §17d carried. **All four hold** — but one is materially overstated, and correcting it
found that **ruling #23's rate model already exists**. Phase B's residue is now **zero**.

### 18a. V1 — *"Pulsar has no ground combat at all"* (aurora seed #1) → ✅ **CONFIRMED REFUTED**

Each of the five sub-claims of absence checked individually against the tree:

| Sub-claim | Reality |
|---|---|
| "no ground unit entity" | `GroundCombat/GroundUnitEntity.cs` exists |
| "no formation concept" | **2** `class GroundFormation` declarations in `GroundForcesDB.cs` |
| "no ground combat processor" | `GroundForcesProcessor : IHotloopProcessor`, `RunFrequency` = 1 hour (`:27,29`) |
| "no invasion order" | both `LoadTroopsOrder.cs` **and** `LandTroopsOrder.cs` exist |
| "no UI" | `Pulsar4X.Client/.../PlanetViewWindow.cs` exists |

**One number tightened:** the correction banner says *"a ~56-file ground subsystem"*; the actual count is **55**
`.cs` files. Within the tilde, but this operation is about precision — fixed.

### 18b. V4 — the garrison note *"so a fresh New Game has ground units"* → ✅ **CONFIRMED OVERSTATED**

`NewGameMenu.cs:55` reads exactly `public static bool AutoRaiseHomeGarrison = false;` under the comment
*"BAREBONES: no default home garrison."* A stock New Game raises **none**, and ruling **#27b** makes that default
deliberate and permanent. The only thing that flips it is the **DevTest** start (`:979-986`). Correction stands as
written.

### 18c. V3 — the `REAL-DISTANCE` header's *"slices 2–5 planned"* → ✅ **CONFIRMED STALE**

The header now records it accurately, including three details worth keeping: slice 2's **behaviour shipped under a
different name** (**K1** put `Range_m` on `GroundWeaponAtb` with 5 base-mod templates — melee 0 / rifle 500 /
autocannon 2000 / cannon 4000 / energy 20000; **K3** added the metre gate at `GroundForcesProcessor.cs:568`); it
shipped behind **`EnableMiniHexCombat`**, *not* the `EnableGroundRealRange` this doc proposed; and it **bypassed the
doc's own `RealRangeKmFor` seam**. Flag state: **OFF in CI, ON for menu games** (`NewGameMenu.cs:580,985`).

### 18d. ⭐ V2 — the saturation scoping → **CONFIRMED for saturation, REFUTED for "no rate dial at all"**

**And I nearly got this wrong the same way twice.** Grepping `RateOfFire` / `FireRate` / `Cadence` /
`ShotsPerSecond` returned **zero hits on both sides** — which reads as "no rate model anywhere." The code spells it
**`RoundsPerSecond`**. *Third time the negative-grep rule earned its place in this operation.*

**What holds:** saturation on the **ship** side genuinely is **derived from rate of fire** —
`Saturation = RoundsPerSecond × PelletsPerShot` (`FlakWeaponAtb.cs:35`, applied at `ShipCombatValueDB.cs:395`),
against a `SaturationReference = 50.0` floor (`CombatKernel.cs:42,186`). And **ground saturation is two hardcoded
category constants** — `AreaSaturation = 100_000.0` and `PointSaturation = 1.0` (`GroundCombatant.cs:40,44`),
selected by weapon category at `:75,78,81,105`. Nothing on the ground side derives saturation from a rate. ✅

**What is REFUTED: "ground has no rate dial at all."** It does — for any weapon that comes from the shared designer:

| Where | What it computes |
|---|---|
| `GroundCombat/SpaceWeaponGround.cs:64` | `KineticEnergyPerShot_J * RoundsPerSecond` |
| `:69` | `DamagePerPellet_J * PelletsPerShot * RoundsPerSecond` |
| `:74`, `:79` | `EnergyPerShot_J * RoundsPerSecond` |
| `GroundCombat/WeaponSupply.cs:85,90,95` | the same product, to size the **reactor draw** |

Those are **true damage-per-second figures, computed inside the `GroundCombat/` folder.** And the rates themselves
are **already authored in the base mod** — `weapons.json` describes the dial in its own words: *"Rate of fire.
Drives both damage/sec and saturation"* (`:394`, `:1036`), *"Bursts per second… sets saturation"* (`:560`),
*"Saturation = rounds/sec × pellets/shot"* (`:570`).

### 18e. ⭐⭐ The consequence — ruling #23's rate model ALREADY EXISTS, so S8 is a CONNECT, not an invention

`GroundForcesProcessor` has **zero** references to `SpaceWeaponGround` or any per-second figure. It resolves damage
as flat **`Attack × SalvoScale` per tick** (`:491`). So:

> **The per-second value the ground resolver needs is already computed, in the same folder, from data already
> authored in the base mod — and the resolver ignores it.**

**This re-shapes S8** (*"the tick and the rate model, in ONE slice"* — sized **large**):
- **Not needed:** designing a rate model, choosing per-weapon rates, or adding a rate field. `RoundsPerSecond`
  exists, is authored on the real templates, and already yields DPS for ground-mounted designer weapons.
- **Needed:** make the resolver **read** the per-second value instead of flat `Attack`, integrate it over the
  elapsed tick, and audit every other per-tick term for `deltaSeconds` scaling in the same change (finding **C2** —
  still the hard part, and still why the tick change and the rate model cannot be separate slices).
- **The real remaining gap is the units WITHOUT a designer weapon** — garrison and base-mod units built from a flat
  `Attack` with no `RoundsPerSecond` behind it. Those need a rate *assigned*, and that is a much smaller question
  than "design the rate model."

**Developer question Q5 is therefore partly ANSWERED, not open:** *"#23 makes every weapon carry damage-per-second.
Nobody has picked the numbers."* — the numbers **are picked** for designer weapons (authored in `weapons.json`,
with the JSON's own description saying they drive damage/sec). Q5 narrows to: **what rate do the flat-`Attack`
garrison/base-mod units get?** The plan's own cheapest-honest-route answer (derive from today's `Attack` ÷ the
chosen tick, so the first build is behaviour-identical) applies to exactly that residue.

### 18f. Phase B — final tally

**22 distinct claims verdicted across three rounds. Residue: zero.**

| Outcome | Count | Which |
|---|---|---|
| 🔴 **REFUTED outright** | **1** | ruling #25 / "zero tooltips anywhere" (§14b) — the walk-back |
| 🟠 **Refuted in its quantifier** (substance held) | **2** | #14 "the ONLY fully-wired move verb" (§17); V2 "ground has no rate dial at all" (§18d) |
| ✅ **CONFIRMED** | **19** | of which **11** were sharpened, corrected or found *stronger* than written |
| ➕ **My own framings withdrawn** | **2** | the keystone-3 "changes a build order" overreach (N2); the `GroundUnitAtb` exact-arity claim |

**Three slices re-sized by Phase B, all downward:** #25 `medium → cheap-wire` · **S6** `large → cheap-wire +
retirement` · **S8** *"build the rate model"* → *connect the one that exists* (the `deltaSeconds` audit remains the
large part).

**The method lesson, final form.** Every one of the three quantifier failures — *"zero tooltips anywhere,"* *"the
ONLY fully-wired verb,"* *"no rate dial at all"* — was an **absolute word** attached to a claim that was
directionally right. And two of the three were caught only because a **negative grep was re-run under other
spellings** (`chips` not `hazard chip`; `RoundsPerSecond` not `RateOfFire`). **Absolutes and negative greps are this
codebase's two most reliable sources of wrong findings.**
