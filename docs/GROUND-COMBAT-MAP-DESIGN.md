# Ground Combat — The Planet Map (Design Lock)

**As of 2026-07-04.** Status: **design-locked; slices 1–4 built + green (regions · build-at-region · planet view · survey-reveal). Slice 5 (ground combat) scoped to FULL TACTICAL, shipping as CI-gated sub-slices 5a–5i.**
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
- **`Surveyed`** — features are only "known" once true. **You know the ground where you SETTLE: nothing is pre-surveyed at generation — a colony reveals its own world (`ColonyFactory`, `RevealAll()`), and everything else is fog until a geo survey scans it.** So Earth (home) is known, but Luna, Mars, and Alpha Centauri all start as fog to explore. *(Corrected 2026-07-04 from the earlier "authored worlds start surveyed" rule — even in Sol, only the world you actually colonise is known; the rest are survey targets. This is what makes "survey Luna" a real test.)*
- **`Features`** — a **bundle** of `RegionFeature` (`Ocean`, `Coast`, `Mountains`, `Forest`, `Desert`, `Ice`, …), *not* a single type. A region is mountains + forest + coast at once.
- **`InstallationIds`** — what's built here (populated by the build-at-a-region slice).

**Persistent from day one** (`Clone()` + `[JsonProperty]` + deep-copy ctors). The earlier `ColonyHexMapDB` was fatally non-persistent (no `Clone`); we do not repeat that.

## Generation (`GameEngine/Galaxy/PlanetRegionsFactory.cs`)

`GenerateForSystem(system, surveyed)` — mirrors `LagrangeFactory`: defensive, idempotent, seeded by the system RNG. For each major body — **planets, giants, dwarfs, AND moons** (Luna/Ganymede are ground-combat places you fight over, so moons get a region layer too, added 2026-07-04) — it builds 4 ring regions and rolls **random-but-logical** features from the world's own reliable scalars — a wet world (`AtmosphereDB.HydrosphereExtent`) gets ocean/coast; a tectonically active world gets mountains; a gas giant gets gas layers. **`surveyed` is now `false` from ALL four gen paths** (procedural + Sol + blueprint); the "known" state comes from `ColonyFactory` calling `RevealAll()` on the world you colonise — so the home planet is known and its siblings are fog. Hooked at all four gen paths in `StarSystemFactory`.

Gauge: `Pulsar4X.Tests/PlanetRegionsTests.cs` — 4-region ring + wrapping adjacency, area/crossing-time, home-surveyed-siblings-fogged (the reveal-on-colony model), moon-gets-a-layer (Luna is surveyable), wet-world-has-ocean (the "logical" gauge), deep-clone (persistence), idempotency, and the survey-reveal wire (`SurveyReveal_*`).

---

## Scope: this is the *map*, not the *battle*

`docs/MVP.md`'s "take a planet" finish line is the combat spine (build a unit → drop → auto-resolve vs. the garrison → flip ownership) and needs **no map**. The developer's priority is the opposite axis — the **living map** (where things are, how far, who defends what). So we build the *place* first. That's a deliberate, eyes-open order-of-operations call, not a detour.

### Slice plan (each its own CI-gated commit)

1. **Region layer** — `PlanetRegionsDB` + generation + tests. **✅ built + green.**
2. **Build at a region** — `PlaceInstallationInRegionOrder`: place an installation *in a chosen region* via the real `OrderHandler.HandleOrder` player-path (deploy-at-a-location, one level down from the station deploy), commanded by the colony. Records the instance id in `Region.InstallationIds` — a **new located axis**; the colony's existing abstract installations are left untouched. **✅ built** (gauge `PlanetRegionsTests.BuildInRegion_*`). *(Refinements: materials + build-time via a region-targeted `IndustryJob`; building on an uncolonised region via a construction unit.)*
3. **Planet view (client)** — a flat 3-region `PlanetViewWindow` (`Pulsar4X.Client/Interface/Windows/PlanetViewWindow.cs`): you see the centre region plus its two ring neighbours, rotate with ◀/▶ (or click a side region), each region painted as stacked terrain bands (ocean/mountains/forest/… sized by coverage), an UNSURVEYED region drawn as fog. Reachable from the planet's right-click context menu ("Planet view (regions)"), gated on the body having a `PlanetRegionsDB`; thin defensive draw (all reads off the CI-tested blob, body wrapped so a throw can't skip `Window.End`). **✅ built** — CI compiles the client but can't RUN it, so live render/feel is the developer's local-build check (T-row added to `docs/TESTING-TRACKER.md`). *(Refinements: to-scale zoom, and clicking a region to issue the build-at-a-region order from here.)*
4. **Survey reveal** — completing a **geological survey** of a world flips its regions from fog to KNOWN. Wired at the single survey-complete site (`GeoSurveyProcessor.ProcessEntity`, right beside the existing mineral-access grant) via a new `PlanetRegionsDB.RevealAll()`. A procedurally-generated world's regions start fogged (`surveyed:false` at gen); surveying it reveals them, so the planet-view map (which already reads `Region.Surveyed` live) shows real terrain the moment the survey finishes — **the exploration→map link, and it needed no client change** (Connect verified). v1 reveal is world-level + faction-agnostic (a single `Surveyed` bool per region, matching the design); per-faction region fog is a documented refinement (the mineral partial-access-mask pattern). **✅ built** (gauges `PlanetRegionsTests.SurveyReveal_RevealAll_*` + `SurveyReveal_CompletingGeoSurvey_RevealsRegions`, the latter through the real `GeoSurveyProcessor`). **← next: ground units (slice 5).**
**Slice 5 — ground combat. v1 TARGET = FULL TACTICAL (developer decision, 2026-07-04).** Not just an auto-resolve garrison — unit *types*, terrain-as-leverage, base-defends-city coverage, formations, AND the navigable click-to-place/units-on-surface map. The target is the ceiling; it still ships as CI-gated **sub-slices** toward that ceiling (a single untested "full tactical" commit is what breaks a branch). Reuse map confirmed: `AutoResolve.Resolve(sideA, sideB, config)` is the strength-math engine, `ShipCombatValueDB` (Firepower J/s · Toughness J · Evasion) is the scale model to mirror, `IConstructableDesign` (`UniqueID`/`ResourceCosts`/`IndustryPointCosts`/`OnConstructionComplete`) is the build interface a `GroundUnitDesign` slots into. The spine ships first, depth stacks on a working base:

- **5a — raise a unit. ✅ built (2026-07-04).** `GroundUnitDesign : IConstructableDesign` (rides the existing industry rails — its `OnConstructionComplete` places the unit) + `GroundForcesDB` on the planet body (the roster of `GroundUnit`s, each a data object stamped with `FactionOwnerID` / `RegionIndex` / attack·defence·HP, deep-cloned + persistent) + `GroundForces.RaiseUnit` (the place primitive). Unit stats live on the `GroundUnit` (snapshot at build, like a ship caches `ShipCombatValueDB`) — no separate value blob needed. New subsystem `GameEngine/GroundCombat/`. Gauges: `GroundForcesTests` (RaiseUnit places in a region · a completed build places on the colony's planet via the real `OnConstructionComplete`+`IndustryJob` · deep-clone persistence). *(Follow-up: a base-mod JSON template so it's player-buildable in a New Game — deferred to keep the six-point registration off the New-Game-crash path for now.)*
- **5b — move it. ✅ built (2026-07-04).** `GroundForces.OrderMove(body, unit, toRegion)` marches a unit to an ADJACENT region, setting its transit clock to that region's `CrossingTimeSeconds` (ground units need no `NewtonMoveDB` — tactical scale); `GroundForcesProcessor` counts it down and lands the unit. v1 is one hop at a time along the ring (multi-hop pathing later). Gauge: move-to-adjacent arrives · move-to-non-adjacent rejected.
- **5c — fight. ✅ built (2026-07-04).** `GroundForcesProcessor` resolves each contested region by strength-math **mirroring `AutoResolve`'s salvo loop** over `GroundUnit`s (each tick = one salvo; every faction takes the combined attack of all others, focus-fired; 0-health units removed). Deterministic, no RNG. Gauge: the stronger (more total attack) garrison wipes the weaker.
- **5d — CAPTURE (the "you can take a planet" MVP moment). ✅ built (2026-07-04).** Two rungs, both in `GroundForcesProcessor`: a cleared region's sole surviving faction takes it (`Region.OwnerFactionID` flip); and when EVERY region of a world is held by one invader, the planet's **colony** flips (`FactionOwnerID`) — same primitive as fleet capture. Gauges: cleared region owned by the victor · all-regions-held flips the colony. *(v1 is the ownership flip; deeper colony transfer + the orbital-bombardment-softens-the-garrison wire come later.)*
- **5e — the tactical MAP (client). ✅ tactical layer built (2026-07-04, CI-compiled; runtime = local build).** The `PlanetViewWindow` upgraded from *readout* to *navigable surface*: **units drawn IN their region** (grouped per faction+type into "I ×3" tokens, cyan = yours / red = hostile, with a health bar and a `»` marching marker), **click-to-move** (click a token to select the group → click an adjacent region *or* a March button → `GroundForces.OrderMove` per unit, engine-validated), **click-to-place a base** (a Build panel places a `PlanetInstallation` `ComponentDesign` at the centre region through the real `PlaceInstallationInRegionOrder` → `Game.OrderHandler.HandleOrder` path), **terrain class** (Open/Cover/Rough via `GroundTerrain.Classify`) and **hazards** (the region's `PlanetEnvironmentsDB` environments as coloured chips) visible, and each `Region.InstallationIds` entry shown as a ⚙ building count. Thin defensive draw (all reads off CI-tested blobs; orders through CI-tested engine paths; body wrapped so a throw logs `[RenderError]` once and still balances `Window.End`; no hard-indexing). This is the developer's "a map I can navigate, not just percentages" *and* the first cut of "what I build in space is a real building on the ground." **Still to finish for the LOCKED principle: give every EXISTING colony installation a home `RegionIndex`** (the reconciliation half — normal-economy buildings still carry no region), and a "Fleet Combat tab"-style ground-combat readout. (CI-blind → local render check.) ← next: 40k depth (see below).
- **5f — terrain-as-leverage.** Region features modify the fight (mountains/highlands favour the defender, open plains the attacker) — so *where* you fight and build matters. Gauge: same forces, different terrain → different outcome.
- **5g — unit TYPES + the ground triangle.** Infantry / armour / artillery with a rock-paper-scissors edge (the ground echo of the space weapon triangle). Gauge: type A beats B beats C on equal strength.
- **5h — base-defends-city coverage + formations.** A military base projects defence over adjacent regions (not just its own); units group into formations that fight/move as one (the ground echo of the fleet). Gauge: a base blunts an attack on the region next door.
- **5i — tactical hex (zoom-in).** The existing `ColonyHexMapDB` battlefield nested inside a region (persistence fixed first) — the "zoom to high accuracy" layer, once the region-level game is real.

Still deferred past full-tactical v1: the to-scale globe renderer, gas-giant platform hosts (reuse the station host), and colony-onto-region migration.

---

## Planet-view evolution — from "readout" to "navigable tactical map" (live-test feedback, 2026-07-04)

First live look at the slice-3 `PlanetViewWindow`: *"it's just regions with topography percentages — it's OK but it's not a map I can navigate units on, plot where I'll make military bases, or use topography to my advantage."* Correct, and expected — slice 3 was the **legible base** (prove the ring reads + the data shows), not the tactical surface. The next evolution turns the readout INTO the map:

- **Spatial, not columnar** — draw each region as an actual area you point at and click *within* (a slice/wedge of the disc, or a 2D panel), not a labelled bar of percentages. Terrain becomes a thing you read positionally.
- **Place things at a spot** — click a region → "build here" (the slice-2 `PlaceInstallationInRegionOrder` already exists; this gives it a *map* front end instead of a menu), so you plot military bases / mines / defenses on the surface.
- **Units live on it and move** — once ground units exist (slice 5), they render *in* their region and you order region→region moves along the crossing-time edges. **This is why the map and the units land together** — a "navigable map" with nothing to navigate is half a feature. So the map's tactical upgrade is co-designed with slice 5, not before it.
- **Topography as leverage** — terrain modifies combat/defence (mountains favour the defender, etc.), so *where* you fight and build matters. That's the `terrain combat modifiers` line above, promoted from "deferred" to "part of what makes the map worth navigating."

Net: slice 3 stays the base; the tactical map is **slice 5's client half** (units + placement + terrain-as-leverage on the same region graph), so the two ship together as "a map you actually play on." The globe/zoom-to-hex fidelity remains a later skin on the same graph.

---

## LOCKED (2026-07-04): TERRAIN is the ground twin of a SPACE HAZARD — dynamic, sci-fi, and contextually generated

**The developer's two-part steer:**
1. *"Terrain mirrors space environments too."* → A region's environment is the ground echo of a `Hazards/SpaceHazardDB`: a bounded area holding **typed effects** that bend what happens inside. **Decision: MIRROR the pattern + share the effect vocabulary, WITHOUT refactoring the (green) hazard engine.** Map: **MovementDrag** ↔ `CrossingTimeSeconds` · **Concealment** ↔ hazard `SensorJam` · **EnvironmentalHazard** ↔ hazard `HeatDamage`/`Corrosive` · **Cover** (ground-specific) = a defender in rough terrain takes less. A unit's TYPE is its innate affinity (the `HazardResistanceAtb` echo; a researched gear component overriding it is the cradle-to-grave follow-up).
2. *"This is a sci-fi game — don't just make Earth biomes."* → The environments are **DYNAMIC and EXOTIC**, and generated **from the planet's PHYSICS**, not copy-pasted Earth or flat RNG. Examples the developer named: *massive lightning storms on the plains that jam radar; continent-spanning corrosive storms that impede movement and damage units; fire tornadoes coating the landscape.* And the engine must be **INTELLIGENT about where each can occur** — *a gas giant can't have fire tornadoes on its surface; it has no surface.*

### Two layers on a region (keep them distinct)
- **Static geography** — the map's *shape*: mountains / plains / ocean / highlands. Drives **Cover** (defence) + **movement** + the terrain×type affinity (armour bad in rough, artillery loves high ground). This is what `GroundTerrain.cs` classifies today (the combat mechanic — generation-agnostic: it consumes whatever a region has).
- **Dynamic environmental HAZARDS** — the sci-fi *weather/menace*: fire-tornado fields, corrosive superstorms, lightning storms, ash/radiation zones. These are the true ground echo of space hazards — they carry **typed effects** (EnvironmentalHazard damage, MovementDrag, Concealment/radar-jam) and (like a solar flare) can be **transient**. **This layer is the NEW work the steer calls for.**

### The intelligence: environments are a FINGERPRINT of the planet's physics (the load-bearing requirement)
Generation must read the planet's already-computed physical scalars and emit only environments that **make sense there** — the ground counterpart of the space system's flagged "read `StarInfoDB`, pick a system profile" (`Hazards/CLAUDE.md` ★). The inputs all exist:

| Physical input (already in the engine) | Gates / produces |
|---|---|
| **Body type** (`SystemBodyInfoDB`) | **Gas/Ice giant → NO SURFACE → no surface hazards** (atmospheric-band environments for floating platforms are a later, separate thing). Terrestrial / moon / dwarf → surface hazards allowed. |
| **Surface temperature** (`AtmosphereDB`, Aurora greenhouse formula) | Scorching (close orbit / runaway greenhouse) → **fire tornadoes, molten zones**; frozen → **ice storms, cryovolcanism**. |
| **Atmospheric composition** (`AtmosphereDB.Composition`) | Corrosive gases (sulphur/chlorine) → **corrosive superstorms**; thick/dense → crushing-pressure zones. |
| **Tectonics** (`SystemBodyInfoDB`) | Active/volcanic → **lava fields, ash storms, quakes**. |
| **Hydrosphere** (`AtmosphereDB.HydrosphereExtent`) | Wet → oceans/monsoons; dry → **dust storms**. |
| **Pressure + rotation / charge** | Stormy worlds → **continent-spanning lightning that jams radar**. |
| **Orbit / radiation** (distance, magnetosphere) | Close-in / unshielded → **radiation zones**. |

So *which* environments a world has is a read-out of *what it is* — "entering a new world is unique and dangerous" (the north-star pillar), the planetary twin of "entering a new system is terrifying." **Same problem, same fix, both sides** — so the contextual generator should be designed to serve space AND ground consistently.

### What this reshapes (build implications)
- **The combat mechanic** (`GroundTerrain.cs`: cover / triangle / type-affinity) stays — it's the generic engine that *consumes* a region's environment, biome or fire-tornado alike. 5f/5g proceed on it.
- **A new slice — "Planetary environments"** owns the sci-fi content + the **contextual, physics-driven, gas-giant-gated generation** (the ground hazard layer, effects shared with space). This is where the steer lives; it's co-designed with fixing the space side's flagged flat-RNG placement so ONE "environment from physical context" approach serves both.
- **The feature vocabulary** (`RegionFeatureType`) grows beyond Earth biomes to carry the exotic set — additively (gotcha #10), and generated by physics, never hardcoded-per-planet.

---

## LOCKED PRINCIPLE (2026-07-04): every buildable is a REAL building on the ground

**The developer's rule:** *"All things I build on the planet that can be selected in space are represented by an actual building on the planet itself."* This is the load-bearing idea that makes the planet-infrastructure system whole — the colony economy and the ground map are **two views of the SAME physical things**, not two parallel bookkeepings.

- **One truth, two views.** A colony's installations (mines, factories, refineries, life-support, a military base…) are `ComponentInstance`s in its `ComponentInstancesDB` — the "selectable in space" abstract list the colony economy UI shows. Under this rule, **each of those also occupies a REGION** and draws as a building on the planet view. Selecting it in space and finding it on the ground are the same object seen two ways.
- **Nothing is abstract-only.** If you can build it and see it in the space/colony view, it has a **place** — a region, and (later, at high zoom) a spot within that region. An installation with no location is a bug against this rule, not a feature.
- **Why it matters (the whole point).** This is what turns "a colony is a bag of numbers" into "a colony is a PLACE you can bombard, invade, defend region-by-region." Orbital bombardment hits *a region's* buildings; a ground invasion takes *the regions the buildings sit in*; losing a region means losing what's built there. The infrastructure only earns its weight once it's located — abstract installations can't be fought over.

**Build state vs. this rule (the reconciliation, a planned slice):**
- **Already true for NEW located builds:** slice 2's `PlaceInstallationInRegionOrder` builds an installation *at a chosen region* and records it in `Region.InstallationIds` — the located axis. That path already honours the rule.
- **NOT yet true for the colony's existing installations:** an installation built the *normal* way (colony economy UI → `ComponentInstancesDB`, no region chosen) has **no region** yet. To satisfy the rule everywhere, every colony installation must get a home region (default: the colony's capital region; player-placeable when built through the map), and the **planet view must render each `Region.InstallationIds` entry as a building**. That's the **"locate + draw the colony's installations"** work — it rides with **slice 5e** (the tactical map: buildings drawn in regions, click-to-place), with the engine half being "give every installation a `RegionIndex`."
- **The reverse link (5d already respects it):** capturing a region should hand its buildings to the new owner; taking every region takes the colony. v1 flips ownership; wiring each building's control to its region's owner is the depth pass.

**Rule for anyone building here:** when you add a new planet buildable (installation, base, defense, ground unit facility), it MUST be reachable as a component (`CONVENTIONS.md` §6) AND carry a region location so it appears on the ground map. "Built in space, invisible on the ground" fails this principle.

---

## Connections (Prime Directive)

- **Galaxy / `StarSystemFactory`** — generation hook (done, 4 paths). **`SystemBodyInfoDB` / `AtmosphereDB`** — the feature-gen inputs (body type, tectonics, hydrosphere).
- **Colonies / `ComponentInstancesDB`** — a colony sits on a planet that now has regions; slice 1 left the colony undisturbed (new axis). **Per the LOCKED principle** (every buildable is a building on the ground), the reconciliation is to give each colony installation a `RegionIndex` and draw it on the planet view (rides slice 5e). Migration of the colony *itself* onto a region is a later pass.
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
