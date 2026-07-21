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

## Operation Earthfall — invasion & campaign wiring (landed 2026-07-21)

The connections the campaign built or revealed. The one-shot orbital bombardment (space→ground garrison soften) was
already mapped in the Ground-combat row above and is unchanged (no bombard **re-fire** wire — that cadence was tabled).

### The ground campaign — beachhead, brain, sustainment, per-hex infra

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Beachhead haul + on-site build** (`GroundParts` / `GroundBeachhead`) | **Construction** (`GroundParts.LandPartsFromShip` reuses `Construction.ConstructionCargo` pooled-hold helper), **Storage** (crated parts are `ComponentDesign` cargo drawn from a ship's `CargoStorageDB`), **GroundConstructorAtb** (the combat-engineer component, read at build time), `GroundForcesDB.SurfaceParts` (the per-region crate pool) → `GroundBeachhead.TickBuilds` erects a footprint building into a per-faction **beachhead OUTPOST** (a bare `ComponentInstancesDB` host, no colony) → **GroundBuildings.BodyComponentStores** (colonies + outposts, the single source of truth the fortification/bombard/readout code all walk) → **GroundFortification** (a beachhead bunker fortifies) + **GroundBuildings.BombardHex** (the grave rung — it can be razed). `GroundBeachhead.HasBeachhead` = the G2 resupply-depot read. |
| **The ground TACTICAL BRAIN** (`GroundTactics.DecidePosture` / `GroundThreat` / `GroundTacticalBrain`, behind `EnableGroundTacticalAI`) | reads **`GroundThreat`** (the fog-honest detected-enemy strength) ← **`PlanetRegionsDB`** per-faction reveal (undetected enemy counts zero); reads **`Factions.CombatRisk` + `PersonalityDB`** (read-only — the SAME odds curve the fleets use, so the UMF is recognizably the UMF on the ground); → **`GroundFormationDoctrine`** (Stance via `TrySetStance`, ROE via `SetEngagementStance`) + the **order queue** (an `GroundOrderIssuer.Ai`-marked `MoveRegion` — a battalion holding any Player order is left alone) + **`GroundAssembly.FormUpLoose`** (forms loose NPC units first). Records `TacticalReason`/`TacticalIntent` on the formation (the AI-tape the client shows). `GroundThreat.DetectedDefenderStrength` = one read, two consumers (the brain + a landing score). |
| **Sustainment loop** (`GroundForcesProcessor` combat + tick) | combat → **`GroundAmmo`** (a firing magazine unit burns `AmmoPerSalvo_kg`; a dry unit is silenced); tick step 0d → **`GroundForces.ResupplyUnit`** (auto-rearm at a depot = a friendly-held region with installations, incl. a G1 beachhead bunker); **`GroundUnitAssembly`/`GroundStartGarrison`** → `GroundUnit.UpkeepCredits` → **`GroundUpkeep`** bill → the faction **Money ledger** (an army now costs money as it stands). |
| **Per-hex infrastructure combat** (`GroundForcesProcessor.ProcessFormationOrders`) | order queue → **`GroundBuildings.BombardHex`** (DestroyInfrastructure — staged raze, range-gated by the resolver's own rule) / **`GroundHex.OwnerFactionID`** (CaptureInfrastructure — instant hex flip); **`GroundHex.OwnerFactionID`** → **`GroundFortification.SumLocal`** (a captured bunker's hex stops fortifying the defender — hex ownership is no longer inert). |
| **Sealed / environmental component** (`GroundSealAtb`) | `sealed-systems` component → **`GroundUnitAssembly`** (best-seal read) → `GroundUnitDesign.EnvironmentalResistance` → `GroundUnit.EnvResistance` → **`GroundForcesProcessor` E4 attrition** (a sealed unit survives an airless/toxic world an unsealed twin dies on; the grave rung = shoot the seal off). |

### The strategic AI — objective continuity, the invasion resolver, the sealift

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Government → legitimacy → objective** (the A3 fix chain) | **`FactionFactory.LoadFromJson`** (`"government"` node) → **`GovernmentDB`** → **`LegitimacyProcessor.WarTermFor`** (`WarMoraleFactor × MaxWarSwing`) → `LegitimacyDB` war term → **`RebellionDB`/`NeedsLadder`/`ObjectiveSelector`** (militarist UMF reads +10 pride, not −5 collapse). **`PopulationProcessor.ComputeCurrentMorale`** → `LegitimacyProcessor.RecalcLegitimacy` (fresh morale, kills the stale echo). **`LegitimacyProcessor.UpdateRebellion`** (debounce) → `LegitimacyDB.ConsecutiveCollapsingReads` → `RebellionDB.IsRebelling` (a rebellion needs N consecutive collapsing reads — no one-sample revolt). |
| **Operation continuity** (`ObjectiveTransition` + resolvers) | **`NeedsLadder.HomelandInvaded`** (a foreign ground unit on a home world) + **`ConquerResolver.HasFleetInTransit`** (an owned fleet in warp) + the P3.3 **`CrisisTrigger`** → **`ObjectiveTransition.ShouldProtectInFlightConquest`** → `Advance`/`ShouldReplan` (`protectCommit`): a winning in-flight Conquer HOLDS through a transient internal wobble. **`DefendResolver` Rung 0 (RECALL)** → **`Movement.MoveToSystemBodyOrder`** → the fleet's `OrderableDB` (a genuine Defend recalls in-flight offensive fleets home). |
| **The sealift** (`ConquerResolver` ⟷ industry ⟷ launch) | `ConquerResolver` Rung 2 → **`FactionHasTransportQueued`** → `ProductionLines.Jobs` (build ONE transport, not one-per-line). **`ShipDesign.OnConstructionComplete`** + **`LaunchComplexProcessor.TryLaunchShip`** → **`ShipDesign.ProvisionBuiltShip`** → `ShipFactory.ChargeReactors`+`FillFuelTanks` → `MilitaryReach.FleetHasWarpRange` (a home-BUILT hull boots charged+fuelled and can warp; NPC-on / player-on per dev decision #3). `LaunchComplexProcessor.TryLaunchShip` → `TryDeductFuel` → `CargoStorageDB.TypeStores["fuel-storage"]` (a launch-complex colony MUST stock launch fuel — the data invariant Mars now satisfies). End-to-end: `ConquerResolver` (Rung 2 Build → 1.5 Load → 1.3 Sail) ⟷ `IndustryProcessor` ⟷ `LaunchComplexProcessor` ⟷ `ProvisionBuiltShip` ⟷ `GroundTransport.TryLoadUnit`. |
| **CONQUER resolver → GROUND surface** (PW, the strategy→tactics seam) | **`ConquerResolver`** → `GroundAssembly.FormUpLoose` (form the landed unit into a battalion) · `GroundParts.LandPartsFromShip` + `GroundBeachhead.TickBuilds` (land crated parts on held ground → the combat-engineer FOB build) · `GroundForces.QueueFormationOrder`/`GroundOrder.DestroyInfra` (task an Offensive battalion to raze an enemy building) · reads `GroundTactics.Offensive` (`formation.StanceFamily` = the STANCE-AS-GATE) + `GroundFormationTools.FormationsFor`. |
| **Concurrency** (`SafeDictionary` ⟷ `EntityManager`) | entity/blob queries read a lock-taken `ValuesSnapshot`/`KeysSnapshot` (not the live `.Values`); events fire copy-then-notify (after releasing the lock) — closes the H3 race where a concurrent `Add`/`Remove` on another sim thread invalidated a live enumeration. |

### Faction development (DEV) + client front doors + the 2D resolver

| System | Connects to (feeds / reads / shares / triggers) |
|--------|--------------------------------------------------|
| **Kithrin survey chain** (`ExpandResolver`) | → **`GeoSurveyOrder`/`GeoSurveyProcessor`** (the AI now drives the geo-survey — the survey leg was `Execute=null`) + **`IndustryTools.AddJob`** (build-a-surveyor fallback: geo-surveyor component → `GeoSurveyAtb` → `GeoSurveyAbilityDB`); `kithrin.json` fields a `kithrin-ship-sable` surveyor. Full arc: `ExpandResolver` (Survey/Found) → `GeoSurveyProcessor` → `GeoSurveyableDB.IsSurveyComplete` → `CreateColonyOrder` → `ColonyFactory.CreateColony` → `FactionInfoDB.Colonies` (+ a `ColonyEconomyDB`). |
| **Station income** (`StationUpkeepProcessor`) | → **`FactionInfoDB.Money`** INCOME (was expense-only): `CollectIncome` books a populated station's tax via the SHARED **`ColonyEconomyDB.MonthlyTaxIncome`** model (× `ColonyMoraleDB` multiplier, capped by `GovernmentDB.TaxCeiling`), under its own **`TransactionCategory.StationIncome`** ledger category. **`ConsolidateResolver`** → **`GrowEconomyResolver`** (station-legal fall-through — a station-only faction is no longer frozen in a crisis). |
| **Client invasion front doors** | **`FleetWindow`** → `LoadTroopsOrder`/`LandTroopsOrder`/`GroundTransport` (embark/land troops) + `GroundForcesDB`/`GroundFormationTools`/`GroundFormationDoctrine` (the Battalions tab) + `PlanetViewWindow` (jump-to-world). **`PlanetViewWindow`** → `GroundSensorAtb`/`GroundRangeTools`/`GroundUnitEntity` backing store (the weapon/radar range overlay). **`FleetWindow` + `PlanetViewWindow`** → `GroundForces.RenameFormation` / `GroundOrder.DestroyInfra`·`CaptureInfra` / `GroundForces.QueueFormationOrder` (rename + infra raze/seize buttons). All UI front doors onto already-connected engine order paths, not new engine couplings. |
| **2D group-plane resolver** (`GroupPlane`, behind `EnableGroupPlane`) | `CombatEngagement.StartEngagement`/`EnsureInCombat` seed a frozen 2D `BattleFrame` + per-fleet `Anchor` on **`FleetCombatStateDB`**; `AdvanceClosing` slides the anchor along `GroupPlane.EnemyDirection`; `SeparationOf`/`WithinWeaponRange` read the 2D anchor pair-distance (S2). Default-off → the scalar `Separation_m` path is authoritative and byte-identical. `RESOLVER-2D-JOINTS.md` pins the fire-allocation + combined-theater joints that gate the later S5/S6. |
| **Operation Earthfall acceptance (P8.1)** — the capstone | verifies the cross-slice STACK connects as one narrative: P3 continuity (`ObjectiveTransition`) × P4 sealift (`ConquerResolver`→`MilitaryReach`) × the bombard→land→FormUp→`GroundTactics`→beachhead→infra→capture ground chain × D1-D3 Kithrin expand, plus the player's own `RegisterAssembledDesign`→industry→FormUp→transport-order rails, plus save/load of the whole mid-invasion state. The gauge that keeps every one of those connections green as the engine changes. |

---

**Anti-pivot rule:** when you start a system, read its row here and every row it points at — that's the
minimum blast radius. Then look one hop further (the Prime Directive's four questions). Work the connected
systems too; don't change a system in isolation.
