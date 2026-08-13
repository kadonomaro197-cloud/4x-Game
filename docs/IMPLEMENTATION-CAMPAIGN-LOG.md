# OPERATION BLUEPRINT-TO-STEEL — Implementation Campaign Log

**What this is:** the running ledger for the campaign that turns the 17 HTML design tools
(`docs/DESIGN-TOOLS-INDEX.md`) into real, shipping game features — engine code, client windows, game data.
This file is the **lifeline**: any session can pick up cold by reading this + `git log --oneline -30` and
continuing from **NEXT ACTION**. Updated and pushed with every slice.

**Started:** 2026-08-13. **Branch:** `claude/operation-blueprint-to-steel-6kxkhb` (all work here; push only here).

**The Prime Directive (from the campaign brief):** the HTMLs are the spec. An HTML beats every `.md`. The
HTMLs' own honesty grades (LIVE / DATA / BUILD) are the build orders. Implement the *design*, not the web page
(re-express in C# + ImGui). Build everything the HTMLs specify and nothing beyond them.

---

## NEXT ACTION

> **A2 + A3 pushed → gate CI on both, then build A1 (employment, flag-gated).** A3 (`ShipRoleTools`) + A2
> (ground penetration) are committed + pushed (both ⏳CI, file-disjoint). NEXT: check the CI runs (GitHub MCP
> `actions_list`/`get_job_logs`) for `e91b722` (A3) + the A2 commit; if red, fix first. Then build **A1** —
> Employment→morale producer, flag-gated default-off (`PopulationProcessor.EnableEmploymentMorale`), byte-identical
> off; **PARK the calibration question** in the ADJUDICATION QUEUE (CrewReq-as-jobs reads heavy unemployment vs a
> billions-pop workforce — the developer must decide the denominator before the flag goes on for real). Then A4
> (order stubs), A5 (component scan).

---

## HOW TO READ THE SLICE BOARD

Status: ⬜ not started · 🔨 building · ⏳CI in flight · ✅ landed (green). Each row names its owning HTML +
ladder row and, once landed, the commit sha.

---

## SLICE BOARD

### Phase A — the first-five welds (highest impact per line of code)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| A1 | Employment → morale producer (feed the dead morale term via `CrewReq`→`GetTotalJobs`, flag-gated) | `civicderived.html` / ENGINE-WIRING-BACKLOG TIER 2 | ⬜ | |
| A2 | Ground `Penetration` + `PerShotEnergy` carry-through in the ground assembler path | `entityassembler.html` / ENGINE-WIRING-BACKLOG TIER 1 | ⏳CI | (pending) |
| A3 | `ShipRoleTools.ClassifyRole` + surface `GroundRoleComposer.ClassifyRole` (one helper, window+AI) | `forceswindow.html` / FORCES-WINDOW S2 | ⏳CI | (pending) |
| A4 | Finish 4 order stubs: `RefuelAction`, `ResupplyAction`, `ServeyAnomalyAction`, `ShipLogisticsOrders` | `forceswindow.html` §10 | ⬜ | |
| A5 | order→ability component-scan table + `AbilitiesOf(entity)` (generalize `Has*Ability`) | `forceswindow.html` §4.5 | ⬜ | |

### Phase B — the Forces window (evolve `FleetWindow.cs`, keep the class name) — ladder S1→S9

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| B-S1 | Battalions tab → built `AllFormationsFor`, scope `PlayerFaction` | `forceswindow.html` / FORCES-WINDOW S1 | ⬜ | |
| B-S3 | Make ship-combat-row + battalion-row reusable | FORCES-WINDOW S3 | ⬜ | |
| B-S4 | One "selected unit" selection abstraction (the load-bearing refactor) | FORCES-WINDOW S4 | ⬜ | |
| B-S5 | New **All Forces** flat roster tab (filters + kind-swapping detail panel) | FORCES-WINDOW S5 | ⬜ | |
| B-S6 | Per-individual ground-unit rows + engine `AllUnitsFor` | FORCES-WINDOW S6 | ⬜ | |
| B-S7 | Civilian-ship detail panel (promote logistics manifest/routes) | FORCES-WINDOW S7 | ⬜ | |
| B-S8 | Aggregate Health + Fuel accessors (the missing ship gauges) | FORCES-WINDOW S8 | ⬜ | |
| B-S9 | Stations + colonies as rows (live-owner cross-check) + assign-commander UI | FORCES-WINDOW S9 | ⬜ | |
| B-orders | Route the 23 button-only DATA orders + deep categorized menu | `forceswindow.html` §10 | ⬜ | |

### Phase C — the designers + assembler (12 door HTMLs + `entityassembler.html`)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| C-run-cost | TIER 2.5 run-cost vector (power/jobs/food/upkeep) | ENGINE-WIRING-BACKLOG TIER 2.5 | ⬜ | |
| C-staffing | TIER 2.6 workforce→production staffing model | ENGINE-WIRING-BACKLOG TIER 2.6 | ⬜ | |
| C-guided | Guided-weapon real warhead (item #3) | `entityassembler.html` / TIER 3 #3 | ⬜ | |
| C-sensors | Sensors band-match fix (item #4) | `sensorsderived.html` / TIER 3 #4 | ⬜ | |
| C-mobility | `GroundMobility.SpeedMultForUnit` scale-not-replace (item #5) | `propulsionderived.html` / TIER 3 #5 | ⬜ | |
| C-deadknobs | TIER 4 dead-knob adjudication (items 6–11 — most are STOP items) | ENGINE-WIRING-BACKLOG TIER 4 | ⬜ | |
| C-civic | New civic dials (Jobs / Amenity / PublicOrder / Commerce) per IO census | `civicderived.html` / 02-IO-MATRIX | ⬜ | |

### Phase D — planetary view (`planetview.html` → client planet view + engine) — ladder U1→T2

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| D-units | U1→T2: select/read/move one unit via the ONE queued verb (kill direct-call bypass `PlanetViewWindow.cs:581`) | `planetview.html` / UNITS-ON-THE-MAP | ⬜ | |
| D-stockpile | Per-hex stockpile + hex-to-hex haul (resource-locality ruling) | UNITS-ON-THE-MAP | ⬜ | |
| D-planfn | Remaining PLANETARY-FUNCTIONAL-PLAN slices the HTML badges call for | PLANETARY-FUNCTIONAL-PLAN-2026-07-27 | ⬜ | |

### Phase E — resolver environment hooks (`resolversim.html` + ENVIRONMENT-CONDITIONS-DESIGN)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| E-env | `CombatConditions` read into the shared kernel (space combat stops being env-blind) | `resolversim.html` / ENVIRONMENT-CONDITIONS-DESIGN | ⬜ | |

---

## ADJUDICATION QUEUE (items parked for the developer — §6 STOP conditions)

*None yet.* When a slice hits a genuine ambiguity (two HTMLs contradict, a save-break with no safe pattern, a
DECISION-PENDING with no default, or an HTML number impossible without a redesign), it parks here with a
plain-English question + options + my recommendation, and I keep working every unaffected slice.

Known future parks (from the backlog, not yet reached):
- **TIER 4 items 6–8** (Console Space on ship bridge · chassis √-law slider · Fighter Construction Points) are
  marked DECISION PENDING — likely STOP items when Phase C reaches them.
- **What colony capture transfers** (ground-side ruling) — a documented STOP.

---

## LANDED SLICES — detail log

*(Each landed slice gets a short plain-English entry here: what it does, the files touched, the gauge added,
and the CI run that turned it green.)*

### A3 — `ShipRoleTools.ClassifyRole` (the ship role classifier) — ⏳CI
**What it does (plain English):** the engine now has ONE place that decides what KIND a ship is — warship,
freighter, survey ship, transport, tender, hauler, or bare utility — by reading the parts bolted to the hull
(a weapon → warship, a survey sensor → survey ship, and so on), exactly the way the ground side already reads a
unit's job from its stats. There is deliberately no stored "is this military?" flag (the engine has a dead one
that's never set); the class is DERIVED and live.

**Why it matters:** the Forces window will show this as each ship's "Class" column, and the faction AI already
needs to tell a warship from a freighter. Before this, the AI carried TWO separate copies of that test
(`ConquerResolver.IsWarship` + `DefendResolver.IsWarship`) that could drift apart. Now both **delegate** to the
one shared helper — the studio law "one verb, both seats": the window and the AI classify a ship the same way,
guaranteed.

**Files:** `GameEngine/Ships/ShipRoleTools.cs` (new — the `ShipRole` enum + `ClassifyRole(design)` /
`ClassifyRole(entity)` / `IsWarship` / `IsMilitary`); `ConquerResolver.cs` + `DefendResolver.cs` (their
`IsWarship` now one-line delegators — byte-identical by construction); `Pulsar4X.Tests/ShipRoleToolsTests.cs`
(new gauge). **Byte-identical:** the AI predicate is unchanged (delegation to identical code); the classifier is
otherwise a pure new read nothing consumes yet.

**Gauge:** `ShipRoleToolsTests` — every AI-warship design classifies Warship+Military and every other design
civilian (the byte-identity tripwire for the two delegators); a built ship classifies the same role as its
design; Mil/Civ maps only Warship to Military; null-safe.

**Note on the HTML badge:** the forceswindow.html "Class" column is graded BUILD because it's about the WINDOW
showing the column. The engine classifier (FORCES-WINDOW S2) is now built, but the column badge stays BUILD until
Phase B (S5) actually surfaces it in the window. The ground classifier (`GroundRoleComposer.ClassifyRole`)
already existed; "surfacing" it is window work, also Phase B.

### A2 — Ground `Penetration` + `PerShotEnergy` carry-through (the ground assembler path) — ⏳CI
**What it does (plain English):** a ground weapon you DESIGN in the Entity Assembler (a frame + weapon parts) now
carries its armour-piercing power. Before this, only the pre-built "monolithic" tank/infantry/artillery units
could crack armour — a *player-built* AP gun came out with zero penetration and bounced off plate. Now the
weapon part carries two dials: **Penetration** (how much of the target's armour the shot ignores) and
**PerShotEnergy** (whether it's one big alpha shot that punches through, or a spray of little shots that bounce).

**Why it matters:** it's the root-cause fix the backlog put first — the reason the ground-battle sim had to
hand-type the Tyranids' claw penetration. A player-designed anti-tank weapon now cracks plate a small-arms
weapon of equal firepower bounces off. And it's **per-weapon**: a unit carrying both a rifle and a cannon cracks
plate only with the cannon (the "honest home").

**Files:** `GroundWeaponAtb.cs` (2 new fields + 6th/7th ctor args + Clone); `GroundWeaponMount.cs` (per-mount
fields + copy-ctor); `GroundUnitAssembly.cs` (Result fields + weapon loop + `ToGroundUnitDesign`);
`GroundCombatant.cs:114` (the profile reads the mount's own pen/per-shot — the one behaviour edit);
`installations.json` (all 5 base-mod weapon templates → 7 `AtbConstrArgs` in lockstep, gotcha-6);
`Pulsar4X.Tests/GroundWeaponPenetrationAssemblyTests.cs` (new gauge).

**Flagged values (developer owns):** cannon **20/140** (= the monolithic Armor gun — parity), autocannon 6/40,
energy 10/90, rifle 0/10, claw 0/10. These reproduce the monolithic behaviour for the assembler path; the
developer can retune. Penetration is a **free dial this slice** (not costed in the Mass formula, so
`GroundWeaponAttackCostTests` stays byte-identical); costing it (CONVENTIONS §16) is a flagged follow-up.

**Not byte-identical (intended):** assembled cannon units now crack plate. No existing resolver test fields an
assembled unit, so nothing re-baselined. `GroundWeaponAttackCostTests` + `BaseModIntegrityTests` (the 7-arg JSON
bind sensor) stay green as tripwires.

**Gauge:** `GroundWeaponPenetrationAssemblyTests` — a cannon (authored 20/140) assembles a unit whose design +
mount + resolver profile carry 20/140; a rifle stays 0/10; on a mixed rifle+cannon unit each mount keeps its OWN
pen; and `GroundDamageMatrix.ArmourSoak` lands more with pen than without (AP cracks plate).

**HTML badge:** the entityassembler.html penetration badge already read "LIVE on ground" — A2 makes that claim
true for the assembler path too, so no HTML flip was needed. The backlog item #1 flipped ⬜→✅ (the engine caught
up to the badge).

---

## SESSION NOTES

- **2026-08-13, session 1 open.** Fresh start (no prior log). Design layer already merged (PR #90). All 17
  HTMLs verified present. Read the four campaign-map docs (DESIGN-TOOLS-INDEX, BUILD-BACKLOG-INDEX,
  ENGINE-WIRING-BACKLOG, DESIGNER-NORTH-STAR method) + CONVENTIONS + the full FORCES-WINDOW-DESIGN. Launched a
  6-agent recon workflow (`wf_98ffad13-d66`) mapping the 5 Phase-A welds + the HTML badges against real source.
  Created this ledger.
