# Ground: the War-Map layer and the City sub-grid (two zooms, one physical thing)

**As of 2026-07-04.** Status: **W-track (War-map layer) BUILT on the engine + CI-gauged; City sub-grid (C-track) follows.** Extends `docs/HEX-GROUND-AND-ORDERS-DESIGN.md` (the operational hex grid we built) and the **LOCKED PRINCIPLE** in `GameEngine/GroundCombat/CLAUDE.md` ("everything I build that's selectable in space is a real building on the ground"). Read those first.

> **W-track build note (2026-07-04):** shipped — `GroundHex.InstallationIds` (the building slot on the operational hex) + `GroundFootprintAtb` (the "occupies-a-tile" component attribute; the base-mod **Bunker** carries it, so a fort is a real capture/bombard target) + `GroundBuildings` (locate-on-hex, capture-captures-contents, bombard-damages-contents), wired into `ColonyFactory` (locate) and `GroundForcesProcessor` (capture). Gauges: `GroundForcesTests.WarMap_*`. **Deferred to follow-ons:** routing the auto orbital-strike (`OnColonyDamage`) at a specific hex's buildings (the `BombardHex` primitive exists + is gauged; the damage-path integration is next), a player-buildable non-Bunker footprint (spaceport/HQ), and the client draw of a hex's strategic presence. Then C-track (the city builder).

---

## The idea in one analogy

Think of the operational map like the **fleet plot** — a ship is one icon. But you can open the damage-control diagram and see the ship's individual **compartments**. Same ship, two zoom levels, one physical thing.

**Buildings work the same way:**

- **Operational hex** (the war map, ~560 km — the hexes we already built) is the *"ship icon."* A hex that holds a base shows its **strategic presence** (a fort, a spaceport, an HQ) and is the unit you **bombard / capture / defend**. This is where the **war** happens.
- **Zoom into that hex → it opens its own fine hex grid** — the *"compartment diagram."* Here every building sits on its **own tile, 1:1**, and this is where the **planning** happens (terrain-matched placement, adjacency, running out of good tiles).

The fine grid does **not** try to fill the whole 560 km — it's just the **developed footprint** of that base (a patch of radius ~5–8, a few hundred fine tiles), generated **lazily** only for a hex that actually has a colony. Same anti-bloat rule as the operational hexes: undeveloped wilderness never gets a fine grid; only the places you build do.

## Locked layer split — buildings live on MINI hexes; units fight on OPERATIONAL hexes (2026-07-04)

The two grids each do ONE job, and the operational hex is a **pure roll-up** of what's built inside it (not a second place you drop buildings):

- **Mini-hex grid = the CONSTRUCTION layer.** *Every* building physically lives here — factory / farm / power plant AND fort / spaceport / HQ (the strategic ones are just a few mini tiles, not a whole hex). Placement, terrain-match, adjacency all happen here. You build by zooming into a developed operational hex's city grid.
- **Operational grid = the WAR layer.** Units move, fight, capture here. Battles stay at this scale (never per-mini-tile). The operational hex **reads the roll-up** of the buildings on its mini tiles (`GroundHex.InstallationIds` = the aggregate) → it knows "this hex contains a spaceport" (strategic marker + capture/bomb/fortify target), without any building being placed on it directly.

**One placement path** (place on a mini tile → it rolls up), **one connection** (the roll-up invariant), no double-bookkeeping. This RETIRES the v1 "place a footprint building directly on the operational hex" path (`W1 LocateFootprintsOnHexes` / `PlaceInstallationInRegionOrder`) in favour of "develop the hex → build on mini tiles → roll up" once C-track lands. Beats the alternatives: no "spaceport fills a hex" absurdity, no simulating combat across a hundred-thousand mini tiles, and it keeps the LOCKED PRINCIPLE (every building is a real, capturable place). Tradeoff: you zoom in to build — but strategic buildings still show at the war zoom via the roll-up.

## Scale reconciliation — "occupies a hex" is about ZOOM, not physical size (2026-07-04)

A spaceport does **NOT** physically fill an operational hex (that's hundreds of km — nothing is). "Occupies the operational hex" was v1 shorthand; the honest model **decouples two separate things**:

- **Physical footprint = mini hexes** (`GroundFootprintAtb.TileFootprint`): a factory = 1 mini hex, a spaceport = a *handful*, a megastructure = many. A spaceport is bigger than a factory — but by a few mini hexes, **not by a whole operational hex**.
- **War-map presence = a strategic FLAG** (carrying `GroundFootprintAtb` at all): is this a *capture/bomb/defend objective* — a reason the hex matters at the war-map zoom? A spaceport / fort / HQ: **yes** (it shows on the operational map as that hex's strategic feature). A factory / farm / power plant: **no** (local economy, only in the city grid; captured as collateral when the hex falls).

So a strategic building **flies a marker on the war-map hex** (roll-up in `GroundHex.InstallationIds`) AND physically sits on a few **mini** tiles in the city grid — like a country map's ✈ icon ("this city has an airport") versus the airport's actual few-blocks footprint when you zoom in. The two are the same building at two zooms; neither is hex-sized. (Engine already has both knobs — `TileFootprint` + the `GroundFootprintAtb` presence — so this is a framing fix, not a rebuild.)

## Why this is load-bearing

The two grids are the **same physical buildings at two zooms**. A power plant you place on a fine city-tile *is* the "power/industry presence" on the operational hex. So:

- In the **city view** you **build and plan** — lay out each building on its tile.
- In the **war view** you **fight** — the operational hex shows the aggregate; **capturing it captures everything inside; bombing it damages what's on it.**

That is the LOCKED PRINCIPLE taken all the way down — and it finally gives the ground war **something worth fighting over**, which is the piece the combat we just built (H3 range/directed fire, capture, fortification) is missing. Right now you can take a region; after this you're taking a region *because of what's built on it*.

## The one honest caution (why we sequence it)

The fine city grid is a **real subsystem** — a new zoom view, a placement UI, and the terrain/adjacency economy rules that make placement matter. It's the **biggest single thing** on the ground track. So the smart sequence is:

1. **War-map layer FIRST (cheap, ~1 slice).** Give strategic buildings a **hex on the operational map** — they become **capture/bombard targets**. `GroundHex` already has `OwnerFactionID`; we just add a **building reference** on the hex + an **"occupies a tile" flag** on the design. This completes the ground-combat loop immediately and is the **foundation** the city view plugs into.
2. **City sub-grid SECOND (the builder).** The nested fine grid + placement + adjacency — the Civ layer, built lazily per base, once the war-map layer proves the connection.

That way you get the **tactical payoff right away**, and the big builder subsystem lands on a foundation that's **already working** — not all at once.

---

## W-track — the War-map layer (build now)

**Goal:** a strategic building occupies an operational hex, so it can be captured and bombed. Engine-first, CI-gauged.

### EXISTS (verified in source, 2026-07-04)
- `Galaxy/GroundHex.cs` — the operational hex. Already has `Q`/`R`/`Terrain`/**`OwnerFactionID`** (−1 = uncontested) + save-safe copy-ctor. **The building slot is the only missing field.**
- `Galaxy/PlanetRegionsDB.cs` → `Region.InstallationIds` — buildings recorded at the **region** level today; `Region.Hexes` is the operational hex list.
- `Galaxy/PlaceInstallationInRegionOrder.cs` — installs a component on the colony (normal economy rail) **and** records the instance id in `Region.InstallationIds`. The "build at a place" front door — one zoom too coarse (region, not hex).
- `GroundCombat/GroundForcesProcessor.cs` — capture: flips `region.OwnerFactionID` when a region is uniformly held; `TryCapturePlanet` flips the colony when all regions are held.
- `GroundCombat/GroundFortification.cs` — reads `Region.InstallationIds` → `GroundDefenseAtb` for the defender multiplier (the fortification wire the located building already feeds).
- `GroundCombat/GroundInstallations.cs` — `LocateColonyInstallations` drops the start colony's buildings into its capital region (the LOCKED-principle reconciliation, region-level).
- Bombard: `DamageProcessor.OnColonyDamage` already does random **installation** damage on a colony hit (the orbital-strike → building-loss path).

### MISSING (this slice adds)
- **`GroundHex.InstallationIds`** (`List<int>`) — the building instance ids that occupy THIS operational hex (the finer-than-region location). Deep-copied in the copy-ctor; `[JsonProperty]` (save-safe from day one, like the rest of `GroundHex`).
- **An "occupies a tile" flag on the design** — a strategic/footprint building declares it has a **presence on the operational map**. Modeled as a component attribute (`CONVENTIONS.md` §6 — abilities are components, so it's researched/built/installed/lost for free): a `GroundFootprintAtb` (a Bunker/Spaceport/HQ carries it; a solar panel doesn't). This is the "which buildings show up as a war-map target" gate.
- **Place-on-hex** — extend the located axis one zoom down: a building with a footprint records its instance id on a **specific `GroundHex.InstallationIds`** (v1: the region's muster/centre hex, since that's the guaranteed-land landing core — see `PlanetHexFactory` muster-core rule), in addition to `Region.InstallationIds`. Keeps the region economy untouched (same reconciliation shape as `GroundInstallations`, one level finer).
- **Capture wiring** — when a region flips owner (or a hex is taken), the footprint buildings on its hexes flip `GroundHex`-side ownership with it: **capturing the hex captures what's on it.**
- **Bombard wiring** — a hit that lands on a hex damages/destroys the footprint buildings recorded there: **bombing the hex damages what's on it.** (Reuses the `OnColonyDamage` installation-damage primitive, targeted by hex.)

### Gauge (the acceptance test — `GroundForcesTests`)
- a footprint building placed on a region's muster hex is recorded in that `GroundHex.InstallationIds` (and still in `Region.InstallationIds` for the economy);
- capturing the region flips the hex-building's ownership to the captor (capture-captures-contents);
- a bomb/strike targeted at the hex removes/damages the footprint building (bomb-damages-contents);
- a non-footprint building (solar panel) is **not** a war-map target.

### Cradle-to-grave (the acceptance walk)
mineral → material → **footprint building** (a Bunker/Spaceport/HQ ComponentDesign carrying `GroundFootprintAtb`) → gated by research → **placed on an operational hex** (`GroundHex.InstallationIds`) → the **decision** (where you fort up / where the enemy must land) → **captured or bombed** (the grave rung — you lose the building with the hex). Every rung reuses an existing rail; the slice only adds the hex-location axis.

---

## C-track — the City sub-grid (W-track is green; building now)

**Goal:** zoom into an operational hex that holds a colony → its own **fine hex grid**, every building on its own tile, with terrain-matched placement + adjacency economy. The "compartment diagram" to the operational hex's "ship icon."

### The locked model

- **Lazy, per-developed-hex.** Only a hex a colony DEVELOPS gets a fine grid (radius ~5–8 → a few hundred tiles). Same anti-bloat rule as operational hexes; generated on first develop/open, idempotent, save-safe — mirrors `PlanetHexFactory.EnsureHexesForBody` **one zoom down**.
- **Where it lives: `GroundHex.CityGrid` (nullable).** The fine grid hangs off the operational hex it details — null until developed (so an undeveloped hex costs nothing), deep-copied by `GroundHex`'s existing clone (save-safe for free, no new blob to register). NOT the old non-persistent `Colonies.ColonyHexMapDB` — that's the city-builder this whole design replaces.
- **1:1 placement.** A `CityTile` holds one building instance id (`BuildingInstanceId`, -1 = empty) + its own fine terrain. A building is a `ComponentInstance` on the colony (the same economy object W-track and the colony screen already use) — the city grid just says *which fine tile it sits on*.
- **The two grids are the SAME buildings — the roll-up invariant.** `GroundHex.InstallationIds` (operational, W-track) == the set of building ids placed on that hex's city tiles. Placing a building on a fine tile ADDS its id to the operational hex's roll-up; removing it (or bombing the operational hex) removes it from both. So **capture/bombard (W-track) act on the operational roll-up; placement/economy (C-track) act on the fine tiles — and they can never disagree.**
- **The builder is the subsystem.** Terrain affinity per fine tile (a farm wants plains, a mine wants highlands), adjacency bonuses (buildings boost neighbours), and the scarcity that makes "running out of good tiles" a real decision. That economy is what makes placement a *decision*, not decoration.

### Slices (engine-first, CI-gauged)

- **C1 — the fine-grid data layer (the substrate).** `CityTile` + `CityGrid` (save-safe data objects) + `GroundHex.CityGrid` (lazy, deep-copied) + `CityGridFactory.EnsureCityForHex(body, region, q, r)` (builds the fine disk, terrain seeded from the operational hex + system RNG, idempotent, defensive — the `PlanetHexFactory` twin one zoom down) + the placement primitive `CityBuilder.PlaceBuildingOnTile` / `RemoveBuildingFromTile` that **keeps the roll-up invariant** (`GroundHex.InstallationIds` stays == the placed set) + `DevelopColonyHex` (generate a colony hex's city grid and lay its existing footprint buildings onto tiles — the migration from W-track's coarse "it's here" to the fine "it's on THIS tile"). Gauge: `CityGridTests` (lazy gen + scaling + clone-safety; place/remove keeps the roll-up in sync; develop lays the colony's buildings; a bombed operational hex clears its city tiles). **← START HERE.**
- **C1b — MORE (not bigger) hexes on bigger planets (developer refinement, 2026-07-04; clarified same day).** **Nothing gets bigger — there are just MORE of them.** A mini hex is a **fixed real footprint** (a factory takes exactly 1 mini hex, everywhere) AND an operational hex is a **fixed real size** everywhere too. A bigger planet therefore has **MORE operational hexes AND MORE mini hexes** (more surface area → more tiles at *both* scales), not bigger tiles. So the per-operational-hex mini count is ~**constant** across planets (≈ the fixed operational-hex area ÷ the fixed mini-hex footprint); Earth just has far more operational hexes overall than Mars. (The developer's "Earth hex = 7 mini, Mars hex = 3" was loosely gesturing at *more total* on Earth, not a per-hex ratio — corrected: **don't make hexes bigger, make there be more**.) This already matches the G-track grid, which scales hex COUNT with surface area (per-hex area ≈ constant). So C1's only job here is to replace the fixed `CityPatchRadius=6` with the fixed **operational-hex-area ÷ mini-hex-footprint** count (a constant, given fixed sizes), and let the *number of operational hexes* carry the planet-size scaling (G-track already does). **Open number (confirm before hardcoding, per the developer's rule):** the real footprint of one mini hex in km² (a factory's footprint) and the real area of one operational hex — the mini-per-operational count falls out of the two. Locked intent: infrastructure builds on BOTH scales (strategic building = a whole operational hex, W-track; fine building = 1 mini hex, C-track); battles stay at operational-hex scale.
- **C2 — the placement economy.** Terrain affinity (a tile's suitability for a building type) + adjacency bonuses + the scarcity of good tiles. The rules that make *where* you place matter — the decision layer.
- **C3 — the client zoom view.** `PlanetViewWindow`: zoom from the operational hex into its city grid, drag-place buildings on tiles, see terrain/adjacency feedback. The Civ layer made visible. (CI compiles it; feel is the developer's local build.)

### Cradle-to-grave (C-track)

mineral → material → **building** (a `ComponentInstance` on the colony) → **placed on a fine city-tile** (`CityTile.BuildingInstanceId`, gated by terrain affinity + adjacency — C2) → rolls up to the operational hex's footprint (`GroundHex.InstallationIds`) → the **decision** (lay out your base to win the adjacency economy AND survive a strike) → **bombed/captured** on the operational hex → the fine tile is cleared too (roll-up invariant). One physical building, two zooms, one grave.

---

## Prime-Directive connections

- **Galaxy / `GroundHex`** — gains the building slot; `PlanetHexFactory` (muster-core land rule) guarantees the landing hex is buildable.
- **GroundCombat** — capture (`GroundForcesProcessor`) and fortification (`GroundFortification`) read the hex-located buildings; the resolver already fights over regions, now with contents worth taking.
- **Damage** — `OnColonyDamage` installation-damage becomes hex-targetable (bombard-damages-contents).
- **Industry / Components** — the footprint is a `*Atb` component attribute → research/build/install/lose for free (`CONVENTIONS.md` §6).
- **Client / `PlanetViewWindow`** — W-track: a hex with a footprint building draws its strategic presence + is a capture/bombard target; C-track: the zoom-in city grid (the new view).

**Do NOT** build the city grid before the war-map layer — the tactical payoff and the foundation come from W-track; C-track is the big builder that plugs into it.
