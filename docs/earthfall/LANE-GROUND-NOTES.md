# LANE GROUND — pending dashboard rows, cross-lane requests, developer decisions

This is the GROUND lane's conflict-free notes file (CAMPAIGN-PLAN.md §2.4). Lanes do NOT edit the shared
dashboards (`docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`) or a
collision-prone subsystem `CLAUDE.md` mid-flight; each pending row lands here and the integration phase (P8.2)
applies them. Sections are delimited by slice so parallel siblings can append without conflict.

---

## G1.1 — GroundConstructorAtb (combat engineer) + surface parts haulage (2026-07-18)

**Files created (this slice, all inside the GROUND fence):**
- NEW `Pulsar4X/GameEngine/GroundCombat/GroundConstructorAtb.cs` — the ground-mountable field-constructor COMPONENT
  (`IComponentDesignAttribute`, the ground echo of `Construction.ConstructorAtb`). One dial `BuildRate` (build-points/day);
  single double-arg ctor (exact-arity JSON binder — GroundCombat gotcha 6). Inert on install (read by the future
  on-site-build order via `TryGetComponentsByAttribute`), never read in combat. A combat engineer = a chassis carrying
  this part (LOCKED PRINCIPLE: a role is a component, never a unit type).
- NEW `Pulsar4X/GameEngine/GroundCombat/GroundParts.cs` — SURFACE PARTS HAULAGE. `AddParts(body, region, designId, count)`
  (the land primitive — merges into the per-region crate, creates the roster on demand), `PartCount`/`PartsInRegion`
  (read accessors), `LandPartsFromShip(ship, targetBody, region, designId, count)` (the full haul — gated on
  `GroundTransport.ShipIsAtBody` + `HasOrbitalControl`, resolves the design off the ship's faction, CHECK-THEN-CONSUME
  from the ship's pooled cargo holds via `Construction.ConstructionCargo`, then `AddParts`). All defensive/never-throw.

**Files changed (this slice, all inside the GROUND fence):**
- `Pulsar4X/GameEngine/GroundCombat/GroundForcesDB.cs` — added the save-safe `SurfacePart` record (RegionIndex/DesignId/
  Count + copy ctor) and `GroundForcesDB.SurfaceParts` (`[JsonProperty] List<SurfacePart>`, default empty), with the
  matching deep-copy line in the `GroundForcesDB(GroundForcesDB other)` ctor (null-guarded). The per-region surface pool.
- `Pulsar4X/GameData/basemod/TemplateFiles/installations.json` — new `ground-constructor` ComponentTemplate (MountType
  `GroundUnit`, `groundConstructorArgs` → `Pulsar4X.GroundCombat.GroundConstructorAtb`).
- `Pulsar4X/GameData/basemod/ScenarioFiles/designs/componentDesigns.json` — new `default-design-ground-constructor` design.
- `Pulsar4X/GameData/basemod/ScenarioFiles/systems/sol/earth.json` — added `ground-constructor` (StartingItems template id)
  + `default-design-ground-constructor` (ComponentDesigns). Materials reuse already-unlocked stainless-steel/aluminium →
  StartingItems materials untouched. Six-point registration complete (point 6, scenario faction files, is N/A: player-only).
- NEW `Pulsar4X/Pulsar4X.Tests/EfGroundConstructorTests.cs` — the two-halves gauge (below).

**PENDING subsystem-CLAUDE.md rows — `GameEngine/GroundCombat/CLAUDE.md` (NOT edited to avoid a G1 sibling collision on
the shared lane doc; merge these at integration, per the prompt's escape hatch):**

Add to the primary File Map table:
| File | Role | Status |
|------|------|--------|
| `GroundConstructorAtb.cs` | **NEW (G1.1 combat engineer, 2026-07-18)** The ground-mountable field-constructor COMPONENT — the ground echo of `Construction.ConstructorAtb`. Mounts on `GroundUnit`; one dial `BuildRate` (build-points/day). Makes a combat engineer "a chassis carrying a constructor part," NOT a unit type (LOCKED PRINCIPLE). Inert on install (the on-site-build order reads it at build time); never read in combat. Single double-arg ctor (exact-arity binder, gotcha 6). Six-point registered base-mod `ground-constructor`. Gauge: `EfGroundConstructorTests`. | ✅ G1.1 |
| `GroundParts.cs` | **NEW (G1.1 surface parts haulage, 2026-07-18)** Lands crated component parts onto the per-region surface pool (`GroundForcesDB.SurfaceParts`) so a combat engineer can assemble a footprint building on site with no colony (the beachhead enabler, A5's "surface component haulage — MISSING"). `AddParts` (land primitive), `PartCount`/`PartsInRegion` (reads), `LandPartsFromShip` (full haul — gated like troop landing + CHECK-THEN-CONSUME from ship cargo via `Construction.ConstructionCargo`; the CARRY half reuses the existing cargo hold, so only the destination + land step were added — the least-invasive of the two candidates). Defensive/never-throw. The on-site build that CONSUMES the pool is a later G1 slice. Gauge: `EfGroundConstructorTests`. | ✅ G1.1 (land only) |

Add a `GroundForcesDB` note (Gotchas / File Map row): `GroundForcesDB.SurfaceParts` is the per-region crated-parts pool
(save-safe, deep-copied); default empty and read/written ONLY by `GroundParts`, so a body with no landed parts is
byte-identical.

**PENDING dashboard rows (integration P8.2 lands these):**
- `docs/DOCS-INDEX.md`: no new doc file added this slice (design refs already indexed: `SURFACE-FOG-AND-RECON-DESIGN.md`,
  `GROUND-SURFACE-MAP-DESIGN.md`). No row change required.
- `docs/TESTING-TRACKER.md` (Layer-1 engine CI): NEW row — **`EfGroundConstructorTests`** · what: the combat-engineer
  atb JSON-binding + surface parts haulage · why: gotcha-10 sensor for the new six-point component + proves parts land
  and are readable (save-safe) · method: `TestScenario.CreateWithColony`, engine-only, `rest` shard · right-looks-like:
  ground-constructor binds `GroundConstructorAtb` (BuildRate 100) + mounts GroundUnit; AddParts merges per region + reads
  back + deep-copy independent; LandPartsFromShip hauls from cargo (check-then-consume) + guards refuse · most-likely-
  failure: a template/ctor-arity drift (reds here not on New Game) · unblocks: G1.2 colony-free on-site build, PW resolver
  beachhead-build rung.
- `docs/SYSTEM-CONNECTION-MAP.md`: NEW connections — **GroundCombat ↔ Construction** (`GroundParts.LandPartsFromShip`
  reuses `Construction.ConstructionCargo` GatherPooledHolds/TryConsumePooled — the shared fleet-pooled cargo helper);
  **GroundCombat ↔ Storage** (crated parts are `ComponentDesign` ICargoable units drawn from a ship's `CargoStorageDB`);
  **GroundConstructorAtb → (future) on-site-build order** (read at build time, the G1.2 consumer); **GroundParts.SurfaceParts
  → (future) on-site-build order** (the parts it consumes into a footprint building).

**Tests added + what they assert (`EfGroundConstructorTests`, 3 tests, engine-only → CI `rest` shard):**
- `GroundConstructor_LoadsFromJson_BindsItsAtb_AndMountsOnGroundUnits` — the six-point / gotcha-10 sensor: the base-mod
  `default-design-ground-constructor` loads onto the start faction, binds a `GroundConstructorAtb` from JSON with the
  template default `BuildRate` (100), and mounts on a GroundUnit (proves the template→atb arity path + the six-point chain).
- `SurfaceParts_LandAndAreReadable_AndThePoolIsDeepCopied` — `AddParts` lands crates onto a body's per-region pool (a
  same-design/region drop MERGES), they read back per design + per region (region-scoped), bad args mutate nothing, and
  the pool is DEEP-copied (a `Clone()` carries the crates and is independent of a later mutation to the original) — the
  save-safety proof.
- `LandPartsFromShip_HaulsCratedPartsOntoTheSurface_AndGuardsRefuse` — a cargo ship parked AT the body carrying crated
  foundations lands them onto the surface (drawn from its hold, gated on orbital control); a short pool lands nothing and
  drains nothing (check-then-consume); a null ship / unknown design id are refused (never throws).

**Byte-identity claim: (b) provably inert absent new data.**
- The `GroundConstructorAtb` + its six-point base-mod registration add a new *available* buildable, but NO default
  garrison, scenario faction, or start colony fields it (identical to how `ground-radar`/`ground-locomotion` shipped
  byte-identical). Nothing mounts it → no runtime behaviour change; it's inert on install. `BaseModIntegrityTests` still
  builds it (materials pre-unlocked) so it stays green.
- The `GroundForcesDB.SurfaceParts` pool defaults empty and is mutated ONLY by the new `GroundParts.AddParts`/
  `LandPartsFromShip`, which NO existing engine code path calls; no existing processor reads it. Adding an empty-list
  `[JsonProperty]` round-trips (`[]`) and old saves keep the initializer default. So absent a call to the new helpers
  (i.e. absent new data), engine behaviour and save round-trips are unchanged; every existing green test stays green.

**FLAGGED balance values (developer sets; all in `installations.json` `ground-constructor`, noted in its Description
+ the BuildRate property description since JSON has no comments):**
- `BuildRate` default **100** bp/day (range 10–500). The build-rate dial's default + range.
- `Mass = BuildRate * 2` — the carry/build-mass coupling (heavier the faster it builds).
- `CreditCost = [Mass] * 0.3`, `BuildPointCost = [Mass] * 2`, `ResourceCost` stainless-steel `[Mass]*0.5` / aluminium
  `[Mass]*0.3` — the cost curve.

**DEVELOPER DECISIONS raised by this slice:**
1. **Combat-engineer frame fit.** At the default `BuildRate` 100 the constructor's mass is 200, which EXCEEDS the default
   `human-frame` carry budget (BaseStrength 100) and its 0.5×-budget per-item limit — so a combat engineer wants a bigger
   frame (vehicle/walker) or a lower build-rate. This is realistic (a construction rig is heavy vehicle-borne gear), but
   the developer may want to (a) tune the default BuildRate/Mass so an engineer fits a human frame, or (b) author a
   dedicated engineer VEHICLE frame. Left as a balance/UX call; the atb + haulage are frame-agnostic.
2. **Surface-parts region validation.** `AddParts` accepts any `regionIndex >= 0` without validating it against the
   body's region count (the caller lands into a region it holds; `LandPartsFromShip` gates on orbital control, not region
   ownership yet). A region-ownership gate on landing (land only into a region you hold) is a natural G1.2/PW refinement —
   flagged, not built.

**CROSS-LANE REQUESTS:**
- **→ CORE (PW):** the resolver's beachhead-build rung should (a) issue `GroundParts.LandPartsFromShip` (or `AddParts` for
  an AI that pre-stages) to land a footprint building's parts onto a held region, then (b) drive the future on-site-build
  order that reads `GroundConstructorAtb.BuildRate` + consumes `GroundForcesDB.SurfaceParts`. G1.1 provides the land +
  read surface; the CONSUME step (parts → footprint building) is the next GROUND slice (G1.2). No CORE edit needed yet —
  this is a heads-up on the surface PW/G1.2 will consume.
- **→ CLIENT (C-lane):** a surface "landed parts" readout (per region: designId → count via `GroundParts.PartsInRegion`)
  and an "unload parts" button (`GroundParts.LandPartsFromShip`) are the client surface for beachhead staging — optional,
  post-merge, no dependency on this slice beyond the two public read/land APIs.
