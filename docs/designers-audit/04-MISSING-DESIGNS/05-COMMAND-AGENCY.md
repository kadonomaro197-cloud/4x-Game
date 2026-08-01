# Missing design 5 — COMMAND AGENCY (a seated leader who actually drives something)

> **What it is, in one breath:** you can build an admin complex, it creates command **seats**, and you can
> **seat a leader** in one — and then *nothing happens.* Occupying a governor's seat, a yard master's seat, a
> research director's seat changes **no rate, no bonus, no outcome** (cracks **C8/C9**). The only place a
> leader's skill actually matters is the **flagship** of a fleet in combat — a completely separate wire. This
> design closes Backbone D: it routes a seated leader's competence into the system the seat governs, using the
> **exact pattern that already works** for the flagship. It makes "put your best administrator on the
> homeworld" a real decision instead of a cosmetic label.
>
> **Honest scoping:** the *door already exists* (the Command designer's Admin-Level ladder / seat types), and
> the *competence machinery already exists* (the flagship combat bonus). The missing piece is the **output** —
> what a seated occupant writes. This is a "wire the consumer" design, but load-bearing: without it, the whole
> delegation layer the vision rests on is inert.

---

## 1. The input surface (verified in source)

**The competence loop that ALREADY drives outcomes (the pattern to copy):**
| Piece | Evidence |
|-------|----------|
| Competence generator | `People/CommanderBonuses.cs` — `RollCombatCompetence`; caps `MaxCombat/Research/EspionageCompetenceBonus` = 0.15 / 0.15 / 0.6 |
| Flagship combat wire (works) | `CombatEngagement.cs:1697-1706` — `FleetCommanderMult` reads the flagship's `ShipInfoDB.CommanderID` → competence → firepower/toughness |
| Site-work wire (works) | `SiteWorkProcessor.cs:280` — `Grade × (1 + leaderSkill + Support/100)`, `leaderSkill` from the berth's `CommanderID` |
| Governor competence hook (consumer EXISTS) | `LegitimacyDB.cs:100-101` — `GovernorCompetence` → governor legitimacy bonus |

**The seat surface that is INERT (the gap):**
| Piece | Evidence |
|-------|----------|
| The seat + its occupant | `AdminSpaceAbilityState.CommanderID` (set by `AssignAdministratorOrder.cs:79`) |
| The scope label | `SeatType` / `AdminLevel` — **no rule reads it** to enforce span (C8, grep clean beyond seat-management) |
| What reads the occupancy | **only seat-management** (assign/unassign/vacate) — no economy/research/industry rate reads `AdminSpaceAbilityState.CommanderID` (C9) |

**The one-line gap:** the flagship's `ShipInfoDB.CommanderID` drives combat; the admin seat's
`AdminSpaceAbilityState.CommanderID` drives **nothing** — and `LegitimacyDB.GovernorCompetence`, a consumer
that's *already wired to receive it*, is never fed from the seat.

---

## 2. The design — every seat writes a competence bonus into the system it governs

The pattern is already proven three times (flagship combat, site work, and the governor hook waiting to be
fed). Generalize it: **a seated occupant's competence folds a bonus into the governed system's rate, exactly
as the flagship folds into combat.** One shape, every seat:

| Seat (from the Command designer's ladder) | Governs | Writes into (consumer) | Status of the consumer |
|-------------------------------------------|---------|------------------------|------------------------|
| **Planetary Governor** | colony order + economy | `LegitimacyDB.GovernorCompetence` → legitimacy; tax/economy rate | **consumer exists** (`LegitimacyDB.cs:100`), just unfed |
| **Yard Master** | a colony's industry | an industry-rate competence modifier on `IndustryTools` rates | hook to add (mirror the infra-efficiency scale at `IndustryTools.cs:121`) |
| **Research Director** | a colony's labs | a colony-wide research-point bonus | mirror the scientist `BonusesDB` fold (`ResearchProcessor`) at colony scope |
| **System Admiral / Fleet Commander** | fleets in a scope | the operational-command layer over the flagship combat bonus | combat competence exists; this is the *operational* seat above it |

**How it writes:** the seat's occupant already carries a `BonusesDB` (from the academy/experience path). On
assignment, its competence is folded into the governed system's rate through that system's **existing**
modifier mechanism — the same `BonusesDB`/modifier fold the flagship and the scientist already use. **No new
math** — the competence generator, the bonus caps, and the fold pattern all exist; this design *connects the
admin seat to them*.

---

## 3. What `AdminLevel` / span means (the C8 ruling)

The scope label must either enforce something or be parked. This design makes it **span of control** — an
**emergent capacity**, not a dial:
- An admin component's `AdminSpace` capacity determines **how many delegate posts one seat can hold** (how
  many subordinate governors/yard-masters/admirals report to it). A higher `AdminLevel` seat commands a wider
  span; exceeding it means a post goes **unmanned** (no competence bonus — the same "understaffed" failure the
  crew gate already models).
- *Intrinsic test:* span capacity is **emergent** from the admin component's size — it's a readout, not a
  dial. The seat's *altitude* (Ship/System/Empire) is the **door** the Command designer already offers; this
  design gives that door teeth by making the altitude cap the number of posts it can fill.

That's the "play at your own altitude" agency the governance docs call for: a capable empire-level seat lets
you hand off whole regions; a weak one forces you to micro-manage because you can't staff the span.

---

## 4. The decision this creates (why it's worth building)

Today leaders are cosmetic outside combat. With this design, **assigning your best people becomes a real
allocation problem:**
- Put your ablest administrator on the homeworld (max legitimacy/economy) or on a fragile frontier colony
  (where the competence matters more)?
- Spend a scarce skilled officer as a Yard Master (faster ships) or a Research Director (faster tech)?
- Build a wide-span admin complex so one seat covers a region, or many narrow seats you must each staff?

Every one of these is a decision that *stacks* — leaders become a finite resource you deploy, exactly like the
People-as-a-resource design intends, and the delegation layer (the anti-"feels like a job" valve) finally has
something to delegate.

---

## 5. Cradle to grave

**people** (trained at an academy — the Civic door, research-gated) → become **officers** with competence
(`CommanderBonuses`) → **seated** in an admin component (built from materials, installed on a colony) → the
**decision** (who governs what, at what altitude, and is the span staffed?) → the seated officer's competence
**drives** the governed system's rate → the **admin component is destroyed** (a decapitation strike) → the
seat vacates, the competence bonus vanishes, the span collapses and posts go unmanned: the grave rung that
wires command to the damage system. Every rung exists except the "drives the governed rate" middle — which is
this design.

---

## 6. Gauge & blast radius

- **Gauge:** a colony with a high-competence governor seated shows higher legitimacy/economy/industry than an
  identical colony with an empty (or low-competence) seat; exceeding a seat's span leaves a post unmanned with
  no bonus.
- **Blast radius:** additive where the consumer already exists (feeding `GovernorCompetence` is a new writer to
  an existing field). The industry/research hooks are new modifier folds — gate each behind its own step and
  verify a default game (empty or auto-seated seats) stays byte-identical until a player deliberately assigns.
  Depends on the **E-rule-3** span ruling (§3) being decided first, so the same number isn't read two ways.

---

*Design 5 of 5. Phase 4 complete — five missing mechanisms designed at the North Star standard. Next: Phase 5,
walking the whole system through classic 4X situations to see where flow breaks.*
