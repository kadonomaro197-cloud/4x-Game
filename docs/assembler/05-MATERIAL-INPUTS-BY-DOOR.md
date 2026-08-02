# 05 — INPUTS BY DOOR: materials AND people (does the game provide them?)

> **The objective, stated plainly:** for each of the 12 doors, list the inputs its components actually need to
> be built and run, and **verify the game provides them.** A component has **two** kinds of input, and both are
> checked here: **materials** (minerals + refined goods — PART 1) and **people** (crew to operate it, drawn from
> the colony workforce — PART 2). A per-door bill with a ✓/✗ on each line.
>
> *(Originally this file covered only materials; PART 2 was added when the obvious question came up — a
> component isn't built from metal alone, it's crewed by people, and people are the scarcer input.)*

## PART 1 — THE MATERIAL INPUTS
>
> **Method:** scanned all 102 buildable component templates across `weapons/ordnance/installations/storage/
> energy/engines/electronics/docking.json`, grouped each by the door that designs it (via its attribute type),
> took the union of every component's `ResourceCost` keys, and checked each key against the 15 defined minerals
> + 24 defined materials. Then confirmed the **refining chain closes** — every material used as an input itself
> refines from defined minerals. Verified 2026-08-02.

---

## THE HEADLINE

**11 of the 12 doors: every material input is PROVIDED.** ✅
**1 door (the missile/ordnance chain): 3 undefined inputs.** 🔴 — `gallicite`, `duranium`, `mercassium`.

That's the whole finding. The base-mod economy is closed and consistent — almost every component is built from
a small, shared set of minerals and materials that are all mineable/refinable — **except three phantom minerals
referenced only by ordnance components**, which are defined nowhere and fault the build.

Legend: **(M)** = a mined mineral · **(R)** = a refined material (its recipe is shown to close the loop) ·
**🔴** = undefined, the game cannot provide it.

---

## THE LIST, BY DOOR

### 🔴 Weapons — 16 components — **1 UNDEFINED**
| Input | Provided? |
|---|---|
| aluminium | (M) |
| copper | (M) |
| graphite | (M) |
| titanium | (M) |
| tungsten | (M) |
| stainless-steel | (R) iron + chromium + hydrocarbons |
| plastic | (R) hydrocarbons |
| electronics | (R) copper + plastic + aluminium + silicon |
| ree-magnetics | (R) rare-earth-elements + iron |
| **gallicite** | **🔴 UNDEFINED** — on *Missile Electronics Suite* (`ordnance.json`) |

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

### 🔴 Logistical — 17 components — **2 UNDEFINED**
| Input | Provided? |
|---|---|
| aluminium, copper, iron, titanium, tungsten | (M) |
| stainless-steel, plastic, electronics | (R) |
| **duranium** | **🔴 UNDEFINED** — on *Ordnance Storage* (`ordnance.json`) |
| **mercassium** | **🔴 UNDEFINED** — on *Ordnance Storage* (`ordnance.json`) |
*(All the cargo holds, fuel tanks, magazines, troop bays, docking — fully provided EXCEPT the ordnance store.)*

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

## THE THREE THAT AREN'T PROVIDED (the only failures)

All three are **Aurora-4X mineral names** referenced by ordnance components but **never defined** in Pulsar's
mineral or material lists — so a build of these designs faults in `ConsumeResources` ("Cant build from non
ICargoable Items"):

| Undefined input | On this component | File |
|---|---|---|
| `gallicite` | Missile Electronics Suite | `ordnance.json` |
| `duranium` | Ordnance Storage | `ordnance.json` |
| `mercassium` | Ordnance Storage | `ordnance.json` |

**The fix (from `03-RESOURCE-LEDGER.md`, NEW-1):** replace these three references with the buildable munitions
chain — a missile costs `explosive-compound` (hydrocarbons + fissionables + copper) + `electronics` +
`stainless-steel`. That closes the last open chain in the game. `BaseModIntegrityTests` is the gauge.

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

**Bottom line (both inputs):** the game provides every **material** input for 11 of 12 doors (fix the three
ordnance references for the 12th), and it provides the **people** to build and crew everything through the
workforce pool — with people being the genuinely *scarce* input, and three people-accounting wires still open
(employment, recruitment-scarcity, colonist delivery).

*Companion to `03-RESOURCE-LEDGER.md` (the full material supply/demand/gaps) and `04-ACQUISITION-MAP.md` (how
you get each one). This file is the direct per-door provision check for both inputs — materials and people.*
