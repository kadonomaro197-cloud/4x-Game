# Pulsar4X Fork — MVP: "One Planet, Taken"

**Read this before adding anything.** This document exists for one reason: to stop the game from being
half-built forever. When a good idea arrives — and it will — it does **not** go into the build. It goes into
the **Parking Lot** at the bottom. The MVP is finished when the loop in §1 works end to end. Nothing else is
required for v1, and nothing else is allowed to block v1.

> **The finish line is movable — but moving it is a decision, not a drift.** If you want v1 bigger or
> smaller, edit §1 deliberately and write down why. Picking up a feature "while we're here" is exactly the
> 50-year trap this doc prevents.

**Framing — engine vs. game.** Pulsar4X is a *world-simulation engine*, not a finished game: it models orbits,
ships, economy, and damage in real depth, but it has no strategic loop, no opponent that acts, and no
victory. That's why every dive finds dark/untested systems and no win condition — it's a flight-simulator-grade
world model with no campaign on top. **The MVP is the first complete campaign loop — the thing that turns the
engine into a game.**

---

## 1. The MVP, in one sentence

> **You can build a fleet and a ground force, defeat a planet's orbital defenders in space, land your troops,
> beat its garrison on the ground, and take the planet.**

If a player can do that one loop, the fork has delivered its core fantasy — combat that runs from orbit down
to the surface, paid for by real planetary infrastructure — and v1 ships.

### 4X coverage — and what's engine vs. game

The reframe that matters: **the economy is not one of the X's — it's the engine substrate the X's run on.**
Pulsar already gives us most of it (mine → refine → build). The four X's are the *strategic* game on top:

| X | In the MVP? | What it is |
|---|-------------|-----------|
| **eXpand** | ✅ | take territory — capture the planet |
| **eXterminate** | ✅ | the combat stack — space battle to clear orbit, then ground battle to take the surface |
| **eXploit** | ⏳ deferred | **espionage & "abnormal" diplomacy** — spying, sabotage, bribery, subversion, propaganda; exploiting your *rivals*, not just your own dirt. A whole strategic pillar — v2 (§6). |
| **eXplore** | ⏳ deferred | survey / jump-point discovery to *find* new systems and targets — v2 (§6). |

So v1 proves **two of the four strategic X's — eXpand + eXterminate, the conquest spine** — running on the
engine's economy. eXploit (espionage) and eXplore are the two big v2 strategic layers. Start v1 with a known
target so we prove the spine before building the discovery and subterfuge around it.

---

## 2. The MVP loop (the whole playthrough)

1. **Start** with the colony you already get. *(exists)*
2. **Run the economy** enough to produce military goods: mine → refine → build. *(engine done; needs a usable UI + the build-installations link)*
3. **Build a warship and a ground unit** at the colony, through the existing industry queue. *(ships exist; ground unit is new)*
4. **Send the fleet** to a target planet. *(movement exists)*
5. **Win the space battle** against the planet's orbital defenders. *(systems exist but are DARK/untested — gauge them)*
6. **Land the ground force** on the now-uncontested planet. *(transport/drop — new)*
7. **Win the ground battle** against its garrison. *(new — minimal)*
8. **Capture** the planet: defenders gone → the colony changes owner. **Win.** *(new — minimal)*

If any step in that chain doesn't work, v1 isn't done. If a feature isn't *on* that chain, it isn't v1.

---

## 3. IN — the must-haves (and where they stand on the map)

| # | Must-have | Minimum that counts | Status today (`SYSTEMS-STATUS-AND-TEST-PLAN.md`) |
|---|-----------|---------------------|--------------------------------------------------|
| A | **Economy produces military goods** | Mine → refine → build installations, ships, *and* ground units, from the colony's own output | Engine ✅ (mining/refining/production gauged). Build-installations link + ground-unit build = **to do.** |
| B | **Space combat actually resolves** | Two fleets fight; the existing beams/missiles do damage; one side wins. No new weapon tech. | 🔴 DARK, **no tests** — beams/missiles exist and are "functional"; this is *gauge it + make a fight end*. **This is also the template we mirror for ground combat.** |
| C | **A ground unit exists as a buildable thing** | ONE unit ("Ground Force", components like ships) with attack / defense / health | 🔴 absent — new `GroundUnitDesign : IConstructableDesign` + `GroundForcesDB` |
| D | **Transport & drop** | Load a unit onto a ship, move it, unload onto a target planet — *after* orbit is clear | 🟡 movement/cargo exist; load/drop = **to do** |
| E | **Ground combat resolution** | Attacker vs defender on one planet; attrition each tick until one side is gone (mirror B) | 🔴 absent — new `GroundCombatProcessor` + tests |
| F | **Win condition** | Defenders (space *and* ground) gone → colony `FactionOwnerID` flips to the attacker | 🔴 absent — minimal capture step + test |
| G | **A defender to fight** | A static enemy garrison + a couple of orbital defenders on one planet — *not* a full NPC admiral | 🔴 minimal seed is enough |
| H | **A UI you can drive it from** | See/queue the economy; build ships+units; send the fleet; issue the invade order; see outcomes | 🔴 weak point — reuse existing panels, don't gold-plate |
| I | **Every new/uncovered engine system has a gauge** | A test per system touched (the no-untested-combat rule — space combat especially) | enforced by CI + the harness |

---

## 4. OUT — explicitly deferred (the scope firewall)

These are **good** — and they are **not v1**. Building any of them before §1 works is the trap.

- **eXplore (a deferred strategic X):** survey processors, jump-point exploration, fog-of-war discovery,
  finding new systems/targets. v1 starts with a known target. A headline v2.
- **eXploit / espionage & "abnormal" diplomacy (a deferred strategic X):** spy networks, agents, sabotage,
  bribery, subversion, propaganda, instigating rebellion — exploiting rivals. A whole strategic system that
  rides on Diplomacy/Factions (design-only today). The other headline v2.
- **Space-combat depth:** new weapon types, detailed fire control, fleet doctrine/formations, the
  auto-resolution system, point defense tuning. v1 uses what exists and makes it *resolve*.
- **Ground-combat depth:** multiple unit types, formations, morale, supply, terrain, the tactical **hex-map**
  layer (`ColonyHexMapDB`) beyond the bare minimum.
- **Orbital bombardment** integrated with the ground phase (engine has partial hooks — v2).
- **Diplomacy**, multi-faction war, alliances, IFF depth.
- **Research** depth — v1 uses starting tech.
- Economy depth: the **Ledger/money** signal, trade, logistics automation, population economics beyond
  "enough population to staff the build."
- An **NPC AI** that plays the whole game — v1's defender is a static seed.
- Combat **visualization** polish (target lines, animations) and **balancing** — make it *work* before *fair*.

---

## 5. The build path — dependency-ordered, each step playable & gauged

Each stage leaves the game in a working, testable state. Don't start a stage until the one before it is green.
Note the order follows your own strategy: **do space combat first, then mirror its shape for ground.**

- **Stage 0 — Economy is real and visible.** *(we are here)* **Engine substrate COMPLETE and gauged: gather
  → refine → build all proven** (mining depletes deposits; refining makes Space-Crete; the factory consumes
  minerals and installs a new Refinery, 1→2 — `ProductionBuildTests`). The build path a unit will ride
  (`IndustryJob.InstallOn` → `AddComponent`) is verified. **Remaining Stage-0 item: confirm the colony economy
  UI works live** — it already EXISTS and is wired (`ColonyManagementWindow`: Summary/Production/Construction/
  Mining, including job-queuing), so this is a *verify-and-fix* task (live-test §5B step 7), not a build. The
  engine is ready to build ships and units.
- **Stage 1 — Space combat resolves, and is gauged.** Take the DARK beam/missile systems, put them under a
  harness test, and make two fleets fight to a winner. This is MVP-critical **and** the reference pattern for
  Stage 2 — study how it's wired (`docs/COMBAT-DESIGN.md`, `Weapons/`, `Damage/`) and gauge it.
- **Stage 2 — Ground combat, mirroring Stage 1.** `GroundUnitDesign` (an `IConstructableDesign`, so the
  *existing* industry queue builds it) + `GroundForcesDB`; a `GroundCombatProcessor` that attrites attacker
  vs defender. Gauge: build a unit, resolve a battle to a winner.
- **Stage 3 — Stitch the loop.** Target planet gets orbital defenders + a garrison. Fleet clears orbit
  (Stage 1) → transport/drop the ground force → ground battle (Stage 2) → defenders gone → flip the colony's
  owner. Gauge each link; gauge the whole loop once.
- **Stage 4 — Drive it from the UI.** Build ships+units, send the fleet, issue the invade order, watch the
  result. Live-test (§5B of the systems map); CI can't see the client.

When Stage 4 works, **v1 is done.** Stop. Play it. *Then* open the parking lot.

---

## 6. Parking Lot — where good ideas wait (so they don't derail v1)

Add ideas here freely. Writing the idea down means it's safe to *not* build it yet. Revisit only after the §1
loop ships.

- **eXplore — a strategic X** (headline v2): survey ships find new systems, jump points, new planets to take.
  The conquest spine from v1 is what makes exploration *matter*.
- **eXploit — espionage & "abnormal" diplomacy — a strategic X** (the other headline v2): intelligence,
  sabotage, bribery, subversion, propaganda, fomenting rebellion — exploiting *rivals*. Rides on
  Diplomacy/Factions (design-only today). The conquest spine is what makes subterfuge worth doing.
- _(seed)_ Orbital bombardment softens defenders before the drop.
- _(seed)_ Multiple ground/space unit types (artillery / armor / infantry; PD / beam / missile doctrine).
- _(seed)_ The hex-map tactical ground battle (`ColonyHexMapDB` already exists as a substrate).
- _(seed)_ Money/Ledger so the economy has a P&L and an NPC can reason about it.
- _(seed)_ A real defending/attacking AI; auto-resolution for off-screen battles.
- _(add yours here…)_

---

## 7. How to use this doc

1. **New idea?** → §6 Parking Lot. Not the build.
2. **Starting a stage?** → open `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, read the systems it connects to, work
   those too (the Prime Directive). Combat design lives in `docs/COMBAT-DESIGN.md`; ground/infrastructure
   design in `docs/aurora/`. Build the *slice*, not the whole spec.
3. **Stage done?** → it has a gauge (test) and the systems-map row is updated.
4. **Tempted to move the finish line?** → edit §1 on purpose and say why. Otherwise, hold the line.
