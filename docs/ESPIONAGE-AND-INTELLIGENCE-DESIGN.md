# Espionage & Intelligence — the Hidden-Information Engine (design)

**What it does, in one line:** turns "what do they actually intend?" into a thing you can *spend to learn* and *risk getting caught doing* — so diplomacy stops being a spreadsheet of known numbers and becomes a **reading game** where you act on incomplete information, pay to reduce uncertainty, and gamble on exposure.

**Why it matters (and why it's NOT optional):** the developer locked the **FULL hidden-information version** of external politics (`docs/DIPLOMACY-DESIGN.md` → "Making politics FUN"): you do not see a rival's true stance or plans; you infer them from behavior, and **intelligence is the currency that buys the truth.** Espionage is the system that *mints that currency*. Without it, the locked "you infer, you can be deceived, you can deceive" loop is just a label. With it, the whole external layer becomes poker — which is exactly the fun 4X has always missed. This is also the Babylon-5 "covert agents" pillar the developer named.

> **Analogy (the developer's wheelhouse):** think of a rival empire the way you'd think of a contact on **sonar**. You don't *see* the boat — you hear a noise signature and infer course, speed, intent. A good sonar tech reads more from the same noise; better sensors resolve more; and you can run **silent** so *they* can't read *you*. Espionage is buying a clearer picture of a contact you can't see directly — and the risk that pinging actively gives away that you're listening.

---

## The core decision (the lever)

**Spend scarce intelligence effort to convert *hidden → known* about a rival — and accept a chance of being caught, which can cost you more than the secret was worth.** Every intel op is a bet: the value of knowing (or of the sabotage/theft you pull off) vs. the **detection risk** and what exposure triggers (a soured relation, a betrayal penalty, even a casus belli handed to *them*). You never have enough good agents or attention to know everything about everyone — so *what you choose to learn, and from whom,* is the strategy.

---

## The INFORMATION LEDGER — the heart of the system

The load-bearing new concept. For every rival faction you've met, you hold an **intel level on several FACETS** — your picture is sharp on some, fuzzy on others, blank on the rest:

| Facet | What it tells you | How it's fed |
|---|---|---|
| **Disposition** | Do they *actually* like you / plan war? (vs. the face they show) | a posted ambassador (passive) · agents (active) |
| **Military** | fleet counts, locations, tech, where the gaps are | sensors/detection (passive) · agents (active) |
| **Economy** | what they need, what they hoard, their money | trade contact (passive) · agents |
| **Internal politics** | which blocs are strong, their unrest, a wavering province's legitimacy | ambassador (passive) · agents (active) |
| **Secrets** | their treaties with others, war plans, a hidden weakness | agents only (active) |

Each facet has an **intel level**, roughly three bands (calibration later):
- **Inferred** (default) — you only see *behavior* (fleet moves, who they treat with, message tone) and a **fuzzy estimate** with error bars. This is the poker default.
- **Confirmed** — you've raised intel on that facet: the estimate sharpens or the truth is revealed.
- **Stale** — intel **decays**; a confirmed picture drifts back toward inferred as the world changes, so you must *refresh* (you can't learn a rival once and know them forever).

**This is fog-of-war for politics**, and it's the same idea as the combat fog already built — extended from "where are their ships" to "what are they thinking." It rides the **detection substrate** (sensors/EMCON), which is why fixing the **degenerate detection-quality signal** (`GameEngine/Sensors/CLAUDE.md`) is a hard prerequisite: how much you can *infer from behavior* depends on detection quality carrying graduated information.

---

## Agents — the people who do it (the M3 talent arm)

Espionage is the **intelligence arm of the people-as-a-resource pool** — same pattern as admirals/governors/diplomats:

- **Spymaster** — the **delegate** (the Every-Layer symmetry). You set a **stance** ("steal tech," "counter-intel focus," "destabilize [Rival]," "keep us informed"), and the spymaster auto-runs the espionage portfolio **at a competence cost** (a skilled one lands ops and reads threats; a poor one gets agents caught). The "I don't want to micro spies" path.
- **Agents / operatives** — individual people (skill + traits) you task on specific ops. Skill **raises success chance and lowers detection**; traits spawn flavor and stories. Scarce (M3 talent-gated) — you have a few good ones, so *where you point them* is a real choice.
- **Grave rung:** an agent can be **caught, killed, or TURNED** (a rival's counter-intel flips them into a double agent feeding *you* disinformation — the mirror of you turning *their* diplomat). Losing a master agent hurts like losing a veteran admiral.

---

## The COVERT-ACTION CATALOG — what an agent can do (build broad, data-driven)

Mirror of the diplomacy **exchange catalog**, but **unilateral and risky**. Build it broad; each entry names the effect, what facet/system it routes into, and the cost of being caught:

| Action | Effect | Routes into | Caught = |
|---|---|---|---|
| **Gather intel** | raise the intel level on a facet (disposition / military / secrets…) | Information Ledger | a relation hit (minor) |
| **Steal technology** | copy a rival's researched tech | Research / `FactionTechDB` | relation hit + they may re-secure |
| **Steal funds / resources** | divert money or materials to you | Ledger / Logistics | relation hit |
| **Sabotage** | damage an installation/station, slow production, wreck a shipyard | Industry / Stations (damage) | **betrayal penalty** + casus belli for THEM |
| **Sow unrest** | boost a discontented bloc, drop a province's legitimacy, incite rebellion | their INTERNAL politics (per-system legitimacy) | **betrayal penalty** + casus belli for THEM |
| **Turn / assassinate a person** | defect or kill a rival commander/minister/diplomat | People (grave rung) | **betrayal penalty** + possible war |
| **Plant disinformation** | feed them FALSE intel — make them misread your intent/strength | their Information Ledger (about you) | they realise they were played |
| **Counter-intelligence** (defensive) | protect your secrets, catch/мole-hunt their agents in you | your own ledger security | — (it's the shield) |

The **EXTERNAL-into-INTERNAL reach** is the spicy one: *sow unrest* lets your spies reach into a rival's **internal politics** — pour fuel on a bloc, push a wavering frontier system toward secession (the per-system legitimacy model from `GOVERNMENT-AND-POLITICS-DESIGN.md`). And the **mirror** is always on: NPC factions run the same playbook against *you*, so **counter-intelligence is a standing decision, not optional** — neglect it and your secrets leak and your provinces get destabilised.

---

## The RISK side — what makes it a gamble, not a free menu

Every **active** op is a **detection roll**: agent skill (+ spymaster stance) vs. the target's counter-intelligence. The outcome scales by **severity**:
- **Clean success** — the effect lands / intel rises, no one the wiser.
- **Success but traced** — it works, but they know *someone* did it (and may suspect you).
- **Caught** — the op fails AND you pay: a **relation hit** for soft ops (gather), the full **betrayal penalty** for hard ops (sabotage/assassinate/sow-unrest), and — the sharp part — **a casus belli handed to THEM** (they now have a justification to come after you, gated by *their* militarism). Your **agent may be lost** (captured/killed/turned).

So a covert op is a genuine risk/reward bet: a juicy sabotage might cripple their shipyard — or hand them a righteous war. **That tension is the gameplay.**

---

## The hidden-information game, made concrete (how it all plays)

1. By default you read a rival's **behavior** + a **fuzzy disposition estimate** (Inferred). Are those fleets massing on your border a real threat or a bluff?
2. You **spend intelligence** — post an ambassador (passive), or task an agent (active) — to raise the intel level and **sharpen or confirm** the picture.
3. You can be **deceived**: their *disinformation* feeds you a false estimate; their **EMCON-dark** buildup is invisible to your passive intel (the detection tie-in). And you can deceive *them* the same ways.
4. You **act on incomplete information** — sign the pact, mass the counter-fleet, call the bluff — knowing you might be wrong. That's the poker, and intelligence is how you tilt the odds before you commit.

This is the loop that makes the **diplomacy-as-a-closing-fight** metaphor real: intelligence is the reconnaissance phase before the engagement.

---

## Cradle to grave (espionage)

> research **spy tech** → design/build the **gear** (a covert-ops component / an intelligence HQ — the seat that gives intelligence capacity) → recruit/train an **agent** (people, M3) → seat a **Spymaster** delegate or task the agent directly → spend scarce **intelligence capacity** on a **covert op** from the catalog → **roll detection** vs their counter-intel → success raises **intel** or lands the **effect**; caught = the **betrayal penalty** + a **casus belli for them** + the **agent is lost** (captured/killed/turned — the grave rung) → re-research / re-recruit / re-run. Destroy a rival's **intelligence HQ** (sabotage or war) and you blind their spy network — the grave rung wired to the damage system.

Every rung is reachable and losable — it is NOT a parachuted-in "spy points" abstraction. Spy capability is a **component** you research/build/install/lose (the law of `CONVENTIONS.md` §6), exactly like a sensor.

---

## Connections (Prime Directive)

- **Detection / sensors / EMCON** (built; quality degenerate) — the **inference substrate**: how much you read from behavior is detection quality. **Hard prerequisite:** fix the degenerate detection-quality signal. EMCON-dark hides your buildup from their passive intel (the bluff).
- **Diplomacy** (designed) — intel is **negotiation leverage** (dirt at the table); caught espionage **craters the relation TRACK** and can hand the other side a casus belli. The ambassador is the *passive* intel feed.
- **Internal politics** (designed) — *sow unrest* reaches into a rival's **blocs / per-system legitimacy**; their agents do it to **you** → counter-intel defends your provinces. The reactive demand-engine can surface "we are being destabilised by [Rival]."
- **People** (M3 talent) — Spymaster + agents are the intelligence arm; *turn/assassinate* hits the **people grave rung** (theirs and yours).
- **Tech / research** (built) — *steal-tech*; and spy capability is **research-gated** (better gear, better counter-intel). Cradle-to-grave via components.
- **Military / combat** (built) — *sabotage* damages production/installations; **military intel feeds the combat first-strike** (knowing where they are = the seer's edge already built in the detection slice).
- **Stations / industry** (built) — sabotage targets; an **intelligence HQ** is a station/colony component (the capacity seat); a fragile station spy-node is the easy-to-lose frontier (ties to the station fragility).

---

## Locked vs. open

**Proposed for lock (developer to confirm):**
- **Espionage is the hidden-information ENGINE** — load-bearing for the locked full version, not optional flavor.
- **The INFORMATION LEDGER** — per-rival, per-facet intel level (Inferred → Confirmed → decays to Stale). Fog-of-war for politics, on the detection substrate.
- **Covert-action catalog built BROAD + data-driven** (mirror of the exchange catalog) — gather / steal-tech / steal-funds / sabotage / sow-unrest / turn-or-assassinate / disinformation / counter-intel.
- **Every active op is a RISK/REWARD detection bet** — caught scales from relation-hit → betrayal penalty → **casus belli for THEM**; agents can be **caught/killed/turned**.
- **Agents = the M3 people intelligence arm** — Spymaster (delegate) + taskable operatives; talent-gated; grave-rung.
- **The MIRROR is always on** — NPCs spy on you; **counter-intelligence is a standing decision**, not opt-in.
- **Spy capability is a COMPONENT** (research → build → install → lose) — an intelligence HQ is the capacity seat; not a bespoke "spy points" flag.

**Open (decide when we build):**
- Information-ledger granularity (per-facet bands vs. a finer score) + the **decay rate** (how fast confirmed intel goes stale).
- Detection-roll math (agent skill + spymaster stance vs. counter-intel) — calibration/feel.
- How much *sow unrest* can move a province's legitimacy (so a spy can *nudge* a rebellion but not single-handedly topple an empire).
- Disinformation mechanics (how you plant false intel; how the target can detect they were played).
- Whether intel is **shareable** as a diplomacy exchange (sell dirt — already a row in the exchange catalog; confirm it reads the ledger).

**Build order (after the keystone + diplomacy substrate; rides the detection-quality fix):**
detection-quality fix (prereq) → the **Information Ledger** (per-rival intel state + the Inferred/Confirmed/Stale bands) → **passive intel** (ambassador + sensors raise it) → the **agent/Spymaster** people layer → the **covert-action catalog** (gather FIRST — the pure hidden-info lever — then steal/sabotage, then sow-unrest) → **detection + counter-intel + caught-consequences** (the risk side) → the **NPC mirror** (they spy on you; reactive "we are being destabilised") → **disinformation** (last; the bluff weaponised).
