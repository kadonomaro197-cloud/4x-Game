# Pulsar4X — Systems Status & Test Plan (Living Map)

> ⚠ **STALE since 2026-06-29 (flagged 2026-07-02).** This map doesn't yet reflect the `claude/space-economy-morale` landings — morale/population (M1–M5), government-as-modulator, diplomacy substrate+teeth+drift, crew/manpower enforcement, legitimacy/rebellion, and stations are all built + CI-green but their rows here show earlier states. **For current status use `docs/TESTING-TRACKER.md` (tests) + `docs/DOCS-INDEX.md` (overview) until these rows are refreshed** (doc-debt #1 in DOCS-INDEX).

**What this is:** one place that lists *every* system in the game, what state it's in, whether we can
*see* it (a gauge/test), and what it's *wired to*. It exists so we stop pivoting from one shiny broken
thing to the next. Pick the next job off this map, not off whatever we happened to trip over.

**The anti-pivot rule (read before touching anything):**
> You do not work a system alone. Find its row, read its **Connected to** column, and read *those* rows too.
> A change to one station ripples down the line. This is the Prime Directive in table form.

**Scope authority:** what to *build* (vs. just gauge) is bounded by **`docs/MVP.md`** — the v1 finish line is
"you can take a planet." This map tells you the state of every system and what it connects to; the MVP doc
tells you which of them are on the critical path to v1 and which are deferred. Use them together: pick the
next MVP stage, then use this map to work it *and* its connected systems.

**Last updated:** 2026-07-06 — **WEAPON UNIFICATION + the planetary-unit designer wired to the battlefield** (branch `claude/4x-hex-movement-pathfinding-o7qeaa`, CLOSED + CI-green). The ONE shared weapon designer now offers its weapons AND reactors AND magazines on a planetary chassis, gated by a **power SUPPLY gate** (guns can't out-draw reactors — the two gates compose, so "infantry can't power the big laser" falls out of the carry gate) and an **AMMO gate** (an ammo weapon needs a magazine). Planetary units are now **assembled (frame+parts) → built through industry → FIELDED on the planet** (slice A), and carry a **mass-based ammo pool** with **manual resupply** on friendly ground (slice B). **DECISION LOCKED:** there is ONE combat resolver — the next branch MERGES the duplicated ground damage-math onto a shared kernel both ships and planetary units call (absolute metric range; planetary = terrain + air modifiers; the Armor/Infantry/Artillery type-triangle dissolves into weapon×armour matchups). Full narrative + the 3 reds-and-fixes + lessons in `SESSION_STATE.md` ⏩ 2026-07-06; merge plan in `docs/WEAPON-UNIFICATION-DESIGN.md` §0. *Prev: 2026-07-05 — GROUND COMBAT is no longer ABSENT (§3f row): the surface-war layer is built and CI-gauged — units built through the **shared component designer** (`GroundUnitAtb` on a `ComponentDesign`; infantry/armor/artillery base-mod designs), hex movement (A\* terrain-weighted), a strength-math region resolver (triangle × terrain × fortification × range), fleet-echo formations (doctrine/ROE/order queue), design-driven fortification (the Bunker), environmental attrition, capture→colony-flip, and a two-zoom globe/mini-hex map with build-on-a-tile wired to real production. Client tactical map compiles; runtime is the dev's local build.* *Prev: 2026-06-25 — MVP Stage 1 (space combat) RESOLVED: the v1 auto-resolve combat engine built and CI-green end-to-end (see §3e, `GameEngine/Combat/CLAUDE.md`). 2026-06-24 — economy substrate proven, MVP scope firewall set, first damage gauge added.*

---

## 1. Are we in a position to test? — YES

| Check | State |
|-------|-------|
| Branch | `claude/adoring-gates-i6svyk` |
| Working tree | clean — everything committed |
| Pushed | yes — 18 session commits on origin (HEAD `439576f`) |
| CI (engine build + full NUnit suite, Linux) | **green** on the latest commit — 382 tests, 381 pass + 1 `[Ignore]`'d |
| Client (SDL/ImGui UI) build | **not checked by CI** — only your local Windows build checks it (see §5B) |

So the engine is provably building and passing. The one thing neither of us has verified since the
economy changes is the **client build + a live New Game**, because CI is structurally blind to the UI.
That's exactly what the play-by-play in §5 has you do.

---

## 2. Legend

| Mark | Meaning |
|------|---------|
| ✅ **DONE** | We touched it this session, fixed/verified it, and left a gauge or assertion watching it. |
| 🟢 **WORKS** | We rely on it and/or it has real tests; observed working. Not changed this session. |
| 🟡 **PARTIAL** | Runs, but known-incomplete, stubbed, or disconnected — *and we know how*. |
| 🔴 **DARK** | Exists and runs every tick, but we have **never put a gauge on it or verified its behavior**. Unknown. |
| ⚫ **ABSENT/DEAD** | Not built yet (design only), or vestigial dead code. |

"Runs (processor)" = the watch-stander that does this job each tick (the ECS *system*). Auto-discovered by
reflection — if it's in the assembly, it runs.

---

## 3. The Master Map

### 3a. Engine core — the ship's power and plumbing (everything rides on these)

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Time loop & system activity** | `MasterTimePulse` / `ManagerSubPulse` / `StarSystem.ActivityState` | ✅ DONE | `ActivityStateTests`, `GameLoopSmokeTests`, economy readout prints ActivityState | **EVERYTHING** — it decides which systems run at all. A `Stasis` system runs *nothing*. |
| **ECS core** (Entity / DataBlob / EntityManager) | — | 🟢 WORKS | `EntityManagerTests`, `DataBlobTests`, `ProtoEntityTests` | every system (all state lives in DataBlobs) |
| **Processor scheduling / re-arm** (`count==0` sleep, `AddSystemInterupt`) | `ManagerSubPulse` | ✅ DONE | documented contract in `GameEngine/CLAUDE.md` gotcha 5 | every processor |
| **Save / Load** | — | 🟡 PARTIAL | `SaveLoadSmokeTests` (round-trips); `SavingAndLoadingTests` | every DataBlob; `TypeNameHandling` ties saves to class names |
| **Orders** | `OrderableProcessor` | 🟡 PARTIAL | used via `IndustryTools.AddJob`; no dedicated gauge | industry, movement, fleets, combat (all player/AI actions) |
| **Modding / data load** | `ModLoader` | 🟢 WORKS | `BaseModIntegrityTests`, `ModLoaderTests` | **everything** (all blueprints/recipes/designs come from here) |
| **Events** | `EventManager` | 🟡 PARTIAL | `EventLogTests` | population, combat, industry (publishers); UI log |

### 3b. Economy & planetary infrastructure — **where we just worked**

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Mining** | `MineResourcesProcessor` | ✅ DONE | `EconomyReadoutTests` — prints full chain, **asserts deposits deplete** | Galaxy (`MineralsDB` on planet), Storage (cargo out), Infrastructure (efficiency ×), Colony |
| **Production / construction** | `IndustryProcessor` + `IndustryTools` | ✅ DONE | economy readout (lines+jobs) + `QueueProductionJob` lever; **`ProductionBuildTests`** (factory consumes minerals → installs a new Refinery — the build-to-product link, and the template a built unit rides) | Storage (inputs in / output out), Mining (feedstock), Infrastructure (×), Ships (builds ships), Components (builds installations via `InstallOn`), Factions (`IndustryDesigns`) |
| **Refining** | (via `IndustryProcessor`, `ProcessedMaterial`) | ✅ DONE | economy readout — **asserts Space-Crete is produced** | Mining (mineral inputs), Storage |
| **Infrastructure (efficiency grid)** | `InfrastructureProcessor` | 🟢 WORKS | economy readout — prints provided/required/efficiency | Components (installations), Colony body (gravity/pressure), **all production** (it's the throttle) |
| **Storage / cargo** | `CargoTransferProcessor` | 🟢 WORKS | `CargoTransferTests`, `CargoSpaceTests`; economy readout per-item | mining, industry, ships, logistics, launch fuel |
| **Local construction line** | `LocalConstructionProcessor` | 🔴 DARK | none | industry, components — a *second* construction path we haven't gauged |
| **Economy accounting (money)** | `Ledger` (Factions) | 🟡 PARTIAL | `LedgerTests` (math only) | **KNOWN GAP:** only records `InitialInvestment`/`Research`. Mining, trade, construction generate **no** money signal. The economy has no P&L. |

### 3c. Colony & population

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Population** | `PopulationProcessor` | 🟡 PARTIAL | `PopulationProcessorTest`; growth formulas still stubs (see `Colonies/CLAUDE.md`) | Galaxy (atmosphere → colony cost), Infrastructure (pop support cap), Storage (future: food), **Morale (migration)** |
| **Morale** (the population-tank valve) | `PopulationProcessor` (M1) reads `ColonyMoraleDB` | ✅ DONE (M1) | `MoraleTests` — pure morale math + migration sign/bounds + the real start born neutral | population (migration), Galaxy (conditions = colony cost), capacity (overcrowding); **roadmap** jobs/tax/power/food/**governor** (`docs/MORALE-AND-POPULATION-DESIGN.md`) |
| **Governance / delegation** (governors maintain worlds — *auto-resolve for the economy*) | — (wires dead `AdminSpaceDB` seat) | ⚫ ABSENT (designed, task #23) | none | **People** (governor = commander), `AdminSpaceDB` (the seat to wire), morale (what it maintains), tax/M4 (sets happy-medium). Principle: agency is OPT-IN — every management lever needs a governor auto-default. |
| **Life support / carrying capacity** | (`ColonyLifeSupportDB`, recalc) | 🔴 DARK | none | population, infrastructure |
| **Colony hex map** (spatial grid) | `ColonyHexMapProcessor` | 🔴 DARK | none | colony, **future ground combat** (already a spatial substrate) |

### 3d. Movement & orbits

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Orbits** | `OrbitProcessor`, `ChangeSOI`/`EnterSOI`, `OrbitUpdateOften` | 🟢 WORKS | `OrbitTests`, `OrbitFuzzTesting`, `StateIntegritySmokeTests` (positions stay finite) | everything with a position; launch-to-orbit; sensors; combat geometry |
| **Newtonian thrust** | `NewtonionMovementProcessor`, `NewtonSimpleProcessor` | 🟡 PARTIAL | some coverage; not gauged this session | ships, **fuel** (burns propellant), orbits, missiles, combat closing |
| **Warp / jump movement** | `WarpMoveProcessor` | 🟡 PARTIAL | `WarpMoveTests` | ships, fuel, jump points |
| **Nav sequence / pathfinding** | `NavSequenceProcessor`, `MoveStateProcessor` | 🟡 PARTIAL | `PathfindingTests` | fleets, orders, movement |

### 3e. Space combat — **MVP Stage 1 + the pattern to mirror for ground combat (`docs/MVP.md`)**

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Beam weapons** | `BeamWeaponProcessor` | 🔴 DARK | **NO TESTS** | fire control, Damage, sensors (targeting), ships, energy |
| **Missiles** | `MissileImpactProcessor` (+ `MissleProcessor`) | 🔴 DARK | **NO TESTS** (made functional 2026-06-21, untested) | Damage, movement (guidance/fuel), ordnance, sensors |
| **Generic firing / fire control** | `GenericFiringWeaponsProcessor` | 🔴 DARK | **NO TESTS** | weapons, sensors, orders |
| **Damage** | `DamageProcessor` (DamageComplex) | 🔴→🟡 gauging | `CombatReadoutTests` — calls `OnTakingDamage` directly on a real ship and prints `[combat]`. **Reading: the per-pixel sim deposits ~0 damage (broken) — which is why the auto-resolve engine below routes AROUND it.** | weapons, ships, components, **colony bombardment → population** |
| **Auto-resolve combat engine** ⭐ **BUILT this session** | `BattleTriggerProcessor` → `CombatEngagement` (`GameEngine/Combat/`) | 🟢 **WORKS** | **19 CI-green fixtures:** `ShipCombatValueTests`, `AutoResolveTests`, `BattleTriggerTests`, `FleetDoctrineTests`, `FleetComponentTests`, `FleetRetreatTests`, `EngagementLockTests`, `CombatTestShipsTests`, + depth: `ShipEvasionTests`, `WeaponProfileTests`, `DodgeResolveTests`, `CombatPerformanceTests`, + multi-party: `MultiPartyEngagementTests`, + weapon types: `RailgunWeaponTests`, `FlakWeaponTests`, `WeaponTriangleTests`, `WeaponTriangleBattleTests`, + sims: `CombatStressLab` (10 extreme weapon/scale stress sims), `CombatBattleSims` (10 whole-battle sims) | ships (combat value + evasion + weapon profiles), fleets (**the sides — now any number per side**), doctrine (player's lever), orders (engagement lock). **v1 combat spine** — decides battles by *strength math*, not the per-pixel sim above (parked v2): rate ships → auto-resolve → trigger → doctrine (per-fleet + per-component) → retreat → engagement lock. **+ combat-DEPTH pass:** weapon-flavor **dodge** (evasion vs weapon velocity/tracking/saturation — beams ignore evasion, slugs are dodged, flak floors it), aggregated by class so it stays **O(ships)**. **+ MULTI-PARTY:** any number of fleets on either side, joining a fight in progress by coming into range (`StepEngagementGroup`; sides = factions, one system = one battle — both v1 stubs); the 2-fleet fight is the n=2 special case. **+ REAL WEAPON TYPES (player-buildable):** railgun (finite-velocity kinetic, dodgeable) and flak (high-saturation, floors the dodge) via the full JSON template→Atb path; the weapon triangle demonstrated on real Wasp fighter / Leviathan capital designs. **+ PACED (hot-damage rebalance 2026-06-25):** `CombatEngagement.SalvoDamageScale` (0.1) — a salvo deposits a tenth of its raw energy, so battles last ~10× more salvos (a 50v50 now runs 38 salvos ≈ 190 game-seconds, not 2–4) WITHOUT changing who wins; one emergent shift is that the 50%-loss retreat now actually triggers (some old wipes are now break-offs). **Remaining depth (v2):** the Capital▸Beam triangle edge (needs weapon RANGE), degraded-condition tiers (needs recalc-combat-value-on-damage). See `docs/COMBAT-DESIGN.md`, `docs/WEAPONS-AND-DODGE-DESIGN.md`, `GameEngine/Combat/CLAUDE.md`. **Example ships (DevTools faction switcher):** Aegis (beam) / Lancer (railgun) / Bulwark (flak) / Wasp (fighter) / Leviathan (capital) / Picket. |
| **Ground combat** ⭐ **BUILT (2026-07-04→05)** | `GroundForcesProcessor` (the one hotloop on `GroundForcesDB`: move · fight · capture · attrition · formations · ROE) | 🟢 **WORKS** (engine, CI-gauged) · client runtime-unverified | **CI-green fixtures:** `GroundForcesTests` (units/formations/hex-path/range-combat/terrain/fortification/stance/ROE/environments/garrison), `CityGridTests` (two-zoom globe+mini-hex roll-up), `GroundBuildEconomyTests` (build-on-tile through real production), `GroundUnitDesignerTests` + `GroundUnitBaseModTests` (**the shared designer**). Client `PlanetViewWindow` tactical map = dev's local build only. | colony (`PlanetEntity` holds the roster), Galaxy/`PlanetRegionsDB` (regions + `SurfaceGrid` hex cylinder), Damage (orbital bombardment softens a garrison), industry (**ground units are `ComponentDesign`+`GroundUnitAtb` — built on the installation-construction line**), research (gates designs). **The model:** a planet body carries `GroundForcesDB` (a roster of `GroundUnit`s, each knowing region/owner/stats); units are **built via the shared component designer** (infantry/armor/artillery base-mod designs), **moved** hex-by-hex (A\* terrain-weighted, ocean impassable), **fought** by a strength-math resolver mirroring the space `AutoResolve` (triangle × terrain × fortification × **range**/directed-fire), grouped into **formations** (fleet echo: doctrine/stance + sequential order queue), fortified by `GroundDefenseAtb` buildings (the Bunker), attrited by `PlanetEnvironmentsDB` hazards (gear negates), and **captured** by clearing a region (owner flip → colony flip). **Two-zoom map** (globe `SurfaceGrid` operational hexes → mini-hex `CityGrid` tiles, roll-up invariant) with build-on-a-tile wired to real production. See `GameEngine/GroundCombat/CLAUDE.md`, `docs/GROUND-COMBAT-MAP-DESIGN.md`, `docs/HEX-GROUND-AND-ORDERS-DESIGN.md`. **Remaining:** units-as-entities (v2), trade-knob designer formulas / research-gating (pending user calls), client runtime pass. |

### 3f. Sensors, survey, and the rest of the watch-bill

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Sensors** | `SensorScan`, `SensorReflectionProcessor` | 🔴 DARK | **detection RANGE now readable** — `SensorTools.SelfDetectionRange_m` / `DetectionRange_m` (the reverse-solve), gauged by `RangeReadoutTests`; surfaced in the Fleet Combat tab "Sensor Reach" column + the blue map range ring (`docs/INFORMATION-DELTA-DESIGN.md`). Contact-existence still via `SensorDetectionTests` | orbits, contacts, IFF, combat targeting, fog-of-war |
| **Research / tech** | `ResearchProcessor` | 🔴 DARK | none | factions, **industry (unlocks the designs you can build)**, components, people (scientists) |
| **Energy / power** | `EnergyGenProcessor`, `EnergyGenHotloopProcessor` | 🔴 DARK | none | ships, components, sensors (power draw), reactors, fuel |
| **Logistics** (auto cargo routes) | `LogiBaseProcessor`, `LogiShipProcessor` | 🔴 DARK | none | storage, fleets, **trade**, colonies |
| **Fleets** | `FleetOrderProcessor` | 🟢 WORKS (engine) | **`StartFleetTests`** — the New Game start builds 3 fleets / 5 ships (CI-proven); DevTools "Dump State" reads live fleet count | ships, movement, orders, combat. **Client:** fleet changes go via `FleetOrder.*` + `OrderHandler` (FleetWindow); `FleetDB` mutators are engine-internal |
| **Ships & launch** | `LaunchComplexProcessor`, `ShipFactory` | 🟡 PARTIAL | **`ShipSpawnTests`** (spawn lands in system + survives a tick); traced launch fuel cost; `ShipTests`, `ShipComponentTests` | industry (builds them), **fuel** (launch cost via rocket eqn), orbits, fleets. **DevTools "Spawn Ship" issue → §6.** |
| **People / commanders** | `AdminSpaceProcessor`, `NavalAcademyProcessor` | 🔴 DARK | none | colony (admin radius), ships (captains), research (scientists) |
| **Survey** (geo & jump-point) | `GeoSurveyProcessor`, `JPSurveyProcessor` | 🔴 DARK | none | Galaxy (reveals minerals), jump points, movement |
| **Factions & NPC AI** | `NPCDecisionProcessor` (socket fires; `Tick` = empty stub) | 🔴 DARK — but now **FULLY DESIGNED** (8-doc AI suite, 2026-07-10/11) | none | **economy (could auto-queue jobs!)**, industry, diplomacy, combat doctrine, People/seats, sensors (enemy-strength read), exploration-content |
| ↳ *AI design entry point* | see `docs/AI-IMPLEMENTATION-AND-WIRING-MAP.md` (hub; §4 socket verification) → `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (code-ready) + 6 more `AI-*.md` | 📝 DESIGN COMPLETE, 0% BUILT | n/a (design) | **Prereqs before any build:** ⚠ fix seat durability (`People/AdminSpaceProcessor.cs:36-46`, "paramount"); 2 NO-SOCKETs (fleet situational read; crisis seed) |
| **Galaxy / system generation** | `StarSystemFactory`, `AtmosphereProcessor` | 🟢 WORKS | `AtmosphereDBExtensionsTests`, `AtmosphereAndSpeciesTests`; we rely on it | minerals, colony siting, orbits, sensors |
| **Diplomacy** | — | ⚫ ABSENT | design only (`docs/DIPLOMACY-DESIGN.md`) | factions, IFF, trade, logistics |
| **UI client** (ImGui/SDL) | — | 🟢 **runtime-confirmed 2026-07-03** (boots + the fleet UI now usable) | **CI-blind** but now play-tested: New Game boots with the auto-spawn scenario (43 ships), the clock runs, and the **Fleet Management window is usable** after fixing a hard freeze (a native ImGui assert in `BeginPopupContextItem` — misread as `[HANG]` because its modal blocks the main thread; converted all fleet-window context menus to explicit `IsItemClicked(Right)+OpenPopup+BeginPopup`). Colony economy UI fully wired (`ColonyManagementWindow`); **+ range/info readouts** (engagement range, sensor reach, delta-V, ETA + map range rings + tooltips + OUT OF RANGE flag). **Diagnostic infra that made this tractable:** the `game_logs/` folder is now git-tracked (crashes ship their trace) + `SessionLog.CurrentStage` sub-breadcrumbs localise a freeze to the exact method. Remaining live-unverified: Society tab / economy-UI tab *render*, played-game save/load, hazards live. Gaps + verify tasks in **§6** | displays every system; live-test only (§5B) |

---

## 4. The economy cluster, wired (where we are)

```
        Galaxy/SystemGen ──> MineralsDB (on planet)
                                  │
                                  ▼
   Infrastructure ──(×efficiency)──> MINING ──> Cargo/Storage ◄─────────────┐
   (grid throttle)                     │            │                        │
                                       │            ▼                        │
                                       │      PRODUCTION / REFINING ─────> outputs:
                                       │       (needs a QUEUED JOB)         • refined materials (Space-Crete…)
                                       │            │                        • installations (Mine, Factory…) ─┘ (grows mining)
                                       │            │                        • ships (consume FUEL at launch)
                                       ▼            ▼
                                  deposits      consumes cargo inputs
                                   deplete       (the same stockpile mining fills)

   NOT WIRED YET:  Ledger (no money from any of this) · Population demand · NPC auto-queue · Local construction line
```

**Proven this session — the full substrate gather→refine→build:** mining fills the stockpile (10 units/
mineral/day, deposits deplete); a queued refining job turns mined regolith/water/etc. into Space-Crete
(0 → 5,200/yr); **the factory consumes minerals and installs a new Refinery (1→2, `ProductionBuildTests`)** —
the `InstallOn` rails a built unit will ride. Launch burns colony fuel by the rocket equation (one-time, ~1%).
Infrastructure efficiency 100%. **The economy substrate is DONE; "turn resources into products" is proven.**

**Remaining economy work — all DEFERRED past the MVP (see `docs/MVP.md` OUT list), not blocking v1:**
1. ~~Local construction / building installations~~ — **DONE** (`ProductionBuildTests`).
2. **Ledger / money** — give mining/refining/construction/trade a P&L signal. v2 (needed for NPC reasoning).
3. **Population demand** — the consumption side. v2.
4. **NPC auto-queue** (`NPCDecisionProcessor`) — autonomy, once there's a complete economy + money to reason about. v2.

For the MVP, the economy is "done enough": it builds ships and units. The only open Stage-0 item is the
**live UI verification** (§6 + §5B step 7).

---

## 5. Play-by-play: how to test (on your Windows machine, PowerShell)

> Two kinds of testing. **A is the reliable proof** (the engine is verified here). **B is the live game**
> — it mostly confirms the UI still launches and a New Game starts, because the economy UI is incomplete.

### A. Automated — the reliable proof (do this first)

```powershell
# from the repo root
git fetch origin
git checkout claude/adoring-gates-i6svyk
git pull origin claude/adoring-gates-i6svyk

# 1) Build the whole solution — this ALSO compiles the SDL client, which CI never does:
dotnet build Pulsar4X/Pulsar4X.sln

# 2) Run the suite with detailed output so the economy readout prints:
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj --logger "console;verbosity=detailed"
```

**What success looks like:**
- Build: `Build succeeded`, 0 errors. (If the *client* fails to compile, that's the one thing CI can't
  catch — copy the error back to me.)
- Tests: all green. In the output, search for `[econ]` — that's the live economy gauge. You should see,
  over one game-year:
  - `mining (end): … BaseMiningRate=15 entries (sum 150)` and deposits dropping (`mined 35,770`),
  - `Refined Space-Crete: 0 -> ~5,200`,
  - `System RP-1 fuel: … (delta ~ -493,027)` with the **Colony** line losing it and **Surveyor I** flat.
- The test `Economy_BaselineReadout_OverOneYear` **passing** is the proof the mine→refine chain runs.
- `ShipSpawnTests` (`SpawnShip_LandsInSystem_WithCoreBlobs`, `SpawnedShip_SurvivesTimeAdvance`) **passing**
  is the proof the **engine** spawns ships correctly — it runs the exact `ShipFactory.CreateShip` path the
  DevTools "Spawn Ship" button uses. So if a ship "won't spawn" in the live game, it's a **UI** problem,
  not the engine (see §6).

### B. Live game — confirm it launches & a New Game starts (catches client-only breakage)

```powershell
# Easiest: use the launch helper (it captures all console output to console_output.txt and
# keeps the window open on crash):
.\launch.bat

# …or run the client directly:
dotnet run --project Pulsar4X/Pulsar4X.Client/Pulsar4X.Client.csproj
```

**Step by step:**
1. Game window opens → **New Game**.
2. Leave the defaults (the wizard's first species/colony/system = the Sol start we test).
3. **Start**. ✅ Expected: it loads into the system map without crashing. (This exercises the Stasis
   promotion + the earlier New-Game crash fix.)
4. Find **Earth** / your starting colony, open its window. Look for cargo / minerals / industry panels.
5. Let time run (speed up). Minerals *should* tick up and deposits down — **but** the colony economy UI
   is known-incomplete (the Installations tab is dead, some panels are orphaned), so if you can't see it
   clearly in the UI, that's a **UI gap, not an engine bug** — the engine side is already proven in §5A.

6. **Ship-spawn fix (yesterday's issue).** Turn on **Space Master (SM)** mode (the Dev Tools window is gated
   on it). Open the **Ship Design** window and design + save a ship. Now open **Dev Tools**:
   - ✅ The ship you just designed should already be in the **"Design" dropdown — *without* clicking
     "Refresh Lists".** That's the fix (the list used to be cached until a manual refresh).
   - Pick a body under "Orbit around", optionally type a name, click **Spawn Ship**. Expected: green
     "Spawned …" text, a new ship in the system, and `[DevTools] Spawn Ship OK: …` in the console.
   - If the design still doesn't appear, or you see `[DevTools] Spawn Ship FAILED: …`, send me
     `console_output.txt` — that's the repro reading we never captured yesterday.

7. **Colony economy UI (the Stage-0 "can I see/drive the loop?" check).** Open **Manage Colonies** (toolbar/
   menu) → pick your colony. Walk the tabs and confirm the minerals→refined→components loop is visible and
   drivable:
   - **Summary** — shows population, **infrastructure** (Provided/Used/Available + "Output at N% of capacity"),
     **installed components**, and the **stockpile** (raw minerals *and* refined materials).
   - **Mining** — Number of Mines + per-mineral table (Stockpile / Available / Accessibility / **Annual
     Production** / **Years to Depletion**). After running time, stockpiles should climb, deposits fall.
   - **Production** — click **+ New Job**, pick something (e.g. a refined material or an installation), set
     batch/repeat/auto-install, **Queue the job**. Run time; confirm it builds and the stockpile/installs change.
   - This is the verification that Stage 0 is *usable*, not just engine-correct. **Report which tabs work, which
     are blank/wrong, and whether Manage Colonies even opens** (the `GetInstance` pattern is slightly suspect) —
     with `console_output.txt` on any crash.

**If anything crashes or looks wrong:** send me `console_output.txt` (next to the executable). That's the
client's only diagnostic channel — CI can't see it.

---

## 6. Known open issues — live-test / client backlog (CI is blind to these)

These are real issues seen in the running client. CI never compiles the UI, so **none of them can go
green in an automated run** — they're verified only by the developer's live build + `console_output.txt`.
Listed here so they're tracked, not lost.

| Issue | Where | What we know | Status / next |
|-------|-------|--------------|---------------|
| **Spawned ships invisible / "fleets not working"** | `DevToolsWindow.cs` · `Ships/ShipFactory.cs` | **Two layered causes, both found 2026-06-24.** (1) `ShipFactory.CreateShip` orbits a ship at **2× the planet's radius** → sub-pixel on the planet icon at system zoom (zoom in / Fleet window to see it); the "previous name stayed" wart was the post-spawn `HardRefresh()` resetting the dropdown. (2) **A bare `CreateShip` puts the ship in NO fleet**, so it never shows in the Fleet window or takes orders — and the *same gap one level up* is why the **New Game start builds no fleet at all** (next row). | ✅ **Engine proven** (`ShipSpawnTests`, CI-green) + **starting fleet proven** (`StartFleetTests`, next row). ✅ **`DevToolsWindow`:** "Dump State" button; dropdown-reset removed; flushed `DevLog`; **spawned ships now join a fleet via the ORDER system** (`FleetOrder.AssignShip(playerFaction, fleet, ship)` → `OrderHandler.HandleOrder` — the client-legal path, *not* the engine-internal `FleetDB.AddChild` that broke the build before). So a spawned ship now appears in the Fleet window like the engine-launched ones do. **Why the difference existed:** `LaunchComplexProcessor.TryLaunchShip` (engine) calls `fleetDB.AddChild(ship)`; the client DevTools spawn couldn't. **Root cause of "ISS Hermes invisible":** the launch-queue courier (earth.json `LaunchQueue`) launches to **low Earth orbit** (`OrbitMath.LowOrbitRadius`), even tighter to the planet than a 2× spawn — so it's behind Earth's icon at system zoom; zoom in to see it. All client-only → verify on the dev's local build. |
| **"No starting fleet" — was a WRONG diagnosis** | `Colonies/ColonyFactory.cs:CreateFromBlueprint` · `GameData/basemod/ScenarioFiles/systems/sol/earth.json` | **The engine DOES build the starting fleet** — CI-proven 2026-06-24 (`StartFleetTests`: **`[start] fleets=3, ships=5`**, all owned by the player faction). The colony blueprint `colony-earth` (earth.json) defines 3 fleets nested correctly (Freight: freighter; Military: 2 gunships; Science: surveyor + sensor sat), and `CreateFromBlueprint` builds them. (An earlier note here claimed the fleets were faction-level/never built — that traced `uef.json`, a scenario file the wizard does **not** use. Wrong file; corrected.) | 🟢 **Engine: WORKS + tested** (`StartFleetTests`, CI-green, regression guard). ⏳ **Live "I see no fleet" is now a CLIENT or STALE-DATA question, not engine:** ships orbit Earth at 2× radius (hidden at system zoom — zoom in), and the running game reads `%AppData%\…\Mods\` which a **successful** client build refreshes from `GameData` (the build was broken — a clean rebuild may be the fix). **Confirm with DevTools "Dump State" after a clean build + NEW game:** 3 fleets ⇒ they're there (find them in the Fleet window); 0 fleets ⇒ stale live mod data, refresh the Mods folder. |
| **Planetary "Installations" tab never appears** | `PlanetaryWindow.cs` | Tab gated on dead `InstallationsDB` (root gotcha 4); should render from `ComponentInstancesDB`. | Known; fix = reuse `ComponentInstancesDBDisplay`. |
| **Colony economy UI — ✅ VERIFIED LIVE 2026-06-24** | `ColonyManagementWindow` + `PlanetaryWindow` | **The full minerals→refined→components UI works in the running game** — developer live-tested it ("everything works but the spawner"). Summary/Production/Construction/Mining tabs + job queuing via `IndustryOrder2` all confirmed. This **closes the last open Stage-0 item** — the economy loop is real, visible, and drivable. | ✅ **Done.** No further action. (The standalone `PlanetaryWindow` Installations tab is the only loose end — see the row below — but the colony economy lives in `ColonyManagementWindow`, which is confirmed.) |

**Already fixed in live-test (2026-06-22, for reference):** New Game empty-mod crash, Save dialog NRE at drive root, ship-design armor-material NRE. See `SESSION_STATE.md`.

---

## 7. How to keep this map honest

Update a row the moment its status changes (same commit as the code). When you finish a system, it moves
🔴→🟡→✅ and gains a "Can we see it?" entry. When you *start* a system, read its **Connected to** row and
every row it points at — that is the whole point of this document. Live-test/client issues go in §6 (CI
can't grade them).
