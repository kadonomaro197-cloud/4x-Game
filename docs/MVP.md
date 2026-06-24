# Pulsar4X Fork — MVP: "One Planet, Taken"

**Read this before adding anything.** This document exists for one reason: to stop the game from being
half-built forever. When a good idea arrives — and it will — it does **not** go into the build. It goes into
the **Parking Lot** at the bottom. The MVP is finished when the loop in §1 works end to end. Nothing else is
required for v1, and nothing else is allowed to block v1.

> **The finish line is movable — but moving it is a decision, not a drift.** If you want v1 bigger or
> smaller, edit §1 deliberately and write down why. Picking up a feature "while we're here" is exactly the
> 50-year trap this doc prevents.

---

## 1. The MVP, in one sentence

> **You can build a ground force on your colony, ship it to another planet, beat its defenders, and take the planet.**

That's it. If a player can do that one loop, the fork has delivered its core fantasy — ground combat with
real planetary infrastructure behind it — and v1 ships. Everything in base Pulsar (space, orbits, ships,
sensors) already exists to support it; this slice adds the missing vertical: **economy → army → invasion → capture.**

---

## 2. The MVP loop (the whole playthrough)

1. **Start** with the colony you already get. *(exists)*
2. **Run the economy** enough to produce military goods: mine → refine → build. *(engine done; needs a usable UI + the build-installations link)*
3. **Build a ground unit** at the colony, through the existing industry queue. *(new — minimal)*
4. **Load it on a ship** and move it to a target planet. *(movement exists; needs load/transport/drop)*
5. **Fight** the planet's defenders — a simple attrition resolution that produces a winner. *(new — minimal)*
6. **Capture** the planet: defenders gone → the colony changes owner. **Win.** *(new — minimal)*

If any step in that chain doesn't work, v1 isn't done. If a feature isn't *on* that chain, it isn't v1.

---

## 3. IN — the must-haves (and where they stand on the map)

| # | Must-have | Minimum that counts | Status today (`SYSTEMS-STATUS-AND-TEST-PLAN.md`) |
|---|-----------|---------------------|--------------------------------------------------|
| A | **Economy produces military goods** | Mine → refine → build installations *and* ground units, from the colony's own output | Engine ✅ (mining/refining/production gauged). Build-installations link + ground-unit build = **to do.** |
| B | **A ground unit exists as a buildable thing** | ONE unit (call it a "Ground Force" — components like ships, e.g. one infantry/armor design) with attack / defense / health | 🔴 absent — new `GroundUnitDesign : IConstructableDesign` + `GroundForcesDB` on the colony |
| C | **Transport** | Load a unit onto a ship, move it, unload it onto a target planet | 🟡 movement/cargo exist; load/drop = **to do** |
| D | **Ground combat resolution** | Attacker vs defender on one planet; attrition each tick until one side is gone | 🔴 absent — new `GroundCombatProcessor` + tests |
| E | **Win condition** | Defenders eliminated → colony `FactionOwnerID` flips to the attacker | 🔴 absent — minimal capture step + test |
| F | **A UI you can actually drive it from** | See/queue the colony economy; build a unit; issue the invade order; see the outcome | 🔴 the economy/colony UI is the weak point — reuse existing panels, don't gold-plate |
| G | **A defender to fight** | A scripted/static enemy garrison on one planet — *not* a full NPC general | 🔴 minimal seed is enough |
| H | **Every new engine system has a gauge** | A test per new system (the no-untested-combat rule) | enforced by CI + the harness |

---

## 4. OUT — explicitly deferred (the scope firewall)

These are **good** — and they are **not v1**. Building any of them before §1 works is the trap.

- Multiple unit types, unit design trees, formations, morale, supply lines, terrain, the tactical **hex-map**
  layer (`ColonyHexMapDB`) beyond the bare minimum.
- Orbital **bombardment** integrated with ground combat (the engine has partial hooks — v2).
- **Diplomacy**, multi-faction war, alliances, IFF depth.
- **Research** depth — v1 uses starting tech.
- Economy depth: the **Ledger/money** signal, trade, logistics automation, population economics beyond
  "enough population to staff the build."
- An **NPC AI** that plays the whole game — v1's defender is a static garrison.
- Combat **visualization** polish (target lines, animations).
- Save/load hardening and **balancing** passes — make it *work* before making it *fair*.

---

## 5. The build path — dependency-ordered, each step playable & gauged

Each stage leaves the game in a working, testable state. Don't start a stage until the one before it is green.

- **Stage 0 — Economy is real and visible.** *(we are here)* Engine economy is done and gauged. Remaining:
  the build-installations link (factory builds installations from refined materials) and a colony economy
  view you can actually read/queue from. → the colony grows itself.
- **Stage 1 — A unit you can build.** `GroundUnitDesign` (an `IConstructableDesign`, so the *existing*
  industry queue builds it) + `GroundForcesDB` holding a colony's units. Gauge: build one in a harness test.
- **Stage 2 — Move it.** Load a unit onto a ship, transport, unload onto a target planet's `GroundForcesDB`.
  Gauge: a unit travels colony → planet in a test.
- **Stage 3 — Fight.** `GroundCombatProcessor`: when attacker + defender units share a planet, attrite each
  tick until one side is gone. Gauge: a battle resolves to a winner in a test.
- **Stage 4 — Take it.** Defenders eliminated → flip the colony's owner. Gauge: capture flips ownership in a test.
- **Stage 5 — Drive it from the UI.** Build a unit, issue the invade order, watch the result. Live-test (§5B
  of the systems map); CI can't see the client.

When Stage 5 works, **v1 is done.** Stop. Play it. *Then* open the parking lot.

---

## 6. Parking Lot — where good ideas wait (so they don't derail v1)

Add ideas here freely. This is the pressure-release valve: writing the idea down means it's safe to *not*
build it yet. Revisit only after the §1 loop ships.

- _(seed)_ Bombardment-softens-defenders before the drop.
- _(seed)_ Multiple ground unit types (artillery / armor / infantry rock-paper-scissors).
- _(seed)_ The hex-map tactical battle (`ColonyHexMapDB` already exists as a substrate).
- _(seed)_ Money/Ledger so the economy has a P&L and an NPC can reason about it.
- _(seed)_ A real defending AI.
- _(add yours here…)_

---

## 7. How to use this doc

1. **New idea?** → §6 Parking Lot. Not the build.
2. **Starting a stage?** → open `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, read the systems it connects to, work
   those too (the Prime Directive). Design depth for ground systems lives in `docs/aurora/GROUND-COMBAT.md`
   and `docs/aurora/PLANETARY-INFRASTRUCTURE.md` — build the *slice*, not the whole spec.
3. **Stage done?** → it has a gauge (test) and the systems-map row is updated.
4. **Tempted to move the finish line?** → edit §1 on purpose and say why. Otherwise, hold the line.
