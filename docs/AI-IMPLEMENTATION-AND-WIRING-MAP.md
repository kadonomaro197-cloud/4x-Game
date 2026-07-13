# AI Implementation & Wiring Map — how the design plugs into the codebase

> **Status: v0.1 written 2026-07-10 as a plan — now LARGELY BUILT (reconciled 2026-07-13).** The AI brain described here below as "logic to fill in" has since been built and wired: `NPCDecisionProcessor.Tick` is fully implemented (decision + planner + diplomacy + espionage + crisis engine), the seat-durability keystone is fixed, and the crisis ascension chain exists in code and data. **For LIVE build status, read `docs/AI-BRAIN-BUILD-TRACKER.md` — that is the single source of truth for what's built vs. not.** Most of what shipped is **flag-gated OFF by default** (order emission, diplomatic proposals, espionage, intel ledger) so a New Game is byte-identical until a flag is flipped — code exists and is wired, but is not live-in-a-default-game and its runtime is unverified here (CI can't run the client). This doc is kept as the **design→code bridge / historical wiring record**; the still-useful part is §4 (the AI-element→code-home index). Legend per item: **✅ built (wire only)** · **🔧 new logic (no new mechanic)** · **🧱 new order/mechanic to build**. Where an item below says "to fill" or "the stub," read it as a past-tense record of the plan — the build tracker has the current state.

---

## 0. Headline — the hard infrastructure is ALREADY there

We are not building a nervous system; we're plugging a brain into one that exists.

- **The clock fires.** `NPCDecisionProcessor` is an `IHotloopProcessor`, runs **monthly per NPC faction**, and the `GlobalManager` is now iterated so it genuinely fires. ✅ **(BUILT since this was written:** its `Tick` body — once the empty `// TODO` — is now fully implemented at `Factions/NPCDecisionProcessor.cs:127`: it reads the strategic objective (`UpdateStrategicObjective`, :143), runs the galaxy-crisis check (`:157-161`), and emits orders (`EmitOrders`, :167). The order-emission and diplomatic-proposal arms are **flag-gated OFF by default** — `EnableOrderEmission`/`EnableDiplomaticProposals` default `false` — so a default game is byte-identical; the crisis check `EnableGalaxyCrisis` defaults `true`. Runtime behavior is unverified here.)
- **The event bus works.** `EventManager` / `EventType` (~250 kinds) is a live publish/subscribe bus, **proven** — `ResearchProcessor` already *reacts to events* (rebuilds on `TechDiscovered`) rather than polling, and every faction already subscribes to the full firehose via `FactionEventLog`. ✅
- **The order rail works.** `Game.OrderHandler.HandleOrder(cmd)` is headless, UI-independent, already driven from the background sim thread; plus the direct-call APIs (`FleetDoctrine`/`FleetEmcon`/`CombatEngagement`/`Diplomacy`/`Treaties`/`GroundForces`). ✅

**So the AI = mostly NEW LOGIC (the scoring/objective/transition engine) + a few NEW ORDERS (the tool punch-list) + WIRING (subscribe the brain to the events).** *(This was the plan; the bulk of that new logic has since been built and wired — see the build tracker. It ships flag-gated off by default.)*

---

## 1. Layer-by-layer wiring (design concept → code home → status)

### A. The brain's clock & reactivity (the cadence pyramid)
- **Monthly PLAN** → fill `NPCDecisionProcessor.Tick` (the stub). ✅ fires · ✅ **body now BUILT** (`Factions/NPCDecisionProcessor.cs:127`; order-emission arm flag-gated off).
- **Event REACT** → in `NPCDecisionProcessor.Init`, subscribe to the interrupt `EventType`s (`ShipDestroyed`, `NewHostileContact`, `PopulationBombarded`, `UnrestIncreased`, `TreatyAgreed`, hazard…) exactly as `ResearchProcessor.Init` already does → a fast "re-evaluate this seat" path alongside the monthly plan. 🔧 wire.
- **Publish the missing interrupt** → have `CombatEngagement.EnsureInCombat` emit a new `CombatStarted` `EventType` (today combat is a 5 s poll nobody subscribes to). 🧱 tiny.

### B. The data model (the AI's state)
- **`PersonalityDB`** (12 traits + mood dict + drift-factor gauge) — a host-agnostic DataBlob on the **faction entity AND commander entities**. Mirrors `DoctrineVector` (a struct field on `FactionInfoDB`) + the `LegitimacyDB`/`ColonyMoraleDB` host-agnostic pattern. **Default 0.5-neutral** (the all-zero = all-extremes trap). Full spec in `AI-PERSONALITY-IMPLEMENTATION-SPEC.md`. 🔧 new.
- **`StrategicObjectiveDB`** (tiny) on the faction — the **goal slot** (the mandate the HoS writes, the delegates read). 🔧 new.
- **The needs-tier** (fractal) — a small field per level (planet/system/faction), each read from its own aggregate. 🔧 new.
- **Load from scenario JSON** — mirror `FactionFactory`'s `DoctrineVector` parse for a `"personality"` + `"ambition"` node. 🔧 wire.

### C. The eyes (perception → gauges)
- **Readable now:** money (`FactionInfoDB.Money`/`Ledger`), fog-correct sensor contacts (`EntityManager.GetSensorContacts`), diplomacy (`DiplomacyDB`), per-fleet strength (`FleetCombat`). ✅
- **Cheap wiring:** a faction-strength rollup + a faction-economy snapshot (sum over `FactionInfoDB.Colonies`/fleets — no helper today). 🔧
- **Real build:** an **enemy-strength estimate** (fog-limited — contacts carry position/signal, not combat value) + target value/defense. 🧱

### D. The scorer (the ONE engine)
- New `Factions/AIScoring.cs`: `Feature` enum + `IScoredOption` + `PersonalityWeights` map + `DecisionScorer` (feature-dot-product; `PickBest`/`PickWeighted`). **Pure, CI-testable.** 🔧
- **Reused by ALL selection** — objective pick, action-plan pick, stance pick, inter-faction stance. Build once. (The "one engine, no special cases" principle, in code.)
- **Blend** (`PersonalityBlend`, tenure-weighted) + **Drift** (`PersonalityDrift.ApplyEvent`, the `IdentityResistance` formula, the `EventKind→Pressure` table). 🔧

### E. The transition engine (objective → coordinated action)
- **Tier advancement objectives** + the **action-plan catalog** (data-driven, the *situation-type × archetype* matrix). Plans **compose atomic leader-tasks** = the tool vocabulary (layer F). 🔧 + data.
- **Phase-gates** — a short linear list, each gated by a completion gauge that **reuses the tier-reading gauges** (C). 🔧
- **Commit-and-hysteresis** (re-plan only on success / material blocker / big gauge-shift; action-momentum; smoothed reads; cost-of-reversal) — the anti-thrash logic. 🔧

### F. The hands (task → game action)
- **Built + headless:** `Game.OrderHandler.HandleOrder` + `FleetDoctrine`/`FleetEmcon`/`CombatEngagement.OrderAttack`/`Diplomacy`/`Treaties`/`GroundForces`/`IndustryOrder2`/`CreateColonyOrder`/survey/`DeployStationOrder`/`PlaceInstallation*`. ✅
- **The tool PUNCH-LIST (new orders the plans need):** `SetColonyPolicyOrder` (tax + stockpile + specialization), `RefitOrder`, a `ThreatCondition : ICondition` (so reactive standing orders can say "enemy in range"), the empty `RefuelAction`/`ResupplyAction.Execute` bodies, scrap/mothball, `SetGovernmentOrder`, wrap the diplomacy static calls as `EntityCommand`s (for Advise-mode). 🧱

### G. The seats & delegates
- **v1: the delegate is a PLAIN FUNCTION** the `Tick` calls (per design §6) — no seat formalism yet. Reads the objective, emits orders through F. 🔧
- **Later: the officer-in-a-seat model** — the `AdminSpace*` seats + `CommanderDB`, with the **seat-durability bug now FIXED** (✅ `AdminSpaceProcessor.ReconcileSeats` carries seats over by reference — see §4 keystone) + a `LeaderLost` handler + the competence generator + skill fields on `CommanderDB`. The **three-mode dial** (Delegate/Advise/Hand-fly) = a field on the seat record; **Advise** needs a `ProposedOrder` staging state. 🧱
- **Character-assignment (HR)** = a HoS best-fit matching function. 🔧

### H. The mandate/report protocol
- **Two data slots per boundary** — a mandate the higher seat writes, a report the lower seat writes — read on each level's clock. **No message bus for v1.** The **fractal needs-tier IS the report/mandate content** (report "my tier" up; get tier-appropriate objectives down). 🔧

### I. The ecosystem & galaxy (EMERGENT — no new engine)
- **Same scorer, rivals as inputs.** Perception = fog-correct sensor contacts + `DiplomacyDB` ✅; inter-faction stance = the scorer (D) 🔧.
- **Reputation** = a fog-gated accumulator on `DiplomacyDB` (a 3rd party must *observe* the deed). 🔧
- **Alliances** = warming `DiplomacyDB` relationships (already drift) + a pact object. 🔧
- **The crisis** = a faction crossing an **ascension-seed** threshold (a game-changing tech/discovery), responded to via the same ecosystem coalition machinery — **no galaxy-AI, no new engine.** ✅ **BUILT** (`Factions/GalaxyCrisis.cs` + the `tech-ascension`→`capability-ascension` chain in `techs.json`; `EnableGalaxyCrisis` defaults on — see §4). Ongoing: richer ascension-seed content via the Site Engine.

---

## 2. The build / wiring order (each slice CI-green before the next)

> **Reconciliation note (2026-07-13): this ordered plan is now largely EXECUTED.** Steps 1–4 and much of 5–7 are built and wired (the scorer, personality core, the eyes, the filled `Tick`, the reactive spine, many punch-list orders, the seat-durability fix, and the crisis/ecosystem layer). Read it below as the *record of the plan that was followed*, and see `docs/AI-BRAIN-BUILD-TRACKER.md` for exactly which slices are done vs. still open and which are flag-gated off. Most shipped slices are gated OFF by default; runtime is unverified here.

1. **Personality Core** (D: data + scorer + drift, pure, CI-testable) — the whole trait system *provable before any live wiring*. The foundation.
2. **The eyes** (C: the faction rollups + a rough enemy-strength read).
3. **Fill `NPCDecisionProcessor.Tick`** (A/B/E monthly plan): read tier → pick objective → write the goal-slot → a plain military delegate emits orders. **First visible AI** (Mars builds + attacks) in the Earth-Mars-alien scenario.
4. **The reactive spine** (A: subscribe to events + publish `CombatStarted`) — the AI *reacts*, not just plans.
5. **The tool punch-list** (F) — build each order as the archetype plans need it.
6. **The seat/officer formalism** (G: durability fix + skill fields + the three-mode dial) — later.
7. **The ecosystem layer** (I: reputation, alliances, the reading game) + **the crisis** — later.

---

## 3. The three wiring principles

- **One engine, reused.** Build the scorer (D) once; objective / action-plan / stance / ecosystem selection all call it. No per-domain AI code.
- **De-risk by structure.** Push logic into the **CI-tested engine**, never the client; the trait/scoring/drift core is *pure and provable first* (the whole demonstration gate green before anything touches the live loop). The client only ever gets a thin readout.
- **Data-driven.** Traits, ambitions, action-plans, ascension-seeds are **authored JSON over a fixed engine** (mirror the base-mod pattern) — the engine is code, the content is data.

---

---

## 4. SOCKET VERIFICATION — the "does every designed thing have a home?" sweep (3-agent adversarial pass, 2026-07-10)

**Bottom line: NO core design gap — nothing in the AI machinery (traits · scorer · objectives · command · the 21-seat roster) is homeless.** Every element is socket-exists or a clean must-build with a *proven* home. The adversarial sweep caught **two load-bearing NO-SOCKETs** (core mechanics the design leans on — NOT dark pillars) and **one keystone hostile-socket**. **Reconciliation (2026-07-13): of those three, the keystone (seat durability) and the crisis-ascension breakthrough have since been BUILT (marked ✅ below); the fleet-commander situational read remains the open gap.** Verified `file:line` by three agents (original), re-verified against current source (reconciliation).

### 🔴 The REAL gaps — one still open, two now built
1. **The fleet commander's SITUATIONAL READ — NO-SOCKET, and it's load-bearing.** This is bigger than "how strong is *that* enemy fleet" (developer's framing, 2026-07-11): a commander doesn't stare at one contact — it **synthesizes everything it can see** (its own force, the *estimated* enemy force, whether escape is even possible, what's at stake behind it) into one of a handful of decisions — ***"I can take him"* · *"nevermind — run silent, opposite heading"* · *"we lost everything but we're going out swinging"* (a last stand).** That's a **scored engagement decision**, and it's exactly where personality bites: the last-stand vs. the retreat is the *same* board read filtered through **Honor / Zealotry / Risk** (a Klingon swings; a Ferengi runs). So the socket is two layers:
   - *The gauge (the missing input):* an **enemy-strength ESTIMATE**, fog-limited. `EntityManager.GetSensorContacts` (`Entities/EntityManager.cs:638`) returns **position/signal, not combat value** (`ShipCombatValueDB` lives on your OWN ships). **Build a `ThreatEstimate`** — infer strength from contact count/signal/known designs, degraded by fog (a dark contact you can't size is *itself* a decision — engage-to-identify vs. avoid).
   - *The decision (what reads the gauge):* an **engagement scorer** that weighs own-strength ÷ threat-estimate × escape-viability × strategic-value-of-holding, **through the seated commander's traits**, and emits fight / disengage / last-stand. Reuses the same `DecisionScorer` as everything else (`AI-PERSONALITY-IMPLEMENTATION-SPEC §3`) — one engine, combat-flavored features.
   *Cannot be hand-waved* — with **no rubber-band** to forgive a miss, a bad read is an unrecoverable snowball loss; this is "the ONE thing we must get right" (`AI-ECOSYSTEM-DESIGN §1`) at the tactical scale.
2. **The CRISIS ascension-seed — was NO-SOCKET on BOTH halves; ✅ the breakthrough half is now BUILT (2026-07-13).**
   - *Activation (partial):* the explore→discover→reward pipeline now has a home — the **Site Engine** (`GameEngine/Sites/`, SE-1..SE-5 complete per `Sites/CLAUDE.md`) is the located-episode/exploration-content system that fills what was called the "unbuilt exploration-content system." Demo sites are **flag-gated OFF** (`SiteScenario.AutoSpawnSites` default `false`, `Sites/SiteScenario.cs:28`) so a New Game is byte-identical. `ServeyAnomalyAction` still exists (`Fleets/ServeyAnomalyAction.cs`); the discovery-content authoring is the ongoing SE-6 work.
   - *The breakthrough itself — ✅ BUILT.* The "a capability, not a component" tech concept the original plan called for now exists: `Factions/GalaxyCrisis.cs` defines `AscensionCapability = "capability-ascension"` and a faction that `HasCapability` it (`:42`) is the galaxy crisis; the `tech-ascension` "Transcendence" research in `GameData/basemod/TemplateFiles/techs.json` grants that capability (a tech that grants a capability rather than unlocking a component — `Factions/FactionDataStore.cs:48`). The crisis is LIVE by default (`NPCDecisionProcessor.EnableGalaxyCrisis` defaults `true`); when a faction ascends, `GalaxyCrisis.FormCoalitionAgainstAscendant` (`:60`, called from `NPCDecisionProcessor.cs:161`) declares a coalition war. Runtime behavior is unverified here (CI can't run the client).

### ⚠ The KEYSTONE hostile-socket — ✅ NOW FIXED (was: fix FIRST before ANY seat/officer work; developer: "paramount, or else nothing can be built," 2026-07-11)
**Seat durability — BUILT (2026-07-13).** The bug (`CalcEntityAdminSpace` doing `CommanderSeats = new List<>()` every pass, silently wiping any field on a seat) is fixed: `People/AdminSpaceProcessor.cs:47` now calls `ReconcileSeats(adminSpaceDB.CommanderSeats, current)` (`:57`), which **carries over each existing seat — and the commander sitting in it — BY REFERENCE so a seated commander survives the recalc** (doc-comment `:52-55`); a seat whose component is gone is dropped and its occupant's `CommanderDB.AssignedTo` is cleared (the decapitation grave-rung). Adding a field to the seat record is now safe. This unblocks the whole officer/seat delegate layer. *(Original text preserved for context: the plain-class `AdminSpaceAbilityState` and the two smaller hostile sockets — `InstallComponentInstanceOrder.Clone()` and `ServeyAnomalyAction` — should be re-verified against current source before relying on them.)*

### 🟡 MUST-BUILD (clean home, just write it) — the expected new logic + the punch-list
The whole scorer/`PersonalityDB`/objective/tier/mood/ambition core (none exists — the expected new logic; homes proven) · the goal slot (`StrategicObjectiveDB`) + mandate/report slots (proven attach-blob-to-faction pattern) · `CombatStarted` event + publish at `CombatEngagement.cs:517` · faction rollups (`FactionEconomySnapshot`/`FactionStrengthRollup` — per-unit sources exist) · reputation accumulator (clean home on `RelationshipState`) + observation-propagation · alliance-DEPTH accumulator (shared-adversity deeds; drift today reads only militarism/treaties) · the punch-list orders (`SetColonyPolicyOrder` — tax + stockpile MUST-BUILD, **specialization = NO-SOCKET/no substrate**; `RefitOrder`; `ThreatCondition`; the empty refuel/resupply `Execute` bodies; `SetGovernmentOrder`; wrap-diplomacy-statics-as-orders for Advise mode; the ground **load→move→land** order — the Generals can't project force between worlds without it) · the `AssignedOn` tenure date.

### ⚫ DARK PILLARS — owner assigned, WHOLE mechanic must be built (each has its own design doc)
Espionage (Spymaster/Agent — zero code, `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`) · bloc-demands (Interior Minister — no engine, `GOVERNMENT-AND-POLITICS-DESIGN.md`) · trade-money (Trade Minister — no `Trade` `Ledger` category, `RESOURCES-AND-MATERIALS-DESIGN.md`). Not homeless-in-design — the seats correctly own decisions over systems that don't exist yet.

### 🟢 SOCKET-EXISTS (confirmed real)
`PersonalityDB` homes (faction `FactionInfoDB.cs:125` + officer `CommanderDB.cs:10`) · the `ModifiableValue` trait→number wire (`ResearchProcessor.cs:246`, with per-id `RemoveModifier`) · the event bus + all six named interrupt EventTypes · scenario-JSON parse (`FactionFactory.cs:71-81`) + the moddable-catalog pipeline (`combatDoctrines.json` pattern) · the gauge sources (`Ledger` / `ColonyMoraleDB` / `LegitimacyDB` / `DiplomacyDB` / fog-correct `GetSensorContacts`) · the order rail + most seat levers · alliance warming→pact (already drifting, `RunDiplomaticDrift:97`) · the emergent arc (no socket needed by design).

### Spec correction (applied)
`AI-PERSONALITY-IMPLEMENTATION-SPEC.md §5` claimed the officer tenure fields (`CommissionedOn`/`RankedOn`) are "never set." **FALSE** — they're stamped in four creation paths (`DefaultStartFactory.cs:284`, `FactionFactory.cs:238`, `ColonyFactory.cs:169`, `NavalAcademyProcessor.cs:24`). The only real tenure gap is the missing `AssignedOn` date.

---

*Companions: `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (the trait core, code-ready), `AI-SELF-PLAY-DESIGN.md` (roster + parity + completeness punch-list), and the design docs `AI-COMMAND-AND-COMMUNICATION` / `AI-OBJECTIVE-ENGINE` / `AI-ECOSYSTEM` / `AI-GALAXY-AND-CRISIS` / `AI-SUPERCLUSTER-AND-AUTHORING`. This doc is the bridge from those to the code — written as the plan before the first commit, kept as the historical wiring record now that the brain is largely built. For live build status see `AI-BRAIN-BUILD-TRACKER.md`.*
