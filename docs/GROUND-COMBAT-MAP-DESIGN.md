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

- **5a — raise a unit.** `GroundUnitDesign : IConstructableDesign` (built at a colony from materials, tech-gated) + `GroundForcesDB` (holds units per region, each with a `FactionOwnerID`) + a `GroundCombatValueDB` mirroring `ShipCombatValueDB` (attack/defence/HP). Gauge: build a unit → it appears in a region.
- **5b — move it.** A ground move order region→region along the existing `CrossingTimeSeconds` edges (ground units need no `NewtonMoveDB` — tactical scale). Gauge: unit crosses on the travel-time.
- **5c — fight.** `GroundCombatProcessor` resolves two opposing forces in a region by strength-math (mirror `AutoResolve`'s salvo loop); losers' units die. Gauge: stronger force wins, casualties taken.
- **5d — CAPTURE (the "you can take a planet" MVP moment).** Clear a region's/colony's garrison → flip `FactionOwnerID` (same primitive as fleet capture). Gauge: cleared garrison → ownership flips. Ties into the live colony-damage path (orbital bombardment softens a garrison).
- **5e — the tactical MAP (client).** The `PlanetViewWindow` upgrade from *readout* to *navigable surface*: units drawn IN their region, **click-to-move**, **click-to-place a base**. This is the developer's "a map I can navigate, not just percentages." (CI-blind → local check.)
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
