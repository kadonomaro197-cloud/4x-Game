# Hex Ground Map + Rich Order Catalog — Design & Roadmap

**As of 2026-07-04.** The developer's direction: the ground map needs to be *more exact* — planetary regions made of **hexes** (Civ-style), so you can order a formation from **London to Paris** (two hexes) and watch it transit in real ticks, with **terrain and hazards living on the hexes**. Larger planet → more hexes. And the same richness is wanted for **orders**: today you can give a fleet or formation only a handful of commands; Aurora has ~60. Build toward that.

This is a large, two-track initiative built the usual way — engine-first, one CI-verified slice at a time.

---

## Locked decisions (2026-07-04)

- **Hierarchy: Planet → Region → Hex.** Keep the 4-region ring as the *coarse* layer (it already solves the sphere's global topology — no seam, the "Pacific survives"). Each region holds a **local hex patch**. Terrain + hazards move *down* onto hexes; units live and move hex-by-hex; a formation crosses region borders at the patch edge. This preserves everything already built (survey, ownership, fortification, combat) and adds the fine layer under it — and it sidesteps the "you can't tile a sphere with only hexes" problem (the ring is the globe, hexes are the ground).
- **Density: Operational — Earth ≈ 1,800 hexes** (≈ radius-12 patch per region, 4 patches). Scale by planet radius so bigger worlds get more hexes; Mars ≈ half; gas/ice giants have no surface → no hexes.
- **Place names: coordinates only for now** — hexes are `(q, r)`; a naming pass ("London", "Paris") comes later once the grid + movement feel right.
- **Performance rule (load-bearing): hexes are generated LAZILY.** Coarse regions still generate for every major body at galaxy-gen (cheap — 4 per body). The **hex patches generate on demand** for a specific body the first time it becomes a theatre (colonized / garrisoned / invaded / the tactical view is opened). A galaxy does **not** carry millions of hexes; only the worlds the game actually touches do. Idempotent + save-safe.

---

## The model

**`GroundHex`** (a save-safe data object, like `RegionFeature`/`GroundUnit`): `Q`, `R` (axial coords), `Terrain` (a `RegionFeatureType`), later `OwnerFactionID` + a per-hex hazard. Stored as a `List<GroundHex>` on each **`Region`** (`Region.Hexes`), `[JsonProperty]` + deep-copied — the save-safety the old `ColonyHexMapDB` lacked.

**Generation** (`PlanetHexFactory.EnsureHexesForBody(body)`): for each region, build a hex **disk** of radius `HexPatchRadiusFor(planetRadius)` (Earth → 12; radius scales linearly with planet radius so hex *count* ∝ surface area; clamped ~[2, 24] to bound cost). Each hex is assigned a terrain drawn from the **region's existing `Features` distribution** (a region that's 40% Plains / 30% Forest gets ~that split across its hexes) — so the fine map is a faithful realization of the coarse one. Seeded by the system RNG (deterministic). Idempotent (skips a region that already has hexes).

**Reused primitive:** `HexCoordinate` (`Colonies/HexCoordinate.cs`) is a solid axial-hex struct (distance, 6-neighbours, radius) — used for movement/pathfinding math (H2). The disk *generation* is a plain axial loop, so H1 has no cross-namespace dependency.

---

## Roadmap — Ground hex track (H)

- **H1 — the hex data layer (foundation).** `GroundHex` + `Region.Hexes` + `PlanetHexFactory.EnsureHexesForBody` (lazy, scaled, terrain-per-hex) + the lazy hook at colony creation (Earth gets hexes) + the pure density functions. Additive — region-level combat/movement keep working. Gauge: `GroundForcesTests`/`PlanetRegionsTests` (Earth region ≈ 469 hexes; radius scales; terrain assigned; idempotent; clone-safe). **← START HERE.**
- **H2 — hex movement + pathfinding.** `GroundUnit` gains a hex position `(Q,R)` within its region; A* over the hex disk (terrain-weighted cost), crossing region borders. `GroundForces.OrderFormationMove` becomes hex-target pathing; formations transit hex-to-hex in ticks. Region-level `OrderMove` stays as the coarse fallback until the client is hex-native.
- **H3 — combat + terrain/hazards on hexes.** Units fight when on the same/adjacent hex; terrain cover / triangle / fortification read the **hex's** terrain; hazards move from `PlanetEnvironmentsDB` (region) to per-hex. The region-level resolver migrates down to hex-adjacency.
- **H4 — the client hex map.** `PlanetViewWindow` renders the hex grid (SDL/ImGui), unit tokens on hexes, click-a-hex-to-move, terrain/hazard shading. The big visible payoff.
- **H5 — place names.** Name standout hexes procedurally (capital, ports, passes).

## Roadmap — Order catalog track (O)

*Findings from the order survey (2026-07-04): ~51 `EntityCommand` classes exist but only ~5 are reachable from the fleet "Issue Orders" tab (a hardcoded `switch`, not a registry). No true multi-order queue / waypoint chaining. The conditional/standing-order framework has exactly ONE condition (`FuelCondition`). Ground formations don't go through the order pipeline at all (direct `GroundForces.*` calls). `INavAction` is doc-only; the nav-actions (`RefuelAction`/…) are stubbed `EntityCommand`s.*

- **O1 — the order-catalog framework.** A data-driven **order registry** (an order descriptor: name, category, which entities it applies to, its target/param shape) so the UI is generated, not a 60-case `switch`; a real **multi-order queue / waypoint chain** per fleet+formation (sequential "then" semantics, not just action-lane masking); and bring **ground formations into the `EntityCommand` pipeline** (a `FormationOrderableDB` or route formation orders through the colony/body).
- **O2 — condition vocabulary.** Grow `ICondition` beyond `FuelCondition`: location/proximity, cargo, health, enemy-detected, time/date, fuel/ammo — the substrate for Aurora-style conditional standing orders.
- **O3+ — order batches toward ~60.** Move/waypoint/patrol/follow/escort/hold/picket; load/unload cargo + colonists + troops; refuel/resupply (finish the stubs); join/detach/transfer; survey variants; ground: move-to-hex/attack-hex/dig-in/garrison/bombard-support/load-to-transport. Each batch is a slice: descriptor + execute + UI.

**Open decision for O1 (flag, not yet locked):** do the combat/doctrine/EMCON/ground *direct-call* actions join the `EntityCommand` pipeline (uniform queue/UI/replay, but must handle the engagement-lock bypass) or stay direct? Lean: give them descriptors for the UI/registry while keeping their direct execution, so the catalog is uniform to the player without losing the mid-battle bypass.

---

## Prime-Directive connections

- **Galaxy / `PlanetRegionsDB`** — hexes hang off `Region`; the coarse layer stays the topology + survey + ownership owner. Hex gen reads `MassVolumeDB.RadiusInM` (Earth ref `6.371e6`) + the region's `Features`.
- **GroundCombat** — `GroundUnit` gains a hex position (H2); the resolver reads hex terrain (H3); `GroundFortification` reads the hex's `InstallationIds` (H3). Everything built stays; it gets *finer*.
- **Orders (`Engine/Orders`)** — the catalog framework (O1) generalizes `EntityCommand`/`OrderableDB` and pulls ground formations in. `FleetDB.StandingOrders` + `ConditionalOrder` are the standing-order base to grow.
- **Client** — `PlanetViewWindow` becomes the hex renderer (H4); the fleet "Issue Orders" tab becomes registry-driven (O1).

**Do NOT** regenerate hexes for every galaxy body — lazy gen is the rule. **Do NOT** rip out the region layer — hexes nest under it.
