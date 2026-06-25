# Combat — Subsystem Reference (Auto-Resolve Engine)

The **one** combat engine for this fork: a math loop that resolves fleet battles off each ship's "spec sheet," with **doctrine** as the player's only lever. Lives in `GameEngine/Combat/`. This is the v1 spine described in `docs/COMBAT-DESIGN.md` -> "What we're building (v1)".

It deliberately does **not** use the per-pixel damage sim (`Damage/DamageComplex` / `DamageVeryComplex`). That path deposits ~0 damage today (see `Damage/CLAUDE.md`) and is parked as a v2 visual skin. The auto-resolver decides casualties by **strength math**, not by simulating hits.

> **Status: under construction.** Built piece-by-piece, each under a test, in the order in `docs/COMBAT-DESIGN.md` -> "Build order". This file grows as each piece lands.

---

## File Map

| File | Purpose | Status |
|------|---------|--------|
| `ShipCombatValueDB.cs` | DataBlob: a ship's **Firepower** (joules/sec from beams + a missile-launcher stub) and **Toughness** (live components + armour), plus a **RoleWeight**. Computed once at build time. `ShipCombatValueDB.Calculate(Entity)` is the calculator. | ✅ built (spine step 2) |

*(rows added as the auto-resolve loop, `FleetCombatStateDB`, doctrine, fleet components, and retreat land.)*

---

## ShipCombatValueDB — the spec sheet

**What it is.** Two numbers that rate a ship for the auto-resolver, read from the ship's REAL parts the moment it's built and cached on the entity:

- **`Firepower`** — hurt-per-second. Each beam weapon contributes `Energy ÷ ChargePeriod` (joules/sec), scaled by that component's `HealthPercent`. Each missile launcher adds a flat `MissileLauncherFirepowerStub` (v1 stub).
- **`Toughness`** — how much it can take, **in joules absorbed**. Each live component contributes `HealthPercent × ComponentHitPoints_J` (1e5 J kills a component — straight from the damage tuning: 1000 dmg-points × 100 J), plus `armour.thickness × ArmorHitPointsPerThickness_J`. Same currency as `Firepower × time`, so the salvo loop's time-to-kill comes out in seconds.
- **`RoleWeight`** — `1.0` for anything that can shoot, `UtilityRoleWeight` (0.25) for a utility hull. The auto-resolver uses it so utility/transport ships are low-priority targets (absorb casualties last) and contribute less strength. v1 stub.

**Where it's computed.** `ShipFactory.CreateShip()` calls `ship.SetDataBlob(ShipCombatValueDB.Calculate(ship))` after the components are installed. `Calculate` is defensive — a part-less ship rates 0/0 and never throws.

**Prime Directive — connections:**
- **Feeds IN:** `ComponentInstancesDB.AllComponents` (live components + `HealthPercent`); `GenericBeamWeaponAtb` (`Energy`, `ChargePeriod`) via `TryGetComponentsByAttribute`; `MissileLauncherAtb` (presence only, v1); `EntityDamageProfileDB.Armor.thickness`.
- **Feeds OUT:** the auto-resolve loop (spine step 3) sums `Firepower`/`Toughness`/`RoleWeight` over a fleet's ships to get fleet strength. Nothing else reads it yet.
- **Shares STATE:** lives on the ship entity alongside `ComponentInstancesDB` and `EntityDamageProfileDB` (reads them; does not write them).
- **Triggers:** nothing — it's a passive cached value.

**Test:** `Pulsar4X.Tests/ShipCombatValueTests.cs` — builds every starting design, asserts each gets a `ShipCombatValueDB` with toughness > 0, logs firepower (`[combat-value]`), and asserts firepower > 0 for any design carrying a beam weapon.

---

## Model-coupled / tuning constants

| Constant | Value | Meaning | Where |
|----------|-------|---------|-------|
| `MissileLauncherFirepowerStub` | 100,000 | flat firepower (J/s) per missile launcher until ordnance warhead energy is wired (v2) | `ShipCombatValueDB.cs` |
| `UtilityRoleWeight` | 0.25 | combat-value role weight of a hull with no weapons | `ShipCombatValueDB.cs` |
| `ComponentHitPoints_J` | 100,000 | joules one component absorbs before destruction (= the damage tuning's "100 kJ kills a component") | `ShipCombatValueDB.cs` |
| `ArmorHitPointsPerThickness_J` | 100,000 | joules of toughness added per unit of armour thickness | `ShipCombatValueDB.cs` |

---

## Gotchas

1. **This engine does not touch the per-pixel damage sim.** Casualties are whole-ship removal driven by strength math, not `DamageProcessor.OnTakingDamage`. Do not wire combat value into the pixel sim — that path is broken and parked for v2.
2. **Combat value is computed once at build (v1).** Recalc-on-damage is a v2 refinement; in v1 a ship is alive at full value or removed whole, so a value cached at build is sufficient.
3. **`ComponentInstancesDB.AllComponents` is `internal`.** Combat code reads it because it lives in the same `GameEngine` assembly; tests reach it via `InternalsVisibleTo("Pulsar4X.Tests")`.
4. **Firepower mixes precise beam J/s with a flat missile stub.** The number is a *relative* strength figure for the salvo math, not a physical unit — don't read absolute meaning into it until missile ordnance energy is wired.
