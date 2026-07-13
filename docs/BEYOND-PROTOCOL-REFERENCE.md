# Beyond Protocol — Design Reference

**What it is:** *Beyond Protocol* (Dark Sky Entertainment, 2008–2011) was a hardcore 3D sci-fi **MMORTS** — you ran a whole empire across thousands of planets, balancing colonization, a deep economy, fully-custom ship design, diplomacy, **galactic politics**, and **espionage**. It shut down in 2011; a spiritual-successor effort ("After Protocol") and a player strategy guide survive. Recorded 2026-06-28 at the developer's request as a second design north star **alongside Aurora 4X** (`docs/aurora/`).

**Why it's a reference (and how it differs from Aurora):** Aurora is the benchmark for **simulation fidelity** (the physics/logistics/fire-control depth). Beyond Protocol is the benchmark for the **strategic/human layer Aurora is thin on** — a living colony economy where *population is a resource you keep happy or lose*, **politics with teeth** (a senate that passes binding law), and **espionage as a real verb**. These are exactly the developer's missing decision layers (`docs/NORTH-STAR-VISION.md`: B5 politics, the economic pressure that makes a nomadic BSG fleet *hurt*). Mine BP for the **space-infrastructure + politics** depth; mine Aurora for the **physics**.

> **Sources** (WebFetch was 403-blocked on the wikis; these are the search-surfaced facts):
> [MMORPG.com](https://www.mmorpg.com/beyond-protocol) · [MobyGames](https://www.mobygames.com/game/37968/beyond-protocol/) · [Players Wiki](http://beyondprotocol.wikidot.com/) · [Ten Ton Hammer preview](https://www.tentonhammer.com/articles/space-mmorts-beyond-protocol-preview) · [After Protocol + strategy guide PDF](https://www.afterprotocol.com/) · [gamepressure](https://www.gamepressure.com/games/beyond-protocol/z11ef5)

---

## 1. THE GEM — the colony morale/population feedback loop (the near-term space-infrastructure target)

This is the mechanic to study first, because it's the **"finish the space economy to a good degree"** piece the developer wants *before* going planetside, and Pulsar's population system is currently a **stub**.

**The loop (BP, verbatim mechanics):**
- **Morale** is driven down by **war, unemployment, and poor living conditions.**
- **Low morale → colonists LEAVE.** Too many leave → the colony can no longer sustain itself → it **COLLAPSES.**
- **High morale → colonists ARRIVE** → the colony makes **more money**…
- …but **more colonists need more jobs, more housing (buildings), and more power plants** for those buildings. So growth *creates its own demand* — a self-balancing pressure, not a free ride.

**Why it's the right next thing:** it turns population from a number that only goes up into a **resource with a feedback loop** — the source of a stacking decision (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`'s one rule). It's the economic **pressure** that makes the BSG-nomad fantasy *bite* (a fleet with no planet has to manufacture morale and jobs out of ships), and it's the colony state a **ground invasion** later acts on (you take a world by breaking its morale, not just its buildings).

**Where Pulsar stands (the gap to close):**
- `PopulationProcessor` (`GameEngine/Colonies/`) **is a stub** — `GrowPopulation` has a placeholder die-off rate, `// @todo get external factors`, no morale, no jobs, no housing/power demand (see `Colonies/CLAUDE.md`).
- The pieces to build the loop on **already exist**: `ColonyLifeSupportDB.MaxPopulation` (carrying capacity), `PopulationSupportAtbDB` (infrastructure → capacity), the **component-based installation system** (jobs/housing/power are components on the colony), and `Energy/` (power gen/consumption). The loop is a *connection* job more than a from-scratch build — exactly the Prime-Directive "Connect" step.
- **Cradle-to-grave fit:** morale is the *decision* (build housing/power/jobs vs. let morale slide); the **grave rung** is colony collapse / depopulation — which is also the ground-combat objective. One system serves the economy pillar AND the invasion pillar.

---

## 2. Ship & component design with CUSTOM PLACEMENT — Pulsar already has this substrate

BP: *"develop your own armor, engines, radar, shields, and weapons, then create a custom layout ship and fit everything on there"* — placement **completely custom**, down to custom projectiles.

**Pulsar already matches the direction** — and this is the validation that the work this session sits on is the right spine:
- Component **design** (the six-point design chain), research-gated, built from materials — present.
- **Placement actually matters**: the damage system reads a ship's **component placement bitmap** (`ComponentPlacement`, `EntityDamageProfileDB`) — where a component sits decides whether a hit reaches it. That's BP's "placement is custom" with *consequences*.
- **Gap / reach:** Pulsar's placement is auto-generated, not player-authored. A player-facing **ship-layout designer** (drag components into a cross-section, armour facings) is the BP-grade reach — and it would make the damage bitmap a *player decision* instead of an engine detail. A strong space-infrastructure candidate after the morale loop.

---

## 3. Politics with teeth — the galactic senate + legislation (the B5 pillar)

BP: a **galactic senate votes on legislation**, and **legislation can only be drafted by players who control an ENTIRE star system.** (Stellaris' Galactic Community is the modern cousin — diplomatic weight from fleet/economy/tech/pops.)

**This is the developer's "reasons to declare war beyond 'they have a planet I want'"** — war as a *political act* (breaking a law, a senate vote, a bloc) rather than pure appetite. Maps onto `docs/society/DIPLOMACY-DESIGN.md` (designed, not built) as the layer **above** raw IFF/relations: a binding inter-faction rule-set with a control gate (own a whole system to legislate). **Build order:** later than the morale loop — but this is the concrete shape the B5 pillar takes.

---

## 4. Espionage as a verb — covert agents (the other half of the B5 pillar)

BP: hire **undercover agents** to run missions — **destroy a facility, assassinate a governor, steal an ally list.** Concrete, targeted, asymmetric actions that aren't fleet combat.

Maps to `People/` (commanders/agents already are entities) + the intel side of `docs/society/DIPLOMACY-DESIGN.md`. The "steal ally list" / "assassinate governor" verbs are a clean later system — **agents are people (components/entities), missions are orders, the grave rung is a caught/killed agent.** Cradle-to-grave fits the existing People + Orders spine.

---

## 5. Scale & co-op (context, not a build item)

- **Thousands of planets → systems → constellations → galaxy.** Reinforces the "fill the system" + **lemon-PC** instinct: BP ran empire-scale sim on 2008 hardware because it was *simulation, not graphics* — the same bet the developer is making (`NORTH-STAR-VISION.md` creed).
- **Multiple players, one empire** (co-op) — interesting but out of scope for a single-player 4X; note and park.

---

## What to mine, in order (ties to "finish space infrastructure first")

1. **The morale/population loop (§1)** — the prime near-term target. Turns the stub `PopulationProcessor` into a real economic pressure; it's a *connection* job on parts that already exist; and it's the colony state ground combat later acts on. **This is the strongest "finish the space economy" candidate.**
2. **Player ship-layout designer (§2)** — makes the existing placement-matters damage model a player decision.
3. **Senate/legislation (§3) + espionage (§4)** — the B5 "politics with teeth" pillar, later — but BP is the proof it works and the shape to copy.

*This is a capture/reference, not a build ticket. The next action is to read it (with `Colonies/CLAUDE.md` + `docs/aurora/COLONY-ENVIRONMENT-AND-POPULATION.md`) when the morale/population loop is scheduled, then turn §1 into locked decisions before code — exactly the Aurora-doc workflow, second source.*
