# AI Galaxy & Crisis — the arc of the game, and the late-game crisis

> **Status: v0.2 DISCUSSION DRAFT (2026-07-10).** Core RESOLVED (§3): arc EMERGENT; **crisis = an emergent ASCENSION of an existing faction (anyone), not an injected boogeyman** — a consequence-of-play breakthrough (star→matter/unlimited-materials archetype) that makes whoever achieves it the galaxy's threat, responded to via the existing ecosystem coalition machinery; **severity scales inversely with reaction time** (early=speed-bump, late=near-extinction) = the ultimate test of the structural-threat read; a moddable catalog of ASCENSION SEEDS; and the **tutorial needs a 3rd alien faction** to test reputation/coalitions/first-contact. The **Galaxy** rung — the *temporal shape* of a whole game (early → mid → late) and the **late-game CRISIS**. This rung mostly **composes** the Ecosystem (`AI-ECOSYSTEM-DESIGN.md`) rather than adding new machinery — with **one deliberate exception** (the crisis). Above it sits only the Supercluster (franchise-staging), which is the acceptance test in `docs/NORTH-STAR-VISION.md`. Cross-links: `AI-OBJECTIVE-ENGINE-DESIGN.md` (the fractal needs-ladder that *is* a faction's arc), `AI-ECOSYSTEM-DESIGN.md` (the common-enemy machinery the crisis triggers).

---

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
The Ecosystem locked **no rubber-band** ("space is unforgiving"). The crisis needs none, because **it literally IS a runaway advantage**, and opposing it *is* the balance-of-power response — the anti-snowball and the crisis are the same thing. And its **severity scales inversely with REACTION TIME** (§Q4): catch the ascension **early** (good intel, fast coalition, before the breakthrough consolidates) → a manageable **speed-bump**, nipped in the bud; let the faction **fully ascend** while the galaxy is blind and slow → a **brutal near-extinction reshaping.** So the crisis is the **ULTIMATE test of the structural-threat read + "intelligence is survival"** (`AI-ECOSYSTEM-DESIGN §3`) — *can the galaxy see one of its own becoming a god in time to stop it?* — the ecosystem's exact machinery with the volume at 11.

---

## 2. Win / loss

**Faction-authored, plus a shared survival stake.** A faction's **Tier-3 authored ambition IS its victory condition** (Klingon → galactic conquest/honor; Federation → a stable peace; the player → whatever they authored; tutorial: Earth → survive & retake Sol, Mars → conquer Earth). The **crisis adds a shared survival stake** on top — *repel it or the galaxy ends* — which can override the faction victories until it's resolved. So the game can end by **domination** (a faction reaches its ambition), by **crisis-loss** (the galaxy falls), or by **crisis-victory** (the galaxy unites, pays the price, and survives — into a weakened post-crisis board).

---

## 3. Design questions — RESOLVED (developer's calls, 2026-07-10)

1. **Arc → EMERGENT** from the fractal ladder + map-filling; tune pacing, don't script phases.
2. **Crisis → an EMERGENT ascension of an EXISTING faction (anyone), NOT a special faction from nowhere.** No deus ex machina; the threat is always one of your own (or a latent thing a faction activated). See §1.
3. **Trigger → a CONSEQUENCE OF PLAY** — a game-changing breakthrough reached by normal exploration/research (the star→matter / unlimited-materials archetype), not a timer. The ascension seeds are the catalog (Q5).
4. **BEATABLE-BUT-BRUTAL, and SEVERITY scales with reaction time** — a speed-bump of *varying size* depending on how early you catch it: nipped early = manageable; fully ascended = near-extinction reshaping (win, but the galaxy pays enormously → a weakened post-crisis board). The ultimate test of the structural-threat read.
5. **A moddable CATALOG of ASCENSION SEEDS** (not external monsters) — latent breakthroughs/phenomena that transform whoever achieves them; author them franchise-appropriately. Same author-it-top-to-bottom philosophy.

> **Build-priority note:** the crisis is a **full-game, late-game** feature — the Earth-vs-Mars tutorial needs none of it. Designed now for completeness (quark→supercluster), a *later* build.

> **Socket-verification (2026-07-10) — the crisis is the biggest MUST-BUILD, on TWO missing sockets:** (1) **activation** — the explore→discover→reward pipeline is a throwing stub (`Fleets/ServeyAnomalyAction.cs` `Execute` throws) + a dormant `RuinsDB` (no processor); so the ascension seed rides the **unbuilt exploration-content system** (`docs/EXPLORATION-CONTENT-DESIGN.md`) — build that first. (2) **the breakthrough effect** — a **tech-model gap**: `Tech.Unlocks` only moves item-IDs (unlock a buildable component); it **cannot represent a galaxy-changing capability**, so an ascension tech needs a *new* tech concept ("a capability, not a component"). The trigger (`EventType.TechDiscovered`) already exists. Both are later builds; noted so the crisis isn't mistaken for a wiring job.

> **Scenario-testing requirement (developer's call, 2026-07-10):** **the Earth-vs-Mars tutorial needs a 3rd ALIEN faction to actually test the systems.** A 2-body war can't exercise **reputation** (needs a 3rd party — `AI-ECOSYSTEM-DESIGN §2 Q5`), **coalitions** (need 3+), or **first-contact / the reading game** (need an *unknown* to read). The 3rd alien makes the tutorial a real ecosystem test: **Earth** (surviving) + **Mars** (ascendant) + an **alien wildcard** (a possible ally for Earth vs. Mars · a first-contact unknown nobody has intel on · a live reputation test — does it trust Earth or Mars by their behavior?). Fold this into the tutorial scenario design.

---

*The bet extends: the arc emerges from the faction ladders + the map; the crisis is the one authored top-down element, and even it is a faction the emergent ecosystem reacts to — so "no galaxy-AI" holds all the way up. Supercluster (franchise-staging) is then the acceptance test, not new machinery.*
