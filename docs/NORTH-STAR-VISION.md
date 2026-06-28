# North-Star Vision — "The 4X I always wanted to play"

**Status: the directional north star. Deliberately vague — that's the point.** Recorded 2026-06-28 at the developer's request, as a realization that supersedes the narrower "fix ground combat" framing. This is the *why* behind every system. It is NOT a build spec and it does NOT loosen the MVP firewall — read `docs/MVP.md` and `docs/REALISM-VS-GAMEPLAY-AUDIT.md` before building anything. This doc tells you what we're ultimately aiming at; those tell you what we're allowed to build *next*.

---

## The statement (the developer's words)

> "I'm going to make the 4X game I always wanted to play. I won't be satisfied until I have a game where a player can play their own 4X version of Battlestar Galactica, Star Trek, Star Wars, Babylon 5, Andromeda, Mass Effect, Stellaris, Halo, The Expanse, Stargate — all of them. At least specific aspects of them. That's the objective. Is it vague? Yes — that's the point."

Ground combat is no longer the finish line. It's **one system** in a much bigger box: a 4X engine deep and connected enough that a player can stage *their own* version of the great sci-fi universes inside it.

---

## The load-bearing phrase: "at least specific aspects of them"

We are **not** building "Star Trek mode" or a BSG total conversion. We are building the **systems** — taken to real depth and *connected* — whose decisions, when stacked, let a player **create** those experiences themselves. Each universe names an **aspect** (a decision, a tension, a feel). Our job is to build the Pulsar system that delivers that aspect as a real, stacking decision — not to script the franchise.

This is the discipline that keeps a vague north star from becoming infinite scope: **a franchise doesn't earn a feature; an *aspect* earns a *system*, and a system only earns its weight if it's the source of a player decision (the audit's one rule).**

---

## The lens — each universe → an aspect → a Pulsar system (many already exist or are in flight)

The point that makes this *not* a pivot: the aspects map onto systems Pulsar **already has or is actively building.** The vision is a lens on the existing build, not a new direction.

| Universe | The aspect it names | The Pulsar system that delivers it | Status today |
|---|---|---|---|
| **The Expanse** | hard-SF Newtonian flight; railgun/torpedo/PDC combat; Earth/Mars/Belt politics; water/reaction-mass scarcity | `NewtonMoveDB` physics · the **weapon triangle** (Beam/Railgun/Missile/Flak) · factions/diplomacy · logistics | physics ✅, weapon triangle ✅, diplomacy 🟡 design |
| **Stargate** | a galaxy connected by a *network* of gates; off-world teams & bases | **jump points** + inter-system travel | exists ✅ (gate-network framing is free) |
| **Stellaris** | the 4X spine itself — pops, species traits, megastructures, empire ethics, late-game crises | the whole game · `ColonyInfoDB`/species · colony progression ladder | spine ✅, progression 🟡 captured |
| **Halo** | ground combat on fortified key worlds; AI characters; war vs a coalition | **ground combat** (the old MVP target) · commanders/people | the next frontier 🔴 |
| **Battlestar Galactica** | a fleet that must *survive* — fuel/water scarcity, no reinforcements, FTL jumps, military-civilian tension | logistics · fuel/cargo · fleets · warp/jump | logistics ✅, scarcity-as-pressure 🔴 |
| **Star Trek** | exploration, first contact, diplomacy, science vessels, a galaxy of distinct species | sensors/detection · survey · diplomacy · species generation | sensors ✅, diplomacy 🟡 design, first-contact 🔴 |
| **Babylon 5 / Andromeda** | a hub/station as a political nexus; re-connecting a scattered network; many-faction diplomacy | colonies/stations · jump network · diplomacy/IFF | pieces exist, the *political* layer 🔴 |
| **Star Wars** | large fleet battles; faction war (Empire vs Rebellion); planetary invasion | auto-resolve fleet combat · factions · orbital bombardment → ground | fleet combat ✅, invasion 🔴 |
| **Mass Effect** | a crew/squad; choices with consequences; a looming existential threat; tech/biotic loadouts | commanders/people · component-design loadouts · (a crisis system) | people ✅, choices/crisis 🔴 |

**The headline:** a surprising amount of the "BSG/Expanse/Stargate/Stellaris" substrate is *already in the engine.* The work is mostly **depth + connection** (the Prime Directive's Connect step) and a few **missing decision layers** (diplomacy-as-politics, scarcity-as-pressure, first-contact, a late-game crisis, ground combat) — not a rewrite.

---

## What this changes about how we work (and what it does NOT)

**Changes:**
- The **Developer Objective** widens: from "give ground combat the depth space combat has" → "build a 4X engine whose systems, taken to depth and connected, can stage *aspects* of the great sci-fi universes." Ground combat is now one funded system among several, not the sole endpoint.
- When we pick the next system, "which universe-aspect does this unlock?" is a legitimate tie-breaker — alongside the audit's "what decision does it create?"

**Does NOT change (the firewall still holds):**
- **MVP first.** `docs/MVP.md` still governs what ships next. A grand vision is the reason a Parking Lot exists, not a reason to empty it. "You can take a planet" is still the nearest concrete milestone.
- **Earn weight.** Every system still has to be the source of a stacking decision (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`). A franchise name is never a justification by itself.
- **Cradle-to-grave + Connect.** Every capability still has to be reachable through the whole chain (mineral → … → decision → loss) and *wired to the other systems*. The vision raises the ceiling; it does not lower the bar.

---

## How to use this doc

Read it when you need to remember *why*. When you're deciding *what next*, this doc is the horizon; `docs/MVP.md`, `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, and `docs/REALISM-VS-GAMEPLAY-AUDIT.md` are the map, the firewall, and the bar. Build one earned, connected system at a time — and check, now and then, that the stack of them is bending toward *this*.

*This is a capture. The next action on it is never "build the vision" — it's "pick the next system, name the decision it creates and the aspect it unlocks, and build it cradle-to-grave."*
