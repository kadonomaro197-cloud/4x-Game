# Weapons — Subsystem Reference

Space combat weapons: beam weapons, missiles, fire control. Lives in `GameEngine/Weapons/`.

---

## Design principle — weapon ranges are REALISTIC, and that's the point (developer's call, 2026-06-27)

**Space is big. Weapon ranges stay physically honest, not sci-fi-convenient.** A default beam (`weapons.json` `Range` ≈ **5 km**) really is a 5 km knife-fight laser — microscopic next to the `EngagementRange_m` = 1,000,000 km detection/closing bubble. That mismatch is **intended**, not a bug: you detect at a million km and then *close* to where your guns actually reach (this is exactly what the closing-fight model — `docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md` — is built to play out).

The rule going forward, for beams and every other weapon:
- **Long range is earned, not given.** A laser coherent over tens of thousands of km needs enormous power — so a long-range design must carry a **fat price tag in both construction (materials/tech) and utilization (power/heat)**. Range is a stat you *pay* for, up the whole cradle-to-grave chain (mineral → material → component → research → installed → the in-play reach).
- **Don't "fix" a small range by inflating the number.** If a weapon feels too short-legged, the answer is a *new, more expensive design*, not rescaling the base data. Keep the base data realistic.
- **UI consequence (not a data problem):** a 5 km ring is sub-pixel at system scale — you **zoom in** to appreciate it. The render side is made safe for that zoom (the `SimpleCircle` on-screen cull, root `CLAUDE.md` gotcha #15), so zooming to the ring no longer stutters. The ring being tiny is the realism showing through, not a defect.

Before changing any `Range`/`MaxRange`/`OptimalRange` value, re-read this — the instinct to make ranges "playable-big" is the thing we're deliberately resisting.

### Start realistic → climb to sci-fi through RESEARCH (developer's call, 2026-06-27)

**Every component starts at a physically-honest baseline; RESEARCH is what pushes it toward sci-fi.** A starting
laser is a real ~5 km knife-fight laser; a **Muon laser** (or whatever the tech tree unlocks) is the *researched*
upgrade that makes lasers genuinely more powerful/longer-ranged. The same arc applies to every system — sensors,
engines, armour, reactors: the v1 numbers are the "we just left Earth" floor, and the climb to space-opera
capability is **earned up the tech tree**, by the player AND by NPCs (so a late-game fleet genuinely out-techs an
early one — a real progression axis, not just bigger numbers handed out).

**The mechanism already exists — don't build a parallel one.** Component design formulas pull tech values via
NCalc `TechData('tech-...')` (e.g. sensor sensitivity = `TechData('tech-antenna-sensitivity') / (size² · eff)`;
weapon stats reference their tech). So "research makes it better" is wired by having a stat's NCalc formula read a
`TechData(...)` term that improves as the tech levels up. To add a sci-fi upgrade:
- **Better numbers on the EXISTING component type** → raise/extend the tech the formula already reads (the cheap,
  smooth path — a higher antenna-sensitivity level just makes every sensor design better).
- **A genuinely NEW weapon/sensor flavour** (the Muon laser, a new emitter) → a new component **template** gated
  behind a new tech, following the six-point registration chain in `Combat/CLAUDE.md` ("Adding a new
  player-buildable weapon"). It's still a component — research-gated, built-from-materials, installed, losable —
  so it inherits the whole cradle-to-grave chain for free (`CONVENTIONS.md` §6: abilities are components).

**Rule:** when you add a stat that should improve with progress, make its design formula read a `TechData(...)`
term rather than hard-coding the number — that is what turns "realistic baseline" into "research climbs to sci-fi"
without a bespoke upgrade system. The realism floor + the tech ceiling are the same scale, just at different tech
levels.

---

## File Map

| File | Purpose |
|------|---------|
| `WeaponBeam/BeamInfoDB.cs` | DataBlob for an in-flight beam entity. Holds target, velocity, energy, position pair, state machine state. |
| `WeaponBeam/BeamWeaponProcessor.cs` | HotloopProcessor (1 sec). Advances beam state machine: Fired → AtTarget/MissedTarget. Calls damage on hit. |
| `WeaponBeam/GenericBeamWeaponAtb.cs` | Component design attribute. Configures beam energy, wavelength, velocity. Has `FireWeapon()` called by GenericFiringWeaponsProcessor. **+`CombatHeat_kJps` (Weapons pilot W5c, 2026-07-10)** — the AUTO-RESOLVE waste-heat (distinct from `ThermalOutput_W`, which drives the parked per-pixel sim); flows into `WeaponProfile.HeatPerSecond` so a HOT beam builds the fleet's heat pool and needs radiators. Added via an 8-arg ctor overload (the 7-arg original kept for existing beam templates — the exact-arity binder rule, gotcha #0); 0 for every base-mod laser → byte-identical. The base-mod `pulse-laser` (on the Ember Pulse Cruiser) dials it up. **`Energy` is a `double`** (weapon-designer scale span, 2026-07-05): it was an `int` that silently overflowed past ~2.1 GJ, capping a superlaser; the whole downstream chain (`FireBeamWeapon`, `ShipCombatValueDB` firepower, power-draw) was already double. Gauge: `GenericBeamWeaponAtbTests`. |
| `WeaponFireControl/FireControlAbilityDB.cs` | DataBlob: list of FireControlAbilityStates (one per fire control unit on the ship). |
| `WeaponFireControl/FireControlAbilityState.cs` | State for one fire control: assigned weapons, target entity, active/cease-fire. |
| `WeaponFireControl/BeamFireControlAtbDB.cs` | Component attribute for beam fire control. Carries `Range` / `TrackingSpeed` / `FinalFireOnly`. **`TrackingSpeed` wired (Sensors ⚙3 S1)** → `ShipCombatValueDB` beam tracking (gated `EnableFireControlTracking`). **`FinalFireOnly` wired (S3, 2026-07-11)** → a director flagged FinalFireOnly is a **CIWS**: `ShipCombatValueDB` routes the ship's BEAM damage/sec into `PointDefense_Jps` (missile interception, the W6 pool) instead of anti-ship firepower (gated `EnableFinalFireOnlyPD`, default off → byte-identical; no base director sets FinalFireOnly). A 3-double ctor `(range, trackingSpeed, finalFireOnly)` feeds the binder (gotcha #0 exact-arity). Base-mod `pd-director` template on the **Sentinel CIWS Escort**. `Range` is still a dead knob (a units-scale mismatch vs realistic weapon ranges — flagged). Gauge `ShipFireControlPDTests`. |
| `WeaponGeneric/GenericFiringWeaponsDB.cs` | DataBlob: arrays of weapon states per weapon slot. Internal magazine qty, reload rate, fire control assignments. |
| `WeaponGeneric/GenericFiringWeaponsProcessor.cs` | HotloopProcessor (1 sec). Iterates all weapon slots, fires when ammo ≥ min shots and target valid, reloads. |
| `WeaponGeneric/GenericWeaponAtb.cs` | Component attribute for generic weapons (ammo capacity, reload rate, damage). |
| `WeaponRailgun/RailgunWeaponAtb.cs` | Component attribute for a **railgun / slug-thrower**: muzzle velocity, kinetic energy/shot, rounds/sec, tracking. Implements ONLY `IComponentDesignAttribute` (no-op install, no `IFireWeaponInstr`) — it feeds the **auto-resolve** combat value (`ShipCombatValueDB` reads it into a `Railgun` `WeaponProfile`: finite velocity, ballistic, rof→saturation) and is invisible to the parked per-pixel firing sim. JSON: `railgun-weapon` template (weapons.json). |
| `WeaponFlak/FlakWeaponAtb.cs` | Component attribute for a **flak / point-defense gun**: muzzle velocity, damage/pellet, rounds/sec, pellets/shot, tracking. Same auto-resolve-only pattern as the railgun (no `IFireWeaponInstr`). `ShipCombatValueDB` reads it into a `Flak` `WeaponProfile` with **saturation = rounds/sec × pellets/shot** (high) and low per-pellet damage — the saturation floors the dodge, so flak catches fighters/missiles. JSON: `flak-weapon` template (weapons.json). |
| `WeaponPlasma/PlasmaBoltWeaponAtb.cs` | Component attribute for a **Plasma Repeater — the DODGEABLE ENERGY bolt** (the two-axis payoff, docs/combat/WEAPONS-DESIGN.md). Energy/shot + rounds/sec + finite bolt velocity + tracking. Same auto-resolve-only pattern (no `IFireWeaponInstr`). `ShipCombatValueDB` reads it into a **`WeaponNature.Energy` + `WeaponDelivery.Bolt`** profile: it reads as **Railgun-CLASS in the dodge model** (finite velocity → juke-able, like a slug) but its **Energy nature** means a shield only HALF-soaks it (it bleeds through, like a beam). The corner the fused enum had no cell for — same dodge-class as a kinetic railgun, different shield behaviour. JSON: `plasma-repeater` template (weapons.json), example ship `default-ship-design-test-plasma` (Vanguard). Gauge: `PlasmaBoltWeaponTests`. |
| `WeaponDisruptor/DisruptorWeaponAtb.cs` | Component attribute for an **Ion Disruptor — the ANTI-SHIELD exotic** (space shield Phase D, docs/combat/WEAPONS-DESIGN.md §5). Energy/shot + rounds/sec. Same auto-resolve-only pattern (no `IFireWeaponInstr`). `ShipCombatValueDB` reads it into a **light-speed (undodgeable), `WeaponNature.Exotic`** `WeaponProfile` — the Exotic nature means the shield's exotic-soak is 0, so it BYPASSES a shield pool and strikes the hull (the rock to the shield's scissors; modest raw yield so it's not a better beam). JSON: `disruptor-weapon` template (weapons.json), example ship `default-ship-design-test-disruptor` (Ravager). Gauge: `DisruptorWeaponTests`. |
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
    → OnHit() → **DamageProcessor.OnTakingDamage(target, damageFragment)**  ← full two-zone/wavelength model (BeamWeaponProcessor.cs:144)
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

> **⚠ DATA FINDING (2026-06-27): the base laser's falloff never fires.** This model assumes **focal length < max range**, but the base-mod laser ships `Range` = 5,000 m and `Focal Length` = 1,000,000 m (a "Distance to target (debug)" placeholder in `weapons.json`), so `OptimalRange_m` is 200× the gun's reach. `OnHit` only attenuates `if (distance > OptimalRange_m)` and caps `energyScale` at 1.0 otherwise — **no damage bug** (never amplifies), but every reachable hit (≤ 5,000 m) is far inside optimal, so the falloff branch is never taken: the laser deals **flat full energy to max range** and the two-zone feature is dead for this design. **Fix is DATA, not code** (balance call — flagged, not changed): set Focal Length below Range (e.g. `Range * 0.5`) for a real falloff band, or declare flat-damage the intent. Full write-up: `docs/combat/INFORMATION-DELTA-DESIGN.md` → "Finding: the base laser's two-zone falloff never fires."

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

### Engagement range READOUT (2026-06-27) — the enforced MaxRange, now visible

`MaxRange` was enforced but invisible: a weapon past range just silently didn't fire, with zero UI feedback (the player couldn't tell "out of range" from "broken"). `WeaponUtils` now exposes it:
- `GetMaxBeamRange_m(Entity ship)` — the ship's longest beam reach (max `MaxRange` across installed, **enabled, undestroyed** beam weapons; a shot-off gun no longer extends reach — the loss rung). 0 = no finite beam range.
- `GetBeamWeaponRanges(Entity ship)` → `List<(name, maxRange, optimalRange)>` — the per-weapon breakdown for a readout; skips legacy `MaxRange==0` "unlimited" designs.

These feed the client (Fleet Combat tab columns + fleet "Beam reach" row; Fire Control range-to-target + red **OUT OF RANGE**; map range rings). Gauged by `Pulsar4X.Tests/RangeReadoutTests.cs` (aggregation on the Aegis). Full survey + the two-failure-mode framing: `docs/combat/INFORMATION-DELTA-DESIGN.md`. Missile range is still a stub (`IsInRange` returns true) so no missile ring is drawn — drawing one would lie.

**The Range DIAL is cradle-to-grave (dossier ⚙1 Weapons▸Energy, 2026-07-11).** The `laser-weapon` template's `Range` Property has `MaxFormula = TechData('tech-beam-range')` (= 10,000 m at starting tech, doubling per level — long range is *earned by tech*, per the §12 note above). The base-mod `default-design-long-range-laser` design turns that knob to the 10,000 m ceiling (Focal Length 5,000 m for a wide full-damage band), mounted on the **Longbow Standoff Cruiser** — proving a design's `Range` override propagates JSON → NCalc `genericBeamWpnAtbArgs` → `GenericBeamWeaponAtb.MaxRange` → the reach the closing-combat trigger and firing path both read. Gauged by `Pulsar4X.Tests/ShipLongRangeLaserTests.cs` (Longbow reaches 10,000 m; a default-laser Aegis stays at 5,000 m → the dial doubles reach, default untouched). Additive/byte-identical.

**Range is also earned by MASS (S9, 2026-07-11) — closing the "range was free" gap.** Range's *ceiling* was tech-gated, but dialing it up *within* the tech band cost nothing (the Mass formula ignored Range) — a "pretty" dial with no tradeoff, contradicting the §12 principle ("a fat price tag in both construction and utilization"). Fixed: the `laser-weapon` Mass formula now adds `Max(0, PropertyValue('Range') - 5000) * 0.5`, so range beyond the 5,000 m baseline costs mass — which flows on to crew/research/credits/materials via the template's `[Mass]` cost formulas, and drags the ship's mobility + mass budget. **Anchored at the current default (5,000 m → zero extra)**, so every existing laser design is byte-identical; only a long-range design pays (the long-range laser is +2,500, a 25% mass premium). The Range dial is now a real decision: reach vs weight. Gauged by `ShipLongRangeLaserTests.TheRangeDial_CostsMass_DefaultUntouched`.

**The railgun MUZZLE-VELOCITY dial is the same story (S10, 2026-07-11) — cradle-to-grave, cost wired.** Muzzle Velocity decides who a railgun can HIT: a faster slug spends less time in flight, so it beats evasion (the dodge model's `velocityTerm = velocity/(velocity + VelocityReference)` — the weapon-triangle "fast slug juks the juker"). It was FREE (the railgun Mass formula ignored it), so max velocity cost nothing. Fixed: the `railgun-weapon` Mass formula now adds `Max(0, PropertyValue('Muzzle Velocity') - 50000) / 1000` (bigger rails/capacitors for a faster slug). The base-mod `default-design-high-velocity-railgun` (Muzzle Velocity 200,000 m/s, 4× the 50,000 baseline) on the new **Culverin cruiser** lands more fire on an evasive target than a default-railgun Lancer, and weighs +150. Anchored at the 50,000 baseline → every existing railgun is byte-identical (the siege-railgun is a separate template, untouched). Gauged by `ShipHighVelocityRailgunTests`.

**S14 completes it for the SIEGE railgun** — the same `+ Max(0, MuzzleVelocity-50000)/1000` term on the `siege-railgun` template, with the base-mod `default-design-high-velocity-siege` (Muzzle Velocity 250,000, +200 mass). Anchored at the 50,000 baseline → the stock siege design and the Bombard that mounts it are byte-identical. Gauged by `ShipHighVelocitySiegeTests`. **This closes "velocity is earned" for ALL ballistic mass-drivers (railgun + siege).** The full free-dial audit of the whole designer (what's costed / what's truly-free-and-why-left) is the ledger in `docs/economy/COMPONENT-DESIGNER-DIALS.md §0d′` — the transparency-law sweep is substantially complete after S8–S14; flak Muzzle Velocity is truly free but left (saturation-dominated, marginal).

**Plasma BOLT-VELOCITY is the third of the free-velocity-dial trio (S11, 2026-07-11).** Same story as the railgun: a plasma repeater's bolts are finite-velocity (dodgeable), and Bolt Velocity decides how well they beat the dodge — but it was free (the plasma Mass formula ignored it). Fixed: `plasma-repeater` Mass += `Max(0, PropertyValue('Bolt Velocity') - 200000) / 10000`. The base-mod `default-design-high-velocity-plasma` (Bolt Velocity 600,000 m/s, 3× the 200,000 baseline) on the new **Tempest Plasma Lancer** lands more fire on an evasive target than a default-plasma Vanguard, and weighs +40. Anchored at the 200,000 baseline → every existing plasma is byte-identical. Gauged by `ShipHighVelocityPlasmaTests`. **The free-velocity-dial trio is now closed: laser Range (S9), railgun Muzzle Velocity (S10), plasma Bolt Velocity (S11) — each a "reach/accuracy vs weight" decision instead of a free win.**

**Flak DAMAGE-PER-PELLET was the last free DAMAGE dial (S12, 2026-07-11) — a consistency fix.** A railgun's damage dial (Kinetic Energy Per Shot) already costs mass, but flak's Damage Per Pellet was free (the flak Mass formula only counted saturation = rounds × pellets). Fixed: `flak-weapon` Mass += `Max(0, PropertyValue('Damage Per Pellet') - 1000) / 100`. The base-mod `default-design-heavy-flak` (Damage Per Pellet 5,000 J, 5× the 1,000 baseline) on the new **Redoubt Heavy Flak Escort** deals more damage/sec at the same saturation, and weighs +40. Anchored at the 1,000 baseline → every existing flak is byte-identical. Gauged by `ShipHeavyFlakTests`. **Every direct-fire weapon's reach/velocity/damage dial now carries a cost** — the "cost the free dial" sweep is complete for the CLEAN cases.

**Why S9–S12 are the whole clean set — and what the remaining free dials share (the "settable mass-dial" pattern, 2026-07-11).** The sweep was byte-identical *only* because the laser/railgun/plasma/flak templates compute Mass from a **direct arithmetic formula** — so adding `Max(0, dial - default) * k` to that formula can't be bypassed, and every existing design (using the default) is unchanged. The audit of the OTHER "free-in-Mass" dials turned up two kinds that are NOT clean data slices, and a lesson:
- **Already costed via a computed Mass PROPERTY** (`Mass = PropertyValue('Mass')`): `beam-fire-control` Tracking Speed, the hardening modules' Resistance (`100 + 200 × Resistance`), sensors — these already pay, no work needed. *(Audit trap: grepping the literal Mass string reports them "free"; you must resolve the Mass-property formula.)*
- **Wired to a different consequence**: `pulse-laser` Combat Heat feeds `WeaponProfile.HeatPerSecond` → the radiator throttle — it costs COOLING, not mass. Fine.
- **The genuinely-free remainder shares the "settable mass-dial" pattern** — the impactful dial is free because component Mass is an INDEPENDENT settable dial a designer could override low. The clean fix (byte-identical, no engine change) is the **mass FLOOR** done in NCalc: `Mass = Max(settableMassDial, floor(impactfulDial))` — at the default the floor equals the current value (byte-identical), and dialing the mass below what the impactful dial demands clamps UP (un-bypassable, unlike a plain additive term on a settable dial):
  - **Exotic drives — DONE via the floor (S13, 2026-07-11):** `reactionless-drive` Mass = `Max(Drive Mass, 5000 + Max(0, Thrust - 200000)/200)` — a high-thrust drive can't be a featherweight, so its evasion payoff is paid in tonnage (base-mod High-Thrust Reactionless Drive + Sprint cruiser; gauge `ShipHighThrustDriveTests`). The **inertialess** drive's Evasion Override is the same shape (a follow-up floor if wanted).
  - **Ground-weapon `Attack` — still flagged:** its carry-GATE reads the `GroundWeaponAtb.Mass` **ctor arg** (`= PropertyValue('CarryMass')`), not the component Mass — so the floor must be applied in BOTH the `Mass` formula AND the `groundrifleArgs`/`...Args` atb-arg formula (a two-place change). Doable the same clean way, but touches the assembler's input contract; see `GroundCombat/CLAUDE.md` "Flagged design question" for the options before wiring.

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
5. Register the weapon's component template in `Pulsar4X/GameData/basemod/TemplateFiles/` (e.g. `weapons.json`; copied to AppData/Mods at build).

---

## Gotchas

0. **🧨 A reflection-bound weapon-atb ctor needs an EXACT-ARITY overload — an OPTIONAL/DEFAULT param does NOT count (learned the hard way, W4a 2026-07-10).** The component binder (`ComponentDesigner.SetAttributes` → `Activator.CreateInstance`) matches a ctor by the **number of args the JSON `AtbConstrArgs(...)` supplies** — it does not fill in C# default parameter values. So adding a dial by turning `Atb(a,b,c,d)` into `Atb(a,b,c,d, e = 0)` **deletes the 4-arg ctor** as far as the binder is concerned: the existing template still passes 4 values → `MissingMethodException: Constructor ... not found` → **every colony/New-Game build crashes** (all 4 CI test shards red, but `build-client` stays green because it doesn't compile the test project — the tell-tale signature of a runtime binder break, not a compile error). **Fix pattern:** keep the original-arity ctor as an explicit overload that delegates with the default (`public Atb(a,b,c,d) : this(a,b,c,d, 0) {}`) AND add the new-arity ctor; a template that wants the dial passes the extra value and binds to the longer ctor. (Contrast the ground `GroundUnitAtb`, where the SAME kind of dial worked with an optional param **only because its JSON template was updated to pass the new arg**, so the arity still matched.) `RailgunWeaponAtb`/`FlakWeaponAtb` gained `Recoil` this way. This is the runtime sibling of the "ctor arg ORDER must match `AtbConstrArgs`" rule.

1. `ValidateTargetExists()` in GenericFiringWeaponsProcessor only sends CeaseFire once even if multiple fire controls have invalid targets. This is a minor bug — all invalid targets should receive CeaseFire.

2. **Reload bug — retired.** Was: `Math.Max` where `Math.Min` is needed. Fixed in a prior session — `GenericFiringWeaponsProcessor.cs` already uses `Math.Min(db.InternalMagQty[i] + tickReloadAmount, db.InternalMagSizes[i])` with a comment confirming the fix.

3. Beam entities are added to the same `StarSystem` as the firing ship. If the target is in a different system (impossible currently, but keep in mind for future multi-system combat), this would break.

4. `MissleLauncherAbilityDB` is spelled with one 's' — `Missle` not `Missile` throughout this directory. Do not "fix" the spelling in file/class names without updating all references.

5. **Missile range is not yet implemented.** `MissileLauncherAtb.IsInRange()` inherits the default `return true` from `IFireWeaponInstr`. The correct implementation is a delta-V range check: can the missile's fuel budget match the target's velocity and distance? See `OrdnanceDesign.cs` for fuel/exhaust data. Tracked as a future task — do not implement until System 9 auto-resolution is being built, as delta-V range directly feeds into the Tier 0 strength model.

6. **Off-by-one component targeting — retired.** Was: G-channel bitmap is 1-indexed but `ComponentLookupTable` is 0-indexed → first component never damaged. Fixed: `DamageProcessor.OnTakingDamage()` now uses `componentIdx = damage.id - 1` with a `>= 0` guard in both damage loops.
