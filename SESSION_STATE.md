# Session State — Current Known Status

This document tracks what we know about the codebase state at the close of each session. A new session reads this first after the CLAUDE.md files, to skip rediscovery.

---

## Last Updated

Session ending ~2026-06-24 (economy substrate proven + scope firewall). Branch: `claude/adoring-gates-i6svyk`, HEAD `439576f` (18 commits this session, all CI-green).

---

## Session 2026-06-24 — Economy Substrate Proven + MVP Scope Firewall (READ THIS)

Chased "the mine does literally nothing," proved the whole economy substrate, built the safety/scope docs, and corrected a pile of stale docs. Everything CI-green.

### What was built
- **Scenario harness `TestScenario`** (`Pulsar4X.Tests/TestScenario.cs`): stands up a REAL faction+colony via the live `CreateFromBlueprint` path, advances the sim clock, and exposes `QueueProductionJob(designId, count, repeat, installOnColony)` — the engine-level "player queues a build" lever. **The mid-game fixture the 2026-06-22 session flagged as the next step.**
- **Economy gauges (CI-green, asserting):** `EconomyReadoutTests` (mining depletes deposits; refining makes Space-Crete; infra/fuel readouts), `ProductionBuildTests` (factory consumes minerals → installs a Refinery, 1→2 — the build-to-product link), `ShipSpawnTests` (engine ship-spawn lands a ship in the system + survives a tick — first coverage for the DARK Ships system).
- **`docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`** — the living systems map: every system's status (done/works/partial/dark/absent), its gauge/test, and what it's wired to; plus §5 play-by-play live-test and §6 client backlog. Made a MANDATORY consult in root `CLAUDE.md` (Prime Directive + working agreement).
- **`docs/MVP.md`** — the scope firewall. MVP = **"One Planet, Taken"**: build a fleet + ground force, win the SPACE battle over a planet, drop troops, win the GROUND battle, capture it. 4X scorecard: Exploit(economy=substrate)/Expand/Exterminate IN; **eXplore and eXploit=espionage are the two deferred v2 strategic pillars**. Build path Stage 0 (economy, DONE) → 1 (space combat, gauge it) → 2 (ground combat, mirror it) → 3 (stitch loop) → 4 (UI).

### The headline fix — the mine "did nothing" was a frozen SYSTEM, not broken mining
`StarSystem.ActivityState` defaults to **Stasis**, and `MasterTimePulse` skips Stasis systems — so the colony's whole system never processed (no mining/industry/population), and nothing threw. The mining chain was correct all along (rates/efficiency/minerals all fine). Fix: the harness promotes the starting system to Foreground (the live game does this via faction presence + the player observing it). Also bumped the mine base rate 10× (`installations.json`, `Area*0.000001`→`*0.00001`) to match the old design scale.

### Economy substrate — COMPLETE and gauged (gather → refine → build)
- **Gather:** mining depletes deposits (asserted), respects storage `FreeVolume`. All 8 connection points audited.
- **Refine:** Space-Crete 0→5,200 over a year (asserted); cross-checked that every refined material's raw inputs are in the 15 mined minerals.
- **Build:** factory installs a new Refinery (asserted) via `IndustryProcessor → ConstructStuff → OnConstructionComplete → AddComponent(InstallOn)` — **the exact path a built ground unit will ride.** So units are now a DataBlob/data task on proven plumbing.

### Stale docs corrected (the colony economy UI ALREADY EXISTS)
Went to "build the colony economy UI" and found it already wired in `ColonyManagementWindow` (Summary/Production/Construction/Mining tabs, with job-queuing via `IndustryOrder2`) and `PlanetaryWindow`'s Installations tab already fixed (gates on `ComponentInstancesDB`). Root gotcha #4, `Pulsar4X.Client/CLAUDE.md`, and this file were stale — corrected. **Lesson: verify client state by running it; don't trust a "broken UI" note without reading the code.** Real state is live-unverified (CI is client-blind).

### Findings parked (not fixed)
- **`gallicite`** — referenced by `ordnance.json` but undefined as a mineral → missiles may not be buildable. Latent; **will bite Stage 1 (space combat uses missiles)**, not Stage 0.
- **`count==0` hotloop sleep is self-healing**, not a bug (re-armed by `SetDataBlob`); documented the contract in `GameEngine/CLAUDE.md` gotcha 5.
- **RP-1 fuel −493k/yr** = the Launch Complex putting the queued courier into orbit (rocket equation), not a leak.

### Open follow-ups (developer live-test — CI can't see the client)
- **§5B step 7:** open **Manage Colonies**, walk Summary/Mining/Production, queue a job, confirm the loop works live (and that the window even opens — `GetInstance` looks slightly suspect). Report + `console_output.txt`.
- **§5B step 6:** confirm the DevTools ship-spawn refresh fix (designed ship appears without "Refresh Lists").
- **Next stage (deliberate):** Stage 1 — put space combat under a gauge (read `COMBAT-DESIGN.md`/`Weapons/`/`Damage/`, stand up a two-fleet harness fight). Fix `gallicite` before relying on missiles.

---

## Session 2026-06-22 — Testing Infrastructure + the Live-Test Loop (READ THIS)

This session built the missing safety net and a live-test workflow, then used it to catch a cascade of real crashes.

### What was built
- **CI** (`.github/workflows/ci.yml`): builds engine + runs the full NUnit suite on every push/PR, with a per-test TRX report (inline table + artifact). The automated gate the cloud container can't be (no local .NET SDK). **CI does NOT build the SDL client.**
- **Four passive read-only sensors** (none mutate the sim): `GameLoopSmokeTests` (advance clock, no processor throws), `SaveLoadSmokeTests` (Game.Save→Load round-trips), `StateIntegritySmokeTests` (entity positions stay finite — catches silent NaN the engine never guards), `PerformanceReadoutSmokeTests` (reads the built-in per-processor stopwatch).
- **`NewGameStartSmokeTests`**: reproduces the real New Game colony path (CreateFromBlueprint) in CI. Base mod passes; **base + testing mod throws NRE** (testing mod ships incomplete Armor/Theme data, adds no species/colony) → marked `[Ignore]` pending a testing-mod data fix.
- **`launch.bat` rewritten** to capture all console output to `console_output.txt` and keep the window open (`pause`). **The single most valuable diagnostic tool of the session.**
- **`DevToolsWindow` promoted** from `sleepy-meitner` (Spawn Ship / Create Colony / Add Minerals in SM mode), now with all actions logged to the console.

### Bugs found & fixed (every one via the live-test console loop)
1. **New Game crash** — mods page `_modDataStore.Species.First()` on an empty store when no mod is enabled (the unimplemented `// FIXME`). Guarded. `NewGameMenu.cs`
2. **DevTools build error** — missing `using Pulsar4X.Galaxy` for `MassVolumeDB` (namespace drift on cherry-pick). `DevToolsWindow.cs`
3. **Save-dialog crash** — `FileDialog` dereferenced `Directory.GetParent` null at the drive root. Guarded. `FileDialog.cs`
4. **Ship-design crash** — `ShipDesign.GetArmorMass` dereferenced a null armor material (not in faction cargo library). Guarded → returns 0. `ShipDesign.cs` — *engine code; a ship-design test would catch this in CI.*
5. **"Ship didn't spawn"** — not a bug: `ShipFactory.CreateShip(design, faction, parent, name)` places it at **2× the body's radius** (hugging the planet, hidden under the icon). Added DevTools console logging so spawn results are captured.

### LESSONS LEARNED (these shaped every fix above)
1. **Live data beats speculation.** Driving the real game and reading `console_output.txt` caught bugs faster and more reliably than reasoning about code. When a symptom is reported, instrument and capture before theorizing.
2. **CI is structurally blind to the client.** It builds engine + tests, never the SDL/OpenGL client. Every client-only bug (build errors, UI crashes) is invisible to CI — the **`launch.bat` console capture is the client's only test/diagnostic channel**, verified by the developer's local build.
3. **A reproduction test reproduces what you SCRIPT, not what the user DOES.** `NewGameStartSmokeTests` found a *different* bug (testing-mod NRE) than the user's actual crash (empty-mods `.First()`), because it always loaded mods explicitly. Oblique results still narrow the search, but the real user flow is ground truth.
4. **Verify a symbol is IMPORTED, not just that it EXISTS.** Cross-branch cherry-picks break on namespace drift (`MassVolumeDB`→Galaxy, `PositionDB`→Movement) even when the type exists. For client cherry-picks, a local build is the only real check.
5. **A swallowed exception is an invisible bug.** DevTools hid action errors in UI-only status text → undiagnosable from logs. Route diagnostic / god-mode results to the console (or a captured file).
6. **Unguarded `null` / `.First()` / `Directory.GetParent` on data-dependent paths is the recurring crash class here** (same family as the New Game JSON data bugs, root gotcha #10). When code assumes data is present, guard the access.
7. **An expected-to-fail reproduction test must land `[Ignore]`'d (or fixed) in the SAME commit.** Merging the failing repro one PR before its `[Ignore]` turned main red briefly. Never land a deliberately-red test on the integration branch.
8. **Engine bugs are CI-catchable; client bugs are not.** The GetArmorMass null was engine code — a ship-design test would have gone red in CI on the introducing commit. This is the concrete case for the **automated scenario harness** (next step): headless tests that build ships/colonies/fleets and exercise features, catching engine-side regressions before a playthrough.

### Live-test workflow (the play-by-play)
Quickstart → Esc → **SM Mode** → toolbar **Dev Tools** → spawn preconditions → **TimeControl** (pause, set span to Years/Days, **Step**) → watch `console_output.txt` → **Save Game** to bank a reusable scenario fixture. The loop: play → crash → send `console_output.txt` → fix → pull → repeat.

### Open follow-ups from this session
- Build the **automated scenario harness** (mid-game fixture + first feature test, e.g. ship-design stats / space combat) — the breadth half that catches engine bugs without a playthrough.
- **Fix the `Pulsar4x-Testing` mod data** (incomplete Armor/Theme) so colony build doesn't NRE and `NewGameStart_BaseModPlusTestingMod` can be un-`[Ignore]`'d.
- **Armor material missing from starting cargo** — `GetArmorMass` now returns 0; the default armor's material should be in the starting cargo library so armor mass is real.
- Optional: bump DevTools ship-spawn distance so spawned ships are visible at normal zoom.
- Re-enable the commented-out integration fixtures (SystemGen, Serialization, Factory, Mining, SavingAndLoading), one at a time, CI-verified.

---

## Build Status

**CI added 2026-06-22:** `.github/workflows/ci.yml` now runs `dotnet build` + the full NUnit suite on every push/PR via GitHub Actions (Linux runners have the .NET SDK this container lacks). The **first CI run establishes the real build/test baseline** that has been "UNKNOWN" — read the Actions tab. It builds the engine + test project (not the SDL client). New broad sensor: `GameLoopSmokeTests.GameLoop_AdvancesClockWithoutThrowing` (advances the sim clock 3 game-days on a generated universe, asserts no processor throws). **CI run #1 already earned its keep:** build compiled clean, 370/371 tests passed, and the one failure exposed that `DefaultStartFactory.DefaultHumans` is broken (see Known Broken Things) — a path no recent change touched.

**UNKNOWN — .NET is not installed in the cloud execution environment, and CANNOT be installed under the current network policy.**

Cannot run `dotnet build` or `dotnet test` from Claude's remote session. The developer must pull the branch to their Windows machine and build there to establish a baseline.

**Verified 2026-06-21 — .NET SDK install is blocked by network egress allowlist.**
A self-install was attempted (`dotnet-install.sh`). The install script downloads fine (GitHub is allowlisted), but every Microsoft .NET binary host returns **HTTP 403 (egress-blocked)**:
- `builds.dotnet.microsoft.com` — 403 (SDK binaries)
- `ci.dot.net` — 403 (version feed + fallback binaries)
- `dotnetcli.azureedge.net`, `aka.ms` — 403

Allowlisted/reachable: `github.com`, `raw.githubusercontent.com`, `api.nuget.org`, `www.nuget.org`, `dot.net` (redirect only).

**To enable Claude to build/test itself**, the developer must add the dotnet hosts to the environment's network egress settings (see https://code.claude.com/docs/en/claude-code-on-the-web) — at minimum `builds.dotnet.microsoft.com` and `ci.dot.net`. Then a container setup script can `dotnet-install.sh --channel 8.0`. **Even then, running the GAME (UI) still needs the developer** — the container is headless (no display/GPU for SDL2+OpenGL). Do NOT re-test the network each session; this is the confirmed state until the policy changes.

**What we know from reading source (not compiling):**
- The original repo was a working fork; baseline source should compile.
- **Phase 1a code changes are now in** — three C# files modified (see "Pinned Change Points" below).
- This session is the FIRST session with actual C# changes.

**Action for developer:** Pull `claude/laughing-cannon-08obma` and run:
```powershell
dotnet build Pulsar4X/Pulsar4X.sln
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj
```
Paste any errors back. Expected result: build passes; tests pass (no combat tests exist yet).

---

## Test Baseline

**Current baseline (CI 2026-06-24): 382 tests — 381 pass, 1 `[Ignore]`'d, 0 fail, build clean on Linux.** (2026-06-22 was 371.) See the Actions tab. New this session: `EconomyReadoutTests`, `ProductionBuildTests`, `ShipSpawnTests`, `ScenarioHarnessTests`, plus the `TestScenario` harness they ride.

**BUT the real coverage is much thinner than 371 implies — a large share of the integration fixtures are commented out:** `SavingAndLoadingTests`, `SerializationManagerTests`, `SystemGenTests`, `FactoryTests`, `MiningTests` are whole-file `/* ... */`, and `PathfindingTests` has its colony setup commented. So the active suite is mostly **unit-level**: orbital math, vectors, datablob serialization, EntityManager, scheduling/activity-state, modding. Colony creation, system gen (`CreateSol`), mining, and the in-code default start (`DefaultStartFactory.DefaultHumans`, broken) have **no active coverage**.

New coverage added this session: `BaseModIntegrityTests` (base-mod data), `GameLoopSmokeTests` (core sim loop advances without throwing), `SaveLoadSmokeTests` (Game.Save→Load round-trips — **now verified green**), plus two passive read-only **sensors**: `StateIntegritySmokeTests` (asserts every entity position stays a finite number across a clock advance — catches silent NaN/garbage the engine never guards against) and `PerformanceReadoutSmokeTests` (reads the engine's built-in per-processor stopwatch and prints a timing breakdown to the CI log). CI also now emits a per-test TRX report (inline table + downloadable artifact). **No tests for combat, damage, or ground combat.**

**The real path to "catch any crash" coverage is re-enabling/modernizing the commented-out integration fixtures one at a time, CI-verified** — they almost certainly broke (like `DefaultHumans`) when data/APIs were reorganized.

---

## What's Been Done (all sessions to date)

### Prior session docs (on `claude/amazing-clarke-7s118n`)
16 doc files, Aurora reference, hooks, CLAUDE.md files. Commits 1–14.

### Phase 1a — DamageComplex wired (session 2026-06-21)
| File | Change |
|------|--------|
| `GameEngine/Damage/DamageComplex/DamageTools.cs` | TryGetValue guard, DamageResult struct, Beer-Lambert energy model, WavelengthAbsorption |
| `GameEngine/Damage/DamageComplex/DamageProcessor.cs` | OnTakingDamage returns DamageResult; component removal filled in |
| `GameEngine/Weapons/WeaponBeam/BeamWeaponProcessor.cs` | Replaced SimpleDamage with DamageProcessor call |

### System 1 — Weapon range enforced (session 2026-06-21)
| File | Change |
|------|--------|
| `GameEngine/Weapons/IFireWeaponInstr.cs` | Added `IsInRange()` default method |
| `GameEngine/Weapons/WeaponBeam/GenericBeamWeaponAtb.cs` | Added `IsInRange()` override, `BaseHitChance` |
| `GameEngine/Weapons/WeaponBeam/BeamInfoDB.cs` | Added `BaseHitChance` field |
| `GameEngine/Weapons/WeaponBeam/BeamWeaponProcessor.cs` | `CalculateHit()` uses `beamInfo.BaseHitChance` |
| `GameEngine/Weapons/WeaponGeneric/GenericFiringWeaponsProcessor.cs` | Added `IsInRange()` call before `FireWeapon()` |

### Phase 2 — 5 beam weapon decisions implemented (session 2026-06-21 continued)

All 5 decisions wired in one pass per developer's explicit instruction.

| File | Change |
|------|--------|
| `GameData/basemod/TemplateFiles/damageResistance.json` | Added `WavelengthAbsorption` arrays for all 5 materials (UV/Vis/NIR/MIR/FIR) |
| `GameEngine/Damage/DamageComplex/DamageTools.cs` | `DamageResistBlueprint`: `[JsonConstructor]`/`[JsonProperty("UniqueID")]` fix; `WavelengthAbsorption` field; `GetWavelengthAbsorption()` helper; `DealDamageEnergyBeamSim()` fully rewritten (Beer-Lambert, infinite-loop fix, energy decrement, wavelength routing) |
| `GameEngine/Damage/DamageComplex/DamageProcessor.cs` | Health scale fix: `HealthPercent -= damageAmount * 0.001f` |
| `GameEngine/Weapons/WeaponGeneric/WeaponState.cs` | Added `CurrentHeat_kJ`, `HeatCapacity_kJ`, `AllowThermalOverride`, `ThermalOverrideActive` fields + copy constructor updated |
| `GameEngine/Weapons/WeaponBeam/BeamInfoDB.cs` | Added `OptimalRange_m` field |
| `GameEngine/Weapons/WeaponBeam/GenericBeamWeaponAtb.cs` | Added `OptimalRange_m`, `ChargePeriod`, `ThermalOutput_W`, `AllowThermalOverride` fields; 7-arg constructor (4 optional, backward compatible); `OnComponentInstallation()` sets `HeatCapacity_kJ`; `FireWeapon()` passes `OptimalRange_m` |
| `GameEngine/Weapons/WeaponBeam/BeamWeaponProcessor.cs` | `FireBeamWeapon()` accepts `optimalRange_m`; `OnHit()` applies two-zone inverse-square energy scaling; `DamageFragment.Wavelength` set from `beamInfo.Frequency` |
| `GameEngine/Weapons/WeaponGeneric/GenericFiringWeaponsProcessor.cs` | Added `using Pulsar4X.Energy`; fixed `Math.Max` → `Math.Min` reload bug; full thermal suppression + power grid check in `UpdateWeapons()` |
| `GameData/basemod/TemplateFiles/weapons.json` | `genericWpnAtbArgs`: physics-driven formula (Charge Period → reload rate); `genericBeamWpnAtbArgs`: 7 args (adds FocalLength, ChargePeriod, ThermalOutput, override flag) |
| `GameEngine/Weapons/CLAUDE.md` | Full 5-decision status added; damage path decision documented |
| `GameEngine/Damage/CLAUDE.md` | Active path updated (DamageComplex, not SimpleDamage); DamageFragment and DamageResistBlueprint documented |

---

## Pinned Change Points (Next Code Work)

### Phase 1a — COMPLETE
`DamageProcessor.OnTakingDamage()` is the active beam-hit path.

### System 1 — Weapon Range — COMPLETE
`MaxRange` enforced via `IsInRange()`. `BaseHitChance` flows from attribute to `CalculateHit()`.

**Developer action required:** JSON default range is 5000m (space-scale tiny). Set "Range" `PropertyFormula` in `GameData/basemod/TemplateFiles/weapons.json` to something realistic before testing (e.g., `50000000` = 50,000 km).

### Phase 2 — 5 Beam Weapon Decisions — COMPLETE
All 5 decisions wired: two-zone range/energy falloff, wavelength-to-material mapping (Beer-Lambert), thermal management as fire-rate limiter, Charge Period drives fire rate, power grid check. See `Weapons/CLAUDE.md` "Beam Weapon Design" section.

**Developer action required:** Test by running a ship-vs-ship combat scenario. Watch for:
1. Power-starved ships not firing (if `EnergyGenAbilityDB` present but `EnergyStored` runs dry).
2. Thermal suppression kicking in after 2 rapid shots.
3. Beam damage respecting wavelength (FIR = 10000nm hits aluminium for ~18% absorption; same beam hits plastic for 85%).

### Resource/Economy Survey — COMPLETE (session 2026-06-21)

5-agent parallel survey of the entire mineral/material/resource system. Findings committed to `docs/RESOURCES-AND-MATERIALS-DESIGN.md`.

**Key findings:**
- 3-tier production pipeline exists and works (mine → refine → build)
- 3 of 15 minerals have zero recipes (nickel, lithium, rare-earth-elements)
- Processed materials (electronics, etc.) not referenced in any component build costs — refinery chain is decorative
- Trade happens but generates no faction wealth (Ledger is disconnected)
- Enemy ships can freely access your logistics bases (no faction access controls)
- Research costs only money — no material requirements
- NPC economic AI = 0% implemented; all systems are faction-agnostic and would work for NPCs if orders were issued

**Priority order from survey:** (1) Fix Installations tab, (2) Wire processed materials into component costs, (3) Add 3 missing mineral recipes, (4) Connect trade to Ledger, (5) Build NPCDecisionProcessor.

---

### System 2 — Sensor Range + Contact Model — NEXT

**Read `GameEngine/Sensors/CLAUDE.md` first** — sensor infrastructure may already partially exist.

---

### Phase 2a — Fix Installations Tab — DONE (already fixed in code, confirmed 2026-06-24)

`PlanetaryWindow` already gates the Installations tab on `ComponentInstancesDB` and renders via
`componentsDB.Display(...)`. The broader colony economy UI also already exists in `ColonyManagementWindow`
(Summary/Production/Construction/Mining + job-queuing). The only remaining work is a **live verification**
that it all works in the running client (CI is client-blind) — `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` §5B step 7.

---

### Phase 2c — Population Formula

**File:** `Pulsar4X/GameEngine/Colonies/PopulationProcessor.cs`

The stub is at **line 54:** `growthRate = -50.0;` — this fires when pop exceeds `maxPopulation`. This is the placeholder die-off.

**Target formula (from `docs/aurora/COLONY-ENVIRONMENT-AND-POPULATION.md` §2):**
- Growth is normal up to 33% of capacity
- Declines linearly from 33% → 100% capacity (i.e., growth rate × (1 − ((pop/capacity − 0.33) / 0.67)))
- At 100% capacity, growth rate = 0 (not −50%)

The `maxPopulation` calculation on line 49 is already close to the Aurora formula — verify it matches `Infrastructure / (CC × 100)` in millions before changing the growth curve.

---

## Known Broken Things (Don't Touch Without Reading the Doc First)

| Issue | File | Line | Doc |
|-------|------|------|-----|
| `DefaultStartFactory.DefaultHumans` broken — loads Sol via legacy `LoadSystemFromJson("Data/basemod/sol/")` → `systemInfo.json`, but Sol data moved to `ScenarioFiles/systems/sol/sol.json`. Only commented-out tests used it; the live game uses `ColonyFactory.CreateFromBlueprint`. Found by CI via `GameLoopSmokeTests`. | `DefaultStartFactory.cs` | 140 | — |
| ~~Installations tab never appears~~ | ~~PlanetaryWindow.cs~~ | ~~107, 221~~ | **FIXED (verified in code 2026-06-24)** — gates on `ComponentInstancesDB`, renders `componentsDB.Display(...)`. Full colony economy UI also exists in `ColonyManagementWindow`. |
| ~~SimpleDamage placeholder~~ | ~~BeamWeaponProcessor.cs~~ | ~~132–134~~ | **FIXED Phase 1a** — DamageComplex now wired |
| ~~One-hit destroys (units mismatch)~~ | ~~DamageProcessor.cs~~ | — | **FIXED Phase 2** — `HealthPercent -= damageAmount * 0.001f` |
| ~~DamageResistsLookupTable sparse~~ | ~~DamageTools.cs~~| — | **FIXED Phase 2** — `[JsonProperty("UniqueID")]` + `WavelengthAbsorption` arrays |
| ~~Math.Max reload bug~~ | ~~GenericFiringWeaponsProcessor.cs~~ | — | **FIXED Phase 2** — changed to `Math.Min` |
| Off-by-one in ComponentLookupTable indexing | DamageProcessor.cs + ComponentPlacement.cs | G-channel 1-indexed, table 0-indexed | Weapons/CLAUDE.md Damage Status |
| Colony damage block commented out | DamageProcessor.cs | ~101–181 | Damage/CLAUDE.md, root CLAUDE.md Gotcha #8 |
| Thermal override weapon damage not implemented | GenericFiringWeaponsProcessor.cs | override fires but no weapon damage | Weapons/CLAUDE.md Decision 3 |
| Population −50% die-off stub | PopulationProcessor.cs | 54 | COLONY-ENVIRONMENT-AND-POPULATION.md |
| Missile guidance hardcoded false | MissleProcessor.cs | directAttack | root CLAUDE.md Gotcha #3 |

---

## What's In the Game Data Already

**`GameEngine/Data/basemod/blueprints/installations.json`** defines these installation component types (all as `MountType: PlanetInstallation` or similar):

| ID | Name | Notes |
|----|------|-------|
| `mine` | Mine | Mines all resources, `MiningAmountDict` attr |
| `automine` | RoboMiner | Unmanned, transportable |
| `university` | University | Research points, `ResearchPointsAtbDB` attr |
| `refinery` | Refinery | Refines minerals to materials |
| `factory` | Factory | Produces components, installations, ordnance |
| `shipyard` | Ship Yard | Builds ships |
| `logistics-office` | Logistics Office | Import/export, `LogiBaseAtb` attr |
| `fuel-cargo-hold` | Fuel Storage | Also `fuel-tank` variant |
| `naval-academy` | Naval Academy | Graduates officers, `NavalAcademyAtb` attr |
| `spaceport` | Planetary Spaceport Complex | Cargo transfer + storage |
| `infrastructure` | Infrastructure | Population life support on hostile worlds |
| `space-port` | Space Port | Cargo transfer (simpler variant) |

This tells us the Installations tab, once fixed, has real data to display — it won't be empty just because of the render bug.

**Note:** `infrastructure` description says "currently non functional other than as cargospace" — the CC/pop formula tie-in is the Phase 2c work.

---

## What CLAUDE.md Files Are Still Missing

| Subsystem | Directory | Priority | Why It Matters |
|-----------|-----------|----------|----------------|
| Fleets | `GameEngine/Fleets/` | HIGH | Phase 4 — transport ships, landing operations |
| Research/Tech | `GameEngine/Tech/` | MEDIUM | Phase 4 — unlocking ground combat tech |
| Sensors | `GameEngine/Sensors/` | LOW | Benchmark; not on critical path |
| Orbits | `GameEngine/Orbits/` | LOW | Already well-understood |
| Galaxy/System Gen | `GameEngine/Galaxy/` | LOW | Not on critical path |
| Logistics | `GameEngine/Logistics/` | MEDIUM | Phase 4 — supply lines, GSP |

**Fleets CLAUDE.md** was written this session (see `GameEngine/Fleets/CLAUDE.md`).

---

## Environment Notes

### Claude's execution environment
Remote Linux cloud container. **No .NET SDK installed.** Claude edits files here; the developer pulls and builds on their own machine.

### CRITICAL — Session repository scope must be `4x-Game`, not `Pulsar4x`

The Claude Code remote session must be started with **`kadonomaro197-cloud/4x-Game`** as the authorized repository. If started with `kadonomaro197-cloud/Pulsar4x` (which doesn't exist on GitHub), the git proxy blocks all pushes with "repository not authorized" and MCP GitHub tools return "Access denied." This cannot be fixed mid-session.

**How to verify the session scope** (run this at the start of any new session):
```bash
cat /home/claude/.claude/remote/.session_ingress_token
```
Look for `"sources"` in the JWT payload. It must contain `kadonomaro197-cloud/4x-Game`. If it shows `kadonomaro197-cloud/Pulsar4x`, the session is misconfigured — end it and start a new one.

**If a session IS misconfigured and you have uncommitted or unpushable work:**
```bash
git format-patch origin/HEAD..HEAD --stdout > /home/user/all-work.patch
```
Then use `SendUserFile("/home/user/all-work.patch")` to deliver the patch. The user applies it on their Windows machine:
```powershell
# In local 4x-Game clone
git checkout -b claude/amazing-clarke-7s118n
git am < all-work.patch
git push -u origin claude/amazing-clarke-7s118n
```

**Correct session configuration:**
- Repo 1: `kadonomaro197-cloud/4x-Game`
- Repo 2: `kadonomaro197-cloud/AiD-Main`

### Developer's machine
| Item | Value |
|------|-------|
| OS | Windows |
| Shell | **PowerShell** — all commands given to the developer must be PowerShell-compatible |
| GPU | NVIDIA RTX 3090 FTW3 Ultra — 24 GB VRAM |
| CPU | AMD Ryzen 7 5800X3D — 8 cores / 16 threads |
| RAM | 32 GB |
| PSU | 850 W |

### Workflow for code changes
Claude writes change → developer runs in PowerShell from repo root:
```powershell
git pull origin claude/amazing-clarke-7s118n
dotnet build Pulsar4X/Pulsar4X.sln
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj
```
If build/test fails → paste error output here → Claude fixes → repeat.
