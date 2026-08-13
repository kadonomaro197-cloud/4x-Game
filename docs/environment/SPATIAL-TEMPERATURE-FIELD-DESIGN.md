# The Spatial Temperature Field — and the rule that every derived output must drive something

**Status:** DESIGN STUDY · 2026-08-09 · **design-only, no engine code.**
**Companions:** `docs/environment/PLANETARY-GENERATION-VARIABLES-DESIGN.md` (this is that doc's ranked **gap #3**, built out), `docs/environment/SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` (the derive-vs-override hybrid this rides on, decision 6), `docs/environment/ENVIRONMENTS-DESIGN.md` (the hazard layer it feeds), the prototype `docs/ground/planetview.html`.
**How this was built:** a 7-agent research workflow — four parallel source surveys (temperature consumers · the other-output consumers · the per-region storage layer · the derivation physics), a synthesis pass, and **two adversarial verifiers** (a *no-dead-output* check enforcing the developer's rule, and a *derivation-soundness + storability* check). Both returned **PASS-WITH-FIXES**; every fix is folded in below and the errors they caught are listed honestly in §9. Every `file:line` is cited from the survey and re-checked against HEAD.

---

## 0. The point, before the plumbing

Right now **every planet has exactly one temperature number.** The engine computes `SystemBodyInfoDB.BaseTemperature` (the sunlight-balance temperature, `SystemBodyFactory.cs:1395`) and its greenhouse-warmed twin `AtmosphereDB.SurfaceTemperature` (`AtmosphereProcessor.cs:62`), and **every part of the game that cares about temperature reads that one gauge.** So on any given world the pole and the equator are exactly the same temperature, a tidally-locked world's blazing day side and frozen night side collapse into a flat `×0.2` fudge on the colony bill (`SpeciesDBExtensions.cs:153`), and a temperate world can never have polar ice caps — because there is no "polar."

This design keeps that one number as the **body average** and hangs a cheap, honest **offset** on it: first a **per-latitude band** (equator warm, poles cold), then a **day/night swing** (or a fixed hot-spot/cold-cap on a sun-locked world). Nothing exotic — a temperature that finally *varies across the surface*.

**The developer's iron rule governs the whole thing:** *"these outputs need to input into something in game."* A derived number that nothing reads is not fidelity, it's a bug. So this doc is organized around **proving each output lands on a real, existing consumer** — and where it doesn't, saying so plainly and either naming the one wire that gives it a home or flagging it as a deferred gap.

**The boiler analogy.** Today we have one thermometer on the steam drum and we treat the whole boiler as that temperature. We're not adding a climate model — we're adding a thermometer to each tube row and telling the difference between the fire side and the back wall. The *average* drum reading doesn't change; we just stop pretending the fire side and the back wall are the same, so the crew can finally decide **where** to stand and **what** to lag.

---

## 1. The one finding that shapes everything: two axes, two homes

The verification turned up a mismatch that, once understood, *is* the architecture. Read this before anything else.

A planet's surface in the engine has **two independent directions**, stored on the `SurfaceGrid` (`SurfaceGrid.cs:16-21`):

- **`R` = latitude row** — north pole at row 0, south pole at the last row (Galaxy `CLAUDE.md` convention). This is the **equator-to-pole** axis.
- **`Q` = longitude column** — wraps around the world at the seam. This is the **day-side-to-night-side** axis (which face is toward the sun).

The trap: the game's **region-hazard layer is keyed by longitude only.** A `Region` is one of four **longitude wedges** that runs pole-to-pole (`PlanetRegionsFactory.cs:79`), and ground attrition reads hazards by the unit's `RegionIndex` (`GroundForcesProcessor.cs:230`), which is computed from its longitude column (`GroundForcesProcessor.cs:210`). **So a region hazard cannot carry a latitude gradient** — two units in the same wedge, one at the equator and one at the pole, would read the identical hazard. If you tried to push "cold poles" through the region-hazard path, it would be a **dead output** — exactly what the rule forbids.

But the terrain/biome layer is different: `WorldTerrain.Classify` is called **per hex, and it already receives latitude** (`PlanetHexFactory.cs:203-217`) — it just holds temperature constant across it. And every ground unit **already stores its own latitude row**, `GroundUnit.GlobalR` (`GroundForcesDB.cs:176`, saved + cloned), right beside its longitude.

So the field splits cleanly, and each half goes where it actually fits:

| Axis | Physical driver | Temperature swing | **Where it reaches the game (its real consumer)** |
|---|---|---|---|
| **LATITUDE** (`R`) | axial tilt + insolation geometry | modest (~tens of °C) | **the per-HEX terrain/biome path** — `WorldTerrain.Classify` (already per-hex, already has latitude) → ice caps, biomes → `HexPathfinder` movement + `GroundTerrain` combat mults; **and colony-cost-by-latitude** (habitability). Its modest swing crosses the *modest* thresholds these use (biome cold cut −10 °C, species tolerance ≈ −15/+45 °C). |
| **DAY / NIGHT + TIDAL** (`Q`) | rotation vs the sun; tidal lock | huge (airless diurnal = hundreds of °C; sun-locked substellar furnace) | **the region-hazard path** — `PlanetEnvironmentFactory` → `RegionEnvironment` → E3/E4 attrition (keyed by longitude-region, which is exactly this axis). Its huge swing crosses the *extreme* hazard thresholds (fire > 400 °C, cryo < −120 °C). |

This is why the mismatch is a gift, not a problem: **the modest latitude swing pairs with the modest biome/habitability thresholds and travels the per-hex road; the violent day/night swing pairs with the violent fire/cryo thresholds and travels the longitude-region road.** Each output lands on a consumer that (a) already exists and (b) can actually feel it. Nothing is pushed down a pipe that can't carry it.

*(There is an optional third road — a latitude-varying **attrition** hazard sampled by the unit's `GlobalR` — for the rare worlds where latitude fire/cryo/radiation genuinely crosses threshold. It's real because units carry `GlobalR`; it's not needed for v1. §4 covers it.)*

---

## 2. The derivation — a flagged game proxy, NOT a climate model

Start from what exists and add three modulations. **Every constant below is a FLAGGED balance dial**, not a physical measurement.

**Anchor (exists today):** `Tmean` = `AtmosphereDB.SurfaceTemperature` (the greenhouse-adjusted body average). For a body that truly has no atmosphere blob, fall back to `BaseTemperature` — **but see the airless-fallback correction in §9(4): the biome classifier currently defaults a blob-less body to a hardcoded 15 °C, and that fallback must be preserved to avoid silently re-classifying such bodies.**

### 2.1 The latitude band (tier 1)

For each grid row `R`:

```
latDeg = 90 - 180 * (R / (Rows-1))          // row 0 = North pole
phi    = latDeg in radians

w(phi) = insolation weight, ~1.3 at the equator, ~0.5 at the pole for Earth-like tilt.
         A smooth equator-to-pole falloff that FLATTENS as axial tilt rises:
         tiltFlatten = 1 - min(1, AxialTilt/90) * TILT_FLATTEN   // TILT_FLATTEN ~ 0.6, FLAGGED
         w(phi)      = 1 + tiltFlatten * BASE_W * cos-shaped(phi) // peaks at equator, dips at poles

wbar   = area-weighted mean of w over all rows (cos-latitude weighting)   // makes the offset ZERO-MEAN

rho    = 1 - exp(-Pressure / P_REDIST)      // heat REDISTRIBUTION: 0 = airless (full gradient),
                                            // ~1 = thick air (nearly isothermal). P_REDIST ~ 30 atm, FLAGGED
grad   = GMAX * (1 - rho)                    // GMAX ~ 55 °C half-spread scale, FLAGGED

RowBaselineTempC[R] = Tmean + grad * (w(phi) - wbar)
```

Three things make this behave:

- **Zero-mean by construction.** Subtracting the area-weighted mean `wbar` means the per-row offsets sum to zero across the globe. So the *body average* is unchanged — which matters for **correctness** (a per-band average reconstructs the body mean once a consumer opts in), **not** as the safety mechanism for existing consumers. Existing consumers are untouched for a different reason: this is **new storage nothing old reads** (see §9(3) — the verifier caught the original doc over-claiming this; the anchor stays read-only).
- **`rho` is why Venus is flat and Luna is savage.** The redistribution term reads `Pressure` (already computed, `AtmosphereProcessor.cs:34`). At Earth's ~1 atm, `rho ≈ 0.03` — almost no flattening, so Earth keeps a real ~50 °C pole-to-equator spread. At Venus's 92 atm, `rho ≈ 0.95` — nearly isothermal. At the Moon's vacuum, `rho = 0` — the full gradient. Correct planetary behavior falls out of **one existing input**. *(This `P_REDIST ≈ 30` scale is the calibration fix from §9(7): the synthesis's first cut used `Pressure/0.5`, which wrongly flattened Earth almost to isothermal.)*
- **Tilt flattens the gradient**, matching real annual-mean insolation (a high-obliquity world spreads its sunlight more evenly over the year). For the extreme case (obliquity > ~54°, where poles are *hotter* than the equator on annual mean — Uranus), **clamp toward isothermal rather than invert** (§8, open decision).

**Calibration target (the dial-setting gauge):** on Earth (`Tmean ≈ 15`, 1 atm), the poles must fall below the biome cold cut `ColdC = −10 °C` (`PlanetHexFactory.cs:103`) or no ice caps ever appear — the whole tier-1 payoff. So `GMAX` is set by *"Earth's polar band crosses −10 °C and its equator stays temperate."* A unit test asserting exactly that is the tier-1 gauge.

### 2.2 Day/night — computed ON READ, never stored (tier 2)

The diurnal swing is a **pure function of position + game-time**, so storing it would bloat every save and force a per-tick refresh (the empty `AtmosphereProcessor.Process` stub, `AtmosphereProcessor.cs:19-22`). Compute it in a helper. **Two regimes — and the split matters (§9(5) correction):**

```
// ROTATING world (the normal case, incl. moons locked to their PLANET — they still turn under the sun):
Adn      = A_DIURNAL * (1-rho) * clamp(LengthOfDay_hours / 24, 0.3, 8)   // long day + thin air = big swing
subsolar = frac(gameTime / LengthOfDay_seconds)                          // the sub-solar longitude sweeps 0..1
dayDelta = Adn * max(-0.4, cos(2*pi*(Q/Cols - subsolar)))                // +Adn at local noon, floored on the dark side

// SUN-LOCKED world ONLY (the body's day == its year — a permanent sub-stellar point):
tlDelta  = A_TIDAL * (1-rho) * cos(angleFromFixedSubstellarPoint)        // hot substellar, frozen antistellar, ~0 terminator

TempAtCell(Q, R, t) = RowBaselineTempC[R] + (isSunLocked ? tlDelta : dayDelta)
```

`LengthOfDay` and `AxialTilt` are already stored and round-trip (`SystemBodyInfoDB.cs:49,100`); they are the raw material sitting unused (`AxialTilt` has **zero** physics readers today — `SystemBodyFactory` survey). The sun-locked branch is the real prize: it **replaces the crude `×0.2` colony-cost fudge** with a genuine substellar furnace / antistellar freezer / habitable terminator ring the hazard generator and colony cost can sample.

> **⚠ Correction (§9(5)): "tidally locked" in the engine means locked to the ORBIT'S PARENT, not the star.** `OrbitExtensions.IsTidallyLocked` tests `LengthOfDay / OrbitalPeriod ≈ 1` (`OrbitExtensions.cs:100`). Luna passes that test — but Luna is locked to *Earth*, and still turns under the *sun* on a ~monthly solar day, so it has **no** permanent sun-facing hot spot. **Gate the fixed-substellar model on STAR-locked bodies only** (parent *is* the star, or the solar day ≈ the orbital period about the star). A planet-locked moon keeps the rotating day/night branch.

### 2.3 The internal-heat floor (a term, not a system)

Equilibrium temperature falls toward absolute zero far from the star, but real outer/tidal worlds have a heat floor: radiogenic (from `Tectonics`) plus tidal flexing (`Eccentricity` × closeness to the primary — Io). It's a `max()` floor folded into the baseline:

```
RowBaselineTempC[R] = max(RowBaselineTempC[R], InternalFloorC)
InternalFloorC      = f(Tectonics) + tidalTerm(Eccentricity, distanceToPrimary)
```

> **⚠ Correction (§9(5)): `Tectonics` is `NA` for every moon** (`SystemBodyFactory.cs:1330`). So a moon's radiogenic term is ~0 and its floor comes **only** from the tidal term — which is correct (Io is tidal, not radiogenic). Map `Tectonics` `Dead/NA/Unknown → ~0` so a dead rock stays deep-frozen (verifier confirmed dead rocks stay cold). **Honest scope:** the floor's *only* consumer is the field itself (it changes which biome/hazard a cold band gets). A standalone geothermal-power or eruption system has **no consumer today and is DEFERRED, not built** — the rule applied to ourselves.

### 2.4 Eccentric-orbit seasons (deferred)

The base temp already folds eccentricity in as the orbit-averaged distance `a(1+e²/2)` (`SystemBodyFactory.cs:1414`). A live seasonal wobble (`±grad_seasonal·cos(trueAnomaly)`) has all its inputs but **needs a per-tick driver and no consumer demands it yet** — flag it, don't build it.

---

## 3. Storage — the cheapest home that serves the consumers

**Primary (the latitude axis): a per-ROW float array on `SurfaceGrid`.**

Add `[JsonProperty] float[] RowBaselineTempC { get; internal set; }` (length = `Rows`, indexed by `R`) beside `Cols`/`Rows`/`Hexes` (`SurfaceGrid.cs:18-21`). Populate it in `PlanetGridFactory.EnsureGridForBody`'s **existing row loop**, where `lat = r/latDiv` is already computed (`PlanetGridFactory.cs:59-65`) — one deterministic pass, no new geometry, no RNG draw (so the `SystemGenTests` golden master is safe). Deep-copy it with **one line** in the `SurfaceGrid` copy ctor (`SurfaceGrid.cs:25-30`). Cost: `Rows` floats per gridded body (~40-90), not `Rows×Cols`.

Why this home and not others:
- **Latitude lives on the row index `R` by construction** — the array is indexed the way the axis is stored.
- **`SurfaceGrid` is a plain class, not a `DataBlob`**, so no `Clone()` override is needed (dodges landmine **L12**); and it adds **no constructor parameter and no type rename**, so it dodges the save-load landmine **L13**. Old saves load the new field as `null`.
- **Do NOT** hang the latitude field on `Region` (a longitude wedge pole-to-pole — can only carry a *longitude*/day-night value, §1) or on `ColonyHexMapDB` (no `Clone`, no `[JsonProperty]`, superseded — landmine **L12**, `ColonyHexMapDB.cs:12`).

> **⚠ Correction (§9(2)) — the back-fill migration, or it silently NPEs.** `EnsureGridForBody` returns **early** when the grid's hexes already exist (`PlanetGridFactory.cs:45`), *before* the row loop that would fill `RowBaselineTempC`. A save made after the `SurfaceGrid` feature but before this field (grid present, temp array null) would **never** populate — a consumer reading `RowBaselineTempC[r]` hits a null array. **Fix, stated as part of the design:** extend the idempotency guard to also require `RowBaselineTempC != null` (back-fill temps onto an existing grid), **and** have every consumer null-guard the array and fall back to the on-demand `BandTempC` function below. Both, not either.

**Day/night: computed on read**, never stored — a helper `TempAtCell(Q, R, gameTime)` on `SurfaceGrid` (sits beside `HexAt`, `SurfaceGrid.cs:36`). Consumers that want the extremes read `TempMaxC(R)` / `TempMinC(R)` (baseline ± the diurnal/tidal amplitude), also derived, no storage.

**Pre-grid habitability: a pure on-demand function `BandTempC(body, latDeg)`.** Colony cost is computed *before* a body is gridded (the grid is lazy — built only on colonize/survey). Expose the §2.1 math as a pure function off the body scalars (`SurfaceTemperature` + `AxialTilt` + `Pressure`) so `ColonyTemperatureCost` can sample a latitude band without forcing a grid build. This is also the null-guard fallback above.

---

## 4. The output → consumer wiring matrix

Every derived output, its **real** in-game consumer at `file:line`, whether it's already wired or one wire short, and — per §1 — **which road it takes.** `EXISTS-WIRED` = the consumer reads it today with no change; `NEEDS-WIRE` = the consumer exists but the field must be fed into it; `MISSING`/`DEFERRED` = no consumer, honestly flagged.

### The LATITUDE road (per-hex terrain + habitability)

| Output | Consumer (file:line) | State | How it wires |
|---|---|---|---|
| **Per-latitude baseline `RowBaselineTempC[R]`** | **`WorldTerrain.Classify` — biome/terrain** (`PlanetHexFactory.cs:141` reads a scalar `_temp`; `:242` cold→Ice/Tundra, `:252` hot→Volcanic/Desert, `:261` temperate→Jungle/Forest) | **NEEDS-WIRE** | `Classify` already **receives** `lat` (`:203-217`) but holds `_temp` constant. Replace the single `_temp` with a per-lat lookup (`RowBaselineTempC[r]`, or the pre-grid `BandTempC`). Cold polar rows fall below `ColdC(−10)` → **ice caps**; the equator stays temperate → jungle. **Equatorial jungle + polar ice on one world — impossible today.** *(Preserve the airless 15 °C fallback, §9(4).)* |
| **Ice/liquid PHASE by band** | `Classify` ocean/ice → **`HexPathfinder` (ocean impassable to non-amphibious)** (`PlanetHexFactory.cs:140,156,244`; pathfinder makes Ocean impassable) | **EXISTS-WIRED** (rides the biome wire) | No new storage. A cold band + hydrosphere → `Classify` returns `Ice`; a warm band → liquid ocean. Latitude ice caps and their **movement** consequences fall out of the biome wire. Hydrosphere *extent* stays whole-body; only its *phase* becomes spatial. |
| **Band temp at the colony's latitude** | **`ColonyTemperatureCost` → `ColonyCost`** — the primary temperature DECISION (`SpeciesDBExtensions.cs:124` reads `BaseTemperature`, `:127` overrides with `SurfaceTemperature`, `:141-158` Aurora band cost, `:153` ×0.2 if locked) | **NEEDS-WIRE** *(+ prerequisite)* | Give the colony a latitude to sample; `ColonyTemperatureCost` reads the band temp there instead of the body scalar. **Prerequisite (§9(1,6)):** a colony has **no latitude today** (it snaps to capital region 0, a longitude wedge). Until a founding **hex/latitude** is a first-class field, this is a *per-colony readout*, not yet a chosen lever — see §6 and §8. |
| ↳ transitively | **`PopulationProcessor` capacity + `ColonyMoraleDB` morale** (`PopulationProcessor.cs:35,110,194`; `ColonyMoraleDB.cs:119`) | **EXISTS-WIRED** | Once `ColonyTemperatureCost` samples a band, the harsher/kinder cost flows through `Max()` → carrying capacity and the morale conditions penalty automatically. |

### The DAY/NIGHT + TIDAL road (region hazards)

| Output | Consumer (file:line) | State | How it wires |
|---|---|---|---|
| **Day-peak (fire) / night-trough (cryo) by longitude region** | **`PlanetEnvironmentFactory.BuildEnvironments` — per-region fire/cryo hazard** (`:69` reads one body `tempC`; `:80` fire > 400 °C, `:81` cryo < −120 °C; `:95-103` RNG-scatters the ONE verdict) | **NEEDS-WIRE** | Evaluate the day-peak / night-trough **per longitude region** (which face the sun is on) instead of one body verdict scattered by chance. Fire sites on the day side / sun-locked substellar wedge; cryo on the night side / antistellar wedge. Emits `RegionEnvironment` HeatDamage. **This axis genuinely maps onto regions** (§1), and the huge diurnal/tidal swings actually cross the 400/−120 thresholds (§9(3): the modest *latitude* swing does not — which is why fire/cryo lives on THIS road, not the latitude one). |
| ↳ downstream | **`GroundForcesProcessor` E3/E4 attrition** (`:230-236` bleed; `:1049` `IsDamageEffect` includes HeatDamage) | **EXISTS-WIRED** | No code change. A unit in a hot/cold wedge bleeds, reduced by its `GroundHardeningAtb`/`GroundSealAtb` (`EnvironmentalResistance`). **This is how temperature reaches ground combat, and the grave rung that makes the hardening component matter** (cradle-to-grave). |
| **RNG-preservation note** | `PlanetEnvironmentFactory.GenerateForSystem` draws the shared `StarSystem.RNG` (`:97,:101`) | constraint | §9(8): a per-region rewrite must **preserve the exact RNG draw count/order** (or move the decision to the lazy grid path) so the `SystemGenTests` golden master doesn't shift. Run `SystemGenTests` as the tripwire. |

### The OTHER outputs (radiation, pressure, internal heat, dead outputs)

| Output | Consumer (file:line) | State | How it wires |
|---|---|---|---|
| **Radiation dose → `RadiationDamage` region env** | **`GroundForcesProcessor` E3/E4** already consumes `RadiationDamage` (`:1049` in `IsDamageEffect`); producer GAP at `PlanetEnvironmentFactory.cs:80-91` (never emits it) | **NEEDS-WIRE** *(sharpest win)* | The **consumer is fully wired; the surface producer is absent.** One bridge — emit a `RadiationDamage` region env on airless / weak-field / **polar** wedges (a magnetosphere funnels particles to the poles) — gives radiation its **first real home**: unsealed/un-hardened units bleed there, a rad-hardened component survives. Full cradle-to-grave from a single wire. |
| **`RadiationLevel` scalar (populate the hard-0)** | none today except client display (`SystemBodyInfoDB.cs:73`; hard-set 0 at `SystemBodyFactory.cs:1345`; read at `EntityDisplay.cs:53`) | **STORED-UNREAD** (prerequisite) | Derive it at gen from `MagneticField` + `Pressure` (column absorption) + stellar distance, replacing the constant 0. Prerequisite for the radiation env above. |
| **`RadiationLevel` → ColonyCost** | `ColonyCost` is `Max(Pressure,Temp,Gas,Toxicity)` — **no radiation term** (`SpeciesDBExtensions.cs:35-43`); the `SystemBodyInfoDB.cs:69` comment *"Affects ColonyCost"* is **false** | **MISSING** (developer decision) | Add a real `ColonyRadiationCost` term to the `Max()` list, **or** correct the stale comment. §8. |
| **Atmospheric column → `Pressure`** | `ColonyPressureCost` (`SpeciesDBExtensions.cs:101`) + production efficiency (`InfrastructureProcessor.cs:61`) + pop-support tolerance (`ComponentInstancesDBExtensions.cs:95`) + Vacuum hazard (`PlanetEnvironmentFactory.cs:70,90`) | **EXISTS-WIRED** | **Not a new output — it's an INPUT to the field** (the `rho` redistribution term). Best-wired scalar in the engine already. Stays whole-body (pressure doesn't vary by latitude at this grain — spatializing it would be a dead output). |
| **Internal-heat floor** | the field itself (raises cold-band baseline → biome + hazard) | **NEEDS-WIRE / bounded** | A `max()` floor inside `RowBaselineTempC`. Io stays volcanic-warm (sites fire, not cryo); a dead moon stays frozen. **Its only consumer is the field; a standalone geothermal system is DEFERRED** (§2.3). |
| **Body-mean temp** | **`SensorTools.PlanetEmmisionSig`** — planet thermal EM signature (`:616` reads `BaseTemperature` → Wien `:619` → Stefan-Boltzmann `j=εσT⁴·area` `:624`) | **EXISTS-WIRED — deliberately left whole-body** | **No per-region wire.** The physically correct operand is the body-integrated (area-weighted mean T⁴) emission, which the body mean already is. Explicit note that this is *not* an oversight — we do **not** invent a spatial consumer where none belongs. (Zero-mean leaves this operand exactly unchanged.) |
| **`AtmosphericDust` (bombardment cooling)** | none today (dust-*storm* hazard keys off `HydrosphereExtent`, not this; `SystemBodyInfoDB.cs:80`) | **STORED-UNREAD** (pre-existing debt) | Same dead-output shape as `RadiationLevel`. Its natural home is the **anti-greenhouse feedback** — `UpdateAtmosphere` should subtract a dust term so a **bombed world's bands cool** (nuclear winter). Flag it; don't build on it silently. |

---

## 5. The other outputs, in one paragraph each

- **Radiation dose** is the highest-value wire in the whole task, because the *hard* half (the ground-attrition consumer) is already built — `RadiationDamage` is in `IsDamageEffect` (`GroundForcesProcessor.cs:1049`) — and only the *producer* is missing. Derive the dose from `MagneticField` (a magnetosphere shields the equator but **funnels particles to the poles**, a natural latitude signal) and the atmospheric column `Pressure`, populate the currently-hard-0 `RadiationLevel`, and emit one `RadiationDamage` region env on airless/weak-field/polar wedges. That single bridge gives radiation a full cradle-to-grave home and makes the rad-hardening rung of `GroundSealAtb`/`GroundHardeningAtb` matter.
- **Pressure** is not a new output — it's the field's most important *input* (the `rho` term that makes Venus flat and Luna extreme). It stays whole-body.
- **Hydrosphere phase** (liquid vs ice) becomes spatial for free — it rides the biome wire: feed the latitude field into `Classify` and cold bands return `Ice`, which `HexPathfinder` already treats as impassable. Extent stays whole-body.
- **Internal heat** is a floor term feeding the field, never a standalone system (no consumer → deferred).
- **Two pre-existing dead outputs** are flagged, not built upon: `AtmosphericDust` (natural home = anti-greenhouse cooling of a bombed world) and **every ground `SensorJam` env** (ash/dust/lightning are generated and read by nothing — the code comment at `GroundForcesProcessor.cs:223` itself calls it a "later wire").

---

## 6. Cradle to grave — the decisions the field drives, and the losses

The field is **generation-upstream** — like gravity, it's a physical property computed at gen, not something you mine and build. So the cradle-to-grave test applies to **the decisions it creates** and **the components that answer them**, and those must run the full chain or the field is just pretty fidelity.

- **Cradle (the world):** at gen, star temp + distance → `BaseTemperature` → greenhouse `SurfaceTemperature` → the field lays down `RowBaselineTempC[R]` per latitude and, on read, the day/night or sun-locked swing. No player input — this is the ground the player reads.
- **The decisions it creates (stacking levers):**
  1. **WHERE to settle** — colony cost varies by the colony's latitude/wedge, so a temperate mid-latitude beats a pole or a substellar furnace; on a sun-locked world the **terminator ring** becomes the prize. *(Real lever only once a colony carries a chosen latitude — §8. Until then it's a per-colony readout, honestly downgraded per §9(1).)*
  2. **WHAT to harden against** — the field sites fire in hot wedges and cryo/radiation in cold/dark ones, so the player must build the **right gear for the right theatre**: `GroundHardeningAtb` (thermal/corrosive) for the day side/equator, seals + rad-hardening for the dark side/poles. Those components run the **full vertical chain** — mined → refined → designed in the Entity Assembler → researched → installed → the in-play resistance → shot off (grave).
  3. **WHETHER a world is worth taking** — the band-sampled colony cost flows through `ColonyCost → PopulationProcessor` capacity and `ColonyMoraleDB` morale, so a world's real value varies by where you land on it.
- **The grave (loss) — two shapes:**
  1. **A unit dies in a band it wasn't built for** — an un-hardened garrison in a fire wedge or a cryo/radiation wedge bleeds via E3/E4 attrition (`:230-236`). Lose the hardening component (shot off, `EnvironmentalResistance` gone) and the same band that was survivable now kills you.
  2. **You can DEGRADE a world's field by bombing it** — orbital bombardment raises `RadiationLevel` + `AtmosphericDust` (`DamageProcessor.cs:309-310`); wired through the anti-greenhouse and radiation-env producers, that drops band temps and spreads radiation wedges — a scorched-earth feedback where the field re-derives colder and more irradiated. "Soften before you land" gains a thermal/radiological consequence, not just a casualty count.

**One Verb, Both Seats (§9(2)):** every decision the field creates is drivable by the AI with the **same primitive the player uses** — ground movement / hazard-avoidance rides the shared ground order queue (the AI issues the same march orders, `GroundCombat/CLAUDE.md`), colony siting rides `CreateColonyOrder` for both seats, and E3/E4 attrition applies to every unit regardless of owner. The field is a passive world property both seats read the same way. It passes the law — stated here explicitly so the check is closed.

**The honest gate:** the WHERE/WHAT/WHETHER levers are real only once the two `NEEDS-WIRE` consumers (biome classifier + region hazard generator) and the colony-latitude prerequisite are built. **Build the field and its two natural consumers in the same slice, or don't build the field** — a spatial temperature nothing samples is exactly the "pretty, not wired" failure the audit forbids.

---

## 7. Fidelity tiers — ships incrementally

- **Tier 0 (today):** whole-body scalar. Every consumer reads one number. No change.
- **Tier 1 — the LATITUDE band.** `RowBaselineTempC[R]` + wire the **biome classifier** (ice caps + equatorial jungle on one world) and **colony-cost-by-latitude**. The visible payoff. Its modest swing crosses the *modest* biome/habitability thresholds. **It does NOT deliver latitude fire/cryo** — those thresholds (400/−120 °C) are far outside a normal latitude swing (§9(3)); fire/cryo is a tier-2 day/night phenomenon. Zero-mean anchoring + new-storage additivity keep every un-wired consumer identical.
- **Tier 2 — the DAY/NIGHT + TIDAL swing** (computed on read). Wire the **region fire/cryo hazards** (day side/substellar fire, night side/antistellar cryo — where the huge swing actually crosses 400/−120), the **radiation region env**, and replace the crude `×0.2` sun-locked colony-cost fudge with the real substellar/terminator/antistellar band. Optional: eccentric seasons (deferred — no consumer).

---

## 8. Open developer decisions

1. **Colony latitude (the tier-1 habitability prerequisite).** A colony has no latitude today (it snaps to capital region 0, a longitude wedge). Add a founding **hex/latitude** as a first-class field on `ColonyInfoDB` (so `ColonyTemperatureCost` samples that row), or keep colony cost whole-body and let only the *ground-combat* consumers use the spatial field? Without a chosen latitude, "where to settle" is a readout, not a lever.
2. **Calibration dials.** `GMAX` (pole-to-equator half-spread) set so Earth's poles cross `ColdC(−10)`; `P_REDIST` (redistribution vs pressure) set so Venus is isothermal and Luna extreme; `A_DIURNAL`/`A_TIDAL` (day-night and substellar amplitudes). Pick target readings and lock them with a gauge.
3. **Radiation → ColonyCost.** The `SystemBodyInfoDB.cs:69` comment claims `RadiationLevel` *"Affects ColonyCost"* but it does not. Add a real `ColonyRadiationCost` term to the `Max()` list (a fifth habitability cost), wire radiation only into the ground hazard, or just fix the comment?
4. **Sun-locked model scope.** Gate the fixed-substellar model on **star-locked** bodies only (§9(5)); planet-locked moons keep rotating day/night. Confirm.
5. **Cryo behavior change.** Per-wedge day/night siting means the night side of a merely-cold world newly bleeds unsealed garrisons — an approved behavior change (like the airless/toxic surface hazards), or gate it behind a flag? *(The cryo generator itself is a flagged follow-up per the `GroundHardeningAtb` notes.)*
6. **Extreme obliquity (> ~54°).** Poles hotter than the equator on annual mean (Uranus). Recommendation: **clamp toward isothermal, do not invert** — simpler and un-surprising for the game proxy.
7. **Storage split.** `RowBaselineTempC[]` on `SurfaceGrid` (lazy, theatre worlds) **plus** the on-demand `BandTempC(body, latDeg)` for pre-grid colony cost — confirm this split, and whether to also cache a coarse per-row array on the eager `PlanetRegionsDB`.
8. **Live seasons.** Eccentric-orbit seasonal swing has all inputs but no consumer and needs a per-tick driver. Defer as tier-2-optional, or is a seasonal cycle a required output?
9. **EM emission stays whole-body.** Confirm the planet's sensor signature (`SensorTools.cs:616`) should remain body-integrated mean-T⁴ and **not** be spatialized — the physically correct call, but worth an explicit sign-off since it's the one temperature reader that deliberately doesn't get the spatial treatment.

---

## 9. Corrections the verification forced (don't skip — these are real errors the first pass made)

Both adversarial verifiers passed the design **with fixes**; the substantive ones, folded into §§1-8 above:

1. **The "where to settle" decision was oversold.** The output (band temp → `ColonyTemperatureCost`) *does* feed a real consumer, so it's not a dead output — but the *decision* only exists if founding lets a latitude be **chosen**, and colonies snap to a fixed wedge today. Downgraded to a "per-colony readout" until the founding-latitude prerequisite lands (§6, §8.1).
2. **One Verb, Both Seats was left unstated.** The field is AI-drivable (shared order queue, `CreateColonyOrder`, owner-agnostic attrition) — now stated explicitly (§6).
3. **The zero-mean rationale was over-claimed.** Existing consumers are untouched because the field is **new storage nothing old reads**, *not* because of zero-mean. Zero-mean matters for the **correctness** of future per-band sampling. Anchor stays read-only. Restated (§2.1).
4. **The airless fallback was misattributed.** `PlanetHexFactory.cs:141` defaults a blob-less body to a hardcoded **15 °C**, not `BaseTemperature−273.15`. Wiring the field into `Classify` must **preserve the 15 °C fallback** or such bodies silently re-classify. (Most airless bodies carry an `AtmosphereDB` with `Exists=false` whose `SurfaceTemperature` *is* populated, so the 15 °C path only bites truly blob-less bodies — but the fallback is still wrong to drop.) (§2, §4.)
5. **"Tidally locked" ≠ "sun-locked."** `IsTidallyLocked` tests lock to the **orbital parent**; Luna passes yet still turns under the sun. The fixed-substellar model is gated on **star-locked** bodies only; planet-locked moons keep rotating day/night. Also: `Tectonics` is `NA` for moons, so the internal-heat floor's radiogenic term is ~0 for them (only the tidal term warms Io). (§2.2, §2.3.)
6. **Colony has no latitude** — a genuine missing prerequisite (duplicate of #1 from the soundness side). (§8.1.)
7. **The redistribution + gradient were mis-calibrated.** The first cut used `Pressure/0.5` (flattening Earth almost to isothermal) and an aggressive quarter-power that shrank the effective spread to ~5-15 °C — too small to cross `ColdC(−10)`, so no ice caps, killing the headline. Fixed: `P_REDIST ≈ 30 atm` (Earth keeps its gradient, Venus flattens), gradient applied more directly, `GMAX` calibrated so Earth's poles cross −10 °C, with a gauge asserting it. **And the load-bearing consequence:** the *latitude* swing is too gentle for fire/cryo (400/−120) — so latitude drives **biomes/habitability** (tier 1) and **day/night drives fire/cryo** (tier 2). This is what produced the two-axes/two-homes split in §1. (§2.1, §7.)
8. **The back-fill guard.** `EnsureGridForBody` returns early before the row loop, so old saves would never populate the temp array. Fix: extend the guard to require the array non-null **and** null-guard every consumer with the `BandTempC` fallback. (§3.)
9. **RNG golden-master risk.** Rewiring `PlanetEnvironmentFactory` (which runs at gen and draws the shared RNG) must preserve the exact draw count/order or move the decision to the lazy grid path; `SystemGenTests` is the tripwire. (§4.)

The verifiers **confirmed** the load-bearing good parts: no insolation double-counting (the field modulates the existing equilibrium mean, doesn't re-add flux); correct latitude/day-night/tidal signs; the `SurfaceGrid` storage home is save-safe (no L12/L13 exposure) and RNG-free; the sensor-emission consumer is correctly left whole-body; dead rocks stay cold; and the `RadiationLevel`/`AtmosphericDust` "Affects ColonyCost" comments are indeed false.

---

## 10. The planet-view tie — moving the map from hand-painted to physically derived

**What `docs/ground/planetview.html` does today:** it carries **one whole-body temperature** per world (`PLANETS[].temp`, e.g. Venus 464 °C, Ganymede −163 °C), places hazards by **terrain type**, and treats the day/night terminator and the night detection-cut as a **THEORY overlay** hand-placed on top (`HAZ.night`, grade THEORY). Ice, storms, and the dark side are painted by rule, not derived from a temperature that varies across the surface.

**What the field makes LIVE-derived:**

1. **Latitude ice caps become real** — feed `RowBaselineTempC[R]` into `Classify` and the top/bottom rows freeze to `Ice` on **any** world whose poles cross freezing (not just the authored Earth/Mars maps). The caps grow/shrink with the world's mean temp and tilt.
2. **Fire/cryo hazards get sited by the day/night wedge, not scattered** — fire on the sunlit/substellar side, cryo on the dark/antistellar side, replacing the RNG scatter.
3. **The terminator carries a real temperature delta** — the sweeping `dayPhase` (already prototyped) stops being a bare detection overlay and becomes a genuine warm-day/cold-night band moving with rotation (colder **and** harder to see). Promotes the THEORY night overlay to **LIVE**.
4. **Sun-locked worlds get a fixed hot spot + frozen cap + habitable terminator ring** instead of a sweeping terminator.

**What the prototype should show (the demo):** a **latitude temperature gradient bar** down the side of the map (equator hot → pole cold, numbers labeled LIVE); **auto-drawn ice caps** on cold bands that resize as you switch worlds (Venus none, Earth small, Ganymede all-ice, a sun-locked world an antistellar cap); **fire clamped to the day/substellar wedge, cryo to the night/antistellar wedge** (grade LIVE); the **terminator with a real ΔT** sweeping a rotating world / a fixed pattern on a sun-locked one (grade LIVE, was THEORY); a **radiation-dose band brighter at the poles** with a `RadiationDamage` marker on airless/weak-field worlds (grade DATA until the producer wire lands, then LIVE). Keep the honest grade key (LIVE = engine-derived, DATA = stored, THEORY = still a proposal) and show which single dials (`GMAX`, `P_REDIST`) the developer tunes.

---

## 11. Provenance

7-agent research workflow (2026-08-09): four parallel source surveys (temperature consumers · other-output consumers · per-region storage · derivation physics), one synthesis, and two adversarial verifiers (no-dead-output; derivation-soundness + storability), both returning PASS-WITH-FIXES with every fix folded in above and every error listed in §9. `GroundUnit.GlobalR` (`GroundForcesDB.cs:176`) and the region-is-longitude fact (`GroundForcesProcessor.cs:210`) were independently re-verified after the workflow to confirm the two-axes/two-homes architecture. Nothing here is built — this is the design a future generation-and-wiring pass derives against, under the locked *derive-procedural / override-authored* hybrid (`SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` decision 6).
