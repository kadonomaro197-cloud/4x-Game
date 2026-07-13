# AI Decision Engine — the DESTINATION and the MIDDLE (what a faction wants, and how it gets there)

> **What this is.** The two halves of the faction AI's decision-making, folded into one doc. **PART A — the DESTINATION:** how a faction figures out *what it wants* (the needs-ladder objective engine — instinct tiers that emerge from the faction's state, filtered by identity and mood). **PART B — the MIDDLE (the means-ends planner):** how a *settled* objective becomes a *reachable* chain of orders — walking a goal backward through its prerequisites until it hits something the faction can actually do this cycle. Part A picks the destination; Part B plots the course to it. Together with `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how" — delegates, mandates, traits, mood) they are the whole brain: identity → destination → plan → orders.
>
> **Consolidated 2026-07-13 from:** `docs/AI-OBJECTIVE-ENGINE-DESIGN.md` (the destination half, v0.2 discussion draft, 2026-07-10) + `docs/AI-MEANS-ENDS-PLANNER-DESIGN.md` (the means-ends planner scope/build map, 2026-07-11 four-agent survey).
>
> **Build state, honest (read this first):**
> - **PART A (objective/destination engine)** is a **DISCUSSION DRAFT — not built as designed.** The pieces it *names* as existing are noted inline. Its refinements are the developer's locked calls from the 2026-07-10 conversation, but the transition-engine catalog, fractal per-level ladder, and action-plan machinery are **design on paper, not yet code.**
> - **PART B (means-ends planner)** front-end is partly **built-but-gated-off**: `NeedsLadder → ObjectiveSelector → StrategicObjectiveDB` is described as **LIVE** in the emit-side survey, but the planner/`EmitOrders` act-half is **gated behind `EnableOrderEmission` (default OFF)** — the arms are empty stubs today, byte-identical-off until opted in. Nothing here is claimed as runtime-verified (CI can't run the client).

---
---

# PART A — THE DESTINATION (Objective Engine)

> **Origin:** the "destination" half of the faction AI — the counterpart to `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how": delegates / mandates / traits). The developer's original framing was *"the big AI sets the destination, the small AIs decide how to get there."* We designed the small AIs in depth; **this half designs the destination.** Built on the identity+mood model (`AI-COMMAND…DESIGN §3`) and feeds the mandate cascade. Design decisions here are the developer's calls from the 2026-07-10 objective-system conversation.
>
> **v0.2 headline:** the needs-hierarchy is **FRACTAL** — every level (planet→system→empire) runs its own ladder read from its own aggregate, bubbling up from the planet, so levels sit at different tiers at once (§A2a); each tier is also a to-do list of advancement objectives that climb to the next (§A2b); blended tiers + small weighted stack confirmed at every level (§A7 resolved).

## A0. Core idea — a faction's goal is neither fixed NOR free; it EMERGES

Three inputs produce a faction's *current* objective:
1. **WHO IT IS** — authored identity: the 12 traits + the faction's *designed grand ambition*. (Player authors their own faction; we author the NPCs.)
2. **HOW IT FEELS** — its transient **mood** (Wounded / Emboldened / Vengeful / Cornered …).
3. **WHERE IT STANDS** — its situation, read as a **needs-tier** (survive → stabilize → thrive → ambition).

The objective is not scripted and not a free-for-all: it *emerges* from a **needs-hierarchy filtered by identity, mood, and situation** — using the same scored-choice machinery as the trait system.

## A1. What a faction can want — authored ambition, universal needs

Objectives are **NOT a flat universal menu** every faction picks from identically. Each faction is **authored** with what it ultimately wants and how much it weights each aim — the same "author the faction top-to-bottom" philosophy as its ship-ladder and its traits. **Player** designs their own faction's wants at game start; **we (designers)** author the NPCs.

**But the split is:** the *lower* tiers (survive / stabilize / thrive) are **universal** — every faction must live before it can dream — while the *top* rung (the grand ambition) is **authored per faction**. So "what a faction can want" = the universal needs-ladder, with its **summit authored per faction**.

## A2. The needs-hierarchy — instinct tiers (the load-bearing idea)

A **Maslow's-hierarchy for factions.** The faction's *current tier* is read from its **state**; a lower unmet need **dominates and suppresses** the higher aims. As the faction grows/stabilizes/prospers it **climbs**; a shock **knocks it back down**.

| Tier | Instinct | Active when… | The kind of objective it produces |
|---|---|---|---|
| **0 — SURVIVE** | don't die | homeworld threatened · militarily outmatched · economy failing · losing a war | defend the core · sue for peace · rebuild · avoid new fights |
| **1 — STABILIZE** | secure & efficient | survival met, not yet strong | fortify borders · fix the economy · quell unrest · defensive tech · hold key systems |
| **2 — THRIVE** | grow & prosper | secure | colonize · expand economy/tech · grow the fleet · build influence |
| **3 — AMBITION** | the authored destiny | thriving | **the faction's DESIGNED grand aim** — Klingon glory-conquest · Federation peace/exploration/uplift · Borg assimilate-all · Mars supremacy · (the player's own authored aim) |

**Why it's powerful:**
- **Legible + dynamic** — read the state, know the tier, know the *kind* of aim. No opaque black-box goal-picking.
- **It handles the tutorial asymmetry FOR FREE, zero scripting.** Earth (one planet, losing to Mars) is *automatically pinned at Tier 0 — Survive*; Mars (winning) sits at Tier 3 pursuing "conquer Earth." The scenario's whole shape **emerges from each side's state.** As the player rebuilds Earth it climbs the ladder on its own (Survive → Stabilize → Thrive → its own ambition — retake Sol?). The comeback isn't scripted; the ladder produces it.
- It **is** the developer's "very base instinct is to survive; as it grows and becomes stable and efficient it shifts from surviving to thriving and so on."

### A2a. The hierarchy is FRACTAL — every level runs it, and it starts at the PLANET (developer's call, 2026-07-10)

The needs-ladder is **not** empire-only / HoS-only. **EVERY level runs its own ladder, read from its own aggregated state:**
- A **planet** reads its own survival (besieged? starving? stable? prospering?).
- A **System Governor** reads the **aggregate of all its planets** — a system with nine healthy worlds and one under siege sits in **Thrive** even while that one world is in **Survive**.
- The **HoS** reads the **aggregate of all its systems**.

**It starts at the planet and BUBBLES UP.** The empire's tier is never *declared* — it **emerges** from the state of its parts (most planets in Survive → the empire is in Survive; core thriving while a frontier bleeds → the empire reads Thrive-with-a-wounded-edge). This is the same **subsidiarity** as decomposition (§A5), applied to *state + goals*: each level assesses locally, and the assessment aggregates upward.

**Levels can sit at DIFFERENT tiers at once, with no contradiction** — each reads a different aggregate. The tutorial, exactly: Mars (empire) at **Ambition**; a besieged **planet** at **Survive**; the **System Governor** at **Stabilize/Thrive** because it averages every populated place under it. One map, three tiers, simultaneously — and that's *correct*.

> **This unifies the objective engine with the mandate/report protocol** (`AI-COMMAND…DESIGN §2`): "report up" now partly means *"here's my tier"* (a planet tells its system it's in Survive), the system aggregates, the empire reads; "mandate down" is *tier-appropriate objectives*. Destination-engine and communication-engine are the **same fractal structure** seen from two directions.

### A2b. Climbing the ladder — each tier is also a TO-DO LIST (developer's requirement)

A tier isn't just a label; **the AI must know what to DO to grow from one state to the next.** Each tier carries its **advancement objectives** — the aims whose satisfaction *is* the condition to climb:
- **Survive → Stabilize:** secure the homeworld/defense · restore food/power · stop the bleeding · end or freeze the losing war.
- **Stabilize → Thrive:** balance the budget · quell unrest · secure the borders · reach a defensive-tech floor.
- **Thrive → Ambition:** hit a strength/prosperity threshold · secure a growth base · out-tech the immediate rivals.
- **Ambition:** pursue the authored grand aim.

So the ladder is **self-propelling**: pursue your tier's advancement objectives → satisfy the tier's gauges → climb → inherit the next tier's objectives. **"How do I grow from state to state" is answered by construction — the tier IS the to-do list.** Each level runs a **small weighted stack** (mostly its current tier's objectives, a sliver of the next tier's prep), so a planet 70%-secure already spends a little on stabilizing while mostly surviving — **blended tiers, not hard switches**, at every level.

## A3. Readability — quantifying the situation into something a faction can READ (the hard problem)

To know its tier **and** whether an objective is still valid, a faction reads a small set of **legible gauges** computed from game state (all reachable from the "eyes" already surveyed — economy/military/threat/morale):
- **Survival gauges:** homeworld-threat level · own-vs-threat military ratio · economy solvency · war-loss trend.
- **Stability gauges:** border security · internal unrest / legitimacy · efficiency.
- **Prosperity gauges:** growth rate · tech standing · expansion room.
- **Per-objective VALIDITY gauges:** each objective carries *"still worth pursuing?"* conditions expressed in these gauges, **weighted by the faction's TRAITS.**

**The Klingon "this war is no longer honorable" — worked:**
- The objective *Conquer X* carries validity conditions. One is a **war-honor gauge** = *fair fight (enemy not helpless)* × *kept our word (didn't betray to start it)* × *glory-not-slaughter (not just bombing civilians)*.
- A **high-Honor Klingon** weights that gauge heavily → when it drops below threshold the objective goes **invalid** → they sue for peace (with a shame/disillusion mood that suppresses re-declaring).
- A **low-Honor Romulan** weights that same gauge ≈ 0 → the identical dishonorable war is perfectly acceptable → they fight on.
- **Same situation, opposite decision, purely from trait-weighted gauges.** Define the gauge set once; each objective declares its validity conditions in gauges; the existing scored-choice engine (trait system §3c) does the weighting. This is how we "quantify the variables into something the faction can read."

## A4. Selection — identity × mood × tier → the objective

Each cycle (the monthly plan **+ on-event**, per the reactive spine): compute the situation gauges → determine the **dominant unmet tier** → generate the **candidate objectives** for that tier (Tier 3 draws the faction's *authored ambition*) → **score them by identity + mood** → pick. **Mood can knock the tier down** (Wounded → drop to Survive even when objectively secure) **or push over-reach** (Emboldened → chase the Tier-3 ambition before truly thriving).

## A5. Decomposition by SUBSIDIARITY — start at the lowest competent level

The chosen objective becomes a cascade of sub-mandates, and **decomposition starts at the LOWEST seat that can see the whole problem**, escalating up only when needed:
- A **SYSTEM-scoped** objective ("deal with Mars," Mars is in Sol) decomposes at the **System Governor** — the local hub with the system knowledge to translate a broad want into concrete slices, farming the military slice to the System Admiral/General and the economic slice to the Planetary Governors.
- Only if **no system-authority exists**, or the objective is **EMPIRE-scoped** (spans systems), does decomposition start at the **HoS**.

This is **subsidiarity + local-knowledge:** the HoS sets the *want*; the level with the local picture does the *planning*. It makes the **System Governor the key translation seat** between empire-intent and local-action.

> **Open sub-question:** for a system-scoped *military* objective, does the System Governor coordinate it, or does the System Admiral/General decompose its own military slice? **Proposed:** the System Governor owns the system's overall *effort allocation* ("the military effort here goes to taking Mars"), and each domain seat decomposes *its own slice* (the System Admiral plans the actual fleet moves). The Governor allocates; the domain seats plan.

## A5a. The TRANSITION ENGINE — what the AI does to change state, and how the 19 leaders coordinate (developer's question, 2026-07-10)

This is the mechanism that turns a tier-flag into coordinated action across the seats.

### Action-plans — the catalog of ways to satisfy an advancement objective
A tier-gap (§A2b) generates an **advancement objective**; that objective is satisfied by one of a **data-driven catalog of ACTION-PLANS**, each a *coordinated bundle of leader-tasks* (not a single move). The faction **scores the plans by identity + mood + situation** and picks one (its weighted stack — a primary, maybe a hedge). Example catalog for a "losing a world" (Survive) situation:

| Plan | What it does | Trait-profile it favors |
|---|---|---|
| **Reinforce** | transfer troops/fleet from elsewhere, pour in defenses | ambitious · values the prize · has slack |
| **Fortify & hold** | dig in, trade space for time, out-last the assault | defensive · patient · high-Honor stand |
| **Scorched-earth / Deny** | *"if I can't have it, nobody can"* — glass/poison/deny the prize | high **Ruthlessness / Zealotry** · **Cornered** mood |
| **Fighting retreat** | evacuate what matters, cut losses, preserve the force | low-Zealotry pragmatist · Guile · values-each-life (Eldar) |
| **Sue for peace** | freeze the war, buy time | diplomatic · low-aggression |
| **Counter-attack** | best defense — hit the enemy's staging base | bold · high-Risk |

**Same flag, different response** — the divergence is one scored choice at the top: a **Borg** profile scores Scorched-earth / fight-to-annihilation high (Ruthlessness + Collectivism + no-retreat); an **Eldar** profile scores Fighting-retreat / preserve high (Guile + risk-averse + values-each-life); a pragmatic **Earth** scores Reinforce-and-negotiate. Both drastic options the developer named — *transfer troops in* (Reinforce) and *deny the prize* (Scorched-earth) — are catalog entries; which fires is the trait-scored pick. Each plan is **data-driven** (author faction-appropriate plans; the trait-scoring selects), same pattern as the stance/exchange catalogs.

### Coordination — mission command, not a scheduler
The 19 leaders **self-synchronize**; they are NOT handed a micromanaged plan. Exactly the way a real military coordinates under *commander's intent*:
1. **The chosen plan writes a tier-mandate to the shared goal-slot** — "Earth: SURVIVE, hold at all costs, plan = Fortify-and-hold." Commander's intent, visible down the chain (this IS the mandate cascade, `AI-COMMAND…DESIGN §2`).
2. **Each involved leader, on its own cycle, reads the mandate + current state and makes its own scored decision to serve it** — Governor → defenses in the build queue; Admiral → hold orbit; General → dig in the garrison; Chief Scientist → research-target shields; Foreign Minister → sue for time; Spymaster → sabotage the invasion fleet.
3. **Coordination emerges from three things — no scheduler:** the **shared mandate** (everyone pulls one direction), the **physical dependency** (a leader can only act on the state that exists — the General can't fight with troops the Governor hasn't raised; fortifications only matter if the Admiral holds orbit so the enemy can't just bombard them), and **light phase-gates** for the genuinely sequential (achieve orbit → *then* invade).
4. **Report-up triggers RE-PLAN.** If a leader can't do its part — Admiral reports "can't hold orbit, outnumbered 3:1" — that escalates, the faction **re-scores the catalog**, and may drop to a more desperate plan (Fortify → Scorched-earth, or → Fighting-retreat). *This is where "if I can't have Earth, nobody can" gets chosen* — not up front, but when the hold plan fails and a Cornered/ruthless faction re-picks.

**Net: the mandate aligns them, physics sequences them, escalation re-plans them.** Decentralized, robust, and the same delegate-issues-its-own-orders model as everywhere else — which is why it also closes the **"Organ" rung** (how a whole chain coordinates internally).

### Worked example — the tutorial, losing Earth, three factions
- **Pragmatic Earth (AI-run):** HoS mandate SURVIVE→hold. Plan = Fortify-and-hold + Sue-for-peace hedge. Governor builds defenses + war-taxes; General fortifies; Admiral holds Earth orbit; System General de-prioritizes the lost outer colonies; Chief Scientist reaches for shields/point-defense; Foreign Minister seeks a ceasefire/ally; Spymaster sabotages Mars's fleet. Admiral fails → re-plan to Fighting-retreat (evacuate key pop/industry).
- **Borg profile, same situation:** no evacuation (cells expendable), no ceasefire (no diplomacy), fight in orbit to the last; on failure → Scorched-earth (deny the world).
- **Eldar profile, same situation:** Fighting-retreat from the start — preserve the population to a refuge, strike back indirectly via Guile (sabotage, proxy), never trade lives for dirt.

One catalog, one scoring engine, three completely different 19-leader campaigns.

### Transition-engine details — RESOLVED (developer's calls, 2026-07-10)

1. **Granularity → a bounded set of SITUATION-TYPES, each with ~5–8 archetype plans.** There's a finite list of strategic situations the game produces — **losing a world · a rising rival · an expansion window · an internal crisis (unrest/rebellion) · an outmatched-tech gap · first contact** — each with its own small catalog of archetype responses. Legible ("the 6 ways to handle losing a world"), tunable; grow the situation-list as systems come online. *Bounded situations × ~6 archetypes*, not one giant flat list.
   > **Refinement (2026-07-10): archetypes COMPOSE from a defined TOOLSET, so the toolset must be pinned first.** An archetype ("Reinforce") is an *ordered recipe of atomic leader-actions* ("tools") — we can't author archetype *content* until the tool vocabulary is fixed. But **the toolset IS the leader-action catalog already surveyed** (every order + direct-call), and the *missing* tools ARE the completeness-sweep punch-list (`SetColonyPolicyOrder`, `RefitOrder`, `ThreatCondition`…). So we lock the archetype **STRUCTURE now** (an ordered, capability-gated, trait-scored composition of tools) and author the **content** once the tools are finalized — no new discovery, it rides the existing punch-list. The **situation × faction-type matrix** is covered by generic-skeleton + capability-gates + trait-scoring + bespoke-signature (item 4), NOT by a separate catalog per faction type.
2. **Phase-gates → a short LINEAR phase list (2–4), each gated by a completion gauge. NOT a dependency graph.** A plan is a mini-sequence of sub-mandates, each with a readable "done?" gauge that opens the next ("Invade Mars" = Phase 1 *achieve space superiority [gauge: control Mars orbit]* → Phase 2 *take the surface [gauge: regions held]*). The active phase IS the current mandate; leaders read "which phase are we in" and serve it (Admiral works Phase 1, General waits for Phase 2). Reuses the tier-reading gauge machinery (§A3); a linear list handles real ops without a brittle scheduler.
   > **Refinement (2026-07-10): the phase-gauges ARE the tier-state sub-gauges, so a stuck transition is SELF-DIAGNOSING.** When a Governor is "having difficulty transitioning" (stuck climbing Survive→Stabilize), whichever phase-gauge is **RED** names *exactly* the blocker (food? orbital defense? the war?) and therefore the fix — pour into the red one. Unifies phase-gates with the readability gauges (§A3); it's the **Visibility Gate turned inward on the AI** — it can *see why it's stuck* and act precisely instead of flailing. "The governor knows exactly what the issue is and how to progress." *(This is the same self-diagnosing principle Part B builds in code as the mineral-floor shortfall sensor + the plan/queue readout — §B "The named mires" and §B-E.)*
3. **Re-plan → COMMIT-AND-HYSTERESIS, governed by mood (the anti-thrash design).** Commit to the plan; only re-score on **success** (objective met → climb), a **material blocker** (a leader reports genuinely-impossible, not merely hard), or a **big gauge-shift** (homeworld falls / major new threat / ally joins). Three guards stop flip-flop: a **minimum-commitment floor** (no re-plan for a while except on catastrophe), a **hysteresis band** (a new plan must beat the current by a MARGIN), and **mood sets the cadence** (Cornered/Wounded → re-plans fast + drastic; stable → holds course). Re-plans **escalate DOWN the desperation ladder** (Fortify fails → Scorched-earth), they don't flip laterally — so it reads as *progressive desperation / a faction breaking under pressure*, never indecision. *(This monthly re-plan-with-hysteresis loop is the same `ObjectiveTransition` loop Part B rides — see §B "The architecture" step 4.)*
   > **Refinement (2026-07-10): plan-level hysteresis isn't enough — add ACTION-level commitment** to kill the "pull the fleet → nevermind → pull it again" yo-yo in a volatile situation (the oscillation happens *below* the plan). Three more guards: (a) **action momentum** — once a leader commits an action (fleet en route), it runs to a *decision point* (arrival) before it can be reconsidered, not re-judged every tick; (b) **smoothed situation-reads** — a gauge change must *persist* (moving average / "sustained for N") to trigger anything, so a one-month flicker never trips a re-plan; (c) **cost-of-reversal** — reversing a committed move eats its sunk cost (fuel/time already spent), so the AI won't reverse unless the new course clearly beats staying. Mood decay keeps a Cornered posture *coherent* rather than blinking off the instant the threat flickers.
4. **Authored vs. computed → GENERIC SKELETONS, faction-flavored, + a bespoke hook.** Archetypes are generic + shared, each defined once as {leader-tasks + phase structure + base trait-profile}. The faction flavors them via (a) **trait/ambition scoring** (which plans it favors — Borg weights Scorched-earth up), (b) **capability gates** (a plan is only *available* if the faction can do it — no Scorched-earth without orbital bombardment, no Reinforce without spare troops), and (c) an optional **authored SIGNATURE plan** for a franchise-defining unique move (Borg "assimilate the population *instead of* killing it" is a Borg-only entry). ~90% shared catalog, ~10% bespoke where earned. Universal machinery, authored flavor — same philosophy as the ship-ladder and the traits.

**Interlock:** bounded situations (1) × generic-skeleton archetypes (4) = a small legible catalog; each chosen plan runs as gated linear phases (2); commit-and-hysteresis (3) governs when a faction abandons one plan for a more desperate one. Tunable, characterful, cannot thrash.

## A6. The full chain (with mood)

> **identity** (durable traits + authored ambition) **+ mood** (transient) → read situation as a **needs-TIER** → select the **OBJECTIVE** (scored by identity + mood) → objective sets the **DOCTRINE DIALS** (this quarter's effort split) → dials drive the **DELEGATES** (who decompose by subsidiarity and issue the orders a player would).

Mood modulates at the tier-read and the selection steps. This is the spine that turns *who a faction is* into *what it does*. **The last arrow — "delegates issue the orders a player would" — is where PART B takes over:** the settled objective and its doctrine dials hand off to the means-ends planner, which computes the actual reachable order sequence.

## A7. Design questions — RESOLVED + what's left (2026-07-10)

**RESOLVED (developer's calls):**
- ✅ **Blended tiers, not strict** — a mostly-met tier lets the next partially activate; the small weighted stack expresses it (§A2b).
- ✅ **A small weighted STACK**, not one objective — one dominant objective sets the doctrine dials, 1–2 secondaries get residual effort. At **every level**.
- ✅ **The ladder is FRACTAL / per-level** — per-planet, per-system, per-empire, read from each level's own aggregate, bubbling up from the planet (§A2a). Yes to per-system (and per-planet).
- ✅ **Each tier is a to-do list** — the advancement objectives that climb to the next tier (§A2b).

**Still open (minor):**
1. **Exact ladder granularity** — 4 tiers (Survive/Stabilize/Thrive/Ambition) as-is, or split any rung finer? *(Lean: 4 is enough; split only if a rung proves too coarse in play.)*
2. **How the authored Tier-3 ambition is STRUCTURED** — one authored "grand aim" per faction, or a weighted set it chooses among at Tier 3 (Klingon → *conquest* OR *a glorious last stand* by state)? And the fractal nuance: is the grand ambition **empire-only** (lower levels' "Tier 3" = maximally serve the empire's aim in their scope), or does each level get its own authored ambition flavor? *(Lean: grand ambition is empire-authored; a system/planet at "Ambition" pours its surplus into the empire's aim.)*

---
---

# PART B — THE MIDDLE (Means-Ends Planner)

> **"I know what I have · I know where I want to be · HOW DO I GET THERE?"**
>
> **What this is:** the design + scope map for the NPC brain's missing middle layer — the **planner** that turns a settled objective (`StrategicObjectiveDB`, built) into a *reachable* sequence of orders, walking a goal backward through its prerequisites until it hits something the faction can do this cycle. Built from a **four-agent read-only survey** (2026-07-11) that traced every objective's prerequisite chain against current-branch source, file:line. This half is the rationale; `docs/AI-BRAIN-BUILD-TRACKER.md` row 2.8 is the build authority.
>
> **As of 2026-07-11** · branch `claude/sol-playtest-earth-map-8r59j6`. Survey scope: `Pulsar4X/GameEngine/` (Industry, Colonies, Galaxy, GeoSurveys, Movement, Combat, Fleets, Ships, Logistics, Factions).
>
> **Where it plugs into PART A:** Part A's `ObjectiveSelector` writes the settled `StrategicObjectiveDB` ("where I want to be"); this planner reads it and figures out the ONE order that advances it this cycle. Part A picks the destination; Part B plots the reachable course.

## B1. The headline — it is BOUNDED, not open-ended

The Organism brain already **PERCEIVES** ("what I have" — `NeedsLadder` over the faction gauges — this is PART A's tier-read) and picks a **GOAL** ("where I want to be" — `ObjectiveSelector` — PART A's selection), then fires one primitive order. The missing middle is **planning**. The fear was that planning is an unbounded new subsystem touching everything. The survey says otherwise: **the engine already resolves the top half of every prerequisite chain, and every rung the AI must drive already has a player-equivalent order.** The planner is a *reconciliation layer at named seams*, not a general graph solver.

**The one architectural fact that bounds the whole thing:** `IndustryTools.AutoAddSubJobs` (`Industry/IndustryTools.cs:261-304`) is *already a recursive backward-chainer*. Queue a build and the engine auto-queues the refined materials it needs, and the sub-materials those need, to arbitrary depth — **for free.** It stops at exactly one place: the **mineral floor** (`IndustryTools.cs:292` — a raw mineral isn't an `IConstructableDesign`, so the recursion silently drops it). So the planner never has to reason about "to build a laser you need steel you need iron"; the engine does. The planner owns **what happens below the mineral line** (mine it / survey it / ship it) and **the gating the engine checks only at execution** (money / crew / tech / capacity).

## B2. The core verdict — DELEGATE vs BUILD

**Roughly the top half of every chain is already done. The planner owns the bottom half + the feasibility gates.**

### Delegate to existing machinery (do NOT rebuild)
| Capability | Lean on | File:line |
|---|---|---|
| component → refined-material → material resolution + auto-queue | `IndustryTools.AutoAddSubJobs` (recursion-safe, arbitrary depth) | `Industry/IndustryTools.cs:261` |
| the dependency DATA (what any buildable consumes) | uniform `IConstructableDesign.ResourceCosts` (one id→qty map for components, materials, ships) | `Engine/Interfaces/IConstructableDesign.cs:18` |
| refining recipes (which minerals a material needs) | data-driven JSON = the material's own `ResourceCosts` | `GameData/basemod/TemplateFiles/materials.json` |
| "is this available to me now vs tech-locked" | `CargoGoods` / `LockedCargoGoods` split | `Factions/FactionDataStore.cs` |
| move materials between colonies | set `LogiBaseDB.DesiredLevels`, the logistics market hauls it | `Logistics/LogisticsProcessor.cs` (`LogiBaseDB.cs:17`) |
| **the goal-selection front end** | `NeedsLadder` → `ObjectiveSelector` → `StrategicObjectiveDB` (**built-but-gated; described LIVE in survey**) | `Factions/NPCDecisionProcessor.cs:150` |
| every DRIVE rung | a player-equivalent order/API already exists for each (see per-objective maps) | — |

### Must build new (no engine support)
| Gap | Why it's net-new |
|---|---|
| **below the mineral floor** — "need mineral M" → point mining / colonize a deposit / re-target extraction | `AutoAddSubJobs` drops mineral shortfalls silently; mining is a continuous processor with no "mine THIS" decision layer |
| **plan-time feasibility oracle** — money / crew / tech / production-capacity checks | those gates fire only at *execution* inside `ConstructStuff` and fail SILENTLY (`MissingResources`), so an un-checked plan queues jobs that stall unseen |
| **reachability read** (military) — "can this fleet get to that body with its fuel/charge/route?" | grep confirms NO `CanReach`/`HasFuelToReach`/`HasDeltaVTo` anywhere; the single biggest military gap |
| **multi-jump auto-router** — string warp→jump→warp into one autonomous mission | pathfinding returns a route + cost; nothing turns it into an executable `NavSequence` for an AI |
| **fuel-readiness gate** (military) — production-built ships come out EMPTY-tanked + zero-charged | `ShipFactory.FillFuelTanks`/`ChargeReactors` are wired only to DevTools/sandbox spawns, never to `OnConstructionComplete` — an NPC fleet silently won't warp |
| **target selection** — which rival to Conquer, which body to Expand to | `StrategicObjectiveDB.TargetFactionId` is hardcoded -1; no candidate enumerator/scorer |
| **reverse indexes** — "what tech unlocks X", "what can I make from iron" | only forward maps exist (`Tech.Unlocks` is level→grants); invert them yourself |

**The biggest single trap:** conflating "`AutoAddSubJobs` resolves the tree" with "the planner can delegate prerequisite resolution." It delegates the *buildable* sub-tree ONLY. The planner's core value-add is the transition **material-demand → raw-resource acquisition** and **plan-time feasibility** — neither of which any existing machinery provides, and both of which fail *silently* today.

## B3. Per-objective scope map (the objectives are NOT uniform in cost)

### 🟢 GrowEconomy — SMALL (engine does the top half)
The full supply chain EXISTS and is AI-drivable rung by rung. What the planner adds is the reconciliation below the mineral line.

- **Build** — `IndustryTools.AddJob` / `IndustryOrder2.CreateNewJobOrder` (AI already drives this, `NPCDecisionProcessor.TryQueueEconomyJob:120`). *Cheap early win: route it through the `AutoAddSubJobs`-wrapped order — today it bypasses the free resolver.*
- **Refine** — a `ProcessedMaterial` is an `IndustryJob`; auto-resolved by `AutoAddSubJobs`.
- **Mineral shortfall** — read `IndustryJob.Status == MissingResources` (`Enums.cs:175`) + `CargoStorageDB.GetUnitsStored` + `MineralsDB.Minerals` (fog-gated `Masked<long>`). **← the seam the planner owns.**
- **Mine** — no "start mining" order; mining is emergent from installed Mine components → queue a Mine *installation* as a build (Rung 1). Read rate: `MiningDB.ActualMiningRate`.
- **Survey** (reveal the deposit) — `GeoSurveyOrder.CreateCommand` (`GeoSurveys/GeoSurveyOrder.cs:84`); read `GeoSurveyableDB.IsSurveyComplete(factionId)`.
- **Logistics** — set `LogiBaseDB.DesiredLevels` via `SetLogisticsOrder`; market hauls it.
- **Net-new:** the "mineral-line reconciliation brain" — detect stuck job → is it a mineral shortfall → is the deposit surveyed & accessible → queue mine / survey / logistics. ~3-4 slices + the AutoAddSubJobs fix.

### 🟡 Expand — MEDIUM (founding is trivial; selection is the work)
The pleasant surprise: **founding a colony is a direct, instant action — no colony ship, no landing.**

- **Found** — `CreateColonyOrder.CreateCommand(faction, species, body)` → `ColonyFactory.CreateColony` in one call (`Colonies/ColonyFactory.cs:188`). AI-drivable, instant.
- **Habitability read** — `SpeciesDB.ColonyCost(planet)` (`People/SpeciesDBExtensions.cs:30`): `-1` unsurvivable, `0` native, `>0` hostile-cost; `CanSurviveGravityOn` = hard gate.
- **Candidate enumerate** — iterate `GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()` over `FactionInfoDB.KnownSystems`; skip `IsOrHasColony()`; filter `ColonizeableDB` / `SupportsPopulations`.
- **Survey + move** — `GeoSurveyOrder` + `MoveToSystemBodyOrder` (both exist, AI-drivable).
- **Net-new:** (a) a **habitability/worth-settling SCORER** on top of `ColonyCost` (nothing ranks candidates); (b) the 3-order **chain** survey→move→found (nobody composes it); (c) an **engine-side habitability gate** (`CreateColonyOrder.IsValidCommand` returns `true` unconditionally — the only gate is client UI). Template to copy: `GameStageFactory.NextSpareBody` (dev rig — add `KnownSystems` + habitability filters). ~3-4 slices.

### 🔴 Conquer / Defend — LARGE (a genuine subsystem)
All six rungs exist and are AI-drivable, but it carries the most missing connective tissue.

- **Design/Build/Fleet/Commit** — all exist: `ShipFactory`, `IndustryTools.AddJob`, `FleetOrder.CreateFleetOrder`/`AssignShip`, `CombatEngagement.OrderAttack`/`OrderAttackNearestHostile` (`Combat/CombatEngagement.cs:492/520`), plus the auto-trigger (`BattleTriggerProcessor`) that fights hostiles in range with no order at all.
- **Reads that exist** — own strength `FactionRollup.MilitaryStrength`; rival fog-limited `ThreatAssessment.DetectedStrengthOf`; intel-sharpened `IntelAssessment.EstimatedMilitaryStrength`; rival positions via `FactionInfoDB.SensorContacts`.
- **Net-new (the mires):** ① **fuel/charge readiness** — production ships come out empty (`ShipFactory.cs:150`); must call `FillFuelTanks`+`ChargeReactors` (or wait & gauge charge) — `CombatSandbox.SpawnHostileFleet` is the working recipe to copy. ② **reachability read** — MISSING entirely. ③ **multi-jump auto-router** — MISSING (`PathfindingManager.GetPath` gives the route; nothing executes it as a mission). ④ **target selection** — `TargetFactionId` is -1. ⑤ **fleet composition** — nothing picks warship designs/counts. ⑥ **cross-system attack targeting** — `OrderAttackNearestHostile` scans only the fleet's own system. ~5-7 slices, and reachability+router may be their own mini-project (reusable foundations, like "the eyes" were).

## B4. The architecture — small resolvers on a shared substrate, driven by the loop we already built

**Not one universal graph solver. A set of per-objective backward-chaining resolvers sharing one substrate**, riding the monthly-hysteresis Tick that already exists (the same commit-and-hysteresis re-plan loop as PART A §A5a-3):

1. **State reads (what I have)** — all exist (`FactionRollup`, `CargoStorageDB`, `MiningDB`, `MineralsDB`, `ThreatAssessment`, `ColonyCost`, `IndustryJob.Status`). A thin **`FactionState` snapshot** helper gathers them once per cycle.
2. **A per-objective resolver (how do I get there)** — for the settled objective, find the **nearest unmet prerequisite** and return the ONE order that advances it. GrowEconomy resolves down the supply chain; Expand resolves survey→move→found; Conquer resolves build→fuel→move→attack.
3. **A shared feasibility oracle (can I actually)** — the plan-time money/crew/tech/capacity checks the engine only does at execution. Build once; every resolver consults it so plans don't queue silently-stalling jobs.
4. **The incremental engine is already built.** The monthly re-plan-with-hysteresis loop (`ObjectiveTransition`, 2.3) means the resolver takes **one step toward the goal per cycle** — queue the mine now, the refinery once ore flows, the build after — i.e. least-commitment planning that needs no big up-front tree solve. **This is why the planner is tractable: it never has to compute the whole plan at once.**
5. **Visibility (the Gate).** Because every failure mode here is SILENT (mineral-floor drop, execution-time stalls), build a **plan/queue readout** alongside the planner — per root `CLAUDE.md`'s Visibility Gate, a stalled-job plan is otherwise un-observable. *(This is PART A §A5a-2's "self-diagnosing stuck transition" made concrete in code.)*

Plug point: the resolvers slot into `NPCDecisionProcessor.EmitOrders` (`Factions/NPCDecisionProcessor.cs:99`) — the switch whose arms are empty stubs today. Gated behind `EnableOrderEmission` (default off) so it stays byte-identical until opted in.

## B5. The named mires (what will bog us down — now known, not discovered mid-build)

1. **The mineral floor is a SILENT drop.** `AutoAddSubJobs` looks complete but stops one layer above raw resources with no signal (`IndustryTools.cs:292`). Trusting it to "resolve everything" yields plans that omit mining and stall. **The #1 trap.**
2. **Feasibility gates are execution-time and silent.** Crew/infra/zero-cost/no-line failures all fire quietly in `ConstructStuff`. No plan-time oracle exists — build one or accept stalls.
3. **Reachability & routing are absent** (military). No `CanReach`; no multi-jump mission executor. The genuine subsystem inside Conquer.
4. **The fuel/charge trap.** Production-built ships are empty-tanked + zero-charged on purpose; warp needs stored energy. An NPC fleet silently won't move. Copy `CombatSandbox`'s fuel+charge recipe.
5. **Selection & scoring don't exist.** No target picker (Conquer), no candidate/habitability scorer (Expand), no "what to mine" prioritizer (GrowEconomy). The AI's judgment layer is net-new across all three.
6. **`ColonizeableDB`/`GeoSurveyableDB` are JSON-data markers, not computed** — procedural bodies may carry neither; verify marker coverage before an AI filters on them. Two overlapping "colonizeable" signals (`ColonizeableDB` vs `SupportsPopulations`).
7. **Dead code to avoid:** `InstallationsDB` (never attached) — read colony industry via `ComponentInstancesDB` + `MiningDB`, per root landmine L1.

## B6. Build sequence & honest slice count

Sequenced cheapest-highest-value first; each byte-identical behind `EnableOrderEmission`, each with a gauge. **~13-18 slices total**, but the first cluster delivers most of the "feels alive" value.

- **P-0 Shared substrate** (~2-3): the `FactionState` snapshot reads + the feasibility oracle skeleton + route `TryQueueEconomyJob` through `AutoAddSubJobs` (the cheap fix).
- **P-1 GrowEconomy reconciliation** (~3-4): MissingResources sensor → mineral-shortfall → queue mine / survey / logistics. *This alone makes the existing GrowEconomy honest (fixes the 2.4c blind-build shallowness).*
- **P-2 Expand** (~3-4): candidate enumerate + habitability scorer + the survey→move→found chain + the engine-side habitability gate.
- **P-3 Conquer/Defend foundations** (~5-7): fuel-readiness gate + fleet composition + target selection + **the reachability read + multi-jump router** (the reusable foundations — may be their own sub-phase). Then the emit.
- **Cross-cutting:** the plan/queue **visibility readout** (build alongside P-1, extend per phase).

**The honest bottom line for the developer:** yes, the planner is more than a byte-identical wire — it's real work. But it is **bounded (~15 slices, not "50 runs of discovery"), it invents no new orders (every rung already has a player-equivalent lever), it rides the incremental loop we already built, and its mires are now named.** And it sequences so the cheap, high-value economy planner lands first and the expensive military routing is a clearly-delineated later subsystem you can choose to fund or defer — exactly like the gate-network was deferred in Movement I.

## B7. Build Spec — the deep dive (2026-07-11, four read-only agents)

A second survey (build-spec · wiring/coexistence · testing/visibility · feasibility-oracle+mineral-floor) turned the scope map above into a **build-ready spec against a MOVING baseline** (the reactive Movement-II slices are landing in parallel). Everything below is file:line-verified on this branch.

### B7-A. The five new classes (all `Pulsar4X.Factions`, pure-helper + `internal`-for-gauge convention)

| File (`GameEngine/Factions/`) | Contents | Role |
|---|---|---|
| `FactionState.cs` | `FactionState` (the gather-once "what I have" snapshot) + `ColonyState` + `MineralShortfall` | reads every gauge ONCE per cycle so a resolver reads memory, not the entity graph. NOT a DataBlob (per-Tick scratch). Null-safe; `Snapshot(faction)` returns null on a blob-less/manager-less faction. |
| `PlannerAction.cs` | `PlannerAction { string Kind; string Detail; Action Execute }` + `.None` | the ONE step a resolver picks. The `Execute` closure unifies the two ways rungs reach the sim (`EntityCommand`→`OrderHandler.HandleOrder` for survey/move/found vs. the **direct** `IndustryTools.AddJob` for builds — a single return type can't express both). `Detail` feeds the visibility readout. |
| `IObjectiveResolver.cs` | `IObjectiveResolver { StrategicObjective Handles; PlannerAction Resolve(state, objective); }` + `ObjectiveResolvers` static registry | one small backward-chainer per objective; the registry (mirrors `ExchangeCatalog`) maps objective→resolver. |
| `GrowEconomyResolver.cs` | `GrowEconomyResolver` + `FindMineDesignFor` | the concrete P-1 resolver (algorithm below). |
| `FeasibilityOracle.cs` | `FeasibilityOracle.CanActuallyBuild(colony, design, factionInfo, out blockedReason)` | the plan-time "will this silently stall?" predicate (spec below). |

**`FactionState` read map** (the "what I have"): `Balance`/`MilitaryStrength`/`MeanMorale`/`MeanLegitimacy` ← `FactionRollup`; per-colony `Industry`(`IndustryAbilityDB`)/`Cargo`(`CargoStorageDB`)/`Mining`(`MiningDB.ActualMiningRate`)/`PlanetMinerals`(`ColonyInfoDB.PlanetEntity`→`MineralsDB.Minerals`); `StalledJobs()` = `ProductionLines[*].Jobs` where `Status==MissingResources` (`Enums.cs:175`); `MineralShortfalls()` = a stalled job's `ResourcesRequiredRemaining` (`JobBase.cs:29`) ids **absent from `IndustryDesigns`** (that absence-test IS the mineral-floor detector); `DetectedRivalStrength` ← `ThreatAssessment.DetectedStrengthOf` per rival.

### B7-B. The `EmitOrders` rewrite — REPLACE, not wrap; ONE flag

The decision half (`UpdateStrategicObjective`) is untouched. The act half becomes snapshot → registry → resolve → execute, still behind the single `EnableOrderEmission` gate:

```
internal static void EmitOrders(factionEntity, factionInfo):
    if no StrategicObjectiveDB: return
    if !ObjectiveResolvers.TryGet(objective.Objective, out resolver): return   // objectives w/o a resolver no-op
    state = FactionState.Snapshot(factionEntity);  if null: return
    action = resolver.Resolve(state, objective)     // PURE decision
    action.Execute?.Invoke()                        // the ONLY side effect
    // (a later slice records action.Detail into the plan readout — the Visibility Gate)
```

- **REPLACE the arm body per-objective, don't wrap.** A blind reactive fallback firing when the resolver says "nothing doable" re-introduces the exact silent-stall bug the planner fixes. The resolver is a strict superset of the reactive emitter (its "inputs ready → build" branch IS `TryQueueEconomyJob`, upgraded to route through `AutoAddSubJobs`), so there's no residual behaviour to fall back to. `TryQueueEconomyJob` is **superseded** by `GrowEconomyResolver`.
- **Keep ONE gate (`EnableOrderEmission`).** A second `EnablePlanner` flag makes a 2×2 dead-state matrix. Byte-identical-off holds automatically through the whole migration (flag off → `EmitOrders` never runs → arm shape irrelevant), so each per-arm replacement is independently safe and CI-gated.

### B7-C. `GrowEconomyResolver` — the backward-chain (nearest unmet prereq → ONE order)

```
Resolve(state, objective):
  A. no colony with Industry            → None
  B. for each STALLED job (MissingResources):
     B1. job has buildable sub-demands not yet queued (id in ResourcesRequiredRemaining ∈ IndustryDesigns)
                                          → QueueBuild: IndustryTools.AutoAddSubJobs(colony, job)   # the cheap fix
     B2. for each mineral shortfall (id ∉ IndustryDesigns, Missing = req − GetUnitsStored):
         → hand to the MINERAL-FLOOR BRIDGE (D): survey / mine / more-mines / logistics / escalate-to-Expand
  C. nothing stalled → start next growth build on a free line, ROUTED THROUGH AutoAddSubJobs
                       (gated by FeasibilityOracle.CanActuallyBuild)     → QueueBuild
  else                                    → None
```

One step per cycle, riding the 2.3 hysteresis loop — never computes the whole plan at once (least-commitment).

### B7-D. The two hardest pieces (deep-spec'd, file:line-anchored)

**D1 — `FeasibilityOracle.CanActuallyBuild` mirrors `IndustryTools.ConstructStuff`'s EXECUTION gates, in order (mirror, NOT a superset — a stricter check makes the AI refuse builds a player could make):**
0. **Tech** — `factionInfo.IndustryDesigns.ContainsKey(design.UniqueID)` (ConstructStuff indexes it unconditionally at `IndustryTools.cs:125` → `KeyNotFoundException` if absent).
1. **Line exists** — a `ProductionLine.IndustryTypeRates.ContainsKey(design.IndustryTypeID)` (`IndustryAbilityDB.cs:16`).
2. **Non-zero cost** — `design.ResourceCosts.Values.Sum() > 0` (ConstructStuff throws "resources can't cost 0" at `:159`).
3. **Capacity ≥ 1 pt/tick** — `(int)(rate × InfrastructureProcessor.GetEfficiency(colony)) ≥ 1` (ConstructStuff skips a job under 1 at `:133`).
4. **Crew/talent — SHIP designs ONLY** (`design is ShipDesign s && s.CrewReq>0`, guard at `:141`): `ManpowerTools.ResolveBuild(colony, CrewReq−TalentReq).CanBuild && HasTalentToBuild(colony, TalentReq)`. Inert on a pool-less host (matches execution — must replicate the guard or it false-blocks stations/installations).
5. **Materials** — replicate `AutoAddSubJobs`'s classification per cost id: satisfied if in stock (`GetUnitsStored`), else if sub-buildable (`is IConstructableDesign` → recurse, terminates at the mineral floor), else → **mineral shortfall** (the reason string carries the id+qty, the hand-off to D2).

**D2 — the mineral-floor bridge (`ResolveMineralShortfall`) — the decision tree, in order:**
1. `missingId` **not a mineral** (`CargoGoods.IsMineral` false) → defer to `AutoAddSubJobs` (engine refines it).
2. mineral on home body (`ColonyInfoDB.PlanetEntity`→`MineralsDB`), **unsurveyed** (`Amount.For(mask)==null` / `!GeoSurveyableDB.IsSurveyComplete`) → **`GeoSurveyOrder.CreateCommand`**.
3. surveyed+accessible, **no mining capacity** (`MiningDB.ActualMiningRate[mid]` absent/0) → **build a Mine** (`IndustryTools.AddJob`), itself pre-checked by D1.
4. has capacity but rate too low → **build another Mine** (raise `NumberOfMines`).
5. **not on home body but stocked/mined at a sibling colony** → **set `LogiBaseDB.DesiredLevels`** (`SetLogisticsOrder.CreateCommand_SetBaseItems`); the freight market hauls it.
6. **nowhere reachable** → **escalate to Expand** (`CreateColonyOrder` — a new colony on a body that has it).

> **⚠ The single most error-prone spot (load-bearing in BOTH D1 and D2):** resource costs are **string-keyed** (`ResourceCosts`/`ResourcesRequiredRemaining` by UniqueID) but `MineralsDB.Minerals` and `MiningDB.*MiningRate` are **int-keyed** by `Mineral.ID` (a runtime `GetEntityID()`). Convert via `factionInfo.Data.CargoGoods.GetMineral(uniqueID).ID` (exactly as `MineResourcesProcessor.cs:128` does), and check **both** `CargoGoods` and `LockedCargoGoods` (a not-yet-unlocked mineral is only in the locked library).

### B7-E. Testing + the Visibility readout (a DELIVERABLE, not an afterthought — every failure here is SILENT)

- **Pure gauges** (fast, `rest` shard): resolver returns the right rung for a hand-built state (unsurveyed→Survey, no-mine→QueueMine, sibling-stock→SetLogistics, flowing→None); oracle predicates at their boundaries; **+ a pinning cross-check** — the oracle's `MissingResources` prediction must agree with what `ConstructStuff` actually sets over an input sweep (guards against plan-time/execution drift, the `CombatKernelTests` technique).
- **Integration gauges**: reuse `CreateWithColony` + flip `IsNPC` (Path A, deterministic); create a real shortfall by draining the stockpile (`RemoveCargoByUnit`) / zeroing the deposit (`MineralsDB.Minerals[id].Amount`, internal-set) / marking it unsurveyed; call the resolver static directly (avoids hotloop-timing flake + the static-leak); assert the corrective order via the read APIs. **First triage line: assert `ActivityState != Stasis`.**
- **"Goal becomes reachable"**: assert **PROGRESS not completion** — the stuck job leaves `MissingResources` and its points burn down — and field the corrective mine deterministically via the `OnConstructionComplete`-direct workaround (dodges the known build-to-completion flakiness).
- **The Visibility readout** (Failure B — the number doesn't exist yet, so building it IS part of the planner): a small persisted record (a `PlanStateDB`, or three fields on `StrategicObjectiveDB`) carrying `BlockedOn` (structured: `Mineral(iron): deposit unsurveyed`) + `LastEmittedOrder`; exposed `SocietyReadout`-style (pure, missing-blob-tolerant string builder) with a `PlanReadoutTests` gauge and a client "Dump Plan" caller. Example line: `Directorate: obj GrowEconomy/Thrive | blocked-on iron [deposit unsurveyed] | emitted: survey Mars`.

### B7-F. The interleave sequence + FIT VERDICT

**FIT = INTERLEAVE, per-objective, along the cost gradient — NOT a strictly-after phase, and do NOT build the reactive Conquer/Defend emitter at all.** The economy planner is the *honest continuation* of the reactive work, not a bolt-on: we fit it in by making 2.4c **correct** instead of building 2.4d shallow.

Refined slice order (each byte-identical behind `EnableOrderEmission`, one CI gate each):
- **P0-a** `FactionState` snapshot (no consumer) · gauge `FactionStateTests` (hand-sum).
- **P0-b** `PlannerAction`+`IObjectiveResolver`+registry+`FeasibilityOracle` skeleton (capacity+tech) + `GrowEconomyResolver` Rung C only (queue-on-free-line **through `AutoAddSubJobs`** — the cheap fix) + rewire `EmitOrders`.
- **P1-a** `StalledJobs()`+`MineralShortfalls()` sensor.
- **P1-b** Rung B2/D2 step 3 (queue a mine) · **P1-c** logistics (D2 step 5) · **P1-d** survey (D2 step 2).
- **P1-e** oracle teeth (crew/money/capacity — D1 checks 3-5).
- **Cross** the plan/queue visibility readout.
- **P-2 Expand** and **P-3 Conquer/Defend** register the same way in their own later phases (P-3's reachability/router/fuel-readiness is the one genuine deferrable sub-subsystem; share the 2.6 eyes-wire).

Decision-side **2.5/2.6/2.7 run in full parallel** — they modulate `ObjectiveSelector`/`UpdateStrategicObjective`, invisible to the emit-side planner (2.6's eyes-read is a shared companion to the future Conquer resolver — build once, use twice).

## B8. Connections (Prime Directive)

- **Feeds IN:** the whole Organism decision loop (`StrategicObjectiveDB` from `NeedsLadder`/`ObjectiveSelector` — **this is PART A's output**); the state reads above; the engine's dependency graph (`AutoAddSubJobs`/`ResourceCosts`).
- **Feeds OUT:** player-equivalent orders (`IndustryOrder2`, `GeoSurveyOrder`, `MoveToSystemBodyOrder`, `CreateColonyOrder`, `FleetOrder`, `CombatEngagement.OrderAttack`) → the same processors that execute player orders.
- **Shares STATE:** every colony/faction gauge it reads is also written by the economy/combat/diplomacy processors — read-only from the planner's side.
- **Cradle-to-grave:** this is the AI-side mirror of the repo's own cradle-to-grave law — the NPC learns to reason along the same mineral→material→component→decision chain the design already enforces on the player.

---

*Companions: `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how" — mandate/report, identity, mood, the three-mode dial), `AI-SELF-PLAY-DESIGN.md` (the roster + parity), `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (the trait code), `AI-IMPLEMENTATION-AND-WIRING-MAP.md` (design→code bridge + socket verification), `AI-BRAIN-BUILD-TRACKER.md` (row 2.8 is the Part-B planner build authority). This doc is the "what/where + how-to-reach-it" — the destination the objective engine picks, and the plan the means-ends layer computes to reach it.*
