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

> **⛔ DO NOT USE the `AskUserQuestion` tool (the multiple-choice question picker). It is BROKEN in this environment** (the permission stream closes before a response is received, every time — confirmed by the developer 2026-07-07). Calling it wastes a turn and returns an error. **Instead: pick the most sensible default, state plainly in chat what you chose and why, and proceed** — the developer will redirect in prose if they want something different. If you genuinely need a decision from the developer, ask it as a normal sentence in your reply and keep working on what you can in the meantime.

---

## Developer Objective (this fork)

**The north star (`docs/NORTH-STAR-VISION.md`, 2026-06-28): build the 4X engine whose systems — taken to real depth and *connected* — let a player stage their own version of *specific aspects* of the great sci-fi universes (The Expanse, Stargate, Stellaris, Halo, BSG, Star Trek, Babylon 5, Star Wars, Mass Effect, Andromeda).** Deliberately broad. Ground combat is no longer the finish line — it's *one* funded system in that box. A surprising amount of the substrate already exists (Newtonian physics, the weapon triangle, jump points, sensors, logistics); the work is mostly **depth + connection** plus a few missing decision layers (diplomacy-as-politics, scarcity-as-pressure, first-contact, a late-game crisis, ground combat).

The **near-term** objective still stands as the path toward that: extend the fork so **planetary/ground combat and planetary infrastructure eventually have the same depth space combat already has**, and improve the UI. Survey priority: understand how space combat is built so it can be mirrored for ground systems, where ground/planetary systems currently live or would hook in, and how the UI layer is constructed.

> **The vision raises the ceiling; it does NOT lower the bar.** `docs/MVP.md` still governs what ships next ("you can take a planet" is the nearest milestone); `docs/REALISM-VS-GAMEPLAY-AUDIT.md` still requires every system to earn its weight as a player decision. A franchise name is never a justification by itself — an *aspect* earns a *system*, built cradle-to-grave.

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
| Mod data | JSON files under `Pulsar4X/GameData/basemod/` (copied to AppData/Mods at build) |

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

> **CI (added 2026-06-22):** `.github/workflows/ci.yml` runs `dotnet build` + the full NUnit suite on **every push and pull request**, on GitHub's Linux runners (which have the .NET SDK this container lacks). This is the automated gate that catches a regression in *any* engine system on every change — including one a given commit didn't touch — without relying on anyone remembering to run tests. It builds the engine + test project and runs all tests (the `test` job); and as of 2026-06-28 a second **`build-client` job COMPILES the SDL/OpenGL client** on every push (compile only — it can't *run* headless, but it compiles, since the SDL/GL natives are runtime-only and the references are managed wrappers). A red ✗ on a commit/PR means build or a test broke — check it before promoting. The broadest sensor it runs is `GameLoopSmokeTests` (advances the simulation clock on a generated universe, asserts no processor throws). Two more passive, read-only sensors run alongside it: `StateIntegritySmokeTests` (after advancing the clock, asserts every entity's position is a finite number — catches silent NaN/garbage a throw-check can't see, since the engine has no NaN guards) and `PerformanceReadoutSmokeTests` (reads the engine's built-in per-processor stopwatch and prints a timing breakdown into the CI log). CI also publishes a per-test report (TRX) as an inline results table and a downloadable artifact, so a red run shows exactly which test broke and why — not just a count. All sensors only read game state; none modify the simulation.
>
> **CI now COMPILES the SDL client but cannot RUN it (2026-06-28).** The `build-client` job catches client **compile** breaks (wrong overload, bad `internal` access, missing using) that used to sail through green CI and only surface on the developer's local Windows build — the #1 client-blind failure, now closed. But CI still **cannot run** the client (display-coupled), so **runtime crashes, render glitches, NaN positions, freezes, and behavior never surface here** — the developer's local build remains the only *runtime* check. The compensating tool is `launch.bat`: it captures all console output (stdout+stderr) to `console_output.txt` and keeps the window open on crash — the client's diagnostic channel. **The running list of client things awaiting a local runtime check is `docs/CLIENT-TEST-CHECKLIST.md`.** Live-test loop: play → reproduce → read/send `console_output.txt` → fix → pull → repeat. Full live-test play-by-play, session log, and lessons learned (2026-06-22) are in `SESSION_STATE.md`.

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
| Combat (Auto-Resolve Engine) | `GameEngine/Combat/` | `GameEngine/Combat/CLAUDE.md` |
| Damage | `GameEngine/Damage/` | `GameEngine/Damage/CLAUDE.md` |
| Colonies / Population | `GameEngine/Colonies/` | `GameEngine/Colonies/CLAUDE.md` |
| Stations (parallel off-world host) | `GameEngine/Stations/` | `GameEngine/Stations/CLAUDE.md` |
| Ground Combat (planet surface + units) | `GameEngine/GroundCombat/` | `GameEngine/GroundCombat/CLAUDE.md` |
| Site Engine (located mid-game episodes) | `GameEngine/Sites/` | `GameEngine/Sites/CLAUDE.md` |
| Industry / Production | `GameEngine/Industry/` | `GameEngine/Industry/CLAUDE.md` |
| Movement / Navigation | `GameEngine/Movement/` | `GameEngine/Movement/CLAUDE.md` |
| Sensors | `GameEngine/Sensors/` | `GameEngine/Sensors/CLAUDE.md` |
| Space Hazards (Gas Clouds / Solar Flares) | `GameEngine/Hazards/` | `GameEngine/Hazards/CLAUDE.md` |
| Orbits | `GameEngine/Orbits/` + `Pulsar4X.OrbitalMath/` | *(not yet written — read source directly)* |
| Galaxy / System Gen | `GameEngine/Galaxy/` | `GameEngine/Galaxy/CLAUDE.md` |
| Fleets | `GameEngine/Fleets/` | `GameEngine/Fleets/CLAUDE.md` |
| Logistics | `GameEngine/Logistics/` | `GameEngine/Logistics/CLAUDE.md` |
| Research/Tech | `GameEngine/Tech/` | `GameEngine/Tech/CLAUDE.md` |
| Factions | `GameEngine/Factions/` | `GameEngine/Factions/CLAUDE.md` |
| People / Commanders | `GameEngine/People/` | `GameEngine/People/CLAUDE.md` |
| UI Client | `Pulsar4X.Client/` | `Pulsar4X.Client/CLAUDE.md` |
| Tests | `Pulsar4X.Tests/` | `Pulsar4X.Tests/CLAUDE.md` |

---

## Design & Convention References

| Doc | Read it when |
|-----|--------------|
| `docs/DOCS-INDEX.md` | **FIRST, on any session — and update it in the SAME commit as any doc change.** The living dashboard of every doc in the repo: its purpose and its status right now (current / stale / design-locked+build-state / reference / superseded). It's how anyone picks up cold and knows where things stand. The rule: a doc's *status* is maintained by hand (git knows the last-touch sha; only a human/agent knows if the content still reflects reality) — so when a commit changes a doc, or changes code that makes a doc newly-true or newly-stale, flip its row here in the same commit, and refresh the "As of" stamp on a substantive pass. |
| `docs/DOCS-AUDIT-PROCESS.md` | **When the docs and code have drifted, or on a periodic doc inspection.** The repeatable playbook (SOP) for auditing every doc against what the code actually does, deciding what earns its keep, and reorganizing — designed to be re-run cold by a future session. Holds the principle (verify against code · truth before structure · build the residual-grep gauge because CI can't test markdown), the ~125-agent discovery fan-out (regenerable Workflow skeleton), the 4-wave execution order, the between-steps verification gauges, and the two honesty guards (three-state build vocabulary; code-exists ≠ runs-correctly). The *findings* of each run go in a dated `docs/DOCS-AUDIT-YYYY-MM-DD.md`; this is the permanent *how-to*. |
| `docs/MVP.md` | **Before adding any feature, or whenever a "good idea" arrives.** Defines the v1 finish line ("you can take a planet") and the scope firewall — what's IN, what's explicitly deferred, and the Parking Lot where ideas wait so they don't derail the build. The thing that stops the game being half-built forever. |
| `docs/REALISM-VS-GAMEPLAY-AUDIT.md` | **Before building or deepening ANY system — and the companion to MVP.md (scope firewall : weight firewall).** Grades every system EARNS WEIGHT / PRETTY / LATENT by one rule: a mechanic earns its keep only if it's the source of a player DECISION that stacks. Holds the headline finding ("a fidelity showcase, not a decision engine — 95% built, 5% wired"), the verdict board, the "stop feeding the pretty" list, and the ranked **cheap-wiring list** that converts built realism into gameplay. Name the decision before you build the realism. |
| `docs/TESTING-TRACKER.md` | **Before testing anything, after building anything testable, and to know what still needs a run — across ALL branches.** The standing global testing ledger: the three test layers (engine CI / client-compile CI / local runtime), and every tracked test with seven fields — what · why · most-efficient method · what-right-looks-like · most-likely-failure · mitigation-in-place · what-it-unblocks. Layer-3 (local runtime) is the live backlog; the always-first item is **T0 New-Game boots + clock runs**. Add a row when you build something; update status + record the gauge reading when you test it. Indexes/outlives the per-branch `CLIENT-TEST-CHECKLIST.md`. |
| `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` | **Deciding what to work on next, or before touching any system.** The living map of every system, its status (done/works/partial/dark), its gauge/test, and what it's wired to. Stops reactive pivoting; enforces "work the connected systems too." Also holds the play-by-play test instructions. |
| `CONVENTIONS.md` | **Before writing any new code.** Pulsar's actual coding idioms (DataBlob copy-ctor/`Clone()`, serialize-one-collection-rebuild-indexes, `TryGet`/sentinel defensiveness, components+`*Atb`, processor auto-discovery). Match these, don't impose your own style. |
| `docs/aurora/INDEX.md` | Designing any ground-combat or infrastructure mechanic. Aurora 4X is the design spec for systems Pulsar lacks. |
| `docs/MORALE-AND-POPULATION-DESIGN.md` | **Before any work on population, morale, colony/station manning, or people-as-a-resource (crew/officers/scientists/army).** The LOCKED design + build plan (M1→M5) turning population from a one-way number into a TANK with morale as the level-control valve and people as a finite, hard-drawn resource. Holds the locked decisions (hard draw, per-colony morale, tax lever, government-as-modulator parked) and the connection map. M1 (MoraleDB + migration valve) is built. |
| `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` | **Before any work on espionage, covert agents, intelligence/fog-of-war-for-politics, counter-intelligence, or what-a-rival-actually-intends.** The **hidden-information ENGINE** that makes the locked FULL-version diplomacy (`DIPLOMACY-DESIGN.md`) a real reading game. Holds the **Information Ledger** (per-rival, per-facet intel level: Inferred→Confirmed→decays-to-Stale — fog-of-war for politics, on the detection substrate), agents as the M3 intelligence arm (Spymaster delegate + taskable operatives, grave-rung: caught/killed/turned), the broad data-driven **covert-action catalog** (gather / steal-tech / steal-funds / sabotage / sow-unrest-into-their-INTERNAL-politics / turn-or-assassinate / disinformation / counter-intel), the **risk/reward detection bet** (caught scales relation-hit → betrayal penalty → casus belli for THEM), and the always-on MIRROR (NPCs spy on you → counter-intel is a standing decision). Spy capability is a COMPONENT (research→build→install→lose). Hard prerequisite: the degenerate detection-quality fix. |
| `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md` | **Before any work on governors/ministers/admirals-as-delegates, command structure, span-of-control, or the "play at your own altitude" agency system.** The unifying **DELEGATE** layer — one shape (officer + post + stance + funding + competence) for every pillar, so any system can be hand-flown OR handed to a capable officer (the anti-"feels like a job" valve; delegation is the default, intervention opt-in). Key finding: it's mostly **CONNECT not build** — the span-of-control seats (`AdminSpaceAtb`/`AdminLevel` Ship→Empire), the generic officer-in-a-post record (`AdministratorDB` with funding dial + competence bonuses), commanders, and academies already exist; we generalize `AdministratorDB` off its research-only flavor. Holds the cradle-to-grave (command components are built/installed/destroyed — a decapitation strike collapses delegation) and the chassis the politics Ministers bolt onto. |
| `docs/GOVERNMENT-AND-POLITICS-DESIGN.md` | **Before any work on government types, regime rules, unrest/revolt, or the popular-demands (Stellaris-parties) layer.** The empire-wide regime as a MODULATOR = coefficient overrides + RULE overrides (e.g. dictatorship flips the crew-shortage rule from Block to Build-understaffed). Holds the type×lever matrix, the naming split (government-type vs governance/delegation), and the future people-demand layer. Build after M3–M5; those are built government-ready. |
| `docs/SITE-ENGINE-DESIGN.md` | **Before ANY work on exploration content, field sites, the Command Berth / leader-assignment, located ground/space combat as a prize, or the late-game crisis — the UNIFYING mid-game spine.** The keystone finding from three player walkthroughs: exploration, ground combat, diplomacy, research, economy, and the crisis system are NOT separate features to wire together — they are ONE data-driven **Site Engine** (a located site → a berth-seated leader works it → it's fought over at that location → it resolves down composable branches → a yield, or nothing), and every episode across every franchise is a *row* across ~7 dials (Location · Discovery · Worker · Role · Shape · Hook · Yield). Holds: the site **state machine** (agency-preserving — no timers, "bleeds you", knowledge unlocks branches, the persistent→crisis *rupture* edge); the **Command Berth** worker interface (Role/Grade/Support/Survivability/Span dials + the posting-danger→incident roll = one leader-death roll across all pillars); the located model + the 6-step transport embark/deploy; the **consumer/hook map** (each existing system becomes a consumer); the EXISTS/NEW ledger from a four-agent survey (most runtime exists — a research station on a hex already produces research; the seat mechanism is type-agnostic today); and the **anomaly-first build sequence** (SE-1 = the whole engine on a space anomaly, no surface). Parent of `EXPLORATION-CONTENT-DESIGN.md` + the Command-Berth half of `GOVERNANCE-AND-DELEGATION-DESIGN.md`. |
| `docs/BEYOND-PROTOCOL-REFERENCE.md` | **Designing the space-economy / politics / espionage depth** — the second design north star alongside Aurora. Aurora = physics fidelity; Beyond Protocol = the strategic/human layer. Read it for the colony **morale/population feedback loop** (the near-term "finish the space economy" target), the player ship-layout designer, and the B5 "politics with teeth" (senate/legislation + covert agents). |
| `docs/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` | **Before any space-economy / infrastructure work — the unifying frame.** The space station as the universal off-world infrastructure container (research / mining+refining+depot / commerce / population stations) + the colony-progression growth ladder, under "a place with component-infrastructure." Key finding: mostly CONNECT not rebuild — research is already component-based (`ResearchPointsAtbDB.bonusCategory`); the one load-bearing change is generalizing the colony host off `PlanetEntity`. *(Consolidated 2026-07-13 from SPACE-STATIONS + COLONY-PROGRESSION.)* |
| `docs/aurora/GROUND-COMBAT.md` | Building ground forces, formations, invasion, bombardment-vs-ground. |
| `docs/aurora/PLANETARY-INFRASTRUCTURE.md` | Adding installations, construction, economy depth. |
| `docs/aurora/SPACE-COMBAT-BENCHMARK.md` | Calibrating "the same depth space combat has." |
| `docs/RESOURCES-AND-MATERIALS-DESIGN.md` | Designing anything touching minerals, materials, production, commerce, research, or NPC economic AI. Full system survey — read before changing any part of the economy. |
| `docs/DIPLOMACY-DESIGN.md` | Designing anything touching faction relationships, IFF, inter-faction trade, logistics access, NPC doctrine, or diplomatic state — **the EXTERNAL-politics half** (the outward twin of `GOVERNMENT-AND-POLITICS-DESIGN.md`'s internal layer). Two halves: the mechanical **substrate survey** (`DiplomacyDB`/`RelationshipState`/IFF/first-contact wiring/commerce — build first) and **"EXTERNAL politics — politics with teeth"** (the decision-engine: first-contact-as-event, the relationship TRACK, costed treaties, casus belli gated by the militarism dial, the INTERNAL⟷EXTERNAL handoff, the Foreign-Minister delegate, and the espionage/gate-control/late-game-crisis frontier), **plus the LOCKED "Making politics FUN + CONNECTION layer"** (the COMMITMENT model — a deal emits real orders into fleets/logistics/money with promised-vs-delivered tracked; the broad data-driven EXCHANGE CATALOG of everything two factions can trade + which system each routes into; REACTIVE diplomacy — the "Are we good?" engine = the internal demand-engine pointed outward; and the **blast radius + 3-keystone prerequisite chain**: fix the GlobalManager-not-iterated trap → fix degenerate detection-quality → wire hostility-from-DiplomacyDB, THEN build politics). Read before adding any cross-faction interaction. |
| `docs/DETECTION-DESIGN.md` | **Before any sensor/detection/fog-of-war/EMCON work** (M1 lever #1). Survey of the existing (rigorous but unwired) sensor engine + the Keep/Cut/Add design: keep the contact track-table + signature model, hide the EM-spectrum math as gameplay, ADD fog-of-war-in-combat + the EMCON (Active/Dark) posture lever. Holds the "what exists / how it works / how it comes together" and the gauged build sequence. The decision is *dark-vs-loud*, not wavelength tuning. |
| `docs/COMBAT-DESIGN.md` | The master space-combat design: the eleven required systems (weapon range → auto-resolution → ground-combat interface). Includes the detailed **Fleet Components & Switchable Doctrine** design (Front Line/Flank/Rear Guard/Artillery as sub-fleets, Offensive/Defensive/Utilitarian options, switch cooldown, commander operational discretion, table-based fleet combat UI) under System 4. Read before any combat-system work. |
| `docs/WEAPONS-DESIGN.md` | Weapon design + the combat-**depth** pass: the two-axis **Nature × Delivery** taxonomy (the governing frame — pick nature × delivery, the triangle position emerges), plus the dodge hit-fraction, computed saturation, the **weapon triangle**, and the **aggregate/bucketed** resolve that keeps 100s-of-ships battles cheap. Read before touching weapon types, evasion, or the dodge resolve. *(Consolidated 2026-07-13 from WEAPONS-AND-DODGE + WEAPON-TAXONOMY.)* |
| `docs/FLEET-COMBAT-CLOSING-DESIGN.md` | **Before any work on combat distance/range, fleet aggregation, the engagement trigger, or rules of engagement.** The phased blueprint for turning the instant strength-compare into a **closing fight** where range/speed/detection/doctrine decide who can hit whom — anchored on the **standoff-vs-brawl** decision, with the locked decisions (doctrine-only control, scalar per-group range = **no 2D**, determinism = fast-forward==watch, **first shot makes the battle**, ROE = the grown-up `FleetDoctrineDB`), the **roots→canopy phase tree** (each phase a gauged slice: weapon-range data → fleet aggregation → single-range closing → kiting counters → first-shot trigger → per-sub-fleet ranges → ROE → the readout), and the parking lot (2D/flanking, hazard fields, provisions). The build plan for the M1 combat-range spine. |
| `docs/INFORMATION-DELTA-DESIGN.md` | **Before adding any readout/gauge/UI-number, or when a system "doesn't communicate with the player."** The gap between what the sim KNOWS and what it SHOWS, with the load-bearing distinction: **Failure A** (the number exists, just unwired — cheap) vs **Failure B** (no number exists — build the gauge first, an engine job before a UI job). Holds the EXISTS/MISSING ledger (file:line) and the first build: engagement range, detection range (the reverse-solve), delta-V, ETA. Read before assuming a missing readout means a missing number — half the time the gauge is built and just unwired. |
| **THE AI DESIGN SUITE (6 docs) — `docs/AI-*.md`** | **Before ANY work on the faction/NPC AI — the brain in `NPCDecisionProcessor.cs` (`GameEngine/Factions/`).** As-built truth (2026-07-13): `Tick` is a **full decision engine** (objective selection + planner + reactive-diplomacy + espionage mirror + crisis), **not** an empty stub — but the behaviour arms ship **gated OFF by default flags** (`EnableOrderEmission`/`EnableDiplomaticProposals`/`EnableEspionageMirror`/`EnableIntelLedger`), so a default game runs the loop idle. Durable seats shipped; `GalaxyCrisis` is live. Runtime unverified (CI can't run the client). A full "quark→brane" design, consolidated 12→6 docs 2026-07-13. **Enter through the build tracker.** **Read order:** ① `AI-BRAIN-BUILD-TRACKER.md` — THE hub: build state per slice + the design→code wiring map + §4 SOCKET VERIFICATION. ② `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` — the 12-trait `PersonalityDB` + shared `DecisionScorer` (`PersonalityDB` built at 0.5-neutral; scoring/drift pending). ③ `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` — mission-command (Head-of-State sets destination, delegates decide *how*; the 3-mode Delegate/Advise/Hand-fly dial). ④ `AI-DECISION-ENGINE-DESIGN.md` — the needs-ladder objective engine (destination) + the means-ends planner (how to reach it). ⑤ `AI-EMERGENT-POLITICS-AND-CRISIS-DESIGN.md` — emergent inter-faction politics (Organism pointed outward) + the temporal arc & late-game crisis + the authoring/acceptance layer. ⑥ `AI-CAPABILITY-CATALOG.md` — the NEED→buildables catalog the utility scorer reads. Roster/seats/parity live in `GOVERNANCE-AND-DELEGATION-DESIGN.md`. Companion: `docs/EXPLORATION-CONTENT-DESIGN.md` (the field-site loop). |

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

### 🧨 Landmine Index — scan this ONCE before touching an unfamiliar system

The worst traps in this codebase, in one scannable table. Each has bitten before. The "Read" column is where the full detail lives. **If a row touches what you're about to change, read that source first.** (The numbered gotchas below this table are the root-level detail; subsystem `CLAUDE.md`s hold the rest.)

| # | Trap (the symptom) | The rule / fix | Read |
|---|--------------------|----------------|------|
| L1 | **Dead code that looks live.** `InstallationsDB`, `SimpleDamage`, `ViewModelLib`, `PlanetaryWindow.RenderInstallations` gated on a dead blob — building on any of these wastes the work. | Grep + read the source before wiring; a class with no `[JsonProperty]` fields and only dead-code refs is a corpse. | this file gotchas #4/#6; Colonies/Industry CLAUDE.md |
| L2 | **`async void` swallows exceptions.** `AddEntity`/`SetDataBlob`/`TagEntityForRemoval`/`RemoveDatablob` — an exception inside vanishes to the thread pool, unobservable. | Keep mutation code minimal + well-tested; never hide logic inside these. | gotcha #5 |
| L3 | **Renaming a DataBlob breaks every save.** `TypeNameHandling.Objects` embeds C# type names in the JSON. | Add a `[JsonConverter]`/migration when renaming/moving a `*DB`. | gotcha #7 |
| L4 | **A broken processor crashes STARTUP silently.** Processors auto-register by reflection; a bad ctor → `NullReferenceException` at boot. | Keep processor constructors trivial; `Tick`/`ProcessEntity` must never throw. | gotcha #2; GameEngine CLAUDE.md #1 |
| L5 | **Faction-level processors only fire because the GlobalManager is now iterated (keystone #34).** Before, anything on a faction entity (politics, NPC AI) never ran. | Faction-level work runs in `GlobalManager.ManagerSubpulses`; if a processor "never fires," check it's keyed to a blob that's actually iterated. | Factions CLAUDE.md; MasterTimePulse |
| L6 | **New component = TWO-part registration or New Game crashes.** Design id in the colony blueprint `ComponentDesigns` **and** the template id in `StartingItems`. A new player weapon is a **six-point** chain. | Add both ends in the same change; run `BaseModIntegrityTests`. | gotcha #10; Combat CLAUDE.md "SIX registration points" |
| L7 | **Template Property values clamp to tech-formula bounds at instantiation.** Setting e.g. gravity tolerance to 0 via a design Property silently won't stick. | To omit an attribute, author a dedicated template without it — don't rely on a 0 value. | Stations CLAUDE.md (space-habitat) |
| L8 | **JSON data drift crashes players, not `dotnet test`.** The game builds the start colony from JSON; the tests build it in C#. A cost/reference added to one end and not the other ships green and blows up on New Game. | Apply the Prime Directive to DATA: check the *other end* (id defined? material in `StartingItems`?); `BaseModIntegrityTests` is the sensor. | gotcha #10 |
| L9 | **One hotloop processor per DataBlob type.** Two processors keyed to the same blob → one silently never runs. | Key a new processor to a blob no other processor owns (e.g. combat trigger → `StarInfoDB`, not `FleetDB`). | Combat/Colonies CLAUDE.md |
| L10 | **Combat is the auto-resolve strength-math engine, NOT the per-pixel damage sim.** `DamageComplex`/`VeryComplex` is parked. | Wire combat via `ShipCombatValueDB`/`AutoResolve`; don't route it through the pixel sim. | Combat CLAUDE.md #1 |
| L11 | **CI can't run the client.** Compile breaks → `build-client` catches; runtime crashes/render/behavior → only the developer's local Windows build sees them. | Engine logic gets a CI test; client behavior goes on `docs/CLIENT-TEST-CHECKLIST.md` for a local run. | this file (CI section) |

1. **Damage path decision is made and fully wired.** `DamageComplex` is the forward direction. `SimpleDamage` is dead code. Beam hits go: `BeamWeaponProcessor.OnHit()` → `DamageProcessor.OnTakingDamage()` → `DealDamageEnergyBeamSim()`. Colony hits route to `OnColonyDamage()` (population + atmospheric + installation damage, all wired). Asteroid hits use `DamageVeryComplex`. See `Damage/CLAUDE.md`.

2. **ProcessorManager auto-discovers via reflection.** Any class implementing `IHotloopProcessor` or `IInstanceProcessor` is automatically registered on startup by `ProcessorManager.CreateProcessors()`. You do not register processors manually. The trade-off: a broken processor crashes startup.

3. **Missile guidance is functional as of 2026-06-21.** `directAttack` is now `true` in `MissleProcessor.cs`. Missiles use `ThrustToTargetCmd` (direct pursuit), not phasing maneuvers. `MissileImpactProcessor` (new `IHotloopProcessor`) checks proximity every second and delivers kinetic damage on impact (≤ 1000 m). Calibration note: kinetic energy at orbital closing speeds is GJ-scale, well above the kJ–MJ beam damage tuning — ships will be one-shot. Tune `MissileImpactProcessor.ImpactRadius_m` or the energy divisor once warhead energy values are finalized. See `Weapons/CLAUDE.md` for full status.

4. **Colony economy UI — EXISTS and is wired (stale-doc warning retired 2026-06-24).** This gotcha used to say `PlanetaryWindow.RenderInstallations()` rendered nothing because it gated on the dead `InstallationsDB`. **That is fixed in the code**: `PlanetaryWindow` now gates the Installations tab on `ComponentInstancesDB` (every colony has it) and renders via `componentsDB.Display(...)` (`PlanetaryWindow.cs:102,220`). More importantly, the **full colony economy UI already exists in `ColonyManagementWindow`** — tabs for **Summary** (planet + population + infrastructure efficiency + installed components + stockpile of raw *and* refined materials), **Production** (`IndustryDisplay` — queue refining/build jobs via `IndustryOrder2`, with batch/repeat/auto-install/priority/cancel — the in-UI version of the engine `QueueProductionJob` lever), **Construction**, and **Mining** (per-mineral rates, annual production, years-to-depletion). The `InstallationsDB` blob is still dead/vestigial (don't use it), but the *UI gap it implied is closed*. **Lesson: this doc and `Pulsar4X.Client/CLAUDE.md` were stale and nearly caused a rebuild of UI that already works — verify client state by running it (CI can't), don't trust a "broken UI" note without checking the code.** The real open question is whether it works *live*, which only the developer's build can answer.

5. **`async void` on EntityManager mutations swallows exceptions.** `AddEntity`, `TagEntityForRemoval`, `SetDataBlob`, `RemoveDatablob` are all `async void` (needed for `MessagePublisher.Publish`). Any exception inside propagates to the thread pool and is unobservable. Keep mutation code minimal and well-tested.

6. **ViewModelLib is dead weight.** `ViewModelLib/` contains WPF-style ViewModel and OpenGL abstractions from a prior frontend. Nothing in the current `Pulsar4X.Client` references it. Do not add new code there.

7. **Save/load uses TypeNameHandling.Objects.** This means the JSON save file embeds C# type names. Renaming or moving a DataBlob class will break existing saves. When refactoring, add a `[JsonConverter]` or migration step.

8. **Colony orbital bombardment damage is wired and missiles now deliver it.** `DamageProcessor.OnColonyDamage()` handles population casualties, atmospheric contamination, and random installation damage when a colony entity takes a hit. `MissileImpactProcessor` calls `DamageProcessor.OnTakingDamage()` on impact, which routes colony hits to `OnColonyDamage()`. Missile guidance fixed 2026-06-21 (`directAttack = true`, `ThrustToTargetCmd`). Ground combat orbital support is now unblocked on the missile side.

9. **Claude Code remote sessions MUST be configured with `kadonomaro197-cloud/4x-Game`, not `Pulsar4x`.** The git proxy enforces the session's repository scope. If the session was started with the wrong repo name (`Pulsar4x` doesn't exist on GitHub — the actual game repo is `4x-Game`), every `git push` returns "repository not authorized" and MCP GitHub tools return "Access denied." There is no fix mid-session; the work cannot be pushed until a new session is started with the correct repo name. Verify the scope by checking the session ingress token: `cat /home/claude/.claude/remote/.session_ingress_token` — the JWT payload's `"sources"` field shows the authorized repos. The correct session configuration is `kadonomaro197-cloud/4x-Game` + `kadonomaro197-cloud/AiD-Main`. If you find yourself blocked, generate a patch with `git format-patch origin/HEAD..HEAD --stdout > /home/user/all-work.patch` and send it to the user via `SendUserFile` — all work is preserved and can be applied to a local clone with `git am`.

10. **The test suite does NOT exercise the New Game JSON path — so JSON data drift crashes players, not `dotnet test`.** The starting colony is built **two different ways**: the tests build it in C# (`DefaultStartFactory.DefaultHumans`), but the **game** builds it from JSON (`ColonyFactory.CreateFromBlueprint` reading `GameData/.../sol/earth.json` + the template JSON). Because the JSON path had no test, three data bugs shipped green and only blew up on New Game / Quickstart: (a) `electronics`/`ree-magnetics` were added as costs to starting designs but never added to the colony's `StartingItems` (crash — fixed); (b) `DamageResistBlueprint`'s `[JsonProperty("UniqueID")]` left the string key null (crash — fixed); (c) `ordnance.json` references `gallicite`, which is **not defined** as a mineral/material (latent — flagged). **Guardrail in place:** `Pulsar4X.Tests/Modding/BaseModIntegrityTests.cs` reproduces the real unlock logic and asserts (a) every starting colony can build all its starting designs, and (b) the base mod loads with **zero skipped entries** — a null/empty `UniqueID` no longer just skips silently; `ModLoader.SkippedEntries` records it so the test fails, closing the gap where the skip-guard hid the DamageResist null-key bug from the suite. **Rule — apply the Prime Directive to DATA, not just code:** when you add or change a cost/requirement/reference in any `GameData/**` JSON, check the *other end* in the same change — (1) the referenced id is actually **defined** (mineral/material/tech/component), (2) if a *starting* design needs it, the material is in that colony's **`StartingItems`** (or unlocked by a starting tech), and (3) **a NEW buildable design's own TEMPLATE id is itself in that colony's `StartingItems`** — a SEPARATE unlock from unlocking its materials, and the one that makes the template available in the faction data store. **Two unlock ends — don't conflate them.** (2026-07-06: the `ground-magazine` slice red-lit *every* colony-build test with `ground-magazine was not found in the faction data store` — the design + colony `ComponentDesigns` were added, materials were already unlocked, but the template id was missing from `StartingItems`. `ComponentDesignFromJson` throws that exact message when a design's `TemplateId` isn't an unlocked template.) Then run `dotnet test` — `BaseModIntegrityTests` is your sensor.

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

**Operationalize this with the systems map — every single time.** `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` is the connection map in table form, and it is not a document you read once. **Whenever you go into a system — to read it, change it, debug it, or even just decide whether to touch it — open the map first, find that system's row, and read its "Connected to" column and every row it points at.** That is the minimum blast radius; the four questions above extend it one hop further. When you finish, move that row's status and "Can we see it?" entry. If you hit a connection the map doesn't list, add it before you continue. The map is only worth keeping if it is consulted and updated on *every* system dive — that discipline is what stops the reactive pivoting that leaves a game half-built.

### Keep / Cut / Add / CONNECT — what you DO with the map (every system, every time)

The Prime Directive tells you to *map* the connections. This is the **action you take with that map whenever you touch any system** — combat, economy, UI, data, anything:

- **Keep** the parts that earn their weight (they're the source of a player decision — `docs/REALISM-VS-GAMEPLAY-AUDIT.md`).
- **Cut** — or hide behind a legible number — the parts that are "pretty": fidelity nobody acts on.
- **Add** the missing decision/lever.
- **Connect** — *the one that matters most* — wire the system into the others and **verify the stacked behavior, in code AND data.** A system is only as done as its connections: detection isn't done when "it detects," it's done when *what you can see decides the fight* (detection × weapons). The **data** half is the same move — when you add a cost/material/tech reference, check the *other end* (gotcha #10): the id is defined, and a starting design's material is stocked. **The real test is always the cross-system stack, never the unit in isolation.**
  - **Investigate before you wire — every connected system, every time (standing process).** When you touch a system, a flagged connection is a **work item, not a footnote**: go open the files on each system it connects to and produce an **EXISTS / MISSING / NEEDS-CHANGE** ledger (file:line, not assertions) *before* designing the change. This is the half of the Prime Directive that's easy to skip — mapping the connection but not looking at what's on the other end. It routinely overturns assumptions and finds half-built scaffolding to finish instead of rebuild. Worked example: `docs/DETECTION-DESIGN.md` §3c (EMCON traced across the emitter / control-heat / detection systems).

**This is how we improve.** Run Keep/Cut/Add/Connect on a system every time it's touched and the game gets tighter — pretty gets trimmed, levers get added, and the connections that turn "100 simple systems" into a *stacking* game get built and verified. Worked example: `docs/DETECTION-DESIGN.md` §3.

### Cradle to Grave — a system is real only if the player can reach it through the whole chain

**Connect is lateral (system × system). This is Connect made VERTICAL, and it is the acceptance test for "is this actually in the game":** every capability must be reachable and shaped by the player through the **full chain — from the mineral in the ground, to the decision on the battlefield, to the loss when it's destroyed.** Nothing is parachuted in as an engine abstraction the player can't research, build, deploy, or lose.

The chain (both ways):

> **mineral** (mined) → **material** (refined) → **production** (built at a colony) → **component** (designed in the designer) → gated by **research** (tech unlocks what you can design) → installed on a **unit/building** → the **in-play decision** (the lever) → **damaged/destroyed** (a component-level loss that *matters* — you re-research / re-mine / re-build).

This is **why** `CONVENTIONS.md` §6 ("abilities are components — do NOT invent parallel systems") is the law: modeling a capability as a component is exactly what gets you research-gating, construction-from-materials, save/load, and the design UI **for free** — i.e. it is what makes the capability *accessible from the base layer*. A capability that's a bespoke engine flag the player can't research/build/lose **fails cradle-to-grave** — the "pretty" disease, vertical.

**The acceptance test, every system, every time:** trace it cradle to grave — name the mineral, the material, the component, the research, the unit/building, the decision, and the loss. A missing rung is a design gap to fill (or a deliberate, written deferral), never a thing to skip. Worked example: `docs/DETECTION-DESIGN.md` — a sensor is a **component** (designed / researched / built / installed); the heat/active/fog *posture* is the **order**; a **destroyed** sensor blinds you (the grave rung, which wires detection to the damage system).

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

### ⚡ Pre-flight — run these SIX steps on EVERY task, before writing code (do not skip because the change "looks small")

This is the operational core of everything below, made scannable. If you do nothing else, do these — in order. They are the difference between a change that lands and one that breaks a system you weren't looking at. **They are not optional and they are not just for big tasks.**

1. **OPEN the subsystem `CLAUDE.md`** for the code you're touching (Subsystem Index above), AND `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` — find the row, read every system in its "Connected to" column. The map is the minimum blast radius.
2. **GREP before you trust.** Search for the type/method you're about to touch and read it in the source — do not build on a doc's *description*. Half the landmines here are **dead code that looks live** (`InstallationsDB`, `SimpleDamage`, `ViewModelLib`, dead UI). The docs have been wrong (gotcha #4); the source is the truth. See the **Landmine Index** below before wiring anything unfamiliar.
3. **WRITE the EXISTS / MISSING / NEEDS-CHANGE ledger** (file:line, not assertions) for each connected system — *before* designing the change. This is the step most likely to be skipped and the one that most often overturns a wrong assumption. If you can't cite the file:line, you haven't looked yet.
4. **NAME the cradle-to-grave chain** (mineral → material → component → research → unit → decision → loss) and the **gauge** (the test that proves it) *before* you build. A capability with no gauge is not done.
5. **EDIT, matching the conventions** you just read (`CONVENTIONS.md` + the subsystem idioms). Update the subsystem `CLAUDE.md` **in the same change** — and flip the affected rows in `docs/DOCS-INDEX.md` (and `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` / `docs/TESTING-TRACKER.md` as relevant) in that same commit, so the status dashboards never lag the code.
6. **ONE SLICE AT A TIME — push, then WAIT for CI green before building the next slice on top.** CI is the only correctness gauge (the SDK can't build locally) and it takes ~30 min. Do **not** stack commit N+1 on N until N is green — if N broke compilation, everything above it is built on sand. A verified base is worth the wait. Confirm both jobs green (`test` + `build-client`) before proceeding.

The numbered agreement below is the full-detail version of these six.

1. **Apply the Prime Directive — through the systems map.** Before touching any system, open `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, find its row, and read every system in its "Connected to" column (and their rows). Work those too — don't change a system in isolation. Update its status row when you're done. Map connections before deciding. See above.
2. **Read `CONVENTIONS.md` before writing any code; read the subsystem `CLAUDE.md` before working on that subsystem.** Only read source directly when the doc is insufficient, then update the doc after. For ground-combat/infrastructure design questions, consult `docs/aurora/`.
3. **Keep all CLAUDE.md files current** whenever code changes — update the subsystem CLAUDE.md in the same commit as the code it describes. Stale docs are worse than no docs.
4. **Build and run tests before and after every change.** Never leave the build broken. `dotnet build` + `dotnet test` before pushing. **CI gates three things now — keep all green: the `test` job (engine + NUnit), the `build-client` job (client COMPILES — added 2026-06-28, the net under client compile breaks), and your local *runtime* check of the client (CI can't run it — work `docs/CLIENT-TEST-CHECKLIST.md`).** Engine logic → CI tests it; client compile → CI's `build-client` catches it; client runtime/behavior → only your local build + `game_logs/` see it.
5. **Match existing conventions** (naming, `[JsonProperty]` discipline, `SafeDictionary`, processor auto-discovery pattern).
6. **Add tests for new systems.** Space combat has no tests; do not compound this pattern.
7. **Do not add features beyond what the task requires.** This is an ambitious codebase — scope creep compounds quickly.
8. **Update ARCHITECTURE.md** when data flow changes.
9. **Update this root CLAUDE.md** when a new subsystem is added, a subsystem moves, or a new gotcha is discovered.
