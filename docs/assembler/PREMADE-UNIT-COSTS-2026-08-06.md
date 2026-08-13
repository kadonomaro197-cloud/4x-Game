# What all the premade units would cost

> ⚠ **Scope caveat (2026-08-13):** this is a **2026-08-06 snapshot of 14 presets**. The assembler now has **18** — Sazabi and the three aircraft (Apache / F-22 / LAAT) landed after this snapshot and are priced only in their own build docs (`SAZABI-BUILD.md`, `AIRCRAFT-BUILDS.md`). The 14 numbers below are **not** contradicted (later commits only *added* parts/presets; existing presets compute unchanged) — the table is just incomplete. Refresh to 18 on the next assembler pass.

**As of 2026-08-06.** All 14 premade units — now **every one a clickable assembler preset** — priced on both surfaces:
**build** (one-time) and **run** (ongoing), straight from the assembler's own `compute()`/`materialBill()`. The 5 ground
units + ARC-170 + Miranda were promoted to real presets (tasks #31/#32): 15 base-mod ground parts were added to the
catalog and the ground host budget raised to seat vehicle-scale units, so ground builds now compute exactly like ships.

## Method + confidence

- **All numbers are exact** (the tool's design-model `compute()`), internally consistent across all 14 units. **Credits
  are in thousands (k); materials in refined tonnes; upkeep = mass × 0.1 /month** (the tool's rate; per the run-cost
  surface, only some of it — energy-weapon power, crew, ground upkeep — actually bills in-engine today).
- **⚠ ARC-170 mass over-reads.** The mock catalog is capital-scale (a "Laser Cannon" is 80 t), so the fighter floors at
  ~1,870 t — a real ARC-170 is ~22 t. Its true cost is a fraction of the Escort; read its mass as "the smallest the mock
  can express."
- **Ground units are on the assembler's mock scale** (their part costs calibrated to the ship catalog so they're
  comparable here). The **real base-mod** ground costs are far tinier (a base-mod trooper is ~110 raw credits) — ground
  forces are dirt-cheap in the actual engine economy; the mock scale just makes them legible beside ships.
- Ground units are priced **per unit**; the **squad** column applies `UnitsPerBuild` (one build stamps out the whole
  brood/squad and multiplies its cost).

---

## Table 1 — ship-scale units (9)

### Build (one-time)
| Unit | Host | Mass (t) | Build-pts | Credits | Materials (t) | Tech |
|---|---|--:|--:|--:|--:|:--:|
| ARC-170 | ship (fighter) | 1,873* | 3,250 | 357k | 2,194 | RP6 |
| Miranda-class | ship (frigate) | 3,804 | 6,800 | 782k | 4,556 | RP6 |
| Escort destroyer | ship | 4,448 | 7,640 | 803k | 5,776 | RP6 |
| Prometheus | ship | 13,855 | 22,960 | 2.74M | 17,274 | RP7 |
| Acclamator | ship | 18,249 | 23,250 | 2.75M | 18,114 | RP7 |
| Sovereign | ship | 19,685 | 30,720 | 3.57M | 22,788 | RP7 |
| Venator | ship | 19,986 | 28,860 | 3.25M | 22,748 | RP7 |
| Imperial-I Star Destroyer | dreadnought | 57,684 | 91,780 | 11.36M | 66,266 | RP7 |
| **DS-1 Death Star** | megastructure | **29,986,810** | **179,738,960** | **34.35B** | **169,783,096** | RP12 |

\* ARC-170 mass over-reads ~80× — the mock can't express a 22 t fighter.

### Run (per month)
| Unit | Crew/jobs | Power draw | Upkeep | Food |
|---|--:|--:|--:|:--:|
| ARC-170 | 62 | 13 / 75 MW | 187 cr | −62 cd/day |
| Miranda | 139 | 24 / 150 MW | 380 cr | −139 cd/day |
| Escort | 142 | 39 / 225 MW | 445 cr | −142 cd/day |
| Prometheus | 617 | 133 / 800 MW | 1,386 cr | −617 cd/day |
| Acclamator | 726 | 153 / 2,400 MW | 1,825 cr | −326 cd/day |
| Sovereign | 800 | 187 / 4,000 MW | 1,969 cr | closed loop |
| Venator | 884 | 223 / 3,200 MW | 1,999 cr | closed loop |
| Star Destroyer | 3,420 | 1,068 / 2,400 MW | 5,768 cr | −2,620 cd/day |
| Death Star | 414,266 | 187,563 / 200,000 MW | ~3.0M cr | closed loop |

---

## Table 2 — ground units (5), per unit and per squad

| Unit | Squad | Per unit: mass · BP · credits · upkeep | **Per squad** (×UnitsPerBuild) |
|---|:--:|---|---|
| **Termagant** (chaff) | ×20 | 63 t · 189 · 7k · 6 cr/mo | **1,260 t · 3,780 BP · 140k cr · 126 cr/mo** |
| **Clone Trooper** | ×9 | 265 t · 1,005 · 22k · 27 cr/mo | **2,385 t · 9,045 BP · 198k cr · 239 cr/mo** |
| **Genestealer** (elite) | ×8 | 593 t · 1,689 · 39k · 59 cr/mo | **4,744 t · 13,512 BP · 312k cr · 474 cr/mo** |
| **Carnifex** (monster) | ×1 | 5,026 t · 14,978 · 93k · 503 cr/mo | — (single) |
| **AT-TE** (gun-walker) | ×1 | 4,750 t · 12,890 · 223k · 475 cr/mo | — (single) |

Ground units carry ~0 crew and ~0 power draw (the exceptions: a locomotion drive adds 2 crew, a radar 3; the AT-TE's
6 space-lasers draw 3 MW off its own reactor). **Upkeep is engine-LIVE for ground units** (`Mass × 0.1 /month`, billed
by `GroundUpkeep`) — the one run-cost that already bites.

---

## The bottom line

- **The Death Star dwarfs everything** — 34.35 **billion** credits, ~180M build-points, alone. **~1,400× the eight
  warships combined.**
- **The eight warships together:** ~25.6M credits · ~205k build-points · ~161k t materials.
- **All five ground units, squads included:** ~**966k credits** · ~54k build-points · ~18,400 t — i.e. **about one
  Escort's price for a whole sampler army**, and the mass of roughly **one Acclamator**. A single infantry squad (198k)
  is a quarter of an Escort; a Tyranid brood (140k) less than that. Cheap to raise armies; the expensive decisions stay
  the capitals and (astronomically) the megastructure.
- *(On the real base-mod economy the ground units are cheaper still — a trooper is ~110 raw credits — so in the shipped
  game an army is a rounding error next to a warship. The mock scale above just makes them legible side-by-side.)*

*Every unit is now a clickable preset in the Entity Assembler — pick it, read its build + run bill, switch hosts and
watch the numbers. Ground presets: Termagant · Clone Trooper · Genestealer · Carnifex · AT-TE. The run-cost column is
"what it costs once the run-cost vector is wired" (`ENGINE-WIRING-BACKLOG` TIER 2.5).*
