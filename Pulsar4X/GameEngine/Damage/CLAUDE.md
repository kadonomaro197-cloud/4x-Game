# Damage — Subsystem Reference

Three implementations exist: `Simple/` (dead code), `DamageComplex/` (active — ships and now colonies), and `DamageVeryComplex/` (active for asteroids). Lives in `GameEngine/Damage/`.

---

## File Map

| File | Purpose |
|------|---------|
| `Simple/SimpleDamage.cs` | Dead code. Not called from any active path — preserved for test reference. |
| `DamageComplex/DamageProcessor.cs` | Active damage path. `OnTakingDamage()` routes ships through `DealDamageEnergyBeamSim()`, colonies through `OnColonyDamage()`, and **stations through `OnStationDamage()`** (Slice B — the station grave rung). The colony/station casualty + module passes are shared via `ApplyPopulationCasualties()` / `ApplyInstallationDamage()`. |
| `DamageComplex/DamageTools.cs` | `DealDamageEnergyBeamSim()` — spatial component damage kernel. |
| `DamageComplex/EntityDamageProfileDB.cs` | DataBlob: HTK remaining per component instance on a ship. Created lazily on first hit. |
| `DamageComplex/ComponentPlacement.cs` | Spatial placement data for components in a ship cross-section. |
| `DamageVeryComplex/DamageMap.cs` | **NEW** — 2D particle grid representing a ship or object cross-section. The substrate the physics sim runs on. |
| `DamageVeryComplex/DamagePhysicsSim.cs` | **NEW** — Physics loop: moves particles, detects collisions, resolves them, handles photon beams. Recursive — spawns sub-maps for high-velocity particles. |
| `DamageVeryComplex/KineticMath.cs` | Kinetic particle math: collision detection, elastic/inelastic resolution, fastest-particle query. |
| `DamageVeryComplex/PhotonMath.cs` | Photon/beam propagation through the DamageMap. |
| `DamageVeryComplex/PressureMath.cs` | Pressure wave propagation (for explosions/detonations). |
| `DamageVeryComplex/TempratureMath.cs` | Thermal effects on particles (heating from beams, ablation). |
| `DamageVeryComplex/Particle.cs` | `PhysicalParticle` struct: position, velocity, material properties. The atom of the physics sim. |
| `DamageVeryComplex/AsteroidDamage.cs` | **Active path for asteroid kinetic impacts.** Wires an asteroid strike into the particle physics sim. |
| `DamageSignature.cs` | **The KEYSTONE (2026-06-28).** The coarse, shared "damage flavour" enum — `DamageSignature` (HardRadiation / Thermal / Kinetic / EMStorm / Gravimetric / Corrosive) — that lets a space HAZARD and a WEAPON speak one language, so armour (and later shields) that resist a flavour resist it from both. Sits ABOVE the narrower `Hazards.HazardEffectType` (hazard-only + non-damage kinds) and `Combat.WeaponClass` (weapon platform). `DamageSignatures.UsesWavelengthArmorPath(sig)` is the load-bearing split: Thermal/HardRadiation/Kinetic already deposit through the wavelength-armour sim below; EMStorm/Gravimetric/Corrosive have **no wavelength** and need their own application site built. See "The DamageSignature keystone" below. |

---

## The DamageSignature keystone (the shared hazard↔weapon damage vocabulary)

**Why it exists.** Armour already resists damage by **wavelength** (`DamageFragment.Wavelength` → `DamageResistBlueprint.WavelengthAbsorption[5]`), and that *already* unifies the wavelength-based flavours for free — a thermal hazard (IR) and a thermal beam (IR) both land in the same far-IR absorption band, so heat-tuned armour resists both with no extra code. What was missing is a **coarse label** the player and the systems can name and match on — and a home for the flavours that are **not** a wavelength. `DamageSignature` (in `Damage/DamageSignature.cs`, namespace `Pulsar4X.Damage`) is that label.

**The six (coarse — the developer's locked "5–8 classes" call; finer variants are DATA later):** `HardRadiation` (UV/ionising), `Thermal` (IR/heat), `Kinetic` (impacts/debris — the wavelength-0 convention), `EMStorm` (EM interference), `Gravimetric` (tidal/spacetime), `Corrosive` (chemical/dense medium).

**The load-bearing distinction — `DamageSignatures.UsesWavelengthArmorPath(sig)`:**
- **TRUE** for Thermal / HardRadiation / Kinetic — these already deposit through `DealDamageEnergyBeamSim` (Kinetic via the wavelength-0 → near-IR-band convention). `RepresentativeWavelength_nm(sig)` gives the nm that lands each in the right band (HardRadiation 150 = UV, Thermal 10000 = far-IR, Kinetic 0).
- **FALSE** for EMStorm / Gravimetric / Corrosive — these have **no wavelength** and so need their **own damage application site** before they can hurt anything (gravimetric especially — tidal force, per the black-hole work). They have no `HazardEffectType` yet for the same reason; when built they're **appended** to that enum, never reordered (JSON references it by int — root gotcha #10).

**Threaded through the damage path (slice 2 — additive, default-identical).** A hit now CARRIES its flavour and armour can RESIST it:
- **`DamageFragment.Signature`** — every hit packet carries a flavour. Producers stamp it: hazards from `HazardEffect.Signature` (`SpaceHazardProcessor`), a beam from its wavelength via `DamageSignatures.FromWavelength_nm` (`BeamWeaponProcessor` — a UV laser → HardRadiation, IR → Thermal), a missile = `Kinetic` (`MissileImpactProcessor`). Railgun/flak have **no per-pixel path** (auto-resolve only, signature-blind) so they stamp nothing — correct.
- **`DamageResistBlueprint.SignatureResistance[6]`** — per-material resistance fraction, indexed by `(int)DamageSignature` `[Kinetic, Thermal, HardRadiation, EMStorm, Gravimetric, Corrosive]`. **Default all 0 → byte-identical to before.** Loaded by-name from the material JSON like `WavelengthAbsorption`.
- **In `DealDamageEnergyBeamSim`** (the deposition loop): the rated material still ABSORBS the energy (so it still shields the interior — `energy -= energyDeposited` unchanged) but takes LESS DAMAGE from the flavour it's rated against: `damageAmount = energyDeposited × 0.01 × (1 − sigResist)` via `GetSignatureResistance` (internal). So "the armour material IS the counter" is now literal — and stacks with the existing wavelength absorption.

**The mapping lives on the Hazards side** (`HazardEffect.SignatureFor` / derived `[JsonIgnore] Signature`: HeatDamage→Thermal, RadiationDamage→HardRadiation, KineticDamage→Kinetic; stat kinds → null), so the keystone enum in Damage keeps **no upward dependency**.

**Gauges:** `Pulsar4X.Tests/DamageSignatureTests.cs` (the vocabulary maps onto the hazard kinds + the wavelength-path split) and `DamageSignatureResistanceTests.cs` (`FromWavelength_nm` classification + the **payoff**: through the real sim on a real ship, thermal-rated armour takes less damage from a thermal hit than unrated). **Still to come:** the discovery→research loop that UNLOCKS a signature-rated armour (so the resistance is earned, not hand-set).

---

## Armour material → damage resistance: the per-design link (FIXED 2026-06-28 — load-bearing)

**The hole (found before building the research loop, exactly the kind that quietly breaks cradle-to-grave).** The whole "the armour material IS the counter" premise — both the existing `WavelengthAbsorption` AND the new `SignatureResistance` — only works if a ship's damage-profile **bitmap encodes the material it's actually clad in** (the sim reads each pixel's R-channel as a `DamageResistBlueprint` IDCode: `DamageResistsLookupTable[px.r]`). It didn't:
- Interior **component** pixels were hard-coded `255` (stainless) — `ComponentPlacement.CreateComponentByteArray` line ~52 (a documented "for now").
- **Armour** pixels used a **density-derived byte** (`ComponentPlacement.CreateShipBmp`), which only *coincidentally* equals 255 for stainless and misses for other materials (e.g. aluminium's density mapped to ~171, but its real IDCode is 150 — so the lookup MISSED and that armour was effectively transparent to damage). So a ship's chosen armour material did **not** reliably drive its resistance.

**The fix.** `DamageResistBlueprint` now exposes `MaterialID` (the JSON already had it — `"stainless-steel"`, `"aluminium"`, …; the class just wasn't reading it). `DamageTools.IDCodeForMaterial(resourceId)` maps an `ArmorBlueprint.ResourceID` → the matching blueprint's IDCode (fallback 255). `ComponentPlacement.CreateShipBmp` paints armour pixels with `IDCodeForMaterial(shipProfile.Armor.armorType.ResourceID)` instead of the density byte — so a ship is hit **as the material it's clad in**, and its `WavelengthAbsorption` + `SignatureResistance` actually apply.

**Still simplified (documented, not a bug):** interior **component** pixels remain a flat 255. Components are made of mixed materials; mapping them is a later pass. Armour is the outer defence the player chooses, so it's the high-value layer and the one wired. A beam crosses armour (now material-correct) then components (255).

**Gauge:** `Pulsar4X.Tests/ArmorMaterialWiringTests.cs` — `IDCodeForMaterial` maps each material to its IDCode (unknown→255), and a ship's damage-profile bitmap paints armour pixels with its **actual** armour-material IDCode. This is the pipe a researched, signature-rated armour rides; without it, researched armour would build but never resist.

---

## Active Path: DamageComplex (beam hits — wired as of Phase 1a/2)

`BeamWeaponProcessor.OnHit()` → `DamageProcessor.OnTakingDamage()` → `DamageTools.DealDamageEnergyBeamSim()`.

`SimpleDamage` is no longer on the beam hit path. `Simple/SimpleDamage.cs` remains in the project but is not called from any active code path.

### DamageFragment struct (in `DamageTools.cs`)
```csharp
public struct DamageFragment
{
    public Vector2 Velocity;
    public (int x, int y) Position;
    public double Energy;       // joules
    public float Mass;
    public float Momentum;
    public float Density;       // kg/m³
    public float Length;
    public double Wavelength;   // nm; 0 = kinetic/non-photon
}
```

### DamageResistBlueprint (in `DamageTools.cs`)
```csharp
public class DamageResistBlueprint : Blueprint
{
    public byte IDCode;
    public int HitPoints;
    public int MeltingPoint;
    public float Density;
    // Bands: UV(0-400nm)=0, Vis(400-700nm)=1, NIR(700-2000nm)=2, MIR(2000-5000nm)=3, FIR(5000+nm)=4
    // 0.0=transparent, 1.0=fully absorbing
    public float[] WavelengthAbsorption = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
}
```
JSON must use `"UniqueID"` field for the byte IDCode (mapped via `[JsonProperty("UniqueID")]` on constructor parameter). `WavelengthAbsorption` arrays are populated from JSON after construction. Data file: `GameData/basemod/TemplateFiles/damageResistance.json`.

**Gotcha — the `[JsonProperty("UniqueID")]` on the ctor param consumes the JSON token, leaving base `Blueprint.UniqueID` null.** `ModLoader.ApplyModGeneric` uses `Blueprint.UniqueID` as its dictionary key, and a null key throws `ArgumentNullException` — this crashed New Game / mod loading. Fix in place: the constructor now sets `UniqueID = iDCode.ToString()` so the string key and the byte channel stay in sync. Do not remove that line.

### DealDamageEnergyBeamSim() — how it works now
- Traverses ship damage bitmap pixel by pixel from entry point toward center.
- Loop stops when energy falls below 0.1% of starting energy, or beam exits bitmap.
- Per pixel: looks up material by R channel (IDCode). Unknown materials are transparent (beam passes through).
- Beer-Lambert absorption: `energyDeposited = energy * absorption_coefficient`. `energy -= energyDeposited`.
- 1 damage point per 100J deposited (`damageAmount = Max(1, (int)(energyDeposited * 0.01))`).
- Damage applied in `DamageProcessor`: `HealthPercent -= damageAmount * 0.001f` (1000 points = 100% health).

### EntityDamageProfileDB
- Created from `ShipInfoDB.Design` when a ship first takes damage (lazy creation).
- `ComponentLookupTable: List<ComponentInstance>` — G-channel is 1-indexed but table is 0-indexed (off-by-one gotcha).
- Should ideally be pre-created at ship construction, not lazily.

### OnColonyDamage() — orbital bombardment (wired, in DamageProcessor.cs)

`DamageProcessor.OnTakingDamage()` routes to `OnColonyDamage()` when the target has `ColonyInfoDB` but not `EntityDamageProfileDB` (i.e., it's a colony, not a ship).

**Energy → damage strength:** `damageStrength = Max(1, (int)(energy / 1e8))`. 100 MJ = 1 unit. A typical missile warhead (1–100 TJ) yields 10,000–1,000,000 units. Adjust the divisor when warhead energies are finalized.

**Population casualties:** `quarter_million × damageStrength`, capped at total population. Distributed proportionally across species.

**Atmospheric contamination:** `AtmosphericDust` and `RadiationLevel` on the colony's planet both increase by `damageStrength × 0.001`, capped at 1.0 and 10.0 respectively.

**Installation damage:** picks random `ComponentInstance`s from `ComponentInstancesDB.AllComponents`, drains `HealthPercent` until the damage budget is spent or 20 consecutive misses exhaust the attempt cap. Destroyed installations are removed via `RemoveComponentInstance()`. Calls `ReCalcProcessor.ReCalcAbilities()` after.

**Important:** `OnColonyDamage` is only reachable if a weapon actually targets the colony *entity*, not just the planet body. Missiles must have their target set to the colony entity. Currently only beam weapons call `OnTakingDamage()` — missile guidance is incomplete (see `Weapons/CLAUDE.md`).

### OnStationDamage() — the station grave rung (Slice B, 2026-07-03)

`OnTakingDamage()` routes to `OnStationDamage()` when the target has `StationInfoDB` but is neither a ship nor a colony. It is the parallel to `OnColonyDamage()` and **shares** its two heavy passes (`ApplyPopulationCasualties`, `ApplyInstallationDamage`), so a station and a colony can never drift apart in how a strike kills people / wrecks installations. Two deliberate differences make a station the cheap, fragile alternative to a planet:

- **No atmospheric contamination** — a sealed habitat has no atmosphere to poison.
- **A structural-integrity KILL trigger** — the hit drains `StationInfoDB.StructuralIntegrity` (a flat placeholder pool, base 500) and, at ≤ 0, calls `StationFactory.DestroyStation()` and returns `Destroyed = true`. A colony has NO such trigger (a planet is effectively infinite on this scale) — that ratio IS the design's durability asymmetry. **Placeholder — tune when the station durability/invasion numbers lock (`docs/SPACE-STATIONS-DESIGN.md`).**

Before this, a station had no branch here and `OnTakingDamage` returned `Damage = 0` — a "ghost target" that could be fired on but never damaged. Only the DIRECT weapon-hit path (beam/missile) reaches it; the fleet auto-resolve engine (`Combat/CombatEngagement`, `FleetDB`-keyed) does not yet see stations — a follow-on.

---

## DamageVeryComplex — The New Physics Sim

This is the active direction on DevBranch. It's a full 2D particle physics simulation:

- A `DamageMap` is a grid of `PhysicalParticle` objects representing a cross-section of the target.
- When a weapon fires, particles (kinetic slugs) or photon beams enter the map at a given velocity.
- `DamagePhysicsSim.PhysicsLoop()` runs until all particles stop moving:
  - Updates particle positions each tick.
  - Detects and resolves elastic/inelastic collisions between particles.
  - Processes photon beams via `PhotonMath`.
  - Recursively spawns sub-maps for very fast particles (multi-scale simulation).
- `AsteroidDamage.cs` is the only fully-wired caller today — asteroid kinetic strikes use this path.
- Beam weapon damage (lasers) is partially wired: `PhotonMath.BeamProcessing()` exists but the integration with `BeamWeaponProcessor` is not complete.

**This system is more ambitious than Aurora's armor-grid model.** It's also incomplete. Before building ground combat damage on this, confirm with the developer whether to:
- Use this system for ground combat (scientifically accurate, high complexity)
- Use a simplified version mirroring DamageComplex (closer to Aurora's per-component HTK model)

---

## Path Decision (Made)

| Path | Status | Role |
|------|--------|------|
| `SimpleDamage` | No longer called | Dead code — preserved in case needed for testing |
| `DamageComplex` | **Active for beam hits** | Forward path for ship and future ground combat damage |
| `DamageVeryComplex` | Active for asteroid impacts | Complex particle physics sim — not used for beam hits |

**DamageComplex is the forward direction.** Ground combat damage (AP rounds, artillery, orbital strikes) extends from this path.

---

## Ground Combat Extension Points

**Colony orbital bombardment** — done. `OnColonyDamage()` handles population, atmosphere, installation damage, **and (2026-07-06) softening the DEFENDING GROUND GARRISON** when a weapon hits a colony entity.

**Garrison softening (2026-07-06) — the space→ground link for "take a planet".** `OnColonyDamage` now calls `ApplyGroundBombardment(planetBody, colonyOwnerFaction, damageStrength)`: an AREA strike deals `GroundBombardmentDamagePerStrength` (**0.01, flagged placeholder**) × `damageStrength` RAW health to every **defending** unit (the colony owner's) on the body, **REDUCED by that unit's own defences through the same `GroundCombat.GroundDamageMatrix` the ground resolver uses** — treated as an undodgeable `Artillery`-class attack, so dodge doesn't help but a **shield soaks a fraction** and **flat armour bounces a little** off the (single, big) source. So a shielded/armoured garrison genuinely resists softening — "build to survive the bombardment" is a real decision. Dead units removed. **+ ARMOUR NATURE (⚙3, 2026-07-11):** the strike is now passed as EXPLOSIVE-nature HE, so a unit's armour TYPE matters — reactive plating (`ArmourVsExplosive` > 1) resists the softening where a plain plate (natureFactor 1.0) is byte-identical. "Build to survive the bombardment" now includes armour type, not just amount. Gauge: `GroundBombardmentTests.OrbitalBombardment_ReactivePlatingResistsBetter`. Defensive (no `GroundForcesDB` / no garrison → no-op, so every existing colony-damage test is byte-identical). v1 flags: whole-surface (not region-targeted); softens only the DEFENDER (friendly-fire on a landed invader is a v2 targeting nuance); bombardment classed as `Artillery`. Calibration is tied to the same unfinalized warhead-energy scale as the colony divisor (gotcha #2) — a beam (strength ~1) barely scratches, a missile/heavy strike genuinely softens. Gauge: `GroundBombardmentTests` (defenders lose health + some die; a non-defender invader is untouched; **a shielded defender resists better than an identical unshielded one**).

**Remaining work for full ground combat integration:**
1. **Region-targeted** orbital strikes (hit a chosen region's units, not the whole surface).
2. Optionally a `GroundUnitDamageProfileDB` mirroring `EntityDamageProfileDB` if per-unit spatial damage is ever wanted (v1 uses flat health drain on the data-object units, which is enough).
3. Wire the missile guidance path to actually call `OnTakingDamage()` on impact — currently missiles are launched but never connect (guidance incomplete, see `Weapons/CLAUDE.md`).

---

## Gotchas

1. **`EntityDamageProfileDB` is lazily created.** The first hit on a ship without this blob creates it inline. This means the first hit's spatial placement may be slightly wrong (the profile is built from current state, not initial state). Fix: create at ship construction.

2. **Colony damage energy scaling needs real warhead data.** The 1e8 J/unit divisor in `OnColonyDamage()` is a placeholder. When missile warhead energies are finalized in `ordnanceDesigns/`, calibrate this to produce meaningful but survivable damage per strike. Population casualties (250k/unit) and atmospheric contamination (0.001/unit) scale from the same divisor.

3. **`DamageTools.DealDamageEnergyBeamSim()`** is the spatial simulation kernel. Read it carefully before wiring — it computes hit location against a component placement grid. If `ComponentPlacement` data is not populated, results will be wrong.

4. **Missiles now deliver damage (was: "don't deliver damage yet").** `MissileImpactProcessor` (an `IHotloopProcessor` in `Weapons/WeaponMissile/`) checks proximity every second and calls `DamageProcessor.OnTakingDamage()` on impact, which routes colony hits to `OnColonyDamage()`. Missile guidance fixed 2026-06-21 (`directAttack = true`). Calibration is still placeholder — kinetic energy at orbital closing speeds is GJ-scale, far above the 1e8 J/unit colony divisor and the kJ–MJ beam tuning. See `Weapons/CLAUDE.md`.
