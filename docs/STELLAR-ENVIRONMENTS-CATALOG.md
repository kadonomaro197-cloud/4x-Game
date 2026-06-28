# Stellar Environments Catalog — real systems → playable hazards

**Purpose:** the developer asked to "look into ALL the different real-life stellar systems and see what we're working with." This is that survey, mapped onto our spine: every environment becomes (a) a body/region to **render**, (b) a set of **DamageSignatures** (the unified hazard+weapon vocabulary — see `HAZARD-DISCOVERY-RESISTANCE-RESEARCH-DESIGN.md`), and (c) a **play value** (the survey→discover→research→exploit decision, and the risk/reward of going there).

**The coarse DamageSignature set (locked):** `hard-radiation` (UV/X/γ), `thermal` (IR/heat), `em-storm` (magnetic/EM — jams sensors & electronics), `gravimetric` (tidal/spacetime — the one that isn't a wavelength), `corrosive-gas` (dense medium — drag + chemical), `kinetic-debris` (impacts/wind/jets). **Headline finding: this set covers essentially the entire real catalog below** — strong validation of the coarse-start decision. What the catalog *adds* is mostly new **body types** and **transient/moving** hazard behaviours, not new signatures.

**Reading each entry:** *real basis → signatures it emits → render/body need → play value.*

---

## 1. Main-sequence single stars (the baseline — partly built)

| Star | Real basis | Signatures | Render/body | Play value |
|---|---|---|---|---|
| **O / B (blue giants)** | Massive, hot (10k–50k K), short-lived, fierce UV/X-ray + stellar wind | `hard-radiation` (high), `thermal`, `kinetic-debris` (wind) | spectral type exists; bigger/bluer glyph | High-radiation systems; rare; rich but punishing inner zones |
| **A / F / G / K** | Sun-like range | `thermal`, mild `hard-radiation` | built | The habitable baseline (Sol = G) |
| **M red dwarf** | Cool, dim, common; violent **flares** | `hard-radiation` (flare bursts), `thermal` (low) | built; flare event built | Habitable band is *close in* → you're forced into the flare danger zone (a real decision). Most common system. |
| **Brown / sub-brown dwarf** | Failed star, dim IR | very low (`thermal` faint) | new: dim/dark body | Dark, low-detection systems — **hideouts**, stealth approaches, cold mining |

*Already mostly covered by `StarInfoDB.SpectralType` + the corona/flare we built.*

## 2. Pre-main-sequence — STAR BIRTH (the developer's protostar)

| Environment | Real basis | Signatures | Render/body | Play value |
|---|---|---|---|---|
| **Protostar (T-Tauri / Class 0–II)** | Forming star inside a collapsing gas/dust envelope + **accretion disk**, violently variable | `corrosive-gas` + `em-storm` (dense dust → drag + sensor jam), `hard-radiation` (T-Tauri flares, *erratic*), `thermal`, `kinetic-debris` | **new bodies:** protostar, protoplanetary disk | **Frontier jackpot:** the protoplanetary disk is a **mining bonanza** of raw material, but blinding, corrosive, and the radiation is *unpredictable*. High reward / high chaos early-game. |
| **Herbig-Haro jets** | Bipolar relativistic jets from the protostar slamming into the cloud | `kinetic-debris` (a jet "river"), `hard-radiation` | new: jet region (a directional hazard) | A hazard you route *around* — a kinetic beam across the system |
| **Star-forming region** (Orion-type) | Many protostars in a giant molecular cloud | all of the above, system-wide | nebula + cluster | A whole frontier zone; multiple protostars; dense fog of war |

## 3. Evolved / dying stars

| Star | Real basis | Signatures | Render/body | Play value |
|---|---|---|---|---|
| **Red giant / AGB** | Bloated, strong slow **stellar wind**, dust shells, pulsating (Mira) | `thermal` (huge), `kinetic-debris` + `corrosive-gas` (wind/dust) | new: giant (huge glyph) + dust shells | A dying star that *engulfed* its inner planets; mine the shed shells; it will soon become a planetary nebula |
| **Wolf-Rayet** | Extreme fast wind, shedding mass, near supernova | `kinetic-debris` (fierce wind), `hard-radiation` | new body | A **ticking bomb** (pre-supernova) — high-energy research, get out before it blows |
| **LBV / hypergiant** (Eta Carinae) | Extreme luminosity + episodic **eruptions** | `thermal` + `hard-radiation` (extreme), eruption **events** | new body + eruption event (rides the transient mechanic) | The most dangerous *living* star to sit near; eruptions are mega-flares |

## 4. Stellar remnants (the exotic end states — the developer's neutron star, black hole)

| Remnant | Real basis | Signatures | Render/body | Play value |
|---|---|---|---|---|
| **White dwarf** | Earth-sized dense ember, hot UV; can accrete → nova | `hard-radiation` (UV/X), `thermal`; high surface gravity | spectral `D` exists; small hot glyph | Stable-ish remnant; **accreting ones are nova bombs** (§5) |
| **Neutron star / pulsar** | City-sized, ultra-dense; intense radiation + magnetic field + a **rotating beam** | `hard-radiation` (extreme), `em-storm` (magnetic), `gravimetric` (strong), pulsar-beam **sweep** (periodic) | **new body** + a rotating periodic-sweep hazard | Extreme hazard, but a pulsar is a **navigation beacon** + exotic-physics research goldmine; time the sweep |
| **Magnetar** | Neutron star with a monstrous magnetic field; **giant flares / starquakes** | `em-storm` (supreme — fries sensors & electronics), `hard-radiation` (catastrophic flare events) | new body + flare events | The ultimate **sensor/electronics-denial** environment; magnetar giant-flares are region-scale catastrophes |
| **Stellar black hole** | Collapsed core; **event horizon**, tidal **spaghettification**, lensing | `gravimetric` (extreme; horizon = a hard **instant-loss boundary**), `hard-radiation` + `thermal` (if accreting) | **new body** + horizon ring + lensing visual | The ultimate risk/reward: extreme research value, **gravity-assist slingshots**, hide in the lensing — but the horizon is instant death. `gravimetric` is the one signature needing a non-wavelength damage path. |
| **Supermassive black hole** | Galactic-centre scale | as above, extreme | new (late/special) | A capstone late-game destination |

## 5. Explosive / transient events (these RIDE the transient-hazard mechanic we built)

| Event | Real basis | Signatures | Render/body | Play value |
|---|---|---|---|---|
| **Nova** (cataclysmic variable: WD + companion) | WD accretes → periodic thermonuclear flash | `hard-radiation` + `thermal` blast, **periodic** | transient hazard (flare-like, scheduled) | Predictable-ish detonations — **time your transit** through the system |
| **Supernova** | Core-collapse or WD detonation | catastrophic, system-sterilising **event** | one-shot mega-event → leaves a remnant | A faction-scale catastrophe / a remnant is born |
| **Supernova remnant** (Crab-type) | Expanding **shockwave** + ionized filaments + heavy elements + a central NS/BH | **moving** `kinetic-debris` + `hard-radiation` **shock front**, `corrosive-gas` filaments | **new:** an *expanding* region (radius grows over time — a giant slow flare) + central remnant | A **treasure field**: rare/heavy minerals forged in the blast, but irradiated, with a central remnant and a **dynamic moving shock** to dodge |
| **Planetary nebula** | Ionized shell around a hot white dwarf | `corrosive-gas` + `em-storm`, `hard-radiation` (central WD) | shell region (we have gas-cloud) + WD | Beautiful, mineral-rich shell, moderate hazard |

## 6. Multiple-star systems

| System | Real basis | Signatures | Play value |
|---|---|---|---|
| **Binary / trinary** | 2–3+ stars (Alpha Centauri = built) | each star's signatures overlap; complex zones | Navigation complexity, two danger zones, **Lagrange-rich** (ties to the missing Lagrange-points feature) |
| **X-ray binary** (BH/NS + normal star, Cygnus X-1) | Compact object **eats** its companion → accretion disk + **relativistic jets** | `hard-radiation` (X-ray, extreme), `gravimetric`, `kinetic-debris` (jets) | A star being devoured — dramatic, high-energy; the accretion stream is a **hazard river** between the two |
| **Cataclysmic variable** (WD + star) | Mass transfer → recurrent novae | periodic `hard-radiation`/`thermal` | The nova-bomb system (§5) |
| **Contact / merging binary** | Stars touching, will merge | unstable, escalating | A system on a countdown to a merger event |

## 7. Special regions (non-stellar terrain)

| Region | Signatures | Play value |
|---|---|---|
| **Molecular / dark nebula** | `corrosive-gas` + `em-storm` (cold, dense, hides things) | Stealth/ambush terrain; **hideout**; the gas-cloud we built, dialled dense |
| **Emission / reflection nebula** | `hard-radiation` + `em-storm` (ionized) | Mineral-rich glowing clouds |
| **Globular cluster / dense field** | overlapping `hard-radiation`; navigation density | Crowded crossfire; strategic/resource density |
| **Asteroid belt / Kuiper / Oort** (belt built) | `kinetic-debris` (micrometeoroids) | Mining + the kinetic hazard that a **kinetic shield** (and ballistic-resistant armour) counters |
| **Rogue objects** (rogue planet / black hole / interstellar comet) | varies | Deep-space surprises between systems |

---

## What this catalog tells us (design conclusions)

1. **The coarse 6-signature set holds.** Across the entire real catalog, almost everything is a mix of `hard-radiation / thermal / em-storm / gravimetric / corrosive-gas / kinetic-debris`. We do **not** need a sprawling signature list — start with 6, name finer *variants* in data later. Validates the locked decision.
2. **The real work is BODY TYPES + DYNAMIC behaviours, not new signatures.** New bodies: protostar, protoplanetary disk, giant/hypergiant, white dwarf (have `D`), neutron star/pulsar, magnetar, black hole, supernova remnant. New behaviours: **moving** hazards (the SN-remnant shock — a region whose radius grows; generalises the flare lifecycle), **periodic** hazards (pulsar sweep, nova), **event** hazards (magnetar/LBV eruptions — already the transient mechanic).
3. **`gravimetric` is the one signature with no wavelength** (black hole / neutron star tidal). It needs its own damage application site (per `Hazards/CLAUDE.md`) — and a black-hole **event horizon** is best modelled as a hard instant-loss boundary, not graded damage. Flag for whenever black holes are built.
4. **Every environment is a survey→discover→research→exploit beat.** Each has a clear payoff to justify the risk: protoplanetary disk = raw materials; SN remnant = rare heavy elements; pulsar/black hole = exotic research points (the Stellar/Energy Science field); dark nebula = a hideout; binary = Lagrange real estate.
5. **Natural difficulty tiering (progression):** M/G singles (safe) → flare stars / belts / gas clouds (early hazards, built) → giants / protostars / planetary nebulae (mid) → white dwarfs / novae / Wolf-Rayet (advanced) → neutron stars / magnetars / SN remnants (hard) → **black holes / X-ray binaries** (endgame). The discover→research→armour/shield loop *is* the progression gate: you can't operate in the hard tiers until you've researched their signatures.

---

## Build implications (no code yet — feeds the foundation plan)
- **Body-type expansion** is a mostly-data job (new `StarInfoDB` types / a body category + render glyphs) — cheap relative to the payoff. Add as content once the discovery/research loop exists.
- **Transient hazard mechanic generalises** to moving (SN shock) and periodic (pulsar/nova) — small extensions of the flare lifecycle already built.
- **Sequencing:** these are CONTENT for after Phase 0–3 of the resistance/research loop. Authoring a black hole or supernova remnant is then *mostly JSON* (the spine's promise), plus the 1–2 engine additions flagged (gravimetric site, moving-region behaviour, horizon boundary).
