# Environments — Space Hazards + Planetary Terrain as ONE Physics-Driven System (Design)

**As of 2026-07-04. Status: DESIGN (no code yet) — the "design it in depth first" pass the developer asked for.**
Companion to `docs/GROUND-COMBAT-MAP-DESIGN.md` (the ground map) and `GameEngine/Hazards/CLAUDE.md` (the built space-hazard engine this extends). Read those first.

---

## The one idea

**An "environment" is a bounded area that changes what happens to anything inside it — and *which* environments a place has is a fingerprint of that place's physics.** That is true in space (a corona, a gas cloud, an ion storm) and it is true on the ground (a fire-tornado field, a corrosive superstorm, a radar-jamming lightning storm). They are the **same system with two hosts** — a patch of space, or a region of a planet.

Two developer steers lock this (2026-07-04):
1. *"Terrain mirrors space environments too."* → mirror the space-hazard pattern on the ground; **share the effect vocabulary**; do **not** refactor the green hazard engine.
2. *"This is a sci-fi game — don't just make Earth biomes… and the engine must be intelligent about where these hazards occur"* (a gas giant has no surface, so no fire tornadoes). → environments are **exotic + dynamic**, and **generated from physical context**, never Earth-copied or flat RNG.

The prize: **"entering a new world is unique and dangerous"** — the planetary twin of the north-star's *"entering a new system is terrifying."* A world's menaces tell you what it is before anyone briefs you.

---

## KEEP: the spine already exists (in space) — do not rebuild it

The space-hazard engine (`GameEngine/Hazards/`) is exactly the shape we want, already CI-green:

- **A typed effect vocabulary** — `HazardEffectType`: `HeatDamage · RadiationDamage · KineticDamage · SensorJam · MovementDrag · WarpInhibit · Corrosive · EMDamage · GravimetricDamage`. A `HazardEffect` = a type + magnitude (+ wavelength, + scales-with-proximity). An environment is a **list** of these.
- **A generic counter as a COMPONENT** — `HazardResistanceAtb` resists an effect kind by a fraction; a new counter is a new component *template* (data), not new C#. The whole cradle-to-grave chain (encounter → discover → research → build rated armour/gear → clad → lose it) rides for free.
- **Two application modes** — *pushed* per-tick (damage, drag) by a processor; *pulled* query-time (sensor cut, warp slow) by the systems affected (`SpaceHazardTools`).
- **Transience** — permanent terrain (a gas cloud) vs scheduled weather (a solar flare that grows→peaks→fades).

**This is the KEEP.** The ground side reuses this vocabulary and this pattern; it does not fork a second effects engine.

---

## The GAP (both sides): generation is DUMB — flat RNG, not physics

Both halves have the same flaw, already flagged for space (`Hazards/CLAUDE.md` ★): **placement is context-blind.** Space rolls the same per-hazard percentages in every system regardless of its star. Ground (today, slice 1) rolls features from a few scalars but only Earth biomes. Neither asks *"what would this place actually have?"*

**The fix is one idea applied twice: an ENVIRONMENT PROFILE derived from physical context.**
- **Space** reads the STAR (`StarInfoDB`: spectral class / luminosity / age) → a system profile (protostar = flares + dust + debris; neutron star = gravimetric + hard-radiation + EM).
- **Ground** reads the PLANET (the scalars below) → a world profile (scorching = fire tornadoes; sulphur air = corrosive storms; gas giant = *nothing on the surface*).

Same emitter, two physical readers. Designing them together is the point of this doc.

---

## The ground exotic-environment CATALOG (sci-fi, typed effects — the vocabulary shared with space)

Each environment is a **list of typed effects** (reusing `HazardEffectType`) plus a permanence. This is a *starting* catalog — new ones are data (a new template + a physics rule), never new engine code.

| Environment | Typed effects (shared vocabulary) | Permanence | The decision it creates |
|---|---|---|---|
| **Fire tornadoes / firestorms** | `HeatDamage` + `MovementDrag` | transient (roam) | don't march armour through; time your assault between them |
| **Molten / lava fields** | `HeatDamage` (severe) + heavy `MovementDrag` | permanent | a near-impassable barrier — flank it |
| **Corrosive superstorm** | `Corrosive` + `MovementDrag` | transient or permanent | need corrosion gear or take attrition crossing it |
| **Toxic/corrosive fog** | `Corrosive` (attrition) + `SensorJam` | permanent | you can't see *or* linger — sensors + protection |
| **Lightning superstorm** | `SensorJam` (radar) + minor `EMDamage` | transient | your recon goes blind — fight by contact, or wait it out |
| **Ash storm / volcanic fall** | `SensorJam` + `MovementDrag` + minor `HeatDamage` | transient | blinds + bogs an offensive |
| **Dust storm** | `SensorJam` + `MovementDrag` | transient | the desert-world fog of war |
| **Cryostorm / ice sheet** | cold `EnvironmentalHazard` (Heat-negative) + `MovementDrag` | permanent/transient | slow, attritional; heated gear helps |
| **Radiation zone** | `RadiationDamage` | permanent | shielding or you bleed strength holding it |
| **Quake/seismic zone** | periodic damage to **BUILDINGS** (ties to the buildings-on-ground principle) | transient pulses | don't put your base on the fault line |
| **High-gravity drag** | `MovementDrag` (whole world) | permanent | heavy worlds are slow to cross — logistics |

**Static geography stays SEPARATE** (the map's *shape*, not weather): mountains/highlands (Cover + artillery high ground), plains (Open), forest/jungle (Cover + Concealment), ocean/coast (barrier / amphibious), desert/ice/wetland. Combat reads *both* the geography (cover/movement/type-affinity — the `GroundTerrain` mechanic) and the dynamic hazards (damage/drag/jam over time).

---

## The INTELLIGENCE: physics → environment (grounded in real, already-computed fields)

The generator reads scalars the engine already computes and emits **only** environments that make sense. Every input below is a real field (confirmed 2026-07-04):

| Physical input (field) | Emits / gates |
|---|---|
| **`SystemBodyInfoDB.BodyType`** = GasGiant / IceGiant | **NO SURFACE → no surface environments at all** (atmospheric-band platforms are a separate, later host). Terrestrial / Moon / DwarfPlanet / GasDwarf → surface environments allowed. **This is the load-bearing gate.** |
| **`AtmosphereDB.SurfaceTemperature`** (°C, Aurora greenhouse) | > ~400 → fire tornadoes / molten fields; < ~-120 → cryostorms / ice; temperate → none from temperature |
| **`AtmosphereDB.Composition`** (gas-id → pressure) | contains a corrosive gas (sulphur/chlorine ids) → corrosive superstorm / toxic fog |
| **`AtmosphereDB.Pressure`** (atm) | thick (> ~5) → lightning superstorms / crushing pressure; near-zero → no weather at all (airless → only radiation/thermal/geology) |
| **`AtmosphereDB.HydrosphereExtent`** (0–100) | < ~10 (+ atmosphere) → dust storms; > ~40 → oceans / monsoons |
| **`SystemBodyInfoDB.Tectonics`** = Minor/Major/… (not Dead/Unknown) | lava fields / ash storms / quake zones (scaled by activity) |
| **`MassVolumeDB`** surface gravity | high-g → whole-world `MovementDrag` |
| **orbit distance / `BaseTemperature`** + no magnetosphere | close-in / unshielded → radiation zones (and couples to the star's flare weather) |

So a world's environment list is *derived*, not authored. Earth: temperate + wet + minor tectonics → oceans, storms, the odd quake — mild. Venus-analogue: scorching + sulphur + thick → corrosive superstorms + crushing pressure + lightning — a nightmare to hold. An airless hot moon: thermal + radiation + lava, no weather. **The world's danger is a read-out of its physics.**

Authored worlds (Sol) can still be hand-tuned, but the *default* is physics-derived so a random Alpha Centauri world is genuinely its own place.

---

## Data model (proposed — decision flagged)

- **Static geography** = `Region.Features` (exists). The map shape; feeds `GroundTerrain` (cover/movement/type-affinity).
- **Dynamic environmental hazards** = a NEW region-hosted **list of typed effects**, the ground echo of `SpaceHazardDB.Effects`. **Proposal: reuse the `HazardEffect` / `HazardEffectType` classes as the shared vocabulary** (read-only reuse — NOT a refactor of the hazard engine), hosted on the region (a `RegionEnvironmentDB` on the body, keyed by region index, or a field on `Region`). Persistent + typed, so a `GroundForcesProcessor`-side applier can push damage/drag and the combat/movement code can pull jam/cover.
  - *Alternative if cross-subsystem reuse feels too coupled:* a parallel `GroundEffect`/`GroundEffectType` that 1:1 mirrors the hazard enum. Costs a little duplication; buys independence. **Decision to lock when E1 starts.**
- **Transience** mirrors the flare: a permanent environment is always present; a transient one has a lifecycle (spawn → roam/peak → fade), advanced by the ground processor exactly as `SpaceHazardProcessor` grows/fades a flare.

---

## Cradle to grave (the mirror completes)

- **Encounter** — a unit in a region with an active hazard takes its typed effect (damage/drag/jam), same as a ship in a space hazard.
- **Innate resistance = unit TYPE** (v1) — the `HazardResistanceAtb` echo, innate: some types shrug some effects.
- **Researched GEAR = a component** (the real grave-rung) — "environmental hardening / mountaineering / amphibious / heat-shielding" is a unit-design component you research → build → install → **lose**, exactly as hazard armour is for ships. A unit stripped of its gear is vulnerable again.
- **Discovery + research** — reuse the space loop: encountering a new planetary hazard flavour records it in the faction's knowledge and opens the counter-research (`FactionHazardKnowledgeDB` / `HazardDiscovery` already exist — likely shared verbatim).

---

## Build plan (CI-gated slices, when we build — AFTER this design is agreed)

- **E1 — the vocabulary + region host.** Lock the data-model decision (reuse `HazardEffect` vs parallel), add the region-hosted typed-effect layer, persistent. Gauge: a region round-trips its effects.
- **E2 — the physics→environment GENERATOR (the intelligence).** `PlanetEnvironmentProfile` reads the scalars above and emits the environment list per region, **gas-giant-gated**. Defensive/idempotent like `PlanetRegionsFactory`. Gauge: a scorching world gets fire/thermal, a sulphur world gets corrosive, a **gas giant gets NONE**, Earth gets mild.
- **E3 — apply the effects.** The `GroundForcesProcessor` pushes per-tick hazard damage/drag to units in a hazardous region (mirroring `SpaceHazardProcessor`); combat/movement pull jam/cover. Gauge: a unit in a lava field bleeds health; radar-jam shrinks detection.
- **E4 — unit environmental gear (cradle-to-grave).** Type-affinity → a researched resistance component; grave-rung on loss. Gauge: a hardened unit survives where a bare one dies.
- **E5 — fix SPACE with the SAME generator.** Point the contextual profile at `StarInfoDB` to replace the flagged flat-RNG system-hazard placement — one intelligence, both hosts.
- **Transient lifecycle** — roaming fire tornadoes / passing storms, mirroring the flare grow→fade.

The **combat mechanic** (`GroundTerrain`: cover/triangle/type-affinity, 5f/5g) is independent and can land before or alongside — it *consumes* whatever a region holds, biome or fire-tornado alike.

---

## Connections (Prime Directive) + landmines

- **Hazards (`GameEngine/Hazards/`)** — the KEEP: shared effect vocabulary + counter pattern + discovery loop. **Do NOT refactor it** (it's green); reuse read-only.
- **Galaxy (`AtmosphereDB` / `SystemBodyInfoDB` / `MassVolumeDB` / `StarInfoDB`)** — the physical inputs to the generator. `AtmosphereProcessor` already computes surface temperature (Aurora formula).
- **Ground combat (`GroundTerrain` / `GroundForcesProcessor`)** — consumes the effects (combat + the per-tick applier).
- **Buildings-on-the-ground principle** — quake/seismic hazards damage a region's *buildings*, closing the loop with the locked "every buildable is a real building" rule.
- **Damage / DamageSignature keystone** — damaging ground hazards carry the same signature flavour as their space cousins, so rated armour/gear resists consistently.
- 🧨 **Landmine — the enum is append-only (gotcha #10):** any new `HazardEffectType` is APPENDED (JSON refs by int). Never reorder.
- 🧨 **Landmine — the gas-giant gate is load-bearing:** a surface hazard on a body with no surface is the bug the developer explicitly called out. Body-type gating is the first thing E2 does and the first thing its gauge checks.
- 🧨 **Landmine — don't hardcode per-planet:** environments are DERIVED from scalars; a hand-authored list is only for authored worlds, never the procedural default.

---

## THE CREATIVE SWEEP (2026-07-04) — go wild, but let physics place it

The developer's note: *"this is sci-fi — I gave you a few ideas; get creative about kinds I'm not even thinking about."* The gold isn't a longer weather list — it's **six meta-mechanics a planet can have that a patch of space can't**, because a world has *global structure*. Design toward these; individual hazards hang off them.

### The six meta-mechanics (the real creative leap)

1. **Whole-planet STRUCTURE dictates the battlefield.** A **tidally-locked** world (close orbit + slow spin) is a permanent-day inferno on one face, a frozen dark on the other, and a single survivable **twilight terminator ring** between — so the *entire war* is forced into that band. A **rogue/starless** world is eternal night + deep cold everywhere. The planet's global nature *is* the strategy, before a shot is fired. (Driver: rotation vs orbit; stellar distance.)

2. **Hazards that MOVE across the map.** A superstorm, a firestorm front, a pyroclastic surge, a migrating dust wall — it crosses region to region on a track you can predict. You *time and route* around it, or get caught. It's the roaming solar-flare, on the ground, and it makes the map a live thing. (Driver: atmosphere + heat gradients.)

3. **Hazards that come and go with the ORBIT (seasons).** An **eccentric-orbit** world swings scorching→frozen over its year; whole hazard sets appear and vanish. Invade in the mild season, or exploit the terrible one to soften a defender. A **flare-star** or **pulsar** world gets surface-radiation storms *timed to the star* — ground weather driven by the star itself (couples the two scales). (Driver: orbital eccentricity; the primary star's behaviour.)

4. **The MAP ITSELF changes.** **Shifting dune-seas** reshape which regions border which and how long a crossing takes — the region graph's `CrossingTimeSeconds` and even adjacency drift over time. A **tidal-flood** moon periodically drowns its coastal regions (impassable pulses). The battlefield you planned for is not the one you fight on. (Driver: dry+windy; a close massive primary → tides.)

5. **The PLANET fights back (living worlds).** A biosphere is a de-facto THIRD combatant: **spore blooms** (attrition + sensor-fouling), **megafauna migration corridors** that overrun a region, **predator swarms** that eat stragglers, **carnivorous/creeping jungle** that reclaims your base. A "tree-hugger" faction doesn't just *defend* an environment — it can *cultivate the world as a weapon*. (Driver: `SupportsPopulations` + temperate/wet → a biosphere.)

6. **The world drives units MAD or BLIND, not just dead.** Not everything is damage: **magnetic-anomaly / aurora storms** scramble electronics (sensor + comms, not hull). **Refractive mirage plains** (extreme thermal layering) make sensors *lie* — phantom contacts, wrong ranges. **Neurotoxin pollen / hallucinogen flora** degrade unit effectiveness (a debuff). **Electrostatic levitated dust** (airless + solar wind) fouls equipment. Information and morale are attack surfaces, not only health. (Driver: magnetosphere; thermal layering; biology; airless+charged.)

### Expanded exotic catalog (hangs off the meta-mechanics; all data, physics-placed)

Grouped by physical driver — the engine reads the field, picks from the group. (`⟶` = typed effect(s); shared `HazardEffectType` vocabulary.)

- **Extreme heat / stellar** — fire tornadoes ⟶ Heat+Drag · molten/glass plains ⟶ Heat+Drag · **glass rain / hypersonic molten-silicate wind** (a real exoplanet obs.) ⟶ Kinetic+Heat · **terminator inferno** (tidal-lock day face) ⟶ escalating Heat · **sublimation fog** (icy world nearing star) ⟶ Jam+Drag, seasonal · flare/pulsar **surface-radiation storms** ⟶ Radiation, timed to the star.
- **Cold / cryo** — cryostorms/ice sheets ⟶ cold+Drag · **cryovolcanism / ice geysers** ⟶ cold+Kinetic · **superfluid/hydrocarbon lakes** (Titan) ⟶ Drag+cold, flammable · **exotic-ice frost** (helium/neon) on ultra-cold worlds.
- **Atmosphere / chemistry** — corrosive superstorm ⟶ Corrosive+Drag · acid rain ⟶ Corrosive · toxic fog ⟶ Corrosive+Jam · **oxygen firestorms** (O-rich → anything sparks) ⟶ Heat, chain-reactive · **methane rain + spark = firestorm** · **diamond/carbon rain** (carbon-rich high-P) ⟶ Kinetic · **metal-vapour smog** ⟶ Corrosive+Kinetic · **superrotating hurricane bands** ⟶ Drag + *pushes units between regions*.
- **Magnetism / radiation / EM** — radiation zones ⟶ Radiation · **aurora/substorm electronic scramble** ⟶ EM+Jam · **chaotic magnetic-anomaly fields** ⟶ heavy Jam (sensors useless) · **EMP thunderstorms** ⟶ EM to sensor/comm *components* · **cosmic-ray sleet** (airless, no field) ⟶ steady Radiation everywhere.
- **Gravity / tidal** — high-g drag ⟶ Drag · **tidal-flex volcanism** (a moon kneaded by its gas-giant primary, Io) ⟶ Heat+quakes, *driven by the moon's primary not the star* · **black-hole/neutron-star tidal gradient** ⟶ Gravimetric · **low-g levitated regolith** ⟶ Jam+Drag.
- **Geology / surface** — quake/seismic zones ⟶ periodic damage to **buildings** · geyser fields ⟶ Kinetic+Heat pulses · **karst/sinkhole collapse** ⟶ Drag + building damage · **tar pits / quicksand** ⟶ extreme Drag, swallows stragglers · **crystalline/silicate forests** ⟶ Drag+Kinetic · **shifting dune-seas** ⟶ *reshape the map* · **natural-reactor mineral fields** ⟶ Radiation + a resource.
- **Biology (living worlds)** — spore blooms ⟶ attrition+Jam · predator swarms ⟶ attrition · megafauna corridors ⟶ transient damage · creeping/carnivorous jungle ⟶ Drag + reclaims buildings · neurotoxin flora ⟶ *effectiveness debuff*.

### Design rule for the sweep

Every one of these is **still just a list of typed effects + a physics rule** — so the engine stays a small generic core and the catalog grows as *data*. The creativity lives in (a) the physics→environment rules and (b) the six meta-mechanics; the *effects* reuse the shared vocabulary. **A hazard we haven't imagined yet is a new data row, never new engine code.** That is what keeps "get creative" from becoming "rewrite the engine every time."
