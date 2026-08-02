# The Death Star, built by the playbook — the capstone

> **What this is.** The first build produced end-to-end by `DESIGNER-DRIVER-PLAYBOOK.md`, run at the top of the
> scaling ladder: a **megastructure**. It proves the method holds from a corvette to a moon-sized battle station —
> and it delivers the playbook's hardest lesson in the cleanest possible form: **the one capability that makes the
> Death Star *the Death Star* is the one the engine cannot express.** Built honestly, that's not a failure — it's
> the finding.
>
> **This is REPRODUCTION mode** (a named object, sourced to canon), at a scale that required a new host tier.

## Step 0 — Objective & role stack
A moon-sized battle station whose purpose is **strategic terror**: destroy a world, and be an unassailable base
while doing it. Stack: **① destroy a planet (the superlaser) → ② overwhelm any fleet (15,000 weapon emplacements)
→ ③ be unkillable (immense armour, shields, a 7,000-fighter screen) → ④ relocate strategically (a hyperdrive) and
sustain itself forever (its own reactors, food, and a crew city).** Sacrifice: mobility and agility — it never
dodges; it doesn't need to.

## Step 1 — Resolved axes
- **Host & scale:** a new **Battle Station (megastructure)** host — immobile in a fight (Evasion 0), FTL between,
  budget in the tens of millions of tonnes.
- **Threat model:** planets (the superlaser), capital fleets (turbolasers + ion cannons), starfighters (a
  point-defense screen + its own TIE wing). Attacked by fleets and — canonically — by a single fighter run on a
  reactor vent.
- **Doctrine:** a fortress. Overwhelming firepower + armour + shields + a fighter screen; no evasion.
- **Endurance:** a self-sufficient city — hypermatter reactors + agriculture + life-support → effectively unlimited.
- **Survivability:** shields + immense armour + ECM + the fighter screen (its famous weakness — a single-point
  reactor vent — is a vulnerability the engine doesn't model; noted, not built).

## The canon (sources at the bottom)
| System | Count |
|---|---|
| Concave Dish Composite Beam Superlaser | **1** |
| Heavy turbolasers (Taim & Bak XX-9) | **5,000** |
| Turbolaser batteries (Taim & Bak D6) | **5,000** |
| Ion cannons (Borstel MS-1) | **2,500** |
| Laser cannons / point-defense (Borstel SB-920) | **2,500** |
| Tractor beam emplacements | **768** |
| TIE fighters | **~7,000** |
| Crew | **~1,200,000** |
| Reactor / hyperdrive | hypermatter core · Class-4 |

## Steps 2–3 — Seven new mega components (catalog 34 → 41)
| Added | Door | Reaches the sim? |
|---|---|---|
| **Superlaser** | Weapons | ⏳ **PENDING** — the engine has no planetary-destruction mechanic; no reader for a "crack a world" output. **The headline.** |
| **Ion Cannon** | Weapons | ✅ **LIVE** — Exotic weapon nature; shields can't soak Exotic at all (the weapon triangle), so it punches through |
| **Hypermatter Reactor** | Power | ✅ **LIVE** — `EnergyGenerationAtb` at 8,000 MW each; ~10-year fuel core |
| **Radiator Tower** | Power | ✅ **LIVE** — the heat-throttle mechanic at 4,000 MW each |
| **Habitation Block** | Crew | ⚠ berths 25,000 — the crew-berthing gate at city scale (ship crew isn't engine-gated; modeled as the real need) |
| **Life Support Complex** | Crew | ⚠ 30,000 air/water — colony `PopulationSupportAtbDB` is LIVE; per-hull modeled as the gate |
| **Agriculture Dome** | Crew | ✅ **LIVE** — `FoodProductionAtbDB` at 50,000/dome; closes the food loop so it never runs down |

*The Battle Station is a **superset host** — it mounts everything a ship or a station can, plus these seven.*

## Step 4 — It closes (every supporting gate green)
| Readout | Value |
|---|---|
| Structural budget | **23,986,810 / 50,000,000 t** ✓ |
| Volume | 18,292,990 / 40,000,000 m³ ✓ |
| Power | **187,563 / 200,000 MW** ✓ (25 hypermatter reactors — 94% loaded; the superlaser's 120 GW charge draw is the bulk, so the station is genuinely built around feeding it) |
| Heat | **+190,000 MW** margin ✓ (90 radiator towers) |
| **Firepower** | **88,550 MJ/s** (5,000 heavy + 5,000 medium turbolasers + 2,500 ion cannons + 2,500 PD, ×1.40 targeting+fire-control — **the superlaser is *not* in this**, it's strategic) |
| Toughness | ~66,900 (40,000 base hull + 500 armour plates + the mass of component HP) |
| Crew | **304,266** — berths **625,000** ✓ · life support **600,000** ✓ |
| **Deployment** | **3,650 d (~10 years)** on station — capped by the reactor core; **food is a closed loop** (9 agriculture domes feed 450,000 ≥ the crew) |
| Mobility | immobile sublight · **hyperdrive** (FTL) ✓ |
| Ammo | 82 min sustained |
| Detection | 830 km — sees past every gun ✓ |

*Crew (~304k) is the tool's ratio-model; canon is ~1.2M. Mass is abstract/ratio scale, not literal (a real DS-1 is
~10¹² t) — like every build here, the tool models **composition and the honesty ledger**, not a moon's physics.*

> ⚡ **Energy recalibrated to the engine (2026-08-02).** Firepower/power are now real engine units. The interesting
> result at this scale: on a small ship a reactor trivially out-supplies its guns (power headroom is huge), but the
> Death Star's **superlaser charge draw (120 GW)** makes power *genuinely tight* — 94% of its 25 hypermatter reactors.
> That's the honest story the recalibration tells: the whole station really is built around feeding the one weapon the
> sim can't yet fire.

## Step 5 — The authenticity pass (the honest ledger)
**LIVE — reaches the sim, verified:** all 15,000 conventional guns (firepower + saturation + the Exotic
shield-bypass of the ion cannons) · armour · shields · ECM (+10% hit-avoidance) · fire-control & targeting (×1.40) ·
detection · the 25 hypermatter reactors + 90 radiator towers (power & heat gates) · the crew city (berths + life
support + a closed food loop) · the 10-year deployment · the hyperdrive · **the vehicle/troop delivery** (it can
land its garrison — the invasion chain is LIVE).

**PENDING — flagged, not faked (the three that make it a Death Star):**
1. ⭐ **The superlaser.** No planetary-destruction mechanic exists in the engine — no reader for a "crack a world"
   output. **The station's entire reason for being is the one thing the sim cannot express.**
2. **The 7,000-TIE wing can't sortie** — strike-launch is a dead output (`ParasiteLauncherReady`, 0 emitters).
3. **The 768 tractor beams have no battlefield payoff** — capture is an open ruling, salvage a stub.

**Not modeled (flavor):** the thermal-exhaust-port single-point vulnerability — the engine has no such
crack-in-the-armour mechanic.

## The verdict
**You can build the Death Star as a fortress-city today, cradle to grave — a self-sufficient, decade-enduring,
88,550-MJ/s armed moon (real engine units, recalibrated 2026-08-02) — and everything that makes it *terrifying* is
an engine job that doesn't exist yet.** It can shoot any fleet to pieces and land an army; it cannot destroy a planet, launch its fighters, or drag
a ship in. That is the playbook's Law in its purest form: *a build works only through its LIVE wires, and the
honest answer to "can the sim do this?" is worth more than a dial that writes to nothing.*

**What it would take to make the superlaser real** (an engine feature, not a designer fix): a planetary-destruction
consumer — a processor that reads a superweapon output and applies it to a body (destroy/render-uninhabitable),
with the target-selection order and the political fallout. That's a genuine new system, the same class of work as
the fighter-launch reader. Until it exists, the Death Star is honestly amber.

---

*Sources: [DS-1 Orbital Battle Station — Wookieepedia](https://starwars.fandom.com/wiki/DS-1_Orbital_Battle_Station/Legends)
· [DS-1 Death Star — Star Wars RPG (FFG) Wiki](https://star-wars-rpg-ffg.fandom.com/wiki/DS-1_Death_Star)
· [Death Star — Wikipedia](https://en.wikipedia.org/wiki/Death_Star). Built 2026-08-02 via `DESIGNER-DRIVER-PLAYBOOK.md`.
Design/reference only; no engine change, no franchise assets reproduced.*
