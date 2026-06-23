# Pulsar4X — Systems Status & Test Plan (Living Map)

**What this is:** one place that lists *every* system in the game, what state it's in, whether we can
*see* it (a gauge/test), and what it's *wired to*. It exists so we stop pivoting from one shiny broken
thing to the next. Pick the next job off this map, not off whatever we happened to trip over.

**The anti-pivot rule (read before touching anything):**
> You do not work a system alone. Find its row, read its **Connected to** column, and read *those* rows too.
> A change to one station ripples down the line. This is the Prime Directive in table form.

**Last updated:** 2026-06-23, end of the economy session (branch `claude/adoring-gates-i6svyk`).

---

## 1. Are we in a position to test? — YES

| Check | State |
|-------|-------|
| Branch | `claude/adoring-gates-i6svyk` |
| Working tree | clean — everything committed |
| Pushed | yes — all 9 session commits on origin |
| CI (engine build + full NUnit suite, Linux) | **green** on the latest commit `06bf4fb` |
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
| **Production / construction** | `IndustryProcessor` + `IndustryTools` | ✅ DONE | economy readout — prints lines+jobs; `QueueProductionJob` lever | Storage (inputs in / output out), Mining (feedstock), Infrastructure (×), Ships (builds ships), Components (builds installations), Factions (`IndustryDesigns`) |
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

### 3e. Space combat — **the pillar to mirror for ground combat (developer's objective)**

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
| **Ships & launch** | `LaunchComplexProcessor`, `ShipFactory` | 🟡 PARTIAL | traced launch fuel cost this session; `ShipTests`, `ShipComponentTests` | industry (builds them), **fuel** (launch cost via rocket eqn), orbits, fleets |
| **People / commanders** | `AdminSpaceProcessor`, `NavalAcademyProcessor` | 🔴 DARK | none | colony (admin radius), ships (captains), research (scientists) |
| **Survey** (geo & jump-point) | `GeoSurveyProcessor`, `JPSurveyProcessor` | 🔴 DARK | none | Galaxy (reveals minerals), jump points, movement |
| **Factions & NPC AI** | `NPCDecisionProcessor` | 🔴 DARK | none | **economy (could auto-queue jobs!)**, industry, diplomacy, combat doctrine |
| **Galaxy / system generation** | `StarSystemFactory`, `AtmosphereProcessor` | 🟢 WORKS | `AtmosphereDBExtensionsTests`, `AtmosphereAndSpeciesTests`; we rely on it | minerals, colony siting, orbits, sensors |
| **Diplomacy** | — | ⚫ ABSENT | design only (`docs/DIPLOMACY-DESIGN.md`) | factions, IFF, trade, logistics |
| **UI client** (ImGui/SDL) | — | 🔴 DARK + partly broken | **CI-blind**; `PlanetaryWindow` installations tab dead (root gotcha 4); economy panels orphaned | displays every system; live-test only (§5B) |

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

**Proven this session:** Mining fills the stockpile (10 units/mineral/day, deposits deplete). A queued
refining job turns mined regolith/water/etc. into Space-Crete (0 → 5,200/yr). Launch burns colony fuel
by the rocket equation (one-time, ~1%). Infrastructure efficiency 100%.

**The three loose ends on this cluster** (in dependency order — do them before leaving the economy):
1. **Local construction / building installations** — point the lever at the Factory to build a Mine from
   refined Space-Crete → colony grows its own mining. Closes mine→refine→**build**→more-mining.
2. **Ledger wiring** — give mining/refining/construction/trade a money signal, or the economy has no P&L
   and no NPC can ever reason about it.
3. **Population demand** — gauge whether pop grows/consumes; it's the demand side of the whole economy.

Only **after** those is it honest to say "the economy works," and only then does **NPC auto-queue**
(`NPCDecisionProcessor`) have a complete economy to drive.

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

**If anything crashes or looks wrong:** send me `console_output.txt` (next to the executable). That's the
client's only diagnostic channel — CI can't see it.

---

## 6. How to keep this map honest

Update a row the moment its status changes (same commit as the code). When you finish a system, it moves
🔴→🟡→✅ and gains a "Can we see it?" entry. When you *start* a system, read its **Connected to** row and
every row it points at — that is the whole point of this document.
