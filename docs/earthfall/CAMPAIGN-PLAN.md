# OPERATION EARTHFALL — Campaign Plan (v2, parallel-lane edition)

**Status: AUTHORED, NOT STARTED. Nothing runs until the developer says go.**
**As of 2026-07-17. This document is self-contained: a fresh Claude session (any model, e.g. Opus 4.8) on any branch must be able to execute its lane from this file + the findings folder alone, with NO access to the session that wrote it.**

---

## 0. What this campaign is (plain English)

The developer played a real game (logs committed to `main` at `fe72043`). Five things went wrong, one thing was missing, and several player-facing features are wanted. Ten research passes produced verified, file-and-line ledgers in `docs/earthfall/findings/` — **read the ledger(s) named by your slice before touching code; they are the ground truth and they overturned several plausible-but-wrong theories.** The campaign fixes the failures, builds the missing invasion machinery (bombard → land → beachhead → maneuver → capture — "each individual step and click"), makes battalions first-class citizens beside fleets (for the AI *and* the player), adds planetary range display and infrastructure destroy/capture combat, makes the NPC factions actually develop, verifies + completes the player's unit-creation chain (the developer intends to design **Space Marines** to defend Earth), and lands the first slices of the locked 2D group-plane resolver.

The headline findings a new session must not re-litigate:
1. **The freeze** was a native client render crash (contact blips lack the finite-coordinate cull orbit icons have), NOT a sim crawl — `findings/A1-freeze.md`.
2. **"Saw the fleet before sensor range"** — detection was honest (a deliberate 200 Gm colony sensor horizon); the drawn ring under-draws — `findings/A2-ghost-contacts.md`.
3. **The AI abandoned its own invasion** via a stale-morale echo → phantom legit crash → one-sample rebellion → Defend locked 180 days at the Survive floor (break-glass unreachable), and nothing recalls an in-flight fleet — `findings/A3-objective-flip.md`.
4. **The troop transport never existed**: 4× redundant queue strangled Mars industry; a finished hull would sit on a fuel-less launch pad; a launched one couldn't warp (built ships never charged) — `findings/A4-sealift.md`.
5. **The ground campaign is mostly missing**: no AI bombardment order, no colony-free building (beachhead impossible), no AI troop movement after landing, ammo/resupply unwired — `findings/A5-ground-campaign.md`.
6. **UMF worlds are self-sufficient** (Ceres can't manufacture — data gap); **the Kithrin are structurally dead**: survey chain unbuilt at three rungs + stations have upkeep but NO income — `findings/A6-faction-development.md`.
7. **The player unit chain is ~90% built** (dials → assembler → build → field → form up → move all work). Verified gaps: no sealed/environmental component, no embark/land UI, and the AI never forms units into formations — `findings/R2-player-unit-chain.md`.
8. Manager + rings anatomy: `findings/R1-manager-and-rings.md`. Infrastructure combat semantics: `findings/R4-infrastructure-combat.md`. Docs directives (62): `findings/R3-docs-directives.md`.

---

## 1. Glossary (do not skip — the codebase's own vocabulary)

- **DataBlob (`*DB`)**: a typed data container attached to an Entity (the ECS "component"). Save-critical fields need `[JsonProperty]` AND a line in the deep-copy constructor.
- **Atb (`*Atb`)**: a component-design attribute bound from JSON templates. **The JSON binder is EXACT-ARITY** (`Activator.CreateInstance`): adding a ctor arg to an atb requires updating EVERY template that binds it in the same change.
- **Six-point registration**: a new buildable component must appear in (1) `installations.json` (or peer template file), (2) `componentDesigns.json`, (3) the colony's `earth.json` `ComponentDesigns`, (4) `StartingItems` template id, (5) `StartingItems` materials, (6) any scenario faction files that field it. `BaseModIntegrityTests` is the CI sensor.
- **Hotloop processor**: auto-discovered by reflection; runs per DataBlob type. **L4**: its body must never throw. **L9**: only ONE hotloop may key a given DataBlob type — new per-tick ground work folds into `GroundForcesProcessor`, never a second processor on `GroundForcesDB`.
- **Byte-identity**: a behavior change ships behind a default-off flag (or provably changes nothing when its data is absent) so every existing green test is unaffected. State which of the two your slice claims.
- **Save-safety**: `TypeNameHandling.Objects` embeds C# type names in saves — renaming/moving a `*DB`/atb breaks saves (L3). Enums serialize by int — APPEND members, never reorder.
- **CI**: 4 sharded test jobs + 1 client-compile job (`build-client`). New test fixtures land in the `rest` shard. **The client compiles in CI but cannot RUN** — client behavior is verified only on the developer's Windows machine via `docs/CLIENT-TEST-CHECKLIST.md`.
- **No local SDK**: the container has NO .NET SDK. You cannot compile. Verify every API by reading source before using it. CI is the only gauge; a wrong member name costs a ~30-minute red.

## 2. Standing rules (every lane, every slice)

1. **One slice per commit; push; WAIT for CI green (all 5 jobs) before the next slice on that branch.** (Within a single workflow phase, file-disjoint slices may IMPLEMENT in parallel — the workflow's declared batches — but they still LAND as separate sequential commits under this rule.)
2. **Never push to a branch you don't own** (lane branches listed below). Never open a PR unless the developer asks.
3. Match `CONVENTIONS.md` + the subsystem `CLAUDE.md`; update the subsystem `CLAUDE.md` rows for what you changed IN THE SAME commit.
4. **Campaign amendment to the dashboard rule** (because parallel branches cannot serialize on shared files): lanes do NOT edit `docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`, or another lane's files mid-flight. Instead each lane appends its pending dashboard rows to its OWN notes file `docs/earthfall/LANE-<X>-NOTES.md` (lane-owned, conflict-free). The integration phase (P8.2) lands all rows. Subsystem `CLAUDE.md`s inside your lane's fence are yours to edit normally.
5. **File fences are absolute.** If your slice needs an edit outside your fence, write the need into your lane notes as a REQUEST for the owning lane — do not edit.
6. Flag every new gameplay number (`// FLAGGED balance value`) — the developer sets balance, not the agent.
7. Commit messages end with the standard co-author + session trailers the harness supplies for that session.
8. Never fabricate a green: report CI/test state as observed.

## 3. The lanes (branch = owner = file fence)

**Fork point: all lanes branch from `claude/devtest-faction-design-xpfnhe` AFTER phase P0 lands green** (P0 builds the shared gauges + ci.yml step every lane relies on; ci.yml is frozen after P0).

| Lane | Branch | Phases | Owns (fence) |
|---|---|---|---|
| **CORE** (this session's branch) | `claude/devtest-faction-design-xpfnhe` | P0 → P3 → P4 → PW → P8 | `GameEngine/Factions/ConquerResolver.cs`, `DefendResolver.cs`, `ObjectiveTransition.cs`, `NeedsLadder.cs`, `NPCDecisionProcessor.cs`, `GameEngine/Colonies/Legitimacy*`, `PopulationProcessor.cs`, `Ships/ShipDesign.cs`, `Industry/LaunchComplexProcessor.cs`, `GameData/.../umf.json`, `.github/workflows/ci.yml` (P0 only), integration docs |
| **GROUND** | `claude/earthfall-ground` | G1 → G2 → G3 → G4 | `GameEngine/GroundCombat/**`, `GameEngine/Galaxy/GroundHex.cs`, `PlanetRegionsDB.cs` (ground fields), `GameData/.../installations.json`, `componentDesigns.json`, `earth.json` (six-point registrations) |
| **CLIENT** | `claude/earthfall-client` | C1 → C2 → C3 → C4 → C5 | `Pulsar4X.Client/**` (all windows/rendering/SessionLog), `docs/CLIENT-TEST-CHECKLIST.md`, `docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md` |
| **DEV** | `claude/earthfall-dev` | D1 → D2 → D3 | `GameEngine/Factions/ExpandResolver.cs`, `ConsolidateResolver.cs`, `GameEngine/Stations/**`, `GameData/.../kithrin.json` |
| **TWOD** | `claude/earthfall-2d` | T0 → T1 → T2 → T3 | `GameEngine/Combat/GroupPlane.cs` (new), `FleetCombatStateDB.cs`, `CombatEngagement.cs` (flag-gated blocks only), `docs/combat/RESOLVER-2D-JOINTS.md` |

Shared-but-safe: every lane creates NEW test files with lane-distinct fixture names (prefix suggestion: `Ef<Lane>...Tests.cs` or descriptive unique names) — the Tests folder never conflicts at file level. `GameEngine/Engine/DataStructures/SafeDictionary.cs` + `EntityManager.cs` (the latent concurrency fixes) are CORE-owned (they ride P0/P3-adjacent slices? NO — they are in phase C1's engine portion? They are ENGINE files; assign them to CORE as slice P0.5 to keep CLIENT purely client).

**Merge order (CORE session executes):** GROUND → DEV → CLIENT → TWOD, each: rebase lane branch onto integration branch → merge → push → CI green before the next. Then PW (wiring), then P8 (acceptance). Rationale: GROUND first (CLIENT's C5b and CORE's PW consume its types), DEV independent, CLIENT after GROUND (battalion manager reads new helpers if present), TWOD last (touches combat state broadly).

**Cross-lane dependencies made explicit:**
- CLIENT C5b (infrastructure order buttons) needs GROUND G3's enum members → C5b is deferred to PW (post-merge), C5a (embark UI) has no dependency and ships in-lane.
- CLIENT C3's battalion rename needs GROUND G2's `RenameFormation` helper → C3 ships list/orders without rename; rename button lands in PW.
- CORE PW wires `ConquerResolver` rungs to GROUND's capabilities (bombard order path, beachhead build step, maneuver kickoff, formation-up helper, landing-region scorer) — CORE owns the resolver, GROUND owns the invoked engine surface.

## 4. Phase → slice index (details live in the workflow script's prompts)

- **P0 (CORE, pre-fork)**: P0.1 campaign-clock CI repro · P0.2 per-faction self-sufficiency readout · P0.3 ci.yml readout-cat step · P0.4 honest station-aware tape · P0.5 latent-concurrency fixes (SafeDictionary events-outside-lock; EntityManager values-snapshot) + stale-docs corrections.
- **P3 (CORE)**: P3.1 umf.json government node (+ Ceres factory data request from DEV folded here) · P3.2 rebellion debounce + legitimacy reads current morale · P3.3 hysteresis break-glass (crisis commit shorter; trigger-cleared release; contradiction release) · P3.4 operation continuity (protect winning in-flight conquest from transient wobble; genuine Defend RECALLS in-flight fleets).
- **P4 (CORE)**: P4.1 Rung-2 already-queued guard · P4.2 charge+fuel built hulls (both build paths; policy flag for player ships — developer decision) · P4.3 launch fuel data + pad-tonnage audit · P4.4 end-to-end sealift gauge.
- **G1 (GROUND)**: combat-engineer beachhead chain — `GroundConstructorAtb` (six-point), surface parts haulage, colony-free on-site build placing a footprint building into a held region/hex, FOB staging-anchor role (per `SURFACE-FOG` FOB framing + the developer's explicit constructor-unit ask).
- **G2 (GROUND)**: battalion engine + **THE GROUND TACTICAL BRAIN** (`docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md` — the answer to "is the AI smart enough to know when to be defensive vs offensive") — `FormUpLoose(body, faction)` (AI formation parity, the brain's hard prerequisite), `GroundThreat` (the fog-honest enemy-strength read — fog "slice 5" promoted to a requirement, also feeds PW's landing score), `GroundTactics.DecidePosture` → Stance/ROE/Intent/Reason per battalion (odds via the fleet AI's own CombatRisk curve × personality × role × fortification/terrain × reserve × ammo, with posture hysteresis + a built-in break-glass), wired behind `EnableGroundTacticalAI` with a save-safe order-issuer marker (player orders always override), plus ammo drain + resupply caller, UpkeepCredits values, and the manager micro-helpers (`RenameFormation`, `AllFormationsFor`, `RadarReachHexes`).
- **G3 (GROUND)**: infrastructure combat — append `DestroyInfrastructure`/`CaptureInfrastructure` order types (ordinal-stable), processor cases with the resolver's own range gate, Attack→strength scaling (staged drain default), per-hex capture flip + first consumers, decisions memo for the developer (produce-for-captor etc. — Aurora refs).
- **G4 (GROUND)**: the sealed/environmental component (six-point; wires `EnvironmentalResistance` at `ToGroundUnitDesign`) — the last Space-Marine blocker — + assembler gate surfacing data (expose training/power/ammo numbers engine-side for the client rows).
- **C1 (CLIENT)**: freeze fix — blip finite/on-screen cull + per-window render breadcrumbs + tighter watchdog; CLIENT-TEST-CHECKLIST entries.
- **C2 (CLIENT)**: sensor truth — honest detection ring; held-vs-fresh `[DETECT]` split; age-out/lost-track engine tests; the 200 Gm horizon decision memo (no value change).
- **C3 (CLIENT)**: **Force Management window** — retitle `FleetWindow` ("Fleet Management" → developer picks from the candidates list; default suggestion "Force Management"), add the Battalions section: cross-body enumeration (StarSystemStates → `GroundForcesDB` → `FormationsFor`), location/strength/health columns, filters (system/body/faction), selection → move (region/global-hex picker), stance/ROE, order-queue view — copying `PlanetViewWindow.DrawFormationPanel` idioms.
- **C4 (CLIENT)**: planetary range overlay — hex-highlight pass in `DrawGlobalHexWindow` (weapon reach red / radar reach green), toggle, fog-honest; TrainingMultiplier + always-on power/ammo gate rows in the Entity Assembler readout.
- **C5 (CLIENT)**: C5a the **invade-from-orbit UI** (MVP Stage 4 — embark `LoadTroopsOrder` / land `LandTroopsOrder` buttons from the manager + orbit view). C5b (deferred to PW): infra Destroy/Capture buttons.
- **D1 (DEV)**: Kithrin survey chain (ExpandResolver emits `GeoSurveyOrder`; kithrin.json adds a Sable surveyor; build-surveyor fallback rung). D2: station income + Consolidate station-legal step. D3: colonization end-to-end gauge.
- **T0-T3 (TWOD)**: joints memo (fire-allocation conservation + combined-theater cadence) · S0 `GroupPlane.cs` pure + tests · S1 anchors behind `EnableGroupPlane` · S2 2D pair-distance range gate. (S3+ are a later campaign.)
- **PW (CORE, post-merge)**: resolver wiring — bombard rung, landing-region choice via the easiest-landing score (fog-honest), beachhead-build rung, campaign-arm kickoff + `FormUpLoose` calls at garrison-raise/landing, infra-order rung; C5b + C3-rename client buttons; cross-lane request resolution.
- **P8 (CORE)**: P8.1 the Operation Earthfall acceptance gauge (full chain, milestone-by-milestone assertions, `[Ignore]` any milestone that cannot pass yet — never a red test) **+ the Space-Marine player-side acceptance** (a player-designed sealed+cadre+power-armor unit, formed into a battalion, defends Earth vs landed UMF troops and wins; then embarks and lands offensively). P8.2 dashboard sync (all lane-notes rows) + final campaign report.

## 5. Launch procedure (per lane session)

1. Open a session on the lane's branch (create it: `git fetch origin claude/devtest-faction-design-xpfnhe && git checkout -b <lane-branch> origin/claude/devtest-faction-design-xpfnhe`) — ONLY after P0 is merged green.
2. Read this file + your lane's findings, then run the workflow BY NAME: `Workflow({name: 'earthfall-campaign', args: {phase: '<your phase key>'}})` — the script lives at `.claude/workflows/earthfall-campaign.js` in the repo.
3. The workflow implements + adversarially verifies the phase's slices in your working tree. YOU (the session) then commit slice-by-slice, push, and gate each on CI green before invoking the next phase.
4. Keep `docs/earthfall/LANE-<X>-NOTES.md` current (pending dashboard rows, cross-lane requests, developer decisions raised).

## 6. Developer decisions queued by this campaign

The 200 Gm homeworld sensor horizon (C2 memo) · built-ships-boot-charged policy for PLAYER ships (P4.2) · recall-vs-press doctrine details (P3.4 defaults chosen, reviewable) · window name (C3) · infra-combat decisions 1-6 (`findings/R4`) · **the tactical brain's thresholds** (posture odds bands off the CombatRisk curve, retreat trigger + losses ratio, posture hysteresis band + minimum hold, blind-caution factor, dry-ammo behavior — `GROUND-TACTICAL-AI-DESIGN.md §5`) · **ground cadre talent-draw size** (marine scarcity parity, G4.1d) · **bombardment re-fire cadence + cap** (PW.1a) · all FLAGGED balance numbers (upkeep, ammo-per-salvo, station tax, cadre costs). **Explicitly deferred by the developer:** orbital strike aimed at a specific hex's buildings (the W-track follow-on).
