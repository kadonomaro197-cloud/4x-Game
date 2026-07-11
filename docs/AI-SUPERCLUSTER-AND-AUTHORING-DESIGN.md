# AI Supercluster & Scenario Authoring — the "multiverse" layer (staging a franchise)

> **Status: v0.1 DISCUSSION DRAFT (2026-07-10).** The **final rung** — and *not new machinery.* It's the **AUTHORING layer that instantiates a "universe" (a scenario) from the engine below**, plus the **ACCEPTANCE TEST** (does the whole stack deliver the north star — can you stage an aspect of a franchise and have it play believably?). Everything below this rung is the engine; this rung spins up universes from it. Cross-links: `docs/NORTH-STAR-VISION.md` (the north star this tests), `AI-OBJECTIVE-ENGINE-DESIGN.md` / `AI-ECOSYSTEM-DESIGN.md` / `AI-GALAXY-AND-CRISIS-DESIGN.md` (the engine), the base-mod JSON + the in-game mod editor (the authoring surface).

---

## 0. The frame — the authoring layer IS the "multiverse"

**One engine, many instantiable universes.** Each authored scenario is a "brane" — a Star Trek universe, an Expanse universe, a BSG universe — spun up from the *same* machinery with *different authored inputs*. The Supercluster rung is the layer that **spawns universes** (authoring/instantiation) + the **test** that they play believably. (Planck-length → multiversal-branes, unironically: the atomic scored-decision at the bottom, the instantiable-universe authoring at the top.)

---

## 1. What a "universe" (scenario) is made of — the authored bundle

A scenario = a bundle of authored data, using every "author top-to-bottom" ingredient we've already established:
- **Factions** — traits, mood defaults, the **authored Tier-3 ambition**, needs-ladder tuning, the **ship/design ladder**, tech tree, government type.
- **The galaxy** — systems, bodies, the jump network (generated *or* hand-authored).
- **The opening situation** — who's where, each level's starting tier, relationships/reputations, who's at war, who's met whom.
- **Seeded ascension-seeds / crisis triggers** (the Galaxy rung's catalog).
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

## 5. Open questions (with leans)

1. **Both authoring paths — generated galaxy AND authored scenario?** *(Lean: yes.)*
2. **Data-driven surface — a scenario is a MOD (JSON), authored via the existing mod editor?** *(Lean: yes — same pattern as factions/ships/traits/crises.)*
3. **Acceptance criteria — "the franchise DYNAMIC emerges, plot NOT scripted"?** *(Lean: yes — emergent feel, never canned events; that's the whole point of building the engine instead of a story.)*
4. **Player vs. designer authoring reach?** *(Lean: player always authors their own faction; full-scenario authoring is a designer/modder capability the editor exposes to anyone who wants it.)*

---

*With this rung the design is complete **quark → brane**: atomic scored-decision → trait → officer → seat → mandate → faction (needs-ladder + transition engine) → emergent ecosystem → galaxy arc + emergent crisis → the authoring layer that instantiates any of it. One engine, no central AI at any scale, franchise-staging as the acceptance test.*
