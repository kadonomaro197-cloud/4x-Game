# AI Capability Catalog — Phase A of the Utility-Brain

**As of 2026-07-12.** Status: **scan complete (knowledge artifact); the CODE catalog + scorer are Phases A-code / B / C.** Produced by a 6-way read-only scan of the buildable catalog (docs/AI-BRAIN-BUILD-TRACKER.md — the utility-brain side objective).

---

## What this is and why it matters

The NPC brain today has a **fixed playbook** — the objective resolvers name a hardcoded move per goal. A real player picks from the **entire component/installation catalog** and combines it (sensor posts, theater armies, influence structures). To close that gap the AI needs a **machine-readable inventory of everything it can build and what NEED each buildable serves** — its parts manual — so a future **utility scorer** can: perceive its biggest gap → look up the options that serve it → score by payoff → build the best. ("Build a long-range sensor post" then falls out for free: intel gap + a sensor part that advertises detection = top score, un-hardcoded.)

This doc is that inventory (Phase A: the knowledge). The pattern is already proven small: `MilitaryTarget.BestEnemyTarget` scores enemy worlds by value×reach and picks the best — the catalog generalizes that from "which world" to "which of all my options."

**The load-bearing fact:** every buildable is a `ComponentTemplate` (JSON) whose `AtbConstrArgs` bind a `*Atb : IComponentDesignAttribute` class that *declares what it does*. So the catalog is READ, not authored — a future `CapabilityCatalog` walks `ModDataStore` and files each template under a NEED by its atb.

Shared NEED vocabulary: **INTEL · OFFENSE · DEFENSE · ECONOMY · TECH · GROWTH · REACH · PEOPLE · INFLUENCE · GOVERNANCE.**

---

## The catalog — NEED → buildables (atb · template · start-unlocked)

### INTEL (see the enemy / hide from them)
- `SensorReceiverAtb` (`Sensors/SensorRecever/SensorReceiverAtb.cs:10`) → `passive-sensor` (electronics.json:63) ✅start, pre-installed
- `BeamFireControlAtbDB` (`Weapons/WeaponFireControl/BeamFireControlAtbDB.cs:9`) → `beam-fire-control`, `pd-director` ✅start (targeting; pd-director = DEFENSE/CIWS)
- `JammerAtb` (`Sensors/SensorEmitter/JammerAtb.cs:37`) → `jammer` ✅start (gated `EnableJamming`)
- `CloakAtb` (`Sensors/SensorEmitter/CloakAtb.cs:28`) → `cloak-device` ✅start
- `GroundSensorAtb` (`GroundCombat/GroundSensorAtb.cs:21`) → `ground-radar` ✅start
- `SensorSignatureAtb` — emitter/detectability, rides reactors/engines/ordnance (not standalone)
- Survey enablers: `JumpPoints/GravSurveyAtb.cs`, `GeoSurveys/GeoSurveyAtb.cs`
- *EMCON = a POSTURE order (`FleetEmconDB`), not a buildable.*

### OFFENSE (firepower)
- Space live-fire: `GenericBeamWeaponAtb` → `laser-weapon`, `pulse-laser`; `MissileLauncherAtb` → `missile-launcher` ✅start
- Space auto-resolve: `RailgunWeaponAtb`→`railgun-weapon`/`siege-railgun`, `FlakWeaponAtb`→`flak-weapon`, `PlasmaBoltWeaponAtb`→`plasma-repeater`, `DisruptorWeaponAtb`→`disruptor-weapon` (anti-shield exotic) ✅start
- Ground: `GroundWeaponAtb` → `ground-rifle`/`-autocannon`/`-cannon`/`energy-weapon`/`claw-weapon`; `GroundUnitAtb` weapon side → `infantry`/`armor`/`artillery-unit` ✅start
- Enhancers: `UnitCaliberAtb` (firepower+toughness) `unit-caliber`; `ShipMagazineAtb`/`GroundMagazineAtb` (ammo sustain — labelled "Defense" in JSON but serve OFFENSE)
- Ordnance warheads via `OrdnanceDesignFromJson` (separate path, not ComponentTemplates)

### DEFENSE (survivability)
- Space: `ShieldAtb`→`deflector-array`, `ArmourHardeningAtb`→`armour-hardening` (nature-tuned), `PointDefenseAtb`→`point-defense-mount` (missile intercept), `RadiatorAtb`→`heat-radiator` (sustain), `ShipHullAtb`→`ship-hull`(light/med/heavy = mass budget) ✅start; base armour = `armor.json` material blueprints (thickness per design, not a mounted component)
- Ground: `GroundDefenseAtb`→`bunker` (fortification), `GroundArmorAtb`→`ground-`/`ablative-`/`reactive-plating` (nature-tuned), `GroundAugmentAtb`→`power-armor`/`shield-generator`/`ward-projector`/`reflex-booster`, `GroundChassisAtb`→frames (HP) ✅start

### ECONOMY & INDUSTRY (make stuff)
- `MineResourcesAtbDB`→`mine`/`automine`; `IndustryAtb`→`refinery`/`factory`/`shipyard`; `InfrastructureCapacityAtb`→`infrastructure`/`space-habitat`; `LocalConstructionAtb`→`local-construction`; `CargoStorageAtb`→`warehouse`/holds; `EnergyGenerationAtb`→`reactor`/`rtg`/`steam-turbine`; `EnergySolarGenerationAtb`→`solarArray`; `EnergyStoreAtb`→`battery-bank` ✅start
- Power is DUAL: colony grid (ECONOMY) + ship reactor/battery (REACH/OFFENSE supply)

### TECH · GROWTH · PEOPLE
- TECH: `ResearchPointsAtbDB`→`research-lab` (spawns the lab that grows the tech tree) ✅start
- GROWTH: `HousingAtbDB` (comfort→morale), `PopulationSupportAtbDB` (life-support cap) → `infrastructure`/`space-habitat` ✅start
- PEOPLE: `NavalAcademyAtb`→`naval-academy` (graduates officers — **NOT start-unlocked, tech-gated**); `AdminSpaceAtb`→`admin-complex`/`ship-command` (command seats/span-of-control) ✅start
- *Scientists are SPAWNED, not buildable (no scientist-academy). Ties to Exploration X.0 "make competence researchable."*

### REACH (move ships / cargo / troops)
- `NewtonionThrustAtb`→`conventional-engine`/`scntr-engine` (sublight); `WarpDriveAtb`→`alcubierre-warp-drive` (the sole player FTL); `ReactionlessThrustAtb`→`reactionless-drive`; `InertialessDriveAtb`→`inertialess-drive` (evasion); `GroundLocomotionAtb`→`ground-locomotion`; `CargoStorageAtb`/`CargoTransferAtb`→holds/`spaceport`/shuttlebay; `GroundBayAtb`→`troop-bay` (invasion transport) ✅start; `LogiBaseAtb`→`logistics-office` (**not start-unlocked**)
- *No jump-DRIVE exists — inter-system travel is through natural jump points; warp is the only built FTL.*

---

## THE GAME GAPS — needs with NO buildable (the important half)

A utility AI can only choose an option that **exists as a buildable**. Four needs have **none** — so they are holes in the GAME, not the AI. Common root: **`Pulsar4X/GameEngine/Factions/` contains ZERO `IComponentDesignAttribute` classes** — those systems are strategic-AI plumbing (DataBlobs/processors/catalogs), entirely off the component/industry build rails.

| Need | State | Verdict |
|---|---|---|
| **INFLUENCE** (cultural / soft-power conversion of population) | No `InfluenceAtb`, no culture component anywhere. Morale/legitimacy exist only as processor-computed gauges. | **GAP** — the AI (and player) can never "build influence." *This is exactly the developer's "convert their people instead of war" — it needs a new system before the AI can choose it.* |
| **DIPLOMACY-as-a-buildable** | Full engine (`DiplomacyDB`/`Treaties`/`ReactiveDiplomacy`/`ExchangeCatalog`/`FirstContact`) but ZERO components — no embassy/envoy installation. | **GAP** — diplomacy is played through orders, not built. |
| **ESPIONAGE-as-a-buildable** | `CovertActionCatalog` (StealTech/Sabotage/SowUnrest…) + `CovertRisk`/`InformationLedger` exist, but no spy-agency/intel-network component. | **GAP** — no buildable spy apparatus; catalog is data-only. |
| **GOVERNANCE (policy/politics)** | `GovernmentDB`/`TaxPolicy`/`PoliticalBlocs` are non-buildable modulators. Only span-of-control (`AdminSpaceAtb` → `admin-complex`/`ship-command`) is constructible. | **PARTIAL GAP** — admin capacity buildable; regime/policy is not. |

**Implication:** closing the "aware of all its options" delta is *two* jobs — (1) the utility scorer over the buildables that DO exist (Phases B/C), and (2) building the missing player systems (INFLUENCE first, per the developer's interest) so those options exist to be chosen at all. The scan hands us the exact to-do list.

---

## Data bugs the scan surfaced (worth a cheap fix pass)

1. **`spaceport` UniqueID collision** — two different `ComponentTemplate`s share `UniqueID "spaceport"` (`installations.json:664` "Planetary Spaceport Complex" vs `storage.json:108` "Space Port"). Only one survives the mod-load dictionary key; the other is silently dropped.
2. **Broken atb type ref** — `installations.json:736` binds `"Pulsar4X.Datablobs.CargoTransferAtbDB"` (no such class; real type is `Pulsar4X.Storage.CargoTransferAtb`). That transfer attribute won't resolve.
3. **`EmploymentAtbDB` is inert** — the class exists (`Colonies/EmploymentAtbDB.cs:17`, jobs→morale) but **no JSON template instantiates it**, so the "jobs" input is dead until a template carries it.

---

## Build path from here (Phases A-code → B → C)

- **A-code:** a `CapabilityCatalog` that walks `ModDataStore` at game start and files each buildable under a NEED by its atb (this doc = the mapping). Gauge: a laser → OFFENSE, a lab → TECH, a sensor → INTEL.
- **B (needs model):** extend `FactionState` to score the faction's gap on each NEED axis, per-system + empire-wide.
- **C (the scorer):** match catalog options to the biggest gap, score by payoff-vs-cost, and convert resolvers from hardcoded rungs to "ask the scorer" — **one axis at a time** (intel → defense → economy…), not a rewrite.
- **Parallel content track:** build the missing systems (INFLUENCE first) so the gaps become choosable.
</content>
