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

---

## G1.2 — colony-free on-site ground build + FOB role (2026-07-18)

Continues G1.1. The CONSUME half: a landed COMBAT ENGINEER (a chassis carrying `GroundConstructorAtb`, G1.1) on
friendly-held, enemy-free ground with landed footprint parts (`GroundParts`, G1.1) now ERECTS a footprint building on
site over ground ticks — with NO colony present — hosted in the invader's beachhead OUTPOST. The placed bunker fortifies
(the `GroundDefenseAtb` path), is a bombard/capture target (grave rung), and marks the region a FOB resupply point (G2).

**Files created (this slice, all inside the GROUND fence):**
- NEW `Pulsar4X/GameEngine/GroundCombat/GroundBeachhead.cs` — the beachhead engine. `TickBuilds(body, deltaSeconds)` (the
  per-tick step, folded into the ground hotloop — L9, not a second processor): for each (faction, region) with an IDLE
  combat engineer standing in it, region FRIENDLY-HELD + ENEMY-FREE, and a buildable FOOTPRINT part landed, accrue
  Σ `GroundConstructorAtb.BuildRate` × elapsed-days onto a `GroundBuildSite`; on reaching the building's assembly effort
  consume one crate (`GroundParts.ConsumePart`) + place the footprint building (`PlaceBuilding` → the invader's
  outpost + region/hex war map). `EnsureOutpost(body, factionId)` (find-or-create the per-faction bare-store host —
  the SAME store a ground unit's backing entity uses). `HasBeachhead(body, factionId, regionIndex)` (the G2 resupply-point
  READ — a footprint the faction's own outpost hosts in a region it holds). Reads the constructor off the unit's backing
  store exactly like `GroundSensors` reads a radar. Defensive/never-throw (L4).
- NEW `Pulsar4X/Pulsar4X.Tests/EfBeachheadBuildTests.cs` — the two-gauge fixture (below).

**Files changed (this slice, all inside the GROUND fence):**
- `GroundForcesDB.cs` — added the save-safe `GroundBuildSite` record (RegionIndex/DesignId/RequiredPoints/ProgressPoints +
  copy ctor) and TWO `[JsonProperty]` lists on `GroundForcesDB`: `OutpostEntityIds` (`List<int>` — the per-faction
  beachhead outpost host entity ids) + `BuildSites` (`List<GroundBuildSite>` — in-progress builds), both default-empty
  with deep-copy lines in the clone ctor.
- `GroundParts.cs` (a G1.1 file — my fence) — added `ConsumePart(body, region, designId, count)` (the on-site-build draw:
  CHECK-THEN-CONSUME, drops an emptied crate; the counterpart of `AddParts`). *Note: this edits a file G1.1 created; both
  changes live in the working tree — the landing session sequences the commits.*
- `GroundBuildings.cs` — added `BodyComponentStores(body)` (the SINGLE source of truth: every store on the body =
  colonies + beachhead outposts) and refactored `IndexBodyComponents` / `FootprintTilesFor` / `BuildingNamesOnBody` to
  use it (byte-identical when no outposts); added `LocateInstanceOnHexes(body, regionsDB, region, instanceId)` (the
  beachhead twin of the colony `LocateFootprintsOnHexes`/`…GlobalHexes` for one freshly-built footprint — region centre
  hex + global band muster hex).
- `GroundFortification.cs` — `BuildResolver(body)` now walks `GroundBuildings.BodyComponentStores(body)` (colonies +
  outposts) instead of colonies-only, so a beachhead bunker fortifies (additive → byte-identical with no outposts).
- `GroundForcesProcessor.cs` — folded `GroundBeachhead.TickBuilds(body, deltaSeconds)` into `ProcessBody` as step 0c
  (after upkeep 0b) — L9 (no new hotloop).

**PENDING subsystem-CLAUDE.md rows — `GameEngine/GroundCombat/CLAUDE.md` (NOT edited, to avoid a G1/G2/G3/G4 sibling
collision on the shared lane doc — following G1.1's precedent + the campaign escape hatch; merge at integration):**

Add to the File Map table:
| File | Role | Status |
|------|------|--------|
| `GroundBeachhead.cs` | **NEW (G1.2 colony-free on-site build / FOB, 2026-07-18)** The CONSUME half of the combat-engineer chain: `TickBuilds` (a ground-tick STEP, L9 — not a second processor) has a landed engineer (`GroundConstructorAtb`) on FRIENDLY-HELD, enemy-free ground with landed footprint parts ERECT a footprint building on site over ticks (Σ `BuildRate` × days vs the building's `IndustryPointCosts`), consuming one crate and placing it into the invader's beachhead OUTPOST (a bare `ComponentInstancesDB` host, the backing-entity pattern — no colony needed) + region/hex war map. `EnsureOutpost` (per-faction host, registered on `GroundForcesDB.OutpostEntityIds`), `HasBeachhead` (the G2 resupply-point read). Byte-identical until an engineer unit exists and lands. Gauge: `EfBeachheadBuildTests`. | ✅ G1.2 |

Add to the `GroundBuildings.cs` row: **+ G1.2** `BodyComponentStores(body)` — the single source of truth (colonies + beachhead outposts) the fortification resolver / bombard index / readouts all walk, so a colony-free beachhead building fortifies/bombs/reads out like a colony's (additive → byte-identical); `LocateInstanceOnHexes` — place one freshly-built footprint on the region centre + global band muster hex.

Add to the `GroundFortification.cs` row: **+ G1.2** `BuildResolver` walks `GroundBuildings.BodyComponentStores` (colonies + beachhead outposts), so a beachhead bunker fortifies.

Add to the `GroundForcesProcessor.cs` row: **+ G1.2 step 0c** `GroundBeachhead.TickBuilds` (colony-free on-site build; L9, folded into the tick).

Add a `GroundForcesDB` note: `GroundForcesDB.OutpostEntityIds` (per-faction beachhead outpost host ids) + `GroundForcesDB.BuildSites` (in-progress on-site builds) are save-safe, deep-copied, default-empty and read/written ONLY by `GroundBeachhead` → byte-identical.

**Tests added + what they assert (`EfBeachheadBuildTests`, 2 tests, engine-only → CI `rest` shard):**
- `Engineer_BuildsAndFortifiesABeachhead_ColonyFree_AndItCanBeBombed` — the happy path + grave rung: an engineer on a
  held region with a landed bunker crate does NOT finish in one day (a `GroundBuildSite` accrues ~`BuildRate`) but DOES
  after the full engineer-days are driven; exactly one new installation appears in the region, the crate is consumed, the
  site clears; the bunker is hosted in a beachhead OUTPOST (NOT the colony store) yet is found by the shared
  `BodyComponentStores` index; `GroundFortification.BuildResolver` resolves the new id to its `GroundDefenseAtb` and the
  region's `DefenseMult` RISES above its pre-beachhead value (fortification readable, colony-free); `HasBeachhead` reads
  true (FOB resupply point); and `GroundBuildings.BombardHex` DESTROYS it (proves the outpost is indexed for
  bombard/capture — the grave rung), returning fortification to baseline + `HasBeachhead` to false.
- `NoBuild_WithoutHeldGround_WithAnEnemy_OrWithoutParts` — the three gates in one scenario: an engineer + parts on ground
  the faction does NOT hold builds nothing; an engineer + parts in a region an enemy contests builds nothing; an engineer
  on held ground with NO parts builds nothing (would-be crates untouched, no outpost created); THEN removing the enemy
  unblocks the contested region (it builds), proving the enemy was the only blocker.

**Byte-identity claim: (b) provably inert absent new data.**
- `GroundBeachhead.TickBuilds` runs every ground tick, but on its first pass over `forces.Units` it finds NO combat
  engineer (a unit whose backing store carries a `GroundConstructorAtb`) and returns having changed nothing — a cheap
  read-only pass. NOTHING fields such a unit by default (the base-mod `ground-constructor` is buildable but unbuilt, and
  no garrison/scenario/start data mounts it), so in every existing scenario and test the step is a no-op → all existing
  green tests unaffected.
- The two new `GroundForcesDB` lists (`OutpostEntityIds`, `BuildSites`) default empty and are read/written ONLY by
  `GroundBeachhead`; no existing code path calls it. Empty `[JsonProperty]` lists round-trip as `[]` and old saves keep
  the initializer default.
- The reader refactors (`BodyComponentStores` behind `IndexBodyComponents` / `FootprintTilesFor` / `BuildingNamesOnBody` /
  `GroundFortification.BuildResolver`) yield EXACTLY the colony stores when `OutpostEntityIds` is empty (colonies first,
  same filter) → byte-identical fortification/bombard/readout for every existing (colony-only) body.

**FLAGGED balance values (developer sets):**
- `GroundBeachhead.MinAssemblyEffort` = **100** build-points — the FLOOR on a footprint building's on-site assembly
  effort (used only when the building's own `IndustryPointCosts` is smaller). The effort itself is the building's own
  industry cost (NO new per-building number); the engineer's rate is its `GroundConstructorAtb.BuildRate` (G1.1's flagged
  dial). So a stock bunker (`IndustryPointCosts` 50,000) at the default 100 bp/day takes ~500 engineer-days — a real
  beachhead-build cadence the developer will likely want to tune via the two existing dials, not a third number.

**DEVELOPER DECISIONS raised by this slice:**
1. **The ownership solution — a per-faction BEACHHEAD OUTPOST (chosen), with the alternatives.** Every building is a
   `ComponentInstance` living in a HOST store; the fortification / bombard / readout code all resolve a building id back
   to its host by walking the body's COLONIES. On an enemy world the invader has no colony → no host. **Chosen (verified
   least-invasive):** a per-faction *beachhead outpost* — a faction-owned entity carrying a BARE `ComponentInstancesDB`,
   created on demand (`GroundBeachhead.EnsureOutpost`) and registered on `GroundForcesDB.OutpostEntityIds`. This is the
   EXACT store a ground unit's backing entity already uses (`GroundUnitEntity`) — proven inert (no processor iterates
   `ComponentInstancesDB`; no position/name → invisible to map/combat/sensors) — so it adds no new machinery and no new
   save-type risk. The four resolvers were pointed at one shared list (`GroundBuildings.BodyComponentStores` = colonies +
   outposts), additive/byte-identical when no outpost exists. **Alternatives considered + rejected:**
   - *(A) Give the outpost a full `ColonyInfoDB`* so the existing colony-walk resolvers find it with ZERO reader change.
     Rejected: heavy + risky — population/industry/mining processors iterate `ColonyInfoDB`; a bare hand-built colony
     risks NREs in OTHER lanes' processors (not least-invasive; the exact "dead code that looks live" trap inverted).
   - *(B) Host the building in the invader's nearest OWN colony store* (e.g. UMF Mars) and reference the id on the target
     body's region. Rejected: `GroundFortification.BuildResolver(targetBody)` only walks colonies whose `PlanetEntity ==
     targetBody`, so a Mars-hosted bunker would NOT fortify Earth without a broader galaxy-wide resolver change; and it's
     semantically wrong (a building "on Earth" living in Mars's store; capture/transfer semantics break).
   - *(C) A new `GroundOutpostDB` marker DataBlob* on the host. Rejected in favour of the marker-free int-registry
     (`OutpostEntityIds`) to EXACTLY mirror the backing-entity precedent — no new save type, least-invasive. (If a future
     slice wants outposts self-describing for the client, adding the marker is a clean additive follow-up.)
   **Open sub-decisions for the developer (deferred, flagged):** (i) does capturing a region TRANSFER the outpost's
   buildings to the captor (today a captured beachhead's `ComponentInstance` stays in the original faction's outpost —
   same open question as R4 decision #4 for colony buildings), and (ii) a region-path bombard clears the region-disk hex
   ref but not the global-band-hex ref (a pre-existing dual-grid property shared with colony footprints — the stale ref
   resolves to nothing, harmless).
2. **One building per (faction, region) per tick.** `TickBuilds` completes at most one building per cell per tick (no
   inner while-loop); in the real hourly sim this is exactly-when-crossed, one at a time. Fine for v1; flagged in case a
   future slice wants multiple simultaneous sites per region.

**CROSS-LANE REQUESTS:**
- **→ CORE (PW):** the beachhead-build rung is now BUILT engine-side — PW's resolver just needs to (a) land parts onto a
  held region (`GroundParts.LandPartsFromShip`/`AddParts`, G1.1) and (b) ensure an engineer unit is present + idle there;
  `GroundBeachhead.TickBuilds` (running in the ground hotloop) does the rest automatically. PW does NOT need to call
  `TickBuilds` — it runs on the tick. The landing-region scorer should prefer a region the AI holds (so the build gate
  passes). No CORE edit to GROUND files needed.
- **→ GROUND G2:** the FOB "resupply point" is `GroundBeachhead.HasBeachhead(body, factionId, regionIndex)` (a region the
  faction holds containing a footprint its own outpost hosts). G2's ammo-resupply caller can treat a region where this
  reads true as a depot source (alongside the existing friendly-held rule in `GroundForces.ResupplyUnit`).
- **→ CLIENT (C-lane):** a "beachhead outpost" readout (per body, per faction: the buildings in `OutpostEntityIds`
  stores, via `GroundBuildings.BuildingNamesOnBody` which now includes outpost buildings) + a build-progress readout
  (`GroundForcesDB.BuildSites`: region → design → ProgressPoints/RequiredPoints) are the client surface for beachhead
  construction — optional, post-merge, no dependency beyond the public reads.
