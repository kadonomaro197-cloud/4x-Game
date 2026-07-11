# AI Objective Engine — the DESTINATION half (what a faction wants, and how it becomes a cascade)

> **Status: v0.2 DISCUSSION DRAFT (2026-07-10).** **v0.2: the needs-hierarchy is FRACTAL** — every level (planet→system→empire) runs its own ladder read from its own aggregate, bubbling up from the planet, so levels sit at different tiers at once (§2a); each tier is also a to-do list of advancement objectives that climb to the next (§2b); blended tiers + small weighted stack confirmed at every level (§7 resolved). The **"destination"** half of the faction AI — the counterpart to `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how": delegates / mandates / traits). The developer's original framing was *"the big AI sets the destination, the small AIs decide how to get there."* We designed the small AIs in depth; **this doc designs the destination.** Built on the identity+mood model (`AI-COMMAND…DESIGN §3`) and feeds the mandate cascade (`§2`). Design decisions here are the developer's calls from the 2026-07-10 objective-system conversation.

---

## 0. Core idea — a faction's goal is neither fixed NOR free; it EMERGES

Three inputs produce a faction's *current* objective:
1. **WHO IT IS** — authored identity: the 12 traits + the faction's *designed grand ambition*. (Player authors their own faction; we author the NPCs.)
2. **HOW IT FEELS** — its transient **mood** (Wounded / Emboldened / Vengeful / Cornered …).
3. **WHERE IT STANDS** — its situation, read as a **needs-tier** (survive → stabilize → thrive → ambition).

The objective is not scripted and not a free-for-all: it *emerges* from a **needs-hierarchy filtered by identity, mood, and situation** — using the same scored-choice machinery as the trait system.

---

## 1. What a faction can want — authored ambition, universal needs (Q1)

Objectives are **NOT a flat universal menu** every faction picks from identically. Each faction is **authored** with what it ultimately wants and how much it weights each aim — the same "author the faction top-to-bottom" philosophy as its ship-ladder and its traits. **Player** designs their own faction's wants at game start; **we (designers)** author the NPCs.

**But the split is:** the *lower* tiers (survive / stabilize / thrive) are **universal** — every faction must live before it can dream — while the *top* rung (the grand ambition) is **authored per faction**. So "what a faction can want" = the universal needs-ladder, with its **summit authored per faction**.

---

## 2. The needs-hierarchy — instinct tiers (Q2, the load-bearing idea)

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

### 2a. The hierarchy is FRACTAL — every level runs it, and it starts at the PLANET (developer's call, 2026-07-10)

The needs-ladder is **not** empire-only / HoS-only. **EVERY level runs its own ladder, read from its own aggregated state:**
- A **planet** reads its own survival (besieged? starving? stable? prospering?).
- A **System Governor** reads the **aggregate of all its planets** — a system with nine healthy worlds and one under siege sits in **Thrive** even while that one world is in **Survive**.
- The **HoS** reads the **aggregate of all its systems**.

**It starts at the planet and BUBBLES UP.** The empire's tier is never *declared* — it **emerges** from the state of its parts (most planets in Survive → the empire is in Survive; core thriving while a frontier bleeds → the empire reads Thrive-with-a-wounded-edge). This is the same **subsidiarity** as decomposition (§5), applied to *state + goals*: each level assesses locally, and the assessment aggregates upward.

**Levels can sit at DIFFERENT tiers at once, with no contradiction** — each reads a different aggregate. The tutorial, exactly: Mars (empire) at **Ambition**; a besieged **planet** at **Survive**; the **System Governor** at **Stabilize/Thrive** because it averages every populated place under it. One map, three tiers, simultaneously — and that's *correct*.

> **This unifies the objective engine with the mandate/report protocol** (`AI-COMMAND…DESIGN §2`): "report up" now partly means *"here's my tier"* (a planet tells its system it's in Survive), the system aggregates, the empire reads; "mandate down" is *tier-appropriate objectives*. Destination-engine and communication-engine are the **same fractal structure** seen from two directions.

### 2b. Climbing the ladder — each tier is also a TO-DO LIST (developer's requirement)

A tier isn't just a label; **the AI must know what to DO to grow from one state to the next.** Each tier carries its **advancement objectives** — the aims whose satisfaction *is* the condition to climb:
- **Survive → Stabilize:** secure the homeworld/defense · restore food/power · stop the bleeding · end or freeze the losing war.
- **Stabilize → Thrive:** balance the budget · quell unrest · secure the borders · reach a defensive-tech floor.
- **Thrive → Ambition:** hit a strength/prosperity threshold · secure a growth base · out-tech the immediate rivals.
- **Ambition:** pursue the authored grand aim.

So the ladder is **self-propelling**: pursue your tier's advancement objectives → satisfy the tier's gauges → climb → inherit the next tier's objectives. **"How do I grow from state to state" is answered by construction — the tier IS the to-do list.** Each level runs a **small weighted stack** (mostly its current tier's objectives, a sliver of the next tier's prep), so a planet 70%-secure already spends a little on stabilizing while mostly surviving — **blended tiers, not hard switches**, at every level.

---

## 3. Readability — quantifying the situation into something a faction can READ (Q2's hard problem)

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

---

## 4. Selection — identity × mood × tier → the objective (Q2)

Each cycle (the monthly plan **+ on-event**, per the reactive spine): compute the situation gauges → determine the **dominant unmet tier** → generate the **candidate objectives** for that tier (Tier 3 draws the faction's *authored ambition*) → **score them by identity + mood** → pick. **Mood can knock the tier down** (Wounded → drop to Survive even when objectively secure) **or push over-reach** (Emboldened → chase the Tier-3 ambition before truly thriving).

---

## 5. Decomposition by SUBSIDIARITY — start at the lowest competent level (Q3)

The chosen objective becomes a cascade of sub-mandates, and **decomposition starts at the LOWEST seat that can see the whole problem**, escalating up only when needed:
- A **SYSTEM-scoped** objective ("deal with Mars," Mars is in Sol) decomposes at the **System Governor** — the local hub with the system knowledge to translate a broad want into concrete slices, farming the military slice to the System Admiral/General and the economic slice to the Planetary Governors.
- Only if **no system-authority exists**, or the objective is **EMPIRE-scoped** (spans systems), does decomposition start at the **HoS**.

This is **subsidiarity + local-knowledge:** the HoS sets the *want*; the level with the local picture does the *planning*. It makes the **System Governor the key translation seat** between empire-intent and local-action.

> **Open sub-question:** for a system-scoped *military* objective, does the System Governor coordinate it, or does the System Admiral/General decompose its own military slice? **Proposed:** the System Governor owns the system's overall *effort allocation* ("the military effort here goes to taking Mars"), and each domain seat decomposes *its own slice* (the System Admiral plans the actual fleet moves). The Governor allocates; the domain seats plan.

---

## 5a. The TRANSITION ENGINE — what the AI does to change state, and how the 19 leaders coordinate (developer's question, 2026-07-10)

This is the mechanism that turns a tier-flag into coordinated action across the seats.

### Action-plans — the catalog of ways to satisfy an advancement objective
A tier-gap (§2b) generates an **advancement objective**; that objective is satisfied by one of a **data-driven catalog of ACTION-PLANS**, each a *coordinated bundle of leader-tasks* (not a single move). The faction **scores the plans by identity + mood + situation** and picks one (its weighted stack — a primary, maybe a hedge). Example catalog for a "losing a world" (Survive) situation:

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

### Open (transition engine)
- Plan **granularity** — how coarse/fine the catalog entries are (a handful of archetypes vs. many).
- How **phase-gates** are expressed (a plan as a small ordered set of gated sub-objectives).
- **Re-plan triggers** — what report-up conditions force a re-score, and how to avoid thrashing between plans.
- **Authored vs. computed** — how much of a plan is authored per faction vs. assembled from generic leader-tasks.

---

## 6. The full chain (Q4 — with mood)

> **identity** (durable traits + authored ambition) **+ mood** (transient) → read situation as a **needs-TIER** → select the **OBJECTIVE** (scored by identity + mood) → objective sets the **DOCTRINE DIALS** (this quarter's effort split) → dials drive the **DELEGATES** (who decompose by subsidiarity and issue the orders a player would).

Mood modulates at the tier-read and the selection steps. This is the spine that turns *who a faction is* into *what it does*.

---

## 7. Design questions — RESOLVED + what's left (2026-07-10)

**RESOLVED (developer's calls):**
- ✅ **Blended tiers, not strict** — a mostly-met tier lets the next partially activate; the small weighted stack expresses it (§2b).
- ✅ **A small weighted STACK**, not one objective — one dominant objective sets the doctrine dials, 1–2 secondaries get residual effort. At **every level**.
- ✅ **The ladder is FRACTAL / per-level** — per-planet, per-system, per-empire, read from each level's own aggregate, bubbling up from the planet (§2a). Yes to per-system (and per-planet).
- ✅ **Each tier is a to-do list** — the advancement objectives that climb to the next tier (§2b).

**Still open (minor):**
1. **Exact ladder granularity** — 4 tiers (Survive/Stabilize/Thrive/Ambition) as-is, or split any rung finer? *(Lean: 4 is enough; split only if a rung proves too coarse in play.)*
2. **How the authored Tier-3 ambition is STRUCTURED** — one authored "grand aim" per faction, or a weighted set it chooses among at Tier 3 (Klingon → *conquest* OR *a glorious last stand* by state)? And the fractal nuance: is the grand ambition **empire-only** (lower levels' "Tier 3" = maximally serve the empire's aim in their scope), or does each level get its own authored ambition flavor? *(Lean: grand ambition is empire-authored; a system/planet at "Ambition" pours its surplus into the empire's aim.)*

---

*Companion: `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how" — mandate/report, identity, mood, the three-mode dial), `AI-SELF-PLAY-DESIGN.md` (the roster + parity), `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (the trait code). This doc is the "what/where" — the destination that all of those deliver.*
