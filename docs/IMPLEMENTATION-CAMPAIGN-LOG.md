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

> **PHASE A VERIFIED CLEAN → Phase B is live.** All five first-five welds are pushed AND CI-verified: the whole
> branch (A1–A5) leaves the `rest` shard with **exactly the two PRE-EXISTING base-red failures and nothing else**;
> `build-client` and all six other shards are green; every new A1–A5 test passes. My slices add **zero** new
> failures (see PRE-EXISTING BASE RED below for the two-line pass/fail protocol). Parked for the developer: A1
> calibration + the 4 A4 order behaviors (ADJUDICATION QUEUE) + the two base-red economy tests (surfaced, not mine).
> **B-S1, B-S3, B-S4 are ALL pushed and CI-verified** — every one has a **GREEN `build-client`** and the branch shows
> only the two known base-red `rest` failures (nothing new). The Forces-window foundation is now fully in place:
> **S2** the shared classifier (`ShipRoleTools.ClassifyRole`), **S3** the reusable rows (`DrawShipCombatRow` +
> `DrawBattalionRowColumns`), **S4** the unified selection (`ForceRef` with `OfFleet`/`OfShip`/`OfBattalion`, the
> Battalions tab already migrated onto it). NEXT: build **B-S5 — the new All Forces flat roster tab** (FORCES-WINDOW
> S5, BUILD-grade): a new sibling tab beside Fleets + Battalions holding ONE table over every force the player owns.
> **Enumeration (recon done):** fleets = `PlayerFaction.GetDataBlob<FleetDB>().GetChildren()` (the faction entity's
> root FleetDB, the same `factionRoot` the Fleets tab walks at `:128`/`:1990`); ships = recurse that fleet tree
> collecting the `IsValid && !HasDataBlob<FleetDB>()` children (the exact filter used at `:990`); battalions =
> `GroundFormationTools.AllFormationsFor(game, PlayerFaction.Id)`. Fold each into a `ForceRef`. ⚠ **Table shape (per
> design §4.2, corrected):** the flat roster is a **COMMON-column** table — Unit / Domain(Space·Ground) / Kind(Ship·
> Formation) / **Class** (`ShipRoleTools.ClassifyRole` for ships, `GroundRoleComposer.ClassifyRole` for formations) /
> **Mil-Civ** (`ShipRoleTools.IsMilitary`) / Location(`PositionDB` system·body / `LeaderRegion`) / Strength
> (`ShipCombatValueDB.Firepower` / `FormationStrength`) / Order — **NOT** the kind-specific S3 rows. Health is BUILD-space
> (needs the S8 aggregate accessor) → show "—" for ships this slice. So B-S5 writes a NEW common-column `DrawRosterRow`;
> the **S3 rows + whole panels serve the DETAIL panel (§4.3), which swaps by `_selRoster.Kind`:** Ship→warship combat
> sheet (`DisplayFleetCombatSheet`-style) / Formation→`DrawBattalionOrders`, each led by a "why this Class?" line.
> Filters: Domain / Role(Mil-Civ) / Class / Location / search. Add a `ForceRef _selRoster` field (distinct from
> `_selBattalion`). Phase B evolves `FleetWindow.cs` (keep the class name) and is client-heavy — CI compile-checks it
> but can't runtime-test, so each behavior gets a `docs/CLIENT-TEST-CHECKLIST.md` row. (S6 adds per-unit child rows +
> `AllUnitsFor`; S7–S9 + B-orders follow.)

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

### 🔴 PRE-EXISTING BASE RED — two economy tests were already failing when this campaign branched (found 2026-08-13)

**Plain English:** when this campaign's branch was cut, two tests were **already red** — they broke on the PR #90
merge, *before* I touched a single line. I proved it: my very first commit here was **docs-only** (no code), and
CI failed it on these same two tests. Every one of my five code slices (A1–A5) also fails **only** these same two
and nothing more. So they are not my breakage — but they matter, because they turn the CI board red, and a red
board is exactly the gauge this campaign trusts. I've **surfaced** them here rather than fix them, for two reasons:
they're in the **economy** system (food + cargo) — a different subsystem than this campaign owns (combat / ground
/ ships / UI) — and fixing either is a **data-design decision that's yours**, not a one-line typo.

**The two failing tests (both in the `rest` shard):**
1. `CargoCompartmentTests.EveryResource_IsConsumedBySomething` — *Expected: not null, But was: null.* A data-audit
   gauge that insists every material a colony can hold is **consumed by something** (a recipe, a build cost, or a
   fuel dial). One resource now has no consumer. The fix is a judgment call: either give that resource a consumer,
   or retire it from the data — an economy-content decision.
2. `FoodProductionTests.FoodProduction_GraveRung_DestroyingTheFarmReturnsStarvation` — *Expected: 1.0, But was:
   0.0.* After a farm is built (food supply covers demand → shortage 0), the test destroys the farm and expects the
   shortage to return to total (1.0). It stays at 0.0 — i.e. a **destroyed farm no longer re-triggers starvation**.
   Most likely a food-buffering / stockpile interaction that changed in the merge; diagnosing it means reading the
   `SustenanceProcessor` food-balance path, an economy job.

**Why the merge did it (best read):** both test files were last touched on the *other* side of the PR #90 merge
(`CargoCompartmentTests` in `0db10e3` "fix CI: four breaks…"). This has the shape of a **merge-semantic break** —
a test from one branch meeting data/code from the other — where each side was green alone but the combination
isn't. `b0f005f` (the Sol-JSON fix in the merge) only touched Mars/Mercury/gas-giant data, **not Earth** (where the
test colony lives), so it's probably not the cause; the cause is likely upstream in the merged economy code.

**THE CAMPAIGN VERIFICATION PROTOCOL (how every future slice is judged green — use this every push):**
> A slice is **CLEAN** iff, in its CI run: (a) `build-client` is green, (b) all six non-`rest` shards are green,
> and (c) the `rest` shard fails **exactly these two tests and no others**. Any *third* failure — or a failure in
> any other shard — is **mine** and blocks the slice until fixed. (Check with GitHub MCP `get_job_logs` on the
> `rest` job; the per-test table lists every ❌ by name.)

**My recommendation:** leave them to the economy work / to you — they're outside this campaign's scope and need a
data call. If you'd rather I take a run at them as a one-off "green the base" commit, say so and I'll dig into the
`SustenanceProcessor` + the cargo-consumer audit; but I won't guess at economy content unasked. Nothing in this
campaign is blocked by them beyond needing the two-line protocol above to read the board.

---

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
