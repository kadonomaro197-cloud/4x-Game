# One Continuous Global Hex Grid — Design & Migration (G-track)

**As of 2026-07-04.** Status: **design captured; building engine-first, additive slice first.** The developer's call (choosing "the real fix" over rendering more patch-overlap): the planet surface should be **ONE continuous hex grid that wraps around the world**, not four separate per-region hex disks stitched at edges. "Region" becomes a **label on a band of the grid**, not a self-contained map. Then a place (Paris) shows up in *every* view that reaches its longitude, overlap is free, terrain is continuous by construction, and movement stops needing edge-gate stitching. Supersedes the per-region-disk hex model in `HEX-GROUND-AND-ORDERS-DESIGN.md` (H1's disks); the coarse strategic `PlanetRegionsDB` ring **stays** (ownership / area / crossing-time), it just stops owning the fine hexes.

---

## Why (the problem this fixes)

Today the surface is **four separate hex sheets** — `Region.Hexes` is a radius-R **disk** per region, each with its own local `(q,r)` centred at `(0,0)`, stitched to neighbours by **edge gates** in `HexPathfinder`. Consequences the developer hit:

- **No shared coordinate space.** A hex in region 2 is not a thing region 1 knows about. The client can *draw* a neighbour's edge bleeding in, but a place deep in region 2 can never appear in region 1's view — there's no continuous world to slide a window over.
- **Disks don't tile.** A disk has a ragged (hexagonal) boundary, so even laid adjacently the sheets don't form a seamless surface.
- **Movement needs stitching.** Crossing a region border is special-cased (edge gates) instead of just… walking to the next column.

The coarse ring already solved the *global topology* (4 longitude slices, no seam — "the Pacific survives"). The fix is to make the **fine** layer honour that same topology: one grid, longitude wraps.

---

## The locked model: a cylinder of hexes

- **Global coordinates.** A hex has `(Q, R)`:
  - **`Q` = longitude column**, `0 .. Cols-1`, and it **WRAPS** (column `Cols-1` is adjacent to column `0` — the seam that keeps the Pacific whole).
  - **`R` = latitude row**, `0 .. Rows-1`, **bounded** (poles are caps, no wrap).
- **Dimensions scale with planet size** (like `HexPatchRadiusFor`): `Cols ≈ 4 × (2·PatchRadius+1)`, `Rows ≈ 2·PatchRadius+1`, so Earth stays ≈ today's ~1800 hexes and Mars is smaller. `Cols` stays divisible by the region count so region bands are clean.
- **Terrain from the GLOBAL position**, reusing V2's `WorldTerrain` field but fed `lon = Q/Cols` (wraps) and `lat = R/Rows` directly — **drop the per-region offset**. Continuity across "region borders" is then automatic (they're just interior columns of one grid) and the longitude field already wraps, so the whole globe is coherent and seamless.
- **Regions become LABELS.** `PlanetRegionsDB.Regions` stays as the coarse strategic layer (ownership, `Area_km2`, `CrossingTimeSeconds`, `Neighbors`, `InstallationIds`), but a region is now **a band of columns**: region `N` owns `Q ∈ [N·Cols/RegionCount, (N+1)·Cols/RegionCount)`. `RegionOfColumn(Q) = Q·RegionCount/Cols`. Ownership/combat/capture still resolve per region (per band); the hexes are just addressed globally.
- **Movement = global A\*** on the wrapping grid (6-neighbour, `Q` wraps, `R` clamped), terrain-weighted by the existing `HexMoveMult` (ocean impassable). **No edge gates** — crossing a region border is just stepping to the next column. A full band-crossing march ≈ the coarse `CrossingTimeSeconds` (derive the per-hex time from `CrossingTimeSeconds ÷ band width`, as today — no new distance constant).

---

## The client (the visible payoff)

The surface view becomes a **sliding window over the cylinder**, centred on a chosen longitude and **wrapping** at the seam: you see a continuous slice (roughly one region wide plus margins), and **any place shows up in every window whose longitude reaches it** — Paris in region 2 appears on the right when you're centred on region 1, and near-centre when you're centred on region 2. Cycling ◀/▶ pans the window in longitude (wrapping). No gaps, no "3 separate maps." The city/fortification zoom (C-track) is unchanged (it's the finer grid under one hex).

---

## Migration — slices (engine-first, CI-gauged, ADDITIVE first so CI stays green)

The coordinate change is pervasive, so slice 1 is **additive** (build the grid alongside the disks, prove it, touch no consumer), then migrate consumers one at a time, then retire the disks.

- **G1 — the global grid, additive.** A `SurfaceGrid` (save-safe: `Cols`/`Rows` + a `GroundHex[]` or list, global `(Q,R)`) on `PlanetRegionsDB` (`SurfaceGrid` field, nullable — lazy, like the disks). `PlanetGridFactory.EnsureGridForBody(body)` generates the cylinder (dimensions from planet radius), terrain from global `lon/lat` via `WorldTerrain`. `RegionOfColumn` / `ColumnsPerRegion` helpers. The existing `Region.Hexes` disks are **untouched** (dual representation, temporary). Gauges: `SurfaceGridTests` — dimensions scale; terrain wraps (column `Cols-1` ≈ column `0`); terrain is continuous column-to-column (no jumps at band borders); clone-safe. **← START HERE.**
- **G2 ✅ built (2026-07-04).** `HexPathfinder.FindGlobalPath(grid, fromQR, toQR)` — A\* on the wrapping cylinder (offset odd-r neighbours, Q wraps via `SurfaceGrid.WrapCol`, R clamped at the poles), terrain-weighted by `HexMoveMult`, ocean impassable, **no edge gates**. Admissible cylinder heuristic (max of wrapped-column and row distance). Additive alongside the per-region `FindPath`. Gauges: `SurfaceGridTests.FindGlobalPath_*` — straight walk, routes around ocean, **wraps the seam** (col 1 → col 8 on a 10-wide grid takes the 3-step short way through column 0/9, not the 7-step long way), dest-on-ocean unreachable.
- **G3 ✅ built (2026-07-04, ADDITIVE).** Kept CI green by adding a PARALLEL global-movement path rather than repurposing the local `HexQ/HexR` (the riskiest-slice mitigation): `GroundUnit` gains `GlobalQ/GlobalR` + `GlobalPath`/`GlobalTransitSecondsRemaining`/`GlobalStepBaseSeconds` (deep-copied). `RaiseUnit` also musters the unit at its region band's **centre column** (`PlanetGridFactory.BandCentreColumn`, generating the grid on demand). `GroundForces.OrderMoveToGlobalHex` plots a `FindGlobalPath` route + per-hex time from the band crossing datum; `GroundForcesProcessor`'s MOVE step gains a **(c) global-march** branch that walks it, updating `RegionIndex` by column as it crosses band borders (no gate). The per-region `HexQ/HexR` path + `OrderMoveToHex` are UNTOUCHED (G6 retires them). Gauges: `GroundForcesTests.RaiseUnit_MustersOnGlobalGrid_InItsBand`, `OrderMoveToGlobalHex_MarchesAcrossBandBorder_NoGate`.
- **G4 ✅ built (2026-07-04, ADDITIVE).** W1/C1 now ride the global hex too. `CityGridFactory` gains `ResolveGlobalHex(body, gQ, gR)` (resolve a hex by GLOBAL `(Q,R)` on `SurfaceGrid`, generating the grid on demand) + `EnsureCityForGlobalHex` (build a developed global hex's fine city, labelled with its column band); `CityBuilder` gains `PlaceBuildingOnGlobalTile` / `RemoveBuildingFromGlobalTile` (place/clear a fine tile + keep the roll-up to `GroundHex.InstallationIds`); `GroundBuildings` gains `BombardGlobalHex` (drain/destroy a global hex's footprint buildings + clear the fine tile — the grave rung; the region-economy removal targets the column BAND). `BombardHex`/`BombardGlobalHex` share an extracted `BombardResolvedHex` core, so the two address-paths can't drift. The per-region disk methods (`ResolveHex`/`EnsureCityForHex`/`PlaceBuildingOnTile`/`BombardHex`) are **untouched** (G6 retires them). Gauges: `CityGridTests.EnsureCityForGlobalHex_GeneratesOnTheCylinder_AndIsIdempotent` + `GlobalPlaceRollsUp_AndGlobalBombardClearsTheTile` (place→roll-up→bombard→destroyed+tile-cleared on the cylinder). Deferred to C-track: the global `LocateFootprintsOnGlobalHexes` / `DevelopColonyGlobalHex` / `CaptureBandContents` (the layer-split consolidation).
- **G5 — the client sliding window.** `PlanetViewWindow` renders the cylinder window (centred longitude, wrapping) instead of `DrawThreeRegionHexMap`'s per-region projection. The continuous-world payoff. (CI compiles; feel is the local build.)
- **G6 — retire the disks.** Remove per-region disk generation (`PlanetHexFactory` disk path), `Region.Hexes`, and the edge-gate pathfinder. `PlanetHexFactory`'s lazy hook becomes `PlanetGridFactory`. Delete the dual representation.

## What breaks, and how each is handled (the cascade)

| Consumer | Today | After |
|---|---|---|
| `PlanetHexFactory` (disks) | radius-R disk per region, local `(q,r)` | replaced by `PlanetGridFactory` (cylinder) — G1 adds, G6 removes |
| `Region.Hexes` | the disk | derived band-view of the grid (or dropped; consumers read the grid) — G4/G6 |
| `HexPathfinder` (edge gates) | per-region A\* + gate stitching | `FindGlobalPath` on the wrapping grid — G2 |
| `GroundUnit.HexQ/HexR` + muster `(0,0)` | local to a region disk | global; muster = band centre column — G3 |
| `RaiseUnit` / `OrderMoveToHex` / processor walk | local, per-region | global — G3 |
| `GroundBuildings` / `CityBuilder` / `CityGridFactory` (W1/C1) | region + local hex | global hex lookup — G4 (mostly mechanical) |
| `WorldTerrain.TerrainAt(region, …, q, r, radius)` | per-region offset + muster-core land guard | `TerrainAt(Q, R, Cols, Rows)` global; muster-core guard becomes "band centre column is land" — G1 |
| `PlanetViewWindow` | per-region disk projection | sliding cylinder window — G5 |
| Tests (`PlanetRegionsTests` / `GroundForcesTests` / `CityGridTests`) | assume disks + `(0,0)` + `PatchRadius` | re-pointed at the grid, slice by slice |

**Kept unchanged:** the coarse `PlanetRegionsDB` ring (ownership / area / crossing-time / neighbours / `InstallationIds`), combat/capture *per region* (now per band), C-track city grid *under* a hex, and V2's `WorldTerrain` field math (only its inputs change from per-region to global).

---

## Prime-Directive connections

- **Galaxy** — `PlanetGridFactory` + `SurfaceGrid` replace `PlanetHexFactory` disks; `PlanetRegionsDB` keeps the coarse ring, gains the grid, its regions become column bands.
- **GroundCombat** — units/movement/combat address hexes globally; capture still flips a region (band) owner; W1 footprints + C1 city grids hang off the (now global) `GroundHex` unchanged in shape.
- **Client** — `PlanetViewWindow` becomes a sliding cylinder window (the continuous world).

**Do NOT** keep both hex representations past G6 — the dual `Region.Hexes` + `SurfaceGrid` is a temporary migration scaffold, deleted once consumers are on the grid.
