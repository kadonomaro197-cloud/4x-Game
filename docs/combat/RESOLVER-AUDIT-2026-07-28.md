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

---

## PASS 4 — Determinism, the halt, and the interconnected systems (People, Hazards)

**Docs read first:** root `CLAUDE.md` landmines (combat determinism is locked: *fast-forward == watch*, no RNG in
resolvers without a seeded order-independent stream); `RESOLVER-2D-JOINTS.md` determinism invariants;
`docs/environment/ENVIRONMENTS-DESIGN.md` §hazards-as-one-physics-driven-system; `GameEngine/People/CLAUDE.md`
(the "a person's skill modifies an outcome" wire).

| # | Sev | Finding |
|---|---|---|
| **P4-1** | ✅ | **NO RNG in any resolver** — `CombatEngagement`, `CombatKernel` and `GroundForcesProcessor` are RNG-free. The locked determinism invariant holds at its most important level. *Recorded as verified-good so nobody re-checks.* |
| **P4-2** | ✅ | **The combat halt cannot deadlock.** A `WasInBattle` latch means an ongoing fight does not re-halt every tick, and it clears when fighting stops so a later fresh battle pauses again. Flag-gated (client-on). *Verified-good.* |
| **P4-3** | 🟡 | **Determinism RELIES ON DICTIONARY ITERATION ORDER — a latent risk, not a live bug.** The ground resolve path iterates dictionaries five times (`byRegion` ×3, `byFaction.Keys` ×2 nested), and damage is applied **immediately as the loop runs** (shields drain in place), so the order of attackers affects intermediate state. In practice the order is stable (insertion order follows the unit list, which round-trips through save), so it is **not currently wrong** — but it depends on an unguaranteed implementation detail. §B7.2's own risk list already says the bucketed rewrite must key iteration *"on the sortable key, not dict order"*; **the same rule should be applied to the code that exists now.** |
| **P4-4** | 🟠 | **AN ADMIRAL IMPROVES A FLEET; A BATTALION'S LEADER DOES NOTHING.** Space folds a flagship commander's skill into combat (`CommanderBonuses.CombatMultiplier`, `CombatEngagement.cs:1844` — the People system's "a person's skill modifies an outcome" wire). Ground's `LeaderUnitId` is used **only** to reassign leadership on death and to report the leader's region — **no combat effect at all**, and `GroundCombat/` contains **zero** references to `OfficerCharacter`, `CommanderDB` or `BonusesDB`. **Under R-a this is a straight asymmetry, and it is also a cradle-to-grave gap:** academies, officers and skills feed ships and stop at the shoreline. |
| **P4-5** | 🔴 | **SPACE COMBAT HAS NO ENVIRONMENT AT ALL — and the developer's definition names environment explicitly.** R-g: *"simulate any battle in any condition… in any environment."* Ground applies environmental attrition **during a fight** (`GroundForcesProcessor.cs:225-230` — a unit standing in a damaging hazard bleeds each tick). `CombatEngagement.cs` and `CombatKernel.cs` contain **zero references to hazards.** A fleet fighting inside a radiation cloud or a solar flare takes **nothing** from it. |
| **P4-6** | 🟠 | **AND THE ASYMMETRY IS MIRRORED — each domain built the half the other didn't.** `SpaceHazardTools.cs:61-62` applies **SensorJam → sensor range** and **MovementDrag → move speed** to ships. Ground **generates** both effects and applies **neither** (its own comment: *"the stat effects (SensorJam / MovementDrag) are carried by the data + generator but applied in a later wire"*). ⇒ **hazards affect space movement/sensors but not space combat; hazards affect ground combat but not ground movement/sensors.** A textbook illustration of the two-resolver problem. |

---

## PASS 5 — What decides the SHAPE of a battle (the X9 follow-through)

**Docs read first:** `FLEET-COMBAT-CLOSING-DESIGN.md` (the standoff-vs-brawl decision, the locked "doctrine-only
control" and "scalar per-group range = no 2D" decisions); `RESOLVER-DESIGN.md` §A7 (the propulsion/closing input
surface); `WEAPONS-DESIGN.md` (the weapon triangle).

Two functions decide who controls the range and what range the fight settles at. **Neither takes a doctrine argument.**

| Function | What it actually computes |
|---|---|
| `FleetManeuver(ships)` | the **MINIMUM evasion** across the fleet — the least-evasive ship sets the floor. *(This half is right: it matches the July ruling that a joined force moves at its slowest unit's pace.)* |
| `FleetDesiredRange(ships)` | the **longest FINITE weapon range** in the fleet |

| # | Sev | Finding |
|---|---|---|
| **P5-1** | 🔴 | **THE COMPUTED PREFERENCE CAN ACTIVELY CONTRADICT THE DOCTRINE.** A fleet ordered to *close and brawl* that happens to carry **one** long-range gun will try to **stand off at that gun's range** — the opposite of its orders. A fleet ordered to *kite* whose ships have low evasion **never gets to dictate the range** and is dragged into a brawl. This is the precise mechanism behind X9's post-commencement half: it is not merely that doctrine has no input, it is that **the computed value can override the player's stated intent.** |
| **P5-2** | 🟠 | **ONE LONG-RANGE WEAPON HIJACKS THE WHOLE FLEET'S ENGAGEMENT RANGE.** `FleetDesiredRange` takes the **max** over *every* weapon on *every* ship. A fleet whose main armament is short-ranged but which carries a single long-range missile launcher will hold at **missile range** and **never bring its main guns to bear.** The fleet fights at the range of its rarest weapon. |
| **P5-3** | 🔴 | **AN ALL-BEAM FLEET CLOSES TO POINT-BLANK — a direct consequence of X13.** Because a space beam encodes unbounded reach as `Range_m = 0`, and `FleetDesiredRange` deliberately lets *"unbounded (0) never raise it"*, a fleet armed **only** with beams computes a desired range of **0** and therefore **closes to contact** — despite its guns reaching across the whole engagement. **The 0-means-unbounded convention (X13) inverts the behaviour of the ships it describes.** This is the clearest argument yet for one reach convention. |

---

## PASS 6 — Ammo, retreat, and what a battle leaves behind

**Docs read first:** `WEAPONS-DESIGN.md` (W3 ammo/magazine + the saturation model); `FLEET-COMBAT-CLOSING-DESIGN.md`
(**the retreat verb + break-away timer + the engagement lock**, and the locked *"first shot makes the battle"* rule);
`RESOLVER-DESIGN.md` §B7 (*"You keep what survives"*); `GameEngine/Logistics/CLAUDE.md` (resupply).

**Correction to an expectation before flagging:** I expected space to have no ammo consumption. **It does** —
`FleetCombatStateDB.AmmoPool_kg`, drained by `(ammo J/s × dt) × AmmoBurnKgPerJoule`. Not an asymmetry. What follows
is what the reading actually found.

| # | Sev | Finding |
|---|---|---|
| **P6-1** | 🟠 | **THE TWO AMMO MODELS MEASURE DIFFERENT THINGS.** Space burns ammo **proportional to the energy fired and scaled by elapsed time**. Ground burns a **flat `AmmoPerSalvo_kg = 1.0` per salvo**, regardless of how many weapons fired or how hard. ⇒ **on the ground a rifleman and a ten-cannon tank consume identical ammunition**, and a magazine's size measures **how many ticks you can fight**, not how many shots you can fire. It also re-confirms **C2** at the ammo level: shortening the tick multiplies ammo consumption. |
| **P6-2** | 🔴 | **THE RETREAT DECISION IS A HARDCODED CONSTANT MODULATED BY PERSONALITY — NOT BY DOCTRINE.** `RetreatCasualtyThreshold` is `public const double = 0.5`, and a **personality** trait swings it. Meanwhile **every one of the 25 doctrine entries authors its own `RetreatCasualtyThreshold`**, which (P2-1) has **no runtime field to live in**. So the most consequential in-combat decision — *when do we break off* — is made by a constant and a personality, while the doctrine's own authored value is unreadable. **Direct R-c violation.** |
| **P6-3** | 🟠 | **CORRECTION TO MY OWN EARLIER NOTE.** In M19 I recorded personality as *"biases which doctrine the AI picks, not how forces behave once committed"*, and flagged it rather than ruling it. **That was wrong.** P6-2 shows personality **directly modulates an in-combat behavioural threshold**. Under R-c personality is therefore a second behavioural driver, exactly like role (X1). **Recorded so the charitable reading is not re-used.** |
| **P6-4** | 🟠 | **GROUND HAS THE WISH BUT NOT THE STATE.** Space has a real retreat *state* (`FleetRetreatDB`, `IsRetreat` posture, the casualty threshold). Ground has a `GroundIntent.Retreat` the AI can choose — which issues an ordinary move. **No retreat state, no break-away timer, no engagement lock, no disengagement cost.** "Walking out of a ground fight is free" is confirmed, with the mechanism named. |
| **P6-5** | 🔴🔴 | **⚠ EVERY SCRAP OF BATTLE ATTRITION IS ERASED ON DISENGAGE — the whole `FleetCombatStateDB` is REMOVED** (`CombatEngagement.cs:848`), and its pools **lazily re-seed to FULL at first contact** (`:692`). So on disengaging, a fleet is handed: **full ammunition · full shields · zero heat · a full manoeuvre reserve · and its accumulated partial damage deleted.** Four consequences, each serious on its own: **(a) FREE REARM** — magazines and the entire ammo-logistics chain are meaningless in space; fight dry, disengage, re-engage full. **(b) FREE SHIELD + HEAT RESET** — instant, not regenerated over time; radiators and heat management stop mattering between fights. **(c) THE KITING CLOCK DEFEATS ITSELF** — `ManeuverBudget` exists expressly as *"the kiting clock that makes 'kite forever' impossible"*, and disengaging refills it, so kite-forever is achieved by disengage-and-re-engage. **(d) A FLEET THAT DISENGAGES BEFORE EACH KILL THRESHOLD NEVER LOSES A SHIP** — `DamageTakenPool` holds the accumulated damage that has not yet converted to a whole kill, and it is discarded, so repeatedly breaking off just below the threshold takes **zero permanent losses**. |

**Why P6-5 is the most serious finding of the audit so far.** It is not a missing feature — it is an **exploit that
also silently deletes four designed systems** (magazines, heat/radiators, the anti-kiting clock, and partial damage).
It also directly contradicts **R-g** (*"simulated throughout the entirety of the battle"*) one level up: the battle's
own accumulated state does not survive the battle ending. And it contradicts **§B7's** *"You keep what survives"* —
you keep more than survives.

---

## PASS 7 — Missiles, ordnance and point defence

**Docs read first:** `RESOLVER-DESIGN.md` §A3 "Guided (missiles) — extra insertion points"; `WEAPONS-DESIGN.md`
(the Nature × Delivery taxonomy — Guided delivery, saturation vs point-defense); root `CLAUDE.md` gotcha #3
(missile guidance fixed 2026-06-21, `MissileImpactProcessor` delivers kinetic damage on impact, with an explicit
calibration warning); `GameEngine/Weapons/CLAUDE.md`.

**This is the worst disconnection found anywhere in the audit.**

| # | Sev | Finding |
|---|---|---|
| **P7-1** | 🔴 | **A MISSILE'S ENTIRE DESIGN IS IGNORED IN AUTO-RESOLVED COMBAT — ALL FIVE VALUES ARE HARDCODED.** `ShipCombatValueDB` builds a missile launcher's weapon profile from `MissileLauncherFirepowerStub = 100_000` J/s · `MissileVelocityStub_mps = 5_000` · `MissileTrackingStub = 0.9` · `MissileSaturationStub = 1.0` · `MissileRange_m = 1_000_000`. The class comment admits it: *"v1 stubs (flagged): missile launchers add a flat `MissileLauncherFirepowerStub` each."* ⇒ **warhead size, seeker quality, engine, fuel and range do not exist.** Every missile launcher on every ship of every faction contributes **exactly the same firepower**. Compared with a ground weapon (2 of 10 values designed) or a railgun (all designed), **a missile is 0 of 5.** |
| **P7-2** | 🔴🔴 | **⚠ THERE ARE TWO MISSILE MODELS AND THEY DISAGREE BY UP TO FOUR ORDERS OF MAGNITUDE.** The **live sim** (`MissileImpactProcessor`, built and working per gotcha #3) states its own scale: *"orbital closing speed (1–10 km/s) with a 100 kg dry mass carries **50 MJ–5 GJ** of kinetic energy, which destroys many components in one hit."* The **auto-resolver** gives the same launcher **100 kJ/s**. ⇒ **the same missile does ~100 kJ/s or up to 5 GJ depending purely on which code path resolves the fight.** Root `CLAUDE.md` gotcha #3 already warns the live path one-shots ships; nothing warns that the abstract path is ~500×–50,000× weaker. **These two models have never been calibrated against each other.** |
| **P7-3** | 🟠 | **THE AUTO-RESOLVER NEVER SPAWNS ORDNANCE — the whole built missile system is bypassed.** Zero references to missile spawning/launching anywhere in `GameEngine/Combat/`. So the guidance work, the proximity/impact processor, the ordnance designs and the magazine feed are **all inert inside an auto-resolved battle**. Two systems exist for one weapon; **the stub is the one that fights.** |
| **P7-4** | 🟡 | **POINT DEFENCE IS REAL BUT CALIBRATED AGAINST THE STUB.** `FleetPointDefense` + a **saturating** intercept curve for incoming missile salvos is genuinely built (and `PointDefenseAtb` is one of the 14 attributes that does reach combat). But it is tuned against 100 kJ/s stub missiles, so **its balance says nothing about the 50 MJ–5 GJ missiles the live sim fires.** Fixing P7-1/P7-2 will invalidate every PD number. |
| **P7-5** | 🟡 | **A DOC-HONESTY PATTERN WORTH NAMING.** `RESOLVER-DESIGN` §A3 marks the missile warhead/tracking/range row **✅** with the parenthetical *"(missile is a stub today → wire real values)"*. **A ✅ beside the words "is a stub" reads as done at a glance** and is how this survived a status pass. Same class as the ⚠ *decided-not-built* lesson from the Phase C re-sweep: **the mark and the caveat disagree, and only the mark gets skimmed.** |

---

## PASS 8 — Death, shields, and what is aboard

**Docs read first:** `GameEngine/Damage/CLAUDE.md` (the damage-path decision; `DamageComplex` forward, `SimpleDamage`
dead); root `CLAUDE.md` gotcha #1 and #8; `UNIFIED-RESOLVER-AND-BATTLE-STATS.md` (the shield pool model);
`docs/ground/GROUND-SURFACE-MAP-DESIGN.md` → transport.

| # | Sev | Finding |
|---|---|---|
| **P8-1** | 🟠 | **SHIELDS ARE ONE AGGREGATE POOL PER *FLEET* IN SPACE, AND PER *UNIT* ON THE GROUND.** The space field says so: *"v1: one aggregate pool for the whole fleet (per-ship shields are a later slice)."* Ground carries `GroundUnit.CurrentShield` per unit. ⇒ **focus-firing a single ship cannot strip its shield in space** — the whole fleet shares one bar — while on the ground it can. **Same designed `ShieldAtb` component, two different tactical realities.** Acknowledged v1 shortcut, recorded here because R-a forbids it. |
| **P8-2** | 🔵 | **X15 IS ACKNOWLEDGED IN-CODE, NOT A DISCOVERY.** The resolver's own comment: *"Per-component loss + per-ship hull% are NOT in this model (**ships are whole-or-dead in v1**) — that needs the parked per-component damage sim."* Recorded so X15 is understood as a **known v1 boundary** rather than an accident. |
| **P8-3** | 🔴 | **EMBARKED TROOPS DIE SILENTLY — THE RESOLVER DOES NOT KNOW THEY EXIST.** Loading a ground unit **removes it from the planet's roster** and stores it in the ship's transport blob. When the resolver kills that ship (`Ships[i].Destroy()`), the troops go with it — **which is the correct outcome** — but `GameEngine/Combat/` contains **zero** references to troop bays, loaded units, or transports. ⇒ **no log line, no battle-report event, no notification.** You can lose an entire invasion army and the report will say only *"Transport Alpha destroyed."* The loss is right; the silence is not — and it lands squarely on the operation's OBSERVABLE leg. |

---

## PASS 9 — Detection inside a battle

**Docs read first:** `docs/combat/DETECTION-DESIGN.md` (incl. its corrected `SignalQuality` banner — the strength-only
decision); `docs/combat/INFORMATION-DELTA-DESIGN.md` (fog-of-war-in-combat listed as an ADD).

| # | Sev | Finding |
|---|---|---|
| **P9-1** | ✅ | **FOG *INSIDE* A BATTLE IS BUILT AND IT IS GOOD.** The trigger is an **OR** — `FleetDetects(a,b) || FleetDetects(b,a)` — so a fight starts if *either* side sees the other, and the resolver narrates the consequence: *"FIRST-STRIKE: {A} detects {B}, which is **BLIND** — it takes fire it can't return."* **A blind fleet is shot at and cannot shoot back.** Verified-good; recorded so it is not mistaken for a gap. |
| **P9-2** | 🔵 | **DETECTION IN COMBAT IS BINARY, AND THAT IS BY DESIGN.** `FleetDetects` returns a bool — there is no partial-resolution state degrading accuracy. This is consistent with `DETECTION-DESIGN`'s decision to collapse detection to **strength only**, so it is **not** a defect. Recorded so a later pass does not re-flag it. *(Note the asymmetry it creates with survey, which still uses graduated `SignalQuality` thresholds — deliberate, but worth knowing.)* |

---

## PASS 10 — The test surface, and a correction to my own Pass 3

**Docs read first:** `Pulsar4X.Tests/CLAUDE.md` (the fixture inventory + the CI shard map); `docs/TESTING-TRACKER.md`
Layer 1–3.

| # | Sev | Finding |
|---|---|---|
| **P10-1** | ⚠ **CORRECTION** | **P3-3 WAS OVERSTATED — "CI tests a configuration nobody plays" is too strong.** Re-checked: **every one of the eight client-set behaviour flags has 1–3 fixtures that set it `true`**, and combinations *are* exercised — **`CampaignClockReadoutTests` turns on 7 together**, `ClosingTests` 6, the group-plane fixtures 4–5. **The honest gap is the last few, not the whole set:** the client turns on ~10 and the deepest test combination is 7, so the *full* shipped configuration is never reproduced in one run — but this is a narrow seam, not a blind spot. **Corrected here rather than left standing.** |
| **P10-2** | 🟠 | **49 COMBAT FIXTURES EXIST — AND NOT ONE OF THIS AUDIT'S FINDINGS HAS A TEST.** Combat is among the most heavily tested areas of the project (kernel, dodge, shields, triangle, stress, performance, closing, reengage, risk, readout, battle-log, trigger, commander bonuses…). Yet **nothing gauges**: that disengaging refills ammo/shields/heat/manoeuvre (**P6-5**), that a missile's design is ignored (**P7-1**), that combat values never refresh after damage (**X14**), that one long-range gun hijacks a fleet's range (**P5-2**), or that an all-beam fleet closes to point-blank (**P5-3**). ⇒ **the suite proves the code does what it does; it does not prove the code does what the DESIGN says.** Every fix in the eventual work list needs its gauge written from the *design* statement, not from current behaviour — otherwise the tests will lock in the bugs. |

---

## PASS 11 — The build→combat seam (does a built ship enter a fight correctly?)

**Docs read first:** `GameEngine/Fleets/CLAUDE.md` (fleet assembly, the home-reserve rule); `Industry/CLAUDE.md`;
`docs/earthfall/` sealift notes (`ProvisionBuiltShip` → charged reactors + filled tanks).

| # | Sev | Finding |
|---|---|---|
| **P11-1** | 🟠 | **"ARMED" IS A PROPERTY OF THE DESIGN, NOT THE SHIP — so a gutted hull is still a warship to the AI.** `ConquerResolver.IsWarship(ShipDesign)` asks whether the **design** carries any of six weapon attributes. It cannot see whether those weapons still exist on the built hull. Combined with **X14** (combat values are frozen at construction and never refreshed by damage), a ship that has lost every gun reads as **both armed *and* at full firepower** — so `FleetAssembly` sweeps it into a fighting fleet and `ConquerResolver` commits it to battle. *The design-vs-built distinction is known — the neighbouring comment says "a per-design proxy because true firepower needs a built ship" — but the consequence when the built value never updates is not recorded anywhere.* **This is X14's concrete operational cost, not a separate defect.** |

---

## PASS 12 — Save/load of a battle in flight, and static state

**Docs read first:** root `CLAUDE.md` gotcha #7 (`TypeNameHandling` + save schema), landmine **L12** (a `*DB` without
`Clone()` silently becomes a bare `object`); `CONVENTIONS.md` (copy-ctor / `Clone()` discipline);
`Pulsar4X.Tests/CLAUDE.md` (the shared-static-counter constraint that forces process-level CI sharding).

### ⭐ THIS PASS FOUND NO DEFECTS. All four checks came back clean.

| # | Sev | Finding |
|---|---|---|
| **P12-1** | ✅ | **The dangerous latch is saved, deliberately and with its reason written down.** `SpreadRegions` is `[JsonProperty]` **and** deep-copied in the copy-ctor, and the field comment states why: *"a mid-battle save must NOT forget which regions were already [spread]."* Without this, loading mid-battle would **re-spread the sides and teleport units apart**. Handled. |
| **P12-2** | ✅ | **The harmless latch is deliberately NOT saved.** `WasInBattle` is `[JsonIgnore]`, so loading during an ongoing ground battle **re-halts the clock once**. That is a considered trade-off (and arguably desirable — the load announces the battle), not a bug. |
| **P12-3** | ✅ | **The resolver's only mutable static is a pure liveness gauge.** `CombatEngagement.TickCount` is `Interlocked.Increment`-ed and read by `MasterTimePulse` as a before/after delta to prove combat ran. **No behaviour depends on it**, so a save/load resetting it is harmless. |
| **P12-4** | ✅ | **`GroundForcesDB` has a proper `Clone()`** (`=> new GroundForcesDB(this)`) with a full deep-copying copy-ctor — **no L12 exposure** on the combat roster. |

---

## 📉 RATE CHECK — the drop-off the developer asked to watch for

| Passes | New defects found |
|---|---|
| 1–3 | 15 |
| 4–5 | 9 |
| 6–7 | 10 |
| 8–10 | 5 (**2 of them corrections to my own earlier findings**) |
| 11–12 | **1** (and it is a *consequence* of X14, not a new defect) — **Pass 12 found ZERO** |

**The rate has bent sharply.** The last five passes produced one genuinely new defect between them, plus two
self-corrections and five verified-good results. **This is the "negligible level" signal** — though the remaining
un-swept lenses are named below.

### Still un-swept (candidates for passes 13+)
- **The ground↔space seam** — orbital bombardment support during a ground battle (audit P3 / slice S9 territory).
- **The AI's post-battle reaction** — what a faction does with a won/lost battle (`AIDecisionRecorder`, objective re-plan).
- **The readouts** — `CombatReadoutTests`, the Battle Report's 250-event cap vs a long fight, `game_logs` volume.
- **Calibration** — the ~15 `FLAGGED` balance constants and whether any are load-bearing on the findings above.

### The one-sentence pattern behind all 42 findings

> **The designer models COMPONENTS; the resolver models TOTALS.** Every defect found is a place where a designed
> detail — a warhead, a fire-control director, a magazine, a wounded ship, a commander, a hazard, a doctrine field —
> is flattened into a number *before* the fight starts, and can never influence it again.
