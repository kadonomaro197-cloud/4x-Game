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

> **A1/A2/A3 all pushed → gate CI, then build A4 (order stubs).** A3, A2, A1 are committed + pushed (all ⏳CI,
> file-disjoint; A1's calibration parked in the ADJUDICATION QUEUE). NEXT: confirm the three CI runs green (`test` +
> `build-client`) — A3 run 31657640207, A2 run 31658202698, A1's run; fix any red first (my first CI-verified
> compile, so watch closely). Then build **A4** — finish the 4 order stubs (`RefuelAction`/`ResupplyAction` empty
> `Execute`, `ServeyAnomalyAction` throws, `ShipLogisticsOrders` empty) so they actually act BEFORE any menu
> surfaces them (recon result in the workflow journal). Then A5 (order→ability component-scan). Update each slice's
> row + this NEXT ACTION on landing.

---

## HOW TO READ THE SLICE BOARD

Status: ⬜ not started · 🔨 building · ⏳CI in flight · ✅ landed (green). Each row names its owning HTML +
ladder row and, once landed, the commit sha.

---

## SLICE BOARD

### Phase A — the first-five welds (highest impact per line of code)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| A1 | Employment → morale producer (feed the dead morale term via `CrewReq`→`GetTotalJobs`, flag-gated) | `civicderived.html` / ENGINE-WIRING-BACKLOG TIER 2 | ⏳CI | (pending) · calibration parked ⚖ |
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

### ⚖ A1-CALIBRATION — should turning employment→morale ON use the full workforce as the denominator? (parked 2026-08-13)

**Plain English:** A1 wired up the "do people have jobs?" morale term. The engine now counts a colony's jobs by
adding up every building's operating-crew requirement. The problem: those crew numbers were written as "how many
people it takes to RUN the building" (a factory might list a few thousand, a spaceport up to a million), while a
homeworld has **billions** of people. So "jobs ÷ workforce" comes out near zero — the game would read almost every
colony as **near-total unemployment** and dock up to −40 morale everywhere the moment the term is switched on.

That's why A1 shipped with the term **flag-gated OFF by default** (byte-identical — nothing changes in a current
game). The code works and is tested; what needs YOUR call is the *number*, before the flag is turned on for real.

**The question:** when we turn employment-morale on, what should "full employment" mean?
- **Option A — keep the full workforce as the denominator, and re-tune the building CrewReq numbers** so a colony's
  buildings realistically employ a big fraction of its people. (Most faithful to "jobs come from buildings"; most
  data to re-balance.)
- **Option B — divide jobs by a smaller "employable" figure**, not the whole workforce (e.g. only the fraction of
  population actually seeking industrial work), so today's CrewReq numbers land near full employment. (Least
  data-churn; changes what "workforce" means for this term.)
- **Option C — leave it OFF** for now; it's a future-economy nicety, not on the MVP path. (Zero risk; the term
  stays dark until you want it.)

**My recommendation:** **Option A** long-term (it's the honest model — a world's buildings *are* its jobs), but it's
a balance pass, not a one-liner. Until you want to do that pass, the flag stays off (Option C is the safe interim).
Nothing else in the campaign is blocked by this — the wire is built and gauged; only the on-switch waits on you.

---

*(Other slices with genuine ambiguity — two HTMLs contradict, a save-break with no safe pattern, a DECISION-PENDING
with no default, or an HTML number impossible without a redesign — will park here the same way.)*

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

### A1 — Employment → morale producer (feed the ±40 term "that can never fire") — ⏳CI · calibration parked ⚖
**What it does (plain English):** the game has a morale rule for "do people have jobs?" — but it never actually
worked, because nothing in the game ever declared a single job, so the number was always zero. A1 wires it up: a
colony's jobs are now counted from its buildings' operating-crew requirement (a factory that needs 500 crew
provides 500 jobs), exactly as the civic-door design says.

**Why it's flag-gated (and parked):** turning this on is the single biggest live behaviour change in the backlog —
morale feeds migration, tax income, and legitimacy. And there's a calibration wrinkle: the crew numbers were
written as "crew to run the building," which against a billions-population homeworld reads as near-total
unemployment (up to −40 morale everywhere). So A1 ships the term **switched OFF by default**
(`PopulationProcessor.EnableEmploymentMorale`) — a current game is byte-identical — and the "should we turn it on,
and with what denominator?" decision is **parked in the ADJUDICATION QUEUE** above for the developer. The wire is
built and tested; only the on-switch waits.

**Files:** `ComponentInstancesDBExtensions.cs` (`GetTotalJobs` now sums `CrewReq`, `EmploymentAtbDB.Jobs` overrides
— keeps that attribute live, no dead code); `PopulationProcessor.cs` (the `EnableEmploymentMorale` flag + gates at
its 2 morale sites); `StationPopulationProcessor.cs` (the shared gate); `Pulsar4X.Tests/EmploymentMoraleTests.cs`
(new gauge).

**Gauge:** `EmploymentMoraleTests` — the producer now reads a non-zero jobs total on a staffed colony (was 0
before A1); the flag gates the morale term (OFF → neutral 0, byte-identical; ON → the term fires).

**HTML badge:** civicderived.html's "the morale term that can never fire" / "ZERO. Nothing produces it" / Jobs
grade "BUILD-NOW" flipped to reflect the honest state — the **producer is built** and the wire is complete, but the
term is **flag-gated pending the parked calibration** (not "live on every colony," which would mislead since it's
off by default). Backlog item #2 flipped ⬜→✅ (built, flag-gated).

---

## SESSION NOTES

- **2026-08-13, session 1 open.** Fresh start (no prior log). Design layer already merged (PR #90). All 17
  HTMLs verified present. Read the four campaign-map docs (DESIGN-TOOLS-INDEX, BUILD-BACKLOG-INDEX,
  ENGINE-WIRING-BACKLOG, DESIGNER-NORTH-STAR method) + CONVENTIONS + the full FORCES-WINDOW-DESIGN. Launched a
  6-agent recon workflow (`wf_98ffad13-d66`) mapping the 5 Phase-A welds + the HTML badges against real source.
  Created this ledger.
