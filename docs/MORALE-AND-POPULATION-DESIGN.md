# Morale & Population — Design (locked decisions + build plan)

**Status: LOCKED design, recorded 2026-06-29.** This is the build spec for the colony/station **morale + people-as-a-resource** loop — the keystone the developer wants finished as part of "the space economy, before going planetside." It closes five circuits the engine currently leaves open. Companion to `docs/BEYOND-PROTOCOL-REFERENCE.md` §1 (the model), `docs/SPACE-STATIONS-DESIGN.md` (morale is the shared "manning" concept both hosts carry), and `GameEngine/Colonies/CLAUDE.md`.

Built from a 3-survey Prime-Directive pass (population loop, people→officers, ground-units/designer). The honest starting state: **population is a one-way number**, there is **no morale anywhere** (only three unused event-name enums), **nothing draws people out** of the population, and the colony is **disconnected from money and from the power grid**.

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
5. **Government type is a MODULATOR (parked expansion), not a tank.** The whole loop above quietly assumes a *consent-based* government. A command economy (full communist) changes the rules: the state assigns jobs, extracts past the consent point, and closes borders — so the pressure converts from **emigration** to **unrest/revolt** plus a productivity penalty. **Therefore: build every morale input/output as a TUNABLE COEFFICIENT, never hardcoded**, so a future `GovernmentDB` can re-skin the loop (capitalist-consent / communist-command / theocracy / hive …). This ties straight into the B5 politics pillar. *Do not build now — design for it.*

---

## The connection map (Prime Directive — what morale touches)

| System | Relationship | Status today |
|---|---|---|
| **PopulationProcessor** (`Colonies/`) | morale recalced here; migration applied at the `@todo: external factors` hooks (lines 67, 83) | growth ✅, morale ❌ |
| **Carrying capacity** (`PopulationSupportAtbDB`, `ColonyLifeSupportDB`) | a single lump today; M2 splits it into Housing / Employment / Food | exists, lumped |
| **Species environment cost** (`SpeciesDBExtensions.ColonyCost`) | the "conditions" morale input | exists ✅ |
| **People** (`CommanderFactory`, `NavalAcademy`, `ResearcherDB`) | officers/captains/scientists — today spawned from thin air, no population link; M3 makes them DRAW from the tank | ❌ unlinked |
| **Ships** (`ShipDesign.CrewReq`) | crew number exists but is never subtracted; M3 wires it | ❌ unlinked |
| **Energy** (`Energy/`) | fully built, **zero** colony connection; M5 wires power shortage → morale + deaths | ❌ unplugged |
| **Money** (`FactionInfoDB.Money` / `Ledger`) | only research drains it; M4 wires colony output → income, scaled by morale, with the tax lever | ❌ unplugged |
| **Ground units / unit designer** | the army consumer of the people resource; mirrors the ship designer (own track) | ❌ absent |
| **Government** (future `GovernmentDB`) | modulates all morale coefficients + draw/migration rules | ❌ parked |

---

## Cradle to grave (passes the test)

> mineral → material → **housing / power / food / jobs modules** (components — designed in the designer, research-gated, built from materials) → installed on the host → **morale** is the running state → the **decision** is build-vs-let-slide (and **set the tax rate**) → the **grave rung** is collapse / depopulation (= the ground-invasion objective).

One system serves the **economy** pillar, the **BSG-nomad** pillar (a planet-less fleet must manufacture morale and jobs out of stations), and the future **invasion** pillar.

---

## The build slices (each small + CI-provable)

### M1 — MoraleDB + the valve  *(building now)*
- New `ColonyMoraleDB` (host-agnostic blob; attach to colony now, station later — the shared "manning" concept).
- Morale (0–100, 50 = neutral) recalced each population tick from inputs that **already exist**: **conditions** (`ColonyCost`) and **overcrowding** (pop vs capacity). All weights are **named coefficients** (government-ready).
- Output: a **migration rate** added to growth — morale < 50 → emigration, > 50 → immigration. Wires into the two `@todo: external factors` hooks.
- A `Factors` breakdown dict on the blob = the **gauge** (so the player/tests can see *why* morale is what it is).
- Gauge/test: morale math is a pure helper (deterministic unit test: hostile/crowded → low → negative migration; hospitable → high). Integration: the starting colony gets a MoraleDB, morale is high on Earth, no emigration.

### M2 — split jobs & housing (the two requirements the developer named)
- **What makes jobs:** every *productive* installation (mine/factory/refinery/lab/shipyard) carries a **worker demand** (`EmploymentAtbDB`). Total jobs = sum of slots. **Two-sided:** people > jobs → **unemployment** → morale down; jobs > people → **labor shortage** → installations run **under-staffed** → output drops. (Hard draw from M3 means workers are actually pulled from the tank.)
- **What makes housing worth a damn:** `HousingAtbDB` raises the **population ceiling** AND reduces **overcrowding** (a morale input). No housing → low ceiling → overcrowding → emigration → labor/tax/recruitment pools all stall. Build housing → ceiling rises → more workers, soldiers, taxpayers. **Tiers make it a design decision** (not a rubber stamp): cheap-and-cramped (capacity, mild morale penalty) vs expensive-and-comfortable (capacity + morale bonus). Housing costs materials/power/upkeep and produces nothing directly — so it trades against productive installations. That tension is the city-builder core.

### M3 — people as a HARD, gated resource (the spine)
- Derive an **available-manpower** sub-pool from population.
- Make crew (`ShipDesign.CrewReq`), officers/academy graduation (cap by population; the unused `GeneratesNavalOfficers` ability flags are the hook), scientists, and installation workers all **draw** from it and be **capped** by it. Shortfall → can't crew/build/staff (penalty or block). Now you can run out of people.

### M4 — colony economy → money + the TAX lever
- Colony output (mining/industry/population) → faction `Ledger` as income, **scaled by morale** (happy = richer); upkeep/maintenance + power as expense.
- **Tax rate** = player slider: income ∝ tax, morale ∝ −tax, with a happy-medium equilibrium that rises with investment. New `TransactionCategory` values (Mining/Production/Maintenance/Tax).

### M5 — wire energy + food
- Power shortage and food shortage feed morale down and (severe) deaths. Connects the built-but-unplugged `Energy/` subsystem and a new food stock/consumption.

### Parallel track (after M3) — the unit designer / armies
- Ground units are **designed like ships** (confirmed: the component/`*Atb` chain, research gating, build-from-materials, and `ComponentInstancesDB` all reuse as-is; only the unit chassis, unit-scale stats, and formation grouping are new). The colony already has a hex-grid spatial substrate (`ColonyHexMapDB`). Armies draw people from the tank — the consumer that makes the resource bite. See task #21.

### Parked (deliberate, do NOT build now)
- **Droids-as-labor** (task #20): substitute machines for people to crew/work/garrison, at a cost/limit. Deferred so it doesn't dilute the people-scarcity loop.
- **Government models** (above): the modulator expansion.

---

## Open questions (lock WHEN we reach each slice)
- M1: the exact morale curve coefficients (condition weight, crowding weight, migration cap %/month) — start simple, tune by feel.
- M2: housing-tier numbers; worker-slots per installation type; the labor-shortage output penalty curve.
- M3: population→manpower conversion ratios per draw type; what a crew/worker shortfall *does* (block vs degrade).
- M4: tax curve + the morale↔tax equilibrium shape; upkeep units.
- M5: food model (stock + per-capita consumption); power-shortage severity bands.

*Next action: build M1 (this is no longer a capture — the decisions above are locked). Lock each later slice's open questions as we reach it.*
