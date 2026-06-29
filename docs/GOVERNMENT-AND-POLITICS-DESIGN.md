# Government Types & Politics — Design (capture, 2026-06-29)

**Status: design capture, recorded 2026-06-29 at the developer's request.** The empire-wide **regime** layer that modulates how every other system plays, plus the future **popular-demands** (Stellaris-parties) layer. Companion to `docs/MORALE-AND-POPULATION-DESIGN.md` (the levers a government modulates) and the governance/delegation layer (task #23). Build AFTER the core levers exist (M3–M5) — but those levers are built *government-ready* so this slots in without rework.

> **Naming — two distinct systems, do not conflate:**
> - **Government type** (this doc) — the empire-wide regime (democracy/dictatorship/…) that sets the *rules of the game*.
> - **Governance / delegation** (task #23) — per-colony, how hands-on the player is (governors). A governor *operates a colony within* the rules the government sets.

---

## The framework: a government = coefficients + rule-overrides

A `GovernmentDB` (on the faction) carries two kinds of effect, and the engine is already built for both:

1. **Coefficient modulation** — scalar dials on existing levers. Every morale weight (and the M3–M5 levers) is a **named constant**, so the government just supplies overrides. Processors read `government?.Coeff(x) ?? default`.
2. **Rule overrides** — swap a behavior *branch*, not just a number. Processors read `government?.Rule(y) ?? default`.

**Why this works:** the levers are coefficients and the rules are identifiable branch points, so a regime re-skins the game without touching the engine. This is the "government-ready" discipline the morale/people slices have been banking (named coefficients everywhere).

**The load-bearing example (the developer's):** a dictatorship building a ship it lacks crew for. Consent governments **Block** the build (can't conscript); a dictatorship **Builds-understaffed-with-a-debuff** (conscription) until the crew fills in. That is a *rule override*, not a coefficient — which is why M3-2's crew shortage must be built as a **swappable policy** (`CrewShortagePolicy = Block | BuildUnderstaffed`, default `Block`), so the regime swaps it later with zero rework.

Likewise **discontent response** is a rule override: consent governments → **emigration** (people leave); command governments (closed borders) → people *can't* leave, so it converts to **unrest → revolt**.

---

## Starter matrix — government types × gameplay levers

A few named types for v1, each a **preset bundle** of coefficients + rules over the underlying table (so composable axes/civics can be added later). The Hive/Machine column is the deliberate edge that proves the framework's range — and it is also the **droid path** parked in task #20 (a machine empire has no morale and "builds" its populace).

| Lever | Democracy | Republic | Dictatorship | Theocracy | Hive / Machine |
|---|---|---|---|---|---|
| **Morale → output** | very high (both ways) | high | low (suppressed) | high if faith held | none / replaced stat |
| **Discontent response** | emigration | emigration | unrest → revolt | schism / unrest | none |
| **Crew shortage (M3)** | **Block** | Block | **BuildUnderstaffed (debuff)** | Block | built, not crewed |
| **Tax ceiling (M4)** | low, morale-sensitive | medium | high, extract anyway | tithe (faith-gated) | n/a |
| **Military build speed/cost** | normal, needs consent | normal | **fast / cheap** | normal | fast |
| **Research** | high (free inquiry) | high | skewed military | dogma-limited | very high |
| **Popular demands** | strong, refusal costly | medium (representatives) | weak, refuse cheap → unrest | religious demands | none |
| **Legitimacy from** | approval / elections | law / representation | stability / force | faith | n/a |

*(Numbers are illustrative directions, not final — calibration is a local-build/feel job, like the rest of the morale system.)*

---

## The "people speak out" layer (Stellaris-parties — future)

The political payoff, riding on top of morale + government:
- The population (or **factions** within it) periodically voices a **demand** — "lower taxes," "expand the fleet," "end the war," "stop colonizing death-worlds."
- The player/NPC **enacts** (buff: morale / legitimacy up) or **refuses** (debuff: morale / unrest up).
- **Government type sets the weight:** a democracy's demands are loud and refusal is expensive; a dictatorship can refuse cheaply but each refusal stacks **unrest toward a coup**.
- Connects morale × government × factions, and is the seed of the **B5 "politics with teeth"** pillar (a faction demand maturing into a **casus belli**). `docs/BEYOND-PROTOCOL-REFERENCE.md` §3–4 (senate/legislation + espionage) is the same political layer.

---

## Locked vs. open

**Locked (developer, 2026-06-29):**
- Government is a **modulator** = coefficients **+ rule overrides** (not just numbers). The dictatorship build-understaffed example is the canonical rule override.
- Built as **named presets over a general coefficient/rule table** (axes/civics can come later).
- M3-2's crew shortage is built as a **swappable `CrewShortagePolicy`** (Block default) so the regime can flip it.
- A future **popular-demands** layer (enact→buff / refuse→debuff), weighted by government type.

**Locked (developer, 2026-06-29) cont.:**
- **Regime CAN change mid-game — phased.** Tier 1 *the switch itself* is nearly free (government is a modulator; swapping its values re-skins the rules next tick). Tier 2 *player-chosen reform* (cost + cooldown + a temporary instability dip) is cheap and lands with the government substrate. Tier 3 *forced change* (revolution/coup, driven by unrest) waits on the unrest system; the *upheaval drama* (civil war / secession / coup-installs-rival) is a bigger, optional, later layer riding on unrest + demands.

**Open (one fork left — decide before building the GovernmentDB):**
- **How does the player pick a government — a fixed MENU, or DIALS underneath a menu?**
  - *Menu only:* pick a preset bundle (Democracy/Dictatorship/…). Simplest to build + balance; you only get what's on the menu.
  - *Dials under a menu (RECOMMENDED):* build the underlying knobs (authority / economic control / civil liberty / …), and ship the named types as **saved dial-settings**. Player sees the simple menu day one; the dials can be exposed later for custom + moddable governments with **zero rework**. Same "presets over a general substrate" pattern as the ship/component designer (a ship class = a saved component loadout; a government = a saved dial setting).
  - *Decision pending the developer's pick. The recommendation ships simple now and grows deep later on one foundation.*
- Exact coefficient/rule values per type (calibration — local-build feel).
- What replaces "morale" for a Hive/Machine empire (a unity/processing stat?), and how the droid/people rules differ.
- Where `GovernmentDB` lives (faction entity) and how per-colony processors read it given the GlobalManager-not-iterated trap (pass it down, or cache on the colony).

---

## Build order

Capture now; **build the GovernmentDB substrate after the core levers exist (M3-2 / M4 / M5)** so there's something real to modulate. Each of those slices is built government-ready (named coefficients + policy-flag indirection). The popular-demands layer comes after the regime substrate. Governance/delegation (task #23) is independent and can land earlier.

*This is a capture, not a build ticket. Next action when scheduled: lock the two open forks, then define the coefficient/rule table + a first two types (democracy vs dictatorship) as the minimal contrast.*
