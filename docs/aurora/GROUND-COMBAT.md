# Aurora 4X — Ground Combat & Ground Forces (Design Reference)

Source: aurora-manual `13-ground-forces/` (v2.7.1) + AuroraWiki `C-Ground_Units` / `C-Ground_Combat`.
Status: design reference. Constants are approximate — see `INDEX.md` caveats. ⚠️ = cross-source conflict.

This is the **single largest gap** between Pulsar and Aurora. Pulsar has **no ground combat at all** — no ground unit entity, no formation concept, no ground combat processor, no invasion order, no UI. Everything below is new work. The good news: Aurora's "units are designed from researched components" model maps almost 1:1 onto Pulsar's existing **component/attribute/`ComponentInstancesDB`** framework (see `CONVENTIONS.md` → "Units are component-bearing entities").

---

## 1. Organisational Hierarchy

Aurora ground forces are four nested tiers:

```
Ground Unit Class      ← a single soldier or vehicle "design" (analogous to a ShipDesign)
   │  (researched, built from components: weapons, armor, support gear)
   ▼
Formation Element      ← N identical Ground Unit Classes grouped (e.g. "120× Infantry")
   │
   ▼
Formation              ← multiple Elements that move as one unit; splits into Elements in combat
   │
   ▼
Formation Template     ← the blueprint a Formation is built from (like a saved loadout)
```

- **Movement** happens at the Formation level (a Formation loads/transports/drops as a whole).
- **Combat** happens at the individual-unit level — each soldier/vehicle in each Element rolls to hit.
- A single Formation **cannot be split across transport ships** — it must fit in one vessel's troop capacity.

---

## 2. Ground Unit Base Types

Each Ground Unit Class is built on one base type, which sets equipment slots, hit points, and fortification ceiling.

| Base Type | Equip Slots | Self-Fort | Max Fort | Hit Points | To-Hit modifier vs it |
|-----------|:-----------:|:---------:|:--------:|:----------:|:---------------------:|
| Infantry | 1 | 3 | 6 | 1 | 1.0 |
| Light Vehicle | 1 | 2 | 3 | 3 | 0.4 |
| Medium Vehicle | 2 | 2 | 3 | 4 | 0.6 |
| Heavy Vehicle | 2 | 2 | 3 | 6 | 0.8 |
| Super-Heavy Vehicle | 3 | 1.5 | 2 | 12 | 0.9 |
| Ultra-Heavy Vehicle | 4 | 1.25 | 1.5 | 24 | 0.95 |
| Static Weapon | 1 | 3 | 6 | 3 | 1.0 (immobile) |

Larger vehicles are **harder to hit** (lower to-hit modifier) but cost more and fortify less. Infantry is cheap, fortifies best, dies to one penetrating hit.

---

## 3. Components (Equipment)

A Ground Unit Class fills its slots with researched components. Every component carries: **Size (tons)**, **Armor-Penetration (AP)**, **Damage**, **Shots/round**, plus any special role.

### Weapons
| Component | AP | Dmg | Shots | GSP | Notes |
|-----------|:--:|:---:|:-----:|:---:|-------|
| Personal Weapons | 1 | 1 | 1 | 1 | Anti-infantry baseline |
| Crew-Served Anti-Personnel | 1 | 1 | 6 | 6 | High shots, low pen |
| Light/Medium/Heavy Anti-Vehicle | e.g. 4 | 4 | 1 | 16 (med) | High pen, single shot |
| Super-Heavy Anti-Vehicle | high | high | 1 | — | Super/ultra-heavy chassis only |
| Light/Med/Heavy/Super-Heavy Bombardment | 2 | 6 | 3 | 36 (heavy) | **Indirect fire** (Support position) |
| Light/Medium/Heavy Anti-Aircraft | — | — | — | — | Fires at ground-support fighters |

**GSP (Ground Support Points)** = `AP × Damage × Shots`. This is both the supply-consumption metric and the firepower metric.

### Support / Non-combat
| Component | Size | Effect |
|-----------|------|--------|
| Headquarters (HQ) | varies | Command capacity in tons. Infantry HQ ≈ 24,000 t; medium ≈ 45,000 t; high command ≈ 250,000 t. Over-capacity → bonuses scale down proportionally. |
| Forward Fire Direction (FFD) | — | Each FFD enables **1 orbital-bombardment support ship** to assist this formation. |
| Construction Equipment | 150 t | 0.05 Construction-Factory-Equivalent each (terraforming/building from the field). |
| Standard Logistics Module | 50 t | Carries 1,000 GSP. Light-vehicle+; can resupply *other* formations. |
| Small Logistics Module | 10 t | Carries 100 GSP. Infantry-only; resupplies *own* formation only. |
| CIWS | — | Close-in defence vs incoming missiles (planetary point defence). |
| STO (Surface-To-Orbit) | — | Energy weapon that fires at ships in orbit. |
| Geosurvey / Xenoarchaeology / Decontamination | 100 t | Field survey, ruin research, radiation cleanup (+~0.01%/yr). |

### Armor (on the unit)
- **Base Armor Rating** × unit size → construction cost contribution (6-armor unit costs ~50% more than 4-armor).
- **Racial Armor Rating** = base × faction's best armor tech.
- Armor tech ladder: Conventional Steel(1) → Composite(2) → Adv Composite(3) → Duranium(4) → High-Density Duranium(6) → Composite(8) → Ceramic Composite(10).
- Armor/HP/components **freeze at the tech level present at design time** (like Pulsar component designs — see `CONVENTIONS.md`).

### Genetic enhancement (infantry only)
| Tier | HP mult | Cost mult |
|------|:-------:|:---------:|
| Basic | ×1.25 | ×1.5 |
| Improved | ×1.6 | ×2.0 |
| Advanced | ×2.0 | ×2.5 |

---

## 4. Combat Resolution

**Cadence:** one combat round per ⚠️ **3 hours (wiki) / 8 hours (manual)** of game time, resolved after the naval phase, whenever hostile forces share a body.

### Field positions

This fork adds a **Reserve** position (5th slot) beyond Aurora's four. See §4a for full Reserve / hidden-force rules.

| Position | Can target | Fortification | Visible to enemy | Notes |
|----------|-----------|---------------|-----------------|-------|
| **Front-Line Attack** | any enemy position | forfeits all fort bonuses | Yes | doubled morale gain from kills; needs supply |
| **Front-Line Defence** | front-line enemies only | keeps fort bonuses | Yes | standard morale |
| **Support** | indirect-fire only | n/a | Yes (position known, not composition) | bombardment; med-artillery targets elements, heavy targets whole formations |
| **Rear Echelon** | very limited | n/a | Yes (position known) | logistics; 5% target-size modifier (hard to hit) |
| **Reserve** | none (not yet engaged) | n/a | **No** | hidden force; committed by order to bypass front-line shield |

### Why position is a mechanical shield, not just a label

A formation in Support or Rear Echelon **cannot be targeted** until all formations in front of it are destroyed or routed. This is not optional — it is a hard rule. An artillery battery in Support fires every round in safety while the front line holds. The moment the front line collapses, that battery is exposed and becomes the primary target for everything.

This creates the core dramatic tension of ground combat in this fork:

- **Carriers, siege weapons, heavy artillery, Warlord-class units** belong in Support. Their value is continuous indirect fire while shielded. Their weakness is that direct-fire weapons (which most of them carry for self-defence) **cannot be used from Support position**. A titan in Support fires its Apocalypse missiles; it does not brawl. It is powerful behind the line and vulnerable in front of it.
- **Front-line formations exist to buy time** for the Support position to work. Their morale, fortification, and staying power are what protect the rear.
- **Breaking the front line** is the decisive moment. When it goes, the enemy can suddenly reach the carrier/titan/artillery, and the engagement changes character entirely — from managed attrition to crisis.

### Visual design for position (performance-conscious)

Even with thousands of units, the combat window renders **positions, not individual units.** Formations are grouped into position slots and displayed as cards. A player with 1,000 ground units in 20 formations across 3 positions sees 20 cards, not 1,000 icons.

```
┌─────────────────────────────────────────┐
│  ENEMY FRONT LINE                       │
│  [3rd Infantry Btn] [AT Rgt] [Armor Bn] │
├─────────────────────────────────────────┤
│  ←── engagement zone / target lines ──►│
├─────────────────────────────────────────┤
│  YOUR FRONT LINE                        │
│  [1st Marines]  [2nd Tank Bn]           │
│                                         │
│  YOUR SUPPORT                           │
│  [Artillery Bde]  ← target lines here  │
│  [Carrier Strike Gp]  ← protected      │
│                                         │
│  YOUR REAR ECHELON                      │
│  [Logistics Rgt]                        │
└─────────────────────────────────────────┘
```

Target lines (see `Pulsar4X.Client/CLAUDE.md` — Target Lines spec) run between **position cards**, not individual units. A red line from an enemy Front card to your Support card is the visual signal that your front line is gone and your carrier is taking fire. This is readable at a glance, renders as a handful of rectangles and lines, and scales to any army size.

The same position-slot model applies to **space combat fleet formations**:

| Fleet Role | Typical ships | Protected by | Exposed when |
|-----------|--------------|-------------|-------------|
| Screen | Destroyers, frigates | — (front) | Always |
| Main Line | Cruisers, battleships | Screen | Screen gone |
| Fire Support | Carriers, monitors, siege ships | Main Line | Main Line gone |
| Logistics | Tankers, repair tenders | All of above | Catastrophic loss |

A carrier in Fire Support launches fighters and fires long-range ordnance every round while the Main Line holds. If the Main Line is destroyed, the carrier is suddenly the closest armed vessel — and its point-defense turrets, designed for missiles not warships, are all it has left.

### Hit → penetrate → destroy (three rolls)
```
1. TO-HIT:
   FinalToHit = BaseToHit(≈20%) × TerrainMod × MoraleRatio × BaseTypeToHitMod
               ─────────────────────────────────────────────────────────────
                       Fortification × EnvironmentMod × DefensiveValue

2. PENETRATE (if hit):
   PenChance = (WeaponAP / TargetArmor)²        (auto if AP ≥ Armor)

3. DESTROY (if penetrated):
   DestroyChance = (WeaponDamage / TargetHP)²    (auto if Damage ≥ HP)
```
Destroyed elements are removed immediately. Fortification does **not** decrease from casualties, but **moving or loading a formation resets fortification to 0**, and units re-fortify to their self-fort level over **30 days**.

### Terrain
| Terrain | Fort mult | To-hit mult |
|---------|:---------:|:-----------:|
| Steppe / Barren | 1.0 | 1.0 |
| Desert | 0.75 | 1.0 |
| Swamp | 0.5 | 1.0 |
| Temperate Forest | 1.25 | 0.5 |
| Mountain | 2.0 | 0.5 |
| Jungle | 1.5 | 0.25 |
| Jungle-Mountain | 3.0 | 0.125 |
| Rift Valley | 1.5 | 0.75 |

Combat-capability specialisations (Mountain/Jungle/Desert Warfare, Extreme Temp/Pressure/Gravity, Boarding) each apply a 0.5× to-hit penalty *against* a specialised unit in that terrain (i.e. 2× effective accuracy for the specialist). They stack multiplicatively.

### Morale & breakthrough
- Max morale = `100 + 5 × commanderTrainingBonus%`.
- Effectiveness scales linearly: `Strength × Morale/100`.
- Breakthrough = `Cohesion × BreakthroughRating`; at ≥30% the attacker gets extra attacks.
- Morale recovers between rounds per commander training bonus; base post-drop recovery ≈ 100/yr.

---

## 4a. Reserve Forces and NPC Tactical AI (fork additions)

### Reserve position — hidden and cloaked forces

The **Reserve** position is a fifth field slot not present in vanilla Aurora. It works differently from all others:

**Rules:**
- A formation in Reserve **cannot fire** and **cannot be targeted** — it is sitting out of the engagement entirely.
- The enemy does **not automatically see it** in the position display — but see Detection below.
- The controlling player (or AI) issues a **Commit Reserve** order designating a specific enemy position as the target.
- On commitment, the Reserve enters the engagement **directly against the designated enemy position**, bypassing the normal front-line shield rule. This represents a flanking maneuver, ambush, airdrop, or tunneling assault.
- After commitment the formation is treated as Front-Line Attack. Normal rules apply from that point.

**Reserve detection — concealment is earned, not automatic**

A Reserve in the open with all systems running is detectable. Hiding is an active effort with three layers:

| Concealment layer | What provides it | Time cost | Trade-off |
|------------------|-----------------|-----------|-----------|
| **EMCON** (emissions control) | Shutting down non-essential comms, radar, active sensors | Hours to implement | Force is partially blind while dark |
| **Environmental cover** | Dense forest/jungle, nebula, underground, urban clutter — multiplies enemy detection difficulty | Time to move into position | Moving resets fortification to 0 |
| **Active stealth component** | Tech-gated hardware installed on unit design | None (permanent) | Cost, reduced mobility, maintenance |

Each round an enemy with sensor capability rolls a detection check against the Reserve's concealment rating. If the check succeeds, the Reserve's **position is revealed** — the enemy knows it exists, may be able to target it before commitment, and the ambush advantage is lost.

**Commander effect on concealment:** A high-GCM (manoeuvre) commander maintains EMCON discipline — troops don't break radio silence early, movement is timed to reduce sensor returns. A poor commander's forces are sloppy: random EM spikes, early movement. On the detecting side, a commander with sensor/intel specialization gets bonus to detection checks against enemy Reserves.

**Hidden vs cloaked:**

| Type | What enemy sees without detection check | Tech required |
|------|----------------------------------------|--------------|
| Reserve (basic EMCON + cover) | Nothing if concealment holds; revealed by successful detection check | None |
| Sensor-cloaked Reserve | Nothing; significantly higher detection threshold | Cloaking component (tech-gated) |

---

### NPC tactical AI — phased approach

**Phase C (initial): Utility scoring**

Each formation evaluates its options each round by scoring them from observable game state and picks the highest score:

```
Score(stay Front-Line Attack)   = MyStrength × EnemyFrontThreat × MySupplyLevel
Score(pull back to Support)     = CasualtyRateThisRound × MyIndirectFireValue × ReplacementAvailable
Score(commit Reserve)           = FrontLineCritical × ReserveStrength × EnemyRearVulnerability
Score(full retreat)             = (1 - Morale) × (1 - StrategicValueWeight) × ReinforcementETA
```

Fast, deterministic, debuggable. Good enough to require real player thought. Predictable once the player learns it — which motivates Phase D.

**Phase D (improved): Hybrid commander GOAP + formation utility**

Goal-Oriented Action Planning (GOAP) — used in F.E.A.R., Halo, serious strategy AI — is not machine learning. The commander holds a *goal* ("destroy enemy carrier", "hold colony for 3 days until reinforcements arrive") and finds a sequence of actions that achieves it by doing A* pathfinding through an action graph. Still runs in microseconds.

Example action chain for "destroy enemy carrier":
```
Goal: EnemyCarrierDestroyed
Action sequence:
  [HoldFrontLine]       precondition: FrontLineExists    effect: BuysTime
  [WaitForExposure]     precondition: BuysTime           effect: EnemySupportExposed
  [CommitReserve]       precondition: EnemySupportExposed effect: ReserveTargetsCarrier
  [PressThroughRear]    precondition: ReserveEngaged     effect: EnemyCarrierDestroyed
```

The commander runs GOAP. Formations under that commander use utility scoring to execute — but the commander shifts their score weights: "hold front line" is up-weighted for all formations until the commit signal fires.

This is a multi-step plan, not reactive scoring. The Reserve gets committed at the *right moment* because the plan requires it, not because the current round's FrontLineCritical score happened to tip over a threshold.

**The structure supports this cleanly in Pulsar's ECS:**
- Commander entity gets a `TacticalGoalDB` and a `TacticalPlanDB`
- Formation entities keep their utility scoring but accept weighted priorities from the commander
- Adding GOAP to the commander is additive — it doesn't require rewriting formation logic

**Ruled out permanently:**
- Reinforcement learning (requires massive offline training, can't run on weak hardware)
- LLMs (latency, cost, requires internet, no offline play)

**Commander skill as quality multiplier (both phases):**

| Commander rating | Effect |
|----------------|--------|
| High **GCM** (manoeuvre) | Reserve commit timing more precise; EMCON maintained longer |
| High **GCO** (direct fire) | Attack/hold threshold better calibrated |
| High **GCT** (training) | Morale term recovers faster; retreat scored less |
| Low skill | Scoring is noisier; GOAP chains break earlier |

**Faction personality (JSON-moddable weight constants):**

| Archetype | Scoring bias |
|----------|-------------|
| Aggressive | Attack ×1.5, retreat ×0.3 |
| Defensive | Fortification ×2.0, advance ×0.5 |
| Guerrilla | Reserve/ambush ×2.0, never holds a fair fight |
| Fanatical | Morale term removed from retreat; holds until destroyed |
| Swarm | CasualtyRate ignored; always commits everything |

---

### Commander capacity cap

Every commander has a `CommandCapacity` — the maximum effective size of forces under their control. This is the mechanism that prevents "stack of doom" without an artificial flat cap.

**Ground forces:** Capacity is in tons of unit mass, matching Aurora's HQ component already in §3. Command capacity = HQ type installed on the headquarters formation element.

**Space fleets:** Capacity is in total Hull Size (HS) of ships in the fleet. An admiral's rank and skill determine how large a fleet they can command effectively.

**Efficiency scaling:**

| Force size vs capacity | Effect |
|-----------------------|--------|
| ≤ 50% | Commander underutilized — no penalty, wasted potential |
| 51–100% | Full bonuses apply |
| 101–150% | Bonuses scale down proportionally with overage |
| > 150% | Coordination breaks — formations lose same-position synergy bonuses |

You can assign more units than a commander can handle — nothing stops you — but effectiveness degrades. A large invasion requires either a high-rank commander with large CommandCapacity or multiple sub-commanders each commanding their own fleet/formation group. This creates real decisions: promote a capable commander and wait for them to develop, or split the force into smaller independently-commanded groups.

**Stellaris comparison:** Stellaris uses flat naval capacity as a global resource pool. This design uses per-commander capacity instead. The result is that force size is bounded not by a game-wide resource but by the quality of the officer corps — which makes the People system meaningful rather than decorative.

---

## 5. Supply & Logistics

- Each unit carries **inherent supply for 10 combat rounds**.
- Consumption per 10 rounds = its total **GSP** (`AP×Dmg×Shots` summed over weapons).
- Out of supply → **25% fire rate** and **cannot take Front-Line Attack**.
- Resupply hierarchy: own logistics elements → parent formation → up the chain. Vehicle logistics can cross formations; infantry logistics cannot.
- Engaging ground **and** air simultaneously doubles consumption.
- **Maintenance:** 12.5% of build cost per year as a wealth expense, regardless of combat.

---

## 6. Transport, Drop, Boarding (the invasion pipeline)

### Troop transport bays (ship components)
| Bay | Ship Size (HS) | Capacity (t) | Cost (BP) |
|-----|:--------------:|:------------:|:---------:|
| Very Small | 2 | 100 | 3 |
| Small | 5 | 250 | 6 |
| Standard | 20 | 1,000 | 20 |
| Large | 100 | 5,000 | 80 |
| Very Large | 500 | 25,000 | 320 |

- Load/unload only at a body **with a colony marker**. Base ≈ 10 days for a brigade-sized load, reduced by cargo-handling systems / spaceport.
- A Formation must fit entirely in one ship.

### Orbital drop
- Only drop-capable units can combat-drop.
- Sequence: orbital superiority → bombard defences → combat-drop → reinforce after beachhead.
- **Drop-module morale decay:** non-cryo modules lose **1 morale/day while loaded**. Cryogenic drop modules eliminate the loss.
- **Drop casualties:** a % of elements lost on descent; enemy AA raises it; heavier units suffer more; ground-combat tech lowers base rate.

### Boarding
- Boarding-equipped transport bay + **all-infantry** formation with Boarding Combat capability.
- Target ship must not be faster than the attacker; fleet must end movement at the target.
- Boarding Combat doubles transfer success and doubles to-hit in internal combat.

### Capture & occupation
- On conquest: reparations (wealth/minerals), possible alien tech from research installations, **intact installations begin producing for the conqueror**, population becomes subject pop.
- **Required garrison** ≈ `Population(millions) × (Determination/100) × (Militancy/100)`.
- **Occupation strength** per element ≈ `sqrt(Size × Units × Morale) / 10,000`.
- Police-specialised light infantry suppress unrest cheaply (distinct from frontline formations).

---

## 7. Construction (building ground forces)

- Built at a colony with a **Ground Force Construction Complex (GFCC)**: ≈500,000 cargo points, ~2,400 Vendarite, ~1,000,000 workers, base **250 BP/yr** (researchable to 500 / 1,000).
- Process mirrors ship/installation construction: queue Formation Templates → BP accrues from colony mineral stockpile → completes when accrued BP = cost.
- v2.7 auto-builds **replacement units** for formations with assigned templates that took losses (priority value, default 10; higher = replaced first). Replacements substitute by **Unit Series**, enabling auto-upgrade.
- Unit-class development cost (RP) ≈ `sqrt(BPcost × 25000)` for non-STO ground units (vs `sqrt(BPcost × 5000)` for most ship components).

### Commander bonuses (ground)
GCD (defence/fort), GCO (direct-fire), GCA (artillery), GCAA (anti-air), GCL (logistics), GCM (manoeuvre/breakthrough), OCC (occupation), GCT (training/morale), plus SRV/XEN/DEC for field specialists. Over command-capacity → bonuses scale down.

---

## 8. Pulsar Mapping (how to build this in-engine)

Mirror Pulsar's ship pipeline. A ground unit is **an entity with `ComponentInstancesDB`**, exactly like a ship — its weapons/armor/support gear are components with `*Atb` attributes, queried via `TryGetComponentsByAttribute<T>()`.

| Aurora concept | Proposed Pulsar implementation | Reuses existing |
|----------------|-------------------------------|-----------------|
| Ground Unit Class | `GroundUnitDesign : IConstructableDesign` (parallel to `ShipDesign`) | `ComponentDesigner`, research/unlock |
| Unit components (weapons/armor/HQ/logistics) | `ComponentDesign`s with new `*Atb` types (`GroundWeaponAtb`, `GroundArmorAtb`, `LogisticsAtb`, `HeadquartersAtb`…) | `ComponentInstancesDB`, attribute query |
| Formation Element / Formation | `GroundForceDB` on a ground-force entity (holds element list, position, morale, fortification, supply) | DataBlob + copy-ctor `Clone()` |
| Formation Template | JSON blueprint under `Data/basemod/` + a `*Blueprint` class | mod loader |
| Combat round | `GroundCombatProcessor : IHotloopProcessor` (RunFrequency = combat cadence) | auto-discovery, hot-loop |
| To-hit / pen / destroy | a `GroundDamage` helper — **reuse the complex `DamageProcessor` path once it is fixed** (Phase 1), do not build a parallel damage system | `Damage/DamageComplex/` |
| Build at GFCC | new `IConstructableDesign` + `IndustryJob` subtype, GFCC is a component (`IndustryAtb`-like) on the colony | `Industry/`, `ConstructStuff()` |
| Transport bay / drop / boarding | new `INavAction`s appended to `NavSequenceDB`; troop bay = ship component | `Movement/NavSequence/` |
| Invade / drop / board orders | `EntityCommand` subclasses via `OrderableDB` | Orders system |
| Garrison / occupation / unrest | fields on `GroundForceDB` + a colony `OccupationDB`; feed `PopulationProcessor` | `Colonies/` |
| Orbital bombardment ↔ ground | the colony-damage branch in `DamageProcessor` (currently commented out, ~lines 101–181) | `Damage/` Phase 3 |
| Ground UI | `GroundForcesWindow` + a `PlanetaryWindow` tab; reuse `ComponentInstancesDBDisplay` for unit loadouts | `Pulsar4X.Client/` |

**Dependency note:** ground-combat damage must sit on the **complex `DamageProcessor`**, which is currently stubbed/placeholder (`SimpleDamage` is active instead). That is why docs/archive/PLAN.md Phase 1 (fix complex damage) is a hard prerequisite for ground combat — see root `CLAUDE.md` gotcha #1 and `Damage/CLAUDE.md`.

---

## 9. Weapon Specialisation — Why "Bigger Number" Doesn't Win

This section captures a deliberate design decision for this fork: **the right tool must beat the wrong tool regardless of raw quantity.** A formation armed entirely with point-defense turrets should not defeat a formation built for direct-fire combat, even if the PD turrets have nominally higher total attack power.

### How Aurora handles it (the AP/Armor floor)

Aurora's two-roll combat sequence (penetrate → destroy) already enforces this, but it isn't obvious at first glance. The key is the penetration roll:

```
PenetrationChance = (WeaponAP / TargetArmor)²
```

Personal weapons have AP 1. A medium vehicle has Armor 4. Penetration chance = `(1/4)² = 6.25%`. Effectively zero — your rifle squad is useless against that tank. The formula means **wrong-tool weapons scale down to nearly nothing against the wrong target class**, not just "do less damage."

| Weapon | AP | vs Infantry (armor 1) | vs Light Vehicle (armor 4) | vs Heavy Vehicle (armor 6) |
|--------|:--:|:---------------------:|:--------------------------:|:--------------------------:|
| Personal Weapons | 1 | 100% pen | 6.25% pen | 2.8% pen |
| Anti-Vehicle (Med) | ~8 | auto | auto | 178% → auto |
| Bombardment (Heavy) | ~2 | 400% → auto | 25% pen | 11% pen |

Artillery is effective against infantry and emplaced positions; useless against armored vehicles. Anti-vehicle missiles are devastating against tanks; wasteful against rifle squads. The composition of your formation determines who you can and cannot fight effectively.

### What this fork adds beyond Aurora (design intent)

Aurora's AP/Armor system is the mechanism but the **weapon design UI doesn't make it obvious**. When a player designs a ground unit, they should see clearly what it's effective against. Proposed additions:

**1. Target class labels on unit designs**

Every Ground Unit Class should display its effective target classes in the design window — similar to how Aurora's `To-Hit modifier vs it` column tells you how hard something is to hit. Show derived effectiveness:

| Unit Type | Strong vs | Weak vs |
|-----------|-----------|---------|
| Rifle Infantry | Infantry | Vehicles, Emplacements |
| Anti-Tank Squad | Vehicles | Infantry (overkill AP), Air |
| Artillery Battery | Emplacements, Infantry | Vehicles (too mobile), Air |
| CIWS Platform | Missiles, Fighters | Everything ground |

**2. Formation composition warnings**

When a player assembles a Formation from Elements, the UI should flag imbalanced compositions: "This formation has no anti-armour capability" or "No anti-air elements — vulnerable to ground-support fighters."

**3. The space-combat parallel (PD turrets vs destroyers)**

The same principle applies to ships. A carrier with only point-defense turrets has high nominal "weapon count" but near-zero AP against armored warship hulls. When space combat damage is wired properly (Phase 1), beam weapon AP vs ship armor should produce the same discriminator. The `DamageComplex` path has the weapon penetration mechanics — they just need to be active and the weapon designs need realistic AP values.

**Design rule: weapon specialisation must be visible, not hidden in formulas.** The player should be able to look at a unit or ship design and understand immediately what it counters and what counters it. If that's not readable from the UI, the balance is right but the decision-making is broken.
