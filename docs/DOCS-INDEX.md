# Pulsar4X — Docs Index & Status Dashboard (LIVING — keep commit-current)

**What this is:** ONE place that lists *every* doc in the repo, what it's *for*, and its *status right now* — so anyone (a new session, a different model, the developer weeks later) can open this file and know where everything stands without spelunking. It is the doc-level twin of `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` (which tracks *systems*) and `docs/TESTING-TRACKER.md` (which tracks *tests*).

**THE RULE (not optional):** when a commit changes a doc — or changes code that makes a doc newly-true or newly-stale — update that doc's row here **in the same commit**. A row's *status* is the maintained truth (git already knows the last-touch sha; what git can't tell you is whether the content still reflects reality). Keep the "As of" stamp current on any substantive pass.

**As of:** 2026-07-03 · testing branch `claude/4x-game-testing-strategy-19xw8q` (merged to main across the session) · **2026-07-03 live-run results + a test-completion scorecard captured** in `docs/TESTING-TRACKER.md`: the New Game now auto-spawns the combat scenario, the **fleet UI is usable** (a three-attempt bug-hunt fixed the fleet-menu freeze), and **~50 % of the near-term runtime backlog is verified — none of the rest blocks design work**. The `game_logs/` folder is now git-tracked (crashes ship their own trace).

**Current focus (what's being worked on):** runtime hardening of the merged M-ECON/politics/combat layer is essentially done at the foundation level (boot, sim, movement, combat trigger, fleet UI all 🟢). **Design development can now begin in parallel** — next BUILD targets are ground combat (MVP M2) and stations (the space-economy pillar). Remaining test debt is a short PC queue (played-game save/load, Society-tab + economy-UI render, a real in-range fight, calibration re-dumps) — see `docs/TESTING-TRACKER.md` → the scorecard + ranked "what's genuinely LEFT". Latest landings: DevTools test levers, New-Game auto-scenario (4 rival factions), alpha stockpiles, a visual pass, and the fleet-menu crash fix.

---

## Status legend

| Mark | Meaning |
|------|---------|
| 🟢 CURRENT | content reflects HEAD; trustworthy as-is |
| 🟡 STALE | behind the code/plan — note says since-when + what changed |
| ♻️ LIVING | a ledger meant to be updated continuously (never "done") |
| 🔒 DESIGN-LOCKED | the design is agreed; **build state is tracked separately** in the Build column |
| 📚 REFERENCE | stable external/spec material; not "built," doesn't go stale from code |
| 🗄 SUPERSEDED | kept for history; a newer doc replaced it (pointer in notes) |

Build column (for design docs): ✅ built · 🏗 building · ⚫ design-only (not started) · ➕ partial.

---

## 1. Operational / process (the "how we work" + code-truth docs)

| Doc | Purpose | Status | Notes |
|-----|---------|--------|-------|
| `CLAUDE.md` (root) | Master dev reference: layout, conventions index, gotchas/Landmine Index, Prime Directive, pre-flight checklist, design-doc index | 🟢 ♻️ | Update the design-reference table + add a gotcha whenever a subsystem/landmine changes |
| `CONVENTIONS.md` | Pulsar's actual coding idioms (DataBlob/Clone, `*Atb`, processor auto-discovery, JSON discipline) | 🟢 📚 | Idioms are stable; touch only when a real convention changes |
| `ARCHITECTURE.md` | Hybrid-ECS data-flow diagram (Entity→DataBlob→Processor→…) | 🟢 | Pattern-level, still accurate; add a note if a new *kind* of data flow is introduced |
| `Pulsar4X/GameEngine/CLAUDE.md` | Engine-core subsystem ref (ECS infra, modding, save/load) | 🟢 | |
| `Pulsar4X/GameEngine/Colonies/CLAUDE.md` | Colonies/population/morale/manpower/economy/legitimacy | 🟢 | Updated this session (ManpowerTools, crew gate) |
| `Pulsar4X/GameEngine/Factions/CLAUDE.md` | Factions, government, diplomacy substrate + teeth + reactive drift | 🟢 | Updated this session (drift live, CrewPolicy wired) |
| `Pulsar4X/GameEngine/Combat/CLAUDE.md` | Auto-resolve combat engine + doctrine + IFF | 🟢 | |
| `Pulsar4X/GameEngine/Damage/CLAUDE.md` | Damage application (Complex is live; Simple dead) | 🟢 | |
| `Pulsar4X/GameEngine/Weapons/CLAUDE.md` | Beams, missiles, flak/railgun, weapon flavors | 🟢 | |
| `Pulsar4X/GameEngine/Sensors/CLAUDE.md` | EM signatures, scanning, contacts, first-contact hook | 🟢 | |
| `Pulsar4X/GameEngine/Movement/CLAUDE.md` | Newton thrust, warp, jump, pathfinding | 🟢 | |
| `Pulsar4X/GameEngine/Industry/CLAUDE.md` | Production, mining, materials, infrastructure efficiency | 🟢 | |
| `Pulsar4X/GameEngine/Fleets/CLAUDE.md` | Fleet grouping, fleet-as-one-icon | 🟢 | |
| `Pulsar4X/GameEngine/Logistics/CLAUDE.md` | Automated cargo routes + diplomacy access gate | 🟢 | |
| `Pulsar4X/GameEngine/Stations/CLAUDE.md` | Space stations (parallel host) | 🟢 | |
| `Pulsar4X/GameEngine/Hazards/CLAUDE.md` | Gas clouds / solar flares / space environments | 🟢 | |
| `Pulsar4X/GameEngine/Galaxy/CLAUDE.md` | System/body gen, atmosphere | 🟢 | System not changed recently — still current |
| `Pulsar4X/GameEngine/Tech/CLAUDE.md` | Research, scientists, tech unlocks | 🟢 | + `ResearchMultiplier` gov wire (small) |
| `Pulsar4X/GameEngine/People/CLAUDE.md` | Commanders, scientists | 🟢 | Officer/scientist talent-draw still a future slice |
| `Pulsar4X/Pulsar4X.Client/CLAUDE.md` | UI reference + the RUNTIME-CI-blind discipline + DevTools inventory | 🟢 | Updated this session (DevTools government lever + diplomacy readout) |
| `Pulsar4X/Pulsar4X.Tests/CLAUDE.md` | Test harness + full test inventory | 🟢 | Updated this session (Manpower/Morale/Diplomacy-drift/SocietyReadout tests) |
| `.claude/commands/build-check.md` · `damage-audit.md` · `phase-status.md` | Slash-command / skill definitions | 🟢 📚 | Tooling; touch only when the workflow changes |

## 2. Vision & the two firewalls (why + what-not-to-do)

| Doc | Purpose | Status | Notes |
|-----|---------|--------|-------|
| `docs/NORTH-STAR-VISION.md` | The ceiling: stage aspects of the great sci-fi universes; the extracted-mechanics pillars | 🟢 📚 | Raises the ceiling; does not lower the bar |
| `OBJECTIVE.md` | The concrete end-state spec (what "done" looks like, build order) | 🟢 📚 | Goal-level; stable |
| `docs/MVP.md` | **Scope firewall** — the v1 finish line ("you can take a planet") + parking lot | 🟢 ♻️ | The live plan (superseded `PLAN.md`); read before adding any feature |
| `docs/REALISM-VS-GAMEPLAY-AUDIT.md` | **Weight firewall** — every system must be the source of a stacking player DECISION; the cheap-wiring list | 🟢 ♻️ | Framework still governs; the verdict board is the thing to refresh as systems get wired |
| `docs/BEYOND-PROTOCOL-REFERENCE.md` | Second design north-star (the strategic/human layer alongside Aurora's physics) | 🟢 📚 | |

## 3. Living status ledgers (the dashboards)

| Doc | Purpose | Status | Notes |
|-----|---------|--------|-------|
| `docs/DOCS-INDEX.md` | **This file** — every doc's purpose + status | ♻️ 🟢 | Keep commit-current (the rule at the top) |
| `docs/TESTING-TRACKER.md` | Standing global test ledger (CI layers + the Layer-3 PC-test queue), 7 fields/row | ♻️ 🟢 | CURRENT — **now holds the 2026-07-03 LIVE-RUN RESULTS + a test-completion SCORECARD (~50 % of the near-term runtime backlog verified, none of the rest blocking) + the diagnostic-methodology lessons** from the fleet-menu bug-hunt. Prior 2026-07-02 results retained above it |
| `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` | Living map of every SYSTEM, its state + gauge + "connected to" | ♻️ 🟡 | **Partly refreshed 2026-07-03** (the UI-client row now 🟢 runtime-confirmed with the fleet-menu fix + game_logs tracking). Still STALE for the morale/gov/diplomacy/crew/stations *system* rows since 2026-06-29 — TESTING-TRACKER is authoritative for status meanwhile |
| `docs/CLIENT-TEST-CHECKLIST.md` | This branch's client RUNTIME test scratchpad (what only the PC build can check) | ♻️ 🟢 | Refreshed 2026-07-04 — **new G5 tactical-map block (units draw / click-to-move / click-to-place / terrain+hazards)** on top of the ground map (G3 planet view, G4 survey-reveal) + space stations (deploy/manage/pooled-materials); Fleet UX PASSED + prior confirmed-live section retained |
| `SESSION_STATE.md` | Session-close handoff ("read first after CLAUDE.md") + play-by-play + lessons | 🟢 | Refreshed 2026-07-02 with the current branch state (older sessions collapsed under a details fold) |

## 4. Design docs (each carries a build state)

| Doc | Purpose | Status | Build | Notes |
|-----|---------|--------|-------|-------|
| `docs/SPACE-STATIONS-DESIGN.md` | Station-as-universal-off-world-host (parallel to colony) | 🔒 | ✅ | Cradle-to-grave complete + green: grave rung (B) + front door (A/A2 construction-ship) + cost/upkeep (C) + Lagrange anchors (D) + listening-outpost sensor (E) + fleet-pooled materials (F) + deploy tests on the real order-handler path. Remaining: fleet-battle targeting, invasion, tuning |
| `docs/ENVIRONMENTS-DESIGN.md` | Space hazards + planetary terrain as ONE physics-driven system: exotic hazard catalog, physics→hazard generation, the 6 meta-mechanics | 🔒 | 🚧 | **INFRASTRUCTURE BUILT (E1–E3, 2026-07-04): region-hosted typed effects (reuse `HazardEffectType`) + the physics→hazard generator (gas-giant-gated) + per-tick attrition — so "more environments" is now DATA, not engine code. E4/E5/transience + the exotic-catalog sweep are the later work.** Design below the build status: Terrain = the ground twin of a space hazard (share the vocabulary, don't refactor the green engine); environments are exotic + generated from planet physics (temp/atmosphere/tectonics/body-type/orbit), **gas-giant-gated**. Holds the creative sweep (6 meta-mechanics + exotic catalog) + the E1–E5 build plan. Co-fixes the space side's flagged flat-RNG placement |
| `docs/GROUND-COMBAT-MAP-DESIGN.md` | The planet map + ground combat: region-graph (ring) + units; features/exploration; the sub-slice roadmap | 🔒 | 🚧 | **Design-locked; slices 1–4 + 5a–5g + 5e built.** map: `PlanetRegionsDB` ✅ · build-at-region ✅ · `PlanetViewWindow` **tactical ✅** (units + click-to-move + click-to-place + terrain/hazards; runtime G5) · survey-reveal ✅. **Ground combat scoped to FULL TACTICAL** (sub-slices 5a–5i); **5a–5g built** (`GameEngine/GroundCombat/`): raise ✅ · move ✅ · fight ✅ · **CAPTURE — "take a planet" ✅** · terrain-as-leverage ✅ · unit types + triangle ✅, all CI-gauged (`GroundForcesTests`); **5e tactical map (client) built** (CI-compiled, runtime-local); **5h fortification + FORMATIONS + formation CLIENT + STANCE built** (region buildings fortify the defender; `GroundFormation` mirrors the fleet — name/leader/members/move-as-one/leader-reassign + a tactical-map Formations panel; **moddable stance catalog** `GroundStanceBlueprint`/`groundStances.json` with the ±25% Offensive/Dig-In/Balanced trade-off, gauges `Formation_MirrorsFleet_*` + `FormationStance_*`) + **stance CLIENT selector** + **DESIGN-DRIVEN fortification** (a `GroundDefenseAtb` component + a real base-mod **Bunker** installation: +25% local / +12% adjacent-friendly, capped; only defence buildings fortify — gauges `Fortification_DesignDriven_*` + `Fortification_BaseModBunker_*`) + **LOCATE existing installations** (`GroundInstallations` hooks `ColonyFactory` so the start colony's buildings get a capital region → draw on the map + count for fortification, gauge `Installations_LocatedInCapitalRegion_*`) + **default HOME GARRISON** (`GroundStartGarrison` raises 3 Inf / 2 Armor / 1 Arty on Earth every New Game via `NewGameMenu`, so the tactical map isn't empty — gauge `StartGarrison_*`). Next: locate dynamically-built installations; formation sub-nesting; the "40k" depth pass |
| `docs/HEX-GROUND-AND-ORDERS-DESIGN.md` | **NEW 2026-07-04.** The next evolution: planet regions made of **HEXes** (Planet→Region→Hex, ~1800 on Earth, lazy-gen) so formations move hex-by-hex (London→Paris) with terrain/hazards on hexes; + a rich Aurora-style **order catalog** (~60) for fleets AND formations. Locked: Planet→Region→Hex, Operational density, coords-only, LAZY hex gen. Two-track roadmap H1–H5 (ground hex) + O1–O3 (orders). | 🔒 | 🚧 | **Design-locked; H1 (hex data layer) + H2 (hex movement/pathfinding) built** (`GroundHex` + `Region.Hexes` + `PlanetHexFactory` lazy gen; `HexPathfinder` A\* terrain-weighted + `GroundUnit` hex position + `OrderMoveToHex`/processor hex-walk — gauges `PlanetRegionsTests.Hex*` + `GroundForcesTests.HexPath_*`/`OrderMoveToHex_*`; **water passability LOCKED H2b: ocean impassable, ice passable**; **H3 range-based directed combat built** — per-unit hex `Range`, out-ranger hits closer unit first (clone-vs-zerg), co-located = old resolver, `GroundForcesTests.RangeCombat_*`; **H4 client hex render built** — PlanetViewWindow ⬡ Hex view toggle: terrain-coloured hex grid, unit markers, click-a-hex-to-march via OrderMoveToHex, range readout). Next: H3 follow-ons (hex-terrain-in-combat, per-hex hazards, commander ROE); O1 order-catalog |
| `docs/GLOBAL-HEX-GRID-DESIGN.md` | **NEW 2026-07-04.** The developer's "real fix" for a continuous world: replace the four per-region hex DISKS with **ONE cylinder hex grid** (Q=longitude wraps, R=latitude bounded); "region" becomes a **column-band label**. Terrain continuous+wrapping by construction (V2 field fed global lon/lat), movement = global A\* (no edge gates), client = a sliding window over the cylinder (any place shows in every view reaching its longitude). Coarse `PlanetRegionsDB` ring kept (ownership/area/crossing-time). Migration G1–G6 (additive grid → global pathfinding → units → W1/C1 → client → retire disks). | 🔒 | 🚧 | **Design captured; building engine-first (G1 additive grid first).** Supersedes the H1 disk model. |
| `docs/GROUND-CITY-AND-WARMAP-DESIGN.md` | **NEW 2026-07-04.** Two zooms, one physical thing (fleet-plot↔damage-diagram analogy): the **War-map layer** (a strategic building occupies an operational hex → capture/bombard target — `GroundHex.InstallationIds` + a `GroundFootprintAtb` "occupies-a-tile" flag; build NOW, ~1 slice) and the **City sub-grid** (lazy fine hex grid per developed hex, 1:1 building placement + adjacency economy; the big builder, build AFTER). Completes the LOCKED PRINCIPLE all the way down — gives the ground war something worth fighting over. | 🔒 | 🚧 | **W-track (war-map layer) BUILT + CI-gauged** — `GroundHex.InstallationIds` + `GroundFootprintAtb` (the Bunker is a footprint) + `GroundBuildings` (locate-on-hex / capture-captures-contents / bombard-damages-contents), wired into ColonyFactory + GroundForcesProcessor; gauges `GroundForcesTests.WarMap_*`. **C1 (city fine-grid data layer) BUILT + CI-gauged** — `CityTile`/`CityGrid` (save-safe) + `GroundHex.CityGrid` (lazy) + `CityGridFactory` (127-tile fine disk per developed hex) + `CityBuilder` (place/remove on a tile keeping the roll-up invariant `InstallationIds`==placed set; `DevelopColonyHex`; bombard clears the fine tile too); gauges `CityGridTests`. Next C2 (placement economy: terrain affinity + adjacency) → C3 (client zoom view). W-track auto-bombard→hex + client draw still deferred. |
| `docs/UNIVERSAL-ASSEMBLY-DESIGN.md` | **NEW 2026-07-05. Cross-cutting PRINCIPLE — governs the whole build system.** Everything buildable (unit / ship / installation / station / super-weapon / world-ship) is the SAME kind of object: a **chassis (structural budget) + components (consume budget, contribute stats)**, stats emergent from the sum, gated by physics, at any scale, within reason (tech+scale caps). Differ by number & scale, not kind. Mostly UNIFICATION not rewrite — components are already universal (`CONVENTIONS §6`), ships are already assemblies, `IConstructableDesign` is the seam, and the ground assembler (`GroundUnitAssembly`) is the general-purpose core. Non-breaking convergence roadmap: record → harden on ground → generalize the assembler+gate → extend the scale ladder → converge the designer UX. | 🔒(principle) | 🚧 | **Principle locked; realized at unit scale (ground G-D track). Survey + roadmap recorded. §5 flags the open per-domain-gate / mobility-flag / scale-cap / UX-convergence questions.** |
| `docs/WEAPON-UNIFICATION-DESIGN.md` | **NEW 2026-07-05. Design draft (not built) — the design-before-code step for the weapon half of the universal-assembly principle (task #3).** Fixes the deep discrepancy: ground uses `GroundWeaponAtb` (Attack/Range/Mode) read by the `GroundForcesProcessor` hex resolver, a PARALLEL system to the space weapon atbs (beam/railgun/flak) read by `ShipCombatValueDB` auto-resolve — so an "energy weapon" exists twice. Key finding: the space side already normalises every weapon into ONE `WeaponProfile`/`WeaponClass` — the natural convergence point. Recommends approach **A** (ground reads a weapon's `WeaponProfile`; `GroundWeaponMode`⇄`WeaponClass` table; hex⇄metric via `GroundRangeTools.HexPitchKm`), a 5-slice phased plan, and the open questions (fidelity, dps→Attack calibration, one-design-vs-one-type) needing the developer before code. The visible half (one designer **category**) is already done (`UNIVERSAL-ASSEMBLY-DESIGN.md` §2a). **§0: decisions LOCKED 2026-07-06 — ONE designer, FULL fidelity (weapon triangle intrinsic, ground+space), one design mounts anywhere the chassis can SUPPLY it, DELETE the ground weapon system, multiplicative growth. Revised 6-phase plan P0–P5.** | 🔒(locked) | 🚧 | **Decisions locked; P0 (multiplicative growth) + P1 (five direct-fire weapons carry `ComponentMountType.GroundUnit`; gauge `WeaponGroundMountTests`) + P2a (the three fuel-burning reactors carry it too — power is a mounted part; gauge `PowerPlantGroundMountTests`) done. P2 supply-gate model DECIDED (reactor-as-component + hard gate); P2b (assembler's Σ-draw ≤ Σ-reactor-output gate) + P3–P5 (ground reads the triangle → delete ground weapons → generalise) remain.** |
| `docs/WEAPON-TAXONOMY-DESIGN.md` | **NEW 2026-07-06. The decision tree, surveyed against ~30 weapons across ~15 sci-fi franchises** — feeds the weapon unification (which "types" the ONE designer must offer). **Headline finding:** the current `WeaponClass{Beam,Railgun,Missile,Flak}` FUSES two independent axes — **damage NATURE** (Kinetic/Energy/Explosive/**Exotic**, meets the defence) × **delivery PHYSICS** (Beam/**Bolt**/Slug/Cloud/Guided/**Blast**, meets the dodge) — which is *why a blaster pistol (slow, dodgeable ENERGY) can't be built* (Beam is hard-wired ~c). Fix: pick Nature × Delivery, dial specs, and the **triangle position EMERGES** (`WeaponClass` becomes a computed readout; a build can sit BETWEEN corners — the multi-barrel coilgun). Gaps: no slow-energy Bolt, no Exotic (EMP/stun/anti-shield/matter-strip), no AoE Blast. Same tree generalises to armour/sensors/propulsion/power. | 📝(survey) | ⏳ | **Survey done; §5 open questions (confirm 2-axis split, v1 Nature/Delivery sets, how Exotic effects model) gate the P1 tree-widening.** |
| `docs/GROUND-UNIT-DESIGNER-DESIGN.md` | **NEW 2026-07-05.** How a player CREATES a ground unit — assemble from components like a ship (LOOSE, no unit classes), with the one rule ships lack: a **carry-capacity gate** (frame strength + augment bonuses vs. part mass; a bare human can't lift the autocannon, power armour lets it). Five general part types (Frame/Weapon/Armor/Augment/Utility+Supply) generalised by FUNCTION not flavour; stats + cost EMERGE from the sum; build-quantity → one `Count` "bunch" (not 1000 objects). Validated by a 6-unit cross-franchise stress-test (Marine/Knight/Zergling/Siege Tank/AT-AT/Jedi from one parts bin). | 🔒(core) | 🚧 | **Core model LOCKED; G-D1 (part attributes) + G-D2 (base-mod parts + `ComponentMountType.GroundUnit`) built + CI-gauged (`GroundUnitPartsTests`/`GroundUnitPartsBaseModTests`).** Next G-D3 (the assembler + the capacity/max-item gate). **§10 flags what's still to design: the component-designer UI, the assembly window, the max-item fraction, quantity UX.** |
| `docs/MORALE-AND-POPULATION-DESIGN.md` | Morale as the population-tank valve; people as a finite draw (M1–M5) | 🔒 | ✅ | M1–M5 built + green this session. Open = employment numbers + base-low **calibration** (PC) |
| `docs/GOVERNMENT-AND-POLITICS-DESIGN.md` | Government-as-modulator (dials) + internal politics (legitimacy/rebellion/demands) | 🔒 | ➕ | Government dials ✅ wired; legitimacy ✅ live; rebellion first rung ✅. Demands-engine + Interior Minister ⚫ (blocked on delegation) |
| `docs/DIPLOMACY-DESIGN.md` | External politics: relationship track, IFF, first-contact, treaties, casus belli, reactive engine | 🔒 | ➕ | Substrate + teeth + first-contact + commerce + reactive DRIFT ✅. Commitment executor / treaty-proposal policy / negotiation UI ⚫ future |
| `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md` | The delegate layer (ministers/governors/admirals) — "play at your own altitude" | 🔒 | ⚫ | **NOT built — the key blocker** for Foreign/Interior Ministers (and thus much of politics) |
| `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` | Hidden-info engine: Information Ledger, agents, covert-action catalog, counter-intel (cluster D–H) | 🔒 | ⚫ | NOT built |
| `docs/COMBAT-DESIGN.md` | Master space-combat design (11 systems) + fleet components/doctrine | 🔒 | ➕ | Auto-resolve spine + doctrine ✅; some systems partial |
| `docs/FLEET-COMBAT-CLOSING-DESIGN.md` | Closing-fight (range/speed/detection/doctrine decide who hits whom) + ROE | 🔒 | ➕ | Most phases built (range, aggregation, first-shot, EMCON) |
| `docs/WEAPONS-AND-DODGE-DESIGN.md` | Weapon flavors + dodge + the weapon triangle | 🔒 | ✅ | Built + gauged (triangle, saturation, evasion) |
| `docs/DETECTION-DESIGN.md` | Sensors/detection/fog-of-war/EMCON as a decision (dark-vs-loud) | 🔒 | ✅ | EMCON posture + fog + first-strike built |
| `docs/INFORMATION-DELTA-DESIGN.md` | The gap between what the sim KNOWS and SHOWS; the readout backlog | 🔒 ♻️ | ➕ | Engagement/detection/Δv/ETA readouts built; more to wire |
| `docs/RESOURCES-AND-MATERIALS-DESIGN.md` | Full economy survey (minerals→materials→production→commerce→NPC econ) | 🔒 📚 | ➕ | Economy largely built; read before touching any econ part |
| `docs/COLONY-PROGRESSION-DESIGN.md` | The colony/infrastructure progression ladder (vision) | 🔒 | ⚫ | Mostly design |
| `docs/HAZARD-DISCOVERY-RESISTANCE-RESEARCH-DESIGN.md` | Hazard discovery → resistance research → armor loop | 🔒 | ✅ | Built (hazards + research→armor) |
| `docs/STELLAR-ENVIRONMENTS-CATALOG.md` | Catalog of stellar/space environments | 📚 | — | Reference catalog |

## 5. Aurora 4X design reference (static external spec — `docs/aurora/`)

| Doc | Purpose | Status |
|-----|---------|--------|
| `docs/aurora/INDEX.md` | Index + the "reference not spec; verify constants" caveat | 📚 |
| `GROUND-COMBAT.md` · `PLANETARY-INFRASTRUCTURE.md` · `SPACE-COMBAT-BENCHMARK.md` · `SHIP-DESIGN.md` · `MISSILES-AND-FIRE-CONTROL.md` · `SENSORS-AND-DETECTION.md` · `RESEARCH-AND-TECH.md` · `FLEETS-AND-SHIPYARDS.md` · `LOGISTICS.md` · `EXPLORATION-AND-SURVEY.md` · `COMMANDERS-AND-OFFICERS.md` · `COLONY-ENVIRONMENT-AND-POPULATION.md` · `DIPLOMACY-AND-INTEL.md` | Aurora's mechanics captured as the design spec for systems Pulsar lacks/deepens | 📚 (all) |

> These are **static reference**, not tracked for "build status" — they're the external spec we diff against. The *shape* is reliable; specific *constants* are approximate (verify before hard-coding).

## 6. Superseded / historical (kept for context, do not follow as live)

| Doc | Purpose | Status | Notes |
|-----|---------|--------|-------|
| `PLAN.md` | The original pre-rework development plan | 🗄 | **SUPERSEDED (2026-06-26) → `docs/MVP.md`** (already carries a banner) |
| `docs/AURORA-GAP-ANALYSIS.md` | 2026-06-21 snapshot of Pulsar vs Aurora depth | 🗄 📚 | Historical; the live system map is `SYSTEMS-STATUS-AND-TEST-PLAN.md` |

---

## Known doc-debt (the honest to-do, so it isn't lost)

1. **`SYSTEMS-STATUS-AND-TEST-PLAN.md` (🟡)** — refresh the rows for morale/population, government, diplomacy, crew/manpower, stations to their built state (they landed after 2026-06-29). A stale-banner is in place pointing to the tracker meanwhile; the per-row refresh is the remaining work.
2. When **delegation** or **espionage** starts, flip their Build column here 🏗 and add their systems to the systems map.
