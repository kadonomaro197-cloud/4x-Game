# Environment & Battle Conditions — how the surroundings shape a fight

**As of 2026-08-05.** Status: **DESIGN + a live model in the resolver sim.** The record half (how the game reads
environments today) is verified against source at file:line by a 5-agent survey. The build half (wiring it into the
real auto-resolver) is a proposal with a named seam, not yet in the engine.

---

## What this is, and why it matters (read this first)

A battle is never fought in a featureless void. A fleet brawling **inside a nebula** can't see far and its beams
scatter. Marines fighting on an **airless moon** die without sealed suits. Tanks own an **open plain** and die in a
**jungle**. Right now Pulsar4X models a lot of this *environment* — but almost none of it reaches the part of the
game that decides who wins a fight.

This doc does three things:

1. **Records** exactly how the game represents environments today, planet-side and space-side, down to the file and
   line — so nobody has to re-derive it.
2. **Names the one honest gap:** the space combat resolver is **environment-blind** — it reads no terrain, no hazard,
   no planet, nothing about where the fight is happening. Ground combat, by contrast, already reads its surroundings.
3. **Proposes the fix** as one simple idea — *an environment is a bundle of multipliers on the shared combat math* —
   and **builds it live in the resolver sim** (`docs/combat/resolversim.html`) so you can pick an environment and
   watch the whole battle change. Thirty of them, space and surface, each effect graded honestly against whether the
   engine actually does it.

The mental model, in one line: **an environment is to a battle what weather and terrain are to a real engagement —
it doesn't fire a shot, but it decides which weapons work, who sees whom first, and where the fight happens.**

---

## The unifying idea — a "conditions" struct on the shared kernel

The engine already has a vocabulary for environmental effects. It's the `HazardEffectType` enum
(`Hazards/HazardEffect.cs:16-43`) — the same list a space hazard or a planet's weather is built from:

> `HeatDamage · RadiationDamage · KineticDamage · SensorJam · MovementDrag · WarpInhibit · CorrosiveDamage · EMDamage · GravimetricDamage · Vacuum · ToxicAtmosphere`

Read those as combat levers and the whole design falls out:

| Engine effect kind | What it does to a fight | The combat lever |
|---|---|---|
| **SensorJam** | you can't see as far | **detection / engagement range ×** |
| **MovementDrag** | thrust bites less | **closing speed ×** |
| the six **…Damage** kinds | steady attrition, armour-resisted | **ambient damage-over-time** |
| **Vacuum / ToxicAtmosphere** | ground troops asphyxiate/corrode unless sealed | **ground attrition** (already live) |
| terrain **cover** (Open/Cover/Rough) | harder or easier to hit | **evasion +** |

So an environment is nothing exotic. It's a **named bundle of multipliers** — detection, cover, damage-by-weapon-
nature, shields, point-defense, closing speed, and an ambient DoT — that feeds the **same** damage math a clean fight
uses. Fight in a nebula and the bundle says "see 45% as far, beams do 82%, everyone drags at 60% speed." That's the
whole model. The sim implements exactly this struct.

Every effect below is graded three ways, and the grade is the honest part:

- **🟢 LIVE** — the engine already applies this exact effect, at a cited file:line.
- **🟡 DATA** — the data (or the JSON authoring path) exists; the **resolver** just doesn't read it yet.
- **🔵 THEORY** — a creative extrapolation with no engine data behind it. Flagged, never faked.

---

## Part 1 — How the game reads environments TODAY (the record)

### 1a. Planet-side: rich data, almost none of it reaches combat

A planet already knows itself in fine detail. Two DataBlobs hold the physics:

**`AtmosphereDB`** (`Galaxy/AtmosphereDB.cs`) — the air and water:
`Pressure` (atm, `:17`) · `Hydrosphere` (bool, `:23`) · `HydrosphereExtent` (0–100 %, `:29`) · `SurfaceTemperature`
(°C, `:60`) · `Composition` (gas → partial pressure, `:67`).

**`SystemBodyInfoDB`** (`Galaxy/SystemBodyInfoDB.cs`) — the body itself:
`BodyType` (`:26`) · `Albedo` (`:33`) · `Tectonics` (`:41`) · `AxialTilt` (`:49`) · `MagneticField` (µT, `:57`) ·
`BaseTemperature` (`:66`) · `RadiationLevel` (`:73`) · `AtmosphericDust` (`:80`) · `LengthOfDay` (`:100`) ·
`Gravity` (m/s², `:107`).

**Terrain** is separate geography: a region carries `RegionFeatureType`s (Ocean/Forest/Jungle/Desert/Mountains/…,
`Galaxy/PlanetRegionsDB.cs:16-20`), classified for combat into three `GroundTerrainClass`es — **Open / Cover /
Rough** (`GroundCombat/GroundTerrain.cs:7`).

**What ground combat actually reads** (this is the important part):

1. **Terrain — read every combat tick (🟢 LIVE).** `GroundTerrain.Classify(region)` at
   `GroundForcesProcessor.cs:373`, feeding three multipliers:
   - **Cover → defense** — Open ×0.9, Cover ×1.25, Rough ×1.5 (incoming damage is *divided* by this;
     `GroundTerrain.cs:114-122`).
   - **Unit-type affinity → attack** — Armor loves open (×1.3) and hates rough (×0.7); Artillery loves rough (×1.3);
     Infantry is even (`GroundTerrain.cs:82-93`).
   - **March cost** — Open ×1, Cover ×1.5, Rough ×2.5; Ocean is impassable (`GroundForcesProcessor.cs:196,718`).
2. **Dynamic hazards → attrition — the ONE physics wire (🟢 LIVE).** A per-region hazard list, `PlanetEnvironmentsDB`,
   is read at `GroundForcesProcessor.cs:225-236`: `unit.Health -= env.Magnitude × (dt/3600) × (1 − resist)`. This is
   the only place a planet's environment directly changes a unit's health. Resistance comes from the **sealing
   chain** — `GroundSealAtb.Sealing` → `EnvironmentalResistance{Vacuum, ToxicAtmosphere}` → `GroundUnit.EnvResistance`
   (`GroundUnitAssembly.cs:311-314`; `GroundForcesDB.cs:640`) — a sealed unit shrugs off vacuum and toxic air. That
   sealing is a **component** you design, build, install, and can lose: cradle-to-grave, exactly right.

**But that hazard list is a baked snapshot.** A generation-time factory, `PlanetEnvironmentFactory.BuildEnvironments`
(`:66-106`), reads just **five** scalars (temperature, pressure, hydrosphere, composition, tectonics) once at
system-gen and bakes a fixed hazard list:

| Condition | Hazard emitted | Effect · magnitude |
|---|---|---|
| temp > 400 °C | Fire Tornadoes | HeatDamage 20–50/hr |
| temp < −120 °C | Cryostorms | HeatDamage 15/hr |
| corrosive gas present | Corrosive Superstorm | CorrosiveDamage 25/hr |
| tectonically active | Ash Storm | SensorJam 0.5 |
| has air & hydro < 10 % | Dust Storm | SensorJam 0.4 |
| pressure > 5 atm | Lightning Superstorm | SensorJam 0.6 |
| airless (pressure ≤ 0.05) | Vacuum Exposure | Vacuum 3.0 |
| else corrosive air | Toxic Atmosphere | ToxicAtmosphere 3.0 |

**The gaps (🟡 DATA — exists, unread):** `Gravity`, `RadiationLevel`, `MagneticField`, `Albedo`, `LengthOfDay`,
`AxialTilt`, `AtmosphericDust` are read by **nothing in combat** — not even the hazard factory. The sharpest is
**radiation**: `RadiationDamage` is a perfectly valid attrition effect, and orbital bombardment even *raises* a
planet's `RadiationLevel` (`DamageProcessor.cs:304-310`) — but the factory never emits a RadiationDamage hazard, so a
planet's radiation produces **zero** combat effect today. **Gravity** is the other: there is no gravity-tolerance
component for ground units at all, so a high-gravity world fights identically to a low-gravity one.

### 1b. Space-side: one component, a typed effect list, and a live coupling to sensors

Everything in space is **one** DataBlob — `SpaceHazardDB` — whose behavior is a **typed list of `HazardEffect`s**
(data, not hardcoded knobs). Six flavors are built by `SpaceHazardFactory`, plus an open JSON authoring path:

| Hazard | Effects | file:line |
|---|---|---|
| **Gas cloud** | Corrosive 50 J/s · SensorJam ×0.35 · MovementDrag ×0.5 · WarpInhibit ×0.25 | `SpaceHazardFactory.cs:59-69` |
| **Star corona** | Heat 800 J/s, proximity-scaled (∝1/dist²) | `:75-89` |
| **Debris field** | Kinetic 60 J/s · MovementDrag ×0.6 | `:97-105` |
| **Ion storm** | EM 40 J/s · SensorJam ×0.4 | `:111-119` |
| **Gravimetric anomaly** | Gravimetric 70 J/s · WarpInhibit ×0.3 | `:126-134` |
| **Solar flare** (transient) | Radiation 500 J/s · SensorJam ×0.0 (full blind) · grows/fades | `:140-149` |
| **JSON-authored** ("nebula", "radiation belt", any region) | any effect list from `SystemBlueprint.Hazards` | `CreateFromEffects`, `:22-52` |

**How they bite (🟢 LIVE):** `SpaceHazardProcessor` (a 5-second hotloop) walks every hazard, tests each ship's
distance (`dist > radius → skip`), and for those inside:
- **Damage kinds** are pushed through the **same** `DamageProcessor.OnTakingDamage` path as a weapon hit — so a
  ship's **armour material** is the defense (`SpaceHazardProcessor.cs:91-110`).
- **MovementDrag** scales the ship's `NewtonMoveDB` velocity (`:113-122`).
- **SensorJam** and **WarpInhibit** are pulled query-time by the systems that care.

**The coupling that already exists (🟢 LIVE and important):** detection already reads the environment.
`SensorScan.cs:83` calls `SpaceHazardTools.CombinedAt(position)`; a gas cloud's SensorJam ×0.35 **cuts** how far you
can see (`:114-129`), and a solar flare's ×0.0 **blinds you entirely** (`:117-120`). So "you can't see far in a
nebula" is not a proposal — it is **shipped behavior**. It's resisted by a **`HazardResistanceAtb`** component
(`HazardResistanceAtb.cs:20-51`) — health-scaled (a blown-off module stops resisting) and clamped so a hazard always
bites at least 10 %. Again: a real cradle-to-grave component.

**Absent as data (would be authored or built):** a "nebula" as a distinct type (author it as a Generic JSON hazard),
asteroid-field *density* (belts are scattered individual asteroid bodies, no region object), radiation belts,
jump-point turbulence, a deep-space-vs-in-system distinction, and solar wind (no matches anywhere).

---

## Part 2 — How it reads into the auto-resolver (the seam)

Here is the load-bearing finding, verified by grepping the whole `Combat/` directory:

> **Space combat is environment-blind. Ground combat is environment-aware.**

The space resolver (`CombatEngagement`, `CombatKernel`, `AutoResolve`, `ShipCombatValueDB`) reads **no** terrain,
hazard, atmosphere, gravity, or body — zero hits. Its only strength inputs are firepower, toughness, evasion,
doctrine, commander, caliber, and the shield/ammo/heat/PD pools. A fleet fighting in a killing nebula resolves
**identically** to one in clean deep space.

The resolver *could* know where it is — cheaply. A battle already holds the **star system** (the manager it ticks)
and each fleet's **absolute position** (`CombatEngagement.cs:2019-2026`). The body-classification helpers all exist:
`OrbitProcessor.FindSOIForPosition(system, pos)` (`:134`, the true "closest body"), `SystemBodyInfoDB.BodyType`,
`AtmosphereDB`, `StarInfoDB`. The ground side already does exactly this shape at `PlanetEnvironmentFactory`. What's
**missing** is only: the wire (no combat code calls those helpers), a "combat environment" field on
`FleetCombatStateDB`, and — the one genuine build — a **belt-region** object to test "am I inside the asteroid belt."

### Where the multipliers attach (the exact seams)

| Lever | Space seam (net-new) | Ground seam (extends existing) |
|---|---|---|
| **Detection / range** | `WithinWeaponRange` `CombatEngagement.cs:1273`; `FleetSeparation :1244` | terrain impassability in pathing |
| **Firepower** | `:759` — `dmgThisSalvo = TotalDamage(incoming) * dt * SalvoDamageScale` → `* conditions.Firepower` (**the single cleanest space hook** — one scalar per defending fleet) | attack pool `:487-491` / `:520-524` |
| **Evasion / hit** | the **shared kernel** `CombatKernel.HitFraction` `CombatKernel.cs:196` (`:214-217`) | same kernel, direct at `GroundForcesProcessor.cs:423` |
| **Shield** | `ApplyShield :764` (`:1605,:1610`) | inline shield drain `:426-431` |
| **Armour** | `:770-771` | `GroundDamageMatrix.ArmourSoak :437` |
| **Closing** | `AdvanceClosing :1055` | march cost `:196,:213` |
| **Attrition (DoT)** | (none today) | `:236` — the live hazard wire |
| **Defense/cover** | (none today) | `coverFort :376-377` |

**The keystone:** `CombatKernel.HitFraction` (`CombatKernel.cs:196`) is the **one function both resolvers call** — the
ship path reaches it via `LandedFraction → ApplyCasualties:897`, the ground path calls it directly at `:423`. A single
accuracy/visibility coefficient added there (smoke, atmosphere, nebula, dust → a worse hit chance) **moves both
theaters with one edit.** Firepower, heat, and retreat are *not* in the shared kernel — those are computed inside each
resolver, so a firepower or attrition coefficient must be applied at each resolver's own scalar (or, the cleaner
long-term path, threaded onto the `CombatKernel.Combatant` view once that has a live consumer).

This is why the design is honest about effort: on the **ground** side, an environment coefficient is an *extension of
multipliers already there*. On the **space** side, it is a *net-new term* — because space combat reads nothing today.

---

## Part 3 — The environment catalog (record + theorize)

Thirty environments, live in the sim (`docs/combat/resolversim.html` → the **Environment** selector). Each is a
condition-multiplier bundle; each effect is graded 🟢 LIVE / 🟡 DATA / 🔵 THEORY. Multipliers are × (1.0 = no change);
"cover" is additive evasion.

### Spaceside (14)

| Environment | The idea | Key multipliers | Grade of the effects |
|---|---|---|---|
| **Open space** | the clean vacuum baseline | all 1.0 | 🟢 the resolver's only real state today |
| **Nebula / gas cloud** | can't see far, beams scatter, gas corrodes → knife fight | detection ×0.45 · energy ×0.82 · closing ×0.6 · PD ×0.72 · Corrosive DoT 50 J/s | 🟢 detection, closing, DoT · 🔵 beam-scatter/PD |
| **Stellar corona** | heat chokes beams & shields; searing DoT | energy ×0.62 · shield cap ×0.72 · regen ×0.5 · Heat DoT 800 J/s | 🟢 Heat DoT · 🔵 heat-throttle |
| **Debris field** | dodge in the clutter, grit peppers hulls | cover +18 % · closing ×0.6 · Kinetic DoT 60 J/s | 🟢 closing, DoT · 🔵 cover |
| **Ion storm** | guidance fries, shields won't recharge → dumb-fire wins | guided reach ×0.5 · explosive ×0.6 · regen ×0.4 · detection ×0.5 · EM DoT 40 J/s | 🟢 detection, DoT · 🔵 guidance/regen |
| **Gravimetric anomaly** | missiles bend & miss; no warp exit | guided reach ×0.55 · explosive ×0.7 · Gravimetric DoT 70 J/s | 🟢 DoT + WarpInhibit exit · 🔵 course-bending |
| **Solar flare** (transient) | everyone blind, radiation bath → point-blank chaos | detection ×0.15 · cover +25 % · PD ×0.4 · Radiation DoT 500 J/s | 🟢 full-blind, DoT · 🔵 blind-spread |
| **Radiation belt** | steady radiation, degraded electronics | explosive ×0.85 · regen ×0.7 · Radiation DoT 220 J/s | 🟡 authorable JSON · 🔵 electronics |
| **Asteroid belt** | superb cover, poor sightlines, collisions | cover +22 % · detection ×0.7 · closing ×0.7 · Kinetic DoT | 🟡 belt has no region object · 🔵 the fight-in-it model |
| **Planetary orbit** | planet-limb cover; opens orbital bombardment | cover +8 % · detection ×0.85 | 🟢 bombardment path · 🟡 knowable · 🔵 limb cover |
| **Gas-giant upper atmosphere** | thick screaming medium; only close brawls | guided reach ×0.4 · kinetic ×0.7 · closing ×0.5 · Heat DoT | 🟡 body known, no hazard · 🔵 the whole drag model |
| **Deep space** | no stellar glare → see farther, shoot first | detection ×1.25 | 🔵 the inverse of an explicit TODO |
| **Jump-point turbulence** | arrive disoriented → confused scramble | detection ×0.6 · cover +15 % · PD ×0.6 | 🟡 jump points carry no hazard · 🔵 disorientation |
| **Pulsar / magnetar wind** | guidance annihilated, beams don't care | guided reach ×0.3 · explosive ×0.4 · regen ×0.3 · Radiation DoT | 🔵 pure theory — no object exists |

### Planetside — surface theater (16)

*Shown in the sim on the shared kernel; in-engine these route through `GroundForcesProcessor`, and their unit-type
affinities (Armor/Artillery/Infantry) apply to ground units.*

| Environment | The idea | Key multipliers | Grade |
|---|---|---|---|
| **Temperate surface** | the Earth-like baseline (Open terrain) | all 1.0 | 🟢 baseline |
| **Airless (vacuum)** | unsealed troops asphyxiate; ballistics carry flat | Vacuum DoT · kinetic ×1.1 · guided reach ×1.1 | 🟢 vacuum attrition + sealing · 🔵 ballistics |
| **Toxic atmosphere** | the air eats the unsealed | ToxicAtmosphere DoT | 🟢 attrition + sealing |
| **Fire tornadoes / molten** | steady burn; beams struggle to shed heat | energy ×0.8 · Heat DoT | 🟢 Heat attrition · 🔵 weapon throttle |
| **Cryostorms / frozen** | cold seeps in, slows everything | closing ×0.8 · Heat DoT | 🟢 cold attrition · 🔵 mobility |
| **Dust storm** | grit kills sightlines | detection ×0.6 · cover +12 % | 🟡 SensorJam generated, not consumed by the ground resolver |
| **Lightning superstorm** | EM crash + dense-air drag | detection ×0.5 · guided reach ×0.8 · kinetic ×0.9 | 🟡 generated · 🔵 EM/drag |
| **Ash storm** | volcanic grey-out | detection ×0.55 · cover +10 % | 🟡 generated, not consumed |
| **High gravity** | crawl, arcs fall short — heavy machines win | closing ×0.7 · guided reach ×0.75 · kinetic ×0.9 · cover −5 % | 🟡 gravity stored · 🔵 **THE sharp gap — combat reads none of it** |
| **Low gravity** | bounding mobility, flat long shots | closing ×1.3 · guided reach ×1.25 · cover +10 % | 🟡 gravity stored · 🔵 unread |
| **Dense jungle** (Cover) | infantry thrive, armour bogs | cover +20 % · closing ×0.66 · kinetic ×0.9 | 🟢 cover + unit-affinity + march, all live |
| **Mountains** (Rough) | artillery lords it, armour claws | cover +30 % · closing ×0.4 | 🟢 cover + artillery/armour affinity + march |
| **Open plains** | tank country, nowhere to hide | cover −5 % · kinetic ×1.1 | 🟢 open cover + armour affinity |
| **Irradiated surface** | unshielded troops sicken | explosive ×0.9 · Radiation DoT | 🟡 RadiationLevel stored & raised by bombardment · 🔵 **factory never emits it → zero effect today** |
| **Urban / dense ruins** | the meat-grinder; infantry king | cover +28 % · closing ×0.6 · kinetic ×0.85 | 🔵 no urban terrain type exists |
| **Long night / polar dark** | the side that sees in the dark wins | detection ×0.7 · cover +10 % | 🟡 LengthOfDay stored · 🔵 unread |

---

## Part 4 — The build sequence (how to make it real, cradle-to-grave)

The sim proves the model; here's how it earns its place in the engine, smallest-first. Each step is a real decision
the player makes or feels, not an abstract flag.

1. **A `CombatConditions` struct + a reader.** Mirror `PlanetEnvironmentFactory` one level up: given a battle's system
   + fleet position, call `FindSOIForPosition` + `SpaceHazardTools.CombinedAt` + `SystemBodyInfoDB`/`AtmosphereDB` to
   fill a small `{Firepower, Detection, Cover, ShieldRegen, Closing, DoT…}` struct, cached per battle. This is the
   whole "wire" — the gap identified in Part 2. Store it on `FleetCombatStateDB`.
2. **The shared-kernel hook first.** Add a visibility/accuracy coefficient to `CombatKernel.HitFraction` (`:196`).
   One edit moves **both** ship and ground resolution — the highest-leverage line in the design.
3. **The space firepower + shield hooks.** Multiply `conditions.Firepower` at `CombatEngagement.cs:759` and scale
   `capacity`/`regen` in `ApplyShield` (`:1605-1610`). Now space combat is no longer environment-blind.
4. **Close the two sharp data gaps** (they're free — the data's already there): teach
   `PlanetEnvironmentFactory` to emit a **RadiationDamage** hazard from `RadiationLevel`, and add a **gravity**
   read to ground movement/attack. Both are 🟡 DATA today; both become 🟢 LIVE with a few lines.
5. **Authorable environments via JSON.** A "nebula", a "radiation belt", a named region — all already flow through
   `SpaceHazardFactory.CreateFromEffects` with **zero new C#**. This makes the catalog content-driven: modders and
   the galaxy generator add environments as data.
6. **The one genuine build: a belt-region object,** so "am I fighting *inside* the asteroid field" is a cheap test
   rather than a scan of scattered rocks.

The counter is already a component the player owns: `HazardResistanceAtb` (space) and `GroundSealAtb` (surface) are
researched, built, installed, and lost — so **surviving an environment is a design decision**, exactly the
cradle-to-grave chain the project's law requires. Environment *tolerance* is a thing you build; environment
*exploitation* (fight where your weapons work and theirs don't) is a thing you maneuver into. Both are player
decisions, which is the whole point.

---

## The honest headline

- The game already **models** environments richly — a planet's full physics, six space-hazard flavors, terrain, and a
  real component-based resistance chain.
- **Ground combat already fights in its environment** (terrain + hazard attrition — 🟢 live).
- **Space combat ignores its environment entirely** — the single biggest gap, and the one the shared-kernel hook
  closes cheapest.
- The sim now lets you **see** all thirty conditions reshape a battle, with every effect labeled by whether the
  engine truly does it (🟢), could with a wire (🟡), or is a creative reach (🔵).

*Companion: the live model is in `docs/combat/resolversim.html` (published as the **Resolver Simulator — the Arena**
artifact). The source ledger this doc is built from lives in the 2026-08-05 five-agent survey. Grounding for the
resolver seams: `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md`.*
