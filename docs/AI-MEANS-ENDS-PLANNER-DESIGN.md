# AI Means-Ends Planner — Design & Scope Map

**"I know what I have · I know where I want to be · HOW DO I GET THERE?"**

**What this is:** the design + scope map for the NPC brain's missing middle layer — the **planner** that turns a settled objective (`StrategicObjectiveDB`, built) into a *reachable* sequence of orders, walking a goal backward through its prerequisites until it hits something the faction can do this cycle. Built from a **four-agent read-only survey** (2026-07-11) that traced every objective's prerequisite chain against current-branch source, file:line. This doc is the rationale; `docs/AI-BRAIN-BUILD-TRACKER.md` row 2.8 is the build authority.

> **As of 2026-07-11** · branch `claude/sol-playtest-earth-map-8r59j6`. Survey scope: `Pulsar4X/GameEngine/` (Industry, Colonies, Galaxy, GeoSurveys, Movement, Combat, Fleets, Ships, Logistics, Factions).

---

## The headline — it is BOUNDED, not open-ended

The Organism brain already **PERCEIVES** ("what I have" — `NeedsLadder` over the faction gauges) and picks a **GOAL** ("where I want to be" — `ObjectiveSelector`), then fires one primitive order. The missing middle is **planning**. The fear was that planning is an unbounded new subsystem touching everything. The survey says otherwise: **the engine already resolves the top half of every prerequisite chain, and every rung the AI must drive already has a player-equivalent order.** The planner is a *reconciliation layer at named seams*, not a general graph solver.

**The one architectural fact that bounds the whole thing:** `IndustryTools.AutoAddSubJobs` (`Industry/IndustryTools.cs:261-304`) is *already a recursive backward-chainer*. Queue a build and the engine auto-queues the refined materials it needs, and the sub-materials those need, to arbitrary depth — **for free.** It stops at exactly one place: the **mineral floor** (`IndustryTools.cs:292` — a raw mineral isn't an `IConstructableDesign`, so the recursion silently drops it). So the planner never has to reason about "to build a laser you need steel you need iron"; the engine does. The planner owns **what happens below the mineral line** (mine it / survey it / ship it) and **the gating the engine checks only at execution** (money / crew / tech / capacity).

---

## The core verdict — DELEGATE vs BUILD

**Roughly the top half of every chain is already done. The planner owns the bottom half + the feasibility gates.**

### Delegate to existing machinery (do NOT rebuild)
| Capability | Lean on | File:line |
|---|---|---|
| component → refined-material → material resolution + auto-queue | `IndustryTools.AutoAddSubJobs` (recursion-safe, arbitrary depth) | `Industry/IndustryTools.cs:261` |
| the dependency DATA (what any buildable consumes) | uniform `IConstructableDesign.ResourceCosts` (one id→qty map for components, materials, ships) | `Engine/Interfaces/IConstructableDesign.cs:18` |
| refining recipes (which minerals a material needs) | data-driven JSON = the material's own `ResourceCosts` | `GameData/basemod/TemplateFiles/materials.json` |
| "is this available to me now vs tech-locked" | `CargoGoods` / `LockedCargoGoods` split | `Factions/FactionDataStore.cs` |
| move materials between colonies | set `LogiBaseDB.DesiredLevels`, the logistics market hauls it | `Logistics/LogisticsProcessor.cs` (`LogiBaseDB.cs:17`) |
| **the goal-selection front end** | `NeedsLadder` → `ObjectiveSelector` → `StrategicObjectiveDB` (LIVE) | `Factions/NPCDecisionProcessor.cs:150` |
| every DRIVE rung | a player-equivalent order/API already exists for each (see per-objective maps) | — |

### Must build new (no engine support)
| Gap | Why it's net-new |
|---|---|
| **below the mineral floor** — "need mineral M" → point mining / colonize a deposit / re-target extraction | `AutoAddSubJobs` drops mineral shortfalls silently; mining is a continuous processor with no "mine THIS" decision layer |
| **plan-time feasibility oracle** — money / crew / tech / production-capacity checks | those gates fire only at *execution* inside `ConstructStuff` and fail SILENTLY (`MissingResources`), so an un-checked plan queues jobs that stall unseen |
| **reachability read** (military) — "can this fleet get to that body with its fuel/charge/route?" | grep confirms NO `CanReach`/`HasFuelToReach`/`HasDeltaVTo` anywhere; the single biggest military gap |
| **multi-jump auto-router** — string warp→jump→warp into one autonomous mission | pathfinding returns a route + cost; nothing turns it into an executable `NavSequence` for an AI |
| **fuel-readiness gate** (military) — production-built ships come out EMPTY-tanked + zero-charged | `ShipFactory.FillFuelTanks`/`ChargeReactors` are wired only to DevTools/sandbox spawns, never to `OnConstructionComplete` — an NPC fleet silently won't warp |
| **target selection** — which rival to Conquer, which body to Expand to | `StrategicObjectiveDB.TargetFactionId` is hardcoded -1; no candidate enumerator/scorer |
| **reverse indexes** — "what tech unlocks X", "what can I make from iron" | only forward maps exist (`Tech.Unlocks` is level→grants); invert them yourself |

**The biggest single trap:** conflating "`AutoAddSubJobs` resolves the tree" with "the planner can delegate prerequisite resolution." It delegates the *buildable* sub-tree ONLY. The planner's core value-add is the transition **material-demand → raw-resource acquisition** and **plan-time feasibility** — neither of which any existing machinery provides, and both of which fail *silently* today.

---

## Per-objective scope map (the objectives are NOT uniform in cost)

### 🟢 GrowEconomy — SMALL (engine does the top half)
The full supply chain EXISTS and is AI-drivable rung by rung. What the planner adds is the reconciliation below the mineral line.

- **Build** — `IndustryTools.AddJob` / `IndustryOrder2.CreateNewJobOrder` (AI already drives this, `NPCDecisionProcessor.TryQueueEconomyJob:120`). *Cheap early win: route it through the `AutoAddSubJobs`-wrapped order — today it bypasses the free resolver.*
- **Refine** — a `ProcessedMaterial` is an `IndustryJob`; auto-resolved by `AutoAddSubJobs`.
- **Mineral shortfall** — read `IndustryJob.Status == MissingResources` (`Enums.cs:175`) + `CargoStorageDB.GetUnitsStored` + `MineralsDB.Minerals` (fog-gated `Masked<long>`). **← the seam the planner owns.**
- **Mine** — no "start mining" order; mining is emergent from installed Mine components → queue a Mine *installation* as a build (Rung 1). Read rate: `MiningDB.ActualMiningRate`.
- **Survey** (reveal the deposit) — `GeoSurveyOrder.CreateCommand` (`GeoSurveys/GeoSurveyOrder.cs:84`); read `GeoSurveyableDB.IsSurveyComplete(factionId)`.
- **Logistics** — set `LogiBaseDB.DesiredLevels` via `SetLogisticsOrder`; market hauls it.
- **Net-new:** the "mineral-line reconciliation brain" — detect stuck job → is it a mineral shortfall → is the deposit surveyed & accessible → queue mine / survey / logistics. ~3-4 slices + the AutoAddSubJobs fix.

### 🟡 Expand — MEDIUM (founding is trivial; selection is the work)
The pleasant surprise: **founding a colony is a direct, instant action — no colony ship, no landing.**

- **Found** — `CreateColonyOrder.CreateCommand(faction, species, body)` → `ColonyFactory.CreateColony` in one call (`Colonies/ColonyFactory.cs:188`). AI-drivable, instant.
- **Habitability read** — `SpeciesDB.ColonyCost(planet)` (`People/SpeciesDBExtensions.cs:30`): `-1` unsurvivable, `0` native, `>0` hostile-cost; `CanSurviveGravityOn` = hard gate.
- **Candidate enumerate** — iterate `GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()` over `FactionInfoDB.KnownSystems`; skip `IsOrHasColony()`; filter `ColonizeableDB` / `SupportsPopulations`.
- **Survey + move** — `GeoSurveyOrder` + `MoveToSystemBodyOrder` (both exist, AI-drivable).
- **Net-new:** (a) a **habitability/worth-settling SCORER** on top of `ColonyCost` (nothing ranks candidates); (b) the 3-order **chain** survey→move→found (nobody composes it); (c) an **engine-side habitability gate** (`CreateColonyOrder.IsValidCommand` returns `true` unconditionally — the only gate is client UI). Template to copy: `GameStageFactory.NextSpareBody` (dev rig — add `KnownSystems` + habitability filters). ~3-4 slices.

### 🔴 Conquer / Defend — LARGE (a genuine subsystem)
All six rungs exist and are AI-drivable, but it carries the most missing connective tissue.

- **Design/Build/Fleet/Commit** — all exist: `ShipFactory`, `IndustryTools.AddJob`, `FleetOrder.CreateFleetOrder`/`AssignShip`, `CombatEngagement.OrderAttack`/`OrderAttackNearestHostile` (`Combat/CombatEngagement.cs:492/520`), plus the auto-trigger (`BattleTriggerProcessor`) that fights hostiles in range with no order at all.
- **Reads that exist** — own strength `FactionRollup.MilitaryStrength`; rival fog-limited `ThreatAssessment.DetectedStrengthOf`; intel-sharpened `IntelAssessment.EstimatedMilitaryStrength`; rival positions via `FactionInfoDB.SensorContacts`.
- **Net-new (the mires):** ① **fuel/charge readiness** — production ships come out empty (`ShipFactory.cs:150`); must call `FillFuelTanks`+`ChargeReactors` (or wait & gauge charge) — `CombatSandbox.SpawnHostileFleet` is the working recipe to copy. ② **reachability read** — MISSING entirely. ③ **multi-jump auto-router** — MISSING (`PathfindingManager.GetPath` gives the route; nothing executes it as a mission). ④ **target selection** — `TargetFactionId` is -1. ⑤ **fleet composition** — nothing picks warship designs/counts. ⑥ **cross-system attack targeting** — `OrderAttackNearestHostile` scans only the fleet's own system. ~5-7 slices, and reachability+router may be their own mini-project (reusable foundations, like "the eyes" were).

---

## The architecture — small resolvers on a shared substrate, driven by the loop we already built

**Not one universal graph solver. A set of per-objective backward-chaining resolvers sharing one substrate**, riding the monthly-hysteresis Tick that already exists:

1. **State reads (what I have)** — all exist (`FactionRollup`, `CargoStorageDB`, `MiningDB`, `MineralsDB`, `ThreatAssessment`, `ColonyCost`, `IndustryJob.Status`). A thin **`FactionState` snapshot** helper gathers them once per cycle.
2. **A per-objective resolver (how do I get there)** — for the settled objective, find the **nearest unmet prerequisite** and return the ONE order that advances it. GrowEconomy resolves down the supply chain; Expand resolves survey→move→found; Conquer resolves build→fuel→move→attack.
3. **A shared feasibility oracle (can I actually)** — the plan-time money/crew/tech/capacity checks the engine only does at execution. Build once; every resolver consults it so plans don't queue silently-stalling jobs.
4. **The incremental engine is already built.** The monthly re-plan-with-hysteresis loop (`ObjectiveTransition`, 2.3) means the resolver takes **one step toward the goal per cycle** — queue the mine now, the refinery once ore flows, the build after — i.e. least-commitment planning that needs no big up-front tree solve. **This is why the planner is tractable: it never has to compute the whole plan at once.**
5. **Visibility (the Gate).** Because every failure mode here is SILENT (mineral-floor drop, execution-time stalls), build a **plan/queue readout** alongside the planner — per root `CLAUDE.md`'s Visibility Gate, a stalled-job plan is otherwise un-observable.

Plug point: the resolvers slot into `NPCDecisionProcessor.EmitOrders` (`Factions/NPCDecisionProcessor.cs:99`) — the switch whose arms are empty stubs today. Gated behind `EnableOrderEmission` (default off) so it stays byte-identical until opted in.

---

## The named mires (what will bog us down — now known, not discovered mid-build)

1. **The mineral floor is a SILENT drop.** `AutoAddSubJobs` looks complete but stops one layer above raw resources with no signal (`IndustryTools.cs:292`). Trusting it to "resolve everything" yields plans that omit mining and stall. **The #1 trap.**
2. **Feasibility gates are execution-time and silent.** Crew/infra/zero-cost/no-line failures all fire quietly in `ConstructStuff`. No plan-time oracle exists — build one or accept stalls.
3. **Reachability & routing are absent** (military). No `CanReach`; no multi-jump mission executor. The genuine subsystem inside Conquer.
4. **The fuel/charge trap.** Production-built ships are empty-tanked + zero-charged on purpose; warp needs stored energy. An NPC fleet silently won't move. Copy `CombatSandbox`'s fuel+charge recipe.
5. **Selection & scoring don't exist.** No target picker (Conquer), no candidate/habitability scorer (Expand), no "what to mine" prioritizer (GrowEconomy). The AI's judgment layer is net-new across all three.
6. **`ColonizeableDB`/`GeoSurveyableDB` are JSON-data markers, not computed** — procedural bodies may carry neither; verify marker coverage before an AI filters on them. Two overlapping "colonizeable" signals (`ColonizeableDB` vs `SupportsPopulations`).
7. **Dead code to avoid:** `InstallationsDB` (never attached) — read colony industry via `ComponentInstancesDB` + `MiningDB`, per root landmine L1.

---

## Build sequence & honest slice count

Sequenced cheapest-highest-value first; each byte-identical behind `EnableOrderEmission`, each with a gauge. **~13-18 slices total**, but the first cluster delivers most of the "feels alive" value.

- **P-0 Shared substrate** (~2-3): the `FactionState` snapshot reads + the feasibility oracle skeleton + route `TryQueueEconomyJob` through `AutoAddSubJobs` (the cheap fix). 
- **P-1 GrowEconomy reconciliation** (~3-4): MissingResources sensor → mineral-shortfall → queue mine / survey / logistics. *This alone makes the existing GrowEconomy honest (fixes the 2.4c blind-build shallowness).* 
- **P-2 Expand** (~3-4): candidate enumerate + habitability scorer + the survey→move→found chain + the engine-side habitability gate.
- **P-3 Conquer/Defend foundations** (~5-7): fuel-readiness gate + fleet composition + target selection + **the reachability read + multi-jump router** (the reusable foundations — may be their own sub-phase). Then the emit.
- **Cross-cutting:** the plan/queue **visibility readout** (build alongside P-1, extend per phase).

**The honest bottom line for the developer:** yes, the planner is more than a byte-identical wire — it's real work. But it is **bounded (~15 slices, not "50 runs of discovery"), it invents no new orders (every rung already has a player-equivalent lever), it rides the incremental loop we already built, and its mires are now named.** And it sequences so the cheap, high-value economy planner lands first and the expensive military routing is a clearly-delineated later subsystem you can choose to fund or defer — exactly like the gate-network was deferred in Movement I.

---

## Connections (Prime Directive)

- **Feeds IN:** the whole Organism decision loop (`StrategicObjectiveDB` from `NeedsLadder`/`ObjectiveSelector`); the state reads above; the engine's dependency graph (`AutoAddSubJobs`/`ResourceCosts`).
- **Feeds OUT:** player-equivalent orders (`IndustryOrder2`, `GeoSurveyOrder`, `MoveToSystemBodyOrder`, `CreateColonyOrder`, `FleetOrder`, `CombatEngagement.OrderAttack`) → the same processors that execute player orders.
- **Shares STATE:** every colony/faction gauge it reads is also written by the economy/combat/diplomacy processors — read-only from the planner's side.
- **Cradle-to-grave:** this is the AI-side mirror of the repo's own cradle-to-grave law — the NPC learns to reason along the same mineral→material→component→decision chain the design already enforces on the player.
