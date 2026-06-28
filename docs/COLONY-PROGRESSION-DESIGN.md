# Colony / Infrastructure Progression — Design Capture

**Status: captured vision, NOT a build spec yet.** Recorded 2026-06-28 at the developer's request — "mark this down, it becomes relevant when we get into building colonies." This is the shape of how *every* place a player or NPC sets up infrastructure should grow. Do not build from this until colony work is on the slate and the MVP firewall (`docs/MVP.md`) allows it; this is the north star, not the next ticket.

---

## The one principle

**Every infrastructure site — player OR NPC — grows through a progression.** A site is never just "placed and done." It climbs a ladder, and **each step up costs more but yields more *if done right*.** Growth is a decision with a payoff and a price, not a free upgrade. The whole ladder **scales** — costs and yields both climb as you go up.

So infrastructure is a *cradle-to-grave* economic decision (mirrors the combat/hazard cradle-to-grave discipline): you invest to grow a site, you get more out of it, and a site you over-extend or mismanage costs you.

---

## The ladder (lowest → highest)

```
Outpost ──(populate)──▶ Colony ──(by flavor)──▶ World ──▶ Minor ─▶ Hub ─▶ Major ─▶ Capitol
  │                       │                        │
  automatable             has FLAVORS         the flavor decides
  (the ONLY tier          (multiple kinds)    which World path it
   that can be                                 can take
   automated)
```

1. **Outpost** — the entry rung, where every site starts.
   - **The ONLY tier that can be AUTOMATED** (set it and let it run; no hands-on management required). This is the key distinction: automation is a *low-tier* affordance, traded away as a site grows into something that demands real management.
   - Once it becomes **populated**, it can be progressed into a **Colony**.

2. **Colony** — the populated, managed tier.
   - **Has multiple different FLAVORS** (specialisations/types — TBD). The flavor you grow it into is a meaningful choice.
   - **The flavor determines which path it can take** toward World status.

3. **World** — the site has become a full world.
   - From World it progresses down a path of increasing stature:
     - **Minor World**
     - **Hub World**
     - **Major World**
     - **Capitol World**
   - Each is a bigger investment and a bigger payoff than the last (scaling).

---

## What's locked vs. open (so we don't over-design now)

**Locked (developer's words):**
- Universal growth process for all infrastructure (player + NPC).
- Cost↑ / yield↑ per step, *"if done right"* (reward for good play, not a free ride).
- Outpost is the only automatable tier.
- Outpost → (populate) → Colony → (flavor) → World → Minor → Hub → Major → Capitol.
- Everything scales.

**Open questions to answer WHEN we build it (parking lot — do not invent answers now):**
- What are the **Colony flavors**, and how does each gate the World path?
- What does **"done right"** mean mechanically — what's the skill/decision that turns growth into *more* yield vs. wasted cost (siting? resource match? infrastructure mix? timing?)?
- What **triggers/gates** each progression (population threshold? built infrastructure? research? funds? time)?
- What exactly **"populated"** means for Outpost → Colony.
- How **automation** at the Outpost tier actually behaves (NPC-style auto-management), and why higher tiers can't be automated (the management *is* the gameplay).
- How **NPCs** drive the same ladder (ties into the thin faction-AI layer — see `GameEngine/Factions/CLAUDE.md` and the NPC-AI parking item in the hazard foundation map).
- The **loss/grave rung** (cradle-to-grave): what happens when a high-tier world is bombarded/blockaded/loses population — does it *demote* down the ladder?

---

## Where this plugs in (Prime Directive — map before building)

This is the planetary/economic mirror of "give ground/infrastructure the depth space combat has" (root `CLAUDE.md` → Developer Objective). When it's built it will touch:
- **Colonies** (`GameEngine/Colonies/`) — the site state + population + life support; the tier lives here.
- **Industry / Production** (`GameEngine/Industry/`) — the cost↑/yield↑ scaling per tier; what a site can build/produce.
- **Galaxy / bodies** (`GameEngine/Galaxy/`) — the body a site sits on (siting = part of "done right").
- **Factions / NPC AI** — NPCs climb the same ladder; outpost automation is NPC-style management applied to the player's low tier.
- **Research/Tech** — likely gates flavors / higher tiers (cradle-to-grave: research → unlock a tier/flavor).
- **UI** — `ColonyManagementWindow` already has the economy tabs; the tier + progression controls would extend it.

**Cradle-to-grave check (the acceptance test when built):** a player must be able to reach each rung through the whole chain — the minerals/materials to grow it, the decision to progress (and the cost), the yield it pays back, and the loss if it's taken/demoted. A tier that's just a label with no decision behind it fails the test (the "pretty, not a decision" anti-pattern, `docs/REALISM-VS-GAMEPLAY-AUDIT.md`).

---

*This doc is a capture. The next action on it is NOT to build — it's to read it (plus `docs/aurora/PLANETARY-INFRASTRUCTURE.md`) when colony progression is actually scheduled, then turn the open questions above into locked decisions before any code.*
