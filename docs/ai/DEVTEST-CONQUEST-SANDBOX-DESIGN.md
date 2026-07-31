# DevTest Conquest Sandbox — Design + Build Plan

**Status:** 🔒 DESIGN-LOCKED · Build 🟡 B2a+B2b landing (engine chain CI-green; full 3-faction sandbox + DevTest button pushed for validation) · branch `claude/devtest-faction-design-xpfnhe`
**As of:** 2026-07-13

> **Build log.** **B4 + B5 (AI observability + AI ON, 2026-07-14):** **B4a (engine, the flight recorder):** `AIDecisionRecordDB` (per-faction ring buffer, cap 60) + `AIDecisionRecorder.Record` (called from `NPCDecisionProcessor.Tick`, always-on) tape every NPC cycle's SENSED (strength/threat/morale) · DECIDED (objective+why) · ACTED; `PlanReadout.DecisionTape/DecisionLine` render `[AI]` lines. Gauge `AIDecisionRecorderTests`. **B4b (client, the two surfaces):** `SessionLog.AiDecisionSnapshot` flushes each NPC's new decisions to `game_logs/` as `[AI]` lines from the ~3 s heartbeat (per-faction timestamp dedup); `AIInspectorWindow` (toolbar button) is the live view of the same tape. **B5 (AI ON):** `DevTestGame()` now flips `EnableOrderEmission`/`EnableDiplomaticProposals`/`EnableEspionageMirror`/`EnableIntelLedger` ON after load — the AI plays for real, every action taped. Client is compile-only in CI; the **runtime** (AI acting + the Inspector + `[AI]` lines) is the next PC test. **B2b tuning pass (post PC-1 boot, 2026-07-14):** first live boot was clean (`faults=0`) — 3 factions loaded, Martian fleet real, fog working. Four corrections from the developer's play: (1) faction name printed blank → `LoadFromJson` now reads an `"abbreviation"` field (UEF/UMF/KTH) since `CreateFaction` leaves it empty and the UI keys off it; (2) unsurveyed worlds (Luna/Venus) showed their full surface → `PlanetViewWindow` now gates the globe on `regions.Any(r => r.Surveyed)` (home starts surveyed; a fogged world stays hidden until scanned); (3) "everything enabled" was read too literally — the player had all 123 templates unlocked, skipping the research system → the player's `startingItems` trimmed 123→73 (option B: all materials + basic infra/power/engine/sensor/shipbuilding kept so the design→build→**entity** loop works turn one, the 50 military/advanced templates — weapons, ground units, warp, shields, espionage — now researched). The homeworld sensor stays (developer's call). NPCs keep full unlocks (they're pre-developed by design). **B2a (engine, CI-green):** the scenario-authoring additions — `FactionFactory.LoadFromJson` modernized to resolve designs/species BY ID from the mod store, a faction-level `startingItems` unlock ("everything enabled"), inline `stations` + `strain` parsers; `DevTestStartFactory.CreateDevTest` (loads Sol via the live blueprint path, loops the faction files, second-pass applies opening war + strain). **B2b (data + test + client, pushed for validation):** the three scenario files — `uef-devtest.json` (player UEF: full 123-id startingItems, 9 infra designs, one Earth colony, NOTHING pre-built), `umf.json` (United Martian Federation — NPC, Mars/Luna/Venus/Ceres colonies, scattered gunship fleets, war-tax + sustenance strain, at war with UEF), `kithrin.json` (Kithrin Collective — NPC, species-xenos, a developed Titan station); `DevTestScenarioTests` (player-faction load + full 3-faction war/strain/station gauge); and the client `NewGameMenu.DevTestGame()` behind the relabeled **"DevTest"** main-menu button (Quickstart replaced). **AI action gates stay OFF for this first bootable slice** — the sandbox WORLD loads and is playable; turning the AI loose is paired with the flight-recorder (B4) so the AI is observable when it acts. **Next: PC-1 boot test** (does the 3-faction Sol sandbox boot + is it playable), then B3 (UMF/Kithrin dialed designs) / B4 (AI recorder + loop) / B5+ (conquest loop + Phase A→D).

---

## 1. What this is, and why (plain English)

Replace the **Quickstart** button with a **DevTest** button. DevTest boots the hardest, most honest test we can give the game: **you start with almost nothing and the goal is to conquer the solar system** — against **two AI factions that are trying to do the same thing.** It is the integration test for everything built over the last several branches: research, the component/ship/ground designers, economy, fleets, space combat, detection/fog, the ground war, diplomacy, and the NPC AI — *connected*, not in isolation.

The point is not a scored victory screen. The point is: **does the infrastructure actually support a start-from-scratch conquest, in both directions?** Where it doesn't, the test shows us exactly which connection is missing, and we build that.

Three things make it a real test rather than a diorama:

1. **You (Earth / UEF) start minimal.** Everything is *unlocked to design*, but *nothing is built for you* — no stock ship or weapon designs, no fleets in orbit. A minimal colony that can mine → refine → build, and Earth is the only surveyed body. You must go through every system yourself.
2. **The two AI factions are pre-established powers with their own materiel.** They are the only pre-authored content in the game, so their ships/units/infrastructure carry the entire identity of both factions.
3. **The AI plays for real.** The factions must *act* on what they see — build, mass, move, invade, expand — through the same order rail you use. Making that true is the bulk of the work (see §4).

---

## 2. The situation (what DevTest authors at t=0)

| Actor | State |
|---|---|
| **Player — United Earth Federation (UEF)** | Minimal Earth colony (mine/refinery/factory/construction/**passive-sensor**/city-hall/infrastructure/research-lab). Full `StartingItems` unlock list kept (everything designable); **zero** pre-built ship/weapon designs, **zero** fleets. Only Earth geo-surveyed. A functional-but-modest population (a few hundred million — enough to crew builds). |
| **United Martian Federation (UMF)** | Human, kinetic-industrial power spread across the **inner** system (Mars capital + Luna + Venus + a Ceres/asteroid outpost). **At war with UEF; the two are mutually aware, nobody else.** Battered by a long war: **low stockpiles, scattered + understrength + lightly-damaged fleets, low colony morale** (authored via war-strain inputs). Its needs-ladder therefore pins it to *Stabilize* — it rebuilds before it invades. |
| **Kithrin Concord** | Xe-breathing alien, energy/shield/exotic, **expansionist**. A **highly-developed station outpost** in the **outer** system + 1–2 smaller outposts (so it reads as already spreading). Unknown to everyone at start — discovered only when a ship reaches the outer system (first contact fires). |

**Detection is always a component.** Earth "sees the inner system" only because it has the passive-sensor installation; that is why UEF and UMF detect each other's inner-system ships. The outer system is dark until you send a hull out. No free sensor bubbles for anyone.

**The AI is switched on.** Part of "everything enabled" is flipping the AI's default-off action gates (`EnableOrderEmission`, `EnableDiplomaticProposals`, `EnableEspionageMirror`) so the factions actually act.

### Why the single-system scope matters
Everything is in **Sol** (one star system). That removes the two worst structural AI blockers for free: **jump-point / multi-system movement** (never needed) and **most survey gating** (the factions start pre-established and pre-surveyed on what they hold). The AI program in §4 is scoped by this.

---

## 3. The two factions' materiel — DESIGNS, not new mechanics

**Key correction (locked):** the component/ship/ground **designer already spans the whole mechanic space** (verified against `GameData/basemod/TemplateFiles/`). A faction's distinct kit is **new DESIGNS** — dialed instances of existing templates with faction names — **not** new component templates or new C# ability classes. Same act the player performs with the designer, so designing the factions doubles as an exercise of that designer.

A design is just:
```json
{ "UniqueId": "umf-mass-driver", "Name": "Mass Driver", "TemplateId": "railgun-weapon",
  "Properties": [ { "Key": "Kinetic Energy Per Shot", "Value": 350000 } ] }
{ "UniqueID": "umf-ship-ironhold", "Name": "UMFS Ironhold",
  "Armor": { "Id": "ablative-composite-armor", "Thickness": 6 },
  "Components": [ { "Id": "umf-mass-driver", "Amount": 4 }, … ] }
```

**Every offensive weapon template maps to a class the AI's warship filter already recognizes** (`laser/pulse-laser→GenericBeamWeaponAtb`, `railgun/siege-railgun→RailgunWeaponAtb`, `flak→FlakWeaponAtb`, `plasma-repeater→PlasmaBoltWeaponAtb`, `disruptor→DisruptorWeaponAtb`, `missile-launcher→MissileLauncherAtb`). So **the AI builds both navies for free — zero `IsWarship` edits.**

### UMF — kinetic, heavy, cheap, rugged
| Design | Template (existing) | Dial intent |
|---|---|---|
| Mass Driver | `railgun-weapon` | Kinetic Energy ↑ ~350k, Muzzle Velocity low (50k) — hits hard, slow, cheap |
| Chain-Flak | `flak-weapon` | high rounds/pellets, low dmg — PD wall |
| Siege Driver | `siege-railgun` | very high Kinetic Energy + penetration — anti-armor monitor gun |
| *(armor)* | `ablative-composite-armor` / `nickel-steel-armor` | thick, cheap plating (ship `Armor` field) |
| *(power/drive/warp)* | `reactor`, `conventional-engine`/`scntr`, `alcubierre-warp` | reused as-is |
| **UMFS Ironhold** (line cruiser) | 4× Mass Driver + composite armor + sensor + FC + 2 reactor + NTR + warp | AI mass-builds |
| **UMFS Bulwark** (flak escort) | 4× Chain-Flak + pd-director + reactor + NTR + warp | AI mass-builds |
| **UMFS Rampart** (monitor) | 3× Siege Driver + heavy armor + reactor + warp | AI mass-builds |
| **UMFS Anvil** (troop dropship) | troop-bays + light armor, **unarmed** | needs invasion role |
| **UMFS Mule** (freighter) | cargo holds + fuel + warp | needs logistics role |
| Ground army | Marine (`human-frame`+`ground-rifle`+`ground-plating`), Siege Tank (`vehicle-frame`+`ground-cannon`+`reactive-plating`), Gun Battery (`walker-frame`+`ground-autocannon`) | needs invasion role |
| Infrastructure | Mars: mine/refinery/factory/**shipyard**/research-lab/infrastructure/city-hall/**bunker**/command-berth. Luna/Venus/Ceres: mine/refinery/infrastructure (light) | — |

### Kithrin — energy, shielded, exotic, station-borne
| Design | Template (existing) | Dial intent |
|---|---|---|
| Energy Lance | `laser-weapon` | Range ↑ to the tech ceiling (10,000), high power — long-reach precision |
| Phase Disruptor | `disruptor-weapon` | Energy/Shot ↑ — anti-shield exotic |
| Plasma Caster | `plasma-repeater` | energy bolts for skirmishers |
| Resonance Shield | `deflector-array` | Shield Capacity ↑ ~15M, Recharge ↑ |
| Ward Lattice | `armour-hardening` | SoakVsEnergy high — plating tuned vs energy fire |
| **Radiance** (energy cruiser) | 3× Energy Lance + 2× Resonance Shield + Ward armor + reactor + warp | AI mass-builds |
| **Umbra** (disruptor skirmisher) | Phase Disruptors + shield + `inertialess-drive` + warp | AI mass-builds |
| **Sable** (scout) | strong passive-sensor + geo/grav surveyor, **unarmed** | needs scout role |
| **Seedship** (expansion) | cargo + surveyor, **unarmed** | needs expansion role |
| Ground (swarm) | Swarm-form (`swarm-frame`+`claw-weapon`), Ward-Guard (`swarm-frame`+`energy-weapon`+`reflex-booster`) | needs invasion role |
| Infrastructure (stations) | Nexus outpost = `space-habitat` (Xe life-support) + mine + refinery + research-lab + factory + passive-sensor + reactor/solar. 1–2 smaller = habitat + mine | — |

**Rule enforced by the design:** troop transports / freighters / scouts / seedships stay **unarmed**, so the AI's warship filter never mistakes them for combat hulls (and, until the missing roles exist, they are only fielded via the authored start, not mass-produced).

---

## 4. The AI — one consolidated **sense → decide → act** loop

### The finding
The AI's *skeleton* is the right shape: monthly, per faction, `NPCDecisionProcessor` runs a needs-ladder → picks an objective → a resolver emits an order, colored by `PersonalityDB`. What's wrong is **content, not architecture**: the resolvers are nearly empty, choices are *first-match* not *best*, whole domains have no resolver, and the action gates are off. **Every verb the AI lacks already has a working player order class** (`GeoSurveyOrder`, `LoadTroopsOrder`, `RefuelAction`, `WarpMoveCommand`…) — the AI just never calls them. So this is *fill the skeleton in*, not a rewrite.

### The consolidated mechanism
Build **one uniform loop every domain plugs into**, like a fire-control loop: **sense → decide → act.**

- **Sense** — a consolidated, **fog-aware** situational read: what this faction actually perceives (sensor contacts, `InformationLedger`, surveyed bodies, `KnownSystems`, own economy/military via `FactionState`). It must *not* be omniscient — the AI competes on the same footing as the player. This "sense" half barely exists as a consolidated thing today (the AI docs flag a "fog-limited enemy-strength estimate" as a piece to build); building it is part of the work.
- **Decide** — the needs-ladder (exists) + a **shared scorer** (best-not-first, personality-weighted). Generalize the one existing scoring pattern (`MilitaryTarget.BestEnemyTarget`) to score *designs* and *actions*, not just enemy targets.
- **Act** — a small **AI action library**: one thin helper per verb (`AiSurvey`, `AiMoveFleet`, `AiInvade`, `AiRefuel`, `AiDeclareWar`, `AiResearch`…) that decides *when/where* and calls the **player's existing order**. The AI plays through the same rail you do → the test stays fair.

Build the loop once; then each domain is a small module: *"here's what I perceive → here's the best-scored option → here's the order."*

### What the AI genuinely needs for *this* test (ranked, single-system-scoped)
1. **Flip the action gates on** — trivial; gives the AI its whole current vocabulary at once. *(scenario stand-up)*
2. **Ground war** — raise → load → land → invade → capture. **The keystone.** Nobody can take territory without it, so conquest is impossible for AI-or-player-as-target. Build first.
3. **Fuel / rearm at a friendly colony** — else a fleet sorties once and dies empty.
4. **A latched aggression target / declare-war** — UMF is *authored* at war (has one ✅); Kithrin needs a proactive war-declaration rule to ever turn hostile.
5. **In-Sol expansion onto seeded bodies** — pre-survey a couple outer bodies so the existing found-colony resolver can settle them (Kithrin spreading); true survey-and-settle is later.

### Can live without for the first playable (polish tier)
Research-picking, station-growth (Kithrin starts developed), logistics routes, field-sites/berths, espionage beyond gather, government tuning, EMCON/attack/retreat maneuver finesse, queue reprioritization, jump-point crossing (irrelevant in one system). All real gaps; none block a first end-to-end run. The test itself tells us which actually hurt.

### The development loop — observability is the SPINE, not a feature
The single most important thing in this whole program is **seeing what the AI did, why, and what it was thinking** — the more the brain narrates itself, the faster every tune lands. So the **AI flight recorder** is built early (Stage 1) and **every later AI slice writes into it** (the rule: no AI slice ships without its "explain" output).

**The AI Decision Record** (per faction, per tick), grounded in existing code — `StrategicObjectiveDB` (already stores a decision-reason), `PlanReadout.cs` (a "dump plan" formatter), `FactionEventLog`, `SessionLog`→`game_logs/` (the flush-to-readable-pages rail), DevTools "View as faction" / "Dump Plan":
- **Sensed** — the fog-limited perception it acted on (contacts, enemy-strength estimate, own economy/military/morale, surveyed bodies).
- **Decided** — objective tier + goal + needs-ladder reason (which gauge fired), the options considered **with their scores**, the personality/doctrine weights applied.
- **Acted** — the order emitted. **Outcome** — did it work (filled next tick, for tuning).

**Two surfaces on the SAME data:** (1) the record flushes to the rolling **`game_logs/` pages as `[AI]` lines** — so a whole game's brain-tape is readable as text, remotely, with the client closed (like SessionLog); (2) an **AI Inspector window** is the live view of that same record (pick a faction → objective+reasoning, scrolling decisions w/ scores, its perceived world, its plan). The log tape is primary (always works, reviewable anywhere); the window is the convenience view. Pair with **view-as-faction** to see the map through its fog *and* its mind.

Then the loop: turn the AI on → watch via the tape/Inspector → spot the dumbest behavior → fix that one domain → repeat. Observable, isolated, grounded.

### Full roadmap to "intelligent" (Phase A→D) — the ladder past batch 7
Batches 1–7 (§6) close the *conquest loop* and make it observable — that's the **first playable + the instrument**. Everything past it is *intelligence*, cheap because you can see what to fix. Each is the same small CI-gated (engine) / 🖥️ PC-verified (runtime) slice, pointed at judgment instead of capability:
- **Phase A — play sensibly:** the unified cross-domain **utility scorer** (guns-vs-butter under one budget, per `docs/economy/CAPABILITY-BUILD-PLAN.md`); **fog-limited threat assessment** (fight/flee/concentrate); then a run of **weight-tuning** slices driven purely by watched games + the `[AI]` tape. *(most ROI)*
- **Phase B — play strategically:** finish the **means-ends planner** act-half (multi-step staged campaigns w/ prerequisites); **force coordination** (screen/strike/reserve); **reactive tempo** (event-driven tactical reactions between the monthly strategy tick — the cadence pyramid).
- **Phase C — play like a character:** **personality/doctrine as weights** on every scored choice (`PersonalityDB` + scorer, the Inspector shows each trait's contribution); **adaptation** (strategy shifts with game state); **mission-command org** (`docs/ai/AI-COMMAND-AND-COMMUNICATION-DESIGN.md` — HoS sets destination, delegates decide how, mandate-down/report-up shown in the Inspector); **emergent multi-faction politics** (`docs/ai/AI-EMERGENT-POLITICS-AND-CRISIS-DESIGN.md`).
- **Phase D — polish verbs as intelligent tools:** EMCON (hide the invasion fleet), espionage ops, logistics networks, diplomacy-as-strategy, research beelining — each a lever the intelligence deploys, each logging its reasoning.

Observability compounds up the ladder: each stage adds a lens (scores → plans → mandates → political reasoning), so by Phase D the tape/Inspector explains the AI at every altitude. "Intelligent" is asymptotic; the practical target (a fair, satisfying opponent) is mostly reached by Phase A + the front of Phase B + steady tuning.

---

## 5. Small feature — name tiles on the planet map *(moved out 2026-07-24)*

**Moved to its subject home: `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` → Layer 6 ("What the map SHOWS and what you can
NAME").** It was orphaned here — tile naming is a surface-map feature, not a DevTest-sandbox one — and the developer has
since given it full requirements (click any hex at either zoom, floating text label, pure bookmark, saves/loads). Design
and build state live there now; this line is a pointer so nothing looks lost.

---

## 6. The build plan — few but thorough CI runs

CI is the only correctness gauge (the SDK can't build in the cloud container) and it is slow (~13 min sharded). So the program is grouped into **coherent, self-contained, independently-tested batches** — each lands green on its own before the next stacks on it. Ordered by dependency. Client changes compile-check only (CI can't *run* the client); their runtime lands on `docs/CLIENT-TEST-CHECKLIST.md`.

| # | Batch | Contents | CI gauge |
|---|-------|----------|----------|
| **1** | **Design doc** *(this commit)* | this doc + `DOCS-INDEX` row | docs-only, trivial green (branch health on main) |
| **2** | **Scenario stands up** | `StationBlueprint` + a `"stations"` parser in `FactionFactory.LoadFromJson`; a post-load **strain-authoring** pass (war-tax / sustenance / committed-manpower setters); `DevTestStartFactory` (engine) that loads the scenario + flips the AI gates; a `DevTestGame()` client entry + relabel the button. Faction files (UMF, Kithrin) initially referencing **existing** base-mod designs so it loads before §3's materiel exists. | one thorough engine test: DevTest scenario loads — both factions build, colonies + stations placed, opening war latched, UMF starts strained (needs-ladder reads Stabilize), only Earth surveyed. `BaseModIntegrityTests` stays green. |
| **3** | **Materiel** | UMF + Kithrin component/ship/ground **designs** (§3) as JSON; point the faction files at them. | extend the integrity test: each faction builds all its designs end-to-end. |
| **4** | **AI loop core** | the sense→decide→act plumbing: the fog-aware perception read, the shared `DecisionScorer` (designs + actions), the action-library skeleton, and the `PlanReadout` observability extension. | unit tests: scorer picks best-not-first; perception respects fog; readout renders a faction's decision. |
| **5** | **AI ground war (keystone)** | `AiInvade` capability + a Conquer-objective path: raise → load → land → capture, through the existing ground orders. | engine tests: an AI faction raises troops, loads, lands, and captures a defended colony (rides `TakeAPlanetIntegrationTests`' proven ground chain). |
| **6** | **AI sustain + aggression + expand** | `AiRefuel`/rearm at a friendly colony; proactive `AiDeclareWar`; seeded in-Sol `AiExpand` (survey-optional). | engine tests per capability. |
| **7** | **Tile naming** | `Name` on hex/tile + rename action + render (client). | data test for the name field; client compile via `build-client`. |

**Sequencing rule:** each batch is pushed and must be **CI-green (both `test` + `build-client`) before the next is built on top** — a verified base is worth the wait, and it keeps total re-runs down (a red batch is re-fixed in place, not stacked on). Polish-tier AI domains (§4) become additional batches *after* a first end-to-end conquest run is observed.

---

## 7. Connections (Prime Directive)

- **Feeds IN:** the base-mod designer templates (`GameData/basemod/TemplateFiles/`), `FactionFactory.LoadFromJson`, `ColonyFactory`/`StationFactory`, the existing player order rail, `NPCDecisionProcessor` + the resolver/needs-ladder/personality skeleton, sensor/intel/survey state (the "sense" inputs).
- **Feeds OUT:** a playable DevTest game mode; two reusable **faction materiel sheets** (the "base for designing other factions"); a consolidated AI **sense→decide→act** loop that every future AI domain plugs into.
- **Shares STATE:** the AI acts through the *same* orders the player uses, so it shares the entire order/industry/combat/ground/diplomacy substrate — no parallel AI-only mechanics.
- **Triggers:** first contact (on discovering Kithrin), the war/combat loop (UMF↔UEF), the ground-capture chain (invasion).

**Companion docs:** `docs/ai/AI-BRAIN-BUILD-TRACKER.md` (the design→code bridge this operationalizes), `docs/ai/AI-DECISION-ENGINE-DESIGN.md`, `docs/ai/AI-CAPABILITY-CATALOG.md`, `docs/MVP.md` ("take a planet"), `docs/REALISM-VS-GAMEPLAY-AUDIT.md` (weight firewall).
