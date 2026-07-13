# Aurora 4X — Space Combat (the Depth Benchmark)

*Status snapshot refreshed 2026-07-13.*

Source: aurora-manual `12-combat/` + `8-ship-design/`; Pulsar side documented in `GameEngine/Weapons/CLAUDE.md` and `GameEngine/Damage/CLAUDE.md`.

This is the **bar the objective is measured against**: "bring ground combat and infrastructure to the same depth that space combat already has." Read this to know what "the same depth" *means*, so ground systems aren't under- or over-built relative to the naval side. This is intentionally a benchmark sketch, not a full mechanics dump — Pulsar's naval combat is already implemented; see the Weapons/Damage CLAUDE.md for the code.

---

## What "Aurora-depth combat" includes

| Layer | Aurora | Pulsar status |
|-------|--------|---------------|
| **Detection** | Active/passive sensors, EM/thermal signatures, resolution vs target size, EW jamming | ✅ `Sensors/` (signatures, scanning, contacts) |
| **Fire control** | Beam/missile fire controls with range & tracking-speed; assign weapons to FCs; target a contact | ✅ `Weapons/` generic fire control |
| **Beam combat** | Range-attenuated damage, tracking vs target speed, per-shot to-hit | ✅ `Weapons/WeaponBeam/BeamWeaponProcessor` |
| **Missile combat** | Multi-stage missiles, agility/guidance, point-defence interception, salvos | ✅ `Weapons/WeaponMissile/` — guidance wired (`directAttack=true`, `MissleProcessor.cs:85`; `ThrustToTargetCmd` pursuit + `MissileImpactProcessor` kinetic delivery). Runtime tuning of impact energy still open. |
| **Point defence** | Final-fire / area modes vs missiles | partial |
| **Damage model** | Armor as a layered grid; component HTK; penetration walks through armor columns; shock/secondary | ✅ complex `DamageProcessor` is the live path — beam hits route `BeamWeaponProcessor.cs:144 → DamageProcessor.OnTakingDamage → DamageTools.DealDamageEnergyBeamSim`. `SimpleDamage.cs` is dead code (no gameplay caller). |
| **Shields** | Regenerating shield bubble absorbing hits before armor | ✅ `Combat/ShieldAtb.cs` (component, six-point registered) + `CombatKernel.ResolveShield` / `CombatEngagement.ApplyShield` — a depleting/regen pool wired into the live resolve. |
| **Survivability outcomes** | Component destruction, crew, secondary explosions, wrecks, salvage | partial (`ShipFactory.DestroyShip`, wreck TODO) |

---

## The depth bar, stated plainly

Aurora-depth combat means each of these is true, and they are the targets for ground combat to match:

1. **Units are designed from researched components**, not picked from fixed types. → Ground forces already work this way in Aurora; mirror with Pulsar's component framework.
2. **Damage is resolved per-component with penetration vs armor**, not a single HP bar. → Ground: AP-vs-armor + HP rolls (see `GROUND-COMBAT.md §4`). Naval and ground already share one damage core — `Combat/CombatKernel.cs` (`ArmourSoak` etc.), which the ground side delegates to (`GroundDamageMatrix.cs:45`).
3. **Combat is a scheduled processor over game time**, with supply/ammunition as a real constraint. → Ground combat round = a hot-loop processor; GSP supply mirrors magazine/ordnance.
4. **There is a detection/targeting layer**, not auto-hit. → Ground to-hit formula with terrain/fortification is the analogue.
5. **Outcomes feed the strategic layer** (losses, occupation, captured production). → Ground invasion captures colonies & installations.

---

## Shared damage core (done)

The naval and ground damage paths already share one core: `Combat/CombatKernel.cs` owns the flat-armour soak math (`ArmourSoak`/`ArmourSoakPerPoint`), and the ground resolver delegates to it (`GroundDamageMatrix.cs:45`). The old "fix the complex `DamageProcessor` first, then share it" sequencing is complete — `DamageComplex` is the live wired path for beams/missiles/hazards, and `SimpleDamage.cs` is dead code with no gameplay caller.

See `GameEngine/Damage/CLAUDE.md` and `GameEngine/Combat/CLAUDE.md`, and root `CLAUDE.md` gotcha #1.
