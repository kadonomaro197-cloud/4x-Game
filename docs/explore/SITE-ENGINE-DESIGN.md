# The Site Engine — the unifying mid-game spine (design)

**Status:** design LOCKED at the architecture level (2026-07-12, from three player-walkthrough design sessions). No engine code yet — this is the parent doc the field-site, Command Berth, located-combat, and crisis threads all become chapters of. Supersedes nothing; it *reframes* `docs/explore/EXPLORATION-CONTENT-DESIGN.md` (now a catalog chapter of this), and pulls in `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the Command Berth = the delegate seat), `docs/society/DIPLOMACY-DESIGN.md` + `docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` (two of the berth roles), and the ground-combat map docs (`docs/ground/GROUND-SURFACE-MAP-DESIGN.md`, `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`). Grounded in a four-agent codebase survey (2026-07-12); file:line citations below are from that survey.

---

## 1. The conclusion, in one line

**We are not building an exploration system, a ground-combat system, and a diplomacy system that we later wire together. We are building ONE engine** — a **Site Engine** — and every "episode" across every sci-fi franchise (a ruin, an anomaly, an outbreak, a derelict, a resource, a first-contact, a late-game crisis) is a **row of data** in it.

The whole mid-game reduces to one shape:

> **a LOCATED thing → a berth-seated LEADER works it → it can be FOUGHT OVER at that location → it RESOLVES down one of several branches → a yield, or nothing.**

Build that once, and exploration, ground combat, diplomacy, research, economy, and the crisis system all deepen at the same time — because they stop being six islands and become **consumers of one engine.** This is the Prime Directive's "connect" made into the actual architecture: stop building islands, build the thing that makes the islands one continent.

## 2. How we got here (the walkthroughs → the pattern)

Three player walkthroughs, done button-by-button, each turned out to be the *same* runtime with different dials:

1. **A gravitational anomaly in Saturn orbit** (player has no ships) — design→build→crew a science vessel, seat a scientist in a **Science Berth**, fly it out, park it, study the anomaly for a research stream. A **space** site: no surface, no landing — the cheapest instance of the engine.
2. **The Europa fish-people** — a mining accident cracks the ice, hostiles spawn at the mine's hex, ground combat happens *right there*; you **hold the ground with soldiers while a scientist contains the outbreak.** A **reactive incident** — the same loop pointed at a problem on your own turf.
3. **The space-squid crisis** — the anomaly you were farming *ruptures* into a portal; squids raid Sol. You resolve it down **branches**: study→negotiate for a peace ally, exterminate then study the remains, or seal the portal. Player agency, composable paths.

Nothing across those three was a new *system*. They were the same seven dials set to different values.

## 3. The dials — the parameter space of every episode

An episode is a point in this space. These are the authoring knobs; the runtime is fixed.

| Dial | Values | What it decides |
|---|---|---|
| **Location** | surface hex `(Q,R)` · space point · system-wide | where the site sits, and therefore how you reach it and fight over it |
| **Discovery** | surveyed · event-fired · ruptured-from-another-site | how the player learns it exists |
| **Worker** | unit on-site · built facility | the tactical trade: cheap/mobile/lower-yield/exposed vs. costly/permanent/higher-yield/defensible |
| **Role** | Science · Tactical · Diplomatic · Intelligence · Engineering | which **Command Berth** role (and leader type) can work it, and which pillar its output feeds |
| **Shape** | one-shot (depletes) · persistent (standing stream) · incident (bleeds you until resolved) | the goal framing: *fill-a-bar* vs. *hold-a-faucet* vs. *stop-the-bleed* |
| **Hook** | benign · guardian/spawns-hostiles · cursed/ruptures · contested · gated-by-capability · reactive/sentient · moral-fork | the twist that makes it an episode, not a vending machine |
| **Yield** | research · blueprint · resource · population · leader/ally · strategic-asset · network-route · **nothing** | which consumer system the payoff routes into |

Two cross-cutting properties every site also carries:

- **Branch set** — the honest resolutions (fight / contain / study / seal / negotiate), each a different reward *or none*; **composable** (understand *then* choose), never mutually-forced.
- **Grave** — deplete · destroyed-by-bombardment · **transfers-on-conquest** · **kills-the-leader** (the posting-danger incident roll, §5).

**Claim:** ~seven dials + two properties fully describe the entire sci-fi catalog. That is the definition of a data-driven engine, and it's why "more episodes" is an *authoring* job, not an engineering one.

## 4. The site state machine (agency-preserving — no timers)

The developer's two calls — a crisis **"just sits and bleeds you"** (no doom clock) and **paths compose** (understand, *then* choose to seal or ally) — together dictate the architecture. There are **no scripted timers and no forced exclusivity.** Instead each site runs a small state machine where **accrued knowledge UNLOCKS branches rather than closing them:**

```
DISCOVERED ──work──▶ WORKED (progress/understanding accrues)
                        │
                        ├─ enough understanding ─▶ unlocks branch options
                        │
                     RESOLVE (player commits a branch)
                        │
             ┌──────────┼───────────┐
         DEPLETED   PERSISTENT   RUPTURED
        (one-shot)  (standing    (becomes a new
                     stream)      crisis site)
```

- **"Bleeds you, no timer"** = an *incident/persistent* site applies steady pressure (population/industry drain, raids) for as long as it's unresolved. The pressure is the clock; the player chooses *when* and *how* to act. Nothing forces their hand — which is exactly what protects agency.
- **"Paths compose"** = branches are gated by accrued knowledge, not consumed by each other. A patient player accrues full understanding, *then* still gets to choose seal-vs-ally. An impatient player acts on partial knowledge for a lesser/riskier outcome.
- **RUPTURED** is the key edge: a persistent site (the anomaly faucet) can transition into a *new* crisis site (the portal). The reward carries the risk. This is the hook = *cursed/ruptures* firing on a *persistent* shape — and it's the small-scale seed of the late-game crisis.

## 5. The Command Berth — the worker interface

Every site is worked through **one component**: the Command Berth. It's the universal socket that plugs any leader into any site (and into fleets/battalions/colonies too — it *is* the delegate seat from `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`). A capital ship fits several (the Enterprise-D: a Tactical + a Science + a Diplomatic suite). Dials:

| Dial | Decision | Wire |
|---|---|---|
| **Role** — Tactical/Science/Diplomatic/Intelligence/(Command) | which leader type seats here and which pillar it feeds | role tag on the seat; assign-order gains a type-vs-role check |
| **Grade** — the rank tier it supports | fit-to-purpose: your best admiral needs a flag-grade berth to function; a low berth under-uses (and demoralizes) a high leader; grade costs mass/crew/materials | compares `CommanderDB.Rank`; mismatch → penalty + morale hit |
| **Support** — staff, tools, war room | spend mass to make the seated leader better (a flat competence boost in their pillar) | folds a bonus into the same `BonusesDB`/competence path research + combat already use |
| **Survivability** — protect the occupant | reduce the leader's death chance when disaster strikes (escape pod / command bunker); costs mass, trades against firepower | reduces the incident roll below |
| **Span** — how large a force the leader can run | a small berth can't command a whole task force | `AdminSpaceAtb.ConsoleSpace` already computes this and discards it — give it teeth |
| **Crew & upkeep** | a berth needs support staff and running cost | standard component crew/cost formulas |

**The standout mechanic — *where* you post a leader sets their risk:**

> **incident chance = (danger of the posting) × (1 − berth survivability)**

Danger comes from the posting (a diplomat in your capital ≈ 0; a flagship admiral in a fleet battle, real; a scientist on a guarded dig, high; a spy on insertion, highest); the berth's Survivability mitigates it. This makes leader loss a **decision you own** — park irreplaceable people in safe, well-appointed berths; risk expendable ones where it's dangerous; or gamble your best scientist on the dangerous dig because their competence finishes it before the guardians wake. It's **one incident roll across every pillar**, and it turns the grave rung from bad luck into a placement choice.

## 6. The located model

A site lives at a real location, which is what makes it reachable, buildable-on, fightable, and conquerable.

- **Surface sites** sit on a `GroundHex (Q,R)` of the planet's `SurfaceGrid` cylinder (the same grid that already holds mineral deposits and located installations). Reached by marching ground units (`GroundForces.OrderMoveToGlobalHex`, terrain-aware A* via `HexPathfinder.FindGlobalPath`, ocean impassable) and/or landing from orbit.
- **Space sites** sit at a point in the system's space (orbit of Saturn, a portal). Reached by moving a ship there (`MoveToSystemBodyOrder` + warp + orbit insertion). No landing — the berth-ship parks and works it. **This is the cheaper first build** (no surface machinery).

### The transport embark/deploy sequence (locked, 6 steps)

Getting soldiers to a surface site is unit-driven, not magic (developer's correction):

1. Ground units get a **"Load onto Transport"** order → they **march to the transport** in its slipway and **embark** (co-location required; the units move to the ship, `GroundTransport.TryLoadUnit`).
2. Transport moves to **orbit**.
3. Transport moves **to the target body** (`MoveToSystemBodyOrder`), winning **orbital control** (`GroundTransport.HasOrbitalControl`).
4. **Land:** at a **friendly base** if one exists on the target hex/region — otherwise the player **picks a specific hex** and it sets down there (`TryLandUnit`, but extended from region-only to a chosen hex).
5. Units **disembark** at the landing hex.
6. Units then **move like normal ground units** (march to the site, fight, hold).

## 7. The consumer / hook map — what every "other" system becomes

The engine is the spine; existing systems become its consumers and hooks (mostly CONNECT, not build):

| System | Its role in the engine |
|---|---|
| **Ground / space combat** | the "spawn hostiles at the location" **hook** + the "hold-while-you-work" coupling. Combat *is* how a contested site resolves. |
| **Research / tech** | the most common **yield** route (and fills the empty Biology & Stellar Science categories). |
| **Diplomacy** | a **branch** (negotiate/ally), gated by a Science prerequisite (learn the language first). |
| **Economy / industry** | the "characterize → then it's a mine/depot" **yield**; and it builds the on-site facilities. |
| **Population** | survivors/refugees from derelicts; biosphere bonuses — a **yield**. |
| **Jump network** | network-reshaping **yield** (a decoded buoy → a hidden route/gate). |
| **The crisis system** | a **persistent site whose hook = ruptures**, with system-scale sub-sites — the RUPTURED edge of the state machine. |
| **Command / delegation** | the **Command Berth** *is* the delegate seat — the worker interface itself. |
| **People / commanders** | the leaders that fill the berths; the grave rung (incident roll) is theirs. |

## 8. Worked examples — the catalog as data rows

Every episode is the same engine, different dials:

| Episode | Location | Role(s) | Shape | Hook | Yield | Branches |
|---|---|---|---|---|---|---|
| Saturn anomaly | space point | Science | persistent | benign → *can rupture* | research stream | study / keep-farming |
| Mars ruin | surface hex | Science + Tactical | one-shot | guardian (spawns) | unique blueprint | dig-and-fight |
| Europa fish-people | surface hex | Science + Tactical | incident (bleeds) | outbreak (spawns) | biology tech + tameable unit | kill / contain |
| Space-squid crisis | space + system | Sci→Diplomat→Tactical | persistent crisis | ruptured spawner | ally / tech / threat-ends | fight · seal · ally *(composable)* |
| Rare resource node | surface hex | Science → economy | characterize→persistent | contested | strategic material (a mine) | study-then-mine |
| Derelict / lost colony | space point | Science | one-shot | booby-trap | population + a ship design + a stranded leader | salvage / rescue |
| Pre-warp world | surface hex | Science → Diplomat | persistent | moral fork | intel / uplift | observe / contact |

## 9. EXISTS / NEW ledger (from the 2026-07-12 four-agent survey)

The unification is cheap because most of the runtime already exists.

**EXISTS (reuse):**
- Located surface grid + on-hex installation placement (`PlaceInstallationOnHexOrder` — colony-commanded, rides the real order path, installs on the economy rail via `colony.AddComponent`, records the id on `GroundHex.InstallationIds`).
- **A research station on a hex produces research TODAY** — `ResearchPointsAtbDB.OnComponentInstallation` spawns an independent researcher entity; `ResearchProcessor` keys on `ResearcherDB`, location-agnostic. (The facility-worker half is nearly free.)
- Ground units + surface movement (`GroundForces.OrderMoveToGlobalHex` `GroundForcesDB.cs:605`, `HexPathfinder.FindGlobalPath`, `GroundForcesProcessor` hourly).
- Ship move-to-body + orbit insertion (`MoveToSystemBodyOrder`, `WarpMoveProcessor`).
- Troop bay component (`GroundBayAtb`, buildable `troop-bay` in stock game) + load/land logic (`GroundTransport.TryLoadUnit`/`TryLandUnit` → `GroundForces.PlaceExistingUnit`) + orbital-control gate.
- Ground combat trigger (region, `factions.Count >= 2` → `ResolveRegionCombat`, per-unit salvo, hex-range-aware) + conquest ownership flip + bombardment softening.
- Hostile-force spawn primitive — `GroundForces.RaiseUnit(body, design, factionId, regionIndex)` `GroundForcesDB.cs:394`, **faction-agnostic** (any faction incl. neutral/hostile).
- The seat mechanism — `AdminSpaceAtb` (scopes Ship→Fleet→…→Empire), `AdminSpaceDB.CommanderSeats`, `AssignAdministratorOrder` (**type-agnostic today**), reconcile + decapitation grave rung.
- Two competence→pillar wires — commander→combat (flagship, `FleetDB.FlagShipID`), scientist→research (`ResearcherDB.ScientistId` + the X.0 academy/competence/experience work).
- Ruins now generate (X.1) — planet-level `RuinsDB`.

**NEW / NEEDS-CHANGE (build):**
- The **site record + state machine** (`FieldSiteDB`/site object: location, dials, accrued progress/understanding, unlocked branches, status). *This is the engine.*
- **Locate the site** — assign a discovered ruin/anomaly a hex `(Q,R)` or a space point (ruins are planet-level today).
- **Presence detection** — there is **no arrival event**; a processor must poll "is a science-capable worker at the site" (unit coords or a facility on the hex, or a berth-ship in the orbit).
- The **Site processor** — accrue from the worker (facility rate > unit rate), unlock branches by understanding, apply incident/persistent pressure (no timer), resolve, deliver yield, deplete/persist/rupture.
- **Command Berth component** (Role/Grade/Support/Survivability/Span/Crew) built on the seat; the **type-vs-role** check; fold the three parallel "assign" slots (`CommanderSeats` / `FlagShipID` / `ScientistId`) toward the one seat.
- **`Diplomat` + `Spy` leader types** (`CommanderTypes` = Navy/Ground/Scientist/Civilian today) + their pillar wires (diplomacy/espionage — designed, not built).
- The **posting-danger → incident roll** (survivability mitigation) — unifies field-site death, flagship-in-combat death, spy loss into one roll.
- **Player Load/Land orders + hex-level landing** — the load/land logic exists but has no player order/UI, and landing targets a region, not a chosen hex (the 6-step sequence, §6).
- A **hostile/neutral "menace" faction** to own outbreak/crisis forces, and the **event/trigger** that spawns them at a site (thin caller of `RaiseUnit`; the space equivalent for portals).
- **Component transfer on conquest** — today the hex owner *marker* flips but the installation component stays the original colony's (deferred in code comments).
- **Colonyless / ground-construction-unit** placement — on-hex build currently requires a colony on the body (a documented refinement).

## 10. Build sequence — anomaly-first (the engine, then content)

The smallest first slice builds the **whole spine**, because the space anomaly is the engine minus the surface bits.

1. **SE-1 — the engine, on a space anomaly.** `FieldSiteDB` + the site state machine + presence detection (a science-berth ship parked at the anomaly) + accrue + resolve + a research yield. The Command Berth **Science** role is its worker. Proves discovered→worked→resolve→yield end-to-end with the least new code, no surface machinery.
2. **SE-2 — the Command Berth as a real component** (Role/Grade/Support/Survivability dials, base-mod design, fit-to-rank) + the incident roll (survivability). Retro-fits the flagship-death path onto the one roll.
3. **SE-3 — surface sites** — locate ruins on a hex, the unit-worker path, the Load/Land orders + hex landing, the guardian hook (spawn hostiles mid-dig). Brings ground combat into the engine.
4. **SE-4 — the incident shape** (Europa) — the bleeds-you pressure + the hold-while-you-work coupling + the menace faction + the spawn event.
5. **SE-5 — branches + the ruptured edge** (the crisis) — composable multi-path resolution, the Diplomat/Intelligence roles, the persistent→crisis rupture, the player-agency outcome set.
6. **SE-6+ — content** — author catalog rows across the dials (rare resource, derelict, biosphere, pre-warp world, megastructure/network) as data.

Each slice is CI-gated and byte-identical-first (new code gated/neutral until wired). SE-1 is the keystone; everything after is additive.

## 11. Locked decisions & open questions

**Locked (2026-07-12):**
- **One Site Engine**, data-driven; every episode is a row across the §3 dials. Not parallel systems.
- **Role-flavored Command Berths** built on the one seat mechanism; a ship fits several (Enterprise-D). Role decides leader type + pillar.
- **Worker sets rate/yield; site shape sets one-shot-vs-persistent** — two independent dials (a lone team vs. a built station changes speed/yield; ruin vs. biosphere changes deplete-vs-persist).
- **The persistent site can rupture** — the reward carries the risk (faucet ruptures → crisis seed).
- **Player agency is a hard rule** — multiple honest branches, different rewards *or none*, **composable** (understand → then choose), never railroaded.
- **Incidents/crises bleed you, no doom clock** — steady pressure is the clock; the player chooses when/how to act.
- **Where a leader is posted sets their incident/death risk**, mitigated by the berth's Survivability dial — one incident roll across all pillars.
- **Transport is unit-driven, 6 steps** (§6); landing can target a specific hex, not just a region.
- **The Command Berth dial set is LOCKED (2026-07-12):** **Role** (Tactical/Science/Diplomatic/Intelligence/Command — decides eligible leader type + pillar), **Grade** (rank tier it supports; fit-to-rank — a high leader under-uses a low berth and loses morale), **Support** (a flat competence boost to the seated leader's pillar), **Survivability** (reduces the posting-danger incident roll), **Span** (force size the leader can run — gives `AdminSpaceAtb.ConsoleSpace` teeth), **Crew & upkeep** (standard cost). **Per-role berths**, built on the one shared seat mechanism; a ship fits several (Enterprise-D). This is the §5 table, now frozen as the build target.

**Open (decide when we build):**
- The accrue/understanding curve and how much understanding each branch requires.
- Whether killing spawns must be paired with sealing/negotiating to actually end a crisis (leaning: yes — kill ≠ close the door).
- Does a site under incident keep producing (pressure) or go offline (punishment)? Per-episode data, probably.
- Component-transfer-on-conquest depth (marker-only today).
- Colonyless on-hex construction (the ground-construction-unit refinement).

## 12. Connections (Prime Directive)

- **Exploration/survey** (`GeoSurveyProcessor`, `Masked`, `RevealAll`) — discovers sites; fog is per-faction.
- **People/commanders + academies** (X.0) — fills the berths; the grave rung is theirs.
- **Ground combat + the hex grid** (`GroundForcesProcessor`, `SurfaceGrid`, `RaiseUnit`, conquest) — the located combat hook + the surface substrate.
- **Ships/fleets/movement** (`MoveToSystemBodyOrder`, `GroundTransport`) — reach + deploy.
- **Research / economy / population / jump-network / diplomacy / espionage** — the yield routes and branch consumers.
- **Hazards + the crisis/ascension seed** — the ruptured edge and the danger geography.
- **Delegation/governance** (`AdminSpaceAtb`, `AssignAdministratorOrder`) — the berth is the seat.

**The engine is the connective tissue itself. Build it on purpose (SE-1), and the islands become one continent.**
