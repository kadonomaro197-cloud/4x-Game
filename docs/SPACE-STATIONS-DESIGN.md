# Space Stations — the universal off-world infrastructure (design capture)

**Status: vision capture, recorded 2026-06-28 at the developer's request. NOT a build spec yet** — this is the *unifying frame* for the space economy + infrastructure: "space eco goes with infrastructure, get it all in one." Read it before scheduling any space-economy work, then turn the open questions into locked decisions. Companion to `docs/COLONY-PROGRESSION-DESIGN.md` (the growth ladder), the colony **morale/population** survey, and `docs/NORTH-STAR-VISION.md` (the BSG nomadic pillar).

---

## The one principle

**A space station is the universal CONTAINER for off-world infrastructure.** Anything you can build on a planet, you can essentially build on a station; the **debuffs you take for doing it in space depend on your racial traits**. Most space-economy gameplay hangs off the station — **but it must not HINGE on it**: the underlying systems (research yield, mining, refining, commerce, population/morale) are *general*, so they work on any host and nothing is blocked waiting for a finished station framework.

The developer's framing, verbatim intent: *"most of space gameplay infrastructure is based off the space station — but [it] shouldn't hinder/hinge on it."*

---

## The big insight (why this is mostly CONNECT, not rebuild)

Pulsar's infrastructure is **already component-based** (`CONVENTIONS.md` §6 — abilities are components). A "place" is just an entity with installed component DataBlobs that do economic work. A **colony** is that, *hard-tied to a planet* (`ColonyInfoDB.PlanetEntity`). A **station** is the *same thing with the planet coupling removed* — a colony-class host that sits in **orbit of a body, a point in a belt, or next to an anomaly**.

So **"build anything on a station" falls out for free** the moment the host is generalized from planet → any-location: the components (research/mining/refining/cargo/population) don't care what they're bolted to.

**What already exists (the Prime-Directive map — the good news):**
| Station capability | Already in the engine as | What's missing |
|---|---|---|
| **Research points by flavor** | `ResearchPointsAtbDB` (component attr) — has `pointsPerEconTick`, `costPerDay`, **`bonusCategory`** | a host in space + "what you study sets the category" |
| **Mining (in range)** | `MiningDB` (+ `MineResourcesAtbDB`) | mine a body/belt from orbit, with an improvable range |
| **Refining on-site** | `IndustryAbilityDB` (refining lines) | run it on a station instead of a planet |
| **Ore depot / storage** | `CargoStorageDB` | a station as a dump/transfer point (logistics already routes cargo) |
| **Population / morale** | `ColonyInfoDB` + life support + the morale loop (survey) | runs on any MANNED place — station or colony |
| **Commerce** | logistics routes + (a future trade/wealth layer) | a station as a trade hub |
| **The host coupling to break** | `ColonyInfoDB.PlanetEntity`, `ColonyFactory.CreateColony(…planet…)` | generalize "place" from planet-bound to any location |

**The single load-bearing change** is generalizing the colony/"place" abstraction off `PlanetEntity`. Everything else is mostly loadout (which components a station carries) + the growth ladder + operating cost + racial debuffs.

---

## The flavors — what a station can BE (like colony flavors)

1. **Research station** — orbit a target you *won't* colonize (a planet with hostile life, a black hole, a special asteroid). **Always manned, full cradle-to-grave.** Yields research points of the flavor **logical to what it studies**, buffing that research category:
   - hostile-life world → **biological** research,
   - black hole / neutron star → **physics / gravimetric**,
   - special asteroid / exotic body → **materials / geology**,
   - …and any other flavor that's logical for the target.
   - **A surface research OUTPOST yields MORE** than an orbital station — the orbital station is the *no-colonize convenience* (less yield, but you don't have to live there).
2. **Mining + refining station** — parked in an asteroid field / belt. **No research, but:** mines bodies in range (range improvable by development), **refines the ore on-station**, and is the **ore depot** — so no refinery ship, no hauling raw ore back to a planet. Mine → refine → store, all in one place.
3. **Commerce station** — a dedicated trade / market hub.
4. **Population station** — pure population development (the morale/population loop in station form — a place whose *product* is people).
5. **Generic** — any component loadout you want; the flavors above are just common presets.

---

## The growth ladder (shared with colony progression)

Every station **starts small** → you **develop it** to fulfill its purpose → if you need more, **build it bigger** → but **the larger the station, the larger the operating cost.** This is the *same* cost↑/yield↑ "if done right" ladder as colonies (`docs/COLONY-PROGRESSION-DESIGN.md`) — stations and colonies should share that progression machinery, not duplicate it.

**Racial traits set the space-habitation debuffs** — a species suited to void-living pays little to run a station; one that isn't pays more (lower efficiency / higher cost / morale penalty). Ties into the species/traits system.

---

## Cradle to grave (the acceptance test, per flavor)

> **mineral** (mined) → **material** (refined) → **station modules** (components, designed in the designer) → gated by **station/research tech** → **built & deployed** to a location → **manned** (population) → the **product** (research points / refined ore / trade / pops) → **operating cost** (upkeep that scales with size) → **destroyed or abandoned** (grave: you lose the research buff / the depot / the refinery throughput, and re-build to get it back).

A research station that can't be researched, built from materials, manned, and *lost* fails the test — it'd be a parachuted-in buff, the "pretty, not a decision" anti-pattern (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`).

---

## Where this plugs in (connections to map before building)

- **Colonies** (`GameEngine/Colonies/`) — the host abstraction to generalize off `PlanetEntity`; the morale/population loop runs here and on stations alike.
- **Tech/Research** (`GameEngine/Tech/`) — `ResearchPointsAtbDB.bonusCategory` is the hook for flavored research; research stations feed it.
- **Industry / Mining** (`GameEngine/Industry/`) — refining lines + `MiningDB` run on a station; the improvable mining *range* is new.
- **Logistics** (`GameEngine/Logistics/`) — a station as a cargo depot / transfer node (routes already exist).
- **Galaxy / bodies** (`GameEngine/Galaxy/`) — what a station orbits (body, belt point, anomaly).
- **People / Species** — "always manned" = population; racial traits set the void-living debuffs.
- **Movement / Orbits** — a station holds a station-keeping orbit (`OrbitDB`) at its location.

---

## Locked vs. open

**Locked (developer's words):**
- The space station is the universal off-world infrastructure container; build-anything-on-a-station, debuffs from racial traits.
- Flavors: research (orbital, less than a surface outpost), mining+refining+depot, commerce, population, generic.
- Research yields the *logical* flavor for what's studied (hostile life → biology, black hole → physics, etc.) and buffs that category.
- Mining station: mine-in-range (improvable) + refine + depot, removing refinery ships / hauling.
- Start small → develop → cost↑ with size; the same ladder as colonies.
- It's the *basis* of space infrastructure but must not *block* the underlying systems.

**Open (lock these WHEN we build, don't invent now):**
- Is a station a generalized **colony** (decouple `PlanetEntity`) or a **parallel host** sharing the component infrastructure? (Lean: generalize the "place," don't fork.)
- The **mining range** model — radius from the station to reachable bodies, and how development improves it.
- **Operating cost** units — credits? materials? crew? — and how it scales with size.
- Exactly which **research categories** map to which **targets**, and the orbital-vs-surface yield ratio.
- How **racial traits** quantify the space-habitation debuff.
- The **station growth tiers** (do they mirror Outpost→…→Capitol, or a station-specific ladder?).

---

## Build-order implication (the "shouldn't hinge on it" reading)

Build the **general systems** so they work on any host, with the station as the host abstraction:
1. **Foundation:** generalize the colony/"place" host off `PlanetEntity` so an infrastructure entity can live in orbit/space. *(The single load-bearing change.)*
2. **First real flavor — the research station**, because research is *already* component-based with `bonusCategory`: a manned host in orbit of a target, hosting research components, yielding the logical category. Cheapest path to a complete cradle-to-grave station.
3. **Mining + refining + depot station** — reuse `MiningDB`/`IndustryAbilityDB`/`CargoStorageDB`; the new bit is mine-from-orbit-in-range.
4. **The morale/population loop** runs on any manned place (the survey) — folds in here, not as a separate track.
5. **Commerce / wealth** — last, with the trade/economy layer.
6. **The ladder + operating cost + racial debuffs** — the shared progression machinery, applied to both stations and colonies.

*This is a capture. Next action is NOT to build — it's to read it (with `COLONY-PROGRESSION-DESIGN.md` + the morale survey + `docs/aurora/PLANETARY-INFRASTRUCTURE.md`), lock the open questions, and pick the first slice (the research station is the natural one).*
