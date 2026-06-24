# Resources and Materials — Full System Survey and Design Reference

Five parallel surveys of the entire codebase were run and merged here. This covers how minerals and materials flow through every part of the game: mining, refining, construction, commerce, research, military, and NPC behavior. Read this before making any design decision about the economy.

---

## The Central Finding

The game already has a working three-tier production pipeline. The pipe is built. The problem is that **nothing comes out the other end** — processed materials are producible but nowhere downstream asks for them, trade happens but no wealth is earned, and NPCs exist in the game world but have zero ability to make decisions. The infrastructure is 90% complete; the wiring between the stages is what's missing.

---

## The Three-Tier Production Chain (What Exists and Works)

Think of this like a ship's engineering plant: boilers feed the turbines feed the generators. Each stage feeds the next.

```
STAGE 1 — RAW MINERALS (dug from planets)
  ↓  [Mine / RoboMiner installations — runs every game-day]
  
STAGE 2 — REFINED MATERIALS (processed minerals)
  ↓  [Refinery installations — runs every game-day]
  
STAGE 3 — FINISHED GOODS (ships, weapons, installations)
     [Factory / Shipyard — runs every game-day]
```

All three stages run through the same daily processor (`IndustryProcessor.cs`). Infrastructure efficiency — determined by how many Infrastructure installations you have relative to your colony's size — acts as a throttle on all three stages at once. If your colony is 60% supported, mining runs at 60%, refining runs at 60%, and construction runs at 60%.

---

## Stage 1 — Raw Minerals (15 Types)

**File:** `GameData/basemod/TemplateFiles/minerals.json`

All 15 minerals sit in the same cargo type ("general-storage") and are mined by the same installation families (Mine, RoboMiner). They differ in where they're found, how they're used, and how fast deposits run out. The deposit depletion formula is cubic — the deeper you mine past the halfway point, the faster accessibility collapses.

### Current Mineral Roster and Roles

| Mineral | Where Found | Rarity | Current Role |
|---------|-------------|--------|--------------|
| iron | Terrestrial (abundant) | Very common | 35–75% of every installation's build cost |
| aluminium | Terrestrial | Common | 10–40% of every installation's build cost; RoboMiner primary material |
| silicon | Terrestrial (very abundant) | Common | Electronics recipe, space-crete recipe |
| regolith | Everywhere | Ubiquitous | Space-crete recipe (70% of recipe) |
| hydrocarbons | Gas giants, comets | Moderate | All 4 fuel types, plastic, stainless-steel trace |
| chromium | Terrestrial (trace) | Rare | Stainless-steel recipe feedstock |
| copper | Asteroids (trace) | Rare | Electronics recipe; electrical wiring in weapon builds |
| nickel | Asteroids (moderate) | Moderate | **No current use — no recipes** |
| titanium | Terrestrial (trace) | Rare | RoboMiner build cost; candidate for high-strength armor |
| tungsten | Terrestrial (ultra-rare) | Very rare | Mine build cost only (5%) |
| fissionables | Terrestrial (trace) | Very rare | NTP fuel recipe, fissile-fuels recipe |
| lithium | Ice giants (trace) | Very rare | **No current use — no recipes** |
| water | Comets, ice moons | Moderate | Space-crete recipe only |
| graphite | Terrestrial | Uncommon | Mine/RoboMiner build cost only |
| rare-earth-elements | Terrestrial (trace) | Rare | **No current use — no recipes** |

**Three minerals do nothing:** Nickel, lithium, and rare-earth-elements are minable but have zero downstream use. Mining them is pointless.

---

## Stage 2 — Refined Materials (9 Types)

**File:** `GameData/basemod/TemplateFiles/materials.json`

Refineries run at a colony and consume raw minerals to produce these outputs. All use `IndustryTypeID: "refining"`.

| Material | Input Minerals → Output | Points | Notes |
|----------|------------------------|--------|-------|
| stainless-steel | 88 iron + 11 chromium + 1 hydrocarbon → 100 | 20 | Referenced in some installation build costs |
| plastic | 1 hydrocarbon → 2 | 5 | Referenced in some installation build costs |
| space-crete | 70 regolith + 10 silicon + 10 aluminium + 5 iron + 5 water → 100 | 25 | Referenced in launch complex build costs |
| electronics | 2 copper + 2 plastic + 1 aluminium + 1 silicon → 5 | 100 | **Not referenced anywhere as a build cost** |
| rp-1 (rocket fuel) | 1 hydrocarbon → 2 | 10 | Ship chemical propellant |
| methalox | 1 hydrocarbon → 2 | 10 | Ship chemical propellant |
| hydrolox | 1 hydrocarbon → 2 | 10 | Ship chemical propellant |
| ntp (nuclear propellant) | 100 hydrocarbons + 1 fissionable → 100 | 100 | Nuclear thermal engine propellant |
| fissile-fuels | 100 fissionables + 10 hydrocarbons → 1 | 10 | Nuclear reactor fuel; high wealth cost 1,000 |

**The critical gap:** Electronics — the most complex recipe requiring copper, plastic, aluminium, and silicon — is not required to build any component in the game. Weapons, sensors, fire control: none of them need electronics as an input.

---

## Stage 3 — Finished Goods

**Files:** `GameData/basemod/TemplateFiles/weapons.json`, `installations.json`, component blueprints

Finished goods are produced by Factories (installations, weapons, components) and Shipyards (ships). Their `ResourceCost` formulae in JSON define what minerals and materials they consume.

**What finished goods currently require:**
- Primarily raw minerals (iron, aluminium, copper, tungsten, titanium, graphite)
- Some reference stainless-steel and space-crete
- **None require electronics, plastic (as a recipe output), or propellants as inputs**

The build cost for a laser weapon lists iron, stainless-steel, aluminium, titanium, tungsten, and copper. It does not require the electronics component at all — a laser weapon built from raw wire and metal, no chip fabrication needed.

---

## How Resources Flow Through Each Empire System

### Mining

Mines extract minerals from planetary deposits daily. The rate is:

```
Actual rate = (Base mining rate from installed mines)
            × (Deposit accessibility: starts high, degrades cubically as deposit empties)
            × (Infrastructure efficiency: provided capacity / required capacity)
```

Minerals land in the colony's cargo hold ("general-storage" container). If the hold is full or the cargo type isn't available, mining is skipped and a log event is written. Deposits are permanent — once empty, they don't refill.

**NPC note:** Every system is faction-agnostic. A mine installed on an NPC colony mines automatically. The NPC just needs to have built the mine.

### Refining

A Refinery installation adds a "refining" production line to the colony. The daily IndustryProcessor checks for queued refining jobs, deducts minerals from cargo, and adds the output material to cargo. Same efficiency throttle applies.

**NPC note:** An NPC refinery works if jobs are queued. Nothing queues jobs for NPCs today.

### Construction

Factories add "component-construction" lines; shipyards add "ship-assembly" lines. Jobs consume minerals and materials from cargo and produce finished goods. Ships are launched into the star system; components go to a staging stockpile on the colony; installations are attached directly to the colony's component list.

**NPC note:** Same situation — the build system is faction-neutral, but nothing tells NPCs to build.

### Commerce and Trade

The logistics system (`LogisticsProcessor.cs`) has a **fully functional automated freight market**. Here is how it works:

- A colony with a Logistics Office sets "desired levels" of cargo items and lists items for sale.
- Freight ships bid on routes: profit = how much the destination wants the item minus how much the source wants to keep it minus travel cost.
- The winning ship flies the route and transfers cargo automatically.
- The market is profit-maximizing and finds economically efficient routes without player micromanagement.

**The serious gap:** The faction money ledger (`Ledger.cs`) is **completely disconnected** from this market. Cargo moves between colonies for free. No money changes hands. There is no trade income for either the seller or the buyer's faction. There are also **no faction access controls** — an enemy faction's freight ship can freely dock at your logistics base and haul your cargo away.

### Research

University installations create research lab entities. The daily ResearchProcessor:
1. Collects research points from all labs (base rate × funding level × scientist bonuses)
2. If the faction can afford the daily cost (in money from the Ledger), advances the front tech in the queue
3. When progress reaches the tech's cost threshold, the tech completes: the faction's locked component templates move to unlocked; upgrades apply

**What research costs:** Faction money only. No minerals, no materials. This is simpler than Aurora, where some techs require specialized materials as a supply chain requirement.

**What's unlocked:** 10 tech categories — propulsion, sensors, energy weapons, missiles, construction, biology, power, bureaucracy, ground combat, and "designs" (custom component slots). The ground combat category exists but has **zero techs defined** and no code path that uses it.

**NPC note:** Research system is fully functional for NPCs. The ResearchProcessor is faction-agnostic. NPCs just need to have a research queue — nobody populates it for them today.

### Military and Combat

Minerals and materials connect to combat in two ways:

**1. Component construction:** Weapons, engines, and armor all cost minerals to build. A weapon that requires electronics as a build input creates a mineral supply chain dependency: copper → electronics → laser weapon. This dependency does not exist today because electronics isn't required by any weapon.

**2. Material damage physics:** The beam weapon system (Phase 2 work from prior session) connects material identity to combat outcomes. Each armor material has wavelength absorption coefficients. A FIR laser (10,000 nm) hits aluminium armor for only 18% energy absorption — most bounces off. The same laser hits plastic for 85% absorption. This means:
- The choice of armor material on your ship is a real tradeoff
- That tradeoff feeds back to what minerals your colony needs to mine
- An NPC choosing its ship's armor should be making this calculation

**Active damage path:** `BeamWeaponProcessor.OnHit()` → two-zone energy scaling → `DamageFragment` with wavelength → `DamageProcessor.OnTakingDamage()` → Beer-Lambert absorption lookup by material. The material lookup uses `damageResistance.json` which has 5 materials defined: null, plastic, aluminium, titanium, stainless-steel.

### Population and Infrastructure

Infrastructure installations provide support capacity. Required capacity is the sum of all other component masses and crew counts. If the colony is undersupported, efficiency drops below 1.0 and everything — mining, refining, construction — runs slower.

**The gap:** There is no ongoing resource consumption. A colony on a lifeless vacuum world needs zero food, zero water, zero atmospheric processing, zero power to keep its population alive after the initial build. This is a "build it and forget it" model. Aurora requires minerals per population per year for life support. That loop is not implemented.

---

## Full Gap Analysis

| Gap | Severity | What It Breaks |
|-----|----------|----------------|
| Processed materials unused in component costs | HIGH | The refinery chain is decorative; nothing downstream requires its output |
| Trade doesn't generate faction wealth | HIGH | The Ledger is dead weight; no economic incentive for commerce |
| No faction access control in logistics | HIGH | Enemy ships can access your freight bases |
| No NPC economic decision loop | CRITICAL | Everything works for NPCs if ordered; nothing orders them |
| No ongoing resource consumption | MEDIUM | Economy is static; mining pressure disappears once buildings are built |
| Nickel, lithium, rare-earths have no recipes | MEDIUM | 3 of 15 minerals serve no purpose; their deposits are ignored |
| Research costs no minerals | LOW | Simpler than Aurora; no material supply chain for tech advancement |
| Ground combat tech category is empty | LOW | Category defined, no techs, no code path |
| Ledger disconnected from trade income | HIGH | No wealth generation from commerce |
| Installations tab never renders | HIGH (UI) | Player cannot see installed colony components |
| No victory conditions or scoring | MEDIUM | NPCs have nothing to optimize toward |

---

## What Each Mineral Should Do (Design Targets)

The goal is **meaningful differentiation** — every mineral should be the unique gating factor for at least one important thing. Here is the recommended role for each:

### Tier 1 — Core Construction (iron, aluminium, silicon, copper)
These four are the backbone. No empire can function without them. They are and should remain abundant on terrestrial planets.

- **Iron** — primary structural material. Cheap and abundant. Required for the initial buildings on any colony. Not scarce enough to be a bottleneck after early game.
- **Aluminium** — lightweight structure. Required for ships (weight matters for delta-V) and robotics (RoboMiners). A colony strong in aluminium can build an efficient automated mining operation.
- **Silicon** — electronics substrate. Required for all sensors and fire control via the electronics recipe. A faction with silicon shortfall has blind ships.
- **Copper** — electrical conductor. Very rare. Required for all electrical systems via the electronics recipe. The first genuine rare-material bottleneck in the tech tree.

### Tier 2 — Advanced Military (tungsten, titanium, chromium, rare-earth-elements)
These gate high-end weapons and armor. Rarity means empires without them are limited to basic military tech.

- **Tungsten** — extremely dense, extremely hard. Required for kinetic penetrators (ground combat rounds, railgun slugs). Also required for high-temperature laser chamber components. Ultra-rare — a colony that finds a tungsten deposit is strategically valuable.
- **Titanium** — lightweight, high-strength. Required for top-tier ship armor and advanced airframes. Rare on terrestrial planets, more common on asteroids.
- **Chromium → Stainless-Steel** — corrosion-resistant alloy. Already in the recipe chain. Required for all fuel systems and shipyards.
- **Rare-Earth-Elements** — permanent magnets and optical-grade materials. Should be required for: sensor arrays (they are magnetized detection systems), laser focusing optics, and railgun electromagnetic coils. Currently unused — needs three new recipes.

### Tier 3 — Power and Propulsion (hydrocarbons, fissionables, lithium)
No ships move, no reactors run without these.

- **Hydrocarbons** — chemical fuel feedstock. Required for all four propellant types and plastic. Found on gas giants and comets — an empire that doesn't mine the outer system is chemically limited.
- **Fissionables** — nuclear fuel. Required for NTP propellant and fissile reactor fuel. Rare on any body. An empire with fissionables can reach very high specific impulse.
- **Lithium** — energy storage. Currently unused. Should be required for ship battery banks and supercapacitors (fire control capacitors for directed-energy weapons, railgun charge cycles). Needs a new recipe: "lithium-battery."

### Tier 4 — Infrastructure and Life Support (water, regolith, nickel, graphite)
These make colonies survivable and efficient.

- **Water** — life support and space-crete feedstock. On hostile worlds with no atmosphere, water for a colony should eventually be a per-capita requirement. Currently only in space-crete.
- **Regolith** — local building material. Already in space-crete. Ubiquitous everywhere — the point of regolith is that it is the "free" construction material available on any body.
- **Nickel** — high-temperature superalloys. Currently unused. Should be required for: engine turbine blades, high-temperature pressure vessel components, the "nickel-steel" armor type that performs better than plain iron under kinetic impact.
- **Graphite** — thermal management and radiation shielding. Currently only in mine build costs. Should be required for: reactor shielding around fission plants, high-heat radiator panels.

---

## Three New Recipes to Add

These close the "useless minerals" gap without adding new complexity:

**1. Lithium-Ion Battery** (`lithium-battery`)
- Input: 5 lithium + 2 copper + 1 plastic → 10 units
- `IndustryTypeID: "refining"`; 50 points
- Role: Required for ship capacitor banks, fire control power supply, railgun charge cycles

**2. REE Magnetics** (`ree-magnetics`)
- Input: 2 rare-earth-elements + 1 copper → 5 units
- `IndustryTypeID: "refining"`; 100 points
- Role: Required for sensor arrays, laser optics, electromagnetic systems

**3. Nickel-Steel** (`nickel-steel`)
- Input: 70 iron + 20 nickel + 10 chromium → 100 units
- `IndustryTypeID: "refining"`; 30 points
- Role: Better kinetic armor than plain iron; required for gun barrels and high-pressure vessels

---

## NPC AI — What Needs to Be Built

### Current State: Zero Economic AI

The research was thorough and the answer is definitive: **there is no NPC economic decision loop.** Every production system (mining, refining, construction, research) is faction-agnostic — they run identically for any faction that has the right components installed and orders queued. The NPC just needs something to generate those orders.

What *does* exist for NPCs:
- They are initialized with starting colonies, ships, and components (from the faction JSON files)
- Their mines run automatically (mining is based on installed components, not player action)
- Their research would run automatically if something queued the tech — but nothing does
- Their production lines would run if jobs were queued — but nothing queues jobs

**The contract:** Player presses a button, an order is created, a processor executes it. For NPCs, we need a processor that presses those buttons.

### What to Build: NPCDecisionProcessor

A new file: `GameEngine/Factions/NPCDecisionProcessor.cs`

This is an `IHotloopProcessor` — a watch-standing processor that auto-registers by just existing (no manual registration needed). It runs monthly for all NPC factions. It works through the order system, so every action it takes uses the exact same code path the player uses.

**Pseudocode for the monthly check-in:**

```
For each NPC faction:
  For each colony the faction owns:
  
    // 1. Infrastructure check
    if InfrastructureEfficiency < 0.9:
      queue: build Infrastructure installation
      
    // 2. Mining check  
    if no mines installed:
      queue: build Mine
    if cargo below desired minimum for key minerals:
      if colony has deposit of that mineral:
        already mining — wait
      else:
        flag for survey or inter-colony shipment
        
    // 3. Refinery check
    if stainless-steel stockpile < construction threshold:
      if no refinery: queue build Refinery
      else: queue refining job
      
    // 4. Factory check
    if no factory installed: queue build Factory
    
  // 5. Research check (faction-level, not colony-level)
  if tech queue is empty:
    select next tech using priority rules:
      - no shipyard designs unlocked → queue shipyard tech
      - no weapon designs unlocked → queue energy weapons tech
      - mining rate low → queue mining efficiency tech
      - otherwise → queue anything with a defined unlock
    issue AddTechToQueueOrder
    
  // 6. Military build check
  if faction has 0 combat ships:
    if shipyard exists: queue build a basic gunship
```

### Orders Already Available (NPC Uses the Same Ones as Player)

| Action | Order Class | Notes |
|--------|-------------|-------|
| Build an installation | `AddToConstructionQueueOrder` | Same as player queuing a mine or factory |
| Queue a tech | `AddTechToQueueOrder` | Same as player picking a tech in the research UI |
| Queue a production job | `AddToConstructionQueueOrder` | Same order, different design type |
| Move cargo | `CargoTransferOrder` | Manual inter-colony shipment |
| Fire weapons | `SetFireControlOrder` | Already works for NPC ships |
| Launch a fleet | `FleetMoveOrder` | Already exists |

**The NPC decision loop does not need new systems — it needs a thin orchestration layer on top of existing systems.**

---

## Implementation Priority Order

Listed from highest leverage to lowest. Each item's value is proportional to how much it unlocks downstream.

### Priority 1 — Fix the Installations UI Tab
**Files:** `Pulsar4X/Pulsar4X.Client/Interface/Windows/PlanetaryWindow.cs`, lines 107 and 221  
**Change:** `HasDataBlob<InstallationsDB>()` → `HasDataBlob<ComponentInstancesDB>()`  
**Why first:** The player can't see what's installed on their colonies. This is the single most visible broken thing in the game. It's also a 2-line fix. See `SESSION_STATE.md` Phase 2a for the full details.

### Priority 2 — Wire Processed Materials into Component Costs
**Files:** `GameData/basemod/TemplateFiles/weapons.json`, installation templates  
**Change:** Add electronics, plastic, stainless-steel to `ResourceCost` formulae for weapons and advanced components  
**Why second:** Without this, the entire refinery chain has no purpose. Weapons should cost electronics. Shipyards should cost stainless-steel. Without this dependency, players mine and refine in separate silos that never connect.

### Priority 3 — Add Three Missing Mineral Recipes
**File:** `GameData/basemod/TemplateFiles/materials.json`  
**Change:** Add nickel-steel, lithium-battery, ree-magnetics recipes  
**Wire them in:** Nickel-steel as an armor type; lithium-battery for capacitors; ree-magnetics for sensors  
**Why third:** 3 of 15 minerals are useless. Any colony that happens to mine them is wasting cargo space. Adding recipes transforms them into strategic resources.

### Priority 4 — Connect Trade to the Wealth Ledger
**Files:** `GameEngine/Logistics/LogisticsProcessor.cs`, `GameEngine/Factions/Ledger.cs`  
**Change:** On successful cargo transfer between factions (or even same-faction inter-colony for colonist incentives), add/subtract from `Ledger.Money`  
**Why fourth:** Commerce generates no income. Trade is economically meaningless. Once trade earns money, factions with good commercial positions get richer, which creates actual strategic differentiation.

### Priority 5 — Build the NPC Decision Loop
**New file:** `GameEngine/Factions/NPCDecisionProcessor.cs`  
**Why fifth:** This is the highest-leverage capability addition — it makes every other system the NPCs can use. But it depends on priorities 1–4 being in place first, so the NPCs are doing meaningful things (building useful components, refining actually-needed materials, etc.).

### Priority 6 — Population Resource Consumption
**File:** `GameEngine/Colonies/PopulationProcessor.cs`  
**Change:** Add per-capita water and hydrocarbon consumption; colony can't grow if supplies run below threshold  
**Why sixth:** Makes the economy feel alive. Without it, a colony set up on any rock will grow indefinitely for free.

---

## Files to Read Before Touching Each System

| System | Read First |
|--------|------------|
| Mining | `GameEngine/Industry/CLAUDE.md` |
| Refining / Production | `GameEngine/Industry/CLAUDE.md` |
| Cargo / Storage | `GameEngine/Industry/CLAUDE.md` (CargoStorageDB section) |
| Logistics / Commerce | `GameEngine/Logistics/CLAUDE.md` |
| Research / Tech | `GameEngine/Tech/CLAUDE.md` |
| Colony population | `GameEngine/Colonies/CLAUDE.md` |
| Material damage connection | `GameEngine/Damage/CLAUDE.md`, `GameEngine/Weapons/CLAUDE.md` |
| NPC AI (new) | This document — no subsystem CLAUDE.md exists yet |

---

## Key Code Locations

| Purpose | File |
|---------|------|
| Mineral definitions (15 minerals) | `GameData/basemod/TemplateFiles/minerals.json` |
| Processed material recipes | `GameData/basemod/TemplateFiles/materials.json` |
| Installation build costs | `GameData/basemod/TemplateFiles/installations.json` |
| Cargo type definitions | `GameData/basemod/TemplateFiles/cargoTypes.json` |
| Weapon build costs | `GameData/basemod/TemplateFiles/weapons.json` |
| Daily mining processor | `GameEngine/Industry/MineResourcesProcessor.cs` |
| Mining rate calculation | `GameEngine/Industry/MiningHelper.cs` |
| Daily production processor | `GameEngine/Industry/IndustryProcessor.cs` |
| Production execution logic | `GameEngine/Industry/IndustryTools.cs` |
| Colony mining state | `GameEngine/Industry/MiningDB.cs` |
| Mineral deposit data | `GameEngine/Industry/MineralsDB.cs` |
| Infrastructure efficiency | `GameEngine/Industry/InfrastructureProcessor.cs` |
| Cargo storage container | `GameEngine/Storage/CargoStorageDB.cs` |
| Automated freight market | `GameEngine/Logistics/LogisticsProcessor.cs` |
| Freight base state | `GameEngine/Logistics/LogiBaseDB.cs` |
| Faction wealth ledger | `GameEngine/Factions/Ledger.cs` |
| Daily research processor | `GameEngine/Tech/ResearchProcessor.cs` |
| Research lab state | `GameEngine/Tech/ResearcherDB.cs` |
| Tech data model | `GameEngine/Tech/Tech.cs` |
| Damage material lookup | `GameData/basemod/TemplateFiles/damageResistance.json` |
| Beer-Lambert absorption | `GameEngine/Damage/DamageComplex/DamageTools.cs` |

---

## What This Survey Did Not Cover

- **Orbital bombardment damage to colony resources** — the commented-out block in `DamageProcessor.cs` (~lines 101–181) has the design intent but references types that may no longer exist. Read that block before designing any ground bombardment system.
- **Ground unit construction** — ground forces don't exist yet. When added, they should consume minerals via the same production system (they are components, just with `MountType: GroundUnit`).
- **Ship armor material selection at design time** — the Beer-Lambert system exists but ships don't currently have a way to specify what material their hull is made of. Connecting ship design to `DamageResistBlueprint` material is the next combat-economy integration point.
- **Tech research as a mineral consumer** — not implemented; considered low priority since the money-cost model is simpler and still creates resource pressure via the Ledger.
