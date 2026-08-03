# ENTITY ASSEMBLER — mission state (resumable)

> **A resumed session reads THIS FILE FIRST, then continues at the first unchecked box.**
> Everything committed and pushed to `claude/operation-ground-truth-prompt-l9729h`.
> Started 2026-08-02. Model: Opus 4.8, Max Effort.

## THE MISSION IN ONE LINE
The 12 door-designers model **components**; the resolvers read **totals**. The **Entity Assembler**
is the machine that turns designed components into those totals. Build (1) an I/O census of all 12
doors, (2) a master I/O wiring matrix, (3) the Assembler design HTML the game will later mirror.

## THE 12 DOORS (READ-ONLY — never edit these)
`docs/Actual HTMLs Of designers/`: weaponsderived, defensederived, chassisderived, civicderived,
commandderived, enhancersderived, industrialderived, logisticalderived, powerderived,
propulsionderived, sensorsderived, auraderived (all `.html`).

## KEY ORIENTATION DOCS (already digested this session)
- `docs/economy/DESIGNER-NORTH-STAR.md` — the intrinsic test (dial = settable knowing only the part;
  assembly = needs the whole entity; emergent = depends on the battlefield). The wall between the two designs.
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §6.1 — the TOTALS the resolver reads at build time
  (`ShipCombatValueDB.Calculate`): Firepower = Σ weapon.DamagePerSecond; Toughness = Σ health×100kJ +
  armour×100kJ; Evasion; RoleWeight; ShieldCapacity_J/Regen; Weapons = List<WeaponProfile> (10 fields).
  **This is the Assembler's output contract.**
- `docs/archive/designers-audit/01-INTERCONNECTION-MAP.md` — §2 per-door capsules, §3 producer→consumer matrix,
  §4 directed-edge ledger. Head start for the matrix — but VERIFY against the current HTMLs (it predates
  the aura door and the weapons/defense single-file rebuilds).

---

## GATES (check as completed; commit updates this file every time)

### STEP 0 — orient + state
- [x] 0.1 Orientation docs read (North Star method, resolver totals, interconnection map)
- [x] 0.2 State file created + committed + pushed

### PHASE 1 — I/O census (one `01-IO-<door>.md` per door) — DRAFTS DONE (unverified)
- [x] 1.01 weapons     (draft)
- [x] 1.02 defense     (draft)
- [x] 1.03 chassis     (draft)
- [x] 1.04 civic       (draft)
- [x] 1.05 command     (draft)
- [x] 1.06 enhancers   (draft)
- [x] 1.07 industrial  (draft)
- [x] 1.08 logistical  (draft)
- [x] 1.09 power       (draft)
- [x] 1.10 propulsion  (draft)
- [x] 1.11 sensors     (draft)
- [x] 1.12 aura        (draft)
> Drafts produced by a 12-agent parallel extraction (workflow wf_ed75044e-ee3), each writing its
> `01-IO-<door>.md` to the fixed schema. **Unverified** — Phase 2 is my own line-by-line pass.

### PHASE 2 — verification pass (MYSELF, NO subagents; append `## VERIFIED` to each record)
- [x] 2.1 verify weapons + defense + chassis + civic (all: no corrections; VERIFIED blocks appended)
- [x] 2.2 verify command + enhancers + industrial + logistical
  - command: **CORRECTED** — grid is 27 filled / 5 blank (census said 21/11, was backwards)
  - industrial: engine cross-ref — lab `Cost Per Day` IS read (`ResearchPointsAtbDB.cs:71`), not dead
  - enhancers, logistical: no corrections
- [x] 2.3 verify power + propulsion + sensors + aura (all: no corrections)
  - **PHASE 2 COMPLETE** — 11/12 no corrections; command CORRECTED (27/5 grid);
    industrial engine cross-ref (Cost Per Day is live). All 12 have VERIFIED blocks.

### PHASE 3 — master I/O matrix (`02-IO-MATRIX.md`)
- [x] 3.1 Table A (every sim-reaching variable, 12 doors), Table B (23 cross-door wires + 4 backbones),
      Table C (Assembler input contract: C1 reads · C2 the totals it computes · C3 the 3 assembler-only dials)

### PHASE 4 — the Entity Assembler design HTML (`entityassembler.html`) — DONE
- [x] 4a skeleton + CSS (house style from defensederived) + prose bands (3-kinds-of-number boundary,
      totals handoff, cradle-to-grave, footer)
- [x] 4b host/mounting panel (host forced choice ship/ground/station; 8-component roster × COUNT;
      separate weapon profiles; model-count dial engine-pending)
- [x] 4c budgets + gates (mass/carry/vol, power supply vs draw, crew, ammo — live pass/fail)
- [x] 4d emergent totals (Firepower/Toughness/Evasion/Shield/WeaponProfiles/effHealth) + cost surface
      + TOTALS-handoff band citing ShipCombatValueDB.Calculate §6.1
- [x] 4e verified: node --check + DOM-stub harness driving all 3 hosts + power-gate stress test (0 throws)
> Built as one atomic file (safer than 5 partial edits to one <script>); all stages present + verified.

### PHASE 5 — verify, index, close out
- [x] 5.1 render-verify the HTML (node --check + DOM-stub driving all 3 hosts + power-gate stress test; 0 throws)
- [x] 5.2 DOCS-INDEX: new §4b (Entity Assembler) + "As of" stamp updated (same commit as this close-out)
- [x] 5.3 all boxes complete; final chat summary delivered

## ✅ MISSION COMPLETE (2026-08-02)
All five phases delivered, committed, pushed to `claude/operation-ground-truth-prompt-l9729h`:
- Phase 1+2: 12 verified I/O census records (`01-IO-<door>.md`) — 11/12 no corrections; command corrected (27/5);
  industrial engine cross-ref (Cost Per Day live).
- Phase 3: `02-IO-MATRIX.md` — the master wiring reference (Table A variables · Table B wires+backbones · Table C
  the Assembler's read-vs-compute contract).
- Phase 4: `entityassembler.html` — the design HTML the game mirrors; render-verified.
- Phase 5: DOCS-INDEX §4b + stamp; this close-out.
Nothing implemented in engine/JSON/tests (design-only, as mandated). The 12 door HTMLs were not edited.

## FOLLOW-ON (2026-08-02) — THE RESOURCE LEDGER (`03-RESOURCE-LEDGER.md`)
The cradle rung, requested after the mission: the bill of materials the designers draw from.
- Supply: the 15 defined minerals + 24 materials (from GameData JSON).
- Demand: a 12-agent scan of every resource each door names (workflow wf_77b7a2d3-d92), verified against
  source (Power/Propulsion/Logistical spot-checked).
- Cross-check: large overlap (19 of ~24 concrete refs map to defined resources) — the whole metal/fuel/
  power/structure economy is shared. Confirmed against the doors' own ResourceCost outputs (the census).
- Gaps found: (demand w/o supply) ammunition + the broken **`gallicite`** ref, biomass, reactive/exotic
  armour, myomer/nanite; (supply w/o demand) nickel-steel, lithium-battery, ree-magnetics-to-sensors, +5 more.
- Developed: NEW-1 munitions chain (fixes gallicite), NEW-2 biomass, NEW-3 armour materials, NEW-4 augments,
  NEW-5 exotic substrates (engine-pending), NEW-6 wire-ins — each with a mine→refine→spend acquisition path
  grounded in existing minerals. DOCS-INDEX §4b row added. Design-only; no GameData JSON touched.

## FOLLOW-ON (2026-08-02) — THE ACQUISITION MAP (`04-ACQUISITION-MAP.md`)
"And what about acquisition?" — the companion to the ledger: how you actually GET each resource.
- Geography: where each mineral is (from the abundance tables) — home terrestrial / asteroids (copper,
  nickel) / outer system (fuels, lithium) / comets (water); tungsten the ultra-rare bottleneck.
- The one working chain, source-verified (workflow wf_4b1c080d-126, 5 agents, file:line):
  survey (reveals tonnage — but does NOT gate mining; the mine reads omniscient truth) → mine (auto,
  faction-blind, throttled by infra efficiency + accessibility) → deplete (cubic decay, one-way, never
  refills) → refine (one daily processor, all 3 stages) → build (consumes minerals + materials).
- Strategic routes: settle WORKS; trade moves goods but NO MONEY (can't buy); capture is a bare
  FactionOwnerID flip ("deeper transfer later" — deposits live on the body, an OPEN ruling); salvage
  DOES NOT EXIST (SpawnWreck is a stub). gallicite would FAULT the ordnance build.
- New resources all ride the mining chain except biomass (grown, not mined). DOCS-INDEX §4b row added.

## FOLLOW-ON (2026-08-02) — INPUTS + OUTPUTS VERIFICATION (`05` + `06`, the two ends)
The inputs question (`05-MATERIAL-INPUTS-BY-DOOR.md`): a 13-input taxonomy verified per door — does the game
**provide** everything a component consumes (build + run)? Answer: build side fully charged; run side mostly
built with 3 "free" gaps. Surfaced 3 undefined ordnance minerals (`gallicite`/`duranium`/`mercassium`) — a live
latent bug that faulted missile builds — and **FIXED** them (commit 76860c1: redirected to electronics/steel/
aluminium; verified zero undefined component-cost refs remain; gauge `BaseModIntegrityTests`).
Then the mirror — the outputs question (`06-OUTPUTS-BY-DOOR.md`): for every number a dial WRITES, does the game
**read it back**? Verified by **12 per-door forensic source traces** (one verifier per door via the Agent tool;
Aura by hand — grep `[Aa]ura` = zero engine files), each output cited file:line at its actual reader or proven
dead. Findings:
- **Combat + economy core fully read** (Weapons/Power/Industrial/Propulsion/Sensors/Defense).
- **6 true dead-ends** (AdminLevel · ship-bridge Console Space · Fighter-Construction-Points · Enhancers iface
  cooldown-cut · strike-launch `ParasiteLauncherReady` · Command hex-radius→save-unsafe blob) + ~12 pending.
- **Load-bearing structural finding: ship vs ground resolvers read DIFFERENT output subsets** — a weapon's
  penetration/per-shot/rate and a defense component's shields are LIVE on a ground unit, INERT on a ship (the ship
  path folds armour into one Toughness pool). The Assembler must wire consumers PER HOST.
- **Crack C1 confirmed at source**: the employment morale term is fully wired on the reader side and contributes
  **zero forever** because nothing writes `EmploymentAtbDB.Jobs` (producer absent, consumer live).
- **13 matrix mislabels** corrected in `06`'s corrections table → a `⚠` banner added to `02` Table A. None are
  crashes; all are "which reader fires" (iface has no writer; Defense shields are ground-inline not the ship
  kernel; ammo/troops feed dedicated `ShipMagazineAtb`/`GroundBayAtb`; `LogiBaseAtb` engine-dead/client-live).
- Bonus source facts: Sensors **C7 band-match bug confirmed in engine** (a visible receiver detects an IR reactor,
  `SensorTools.cs:147` ignores `rec.max`); Weapons `HeatPerSecond` is a live field the door forgot to emit.
DOCS-INDEX §4b rows for `05`/`06` added; `02` Table A banner added; "As of" stamp refreshed. Design/verification
only — the only code change in the whole follow-on was the ordnance-mineral data fix (already pushed).

---

## RESUME NOTES (update on every commit — what's done, what's next, any gotcha)
- 2026-08-02: State file created. Then Phase 1 census DRAFTS produced (12 files, one per door) via a
  parallel extraction workflow. Spot-checked `01-IO-weapons.md` against the HTML — schema followed,
  formulas exact, markers correct. Census schema each record follows: **A. Inputs** (A.1 chips, A.2
  sliders with range/default/mapping/formula, A.3 toggles/presets) · **B. Outputs** (label/units/
  formula/honesty-marker/sim-variable/consumer/build-state) · **C. Cross-door reads** · **D. Notes**.
- Phase 2 done (all 12 verified; command corrected to 27/5; industrial Cost-Per-Day is live).
- Phase 3 done — `02-IO-MATRIX.md` written: Table A (per-door sim variables), Table B (cross-door wires +
  backbones + the C9/C10/C1 boundary disputes), Table C (the Assembler's read-vs-compute contract + the
  three assembler-only dials: host · component×count · model-count). The resolver totals in C2 are the
  Assembler's output contract (`ShipCombatValueDB.Calculate`).
- **NEXT: Phase 4** — build `entityassembler.html`, staged (4a skeleton/CSS/prose → 4b host+mounting panel
  → 4c budgets+gates → 4d emergent readouts+cost+totals handoff → 4e polish), committing each stage.
  Copy the house CSS from `defensederived.html`. Host is the first forced choice; mounting = component×count
  with separate weapon profiles; model-count dial (engine-pending); budgets/gates live; cost surface here;
  a TOTALS-handoff section citing the resolver §6.1. No franchise IP. Then Phase 5 (verify render + DOCS-INDEX).
