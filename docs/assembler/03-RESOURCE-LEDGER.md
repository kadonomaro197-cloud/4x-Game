# 03 — THE RESOURCE LEDGER (the cradle rung: what the designers are made of)

> **What this is, in plain English.** Every component the twelve doors design has to be *built out of
> something* — a mineral dug from a planet, refined into a material, then spent to build the part. This file is
> the **bill of materials for the whole designer surface**: the resources that already exist, the resources the
> designers actually reach for, where the two match (a lot), where they don't (the gaps), and — for every gap —
> a **new resource plus the path to get it** (mine it → refine it → spend it). It's the "cradle" end of
> cradle-to-grave: name the mineral in the ground before you name the gun on the battlefield.
>
> Built from three things, cross-checked against each other: the **defined resources** in
> `GameData/basemod/TemplateFiles/{minerals,materials}.json` (the supply); a **12-agent extraction of every
> resource each door names** (the demand), verified against source; and the **design targets** in
> `docs/economy/RESOURCES-AND-MATERIALS-DESIGN.md` (what each mineral is *meant* to gate). Design-only — nothing
> is written into the game's JSON here; the recipes below are the spec for that follow-on step.

---

## PART 1 — THE SUPPLY: what resources are DEFINED today

### 1.1 The 15 minerals (dug from planets — `minerals.json`)
The base of everything. All 15 are mined by the same Mine/RoboMiner families; they differ in where they're
found and what they gate. Roles are the design targets from `RESOURCES-AND-MATERIALS-DESIGN.md`.

| Mineral (id) | Where found | Its job (design target) |
|---|---|---|
| `iron` | terrestrial, abundant | primary structure — 35–75% of most builds |
| `aluminium` | terrestrial, common | lightweight structure — ships, robotics |
| `silicon` | terrestrial, very abundant | electronics substrate, space-crete |
| `copper` | asteroids, rare | electrical conductor — all electronics |
| `titanium` | terrestrial trace / asteroids | high-strength ship armour, airframes |
| `tungsten` | terrestrial, ultra-rare | kinetic penetrators, laser-chamber, gun barrels |
| `chromium` | terrestrial trace | stainless-steel feedstock |
| `nickel` | asteroids, moderate | superalloys, turbine blades, nickel-steel |
| `graphite` | terrestrial, uncommon | reactor shielding, thermal radiators |
| `hydrocarbons` | gas giants, comets | ALL fuels, plastic, explosives feedstock |
| `fissionables` | terrestrial trace, rare | nuclear propellant + reactor fuel |
| `lithium` | ice giants, rare | batteries, capacitors, charge cycles |
| `water` | comets, ice moons | life support + space-crete |
| `regolith` | everywhere | the "free" local building material |
| `rare-earth-elements` | terrestrial trace | magnets, optics, sensor arrays |

### 1.2 The 24 refined materials (`materials.json`)
Made at a Refinery from minerals via a `ResourceCosts` recipe (e.g. `stainless-steel` = 88 iron + 11 chromium
+ 1 hydrocarbon). The set: `stainless-steel` (+`-d`/`-a` grades), `nickel-steel`, `plastic`, `space-crete`,
`electronics` (+`-d`/`-a`), `electricity`, `rp-1`, `methalox`, `hydrolox`, `ntp`, `fissile-fuels`,
`lithium-battery`, `ree-magnetics`, `ablative-composite`, `corrosion-resistant-alloy`, `em-shielding-mesh`,
`reinforced-trusswork`, `tungsten-plating`, `space-crete`, `food`, `antimatter`. Plus a separate 6-material
particle/damage set (`particleMaterials.json`: water, air, aluminum, stainless-steel, concrete, delrin) used by
the beam-absorption physics, and the 5-material `damageResistance.json` set (null, plastic, aluminium,
titanium, stainless-steel).

---

## PART 2 — THE DEMAND: what each designer reaches for

Extracted from all 12 door HTMLs (a 12-agent scan, each reading one door in full), then verified against
source. Each row: what the door NAMES → the defined resource it maps to (or **GAP**).

| Door | Resources it names | Maps to |
|---|---|---|
| **Weapons** | "power" (beam), "ammo" (projectile/guided), "nothing" (contact), heat (waste) | electricity · **GAP: ammo** |
| **Defense** | mass; Composite / Ablative / **Reactive** / **Null-Ward** plating; shield/ward device | ablative-composite · **GAP: reactive + exotic plating**; devices name no material |
| **Chassis** | **biomass**, food, fuel, continuous power, "materials"; substrate substances (metal / crystal / **nanite cloud** / **energy field**) | food · electricity · **GAP: biomass, generic fuel, exotic substrates** |
| **Civic** | food; mass; build points / credits / research; crew; time | food (rest are cost currencies, not materials) |
| **Command** | mass; BP / credits / RP | (cost currencies only — no material) |
| **Enhancers** | electronics.json; workforce; **myomer**, **nanite stock**, patch plate, reflex wetware, energy screen, neural lattice | electronics · **GAP: myomer / nanite / bio-augment substances** |
| **Industrial** | minerals (generic), ResourceCost, BP/credit/RP | the mineral system itself (this door BUILDS from all of them) |
| **Logistical** | 4 mm **steel shell** (8000 kg/m³), stainless-steel, fuel, water, hydrocarbons, regolith, food, fissionables, electricity, lithium-battery, antimatter, **biomass**, ammunition, volatiles | stainless-steel · water · hydrocarbons · regolith · food · fissionables · electricity · lithium-battery · antimatter · **GAP: biomass, ammunition, generic fuel** |
| **Power** | fissile-fuels, fissionables, hydrocarbons, graphite, tungsten, copper, aluminium, nickel, stainless-steel, isotopes, electricity, starlight | fissile-fuels · fissionables · hydrocarbons · graphite · tungsten · copper · aluminium · nickel · stainless-steel · electricity (starlight = ambient, not mined) |
| **Propulsion** | RP-1, Methalox, Hydrolox, NTP, fissionables, kerolox, electricity, reaction mass, hull/drive mass | rp-1 · methalox · hydrolox · ntp · fissionables · electricity · **GAP: generic reaction mass** |
| **Sensors** | power/electricity, antenna/dish, sensor/cloak/jammer mass, reactor/thruster/warp (emitters) | electricity · **GAP: the door names NO build material** (should be ree-magnetics + electronics + antenna metal) |
| **Aura** (proposed) | metal, electronics, mass, power | electronics · electricity · **GAP: generic metal** |

---

## PART 3 — THE OVERLAP (the large one, as predicted)

**The designers reach for the resources that already exist — heavily.** Cross-checking the demand (Part 2)
against the supply (Part 1) AND against the doors' own cost OUTPUTS (the `ResourceCost` readouts recorded in the
`01-IO-<door>.md` census), the overlap is exactly what you'd expect from a base layer that was built before the
designers: **19 of the ~24 distinct concrete resources the designers name are already defined.**

| Defined resource | Which designers draw on it |
|---|---|
| `electricity` | Weapons, Chassis, Sensors, Aura, Power, Propulsion, Logistical (7 doors) |
| `stainless-steel` | Logistical (fuel-tank shell), Power (structure) |
| `fissionables` | Power, Propulsion, Logistical |
| `hydrocarbons` | Power, Logistical (fuels + explosives feedstock) |
| `food` | Chassis (cyber upkeep), Civic, Logistical |
| `tungsten` · `graphite` · `copper` · `aluminium` · `nickel` | Power (reactor/turbine build) |
| `fissile-fuels` | Power (reactor fuel) |
| `rp-1` · `methalox` · `hydrolox` · `ntp` | Propulsion (the fuel chip) |
| `water` · `regolith` | Logistical (fluid/local cargo) |
| `lithium-battery` · `antimatter` | Logistical (cargo classes) |
| `electronics` | Aura, Enhancers, (proposed) Sensors |
| `ablative-composite` | Defense (Composite / Ablative plating) |

**Cross-check verdict:** the demand and supply agree on the whole *core industrial economy* — metals, fuels,
power, structure. The overlap is real and large. The gaps are concentrated in exactly three places: **munitions,
biology, and the exotic tail** — plus a set of materials that exist but nobody spends yet.

---

## PART 4 — THE GAPS

### 4a · Demand WITHOUT supply — a designer needs it, nothing defines it
| Missing resource | Who needs it | Why it's a real gap |
|---|---|---|
| **ammunition / ordnance charge** | Weapons (projectile/guided "eat ammo"), Logistical (magazine) | No material named `ammo`; worse, `ordnance.json:311` charges **`gallicite`** — a mineral **defined nowhere** → the missile's build cost points at a resource you can never mine (a broken chain) |
| **biomass** | Chassis (organic/cybernetic substrate "EATS biomass"), Logistical (refrigerated hold), life-support | Named in substrate upkeep + cargo descriptions; **undefined** as mineral or material. The organic-substrate door cannot be built cradle-to-grave without it |
| **reactive armour compound** | Defense (Reactive Plating preset) | A distinct armour material; only `ablative-composite` exists — no material backs Reactive Plating |
| **exotic-resist plating** | Defense (Null-Ward Plating, vs Exotic) | The door invents "Null-Ward"; no material and no mineral chain for exotic resistance |
| **bio-augment / myomer / nanite stock** | Enhancers (power armour, reflex, self-repair), Chassis (nanite substrate) | Flavour substances with no defined material; the augment can't be sourced |
| **exotic substrates** (crystalline lattice, coherent-energy field) | Chassis (crystalline / energy-bound substrates) | Marked engine-pending in the door itself, but they still name a substance with no chain |
| **generic reaction mass / fuel** | Propulsion, Chassis, Logistical | A mapping gap more than a true one — "fuel" = one of the defined propellants/fissile-fuels; needs a stated default, not a new material |

### 4b · Supply WITHOUT demand — defined, but nobody spends it (the mirror gap)
Just as broken cradle-to-grave: a material you can refine that no component requires.
| Defined material | Should be spent by (per design targets) | Status |
|---|---|---|
| `nickel-steel` | kinetic armour, gun barrels, pressure vessels | recipe exists, **0 build-cost consumers** |
| `lithium-battery` | capacitor banks, fire control, railgun charge, directed-energy weapons | recipe exists, **0 build-cost consumers** |
| `ree-magnetics` | **sensor arrays**, laser optics, railgun coils | 1 weapon reference; **the sensor door names none** |
| `tungsten-plating` | reactor shielding, radiation plating | 0 consumers |
| `em-shielding-mesh` | sensors, electronic warfare, cloak | 0 consumers |
| `reinforced-trusswork` | station/large structure | 0 consumers |
| `corrosion-resistant-alloy` | fuel systems, tankage | 0 consumers |

---

## PART 5 — THE MISSING RESOURCES, DEVELOPED (with acquisition paths)

For each gap: **what it is · the acquisition path (mine → refine → spend) · which door consumes it · status.**
Recipes follow the `materials.json` style (input minerals → output units, at a Refinery) and are grounded in the
15 existing minerals so nothing is parachuted in. Balance numbers are FLAGGED, not chosen.

### NEW-1 · The munitions chain — fixes `gallicite` AND the Weapons "ammo" gap
The single highest-value fix: it makes every kinetic/explosive weapon and every missile buildable through a real
chain, and it retires the one undefined reference in the whole game.

- **`explosive-compound`** — *the energetic filler in a warhead or shell.* **Path:** `hydrocarbons` (mined, gas
  giants/comets) + `fissionables` (trace) → **refine** `80 hydrocarbons + 5 fissionables + 15 copper → 100`
  [~40 pts] → **spent by** Weapons (explosive-nature shots), Defense (reactive plating, NEW-3), ordnance
  warheads. *The decision it enables: explosive weapons draw on the outer-system gas economy, not just metal.*
- **`ammunition`** — *a finished round: slug + charge + casing.* **Path:** `tungsten` (the penetrator — the
  design target already names tungsten for kinetic rounds) + `explosive-compound` + `stainless-steel` (casing) →
  **build** `tungsten + explosive-compound + stainless-steel` → **spent by** Weapons (projectile/guided "runs on
  ammo"), Logistical (magazine stock). *The Weapons door's "ammo" token finally points at something.*
- **THE `gallicite` FIX.** `ordnance.json:311` (`"gallicite": "[Mass] * 8"`) points at nothing. **Recommended:**
  replace that line with the ammunition/explosive chain above (a missile costs `explosive-compound` + a guidance
  `electronics` + `stainless-steel` body). **Alternative** if the name is wanted: define `gallicite` as a new
  rare mineral (Aurora's missile-engine material) with an abundance profile — but that adds a 16th mineral for
  one reference, so redirecting to the buildable chain is cleaner and cradle-to-grave. Status: **NEW (near-term);
  the gallicite reference is a live latent bug — `BaseModIntegrityTests` is the gauge.**

### NEW-2 · Biomass — the biology chain (grown, not mined)
- **`biomass`** — *raw living matter: the feedstock the organic economy runs on.* Unlike every other resource it
  is **grown, not mined** — which is exactly the "grown" industry type the Chassis door already proposes.
  **Path:** `water` + `hydrocarbons` (a carbon source) + starlight/`electricity`, at a **Hydroponics / Farm
  building** → `biomass`. Then `biomass` → **refine** → `food` (the edible grade — `food` already exists, so this
  slots *under* it). **Spent by:** Chassis (organic + cybernetic substrate upkeep — "the only one that EATS"),
  Logistical (refrigerated-hold contents), and the parked per-capita **life-support** loop
  (`RESOURCES-AND-MATERIALS-DESIGN.md` Priority 6). *The decision it enables: an organic army or a colony on a
  dead rock has to be FED — a supply line, not a one-time build.* Status: **NEW; the "grown" industry type is
  itself engine-pending, so biomass ships when it does.**

### NEW-3 · The armour materials — Reactive and Null-Ward
- **`reactive-plating`** — *explosive-reactive armour: a charge that fires outward to blunt a kinetic hit.*
  **Path:** `stainless-steel` + `explosive-compound` (NEW-1) → **build** → **spent by** Defense (Reactive
  Plating preset, strong vs Kinetic/Explosive). *Nice connection: the same explosive chain feeds both the shell
  and the armour that stops it.* Status: **NEW (near-term).**
- **`null-ward-plating`** — *exotic-dampening plate (vs the shield-bypassing Exotic nature).* **Path:**
  `ree-magnetics` (field material) + `tungsten-plating` (radiation layer — a currently-unconsumed material, so
  this also fixes a 4b gap) → **build** → **spent by** Defense (Null-Ward preset). Status: **NEW; deeper exotic
  fidelity is engine-pending (there is no exotic *mineral* — this is the first-cut mapping).**

### NEW-4 · The bio-augment substances (Enhancers)
- **`myomer`** — *artificial muscle: what a power-armour StrengthBonus is made of.* **Path:** `plastic` +
  `electronics` + `aluminium` → **build** → **spent by** Enhancers (power armour / carry augments). Grounded in
  existing materials; "myomer" is just the named grade. Status: **NEW (near-term, cheap — all inputs exist).**
- **`nanite-stock`** — *self-replicating repair machines.* **Path:** `ree-magnetics` + `electronics` +
  `rare-earth-elements` → **build** → **spent by** Enhancers (self-repair — H2), Chassis (nanite substrate).
  Status: **ENGINE-PENDING** (self-repair needs the parked degraded-condition model; the nanite substrate is
  engine-pending too — so the material waits on both).

### NEW-5 · The exotic substrate substances (Chassis — forward-looking)
The Chassis door marks these substrates engine-pending; their materials wait with them. Sketched so the chain is
designed, not invented later:
- **`crystalline-lattice`** — grown from `silicon` + `rare-earth-elements` under an exotic tech. Chassis
  crystalline substrate. **ENGINE-PENDING.**
- **`energy-substrate`** — bound `antimatter` / exotic field. Chassis energy-bound substrate. **ENGINE-PENDING.**

### NEW-6 · The wire-ins — materials that EXIST but need a DEMAND (fixes 4b)
No new material — just the missing spend-side wire, per the design targets. These are the cheapest of all (data
only, both ends already exist):
| Existing material | Wire it into (as a build cost) | Design-target basis |
|---|---|---|
| `ree-magnetics` | **Sensors** components (arrays, optics), railgun coils | "sensor arrays are magnetized detection systems" |
| `lithium-battery` | beam/railgun **fire-control capacitors**, directed-energy weapons | "capacitor banks, railgun charge cycles" |
| `nickel-steel` | kinetic **armour**, gun barrels, pressure vessels | "better kinetic armor than plain iron" |
| `electronics` | **Sensors**, fire control, Command seats, Aura projector | the electronics chain should reach all electronic parts |
| `tungsten-plating` | reactor shielding (Power), Null-Ward plating (NEW-3) | "radiation plating" |
| `em-shielding-mesh` | Sensors, cloak, electronic warfare | EM/EW parts |
| `reinforced-trusswork` | stations, large hulls (Chassis infrastructure) | large-structure material |
| `corrosion-resistant-alloy` | fuel tankage (Logistical), fuel systems | "all fuel systems" |

---

## The one-screen summary
- **The overlap is large and real:** 19 of ~24 concrete designer references already map to defined minerals/
  materials — the whole metal/fuel/power/structure economy is shared between what's defined and what the
  designers reach for.
- **The gaps cluster in three places:** **munitions** (ammo + the broken `gallicite`), **biology** (biomass), and
  the **exotic tail** (reactive/exotic armour, nanite, crystalline/energy substrates).
- **Two shapes of gap:** demand-without-supply (build the material — NEW-1..5) and supply-without-demand (wire
  the existing material to a consumer — NEW-6, the cheapest).
- **Build order by leverage:** NEW-1 (munitions — fixes a live bug + the biggest weapons gap) → NEW-6 (wire-ins —
  data only, both ends exist) → NEW-3/NEW-4 (armour + augments — inputs all exist) → NEW-2 (biomass — waits on
  the "grown" industry) → NEW-5 (exotic substrates — engine-pending).
- **Nothing is parachuted in:** every new material is refined from the 15 existing minerals, so each has a real
  acquisition path from a rock in the ground to the decision on the battlefield.

*Resource ledger complete. This is the cradle rung feeding both the 12 door-designers and the Entity Assembler
(`entityassembler.html`) — the materials a component is built from, before it becomes a total the resolver reads.*
