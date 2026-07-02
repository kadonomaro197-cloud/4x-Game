# Pulsar4X — Docs Index & Status Dashboard (LIVING — keep commit-current)

**What this is:** ONE place that lists *every* doc in the repo, what it's *for*, and its *status right now* — so anyone (a new session, a different model, the developer weeks later) can open this file and know where everything stands without spelunking. It is the doc-level twin of `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` (which tracks *systems*) and `docs/TESTING-TRACKER.md` (which tracks *tests*).

**THE RULE (not optional):** when a commit changes a doc — or changes code that makes a doc newly-true or newly-stale — update that doc's row here **in the same commit**. A row's *status* is the maintained truth (git already knows the last-touch sha; what git can't tell you is whether the content still reflects reality). Keep the "As of" stamp current on any substantive pass.

**As of:** 2026-07-02 · branch `claude/space-economy-morale` · last verified-green HEAD `b2d2e60` (engine + client CI green).

**Current focus (what's being worked on):** the space-economy / morale / political-substrate branch. This session drove it "to the PC line" — everything cloud-doable wired + CI-green; what remains is local runtime testing + a few design decisions. See `docs/TESTING-TRACKER.md` for the live PC-test queue and the design/PC-gated list.

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
| `docs/TESTING-TRACKER.md` | Standing global test ledger (CI layers + the Layer-3 PC-test queue), 7 fields/row | ♻️ 🟢 | CURRENT to HEAD (updated this session: C1/C3 wired, D3 drift, design/PC-gated list) |
| `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` | Living map of every SYSTEM, its state + gauge + "connected to" | ♻️ 🟡 | **STALE since 2026-06-29** — doesn't yet reflect this session's morale/gov/diplomacy/crew/stations landings. Refresh the changed rows next docs pass. Meanwhile TESTING-TRACKER is authoritative for status |
| `docs/CLIENT-TEST-CHECKLIST.md` | This branch's client RUNTIME test scratchpad (what only the PC build can check) | ♻️ 🟢 | Refreshed 2026-07-02 with the economy/morale/politics PC-tests (T0, Dump Society, gov dials, morale-moves-pop, crew gate, drift) |
| `SESSION_STATE.md` | Session-close handoff ("read first after CLAUDE.md") + play-by-play + lessons | 🟢 | Refreshed 2026-07-02 with the current branch state (older sessions collapsed under a details fold) |

## 4. Design docs (each carries a build state)

| Doc | Purpose | Status | Build | Notes |
|-----|---------|--------|-------|-------|
| `docs/SPACE-STATIONS-DESIGN.md` | Station-as-universal-off-world-host (parallel to colony) | 🔒 | ✅ | v1 delivered (foundation + mining + pop + in-situ + research flavor), CI-green |
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
