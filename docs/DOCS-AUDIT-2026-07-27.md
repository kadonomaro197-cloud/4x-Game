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
| **Moved** by the subfolder reorg | 13 paths | `docs/AI-BRAIN-BUILD-TRACKER.md` (61 sites!) → `docs/ai/…`; `docs/SITE-ENGINE-DESIGN.md` (35) → `docs/explore/…`; `docs/DIPLOMACY-DESIGN.md` (17) → `docs/society/…` |
| **Consolidated** into a survivor | 10 paths | `docs/GROUND-COMBAT-MAP-DESIGN.md` (27), `GROUND-CITY-AND-WARMAP-DESIGN.md` (13), `HEX-GROUND-AND-ORDERS-DESIGN.md` (9), `GLOBAL-HEX-GRID-DESIGN.md` (9) → `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`; `WEAPON-TAXONOMY` + `WEAPONS-AND-DODGE` (23) → `docs/combat/WEAPONS-DESIGN.md`; `AI-MEANS-ENDS-PLANNER` + `AI-OBJECTIVE-ENGINE` (23) → `docs/ai/AI-DECISION-ENGINE-DESIGN.md`; `RESOLVER-MERGE` (6) → `docs/combat/RESOLVER-DESIGN.md`; `SPACE-STATIONS` (8) → `docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` |
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

### Also confirmed working (worth knowing)

- **Detection + fog are genuinely healthy at runtime:** 179 `[DETECT]` + 679 `[DETECT-CONTACT]` lines,
  contacts 2→5, detected 1-of-8 → 4-of-8, and **all 679 contacts read `src=LAGGED` with zero `LIVE`** and real
  fog lag up to ~90,000 km — the 2026-07-17 scan-snapshot fix is confirmed live.
- One `[PERF]` 2117 ms frame at startup, correctly below the hang threshold.
- 24 missing `Resources\*.bmp` at boot: the texture pre-load runs as the first statement of the
  `PulsarMainWindow` constructor, ~90 lines **before** `ResourcesPath` is combined with the exe directory.

---

## 10. THE COMPLETE A2 SEED-SWEEP LEDGER (orders §8: *"the dated audit doc records the sweep"*)

All 13 named seed items, plus what the sweep turned up beyond them. **Three seed claims were themselves wrong**
— recorded here so a future pass doesn't re-hunt them.

| # | Seed item | Verdict | Where the fix landed |
|---|---|---|---|
| 1 | `docs/aurora/GROUND-COMBAT.md:6` — "Pulsar has no ground combat at all" | **REFUTED** (all five sub-claims false vs a ~56-file subsystem) | correction banner + pointer to the as-built subsystem |
| 2 | `MVP.md` + `PLAY-TO-MARS` — the invade-from-orbit panel is the #1 blocker | **REFUTED** — built 2026-07-19 (`FleetWindow.cs:1756,1814`; AI at `ConquerResolver.cs:63`) | §L rewritten ✅; MVP row D + Stage 4 **re-pointed** at the 3 real gaps |
| 3 | `SYSTEMS-STATUS-AND-TEST-PLAN.md` retirement vs root `CLAUDE.md` mandating it | **CONFIRMED** (4 mandates vs 1 retirement note, same file) | retirement finished; 4 refs repointed; **then archived** (§8-equivalent, see the compliance doc §8) |
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
| `REAL-DISTANCE-COMBAT-DESIGN` header — "slices 2–5 planned" | **STALE** — slice 2 shipped as K1+K3 under a different flag; two more claims refuted | header corrected |
| `GROUND-UNIT-VARIABLES` — 5 stale `GroundForcesProcessor.cs` line refs + "multi-weapon plurality missing" | **REFUTED** — the W-track built it (`:472` loops the loadout, range-gating each weapon) | drift table + row flipped |

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
