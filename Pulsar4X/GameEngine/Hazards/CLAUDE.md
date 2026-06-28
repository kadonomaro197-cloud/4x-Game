# Space Hazards — Subsystem Reference

**What it does:** Puts *terrain* into space. A "space hazard" is a circular region that changes how ships behave while they're inside it. Two flavours ship today, built on one shared component:

- **Gas cloud** — permanent terrain in some (non-home) systems. Inside it your sensors are cut, sub-light movement is dragged, warp crawls, and your hull slowly corrodes. A place to hide, get ambushed, or have to cross.
- **Solar flare** — the home star's *weather*. On a schedule the star erupts a flare that grows near it, blinds sensors in the area and irradiates any ship caught there, then fades and is gone.

**Why it matters:** before this, empty space was truly empty — planets are tiny dots separated by astronomical gaps and nothing filled them. Hazards give the map geography to fight, hide and route around, which is what makes movement and detection *decisions* instead of straight lines.

---

## Files

| File | Role |
|------|------|
| `SpaceHazardDB.cs` | The shared component (and `SpaceHazardType` enum). A circular region — centred on the entity's `PositionDB`, reaching `Radius_m` — with generic effect knobs: `SensorRangeMultiplier`, `MoveSpeedMultiplier`, `WarpSpeedMultiplier`, `DamagePerSecond`, `BlindsSensors`, plus transient-flare lifecycle fields (`IsTransient`/`StartedAt`/`ExpiresAt`/`MaxRadius_m`). |
| `StarFlareSourceDB.cs` | Marks a star as able to flare and holds its schedule (`NextFlareTime`, mean gap, duration, peak radius). |
| `SpaceHazardTools.cs` | Read-side query. `CombinedAt(system, position)` / `CombinedForEntity(entity)` → a `HazardModifiers` (multipliers multiply, damage sums, blind = any). This is how the sensor/warp code asks "is this point inside a hazard, and what does it do?" |
| `SpaceHazardFactory.cs` | Builds the hazard entities — `CreateGasCloud(...)` and `CreateSolarFlare(...)`. |
| `SpaceHazardProcessor.cs` | `IHotloopProcessor` (5 s, keyed to `SpaceHazardDB`). Grows/fades/expires transient flares, and applies the **per-tick** effects — hull DAMAGE (via the wired `DamageProcessor.OnTakingDamage` path) and Newtonian DRAG — to ships inside. `FlareRadiusAt` (pure, tested) is the grow→peak→fade shape. |
| `SolarFlareProcessor.cs` | `IHotloopProcessor` (1 h, keyed to `StarFlareSourceDB`). When the clock reaches a star's scheduled time it erupts a flare and rolls the next one off the system RNG. |

---

## How the effects are wired (Prime Directive — the connections)

Two kinds of effect, applied two different ways:

- **Per-tick effects (damage, drag)** are *pushed* by `SpaceHazardProcessor`: each tick it finds ships inside each hazard and applies them. Damage rides the existing `DamageProcessor` path (same as a missile hit). Drag scales `NewtonMoveDB.CurrentVector_ms` — so it only bites a ship in **free-flight** (coasting/thrusting through the medium), not one in a stable Kepler orbit.
- **Query-time effects (sensor cut, warp slow)** are *pulled* by the systems they affect, via `SpaceHazardTools`:
  - **Sensors** — `SensorScan` reads the observer's hazard mods and gates contact registration: blinded (flare) → no contacts; sensor-cut (gas cloud) → contacts beyond the observer's *reduced* reach (using the `SensorTools.DetectionRange_m` reverse-solve) are dropped. **Default-identical when no hazard is present**, so existing detection/fog tests are untouched.
  - **Warp** — `WarpMoveProcessor.StartNonNewtTranslation` scales warp speed by the hazard at the ship's departure point (clamped to a crawl, never 0, so the transit-time division can't blow up).

**Placement** lives in `Galaxy/StarSystemFactory.cs`: `LoadFromBlueprint` gives the home star a `StarFlareSourceDB` (first flare within a month); `CreateSystem` gives every generated star one and drops a gas cloud in ~40% of generated systems.

**Rendering** is client-side (CI-blind): `SystemMapRendering.UpdateHazardRegions()` draws each region as a coloured `SimpleCircle` (gas cloud green, flare orange), rebuilt each frame so a growing flare re-reads its size.

---

## Gotchas

1. **Why `StarFlareSourceDB` is separate from `StarInfoDB`.** Only ONE hotloop processor may key a given DataBlob type (`ProcessorManager` is `Dictionary<Type, IHotloopProcessor>`), and `StarInfoDB`'s slot is already taken by the combat battle-trigger. So flare scheduling hangs off its own marker blob on the star.

2. **Drag only affects `NewtonMoveDB` ships.** A ship in a stable orbit (`OrbitDB`, no `NewtonMoveDB`) is *not* slowed — it isn't "ploughing through" the medium in the thrust sense. This is deliberate; don't "fix" it by touching the Kepler/orbit integrator (it's known-fragile — see `Movement/`).

3. **Warp slow is keyed off the DEPARTURE point only (v1).** A ship that starts outside a cloud and warps *through* it isn't slowed; a true block (`WarpSpeedMultiplier` 0) is clamped to a crawl rather than refusing the order. Both are flagged follow-ups.

4. **Damage calibration is borrowed.** `DamagePerSecond` feeds `DamageFragment.Energy` on the same scale as beam/missile damage (~1 point / 100 J, 1000 = 100% health). Gas cloud 50 J/s ≈ slow attrition; flare 500 J/s ≈ noticeable. Tune in the factory if combat damage gets recalibrated.

---

## Tests

`Pulsar4X.Tests/SpaceHazardTests.cs` (engine-only → CI-green):
- `GasCloud_AffectsInsideOnly` — the region query bites inside the radius and nowhere else.
- `FlareRadius_GrowsToPeakThenFades` — the flare shape.
- `HomeStar_HasFlareWeather` — placement wiring (Sol's star gets a `StarFlareSourceDB`).
- `Flare_BlindsThenExpires` — a flare blinds sensors at the star and is removed once it expires.
