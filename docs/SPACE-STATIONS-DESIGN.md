# Space Stations — the universal off-world infrastructure (design capture)

**Status: vision capture, recorded 2026-06-28 at the developer's request. NOT a build spec yet** — this is the *unifying frame* for the space economy + infrastructure: "space eco goes with infrastructure, get it all in one." Read it before scheduling any space-economy work, then turn the open questions into locked decisions. Companion to `docs/COLONY-PROGRESSION-DESIGN.md` (the growth ladder), the colony **morale/population** survey, and `docs/NORTH-STAR-VISION.md` (the BSG nomadic pillar).

---

## The one principle

**A space station is the universal CONTAINER for off-world infrastructure.** Anything you can build on a planet, you can essentially build on a station; the **debuffs you take for doing it in space depend on your racial traits**. Most space-economy gameplay hangs off the station — **but it must not HINGE on it**: the underlying systems (research yield, mining, refining, commerce, population/morale) are *general*, so they work on any host and nothing is blocked waiting for a finished station framework.

The developer's framing, verbatim intent: *"most of space gameplay infrastructure is based off the space station — but [it] shouldn't hinder/hinge on it."*

---

## Why PARALLEL, not generalized — the station is the planet's ALTERNATIVE (LOCKED 2026-06-29)

The station is built as its **own host type** (`StationInfoDB`), a deliberate PARALLEL to the colony — **not** a generalized planet-less colony. Because two trade-offs *are* the gameplay, and they only exist if stations and planets are mechanically distinct:

**1. The cost gradient — cheap while focused, expensive as a planet-replacement.** A station is the **cheap, fast, flexible alternative to planetary colonization** — the way to do anything you'd do planetside *without committing to a planet*. It stays cheap **while it stays a focused tool** (a mining station, a trade hub, a research post, a shipyard). The moment you push it toward being a **full planetary replacement** (doing everything a colony does at once), the cost **climbs**. Focused = cheap; general = you pay for it. *(Exact curve = math TBD: cost should rise as a station accumulates distinct functions / population beyond its core purpose.)*

**2. The durability asymmetry — cheap to build, cheap to kill.** However much infrastructure you stack on a station, it takes a **fraction of the effort to destroy** that a planet does. A station **can** be invaded — but that's the attacker's **quick choice**, not a campaign. Taking a **planet** is a **long ground war**. Planets can *eventually* be destroyed too, but the energy gap is enormous — **blowing on a flower vs. pushing a Seawolf-class submarine.** *(Destruction/invasion-effort numbers = math TBD; the RATIO is the point.)*

**The decision this creates:** spread **cheap, fragile stations** (fast reach, easily lost) vs. invest in **durable planets** (slow, costly, enduring). A single generalized host would flatten that into a slider; parallel hosts let each own its cost curve, HP/durability, and invasion model. **This wires stations straight into the combat/damage systems already built** — a station is a damageable entity with toughness (like a ship), killable by normal weapons; a planet sits at the far, planet-cracker end of the scale.

**Parallel ≠ duplicate-everything.** The **component-infrastructure layer is SHARED**: research/mining/refining/cargo are components attached via `ComponentInstancesDB` to *any* entity, so the same equipment runs on a station or a colony, and the economic processors that key off the *ability* blob (mining, industry, research) work on a station for free. Only the **host wrapper** (`StationInfoDB` vs `ColonyInfoDB`), the **cost curve**, the **durability/invasion math**, and **population/manning** differ. So: **shared equipment, distinct chassis, distinct survivability.**

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
| **The host (NEW — parallel chassis)** | a colony is `ColonyInfoDB` + `PlanetEntity` (left as-is) | build a parallel `StationInfoDB` + `StationFactory` that orbits a location, reuses the shared components, and owns its cost / durability / invasion math |

**The load-bearing build** is a new **parallel `StationInfoDB` host** (+ a `StationFactory`) that reuses the shared component layer — so the *equipment* isn't duplicated — but carries its **own** cost curve, durability/HP, and invasion math (see "Why PARALLEL" above). Everything else is mostly loadout (which components a station carries) + the growth ladder + operating cost + racial debuffs. (Note: the colony stays planet-tied; we are NOT decoupling `PlanetEntity` — the station is a separate chassis, not a generalized colony.)

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

- **Colonies** (`GameEngine/Colonies/`) — the **reference chassis** the parallel `StationInfoDB` mirrors (NOT generalized — `ColonyInfoDB` stays planet-tied); the morale/population loop must be shared so it runs on both hosts.
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

**Locked (architecture, 2026-06-29):**
- **PARALLEL host, NOT a generalized colony.** A station is its own `StationInfoDB` chassis; the colony stays `ColonyInfoDB` + `PlanetEntity`, untouched. The two trade-offs below ARE the gameplay and only exist if the hosts are mechanically distinct (see "Why PARALLEL" above). The **component-infrastructure layer is shared** (research/mining/refining/cargo via `ComponentInstancesDB`); only the host wrapper, cost curve, durability, and manning differ.

**Open (lock these WHEN we build, don't invent now):**
- The **cost-gradient curve** — how cost rises as a station accumulates distinct functions / population beyond its core focus (cheap while focused → expensive as a planet-replacement).
- The **durability / invasion / destruction numbers** — the station-kill-effort vs. planet-invasion-effort RATIO (the "blowing on a flower vs. a Seawolf" gap), and what a station's HP/toughness is as a damageable combat entity.
- The **population / manning coupling** — `PopulationProcessor` is `ColonyInfoDB`-keyed today; a manned station needs a shared population/morale concept that both hosts can carry without forking the processor.
- The **mining range** model — radius from the station to reachable bodies, and how development improves it.
- **Operating cost** units — credits? materials? crew? — and how it scales with size.
- Exactly which **research categories** map to which **targets**, and the orbital-vs-surface yield ratio.
- How **racial traits** quantify the space-habitation debuff.
- The **station growth tiers** (do they mirror Outpost→…→Capitol, or a station-specific ladder?).

---

## Build-order implication (the "shouldn't hinge on it" reading)

Build the **general systems** so they work on any host, with the station as a parallel chassis:
1. **Foundation:** build the **parallel `StationInfoDB` host + `StationFactory`** — an infrastructure entity that holds a station-keeping orbit and reuses the shared `ComponentInstancesDB` equipment layer. The colony (`ColonyInfoDB` + `PlanetEntity`) is left untouched. *(The load-bearing build — and where the station's own cost curve / durability / manning hook in.)*
2. **First real flavor — the research station**, because research is *already* component-based with `bonusCategory`: a manned host in orbit of a target, hosting research components, yielding the logical category. Cheapest path to a complete cradle-to-grave station.
3. **Mining + refining + depot station** — reuse `MiningDB`/`IndustryAbilityDB`/`CargoStorageDB`; the new bit is mine-from-orbit-in-range.
4. **The morale/population loop** runs on any manned place (the survey) — folds in here, not as a separate track.
5. **Commerce / wealth** — last, with the trade/economy layer.
6. **The ladder + operating cost + racial debuffs** — the shared progression machinery, applied to both stations and colonies.

*This is a capture. Next action is NOT to build — it's to read it (with `COLONY-PROGRESSION-DESIGN.md` + the morale survey + `docs/aurora/PLANETARY-INFRASTRUCTURE.md`), lock the open questions, and pick the first slice (the research station is the natural one).*
