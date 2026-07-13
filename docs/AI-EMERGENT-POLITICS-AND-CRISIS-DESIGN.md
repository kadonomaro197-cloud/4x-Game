# AI Emergent Politics & Crisis — the Organism engine pointed OUTWARD, over time, and how a universe is instantiated

> **What this is.** The top three rungs of the AI design ladder, told as one continuous story: what happens when several fully-designed factions share a galaxy and must react to *each other* (**Ecosystem** — emergent inter-faction politics), the **temporal shape** of a whole game plus the **late-game crisis** (**Galaxy**), and the **authoring layer** that instantiates a whole "universe" from the engine below plus the **acceptance test** that proves the stack delivers the north star (**Supercluster**). The through-line: **there is no galaxy-AI at any scale** — every inter-faction behavior emerges from individual factions each running their own needs-ladder + transition engine, with the *other factions* as inputs. Galaxy and Supercluster mostly **compose** the Ecosystem rather than adding new machinery (the one deliberate exception is the crisis).
>
> **Consolidated 2026-07-13 from:** `docs/AI-ECOSYSTEM-DESIGN.md`, `docs/AI-GALAXY-AND-CRISIS-DESIGN.md`, `docs/AI-SUPERCLUSTER-AND-AUTHORING-DESIGN.md`.
>
> **Status: v0.2 DISCUSSION DRAFT / RATIFIED (2026-07-10).** Ecosystem core questions RESOLVED (Part 1 §2); Galaxy/Crisis core RESOLVED (Part 2 §3); Supercluster authoring layer **RATIFIED** (Part 3 §5) — which **ratifies the whole LADDER quark→brane**. This is a *design*, not a build — see the honest build-state notes inline (the crisis in particular is the biggest MUST-BUILD, riding two unbuilt sockets).
>
> **Cross-links:** `AI-OBJECTIVE-ENGINE-DESIGN.md` (the actors — the fractal needs-ladder + transition engine that *is* a faction's arc), `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (traits / mood / the scored-choice engine), `DIPLOMACY-DESIGN.md` (the relationship substrate + treaties + casus belli, already built — the substrate the tools sit on), `docs/EXPLORATION-CONTENT-DESIGN.md` (the field-site loop the crisis activation rides on), `docs/NORTH-STAR-VISION.md` (the north star the acceptance test checks).

---

# PART 1 — THE ECOSYSTEM: emergent inter-faction politics (NO galaxy-AI)

*The last design rung that adds genuinely new machinery. It's the **Organism engine pointed outward** — what happens when several fully-designed factions (Organism level: fractal needs-ladder + traits + mood + transition engine) share a galaxy and must react to each other. Galaxy and Supercluster (below) mostly compose it.*

## 0. The load-bearing principle — there is NO galaxy-AI; the Ecosystem is the Organism engine pointed OUTWARD

No central mind stage-manages galactic politics. **Every inter-faction behavior EMERGES from individual factions each running their own needs-ladder + transition engine, with the OTHER FACTIONS as inputs** (a rival is just another situation/gauge — "a rising rival" is already one of the bounded situation-types). The Ecosystem layer adds only two things:

1. **What a faction PERCEIVES about the others** (the reading game).
2. **The inter-faction TOOLS** (ally / betray / threaten / appease / vassalize — the diplomacy actions, mostly already in the `DiplomacyDB` substrate).

The engine we already built produces the politics.

### The acceptance test — trace every emergent behavior back to individual engines

If a desired behavior needs a special *"galaxy rule,"* we've failed the no-galaxy-AI principle. The four behaviors we want, traced back to individual engines:

- **Balance of power** → each faction's *Survive-instinct* reads a would-be hegemon as a survival threat → individually balances (ally against the strongest / build up). *Emergent.*
- **Coalition vs. a rising threat** → several factions independently read the same riser as a threat → each seeks allies against it → they find each other (aligned objectives). *Emergent.*
- **The alliance CRACKS when the common enemy dies** → the alliance was each faction's *"ally against X to survive"*; X gone → the threat-gauge drops → the alliance objective no longer scores → each **re-plans** (the transition engine's re-score) and old ambitions resurface. **The dissolution is just the re-plan firing when the situation changes** — no "alliance expiry rule." *Emergent.*
- **Betrayal** → a faction reads *"my ally is now weak/distracted; taking them serves my goal more than the alliance"* → re-plans to attack, **gated by Honor** (high-Honor won't; low-Honor will). *Emergent, trait-gated.*

Every one traces back. The Ecosystem is the **inputs + tools** that let the built engine produce politics — not a second engine.

---

## 1. The ONE thing we must get right — the STRUCTURAL-threat read

Balance-of-power emerges **only if a faction fears RISING power, not just active attacks.** The threat-gauge must register *"a neighbor becoming powerful enough to eventually crush me"* as a survival threat **now** — otherwise you get **runaway snowballing** (one faction wins because nobody opposed its rise until too late — the classic 4X-AI failure). This is the load-bearing requirement of the whole rung: the survival-read must include **structural / rising threat**, not just "is attacking me this turn." Get this right and the balance-of-power dance emerges; get it wrong and the galaxy snowballs.

---

## 2. Design questions — RESOLVED (developer's calls, 2026-07-10)

**1. Perception → FOGGED, and IGNORANCE is itself a driver.** A faction reads *estimates* of a rival's strength and *infers* intentions from observed behavior; espionage sharpens it; misjudgment is a feature. **The upgrade:** an information deficit is a **GAUGE** — "I know nothing about faction X and that's dangerous" — that spawns an **objective** (close the gap), satisfied by a **trait-scored plan** like any other transition:
   - **Borg** → aggressive probes/scans into enemy space, *tip-off be damned* (Aggression + Collectivism).
   - **Federation** → open a trade route / research agreement to *earn* access peacefully (low-Xeno, Honor).
   - **Guile faction** → infiltrate quietly (covert intel).

   So fog is **interactive** — the AI *acts* on not-knowing; intelligence-gathering is a first-class driven objective, not background bookkeeping. "The AI must see the problem and be given options to solve it."

**2. Alliances → EARNED organically, not declared.** An alliance is the **top of a warming trajectory**, never a seed pact. A **shared enemy is the spark** (soft alignment); the **bond is built by shared adversity + cooperative deeds** (fighting the common foe together, sending aid) — *boot-camp / shared-trauma bonding*. A formal pact is the **capstone**, not the start. **Emergent gradient:** the *depth* of the bond decides whether it survives the enemy's death — a shallow marriage-of-convenience cracks the moment the threat's gone (the re-plan fires); a deep brotherhood-of-arms forged over a long war may endure. (The `DiplomacyDB` relationship score already models the warming.)

**3. Balance of power → EMERGENT, and NO rubber-band.** *"If you can't react to a rising power quick enough, tough — space is unforgiving."* No artificial balancing, no safety net; a missed threat-read is a legitimate loss, and snowballing is a valid outcome. **This is what makes Q1 matter:** with no net, **intelligence IS survival** — the faction that invested in knowing its neighbors sees the hegemon rising in time to act; the blind one gets snowballed before it noticed. **Consequence:** the **structural-threat read (§1) must be genuinely good** — a competent faction MUST be able to see a riser — because there's nothing catching it if it can't.

**4. Target / side selection → the scored engine.** Each inter-faction stance (ally X / attack Y / appease Z / stay neutral) scored by **THREAT × OPPORTUNITY × AFFINITY × HISTORY**, weighted by traits + current tier (Survive-tier picks defensively; Ambition-tier picks a predator's targets). Confirmed.

**5. Reputation → real, but SOCIAL and FOG-GATED (needs a 3rd party).** Reputation only works if someone else is there to *learn* of the deed — with only two factions there's no reputation, just a private grudge, so **reputation is an emergent property of a 3+ faction galaxy** (developer's hard requirement). And it's **fog-gated:** a **covert** betrayal (a secret op) only costs you *if you're caught*; an **overt** one (breaking a public treaty, an open backstab) is known to all who can observe. So **Honor × Guile interact** — a Guile faction betrays in the shadows and keeps its name; an open brute eats the hit; and getting *caught* in a covert betrayal is catastrophic (reputation hit *plus* you're exposed as a schemer). Reputation propagates through the same channels as all info (observation + the wronged party telling your rivals). This is where the **Honor trait gets its ecosystem-scale teeth.**

> **Scenario-testing requirement (developer's call, 2026-07-10): the tutorial needs 3+ factions.** Reputation (Q5), coalitions, and first-contact/the-reading-game are **only exercisable with a 3rd party** — a 2-faction Earth-vs-Mars war can't test them. So the tutorial scenario must add a **3rd alien faction** (Earth surviving + Mars ascendant + an alien wildcard = a possible ally, a first-contact unknown, and a live reputation test). Full detail in Part 2 §3 below.

---

## 3. The interlocks (why the rung "clicks")

- **Q1 × Q3 — intelligence is survival.** No rubber-band (Q3) is what *rewards* intel-gathering (Q1): good eyes = early warning on a riser = you live; ignorance = a surprise death. Q3 supplies the incentive that makes Q1 worth doing.
- **Q2 × Q5 — cooperation builds both bond AND reputation.** The shared-adversity deeds that deepen an alliance (Q2) are the same observed reliability that builds a good reputation (Q5). Fighting beside someone is *how you earn trust*, twice over.
- **Q5 × Guile/Honor — the shadow game.** Covert betrayal preserves reputation unless exposed → the espionage detection game (getting caught) becomes the hinge of the whole trust economy. High-Guile-low-Honor = the schemer nobody can pin; low-Guile-low-Honor = the pariah everyone shuns.
- **Q1 × Q5 — reputation rides the fog.** You only lose standing with factions that *learn* of your betrayal — reputation propagates through the same perception channels as everything else, so a well-hidden deed (or an isolated victim who can't spread word) costs little.

> **The Ecosystem bet holds:** perception (with ignorance as a driver) + earned alliances + a good structural-threat read + scored stance-selection + fog-gated social reputation, and coalitions / balance-of-power / betrayal / the-alliance-that-cracks all EMERGE from the faction engine we already built — no central galaxy-AI. Galaxy (the arc/crisis) and Supercluster (franchise-staging) then compose this layer rather than adding new mechanics.

---

# PART 2 — THE GALAXY & CRISIS: the arc of the game, and the late-game crisis

*The **temporal shape** of a whole game (early → mid → late) and the **late-game CRISIS**. This rung mostly **composes** the Ecosystem (Part 1) rather than adding new machinery — with **one deliberate exception** (the crisis). Above it sits only the Supercluster (Part 3), which is the acceptance test.*

## 0. The arc EMERGES — it is not scripted

The game already has a built-in arc, because the **fractal needs-ladder is a developmental arc** (Survive → Stabilize → Thrive → Ambition) and the map fills over time. So the classic 4X shape falls out of the faction engines + the map, **unscripted**:

- **Early game** — factions low on the ladder (Survive/Stabilize), the map is fog, contact is sparse: **exploration & establishment.** (The reading game is *hungry* here — everyone's ignorant, so intel-gathering objectives dominate.)
- **Mid game** — factions climbing to Thrive, borders touch, rivalries form: **collision & the ecosystem at full tilt** (coalitions, wars, the balance-of-power dance).
- **Late game** — mature board, a few dominant powers pursuing their Tier-3 **Ambitions**, high-stakes wars — **and/or the CRISIS.**

**We do NOT script phases.** The lever is **pacing** (map size, expansion cost, tech rate, contact density) — tune *when* factions collide, not *that* they do. The mid-game is the ecosystem running hot; its only design risk is being too static (factions never interact) or too chaotic (permanent war), and that's a **tuning** problem, not a new mechanic.

---

## 1. The late-game CRISIS — an EMERGENT ascension, NOT an injected boogeyman (developer's call, 2026-07-10)

A crisis is **NOT a special faction spawned from nowhere** (no Reaper-style deus ex machina). **It EMERGES: an existing faction — ANYONE, NPC or the player — crosses a game-changing BREAKTHROUGH threshold and becomes so powerful the galaxy must unite against it or submit.** The threat is always *one of your own* (or a latent thing already present in the world that a faction **activated** through play — an ancient superweapon explored, a forbidden tech researched). **Nothing appears that wasn't already there.** Thematically: *power corrupts / the arms race / the enemy is us*, not a random apocalypse.

- **The trigger is a consequence of play** (§Q3): a runaway breakthrough achieved by normal exploration/research. Archetype: a faction explores a system and learns to **convert a star's energy output into matter → unlimited materials → it out-builds the entire galaxy → existential threat.** Other seeds: machine-transcendence, an assimilation/conversion tech, an awakened ancient superweapon, a cataclysm unleashed.
- **The catalog (§Q5) is a moddable set of ASCENSION SEEDS** — latent breakthroughs/phenomena seeded in the galaxy that transform *whoever achieves one*. Author them franchise-appropriately (a Reaper-tech, a Flood-release, a Borg-assimilation breakthrough) — same author-it-top-to-bottom philosophy — but they **transform a PRESENT faction, they do not spawn a new one.**
- **The galaxy responds through the EXISTING ecosystem machinery** — the ascendant becomes the **ultimate common enemy → the ultimate forced coalition** (or others bandwagon/submit). No galaxy-AI; the crisis is the **balance-of-power machinery at maximum stakes.**

### The crisis IS the anti-snowball — because it's the runaway made flesh

The Ecosystem locked **no rubber-band** ("space is unforgiving"). The crisis needs none, because **it literally IS a runaway advantage**, and opposing it *is* the balance-of-power response — the anti-snowball and the crisis are the same thing. And its **severity scales inversely with REACTION TIME** (§Q4): catch the ascension **early** (good intel, fast coalition, before the breakthrough consolidates) → a manageable **speed-bump**, nipped in the bud; let the faction **fully ascend** while the galaxy is blind and slow → a **brutal near-extinction reshaping.** So the crisis is the **ULTIMATE test of the structural-threat read + "intelligence is survival"** (Part 1 §3) — *can the galaxy see one of its own becoming a god in time to stop it?* — the ecosystem's exact machinery with the volume at 11.

---

## 2. Win / loss

**Faction-authored, plus a shared survival stake.** A faction's **Tier-3 authored ambition IS its victory condition** (Klingon → galactic conquest/honor; Federation → a stable peace; the player → whatever they authored; tutorial: Earth → survive & retake Sol, Mars → conquer Earth). The **crisis adds a shared survival stake** on top — *repel it or the galaxy ends* — which can override the faction victories until it's resolved. So the game can end by:

- **Domination** — a faction reaches its ambition.
- **Crisis-loss** — the galaxy falls.
- **Crisis-victory** — the galaxy unites, pays the price, and survives — into a **weakened post-crisis board**.

---

## 3. Design questions — RESOLVED (developer's calls, 2026-07-10)

1. **Arc → EMERGENT** from the fractal ladder + map-filling; tune pacing, don't script phases.
2. **Crisis → an EMERGENT ascension of an EXISTING faction (anyone), NOT a special faction from nowhere.** No deus ex machina; the threat is always one of your own (or a latent thing a faction activated). See §1.
3. **Trigger → a CONSEQUENCE OF PLAY** — a game-changing breakthrough reached by normal exploration/research (the star→matter / unlimited-materials archetype), not a timer. The ascension seeds are the catalog (Q5).
4. **BEATABLE-BUT-BRUTAL, and SEVERITY scales with reaction time** — a speed-bump of *varying size* depending on how early you catch it: nipped early = manageable; fully ascended = near-extinction reshaping (win, but the galaxy pays enormously → a weakened post-crisis board). The ultimate test of the structural-threat read.
5. **A moddable CATALOG of ASCENSION SEEDS** (not external monsters) — latent breakthroughs/phenomena that transform whoever achieves them; author them franchise-appropriately. Same author-it-top-to-bottom philosophy.

> **Build-priority note:** the crisis is a **full-game, late-game** feature — the Earth-vs-Mars tutorial needs none of it. Designed now for completeness (quark→supercluster), a *later* build.

> **Socket-verification (2026-07-10) — the crisis is the biggest MUST-BUILD, on TWO missing sockets (build-state: NOT-BUILT):**
> 1. **Activation** — the explore→discover→reward pipeline is a throwing stub (`Fleets/ServeyAnomalyAction.cs` `Execute` throws) + a dormant `RuinsDB` (no processor); so the ascension seed rides the **unbuilt exploration-content system** (`docs/EXPLORATION-CONTENT-DESIGN.md`) — build that first.
> 2. **The breakthrough effect** — a **tech-model gap:** `Tech.Unlocks` only moves item-IDs (unlock a buildable component); it **cannot represent a galaxy-changing capability**, so an ascension tech needs a *new* tech concept ("a capability, not a component"). The trigger (`EventType.TechDiscovered`) already exists.
>
> Both are later builds; noted so the crisis isn't mistaken for a wiring job.

> **Scenario-testing requirement (developer's call, 2026-07-10): the Earth-vs-Mars tutorial needs a 3rd ALIEN faction to actually test the systems.** A 2-body war can't exercise **reputation** (needs a 3rd party — Part 1 §2 Q5), **coalitions** (need 3+), or **first-contact / the reading game** (need an *unknown* to read). The 3rd alien makes the tutorial a real ecosystem test:
> - **Earth** (surviving),
> - **Mars** (ascendant), and
> - an **alien wildcard** — a possible ally for Earth vs. Mars · a first-contact unknown nobody has intel on · a live reputation test (does it trust Earth or Mars by their behavior?).
>
> Fold this into the tutorial scenario design.

> **The Galaxy bet extends:** the arc emerges from the faction ladders + the map; the crisis is the one authored top-down element, and even it is a faction the emergent ecosystem reacts to — so "no galaxy-AI" holds all the way up. Supercluster (franchise-staging, Part 3) is then the acceptance test, not new machinery.

---

# PART 3 — THE SUPERCLUSTER & SCENARIO AUTHORING: the "multiverse" layer (staging a franchise)

*The **final rung** — and **not new machinery.** It's the **AUTHORING layer that instantiates a "universe" (a scenario) from the engine below**, plus the **ACCEPTANCE TEST** (does the whole stack deliver the north star — can you stage an aspect of a franchise and have it play believably?). Everything below this rung is the engine; this rung spins up universes from it. Cross-links: `docs/NORTH-STAR-VISION.md` (the north star this tests), the engine (Parts 1 & 2 + `AI-OBJECTIVE-ENGINE-DESIGN.md`), the base-mod JSON + the in-game mod editor (the authoring surface).*

## 0. The frame — the authoring layer IS the "multiverse"

**One engine, many instantiable universes.** Each authored scenario is a "brane" — a Star Trek universe, an Expanse universe, a BSG universe — spun up from the *same* machinery with *different authored inputs*. The Supercluster rung is the layer that **spawns universes** (authoring/instantiation) + the **test** that they play believably. (Planck-length → multiversal-branes, unironically: the atomic scored-decision at the bottom, the instantiable-universe authoring at the top.)

---

## 1. What a "universe" (scenario) is made of — the authored bundle

A scenario = a bundle of authored data, using every "author top-to-bottom" ingredient we've already established:

- **Factions** — traits, mood defaults, the **authored Tier-3 ambition**, needs-ladder tuning, the **ship/design ladder**, tech tree, government type.
- **The galaxy** — systems, bodies, the jump network (generated *or* hand-authored).
- **The opening situation** — who's where, each level's starting tier, relationships/reputations, who's at war, who's met whom.
- **Seeded ascension-seeds / crisis triggers** (the Galaxy rung's catalog — Part 2 §1/§Q5).
- **Victory conditions** — the per-faction ambitions + any scenario-specific goal.

All of it is **data (JSON)**, same as the base mod. A universe is a data file, not code.

---

## 2. Two authoring paths

- **Generated galaxy (replayable):** a random map; you author just **your own faction**; NPCs drawn from a pool. The "normal game."
- **Authored scenario (curated):** a designer sets **everything** — the **tutorial** (Earth + Mars + a 3rd alien), or a **franchise scenario** (a Dominion War). The "campaign/scenario."

A franchise scenario **is a MOD** (a bundle of JSON); the existing **mod editor** is the tooling. *(Lean: support both paths; the player always authors their own faction, and full-scenario authoring is a designer/modder capability the editor exposes to any player who wants it.)*

---

## 3. The ACCEPTANCE TEST — does the machinery deliver the north star?

**The criterion: given franchise-appropriate authored inputs, does the EMERGENT machinery reproduce the franchise's *feel* — WITHOUT scripting the plot?** The test passes if the *dynamic* emerges from authored inputs + the engine, not from canned events. Worked targets:

- **Dominion War (Trek):** author Federation + allies vs. Dominion + Cardassians with the right traits/ambitions/relationships → the **coalition-forms, war, the-Cardassians-defect** dynamics should EMERGE recognizably.
- **Cylon uprising (BSG):** a machine faction (Guile/infiltration, high-Ruthlessness) + a human remnant (pinned at Survive, fleeing) → the **reputation + espionage + reading-game + needs-ladder** should produce the paranoia-and-flight feel.
- **Goa'uld system-lords (Stargate):** many mid-power factions (high-Ambition, low-Honor, backstabbing) → the **shifting-alliances, betrayal-of-convenience** squabble should emerge.

If those *dynamics* fall out of the machinery given the authored setups — no scripted plot beats — the north star is delivered. This is the vision **made checkable**: it's a TEST, not a build.

---

## 4. The loop-back — this rung IS the first build

Closing the ladder lands you exactly where you'll start: **the tutorial is an authored scenario** (Earth + Mars + a 3rd alien), written in JSON, that exercises the whole stack. So *"how do I author Earth-Mars-plus-alien"* is the Supercluster rung applied to the first slice — the design's top rung and the build's first rung are the same file. Quark-to-brane, and it closes the loop back to something you can type.

---

## 5. RESOLVED (developer's calls, 2026-07-10)

**1 & 4 — the authoring SPECTRUM (one axis), with a clear near-term vs. later split:**

- **NEAR-TERM — the player authors their own faction, as deeply as they want.** This is the one authoring capability that matters for the build *now* — it's how a player makes their Federation, and how the tutorial's factions are defined. (The three-tier framing — new-game-setup → scenario-editor → full-mod — is the right shape, but only the faction-authoring floor is near-term.)
- **LATER — "a MUCH later us problem":** the **collection of starting scenarios** + the mod-editor scenario/full-mod tiers. Explicitly deferred; not first-slice. The generated-galaxy **faction POOL** (authored archetypes, *not* random-dial mush — replayability is a *content* job) rides here too.

**2 — Data-driven: flat yes.** A scenario is JSON over a fixed engine: **engine = the machinery (code); mod = the content (data).** Editor-extension (AI-authoring panels) is the deferred later cost — raw JSON to start.

**3 — Emergent: author the SETUP, never the PLOT.** Strong *initial conditions* bias toward the franchise dynamic; **no scripted events** — the Cardassians defect *when their own engine decides it pays*, not on a timer. And **the decision-logs (the personality/objective readout gauges) are what make "did it emerge?" checkable** — the acceptance test is *playtest + confirm-via-logs*, not a vibe-check. The visibility gauges are what make the acceptance test rigorous.

**→ LADDER COMPLETE & RATIFIED (2026-07-10): quark → brane.** One engine, no central AI at any scale; near-term authoring = the player's own faction; scenarios/modding deferred.

---

*With this rung the design is complete **quark → brane**: atomic scored-decision → trait → officer → seat → mandate → faction (needs-ladder + transition engine) → emergent ecosystem → galaxy arc + emergent crisis → the authoring layer that instantiates any of it. One engine, no central AI at any scale, franchise-staging as the acceptance test.*
