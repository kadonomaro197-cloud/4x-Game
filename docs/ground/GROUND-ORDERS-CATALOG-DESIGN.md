# Rich Order Catalog — Design & Roadmap (Fleets + Ground Formations)

> **⚠ The GROUND order set is FIXED, not open-ended — corrected by the 2026-08-09 ledger (canon: `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` M18).** The O3+ six-order ground roadmap below (move-to-hex / attack-hex / dig-in / garrison / bombard-support / load-to-transport) is superseded: the ground vocabulary is **THREE battalion orders (MOVE · RAZE · SET DOCTRINE) + two transport (load/land)**. Garrison is the **default** (no order, no timed hold); SetStance + SetEngagement fold into **SET DOCTRINE**; there is **no separate attack/seize order** (M16 derives capture from standing on the tile). The rich-catalog ambition still governs **fleet** orders; the ground half is closed. *(The FLEET roadmap below is unaffected.)*

**What this is:** the plan to grow the game's order vocabulary from the handful of commands you can give a fleet or formation today toward the ~60 orders Aurora 4X has. Today you can only give a fleet or formation a small handful of commands; the goal is the full Aurora-style menu — move, waypoint, patrol, follow, escort, hold, picket, load/unload cargo and troops, refuel, join/detach, survey variants, and the ground equivalents. This is the order-catalog half of the old combined hex+orders initiative — the only live forward plan for orders. It is built the usual way: engine-first, one CI-verified slice at a time.

**Consolidated 2026-07-13 from:** `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the Order catalog track — the hex-map half of that doc lives in `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`).

---

## Where things stand — the order survey (2026-07-04)

Before building, here is what actually exists in the order plumbing today. These are the findings that shape the roadmap:

- **~~~51 `EntityCommand` classes exist, but only ~5 are reachable~~** — ⚠ **RE-COUNTED 2026-07-28: 46 classes exist, and 23 DISTINCT command types are reachable somewhere in the client** (cargo, colony, station, survey, troop lift, fire control, warp/Newtonian movement, on-site construction, logistics…). The *"~5"* was true only of the **fleet "Issue Orders" tab** specifically, which is still a hardcoded `switch`, not a registry. **Half the commands are reachable, not a tenth** — the registry argument stands, but the headline overstated the gap by ~4×.
- **No true multi-order queue / waypoint chaining.** You can't tell a fleet "go here, THEN there, THEN do this" as a real sequence.
- **The conditional / standing-order framework has exactly ONE condition** (`FuelCondition`). That's the entire vocabulary for "do X when Y happens."
- **Ground formations don't go through the order pipeline at all** — they're driven by direct `GroundForces.*` calls, not `EntityCommand`s.
- **`INavAction` is doc-only;** the nav-actions (`RefuelAction` / …) are stubbed `EntityCommand`s that don't do anything yet.

---

## Roadmap — Order catalog track (O)

### O1 — the order-catalog framework

Three pieces:

1. A **data-driven order registry** — an order descriptor (name, category, which entities it applies to, its target/param shape) so the "Issue Orders" UI is *generated* from the registry, not written as a 60-case `switch`.
2. A **real multi-order queue / waypoint chain** per fleet AND per formation — sequential "then" semantics (do this, then that), not just the current action-lane masking.
3. **Bring ground formations into the `EntityCommand` pipeline** — either a `FormationOrderableDB`, or route formation orders through the colony / body.

**✅ O1a BUILT 2026-07-04 — the formation ORDER QUEUE (ground side, the biggest gap).** *Build state: built-live (engine + tests green); client order-queue UI not yet built.* Ground formations had NO order pipeline at all (direct `GroundForces.*` calls); now `GroundFormation.Orders` is a real **sequential "then" queue** (`GroundOrder`: MoveToHex / MoveToRegion / HoldFor / SetStance / SetEngagement) run one-at-a-time by `GroundForcesProcessor.ProcessFormationOrders`. So you can queue **"move to London, THEN Paris, THEN dig in"** and it executes over ticks — the true waypoint chain that the fleet action-lane model can't express. Behavior rules:

- A **move** pops when the leader idles (arrived OR stuck → it never wedges).
- A **hold** counts down.
- A **stance / ROE** order applies instantly.
- A queued plan **overrides the auto-ROE** (see the ground engagement-stance / ROE work, which lives with the ground-surface map design).

API: `QueueFormationOrder` / `SetFormationOrder` / `ClearFormationOrders`. **Design choice:** a formation-level data-object queue (the doc's sanctioned "a `FormationOrderableDB`" approach), consistent with the ground layer's deliberate data-object formations — they aren't entities, so their orders aren't `EntityCommand`s. Gauge: `GroundForcesTests.Orders_*`.

**Next O1 slices (not yet built):**
- The **client order-queue UI** — add-waypoint / list / clear on the formation panel.
- The **fleet-side registry** — generalize the hardcoded fleet "Issue Orders" switch + add fleet waypoint chaining.
- The data-driven order **descriptor registry** for a generated UI.

### O2 — condition vocabulary

*Not built.* Grow `ICondition` beyond the single `FuelCondition`: location / proximity, cargo, health, enemy-detected, time/date, fuel/ammo. This is the substrate for Aurora-style conditional standing orders ("do X when Y is true").

### O3+ — order batches toward ~60

*Not built.* Fill out the catalog in batches. Each batch is one slice: descriptor + execute + UI.

- **Fleet / general:** move / waypoint / patrol / follow / escort / hold / picket; load / unload cargo + colonists + troops; refuel / resupply (finish the existing stubs); join / detach / transfer; survey variants.
- **Ground:** move-to-hex / attack-hex / dig-in / garrison / bombard-support / load-to-transport.

---

## ~~Open decision for O1 (flagged, not yet locked)~~ — 🔒 **CLOSED BY CANON, 2026-07-28. The old "lean" is now FORBIDDEN.**

The question was: do the combat / doctrine / EMCON / ground *direct-call* actions join the `EntityCommand` pipeline, or
stay direct calls? The old lean was **keep direct execution, add descriptors for the UI only** — so a commander could
bypass the engagement lock mid-battle.

**That is exactly the failure the developer's law forbids.** Root `CLAUDE.md` → **One Verb, Both Seats**: *if the AI
cannot use a mechanic with the same primitive the player uses, the mechanic is too complex.* A direct call with a
UI-only descriptor is **two parallel paths for one verb** — a rich path for the player and none for the AI, because the
**AI issues orders through the queue.** It is the same defect as the client's click-to-march
(`GroundForces.OrderMoveToGlobalHex` called directly: no issuer marker, unsequenceable, AI-invisible), which the
2026-07-28 movement rulings delete for precisely this reason.

**RULING: they JOIN the pipeline.** One order object, one queue, one issuer marker, both seats. If a mid-battle
reaction is needed, that is a **priority/pre-emption property of an order** (the queue already has `Issuer` and the
brain already refuses to touch a Player queue) — **not a second execution path.**

> **And M9 applies:** ground orders are issued from the **Force Management window and nowhere else**. The planet view
> keeps selection + display (**M13**) but loses its duplicate order buttons.

---

## Prime-Directive connections

- **Orders (`Engine/Orders`)** — the catalog framework (O1) generalizes `EntityCommand` / `OrderableDB` and pulls ground formations into the pipeline. `FleetDB.StandingOrders` + `ConditionalOrder` are the standing-order base to grow (O2).
- **Client** — the fleet "Issue Orders" tab becomes registry-driven (O1); the formation panel gains an order-queue UI (O1a follow-on).
- **Ground formations** — driven today by direct `GroundForces.*` calls; O1a already gave them a real `GroundFormation.Orders` queue. The remaining ground/hex movement, ROE, and surface-map work is tracked in `docs/ground/GROUND-SURFACE-MAP-DESIGN.md`.

**Do NOT** rebuild the "Issue Orders" tab as another hardcoded switch — the whole point of O1 is to make it registry-driven so the ~51 existing commands become reachable.
