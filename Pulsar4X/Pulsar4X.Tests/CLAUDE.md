# Pulsar4X.Tests — Testing Reference

NUnit 3 test project. Runs in CI (`.github/workflows/ci.yml`) on every push/PR, and locally via `dotnet test`.

> **CI builds the engine + tests only — NOT the SDL client.** Client (UI) bugs are invisible here; they surface only in the developer's local build / the `launch.bat` console loop. See root `CLAUDE.md` → "The Visibility Gate" and the CI note.

---

## How to run

```powershell
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj
dotnet test Pulsar4X/Pulsar4X.Tests/Pulsar4X.Tests.csproj --logger "console;verbosity=detailed"   # per-test output
```
CI runs the full suite automatically and publishes a per-test TRX report (inline table + downloadable artifact), so a red run names the exact failing test.

**Mod data at runtime:** the `CopyJsonData` target in the `.csproj` copies `..\GameData\**` to `bin\...\Data\`, so tests load the real game data via relative paths like `"Data/basemod/modInfo.json"` and `"Data/testingmod/modInfo.json"`.

---

## The Scenario Harness — `TestScenario` (build feature tests on this)

`TestScenario` is the reusable engine-level mid-game state. It stands up a REAL faction + colony the way the live game does, so feature tests (economy, combat, ground) don't re-derive a start each time. **Engine-only → runs in CI.** It is the engine's *gauge board*: build a known state, advance the clock, read/assert.

### Use it
```csharp
var s = TestScenario.CreateWithColony();        // base mod, New Game wizard defaults
// handles: s.Game, s.Faction, s.Species, s.StartingSystem, s.StartingBody, s.Colony
s.QueueProductionJob("space-crete", repeat: true); // optional: give the factory/refinery work to do
s.AdvanceTime(TimeSpan.FromDays(365));           // step granularity defaults to 1 game-day
```

`QueueProductionJob(designId, count, repeat)` is the **job lever**: the factory/refinery do nothing until a job is queued. It looks the design up in the faction's unlocked `IndustryDesigns` (refined materials like `space-crete`/`stainless-steel`, and component/installation designs), builds an `IndustryJob`, and adds it to the production line that handles its industry type. A "refining" material needs its mineral inputs present (mined or in starting cargo) or the job sits at `MissingResources` — check the job Status in the industry readout.

### Feature-test pattern (read → advance → read → assert the delta)
```csharp
var s = TestScenario.CreateWithColony();
// read STARTING gauges off s.Colony (cargo, installed components, deposits) ...
s.AdvanceTime(TimeSpan.FromDays(365));
// read ENDING gauges and assert/report the deltas (X mined, Y refined, Z built) ...
```
*(The exact cargo / mineral read API is established by the first economy readout test — link it here when it lands.)*

### How it's built (mirrors `NewGameMenu.CreateGameCore`)
load mods → pick New Game defaults (first `CanStartHere` body / first species / first colony) → `GameFactory.CreateGame` → `StarSystemFactory.LoadFromBlueprint` per enabled system → find the body by matching `GetDefaultName()` to the blueprint → `FactionFactory.CreateBasicFaction` → `SpeciesFactory.CreateFromBlueprint` → `ColonyFactory.CreateFromBlueprint` → **`startingSystem.IncrementExternalObserver(priority: true)`** (promote out of Stasis — see gotcha 6). A build failure logs the full exception (`ex.ToString()`) to the test output, then rethrows.

### Extend it
- **Expose another entity:** capture it in `CreateWithColony` and add a `public ... { get; private set; }` handle.
- **Add a precondition helper:** e.g. an `AddMine(s.Colony)` that attaches a mine component before `AdvanceTime`, so the economy actually does work to measure.
- **Use the verified namespace map below** when referencing engine types — this is where blind edits go wrong.

### Verified API + namespaces (the harness's dependencies)
Namespace drift between branches is the #1 compile trap here (it bit `PositionDB` and `MassVolumeDB`). Verify a type is **imported**, not just that it exists:

| Type / API | Namespace |
|---|---|
| `Game`, `StarSystem`, `Entity`, `NewGameSettings`, `GameFactory.CreateGame(ModDataStore, NewGameSettings)` | `Pulsar4X.Engine` |
| `ModDataStore`, `ModLoader.LoadModManifest(path, store)` | `Pulsar4X.Modding` |
| `FactionFactory.CreateBasicFaction(game, name, abbr, funds)`, `FactionInfoDB` | `Pulsar4X.Factions` |
| `SpeciesFactory.CreateFromBlueprint(system, speciesBlueprint)` | `Pulsar4X.People` |
| `ColonyFactory.CreateFromBlueprint(game, faction, species, system, body, colonyBlueprint)` → `Entity` | `Pulsar4X.Colonies` |
| `StarSystemFactory.LoadFromBlueprint(game, systemBlueprint)` → `StarSystem`; `SystemBodyInfoDB`; `MassVolumeDB` | `Pulsar4X.Galaxy` |
| `PositionDB` (**active class is in Movement, not Datablobs**) | `Pulsar4X.Movement` |
| `GetDefaultName()` (Entity extension) | `Pulsar4X.Extensions` |
| `CargoStorageDB` | `Pulsar4X.Storage` |
| `Mineral`, `ProcessedMaterial` | `Pulsar4X.Industry` |

---

## Gotchas (hard-won)

1. **`CreateWithColony(base + testing mod)` throws** — the testing mod ships incomplete Armor/Theme data and NREs in colony build (it adds no species/colony). That variant is `[Ignore]`'d in `NewGameStartSmokeTests`. **Base mod alone is the working baseline.** Re-enable once the testing-mod data is fixed.
2. **`AdvanceTime` is single-threaded and sets `Ticklength`.** It sets `Game.Settings.EnforceSingleThread = true` (so a processor throw surfaces on the test thread, not the thread pool) and `Game.TimePulse.Ticklength` (default 1 day; the per-system scheduler catches up sub-daily processors within each step). Pass a smaller `step` for fine-grained needs (e.g. combat closing).
3. **Faction entities live in the `GlobalManager`, which `MasterTimePulse` does NOT iterate.** `AdvanceTime` fires **colony** processors (the colony is in the star system, which IS iterated) — mining, industry, population. It does **not** fire faction-level processors (NPC decisions, faction-wide aggregation). See `GameEngine/Factions/CLAUDE.md`.
4. **A large chunk of the integration suite is commented out** (`/* ... */`): `SystemGenTests`, `SerializationManagerTests`, `FactoryTests`, `MiningTests`, `SavingAndLoadingTests`, parts of `PathfindingTests`. So the "all green" count is mostly unit-level. Re-enable these one at a time, CI-verified — they likely rotted (like the in-code `DefaultHumans` start).
5. **CI cannot test the client.** Engine-only. UI verification is the developer's local build.
6. **A star system defaults to `Stasis` and Stasis systems are NOT processed.** `StarSystem.ActivityState` starts at `Stasis`; `MasterTimePulse.SimulateTimeUntil` filters `Stasis` systems out of the processing loop, so a colony in a Stasis system does **zero** mining/population/industry no matter how far you advance the clock — and nothing throws, so a naive smoke test passes while measuring a frozen universe (this is exactly what hid "the mine does nothing" — the rates were correct; the system was asleep). The live game promotes a system via faction-entity detection (`Game` setup loop → `UpdateActivityState` → Background) and via the client observing it (→ Foreground). `CreateWithColony` reproduces the "player is here" case with `IncrementExternalObserver(priority: true)` → Foreground (full speed; Background throttles hotloop frequency 10×). The economy readout prints `s.StartingSystem.ActivityState` as its first gauge for this reason. **If a feature test advances time and sees no state change, check ActivityState first.**

---

## Test inventory

| Test | What it guards |
|------|----------------|
| `ScenarioHarnessTests` | the harness itself — builds a colony start; advances a real colony a game-year without throwing (**the colony/economy loop coverage the suite never had**) |
| `NewGameStartSmokeTests` | the real New Game colony path (rides the harness) |
| `EconomyReadoutTests` | the economy board — mining (asserts deposits deplete), refining (asserts Space-Crete produced via the job lever), infra/fuel readouts |
| `ShipSpawnTests` | engine ship-spawn — `ShipFactory.CreateShip` (the DevTools spawn path) lands a ship in the system with its parts, and it survives a tick |
| `GameLoopSmokeTests` | core sim loop advances on a generated (colony-less) universe |
| `SaveLoadSmokeTests` | `Game.Save → Load` round-trips |
| `StateIntegritySmokeTests` | entity positions stay finite across a clock advance (catches silent NaN) |
| `PerformanceReadoutSmokeTests` | reads the engine's per-processor stopwatch; prints timing |
| `BaseModIntegrityTests` | base-mod JSON data (starting designs buildable; zero skipped entries) |
| `LedgerTests` | the (economy-disconnected) `Ledger` math |
| orbit math / vectors / EntityManager / serialization / modding | unit-level |

---

## Adding a new test

1. New `*.cs` in `Pulsar4X.Tests/` with `[TestFixture]` + `[Test]`.
2. For a feature test, start from `TestScenario.CreateWithColony()` and follow the read → advance → read pattern.
3. **If the test reproduces a known-broken path, ship it `[Ignore("reason")]` in the SAME commit** — never land a deliberately-red test on the integration branch (root `CLAUDE.md` lessons / `SESSION_STATE.md` 2026-06-22).
4. Push. CI runs it; the TRX report names any failure.
