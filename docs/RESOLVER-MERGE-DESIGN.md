# Resolver Merge — one combatant kernel for space AND planetary combat

**As of:** 2026-07-08 · the DECIDED 2026-07-06 merge (`Combat/CLAUDE.md`), designed before code. Goal: one bucketed salvo kernel both a **ship entity** and a **planetary unit** run through; delete the duplicate ground resolver (`GroundForcesProcessor.ResolveRegionCombat`). Blocking gate — *nothing else moves until space AND planetary combat both resolve end-to-end on the shared kernel* (developer, 2026-07-08).

## 1. The honest reality (why it's a reconciliation, not a move)

The ship resolver (`CombatEngagement`) and the ground resolver (`GroundForcesProcessor.ResolveRegionCombat`) implement the **same concepts differently**:

| Concept | Ship resolver | Ground resolver |
|---------|---------------|-----------------|
| Weapon | real `WeaponProfile` (Nature×Delivery, velocity, tracking, saturation, range_m) | `GroundWeaponMode` triangle (Attack, DamageType, hex Range) |
| Dodge | `HitFraction(weapon, evasion, sep)` → effective toughness | `GroundDamageMatrix.Matchup` (IsAimed × (1−evasion)) |
| Shield | depleting POOL (`FleetCombatStateDB.ShieldPool_J`, `SoakFractionOf` by nature) | innate % stat (`Shield/(Shield+150)`, weaker vs energy) |
| Armour | folded into Toughness (component HP + thickness, joules) | flat-per-source `ArmourSoak(defense, dmg)` |
| Casualty | WHOLE ship, bucketed by combat value (O(buckets)) | continuous `unit.Health` drain (per-unit) |
| Range | metric separation between fleets (closing model) | hex distance ≤ `unit.Range` (hexes) |

**The direction (developer's call): planetary ADOPTS the ship kernel.** The ground triangle "dissolves into weapon×armour matchups"; planetary units come to carry real `WeaponProfile`s (which the universal weapon designer already produces), run the same `HitFraction`/shield/armour math, and differ ONLY in **range/movement on the hex board** (+ terrain, + the air/altitude layer later). The **ship side stays byte-identical** (it already *is* the kernel).

## 2. The neutral COMBATANT view (the seam)

A struct/interface both a ship entity and a `GroundUnit` present to the kernel — nothing entity- or hex-specific leaks in:

```
struct Combatant {
  int    FactionId;
  double Health;        // current hit-points, joules-scale (ship Toughness | unit Health×k)
  double MaxHealth;
  double Evasion;       // 0..cap
  double ShieldPool;    // depleting shield (0 if none); ground unit seeds from its shield stat×capacity
  double ShieldRegen;
  List<WeaponProfile> Weapons;   // what it fires — SAME type both domains
  double Position_m;    // 1-D range coordinate: fleet separation (space) | hexDist×HexPitchKm×1000 (planet)
  // read-model back-refs the caller uses to apply results (ship entity id | GroundUnit ref)
}
```

The kernel never sees a hex or an `Entity` — only `Position_m` (metric) and `Weapons`. **"A 100 km weapon is 100 km on a surface too; hexes are just the board"** → planetary maps hex distance to metres via `GroundRangeTools.HexPitchKm(region)`.

## 3. The shared KERNEL (the pure salvo math)

`CombatKernel.ResolveSalvo(attackers, defenders, dt, ctx)` — the extracted ship math, made domain-neutral:

1. **Fire mix** — aggregate attackers' `Weapons` by class bucket (`BuildFireMix`), gated on `Range_m ≥ |Δposition|`.
2. **Landed fraction** — `LandedFraction(mix, defender.Evasion, separation)` = damage-weighted `HitFraction` (velocity/tracking/saturation/range term). *(the existing ship dodge curve, verbatim.)*
3. **Shield** — `ResolveShield` drains the defender's `ShieldPool` by the soakable fraction (`SoakFractionOf` by Nature); regen toward capacity.
4. **Armour** — `ArmourSoak(defender.armour, perSourceDamage)` flat-per-source *(the existing `GroundDamageMatrix.ArmourSoak`, promoted to the shared kernel — the space side gains real armour-per-source instead of only folding it into toughness; verify byte-identity or gate behind the closing flag).*
5. **Apply** — drain `defender.Health`; caller removes it (ship: whole-ship bucket; unit: Health≤0). `× SalvoDamageScale`.
6. **Bucketing** — defenders keyed by `(evasion, health, shield, armour, doctrineMult)` → O(buckets) for **both** ships AND planetary units *(this is what fixes the ground O(units²) loop — §6 of `AUTO-RESOLVER-ANATOMY.md`).*

The kernel is **pure** (no entity mutation, no RNG) — deterministic (fast-forward == watch, the locked rule).

## 4. Planetary specifics (what stays different — range & movement on the hex board)

Everything above is shared. Planetary combat adds, in its CALLER (a slimmed `ResolveRegionCombat`), only:

- **Range in metres from hexes:** a unit's weapon reaches `Range_m`; its target's `Position_m` = `hexDistance × HexPitchKm(region) × 1000`. So the SAME weapon-range gate as space, on the hex board.
- **Movement changes range AS THE FIGHT PROGRESSES** (the developer's core point): each tick, units advance/kite on the hex grid by **move speed** (the existing `GroundMobility` + hex pathing/ROE — `ApplyEngagementManeuvers` already steps units toward/away by one hex), so `Position_m` shifts and who-can-hit-whom evolves salvo to salvo. This is the ground twin of the space **closing model** — same idea (range decided by speed × firepower), different board.
- **Terrain × fortification × stance** — kept as multipliers on the kernel's inputs (cover/fort divide incoming; stance scales attack/damage-taken) exactly as today.
- **The type-triangle DISSOLVES** — Armor▸Infantry▸Artillery becomes emergent from weapon Nature × target armour/evasion (a tank = heavy armour + kinetic gun; artillery = long range + splash), so `AvgTriangleVs` retires.

## 5. Slice sequence (each additive, CI-GREEN before the next)

1. **Kernel skeleton (additive, byte-identical). ✅ LANDED 2026-07-08.** New `Combat/CombatKernel.cs`: the neutral `Combatant` view + the pure math (`HitFraction`, `SoakFractionOf`, `ResolveShield`, `ArmourSoak`, `LandedFraction`, `ShieldSoakFraction`) written byte-for-byte from the live ship math (`CombatEngagement`) + ground math (`GroundDamageMatrix`). **Realized as PURELY ADDITIVE, not "moved + shim":** the kernel is a new home for the arithmetic and is *unwired* this slice — nothing calls it yet, so ship + ground behaviour is byte-identical because nothing existing was touched. The duplication is deliberate and lives for exactly one slice; **`CombatKernelTests`** both PINS the kernel outputs to hand-computed values AND CROSS-CHECKS them against the live `CombatEngagement`/`GroundDamageMatrix` functions over an input sweep, so any drift between the two copies goes red. Slice 2 routes the ship resolver through the kernel and DELETES the ship copies (the ship fixtures become the byte-identity tripwire), which is where the "move + remove duplication" actually happens. **Gauge:** every existing combat + ground fixture stays green (nothing rewired) + `CombatKernelTests` green.
2. **Ship routes through the kernel. ✅ LANDED 2026-07-08.** Realized as **delegation, not a monolithic `ResolveSalvo`**: the pure dodge/shield ARITHMETIC (`HitFraction`, `LandedFraction`, `SoakFractionOf`, `ResolveShield`, `ShieldSoakFraction`) now lives ONLY in `CombatKernel`; `CombatEngagement`'s same-named helpers became thin `=> CombatKernel.X(...)` delegators, its tuning constants (`VelocityReference_mps`, `SaturationReference`, `MinLandedFraction`, `FlightTimeReference_s`, the four `ShieldSoakVs*`) forward to the kernel's, and `RangeBaseMiss` is a forwarding property — so there is a single source of truth for the ship path and no drift is possible. The fleet/entity ORCHESTRATION (multi-fleet fire-division, per-fleet `DamageTakenPool`, the casualty bucketing in `ApplyCasualties`, closing, retreat, narration) stays in `CombatEngagement` — that's the ship-specific layer the planetary side will meet at the kernel in slice 3. **Why delegation over a `ResolveSalvo` rewrite:** delegation to identical code is provably byte-identical (zero number moves), whereas folding `ApplyCasualties`' bucketing + pool carry-over into a neutral entry point is the genuinely new interface that belongs in slice 3, where BOTH domains adopt it at once. **Gauge:** all ship fixtures stay green (`CombatPerformance`, `Dodge`, `Shield`, `Triangle`, `Stress`, `BattleSims`, `Multiparty`, weapon-triangle) — the byte-identity tripwire.
3. **Planetary side adopts the kernel — split into 3a/3b (the ledger, /scratchpad slice3-ledger, said only ONE piece is truly byte-identical today):**
   - **3a — flat armour (byte-identical). ✅ LANDED 2026-07-08.** `GroundDamageMatrix.ArmourSoak` + its two constants (`ArmourSoakPerPoint`, `ArmourMinPassFraction`) now delegate/forward to `CombatKernel` — the last piece of math that was already identical on both sides. Zero behaviour change; `GroundDamageMatrixTests` / `GroundForcesTests` / `GroundBombardmentTests` stay green. Collapses the ship↔ground armour duplication.
   - **3b-i — the BRIDGE (additive, byte-identical). ✅ LANDED 2026-07-08.** `GroundCombat/GroundCombatant.cs` maps a `GroundUnit` → `CombatKernel.Combatant`: its Attack/`GroundWeaponMode`/hex-Range become a real `WeaponProfile` whose velocity/tracking/saturation are chosen so the kernel REPRODUCES the ground dodge/shield semantics — ballistic ≈(1−evasion) dodgeable, artillery/melee undodgeable, energy dodgeable-but-shield-bleeds, kinetic fully-soaked. **Unwired** (ResolveRegionCombat doesn't call it yet → live combat byte-identical). `GroundKernelBridgeTests` PROVES the triangle/dodge/shield fall out of the kernel with sensible specs — the verification that de-risks the swap.
   - **3b-ii — the swap: TRIANGLE DISSOLVES. ✅ LANDED 2026-07-08 (developer chose "option 1: type edge lives in stats").** `ResolveRegionCombat` no longer multiplies the attack pool by `AvgTriangleVs` — the Armor▸Infantry▸Artillery type edge is no longer a flat ×1.5/×0.67 and now emerges from raw stats (attack/armour/HP) × weapon-nature vs the target's evasion/shield/armour, the ship way. `AvgTriangleVs` retired; `GroundTerrain.TriangleMult` kept as a readout/helper. **Zero test re-baseline needed** — every combat-outcome fixture is same-type (triangle was 1.0), and the triangle test checks only the `TriangleMult` helper; the cross-type numbers are gauged by `GroundResolverComparisonReadout` (CI-certified before/after: Armor→Inf 258→167, Inf→Armor 44.5→77.5, same-type unchanged). Dodge/shield stay on the existing `GroundDamageMatrix.Matchup` this slice (the 3b-i bridge proved the kernel reproduces them; routing dodge fully through `CombatKernel.HitFraction` lands in **slice 4** when its RANGE term goes live — doing it now, at separation 0, would be churn with no behaviour change). The innate-%→depleting-**pool** shield conversion is re-scoped to its own later slice (**3c**), since a faithful per-unit pool needs a persistent `CurrentShield` field + a nature-mix soak — a real change, not to be rushed under this one.
4. **The CLOSING model on the hex board — the north star (§7).** The auto-resolver simulates the closing itself: each sub-formation carries a `Position_m` that shrinks/opens each fine step by its **true move speed** under its doctrine (advance / screen / kite), fire is range-gated, damage accrues over the battle's duration. This is the space closing model (`AdvanceClosing` + the range gate) brought to ground with hex↔metre, and it's what makes "the zerglings close 3 hexes over X seconds and only then start landing hits" real. **Gauge:** a longer-ranged unit kites a faster short-ranged one until it closes (the ground twin of `ClosingTests`); a battle's outcome depends on closing time.
5. **Formation = fleet parity + sub-formation ranges + delete the duplicate.** Formation join → **min move speed / max sensor** (the ground twin of `FleetCombat.WarpSpeedFloor`/`SensorReach`); sub-formations each hold their own gap/speed/doctrine in one engagement (the P4 per-sub-group piece, both domains); remove the now-redundant triangle + the O(units²) pairwise loop, bucketing the ground side by combat value (100 zerglings → 1 bucket, the Titan → a bucket of one). **Gauge:** full suite green; a large-battalion perf gauge (the ground twin of `CombatPerformanceTests`) proves O(buckets).

## 6. Risks / invariants

- **Determinism** (fast-forward == watch) — the kernel is pure arithmetic; keep it RNG-free.
- **Byte-identity for ships** — slices 1–2 must not move a single ship-combat number; the ship fixtures are the tripwire.
- **The ground behaviour change is deliberate** (triangle→matchup) — slice 3b re-baselines `GroundForcesTests` with the reason written down; it is the one place outcomes legitimately shift. Slice 3a (armour) is byte-identical.
- **CI-blind** — one slice per push, both jobs green before the next; no stacking.
- **Bucketing both sides** is the payoff — it's what makes "any number of soldiers, any battalion combination" O(buckets), closing the §6 caveat.

## 7. THE NORTH STAR — the closing fight, one model, both domains (developer, 2026-07-08)

The definitive statement of what this merge is FOR. Quoted essence: *"ground units work the same as ships as far as fleets or battalions are concerned."* One shape, both domains:

**Grouping = fleets, exactly.** A **formation/battalion IS a fleet**. You can raise 50–100 zerglings that hold a formation, an armor column with a Titan in another, each its own battalion (fleet-equivalent). **Join them → the joined force moves at the pace of its SLOWEST unit but sees as far as its LONGEST sensor** — byte-for-byte the fleet rule (`FleetCombat.WarpSpeedFloor`/`DeltaVFloor` = min, `SensorReach` = max). They march **hex-by-hex across regions as one unit**.

**Trigger = weapon range of the longest-ranged unit.** The Titan sees 5 hexes but shoots 3. Combat starts when an enemy comes within **3** hexes — *because of the Titan* — and the auto-resolver takes over. This is the space rule already (`WithinWeaponRange` opens the fight at `Max(reachA, reachB)`; detection range ≫ weapon range).

**The fight = the auto-resolver simulates the closing, tick by tick, over the battle's duration.** With a doctrine (Titan kites / armor column screens the Titan / zerglings advance — as **sub-formations**), each sub-formation moves at its **true speed** under its stance: the zerglings sprint at the enemy, and *the resolver accounts for the 3-hex distance* — as battle-time passes they close based on their move speed and who's left, **only landing damage once they reach their range**, and the resolver keeps computing damage/losses continuously until it's decided. **You keep what survives.** This is the space **closing model** (`FleetCombatStateDB.Separation_m` + `AdvanceClosing` + the `BuildFireMix` range gate + the `HitFraction` range term) — on the hex board, with the gap in hexes×HexPitch = metres.

> *"This IS how space combat should be and how planetary combat MUST be. This is the north star of combat and what you are building towards this whole time."*

**Why the kernel merge is the enabler:** the closing model + the damage math must be **one** implementation, or the two domains drift. Slices 1–2 put the ship damage math in `CombatKernel`; 3a puts armour there; 3b puts the ground damage on it; slice 4 brings the closing model to ground; slice 5 gives formations fleet-parity aggregation + sub-formation ranges + buckets the ground side. The zergling/Titan example is the acceptance test for "done."

**Bucketing resolves the scale (corrected 2026-07-08):** 100 interchangeable zerglings collapse to **one** combat-value bucket; the unique Titan is a **bucket of one** — exactly the ship model (100 fighters + 1 dreadnought). Per-unit identity lives at the **sub-formation** level (swarm / column / Titan), not the individual, so bucketing loses nothing the player cares about.

### 7.1 The vocabulary + the "compute one, distribute across N" model (developer, 2026-07-08)

**Locked naming — the SAME two-level shape, one label set per domain:**

| Level | Space | Planetary |
|-------|-------|-----------|
| The whole force (the fleet-equivalent you select + order) | **Fleet** | **Battalion** |
| A sub-group inside it with its OWN doctrine | **Group** (was "sub-fleet") | **Formation** |
| The individuals | units (ships) | units (space marines, clone troopers…) |

So: **Fleet ▸ Groups ▸ ships** and **Battalion ▸ Formations ▸ soldiers**. Selecting a Fleet/Battalion and opening its info shows a **Group/Formation breakdown, split by units** (e.g. space marines and clone troopers in the same Battalion but different Formations). *(Naming reconciliation for slice 5: today's `GroundFormation` class sits at the **fleet/Battalion** level — "the ground echo of a fleet." Under this vocabulary a **Battalion** is that top grouping and a **Formation** is the sub-group. Slice 5 either renames or nests so the ground hierarchy reads Battalion ▸ Formation ▸ units. Pure orchestration above the kernel — no kernel change.)*

**Doctrine lives on the Group/Formation, not the whole force.** Each Group/Formation carries its own doctrine that dictates its behaviour in the fight — hang back, retreat out of range, rush the enemy, kite, hold, etc. (space: `FleetDoctrineDB` per Group — already the per-component doctrine model; ground: `GroundFormationDoctrine` + `GroundEngagementStance` per Formation — already built). The closing model (slice 4) reads each Group/Formation's doctrine to decide close/kite/hold, so the Titan's Group kites while the zergling Formation rushes, in one battle.

**The math the developer wants — "do the math for ONE unit + its doctrine, then distribute across N":** the resolver computes the outcome for a **single representative** — one unit, folding in its Group/Formation's doctrine, the opposing side, environmental effects, unit statuses, and any other modifiers — and then **distributes that result across the 50 / 100 / 10,000 identical units** in that bucket. This IS the bucketing model, stated as a design intent: the expensive per-salvo math (`CombatKernel.HitFraction`/shield/armour) runs **once per (representative × doctrine × situation) bucket**, and the caller scales it by the unit count. It's why "any number of soldiers, any battalion combination" stays O(buckets), not O(units) — and why the kernel is a set of **pure per-combatant functions** (compute-one) with the count/distribution handled by the orchestration (distribute-across-N). The refinement CONFIRMS the slice 1–3 kernel shape; nothing changes below the orchestration.
