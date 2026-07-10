# AI Command & Communication Design — how the levels of a faction's mind work together

> **Status: v0.2 DISCUSSION DRAFT (2026-07-10).** This is a live design conversation, not a locked plan. **Settled this pass:** identity drifts (state-vs-trait, §3e); officers are full characters that drift with their careers (§3d); subordinate-autonomy is a Head-of-State/player dial and NPCs run their delegates free (so v1 needs no approval loop). **Still open:** the trait vocabulary itself (§3a/§5), tier count for v1, and the player-facing report UI. It answers three questions the developer posed: at each level of a faction's AI, **what can it SEE, what can it REACT to, and what can it DECIDE?** — **how do the levels TALK to each other?** — and **how does all of it stay true to the faction's own identity, character, and design?** Sits on top of `AI-SELF-PLAY-DESIGN.md` (the 19-role roster + the cradle-to-grave leader pipeline) and `GOVERNANCE-AND-DELEGATION-DESIGN.md` (the delegate mechanism). Where those describe *who the seats are*, this describes *how information and intent flow between them*.

---

## 0. The one idea: mission command, not micromanagement

A faction's mind is a **chain of command**, and it runs on the same principle a warship does: **the Captain sets the mission and the standing orders; the watch officers execute it without calling the Captain for every valve lineup.** The Navy word is *mission command* (the Germans called it *Auftragstaktik*): higher command says **WHAT to achieve, with WHAT resources, under WHAT constraints** — and never *exactly how*. The subordinate owns the "how," reports status, and **escalates** only what's above their authority or ability.

That single principle answers most of the design:
- **Down the chain flows INTENT + RESOURCES + CONSTRAINTS** (call it a *mandate*). Not a task list.
- **Up the chain flows STATUS + EXCEPTIONS** (call it a *report*). Mostly "on track"; sometimes "I can't do this — advise."
- **Each level sees its own scope and thinks on its own clock.** The Captain re-plans slowly; the throttleman reacts every second. Nobody re-plans the war every tick.

This is also *why* delegation and NPC AI are the same system (the locked idea from `AI-SELF-PLAY-DESIGN.md`): the mandate/report protocol is identical whether a human or the top-level AI sits in the Captain's chair. The player, when they delegate, **becomes** the level that issues mandates and reads reports. There is no second AI to write.

---

## 1. The levels — what each one SEES, REACTS to, and DECIDES

Four tiers. The top three are decision-makers; the bottom one is mechanical (and already built). The names below are functional roles, not final titles — the full roster is in `AI-SELF-PLAY-DESIGN.md`.

### Tier 1 — Head of State (the faction's strategic mind)
*The Captain. One per faction. Thinks slowly (≈ monthly).*

- **SEES:** the whole empire in **aggregate** — total military strength, total economy/income, territory held, research standing — plus the **diplomatic picture** (who is a threat, who is a friend) and the **fog-limited view of rivals** (what our sensors and spies actually know). It does *not* see individual ships or single build queues. Strategic altitude only.
- **REACTS TO:** big shifts — war declared on us, a homeworld threatened, the economy collapsing, a rival becoming dominant, a decisive victory or loss. Rare, heavy events.
- **DECIDES:** the **grand strategy / destination** — a small set of standing **objectives** ("survive," "conquer faction X," "expand into the outer system," "win the tech race"), the **priority** among them (which personality dial is turned up *now*), and the **budget split** across departments. This is the "where are we going" the developer named.
- **DOES NOT DECIDE:** which ship to build, which system to defend, when a fleet engages. Those belong below.

### Tier 2 — Cabinet delegates (department heads, empire-wide)
*The XO and the heads of department. A handful per faction: Grand Admiral (all military), Interior/Chancellor (all economy & production), Foreign Minister (diplomacy), Spymaster (intel), Research Director (tech). Thinks at a middle clock (≈ weekly).*

- **SEES:** everything inside **its own domain, empire-wide.** The Grand Admiral sees every fleet, every detected threat, all military production. The Interior Minister sees every colony, all industry, every stockpile. Across domains it sees only **summaries** (the Admiral knows its budget, not the tax code).
- **REACTS TO:** domain events — a fleet destroyed, a colony lost, a new enemy fleet detected, a key tech finished, a treaty broken.
- **DECIDES:** **how to achieve the mandate within the domain** — the *plan*. The Admiral decides which systems to defend vs. attack, the ship mix to build, where to mass the fleet. The Interior Minister decides which colonies build what, the tax rate, where to expand. This is "the way we get there."
- **DOES NOT DECIDE:** the empire's objective (that's handed down) or the tick-by-tick execution (that's handed further down).

### Tier 3 — Operational delegates (regional / tactical)
*The watch officers. Many per faction: System Admiral (one system's fleet), System/Planetary Governor (one system/planet's economy), field commanders. Thinks fast (per-event, near-real-time).*

- **SEES:** its **local scope only** — one system, one planet, one theatre. The System Admiral sees the ships and contacts in its system.
- **REACTS TO:** immediate events — enemy in range, colony under attack, resource shortage, an order arriving from above.
- **DECIDES:** **execution** — move this fleet here, engage now or hold, queue these specific builds, set this EMCON posture, retreat or stand. It **issues the actual orders** through the "hands" (the order system, which is fully built and callable headless — see §4).
- **DOES NOT DECIDE:** anything outside its theatre. It escalates cross-theatre needs upward.

### Tier 4 — Autonomous execution (already built)
*The equipment. Individual ships and units.* Follows orders mechanically — the combat resolver picks targets by the weapon triangle, movement follows the nav order. **No decisions of consequence.** This tier exists and works today; the AI project does not touch it.

> **Cadence pyramid (the load-bearing performance idea):** Tier 1 rarely re-plans (monthly), Tier 2 adjusts weekly, Tier 3 reacts per-event, Tier 4 every tick. Big thinking is rare and cheap; only the cheap reactions are frequent. This is what keeps a galaxy of AI factions affordable — and it maps onto watch rotations the developer already knows.

---

## 2. How the levels COMMUNICATE

Two channels, and only two. Keeping it to two is what stops this becoming an unmaintainable tangle.

### 2a. DOWN = the MANDATE (intent + resources + constraints)
When a higher level directs a lower one, it hands down a small, uniform bundle — **the same shape at every boundary**:

| Field | What it is | Example (Head of State → Grand Admiral) |
|---|---|---|
| **Objective** | the aim, as a target + verb | "Break faction *Mars* — reduce it to zero colonies" |
| **Priority** | how much this matters vs. other mandates | Max (war footing) |
| **Resource share** | the budget/forces allocated | 60% of military spending; the 2nd Fleet |
| **Stance / principles** | *how* to pursue it — the posture card | "Offensive; accept heavy losses; no civilian targets" |
| **Constraints** | hard limits | "Do not commit the home guard; hold Luna" |

The subordinate takes that mandate and **owns everything below it.** The Grand Admiral turns "break Mars, offensive, 60%" into "build 8 cruisers at Earth, mass at L4, System Admiral Sol takes Mars orbit." The Head of State never sees those details — it only set the destination and the rules.

> **The stance is the heart of it.** A *stance* is a named bundle of standing orders — a thermostat setting, not a joystick. "Offensive Push," "Dig In," "Defensive Line." It's the single thing that carries *principles* down the chain, and (per `AI-SELF-PLAY-DESIGN.md`) it's a **data-driven, moddable preset**, biased for NPCs by the faction's personality. Same code, different stance → different behavior.

### 2b. UP = the REPORT (status + exceptions/escalation)
On its own clock, each subordinate reports up — again a uniform small shape:

| Field | What it is | Example (Grand Admiral → Head of State) |
|---|---|---|
| **Status** | on-track / at-risk / failed | "At risk" |
| **Progress** | toward the mandate | "Fleet 70% built; Mars fleet stronger than estimated" |
| **Exception / ask** | what I can't handle alone | "I cannot both hold Luna and take Mars with current forces — need more yards, or drop one" |

**Escalation is the reactive spine.** A local problem the operational level can't solve (System Admiral is losing) bubbles up as an exception, which can make the cabinet re-plan, which can make the Head of State reconsider the whole objective. This is how a lost battle at Mars can, three reports later, flip the faction from "conquer" to "sue for peace." **The AI reacts to the world by reports flowing up, not by every level re-scanning everything.**

> **Where this lives in code (the thin, buildable version):** the mandate is a small record the higher level **writes onto the faction** (or onto the seat), and the lower level **reads** each cycle. The report is a small record the lower level writes and the higher level reads. No message bus, no new event system required for v1 — it's two data slots per boundary, read on each level's clock. The order system (`Game.OrderHandler.HandleOrder`) is only touched at Tier 3, where mandates finally become real game actions.

---

## 3. Staying true to the faction's IDENTITY, character, and design

This is the part that makes factions *feel* like themselves instead of like the same robot in different paint. The rule:

> **Identity is a small set of parameters carried on the faction, and it TILTS every scored decision at every level — but it expresses differently at each level. You never write a "Belter AI" and an "Earther AI"; you write ONE AI whose choices are weighted by the faction's identity data.** (This is the "no special AI code path" principle applied to *character*.)

### 3a. What "identity" is made of
Three layers, all data on the faction — and it's important they're **distinct**, not redundant:

1. **The four priority dials** (`DoctrineVector`, already exists): Economic / Military / Tech / Expansion. These are the **current allocation of effort** — an *output* the Head of State sets and re-sets ("this quarter we pour into military"). Dynamic; they move with strategy. *What the faction is doing right now.*
2. **Character traits** (proposed new, small set of 0–1 dials) — the enduring **temperament** that biases how it sets those dials and how it makes *every* scored choice. Durable; drifts only slowly (see §3e). *Who the faction is.* A high-Aggression faction tends to run a high Military dial — but a mauled high-Aggression faction may temporarily run a high Economic dial to rebuild **while staying high-Aggression.** Trait = cause, dial = current setting; that's why they're separate.

   **Proposed 6-trait vocabulary (v0.2 — stress-tested against the developer's own examples; still open):**
   - **Aggression** — reaches for *force* vs. other means.
   - **Ambition** — content within its borders vs. an insatiable drive to grow/dominate (any pillar, not just territory).
   - **Risk tolerance** — bold/gambles vs. cautious/methodical.
   - **Honor** — keeps its *word* (treaties, deals) vs. betrays when convenient. *(The diplomacy-reliability axis.)*
   - **Xenophobia** — cooperative/curious about others vs. others-are-enemies-or-resources.
   - **Ruthlessness** — restrained in *methods* vs. will-cross-any-line (atrocities, civilian bombardment, WMDs). *(A separate axis from Honor: Honor is about promises, Ruthlessness is about methods — a faction can keep every treaty yet glass a planet it's openly at war with, or betray freely yet never commit an atrocity.)*
   - *(candidate 7th: **Curiosity** — the tech/explore temperament: incurious vs. driven to understand/acquire knowledge.)*

   **Why Honor and Ruthlessness are both load-bearing (the Klingon-vs-Borg test):** both are high-Aggression, but a Klingon is **high Honor / low Ruthlessness** (keeps its word, seeks *glorious* battle, spares civilians) while a Borg/Zerg/Tyranid/Flood is **no Honor / max Ruthlessness** (no deals exist, consume everything). Those two axes are exactly what separate an honorable warrior race from a genocidal swarm — drop either and the two collapse into "aggressive."
3. **Government type** (already exists, `GovernmentDB`): the **modulator** — not a bias but a *rule override*. A war-state can build understaffed; a democracy has morale ceilings on aggression. *What the faction is allowed / structurally driven to do.*

Together: dials (current priorities) + traits (temperament) + government (rules) = the faction's identity. **These are the ONLY things that differ between two factions' AIs** — plus their situation, and the traits + competence of the individual officers they happen to have (§3d).

> **Personality ≠ mechanics (the Zerg/Borg/Replicator/Flood boundary).** The trait cluster gets a hive/assimilator race ~80% of the way — they all sit near each other (Aggression↑ Ambition-max Xenophobia-max Ruthlessness-max Honor-none), which is *correct*: they *are* close. What ultimately separates a Zerg from a Borg from a Replicator is their **unique growth/consumption MECHANIC** (evolve-biomass vs. assimilate-tech vs. self-replicate vs. infect), which is a **separate special-rules layer**, not a personality trait. Do not try to encode "assimilates tech" as a trait — traits are temperament; mechanics are a different system. This doc covers the temperament; the unique-mechanic layer is its own future design.

### 3b. How identity expresses at each level
The same identity data reads out differently by altitude:
- **Tier 1 (Head of State):** identity picks **which objective.** High Aggression + high Ambition → "conquer." High Tech + low Aggression → "win the tech race, avoid war." Xenophobia → "isolate, fortify." This is where character matters most.
- **Tier 2 (Cabinet):** identity picks **which stance/doctrine.** Given "defend the border," an aggressive faction's Admiral picks a forward, offensive screen; a cautious one digs in. Same mandate, different plan.
- **Tier 3 (Operational):** identity **colors tactical choices.** A proud/honorable faction won't abandon a colony or flee a fight; a pragmatic one retreats to fight later. Least room here — it's execution — but stance still tints it.

### 3c. The mechanism: identity as WEIGHTS on scored choices
Every AI decision is a **scored choice** — list the options, score each, pick the best (or roll weighted). Identity multiplies into those scores. "Attack vs. defend" scored for an aggressive faction weights *attack* up; for a cautious one, *defend*. Because it's one scoring function with faction-supplied weights, **you get distinct-feeling factions for free**, and a modder can author a new faction's soul as a handful of numbers.

### 3d. Officers are a SECOND personality — competence *and* character (DECIDED 2026-07-10)
An officer is not just a competence number. **Each officer carries their own trait set (same §3a vocabulary) AND a competence.** When a seat makes a scored choice, the score is weighted by **both** the faction's identity **and** the seated officer's traits, blended. So:
- **Competence** sets the **quality of execution** (a brilliant admiral masses and strikes cleanly; a poor one commits piecemeal). Faction identity says *what it wants and how*; competence says *how well it's pulled off*.
- **Officer traits** tilt the *decisions themselves*, and can **contradict the role or the faction** — the productive friction the developer asked for. Your best-trained, fully-specialized diplomat can be personally **xenophobic**: mechanically excellent at negotiation, but every call he owns tilts toward distrust and hard terms. A cautious admiral in an aggressive faction drags the fleet toward defensive postures. This friction is a *feature* — a source of emergent story, and a reason to choose who sits where (use the xenophobe as a hawk, or replace him).

**Officer traits drift with their career (DECIDED 2026-07-10) — and it must be logical for what they did.** Over time an officer gains new traits, deepens existing ones, or loses them, driven by their assignment history: an admiral who fights war after war hardens (+Aggression, +Ruthlessness, −caution); a diplomat who's been betrayed sours (+Xenophobia, −Honor-in-others' eyes); a governor who crushes a rebellion turns authoritarian. Same machine as faction drift (§3e): an *experience* applies a *pressure*, and the officer's current character shapes the actual movement. This is the officer half of the cradle-to-grave pipeline — and it's why losing a seasoned, well-shaped officer *hurts* (the grave rung).

> This means officer traits use the **same 0–1 vocabulary as factions** — one trait system, two owners (a faction, a person). A seat's effective "personality" for scoring = a blend of faction identity + the officer's own traits (blend weights TBD — e.g. faction sets the baseline, officer nudges it).

---

### 3e. Drift — identity CHANGES with events, but stays in character (DECIDED 2026-07-10)
Identity is **not** fixed at scenario authoring. Events reshape it — losing a war *does* make Mars vengeful. Two mechanisms, on two timescales, keep this from turning every faction into mush:

**Two timescales — STATE (mood) vs. TRAIT (character):**
- **State / mood** is fast and transient — a posture the faction snaps into and out of: *Wounded / Rebuilding / Emboldened / Vengeful / Cornered*. A single lost war drops a faction into a Wounded state.
- **Trait / character** is slow and durable — the §3a dials. A trait only shifts after **sustained, era-defining** experience (many wars, a generational trauma), not one event.

**This solves the Klingon puzzle** ("how do I make a warrior race less inclined to fight after a loss without making them un-warrior?"): you **don't** lower their Aggression trait. A defeat drops the Klingons into a **Wounded/Rebuilding STATE** that temporarily suppresses *starting new wars* — for their own survival, so they don't suicide — while their Aggression and Honor traits stay fully intact and they actually gain **+Vengeance** (a mood). They pause, rebuild, and come back *angrier*. Self-preservation without betraying character. Only if defeats pile up across an era would the durable traits themselves begin to erode.

**Drift is FILTERED THROUGH the current identity — the same event pushes different factions opposite directions.** An event doesn't apply a universal rule ("defeat → +caution"); it applies a *pressure*, and the faction's existing character decides the actual movement:
- A **proud/honorable** faction (Klingon): defeat → +Vengeance, hold Aggression, rebuild for round two.
- A **pragmatic trader**: defeat → +Caution, −Aggression, sue for peace, pivot to economy.
- A **ruthless swarm**: defeat → regroup and come again, unchanged — it has no other mode.

Mechanically this is the *same scoring machine* as every other decision: the event proposes trait/state pressures, and the current weights shape how much (and which way) each actually moves. One system, reused. **(This applies identically to officer traits — §3d — an officer's career experiences drift *their* traits through *their* current character.)**

---

## 4. What we're building on (so the design is grounded, not wishful)

From the five-agent survey (2026-07-10):
- **The "hands" are DONE.** One clean order window, `Game.OrderHandler.HandleOrder(...)`, zero UI coupling, already driven from the background sim thread. Nearly every player action — build, form fleets, move, attack, research, colonize, survey, diplomacy — is callable by a background AI through the same code the player's buttons use. Tier 3 has everything it needs to *act*.
- **The "eyes" are HALF-WIRED.** Fog-correct perception exists (money, sensor contacts, diplomacy, own colonies, per-fleet strength). Missing: a **faction-level aggregate dashboard** (most totals must be summed over colonies/fleets — cheap wiring) and two real builds — **own total military strength as one number**, and **enemy strength / target value / target defense estimates** (fog hides enemy combat values). Tiers 1–2 need these aggregates to see at their altitude.
- **The "brain," the "plan slot," and "delegate behavior" are the real build.** The clock fires (`NPCDecisionProcessor`, monthly, per NPC), but its decision body is an empty `// TODO`. There is **no goal/objective slot** on a faction and **no code that reads a seat and makes its officer act.** The delegate chassis is furniture (officers + chairs exist; nothing consumes the assignment); the class the docs named as the delegate record (`AdministratorDB`) is dead code — the working competence/funding pattern to copy lives in the research system (`ResearcherDB`/`ResearchProcessor`).

**Net:** the mandate/report protocol above is buildable now — it's small data slots + scoring functions on top of a finished order system and a mostly-finished perception layer.

---

## 5. Decisions — settled and still-open

**SETTLED (2026-07-10 discussion):**
- ✅ **Identity DRIFTS with events** (not fixed) — via the two-timescale **state (mood) vs. trait (character)** model, with drift **filtered through current identity** so the same event pushes different factions different ways. Solves the Klingon-loss puzzle. See §3e.
- ✅ **Officers are full characters, not just competence numbers** — own trait set (same vocabulary), can contradict role/faction (the xenophobic master diplomat = friction-as-story), and **drift with their career** (logically tied to what they did). See §3d.
- ✅ **Subordinate autonomy is set at the Head-of-State level, and is primarily a PLAYER feature.** The player decides how much rope each delegate gets ("execute freely / hold and report / ask first"). For **NPC** factions the Head-of-State AI runs on **auto** and simply lets its delegates run — **so v1 needs no escalation-approval loop**; the approval UI is a later, player-side layer. (Big v1 scoping win: NPC delegates run free.)

**STILL OPEN:**
1. **How many tiers for v1?** Full model is 4; for the "Mars attacks Earth" proof, collapse to **2** (Head of State sets objective → one military delegate executes), grow later. *(Recommendation: collapse first.)*
2. **The trait vocabulary itself (§3a).** Proposed 6: **Aggression / Ambition / Risk / Honor / Xenophobia / Ruthlessness** (+ candidate **Curiosity**). Stress-tested against the hive cluster, Klingon-vs-Borg, and the xenophobic diplomat — but not locked. Open sub-questions: is 6 (or 7) the right count; do we want to instead map onto a known system (Stellaris ethics / explicit franchise archetypes); and what are the **blend weights** when a faction's identity meets a seated officer's own traits (§3d).
3. **Does the PLAYER see the same reports/escalations an NPC Head of State would?** This is the payoff of "one system" — build the player-facing report/mandate UI as the *same* thing, not a bolt-on. *(Recommendation: yes — but it's a later slice, after the NPC loop works.)*

---

## 6. First build slice (once the above is settled)

Proposed vertical slice that proves the whole protocol with visible behavior (the "Mars attacks Earth" test):
1. **Add the goal slot** — a small `StrategicObjectiveDB` (or field on `FactionInfoDB`) holding {objective kind, target faction/body, priority}. The mandate, in data.
2. **Fill the Head-of-State stub** — `NPCDecisionProcessor` reads the personality dials + the aggregate "eyes," and writes an objective ("Military-heavy + a reachable enemy → conquer it").
3. **Build ONE military delegate** (a plain function for v1, reshaped into an officer-in-a-seat later) — reads the objective, and through the finished hands: queues warships (`IndustryOrder2`), forms them into a fleet (`FleetOrder`), sends them at the target (`WarpMoveCommand` + `CombatEngagement.OrderAttack`).
4. **Wire the two missing gauges** it needs — own fleet-strength rollup, rough enemy-strength read.
5. **Watch it in the Earth-vs-Mars scenario** — Mars should build, mass, and come for Earth. That visible loop is the gauge that the AI "works."

The goal slot and the orders it emits carry forward unchanged when we later formalize delegates into the officer/seat/stance model — nothing in this slice is throwaway.

---

*Companion docs: `AI-SELF-PLAY-DESIGN.md` (the roster + the leader pipeline), `GOVERNANCE-AND-DELEGATION-DESIGN.md` (the delegate mechanism), `GOVERNMENT-AND-POLITICS-DESIGN.md` (government-as-modulator), `DIPLOMACY-DESIGN.md` (Foreign-Minister external politics). This doc is the information/communication layer that ties the levels together.*
