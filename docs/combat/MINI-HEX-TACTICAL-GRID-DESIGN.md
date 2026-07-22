# Mini-Hex Tactical Grid — the LOCKED position model (2026-07-22)

**Status:** 🔒 design-locked (the developer's call, 2026-07-22). The "where exactly is my unit" half of
`docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md`. This doc pins **the grid units actually stand and fight on**;
the sibling doc pins **the real-distance rules** they fight by. Read them together.

---

## The point, before the plumbing

A planet is too big to plot units on at one zoom. Earth's strategic hexes are ~477 km across — you can't
say "my marine is *here*" to within 477 km and expect a real fight. So we use **two zooms of the same
map**, and it's the exact same trick the game already uses for buildings:

- **The coarse hex (the "what's here" view)** — the big regional/planetary hex (`GroundHex` on the
  global `SurfaceGrid`). It tells you *which neighborhood* a force is in: "there's a UEF battalion and a
  hostile battalion somewhere in this ~477 km cell." It's the strategic chart.
- **The mini-hex (the "where exactly" view)** — zoom into that coarse hex and you get its
  **`CityGrid`** — the *same fine tile grid the infrastructure/city view already uses*
  (`GameEngine/Galaxy/CityGrid.cs`, ~127 `CityTile`s at `CityPatchRadius = 6`). Each mini-tile is
  ~37 km across on Earth (tunable — the developer's "10 to 50 km, I don't care, it's just more exact").
  **Units plot on mini-hexes.** That's where "my marine is *here*, the zerglings are 3 mini-hexes away"
  becomes a real, sub-continental position.

**The developer's rule, verbatim:** *"the regional hexes show all of what's in that hex and the mini
hexes show where the things are. just use the same mini hexes that the infrastructure uses."*

Why this is the right call and not a rebuild: the mini-hex grid **already exists, and it's already the
right shape.** `CityGrid` hangs off `GroundHex.CityGrid` (`GroundHex.cs`), is generated **lazily** (only
where something develops — `CityGridFactory.EnsureCityForGlobalHex`), is **save-safe** (`[JsonProperty]`
+ deep-copied), and every `CityTile` **already carries its own `Terrain`** (`CityTile.cs:22`). So plotting
units on it, giving each mini-tile its own geology/hazard, and fighting on real mini-hex distances is
mostly *wiring what's there*, not building something new.

---

## Why this beats a uniform fine grid (the feasibility wall)

You cannot give a whole planet 1-km hexes: Earth would need ~590 million of them (5.1×10⁸ km² ÷
~0.87 km²/hex). That can't be generated, saved, or drawn. The two-zoom model sidesteps the wall
completely, because **mini-hex grids are materialized only where units or bases actually are** — exactly
how `CityGrid` is lazy today. A hundred active battle sites cost a hundred ~127-tile grids, not a
590-million-tile planet. You get 1–50 km precision *where it matters* and pay nothing for the wilderness.

| Grid | What it is | Size on Earth | Cost |
|---|---|---|---|
| Coarse (`GroundHex`/`SurfaceGrid`) | strategic "what's here" | ~477 km/hex, ~2,600 hexes | always present, cheap |
| Mini (`CityGrid`/`CityTile`) | tactical "where exactly" | ~37 km/tile, ~127 tiles per developed/occupied coarse hex | **lazy** — only where units/bases are |

Want finer mini-hexes? Two dials, both cheap and local: raise `CityPatchRadius` (more tiles per coarse
hex) or make the coarse `SurfaceGrid` finer (smaller coarse hexes → smaller mini-hexes). Neither touches
the feasibility wall because the fine grid stays local.

---

## The one hard part — continuity across coarse-hex edges ("transitional hexes")

The developer flagged the real subtlety: *"we will need transitional hexes between the normal ones so
that if 2 entities are at the edges of their respective hexes they will actually engage and see each
other."*

This is a genuine geometry problem. A hexagon can't be tiled perfectly by smaller hexagons, so the
mini-grid under coarse-hex A and the mini-grid under its neighbor B don't line up cleanly at the shared
border. If we naively checked "same coarse hex → can fight," two units standing a mile apart across a
coarse-hex boundary would be blind to each other. That's the wall the developer wants gone.

**The fix — measure on a continuous REAL position, not on hex membership.** Every unit carries a
**continuous real position on the planet**, built from two parts:

> `real position = (its coarse hex's real center) + (its mini-hex's real offset within that hex)`

Both parts are in real kilometres. The distance between two units is then just the **real metric distance
between their two positions** — and that distance is small when they're near a shared edge *no matter
which coarse hex each is filed under*. The "transitional" behavior falls out for free: there are no walls,
because the coarse hex stopped being a wall and became just a coarse *label* over a continuous field.

This is the same move space combat already makes — a fleet has a real position and `Separation_m` is the
real gap; nobody asks "are you in the same sensor cell." We're giving ground units the same continuous
real position, sourced from (coarse hex + mini-hex) instead of an orbit. If we ever want *visible*
bridging tiles on the map, that's a rendering nicety on top; the engine doesn't need them once distance
is continuous.

---

## How a fight works under this model (ties to REAL-DISTANCE-COMBAT-DESIGN.md)

1. **Strategic:** forces march on the coarse grid (the existing global cylinder move). Detection uses
   real radar km — you see a force when it's within your real sensor reach, computed on the continuous
   position (so you can see a contact in the *next* coarse hex if it's physically close).
2. **Contact:** two hostile forces come within real weapon/sensor range on the continuous mini-hex field
   → they're in contact. On today's coarse grid that reads as "same coarse hex" (the developer's default:
   *"if units are in the same hex, they go into combat"*); with mini-hexes it's the real sub-hex gap.
3. **Tactical:** the fight resolves on the mini-hex field — each weapon fires when the **real** gap falls
   inside its **real** range (`GroundUnit.Range_m`, landed in Slice 1), units close at real speed
   (`Speed_kmh`), and each mini-tile's **own terrain/hazard** (`CityTile.Terrain`) shapes the ground —
   cover, rough going, a hazard the crosser bleeds in. This is the real closing fight from
   `REAL-DISTANCE-COMBAT-DESIGN.md` §3, now with a real board to play it on.

---

## Slice plan (CI-gated, additive-first — building on the landed Slice 1)

The rule holds: combat is the only green combat code, CI is the only gauge, one slice at a time, additive
and byte-identical until a deliberate flip.

- **M1 — the mini-hex POSITION field (additive, byte-identical). ✅ BUILT 2026-07-22.** `GroundUnit.MiniQ`/`MiniR`
  (beside `GlobalQ/GlobalR`, `[JsonProperty]` + deep-copied, default (0,0) = centre = muster) + the pure
  **`GroundMiniHex`** helper (`MiniPitchKm` = coarse/(2r+1); `ContinuousPosKm` folds the coarse global hex + the
  mini-hex offset into ONE flat real position; `RealGapMetres` measures across coarse-hex boundaries). Unread by the
  resolver → live combat unchanged. Gauge `Pulsar4X.Tests/GroundMiniHexTests.cs`: two units in *adjacent* coarse hexes
  near the shared edge read a ~1-mini-hex gap (the transitional continuity), two in the same coarse hex read their
  mini-hex gap, mini-pitch ≈ 37 km on Earth. (Mirrors exactly how Slice 1b landed — a field + a helper + a gauge.)
- **M2 — the resolver gates on the real gap. ✅ BUILT 2026-07-22 (behind `EnableMiniHexCombat`).** Both range gates in
  `ResolveRegionCombat` now route through one `WeaponReaches(u, t, rangeHexes, range_m, forces)` predicate: flag OFF
  (the CI test harness default) = the legacy local-patch hex gate `HexDist ≤ rangeHexes` → **byte-identical** (every
  existing ClosingFight/RangeCombat/ROE gauge is calibrated on it, so they stay green untouched); flag ON = the real
  metre gap `GroundMiniHex.RealGapMetres(u,t,body) ≤ u.Range_m`. The flag is **flipped ON for menu games** in
  `NewGameMenu` (both start paths), so a real game gets real distances on-by-default — the developer's "keep the real
  gate on"; the flag exists only to keep the hex-calibrated CI gauges valid, not to hide the feature from players. On
  today's coarse grid this reads as "same coarse global hex → fight, a real distance away → hold fire until you close."
  Gauge: `GroundForcesTests.MiniHexCombat_SameCoarseHexFights_DifferentCoarseHexHoldsFire`.
- **M3 — mini-hex movement + the real closing fight.** Units march mini-hex to mini-hex on the continuous
  field at real `Speed_kmh`; the closing loop (mirror of space `AdvanceClosing`) plays out over real
  metres and seconds; per-mini-tile terrain/hazard attrits the crosser.
- **M4 — per-mini-hex geology/environment.** Fill each `CityTile.Terrain` (and later a hazard field) with
  real variation instead of copying the coarse hex's single terrain (`CityGridFactory.BuildGrid` v1 sets
  them all equal — the field is already there to fill). This is the developer's "opportunity for more
  geological and environmental variations across a planet," and it feeds the closing fight and cover math.
- **M5 — client:** draw units on mini-hexes in the zoom view (the city zoom already renders `CityTile`s);
  the coarse view shows the *aggregate* ("what's here") per the developer's split. Compile-checked; the
  look is the developer's local build.

---

## Connections (Prime Directive)

- **`Galaxy/CityGrid` + `CityTile` + `GroundHex`** — the mini-hex host, already lazy + save-safe + with a
  per-tile `Terrain`. This model *consumes* it as the unit-position grid; the roll-up invariant
  (tile buildings == `GroundHex.InstallationIds`) is untouched (units aren't buildings).
- **`GroundForcesProcessor` / `GroundForcesDB`** — units gain a mini-hex position beside the existing
  `HexQ/HexR` + `GlobalQ/GlobalR`; the resolver's distance read moves from hex-count to the continuous
  real gap (M2). L9 holds — all changes stay inside the one ground hotloop.
- **`GroundRangeTools`** — the km↔hex + real-position math lives here (Slice 1 added the translation;
  M1 adds the continuous-position + real-gap helpers).
- **Movement (`HexPathfinder`, `GroundMobility`)** — strategic march stays on the coarse grid; tactical
  micro moves on the mini-hex field at real speed (M3). Pathfinding cost stays bounded (mini-grids are
  local, ~127 tiles).
- **Detection (`GroundSensors`)** — already real-km based; it reads the continuous position so a contact
  in an adjacent coarse hex is seen when physically close (the "see each other across the edge" half of
  the transitional requirement).
- **Save/load + client** — mini-grids already serialize (they're on `GroundHex`); the client already
  renders `CityTile`s in the city zoom. Cost stays bounded because the fine grid is lazy.

## Cradle-to-grave

Nothing here parachutes in an abstraction. A unit is still built from components and raised; it now simply
has an exact place to stand. Its weapon's real range (a designed/built/losable component) decides the
fight on a real board. A destroyed radar still blinds it; a destroyed weapon still stops its reach — the
grave rungs are unchanged. The mini-hex grid is a *ruler*, not a capability.
