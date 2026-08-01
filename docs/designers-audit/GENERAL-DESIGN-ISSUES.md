# Generalized design issues — the repeatable problems the franchise litmus tests surfaced (2026-08-01)

> **What this is:** the Blood Angels marine and the Venator Star Destroyer builds each hit a set of walls.
> This file **generalizes** those specific walls into legitimate, repeatable **classes of design issue** —
> the ones that will show up again in *any* future design, not just these two. Each issue gets: the general
> statement, where it showed up (the concrete examples), **why it recurs**, a **one-line check** you can run
> on any new design to catch it early, and the **general recommended fix.**
>
> **Why bother generalizing:** a bug found on a Space Marine that stays "a Space Marine bug" gets fixed once
> and recurs forever. Named as a *class*, it becomes a checklist item — the same way the root `CLAUDE.md`
> Landmine Index turns one-off traps into a scannable list. Several of these line up with the interconnection
> audit's five themes (`docs/designers-audit/02-OUTPUT-READABILITY-AUDIT.md §1`); those cross-refs are noted.

---

## How to use this

When you design **anything** — a unit, a ship, a building, a component — run the **checks** in §"The
checklist" against it. Each check is the generalized form of a real wall these two builds hit. A "yes" to any
check is a design issue to resolve or consciously accept before you build.

---

## The issues, by family

### Family A — Structure & multiplicity (what an entity *is*)

**G1 — The atomic-unit assumption (no multiplicity / sub-element model).**
- **General statement:** an entity is a single capability+HP scalar, not a countable collection of like
  sub-elements that live and die independently.
- **Showed up as:** the marine can't be a *6-model squad* (one HP bar, no `ModelCount`); the Venator's
  *fighter complement* is the same problem from the ship side (a bay of craft, not a countable wing that
  attrits).
- **Why it recurs:** *any* "N of a thing that dies one at a time" hits it — an infantry squad, a fighter
  wing, a gun battery, a flotilla, a swarm. It's the most cross-cutting issue on this list precisely because
  it showed up on *both* a ground unit and a ship.
- **Check:** *"Is this entity really one thing, or N of a thing that should attrit individually?"*
- **Fix:** a generic `Count`/`ElementsAlive` on the entity + per-element attrition in the resolver + an
  assembler concept of "this design fields N elements" (today `count` means parts-per-unit, not
  elements-per-unit). One primitive serves squads *and* fighter wings.

**G11 — Aggregation hides individuation (like sub-parts merge; per-instance behavior is lost).**
- **General statement:** for performance, like sub-components/units are bucketed into one summed profile, so
  per-instance state (this turret knocked out, this weapon's own range/nature) disappears.
- **Showed up as:** ground weapons blend to **one summed Attack + one dominant nature** (a bolter-and-chainsword
  marine loses its two profiles); ship turrets bucket by class (no named DBY-827 that can be individually
  silenced).
- **Why it recurs:** every multi-weapon / multi-module entity hits it. *This one is a deliberate performance
  tradeoff* (the bucketed resolve is what makes 100s-of-ships battles cheap) — so the issue is knowing *when*
  the aggregation costs a decision the player cares about.
- **Check:** *"Does merging these like-parts erase a distinction the player would act on?"*
- **Fix:** aggregate for the strength-math, but keep a **per-instance layer where the decision lives** — e.g.
  per-weapon `WeaponProfile` resolution (the ship side already does this by class; the ground side should).
  Individuate exactly where it matters, aggregate everywhere else.

**G12 — Scale as an emergent scalar, not a modeled dimension** *(judgment call, not a clear defect).*
- **General statement:** an entity's size is emergent from its mass, so nothing can key off *size* independent
  of *mass*.
- **Showed up as:** the Venator's "1,137 m" is just a large mass budget; a bigger hull isn't easier to detect
  or hit *by virtue of being big*.
- **Why it recurs:** any size-dependent effect (big-target-easier-to-hit, capacity-by-volume, silhouette).
- **Check:** *"Does any mechanic need to read size independently of mass?"*
- **Fix:** usually **none** — mass is a fine proxy; only add an explicit size/length axis if a mechanic
  genuinely needs it. Flagged for honesty, not urgency.

### Family B — Cost & scarcity across the whole lifecycle

**G2 — Build-cost without hold-cost (creation is priced, existence is free).**
- **General statement:** the economy charges to *create* an entity but nothing to *keep* it — no standing
  upkeep, no consumption while it exists, no per-operation cost.
- **Showed up as:** a fielded marine costs nothing and drains no ammo (ground has no upkeep step); the
  Venator's *shields and sensors run for free* (no operate-power cost — audit **C12/C15**).
- **Why it recurs:** any standing asset — idle fleets, garrisons, stations (only partly billed), a running
  sensor, a held shield. The economy models the *verb build* but not the *verb keep/run*.
- **Check:** *"Once this exists, does it cost anything to stand there or to operate? If not, should it?"*
- **Fix:** a **standing-cost model** in two layers — a per-entity upkeep bill (copy
  `StationUpkeepProcessor.BillUpkeep` into the entity's process tick) **and** a per-component *operate*-draw
  (the generic power-draw, audit **Design 1**) — plus wiring consumption (ammo drain) into the resolvers.

**G10 — No finite non-material stock, no per-design cap (scarcity beyond materials + time).**
- **General statement:** buildables draw *materials + time* but not a finite **non-material** input (people, a
  gene-line, a license) and have no per-type ceiling.
- **Showed up as:** the marine draws **zero population/gene-seed** (the crew/manpower gate is ship-only) and
  has no hard cap — so six "irreplaceable" marines are actually re-queueable from steel.
- **Why it recurs:** anything that should be people-limited or hard-capped — crewed units, elite formations,
  unique hulls, wonders.
- **Check:** *"Should this draw on a limited pool of people or a scarce non-material stock, or be capped in
  number?"*
- **Fix:** extend the crew/manpower gate to **all** buildable classes (it's `is ShipDesign`-only today) + a
  generic **finite-stock consumable** (a non-material input a build draws down and that refills slowly) + an
  optional per-design build cap.

**G9 — Missing loss-accounting (destruction is a silent delete).**
- **General statement:** removing/destroying an entity publishes no event and returns/consumes nothing — the
  *grave* rung of cradle-to-grave is unmodeled.
- **Showed up as:** a dead marine is a silent `RemoveAll` (no casualty event, no gene-seed/manpower
  write-back); the audit's cradle-to-grave law names this rung explicitly.
- **Why it recurs:** any entity whose loss should *matter* — a ship fires a crew-loss event, but ground
  doesn't; a captured colony (audit ruling **T1** — what capture transfers is undecided); a destroyed
  component.
- **Check:** *"When this dies, does anything get logged, returned, or paid? Should it?"*
- **Fix:** a generic **loss event on destruction** + a write-back hook (return/consume resources, publish a
  casualty/loss event). Make the grave rung a standing engine requirement, not per-system.

### Family C — State between full and dead

**G5 — Whole-or-dead (no partial-condition state).**
- **General statement:** entities/components are at full capability until instantly destroyed — there is no
  degradation state in between.
- **Showed up as:** the marine's **power armour has no condition/maintenance** (can't wear, can't be
  field-repaired by a capped specialist); the audit **parked enhancer self-repair (C13) on exactly this.**
- **Why it recurs:** any wear / maintenance / damage-degradation / self-repair / neglect-penalty mechanic —
  on *any* entity class.
- **Check:** *"Does this need a state between 100% and destroyed?"*
- **Fix:** a generic **condition / health-fraction** on components + a degradation step + effects that scale
  with condition. A large engine change (which is why self-repair is parked) — but it's the single unlock
  behind a whole family of mechanics.

### Family D — Taxonomy & axis rigidity

**G6 — Closed capability taxonomies (a fixed enum; a new mode needs code, not a dial).**
- **General statement:** movement, weapon-delivery, damage-nature, etc. are fixed enums — a capability outside
  the set is unrepresentable without an engine change.
- **Showed up as:** no **jump/flight** locomotion (the movement enum is Foot/Tracked/Walker/Hover); no
  **thrown/area** weapon delivery (grenades).
- **Why it recurs:** any exotic capability — teleport, burrow, cloak-move, artillery arc, a new damage nature.
- **Check:** *"Is the capability I want a value the enum already has, or does it need a new one?"*
- **Fix:** treat each taxonomy as a **known extension point** — make it data-driven where feasible, and where
  not, keep a documented "add-a-mode" recipe (the enum value + its resolver branch) so a new mode is a small
  scoped build, not a surprise.

**G7 — Coupled axes that should be independent (two dials locked into one).**
- **General statement:** two design axes that carry independent meaning are hard-coupled, collapsing the
  option space.
- **Showed up as:** weapon **nature** is hard-coupled to **delivery** (Ballistic → always Kinetic), so you
  can't build a kinetic-delivery / explosive-nature round — the bolter's whole *mass-reactive* character.
- **Why it recurs:** anywhere a "pick one" secretly forces a second choice. It's the **inverse** of the North
  Star's rule ("two types with no differing variable are one type") — here two axes with *genuinely
  independent* meaning are forced to move together, and the DATA hard-codes what the DESIGN says should be
  free.
- **Check:** *"Do these two choices have to move together, or am I forcing them?"*
- **Fix:** split into two independent dials (exactly the **Nature × Delivery** matrix the re-derived Weapons
  designer already espouses — this is a case where the data lags the design).

### Family E — Wiring & cross-class parity (the audit's home turf)

**G3 — Capability parity gap between entity classes (a mechanic built for one class, not mirrored).**
- **General statement:** a feature exists for ships but not ground (or vice-versa), so equivalent entities
  behave inconsistently.
- **Showed up as:** ground has **no elite/caliber dial** (ships do); ships have **no reactor/magazine
  requirement gate** (ground does); ground penetration works, **ship penetration is hard-0.**
- **Why it recurs:** *every* new mechanic — it gets built for one class and the port to the others is
  forgotten. This is the audit's **Theme 2** (space vs ground are asymmetric; ground is often the stricter,
  more complete one).
- **Check:** *"Every entity class that shares this concept — do they all have it?"* (ship · ground · station ·
  installation)
- **Fix:** when a mechanic is added, **port it across all classes that share the concept in the same slice**;
  better, host the shared stat/gate on a **common substrate** instead of per-class code, so parity is
  structural, not remembered.

**G4 — Producer wired to a dead or wrong consumer (a dial that does nothing live).**
- **General statement:** a dial writes a real field, but that field's only reader is off the live path
  (test-only, dead code, or a differently-named consumer).
- **Showed up as:** the **firepower-caliber** enhancer writes `cv.Firepower`, which only the **test-only**
  `AutoResolve` sums — so it's inert in real combat (audit **D-gate-2**). The audit found a cluster of these
  (signature → wrong consumer, detection-trigger → wrong path, `LogiBase` → no reader). This is the audit's
  **Themes 3 & 4** (two resolvers, the tidy one is test-only; named-consumer misattribution).
- **Why it recurs:** any dial. It's the most insidious class because the design *looks* wired — the value
  reaches a field — and only tracing to the live reader reveals it does nothing.
- **Check (the two-ends rule, live form):** *"Cite the file:line of the LIVE code path that reads this dial's
  field. If the only reader is a test or a dead resolver, the dial is a no-op."*
- **Fix:** make "trace to the live consumer" a standing step in the designer method (the interconnection audit
  is the exhaustive form). A dial with no live reader is a bug, not a feature.

**G8 — Container without an operating verb (a carry component with no deploy order).**
- **General statement:** a component that *stores/carries* sub-entities exists, but the order to **deploy**
  them — and the resolve for them as an operating group — isn't wired.
- **Showed up as:** the Venator's **Docking Bay** holds fighters but has **no launch order** (`DockTools` has
  no game caller; the strike-craft-as-sub-fleet resolver is designed-not-built in `CARRIER-DESIGN.md`).
- **Why it recurs:** *every* carry-and-deploy pairing — troop bay → land (this one **works**), strike bay →
  launch (doesn't), dock → undock (doesn't), passenger hold → settle colonists (doesn't — audit **C3**). A
  container is only as done as its deploy verb.
- **Check:** *"This component carries something — what's the order that gets it back out, and is that order
  wired?"*
- **Fix:** for every container component, ship its **deploy/operate order** (and, if it operates as a group,
  the group-resolve) **in the same slice** as the container. Never land a "holds X" component without its
  "release X" verb.

---

## The checklist (run on every new design)

| # | The check — a "yes" is a design issue | Family |
|---|----------------------------------------|--------|
| G1 | Is this one thing, or **N of a thing** that should attrit individually? | structure |
| G11 | Does **merging like-parts** erase a distinction the player would act on? | structure |
| G12 | Does any mechanic need **size independent of mass**? *(usually no)* | structure |
| G2 | Once it exists, does it **cost anything to stand there or operate**? Should it? | cost |
| G10 | Should it draw a **limited pool of people / a scarce stock**, or be **capped**? | cost |
| G9 | When it **dies**, does anything get logged, returned, or paid? Should it? | cost |
| G5 | Does it need a **state between 100% and destroyed** (wear, repair)? | state |
| G6 | Is the capability I want an **enum value that exists**, or a new one? | taxonomy |
| G7 | Do these two choices **have to move together**, or am I forcing them? | taxonomy |
| G3 | Do **all entity classes** that share this concept actually have it? | parity |
| G4 | Cite the **live file:line** that reads this dial. Test-only reader = no-op. | wiring |
| G8 | This carries something — is its **deploy order** wired, in the same slice? | wiring |

---

## Severity & effort (for prioritizing the general fixes)

| Issue | Blast (how many designs it silently affects) | Effort of the general fix |
|-------|----------------------------------------------|---------------------------|
| G4 dead consumer | **high** — any dial can be a silent no-op | S per case (trace + rewire); the audit is the sweep |
| G3 parity gap | **high** — every cross-class mechanic | S–M per mechanic (port it) |
| G8 container-no-verb | high — every carry/deploy pair | M per verb (order + group-resolve) |
| G2 hold-cost | high — every standing asset | M (upkeep bill + operate-draw) |
| G1 atomic-unit | high — squads, wings, batteries | **L** (a core primitive) |
| G9 loss-accounting | medium — every destruction | S–M (loss event + write-back) |
| G10 finite stock / cap | medium — scarce/elite buildables | M–L (people gate + stock + cap) |
| G7 coupled axes | medium — every two-axis design | S–M (decouple the dials) |
| G6 closed taxonomy | medium — exotic capabilities | M per new mode |
| G5 whole-or-dead | medium — wear/repair mechanics | **L** (condition state) |
| G11 aggregation | low–medium — a conscious tradeoff | M where individuation is needed |
| G12 scale-as-scalar | low — a judgment call | M, usually skip |

**The two to institutionalize first, because they're cheap and catch the most silent damage:** **G4** (every
dial must cite a live reader — the two-ends rule made a standing method step) and **G3** (every mechanic ports
to all entity classes in the same slice). Those two are process fixes as much as code, and they stop the whole
*class* of "built but does nothing / built for one class only" from recurring.

---

*Generalized from `docs/showcase/LITMUS-BLOOD-ANGELS-BUILD.md`, `docs/showcase/LITMUS-VENATOR-BUILD.md`,
`docs/showcase/LITMUS-TO-100-ROADMAP.md`, and the interconnection audit (`docs/designers-audit/`), 2026-08-01.*
