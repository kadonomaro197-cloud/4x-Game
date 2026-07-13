# Fleet Combat — Closing Distance & Rules of Engagement (the range spine)

*Design + phased build plan, drafted 2026-06-27 from the closing-distance design conversation. This is the
blueprint for turning the auto-resolve engine from an instant strength-compare into a **closing fight where range,
speed, detection, and doctrine decide who can hit whom** — without ever becoming an RTS. Read `docs/COMBAT-DESIGN.md`
(the eleven-system master) and `docs/WEAPONS-DESIGN.md` (the weapon triangle + dodge) first; this is the
detailed spec for the "weapon range → engagement" rungs of that spine.*

---

## 0. The one decision this whole tree serves

**Standoff vs. brawl.** Do I build a fast, long-range artillery fleet that opens fire first and kites — never
letting the enemy's big short-range guns reach me? Or a brawler that eats long-range fire on the way in to reach
knife range where it dominates? Every phase below exists to make *that* decision real and stacking (ship design ×
fleet composition × doctrine × detection × speed). **The test for any feature here: does it sharpen that decision,
or is it fidelity the player can't act on?** If the latter, it goes in the parking lot.

This is a **4X, not an RTS.** The player never flies a ship. The player is the admiral: they write the *playbook*
(doctrine / rules of engagement), and the math fights the battle. All battles are deterministic auto-resolved
simulations; the player's only lever is doctrine, set before and (at decision points) during the fight.

---

## 1. Where we are (current combat, accurately)

| Piece | State today |
|---|---|
| **Trigger** | `CombatEngagement.Tick` engages any two hostile fleets within `EngagementRange_m` (a **1×10⁹ m stub**) — **proximity-triggered, automatic.** |
| **Resolve** | `StepEngagementGroup(members, dt)` aggregates firepower **by weapon class**, applies the dodge (`HitFraction`), pools damage × dt × `SalvoDamageScale`. **Once engaged, ALL weapons fire — range is not gated.** |
| **Ship values** | `ShipCombatValueDB`: Firepower, Toughness, Evasion, `List<WeaponProfile>`. |
| **WeaponProfile** | Class, DamagePerSecond, Velocity, Tracking, Saturation — **no Range field.** |
| **Doctrine** | `FleetDoctrineDB`: firepower/toughness/speed multipliers + posture + retreat threshold. Per-fleet AND per-sub-fleet (components). |
| **EMCON** | `FleetEmconDB`: Full/Cruise/Silent signature posture. |
| **Detection** | per-target signal vs. threshold; **`SensorTools.DetectionRangeAgainst` / `SelfDetectionRange_m` (built 2026-06-27)** give a real detection range; `RequireDetectionToEngage` gates engagement on detection (fog/first-strike). |
| **Movement** | warp + Newtonian; fuel/Δv readouts (built 2026-06-27). **No fleet-speed aggregation feeds combat.** |
| **Retreat** | `FleetRetreatDB` — break off at a loss threshold. |

**The gap in one line:** the engine decides *who wins* by strength math, but not *who can reach whom* — there is no
distance, no closing, no range-gating of weapons, and the battle triggers on mere proximity rather than a shot.

---

## 2. Where we're going (the vision, one paragraph)

Two fleets meet at a real distance. Each **sub-fleet** (component) has its own range to the enemy that **closes
over time** per its doctrine intent × its (slowest-unit) speed — so a fast fighter wing reaches knife range while
the capitals are still trading artillery from the back, and the two groups take **different fire**. A weapon fires
only if its **true range** reaches the current gap. The faster side **dictates the range** (kites or forces the
merge). A battle only **erupts when a shot is fired** — weapons-hold fleets can sit in tense standoff. Kiting is
real but **counterable** (fuel/ammo clocks, interceptors, the map). The player controls none of it directly — they
author the **Rules of Engagement** (the grown-up doctrine) and read a **battle readout that is the agency surface**.
Fast-forward and watch give the **identical** result for fixed doctrine; watching matters because you can change
doctrine at **decision points**.

---

## 3. Decisions already locked (the ledger) + the firewalls

These came out of the design conversation and are **settled** — build to them, don't relitigate:

1. **Doctrine-only control.** The player sets doctrine/ROE; the math fights. No per-ship control, ever. (This is
   what kills the RTS/2D temptation at the root — there's no control surface that demands geometry.)
2. **Range is a scalar per group**, not a position. Each sub-fleet has a single **range-to-the-enemy** that closes
   per doctrine × speed. **No 2D. No facing. No flanking.** (Parking lot, explicitly.)
3. **Determinism.** Combat is a pure function of (forces, doctrine, start distance, seeded RNG). **Fast-forward ==
   watch**, for fixed doctrine. Watching only matters because you can intervene at decision points.
4. **First shot makes the battle.** Proximity ≠ combat. A battle erupts only on weapons-release (per ROE). Two
   weapons-hold fleets in range = a tense standoff, not a fight.
5. **True weapon ranges are respected** with a **continuous** closing range (not discrete bands) — the real range
   number is the gate.
6. **Detail intel comes from dedicated scanners, not from closing.** Combat detection is "do I have a track" (coarse
   vs. fine), not a continuous resolution curve — so it does **not** depend on the broken `SignalQuality`. A coarse
   track = "shoot with a penalty and pray."
7. **Rules of Engagement is the grown-up `FleetDoctrineDB`** — NOT a new parallel system (CONVENTIONS §6). It carries
   trigger + closing intent + target priority + break-off + band.
8. **The battle readout is the agency surface**, co-equal with the sim — "lose agency, lose the player."
9. **Post-battle form-up** reconciles combat-space with the map: survivors re-coalesce to a map point over a timer;
   a scattered fleet forms up slower (fragmentation's natural cost).

**Parking lot (written OUT — do not build until promoted):** 2D positioning / flanking / facing; environmental
hazard fields (energetic-particle bands that wreck sensors); crew **provisions** as a ship endurance variable;
player-authored fine-grained target priorities (the v1 default targeting is the triangle).

---

## 4. The build tree — roots first, each rooted before the next

Each phase is a **working, gauged slice**. The rule (the whole repo's method): **a phase isn't done until its gauge
is green and you can SEE it.** Engine gauges are CI-tested; client/readout gauges are `SessionLog` lines verified on
the developer's build. Don't start a phase until the one below it is rooted.

> **Cross-cutting from day one — the readout.** Each phase ships the slice of the battle readout needed to *observe
> its own gauge*. The readout is not a final phase bolted on; it grows rung by rung, and Phase 6 is its
> consolidation. You cannot believe Phase 1's kiting works if you can't watch the range close.
>
> **Live visibility BUILT (2026-06-27) — the closing model narrates itself.** Since CI can't run the closing fight,
> the engine writes `[Combat]` lines (gated on `NarrateToLog`, which the client turns on) so a play-test log shows it
> move: a **per-step closing line** per fleet (gap / IN-or-OUT-of-RANGE / reach / maneuver reserve), a **WEAPONS
> RELEASE** line when the first-shot rule breaks a standoff, and a **maneuver-reserve-spent** line when a kiter burns
> out. Plus `CombatEngagement.DumpActiveCombat(system)` — an on-demand snapshot of every active engagement (DevTools
> "Dump Combat" button). DevTools also has the **toggles** to turn closing / first-shot on, and **"Spawn Combat
> Scenario"** to stand up a ready fight (`CombatSandbox.SpawnCombatScenario`). This is how the developer's play-test
> data lets us troubleshoot what CI is blind to.

---

### ROOT A — Weapon range on the combat profile ✅ BUILT (2026-06-27)
*The data everything else reads. Nothing closes until a weapon knows how far it reaches.*

> **Status:** `WeaponProfile.Range_m` added (convention: **0 = unbounded**, same as beam's `IsInRange` — serialization-
> safe, no Infinity in JSON). Beams carry their design `MaxRange`; railgun/flak/missile default to 0 (rangeless) —
> their own range fields are the immediate follow-up (flagged in `ShipCombatValueDB.Calculate`). Gauged by
> `FleetAggregationTests.WeaponProfile_CarriesDesignRange_FirepowerUnchanged` (beam range > 0; Firepower identity
> unchanged; railgun = 0).

- **Design:** `WeaponProfile` gains a `Range_m`. Fed from the real weapon designs — beam `MaxRange` (exists),
  railgun/flak ranges, missile range (still a stub — flagged; a rangeless weapon = "always in range" until built).
- **Build:** add the field; populate it where `ShipCombatValueDB` builds profiles from components; default rangeless
  weapons to ∞ (unchanged behavior) so nothing regresses.
- **Gauge (CI):** a beam ship's profile carries its design `MaxRange`; a railgun/flak ship carries theirs; sum/identity
  unchanged. (Extends `WeaponProfileTests`.)
- **Unlocks:** range-banded firepower (everything).

### ROOT B — Fleet capability aggregation ✅ BUILT (2026-06-27)
*Pure read-models over data that already exists. No behavior change yet — just the numbers the closing model will read.*

> **Status:** `GameEngine/Combat/FleetCombat.cs` — `WarpSpeedFloor` / `DeltaVFloor` (min over ships = the fleet moves
> as one), `FirepowerAtRange(R)` (sum of weapons whose `Range_m` reaches R — the firepower-vs-range curve), `SensorReach`
> (max over ships — parallel, not summed), `Ships(fleet)` (the tree walk). Gauged by `FleetAggregationTests`
> (`FirepowerAtRange_DropsFiniteRangeWeapons_AsTheGapGrows`, `Floors_TakeTheSlowest_SensorReach_TakesTheBest`).

- **Design:** Fleet **speed** = min(unit max speeds); fleet **endurance/Δv** = min(unit Δv) — the slowest, shortest-
  legged unit constrains the fleet (it moves as one icon). Fleet **firepower-vs-range curve** = `firepower(R)` = sum
  of every weapon profile with `Range_m ≥ R`. Fleet **sensor envelope** = max over ships of `DetectionRangeAgainst`
  (parallel, not additive — two identical sensors are redundant; diverse ones are complementary).
- **Build:** accessors only (`FleetCombat.SpeedFloor`, `FleetCombat.FirepowerAtRange(R)`, `FleetCombat.SensorReach`).
  Mirror the `WeaponUtils`/`SensorTools` accessor pattern — engine-side, CI-covered.
- **Gauge (CI):** a mixed fleet's speed = its slowest ship; `FirepowerAtRange` is full at 0 and drops as R passes each
  weapon's range; sensor reach = the longest-ranged sensor's, not the sum.
- **Unlocks:** the closing resolve; the readout's range curve.

---

### PHASE 1 — Single-range closing (prove the standoff decision is fun) ✅ BUILT (2026-06-27)
*One range per fleet-pair (NOT yet per-sub-fleet). The cheapest model that proves the core decision before we add richness.*

> **Status:** behind `CombatEngagement.EnableClosingRange` (default OFF → every existing fixture byte-identical).
> `FleetCombatStateDB.Separation_m` is the gap (seeded from real distance at `StartEngagement`); `BuildFireMix` gates
> each weapon on `Range_m ≥ gap`; `AdvanceClosing` moves the gap toward the FASTER side's preferred range (controller =
> max maneuver = min-evasion-over-ships; desired = longest finite weapon range). Deterministic (pure dt arithmetic).
> Tunables: `ClosingSpeedScale_mps` (0 = freeze), `InitialSeparationDefault_m`. Gauged by `ClosingTests`: the range
> gate (100-km fleet hits across a 50-km gap, 1-km fleet deals zero — kited), determinism, flag-off identity, and the
> faster side dictating (fast+long OPENS the gap/kites, fast+short CLOSES it). **Live calibration of the closing rate +
> the "is it fun" gut-check are the developer's play-test.**

- **Design to lock:** initial separation = seeded from **real map distance** at contact (Decision 3/4 input). Closing
  rate = f(both sides' doctrine intent: close/hold/kite) × **relative fleet speed** — the **faster side dictates the
  range**. Weapons gate on `Range_m ≥ R` (Root A/B). Determinism: integrate the close over fine internal `dt` inside
  the triggering tick, seeded RNG, no wall-clock.
- **Build:** add `Separation_m` to the engagement state; in `StepEngagementGroup`, sum only the firepower buckets with
  `Range_m ≥ Separation_m`; advance `Separation_m` each step by the doctrine×speed closing rule.
- **Gauge (CI + readout):** (a) a long-range fleet vs. a *slower* short-range fleet **kites and wins taking ~zero
  damage** (range matters); (b) a *faster* brawler **closes and wins** (speed dictates range); (c) fast-forward
  result == stepped result (determinism). Readout: show `Separation_m` ticking down + which weapon classes are live.
- **Unlocks:** validates the whole premise. **If standoff-vs-brawl isn't fun here, stop and fix the math before
  building Phases 2–6 on top of it.**

### PHASE 2 — Kiting counters (so Phase 1 isn't a dominant-strategy generator) ✅ BUILT (2026-06-27, fuel tier)
*Phase 1 makes "fast + long range" potentially unbeatable. This roots the counters in the SAME pass — never ship the kite without its clock.*

> **Status (endurance tier 1 = fuel):** `FleetCombatStateDB.ManeuverBudget` (a combat-abstract Δv reserve, seeded
> from `FleetCombat.DeltaVFloor` at engagement start). In `AdvanceClosing` only a fleet with budget LEFT can be the
> controller, and the controller spends `ManeuverBurnRate × dt` each step — so a kiter that burns out stops dictating
> the range and the enemy closes. Gauged by `ClosingTests.Kiting_RunsOutOfBudget_TheEnemyCloses` (a short-budget kiter's
> gap collapses to the brawler's range once it runs dry). Interceptors are already emergent from P1's speed rule (the
> faster short-range fleet forces the merge — `Closing_FasterSideDictatesTheRange` case B). Munitions depletion +
> provisions (tiers 2–3) stay parked (canopy). Live calibration of `ManeuverBurnRate` is the developer's play-test.

- **Design to lock:** **Endurance tier 1 = FUEL** (already built): holding range = maneuvering = burning Δv; the
  resolver debits maneuver fuel during the close/kite, so a kiter has a clock and must eventually disengage or close.
  **Interceptors** are emergent from Phase 1's speed rule (a faster short-range group forces the merge) — verify, don't
  build. (Endurance tiers 2–3 — munitions depletion, provisions — are later phases / parking lot.)
- **Build:** a maneuver-fuel debit in the closing step; a "out of Δv → can no longer hold range" transition.
- **Gauge (CI):** a kiter with limited fuel is forced to close (or break off) before it can finish a tougher enemy; an
  interceptor (faster, short-range) runs down a kiter and forces the merge.
- **Unlocks:** the standoff decision has real counterplay — the triangle survives in the spatial layer.

### PHASE 3 — First-shot trigger + the standoff state (detect ≠ fire) ✅ BUILT (2026-06-27)
*Rework the trigger from proximity to weapons-release. Adds the tense cold-war state.*

> **Status:** `EngagementPosture` (WeaponsFree / WeaponsHold / ReturnFire) on `FleetDoctrineDB` (the first ROE knob —
> ROE grows here in P5, not a parallel system); `FleetDoctrine.PostureOf` / `SetEngagementPosture` (a direct call,
> works mid-battle; preserved across a doctrine switch). Behind `CombatEngagement.RequireWeaponsReleaseToEngage`
> (default OFF → default posture is WeaponsFree, so proximity engages exactly as before). When on, the engage pass
> skips a hostile pair if BOTH are non-WeaponsFree → tense standoff, no battle. Gauged by `WeaponsReleaseTests`
> (both weapons-hold = no engagement; one weapons-free = battle forms; flag-off = proximity still engages). Deterministic
> tiebreaker (no bluffing model) + the ReturnFire nuance are P5/ROE refinements.

- **Design to lock:** a battle erupts when a fleet **releases a shot** — which happens when its ROE is weapons-free
  AND an enemy is **detected** AND **in weapon range**. Two weapons-hold fleets in range = **no battle** (tense
  standoff). The first ROE knob: **weapons-free / weapons-hold / return-fire-only.** Deterministic tiebreaker for two
  cautious fleets: closing continues; whoever's range + ROE forces the issue fires first (no bluffing model).
- **Build:** replace the proximity auto-engage in `CombatEngagement.Tick` with a weapons-release check driven by the
  engagement posture; add the posture to the doctrine blob (seed of ROE).
- **Gauge (CI):** two hostile fleets in range, both weapons-hold → **no `FleetCombatStateDB`** forms; flip one to
  weapons-free → it fires first and the battle forms (composing detection × range × posture).
- **Unlocks:** brinkmanship/blockade/escort as real states; the ROE system's first knob; removes the "everything in a
  system instantly brawls" stub.

### PHASE 4 — Per-sub-fleet ranges (the fighter-wing scenario) ⏸ SPEC-READY, deliberately NOT built blind (2026-06-27)
*The structural upgrade that makes sub-fleet doctrine actually matter. Build only once Phases 1–3 are solid — this multiplies the state.*

> **Why P1–P3 landed but P4 paused here:** P1–P3 are *additive, flag-gated* changes — the gauge is pure math, so CI
> fully verifies them. **P4 is a resolver RESTRUCTURE**, and its payoff (the fighter wing eating flak while the
> capitals trade artillery from the back) is an *incoming-fire* behaviour whose correctness is a **feel** question CI
> can't answer — only the developer's play-test can. Landing that blind would risk the well-tested combat engine for a
> result I can't see. So P4 is taken to **implementation-ready** and left for a session with playtest in the loop.
>
> **The implementation, concretely (when we build it together):**
> 1. **Per-component gap.** Each component (sub-fleet node) needs its own `Separation_m`, not one per top fleet. Storage:
>    a `Dictionary<int,double>` (component-entity-id → gap) on the top fleet's `FleetCombatStateDB`, seeded + closed per
>    component. `CollectCombatShips` already recurses components tagging each ship with its component's doctrine mults —
>    extend `CombatShip` with a `Separation` and tag it there (the clean hook).
> 2. **Per-component closing.** `AdvanceClosing` runs per component: each closes per ITS own maneuver floor + desired
>    range, so a fast fighter component slides to knife range while the slow capital component holds back — naturally
>    producing the spread. Cohesion = a component closes to its doctrine band and HOLDS (don't let it fragment past).
> 3. **OUTGOING gate (the easy half):** `BuildFireMix` already takes a separation — make it per-ship via `CombatShip.Separation`
>    so each component fires only the weapons that reach ITS gap. Low risk, additive.
> 4. **INCOMING gate (the hard half — the actual restructure):** a defending ship takes an attacker's weapon only if it
>    reaches the DEFENDER's component gap. So the attacker's fire must be re-gated at each target component's separation,
>    and `ApplyCasualties` bucketed per target component. **This is the part that needs playtest** — it changes the damage
>    phase, and the bucket key gains the component gap.
> 5. **Default targeting (the triangle)** + **form-up** ride on top once the per-component structure exists.
>
> **Gauge to write:** a fighter sub-fleet (close doctrine) reaches flak range and TAKES flak while the capital sub-fleet
> (standoff) trades only artillery from the back — assert the two components take *different* damage profiles in one battle.

- **Design to lock:** each **sub-fleet (component)** carries its **own** range-to-enemy, closing per its **own**
  doctrine × its **own** (min-unit) speed — so the fighter wing closes fast, the capitals lag. Fire is **group-to-group**,
  range-gated (a group fires at an enemy group only if a weapon's range reaches *that* group). **Default target
  priority = the triangle** (flak→fast/light, artillery→capitals, etc.) — legible, no player config yet. **Cohesion =
  band-holding:** a group closes to its doctrine's band and **holds** (knife / mid / max-range), producing a stable
  spread, not runaway fragmentation. **Form-up** post-battle (Decision 9).
- **Build:** lift `Separation_m` from per-engagement to **per-group**; group-to-group fire loop (few groups → cheap,
  stays O(ships) via the existing class buckets); band-hold closing; the form-up timer.
- **Gauge (CI + readout):** a fighter sub-fleet (close doctrine) reaches flak range and **takes flak damage** while the
  capital sub-fleet (standoff doctrine) trades **only artillery** from the back — assert the two groups take
  *different* damage profiles in the same battle. Readout: per-group range + band.
- **Unlocks:** sub-fleet doctrine is now load-bearing (it was cosmetic); the full standoff/brawl picture.

### PHASE 5 — Rules of Engagement (the agency surface / "doctrinal dogma")
*Consolidate the knobs into ONE authored ruleset — the grown-up `FleetDoctrineDB`. This is the player's whole hand on the battle.*

- **Design to lock:** one per-fleet/per-component **ROE** carrying: **engagement trigger** (Phase 3), **closing intent +
  band** (Phase 4), **target priority** (default = triangle; player-set = parking lot), **break-off threshold** (exists).
  **Presets** (Aggressive / Defensive / Standoff / Screen) on top, granular knobs underneath (preset-vs-custom — agency
  without an exam). **Decision-points:** the clock offers an auto-pause at first contact, a group crossing into a new
  weapons band, and a group breaking — so fast-forward never silently costs a doctrine change you'd have made.
- **Build:** extend `FleetDoctrineDB` (don't fork it) with the ROE fields + presets; hook decision-points into the
  combat-interrupt mechanism (`MasterTimePulse` already fine-steps at engagement birth — extend the trigger set).
- **Gauge (CI + live):** changing ROE at a decision point flips an outcome a fixed ROE would lose; each preset behaves
  sensibly on the example fleets; a decision-point auto-pause fires at a band crossing.
- **Unlocks:** the player's full agency over auto-combat, in one legible place.

### PHASE 6 — The battle readout, consolidated (the keystone)
*The agency surface, finished. Each phase shipped its slice; this makes it a coherent picture.*

- **Design to lock:** the combat tab becomes a **range/closing view**: each group's band and current range-to-enemy,
  which weapon classes are live vs. out-of-range, **who is firing and who is holding and WHY** (out of range / weapons-
  hold / dark by choice / dry), the closing timeline, and the ROE levers inline at the decision points. (The "why
  isn't my group firing" readout is the OUT-OF-RANGE principle at fleet scale.)
- **Build:** client (CI-blind) — every new path drops a `SessionLog` line (the client gauge); mirror the existing
  combat-tab table + the range-ring patterns.
- **Gauge (live):** the play-test log + screen show, for a real battle, each group's range/band/fire-state and let the
  player change ROE at a pause — and the change takes effect. `[combat-readout]` / `[ROE]` lines name every action.
- **Unlocks:** "lose agency, lose the player" is honored — the player can read and steer any battle.

---

### Canopy — deferred, promote deliberately (NOT now)
- **Endurance tier 2 — munitions depletion** (a per-ship reserve drained over the fight; missiles run dry, beams don't).
- **Endurance tier 3 — provisions** (the one genuinely new ship sub-system: crew consumables → a fleet can't operate
  forever far from base).
- **Player-authored target priorities** (deeper fire-allocation agency on top of the triangle default).
- **Environmental hazard fields** (energetic-particle bands that wreck sensors / drag movement — lands on sensors +
  movement + combat at once; evocative, expensive, its own build).
- **2D / flanking / facing — permanently parked** unless the no-RTS principle is itself revisited.

---

## 5. The connection map (Prime Directive — what this touches)

- **Reads:** `ShipCombatValueDB` + `WeaponProfile` (Root A), fuel/Δv (Phase 2), `SensorTools.DetectionRange*` (Phase 3),
  `FleetDB` tree / components (Phase 4).
- **Changes:** `WeaponProfile` (+Range), `CombatEngagement` (separation + range-gated resolve + first-shot trigger),
  `FleetDoctrineDB` (grows into ROE), `MasterTimePulse` (decision-point pauses).
- **Shares state with:** EMCON (firing/closing lights you up — going loud to close; a Phase-2/3 interaction worth a
  test), retreat (`FleetRetreatDB` = the break-off rung of ROE).
- **Must not break:** `CombatPerformanceTests` (the resolve stays O(ships) — the range axis rides the existing class
  buckets); determinism (no wall-clock in the close).

## 6. The acceptance test for the whole tree (cradle to grave)

A capability is real only if the player can reach it through the whole chain. For this system: a **long-range gun**
(mineral → material → component → researched → installed) on a **fast hull** → fielded in a **fleet with a kiting
ROE** → at a **detected** enemy → that **opens fire first at range** the enemy can't answer → **wins or runs out of
fuel/ammo and must close** → and when the fleet is beaten, the **survivors form up** and the player **re-roles or
rebuilds**. Every rung is a phase above. A missing rung is a gap to fill or a written deferral — never a skip.
