# AI Supercluster & Scenario Authoring — the "multiverse" layer (staging a franchise)

> **Status: v0.2 — RATIFIED (2026-07-10).** §5 resolved: the authoring spectrum with a near-term/later split (**near-term = the player authors their own faction; scenarios + mod-editor tiers are a "much later us problem," deferred**); data-driven (engine=code, mod=data); emergent = **author the setup, never the plot**, with decision-logs making emergence checkable. **This ratifies the LADDER quark→brane.** The **final rung** — and *not new machinery.* It's the **AUTHORING layer that instantiates a "universe" (a scenario) from the engine below**, plus the **ACCEPTANCE TEST** (does the whole stack deliver the north star — can you stage an aspect of a franchise and have it play believably?). Everything below this rung is the engine; this rung spins up universes from it. Cross-links: `docs/NORTH-STAR-VISION.md` (the north star this tests), `AI-OBJECTIVE-ENGINE-DESIGN.md` / `AI-ECOSYSTEM-DESIGN.md` / `AI-GALAXY-AND-CRISIS-DESIGN.md` (the engine), the base-mod JSON + the in-game mod editor (the authoring surface).

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

## 5. RESOLVED (developer's calls, 2026-07-10)

**1 & 4 — the authoring SPECTRUM (one axis), with a clear near-term vs. later split:**
- **NEAR-TERM — the player authors their own faction, as deeply as they want.** This is the one authoring capability that matters for the build *now* — it's how a player makes their Federation, and how the tutorial's factions are defined. (The three-tier framing — new-game-setup → scenario-editor → full-mod — is the right shape, but only the faction-authoring floor is near-term.)
- **LATER — "a MUCH later us problem":** the **collection of starting scenarios** + the mod-editor scenario/full-mod tiers. Explicitly deferred; not first-slice. The generated-galaxy **faction POOL** (authored archetypes, *not* random-dial mush — replayability is a *content* job) rides here too.

**2 — Data-driven: flat yes.** A scenario is JSON over a fixed engine: **engine = the machinery (code); mod = the content (data).** Editor-extension (AI-authoring panels) is the deferred later cost — raw JSON to start.

**3 — Emergent: author the SETUP, never the PLOT.** Strong *initial conditions* bias toward the franchise dynamic; **no scripted events** — the Cardassians defect *when their own engine decides it pays*, not on a timer. And **the decision-logs (the personality/objective readout gauges) are what make "did it emerge?" checkable** — the acceptance test is *playtest + confirm-via-logs*, not a vibe-check. The visibility gauges are what make the acceptance test rigorous.

**→ LADDER COMPLETE & RATIFIED (2026-07-10): quark → brane.** One engine, no central AI at any scale; near-term authoring = the player's own faction; scenarios/modding deferred.

---

*With this rung the design is complete **quark → brane**: atomic scored-decision → trait → officer → seat → mandate → faction (needs-ladder + transition engine) → emergent ecosystem → galaxy arc + emergent crisis → the authoring layer that instantiates any of it. One engine, no central AI at any scale, franchise-staging as the acceptance test.*
