# Session State — Current Known Status

This document tracks what we know about the codebase state at the close of each session. A new session reads this first after the CLAUDE.md files, to skip rediscovery.

---

## Last Updated

Session ending 2026-06-25 — **MVP Stage 1 (space combat) BUILT, then a combat-DEPTH pass started — all CI-green.** Branch `claude/focused-ritchie-debock`. First the v1 auto-resolve spine (rate → auto-resolve → trigger → doctrine + per-component → retreat → engagement lock + example ships + DevTools faction switcher). Then, at the developer's explicit call to **cross the MVP firewall** for combat depth, the **weapon-flavor + dodge model**: ship Evasion (size+agility), per-weapon flavor profiles (damage/velocity/tracking/saturation), and DODGE in the resolve (a weapon's flavor decides who it hits — beams ignore evasion, slugs are dodged by the nimble, flak floors it), aggregated by weapon class so it stays **O(ships)** for 100s-of-ship battles. The DevTools faction switcher is the only piece CI can't verify (client). **Then the depth pass FINISHED and went further (all CI-green — see "cont. 3" below): the real player-buildable railgun + flak COMPONENTS (the full JSON template→Atb path) and the triangle example fleets; MULTI-PARTY engagements (any number of fleets, join mid-fight); a 20-sim behaviour lab (`CombatStressLab` + `CombatBattleSims`); and the HOT-DAMAGE REBALANCE (`SalvoDamageScale` — battles now last ~10× more salvos). Stage 2 (mirror the spine for ground combat) is next.**

---

## Session 2026-06-25 (cont. 3) — railgun/flak built, multi-party, + HOT-DAMAGE REBALANCE (READ THIS FIRST of the combat entries)

Everything the older entries below call "paused" or "remaining" is now **DONE and CI-green** on `claude/focused-ritchie-debock`:

- **Real player-buildable weapon types.** Railgun (P3) + flak (P4) ship through the full six-point JSON path (`*Atb` class → `weapons.json` template → `componentDesigns.json` → `earth.json` StartingItems + ComponentDesigns + ShipDesigns). The "riskiest CI-blind data work" note below is retired — the harness builds every `earth.json` design end-to-end, so a missing registration now fails CI loudly (learned it twice on railgun; the six-point checklist is in `GameEngine/Combat/CLAUDE.md`). Triangle example fleets (Wasp fighter / Leviathan capital + Lancer/Bulwark) are in the base mod and DevTools-spawnable.
- **Multi-party engagements.** Any number of fleets, either side, joining a fight in progress by coming into range. `StepEngagementGroup` is the resolver; the 2-fleet path is its n=2 special case (`MultiPartyEngagementTests`).
- **HOT-DAMAGE REBALANCE (the headline of this entry).** Raw numbers made fights end in 2–4 salvos (10–20 game-seconds) — over before the default 1-hour master tick. **`CombatEngagement.SalvoDamageScale` (0.1)** makes a salvo deposit a tenth of its raw energy toward kills, so the SAME fight lasts ~10× more salvos (a 50v50 mirror now runs 38 salvos ≈ 190 game-seconds — watchable, steerable). The scale is **uniform**, so it changed DURATION, not who wins. It lives only on the stepped (live) resolve; `AutoResolve` (instant off-screen) stays unscaled. The one knob to tune combat pace.
  - **One emergent shift a future session MUST know:** the slower pace let the **50%-loss retreat actually trigger**. At hot damage a loser was often alpha-wiped before it could break off; now it hits 50% losses and *retreats with survivors*. So a few "X wipes Y" matchups are now break-offs — e.g. a 150-fighter swarm now retreats from a super-capital it used to wipe (takes ~400 to overwhelm it). This is the retreat mechanic finally working, not a bug.
- **20-sim behaviour lab** (numbers in the test messages + `docs/WEAPONS-AND-DODGE-DESIGN.md`): `CombatStressLab` (10 extreme weapon/scale stress sims) + `CombatBattleSims` (10 whole-battle sims: duration, toughness sweep, saturation/evasion frontiers, combined-arms, quality-vs-quantity, 3-way FFA, reinforcements, mid-fight doctrine, 1-vs-1000). All directions held under the rebalance (it's balance-preserving); only durations grew and a few wipes became break-offs.

Source of truth for combat detail: **`GameEngine/Combat/CLAUDE.md`** (constants table incl. `SalvoDamageScale`, the six-point weapon-registration checklist, multi-party section) and **`docs/WEAPONS-AND-DODGE-DESIGN.md`** (rebalance note + findings).

---

## Session 2026-06-25 (cont.) — Combat-DEPTH pass: weapon flavor + dodge (READ THIS SECOND)

After the spine, the developer chose to add space-combat depth (knowingly crossing his own `docs/MVP.md` firewall — "cross it, it'll just deepen the tests"). Design captured in **`docs/WEAPONS-AND-DODGE-DESIGN.md`** (the four weapon-flavor stats, computed saturation, the **Fire-Emblem weapon triangle** Beam▸Fighter▸Capital▸Beam + a Missile⟷Flak axis, and the aggregate O(ships) math). Built gauge-first, all CI-green:

| Piece | What | Test |
|-------|------|------|
| Evasion | how hard a ship is to HIT = size (Volume_m3) × agility (thrust÷mass). Separate from toughness. | `ShipEvasionTests` |
| Weapon profiles | each weapon's {class, damage/sec, velocity, tracking, saturation=rate-of-fire} on `ShipCombatValueDB.Weapons`; Firepower = sum (backward-compat) | `WeaponProfileTests` |
| Dodge resolve | `BuildFireMix`→`LandedFraction`→`HitFraction`; effective toughness = raw ÷ landed; hittable ships die first. Beams ignore evasion; slugs dodged; flak floors. | `DodgeResolveTests` |
| Performance | fire aggregated by weapon CLASS → O(ships) per step (not O(ships²)); 200 warships resolve in ms | `CombatPerformanceTests` |

**Key things a future session MUST know:**
- **Backward-compat is load-bearing.** A ship with no `WeaponProfile`s fires as a light-speed always-hit beam, and a 0-evasion target has landed-fraction 1 — so every pre-dodge combat test behaves identically. That's WHY the whole spine stayed green. Don't break it.
- **Performance hinges on `BuildFireMix` aggregating by weapon class.** If that ever stops, the resolve goes O(ships²); `CombatPerformanceTests` is the tripwire.
- **The dodge model is exercised by STAMPING `WeaponProfile`s in tests** (Railgun/Beam/Flak) — it does NOT yet need a real railgun/flak component. That's deliberate: the real components are the remaining piece.
- **Why the railgun/flak COMPONENTS are paused:** making them player-buildable needs the NCalc component-designer **template** system (`GameData/.../TemplateFiles/weapons.json` — ~30 formula properties per weapon). The runtime template→attribute construction isn't covered by CI (gotcha 10 — JSON data drift crashes New Game, not `dotnet test`), so it's the riskiest CI-blind work. Do it as a careful dedicated pass WITH a local New Game check, or get the developer's input on the template approach first. The weapon-attribute classes themselves (code) ARE CI-verifiable; it's the JSON template + designer formulas that aren't.
- Full per-detail source of truth: **`GameEngine/Combat/CLAUDE.md`** ("Dodge in the resolve", "Example combat-test ships", constants table) and `docs/WEAPONS-AND-DODGE-DESIGN.md`.

---

## Session 2026-06-25 — MVP Stage 1: the Auto-Resolve Combat Engine (READ THIS FIRST)

Built the entire v1 space-combat spine that the 2026-06-24 rework specced — **one engine (auto-resolve), doctrine is the wheel** — piece by piece, each under a CI test, on branch `claude/focused-ritchie-debock`. All engine/data pieces are CI-green.

### WHERE TO RESUME
Stage 1 space combat **resolves** now. The next move is the developer's **live test**: enter SM mode → DevTools → spawn an *Aegis Test Warship* fleet, use the **Faction Switcher** to act as another faction and spawn a *Picket Test Corvette* fleet in the same system → they auto-engage → watch one side win (and try a doctrine change / `fighting-withdrawal` to see retreat). After that, **Stage 2 = mirror this engine for ground combat** (`GroundUnitDesign : IConstructableDesign` + a `GroundCombatProcessor` that attrites attacker vs defender — same shape as `CombatEngagement`).

### What was built (each its own commit + CI-green test)
| Piece | Engine | Test |
|-------|--------|------|
| Combat-design rework (one engine + doctrine, v1 boundary) | `docs/COMBAT-DESIGN.md` | — |
| **Ship combat value** (firepower from beams + missile stub; toughness from components + armour; role weight) — computed at build | `Combat/ShipCombatValueDB.cs`, hook in `ShipFactory` | `ShipCombatValueTests` |
| **Auto-resolve salvo loop** (strength → damage pools → whole-ship casualties, combatants first; pure, reports casualties) | `Combat/AutoResolve.cs` | `AutoResolveTests` |
| **In-game battle trigger** (hostile fleets in range auto-engage, fight over game-time) | `Combat/CombatEngagement.cs`, `BattleTriggerProcessor.cs`, `FleetCombatStateDB.cs` | `BattleTriggerTests` |
| **Switchable doctrine** (moddable `CombatDoctrineBlueprint` catalog → active `FleetDoctrineDB`; read-time strength/toughness mults; switch cooldown) | `Combat/FleetDoctrine*.cs`, `combatDoctrines.json` + mod pipeline | `FleetDoctrineTests` |
| **Per-component doctrine** (a fleet's sub-fleets each run their own posture) | `CombatEngagement.GetCombatShips` (CombatShip struct) | `FleetComponentTests` |
| **Retreat** (math outcome: flag + withdraw vector, no move order; posture OR casualty threshold) | `Combat/FleetRetreatDB.cs`, `CombatEngagement` | `FleetRetreatTests` |
| **Engagement lock** (engaged fleets refuse regular orders; only doctrine — a direct call — applies) | `StandAloneOrderHandler` + `EntityCommand.IsAllowedDuringEngagement` | `EngagementLockTests` |
| **Example test ships** (Aegis warship / Picket corvette — strong vs weak) | `shipDesigns.json` + colony-earth | `CombatTestShipsTests` |
| **DevTools faction switcher** (view/act as any faction — SM) | `DevToolsWindow.cs` | *client — local build only* |

### Key decisions / things a future session must know
- **The auto-resolve engine deliberately does NOT use the per-pixel damage sim** (it deposits ~0 damage — see BIG FINDING #1 below). Casualties are whole-ship removal by strength math. Don't wire combat value into `DamageProcessor`.
- **`CombatEngagement` is the heart**: `Tick` (detect + step engagements per system, run by `BattleTriggerProcessor` every 5 s), `GetFleetShips` (flat, for counts/detection), `GetCombatShips` (doctrine-tagged, for the math). v1 stubs are flagged in code: hostility = different non-neutral faction; range = flat distance + per-system; detection = mutual.
- **Doctrine is a direct call** (`FleetDoctrine.TrySetDoctrine`), NOT an order — that's *why* it still works under the engagement lock (the lock only gates the order handler).
- **Test-scenario gotcha that bit twice:** `TestScenario.CreateWithColony()` spawns the colony's own 3 fleets; a Tick test must clear them first (`ClearExistingFleets`) or the enemy engages a colony fleet before the test's. Documented in `BattleTriggerTests`.
- Full per-system detail is in **`GameEngine/Combat/CLAUDE.md`** (the source of truth) — file map, every gotcha, tuning constants.

---

## Session 2026-06-24 (cont.) — Stage 1 Combat Gauge → Combat-Design Rework → Fleet/Spawner/UI Shakedown (READ THIS FIRST)

Started Stage 1 (space combat) the gauge-first way; the gauge exposed that space combat's damage is **broken AND the wrong layer**. The developer reframed and chose to **rework the combat design**. Then a long live-test marathon shook out the ship/fleet/spawner/UI — a cascade that all traced back to **two roots** (stale live mod data + SM mode views the Game Master faction). Everything is resolved and live-verified. Combat is now a **deliberately separate effort.**

### WHERE TO RESUME (the one thing that matters)
**Stage 0 (economy) and the whole ship/fleet/economy/UI foundation are DONE and live-verified.** The next effort is **COMBAT, as its own thing** (the developer's explicit call — all combat *and* move-order work goes there). First combat task: **write the reworked `docs/COMBAT-DESIGN.md`** (the agreed direction is captured below and in a banner at the top of that file), then build **Stage 1 = the Tier 0 auto-resolve spine + ship combat value + doctrine, under harness tests.** Park the per-pixel damage sim (it's v2).

### Current state (live-verified by the developer)
- **Stage 0 economy:** DONE + live ("everything works but the spawner" — spawner since resolved).
- **Starting fleet:** WORKS + **CI-proven** (`StartFleetTests`: a New Game builds **3 fleets / 5 ships**). Live `Dump State` read **6 ships, 3 fleets** (5 start + 1 spawned).
- **DevTools spawner:** WORKS — spawns the ship, now joins it to a player fleet (via the order system), visible/controllable after exiting SM mode.
- **Combat:** NOT started, separate effort. The **move-order crash** and the **0-damage** finding are parked there.

### What was built this session
- **`CombatReadoutTests`** — the space-combat DAMAGE gauge (first coverage of the DARK damage path).
- **`StartFleetTests`** — the start-fleet gauge; CI-proves the engine builds the colony blueprint's fleets.
- **DevTools:** new **"Dump State"** button (live ship/fleet counts on-screen + flushed log); spawned ships **auto-join a player fleet** via `FleetOrder.AssignShip` → `OrderHandler`; dropdown-reset removed; **`DevLog` flushes** (`Console.Out.Flush()`).
- **Fixes:** `ShipDesignWindow` plastic-armor crash (graceful default, not a hard index). Reverted a client build-break I caused (see lesson 3).
- **Docs:** systems map §6 + Fleets row; `Pulsar4X.Client/CLAUDE.md` gotchas 6–10; test CLAUDE.md inventory; this file; COMBAT-DESIGN.md rework banner.

### BIG FINDING #1 — space combat damage is broken *and* the wrong layer
- `CombatReadoutTests`: **100 beam hits @ 1e10 J on a real ship → 0 damage, 0 components lost, ship not destroyed.** The per-pixel spatial sim (`DamageTools.DealDamageEnergyBeamSim`) deposits nothing. Space combat does not actually damage ships.
- Deeper: the as-built combat is **only** the Tier 2 per-pixel sim (the most detailed/expensive layer). The plan (`COMBAT-DESIGN.md`) wants **Tier 0 auto-resolution (fleet-math) as the spine — which does not exist.** They built the flashiest 10% and skipped the structural 90%. **Fixing the per-pixel sim = repairing the wrong layer.**

### THE COMBAT-DESIGN REWORK (agreed verbally — this is the spec to write up)
- **Developer's key insight:** *"it's always auto-resolve unless someone changes the fleet doctrine."* → There aren't 3 separate combat engines (the LOD tiers); there is **ONE engine — the auto-resolve loop — always running**, and player/AI **doctrine changes** (which take game-time to execute) are just inputs to it. "Watching a battle" = a **camera + the doctrine controls** on the same running loop. The per-pixel sim becomes an optional visual skin (v2), not a separate model.
- **Doctrine is the ENTIRE player control** — no individual weapon aiming. So doctrine is **v1-core**, co-equal with the auto-resolve math.
- **v1 line:** go **deep in the spine** (ship combat value derived from real weapons/armor + the auto-resolve loop + the fleet-components/switchable-doctrine model + retreat); **honest flagged STUBS at the edges** (weapon range = in/out; sensors + IFF = assume mutual detection, no fog; commander = one flat modifier; EMCON/terrain = none). **Parked for v2:** the per-pixel watched-battle sim, debris/salvage, real fog/IFF/commander-careers/EMCON/terrain. The stubs ARE the flags — they record what to deepen later (delivers the dev's "building it flags what we need" goal without dragging every dark system into v1).
- **Ground inherits the same engine** (movement + engagement are the same shape; doctrine is the same lever; ground just adds terrain/dig-in). This is *why* we do space combat first.
- **4X reframe stands:** economy = engine substrate; **eXpand + eXterminate = the v1 spine**; eXplore + eXploit(espionage) = the two deferred v2 strategic pillars (`docs/MVP.md`).

### BIG FINDING #2 — the fleet/spawner/UI cascade had TWO roots (not many bugs)
1. **Stale live mod data.** The running game reads `%AppData%\Roaming\Pulsar4X\Pulsar4X\Mods\`, refreshed from the repo's `GameData` only by a **successful** client build. Builds were broken (my fault, once), so the live `earth.json` was stale → "New Game has no starting fleet." **A clean build copied the fresh `earth.json` (which DOES define the fleet) → the fleets appeared.** CI always used the repo's `GameData` directly, so CI/`StartFleetTests` were right all along; the *live game* lagged.
2. **SM (Space Master) mode switches the VIEWED faction to the Game Master faction** (`GlobalUIState` SM toggle → `SetFaction(Game.GameMasterFaction)`), which owns **no fleets and no armor**. This single fact caused: (a) fleets vanishing from the Fleet window in SM mode; (b) spawned/own ships invisible in SM mode; (c) the **ship-design crash** (`ShipDesignWindow.RefreshArmor()` hard-indexed `Armor["plastic-armor"]`, which the Game Master lacks → KeyNotFound → whole-client crash). `_uiState.PlayerFaction` stays the real player; only `_uiState.Faction` changes. **Workflow:** spawn/use dev tools in SM mode, then **exit SM mode** to see and command your ships.

### Other diagnoses (smaller)
- Spawned ships orbit at **2× the planet radius** → sub-pixel on the planet icon at system zoom (zoom in to see them). The starter **"ISS Hermes"** is the launch-queue courier (`earth.json` `LaunchQueue`), launched by `LaunchComplexProcessor.TryLaunchShip` to **low Earth orbit** (even tighter) and `fleetDB.AddChild`-ed into the faction fleet — invisible behind Earth's icon, but in the fleet menu. The engine launch path can call `fleetDB.AddChild`; the **client DevTools spawn can't** (engine-internal), which is exactly why spawned ships weren't in the fleet tree (now fixed via the order system).
- **Console-output buffering:** when `launch.bat` redirects stdout to `console_output.txt`, .NET buffers runtime output and an **X-close does not reliably flush it** → the captured file is usually build-only. Reliable channels instead: the **on-screen DevTools status** and the **flushed `DevLog`** line.

### LESSONS LEARNED (methodology — these are the durable takeaways)
1. **Gauge-first won; guessing lost — twice.** The Visibility Gate paid off: `CombatReadoutTests` found 0 damage; `StartFleetTests` proved the engine builds fleets. Every time I *guessed* a live cause ("zoom in"; "fleets are defined at the faction level / never built"), I was **wrong**. The CI gauge was right. **When a fix fails, build the gauge — don't pile on a second guess.**
2. **Wrong-file trap.** The "fleets never built" mis-diagnosis traced `uef.json` — a *scenario* file the New Game wizard does **not** use. The real colony blueprint is `earth.json` (`colony-earth`), which HAS the fleet. **Verify which file the LIVE code path actually reads before concluding.**
3. **CI can't build the client → client changes are unverified until the dev builds.** I broke the client build once by calling **engine-internal** `FleetDB` mutators (`SetParent`/`AddChild`/`FlagShipID`) from the client — `ColonyFactory`/`LaunchComplexProcessor` (engine) may; `DevToolsWindow` (client) may not. **Client fleet changes MUST go through the order system** (`FleetOrder.*` → `Game.OrderHandler.HandleOrder`, as `FleetWindow` does). Verify a member is *accessible from the client*, not just that it exists.
4. **Stale live data vs. repo data.** CI/tests use the repo's `GameData`; the running game uses `%AppData%\Mods` refreshed only on a *successful* build. A broken build → stale live data → live symptoms that don't reproduce in CI. **When live ≠ CI, suspect stale Mods → clean rebuild.**
5. **The buffered console channel is unreliable; prefer on-screen + flushed.** Don't rely on `console_output.txt` for runtime readings (X-close doesn't flush). Build gauges that show **on-screen** and **flush** their log line.

### Open / deferred (the pickup list)
- **COMBAT — its own effort (developer's call).** (a) Write reworked `docs/COMBAT-DESIGN.md` (one engine + doctrine; v1 line above). (b) Build Stage 1: `ShipCombatValueDB` + the auto-resolve loop + the doctrine/fleet-components model + retreat, **each under a harness test** (no untested combat). Park the per-pixel sim.
- **Move-order crash** — selecting a fleet + a *Move* order crashes *before* issuing it (on the latest branch). Parked with combat. Not the spawn fix (that's a fleet-*assign* order, which works). A missing feature should fail gracefully, not crash the game — fix when building the order/movement layer.
- **Space-combat damage = 0** (per-pixel sim) — parked with the per-pixel sim. Tier 0 auto-resolve is the v1 damage model instead.
- **`gallicite`** — `ordnance.json` references an undefined mineral; will bite missiles. Still open (carried from the prior session).
- **Merge state:** latest fixes are on `claude/adoring-gates-i6svyk` (HEAD `7878b5b`). Ensure `main` has the crash fix (`f16c531`) and spawn-into-fleet (`7878b5b`) before the next live test.

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

---

## Session 2026-06-25 — multi-party engagements + real weapon types + the triangle (branch `claude/focused-ritchie-debock`)

**What got built (all CI-green on GitHub; engine only — the SDL client still needs your local run):**

1. **Multi-party engagements.** A battle is no longer locked to two fleets. Any number of fleets fight at once, and a fleet **joins a fight in progress just by coming into range** ("send in another fleet to assist"). Same-faction fleets share a side (no friendly fire); an attacker facing several enemies splits its fire across them (outnumbering doesn't multiply your guns). The old two-fleet fight is just the simplest case of the same code. Tests: `MultiPartyEngagementTests`.

2. **Two real, player-buildable weapon types** (the meaty part — they go through the live New Game JSON path, which `dotnet test` normally skips, so this took a few CI rounds to get the data registration right):
   - **Railgun / slug** — fast but finite-speed kinetic. Brutal vs slow capitals, but a nimble fighter **dodges** it. Design: **Lancer** cruiser. Tests: `RailgunWeaponTests`.
   - **Flak / point-defense** — low per-pellet, but **huge volume of fire** (rate × pellets) fills the sky and floors the dodge: the fighter/missile killer. Design: **Bulwark** escort. Tests: `FlakWeaponTests`.

3. **The weapon triangle on real ships.** New **Wasp** fighter (tiny, agile, evasive) and **Leviathan** battleship (big, armoured, can't dodge) designs. `WeaponTriangleTests` proves on the real built ships: fighter dodges the railgun, beam ignores the dodge, flak floors it. (The Capital▸Beam edge needs weapon *range*, still a v1 stub.)

**Spawn these from DevTools (faction switcher) to watch it live:** Aegis (beam), Lancer (railgun), Bulwark (flak), Wasp (fighter), Leviathan (capital), Picket (weak).

**Hard-won lesson (now in `Combat/CLAUDE.md`):** adding a player-buildable weapon touches **six** registration points — the C# Atb, the `weapons.json` template, the `componentDesigns.json` design, and **three** lists in `earth.json` (`StartingItems` unlocks the template, `ComponentDesigns` builds it, `ShipDesigns` mounts it). Miss any one and New Game crashes (but `dotnet test` over the C# path looks fine). CI's `CreateWithColony` harness is the standing sensor — it builds every starting design from JSON, so a missing registration fails loudly there.

**Still open / v2:** Capital▸Beam range edge, degraded-condition tiers (need recalc-combat-value-on-damage), explicit TriangleBonus tuning knob, and the per-pixel firing sim (parked — these weapons feed the auto-resolve only). Next big arc per the developer objective: **ground combat**, mirroring this now-solid space-combat spine.

---

## Session 2026-06-25 (cont.) — live-test: Fleet Combat UI + the "click a hostile ship → crash" bug (branch `claude/focused-ritchie-debock`)

**Live test result:** New Game started, the **Fleet Combat tab showed and doctrine selection worked** (`[FleetCombat] Set doctrine … OK` in the log). The developer spawned 6 hostile "Cargo Courier" ships around Ceres (`[DevTools] Spawn Hostile Fleet OK … fleet id=700`), zoomed in, clicked Ceres — and the game **crashed** (looked like a freeze; `game_log.txt` ended clean right after the spawn line).

**Root cause (provable, three code traces + the build log agreed):** a pre-existing latent client bug my hostile-spawn feature *exposed* — it's the first time a player could click a foreign-faction ship. Zoomed in, ships render at ~2× body radius, so they sit **on top of** the Ceres icon (Client gotcha #7) — the click opened the *ship's* `EntityWindow`, whose cargo-bar block did `factionInfoDB.Data.CargoTypes[sid].Name` on the **Hostiles** faction. A bare faction's `CargoTypes` is **empty** (everything's in `LockedCargoTypes` until tech unlock — Factions gotcha #4), so the hard index threw `KeyNotFoundException`. The SDL `Run` loop has **no try/catch** → the process crashed, and the trace went to **stderr**, which isn't in `game_log.txt` (Program.cs redirects stdout only) → invisible, looked like a hang.

**Fixed (client-only — CI can't build the client, so this needs your local `dotnet build`):**
- **Defensive cargo-type lookup** at the three sites that read an *owner* faction's cargo types (unlocked → locked → fall back to the id, never a hard index): `EntityWindow.cs` (ship cargo bars), `CargoStorageDBDisplay.cs`, `CreateTransferWindow.cs`.
- **A render-loop visibility gauge** — `PulsarMainWindow.SafeRender(...)` wraps the map draw + every window's `Display()`; if one throws, it logs the full stack trace **once** to `game_log.txt` (`[RenderError] <context> …`) and skips just that piece instead of crashing the whole app. So the *next* hidden render crash names itself in the log. (Client gotchas #11–#12.)

**Pull + retest:** `git pull origin claude/focused-ritchie-debock` → `dotnet build Pulsar4X/Pulsar4X.sln` → run → spawn hostiles, zoom in, **click a hostile ship** (the exact repro). It should no longer crash. If anything else faults, grep `game_log.txt` for `[RenderError]` and send me that block — it'll name the window.

**Still open from this live test:**
- **Fleet-component split UX** — the developer couldn't find how to divide the starting fleet into components (sub-fleets). The capability exists (Fleet tree → sub-fleet doctrine); it needs a discoverable affordance in the Fleet window. **Next task.**
- Confirm whether pressing **play** auto-starts the battle, or whether the "Force Engagement" fallback is needed (the test harness couldn't settle this — Combat sandbox gotcha #2).
- Deferred (developer's call, "later"): move game data out of `%AppData%\…\Pulsar4X\`; make `launch.bat` capture `game_log.txt` (the runtime log) instead of the build-only `console_output.txt`.
