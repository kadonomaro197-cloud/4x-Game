# Planetary Generation Variables — the input vector that derives every environment

**Status:** DESIGN STUDY · 2026-08-09 · design-only, no engine code.
**Companions:** `docs/environment/ENVIRONMENTS-DESIGN.md` (the hazard layer), `docs/environment/SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md` (the generator + the frozen-detail spec), `docs/ground/PLANETARY-VIEW-AND-INTERACTION-DESIGN.md` (how the surface becomes the fight), the prototype `docs/ground/planetview.html`.
**How this was built:** a 10-agent research workflow — a five-domain environment catalogue (real planetary science + franchise relevance) crossed against the real engine source, synthesized into the variable vector, then hardened by three adversarial verifiers. Every EXISTS/MISSING state below is cited at `file:line` and re-checked against HEAD.

---

## 0. The question, and the one-paragraph answer

The developer asked, after hand-editing `sol/*.json` "a bunch" to get surface features and atmospheres right:

> **"What variables must be available upon planetary generation to fully exploit ALL possible environments and weather systems that a body in space can incur?"**

**The answer in one line:** the generator today makes you type in the **outputs** (surface temperature, atmosphere composition, hydrosphere, features) because it lacks a handful of **fundamental inputs** — chiefly a **per-species volatile inventory**, an **internal-heat term**, a **spatial (per-latitude, day/night) temperature field**, and a **derived-radiation term** — and because **the authored path skips the derivations the engine already owns.** Add those inputs (most are cheap; several already sit in the data unread) and every environment in §3 falls out of physics instead of your keyboard.

**The reactor analogy.** Right now the body generator is like a plant where someone hand-writes the gauge readings on a whiteboard — steam temp, pressure, level — instead of the instruments computing them from the actual conditions (feed flow, heat input, valve positions). It *looks* right on the board, but nothing downstream can *react*, because there's no live chain from cause to reading. We want the instruments back: give the generator the real physical inputs, and the "gauges" (temperature, atmosphere, weather) compute themselves — and then they can drive combat, colony cost, and the AI.

---

## 1. The root cause — the authored path skips the engine's own derivations (READ THIS FIRST)

This is the single most important finding, and it reframes the whole problem. **The engine already owns most of the derivations you've been doing by hand — but the code path that builds a hand-authored Sol body never runs them.**

- The environment/hazard generator, `GroundCombat/PlanetEnvironmentFactory.GenerateForSystem`, consumes **exactly** `AtmosphereDB.SurfaceTemperature`, `.Pressure`, `.HydrosphereExtent`, `.Composition`, and `SystemBodyInfoDB.BodyType` + `.Tectonics`. Everything else a body carries — `AxialTilt`, `MagneticField`, `LengthOfDay`, `OrbitDB.Eccentricity`, `StarInfoDB.Luminosity`/`SpectralType`/`Age`, `RadiationLevel` — is **stored but never read for weather.**
- `AtmosphereProcessor.UpdateAtmosphere` **does** compute surface temperature (the Aurora greenhouse formula) and would compute more — but on the **authored path** (`SystemBodyFactory.Create` / `CreateFromBlueprint`, the one that reads `sol/*.json`) the `AtmosphereDB` is built **straight from JSON** and `UpdateAtmosphere`/`GenerateAtmosphere` **never run.** The one derivation that *is* re-run on the authored path is `BaseTemperature` (`SystemBodyFactory.cs:145,382`) — and that is the **proven fix-pattern**: wire the rest of the derivations in the same way and the authored `SurfaceTemperature` / `Pressure` / `Composition` / `Hydrosphere` / `Tectonics` mostly stop needing to be typed in.
- `AtmosphereProcessor.Process` (the per-tick hotloop) is an **empty stub** (`AtmosphereProcessor.cs:19-22`) — so temperature never varies in **time** (no seasons, no day/night, no frost cycles), and one scalar `SurfaceTemperature` is assigned to the **whole body** — so it never varies in **space** (no poles, no tidal-lock hot side).

So there are two classes of work, and the split matters for cost:

- **Failure A — "the number exists, it's just unwired"** (cheap, mostly one-line reads): `AxialTilt`, `MagneticField`, `LengthOfDay`, `Eccentricity`, `RadiationLevel`, `Albedo`, the gas-property table already in `atmosphereGases.json`. These need a **consumer**, not a new field.
- **Failure B — "the number doesn't exist, build the gauge first"** (real new work): the **per-species volatile inventory**, an **internal-heat term** (radiogenic + tidal), a **spatial temperature field**, an **exospheric-temperature / retention model**, and the **radioactive-element fraction**. These are the genuinely new inputs.

(This "Failure A vs Failure B" split is the same lens `docs/combat/INFORMATION-DELTA-DESIGN.md` uses — half of a "missing readout" is really an unwired one.)

---

## 2. The complete input-variable vector

41 fundamental inputs, grouped by physical domain. **State:** ✅ read · 🟡 stored-but-unread (Failure A — wire a consumer) · 🔴 missing (Failure B — build it). The `enables` column is abbreviated; the full environment list is §3.

### 2.1 Stellar (the body's sky)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Stellar effective temperature (K) | ✅ `StarInfoDB.Temperature:30` → `CalculateBaseTemperatureOfBody:1398` | base temp of every body; UV flux | from the star (rolled at system gen) |
| Stellar luminosity (L☉) | ✅ for placement/ecosphere (`StarInfoDB:80,87`); 🟡 for a body's *seasonal/time-varying* flux | insolation baseline (already via T★+R★+distance), habitable-band, snowball threshold, flare amplitude | from the star |
| Stellar spectral type (O…M) | 🟡 `StarInfoDB.SpectralType:51` (system-gen only) | UV sterilization, stellar-wind strength, flare/aurora particle source | from the star |
| Stellar age (yr) | 🟡 `StarInfoDB.Age:22` ("Fluff") | flare frequency (young = more), wind density, stripping history | from the star |
| Stellar flare/activity level | 🟡 space-side only (`StarFlareSourceDB`, flat-RNG); no surface consumer | flare/proton-storm surface pulse, EM storm, insolation spikes | **derive** from spectral type + age (not flat RNG) |
| Stellar variability / compact-remnant flag | 🔴 no field | flare-star spikes, magnetar/neutron-star hard-X-ray (crisis flavor) | authored/rolled for exotic placements |

### 2.2 Orbital / dynamical (where the body sits + how it moves)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Semi-major axis (AU) | ✅ folded into distance in `CalculateBaseTemperatureOfBody:1414` | insolation (with L★), radiation dose (1/d²), astrosphere-edge | from orbit |
| Orbital eccentricity (0–1) | 🟡 read but **degenerate** — only as a time-average `a(1+e²/2)` (`:1414`), no seasonal swing, no tidal heat | perihelion-summer/aphelion-freeze cycle, seasonal atmospheric collapse, **tidal heating** (with primary), perihelion outgassing | from orbit — **wire a time-varying flux term + a tidal term** |
| Orbital period (s) | 🟡 `OrbitDB.OrbitalPeriod:92`; read by `IsTidallyLocked` + to seed LoD, **not** by any weather model | season length, frost-cycle cadence, magnetotail dose cadence | Kepler from a + M★ (already computed) |
| **Orbital phase / true anomaly** (time-varying) | 🔴 not read as an environment driver | *when* a moon crosses its primary's magnetotail (dose swing), eclipse-by-primary thermal dips | from orbit at tick time (needs the time-varying layer) |
| Proximity to parent primary (moon orbital radius, in primary-radii) | 🔴 no cross-body read | **tidal heating** (Io/Europa), trapped-belt bombardment, synchrotron radio-jam, megatides | from the moon's orbit about its giant |
| Parent primary mass + magnetic field (cross-body read) | 🔴 no moon-side read of its parent; **and the giants' `MagneticField` is currently 0** | tidal-heat amplitude, trapped radiation belt, synchrotron noise | read the parent body at gen (needs the parent's field *populated* first) |

### 2.3 Bulk / geophysical (what the body is made of)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Body mass (kg) | ✅ for procedural atmo/tectonics (`:1634`); 🟡 for weather/heat | escape velocity → atmosphere retention; radiogenic heat; giant-vs-terrestrial gate | authored (Sol) / rolled (`MassVolumeDB.MassDry:17`) |
| Body radius (m) | ✅ for grid sizing; 🟡 for climate | scale height (with g,T), latitude spread, tidal differential (megatides), **tidal-heat ∝ R⁵** | authored / mass+density |
| Surface gravity (m/s²) | 🟡 `SystemBodyInfoDB.Gravity:107` (read only by the gas selector + colony cost) | derived pressure = column-mass × g; scale height; dust/sand saltation threshold; droplet fall | derived from mass+radius (`MassVolumeDB.SurfaceGravity:78`) |
| Density / rock-iron-ice fraction | 🟡 `MassVolumeDB.DensityDry_gcm:45` | thermal inertia (diurnal damping); tidal-response (rock vs ice); core state | authored / mass+volume |
| Radioactive-element fraction (U/Th/K) | 🔴 no representation | radiogenic internal heat → geothermal, subsurface-ocean warmth, rogue-world temp floor | **derive** from composition class + mass |
| BodyType (terrestrial/moon/dwarf/gas-giant/ice-giant/gas-dwarf) | ✅ `SystemBodyInfoDB.BodyType:26` → `HasSurface` gate (`:48,62-64`) | surface-vs-aerial gate; gas-dwarf reducing air | authored / classified from mass+density+distance |

*(Note the verifier correction: a separate **per-body formation age** is NOT needed — planets co-form with their star, so use `StarInfoDB.Age`; inter-body radiogenic variance comes from mass + the radioactive fraction. And **core-fraction/dynamo** is dropped as a required input — see §5.)*

### 2.4 Thermal (how hot, where, and when)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Bond albedo (0–1) | 🟡 `SystemBodyInfoDB.Albedo:33` — feeds temp via `UpdateAtmosphere:63`, **but that never runs on the authored path, so authored albedo is inert** | scorched dayside vs snowball; runaway greenhouse; permanent cloud deck | should derive from ice + cloud/aerosol (from the inventory) |
| **Internal heat flux (radiogenic + tidal)** (W/m²) | 🔴 base temp is 100% insolation — a tidally-heated moon reads as cold as a dead rock | molten/lava surface (Io), geothermal zones, **subsurface ocean**, cryovolcanic plumes, rogue-world temp floor, giant convective storms | **derive** = radiogenic(mass·age·radio-frac) + tidal (see §5 for the correct scaling) |
| Heat-redistribution efficiency | 🔴 temperature is one scalar; heat transport can't be expressed | day=night uniformity on thick air; terminator-band width; whether the nightside collapses | derive from pressure + hydrosphere |
| **Per-region (latitude + day/night) temperature FIELD** (K/region) | 🔴 ONE scalar for the whole body; `PlanetRegionsDB`/`SurfaceGrid` carry latitude but no temperature varies across it | latitudinal banding, tidal-lock hot/cold poles + terminator ribbon, polar night/midnight sun, local hot spots, **"which region do I land in" as a real choice** | **derive** from insolation geometry (tilt+rotation+tidal-lock) × redistribution |
| Surface thermal inertia | 🔴 derivable from density/regolith but no term | diurnal-swing damping, seasonal moderation | derive from density + surface composition |

### 2.5 Atmosphere (the air, and where it comes from)
| Variable | State | Enables | How generated |
|---|---|---|---|
| **Per-species VOLATILE INVENTORY** (kg of H₂O, CO₂, CO, CH₄/C₂H₆, N₂, biogenic O₂, SO₂/H₂S, NH₃, HCl/HF, noble gases) | 🔴 **the single biggest gap** — zero representation; composition is rolled at random (`SelectGases:1679`), so it can never reproduce a known world | correct composition (Venus 96.5% CO₂, Titan N₂/CH₄, Earth N₂/O₂), derived pressure, *which* liquid the hydrosphere is, greenhouse term, every toxic/corrosive/flammable/reducing/inert flag, cloud/haze composition | **derive** from body composition class + formation distance/temperature (condensation sequence) — ~25 environments cascade from this |
| Per-gas physical-property table (boil/melt/triple/**critical** point, molar weight, toxicity, flammability limit, greenhouse strength, corrosivity) | 🟡 partial — `atmosphereGases.json` already has boil/melt/weight/greenhouse (greenhouse code reads boil only); no critical point, no toxicity/flammability | phase state (which volatile is liquid/ice/gas → hydrocarbon/ammonia oceans, CO₂ frost, supercritical), toxic/inert/breathable distinction, flammable air, corrosive acid | static data — extend the existing gas JSON |
| Volatile phase-state resolver (surface T/P vs each species' phase diagram) | 🔴 no per-species liquid/ice/gas check (greenhouse checks one boiling point) | methane/ammonia/water ocean selection, global ice, CO₂ frost caps, frozen/collapsed atmosphere, rain of water OR methane | a **derivation**, not a stored field |
| Exospheric (upper-atmosphere) temperature | 🔴 retention faked via `massRatio²` (`:1619`) | Jeans escape → trace-vs-thin-vs-dense retention, atmospheric stripping | derive from insolation + composition |
| Atmospheric column mass (→ derived pressure) | 🟡 `AtmosphereDB.Pressure:17` is read but **authored, not computed** | crushing (Venus 92 atm), thin/trace/vacuum, supercritical, buoyancy band | **derive** = retained inventory × gravity, replacing the typed-in number |
| AtmosphericDust | 🟡 `SystemBodyInfoDB.AtmosphericDust:80` — a real field the dust-storm branch ignores (it keys off `HydrosphereExtent<10`) | dust storms tied to *actual* dust, not just dryness | authored / rolled — **wire it into the dust branch** |

### 2.6 Hydrosphere / volatiles (oceans, ice, rain — of what?)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Hydrosphere extent (% + which volatile) | ✅ read but **rolled randomly** (`RNG>0.75`, `:1608-1612`) divorced from whether the liquid is even stable | water ocean/waterworld, hydrocarbon lakes, ammonia ocean, acid sea, desert (extent→0), cyclones (warm ocean), salt flats | **derive** from the volatile inventory + phase check |
| Salt / freezing-point-depression content | 🔴 no dissolved-solute term | subsurface brine / recurring slope lineae, evaporite salt flats | derive from surface mineralogy |

### 2.7 Magnetosphere / radiation (the invisible weather)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Body magnetic field strength (µT) | 🟡 `SystemBodyInfoDB.MagneticField:57` — read only by procedural atmo thickness (`:1667`), **never by hazard gen; on the authored path never read at all** | unshielded surface radiation, aurorae, planetary Van-Allen belt, EM storm, long-term atmosphere retention | **keep authoring it** (see §5); wire a surface-radiation shield term |
| Atmospheric column shielding (Pressure × Gravity) | 🔴 the two inputs exist but are never combined into an overburden | GCR attenuation, SEP/flare surface dose, UV attenuation — *why Venus shields with no dipole* | derive at gen from pressure × gravity |
| Surface radiation dose (`RadiationLevel`) — a DERIVED OUTPUT, currently unfilled | 🟡 `SystemBodyInfoDB.RadiationLevel:73` **hard-set to 0** (`:1345`); `HazardEffectType.RadiationDamage` exists but no env is emitted | GCR bleed, SEP exposure, flare/proton pulse, trapped-belt, UV sterilization, heliopause jump | **derive** = (stellar flux/flares) attenuated by (field + column) + (parent-belt for moons) |
| Ozone / UV-absorber flag | 🟡 derivable from Composition (O₂/O₃) but unread for UV | UV sterilization gate (thin/ozone-free = biocidal) | derive from the inventory (free O₂ → O₃) |
| Heliopause / astrosphere radius (AU) | 🔴 no astrosphere boundary; GCR flux has no distance dependence | outer-frontier "it gets worse out here" GCR step-up | derive from stellar luminosity/wind at system gen |

### 2.8 Surface / geology (the ground itself)
| Variable | State | Enables | How generated |
|---|---|---|---|
| Tectonic / volcanic activity level | ✅ `SystemBodyInfoDB.Tectonics:41` → Ash Storm (`:73,83`), mountains | volcanic ash/plume, sulphur source for acid air, geothermal zones, mountain terrain, dust supply | **derivable** from mass+star-age (`GenerateTectonicActivity:1361`) but **hand-authored on the Sol path** (Mercury/Mars wrongly "earth-like") — wire the derivation into the authored path |
| Axial tilt / obliquity (°) | 🟡 `SystemBodyInfoDB.AxialTilt:49` — **ZERO downstream reads** except client display; fully dead for weather | seasons, latitudinal banding, polar night/midnight sun, seasonal caps, monsoon | **genuinely-FREE input — keep authoring**; build the seasonal/latitude consumer |
| Rotation period / LengthOfDay (s) | 🟡 `SystemBodyInfoDB.LengthOfDay:100` — read by solar panels + tidal-lock cost, **not** any weather | extreme diurnal swing, cyclones/vortices (Coriolis), super-rotation banding, tidal-lock detection | **genuinely-FREE — keep authoring**; build the diurnal/Coriolis consumers |
| Tidal-lock state | 🟡 `IsTidallyLocked` **already computed** (`OrbitExtensions.cs:106`) — drives only a colony-cost discount | eyeball world (permanent dayside), antistellar cold trap, terminator twilight band, katabatic winds, nightside collapse | derived (already); wire into the per-hemisphere temperature field |
| Topography / elevation relief | 🟡 `WorldTerrain`/`PlanetRegionsDB` produce mountains/coast but elevation isn't fed to any weather gen | katabatic winds, basin fog/mist, coastal flooding, sandstorm channeling | derive from tectonics + hydrosphere (partly produced) |
| **Impactor / system-debris flux** | 🔴 no representation; `HazardEffectType.KineticDamage` exists (`HazardEffect.cs:22`) with no derivable source | meteorite/micrometeorite bombardment on airless/thin worlds | **derive** from system age + local debris/ring density, shielded by the atmospheric column |
| Regolith / fine-dust availability | 🟡 implicit in terrain; not a first-class weather input | electrostatic dust levitation (airless "dust fountains"), dust-storm supply | derive from surface age + composition |

---

## 3. The environment → variables matrix — what's derivable today

Every environment a body in space can incur, its required variables, and whether the generator can **derive** it now. **Almost nothing is derivable today** — that's the finding, and the reason for the hand-editing.

### Atmospheres & pressure
| Environment | Derivable now? |
|---|---|
| Airless vacuum / volatile-stripped surface | 🟡 the vacuum *gate* works (`Pressure<0.05`), but *why* it's airless (retention) isn't derived — needs volatile inventory + exospheric-escape |
| Trace / thin low-pressure atmosphere | 🔴 needs volatile inventory + exospheric temp; retention is faked by `massRatio²` |
| Earth-normal temperate atmosphere | 🔴 needs inventory (incl. biogenic O₂) + derived pressure — must be authored |
| Dense crushing atmosphere (Venus 92 atm) | 🔴 pressure is literally typed in; needs inventory → column-mass × gravity |
| Supercritical fluid atmosphere | 🔴 needs a **critical-point** column in the gas table + derived P/T |
| Gas-giant H₂/He aerial envelope + pressure gradient | 🟡 the surface gate correctly excludes giants; the *aerial* profile is unmodeled (deferred) |
| Inert suffocant atmosphere (CO₂/N₂) | 🔴 needs inventory (a suffocant needs O₂ supply, not sealing) |
| Toxic atmosphere | 🟡 fires on a name-match; true toxicity needs per-gas toxicity flags + quantities |
| Corrosive acid atmosphere / acid rain / acid sea | 🟡 the *air* branch works by name-match; acid **rain/sea** need the phase model + inventory |
| Reducing (H₂/CH₄/NH₃) atmosphere | 🔴 no reducing-vs-oxidizing notion; needs inventory |
| Flammable / explosive atmosphere | 🔴 needs inventory + flammability flags |

### Weather (dynamic)
| Environment | Derivable now? |
|---|---|
| Global dust storm | 🟡 fires off `hydro<10%`+air, but ignores the `AtmosphericDust` field, gravity, season, rotation |
| Sandstorm / haboob | 🔴 needs gravity-as-input, thermal-gradient wind, topography |
| Volcanic ash storm / plume fallout | 🟡 fires off live `Tectonics`; tidal-heated-moon volcanism (Io) not derivable (no internal heat) |
| Thunderstorms & lightning | 🟡 fires off `pressure>5 atm` only; convection/moisture/rotation unread |
| Super-rotation & zonal banding | 🔴 needs rotation-as-input + spatial thermal field |
| Cyclones / hurricanes / hypercane | 🔴 needs rotation + latitude field + moisture from inventory |
| Giant vortex / supersonic winds / diamond hail | 🔴 deferred aerial branch |
| Water rain/snow · hydrocarbon (methane) weather & lakes · ammonia clouds/ocean | 🔴 need inventory + phase check (hydrosphere is water-only today) |
| CO₂ frost / dry-ice snow & seasonal cap | 🔴 needs seasonal/latitude thermal field (tilt+eccentricity unread) |
| Photochemical haze / organic smog / permanent cloud deck | 🔴 needs inventory + UV flux + circulation |
| Ground fog / mist belt · katabatic downslope winds | 🔴 need rotation, topography feedback, spatial gradient |

### Thermal & orbital
| Environment | Derivable now? |
|---|---|
| Runaway-greenhouse furnace (Venus regime) | 🟡 the greenhouse *formula* reproduces Venus IF composition+pressure are authored; the runaway multiplier lives only in procedural gen and the formula never runs on the authored path — so it's hand-authored |
| Molten metal / rock / silicate-glass rain | 🔴 engine tops out at the 400 °C fire threshold; no rock-vapour composition, no tidal-lock day/night field |
| Extreme diurnal thermal swing (airless day/night) | 🔴 LoD unread; `Process` is an empty stub so temperature never varies in time |
| Axial-tilt seasons + polar night / midnight sun | 🔴 `AxialTilt` has zero downstream reads; no time-varying, latitude-aware temperature |
| Eccentric-orbit climate cycle (perihelion summer/aphelion freeze) | 🔴 eccentricity read only as a time-average |
| Tidally-locked eyeball world + terminator band + antistellar cold trap | 🔴 `IsTidallyLocked` computed but drives no environment; no spatial temperature field |
| Atmospheric collapse / volatile freeze-out | 🔴 a frozen scalar can't respond to temperature; needs time-varying T + inventory |
| Snowball / global glaciation | 🔴 albedo feedback + greenhouse-from-inventory not wired |
| Steam / runaway-vapor & sublimation-outgassing storms | 🔴 needs inventory + phase model + eccentric-orbit swing |

### Radiation & magnetosphere
| Environment | Derivable now? |
|---|---|
| Unshielded surface radiation (weak magnetosphere) | 🔴 `MagneticField` unread by hazard gen; `RadiationLevel` hard-set 0; no radiation branch |
| Trapped radiation-belt bombardment (Jovian moon) | 🔴 no cross-body read of the parent's field; **and the giants have no field authored** |
| Stellar-flare / proton-storm surface exposure | 🔴 flare activity is space-side only; not derived from spectral/age |
| Aurorae · ionospheric/geomagnetic EM storm · synchrotron radio-jam near a giant | 🔴 no consumer reads the field; cross-body parent read missing |
| Hard-X-ray / magnetar flux (exotic) · heliopause GCR jump | 🔴 no exotic-remnant flag; no astrosphere-radius derivation |

### Geology & impacts (the catalogue the first pass missed — added in verification)
| Environment | Derivable now? |
|---|---|
| Tidal-flexing volcanic surface (Io) | 🔴 no tidal-heating term (eccentricity + primary proximity unread for heat) |
| Ice-shell / subsurface ocean moon + cryovolcanism | 🔴 no internal-heat term; surface-only scalar |
| Seismic / tectonic ground-shaking (quakes, tidal quakes) | 🔴 driver (`Tectonics`+internal heat) exists but no quake environment is emitted |
| Meteorite / micrometeorite impact bombardment | 🔴 `KineticDamage` effect exists (`HazardEffect.cs:22`) but has no derivable source (needs impactor flux) |
| Electrostatic dust levitation (airless-body dust fountains) | 🔴 structurally impossible today — every dust env requires an atmosphere; the airless case needs no-atmosphere + UV charging + fine regolith |
| Desiccated desert / salt flats / brine seeps | 🔴 needs water inventory + salt term |
| Global-ocean (waterworld) storms + megatides · coastal/seasonal flooding | 🔴 need water inventory + rotation + tidal cross-body read + latitude field |

---

## 4. The prioritized gap — the direct answer, ranked by how much each input unlocks

If you add the inputs in this order, the hand-editing collapses fastest:

**#1 — Per-species VOLATILE INVENTORY (+ the gas-property table extension + a T/P phase resolver).** The single largest lever. Add "how much of each volatile the body holds," and you **derive** the atmosphere composition, the total pressure (= column-mass × gravity), the hydrosphere extent *and which liquid it is*, the greenhouse term, and every toxic/corrosive/flammable/reducing/inert flag. **~25 environments** cascade from this — every atmosphere type, every ocean/ice/rain type, crushing pressure, supercritical, haze. Half the gas-property table already sits unused in `atmosphereGases.json`; extend it with critical/triple points + toxicity/flammability. *(Failure B — the master new gauge.)*

**#2 — INTERNAL HEAT (radiogenic + tidal).** Base temperature is 100% insolation today, so a tidally-heated moon reads as cold as a dead rock. Add an internal-heat floor and you unlock **~7 outer-system environments**: Io's volcanism, cryovolcanic plumes, subsurface/ice-shell oceans, geothermal zones, giant convective storms, the temperature floor on rogue/outer worlds. Needs three cheap inputs (eccentricity — already stored; proximity-to-primary + a cross-body read of the parent's mass; a radiogenic term from mass·age·radioactive-fraction). **Get the scaling right** (§5). *(Mostly Failure B.)*

**#3 — A SPATIAL (per-latitude + day/night) TEMPERATURE FIELD, driven by AXIAL TILT + ROTATION.** The engine assigns one scalar to a whole body and never varies it in time. Add the field + wire tilt/rotation/eccentricity/tidal-lock into it and you unlock **~15 environments**: seasons, latitudinal banding, polar night, extreme diurnal swing, tidally-locked eyeball worlds + the habitable terminator ribbon, atmospheric collapse, CO₂-frost caps, cyclones, katabatic/monsoon weather. Tilt/rotation/tidal-lock are all **stored-but-unread** — the field is the new build; the drivers are one-line reads. *(Mixed — the field is Failure B, the drivers Failure A.)* **This is the one that makes "which region do I land in" a real decision** — it ties straight into the planet-view + ground-combat work.

**#4 — RADIATION as a first-class DERIVED quantity.** Read `MagneticField` + atmospheric-column shielding + stellar spectral/age/distance into a surface-dose derivation that finally fills `RadiationLevel` (0 today) and drives a radiation branch in `PlanetEnvironmentFactory`; add a cross-body read of a parent giant's field for trapped belts. Unlocks **~8 environments** (unshielded radiation, Jovian belts, flare/proton storms, aurorae, EM storms, synchrotron sensor-jam, heliopause). Mostly Failure A (the inputs are stored), plus the giants need a field *populated*. *(Ties into the espionage/detection substrate — radiation is a sensor and combat modifier.)*

**#5 — SURFACE-GEOLOGY / SOLID-BODY inputs.** Impactor/debris flux → meteorite bombardment (the existing `KineticDamage` effect finally gets a source); a quake environment off `Tectonics`+internal heat; airless electrostatic dust levitation; topography → katabatic/fog/flood. Smaller but rounds out the "solid ground is also hostile" catalogue.

**The cross-cutting unblock (do this regardless):** wire the derivations the engine already owns into the **authored path** (§1), following the `BaseTemperature` fix-pattern, so the Sol bodies fall out of physics instead of JSON. This is what actually kills the hand-editing.

---

## 5. Corrections the verification forced (don't skip — the first pass had real errors)

1. **Insolation is NOT unread.** `BaseTemperature = T★·√(R★/2d)` is the Stefan-Boltzmann equilibrium temperature ∝ (L/d²)¼ — so the generator **already** reads insolation via star temperature + radius + distance. Luminosity's genuinely-new work is the **seasonal / time-varying / spatial** flux, not the baseline. (Don't "add an insolation term" — it exists.)
2. **The tidal-heating formula was wrong.** Not `eccentricity × primary-mass/distance³ × body-mass`. Real tidal heating scales as **Ė ∝ (k₂/Q)·e²·R_body⁵·M_primary^~2.5 / a^~6** — body **radius⁵** (not mass¹), and it needs a **dissipation/rheology term** (k₂/Q, or a rock-vs-ice class). Without R⁵ and dissipation, Io-vs-Callisto is un-derivable. Use the correct scaling or an explicitly-flagged game proxy.
3. **Magnetic field stays AUTHORED — do not derive it from a new core/dynamo model.** That was the least-sound proposal. Keep `MagneticField` in the same "genuinely-free, keep authoring" tier as `AxialTilt` and `LengthOfDay` (all three are chaotic products of formation/history). Drop the "core fraction" new field. **But:** the Sol giants (`jupiter.json`/`saturn.json`) currently author **no** field, so before Jovian radiation belts can work, the giants' fields must be **populated** (author them, or run a coarse by-type estimate for giants only).
4. **Drop the per-body formation age field.** Planets co-form with their star, so use `StarInfoDB.Age`; inter-body radiogenic variance comes from mass + radioactive fraction. (Reintroduce per-body age only if captured/rogue interlopers ever become content.)
5. **The volatile inventory has two forms.** INITIAL inventory is derivable from the condensation sequence (great for **procedural** worlds). PRESENT composition is an *evolved* endpoint (escape, biology, outgassing over Gyr). For the hand-authored Sol bodies you either **keep an authored composition override** or add a coarse atmospheric-evolution stage. So #1 fully auto-derives new worlds; known worlds keep an override or an evolution pass. (This is the same override-vs-derive choice as `SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md`.)
6. **Wire `AtmosphericDust`** (`SystemBodyInfoDB.AtmosphericDust:80`) — a real field the dust-storm branch ignores.

---

## 6. Data bugs found in the authored Sol JSON (fix these regardless of scope)

The forensics surfaced silent authoring errors that partly explain the "edit a bunch" pain:

- **`"HyrdoExtent"` misspelled** on `mars.json` / `titan.json` / `saturn.json` — the loader reads the correct key, so these bodies silently read **hydrosphere 0**. (This is the class of bug root `CLAUDE.md` gotcha #10 is about — a reference that doesn't bind fails silent.)
- **Mercury and Mars authored `Tectonics: "earth-like"`** — wrong (both are geologically near-dead); the value drives ash-storm generation + mountain terrain, so they generate hazards they shouldn't.

---

## 7. Open developer decisions (your calls before any build)

1. **Replace vs override.** Wire the derivations into the authored path so Sol falls out of physics (kills hand-editing) — OR keep authoring as an *override* and only add derivation for procedural worlds? The `BaseTemperature` fix-pattern supports either.
2. **Runaway greenhouse.** The calibrated formula (0.035 constant, ±3.0 clamp) can't reach Venus 464 °C; the runaway multiplier lives only in procedural gen. Add a per-gas greenhouse/runaway term to the temperature formula, or keep Venus as an authored special case?
3. **Spatial temperature field — now or later?** It's the biggest new build but unlocks ~15 environments (incl. the red-dwarf eyeball-world premise). Ship a whole-body scalar first, or build the per-latitude/day-night field now? (`PlanetRegionsDB`/`SurfaceGrid` already carry latitude to hang it on.)
4. **Time-varying weather.** Diurnal/seasonal cycles, frost sublimation, atmospheric collapse need `AtmosphereProcessor.Process` (empty stub today) to actually recompute per tick. Is per-tick atmospheric evolution in scope, or are these generated as **static state labels** at gen time only?
5. **Exotic content.** Magnetar/neutron-star hard-X-ray, flare-star variability — in scope this pass, or parked as late-game/crisis flavor until the core habitable catalogue derives cleanly?
6. **Cross-body reads.** A moon reading its parent giant's mass + field (tidal heat, belts) is new plumbing — confirm generation order builds the primary before its moons.

---

## 8. Provenance

10-agent research workflow (2026-08-09): a five-domain environment catalogue (the geology/solid-body domain was recovered in verification after its agent failed), two source-forensics ledgers (engine variable inventory + the Sol hand-edit gap), a synthesis pass, and three adversarial verifiers (completeness, derivation-soundness, engine-accuracy). The engine-accuracy verifier confirmed every EXISTS/MISSING label against HEAD with zero inversions; the completeness verifier added the 5th catalogue + the orphan environments; the derivation-soundness verifier corrected the insolation, tidal-heating, magnetic-field, and per-body-age claims folded into §2/§5. Nothing here is built — this is the input-surface spec that a future generation pass derives against.
