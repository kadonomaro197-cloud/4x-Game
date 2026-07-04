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

## C-track — the City sub-grid (build after W-track is green)

**Goal:** zoom into an operational hex that holds a colony → its own **fine hex grid**, every building on its own tile, with terrain-matched placement + adjacency economy.

- **Lazy, per-developed-hex.** Only a hex with a colony gets a fine grid (radius ~5–8, a few hundred tiles). Same anti-bloat rule as operational hexes; generated on first open, idempotent, save-safe — mirrors `PlanetHexFactory.EnsureHexesForBody` one zoom down.
- **The two grids are the same buildings.** A building placed on a fine city-tile IS the footprint on the operational hex (W-track's `GroundHex.InstallationIds` is the roll-up). Placement/economy live in the fine grid; capture/bombard read the operational roll-up.
- **The builder is the subsystem.** A placement UI (the Civ layer), terrain affinity per fine tile, adjacency bonuses, and the scarcity that makes "running out of good tiles" a real decision. This is the big piece — deferred until the war-map connection is proven.

*(Detailed C-track model — fine-grid generation, placement rules, adjacency economy, the client zoom view — to be written when W-track lands.)*

---

## Prime-Directive connections

- **Galaxy / `GroundHex`** — gains the building slot; `PlanetHexFactory` (muster-core land rule) guarantees the landing hex is buildable.
- **GroundCombat** — capture (`GroundForcesProcessor`) and fortification (`GroundFortification`) read the hex-located buildings; the resolver already fights over regions, now with contents worth taking.
- **Damage** — `OnColonyDamage` installation-damage becomes hex-targetable (bombard-damages-contents).
- **Industry / Components** — the footprint is a `*Atb` component attribute → research/build/install/lose for free (`CONVENTIONS.md` §6).
- **Client / `PlanetViewWindow`** — W-track: a hex with a footprint building draws its strategic presence + is a capture/bombard target; C-track: the zoom-in city grid (the new view).

**Do NOT** build the city grid before the war-map layer — the tactical payoff and the foundation come from W-track; C-track is the big builder that plugs into it.
