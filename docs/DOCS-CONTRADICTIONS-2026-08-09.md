# New-Law vs Old-Doc Contradiction Ledger — 2026-08-09

**Read this before deleting anything.** The last 11 days (2026-07-29 → today) built the new, verified **law** of how the
system works — the Component Designer (12 doors), the Entity Assembler, the visible Resolver Sim, and the environment /
ground / air layers. This ledger compares that canon against every older doc on the same subject scattered across `docs/`,
`docs/archive/`, and the root + per-subsystem `CLAUDE.md` files, and lists **every contradiction, both sides quoted
verbatim**, so nothing is deleted blind.

**How it was built:** eight subject lenses harvested contradictions from source; two adversarial verifiers re-checked each
against the actual files and classified it. **51 raised → 45 confirmed real, 6 rejected as not-a-contradiction.** Of the 45:

- **Group A — 24 LIVE contradictions** (new law wins; the old doc carries **no warning banner**, so a reader hits it cold). *The dangerous set.*
- **Group B — 3 OLD-IS-RIGHT** (the *new-law* doc is the one that's wrong — fix it, don't delete the old). **Two are in `RESOLVER-SIM.md`, verified against engine source below.**
- **Group C — 17 already-flagged** (genuine conflicts, but the old doc already carries a superseded/archived banner — safe to delete).

**Nothing in this ledger has been deleted or edited.** The concrete worklist is at the bottom.

---

## Group A — LIVE contradictions (new law wins; NO warning banner)

### A1 · Engine subsystem `CLAUDE.md` files — read every session, highest priority

**A1.1 — Employment→morale loop: "built and read into morale" vs a dead wire that reads 0 in every colony**
- **NEW** (`docs/economy/CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md` §5.2): *"Employment is a dead wire. The morale model wants an employment ratio and the reader is live (`PopulationProcessor.cs:74` → a ±40 morale band), but no building declares a job — nothing writes `EmploymentAtbDB.Jobs`, so a fully-industrialised Capitol … reports 0 jobs and the employment-morale term sits at 0 forever."*
- **OLD** (`Pulsar4X/GameEngine/Colonies/CLAUDE.md`): *"Jobs/unemployment + housing comfort — built (M2): `EmploymentAtbDB`, `HousingAtbDB`, summed and read into morale. … jobs-vs-pop is two-sided (unemployment debuff / full-employment buff)."*
- **Contradiction:** the subsystem doc presents the loop as built and functioning; zero base-mod templates declare `EmploymentAtbDB.Jobs`, so `GetTotalJobs` sums to 0 and the term contributes 0 in every colony, every game. Attribute + consumer exist; the loop never fires. → **RECONCILE** (mark "built machinery, no producer → reads 0").

**A1.2 — `FleetRoleComposer.FormRoleSubFleets` "UNWIRED — no game-loop caller" vs the caller exists**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §19 S-9): *"The caller exists (`NPCDecisionProcessor.cs:819`). … (`Fleets/CLAUDE.md:18` still carries the stale line.)"*
- **OLD** (`Pulsar4X/GameEngine/Fleets/CLAUDE.md` line 18): *"Still UNWIRED — no game-loop caller — so a running game is byte-identical. B-2c wires it into AI battle-prep…"*
- **Contradiction:** `NPCDecisionProcessor.cs:819` calls it (gated behind default-off `EnableOrderEmission`, so "byte-identical in a stock game" survives), but "no game-loop caller" is factually false, and the same file later references B-2c2 having formed role sub-fleets — internally inconsistent. → **RECONCILE**.

**A1.3 — `RealRangeKmFor` framed as THE future seam to flip the range gate; the flip already shipped and bypassed it**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §19 S-8): *"The behaviour shipped and BYPASSED the seam. `GroundRangeTools.cs:94` is still `=> RealReachKm(...)`. The metre gate went in under `EnableMiniHexCombat` instead. A designed seam that the build routed around … reads as an unbuilt hook."*
- **OLD** (`Pulsar4X/GameEngine/GroundCombat/CLAUDE.md` line 102): *"`RealRangeKmFor` is the SINGLE seam where 'a weapon's real range' is defined … Slice 2 … flips the resolver gate) … The resolver still gates on hexes … flipping that gate to the real metre gap is Slice 2."*
- **Contradiction:** the metre gate already shipped (2026-07-22, under `EnableMiniHexCombat`), bypassing `RealRangeKmFor`. The "flip is future Slice 2 / nothing reads these yet" framing is stale, and it is **not** covered by the file's movement-only (M1-M12) CANON-OVERRIDE banner. → **RECONCILE**.

**A1.4 — Starting stockpile per good: gotcha #6 says 50M vs the actual 1,000,000**
- **NEW** (`docs/economy/CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md` §2): *"Starting stock | 1,000,000 of every mineral + material (20 goods, `earth.json:71-90`)."*
- **OLD** (`Pulsar4X/GameEngine/Industry/CLAUDE.md` gotcha #6): *"the start colony stocks the common refined materials at 50M — see the mining section."*
- **Contradiction:** `earth.json:71-90` = 1,000,000 per good; gotcha #6 still says 50M — and the same file's own mining section already records the drop. Low severity (a test tip). → **RECONCILE**.

**A1.5 — `Client/CLAUDE.md` cites the RETIRED `SYSTEMS-STATUS-AND-TEST-PLAN` as a live authority**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §19 S-11): *"That doc is RETIRED (2026-07-27). The pointer resolves — the file is on disk in `archive/` — so it silently 'works' and misleads. The live replacements are `SYSTEM-CONNECTION-MAP.md` / `TESTING-TRACKER.md` / `DOCS-INDEX.md`."*
- **OLD** (`Pulsar4X/Pulsar4X.Client/CLAUDE.md` line 200): *"…the only way to know the real state is to run it (see `docs/archive/SYSTEMS-STATUS-AND-TEST-PLAN.md` §5B)."*
- **Contradiction:** names the retired doc as the authority, no retired marker; the `archive/` path resolves silently and misleads. → **RECONCILE** (repoint to the live dashboards).

### A2 · Live design docs (keep the body; the flagged section is stale)

**A2.1 — `DOCS-INDEX` carries a DELETED tombstone AND a live-canon row for the SAME file (`GROUND-SURFACE-MAP-DESIGN`)**
- **NEW** (`docs/DOCS-INDEX.md` line 105): the live locked-canon row — *"THE single design for the planet surface — all three zooms … ⚠ CANON-OVERRIDDEN 2026-07-28."*
- **OLD** (`docs/DOCS-INDEX.md` line 74): *"~~`docs/ground/GROUND-SURFACE-MAP-DESIGN.md`~~ | DELETED 2026-07-08 — superseded … by `docs/archive/economy/COMPONENT-DESIGNER-CATEGORIES.md`."*
- **Contradiction:** two rows for the same path — a tombstone and the live canon row. The file is live on disk. Doubly stale (the tombstone points at a doc that has itself since been archived). → **DELETE-OLD** (the line-74 tombstone).

**A2.2 — Ship combat value: cached-forever-at-build vs recalculated-on-damage** *(the core of whole-or-dead)*
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §1/§7): *"Combat value is cached at build and never recalculated on damage — a damaged-but-alive ship still rates at full value."*
- **OLD** (`docs/combat/COMBAT-DESIGN.md` System 9): *"Cached in `ShipCombatValueDB`, recalculated when a ship takes significant damage (component loss triggers a recalc)."*
- **Contradiction:** COMBAT-DESIGN asserts the opposite (three times); recalc-on-damage is explicitly "not built" and is the mechanism behind whole-or-dead. → **RECONCILE**.

**A2.3 — Weapon range: a real finite-range closing model vs "all weapons fire regardless of distance"**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §14.4 / §19 S-6): *"`WeaponProfile.Range_m` exists; the gate is in `BuildFireMix`."* (missile 1000 km > railgun 500 km > flak 50 km).
- **OLD** (`docs/combat/COMBAT-DESIGN.md` System 1 + v1-boundary): *"Currently missing — all weapons fire regardless of distance … Weapon range = simple in-range / out-of-range."*
- **Contradiction:** range is a built per-weapon finite field driving a closing/dodge/range-accuracy model; COMBAT-DESIGN still describes it unbuilt. → **RECONCILE**.

**A2.4 — Determinism (no RNG in the resolve) vs a `variance_roll` in the System 9 algorithm**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §5 LD-7): *"Determinism is a locked rule. The kernel is pure — no entity mutation, no RNG, no clock read … Do not add RNG or a clock read here."*
- **OLD** (`docs/combat/COMBAT-DESIGN.md` System 9): *"`casualties_A = f(strength_B, defenseRating_A, variance_roll)`."*
- **Contradiction:** the written algorithm uses a `variance_roll`; the locked rule and the actually-built `AutoResolve` are deterministic. Low severity (System 9 is "what to build"). → **RECONCILE**.

**A2.5 — Hostility/IFF: `AreHostile` reads `DiplomacyDB` + a detection gate vs "everyone sees everyone; a different faction is hostile"**
- **NEW** (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §8 rows 7/12): *"Hostility test — `AreHostile` reads `DiplomacyDB` (pacts, war latch, stances)."*
- **OLD** (`docs/combat/COMBAT-DESIGN.md` v1-boundary / System 9): *"Sensors + IFF = mutual detection (everyone sees everyone; a different faction is hostile) … no alliance/diplomacy model yet."*
- **Contradiction:** space hostility now consults `DiplomacyDB` + a detection gate; COMBAT-DESIGN describes a flat stub. → **RECONCILE**.

**A2.6 — `COMBAT-DESIGN` names the retired `SYSTEMS-STATUS-AND-TEST-PLAN` as combat "source of truth"**
- **NEW** (`…GROUND-TRUTH…` §19 S-11): the retired-doc caution (see A1.5).
- **OLD** (`docs/combat/COMBAT-DESIGN.md` "What Already Exists" preamble): *"The authoritative, always-current build ledger is `GameEngine/Combat/CLAUDE.md` + `docs/archive/SYSTEMS-STATUS-AND-TEST-PLAN.md` … treat … those two as the source of truth."*
- **Contradiction:** names a retired/misleading doc as an authoritative always-current source. → **RECONCILE** (repoint to `Combat/CLAUDE.md` + the live dashboards).

**A2.7 — Hazard architecture: build a new `EnvironmentalZoneDB` vs the as-built `SpaceHazardDB` typed-effects**
- **NEW** (`docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` Part 1b): *"Everything in space is one DataBlob — `SpaceHazardDB` — whose behavior is a typed list of `HazardEffect`s (data, not hardcoded knobs)."*
- **OLD** (`docs/combat/COMBAT-DESIGN.md` System 8): *"`EnvironmentalZoneDB`: zone type, position, radius, modifier table … looked up by zone type at combat start — pre-baked, not recalculated each round."*
- **Contradiction:** System 8 says build a NEW pre-baked-table blob; the built-and-canon system is the data-driven `SpaceHazardDB`. → **RECONCILE**.

**A2.8 — Nebula/belt as existing generator objects to "attach to" vs no belt-region object / nebula authored as JSON**
- **NEW** (`docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` Part 1b/4): *"belts are scattered individual asteroid bodies, no region object … The one genuine build: a belt-region object."*
- **OLD** (`docs/combat/COMBAT-DESIGN.md` System 8): *"The galaxy generator already places asteroid belts, nebulae … If the generator already creates an object called 'nebula,' the hazard system should attach to it."*
- **Contradiction:** COMBAT-DESIGN assumes readable region objects exist; the new law says they don't (nebula must be JSON-authored; a belt-region object is the single genuine unbuilt piece). → **RECONCILE**.

**A2.9 — Weapon delivery axis: SIX values (Beam/Bolt/Slug/Cloud/Guided/Blast) vs FOUR (Contact/Beam/Projectile/Guided)**
- **NEW** (`docs/economy/DESIGNER-NORTH-STAR.md` §9/§4): *"Bolt, Slug and Cloud all answer [PD] no, identically. Three names for one row."* → four delivery values.
- **OLD** (`docs/combat/WEAPONS-DESIGN.md` §1.6, "DECIDED 2026-07-06"): *"v1 supports all four Natures … and all six Deliveries (Beam/Bolt/Slug/Cloud/Guided/Blast)."*
- **Contradiction:** WEAPONS-DESIGN (no superseded banner) asserts SIX decided-and-built deliveries (engine enum `WeaponProfile.cs:42` has all six); the new law collapses them to FOUR. → **RECONCILE** (note the engine still has six; this is a *design* collapse).

**A2.10 — Weapon RANGE: "v1 stub / in-out per system" vs a real `Range_m` that starts and sizes battles**
- **NEW** (`docs/economy/DESIGNER-NORTH-STAR.md` §8b): *"`Range_m` → `MaxReach` | `WithinWeaponRange` | starts battles — and under LD-30 it sizes the arena."* (hardcoded flak 50 / disruptor 400 / railgun 500 / missile 1000 km).
- **OLD** (`docs/combat/WEAPONS-DESIGN.md` §2.6/§2.1): *"Range is in/out per system (real weapon-range geometry is v2) … Range (v1 stub)."*
- **Contradiction:** old says range is a v1 stub, real geometry deferred to v2; the new law makes `Range_m` a real per-weapon reach that gates and sizes every battle. → **RECONCILE**.

**A2.11 — Obscuring hazards "don't exist yet / latent v2" vs gas-cloud `SensorJam` shipped and cutting detection**
- **NEW** (`docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` Part 1b): *"`SensorScan.cs:83` calls `SpaceHazardTools.CombinedAt(position)`; a gas cloud's SensorJam ×0.35 cuts how far you can see … 'you can't see far in a nebula' … is shipped behavior."*
- **OLD** (`docs/combat/DETECTION-DESIGN.md` §3a/b/c): *"obscuring hazards (don't exist yet) … Environmental masking | none … v2 / LATENT."*
- **Contradiction:** DETECTION-DESIGN (drafted before the hazard system landed) says masking is v2/latent; it ships (gas-cloud SensorJam, flare-blind, sensor-hardening module). → **RECONCILE**.

**A2.12 — Assembler architecture: `GroundUnitAssembly` IS "the shared core" vs four parallel assemblers**
- **NEW** (`docs/assembler/06-OUTPUTS-BY-DOOR.md` Chassis): *"`IChassisAtb.StructuralBudget` is read by only 2 of the 4 assemblers … The 'uniform chassis view' is wired in 2 of 4 cells."* (readers: `GroundUnitAssembly.cs:98`, `StationAssembly.cs:61`, `BuildingAssembly.cs`…).
- **OLD** (`docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §0): *"`GroundUnitAssembly` is the shared assembler core … the general 'chassis + parts → a buildable's emergent stats' engine … for every assembled kind."*
- **Contradiction:** old asserts one shared core; the reader trace shows FOUR parallel assemblers, the shared chassis interface wired in only 2 of 4, with Station/Building mirroring rather than reusing. → **RECONCILE**.

**A2.13 — Installation/building: a leaf component vs its own assembly**
- **NEW** (`docs/assembler/06-OUTPUTS-BY-DOOR.md`): *"THE BUILDING ASSEMBLER — turns a foundation + modules into a building's totals … with a `BuildingChassisAtb FootprintBudget`."*
- **OLD** (`docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §3, 2026-07-05): *"Installation … a single component; the colony is the assembly … leaf part, not itself an assembly."*
- **Contradiction:** old classes an installation as a leaf; the building assembler makes it its own assembly (foundation + modules, footprint budget). → **RECONCILE**.

**A2.14 — Station: host-accumulates-components (no gate) vs a designed assembly with a structural budget**
- **NEW** (`docs/assembler/06-OUTPUTS-BY-DOOR.md`): *"`StationAssembly.cs:61` … `StationChassisAtb` carries `StructuralAllowance` (the module budget)."*
- **OLD** (`docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §3, 2026-07-05): *"Station … a host that accumulates installed components (colony-like) … [no structural gate]."*
- **Contradiction:** old = gate-less host; new law = designed assembly (chassis + modules) with a `StructuralAllowance` budget gate. → **RECONCILE**.

**A2.15 — Super-weapon / megastructure: "doesn't exist" vs a Death Star built as a Battle-Station host**
- **NEW** (`docs/assembler/DESIGNER-DRIVER-PLAYBOOK.md` scaling ladder): *"Megastructure / Death Star | station (mega tier) — Battle Station host tier + scaled budgets, BUILT 2026-08-02 (`deathstar` preset)."*
- **OLD** (`docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §3): *"Super-weapon | doesn't exist … World-ship / megastructure | doesn't exist."*
- **Contradiction:** old marks both "doesn't exist"; a megastructure HOST is now expressible/built (deathstar preset). (Both agree the super-*weapon* reader is still pending.) → **RECONCILE**.

**A2.16 — Carrier bay: "ONE generalized bay, build no second bay type" vs a separate `DockBayAtb` ship dock built**
- **NEW** (`docs/assembler/entityassembler.html` + engine `Docking/DockBayAtb.cs`): *"A DOCK — berths for whole vessels … its OWN area and not another cargo hold (the developer's call, 2026-07-30)."*
- **OLD** (`docs/combat/CARRIER-DESIGN.md` §5/§10/§12): *"there is ONE bay component … promote the existing ground-only `GroundBayAtb` into a general `BayAtb` … do not build a separate hangar component … Do the promotion FIRST (C1), so nothing builds a second bay type."*
- **Contradiction:** CARRIER-DESIGN's keystone C1 was overtaken — a separate `DockBayAtb` ship dock was built 2026-07-30, `GroundBayAtb` was never renamed, C1 is unexecuted. → **RECONCILE**.

**A2.17 — Capture model: Earthfall region-flip / whole-planet capture as the live mechanism (NO banner)**
- **NEW** (`docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` M8/M16): *"You capture MINI hexes. A REGIONAL hex flips when you hold the MAJORITY of its occupied mini hexes … Region capture is gone."*
- **OLD** (`docs/earthfall/findings/A5-ground-campaign.md` + `R4-infrastructure-combat.md`): *"Whole-planet capture | EXISTS v1 flip only (`TryCapturePlanet` requires ALL regions one holder) … CAPTURE — region-triggered only … flips `hex.OwnerFactionID` … when the REGION flips."*
- **Contradiction:** Earthfall findings describe region-owner-flip + whole-planet capture as live and propose region-triggered capture; the new law deletes region capture for per-mini-hex ownership + majority roll-up. The Earthfall docs carry **no** override banner. → **RECONCILE** (add a banner).

**A2.18 — A `CaptureInfrastructure` order that is BUILT AND SHIPS vs M18 deletes it**
- **NEW** (`docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` M18): *"`CaptureInfrastructure` | ⛔ DELETE — M16 derives it. Standing on a mini tile makes it yours … The order was doing by hand what the invariant now does automatically."*
- **OLD** (`docs/earthfall/findings/R4-infrastructure-combat.md` + `LANE-CORE-NOTES.md`): *"APPEND `DestroyInfrastructure=5`, `CaptureInfrastructure=6` … Capture: range/hold-gate → `hex.OwnerFactionID = faction`."*
- **Contradiction (with a code note):** the verifier flags this as an *understatement* — `CaptureInfrastructure` is not merely planned; it is **BUILT and ships** (`GroundForcesDB.cs:298` enum, factory `:348`, `Describe :361`, `ResolveInfraOrder`, client Raze/Capture buttons). So M18's "delete" is a design ruling that needs a **code follow-up**, not just a doc edit. → **RECONCILE + code**.

**A2.19 — Ground order roadmap: O3 six-order list vs the fixed 3+2 set (M18)**
- **NEW** (`docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` M18): *"The order enum goes 7 → 3 … HOLD is not an order — it is the DEFAULT … SetStance + SetEngagement FOLD 2 → 1 as SET DOCTRINE."*
- **OLD** (`docs/ground/GROUND-ORDERS-CATALOG-DESIGN.md` O3+): *"Ground: move-to-hex / attack-hex / dig-in / garrison / bombard-support / load-to-transport."*
- **Contradiction:** O3 lists a richer six-order set; M18 fixes the vocabulary at three battalion orders (MOVE, RAZE, SET DOCTRINE) + two transport, garrison as default, no seize order. → **RECONCILE**.

*(Two more A-items — the Colonies/Fleets subsystem lines — are folded above into A1.)*

---

## Group B — OLD-IS-RIGHT (the NEW-LAW doc is wrong — fix it, don't delete the old)

**B1 — Target priority IS wired into the resolver** *(fix `RESOLVER-SIM.md`)* — **source-verified 2026-08-09**
- **NEW-law doc, WRONG** (`docs/combat/RESOLVER-SIM.md` §"What 2026-08-04 added"): *"`FleetDoctrine.TrySetDoctrine` throws it away … wiring it is a field-add + a read."*
- **OLD, CORRECT** (`Pulsar4X/GameEngine/Combat/CLAUDE.md`, wired 2026-08-04): *"`ApplyCasualties` reads the attacker doctrine's priority … `Targeting` … copied … by `TrySetDoctrine`, read by `ApplyCasualties`."*
- **Verified in source:** `FleetDoctrineDB.cs:51` `Targeting` property, `:67` `Targeting = db.Targeting`, `ApplyCasualties(... TargetPriority priority ...)` at `:884`, passed at `:784`. RESOLVER-SIM's prose was true on 08-03 and went stale on 08-04. → **FIX RESOLVER-SIM.md.**

**B2 — Auto-retreat at a 0.5 casualty threshold ships** *(fix `RESOLVER-SIM.md`)* — **source-verified 2026-08-09**
- **NEW-law doc, WRONG** (`docs/combat/RESOLVER-SIM.md` step 6): *"There is no automatic casualty retreat … (That mirrors the engine: retreat is a withdraw doctrine, not a threshold.)"*
- **OLD, CORRECT** (`docs/combat/COMBAT-DESIGN.md` + `Combat/CLAUDE.md`): *"A fleet automatically breaks off when its losses cross its doctrine's retreat threshold (`RetreatCasualtyThreshold`, v1 flat 0.5)."*
- **Verified in source:** `CombatEngagement.cs:48 RetreatCasualtyThreshold = 0.5`, `ShouldRetreat` `:1662`, called `:815`. The sim may not model auto-retreat, but the claim "mirrors the engine" is false. → **FIX RESOLVER-SIM.md** (the sim is a simplification; don't claim it mirrors the engine here).

**B3 — Food IS buildable at the playable start** *(fix `CAPITOL-WORLD-INFRASTRUCTURE`)*
- **NEW-law doc, OVER-GENERALIZED** (`docs/economy/CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md` §4 B1): *"`food-production` … ids are absent from Earth's `StartingItems`/`ComponentDesigns` … the entire food supply side is un-buildable out of the box."*
- **OLD, CORRECT** (`Pulsar4X/GameEngine/Colonies/CLAUDE.md`): *"`food-production` template … DevTest player + UMF colonies + Kithrin's Titan station carry one (`StartingItems` + `ComponentDesigns` + a start installation)."*
- **Why:** CAPITOL read `earth.json`'s embedded 8.2B colony (which lacks `food-production`); the actual playable **DevTest/menu** New Game builds `uef-devtest.json`'s Earth colony WITH `food-production` unlocked + 2 pre-built agri-complexes. → **FIX CAPITOL B1** (scope it to the unused `earth.json`, not the playable start).

---

## Group C — Already-flagged (genuine, but the old doc already carries a banner — safe to delete)

These 17 are real conflicts, but the old doc already warns the reader (a top SUPERSEDED / CANON-OVERRIDE / archived banner points at the new law), so deleting them loses nothing.

**Component designer (archive, all banner'd ARCHIVED 2026-08-03):**
1. The METHOD — derive-from-source vs the authored 37-door taxonomy (`archive/economy/COMPONENT-DESIGNER-CATEGORIES.md`).
2. Weapons — 5 authored doors vs 2 choices + 4 sliders (CATEGORIES + `COMPONENT-DESIGNER-DIALS.md`; both have inline "⛔ THE FIVE WEAPONS DOORS ARE SUPERSEDED" callouts).
3. Exotic as a Weapons DOOR vs Exotic as a Nature setting (CATEGORIES + DIALS).
4. Pulse vs Continuous Beam as separate deliveries vs one weapon at two shot-sizes (DIALS §1).
5. Intrinsic Test — magazine size as a Weapons▸Guided dial vs a Logistical▸Storage decision (DIALS §1.4).
6. Propulsion — 5 authored doors (incl. Fluid & Exotic) vs 3 derived families (DIALS/CATEGORIES).
7. Non-damage Exotic effects (mind-control) as Weapons▸Exotic dials vs not-weapons (DIALS §1.5; *keep the effect-bus concept — GROUND-TRUTH L23 endorses it*).
8. Template count — "~37 doors replace 67 templates" vs 96 and growing (CATEGORIES + `archive/DESIGNER-AUDIT-2026-07-08/**`; *preserve that folder's two-lock/universality diagnosis*).
9. Parametric-collapse STATUS — "WIRING-READY" vs "never built; zero shared dials" (CATEGORIES).
10. Weapon naming — franchise-IP preset names retained vs generic-only (DIALS §1.3; *the banner keeps the ⚙ wiring dossiers, not the IP names*).

**Ground/air (live docs, each carries a 🔒 CANON-OVERRIDE banner):**
11. Movement primitive — GROUND-SURFACE-MAP's coexisting movement models vs M1 one-verb.
12. Movement primitive — `GroundCombat/CLAUDE.md` region→region + coarse `OrderMove` + hex + global march vs M1 (banner lists them ⛔ DELETED).
13. Battles/capture per-region vs per-mini-hex (GROUND-SURFACE-MAP G-track Model 3).
14. Capture — `GroundCombat/CLAUDE.md` region flip + `TryCapturePlanet` all-regions vs M8/M16 (accurate as-built; banner covers it).
15. Ground multi-weapon resolve — GROUND-UNIT-VARIABLES Q2 "one blended gun" vs W2 per-mount (the doc's own banner refutes Q2).

**Environment / economy (live docs with banners):**
16. `SignalQuality` "CUT / detection = strength only" vs "live and load-bearing" (`DETECTION-DESIGN.md` — the 07-07 CUT banner is immediately followed by the 07-28 correction; *the retracted rows should still be excised*).
17. Ongoing life-support/food consumption "not implemented" vs "built machinery, inert by a 0 coefficient" (`RESOURCES-AND-MATERIALS-DESIGN.md`, top banner line 7).

---

## The worklist (nothing done yet — your call)

**Safe deletes (archive, already banner'd):**
- `docs/archive/economy/COMPONENT-DESIGNER-CATEGORIES.md`, `COMPONENT-DESIGNER-DIALS.md`, `COMPONENT-DESIGNER-DIAL-LEDGER.md`, `COMPONENT-DESIGNER-DIAL-AUDIT-2026-07-23.md` — the whole superseded 37-door model.
- `docs/archive/DESIGNER-AUDIT-2026-07-08/**` — the stale "67 templates" count only; **preserve** its two-lock/universality diagnosis (already carried forward in GROUND-TRUTH §16).
- `docs/DOCS-INDEX.md` **line 74** — the stale `GROUND-SURFACE-MAP-DESIGN` tombstone row (the file is live; its real row is line 105).

**Edits — engine subsystem `CLAUDE.md` (highest priority, no banner, read every session):**
- `Colonies/CLAUDE.md` — employment→morale: "built machinery, but no producer writes `EmploymentAtbDB.Jobs` → reads 0."
- `Fleets/CLAUDE.md` line 18 — "UNWIRED — no caller" → caller exists (`NPCDecisionProcessor.cs:819`), gated behind default-off `EnableOrderEmission`.
- `GroundCombat/CLAUDE.md` line 102 — the metre gate already shipped under `EnableMiniHexCombat`, bypassing `RealRangeKmFor`.
- `Industry/CLAUDE.md` gotcha #6 — "50M" → "1,000,000" per good.
- `Client/CLAUDE.md` line 200 — repoint from the retired `SYSTEMS-STATUS-AND-TEST-PLAN` to the live dashboards.

**Edits — live design docs (keep the body, fix the stale sections):**
- `docs/combat/COMBAT-DESIGN.md` — Systems 1, 8, 9 + the v1-boundary table (combat-value recalc; weapon-range gate; drop `variance_roll`; `AreHostile`/`DiplomacyDB`; `SpaceHazardDB` vs `EnvironmentalZoneDB`; nebula/belt objects; stop naming the retired doc).
- `docs/combat/WEAPONS-DESIGN.md` — six→four deliveries (note the engine still has six); range v1-stub → real `Range_m`.
- `docs/combat/DETECTION-DESIGN.md` — obscuring-hazards "latent" → shipped; excise the retracted `SignalQuality` CUT rows.
- `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` — one shared core → four parallel assemblers; refresh the §3 survey rows (installation/station/megastructure).
- `docs/combat/CARRIER-DESIGN.md` — reconcile C1 (one bay) against the built separate `DockBayAtb`.
- `docs/ground/GROUND-ORDERS-CATALOG-DESIGN.md` — O3 six-order roadmap → M18's 3+2.
- `docs/earthfall/findings/A5-ground-campaign.md`, `R4-infrastructure-combat.md`, `LANE-CORE-NOTES.md`, `LANE-GROUND-NOTES.md` — add a CANON-OVERRIDE banner (M8/M16/M18); **note `CaptureInfrastructure` is built and ships (needs a code follow-up).**

**Fix the NEW-LAW doc (OLD-IS-RIGHT):**
- `docs/combat/RESOLVER-SIM.md` — B1 (target priority IS wired) + B2 (0.5 auto-retreat ships).
- `docs/economy/CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md` — B3 (food is buildable at the playable DevTest start).

---

*Ledger built 2026-08-09 by an 8-lens contradiction hunt + 2 adversarial verifiers; the 3 OLD-IS-RIGHT cases re-verified against engine source by hand. 6 harvested items were rejected as not-a-contradiction. Nothing has been deleted or edited — this is the read-before-delete list.*
