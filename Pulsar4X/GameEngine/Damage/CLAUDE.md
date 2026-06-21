# Damage — Subsystem Reference

Three implementations exist: `Simple/` (dead code), `DamageComplex/` (active — ships and now colonies), and `DamageVeryComplex/` (active for asteroids). Lives in `GameEngine/Damage/`.

---

## File Map

| File | Purpose |
|------|---------|
| `Simple/SimpleDamage.cs` | Dead code. Not called from any active path — preserved for test reference. |
| `DamageComplex/DamageProcessor.cs` | Active damage path. `OnTakingDamage()` routes ships through `DealDamageEnergyBeamSim()` and colonies through `OnColonyDamage()`. |
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

### OnColonyDamage() — orbital bombardment (wired, in DamageProcessor.cs)

`DamageProcessor.OnTakingDamage()` routes to `OnColonyDamage()` when the target has `ColonyInfoDB` but not `EntityDamageProfileDB` (i.e., it's a colony, not a ship).

**Energy → damage strength:** `damageStrength = Max(1, (int)(energy / 1e8))`. 100 MJ = 1 unit. A typical missile warhead (1–100 TJ) yields 10,000–1,000,000 units. Adjust the divisor when warhead energies are finalized.

**Population casualties:** `quarter_million × damageStrength`, capped at total population. Distributed proportionally across species.

**Atmospheric contamination:** `AtmosphericDust` and `RadiationLevel` on the colony's planet both increase by `damageStrength × 0.001`, capped at 1.0 and 10.0 respectively.

**Installation damage:** picks random `ComponentInstance`s from `ComponentInstancesDB.AllComponents`, drains `HealthPercent` until the damage budget is spent or 20 consecutive misses exhaust the attempt cap. Destroyed installations are removed via `RemoveComponentInstance()`. Calls `ReCalcProcessor.ReCalcAbilities()` after.

**Important:** `OnColonyDamage` is only reachable if a weapon actually targets the colony *entity*, not just the planet body. Missiles must have their target set to the colony entity. Currently only beam weapons call `OnTakingDamage()` — missile guidance is incomplete (see `Weapons/CLAUDE.md`).

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

**Colony orbital bombardment** — done. `OnColonyDamage()` handles population, atmosphere, and installation damage when a weapon hits a colony entity.

**Remaining work for full ground combat integration:**
1. Create `GroundUnitDamageProfileDB` mirroring `EntityDamageProfileDB` for ground unit formations.
2. Add ground unit damage routing in `OnTakingDamage()` — check for `GroundUnitDB` (to be created) the same way colony checks for `ColonyInfoDB`.
3. Wire the missile guidance path to actually call `OnTakingDamage()` on impact — currently missiles are launched but never connect (guidance incomplete, see `Weapons/CLAUDE.md`).

---

## Gotchas

1. **`EntityDamageProfileDB` is lazily created.** The first hit on a ship without this blob creates it inline. This means the first hit's spatial placement may be slightly wrong (the profile is built from current state, not initial state). Fix: create at ship construction.

2. **Colony damage energy scaling needs real warhead data.** The 1e8 J/unit divisor in `OnColonyDamage()` is a placeholder. When missile warhead energies are finalized in `ordnanceDesigns/`, calibrate this to produce meaningful but survivable damage per strike. Population casualties (250k/unit) and atmospheric contamination (0.001/unit) scale from the same divisor.

3. **`DamageTools.DealDamageEnergyBeamSim()`** is the spatial simulation kernel. Read it carefully before wiring — it computes hit location against a component placement grid. If `ComponentPlacement` data is not populated, results will be wrong.

4. **Missiles don't deliver damage yet.** `OnColonyDamage()` is wired and ready, but the missile guidance path never calls `OnTakingDamage()` on impact. Ground combat orbital bombardment depends on fixing missile guidance first.
