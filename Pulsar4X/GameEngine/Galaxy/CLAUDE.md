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
| `AtmosphereProcessor.cs` | Static processor. The temperature/pressure compute logic lives in `UpdateAtmosphere()` (uses the **Aurora greenhouse formula**, explicitly cited in comments). **Its `Process(game, systems, deltaSeconds)` hotloop method is an empty `@todo` stub** (`AtmosphereProcessor.cs:19-22`) — it does nothing per tick. The only live caller of `UpdateAtmosphere` is **system generation** (`SystemBodyFactory.cs`); there is no "whenever atmosphere changes" caller yet (terraforming is absent — see below). |
| `AtmosphereDBExtensions.cs` | Helper methods on `AtmosphereDB`. |
| `MassVolumeDB.cs` | Mass, volume, density, radius, surface gravity, escape velocity. `Volume_km3` is **referenced in the commented-out colony damage block** in `DamageProcessor.cs` — this field exists. |
| `MassVolumeProcessor.cs` | Processes mass/volume-related changes. |
| `StarInfoDB.cs` | Star type, luminosity, spectral class. |
| `SystemBodyInfoDB.cs` | Body type (terrestrial, gas giant, etc.), albedo, base temperature. |
| `AsteroidDamageDB.cs` | Damage state for asteroids (kinetic impact mechanics). |
| `AsteroidFactory.cs` | Creates asteroid entities. |
| `RuinsDB.cs` | Alien ruins on a body (exploration reward). **Now actually GENERATES (2026-07-12, Exploration X.1).** For the whole life of the code `GenerateRuins` gated on the tautology `bodyType != Terrestrial \|\| bodyType != Moon` (always true — a body can't be both) so EVERY body bailed and no ruin ever spawned. Fixed to the pure, tested predicate `SystemBodyFactory.CanBodyHaveRuins` (Terrestrial or Moon; no atmosphere requirement — ruins need ground not air, so airless Luna / thin-atmosphere Mars qualify). The roll now draws from a **dedicated per-body RNG seeded from the body's mass**, NOT the shared system RNG — so adding this content does not perturb the rest of galaxy gen and the `SystemGenTests` golden master stays exact (the old code drew zero from the shared stream, having bailed first). **Still latent:** nothing READS `RuinsDB` yet — the field-site consumer that turns a ruin into a scientist expedition is X.2 (`docs/EXPLORATION-CONTENT-DESIGN.md`). Gauge: `RuinsGenerationTests`. |
| `PopulationSupportAtbDB.cs` | Component attribute for infrastructure items that support colonists. `PopulationCapacity` = how many million people this unit of infrastructure supports at CC 1.0. |
| `VisibleByDefaultDB.cs` | Tag blob — entities with this are visible without sensors (stars, known bodies). |
| `LagrangePointDB.cs` | **NEW (Slice D, 2026-07-03)** Tags an entity as a Lagrange-point ANCHOR marker (Primary/Secondary bodies + PointIndex). A named, stable point in space a station can deploy at (instead of a random spot). |
| `PlanetRegionsDB.cs` | **NEW (Ground-map slice 1, 2026-07-03)** The planet's surface as a set of **`Region`s** — the strategic ground map. Attached to the **planet body** entity (regions are *of the planet*, persist without a colony). v1: **4 longitude slices in a RING** (topology-correct — no seam, so the Pacific theatre survives). Each `Region` carries `Area_km2` (true size), `CrossingTimeSeconds` (distance datum for movement), ring `Neighbors`, a `Surveyed` flag (features known only once scanned), a **bundle** of `RegionFeature` (Ocean/Mountains/Forest/… — a region is many features, not one type), and `InstallationIds` (later slices). **Fully persistent** (`Clone` + `[JsonProperty]` + deep-copy ctors — the fix the old `ColonyHexMapDB` lacked). **`RevealAll()` (slice 4, 2026-07-03)** flips every region to surveyed — called from `GeoSurveyProcessor` on survey completion (the exploration→map link). Design: `docs/GROUND-COMBAT-MAP-DESIGN.md`. |
| `PlaceInstallationInRegionOrder.cs` | **NEW (Ground-map slice 2, 2026-07-03)** The "build at a place" front door — the station-deploy pattern one level DOWN (a region of a planet instead of an orbital body). Issued to the **colony** (which carries `OrderableDB`), so it rides the real `Game.OrderHandler.HandleOrder` player path. Installs a component on the colony (the normal installation rail → the economy sees it) AND records the instance id in the chosen `Region.InstallationIds` — a **new located axis** that leaves the existing economy untouched. v1 is direct placement; a region-targeted `IndustryJob` (materials + build-time) and building on an UNcolonised region (via a ground construction unit) are refinements. Gauge: `PlanetRegionsTests.BuildInRegion_*`. |
| `PlaceInstallationOnHexOrder.cs` | **NEW (Mine-on-deposit, 2026-07-07)** The `PlaceInstallationInRegionOrder` twin one zoom FINER — builds a structure on a SPECIFIC surface-grid hex (global `(Q,R)`) instead of a whole region band. The front door for "build a mine ON that mineral deposit": deposits are located on individual `GroundHex`es, so a mine sits on the exact ore hex. Issued to the COLONY (`OrderableDB` → real order path); installs the component on the colony (economy + mining see it as any building) AND records the instance id in that `GroundHex.InstallationIds` — purely ADDITIVE located axis. Resolves the hex via `PlanetGridFactory.EnsureGridForBody` (builds the grid on demand) + `SurfaceGrid.HexAt` (seam-wrapping). v1 keeps mining body-wide; per-hex mining (a mine works the deposit on its OWN hex, that hex depletes) is the flagged follow-up. Gauge: `HexMiningPlacementTests`. |
| `PlanetRegionsFactory.cs` | **NEW (Ground-map slice 1; survey model corrected 2026-07-04)** `GenerateForSystem(system, surveyed)` — builds the region layer for every major body (mirrors `LagrangeFactory`: defensive, idempotent, seeded by the system RNG). **`IsMajorBody` now includes `Moon`** — Luna/Ganymede are ground-combat places, so moons get a region layer, not just planets/giants/dwarfs. Rolls **random-but-logical** features from the world's own scalars (wet `HydrosphereExtent` → ocean/coast; active `Tectonics` → mountains; gas giant → gas layers). **`surveyed:false` from ALL four gen paths now** — nothing is pre-surveyed at generation; the "known" state comes from **`ColonyFactory` calling `PlanetRegionsDB.RevealAll()` on the world you colonise** (you know the ground where you settle), so the home planet is known and its siblings (Luna, Mars…) are fog until a geo survey scans them. Hooked at all four gen paths in `StarSystemFactory`. Gauge: `Pulsar4X.Tests/PlanetRegionsTests.cs` (`HomeColony_WorldSurveyed_SiblingsFogged`, `Moon_GetsRegionLayer_SoLunaIsSurveyable`, `SurveyReveal_*`). |
| `RealSurfaceMaps.cs` + `EarthTerrainMap.cs` + `MarsTerrainMap.cs` + `LunaTerrainMap.cs` | **NEW (Sol playtest, 2026-07-06)** The **real, authored surface maps** — the worlds we have an actual map of render as themselves instead of the random noise field every other body gets. `RealSurfaceMaps` is the registry (`SamplerForName(name)` → the body's `Sample` or null) + the ONE shared letter→terrain decoder (`CharToFeature`, so maps can't drift on what a letter means). The baked, hand-authored equirectangular biome maps (72×36): **Earth** (Americas + Andes, Sahara, Congo, Himalaya, Siberia, India, SE Asia, Australia, both ice caps, oceans); **Mars** (Tharsis/Olympus + Elysium volcanoes, Valles Marineris, Hellas/Argyre basins, northern lowlands vs. cratered southern highlands, small polar caps — a DRY world, no ocean); **Luna** (the near-side maria "face" — Imbrium/Serenitatis/Tranquillitatis/Procellarum + southern seas — dominant cratered highlands, South Pole-Aitken basin, small polar ice; airless, no ocean). Shared convention: **row 0 = North pole**, **col 0 = 180°W** (wrap seam), columns run EAST — matching the client's top-down draw + the engine's lat=0→north / lon-wraps sample space. It's DATA, not physics (generators: `scratchpad/gen_earth.py`, `gen_mars.py`). `WorldTerrain.ForBody` routes a body whose `NameDB.DefaultName` hits the registry (gated to a non-gas body) to its map instead of the noise field, at the **same** sample points — so the cylinder grid, region disks, hex terrain and client map keep their shape, only the terrain changes. Fixed the "Earth is a joke as far as how the map looks" report; adding a world = one line in `SamplerForName` + its baked class. Gauges: `EarthTerrainMapTests` / `MarsTerrainMapTests` (well-formed shape + the body's signature landmarks). Edit the strings to tune (keep each row exactly `Cols` chars — `IsWellFormed` is the guard). |
| `GroundHex.cs` | **NEW (Hex ground H1, 2026-07-04)** One HEX of a planet's surface — the fine tile a `Region` is made of (Planet → Region → Hex). Save-safe data object (axial `Q`/`R` as ints + `Terrain` + `OwnerFactionID`), like `RegionFeature`. Stored as `Region.Hexes`. Reuse `Colonies.HexCoordinate` for movement MATH (H2). Design: `docs/HEX-GROUND-AND-ORDERS-DESIGN.md`. |
| `PlanetHexFactory.cs` (+ `WorldTerrain`) | **NEW (Hex ground H1, 2026-07-04; Earth map + generator calibration 2026-07-06)** Also hosts the `internal WorldTerrain` planet-wide terrain generator. Every body samples a coherent noise field (continents/oceans that span borders) **except the authored worlds** (Earth/Mars), which `WorldTerrain.ForBody` routes to their real map via `RealSurfaceMaps`. **Generator calibration (2026-07-06):** two bugs were found by rendering its own output — (1) sea level was a linear `0.12+0.72*hydro` guess that over-flooded (a 71%-water world came out ~82% ocean); it's now the hydrosphere **QUANTILE** of the elevation field (`SeaLevelForHydrosphere`, sampled once per world from the already-drawn phases → no extra RNG, determinism preserved), so ocean coverage tracks hydrosphere (~71%). (2) cold worlds (≤ −10 °C) collapsed to uniform **tundra**; the cold branch now gives a DRY cold world (Mars/Luna-like) barren/desert lowlands, so a frozen world isn't one flat biome (`DryWorldHydro`/`ColdDryMoist`/`ColdDampMoist` — tunable dials, flagged). Both fixes keep the field smooth, so the coherence/continuity invariants hold. Gauge: `WorldTerrainCalibrationTests` (ocean tracks hydro across seeds; dry cold world isn't tundra) via the `internal ForTest`/`ClassifyForTest` seam. Generates a body's hex patches **LAZILY** — `EnsureHexesForBody(body)` builds a hex DISK per region (radius `HexPatchRadiusFor(planetRadius)` — Earth→12≈469 hexes/region, scales linearly, clamped [2,24]), terrain per hex drawn from the region's `Features` mix. Idempotent, deterministic (system RNG), defensive. **Hexes generate for a world you SETTLE or SCAN** — hooked at `ColonyFactory` (colony creation) **and `GeoSurveyProcessor` (survey completion, beside `RevealAll` — U1 2026-07-04, the developer's "hexes exist for every surveyed world so the map is never empty")**; the galaxy still isn't bloated (unsurveyed worlds carry only the 4 coarse regions). Pure `HexPatchRadiusFor`/`HexDiskCount` unit-tested. Gauges: `PlanetRegionsTests.Hex*` + `SurveyGen_CompletingGeoSurvey_GeneratesHexGrid`. |
| `CityTile.cs` / `CityGrid.cs` | **NEW (City sub-grid C1, 2026-07-04)** The FINE zoom below the operational hex (Planet → Region → Hex → **CityTile**). `CityTile` = one fine tile (axial Q/R + terrain + `BuildingInstanceId`, -1 = empty; 1:1 placement). `CityGrid` = one developed operational hex's fine tile disk, hung off `GroundHex.CityGrid` (nullable — lazy, so an undeveloped hex costs nothing; deep-copied by `GroundHex`'s clone = save-safe). The buildings on its tiles roll up to `GroundHex.InstallationIds` (the W-track footprint). Design: `docs/GROUND-CITY-AND-WARMAP-DESIGN.md`. |
| `SurfaceGrid.cs` | **NEW (Global grid G1, 2026-07-04)** ONE continuous CYLINDER of surface hexes — the planet as a single grid, not 4 per-region disks. `Q`=longitude column (WRAPS at the seam), `R`=latitude row (bounded/poles); row-major `List<GroundHex>` with GLOBAL `(Q,R)`; `HexAt(q,r)` wraps `q`. Hangs off `PlanetRegionsDB.SurfaceGrid` (nullable, lazy). Terrain continuous + seam-wrapping by construction. Save-safe. Design: `docs/GLOBAL-HEX-GRID-DESIGN.md`. **Additive** alongside the disks during the migration (G6 retires the disks). |
| `PlanetGridFactory.cs` | **NEW (Global grid G1, 2026-07-04)** `EnsureGridForBody(body)` builds the cylinder (dims scale with planet radius, `Cols`=regionCount×`ColumnsPerRegion` so region column-BANDS are clean; terrain from each hex's GLOBAL lon/lat via `WorldTerrain.TerrainForLonLat`), lazy/idempotent/defensive. `RegionOfColumn` / `BandCentreColumn` (a region is a column band; units will muster at the band centre — G3). Gauge: `SurfaceGridTests` (scaled+banded, terrain coherent + wraps seamlessly, HexAt wraps, clone-safe). |
| `CityGridFactory.cs` | **NEW (City sub-grid C1, 2026-07-04)** `EnsureCityForHex(body, region, q, r)` — builds a developed operational hex's fine tile disk LAZILY (radius `CityPatchRadius`=6 → 127 tiles; terrain v1 = the operational hex's terrain), idempotent, defensive. The `PlanetHexFactory` twin **one zoom down**. **+ GLOBAL grid (G4):** `ResolveGlobalHex(body, gQ, gR)` (find a hex by GLOBAL `(Q,R)` on `SurfaceGrid`, generating the grid on demand) + `EnsureCityForGlobalHex` (build a developed global hex's city, labelled with its column band). Placement + roll-up live in `GroundCombat.CityBuilder`. Gauge: `CityGridTests` (incl. `EnsureCityForGlobalHex_*`). |
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

Surface temperature calculation (as actually coded in `AtmosphereProcessor.cs:62-63`):
```
SurfaceTemp(K) = BaseTemp(K) + BaseTemp(K) × GreenhouseFactor × (1 - Albedo)^0.25
```
Note: the first term is the **plain** base temperature in Kelvin — **no albedo factor** is applied to it. The `(1 - Albedo)^0.25` Stefan-Boltzmann factor is applied **only to the greenhouse term** (the second term). (The airless-body branch, `AtmosphereProcessor.cs:71-72`, does apply `(1 - Albedo)^0.25` to the base temperature directly.)

**For terraforming (Phase 2d) — NOT built:** the *plan* is for a `TerraformingProcessor` to modify `AtmosphereDB.Composition` over time (add or remove gas entries), then call `AtmosphereProcessor.UpdateAtmosphere()` to recompute temperature and pressure. **No `TerraformingProcessor` and no `TerraformingAtbDB` exist in the code today** — this is a design sketch, not a wired hook. `UpdateAtmosphere()` is the recompute entry point it *would* call, and that method does exist and work — so the recompute half is ready, but the driving processor still has to be written.

---

## PopulationSupportAtbDB — the Infrastructure Link

This component attribute is on installations that make hostile worlds habitable. `PopulationCapacity = 10000` means "this unit supports 10,000 people at CC 1.0." 

The aurora formula is: `MaxPop(millions) = Infrastructure / (CC × 100)`

Where `Infrastructure = sum of PopulationCapacity across all installed units`. **This is already wired — it is no longer a stub.** `PopulationProcessor` (`Colonies/PopulationProcessor.cs:26,165`) and `StationPopulationProcessor` (`Stations/StationPopulationProcessor.cs:60`) both read the live carrying capacity via `ComponentInstancesDB.GetPopulationSupportValue()` (`Engine/Components/ComponentInstancesDBExtensions.cs:47-78`), which sums `PopulationCapacity` across the installed infrastructure — **tolerance-gated** by each design's `GravityToleranceAtb`/`PressureToleranceAtb` (a component that can't handle the body's gravity/pressure contributes nothing) and scaled by each component's health. (Runtime behaviour is unverified in CI, which can't run the client — but the read path exists and is wired.)

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Procedural system/planet generation | `GalaxyFactory`, `StarSystemFactory` | ✅ functional |
| Atmosphere with gas composition | `AtmosphereDB.Composition` | ✅ functional |
| Surface temperature from greenhouse | `AtmosphereProcessor` (Aurora formula, cited) | ✅ functional |
| Hydrosphere percentage | `AtmosphereDB.HydrosphereExtent` | ✅ stored |
| Ruins generation | `RuinsDB` | ✅ generates (X.1 tautology fix, 2026-07-12) — but nothing reads it yet (X.2) |
| Colony cost calculation | `SpeciesDB.ColonyCost()` (see `People/CLAUDE.md`) | ✅ exists, verify formula |
| Infrastructure → population capacity | `PopulationSupportAtbDB.PopulationCapacity` | ✅ wired — read live by `PopulationProcessor` + `StationPopulationProcessor` via `GetPopulationSupportValue()` (tolerance-gated) |
| Terraforming (modify atmosphere over time) | no `TerraformingProcessor`, no `TerraformingAtbDB` | ❌ not built — only `UpdateAtmosphere()` recompute exists |
| Asteroid kinetic impact | `AsteroidDamageDB` | ✅ partial |

---

## Phase 2 Hook Points

| Phase 2 task | Hook point |
|-------------|------------|
| Population formula (Phase 2c) — **DONE/wired** | `PopulationProcessor` already reads `sum(PopulationSupportAtbDB.PopulationCapacity)` from `ComponentInstancesDB` on the colony (via `GetPopulationSupportValue()`) as its carrying-capacity cap. Runtime behaviour unverified (CI can't run the client). |
| Terraforming (Phase 2d) — **still to build** | No `TerraformingAtbDB` and no `TerraformingProcessor` exist yet. Plan: a new `TerraformingAtbDB` component attribute on a terraformer installation + a new `TerraformingProcessor` that modifies `AtmosphereDB.Composition` then calls `AtmosphereProcessor.UpdateAtmosphere()` (the recompute method that already exists). |
| Colony bombardment dust (Phase 3) | Orbital strikes add a "dust" gas to `AtmosphereDB.Composition` with anti-greenhouse effect; `AtmosphereProcessor` recalculates cooling naturally |
