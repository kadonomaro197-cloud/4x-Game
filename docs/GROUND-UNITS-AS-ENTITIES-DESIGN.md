# Ground Units as Entities — the "abilities just fall out" migration (Option A)

**As of:** 2026-07-07 · branch `claude/sol-playtest-earth-map-8r59j6` · **status: design-locked, build not started**

## Why (the developer's call)

A ship's radar "just works" because a ship is a live **entity that keeps its components** — a processor scans the ship's component list, finds the sensor, done. A ground unit today is the opposite: a **flat snapshot of ~6 combat numbers** (`GroundUnit` in `GroundForcesDB.cs`) copied at build time, with its components **thrown away** by the assembler (`GroundUnitAssembly.ToGroundUnitDesign` flattens frame+parts into fixed fields, `:157-171`). So no ability can "fall out" — the unit doesn't carry the radar, only those numbers.

**Decision (2026-07-07): promote ground units to real entities with components (Option A).** Then every ability — a radar's map-reveal, an engine's speed, crew cost, weapons, future gear — falls out of the SAME component infrastructure ships already use (`ComponentInstancesDB` + `TryGetComponentsByAttribute<TAtb>()`), with zero per-ability special-casing. Speed, crew, radar, and "compose an arbitrary ability" (audit holes 1/3/4) are all symptoms of the flattening; this fixes the one root cause. The design UI is the existing designer, not a new window (hole 5).

## The radar payoff (what this unlocks, concretely)

A "radar" is just a **sensor/detection component with a range**, mounted on a unit you design. Each tick the unit "sees and surveys" the hexes within that range — the same way an entity with a sensor would. The one translation needed: **real radar range (km) → hex reach on the planet map**, since a hex's real size differs body-to-body. Half of it exists: `GroundRangeTools.HexPitchKm(region)` = km-per-hex, so `hexReach = radarRangeKm / hexPitchKm`, then reveal every surface-grid hex within `hexReach` of the unit's position (flip `Region.Surveyed` / the finer hex fog). Reuse the geo-survey reveal call chain — the survey component already calls the reveal; a grounded unit carrying a survey/sensor component just does it in place. **No new reveal attribute is invented.**

## The model

A raised ground unit becomes an **Entity** in the planet body's manager (or a dedicated ground manager), carrying:
- `ComponentInstancesDB` — its mounted components (chassis, weapons, armour, reactor, magazine, **sensor/radar**, …), instantiated from its design. THIS is where abilities live and fall out.
- A small `GroundUnitDB` datablob — the per-unit state that isn't a component: `RegionIndex` / hex position, `FactionOwnerID`, `Health`, ammo pool, formation id, march path. (The combat-stat *snapshot* stays as a cached read-model, recomputed from components like `ShipCombatValueDB`.)
- Standard blobs as needed: `NameDB`, `OrderableDB` (per-unit orders — a v2 unlock this enables), position.

Both current design paths unify onto "a unit is a set of component instances":
- **Assembler path** (`GroundUnitAssembly`): frame + parts → the unit's components (stop discarding them).
- **Monolithic base-mod path** (`GroundUnitAtb` on one `ComponentDesign`): the one component IS the unit's component (v1 keeps working; a chassis+parts design is the richer form).

## Migration — strangler-fig, each a CI-GREEN slice

The existing `GroundUnit` data-object is read all over (resolver, movement, formations, capture, the client map, save/load). We do NOT big-bang it. We add the entity backing alongside the snapshot, route NEW capabilities (radar, speed, crew) through the entity, and migrate the old readers one at a time.

1. **Design keeps its components.** `GroundUnitDesign` gains a component list (design-id → count); `GroundUnitAssembly` populates it (frame + parts); the monolithic path records its single component. Additive — nothing reads it yet. Gauge: an assembled/monolithic design carries its component list.
2. **Raised unit gets an entity backing.** `RaiseUnit` builds a backing `Entity` + `ComponentInstancesDB` from the design's component list; `GroundUnit.BackingEntityId`. Additive — combat/movement still read the flat snapshot. Gauge: a raised unit's entity carries the design's components; save/load round-trips it; `GameLoopSmokeTests` stays green (no processor wakes wrongly).
3. **Radar reveal falls out (the payoff).** A sensor/detection component gives a range; a `GroundSensors` pass reads the unit-entity's sensor component, translates range→hex reach, and reveals hexes each tick. Gauge: a unit whose design carries a radar reveals a fogged region within reach; one without doesn't; range→hex is body-correct.
4. **Speed falls out.** March time reads the unit's chassis `Locomotion` / an engine component instead of the region-only datum. Gauge: a high-mobility design marches faster than a foot unit over the same ground.
5. **Crew / minimal-support falls out.** Crew is summed from the unit's components (like a ship); "low support" = few crew / low power, already gated for power. Gauge: crew is a computed, designable cost.
6. **Composition + UI.** The assembler already composes; expose it through the **existing designer** (hole 5) so the player builds chassis + radar + weapons. Retire the flat snapshot once every reader is off it.

## Risks / invariants

- **Perf + save size:** an entity per unit is heavier than a struct-in-a-list — a 1,000-unit garrison was the reason for the data-object choice. Keep `GroundUnitDB` lean; the combat snapshot stays a cache. Watch `PerformanceReadoutSmokeTests` / save size.
- **Processor scheduling (GameEngine gotcha #5):** adding entities/blobs must go through `SetDataBlob` so the right processors arm and empty managers don't churn. A stray hotloop keyed to `GroundUnitDB` must be trivial-ctor + never-throw (L4/L1).
- **Save/load (gotcha L3):** new `*DB` types embed their type name — no renames after they ship without a converter.
- **Client:** `PlanetViewWindow` reads `GroundForcesDB.Units` — it keeps working through the snapshot until the final migration step; the entity backing is invisible to it until we point it at components.

## Status board

| Slice | State |
|-------|-------|
| 1 design-keeps-components · 2 entity-backing · 3 radar-reveal · 4 speed · 5 crew · 6 compose+UI | ⚫ not started (design-locked) |
