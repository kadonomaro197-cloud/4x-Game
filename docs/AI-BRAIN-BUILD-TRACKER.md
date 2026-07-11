# AI Brain — Build Tracker (Planck → Brane)

**What this is:** the single standing tracker for building the NPC "brain" — the faction-AI design suite (the nine `AI-*.md` + `EXPLORATION-CONTENT-DESIGN.md` docs) turned into a **verified, ordered, buildable plan**. It answers, for every rung of the ladder: *what does the design say to build · where does it plug into the real code (file:line) · is it possible today · what's the gauge · what's its status right now.* It is the doc-level twin of `docs/TESTING-TRACKER.md` (tests) and `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` (systems), pointed at one campaign.

> **As of 2026-07-11.** Built from a **six-agent feasibility survey** (one per doc-group) that verified every design claim against the *current* branch source (`claude/sol-playtest-earth-map-8r59j6`), not the `main` baseline the docs were written against. Every file:line below was checked in code. Companion design docs: the nine `docs/AI-*.md`. This tracker is the build authority; the design docs are the rationale.

---

## The headline — is it possible? YES, and mostly on rails already laid

**The brain is not a new nervous system — it is a mind plugged into one that exists.** The six surveys agree: the hard infrastructure the design needs is **already built and, on this branch, better than the docs assume.** What's left is **new *logic* (scoring / objective / transition engine) + a few new *data blobs* (personality, objective, threat-estimate) + wiring** — not a rebuild.

**The one load-bearing sequencing fact:** the upper rungs (Ecosystem, Galaxy, Brane) genuinely *compose* the lower engine — but they sit on the **Organism engine (needs-ladder + traits + mood + transition), which is design-only today.** So the build is **bottom-up**: the atomic scored-decision and the single-faction Organism come first; everything above is composition once they exist.

**The keystone gate:** `NPCDecisionProcessor.Tick` (`Factions/NPCDecisionProcessor.cs:64-84`) fires monthly per-NPC and already runs diplomatic drift + reads the dominant doctrine axis — but the **axis→orders translation is an empty `// TODO` (`:79-83`).** Filling that (plus the tiny objective/personality blobs it reads) is the gate the whole brain waits behind. Nothing structurally blocks it.

**The chosen strategy — FOUNDATIONS FIRST (2026-07-11):** the six surveys sort the design into three tiers — buildable-now, net-new-but-unblocked, and blocked-behind-a-subsystem (espionage, trade-money, bloc-demands, gate-network, capability-tech). The design would let those last ones ship as inert "labeled sockets." **The developer chose to build the missing pillars FIRST**, so that when the brain goes in, *every* trait/role/rung wires into a live system — nothing on the finished brain is a dark socket. That splits the campaign into **Movement I (Foundations — level the ground)** and **Movement II (Build Up — the brain)**, both detailed below. Honest scope: Movement I is the bulk of the remaining work, because three of its tracks are full subsystems (each already has a design doc — it's *build*, not *design*).

---

## The ladder — Planck → Brane (the tracker's spine)

The design frames itself, unironically, as *"Planck-length → multiversal-branes"*: the atomic scored-decision at the bottom, an instantiable universe at the top. Build bottom-up.

| Rung | What lives here | Depends on | Status |
|---|---|---|---|
| **⚛ Planck** — the atomic scored decision | the one trait-weighted "score the options, pick one" helper every level reuses | — (foundation) | ⚫ NOT-STARTED |
| **🧬 Organism** — one faction's mind | needs-ladder (what it wants) · 12-trait `PersonalityDB` · mood · the Tick decision body · transition engine · officer characters | ⚛ Planck + the gauges | ⚫ NOT-STARTED (substrate ready) |
| **🌐 Ecosystem** — factions vs factions | rival-intel fog · scored stance-selection · NPC treaty policy · coalitions/betrayal emerge | 🧬 Organism + rival-intel | ⚫ NOT-STARTED (diplomacy substrate ready) |
| **🌌 Galaxy** — the arc + the crisis | early/mid/late emerges · late-game crisis = an existing faction's runaway ascension | 🌐 Ecosystem + capability-tech | ⚫ NOT-STARTED |
| **🪐 Supercluster / Brane** — authoring a universe | stage a franchise from JSON · the north-star acceptance test | all rungs + JSON schema | ⚫ NOT-STARTED (geography/factions already JSON) |
| **🔭 Exploration** *(cross-cutting)* | field sites to find · scientists with a career — feeds the Organism's objectives | independent; shares the competence prereq | ⚫ NOT-STARTED (survey spine ready) |

---

## The nervous system — ALREADY BUILT (what the brain plugs into)

Verified live on this branch. **Do not rebuild any of these — wire to them.**

| Substrate | Verified home (file:line) | Note |
|---|---|---|
| **The clock** — monthly per-NPC decision loop | `Factions/NPCDecisionProcessor.cs:17,19,42`; `GlobalManager` iterated `Engine/MasterTimePulse.cs:348`; liveness gauge `TickCount:29` | Fires live (proven by `FactionEconomyTests`). Body runs drift + doctrine-max; only axis→orders is TODO (`:79`). ⚠ its own header comment `:11-16` is STALE ("not iterated") — fix when touched. |
| **The hands** — headless order rail | `Engine/Orders/IOrderHandler.cs:10`; `StandAloneOrderHandler.cs`; ~15 sim-thread callers | A delegate issues the *same* orders a player does — no separate AI code path. |
| **The hands** — direct-call APIs | `Combat/FleetDoctrine.cs:47,34`; `Sensors/Emcon/FleetEmcon.cs:55`; `Factions/Diplomacy.cs:19`; `Factions/Treaties.cs:32,66,37`; `Combat/CombatEngagement.cs:485` (OrderAttack); `GroundCombat/GroundForcesDB.cs:388` | Doctrine/EMCON stay live mid-battle. Diplomacy/Treaties are static utils (bypass OrderHandler — no order record for Advise-mode; a real gap for later). |
| **The event bus** — pub/sub, ~250 kinds | `Engine/Events/EventTypes.cs:21-443`; `EventManager`; `Tech/ResearchProcessor.cs:28-33` reacts (not polls); every faction subscribes `Factions/FactionEventLog.cs:30-34` | The reactive spine: subscribe the brain the way ResearchProcessor already does. `CombatStarted` event not yet published (net-new). |
| **The delegate chassis** — durable officer-in-seat | `People/AdminSpaceAtb.cs:38`; `AdminSpaceProcessor.cs:47` `ReconcileSeats` (durable), `:106`/`:129` decapitation grave-rung; gauge `AdminSpaceSeatReconcileTests` | **Our branch — docs call this broken/"fix first"; it's already fixed.** Seats persist across recalc, survive save, empty on officer/component death. |
| **The competence→outcome wire** (proven TWICE) | research: `Tech/ResearchProcessor.cs:246` `RefreshPointModifiers` + `ResearcherDB.cs:16`; combat: `People/CommanderBonuses.cs:17,28` folded at `Combat/CombatEngagement.cs:1485-1501`, generator `NavalAcademyProcessor.cs:57`; gauges `CommanderCombatBonusTests` | **Our branch — the doc's roster #10 "STUB, no commander link" is STALE.** Flagship competence scales the fleet, academy→skill→fight end-to-end. |
| **The diplomacy substrate** | `Factions/DiplomacyDB.cs:21,48,60`; `RelationshipState.cs:35`; IFF reads it `Combat/CombatEngagement.cs:178…521,756`; first-contact from sensor loop `Sensors/SensorRecever/SensorScan.cs:120`→`Factions/FirstContact.cs:40`; `Treaties.cs`; `CasusBelli.cs`; `ReactiveDiplomacy.cs:58` | The tools the Ecosystem points outward — all real, all wired. Nothing calls `Treaties.Propose` yet (no NPC proposal policy). |
| **The planet-rung gauges** (needs-ladder inputs) | morale `Colonies/ColonyMoraleDB.cs:54`; legitimacy `Colonies/LegitimacyDB.cs:45`, collapse `:107`; rebellion `Colonies/RebellionDB.cs:37`; money `Factions/FactionInfoDB.cs:38`; own ship value `Combat/ShipCombatValueDB.cs`; war-standing `DiplomacyDB` | Every colony carries morale+legitimacy+rebellion, recomputed monthly — the **planet rung is pre-wired**. |
| **Scenario-as-JSON** (the authoring floor) | `Engine/Factories/DefaultStartFactory.cs:61` `LoadFromJson`; `Factions/FactionFactory.cs:57,71-81` (doctrine-vector parse); `Galaxy/StarSystemFactory` | A universe already loads from a JSON bundle: systems + jump-net + factions + designs. Real for geography/factions; aspirational for traits/ambition. |
| **The survey→reveal spine** (exploration) | `GeoSurveys/GeoSurveyProcessor.cs:25-83`; `Galaxy/PlanetRegionsDB.cs:142` `RevealRegion`; fires `EventType.GeoSurveyCompleted` | Richer than the doc knew — completion reveals regions + grants per-faction mineral access. The field-site loop hooks the same point. |
| **The PersonalityDB host** | `Factions/FactionInfoDB.cs:125` (Doctrine already lives here); `People/CommanderDB.cs` | The faction blob already seats identity data — the new blobs attach the same way. |

---

## The net-new to build (the brain itself)

None of these exist in source yet (grep-confirmed: they appear only under `docs/`). All have proven attach hosts or copyable patterns.

| Rung | Net-new piece | Kind | Host / pattern to copy |
|---|---|---|---|
| ⚛ | **`DecisionScorer`** — score options × identity weights, pick one | pure helper (no host) | CI-testable function; inputs = `PersonalityDB` + `DoctrineVector` |
| 🧬 | **`PersonalityDB`** — 12 traits (0–1) + mood dict + drift | blob | on faction (`FactionInfoDB.cs:125`, beside Doctrine) + `CommanderDB` |
| 🧬 | **`StrategicObjectiveDB`** — the goal slot / mandate the Tick writes | blob | on faction; attach-blob pattern proven |
| 🧬 | **Needs-tier field** (fractal: planet/system/empire) | field | rides colony / `AdminSpaceDB` system-seat / faction |
| 🧬 | **Trait-weighted validity scorer + transition engine** (commit + hysteresis) | pure logic | CI-testable; no host |
| 🧬 | **The Tick body** — read gauges → score objective → emit orders | logic | fill `NPCDecisionProcessor.cs:79-83` |
| 🧬 | **Faction roll-up gauges** — sum colony morale/economy + fleet strength to empire tier | cheap helpers | `FactionEconomySnapshot` / `FactionStrengthRollup` |
| 🧬 | **`ThreatEstimate`** — fog-limited enemy strength ("the eyes") | logic + slot | the one real gauge NO-SOCKET — contacts carry position/signal, not combat value (`EntityManager.cs:638`) |
| 🧬 | **Officer traits + tenure-weighted blend + shared drift** | fields + logic | trait fields on `CommanderDB`; tenure inputs already stored (`CommissionedOn`/`Experience`) |
| 🌐 | **Rival-intel fog store** — "what I think faction X's power/intent is" | blob | net-new on the sensor-contact model (`SensorContact.cs:42`, `FactionSystemInfoDB.cs:12`) |
| 🌐 | **Structural/rising-threat read** — fear rising power, not just attacks | logic | consumes rival-intel + `ThreatEstimate` |
| 🌐 | **NPC treaty/stance policy** — actually call `Treaties.Propose` | logic | the direct-call exists; the *policy* is new |
| 🌌 | **Capability-tech concept** — a tech that grants a capability, not a component | tech-model | today `Tech.Unlocks` moves item-IDs only — the crisis's single genuinely-new mechanic |
| 🌌 | **Ascension seed + crisis trigger** | data + logic | trigger enum exists (`EventTypes.cs:122 AnomalyDiscovered`) with no producer |
| 🪐 | **Author traits/ambition/mood/opening-diplomacy as JSON** | schema | opening-diplomacy exists in C# (`GameStageFactory.SetRelation:169`) — expose as data |
| 🪐 | **Decision-log gauges** (the emergence acceptance test) | readout | inert until the engine + logs exist |
| 🔭 | **`FieldSiteDB`** (type · shape {persistent/one-shot/incident} · yield · per-faction fog · assigned scientists) | blob | net-new; the one new data structure |
| 🔭 | **`FieldSiteProcessor`** (yield-over-time + one-shot resolve) | processor | reuses `ResearcherDB` funding dial + `AssignScientistOrder` verbatim |
| 🔭 | **Site catalog** (6 planetary + 5 space types) | JSON | mirror `CombatDoctrines`/`GroundStances` catalogs |

---

## Dead code — DO NOT build on (Landmine L1)

| Corpse | Verified (file:line) | Build on this instead |
|---|---|---|
| **`AdministratorDB`** — orphan stub, verbatim research-flavored copy | `People/AdministratorDB.cs:10-72`; `new AdministratorDB` = **zero** matches; zero `.cs` consumers | the live `ResearcherDB` + `CommanderBonuses` competence pattern |
| **`RuinsDB`** — never generates + nothing reads it | tautology bug `Galaxy/SystemBodyFactory.cs:1419-1420` (`!=A \|\| !=B` always true → early-return); only consumer is inside `GenerateRuins` itself | a new `FieldSiteDB` (reuse RuinsDB's size/quality enums if wanted) |
| **`ServeyAnomalyAction`** — throws | `Fleets/ServeyAnomalyAction.cs:14-32` `Execute`/`IsValidCommand`/`Clone` all `throw new NotImplementedException()` | the `FieldSiteProcessor` loop |

---

## The strategy — LEVEL THE GROUND FIRST, then build up (developer's call, 2026-07-11)

The design's "live-when-wired" pattern would let us ship the brain with several dials dark (espionage, trade, bloc-demands, gate-network, the crisis) as **labeled sockets** — present but inert until their subsystem lands. The developer chose the opposite, deliberately: **build the missing pillars FIRST so every trait, role, and rung wires into a LIVE system — nothing on the finished brain is a dark socket.** Then build up.

So the campaign is two movements:

- **Movement I — Foundations (level the ground).** Build the Tier-2/Tier-3 pieces that don't exist yet: the shared prerequisites, the "eyes," and the dark subsystems (espionage, trade-money, popular-demands, gate-network, capability-tech). Most already have a design doc — this is *build*, not *design*. **Honest scope: this is the bulk of the remaining work** — three of these (espionage, the trade economy, popular-demands) are each a subsystem in their own right.
- **Movement II — Build Up (the brain, on level ground).** The bottom-up brain phases below — but now every slice wires into a live pillar, no sockets left dark. Ends at the Brane acceptance test = *finish EVERYTHING.*

---

## Movement I — Foundations First (level the ground)

Ordered by dependency and cheapness. Each is a build track; the "Unblocks" column ties it to the Movement-II slice(s) it lights up. Statuses all ⚫ NOT-STARTED.

### Group A — cheap shared prerequisites (do first; small, each unblocks several things)
| # | Foundation | Verified gap / plug point | Design doc | Unblocks | Status |
|---|---|---|---|---|---|
| **F-A1** | ~~Fix the degenerate detection-quality~~ — **SATISFIED / SUPERSEDED (2026-07-11)** | the detection-quality bug was already FIXED (`Sensors/CLAUDE.md:160`, 2026-06-28); only a bounded multi-band quirk remains (a tidy standalone follow-up, not on the critical path). AND the AI merge **DESIGN-CUT `SignalQuality`** → detection is strength-only, so the eyes (F-B1) build on **signal STRENGTH + range**, not quality | `docs/DETECTION-DESIGN.md` | folded into F-B1 | ✅ superseded |
| **F-A2** | **Make competence researchable** — scientists ship with an EMPTY `BonusesDB`, so the live reader (`RefreshPointModifiers`) had nothing to fold; add the missing GENERATOR | built: `People/CommanderBonuses.cs` `RollResearchCompetence(cap, techCategory)` — the research twin of `RollCombatCompetence`, shaped for the reader (FilterId = tech category, Percentage); gauge `CommanderResearchBonusTests` (generator + real generate→read round-trip through `RefreshPointModifiers`, matching-category only; empty scientist unchanged → byte-identical). *Wiring it onto a live scientist path is a later slice (the deliberate rebalance) — this makes competence available.* | — | Organism 2.7 (officer traits), Exploration X.0 (field career) | 🟡 WIRED (CI-green `a0b3f17`) |
| **F-A3** | **Faction roll-up gauges** — sum colony population/morale/legitimacy + the ledger to the empire tier | built: `Factions/FactionRollup.cs` (`Balance`/`TotalPopulation`/`ColonyCount`/`MeanMorale`/`MeanLegitimacy`, population-weighted, read-only → byte-identical); gauge `FactionRollupTests`. *Military-strength roll-up deferred to F-B1 (needs cross-system ship enumeration).* | (this tracker) | Organism 2.2 (needs-ladder), F-B1 | 🟡 WIRED (CI-green `3855daf`) |

### Group B — the Eyes (rival perception; the linchpin, shared with espionage)
| # | Foundation | Verified gap / plug point | Design doc | Unblocks | Status |
|---|---|---|---|---|---|
| **F-B1** | **`ThreatEstimate` + the rival-intel "Information Ledger"** — own + fog-limited-enemy strength; a per-rival estimate that sharpens with detection and decays to Stale. The ONE true net-new *gauge*; it is BOTH the Ecosystem's missing input AND espionage's intel arm. **Built on signal STRENGTH + range** (F-A1's quality path was design-cut). | **F-B1a (own strength) built:** `Factions/FactionRollup.cs` `MilitaryStrength(faction)` sums own ships' `ShipCombatValueDB` Firepower+Toughness across systems; gauge `FactionRollupTests.MilitaryStrength_*` (excludes rivals). **F-B1b (fog-limited enemy estimate + per-rival store) pending** — contacts carry position/signal not combat value (`EntityManager.cs:638`, `SensorContact.cs:42`); no faction-level intel store yet | `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` (Information Ledger), `docs/DETECTION-DESIGN.md` | Organism 2.6 (Risk), Ecosystem 3.1–3.4, Galaxy 4.2 | 🟡 F-B1a CI-green (`9ec430b`); F-B1b CI-green (`38fee3f`); F-B1c (persistent Ledger + decay) later |

### Group C — the dark economic / political pillars (each a subsystem; already designed)
| # | Foundation | Verified gap / plug point | Design doc | Unblocks | Status |
|---|---|---|---|---|---|
| **F-C1** | **Trade-as-money** — a Trade `TransactionCategory` + income from standing trade agreements, so a trade agreement earns money | **F-C1a built:** `Factions/Ledger.cs` gains the `Trade` category + `Factions/TradeIncome.cs` `MonthlyIncomeFor(faction)` (sums standing `TradeAgreement`s × a per-agreement value); pure read → byte-identical; gauge `TradeIncomeTests`. **F-C1b built:** `Factions/TradeIncomeProcessor.cs` — a monthly `IHotloopProcessor` keyed on `DiplomacyDB` (L9-clean, L4-safe) that books `MonthlyIncomeFor` into the ledger under `Trade`, gated behind default-off `EnablePayout` (→ byte-identical); gauge `TradeIncomeTests.PayoutProcessor_*` (on → ledger grows; off → no trade txn). | `docs/DIPLOMACY-DESIGN.md` (commitment/exchange), `docs/RESOURCES-AND-MATERIALS-DESIGN.md` | role #6 Trade Minister; commerce diplomacy; Altruism (Ledger gift) | ✅ F-C1a (`3e591e5`) + F-C1b (`b66ac85`) CI-green |
| **F-C2** | **Internal popular-demands / bloc layer** — the Stellaris-parties demand engine on the morale/legitimacy substrate; an unmet demand pressures legitimacy | building per `docs/GOVERNMENT-AND-POLITICS-DESIGN.md`. **F-C2a built:** `Factions/PoliticalBlocs.cs` — the 5 fixed blocs (Labor/Merchants/Militarists/Liberty/Order) + derived `Loudness(gov, bloc)` (the government dials bias which blocs organise); pure → byte-identical; gauge `PoliticalBlocsTests`. **Next: F-C2b demand engine (morale factors → demands), F-C2c enact/refuse → legitimacy.** | `docs/GOVERNMENT-AND-POLITICS-DESIGN.md` (popular-demands) | role #3 Interior Minister; Authoritarianism (full) | 🟡 F-C2a WIRED (awaiting CI); F-C2b/c next |
| **F-C3** | **Espionage engine** — the Information Ledger (F-B1, shared) + agents as taskable operatives (Spymaster delegate + covert-action catalog + the risk/reward detection bet) | no engine — only a route name in `ExchangeCatalog` (Espionage route) | `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` | Guile (full facet); roles #4 Spymaster, #16 Agent; the mirror (NPCs spy on you) | ⚫ (needs F-A1 + F-B1) |

### Group D — the world / tech-model gaps
| # | Foundation | Verified gap / plug point | Design doc | Unblocks | Status |
|---|---|---|---|---|---|
| **F-D1** | **Jump-network rework / gate-network** — implement the dead `CreateConnection` stub so a discovered/built connection links two systems | `JumpPoints/JPFactory.cs:124` `CreateConnection` is an empty `// FIXME` stub | (the exploration + galaxy docs; hole H8) | Exploration network finds (gate/wormhole); hole H8 | ⏸ DEFERRED (developer's call 2026-07-11 — a late-game tech; lands with the exploration content it's tied to) |
| **F-D2** | **Capability-tech model** — a tech unlock KIND that grants a capability, not just moves an item-ID | built: `Factions/FactionDataStore.cs` gains a `Capabilities` set + `HasCapability(id)` + a `capability-` prefix branch in `Unlock` (a tech whose Unlocks lists a `capability-xxx` id grants the ability instead of no-opping); empty by default → byte-identical; gauge `FactionCapabilityTests`. | `docs/AI-GALAXY-AND-CRISIS-DESIGN.md` | Galaxy 4.1–4.2 (the crisis ascension) | 🟡 WIRED (awaiting CI) |

**Foundation build order:** Group A (cheap, parallelizable) → F-B1 (the eyes) → Groups C & D (independent subsystems, any order; F-C3 after F-A1+F-B1). Each track is itself CI-gated and byte-identical-first, and each has a live gauge before it's called done. When Movement I is green, the ground is level: every Movement-II trait/role/rung has a real system under it.

---

## Movement II — Build Up (the brain, on level ground)

Same discipline that kept the last 83 commits clean: **one slice at a time, a new field/blob defaults neutral so existing fixtures stay green, the payoff lands via a new example, push, WAIT for CI green before the next slice.** Each slice names its plug point and its gauge. **Assumes the Movement-I foundation each slice needs is green** (the "Unblocks" links above are the map).

### Phase 0 — ⚛ Foundations (unblock everything)
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 0.1 | `PersonalityDB` blob (12 traits, 0.5 default) + JSON parse | new blob on `FactionInfoDB`; mirror `FactionFactory.cs:71-81` | load/clone/parse round-trip test | ⚫ |
| 0.2 | `DecisionScorer` pure helper (score × identity weights) | new pure class | scorer unit test (weights bias the pick) | ⚫ |
| 0.3 | Faction roll-up gauges (`FactionEconomySnapshot`, `FactionStrengthRollup`) | sum over `Colonies`/fleets | rollup matches hand-sum | ⚫ |

### Phase 1 — ⚛→🧬 The first traits (proof-of-concept; no Tick body needed)
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 1.1 | **Zealotry/Xenophobia → treaty acceptance** *(the cheapest wire — start here)* | `Factions/Treaties.cs:66` `WouldAccept` (+`RequiredScore:37`) | accept-good-treaty at trait 0 vs refuse at trait 1 (two `RelationshipState`s, no processor) | ⚫ |
| 1.2 | Collectivism → retreat threshold | `Combat/CombatEngagement.cs:47` const + `:1401` `ShouldRetreat` | flees at 25% loss (0) vs fights to wipe (1) | ⚫ |
| 1.3 | Honor → keep-faith / renege | keep-faith live at `CombatEngagement.cs:1504/1529`; +new ~12-line `Diplomacy.BreakTreaty` | pact-intact (1) vs pact-broken (0) | ⚫ |
| 1.4 | Authoritarianism → tax-under-unrest | `GovernmentDB.cs:68` `TaxCeiling` + `RebellionDB:37`/`LegitimacyDB:107` | high-Auth holds tax under unrest, low-Auth cuts to appease | ⚫ |

### Phase 2 — 🧬 The Organism engine (the keystone)
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 2.1 | `StrategicObjectiveDB` goal slot | new blob on faction | write/read/clone | ⚫ |
| 2.2 | Needs-ladder read (Survive→Stabilize→Thrive→Ambition) from planet gauges + rollups | new logic over the built gauges | tier flips as morale/money/war-standing move | ⚫ |
| 2.3 | Transition engine (commit + hysteresis) | new pure logic | no thrash under noise; re-plans on real change | ⚫ |
| 2.4 | **Fill `NPCDecisionProcessor.Tick`** — score objective → emit orders | `NPCDecisionProcessor.cs:79-83` | an NPC colonizes/builds/attacks per its objective | ⚫ |
| 2.5 | Ambition/Aggression traits become live (ride the Tick) | the Tick body | expansion cadence tracks Ambition | ⚫ |
| 2.6 | `ThreatEstimate` (the eyes) → Risk trait | new logic over `GetSensorContacts` (`EntityManager.cs:638`) | engages at parity (Risk 1) vs demands 2× (Risk 0) | ⚫ |
| 2.7 | Officer traits + tenure blend + drift | fields on `CommanderDB` | officer character shifts a decision; drifts with tenure | ⚫ |

### Phase 3 — 🌐 The Ecosystem
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 3.1 | Rival-intel fog store | new blob on the sensor model | intel decays; ignorance is a real gauge | ⚫ |
| 3.2 | Structural/rising-threat read | logic over 3.1 + `ThreatEstimate` | a riser is feared before it attacks | ⚫ |
| 3.3 | NPC treaty/stance policy (call `Treaties.Propose`) | `Treaties.cs:32` | an NPC proposes a pact against a shared threat | ⚫ |
| 3.4 | Coalitions/betrayal emerge (Honor×Guile) | composition of 3.1–3.3 | alliance forms vs a riser, cracks when it dies | ⚫ |

### Phase 4 — 🌌 The Galaxy + Crisis
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 4.1 | Capability-tech concept (tech grants a capability, not a component) | extend `Tech.Unlocks` model | a tech flips a faction capability flag | ⚫ |
| 4.2 | Ascension seed + crisis trigger → coalition | producer for `EventTypes.cs:122`; reuse 3.x | one faction ascends → galaxy coalitions vs it | ⚫ |

### Phase 5 — 🪐 The Brane (authoring / acceptance test)
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| 5.1 | Author traits/ambition/mood/opening-diplomacy as JSON | extend `FactionFactory` schema; expose `GameStageFactory.SetRelation:169` as data | a scenario JSON sets a faction's personality + opening war | ⚫ |
| 5.2 | Decision-log gauges (emergence checkable) | readout over the engine | a run's decisions are legible + trace to inputs | ⚫ |
| 5.3 | **Acceptance test** — stage one aspect of a franchise, it plays believably | the whole stack | the north-star test (`docs/NORTH-STAR-VISION.md`) | ⚫ |

### 🔭 Cross-cutting — Exploration (independent; can run in parallel)
| # | Slice | Plug point | Gauge | Status |
|---|---|---|---|---|
| X.1 | Fix the ruins tautology (so something spawns to find) | `Galaxy/SystemBodyFactory.cs:1419` | ruins generate on qualifying bodies | ⚫ |
| X.2 | `FieldSiteDB` + `FieldSiteProcessor` + `siteId` on `ResearcherDB` | new blob/processor; reuse `AssignScientistOrder` | assign scientist → site yields/depletes | ⚫ |
| X.3 | Site catalog (JSON) — start with one "ancient ruins" entry | mirror `CombatDoctrines` | the loop runs end-to-end from data | ⚫ |
| X.0 | **Shared prereq — make scientist competence researchable** (today a scientist ships with empty `BonusesDB`; only one hardcoded line in `NewGameMenu.cs:632` grants any) | reuse `RefreshPointModifiers` + a competence roll | a trained scientist scales research/field yield | ⚫ |

> **X.0 is shared with the Organism:** the same "empty `BonusesDB` → make competence earnable" fix serves both the field-scientist career and the officer-competence blend (2.7). Build it once.

---

## Feasibility scoreboard (from the six surveys)

- **Traits (12):** 6 sit on a live decision point (buildable now) · 3 need the Tick body (Phase 2) · 3 sequenced behind an unbuilt system with a buildable down-payment (Guile→EMCON, Altruism→Ledger gift, Curiosity→exploration).
- **19-role delegate roster:** ~3 ride a built consumer today · 1 wired-but-unfed slot (Governor) · ~15 net-new seats (3-4 hard-blocked on dark pillars: espionage, bloc-demands, trade-money).
- **Wiring-map's 3 "already there" claims:** clock ✅ · event bus ✅ · order rail ✅ — all VERIFIED. Plus two branch-diff bonuses the docs miss: durable seats + commander combat-competence are already built.
- **The recurring gap across rungs:** the **"eyes"** — a fog-limited estimate of a rival's strength (`ThreatEstimate` / rival-intel). It's the one true net-new *gauge*, and it gates Risk, structural-threat, coalitions, and the crisis.

---

## How this tracker stays true

1. **Build a slice → flip its row** (⚫ NOT-STARTED → 🟡 WIRED-CI-GREEN → 🟢 verified-live), record the gauge reading, in the same commit as the code.
2. **A slice isn't done until CI is green** (both `test` + `build-client`), byte-identity held where claimed.
3. **Correct the design docs as you build:** the surveys found stale line numbers (`ShouldRetreat` is `:1401` not `:1158`; `AreHostile/AtPeace` `:1504/1529` not `:1271`; `FirstContact.OnDetection` `:40` not `:57`) and stale "unbuilt" claims (durable seats, commander competence). Fix the doc rung you're wiring in the same commit.
4. **Bottom-up or it doesn't work:** never start an Ecosystem/Galaxy/Brane slice before the Organism rung it composes is green.
5. **Foundations before the brain:** a Movement-II slice may not start until the Movement-I foundation in its "Unblocks" chain is green — that's the whole point of leveling the ground first. When you finish a foundation, flip its row AND note which Movement-II slices it just unblocked.

**Status legend:** ⚫ NOT-STARTED · 🟡 WIRED (CI-green, engine-verified) · 🟢 VERIFIED-LIVE (the developer's local runtime) · ⏸ BLOCKED (name the blocker) · 🔧 needs a named prereq first.
