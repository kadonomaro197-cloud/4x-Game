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

> **DEVELOPER-REQUESTED WORK DONE (2026-08-13): the two base-red economy tests are fixed, the morale term is
> calibrated AND turned ON for the real game.** ✅ The base is greened (cargo + food test fixes, commit `c96dd5f`) —
> the PRE-EXISTING BASE RED carve-out is **retired**; any red is now real. ✅ The A1 employment→morale calibration
> (Option B per-capita demand, `JobsPerCapita = 7.0e-6`, commit `2e06464`) is resolved, and ✅ the term is **flipped
> ON for menu games** via `NewGameMenu` (engine default still OFF → tests byte-identical, commit `7ea23f4`). **⏳
> VERIFIED (2026-08-13):** the morale run `2e06464` completed **FULL SUCCESS (all 7 shards green)** — the base is
> green (cargo + food) and the employment homeworld-band test passed, so the calibration landed near-neutral (**no
> +15 snap**, `JobsPerCapita = 7.0e-6` confirmed); the flag-on run (`7ea23f4`) build-client is green (NewGameMenu
> compiles). The live morale feel is the developer's PC play-test (CLIENT-TEST-CHECKLIST). **NOW BUILDING: B-S5.**
>
> **Phase A + B-S1..S7 are CI-verified; B-S8's fix is re-gating.** The Forces-window foundation is fully in place
> through the All-Forces roster. **B-S6** (`0e0faaa`, per-unit ground rows + engine `AllUnitsFor`): run `31852230485`
> **fully GREEN** → ✅. **B-S7** (`e894b12`, civilian-ship detail — cargo manifest via reused `CargoStorageDBDisplay` +
> `LogiShipperDB` route/state + a survey note): run `31853742806` **fully GREEN** (all 7 shards + build-client) → ✅.
> **B-S8 — aggregate Health + Fuel gauges**: the first push (`d369ebb`) went RED on the `rest` shard — the
> `HealthFraction_Defensive_NullAndComponentless` test fed a bare `Entity.Create()` (an UNMANAGED entity, `Manager ==
> null`), and `TryGetDataBlob` delegates to `Manager`, so it NRE'd instead of returning 1.0. The core invariant test
> (pristine → damaged → destroyed) PASSED, so the accessor's math was right; only the defensive edge was uncovered.
> Fixed in **`5e42d87`** with a `ship.Manager == null` guard (an unmanaged entity now returns 1.0 without throwing,
> honouring the "never throws" contract) — re-gating as run `31856896871`. The NEW engine accessor
> **`ShipHealth.HealthFraction(entity)`** sums a ship's living-component `HealthPercent` over its ORIGINAL design count
> (a destroyed/removed component honestly counts as 0 — not hidden by a mean-of-survivors), CI-gauged by
> `ShipHealthTests`; the roster gained a real **Health column** (ship via that accessor, battalion via `FormationHealth`,
> ground unit via `Health/MaxHealth`, colour-banded); and the ship detail shows **Health % + Fuel %** (Fuel reuses the
> existing `EntityExtensions.GetFuelInfo` fill fraction). **NEXT (once B-S8 is green): B-S9** — stations + colonies as roster rows (with the live-owner
> cross-check for the capture-stale-registry gap) + the assign-commander UI; then B-orders + Phase C/D/E. Phase B
> evolves `FleetWindow.cs` (keep the class name) and is client-heavy — CI compile-checks it but can't runtime-test, so
> each behavior gets a `docs/CLIENT-TEST-CHECKLIST.md` row.
>
> **Phase C recon (2026-08-15, done while B-S8's CI ran — §4 file-disjoint work):** the file-disjoint TIER 3 engine
> welds are mostly PARK items, now in the ADJUDICATION QUEUE — **C-guided (#3)** needs the "which ordnance at build"
> decision (rec: read a representative warhead at build; combat value is cached at build but a launcher's ordnance is a
> runtime loadout), **C-mobility (#5)** is the developer's call on the frame×drive combine (rec: multiply). **C-sensors
> (#4)** has a STALE backlog file:line (`SensorTools.cs:147` doesn't exist at HEAD) and is behaviour-changing (detection
> band re-tune) → its own focused slice, not a fill. So the clean next BUILD stays **B-S9** once B-S8 is green; the
> larger file-disjoint BUILDs (C-run-cost / C-staffing / C-civic, D-units, E-env) each want their own focused slice + CI
> cycle.

---

## HOW TO READ THE SLICE BOARD

Status: ⬜ not started · 🔨 building · ⏳CI in flight · ✅ landed (green). Each row names its owning HTML +
ladder row and, once landed, the commit sha.

---

## SLICE BOARD

### Phase A — the first-five welds (highest impact per line of code)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| A1 | Employment → morale producer (feed the dead morale term via `CrewReq`→`GetTotalJobs`, flag-gated) | `civicderived.html` / ENGINE-WIRING-BACKLOG TIER 2 | ✅ | `892924b` · calibration parked ⚖ |
| A2 | Ground `Penetration` + `PerShotEnergy` carry-through in the ground assembler path | `entityassembler.html` / ENGINE-WIRING-BACKLOG TIER 1 | ✅ | `a676efd` |
| A3 | `ShipRoleTools.ClassifyRole` + surface `GroundRoleComposer.ClassifyRole` (one helper, window+AI) | `forceswindow.html` / FORCES-WINDOW S2 | ✅ | `e91b722` |
| A4 | De-fang the 4 order stubs (no wedge/crash) + park their behavior: `RefuelAction`, `ResupplyAction`, `ServeyAnomalyAction`, `ShipLogisticsOrders` | `forceswindow.html` §10 | ✅ | `72dcc72` · 4 behaviors parked ⚖ |
| A5 | order→ability component-scan table + `AbilitiesOf(entity)` (generalize `Has*Ability`) | `forceswindow.html` §4.5 | ✅ | `761a017` |

### Phase B — the Forces window (evolve `FleetWindow.cs`, keep the class name) — ladder S1→S9

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| B-S1 | Battalions tab → built `AllFormationsFor`, scope `PlayerFaction` | `forceswindow.html` / FORCES-WINDOW S1 | ✅ | `7f96ea1` (build-client green) |
| B-S3 | Make ship-combat-row + battalion-row reusable | FORCES-WINDOW S3 | ✅ | `d0f9df6` (build-client green) |
| B-S4 | One "selected unit" selection abstraction (the load-bearing refactor) | FORCES-WINDOW S4 | ✅ | `edd32d8` (build-client green) |
| B-S5 | New **All Forces** flat roster tab (filters + kind-swapping detail panel) | FORCES-WINDOW S5 | ✅ | `42d01c7` (all 7 shards + build-client green, run 31692300418) |
| B-S6 | Per-individual ground-unit rows + engine `AllUnitsFor` | FORCES-WINDOW S6 | ✅ | `0e0faaa` (all 7 shards + build-client green, run 31852230485) |
| B-S7 | Civilian-ship detail panel (promote logistics manifest/routes) | FORCES-WINDOW S7 | ✅ | `e894b12` (all 7 shards + build-client green, run 31853742806) |
| B-S8 | Aggregate Health + Fuel accessors (the missing ship gauges) | FORCES-WINDOW S8 | ⏳CI | engine `ShipHealth` + `ShipHealthTests` + roster Health column + Fuel readout; `d369ebb` rest RED (defensive test NRE on unmanaged `Entity.Create()`) → guarded `Manager == null` in `5e42d87`, re-gating (run 31856896871) |
| B-S9 | Stations + colonies as rows (live-owner cross-check) + assign-commander UI | FORCES-WINDOW S9 | ⬜ | |
| B-orders | Route the 23 button-only DATA orders + deep categorized menu | `forceswindow.html` §10 | ⬜ | |

### Phase C — the designers + assembler (12 door HTMLs + `entityassembler.html`)

| Slice | What | Owning HTML / ladder | Status | Commit |
|-------|------|----------------------|--------|--------|
| C-run-cost | TIER 2.5 run-cost vector (power/jobs/food/upkeep) | ENGINE-WIRING-BACKLOG TIER 2.5 | ⬜ | |
| C-staffing | TIER 2.6 workforce→production staffing model | ENGINE-WIRING-BACKLOG TIER 2.6 | ⬜ | |
| C-guided | Guided-weapon real warhead (item #3) | `entityassembler.html` / TIER 3 #3 | ⚖ parked | ordnance-at-build decision — ADJUDICATION QUEUE (rec: Option A) |
| C-sensors | Sensors band-match fix (item #4) | `sensorsderived.html` / TIER 3 #4 | ⬜ | ⚠ backlog file:line STALE + behaviour-changing (band re-tune) — needs a focused slice |
| C-mobility | `GroundMobility.SpeedMultForUnit` scale-not-replace (item #5) | `propulsionderived.html` / TIER 3 #5 | ⚖ parked | combine = developer's call — ADJUDICATION QUEUE (rec: multiply) |
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

### ✅ RESOLVED — the two PRE-EXISTING base-red economy tests are FIXED (developer authorized "do the data design call", 2026-08-13)

**Plain English:** when this campaign branched, two tests were **already red** (proven: the docs-only opening commit
failed them identically). The developer authorized the data-design call, so both are now fixed. **Both turned out to
be TEST bugs — the engine and data were CORRECT** — where an older test expectation met a newer, deliberately-built
feature (a "merge-semantic break"). Both fixes are **test-only and byte-identical** (zero engine/data/JSON change),
verified by a parallel investigation workflow + independent source reads that agreed exactly.

1. **`CargoCompartmentTests.EveryResource_IsConsumedBySomething`** — *was Expected: not null, But was: null.* The
   failing assert was **not** the "unconsumed resource" check (that passes); it was the **grade-ladder shape check**
   (`:508-525`) which looks up four deliberately-unwired materials (`stainless-steel-d/-a`, `electronics-d/-a`) via
   the faction's **UNLOCKED** store `data.CargoGoods`. Those four sit on the test's own "awaiting-a-mechanic" list —
   they have no build-with-grade mechanic yet, so they are correctly **never unlocked**, so `CargoGoods.GetAny`
   returns null (a faction's `CargoGoods` starts empty; all materials live in `LockedCargoGoods` until unlocked —
   `FactionDataStore.cs:41/98-99`). **Fix:** the ladder lookup falls back to `LockedCargoGoods` so the shape check
   runs against the authored blueprint regardless of unlock state. **Data call: the four materials STAY LOCKED** —
   unlocking them would create "refining jobs you can queue forever for no reason", the exact thing this audit
   condemns.
2. **`FoodProductionTests.FoodProduction_GraveRung_DestroyingTheFarmReturnsStarvation`** — *was Expected: 1.0, But
   was: 0.0.* The engine is correct: while the farm ran (5000/day grown vs 2000/day eaten),
   `SustenanceProcessor.BankFoodSurplus` banked the ~90,000-unit surplus into the colony's cold store — a
   **deliberate food-supply-line buffer** (its own gauge: `Food_IsAShippableGood_…`). So a destroyed farm doesn't
   starve the colony *instantly*; it starves once reserves run out. The old test expected instant starvation. **Fix:**
   the grave-rung gauge now **drains the banked reserve** after destroying the farm, then asserts total shortage — the
   TRUE grave condition (no production AND no reserves). Food stays a losable capability; the buffer stays intact.

**Files:** `Pulsar4X.Tests/CargoCompartmentTests.cs` (grade-ladder lookup → locked-store fallback) ·
`Pulsar4X.Tests/FoodProductionTests.cs` (drain the banked food before the grave assert; `+using Pulsar4X.Storage`).

**THE CAMPAIGN VERIFICATION PROTOCOL — UPDATED (the base is now GREEN):**
> A slice is **CLEAN** iff its CI run is **fully green — `build-client` + all seven test shards, zero failures.**
> (Before this fix the protocol tolerated exactly two known `rest`-shard failures; that carve-out is retired — any
> red is now real.) Check with GitHub MCP `get_job_logs` on any failed job.

---

### ✅ A1-CALIBRATION — RESOLVED: the employment→morale denominator (was parked 2026-08-13, done 2026-08-13)

**Plain English:** A1 wired up the "do people have jobs?" morale term. The engine now counts a colony's jobs by
adding up every building's operating-crew requirement. The problem: those crew numbers were written as "how many
people it takes to RUN the building" (a factory might list a few thousand, a spaceport up to a million), while a
homeworld has **billions** of people. So "jobs ÷ workforce" comes out near zero — the game would read almost every
colony as **near-total unemployment** and dock up to −40 morale everywhere the moment the term is switched on.

That's why A1 shipped with the term **flag-gated OFF by default** (byte-identical — nothing changes in a current
game). The code works and is tested; what needs YOUR call is the *number*, before the flag is turned on for real.

**The question:** when we turn employment-morale on, what should "full employment" mean?
- **Option A — keep the full workforce as the denominator, and re-tune the building CrewReq numbers.** ❌ **REJECTED.**
  CrewReq is *shared* — the ship-crew gate (`ManpowerTools.ResolveBuild`) and talent draws read the same number, so
  inflating it to billions-scale corrupts ship crewing (Prime-Directive violation). And it can't scale: one factory
  can't employ 4.1 billion; you'd need absurd per-building numbers that still don't track population.
- **Option B — a per-capita job DEMAND denominator.** ✅ **CHOSEN.** Measure jobs against `population × JobsPerCapita`
  — the SAME shape the trusted `SustenanceProcessor` uses for food/power. The denominator now **scales with
  population**, so it isn't a magic number tied to one colony's size; a populous colony that under-builds industry
  reads a deficit, building more climbs toward the +15 bonus.
- **Option C — leave it OFF.** Superseded; the calibration is done (below), but the live on-switch stays the
  developer's (see the flag note).

**✅ RESOLVED (developer authorized "do the morale cal", 2026-08-13).** Chose **Option B**, verified by a parallel
investigation workflow + an adversarial review (which caught two defects in the first draft — a `0`-default that would
have red-lit the existing flag-gate test, and a fragile band assertion — both fixed). **The number:** the fully-built
start homeworld has **~52,000** installed jobs against 8.2 billion people; `JobsPerCapita = 7.0e-6` makes the per-capita
demand ≈ 57,400, so the homeworld reads a ratio just under 1 → a **mild employment deficit (near-neutral)** — lifted off
the −25 catastrophe, and NOT the **earned** +15 full-employment bonus (a thriving world is earned by over-building, per
the locked design). `JobsPerCapita` is a **mutable static** (`ColonyMoraleDB.JobsPerCapita`) so a scenario node / the
DevTools Society lever can retune it. **Files:** `ColonyMoraleDB.cs` (the coefficient) · `PopulationProcessor.cs` (both
morale sites) · `StationPopulationProcessor.cs` (the station site) · `EmploymentMoraleTests.cs` (the calibration band +
homeworld readout gauge). **✅ TURNED ON for the real game (developer-authorized 2026-08-13):** the engine flag
`PopulationProcessor.EnableEmploymentMorale` still **defaults OFF** (so the ENGINE test suite stays byte-identical — it
builds colonies via a factory, never the menu), but **`NewGameMenu` now flips it ON** for a menu-started game (both
`CreateGameCore` + Quickstart), the SAME default-off/menu-on pattern as `EnableGroundTacticalAI` /
`LegitimacyProcessor.ReadCurrentMorale`. So a real New Game runs the employment term live. **The live feel is the
developer's PC play-test** (CI can't run the client): read the `[a1-employment] HOMEWORLD …` gauge in `game_logs/` and
watch the homeworld's morale/population; tune `JobsPerCapita` (raise → more of a deficit; lower → nearer full employment;
too low snaps to the +15 bonus). One line in `NewGameMenu` reverts it if the live feel isn't right.

---

### ⚖ A4-ORDERS — what should the four "issue-and-do-nothing" orders actually DO? (parked 2026-08-13)

**Plain English:** the Forces window offers (or will offer) four orders that were never finished — they issue and do
nothing. A4 made them **SAFE** (they can no longer jam a fleet's order queue or crash the game clock — real bugs
that are now fixed). But making each one actually *work* needs a design call from you, because each is genuinely
ambiguous:

1. **Refuel a fleet in place** — WHAT is a "supply source" (a friendly colony/station you're parked at? a fleet
   tanker ship?), and must the fleet be physically close enough to receive fuel? (The cargo-transfer code does no
   distance check, so without a rule it would teleport fuel from anywhere.) **My recommendation:** refuel from a
   *co-located* friendly colony/station (same body) that holds the fuel — the natural "top off at the base you're
   at" — reusing the existing, proven `CargoTransferOrder.CreateRefuelFleetCommand`. Say the word and I finish it
   (also needs a one-line client fix so the menu passes the fleet to the order).
2. **Resupply a fleet** — this one has no defined meaning yet. Is "resupply" = reloading missile/ordnance
   magazines? Or an Aurora-style "maintenance supply point" resource that **doesn't exist in this engine at all**?
   **My recommendation:** define it as ordnance/magazine reload (the only thing the engine can actually move), and
   I'll build a `CreateResupplyFleetCommand` mirroring the refuel one.
3. **Survey an anomaly** — this is a near-duplicate of the already-working jump-point survey order (an "anomaly" is
   the same kind of surveyable point). **My recommendation:** DELETE this half-built duplicate and route "survey
   nearest anomaly" through the existing survey order — duplicating survey logic just invites the two to drift.
4. **Ship logistics (per-ship)** — the automated freight market already moves cargo (the base-side bidding loop
   does the real work), so this per-ship order is a *display shim*. **My recommendation:** leave it a shim (it's
   correct as-is); building a second per-ship state machine would duplicate the working market. Only revisit if you
   want manual per-ship logistics control.

**Meanwhile:** Refuel/Resupply currently complete as safe no-ops if issued. If you'd rather they NOT appear in the
menu until finished, I can pull them from the client's order list — your call (I recommend finishing #1 instead).

---

### ⚖ C-GUIDED — which ordnance does a missile ship's auto-resolve firepower assume? (parked 2026-08-15, §6 #3)
**Plain English:** Right now the auto-resolver rates every missile/torpedo launcher at a flat stub (100 kJ/s) instead
of the real warhead, so torpedo ships read far weaker than they are (`entityassembler.html` flags this outright; TIER 3
#3). The obvious fix — "read the mounted warhead" — hits a wrinkle: a launcher's ordnance
(`MissileLauncherAtb.AssignedOrdnance`) is a **runtime loadout** the player/AI assigns later, but a ship's combat value
(`ShipCombatValueDB`) is computed **once at build**, when no ordnance is loaded yet. So at the one read site there is
nothing to read.

**The decision (needs the developer):** which warhead should a missile ship's firepower use?
- **Option A (recommended): read a REPRESENTATIVE warhead at build** — the faction's default/available ordnance design
  for that launcher size — as a proxy for "what this launcher fires." Simple, one read site, no recalc machinery, and it
  fixes the under-count immediately. Downside: it doesn't track a mid-game loadout swap.
- **Option B: recalc combat value when ordnance is assigned** — honest per-loadout, but needs the parked
  "recalc-`ShipCombatValueDB`-on-change" hook (Combat gotcha #2), a bigger wire touching the whole combat-value caching
  model.
- Either way, warhead ENERGY = TNT-equiv mass × ~4.184e6 J/kg, then divided down to the beam kJ–MJ scale (the same open
  calibration `MissileImpactProcessor` carries — I'd mirror its divisor, not invent one).

**Recommendation:** Option A now (a representative-ordnance read at build, calibrated off the impact processor's scale),
with Option B flagged as the v2 loadout-accurate follow-up. Gauge: a torpedo ship's `ShipCombatValueDB.Firepower` scales
with its warhead choice, not the constant. Say the word and I build A.

### ⚖ C-MOBILITY — how should a designed drive combine with the frame's locomotion mode? (parked 2026-08-15, §6 #3)
**Plain English:** The propulsion door sells the four frame locomotion modes (Foot ×1 / Tracked ×2 / Walker ×1.5 /
Hover ×3) as "modes the simulation already reads." But `GroundMobility.SpeedMultForUnit` **replaces** the frame mode with
a mounted drive's `SpeedFactor` outright — so the moment a unit carries a designed drive, whether its frame is Foot or
Hover stops affecting speed at all (the four modes become dead weight; flagged in `GroundCombat/CLAUDE.md` + TIER 3 #5).
The backlog names the fix as **the developer's call on the exact combine.**

**The decision (needs the developer):** how do the frame mode and the designed `SpeedFactor` combine?
- **Option A (recommended): MULTIPLY** — `speed = frameMode × SpeedFactor`. A Hover chassis with a good drive is faster
  than a Foot chassis with the same drive; the frame choice stays a real decision. Simplest, matches "the frame matters."
- **Option B: a weighted blend** (e.g. `frameMode × (1 + w·(SpeedFactor−1))`) — lets the designed drive dominate but not
  erase the frame. More dials, needs a weight.

**Recommendation:** Option A (multiply). It's the smallest change (one line), makes frame choice matter again, and is
the natural reading of "modes the sim reads." Behavior change → re-baseline the mobility gauges. Say multiply (or a blend
weight) and I build it.

> **Note on TIER 3 #4 (sensors band-match):** the backlog cites `SensorTools.cs:147`, but that path/line is STALE — the
> band-match code isn't there at HEAD (grep before you trust). It's also a *behaviour-changing* detection fix (the ⚠:
> "expect to re-tune emitter/receiver bands so detection works by design not by bug"), so it warrants its own focused
> slice with a sensor-band data pass, not a rushed fill. Locating the real band-match site + checking whether the base
> data relies on the bug is the first step when Phase C reaches it.

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

### B-S8 — aggregate Health + Fuel gauges — ⏳ CI (fix re-gating)

> **CI note (red → fixed):** first push `d369ebb` went RED on the `rest` shard —
> `HealthFraction_Defensive_NullAndComponentless` fed a bare `Entity.Create()`, which is an **unmanaged** entity
> (`Manager == null`); `Entity.TryGetDataBlob` delegates to `Manager`, so it NRE'd instead of returning 1.0. The core
> invariant test (pristine → damaged → destroyed) passed, confirming the accessor's math. Fixed in **`5e42d87`** by
> adding a `ship.Manager == null` guard — an unmanaged entity now returns 1.0 without throwing, honouring the accessor's
> "never throws" contract. Re-gating as run `31856896871`.

**What it does (plain English):** the All Forces roster gets a real **Health** column, and the ship detail panel shows a
ship's **Health %** and **Fuel %**. Before this, the roster had no health readout for ships at all — the game tracked a
ship's damage down at the individual-component level, but nothing added it up into a single "how beat-up is this ship"
number the window could show. This slice builds that number and wires it in. A ground battalion's health and a ground
unit's health already existed (the ground side computes them), so those show too — now every row has a health reading.

**Why it matters:** it's the design's decision #4 (`FORCES-WINDOW-DESIGN.md` §7 / §S8) — *"build the missing aggregate
ship accessors (they don't exist — a gauge-before-UI job) so those columns show real numbers."* The important discipline
here: **the gauge is built in the ENGINE first, where CI can test it**, then the client just reads it. And it's built
*honestly* — a ship's health = the summed integrity of its living components divided by the number the ship was **built
with**, so a component that got blown off in battle (which the damage system deletes) correctly counts as 0 rather than
being quietly ignored by an average of the survivors (which would make a half-wrecked ship read as pristine). Fuel needed
no new engine work — an accessor already existed (`GetFuelInfo`), it was just never surfaced in the roster.

**Files:** `Pulsar4X/GameEngine/Ships/ShipHealth.cs` (NEW) — `ShipHealth.HealthFraction(Entity)`, pure/defensive, reads
`ComponentInstancesDB.AllComponents` + the design's original component count. `Pulsar4X/Pulsar4X.Tests/ShipHealthTests.cs`
(NEW) — pristine = 1.0, a half-damaged component drops it by 0.5/count, a destroyed (removed) component counts as 0, and
null/component-less entities read 1.0 without throwing. `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — a **Health**
column on the roster table (8th column; `RowHealthFraction` dispatches by row kind, `HealthColor` green→red band) + a
**Health % / Fuel %** line in the ship detail (Fuel via the reused `GetFuelInfo`, faction/library read `TryGet`-guarded).
Docs: campaign log, Tests CLAUDE.md (ShipHealthTests row), CLIENT-TEST-CHECKLIST (B-S8 row). (No `GameEngine/Ships/CLAUDE.md`
exists, so the accessor is documented in its own XML docs + here + the Tests inventory.)

**Gauge:** engine `ShipHealth.HealthFraction` → `ShipHealthTests` (CI, `rest` shard). Client Health column + Fuel readout
→ the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S8"): the roster's Health column shows a % per row (colour-banded),
and a ship's detail shows Health % + Fuel %. Fuel only appears for a ship that burns fuel.

### B-S7 — civilian-ship detail panel — ⏳ CI in flight (rest shard) → flip ✅ when green
**What it does (plain English):** in the All Forces roster, clicking a **civilian** ship (a freighter, hauler, tender,
troop transport, survey ship — anything that isn't a warship) now shows what that ship is actually *carrying and doing*,
not just a combat line that reads "Firepower 0" for a ship with no guns. The detail panel gains: its **cargo manifest**
(what's in the holds, and how full each is), its **trade route + state** if it's running an automated logistics route
(what it's hauling, from where to where, and whether it's loading / en route / unloading), and a note if it's a **survey
vessel**. A warship is unchanged — it still shows its firepower/toughness/evasion combat sheet.

**Why it matters:** the design (`FORCES-WINDOW-DESIGN.md` §4.3) calls for the detail panel to *swap by kind* — a warship
shows combat, a civilian ship shows its manifest/route. This is graded **DATA** (not BUILD) because the numbers already
exist — the Logistics window renders them, they were just never surfaced in the roster. So this slice is almost pure
**reuse**: the cargo manifest is the *same* `CargoStorageDBDisplay` panel `EntityWindow` already draws (the exact call,
verbatim), and the route/state come straight off `LogiShipperDB.StateString` + `ActiveCargoTasks`. It makes the roster a
real order-of-battle for the *civilian* half of your fleet, not just the fighting ships.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — `using Pulsar4X.Logistics`; in `DrawRosterDetail`'s ship
branch, `if(!e.Military) DrawCivilianShipReadout(ship)` (after the combat line, so a warship is untouched);
`DrawCivilianShipReadout` (cargo manifest via the reused `CargoStorageDBDisplay.Display` + a resolved `EntityState`;
`LogiShipperDB` route/state + active From→To tasks; a Survey-vessel note); `ResolveEntityState` (the `JumpToPlanetView`
walk-the-system-states idiom). Docs: campaign log, Client CLAUDE.md (All Forces §S7), CLIENT-TEST-CHECKLIST (B-S7 row).

**Gauge:** client-only → the developer's PC play-test (CLIENT-TEST-CHECKLIST "B-S7"): select a civilian ship (a start
freighter) in the roster and confirm its cargo manifest shows, plus route/state if it's on a logistics run; a warship
still shows only the combat sheet. Compile is gated by `build-client` (the reuse call is verbatim from `EntityWindow`).
Thin/defensive: reads only, `TextUnformatted` for user-renamable names (the `%` printf trap), no hard-index, and the
manifest degrades to a one-line note if the ship isn't in the active system view.

### B-S6 — per-individual ground-unit rows + engine `AllUnitsFor` — ⏳ CI in flight (rest shard) → flip ✅ when green
**What it does (plain English):** the All Forces roster (from B-S5) lists your battalions as single rows. This slice lets
you drill into a battalion to see the *individual soldiers/vehicles* inside it — and, importantly, it also surfaces the
**loose units** that aren't in any battalion yet (a freshly-raised garrison unit, a just-landed invader), which the old
"list of battalions" simply couldn't show. A new **"Show individual units"** checkbox on the roster: leave it off and you
get the tidy battalion-level view (unchanged); tick it and each battalion expands to show its member units as indented
child rows (`└`), with the loose units listed below (`•`). Click any unit row and the detail panel shows *that unit's*
stats — its type, its role, health, attack, defense, range, whether it's a veteran, and where it's standing.

**Why it matters:** the design's S6 closes "list *every* unit" (`FORCES-WINDOW-DESIGN.md` §S6). Before this, a unit that
hadn't been formed into a battalion was invisible in the Force-Management window — you couldn't even see it existed there.
The load-bearing new piece is an **engine** helper, `GroundFormationTools.AllUnitsFor(game, factionId)` — the unit-level
twin of the `AllFormationsFor` the Battalions/roster already use — which walks every world and returns *all* of a faction's
ground units, formed or not. It's an engine method, so it gets a real CI test (unlike the client, which CI can't run):
`EfGroundFormUpTests.AllUnitsFor_EnumeratesAcrossBodies_IncludesUnformed_FactionFiltered` proves it enumerates across
multiple worlds, includes a loose unit a formation-walk would miss, and excludes other factions' units.

**Files:** `Pulsar4X/GameEngine/GroundCombat/GroundForcesDB.cs` — new `GroundFormationTools.AllUnitsFor` (mirrors
`AllFormationsFor`, iterates `forces.Units`, read-only/defensive). `Pulsar4X/Pulsar4X.Tests/EfGroundFormUpTests.cs` — the
new gauge. `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — `ForceKind.GroundUnit` + `ForceRef.OfGroundUnit`; a
`GroundUnit` slot on `RosterEntry`; the `_rosterShowUnits` checkbox + the interleave (battalion → its `MembersOf` rows;
then loose units from `AllUnitsFor`); `UnitRow` (builds a unit row — Class via `GroundRoleComposer.ClassifyRole`, Mil/Civ
= `Attack > 0`); `DrawGroundUnitDetail` (the unit stats panel); plus a small printf-safe `RosterDetailHeader` and a
sweep routing user-renamable names/locations through `TextUnformatted` (the one runtime finding the B-S5 adversarial
verification surfaced — a ship/body named with a `%` could garble ImGui's `Text`). Docs: campaign log, Client CLAUDE.md
(All Forces §S6 note), GroundCombat CLAUDE.md (`AllUnitsFor`), CLIENT-TEST-CHECKLIST (B-S6 row).

**Gauge:** engine `AllUnitsFor` → `EfGroundFormUpTests` (CI, `rest` shard). Client per-unit rows → the developer's PC
play-test (CLIENT-TEST-CHECKLIST "B-S6"): tick "Show individual units", confirm a battalion expands to its members + loose
units appear, and clicking a unit shows its stat panel. Default-off keeps the S5 view byte-identical.

### B-S5 — the All Forces flat roster tab — ✅ `42d01c7` (all 7 shards + build-client green, run 31692300418)
**What it does (plain English):** the Force Management window gets a new sibling tab, **"All Forces"**, next to Fleets
and Battalions. It's a single flat list of *everything* you own — every ship AND every battalion, space AND ground —
in one table with the same columns for both: **Unit** (its name), **Domain** (Space or Ground), **Kind** (Ship or
Battalion), **Class** (Warship / Freighter / Survey… for a ship; Line / Artillery / Screen / Support for a battalion),
**Mil/Civ** (is it a fighting unit or a civilian one), **Location** (which system + body, or which world + region),
and **Strength** (firepower for a ship, formation strength for a battalion). So the question "what do I have, and
where is it?" is answered in ONE place instead of hopping between two tabs. Click any row and the panel below swaps to
the right tools for that KIND: a ship shows its combat readout (firepower / toughness / evasion) plus a "Select on
map" jump; a battalion shows the exact same march / queue / stance / ROE order surface the Battalions tab gives.

**Why it matters:** this is the design's "front door" (FORCES-WINDOW §4.2/§4.3) — the whole point of the Force
Management window. The earlier slices built the parts it needs (S2 the class classifier, S3 the reusable rows, S4 the
one-selection `ForceRef`); this slice assembles them into the unified roster a player actually reads. It reuses the
CI-tested engine brains rather than inventing new logic: `ShipRoleTools.ClassifyRole`/`IsMilitary` for a ship's class,
`GroundRoleComposer.ClassifyRole` for a battalion's plurality role, `GroundFormationTools.AllFormationsFor` for the
cross-body battalion list — the "one verb, both seats" classifiers the AI uses too, so the window and the AI agree.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — the "All Forces" tab item (try/catch-wrapped, logs
`[RenderError]` once, still runs `EndTabItem`); `DisplayAllForces()` (gather ships by recursing `PlayerFaction`'s root
`FleetDB` with a fleet-id cycle guard + battalions via `AllFormationsFor`, fold each into a `RosterEntry`; Domain /
Mil-Civ / search filters; the common-column table over `_selRoster`); `DrawRosterDetail()` (kind-swapping — battalion
→ `DrawBattalionOrders`, ship → combat readout + Select-on-map); helpers `AllShipsUnder`/`CollectShips`,
`ShipLocation`, `BattalionLocation`, `FormationClass`; the `ForceDomain` enum + `RosterEntry` struct + `_selRoster`/
filter fields (added in the S4/S5 prep). Docs: `docs/CLIENT-TEST-CHECKLIST.md` (B-S5 roster row).

**Gauge:** compile-checked by `build-client` (the client can't run in CI). Runtime is the developer's PC play-test —
the CLIENT-TEST-CHECKLIST row: open Force Management → All Forces, confirm ships + battalions both list with the right
Class/Mil-Civ/Location/Strength, the Domain/Role/search filters narrow correctly, and clicking a ship vs a battalion
swaps the detail panel to the right tools. Defensive notes baked in: evasion renders as `F2` (not `P0`) to dodge the
ImGui `%` printf trap; every faction/entity read is `TryGet`-guarded; the fleet recursion is cycle-guarded.

### B-S4 — the unified "selected force" (`ForceRef`) — ✅ `edd32d8` (build-client green)
**What it does (plain English):** the Force Management window is going to grow one flat "All Forces" list that mixes
ships, fleets, and battalions in a single table (the next slice, S5). For that to work, the window needs ONE way to
say "this is the thing you have selected" that can point at *any* kind of force — not the three separate,
incompatible selection variables it has today (a selected fleet, a set of selected ships, and a selected battalion
tracked as two loose integers). This slice adds that one thing: a small value called **`ForceRef`** that records the
KIND of force selected (Fleet / Ship / Battalion) plus the id that pins it down — a ship or fleet by its entity id, a
battalion by its (world, formation) pair, exactly how a battalion has always been identified across worlds.

**Why it matters:** it's the "load-bearing refactor" the design calls out — the shared selection model the All-Forces
roster (S5) is built on, and the reason a click on a ship row and a click on a battalion row can live in one table. To
prove `ForceRef` actually works (not dead scaffold), this slice **migrates the Battalions tab's own selection onto
it** — the two loose `_selBattalionBodyId` / `_selBattalionFormationId` integers become one `_selBattalion` `ForceRef`.
That's a **byte-identical** swap: the selection test `IsBattalion(bodyId, formationId)` reproduces the old
`body.Id == … && formation.Id == …` check exactly, and the starting "nothing selected" value matches the old `-1`
defaults. The `OfFleet` / `OfShip` factories are the foundation S5 will use for the other two kinds.

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — new nested `ForceKind` enum + `ForceRef` readonly
struct (`None` / `OfFleet` / `OfShip` / `OfBattalion` + `IsNone` / `IsBattalion`); the Battalions tab's two selection
ints replaced by one `_selBattalion` `ForceRef` at its three sites (field, `isSel` test, click-set). Docs:
`docs/CLIENT-TEST-CHECKLIST.md` (B-S4 byte-identical selection check).

**Gauge:** compile-checked by `build-client` (the refactor's real risk is a type slip). Runtime is **byte-identical**
(same battalion selection behaviour), so no engine test moved and the local check is "battalion selection still works
exactly as before." The `OfFleet`/`OfShip` factories are deliberately not consumed yet — S5 wires them, the same
"foundation for the next slice" pattern as A5's ability scan.

### B-S3 — the ship-combat row + battalion row are now reusable methods — ✅ `d0f9df6` (build-client green)
**What it does (plain English):** the Force Management window draws two tables — the Combat tab's per-ship line, and
the Battalions tab's per-formation line. Until now each was written *inline*, tangled into its own loop, so the coming
"All Forces" tab (one flat list of everything you own) couldn't reuse them without copy-pasting. This slice lifts each
row out into its own small method — `DrawShipCombatRow(ship)` and `DrawBattalionRowColumns(body, forces, formation)` —
so the new roster can call the exact same row-drawing code and every table shows a ship (or a battalion) the same way.

**Why it matters:** it's plumbing for S4/S5 — one place that knows how to draw a ship row, one for a battalion row.
Nothing the player sees changes: the methods draw the exact same columns, from the exact same engine reads, in the
exact same order (a **byte-identical** refactor). The battalion row's *name + selection* deliberately stayed in the
caller, because unifying selection across ships and battalions is the next slice's job (S4).

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — extracted `DrawShipCombatRow(Entity, int)` from
`DisplayFleetCombatSheet`'s loop and `DrawBattalionRowColumns(Entity, GroundForcesDB, GroundFormation, int)` from
`DisplayBattalions`'s loop; both call sites now invoke the methods. Docs: `docs/CLIENT-TEST-CHECKLIST.md` (B-S3
byte-identical render check).

**Gauge:** compile-checked by the `build-client` CI job (the refactor's only real risk is a type/scope slip, which the
compile catches). No engine value changed and the draws are byte-identical, so no engine test moved; the "tables look
the same" confirmation is a local-build glance (CLIENT-TEST-CHECKLIST B-S3).

### B-S1 — Battalions tab reads the built cross-body helper — ✅ `7f96ea1` (build-client green)
**What it does (plain English):** the "Battalions" tab of the Force Management window lists every ground formation you
own across every world — the ground echo of the fleet list. It was gathering that list the hard way: walking every
star system the player currently knows and summing up each world's formations by hand, with a code comment admitting
"there's no engine helper for this yet." That engine helper *does* exist now (`AllFormationsFor` — its own comment
literally names this window as the thing it was built for), so this slice just points the tab at it.

**Why it matters:** it's the studio "one place, one way" discipline — the engine now owns "list all my battalions
across the galaxy" as ONE tested method, instead of the client re-deriving it. The hand-rolled walk could also *miss*
a battalion sitting on a world that had dropped out of the player's known-systems view; the engine helper walks the
real game, so your order of battle is complete. And it's scoped to **PlayerFaction** (the design's call), so the tab
shows YOUR battalions even while a Space-Master session is viewing another faction — before, SM mode showed the
viewed faction's (empty for the Game Master).

**Files:** `Pulsar4X.Client/Interface/Windows/FleetWindow.cs` (`DisplayBattalions` — the gather swapped from the
`StarSystemStates` walk to `GroundFormationTools.AllFormationsFor(game, myFaction)`; `myFaction` now
`PlayerFaction ?? Faction`; each returned body reconstructs its `(system, forces)` via `body.Manager as StarSystem` —
the same cast the position path already uses). Docs: `Pulsar4X.Client/CLAUDE.md` (Battalions-tab stale "no engine
helper yet" note corrected), `docs/CLIENT-TEST-CHECKLIST.md` (B-S1 runtime row).

**Gauge:** the engine helper is already CI-pinned by
`EfGroundFormUpTests.AllFormationsFor_EnumeratesAcrossBodies_FactionFiltered` (cross-body enumeration, faction-filtered,
each paired with its body) — the exact contract this tab now relies on — so no new engine test was needed. The client
change is compile-checked by the `build-client` CI job; its runtime look/feel is the developer's local build
(CLIENT-TEST-CHECKLIST B-S1). **Byte-identical in normal play** (PlayerFaction == Faction, and `AllFormationsFor`
returns the same formations the hand-walk did for known systems); only SM-mode scoping + completeness improve.

### A3 — `ShipRoleTools.ClassifyRole` (the ship role classifier) — ✅ `e91b722`
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

### A2 — Ground `Penetration` + `PerShotEnergy` carry-through (the ground assembler path) — ✅ `a676efd`
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

### A1 — Employment → morale producer (feed the ±40 term "that can never fire") — ✅ `892924b` · calibration parked ⚖
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

### A4 — de-fang the four order stubs (no wedge, no crash) — ✅ `72dcc72` · 4 behaviors parked ⚖
**What it does (plain English):** the Forces window has four orders that were never finished and just do nothing
when issued. Two of them were worse than useless — a real BUG: "Refuel" and "Resupply," once issued, would **jam
the fleet's order queue forever** (the order never marked itself done, and a fleet won't take new standing orders
while its queue is stuck). The other two would **crash the game clock** if ever wired in (they threw an error on
the background thread when the game tried to copy them). A4 removes both landmines: the two now complete cleanly
instead of jamming, and the two crash-throwers are made safe.

**Why the behavior is parked, not finished:** making each order actually *work* needs a design decision from the
developer (what's a supply source, what "resupply" means, whether to finish-or-delete the duplicate anomaly-survey,
whether the per-ship logistics order should exist at all). Those four decisions are in the ADJUDICATION QUEUE with
my recommendation for each. The campaign's own STOP rule covers this: a stub with a real open question and no
documented default gets parked — but I fixed the live bugs (wedge/crash) either way.

**Files:** `RefuelAction.cs` + `ResupplyAction.cs` (de-wedged — `Execute` completes the order so it leaves the
lane); `ServeyAnomalyAction.cs` (was a raw throwing skeleton → now a safe inert shell, real non-throwing `Clone`;
the misspelled class name kept — it's embedded in saves, L3); `ShipLogisticsOrders.cs` (real non-throwing `Clone`,
left as the documented display shim); `Pulsar4X.Tests/OrderStubSafetyTests.cs` (new gauge).

**Gauge:** `OrderStubSafetyTests` — Refuel/Resupply complete after `Execute` (de-wedged, IsFinished flips true);
`ServeyAnomalyAction`/`ShipLogisticsOrders` `Clone()` doesn't throw; the survey order is a safe inert shell.
Engine-only, no JSON drift, no save-break (no class renamed).

### A5 — the order→ability component scan (`AbilitiesOf` + `CanIssue`) — ✅ `761a017`
**What it does (plain English):** an order in the game isn't a free-floating verb — it's powered by a part bolted
to the unit (a survey sensor lets you survey, a jump drive lets you jump, a troop bay lets you load troops). So the
Forces window should offer an order only when the unit actually carries the part. The engine already did this by
hand for three specific cases; A5 turns it into ONE shared tool: `AbilitiesOf(unit)` (the set of parts a unit
carries), an order→part table, and `CanIssue(unit, "GeoSurvey")` that checks them. A fleet's abilities are the
union of its ships' — "can this fleet survey?" = "does any ship aboard carry a survey sensor?"

**Why it matters:** it's the mechanism the deep order menu gates on (Forces-window §4.5), and it's read by the
window AND the AI (one-verb-both-seats). It's cradle-to-grave: install the part → the order appears; lose the part
in battle → the order vanishes. That last bit needed a subtle fix — when a component is uninstalled the engine
leaves an empty entry behind, so `AbilitiesOf` filters on a live part count, or a shot-off sensor would still
grant its order (the grave rung).

**Files:** `GameEngine/Ships/ShipRoleTools.cs` (extended the A3 file — added `AbilitiesOf` + `OrderAbilityTable` +
`CanIssue`); `Pulsar4X.Tests/AbilityScanTests.cs` (new gauge). **Byte-identical:** pure additive read-only helper;
nothing consumes it yet (the window/AI reroute onto it is a later slice, deliberately deferred to keep A5 green).

**Gauge:** `AbilityScanTests` — a surveyor reports GeoSurveyAtb + `CanIssue("GeoSurvey")`; a fleet holding it
reports the same (the union); a non-surveyor can't; clearing the component (the grave-rung stale state) removes the
ability (the Count>0 filter); a troop bay is covered too (it has no *AbilityDB — the case that forced the client's
hand-written scan).

**Phase A is COMPLETE.** All five first-five welds landed (A1 employment · A2 ground-penetration · A3
role-classifier · A4 order-stub safety · A5 component-scan). Two developer decisions parked (A1 calibration, A4
order behaviors). Next: **Phase B** — the Forces window (S1→S9), starting with the roster + the reuse of the
existing `AllFormationsFor`.

---

## SESSION NOTES

- **2026-08-13, session 1 open.** Fresh start (no prior log). Design layer already merged (PR #90). All 17
  HTMLs verified present. Read the four campaign-map docs (DESIGN-TOOLS-INDEX, BUILD-BACKLOG-INDEX,
  ENGINE-WIRING-BACKLOG, DESIGNER-NORTH-STAR method) + CONVENTIONS + the full FORCES-WINDOW-DESIGN. Launched a
  6-agent recon workflow (`wf_98ffad13-d66`) mapping the 5 Phase-A welds + the HTML badges against real source.
  Created this ledger.
