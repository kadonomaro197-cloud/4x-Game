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
| `ComponentInstancesDB` (**namespace ≠ folder** — file is in `Engine/Components/`, namespace is Datablobs) | `Pulsar4X.Datablobs` |
| `ComponentInstance`, `ComponentDesign`, `TryGetComponentsByAttribute`, `GetAttribute` | `Pulsar4X.Components` |
| `IndustryAbilityDB`, `IndustryJob`, `IndustryTools`, `MiningDB`, `InfrastructureDB`, `MineResourcesAtbDB` | `Pulsar4X.Industry` |
| `ShipFactory`, `ShipDesign`, `ShipInfoDB` | `Pulsar4X.Ships` · `OrbitDB` → `Pulsar4X.Orbits` |
| `IConstructableDesign` | `Pulsar4X.Interfaces` |
| `DamageProcessor` (**internal** — reachable via `[assembly: InternalsVisibleTo("Pulsar4X.Tests")]` in `Engine/Game.cs`), `DamageFragment`, `DamageResult`, `EntityDamageProfileDB`, `DamageTools` | `Pulsar4X.Damage` |
| `Vector2` — `DamageFragment.Velocity`; **must be non-zero** or `DealDamageEnergyBeamSim` does `1/\|v\|` → NaN → `Convert.ToInt32` throws | `Pulsar4X.Orbital` |
| `BeamWeaponProcessor.FireBeamWeapon(launcher, target, hitsTarget, energy, wavelen, beamVel, beamLenSec, baseHitChance, optimalRange_m)` (the real fire entry point — for the firing-path gauge) | `Pulsar4X.Weapons` |

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
| `StartFleetTests` | the New Game **start fleet** — asserts `CreateFromBlueprint` builds the colony blueprint's fleets (CI-proven `[start] fleets=3, ships=5` from `colony-earth`). The engine-checkable half of "fleets aren't working" (the client half needs the dev's local build + DevTools "Dump State") |
| `ProductionBuildTests` | the build-to-product link — the factory consumes stored minerals and installs a new Refinery on the colony (`InstallOn` path); proves "resources → installed product", the rails a built unit rides |
| `CombatReadoutTests` | **MVP Stage 1** — the space-combat *damage* gauge (the path was 🔴 DARK / no tests). Calls `DamageProcessor.OnTakingDamage` directly on a real ship with a beam-shaped fragment and prints `[combat]` (health drop / components removed / destroyed?). Reading: the per-pixel sim deposits ~0 damage — which is why the auto-resolve engine below routes around it. |
| **Auto-resolve combat engine** (8 fixtures, `GameEngine/Combat/`) | the v1 space-combat spine — all CI-green: |
| &nbsp;&nbsp;`ShipCombatValueTests` | every starting design gets a `ShipCombatValueDB` (toughness > 0; firepower > 0 for beam-carriers) at build |
| &nbsp;&nbsp;`AutoResolveTests` | the salvo loop — stronger fleet wins & wipes the weaker; zero-firepower = stalemate; combatants die before utility hulls |
| &nbsp;&nbsp;`BattleTriggerTests` | `CombatEngagement.Tick` detects two hostile fleets in range, engages, and resolves; same-faction fleets never engage; **+ the combat interrupt** — a new engagement with `InterruptTimeOnNewEngagement` on sets `TimePulse.CombatInterruptPending` (clock halts at first contact); asserts it defaults off + try/finally so the static flag never leaks. **+ `NewEngagementImminent_*`** — the time-loop's fine-step gate reads **true** for two hostile un-engaged fleets in range and for one-engaged-one-joining (round 2), **false** for BOTH-already-engaged (the ongoing fight runs at the player's set speed, not a forced 5 s), for friendly-only, and for no-fleets. The *clock-stops-early* integration is **live-only** — this colony harness doesn't reliably auto-fire the battle-trigger hotloop on `TimeStep`, which is also why the fixtures drive `Tick` directly. **+ `Tick_RequireDetection_NoBattleUntilDetected`** — fog of war (detection slice 2): with `RequireDetectionToEngage` on, two hostile sensor-capable fleets in range do NOT engage until a sensor scan populates the track tables (no scan → no battle; scan → battle). Flag defaults off; try/finally so it never leaks. **+ `FirstStrike_SeerWipesBlindEnemy_Unscathed`** — first-strike (detection slice 5): two EQUAL armed fleets, the enemy BLINDED via the grave-rung path (its `SensorReceiverAtb` components removed + `ReCalcAbilities`), fog on → the player detects it and wipes it taking **zero** losses (the directed-fire resolver lets the blind fleet take fire without returning it; equal forces both firing would be mutual destruction, so player-intact + enemy-wiped proves the asymmetry — composes detection × grave-rung × weapons). (Clears the colony's own start fleets first — see the fixture's `ClearExistingFleets` note) |
| &nbsp;&nbsp;`FleetDoctrineTests` | the moddable doctrine catalog loads; `TrySetDoctrine` applies mults + honours the switch cooldown; an aggressive (×2) fleet beats the identical enemy |
| &nbsp;&nbsp;`FleetComponentTests` | per-component doctrine — a ship in an offensive sub-fleet reads ×2 while a ship directly in the fleet reads ×1; a component's ×2 flips a battle the raw hull would lose |
| &nbsp;&nbsp;`FleetRetreatTests` | retreat — a `fighting-withdrawal` fleet breaks off intact (posture); a 4-ship fleet that loses half retreats with survivors (threshold); both record a `FleetRetreatDB` |
| &nbsp;&nbsp;`EngagementLockTests` | an engaged fleet (`FleetCombatStateDB`) refuses an AssignShip order; a `TrySetDoctrine` on it still applies; clearing the combat state lets the order through |
| &nbsp;&nbsp;`CombatTestShipsTests` | the example ships (Aegis warship / Picket corvette) load + rate strong-vs-weak; a 3v3 auto-resolve is a decisive `SideAVictory`; **+ `ChargeReactors_FillsStoredEnergy_SoASpawnedShipCanWarp`** — a freshly built Leviathan boots with 0 stored energy (< warp bubble cost), `ShipFactory.ChargeReactors` tops it to `EnergyStoreMax` ≥ bubble cost (the "spawned ship won't move" fix) |
| **Combat depth — weapon flavor + dodge** (`docs/WEAPONS-AND-DODGE-DESIGN.md`) | all CI-green: |
| &nbsp;&nbsp;`ShipEvasionTests` | Evasion (maneuverability) — a small/light/high-thrust fighter dodges far better than a heavy sluggish battleship; no engine = no dodge |
| &nbsp;&nbsp;`WeaponProfileTests` | per-weapon flavor profiles — a laser warship has Beam profiles at ~light-speed with hit-chance + rate-of-fire; profiles sum to Firepower |
| &nbsp;&nbsp;`DodgeResolveTests` | the dodge — `HitFraction` curve (beams ignore evasion, slugs are dodged, flak floors it); and through the resolve, slug fire kills the un-evasive battleship while the same-toughness fighter survives |
| &nbsp;&nbsp;`CombatPerformanceTests` | 200 real warships resolve in milliseconds — the dodge resolve is O(ships) (fire aggregated by weapon class), the tripwire against an O(ships²) regression |
| **Combat — multi-party engagements** (`GameEngine/Combat/`) | CI-green: |
| &nbsp;&nbsp;`MultiPartyEngagementTests` | any number of fleets / either side / join mid-fight (`StepEngagementGroup`) — **assist** (two fleets beat a lone equal enemy on combined fire), **same-faction side** (a friendly reinforcement shares a side: no friendly fire — sized so an ally would die if it regressed), **join** (a reinforcement pulled into a 1-v-1 through the real `Tick` tips the fight), **fire-split** (one fleet vs two divides its fire — can't kill both in the step it could kill one, but reaches both). The 2-fleet path stays the existing fixtures' n=2 special case |
| &nbsp;&nbsp;`RailgunWeaponTests` | **P3 railgun, through the REAL data path** — builds the base-mod `Lancer` railgun cruiser (JSON `railgun-weapon` template → `RailgunWeaponAtb` via reflection → `ShipCombatValueDB`) and asserts the railgun profiles rate as finite-velocity, ballistic (low-tracking) kinetic fire that's **dodgeable** (lands on a sluggish hull, juked by a nimble one). This + `BaseModIntegrityTests` gauge the gotcha-10 JSON→Atb binding in CI |
| &nbsp;&nbsp;`FlakWeaponTests` | **P4 flak, through the REAL data path** — builds the base-mod `Bulwark` flak escort (JSON `flak-weapon` template → `FlakWeaponAtb` → `ShipCombatValueDB`) and asserts **saturation = rounds/sec × pellets/shot** (high) FLOORS the dodge: flak lands heavily on a nimble (ev 0.9) target where a low-saturation slug is juked — the fighter/missile killer. Same gotcha-10 gauge as railgun |
| &nbsp;&nbsp;`WeaponTriangleTests` | **P6 the triangle, on REAL built ships** — builds the `Wasp` fighter (tiny/agile = evasive) + `Leviathan` capital (big/sluggish/armoured) and asserts the dodge edges off their real combat values: FIGHTER ▸ railgun (fighter dodges slugs the capital eats), BEAM ▸ fighter (light-speed ignores the dodge), FLAK ▸ fighter (saturation floors it). All four corner designs (Wasp/Leviathan/Aegis/Bulwark) spawnable from DevTools |
| &nbsp;&nbsp;`WeaponTriangleBattleTests` | the triangle as ACTUAL fleet battles (not just hit-fractions): identical fighter screens take equal firepower — survive the railgun, fall to beams/flak; and an evasive Wasp swarm out-survives an evasion-zeroed swarm vs the same capital. Isolates evasion so it's calibration-robust |
| &nbsp;&nbsp;`CombatStressLab` | **10 extreme weapon-design + fleet-scale stress sims** (post-rebalance, `SalvoDamageScale` 0.1), assertions encode measured DIRECTION + real numbers: rate-of-fire (S01: 38 vs 5 /100) and velocity (S03: 39 vs 5) each defeat evasion like saturation does; slow flak is useless (S02: 34 vs 7); nothing is untouchable (S04: 39 vs 3 of ev0.95); alpha beats attrition (S05); 100v100 mirror is **exactly even** (S06: 50–50); a big-enough swarm overwhelms a super-capital (S07: ~400 now, since the slower pace lets the swarm's 50% retreat trigger first) at ~25–50 fighters/capital (S10); doctrine x2 swings a fight ~1.6:1 (S08: 40 vs 25); dodge scales to fleets (S09: railguns leave 85/100, beams 49/100) |
| &nbsp;&nbsp;`CombatBattleSims` | **the "10 more" whole-BATTLE sims at the rebalanced pace** — B01 duration (a 50v50 mirror lasts 38 salvos = 190 game-seconds, not 2–4); B02 duration scales ~linearly with toughness (38/150/599 at ×1/×4/×16); B03 saturation frontier + B04 evasion frontier as smooth curves; B05 combined-arms (railgun+flak) clears a mixed enemy better than mono; B06 quality endures over quantity at equal totals; B07 symmetric 3-way FFA (5/5/5); B08 a joining reinforcement flips a losing 10-v-20; B09 a mid-fight doctrine switch turns an even mirror into a 22–15 win; B10 1 dreadnought vs **1000** gnats resolves in ~9 ms (the bucketed-resolve tripwire at extreme scale) |
| `GameLoopSmokeTests` | core sim loop advances on a generated (colony-less) universe |
| `SaveLoadSmokeTests` | `Game.Save → Load` round-trips |
| `StateIntegritySmokeTests` | entity positions stay finite across a clock advance (catches silent NaN) |
| `PerformanceReadoutSmokeTests` | reads the engine's per-processor stopwatch; prints timing |
| `BaseModIntegrityTests` | base-mod JSON data (starting designs buildable; zero skipped entries) |
| `SensorDetectionTests` | **first gauge on the (previously 🔴 DARK) sensor/contact layer** — M1 detection lever, slice 1. Two hostile ships at a body; fire `SensorScan` by hand (the harness never schedules it — `PostNewGameInitialization` does, and the colony harness skips it), then assert the player faction holds a `SensorContact` for the enemy ship. Reaches `ProcessorManager.GetInstanceProcessor` via `InternalsVisibleTo`. *Foundation for the fog-of-war seam (slice 2).* |
| `LedgerTests` | the (economy-disconnected) `Ledger` math |
| orbit math / vectors / EntityManager / serialization / modding | unit-level |

---

## Adding a new test

1. New `*.cs` in `Pulsar4X.Tests/` with `[TestFixture]` + `[Test]`.
2. For a feature test, start from `TestScenario.CreateWithColony()` and follow the read → advance → read pattern.
3. **If the test reproduces a known-broken path, ship it `[Ignore("reason")]` in the SAME commit** — never land a deliberately-red test on the integration branch (root `CLAUDE.md` lessons / `SESSION_STATE.md` 2026-06-22).
4. Push. CI runs it; the TRX report names any failure.
