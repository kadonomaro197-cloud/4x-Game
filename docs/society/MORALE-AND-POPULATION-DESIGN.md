# Morale & Population — Design (locked decisions + build plan)

> **BUILD STATUS (updated 2026-07-13): M1–M5a are BUILT and wired in the engine.** The core blobs and processors all exist in source: `ColonyMoraleDB` (M1 valve), `ColonyManpowerDB` (M3 people-as-resource), `ColonyEconomyDB` + `ColonyEconomyProcessor` (M4 tax→money), the M2 `EmploymentAtbDB`/`HousingAtbDB` machinery, and the M5a `MoraleInputs` power/food refactor — all in `GameEngine/Colonies/`. Live shortage gauge is `ColonySustenanceDB` (`SustenanceProcessor`). **This is code-exists-and-is-wired, NOT runtime-verified** — CI cannot run the client, so the actual in-game *feel* and the M2-data/M3-2/M5b live-wiring slices still need the developer's local build (see the per-slice notes below and `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` for live build-state tracking). **This doc is now the LOCKED design-rationale of record; the per-slice build stamps below are historical.**

**Status: LOCKED design, recorded 2026-06-29.** This is the build spec for the colony/station **morale + people-as-a-resource** loop — the keystone the developer wants finished as part of "the space economy, before going planetside." It closes five circuits the engine currently leaves open. Companion to `docs/BEYOND-PROTOCOL-REFERENCE.md` §1 (the model), `docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` (morale is the shared "manning" concept both hosts carry), and `GameEngine/Colonies/CLAUDE.md`.

Built from a 3-survey Prime-Directive pass (population loop, people→officers, ground-units/designer). The honest starting state **at the time of that survey (since largely closed — see the build-status banner above)**: **population is a one-way number**, there is **no morale anywhere** (only three unused event-name enums), **nothing draws people out** of the population, and the colony is **disconnected from money and from the power grid**.

---

## The one principle — population is a TANK, morale is the level-control valve

Each colony/station holds a **population tank** (people per species). It has:
- **Makeup (inflow):** births (the growth math already exists) + **immigration** when morale is high.
- **Letdown (outflow):** **emigration** when morale is low + deaths (starvation / power loss / bombardment).
- **Draws (the resource):** ship **crew**, **officers/captains**, **scientists**, **army units**, and **workers staffing installations**. Every body allocated to one of these is a colonist who is therefore *not* doing the others. **People are finite.**
- **Collapse (grave rung):** letdown + draws outrunning makeup drains the tank; empty → the colony collapses. This is also the **ground-invasion objective** — you take a world by breaking its morale, not just its buildings.

**Morale** is a score per colony, and it is both the **valve** (sets emigration/immigration) and a **plant-efficiency multiplier** (scales economic output and gates recruitment — a miserable population won't crew ships or raise armies). It earns its weight (the audit's one rule) because you must **spend** to keep it up, and growth creates its own demand — never a free ride.

---

## Locked decisions (developer, 2026-06-29)

1. **People-draw is HARD.** Crewing a ship / staffing a mine / raising a regiment **removes** those people from the population tank (a reserved sub-pool). Not a soft flag. Scarcity is the point.
2. **Morale granularity: per-colony** (one score per host) for v1. Per-species morale (a conquered species unhappy while founders are content) is a later expansion — it feeds the politics pillar.
3. **Build sequence: M1 → M2 → M3 → M4 → M5** (below). Morale valve first, because the people-draw (M3) needs morale to gate recruitment.
4. **Tax is a core lever.** A happy colony tolerates more tax before morale suffers; there's a moving **equilibrium** ("happy medium") that rises as you invest and falls when you neglect. Higher tax = more money, lower morale. (Lands in M4.)
5. **Employment is a MODIFIER on a LOW base, not a base-setter.** A frontier colony/station's base morale/output sits **low** — it is *not* content by default; a thriving world is *earned*. **Full employment = buff, unemployment = debuff**, stacked on that low base alongside the other levers (housing comfort, power, fair tax, a competent governor). (Implementation note: M1 uses a neutral-50 baseline only because it has no positive levers yet; M2 drops the base to "struggling frontier" once employment + housing-comfort exist as positive modifiers to climb with. The M1 valve direction is unchanged.)
6. **Agency is OPT-IN — every management decision must have a competent auto-default (the "auto-resolve for the economy").** Mandatory micro turns a 4X into a job; combat already escapes this with auto-resolve, and management gets the same escape hatch via a **governor**. See the Governance section below. This is a standing design rule: *if a management system can't be delegated to a governor, it was built wrong.*
7. **Government type is a MODULATOR (parked expansion), not a tank.** The whole loop above quietly assumes a *consent-based* government. A command economy (full communist) changes the rules: the state assigns jobs, extracts past the consent point, and closes borders — so the pressure converts from **emigration** to **unrest/revolt** plus a productivity penalty. **Therefore: build every morale input/output as a TUNABLE COEFFICIENT, never hardcoded**, so a future `GovernmentDB` can re-skin the loop (capitalist-consent / communist-command / theocracy / hive …). This ties straight into the B5 politics pillar. *Do not build now — design for it.*

---

## Governance & Delegation — agency is opt-in (the "auto-resolve for the economy")

**The principle (locked decision #6):** the player must be able to *ignore* a colony and have it run competently, exactly the way they can let the combat engine auto-resolve a battle. Too much *mandatory* agency makes the game a job. So every management lever we build (employment balance, housing, tax, production queue) ships with a **governor-automatable default**.

**The model:**
- A **governor is a person** — a commander (`CommanderTypes.Civilian`/etc., `People/`). The `AdminSpaceDB` "admin seat" is **now wired** (was a dead hook at survey time): `AdminSpaceProcessor` runs a durable `ReconcileSeats`/`VacateSeat` pass and commander competence rides on `BonusesDB` (`CommanderBonuses.CombatMultiplier`), with a decapitation grave rung via `CommanderFactory.DestroyCommander`. What remains for *this* pillar is the morale-nudge consumer — a processor that reads the seated governor's competence and applies it to the host's morale.
- **Competence sets the auto-standard.** Great governor → morale held up, tax at the happy-medium, housing built before crowding bites (hands-off and it thrives). Mediocre → keeps it running, leaves potential on the table. None → coasts at the low base.
- **Three involvement levels, per world:** *delegate* (pick a policy — keep-morale-stable / max-production / grow-population — and walk away), *fine-tune* (override a few decisions for better-than-auto), or *micro* (hands-on a key world).
- **Governors are doubly scarce:** *people* (finite, drawn from population in M3) and *talent* (good ones rare/trained). "Where do I put my best governor" is a real decision; you physically can't hand-run 50 worlds — which is what makes delegation the default, not a crutch.
- **Reconciles the progression ladder** (`docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md` "Outpost = the only automatable tier"): automation is **always** available via a governor; what scales with size is *how much potential a poor/no governor wastes*. Outpost barely needs one; a Capitol with a bad governor underperforms badly. Same system, scaling stakes.

**Connects to:** People/commanders (the governor), `AdminSpaceDB` (the seat — now wired by `AdminSpaceProcessor`; the morale-nudge consumer is what's left), morale (what a governor maintains), tax/M4 (governor sets the happy-medium), the audit's anti-"job" rule. **Build order:** the governance *layer* is its own slice (after M2 gives it levers to drive), but **M2/M4 must be built delegation-ready** — each decision exposed as a policy a governor can set, not a click the player must make.

## The connection map (Prime Directive — what morale touches)

| System | Relationship | Status today |
|---|---|---|
| **PopulationProcessor** (`Colonies/`) | morale recalced here (`ColonyMoraleDB`); migration applied at the external-factors hooks | growth + morale wired (M1) |
| **Carrying capacity** (`PopulationSupportAtbDB` — live via `GetPopulationSupportValue`; live shortage gauge `ColonySustenanceDB`) | M2 `HousingAtbDB` raises the ceiling. NOTE: `ColonyLifeSupportDB` is **DEAD** — never attached by `ColonyFactory`, its `ReCalcMaxPopulation` registration in `RecalcProcessor.cs` is commented out; do not use it. | split machinery built (M2) |
| **Species environment cost** (`SpeciesDBExtensions.ColonyCost`) | the "conditions" morale input | exists ✅ |
| **People** (`CommanderFactory`, `NavalAcademy`, `ResearcherDB`) | officers/captains/scientists — M3 `ColonyManpowerDB` gives the talent-pool draw | draw machinery built (M3-1); enforcement wiring needs local build |
| **Ships** (`ShipDesign.CrewReq`) | crew number — M3 bulk-pool draw; the enforcement gate (block-on-shortage) is the M3-2 slice | machinery built; enforcement needs local build |
| **Energy** (`Energy/`) | fully built, entity-agnostic; M5a added power-shortage as a morale input (neutral when absent), M5b attaches the blob to colonies | M5a built; M5b wiring needs local build |
| **Money** (`FactionInfoDB.Money` / `Ledger`) | M4 wires colony output → income via `ColonyEconomyProcessor`, scaled by morale, with the tax lever (`ColonyEconomyDB.TaxRate` → `TransactionCategory.ColonyTax`) | built (M4); CI/local pending |
| **Ground units / unit designer** | the army consumer of the people resource; mirrors the ship designer (own track) | ❌ absent |
| **Government** (future `GovernmentDB`) | modulates all morale coefficients + draw/migration rules | ❌ parked |

---

## Cradle to grave (passes the test)

> mineral → material → **housing / power / food / jobs modules** (components — designed in the designer, research-gated, built from materials) → installed on the host → **morale** is the running state → the **decision** is build-vs-let-slide (and **set the tax rate**) → the **grave rung** is collapse / depopulation (= the ground-invasion objective).

One system serves the **economy** pillar, the **BSG-nomad** pillar (a planet-less fleet must manufacture morale and jobs out of stations), and the future **invasion** pillar.

---

## The build slices (each small + CI-provable)

### M1 — MoraleDB + the valve  *(BUILT — `ColonyMoraleDB` in `GameEngine/Colonies/`, recalced by `PopulationProcessor`)*
- New `ColonyMoraleDB` (host-agnostic blob; attach to colony now, station later — the shared "manning" concept).
- Morale (0–100, 50 = neutral) recalced each population tick from inputs that **already exist**: **conditions** (`ColonyCost`) and **overcrowding** (pop vs capacity). All weights are **named coefficients** (government-ready).
- Output: a **migration rate** added to growth — morale < 50 → emigration, > 50 → immigration. Wires into the two `@todo: external factors` hooks.
- A `Factors` breakdown dict on the blob = the **gauge** (so the player/tests can see *why* morale is what it is).
- Gauge/test: morale math is a pure helper (deterministic unit test: hostile/crowded → low → negative migration; hospitable → high). Integration: the starting colony gets a MoraleDB, morale is high on Earth, no emigration.

### M2 — split jobs & housing (the two requirements the developer named)
**Build split:** *(A) machinery* (built — `EmploymentAtbDB`, `HousingAtbDB`, `GetTotalJobs`/`GetHousingComfort`, the two-sided employment + comfort morale factors, all neutral-when-absent so the starting colony can't regress; unit-tested) and *(B) data + base-low* (pending, task #25 — put real job/housing numbers on installation JSON templates and drop the base to "struggling frontier", guarded by `BaseModIntegrityTests` + the `MoraleTests` integration check; best verified on the developer's local build).
- **What makes jobs:** every *productive* installation (mine/factory/refinery/lab/shipyard) carries a **worker demand** (`EmploymentAtbDB`). Total jobs = sum of slots. **Two-sided:** people > jobs → **unemployment** → morale down; jobs > people → **labor shortage** → installations run **under-staffed** → output drops. (Hard draw from M3 means workers are actually pulled from the tank.)
- **What makes housing worth a damn:** `HousingAtbDB` raises the **population ceiling** AND reduces **overcrowding** (a morale input). No housing → low ceiling → overcrowding → emigration → labor/tax/recruitment pools all stall. Build housing → ceiling rises → more workers, soldiers, taxpayers. **Tiers make it a design decision** (not a rubber stamp): cheap-and-cramped (capacity, mild morale penalty) vs expensive-and-comfortable (capacity + morale bonus). Housing costs materials/power/upkeep and produces nothing directly — so it trades against productive installations. That tension is the city-builder core.

### M3 — people as a HARD, gated resource (the spine) — LOCKED 2026-06-29
The decisions (A–E):
- **A. Workforce pool.** Not all population is drawable (children/elderly). `Workforce = population × WorkforceFraction` (a tunable coefficient, ~0.5) — a derived value, not separately tracked. Jobs, crew, soldiers, officers all draw against the workforce, **not** raw headcount. *This is also the fix for M2's employment denominator.*
- **B. Two tiers — bulk vs talent.** Bulk manpower (crew, workers, rank-and-file) draws from the workforce (plentiful). **Talent** (officers, scientists, governors) is a small, scarce pool (`Talent = population × TalentFraction`, ~0.5%) — trained, not drafted. "Who captains my ships / runs my worlds" is a scarcity separate from "do I have hands."
- **C. Return vs lost.** Disband a ship/army → its people **return** to the nearest colony's population. **Destroyed** → **lost** (casualties subtract from population). This is the BSG bite — losing a fleet/world hurts.
- **D. Shortage BLOCKS the build.** You cannot build a ship you can't crew — construction is gated on available manpower (block until people are free), not a uncrewed-hull penalty. Manpower is a hard gate on construction.
- **E. v1 scope.** Wire the consumers that exist today: **crew** (`ShipDesign.CrewReq` ← bulk), **officers** (academy/captains ← talent, capped; the unused `GeneratesNavalOfficers` flags are the hook), **scientists** (← talent). The **workforce** concept retro-fixes M2 employment. Army units ride the unit-designer track (task #21), drawing the same pools later.

**Pools are per-colony** (population is per-colony; the faction aggregates). Coefficients are named/government-ready.

**Build sub-slices (CI-safety):** *(1) foundation* — `ColonyManpowerDB` (committed bulk/talent) + pure pool math (`Workforce`/`Talent`/`Available`/`CanCommit`) + attach + retro-fix M2's employment denominator to use workforce; engine + unit tests, nothing enforced yet (CI-safe). *(2) enforcement* — gate ship construction on available crew (block), commit on build, return on disband, subtract-on-destroy, draw officers/scientists from talent; invasive (Industry/ShipFactory/CommanderFactory) and best confirmed on the developer's local build.

### M4 — colony economy → money + the TAX lever  *(built — CI pending)*
- **BUILT:** `ColonyEconomyDB` (player `TaxRate`) + `ColonyEconomyProcessor` (monthly, keyed on `ColonyEconomyDB`, bills tax into the faction `Ledger` via new `TransactionCategory.ColonyTax`). Income = population × per-capita × tax × morale-multiplier (a happy colony pays more). Tax is also a morale penalty (read by `PopulationProcessor`) — the one-tick-lagged loop. Pure math + the missing money gauge (`FactionEconomyTests`) included.
- **Deferred refinement (needs local feel / later):** pricing *actual* output (minerals/refined goods) instead of a per-capita prosperity tax; upkeep/maintenance + power as expense; the happy-medium equilibrium tuning.

### M5 — wire energy + food
- **M5a BUILT (machinery, CI pending):** the `ComputeMorale` overload-explosion is refactored to a `MoraleInputs` struct (positional overloads kept as thin delegates — back-compat); power-shortage and food-shortage added as morale inputs (food bites harder), **neutral when absent** so nothing changes live until the wiring. Unit-tested.
- **M5b (wiring, needs local build):** attach `EnergyGenAbilityDB` to colonies + a `PowerConsumptionAtb` / per-capita demand so a power deficit feeds a real `PowerShortage`; a food cargo good + monthly consumption processor feeding `FoodShortage`; severe shortage → a death term in `PopulationProcessor`. Additive + ship-safe (energy processors are entity-agnostic), but it's live cross-system behavior — pairs with the developer's build.

### Parallel track (after M3) — the unit designer / armies
- Ground units are **designed like ships** (confirmed: the component/`*Atb` chain, research gating, build-from-materials, and `ComponentInstancesDB` all reuse as-is; only the unit chassis, unit-scale stats, and formation grouping are new). The colony already has a hex-grid spatial substrate (`ColonyHexMapDB`). Armies draw people from the tank — the consumer that makes the resource bite. See task #21.

### Parked (deliberate, do NOT build now)
- **Droids-as-labor** (task #20): substitute machines for people to crew/work/garrison, at a cost/limit. Deferred so it doesn't dilute the people-scarcity loop.
- **Government models** (above): the modulator expansion.

---

## Prime Directive accounting — verified hooks & hidden connections (2026-06-29)

A four-survey verification pass over every remaining slice (file:line confirmed). **Seven cross-cutting findings** that change how we build:

1. **~~GlobalManager is never iterated~~ — NOW STALE (fixed).** At survey time `MasterTimePulse` only stepped *star systems*, not the GlobalManager where faction entities live, so a faction-level processor would never fire. **That trap is closed:** `MasterTimePulse` now calls `_game.GlobalManager?.ManagerSubpulses?.ProcessSystem(...)` (`MasterTimePulse.cs:348`, keystone #34), so faction-level loops (politics, NPC AI) now fire. The doc's *derived conclusion still holds* — **M4 economy + tax was built as a per-colony processor** (`ColonyEconomyProcessor`, `IHotloopProcessor` on `ColonyEconomyDB`, reading the colony's faction owner to write the `Ledger`) — but the original reason ("GlobalManager never fires") no longer applies.
2. **The start fleet bypasses construction (M3-2 regression killer).** New-game ships spawn via direct `ShipFactory.CreateShip` (not the `IndustryProcessor` queue) and officers are created directly (not via the academy). So crew/officer **gates can't break the start** — the #1 fear is gone. Start pop is also huge (billions), so workforce is ample.
3. **Energy is entity-agnostic and ship-only (M5).** The energy processors run on *any* entity with `EnergyGenAbilityDB`; colonies simply don't have the blob. Wiring colonies is **purely additive — no ship regression**. But there is **no consumer-side attribute** (`PowerConsumptionAtb` missing); consumers call `AddDemand` at runtime, so colony power demand needs a new attribute or a per-capita demand term.
4. **~~The admin seat is "dead code with a pulse"~~ — NOW STALE (fixed).** At survey time nothing read `AdminSpaceAbilityState.CommanderID` and commanders had no skill fields. **Both halves are now wired:** `AdminSpaceProcessor` runs a durable `ReconcileSeats`/`VacateSeat` seat pass (with a decapitation grave rung via `CommanderFactory.DestroyCommander`), and commander competence rides on the new `BonusesDB` (`CommanderBonuses.CombatMultiplier`, mirroring the Scientist bonus dict). The one piece still to build for *this* pillar is the morale-nudge consumer — a processor that reads the seated governor's competence and applies it to the host's morale (still ≈ 200–300 LOC).
5. **No money integration tests exist.** `LedgerTests` only checks the math in isolation — a new income stream is invisible to CI regression. **Build the gauge:** M4 needs a `FactionEconomyTests` (colony produces → funds rise) or it's flying blind.
6. **Tax ↔ morale is a two-way coupling.** Tax lowers morale; morale raises tax tolerance. Processor ordering matters — the economy processor reads morale and writes a "tax" factor that `PopulationProcessor`'s morale recalc consumes next tick. Keep it a one-tick-lagged loop so it can't oscillate within a tick.
7. **The morale/manpower foundations are host-agnostic — stations get them nearly free (#17).** `ColonyMoraleDB`/`ColonyManpowerDB`/`EmploymentAtbDB`/`HousingAtbDB` don't depend on `ColonyInfoDB`. To give a **station** morale+population, attach the same blobs and teach `PopulationProcessor` to also process `StationInfoDB` (or a thin parallel processor). M1–M3 thus serve the station-economy slice too.

**Verified injection points (file:line):**
| Slice | Hook | file:line |
|---|---|---|
| M3-2 build gate | block before materials consumed | `Industry/IndustryTools.cs:~195` (`ConstructStuff`) |
| M3-2 crew commit | on build complete | `Ships/ShipDesign.cs:64` (`OnConstructionComplete`), crew field `:56`/`:141` |
| M3-2 casualties / return | on destroy / disband | `Ships/ShipFactory.cs:~246` (`DestroyShip`) |
| M3-2 officer/scientist draw | gate creation | `People/NavalAcademyProcessor.cs:21`, `People/CommanderFactory.cs:85` |
| M4 ledger | add income/expense | `Factions/Ledger.cs:59-75`; add `TransactionCategory.ColonyIncome`/`Tax` at `:8` |
| M4 pattern to mirror | per-day expense | `Tech/ResearchProcessor.cs:100` |
| M5 energy state | the power blob (ship-only today) | `Energy/EnergyGenAbilityDB.cs:9`; attach in `Colonies/ColonyFactory.cs` |
| M5 shortage→morale/deaths | extend the recalc | `Colonies/PopulationProcessor.cs:~68-70` |
| M5 food | cargo good + monthly draw | `Storage/CargoStorageDB.cs` (colonies already have it) — new consumption processor |
| Governance | read the seat, apply morale effect | `People/AdminSpaceProcessor.cs` (`ReconcileSeats`/`VacateSeat` now wired); seat-read + competence-via-`BonusesDB` exists — the morale-nudge consumer is the remaining build |

**Design cleanup flagged:** `ComputeMorale`'s parameter list is growing (M5 adds power+food → 7 args). Before M5, refactor to a `MoraleInputs` struct to stop the overload explosion.

**Dependency-ordered build sequence (all foundations M1/M2A/M3-1 already green):**
- **M3-2** (crew/officer enforcement) — depends only on M3-1 ✅; start-safe; *needs local run to confirm feel.*
- **M2-data + base-low** — depends on M3-1 workforce ✅; *needs local calibration.*
- **M4** (per-colony economy + tax) — depends on M1 ✅; new `ColonyEconomyDB` + per-colony processor + the missing money gauge.
- **M5** (power+food) — depends on M1 ✅; `MoraleInputs` refactor first; additive, ship-safe.
- **Governance v1** (morale-nudge) — depends on M1 ✅; fuller auto-tax/auto-build depends on M4 + M2-data.
- **Station economy (#17)** — reuse M1–M3 host-agnostic blobs + teach population/mining processors `StationInfoDB`.

## Open questions (lock WHEN we reach each slice)
- M1: the exact morale curve coefficients (condition weight, crowding weight, migration cap %/month) — start simple, tune by feel.
- M2: housing-tier numbers; worker-slots per installation type; the labor-shortage output penalty curve. **Open finding (surfaced building M2-A):** employment must be measured against a **WORKFORCE** (the fraction of population available to work), NOT total population — a 500M homeworld can't be "employed" by a few installations, so a literal jobs/total-pop ratio reads as total unemployment and would tank the starting colony. The workforce concept is defined in M3 (it's the same pool crew/army/officers draw from). M2's base-low calibration waits on it.
- M3: population→manpower conversion ratios per draw type. *Shortfall behavior is now decided:* a swappable `CrewShortagePolicy` (Block default; BuildUnderstaffed for command regimes) — pure decision `ColonyManpowerDB.ResolveConstructionCrew` is **built + tested (M3-2a)**. **Open for the M3-2b wiring — crew provenance:** a committed ship must remember WHICH colony's pool it drew from (it roams between systems, so "nearest colony" won't do), so disband releases the right pool and destroy subtracts the right population. Lean: a `CrewSourceColonyId` on the ship, set at build. Decide before wiring.
- M4: tax curve + the morale↔tax equilibrium shape; upkeep units.
- M5: food model (stock + per-capita consumption); power-shortage severity bands.

*M1–M5a are built and wired (see the build-status banner at the top). The remaining live work is the M2-data calibration, the M3-2 enforcement wiring, M5b (energy+food attach), and Governance v1 (the morale-nudge consumer) — each needs the developer's local build to confirm runtime feel, since CI can't run the client. Live build-state is tracked in `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`. Lock each remaining slice's open questions as we reach it.*
