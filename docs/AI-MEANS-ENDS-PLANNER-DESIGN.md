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

## Build Spec — the deep dive (2026-07-11, four read-only agents)

A second survey (build-spec · wiring/coexistence · testing/visibility · feasibility-oracle+mineral-floor) turned the scope map above into a **build-ready spec against a MOVING baseline** (the reactive Movement-II slices are landing in parallel). Everything below is file:line-verified on this branch.

### A. The five new classes (all `Pulsar4X.Factions`, pure-helper + `internal`-for-gauge convention)

| File (`GameEngine/Factions/`) | Contents | Role |
|---|---|---|
| `FactionState.cs` | `FactionState` (the gather-once "what I have" snapshot) + `ColonyState` + `MineralShortfall` | reads every gauge ONCE per cycle so a resolver reads memory, not the entity graph. NOT a DataBlob (per-Tick scratch). Null-safe; `Snapshot(faction)` returns null on a blob-less/manager-less faction. |
| `PlannerAction.cs` | `PlannerAction { string Kind; string Detail; Action Execute }` + `.None` | the ONE step a resolver picks. The `Execute` closure unifies the two ways rungs reach the sim (`EntityCommand`→`OrderHandler.HandleOrder` for survey/move/found vs. the **direct** `IndustryTools.AddJob` for builds — a single return type can't express both). `Detail` feeds the visibility readout. |
| `IObjectiveResolver.cs` | `IObjectiveResolver { StrategicObjective Handles; PlannerAction Resolve(state, objective); }` + `ObjectiveResolvers` static registry | one small backward-chainer per objective; the registry (mirrors `ExchangeCatalog`) maps objective→resolver. |
| `GrowEconomyResolver.cs` | `GrowEconomyResolver` + `FindMineDesignFor` | the concrete P-1 resolver (algorithm below). |
| `FeasibilityOracle.cs` | `FeasibilityOracle.CanActuallyBuild(colony, design, factionInfo, out blockedReason)` | the plan-time "will this silently stall?" predicate (spec below). |

**`FactionState` read map** (the "what I have"): `Balance`/`MilitaryStrength`/`MeanMorale`/`MeanLegitimacy` ← `FactionRollup`; per-colony `Industry`(`IndustryAbilityDB`)/`Cargo`(`CargoStorageDB`)/`Mining`(`MiningDB.ActualMiningRate`)/`PlanetMinerals`(`ColonyInfoDB.PlanetEntity`→`MineralsDB.Minerals`); `StalledJobs()` = `ProductionLines[*].Jobs` where `Status==MissingResources` (`Enums.cs:175`); `MineralShortfalls()` = a stalled job's `ResourcesRequiredRemaining` (`JobBase.cs:29`) ids **absent from `IndustryDesigns`** (that absence-test IS the mineral-floor detector); `DetectedRivalStrength` ← `ThreatAssessment.DetectedStrengthOf` per rival.

### B. The `EmitOrders` rewrite — REPLACE, not wrap; ONE flag

The decision half (`UpdateStrategicObjective`) is untouched. The act half becomes snapshot → registry → resolve → execute, still behind the single `EnableOrderEmission` gate:

```
internal static void EmitOrders(factionEntity, factionInfo):
    if no StrategicObjectiveDB: return
    if !ObjectiveResolvers.TryGet(objective.Objective, out resolver): return   // objectives w/o a resolver no-op
    state = FactionState.Snapshot(factionEntity);  if null: return
    action = resolver.Resolve(state, objective)     // PURE decision
    action.Execute?.Invoke()                        // the ONLY side effect
    // (a later slice records action.Detail into the plan readout — the Visibility Gate)
```

- **REPLACE the arm body per-objective, don't wrap.** A blind reactive fallback firing when the resolver says "nothing doable" re-introduces the exact silent-stall bug the planner fixes. The resolver is a strict superset of the reactive emitter (its "inputs ready → build" branch IS `TryQueueEconomyJob`, upgraded to route through `AutoAddSubJobs`), so there's no residual behaviour to fall back to. `TryQueueEconomyJob` is **superseded** by `GrowEconomyResolver`.
- **Keep ONE gate (`EnableOrderEmission`).** A second `EnablePlanner` flag makes a 2×2 dead-state matrix. Byte-identical-off holds automatically through the whole migration (flag off → `EmitOrders` never runs → arm shape irrelevant), so each per-arm replacement is independently safe and CI-gated.

### C. `GrowEconomyResolver` — the backward-chain (nearest unmet prereq → ONE order)

```
Resolve(state, objective):
  A. no colony with Industry            → None
  B. for each STALLED job (MissingResources):
     B1. job has buildable sub-demands not yet queued (id in ResourcesRequiredRemaining ∈ IndustryDesigns)
                                          → QueueBuild: IndustryTools.AutoAddSubJobs(colony, job)   # the cheap fix
     B2. for each mineral shortfall (id ∉ IndustryDesigns, Missing = req − GetUnitsStored):
         → hand to the MINERAL-FLOOR BRIDGE (D): survey / mine / more-mines / logistics / escalate-to-Expand
  C. nothing stalled → start next growth build on a free line, ROUTED THROUGH AutoAddSubJobs
                       (gated by FeasibilityOracle.CanActuallyBuild)     → QueueBuild
  else                                    → None
```

One step per cycle, riding the 2.3 hysteresis loop — never computes the whole plan at once (least-commitment).

### D. The two hardest pieces (deep-spec'd, file:line-anchored)

**D1 — `FeasibilityOracle.CanActuallyBuild` mirrors `IndustryTools.ConstructStuff`'s EXECUTION gates, in order (mirror, NOT a superset — a stricter check makes the AI refuse builds a player could make):**
0. **Tech** — `factionInfo.IndustryDesigns.ContainsKey(design.UniqueID)` (ConstructStuff indexes it unconditionally at `IndustryTools.cs:125` → `KeyNotFoundException` if absent).
1. **Line exists** — a `ProductionLine.IndustryTypeRates.ContainsKey(design.IndustryTypeID)` (`IndustryAbilityDB.cs:16`).
2. **Non-zero cost** — `design.ResourceCosts.Values.Sum() > 0` (ConstructStuff throws "resources can't cost 0" at `:159`).
3. **Capacity ≥ 1 pt/tick** — `(int)(rate × InfrastructureProcessor.GetEfficiency(colony)) ≥ 1` (ConstructStuff skips a job under 1 at `:133`).
4. **Crew/talent — SHIP designs ONLY** (`design is ShipDesign s && s.CrewReq>0`, guard at `:141`): `ManpowerTools.ResolveBuild(colony, CrewReq−TalentReq).CanBuild && HasTalentToBuild(colony, TalentReq)`. Inert on a pool-less host (matches execution — must replicate the guard or it false-blocks stations/installations).
5. **Materials** — replicate `AutoAddSubJobs`'s classification per cost id: satisfied if in stock (`GetUnitsStored`), else if sub-buildable (`is IConstructableDesign` → recurse, terminates at the mineral floor), else → **mineral shortfall** (the reason string carries the id+qty, the hand-off to D2).

**D2 — the mineral-floor bridge (`ResolveMineralShortfall`) — the decision tree, in order:**
1. `missingId` **not a mineral** (`CargoGoods.IsMineral` false) → defer to `AutoAddSubJobs` (engine refines it).
2. mineral on home body (`ColonyInfoDB.PlanetEntity`→`MineralsDB`), **unsurveyed** (`Amount.For(mask)==null` / `!GeoSurveyableDB.IsSurveyComplete`) → **`GeoSurveyOrder.CreateCommand`**.
3. surveyed+accessible, **no mining capacity** (`MiningDB.ActualMiningRate[mid]` absent/0) → **build a Mine** (`IndustryTools.AddJob`), itself pre-checked by D1.
4. has capacity but rate too low → **build another Mine** (raise `NumberOfMines`).
5. **not on home body but stocked/mined at a sibling colony** → **set `LogiBaseDB.DesiredLevels`** (`SetLogisticsOrder.CreateCommand_SetBaseItems`); the freight market hauls it.
6. **nowhere reachable** → **escalate to Expand** (`CreateColonyOrder` — a new colony on a body that has it).

> **⚠ The single most error-prone spot (load-bearing in BOTH D1 and D2):** resource costs are **string-keyed** (`ResourceCosts`/`ResourcesRequiredRemaining` by UniqueID) but `MineralsDB.Minerals` and `MiningDB.*MiningRate` are **int-keyed** by `Mineral.ID` (a runtime `GetEntityID()`). Convert via `factionInfo.Data.CargoGoods.GetMineral(uniqueID).ID` (exactly as `MineResourcesProcessor.cs:128` does), and check **both** `CargoGoods` and `LockedCargoGoods` (a not-yet-unlocked mineral is only in the locked library).

### E. Testing + the Visibility readout (a DELIVERABLE, not an afterthought — every failure here is SILENT)

- **Pure gauges** (fast, `rest` shard): resolver returns the right rung for a hand-built state (unsurveyed→Survey, no-mine→QueueMine, sibling-stock→SetLogistics, flowing→None); oracle predicates at their boundaries; **+ a pinning cross-check** — the oracle's `MissingResources` prediction must agree with what `ConstructStuff` actually sets over an input sweep (guards against plan-time/execution drift, the `CombatKernelTests` technique).
- **Integration gauges**: reuse `CreateWithColony` + flip `IsNPC` (Path A, deterministic); create a real shortfall by draining the stockpile (`RemoveCargoByUnit`) / zeroing the deposit (`MineralsDB.Minerals[id].Amount`, internal-set) / marking it unsurveyed; call the resolver static directly (avoids hotloop-timing flake + the static-leak); assert the corrective order via the read APIs. **First triage line: assert `ActivityState != Stasis`.**
- **"Goal becomes reachable"**: assert **PROGRESS not completion** — the stuck job leaves `MissingResources` and its points burn down — and field the corrective mine deterministically via the `OnConstructionComplete`-direct workaround (dodges the known build-to-completion flakiness).
- **The Visibility readout** (Failure B — the number doesn't exist yet, so building it IS part of the planner): a small persisted record (a `PlanStateDB`, or three fields on `StrategicObjectiveDB`) carrying `BlockedOn` (structured: `Mineral(iron): deposit unsurveyed`) + `LastEmittedOrder`; exposed `SocietyReadout`-style (pure, missing-blob-tolerant string builder) with a `PlanReadoutTests` gauge and a client "Dump Plan" caller. Example line: `Directorate: obj GrowEconomy/Thrive | blocked-on iron [deposit unsurveyed] | emitted: survey Mars`.

### F. The interleave sequence + FIT VERDICT

**FIT = INTERLEAVE, per-objective, along the cost gradient — NOT a strictly-after phase, and do NOT build the reactive Conquer/Defend emitter at all.** The economy planner is the *honest continuation* of the reactive work, not a bolt-on: we fit it in by making 2.4c **correct** instead of building 2.4d shallow.

Refined slice order (each byte-identical behind `EnableOrderEmission`, one CI gate each):
- **P0-a** `FactionState` snapshot (no consumer) · gauge `FactionStateTests` (hand-sum).
- **P0-b** `PlannerAction`+`IObjectiveResolver`+registry+`FeasibilityOracle` skeleton (capacity+tech) + `GrowEconomyResolver` Rung C only (queue-on-free-line **through `AutoAddSubJobs`** — the cheap fix) + rewire `EmitOrders`.
- **P1-a** `StalledJobs()`+`MineralShortfalls()` sensor.
- **P1-b** Rung B2/D2 step 3 (queue a mine) · **P1-c** logistics (D2 step 5) · **P1-d** survey (D2 step 2).
- **P1-e** oracle teeth (crew/money/capacity — D1 checks 3-5).
- **Cross** the plan/queue visibility readout.
- **P-2 Expand** and **P-3 Conquer/Defend** register the same way in their own later phases (P-3's reachability/router/fuel-readiness is the one genuine deferrable sub-subsystem; share the 2.6 eyes-wire).

Decision-side **2.5/2.6/2.7 run in full parallel** — they modulate `ObjectiveSelector`/`UpdateStrategicObjective`, invisible to the emit-side planner (2.6's eyes-read is a shared companion to the future Conquer resolver — build once, use twice).

---

## Connections (Prime Directive)

- **Feeds IN:** the whole Organism decision loop (`StrategicObjectiveDB` from `NeedsLadder`/`ObjectiveSelector`); the state reads above; the engine's dependency graph (`AutoAddSubJobs`/`ResourceCosts`).
- **Feeds OUT:** player-equivalent orders (`IndustryOrder2`, `GeoSurveyOrder`, `MoveToSystemBodyOrder`, `CreateColonyOrder`, `FleetOrder`, `CombatEngagement.OrderAttack`) → the same processors that execute player orders.
- **Shares STATE:** every colony/faction gauge it reads is also written by the economy/combat/diplomacy processors — read-only from the planner's side.
- **Cradle-to-grave:** this is the AI-side mirror of the repo's own cradle-to-grave law — the NPC learns to reason along the same mineral→material→component→decision chain the design already enforces on the player.
