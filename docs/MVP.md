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

> **⚠️ RE-SEQUENCED 2026-06-26 — SPACE earns its weight BEFORE we take a planet (a deliberate finish-line move, per §7 — not a drift).** The realism-vs-gameplay audit (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`) found the loop above would be a thin shell on hollow systems: a planet that's colonize-and-forget isn't worth taking, and a space battle with no fog, no supply, no power isn't a game. So the build order is now two milestones:
> - **M1 — make the SPACE + infrastructure layer a real decision engine.** Build the audit's **ranked decision-levers** (§6), one bounded+gauged slice at a time, in leverage order — **starting with DETECTION** (fog of war + the dark-vs-active EMCON knob, riding the existing contact engine; *not* the spectrum-physics sim). This is the active build path now.
> - **M2 — the take-a-planet loop above** (ground combat → transport/drop → ground battle → capture), deferred until M1 lands, then built on a space layer that's actually a game.
>
> **Why (developer, 2026-06-26):** planetary combat is hollow without real planetary infrastructure to fight over — better to deepen what we already have in space until it earns its keep, then go planetary. **Discipline for M1:** one ranked lever at a time, each a gauged slice — *not* "fix everything at once" (that phrasing is the half-built-forever trap this doc exists to stop). M2's spec below (§§2–5) is unchanged and waiting.

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
| B | **Space combat actually resolves** | Two fleets fight; one side wins. No new weapon tech. | 🟢 **DONE (2026-06-25)** — the `GameEngine/Combat/` auto-resolve engine: hostile fleets in range auto-engage and a battle plays out over game-time until one side is wiped or breaks off. Decides by **strength math** (each ship rated for firepower/toughness), not the per-pixel beam/missile sim (which deposits ~0 damage — parked v2). Player's lever is **doctrine** (per-fleet *and* per-component), plus **retreat** and an **engagement lock**. 8 CI-green test fixtures. **This is the template we mirror for ground combat.** See `docs/COMBAT-DESIGN.md`. |
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

> **Re-sequenced (2026-06-26):** **M1 — the space-depth levers (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`, detection
> first) now run BEFORE Stages 2–4.** Stage 1 (space combat resolves) is ✅ done; rather than proceed straight to
> Stage 2 ground combat, we first make the space + infrastructure layer earn its weight (M1). Stages 2–4 below
> are the M2 conquest loop, unchanged and deferred until M1 lands. Each M1 lever is its own bounded, gauged slice.

- **Stage 0 — Economy is real and visible.** *(we are here)* **Engine substrate COMPLETE and gauged: gather
  → refine → build all proven** (mining depletes deposits; refining makes Space-Crete; the factory consumes
  minerals and installs a new Refinery, 1→2 — `ProductionBuildTests`). The build path a unit will ride
  (`IndustryJob.InstallOn` → `AddComponent`) is verified. **Remaining Stage-0 item: confirm the colony economy
  UI works live** — it already EXISTS and is wired (`ColonyManagementWindow`: Summary/Production/Construction/
  Mining, including job-queuing), so this is a *verify-and-fix* task (live-test §5B step 7), not a build. The
  engine is ready to build ships and units.
- **Stage 1 — Space combat resolves, and is gauged. 🟢 DONE (2026-06-25).** The `GameEngine/Combat/`
  auto-resolve engine (strength-math, doctrine as the player's lever, retreat, engagement lock), 8 CI-green
  fixtures. **This is the reference pattern for Stage 2** (`docs/COMBAT-DESIGN.md`). *Open live-test threads
  riding on top, not new stages: the combat **interrupt** (auto-pause at first contact, runs at the player's
  set speed — built, CI-green, awaiting the developer's live test) and the **teleport-to-Sun** movement defect
  (auto-detected by the SessionLog heartbeat; needs one live repro + root fix — a Stage-4 live-drive blocker,
  see `SESSION_STATE.md`).*
- **Stage 2 — Ground combat, mirroring Stage 1 — build the DECISION SPINE, not the realism.** `GroundUnitDesign`
  (an `IConstructableDesign`, so the *existing* industry queue builds it) + `GroundForcesDB`; a
  `GroundCombatProcessor` that attrites attacker vs defender. **Mirror what *earns its weight* in Stage 1 — the
  doctrine/composition decision — not the parked per-pixel realism** (see `docs/REALISM-VS-GAMEPLAY-AUDIT.md`).
  Gauge: build a unit, resolve a battle to a winner.
- **Stage 3 — Stitch the loop.** Target planet gets orbital defenders + a garrison. Fleet clears orbit
  (Stage 1) → transport/drop the ground force → ground battle (Stage 2) → defenders gone → flip the colony's
  owner. Gauge each link; gauge the whole loop once.
- **Stage 4 — Drive it from the UI — the keystone, not polish.** Build ships+units, send the fleet, issue the
  invade order, watch the result. The realism-vs-gameplay audit's headline applies here: the engines are
  built, **the UI is the missing control panel.** Stage 4 is where must-have **H** turns the existing,
  already-working engines into reachable *decisions* — wire levers onto what exists, don't gold-plate or
  rebuild. Live-test (§5B of the systems map); CI can't see the client.

When Stage 4 works, **v1 is done.** Stop. Play it. *Then* open the parking lot.

> **The weight-firewall lens (companion to this scope-firewall).** Every remaining stage is built to one extra
> rule from `docs/REALISM-VS-GAMEPLAY-AUDIT.md`: **name the player decision before you build the realism.** This
> doc says *what* is on the path (only the §1 loop); the audit says *how well* each piece must earn its keep (it
> must hand the player a decision that stacks). The two compose — they don't compete. Staying on this spine is
> also what *prevents* the detection/omniscience debt: that debt only grows if we keep **deepening** combat on
> the "everyone sees everyone" stub, and the firewall says don't — finish the loop. Detection is parked (§6).

---

## 6. Parking Lot — where good ideas wait (so they don't derail v1)

Add ideas here freely. Writing the idea down means it's safe to *not* build it yet. Revisit only after the §1
loop ships.

**Strategic pillars (headline v2):**
- **eXplore — a strategic X**: survey ships find new systems, jump points, new planets to take.
  The conquest spine from v1 is what makes exploration *matter*.
- **eXploit — espionage & "abnormal" diplomacy — a strategic X**: intelligence, sabotage, bribery, subversion,
  propaganda, fomenting rebellion — exploiting *rivals*. Rides on Diplomacy/Factions (design-only today). The
  conquest spine is what makes subterfuge worth doing.

**The ranked decision-lever backlog (the FIRST thing to build once v1 ships).** Sourced from and ranked in
`docs/REALISM-VS-GAMEPLAY-AUDIT.md` — these are the highest *decision-installed-per-effort* wins, each one
turning an engine that *already runs* into a player decision, and most making systems **stack**. Build in this
order when the §1 loop is done; do **not** pull them onto the v1 path (that's the firewall):

1. **Refined materials → component costs.** Weapons/shipyards require steel/electronics → mining→refining→
   production→combat becomes ONE supply-chain decision. Converts ~4 "pretty" systems into a stacked choice.
   Mostly JSON data + a `BaseModIntegrityTests` check. *Highest leverage in the game.*
2. **Energy powers weapons & engines.** Cut power → guns don't fire / thrust drops. The dead power gauge
   becomes "guns or thrust?" mid-fight; reactor/battery ship-design choices start to matter.
3. **Detection done right — active/passive + EMCON on a *simple* model** (NOT the spectrum sim). See far but
   get seen, or go dark and go blind. *This* is the gameplay of detection (fog, ambush, first-strike). The
   v2 entry point is a one-method seam in the combat trigger ("what can this faction see", currently "everything").
4. **IFF + relationship state.** Fire control refuses friendlies; hostility becomes friendly/neutral/hostile.
   Seeds the whole diplomacy/eXploit axis.
5. **Expose the `NavWindow`** (~2-line wiring) — a complete maneuver/intercept/delta-V planner exists and is
   unreachable; surfacing it makes fuel/intercept a real decision.
6. **Tighten storage + surface fuel scarcity**, **commander combat skill read by the resolve**, **armor design /
   in-battle damage-focus** — the rest of the audit's list.

**Other seeds:**
- _(seed)_ Orbital bombardment softens defenders before the drop.
- _(seed)_ Multiple ground/space unit types (artillery / armor / infantry; PD / beam / missile doctrine).
- _(seed)_ The hex-map tactical ground battle (`ColonyHexMapDB` already exists as a substrate).
- _(seed)_ Money/Ledger so the economy has a P&L and an NPC can reason about it (unblocks lever #1's pricing).
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
