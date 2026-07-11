# AI Implementation & Wiring Map — how the design plugs into the codebase

> **Status: v0.1 (2026-07-10).** The **bridge** from the seven AI design docs to the code — *not new design*, but **how each designed layer wires into existing structures.** Grounded in this session's surveys (the 5-agent implementation audit + the 4-agent completeness sweep); every code anchor was verified there. Legend per item: **✅ built (wire only)** · **🔧 new logic (no new mechanic)** · **🧱 new order/mechanic to build**.

---

## 0. Headline — the hard infrastructure is ALREADY there

We are not building a nervous system; we're plugging a brain into one that exists.

- **The clock fires.** `NPCDecisionProcessor` is an `IHotloopProcessor`, runs **monthly per NPC faction**, and the `GlobalManager` is now iterated so it genuinely fires. Its `Tick` body is the empty `// TODO` we fill. ✅
- **The event bus works.** `EventManager` / `EventType` (~250 kinds) is a live publish/subscribe bus, **proven** — `ResearchProcessor` already *reacts to events* (rebuilds on `TechDiscovered`) rather than polling, and every faction already subscribes to the full firehose via `FactionEventLog`. ✅
- **The order rail works.** `Game.OrderHandler.HandleOrder(cmd)` is headless, UI-independent, already driven from the background sim thread; plus the direct-call APIs (`FleetDoctrine`/`FleetEmcon`/`CombatEngagement`/`Diplomacy`/`Treaties`/`GroundForces`). ✅

**So the AI = mostly NEW LOGIC (the scoring/objective/transition engine) + a few NEW ORDERS (the tool punch-list) + WIRING (subscribe the brain to the events).**

---

## 1. Layer-by-layer wiring (design concept → code home → status)

### A. The brain's clock & reactivity (the cadence pyramid)
- **Monthly PLAN** → fill `NPCDecisionProcessor.Tick` (the stub). ✅ fires · 🔧 body.
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
- **Later: the officer-in-a-seat model** — the `AdminSpace*` seats + `CommanderDB`, with the **seat-durability bug fixed** (seats are rebuilt each pass) + a `LeaderLost` handler + the competence generator + skill fields on `CommanderDB`. The **three-mode dial** (Delegate/Advise/Hand-fly) = a field on the seat record; **Advise** needs a `ProposedOrder` staging state. 🧱
- **Character-assignment (HR)** = a HoS best-fit matching function. 🔧

### H. The mandate/report protocol
- **Two data slots per boundary** — a mandate the higher seat writes, a report the lower seat writes — read on each level's clock. **No message bus for v1.** The **fractal needs-tier IS the report/mandate content** (report "my tier" up; get tier-appropriate objectives down). 🔧

### I. The ecosystem & galaxy (EMERGENT — no new engine)
- **Same scorer, rivals as inputs.** Perception = fog-correct sensor contacts + `DiplomacyDB` ✅; inter-faction stance = the scorer (D) 🔧.
- **Reputation** = a fog-gated accumulator on `DiplomacyDB` (a 3rd party must *observe* the deed). 🔧
- **Alliances** = warming `DiplomacyDB` relationships (already drift) + a pact object. 🔧
- **The crisis** = a faction crossing an **ascension-seed** threshold (a game-changing tech/discovery), responded to via the same ecosystem coalition machinery — **no galaxy-AI, no new engine.** 🧱 (the ascension-seed content + its power).

---

## 2. The build / wiring order (each slice CI-green before the next)

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

*Companions: `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (the trait core, code-ready), `AI-SELF-PLAY-DESIGN.md` (roster + parity + completeness punch-list), and the design docs `AI-COMMAND-AND-COMMUNICATION` / `AI-OBJECTIVE-ENGINE` / `AI-ECOSYSTEM` / `AI-GALAXY-AND-CRISIS` / `AI-SUPERCLUSTER-AND-AUTHORING`. This doc is the bridge from those to the first commit.*
