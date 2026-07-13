# Documentation Audit — Docs vs. Actual Game Code (2026-07-13)

**What this is:** a full sweep of every document in the repo — all ~84 design/process docs plus the ~20 subsystem `CLAUDE.md` files — checked against what the C# code *actually does today*. It says which docs earn their keep, which are stale/redundant/bloated, and how to reorganize the whole set. Produced by a 125-agent parallel audit: one agent established ground truth for each code subsystem (what's really built vs. dead/stub), one agent audited each doc against that ground truth, then the findings were reconciled per theme and rolled up into the plan below.

> **The one-paragraph version (plain English).** Your docs folder grew the way a ship's logbook does when three watches each keep their own copy of the same gauge readings. The *design writing itself is honest and good* — almost nothing here is wrong about intent, and almost nothing should be deleted. The problem is the **"is it built yet?" notes**, which are scattered across a dozen docs and **nearly all drifted the same direction**: the code shipped ground combat, missiles, damage, diplomacy, espionage, the NPC AI brain, morale/legitimacy, EMCON, and commander bonuses — while many docs still read as if those are unbuilt to-do lists. A cold reader trusting them would **rebuild working systems.** The fix is three moves, not a rewrite: **(1) collapse the biggest duplicate clusters**, **(2) make ONE doc own build-status** and strip the private status columns out of everything else so they can't rot again, and **(3) run a coordinated flip-stale-to-shipped pass** on the ~60 specific false claims found. Net result: a doc set about a third smaller with a single trustworthy answer to "what's built."

---

## 1. The numbers

| Verdict | Count | Meaning |
|---|---|---|
| **KEEP** | 19 | Healthy, load-bearing, accurate — leave alone |
| **KEEP-TRIM** | 34 | Good doc, needs a trim or a small fix (usually a rotted status line) |
| **STALE-FIX** | 26 | Content is worth keeping but makes false build claims that must be corrected |
| **CONSOLIDATE** | 2 | Fold into another doc; the standalone is done |
| **ARCHIVE** | 3 | Superseded snapshots — banner them and move to `archive/` |
| **DELETE** | 0 | Nothing is pure garbage — the design intent is real everywhere |

Bloat: **4 BLOATED · 19 VERBOSE · 47 OK · 14 LEAN.** The bloat is concentrated in the giant files — `SESSION_STATE.md` (117 KB), `DOCS-INDEX.md` (69 KB), `TESTING-TRACKER.md` (62 KB), `COMPONENT-DESIGNER-DIALS.md` (580 KB) — and in the `CLAUDE.md` reference tables (paragraph-long rows).

**The headline: this is a staleness-and-duplication problem, not a bad-docs problem.** Only 3 docs get archived; the rest all survive. The work is correcting drift and merging near-duplicates.

---

## 2. The dangerous stale claims (fix these first)

These are ranked by how badly they'd mislead a reader who trusted them. Every one is a case where the **doc says "not built" but the code says "shipped and wired"** (behavior is often just gated off behind a default-false flag, which is not the same as unbuilt).

1. **`docs/aurora/DIPLOMACY-AND-INTEL.md` and `docs/aurora/GROUND-COMBAT.md` still tell a reader diplomacy, intel, and ground combat don't exist in Pulsar.** All three are built and wired. Highest rebuild risk. Add a pointer to `DIPLOMACY-DESIGN.md`, `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`, and `GroundCombat/CLAUDE.md`.
2. **The whole AI suite frames `NPCDecisionProcessor.Tick` as an empty stub and "fix seat durability" as the paramount unbuilt task.** False. `Tick` is a full decision + planner + diplomacy + espionage + crisis engine (`NPCDecisionProcessor.cs:127-508`); durable seats shipped (`AdminSpaceProcessor.ReconcileSeats`/`DropSeatForComponent`); `GalaxyCrisis` is wired and live. It's **gated OFF by default flags** (`EnableOrderEmission`/`EnableDiplomaticProposals`/`EnableEspionageMirror`/`EnableIntelLedger` = false), not unbuilt.
3. **`ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` and `DOCS-INDEX.md` mark espionage NOT built.** False — built cradle-to-grave (`IntelDirectorateDB`/`Atb` registered in `installations.json`, `componentDesigns.json`, `earth.json`; `IntelDirectorateProcessor` recruits agents; `Espionage.TaskAgent` + `EspionageProcessor` resolve ops; client `IntelligenceWindow`; tests exist). Inert only because default flags are off.
4. **`ARCHITECTURE.md` describes the exact inversion of reality** — it shows the dead `SimpleDamage` beam path as live and `DamageProcessor` as commented out. Truth: `BeamWeaponProcessor.OnHit → DamageProcessor.OnTakingDamage → DealDamageEnergyBeamSim` (DamageComplex) is the live path. Also delete its "Where Ground Combat Would Hook In" section — ground combat is built.
5. **`COMBAT-DESIGN.md`'s bottom "What Already Exists" table (stamped 2026-06-21)** lists Doctrine, Retreat, Auto-resolution, EMCON, Environmental hazards, and Ground-combat-interface as "Not started" — all six are built and wired.
6. **`MORALE-AND-POPULATION`, `GOVERNANCE-AND-DELEGATION`, and `BEYOND-PROTOCOL` cite `ColonyLifeSupportDB` as live population-cap state.** It's **dead code** (never attached by either `ColonyFactory` path, `ReCalcMaxPopulation` never called, no `[JsonProperty]`). Live capacity is `PopulationSupportAtbDB`; live gauge is `ColonySustenanceDB`. Remove every `ColonyLifeSupportDB` reference.
7. **`REALISM-VS-GAMEPLAY-AUDIT.md`'s verdict board** lists EMCON, ship energy draw, commander combat multiplier, nav planner, and diplomacy/IFF as missing levers — all now wired. Re-rank the cheap-wiring list against what actually remains.
8. **`MVP.md` rows C/D/E/F present ground combat, transport, and capture as absent.** Ground combat + capture are wired (`GroundForcesProcessor`); troop transport is engine-complete AND tested (`GroundTransport`/`GroundBayAtb` + `GroundTransportTests`) but **player-unreachable — the single real MVP blocker is the invade-from-orbit UI hook** (plus no enemy auto-placed on Mars, no first-class bombardment order).
9. **`CONVENTIONS.md` and `CLAUDE.md` say mod data lives under `GameEngine/Data/basemod`; no such directory exists.** Correct to `Pulsar4X/GameData/basemod`. Also `CONVENTIONS.md` §1 cites the dead `Industry/InstallationsDB.cs` as the DataBlob-shape exemplar — swap to a live blob (e.g. `Stations/StationInfoDB.cs`).
10. **`CLAUDE.md` gotcha #1 / `Damage/CLAUDE.md` claim asteroid hits use `DamageVeryComplex`.** That path has no gameplay caller; asteroids use the `DamageComplex` path via `EntityDamageProfileDB.AsteroidDamageProfile`. The `DamageVeryComplex` dir is unwired.

### Subsystem `CLAUDE.md` files that misdescribe their own code

Mostly accurate overall, but these carry specific wrong facts to fix (ground-truth agents cited file:line):

- **Tech** — describes a superseded `EntityResearchDB` path; live is `ResearcherDB` + Orders + funding. *(Only outright STALE subsystem doc.)*
- **Factions** — calls `NPCDecisionProcessor` a skeleton (it's a full engine).
- **Movement** — invents an `INavAction` type that doesn't exist; stale `directAttack=false` gotcha.
- **Fleets** — wrong processor kind, wrong `OwnedDB` claim.
- **Colonies** — dead `ColonyLifeSupportDB` + stale installations-UI note.
- **Industry** — phantom `BuildPoints` field, omits the whole `LocalConstruction` path.
- **Logistics** — references a nonexistent `LogisticsCycle.cs`, wrong 6-hour frequency.
- **Sensors** — wrong `SensorAbilityDB` shape.
- **Galaxy** — calls the wired `PopulationSupportAtbDB` a stub.
- **People** — bottom legacy tables say commander bonuses are missing; they're built.
- **GroundCombat** — says `GroundCombatant` is unwired; it's called at `GroundForcesProcessor.cs:340`.
- **Weapons** — still shows the dead `SimpleDamage` placeholder beam path.

---

## 3. How to organize everything (the reorg plan)

### 3a. Proposed folder structure

Today `docs/` is one flat pile of 55+ files. Group it by subject so a reader can find the family they need:

```
repo root  — process, keep in place (all pre-flight required reading)
    CLAUDE.md · CONVENTIONS.md · ARCHITECTURE.md · SESSION_STATE.md

docs/  (vision + the status dashboards, at top level)
    NORTH-STAR-VISION · MVP · REALISM-VS-GAMEPLAY-AUDIT · LIVING-GALAXY-DESIGN · BEYOND-PROTOCOL-REFERENCE
    DOCS-INDEX · TESTING-TRACKER · SYSTEM-CONNECTION-MAP (new) · CLIENT-TEST-CHECKLIST · PLAY-TO-MARS-WALKTHROUGH

docs/combat/     COMBAT-DESIGN · FLEET-COMBAT-CLOSING · WEAPONS-DESIGN (merged) · RESOLVER-DESIGN (merged) · DETECTION-DESIGN · INFORMATION-DELTA
docs/ground/     GROUND-SURFACE-MAP-DESIGN (merged) · GROUND-UNIT-DESIGNER-DESIGN · GROUND-ORDERS-CATALOG-DESIGN (split-out)
docs/economy/    RESOURCES-AND-MATERIALS · OFF-WORLD-INFRASTRUCTURE-DESIGN (merged) · COMPONENT-DESIGNER-{CATEGORIES,DIALS,DIAL-LEDGER} · UNIVERSAL-ASSEMBLY · CAPABILITY-BUILD-PLAN
docs/society/    MORALE-AND-POPULATION · GOVERNMENT-AND-POLITICS · GOVERNANCE-AND-DELEGATION · INFLUENCE-PILLAR · DIPLOMACY · ESPIONAGE-AND-INTELLIGENCE
docs/ai/         AI-BUILD-STATUS-AND-WIRING-MAP (hub) · AI-DECISION-ENGINE · AI-EMERGENT-POLITICS-AND-CRISIS · AI-PERSONALITY-SPEC · AI-CAPABILITY-CATALOG · AI-COMMAND-AND-COMMUNICATION
docs/environment/  ENVIRONMENTS-DESIGN · STELLAR-ENVIRONMENTS-CATALOG
docs/explore/    SITE-ENGINE-DESIGN · EXPLORATION-CONTENT-DESIGN
docs/aurora/     (external spec — keep the whole 14-file family intact; only strip the per-doc "Maps to Pulsar" status columns)
docs/DESIGNER-AUDIT/  (keep intact as one 8-file point-in-time deliverable)
docs/archive/    (new — superseded but kept for provenance, each with a HISTORICAL/SUPERSEDED banner)
```

### 3b. The consolidations (ranked, biggest win first)

1. **AI suite: 12 docs → 6.** The biggest single win. Merge `AI-IMPLEMENTATION-AND-WIRING-MAP` into `AI-BRAIN-BUILD-TRACKER` as the one status+wiring hub (they currently *disagree with each other*); merge `AI-OBJECTIVE-ENGINE` + `AI-MEANS-ENDS-PLANNER` → `AI-DECISION-ENGINE-DESIGN`; merge `AI-ECOSYSTEM` + `AI-GALAXY-AND-CRISIS` + the one live decision from `AI-SUPERCLUSTER` → `AI-EMERGENT-POLITICS-AND-CRISIS`; extract the 21-role roster from `AI-SELF-PLAY` (83 KB of dated conversation) into `GOVERNANCE-AND-DELEGATION` and archive the rest. Keep `PERSONALITY-SPEC`, `CAPABILITY-CATALOG`, `COMMAND-AND-COMMUNICATION` standalone. *(Do this as its own CI-green commit series — it's the riskiest move.)*
2. **Ground: 5 → 3.** Merge `GROUND-COMBAT-MAP` + `GROUND-CITY-AND-WARMAP` + `GLOBAL-HEX-GRID` + the hex-map half of `HEX-GROUND-AND-ORDERS` into one `GROUND-SURFACE-MAP-DESIGN` (region-graph → hex disks → global cylinder → city sub-grid is one continuous story where each later doc supersedes the earlier). Split the order-catalog half of `HEX-GROUND` into a new `GROUND-ORDERS-CATALOG-DESIGN`. Leave `GROUND-UNIT-DESIGNER` standalone. *(Timing trap: fold `GLOBAL-HEX-GRID` only after its migration slice G6b-3 lands — it's the sole source of truth for that slice.)*
3. **Combat: 6 → 4.** Merge `WEAPONS-AND-DODGE` + `WEAPON-TAXONOMY` → `WEAPONS-DESIGN` (the two-axis Nature×Delivery model is the current code truth and supersedes the four-flavors frame); merge `AUTO-RESOLVER-ANATOMY` + `RESOLVER-MERGE` → `RESOLVER-DESIGN`. Keep `COMBAT-DESIGN` as the hub and `FLEET-COMBAT-CLOSING` as the closing-fight tracker.
4. **Extract the connection graph.** `SYSTEMS-STATUS-AND-TEST-PLAN.md`'s "Connected to" column is the Prime-Directive graph that CLAUDE.md mandates consulting and it **exists nowhere else.** Pull it into a lean new `SYSTEM-CONNECTION-MAP.md` FIRST, verify it's complete, THEN retire the old doc's redundant build-status/test columns to `DOCS-INDEX` and `TESTING-TRACKER`. **Delete-before-extract loses the only copy.**
5. **Environment: 3 → 2.** Keep `ENVIRONMENTS-DESIGN` + `STELLAR-ENVIRONMENTS-CATALOG`; fold `HAZARD-DISCOVERY`'s five locked decisions into `Hazards/CLAUDE.md`, then archive it (its whole discover→research→rated-armor loop is built and tested).
6. **Economy.** Merge `COLONY-PROGRESSION` into `SPACE-STATIONS`, rename to `OFF-WORLD-INFRASTRUCTURE-DESIGN` (same off-world-infrastructure frame). Split `RESOURCES-AND-MATERIALS` into a Reference half (current) and a Prescriptions half (superseded by shipped code).
7. **Designer.** Fold `COMPONENT-DESIGNER-STRESS-TEST`'s 12-hole plug catalogue into `COMPONENT-DESIGNER-CATEGORIES` as an appendix, then archive the stress-test. Keep the 3-doc spine (CATEGORIES = the map, DIALS = the dials, DIAL-LEDGER = the code-truth) as a deliberately layered suite.
8. **`CLAUDE.md` dedupe (no doc deleted).** Collapse the paragraph-long Design-Reference rows (especially the AI-suite wall of prose) down to one-line "read-it-when" pointers; let `DOCS-INDEX` own the long descriptions.
9. **Diplomacy/espionage: do NOT merge** — each is internally coherent and separately code-mirrored. Only fold `DIPLOMACY-DESIGN`'s two overlapping 2026-06-30 sections into one.

### 3c. Archive (banner + move to `docs/archive/`)

- **`PLAN.md`** — self-declared superseded 2026-06-26; its state assessment badly understates combat/ground/infrastructure and would steer a reader back onto parked per-pixel-combat paths.
- **`docs/AURORA-GAP-ANALYSIS.md`** — a 2026-06-21 snapshot whose four "Critical Gaps" and "ground-combat 100% gap" are all contradicted by shipped code. Keep as a baseline record with a HISTORICAL banner.
- **`docs/HAZARD-DISCOVERY-RESISTANCE-RESEARCH-DESIGN.md`** — its "no code yet" premise is flatly contradicted; salvage the locked decisions into `Hazards/CLAUDE.md` first.
- (After folding their durable content:) the conversational bulk of **`AI-SELF-PLAY-DESIGN.md`**, the phase-deliverable body of **`OBJECTIVE.md`**, and **`COMPONENT-DESIGNER-STRESS-TEST.md`**.

### 3d. The status-ledger fix (stop the drift at the source)

Three docs all try to be the status board and all three rot: `DOCS-INDEX` (69 KB), `SYSTEMS-STATUS-AND-TEST-PLAN`, and status tables buried in `SESSION_STATE`. **Give each of the three status jobs exactly one owner; everyone else links, not copies:**

- **`DOCS-INDEX.md` owns doc-currency** (is each doc current/stale/superseded). Keep it as the mandated read-first catalog, but trim every Notes cell from a commit-by-commit changelog down to a one-line status blurb, and correct the over-green marks (Tech → stale; flag the ~dozen "mostly-accurate-not-current" subsystem rows instead of blanket green).
- **`TESTING-TRACKER.md` owns test-and-build status.** Fix the CI shard count (3 → 4) and absorb `CLIENT-TEST-CHECKLIST`'s detail so there's one test home (but keep the checklist alive as the live runtime backlog).
- **`SYSTEM-CONNECTION-MAP.md` (new) owns the connection graph** — one row per system, just its connections.
- **`SESSION_STATE.md`** keeps only the recent-session narrative + the durable Lessons section; delete its status tables (it already defers to the dashboards, and it currently lists the already-fixed missile bug as broken).
- The 580 KB **`COMPONENT-DESIGNER-DIALS.md`** is a design authority, not a status doc — leave it, but trim its folded-in origin/rationale appendices now that `DIAL-LEDGER` owns build-state.

---

## 4. The healthy core (keep as-is)

The load-bearing docs that are accurate and must stay: **`CLAUDE.md`** (with two fact-fixes), **`CONVENTIONS.md`**, **`LIVING-GALAXY-DESIGN.md`** (uniquely forbids building an event-engine/director — has already prevented a wrong build twice), **`COMPONENT-DESIGNER-DIAL-LEDGER.md`** (the model doc for the build-vs-designed distinction), **`PLAY-TO-MARS-WALKTHROUGH.md`** (unusually accurate — pinpoints the three real MVP blockers), **`SITE-ENGINE-DESIGN.md`**, **`NORTH-STAR-VISION.md`**, the **`docs/aurora/`** family (once the status columns are stripped), the **`docs/DESIGNER-AUDIT/`** folder, and the shipped-subsystem specs **`MORALE-AND-POPULATION` / `GOVERNMENT-AND-POLITICS` / `GOVERNANCE-AND-DELEGATION`** (design bodies stay; only the status layer refreshes).

---

## 5. Executing this safely (risks)

1. **Renames break the reference tables.** `CLAUDE.md`'s Subsystem Index and Design-Reference tables (and cross-doc links) point at real paths. Before any move/rename, **grep the old filename across all `.md` files** and update in the same commit — the DOCS-INDEX same-commit rule already applies; extend it to a link-sweep.
2. **Merging is where design intent gets lost.** Preserve the durable *why* (the needs-ladder rationale, the two-axis weapon model, the region-graph-over-a-sphere design). **Fold, don't summarize away** — only drop duplicated shipped-code detail and dated build ledgers.
3. **Extract the connection graph before retiring `SYSTEMS-STATUS`** (see 3b#4). Delete-before-extract loses the only copy.
4. **Re-verify each stale-claim fix against current source** — the audit's file:line numbers may have drifted since; the last commit is literally "Crashed while going through component window," so HEAD is mid-motion.
5. **Banner every archived doc** (HISTORICAL/SUPERSEDED at top + flip its DOCS-INDEX row) — an unmarked archive of contradicted docs misleads as badly as a live stale doc.
6. **Do the AI-suite merge as its own CI-green commit series**, not bundled with other clusters — it's the largest, most interlocking move.

---

## Appendix — per-doc verdicts (all 84)

Verdicts: **KEEP** (healthy) · **KEEP-TRIM** (small fix/trim) · **STALE-FIX** (correct false claims) · **CONSOLIDATE** (fold into another) · **ARCHIVE** (banner + move).

| Theme | Doc | Verdict | Bloat | Where it goes |
|---|---|---|---|---|
| PROCESS | `ARCHITECTURE.md` | STALE-FIX | OK | Keep the doc; do a correctness pass in one commit: (1) fix the Space Combat block to Damag |
| PROCESS | `CLAUDE.md` | KEEP-TRIM | VERBOSE | Fix two facts in place: correct line 58 to Pulsar4X/GameData/basemod, and correct gotcha # |
| PROCESS | `CONVENTIONS.md` | KEEP | LEAN | Keep in place at repo root as the canonical style guide. Fix the two stale references in t |
| VISION | `OBJECTIVE.md` | STALE-FIX | OK | Rewrite the phase sections as a status ledger (mark A1/A2, B-damage, and most of C as DONE |
| VISION | `PLAN.md` | ARCHIVE | OK | Move to docs/archive/PLAN-original.md (or docs/superseded/) and reduce the in-repo footpri |
| VISION | `docs/BEYOND-PROTOCOL-REFERENCE.md` | KEEP-TRIM | OK | Keep in place as an inspiration/reference doc, but add a top banner: 'Reference capture —  |
| VISION | `docs/LIVING-GALAXY-DESIGN.md` | KEEP | OK | Keep as-is. Optionally cross-link it as a one-line 'the bar' entry beside NORTH-STAR-VISIO |
| VISION | `docs/MVP.md` | STALE-FIX | OK | Keep MVP.md as the scope firewall, but stop duplicating live build-status inside it — §3's |
| VISION | `docs/NORTH-STAR-VISION.md` | KEEP-TRIM | OK | Keep in place as the top-level VISION doc. Strip the 'Status today' column from the lens t |
| VISION | `docs/PLAY-TO-MARS-WALKTHROUGH.md` | KEEP | OK | Keep in place but cross-link from docs/MVP.md as the living acceptance checklist for the ' |
| VISION | `docs/REALISM-VS-GAMEPLAY-AUDIT.md` | STALE-FIX | OK | Keep the doc, but split its two halves in-place: (1) preserve the framework/rule + 'stop f |
| STATUS | `SESSION_STATE.md` | KEEP-TRIM | BLOATED | Trim to the latest 1-2 session blocks plus a permanent 'Durable Lessons' section; move all |
| STATUS | `docs/CLIENT-TEST-CHECKLIST.md` | KEEP-TRIM | VERBOSE | Keep as the single live runtime punch-list but restructure: (1) delete or move all ✅ PASSE |
| STATUS | `docs/DOCS-INDEX.md` | KEEP-TRIM | BLOATED | Keep as the canonical index but enforce a hard cap on the Notes column (~1 line / ~25 word |
| STATUS | `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` | STALE-FIX | VERBOSE | Keep the doc for its unique value — the system×system 'Connected to' wiring map — but (1)  |
| STATUS | `docs/TESTING-TRACKER.md` | KEEP-TRIM | VERBOSE | Keep as the canonical test ledger but split concerns: (1) move the dated per-session live- |
| COMBAT | `docs/AUTO-RESOLVER-ANATOMY.md` | STALE-FIX | VERBOSE | Do a status pass: flip §4 #1/#2/#3/#5/#6 and §7d #1/#2 from ➕/backlog to ✅ built (cite Wea |
| COMBAT | `docs/COMBAT-DESIGN.md` | STALE-FIX | VERBOSE | Keep as the combat design hub, but: (1) replace the stale 'What Already Exists' table with |
| COMBAT | `docs/FLEET-COMBAT-CLOSING-DESIGN.md` | KEEP-TRIM | OK | Keep in docs/ as the combat-range design spine. Add an 'as of' refresh stamp noting Phases |
| COMBAT | `docs/RESOLVER-MERGE-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep in place as the combat-resolver-merge record. Once slice 5c (bucketing) lands, collap |
| COMBAT | `docs/WEAPON-TAXONOMY-DESIGN.md` | KEEP-TRIM | OK | Keep in place. (1) Update the header status from 'design survey' to 'design LOCKED + built |
| COMBAT | `docs/WEAPONS-AND-DODGE-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep as the canonical dodge/triangle/bucketing design doc but (1) add a one-line header no |
| GROUND | `docs/GLOBAL-HEX-GRID-DESIGN.md` | KEEP-TRIM | OK | Keep as the active G6b-3 tracker now. On G6b-3 completion: (1) delete or archive docs/HEX- |
| GROUND | `docs/GROUND-CITY-AND-WARMAP-DESIGN.md` | KEEP-TRIM | VERBOSE | Refresh the As-of stamp and flip the status line to 'W-track built; C-track C1 substrate b |
| GROUND | `docs/GROUND-COMBAT-MAP-DESIGN.md` | KEEP-TRIM | VERBOSE | Split concerns: keep this as a pure DESIGN-LOCK (the model, schema, the two LOCKED princip |
| GROUND | `docs/GROUND-UNIT-DESIGNER-DESIGN.md` | STALE-FIX | VERBOSE | Keep in place as the ground-unit-designer SoT, but refresh the As-of stamp and flip the bu |
| GROUND | `docs/HEX-GROUND-AND-ORDERS-DESIGN.md` | KEEP-TRIM | OK | Split by track. Fold the H (hex-ground) sections into docs/GLOBAL-HEX-GRID-DESIGN.md as th |
| DESIGNER | `docs/CAPABILITY-BUILD-PLAN.md` | STALE-FIX | OK | Keep as the build spine but do a reconciliation pass against reality: mark slice 4.6 (espi |
| DESIGNER | `docs/COMPONENT-DESIGNER-CATEGORIES.md` | KEEP-TRIM | OK | Keep as the map half of the two-doc designer reference, but add a short 'Build state as of |
| DESIGNER | `docs/COMPONENT-DESIGNER-DIAL-LEDGER.md` | KEEP | OK | Keep as the canonical 'what's actually wired' ledger for the component designer and cross- |
| DESIGNER | `docs/COMPONENT-DESIGNER-DIALS.md` | KEEP-TRIM | BLOATED | Keep as the DESIGNER design authority but shrink it: (1) delete or archive the folded-in o |
| DESIGNER | `docs/COMPONENT-DESIGNER-STRESS-TEST.md` | CONSOLIDATE | OK | Fold the Part 3 hole→plug ledger and the Part 2 'shared EFFECT bus / gear=designer vs bein |
| DESIGNER | `docs/DESIGNER-AUDIT/00-EXECUTIVE-SUMMARY.md` | KEEP-TRIM | OK | Keep in place as the DESIGNER-AUDIT front page. Update line 136 to drop/repoint the non-ex |
| DESIGNER | `docs/DESIGNER-AUDIT/01-DESIGNER-UIS.md` | KEEP-TRIM | OK | Keep in place as part 1 of the DESIGNER-AUDIT series. Add a dated staleness banner at top  |
| DESIGNER | `docs/DESIGNER-AUDIT/02-DESIGNABLE-TYPES.md` | KEEP | OK | Keep in place within docs/DESIGNER-AUDIT/. Add a one-line cross-link from the Open-Questio |
| DESIGNER | `docs/DESIGNER-AUDIT/03-ABILITIES-AND-MOUNTS.md` | STALE-FIX | OK | Keep in place as the crux section of docs/DESIGNER-AUDIT/. Do a reference-fix pass: replac |
| DESIGNER | `docs/DESIGNER-AUDIT/04-BASEMOD-TEMPLATES.md` | STALE-FIX | OK | Re-run the per-file UniqueID enumeration against current TemplateFiles/*.json to rebuild a |
| DESIGNER | `docs/DESIGNER-AUDIT/05-ASSEMBLIES.md` | KEEP | OK | Keep in place as part of the DESIGNER-AUDIT set. Add a one-line date/'as-of-SHA' stamp at  |
| DESIGNER | `docs/DESIGNER-AUDIT/06-INDUSTRY-AND-MATERIALS.md` | KEEP | OK | Keep in place. Add one paragraph (or a §6 resolution) covering the LocalConstruction subsy |
| DESIGNER | `docs/DESIGNER-AUDIT/07-RESEARCH-AND-UNLOCKS.md` | KEEP | OK | Keep in place within docs/DESIGNER-AUDIT/. Add a date/commit stamp at the top (all DESIGNE |
| DESIGNER | `docs/DESIGNER-AUDIT/README.md` | KEEP | LEAN | Keep in place as the DESIGNER-AUDIT folder index. Add a one-line status/date-of-relevance  |
| DESIGNER | `docs/UNIVERSAL-ASSEMBLY-DESIGN.md` | KEEP-TRIM | OK | Keep in place as the parent principle of the DESIGNER cluster. Trim/restamp the §3 'Curren |
| ECONOMY | `docs/COLONY-PROGRESSION-DESIGN.md` | KEEP-TRIM | OK | Keep as the canonical ladder-vision capture but fold it into the economy/off-world design  |
| ECONOMY | `docs/RESOURCES-AND-MATERIALS-DESIGN.md` | STALE-FIX | OK | Add a dated 'SUPERSEDED SINCE SURVEY' banner listing what has landed (NPCDecisionProcessor |
| ECONOMY | `docs/SPACE-STATIONS-DESIGN.md` | KEEP-TRIM | VERBOSE | Rewrite the status banner to reflect reality: this is a DESIGN-LOCKED + build-state doc, m |
| SOCIETY | `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md` | KEEP-TRIM | OK | Keep as parent/chassis doc, do a staleness pass: (1) flip the CommanderDB 'no skill fields |
| SOCIETY | `docs/GOVERNMENT-AND-POLITICS-DESIGN.md` | KEEP | OK | Add a short 'Build state as of' banner at the top mirroring the DOCS-INDEX row (dials done |
| SOCIETY | `docs/INFLUENCE-PILLAR-DESIGN.md` | KEEP | OK | Keep as-is under docs/. It is the correctly-scoped parent for the parked 'religion 5th civ |
| SOCIETY | `docs/MORALE-AND-POPULATION-DESIGN.md` | STALE-FIX | OK | Add a top banner stamping M1-M5a as BUILT/wired (cite ColonyMoraleDB, ColonyManpowerDB, Co |
| DIPLO | `docs/DIPLOMACY-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep as the canonical external-politics design. Reconcile the body with the 2026-07-07 ban |
| DIPLO | `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` | STALE-FIX | VERBOSE | Keep in place as the espionage design of record, but add an 'AS-BUILT (2026-07)' banner at |
| AI | `docs/AI-BRAIN-BUILD-TRACKER.md` | STALE-FIX | VERBOSE | Keep in place as the AI build authority. (1) Reconcile the top 'ladder' table to the detai |
| AI | `docs/AI-CAPABILITY-CATALOG.md` | STALE-FIX | OK | Keep the doc as the NEED→buildables reference but (1) demote ESPIONAGE from the GAP table  |
| AI | `docs/AI-COMMAND-AND-COMMUNICATION-DESIGN.md` | KEEP-TRIM | OK | Keep as the conceptual chapter of the AI suite but add a top banner: 'SUPERSEDED-BY-CODE a |
| AI | `docs/AI-ECOSYSTEM-DESIGN.md` | KEEP | OK | Keep in place as part of the 8-doc AI suite (entered via AI-IMPLEMENTATION-AND-WIRING-MAP. |
| AI | `docs/AI-GALAXY-AND-CRISIS-DESIGN.md` | KEEP-TRIM | OK | Promote the status banner from 'v0.2 DISCUSSION DRAFT' to a LOCKED-DESIGN + build-state st |
| AI | `docs/AI-IMPLEMENTATION-AND-WIRING-MAP.md` | STALE-FIX | OK | Flip the status banner and DOCS-INDEX row from 'plan/not-built' to 'largely BUILT — see AI |
| AI | `docs/AI-MEANS-ENDS-PLANNER-DESIGN.md` | KEEP-TRIM | OK | Add a top-of-file BUILT banner ('Implemented 2026-07-13 — see Pulsar4X/GameEngine/Factions |
| AI | `docs/AI-OBJECTIVE-ENGINE-DESIGN.md` | KEEP-TRIM | OK | Flip the banner from 'v0.2 DISCUSSION DRAFT' to 'DESIGN-LOCKED — core BUILT (as-of 2026-07 |
| AI | `docs/AI-PERSONALITY-IMPLEMENTATION-SPEC.md` | STALE-FIX | OK | Keep the doc but retitle its status to reflect the M2-0a slice that landed: strike 'Nothin |
| AI | `docs/AI-SELF-PLAY-DESIGN.md` | CONSOLIDATE | BLOATED | Reconcile the 'no code written / fill the stub / seat-durability prerequisite' framing aga |
| AI | `docs/AI-SUPERCLUSTER-AND-AUTHORING-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep as the AI suite's capstone but cut it to ~half: strip the multiverse/brane rhetoric a |
| EXPLORE | `docs/EXPLORATION-CONTENT-DESIGN.md` | KEEP-TRIM | OK | Keep as the catalog chapter of SITE-ENGINE-DESIGN.md but (1) delete the duplicate DOCS-IND |
| EXPLORE | `docs/SITE-ENGINE-DESIGN.md` | KEEP | OK | Keep as the parent/hub for the EXPLORE theme. Two hygiene items: (1) fix the one stale led |
| DETECT | `docs/DETECTION-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep the doc but do a staleness pass: rewrite the header from 'Draft/before we build' to a |
| DETECT | `docs/INFORMATION-DELTA-DESIGN.md` | KEEP-TRIM | LEAN | Keep as the canonical A-vs-B readout principle doc. Trim or update the 'base laser two-zon |
| ENV | `docs/ENVIRONMENTS-DESIGN.md` | KEEP-TRIM | VERBOSE | Keep as the environments design+status doc. Immediate fix: replace the top banner with an  |
| ENV | `docs/HAZARD-DISCOVERY-RESISTANCE-RESEARCH-DESIGN.md` | ARCHIVE | VERBOSE | Move to docs/archive/ (or mark SUPERSEDED in DOCS-INDEX) with a one-line pointer to GameEn |
| ENV | `docs/STELLAR-ENVIRONMENTS-CATALOG.md` | KEEP | OK | Keep as-is but add a one-line staleness note near conclusion #3 and the binary row that th |
| AURORA | `docs/AURORA-GAP-ANALYSIS.md` | ARCHIVE | OK | Move to a docs/archive/ (or docs/history/) folder and add a one-line 'SUPERSEDED — see SYS |
| AURORA | `docs/aurora/COLONY-ENVIRONMENT-AND-POPULATION.md` | STALE-FIX | LEAN | Keep in place. Update §6's status column: mark the population-support row as 'wired (Popul |
| AURORA | `docs/aurora/COMMANDERS-AND-OFFICERS.md` | STALE-FIX | LEAN | Keep in docs/aurora/ as a reference doc. Update §4 to state that the naval flagship combat |
| AURORA | `docs/aurora/DIPLOMACY-AND-INTEL.md` | STALE-FIX | LEAN | Keep as the Aurora reference under docs/aurora/, but rewrite §6 and the 'In plain terms' b |
| AURORA | `docs/aurora/EXPLORATION-AND-SURVEY.md` | KEEP | LEAN | Leave in place under docs/aurora/. Optional one-line update to §3 to reflect that GeoSurve |
| AURORA | `docs/aurora/FLEETS-AND-SHIPYARDS.md` | KEEP | LEAN | Keep in place under docs/aurora/. One edit: replace the 'land troops INavAction' phrasing  |
| AURORA | `docs/aurora/GROUND-COMBAT.md` | KEEP-TRIM | OK | Keep as the Aurora spec but retitle/status it clearly as 'design reference — implemented,  |
| AURORA | `docs/aurora/INDEX.md` | STALE-FIX | OK | Keep the file as the docs/aurora/ index. Fix the 'Maps to Pulsar' column: ground combat →  |
| AURORA | `docs/aurora/LOGISTICS.md` | KEEP | LEAN | Keep in place under docs/aurora/. Two light fixes: (1) update the §4 GSP row from 'not imp |
| AURORA | `docs/aurora/MISSILES-AND-FIRE-CONTROL.md` | STALE-FIX | LEAN | Keep in place as an aurora/ benchmark reference. Fix §4 in one pass: (1) replace both 'dir |
| AURORA | `docs/aurora/PLANETARY-INFRASTRUCTURE.md` | STALE-FIX | OK | Keep as the AURORA reference for planetary infrastructure (sections 1-5 are the payload).  |
| AURORA | `docs/aurora/RESEARCH-AND-TECH.md` | KEEP | LEAN | Keep in place under docs/aurora/ as a benchmark reference. Optionally add a one-line point |
| AURORA | `docs/aurora/SENSORS-AND-DETECTION.md` | KEEP | LEAN | Keep in place under docs/aurora/ as the reference-side twin of docs/DETECTION-DESIGN.md. A |
| AURORA | `docs/aurora/SHIP-DESIGN.md` | STALE-FIX | LEAN | Keep in place under docs/aurora/ as a benchmark reference. Fix lines 84 and 89: replace '( |
| AURORA | `docs/aurora/SPACE-COMBAT-BENCHMARK.md` | STALE-FIX | LEAN | Keep the doc as the ground-combat depth bar (the 'depth bar, stated plainly' 5-point list  |

