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

## 5. Resume — everything the next pass needs is already on disk

The 19 assignment briefs and the shared briefing survive at
`/tmp/claude-0/-home-user-4x-Game/<session>/scratchpad/` (`BRIEFING.md`, `assignments/<id>.md`,
`run-evidence.js`). If that scratch is gone, regenerate from `OPERATION-GROUND-TRUTH-PROMPT.md` §2.

| Batch | Assignments | State |
|---|---|---|
| A1 log forensics | `A1a-timeline`, `A1b-failures`, `A1c-combat`, `A1d-ai-silent` | ❌ died on usage limit |
| A2 doc verification | `A2a-docs-ground`, `A2b-docs-combat`, `A2c-docs-subsystems`, `A2d-docs-client-tests`, `A2e-docs-dashboards`, `A2f-scale-comments` | ❌ died — **partially covered solo in §4** |
| A3 reachability walls | `A3a-start-state`, `A3b-designer-wall`, `A3c-build-queue-wall`, `A3d-movement-verbs-wall`, `A3e-observability-wall` | ❌ died — **partially covered solo in §3** |
| A4 rulings matrix | `A4a-rulings-1-9`, `A4b-rulings-10-18`, `A4c-rulings-19-27` | ❌ died — not started |
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
