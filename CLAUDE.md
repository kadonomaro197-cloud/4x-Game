# Pulsar4X — Developer Reference

**Pulsar4X** is an open-source C# fan recreation of the 4X space sim Aurora 4x. Entry point: `Pulsar4X/Pulsar4X.Client/Program.cs`. Central game loop: `GameEngine/Engine/MasterTimePulse.cs`.

---

## Communicating With the Developer (READ FIRST — applies every session)

The developer is hands-on technical (US Navy nuclear-trained machinist's mate) but **not a professional software engineer**. Explain in **plain English**, not software jargon. This applies to chat replies *and* to docs you write.

- **Lead with what it does and why it matters**, then how it works. The point before the plumbing.
- **Spell out and define jargon on first use.** "DataBlob (a container that holds one kind of data for a game object)", not just "DataBlob". Avoid unexplained acronyms.
- **Use analogies** — mechanical, electrical, thermodynamic, shipboard analogies land well (e.g. "a processor is like a watch station that does one job every shift").
- **Don't dumb down the substance — simplify the language.** He can follow reactor physics; he just isn't fluent in code vocabulary. Keep the engineering honest; lose the CS lingo.
- **Short sentences. Concrete examples. Name the file and what it's for**, not abstract patterns.
- When something genuinely is complex, say so plainly and walk through it step by step rather than hiding it behind a term.

---

## Developer Objective (this fork)

Extend the fork so that **planetary/ground combat and planetary infrastructure eventually have the same depth that space combat already has**, and improve the UI. Survey priority: understand how space combat is built so it can be mirrored for ground systems, where ground/planetary systems currently live or would hook in, and how the UI layer is constructed.

---

## Project / Solution Layout

Solution file: `Pulsar4X/Pulsar4X.sln`

| Project | Directory | Role |
|---------|-----------|------|
| **GameEngine** | `Pulsar4X/GameEngine/` | Core game logic, data model, all processors. The heart of the game. |
| **Pulsar4X.Client** | `Pulsar4X/Pulsar4X.Client/` | ImGui.NET + SDL2 UI — the only runnable application. |
| **Pulsar4X.OrbitalMath** | `Pulsar4X/Pulsar4X.OrbitalMath/` | Standalone orbital mathematics library (`Pulsar4X.Orbital` namespace). Referenced by GameEngine. |
| **Pulsar4X.Tests** | `Pulsar4X/Pulsar4X.Tests/` | NUnit 3 test suite, references GameEngine directly. |
| **Benchmarks** | `Pulsar4X/Benchmarks/` | BenchmarkDotNet performance benchmarks. |
| **ViewModelLib** | `Pulsar4X/ViewModelLib/` | Legacy ViewModel/GL library from an older WPF frontend. Not referenced by the current client — ignore for new work. |

---

## Tech Stack

| Item | Value |
|------|-------|
| .NET target | net8.0 (all projects) |
| UI framework | ImGui.NET 1.88.0 + SDL2-CS |
| Rendering | OpenGL via SDL2 |
| Serialization | Newtonsoft.Json 13.0.3 (`PreserveReferencesHandling.Objects`, `TypeNameHandling.Objects`) |
| Math expressions | CoreCLR-NCalc 2.2.113 (used in component design formulas) |
| Tests | NUnit 3.13.3 + NUnit3TestAdapter |
| Benchmarks | BenchmarkDotNet 0.14.0 |
| Mod data | JSON files under `GameEngine/Data/basemod/` |

---

## Developer Machine

The developer runs **Windows** with **PowerShell** as their shell. All commands given to the developer must be PowerShell-compatible. `dotnet` CLI commands are identical on Windows and Linux — paths with forward slashes work fine in PowerShell.

| Component | Spec |
|-----------|------|
| OS | Windows |
| Shell | PowerShell |
| GPU | NVIDIA RTX 3090 FTW3 Ultra (24 GB VRAM) |
| CPU | AMD Ryzen 7 5800X3D (8 cores / 16 threads) |
| RAM | 32 GB |
| PSU | 850 W |

**Claude's execution environment is a remote Linux cloud container** — separate from the developer's machine. Claude edits code in the cloud container; the developer pulls the branch and builds/runs on their Windows machine.

---

## Build / Run / Test Commands

> **Cloud environment note:** `.NET SDK is NOT installed in the remote Claude Code execution environment.` Claude can read and edit C# files but cannot compile or run tests remotely. **Workflow:** Claude writes the change → developer pulls branch in PowerShell → builds/tests locally → pastes any errors back → Claude fixes. See `SESSION_STATE.md` for current build/test baseline.

> **CI (added 2026-06-22):** `.github/workflows/ci.yml` runs `dotnet build` + the full NUnit suite on **every push and pull request**, on GitHub's Linux runners (which have the .NET SDK this container lacks). This is the automated gate that catches a regression in *any* engine system on every change — including one a given commit didn't touch — without relying on anyone remembering to run tests. It builds the engine + test project and runs all tests; the SDL/OpenGL client is **not** built in CI (Windows/display-coupled — built locally when running the game). A red ✗ on a commit/PR means build or a test broke — check it before promoting. The broadest sensor it runs is `GameLoopSmokeTests` (advances the simulation clock on a generated universe, asserts no processor throws). Two more passive, read-only sensors run alongside it: `StateIntegritySmokeTests` (after advancing the clock, asserts every entity's position is a finite number — catches silent NaN/garbage a throw-check can't see, since the engine has no NaN guards) and `PerformanceReadoutSmokeTests` (reads the engine's built-in per-processor stopwatch and prints a timing breakdown into the CI log). CI also publishes a per-test report (TRX) as an inline results table and a downloadable artifact, so a red run shows exactly which test broke and why — not just a count. All sensors only read game state; none modify the simulation.
>
> **CI is structurally blind to the SDL client** — it never compiles the UI, so client build errors and runtime crashes never surface here; the developer's local build is the only client check. The compensating tool is `launch.bat`: it captures all console output (stdout+stderr) to `console_output.txt` and keeps the window open on crash — the client's diagnostic channel. Live-test loop: play → reproduce → read/send `console_output.txt` → fix → pull → repeat. Full live-test play-by-play, session log, and lessons learned (2026-06-22) are in `SESSION_STATE.md`.

All commands run from the repo root in **PowerShell on the developer's Windows machine**:

```powershell
# Build entire solution
dotnet build Pulsar4X/Pulsar4X.sln

# Run the game
dotnet run --project Pulsar4X/Pulsar4X.Client/Pulsar4X.Client.csproj

# Run tests
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj

# Run tests with output
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj --logger "console;verbosity=detailed"

# Run benchmarks (Release mode required)
dotnet run --project Pulsar4X/Benchmarks/Benchmarks.csproj -c Release
```

**SDL2 on Windows:** The SDL2 native library must be available when running the client. NuGet should handle this automatically via the SDL2-CS package — if the game fails to start with a DLL error, download `SDL2.dll` from libsdl.org and place it next to the built executable.

---

## Architecture Pattern: Hybrid ECS

Pulsar4X uses a **hybrid Entity-Component-System (ECS)** pattern:

- **Entity** (`Engine/Entities/Entity.cs`): A lightweight container with an integer `Id`. All data is stored in DataBlobs attached through the EntityManager.
- **DataBlob** (`Engine/Datablobs/BaseDataBlob.cs`): The "Component" — a strongly-typed C# class holding state. Named with a `DB` suffix. Attached to and retrieved from entities by type.
- **Processor** (`Engine/Processors/`): The "System" — processes all entities with a given DataBlob type. Two kinds:
  - `IHotloopProcessor`: Runs on a fixed game-time frequency for all entities in a manager (e.g., every 1 second game-time). Auto-discovered via reflection.
  - `IInstanceProcessor`: Runs at a specific scheduled instant for specific entities (interrupt-driven). Auto-discovered via reflection.
- **EntityManager** (`Engine/Entities/EntityManager.cs`): Owns a set of entities and their DataBlobs. `StarSystem` extends it to represent a solar system.
- **MasterTimePulse** (`Engine/MasterTimePulse.cs`): The global game loop driver. Steps all `StarSystem.ManagerSubpulses` forward in time, optionally in parallel.
- **ManagerSubPulse** (`Engine/ManagerSubPulse.cs`): Per-system scheduler that maintains two queues — a frequency-based HotLoop queue and a time-indexed instance queue.

See `ARCHITECTURE.md` for the full data-flow diagram.

---

## Subsystem Index

| Subsystem | Directory | CLAUDE.md |
|-----------|-----------|-----------|
| Game Engine Core | `GameEngine/Engine/` | `GameEngine/CLAUDE.md` |
| Space Combat (Weapons) | `GameEngine/Weapons/` | `GameEngine/Weapons/CLAUDE.md` |
| Damage | `GameEngine/Damage/` | `GameEngine/Damage/CLAUDE.md` |
| Colonies / Population | `GameEngine/Colonies/` | `GameEngine/Colonies/CLAUDE.md` |
| Industry / Production | `GameEngine/Industry/` | `GameEngine/Industry/CLAUDE.md` |
| Movement / Navigation | `GameEngine/Movement/` | `GameEngine/Movement/CLAUDE.md` |
| Sensors | `GameEngine/Sensors/` | `GameEngine/Sensors/CLAUDE.md` |
| Orbits | `GameEngine/Orbits/` + `Pulsar4X.OrbitalMath/` | *(not yet written — read source directly)* |
| Galaxy / System Gen | `GameEngine/Galaxy/` | `GameEngine/Galaxy/CLAUDE.md` |
| Fleets | `GameEngine/Fleets/` | `GameEngine/Fleets/CLAUDE.md` |
| Logistics | `GameEngine/Logistics/` | `GameEngine/Logistics/CLAUDE.md` |
| Research/Tech | `GameEngine/Tech/` | `GameEngine/Tech/CLAUDE.md` |
| Factions | `GameEngine/Factions/` | `GameEngine/Factions/CLAUDE.md` |
| People / Commanders | `GameEngine/People/` | `GameEngine/People/CLAUDE.md` |
| UI Client | `Pulsar4X.Client/` | `Pulsar4X.Client/CLAUDE.md` |
| Tests | `Pulsar4X.Tests/` | *(see Testing section below)* |

---

## Design & Convention References

| Doc | Read it when |
|-----|--------------|
| `CONVENTIONS.md` | **Before writing any new code.** Pulsar's actual coding idioms (DataBlob copy-ctor/`Clone()`, serialize-one-collection-rebuild-indexes, `TryGet`/sentinel defensiveness, components+`*Atb`, processor auto-discovery). Match these, don't impose your own style. |
| `docs/aurora/INDEX.md` | Designing any ground-combat or infrastructure mechanic. Aurora 4X is the design spec for systems Pulsar lacks. |
| `docs/aurora/GROUND-COMBAT.md` | Building ground forces, formations, invasion, bombardment-vs-ground. |
| `docs/aurora/PLANETARY-INFRASTRUCTURE.md` | Adding installations, construction, economy depth. |
| `docs/aurora/SPACE-COMBAT-BENCHMARK.md` | Calibrating "the same depth space combat has." |
| `docs/RESOURCES-AND-MATERIALS-DESIGN.md` | Designing anything touching minerals, materials, production, commerce, research, or NPC economic AI. Full system survey — read before changing any part of the economy. |
| `docs/DIPLOMACY-DESIGN.md` | Designing anything touching faction relationships, IFF, inter-faction trade, logistics access, NPC doctrine, or diplomatic state. Full system survey — read before adding any cross-faction interaction. |
| `docs/COMBAT-DESIGN.md` | The master space-combat design: the eleven required systems (weapon range → auto-resolution → ground-combat interface). Includes the detailed **Fleet Components & Switchable Doctrine** design (Front Line/Flank/Rear Guard/Artillery as sub-fleets, Offensive/Defensive/Utilitarian options, switch cooldown, commander operational discretion, table-based fleet combat UI) under System 4. Read before any combat-system work. |

---

## Key Constants and Conventions

### Naming Conventions
| Pattern | Meaning | Example |
|---------|---------|---------|
| `*DB` suffix | DataBlob (component) | `ColonyInfoDB`, `BeamInfoDB` |
| `*Processor` suffix | HotloopProcessor or IInstanceProcessor | `BeamWeaponProcessor` |
| `*Atb` suffix | Component design attribute | `GenericBeamWeaponAtb` |
| `*Order` / `*Command` suffix | Player-issued order | `SetFireControlOrder`, `NewtonThrustCommand` |
| `*Factory` suffix | Entity creation helper | `ColonyFactory`, `ShipFactory` |
| `*Blueprint` suffix | JSON-loadable data template | `ComponentDesignBlueprint` |

### Code Conventions Observed
- `[JsonProperty]` on all fields that must survive save/load.
- `[JsonIgnore]` on runtime-only references (Manager, Game, etc.).
- `async void` used (not `async Task`) on `EntityManager` mutation methods — be aware this swallows exceptions.
- DataBlobs implement `Clone()` for when they are moved between managers (e.g., ship jumping systems).
- `SafeDictionary<K,V>` and `SafeList<T>` are thread-safe wrappers used for shared collections.
- `NullReferenceException` from `#nullable enable` warnings suppressed with `NoWarn>0649` in project files — nullable annotations exist but are unenforced in many places.

---

## Testing

All tests are NUnit 3 in `Pulsar4X.Tests/`. Run with `dotnet test`.

Coverage includes: EntityManager, DataBlobs, orbits (including fuzz testing), save/load, ship components, mining, population processor, pathfinding, system generation, serialization.

**No tests exist for space combat (weapons, damage, fire control) or ground combat.** Any new combat system must add tests.

Test utilities live in `TestHelper.cs` and `TestingUtilities.cs`.

---

## Critical Gotchas

1. **Damage path decision is made and fully wired.** `DamageComplex` is the forward direction. `SimpleDamage` is dead code. Beam hits go: `BeamWeaponProcessor.OnHit()` → `DamageProcessor.OnTakingDamage()` → `DealDamageEnergyBeamSim()`. Colony hits route to `OnColonyDamage()` (population + atmospheric + installation damage, all wired). Asteroid hits use `DamageVeryComplex`. See `Damage/CLAUDE.md`.

2. **ProcessorManager auto-discovers via reflection.** Any class implementing `IHotloopProcessor` or `IInstanceProcessor` is automatically registered on startup by `ProcessorManager.CreateProcessors()`. You do not register processors manually. The trade-off: a broken processor crashes startup.

3. **Missile guidance is functional as of 2026-06-21.** `directAttack` is now `true` in `MissleProcessor.cs`. Missiles use `ThrustToTargetCmd` (direct pursuit), not phasing maneuvers. `MissileImpactProcessor` (new `IHotloopProcessor`) checks proximity every second and delivers kinetic damage on impact (≤ 1000 m). Calibration note: kinetic energy at orbital closing speeds is GJ-scale, well above the kJ–MJ beam damage tuning — ships will be one-shot. Tune `MissileImpactProcessor.ImpactRadius_m` or the energy divisor once warhead energy values are finalized. See `Weapons/CLAUDE.md` for full status.

4. **PlanetaryWindow.RenderInstallations() renders nothing — and the bug is worse than "empty body."** The method is gated on `HasDataBlob<InstallationsDB>()`, but **`InstallationsDB` is never attached to any colony** (verified: `ColonyFactory` and `DefaultStartFactory` only ever call `AddComponent(...)`). So the "Installations" tab button never even appears (`PlanetaryWindow.cs:107`). `InstallationsDB` is **vestigial/abandoned** (no `[JsonProperty]` fields, only dead-code references). Real installations are **components in `ComponentInstancesDB`** — the colony's `AddComponent(mineDesign)`, `AddComponent(facEntity)`, etc. The correct fix renders from `ComponentInstancesDB` (a `ComponentInstancesDBDisplay` panel already exists), not from `InstallationsDB`. See `docs/aurora/PLANETARY-INFRASTRUCTURE.md` and `CONVENTIONS.md` §6.

5. **`async void` on EntityManager mutations swallows exceptions.** `AddEntity`, `TagEntityForRemoval`, `SetDataBlob`, `RemoveDatablob` are all `async void` (needed for `MessagePublisher.Publish`). Any exception inside propagates to the thread pool and is unobservable. Keep mutation code minimal and well-tested.

6. **ViewModelLib is dead weight.** `ViewModelLib/` contains WPF-style ViewModel and OpenGL abstractions from a prior frontend. Nothing in the current `Pulsar4X.Client` references it. Do not add new code there.

7. **Save/load uses TypeNameHandling.Objects.** This means the JSON save file embeds C# type names. Renaming or moving a DataBlob class will break existing saves. When refactoring, add a `[JsonConverter]` or migration step.

8. **Colony orbital bombardment damage is wired and missiles now deliver it.** `DamageProcessor.OnColonyDamage()` handles population casualties, atmospheric contamination, and random installation damage when a colony entity takes a hit. `MissileImpactProcessor` calls `DamageProcessor.OnTakingDamage()` on impact, which routes colony hits to `OnColonyDamage()`. Missile guidance fixed 2026-06-21 (`directAttack = true`, `ThrustToTargetCmd`). Ground combat orbital support is now unblocked on the missile side.

9. **Claude Code remote sessions MUST be configured with `kadonomaro197-cloud/4x-Game`, not `Pulsar4x`.** The git proxy enforces the session's repository scope. If the session was started with the wrong repo name (`Pulsar4x` doesn't exist on GitHub — the actual game repo is `4x-Game`), every `git push` returns "repository not authorized" and MCP GitHub tools return "Access denied." There is no fix mid-session; the work cannot be pushed until a new session is started with the correct repo name. Verify the scope by checking the session ingress token: `cat /home/claude/.claude/remote/.session_ingress_token` — the JWT payload's `"sources"` field shows the authorized repos. The correct session configuration is `kadonomaro197-cloud/4x-Game` + `kadonomaro197-cloud/AiD-Main`. If you find yourself blocked, generate a patch with `git format-patch origin/HEAD..HEAD --stdout > /home/user/all-work.patch` and send it to the user via `SendUserFile` — all work is preserved and can be applied to a local clone with `git am`.

10. **The test suite does NOT exercise the New Game JSON path — so JSON data drift crashes players, not `dotnet test`.** The starting colony is built **two different ways**: the tests build it in C# (`DefaultStartFactory.DefaultHumans`), but the **game** builds it from JSON (`ColonyFactory.CreateFromBlueprint` reading `GameData/.../sol/earth.json` + the template JSON). Because the JSON path had no test, three data bugs shipped green and only blew up on New Game / Quickstart: (a) `electronics`/`ree-magnetics` were added as costs to starting designs but never added to the colony's `StartingItems` (crash — fixed); (b) `DamageResistBlueprint`'s `[JsonProperty("UniqueID")]` left the string key null (crash — fixed); (c) `ordnance.json` references `gallicite`, which is **not defined** as a mineral/material (latent — flagged). **Guardrail in place:** `Pulsar4X.Tests/Modding/BaseModIntegrityTests.cs` reproduces the real unlock logic and asserts (a) every starting colony can build all its starting designs, and (b) the base mod loads with **zero skipped entries** — a null/empty `UniqueID` no longer just skips silently; `ModLoader.SkippedEntries` records it so the test fails, closing the gap where the skip-guard hid the DamageResist null-key bug from the suite. **Rule — apply the Prime Directive to DATA, not just code:** when you add or change a cost/requirement/reference in any `GameData/**` JSON, check the *other end* in the same change — (1) the referenced id is actually **defined** (mineral/material/tech/component), and (2) if a *starting* design needs it, the material is in that colony's **`StartingItems`** (or unlocked by a starting tech). Then run `dotnet test` — `BaseModIntegrityTests` is your sensor.

---

## The Prime Directive — Map the Connections First

**Before making any decision about any system — combat, economy, UI, damage, population, anything — stop and map every connection that system has. Then look further.**

This is not optional and it is not just for complex tasks. A change that looks local almost never is. The codebase is deeply interconnected: DataBlobs feed processors that write to other DataBlobs that trigger events that schedule more processors. Pulling one thread without knowing what it's attached to breaks things in places you weren't looking.

**The four questions to answer before touching anything:**

1. **What feeds INTO this system?** — What DataBlobs, processors, events, or JSON data does it read? If the input contract changes, what upstream provider breaks?
2. **What does this system feed INTO?** — Who reads its output? What processor, what UI panel, what event consumer? If the output contract changes, what downstream consumer breaks?
3. **What shares STATE with this system?** — Same DataBlob, same entity, same global table, same JSON file? A shared-state partner doesn't call you — it just reads the same memory.
4. **What does this system TRIGGER?** — Events published, processors scheduled, orders issued. Follow the chain one more step than feels necessary.

**Then look further.** The answer to those four questions is the minimum scope. The real scope is often one hop wider. Ask: does anything in that downstream system have the same four connections? If the answer surprises you, document it before writing a line of code.

**This applies to every subsystem listed in the Subsystem Index, every system in `docs/COMBAT-DESIGN.md`, every column in the game.** Economy connects to industry connects to population connects to research connects to ship production connects to fleet strength. Sensors connect to contact model connects to IFF connects to doctrine connects to auto-resolution. None of these are islands.

---

## The Visibility Gate — "Can We See Enough?"

**You cannot control what you cannot measure.** You can't run a steam plant without the pressures, temperatures, flow rates, and ratings — and a picture of what *normal* looks like. The same is true here: you cannot safely change a system you cannot observe.

**The rule — when a fix fails on the first try, STOP.** Do not pile on a second guess. Ask one question first:

> **"Can we see enough?"**

If the answer is no — the failure is hidden, the error is swallowed, the state is invisible, there is no baseline for "normal" — then **building the visibility IS the task.** Add the gauge, capture the log, write the read-back, establish the baseline. *Then* resume the fix. This is not a detour from the work; it is the work.

Proven 2026-06-22: every bug fixed that day was caught because the gauge was built first (CI, the passive sensors, the `launch.bat` console capture, the DevTools action logging). The failures that stayed mysterious were the ones whose errors were swallowed where nothing could read them.

**Corollaries:**
- A caught exception with no log is an invisible bug — route it somewhere captured.
- "It didn't work" is not diagnosable; "here is the gauge reading when it didn't work" is. Instrument before you theorize — live data beats speculation.
- Before changing a system, be able to state its *normal*: its inputs, expected outputs, ratings/limits. If you can't, read the docs and the source until you can. A plan built on a skim is a guess.

Pairs with the Prime Directive: **map the connections, then make sure you can see them move.**

---

## How to Work in This Repo (Working Agreement)

1. **Apply the Prime Directive.** Map connections before making decisions. See above.
2. **Read `CONVENTIONS.md` before writing any code; read the subsystem `CLAUDE.md` before working on that subsystem.** Only read source directly when the doc is insufficient, then update the doc after. For ground-combat/infrastructure design questions, consult `docs/aurora/`.
3. **Keep all CLAUDE.md files current** whenever code changes — update the subsystem CLAUDE.md in the same commit as the code it describes. Stale docs are worse than no docs.
4. **Build and run tests before and after every change.** Never leave the build broken. `dotnet build` + `dotnet test` before pushing.
5. **Match existing conventions** (naming, `[JsonProperty]` discipline, `SafeDictionary`, processor auto-discovery pattern).
6. **Add tests for new systems.** Space combat has no tests; do not compound this pattern.
7. **Do not add features beyond what the task requires.** This is an ambitious codebase — scope creep compounds quickly.
8. **Update ARCHITECTURE.md** when data flow changes.
9. **Update this root CLAUDE.md** when a new subsystem is added, a subsystem moves, or a new gotcha is discovered.
