# What all the premade units would cost

**As of 2026-08-06.** The 14 units we've built — the 7 clickable assembler presets + the 7 franchise dossier units —
priced on both surfaces: **build** (one-time) and **run** (ongoing). Numbers come from the assembler's own `compute()`
where a unit is a preset or maps cleanly to the catalog; ground units are on the real base-mod scale (see the method
note). Companion to the run-cost work in `CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md §8`.

## Method + confidence (read this — the numbers live on two different scales)

- **Exact (mock basis).** The 7 presets are run through the assembler's real `compute()`/`materialBill()`. These are
  the tool's design-model numbers (internally consistent; not the engine's per-template JSON), comparable to each other.
  **Credits are in thousands (k); materials in refined tonnes; upkeep = mass × 0.1 /month** (the tool's ground-unit
  rate, host-aware — see the run-cost surface).
- **Mapped-approx (mock basis).** ARC-170 and Miranda aren't presets, so their dossier loadouts were mapped to the
  nearest catalog parts and run through `compute()`. ⚠ The catalog is **capital-scale** (a "Laser Cannon" is 80 t), so
  the **ARC-170 over-reads badly** — the real fighter is ~22 t, the mock floors it at ~1,800 t. Treat ARC-170's mass as
  "smallest the mock can express," not the canon fighter.
- **Base-mod scale (ground).** The 5 ground units are built from base-mod ground parts (frames/weapons/armour/augments),
  a **different, much smaller cost system** than the ship catalog — you cannot sum a clone squad and a Star Destroyer
  into one meaningful total. Their exact credits need the engine (or building them as presets, tasks #31/#32); here they
  carry composition, squad size, and the mass-derived cost **shape**.

---

## Table 1 — ship-scale units (9), assembler-model basis

### Build (one-time)
| Unit | Host | Mass (t) | Build-pts | Credits | Materials (t) | Tech | Confidence |
|---|---|--:|--:|--:|--:|:--:|:--:|
| ARC-170 | ship (fighter) | 1,803* | 3,110 | 339k | 2,074 | RP6 | mapped-approx* |
| Escort destroyer | ship | 4,448 | 7,640 | 803k | 5,776 | RP6 | exact |
| Miranda-class | ship (frigate) | 3,444 | 6,120 | 700k | 3,972 | RP6 | mapped-approx |
| Prometheus | ship | 13,855 | 22,960 | 2.74M | 17,274 | RP7 | exact |
| Acclamator | ship | 18,249 | 23,250 | 2.75M | 18,114 | RP7 | exact |
| Sovereign | ship | 19,685 | 30,720 | 3.57M | 22,788 | RP7 | exact |
| Venator | ship | 19,986 | 28,860 | 3.25M | 22,748 | RP7 | exact |
| Imperial-I Star Destroyer | dreadnought | 57,684 | 91,780 | 11.36M | 66,266 | RP7 | exact |
| **DS-1 Death Star** | megastructure | **29,986,810** | **179,738,960** | **34.35B** | **169,783,096** | RP12 | exact |

\* ARC-170 mass over-reads ~80× (the mock catalog can't express a 22 t fighter); its true build cost is a small
fraction of the Escort.

### Run (ongoing, per month)
| Unit | Crew/jobs | Power draw | Upkeep | Food |
|---|--:|--:|--:|:--:|
| ARC-170 | 61 | 13 / 75 MW | 180 cr | −61 cd/day |
| Escort destroyer | 142 | 39 / 225 MW | 445 cr | −142 cd/day |
| Miranda-class | 135 | 24 / 150 MW | 344 cr | −135 cd/day |
| Prometheus | 617 | 133 / 800 MW | 1,386 cr | −617 cd/day |
| Acclamator | 726 | 153 / 2,400 MW | 1,825 cr | −326 cd/day |
| Sovereign | 800 | 187 / 4,000 MW | 1,969 cr | closed loop |
| Venator | 884 | 223 / 3,200 MW | 1,999 cr | closed loop |
| Star Destroyer | 3,420 | 1,068 / 2,400 MW | 5,768 cr | −2,620 cd/day |
| Death Star | 414,266 | 187,563 / 200,000 MW | ~3.0M cr | closed loop |

**Ship totals (all 9):** ~30.1M t · **179.9M build-pts** · **~34.4B credits** · ~169.9M t materials · ~421k crew ·
189 GW · ~3.0M cr/mo upkeep. **The Death Star is 99.9% of it.** The eight *ships* without it: ~139,000 t ·
**~194,000 build-pts** · **~25.5M credits** · ~159,000 t materials — the Death Star alone is **~1,400× the eight
ships combined.** That's the megastructure tier working as designed.

---

## Table 2 — ground units (5), base-mod scale (a different, much smaller world)

Ground units are assembled from base-mod ground parts and priced by **mass** (materials ≈ mass, build-points ≈
mass × ~3) plus the **live** upkeep (`Mass × 0.1 /month`, billed by `GroundUpkeep`), all **× `UnitsPerBuild`** (the
squad/brood multiplier — one build order stamps out the whole squad and multiplies its cost).

| Unit | Composition (parts) | Squad (`UnitsPerBuild`) | ~mass/unit | Scale |
|---|---|:--:|--:|---|
| **Termagant** (Tyranid chaff) | swarm-frame + fleshborer + rending claws + chitin + adrenal augment | **×20** (a brood) | ~28 | the cheapest thing in the game — near-free, thrown in numbers |
| **Clone Trooper** | human-frame + DC-15A blaster + thermal detonator + ablative plate + **sealed systems** + **training cadre** | **×9** (a squad) | ~266 | line infantry; the seal + cadre are the pricey bits |
| **Genestealer** (elite) | human-frame + hyper-musculature + bio-drive + chitin + void-seal + radar + instinct-cadre | **×8** (a brood) | ~1,074 | elite vanguard — ~4× a clone per body (heavy augments) |
| **Carnifex** (monster) | walker vehicle-frame + 2× chitin carapace + 2× talons + bio-cannon + musculature + locomotion + bio-seal | **×1** | ~3,400 | a bio-tank — the heaviest ground unit |
| **AT-TE** (gun-walker) | walker-frame (dialed ~3,000 str) + railgun mass-driver + 6× AP laser + reactor + magazine + 4× heavy plating + locomotion | **×1** | ~3,000–4,000 | a heavy assault-walker; reactor + ammo + a wall of armour |

**The whole point:** a ground unit costs a **rounding error** next to a warship. One clone squad (~2,400 mass total)
or a full Tyranid brood is a tiny fraction of the Escort, let alone a capital. An entire sampler army — one of each of
the five, squads included — comes to roughly **one Acclamator's** worth of mass. Ground forces are cheap to raise; the
expensive decisions are the capitals and (astronomically) the megastructure.

*(Ground mass-units aren't cleanly tonnes — the assembled-part scale differs from the monolithic base-mod templates —
so these are shape/scale figures, not exact credits. Exact per-unit credits need the engine or the units built as
presets.)*

---

## The one-line answer

- **The Death Star dwarfs everything** — 34.35 **billion** credits, ~180M build-points, ~170M t of materials, alone.
- **The eight warships together** are ~25.5M credits · ~194k build-points · ~159k t materials — the whole fleet is
  **1/1,400th of the one battle station.**
- **The ground units are almost free by comparison** — a squad or a brood is a fraction of a single escort.

*The costs are the tool's design model (calibrated + internally consistent), not the engine's shipped per-template
numbers; the run-cost column is "what it would cost once the run-cost vector is wired" (`ENGINE-WIRING-BACKLOG` TIER
2.5). To turn the ground + ARC-170/Miranda estimates into exact numbers, build them as assembler presets (tasks
#31/#32) or run them in-engine.*
