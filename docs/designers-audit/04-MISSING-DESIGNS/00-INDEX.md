# 04 — DESIGN THE MISSING (Phase 4 index)

> **What this is:** the audit found things the eleven designers don't cover but the connected system needs.
> This folder designs each one at the **same standard as the existing designers** — the DESIGNER NORTH STAR
> method (`docs/economy/DESIGNER-NORTH-STAR.md`): a door is **derived** from the sim variables the game
> actually reads, not invented; every dial writes a real variable (or costs mass); the **intrinsic test**
> separates a component dial (settable knowing only this part) from an assembly decision or an emergent
> readout; and every design **reproduces** what already exists before it adds anything. Each design is a new
> file here — **never an edit to the eleven.**
>
> These are the E-build items from `03-CORRECTION-PLAN.md` that are genuine *missing mechanisms* needing a
> designed door/dial — not the pure wiring fixes (colonist-unload path, firepower re-scale, dock order),
> which are engine work the plan already specifies.

## The five missing designs

| # | File | Answers (from Phase 2/3) | Why it needs a real design |
|---|------|--------------------------|----------------------------|
| 1 | `01-POWER-ECONOMY.md` | C12 (no generic power-draw), C15 (shields free), E-build-11 (no colony power) | The single most-connected gap: a generic `PowerDraw` dial every active part gets, netted against reactor output, plus a colony-installable power producer. Changes the whole power/morale economy. |
| 2 | `02-SETTLING-A-WORLD.md` | C3 (passengers don't reach population) | "Take a planet" and "found a colony" both need colonists to *move and arrive*. Today nothing reads carried colonists into a colony's population. A cradle-to-grave population-movement design. |
| 3 | `03-EMPLOYMENT-MODEL.md` | C1 (jobs producer) + the denominator trap | Jobs can't just be seeded — `employmentRatio = jobs/(pop×0.5)` makes a fixed number read as mass unemployment. The employment *model* needs deriving so the loop is coherent and neutral-safe. |
| 4 | `04-HEAT-AND-SIGNATURE.md` | C6 (drive-heat absent) + thrust-signature shape | A coherent "what makes a unit loud/hot" model: drives emit heat like weapons, signature scales with load/burn, and the designer shows loudness as a readout, not a fake dial. |
| 5 | `05-COMMAND-AGENCY.md` | C8/C9 (seat occupancy cosmetic) | Backbone D's gap: a seated leader must *drive* a real rate. The competence machinery exists (flagship combat bonus); this design routes seat occupancy into economy/research/industry the same way. |

## Method applied to every file (the North Star checklist)

Each design file carries the same spine:
1. **The input surface** — the exact sim variables this design can WRITE, with `file:line` and the reader
   (verified in source by the Phase-4 input-surface pass, not asserted).
2. **What already exists** — the partial coverage to *extend*, not rebuild (most of these are 70% built).
3. **The doors (forced choices) and dials (free sliders)** — derived from the variables, with the
   **intrinsic test** applied to each (dial vs assembly vs emergent).
4. **The price** — mass/crew/cost, so no capability is free (the "abilities are components" law).
5. **Worked reproduction** — the design rebuilds what already exists before adding anything new.
6. **The cradle-to-grave chain** — mineral → material → component → research → unit → decision → loss.
7. **The gauge** — the test that proves it, and the blast radius (carried from Phase 3).

## Status: COMPLETE (all five authored)

All five designs are written, each grounded in verified source (`file:line`) from the Phase-2 audit and my own
direct reads. *(The Phase-4 input-surface agent pass hit a session usage limit and returned nothing; I
authored from the Phase-2 ground truth + targeted direct reads instead — the North Star "verify in source"
step was done by hand.)*

**The through-line across all five:** every one is **~70–80% already built** — the door exists, or the
consumer exists, or the competence machinery exists. What's missing is almost always the **one connecting
wire** (a generic draw, an unload order, a scaled dial, a heat coefficient, a competence fold), not a system.
That is the Phase-2 "built-but-dormant" theme carried into design. And every design **stacks** on the others:
the power economy (1) is what makes cryo settling (2) a real risk and heat/signature (4) a real tradeoff; the
employment model (3) and command agency (5) are the two halves of the colony/people loop. None is an island.
