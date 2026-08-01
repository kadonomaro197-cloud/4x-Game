# 05 — SELF-SIMULATION (Phase 5 deliverable)

> **What this is:** the audit so far checked wires one at a time. This file does the opposite — it plays the
> game *on paper*, walking the whole designer system through five classic 4X situations and naming, at every
> step, **which designer's output the step consumes** and **where the flow breaks.** The point is to find the
> issues that only show up in a *sequence* — bootstrap deadlocks, dominated strategies, dead ends the
> single-wire audit can't see. Every issue found here feeds back to `03-CORRECTION-PLAN.md` (a fix) or
> `04-MISSING-DESIGNS/` (a design).
>
> These are thought-experiments with the designers' **actual** outputs (verified in Phases 1–4), not code
> runs. Read `01`–`04` first.

---

## The five situations

1. **Cold-start bootstrap (years 0–5)** — survey → mine → refine → build → research → *expand*.
2. **First contact** — sensors → detection → hostility → maybe a fight.
3. **Take a planet (cradle-to-grave)** — research → design → build → transport → invade → occupy.
4. **The economy/politics loop** — population → morale → migration/tax/legitimacy → government.
5. **A late-game crisis surge** — a wave that stresses every combat and logistics system at once.

---

## SIM 1 — Cold-start bootstrap (years 0–5)

**Order of operations, and what each step consumes:**
1. The start colony (Earth, 8.2 B pop) boots with a full installation set — mine, refinery, factory,
   shipyard, research-lab, local-construction, etc. → so mining (Industrial ▸ mine), refining (Industrial ▸
   refinery), component build (Industrial ▸ factory), ship assembly (Industrial ▸ shipyard) and research
   (Industrial ▸ research-lab) **all run on turn 1.** No leader is required (research works with
   `ScientistId = -1`). ✅ **The start colony has no bootstrap deadlock.**
2. Design a ship → the four assemblers read the Chassis budget + Weapons/Defense/Power/Propulsion/Sensors
   doors. Build it at the shipyard (ship-assembly points). ✅
3. Expand: survey a new body → mine it → **found a colony there.** ⛔ **HERE THE BOOTSTRAP BREAKS.**

**Where the flow breaks:**
- **B1 (deadlock) — you can't found a frontier colony at all.** `ColonyFactory.CreateColony` exists and takes
  a founding population, but **nothing sources that population from a ship** — the colonist can't be carried
  or unloaded (crack **C3**). So the entire "expand" verb — the X in 4X — is blocked. → **Design 2
  (Settling a World).** This is the single most important bootstrap finding: without it the game is one
  colony forever.
- **B2 — a frontier colony can't make its own power.** No colony-installable power generator exists
  (**E-build-11 / Design 1**). Harmless today (`PerCapitaPowerDemand = 0`), but the instant the power-morale
  wire is lit, a new colony with no power plant tanks. → **Design 1.**
- **B3 — leaders are unreachable.** The naval/other academies are on **no** starting build list, so you can
  never train an officer in a default game. No hard deadlock (leaders are bonuses, not gates), but the entire
  People-as-a-resource and command-agency layer is dark. → **Correction: add an academy to a start build
  list** (a data line), which then makes **Design 5** meaningful.
- **B4 (latent) — the first frontier colony's morale.** If jobs are seeded naively (**C1**), a brand-new
  colony reads as fully unemployed too. → **Design 3.**

**Verdict:** the *first* colony bootstraps cleanly; **every colony after it is blocked** on colonist
transport (Design 2). Bootstrap is the strongest argument for prioritizing Design 2.

---

## SIM 2 — First contact

**Order of operations:**
1. Your sensor sweeps (`SensorScan`) → a contact crosses the detection threshold → `SensorContactExists` →
   `FirstContact.OnDetection` fires. Consumes: Sensors door (detection range) + Power/Propulsion signature
   (how loud the *other* guy is).
2. Hostility check: `CombatEngagement.AreHostile` reads `DiplomacyDB` → if hostile and
   `RequireDetectionToEngage` is on, a battle can form.

**Where the flow breaks:**
- **F1 — detection is corrupted at the source.** The band-overlap gate (`SensorTools.cs:147`, crack **C7**)
  uses the wrong upper bound, so a signal *entirely outside* your receiver's band still passes. First contact
  therefore fires on things you shouldn't be able to see. → **C7 fix** (an engine bug, gates everything below).
- **F2 — everyone is equally loud.** Reactor-heat signature is off (`EnableReactorHeat = false`) and thrust
  signature is a flat constant, so activity doesn't change detectability. The "did they see me first?"
  question — the heart of first contact — has **no player input**: you can't run dark. → **A-flip-3a +
  Design 4.**
- **F3 — the intelligence half is unreachable.** `IntelDirectorateAtb` (agents, counter-intel, the intel
  ledger) is wired but on no start build, so espionage-flavoured first contact ("what do they *intend*?")
  can't happen in a default game. → **Correction: buildable intel directorate** (data line).

**Verdict:** the *mechanical* flow (detect → hostility → engage) is intact, but the two decisions that make
first contact a *game* — stealth (who sees whom) and intelligence (what they intend) — are both dark. First
contact is currently a dice-less "you both suddenly see each other."

---

## SIM 3 — Take a planet (the MVP finish line, cradle-to-grave)

**Order of operations, naming the designer output each step consumes:**
1. **Research** ground-combat tech → unlocks ground components. Consumes: Industrial ▸ research-lab points +
   the tech tree. ✅
2. **Design** a ground unit → Weapons door (attack, **penetration** — live on the ground side), Defense door
   (armour, env-resist), Enhancers door (training/strength/evasion/shield). The ground assembler **enforces**
   the reactor gate and magazine gate. ✅ (Ground is the complete, strict resolver.)
3. **Build** the unit → `GroundBuild` routes it through the `IndustryTypeRates` table by its `IndustryTypeID`
   (`GroundBuild.cs:45`) and reserves a build tile. Consumes: Industrial construction points. ✅
4. **Build a transport** with troop bays → Logistical door ▸ `GroundBayAtb` (the bay split). ✅
5. **Load troops** → `LoadTroopsOrder` → `TryLoadUnit` → `CanLoad` → `BayCapacity` (reads `GroundBayAtb`). ✅
   — **but** the per-unit carry size is **hard-coded** (Infantry 1 / Artillery 2 / Armor 3), ignoring the
   frame Size dial (crack **C11**). So a heavy elite armour unit and a stripped one take the *same* room. →
   **E-build-3.**
6. **Fly** to the target → Propulsion door (thrust/Δv) + Logistical (fuel). ✅
7. **Orbital bombardment** to soften the defenders → Weapons door (missiles, guidance fixed) →
   `DamageProcessor.OnColonyDamage`. ✅
8. **Invade** → land troops (`LandTroopsOrder`) → the ground resolver (`GroundForcesProcessor`) resolves the
   fight; fortification (Industrial ▸ bunker → `coverFort` divisor) protects the defender. ✅
9. **Occupy** → seize the surface. `CaptureInfrastructure` lets you take individual **buildings/hexes**
   (`GroundFortification.cs:54,70`). ⛔ **But the colony-level ownership transfer — "the planet is now
   yours" — is a BLOCKED developer decision** (the close-planetary-delta plan's S12: *what capture
   transfers*).

**Where the flow breaks:**
- **T1 — the last step of the MVP is undecided.** You can win the ground battle and seize buildings, but
  whether that flips the **colony's** `FactionOwnerID` (and what you inherit — population? installations?
  morale penalty?) is unruled. **This is the literal finish line of `docs/MVP.md` ("you can take a planet")
  and its final rung is open.** → flag to the developer as the #1 ruling to make; feeds a correction.
- **T2 — transport sizing is meaningless** (C11). → **E-build-3.**
- **T3 — you can't *settle* what you take.** Moving *your* colonists onto the captured world needs colonist
  transport (**Design 2**). Conquest without settlement is half the loop.

**Verdict:** the take-a-planet chain is **remarkably complete** — 8 of 9 steps work, and the ground resolver
is the most finished system in the game. The gaps are the *bookend* steps: transport sizing (C11), the final
ownership transfer (T1, blocked), and settling afterward (Design 2).

---

## SIM 4 — The economy/politics loop

**Order of operations:**
population grows (`PopulationProcessor`) → morale is computed from its terms (`ColonyMoraleDB`) → morale drives
migration + tax income + legitimacy → legitimacy drives rebellion → government type modulates all of it.

**Which morale terms actually carry the load today:**
| Term | State | Design ref |
|------|-------|-----------|
| Comfort (housing) | ✅ **live** | — |
| Crowding (pop-support) | ✅ live (conditionally) | — |
| Employment | ⛔ **dead** (no producer) | C1 / **Design 3** |
| Food | ⛔ **dormant** (`PerCapitaFoodDemand=0`) | A-flip-2 |
| Power | ⛔ **dormant + no producer** | Design 1 / A-flip-2 |
| Tax | ✅ live | — |

**Where the flow breaks:**
- **E1 — the loop is half-lit.** Only **comfort, crowding, and tax** actually move morale. The three terms
  that would create *scarcity pressure* — employment, food, power — are all dark. So the economy can't yet
  generate the "your colony is unhappy because it's hungry/idle/dark" tension that drives Beyond-Protocol
  play. → **Designs 1 & 3 + A-flip-2** are exactly the terms that light it.
- **E2 — markets are uncapped.** Inter-faction trade runs on logistics, but the logistics-capacity number
  (`LogiBaseAtb`) is **dead** (crack **C2**) — nothing limits throughput. Trade has no friction. → **C2
  decide-or-cut.**
- **E3 — politics is flag-gated off.** The NPC/AI politics engine exists but its behaviour arms
  (`EnableOrderEmission` etc.) default off, so the political layer is inert in a default game (a known state,
  per the AI docs).

**Verdict:** the economy loop's *plumbing* is complete, but three of its six pressure sources are dark. It's a
water system with the valves shut — Designs 1/3 and A-flip-2 are the valves.

---

## SIM 5 — A late-game crisis surge

**What it stresses:** a wave of hostile forces stresses the combat resolver at scale, detection, doctrine,
logistics, and leadership all at once — the situation that should make every deep system *matter*.

**Where the flow breaks — and the pattern is telling:**
- **X1 — elite units don't fight better.** The Firepower-Caliber enhancer is **dead in live combat** (crack
  D-gate-2 / Theme 3) — so building elite *offensive* cadres to meet the crisis is a **dominated strategy**;
  they cost more and deal the same. (Toughness cadres *do* work — so the meta is lopsided.) → **D-gate-2.**
- **X2 — no stealth option.** You can't run dark to evade or ambush a crisis fleet (signature dormant). →
  **Design 4 + A-flip-3a.**
- **X3 — no logistics strain.** Fuel exhaustion is off, so a long defensive war doesn't tax your fuel/tanker
  logistics — the endurance decision that should bite hardest in a crisis is absent. → **A-flip-3b.**
- **X4 — leadership doesn't matter.** Putting your best admiral in operational command of the defense does
  nothing beyond the single flagship (command agency dead). → **Design 5.**

**Verdict:** a crisis is *survivable by attrition* but **strategically flat** — every decision layer that
should create tension (elite quality, stealth, endurance, leadership) is dormant or broken. The crisis is the
clearest demonstration that the game's depth is *built but unlit*.

---

## The dominated / dominant strategy board (what the sims expose about balance)

The most actionable cross-sim finding: several designer outputs are currently **never worth building**, and a
few **dominate** — a balance distortion that comes entirely from the dormant/broken wires above.

| Never worth building (dominated) | Why | Fix |
|----------------------------------|-----|-----|
| **Firepower cadres** | dead in live combat (only test-only resolver reads them) | D-gate-2 |
| **Big transports/frames for carry** | carry size ignores frame size | E-build-3 |
| **Fire control** | track/range don't gate fire (flag off) | A-flip-4 |
| **Cryo holds** | no colonists to carry, no power to hold freeze | Designs 1 & 2 |
| **Admin/command components (beyond a flagship)** | seat occupancy is cosmetic | Design 5 |
| **Colony power / farm buildings** | demand is 0, so no reason to build them | A-flip-2 + Design 1 |

| Dominant | Why |
|----------|-----|
| **Toughness cadres over firepower cadres** | toughness works, firepower doesn't — a pure bug asymmetry |
| **Ground design over ship design** | the ground resolver enforces gates (reactor/magazine/penetration) ships don't — ground choices *matter* more |

**The through-line of all five sims:** the game is **built but unlit.** The mechanical chains are
astonishingly complete — take-a-planet is 8/9 steps, the economy plumbing is whole, ground combat is finished
— but the **decisions** that make those chains a *game* (expand, stealth, elite quality, leadership, scarcity)
are sitting behind dormant flags, zeroed coefficients, dead producers, and a handful of missing wires. Nothing
here argues for a rebuild. It argues for **lighting what's already there, in the Phase-3 wave order, with
Designs 1–5 supplying the five missing wires.**

---

## Issues fed back (traceability)

| Sim issue | Feeds |
|-----------|-------|
| B1 can't found a colony | Design 2 |
| B2 no colony power | Design 1 / E-build-11 |
| B3 leaders unreachable | Correction: academy on start build |
| F1 detection corrupted | C7 fix |
| F2 no stealth decision | A-flip-3a + Design 4 |
| F3 espionage unreachable | Correction: buildable intel directorate |
| T1 colony ownership transfer undecided | **New developer ruling (MVP finish line)** |
| T2 transport sizing dead | E-build-3 |
| T3 can't settle a conquest | Design 2 |
| E1 half-lit morale loop | Designs 1 & 3 + A-flip-2 |
| E2 uncapped markets | C2 |
| X1 firepower cadres dead | D-gate-2 |
| X2/X3/X4 crisis flat | Design 4 / A-flip-3b / Design 5 |

*Phase 5 complete. One new developer ruling surfaced (T1 — what colony capture transfers). Everything else
maps onto an existing correction or design. Next: Phase 6 — the final report and recommended build order.*
