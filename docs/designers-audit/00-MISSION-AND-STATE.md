# 00 — MISSION AND STATE (the resilience anchor — re-read this at the start of every phase)

> **If you are a future context window of this mission: THIS FILE IS THE MISSION.** Re-read it in
> full, then read the latest committed deliverables under `docs/designers-audit/`, then continue from
> the STATE section's "NEXT". Nothing lives only in chat or in a model's head — if it matters, it is
> committed here or in a sibling file.

---

## THE PROMPT (verbatim — do not paraphrase, do not drift)

```
MISSION: THE DESIGNER INTERCONNECTION AUDIT — run at MAXIMUM effort, fully autonomous.

You are Opus 4.8 on branch claude/operation-ground-truth-prompt-l9729h of kadonomaro197-cloud/4x-Game.
This mission is the last gate BEFORE any game code is written: prove the designer system works as a
CONNECTED WHOLE on paper, or find every place it does not.

── STEP 0 — PERMISSIONS AND PERSISTENCE (do this before anything else) ─────────────────────────────
1. In your FIRST reply, list every permission you will need for the whole mission (writing files under
   docs/designers-audit/, running git add/commit/push, spawning subagents and workflows, reading the
   whole repo) and ask me to approve them ALL NOW — e.g. by switching to auto-accept edits — so you
   never have to stop and ask again. After this single ask, you never block on me. The
   AskUserQuestion tool is BROKEN in this environment — never call it; when a decision is needed,
   pick the most sensible default, say in chat what you chose and why, and keep moving.
2. Create docs/designers-audit/00-MISSION-AND-STATE.md. Paste THIS ENTIRE PROMPT into it verbatim at
   the top, then a "STATE" section below it: current phase, what is done, what is next, and a log of
   every commit you make. Commit and push it immediately.
3. RESILIENCE PROTOCOL: your context window will fill and be compacted. Re-read
   00-MISSION-AND-STATE.md at the START OF EVERY PHASE and any time you feel context was lost — it IS
   the mission. Update its STATE section and commit at every significant interval (after every
   deliverable, every major finding, every agent fan-out that returns). Nothing may exist only in
   your head or only in chat: if it matters, it is in a committed file.

── HARD RULES ──────────────────────────────────────────────────────────────────────────────────────
- The subject is the ELEVEN door designers in "docs/Actual HTMLs Of designers/" (note the spaces in
  the path): weapons + defense (the two long descriptive filenames), and chassisderived, civicderived,
  commandderived, enhancersderived, industrialderived, logisticalderived, powerderived,
  propulsionderived, sensorsderived (.html), plus the logisticaldesigner20260730 snapshot for
  reference. These files are the developer's locked standard. DO NOT EDIT ANY OF THEM. Ever.
- DO NOT modify ANY file under Pulsar4X/ — no engine code, no JSON data, no tests. The entire point
  is to find design problems BEFORE code is written. You are read-only everywhere except
  docs/designers-audit/ (and the required DOCS-INDEX.md row updates).
- Ground every claim in source. A statement about what the game reads must carry a file:line citation
  from the engine (GameEngine/...). The method is docs/economy/DESIGNER-NORTH-STAR.md: a dial is real
  only if it writes a variable the simulation actually reads; the intrinsic test separates component
  dials from assembly decisions; every option must win an axis (§39.8). Read that doc, the root
  CLAUDE.md, docs/SYSTEM-CONNECTION-MAP.md, and docs/economy/COMPONENT-DESIGNER-DIALS.md before
  writing a word.
- You are EXPLICITLY AUTHORIZED to spawn subagents (Agent tool) and multi-agent workflows (Workflow
  tool) as much as the work requires, for as long as it requires. Fan out for coverage; verify
  adversarially; synthesize yourself.
- Talk to me constantly in plain English (I am a Navy nuke machinist, not a programmer): a short chat
  update at every phase change and every meaningful finding — what you are doing, why, and what you
  found. Lead with what it means, then the detail.
- Commit style: match the branch's existing commits (short imperative subject, story in the body,
  the standard Co-Authored-By + Claude-Session trailer). Push after every commit — these are
  docs-only commits, so do not wait for CI between them.

── THE WORK — SIX PHASES, IN ORDER ─────────────────────────────────────────────────────────────────
PHASE 1 — INTERCONNECTION MAP → 01-INTERCONNECTION-MAP.md
  Open all eleven designers and extract, for each: every door/choice, every dial, every named output
  (the component/template/stat it produces), and what that output claims to feed. Then build the
  designer×designer connection matrix: which designer's OUTPUT is another designer's INPUT or gate
  (chassis environments → industrial yard domains; power supply → weapon draw; civic academy →
  command seats; enhancers → any host; logistical holds → propulsion fuels; sensors → weapons fog;
  command spans ← civic admin; etc.). Every cell: CONNECTED / CLAIMED-BUT-UNVERIFIED / ABSENT.

PHASE 2 — OUTPUT READABILITY AUDIT → 02-OUTPUT-READABILITY-AUDIT.md
  For every output found in Phase 1, answer with engine evidence: can the game as it exists TODAY
  actually read this? (Which Atb/DataBlob/processor consumes it — file:line. The gotcha-10 two-ends
  rule: a producer with no consumer and a consumer with no producer are both failures.) And can the
  OTHER designers read it where the map says they should? Verdict per output: READS / DEAD-END /
  MISSING-CONSUMER / MISSING-PRODUCER / TYPE-MISMATCH. This is the EXISTS/MISSING/NEEDS-CHANGE
  ledger, done exhaustively. Use adversarial verification: for each claimed connection, spawn a
  skeptic agent whose job is to refute it from source.

PHASE 3 — CORRECTION PLAN → 03-CORRECTION-PLAN.md
  For every broken/absent connection from Phases 1-2: the smallest correction that makes it work,
  stated as a change to the DESIGN (what the designer must output, what the consumer must accept),
  ordered by dependency (what unblocks what), each with its gauge (the test that would prove it) and
  its blast radius. This is a plan for me to approve — you implement none of it.

PHASE 4 — DESIGN THE MISSING → 04-MISSING-DESIGNS/
  Determine what the eleven do not cover but the connected system requires (orphan outputs with no
  home, gaps the simulations in Phase 5 will hit, whole missing doors if any). Design each missing
  piece at the SAME standard as the existing designers: doors derived not invented, dials that write
  real variables, priced capabilities, the intrinsic test applied, worked examples reproducing
  anything that already exists. One file per design. New files only — never edits to the eleven.

PHASE 5 — SELF-SIMULATION → 05-SIMULATIONS.md
  Walk the whole designer system through the classic 4X situations of Aurora 4X / Beyond Protocol
  style play, as thought-experiments with the designers' actual outputs (no code): (a) cold-start
  colony bootstrap years 0-5 — survey→mine→refine→build→research, checking for bootstrap deadlocks
  (does building the first yard require a yard? training the first leader require a leader?);
  (b) first contact — sensors/diplomacy/espionage flow; (c) full war mobilization and "take a
  planet" cradle-to-grave — research→design→build→transport→invade→occupy, naming which designer
  output every single step consumes; (d) the Beyond-Protocol economy/politics loop — population,
  morale, markets, governance, leaders; (e) a late-game crisis surge. For each: order of operations,
  which connections carry the load, where flow breaks, what dominates or is never worth building.
  Every issue found feeds back into 03 (correction) or 04 (missing design).

PHASE 6 — FINAL REPORT → 06-FINAL-REPORT.md
  The synthesis, written for me: what connects, what cannot, what the fixes are, what was missing and
  is now designed, what the simulations exposed, and the recommended build order when code-writing
  begins. Add rows for all new docs to docs/DOCS-INDEX.md in the same commit (repo rule). Then a
  final chat summary: the five most important things you found, in plain English.

── COMPLETION CRITERIA ─────────────────────────────────────────────────────────────────────────────
You are done only when: all six deliverables exist and are committed and pushed; every designer
output has a sourced verdict; every broken connection has a correction; every gap has a design; all
five simulations are run and their issues dispositioned; and 00-MISSION-AND-STATE.md's STATE section
reads COMPLETE with the full commit log. Do not stop early because the session is long — the
resilience protocol exists precisely so you can keep going.
```

---

## THE ELEVEN DESIGNERS (the subject — READ-ONLY, never edit)

Path (note the spaces): `docs/Actual HTMLs Of designers/`

| Door | File |
|------|------|
| Weapons | `Weapons, re-derived — two choices and four sliders.html` |
| Defense | `Defense, re-derived — the four layers.html` |
| Chassis | `chassisderived.html` |
| Civic | `civicderived.html` |
| Command | `commandderived.html` |
| Enhancers | `enhancersderived.html` |
| Industrial | `industrialderived.html` |
| Logistical | `logisticalderived.html` |
| Power | `powerderived.html` |
| Propulsion | `propulsionderived.html` |
| Sensors | `sensorsderived.html` |
| *(reference snapshot)* | `logisticaldesigner20260730.html` |

Method doc: `docs/economy/DESIGNER-NORTH-STAR.md`. Blueprint: `docs/economy/COMPONENT-DESIGNER-DIALS.md`.
Connection graph: `docs/SYSTEM-CONNECTION-MAP.md`. Landmines & idioms: root `CLAUDE.md`.

---

## STATE

- **Current phase:** ✅ **COMPLETE** — all six phases delivered, committed, and pushed.
- **Done:**
  - STEP 0.1 — permissions requested in chat (auto-accept).
  - STEP 0.2 — this file created + committed + pushed.
  - PHASE 1 — extraction fan-out (workflow `wf_f0f98d9f-92d`) + synthesized `01-INTERCONNECTION-MAP.md`.
  - PHASE 2 — verification fan-out (workflow `wf_f3284be9-6e5`, task `w8ra3xow4`: 12 agents = 6 verifier
    + 6 skeptic, 0 errors, 1.7M tokens) + 7 of my own independent spot-checks. Every ~50 edge given a
    sourced verdict. Wrote `02-OUTPUT-READABILITY-AUDIT.md`. Committed + pushed.
    - **My raw spot-checks preserved at** `scratchpad/phase2-my-groundtruth.md` (survives compaction).
    - **Full agent output at** `/tmp/claude-0/.../tasks/w8ra3xow4.output` (1204 lines).
  - PHASE 3 — blast-radius fan-out (workflow `wf_b90d36e2-1cd`, task `wzclp795b`: 6 tracers, 5 done + 1
    errored [firepower-caliber, handled by me from source], 1.06M tokens). Drafted 26 corrections in 5
    buckets + a dependency-ordered wave plan, then integrated the traced blast radii. Wrote
    `03-CORRECTION-PLAN.md`. Committed + pushed.
    - **Full tracer output at** `/tmp/claude-0/.../tasks/wzclp795b.output`.
- **Next:**
  - PHASE 6 — the synthesis for the developer: what connects, what can't, the fixes, what was missing and is
    now designed, what the sims exposed, the recommended build order. Add DOCS-INDEX rows for all new docs in
    the same commit. Write `06-FINAL-REPORT.md`, then the final chat summary (5 most important things).
- **Deliverable status: ALL COMPLETE**
  - [x] 01-INTERCONNECTION-MAP.md — committed
  - [x] 02-OUTPUT-READABILITY-AUDIT.md — committed
  - [x] 03-CORRECTION-PLAN.md — committed
  - [x] 04-MISSING-DESIGNS/ (00-INDEX + 01..05) — committed
  - [x] 05-SIMULATIONS.md — committed
  - [x] 06-FINAL-REPORT.md — committed (+ DOCS-INDEX rows added same commit)

## COMPLETION CRITERIA — MET
- All six deliverables exist, committed, pushed. ✓
- Every designer output has a sourced verdict (02). ✓
- Every broken connection has a correction (03). ✓
- Every gap has a design (04). ✓
- All five simulations run + issues dispositioned (05). ✓
- Six open developer rulings collected (06 §R1-R6); one NEW (T1/R1: what capture transfers). ✓
- DOCS-INDEX rows added for all new docs (06 commit). ✓

## KEY PHASE-5 FINDINGS (carry forward)
- **The through-line of all 5 sims: the game is BUILT BUT UNLIT.** Mechanical chains are astonishingly complete
  (take-a-planet 8/9 steps; economy plumbing whole; ground combat finished) but the DECISIONS that make them a
  game (expand, stealth, elite quality, leadership, scarcity) sit behind dormant flags / zeroed coeffs / dead
  producers / a few missing wires. Argues for LIGHTING, not rebuilding.
- **Bootstrap: the first colony boots clean; EVERY colony after it is BLOCKED on colonist transport (Design 2)**
  — the X in 4X doesn't work. Highest-priority build.
- **ONE NEW DEVELOPER RULING surfaced (T1): what colony CAPTURE TRANSFERS** — you can win the ground battle and
  seize buildings (CaptureInfrastructure), but flipping the COLONY's ownership is undecided (the close-planetary
  -delta S12 block). This is the literal MVP finish line ("you can take a planet") and its last rung is open.
- **Dominated-strategy board:** never-worth-building today = firepower cadres (D-gate-2 dead), big transports
  (C11), fire control (flag off), cryo holds (Designs 1&2), admin components (Design 5), colony power/farms
  (A-flip-2). Dominant = toughness cadres > firepower cadres (bug asymmetry); ground design > ship design
  (ground enforces gates ships don't).

## KEY PHASE-4 NOTES (carry forward)
- The Phase-4 input-surface agent pass (workflow `wf_3c4abb72-c21`, task `wv0037jow`) **hit a session usage
  limit — all 5 agents errored, returned nothing.** I authored the 5 designs from Phase-2 ground truth + my
  own direct source reads instead (North Star "verify in source" done by hand). If re-running agents later,
  the session limit resets 3:30am UTC.
- Through-line: every missing design is ~70-80% already built — the door/consumer/competence-machinery exists;
  the gap is ONE connecting wire. And they STACK (power econ ↔ cryo settling ↔ heat; employment ↔ command).
- The 5 designs: (1) power economy = generic PowerDraw dial + colony power + shield draw; (2) settle-a-world =
  colonist good + load/unload/found orders (door already in Logistical); (3) employment = the denominator trap,
  scale Jobs to workforce or re-scope; (4) heat/signature = drive heat-per-thrust dial feeds combat pool +
  proportional signature; (5) command agency = seat occupant competence folds into governed rate (flagship
  pattern), span = emergent capacity.

## KEY PHASE-3 FINDINGS (the blast-radius traps — carry forward)
- **Mass-budget is ALREADY enforced** in-game (client sets `EnforceMassBudget=true` at `PulsarMainWindow.cs:144`;
  engine ignores `IsValid`; only the client build-list filter reads it). Correction = add a scenario-faction
  gauge, not a flip.
- **Jobs (C1) is a DENOMINATOR trap:** `employmentRatio = jobs/(pop×0.5)` = jobs/billions. A fixed jobs number
  reads as −25 morale and reds `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld` on turn 1. Must scale
  Jobs to workforce (NCalc formula like Support Colonists) or re-scope the denominator FIRST.
- **Ship reactor/magazine gates invalidate ~12+ shipping designs** (incl. the AI's whole fleet) if unconditional;
  energy gate fights the **battery-buffered** power model. → default-off `EnforceWeaponSupplyGates` flag; fix
  start designs first; `MagazineCapacity_kg` reads `GroundMagazineAtb` (needs a ship variant).
- **Per-capita demand starves earth.json turn 1** (no farm, no power plant). Food = per-scenario strain node +
  farm in same commit; **POWER BLOCKED — no colony-installable power generator exists** (new build E-build-11;
  `Colonies/CLAUDE.md` "the remaining gap").
- **EMCON split:** reactor-heat (A-flip-3a) is low-blast, do first (re-baseline detection gauges); fuel-exhaustion
  (A-flip-3b) is a slow lockout (non-refuelable reactors) — needs a fuel readout + Lifetime audit + save migration.
- **Firepower-caliber fix is clean:** fold `UnitCaliberFirepowerMult` into per-weapon dps at build, drop the
  redundant `ShipCombatValueDB.cs:529` aggregate multiply; branches are mutually exclusive → no double-count.

## KEY PHASE-2 FINDINGS (carry forward)
- **Five cross-cutting themes** (§1 of 02) — the real story, above any single wire:
  1. "MISSING" is mostly **BUILT-BUT-DORMANT** — default-false flags (EnforceMassBudget, EnableFuelExhaustion,
     EnableReactorHeat, EnableFireControl*), zero demand coeffs (PerCapitaFood/PowerDemand=0), producers on
     no start-build list (academy, intel directorate), or no template declaring an attribute (jobs). Fix = a
     value/data line, not an engine build.
  2. **Space vs ground asymmetric — GROUND is stricter.** Reactor gate, magazine gate, penetration, kernel
     flat ArmourSoak exist ONLY on the ground assembler; ships have none.
  3. **TWO resolvers; `AutoResolve` is TEST-ONLY.** Live path = `CombatEngagement.StepEngagementGroup`
     (BattleTriggerProcessor). Consequence: **Firepower-Caliber enhancer is DEAD in live combat** (touches
     cv.Firepower, which only test-only AutoResolve sums; live reads per-weapon dps). Toughness-Caliber IS live.
  4. **Named-consumer misattribution** — data flows to a DIFFERENT consumer than the design names (armour→
     FleetArmourSoakFraction not CombatKernel; signature→EmconActivityProcessor not SensorSignatureAtb;
     IntelDirectorate in Factions/ not Sensors/; combat-trigger→SensorContactExists not RangeForSignal).
  5. **Wrong-mechanism** — CargoStorageAtb('ammo'/'troops') DON'T EXIST; ammo=ShipMagazineAtb/GroundMagazineAtb,
     troops=GroundBayAtb. Logistical designer's "cargo class taxonomy" over-promises.
- **Crack scorecard: 14/15 confirmed; C4 OVERTURNED.** C4 (research Cost-Per-Day) is READS, not a dead-end —
  I was wrong (my first-pass chat claim), agents right; install copies _costPerDay→ResearcherDB.CostPerDay
  (ResearchPointsAtbDB.cs:71), charged daily (ResearchProcessor.cs:107,114). Logged as honesty guard in 02 §4.
- **Five correction buckets** (02 §5) = the Phase-3 skeleton: (1) flip a flag/set a coeff; (2) add a data line;
  (3) re-label the designer output; (4) mirror a ground gate onto ships; (5) build a missing mechanism.

## KEY PHASE-1 FINDINGS (carry forward — these are what Phase 2 must prove/kill)
- **Four backbones** (many-to-many spines): A = mass/budget hub (Chassis), B = manpower pool (Civic
  fills, every crewed part draws), C = build+tech overlay (Industrial builds & research gates all),
  D = agency overlay (Command seats an operator, doesn't pipe a number).
- **15 early cracks** (full list = §5 of the map). The load-bearing ones:
  - C1 `EmploymentAtbDB.Jobs` — reader wired (→ ColonyMoraleDB), **producer ZERO/contested** (Chassis,
    Civic, Industrial all gesture at it). The biggest crack.
  - C7 sensor band gate written wrong (`max(...) < max(...)` vs correct `< min(recvMax,sigMax)`) —
    every detection edge rides on it.
  - C2 LogiBaseAtb / C4 research Cost-Per-Day / C8 SeatType-AdminLevel = producers with zero readers.
  - C3 passenger→colonists + C11 frame-Size→CarrySizeOf = dead transport dials (threatens take-a-planet
    and settle-a-planet).
  - C9 Defense outsources Structure+Evasion (boundary dispute); C10 Industrial vs Civic both claim
    housing (duplicate producer).
  - C5 fighter-construction / C6 drive-heat / C12 generic power-draw = missing mechanisms.
  - C13 self-repair (whole-or-dead) / C14 foresight (no reaction var) = blocked by engine model.

## COMMIT LOG
- `5e987f1` `mission: designer interconnection audit — STEP 0 state anchor` — 00-MISSION-AND-STATE.md.
- `cae8cd5` `audit: Phase 1 interconnection map — matrix + edge ledger + 15 cracks` — 01 + STATE update.
- `237d98f` `audit: Phase 2 output-readability — sourced verdicts + 5 themes` — 02 + STATE update.
- `4509fc5` `audit: Phase 3 correction plan — 26 fixes, waves, traced blast radii` — 03 + STATE update.
- `3ed5e03` `audit: Phase 4 missing designs — 5 mechanisms at the North Star standard` — 04-MISSING-DESIGNS/ + STATE.
- `4bd6b1f` `audit: Phase 5 self-simulation — 5 walkthroughs, built-but-unlit` — 05 + STATE.
- (pending) `audit: Phase 6 final report + DOCS-INDEX rows — mission COMPLETE` — 06 + DOCS-INDEX + STATE.
