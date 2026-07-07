# AI Self-Play & the Leadership Layer — design (concepts + path forward)

**Status:** concepts + path forward only. No code written yet — this doc captures a design conversation (2026-07-06) so the decisions and the open questions don't scroll away. It sits *on top of* `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md` (the mechanism) and pulls together the combat, diplomacy, espionage, ground-combat, and survey docs into one picture: **how the AI runs the other empires, and what it costs.**

---

## What it does, in one line

Let every NPC empire **play itself with the full toolkit the player has** — conquer worlds, put down rebellions, run diplomacy, build fleets with their own doctrines — the way *Distant Worlds* does, **without inventing a separate "AI" and without grinding the game to a halt.**

## Why it matters (the developer's north star for this)

The long-term dream: the player is one empire among many living, acting rivals. The player picks whatever government they want and plays at whatever altitude they like; the galaxy runs on its own around them. Two ways a 4X normally fails at this, and this design kills both at once:

- **The "it feels like a job" failure** — the player is forced to hand-fly every fleet and click every colony's build queue, and the game collapses under busywork by the midgame.
- **The "inert AI" failure** — the NPC empires just sit there because nobody wrote them a brain. (This is where we are *today* — see the survey below.)

---

## THE KEY INSIGHT — delegation and NPC AI are the same system

This is the load-bearing idea of the whole doc, and it's what makes the dream affordable.

**A leader-seat is simultaneously the player's hand-off valve and the NPC's brain.** One system, two jobs:

- For the **player**, a leader is the *off-switch for micro*: seat an officer, set a standing order ("stance"), and stop clicking.
- For the **NPC**, that *same* leader-seat **is the AI** — it issues the identical orders a player would.

The governance design already locks the rule that makes this true: a delegate *"issues the same `IndustryOrder2`/tax orders"* the player uses — **there is no special AI code path** (`docs/GOVERNANCE-AND-DELEGATION-DESIGN.md`). So a Governor running a colony to a stance for a lazy *player* is the exact same machinery as "the AI" running an *NPC's* colony.

**The payoff:** we don't build a player game *plus* an AI. We build **one** delegation layer and point it at both. An NPC empire is just an empire where *every* seat is filled by a delegate; the player is a human sitting in as many or as few of those seats as they like. This is how *Distant Worlds* actually works, and it's why the scope is reachable.

---

## Will it slow the game down? (the performance question that started this)

Short answer: **the AI *thinking* is nearly free; the AI *doing* is the whole cost.** Those are two different bills and must not be conflated.

### Bill #1 — Deliberation (the AI making decisions). Negligible.

Grounded in the actual loop:

- NPC brains run in `NPCDecisionProcessor`, which fires **once a game-month** per NPC faction (`RunFrequency = 30 days`), and it already staggers factions so they don't all think on the same tick.
- The clock ticks in **1-hour steps** by default (`MasterTimePulse.Ticklength = 3600 s`). A month is ~720 ticks. So a faction "thinks" once every ~720 steps.

Put numbers on it: even a thorough turn costing 20 ms per faction, across 15 empires, is ~300 ms spent *once a month* — under half a millisecond per tick amortized. A rounding error next to the physics already running every hour. **Cadence is the friend, and it's already built.** The AI is the department head who plans at the Monday meeting, not a watchstander taking readings every hour.

### Bill #2 — Consequences (what the AI builds and animates). This is the real cost.

Every ship, colony, fleet, and sensor the AI creates is simulated by the **same per-tick processors, forever**. The decisions are cheap to *make*; each one adds a permanent tenant to the hourly simulation. And there's an engine-specific multiplier underneath:

> **Today the galaxy is cheap because most of it is asleep.** Empty systems drop out of the loop (`MasterTimePulse` filters `ActivityState != Stasis`), and hotloop processors on empty systems put themselves to sleep. The benchmark runs **2 systems**. A galaxy with 15 AI empires spread across it is a galaxy where **most systems are awake** — because someone lives there.

So full AI doesn't just add its own cost — it **removes the sleeping-galaxy discount** that currently makes big maps affordable. Per-tick cost is roughly **linear** in active systems + entities for most processors (combat is proven O(ships) via weapon-class bucketing — 200 ships in <4 s, 1000 gnats in ~9 ms). The one **landmine is sensors/detection**, which can go O(emitters × receivers) — quadratic — once many factions' fleets are all sensing at once. Worth watching; the "degenerate detection-quality" fix flagged elsewhere matters here too.

### The constraints that bound it

1. **The sim is serial by default** (`EnableMultiThreading = false`). Star systems run one after another; the `GlobalManager` (where faction brains live) runs **serially, after** the systems (`MasterTimePulse.cs` — the keystone line that makes faction processors fire at all). So AI work lands on one thread today.
2. **A parallel mode exists but is unproven** — it parallelizes *per system* (helps consequence cost), but not the single-bucket faction deliberation. It's off by default.
3. **There is no scaling gauge yet.** The benchmark is 2 systems, 1 faction. The per-processor stopwatch (`PerformanceStopwatch`, read by `PerformanceReadoutSmokeTests`) exists and works, but nothing measures tick-time as faction/entity count climbs. **By the Visibility Gate, that's the first thing to build** — you can't steer a cost you can't see.

### The levers (path to "many AI empires, still fast")

- **Build the gauge first.** A scaling benchmark: dial up N factions × M systems × K ships, read the existing stopwatch, plot the curve. Turns "how much?" into data, and exposes the sensor-quadratic if it's real.
- **Keep deliberation slow + staggered** (already the design). Never move NPC thinking to an hourly loop.
- **Level-of-Detail for distant empires — the headline lever.** An NPC the player has never met and can't see does not need entity-by-entity simulation. Run it in "cold layup": its economy is a handful of numbers advanced cheaply, and it **instantiates real ships/colonies only when it interacts with the player or enters an observed system.** This is how every big 4X affords 20 AI empires, and it's philosophically identical to the stasis/sleep the engine already has.
- **The survey fog is a *free* brake** (see below): an NPC literally cannot expand into space it hasn't scouted, so a correct AI only wakes systems it has surveyed.
- **Make AI footprint a difficulty/performance dial** — since consequence cost = entity count, "how aggressively the AI expands and how many ships it fields" is simultaneously a difficulty knob and a performance knob.

---

## The leadership model — the shape every leader shares

Every leader is the same shape (from `docs/GOVERNANCE-AND-DELEGATION-DESIGN.md`): **an officer seated in a post, given a stance (standing orders), who auto-runs that scope at a competence cost** — and whom the player can drop in on at any time without un-seating them.

**The navy picture:** a CO doesn't stand every watch. He sets the standing orders, his department heads and watchstanders run their spaces to those orders, and he intervenes only when something's off or when he chooses to. Span of control limits how much one chain can hold. This game is that, at empire scale.

### The three layers (the developer's own framing)

Every pillar is three layers deep:

1. **God** (player / for an NPC, the top-level AI) — sets policy, can drop into any chair.
2. **The Leader** (governor / commander / minister) — holds one scope, runs it to a stance, at a competence cost.
3. **The autonomous layer below** — executes on its own. In combat this already exists: sub-fleet components (Front Line / Flank / Rear Guard / Artillery) each carry their **own** doctrine, and there is *"no per-ship control, ever"* — the individual ship picks targets by the weapon-triangle rule with nobody micromanaging it.

### Two chains that cross (not one)

This is how real militaries are actually organized, and it fell out of the design naturally:

- **Administrative / territorial chain — "who you belong to."** Head of State → System Governor → Planetary Governor, with the Interior Minister and Planetary General under their Governor, and the System General under the System Governor. This chain owns **intent**: what a world protects, where a system's economic effort flows, local defense posture.
- **Operational military chain — "who gives you combat orders."** Grand Admiral → System Admiral → Fleet Commander (space, *mobile*); Grand Admiral → System General → Planetary General → Battalion Commander (ground operations).

The **ground leaders are where the two chains cross** — a Planetary General answers to the **Governor** for *what to defend* and to the **System General** for *where to move*. Those are distinct decisions, so it's a clean matrix, not a conflict. **Fleets stay purely on the operational chain** because they're mobile — a fleet doesn't belong to a system the way ground forces belong to a world.

---

## The rule — no leaders for leaders' sake

A leader who is just "+2 to a modifier" is the "pretty" disease from `docs/REALISM-VS-GAMEPLAY-AUDIT.md`: fidelity nobody acts on. Every seat must earn its place. The test:

> **A distinct leader seat is justified only if it owns a decision the player would otherwise make by hand, and that neither the leader above nor below it already owns.**

This is *why* the naval ladder got collapsed — Task Force / Task Group / Task Unit were cut because they didn't own a decision the System Admiral or Fleet Commander didn't already own. Rungs for rungs' sake.

And **competence (the "+2") is demoted to its proper job**: it is not the reason a seat exists — it's the dial on how *well* the seat's decision gets executed. A master governor keeps the queue full and morale up; a green one lets the colony drift. The decision is the substance; the modifier is only the texture on the outcome.

---

## The full roster — 19 leader role-types

Grouped by the administrative spine, with the operational military line crossing in. Each row names **the decision it owns** and its **build status** (EXISTS = real data home + wiring · STUB = hook exists, unfed · WIRE = the underlying mechanic is built, only the officer-in-the-seat is missing · NEW = net-new).

### The tree

```
HEAD OF STATE   (regime type + empire legitimacy — held DIRECTLY, no minister)
│
├─ EMPIRE CABINET
│   ├─ Grand Admiral ............ where the war effort goes (which systems)
│   ├─ Empire Foreign Minister .. overall external posture
│   │     └─ Per-faction Foreign Minister  (one per met faction)
│   │           ├─ Ambassador ... posted at that court
│   │           ├─ Envoy ........ sent for a negotiation
│   │           └─ Agent ........ ops against that faction  ⟵ dotted line to Spymaster
│   ├─ Spymaster ............... espionage doctrine + counter-intel (functional home of Agents)
│   ├─ Chief Scientist ......... research direction
│   │     ├─ Lab Scientist ..... runs a research institution
│   │     └─ Survey Scientists . system + planetary survey (the empire's eyes)
│   └─ Trade Minister .......... routes, tariffs, import/export
│
└─ SYSTEM SCOPE
    ├─ System Admiral .......... fleet movement & engagement in-system  [MOBILE — coordinates with, not under, the System Governor]
    │     └─ Fleet Commander ... one fleet's doctrine  →  [autonomous ships]
    └─ System Governor ......... economic effort across the system's worlds
          ├─ System General .... which worlds to reinforce / hold / invade   ⟵ also operational orders from Grand Admiral
          └─ Planetary Governor  (one world's development)
                ├─ Interior Minister .. that world's politics / stability / blocs
                └─ Planetary General .. the surface campaign on this world   ⟵ also operational orders from the System General
                      └─ Battalion Commander ... one battalion  →  [autonomous units]
```

### The roster table

| # | Role | Scope | The decision it owns | Status |
|---|------|-------|----------------------|--------|
| 1 | Grand Admiral | Empire (space) | Which systems get the war effort | STUB (commander entity exists, unwired; v1 flat modifier) |
| 2 | Foreign Minister | Empire + per-faction | Overall external posture; policy toward each rival | NEW |
| 3 | Interior Minister | Planet (under Governor) | How to answer a world's bloc demands — stability vs tax vs military | NEW (`GovernmentDB` substrate partial) |
| 4 | Spymaster | Empire | Which covert ops, against which rivals; counter-intel | NEW |
| 5 | Chief Scientist | Empire | Research direction — which categories get effort | EXISTS (research funding-dial delegate is the one built example) |
| 6 | Trade Minister | Empire | Routes, tariffs, import/export | NEW (+ the trade-money wire must come first — see commerce note) |
| 7 | System Governor | System | Where the system's economic effort flows across its worlds | NEW (seat scope `AdminLevel.System` exists) |
| 8 | Planetary Governor | Planet | Build priorities, tax, stockpile, growth-vs-military for one world | STUB (`LegitimacyDB.GovernorCompetence` hook exists, never fed) |
| 9 | System Admiral | System (space) | Where fleets move & what they engage in-system | NEW |
| 10 | Fleet Commander | Fleet | One fleet's combat doctrine/posture | STUB (`FleetDB.FlagShipID` is a ship, no commander link) |
| 11 | System General | System (ground) | Which worlds to reinforce / hold / invade | NEW |
| 12 | Planetary General | Planet (ground) | The surface campaign — where battalions push or hold | NEW (over the built formation layer) |
| 13 | Battalion Commander | Formation | One battalion's doctrine/posture | WIRE (`GroundFormation` + `GroundFormationDoctrine` built; person-commander net-new) |
| 14 | Ambassador | Per-faction (field) | *Where to post a scarce specialist* (allocation) | NEW |
| 15 | Envoy | Per-faction (field) | Terms of one negotiation | NEW |
| 16 | Agent / Operative | Per-faction (field) | Which op, which target (allocation) | NEW |
| 17 | Lab Scientist | Institution (field) | (runs research to the Chief Scientist's priorities) | EXISTS (fully wired) |
| 18 | System Survey Scientist | System (field) | Which bodies/anomalies to survey first | WIRE (geo + JP survey fully built; only the leader wrapper is new) |
| 19 | Planetary Survey Scientist | Planet (field) | Which world / deposit to survey first | WIRE (as above) |

### Symmetry check — clean at every scope except one

| Scope | Space military | Ground military | Civilian governance |
|-------|----------------|-----------------|---------------------|
| **Empire** | Grand Admiral | *(deferred)* | Head of State |
| **System** | System Admiral | System General | System Governor |
| **Planet** | — (ships don't hold worlds) | Planetary General | Planetary Governor |

The only remaining asymmetry is the **empire-scope ground ceiling** — deferred until multi-system ground wars are real.

---

## Survey — what EXISTS today (this is mostly wire-and-generalize, not build)

The Prime-Directive pass (2026-07-06) found the substrate in far better shape than "inert NPCs" implies:

- **The faction-loop keystone is DONE.** `MasterTimePulse` now iterates the `GlobalManager`, so `NPCDecisionProcessor` genuinely fires monthly per NPC — **but its decision body is an empty `TODO` stub.** Today NPCs only drift diplomatic-relationship *numbers*; they take no actions. The hook to build on is ready; the decision-to-order translation is unwritten.
- **The delegate skeleton exists** and is used for one narrow case (research labs). The span-of-control **seat ladder** (`AdminLevel`: `Ship · TaskUnit · TaskGroup · TaskForce · Fleet · Colony · Planet · SOI · System · Sector · Empire`) is real and wired; the generic officer-in-a-post record (`AdministratorDB`) exists but is an orphan stub (never instantiated — the real seating uses `AdminSpace*` + `CommanderDB`). Commanders exist (`CommanderDB`, types Navy/Ground/Scientist/Civilian) but have **no skill-bonus fields** and `Experience` is stored-but-unused. The naval academy generates officers (Navy only).
- **Combat leaders' machinery is built** — `FleetDoctrineDB` per-fleet AND per-sub-fleet; the commander is a flat stub in v1.
- **Ground combat is a real system**, not a scaffold. `GroundFormation` is literally *"the ground echo of a FleetDB"*, `GroundFormationDoctrine` is *"the ground echo of FleetDoctrine"*, and sub-formation nesting exists. The gap: a formation's "leader" is one of its own units (a flagship echo), **not a person from the commander pool** — units are data objects, not entities, and per-unit commanders are an explicit v2 promotion.
- **Survey (the eXplore arm) is fully built.** Geological survey and jump-point/gravitational survey each have a component, a player order, a processor, completion events, and fog-of-war reveal. Minerals are **hidden by default** (`Masked<T>`, `AccessLevel.None`); jump points are hidden until surveyed; discovering one **reveals the system on the far side** and adds it to `KnownSystems`. There are even auto-survey pathing helpers. **The one content gap: no ruins/artifacts** beyond jump points — that piece is net-new.
- **Commerce has no leader** (designed or built) — it's the one pillar that fell through. The automated freight market IS built and diplomacy-gated, but **trade earns no money** (`Ledger` has no `Trade` category; the `ExchangeCatalog`/`TradeAgreement` are inert data). A Trade Minister needs the trade-money wire first.

---

## The leader cradle-to-grave pipeline (born → grave)

A leader isn't mined, so it doesn't ride the mineral→component chain — it rides the **people chain**, and it's the **same six rungs for all 19 roles**. The whole point: don't build 19 leaders — build **one pipeline**, prove it on one role, then every other role is configuration (a `BonusCategory` + a `Refresh` method), not a rebuild.

Navy picture: how you get an officer from nothing to running a department — recruited → rated at a school → assigned to a billet → stands the watch (his skill runs the plant) → advances with quals → transferred/lost, billet gapped.

| Rung | What it means | State in the code today (file:line) |
|------|---------------|-------------------------------------|
| **1. Born** | a leader is generated *with a competence value* | ⚠️ **Weakest.** `NavalAcademyProcessor` graduates Navy officers with a bell-curve `Experience` int (`NavalAcademyProcessor.cs:31-46`) but **no competence bonuses** — the graduate's `BonusesDB` is left **empty** (`CommanderFactory.cs:24`). The one "skilled" example hardcodes `0.1` in `NewGameMenu.cs:629`. **No competence generator exists.** |
| **2. Skilled** | competence stored in a reusable container | ✅ **Built & reusable.** `BonusesDB.Bonuses` — `Bonus(Value, Type, FilterId)`; `FilterId` routes a bonus to a scope (`BonusesDB.cs:20-47`). |
| **3. Seated** | the leader put in a post with command scope | ✅ **Strongest.** `AdminSpace` seat ladder (11 levels Ship→Empire), `AssignAdministratorOrder` links both ways, `FundingLevel 0–5`. The billet exists only because you **built the command component** that opens it (`AdminSpaceAtb`) — so leaders connect back to the mineral→material→component chain. |
| **4. Acts** | competence multiplies a real game number | 🔸 **Works in exactly one place, as a copyable pattern.** Only research: target holds a `ModifiableValue`, `RefreshPointModifiers` folds the officer's `BonusesDB` in, `GetValue()` reads it (`ResearchProcessor.cs:87,292-333`). Dead for the other 18 roles. |
| **5. Improves** | leader gets better with tenure/success | ❌ **Absent** as an auto-loop — `Experience` is stored and never read into any computation. **Replaced by the retraining loop below** (deliberate re-enrollment, not passive XP). |
| **6. Lost** | killed/captured/turned/died → seat empties, delegation collapses | 🔸 **Mostly absent + a bug.** Only a ship's captain dies today (`ShipFactory.cs:247`); it leaks dangling seat refs, and `AdminSpaceProcessor` resets seats each pass. Fix = one `LeaderLost` handler (the `CrewLosses` event is already published, unconsumed). See Rung 6 in depth — the rung that makes the player *care*. |

**The essence:** rungs 2 and 3 are built and reusable; rung 4 exists once as a pattern to copy; rungs 1, 5, 6 are the real work — and even the proven example lacks a competence generator (1) and growth (5).

### Rung 1 in depth — Leader Academies (the installation)

Leaders are **produced by installations** — schools / colleges / universities — that take a fraction of a colony's citizens and turn them into placeable leaders. This is the bottom rung: *it takes from the colony and gives the player/NPC leaders to use.* It is mostly **connect-and-generalize**, because the pieces are already half-built:

| Design element | Today | Move |
|----------------|-------|------|
| Installation = a designed component | `NavalAcademyAtb : IComponentDesignAttribute` with design-tunable `ClassSize`, `TrainingPeriodInMonths` (`NavalAcademyAtb.cs:9-12`) | **Connect** — generalize off "Navy only" |
| Design knob: quality via training investment | `TrainingPeriodInMonths` already shifts graduate quality via a bell-curve mean (`NavalAcademyProcessor.cs:31`) | **Exists** |
| Design knob: which type/tier it produces | academy hardcodes `CommanderTypes.Navy` | **Small build** — make type/tier a design field |
| "More valuable resources → better output" | component is built from materials; `CostPerDay` pattern on `ResearcherDB` | **Connect** — add a per-cycle material draw |
| Modifier: colony populousness | `ColonyInfoDB.Population.Values.Sum()` | **Exists** — plugs in |
| Modifier: colony development level | ❌ no colony-wide stat (only per-hex `HexTile.InfrastructureLevel:47`) | **Build one accessor** (see below) |
| Modifier: a leader assigned to *teach* | the scientist-seated-at-a-lab pattern (`AssignScientistOrder` + `ResearcherDB.ScientistId`) | **Connect** — copy assignment + the `RefreshModifiers` competence read |
| "Takes a fraction of citizens" | `ColonyManpowerDB.TalentPool` (pop × 0.005) + `AvailableTalent`/`CommitTalent` — **purpose-built for "officers, scientists, governors," zero consumers today** (`ColonyManpowerDB.cs:15,27,61`) | **Connect** — the academy is the first thing to draw talent |
| Output: leaders with *inherent value* (competence) | ❌ graduates get only an `Experience` int; `BonusesDB` is **empty** | **THE core build** — the competence generator |

**Two purpose-built sockets nobody has plugged into:** the **talent pool** (a scarce population slice literally documented for officers/scientists/governors, never drawn) and the **empty `BonusesDB`** on every officer (the competence slot rung 4 already reads). Your academy is the first consumer of both.

**The one core new mechanic — the competence generator:** at graduation, **roll the leader's `BonusesDB` values**, scaled by `(design tier + material investment) × populousness × development level × teacher competence`. That single roll IS "the leaders produced and their inherent value." It emits into the shape (`BonusesDB`) the rest of the pipeline already consumes.

**The roll's math (reuses existing machinery).** Each graduate rolls a competence `score` (0–100) on a bell curve — reusing `NextBellCurve(RNG, floor, ceiling, mean, stddev)`, already used for `ExperienceCap` (`NavalAcademyProcessor.cs:31`). The four inputs **shift the mean** (and tier sets the ceiling); they do **not** flat-add to the result:

```
mean =  BaseMean(designTier)          // school ~30 · college ~50 · university ~70   [DESIGNABLE, dominant]
      + MaterialInvestment(materials) //                                            [DESIGNABLE]
      + Populousness(pop)             // log-scaled, diminishing                     [modifier]
      + Development(colonyDevelopment) // the ColonyDevelopment accessor             [modifier]
      + Teacher(teacherCompetence)     // capped/diminishing (runaway control)       [modifier]
score = NextBellCurve(RNG, floor, ceiling = TierCap(designTier), mean, stddev)
```

- **Mean-shift, not flat-add**, so variance survives: the odd prodigy from a poor school, the odd dud from a university. Loaded dice, not rigged — keeps "good officers are scarce" real.
- `score` drives two outputs: the **`BonusesDB` value** (inherent competence, rung 4 reads it) and the **eligibility ceiling** (which rung they qualify for, hard-capped by tier).
- **Quantity vs quality is a design choice:** `ClassSize` sets *how many*; the mean sets *how good* — a mass academy (big class, low mean) vs an elite one (small class, high mean).
- **Diminishing returns on the three soft modifiers** so none dominates and the teacher loop can't runaway (the contract also locks the teacher in place).
- **v1:** one primary competence bonus (`FilterId` = the pillar); secondary aptitudes / traits parked. The roll is *starting* competence; retraining (rung 5) adds more over a career.

**Tier = ceiling, competence = value.** The installation *tier* (school → college → university) sets the **highest rung** a graduate is eligible for; their rolled competence + record decides how good they are and which seats they qualify for. (The `COLONY-PROGRESSION` tier ladder, when built, is the natural backing for a higher academy tier on a more-developed world.)

**Colony development level — one accessor, swappable backing.** No colony-wide development number exists (only per-hex `InfrastructureLevel`). Build a single `ColonyDevelopment` accessor that **aggregates the hex infra levels now**, and is forward-compatible with the `COLONY-PROGRESSION-DESIGN.md` tier ladder later (the tier becomes a factor inside the same accessor). One figure, never two competing ones (`CONVENTIONS.md` §6). Its grave rung matches the progression doc's open question — a bombarded world both *demotes* and *starves its leader pipeline*.

**The installation's own cradle-to-grave (vertical):** designed → built from minerals/materials → installed → draws talent → produces leaders → **destroyed** (bombardment / captured when the planet falls) → **the leader pipeline goes dark.** That grave rung is strategic — hitting an enemy's universities starves their *future* leadership, wiring this system straight into ground combat and orbital bombardment.

### Rung 3 in depth — "Seated" (command capacity you build up)

Delegation is never free — you **earn** command capacity by building it, which is the span-of-control limiter that stops "delegate the whole empire on turn 1."

- **Seats come from components built onto installations** (the command components — HQ, flag bridge, sector capital). Generalizes the existing `AdminSpaceAtb` (a component that grants seats at an `AdminLevel`).
- **You start from nothing. Your first HQ grants 1 seat.** Every position must be **filled as you progress** — more command infrastructure → more seats → more of the empire you can delegate. Total hands-off is a thing you *build toward*, not a default.
- **`FundingLevel` (0–5) = a spend dial** — crank it up and the post runs harder at higher daily cost. Same knob for every pillar (labs → points, governors → budget, spymasters → ops, admirals → readiness).
- **Grave-rung tie-in:** because a seat is a built component, destroying it (decapitation, rung 6) collapses the seat and its delegation. Command infrastructure you build is command infrastructure the enemy can blow up.

### Rung 4 in depth — "Acts" (the competence → outcome pattern, generalized)

The scientist→research chain is the one place this works end-to-end; every leader reuses its four parts:

1. **The target is a `ModifiableValue<T>`** — the number the leader moves (research: `ResearcherDB.PointsPerDay`).
2. **A `Refresh…Modifiers()` method** folds modifiers in priority order: `base → funding(×) → local bonus → the seated officer's BonusesDB` (matched by `FilterId`) — `ResearchProcessor.cs:292-333`.
3. **Consume reads `GetValue()`** at use-time (`:87`).
4. **Rebuild is event-driven** (assign / unassign / funding / stance change), **not per tick** — cheap, which matters for the AI-cost bill.

**Two flavors of "Acts" — keep them distinct:**
- **(a) Passive competence bonus** — the officer makes a standing number *better*, continuously (Governor → legitimacy). Pure `ModifiableValue` + `Refresh`. No orders.
- **(b) Active orders** — the officer *issues orders* per their stance (Admiral moves fleets, Spymaster runs ops, Governor queues builds). This is what fills the dead `NPCDecisionProcessor` stub.

Competence modulates *how well* both go; the **stance** decides *what* the active ones do.

**The two-layer modifier model (already half-built):** *local post competence* (the seated officer's `BonusesDB` folded into the target) vs the *empire-wide modulator* applied after `GetValue()` (research already does this: `× GovernmentTools…ResearchMultiplier()`, `:91`). That maps exactly onto our hierarchy — the **leader's competence** is the modifier; the **Head of State's regime** is the modulator on top.

**Per-pillar targets** (name the `ModifiableValue`, copy `RefreshPointModifiers`):

| Leader | Target the competence moves | Passive / Active |
|--------|-----------------------------|------------------|
| Chief Scientist / Lab | research points | passive (built) |
| Governor | colony legitimacy (`GovernorCompetence` slot) + build orders | both |
| Fleet Commander | fleet firepower/toughness/speed (`FleetDoctrineDB`) + move/engage orders | both |
| System Admiral | which fleets move where | active |
| Spymaster | op success / counter-intel + op orders | both |
| Foreign Minister | deal quality / relation drift + treaty proposals | both |
| Trade Minister | trade income & route efficiency + route orders | both |
| Academy teacher | the graduate competence roll | passive (a rung-4 act pointed back at rung 1) |

The same code path runs whether the actor is player, delegate, or NPC — a seated delegate's `Refresh` runs automatically; the player dropping in sets the value directly. One implementation, not two.

### Stances — the decision surface

A **stance is a small, named bundle of standing orders** the player or NPC picks — *presets, not sliders*. The stance **is the decision the leader owns**; competence is only how well it's executed.

**Templates already shipped:**
- **`FleetDoctrineDB`** (built) — families Offensive / Defensive / Utilitarian + ROE (WeaponsFree / Hold / ReturnFire) + a **switch cooldown**.
- **`GroundFormationDoctrine`** (built, **data-driven**) — stances defined in `groundStances.json` as `GroundStanceBlueprint`s. **This moddable-JSON-catalog pattern is the one to reuse for every pillar** — a stance = coefficient tweaks + a behavior rule, defined in data.
- **`DoctrineVector`** on `FactionInfoDB` (four floats: Economic / Military / Tech / Expansion) is the NPC "personality" that **biases which stance an NPC picks**. Filling the `NPCDecisionProcessor` stub = *DoctrineVector biases stance choice → the seated delegate executes it → emits orders through rung 4.*

**Example preset sets (3–4 per pillar, from the design docs):**

| Leader | Stances |
|--------|---------|
| Governor | Growth · Industry · Fortress · Balanced |
| System Governor | Breadbasket · Mining Hub · Fortress |
| Fleet Commander *(built)* | Offensive · Defensive · Utilitarian + ROE |
| Planetary General | Hold Ground · Counter-attack · Fighting Withdrawal |
| Foreign Minister | Seek Allies · Isolationist · Expand by Tribute · Keep the Peace |
| Interior Minister | Favor Stability · Favor Military · Low Taxes · Balance the Blocs |
| Spymaster | Steal Tech · Counter-intel · Destabilize [rival] · Keep Us Informed |
| Trade Minister | Maximize Income · Stockpile Strategic · Free Trade · Autarky |

**Three points:** (1) stance = the stacking *decision*, competence = *texture* — keeps every seat honest against the realism firewall; (2) the existing switch **cooldown composes with contracts** — a leader on contract runs their stance for the term, no per-tick flip-flopping (good for feel and the AI-cost bill); (3) whether stances **re-skin by government type** (a dictatorship's "Favor Stability" reaching for harsher tools) is the one open call — the `GovernmentDB` dial hook to do it already exists.

### Two empire-level modulators — race and government

Beyond the leader's own competence, two empire-wide factors shape the whole pipeline. Both are the **modulator layer** (applied on top, like the regime multiplier on research) — not per-leader stats:

- **Race (species setup)** — sets leader **lifespan** (rung 6 mortality) and biases **doctrine tendencies** (which stances an NPC of that race leans toward, rung 4). Different species live and lead differently. Wires to the species system (`ColonyInfoDB.Population` is already per-species; verify/define the trait hooks — lifespan, doctrine lean — when we build).
- **Government (regime)** — **renames and re-tools stances**: a communist state's stances are named and behave differently from a republic's (rung 4). Uses the `GovernmentDB` dial hook.

This is why stance content and mortality aren't fixed constants — they're modulated by *who you are*.

### The retraining loop (rung 1 + rung 5, merged)

Leaders don't improve on their own (passive `Experience` is dead). Instead, **a leader can be sent back to school to gain more modifiers** — re-enrolling in an academy as a student. This is a deliberate, costly decision: it takes a school slot, costs time, and pulls the leader **out of service** while they train. It reuses the academy mechanic wholesale (the leader is a student again), gives academies permanent relevance beyond first spawn, and makes competence growth something you *invest in and plan around* — never automatic. This is how "Improves" is delivered.

### Contracts — the universal commitment term (and the runaway fix)

The teacher-feedback loop (a great leader teaching → better graduates) would spiral if you could cycle your best officer through the academy every year. The fix generalizes into the connective tissue for rungs 3→6:

**Every assignment is a fixed-term contract.** When a leader is seated — as a teacher, a governor, an admiral — they're committed for a term and **locked in place** for it.

- Caps the teacher loop (a star teacher can't be cycled).
- Makes "which leader, which post" a **durable strategic choice**, not per-turn optimization — parking your one genius admiral at the war college for a decade means he's *not* on the front for a decade. Real opportunity cost.
- **Net-new but small** — today's assignment orders are instant with no duration; add a term-end date + an early-break cost (payout / morale hit / reputation ding). **Default term: 5 years** (calibrate later).
- Preserves the governance rule "dropping in for one decision never un-seats the delegate" (you take the conn without breaking the contract).
- Quietly helps the performance bill — leaders don't get reassigned every tick.

Navy framing: a commission/enlistment term. You don't PCS a department head every week; cutting a tour short is a real event with real cost. "Back to school" is that officer going to the War College mid-career — off the watch bill, back sharper.

### Rung 6 in depth — "Lost" (the rung that makes the player care)

**This is the load-bearing rung for the whole design's *feel*.** A leader you can't lose is just a permanent modifier — and modifiers don't get names, don't get protected, don't get mourned. A leader you *can* lose — killed, captured, turned, retired — is someone the player invests in, escorts, and grieves, and someone whose enemies they hunt. **Loss with real cost is what converts a stat into a character the player cares about.** Everything below serves that.

**The reframe — one event, one handler.** Every way a leader dies is just a *producer* of a single **`LeaderLost` event**; **one handler** consumes it — vacate the seat, drop the competence, collapse the delegation. The event effectively **already exists and is unconsumed**: `DestroyCommander` publishes `EventType.CrewLosses` (`CommanderFactory.cs:115`) and *nothing listens.* So the spine is cheap: add the one subscriber.

**Prerequisite (and it fixes a real bug).** Today `DestroyCommander` clears **none** of the back-references — not `CommanderDB.AssignedTo`, not the seat's `CommanderID`/`Commander`, not `ShipInfoDB.CommanderID` — and `AdminSpaceProcessor.CalcEntityAdminSpace` **rebuilds the seat list from scratch each pass with `CommanderID = -1`** (the code flags it: *"need to check if we want that"*). So seating isn't durable and death leaks dangling references. **Fix first:** make seat occupancy durable (reconcile, don't reset) and route every removal through the one vacate handler. This is a prerequisite for rung 3 being durable and the Governor slice too — it pays for itself as a bug fix before it's a feature.

**Current state of the four causes:**

| Cause | Today |
|-------|-------|
| Killed (ship) | 🔸 Partial — captain killed via `DestroyShip`→`DestroyCommander` (`ShipFactory.cs:247-251`); only the captain; leaves dangling refs |
| Killed (colony bombardment) | ❌ absent for leaders |
| Captured (planet falls) | ❌ absent — capture only flips `FactionOwnerID` (`GroundForcesProcessor.cs:528`, "deeper transfer later"); seated officers orphaned |
| Turned / defected | ❌ no code |
| Died / retired (age) | ❌ commanders are immortal — no aging/lifespan/mortality anywhere |
| Decapitation (seat-host destroyed) | ❌ seat silently stops regenerating |

**The four causes, each a producer of `LeaderLost`:**
- **Killed** — *ship* exists (extend past the captain, route through the handler); *colony bombardment* hooks a casualty roll into `DamageProcessor.OnColonyDamage` (already exists). → **combat + damage.**
- **Captured** — the "deeper transfer later" hook. A captured leader becomes a **prisoner**: intel windfall + bargaining chip (ransom / exchange). v1 = removed + intel chance; full = a diplomacy sub-game. → **ground combat + diplomacy.**
- **Turned / defected** — the espionage grave rung (already named in `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`). Clean defection (they leave) or a **mole** (stays seated, leaks/sabotages until counter-intel catches them). → **espionage + the Information Ledger.**
- **Died / retired** — give commanders a lifespan and a mortality/retirement processor. Ties to **contracts** (a term can end in retirement) and keeps the **academy permanently relevant** — rungs 1 and 6 are the loop: leaders are born and lost, forever. Without it you accumulate immortal god-officers.

**What "collapse" means (the stakes the handler enforces):** the scope loses the competence bonus (rung-4 `Refresh` re-runs, the `ModifiableValue` drops); a *delegate* seat's delegation **collapses** — reverts to hands-on for the player, or **rudderless for an NPC** (stops executing its stance, loses coordination); the seat empties → back to rung 1 to appoint/train a replacement. A master admiral killed = years to replace. It is *felt*.

**Decapitation — the vertical grave rung.** Seats come from a **built command component** (`AdminSpaceAtb` — flag bridge / HQ / sector capital). Destroying it should publish `LeaderLost` for every commander under it → same handler → the delegation it held falls apart. Kill the enemy flagship or bomb their sector capital and their fleets/colonies go rudderless until re-seated. Makes command infrastructure a **target**, gives "who's holding this together" teeth, and closes the `AdminSpaceAtb` component's own cradle-to-grave.

**The gameplay it unlocks (earns-weight check):** *protect yours* (don't post your best governor on a frontier world; escort the flagship; garrison the capital), *hunt theirs* (assassination, decapitation strikes, turn their best officer into your mole), *the churn* (death/retirement keeps every good officer genuinely scarce and the academy always in play).

**Resolved (2026-07-06):**
- **Turned leader → both outcomes, decided by `RNG × counter-intel × leader stats`.** A defection attempt can land the leader as a **mole** (stays seated, feeds the enemy), a **clean defection** (they leave), or a caught/failed attempt — the roll and your counter-intel decide which. Not a fixed v1 pick; it's a probabilistic outcome.
- **Captured leader → both, same model.** Killed / prisoner (intel + ransom) / escape is decided the same way (`RNG × counter-intel × leader stats`).
- **Ground-formation leader loss → leave the no-penalty reassign, but notify the player** when it happens (visibility, no mechanical change).
- **Mortality → race-dependent lifespan** (see the race modulator above); no single global number.

### First vertical slice — the Governor (proves the whole pipeline)

Cheapest end-to-end proof, because the grave-end target already exists and is already dead-wired for it: `LegitimacyDB.GovernorCompetence` is a `0..1` slot feeding a `×15` legitimacy bonus that **nothing has ever written to** (`LegitimacyDB.cs:88-92,128,138`), and `AdministratorDB` is already a near-identical copy of the working `ResearcherDB`. The slice: **generate an officer with rolled competence (build rung 1) → seat via the existing `AdminSpace` order (rung 3, done) → copy `RefreshPointModifiers` to write competence into `GovernorCompetence` (rung 4) → watch legitimacy move.** It forces the reusable pieces into existence; after that, the System Admiral, Spymaster, and Trade Minister are the same wiring pointed at a different `ModifiableValue`.

---

## Connections (Prime Directive)

- **`docs/GOVERNANCE-AND-DELEGATION-DESIGN.md`** — the mechanism this doc rides on. That doc is the "one delegate shape + span of control"; this doc is the full roster, the AI-self-play framing, and the cost analysis. Keep them in sync.
- **`MasterTimePulse` / the GlobalManager keystone** — the shared prerequisite for every empire-level (cabinet) delegate; already in place. Colony/fleet delegates ride processors that already fire.
- **People / Commanders** (built, no bonus fields) — delegates ARE commanders; the skill-bonus gap must be closed (mirror the `Scientist`/`BonusesDB` path). The academy supplies them.
- **Leader academies (rung 1)** — generalize `NavalAcademyAtb` (`People/`) off Navy-only; first consumer of `ColonyManpowerDB.TalentPool` (the unused talent draw); emits competence into the empty `BonusesDB`. Feeds every seat above.
- **Colony development** — a new single `ColonyDevelopment` accessor aggregating `HexTile.InfrastructureLevel`, forward-compatible with `docs/COLONY-PROGRESSION-DESIGN.md`'s tier ladder. Read by the academy competence roll; a bombarded world demotes it AND starves the pipeline (shared grave rung).
- **Legitimacy / colony happiness** (`Colonies/LegitimacyDB.cs`, `ColonyMoraleDB.cs`) — the Governor's `Acts` target: the dead `GovernorCompetence` slot (`×15` bonus) is the first payoff to wire. Legitimacy → `RebellionDB` is the downstream stake.
- **Contracts** — a net-new fixed-term on assignment (extends `AssignAdministratorOrder`); the commitment layer for rungs 3→6; caps the teacher loop and throttles AI reassignment churn.
- **Loss (rung 6)** — one `LeaderLost` handler (consume the already-published, unconsumed `EventType.CrewLosses`) vacates the seat + collapses delegation; producers wire in from **damage** (`DamageProcessor.OnColonyDamage` casualty roll), **ground capture** (`GroundForcesProcessor.TryCapturePlanet`), **espionage** (turn/defect), and a new **mortality/retirement** processor. Fixes the dangling-reference bug in `DestroyCommander` + the seat-reset in `AdminSpaceProcessor`.
- **Combat / fleets** (`docs/COMBAT-DESIGN.md`, `docs/FLEET-COMBAT-CLOSING-DESIGN.md`) — the Admiral chain and Fleet Commander are delegates over the already-built doctrine/auto-resolve.
- **Ground combat** (`docs/GROUND-COMBAT-MAP-DESIGN.md`, `docs/HEX-GROUND-AND-ORDERS-DESIGN.md`) — the General chain sits over the built formation/doctrine layer; the person-commander is the net-new wrapper.
- **Diplomacy / espionage / internal politics** (`docs/DIPLOMACY-DESIGN.md`, `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`, `docs/GOVERNMENT-AND-POLITICS-DESIGN.md`) — Foreign Minister, Spymaster, Interior Minister are delegates of this exact shape.
- **Survey / detection** (`docs/DETECTION-DESIGN.md`) — the survey fog is the free LOD brake; the sensor O(n²) risk is the performance landmine to watch.
- **Commerce** (`docs/RESOURCES-AND-MATERIALS-DESIGN.md`, `docs/SPACE-STATIONS-DESIGN.md`) — the Trade Minister is blocked on the trade-money wire.
- **Performance** (`docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`, `Benchmarks/`, `PerformanceReadoutSmokeTests`) — the scaling gauge is the first build.

---

## Locked vs. open

**Locked this session (2026-07-06):**
- **Delegation = NPC AI** — one system runs the player's hand-off and the NPC's brain; no separate AI path.
- **The 19-role roster** and its two-chain (administrative + operational) structure.
- **Head of State holds the regime directly** (government type + empire legitimacy) — no empire Interior Minister.
- **Interior Minister and Planetary General report under the Planetary Governor**; **System General reports under the System Governor**; **the System Admiral (mobile) stays on the naval operational line and only coordinates with the System Governor.**
- **No leaders for leaders' sake** — every seat must own a distinct decision; competence is texture, not the justification.
- **Ship Captain cut** (a lone ship is a fleet of one); **company/unit commander deferred to v2** (keeps the AI-seat count honest).
- **One six-rung people pipeline** (born → skilled → seated → acts → improves → lost) for all 19 roles — build once, prove on the Governor, reuse.
- **Leaders are produced by academy installations** (rung 1) — generalize `NavalAcademyAtb`; draw the (unused) talent pool; the **competence generator** rolls `BonusesDB` from `(design tier + materials) × populousness × development × teacher`. Tier = eligibility ceiling; competence = value.
- **One `ColonyDevelopment` accessor** — aggregates hex infra now, tier-ladder-ready later (never two competing development numbers).
- **Retraining loop replaces passive XP** (rung 5) — leaders improve only by deliberate, costly re-enrollment.
- **Contracts = the universal commitment term** — every assignment is fixed-term; caps the teacher loop, makes seating a durable strategic choice.
- **Rung 4 "Acts" pattern** — `ModifiableValue` + `Refresh…Modifiers` reading `BonusesDB` (copy the research chain); the two-layer split (leader competence = modifier, regime = post-`GetValue` modulator); passive-bonus vs active-orders as the two flavors.
- **Stances = data-driven presets** (reuse the `GroundStanceBlueprint`/JSON-catalog pattern), biased for NPCs by `DoctrineVector`; stance = decision, competence = texture.
- **Competence roll = mean-shifted bell curve with a tier-gated ceiling** (`NextBellCurve`); inputs shift the mean, not the result.
- **Rung 6 = one `LeaderLost` event + one vacate/collapse handler** (leaders aren't modifiers — the player must be able to *lose* them). Four causes (killed / captured / turned / died); seat-durability fix is the prerequisite; decapitation collapses a whole scope.
- **Rung 3 seating = built command capacity** — seats come from components built onto installations; you start from nothing, the first HQ grants 1 seat, and every position is filled as you progress. `FundingLevel` = a 0–5 spend dial (output↑, cost↑) on any post.
- **Two empire modulators — race and government.** Race sets leader **lifespan** and biases **doctrine tendencies**; government **renames and re-tools stances**. Both are the modulator layer, not per-leader stats.
- **Contracts default to a 5-year term.**
- **Rung 6 outcomes are probabilistic, not fixed picks** — a turned leader lands as mole / clean defection / caught, and a captured leader as killed / prisoner / escape, both decided by `RNG × counter-intel × leader stats`. Ground-formation leader loss keeps the no-penalty reassign but **notifies the player**.

**Open (decide when we build):**
- The **empire-scope ground ceiling** — a "High Command"/Field Marshal, a joint Supreme Commander over both, or nothing. **Deferred (nothing for now).**
- **The actual stance names** per pillar per government type (the fact that they re-skin by government is locked; the specific names come with the government content).
- **How race biases doctrine/stance selection** — the trait→lean mapping (locked that it's race-driven; the mapping itself is content).
- **The competence-roll numbers** — base means per tier, stddev, weights/caps on the three soft modifiers.
- **Contract early-break penalty** — the number/shape (term = 5y locked).
- **Race lifespan values** — per-species numbers.

**Sequencing notes (decided, deferred — do NOT build yet):**
- **Commerce / Trade Minister** — build the trade-money wire (and the Trade Minister) **after all of the above is established.**
- **Ruins / anomalies** — **completely in**, but it becomes **its own system**; note to develop later (not part of the leader pipeline build).

**Parked design threads (explored the roster, not yet the depth):**
- **Foreign Minister pillar** — the external-politics delegate (empire-wide + per-met-faction) with the Ambassador / Envoy / Agent field hands under it. Design pass owed; rides `docs/DIPLOMACY-DESIGN.md`.
- **Race / species trait system** — became load-bearing this session (it sets leader **lifespan**, biases **doctrine lean**, and plausibly **espionage aptitude**). Needs its own design pass; wires to the species system (`ColonyInfoDB.Population` is already per-species).
- **Spymaster / espionage pillar** — worked out 2026-07-07 (decisions in `docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` → Locked 2026-07-07): agents are full leader-entities; org = empire **Director of Intelligence** → per-faction **Spymaster** → agents; **counter-intel = same agent, target = self**; `SignalQuality` **CUT** (passive feed binary, gradient in the Ledger); Spymaster MVP = Ledger + passive + per-faction seat + `gather`, as the **second** leader vertical slice after the Governor. Open fork: **agent delivery — abstract vs physical.** Foreign Minister pillar (parallel structure) still owed a pass.

**Path forward (build order — after the shared prerequisites):**
1. **Build the scaling gauge** (Visibility Gate) — the faction/entity performance benchmark, before any AI logic.
2. **Finish the delegate substrate** (per the governance doc): generalize `AdministratorDB` → the universal delegate record → close the `CommanderDB` skill-field gap → **make seating durable** (fix the `AdminSpaceProcessor` seat-reset + the `DestroyCommander` dangling-reference leak; add the one `LeaderLost` vacate handler). This is the shared prerequisite for rungs 3 and 6.
3. **Rung 1 — leader academies**: generalize the academy, wire the talent draw, build the competence generator + the `ColonyDevelopment` accessor. This is the cradle that makes every later rung have something to seat.
4. **The Governor vertical slice** (rungs 1→4 end-to-end): rolled officer → `AdminSpace` seat → competence into the dead `GovernorCompetence` slot → legitimacy moves. Proves the whole pipeline.
5. **Contracts, retraining, and loss** (rungs 5–6) once one role is seated and acting: wire the `LeaderLost` producers (bombardment casualty roll, colony-capture, mortality/retirement, then espionage turning) so a leader can actually be *lost* — the rung that makes the player care.
6. **Fill the `NPCDecisionProcessor` stub** — translate the doctrine vector into real orders through the seated delegates.
7. **Level-of-Detail for distant empires** — the affordability lever, once there's a gauge to prove it works.
