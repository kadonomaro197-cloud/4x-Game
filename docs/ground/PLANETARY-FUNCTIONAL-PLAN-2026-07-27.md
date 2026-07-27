# THE PLAN — making planetary gameplay FUNCTIONAL, ACCESSIBLE, and OBSERVABLE (2026-07-27)

**What this is.** The ordered, gauged, slice-by-slice route from "a deep ground engine nobody can watch"
to "a planetary war you can play and see." Produced by OPERATION GROUND TRUTH
(`OPERATION-GROUND-TRUTH-PROMPT.md`); the evidence run behind it is `docs/DOCS-AUDIT-2026-07-27.md`.

**Governed by:** `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` (the 27 rulings — they override every
other doc), `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the board, Layers 5–6),
`docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md` (the rules), `docs/MVP.md` (scope firewall),
`docs/REALISM-VS-GAMEPLAY-AUDIT.md` (weight firewall).

> **⚠ EVIDENCE HONESTY — read before trusting a row.** A 19-agent verification fan-out for this pass **died
> on the account usage limit**, so this plan is built from three sources and every row says which:
> **[V]** = verified first-hand this pass with `file:line`; **[A24]** = inherited from the 2026-07-24
> holistic audit, *not* re-verified here; **[?]** = needs verification before the slice is sized. Do not let
> an **[A24]** row be quoted later as if it were **[V]**.

---

## 0. FOR THE DEVELOPER — five questions, in plain English

Answer these and the plan hardens. Nothing below is blocked on them except where noted.

**Q1 — Ruling #21: what does capturing a planet actually take from the loser?** *(Still OPEN by your own
ruling — I have deliberately not decided it.)* Right now capture flips one number: the owner ID. Nothing
moves. The candidates are population, the buildings standing on the hexes, the mineral stockpile, the
designs/research, surviving units, and the infrastructure rating. My recommendation is to answer it as a
*shopping list with a condition on each* — e.g. "buildings yes but damaged; stockpile yes; population stays
but unhappy; research no." Each item is a separate small slice, so a partial answer is still useful.
**Scheduled as S12 and deliberately left unspecified until you rule.**

**Q2 — the scenario start.** You ruled a stock New Game must raise no garrison and place no enemy (#27b).
The audit wanted a takeable target from the front door. Those look like they fight, but they don't —
**because the start you'd need already exists and is already on the main menu.** The button is called
**"DevTest"**, and it boots you plus two developed rivals (the UMF at war with Earth, and the Kithrin) with
every ground behaviour switch already on. **[V]** `MainMenuItems.cs:51`, `NewGameMenu.cs:895-911,979-986`.
So the question isn't "build a skirmish mode," it's: **do you want that button renamed and promoted into a
supported "Scenario / Skirmish" start?** My recommendation: yes — it's a rename plus menu copy, and it
satisfies #27b exactly, because your stock New Game stays clean. (If you'd rather keep DevTest as a dev toy
and have a separate authored scenario, that's a real build, and I'd want to know before S3.)

**Q3 — the combat tick VALUE.** You ruled we shorten it (#23); the number is still open. Ground runs
**hourly** today **[V]** (`GroundForcesProcessor.cs:29`). Space's precedent is a **5-second**
`CombatReactionStep`. My recommendation: mirror space — a fine step *while a battle is live*, hourly
otherwise — and start the fine step at **60 seconds**, not 5. Reason: 5 s on ground would multiply the
resolver's per-tick work by 720 across potentially many regions, and a minute is already fine enough that
a 10-damage-per-second gun does 600 per tick instead of 36,000. **FLAGGED balance value.** Pick the number
and S8 is unblocked.

**Q4 — mid-tick overkill.** When a target dies partway through a tick, does the shooter's leftover damage
roll onto the next target down the doctrine's priority list, or is it wasted? Your phrasing read as
*sequential, no waste*, and that's how I've written S8 — but that's **my interpretation of your words, not
a ruling**, and it ties to #18. Confirm it.

**Q5 — per-weapon fire rates.** #23 makes every weapon carry damage-per-second. Nobody has picked the
numbers. I don't want to invent five silently. Cheapest honest route: derive each from the existing flat
`Attack` value divided by the tick you choose in Q3, so the first build is *behaviour-identical* to today,
then tune from there. Say yes and S8 needs no balance decisions at all up front.

---

## 1. The two-minute version

The ground engine is real: an assembler that derives stats from parts, terrain-weighted pathfinding on a
wrapping planet, a deterministic resolver with armour and cover and fortification, real-metre weapon
ranges, a tactical AI, troop lift, and capture. CI proves those paths connect.

**It is not a game yet for exactly three reasons, and only one of them is "missing depth":**

1. **You can't watch it.** A ground battle **already stops your clock** — and then says nothing at all. The
   resolver has no log line, no event, no battle record anywhere in its 1,078 lines, and it deletes the dead
   on the way out. The clock halts, the UI opens a *space* battle report, and that report is empty. That is
   worse than silence: it's a gauge that lies. **[V]** `GroundForcesProcessor.cs:329-330, 334`
2. **The door sticks in two places** (not the front door — that's already open, see Q2): you can save a
   ground design and then **never reopen it**, and the ground panels won't even show up until you've made a
   ship design first. **[V]** `ShipDesignWindow.cs:166, 223, 592`
3. **Nobody has ever seen the whole chain run once, live.** CI cannot run the client. Everything
   client-side is compile-checked only. **[A24]**

**So the order is: make it watchable, then make it reachable, then make it deep.** Depth built on an
unwatched system is depth you cannot tune or trust. The first slice is the cheapest one in the plan and it
converts every later slice from guesswork into measurement.

---

## 2. THE DELTA LEDGER — the headline artifact

Three columns, because "built" has been hiding three different failures. **Functional** = it works and a CI
gauge proves it. **Accessible** = a player reaches it from the normal game, no DevTools, no workaround.
**Observable** = you can watch it happen.

| Capability | Functional? | Accessible? | Observable? | Evidence |
|---|---|---|---|---|
| Ground unit as designed components | BUILT_AND_GAUGED | **BUILT_INERT** — can't reopen a saved design; panels gated behind a ship design | BUILT_RUNTIME_UNVERIFIED | **[V]** `ShipDesignWindow.cs:166/223/592` |
| Penetration / per-shot energy | **BUILT_INERT** — only the 3 prebuilt templates carry them | MISSING on the assembled path | MISSING | **[A24]** + ruling #2 |
| Ground parts cost research | **BUILT_INERT** — 17/22 templates cost 0, all start-unlocked | n/a | MISSING | **[A24]** ruling #4 **[?]** re-count |
| Invalid design blocked from saving | **[?]** gates compute + maybe display; the SAVE block is the disputed half | **[?]** | **[?]** | ruling #3 — **must be pinned before sizing** |
| Muster location (rally point) | **BUILT_INERT** — `DefaultRegionIndex` read at muster, never set non-zero | MISSING | MISSING | **[A24]** ruling #6 |
| Build a ground unit → field it | BUILT_AND_GAUGED | partial — rides industry, but destination is hardcoded region 0 | MISSING | **[A24]** |
| One build queue for units + buildings | **MISSING** — four divergent paths | free path reachable, costed path partial | MISSING | ruling #10 **[?]** count the queues |
| Free "Build here" (unlimited, instant, free) | BUILT — and it is the **only** path that produces a building that fortifies | **reachable** (that's the problem) | n/a | **[A24]** ruling #9 |
| Buildings occupy ground / are war-map objectives | BUILT for start-colony layout | — | drawn | **[A24]** |
| Buildings built AFTER game start get a location | **MISSING** — no hook at production completion | n/a | invisible on the war map | **[A24]** |
| Employment (jobs) + colony power | BUILT_INERT — wired, **data zero** | n/a | MISSING | **[A24]** ruling #12 |
| Semantic tile bonuses | MISSING | MISSING | MISSING | ruling #13 |
| Regional march | BUILT_INERT — **sets the region index without restamping the global position, so the token never moves** | reachable but broken | MISSING | **[A24]** ruling #14 |
| Mini-hex movement as an order | **[?]** | **[?]** | **[?]** | ruling #16 |
| March readout (destination/distance/ETA/speed) | partial — `Speed_kmh` now real and read | **[?]** | MISSING | **[V]** `GroundForcesProcessor.cs:855` |
| Committed fight + retreat + break-away + pursuit | **MISSING** (no retreat verb; no engagement lock) | MISSING | MISSING | **[A24]** ruling #15 |
| Engage decision on real distance | partial — real-metre gate built (K1–K4, M2) | auto-engages on region-band share | MISSING | **[A24]** rulings #19/#23 |
| Doctrine as the steering wheel | **catalog BUILT_AND_GAUGED (D1/D1b, 25 entries)**, behaviour fields read by **nothing** | assignable in formation management | MISSING | D1 `97ecd5b`, D1b `b218acf` — both CI-green **[V]** |
| Leader-modulated doctrine | substrate exists on the SPACE retreat path; **ground has no read** | n/a | MISSING | **[A24]** |
| Calculated fire rate | **MISSING** — weapons carry a flat `Attack`, applied once per **hour** | n/a | MISSING | **[V]** `GroundForcesProcessor.cs:29` |
| Wound model (`CasualtyTier`) + battle-stats ledger | **MISSING** (designed, not built) | n/a | MISSING | ruling #22 **[?]** confirm zero |
| **Ground battle log / events / records** | **MISSING — zero emission anywhere in the resolver** | n/a | **MISSING, and the clock halts anyway → the interrupt lies** | **[V]** `GroundForcesProcessor.cs:329-330` + whole-file grep |
| Unit inspection (hover + Force Management) | partial | **[?]** | partial | ruling #25 |
| Lost-contact fading marker | BUILT for space (`SensorContactIcon`) | — | **MISSING on ground** | ruling #26 |
| Ground behaviour flags | BUILT — but **process statics**; set on New Game (menu *and* DevTest), **never on load** | — | MISSING | **[V]** `GroundForcesProcessor.cs:62/72/85/100`, `NewGameMenu.cs:562-581/979-986` |
| A takeable enemy from the menu | BUILT | **ACCESSIBLE — the ungated "DevTest" main-menu button** (premise corrected) | — | **[V]** `MainMenuItems.cs:51` |
| Orbital bombardment of a colony | **BUILT_INERT** — no `BombardColonyOrder`, no button, no AI rung | Fire-Control workaround only | MISSING | **[A24]** audit P3 |
| Per-faction ground fog | BUILT engine-side | **client ignores it** — a rival's survey reveals your deposits | wrong | **[A24]** |
| Located hex deposits feed mining | **MISSING** — mining reads only the body-wide pool | n/a | drawn but not load-bearing | **[A24]** |
| Units cost people, permanently | MISSING — `CrewReq` computed, never read | n/a | MISSING | **[A24]** ruling #7 |
| Ammo bites | BUILT_INERT — flat 1 kg/salvo, so a magazine is never a trade | n/a | MISSING | **[A24]** ruling #8 |
| Hazard counters as specific gear | MISSING | MISSING | MISSING | ruling #5 |
| Per-mini-tile terrain (M4) | **MISSING** — every mini tile copies its coarse hex's terrain | n/a | drawn (as a copy) | Layer 5 M4 |
| Hex/tile naming | MISSING (fully specced, Layer 6) | MISSING | MISSING | Layer 6 |
| Capture transfers substance | **MISSING** — bare owner-ID flip | n/a | MISSING | ruling #21 — **OPEN** |

**The shape of it:** almost nothing in that table is *unbuilt machinery*. The failures cluster in the
**Accessible** and **Observable** columns, and in **data that ships as zero**. That is why this plan front-
loads wiring and gauges, not features.

---

## 3. Sequencing — the principle, and why

**Observable → Accessible → Steerable → Deep.** Justification, in order:

1. **Observability first, and it is not a preference — the clock already halts for a ground battle.** The
   most expensive thing in this project is a change whose effect nobody can see. Every later slice here
   (fire rate, doctrine steering, retreat, casualties) is a *tuning* problem, and you cannot tune what you
   cannot read. S1 is also the cheapest slice in the plan. **Do it first.**
2. **Accessible second**, because a runtime pass (the P2 sitting) is the only gauge CI structurally cannot
   provide, and it needs a reachable door plus a readable log to be worth running.
3. **Then the live sitting (M1)** — before any depth. Everything after it is deepening a system that has
   actually been observed working once, instead of one that merely compiles.
4. **Steering (doctrine) before fire-rate**, because "everything goes through the doctrines" is your frame:
   the fire behaviour in #23/#18 wants a doctrine priority list to fire *down*. Building the rate first
   means building it twice.
5. **Depth last**, in cradle-to-grave order, gated on the forced order #2 → #4 → #1.

**One hard pairing, do not break it:** ruling #9 (kill the free path) and ruling #10 (the one queue) **must
land in the same slice**, because the free path is today the *only* producer of a building that actually
fortifies. Ship #9 alone and ground defence silently breaks. **[A24]**

---

## 4. THE SLICES

Every slice: one push, one CI gate (**~33 min** — the `rest` shard is the critical path **[V]**, and
`ci.yml`'s complement filter puts every new fixture there, so each slice asks "does this fixture need its
own shard?").

Legend — **Size:** cheap-wire / medium / large. **Gate:** what CI asserts. **Reach:** the literal click
path. **See:** what proves it live.

### S0 — Settle the surface scale with a measurement, not an argument · cheap-wire
Three docs disagree (~560 km/47 km vs ~477 km/37 km). Diagnosed **[V]**: they measure different things —
37 km is `pitch ÷ 13` and is **what the resolver actually measures** (`GroundMiniHex.cs:28-34`,
`GroundForcesProcessor.cs:612/749`); 47 km is an area-equivalent no code uses. The coarse number depends on
Earth's generated hex count, which **cannot be settled by reading** (no SDK here, and two hex models
coexist).
- **Build:** a CI readout that prints real `HexPitchKm` + `MiniPitchKm` for Earth / Mars / Luna.
- **Gate:** the readout exists and the two are related by exactly ÷13.
- **See:** the numbers in the CI log. Then quote the measurement in all three docs.
- **Deps:** none. **Why first:** it's tiny, it pays a doc debt with evidence, and it's the Visibility Gate
  in miniature — build the gauge, *then* state the number.

### S1 — ⭐ THE GROUND BATTLE LOG (#24) · cheap-wire · **START HERE**
The keystone. Mirror the space pattern exactly **[V]**: `CombatEngagement.CombatLog()` gated on a static
`NarrateToLog` (`CombatEngagement.cs:352,452-455`; the client sets it at `PulsarMainWindow.cs:77`), plus
the unconditional structured trail in `Combat/BattleLog.cs`.
- **Build:** `[Ground]`/`[GroundCombat]` narration at engage / salvo / loss / region-flip / disengage, plus
  a structured record per event. Narrate the halt itself, so the clock stopping explains itself.
- **Design fork to decide in the slice (recorded, not guessed):** `BattleEvent`'s fields are ship-shaped
  (`FleetId`, `ShipsLost` — `BattleLog.cs:29-41`). **Recommendation: add a domain discriminator to
  `BattleEvent` and reuse `BattleLog`**, because the client's existing Battle Report already reads it — so
  ground fights show up in the report for free. That is the CONNECT move rather than a parallel system.
- **Two cautions, both flagged now:** `BattleLog` is explicitly **runtime-only, not saved**
  (`BattleLog.cs:66-68`) — if the after-action report must survive a save that's a separate save-safety
  decision; and `MaxEvents = 250` is sized for fleets, so a big ground fight could evict space history.
  **FLAGGED sizing value.**
- **Gate:** a fixture fights a ground battle and asserts (a) ≥1 record per phase, (b) records name the
  right formations and losses, (c) the narration flag off ⇒ byte-identical.
- **Reach:** Main menu → DevTest (or a menu game with a garrison) → advance until a ground fight → the
  fight narrates into `game_logs/`.
- **See:** `[Ground…]` lines in `game_logs/`, and events in the Battle Report.
- **Deps:** none. **Unblocks:** literally every tuning slice below.

### S2 — The five behaviour flags into the save (#27a) · medium
Corrected premise **[V]**: they're set on the **normal** New Game path too (`NewGameMenu.cs:562-581`), not
just DevTest. The defect is that **loading a save never sets them**, so a save plays differently depending
on whether you started a new game earlier in the same process.
- **Build:** move the five onto saved game settings; New Game writes them, load reads them.
- **Landmine:** `TypeNameHandling.Objects` — **append fields to an existing settings blob** if one exists;
  a brand-new `*DB` introduces a new type name. **[?]** find the right home before building.
- **Gate:** save → load → the five flags match what the game was created with; and a CI run can now test
  the configuration players actually run.
- **See:** a one-line `[STATE]` dump of the five at load.
- **Deps:** none (independent of S1).

### S3 — Promote the scenario start (#27b + audit P1) · cheap-wire · **GATED ON Q2**
Rename/promote the existing ungated **DevTest** main-menu button into a first-class **Scenario / Skirmish**
start. No new machinery **[V]**.
- **Gate:** a `BaseModIntegrity`-style assert that the scenario start yields ≥1 hostile garrison and a
  takeable target, so it can't silently empty out later.
- **Reach:** Main menu → **Scenario** → you have rivals and something to take.
- **See:** the AI tape (`[AI]`) plus S1's ground log.

### S4 — Unstick the designer door (BREAK 3) · cheap-wire (client)
**[V]** `ShipDesignWindow.cs:166` lists only `ShipDesigns`; `:223` documents ground designs as
unreachable; `:592` registers them into `IndustryDesigns`.
- **Build:** list ground designs from `IndustryDesigns` in the picker; let the ground panels render without
  a pre-existing ship design.
- **Gate:** CI `build-client` compile + an engine-level assert that a registered ground design is
  retrievable by the same call the picker uses.
- **Reach:** Main menu → New Game → Entity Assembler → **Ground** → design → save → **reopen it**.
- **See:** local runtime only (CI can't run the client) → a row in `docs/CLIENT-TEST-CHECKLIST.md`.

### ⛳ M1 — THE LIVE CRADLE-TO-GRAVE SITTING (audit P2) · developer, on Windows
**Not a code slice — the milestone that makes everything after it honest.** After S1–S4: one recorded
sitting — survey → colonize → mine → design+build a unit → load → sail → win orbit → land → fight → capture
— capturing `console_output.txt` + `game_logs/`. Rows added to `CLIENT-TEST-CHECKLIST.md` and
`TESTING-TRACKER.md`. **Everything below is deepening an unproven system until this fires once.**

### S5 — ONE build queue, with destinations (#10 + #9 + #6 + #11) · large · **#9 and #10 TOGETHER**
Collapse the divergent build paths into one RTS-style queue where every entry carries its destination and
shows progress; delete the free "Build here" path; make muster a rally-point setting (#6); make "occupies a
tile" and "is a war-map objective" one attribute (#11).
- **Must-not-break:** the free path is the only current producer of a fortifying building **[A24]** — the
  costed queue must write the region list `GroundFortification` reads *in the same slice*.
- **Also closes:** the "building built after game start is located nowhere" hole **[A24]**.
- **Gate:** queue a unit *and* a building with destinations; assert both arrive at the named place, the
  building fortifies its region, and nothing can be created free.
- **Reach:** Colony → Production → queue → destination picker → watch progress.
- **See:** queue progress in the UI + a `[Build]` completion line naming the destination.

### S6 — Movement rework (#14 + #16 + #17) · medium
Delete "march to region" **[A24]** (it never restamped the global position, so the token never moved), move
to the two-layer address `(17,09)(22,47)`, mini-hex moves with the same verb, and show all four march
numbers.
- **Gate:** an order moves a unit and its **global position actually changes**; the four readouts are
  non-zero and consistent (`Speed_kmh` is already real and read **[V]**).
- **Reach:** Force Management → battalion → Move → pick a two-layer address.
- **See:** the token moves; `[Ground]` march lines from S1.

### S7 — Doctrine becomes the steering wheel (D2 → D3a → D3b; #15/#18/#19/#20) · medium ×3
Your frame: *everything goes through the doctrines.* Catalog is built and green (25 entries) but **nothing
reads the behaviour fields** **[V]**.
- **D2:** ground reads the unified catalog; retire `groundStances.json`.
- **D3a (movement):** add `ClosingIntent`; make the maneuver consult doctrine **first**, role as fallback;
  fold `GroundEngagementStance` in; make `SpeedMult` finally bite. Then "Standoff Barrage" can be added.
- **D3b (fire):** target priority (#18), per-doctrine retreat threshold, **the retreat verb + break-away
  timer + the engagement lock** (#15), pursuit-as-doctrine, and **leader modulation** (mirror the space
  read of `OfficerCharacter.Blend`/`TenureWeight`, or record the deferral explicitly).
- **Gate per sub-slice:** two identical forces differing only in assigned doctrine produce **different,
  named** behaviour — and each is visible in the S1 log.
- **See:** the log says which doctrine chose what. *(Without S1 these three slices are unverifiable.)*

### S8 — Shorten the tick, then calculate fire rate (#23) · medium · **GATED ON Q3/Q4/Q5**
Fine-step while a battle is live, hourly otherwise (space's precedent). Then a weapon carries damage/second
and the resolver integrates it over the tick against available targets.
- **Gate:** with rates derived from today's `Attack` ÷ the chosen tick, a reference fight resolves
  **identically to today** (proves the refactor before any tuning), then a rate change moves the outcome
  the expected way. Determinism preserved (fast-forward == watch).
- **FLAGGED:** the tick value, every per-weapon rate, and the overkill rule.

### S9 — The bombardment joint (audit P3) · medium
A region-targeted `BombardColonyOrder` + client button routing into the already-wired
`DamageProcessor.ApplyGroundBombardment`, plus a `ConquerResolver` bombard rung **above** LAND so the AI
softens the beach too **[A24]**.
- **Gate:** an order damages the *targeted region's* defenders; the AI rung fires before landing.
- **See:** a bombardment line in the S1 log; the colony readout loses a building.

### S10 — The designer chain, in the forced order #2 → #4 → #1, then #3 · large
**Order is not negotiable:** penetration/per-shot-energy onto the **weapon** (#2) → research costs (#4) →
*then* delete the three prebuilt templates (#1), with a garrison-composition replacement, because the
prebuilts are today the only carrier of those dials **and** what `GroundStartGarrison` raises. Then block
invalid designs from **saving** (#3).
- **Landmine:** the **JSON atb binder is exact-arity** — adding dials means updating **every** template
  binding that atb in the same change, or they all fail to bind. Six-point registration applies.
- **Gate:** `BaseModIntegrityTests` green with zero skipped entries; a garrison still raises after the
  prebuilts are gone; an invalid design cannot be saved.
- **Pin first [?]:** for each of the three validity gates, which of {computes, displays, blocks assembly,
  blocks save} it does today. The audit and the surveys disagree; that disagreement is the whole size of #3.

### S11 — Depth, cradle-to-grave · large, many small slices
Each is independently shippable: units cost **people** permanently (#7 — `CrewReq` exists, unread);
**ammo bites** (#8); **hazard counters as specific gear** (#5); **`CasualtyTier` + damage ledger** (#22 —
build, don't redesign); **employment jobs + power demand** (#12 — mostly JSON); **semantic tile bonuses**
(#13); **M4 per-mini-tile terrain**; **hex/tile naming** (Layer 6); **client per-faction fog**; the
**grave rung** (pop→0 / rebellion expiry → colony collapse); **hex deposits as the mined truth**; and the
**G6b** single-hex-model cleanup.
- The `SYSTEM-GENERATION` G1–G6 track (spec-file writer, belts/Oort, physics terrain) is **a separate
  doc's build order and none of it is built.** **Recommendation: written deferral** — M4's honest terrain
  variation needs it, so schedule G1–G6 only when planetary diversity becomes the goal.

### S12 — What capture transfers (#21) · **BLOCKED ON Q1 — do not start**

---

## 4b. The executable half — `close-planetary-delta`

The slices above are encoded as a committed, re-runnable workflow:
**`.claude/workflows/close-planetary-delta.js`**. Invoke one slice at a time —
`{slice:"S1"}` — from the branch that owns the work; the session commits and gates CI (~33 min) between
slices. Each slice runs **implement → adversarial verify (default to refuted) → repair → a docs agent that
flips the dashboard rows**, then a **completeness critic** whose findings become the next slice's work list.

Key mapping, kept deliberately in step with §4: **S7 is split into `D2` / `D3a` / `D3b`** (three pushes,
three gauges, matching the doctrine build plan's own names). **S11 (depth) is intentionally not encoded
yet** — it is many small independent slices gated behind milestone **M1**, and writing their prompts before
the live sitting has been observed would be guessing. **S12 refuses to run** and emits the Q1 decision aid
instead.

**The workflow has never been invoked.** It was validated statically only (`node --check`, meta-shape diff
against `.claude/workflows/earthfall-campaign.js`, phases/slice-key audit). Running it starts the build, and
that is the developer's call.

---

## 5. Every balance number this plan introduces (nothing chosen silently)

| Number | Where | Status |
|---|---|---|
| Ground fine-step tick length | S8 | **OPEN (Q3)** — recommend 60 s, not space's 5 s |
| Per-weapon damage/second (all weapons) | S8 | **OPEN (Q5)** — recommend deriving from `Attack` ÷ tick so v1 is behaviour-identical |
| Mid-tick overkill rule | S8 | **OPEN (Q4)** — recommend sequential down the priority list |
| Break-away timer (~1–2 h) | S7 D3b | from ruling #15; exact value FLAGGED |
| `BattleLog.MaxEvents` (250 today) | S1 | FLAGGED — sized for fleets, not battalions |
| Research cost curve vs complexity | S10 | FLAGGED |
| Employment jobs per template; per-capita power demand | S11 | FLAGGED |
| Terrain cost multipliers ("price it, don't ban it") | S11 | FLAGGED — and faction-modulated per Layer 6's law |

---

## 6. What is still owed on the evidence side

The fan-out died, so these remain **unverified** and each is marked **[?]** above. A resumed pass should run
them first (briefs are preserved — `docs/DOCS-AUDIT-2026-07-27.md` §5): the **game-log forensics** of the
2026-07-23 session (what actually ran, what failed, what stayed silent); the **rulings-compliance matrix**
for all 27; the **validity-gate** question that sizes #3; the remaining **doc-claim sweep** (`docs/combat/*`,
the subsystem `CLAUDE.md`s, `MVP.md` / `PLAY-TO-MARS`, the `SYSTEMS-STATUS-AND-TEST-PLAN` retirement); and
the **gauge ledger** truth-check.
