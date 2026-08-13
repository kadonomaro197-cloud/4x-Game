# Build Backlog Index — where the "what we will build" scope lives

**What this is:** a **pointer index**, not a merge. The open "engine work to build" is deliberately split across several single-owner docs (each owns one surface). This page is the one place that lists them, so a reader can see the whole fix/build scope without hunting — and without collapsing the split (the repo's process warns that a merged mega-backlog invites reactive pivoting).

> **The governing frame:** almost none of this is "rebuild." The recurring finding across every design doc is *the machine is built; the wiring and the decision-levers aren't* (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`). Most rows below are **weld an existing pipe** or **surface an existing command**; a minority are genuinely new. And `docs/MVP.md` — not these backlogs — decides what actually ships next.

**As of 2026-08-13.**

## The owned build-order docs

| Doc | Surface it owns | Its ladder |
|-----|-----------------|-----------|
| `assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md` | **The designer↔engine gaps** — dials the design tools draw but the engine hasn't welded (weld-the-pipe vs cut-the-dead-knob) | TIER 1 → TIER 4 (11 items) |
| `combat/FORCES-WINDOW-DESIGN.md` §8 | **The unified Forces / order-of-battle window** — one roster + the 123-order menu + the component-scan | S1 → S9 |
| `ground/UNITS-ON-THE-MAP-DESIGN.md` | **Units on the planet surface + trade/hauling** — select/read/move a unit, per-hex stockpile, hex-to-hex haul | U1 → T2 |
| `ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md` | **The planetary/ground loop** — observability, reachability, doctrine, the tick, the designer chain | S0 → S12 |
| `combat/CARRIER-DESIGN.md` | **Carriers + parasites** — the sortie/launch/recover chain | C1 → C5 |
| `economy/CAPABILITY-BUILD-PLAN.md` | **The AI + economy capability slices** — the NEED→buildables the utility scorer reads | its own slices |
| `ai/AI-BRAIN-BUILD-TRACKER.md` | **The faction/NPC AI** — build state per slice + the design→code socket map | its own slices |
| `assembler/SIM-DRIVEN-DESIGNER-AUDIT-2026-08-05.md` | **Sim-surfaced designer fixes** — the 11-item ranked table (its item list is a *subset* of ENGINE-WIRING-BACKLOG; this doc owns the diagnostic reasoning) | ranked 1–11 |

## The recommended "first five" (cheapest, highest-impact welds)
Drawn from the above — the ones that light up the most for the least engine cost:
1. **Employment → morale producer** — `ENGINE-WIRING-BACKLOG` TIER 2. Feeds the dead morale term; every colony, every game.
2. **Ground penetration carry-through** — `ENGINE-WIRING-BACKLOG` TIER 1. The root cause behind hand-typing unit penetration in the sim.
3. **The mil/civ classifier + the order component-scan** — `FORCES-WINDOW-DESIGN` S2 / §4.5. One small tested engine helper; unlocks the whole Forces roster *and* gives the AI parity.
4. **The 23 button-only orders** — `FORCES-WINDOW-DESIGN` §10 (DATA rows). Route commands that already exist into the one menu.
5. **Finish the 4 order stubs** — `FORCES-WINDOW-DESIGN` §10. `RefuelAction`/`ResupplyAction`/`ServeyAnomalyAction`/`ShipLogisticsOrders` issue-and-do-nothing today; finish before surfacing.

## How to close a row (the upkeep)
When a slice lands: flip its status in **its owning doc** (not here), flip any matching honesty flag in the design tool, and update `docs/DOCS-INDEX.md` — all in the same commit. This index only lists *where* the ladders live; it holds no per-row status of its own, so it can't rot.
