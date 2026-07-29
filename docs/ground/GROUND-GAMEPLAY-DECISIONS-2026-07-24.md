# Ground Gameplay — the developer's DECISIONS (2026-07-24)

**Status:** 🔒 decisions recorded, build pending. Follows the `docs/earthfall/EARTHFALL-DECISIONS.md` pattern — the
developer's answers, captured verbatim-in-essence so a future session doesn't re-ask or guess. Produced from a
two-pass survey (8 domain readers + 8 cross-system connection readers, ~4M tokens, file:line throughout).

**Read with:** `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the board), `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §12`
(the rules), `GameEngine/GroundCombat/CLAUDE.md` (as-built).

---

## The survey's headline (what prompted these decisions)

**Ground gameplay is ~85% built engine-side, ~30% reachable by a player, and 0% observable.**

The engine is genuinely good and CI-gauged: an assembler deriving stats from parts, terrain-weighted A* on a wrapping
planetary grid, a deterministic salvo resolver with shields / armour-by-damage-nature / fortification / cover, stances,
rules of engagement, an order queue, a tactical AI, inter-world troop lift, and real-metre tactical combat. Then:

- **A stock New Game raises no troops, spawns no enemy, and builds no fleet** (`NewGameMenu.cs:52/55/60` all false).
  The ground layer runs perfectly over an empty world — *nobody has ever seen it work in a normal game.*
- **Five ground behaviour flags are process-wide statics set only on the New Game path.** Loading a save doesn't set
  them, so the same save plays differently depending on whether you started a new game earlier in the same session.
  Every CI gauge therefore runs with the behaviour OFF.
- **The entire `GroundCombat` folder emits zero log lines, zero events, zero battle records.** The clock stops, the UI
  points at the SPACE combat tab, and opens an empty space report.

---

## The decisions

### A — Designing a unit

| # | Question | **RULING** |
|---|---|---|
| 1 | Two design paths, or one? | **ONE designer for everything in the game. The prebuilt templates GET DELETED.** (Restores the locked "no labelled unit types" principle.) |
| 2 | Are Penetration / Per-Shot-Energy unit or weapon properties? | **Properties of the WEAPON.** |
| 3 | Invalid design — block or warn? | **Blocked from SAVING.** |
| 4 | Should ground parts cost research? | **YES — and the cost SCALES WITH COMPLEXITY.** |
| 5 | Hazard counters — how many components? | **Every hazard gets a counter, and the COMPONENT DEPENDS ON THE HAZARD.** The counter must make physical sense for that hazard, and a hazard may hit several stats at once. *Developer's worked example: a dust storm might need advanced radar to see through it, and it limits movement speed and reduces accuracy.* So a counter is not one generic "resistance %" — it is the specific gear that answers that specific menace. |

### B — Building and fielding a unit

| # | Question | **RULING** |
|---|---|---|
| 6 | Where does a new unit muster? | **A BUILDING SETTING, for BOTH space and planetary units.** Planetary units go to a chosen planetary coordinate; space units go to a particular orbit. (One rally-point concept, two address types.) |
| 7 | Do ground units cost people? | **YES — and there is NO return. They die.** |
| 8 | Prebuilt units carry upkeep + magazine; should ammo bite? | **YES.** *(Note: ruling #1 deletes the prebuilt templates, so this becomes "the one designer's output always carries upkeep + a magazine.")* |

### C — Buildings and the surface economy

| # | Question | **RULING** |
|---|---|---|
| 9 | Free region button vs costed tile queue? | **COSTED TILE QUEUE. NOTHING IS FREE.** (The free "Build here" path is deleted.) |
| 10 | Where does the "this building has a home" hook live? | **WRONG QUESTION — the developer reframed it: "route them all into ONE and make a build queue that progresses like any other RTS."** There is no "hook" to place because there is no longer more than one path. **ONE build queue for everything** (ground units and buildings alike), every entry carrying its destination, with visible progress. The four divergent paths collapse into one; the free "Build here" button dies with ruling #9. *Most of this already exists* — Pulsar's `IndustryJob` queue already does batch / repeat / priority / cancel / auto-install; the work is routing ground through it properly, adding the destination field, and showing progress. |
| 11 | Does every building occupy ground? Is "occupies a tile" the same as "war-map objective"? | **YES, every building occupies ground. And they are THE SAME attribute** (one flag, not two). |
| 12 | Fund employment + power, or cut? | **FUND THEM.** |
| 13 | What makes a tile good to build on? | **It depends on the tile type and WHAT MAKES SENSE.** A grassland tile with a food-production building gets a bonus *because it makes sense*. Different components get different bonuses for the building they're on and where it's built. (Terrain affinity is semantic, not a generic multiplier table.) |

### D — Movement

| # | Question | **RULING** |
|---|---|---|
| 14 | Which coordinate is authoritative? | **DELETE "march to region" entirely.** Planetary units move on a **TWO-LAYER COORDINATE SYSTEM** written like `(17,09)(22,47)` — the first pair is the **regional hex**, the second is the **mini hex**. One address, two layers. |
| 15 | Should walking out of contact be free? | **NO — the fight is COMMITTED.** Once the auto-resolver starts, it ends exactly two ways: **every unit on one side dies**, or **one side calls a RETREAT order**. Retreat starts a **break-away timer** (~1–2 hours, tuned to what makes sense) during which the units are disengaging. **PURSUIT is a DOCTRINE the opponent can choose.** All the priced-exit options (parting shot, delay, pursuit, fighting withdrawal) are on the table — they become **doctrine entries**, not hardcoded rules. |
| 16 | Is mini-hex movement a player order? | **YES — mini-hex movement works JUST LIKE regional hex movement.** (Same verb, finer layer — consistent with the #14 two-layer address.) |
| 17 | What to show while marching? | **SHOW ALL FOUR** — destination, distance remaining, ETA, current speed. |

### E — The fight

| # | Question | **RULING** |
|---|---|---|
| 18 | Player target selection? | **YES — but also dependent on the DOCTRINE of the battalion.** |
| 19 | Battle scope + an engage decision? | **Battle is scoped to REAL DISTANCE** (not the region band). **And yes to an engage decision — dependent on orders or doctrine.** |
| 20 | How is a battalion composed? | **The same way it's done in SPACE.** (Ground mirrors the fleet composition model — consistent with the existing "formation is the ground echo of a fleet" parity map.) |
| 21 | What does capturing a planet transfer? | **OPEN — still thinking.** |
| 22 | Is one health scalar enough? | **ALREADY DECIDED IN THE DOCS — the question should not have been asked.** `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §14.2` Part B designs the `CasualtyTier` wound function (returns untouched / wounded / dead, works for a model squad *and* a monolith) plus a damage ledger totalled at battle end; `docs/ground/GROUND-UNIT-VARIABLES.md` designs the shared **Training** dial (build-time + scarce talent + credits → veterancy, militia→elite on one slider). **Both designed, neither built.** Treat as settled design awaiting build. |
| 23 | How long is a battle meant to last? | **A fire rate EXISTS but must be CALCULATED, not stored as a lump.** A weapon carries a **rate** (e.g. 10 damage/second). Each tick the resolver **integrates that rate over the tick's duration against the available targets and works out who actually died and who is still alive** — developer's example: *a machine gun at 10 dps, 500 reachable targets at 200 health each, in one tick → compute it out.* **Applies to ALL weapons**, not just ground. |

### F — Showing the ground war

| # | Question | **RULING** |
|---|---|---|
| 24 | What is the ground battle readout? | **A LOG, for now.** (Cheapest, and it stops the combat interrupt from lying.) |
| 25 | Ownership drawing + unit inspection surface? | **Ownership: deal with it LATER.** Inspection: **a HOVER TOOLTIP on the map, AND the same detail in Force Management.** (Two surfaces: quick read in place, full read in the roster.) |
| 26 | Contact memory? | **YES — a lost contact leaves a FADING LAST-KNOWN marker** (matching how space contacts behave). |

### G — Defaults and lifecycle

| # | Question | **RULING** |
|---|---|---|
| 27a | Should ground flags live in the SAVE rather than process statics? | **YES — make the save work.** Each campaign carries its own settings, so a save always plays the way it was created, and CI can test the configuration players actually run. |
| 27b | Should a New Game raise a garrison and place an enemy by default? | **NO.** |

---

## The #23 correction (fire rates)

The developer challenged: *"there are legitimate fire rates on units what do you mean?"* **Verified against source —
there is no fire rate on a ground weapon.** `GroundWeaponAtb` carries exactly five fields: `Mass`, `Attack`, `Range`,
`Mode`, `Range_m` (`GroundWeaponAtb.cs:33-45`). No rate-of-fire, no reload, no shots-per-interval.

`Attack` is a **flat lump of damage**, and `GroundForcesProcessor` applies it **in full, once per game hour**
(`RunFrequency = TimeSpan.FromHours(1)`, `:29`). So the effective fire rate of every ground weapon in the game is
identical — *once per hour, full strength* — and it is a property of the **processor tick**, not of the weapon.

That is why battle length is currently set by the tick rather than by the guns, and why the order queue rarely matters
(most fights end before a second waypoint pops). **If per-weapon fire rates are wanted, that is a new dial** — and it
pairs naturally with ruling #2 (penetration/energy moving onto the weapon) and #8 (ammo biting), since all three are
per-weapon properties landing on the same object.

---

## THE DOCTRINE BUILD PLAN (D1 → D3b) — what a doctrine actually IS

**The developer's framing, confirmed 2026-07-27:** *"these doctrines are what control the movements of the particular
units ONCE the auto resolver has started and the combat is initiated"* — and *"everything goes through the doctrines."*

So a doctrine is **not** a slider the player tunes and **not** a strategic map setting. It is a **named posture assigned
to a formation or sub-fleet in formation management**; the numbers behind the name are authoring data. Once the resolver
takes over, that posture is the steering wheel — it decides how those units move and fight for the rest of the
engagement. The player's only other input is the retreat call. (A doctrine CAN be switched mid-fight: doctrine changes
are a direct call that deliberately bypasses the engagement lock.)

| Slice | What it does | State |
|---|---|---|
| **D1** | The unified blueprint shape + the pure reader + the reciprocal guard | ✅ built, CI-green (`97ecd5b`) |
| **D1b** | The ROLE catalog (25 doctrines) + the client domain filter | ✅ built (`b218acf`), CI pending |
| **D2** | Ground reads the unified catalog; retire `groundStances.json` | pending |
| **D3a** | **MOVEMENT steering** — doctrine decides where units go inside the fight | pending |
| **D3b** | **FIRE behaviour** — targeting, retreat threshold, break-away, pursuit | pending |

### D3a — the movement half (the developer's actual point)

**Verified state (2026-07-27), and it makes this cheaper than expected:**

- The in-engagement movement machinery **already exists and runs inside the resolver** on both sides:
  ground `GroundForcesProcessor.ApplyEngagementManeuvers` (`:263`) → `GroundRoleComposer.RoleMoveAway` → `StepMiniToward`;
  space `CombatEngagement.AdvanceClosing` (`:785`).
- `RoleMoveAway` already returns exactly the tri-state a closing intent needs: **`false` = close · `true` = kite ·
  `null` = hold**.
- Ground **already has the intent concept** — `GroundEngagementStance` (Hold / Close / Stand-off) on the formation gates
  *whether* a unit auto-maneuvers, while the ROLE decides *which way*. It is simply a **separate setting, not part of
  doctrine.**
- **Doctrine has ZERO movement authority today.** Of the movement dials the catalog carries only `SpeedMult`, and
  **nothing reads it** (verified: the only `SpeedMult` hits in the engine are unrelated — hazard drag and locomotion).
  The blueprint's own comment, *"v1: stored, applied in v2,"* is still literally true.

**So D3a is mostly FOLD + REDIRECT, not new machinery:**
1. Add **`ClosingIntent`** (Close / Hold / Standoff / Kite) to the blueprint + a `CombatDoctrine.ParseClosingIntent`,
   authored across the catalog. This is the dial that says *where the unit wants to be* relative to the enemy.
2. Make `RoleMoveAway` / `AdvanceClosing` consult **the formation's doctrine FIRST**, falling back to role when the
   doctrine doesn't specify. *This is the line that transfers steering from "what I am" to "what I was ordered."*
3. Fold ground's existing `GroundEngagementStance` into the doctrine's `ClosingIntent` so there is one intent, not two.
4. Make `SpeedMult` finally bite on those maneuvers.

**Why it must land before Standoff Barrage is added to the catalog:** without step 2, assigning an artillery doctrine
maneuvers identically to unassigned artillery — the name would be pure decoration. That doctrine was deliberately held
back from D1b for this reason.

### D3b — the fire half

Targeting priority (replacing the spread-by-current-health allocation that shoots the healthiest and never finishes a
cripple), the per-doctrine retreat threshold (replacing the flat 0.5 constant), the break-away timer under fire, and
pursuit. Each behind its own flag, byte-identical off.

---

## THE NEXT DESIGN ITEM the developer named: the DOCTRINE CATALOG (planetary + space, leader-modulated)

Rulings #15, #18, #19 and #20 all resolve *into doctrine* rather than into hardcoded rules — the developer's words:
*"everything you mentioned are options we just gotta fill out what the different doctrines are for planetary and space
combat. which are also affected by leaders."* So doctrine stops being a small stance multiplier and becomes **the
container for every combat behaviour decision**.

**What exists today is thin.** The entire ground doctrine surface is **three** JSON entries
(`GameData/basemod/.../groundStances.json`), each carrying only an attack multiplier, a damage-taken multiplier and a
cooldown:

| Stance | Attack | Damage taken | Cooldown |
|---|---|---|---|
| Balanced | ×1.0 | ×1.0 | 60 s |
| Offensive Push | ×1.25 | ×1.25 | 300 s |
| Dig In | ×0.75 | ×0.75 | 300 s |

Plus a separate 3-value rules-of-engagement enum (`GroundEngagementStance`: Hold / Close / Stand-off) and, on the space
side, `FleetDoctrineDB.EngagementPosture`. **Nothing carries retreat, pursuit, or targeting behaviour**, and nothing is
leader-modulated.

**What the catalog must now express** (each entry, for ground AND space, since the developer wants parity):
- **Engage** — do we start this fight at all (ruling #19)?
- **Target priority** — who do we shoot first (ruling #18): armour-first, artillery-first, weakest-first, closest?
- **Retreat trigger** — when do we call it, and the break-away timer (ruling #15).
- **Pursuit** — do we chase a retreating enemy (ruling #15)?
- **Stand-off vs close** — the existing ROE, folded in.
- **The existing multipliers** — attack / damage-taken / cooldown.
- **Leader modulation** — how a commander's character bends the above. *(The substrate exists: `CommanderDB` already
  carries a `PersonalityDB`, and `OfficerCharacter.Blend`/`TenureWeight` already blend an officer's own character toward
  faction doctrine by tenure — it is wired into the space retreat decision today. Ground has no equivalent read yet.)*

It is **data-driven and moddable already** (`ModDataStore.GroundStances` reads the JSON), so growing the catalog is
mostly authoring + the resolver reads, not new architecture.

---

## ⚠ The calibration problem ruling #23 creates — PARTIALLY RULED 2026-07-27

> **RULED (developer, 2026-07-27): dial 1 — SHORTEN THE COMBAT TICK** ("shorten the combat tick and yes lets build
> the doctrine catalog"). Space's 5 s `CombatReactionStep` fine-step is the precedent: ground should run a fine step
> while a battle is live and stay hourly otherwise. **Still OPEN:** the tick VALUE itself, and the mid-tick overkill
> semantics — "sequential down the priority list" below is an INTERPRETATION of the developer's phrasing, not a
> ruling; confirm it before building (it ties to ruling #18). The three-dial analysis below is kept as the record of
> the options considered.

A **rate** model makes the **tick length balance-critical**, in a way the current lump model hides.

The ground resolver runs on a **1-hour** hotloop (`GroundForcesProcessor.RunFrequency`). Under the new rule, a 10
damage/second weapon delivers `10 × 3600 = 36,000` damage in one tick — enough to kill **180** of the developer's
200-health units, from a single machine gun, in one pass. The arithmetic is exactly what was asked for; the *tick* is
what makes it absurd.

Three dials can absorb it, and the choice is a design decision, not a bug fix:
1. **Shorten the combat tick.** Precedent exists: space already fine-steps to a 5-second `CombatReactionStep` when an
   engagement is imminent. Ground could run a fine step while a battle is live and stay hourly otherwise.
2. **Author small rates.** Keep the hourly tick and treat "damage per second" as a scaled abstraction.
3. **Cap engagement width** — a weapon can only service so many targets at once (a traverse/retarget limit), so surplus
   damage is wasted rather than spread across 500 enemies.

**Related sub-question the example raises:** when a target dies mid-tick, does the weapon's remaining damage roll onto
the next target (sequential, no waste) or is it lost (simultaneous, overkill is real)? The developer's phrasing —
*"all of which that died or are still alive should be calculated"* — reads as **sequential down the priority list**,
which ties this directly to ruling #18 (doctrine sets that priority order).

---

## Consequences worth flagging before build

> **⚠ FACTUAL CORRECTIONS FROM CODE, 2026-07-27 (OPERATION GROUND TRUTH — see `docs/DOCS-AUDIT-2026-07-27.md`).**
> **The RULINGS below stand unchanged — they are the developer's decisions.** What was wrong was some of this
> section's *claims about what the code does*, and those claims were steering the build order. Four corrections:
>
> 1. **Ruling #1's stated blocker is WRONG.** `GroundStartGarrison` does **not** raise the prebuilt templates —
>    `MakeGarrisonDesign` (`GroundStartGarrison.cs:90-103`) builds throwaway C# `GroundUnitDesign`s, so deleting
>    the templates does **not** break the start garrison. **The real blocker is different:** the prebuilts are
>    the only ground units the AI can actually build **in a stock game**. Stated precisely (verified
>    2026-07-27): `GroundReinforcement.IsBuildableGroundUnit` is a **generic** predicate — it accepts any
>    `GroundUnitDesign` *or* any `ComponentDesign` carrying `GroundUnitAtb` — but **exactly 3 base-mod
>    templates carry that attribute**, and they are the three prebuilts (`TemplateFiles/installations.json`).
>    So the AI's reinforcement path is fine in principle and empty in practice the moment they are deleted.
>    #1 therefore needs a scenario/AI-authorable design source before the templates can go. **The forced order #2 → #4 → #1 still holds** — for the *other* reason given here, which
>    is confirmed: penetration/per-shot-energy have no home on the weapon yet.
> 2. **Ruling #9 has TWO free paths to kill, not one.** Besides the free "Build here" order there is a **second
>    reachable, materials-free path**: the `LocalConstructionDB` queue (Colony Management → **Construction**
>    tab) lists every `PlanetInstallation` design — *including infantry/armor/artillery* — and its processor
>    spends only `PointsPerDay`, **never `ResourceCosts`** (`LocalConstructionProcessor.cs:33,50`), then calls
>    `AddComponent`, which fires `GroundUnitAtb` and raises a real unit. **No test covers this queue at all.**
> 3. **The #9 ⟷ #10 pairing is worse than stated, and the gauge would not catch it.**
>    the fortification **value is summed only from `Region.InstallationIds`** — both `SumLocal`
>    (`GroundFortification.cs:56-57`) and `SumAdjacent` (`:88-89`) iterate that one list, and the doc comment
>    at `:100` names `PlaceInstallationInRegionOrder` as its writer. Hex-level `InstallationIds` are read
>    **only subtractively** (`CapturedBuildingIds`, `:79-80`, so a building on an enemy-seized hex stops
>    fortifying). **So writing hexes alone can never produce fortification.** The costed tile queue does not
>    write the region list, which means deleting the free path leaves a colony player with **no buildable
>    fortification whatsoever** — and CI would stay green through it. The costed path must write that list
>    *in the same slice*.
> 4. **Ruling #14 removes a WORKING verb, not a broken one.** "March to region" is the **only fully-wired
>    planetary move verb** — live in the primitive, the order enum, the processor, the **AI tactical brain**,
>    **both** client windows, the Site engine, and a save/load fixture. Four coordinate systems coexist today.
>    Deleting it without the two-layer replacement already in place removes the only way anything moves.
>    *(The "token never moves" observation may still hold for the global-position restamp — treat it as
>    unverified this pass, not as licence to cut the verb cheaply.)*
>
> Also corrected: **`CrewReq` is not on a ground design at all.** `GroundUnitDesign` has **no crew field**, and
> `GroundUnitAssemblyResult` does not even sum crew (unlike its station and building siblings) — so ruling #7's
> note below overstates how close it is. The ship/station manpower machinery is genuinely built and gauged; the
> ground side has nothing to connect yet.

- **Ruling #1 (delete the prebuilt templates) is load-bearing and wide.** The three prebuilt units are today the *only*
  path to penetration and per-shot energy, ~~they are what `GroundStartGarrison` raises~~ *(corrected above — the
  garrison builds its own designs; the real blocker is that they are the AI's only buildable ground unit)*, and they are
  referenced from the base mod and several tests. Deleting them requires ruling #2 to land first (the dials must exist on
  the weapon before their only carrier is removed), plus a garrison-composition replacement. **Order: #2 → #4 → #1.**
- **The JSON binder is exact-arity** (gotcha #6/#10). Adding penetration + per-shot-energy to `GroundWeaponAtb` means
  updating **all five** ground-weapon templates in lockstep or every one of them fails to bind.
- **Ruling #7 (units cost people, no return) connects ground to population for the first time.** The templates already
  declare `CrewReq` 40–100 and it is computed onto the design and never read; the manpower machinery already works for
  ships and stations. This is the wire that makes the population system notice a war.
- **Ruling #9 (nothing is free) deletes a path players can currently use** to make unlimited instant infantry and
  buildings. It is also, today, the *only* path that produces a building which actually fortifies — so #9 and #10 must
  land together or ground defence silently breaks.
- **Ruling #14 (delete "march to region") removes a currently-broken path**, not a working one: every region-march
  button, every queued region move, and every AI move sets the region index without restamping the global position, so
  the token never moves on the map.

---

# ADDENDUM — THE MOVEMENT & GEOGRAPHY RULINGS (developer, 2026-07-28)

**Status: 🔒 CANON. These override every older document, including anything in this file above them, and including
`docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md`.** They were given in dialogue after a code walk-through of how
movement actually works. Where an older doc contradicts one of these, the older doc is **wrong and gets corrected or
deleted** — not reconciled.

## The governing law it starts from — ⭐ ONE VERB, BOTH SEATS

> **"Movement planetside must be just like movement spaceside, meaning the AI must be able to do it — and thus it
> must be simple."**

Generalised, because it is the sharpest design rule in this project and it is not about movement:

> **If the AI cannot use a mechanic with the SAME primitive the player uses, the mechanic is too complex. Full stop.**
> Complexity only a human can drive is not depth — it is a system with half its players locked out. Two parallel paths
> for the same verb (one for the player, one for the AI) is the failure this law forbids.

**It has immediate teeth.** The client's click-to-march calls `GroundForces.OrderMoveToGlobalHex(...)` **directly**,
bypassing the order queue — so it carries no issuer marker, cannot be sequenced, and **the AI cannot use it at all**,
because the AI issues orders through the queue. That is a two-path violation and it is why the direct call is deleted
rather than kept as a convenience.

## The rulings

| # | Ruling |
|---|---|
| **M1** | **ONE movement layer, not three.** The coarse region hop, the region-local hex march, and the global cylinder march collapse into a single movement system. |
| **M2** | **REGIONS ARE A VISUAL AID.** They are a display grouping — not a movement layer, not a combat container, not the unit of capture. |
| **M3** | **Position is a TWO-PART ADDRESS: the regional-hex address + the mini-hex address.** Movement between hexes is coordinate-based on that pair. |
| **M4** | **The player orders a unit to a specific mini hex inside a specific regional hex** — e.g. *"to mini-hex (22,47) in regional hex (17,09)."* The mini hex is directly addressable, not an engine-only placement. |
| **M5** | **TRANSITIONAL HEXES exist at the REGIONAL-HEX level** so the whole planet is seamlessly connected — connective ground between the per-regional-hex mini patches, so there is somewhere to stand and something to path across at a boundary. |
| **M6** | **Combat by proximity works at the MINI-HEX level.** That is what the regional hexes and the transitional hexes exist to serve. |
| **M7** | **Combat starts when the auto-resolver starts, which is when one party enters weapons range** — already the design. On the mini-hex map, **weapons range is drawn as a RED border and sensor/radar range as a WHITE border.** |
| **M8** | **CAPTURE IS PER-HEX.** You capture *that hex*. Region capture is gone. *"What that means doesn't matter much right now — once everything is built we can determine what victory actually means."* ⇒ **the victory condition is a WRITTEN DEFERRAL, not an open design hole** (and is distinct from ruling **#21**, what a capture *transfers*, which remains OPEN). |
| **M9** | **ORDERS ARE ISSUED FROM THE FORCE MANAGEMENT WINDOW, AND THAT IS IT.** One order surface. |
| **M10** | **Fortification's region-adjacency is DELETED** — no more "a bunker shields adjacent regions." |
| **M11** | **HAZARDS ARE PER-HEX, derived from hex type + geography** and any other variable specifiable at that level of the planet. Not per-region declarations. |
| **M12** | **The AI tactical brain should be HYBRID if it can be managed** — coarse at the regional-hex level, fine at the mini-hex level. *(Scope of "hybrid" to be confirmed.)* |

## M13–M15 — the follow-up rulings (developer, 2026-07-28, same dialogue)

| # | Ruling |
|---|---|
| **M13** | **THE PLANET MAP STAYS CLICKABLE — for SELECTION and DISPLAY, never for orders.** You click a unit on the surface map to select it and to see its **RED** weapons-range border and **WHITE** sensor border. The **order** is then issued in **Force Management** (M9). So the map stays a live instrument; it just stops being an order surface. |
| **M14** | **THE AI BRAIN IS HYBRID ACROSS THE TWO ADDRESSES:** it thinks **coarse at the regional-hex level** to decide *where to go*, and **fine at the mini-hex level** to decide *how to fight*. Same split as the two-part address (M3) — which is what makes it manageable, and what keeps it inside **One Verb, Both Seats** (the AI reasons at the same two scales the player orders at). |
| **M15** | **ONE NAME FOR THE BIG HEX: "REGIONAL HEX."** The developer did not recognise *"operational hex"* — and a survey found the same object carries **five competing names**: "operational hex" (5 docs / 11 code files), "war-map hex" (2/5), "coarse hex" (5/4), **"regional hex" (4 docs / 0 code)** and "global hex" (2/11), with the code field itself named `GlobalQ`/`GlobalR`. **A term the developer does not recognise is a term that must not be in the docs.** Standardise every doc and every UI string on **regional hex** (and **mini hex** for the small one). ⚠ *The code field rename is OPTIONAL and has a cost — `GlobalQ` is a `[JsonProperty]`, so it is written into save files; renaming needs a compatibility alias. Fix the docs/UI now; treat the field rename as a later tidy.* |

**The vocabulary, fixed (use these two words and no others):**
- **REGIONAL HEX** — the big hex you march between. ~477 km across on Earth. The scale of march and objectives.
- **MINI HEX** — the little hexes inside it, **13 across** ⇒ ~37 km each. Where units stand, shoot, and where buildings sit.
- *(A **region** is neither — it is a **visual grouping** of regional hexes, M2.)*

## M16 — CAPTURE IS A HYBRID: mini-hex ownership is the truth, regional-hex ownership is the ROLL-UP (developer, 2026-07-28)

**The ruling.** Both levels, not one. **You capture MINI hexes. A REGIONAL hex flips when you hold the MAJORITY OF ITS
OCCUPIED mini hexes.** (Developer, asked "why not a hybrid of both?" — and it is the better answer; the earlier
recommendation of regional-hex-only is withdrawn.)

**Why it is the cheapest option, not the most expensive — it reuses a pattern that is already locked and working.**
`CityGrid.cs:14` names *"the roll-up invariant"*: buildings physically live on **mini** tiles and the **regional** hex
keeps a maintained **aggregate** of them (`GroundHex.InstallationIds`). **M16 is that same pattern applied to
ownership.** Fine layer is the truth; coarse layer is a maintained summary. Same shape, same machinery, same discipline.

**And it satisfies One Verb, Both Seats.** The AI never tracks ~169 tiles as ~169 decisions — it reads **one derived
number per regional hex** ("do I hold it, and how much"). Fine grain for manoeuvre, coarse grain for decisions: the
same split as **M14**.

### 🧨 THE ONE GUARD — one source of truth, or we rebuild the bug we just deleted

**Ownership is STORED in exactly one place: the mini hex. The regional hex's owner is DERIVED and never independently
written.** Today it is the other way round: `GroundHex.OwnerFactionID` is a stored `[JsonProperty]` (`GroundHex.cs:26`)
and `GroundBuildings.CaptureRegionHexContents(regionsDB, regionIndex, newOwner)` (`:187`) writes it **top-down**,
flipping every hex in a region at once. **M16 inverts that direction.** If both levels stay stored *and* writable, they
drift — and two ownership records that can disagree is exactly the region-versus-hex bug M2/M6 exist to delete.

### The rules, stated so they can be built without a second conversation

| Question | Rule |
|---|---|
| When is a mini hex **OCCUPIED**? | It carries a **living unit** *or* a **building** (`CityTile.BuildingInstanceId >= 0`). Empty ground is not occupied and does not count. |
| Who **owns** an occupied mini hex? | The faction whose unit stands on it; if no unit, the faction owning the building. |
| When does the **regional hex** flip? | Whoever holds the **majority of its occupied mini hexes**. **Derived** — recomputed with the roll-up, no separate capture event. |
| **Empty** regional hex (zero occupied tiles) | **Unowned.** Walking in with one unit makes it 1-of-1 ⇒ majority ⇒ yours. *An undefended regional hex is taken by arriving, which is correct — there is nothing there to fight for.* |
| **Tie** | **No flip.** It stays with its current owner (or unowned) — a tie is a stalemate, which is the honest outcome. |
| Why not "all tiles"? | **Rejected:** it recreates the *"every region uniformly held"* rule **M8** just deleted — one enemy squad in a corner blocks a capture forever. |
| Why not "majority of ALL tiles"? | **Rejected:** garrisoning empty desert would count toward taking a hex. |

*Not a FLAGGED balance value — the developer ruled the threshold explicitly (majority of occupied). The only genuinely
tunable part is the definition of "occupied", set above.*

### What this needs in code — ONE new field, and one inversion

1. **NEW: `CityTile.OwnerFactionID`** (default `-1`). `CityTile` today carries only `Q`, `R`, `Terrain`,
   `BuildingInstanceId` (`CityTile.cs:18-24`) — **there is no owner at the mini level yet.** ⚠ **Add it to the
   `[JsonProperty]` set AND to the copy-constructor** (`:28`) — `CONVENTIONS.md` discipline; a field left out of the
   copy-ctor silently drops when a grid is copied. Save-compatible (a new field defaults on load).
2. **`GroundHex.OwnerFactionID` becomes a maintained roll-up**, computed from the tiles — not written directly.
3. **Invert `CaptureRegionHexContents`** (`GroundBuildings.cs:187`) — it currently pushes an owner *down* from a region
   to all of its hexes. Under M16 ownership only ever flows *up*.
4. **Already deleted by M8:** region capture and `TryCapturePlanet`'s all-regions-uniformly-held test.

## M17 — THE THREAT READ IS ONLY WHAT THE UNITS CAN SEE (developer, 2026-07-28)

**The ruling:** *"For the ground threat read it should ONLY be based on what the AI can see, which is based only off
what the units can see."* ⇒ **Threat = the union, over my living units, of the enemies inside THAT unit's own sensor
reach.** Nothing else counts — not ownership, not survey history, not sharing a region.

### The three omniscience leaks this DELETES

`GroundThreat.IsRegionDetected` (`:53-60`) currently returns true on **any** of three grounds. All three are deleted:

| Leak | Why it is omniscience |
|---|---|
| `regionIndex == viewerRegion` — *"I'm standing there — contact"* | A region is a **band of many regional hexes** (M2). Sharing a region does not mean anyone can *see*; the enemy may be a thousand km away. |
| `Regions[regionIndex].OwnerFactionID == viewerFactionId` — *"I hold it — I see my own ground"* | **Ownership is not eyesight.** You can own a region with **no units in it at all** and still see every enemy that walks through. |
| `IsRegionRevealedFor(...)` — *"scouted / surveyed to me"* | A survey is knowledge of **terrain**, and it is **permanent**. It is not live detection of **moving units** — so once scouted you would see enemies there forever. |

**Plus the un-gated base case:** `DetectedEnemyStrength` (`:77-88`) sums the viewer's **own** region with **no fog gate at
all** (*"own region — always in contact"*), then adds whole **neighbour regions**. Both the base case and the
neighbour-summing go — they are region-shaped, which **M2** deletes anyway.

### The replacement — already built, and the right shape

`GroundSensors.RadarReachHexes(body, unit)` (`:64`) reads the unit's **`GroundSensorAtb` components off its own
component store** (`TryGetComponentsByAttribute<GroundSensorAtb>`) and converts `Range_km / HexPitchKm` for **that
body**. So sensor reach is **per-unit, component-borne, cradle-to-grave** — researched, built, installed, and **shot off
blinds that unit** (the grave rung the space side already has).

**It is the IDENTICAL shape to the weapon gate**, which is what keeps it simple: `WeaponReaches` compares *weapon* range
against the continuous metre distance (`RealGapMetres`); the threat read compares *sensor* range against **the same
distance function**. One function each, same primitive, no new machinery. **Same slice as S6a's unbucketing.**

### ⭐ The consequence worth keeping — the UI becomes a picture of what the AI knows

**M7** already draws sensor range as a **WHITE** border and weapons range as **RED**. If the threat read is the union of
the white circles, then **the AI's knowledge is literally what is on the screen.** You can look at a planet and see
exactly what your opponent does and does not know. *The Visibility Gate satisfied by construction rather than bolted on.*

### ✅ SAFETY: this does NOT make the AI reckless — the valve already exists

Tightening the sensors means the AI will see nothing more often, and "no threat" must not read as "safe to charge" (that
is exactly the strategic-AI failure in **S1e**). **Already guarded:** `GroundTacticsContext.Blind` (`:72-73`) —
*"treat unknown as risk"* — multiplies the required odds ratio by `BlindCautionFactor = 1.5` (`:104,149`, FLAGGED), and
the commit rule is explicit at `:155`: *"no threat + scouted = free to press; **blind = don't**."* So the AI already
refuses to advance into ground it cannot see. **M17 makes that valve fire more often, which is correct.**

⚠ **One thing must be ported WITH it:** `Blind` is defined today as *"an adjacent **region** is un-scouted."* Under M17
it must mean **"there is ground I care about that none of my units can see."** The flag stays; its input changes — the
same port as the threat read.

### Note the distinction M17 does NOT touch

**The GROUND brain reads real strength and is honest** — `GroundThreat` sums actual `GroundUnit.Attack` of real
formations, and the code says so, taking a shot at its sibling: *"so own-vs-enemy is an apples-to-apples ratio, unlike
the space AI's."* **The blind AI of S1e is the STRATEGIC layer** (`ThreatAssessment` summing a sensor *margin*). Two
different problems: **S1e fixes what the empire can see; M17 fixes what a battalion can see.**

## M18 — THE ORDER SET: THREE battalion orders, two transport orders (developer, 2026-07-28)

**The whole planetary order vocabulary.** Arrived at by asking of every candidate: *is it a decision the AI can make,
and is it a DIFFERENT decision from one already here?* Anything that failed either test is deleted.

| Order | The decision it answers | Target |
|---|---|---|
| **MOVE** | *Go there* — and thereby **take** it (M16 derives ownership from occupation) and **fight** whoever contests it (M7 fires automatically at weapons range) | **(regional hex, mini hex)** |
| **RAZE** | *Destroy it rather than take it* | **(regional hex, mini hex)** — and because a mini tile holds **exactly one** building (`CityTile.BuildingInstanceId`), **the address IS the target** |
| **SET DOCTRINE** | *How to behave* while doing either (posture + ROE folded in) | a doctrine id |
| **EMBARK** / **LAND** | genuinely different — a **ship** is involved | existing troop-lift orders |

**HOLD is not an order — it is the DEFAULT.** An empty queue means "stand here," indefinitely. Nothing to issue, nothing
for the AI to re-issue, and no chain of timed holds to garrison a hex.

**No timed hold either** (developer: *"battalions arrive when they arrive"*). The one thing it could have expressed —
pausing to synchronise two battalions — is deliberately given up rather than paid for with a mechanic.

### The order enum goes 7 → 3. What is deleted, and by which ruling

| Today | Fate |
|---|---|
| `MoveToHex` | ✅ **KEEP** → becomes **MOVE**, generalised to the two-part address (M3/M4) |
| `MoveToRegion` | ⛔ **DELETE** — regions are a visual aid (M1/M2) |
| `HoldFor` | ⛔ **DELETE** — hold is the default, and no timed hold (M18) |
| `SetStance` + `SetEngagement` | 🔀 **FOLD 2 → 1** as **SET DOCTRINE**. ROE is *behaviour*, and behaviour is what a doctrine **is** — it becomes a field of the doctrine, matching the developer's own frame that *everything goes through the doctrines* |
| `DestroyInfrastructure` | ✅ **KEEP** → **RAZE**, re-targeted to the two-part address. **This also kills the hardcoded `(0,0)` target bug** the client passes today |
| `CaptureInfrastructure` | ⛔ **DELETE** — **M16 derives it.** Standing on a mini tile makes it yours; the roll-up flips the regional hex. The order was doing by hand what the invariant now does automatically |

### ⭐ THE ACID TEST — "can the AI do all of these intelligently?" — NOW YES, ACROSS THE BOARD

The six-order set failed this in three places. **Simplifying fixed the answer**, which is the clearest evidence the
simplification is right:

| Order | AI today |
|---|---|
| **MOVE** | ✅ issues it (`GroundTacticalBrain.cs:205`) and picks the destination from a **real** strength ratio. Needs the two-address port |
| **RAZE** | ✅ issues it (`ConquerResolver.cs:135`) — tasks an Offensive battalion at an enemy building. Needs the two-address target |
| **SET DOCTRINE** | ⚠ decides it well (`DecidePosture` — a pure, deterministic, testable function with real gauges: dry ⇒ never Offensive, 4:1 losses ⇒ retreat, a break-glass so a dying unit is never locked offensive) **but applies it by DIRECT CALL** (`TrySetStance`/`SetEngagementStance`, `:176,254`) while the player queues it — **the third One-Verb violation.** Fix = route it through the queue |
| **EMBARK / LAND** | ✅ issues both (`ConquerResolver` Rung 1.5 Load → the landing rung) |
| **HOLD** | ✅ trivially — it is the absence of an order |

**And the one serious gap CLOSED ITSELF.** The six-order audit found the AI *never* seizes — *"it can wreck a planet
but never take one."* Under **M16 + M18 there is no seize order**: the AI takes ground by **moving onto it**, which it
already does. The gap was an artefact of a mechanic that should not have existed.

### The one thing left to build, and it is small

**Not three orders' worth.** It is: **generalise MOVE and RAZE to the two-part address · fold ROE into doctrine · route
the AI's doctrine application through the order queue · delete four order types.** The (0,0) bug dies with the RAZE
port. One medium slice.

## M19 — WHAT THE AUTO-RESOLVER *IS* (developer, 2026-07-28) — 🔒 the definition every line of resolver code answers to

> **"The whole point of the auto resolver is to simulate ANY battle, in ANY condition, between ANY collection of
> components, in ANY environment."**
>
> **"IT IS 1 RESOLVER — IT SHOULD NOT MATTER"** whether the battle is in space or on a planet.
>
> **"If there is a line of code that doesn't reflect these ideas, DELETE THEM WITH THE UTMOST PREJUDICE."**

### The model, clause by clause

| # | Clause |
|---|---|
| **R-a** | **ONE resolver.** The space/planetary distinction does not exist at the resolver level. |
| **R-b** | **EXACTLY TWO TIERS** (clarified by the developer 2026-07-28: *"there is only Fleet/Battalions and Wings/formations"*). **Top tier: FLEET** (space) = **BATTALION** (ground). **Sub tier: WING** (space) = **FORMATION** (ground). Units sit in the sub tier. **No third tier** — *"Squadron" is retired as a tier name*; if a sub-fleet needs a spoken name it is a **Wing**. |
| **R-c** | **COMBAT DOCTRINE IS THE ONLY THING that changes how those forces act when combat begins.** Nothing else. |
| **R-d** | **Engagement begins ONLY when an opposing force falls within the range of an opposing force's WEAPONS** — specifically **the highest-range weapon on the unit of the group** (developer, 2026-07-28). ✅ **VERIFIED CORRECT AS BUILT:** `WithinWeaponRange` takes `Math.Max(reachA, reachB)`. |
| **R-e** | **On engagement the distance between the two forces is calculated**, and both are **simulated on either side of a simulated battlefield**. |
| **R-f** | **The wings/formations then manoeuvre according to their doctrine's instructions.** |
| **R-g** | **Components function differently at varying parameters, and that must be accurately simulated throughout THE ENTIRETY OF THE BATTLE** — not sampled once at the opening. |

### ✅ AUDIT — what already lines up (verified 2026-07-28; do NOT rebuild these)

- **R-d is BUILT AND LIVE.** `RequireWeaponRangeToEngage` gates the trigger on actual `WithinWeaponRange`, not the
  coarse `EngagementRange_m` bubble — and the code already calls this *"the developer's rule."* The client turns it on,
  together with `EnableClosingRange`, `RequireDetectionToEngage` and `RequireWeaponsReleaseToEngage`
  (`PulsarMainWindow.cs:98,105,113,114`). CI runs them **off** for byte-identity; a real game runs them **on**.
- **R-e is BUILT.** `StartEngagement` seeds the **real** distance; `FleetCombatStateDB.Separation_m` is the battlefield
  axis; `EnableClosingRange` closes the gap each step.
- **R-g is PARTLY built on the space side** — range-accuracy falloff, per-weapon range gates re-checked every step, and
  shields regenerating between volleys all vary with the state of the battle.
- **The kernel is already ONE.** Armour, dodge, shields, penetration and per-shot energy have a single definition
  (`Combat/CombatKernel.cs`) that both domains call.

### ⛔ AUDIT — what CONTRADICTS the ruling (the delete-with-prejudice list)

| # | Contradiction | Evidence |
|---|---|---|
| **X1** | **🔴 ROLE is a SECOND behavioural driver, and it is LIVE IN A REAL GAME.** `EnableGroundRoleManeuver` is set **true on the NORMAL New Game path** (`NewGameMenu.cs:567`, and DevTest `:981`), and `GroundRoleComposer.RoleMoveAway(role, …)` independently decides whether a unit backs off — *"fighters move differently than a line ship."* **R-c says doctrine is the ONLY thing.** | `GroundForcesProcessor.cs:72,795-803` |
| **X2** | **The tiers are UNNAMED and UNBOUNDED — this is the real defect, and it is the SAME in both domains.** Space: `FleetDB : TreeHierarchyDB` — an **arbitrary-depth** tree. Ground: `GroundFormation.ParentFormationId` — also **arbitrary depth**, with a cycle-guard walk (`:932-946`). So nesting *works everywhere* and is *capped nowhere*, and no tier carries a name: it is just parent/child. **R-b caps it at exactly two named tiers.** ⇒ **R5 is a CONSTRAIN-AND-NAME job, not a rebuild** — cheaper than first sized. *(There is no `Squadron` or `Wing` type; `Squadron` appears only as prose in a scenario helper.)* | `FleetDB.cs:8`; `GroundForcesDB.cs:392,923,932-946` |
| **X3** | **And the two domains bucket DIFFERENTLY** — the two-resolver problem in structural form. Space: a `FleetDB` **tree** (arbitrary nesting) plus **computed** `FleetRole` sorting that is explicitly *"not a per-role fleet"* (`FleetComposition.cs:31`) — i.e. roles are a transient classification, not persistent buckets. Ground: `GroundFormation` + sub-formations. **Neither is R-b's Fleet/Battalion → Squadron → Wing/Formation.** | `FleetRoleComposer.cs:16,29`; `GroundForcesDB.cs:378` |
| **X4** | **R-g is IMPOSSIBLE on the ground at the current tick.** Ground resolves **hourly**; a battle can finish inside one or two steps, so *"accurately simulated throughout the entirety of the battle"* cannot hold. **R-g therefore makes the shorter tick a CORRECTNESS requirement, not a balance choice.** | `GroundForcesProcessor.cs:29` |
| **X5** | **The FEED throws the design away** (the teardown finding). Of the resolver's 10 weapon variables, a ground weapon gets **2** from the designer; velocity/tracking/saturation are **hardcoded by a 4-value mode enum**, penetration + per-shot energy are **never written by the assembler**, heat is never set. **A railgun bolted to a tank stops being a railgun** — even though `SpaceWeaponGround` exists precisely so *"a weapon that's stronger in space is stronger on the ground."* | `GroundCombatant.cs:67-115`; `GroundUnitAssembly.ToGroundUnitDesign` |
| **X6** | **Alpha does not exist in EITHER domain.** Ground: the assembler never sets `PerShotEnergy`. Space: `BuildFireMix` **hard-zeroes** `Penetration` and `PerShotEnergy` when it buckets weapons. Since `BurstShotCount = DPS ÷ PerShotEnergy` (clamped ≥1), **every attack lands as exactly ONE shot**, so flat armour never bounces many small hits. | `CombatEngagement.cs:1188`; `CombatKernel.cs:266-273` |
| **X7** | **The battlefield is a geographic container, not an engagement.** Space: *"a star system IS the battlefield … every in-combat fleet fights in ONE multi-party engagement … (real weapon-range clustering — distinct simultaneous battles in one system — **is a v2 layer**)."* Ground: the same bug via the `byRegion` bucket. | `CombatEngagement.cs:264-267`; `GroundForcesProcessor.cs:268-278` |
| **⭐ X9** | **REFINED BY THE DEVELOPER 2026-07-28:** *"doctrine dictates how the forces will OPERATE; the range at which the battle COMMENCES is done at the range of the highest-range weapon on the unit of the group."* ⇒ **Commencement range = physics (max weapon reach) — and that is ✅ ALREADY CORRECT**: `WithinWeaponRange` uses `Math.Max(reachA, reachB)` (`:1244-1249`). **The violation is what happens AFTER:** `AdvanceClosing` picks a *controller* by highest `FleetManeuver(ships)` and closes toward `FleetDesiredRange(ships)` — **both take a list of SHIPS and take no doctrine argument** — so a doctrine saying *"close and brawl"* is kited anyway if its computed manoeuvre is lower. **Post-commencement range behaviour must be doctrine's.** | `CombatEngagement.cs:1028-1055,1186,1199` |
| **⭐ X10** | **The OPENING POSITIONS are a hardcoded geometric rule, not doctrine.** `SpreadNewlyContestedRegions`: the *holder* stays at its muster hex and every other faction is pushed away by **the holder's longest weapon range**. Deterministic, sensible — and **not something any doctrine can change.** | `GroundForcesProcessor.cs:289,571-580` |
| **⭐ X11** | **🔴 R-f IS NOT IMPLEMENTED — wings/formations have no position of their own to manoeuvre with.** Space carries **ONE `Separation_m` per FLEET**, and (only under the default-off `EnableGroupPlane`) **ONE `Anchor` per FLEET** — whose own comment says *"fleet is a single group sitting at Anchor. **Slice S3 replaces this with one point per role**."* **Per-wing positions are an unbuilt future slice behind a switched-off flag.** Ground has per-unit positions but manoeuvres them by **ROE/role**, not by a formation's doctrine. | `FleetCombatStateDB.cs:55,113,116` |
| **⭐ X12** | **THE KERNEL'S OWN COMMENT LISTS WHAT IS NOT SHARED** — *"the kernel is the single source of truth for the flat armour math on BOTH domains. **The rest of the planetary resolver (weapon profiles, the dodge/shield reconcile, the closing model on the hex board) adopts this kernel in slice 3b+.**"* ⚠ **PARTLY STALE — corrected here:** dodge and shield **DID** land (the ground resolver calls `CombatKernel.HitFraction` and `ShieldSoakFraction` directly). **Still true and unfixed: WEAPON PROFILES and THE CLOSING MODEL.** So the honest statement of R-a today is *"armour + dodge + shields are one; the feed and the closing are two."* | `CombatKernel.cs:24-30` |
| **⭐ X13** | **🔴 THE SAME NUMBER MEANS OPPOSITE THINGS IN THE TWO DOMAINS.** The kernel says so outright: *"the two domains **DISAGREE** on what reach 0 means: a ground melee weapon (reach 0) hits only at contact (gap 0), while a space beam (`Range_m` 0) is **UNBOUNDED**. Each caller layers its own reach-0 rule on top."* **Under R-a this is indefensible** — a weapon designed once and mounted in both places is melee-only on the ground and infinite-ranged in orbit. **Fix: ONE convention — `0` = no reach, and genuinely unbounded is `double.PositiveInfinity`** (which `WithinWeaponRange` already handles). | `CombatKernel.cs:130-140` |
| **⭐ X14** | **🔴 A SHIP'S FIGHTING STRENGTH IS FROZEN AT BUILD TIME AND NEVER REFRESHED BY ANY DAMAGE PATH.** `ShipCombatValueDB` is computed once in `ShipFactory` (`:144`) and cached; every reader uses `TryGetDataBlob(…) ? cached : Calculate(…)`. **It appears NOWHERE in the damage path and is NOT in `RecalcProcessor.TypeProcessorMap`** — unlike sensors, whose grave rung works. ⇒ **a ship that loses half its guns to missile/beam fire still fights at full firepower forever.** Two knock-ons: the `× comp.HealthPercent` term in every weapon's DPS is evaluated when all components are 100%, **so it is permanently ×1.0** (the "damaged gun shoots weaker" rung is real code that can never fire); and `FleetRoleComposer`/`FleetAssembly` classify a gutted hull as still-armed. **The most basic varying parameter — did this component survive — never varies.** | `ShipFactory.cs:144`; `CombatEngagement.cs:1629`; `AutoResolve.cs:124` |
| **⭐ X15** | **THE AUTO-RESOLVE HAS NO PER-SHIP DAMAGE STATE.** It accumulates a `DamageTakenPool` and converts it to **whole kills** (`kills = pool / EffToughness`), carrying the remainder. So partial damage exists at the **fleet** level but a *ship* is either at 100% or dead — there is no wounded ship. **R-g asks for components behaving differently at varying parameters; a ship's own damage state is the first parameter that should vary, and it does not.** | `CombatEngagement.cs:926-932` |
| **X8** | **Two different ARMOUR models.** Ground applies real per-source flat soak + a burst split; ships fold armour into Toughness and apply a proportional fraction. **The same designed armour means different things in different places.** | teardown scenario 6 |

### ✅ NOT violations — do NOT delete these under "utmost prejudice"

**R-c governs BEHAVIOUR (how forces act/manoeuvre). It does not forbid the world affecting outcomes** — indeed **R-g
requires it.** These are correct as built:

- **Terrain multipliers** (`GroundTerrain.TerrainAttackMult` / `LocomotionTerrainMult`) and **fortification cover** —
  environment shaping output is exactly *"components function differently at varying parameters."*
- **Ammo-dry silencing a unit** — physical state, not a behavioural driver.
- **`GroundFormationDoctrine.AttackMult` / `DamageTakenMult`** — these ARE the doctrine's own multipliers. Correct.
- **`PersonalityDB` / `CombatRisk`** — these bias the AI's *choice of what to do*, not how forces behave once
  committed. Doctrine still governs the fighting; personality governs which doctrine gets picked and whether to
  commit at all. *(Flagged rather than ruled — say if you want personality out of the loop entirely.)*

### ⭐ The resolution for X1 that keeps what is useful

**Do not delete role — delete role as an INDEPENDENT driver.** The space 2D layer already does it the ruling's way:
`FleetCombatStateDB` positions a sub-fleet by *"anchor + **doctrine** `RoleOffset`"* — **the doctrine places the roles.**
Ground does it backwards, with role deciding on its own. ⇒ **Make ground do what space's group-plane already does: the
doctrine addresses roles** ("artillery stands off, the line closes"), so doctrine remains the sole driver and
role becomes the *thing addressed* rather than a parallel switch.

---

## THE RESOLVER RETROFIT — the R-track (R1–R5)

**Cause established (M19's audit): the kernel is shared, but the FEED into it and the WRAP around it are duplicated —
and every contradiction above lives in that duplication.** The retrofit unifies the feed and the wrap and keeps the
kernel. **Two of the five slices are already ruled by the developer.**

| # | Slice | What it does | Fixes |
|---|---|---|---|
| **R1** | **UNIFY THE FEED** | ONE `design → WeaponProfile` converter both domains call. Ground stops synthesizing velocity/tracking/saturation from a mode enum; the assembler writes `Penetration`/`PerShotEnergy`; heat is set. Ground gains the two ship-side behaviours it lacks: **component health scaling damage** and **recoil-vs-chassis-mass degrading tracking**. | **X5, X6** |
| **R2** | **UNIFY THE BATTLEFIELD — per-engagement clustering** *(developer-ruled)* | A battle is a **cluster of forces in contact**, not a geographic container. *"100 separate fights in one region need 100 separate resolvers."* Replaces BOTH the star-system battlefield and the `byRegion` bucket. **Cheaper, not dearer:** n² inside small clusters beats N² across everything (100 fights of 4 ≈ 1,600 comparisons vs one fight of 400 ≈ 160,000). | **X7** |
| **R3** | **UNIFY ALLOCATION — real targeting, NO roll-over** *(developer-ruled)* | Pick a target by doctrine priority, fire, **excess over the target's remaining health is WASTED** (*"design a weapon with a bunch of alpha and you pay for it"*). Replaces ground's health-weighted **pool smear** (which never finishes a cripple) and the ship-side bucket aggregate. **Requires R1** — alpha must reach the resolver before wasting it can mean anything. **Do R2 first: clustering buys the budget targeting spends.** | **X6** + #18 |
| **R4** | **UNIFY THE TICK** | Ground moves to the committed **5 s** quantum on the same per-second basis space uses. **R-g makes this correctness, not balance.** Every per-tick damage term audited for time-scaling in the same change (finding **C2**). | **X4** |
| **R5** | **UNIFY THE ARMOUR MODEL + THE HIERARCHY** | One armour definition (ground's per-source flat soak is the more honest — it is what makes flat armour bounce). Plus **CAP the nesting at R-b's two named tiers** — **Fleet/Battalion** over **Wing/Formation** — in both domains. *Both already nest to arbitrary depth via a parent/child tree, so this CONSTRAINS and NAMES rather than builds.* **Why it matters beyond tidiness: an unbounded tree forces the AI to reason recursively about arbitrary depth; two named tiers means exactly two things to reason about — One Verb, Both Seats again.** And **doctrine addresses roles**, killing X1. | **X1, X2, X3, X8** |

**Sequencing, and it is not arbitrary:** **S1 (the ground battle log) still comes first** — retrofitting a resolver you
cannot watch is tuning blind (the Visibility Gate). Then **R1 → R2 → R3 → R4 → R5**: R1 makes the designer honest and
is independently testable; R2 makes R3 affordable; R4 is what makes R-g true; R5 is the structural tidy that needs the
rest landed first.

## What is ALREADY BUILT for these (verified in source, 2026-07-28 — so this is mostly a DELETE job)

The ruling is far closer to as-built than the older docs suggest. **Do not rebuild these:**

- **M3's two-part address is the live distance function.** `GroundMiniHex.ContinuousPosKm(globalQ, globalR, miniQ,
  miniR, coarsePitchKm, cityRadius)` (`:47-53`) = coarse-hex centre in km **+** mini-hex centre in km, and a second
  overload (`:61-66`) adds a real **sub-mini-hex offset** (`MiniOffX_km`/`MiniOffY_km`) — so the field is continuous
  **three levels deep** and a weapon range *shorter than a 37 km mini tile* can decide a fight.
- **M6/M7's proximity gate is built and ON for menu games.** `GroundForcesProcessor.WeaponReaches` (`:561-571`) routes
  through the shared `CombatKernel.WithinReach(range_m, GroundMiniHex.RealGapMetres(...))` behind
  `EnableMiniHexCombat` (`NewGameMenu.cs:580,985`).
- **The coarse-hex edge problem M5 was raised to solve is ALREADY SOLVED at the distance level** — the code says so
  itself (`GroundMiniHex.cs:70-71`): *"two units at a shared coarse-hex edge read a small gap **regardless of which
  coarse hex each is filed under**."* ⇒ **Transitional hexes are therefore NOT needed to make ranges work across a
  boundary. Their job is the CONNECTED MOVEMENT GRAPH and addressable standing ground** (mini patches are sized per
  coarse hex — `2·radius+1` = 13 across — so they are addressing islands that A* needs connective tissue between).
- **M2 is half-true already:** on the global path `RegionIndex` is **derived**, not stored truth — recomputed from the
  column via `PlanetGridFactory.RegionOfColumn` (`GroundForcesProcessor.cs:210`).

## ⛔ THE ONE REAL BLOCKER — and it is not the hexes

**Combat is bucketed by REGION before the range gate ever runs.** `GroundForcesProcessor` builds
`byRegion` keyed on `unit.RegionIndex` (`:268-278`) and calls `ResolveRegionCombat` **once per region bucket**
(`:295-304`). Two units in **different regions** never enter the same list, so they are **never compared at all**, no
matter how close they are in metres — the metre gate only ever runs *inside* a bucket.

⇒ **That bucket is what makes the planet not seamlessly connected. Deleting it is the ruling's load-bearing change.**

## What these rulings DELETE (code — scheduled as a build slice, not executed on the doc pass)

1. **The coarse region hop** — `GroundForcesDB.OrderMove` (`:756-770`) entirely: the **adjacency gate**
   (`Neighbors.Contains`, `:762` — one ring-hop at a time), the per-region `CrossingTimeSeconds ÷ speed` transit clock
   (`:768`), and `MovingToRegion` as a movement state (with its several "a coarse hop wins" precedence guards).
2. **The region-local hex march** (`HexPath`/`HexStepBaseSeconds`, the middle layer) — redundant once the global grid
   is the one layer.
3. **`byRegion` as the combat container** → proximity clustering on the continuous surface (**M6**).
4. **Region capture** → per-hex capture (**M8**), including the all-regions-uniformly-held planet-capture test
   (`TryCapturePlanet`, `:1056-1073`).
5. **The direct click-to-march call** that bypasses the order queue (**the One-Verb law**).
6. **Every order surface outside Force Management** (**M9**) — the planet view's March / raze / seize / hold / ROE
   buttons.
7. **Fortification's `SumAdjacent` region shielding** (**M10**).
8. **Per-region hazard declaration** → per-hex, from terrain + geography (**M11**).
9. **Four coordinate systems → the two of M3.**
