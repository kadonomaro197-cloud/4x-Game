# Galaxy / System Generation Subsystem — Developer Reference

**What it does:** Generates and stores everything that isn't a ship or colony — star systems, planets, moons, asteroids, atmospheres, ruins. This is the world-building layer. When a new game starts, `GalaxyFactory` and `StarSystemFactory` procedurally generate the playing field.

**Why it matters for ground combat:** `AtmosphereDB` is already here and **already implements the Aurora temperature formula** — this is the data Phase 2a (colony cost/habitability) reads to compute whether a world is hostile. The atmosphere system is more complete than any other "to-do" subsystem.

---

## Files

| File | Role |
|------|------|
| `GalaxyFactory.cs` | Top-level generator. Creates the galaxy (multiple systems, warp point network). |
| `StarSystemFactory.cs` | Creates a single star system: star + planets + moons + asteroids. |
| `StarFactory.cs` | Creates star entities with `StarInfoDB`, `MassVolumeDB`, `SensorProfileDB`, etc. |
| `SystemBodyFactory.cs` | Creates planet/moon/asteroid entities. Assigns body type, mass, orbit, atmosphere. |
| `AtmosphereDB.cs` | DataBlob on any body with an atmosphere. Holds `Pressure`, `SurfaceTemperature`, `GreenhouseFactor`, `GreenhousePressure`, `Hydrosphere`, `HydrosphereExtent`, `Composition` (gas name → atm pressure), `CompositionByPercent`. |
| `AtmosphereProcessor.cs` | Static processor. Computes surface temperature from gas composition using the **Aurora greenhouse formula** (explicitly cited in comments). Called during system generation and whenever atmosphere changes. |
| `AtmosphereDBExtensions.cs` | Helper methods on `AtmosphereDB`. |
| `MassVolumeDB.cs` | Mass, volume, density, radius, surface gravity, escape velocity. `Volume_km3` is **referenced in the commented-out colony damage block** in `DamageProcessor.cs` — this field exists. |
| `MassVolumeProcessor.cs` | Processes mass/volume-related changes. |
| `StarInfoDB.cs` | Star type, luminosity, spectral class. |
| `SystemBodyInfoDB.cs` | Body type (terrestrial, gas giant, etc.), albedo, base temperature. |
| `AsteroidDamageDB.cs` | Damage state for asteroids (kinetic impact mechanics). |
| `AsteroidFactory.cs` | Creates asteroid entities. |
| `RuinsDB.cs` | Alien ruins on a body (exploration reward). |
| `PopulationSupportAtbDB.cs` | Component attribute for infrastructure items that support colonists. `PopulationCapacity` = how many million people this unit of infrastructure supports at CC 1.0. |
| `VisibleByDefaultDB.cs` | Tag blob — entities with this are visible without sensors (stars, known bodies). |
| `LagrangePointDB.cs` | **NEW (Slice D, 2026-07-03)** Tags an entity as a Lagrange-point ANCHOR marker (Primary/Secondary bodies + PointIndex). A named, stable point in space a station can deploy at (instead of a random spot). |
| `PlanetRegionsDB.cs` | **NEW (Ground-map slice 1, 2026-07-03)** The planet's surface as a set of **`Region`s** — the strategic ground map. Attached to the **planet body** entity (regions are *of the planet*, persist without a colony). v1: **4 longitude slices in a RING** (topology-correct — no seam, so the Pacific theatre survives). Each `Region` carries `Area_km2` (true size), `CrossingTimeSeconds` (distance datum for movement), ring `Neighbors`, a `Surveyed` flag (features known only once scanned), a **bundle** of `RegionFeature` (Ocean/Mountains/Forest/… — a region is many features, not one type), and `InstallationIds` (later slices). **Fully persistent** (`Clone` + `[JsonProperty]` + deep-copy ctors — the fix the old `ColonyHexMapDB` lacked). **`RevealAll()` (slice 4, 2026-07-03)** flips every region to surveyed — called from `GeoSurveyProcessor` on survey completion (the exploration→map link). Design: `docs/GROUND-COMBAT-MAP-DESIGN.md`. |
| `PlaceInstallationInRegionOrder.cs` | **NEW (Ground-map slice 2, 2026-07-03)** The "build at a place" front door — the station-deploy pattern one level DOWN (a region of a planet instead of an orbital body). Issued to the **colony** (which carries `OrderableDB`), so it rides the real `Game.OrderHandler.HandleOrder` player path. Installs a component on the colony (the normal installation rail → the economy sees it) AND records the instance id in the chosen `Region.InstallationIds` — a **new located axis** that leaves the existing economy untouched. v1 is direct placement; a region-targeted `IndustryJob` (materials + build-time) and building on an UNcolonised region (via a ground construction unit) are refinements. Gauge: `PlanetRegionsTests.BuildInRegion_*`. |
| `PlanetRegionsFactory.cs` | **NEW (Ground-map slice 1; survey model corrected 2026-07-04)** `GenerateForSystem(system, surveyed)` — builds the region layer for every major body (mirrors `LagrangeFactory`: defensive, idempotent, seeded by the system RNG). **`IsMajorBody` now includes `Moon`** — Luna/Ganymede are ground-combat places, so moons get a region layer, not just planets/giants/dwarfs. Rolls **random-but-logical** features from the world's own scalars (wet `HydrosphereExtent` → ocean/coast; active `Tectonics` → mountains; gas giant → gas layers). **`surveyed:false` from ALL four gen paths now** — nothing is pre-surveyed at generation; the "known" state comes from **`ColonyFactory` calling `PlanetRegionsDB.RevealAll()` on the world you colonise** (you know the ground where you settle), so the home planet is known and its siblings (Luna, Mars…) are fog until a geo survey scans them. Hooked at all four gen paths in `StarSystemFactory`. Gauge: `Pulsar4X.Tests/PlanetRegionsTests.cs` (`HomeColony_WorldSurveyed_SiblingsFogged`, `Moon_GetsRegionLayer_SoLunaIsSurveyable`, `SurveyReveal_*`). |
| `GroundHex.cs` | **NEW (Hex ground H1, 2026-07-04)** One HEX of a planet's surface — the fine tile a `Region` is made of (Planet → Region → Hex). Save-safe data object (axial `Q`/`R` as ints + `Terrain` + `OwnerFactionID`), like `RegionFeature`. Stored as `Region.Hexes`. Reuse `Colonies.HexCoordinate` for movement MATH (H2). Design: `docs/HEX-GROUND-AND-ORDERS-DESIGN.md`. |
| `PlanetHexFactory.cs` | **NEW (Hex ground H1, 2026-07-04)** Generates a body's hex patches **LAZILY** — `EnsureHexesForBody(body)` builds a hex DISK per region (radius `HexPatchRadiusFor(planetRadius)` — Earth→12≈469 hexes/region, scales linearly, clamped [2,24]), terrain per hex drawn from the region's `Features` mix. Idempotent, deterministic (system RNG), defensive. **Hexes generate for a world you SETTLE or SCAN** — hooked at `ColonyFactory` (colony creation) **and `GeoSurveyProcessor` (survey completion, beside `RevealAll` — U1 2026-07-04, the developer's "hexes exist for every surveyed world so the map is never empty")**; the galaxy still isn't bloated (unsurveyed worlds carry only the 4 coarse regions). Pure `HexPatchRadiusFor`/`HexDiskCount` unit-tested. Gauges: `PlanetRegionsTests.Hex*` + `SurveyGen_CompletingGeoSurvey_GeneratesHexGrid`. |
| `LagrangeFactory.cs` | **NEW (Slice D)** `GenerateForSystem(system)` — creates **L4/L5 Trojan markers** for each star-planet pair (mirrors `JPSurveyFactory`'s non-body-marker recipe: NameDB + token `MassVolumeDB` + `PositionDB` with **`MoveType.None`** + `VisibleByDefaultDB` + `LagrangePointDB`). The marker is a **STATIC point** at the L4/L5 position (the star→planet vector rotated ±60° in the orbital plane) — **no `OrbitDB`**, so it never enters the parallel orbit processor. (A first cut gave the marker the planet's orbit offset ±60° to co-orbit "for free"; that crashed the orbit processor with a `PositionDB` lookup on a worker thread — so v1 is a static point at the epoch L-point, and making it co-orbit is a documented refinement, e.g. a tiny `LagrangeProcessor` that recomputes the position each cycle.) **Fully defensive + idempotent** (hooked into New-Game-critical system gen — must never throw or double-generate). Hooked at the main + Sol gen paths in `StarSystemFactory`. Deploy anchors to it via `DeployStationOrder`'s nearby-marker preference. L1/L2/L3 + planet-moon pairs are refinements. |

---

## AtmosphereDB — the Key Data for Phase 2

`AtmosphereDB` holds everything the colony cost formula needs:

```csharp
Pressure            float   — total atmospheric pressure in atm
SurfaceTemperature  float   — in °C, after greenhouse effects
GreenhouseFactor    float   — computed by AtmosphereProcessor
GreenhousePressure  float   — sum of (gas pressure × greenhouse effect factor)
Hydrosphere         bool    — does liquid water exist?
HydrosphereExtent   decimal — 0–100% water coverage
Composition         Dictionary<string, float>  — gas ID → pressure in atm
```

---

## AtmosphereProcessor — Already Implements Aurora's Formula

This is the most complete Aurora-derived implementation in the codebase. From the source comments (verbatim):

> "From Aurora: Greenhouse Factor = 1 + (Atmospheric Pressure / 10) + Greenhouse Pressure (Maximum = 3.0)"

The actual implementation uses a slightly adjusted constant (0.035 instead of 0.1) for calibration — this is intentional for Pulsar's scale. The structure is correct.

Surface temperature calculation:
```
SurfaceTemp(K) = BaseTemp(K) × (1 - Albedo)^0.25 + BaseTemp(K) × GreenhouseFactor × (1 - Albedo)^0.25
```
(Applies Stefan-Boltzmann law — more physically accurate than Aurora's simpler version.)

**For terraforming (Phase 2d):** A `TerraformingProcessor` modifies `AtmosphereDB.Composition` over time (adds or removes gas entries), then calls `AtmosphereProcessor.UpdateAtmosphere()` to recompute temperature and pressure. The hook is already designed — no structural changes needed.

---

## PopulationSupportAtbDB — the Infrastructure Link

This component attribute is on installations that make hostile worlds habitable. `PopulationCapacity = 10000` means "this unit supports 10,000 people at CC 1.0." 

The aurora formula is: `MaxPop(millions) = Infrastructure / (CC × 100)`

Where `Infrastructure = sum of PopulationCapacity across all installed units`. This is what `PopulationProcessor` should read to replace the stub.

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Procedural system/planet generation | `GalaxyFactory`, `StarSystemFactory` | ✅ functional |
| Atmosphere with gas composition | `AtmosphereDB.Composition` | ✅ functional |
| Surface temperature from greenhouse | `AtmosphereProcessor` (Aurora formula, cited) | ✅ functional |
| Hydrosphere percentage | `AtmosphereDB.HydrosphereExtent` | ✅ stored |
| Ruins generation | `RuinsDB` | ✅ stored |
| Colony cost calculation | `SpeciesDB.ColonyCost()` (see `People/CLAUDE.md`) | ✅ exists, verify formula |
| Infrastructure → population capacity | `PopulationSupportAtbDB.PopulationCapacity` | ✅ stored, stub in PopulationProcessor |
| Terraforming (modify atmosphere over time) | `AtmosphereDB` supports it; no `TerraformingProcessor` | ❌ hook exists, processor missing |
| Asteroid kinetic impact | `AsteroidDamageDB` | ✅ partial |

---

## Phase 2 Hook Points

| Phase 2 task | Hook point |
|-------------|------------|
| Population formula (Phase 2c) | `PopulationProcessor` reads `sum(PopulationSupportAtbDB.PopulationCapacity)` from `ComponentInstancesDB` on the colony, divides by `(ColonyCost × 100)` for max pop |
| Terraforming (Phase 2d) | New `TerraformingAtbDB` component attribute on terraformer installation; new `TerraformingProcessor` modifies `AtmosphereDB.Composition` then calls `AtmosphereProcessor.UpdateAtmosphere()` |
| Colony bombardment dust (Phase 3) | Orbital strikes add a "dust" gas to `AtmosphereDB.Composition` with anti-greenhouse effect; `AtmosphereProcessor` recalculates cooling naturally |
