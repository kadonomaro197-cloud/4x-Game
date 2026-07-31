# THE AUTO-RESOLVER — GROUND TRUTH

**As of 2026-07-29.** The single consolidated record of Pulsar's auto-resolve combat engine: **how it is actually
built**, **what it was designed to become**, **every locked ruling that bounds it**, **every build slice and its state**,
and **every open question**. Assembled from eight documents by a four-agent verbatim extraction, each finding
re-verified against source.

> **This file supersedes and replaces (all deleted):**
> `docs/combat/RESOLVER-DESIGN.md` · `docs/combat/AUTO-RESOLVER-TEARDOWN.md` ·
> `docs/combat/UNIFIED-RESOLVER-AND-BATTLE-STATS.md` · `docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md` ·
> `docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md` · `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` ·
> `docs/combat/RESOLVER-2D-JOINTS.md` · `docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md`
> **§20 maps every section of every one — nothing was lost.**

**Companion:** `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md` — the designer + the 17-pass resolver audit's findings.
**Canon:** `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` (M1–M19). **Live design docs kept separate:**
`docs/combat/COMBAT-DESIGN.md` (the eleven-system master), `WEAPONS-DESIGN.md`, `DETECTION-DESIGN.md`, `CARRIER-DESIGN.md`.

**⚠ THE LINE-NUMBER WARNING, inherited from the teardown and now doubly true:** *"Line numbers are as-of that date —
re-verify before editing."* Every file:line in this document was checked on **2026-07-29**; drift since the source docs
was **pervasive** (see §19). **The source is the gauge; this file is the map.**

**Severity key:** 🔴 **BLOCKER** · 🟠 **REAL** · 🟡 **DEBT** · 🔵 **NOTE** · ✅ **verified-good**.

> ## 🔒 THE GOVERNING MODEL — LD-30, THE ARENA (developer, 2026-07-29)
>
> **Read this before any section below.** The developer specified what a battle *is*, and it supersedes the
> frame-and-anchor geometry that slices S1/S2 were built on:
>
> **A battle is an ARENA, not a gap.** A circle at `0,0` whose **radius is set by the built weapon range** that
> opened the fight, with the combatants at opposite ends of a diameter. **The circle does not close.** Inside it the
> resolver is a **live simulation — units move at their own real speeds, "as though it was an RTS."** A joiner
> **expands the arena only if its range is larger**, never retroactively, and **enters at whatever state the battle
> is currently in.**
>
> **⇒ The arena is bounded by RANGE; the fight is resolved by MOVEMENT.** Today's engine conflates the two into one
> shrinking scalar (`Separation_m`), which is why the 2D plane is a **2D readout painted on a 1-D model** (**§13.9a**
> — the root-cause finding).
>
> **How to read the rest of this file:** every *as-built* description of the scalar-closing / frozen-frame model is
> **kept and labelled**, because you cannot fix a model you cannot see. Every *design intent* that contradicted LD-30
> has been **pruned or marked superseded** in place. Full ruling: **§5 LD-30**. What it requires: **§13.9b**.

---

## 0. HOW TO USE THIS DOCUMENT

| If you are… | Read |
|---|---|
| **Anything at all** | 🔒 **the LD-30 banner above, then §5 LD-30** — the arena model governs every section |
| New to the resolver | §1 the headline · §2 the two resolvers + the naming trap · §3 the tick order |
| Asking "why is the 2D plane wrong?" | **§13.9a the root cause** (a 2D readout on a 1-D model) · §13.9b what LD-30 requires |
| About to change combat | §5 the locked rulings · §6 the anatomy · §14 the build ledger · §19 the stale warning |
| Asking "does it handle X?" | **§8 the 14-row scenario matrix** — the fastest answer in the file |
| Working on ground units / squads | §9 the 6-man squad + Fix A/B/C · §12.4 the W-track |
| Working on the ship↔ground merge | §10 the divergence table · §11 the north star · §14.1 |
| Working on 2D / positions | §13 the group plane + §13.7/§13.8 the two joints |
| Working on ranges / distance | §12.1–§12.3 real distance · §5 LD-8 |
| Working on casualty reporting / stats | §15 · §9.3 (the `ModelCount` seam) |
| Looking for what's NOT built | §14 (the ledger) · §16 (open questions) · §17 (the parking lot — *decisions, not gaps*) |
| Asking "what does this touch?" | §18 the Prime-Directive connection map |
| **About to trust a claim from an older doc** | **§19 the stale-claim register — 14 rows that were live text somebody would have believed** |
| Looking for something from a deleted file | §20 the deletion map |

---

## 1. THE HEADLINE — the whole engine in seven sentences

1. **A ship or a ground unit is "whole-or-dead."** There is no half-wrecked-but-still-fighting ship; the per-component
   damage sim (`DamageComplex`) exists and is **parked** (root `CLAUDE.md` **L10**). Every survey agent confirmed this
   independently. **It is the single most important limitation in the engine.**
2. **There are TWO resolvers and ONE shared kernel.** The damage arithmetic is genuinely unified in
   `CombatKernel.cs`; everything *around* it — range, fog, retreat, stats, multi-weapon, firepower conservation — is
   still two harnesses. The accurate description is **"two resolvers sharing one damage kernel,"** not "one unified
   resolver."
3. **⚠ The word "AutoResolve" is a trap.** `AutoResolve.cs` is the *stripped-down instant path* — no dodge, no shields,
   no doctrine, no retreat. The interesting engine is `CombatEngagement.StepEngagementGroup`.
4. **Combat value is cached at build and never recalculated on damage** — a damaged-but-alive ship still rates at full
   value. This is whole-or-dead's direct consequence.
5. **The north star is stated and mostly built:** *"ground units work the same as ships as far as fleets or battalions
   are concerned"* — one shape, both domains, with the closing fight simulated tick by tick.
6. **The kernel is PURE and that purity is load-bearing** — it is what makes fast-forward == watch. There is exactly
   **one** break: `RangeBaseMiss` is a mutable public static.
7. 🔴 **The 2D group plane is built, unreachable, AND the wrong shape.** S0–S2 shipped behind `EnableGroupPlane`,
   which **no client code ever turns on** — and under **LD-30** it could not be turned on as-is anyway: the anchor is
   moved by *"however much the scalar gap just changed"* (`:1106`), so the plane is a **2D readout painted on a 1-D
   model**. Only the controller's anchor can move; everyone else freezes. **§13.9 / §13.9a.**

---

## 2. THE TWO RESOLVERS AND THE SHARED KERNEL

| Piece | File | What it is |
|---|---|---|
| **Space resolver (live)** | `Combat/CombatEngagement.cs` — `StepEngagementGroup` **`:640`** | The watchable, stepped, multi-fleet space battle. Dodge, shields, ammo, heat, point-defense, doctrine, retreat, fog. **This is "the auto-resolver" in practice.** |
| **Space resolver (instant)** | `Combat/AutoResolve.cs` — `Resolve` `:74` | A *second, much simpler* space resolver for off-screen fights. Pure strength-math. **No dodge, no evasion, no shields, no ammo, no heat, no doctrine, no retreat.** Reports casualties; **destroys nothing itself.** |
| **Ground resolver** | `GroundCombat/GroundForcesProcessor.cs` — `ResolveRegionCombat` **`:370`** | The planet-surface fight, per contested region, per hour. |
| **The shared kernel** | `Combat/CombatKernel.cs` (**314 lines**) | The domain-neutral salvo math — dodge curve, shield-soak fractions, flat armour. Pure functions. **Both** live resolvers call it, so dodge/shield/armour are defined **once**. |

> ⚠️ **THE NAMING TRAP (flagged twice by the surveys).** *"a reader expecting bucketing, retreat, or dodge inside
> `AutoResolve.cs` will be surprised — those are all in `CombatEngagement.cs`."*

**`CombatKernel.Combatant` — two corrections to the common assumption:** it is a **`sealed class`, not a struct**, and
**no kernel function actually takes a `Combatant`** — every function takes primitives. It is a caller-side data holder.
*(And per the designer audit, it has **zero production consumers** — see §16 O-1.)*

---

## 3. THE TICK ORDER — "two forces in range" → "who died"

### Space
1. **The clock is the prime mover** — `MasterTimePulse.SimulateTimeUntil`, normally one-hour jumps (`Ticklength` 3600 s).
2. **A trigger sweeps each star system every 5 seconds of game-time** — `BattleTriggerProcessor` (keyed to `StarInfoDB`)
   calls `CombatEngagement.Tick`.
3. **Tick pass 1 — form the fight.** Every fleet pair: hostile (`AreHostile`), both crewed, within the coarse
   **1-gigametre** bubble (`EngagementRange_m = 1e9`, `CombatEngagement.cs:40`), and — if the client's flags are on —
   within weapon range, detected, weapons-free. Pass ⇒ both fleets get a `FleetCombatStateDB`.
4. **Tick pass 2 — fight.** Every in-combat fleet in the system is gathered and `StepEngagementGroup` runs **one**
   exchange. ⚠ **In this version the whole star system is ONE battlefield** — range gated *joining*, not who fights
   whom once in.
5. **Inside one exchange:** snapshot → **ammo drain** (`:685-701`) → **heat build** (`:715-722`) → **damage** → **closing**
   → **resolution** (drop-out / retreat).
6. **Repeat** until fewer than two hostile sides remain.

### Ground
1. `GroundForcesProcessor` runs **once per hour** per planet (`RunFrequency = 1h`, `:29`).
2. `ProcessBody` does the surface turn in order: reveal map → bill upkeep → move units → bleed hazard attrition → run
   the ROE maneuver step → **group units by region**.
3. Any region with **2+ factions** fights: `ResolveRegionCombat` (`:370`) runs one salvo.
4. **Inside one salvo:** classify terrain → compute the defender's cover×fortification divisor → regen shields → for
   every attacker-vs-defender-faction pair, gate fire by range, scale by terrain and stance, route each shot through
   the kernel → accumulate damage from **pre-salvo health** (so the exchange is simultaneous) → apply.
5. After all regions: remove the dead (`:334`), flip captured regions and planets.

---

## 4. THE GAUGES — numbers that cannot rot

| Gauge | Today | Note |
|---|---|---|
| **Resolvers sharing the damage kernel** | **2 of 2** ✅ | The hardest part of unification is done. |
| **Harness concerns still divergent** | **10** | §10 — range · fog · multi-weapon · conservation · retreat · stats · hostility · pace · two space paths · perf. |
| **Scenario matrix: ground GAP/PARTIAL rows** | **5 of 14** *(was 7 — two closed since)* | §8. |
| **Ground resolver cost** | **O(units²)** | Space is O(buckets). §14 slice 5c is the fix, spec'd and ready. |
| **`EnableGroupPlane` client switches** | **0** | S0–S2 built, CI-gauged, **unreachable at runtime**. |
| **Ground firepower conservation (3+ sides)** | **BROKEN** | A unit applies its **full** pool to **each** enemy faction. |
| **Ground battle statistics** | **NONE** | No log, no event, no record of any kind. |
| **Kernel purity breaks** | **1** | `RangeBaseMiss`, a mutable public static. |

---

## 5. THE LOCKED RULINGS — constraints on all future work

**LD-1 · CANON supersession (2026-07-28).** Where any of the source docs treated the **region** as a combat container,
a movement layer, or the unit of capture, it is **superseded**: regions are a **visual aid**; combat proximity resolves
at the **mini-hex** level on continuous real distances; **capture is per-hex**. *(GROUND-GAMEPLAY-DECISIONS M2/M6/M8.)*

**LD-2 · Source of truth.** *"This doc's status markers are a **snapshot, not the gauge** — for whether any one dial is
live, read the source. CI cannot run the client, so nothing here is claimed to be verified at runtime; where a thing is
'built and wired,' that means it exists and compiles, not that it's been watched working."*

**LD-3 · The authenticity test for a dial.** *"A dial is authentic **iff** it writes one of these"* — the ten
`WeaponProfile` fields + the per-ship values (§6). *"Anything a dial wants to do must reduce to writing one of these —
or the surface must be extended."*

**LD-4 · The merge (DECIDED 2026-07-06).** *"Extract the shared salvo/matchup math onto a neutral COMBATANT view that
both a ship entity and a soldier present, route both through the ONE resolver, delete the ground duplicate."*

**LD-5 · The blocking gate (developer, 2026-07-08).** *"nothing else moves until space AND planetary combat both
resolve end-to-end on the shared kernel."*

**LD-6 · The DIRECTION of the merge (developer's call).** *"planetary ADOPTS the ship kernel… The **ship side stays
byte-identical** (it already IS the kernel)."*

**LD-7 · Determinism is a locked rule.** The kernel is pure — no entity mutation, no RNG, no clock read.
**fast-forward == watch.** *"Do not add RNG or a clock read here — it would break it."*

**LD-8 · Real distance (the developer's rule, the governing ruling on range).**
> *"A hex on Mars is a COMPLETELY different distance than a hex on Earth. HEXes are a visual indication of distances and
> locations. REGIONS are not important either — they're just a method of viewing hexes. **What matters is the actual
> numbers on the weapon/entity itself.**"*
**The one-way street:** the km stat is the truth; the hex reach is **DERIVED** from it for drawing, per body. **The
combat math never converts FROM hexes.** Accepted consequence, in the developer's words: *"the 2 battalions must occupy
the same exact hex in order for combat to start."*

**LD-9 · The type-triangle DISSOLVES** (developer chose "option 1: type edge lives in stats", 2026-07-08).
Armor▸Infantry▸Artillery is now emergent from weapon Nature × target armour/evasion. `AvgTriangleVs` retired.

**LD-10 · LOCKED NAMING (developer, 2026-07-08).**

| Level | Space | Planetary |
|---|---|---|
| The whole force (you select + order it) | **Fleet** | **Battalion** |
| A sub-group with its OWN doctrine | **Group** *(was "sub-fleet")* | **Formation** |
| The individuals | ships | soldiers |

⇒ **Fleet ▸ Groups ▸ ships** and **Battalion ▸ Formations ▸ soldiers**. ⚠ *Reconciliation still owed: today's
`GroundFormation` class sits at the **Battalion** level. Slice 5b either renames or nests.*

**LD-11 · Doctrine lives on the Group/Formation, not the whole force.** Each carries its own doctrine dictating its
behaviour in the fight — hang back, retreat out of range, rush, kite, hold. The closing model reads it, *"so the Titan's
Group kites while the zergling Formation rushes, in one battle."*

**LD-12 · "Compute one, distribute across N."** The resolver computes the outcome for a **single representative** —
one unit folding in its doctrine, the opposition, environment, statuses — then **distributes across the 50 / 100 /
10,000 identical units** in that bucket. *"It's why 'any number of soldiers, any battalion combination' stays
O(buckets), not O(units)."*

**LD-13 · Bucketing resolves scale.** 100 interchangeable zerglings collapse to **one** bucket; the unique Titan is a
**bucket of one** — exactly the ship model. *"Per-unit identity lives at the sub-formation level, not the individual, so
bucketing loses nothing the player cares about."*

**LD-14 · The acceptance test for "done"** is the **zergling/Titan example** (§11).

**LD-15 · Casualty model v1:** whole-ship removal, **no per-component damage (parked)**.

**LD-16 · Build discipline (non-negotiable).** *"combat is the ONLY green combat code in the tree, and there is no local
.NET SDK — CI (~30 min/push) is the only compile + correctness gauge. So every slice is: **additive/verified → push →
WAIT for both CI jobs green → build the next on top.** Never stack a behaviour change on an unproven base."* Each
behaviour change ships behind a **default-OFF flag**.

**LD-17 · Byte-identity for ships is the tripwire.** Slices must not move a single ship-combat number; the ship
fixtures prove it. **The ground behaviour change (triangle→matchup) is the one deliberate exception**, re-baselined with
the reason written down.

**LD-18 · THE STANDOFF-VS-BRAWL DECISION is what combat is FOR.**
> *"Do I build a fast, long-range artillery fleet that opens fire first and kites? Or a brawler that eats long-range
> fire on the way in to reach knife range where it dominates? **The test for any feature here: does it sharpen that
> decision, or is it fidelity the player can't act on?** If the latter, it goes in the parking lot."*

**LD-19 · This is a 4X, not an RTS.** *"The player never flies a ship. The player is the admiral: they write the
playbook (doctrine / rules of engagement), and the math fights the battle."* **Doctrine is the entire control surface.**

**LD-20 · The nine closing decisions (2026-06-27).** 1 **doctrine-only control** · 2 ~~range is a scalar per group; no
2D, no facing, no flanking~~ ⛔ **SUPERSEDED — by shipped code (§19 S-1) and decisively by LD-30** · 3 **determinism** · 4 **first shot makes the battle**
(proximity ≠ combat; two weapons-hold fleets in range = a tense standoff) · 5 **true weapon ranges, continuous closing**
(⚠ *"continuous closing" survives LD-30 only as **units moving continuously**, never as a shrinking global gap*)
· 6 **detail intel comes from scanners, not from closing** (so combat detection does **not** depend on the broken
`SignalQuality`) · 7 **ROE is the grown-up `FleetDoctrineDB`, NOT a parallel system** · 8 **the battle readout is the
agency surface — "lose agency, lose the player"** · 9 **post-battle form-up**.

**LD-21 · A phase isn't done until its gauge is green and you can SEE it.** *"You cannot believe Phase 1's kiting works
if you can't watch the range close."*

**LD-22 · Position lives at the GROUP, not the unit.** *"a 'group' is the only thing that has a position, and a group is
one point on the map no matter how many units are inside it."* **This is what preserves bucketing** — cost depends on
groups (dozens), never units (thousands).

**LD-23 · Keep the kernel pure and 1-D.** *"do all the 2D vector math in the **caller**, and hand the kernel a single
scalar distance. The kernel never learns geometry."*

**LD-24 · Combined battles are coupled BY DATA, never by shared geometry.** The Endor lock is **one boolean** — is the
guardian alive? *"That is what makes the film's real tension come out of pure deterministic math."*

**LD-25 · The theater owns the cadence (PINNED).** *"The `BattleTheater` owns the ground plane's fight-cadence for the
duration of a combined battle. It force-steps at a FIXED 5 s quantum — the same fixed quantum watched or
fast-forwarded — by riding the space trigger."*

**LD-26 · Three rules to guard forever** (combined-theater): ① **never pass a variable master delta into a group step**
· ② `TheaterGroundQuantum` **must evenly divide** `RunFrequency` (5 | 3600 → **720 exact**) · ③ **a combined battle is
NEVER routed through the instant `AutoResolve` shortcut.**

**LD-27 · The developer's ground-weapon calibration rule.** *"as long as a unit can provide power / ammo / hold the
actual weapon, it gets to use it."* The gates decide eligibility; pass them and the weapon fires at its real
characteristics.

**LD-28 · Cradle-to-grave is preserved.** *"a ground weapon is still a **component** — designed, researched, built from
materials, mounted, and lost. This design changes only how its range number is interpreted."*

**LD-29 · 🔒 THE 2D GROUP PLANE IS THE DEFAULT FOR ALL COMBAT (developer, 2026-07-29).** Asked whether the plane should
be switchable at runtime or parked, the developer's ruling: ***"it should be the default of all combat."*** So the
plane is **not** an opt-in experiment — it is how combat is meant to work, and the scalar single-gap model is the
fallback, not the norm. **This closes §16 O-5.**

> **What this does NOT license.** The ruling settles the *destination*, not the landing date. **`EnableGroupPlane`
> cannot be switched on as-is** — see **§13.9**, a live multi-fleet defect found while wiring this ruling. The engine
> default stays `false` (the house pattern: the engine ships every combat behaviour off so CI is byte-identical, and
> the **client** turns on what a real game should use — `PulsarMainWindow.cs:87-114`). The ruling is delivered by
> fixing §13.9, gauging it, and *then* adding the client line — not by flipping the flag.

**LD-30 · 🔒 THE ARENA MODEL — how a battle is shaped (developer, 2026-07-29).** The developer's specification of what
the 2D plane actually *is*, given in their own words across two messages. **This supersedes the frame-and-anchor
geometry of S1/S2 as the target model.**

1. **The trigger is the built weapon.** *"when a battle commences that means Unit A weapons range covers unit B BAM
   auto resolver starts."* **And it revolves around what the unit is BUILT with** — *"the 1000km is an example… it all
   revolves around what the unit is built with."*
2. **The battle has an AREA OF ENGAGEMENT — a circle.** Centre `0,0`; its **radius** is set by the weapon range that
   opened the fight; the two original combatants sit at opposite ends of a **diameter**.
3. 🔴 **THE CIRCLE DOES NOT CLOSE.** *"the circle doesnt close."* It is the **arena**, not a gap. It never shrinks.
4. 🔴 **Inside it, the resolver is a live SIMULATION.** *"the auto resolver is a simulation the moment combat starts
   they're moving as though it was an RTS."* **Closing is the UNITS moving at their real speeds — not a global gap
   number shrinking.** The player never drives it; the sim does.
5. **A joiner expands the arena ONLY if its range is larger.** *"the battle will ONLY expand if they're range is LARGER
   than Unit A. at which point they just expand the size of the plane to encompass them."* Otherwise the arena is
   untouched and the joiner simply fits inside it.
6. **Expansion is forward-only.** *"it doesnt change what has already occurred in the battle."* Existing units keep
   their positions and their damage; the circle grows outward around them.
7. **Scale is a non-issue.** *"then 50 other units can join, it wont matter either they'll just have a smaller range
   and fall into the the auto resolver or they're expand the range of the engagement and Fall into the auto
   resolver."*
8. **A joiner enters at the battle's CURRENT state.** *"they show up at whatever status that battle currently is at."*
   No rewind, no re-seed, no fresh start.

> **The one-line consequence:** the arena is bounded by **range**; the fight is resolved by **movement**. Those are two
> different things, and today's engine conflates them into one shrinking scalar.

---

## 6. THE ANATOMY — the input surface every dial must land on

### 6.1 The data flow

```
BUILD TIME ─ ShipCombatValueDB.Calculate(ship)              [ShipCombatValueDB.cs:281]
    Firepower  (J/s)  = Σ weapon.DamagePerSecond
    Toughness  (J)    = Σ component.HealthPercent × 100 kJ + armour.thickness × 100 kJ
    Evasion    (0..0.95) = EvasionCap × sizeFactor × agilityFactor     [CalculateEvasion :571]
    RoleWeight (1.0 armed / 0.25 utility)
    ShieldCapacity_J, ShieldRegen_Jps
    Weapons: List<WeaponProfile>                            ← THE per-weapon footprint

BATTLE ─ CombatEngagement.StepEngagementGroup(members, dt)   (every 5 s game-time)
  1. BuildFireMix(ships, separation)   → aggregate each fleet's fire BY CLASS (≤~6 buckets)
  2. Each fleet takes the COMBINED fire of hostile fleets; an attacker facing N enemies
     divides its fire 1/N (firepower is conserved)
  3. LandedFraction(fireMix, evasion, separation) = damage-weighted avg HitFraction
  4. ApplyShield  — the fleet's aggregate pool soaks the SOAKABLE part per Nature
  5. ApplyCasualties — bucket defenders; kill WHOLE ships, combatants first
```

### 6.2 The ten-field `WeaponProfile` — the entire authentic surface

| Field | Read by | Decides |
|---|---|---|
| **DamagePerSecond** | firepower + fire mix | how much hurt |
| **Velocity** | `HitFraction` velocityTerm | beam (≥1e7 hitscan) vs dodgeable |
| **Tracking** | `HitFraction` | how well it beats evasion |
| **Saturation** | `HitFraction` floor | floods dodge (flak) |
| **Range_m** | `BuildFireMix` gate + range term | reach / accuracy-at-distance. **0 = unbounded** |
| **Nature** (Kinetic/Energy/Explosive/Exotic) | `SoakFractionOf` | the **shield** matchup |
| **Delivery** (Beam/Bolt/Slug/Cloud/Guided/Blast) | `Class` (computed) | the **dodge** flavour |
| **Penetration** | `ArmourSoak` | cancels flat armour point-for-point (AP/sabot) |
| **PerShotEnergy** | `BurstShotCount` → `ArmourSoakBurst` | **alpha vs chip vs flat armour** |
| **HeatPerSecond** | fleet `HeatPool_kJ` throttle | sustained-fire heat |

**Per-ship:** Toughness · Evasion · RoleWeight · ShieldCapacity_J / Regen · doctrine Firepower/Toughness mult.

### 6.3 The kernel math (`CombatKernel.cs`)

- **`HitFraction(weapon, evasion, separation)`** `:171` — `velocityTerm = V/(V+1e6)` ·
  `trackingEff = max(velocityTerm, Tracking)` · `dodge = evasion × (1 − trackingEff)` — **you cannot dodge light** ·
  `saturationFloor = S/(S+50)` floored at `MinLandedFraction` · `hit = clamp(1 − dodge, floor, 1)`.
- **`LandedFraction`** `:198` — damage-weighted average over a mix.
- **`ShieldSoakFraction(nature)`** `:157` — **Kinetic 1.0 / Energy 0.5 / Explosive 0.75 / Exotic 0.0.**
  **Exotic bypasses shields entirely.**
- **`ResolveShield(pool, capacity, regen, salvoDamage, soakFraction, dt)`** `:228` → `(absorbed, newPool)`.
- **`ArmourSoak(armour, sourceDamage, penetration, natureFactor)`** `:269` —
  `effectiveArmour = armour − penetration`; ≤0 ⇒ full pass; `after = dmg − effArmour × 1.5 × natureFactor`, floored at
  `dmg × 0.1`.
- **`BurstShotCount`** `:290` — `round(DPS / PerShotEnergy)`, clamped `[1, 1000]`.
- **`ArmourSoakBurst`** `:306` — **the "many small hits bounce, one big hit punches through" identity.**
- **`WithinReach(reach, gap)`** `:140` / **`WeaponReaches(w, separation_m)`** `:149` — the shared range predicate.

> ⚠️ **The one purity break:** `RangeBaseMiss` (`CombatKernel.cs:57`) is a **mutable public static**, live-tuned. Two
> runs with different values give different results. Everything else is pure w.r.t. its arguments.

### 6.4 Every constant

| Constant | Value | Where |
|---|---|---|
| `VelocityReference_mps` | 1e6 | `CombatKernel.cs:38` |
| `SaturationReference` | 50 | `:42` |
| `MinLandedFraction` | 0.02 | `:46` |
| `FlightTimeReference_s` | 10 | `:51` |
| `RangeBaseMiss` | 0.9 | `:57` ⚠ mutable |
| Shield soak K/E/X/O | 1.0 / 0.5 / 0.75 / 0.0 | `:62-68` |
| `ArmourSoakPerPoint` | 1.5 | `:75` |
| `ArmourMinPassFraction` | 0.1 | `:78` |
| `BurstSoakMaxShots` | 1000 | `:283` |
| `ComponentHitPoints_J` · `ArmorHitPointsPerThickness_J` | 100 000 each | `ShipCombatValueDB.cs:81, :84` |
| `EvasionCap` | 0.95 | `:96` |
| `SizeReference_m3` · `AgilityReference_mps2` | 1000 · 5.0 | `:88, :92` |
| `UtilityRoleWeight` | 0.25 | `:76` |
| **`MissileLauncherFirepowerStub`** | **100 000** | `:38` ⚠ **missile firepower is not real** |
| `EngagementRange_m` | **1e9** ("a stub") | `CombatEngagement.cs:40` |
| `SalvoDamageScale` (space pace) | 0.1 | `:103` |
| `RetreatCasualtyThreshold` · `CollectivismRetreatSwing` | 0.5 · 0.4 | `:48, :55` |
| Retreat threshold formula | `0.5 + (collectivism − 0.5) × 2 × 0.4`, clamped `[0.05, 0.95]` | `:1665` |
| `ClosingSpeedScale_mps` · `InitialSeparationDefault_m` · `ManeuverBurnRate` | 1e6 · 1e6 · 5.0 | `:421, :428, :450` ⚠ **the scalar-closing dials — superseded in shape by LD-30** (the arena does not close; units move) |
| `SalvoScale` (ground pace) | **1.0** | `GroundForcesProcessor.cs:35` |
| Ground `RunFrequency` | **1 hour** | `:29` |
| `Ticklength` default · battle-trigger sweep | 3600 s · **5 s** | `MasterTimePulse.cs:83` · `BattleTriggerProcessor` |
| `CombatReactionStep` | 5 s | `MasterTimePulse.cs:95` |
| `AutoResolve` `RoundSeconds` · `MaxRounds` | 5 · 2000 | `AutoResolve.cs:19, :23` |
| `BattleLog.MaxEvents` | 250, **not saved** | `BattleLog.cs:73` |
| `BeamVelocityThreshold_mps` · `FlakSaturationThreshold` | 1e7 · 50 | `WeaponClassifier.cs:19, :23` |
| Ground bridge synth (velocity/tracking/saturation) | Melee 1/1.0/1 · Artillery 300/0/100 000 · Ballistic-Energy 1000/0/1 | `GroundCombatant.cs:72-83` |
| Ground weapon `Range_m` ladder (K1, base mod) | melee 0 · rifle 500 · autocannon 2000 · cannon 4000 · energy 20 000 | `installations.json` |
| `AttackPerDps` (space→ground calibration) | **1/2500 — FLAGGED, the developer tunes this one number** | `SpaceWeaponGround.cs:27` |
| Space-weapon ground bands | beam 4 · bolt 3 · flak 2 hexes | `:32-36` |
| `GroupPlane.Epsilon` · equidistant tie-break tol | 1e-9 · `1e-6 × max(d², bestD², 1)` | `GroupPlane.cs:34, :193` |
| Ship class-default ranges | flak 50 km · railgun 500 km · disruptor 400 km · missile 1000 km · **plasma borrows railgun's** | `ShipCombatValueDB.cs:52,62,68,73,435` |

---

## 7. THE PER-FILE FIELD MANUAL

**`ShipCombatValueDB.cs`** — *"turns a ship's real installed parts into the flat numbers combat reads — like stamping a
nameplate rating on a machine when you build it, then never re-deriving it under load."* Firepower per class: beam =
Energy/ChargePeriod · railgun = KE/shot × RoF · flak = dmg/pellet × (RoF × pellets) · **missile = a flat stub**.
Evasion = `0.95 × [1000/(1000+Volume)] × [accel/(5+accel)]`.
> ⚠️ **Critical gotcha every salvo agent flagged:** combat value is **cached at build and never recalculated on
> damage.** A damaged-but-alive ship still rates at *full* value. *"It's also the intended future seam: recalculating a
> damaged ship's value would land it in a different casualty bucket (a degraded tier), but that is **not built**."*

**`WeaponProfile.cs` + `WeaponClassifier.cs`** — the two-axis model. **Nature** meets the defence; **Delivery** meets
the dodge; **Class** is *derived* (`[JsonIgnore]`). **You set dials; the triangle corner emerges.**

**`CombatEngagement.cs`** (**2006 lines**) — `Tick` `:173` · **`BuildFireMix` `:1317`** — *"the aggregator that keeps
100-ship battles cheap"*, bucketing by `(Class, Nature, Delivery)` with damage-weighted averages. ⚠ **It hard-zeroes
`Penetration` and `PerShotEnergy`** — which is why flat-armour bounce does **not** work on ships. ·
**`ApplyCasualties` `:878`** — buckets defenders by `(toughnessMult, evasion, toughness, role)`,
`EffToughness = Toughness × mult / landed`, kills **whole ships as a count**. *"This is where '500 identical fighters
cost the same as 5' comes from."* · `ShouldRetreat` `:1635`.

**`GroundForcesProcessor.cs`** — `ResolveRegionCombat` `:370`; defender's `coverFort = CoverDefenseMult ×
Fortification.DefenseMult` divides **only the defender's** incoming; the **nested faction loop `:437-441`**;
`HexDist` `:550`; `ApplyEngagementManeuvers` `:732`; `WeaponReaches` `:561-569`.
**The ground first-strike (the surveys praised it):** directed per-attacker fire means a Range-3 artillery unit hits a
Range-1 infantry unit from 2 hexes and **gets no reply** until it closes — *and a unit fires while repositioning, so it
"shoots while backing away."*

**`GroundCombatant.cs`** — the ground→kernel bridge. `ToWeaponProfile(unit)` `:66` (collapsed) and
`ToWeaponProfile(unit, mount)` `:96` (per-weapon, W2).
> ⚠️ **Structural limits the bridge enforces:** velocity/tracking/saturation are synthesized from `DamageType` alone, so
> **two Ballistic units cannot differ in muzzle velocity or rate of fire**; **Exotic nature and Beam/Cloud/Guided
> delivery are unreachable from a ground unit**; heat is always 0.

**`GroundDamageMatrix.cs`** — its `ArmourSoak` overloads **all forward to `CombatKernel`**, and its two constants are
literally the kernel's. `Matchup` is now **readout/bombardment-only**.

**`AutoResolve.cs`** — sorts each side by RoleWeight; each round adds `Σfirepower × 5 s` to the other side's pool.
**Reports `DestroyedA/DestroyedB` but destroys nothing itself.** The live path does **not** use it.

**Doctrine / retreat / log blobs** — `FleetDoctrineDB` (`SpeedMult` **stored but never read by movement — a dead
dial**) · `FleetRetreatDB` (**a dead-end hook: no survivors actually move**) · `BattleLog` (runtime-only, 250-event
ring, **not saved**, per-fleet whole-ship counts). **Ground has no battle log at all.**

---

## 8. THE SCENARIO-CORRECTNESS MATRIX — the fastest answer in the file

Fourteen combat scenarios, each with a **space verdict** and a **ground verdict**: **HANDLED** / **PARTIAL** / **GAP**.
Produced by an eight-component read-only survey (2026-07-17), **re-verified and re-stamped 2026-07-29** — three rows
moved since the original because slices landed in between. Where the survey agents disagreed, the cell says so out
loud rather than smoothing it over.

| # | Scenario | Space | Ground | One-line note (line numbers restamped 2026-07-29) |
|---|----------|:-----:|:------:|---------------|
| 1 | **N bodies vs one HP lump** (the 6-man squad) | HANDLED\* | **GAP** | Space models N *ships*, each whole-or-dead (`ApplyCasualties` `:878`). Ground `GroundUnit` is **one `Health` scalar, no model count** (`GroundForcesDB.cs:53-55`). *(§9)* |
| 2 | **One unit, multiple different weapons** | HANDLED | ✅ **NOW HANDLED** — *was GAP* | **CHANGED by W2 (2026-07-21).** `GroundForcesProcessor.cs:479` loops `u.WeaponLoadout` and gates **each mount** on its own range; `GroundCombatant.ToWeaponProfile(unit, mount)` `:96` is the per-mount overload. A loadout-less unit still falls back to the single collapsed profile. |
| 3 | **Ranged vs melee closing** | **PARTIAL** | HANDLED | Ground: clean range first-strike + ROE march (`WeaponReaches` `:561`, `ApplyEngagementManeuvers` `:732`). Space: closing exists but `EnableClosingRange` **defaults OFF** in the engine (`:399`) → at gap 0 all weapons fire regardless of range. *(The client DOES turn it on — `PulsarMainWindow.cs:98`. CI does not.)* ⚠ **Under LD-30 the space side is wrong in shape, not just off:** closing must be units moving in an arena, not one gap shrinking. |
| 4 | **Evasion / dodge** (fast light vs heavy) | HANDLED | HANDLED | Shared kernel `HitFraction` `:171`; evasion is a per-target bucket key. A jump-pack is just a component raising Evasion. |
| 5 | **Saturation / area fire floors a dodge** | HANDLED | HANDLED | `saturationFloor` (`CombatKernel.cs:171-188`). *One agent marked this UNCERTAIN because it had not opened the kernel; the agents that did read it confirm HANDLED.* |
| 6 | **Flat armour bounces many small hits, not one big** | **GAP** | HANDLED | Ground applies real per-source flat soak + burst split. Ship folds armour into Toughness and applies a *proportional* fraction — and **`BuildFireMix` `:1317` hard-zeroes `PerShotEnergy`**, so `BurstShotCount` is always 1. A deliberate v1 deferral, **not** an accident. |
| 7 | **Shields deplete → collapse; nature matchup** | HANDLED | HANDLED\* | Both use `ResolveShield` + `ShieldSoakFraction` (K 1.0 / E 0.5 / X 0.75 / Exotic 0.0). *Ground caveat: **Exotic is unreachable** from a ground weapon (`NatureDeliveryFor` never emits it), so the shield-bypass corner cannot be fielded on the surface.* **Scope also differs:** space pools per **FLEET** (`FleetCombatStateDB.ShieldPool_J`); ground pools per **UNIT** (`GroundUnit.CurrentShield`). |
| 8 | **Ammo runs dry mid-fight** | HANDLED | **PARTIAL** — *was GAP* | Space drains `AmmoPool_kg` and silences dry weapons. Ground now burns a salvo through `WeaponSupply`, but **ammo is a WHOLE-UNIT gate in v1** — a dry magazine silences the *unit*, not the individual weapon. Per-weapon silence is deferred with the per-mount Penetration/PerShotEnergy split. |
| 9 | **Retreat + per-weapon / per-side casualty stats** | **PARTIAL** | **GAP** | Space: retreat works; per-*side* whole-ship counts in `BattleLog`; **no per-weapon, no wounded**. Ground: **no retreat stance at all** (only `HoldGround`/`CloseToEngage`/`StandOff`, `GroundForcesDB.cs:278-284`) and **no battle log at all**. *(§15)* |
| 10 | **Multi-party (3+ sides, reinforcement join)** | HANDLED | 🔴 **PARTIAL — the double-count bug is LIVE** | Space is conserved: fire divided `1/split` (`CombatEngagement.cs:745,747`). Ground is **not** conserved — nested faction loops at `:443/:445` rebuild the pool *inside* the loop (`:491` per-weapon, `:524` collapsed), so **a unit facing two enemy factions applies its FULL pool to each — 2× in a 3-way fight.** *(§13 Joint #1 is the pinned fix.)* |
| 11 | **Doctrine / stance swing; terrain / cover** | HANDLED | HANDLED | Mid-fight posture switch is a direct call past the engagement lock. Ground adds real terrain/cover/fortification divisors (`coverFort` divides **only the defender's** incoming). |
| 12 | **Fog of war / first-strike (blind enemy)** | HANDLED | **GAP** (detection) | Space: detection asymmetry — a seer shoots a blind target that cannot reply (`CanEngageTarget`), behind `RequireDetectionToEngage`. Ground has **no detection combat gate**: radar reveals the *map*, not who may shoot. Its first-strike is **range-based only**. *(§12 Slice 4 is the design.)* |
| 13 | **Wildly mismatched forces resolve cheaply** | HANDLED | **PARTIAL** | Space buckets by combat value → O(buckets); proven by `CombatPerformanceTests` (200 warships in ms) and `CombatBattleSims` B10 (1 dreadnought vs **1000** gnats ≈ 9 ms). Ground gets the right *outcome* but is **O(units²)** with **no perf gauge** — a large *symmetric* ground battle is far costlier than the space equivalent. *(W4 / slice 5c is the fix.)* |
| 14 | **Defensive posture as improvised armour** | HANDLED | HANDLED | `ToughnessMult` / `DamageTakenMult` + hardened plating all stack. Missing only the per-source bounce (row 6) on the space side. |

> **\*Honest disagreements, preserved.** Row 1 space is where the agents split — two called the fleet-of-N-ships case
> HANDLED (N discrete kills), one called it PARTIAL (no per-*ship* hull tracking). **Both describe the same fact:
> bodies are modeled, wounds within a body are not.** Row 5 had one UNCERTAIN from an agent that had not read the
> kernel, later resolved to HANDLED by the agents that had. These are recorded rather than reconciled because the
> disagreement itself is information.

**Read the matrix this way:** the *damage arithmetic* is uniformly good in both columns. Every GAP and PARTIAL is in
the **harness around it** — range, fog, retreat, stats, conservation, performance. That is exactly the finding §10
formalises.

---

## 9. THE SIX-MAN SQUAD — the marquee test, and the three fixes

This is the developer's stated acceptance case: **a 6-model Blood Angels squad.** Here is exactly what the engine
does with it today.

### 9.1 What the resolver does with it TODAY

A ground squad becomes **one `GroundUnit`** — a single serializable data object carrying:

- **one `Health` number** (`GroundForcesDB.cs:53-55`) — not six. There is **no `Count`, `Models`, or `Strength`
  field anywhere on the unit** (grepped: `ModelCount` returns zero files across the whole solution).
- **one `Attack`** (`:49`) — though W1 now *also* stores a per-weapon `WeaponLoadout` alongside it.
- **one `Range`** in hexes (`:75`) plus, since Slice 1b, a real `Range_m`.
- **one `DamageType`** — the source comment says "from its heaviest weapon" (`:93-94`).

When the squad takes fire, the resolver subtracts floating-point damage from that one `Health` pool and, when it
reaches zero, `forces.Units.RemoveAll(u => u.Health <= 0)` (`GroundForcesProcessor.cs:334`) deletes the whole unit.
**It is a single HP bar that vanishes at zero.**

### 9.2 What is lost versus modeling six bodies

1. 🔴 **Firepower does not degrade with casualties.** The attack pool uses **full `u.Attack` regardless of
   `u.Health`** (`:491`, `:524`). A squad at 1% health hits exactly as hard as at full, right up until it
   evaporates. Losing five of six bolters *should* cut output ~83%; today it cuts it **0%** until death.
   **This is the single cleanest fix in the engine.**
2. ✅ **Mixed loadout — NO LONGER LOST.** W2 (2026-07-21) closed this: a squad's lascannon, bolter, and chainsword
   now each gate on their own range. *(The original teardown listed this as loss #2; it is superseded.)*
3. 🔴 **No wounded / killed / untouched distinction.** Whole-or-dead means there is no "3 wounded, 2 dead, 1 fresh"
   state to report or act on.
4. 🟠 **No per-model targeting or per-model armour saves** — the flat-armour math treats the squad as one source and
   one target, not six independent saves.

### 9.3 The three fixes, cheapest first — and the ordering is a ruling, not a preference

| Fix | What | Size | Status |
|---|---|---|---|
| **Fix A** | **Scale outgoing fire by health.** Multiply the attack pool by `(u.Health / u.MaxHealth)` at `GroundForcesProcessor.cs:491` **and `:524`** (both paths, or they disagree). A battered squad fires proportionally less. **No new data model at all.** | **one line ×2** | ❌ not built |
| **Fix B** | **Add a model count.** `GroundUnit` gains `ModelCount` + per-model HP; `ResolveRegionCombat` applies casualties model-by-model. Buys "4 of 6 dead", morale-by-losses, and a real wounded/killed/untouched tally. | genuine slice | ❌ not built — **`ModelCount` does not exist anywhere in the solution** |
| **Fix C** | **A weapon list.** Emit one `WeaponProfile` per mounted weapon and range-gate each separately. | real slice | ✅ **BUILT as W1+W2, 2026-07-21** |

> **⚠ The ordering ruling — A → B → C — is now partly overtaken by events.** The teardown prescribed A→B→C, each
> independently shippable and byte-identical when its new data is absent. **C shipped first** (as the W-track), so
> the live order is **A → B**. That is not a problem — A and B remain independent of C — but do not read "A → B → C"
> as an unstarted sequence.

> **⚠ And a trap in front of Fix B:** feeding `ModelCount = 1` into the squad casualty formula makes a half-dead
> unit yield **zero** models lost → "untouched", while the health-band path calls the same unit "wounded". The two
> models genuinely disagree, so "one function for a squad AND a tank" is really **two with a deliberate seam**.
> Also `Math.Round(0.5)` **banker's-rounds to 0** — one downed model gives 0 dead / 1 wounded. Pick that on purpose.

---

## 10. SHIP vs GROUND — every place they diverge, and what unifying each costs

**The good news first:** the *hardest* part of unification — the actual damage arithmetic — is **already unified**.
Both resolvers call `HitFraction`, `ShieldSoakFraction`, `ArmourSoak`, `BurstShotCount` with **identical constants**.
What diverges is the **harness**, and every one of these is a bounded, independently-shippable slice.

| # | Divergence | Space | Ground | To unify |
|---|---|---|---|---|
| 1 | **Range model** ⚠ *LD-30: space is wrong in SHAPE — a single gap cannot express an arena with units moving inside it (§13.9a)* | Metric separation in metres, gated behind `EnableClosingRange` (engine default OFF → usually gap 0). | **Both, by flag.** `EnableMiniHexCombat` OFF → integer hex distance; ON → a **real metre gap** on the continuous mini-hex field (`GroundForcesProcessor.cs:421`, `:567`). **OFF in CI, ON for menu games.** | Mostly done — Slice 1 put `WeaponReaches`/`WithinReach` in the kernel. What remains is retiring the hex fields (§12 Slice 5). **Latent risk:** if a future slice sets ground `Position_m`, the *untested* kernel range term (`RangeBaseMiss = 0.9`) suddenly activates and changes **every** ground outcome. |
| 2 | **Fog / detection** | `CanEngageTarget` gates who may shoot whom via the sensor track table; a blind enemy takes fire without reply. Behind `RequireDetectionToEngage`. | **No detection combat gate at all.** Radar reveals the *map*; the fight uses raw faction difference + range. | Add a per-faction detection predicate in `ResolveRegionCombat`'s target loop mirroring `CanEngageTarget` — **in both the engage trigger and the interrupt-imminent check** (the two-places rule that already bit the space fog gate). §12 Slice 4. |
| 3 | **Multi-weapon** | `List<WeaponProfile>` per ship, all fire. | ✅ **CLOSED by W2.** | — |
| 4 | 🔴 **Firepower conservation (3+ sides)** | Conserved: `1/split` (`:745,747`) — but **equal**, not target-weighted. | **NOT conserved** — full pool to each enemy faction (`:443-445` + `:491`/`:524`). | Hoist the attacker's pool/reachable computation *above* the per-defender-faction loop and divide across all reachable enemies regardless of faction. **The pinned algorithm is §13 Joint #1** — residual-exact, target-weighted. |
| 5 | **Retreat** | Real threshold (`ShouldRetreat` `:1635`) + `FleetRetreatDB` — though survivors don't yet move. | **None** — units fight to annihilation unless manually ordered elsewhere. | A ground casualty-threshold break-off mirroring `ShouldRetreat` + a `GroundRetreatDB` echo + a **withdraw stance** (none of the three stances is one). **The single biggest new piece in the whole ledger.** |
| 6 | **Battle stats** | Per-fleet `BattleLog` events (whole-ship counts, runtime-only). | **Nothing** — units die and vanish silently. | §15. |
| 7 | **Hostility test** | `AreHostile` reads `DiplomacyDB` (pacts, war latch, stances). | Raw `FactionOwnerID` difference — **no diplomacy**, so a neutral or an ally sharing a hex is attacked. | Route ground faction-pairing through `AreHostile`. **Cheap and correctness-bearing.** |
| 8 | ⛔ **Pace dial / `dt`** | `SalvoDamageScale = 0.1` **and the pool is `× dt`** (`:759`). | `SalvoScale = 1.0` **and the pool is NOT `× dt`** (`:491` and `:524`). | **This is not cosmetic — it is a trap.** Ground damage is flat **per tick**; space is **per second**. **Shortening the combat tick would MULTIPLY ground damage by the number of extra ticks, not spread it across them.** The `rate × dt` conversion must come first, in the same slice, and **it re-baselines every existing ground combat gauge.** Both sites must change together. |
| 9 | **Two space paths** | The instant `AutoResolve` (no dodge/shields/doctrine) vs the live `StepEngagementGroup` (everything). | One path only. | If you want one rule set, `AutoResolve` should eventually call the same kernel/bucketing. Today they are **two different resolvers with different fidelity**. |
| 10 | **Performance model** | Buckets by combat value → O(buckets). | O(units²), no bucketing, **no perf gauge**. | Bucket identical `GroundUnit`s the way `ApplyCasualties` buckets ships. Full build plan in §12.4. |

---

## 11. THE NORTH STAR — the closing fight, one model, both domains

**The definitive statement of what all of this is FOR** (developer, 2026-07-08). Quoted essence:

> *"ground units work the same as ships as far as fleets or battalions are concerned."*

One shape, both domains, on three pillars:

**① Grouping = fleets, exactly.** A **formation/battalion IS a fleet.** You can raise 50–100 zerglings holding a
formation, an armour column with a Titan in another, each its own battalion (fleet-equivalent). **Join them → the
joined force moves at the pace of its SLOWEST unit but sees as far as its LONGEST sensor** — byte-for-byte the fleet
rule (`FleetCombat.WarpSpeedFloor`/`DeltaVFloor` = min, `SensorReach` = max). They march across the surface as one.

**② Trigger = the weapon range of the longest-ranged unit.** The Titan *sees* 5 hexes but *shoots* 3. Combat starts
when an enemy comes within **3** — *because of the Titan* — and the auto-resolver takes over. This is already the
space rule (`WithinWeaponRange` opens the fight at `Max(reachA, reachB)`; detection range ≫ weapon range).

**③ The fight = the resolver simulates the closing, tick by tick, over the battle's duration.** With a doctrine
(Titan kites / armour column screens the Titan / zerglings advance — as **sub-formations**), each sub-formation moves
at its **true speed** under its stance: the zerglings sprint, *and the resolver accounts for the 3-hex distance* — as
battle-time passes they close based on their move speed and who is left, **only landing damage once they reach their
range**, and the resolver keeps computing damage and losses continuously until it is decided. **You keep what
survives.**

> ### *"This IS how space combat should be and how planetary combat MUST be. This is the north star of combat and what you are building towards this whole time."*

> ✅ **Pillar ③ IS LD-30, stated a year earlier.** *"each sub-formation moves at its **true speed** under its stance …
> as battle-time passes they close based on their move speed"* is exactly *"they're moving as though it was an RTS."*
> **The north star and the arena model agree; it is the SPACE IMPLEMENTATION that diverged** by collapsing all that
> movement into one shrinking scalar. Ground already moves units continuously (`StepMiniToward`, `Speed_kmh × dt`,
> §12.3) — **so on this axis the ground side is closer to the north star than space is.**

**Why the kernel merge is the enabler:** the closing model and the damage math must be **one** implementation or the
two domains drift apart. The zergling/Titan example is the **acceptance test for "done."**

**Bucketing resolves the scale.** 100 interchangeable zerglings collapse to **one** combat-value bucket; the unique
Titan is a **bucket of one** — exactly the ship model (100 fighters + 1 dreadnought). Per-unit identity lives at the
**sub-formation** level (swarm / column / Titan), not the individual, so bucketing loses nothing the player cares
about.

### 11.1 The locked vocabulary — the same two-level shape, one label set per domain

| Level | Space | Planetary |
|-------|-------|-----------|
| The whole force (what you select and order) | **Fleet** | **Battalion** |
| A sub-group inside it with its OWN doctrine | **Group** *(was "sub-fleet")* | **Formation** |
| The individuals | units (ships) | units (space marines, clone troopers…) |

So: **Fleet ▸ Groups ▸ ships** and **Battalion ▸ Formations ▸ soldiers.** Selecting a Fleet/Battalion and opening its
info shows a **Group/Formation breakdown, split by units.**

> ⚠ **The naming reconciliation is still owed.** Today's `GroundFormation` class sits at the **Battalion** level —
> "the ground echo of a fleet." Under this vocabulary a Battalion is that top grouping and a Formation is the
> sub-group. Slice 5b either renames or nests so the ground hierarchy reads Battalion ▸ Formation ▸ units. **Pure
> orchestration above the kernel — no kernel change.**

**Doctrine lives on the Group/Formation, not the whole force.** Each carries its own doctrine dictating its behaviour
in the fight — hang back, retreat out of range, rush, kite, hold. Space: `FleetDoctrineDB` per Group. Ground:
`GroundFormationDoctrine` + `GroundEngagementStance` per Formation. Both already built.

### 11.2 "Compute one, distribute across N" — the developer's own statement of bucketing

> The resolver computes the outcome for a **single representative** — one unit, folding in its Group/Formation's
> doctrine, the opposing side, environmental effects, unit statuses, and any other modifier — and then
> **distributes that result across the 50 / 100 / 10,000 identical units** in that bucket.

This IS the bucketing model, stated as design intent. The expensive per-salvo math runs **once per (representative ×
doctrine × situation) bucket**, and the caller scales it by the unit count. It is why "any number of soldiers, any
battalion combination" stays O(buckets) — and **why the kernel is a set of pure per-combatant functions
(compute-one) with the count handled by the orchestration (distribute-across-N).** The refinement **confirms** the
kernel's shape; nothing below the orchestration changes.

---

## 12. THE GROUND CLOSING FIGHT — real distance, weapon banding, role parity

Two design tracks that turned out to be one: **real distance** (what a range *number means*) and the **W-track**
(how many range numbers a unit gets to have). Both exist to finish the §11 north star on the ground.

### 12.1 The governing rule — real stats are the truth, hexes are just the chart

> **The developer's rule, verbatim:** *"A hex on Mars is a COMPLETELY different distance than a hex on Earth. HEXes
> are a visual indication of distances and locations. REGIONS are not important either — they're just a method of
> viewing hexes. **What matters is the actual numbers on the weapon/entity itself.**"*

Think of a navigation chart with a scale bar. Your gun's range is 12 nautical miles — a real number. On a harbour
chart 12 miles fills the page; on an ocean chart it is a speck. **The range never changed, only how big it looks on
the paper.** Hexes are the scale bar and the scale differs per body. **That difference is the feature.**

**It is a one-way street:** the km stat is the truth; the hex reach is **derived** from it for drawing, per body.
**The combat math never converts FROM hexes.**

**The accepted consequence, in the developer's words:** *"the 2 battalions must occupy the same exact hex in order
for combat to start."* On Earth a rifle's few hundred metres is a rounding error inside a continental tile — so two
forces must share a tile before anyone fires. On a small moon a 3 km gun already reaches the next tile over.
**Nothing in the code differs between the two worlds — only `HexPitchKm` does.**

**Space already runs on this principle; the ground is the half that was still cheating.**

### 12.2 The metres ↔ hex translation layer

| Helper | What | Where |
|---|---|---|
| `HexPitchKm(region)` | **real km per hex on this body** = `√(2 × (Area_km2 ÷ HexCount) ÷ √3)` | `GroundRangeTools.cs:39-44` |
| mini pitch | **coarse ÷ 13** | `GroundMiniHex.cs:28-34` |
| `RealReachKm(hexRange, region)` | hexes → km (the readout) | `:48` |
| `HexesForKm` / `HexesForMetres` | km/m → hexes (put a real range on the chart) | `:65`, `:76` |
| `MetresForHexes(hexes, region)` | hexes → real metres (for the gate) | `:82` |
| **`RealRangeKmFor(hexRange, region)`** | **THE single seam** where "a weapon's real range" is defined | `:94` |
| `HexesFloorForMetres` · `DescribeReach` | two **undocumented K4 additions** | `:106`, `:117` |

> 🔴 **Two hazards on this layer.**
> **(a) The divide-by-nothing hole.** `HexPitchKm` returns **0.0** when a region has no hex geometry or area. Feed
> that in and `separation_m = 0`, which makes `WeaponReaches` return **true for every unit regardless of distance** —
> everyone suddenly in range. **The pure-hex compare has no such failure mode.** Any metre path needs a pitch-0 guard.
> **(b) The pitch cancels if you're not careful.** `ToWeaponProfile` derives a weapon's metric range as
> `unit.Range (hexes) × pitch`. If the gate then computes `separation_m = HexDist × (the same pitch)`, the pitch
> **cancels out** and the whole thing collapses back to `HexDist ≤ u.Range` — byte-identical *and with no per-body
> effect at all.* **You cannot get both "byte-identical" and a free per-body bonus. Pick one.** To make a hex on a
> big body genuinely differ, the weapon needs an **intrinsic metric range independent of hex count** — which is not
> byte-identical and re-baselines the ground gauges.
>
> 🔵 **And the scale figure is not authoritative.** "560 km per hex on Earth" appears in the worked examples of the
> superseded design doc; it is **illustrative only.** Earth's actual coarse pitch **awaits a CI measurement**. The
> examples' logic is unaffected — they only need "a hex is hundreds of km, a rifle is hundreds of metres."

### 12.3 Real-distance build state — what actually shipped, and under what name

| Slice | Design intent | State |
|---|---|---|
| **1a** metres↔hex helpers | the four conversions + the `RealRangeKmFor` seam | ✅ **BUILT** 2026-07-22 · gauge `GroundForcesTests.RealDistance_HexTranslation_BothWays_AndByBody` |
| **1b** real-metre stat fields | `GroundWeaponMount.Range_m`, `GroundUnit.Range_m`/`Speed_kmh`, `GroundUnitDesign.Range_m`, populated at the assembler + `RaiseUnit` | ✅ **BUILT** 2026-07-22, additive/byte-identical |
| **1c** client rings from real km | weapon ring + radar ring drawn per body | 🟠 **PARTLY BUILT.** The **weapon ring** landed. But the **Entity Assembler still shows hexes** (`ShipDesignWindow.cs:496` `Row("Range (hex)"…)`) and the **Force-Management "Reach" column is still hexes** (`FleetWindow.cs:1227,1239`). |
| **2** flip the resolver gate to real metres | `realGap_m > mount.Range_m` in both paths, behind a flag | ⚠ **THE BEHAVIOUR SHIPPED UNDER A DIFFERENT NAME.** **K1** put `Range_m` on `GroundWeaponAtb` (5-arg ctor; base mod: melee **0** · rifle **500** · autocannon **2000** · cannon **4000** · energy **20 000** m — verified). **K3** added the metre gate (`GroundForcesProcessor.cs:567`). Both behind **`EnableMiniHexCombat`** — **OFF in CI, ON for menu games** (`NewGameMenu.cs:580`, `:985`). **It landed under a different flag than the proposed `EnableGroundRealRange`, and it BYPASSED the doc's own `RealRangeKmFor` seam** (still `=> RealReachKm(...)` at `GroundRangeTools.cs:94`). |
| **3** real closing-over-time + hazard-during-crossing | `AdvanceGroundClosing` mirroring `AdvanceClosing`; hazards bite the crossers *during* the crossing, before the defender fires | 🟠 **PARTLY.** The **3-arg `HitFraction` with a real metre separation is already wired** (`:421-423`) — so the old claim *"the ground calls the 2-arg form, so separation defaults to 0"* is **refuted**. Sub-tile offsets exist (K2 `MiniOffX_km`/`MiniOffY_km`) — so *"a `GroundUnit` has no continuous position"* is **also refuted**. `ApplyEngagementManeuvers` `:732` now does **continuous metre stepping** (`StepMiniToward` `:848-855`, `Speed_kmh × dt/3600`), not one-hex-per-tick. **The hazard-during-crossing ADD is NOT built.** |
| **4** real-metre detection / fog gate | the ground echo of `RequireDetectionToEngage`, applied in **both** the engage trigger and the interrupt-imminent check | ❌ **NOT BUILT** |
| **5** retire the hex range fields | `GroundUnit.Range` / `GroundWeaponMount.RangeHexes` / `GroundUnitDesign.Range` / `DefaultRangeFor` → display-only; base-mod templates author real km | ❌ **NOT BUILT** |

> 🔴 **Five proposed gauges DO NOT EXIST** (verified: zero hits) — `RangeGate_FiresOnRealGap`,
> `GroundClosing_GapShrinksAtRealSpeed`, `GroundFog_UndetectedEnemyDoesNotEngage`, the method
> `AdvanceGroundClosing`, and the flag `EnableGroundRealRange`. **Do not cite any of them as existing.**

> 🟠 **A stale code comment to sweep:** `GroundWeaponMount.cs` (the `Range_m` docstring, ~`:21-27`) still says the
> field is *"ADDITIVE + UNREAD by the resolver (it still gates on `RangeHexes`)"* and that *"the range gate flips to
> this in Slice 2."* **Both false since K3** — `WeaponReaches(u, t, m.RangeHexes, m.Range_m, forces)` at `:479`
> passes it, and `:567` reads it when `EnableMiniHexCombat` is on. It also points at a doc this file replaces.

### 12.4 The W-track — per-weapon banding and role parity

**The problem in one picture.** A ship carries `List<WeaponProfile>` and fires **each weapon only once its own
`Range_m` reaches the shrinking gap** — a long beam opens first, short flak waits for the brawl. A ground unit
**summed** every weapon into one `Attack`, kept only the **longest** `Range`, and stamped the flavour of the
**heaviest** hitter — so a Space Marine squad's lascannon, bolter, and chainsword all reached the enemy at the same
instant. **The closing engine already existed; it just ran on a one-weapon-per-unit model.**

| Slice | What | Built | Gauge |
|---|---|---|---|
| **W1** multi-weapon ground loadout (data + assembler) | `GroundWeaponMount { Attack, RangeHexes, Range_m, Mode }` per mounted `GroundWeaponAtb`; unwired ⇒ byte-identical. *(Penetration/PerShotEnergy stayed **unit-level**, not per-mount — a later refinement.)* | ✅ 2026-07-21 | `GroundWeaponLoadoutTests` |
| **W2** the resolver fires per-weapon by range | Each weapon gates on its OWN range vs the gap — lascannon 5 / bolter 2 / chainsword contact. Extracted `FireWeaponAtReachable` `:406` as the shared per-target math. **Ammo is a WHOLE-UNIT gate in v1.** | ✅ 2026-07-21 | `GroundWeaponBandingTests` |
| **W3** sub-formation ROLE parity | `GroundRoleComposer`: `Screen / Line / Artillery / Support`; `ClassifyRole` [Attack→Support · Evasion→Screen · Range→Artillery · else Line]; `RoleMoveAway` [screen leads · line closes-then-holds · artillery kites · support keeps away]. **Roles are per UNIT** (matching the space `FleetRoleComposer`, which classifies per ship), behind default-OFF `EnableGroundRoleManeuver` (**ON for menu games**, `NewGameMenu.cs:567`,`:981`). The formation ROE still gates *whether* a unit auto-maneuvers; the ROLE decides the target band. | ✅ 2026-07-21 | `GroundRoleManeuverTests` |
| **W1b** space weapons compose on a ground unit | `SpaceWeaponGround`: `IsSpaceWeapon` / `Firepower_Jps` / `ModeFor` (laser·plasma·disruptor→Energy; railgun·flak→Ballistic) / `RangeHexesFor` (beam 4 · bolt 3 · flak 2) / `MountFor`. Fixes the verified #1 buildability gap: a ship-grade beam on a walker used to **draw reactor power and contribute ZERO ground Attack.** | ✅ 2026-07-21 | `GroundSpaceWeaponTests` |
| **W4** bucket the ground resolver | O(units²) → O(buckets); 5 000 gaunts in ms | ❌ **PENDING** (verified: zero occurrences of "bucket" in the file) | — |

> **⚠ The W1b calibration is ONE flagged number the developer owns:** `AttackPerDps = 1/2500`
> (`SpaceWeaponGround.cs:27`). It lands the base-mod weapons in the ground band — **laser ≈ 39** (vs rifle 40),
> **flak/plasma/disruptor ≈ 120**, **railgun ≈ 400** — balanced by construction against the ship weapon triangle (the
> dps spread is only ~10×), no one-shots.
>
> **The developer's locked calibration rule:** *"as long as a unit can provide power / ammo / hold the actual weapon,
> it gets to use it."* **The gates decide eligibility; if you pass, the weapon fires at its real characteristics.**

> 🔵 **W1b has grown a sibling the track never mentions:** `SpaceWeaponGround.RealRange_m` (`:113-124`) derives a
> space weapon's **true metric ground reach** from the ship range constants. So the hex bands above are now only
> *the display ruler* (kept for the hex fallback). **Both live.**

**W4's build plan already exists** — the bucket key is
`(FactionId, HexQ, HexR, UnitType, Attack, Defense, Evasion, Shield, DamageType, Range, doctrineAttackMult,
doctrineDamageTakenMult)` plus a coarse **health band** (`HealthBandSize`, a tunable — so a lightly- and a
heavily-damaged zergling split into two buckets). It is a **LOOP restructure, not a math change**: build buckets →
reach per bucket-**pair** → run the kernel chain **once per defender bucket** and multiply by counts → distribute
uniform damage inside the bucket. Gauges: `GroundBucketPerfTests` (5 000 units in ms) **plus an equivalence test**
(a bucketed resolve matches the per-unit resolve unit-for-unit for the interchangeable case). **The per-hex position
in the key is why a *spread-out* army compresses less than a *stacked* one — which is realistic.**

### 12.5 The W-track's six invariants (they govern every ground combat slice)

1. **Ship byte-identity** — the track never touches the ship path; ship fixtures are the tripwire.
2. **Existing-ground byte-identity** — a one-weapon unit, a garrison, a DevTools unit, an old save resolves exactly
   as today.
3. **Save-safe** — every new field `[JsonProperty]` **and** deep-copied. Sensors: `MidCampaignSaveLoadTests`,
   `SaveLoadDesignRoundTripTests`.
4. **Determinism** — the kernel stays pure and RNG-free (fast-forward == watch).
5. **One slice per push, both CI jobs green before the next.** No stacking.
6. 🔴 **No `*Atb` constructor changes** — the JSON binder is **exact-arity**. Build the loadout at the assembler and
   store it on the design/unit. *(Root `CLAUDE.md` **L13**.)*

---

## 13. THE 2D GROUP PLANE AND THE TWO JOINTS

> ### ⚠ READ §13 AS AS-BUILT, NOT AS TARGET
> **LD-30 supersedes this section's geometry.** What follows is the design S0–S2 were actually built from — kept
> because it is what the code does today and you cannot fix what you cannot see. **The target is the arena
> (§5 LD-30); the reason this design cannot reach it is §13.9a.** Each subsection is marked where it diverges.
> **Still valid and carried forward:** the group-not-unit position choice (§13.2), the directed range rule (§13.3),
> the data-only coupling for combined battles (§13.4), and both pinned joints (§13.7/§13.8).

### 13.1 The one-line version

Give every **group** in a battle a single position on an invisible 2D map; let doctrine decide where each group sits
(front / flank / hang-back); measure the distance between groups to decide who can shoot whom. **The player never
sees the map** — they set doctrine and read the result. One math module runs it for both space fleets and ground
formations, and a battle spanning both (a fleet in orbit *and* troops on the moon — Endor) is two of these maps
stepped together, **wired to each other by data, never by shared geometry.**

**Why it exists — the developer named the gap: *"the 2D issue is the biggest issue."*** Space tracks range as **one
number for the whole fight** — there is no such thing as a flank or a rear; every ship on your side is exactly as far
from the enemy as every other. Ground is the opposite extreme: **every soldier** has its own coordinate compared
against every enemy soldier — honest, but O(units²) with no bucketing. **Neither is what a real battle looks like.**
A real battle has a **shape**: a line holding the centre, a fast wing swinging wide, artillery shelling from max
range while nothing can answer. *"There is nothing to see as far as the player is concerned. All this is being done
with math in the background."*

### 13.2 The one big decision — position lives at the GROUP, not the unit

> **L-1, the load-bearing choice:** *"a 'group' is the only thing that has a position, and a group is one point no
> matter how many units are inside it."*

- **Space:** a group is a **sub-fleet** — Screen / Line / Artillery / Support. A wing of 500 identical X-wings is
  **one group, one point.**
- **Ground:** a group is a **formation**. A 6-man Blood Angels squad is **one group, one point** (with two weapons
  inside it: the bolter that shoots at range, the chainsword that only reaches at distance zero).
- A lone capital ship, the Death Star, a bunker, a planetary shield generator: each its own one-unit group. The
  fixed ones are **combatants nailed to the deck** — locked bearing, anchor that never moves.

**Because position never touches an individual unit, bucketing survives completely intact.** Cost is O(**groups**²),
never O(units²) — dozens even at Endor scale.

### 13.3 The model, the two rules, and the three invariants

**The frozen frame — ⛔ SUPERSEDED AS TARGET by LD-30; this is what S1 BUILT.** Seed the sheet **once** at battle
start from the fleets' real 3D positions; find the centre; pick a deterministic axis pair; flatten. The projection is
frozen and never recomputed; a latecomer is placed using the *same stored* axes.

> ⛔ **Three ways this contradicts LD-30, and they are why S1/S2 cannot deliver it:**
> ① the frame is seeded from **real 3D positions**, not sized as a **circle whose radius is the built weapon range**;
> ② there is **no radius at all**, so a joiner *"expands the size of the plane"* has nothing to expand — it just
> copies the frozen frame (`:614`);
> ③ *"the whole thing collapses back to today's 1-D tug-of-war"* was written as the **safety property**. Under LD-30
> it is the **defect** — the arena must never collapse to a tug-of-war, because the fight is units moving inside it.
>
> **What survives:** freezing-so-it-cannot-drift is the right instinct and LD-30 keeps it — *"it doesnt change what
> has already occurred in the battle."* The thing frozen changes from an axis pair to **already-placed units and
> already-dealt damage**.

**Range rule — ✅ COMPATIBLE with LD-30, carry it forward.** For each enemy group pair A→B, measure the 2D distance `d`, then hand **that same `d`** to the
*unchanged* firing code as `separation_m`. Each weapon inside A skips if its own range is less than `d`. So the
missile (1000 km) > railgun (500 km) > flak (50 km) > melee (~0) **layering falls out per group-pair.** The gate is
**directed** — a long-range group can shoot a short-range group **that cannot shoot back yet.** *That is exactly
"those with the farthest range shoot first."*

**Bearing rule — 🔵 COMPATIBLE but UNBUILT (S3).** LD-30 says nothing about roles, and nothing here contradicts it:
an arena still needs to decide *where inside it* a group stands. Carry this table forward as the placement rule.

**`RoleGeometry(role) → (bearing, standoff)`:**

| Role | Bearing | Behaviour |
|---|---|---|
| **Line / Front** | 0° | pushed forward to brawl |
| **Screen / Flank** | ±90° | swings wide, reaches the enemy's flank and rear |
| **Artillery / Standoff** | 0° | held back at its own **longest** range — it kites |
| **Rear Guard / Support** | 180° | behind its own anchor; reachable only by a flanker or a very long gun |
| **Fixed installation** | locked | anchor never moves |

> *"**No flank is ever discovered by clever maneuver — it is assigned by doctrine.**"* An accepted limitation, written
> down on purpose: the plane is unseen and micro is forbidden, so flanking is a doctrine **fiction** that produces
> the right result, not emergent tactics.

**The three invariants — ✅ ALL THREE SURVIVE LD-30:** ① **determinism** (no RNG, no clock, no iteration-order
dependence) · ② **default-off / byte-identity** · ③ 🔴 **keep the kernel pure and 1-D — do the 2D math in the CALLER
and hand the kernel one scalar.**

> ✅ **Invariant ③ is not in tension with the arena.** The kernel only ever needs *one distance between two things*.
> The arena changes **who computes that distance and how it moves** (the caller, from real unit positions), not what
> the kernel receives. **The damage math needs no change for LD-30** — which is the single biggest reason the arena
> is affordable.

### 13.4 Combined battles — Endor, done with data not geometry

A **`BattleTheater`** holds several planes (one space + one ground per contested surface), stepped in the same tick,
**coupled by data only.** Two kinds:

- **① Objective link — the Endor lock.** The Death Star is a space group carrying a marker: *"I take zero damage
  while my guardian (the shield generator) is alive."* Meanwhile the **ground** battle is resolved on the forest-moon
  plane by the *identical* resolver. When the ground team kills the generator, the marker clears and on the **next**
  space step the Death Star starts dying. **The two planes never share a distance — they share one boolean.**
- **② Bombardment edge.** An orbiting group targets ground groups through a coupling edge with a **large fixed
  separation** (orbital altitude), so only long-range weapons reach down. The drop is ground reinforcement groups
  *appearing* on the ground plane (a join).

### 13.5 The build ledger + the escape hatch

| Slice | What | State |
|---|---|---|
| **S0** | `GroupPlane.cs` pure static (project / offset / distance); nothing calls it | ✅ **BUILT** · `GroupPlaneTests` (10 tests) |
| **S1** | anchors in space — Anchor/Frame/GroupPositions on `FleetCombatStateDB` `:83-118`, seeded at engagement start (`CombatEngagement.cs:520`), copied to joiners (`:614`), `AdvanceClosing` moves anchors in 2D (`:1062`,`:1077`) | ✅ **BUILT** · `EfGroupPlaneAnchorTests` — ⛔ **superseded in SHAPE by LD-30**: the anchor is a follower of the scalar (`:1106`), so only the controller's can move (§13.9a) |
| **S2** | the 2D range gate — `SeparationOf` `:1003` / `WithinWeaponRange` `:1268` read the 2D pair-distance | ✅ **BUILT** · `EfGroupPlaneRangeGateTests` — 🔴 **two-fleet-only, see §13.9** |
| **S3** | role geometry — the `RoleGeometry` table | ❌ **NOT BUILT** (verified absent) · 🔵 **compatible with LD-30** — an arena still needs a placement rule |
| **S4** | ground onto the plane — **a DELIBERATE re-baseline**, not byte-identical | ❌ **NOT BUILT** (`EnableGroundGroupPlane` absent) |
| **S5** | combined theater — `BattleTheater` + `GuardedByDB` + bombardment edges | ❌ **NOT BUILT** (both types absent) · **gated on Joint #2** ✅ pinned |
| **S6** | multi-party — reinforcement join + FFA + cross-plane conservation | ❌ **NOT BUILT** (no production `AllocateFire`) · **gated on Joint #1** ✅ pinned |

> 🔴 **`EnableGroupPlane` HAS NO CLIENT SWITCH.** Verified 2026-07-29: the flag is assigned nowhere outside
> `CombatEngagement.cs:413` (its `= false` declaration) and the tests. Contrast `EnableClosingRange`, which
> `PulsarMainWindow.cs:98` turns on, and `EnableMiniHexCombat`, which `NewGameMenu.cs:580` turns on.
> **⇒ S0–S2 are built, CI-gauged, and UNREACHABLE AT RUNTIME.** *(Layer-3 item in `docs/TESTING-TRACKER.md`.)*

> 🟠 **S3 has SHRUNK.** Its stated scope included *"give `FleetRoleComposer.FormRoleSubFleets` its missing game-loop
> caller."* **That caller now exists** — `NPCDecisionProcessor.cs:819`, behind default-off `EnableOrderEmission`. So
> **S3 reduces to the `RoleGeometry` table alone.** *(And `Fleets/CLAUDE.md:18` still says "Still UNWIRED — no
> game-loop caller" — itself stale.)*

**The escape hatch, kept on purpose (§12 of the design):** since the plane is never rendered, all it ever produces is
a **table of distances between groups.** If full 2D proves to be more machinery than the problem needs, **degrade
polar to the Station-Lattice per-pair distance matrix** — same range rule, same coupling, same bucketing, less trig.
> ***"Polar is the spine; the per-pair matrix is the pressure-relief valve."***

**What was rejected and why it matters:** *Range-Band + Flank-Slot* added Left/Center/Right lanes, but the lanes
added distance **symmetrically**, so a "flanker" was just farther away and therefore **less** effective — the
opposite of a flank advantage. **The lesson is baked into the polar bearing rule: a flank must produce an
*asymmetric* effect (reduced enemy return-fire or reduced target soak), never just a bigger number.**

### 13.6 The eight known weaknesses (kept so nobody re-discovers them the hard way)

1. The 3D→2D projection is **lossy**; bearings are a doctrine fiction, not emergent maneuver.
2. Determinism leans on the **fixed hotloop cadence** — the range gate makes closing nonlinear, so a variable
   time-step anywhere in a group step breaks fast-forward == watch.
3. Ground's native cadence is 1 h; a combined 5 s + 1 h battle needs force-stepping (**Joint #2**, now pinned).
4. Objective-link invulnerability is **binary** — no partial shield. Matches the Endor fiction but is blunt.
5. 🟠 **"Nearest enemy" can oscillate with 3+ equidistant sides.** As-built has the lowest-id tie-break
   (`GroupPlane.cs:193`, tolerance `1e-6 × max(d², bestD², 1)`) but **no hysteresis. Still open.**
6. The battle frame **must be persisted and copied to joiners** — a real save/load + reinforcement correctness trap.
7. S4 knowingly changes ground outcomes — **not byte-identical, must be a signed-off re-baseline.**
8. 🟠 **Standoff radii and role-bearing constants are unchosen** — a live-tuning question CI cannot answer. S3 unbuilt.
9. 🔴 **THE FROZEN-ANCHOR DEFECT (§13.9) — not on the original list, found 2026-07-29.** Only the controller's anchor
   moves, so with 3+ fleets a pair where neither is the controller has a permanently frozen gap. **The design's own
   weakness list missed this because every gauge it was written against had two fleets.** It blocks LD-29.
10. 🔴 **THE ROOT CAUSE BENEATH #9 (§13.9a):** the anchor is driven by the scalar gap delta, so the "2D plane" is a
    **2D readout on a 1-D model.** Weaknesses #1 (lossy projection) and #7 (S4 re-baseline) are also reframed by
    LD-30 — under the arena, position is the source of truth, so there is no projection to be lossy about and the
    ground side stops being a special case.

### 13.7 JOINT #1 — conserved fire-allocation (🔒 pinned, gates S6)

> *"The firemain manifold. One pump feeds several outlets. If you open every outlet to full pressure you're
> pretending the pump moves more water than it does."*

**The trap:** the range rule is **directed and per-pair**, so if each pair gets A's **full** pool, a group facing
three enemies deals `3 × P_A` — it shoots as if it had three times the guns.

**Three requirements: conserve exactly · weight by target priority · be deterministic.**

**The algorithm — residual-exact, target-weighted:**

```
AllocateFire(P_A, T_A, weight):
    if T_A is empty: return {}          // no carry — the pool is regenerated from BuildFireMix next step

    order = sort(T_A) by stable GroupId ascending     // (1) DETERMINISTIC ORDER
    W = Σ weight(B) over order                        // > 0 by construction
    running = 0
    for i in [0 .. count-2]:                          // every target EXCEPT the last
        d = P_A * (weight(order[i]) / W); dealt[order[i]] = d; running += d
    dealt[order[last]] = P_A - running                // THE RESIDUAL
```

**Why the residual guarantees exact conservation.** `Σ dealt = running + (P_A − running) = P_A` in **one
subtraction, with no accumulated floating-point drift.** We never sum `count` separately-rounded shares and hope they
land on `P_A` — we compute `count−1` and **back out the last.** A group therefore **can never deal more than its
pool** (the double-count trap is *structurally impossible*) nor mysteriously less. The residual owner is
deterministic, so watch and fast-forward assign the same residual to the same target.

**Splitting the fire MIX, not just the scalar.** Apply the same per-target share `s_B = dealt(A→B) / P_A` to scale
every weapon profile — exactly what `AddScaledFire` already does, but with `s_B` = the weighted share instead of
`1.0 / split`. **The scalar total is conserved exactly; the per-weapon mix to float epsilon per bucket.**
Nature/Delivery/velocity/tracking ride along unchanged, so the whole downstream PD → shield-nature → armour-nature →
dodge-bucket pipeline is untouched.

**The default weight (the tunable layer):**
`weight(A→B) = threatPriority(B) × hittability(A→B)`, where `threatPriority = max(TotalDamage(fireMix_B),
MinTargetWeight)` (*"shoot the guns that can hurt you first"*) and `hittability = max(LandedFraction(fire_A,
evasion_B, sep), MinHitWeight)` (*"concentrate where you actually connect"*). Both floored so nothing is
un-targetable.

**The byte-identity floor (why S6 flag-off is safe):** when both terms are uniform across `T_A`, the weighted share
collapses to `1/count` — **exactly today's space `1.0 / split`.**

> ⚠ **FLAGGED, no numbers assigned — the developer's call:** `MinTargetWeight`, `MinHitWeight`, and **the choice of
> weight terms itself** (threat-only / hittability-only / both / equal) is a doctrine-tuning decision, not a law.

**Worked example — the 3-way FFA.** Pools A 100, B 60, C 40. Equal weights: A deals 50+50, B deals 30+30, C deals
20+20. Incoming: A←50, B←70, C←80. **Total dealt 200 = total owned 200.** ✅
**Contrast the double-count bug:** A would deal its **full 100 to B AND its full 100 to C** = 200 out of a 100 pool.
**Scale that to a 4-way fight and A triples.**

**Cross-plane conservation:** in a combined battle a bombarding fleet's pool is allocated by the **same**
`AllocateFire` over an engageable set spanning **both planes** — one pool, one allocation, one residual, so a fleet
that both fights ships and shells the surface deals exactly its pool total, **never once per plane.**

### 13.8 JOINT #2 — combined-theater cadence (🔒 pinned, gates S5)

> *"The watch bill for a two-front action. Space stands a 5-second watch; ground stands a 1-hour watch. When a single
> battle spans both decks, somebody has to decide who rings the ground watch faster."*

**The mechanism it stands on (verified):** every hotloop's `deltaSeconds` is derived from **its own `RunFrequency`
boundary, not the master step** (`ManagerSubPulse.ProcessToNextInterupt` `:346-347`). So the space trigger **always**
fires with `dt = 5` and the ground processor **always** with `dt ≈ 3600`, no matter how big a chunk the player
fast-forwards. **Each plane is already fast-forward == watch independently.** And `3600 / 5 = 720` **exactly.**

> **🔒 THE PINNED DECISION: the `BattleTheater` owns the ground plane's fight-cadence for the duration of a combined
> battle. It force-steps the theater body's ground fight at a FIXED 5 s quantum (`TheaterGroundQuantum =
> CombatReactionStep = 5 s`) — the same fixed quantum watched or fast-forwarded — by riding the space trigger that
> already runs at 5 s. When the theater dissolves, the ground plane returns to its native 1 h cadence.**

**Four beats:**
1. **Formation** — when the 5 s space trigger finds an engagement coupled to a surface, it attaches a `BattleTheater`
   marker. A **pure function of game state at a 5 s boundary** → forms at the same game-time watched or skipped.
2. 🔴 **Ownership transfer (L9-safe)** — the native `GroundForcesProcessor.ProcessBody` **skips exactly two steps**
   for a theater-driven body: `ApplyEngagementManeuvers` and `ResolveRegionCombat`. It **still runs** movement,
   upkeep, radar reveal, attrition, and the order queue at 1 h. **NO second hotloop is added to `GroundForcesDB`**
   (root `CLAUDE.md` **L9**) — the native processor stays the only hotloop on that blob; it merely *yields the fight*.
3. **The theater step** — `CombatEngagement.Tick`, already at `dt = 5`, calls the extracted
   `ApplyEngagementManeuvers` + `ResolveRegionCombat` **as functions** with `deltaSeconds = 5`. The ground fight
   never sees a variable step and never sees 3600.
4. **Return** — marker removed at a deterministic 5 s boundary; the native processor resumes on its own grid.

**The three rules to guard forever:**
1. 🔴 **The theater step MUST use the space trigger's fixed 5 s `dt`.** Never pass a variable master delta into a
   group step. If the ground fight ever receives anything other than the fixed quantum, **fast-forward ≠ watch.**
2. 🔴 **`TheaterGroundQuantum` MUST evenly divide `GroundForcesProcessor.RunFrequency`** (5 s | 3600 s → **720,
   exact**). If either cadence is retuned, **keep the divisibility.**
3. 🔴 **A combined battle is NEVER routed through the instant `AutoResolve` shortcut** — both planes step through the
   *stepped* resolver at the fixed quantum, or the coupling boolean can't be read at the right instant and the Endor
   lock breaks.

**The one honest limitation (accepted, written down):** a ground battle stepped at 1 h and then switched to 5 s
mid-fight does **not** produce the same numbers as one stepped at 5 s from the start — coarse vs fine integration of
the same fight. But theater formation is deterministic, so **both watch and fast-forward switch at the same instant
and produce the same switched result — which is all determinism requires.**

**✅ The gauge EXISTS: `Pulsar4X.Tests/Resolver2DJointsSpecTests.cs`** — 10 tests, an **executable specification**
holding a self-contained reference `AllocateFire` and the fixed-quantum stepping, asserting the invariants so the
algorithms are pinned and proven **before** S5/S6 wire them into production. **It touches no production path.** When
S5/S6 land, they must reproduce this fixture's worked example.

> ⚠ **The parent design's §11 must be read as CLOSED.** It named these two joints as blocking homework and said
> *"'named' is not 'designed.'"* **Both are now designed and pinned.** Do not re-read §11 as open work.

### 13.9 🔴 BLOCKER — the frozen-anchor defect: S1/S2 are **only correct for two fleets**

**Found 2026-07-29 while wiring LD-29 (make the plane the combat default). It is why the flag cannot simply be
switched on.** Verified in source, not inferred.

**The mechanism, in three lines of code:**

| Step | What it does | Where |
|---|---|---|
| ① | `AdvanceClosing` moves **every** fleet's scalar `Separation_m` toward the **one shared** `desired` (the controller's preferred standoff) | `CombatEngagement.cs:1065-1072` |
| ② | `AdvanceAnchorPlane` then slides **only the controller's** anchor — `var ctrl = live[controller]` and the body mutates `cst` **and nothing else** | `:1088-1111` |
| ③ | With the plane on, `SeparationOf` returns the **2D anchor pair-distance**, no longer the scalar | `:1003` |

**⇒ At most ONE anchor moves per step. Every other fleet's anchor is frozen for the whole battle.**

**Why two fleets hide it completely.** In a 1v1 one of the two is always the controller, so the pair-distance tracks
the scalar exactly — which is precisely what `EfGroupPlaneAnchorTests.AdvanceClosing_FlagOn_SlidesControllerAnchorTowardEnemy`
asserts, and it is right. **All eight group-plane tests are two-fleet scenarios** (verified: `GroupPlaneTests`,
`EfGroupPlaneAnchorTests`, `EfGroupPlaneRangeGateTests` — every fixture stands up exactly two fleets). **The defect
is structurally invisible to the existing suite.**

**What breaks at three or more.** Each fleet's plane gap is measured to its **stored** `OpponentFleetId`
(`TryPlanePairDistance`, `:1013`). Take a 3-way where fleet **A** is the controller and fleet **C**'s stored opponent
is **B**: neither C nor B is the controller, so **both anchors never move, and `SeparationOf(C)` is pinned at its
seeded value for the entire battle.** Under the scalar model C's gap converged with everyone else's.

**The consequence, stated accurately.** ⚠ *An earlier draft of this section claimed a frozen fleet sits a gigameter
out of range. That was WRONG for a real game and is corrected here.* `EngagementRange_m = 1e9` is only a **coarse
broad-phase pre-filter** (`InRange`); the real gate is `WithinWeaponRange` (`:218`), which is **ON in the client**
(`RequireWeaponRangeToEngage`, `PulsarMainWindow.cs:113`), and `EnsureInCombat` seeds the gap from the **real
distance** (the 2026-07-16 freeze fix). So a battle forms at genuine weapon reach and a frozen pair is stuck at
**weapon-range scale** — which still means *the brawler never closes on the artillery* and that fleet contributes
nothing for the rest of the fight, but not the three-orders-of-magnitude disaster first described.

**Multi-fleet is not an edge case here:** the resolver is natively multi-party (fire divided `1/split`,
`:745-747`), `MultiPartyEngagementTests` covers assist / join / fire-split, and the AI masses fleets
(`FleetAssembly`) — so 3+ fleets in one engagement is the normal case in a real game, and the *only* case the
scenario matrix (§8 row 10) calls HANDLED for space.

### 13.9a THE ROOT CAUSE — the "2D plane" is a 2D READOUT PAINTED ON A 1-D MODEL

**This is the finding that matters, and LD-30 is what exposed it.** The frozen anchor is a *symptom*; here is the
disease, in one line of source:

```csharp
double moved = ctrlGapBefore - cst.Separation_m;              // CombatEngagement.cs:1106
Vector2 newAnchor = GroupPlane.Place(cst.Anchor, dir * moved);
```

**The anchor moves by however much the SCALAR gap just changed.** The scalar is the driver; the 2D position is a
follower. And `SeparationOf` then reads that follower back as if it were the truth (`:1003`).

**Everything else falls out of that:**

- **Only one anchor can move**, because only one fleet has a scalar-gap delta attributable to it — the controller.
  That *is* §13.9. It was never a missing `for` loop; it is the shape of the model.
- **The plane cannot express LD-30 at all.** A single shrinking scalar cannot represent *"they're moving as though it
  was an RTS"* — one number has no room for units moving independently inside an arena.
- **The arena does not exist.** There is no radius, no `0,0` centre, no notion of an engagement *extent*. S1 seeds a
  frame from real 3D positions and gives each fleet a drifting anchor; a joiner **copies the frozen frame** (`:614`)
  and is projected into it. **Nothing expands, because there is nothing to expand.**

**⇒ The (a)/(b) repair I previously offered here is WITHDRAWN. Both were patches to the follower.** Neither would have
produced the arena, and neither would have made the units move.

### 13.9b WHAT LD-30 ACTUALLY REQUIRES

| Piece | Today | LD-30 |
|---|---|---|
| **Trigger** | `WithinWeaponRange` = `Max(reachA, reachB)`, client-on | ✅ **already correct — this is the ruling, built** |
| **Weapon range** | ❌ **1 of 6 classes designable** (see below) | the arena's radius, so it MUST be designable |
| **Battle extent** | none — no radius, no centre | a circle at `0,0`, radius = the largest weapon range present |
| **Closing** | one scalar gap shrinking for everyone | **units moving at their own speeds inside a fixed arena** |
| **Position** | a follower of the scalar | **the source of truth** |
| **Joining** | copies the frozen frame; nothing expands | expands the radius **iff** the joiner's range is larger; else fits inside |
| **Joiner state** | seeded onto the frame | **enters at the battle's current state** — no rewind |

🔴 **THE LOAD-BEARING PREREQUISITE — only the beam can express a designed range.** LD-30 makes the arena's size a
function of built weapon range, so this stops being cosmetic and becomes the thing everything hangs off
(`ShipCombatValueDB.cs`):

| Weapon | Range source | |
|---|---|---|
| **Beam** `:359` | `beam.MaxRange` — **the design** | ✅ |
| Railgun `:381` | `RailgunRange_m` = 500 km | ❌ engine constant `:62` |
| Flak `:400` | `FlakRange_m` = 50 km | ❌ engine constant `:52` |
| Disruptor `:417` | `DisruptorRange_m` = 400 km | ❌ engine constant `:73` |
| Plasma `:435` | **borrows `RailgunRange_m`** | ❌ not even its own |
| Missile `:448` | `MissileRange_m` = 1000 km | ❌ engine constant `:68` |

**So five of six weapons run off a fixed ladder no design decision can reorder** — and under LD-30 that ladder would
silently dictate the size of every battlefield. *(Same finding as `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`
**D4-1**; LD-30 promotes it from a designer wart to a resolver blocker.)*

**Order of work this implies:** ① make weapon range designable across all six classes (the cradle-to-grave rung) →
② give the engagement an arena with a radius → ③ make positions the source of truth and move units at their own
speeds inside it → ④ joiner expand-or-fit, entering at current state. **The gauge is owed at each step
(`docs/TESTING-TRACKER.md` G-C1); the 3-fleet no-frozen-pair assertion remains the acceptance test for ③.**

---

## 14. THE BUILD LEDGER — every slice from all six tracks, one table

**Vocabulary discipline (three states, never two).** *built-and-gauged* = exists, has a CI gauge, gauge is green ·
*built-but-runtime-unverified* = exists and compiles, **CI cannot run the client** so behaviour is unwatched ·
*built-but-INERT* = exists and is gauged but **nothing turns it on at runtime.** "Built" alone is not a status.

### 14.1 Track 1 — the KERNEL MERGE (the ship↔ground damage merge)

| Slice | What | State |
|---|---|---|
| **1** kernel skeleton | `CombatKernel.cs` — the neutral view + pure math, written byte-for-byte from the live ship + ground math. **Purely additive, unwired for exactly one slice.** `CombatKernelTests` both PINS outputs to hand-computed values **and CROSS-CHECKS** against the live functions over an input sweep, so drift between the two copies goes red. | ✅ **LANDED** 2026-07-08 |
| **2** ship routes through the kernel | Realized as **delegation, not a monolithic `ResolveSalvo`**: the pure arithmetic lives ONLY in `CombatKernel`; `CombatEngagement`'s same-named helpers became thin `=> CombatKernel.X(...)` delegators and its constants forward to the kernel's. **Why delegation over a rewrite: delegation to identical code is provably byte-identical (zero numbers move).** | ✅ **LANDED** 2026-07-08 |
| **3a** flat armour | `GroundDamageMatrix.ArmourSoak` + its two constants forward to `CombatKernel` — the last piece already identical on both sides. **Zero behaviour change.** | ✅ **LANDED** 2026-07-08 |
| **3b-i** the bridge | `GroundCombatant.cs` maps a `GroundUnit` → the kernel view; velocity/tracking/saturation chosen so the kernel **reproduces** the ground dodge/shield semantics. **Unwired** → byte-identical. `GroundKernelBridgeTests` proves the triangle/dodge/shield fall out of the kernel. | ✅ **LANDED** 2026-07-08 |
| **3b-ii** the swap — **THE TRIANGLE DISSOLVES** | The Armor▸Infantry▸Artillery type edge is **no longer a flat ×1.5/×0.67** — it emerges from raw stats × weapon nature vs the target's evasion/shield/armour, the ship way. `AvgTriangleVs` **retired**. **Zero test re-baseline needed** (every combat fixture is same-type, where the triangle was 1.0); cross-type numbers gauged by `GroundResolverComparisonReadout` — CI-certified before/after: **Armor→Inf 258→167, Inf→Armor 44.5→77.5**, same-type unchanged. | ✅ **LANDED** 2026-07-08 *(developer chose "option 1: type edge lives in stats")* |
| **3c** shield → depleting POOL + dodge on the kernel | `GroundUnit.CurrentShield` (save-safe, deep-copied), drained by the nature soak-fraction before armour, regenerating toward capacity. **With 3c the ground damage model IS the kernel — the merge's damage half is complete for both domains.** *(v1 flags: a unit regenerates only while in a contested region; orbital bombardment still reads the innate `Shield` %, not the pool.)* | ✅ **LANDED** 2026-07-08 |
| **4a** the closing fight plays out | `GroundForcesTests.ClosingFight_LongRangeWhittlesTheRusherDuringTheApproach` — equal-stats long-range vs rusher, 3 hexes apart, over 60 game-hours; the rusher ends more damaged because the kiter fired across the whole approach. **The north-star fight, proven end-to-end.** | ✅ **LANDED** 2026-07-08 |
| **4b** the ground combat interrupt | Halts the clock the **first** tick a NEW planetary battle forms (the ground mirror of the space combat-pause), via the `WasInBattle` latch so an ongoing fight doesn't re-halt. Behind `InterruptTimeOnNewBattle` — **client turns it ON** (`PulsarMainWindow.cs:87`). *(v1: coarse to the hourly tick; halts on ANY new battle, not only the player's.)* | ✅ **LANDED** 2026-07-08 |
| **5a** formation aggregation | `GroundFormationTools`: `FormationSpeedMult` (**MIN** — "moves at the slowest"), `FormationReachHexes` (**MAX** — "strikes as far as the longest"), `FormationStrength` (**Σ**), `FormationHealth` — the ground twin of `FleetCombat`. Plus the **cohesive march** (every member timed on the shared slowest pace). *(v1: DIRECT members only; tree roll-up is a follow-up.)* | ✅ **LANDED** 2026-07-08 |
| **5b** vocabulary + the client battalion sheet | Rename the UI to Fleet▸Group / Battalion▸Formation; surface the 5a aggregation as a battalion readout. **UI-heavy — needs the developer's local build.** | ❌ **REMAINING** |
| **5c** bucket the ground resolver | O(units²) → O(buckets). **Same slice as W4.** Full plan in §12.4. | ❌ **REMAINING** |

### 14.2 Track 2 — UNIFIED RESOLVER + BATTLE STATISTICS (9 slices)

> 🔴 **VERIFIED 2026-07-29: ONE of the nine exists.** Every one of these greps returns **zero files** —
> `CasualtyTier`, `ModelCount`, `BattleLedger`, `WeaponKey`, `VictimSnapshot`, `WeaponTally`, `SideReport`,
> `GroundRetreatDB`, `MedicalAtb`, `WoundedFraction`. **Read slices 2–6 and 8–9 as design, not as progress.**

| Slice | What | Size | State |
|---|---|---|---|
| **1** `WeaponReaches` shared range gate | Two predicates in the kernel: `WithinReach(reach, gap)` (the convention-free core, same units both sides) and `WeaponReaches(w, separation_m)` (the space gate layering the `Range_m ≤ 0 ⇒ unbounded` beam convention on it). | S | ✅ **LANDED** 2026-07-22 · `CombatKernelTests.RangeGate_SharedReachPredicate_PinsBothConventions` |
| **2** `CasualtyTier` wound function | killed / wounded / untouched bands. Monolith path real; squad path dormant against a non-existent `ModelCount`. | S | ❌ |
| **3** shared report records + ground attribution | `WeaponKey` / `VictimSnapshot` / `BattleLedger` / `BattleReport` / `WeaponTally` + **one free line** in the ground per-source loop. | S–M | ❌ |
| **4** space report storage + trigger | **NOT a free reuse** — needs a synthesized **battle identity** and a **whole-engagement-end trigger** with dedup. | **M–L** | ❌ |
| **5** ground battle log + report emit | accumulate per body+region; emit when the `WasInBattle` latch clears (`:331`). | M | ❌ |
| **6** report UI | the per-side / per-enemy-weapon sub-table beside the existing event table. | S–M | ❌ |
| **7** per-weapon ground resolution | 🔴 **THIS DOC'S SOURCE CLAIMED IT UNBUILT — IT IS BUILT.** Slice 7 **is W2**, landed **2026-07-21**, six days *before* that doc's verification stamp. Verified in source: `GroundForcesProcessor.cs:479` + `GroundCombatant.cs:96` + `GroundWeaponMount.cs`. | M | ✅ **BUILT** = W2 |
| **8** ground retreat + `GroundRetreatDB` | **The single biggest new piece.** Ground has no withdraw stance — only `HoldGround` / `CloseToEngage` / `StandOff` (`GroundForcesDB.cs:278-284`, verified). | **M–L** | ❌ |
| **9** wounded recovery | recovery tick on the hourly hotloop, gated by the friendly-ground check; `MedicalAtb` Apothecary deferred. Itself gated on `ModelCount`. | M | ❌ |

> **Ruling: this design is DECIDED — *"build it, never redesign it."*** That is exactly why it is worth being precise
> about how much is waiting.

### 14.3 Track 3 — THE W-TRACK · Track 4 — REAL DISTANCE · Track 5 — THE 2D PLANE

Full detail in §12.4 (W1 ✅ · W2 ✅ · W3 ✅ · W1b ✅ · **W4 ❌**), §12.3 (1a ✅ · 1b ✅ · **1c 🟠 partial** ·
2 ⚠ shipped-under-another-name · **3 🟠 partial** · **4 ❌** · **5 ❌**), and §13.5 (S0 ✅ · S1 ✅ · S2 ✅ ·
**S3–S6 ❌**, both joints 🔒 pinned with an executable-spec gauge).

### 14.4 Track 6 — FLEET COMBAT CLOSING (the space range spine)

**§0 — the one decision the whole tree serves: STANDOFF vs BRAWL.** *Do I build a fast, long-range artillery fleet
that opens fire first and kites — never letting the enemy's big short-range guns reach me? Or a brawler that eats
long-range fire on the way in to reach knife range where it dominates?*

> **The test for any feature here: does it sharpen that decision, or is it fidelity the player can't act on?**
> If the latter, it goes in the parking lot.
>
> *"This is a **4X, not an RTS.** The player never flies a ship. The player is the admiral: they write the
> **playbook** (doctrine / rules of engagement), and the math fights the battle."*

| Rung | What | State | Gauge |
|---|---|---|---|
| **ROOT A** weapon range on the combat profile | `WeaponProfile.Range_m` (**convention: 0 = unbounded** — serialization-safe, no `Infinity` in JSON) | ✅ **BUILT** 2026-06-27 | `FleetAggregationTests.WeaponProfile_CarriesDesignRange_FirepowerUnchanged` |
| **ROOT B** fleet capability aggregation | `WarpSpeedFloor` / `DeltaVFloor` = **MIN** (the fleet moves as one) · `FirepowerAtRange(R)` = Σ weapons reaching R · **`SensorReach` = MAX, not summed** (two identical sensors are redundant; diverse ones are complementary) | ✅ **BUILT** 2026-06-27 | `Floors_TakeTheSlowest_SensorReach_TakesTheBest` |
| **PHASE 1** single-range closing ⛔ **superseded in SHAPE by LD-30 — the arena does not close; units move inside it** | `Separation_m` seeded from real distance; `BuildFireMix` gates each weapon; `AdvanceClosing` moves the gap toward the **faster** side's preferred range (controller = max maneuver = min evasion). Tunables `ClosingSpeedScale_mps` (0 = freeze), `InitialSeparationDefault_m`. | ✅ **BUILT** 2026-06-27 | `ClosingTests` — a 100-km fleet hits across a 50-km gap, a 1-km fleet deals **zero** (kited); determinism; flag-off identity; the faster side dictating |
| **PHASE 2** kiting counters (endurance tier 1 = **fuel**) | `ManeuverBudget` seeded from `DeltaVFloor`; **only a fleet with budget left can be the controller**, and it spends `ManeuverBurnRate × dt` — so a kiter that burns out stops dictating the range. **Interceptors are emergent from P1's speed rule — verify, don't build.** | ✅ **BUILT** 2026-06-27 | `Kiting_RunsOutOfBudget_TheEnemyCloses` |
| **PHASE 3** first-shot trigger + standoff | `EngagementPosture` (WeaponsFree / WeaponsHold / ReturnFire) on `FleetDoctrineDB` — **the first ROE knob, grown on doctrine, not a parallel system.** Both non-WeaponsFree ⇒ **tense standoff, no battle.** | ✅ **BUILT** 2026-06-27 | `WeaponsReleaseTests` |
| **PHASE 4** per-sub-fleet ranges | ⏸ **SPEC-READY, deliberately NOT built blind** | — | gauge **named, unwritten** |
| **PHASE 5** Rules of Engagement | consolidate the knobs into ONE authored ruleset + presets + decision-point auto-pauses | ❌ | — |
| **PHASE 6** the battle readout, consolidated | the agency surface, finished | ❌ | — |

> **PHASE 4's "why paused" ruling — verbatim, and it is a standing principle, not a one-off excuse:**
> *"P4 is a resolver **RESTRUCTURE**, and its payoff … is an incoming-fire behaviour whose correctness is a **feel**
> question CI can't answer — only the developer's play-test can. **Landing that blind would risk the well-tested
> combat engine for a result I can't see.**"*
>
> **Its five-step implementation plan, preserved:** ① a **per-component gap** (`Dictionary<int,double>` on the top
> fleet's `FleetCombatStateDB`; `CollectCombatShips` already recurses components — extend `CombatShip` with a
> `Separation` and tag it there, the clean hook) · ② **per-component `AdvanceClosing`** (each closes per its own
> maneuver floor and desired range; **cohesion = close to the band and HOLD**, don't fragment past) · ③ the
> **OUTGOING gate — the easy half** (make `BuildFireMix`'s separation per-ship; low risk, additive) · ④ 🔴 the
> **INCOMING gate — the hard half, the actual restructure** (a defender takes an attacker's weapon only if it
> reaches the **DEFENDER's** component gap, so fire must be re-gated at each target component and `ApplyCasualties`
> bucketed per target component — **this is the part that needs playtest**) · ⑤ default targeting = the triangle,
> plus post-battle form-up.
>
> ⛔ **PHASE 4 IS SUPERSEDED BY LD-30 — do not build it as specified.** Its whole premise is *"each component needs
> its own `Separation_m`"* — more resolution on a **gap**. LD-30 goes the other way: **position is the source of
> truth and gaps are derived**, so per-component gaps are the wrong direction. Its five-step plan is kept below as
> the record of a carefully-reasoned approach that the arena replaces, and its "why paused" ruling is kept because
> **that reasoning is still exactly right and still binds** (a resolver restructure whose payoff is a *feel*
> question CI cannot answer — which is now doubly true of the arena itself).
>
> ⚠ **Phase 4 also overlapped Group-Plane S3.** `FleetCombatStateDB.cs:116-118` says **S3 replaces the anchor with one
> point per role sub-fleet** — which is the same ground Phase 4 covers. **An open question: which one governs
> per-sub-fleet positioning?** *(§16 Q-3.)*

**The live-visibility layer, BUILT 2026-06-27 — the closing model narrates itself.** Since CI can't run the closing
fight, the engine writes `[Combat]` lines (gated on `NarrateToLog`, which the client turns on): a **per-step closing
line** per fleet (gap / IN-or-OUT-of-RANGE / reach / maneuver reserve), a **WEAPONS RELEASE** line when the
first-shot rule breaks a standoff, and a **maneuver-reserve-spent** line when a kiter burns out. Plus
`CombatEngagement.DumpActiveCombat(system)` (a DevTools "Dump Combat" button), the DevTools toggles for closing and
first-shot (`DevToolsWindow.cs:855`,`:857`), and **"Spawn Combat Scenario"** (`CombatSandbox.SpawnCombatScenario`).
**This is the Visibility Gate applied to a CI-blind system, and it is why play-test data can troubleshoot it.**

---

## 15. BATTLE STATISTICS — stated plainly

**Is killed / wounded / untouched, per-weapon, per-side recorded anywhere today?** Five findings, in order of how
badly each is missing:

1. 🔴 **Per-weapon kill attribution: NONE, anywhere.** Not space, not ground. The richest artifact is the space
   **"Salvo Note"**, which names weapon *classes* that fired ("Railgun + Beam") plus an aggregate hit/dodge % and
   total damage — but attributes **zero kills** to any specific weapon.
2. 🔴 **Wounded / untouched: STRUCTURALLY IMPOSSIBLE.** Ships and ground units are whole-or-dead; there is no
   partial state to tally. **Cannot be fixed without un-parking the per-component damage sim, or adding Fix B's
   model count.**
3. 🟠 **Per-side, per-fleet counts: PARTIAL, space only.** `BattleLog` records per-fleet `Salvo` events with
   whole-ship `ShipsLost`/`ShipsLeft`. A caller *could* group these by faction, but **nothing generates a per-side
   summary**, and on retreat/end the events carry `ShipsLost = 0`, so there is **no end-of-battle casualty summary
   at all.** And it is **runtime-only, not saved** (250-event ring).
4. 🔴 **Ground: NOTHING.** No battle log, no event, no statistics of any kind (grepped across the whole
   `GroundCombat` directory). `ResolveRegionCombat` returns a bare `bool`. **Units die and vanish.**
5. 🔵 **`AutoResolve` *does* return `DestroyedA`/`DestroyedB` lists** — a genuine per-side casualty list — but they
   are consumed **only inside the resolver and tests**, never persisted, and **the live path doesn't use
   `AutoResolve`.**

### 15.1 The design — a running damage ledger, totaled at battle-end

**Don't try to catch the exact killing shot as it lands — the space engine can't, and forcing it would fight the
model.** Instead **add up cumulative damage per victim, per enemy weapon, over the whole battle.** At battle-end (or
at the moment a victim dies), credit each loss to the enemy weapon that dealt it the **most** total damage —
**plurality = "the killing blow."** Deterministic, and it works the same for aggregate space and per-source ground.

- **Ground: genuinely one free line.** `ResolveRegionCombat` is *already* a per-source loop that holds attacker →
  target → damage for every shot, including nature, penetration, and design id. **Today it throws that away by
  summing into one scalar. That is the cheap hook.**
- **Space: medium, and honest about its limit.** Split kill credit across the incoming fire mix **proportionally to
  each weapon class's damage share**. Two non-negotiables: 🔴 **snapshot the victim at the kill site, not at
  battle-end** (the ship is already `Destroy()`'d by then), and 🔴 **label the report "proportional
  (auto-resolve)."** In a multi-party battle credit is proportional **and ambiguous across fleets** — it may not
  resolve to a specific enemy weapon instance or even a specific enemy fleet. **That is a real limitation of the
  aggregate model, worth stating in the report itself, not just in a footnote.** In a 1v1 the label is fully honest.

**The report fires on ANY battle-end** — wipe, capture, disengage, or a called retreat. Retreat is *one* trigger, not
the only one.

> 🔴 **The space storage is harder than it looks — reclassified M → M–L.** The named emit sites are **per-fleet,
> per-state-change** events and `BattleLog` is **per-fleet by design** (*"a battle has no single entity"*). A
> `BattleReport` is **one per battle covering both sides.** Emit at the existing sites and you fire **N reports per
> battle.** To get one correct report you must first **synthesize a battle identity** and add a real *"fewer than two
> hostile sides remain"* whole-engagement-end trigger with dedup — **none of which exists today.** That is the
> actual work.
>
> **Ground's hook is named and cheap by comparison:** the `WasInBattle` latch clearing (`GroundForcesProcessor.cs:331`)
> is the fighting → not-fighting signal. Generalize `BattleLog` to hold domain-neutral `BattleReport`s, accumulate
> per body + region, emit on that transition, **reset the ledger on the same transition**, and treat a captured
> region that re-fights next tick as a **fresh battle.**

### 15.2 The measurement caveat that must ride with the readout

> **The per-side "Wounded" total sums a wounded 500-HP tank (1) and a wounded rifleman (1) into the same number — it
> mixes UNITS and MEN.** Fine as a casualty readout; **not a rigorous manpower count.** Say so on the report.

### 15.3 Wounded recovery (design, unbuilt)

Squad models heal back to untouched slowly (`WoundedRecoveryPerDay`) when the unit is **on friendly-held ground and
not in combat** — reusing the friendly-ground check already in `GroundForces.ResupplyUnit`. An optional
Apothecary/medic **component** carrying a new `MedicalAtb` (researched → built → located → **bombable**, exactly like
`GroundDefenseAtb`) speeds it up and can turn a fraction of would-be *dead* into *wounded* — *"a field hospital means
fewer permanent losses."* **A single monolithic unit does NOT self-heal in v1** — it stays degraded until a
repair/medic building is present, which keeps the model honest without inventing ground repair wholesale. The tick
rides the existing hourly hotloop beside environmental attrition. **Cradle-to-grave-clean: the medic is a component,
not an engine flag.**

---

## 16. OPEN QUESTIONS AND DEVELOPER DECISIONS OWED

### 16.1 Decisions only the developer can make

| # | The question | Why it's blocking | Where it bites |
|---|---|---|---|
| **Q-1** | **`AttackPerDps = 1/2500`** — bless it or retune it? | It is the ONE number converting space joules to ground attack points. Everything a space weapon does on a surface scales off it. | `SpaceWeaponGround.cs:27` · §12.4 |
| **Q-2** | **`MinTargetWeight` and `MinHitWeight`**, and the **choice of weight terms** (threat-only / hittability-only / both / equal) | Joint #1's conservation machinery is a hard invariant; the *weight* is a doctrine-tuning decision the agent explicitly did not make. | §13.7 · gates S6 |
| **Q-3** | ~~Which governs per-sub-fleet positioning — S3 or PHASE 4?~~ ✅ **ANSWERED BY LD-30 — NEITHER.** | Both were ways to give a *gap* more resolution. LD-30 makes **position the source of truth and gaps derived**, so P4's "each component gets its own `Separation_m`" is the wrong direction outright, and S3 survives only as the **placement rule inside the arena** (`RoleGeometry`), not as a second positioning system. **One verb: units have positions and move.** | §13.9b |
| **Q-4** | **Standoff radii and role-bearing constants** (S3) | Balance-pass defaults were never chosen. The standoff-vs-brawl and flank-vs-hold gut-check is **a live-tuning question CI cannot answer.** | §13.6 #8 |
| **Q-5** | **`RangeBaseMiss = 0.9`** — is the range-accuracy term calibrated? | It is a **mutable public static** and **the kernel's only purity break.** It is also **untested on the ground path**: if a slice ever feeds ground a non-zero separation into a path that reads it, **every ground outcome changes.** | `CombatKernel.cs:57` · §10 row 1 |
| **Q-6** | **`ManeuverBurnRate` and `ClosingSpeedScale_mps`** — ⚠ **hold this one.** | "Live calibration of the closing rate + the *is it fun* gut-check are the developer's play-test" — stated at Phase 1/2, never done. **Under LD-30 these dials change meaning** (`ClosingSpeedScale_mps` scales a gap that no longer exists; unit speed becomes the driver), so calibrating them now would tune a mechanism that is being replaced. **Re-ask after §13.9b step ③.** | §14.4 · §13.9b |
| **Q-7** | **`Math.Round(0.5)` banker's-rounds to 0** in the casualty split — one downed model gives 0 dead / 1 wounded. | *"Pick that rounding on purpose."* Not yet picked. | §9.3 · §15 |
| **Q-8** | **`HealthBandSize`** — how coarse should the bucket health band be? | Too fine and a swarm stops compressing; too coarse and damage smears. | §12.4 (W4) |
| **Q-9** | **`MissileLauncherFirepowerStub = 100 000`** — missile firepower is a flat stub, not derived from the warhead. | Every missile ship's combat value is fiction until this is real. | `ShipCombatValueDB.cs:38` |

### 16.2 Structural questions the code raises

| # | Question | The evidence |
|---|---|---|
| **O-1** | **Does `CombatKernel.Combatant` earn its keep?** | It is a `sealed class` that **no kernel function takes** (every function takes primitives) and that **has zero production consumers** — only `GroundKernelBridgeTests` builds one. It is a design seam nothing sits on. **Either wire it or retire it.** |
| **O-2** | **Should `AutoResolve` and `StepEngagementGroup` remain two resolvers?** | They have genuinely different fidelity — one has no dodge, shields, ammo, heat, doctrine, or retreat. **Two paths for one verb.** *(§10 row 9.)* |
| **O-3** | **When does `dt` get fixed on the ground?** | Ground damage is per-tick, space per-second. **This must be fixed BEFORE the wound model or the tick is ever shortened, and it re-baselines every ground gauge.** *(§10 row 8.)* |
| **O-4** | **`ResolveSalvo` was designed and never built.** | The merge design specified a single neutral `CombatKernel.ResolveSalvo(attackers, defenders, dt, ctx)`. **It does not exist** — the build chose delegation instead, deliberately and with a written reason. **The design text is superseded by that choice; do not build `ResolveSalvo` on the strength of the old spec.** |
| **O-5** | ~~Is the 2D plane wanted at runtime, or is it parked?~~ ✅ **ANSWERED 2026-07-29 — LD-29: *"it should be the default of all combat."*** | **Superseded by a NEW blocker: §13.9.** The flag cannot be switched on as-is — S1/S2 are only correct for two fleets. The ruling is delivered by fixing §13.9, gauging it, then adding the client line. |
| **O-6** | **`FleetDoctrineDB.SpeedMult` is a dead dial** and **`FleetRetreatDB` is a dead-end hook** (no survivor actually moves). | Both are shipped knobs the sim ignores — the "never ship a dead knob" failure. |

---

## 17. THE PARKING LOT — deliberately NOT built (do not promote by accident)

**Written OUT on purpose.** A thing in this list is not a gap; it is a decision.

**From the closing spine:**
- ⛔ **2D positioning / flanking / facing — *"permanently parked unless the no-RTS principle is itself revisited."*
  🔴 SUPERSEDED TWICE OVER — DO NOT TREAT AS LAW.** First by shipped code (§19 S-1), then decisively by **LD-30**,
  which makes 2D *the model* and states outright that inside a battle units move *"as though it was an RTS."*
  **The no-RTS principle was about the PLAYER's control surface, not the simulation's fidelity** — doctrine is still
  the only lever; the sim underneath is free to move units. Original text preserved to show what changed.
- **Environmental hazard fields** (energetic-particle bands wrecking sensors / dragging movement) — lands on
  sensors + movement + combat at once; evocative, expensive, its own build.
- **Crew provisions** as a ship endurance variable (endurance tier 3 — the one genuinely new ship sub-system).
- **Munitions depletion** as a per-ship reserve (endurance tier 2).
- **Player-authored fine-grained target priorities** — the v1 default targeting is the triangle.

**From the dial-insertion map (each gated on a subsystem that must be built first):**
adaptive shields (→ frequency modulation) · a combat-environment modifier (→ fighting in atmosphere/water) ·
per-shot timing (→ charge telegraph) · a self-damage rule (→ overcharge/burnout) · profile-swap (→ multi-ammo) ·
the effect bus + capture (→ stun/conversion/Exotic effects) · positional arc/traverse (**or drop it as flavour** —
the aggregate resolver is non-positional) · full missiles-as-individually-resolvable-targets (→ per-projectile PD;
today it is a **fleet-PD-rating-vs-missile-damage intercept fraction**, not a shootdown loop) · the
air/altitude/depth combat layer · the H8 gate-network/addressing · Transfer ▸ teleport.

**Also parked:** the per-component damage sim (`DamageComplex`) — **the parent of whole-or-dead and therefore of
half this document's limitations** (root `CLAUDE.md` **L10**).

> **The rule that keeps this list honest (from §0 of the closing spine):** *"does it sharpen the standoff-vs-brawl
> decision, or is it fidelity the player can't act on?"* If the latter — parking lot.

---

## 18. CONNECTIONS (the Prime Directive map for the resolver)

**Feeds IN** — `ShipCombatValueDB` (built once at `ShipFactory.CreateShip`) · `WeaponProfile` + `WeaponClassifier` ·
`GroundUnitDesign`/`GroundUnit` via `GroundCombatant` · `FleetDoctrineDB` + `EngagementPosture` (the **entire**
player control surface) · `GroundFormationDoctrine` + `GroundEngagementStance` · `DiplomacyDB` via `AreHostile`
(space only — **ground does not read it**, §10 row 7) · the sensor track table via `CanEngageTarget` (space only) ·
`PositionDB` (the 3D seed for the plane) · `FleetCombat` aggregation (min speed, min Δv, **max** sensor) ·
`GroundRangeTools`/`GroundMiniHex` (the metres↔hex layer) · `MasterTimePulse`/`ManagerSubPulse` (the fixed-cadence
quanta that make fast-forward == watch) · `GroundTerrain`/`GroundFortification` (cover × fort divisors) ·
`WeaponSupply` (ground ammo/power).

**Feeds OUT** — entity destruction (space: whole ships out of `ApplyCasualties`; ground: `RemoveAll(Health<=0)`
`:334`) · region/planet capture flips (→ colony ownership) · `BattleLog` → `BattleReportWindow` (auto-opens at
battle end) · `FleetRetreatDB` (**a dead-end today**) · `MasterTimePulse.RequestCombatHalt` (the combat interrupt,
both domains) · the `[Combat]` narration lines the play-test reads.

**Shares STATE** — `FleetCombatStateDB` (separation, ammo pool, heat pool, shield pool, maneuver budget, **and the
2D frame/anchors**) · `GroundForcesDB` (units, formations, the `WasInBattle` latch) — 🔴 **L9: it is the ONE hotloop
on that blob; never add a second** · `GroundUnit.CurrentShield` · the static `CombatKernel.RangeBaseMiss` (mutable —
a cross-run shared mutable).

**Triggers** — `BattleTriggerProcessor` (keyed to `StarInfoDB`, 5 s sweep) → `CombatEngagement.Tick` ·
`GroundForcesProcessor` (keyed to `GroundForcesDB`, 1 h) · the combat interrupt → clock halt → the client banner.

**Cradle-to-grave note.** The resolver is a *layer*, not a buildable — so its chain is the **doctrine** chain: a
group's shape comes from the sub-fleet/formation roles the player composes, which come from ships and units the
player designed, built from mined and refined materials, gated by research, and **can lose.** Shoot the artillery
group's hulls off and its standoff advantage is gone; kill the flank wing and nothing reaches the enemy rear;
destroy a sensor and detection goes dark. **The shape is only as real as the units that hold it.**

> **⚠ One-Verb-Both-Seats check on this system.** The resolver passes: both seats issue **doctrine**, and the AI's
> `FleetRoleComposer.FormRoleSubFleets` call (`NPCDecisionProcessor.cs:819`) is the same primitive. **The place to
> watch WAS Q-3** — two parallel positioning systems for one verb. **LD-30 closes it:** position is the source of
> truth, gaps are derived, and there is exactly one way a unit moves. Do not revive Phase 4's per-component gap.

---

## 19. THE STALE-CLAIM REGISTER — what the source docs asserted that is no longer true

**Every one of these was live text in a doc a reader would have trusted.** They are recorded rather than silently
dropped, because *how* a doc went stale is the most reusable lesson in the file.

| # | The stale claim | The truth at HEAD |
|---|---|---|
| **S-1** | 🔴 **"Range is a scalar per group, not a position… No 2D. No facing. No flanking."** — locked decision #2, and its canopy *"2D / flanking / facing — permanently parked."* | **OVERTURNED BY CODE IN THE TREE.** `CombatEngagement.cs:413` `EnableGroupPlane`; `FleetCombatStateDB.cs:83-118` HasFrame/FrameOrigin/FrameXAxis/FrameYAxis/Anchor/GroupPositions; `:110-112` says *"Slice S2 (built): SeparationOf/WithinWeaponRange now read the 2D pair-distance"*; gauge `EfGroupPlaneRangeGateTests`. **Recorded as SUPERSEDED (§17), not as live law.** |
| **S-2** | **"Slice 7 (per-weapon ground fire) is unbuilt — a genuine new mechanic, effort medium"**, stamped *verified 2026-07-27*. | **It is W2, BUILT 2026-07-21 — six days EARLIER.** Two docs in the same folder contradicted each other. *(§14.2.)* |
| **S-3** | **"a ground unit has exactly one weapon"** / *"one range, one nature; it can't hold a close weapon while firing a ranged one."* | **False since W2.** The mount loop is `GroundForcesProcessor.cs:479`. |
| **S-4** | **"the ground calls the 2-arg `HitFraction`, so separation defaults to 0"** | **Refuted** — `:421-423` passes a real metre separation (`GroundMiniHex.RealGapMetres`). |
| **S-5** | **"a `GroundUnit` has no continuous position, only integer hex coordinates"** | **Refuted** by K2's sub-tile offsets (`MiniOffX_km`/`MiniOffY_km`). |
| **S-6** | **"`WeaponProfile` has no Range field"** · **"once engaged, ALL weapons fire — range is not gated"** · **"no fleet-speed aggregation feeds combat"** — a 2026-06-27 "Where we are" snapshot. | **All three false.** `WeaponProfile.Range_m` exists; the gate is in `BuildFireMix` `:1317`; `FleetCombat` aggregates. **A dated snapshot table that was never re-dated.** |
| **S-7** | **"railgun / flak / missile default to 0 (rangeless)"** | Each now has a **hardcoded class-default** (`ShipCombatValueDB.cs`: flak 50 km `:52`, railgun 500 km `:62`, missile 1000 km `:68`, disruptor 400 km `:73`; **plasma borrows the railgun's** `:435`), and the gauge moved with it. 🔴 **The follow-up is still open and is now the load-bearing one: those are ENGINE CONSTANTS, not design dials — only the beam can express a designed range**, so the "battle commences at the longest-ranged weapon" rule runs off a **fixed ladder no design decision can reorder.** |
| **S-8** | **"`RealRangeKmFor` is THE single seam; a later slice substitutes a real per-weapon stat here and that one substitution flips the whole model."** | **The behaviour shipped and BYPASSED the seam.** `GroundRangeTools.cs:94` is still `=> RealReachKm(...)`. The metre gate went in under `EnableMiniHexCombat` instead. **A designed seam that the build routed around is worse than no seam — it reads as an unbuilt hook.** |
| **S-9** | **"S3 needs `FleetRoleComposer.FormRoleSubFleets`'s missing game-loop caller."** | The caller exists (`NPCDecisionProcessor.cs:819`). **S3 shrinks to the `RoleGeometry` table alone.** *(`Fleets/CLAUDE.md:18` still carries the stale line.)* |
| **S-10** | **"560 km per hex on Earth."** | **Illustrative, not authoritative.** Real coarse pitch is derived per region; **Earth's actual figure awaits a CI measurement.** |
| **S-11** | **"the teardown's standing caution: open `docs/archive/SYSTEMS-STATUS-AND-TEST-PLAN.md` before touching the ground resolver."** | 🔴 **That doc is RETIRED** (2026-07-27). The pointer resolves — the file is on disk in `archive/` — so it **silently "works" and misleads.** The live replacements are **`docs/SYSTEM-CONNECTION-MAP.md`** (connections), **`docs/TESTING-TRACKER.md`** (gauges), **`docs/DOCS-INDEX.md`** (currency). **§18 above carries the connection content directly.** |
| **S-12** | **"scenario 2 (ground multi-weapon) = GAP"** and **"scenario 8 (ground ammo) = GAP"** | **Both closed since.** Row 2 is HANDLED (W2); row 8 is PARTIAL (whole-unit ammo gate). *(§8.)* |
| **S-13** | **the `GroundWeaponMount.Range_m` docstring: "ADDITIVE + UNREAD by the resolver… the range gate flips to this in Slice 2."** | **False since K3.** A **stale comment in live source**, and it points at a doc this file replaces. **Fix it in the code.** |
| **S-14** | **"`ResolveRegionCombat` at `:266`" / "`BuildFireMix` at `:1139`" / "`StepEngagementGroup` at `:622`" / "`ApplyCasualties` at `:860`" / "`ShouldRetreat` at `:1455`"** etc. | **Pervasive line drift** — see below. |

| **S-15** | **"Range is a scalar per group"** / **"the whole thing collapses back to today's 1-D tug-of-war — which is exactly the byte-identical path"** / **"only the faster (controller) fleet moves"** — the scalar-closing and frame-and-anchor design, live text in three separate docs. | ⛔ **SUPERSEDED 2026-07-29 by LD-30.** A battle is an **arena bounded by weapon range**, and the fight is **units moving inside it at their own speeds**. The collapse-to-1-D that was written down as the *safety property* is, under the arena, the *defect*. All three are kept in §13 as **as-built**, marked, because the code still does them. |
| **S-16** | **"2D / flanking / facing — permanently parked."** | ⛔ **Superseded twice** — by shipped code (S-1) and then decisively by LD-30. The no-RTS principle governs the **player's control surface**, not the simulation's fidelity. §17. |
| **S-17** | **My own §13.9 first draft: "engagements form out to a gigameter, three orders of magnitude past any weapon."** | ⛔ **WRONG for a real game, corrected in place 2026-07-29.** `EngagementRange_m = 1e9` is a **coarse broad-phase pre-filter**; the real gate is `WithinWeaponRange` (`:218`), **ON in the client**, and `EnsureInCombat` seeds from the **real distance**. A frozen pair is stuck at *weapon-range* scale. Recorded because the overstatement was mine and a reader would have believed it. |

### 19.1 THE LINE-DRIFT WARNING

> **Inherited from the teardown and now doubly true:** *"Line numbers are as-of that date — re-verify before
> editing."*

Drift between the source docs (2026-07-08 → 2026-07-22) and HEAD was **pervasive, not occasional.** A sample of what
moved: `ResolveRegionCombat` **:266 → :370** · `StepEngagementGroup` **:622 → :640** · `BuildFireMix`
**:1139 → :1317** · `ApplyCasualties` **:860 → :878** · `ShouldRetreat` **:1455 → :1635** · `AdvanceClosing`
**:1009 → :1028** · `SeparationOf` **:975 → :1000** · the ground fire gate **:307/:312 → `WeaponReaches` :561-569** ·
`ApplyEngagementManeuvers` **:408 → :732** · the `WasInBattle` latch **:234 → :331** · `RemoveAll(dead)`
**:237/:242 → :334** · the nested faction loops **:297-379 → :443-529** · the space `1/split` **:727-729 → :745,:747**
· `HitFraction` **:147 → :171** · `ArmourSoak` **:245 → :269** · `CombatEngagement.cs` grew **~1827 → 2006 lines**;
`CombatKernel.cs` **291 → 314**.

**Every file:line in THIS document was checked on 2026-07-29. It will drift too. The source is the gauge; this file
is the map.**

---

## 20. THE DELETION MAP — where every section of the eight replaced docs went

Nothing was lost. Each row is a deleted file; the right column says where its content lives now.

| Deleted file | Its content → where it is now |
|---|---|
| **`docs/combat/RESOLVER-DESIGN.md`** (362) | §A1 data flow → **§6.1** · §A2 the input surface (the 10-field `WeaponProfile` + per-ship + the constants) → **§6.2/§6.4** · §A3/§A7c the dial-insertion maps → **`docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`** (the designer owns dials) + the resolver-side landing points here in **§6.2** · §A4/§A7d the extension backlogs (Penetration, PerShotEnergy, ammo drain, recoil→tracking, heat throttle, PD intercept, inertialess, reactionless — all BUILT) → **§6.2, §17** (the still-⚙ ones) · §A5/§A7e "what the map shows" → **§6** · §A6 scale & composition → **§10 row 10, §12.4** · **§B1 the honest reality (the six-concept ship-vs-ground table) → §10** · §B2 the neutral `Combatant` view → **§2 + §16 O-1** · §B3 `ResolveSalvo` → **§16 O-4** (designed, deliberately not built) · §B4 planetary specifics → **§12** · **§B5 the slice sequence → §14.1** · §B6 risks/invariants → **§12.5** · **§B7 THE NORTH STAR → §11** · §B7.1 vocabulary + compute-one-distribute-across-N → **§11.1/§11.2** · §B7.2 the 5c bucket build plan → **§12.4** |
| **`docs/combat/AUTO-RESOLVER-TEARDOWN.md`** (266) | §1 the big picture + the two-resolvers table + the naming trap → **§1, §2, §3** · §2 the component-by-component teardown → **§6, §7** · **§3 the scenario-correctness matrix → §8** (re-stamped; 3 rows moved) · **§4 the 6-man squad + Fix A/B/C → §9** · **§5 the unified-resolver gaps → §10** · **§6 battle statistics → §15** · §7 the recommended teardown order → **§7** (the field manual is the same reading order) · 🔴 its §7 caution pointing at the **retired** `SYSTEMS-STATUS-AND-TEST-PLAN.md` → **repointed, §19 S-11** |
| **`docs/combat/UNIFIED-RESOLVER-AND-BATTLE-STATS.md`** (201) | Part A the honest framing ("two resolvers sharing one damage kernel") → **§1, §2** · the acid test + the shared `WeaponReaches` slice + **both reach-0 conventions** → **§14.2 slice 1, §12.2** · the two corrections (pitch cancels; divide-by-nothing) → **§12.2** · Part B the ledger / `CasualtyTier` / the report records → **§15.1** · the squad-path honesty (`ModelCount` doesn't exist; the deliberate seam; banker's rounding) → **§9.3, §15** · the mixes-units-and-men caveat → **§15.2** · ground retreat as the biggest new piece → **§10 row 5, §14.2 slice 8** · wounded recovery + `MedicalAtb` → **§15.3** · Part C the nine slices + the build-state banner → **§14.2** · **the per-tick-vs-per-second `dt` trap → §10 row 8 + §16 O-3** · the CANON banner (regions are a visual aid) → **the header of this file** · 🔴 its "Slice 7 unbuilt" claim → **corrected, §19 S-2** |
| **`docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md`** (435) | §1 the principle + the developer's rule verbatim → **§12.1** · §2 the metres↔hex layer + the worked example + the scale note → **§12.2** · §3 the resolver as a real closing fight → **§12.3 slice 3** · §4 sub-hex continuous positions → **§12.3** · §5 the five-slice plan → **§12.3** · §6 marine-vs-zerglings → **§12.1/§12.2** (the acceptance story, condensed) · §7 connections → **§18** · the 2026-07-27 status correction (slice 2 shipped under `EnableMiniHexCombat`, bypassing the seam) → **§12.3 + §19 S-8** |
| **`docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md`** (319) | **§0 the one decision (standoff vs brawl) + the 4X-not-RTS statement → §14.4** · §1 "where we are" → **§19 S-6** (a stale snapshot, recorded as such) · §2 the vision → **§11** · **§3 the nine locked decisions → §5 LD-18/LD-20** (with #2 marked SUPERSEDED) · **§3 parking lot → §17** · §4 ROOT A/B + PHASE 1–6 + the live-visibility layer → **§14.4** · **PHASE 4's "why paused" ruling + its 5-step plan → §14.4** · §5 the connection map → **§18** · §6 the cradle-to-grave acceptance test → **§18** |
| **`docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md`** (225) | §1 why it exists + *"the 2D issue is the biggest issue"* → **§13.1** · **§2 position-lives-at-the-group (L-1) → §13.2** · §3 the substrate + the three invariants → **§13.3** · §4 the frozen frame + doctrine offsets → **§13.3** · §5 the range + bearing rules + `RoleGeometry` → **§13.3** · §6 how it stays cheap → **§13.2** · §7 determinism → **§13.3, §13.8** · §8 one module two domains → **§13.3** · **§9 combined battles / Endor → §13.4** · §10 the scenario battery → **§8, §13.4** · **§11 the two joints → §13.7/§13.8, and re-stamped CLOSED** · §12 what we rejected + the Station-Lattice escape hatch → **§13.5** · §13 the S-slices → **§13.5** · **§14 the eight known weaknesses → §13.6** · §15 connections → **§18** |
| **`docs/combat/RESOLVER-2D-JOINTS.md`** (232) | the firemain/watch-bill framing → **§13.7/§13.8** · **Joint #1 the trap, the ledger, `AllocateFire`, the residual proof, the weight function, the FLAGGED values, the 3-way worked example, cross-plane conservation → §13.7** · **Joint #2 the mechanism, the PINNED ownership decision, the four beats, the fixed-quantum proof, the three rules, the mid-fight-switch subtlety → §13.8** · §3 what each joint hands its slice → **§13.5** · §4 connections → **§18** · §5 the executable-spec test coverage → **§13.8** |
| **`docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md`** (147) | §1 the problem in one picture → **§12.4** · **§2 W1 / W2 / W3 / W4 / W1b + the as-built notes + the developer's calibration rule → §12.4** · **§3 the six invariants → §12.5** · §4 the build order → **§12.4** · the `RealRange_m` sibling → **§12.4** |

### 20.1 What was deliberately NOT carried over

- **The `docs/economy/COMPONENT-DESIGNER-DIALS.md` cross-references** from the dial-insertion map — dials are the
  **designer's** subject and live in `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`. This file records only where
  a dial **lands on the resolver's input surface** (§6.2).
- **The provenance/how-we-got-here paragraphs** of each source doc (which workflow produced them, which agent hit a
  session limit). Superseded by this file's own provenance line in the header.
- **Duplicated restatements of the shared kernel math** — five of the eight docs each described `HitFraction` and
  `ArmourSoak` in their own words. **One statement, §6.3.**
