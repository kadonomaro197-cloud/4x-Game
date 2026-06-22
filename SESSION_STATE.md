# Session State — Current Known Status

This document tracks what we know about the codebase state at the close of each session. A new session reads this first after the CLAUDE.md files, to skip rediscovery.

---

## Last Updated

Session ending ~2026-06-21 (survey complete). Branch: `claude/laughing-cannon-08obma`.

---

## Build Status

**CI added 2026-06-22:** `.github/workflows/ci.yml` now runs `dotnet build` + the full NUnit suite on every push/PR via GitHub Actions (Linux runners have the .NET SDK this container lacks). The **first CI run establishes the real build/test baseline** that has been "UNKNOWN" — read the Actions tab. It builds the engine + test project (not the SDL client). New broad sensor: `GameLoopSmokeTests.DefaultStart_AdvancesClockWithoutThrowing` (creates the default start, advances the clock 3 game-days, asserts no processor throws).

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

**UNKNOWN** for the same reason — cannot run `dotnet test` remotely.

From reading the test project structure: tests exist for EntityManager, orbits, save/load, mining, population, pathfinding. **No tests for combat, damage, or ground combat.**

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

### Phase 2a — Fix Installations Tab (FIRST UI TASK)

**File:** `Pulsar4X/Pulsar4X.Client/Interface/Windows/PlanetaryWindow.cs`

Two broken lines to fix:
- **Line 107:** `if (_lookedAtEntity.Entity.HasDataBlob<InstallationsDB>())` — this is the tab's render gate; it's always false because `InstallationsDB` is never attached to any colony. Change to `HasDataBlob<ComponentInstancesDB>()`.
- **Line 221:** `if (_lookedAtEntity != null && _lookedAtEntity.Entity.HasDataBlob<InstallationsDB>())` — same dead gate inside `RenderInstallations()`. Change to `ComponentInstancesDB`, then implement the render body using `ComponentInstancesDB.DesignsAndComponentCount` (a dict of design entity → count).

The `ComponentInstancesDBDisplay` panel already exists in the client — search for it and reuse rather than writing a new table.

**Read before touching:** `GameEngine/Colonies/CLAUDE.md`, `CONVENTIONS.md` §6.

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
| Installations tab never appears | PlanetaryWindow.cs | 107, 221 | Colonies/CLAUDE.md Gotcha #1 |
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
