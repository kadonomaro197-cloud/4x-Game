# Weapon Unification — one universal weapon, both resolvers

**Status:** design draft (2026-07-05), the design-before-code step for **task #3**. Not built. Sequenced with the
developer — this touches TWO live combat resolvers, so it gets a reviewed plan, not a blind refactor.

> Governs by `UNIVERSAL-ASSEMBLY-DESIGN.md` §2a: *a weapon is universal by TYPE, not by setting; the mount + carry
> decide where it fights.* The visible half (one designer **category**) is done; this is the **deep** half (one weapon
> **model** both combat systems read).

---

## 1. The discrepancy, precisely

There are **two parallel weapon systems** today, and an "energy weapon" is modelled **twice**:

| | Ground | Space |
|---|--------|-------|
| Weapon attribute | `GroundWeaponAtb` (`Attack`, `Range` in hexes, `Mode`) | `GenericBeamWeaponAtb` / `RailgunWeaponAtb` / `FlakWeaponAtb` (per-type, physical stats) |
| Type selector | `GroundWeaponMode` = Melee / Ballistic / Energy / Artillery | `WeaponClass` = Beam / Railgun / Flak / … |
| Resolver | `GroundForcesProcessor.ResolveRegionCombat` reads `unit.Attack` × triangle × terrain × the dodge/shield/armour matchup (`GroundDamageMatrix`) | `ShipCombatValueDB.Calculate` projects each atb into a `WeaponProfile(class, dps, velocity, tracking, saturation, range)`; `AutoResolve` consumes the profiles |
| Damage flavour used in combat | `GroundWeaponMode` → `GroundDamageMatrix` (dodge/shield/armour) | `WeaponClass` → the weapon triangle + dodge/saturation |

**The key observation:** the space side ALREADY normalises every weapon type into ONE intermediate — `WeaponProfile`
carrying a `WeaponClass`. That is the natural convergence point. `GroundWeaponMode` is a coarser parallel of
`WeaponClass`; `GroundUnit.Attack` is a coarser parallel of `WeaponProfile.dps`. The ground side just consumes a
*simpler projection* of the same idea.

---

## 2. Target

**One weapon design, carrying a TYPE + specs, projects into ONE shared weapon representation that BOTH resolvers
read.** "Energy weapon" is defined once. Whether that weapon rides infantry, a tank, or a ship spinal mount is the
**mount + the chassis carry gate + scale**, not a second attribute class.

The convergence artefact is the existing **`WeaponProfile` / `WeaponClass`** (extended as needed). A weapon design
(beam/kinetic/flak/…) produces a `WeaponProfile`; the **space** resolver consumes it as it does now; the **ground**
resolver derives what it needs (`Attack` from `dps`, hex `Range` from the profile's metric range via the *existing*
`GroundRangeTools.HexPitchKm` bridge, damage flavour from `WeaponClass`).

---

## 3. Candidate approaches

**A — Converge on `WeaponProfile` (recommended).** Make a weapon design's canonical output a `WeaponProfile`. Ground
units reference real weapon designs; `GroundForcesProcessor` derives `Attack`/`Range`/`DamageType` from the profile
(a small projection function, unit-testable). `GroundWeaponAtb` becomes either a thin *projector into* `WeaponProfile`
or is retired in favour of the typed weapon atbs. **Pro:** reuses the space side's already-unified representation;
`WeaponClass`↔`GroundWeaponMode` is a table; the hex↔metric bridge already exists. **Con:** the ground resolver must
learn to read a profile; the projection (how much `dps` = how much ground `Attack`) is a **calibration** the developer
tunes.

**B — Shared typed `WeaponAtb`.** Replace `GroundWeaponAtb` AND the three space atbs with ONE typed `WeaponAtb`
(`WeaponClass` + physical specs) that both resolvers read. **Pro:** one attribute, cleanest end state. **Con:** biggest
change; touches every space-combat gauge; higher risk.

**C — Shared spec value only.** Keep both atbs but have each project into a shared `WeaponSpec` struct both resolvers
consume. **Pro:** least invasive. **Con:** leaves the two atbs (the duplication) standing — doesn't really unify, just
bridges.

**Recommendation: A**, phased — it leans on the `WeaponProfile` normalisation the space side already proves, and lets
the ground resolver converge without a big-bang atb rewrite. B is the eventual clean end-state once A has de-risked it.

---

## 4. Phased plan (each a CI-gated slice)

1. **Map the vocabularies.** A pure, tested table `GroundWeaponMode ⇄ WeaponClass` (Energy⇄Beam, Ballistic⇄Railgun,
   Artillery⇄?, Melee⇄?). Add the missing `WeaponClass` members if needed. No behaviour change.
2. **Weapon → `WeaponProfile` for ground.** A projection `WeaponProfile → (Attack, hexRange, DamageType)` using
   `GroundRangeTools.HexPitchKm` for the range conversion. Pure + gauged. Still nothing wired.
3. **Ground resolver reads the profile.** `GroundForcesProcessor` derives a unit's combat stats from its weapon
   design's `WeaponProfile` instead of the raw `GroundWeaponAtb` fields (kept behind a flag / parallel until the
   numbers match the current garrison, so existing ground gauges stay green).
4. **Unify the designer end-to-end.** A single weapon design is mountable on a ground unit (its stats flow through the
   profile) — proving "design one beam, use it on infantry or a ship." Retire the `GroundWeaponAtb` duplication (or
   demote it to a pure projector).
5. **Extend the pattern to the other component kinds** (armour, sensor, engine…): one designer per kind, pick a type,
   spec it — the same convergence.

---

## 5. Open questions for the developer (needed before slice 3)

- **Fidelity:** should ground combat read the FULL space weapon profile (rate-of-fire, saturation, projectile
  velocity, tracking → dodge) — i.e. the weapon triangle applies on the ground too — or a **simplified projection**
  (just `dps`→`Attack` + a flavour)? This decides how deep the ground resolver converges.
- **Calibration:** the `dps → ground Attack` scale (a ship beam is GJ/s; a rifle is not). Likely a per-scale divisor —
  a flagged number, your call.
- **hex ⇄ metric range:** confirm `GroundRangeTools.HexPitchKm` is the intended bridge (a 5 km ship laser = how many
  hexes on Earth vs Io — already a visible readout).
- **One design on both, or one TYPE on both?** Is a *single weapon design instance* literally mountable on a ground
  unit AND a ship, or do they share the TYPE + designer but instantiate separately at their own scale? (The carry gate
  suggests the latter — same designer, scale-appropriate instances.)

---

## 6. Risks

- Two live resolvers + every space-combat gauge (`CombatStressLab`, `WeaponTriangle*`, etc.) — a projection that
  shifts numbers reds them. Mitigate with the parallel/flagged path in slice 3 (converge only once numbers match).
- `WeaponProfile` is currently a *space* combat-value type; widening its home (namespace/assembly) so ground can read
  it needs care (`GameEngine/Combat` ↔ `GameEngine/GroundCombat`).
- Calibration numbers are the developer's — do not bake scale divisors without flagging.
