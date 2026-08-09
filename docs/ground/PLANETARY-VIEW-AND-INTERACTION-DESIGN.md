# Planetary View & Interaction — the surface as a board of environments

**As of 2026-08-09 (rev. B — re-grounded on real game data).** Status: **DESIGN STUDY (design-only).** No engine code,
no CI. This doc + its interactive prototype (`docs/ground/planetview.html`) answer one question the developer put on the
table:

> *"design the way we view and interact with planets in general, and connect it to the auto-resolver and the
> different settings"* — **followed by the binding constraint: *"these planet views [must] be based exclusively by
> rules and data taken straight out the game so we know exactly what we're working with."***

The headline, in one sentence: **a planet is not a dimensionless dot — it is a board of environments, and the square
of ground you choose to fight on IS the setting the auto-resolver reads.** You don't pick a battle's environment from a
menu; the *location picks it for you*, and your only environment decision is **where you commit**.

> ### ⚠ REV-B correction — this doc's first cut invented numbers; this cut does not
> The first version of this doc (and prototype) showed a rich stack of environment *multipliers* — low-g `closing ×1.3`,
> dust `range ×0.6`, and so on — described as "sim-proven." **The developer's ground-truth mandate exposed that as
> over-reach: those multipliers are a *design proposal* authored in `resolversim.html`, not what the engine does.** This
> revision rebuilds everything from source: the worlds are the **real Sol bodies** (their gravity/atmosphere/temperature
> read from the JSON data files), the terrain numbers are the **exact values the ground resolver applies**
> (`GroundTerrain.cs`), and the world hazards are what the **physics-driven generator actually produces**
> (`PlanetEnvironmentFactory.cs`). Where the engine does *nothing* with a value, this doc now says so. The three-state
> grade (LIVE / DATA / THEORY) is the honesty spine — see §5.

**Companions (read alongside):**
- `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` — THE surface board this view sits on (region ring → global cylinder →
  mini/city hex). This doc is the **front door and the interaction**; that doc is the **board**.
- `docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` — the env-effects catalog + the LIVE/DATA/THEORY grading, and (its own
  headline) the honest gap: **the resolver is environment-blind; the rich multiplier model is an accepted hook, not
  wired.** This doc's THEORY column IS that unwired proposal.
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` — the resolver anatomy (§11 the north star; §12 the ground fight).
- **Verified source (rev-B ground truth):** `sol/*.json` (bodies) · `GroundCombat/GroundTerrain.cs` (terrain dials) ·
  `GroundCombat/PlanetEnvironmentFactory.cs` (hazard generation) · `GroundCombat/GroundForcesProcessor.cs:220-238`
  (attrition application) · `GroundCombat/GroundSealAtb.cs` (the seal) · `Hazards/HazardEffect.cs` (effect vocabulary).

---

## 1. Why this matters (read this first, in plain English)

Right now a planet in Pulsar4X is closer to a *number* than a *place*. It has a population, some buildings, an owner —
but combat that happens "there" has never really cared *where* on the surface it happened. That's the gap.

Think of it the way a navigator thinks about a chart. The ocean isn't one flat blue field — it's shoals here, deep
water there, a storm rolling through, a coastline that funnels traffic. **Where** you meet the enemy decides half the
fight before a shot is fired. The same is true on the ground: tanks own an open plain and die in a jungle; marines die
on an airless moon without sealed suits; a dust storm collapses your sightlines to knife range.

Pulsar4X already *models* a lot of that surface detail — gravity, temperature, atmosphere, radiation, terrain type.
The problem `docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md` names is that **almost none of it reaches the part of the
game that decides who wins a fight.** The detail is on the shelf; the combat resolver never opens the jar.

This design closes that. It does two things:

1. **A way to SEE the planet** — a zoomable surface where every square of ground carries a visible environment (its
   terrain, the planet's condition it sits under, and any weather rolling through it).
2. **A way to ACT on it** — the environment of the square you pick becomes, automatically, the environment the
   auto-resolver opens the battle with. **The map is the front door; the resolver is the room.**

The point before the plumbing: **you win by choosing your ground, not by choosing a menu option.**

---

## 2. Four zooms, one surface

The developer's own framing of the surface is **"4 big slices you can zoom into at high accuracy."** Stacked as zoom
levels, the planet reads like a set of nested charts — the strategic plot, the globe, the theatre, the city block —
each the same physical world seen closer. This is not new geometry; it's the ladder that already exists in
`GROUND-SURFACE-MAP-DESIGN.md`, named as an interaction.

| Zoom | Name | What you see & do | Build-state (honest) |
|------|------|-------------------|----------------------|
| **1** | **System** | Planets as points on the orbit map — the strategic board. Where you decide *which world*. | **ENGINE-WIRED** — the system map ships. |
| **2** | **Planet globe** | One continuous hex grid wrapping the whole world (`SurfaceGrid`), banded into the 4 regions. Terrain, ownership, weather at a glance. Where you decide *which theatre*. | **ENGINE-WIRED (G6a)** — `PlanetViewWindow.DrawGlobalHexWindow` renders the sliding cylinder window; the client is fully off the old disks. |
| **3** | **Regional / operational** ← *this study lives here* | The war layer: units move, fight, and capture on real-distance hexes. Each hex carries a **terrain** and inherits the planet's **condition**. Where you decide *which ground*. | **ENGINE-WIRED (W-track)** — units march/fight/capture per-hex; combat resolves on continuous real-metre distances. |
| **4** | **Local / city** | The build layer — each installation sits 1:1 on its own fine tile. Raze it, capture it, defend it. Where you decide *what to build*. | **PARTIAL** — W-track (war zoom) built + CI-gauged; the C-track city builder follows. |

**Why zoom 3 is the one that matters for this task.** Zoom 1 and 2 are strategic — they pick a target. Zoom 4 is
economic — it lays out buildings. **Zoom 3 is where a battle actually happens**, and therefore it's the zoom where the
"environment picks the fight" connection has to live. The prototype (`planetview.html`) is built at this zoom
deliberately: an operational hex band you click to read the fight it produces.

---

## 3. The load-bearing connection — how the ground becomes the fight (what's REAL)

This is the heart of the design, and rev-B states it honestly. The idea is unchanged; the scope is narrower than the
first cut claimed.

> **A hex hands the resolver two live things: its TERRAIN (a per-hex block of multipliers) and its world's SURFACE
> ATTRITION (a per-hour bleed on units standing there). That pair IS the ground resolver's "environment setting."**

A crucial clarification the first cut blurred: **"the auto-resolver" here means the GROUND resolver**
(`GroundForcesProcessor.ResolveRegionCombat` + its attrition step). **The SPACE resolver** (`AutoResolve` /
`CombatEngagement`) **reads nothing about location — no terrain, no hazard, no planet. It is environment-blind.** So
this whole model lives on the ground side.

The three layers, and what the engine actually does with each:

- **Terrain (per-hex, local) — 🟢 LIVE.** Each hex's dominant feature sorts into **Open / Cover / Rough**
  (`GroundTerrain.Classify`), and the resolver applies three real multipliers off that class: a **cover** divisor on the
  defender's incoming damage, a **unit-type affinity** (armour loves open, artillery loves rough), and a **march-cost**.
  Shipped and gauged — this is the layer that genuinely "bends the fight" today.
- **World condition → surface hazard — 🟢 LIVE as ATTRITION only.** A world's physics generates hazards
  (`PlanetEnvironmentFactory`): airless → **Vacuum**, corrosive air → **ToxicAtmosphere**, >400 °C → **Fire**,
  <−120 °C → **Cryo**, sulphur air → **Corrosive**. A unit standing in one **bleeds a fixed HP per hour**
  (`GroundForcesProcessor.cs:220-238`), reduced by any matching resistance. That's the whole combat effect — a bleed,
  not a reshaping of sight/closing/damage.
- **World condition → gravity / temperature / radiation / day-length — 🟡 DATA.** These are on every body (from the
  JSON) but **no combat code reads them.** Temperature matters only indirectly, by deciding which hazard was generated.

**The "weather" layer from the first cut does not exist as a live system.** There is **no transient/moving weather** in
the engine. The closest real thing is the *static* SensorJam hazards the generator makes (Dust / Ash / Lightning) — but
the attrition step skips SensorJam (it isn't a damage effect) and ground detection doesn't read it, so **they are
generated DATA that combat currently ignores.** Making them (and gravity, and temperature) reshape the fight is the
THEORY column — the accepted-but-unwired hook from `ENVIRONMENT-CONDITIONS-DESIGN.md`.

```
   The hex you pick                                 The GROUND resolver
   ────────────────                                 ───────────────────
   terrain class (this hex)  --> Open/Cover/Rough --> cover / affinity / march   (LIVE)
   world hazards (this world)--> Vacuum/Fire/... --> per-hour attrition bleed     (LIVE)
                                                     [ the SPACE resolver reads none of this ]
```

**Why the shape is still right** (and still satisfies the project's laws): the terrain block + the attrition bleed *are*
the source of a real, stacking decision — *where do I commit, and do my units need sealing to stand here?* — and the AI
reads the exact same two things off the exact same hex (**One Verb, Both Seats**). The design ambition (wire gravity →
movement, weather → detection) raises the ceiling later; it does not change today's honest floor.

---

## 4. The composition rule (how the two LIVE layers combine)

No invented multiplier bundle — the real composition is small and exact:

1. **Terrain block** (from the hex's Open/Cover/Rough class, `GroundTerrain.cs` — verbatim):

   | Class | Defender cover (÷ incoming) | Armour affinity | Artillery affinity | March cost |
   |-------|----------------------------|-----------------|--------------------|------------|
   | **Open** (plains/desert/barren/coast/ice/tundra) | ÷0.90 *(favours attacker)* | ×1.30 | ×1.00 | ×1.0 |
   | **Cover** (forest/jungle/wetland) | ÷1.25 | ×0.75 | ×0.85 | ×1.5 |
   | **Rough** (mountains/highlands/volcanic) | ÷1.50 | ×0.70 | ×1.30 | ×2.5 |

   (Infantry is ×1.00 everywhere — the neutral baseline. Ocean is **impassable**; ice is Open for combat but crossed at
   the rough march cost.)

2. **Surface attrition** (from the world's generated hazards): **sum the per-hour bleed** of every damaging hazard
   present, then subtract each unit's resistance — `Health −= Σ Magnitude × (Δt/3600) × (1 − resist)`. That is an
   *addition* of independent bleeds, exactly as the engine loops them (`GroundForcesProcessor.cs:220-238`).

**Worked example — Venus, a volcanic (Rough) hex, an unsealed unit:**
- terrain (Rough) → defender **÷1.50**, armour **×0.70**, artillery **×1.30**, march **×2.5**
- surface attrition (Venus generates Fire + Toxic; this region also has Corrosive) → Fire **23.2/hr** + Toxic
  **3.0/hr** + Corrosive **25.0/hr** = **−51.2 HP/hr**
- a **sealed** unit here still loses **−48.2/hr** — sealing only negates the 3.0 toxic bleed (see §9's real gap).
- **the read:** a fortress to *take* (rough cover), but the ground is killing everyone who stands on it — you win Venus
  fast or you don't land, and no buildable kit stops the fire. *The AI reads the identical two numbers.*

---

## 5. The environment catalog (straight from source)

Every number below is read from the game. **Grade** = what the engine does with it: **🟢 LIVE** the resolver applies it
in a fight today · **🟡 DATA** the value exists on the body but combat ignores it · **🔵 THEORY** a design proposal, not
wired anywhere. *(Independently re-verified 2026-08-09 by a 6-domain adversarial source audit — the space resolver
greps to zero terrain/hazard reads; no weather system exists; every magnitude below matches source.)*

### Terrain — 🟢 LIVE (`GroundCombat/GroundTerrain.cs`)
The full Open/Cover/Rough dial table is in §4. The `RegionFeatureType → class` mapping (from `Classify()`, verbatim):
**Rough** = Mountains / Highlands / Volcanic · **Cover** = Forest / Jungle / Wetland · **Open** = Plains / Desert /
Barren / Coast / Ocean / Ice / Tundra.

### Surface hazards — generated from physics (`GroundCombat/PlanetEnvironmentFactory.cs`)
The generator reads a body's `AtmosphereDB` (temp / pressure / hydrosphere / composition) + `SystemBodyInfoDB`
(tectonics) at world-gen and emits only hazards that fit. This is the **only** path from a body's physics to a combat
effect. Thresholds and magnitudes, verbatim:

| Hazard | Effect kind | Trigger (real rule) | Magnitude | Combat grade |
|--------|-------------|---------------------|-----------|--------------|
| **Vacuum exposure** | `Vacuum` | pressure ≤ 0.05 atm (airless) | **−3.0 HP/hr** | 🟢 LIVE (attrition; **sealed suit exempt**) |
| **Toxic atmosphere** | `ToxicAtmosphere` | has air **and** a sulphur/chlorine/acid gas present | **−3.0 HP/hr** | 🟢 LIVE (attrition; **sealed suit exempt**) |
| **Fire tornadoes** | `HeatDamage` | surface temp > 400 °C | **−(20 + (T−400)×0.05, cap 50) HP/hr** | 🟢 LIVE (attrition; **no buildable counter**) |
| **Cryostorms** | `HeatDamage` | surface temp < −120 °C | **−15.0 HP/hr** | 🟢 LIVE (attrition; **no buildable counter**) |
| **Corrosive superstorm** | `CorrosiveDamage` | a sulphur/chlorine/acid gas present | **−25.0 HP/hr** | 🟢 LIVE (attrition; **no buildable counter**) |
| **Ash storm** | `SensorJam` | tectonically active | ×0.5 sight | 🟡 DATA (generated; **combat skips SensorJam**) |
| **Dust storm** | `SensorJam` | has air **and** hydrosphere < 10% | ×0.4 sight | 🟡 DATA (generated; combat-inert) |
| **Lightning superstorm** | `SensorJam` | pressure > 5 atm | ×0.6 sight | 🟡 DATA (generated; combat-inert) |

*(The DATA line is load-bearing: SensorJam is excluded from `IsDamageEffect` — `GroundForcesProcessor.cs:1048` — so the
dust/ash/lightning storms the generator makes bleed nobody. Only Vacuum/Toxic/Heat/Corrosive attrition is LIVE.)*

### Condition readouts — 🟡 DATA / 🔵 THEORY
`Gravity` (m/s²), `SurfaceTemperature`, `RadiationLevel`, `MagneticField`, `LengthOfDay` all exist on the body but
**combat reads none of them** (gravity/pressure feed only colony population-support tolerance; radiation feeds only
ColonyCost). The first cut's low-g `closing ×1.30`, high-g drag, and the weather sight-cuts are the **🔵 THEORY**
proposal — the accepted-but-unwired hooks — shown in the prototype clearly boxed as "design proposal," never mixed into
the live numbers.

### The real Sol bodies (what the prototype ships — read from `sol/*.json`)
Applying the generator's rules to each body's authored data gives the real environment per world:

| Body | Gravity | Surface temp | Atmosphere | LIVE surface attrition (generated) |
|------|---------|--------------|------------|-------------------------------------|
| **Earth** | 1.00 g | 14.8 °C | N₂/O₂ breathable, 1 atm | *(none — only an Ash Storm, which is DATA)* |
| **Mars** | 0.38 g | −63 °C | CO₂ thin, 0.87 atm | *(none — Dust + Ash, both DATA; not cold enough for cryo)* |
| **Luna** | 0.17 g | airless (computed) | none | **Vacuum −3/hr** |
| **Mercury** | 0.38 g | airless (computed) | none | **Vacuum −3/hr** |
| **Venus** | 0.90 g | 464 °C | CO₂/SO₂ crushing, 92 atm | **Fire −23.2/hr + Corrosive −25/hr + Toxic −3/hr** |
| **Ganymede** | 0.15 g | −163 °C | trace, vacuum | **Vacuum −3/hr + Cryo −15/hr** |

The game already ships the extremes — **Venus is the hell world, Mercury/Luna the airless rocks, Ganymede the frozen
moon** — so no invented planet is needed. Earth and Mars are combat-benign (their only generated hazards are the
combat-inert SensorJam storms), itself an honest finding: *most of the Sol map fights on terrain alone today.*

**Two hexes that are NOT battles:** **ocean is impassable** (out of the movement graph — `HexPathfinder.IsImpassable`),
and **ice is passable but crossed at the rough march cost**. The prototype honours both.

---

## 6. Two settings meet on the field — environment vs. doctrine

The developer's phrasing was *"connect it to the auto-resolver and the **different settings**."* There are two kinds of
setting, and keeping them straight is what makes the connection clean:

| Setting | Whose call | Set where | What it is |
|---------|-----------|-----------|------------|
| **Environment** | The **ground's** call | The planet view (this map) — fixed by *where you fight* | the hex's terrain block + the world's surface attrition (both LIVE) |
| **Doctrine** | **Your** call | The resolver (Force Management) — your standing orders | stance (close / hold / stand-off / withdraw) + target priority |

**A battle is the region's environment ✕ your doctrine, run through the one shared combat kernel.** The environment is
handed to you by the location — you can only choose it by choosing *where to commit*. The doctrine is yours to set
freely. The planet view fixes the first; the resolver takes the second. They meet on the field.

This is why the prototype's readout ends with a **"Resolve a battle here"** button: in the full client that button
opens the ground resolver **pre-loaded with the hex's terrain block and applying its surface attrition**, and *then*
you set doctrine and watch it resolve. The environment is not a thing you configure — it's a thing you *inherit from the
ground you chose.*

---

## 7. The interaction model — what the player (and the AI) actually DO

The whole loop at the regional zoom, from both seats:

1. **Survey the theatre.** Open zoom 3 on a world you can see (survey-fog gates this — you know the ground where you
   settle or have scouted; `PlanetRegionsDB.Surveyed`). The hex band shows terrain colour per hex, region bands, and a
   ☣ marker on hexes carrying a live surface-attrition hazard.
2. **Read a hex.** Click any hex → the readout shows the two live things: the **terrain block** (cover / affinity /
   march) and the **surface attrition** (per-hour bleed, and how much a sealed unit still takes), plus a plain-English
   tactical read ("a fortress to take, but the ground is killing everyone — win fast or don't land").
3. **Choose your ground.** The decision the whole view exists to serve: *where do I commit, and is my kit good enough to
   stand here?* A defender picks the rough hex that quadruples their cover; an attacker avoids the fire region and lands
   where the terrain — and the bleed — cost them least.
4. **Commit → resolve.** March a force to the hex (the live per-hex movement), and when it meets an enemy in weapons
   range, the **ground** resolver opens with **that hex's terrain block** and applies **that hex's attrition** each tick,
   plus your doctrine.
5. **The AI does all of 1–4 with the same primitives.** It reads the same terrain block + attrition off the same hexes,
   scores ground the same way, and issues moves through the same order queue. *If it couldn't, the mechanic would be
   too complex by the project's own law* (`CLAUDE.md` "One Verb, Both Seats"). The environment being a property of the
   *ground* — not of a UI panel — is exactly what keeps both seats able to drive it.

**What this view is NOT:** it is not a new order surface. Per the M9 ruling, orders come from Force Management, not
scattered onto the planet view. The planet view's job is to **show the ground and let you read the fight**; committing a
force is the existing movement order, and the environment rides along for free because it's baked into the destination
hex.

---

## 8. Cradle-to-grave & the connection map (the Prime Directive check)

**What feeds INTO this view:**
- `PlanetRegionsDB.SurfaceGrid` (the global cylinder hex grid) — terrain per hex. **ENGINE-WIRED.**
- `PlanetEnvironmentsDB` (per-region generated hazards) — the surface attrition, from `PlanetEnvironmentFactory` reading
  the body's `AtmosphereDB` + `SystemBodyInfoDB` at world-gen. **ENGINE-WIRED.**
- The body's condition data — gravity/temp/radiation/day from `AtmosphereDB` / `SystemBodyInfoDB`. **DATA** (exists;
  combat reads none of it directly — the honest gap).
- Per-hex hazards (ruling M11) + transient weather — **not built** (hazards are per-region; no weather system exists).

**What this view feeds INTO:**
- The **ground** resolver's environment: the terrain block (`ResolveRegionCombat`) + the attrition step
  (`GroundForcesProcessor.cs:220-238`). **Both LIVE.** The rich-multiplier reshaping is the THEORY proposal, unwired.
- Nothing feeds the **space** resolver — it is environment-blind (verified: zero `.cs` reads of terrain/hazard/planet).

**What it shares STATE with:**
- `PlanetViewWindow` (the live globe client) — same `SurfaceGrid`, same regions, same units. This view is a *zoom* of
  that window, not a parallel map.
- The ground movement/combat system (`GroundForcesProcessor`) — same hexes units already march and fight on.

**What it TRIGGERS:**
- A battle resolution that reads the hex's terrain block and applies its surface attrition. (Trigger is the existing
  movement-into-range, not a new order.)

**The cradle-to-grave chain for the connection** (mineral → … → decision → loss):
- The **terrain** rung is fully live: it's on the map, it's researched into no component (it's just ground), and it
  already shapes a fight.
- The **condition** rung is **partly live**: the surface attrition already runs, and a unit's answer to it is a
  **component** — the `sealed-systems` fit that zeroes vacuum/toxic bleed is **built and wired** (cradle-to-grave:
  research → build → mount → lose → re-exposed). The **missing rung** is a heat/cryo/corrosion-hardening component — the
  `EnvironmentalResistance` map supports those kinds, but no buildable part writes them, so fire/cryo/corrosive worlds
  are un-counterable (§11 Q4). Building that one component closes the loop.
- The **gravity/temperature/radiation** readouts are pure DATA — combat reads none of them; wiring them is THEORY.
- The **weather** rung is design-only end to end (no transient weather system; the cheapest path is to wire the already-
  generated SensorJam storms — §11 Q3).

---

## 9. Build-state honesty — what's real, what's proposed, the real gap

Straight about the layers, re-grounded in source. What the engine does today is **more** than the first cut credited on
the terrain/attrition side and **less** on the rich-multiplier side.

- **🟢 LIVE — already wired into the ground resolver.** Two things: (1) the **terrain block** — Open/Cover/Rough sets a
  cover divisor, a unit-type affinity, and a march cost (`GroundTerrain.cs`, read by `ResolveRegionCombat`); and (2) the
  **surface attrition** — a unit standing in a generated Vacuum/Toxic/Fire/Cryo/Corrosive hazard loses a fixed HP/hour
  (`GroundForcesProcessor.cs:220-238`), a sealed unit exempt from Vacuum/Toxic. These are shipped and gauged. *You can
  fly the zoom-2/zoom-3 view and fight on this terrain + attrition in the real game today.*
- **🟡 DATA — the value exists on the body, combat ignores it.** Gravity, temperature, radiation, magnetic field,
  day-length are all read from the JSON and stored, but **no combat code reads them** (temperature matters only by
  deciding which hazard was generated at world-gen). The SensorJam storms (dust/ash/lightning) are *generated* but
  excluded from the attrition step (`IsDamageEffect`, `:1048`) — so they bleed nobody and change no fight.
- **🔵 THEORY — the design proposal, wired nowhere.** The rich "environment reshapes the whole fight" model — low-g
  speeds the close, high-g drags it, weather cuts detection, damage-by-nature bends — is the accepted-but-unwired hook
  from `ENVIRONMENT-CONDITIONS-DESIGN.md`. The **space** resolver is fully environment-blind (verified: zero terrain/
  hazard reads in `GameEngine/Combat/*.cs`), so this model would be a **ground-side** build.

**The real gaps, named plainly:**
1. **The rich model isn't wired.** Gravity, temperature, and weather do nothing to a fight. Making them matter (the
   THEORY column) is the open design/build. The board is real; the ceiling is not built.
2. **The surface-hazard armour is half-built** (§3's headline finding). The one buildable seal covers only Vacuum +
   Toxic; there is no component for Fire/Cryo/Corrosive, so those hazards are un-counterable today even though the
   `EnvironmentalResistance` map supports them.
3. **Per-hex hazards are now the LOCKED target (ruling C, §11 Q2) but not yet built in the engine.** The engine today
   is per-*region* (seeded + spread); the developer locked **per-hex-from-terrain** for the local menaces (fire on
   volcanic, cryo on ice, acid on lowlands) with world-facts (vacuum/toxic) staying world-wide, and the prototype
   (rev-D) now models exactly that. The M11 build is: derive each local hazard from the hex's own `GroundHex.Terrain`
   instead of stamping a region band. **Moving weather is now a greenlit emergent design (§11 Q3):** storms drift from
   their source terrain by the planet's `AxialTilt` rotation across the wrapping cylinder — the engine build is a small
   storm-cell processor, not an authored system.
4. **A body's terrain-climate and its hazard-climate can disagree when it has no atmosphere record** (surfaced building
   the real-map prototype). The terrain generator reads `AtmosphereDB.SurfaceTemperature`, but a body with *no*
   `AtmosphereDB` blob (Mercury, Europa) falls back to a **15 °C default** (`WorldTerrain.ForBody`), so **Mercury renders
   as a temperate barren world** even though its *hazard* layer correctly reads airless → Vacuum. Not a bug that breaks
   anything today (combat reads neither the terrain-temp nor gravity), but a real inconsistency to know about before any
   climate-driven terrain or condition wiring. (Bodies with a baked map — Luna — dodge it.)

This doc is the specification for closing gap 1 (and flagging 2); it is not a claim any of it is built. The prototype is
a faithful *model* of what the engine does today (LIVE), plus what it holds but ignores (DATA), plus what's proposed
(THEORY) — three registers, never blurred.

---

## 10. The prototype — `docs/ground/planetview.html`

A single self-contained HTML study of zoom 3. It is a **design study, not shipped UI** — it demonstrates the
interaction and the connection so the developer can react to the *feel* before any engine work is authorized.

What it does:
- **Six real Sol bodies** (Earth / Mars / Luna / Mercury / Venus / Ganymede) — each condition strip value read from the
  game's `sol/*.json` (gravity, surface temp, pressure, atmosphere, radiation, day-length), each cell badged DATA or
  LIVE by whether combat reads it.
- **Terrain from the game's REAL surface maps (rev-C).** Earth, Mars and Luna sample their actual baked biome tables
  (`EarthTerrainMap.cs` / `MarsTerrainMap.cs` / `LunaTerrainMap.cs`, the 72×36 maps the engine itself samples), through
  the shared `RealSurfaceMaps.CharToFeature` decoder and the exact grid formula (`lon=Q/Cols, lat=R/(Rows−1)`, row 0 =
  north pole) — so you see real continents, oceans, the Tharsis volcanoes, the lunar maria, and the **polar ice caps**.
  Venus / Mercury / Ganymede have no baked map, so they use the engine's procedural `WorldTerrain.Classify` rules
  (hot→volcanic/desert, cold→ice/barren, temperate→forest/plains) — labeled "procedural (representative)" because the
  game's exact layout is a per-save RNG seed.
- **A clickable operational hex band at TRUE per-planet scale (rev-G).** The grid is sized by radius exactly as the
  engine does — `R = clamp(round(12·radius/rEarth), 2, 24)`, then `cols = 4·(2R+1)`, `rows = 2R+1`
  (`PlanetHexFactory.HexPatchRadiusFor` + `PlanetGridFactory`) — so **Earth ≈ 100×25 ≈ 2500 hexes** and **Luna ≈ 28×7
  ≈ 196** (Mars 52×13, Venus 92×23, Mercury/Ganymede 44×11). Ocean impassable, ice handled, per-hex hazard icons
  (🔥/❄/⚗), rolling storm cells (🌀), and a **day/night terminator** that sweeps by rotation.
- **The map window sizes itself to the world + a per-hex distance readout (rev-H).** Every hex draws at the **same fixed,
  readable size** and the **map window grows or shrinks to fit that world** — Earth's 100×25 grid makes a wide window that
  **scrolls sideways** inside its column, Luna's 28×7 makes a small one that fits with room to spare (the map column is
  `min-width:0` so the big grid scrolls instead of stretching the whole page). This replaces rev-G's shared Earth-sized
  frame, where a small moon's hexes shrank to dots — now a moon's hexes stay just as readable as Earth's, the window is
  what changes. Alongside it, a **per-hex distance readout**: the whole surface (`4·π·r²`) split across `cols×rows` hexes
  gives the average width of one hex — **Earth ≈ 452 km, Mars ≈ 463, Luna ≈ 440, Mercury ≈ 393, Venus ≈ 466, Ganymede
  ≈ 424 km across** — shown in the map header (`grid 100×25 ≈ 2500 hexes · 1 hex ≈ 452 km across`) and in the tap
  readout (both the coord line and a *Hex span* row that multiplies by the march cost, so a `×2.5` rough hex reads as
  its real km-equivalent to cross). That is the movement scale the terrain march-cost multiplies against.
- **Rolling environments + day/night (rev-F).** All the atmospheric menaces roll (fire/acid storms too, not just
  dust/ash/lightning), born from source terrain and drifting by the planet's `AxialTilt` spin; the day/night terminator
  sweeps on every world (airless included). Controls: 🌀 Roll · ▶ Auto-roll · toggles for hazards / weather / day-night.
- **Micro-hex zoom (rev-F).** Tapping an operational hex opens its **micro-hex sub-grid** (the game's city / mini-hex
  layer — a 37-tile disk inheriting the theatre's terrain, M4 per-tile variation illustrated) right in the readout, so a
  tap always produces a visible zoom-in (the readout also scrolls into view on a narrow screen — the "I tap and see
  nothing" fix).
- **A comprehensive legend** — terrain swatches, the ground-hazard icons, the rolling-storm + night overlays, and the
  LIVE/DATA/THEORY grade key.
- **An engagement readout** — click a hex and it shows the two live layers (the terrain block with its real
  `GroundTerrain.cs` numbers; the surface attrition with its real `PlanetEnvironmentFactory.cs` per-hour magnitudes and
  the sealed-vs-unsealed bleed), the DATA overlays (generated-but-inert SensorJam storms), a boxed THEORY note, and a
  plain-English tactical read — then offers **"Resolve a battle here."**
- **Honest grading throughout** — every number carries its LIVE/DATA/THEORY status; the "what's real" table and the
  "real gap" box spell out that the ground resolver reads terrain + attrition and the space resolver reads nothing.

**Verification (headless, no CI):** the embedded script compiles clean, the full render path runs to completion under a
DOM stub with zero throws; a source-number spot-check confirms every terrain dial and hazard magnitude matches the
engine (cover 0.9/1.25/1.5, armour affinity 1.3/0.7/0.75, march ×1/×1.5/×2.5; Vacuum/Toxic 3, Fire 23.2, Corrosive 25,
Cryo 15 per hour) and that Venus generates the real Fire+Toxic+Corrosive set; and a **terrain check** confirms the three
baked maps are well-formed (36 rows × 72 chars, the engine's `IsWellFormed` guard), that Earth's poles sample all-ice
with oceans and continents between, that Mars/Luna are dry with ice poles, and that the procedural worlds render the
right character (Venus volcanic+desert no-ice · Ganymede ice+barren · Mercury no-ice temperate) — all via
`/opt/node22/bin/node`.
**Independently corroborated** by a 6-domain adversarial source audit (2026-08-09): space resolver environment-blind,
no weather system, all magnitudes verified.

---

## 10a. Grid scale — the mini-hex is the anchor, the op-hex count scales per planet (rev-I, 2026-08-09)

**The developer's rule (plain English):** *"the game needs to be playable on potatoes, so the regional-hex and mini-hex
numbers change so the regional-hex number can scale large or small enough that the mini-hexes are at most 50, a few km
across."* This is the DESIGN, verified against source by a blast-radius workflow (6 parallel surveyors + 3 adversarial
verifiers, all re-checked at file:line).

**The method — fix the finest tile first, derive everything upward.** There are three surface layers: the 4 coarse
**regions** (strategic glance), the **operational / "regional" hex** (`SurfaceGrid`, where a battle happens), and the
**mini / "city" hex** (`CityGrid`, the finest tile, where units actually fight). Today they are mis-scaled: an
operational hex is ~450–486 km and one mini-hex is ~35 km — nowhere near "a few km," and every conventional weapon
out-ranges its own tile.

The fix pins the **mini-hex** as a real tactical size and BOUNDS it, then derives the rest:

1. **Mini-hex = a few km (default 3 km), grid capped at ≤50 across.** A hex disk is always an ODD number across
   (`2·R+1`), so `CityPatchRadius = 24` gives **49 across, 1,801 tiles** — the honest realization of "≤50." Both bounds
   are the **potato ceiling**, and the grid is built LAZILY, only for an op-hex actually being fought in.
2. **One operational hex is then exactly `mini × 49` wide** (≈147 km at 3 km) — so its city tiling comes out at 49
   across by construction.
3. **The operational grid dims scale per planet to hold that size.** Hex real size uses the engine's own flat-to-flat
   HEXAGON pitch — `pitch = √(2·areaPerHex/√3) = ENGINE_K·r/cpr` with `ENGINE_K = √(2π/√3) ≈ 1.9046`
   (`GroundMiniHex.CoarseHexPitchKmForBody`) — **not** the `√(area)` square-cell figure, which is ~7.5% smaller
   (the rev-H readout used the wrong one; rev-I corrects it). Solve `colsPerRegion = round(ENGINE_K·r / opTargetKm)`;
   then `cols = 4·cpr`, `rows = cpr`. **Big world → many op-hexes, small moon → few** ("large or small enough").
4. **Engine reconciliation:** `cpr = 2·patchR+1`, `patchR = round(BaseHexRadius·r/rEarth)`, so `BaseHexRadius ≈ 41`
   (Earth `patchR 41`), `MaxHexRadius 24 → ≈ 43` (the giant clamp = **potato governor**: a gas giant coarsens instead
   of exploding — its mini-hex grows past 3 km, the intended flex), `CityPatchRadius 6 → 24`.

**The per-planet table (3 km mini, engine hexagon pitch):**

| World | radius | op-hex | op grid | op-hexes | mini-hex |
|-------|-------:|-------:|--------:|---------:|---------:|
| Earth | 6,378 km | ≈146 km | 332×83 | 27,556 | 2.99 km |
| Venus | 6,052 km | ≈148 km | 312×78 | 24,336 | 3.02 km |
| Mars | 3,396 km | ≈147 km | 176×44 | 7,744 | 3.00 km |
| Ganymede | 2,634 km | ≈148 km | 136×34 | 4,624 | 3.01 km |
| Mercury | 2,440 km | ≈145 km | 128×32 | 4,096 | 2.96 km |
| Luna | 1,737 km | ≈144 km | 92×23 | 2,116 | 2.94 km |

*(The **5 km** alternate keeps op-hexes at 250 km and drops Earth to 200×50 = 10,000 — ~2.8× lighter. It's the dial in
the prototype; pick it if the local build shows save size biting.)*

**Why this is potato-safe (the budget, verified) — three things stay bounded no matter the planet:**

- **Render / frame is a FIXED window.** The client draws a bounded sliding window (`DrawGlobalHexWindow`), not the whole
  grid — so Earth (27,556) and a gas giant cost the **same** to draw. ⚠ **Required change:** today the window is sized
  to *"2 region bands"* (`winCols = 2·colsPerRegion+1`), which balloons ~9× to ~11,000 hexes/frame at the finer grid;
  it must be a **fixed span** (e.g. 64×40 ≈ 2,560), independent of `colsPerRegion`. The prototype models the fixed
  window.
- **Mini cost is CAPPED** at 1,801 tiles per developed op-hex (the ≤50-across ceiling) and lazy — bounded by *activity*,
  not planet size.
- **Op-hexes are lazy DATA** — cheap terrain bytes, O(1) `HexAt` lookup, generated only where there's action; not a
  per-frame cost.

**What auto-recomputes for free (no code change):** per-hex real distance and crossing time, the region-band math
(`RegionOfColumn`/`BandCentreColumn`, `cols` stays divisible by `regionCount`), unit march speed, and the whole ground
**combat-range gate** — all read `(radius, cols, rows)` and rescale automatically. Finer hexes are in fact the **fix**
for "weapons out-range the whole map": at a 3 km mini-hex a tank cannon (4 km) spans ~1 tile, a laser (20 km) ~7, tube
artillery (30 km) ~10 — the built range-layering finally becomes visible.

**The three engine guards that must ship WITH the constant change (from the adversarial verify):**

1. **Fixed render window** (above) — else the per-frame draw balloons ~9× and it is NOT potato-safe.
2. **Retire the redundant per-region DISK grid** (`PlanetHexFactory.EnsureHexesForBody`) — it shares the same constants,
   so it *also* scales ~9× and would store ~16,900 disk hexes ON TOP of the 27,556 cylinder hexes (a ~1.75× undercount
   if left live). Cheapest save-size win.
3. **Trim the heavy per-hex `GroundHex` payload** (the always-serialized `Masked<long>` deposit-assay block + the
   always-allocated empty `List<int>` installation list) to non-default-only, **and** build the grid on
   *develop/survey*, not on merely *opening the planet view* (today an open persists the whole cylinder; a zoom persists
   a 1,801-tile city with nothing built).

**Save-load:** the constants embed no dims, so **new games are clean**; the hazard is that a stored hex `(Q,R)` addresses
a different physical spot after a regrid, so the rescale is **new-game-only** (or needs a migration step). Trimming the
per-hex fields is a serialization-shape change ⇒ also new-game-only / version-gated (root gotcha L3).

**Prototype (`planetview.html` rev-I):** the operational map is now a **fixed 64×40 window** of the fine grid
(anchored at the N-pole / 180°W corner) — the same size on Earth or Luna, the potato mechanism made visible; a
**Grid-scale & potato-budget panel** shows the three-rung ladder (Region → op-hex → mini-hex), the per-planet table,
the budget cards, and the three guards, with a **mini-hex-size dial** (3 km / 5 km) that rescales the whole grid live;
the tap **mini-hex zoom** renders the true 49-across / 1,801-tile / ~3 km grid; and the readouts carry both the op-hex
(~146 km) and mini-hex (~3 km) distances. Verified headless (engine-pitch ratio 1.0746, per-planet cpr, bounded window,
dial rescale, regressions) + Playwright (0 console errors, identical window width Earth vs Luna).

---

## 11. The five questions — the developer's rulings (2026-08-09)

The first cut left these five as "open, recommend X"; the second cut answered them against source. **The developer is now
locking them one by one — those rulings are recorded here as 🔒 DEVELOPER-LOCKED, with the source-grounding kept beneath
each.**

### Q1 — Does the environment read at battle-open, or re-read each tick? → 🔒 **LOCKED: A — live / per-tick.**
**The developer's call (2026-08-09): the environment re-reads continuously — a fight is never frozen to a one-time
snapshot at battle-open, and this holds for the future weather/gravity layer too (a storm that rolls in *during* a long
fight changes that fight as it happens).**

*Source-grounding (why this is free):* the engine already runs this way. The **surface attrition** is applied in the
ground hotloop **every tick** — `Health −= Magnitude × (Δt/3600) × (1−resist)` (`GroundForcesProcessor.cs:220-238`) — so
a unit that marches into a fire region starts bleeding on the next tick and stops when it leaves; the **terrain block**
is re-read at each combat resolution. So the LIVE layers already re-evaluate continuously, and when the THEORY layer
(weather → sight, gravity → movement) is wired, its natural home is the *same* per-tick loop — no new machinery, and
"weather that rolls in mid-fight" comes for free. Ruling A is the engine's existing grain, now locked as the intended
design.

### Q2 — How fine-grained is a planet's condition? → 🔒 **LOCKED: C — split it (world-facts world-wide, local menaces per-hex).**
**The developer's call (2026-08-09): whole-planet facts stay world-wide; local menaces go per-hex — and *build it into
the prototype now* ("if it works here we match it in game").** So:
- **World-wide** (a property of the whole planet): **airlessness → Vacuum**, a **poison sky → Toxic**. Every hex of that
  world carries it. These are the "you can't breathe *anywhere*" facts.
- **Per-hex, derived from the hex's own terrain + geography** (the M11 intent): **Fire** on the **volcanic** ground,
  **Cryo** on the **ice**, **Corrosive** storms over the open **lowlands**, and the DATA storms (Dust on desert/barren,
  Ash off the peaks, Lightning on the exposed high ground). A menace appears only on the terrain that *breeds* it.

*Built into `planetview.html` (rev-D):* a hazard now carries a `scope` (`world` / `local`) and, for local ones, an
`onTerrain` set; `hazardsAt(hex)` returns the world-wide facts **plus** the local menaces whose terrain matches the hex.
This turns "where do I land?" into a real decision — verified: on **Venus** a **volcanic** hex burns (fire + toxic =
**−26.2/hr**) while a **lowland** hex takes acid (corrosive + toxic = **−28/hr**), and the two are *different hexes*; on
**Ganymede** only the **ice** hexes get cryo (**−18/hr**) versus vacuum-only (**−3/hr**) elsewhere; **Luna** is uniform
vacuum; **Earth** stays combat-benign. The map draws each local menace's icon on its hexes and a faint uniform tint for
the world-wide fact.

*Engine reality vs. this ruling:* the engine **today** is **per-region** — `PlanetEnvironmentsDB` keys `RegionEnvironment`
by region index (`PlanetEnvironmentFactory` seeds each hazard into ≥1 region, spreads at 35%, `:97-102`), with Vacuum/
Toxic effectively world-wide. **Ruling C makes per-hex-from-terrain the target** (the M11 slice): the build is to
derive each local hazard from the hex's `GroundHex.Terrain` instead of stamping a whole region band. The world-wide
half already matches (airlessness is global); the local half is the M11 engine work this prototype now specifies.

### Q3 — Weather: authored or emergent, and can storms roll? → 🔒 **LOCKED: A — emergent, wire the generated storms; AND yes, make them MOVE, emergently.**
**The developer's call (2026-08-09): don't author a weather system — wire the storms the generator *already* makes
(Dust / Ash / Lightning) into combat (ruling A); and the follow-up they asked for — "is there no way to make this roll?"
— *yes*, make storms drift across the planet, but keep it emergent, not hand-scripted.**

*The emergent-drift design (built into the prototype, rev-E):* a storm is not placed — it is **born over its source
terrain** (dust off the desert, ash off a volcano, lightning over the high ground — the same physics that already
generates it) and then **drifts with the planet's rotation** across the wrapping cylinder grid. The drift direction
comes from **real data**: a body's `AxialTilt` gives its spin sense — a tilt near 180° means retrograde, so **Venus
(177°) rolls its storms WEST**, while **Earth/Mars (prograde) roll EAST**; **airless worlds have no air, so no weather**
(Luna/Mercury/Ganymede stay clear). No authoring, no fluid-sim — a storm's path is a consequence of the world's real
rotation, and it wraps the seam like everything else on the cylinder. The prototype seeds storm cells, drifts them one
column per weather-step (a "🌀 Roll weather" / "▶ Auto-roll" control), and reports "weather rolling over now" when a cell
covers the clicked hex; combined with **Q1 (per-tick reads)**, a storm that rolls over a fight blinds it *live*, then
passes.

*Grade:* wiring the SensorJam cut into the resolver is **the near-term A build** (a DATA→LIVE flip, no new content). The
**moving** layer is the **design proposal** the developer greenlit — its ingredients (per-hex storm sources from ruling
C, the wrapping cylinder, `AxialTilt` for direction, `LengthOfDay` available for speed) all exist, so the engine build
is a small storm-cell processor (hourly, like the other hazard processors) that spawns from source terrain and drifts
by rotation. Emergent, data-driven, no authoring — exactly the pattern the rest of the surface already follows.

*Extended (rev-F, developer follow-up "make the OTHER environments roll too + a day/night cycle"):* the rolling now
covers **every atmospheric menace** the world breeds — **fire tornadoes and acid superstorms roll**, not just the
sight-storms — each still born from its source terrain (ruling C) and spreading downwind; a hex it rolls over picks up
that attrition *temporarily* on top of its terrain baseline. And a **day/night terminator** was added: it's the same
emergent idea one layer up — the whole planet's lit hemisphere, sweeping by rotation (so it runs on **airless worlds
too**, Mercury's stark day/night included), cutting detection on the dark side. Both are driven by `AxialTilt`/rotation,
both wrap the seam. So the surface is a living hazard-scape: storms and night roll across it, and with Q1 they change a
fight live and then clear.

### Q4 — Do sealed/hardened components close the condition→answer loop? → **Half-closed, and this is the one real gap.**
Verified: the **sealed-systems** component (`GroundSealAtb`) is real and live — one `Sealing` dial folds into
`EnvironmentalResistance{Vacuum, ToxicAtmosphere}` at assembly, and the attrition step reads it, so a sealed unit is
genuinely exempt from vacuum/toxic bleed (cradle-to-grave: research → build → mount → lose). **But there is no
Fire/Cryo/Corrosive counterpart.** On Venus a fully sealed marine still takes the fire (23.2/hr) and corrosive (25/hr)
bleed — sealing only saves the 3/hr toxic. The `EnvironmentalResistance` map is keyed by *every* hazard kind, so the fix
is small: **build a heat-/corrosion-hardening component** (an `*Atb` that writes `{HeatDamage:x, CorrosiveDamage:x}`,
six-point registered like the seal). **The answer:** vacuum/toxic loop is closed and live; the thermal/corrosive loop is
**one component away** — that's the concrete next build this whole exercise surfaced, and the highest-value one.

### Q5 — Which zoom owns "commit a force"? → **Force Management already owns it; the answer is locked by ruling M9.**
Not open. Ruling **M9** deletes every order surface outside Force Management, and the movement order already exists as a
**queued** order (`GroundForces.OrderMoveToGlobalHex`, issuer-marked `GroundOrderIssuer.Player/Ai`). The client's old
**direct** click-to-march was *deleted* precisely because it bypassed the queue and the AI couldn't use it (One Verb,
Both Seats). **The answer:** the planet view is a **read-only front door** — it shows the ground and lets you read the
fight; committing a force is the existing queued movement order, issued from Force Management, and the environment rides
along for free because it's a property of the destination hex. Adding an order surface to the planet view would
re-introduce exactly the bug M9 removed. Nothing to build here — just don't.

**Net:** four of the five were already decided by the code (per-tick reads · per-region conditions · emergent-static
effects · Force-Management orders); the one true build decision — **the thermal/corrosive-hardening component** — is the
gap Q4 surfaced, and it's the recommended next slice whenever surface hazards are made to matter.

---

## 12. One-paragraph summary (for a cold read)

A planet is a **board of environments**, read straight from the game. You see it at four zooms — system, globe,
regional, city — and the regional zoom is where battles happen. Each hex hands the **ground** resolver two live things:
its **terrain block** (Open/Cover/Rough → a cover divisor, a unit-type affinity, a march cost, real numbers from
`GroundTerrain.cs`) and its world's **surface attrition** (a per-hour bleed on units standing in a generated
Vacuum/Toxic/Fire/Cryo/Corrosive hazard, real magnitudes from `PlanetEnvironmentFactory.cs`). You choose a battle's
environment by choosing **where you commit**, and the AI reads the identical two numbers off the identical hex. Every
other "environment" idea is honestly graded: gravity/temperature/radiation and the dust/ash/lightning storms are **DATA**
the body holds but combat ignores; the rich reshaping (gravity→movement, weather→sight) is **THEORY**, unwired; the
**space** resolver reads nothing at all. The prototype (`planetview.html`) ships the six real Sol bodies and shows only
what the engine really does — with the one real gap it surfaced flagged: *the sealed suit stops vacuum and poison, but
nothing built stops fire.*
