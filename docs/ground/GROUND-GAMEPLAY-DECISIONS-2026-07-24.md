# Ground Gameplay — the developer's DECISIONS (2026-07-24)

**Status:** 🔒 decisions recorded, build pending. Follows the `docs/earthfall/EARTHFALL-DECISIONS.md` pattern — the
developer's answers, captured verbatim-in-essence so a future session doesn't re-ask or guess. Produced from a
two-pass survey (8 domain readers + 8 cross-system connection readers, ~4M tokens, file:line throughout).

**Read with:** `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the board), `docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md`
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
| 22 | Is one health scalar enough? | **ALREADY DECIDED IN THE DOCS — the question should not have been asked.** `docs/combat/UNIFIED-RESOLVER-AND-BATTLE-STATS.md` Part B designs the `CasualtyTier` wound function (returns untouched / wounded / dead, works for a model squad *and* a monolith) plus a damage ledger totalled at battle end; `docs/ground/GROUND-UNIT-VARIABLES.md` designs the shared **Training** dial (build-time + scarce talent + credits → veterancy, militia→elite on one slider). **Both designed, neither built.** Treat as settled design awaiting build. |
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

## ⚠ The calibration problem ruling #23 creates (must be settled before it is built)

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

- **Ruling #1 (delete the prebuilt templates) is load-bearing and wide.** The three prebuilt units are today the *only*
  path to penetration and per-shot energy, they are what `GroundStartGarrison` raises, and they are referenced from the
  base mod and several tests. Deleting them requires ruling #2 to land first (the dials must exist on the weapon before
  their only carrier is removed), plus a garrison-composition replacement. **Order: #2 → #4 → #1.**
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
