# Pulsar4X — Systems Status & Test Plan (Living Map)

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

**Last updated:** 2026-06-24 — economy substrate proven (gather→refine→build all gauged), MVP scope firewall set, stale UI docs corrected (branch `claude/adoring-gates-i6svyk`).

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
| **Population** | `PopulationProcessor` | 🟡 PARTIAL | `PopulationProcessorTest`; runs in our year-advance but **we never gauged growth** | Galaxy (atmosphere → colony cost), Infrastructure (pop support cap), Storage (future: food) — formulas are stubs (see `Colonies/CLAUDE.md`) |
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
| **Damage** | `DamageProcessor` (DamageComplex) | 🔴 DARK | **NO TESTS** (wired per root gotcha 1) | weapons, ships, components, **colony bombardment → population** |
| **Ground combat** | — | ⚫ ABSENT | — | colony, hex map, Damage, population, industry (build ground units) — **the destination** |

### 3f. Sensors, survey, and the rest of the watch-bill

| System | Runs (processor) | Status | Can we see it? | Connected to |
|--------|------------------|--------|----------------|--------------|
| **Sensors** | `SensorScan`, `SensorReflectionProcessor` | 🔴 DARK | none | orbits, contacts, IFF, combat targeting, fog-of-war |
| **Research / tech** | `ResearchProcessor` | 🔴 DARK | none | factions, **industry (unlocks the designs you can build)**, components, people (scientists) |
| **Energy / power** | `EnergyGenProcessor`, `EnergyGenHotloopProcessor` | 🔴 DARK | none | ships, components, sensors (power draw), reactors, fuel |
| **Logistics** (auto cargo routes) | `LogiBaseProcessor`, `LogiShipProcessor` | 🔴 DARK | none | storage, fleets, **trade**, colonies |
| **Fleets** | `FleetOrderProcessor` | 🔴 DARK | none | ships, movement, orders, combat |
| **Ships & launch** | `LaunchComplexProcessor`, `ShipFactory` | 🟡 PARTIAL | **`ShipSpawnTests`** (spawn lands in system + survives a tick); traced launch fuel cost; `ShipTests`, `ShipComponentTests` | industry (builds them), **fuel** (launch cost via rocket eqn), orbits, fleets. **DevTools "Spawn Ship" issue → §6.** |
| **People / commanders** | `AdminSpaceProcessor`, `NavalAcademyProcessor` | 🔴 DARK | none | colony (admin radius), ships (captains), research (scientists) |
| **Survey** (geo & jump-point) | `GeoSurveyProcessor`, `JPSurveyProcessor` | 🔴 DARK | none | Galaxy (reveals minerals), jump points, movement |
| **Factions & NPC AI** | `NPCDecisionProcessor` | 🔴 DARK | none | **economy (could auto-queue jobs!)**, industry, diplomacy, combat doctrine |
| **Galaxy / system generation** | `StarSystemFactory`, `AtmosphereProcessor` | 🟢 WORKS | `AtmosphereDBExtensionsTests`, `AtmosphereAndSpeciesTests`; we rely on it | minerals, colony siting, orbits, sensors |
| **Diplomacy** | — | ⚫ ABSENT | design only (`docs/DIPLOMACY-DESIGN.md`) | factions, IFF, trade, logistics |
| **UI client** (ImGui/SDL) | — | 🟡 more built than the docs claimed | **CI-blind**; the colony economy UI is fully wired (`ColonyManagementWindow`); real state is live-unverified. Gaps + verify tasks in **§6** | displays every system; live-test only (§5B) |

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
| **"Spawn Ship" — designed ship not in dropdown** | `DevToolsWindow.cs` (Spawn Ship) | The spawn dropdown was built from `factionInfo.ShipDesigns` only inside `HardRefresh()`, which runs on open / **"Refresh Lists"** / system-change / after a spawn — **not** when you design a ship elsewhere. So a just-designed ship was missing until you clicked Refresh (the developer's own "I probably didn't refresh"). The **engine spawn itself works** (`ShipFactory.CreateShip` is sound; proven by the live "Surveyor I"). | ✅ **Engine gauge added & CI-green:** `ShipSpawnTests` runs the exact `CreateShip` path and asserts the ship lands in the system — engine spawning is now proven, so this was purely a UI problem. ✅ **Client fix applied** (`DevToolsWindow.SyncShipDesigns()` re-reads the design list each frame when its count changes, so a just-designed ship appears with no manual Refresh). ⏳ **Pending live verification** — §5B step 6 (CI can't build the client). |
| **Planetary "Installations" tab never appears** | `PlanetaryWindow.cs` | Tab gated on dead `InstallationsDB` (root gotcha 4); should render from `ComponentInstancesDB`. | Known; fix = reuse `ComponentInstancesDBDisplay`. |
| **Colony economy UI — exists, live-unverified** | `ColonyManagementWindow` + `PlanetaryWindow` | **The full minerals→refined→components UI is already wired** (2026-06-24 code read): Summary/Production/Construction/Mining tabs, including queuing jobs via `IndustryOrder2`. Earlier "panels not wired" note was wrong. | **Not a build task — a verify task.** Run it (§5B step 7) and report what works/breaks; CI can't see the client. Fix only real live bugs. |

**Already fixed in live-test (2026-06-22, for reference):** New Game empty-mod crash, Save dialog NRE at drive root, ship-design armor-material NRE. See `SESSION_STATE.md`.

---

## 7. How to keep this map honest

Update a row the moment its status changes (same commit as the code). When you finish a system, it moves
🔴→🟡→✅ and gains a "Can we see it?" entry. When you *start* a system, read its **Connected to** row and
every row it points at — that is the whole point of this document. Live-test/client issues go in §6 (CI
can't grade them).
