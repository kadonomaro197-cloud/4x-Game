# Unified Resolver + Battle Statistics — simplified solutions

This doc merges the original design with a line-by-line source verification. Every correction from that check is applied. The honest headline up front: **the damage math is genuinely one shared engine today; almost everything else the word "unified" implies is still two separate pieces of code that we can make agree.** Where the original design oversold something, this version says so and reclassifies the effort.

---

## Part A — The range-aware resolver: what's built, the one rule to share, the minimal change

### First, the honest framing

There is **one damage engine** shared by ships and ground units — that part is real and solid. There are **two resolvers** around it (one for space, one for ground) that each do their own range-checking, closing, movement, and target selection. So the accurate description is *"two resolvers sharing one damage kernel,"* not *"one unified resolver."* Slice 1 below unifies a single one-line range test between them — a real and worthwhile merge, but a one-line predicate, not the whole resolver.

### What already works (do not rebuild)

**One damage engine, shared by both.** Ships and ground units already push every shot through a single pure-math file, `Combat/CombatKernel.cs`. The hit chance (`HitFraction`, `CombatKernel.cs:147`), the shield drain, and the armour soak (`ArmourSoak`, `CombatKernel.cs:224`) are one implementation. The ground side runs every attacker's shot through that same kernel (`GroundForcesProcessor.cs:340` builds the profile, `:347` calls `HitFraction`, `:368` calls `ArmourSoak`). So "how hard does a hit land" is genuinely one rule everywhere. Done.

**The acid test already passes on the ground — today, with no flag.** In `GroundForcesProcessor.cs:307` the resolver checks, for each attacker `u` against each target `t`:

> `if (HexDist(u, t) > u.Range) continue;`

Plain English: if the target is farther than this unit can reach, this unit adds nothing to the incoming fire this tick. The artillery unit reaches 3 hexes (`GroundRangeTools.cs:26`); a melee unit reaches 1 (the default, `GroundRangeTools.cs:27`). So the ranged unit scores on a closing melee unit every tick, and the melee unit contributes zero until it's adjacent. Once adjacent, melee is undodgeable (its tracking is 1.0, `GroundCombatant.cs:75`). The rules-of-engagement step even walks idle units one hex closer or farther each tick (`ApplyEngagementManeuvers`, `GroundForcesProcessor.cs:432-446`). **This is exactly "two units charging, the ranged one is the only one that scores until the gap closes." It is live.**

**The same behavior exists on the space side, but behind a switch.** `CombatEngagement.cs:1169` gates each weapon inside the per-weapon `BuildFireMix` loop (`:1164`):

> `if (separation_m > 0 && w.Range_m > 0 && w.Range_m < separation_m) continue;`

A weapon that can't reach the current gap fires nothing. The gap shrinks each step in `AdvanceClosing` (`CombatEngagement.cs:1009-1017`). But this only bites when `EnableClosingRange` is on; with it off, `SeparationOf` returns 0 (`CombatEngagement.cs:975`) and the gate does nothing. Every headless test fixture runs with it off.

### The honest problem: same idea, two copies

The range rule is **conceptually identical but physically duplicated.** Ground gates the *whole unit* by an integer *hex* count. Space gates *each weapon* by a *metric* range in metres. Same concept, two pieces of code, two units of measure. Change the rule in one place and the other doesn't follow.

### The one change — a single shared range function

Add one function to the shared kernel that both sides call:

```
CombatKernel.WeaponReaches(WeaponProfile w, double separation_m)
    = w.Range_m <= 0 || w.Range_m >= separation_m;
```

This is the space gate (`CombatEngagement.cs:1169`) lifted verbatim into the shared home. Space calls it with its existing separation. Ground calls it instead of comparing raw hexes.

**Two corrections the original design got wrong here — read before building:**

1. **You cannot get both "byte-identical" and a "free 1-hex-on-Earth ≠ 1-hex-on-Io bonus." Pick one.** `GroundCombatant.ToWeaponProfile` *derives* the weapon's metric range as `unit.Range (hexes) × hex pitch` (`GroundCombatant.cs:69`). If the gate then also computes `separation_m = HexDist × (the same pitch)`, the pitch **cancels out** and `WeaponReaches` collapses back to exactly `HexDist ≤ u.Range` — byte-identical, *and no per-body effect at all.* To actually make a hex on a big body differ from a hex on a small one, you'd have to give the weapon an **intrinsic metric range independent of hex count** — which is *not* byte-identical and would re-baseline `GroundForcesTests`. The "free bonus" claim was wrong. And note the premise was only half-true anyway: `ToWeaponProfile` currently uses a **nominal 1000 m pitch** (`GroundCombatant.cs:50`), not the real per-body value — so ground doesn't yet "already speak real metres."

2. **Routing ground through metres opens a divide-by-nothing hole.** `GroundRangeTools.HexPitchKm` returns `0.0` when a region has no hex geometry/area (`GroundRangeTools.cs:36`). Feed that in and `separation_m = 0`, which makes `WeaponReaches` return true for *every* unit regardless of distance — every unit is suddenly "in range." The current pure-hex compare has no such failure mode.

**So the safe, cheap version of Slice 1 is: keep ground comparing in hex-space, and have `WeaponReaches` be the one shared predicate that both domains route through — space in metres, ground in hexes — without claiming a per-body bonus and without a pitch-zero guard needed.** If you later *want* the per-body effect, that's a separate, non-byte-identical change with its own test re-baseline and a pitch-0 guard.

**Effort: small.** What stays correctly per-domain: the *movement board* underneath (hex A\* march for ground, metric fleet closing for space), target selection, and terrain. You share only the "can this weapon reach" test — not the board it moves on.

### Bolter-fires-at-range, chainsword-only-adjacent (each weapon resolves separately)

**Space already does this today.** A ship's weapons are a *list*, and `BuildFireMix` loops each weapon and applies the reach gate to each one independently (`CombatEngagement.cs:1164-1170`). A ship with a missile (long reach) and a beam (knife range) already fires the missile while closing and only adds the beam once adjacent. Bolter/chainsword, working, on ships.

**Ground can't yet — because a ground unit is a single weapon.** `GroundCombatant.ToWeaponProfile` builds *one* weapon profile per unit (`GroundCombatant.cs:66-85`), and the resolver gates the whole unit by one `u.Range`. One range, one nature; it can't hold a close weapon while firing a ranged one.

**The fix needs no kernel change.** The kernel already consumes `List<WeaponProfile>` and gates per weapon — that's what space uses. The work is entirely on the ground resolver: let a ground unit carry a *list* of weapon profiles instead of one, and have `ResolveRegionCombat` loop each mounted weapon through the shared `WeaponReaches` gate. Then a squad's bolter scores while closing and its chainsword only lands at contact — automatically. This lines up with the direction ground is already heading (units becoming real entities with a `ComponentInstancesDB`). **Effort: medium** — this is a genuine new mechanic, not a dormant additive.

---

## Part B — Battle statistics + wound state: the design

### How it works today (the starting point)

**Ground casualties are a silent delete.** The salvo in `GroundForcesProcessor.cs:261-389` runs a real per-attacker → per-target loop: each attacker `u` computes a `contribution` to each target `t` (`:342-371`). Then dead units just vanish: `forces.Units.RemoveAll(u => u.Health <= 0)` (`GroundForcesProcessor.cs:237`). No record of what killed it, or that it existed. A unit is one `Health` over one `MaxHealth` (`GroundForcesDB.cs:53-55`) — no model count, no wound state.

**But the ground resolver already knows who dealt every hit.** Because `:342-371` is a per-source loop, the resolver holds attacker → target → damage for every shot, including the attacker's weapon nature, penetration, and design id. Today it throws that away by summing into one scalar. **That is the cheap hook.**

**Space already has an after-action log to copy — but note what it is.** `BattleLog.cs` is a static, thread-safe ring buffer of `BattleEvent` records (`:27-57`) — engaged / salvo / retreat / disengaged, **per fleet** — read into a table by `BattleReportWindow.cs:44-84`, which auto-opens when a battle ends. It's a play-by-play, not a per-weapon tally, it's runtime-only (not saved), and critically it is **per-fleet by design** ("a battle has no single entity", `BattleLog.cs:24`). Keep that fact — it matters for Slice 4 below.

**Space kills give no per-shot attribution.** `ApplyCasualties` removes whole ships out of a damage pool (`CombatEngagement.cs:900-919`); it cannot know which shot did it. This is an honest limit of the aggregate model (Combat/CLAUDE.md gotcha #2), not a bug to fix.

**Retreat exists for fleets, not for ground.** Space has `FleetRetreatDB` and a retreat trigger. Ground has **no** withdraw order at all — the only ground stances are `HoldGround` / `CloseToEngage` / `StandOff` (`GroundForcesDB.cs:213`). A ground unit fights to death or the region flips. So "retreat" on the ground is itself a new piece.

### The core idea: a running damage ledger, totaled at battle-end

Don't try to catch the exact killing shot as it lands — the space engine can't, and forcing it would fight the model. Instead, **add up cumulative damage per victim, per enemy weapon, over the whole battle.** At battle-end (or at the moment a victim is destroyed — see the snapshot note), credit each loss to the enemy weapon that dealt it the *most* total damage (plurality = "the killing blow"). Deterministic, and it works the same for aggregate space and per-source ground.

**One shared record, living in `Combat/`, referencing no ship- or ground-specific type:**
- `WeaponKey` = { faction, weapon name, nature } — e.g. "Martian Directorate / Railgun (Kinetic)"
- `VictimSnapshot` = { name, side, max health, model count } captured *when the victim dies*, so a deleted victim is still nameable in the report.
- `BattleLedger` = per victim, a tally of cumulative damage by `WeaponKey`, plus that victim's snapshot.

One ledger per live battle. Runtime-only, like `BattleLog`.

### (1) Kill attribution — where each resolver tags the source

- **Ground (small — genuinely one free line):** inside the existing per-source loop where `contribution` from `u` to `t` is computed (`GroundForcesProcessor.cs:342-371`), add `ledger.Add(t, WeaponKey.For(u), contribution)`. Zero new math — the number is already there.
- **Space (medium):** when the pool destroys ships (`CombatEngagement.cs:900-919`), split the kill credit across the incoming fire mix *proportionally to each weapon class's damage share* (the fire mix already carries this; `DescribeFireMix`, `CombatEngagement.cs:466`, already prints it). **Snapshot the victim at the kill site (`:915`), not at battle-end** — the ship is already `Destroy()`'d by then. **Label this in the report as "proportional (auto-resolve)."** And be honest about its limit: fire is aggregated *by weapon class* (`:1150`), and in a fight with three or more fleets each attacker divides its fire across enemies (`1/enemyCount`), so in a multi-party battle the credit is proportional *and ambiguous across fleets* — it may not resolve to a specific enemy weapon instance or even a specific enemy fleet. That's a real limitation of the aggregate model, worth stating in the report itself, not just a footnote. In a 1v1 the "proportional" label is fully honest.

### (2) The wound tier — what's real vs. what waits on a field that doesn't exist yet

Add `CasualtyTier` to `CombatKernel.cs`. It returns `{ untouched, wounded, dead }` counts for any combatant. It has two branches:

- **Single unit (the real, working path — every ship and every current ground unit):** read the health band. Above ~95% health → untouched (1). Between 0 and ~95% → wounded (1). Removed / health ≤ 0 → dead (1), read from the ledger snapshot since the unit is already deleted. **This path is real today and does the whole job for monolithic units.**
- **Squad path (a stub against a field that does not exist yet):** convert health to models lost, then split `dead = round(modelsLost × (1 − WoundedFraction))`, the rest wounded, the untouched are the ones still up. `WoundedFraction ≈ 0.5` is the one balance knob — "half the men who go down are wounded, not killed."

**Be clear-eyed about the squad path.** It reads a `ModelCount` field, and **`ModelCount` does not exist anywhere in the codebase today.** The units-as-entities workflow (per `GroundCombat/CLAUDE.md`) adds a backing entity and a `ComponentInstancesDB` — *not* a model count. So the headline "one function for a 6-model squad AND a single tank" describes something half-buildable: the tank half works now; the squad half is dormant against a non-existent field. The design's safety hedge is honest — with no `ModelCount`, everything defaults to 1 and you get pure health-band behavior, byte-identical to today — but don't claim the squad readout ("2 dead / 1 wounded / 3 untouched") until some workflow actually adds `ModelCount`.

**Two more honest notes:** the branch is required precisely because the two models *disagree* — feed `ModelCount = 1` into the squad formula and a half-dead unit yields zero models lost → "untouched," while the band path calls the same unit "wounded." So "one function" is really two with a deliberate seam. And the report's per-side "Wounded" total sums a wounded 500-HP tank (1) and a wounded rifleman (1) into the same number — it mixes *units* and *men*. Fine for a casualty readout; not a rigorous manpower count. (Also: `Math.Round(0.5)` banker's-rounds to 0, so one downed model → 0 dead / 1 wounded — pick that rounding on purpose.)

**Effort: small** for the working monolith path; the squad path is free-but-dormant until `ModelCount` exists elsewhere.

### (3) The after-action report — battle-end, both sides, per enemy weapon

**Shared records (in `Combat/`, domain-neutral):**
- `BattleReport` = { when, battle name, victor, list of `SideReport` }
- `SideReport` = { faction, this side's own Untouched/Wounded/Killed totals, and a breakdown of losses by the *enemy* weapon that inflicted them }
- `WeaponTally` = { weapon, killed, wounded, untouched }

At battle-end: for each victim, compute its `CasualtyTier` and credit it to the enemy `WeaponKey` with the largest cumulative damage against it. Roll up per side.

**The report fires on ANY battle-end** — wipe, region capture, disengage, or a called retreat. This is the right call: retreat is *one* trigger, not the only one, so a fight that ends in annihilation or capture still produces stats. But the storage and trigger are harder than the original design admitted:

- **Space (medium-to-large — reclassified up):** the original plan said "emit at the same battle-end sites — reuses everything, medium." It doesn't reuse cleanly. The named sites (`EndEngagement`→`RecordBattleEvent` at `CombatEngagement.cs:829`, and `RecordRetreat` at `:1557`) are **per-fleet, per-state-change** events, and `BattleLog` is per-fleet by design. A `BattleReport` is **one per battle, covering both sides.** Emit at `:829` and you fire one report per fleet as each disengages — N reports per battle. To get a single correct report you must first **synthesize a battle identity** to hang one ledger and one report on, and add a real **"fewer than two hostile sides remain" whole-engagement-end trigger** with dedup — none of which exists today. That's the actual work, and it's why this is medium-to-large, not medium.
- **Ground (medium — and name the hook):** ground has no battle log and no explicit battle-end event. The real signal is the `WasInBattle` latch clearing (fighting → not-fighting) at `GroundForcesProcessor.cs:234`. Recommended: **generalize `BattleLog` to hold `BattleReport`s from both domains** (the record is domain-neutral, so this stays clean). Accumulate the ground ledger keyed by body + region, and emit when that latch clears. Define the ledger lifecycle explicitly: reset it when the latch clears, and treat a captured region that re-fights next tick as a fresh battle.

**Display (small-to-medium):** extend `BattleReportWindow.cs` with a second view listing, per battle, each side's Untouched/Wounded/Killed and the per-enemy-weapon tally — the same thin defensive draw as the existing `:44-84`.

### The retreat piece — genuinely new on the ground (a real build)

Fleet retreat is done (`FleetRetreatDB`). Ground retreat does not exist — there is no `GroundRetreatDB` and no withdraw stance (only `HoldGround`/`CloseToEngage`/`StandOff`, `GroundForcesDB.cs:213`). To honor "on a called retreat," add a ground withdraw stance/order that marches a formation out of the contested region (mirroring the space fighting-withdrawal posture), recorded by a `GroundRetreatDB` or per-formation flag that triggers the report. **This is the single biggest new piece. Effort: medium-large.** Until it ships, the ground report still works — just gate it on battle-end-by-capture-or-wipe (the `WasInBattle` latch), and accept that you can't yet *call* a retreat.

### (4) Wounded recovery — keep it simple

- **Squad models (once `ModelCount` exists):** wounded models heal back to untouched slowly (`WoundedRecoveryPerDay`) when the unit is on friendly-held ground and not in combat — reuse the friendly-ground check already in `GroundForces.ResupplyUnit` (`GroundForcesDB.cs:461-464`). Recovering a model restores its share of health. An optional Apothecary/medic building — a component carrying a new `MedicalAtb` (researched → built → located → bombable, exactly like `GroundDefenseAtb`) — speeds it up and can turn a fraction of would-be *dead* into *wounded* at the casualty roll ("a field hospital means fewer permanent losses").
- **Single unit:** a wounded-band unit does not self-heal in v1 — it stays degraded until a repair/medic building is present. Keeps the model honest without inventing ground repair wholesale.
- The recovery tick rides the existing hourly `GroundForcesProcessor` hotloop, beside environmental attrition (`GroundForcesProcessor.cs:143-158`). **Effort: medium** (small if you ship squad recovery only and defer the Apothecary).

### (5) How this generalizes to fleet + ground

`WeaponKey`, `VictimSnapshot`, `BattleLedger`, `CasualtyTier`, and `BattleReport`/`WeaponTally` all live in `Combat/` and reference no ship- or ground-specific type. Each resolver supplies only its own damage-with-source (ground: the free per-source loop; space: proportional from the fire mix, snapshotted at the kill site) and calls the report emit at its own battle-end. A combatant with `ModelCount = 1` falls through the health-band path — so everything is **byte-identical to today until squads, wounds, and the space battle-identity are actually wired**, the same safety property every combat slice here holds to.

---

## Part C — Cheapest-first build order

Each slice names its hook, its size (S/M/L), and is honest about *built vs. additive vs. real new mechanic*. Push one, wait for CI green, then stack the next.

**Slice 1 — `WeaponReaches` shared range gate. [S — additive]**
Add `CombatKernel.WeaponReaches(w, separation_m)` (lift `CombatEngagement.cs:1169`). Point space at it. Point ground at it **in hex-space** (`HexDist` vs `u.Range`) so it stays byte-identical and needs no pitch-zero guard. This makes the acid test a single shared kernel rule instead of two copies. *Do not claim a per-body "1 hex ≠ 1 hex" bonus here — that's a separate, non-byte-identical change with its own test re-baseline.*

**Slice 2 — `CasualtyTier` wound function. [S — additive]**
Add the killed/wounded/untouched band function to `CombatKernel.cs`. The monolith (health-band) path is the real, working one. The squad path is present but dormant — it reads `ModelCount`, which doesn't exist yet; defaults to 1 → identical to today. Nothing consumes it yet.

**Slice 3 — shared report records + ground attribution. [S–M — additive, one free line on ground]**
Add `WeaponKey` / `VictimSnapshot` / `BattleLedger` / `BattleReport` / `WeaponTally` in `Combat/`. Wire the one-line `ledger.Add` into the ground per-source loop (`GroundForcesProcessor.cs:342-371`) — free, the number's already there.

**Slice 4 — space report storage + trigger. [M–L — NOT a free reuse]**
Generalize `BattleLog.cs` to also hold `BattleReport`s. The hard part first: synthesize a **battle identity** and a **whole-engagement-end trigger** ("fewer than two hostile sides remain") with dedup — the existing `:829`/`:1557` sites are per-fleet and will otherwise emit N reports per battle. Snapshot victims at the kill site (`:915`). Split kill credit proportionally across the fire mix; label it "proportional (auto-resolve)" and note multi-party ambiguity.

**Slice 5 — ground battle log + report emit. [M — part reuse / part new]**
Reuse the now-shared `BattleReport` record. Accumulate per body+region, emit when the `WasInBattle` latch clears (`GroundForcesProcessor.cs:234`) — i.e. battle-end-by-capture-or-wipe. Define the ledger reset on that same transition. Ground battles now produce stats.

**Slice 6 — report UI. [S–M — reuses `BattleReportWindow.cs:44-84`]**
Add the per-side / per-enemy-weapon casualty sub-table beside the existing event table.

**Slice 7 — per-weapon ground resolution (bolter/chainsword on the ground). [M — real new mechanic]**
Grow the ground unit from one synthesized weapon (`GroundCombatant.cs:66-85`) to a weapon *list*; loop each weapon through `WeaponReaches` in `ResolveRegionCombat`. Kernel needs no change (space already does this). Now a ground squad fires ranged while closing and holds melee for contact.

**Slice 8 — ground retreat. [M–L — real new mechanic, the biggest piece]**
Add the ground withdraw stance/order + `GroundRetreatDB`, mirroring `FleetRetreatDB` (none exists today). This makes "on a called retreat" a real ground event; the report already fires on it once it exists.

**Slice 9 — wounded recovery. [M — small if squad-only]**
Recovery tick on the hourly hotloop (`GroundForcesProcessor.cs:143-158`), gated by the friendly-ground check (`GroundForcesDB.cs:461-464`). Ship squad-recovery first; defer the `MedicalAtb` Apothecary component to keep it smaller. (Squad recovery is itself gated on `ModelCount` existing.)

### Bottom line: what's built vs. what's new

- **Genuinely built and solid:** one shared damage kernel; the ranged-beats-closing acid test, which **passes on the ground today** (`GroundForcesProcessor.cs:307`, no flag) and on space behind a flag (`CombatEngagement.cs:1169`). Slice 1 just merges those into one shared predicate.
- **Additive and dormant until wired (safe, cheap):** the monolith wound tier (Slice 2), the shared report records, and the free ground per-source ledger line (Slice 3).
- **Real work the title's word "unified" tends to hide — treat these as builds, not "dormant additive":** the space report host needs a *battle identity + whole-engagement-end trigger* that doesn't exist (Slice 4, M–L); squad wounds need a `ModelCount` field that doesn't exist anywhere yet (Slice 2's squad half); ground per-weapon fire is a new mechanic (Slice 7); and ground retreat is a brand-new ground system (Slice 8, the biggest single piece).

In one sentence: **the damage core is one shared engine and the acid test already works — but "one unified resolver" is really two resolvers sharing that engine, and the battle-statistics half leans on two things that don't exist yet (a per-battle identity to host one report, and a squad model count) plus a genuinely new ground-retreat mechanic. Ship the truly-additive slices first; budget the rest as real builds.**
