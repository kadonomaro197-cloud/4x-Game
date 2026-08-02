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
- `docs/designers-audit/01-INTERCONNECTION-MAP.md` — §2 per-door capsules, §3 producer→consumer matrix,
  §4 directed-edge ledger. Head start for the matrix — but VERIFY against the current HTMLs (it predates
  the aura door and the weapons/defense single-file rebuilds).

---

## GATES (check as completed; commit updates this file every time)

### STEP 0 — orient + state
- [x] 0.1 Orientation docs read (North Star method, resolver totals, interconnection map)
- [x] 0.2 State file created + committed + pushed

### PHASE 1 — I/O census (one `01-IO-<door>.md` per door)
- [ ] 1.01 weapons
- [ ] 1.02 defense
- [ ] 1.03 chassis
- [ ] 1.04 civic
- [ ] 1.05 command
- [ ] 1.06 enhancers
- [ ] 1.07 industrial
- [ ] 1.08 logistical
- [ ] 1.09 power
- [ ] 1.10 propulsion
- [ ] 1.11 sensors
- [ ] 1.12 aura

### PHASE 2 — verification pass (MYSELF, NO subagents; append `## VERIFIED` to each record)
- [ ] 2.1 verify weapons + defense + chassis + civic
- [ ] 2.2 verify command + enhancers + industrial + logistical
- [ ] 2.3 verify power + propulsion + sensors + aura

### PHASE 3 — master I/O matrix (`02-IO-MATRIX.md`)
- [ ] 3.1 Table A (every sim-reaching variable), Table B (cross-door wires), Table C (Assembler input contract)

### PHASE 4 — the Entity Assembler design HTML (`entityassembler.html`), staged
- [ ] 4a skeleton + CSS + prose bands
- [ ] 4b host/mounting panel (host choice; component × COUNT; separate weapon profiles; model count)
- [ ] 4c budgets + gates (mass/carry, power supply vs draw, crew, ammo)
- [ ] 4d emergent readouts + cost surface + TOTALS handoff
- [ ] 4e final polish

### PHASE 5 — verify, index, close out
- [ ] 5.1 render-verify the HTML (node --check + DOM-stub driving every host + branch)
- [ ] 5.2 DOCS-INDEX rows for all new docs/assembler/ files
- [ ] 5.3 mark all boxes complete + final chat summary

---

## RESUME NOTES (update on every commit — what's done, what's next, any gotcha)
- 2026-08-02: State file created. Next: Phase 1 census. Plan — extract each door's I/O to
  `01-IO-<door>.md` (parallel first-draft allowed in Phase 1), then Phase 2 is my own line-by-line
  verify (no subagents). The census schema each record follows: **A. Inputs** (A.1 chips, A.2 sliders
  with range/default/mapping/formula, A.3 toggles/presets) · **B. Outputs** (label/units/formula/
  honesty-marker/sim-variable/consumer/build-state) · **C. Cross-door reads** · **D. Notes/discrepancies**.
