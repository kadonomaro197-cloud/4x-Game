# 05 — MATERIAL INPUTS BY DOOR (does the game provide them?)

> **The objective, stated plainly:** for each of the 12 doors, list the **material inputs** its components
> actually need to be built, and **verify the game provides them** — i.e. every input is either a defined
> mineral you can mine or a defined material you can refine from real minerals. This is the direct answer, not
> a gap essay: a per-door bill of materials with a ✓/✗ on each line.
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

**Bottom line:** the game provides every material input for 11 of 12 doors today; fix the three ordnance
references and it provides **all** of them.

*Companion to `03-RESOURCE-LEDGER.md` (the full supply/demand/gaps) and `04-ACQUISITION-MAP.md` (how you get
each one). This file is the direct per-door provision check.*
