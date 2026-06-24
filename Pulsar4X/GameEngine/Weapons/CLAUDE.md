# Weapons — Subsystem Reference

Space combat weapons: beam weapons, missiles, fire control. Lives in `GameEngine/Weapons/`.

---

## File Map

| File | Purpose |
|------|---------|
| `WeaponBeam/BeamInfoDB.cs` | DataBlob for an in-flight beam entity. Holds target, velocity, energy, position pair, state machine state. |
| `WeaponBeam/BeamWeaponProcessor.cs` | HotloopProcessor (1 sec). Advances beam state machine: Fired → AtTarget/MissedTarget. Calls damage on hit. |
| `WeaponBeam/GenericBeamWeaponAtb.cs` | Component design attribute. Configures beam energy, wavelength, velocity. Has `FireWeapon()` called by GenericFiringWeaponsProcessor. |
| `WeaponFireControl/FireControlAbilityDB.cs` | DataBlob: list of FireControlAbilityStates (one per fire control unit on the ship). |
| `WeaponFireControl/FireControlAbilityState.cs` | State for one fire control: assigned weapons, target entity, active/cease-fire. |
| `WeaponFireControl/BeamFireControlAtbDB.cs` | Component attribute for beam fire control. |
| `WeaponGeneric/GenericFiringWeaponsDB.cs` | DataBlob: arrays of weapon states per weapon slot. Internal magazine qty, reload rate, fire control assignments. |
| `WeaponGeneric/GenericFiringWeaponsProcessor.cs` | HotloopProcessor (1 sec). Iterates all weapon slots, fires when ammo ≥ min shots and target valid, reloads. |
| `WeaponGeneric/GenericWeaponAtb.cs` | Component attribute for generic weapons (ammo capacity, reload rate, damage). |
| `WeaponGeneric/WeaponState.cs` | Per-weapon state struct (internal magazine current amount, name). |
| `WeaponMissile/MissleLauncherAtb.cs` | Component attribute: configures missile launcher. Has `FireWeapon()` → `MissileProcessor.LaunchMissile()`. |
| `WeaponMissile/MissleLauncherAbilityDB.cs` | DataBlob for missile launcher ability. |
| `WeaponMissile/MissleProcessor.cs` | Static class. `LaunchMissile()` creates missile entity with NewtonMoveDB + ordnance design components. Uses `directAttack = true` → `ThrustToTargetCmd`. |
| `WeaponMissile/ProjectileInfoDB.cs` | DataBlob on a missile entity: who launched it, count, and `Entity TargetEntity` for impact detection. |
| `WeaponMissile/MissileImpactProcessor.cs` | HotloopProcessor (1 sec). Checks proximity of each `ProjectileInfoDB` entity to its target. On hit (≤ 1000 m): calls `DamageProcessor.OnTakingDamage()` with kinetic energy, destroys missile. |
| `OrdnanceDesign.cs` | Missile/ordnance design data (wet/dry mass, burn rate, exhaust velocity, payload). |
| `OrdnanceDesignFromJson.cs` | JSON loading for ordnance designs. |
| `OrdnancePayloadAtb.cs` | Component attribute: warhead payload type. |
| `SetFireControlOrder.cs` | Order to assign a fire control to a target and set fire mode (OpenFire / CeaseFire). |
| `IFireWeaponInstr.cs` | Interface: `FireWeapon(owner, target, shots)`. Implemented by weapon component attributes. |
| `WeaponUtils.cs` | Static helpers: `TimeToTarget()`, `ToHitChance()`, `PredictTargetPositionAndTime()`. |

---

## How Space Combat Works

### 1. Fire Control Assignment (player action)
```
Player → SetFireControlOrder → FireControlAbilityState.Target = targetEntity
                                                               .FireMode = OpenFire
```

### 2. Weapon Firing (HotLoop, every 1 sec)
```
GenericFiringWeaponsProcessor.UpdateWeapons(GenericFiringWeaponsDB db)
  for each weapon slot:
    shots = floor(internalMagQty / amountPerShot)
    if shots >= minShotsPerFire AND target.IsValid:
      fireInstructions[i].FireWeapon(owner, target, shots)
          ↓
      BeamWeaponAtb.FireWeapon() → BeamWeaponProcessor.FireBeamWeapon()
      OR
      MissleLauncherAtb.FireWeapon() → MissileProcessor.LaunchMissile()
    
    reload: internalMagQty = min(internalMagQty + reloadRate, magSize)
```

### 3. Beam Physics (HotLoop, every 1 sec)
```
BeamWeaponProcessor.UpdateBeam(BeamInfoDB)
  State: Fired
    → calc timeToTarget = distance / beamVelocity
    → if timeToTarget <= deltaSeconds:
        → CalculateHit() → RNG check using ToHitChance()
        → if hit: state = AtTarget, beam positioned at target
        → if miss: state = MissedTarget, energy dissipates 10%/sec
  State: AtTarget
    → OnHit() → **SimpleDamage.OnTakingDamage(target, 100, 500)**  ← PLACEHOLDER
    → OwningEntity.Destroy() (remove beam from game)
  State: MissedTarget
    → UpdatePhysics() → move beam forward
    → decay energy, destroy when energy == 0
```

### 4. Missile Launch
```
MissileProcessor.LaunchMissile(launcher, target, launchForce, design, count)
  → creates Entity with: ProjectileInfoDB, ComponentInstancesDB, PositionDB,
                          MassVolumeDB, NameDB, NewtonMoveDB, OrderableDB
  → adds components from OrdnanceDesign (propulsion, warhead, sensors)
  → issues NewtonThrustCommands for orbital phasing maneuvers toward target
  → removes missile from launcher cargo
```

---

## Beam Weapon Design (5 decisions — wired)

### Decision 1 — Two-zone range model (complete)
- Inside `OptimalRange_m` (= Focal Length from JSON): beam hits at full energy.
- Beyond `OptimalRange_m` out to `MaxRange`: energy scales inverse-square. `energyScale = (OptimalRange_m / distance)²`.
- `BeamInfoDB.OptimalRange_m` carries this per-beam. `BeamWeaponProcessor.OnHit()` applies the scale before building the `DamageFragment`.
- `MaxRange == 0` = unlimited (legacy designs unaffected). `OptimalRange_m == 0` = no falloff.

### Decision 2 — Wavelength connected to material resistance (complete)
- `DamageFragment.Wavelength` (double, nm) flows from `GenericBeamWeaponAtb.WaveLength` → `BeamInfoDB.Frequency` → `DamageFragment.Wavelength`.
- `DamageResistBlueprint.WavelengthAbsorption[5]` stores per-band absorption coefficients (UV/Vis/NIR/MIR/FIR).
- `DamageTools.GetWavelengthAbsorption()` maps nm to band index and returns the coefficient.
- Beer-Lambert model: `energyDeposited = energy × absorption`. Aluminium (0.06–0.18 across bands) is very hard to burn with laser; plastic (0.2–0.9) is easy. See `damageResistance.json` for all values.
- **JSON field bug fixed**: `[JsonProperty("UniqueID")]` maps the JSON key to `iDCode` byte. `DamageResistsLookupTable` is now populated at runtime.

### Decision 3 — Thermal management as fire-rate limiter (complete)
- `WeaponState.CurrentHeat_kJ` accumulates each time the weapon fires: `+= ThermalOutput_W × ChargePeriod / 1000`.
- `WeaponState.HeatCapacity_kJ` = `ThermalOutput_W × ChargePeriod × 2 / 1000` (headroom for 2 charge cycles).
- Each tick (1 second): passive cooling of `ThermalOutput_W / 1000 × 0.5` kJ removed.
- At `CurrentHeat_kJ >= HeatCapacity_kJ`: weapon is suppressed and cannot fire.
- `AllowThermalOverride` (weapon design flag) + `ThermalOverrideActive` (player toggle): override fires through thermal limit. Weapon damage from override is tracked but not yet implemented (future task).

### Decision 4 — Fire rate driven by Charge Period (complete)
- JSON formula: `genericWpnAtbArgs` = `AtbConstrArgs(100, Max(1, Ceiling(100 / ChargePeriod)), 100, 1)`.
- MagSize=100 abstract units, AmountPerShot=100, MinShotsPerfire=1, ReloadPerSec=`ceil(100/ChargePeriod)`.
- A 10-second charge period → reload rate 10/s → 10 ticks to reach full → fires once every 10 seconds.
- **Math.Max reload bug fixed** (was: `Math.Max(qty + reload, magSize)` always returned magSize → instant reload). Now `Math.Min`.

### Decision 5 — Power grid check (complete)
- Before firing, `GenericFiringWeaponsProcessor` deducts `beamAtb.Energy / 1000.0` kJ from `EnergyGenAbilityDB.EnergyStored`.
- If stored energy is insufficient, weapon cannot fire that tick.
- Ships without `EnergyGenAbilityDB` (no power plant) skip the power check and fire freely (for testing/basic ships).
- `ChargePeriod` and `ThermalOutput_W` are now args 5 and 6 of `genericBeamWpnAtbArgs` in weapons.json.

---

## Weapon Range Status (System 1 — complete)

`MaxRange` was already stored on `GenericBeamWeaponAtb` and populated from JSON but never enforced. Now wired:

- `IFireWeaponInstr` has `IsInRange(launcher, target)` with a default `return true` — existing weapon types unaffected unless they override it.
- `GenericBeamWeaponAtb.IsInRange()` checks `(launchPos - tgtPos).Length() <= MaxRange`. `MaxRange == 0` is treated as unlimited (preserves legacy designs that don't set a range).
- `GenericFiringWeaponsProcessor` calls `IsInRange()` before `FireWeapon()` — beam weapons now refuse to fire beyond their configured range.
- `BeamInfoDB` carries `BaseHitChance` (threaded from `GenericBeamWeaponAtb.FireWeapon()` → `FireBeamWeapon()`). `BeamWeaponProcessor.CalculateHit()` now uses the weapon's actual hit chance instead of the hardcoded 0.95.
- Missile range: deferred — `MissileLauncherAtb` inherits `IsInRange() = true`. Correct implementation requires delta-V calculation. See Gotcha 5.

**JSON default range is 5000m** (weapons.json, "Range" property). This is space-scale tiny — the developer should set this to something realistic (millions of km) when testing. The code is correct; the value is a configuration decision.

---

## Damage Status (Phase 1a + Phase 2 — DamageComplex fully wired)

**`DamageProcessor.OnTakingDamage()` is the active beam-hit path.** Path: `BeamWeaponProcessor.OnHit()` → energy scaled by two-zone model → `DamageFragment` with `Wavelength` → `DamageProcessor.OnTakingDamage()` → `DamageTools.DealDamageEnergyBeamSim()`.

Health scale calibration (fixed): `HealthPercent -= damageAmount * 0.001f`. 1000 damage points = 100% health. At 1 point per 100J deposited, a 100kJ direct hit destroys the component.

Remaining known calibration issues (tracked, don't fix without dedicated task):
- **Off-by-one**: G-channel bitmap is 1-indexed but `ComponentLookupTable` is 0-indexed → first slot never targeted.

---

## Missile Guidance Status (functional as of 2026-06-21)

`directAttack` is now `true`. `ThrustToTargetCmd.CreateCommand()` handles pursuit.

`ProjectileInfoDB` now stores `Entity TargetEntity` (following `BeamInfoDB` pattern). This is serialized via Newtonsoft `PreserveReferencesHandling.Objects`; it does not survive save/load if the missile is in-flight at save time (acceptable — combat is transient).

`MissileImpactProcessor` (new `IHotloopProcessor`) checks every second whether any `ProjectileInfoDB` entity is within 1000 m of its target. On hit: computes kinetic energy (0.5 × dry mass × closing speed²), calls `DamageProcessor.OnTakingDamage()`, destroys the missile.

**Known calibration issue:** Kinetic energy at orbital closing speeds (1–10 km/s) is GJ-range, far above the kJ–MJ scale the beam damage path is tuned for. Ships will be instantly destroyed by missile hits. Tune `MissileImpactProcessor.ImpactRadius_m` or scale energy before calling `OnTakingDamage` once warhead energy values are finalized.

---

## Adding a New Weapon Type

Pattern (copy beam weapon approach):

1. Create `WeaponXxx/XxxAtb.cs` implementing `IComponentDesignAttribute` and `IFireWeaponInstr`.
2. In `FireWeapon()`, either create a new entity (like BeamInfoDB) or call a processor static method (like MissileProcessor).
3. If the weapon has in-flight physics, create `XxxInfoDB.cs` (DataBlob) and `XxxProcessor.cs` (IHotloopProcessor).
4. The processor auto-registers — no manual setup needed.
5. Register the weapon's component template in `Data/basemod/blueprints/components/`.

---

## Gotchas

1. `ValidateTargetExists()` in GenericFiringWeaponsProcessor only sends CeaseFire once even if multiple fire controls have invalid targets. This is a minor bug — all invalid targets should receive CeaseFire.

2. **Reload bug — retired.** Was: `Math.Max` where `Math.Min` is needed. Fixed in a prior session — `GenericFiringWeaponsProcessor.cs` already uses `Math.Min(db.InternalMagQty[i] + tickReloadAmount, db.InternalMagSizes[i])` with a comment confirming the fix.

3. Beam entities are added to the same `StarSystem` as the firing ship. If the target is in a different system (impossible currently, but keep in mind for future multi-system combat), this would break.

4. `MissleLauncherAbilityDB` is spelled with one 's' — `Missle` not `Missile` throughout this directory. Do not "fix" the spelling in file/class names without updating all references.

5. **Missile range is not yet implemented.** `MissileLauncherAtb.IsInRange()` inherits the default `return true` from `IFireWeaponInstr`. The correct implementation is a delta-V range check: can the missile's fuel budget match the target's velocity and distance? See `OrdnanceDesign.cs` for fuel/exhaust data. Tracked as a future task — do not implement until System 9 auto-resolution is being built, as delta-V range directly feeds into the Tier 0 strength model.

6. **Off-by-one component targeting — retired.** Was: G-channel bitmap is 1-indexed but `ComponentLookupTable` is 0-indexed → first component never damaged. Fixed: `DamageProcessor.OnTakingDamage()` now uses `componentIdx = damage.id - 1` with a `>= 0` guard in both damage loops.
