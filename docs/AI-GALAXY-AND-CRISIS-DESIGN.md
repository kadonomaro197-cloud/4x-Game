# AI Galaxy & Crisis — the arc of the game, and the late-game crisis

> **Status: v0.1 DISCUSSION OPENING (2026-07-10).** The **Galaxy** rung — the *temporal shape* of a whole game (early → mid → late) and the **late-game CRISIS**. This rung mostly **composes** the Ecosystem (`AI-ECOSYSTEM-DESIGN.md`) rather than adding new machinery — with **one deliberate exception** (the crisis). Above it sits only the Supercluster (franchise-staging), which is the acceptance test in `docs/NORTH-STAR-VISION.md`. Cross-links: `AI-OBJECTIVE-ENGINE-DESIGN.md` (the fractal needs-ladder that *is* a faction's arc), `AI-ECOSYSTEM-DESIGN.md` (the common-enemy machinery the crisis triggers).

---

## 0. The arc EMERGES — it is not scripted

The game already has a built-in arc, because the **fractal needs-ladder is a developmental arc** (Survive → Stabilize → Thrive → Ambition) and the map fills over time. So the classic 4X shape falls out of the faction engines + the map, **unscripted**:
- **Early game** — factions low on the ladder (Survive/Stabilize), the map is fog, contact is sparse: **exploration & establishment.** (The reading game is *hungry* here — everyone's ignorant, so intel-gathering objectives dominate.)
- **Mid game** — factions climbing to Thrive, borders touch, rivalries form: **collision & the ecosystem at full tilt** (coalitions, wars, the balance-of-power dance).
- **Late game** — mature board, a few dominant powers pursuing their Tier-3 **Ambitions**, high-stakes wars — **and/or the CRISIS.**

**We do NOT script phases.** The lever is **pacing** (map size, expansion cost, tech rate, contact density) — tune *when* factions collide, not *that* they do. The mid-game is the ecosystem running hot; its only design risk is being too static (factions never interact) or too chaotic (permanent war), and that's a **tuning** problem, not a new mechanic.

---

## 1. The late-game CRISIS — the ONE deliberate top-down element (but still not a galaxy-AI)

A late-game **crisis** is the galaxy-wide threat that gives the endgame a *climax* instead of a slow snowball grind — the Reapers, the Flood, a Borg incursion, a machine uprising, an awakened ancient, an extragalactic swarm, a spreading cataclysm. It's the one place a *designed*, top-down element is warranted. **But it preserves the "no galaxy-AI" principle by being modeled as a special FACTION**, not a scripted galaxy-event:

> **The crisis is just another faction** — an **authored** one with an **overwhelming Tier-3 ambition** ("end all other life" / "assimilate everything" / "consume the galaxy"), dramatic power, and special mechanics, spawned or awakened **late**. The galaxy responds to it through the **exact ecosystem machinery already built**: it becomes the **ultimate common enemy → the ultimate forced coalition** (`AI-ECOSYSTEM-DESIGN §0` — the alliance-from-shared-adversity mechanism, at maximum stress). No new "galaxy-AI" is needed — a scary Organism is.

This is the elegant consistency: even the game's biggest top-down moment is *a faction the emergent system reacts to*, so the whole design stays "no central mind."

### Why the crisis is the RIGHT anti-snowball mechanism (not a rubber-band)
The Ecosystem locked **no rubber-band** (`§2 Q3`: "space is unforgiving"). The crisis is the **legitimate, in-world** answer to a runaway hegemon — *not* an artificial catch-up:
- It threatens **everyone**, including the leader — so it resets stakes without cheating.
- It can even be **triggered by dominance or recklessness** (a hegemon's over-expansion awakens the ancient; someone researched the forbidden tech) — making the crisis a **consequence of how the game was played**, and a natural check on both snowballing *and* reckless tech-rushing, without any hidden hand tilting the odds.

---

## 2. Win / loss

**Faction-authored, plus a shared survival stake.** A faction's **Tier-3 authored ambition IS its victory condition** (Klingon → galactic conquest/honor; Federation → a stable peace; the player → whatever they authored; tutorial: Earth → survive & retake Sol, Mars → conquer Earth). The **crisis adds a shared survival stake** on top — *repel it or the galaxy ends* — which can override the faction victories until it's resolved. So the game can end by **domination** (a faction reaches its ambition), by **crisis-loss** (the galaxy falls), or by **crisis-victory** (the galaxy unites, pays the price, and survives — into a weakened post-crisis board).

---

## 3. Open design questions (with leans)

1. **Arc — emergent or imposed?** *(Lean: EMERGENT from the fractal ladder + map-filling; we tune pacing, we don't script phases.)*
2. **Is the crisis a special FACTION, not a scripted galaxy-event?** *(Lean: YES — a crisis-faction with an overwhelming ambition + trigger, responded to via the ecosystem. Preserves "no galaxy-AI.")*
3. **What TRIGGERS the crisis?** pure time (a doomsday clock) · a **tech-threshold** (someone researched the forbidden thing) · a **dominance-threshold** (a hegemon's over-expansion awakens it) · a mix? *(Lean: CONDITION-triggered — forbidden-tech and/or over-dominance — so it's a consequence of play, not an arbitrary timer, and it doubles as the anti-snowball/anti-tech-rush check.)*
4. **Is the crisis BEATABLE, or a reshaping force?** an insta-loss doomsday · a trivial speed-bump · or **beatable-but-brutal** (the galaxy CAN win if it unites and pays enormously, opening a weakened post-crisis phase)? *(Lean: beatable-but-brutal.)*
5. **A CATALOG of crisis types, authored like factions?** *(Lean: YES — a small data-driven set (machine-uprising · extragalactic swarm · awakened ancient · cataclysm) so designers/modders add franchise-appropriate crises — Reaper/Flood/Borg analogs — same "author the faction top-to-bottom" philosophy.)*

> **Build-priority note:** the crisis is a **full-game, late-game** feature — the Earth-vs-Mars tutorial is a bounded early-arc slice that needs none of it. So the Galaxy rung is a *later* build; it's designed now for completeness (quark→supercluster), not for the first slice.

---

*The bet extends: the arc emerges from the faction ladders + the map; the crisis is the one authored top-down element, and even it is a faction the emergent ecosystem reacts to — so "no galaxy-AI" holds all the way up. Supercluster (franchise-staging) is then the acceptance test, not new machinery.*
