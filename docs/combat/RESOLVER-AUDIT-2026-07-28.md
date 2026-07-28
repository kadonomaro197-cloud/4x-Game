# The Auto-Resolver Multi-Pass Audit — 2026-07-28

**What this is.** The developer commissioned repeated audit passes over the auto-resolver and every system it touches,
*"until the amount of issues found drops off to negligible levels"* — minimum ten passes — with two standing rules:

> 1. **Every issue is recorded, explained clearly and simply.**
> 2. **⛔ DO NOT flag an issue without reading the associated and relevant docs FIRST.**

Rule 2 exists because it was already broken once: the **R1–R5 retrofit track was proposed without reading
`docs/combat/RESOLVER-DESIGN.md` §B5/§B7** — which already contained a developer ruling and a drafted build plan
covering much of the same ground. Pass 1 is largely the correction of that.

**Companion records:** canon rulings + the X1–X15 contradiction table live in
`docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` (M19). The as-built field manual is
`docs/combat/AUTO-RESOLVER-TEARDOWN.md`. This file is the *pass log*.

**Severity key:** 🔴 **BLOCKER** (silently wrong today) · 🟠 **REAL** (a designed thing does not work) ·
🟡 **DEBT** (right but stale/undone) · 🔵 **NOTE** (worth knowing, not broken).

---

## PASS 1 — The doc-first pass: read the design docs, then check my own prior work against them

**Docs read in full for this pass:** `RESOLVER-DESIGN.md` §A1–A7, §B1–B7.2 (362 lines) · the section index of
`UNIFIED-RESOLVER-AND-BATTLE-STATS.md`, `AUTO-RESOLVER-TEARDOWN.md`, `FLEET-COMBAT-CLOSING-DESIGN.md`,
`RESOLVER-2D-JOINTS.md`, `GROUND-CLOSING-FIGHT-W-TRACK.md`.

### The headline: a developer north star from 2026-07-08 already covers most of today's M19

`RESOLVER-DESIGN.md` **§B7** records a prior ruling, quoted there as:

> *"This IS how space combat should be and how planetary combat MUST be. This is the north star of combat and what
> you are building towards this whole time."*

It already establishes, in the developer's own words: **a formation/battalion IS a fleet**; a joined force **moves at
its slowest unit's pace but sees as far as its longest sensor**; **the fight trigger is the weapon range of the
longest-ranged unit**; and **the resolver simulates the closing tick-by-tick over the battle's duration**, each
sub-group moving at its true speed under its own doctrine — the zergling/Titan example is named as the acceptance
test for "done."

**This means today's M19 is a re-statement and sharpening of a standing ruling, not a new direction.** Good news —
but it also means several things I flagged as gaps were already ruled, and one was already *built*.

### Findings

| # | Sev | Finding |
|---|---|---|
| **P1-1** | 🟡 | **MY R1–R5 TRACK PARTLY DUPLICATES AN EXISTING, BETTER-SPECIFIED PLAN.** `RESOLVER-DESIGN` §B5 already sequences slices 1→5 (kernel → armour → ground damage → **closing model to ground (slice 4)** → **formation fleet-parity + sub-formation ranges + ground bucketing (slice 5)**), and **§B7.2 is a complete, ready-to-build spec for slice 5c** with a bucket key, a 5-step rewrite, two gauges and a risk list. **The R-track must be reconciled against it rather than replacing it.** |
| **P1-2** | 🟠 | **VOCABULARY CONFLICT — the sub-group has two locked names.** §B7.1 locks **"Group"** (space) / **"Formation"** (ground), explicitly noting *"Group (was 'sub-fleet')"*. Today's ruling says **"Wing"** (space) / **"Formation"** (ground). **Today's is newer and wins — but §B7.1's table is now wrong and must be corrected**, or the codebase will grow both names. *(Also settles it: the July doc never used "Squadron" either.)* |
| **P1-3** | 🟠 | **`GroundFormation` IS AT THE WRONG TIER, AND THIS WAS ALREADY KNOWN.** §B7.1: *"today's `GroundFormation` class sits at the **fleet/Battalion** level — 'the ground echo of a fleet.' Under this vocabulary a **Battalion** is that top grouping and a **Formation** is the sub-group. Slice 5 either renames or nests."* So the class named `Formation` **is** the Battalion. This is recorded debt, not a discovery — and it explains why the ground hierarchy looked flat: the second tier is the *nesting* via `ParentFormationId`, unnamed. |
| **P1-4** | 🔴 | **TODAY'S "NO DAMAGE ROLL-OVER" RULING CONFLICTS WITH THE JULY DISTRIBUTION MODEL.** §B7.1 locks *"compute the math for ONE unit + its doctrine, then distribute across N"*, and §B7.2 step 3 distributes damage **health-weighted across defender buckets** — which **is** the smear that today's ruling replaces with real targeting. **Both rulings are the developer's; they must be reconciled, not silently chosen between.** *(Proposed resolution in the note below — needs a ruling.)* |
| **P1-5** | 🔵 | **X9 WAS ALREADY RULED *AND BUILT* IN JULY.** §B7: *"Trigger = weapon range of the longest-ranged unit… This is the space rule already (`WithinWeaponRange` opens the fight at `Max(reachA, reachB)`)."* Today's refinement matches it exactly. **My original X9 framing ("doctrine has no input into range") was over-broad** — commencement was never meant to be doctrine's. Only post-commencement closing behaviour is at issue. Corrected in M19. |
| **P1-6** | 🟠 | **`ResolveRegionCombat` IS O(units²) AND THE DOC SAYS SO.** §B7.2: nested `foreach attacker → foreach reachable defender`; *"at 10,000 units that's ~10⁸ ops/tick."* The fix is designed and *"ready to build"* — and **deferred**, deliberately, because it needs its own equivalence proof. **This is the performance answer I was missing when I sized R2/R3.** |
| **P1-7** | 🟡 | **§B7.1 CLAIMS PER-GROUP DOCTRINE IS "ALREADY BUILT" IN BOTH DOMAINS** — *"space: `FleetDoctrineDB` per Group — already the per-component doctrine model; ground: `GroundFormationDoctrine` + `GroundEngagementStance` per Formation — already built."* **UNVERIFIED. Flagged for Pass 2**, because it sits directly against **X11** (wings have no position to manoeuvre with) and the D0 finding that doctrine assignment **drops four of its own fields**. If doctrine is per-group but unreadable, "already built" is a *code-exists* claim, not a *works* claim. |

### The P1-4 conflict, stated plainly (this needs a developer ruling)

**The July model:** to keep 10,000 units affordable, group identical co-located units into **buckets**, run the
expensive math **once per bucket**, then multiply by the count. Damage is then spread across defender buckets
**weighted by their health**.

**Today's model:** pick a target, fire, and **excess damage over that target's remaining health is wasted** — *"if you
design a weapon with a bunch of alpha then you pay for it."*

**They collide on one point only:** health-weighted spreading *is* the smear that makes alpha meaningless and never
finishes a cripple.

**They are otherwise orthogonal and both worth keeping** — bucketing is about *how many objects the math runs on*;
targeting is about *where the damage goes*. **Proposed reconciliation:** keep bucketing for cost, but make the
allocation **doctrine-targeted rather than health-weighted** — the attacker's doctrine picks a *target bucket* by
priority, damage lands there, overkill is **wasted at the bucket's per-unit granularity**, and only then does the
next bucket get fired on. That preserves O(buckets) performance *and* makes alpha a real trade-off. **Not adopted —
recorded for the developer's decision.**

---

## PASS 2 — Doctrine end-to-end (because R-c makes it the ONLY behavioural driver)

**Docs read first:** `COMBAT-DESIGN.md` §"one combat engine, doctrine is the wheel" + System 4 (Fleet Doctrine) and
System 4-detailed (Fleet Components & Switchable Doctrine); `RESOLVER-DESIGN.md` §B7.1 (doctrine lives on the
Group/Formation); the D0/C4 finding in `docs/DOCS-AUDIT-2026-07-27.md`.

**The plain-English summary: a doctrine is authored with 14 dials, and neither domain's runtime object can hold them
all — and the two domains hold DIFFERENT subsets, under DIFFERENT names, with one pair inverted.**

| Where | Fields |
|---|---|
| **The authored blueprint** (`CombatDoctrineBlueprint`, what all 25 catalog entries fill in) | **14** — DisplayName · Family · Domain · FirepowerMult · ToughnessMult · **DamageTakenMult** · SpeedMult · CooldownSeconds · IsRetreat · EngagementPosture · **TargetPriority** · **RetreatCasualtyThreshold** · **BreakAwaySeconds** · **Pursues** |
| **Space runtime** (`FleetDoctrineDB`) | **8** — DoctrineId · Family · FirepowerMult · ToughnessMult · SpeedMult · IsRetreat · Posture · SwitchableAfter |
| **Ground runtime** (fields on `GroundFormation`) | **5** — StanceId · StanceFamily · **AttackMult** · **DamageTakenMult** · Engagement |

| # | Sev | Finding |
|---|---|---|
| **P2-1** | 🔴 | **THE RUNTIME DOCTRINE OBJECT HAS NO FIELDS FOR ITS OWN BEHAVIOUR — D0/C4 UNDERSTATED THIS.** The earlier finding said `TrySetDoctrine` *"silently drops four fields."* It is worse and simpler than a copy bug: **`FleetDoctrineDB` has nowhere to put them.** `TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds` and `Pursues` are authored on every catalog entry and **do not exist as runtime fields**. That is why 9 of 10 reader functions have no non-test callers — **there is nothing to read.** ⇒ the fix is a **save-schema change** (new `[JsonProperty]` fields), not a one-line copy. |
| **P2-2** | 🔴 | **THE SAME CONCEPT HAS TWO NAMES ACROSS THE DOMAINS — AND ONE PAIR IS A RECIPROCAL.** `FirepowerMult` (space) **is** `AttackMult` (ground). `ToughnessMult` (space) and `DamageTakenMult` (ground) are the **same dial inverted**. **The blueprint authors BOTH**, so a single doctrine entry can carry a toughness multiplier *and* its reciprocal and the two domains read different ones. The root `CLAUDE.md` landmine index already warns *"author exactly one"* — this is the structural reason that warning exists. **Under R-a ("it is 1 resolver") this is indefensible.** |
| **P2-3** | 🟠 | **THE POSTURE/ROE ENUMS ARE DIFFERENT TYPES.** Space carries `EngagementPosture` (WeaponsFree/…); ground carries `GroundEngagementStance` (HoldGround/CloseToEngage/StandOff). **Same idea, two incompatible vocabularies** — so a doctrine's authored `EngagementPosture` string cannot express a ground ROE at all. |
| **P2-4** | 🟠 | **GROUND HAS NO `SpeedMult` FIELD.** The blueprint authors one and space stores one; a ground formation has nowhere to put it. **So the doctrine's speed dial has no landing site on the ground** — which is precisely why the plan's D3a says `SpeedMult` must *"finally bite."* Recorded here as the structural cause. |
| **P2-5** | 🔵 | **Per-Group doctrine IS structurally possible in space.** `FleetDoctrineDB` attaches to a *fleet entity*, and a Group/Wing is a nested fleet — so §B7.1's *"already built"* claim is structurally true for space. It is the **fields**, not the attachment point, that are missing. |

---

## PASS 3 — The inertness sweep: what is built and switched OFF

**Docs read first:** root `CLAUDE.md` (byte-identical flag discipline: behaviour ships default-off, CI-off/menu-on);
`docs/TESTING-TRACKER.md` Layer-1/Layer-2 boundaries; `docs/DOCS-AUDIT-2026-07-27.md` §13 (the CI shard picture).

Every combat behaviour flag in the engine, its default, and whether the client ever turns it on:

| Flag | Default | Client turns on? |
|---|---|---|
| `EnableClosingRange` · `EnableFinalFireOnlyPD` · `EnableFireControlTracking` · `EnableGroundRoleManeuver` · `EnableGroundTacticalAI` · `EnableInitialEngagementSpread` · `EnableMiniHexCombat` · `RequireDetectionToEngage` · `RequireWeaponRangeToEngage` · `RequireWeaponsReleaseToEngage` | false | ✅ **yes** |
| **`EnableFireControlRange`** | false | ❌ **NEVER** |
| **`EnableGroupPlane`** | false | ❌ **NEVER** |

| # | Sev | Finding |
|---|---|---|
| **P3-1** | 🔴 | **`EnableFireControlRange` IS A DESIGNER DIAL THAT NOTHING SWITCHES ON.** It gates `BeamFireControlAtbDB` — a real, researchable, buildable, installable **fire-control component** — from contributing to a ship's combat values (`ShipCombatValueDB.cs:320`). Default false, **and no client code sets it true.** ⇒ **a player designs a beam fire-control director to extend weapon reach, and that reach never reaches the resolver.** This is exactly the developer's *"spinning our wheels"* concern, in its purest form: the whole cradle-to-grave chain exists and the last switch is off. |
| **P3-2** | 🟠 | **`EnableGroupPlane` — the 2D battlefield — IS NEVER SWITCHED ON EITHER.** This is the machinery that would give Wings/Formations their own positions (**X11**'s fix, and R-f's prerequisite). Built through slices S0–S2 with tests, and **inert in every game anyone plays.** |
| **P3-3** | 🔴 | **⚠ SYSTEMIC: CI TESTS A CONFIGURATION NOBODY PLAYS.** Ten of the twelve flags are **off in CI and on in the client**. So the green light proves the *default-off* engine is correct and says **nothing** about the combination a player actually runs — closing + fog + weapon-range trigger + weapons-release + mini-hex combat + initial spread + role manoeuvre + the tactical AI, all interacting. **There is no CI configuration that mirrors the shipped game.** This is a plausible root cause for "CI is green but the game misbehaves," and it is cheap to fix: one additional test run with the client's flag set. |
