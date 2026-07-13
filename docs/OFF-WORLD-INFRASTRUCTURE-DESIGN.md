# Off-World Infrastructure — Stations, Colonies, and the Growth Ladder (design capture)

**What this is:** the single design frame for *every place a player or NPC stands up infrastructure off the homeworld* — a space station, an orbital research post, a mining depot in a belt, or a full-blown planetary colony. The one idea underneath all of it: **a "place" is just a container with component-infrastructure bolted on**, and every such place **climbs a growth ladder** where each rung costs more and yields more *if you play it right*. Stations are the universal host; the tier-ladder is how any host grows.

**Consolidated 2026-07-13 from:** `docs/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` (the universal off-world host + its full cradle-to-grave build state), `docs/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` (the growth ladder — the shared progression axis).

**Status: mixed.** The **station host, deploy path, and several flavors are BUILT and CI-gauged** (engine-side — runtime unverified, CI can't run the client). The **progression ladder (Outpost→…→Capitol) is vision-capture only, NOT a build spec yet** — do not build the ladder until colony work is on the slate and the MVP firewall (`docs/MVP.md`) allows it. Read this before scheduling any space-economy / infrastructure work, then turn the open questions into locked decisions.

Companions: the colony **morale/population** survey (`docs/MORALE-AND-POPULATION-DESIGN.md`), `docs/NORTH-STAR-VISION.md` (the BSG nomadic pillar), and `docs/aurora/PLANETARY-INFRASTRUCTURE.md`.

---

## The one principle

**A space station is the universal CONTAINER for off-world infrastructure.** Anything you can build on a planet, you can essentially build on a station; the **debuffs you take for doing it in space depend on your racial traits.** Most space-economy gameplay hangs off the station — **but it must not HINGE on it:** the underlying systems (research yield, mining, refining, commerce, population/morale) are *general*, so they work on any host and nothing is blocked waiting for a finished station framework.

The developer's framing, verbatim intent: *"most of space gameplay infrastructure is based off the space station — but [it] shouldn't hinder/hinge on it."*

**And every such place GROWS.** A site — station or colony, player or NPC — is never just "placed and done." It climbs a ladder, and **each step up costs more but yields more *if done right*.** Growth is a decision with a payoff and a price, not a free upgrade. The whole ladder **scales:** costs and yields both climb as you go up. So infrastructure is a *cradle-to-grave* economic decision (mirrors the combat/hazard cradle-to-grave discipline): you invest to grow a site, you get more out of it, and a site you over-extend or mismanage costs you.

---

## Why PARALLEL, not generalized — the station is the planet's ALTERNATIVE (LOCKED 2026-06-29)

The station is built as its **own host type** (`StationInfoDB`), a deliberate PARALLEL to the colony — **not** a generalized planet-less colony. Because two trade-offs *are* the gameplay, and they only exist if stations and planets are mechanically distinct:

**1. The cost gradient — cheap while focused, expensive as a planet-replacement.** A station is the **cheap, fast, flexible alternative to planetary colonization** — the way to do anything you'd do planetside *without committing to a planet.* It stays cheap **while it stays a focused tool** (a mining station, a trade hub, a research post, a shipyard). The moment you push it toward being a **full planetary replacement** (doing everything a colony does at once), the cost **climbs.** Focused = cheap; general = you pay for it. *(Exact curve = math TBD: cost should rise as a station accumulates distinct functions / population beyond its core purpose.)*

**2. The durability asymmetry — cheap to build, cheap to kill.** However much infrastructure you stack on a station, it takes a **fraction of the effort to destroy** that a planet does. A station **can** be invaded — but that's the attacker's **quick choice,** not a campaign. Taking a **planet** is a **long ground war.** Planets can *eventually* be destroyed too, but the energy gap is enormous — **blowing on a flower vs. pushing a Seawolf-class submarine.** *(Destruction/invasion-effort numbers = math TBD; the RATIO is the point.)*

**The decision this creates:** spread **cheap, fragile stations** (fast reach, easily lost) vs. invest in **durable planets** (slow, costly, enduring). A single generalized host would flatten that into a slider; parallel hosts let each own its cost curve, HP/durability, and invasion model. **This wires stations straight into the combat/damage systems already built** — a station is a damageable entity with toughness (like a ship), killable by normal weapons; a planet sits at the far, planet-cracker end of the scale.

> **BUILD STATE — the "cheap to kill" half is now wired (Slice B, 2026-07-03).** A station is no longer a "ghost target." `DamageProcessor.OnStationDamage` deals real population + module damage, and a **placeholder** structural-integrity pool (`StationInfoDB.StructuralIntegrity`, base 500) gives it the kill trigger a planet doesn't have — `StationFactory.DestroyStation` then tears it down cleanly (spawned researcher sub-entities, faction registries). This rides the DIRECT weapon-hit path (beam/missile). **Still to build:** representation in the fleet **auto-resolve** engine (it's `FleetDB`-keyed, so a station isn't a target *inside a fleet battle* yet), and **invasion/capture** (taking a station intact — rides ground combat). The HP/durability numbers and the station-kill-vs-planet-invasion RATIO remain the open tuning question below.

**Parallel ≠ duplicate-everything.** The **component-infrastructure layer is SHARED:** research/mining/refining/cargo are components attached via `ComponentInstancesDB` to *any* entity, so the same equipment runs on a station or a colony, and the economic processors that key off the *ability* blob (mining, industry, research) work on a station for free. Only the **host wrapper** (`StationInfoDB` vs `ColonyInfoDB`), the **cost curve,** the **durability/invasion math,** and **population/manning** differ. So: **shared equipment, distinct chassis, distinct survivability.**

---

## The big insight (why this is mostly CONNECT, not rebuild)

Pulsar's infrastructure is **already component-based** (`CONVENTIONS.md` §6 — abilities are components). A "place" is just an entity with installed component DataBlobs that do economic work. A **colony** is that, *hard-tied to a planet* (`ColonyInfoDB.PlanetEntity`). A **station** is the *same thing with the planet coupling removed* — a colony-class host that sits in **orbit of a body, a point in a belt, or next to an anomaly.**

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

1. **Research station** — orbit a target you *won't* colonize (a planet with hostile life, a black hole, a special asteroid). **Always manned, full cradle-to-grave.** Yields research points of the flavor **logical to what it studies,** buffing that research category:
   - hostile-life world → **biological** research,
   - black hole / neutron star → **physics / gravimetric,**
   - special asteroid / exotic body → **materials / geology,**
   - …and any other flavor that's logical for the target.
   - **A surface research OUTPOST yields MORE** than an orbital station — the orbital station is the *no-colonize convenience* (less yield, but you don't have to live there).
2. **Mining + refining station** — parked in an asteroid field / belt. **No research, but:** mines bodies in range (range improvable by development), **refines the ore on-station,** and is the **ore depot** — so no refinery ship, no hauling raw ore back to a planet. Mine → refine → store, all in one place.
3. **Commerce station** — a dedicated trade / market hub.
4. **Population station** — pure population development (the morale/population loop in station form — a place whose *product* is people).
5. **Listening outpost** — a sensor/listening station out at a **distant star system** for early warning / detection coverage (the detection-arm flavor). Mostly a LOADOUT (station + a big sensor module) — see "Developer directions" #3 below, now BUILT.
6. **Generic** — any component loadout you want; the flavors above are just common presets.

---

## The growth ladder (the shared progression axis — stations AND colonies)

**This is the growth machinery every host shares.** Stations and colonies should share this progression, not duplicate it: every station **starts small** → you **develop it** to fulfill its purpose → if you need more, **build it bigger** → but **the larger the station, the larger the operating cost.** This is the *same* cost↑/yield↑ "if done right" ladder as colonies.

**Racial traits set the space-habitation debuffs** — a species suited to void-living pays little to run a station; one that isn't pays more (lower efficiency / higher cost / morale penalty). Ties into the species/traits system.

### The colony/infrastructure ladder (lowest → highest)

> *Status note: this ladder is captured VISION, not yet a build spec. Recorded 2026-06-28 — "mark this down, it becomes relevant when we get into building colonies." It is the north star for how sites grow, not the next ticket.*

```
Outpost ──(populate)──▶ Colony ──(by flavor)──▶ World ──▶ Minor ─▶ Hub ─▶ Major ─▶ Capitol
  │                       │                        │
  automatable             has FLAVORS         the flavor decides
  (the ONLY tier          (multiple kinds)    which World path it
   that can be                                 can take
   automated)
```

1. **Outpost** — the entry rung, where every site starts.
   - **The ONLY tier that can be AUTOMATED** (set it and let it run; no hands-on management required). This is the key distinction: automation is a *low-tier* affordance, traded away as a site grows into something that demands real management.
   - Once it becomes **populated,** it can be progressed into a **Colony.**

2. **Colony** — the populated, managed tier.
   - **Has multiple different FLAVORS** (specialisations/types — TBD). The flavor you grow it into is a meaningful choice.
   - **The flavor determines which path it can take** toward World status.

3. **World** — the site has become a full world. From World it progresses down a path of increasing stature:
   - **Minor World**
   - **Hub World**
   - **Major World**
   - **Capitol World**
   - Each is a bigger investment and a bigger payoff than the last (scaling).

> **Open question carried from the stations doc:** do the **station growth tiers** mirror this Outpost→…→Capitol ladder, or is there a station-specific ladder? Unresolved — lock when built. The intent is that stations and colonies *share the progression machinery,* which argues for one ladder; the automation and manning differences may argue for station-specific rungs.

---

## Cradle to grave (the acceptance test)

**For a station, per flavor:**

> **mineral** (mined) → **material** (refined) → **station modules** (components, designed in the designer) → gated by **station/research tech** → **built & deployed** to a location → **manned** (population) → the **product** (research points / refined ore / trade / pops) → **operating cost** (upkeep that scales with size) → **destroyed or abandoned** (grave: you lose the research buff / the depot / the refinery throughput, and re-build to get it back).

A research station that can't be researched, built from materials, manned, and *lost* fails the test — it'd be a parachuted-in buff, the "pretty, not a decision" anti-pattern (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`).

**For the colony ladder (the acceptance test when built):** a player must be able to reach each rung through the whole chain — the minerals/materials to grow it, the decision to progress (and the cost), the yield it pays back, and the loss if it's taken/demoted. A tier that's just a label with no decision behind it fails the test (the "pretty, not a decision" anti-pattern).

---

## The BUILD MODEL — deploy bare, build in-situ (LOCKED 2026-06-30)

**The developer's call (his words):** *"What if I slap a mining station between some asteroids and a mining station is just a station UNTIL I build a mining module on it."* Locked: a station is a **generic chassis;** *"mining station"* / *"research station"* are **descriptions of the modules you bolted on, not types you pick** — exactly the component-chassis law ships already follow (a "warship" is a ship with guns). And the build model is **deploy a bare platform, then build its modules ON it, on location** — NOT "design-and-assemble-complete-at-a-shipyard" (that was the rejected alternative; it bakes the modules in at build time and loses the "just a station until you add a module" feel).

**The bare platform** is the cheap minimum that can host modules and bootstrap construction: **frame + power + control core + minimal infrastructure + a small constructor module** (the constructor is what gives it an in-situ build line; the infra/power is what lets that line actually run — see the efficiency note below). You deploy it to a spot (a belt body / point between asteroids), and it sits there inert — *just a station* — until you build a module onto it.

**The in-situ loop is ALREADY host-agnostic in the engine** (verified by reading, 2026-06-30 — this is mostly CONNECT, not build):
- `IndustryProcessor` keys on `IndustryAbilityDB`, not host type — a station with a constructor module is processed for free.
- `IndustryAtb.OnComponentInstallation` creates the `IndustryAbilityDB` (the production line) on **any** entity the constructor is installed on (`Industry/IndustryAtb.cs:55-57`).
- `IndustryTools.ConstructStuff` operates on a generic `industryEntity` (CargoStorageDB + IndustryAbilityDB + faction) with **no `ColonyInfoDB` assumption** (`Industry/IndustryTools.cs:90`).
- `ComponentDesign.OnConstructionComplete` installs a finished module via **`batchJob.InstallOn.AddComponent(...)` + `ReCalcAbilities`** — no colony check (`Engine/Components/ComponentDesign.cs:70-77`). Set `InstallOn = theStation` and the built module lands on the station.
- The **mining wire** (this branch) then makes the installed mine actually mine its hosting body.

So the full chain — *ship materials to the platform → its constructor fabricates a mine → installs it on itself → it mines the belt* — rides entirely on existing, now-verified host-agnostic code. **Gauge:** `StationFactoryTests.Station_WithConstructorModule_IsAnInSituBuilder` (a station gains a production line that handles the colony's installation industry type and accepts an install-on-self job) + `Station_WithMiningModule_MinesItsHostingBody`.

**The efficiency catch (CORRECTED 2026-07-03 — the earlier claim was backwards).** `ConstructStuff` scales output by `InfrastructureProcessor.GetEfficiency`, and the intent was: a bare platform carrying a constructor but no infrastructure/power runs at ~0 efficiency, so it MUST carry minimal infra. **But as currently coded that pressure does NOT exist on a station.** `InfrastructureProcessor.RecalcCapacity` early-returns for any entity lacking `ColonyInfoDB` (`Industry/InfrastructureProcessor.cs:27-30`), so a station's `InfrastructureDB` stays `0/0` → `Efficiency == 1.0` → a bare platform builds at FULL speed, not ~0. So today the "build your infra first" pressure is absent for stations. If we want it, `RecalcCapacity` must be extended to resolve a station's body via `StationInfoDB.HostingBodyEntity` (a small, contained change) — otherwise drop the claim. Either way the bare platform is still "frame + power + control + minimal infra + constructor" for the *other* reasons (life support, and so the infra is there once the pressure is wired).

**What's still to BUILD (the player-facing front of the chain — engine + UI):**
1. ~~**A deploy path**~~ — **BUILT (Slice A → A2, 2026-07-03).** A **SHIP-ISSUED** `DeployStationOrder`: fly a **construction ship** (a hauler) to a spot and "Deploy Station Here" → the platform anchors to the ship's SOI body (**a star / belt / planet — wherever it's parked**) with a starter constructor. The reusable vessel survives. *(First cut was a planet-list button, but that was survey-gated/body-bound and could NOT place a station around a star — the whole point — so it was replaced with the ship-issued path and the button removed.)* Engine order CI-gauged (`ConstructionShip_DeploysStation_AtAStar_AndSurvives` — deploys at a star); the context-menu UI is compile-checked only.
2. ~~**Queue-a-module-on-a-station UI**~~ — **BUILT (Slice A).** A new **`StationWindow`** (map context menu → "Manage Station", gated on `StationInfoDB`) embeds the host-agnostic `IndustryDisplay` — so a deployed platform with a constructor lets the player queue + install modules on itself. (Runtime-unverified — CI can't run the client.)
3. **Materials supply** — ship materials to the station (logistics/cargo) for the FIRST module; once a mine + refinery are up it can self-sustain from the belt (the satisfying loop). *Partially built via Slice F (see Developer directions #1/#2 — the deploy now charges frame materials from the ship/fleet hold). Still to build for ongoing MODULE supply: `StationFactory` doesn't attach `LogiBaseDB`, and nothing seeds a station's `DesiredLevels` from a queued module — so per-module materials must be hand-shipped for now.*
4. **(Later)** a station designer mirroring the ship designer (task #21 territory) — for v1, a couple of preset bare-platform + module designs suffice.

---

## Developer directions — deploy mechanics + flavors + anchors (captured 2026-07-03)

Four directions the developer set after the first deploy path landed (feasibility verified against the code; captured so nothing's lost):

1. **The construction vessel is REUSABLE and consumes resources scaled to how it's built.** A construction ship survives its deploy and can deploy again; **how much it can build is a function of its CARGO CAPACITY** (a bigger hauler carries more of the materials a station needs). This is the materials-supply loop, now with a design: the deploy/build DRAWS materials, capacity-limited — not free. **BUILT (Slice F, 2026-07-03):** `DeployStationOrder` now charges a **frame material cost** (`TryPayFrameMaterials` / `FrameCostUnits`) — placeholder consts `FrameMaterialId = "stainless-steel"`, `FrameFractionOfShipCapacity = 0.5`, `FrameFloorUnits = 10` — scaled by the construction ship's own cargo capacity, drawn from its hold; not enough → the deploy is **refused** (no station, a `MineralShortage` event). This closes the station's cradle-to-grave **production** rung (the frame is now built from mined→refined material, not conjured). Also fixed the flagged **empty-hold bug:** the deploy loadout now includes `default-design-warehouse` so a deployed station's `CargoStorageDB` is a real seeded store (a bare `new CargoStorageDB()` silently no-ops every add/remove). Gauge: `ConstructionShip_DeploysStation_AtAStar_AndSurvives` (now asserts consumption + a working hold). **Refinements:** a multi-material bill; tuning the ratios.
2. **Fleet-pooled materials.** If the construction ship is **part of a FLEET, the build draws on the materials stored across the OTHER ships in that fleet** — a fleet-wide cargo pool, not just the one hull. So you send a construction ship + freighters together and the whole fleet's holds feed the station. **BUILT (Slice F, 2026-07-03):** `GatherPooledHolds` — since a ship has **no fleet back-reference,** the deploy finds the ship's fleet by searching the system's `FleetDB` entities for the one whose `Children` include it, then pools every fleet-mate's `CargoStorageDB` with the ship's own; the cost is drawn across the pool. Gauge: `ConstructionShip_PoolsFleetMaterials_OnDeploy` (constructor's own hold empty → a fleet-mate freighter supplies the materials → deploy succeeds, freighter drained) + `ConstructionShip_InsufficientMaterials_RefusesDeploy`. **Refinements:** sub-fleet recursion (v1 pools direct children only — `CombatEngagement.GetFleetShips` is the recursive walk to copy).
3. **The "listening outpost" flavor.** A sensor/listening station built out at a **distant star system** for early warning / detection coverage — the detection-arm station flavor (alongside research / mining / commerce / population). Likely mostly a LOADOUT (station + a big sensor module) if the sensor system runs on a station host. **BUILT (Slice E, 2026-07-03):** it WAS mostly a loadout — detection is host-agnostic (`SensorScan` keys on `SensorAbilityDB`, which a station gets when you install a sensor). The one real fix was the **mid-game first-scan kick:** `SensorReceiverAtb.OnComponentInstallation` now schedules the first `SensorScan` (previously only `Game.PostNewGameInitialization` did, so a station built after game-start sat deaf). Data: a bigger `default-design-outpost-sensor` ("Deep-Space Listening Array", antenna 12000, off the existing `passive-sensor` template) unlocked in the Earth blueprint. Gauge: `StationFactoryTests.ListeningOutpostStation_DetectsHostileShip_ViaInstallScheduledScan`. **Refinements:** a dedicated sealed-sensor template with its own tuned reach/scan-time, and the client blip/label for an outpost's contacts (runtime-unverified).
4. **Lagrange-point anchors — build in space, not at "random points."** Mark the **Lagrange points around planet/star/moon pairs** as named anchor locations, so a station deploys at a *stable, meaningful point in space* rather than an arbitrary spot. **BUILT (Slice D, 2026-07-03):** `LagrangeFactory.GenerateForSystem` (hooked into system gen, defensive/idempotent) creates **L4/L5 Trojan markers** for each star-planet pair — a **STATIC point** entity (the star→planet vector rotated ±60° in the orbital plane) with **no `OrbitDB`** and a `PositionDB` set to `MoveType.None`, so it never enters the orbit processor, carrying a token `MassVolumeDB` so `StationFactory` anchors to it unchanged. *(A first cut gave the marker the planet's orbit offset ±60° to co-orbit "for free"; that crashed the parallel orbit processor with a `PositionDB` lookup on a worker thread — so v1 is a fixed point at the epoch L-point, mirroring the proven `JPSurveyFactory` non-body-marker recipe. Making it co-orbit is a documented refinement.)* `DeployStationOrder` now prefers a nearby Lagrange marker (within a placeholder capture radius) over the raw SOI body. Gauge: `LagrangeMarkers_AreGeneratedForPlanets_AtTheTrojanPoints`. **Refinements:** letting the marker co-orbit (a tiny `LagrangeProcessor` recomputing position each cycle), the collinear/unstable L1/L2/L3, planet-moon pairs, the other gen paths (only the main + Sol paths hooked), and a client icon/label for the markers (runtime-unverified) + real capture-radius tuning.

**Build-order read:** (3) listening outpost is the cheapest (a loadout, since sensors run on a station); (1)+(2) materials/fleet-pool is the meaty economy loop (the deferred `LogiBaseDB`/cargo work, now scoped); (4) Lagrange anchors is a self-contained galaxy-gen + deploy-target feature. Lock each as its own slice.

---

## Where this plugs in (connections to map before building)

**For stations:**
- **Colonies** (`GameEngine/Colonies/`) — the **reference chassis** the parallel `StationInfoDB` mirrors (NOT generalized — `ColonyInfoDB` stays planet-tied); the morale/population loop must be shared so it runs on both hosts. This is also where the colony **tier** lives (the ladder above).
- **Tech/Research** (`GameEngine/Tech/`) — `ResearchPointsAtbDB.bonusCategory` is the hook for flavored research; research stations feed it. Also likely gates colony flavors / higher tiers (cradle-to-grave: research → unlock a tier/flavor).
- **Industry / Mining** (`GameEngine/Industry/`) — refining lines + `MiningDB` run on a station; the improvable mining *range* is new. Also owns the cost↑/yield↑ scaling per tier; what a site can build/produce.
- **Logistics** (`GameEngine/Logistics/`) — a station as a cargo depot / transfer node (routes already exist).
- **Galaxy / bodies** (`GameEngine/Galaxy/`) — what a station orbits (body, belt point, anomaly); the body a colony sits on (siting = part of "done right").
- **People / Species** — "always manned" = population; racial traits set the void-living debuffs.
- **Morale / population / manpower (M1–M3, built)** — `ColonyMoraleDB`, `ColonyManpowerDB`, `EmploymentAtbDB`, `HousingAtbDB` were built **host-agnostic** (no `ColonyInfoDB` dependency). A manned station attaches the *same* blobs; the one load-bearing change is teaching `PopulationProcessor` to also process `StationInfoDB` (or a thin parallel processor). So the morale/people loop serves stations nearly for free — this is the answer to the station "manning" open question. See `docs/MORALE-AND-POPULATION-DESIGN.md` Prime-Directive accounting finding #7.
- **Movement / Orbits** — a station holds a station-keeping orbit (`OrbitDB`) at its location.

**For the colony-progression ladder (the planetary/economic mirror of "give ground/infrastructure the depth space combat has" — root `CLAUDE.md` → Developer Objective):**
- **Factions / NPC AI** — NPCs climb the same ladder; outpost automation is NPC-style management applied to the player's low tier (ties into the thin faction-AI layer — see `GameEngine/Factions/CLAUDE.md` and the NPC-AI parking item in the hazard foundation map).
- **UI** — `ColonyManagementWindow` already has the economy tabs; the tier + progression controls (and, for stations, the `StationWindow`) would extend it.

---

## Locked vs. open

### Locked

**Stations (developer's words):**
- The space station is the universal off-world infrastructure container; build-anything-on-a-station, debuffs from racial traits.
- Flavors: research (orbital, less than a surface outpost), mining+refining+depot, commerce, population, listening outpost, generic.
- Research yields the *logical* flavor for what's studied (hostile life → biology, black hole → physics, etc.) and buffs that category.
- Mining station: mine-in-range (improvable) + refine + depot, removing refinery ships / hauling.
- Start small → develop → cost↑ with size; the same ladder as colonies.
- It's the *basis* of space infrastructure but must not *block* the underlying systems.

**Stations (architecture, 2026-06-29):**
- **PARALLEL host, NOT a generalized colony.** A station is its own `StationInfoDB` chassis; the colony stays `ColonyInfoDB` + `PlanetEntity`, untouched. The two trade-offs (cost gradient, durability asymmetry) ARE the gameplay and only exist if the hosts are mechanically distinct. The **component-infrastructure layer is shared** (research/mining/refining/cargo via `ComponentInstancesDB`); only the host wrapper, cost curve, durability, and manning differ.

**Stations (build model, 2026-06-30):**
- A station is a generic chassis; flavors are the modules you bolt on, not types you pick.
- Deploy a bare platform, then build its modules ON it, on location (rejected: design-and-assemble-complete-at-a-shipyard).

**The colony/infrastructure ladder (developer's words):**
- Universal growth process for all infrastructure (player + NPC).
- Cost↑ / yield↑ per step, *"if done right"* (reward for good play, not a free ride).
- Outpost is the only automatable tier.
- Outpost → (populate) → Colony → (flavor) → World → Minor → Hub → Major → Capitol.
- Everything scales.

### Open (lock these WHEN we build, don't invent now)

**Stations:**
- The **cost-gradient curve** — how cost rises as a station accumulates distinct functions / population beyond its core focus (cheap while focused → expensive as a planet-replacement). *(A PLACEHOLDER is now in code — Slice C, 2026-07-03: a monthly OPERATING cost `StationEconomyDB.OperatingCost` = `Base + PerModule×modules + PerFunction×distinctDesigns + PerPop×pop/1000`, billed to the faction by `StationUpkeepProcessor`. The steep `PerFunction` term per DISTINCT design is what encodes "function-diversity = expensive." Gauge: `StationUpkeep_DrainsFunds_AndScalesWithFunctionDiversity`. Lock the real curve/coefficients here; also decide whether BUILD cost — not just upkeep — should climb with accumulated functions.)*
- The **durability / invasion / destruction numbers** — the station-kill-effort vs. planet-invasion-effort RATIO (the "blowing on a flower vs. a Seawolf" gap), and what a station's HP/toughness is as a damageable combat entity. *(A PLACEHOLDER is now in code — `StationInfoDB.BaseStructuralIntegrity = 500`, a flat pool vs. the planet's no-kill-trigger ∞ — as of Slice B, 2026-07-03. Lock the real curve/ratio here; also decide whether structural integrity should scale with anything, and the invasion-intact model, which is unbuilt.)*
- The **population / manning coupling** — `PopulationProcessor` is `ColonyInfoDB`-keyed today; a manned station needs a shared population/morale concept that both hosts can carry without forking the processor.
- The **mining range** model — radius from the station to reachable bodies, and how development improves it.
- **Operating cost** units — credits? materials? crew? — and how it scales with size.
- Exactly which **research categories** map to which **targets,** and the orbital-vs-surface yield ratio.
- How **racial traits** quantify the space-habitation debuff.
- The **station growth tiers** (do they mirror Outpost→…→Capitol, or a station-specific ladder?).

**The colony/infrastructure ladder (parking lot — do not invent answers now):**
- What are the **Colony flavors,** and how does each gate the World path?
- What does **"done right"** mean mechanically — what's the skill/decision that turns growth into *more* yield vs. wasted cost (siting? resource match? infrastructure mix? timing?)?
- What **triggers/gates** each progression (population threshold? built infrastructure? research? funds? time)?
- What exactly **"populated"** means for Outpost → Colony.
- How **automation** at the Outpost tier actually behaves (NPC-style auto-management), and why higher tiers can't be automated (the management *is* the gameplay).
- How **NPCs** drive the same ladder.
- The **loss/grave rung** (cradle-to-grave): what happens when a high-tier world is bombarded/blockaded/loses population — does it *demote* down the ladder?

---

## Build-order implication (the "shouldn't hinge on it" reading)

Build the **general systems** so they work on any host, with the station as a parallel chassis:
1. **Foundation:** build the **parallel `StationInfoDB` host + `StationFactory`** — an infrastructure entity that holds a station-keeping orbit and reuses the shared `ComponentInstancesDB` equipment layer. The colony (`ColonyInfoDB` + `PlanetEntity`) is left untouched. *(The load-bearing build — and where the station's own cost curve / durability / manning hook in.)* **[BUILT — see build state below.]**
2. **First real flavor — the research station,** because research is *already* component-based with `bonusCategory`: a manned host in orbit of a target, hosting research components, yielding the logical category. Cheapest path to a complete cradle-to-grave station.
3. **Mining + refining + depot station** — reuse `MiningDB`/`IndustryAbilityDB`/`CargoStorageDB`; the new bit is mine-from-orbit-in-range.
4. **The morale/population loop** runs on any manned place (the survey) — folds in here, not as a separate track.
5. **Commerce / wealth** — last, with the trade/economy layer.
6. **The ladder + operating cost + racial debuffs** — the shared progression machinery, applied to both stations and colonies.

---

## Build-state summary (honest, three-state)

Nothing below is runtime-verified — CI compiles but cannot run the client. "Built" here means *coded + CI-gauged by an engine test*, not *played.*

**built-but-unverified-at-runtime (engine coded + CI test exists, client-runtime unproven):**
- Station host `StationInfoDB` + `StationFactory` (the parallel chassis) — Slice A.
- Deploy path: ship-issued `DeployStationOrder`, reusable construction ship, deploys at a star/belt/planet — Slice A/A2. UI (`StationWindow`, context menu) compile-checked only.
- In-situ build loop: constructor module → queue module → install-on-self → mine hosting body — verified host-agnostic + gauged.
- "Cheap to kill": `DamageProcessor.OnStationDamage` + placeholder `StructuralIntegrity` (500) + `StationFactory.DestroyStation` — Slice B (direct weapon-hit path only).
- Operating-cost placeholder: `StationEconomyDB.OperatingCost` + `StationUpkeepProcessor` — Slice C.
- Lagrange L4/L5 Trojan static markers + deploy prefers them — Slice D.
- Listening-outpost flavor: sensor on a station + mid-game first-scan kick — Slice E.
- Frame-material cost on deploy + fleet-pooled materials + empty-hold fix — Slice F.

**not-built (design only):**
- Station in the fleet **auto-resolve** engine (it's `FleetDB`-keyed — a station isn't a target inside a fleet battle yet).
- **Invasion / capture** of a station intact (rides ground combat).
- Per-module ongoing **materials supply** loop (`LogiBaseDB` on stations, `DesiredLevels` seeding).
- The **efficiency pressure** for stations (`InfrastructureProcessor.RecalcCapacity` early-returns without `ColonyInfoDB` — so a bare platform builds at full speed, not ~0).
- The **colony-progression ladder** (Outpost→…→Capitol) — entirely vision-capture, no code.
- Real tuning for every placeholder above (cost curve, durability ratio, mining range, racial debuffs, research-category map, capture radius).
- Station designer UI; co-orbiting Lagrange markers + collinear L1/L2/L3 + planet-moon pairs.

*This began as a capture. The next action on the ladder half is still NOT to build — it's to read it (with the morale survey + `docs/aurora/PLANETARY-INFRASTRUCTURE.md`) when colony progression is scheduled, then turn the open questions into locked decisions. The station half is already several slices into build; continue by locking the placeholders above into real numbers and finishing the not-built list.*
