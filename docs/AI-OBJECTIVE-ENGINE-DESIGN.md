# AI Objective Engine — the DESTINATION half (what a faction wants, and how it becomes a cascade)

> **Status: v0.1 DISCUSSION DRAFT (2026-07-10).** The **"destination"** half of the faction AI — the counterpart to `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how": delegates / mandates / traits). The developer's original framing was *"the big AI sets the destination, the small AIs decide how to get there."* We designed the small AIs in depth; **this doc designs the destination.** Built on the identity+mood model (`AI-COMMAND…DESIGN §3`) and feeds the mandate cascade (`§2`). Design decisions here are the developer's calls from the 2026-07-10 objective-system conversation.

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

## 6. The full chain (Q4 — with mood)

> **identity** (durable traits + authored ambition) **+ mood** (transient) → read situation as a **needs-TIER** → select the **OBJECTIVE** (scored by identity + mood) → objective sets the **DOCTRINE DIALS** (this quarter's effort split) → dials drive the **DELEGATES** (who decompose by subsidiarity and issue the orders a player would).

Mood modulates at the tier-read and the selection steps. This is the spine that turns *who a faction is* into *what it does*.

---

## 7. Open design questions (the next rung)

1. **The exact tier ladder** — is it 4 (Survive / Stabilize / Thrive / Ambition), or finer (e.g. split Survive into "not-die" vs. "secure-the-core")?
2. **Strict vs. blended tiers** — must a lower need be *fully* met before a higher aim activates, or does a mostly-met tier let the next partially activate (a faction 70% secure starts expanding while still shoring up)?
3. **One objective or a weighted STACK** — does a faction pursue a single objective, or a primary + secondaries running in parallel?
4. **How the authored Tier-3 ambition is STRUCTURED** — one authored "grand aim" per faction, or a weighted set the faction chooses among at Tier 3 (e.g. a Klingon could aim at *conquest* OR *a glorious last stand* depending on state)?
5. **Does the needs-tier apply per-SYSTEM too** — a frontier system in Survive while the core Thrives — or only empire-wide?

---

*Companion: `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the "how" — mandate/report, identity, mood, the three-mode dial), `AI-SELF-PLAY-DESIGN.md` (the roster + parity), `AI-PERSONALITY-IMPLEMENTATION-SPEC.md` (the trait code). This doc is the "what/where" — the destination that all of those deliver.*
