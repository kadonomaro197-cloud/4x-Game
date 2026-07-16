# Pulsar4X — System Connection Map (the "map the connections first" tool)

**What this is — read this before touching any system.** This is the wiring diagram of the whole
game in table form: for every system, it lists the other systems it's plugged into — what feeds *into*
it, what it feeds, what it shares state with, and what it triggers. It is the **Prime Directive** ("map
the connections first") made into a lookup table. You do not work a system alone: find its row, read
what it connects to, then go read *those* rows too. A change to one station ripples down the line.

**This is the ONE owner of system-to-system connections.** Nothing else in the docs holds the connection
graph — this file is where it lives.

**What this file deliberately does NOT carry** (so it can never rot from stale status marks):
- **Build status** (is it done / partial / dark / absent) lives in **`docs/DOCS-INDEX.md`**.
- **Test & gauge status** (is there a test watching it, and is it green) lives in **`docs/TESTING-TRACKER.md`**.

Keep those two columns out of this file. Here we track connections and nothing else.

*Extracted from `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` on 2026-07-13 — this file lifts out only that doc's
"Connected to" column. If you find a connection this map doesn't list, add it in the same commit as the
change that revealed it.*

---

## Engine core — the ship's power and plumbing (everything rides on these)

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Time loop & system activity** (`MasterTimePulse` / `ManagerSubPulse` / `StarSystem.ActivityState`) | **EVERYTHING** — it decides which systems run at all. A `Stasis` system runs *nothing*. |
| **ECS core** (Entity / DataBlob / EntityManager) | Every system — all state lives in DataBlobs. |
| **Processor scheduling / re-arm** (`ManagerSubPulse`) | Every processor. |
| **Save / Load** | Every DataBlob; `TypeNameHandling` ties saves to class names. |
| **Orders** (`OrderableProcessor`) | Industry, movement, fleets, combat — all player/AI actions route through here. |
| **Modding / data load** (`ModLoader`) | **Everything** — all blueprints/recipes/designs come from here. |
| **Events** (`EventManager`) | Population, combat, industry (publishers); UI log (consumer). |

## Economy & planetary infrastructure

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Mining** (`MineResourcesProcessor`) | Galaxy (`MineralsDB` on planet), Storage (cargo out), Infrastructure (efficiency ×), Colony. |
| **Production / construction** (`IndustryProcessor` + `IndustryTools`) | Storage (inputs in / output out), Mining (feedstock), Infrastructure (×), Ships (builds ships), Components (builds installations via `InstallOn`), Factions (`IndustryDesigns`). |
| **Refining** (via `IndustryProcessor`, `ProcessedMaterial`) | Mining (mineral inputs), Storage. |
| **Infrastructure (efficiency grid)** (`InfrastructureProcessor`) | Components (installations), Colony body (gravity/pressure), **all production** (it's the throttle). |
| **Storage / cargo** (`CargoTransferProcessor`) | Mining, industry, ships, logistics, launch fuel. |
| **Local construction line** (`LocalConstructionProcessor`) | Industry, components — a *second* construction path. |
| **Economy accounting (money)** (`Ledger`, Factions) | Records only `InitialInvestment`/`Research` today; mining, trade, construction generate no money signal (known gap — no P&L). |

## Colony & population

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Population** (`PopulationProcessor`) | Galaxy (atmosphere → colony cost), Infrastructure (pop support cap), Storage (future: food), Morale (migration). |
| **Morale** (`PopulationProcessor` M1, reads `ColonyMoraleDB`) | Population (migration), Galaxy (conditions = colony cost), capacity (overcrowding); roadmap: jobs/tax/power/food/governor. |
| **Governance / delegation** (governors; wires dead `AdminSpaceDB` seat) | People (governor = commander), `AdminSpaceDB` (the seat to wire), morale (what it maintains), tax/M4 (sets happy-medium). |
| **Life support / carrying capacity** (`ColonyLifeSupportDB`) | Population, infrastructure. |
| **Colony hex map** (`ColonyHexMapProcessor`) | Colony, future ground combat (already a spatial substrate). |

## Movement & orbits

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Orbits** (`OrbitProcessor`, `ChangeSOI`/`EnterSOI`, `OrbitUpdateOften`) | Everything with a position; launch-to-orbit; sensors; combat geometry. |
| **Newtonian thrust** (`NewtonionMovementProcessor`, `NewtonSimpleProcessor`) | Ships, fuel (burns propellant), orbits, missiles, combat closing. |
| **Warp / jump movement** (`WarpMoveProcessor`) | Ships, fuel, jump points. |
| **Nav sequence / pathfinding** (`NavSequenceProcessor`, `MoveStateProcessor`) | Fleets, orders, movement. |

## Space combat

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Beam weapons** (`BeamWeaponProcessor`) | Fire control, Damage, sensors (targeting), ships, energy. |
| **Missiles** (`MissileImpactProcessor` + `MissleProcessor`) | Damage, movement (guidance/fuel), ordnance, sensors. |
| **Generic firing / fire control** (`GenericFiringWeaponsProcessor`) | Weapons, sensors, orders, auto-resolve combat. |
| **Damage** (`DamageProcessor`, DamageComplex) | Weapons, ships, components, colony bombardment → population. |
| **Auto-resolve combat engine** (`BattleTriggerProcessor` → `CombatEngagement`) | Ships (combat value + evasion + weapon profiles), fleets (the sides — any number per side), doctrine (player's lever), orders (engagement lock). Rides the shared `CombatKernel` (same salvo resolver as ground). |
| **Ground combat** (`GroundForcesProcessor`) | Colony (`PlanetEntity` holds the roster), Galaxy/`PlanetRegionsDB` (regions + `SurfaceGrid` hex cylinder), Damage (orbital bombardment softens a garrison), industry (units are `ComponentDesign`+`GroundUnitAtb` — built on the installation-construction line), research (gates designs). |

## Sensors, survey, and the rest of the watch-bill

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Sensors** (`SensorScan`, `SensorReflectionProcessor`) | Orbits, contacts, IFF, combat targeting, fog-of-war. |
| **Research / tech** (`ResearchProcessor`) | Factions, industry (unlocks the designs you can build), components, people (scientists). |
| **Energy / power** (`EnergyGenProcessor`, `EnergyGenHotloopProcessor`) | Ships, components, sensors (power draw), reactors, fuel. |
| **Logistics** (auto cargo routes) (`LogiBaseProcessor`, `LogiShipProcessor`) | Storage, fleets, trade, colonies. |
| **Fleets** (`FleetOrderProcessor`) | Ships, movement, orders, combat. Client fleet changes go via `FleetOrder.*` + `OrderHandler`; `FleetDB` mutators are engine-internal. **NPC AI reaches in through `Fleets/FleetAssembly`** (engine-internal tree mutators — `RemoveChild`/`SetParent`/`AddChild`/`Transfer`) to fold built armed hulls into fleets + spill overflow into a reserve; a fleet can carry a `FleetCompositionDB` (its template + aspiration tier). |
| **Ships & launch** (`LaunchComplexProcessor`, `ShipFactory`) | Industry (builds them), fuel (launch cost via rocket eqn), orbits, fleets. |
| **People / commanders** (`AdminSpaceProcessor`, `NavalAcademyProcessor`) | Colony (admin radius), ships (captains), research (scientists). |
| **Survey** (geo & jump-point) (`GeoSurveyProcessor`, `JPSurveyProcessor`) | Galaxy (reveals minerals), jump points, movement. |
| **Factions & NPC AI** (`NPCDecisionProcessor`) | Economy (could auto-queue jobs), industry, diplomacy, combat doctrine, People/seats, sensors (enemy-strength read), exploration-content, **Fleets (fleet assembly — see next row)**, **ground garrison** (`FactionInfoDB.GarrisonComposition` → `GroundStartGarrison`). |
| ↳ **Fleet assembly / composition** (`Fleets/FleetAssembly` + `FleetCompositionDB`, driven by `NPCDecisionProcessor.RunFleetAssemblyPolicy`) | **Fleets** — sweeps built armed hulls out of the faction ROOT `FleetDB` (where NPC-built ships park, invisible to `FactionState.OwnedFleets()`) into in-system fleets so the AI can see + use them (fixes "builds but never attacks"). Reads `FactionRollup.Balance` + `NeedsLadder.WarStanding` for the aspiration tier (Deployable→Ideal→Perfect, war bumps one) → `FleetCompositionDB`. **Combat** (`ConquerResolver`: `HasHomeReserve`/`ShouldStopMassing` keep a home-defence reserve — the military-commander's job — so the AI never commits its whole navy; `IsWarship` decides which built hulls fold in, so a new armed hull is AI-usable with zero code). Per-faction numbers read off `FactionInfoDB.Fleet*`/`FleetTemplateName` (the scenario `fleetComposition` JSON node). |
| ↳ *AI build prereqs* | Seat durability (`People/AdminSpaceProcessor.cs:36-46`). Fleet situational read + crisis seed sockets are now filled (the assembly row above is the fleet read). |
| **Galaxy / system generation** (`StarSystemFactory`, `AtmosphereProcessor`) | Minerals, colony siting, orbits, sensors. |
| **Diplomacy** (design only) | Factions, IFF, trade, logistics. |
| **UI client** (ImGui/SDL) | Displays every system; live-test only. |

---

**Anti-pivot rule:** when you start a system, read its row here and every row it points at — that's the
minimum blast radius. Then look one hop further (the Prime Directive's four questions). Work the connected
systems too; don't change a system in isolation.
