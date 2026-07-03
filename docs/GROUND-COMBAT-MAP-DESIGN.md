# Ground Combat — The Planet Map (Design Lock)

**As of 2026-07-03.** Status: **design-locked; slice 1 (region layer) building.**
Companion to `docs/aurora/GROUND-COMBAT.md` (the combat spec) and `docs/SPACE-STATIONS-DESIGN.md` (the host-pattern this reuses). Read `docs/MVP.md` for the scope firewall.

---

## What this is, and why it exists

Before you can fight over a planet, the planet has to be a **place** — somewhere with locations you build at, distances you march across, and geography you discover. Today a planet in Pulsar is a *dimensionless point*: a colony sits on it as an abstract bag of population and buildings with no "where." This design gives the planet a **surface** without lying about its shape.

The north star (the developer's own framing): **4 big slices you can zoom into at high accuracy.** A world is cut into a small ring of regions; you build in them, move between them, and defend them. The map must survive three truths a flat map breaks:

1. **The Pacific theatre must survive** — a flat map split down a seam destroys the ocean between two shores. → topology, not a projection.
2. **Africa is actually huge** — flat maps shrink the equator. → regions carry real **area**.
3. **A straight line isn't the fastest road** — the shortest path on a ball curves. → distance is a **travel-time on the adjacency**, measured along the sphere, not flat-map pixels.

(The interactive study that motivated this: the four instruments — wrapping regions, true size, great-circle pathing, the globe.)

---

## The locked model: nested — strategic ring + tactical hex

| Layer | What | Status |
|---|---|---|
| **Strategic** | A **region graph on the planet** — v1 is **4 longitude slices in a ring**. Topology-correct (no seam → Pacific survives). Each region carries area + adjacency-with-travel-time + a feature bundle + a surveyed flag. | **building (slice 1)** |
| **Tactical** | The existing **colony hex map** (`ColonyHexMapDB`) — a local battlefield that drops *inside* a region when you zoom to "high accuracy." | exists, deferred (persistence fix required first) |

The ring is seen from the pole: you view the region you're in plus its two neighbours (3 of 4), and because it's a ring there is no edge to fall off — marching "off the east edge" of region 4 enters region 1. That is the developer's "see 3, wrap the seam" model, and it *is* a region graph.

**Why a graph, not a real sphere of coordinates:** the player decides "march the army from the highlands to the coast," not "move to lat 52.5, lon 13.4." Continuous spherical positions are realism the player never acts on — "pretty," per `docs/REALISM-VS-GAMEPLAY-AUDIT.md`. The graph carries the *decisions* (where, how far, what terrain); a globe view (later) is an optional pretty skin on the same graph.

---

## The region schema (`GameEngine/Galaxy/PlanetRegionsDB.cs`)

A **`PlanetRegionsDB`** is attached to the **planet body entity** (the parallel to how a colony/station is a host — regions are *of the planet*, so oceans and empty continents exist whether or not anyone colonised it). It holds a list of **`Region`**, each carrying:

- **`Index`** + ring **`Neighbors`** (east/west) — the seam-free topology.
- **`Area_km2`** — the true-size datum (from the body's radius, split across the ring with variation).
- **`CrossingTimeSeconds`** — the distance datum movement will read (from region width ÷ a placeholder march speed). Data now; movement later.
- **`Surveyed`** — features are only "known" once true. **Authored worlds (Earth) start surveyed; procedurally generated worlds start UNKNOWN until scanned** — this is where exploration meets the map.
- **`Features`** — a **bundle** of `RegionFeature` (`Ocean`, `Coast`, `Mountains`, `Forest`, `Desert`, `Ice`, …), *not* a single type. A region is mountains + forest + coast at once.
- **`InstallationIds`** — what's built here (populated by the build-at-a-region slice).

**Persistent from day one** (`Clone()` + `[JsonProperty]` + deep-copy ctors). The earlier `ColonyHexMapDB` was fatally non-persistent (no `Clone`); we do not repeat that.

## Generation (`GameEngine/Galaxy/PlanetRegionsFactory.cs`)

`GenerateForSystem(system, surveyed)` — mirrors `LagrangeFactory`: defensive, idempotent, seeded by the system RNG. For each major body (planets/giants/dwarfs) it builds 4 ring regions and rolls **random-but-logical** features from the world's own reliable scalars — a wet world (`AtmosphereDB.HydrosphereExtent`) gets ocean/coast; a tectonically active world gets mountains; a gas giant gets gas layers. `surveyed` is **false** from the procedural path (`CreateSystem`) and **true** from the authored/blueprint paths (`LoadFromBlueprint`, the Sol builders) — so Earth is known and Alpha Centauri is a mystery until you scan it. Hooked at all four gen paths in `StarSystemFactory`.

Gauge: `Pulsar4X.Tests/PlanetRegionsTests.cs` — 4-region ring + wrapping adjacency, area/crossing-time, authored-surveyed, wet-world-has-ocean (the "logical" gauge), deep-clone (persistence), idempotency.

---

## Scope: this is the *map*, not the *battle*

`docs/MVP.md`'s "take a planet" finish line is the combat spine (build a unit → drop → auto-resolve vs. the garrison → flip ownership) and needs **no map**. The developer's priority is the opposite axis — the **living map** (where things are, how far, who defends what). So we build the *place* first. That's a deliberate, eyes-open order-of-operations call, not a detour.

### Slice plan (each its own CI-gated commit)

1. **Region layer** — `PlanetRegionsDB` + generation + tests. **✅ built + green.**
2. **Build at a region** — `PlaceInstallationInRegionOrder`: place an installation *in a chosen region* via the real `OrderHandler.HandleOrder` player-path (deploy-at-a-location, one level down from the station deploy), commanded by the colony. Records the instance id in `Region.InstallationIds` — a **new located axis**; the colony's existing abstract installations are left untouched. **✅ built** (gauge `PlanetRegionsTests.BuildInRegion_*`). *(Refinements: materials + build-time via a region-targeted `IndustryJob`; building on an uncolonised region via a construction unit.)*
3. **Planet view (client)** — a flat 3-region `PlanetViewWindow` (an ImGui window modelled on `ColonyHexMapWindow`, reachable from the planet's context menu). CI-blind → local-build check. **← next.**
4. **Survey reveal** — scanning a world flips `Surveyed` and reveals its features; wires into `GeoSurveys`/`Sensors` (the detection-quality fix already landed as the prerequisite).
5. **Ground units** — `GroundUnitDesign : IConstructableDesign` + `GroundForcesDB` + a `GroundCombatProcessor` mirroring `AutoResolve`; move between regions on the crossing-time edges; capture by flipping `FactionOwnerID`.
6. **Tactical hex** — the existing `ColonyHexMapDB` battlefield nested inside a region (persistence fixed first); the "zoom to high accuracy" layer.

Deferred beyond that: the to-scale globe renderer, base-defends-adjacent-city coverage, terrain combat modifiers, gas-giant platform hosts (reuse the station host), and colony-onto-region migration.

---

## Connections (Prime Directive)

- **Galaxy / `StarSystemFactory`** — generation hook (done, 4 paths). **`SystemBodyInfoDB` / `AtmosphereDB`** — the feature-gen inputs (body type, tectonics, hydrosphere).
- **Colonies** — a colony sits on a planet that now has regions; **not** disturbed in slice 1 (new axis). Migration later.
- **Stations** (`docs/SPACE-STATIONS-DESIGN.md`) — the host pattern this copies; a gas-giant "platform" region reuses the station host later.
- **Industry / `IConstructableDesign`** — installations and (later) ground units build on the existing production rails; "build at a region" adds only a *where*.
- **Orders / `OrderHandler`** — build-at-a-region rides the verified player-path.
- **GeoSurveys / Sensors** — the survey-reveal loop (slice 4).
- **Combat / `AutoResolve`** — the unit-agnostic resolver ground battles reuse (slice 5).
- **Damage** — orbital bombardment (`OnColonyDamage`, already live) later targets regions.
- **Save/Load** — `PlanetRegionsDB` round-trips (persistent by construction).

---

## Landmines (from the survey)

- **`ColonyHexMapDB` is not save-safe** (no `Clone`/`[JsonProperty]`) — fix before nesting it (slice 6).
- **`InstallationsDB` is a corpse** — military installations are plain `ComponentDesign`s, never that blob.
- **Docs claiming orbital bombardment is "commented out" are stale** — `DamageProcessor.OnColonyDamage` is live.
- **Don't route ground damage through the per-pixel sim** — use the strength-math `AutoResolve`.
