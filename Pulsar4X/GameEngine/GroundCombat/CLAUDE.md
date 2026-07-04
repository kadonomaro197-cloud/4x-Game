# Ground Combat — Subsystem Reference

The planet-surface war layer: units you build, station in regions, move, and fight with — the ground echo of the space fleet/combat systems. Lives in `GameEngine/GroundCombat/`. **New 2026-07-04 (slice 5a).**

> **Read `docs/GROUND-COMBAT-MAP-DESIGN.md` first** — the locked design + the sub-slice roadmap (5a–5i). v1 target is **full tactical** (unit types + terrain + base-coverage + formations + a navigable click-to-place map), shipped as CI-gated sub-slices. This subsystem sits ON the region map (`Galaxy/PlanetRegionsDB` — the 4-slice ring per body).

---

## The model in one line

A planet body carries a **`GroundForcesDB`** (a roster of `GroundUnit`s). Each `GroundUnit` knows its **region**, its **owner faction**, and its **combat stats** — so one roster holds both sides of a contested world. Units are **built** at a colony through the normal industry rails (`GroundUnitDesign : IConstructableDesign`), **moved** region→region on the region graph's crossing-time edges (5b), **fought** by a strength-math resolver mirroring the space `AutoResolve` (5c), and **captured** by flipping `FactionOwnerID` (5d) — the same primitive as a ship/fleet.

## File Map

| File | Role | Status |
|------|------|--------|
| `GroundForcesDB.cs` | `enum GroundUnitType` (Infantry/Armor/Artillery), the `GroundUnit` **data object** (DesignId · Name · FactionOwnerID · RegionIndex · UnitType · Attack · Defense · MaxHealth · Health — a build-time snapshot, like a ship's cached `ShipCombatValueDB`), the **`GroundForcesDB`** blob on the planet body (`List<GroundUnit>`, deep-cloned + `[JsonProperty]` = save-safe from day one), and **`GroundForces.RaiseUnit(body, design, factionId, regionIndex)`** — the "place a unit on the surface" primitive (creates the roster on demand). | ✅ 5a |
| `GroundUnitDesign.cs` | `GroundUnitDesign : IConstructableDesign` — a **buildable** ground unit. Rides the existing industry rails for free (research→queue→consume materials→complete). Its `OnConstructionComplete` mirrors `ComponentDesign`'s batch bookkeeping but PLACES a unit on the building colony's planet (via `RaiseUnit`) instead of installing a component. Defensive (a colony with no planet / no region layer / missing production line skips that part; never throws in the daily industry hotloop). | ✅ 5a |

## Why units are DATA objects, not entities (v1)

A `GroundUnit` is a plain serializable object inside `GroundForcesDB`, not a full `Entity` — like `Galaxy.RegionFeature` inside `PlanetRegionsDB`. So a garrison of many units stays cheap and save-safe, and the combat resolver (5c) will read stats directly off the object. The trade-off: it can't reuse the Entity-based `AutoResolve.Resolve` verbatim — 5c **mirrors** that salvo-loop math over `GroundUnit`s instead. (If per-unit entities are ever needed — commanders, per-unit orders — that's a v2 promotion.)

## Connections (Prime Directive)

- **Galaxy / `PlanetRegionsDB`** — the surface `GroundForcesDB` sits on (regions = where units stand/move). A unit's `RegionIndex` indexes into `Regions`.
- **Industry / `IConstructableDesign`** — `GroundUnitDesign` builds on the existing rails (`IndustryTools.ConstructStuff` → `OnConstructionComplete`). A base-mod JSON template (six-point registration, gotcha #10) is the follow-up that makes it player-buildable in a New Game.
- **Colonies / `ColonyInfoDB.PlanetEntity`** — the build hook resolves colony → its planet → the roster.
- **Combat / `AutoResolve`** — the strength-math **shape** slice 5c mirrors for ground battles.
- **Capture** — clearing a region's garrison flips `FactionOwnerID` (5d), the "take a planet" moment; reuses the fleet-capture primitive and ties into the live colony-damage path (orbital bombardment softens a garrison).
- **Client / `PlanetViewWindow`** — 5e draws units in their region + click-to-move/place (the map's upgrade from readout to navigable surface).

## Gotchas

1. **`GroundForcesDB` is on the PLANET BODY, not the colony** — parallel to `PlanetRegionsDB`. Forces are *of the planet* so an unowned world can hold a defending garrison. Don't attach it to the colony entity.
2. **Unit stats are a build-time SNAPSHOT on the `GroundUnit`** (not looked up from the design each time) — the same choice `ShipCombatValueDB` makes. Changing a design later does not retroactively change units already raised.
3. **`OnConstructionComplete` must never throw** — it runs in the daily industry hotloop (a throw there crashes the sim, GameEngine gotcha #1). It's guarded on colony/planet/region-layer/production-line presence.
4. **v1 units are data objects, not entities** — so `Entity.Destroy()` / the Entity-based `AutoResolve` don't apply; the 5c resolver operates on `GroundUnit`s. Don't reach for the ship resolver directly.
