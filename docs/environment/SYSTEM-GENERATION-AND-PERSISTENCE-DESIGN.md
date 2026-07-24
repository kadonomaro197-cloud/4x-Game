# Star-System Generation & Persistence — the LOCKED design (2026-07-24)

**Status:** 🔒 design-locked (the developer's five rulings, 2026-07-24). **Not built.** This is the *source* of the
diversity the surface map displays — `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` Layer 6 is the window, this is the
world behind it. Companions: `docs/environment/ENVIRONMENTS-DESIGN.md` (hazards/terrain as one physics-driven system),
`GameEngine/Galaxy/CLAUDE.md` (as-built generation).

---

## The point, before the plumbing

Right now a real New Game runs on **authored** star systems — Sol exists because a human typed it into JSON, body by
body. The procedural generator exists and works, but a system it makes is thinner than Sol and, once made, exists only
in memory and the save file.

The developer's requirement fixes both halves at once:

> When the generator creates a system — say **Barnard's Star** — for the **FIRST** time, it generates **everything**:
> from the *grassland with a resource node in it* on an inner planet, out to the *asteroids in the star's Oort cloud*.
> **After that first generation, a file is written saving the specifications of that system**, and thereafter the system
> comes from the file.

**Why this matters beyond flavour:** it turns a generated system from a dice roll into a **place with a birth
certificate**. It can be inspected, hand-edited, shared, and — critically — it stops changing under the player's feet.

**The analogy:** it's the difference between a course you re-plot from scratch every watch and a **chart you drew once,
signed, and filed.** Same waters, but now everyone reads the same chart, and the chart doesn't quietly redraw itself
because someone recalibrated the plotting table.

---

## The five LOCKED decisions (developer, 2026-07-24)

| # | Question | **Ruling** |
|---|---|---|
| 1 | What goes in the file — the recipe (seed + parameters) or frozen detail? | **HYBRID.** Seed/recipe for everything untouched; **frozen detail for anything the player has observed.** |
| 2 | When is it written? | **The first time** — first generation of that system. |
| 3 | Per-save, or a shared library across all games? | **It depends on the initial galaxy setup the player chooses when creating their game.** → a **new-game option**, not a hardcoded rule. |
| 4 | Hand-editable / moddable? | **YES.** |
| 5 | How deep on first generation — every surface committed, or on demand? | **Materialize on demand.** |

---

## Why the HYBRID is the load-bearing decision (it solves a real problem, not just file size)

**Store the recipe, not the cake.** Terrain is *deterministic* — it falls out of a seed plus a body's physical numbers.
So a file holding the seed + the physical truth regenerates *the identical grassland hex with the identical resource
node in it*, every time, for free. Earth alone would be ~590 million hexes at 1 km; the recipe is a few hundred lines.
That is what makes decision #5 (materialize on demand) safe — and it's the same lazy rule the whole surface map already
runs on.

**But a pure recipe has a nasty failure mode:** if terrain is *derived*, then **improving the terrain generator silently
rewrites the terrain of every system anyone ever visited.** The hill you named, fortified, and bled over becomes a swamp
after a patch. That's the bug the hybrid kills:

- **Untouched space stays a recipe.** 95% of the galaxy nobody has visited costs almost nothing, and *benefits* from a
  better generator later — a patch legitimately improves the places no one has seen.
- **Observed things get frozen into the file.** The moment a system is surveyed / landed on / built on / named, its
  detail is written down as fact. **What became yours stops being a dice roll.**

So the same mechanism gives you a cheap galaxy *and* a stable history. **Freezing is triggered by observation** — the
same "you know the ground where you settle" principle the survey-fog model already uses.

---

## EXISTS / MISSING / NEEDS-CHANGE (verified against source, 2026-07-24)

| Piece | State | Where |
|---|---|---|
| Procedural generator, **deterministic by seed** | ✅ EXISTS | `StarSystemFactory.CreateSystem(game, name, seed)` :41 — stars, bodies, star coronas/flares, minerals. `SystemGenTests` proves same seed → twin systems |
| **The file format** + the LIVE read path | ✅ EXISTS, proven | `SystemBlueprint` (clean data class: bodies, `AsteroidBeltValue`, `SurveyRingValue`, `HazardValue`…) → `LoadFromBlueprint` :544. **This is what a real New Game runs** (`NewGameMenu.cs:599`); Sol is authored JSON at `GameData/basemod/ScenarioFiles/systems/sol/` (one file per body) |
| A second folder-of-JSON reader | ✅ EXISTS | `LoadSystemFromJson` :402 (`systemInfo.json` + per-star/per-body files), used by `DefaultStartFactory` |
| Per-body terrain from physics | ✅ EXISTS, **lazy** | `WorldTerrain` / `PlanetHexFactory` / `PlanetGridFactory`; real authored maps for Earth/Mars/Luna. Generated only for worlds settled or surveyed |
| Located mineral deposits on hexes | ✅ EXISTS | `MineralDepositFactory` (+ the 2026-07-23 body-type fallback) |
| **The WRITER** — serialize a generated system to a spec file | ❌ **MISSING ENTIRELY** | no `File.WriteAllText` / `SerializeObject` anywhere in `GameEngine/Galaxy/` |
| **Belts / comets / Oort cloud in PROCEDURAL systems** | ❌ **MISSING** | `GenerateAsteroidBelt` :481 is called from **exactly one site** — inside `LoadFromBlueprint` :573 (the *authored* path). A procedurally generated system gets **no belts at all** |
| Terrain shaped by **stellar activity + orbital position** | ❌ MISSING | terrain reads the body's own scalars (hydrosphere, tectonics, temperature); the star's activity and the body's distance don't yet shape topology |
| "Observed" flag to drive freezing | ⚠️ PARTIAL | survey state exists (`GeoSurveyableDB.GeoSurveyStatus`, `PlanetRegionsDB.PerFactionRevealed`) — needs a system-level "this has been observed, freeze it" read |
| Galaxy-setup options at new game | ⚠️ NEEDS-CHANGE | `systemGenSettings.json` + `NewGameMenu` exist; decision #3 adds a per-save-vs-shared-library choice |

---

## Build sequence (CI-gated, gauge first)

**The Visibility Gate says build the gauge before the feature** — and here the gauge *is* the keystone: a
**round-trip**. Generate a system → write it → load it back → assert it is the same system. Until that holds, nothing
downstream can be trusted, and it is exactly the read-back that proves the file is honest.

- **G1 — the WRITER + the round-trip gauge.** Serialize a generated `StarSystem` into the **existing** `SystemBlueprint`
  JSON shape (the format Sol already uses, so decision #4 — hand-editable — falls out for free). Gauge: generate with
  seed N → write → `LoadFromBlueprint` → assert body count, orbits, masses, minerals identical. **Additive: nothing
  reads the file yet.**
- **G2 — procedural systems get their full furniture.** Belts, comets, and the Oort cloud in `CreateSystem`, closing the
  gap that `GenerateAsteroidBelt` is only reachable from the authored path. Covered by G1's round-trip. *(Careful: use
  RNG-free/dedicated-stream generation so galaxy-gen determinism isn't perturbed — the `RuinsDB` lesson,
  `Galaxy/CLAUDE.md`.)*
- **G3 — write on first generation + read thereafter.** Decision #2. The generate path checks for a spec file first.
- **G4 — the hybrid freeze.** Decision #1: on observation, write the observed detail into the file; everything else stays
  a recipe. Gauge: observe a body → patch the generator → assert the observed body is unchanged and an unobserved one
  legitimately re-rolls.
- **G5 — the galaxy-setup choice.** Decision #3: per-save vs shared library, chosen at game creation.
- **G6 — diversity from stellar/orbital physics.** The actual *"MUST change the topology"* requirement — star activity
  and orbital position shape terrain. Feeds the surface map's **M4** (per-mini-tile terrain), which is the display half.

---

## Connections (Prime Directive)

- **`Galaxy/StarSystemFactory` + `SystemBlueprint` + `ModDataStore`** — the generator and the format. The writer emits
  the same shape the mod loader consumes, so a written system is indistinguishable from an authored one (that's the
  point, and it's what makes it moddable).
- **Surface map** (`docs/ground/GROUND-SURFACE-MAP-DESIGN.md`) — the consumer. Layer 6 displays terrain type; **M4**
  needs the per-tile variation this generates. *A rich terrain display over a uniform generator is a lie* — the display
  work is gated on G6.
- **Environments/hazards** (`docs/environment/ENVIRONMENTS-DESIGN.md`) — hazards are already part of `SystemBlueprint`
  (`HazardValue`), so they ride the same file. "Which environments a place has is a fingerprint of its physics" is the
  same rule as G6, one zoom up.
- **Survey / fog** (`GeoSurveyableDB`, `PlanetRegionsDB.PerFactionRevealed`) — supplies the *observation* signal that
  triggers freezing (G4).
- **Save/load** — a spec file is **not** a save file. The save holds what the factions did; the spec file holds what the
  universe *is*. Decision #3 governs whether the two travel together.
- **Minerals** (`MineralDepositFactory`) — a located deposit is part of "the grassland with a resource node in it," so it
  must survive the round trip.

## Cradle-to-grave

Generation is upstream of the whole chain: it puts **the mineral in the ground** on a **specific hex** of a **specific
world**. Everything downstream — mine it, refine it, build with it, fight over it, lose it — already exists. This design
adds no new capability; it makes the *starting conditions* of the chain deep, diverse, and permanent.

## Landmines

- **Don't perturb galaxy-gen determinism.** Adding generation that draws from the shared `StarSystem.RNG` silently
  shifts every downstream body. Use a dedicated/derived stream (the `RuinsDB` + mineral-fallback precedent).
- **Don't write hexes to the file.** Surfaces materialize on demand (decision #5). The file holds the recipe plus frozen
  *observed* detail — never the raw grid.
- **A spec file is data, and data drift crashes players, not `dotnet test`** (root gotcha #10). Any id the writer emits
  must be defined at the other end; `BaseModIntegrityTests` is the sensor.
- **Two readers exist** (`LoadFromBlueprint`, `LoadSystemFromJson`). Write to the **blueprint** shape — that's the live
  New-Game path — and don't let the two formats silently diverge.
