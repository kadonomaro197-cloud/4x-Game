# OPERATION GROUND TRUTH — the handoff prompt

**Give this whole file to Opus 5 with ULTRACODE ON.** Written 2026-07-27 by the prior session (the one that recorded
the developer's 27 ground-gameplay rulings and built doctrine slices D1/D1b). It is self-contained: everything you
need is either summarized inline or named by exact path on the branch below.

---

## YOUR MISSION (one paragraph)

The last full survey put planetary/ground gameplay at **~85% built engine-side, ~30% reachable by a player, and 0%
observable** — a deep, CI-tested engine behind a nailed-shut front door with no window to watch through. Your job, in
order: (1) **verify and reconcile every planetary/ground/combat document against the code and against the developer's
recorded rulings** — consolidate, correct, and DELETE what is no longer valid (deletion-vs-archive rules and the link-sweep precedent live in Phase C — no doc is touched before Phase C); (2) **do forensics on the developer's
actual play-session logs** to find what really ran, what failed, and what stayed silent; (3) produce **THE PLAN** — an
ordered, gauged, slice-by-slice plan that makes everything planned for planetary gameplay **FUNCTIONAL** (works and is
CI-gauged), **ACCESSIBLE** (a player reaches it from the normal game, not DevTools), and **OBSERVABLE** (you can watch
it happen); and (4) **culminate by AUTHORING THE WORKFLOW** — a saved, re-runnable Workflow script that executes that
plan slice by slice. Doc work (1–3) you execute now. The build workflow (4) you author, validate, and present — the
developer gives the go before it runs. Use as many subagents as the work needs; token cost is not a constraint.

---

## 0. SESSION SETUP — do these before anything else

> ### ✅ SECTION 0: ALL FOUR VERIFIED DONE (checked 2026-07-27)
>
> | # | Requirement | Verified how |
> |---|---|---|
> | 0.1 | Start the branch FROM `claude/faction-design-audit-bb3tqz`; merge `main` if it is ahead | ✅ `git merge-base --is-ancestor` confirms HEAD contains **both** the audit tip **and** `origin/main`. The count came back **55 / 0** — the audit branch was already 55 ahead with `main` fully contained, **so no merge was needed**. No force-push was needed either; the first push created the branch cleanly. |
> | 0.2 | Read root `CLAUDE.md` in full | ✅ read; also confirmed byte-identical to `main`'s copy, so nothing branch-specific was missed. **Note: the Landmine Index is now L1–L12, not L1–L11** — this session added **L12** (`BaseDataBlob.Clone()` is virtual with a garbage default, so a `*DB` that forgets `Clone()` silently becomes a bare `object`). |
> | 0.3 | Check CI on `b218acf` + `255bc52` | ✅ **both green.** `b218acf` `conclusion: success`; `255bc52` green on **all 7 jobs** (6 test shards + `build-client`). Nothing was stacked on a red base. |
> | 0.4 | Know the constraints | ✅ observed. **21 of 21 commits carry both required trailers** (verified by count). The multiple-choice question tool was **never called** — all questions asked in prose (plan §0 Q1–Q5). **⚠ TWO CORRECTIONS TO THIS SECTION'S OWN NUMBERS:** CI is **~33 minutes, not ~13** (the `rest` shard is the critical path; measured 2 / 4 / 8 / 11 / 13 / **33** min + 0.5 for `build-client`), and `ci.yml`'s complement filter means every **new** fixture lands in that slowest shard by default. The 6-way sharding and the client-can't-run rule are both confirmed exactly as written. |


1. **Get the branch.** Everything below lives on `claude/faction-design-audit-bb3tqz`, NOT the default branch:
   `git fetch origin claude/faction-design-audit-bb3tqz` and start your session's designated branch FROM it
   (`git checkout -B <your-branch> origin/claude/faction-design-audit-bb3tqz`). If you skip this, half the documents
   and code this prompt references will not exist. Then run `git rev-list --left-right --count origin/claude/faction-design-audit-bb3tqz...origin/main` — if main has commits the audit branch lacks, MERGE origin/main into your branch before Phase A ("code at HEAD" must mean the real latest). If your first push is rejected non-fast-forward because the harness pre-created your branch from a newer main, use `git push --force-with-lease` after confirming your branch contains the audit tip.
2. **Read root `CLAUDE.md` in full.** The six-step pre-flight, the Landmine Index (L1–L11), the Prime Directive
   (map connections first), the Visibility Gate ("can we see enough?"), and the doc-upkeep rules are all binding.
3. **Check CI on the two commits that were pending at handoff:** `b218acf` (doctrine D1b — the 25-doctrine catalog +
   the client domain filter) and `255bc52` (docs). `97ecd5b` (D1) is confirmed green. As of prompt-writing (2026-07-27) BOTH had gone green on all checks — expect this to resolve instantly; if it doesn't, that's real news. If anything at your tip is red, diagnose and fix it FIRST — nothing stacks on a red base. (`gh` is NOT installed in this container — check CI via the GitHub MCP actions tools, e.g. `actions_list` on `ci.yml` filtered to the branch.)
4. **Know your constraints.** No .NET SDK in the container — **CI is the only compile/test gauge** (~13 min, sharded
   6 ways; a new heavy test fixture lands in the `rest` shard). CI compiles the client (`build-client` job) but can
   NEVER run it — runtime behaviour is verified only on the developer's Windows machine via `launch.bat` +
   `game_logs/`. One slice per push; wait for ALL checks green — the SIX test shards AND `build-client`; one red shard reds the push. Commits end with the two trailers your
   session's rules specify. **Communication:** the developer is a US Navy nuclear-trained machinist's mate — plain
   English, mechanical/shipboard analogies, define jargon on first use, lead with the point. NEVER use the
   multiple-choice question tool — it is broken in this environment (root `CLAUDE.md`, confirmed by the developer; calling it wastes a turn). Ask in prose, pick sensible defaults, state them, and proceed.

---

## 1. CANON — the developer's decisions (these override every older document)

> ### ✅ SECTION 1: CANON READ AND OBEYED — with one gap this slow walk caught (2026-07-27)
>
> **The rulings were obeyed.** #21 (capture transfer) was **never decided** — only inventoried as a decision aid
> (audit §8). #27b was actively **defended**: the planetary audit's P1 recommended defaulting the home garrison
> ON, which the ruling forbids, so P1 was re-pointed rather than followed. Where a canon doc's *factual notes*
> disagreed with the code, the **notes** were corrected and the **rulings left untouched** (4 corrections, in
> `GROUND-GAMEPLAY-DECISIONS`'s Consequences section).
>
> | Canon doc | Read? |
> |---|---|
> | `GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` | ✅ in full (252 lines) — factual notes corrected |
> | `PLANETARY-GAMEPLAY-AUDIT-2026-07-24.md` | ✅ in full (259 lines) — 2 stale sections reconciled |
> | `GROUND-SURFACE-MAP-DESIGN.md` | ⚠ **partially** — Layers 4/5/6 + the scale sections read directly; not all 560 lines |
> | `SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` | ❌ **NOT READ until this walk-through — the real gap. See below.** |
> | `REAL-DISTANCE-COMBAT-DESIGN.md` | ⚠ partially direct + fully swept by agent A2b |
> | `UNIFIED-RESOLVER-AND-BATTLE-STATS.md` | ⚠ partially direct + fully swept by agent A2b |
> | `GROUND-UNIT-VARIABLES.md` | ⚠ partially (targeted) — and corrected |
> | `DOCS-INDEX.md` + `TESTING-TRACKER.md` | ✅ read and updated throughout |
>
> **⚠ THE GAP, AND WHAT IT COST.** `SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` was never read, yet THE PLAN
> still issued a recommendation about its G1–G6 build order — *"defer the whole track."* Reading it **overturned
> that**, because its own EXISTS/MISSING ledger shows most of the substrate is already built:
> - **G1 (the spec-file writer) is CHEAP and should be pulled forward** — the file *format* (`SystemBlueprint`)
>   already exists and is what a real New Game loads; only the **writer** is missing (verified: no
>   `File.WriteAllText`/`SerializeObject` anywhere in `GameEngine/Galaxy/`). Additive and gauge-first.
> - **G2 is a concrete content BUG, not a design want** — `GenerateAsteroidBelt` has **exactly one caller**, on
>   the *authored* path, so **procedurally generated systems get no belts at all.**
> - **G3–G6 stay deferred**, with **G6 scheduled alongside M4** (a rich terrain display over a uniform generator
>   is a lie).
>
> **Lesson worth keeping: a recommendation about a doc you have not read is a guess wearing a verdict's clothes.**
> THE PLAN is corrected.

Read these first. Where any other doc disagrees with them, the other doc is wrong.

| Doc | What it holds |
|---|---|
| `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` | **THE governing rulings** — 26 of 27 questions answered, the doctrine build plan (D1→D3b), the fire-rate correction, the tick-calibration problem |
| `docs/ground/PLANETARY-GAMEPLAY-AUDIT-2026-07-24.md` | The holistic audit — status board, the four loop breaks, P1–P7 roadmap, surface-map status table |
| `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` | THE single surface design, all three zooms. Layer 5 = the locked mini-hex tactical model; Layer 6 = the developer's display/naming requirements (hover-reveal, glyphs, city marker, battalion stacks, click-to-name, weather at both zooms) + the "nothing is impossible, it's just costs" law |
| `docs/environment/SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` | LOCKED: generation + the spec-file persistence (5 decisions: hybrid recipe/frozen · written first time · per-save-vs-library at galaxy setup · hand-editable · surfaces on demand) + build order G1–G6 |
| `docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md` | LOCKED: real km on the weapon are the truth; hexes are a display ruler |
| `docs/combat/UNIFIED-RESOLVER-AND-BATTLE-STATS.md` | Part B designs the `CasualtyTier` wound model + the battle-stats ledger — ruling #22 says this is DECIDED; build it, never redesign it |
| `docs/ground/GROUND-UNIT-VARIABLES.md` | The Training/veterancy dial design (partially built) |
| `docs/DOCS-INDEX.md` + `docs/TESTING-TRACKER.md` | The doc dashboard (update rows in the SAME commit as any doc change) and the test ledger |

**The 27 rulings, compact** (full text + context in the DECISIONS doc — trust that doc where this table is terse):

1. **ONE designer for everything; the three prebuilt unit templates are DELETED.** Forced order: #2 → #4 → #1
   (the prebuilts are today the only carrier of penetration/per-shot-energy — dials first, then delete), plus a garrison-composition replacement: the prebuilts are what `GroundStartGarrison` raises and are referenced from the base mod and several tests.
2. Penetration + Per-Shot-Energy are **WEAPON** properties, not unit properties.
3. An invalid design (over carry budget / unpowered / no magazine) is **blocked from SAVING**.
4. Ground parts **cost research, scaling with complexity** (today 17/22 templates cost 0 and all are start-unlocked).
5. **Every hazard gets a counter, and the component depends on the hazard** (a dust storm wants better radar, and it
   also slows movement and hurts accuracy — counters are specific gear, not a generic resistance %).
6. **Rally point = a building setting**, for space AND ground (planetary coordinate / particular orbit). Kills the
   hardcoded region-0 muster.
7. **Ground units cost PEOPLE, and there is no return — they die.** (Connects ground to population for the first time;
   `CrewReq` is already declared on templates and never read.)
8. Units always carry upkeep + a magazine; **ammo must bite** (needs per-weapon consumption — today it's a flat
   1 kg/salvo, so magazines are never a trade).
9. **NOTHING IS FREE.** The costed tile queue is canonical; the free "Build here" path dies.
10. **ONE RTS-style build queue for everything** (units + buildings), every entry carrying its destination, with
    visible progress. There is no "location hook" question — one path, so nothing can diverge.
11. **Every building occupies ground**, and "occupies a tile" and "is a war-map objective" are the SAME attribute.
12. **Fund employment + power** (both engine-wired, both data-zero today).
13. Tile bonuses are **semantic** — a food building on grassland earns its bonus *because it makes sense*; different
    components get different bonuses by tile type.
14. **DELETE "march to region" entirely.** Planetary movement uses a **two-layer coordinate** written like
    `(17,09)(22,47)` — regional hex + mini hex, one address.
15. **A fight is COMMITTED.** Once the resolver starts it ends only when one side is wiped or calls **RETREAT**, which
    starts a **break-away timer** (~1–2 h). **Pursuit is a doctrine the opponent chooses.** All exit pricing lives in
    doctrine entries, never hardcoded.
16. Mini-hex movement works **just like regional movement** (same verb, finer layer).
17. While marching, show **all four**: destination, distance remaining, ETA, current speed.
18. Target selection: **yes, the player — but gated by the battalion's DOCTRINE.**
19. Battle is scoped to **REAL DISTANCE** (not the region band); the engage decision comes via orders/doctrine.
20. Battalion composition works **the same way fleet composition does.**
21. **What a planet capture transfers: STILL OPEN. Do NOT decide it. Ask the developer in prose.**
22. Wound/casualty model: **already designed** (CasualtyTier + damage ledger + Training dial). Build, don't redesign.
23. **Fire rate is CALCULATED**: a weapon carries a rate (damage/second); the resolver integrates it over the tick
    against available targets and resolves who died. Applies to ALL weapons. **Prerequisite ruled 2026-07-27:
    SHORTEN the combat tick** (ground runs hourly; 10 dps × 3600 s = 36,000 damage/tick otherwise; space's 5 s
    `CombatReactionStep` is the precedent). The ruling came AFTER the DECISIONS doc's calibration section was
    written; that section is updated as of this prompt's commit — only the tick VALUE is still open. The mid-tick
    overkill semantics ("sequential down the doctrine's priority list") is an INTERPRETATION of the developer's
    phrasing, not a ruling — confirm it in the open-questions prose (it ties to ruling #18).
24. Ground battle readout: **a LOG, first.** (The ground COMBAT path — `GroundForcesProcessor`/`ResolveRegionCombat` — emits zero log lines, zero events, zero battle records; only the C5.1 troop load/land orders publish anything. The single worst observability hole in the game. The DECISIONS doc carries the older absolute wording — qualify it there during Phase C.)
25. Ownership drawing: later. Unit inspection: **hover tooltip on the map + the same detail in Force Management.**
26. A lost contact leaves a **fading last-known** marker (like space).
27. (a) The five ground behaviour flags move **into the SAVE** (today they're process statics set only by New Game —
    a LOADED save plays differently than a fresh one, and CI tests a configuration no player runs).
    (b) **NO** default garrison/enemy in a stock New Game. (Open follow-up to raise with the developer: a deliberate
    scenario/skirmish start as the alternative, since without one the ground layer is only exercisable via DevTools.)

**Doctrine — the frame that governs D2/D3 (developer, 2026-07-27):** a doctrine is a **named posture assigned to a
formation or sub-fleet in formation management** — not sliders, not a strategic setting. **Once the auto-resolver
starts, the doctrine controls how those units move and fight. Everything goes through the doctrines.** A doctrine CAN be switched mid-fight — doctrine changes are a
direct call that deliberately bypasses the engagement lock; the player's only other in-fight input is the retreat
call (#15). The catalog is **LEADER-MODULATED** (developer: doctrines "are also affected by leaders") — a commander's
character bends engage/targeting/retreat/pursuit. The substrate exists (`CommanderDB` carries a `PersonalityDB`;
`OfficerCharacter.Blend`/`TenureWeight` are already wired into the space retreat decision; ground has no equivalent
read) — D3b must include it or record its deferral explicitly. State: D1 (unified catalog SHAPE, 12 entries, + reader
+ reciprocal guard) CI-green; D1b (role catalog growing it 12→25 + client domain filter) green as of prompt-writing;
D3a = movement steering (ClosingIntent; make `RoleMoveAway`/`AdvanceClosing` consult doctrine FIRST, fall back to
role; fold `GroundEngagementStance` in; make `SpeedMult` bite — verified: nothing reads it today); D3b = fire
behaviour (target priority, per-doctrine retreat threshold, break-away, pursuit); D2 = ground reads the unified
catalog, retire `groundStances.json`. "Standoff Barrage" was deliberately left OUT of the catalog until D3a exists.

---

## 2. PHASE A — EVIDENCE (a parallel fan-out; be exhaustive)

Run these as parallel subagent groups. Ground rules for every agent: **grep before you trust; cite file:line for
every claim; use the three-state vocabulary** (built-and-gauged / built-but-runtime-unverified / built-but-INERT) and
never let "code exists" pass for "a player can reach it" or "anyone has seen it run."

### A1 — Game-log forensics (be inquisitive; this is detective work, not a skim)

> #### ✅ A1: ALL SIX REQUIREMENTS DONE (3 agents, ~912k tokens) — findings in `docs/DOCS-AUDIT-2026-07-27.md` §9
>
> | A1 requirement | Done? |
> |---|---|
> | Check `game_logs/` on **BOTH** branches for newer logs | ✅ `origin/main` newest = `fe72043` (2026-07-17); audit branch newest = `436f73e` (**2026-07-23**). **No newer logs exist on either** — so the logs do pre-date the five fixes, exactly as the handoff warned. |
> | **Timeline** | ✅ 9 min wall-clock · 144 game-days · **two mouse clicks** · 11 time-button presses · zero ships · one colony · **no ground content reached at all**. |
> | **Failures**, each root-caused against HEAD | ✅ 7 × `[FATAL]` "Speed Result is NaN" in two distinct stack shapes — **both throw sites now bail to a finite value at HEAD** (`496a5d1`), but **the CAUSE is untouched** (see G5). Also `[PERF]` ×1 (2117 ms startup frame, correctly below the hang threshold), 24 missing boot textures, and `console_output.txt` holding **1667/1667 build warnings and zero runtime lines**. |
> | **Combat** | ✅ both engines provably scheduled (sensor scans 148,894 · battle-trigger 4,985,827) yet **ZERO battles formed** — the player had 0 ships and `Tick` enrols only `FleetDB` entities. **And the counters turned out to be placebos** (they increment before the early-return, so they climb on an empty galaxy) — G3. |
> | **The AI** | ✅ 288 decision records tabulated; UMF returned "no legal step" on **135 of 144** cycles while at war; Kithrin did 108 consecutive no-ops. Reconciled against `AI-BRAIN-BUILD-TRACKER`. |
> | **THE SILENT SYSTEMS (most important)** | ✅ delivered as the three-bucket ledger — *exercised-and-worked / exercised-and-broke / never-exercised* — with per-system verdicts (e.g. the battle trigger = **exercised-and-broke**; invasion rungs 0/0a/0b/1.3/1.5/2.5 and the ground fight = **never-exercised**). |
>
> **⚠ One A1 finding was missed in the first write-up and is now recorded as G10 — it may be the widest-blast-radius
> item of the whole run.** All 288 tape lines read `vs no threat` because **every ship contact reports `sig=0kW`
> while the star reports 1.4 M kW**: `LatestDetectionQuality` is zeroed even though the contact had to pass
> `> 0` at scan time. Consequence — `CombatRisk.WouldEngage` deliberately returns **true** on a non-positive
> enemy estimate, so with the input always zero **the AI's entire risk appetite never evaluates anything**, and
> two treaty behaviours can never fire. This is the *"degenerate detection-quality"* keystone
> `DIPLOMACY-DESIGN` already names. **Scheduled as plan slice S1e.** *(Lesson: reading an agent's headline is
> not the same as reading its findings — G10 was in the detail file all along.)*

The repo tracks the developer's real play-session output: **`game_logs/game_log_NNN.txt`** (read in numeric order)
and **`console_output.txt`**. As of handoff the newest logs were committed at `436f73e` — from the **2026-07-23 play
session**, which means they PRE-DATE the fixes that session produced (warp-NaN `496a5d1`, Kithrin survey speed
`fd37692`, hive habitat `181130a`, assembler instrumentation `63684f9`, city-zoom deposit `104eaa2`). Check BOTH `git log origin/main -- game_logs/` AND `git log origin/claude/faction-design-audit-bb3tqz -- game_logs/` (fetch both first) — the developer has pushed logs to each in the past; if newer logs exist on either, those are gold: they show whether the fixes held.

Reconstruct the session like an incident review:
- **Timeline:** `[ACTION]`/`[TIME]`/`[SELECT]`/`[VIEW]`/`[STATE]` lines — what did the developer actually DO, in order?
- **Failures:** `[FATAL]`, `[HANG]`, `[RenderError]`, `[InputError]`, `[PERF]`, `⚠ TELEPORT` — every
  one gets a root-cause hypothesis checked against the code at HEAD (is it already fixed? partially? untouched?).
- **Combat:** `[Combat]`, `[FleetCombat]`, `[DETECT]`, `[EMCON]`, `[ENGINE]` heartbeats — did battles form, resolve,
  interrupt correctly? Did the trigger fire on play?
- **The AI:** `[AI]` decision-tape lines — what did the NPC factions decide and do? Did the Kithrin expand? Did any
  faction invade? Compare against what the AI docs claim it should do.
- **THE SILENT SYSTEMS (most important):** what SHOULD have logged and didn't? Ground combat is known to emit
  nothing. What else ran invisibly or plainly never ran? Build a ledger: *exercised-and-worked / exercised-and-broke /
  never-exercised* — this ledger feeds the reachability plan directly.

### A2 — Doc-claim verification sweep

> #### ⚠ A2: THE 13-ITEM SEED LIST IS 13/13 DONE. The WIDER sweep is partial — scored per target below (2026-07-27)
>
> **Seed list: ✅ COMPLETE.** All 13 verified-then-fixed, with the full ledger in
> `docs/DOCS-AUDIT-2026-07-27.md` **§10** — including **three seed claims that were themselves WRONG** (the
> client "dead code" trio: health bars, hazard chips and the `Held:` line are all **built**; only Shift-click is
> dead, and that one is *worse* than stated — `HandleHexClick` does not exist anywhere).
>
> **The wider sweep ("every planetary/ground/combat doc PLUS 7 subsystem `CLAUDE.md`s"), honestly scored:**
>
> | Target | State |
> |---|---|
> | Subsystem `CLAUDE.md` — GroundCombat · Colonies · Industry · Combat · Galaxy | ✅ **swept by agent A2c** (found: the backwards upkeep table, the non-existent C3 test, `ColonyHexMapDB`, the "no diplomacy system" claim refuted inside the function that reads it, the retired installations-UI claim, the garrison-vs-#27b overstatement) |
> | Subsystem `CLAUDE.md` — **Client** | ⚠ **targeted, not swept** — 5 specific claims verified + corrected (ground-unit coordinates; the non-existent `GroundCombatWindow`; Shift-click; `[FleetCombat]` as a battle channel; the `faults=`/heartbeat gauge traps). No exhaustive pass. |
> | Subsystem `CLAUDE.md` — **Tests** | ✅ **swept this walk-through.** All **142** named fixtures exist (**zero missing**). But it is indexed as a *"full test inventory"* and names **142 of 343** — **201 undocumented (59%)**, incl. `BattleLogTests`, which slice S1 needs. Also caught: its **"~13 min" sharded-CI claim is stale** — `rest` measures **33 min**, so wall-clock is back to the pre-sharding figure and the doc's own rebalance trigger is met. Both corrected. |
> | `docs/combat/*` (14 docs) | ✅ **swept by agent A2b**, 31 findings (ruling #22 has ZERO code — 7 types, 0 files each; the per-tick vs per-second damage asymmetry; the ship-only saturation rule; the stale REAL-DISTANCE status header) |
> | `docs/ground/*` (7 docs) | ⚠ **5 of 7 corrected individually** (EARTHFALL-CAMPAIGN-OPS · GROUND-SURFACE-MAP · GROUND-UNIT-VARIABLES · PLANETARY-GAMEPLAY-AUDIT · SURFACE-FOG). **Agent `A2a` never ran**, so `GROUND-ORDERS-CATALOG-DESIGN` and a full claim-sweep of `GROUND-GAMEPLAY-DECISIONS` remain **unverified**. |
> | Dashboards | ⚠ MVP ✅ · PLAY-TO-MARS ✅ · DOCS-INDEX ✅ · SYSTEMS-STATUS ✅ (retired + archived) · aurora/GROUND-COMBAT ✅. **`REALISM-VS-GAMEPLAY-AUDIT` and `SYSTEM-CONNECTION-MAP` never verified** — agent `A2e` never ran. |
>
> **Un-run agents, named so they are not lost: `A2a` (ground docs), `A2d` (client+tests exhaustive), `A2e`
> (dashboards).** Briefs preserved on disk (audit §5). This is a **stated limit, not a pass**.

Every planetary/ground/combat doc in `docs/` PLUS the subsystem `CLAUDE.md`s (`GroundCombat`, `Galaxy`, `Colonies`,
`Industry`, `Combat`, `Client`, `Tests`) — extract each doc's load-bearing factual claims and verdict them
**CONFIRMED / STALE / REFUTED / OVERSTATED** against code at HEAD, with file:line evidence. Seed list of drift already
found by the prior surveys (verify each is still true, then FIX in Phase C):

- `docs/aurora/GROUND-COMBAT.md:6` claims Pulsar has *no ground combat at all* — false against a ~55-file subsystem, and root
  `CLAUDE.md` sends every ground designer straight to it.
- `docs/MVP.md` + `docs/PLAY-TO-MARS-WALKTHROUGH.md` still name the invade-from-orbit panel as the #1 blocker; it was
  built (Earthfall C5.1, 2026-07-19).
- `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` is "being retired" yet root `CLAUDE.md` both mandates opening it on every
  dive AND says don't add to it — complete the retirement and fix the contradiction.
- `Pulsar4X/GameEngine/Colonies/CLAUDE.md` calls `ColonyHexMapDB` "built and wired"; the surface design lists it as a
  do-not-revive landmine; in code it's save-UNSAFE and still live on a toolbar window that ATTACHES it to a colony.
- `Pulsar4X/Pulsar4X.Client/CLAUDE.md` (~line 689) says ground units live on the `ColonyHexMapDB` tile grid — false; they live
  on `GlobalQ/GlobalR` + `MiniQ/MiniR`.
- `Pulsar4X/GameEngine/GroundCombat/CLAUDE.md`: the upkeep-source claim is BACKWARDS (assembler/garrison DO set it; the
  base-mod monolithic path doesn't), and the "C3 FULL path" test it cites does not exist.
- **Scale contradiction our own consolidation introduced:** `GROUND-SURFACE-MAP-DESIGN.md` Layer 4 says ~560 km
  op-hex / ~47 km mini-tile; Layer 5 says ~477 km / ~37 km; `REAL-DISTANCE-COMBAT-DESIGN.md` uses 560. **Derive the
  TRUE numbers FROM CODE** (`PlanetGridFactory` dims + `GroundMiniHex.MiniPitchKm`) and make all three docs agree.
- `docs/DOCS-INDEX.md` disagrees with itself: the As-of stamp flags PLAY-TO-MARS / MVP / GROUND-UNIT-VARIABLES stale
  while their rows still read current.
- Stale "nothing calls this yet" comments that are false: `LoadTroopsOrder`/`LandTroopsOrder` doc comments,
  `GroundForcesDB` on `OrderFormationMoveToGlobalHex`.
- `docs/ground/EARTHFALL-CAMPAIGN-OPS.md` says "the hex is the unit of everything" — the code inverts it (combat
  groups by region; capture flips the region).
- `docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md` under-reports itself — slices 5–6 are built elsewhere.
- Client dead code documented as live: token health bars, hazard chips, the "Held:" line, Shift-click waypointing.
- `docs/ground/PLANETARY-GAMEPLAY-AUDIT-2026-07-24.md` is itself part-stale: its "two open design questions" were
  LOCKED by Layer 6 the same day (fine terrain = affinity + bends-the-fight + the costs law; weather at BOTH
  zooms), and its P1 "default garrison on" recommendation is overridden by ruling #27b — reconcile in Phase C.

### A3 — Reachability walls audit (the player-path ledger)

> #### ⚠ A3: 21 of the 23 named walls VERIFIED — but by OTHER MEANS; agents `A3a–e` never ran (2026-07-27)
>
> **Honest accounting:** the five assigned wall agents never ran. The coverage came from the **A4 rulings
> matrix** (which hit most walls from the ruling side), from **direct session verification**, and from **this
> slow walk**, which closed the last five that nobody had checked. The orders also ask for a **click path + size
> per wall** — that lives in THE PLAN's slices (each carries a literal click-path reachability criterion and a
> cheap-wire/medium/large size).
>
> **Verified ✅ (21):** stock New Game raises nothing (`NewGameMenu.cs:52,55,60`) · assembler ground panel gated
> on a ship design · saved ground design unreopenable (`ShipDesignWindow.cs:166,223,592`) · no penetration/energy
> on the assembled path · region-0 muster hardcode (zero non-zero writers) · the free build path **and a SECOND
> free one** vs the fortification list · **a Production-tab building is located nowhere** (`BuildingDesign.cs:52-57`
> installs but never places on a hex) · no retreat verb · auto-engage on the region band · zero battle
> log/events/records · the five flags as process statics · ground fog unread by the client · research data-inert
> (17/22 templates cost 0) · **`CrewReq` — worse than stated: there is no crew field on a ground design at all** ·
> `EmploymentAtbDB` on zero templates · colony power double-dark · `BombardGlobalHex` **test-only (one caller, a
> test)** · `OrderFormationTreeMoveToHex` **zero callers** · the validity gates **resolved: they compute + display
> but do NOT block the save** · orbital bombardment orphaned · **`Amphibious` read by nothing.**
>
> **⛔ TWO LIVE BUGS fell out of walls nobody had checked** — both cheap, both now plan slices:
> - **W14 → S1f: the AI's garrison rebuild produces CARGO, not soldiers.** `ConquerResolver.cs:388-396` never
>   sets `job.InstallOn`; `ComponentDesign.cs:70,73` only installs a finished component when it is non-null, and
>   the generic default in `IndustryTools.cs:66-75` is **commented out**. Every player path sets it, and so does
>   the costed tile queue (`GroundBuild.cs:63`). So the AI spots a depleted garrison, spends the materials, and
>   gets an item in cargo — forever, and it still reads as depleted afterwards. **One line.**
> - **W17: `Amphibious` is a cradle-to-grave violation, not just dead data.** The dial is declared, JSON-settable,
>   copy-ctor'd, **and described to the player in the designer blurb** (`GroundLocomotionAtb.cs:32,40,43,51`) —
>   while the only other engine mention is a `HexPathfinder.cs:39` comment saying the gating is *not* implemented.
>   A player can design, cost and build an amphibious unit and it does nothing; water is impassable for everyone.
>
> **⚠ Still UNVERIFIED (2):** the *"every March-to-Region path sets the region index **without restamping the
> global position**"* claim (A4b confirmed it is the only fully-wired move verb, but the restamp half was never
> proven — flagged in the canon doc rather than asserted), and the **city-builder half** of "test-only
> city-builder methods" (only `BombardGlobalHex` was checked). Full detail: audit §11.

Re-verify each known wall at HEAD (some may have moved). For each: the exact click path a player would take, where it
breaks, file:line, and whether the fix is cheap-wire / medium / large. The known list: stock New Game has no troops,
no enemy, no fleet (`NewGameMenu` auto-flags false — ruling 27b keeps this; the scenario-start question is open);
the Entity Assembler's ground panel gated on a pre-existing SHIP design (`ShipDesignWindow.cs` ~166/178/332); a saved
ground design can never be reopened (registered into `IndustryDesigns`, list reads `ShipDesigns`); no
penetration/energy dials on the assembled path; region-0 muster hardcode (`GroundUnitDesign.DefaultRegionIndex` — declared at GroundUnitDesign.cs:126 and READ at muster, :163-165, but never set non-zero by any path including the `GroundUnitAtb` install hook, so every build musters into region 0); the free "Build here" button (unlimited instant free buildings AND infantry) vs the costed
tile queue that charges full price yet never writes the region list fortification reads; a Production-tab building
located NOWHERE on the map; every "March to Region N" path sets the region index without restamping the global
position (token never moves); no retreat/withdraw verb; auto-engage on region-band share; zero battle
log/events/records; the five behaviour flags as process statics; per-faction ground fog fully built engine-side and
unread by the client (a rival's survey reveals your deposits); the AI garrison-rebuild queue missing `InstallOn`
(replacement infantry becomes a crate forever); research data-inert; `CrewReq` computed and never read;
`Amphibious` read by nothing; `EmploymentAtbDB` on zero templates; colony power double-dark; test-only city-builder
methods + `BombardGlobalHex`; `OrderFormationTreeMoveToHex` zero callers; assembler validity gates (carry/power/ammo): the audit's status board says they bite in the ENGINE assembly path while the surveys found them unenforced at the SAVE step ruling #3 targets — verify with file:line which of compute / display / save-block each gate actually does before sizing the wall; ORBITAL BOMBARDMENT ORPHANED (the audit's BREAK 2 / P3): the live fleet engine never fires on colonies — no `BombardColonyOrder`, no client button, no `ConquerResolver` bombard rung, so "soften before you land" is dark for the AI and a fire-control workaround for the player.

### A4 — Rulings-compliance matrix

> #### ✅ A4: VERIFIED DONE (2026-07-27) — all three agents ran; the gap was that **nothing in the repo held the matrix**
>
> **What ran:** `A4a` (rulings 1–9), `A4b` (10–18), `A4c` (19–27) all completed before the usage-limit wall — the only
> Phase A batch where every assigned agent finished. Each produced done/partial/missing verdicts **with file:line
> evidence**, and each delivered **dependency edges** (verified by grep, not assumed: 20 / 10 / 10 edge mentions across
> `A4a-rulings-1-9.md`, `A4b-rulings-10-18.md`, `A4c-rulings-19-27.md`).
>
> **The gap this walk closed:** the verdicts existed only in **ephemeral scratchpad files**. Nothing committed to the
> repo carried a ruling-by-ruling table — THE PLAN cites rulings in prose and marks delta rows `[A24]`, but a reader
> could not ask "what is the state of ruling #14?" and get an answer. **Now consolidated as
> `docs/DOCS-AUDIT-2026-07-27.md` §12** (commit `214c8b6`): 27 rows (ruling / verdict / load-bearing fact / size),
> a doctrine+tick table, and the dependency edges.
>
> **Doctrine + the tick (the frame this section names separately):** `D1` is **data-gauged but its reader is INERT** —
> 9 of its 10 functions have **zero non-test callers**; `D1b`'s filter is **space-only**; `D2` / `D3a` / `D3b` are
> **MISSING**; leader modulation is **space-built / ground-missing**; the shorter tick is **MISSING with a 5 s spec
> already committed and waiting** (which is why Q3 is a confirm, not an open design question).
>
> **Dependency edges — the orders' three CONFIRMED, two of them sharpened, plus two added:**
> - *#1 needs #2 and #4* — confirmed as stated.
> - *#9 and #10 must land together or ground defence silently breaks* — confirmed, **and sharpened: CI would not catch
>   the breakage.** There is no gauge on the pair, so the silent failure would ship green.
> - *#23 needs the tick slice* — confirmed, **and sharpened: they must be the SAME slice**, not sequenced ones. The
>   overkill rule is defined in tick units; landing it against the 1-hour tick encodes the wrong number.
> - **ADDED:** #18 should precede #23 (the overkill rule reads the value #18 establishes).
> - **ADDED:** D0 precedes D2 / D3a / D3b (the three doctrine slices all consume D0's frame).

All 27 rulings + the doctrine frame + the tick-shortening decision × the code at HEAD → **done / partial / missing**,
each with evidence and its dependency edges (e.g. #23 needs the tick slice; #1 needs #2 and #4; #9 and #10 must land
together or ground defence silently breaks).

### A5 — Test/gauge coverage

> #### ✅ A5: VERIFIED DONE (2026-07-27) — agents never ran; **done in the main loop**. All four questions answered
>
> Full findings: **`docs/DOCS-AUDIT-2026-07-27.md` §13**. The gaps it found were **closed in the same commit**, not
> just reported — `TESTING-TRACKER.md` gained the two missing boards, `CLIENT-TEST-CHECKLIST.md` gained the crash row.
>
> **① Tracker truth-check → HONEST. Zero phantom tests.** The C3 defect does **not** recur. All **97** backticked
> fixture names check out — 94 resolve to a real `class` in `Pulsar4X.Tests/`, and the 3 that don't are the tracker's
> own family abbreviations inside `…`-ellipsis lists (`RangeReadoutTests.cs` and `SpatialEnvironmentsDioramaTests.cs`
> both exist; **`SocietyReadout` is an engine class**, correctly named as the thing under test). All **7**
> `Fixture.Method` claims verified present.
>
> **② The tracker's real blind spot is the INVERSE of C3 — not phantom tests, but REAL tests that aren't running.**
> **Six** `[Ignore]` attributes at HEAD; the tracker indexed **one**, and only to say it was *resolved*, then claimed
> *"no deliberately-red engine gaps remain"* — true about **reds**, silent about switched-off gauges. **Three park a
> live defect:** a **player-REACHABLE** New-Game NRE when `Pulsar4x-Testing` is ticked (`NewGameStartSmokeTests.cs:24`
> — the mod ships, the mods page lists every mod with a checkbox at `NewGameMenu.cs:157-171`, so it is **one tick
> away**; off by default only because its manifest has no `DefaultEnabled` field, `ModsState.cs:62`); an **AI-founded
> colony that starts 0-population AND 0-tax-rate** so it never pays (`EfKithrinExpandArcTests.cs:322` — bears directly
> on slice **S1b**); and the 2D group-plane anchors unproven through save/load (`MidCampaignSaveLoadTests.cs:173`).
> A fourth, `PathfindingTests.cs:102`, is `[Ignore("Incomplete Test")]` with **no reason, no owner, no date**.
>
> **③ Slices with NO gauge → three, and only ONE is a real gap.** 17 of 19 slice headings in THE PLAN carry an
> explicit **Gate:** (S7's four sub-slices covered by D0's own gate + *"Gate per sub-slice"*). **S11 (Depth) is the
> real gap** — ~13 named items, and only the pulled-forward `SYSTEM-GENERATION` G1 round-trip has a gauge, so S11 must
> never be entered as a unit. **S12** is gauge-**BLOCKED** (the assertion can't be written until Q1 lands), not
> gauge-missing. **M1** is ungauged by design (it *is* the Layer-3 gauge) but had **no home in any doc that owns
> Layer 3** — now a tracker row.
>
> **④ Heavy fixtures → TWO slices need their own shard, and there is a trap documented nowhere.** Measured heavy-path
> load (`TestScenario.CreateWithColony`, the cost `ci.yml` itself names): **`rest` carries 665 calls across 234
> fixtures**, against **18** in the `stations` shard that was isolated *because* it was the ~11-min bottleneck —
> and **`GroundForcesTests` alone is 48 calls / 61 tests**, the single heaviest fixture in the suite and the natural
> host for S1/S8. **S1** (a battle to completion) and **S8** (the same fight at two tick lengths ⇒ ≥3 fights) each
> need their own shard, carved in the same commit. **🧨 The trap:** `rest` is a **hand-maintained** complement
> (`ci.yml:69` = `!~` of all seven named shards), so adding shard `X` without also adding `FullyQualifiedName!~X`
> to `rest` makes `X` run **TWICE** — the isolation then costs more than it saves.
> *(`docs/earthfall/IMPLEMENTATION-AUDIT-2026-07-22.md:83` calls the sharding "gap-proof by construction" — true for
> **coverage**, silent on this **duplication** direction.)*

`docs/TESTING-TRACKER.md` truth-check + which planned slices have NO gauge yet. Flag any doc-claimed test that
doesn't exist (the C3 case) and any heavy new fixture that would land in the `rest` CI shard and need rebalancing.

## 3. PHASE B — ADVERSARIAL VERIFICATION

> ### 🟡 PHASE B: ROUND 1 + ROUND 2 DONE — **1 genuine walk-back, 4 new defects, 5 verdicts still owed**
>
> Round 1: **audit §15a** (10 claims, 0 refuted, 3 sharpened). **Round 2: audit §14.** Both in `docs/DOCS-AUDIT-2026-07-27.md` — round 1 was migrated there when the compliance tracker was deleted.
>
> **Cost note, recorded because the deviation must be explicit:** a 15-agent fan-out (5 claims × 3 diverse lenses,
> default-to-refuted) was launched and **killed on the developer's third budget intervention** — 2 agents started,
> **none finished**, ~150–200 k tokens, **zero verdicts**. The arithmetic was already in the compliance doc §6 and
> was ignored because a session flag asked for fan-out — the measurements now live in **audit §15b**. **The standing instruction outranks the flag.** Round 2 ran
> in the main loop for a small fraction, applying the same three lenses by hand.
>
> **🔴 The first genuine walk-back of the operation — ruling #25.** *"Zero tooltips anywhere in the client"* is
> **flatly false** (**101 `SetTooltip` + 12 `BeginTooltip`** across 20+ files), and aggregated ground strength
> already renders (`PlanetViewWindow.cs:1081-1087,466,1497`) with hover already detected 3× for clicks. The first
> plan draft said **PARTIAL**; the session "corrected" it to MISSING; Phase B **restored PARTIAL** and re-sized the
> slice **medium → cheap-wire**. *An absolute quantifier is the most refutable thing in any finding.*
>
> **4 CONFIRMED — but three of them exposed a NEW defect apiece, all fixed:**
> - **N1** `PLAY-TO-MARS-WALKTHROUGH.md` contradicted itself 95 lines apart — §L was rewritten to say the invade
>   panel is built, while **table row L still read `❌ no button, no order`** and the header still said *"three gaps."*
>   Row flipped; count corrected to **two** (K bombardment → S9; I no-enemy-on-normal-start, DevTest qualified).
> - **N2 (my own overreach, withdrawn)** I framed B5 as *"changes a documented build order."* It does not —
>   `DIPLOMACY-DESIGN.md:7` already recorded keystone 3 as *"substantially DONE"* on **2026-07-07**. Only
>   `Combat/CLAUDE.md:107` was stale.
> - **N3** `DIPLOMACY-DESIGN.md` contradicted **itself** — banner said keystone 3 done, its table ~460 lines below
>   still said *"hostility isn't diplomacy-driven."* Row corrected.
> - **N4 — a doc claim that invited deleting live code.** `DIPLOMACY-DESIGN.md:458` said `SignalQuality` **"was CUT."**
>   **REFUTED:** live in **9 files**, gating **survey reveal** at `> 0.20` / `> 0.80` (`SystemBodyInfoDB.cs:154-160`,
>   `StarInfoDB.cs:130`). Only its *role as the hidden-info gradient* was cut. Read literally, the old wording is an
>   instruction to break survey accuracy.
>
> **Two consequences for slice S1e, both landed in THE PLAN:** its *"named keystone prerequisite"* justification is
> **withdrawn** (that keystone was dissolved — the slice stands on its own 288-of-288-blind evidence); and it now has
> a **concrete root-cause lead** where the plan had said *"unverified, do not guess"* — `ThreatAssessment.cs:11`
> (deliberately reads signal STRENGTH because the `SignalQuality` path was design-cut) plus the **commented-out**
> `if(detectionValue.SignalStrength_kW > 0)` guard at `SensorTools.cs:69`, ~150 lines above its setter at `:220`.
>
> **Also re-checked (all CONFIRMED, three sharpened):** `GroundCombatWindow` truly absent (and `PlanetaryWindow.old.cs`
> is a **sealed** commented-out corpse, so no L1 risk) · multi-weapon plurality range-gates **per weapon per target**
> · the region is a **derived band of hex columns** and **planet** capture is **all-or-nothing** across regions ·
> upkeep confirmed hard, and **`GroundUnitAtb` has no upkeep parameter at all**, so base-mod units are *structurally*
> unable to bill *(self-corrected: its ctor uses optional trailing params, so adding one is cheap — not the
> exact-arity break I first called it)* · the installations-UI gap refuted.
>
> **✅ B1 RESOLVED 2026-07-28 (audit §17) — and it made a LARGE slice SMALL.** The highest-value owed verdict is
> done. `MoveToRegion` *is* the only verb wired **end-to-end**, but *"the ONLY fully-wired move verb"* is
> **refuted**: `MoveToHex` already has its enum, factory, formatter, **processor execution**, and — the part nobody
> had noticed — **the client already draws its waypoint path, on global cylinder coordinates**, which is the global
> half of the two-layer scheme #14 asks for. It lacks only **ISSUERS** (no button, no AI call, no test). So #14 is
> not *"delete the only thing that works and build a replacement"* — it is **wire the issuers → add the missing
> two-layer formatter → then retire `MoveToRegion`**. **Slice S6 re-sized `large` → cheap-wire + a retirement.**
> ⚠ Retirement caution recorded: `MoveToRegion` carries the **only save/load gauge of ground movement**.
>
> **✅ PHASE B COMPLETE 2026-07-28 — the last four verdicts are done, RESIDUE ZERO (audit §18).** All four hold,
> but one was materially overstated and correcting it found a **third slice to shrink**:
> - **V1** *"Pulsar has no ground combat at all"* → **confirmed refuted**, all five sub-claims checked individually
>   (unit entity · 2 formation classes · the hourly `IHotloopProcessor` · both troop orders · the client window).
>   File count tightened **~56 → 55**.
> - **V4** the garrison note → **confirmed overstated**: `NewGameMenu.cs:55` is literally
>   `AutoRaiseHomeGarrison = false` under *"BAREBONES: no default home garrison"*; only **DevTest** flips it.
> - **V3** the `REAL-DISTANCE` header → **confirmed stale**: slice 2's behaviour shipped as **K1+K3** under
>   `EnableMiniHexCombat` (not the proposed `EnableGroundRealRange`), and it **bypassed the doc's own
>   `RealRangeKmFor` seam**. CI-off, menu-on.
> - **⭐ V2** the saturation scoping → **confirmed for saturation, REFUTED for *"no rate dial at all."*** Ship
>   saturation genuinely derives from rate (`Saturation = RoundsPerSecond × PelletsPerShot`, `FlakWeaponAtb.cs:35`)
>   and ground saturation genuinely is two hardcoded category constants (`GroundCombatant.cs:40,44`). **But ground
>   HAS a rate dial:** `GroundCombat/SpaceWeaponGround.cs:64-79` already computes **true damage-per-second** from
>   `RoundsPerSecond`, which is **authored in `weapons.json`** — whose own text says it *"drives damage/sec and
>   saturation."* `GroundForcesProcessor` references **none** of it and resolves flat `Attack × SalvoScale` per tick.
>
> **⇒ RULING #23's RATE MODEL ALREADY EXISTS. Slice S8 is a CONNECT, not an invention** — make the resolver read the
> per-second value that is already computed in its own folder. The `deltaSeconds` audit (**C2**) remains the large
> part. **And developer question Q5 narrows sharply**: the rates *are* picked for designer weapons; the only weapons
> without one are the flat-`Attack` garrison/base-mod units.
>
> **📌 THE METHOD LESSON, FINAL FORM.** All three quantifier failures across Phase B — *"zero tooltips anywhere,"*
> *"the ONLY fully-wired verb,"* *"no rate dial at all"* — were an **absolute word** on a claim that was
> directionally right. **Two of the three were caught only because a negative grep was re-run under other
> spellings** (`chips` not `hazard chip`; **`RoundsPerSecond` not `RateOfFire`**). **Absolutes and negative greps are
> this codebase's two most reliable sources of wrong findings.**
>
> **Final tally — 22 claims, residue zero:** 1 refuted outright · 2 refuted in their quantifier · 19 confirmed (11
> of them sharpened) · 2 of my own framings withdrawn. **Three slices re-sized, all downward** (#25, S6, S8).
>
> **📌 STANDING LESSON, worth more than any single verdict:** three of the four new defects were **not wrong
> findings — they were correct findings with INCOMPLETE remediation** (a section fixed but not its index row; a
> banner fixed but not its table row). **A half-applied fix is indistinguishable from a wrong verdict.** When a
> verdict lands, grep the doc for every *other* place that states the same thing.

Nothing from Phase A enters Phase C on one agent's word. For every REFUTED/STALE verdict and every wall: an
independent refute pass (3 lenses or 3 voters, majority rules; "default to refuted if uncertain"). Keep the killed
findings in an appendix so the next session knows they were checked.

## 4. PHASE C — CONSOLIDATE, CORRECT, DELETE (execute now, in CI-gated commit slices)

> ### 🟡 PHASE C: five of six rules SATISFIED — and the sixth was the one nobody had touched
>
> | Rule | State |
> |---|---|
> | Subject subfolders · `DOCS-INDEX` row in the SAME commit · gauge state → `TESTING-TRACKER` | ✅ held every commit |
> | **Connections → `SYSTEM-CONNECTION-MAP.md`** | ❌ **WAS THE GAP — now closed.** See below. |
> | Merged-away docs hard-deleted with a full `.md` **and** `.cs` link sweep; provenance names as plain text | ✅ **327 dead pointers** across 249 files repointed, residual grep **0**; the ~22 provenance mentions deliberately left alone (rewriting exactly those is what destroyed history in the 2026-07-13 sweep) |
> | Superseded-but-historical → `docs/archive/` with a banner; invalid-and-worthless → delete | ✅ `SYSTEMS-STATUS-AND-TEST-PLAN.md` **moved** to `docs/archive/` (23 sites / 17 files repointed, residual 0); **one deletion executed** — the compliance tracker, at the developer's instruction, content migrated to audit §15. **No design doc deleted, and that is a written verdict, not an omission** (audit §15d) |
> | Every A2 seed item fixed or explicitly ruled still-true | ✅ **13 of 13**, ledger at audit §10 — including the **3 seed claims that were themselves wrong** |
> | Finish the `SYSTEMS-STATUS-AND-TEST-PLAN.md` retirement | ✅ complete — bannered, archived, and root `CLAUDE.md`'s **four** contradictory mandates repointed |
>
> **⚡ THE GAP, AND WHY IT MATTERED.** `SYSTEM-CONNECTION-MAP.md` is the **named owner of system-to-system
> connections** and the Prime Directive's *one* upkeep rule — *"if you find a connection this map doesn't list, add
> it in the same commit as the change that revealed it."* This operation's only change to it was a **one-line path
> repoint**. Every connection the operation found was missing from it.
>
> **The structural reason it stayed empty:** the map could only draw wires that **exist**, and nearly everything
> this operation found is a wire that **should** exist and doesn't. A diagram that shows only working connections
> reads as *"the rest is fine."* So the map now carries a **⚡ BROKEN AND MISSING EDGES** section — 10 rows, each
> naming **both ends**, the **exact line** where the wire should be, and its **slice key** — with the rationale
> stated in the header so it can't become a status column by the back door: **a cut wire is a connection fact, not a
> build-status mark.** The rows: capture never updating the faction colony registry (no removal path exists at all,
> and 8 AI readers consume it) · ground combat halting the clock while emitting nothing · the AI threat read summing
> a field that is always 0 · doctrine assignment dropping 4 of its own fields · the costed queue writing hexes that
> can never fortify (why #9 and #10 are one slice) · the AI garrison rebuild producing cargo instead of soldiers ·
> base-mod units structurally unable to bill upkeep · ground damage per-tick vs space per-second · orbital
> bombardment never firing on a colony · plus **one edge that DOES work** (`GroundFootprintAtb`), recorded so nobody
> rebuilds it.
>
> **Three existing rows were also stale or incomplete, now corrected:** **Diplomacy** read *"design only"* — it is
> wired into space-combat hostility **both ways**, and *"design only"* was itself a build-status mark this file bans;
> **Ground combat** was missing its edge **INTO the time loop** (a ground battle stops the player's clock);
> **Sensors** was missing its two real consumers — the AI threat read and **survey-reveal accuracy** — which
> **share one struct**, so a change aimed at one lands on the other.
>
> **⚠ STILL OWED (carried in audit §15f):** Phase C ran **before** Phase A finished, so some doc corrections were
> made against **incomplete evidence**. The A4, A5 and Phase-B passes all landed later and changed real facts — a
> **re-sweep of the docs those passes touched** is owed before Phase C can be called closed.

Rules (all from root `CLAUDE.md` + established precedent):
- Docs live in subject subfolders; **flip the `DOCS-INDEX.md` row in the SAME commit** as any doc change; test/gauge
  state → `TESTING-TRACKER.md`; connections → `SYSTEM-CONNECTION-MAP.md`.
- Merged-away docs are **hard-deleted with a full link sweep** (precedent: commit `6492498`) — grep every `.md` AND
  `.cs` for the old path. Keep provenance names as **plain text**, not links: the 2026-07-13 sweep once rewrote
  merged-source names into the surviving doc's own path and destroyed the history (the repair note is in
  `GROUND-SURFACE-MAP-DESIGN.md`'s header). Don't repeat that.
- Superseded-but-historical → `docs/archive/` with a banner; invalid-and-worthless → delete.
- Every specific item in the A2 seed list gets fixed or explicitly ruled still-true-as-written.
- Finish the `SYSTEMS-STATUS-AND-TEST-PLAN.md` retirement (migrate live rows, banner it, repoint root `CLAUDE.md`).
- Prune `docs/CLIENT-TEST-CHECKLIST.md` of retired items; bring `TESTING-TRACKER.md` current.
- Comment-only `.cs` corrections (fixing FALSE doc-comments, e.g. the stale "nothing calls this yet" lines) ARE
  doc work — do them in Phase C, through the same one-slice-one-push CI gate. Anything that alters BEHAVIOUR is a
  build slice: it goes in THE PLAN and the workflow, never executed this session.
- Record your findings as a dated audit doc (the `DOCS-AUDIT-YYYY-MM-DD.md` pattern) so the process is re-runnable.

## 5. PHASE D — THE PLAN (one document, the developer's map to done)

Write `docs/ground/PLANETARY-FUNCTIONAL-PLAN-<date>.md` (indexed same commit). Required content:

1. **The delta ledger:** every designed planetary capability × three columns — *functional? / accessible? /
   observable?* — each cell three-state with evidence. This is the headline artifact.
2. **Ordered build slices.** Every slice: what it does · its CI gauge · its **reachability criterion written as a
   literal click-path** ("New Game → Force Management → Battalions → …") · its observability criterion (what log/
   readout proves it live) · dependencies · size (cheap-wire / medium / large). One slice = one push = one CI gate.
3. **Sequencing principle — justify it, the developer rules on it.** Recommended spine: **observability first**
   (the ground battle LOG #24 + flags-into-save #27a — you cannot tune or trust-test what you cannot watch), then
   **reachability** (assembler entry + design reopen; the ONE build queue #10 with destinations #6; movement rework
   #14/#16/#17; delete the free path #9 together with unified building location), then **doctrine steering**
   (D2, D3a, D3b — everything goes through doctrines), then the **tick + rate-fire pair** (#23), then the
   **bombardment joint** (audit P3: a region-targeted `BombardColonyOrder` + the client button + the
   `ConquerResolver` rung — "soften before you land" becomes real for both seats), then the **designer chain in its
   forced order #2 → #4 → #1** (weapon dials → research costs → delete the prebuilts, with the garrison-composition
   replacement) **plus #3** (block invalid designs from saving), then **depth**
   (people cost #7, ammo #8, hazard counters #5, CasualtyTier #22, employment/power #12, per-tile terrain M4 —
   gated on the SYSTEM-GENERATION doc's G1–G6 build order (writer+round-trip → belts/Oort → write-on-first-gen →
   hybrid freeze → setup choice → physics terrain; NONE built — distinct from the surface-map doc's same-lettered,
   mostly-built cylinder G-track; decide in THE PLAN whether these are slices or a written deferral) — naming +
   Layer-6 display, fog client wiring, semantic tile bonuses #13, and the audit's P6/P7 tail: the grave rung
   (pop→0 / Rebellion-expiry colony collapse; deepening `TryCapturePlanet` waits on open #21), the G6b-3 disk
   deletion (one hex model), and hex-deposit-as-mined-truth — each scheduled or explicitly deferred, never silently
   dropped). **Schedule the audit's P2 milestone explicitly** after the observability + reachability slices: one
   recorded live cradle-to-grave sitting on the developer's machine (survey → colonize → mine → build a unit → load →
   sail → win orbit → land → capture), capturing `console_output.txt` + gauge readings, with rows added to
   CLIENT-TEST-CHECKLIST / TESTING-TRACKER — everything downstream is deepening an unproven system until it fires.
4. **Every balance number flagged**, never silently chosen.
5. **The open developer questions**, asked in prose at the top: #21 (capture transfer), the scenario/skirmish start,
   the tick length VALUE (shortening itself is ruled; the value is not) and the mid-tick overkill semantics (sequential-down-priority vs lost — confirm the interpretation), per-weapon fire-rate numbers, and anything new your evidence surfaces.
6. A plain-English executive summary the developer can read in two minutes.

## 6. PHASE E — THE WORKFLOW (the culmination)

Author a **saved, re-runnable Workflow script** that executes the plan — the delta-closer. Requirements:
- Persist it as a named workflow (`.claude/workflows/close-planetary-delta.js`, committed) so any future session can
  invoke it by name with `args`.
- **Parameterize by slice** (`args: {slice: "S3"}` or a range): the CI wait (~13 min) lives OUTSIDE the workflow —
  the session pushes a slice, waits for green, then invokes the next. The workflow encodes, per slice: implement
  (agents with the relevant file:line ledger in-prompt) → gauge-write → adversarial verify (refute pass on "done") →
  a docs agent that flips DOCS-INDEX/TESTING-TRACKER rows in the same slice.
- Include a **completeness-critic** terminal stage ("what's missing — a wall not covered, a ruling not landed, a
  gauge not written?") whose findings feed the next slice list.
- Validate STATICALLY ONLY: `node --check` the script file, diff its `meta` shape against the precedent
  `.claude/workflows/earthfall-campaign.js`, and READ the phases array to confirm it matches THE PLAN's slices.
  **NEVER invoke the workflow (or its skill wrapper) this session** — files in `.claude/workflows/` surface as
  invocable skills, and "validating" by running it would start the build without the developer's go. Present the
  plan + the workflow, ask for the go and for rulings on the open questions, and stop there.

## 7. LANDMINES YOU WILL HIT (learned the hard way — do not relearn)

- **JSON atb binder is EXACT-ARITY** (GroundCombat gotcha 6): a new ctor dial means updating EVERY template that
  binds that atb in the same change. This is why ruling #2 (weapon dials) is genuinely wide.
- **Six-point registration** (gotcha #10) for any new buildable; `BaseModIntegrityTests` is the sensor; JSON drift
  crashes players, not `dotnet test`.
- **One hotloop processor per DataBlob** (L9) — new ground steps go INSIDE `GroundForcesProcessor`, never beside it.
- **Byte-identical flag discipline:** behaviour changes ship behind default-off flags (CI-off, menu-on) — and note
  ruling #27a will eventually move those flags into the save.
- **Combat determinism is locked** (fast-forward == watch): no RNG in resolvers without a seeded, order-independent
  stream.
- **`TypeNameHandling.Objects`**: renaming/moving any `*DB` breaks saves — migration or don't.
- **Doctrine reciprocal trap:** space `ToughnessMult` and ground `DamageTakenMult` are reciprocals; author exactly
  ONE per entry; `CombatDoctrine`/`UnifiedDoctrineTests` guard it — keep them green.
- **System-gen (if G1–G6 land in the plan):** new generation must draw from a dedicated RNG stream, never the
  shared `StarSystem.RNG` (the `RuinsDB` lesson — one extra draw silently shifts every downstream body); the spec
  file holds the recipe + frozen OBSERVED detail, never raw hex grids; write the `SystemBlueprint` shape — two
  readers exist and only the blueprint is the live New-Game path.
- If subagents die with a usage-limit error, checkpoint your ledger to a dated doc and resume after reset — the
  prior session lost three verify agents to exactly this.

## 8. DEFINITION OF DONE for your session

- [ ] CI green at your tip, including the inherited `b218acf`/`255bc52` if they were red.
- [ ] Every A2 seed item fixed or ruled still-true; the dated audit doc records the sweep.
- [ ] The doc tree contains no claim a grep of the code refutes; deletions swept, index rows current.
- [ ] The delta ledger + THE PLAN exist, indexed, with a plain-English summary.
- [ ] `close-planetary-delta` workflow committed, validated, parameterized by slice.
- [ ] The open questions (#21, scenario start, tick value, fire rates) put to the developer in prose.
- [ ] A short handoff message: what changed, what's red/green, what you recommend the developer rule on first.

**The one-line spirit of all of it:** the engine is largely built — stop building depth; make the truth documented,
the door openable, and the fight watchable.
