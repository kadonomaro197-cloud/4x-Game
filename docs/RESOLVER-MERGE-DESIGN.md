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
3. **Planetary Combatant view + route through the kernel.** `GroundUnit` presents a `Combatant` (its `WeaponProfile`s from the unit designer; Position from hexes). `ResolveRegionCombat` calls the kernel; keeps terrain/fort/stance/hex-range/movement. **Gauge:** `GroundForcesTests` — outcomes match (or the diff is the *intended* triangle→matchup change, re-baselined with a documented reason).
4. **Movement-as-range on the hex board.** Wire `Position_m` to update each tick from `GroundMobility` speed + the ROE close/kite step, so range evolves during the fight. **Gauge:** a longer-ranged unit kites a faster short-ranged one until it closes (the ground twin of `ClosingTests`).
5. **Delete the duplicate.** Remove `GroundDamageMatrix`'s now-redundant triangle/matchup + the O(units²) pairwise loop; both domains are the one bucketed kernel. **Gauge:** full suite green; a large-battalion perf gauge (the ground twin of `CombatPerformanceTests`) proves O(buckets).

## 6. Risks / invariants

- **Determinism** (fast-forward == watch) — the kernel is pure arithmetic; keep it RNG-free.
- **Byte-identity for ships** — slices 1–2 must not move a single ship-combat number; the ship fixtures are the tripwire.
- **The ground behaviour change is deliberate** (triangle→matchup) — slice 3 re-baselines `GroundForcesTests` with the reason written down; it is the one place outcomes legitimately shift.
- **CI-blind** — one slice per push, both jobs green before the next; no stacking.
- **Bucketing both sides** is the payoff — it's what makes "any number of soldiers, any battalion combination" O(buckets), closing the §6 caveat.
