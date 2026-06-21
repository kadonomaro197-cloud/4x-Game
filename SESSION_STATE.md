# Session State — Current Known Status

This document tracks what we know about the codebase state at the close of each session. A new session reads this first after the CLAUDE.md files, to skip rediscovery.

---

## Last Updated

Session ending ~2026-06-21. Branch: `claude/laughing-cannon-08obma` (working branch; `claude/amazing-clarke-7s118n` contained prior docs-only work).

---

## Build Status

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

## What's Been Done This Session (2026-06-21)

| File | Status |
|------|--------|
| `GameEngine/Damage/DamageComplex/DamageTools.cs` | **Modified** — added `TryGetValue` guard in `DealDamageEnergyBeamSim()` to skip unknown material IDs instead of throwing `KeyNotFoundException` |
| `GameEngine/Damage/DamageComplex/DamageProcessor.cs` | **Modified** — added `DamageResult` struct, changed `OnTakingDamage` to return `DamageResult`, filled the empty component-removal block (finds destroyed components by design ID match, removes via `RemoveComponentInstance`, destroys ship when all components gone) |
| `GameEngine/Weapons/WeaponBeam/BeamWeaponProcessor.cs` | **Modified** — replaced `SimpleDamage.OnTakingDamage(100, 500)` with corrected `DamageFragment` construction + `DamageProcessor.OnTakingDamage()` call |
| `GameEngine/Weapons/CLAUDE.md` | Updated — Damage Status section reflects Phase 1a completion and known calibration issues |
| `docs/COMBAT-DESIGN.md` | **Created** — generalized combat build plan: hardware constraints, LOD model, 11 required systems, build order |
| `SESSION_STATE.md` | This file |

**Prior session docs** (on `claude/amazing-clarke-7s118n`): 16 doc files, Aurora reference, hooks, CLAUDE.md files. These are on the branch as commits 1–14.

---

## Pinned Change Points (Next Code Work)

### Phase 1a — COMPLETE

`DamageProcessor.OnTakingDamage()` is now the active beam-hit path. See `GameEngine/Weapons/CLAUDE.md` Damage Status for the three known calibration issues that are tracked but intentionally deferred.

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
| Off-by-one in ComponentLookupTable indexing | DamageProcessor.cs + ComponentPlacement.cs | G-channel 1-indexed, table 0-indexed | Weapons/CLAUDE.md Damage Status |
| One-hit destroys (units mismatch) | DamageProcessor.cs | damage.damageAmount=1, HealthPercent starts 1.0f | Weapons/CLAUDE.md Damage Status |
| DamageResistsLookupTable sparse | DamageTools.cs + damageResistance.json | JSON field name mismatch | Weapons/CLAUDE.md Damage Status |
| Colony damage block commented out | DamageProcessor.cs | ~101–181 | Damage/CLAUDE.md, root CLAUDE.md Gotcha #8 |
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
