# 05 — EVERY INPUT BY DOOR (does the game provide them?)

> **The objective, stated plainly:** for each of the 12 doors, list **everything** its components consume — to
> BUILD them and to OWN and RUN them — and **verify the game provides it.** A component isn't just metal; it's
> metal + people + industry + money + research to build, then power + fuel + ammo + upkeep + food + budget to
> keep and operate. All of it is checked here, in four parts:
> **PART 1 materials · PART 2 people · PART 3 the rest of the build cost · PART 4 the run-time inputs.**
>
> *(This file grew as the question got sharper: materials → "shouldn't there be people too?" → "I want
> EVERYTHING." So it now covers the complete input surface.)*

## THE COMPLETE INPUT TAXONOMY (the summary — detail in the parts below)
| # | Input | When | Provided / charged? |
|---|---|---|---|
| 1 | **Materials** (minerals + refined) | build | ✅ **ALL 12 doors** — the 3 undefined ordnance minerals were **FIXED 2026-08-02** (redirected to electronics/steel/aluminium; see PART 1) |
| 2 | **People / crew** (workforce) | build + run | ✅ supplied (population → pool); the *scarce* input; 3 accounting gaps (PART 2) |
| 3 | **Build points** (industry capacity → time) | build | ✅ every component; needs the right factory |
| 4 | **Credits** (money) | build | ✅ every component |
| 5 | **Research** (RP + the tech unlock) | build | ✅ every component; unlock gate lightly used in base mod |
| 6 | **The right facility** (installation- / component-construction) | build | ✅ every component declares its `IndustryTypeID` |
| 7 | **Power** (a reactor feeding it) | run | ⚠ energy weapons + warp draw; **no generic component draw** (crack C12) |
| 8 | **Fuel** (propellant / fissile-fuels) | run | ✅ engines burn propellant; reactors burn fissile-fuels |
| 9 | **Ammo** (a magazine to feed a gun) | run | ✅ kinetic/missile weapons (WeaponSupply Energy/Ammo/Both + `GroundAmmo`); wire lightly used |
| 10 | **Upkeep** (standing maintenance) | run | ⚠ ground units + stations billed monthly; **ships/most installations free** |
| 11 | **Food / life-support** (crew must eat) | run | ⚠ machinery exists; `PerCapitaFoodDemand` defaults **0** → population eats free by default |
| 12 | **Mass / carry / volume budget** (host space) | assemble | ✅ every component spends the chassis budget (the Assembler gate) |
| 13 | **Infrastructure capacity** (colony support) | run | ✅ installations throttle on the infra-efficiency grid |

**Headline: the BUILD side is fully provided and charged (1–6). The RUN side is mostly built too (7–13), with
three "free" gaps — no generic power draw, no ship upkeep, no per-capita food.** These are the audit's "free to
own / free to run" problem (Problem 2), and some of it has since been closed (ground upkeep and ammo now exist).

## PART 1 — THE MATERIAL INPUTS
>
> **Method:** scanned all 102 buildable component templates across `weapons/ordnance/installations/storage/
> energy/engines/electronics/docking.json`, grouped each by the door that designs it (via its attribute type),
> took the union of every component's `ResourceCost` keys, and checked each key against the 15 defined minerals
> + 24 defined materials. Then confirmed the **refining chain closes** — every material used as an input itself
> refines from defined minerals. Verified 2026-08-02.

---

## THE HEADLINE

**ALL 12 doors: every material input is PROVIDED.** ✅ *(as of the 2026-08-02 ordnance fix)*

The base-mod economy is closed and consistent — every component is built from a small, shared set of minerals
and materials that are all mineable/refinable. The three phantom minerals that used to fault the ordnance build
(`gallicite`, `duranium`, `mercassium`) were **redirected to already-provided materials** — a verified scan now
finds **zero** undefined component-cost references anywhere in the game.

> **FIXED 2026-08-02:** `gallicite` → `electronics` + `aluminium` (the Missile Electronics Suite is an
> electronics part); `duranium`/`mercassium` → `stainless-steel` + `aluminium` (Ordnance Storage is a metal
> rack). Both now cost materials the game already provides. Gauge: `BaseModIntegrityTests`. The per-door lists
> below still show the *pre-fix* undefined entries with a ✅ FIXED note so the history is visible.

Legend: **(M)** = a mined mineral · **(R)** = a refined material (its recipe is shown to close the loop) ·
**🔴** = undefined, the game cannot provide it.

---

## THE LIST, BY DOOR

### ✅ Weapons — 16 components — *(was 1 undefined; FIXED 2026-08-02)*
| Input | Provided? |
|---|---|
| aluminium · copper · graphite · titanium · tungsten | (M) |
| stainless-steel | (R) iron + chromium + hydrocarbons |
| plastic | (R) hydrocarbons |
| electronics | (R) copper + plastic + aluminium + silicon |
| ree-magnetics | (R) rare-earth-elements + iron |
| ~~gallicite~~ → electronics + aluminium | ✅ **FIXED** — Missile Electronics Suite now costs provided materials |

### ✅ Defense — 10 components — all provided
aluminium (M) · copper (M) · stainless-steel (R). *(Composite/Ablative/Reactive plating, shields, hardening,
point-defense, radiators, sealed systems — all built from the base three.)*

### ✅ Chassis — 7 components — all provided
aluminium (M) · iron (M) · stainless-steel (R). *(Ship hull, station chassis, building foundation, the four
ground frames.)*

### ✅ Power — 5 components — all provided
aluminium (M) · copper (M) · graphite (M) · nickel (M) · silicon (M) · rare-earth-elements (M) · titanium (M) ·
tungsten (M) · stainless-steel (R) · plastic (R) · **fissile-fuels (R)** fissionables + hydrocarbons ·
**lithium-battery (R)** lithium + copper + electronics. *(The richest input set — reactor, RTG, turbine, solar,
battery. Note: this door is where `lithium-battery` and `fissile-fuels` actually get consumed.)*

### ✅ Propulsion — 7 components — all provided
aluminium (M) · copper (M) · graphite (M) · rare-earth-elements (M) · titanium (M) · tungsten (M) ·
stainless-steel (R) · plastic (R) · electronics (R). *(Warp, antimatter, rocket, inertialess, reactionless,
NTP drives + ground locomotion.)*

### ✅ Sensors — 11 components — all provided
aluminium (M) · copper (M) · titanium (M) · stainless-steel (R) · plastic (R) · electronics (R). *(Passive
sensor, fire control, jammer, cloak, surveyors, ground radar, hardening.)*

### ✅ Logistical — 17 components — *(was 2 undefined; FIXED 2026-08-02)*
| Input | Provided? |
|---|---|
| aluminium, copper, iron, titanium, tungsten | (M) |
| stainless-steel, plastic, electronics | (R) |
| ~~duranium~~ + ~~mercassium~~ → stainless-steel + aluminium | ✅ **FIXED** — Ordnance Storage now costs provided metals |
*(All the cargo holds, fuel tanks, magazines, troop bays, docking — now fully provided.)*

### ✅ Industrial — 11 components — all provided
aluminium (M) · copper (M) · iron (M) · graphite (M) · nickel (M) · silicon (M) · titanium (M) · tungsten (M) ·
stainless-steel (R) · plastic (R) · electronics (R) · **space-crete (R)** regolith + silicon + aluminium + iron
+ water. *(Mine, RoboMiner, refinery, factory, shipyard, lab, bunker, launch complex, infrastructure.)*

### ✅ Civic — 5 components — all provided
aluminium (M) · copper (M) · iron (M) · water (M) · stainless-steel (R) · plastic (R) · electronics (R). *(Food
production, space habitat, naval + research academies.)*

### ✅ Command — 4 components — all provided
aluminium (M) · copper (M) · iron (M) · stainless-steel (R) · plastic (R) · electronics (R). *(Admin complex,
ship bridge, command berth, intel directorate.)*

### ✅ Enhancers — 5 components — all provided
aluminium (M) · stainless-steel (R) · electronics (R). *(Power armour, reflex booster, training cadre, veteran
cadre, crew automation.)*

### ⚪ Aura — 0 shipped components (proposed door)
No components ship yet, so no build cost to verify. When built, its projector would consume the same
electronics + metals chain (all provided).

---

## THE THREE THAT WEREN'T PROVIDED — ✅ FIXED 2026-08-02

All three were **Aurora-4X mineral names** referenced by ordnance components but **never defined** in Pulsar's
mineral or material lists — so a build of those designs faulted in `ConsumeResources` ("Cant build from non
ICargoable Items"). **Now redirected to already-provided materials:**

| Was (undefined) | On this component | Now costs |
|---|---|---|
| `gallicite` | Missile Electronics Suite | `electronics` [Mass]×0.6 + `aluminium` [Mass]×0.4 |
| `duranium` + `mercassium` | Ordnance Storage | `stainless-steel` 60 + `aluminium` 60 |

Both now cost materials the game already provides, matching what analogous components cost (an electronics suite
costs electronics; a cargo rack costs steel + aluminium). A verified scan finds **zero** undefined
component-cost references remaining. `BaseModIntegrityTests` is the gauge that confirms it on CI.
*(A richer optional version — a dedicated `explosive-compound` munitions chain — is designed in
`03-RESOURCE-LEDGER.md` NEW-1 if the developer wants warheads to pull on the gas-giant economy; the redirect
above is the minimal safe fix.)*

---

## TWO THINGS THE LIST MAKES OBVIOUS

1. **The economy runs on a small shared spine.** Nearly every component in the game is built from **aluminium +
   stainless-steel**, plus **copper / plastic / iron** — with a handful of door-specific specials on top
   (Weapons/Propulsion add titanium + tungsten + graphite; Power adds fissile-fuels + lithium-battery + the
   rare-earth/silicon set; Industrial adds space-crete). So "will the game provide it" is really a question
   about a dozen resources, and it provides all of them.
2. **The refining chain is closed.** Every *material* used as an input (stainless-steel, electronics, plastic,
   space-crete, ree-magnetics, lithium-battery, fissile-fuels) itself refines from **defined minerals only** —
   verified recipe by recipe. There is no hidden dead-end: mine the 15 minerals and you can refine everything
   any door needs.

---

## PART 2 — THE PEOPLE INPUT (crew), by door

A component isn't built from metal alone — it's **crewed by people**, and people are the input the game is
*scarcest* in. Every crewed component draws from the colony's **workforce pool**, which is filled by population
(the Civic door) and gated at build time by `ManpowerTools.ResolveBuild`. Unlike ore, you can't "mine more
people" — the pool is finite, slow to grow, and hard-drawn.

### How much crew each door needs (components that require crew / total)
| Door | Needs crew | Notably crew-FREE |
|---|---|---|
| Sensors | **11 / 11** | — (every sensor is manned) |
| Logistical | **16 / 17** | Stainless-Steel Fuel Tank (a passive tank) |
| Industrial | **10 / 11** | RoboMiner (unmanned by design) |
| Civic | **5 / 5** | — |
| Command | **4 / 4** | — |
| Weapons | 9 / 16 | the ground weapons; big ship guns draw `[Mass]` crew |
| Propulsion | 5 / 7 | Conventional Rocket, NTP (fire-and-forget) |
| Defense | 4 / 10 | armour plates, shields, wards (passive — **no crew**) |
| Chassis | 3 / 7 | the ground frames (crew comes from the unit, not the frame) |
| Power | 2 / 5 | RTG, Solar, Battery (passive — **no crew**); a Reactor needs `Max(2,[Mass]/500)` |
| Enhancers | 2 / 5 | power armour, reflex, training cadre (passive augments) |

**And ground units draw people directly:** an **Infantry** unit is `CrewReq = 100`, **Armor** 60, **Artillery**
40 — those are the *soldiers*, pulled from the workforce pool the same as a factory's workers. So a battalion is
paid for in people as much as in steel.

### Does the game PROVIDE the people? — mostly yes, with three gaps
**The supply chain exists and is one of the four backbones:**
- **Population** — grown by the Civic door (residency / housing → `PopulationProcessor`). The base pool.
- **The workforce** — `ManpowerTools.ResolveBuild` draws crew from population for every build; a government's
  `CrewPolicy` decides whether a shortage **blocks** the build or **conscripts** to cover it.
- **Specialists** — officers from the Naval / Ground / Government academies, scientists from the Research
  Academy (the Civic + Command doors) fill the seats that generic crew can't.

So the people-supply is real and wired for the core loop. But three people-side gaps mirror the three undefined
ordnance minerals — the places where the accounting doesn't close:

| People gap | What it is |
|---|---|
| **Employment has no producer** (crack C1) | The colony reads a *jobs-vs-workforce* ratio for morale (`ColonyMoraleDB` reads `employmentRatio`), but **no template produces `EmploymentAtbDB.Jobs`** — so that morale term is permanently dead (contributes 0). The consumer exists; the producer was never built. |
| **No recruitment / scarcity pipeline** | Units draw generic crew (Infantry = 100), but there's **no distinct recruit → train → veteran scarcity** — a lost battalion is just re-queued from the same pool, so "irreplaceable veterans" can't exist yet. (The marine-build gap.) |
| **Colonists can't be delivered** | Settling a new world needs people *shipped there*, but the colonist cargo has **no unload path** — nothing adds them to a new colony's population (engine-pending, from the Logistical door). |

**Net for people:** the game provides the workforce for building and manning everything today (population → pool
→ crew, gated by government policy). What's missing is the *fine-grained* people accounting — the employment
morale wire, a real recruitment-scarcity pipeline, and colonist delivery.

---

## PART 3 — THE REST OF THE BUILD COST (points · money · research · facility)

Every component template carries the *same* four build-cost channels alongside its materials and crew — verified
present on all 102 templates (`Formulas`: `BuildPointCost`, `CreditCost`, `ResearchCost`, plus `Mass`/`Volume`/
`HTK`/`CrewReq`). **All fully provided and charged, on every door.**

| Input | What it is | Provided? |
|---|---|---|
| **Build points** | Industry capacity — a Factory/Shipyard/Refinery contributes typed points/day; the build consumes them, which is what makes a build take *time* | ✅ every component; throttled by infrastructure efficiency |
| **Credits** | Money from the faction ledger (fed by colony tax) | ✅ every component (`CreditCost`) |
| **Research** | Research points to unlock + a per-build `ResearchCost` | ✅ every component; **the tech UNLOCK gate** (a component must be on the faction's buildable list) exists but is *lightly used* in the base mod — most parts start unlocked |
| **The right facility** | You need the matching factory: **installation-construction** (60 components — buildings/ground) or **component-construction** (42 — ship parts); refining and ship-assembly are their own lines | ✅ every component declares its `IndustryTypeID` |

So the whole **build side** is closed: materials + crew + build-points + credits + research + the right factory,
all charged, all provided (bar the 3 ordnance minerals).

## PART 4 — THE RUN-TIME INPUTS (what it eats once it exists)

A component keeps consuming after it's built. This is the audit's **"free to own / free to run"** territory
(Problem 2) — and it's more built than that audit implied, but three gaps remain. Verified from source:

| Input | Who consumes it | State |
|---|---|---|
| **Power** | Energy weapons + warp drives draw from a reactor (`WeaponSupply`: a weapon takes Energy / Ammo / Both) | ⚠ **real for weapons + warp; no GENERIC component draw** — active sensors and everything else draw nothing (crack C12). Reactors/RTG/solar SUPPLY it; most consumers don't declare a demand |
| **Fuel** | Engines burn propellant (`NewtonThrust` fuel draw); reactors burn fissile-fuels (`Lifetime → LocalFuel`, gated by `EnableFuelExhaustion`) | ✅ real for the things that move and generate |
| **Ammo** | Kinetic / missile weapons feed from a magazine (`WeaponSupply` Ammo mode + `GroundAmmo`) | ✅ mechanism real; the per-shot **consume wire is lightly used** (the marine-build gap — `GroundAmmo.Consume` mostly exercised by tests) |
| **Upkeep** | **Ground units** (`GroundUpkeep` bills `Σ UnitUpkeepCredits` monthly to the faction ledger) + **stations** (`StationUpkeepProcessor`); colonies pay via tax | ⚠ **real for ground units + stations; ships and most installations have NO standing upkeep** — the "free to own" gap, half-closed |
| **Food / life-support** | Population + crew (`SustenanceProcessor`: `foodDemand = pop × PerCapitaFoodDemand`) | ⚠ machinery exists but **`PerCapitaFoodDemand` defaults to 0** → population is fed for **free** by default; organic units "eat biomass" (an *undefined* material — see `03`) |
| **Mass / carry / volume budget** | Every mounted component spends the host's chassis budget | ✅ the Assembler gate (mass/carry/volume vs the frame budget) |
| **Infrastructure capacity** | Installations demand colony infrastructure support | ✅ the efficiency throttle — over-demand the grid and mining/refining/building all slow together |

So the **run side** is genuinely built for the things that fight and move (power for guns, fuel for engines,
ammo for kinetics, upkeep for ground/stations) — and has three honest "it's free" gaps: **no generic power
draw, no ship upkeep, and population that eats nothing by default.**

---

**Bottom line (EVERYTHING):**
- **Build inputs (1–6): fully provided and charged** on every door — materials, people, build-points, credits,
  research, the right factory — with the single exception of the 3 undefined ordnance minerals.
- **Run inputs (7–13): mostly built** — power (weapons/warp), fuel, ammo, ground+station upkeep, the mass budget,
  and infrastructure all bite today. **Three stay "free":** no generic component power draw, no ship upkeep, and
  no per-capita food.
- **Progress (2026-08-02):** the 3 ordnance-mineral gaps are now **FIXED** (a data redirect — every component
  cost resolves). The remaining open items are **6, all engine-side:** the 3 people-accounting wires (employment
  producer, recruitment scarcity, colonist delivery) + the 3 run-time "free" gaps (generic power draw, ship
  upkeep, per-capita food) — each an engine change requiring the CI build-loop one slice at a time, and three of
  them (food amount, recruitment model, ship-upkeep policy) needing a developer balance ruling.

*Companion to `03-RESOURCE-LEDGER.md` (material supply/demand/gaps) and `04-ACQUISITION-MAP.md` (how you get each
one). This file is the direct per-door provision check for the COMPLETE input surface — everything a component
consumes to be built, owned, and run.*
