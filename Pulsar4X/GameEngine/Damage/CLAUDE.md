# Damage — Subsystem Reference

Three implementations exist: `Simple/` (active placeholder for beam hits), `DamageComplex/` (partial component-level system), and `DamageVeryComplex/` (new particle physics simulation, active for asteroids). Lives in `GameEngine/Damage/`.

---

## File Map

| File | Purpose |
|------|---------|
| `Simple/SimpleDamage.cs` | Active placeholder for beam hits. Random component selection, random 100–500 HTK damage. |
| `DamageComplex/DamageProcessor.cs` | Partial component-level damage path. `OnTakingDamage()` partially wired; colony damage block is commented out. |
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

**Colony damage block (~lines 101–181) is entirely commented out.** It was the design for orbital bombardment:
- Population casualties based on damage amount
- Atmospheric dust + radiation effects
- Random installation targeting using `MassVolumeDB.Volume_km3`
- `ComponentInfoDB`, `ComponentInstanceData` type references (may have been renamed — verify before uncommenting)

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

## Ground Combat Extension Point (After Path Decision)

For DamageComplex path:
1. Uncomment and update the colony damage block in `DamageProcessor.OnTakingDamage()` (~lines 101–181).
2. Verify `MassVolumeDB.Volume_km3` still exists (it does — verified in Galaxy/).
3. Create `GroundUnitDamageProfileDB` mirroring `EntityDamageProfileDB` for ground units.

For DamageVeryComplex path:
1. Create a `DamageMap` for ground unit cross-sections.
2. Wire `GroundCombatProcessor` to call `DamagePhysicsSim.PhysicsLoop()` with appropriate projectile particles.

---

## Gotchas

1. **`EntityDamageProfileDB` is lazily created.** The first hit on a ship without this blob creates it inline. This means the first hit's spatial placement may be slightly wrong (the profile is built from current state, not initial state). Fix: create at ship construction.

2. **Colony damage code references types that may not exist.** `ComponentInfoDB`, `ComponentInstanceData` in the commented block may have been renamed/removed. Verify each type before uncommenting.

3. **`DamageTools.DealDamageEnergyBeamSim()`** is the spatial simulation kernel. Read it carefully before wiring — it computes hit location against a component placement grid. If `ComponentPlacement` data is not populated, results will be wrong.
