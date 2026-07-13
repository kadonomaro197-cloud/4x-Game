# AI Command & Communication Design — how the levels of a faction's mind work together

> **Status: v0.3 DISCUSSION DRAFT (2026-07-10).** This is a live design conversation, not a locked plan. **Settled:** identity drifts (state-vs-trait, §3e); officers are full characters that drift with their careers, faction outweighs officer by default but tenure/experience can override (§3d); subordinate-autonomy is a Head-of-State/player dial and NPCs run their delegates free (so v1 needs no approval loop). **Expanded this pass:** the trait vocabulary grew from 6 to a **12-trait model** via a four-agent cross-franchise survey (§3a) — added Zealotry, Guile, Collectivism, Authoritarianism, Altruism, each tied to a named gameplay lever + a live-when-wired rule; deferred Patience/Adaptability/Venality/Isolationism; cut grudge-holding to the relationship track; added the faction-fingerprint gauge (§3a-bis). **Still open:** final ratify of the 12 (esp. the deferred four), tier count for v1, the player-facing report UI. It answers three questions the developer posed: at each level of a faction's AI, **what can it SEE, what can it REACT to, and what can it DECIDE?** — **how do the levels TALK to each other?** — and **how does all of it stay true to the faction's own identity, character, and design?** Sits on top of `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the 19-role roster + the cradle-to-grave leader pipeline) and `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the delegate mechanism). Where those describe *who the seats are*, this describes *how information and intent flow between them*.

---

## 0. The one idea: mission command, not micromanagement

A faction's mind is a **chain of command**, and it runs on the same principle a warship does: **the Captain sets the mission and the standing orders; the watch officers execute it without calling the Captain for every valve lineup.** The Navy word is *mission command* (the Germans called it *Auftragstaktik*): higher command says **WHAT to achieve, with WHAT resources, under WHAT constraints** — and never *exactly how*. The subordinate owns the "how," reports status, and **escalates** only what's above their authority or ability.

That single principle answers most of the design:
- **Down the chain flows INTENT + RESOURCES + CONSTRAINTS** (call it a *mandate*). Not a task list.
- **Up the chain flows STATUS + EXCEPTIONS** (call it a *report*). Mostly "on track"; sometimes "I can't do this — advise."
- **Each level sees its own scope and thinks on its own clock.** The Captain re-plans slowly; the throttleman reacts every second. Nobody re-plans the war every tick.

This is also *why* delegation and NPC AI are the same system (the locked idea from `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`): the mandate/report protocol is identical whether a human or the top-level AI sits in the Captain's chair. The player, when they delegate, **becomes** the level that issues mandates and reads reports. There is no second AI to write.

---

## 1. The levels — what each one SEES, REACTS to, and DECIDES

Four tiers. The top three are decision-makers; the bottom one is mechanical (and already built). The names below are functional roles, not final titles — the full roster is in `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`.

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

> **The stance is the heart of it.** A *stance* is a named bundle of standing orders — a thermostat setting, not a joystick. "Offensive Push," "Dig In," "Defensive Line." It's the single thing that carries *principles* down the chain, and (per `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`) it's a **data-driven, moddable preset**, biased for NPCs by the faction's personality. Same code, different stance → different behavior.

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

   **The trait vocabulary (v0.3 — expanded by a four-agent cross-franchise survey, 2026-07-10: The Expanse/BSG/B5/Dune/Culture · Trek/Wars/Mass Effect/40k · the Stellaris ethics system + Stargate/Halo/StarCraft/4X-game AI systems · an adversarial hive-mind/AI/utopia axis-hunt). Twelve traits, in two groups. Each is a 0–1 dial, and — the developer's iron rule — each names the concrete decision lever it moves. A dial with no lever does not exist. "Nothing pretty for pretty's sake."**

   *Group 1 — temperament (the original six + Curiosity):*
   - **Aggression** — reaches for *force* vs. other means. → *lever:* attack-vs-defend; military share of the build queue.
   - **Ambition** — content vs. an insatiable drive to grow/dominate (any pillar). → *lever:* expansion pace; grand-objective choice.
   - **Risk tolerance** — bold/gambles vs. cautious/methodical. → *lever:* engage-vs-wait; commit vs. hold reserves; accept a fight below overwhelming force.
   - **Honor** — keeps its *word* (treaties) vs. betrays when convenient. → *lever:* honor-vs-break a deal; renege on a pact when it pays.
   - **Xenophobia** — cooperative toward others vs. others-are-enemies-or-resources. → *lever:* first-contact hail-vs-shoot; ally-vs-shun; accept vs. purge alien pops.
   - **Ruthlessness** — restrained in *methods* vs. crosses-any-line. → *lever:* atrocities / civilian bombardment / WMD use against **enemies**.
   - **Curiosity** *(narrowed from the first draft)* — will spend to investigate the unknown vs. incurious. → *lever:* survey/explore budget; investigate-vs-ignore-vs-destroy an anomaly; hail-to-learn. *(Narrowed: the "curious about others" sense is Xenophobia's job; Curiosity is strictly the go-look-in-the-dark drive — kept only because the north-star vision funds exploration/first-contact, which is its lever.)*

   **Why Honor and Ruthlessness are both load-bearing (the Klingon-vs-Borg test):** both are high-Aggression, but a Klingon is **high Honor / low Ruthlessness** (keeps its word, seeks *glorious* battle, spares civilians) while a Borg/Zerg/Tyranid/Flood is **no Honor / max Ruthlessness** (no deals, consume everything). Drop either and the honorable warrior and the genocidal swarm collapse into "aggressive."

   *Group 2 — the five the franchise survey added (each fills a gap the six provably could not — the named faction is one the six render as bland/wrong):*
   - **Zealotry / Dogmatism** *(proposed independently by ALL FOUR agents — the clearest buy)* — subordinates strategy to a creed vs. pure pragmatism. → *lever:* **the only trait that makes the AI knowingly leave value on the table** — refuse a materially-good treaty on principle; ban an entire tech branch as *taboo* (refusing a wanted payoff, which Curiosity/Risk can't produce); refuse to surrender/sue-for-peace when losing; generate a holy-war casus belli. *(The Minbari surrendering at the brink of victory; the Imperium refusing forbidden tech while being ground down for lack of it.)*
   - **Guile / Subtlety** — pursues goals overtly (declared force) vs. covertly (spies/sabotage/proxies). → *lever:* espionage-budget-vs-fleet-budget; EMCON default (dark vs. loud); proxy/foment-unrest vs. direct war; infiltrate-vs-hail on contact. *(Separates the Romulan spymaster from the Klingon duelist — both "non-force-first," play nothing alike. NOT Honor: you can be covert yet keep your word — Salarian STG — or overt yet treacherous — Orks.)*
   - **Collectivism / Cohesion** — its own people/units are expendable cells vs. individuals that matter. → *lever:* retreat/rout threshold + acceptable-losses (does the morale system even apply?), **and the mirror** — sow-unrest / incite-defection ops return near-zero against a high-Cohesion faction, so the AI neither wastes them attacking nor spends defending against them. *(The hive axis: separates the Borg — never routs, morale-proof — from the Klingons, who spend losses for glory but can be demoralized and split. NOT Risk: a cautious hive still never breaks once committed.)*
   - **Authoritarianism** *(from mining Stellaris's Authoritarian↔Egalitarian — and the one whose levers are ALREADY BUILT)* — coerces its OWN population vs. governs by consent. **Orthogonal to Ruthlessness** (that's cruelty to *enemies*; this is control of *your own side*). → *lever:* the slave/forced-labor economy bonus (accept lower happiness for output); and the unrest response — **suppress-by-force vs. cut-taxes/grant-autonomy** — the exact fork already living in the built morale / legitimacy / rebellion / tax systems.
   - **Altruism** — spends its own resources at *net loss* for others vs. self-only. → *lever:* proactive foreign aid / uplift / defend-a-weaker-party at cost to self. One notch past Xenophobia's cooperative pole: cooperation is *mutual gain*, altruism is *accepted loss* (Ferengi trade with anyone but never at a loss — proving it's a separate axis). *(Resolves the Culture, invisible on the original six. Note "reshape others" is NOT one axis but two: benevolent meddling = Altruism + Guile; coercive "convert or die" = Zealotry + Aggression.)*

   > **→ The code-ready audit of all this lives in `docs/ai/AI-PERSONALITY-IMPLEMENTATION-SPEC.md`** (five-agent source audit, 2026-07-10): per-trait wiring points with file:line, the `PersonalityDB` data model, the scoring/drift/blend math, and the demonstration tests. Audit result: **9 of 12 demonstrable now, 3 sequenced behind their system (Guile→espionage, Altruism→commitment, Curiosity→exploration), 1 facet cut (Ruthlessness internal-purge).** Read it when it's time to code.
   >
   > **The live-when-wired rule (this is how we keep the developer's promise).** A trait is *defined* in the data the moment we agree it's real — but it only goes *live* when its lever is built, and we build the trait's effect **in the same slice as the lever**, never a dial with nothing behind it. Most-built levers today (v1-ready): Aggression, Risk, Ruthlessness (colony bombardment is wired), Zealotry (refuse-treaty + won't-retreat + don't-queue-taboo-tech all ride built paths), Collectivism (combat retreat + the morale system are built), Authoritarianism (morale/tax/legitimacy/rebellion are built), Ambition, Honor (diplomacy calls are built). Awaiting their system (define now, wire when the system lands): Guile (espionage engine), Altruism (diplomacy aid/commitment), Curiosity (exploration content), the deep half of Xenophobia (first-contact-as-event). A defined-but-dormant trait is explicitly *not* "pretty for pretty's sake" — it's a labeled socket waiting for its plug.
3. **Government type** (already exists, `GovernmentDB`): the **modulator** — not a bias but a *rule override*. A war-state can build understaffed; a democracy has morale ceilings on aggression. *What the faction is allowed / structurally driven to do.*

Together: dials (current priorities) + traits (temperament) + government (rules) = the faction's identity. **These are the ONLY things that differ between two factions' AIs** — plus their situation, and the traits + competence of the individual officers they happen to have (§3d).

> **Personality ≠ mechanics (the Zerg/Borg/Replicator/Flood boundary).** The trait cluster gets a hive/assimilator race ~80% of the way — they all sit near each other (Aggression↑ Ambition-max Xenophobia-max Ruthlessness-max Honor-none), which is *correct*: they *are* close. What ultimately separates a Zerg from a Borg from a Replicator is their **unique growth/consumption MECHANIC** (evolve-biomass vs. assimilate-tech vs. self-replicate vs. infect), which is a **separate special-rules layer**, not a personality trait. Do not try to encode "assimilates tech" as a trait — traits are temperament; mechanics are a different system. This doc covers the temperament; the unique-mechanic layer is its own future design.

**Deferred candidates (real, but held out of the core 12 — adopt when their lever exists / the count earns it):**
- **Patience / tempo** (act on a 1000-year plan vs. grab-it-now) — the survey split 2–2. The concept (temporal discount rate, distinct from Risk's *variance*) is real, but most of its levers reconstruct from Ambition + Risk + research, and the sim has little long-term *compounding* yet for it to bite. **Revisit once the economy has real long-game depth** (then "cede ground now for position later" becomes a lever nothing else moves — Bene Gesserit / Necrons).
- **Adaptability** (copy the enemy's tech/doctrine vs. fixed dogma) — its doctrine-switch lever is largely the *inverse* of Zealotry (a dogmatic faction won't adopt enemy doctrine — already Zealotry's lever), and its unique half ("reverse-engineer captured tech") has no built mechanic yet. **Fold into Zealotry for now; split out if a capture-and-copy-tech mechanic is ever built.**
- **Venality** (can be bribed into/out of a war/alliance/access) — a genuinely distinct "is this AI for sale?" lever (Ferengi/Hutts), but it needs the diplomacy **exchange engine to support stance-flipping deals with resource riders**. **Adopt when that lands; until then it's Ambition-economic.**
- **Isolationism** (engages the galaxy at all — Inward-Perfection/Asgard) — ~60% predicted by Xenophobia + Ambition; the clean residue is "joins multilateral blocs vs. lone-wolf." **Fold trade/borders into Xenophobia; split a lean "Alliance-seeking" dial if federation-style diplomacy earns depth.**

**Cut (belongs in another system, not a trait):**
- **Vengefulness / Pride / grudge-holding** *(all four agents agree)* — a grudge is **per-target, stateful, and decays** = the definition of the existing per-rival relationship/mood track (`DiplomacyDB`), not a fixed disposition. Model it as **Honor modulating how fast grudges form and fade** on that track. A standalone Pride dial would just multiply a system that already exists.
- **Commerce/materialism, Bellicosity, Victory-vector** — each reconstructs from existing traits (Ambition-econ + low Altruism; Aggression-max + Honor-min; a product of Aggression/Ambition/Curiosity). No unique lever.

**One reconciliation flag to resolve at build time:** **Authoritarianism (trait) vs. `GovernmentDB` (the government-type modulator).** Both touch how a faction governs itself. The clean split is: the **trait** is the faction's *temperament* (how much it *wants* control), the **government type** is the *structure/rules* it operates under — a temperamentally-authoritarian faction under a democratic structure is a real, interesting tension (drift toward autocracy). But we must make sure the trait's levers (slave-labor bonus, unrest-suppression) don't double-count the government dials that already exist. **Check the `docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md` dials before wiring Authoritarianism.**

### 3a-bis. Faction fingerprints — the design GAUGE (not flavor)
This table is a **test**, not decoration: if the finished AI does *not* play these factions visibly differently, the traits aren't wired. Every pair below is separated by at least one axis — that's the check. (H/M/L; blank = mid. Group-2 axes: Zeal=Zealotry, Guile, Coh=Collectivism, Auth=Authoritarianism, Alt=Altruism.)

| Faction | Aggr | Amb | Risk | Hon | Xeno | Ruth | Cur | Zeal | Guile | Coh | Auth | Alt | The one-line fingerprint |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Klingons** | H | M | H | H | M | L | L | M | L | M | M | L | honorable open-battle glory-seekers |
| **Borg** | H | H | L | L | M | H | L | H | L | **H** | H | 0 | morale-proof assimilating swarm |
| **Romulans** | M | H | M | L | H | M | M | M | **H** | L | H | 0 | paranoid covert imperialists |
| **The Culture** | L | L | M | H | L | L | H | L | H | L | L | **H** | post-scarcity utopia that can't stop helping |
| **Minbari** | M | L | L | H | M | L | M | **H** | L | L | M | M | doctrine over victory (surrender at the brink) |
| **Imperium (40k)** | H | M | L | L | **H** | H | L | **H** | L | M | **H** | 0 | dogmatic xenophobe; tech-taboo despite need |
| **Federation** | L | M | M | H | L | L | H | L | L | L | L | H | diplomacy-first principled explorers |
| **Harkonnen** | H | H | M | L | H | **H** | L | L | M | M | H | 0 | cruelty as policy |
| **Ferengi** | L | H | M | H | L | L | L | 0 | M | L | L | **0** | trade with anyone, never at a loss |
| **Fremen / Ori-type** | H | M | H | H | H | M | L | **H** | L | M | M | 0 | faith that becomes galactic jihad |
| **Bene Gesserit** | L | H | L | M | M | M | H | L | **H** | L | H | 0 | century-scale schemers acting through others |

Read the columns: **Borg vs. Klingons** split on Collectivism + Honor; **Romulans vs. Klingons** on Guile; **Culture vs. Ferengi** on Altruism (both low-aggression traders — opposite souls); **Imperium vs. Harkonnen** on Zealotry (dogma vs. mere cruelty); **Minbari vs. anyone** on Zealotry (the only faction that *stops winning* on principle). If our AI can't reproduce those contrasts in actual play, a trait is unwired.

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

> This means officer traits use the **same 0–1 vocabulary as factions** — one trait system, two owners (a faction, a person). A seat's effective "personality" for scoring = a blend of faction identity + the officer's own traits.

**Blend rule (DECIDED 2026-07-10): faction identity outweighs the officer BY DEFAULT — but a long tenure and/or formative experiences can let the officer OVERRIDE it in their domain.** A new xenophobic diplomat in a cooperative faction lands only *slightly* pricklier than the faction norm (faction dominates). But an officer who holds the seat for years — especially one hardened by defining events (§3e career drift) — accumulates *override weight*, until a long-serving, strong-charactered officer genuinely bends their domain toward *their* temperament (the faction's master diplomat, after a career of betrayals, actually runs a hostile foreign policy the faction wouldn't naturally choose). So the blend is not fixed: it **shifts from faction-dominant toward officer-capable-of-override with tenure + experience.** This is the mechanism behind "a decapitation/replacement matters" and "who you leave in a seat for a decade changes your empire."

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
2. **Ratify the 12-trait model (§3a).** Group 1 (Aggression/Ambition/Risk/Honor/Xenophobia/Ruthlessness/Curiosity) + Group 2 (Zealotry/Guile/Collectivism/Authoritarianism/Altruism). Open sub-questions: (a) accept all 5 Group-2 adds, or trim; (b) any of the **deferred four** (Patience/Adaptability/Venality/Isolationism) you want promoted now vs. later; (c) resolve **Authoritarianism vs. `GovernmentDB`** overlap before wiring; (d) 12 dials each on factions AND officers is real tuning surface — confirm that's the depth you want (the developer's call was "deeper than Dwarf Fortress / OG Aurora," so likely yes).
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

*Companion docs: `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the roster + the leader pipeline), `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the delegate mechanism), `docs/society/GOVERNMENT-AND-POLITICS-DESIGN.md` (government-as-modulator), `docs/society/DIPLOMACY-DESIGN.md` (Foreign-Minister external politics). This doc is the information/communication layer that ties the levels together.*
