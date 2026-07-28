# THE PLAN — making planetary gameplay FUNCTIONAL, ACCESSIBLE, and OBSERVABLE (2026-07-27)

**What this is.** The ordered, gauged, slice-by-slice route from "a deep ground engine nobody can watch"
to "a planetary war you can play and see." Produced by OPERATION GROUND TRUTH
(`OPERATION-GROUND-TRUTH-PROMPT.md`); the evidence run behind it is `docs/DOCS-AUDIT-2026-07-27.md`.

**Governed by:** `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` (the 27 rulings — they override every
other doc), `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the board, Layers 5–6),
`docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md` (the rules), `docs/MVP.md` (scope firewall),
`docs/REALISM-VS-GAMEPLAY-AUDIT.md` (weight firewall).

> **UPDATE 2026-07-27 (later the same day): the rulings-compliance matrix for #1–#18 LANDED** after the usage
> limit reset, and it moved real numbers. Rows below now marked **[V2]** were verified by that pass. Six of its
> findings changed this plan, and two changed the canon doc's *factual notes* (the rulings themselves stand) —
> see `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` § Consequences, corrections 1–4. The headline
> re-sizings: **#18 and much of #15 are CHEAP-WIRE, not medium** (the dials are already built and authored on
> all 25 doctrine entries — every caller is just a unit test); **#11 is data-authoring, not a build** (the
> single attribute already exists); **#9 has TWO free paths to kill**; and **#14 is wider than thought** (it is
> the only working move verb).
>
> **⚠ EVIDENCE HONESTY — read before trusting a row.** A 19-agent verification fan-out for this pass **died
> on the account usage limit**, so this plan is built from three sources and every row says which:
> **[V]** = verified first-hand this pass with `file:line`; **[A24]** = inherited from the 2026-07-24
> holistic audit, *not* re-verified here; **[?]** = needs verification before the slice is sized. Do not let
> an **[A24]** row be quoted later as if it were **[V]**.

---

## 0. FOR THE DEVELOPER — five questions, in plain English

Answer these and the plan hardens. Nothing below is blocked on them except where noted.

**Q1 — Ruling #21: what does capturing a planet actually take from the loser?** *(Still OPEN by your own
ruling — I have deliberately not decided it.)* Right now capture flips one number: the owner ID. Nothing
moves. The candidates are population, the buildings standing on the hexes, the mineral stockpile, the
designs/research, surviving units, and the infrastructure rating. My recommendation is to answer it as a
*shopping list with a condition on each* — e.g. "buildings yes but damaged; stockpile yes; population stays
but unhappy; research no." Each item is a separate small slice, so a partial answer is still useful.
**Scheduled as S12 and deliberately left unspecified until you rule.**

> **The decision aid you asked for now exists.** A full candidate-by-candidate inventory of what capture
> moves / destroys / ignores today is in `docs/DOCS-AUDIT-2026-07-27.md` §8 — 20 rows, each with its
> `file:line`. The short version: capture is **one statement**. Population, stockpiles, component/ordnance
> stockpiles, every building at full health, morale, legitimacy, rebellion state and the manpower pool all
> ride along **untouched**; the production queue rides along and then **stalls** (jobs the new owner cannot
> build are marked `MissingResources` — `IndustryTools.cs:131-135`, whose comment names colony capture as the
> reason); designs, research and treasury **do not transfer** (so the captor cannot rebuild what it took);
> and future tax income **does** follow the flip (`ColonyEconomyProcessor.cs:66`). Ground units keep their own
> owner — no surrender, no POWs, no disband.

**Q2 — the scenario start.** You ruled a stock New Game must raise no garrison and place no enemy (#27b).
The audit wanted a takeable target from the front door. Those look like they fight, but they don't —
**because the start you'd need already exists and is already on the main menu.** The button is called
**"DevTest"**, and it boots you plus two developed rivals (the UMF at war with Earth, and the Kithrin) with
every ground behaviour switch already on. **[V]** `MainMenuItems.cs:51`, `NewGameMenu.cs:895-911,979-986`.
So the question isn't "build a skirmish mode," it's: **do you want that button renamed and promoted into a
supported "Scenario / Skirmish" start?** My recommendation: yes — it's a rename plus menu copy, and it
satisfies #27b exactly, because your stock New Game stays clean. (If you'd rather keep DevTest as a dev toy
and have a separate authored scenario, that's a real build, and I'd want to know before S3.)

**Q3 — the combat tick VALUE.** You ruled we shorten it (#23); the number is still open. Ground runs
**hourly** today **[V]** (`GroundForcesProcessor.cs:29`). Space's precedent is a **5-second**
`CombatReactionStep`. My recommendation: mirror space — a fine step *while a battle is live*, hourly
otherwise — and start the fine step at **60 seconds**, not 5. Reason: 5 s on ground would multiply the
resolver's per-tick work by 720 across potentially many regions, and a minute is already fine enough that
a 10-damage-per-second gun does 600 per tick instead of 36,000. **FLAGGED balance value.**
> **⚠ SUPERSEDED — see C3 above.** A committed CI spec already pins **5 s** and asserts the
> 720-steps-per-hour and determinism invariants. **Read C3 and rule on 5 s, not on my 60 s.** And note C2:
> the tick cannot be shortened without the rate model in the same slice, or damage scales by the same factor.

**Q4 — mid-tick overkill.** When a target dies partway through a tick, does the shooter's leftover damage
roll onto the next target down the doctrine's priority list, or is it wasted? Your phrasing read as
*sequential, no waste*, and that's how I've written S8 — but that's **my interpretation of your words, not
a ruling**, and it ties to #18. Confirm it.

**Q5 — per-weapon fire rates.** ⚠ **NARROWED 2026-07-28 (audit §18e) — this is mostly already answered.** The
numbers **are** picked for any weapon from the shared designer: `RoundsPerSecond` is authored on the real base-mod
templates and the JSON's own description says it *"drives damage/sec and saturation"*, and
`GroundCombat/SpaceWeaponGround.cs:64-79` already turns it into damage-per-second. **The only weapons with no rate
are the flat-`Attack` garrison/base-mod units.** So the question you actually need to answer is just: *what rate do
those get?* — and the cheapest honest route below still answers it. #23 makes every weapon carry damage-per-second.
Nobody has picked the numbers **for that residue**. I don't want to invent five silently. Cheapest honest route: derive each from the existing flat
`Attack` value divided by the tick you choose in Q3, so the first build is *behaviour-identical* to today,
then tune from there. Say yes and S8 needs no balance decisions at all up front.

---

## 0b. ⛔ FOUR CRITICAL FINDINGS from the rulings matrix (added later on 2026-07-27)

These came out of the #19–#27 pass and they change the build, not just the paperwork. All **[V2]**.

### C1 — A LIVE BUG: after a capture, the AI can be pointed at invading its own planet · cheap-wire

`FactionInfoDB.Colonies` (`FactionInfoDB.cs:62`) is the registry the **entire** faction/AI layer reads. It is
only ever *added to* (`ColonyFactory.cs:104,226`) — **there is no removal anywhere**, and capture never
touches it (`GroundForcesProcessor.cs:1073` flips only `colony.FactionOwnerID`).

**Concrete failure:** A takes every region of B's world. B **still lists it** — so `DefendResolver` keeps
defending it and `FactionRollup.ColonyCount` keeps counting it. A **never lists it**, so A's `FactionState`
world-view never sees it. Then `MilitaryTarget.EnemyColonies` builds A's strike set by enumerating **B's own
`Colonies` list** (`MilitaryTarget.cs:106-129`, esp. `:120`) — and scores the planet **A already owns** as a
target to invade.

This is independent of ruling #21 (it is registry *hygiene*, not "what transfers"), so it is **not blocked on
Q1**. It is cheap. **Scheduled as S1b, right after the log.** Eight AI readers depend on that registry
(`FactionState.cs:50`, `NPCDecisionProcessor.cs:478`, `ConsolidateResolver.cs:55,109`,
`DefendResolver.cs:155,171`, `FactionRollup.cs:39,45,111`, `Espionage.cs:61`, `MilitaryTarget.cs:120`,
`GroundStartGarrison.cs:43`) — read them all before touching it.

### C2 — Shortening the tick, on its own, multiplies ground damage by the shortening factor

The salvo pool is **not** `deltaSeconds`-scaled: `pool = m.Attack * SalvoScale`, applied once per tick
(`GroundForcesProcessor.cs:487,491,520,543`). So RunFrequency 1 h → 5 s is **720× the damage**, not a finer
resolution of the same damage. Ammo drain (`AmmoPerSalvo_kg`) and infrastructure bombardment scale the same
way — while attrition, shield regen and the K3 closing step **are** properly tick-scaled. **The balance
inverts.**

**Consequence for the plan: the tick change and the rate model MUST land in the same slice.** "Shorten the
tick first as a safe step" is exactly the wrong move. S8 is rewritten accordingly.

### C3 — There is already a CI acceptance spec for the tick, pinning **5 seconds** — and I gave you the wrong number

`Resolver2DJointsSpecTests.cs:210,216,232,253` already pins a **5 s ground quantum**, the
**720-steps-per-hour divisibility invariant**, and fast-forward == watch at a fixed quantum — including a
proof that a variable 1 h step diverges. **No engine code implements it.**

**This corrects my own Q3 recommendation below.** I suggested 60 s on cost grounds without knowing a spec
existed. The honest recommendation is now: **follow the existing spec (5 s)** — it is already written, already
asserts the invariants, and diverging from it means changing a committed spec. If 5 s proves too expensive
once C2 is fixed, that is a *measured* decision to make later, not a guess to make now.

### C4 — The doctrine keystone is one function, and it is dropping the fields on the floor

The 25-entry catalog is authored data **nothing reads**. `CombatDoctrine`'s reader has **zero non-test engine
callers for 9 of its 10 functions**, and the one live path — `FleetDoctrine.TrySetDoctrine`
(`FleetDoctrine.cs:54-70`) — copies only `DoctrineId`, `Family`, `FirepowerMult`, `ToughnessMult`,
`SpeedMult`, `IsRetreat` and the cooldown. **Verified precisely 2026-07-27: FOUR fields are dropped with no
mention at all — `TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds`, `Pursues` — and a fifth,
`EngagementPosture`, is *deliberately* overridden** (the fleet's existing posture is preserved instead, with a
comment explaining that otherwise a doctrine switch would silently reset a fleet to WeaponsFree). So the
doctrine's authored posture is ignored too, but by design rather than by omission — worth knowing before
"fixing" it.

So rulings **#15, #18, #19 and #20 all currently resolve into a JSON file with no consumer.** The fix seam is
small and specific: route `FleetDoctrine.cs:58-64` through `CombatDoctrine.Effective*`/`ParsePosture`.
**This becomes D0 and it comes before D2/D3a/D3b** — without it, every doctrine slice is decorating a value
that gets discarded on assignment.

---

## 1. The two-minute version

The ground engine is real: an assembler that derives stats from parts, terrain-weighted pathfinding on a
wrapping planet, a deterministic resolver with armour and cover and fortification, real-metre weapon
ranges, a tactical AI, troop lift, and capture. CI proves those paths connect.

**It is not a game yet for exactly three reasons, and only one of them is "missing depth":**

1. **You can't watch it.** A ground battle **already stops your clock** — and then says nothing at all. The
   resolver has no log line, no event, no battle record anywhere in its 1,078 lines, and it deletes the dead
   on the way out. The clock halts, the UI opens a *space* battle report, and that report is empty. That is
   worse than silence: it's a gauge that lies. **[V]** `GroundForcesProcessor.cs:329-330, 334`
2. **The door sticks in two places** (not the front door — that's already open, see Q2): you can save a
   ground design and then **never reopen it**, and the ground panels won't even show up until you've made a
   ship design first. **[V]** `ShipDesignWindow.cs:166, 223, 592`
3. **Nobody has ever seen the whole chain run once, live.** CI cannot run the client. Everything
   client-side is compile-checked only. **[A24]**

**So the order is: make it watchable, then make it reachable, then make it deep.** Depth built on an
unwatched system is depth you cannot tune or trust. The first slice is the cheapest one in the plan and it
converts every later slice from guesswork into measurement.

---

## 2. THE DELTA LEDGER — the headline artifact

Three columns, because "built" has been hiding three different failures. **Functional** = it works and a CI
gauge proves it. **Accessible** = a player reaches it from the normal game, no DevTools, no workaround.
**Observable** = you can watch it happen.

| Capability | Functional? | Accessible? | Observable? | Evidence |
|---|---|---|---|---|
| Ground unit as designed components | BUILT_AND_GAUGED | **BUILT_INERT** — can't reopen a saved design; panels gated behind a ship design | BUILT_RUNTIME_UNVERIFIED | **[V]** `ShipDesignWindow.cs:166/223/592` |
| Penetration / per-shot energy | **BUILT_INERT** — only the 3 prebuilt templates carry them | MISSING on the assembled path | MISSING | **[A24]** + ruling #2 |
| Ground parts cost research | **PARTIAL** — the gate mechanism is real (cost 0 ⇒ instantly unlocked), but **17 of 22** ground templates author 0, **all 22 are start-unlocked**, the 5 that do cost all use the same `[Mass]*2` (no complexity relation at all), and the assembled **unit** design has no tech gate | n/a | MISSING | **[V2]** `ComponentDesigner.cs:130,206`, `GroundUnitAssembly.cs:354`, `ColonyFactory.cs:48` — count confirmed |
| Invalid design blocked from saving | **MISSING** — validity IS computed and displayed, but `SaveGroundDesign` never reads `r.Valid` and registration proceeds regardless, so an over-budget / unpowered / magazine-less design lands in `IndustryDesigns` as buildable | n/a | the warning shows | **[V2]** `GroundUnitAssembly.cs:264,352`, `ShipDesignWindow.cs:520,557,592` — **the disputed half is settled: it computes + displays, it does NOT block. cheap-wire** |
| Muster location (rally point) | **BUILT_INERT** — `DefaultRegionIndex` read at muster, never set non-zero | MISSING | MISSING | **[A24]** ruling #6 |
| Build a ground unit → field it | BUILT_AND_GAUGED | partial — rides industry, but destination is hardcoded region 0 | MISSING | **[A24]** |
| One build queue for units + buildings | **MISSING — there are FIVE live build paths**, not four: the `IndustryJob` line, a **second materials-free `LocalConstructionDB` queue**, the `GroundBuildQueueDB` tile side-car, the beachhead `BuildSites` list, and two instant free placement orders. Only the side-car carries a planetary destination; the only real progress bar is on the free queue | mixed | MISSING | **[V2]** `IndustryAbilityDB.cs:17`, `LocalConstructionDB.cs:16`, `GroundBuildQueueDB.cs:36` |
| Free build paths (**TWO of them**) | BUILT — the free "Build here" order **and** the `LocalConstruction` queue, which spends only `PointsPerDay` and **never** `ResourceCosts`, yet lists infantry/armor/artillery and raises real units. **No test covers that second queue.** The free order is also the **only** producer of a building that fortifies | **both reachable** (Colony Management → Construction) — that's the problem | n/a | **[V2]** `LocalConstructionProcessor.cs:33,50`, `ConstructionDisplay.cs:56,70` |
| Buildings occupy ground / are war-map objectives | **the "two attributes" premise is REFUTED — `GroundFootprintAtb` is ALREADY the single attribute** (its *presence* is the war-map-objective flag; its `TileFootprint` is tile occupancy; both read live). **The real gap is DATA: only **2 of 51** installation templates carry it *(denominator corrected 2026-07-28 — the old "26" counted the wrong thing)*** | — | drawn where authored | **[V2]** `GroundFootprintAtb.cs:27,31`, `GroundBuildings.cs:27,332` — **#11 is authoring, not a build** |
| Buildings built AFTER game start get a location | **MISSING** — no hook at production completion | n/a | invisible on the war map | **[A24]** |
| Employment (jobs) + colony power | **BUILT_INERT, wiring complete end-to-end, both inputs STRUCTURALLY zero**: **zero** base-mod templates declare `EmploymentAtbDB` (so `GetTotalJobs()` is always 0 and the morale term is skipped by a −1 sentinel), and `powerDemandPerCapita` is authored 0 in both strain nodes while `uef.json` has no strain node at all | n/a | MISSING | **[V2]** `EmploymentAtbDB.cs:17`, `PopulationProcessor.cs:74`, `ColonyMoraleDB.cs:134` — **cheap-wire, mostly JSON** |
| Semantic tile bonuses | **MISSING — no per-tile bonus mechanism of any kind.** The only terrain rules are `GroundTerrain.TerrainAttackMult` (combat, keyed on unit TYPE not on a building) and `HexMinerals.TerrainWeight` (deposit-seeding at generation). **`CityTile.Terrain` is populated and read by nobody but its copy-ctor and two tests** | MISSING | MISSING | **[V2]** `CityTile.cs:22`, `GroundTerrain.cs:82` |
| Regional march | **BUILT and WIDELY WIRED — it is the ONLY fully-wired planetary move verb**: live in the primitive, the order enum, the processor, the **AI tactical brain**, **both** client windows, the Site engine, and a save/load fixture. **FOUR coordinate systems coexist** (region, per-region hex, global cylinder, mini+sub-tile) and **no formatter prints the combined `(17,09)(22,47)` address** | reachable | MISSING | **[V2]** `GroundForcesDB.cs:293,755`, `GroundForcesProcessor.cs:913`, `GroundTacticalBrain.cs:201` — **deleting it is WIDE; the replacement must land first** |
| Mini-hex movement as an order | **MISSING** — mini coordinates are written ONLY by the engine's automatic spread + closing steps. No `OrderMoveToMiniHex`, no order-enum member, and the city-zoom click handler has only build/place/inspect branches — **no move branch** | MISSING | — | **[V2]** `GroundForcesDB.cs:185,290`, `GroundForcesProcessor.cs:665,863` |
| March readout (destination/distance/ETA/speed) | **PARTIAL — all four ingredients exist as engine state** (`Speed_kmh` populated at raise; `GlobalPath` + transit seconds give distance and ETA) but **ZERO are displayed**: the globe shows only a `»` glyph and the Battalions table's eight columns carry none of them. No accessor computes distance-remaining or ETA | n/a | **MISSING** | **[V2]** `GroundForcesDB.cs:91,202,204,656` — **cheap-wire** |
| Committed fight + retreat + break-away + pursuit | **Ground has NONE of the four — and walking out is FREE today: a region march simply removes the unit from the resolver roster.** Formations are data objects, so the space engagement lock cannot apply to them as-is. Space has the lock + retreat, but prices the exit with a hardcoded `const RetreatCasualtyThreshold = 0.5` while the doctrine's own `BreakAwaySeconds`/`Pursue` sit unread | MISSING | MISSING | **[V2]** `GroundForcesProcessor.cs:271`, `CombatEngagement.cs:48,1646` |
| Engage decision on real distance | partial — real-metre gate built (K1–K4, M2) | auto-engages on region-band share | MISSING | **[A24]** rulings #19/#23 |
| Doctrine as the steering wheel | **catalog BUILT_AND_GAUGED (D1/D1b, 25 entries)**, behaviour fields read by **nothing in the resolvers** — **but the dials for #18 (TargetPriority) and #15 (BreakAwaySeconds/Pursue) are already BUILT, PARSED and AUTHORED on all 25 entries; every caller is a unit test.** Ground fire is still spread across all reachable enemies weighted by *current health*, so a cripple is never finished | assignable in formation management | MISSING | **[V2]** `TargetPriority.cs:14`, `CombatDoctrineBlueprint.cs:66`, `GroundForcesProcessor.cs:408,422` — **large parts of D3b are CHEAP-WIRE** |
| Leader-modulated doctrine | substrate exists on the SPACE retreat path; **ground has no read** | n/a | MISSING | **[A24]** |
| Calculated fire rate | **MISSING** — weapons carry a flat `Attack`, applied once per **hour** | n/a | MISSING | **[V]** `GroundForcesProcessor.cs:29` |
| Wound model (`CasualtyTier`) + battle-stats ledger | **MISSING** (designed, not built) | n/a | MISSING | ruling #22 **[?]** confirm zero |
| **Sim-health gauges** (is the engine even alive?) | **BUILT_INERT / MISLEADING** — SIM-STALL can't fire for a faulted task, `[HANG]` watches the UI thread, the fault tally counts render/input only and printed `faults=0` against 7 `[FATAL]`s | n/a | **actively misleading** | **[V2]** `MasterTimePulse.cs:49`, `SessionLog.cs:158`, `PulsarMainWindow.cs:485` — audit §9/G1 |
| **Heartbeat counters** (sensor scans / battle-trigger passes) | **PLACEBO** — both increment *before* the early-return, so they climb to millions on an empty galaxy. A climb proves only "the hotloop is scheduled" | n/a | misleading | **[V2]** audit §9/G3 |
| **Ground UI telemetry** | **MISSING** — `PlanetViewWindow.cs` has ZERO `SessionLog` calls, so a play-test cannot prove the surface was opened | n/a | MISSING | **[V2]** audit §9/G7 |
| **`[FleetCombat]` as a battle channel** | **REFUTED** — 3 emitters, all client button handlers; it is a player-input echo and can never confirm a battle | n/a | mis-documented | **[V2]** audit §9/G9 |
| **AI actually prosecuting a war** | **BUILT_INERT in practice** — UMF returned "no legal step" on **135 of 144** cycles while at war with a transport built and its fleet over an undefended homeworld; never landed a soldier in 5 months | n/a | visible only in `[AI]` | **[V2]** audit §9/G6 — **not scheduled; needs its own investigation** |
| **Ground battle log / events / records** | **MISSING — zero emission anywhere in the resolver** | n/a | **MISSING, and the clock halts anyway → the interrupt lies** | **[V]** `GroundForcesProcessor.cs:329-330` + whole-file grep |
| Unit inspection (hover + Force Management) | **PARTIAL — and the first draft was RIGHT. My own "correction" to MISSING was the error; Phase B walked it back.** What EXISTS: the ground surface already shows **aggregated** strength — count + summed Health/MaxHealth per (faction × unit-type × region) (`PlanetViewWindow.cs:1081-1087`), a map token printing type-initial + count + a `»` moving marker (`:466`), and the formation panel printing live stance multipliers (`:1497`). Hover is already **detected** three times for click handling (`:295,296,791`). What is MISSING: any **per-INDIVIDUAL-unit** drill-down, and a single tooltip **call** in either ground window (`SetTooltip`/`BeginTooltip` = **0** in both `PlanetViewWindow.cs` and `PlanetaryWindow.cs`). **"Zero tooltips anywhere" was flatly false** — the client has **101 `SetTooltip` + 12 `BeginTooltip`** across 20+ files; the true claim is scoped to the *ground* windows. **⇒ RE-SIZED medium → cheap-wire:** the numbers and the hover plumbing are already there; the missing part is the tooltip body and a per-unit view | PARTIAL | MISSING (per-unit only) | **[B]** Phase B, 3 lenses — the operation's first genuine walk-back |
| Lost-contact fading marker | BUILT for space (`SensorContactIcon`) | — | **MISSING on ground** | ruling #26 |
| Ground behaviour flags | BUILT — but **process statics**; set on New Game (menu *and* DevTest), **never on load** | — | MISSING | **[V]** `GroundForcesProcessor.cs:62/72/85/100`, `NewGameMenu.cs:562-581/979-986` |
| A takeable enemy from the menu | BUILT | **ACCESSIBLE — the ungated "DevTest" main-menu button** (premise corrected) | — | **[V]** `MainMenuItems.cs:51` |
| Ruling #27b — no default garrison/enemy in a stock New Game | **DONE** — all three auto-spawns default `false` | n/a (by design) | — | **[V2]** `NewGameMenu.cs:52,55,60` — the one ruling already satisfied |
| Orbital bombardment of a colony | **BUILT_INERT** — no `BombardColonyOrder`, no button, no AI rung | Fire-Control workaround only | MISSING | **[A24]** audit P3 |
| Per-faction ground fog | BUILT engine-side | **client ignores it** — a rival's survey reveals your deposits | wrong | **[A24]** |
| Located hex deposits feed mining | **MISSING** — mining reads only the body-wide pool | n/a | drawn but not load-bearing | **[A24]** |
| Units cost people, permanently | **MISSING — and further off than the canon doc says: `GroundUnitDesign` has NO crew field at all**, and `GroundUnitAssemblyResult` doesn't even sum crew (its station/building siblings do). The build-time crew gate is ship-only; no death path touches population | n/a | MISSING | **[V2]** `GroundUnitDesign.cs:30` — ship/station manpower machinery is built + gauged, ground has nothing to connect yet |
| Ammo bites | BUILT_INERT — flat 1 kg/salvo, so a magazine is never a trade | n/a | MISSING | **[A24]** ruling #8 |
| Hazard counters as specific gear | **PARTIAL** — 5 hazard types / 8 menaces generated; only Vacuum + ToxicAtmosphere have a designable counter (`GroundSealAtb`). **The developer's own dust-storm example has nothing to counter: ground `SensorJam` is generated and read by NO ground code.** Heat/Corrosive resistance is C#-only, unreachable from the designer. And "one hazard hits several stats" is **not expressible** — `HazardEffect` carries one Type + one Magnitude | MISSING | MISSING | **[V2]** `PlanetEnvironmentFactory.cs:80`, `GroundForcesProcessor.cs:222,1048`, `HazardEffect.cs:54` |
| Per-mini-tile terrain (M4) | **MISSING** — every mini tile copies its coarse hex's terrain | n/a | drawn (as a copy) | Layer 5 M4 |
| Hex/tile naming | MISSING (fully specced, Layer 6) | MISSING | MISSING | Layer 6 |
| Capture transfers substance | **MISSING** — bare owner-ID flip | n/a | MISSING | ruling #21 — **OPEN** |

**The shape of it:** almost nothing in that table is *unbuilt machinery*. The failures cluster in the
**Accessible** and **Observable** columns, and in **data that ships as zero**. That is why this plan front-
loads wiring and gauges, not features.

---

## 3. Sequencing — the principle, and why

**Observable → Accessible → Steerable → Deep.** Justification, in order:

1. **Observability first, and it is not a preference — the clock already halts for a ground battle.** The
   most expensive thing in this project is a change whose effect nobody can see. Every later slice here
   (fire rate, doctrine steering, retreat, casualties) is a *tuning* problem, and you cannot tune what you
   cannot read. S1 is also the cheapest slice in the plan. **Do it first.**
2. **Accessible second**, because a runtime pass (the P2 sitting) is the only gauge CI structurally cannot
   provide, and it needs a reachable door plus a readable log to be worth running.
3. **Then the live sitting (M1)** — before any depth. Everything after it is deepening a system that has
   actually been observed working once, instead of one that merely compiles.
4. **Steering (doctrine) before fire-rate**, because "everything goes through the doctrines" is your frame:
   the fire behaviour in #23/#18 wants a doctrine priority list to fire *down*. Building the rate first
   means building it twice.
5. **Depth last**, in cradle-to-grave order, gated on the forced order #2 → #4 → #1.

**One hard pairing, do not break it:** ruling #9 (kill the free path) and ruling #10 (the one queue) **must
land in the same slice**, because the free path is today the *only* producer of a building that actually
fortifies. Ship #9 alone and ground defence silently breaks. **[A24]**

---

## 4. THE SLICES

Every slice: one push, one CI gate (**~33 min** — the `rest` shard is the critical path **[V]**, and
`ci.yml`'s complement filter puts every new fixture there, so each slice asks "does this fixture need its
own shard?").

Legend — **Size:** cheap-wire / medium / large. **Gate:** what CI asserts. **Reach:** the literal click
path. **See:** what proves it live.

### S0 — Settle the surface scale with a measurement, not an argument · cheap-wire
Three docs disagree (~560 km/47 km vs ~477 km/37 km). Diagnosed **[V]**: they measure different things —
37 km is `pitch ÷ 13` and is **what the resolver actually measures** (`GroundMiniHex.cs:28-34`,
`GroundForcesProcessor.cs:612/749`); 47 km is an area-equivalent no code uses. The coarse number depends on
Earth's generated hex count, which **cannot be settled by reading** (no SDK here, and two hex models
coexist).
- **Build:** a CI readout that prints real `HexPitchKm` + `MiniPitchKm` for Earth / Mars / Luna.
- **Gate:** the readout exists and the two are related by exactly ÷13.
- **See:** the numbers in the CI log. Then quote the measurement in all three docs.
- **Deps:** none. **Why first:** it's tiny, it pays a doc debt with evidence, and it's the Visibility Gate
  in miniature — build the gauge, *then* state the number.

### S1 — ⭐ THE GROUND BATTLE LOG (#24) · cheap-wire · **START HERE**
The keystone. Mirror the space pattern exactly **[V]**: `CombatEngagement.CombatLog()` gated on a static
`NarrateToLog` (`CombatEngagement.cs:352,452-455`; the client sets it at `PulsarMainWindow.cs:77`), plus
the unconditional structured trail in `Combat/BattleLog.cs`.
- **Build:** `[Ground]`/`[GroundCombat]` narration at engage / salvo / loss / region-flip / disengage, plus
  a structured record per event. Narrate the halt itself, so the clock stopping explains itself.
- **Design fork to decide in the slice (recorded, not guessed):** `BattleEvent`'s fields are ship-shaped
  (`FleetId`, `ShipsLost` — `BattleLog.cs:29-41`). **Recommendation: add a domain discriminator to
  `BattleEvent` and reuse `BattleLog`**, because the client's existing Battle Report already reads it — so
  ground fights show up in the report for free. That is the CONNECT move rather than a parallel system.
- **Two cautions, both flagged now:** `BattleLog` is explicitly **runtime-only, not saved**
  (`BattleLog.cs:66-68`) — if the after-action report must survive a save that's a separate save-safety
  decision; and `MaxEvents = 250` is sized for fleets, so a big ground fight could evict space history.
  **FLAGGED sizing value.**
- **Gate:** a fixture fights a ground battle and asserts (a) ≥1 record per phase, (b) records name the
  right formations and losses, (c) the narration flag off ⇒ byte-identical.
- **Reach:** Main menu → DevTest (or a menu game with a garrison) → advance until a ground fight → the
  fight narrates into `game_logs/`.
- **See:** `[Ground…]` lines in `game_logs/`, and events in the Battle Report.
- **Deps:** none. **Unblocks:** literally every tuning slice below.

### S1b — ⭐ FIX THE COLONY REGISTRY (finding C1 — a live bug) · cheap-wire
Capture flips `colony.FactionOwnerID` but **never updates `FactionInfoDB.Colonies`**, which has *no removal
path at all*. The loser keeps defending and counting a world it lost; the captor never sees it; and
`MilitaryTarget.EnemyColonies` reads the **loser's** list, so the captor can score its own planet as an
invasion target.
- **Build:** remove the colony from the old owner's registry and add it to the new owner's, at the capture
  site. Read all eight AI readers first (listed in C1) — several assume membership means ownership.
- **Gate:** capture a world, then assert (a) it leaves the loser's `Colonies`, (b) it joins the captor's,
  (c) `FactionRollup.ColonyCount` moves on both sides, and (d) **`MilitaryTarget` no longer returns the
  captured world as a target for its new owner** — that last one is the assertion that encodes the bug.
- **See:** the AI tape (`[AI]`) stops proposing an invasion of a world the faction owns.
- **Deps:** none. **NOT blocked on ruling #21** — this is registry hygiene, not "what capture transfers".
- **Why this early:** it is cheap, it is a real bug, and every AI decision downstream of a capture is
  currently made on a false world-view.

### S1c — ⭐ SIM-HEALTH GAUGES: make a dead simulation impossible to miss · cheap-wire
**From the log forensics (audit doc §9/G1) — the developer sat pressing play for 2.7 minutes at a dead sim
while every instrument read normal, then the game reported `faults=0`.** This is the Visibility Gate applied
to the thing that matters most: knowing the engine is alive.
- **Build four small gauges:** (a) a **dead-sim detector** that does not depend on `IsRunning` — that flag is
  derived from task completion (`MasterTimePulse.cs:49`), so a *faulted* task reads "paused" and the existing
  SIM-STALL check (`SessionLog.cs:158`) can never fire for this class. Detect a faulted/completed sim task
  directly, and say so loudly on the *first* frozen heartbeat, not on GC. (b) An **honest fault tally** —
  `SessionSummary()` counts only `_loggedRenderErrors` (`PulsarMainWindow.cs:485`), so it printed `faults=0`
  against 7 `[FATAL]`s; count every fault class. (c) **Log the silent auto-pauses** — the event-log
  `PauseTime()` on `NewHostileContact` (`FactionEventLog.cs:50`, from `SensorEvents.cs:39`) writes nothing, so
  a legitimate pause reads as an unexplained stop. (d) **Make the play button honest** — if the sim task is
  dead, say that instead of silently doing nothing.
- **Also (G7):** `PlanetViewWindow.cs` has **zero `SessionLog` calls** — the whole ground UI leaves no trace,
  so a play-test cannot prove the surface was even opened. Add open/close + hex-click/march lines.
- **Gate:** engine-side, a faulted sim is detectable without reading `IsRunning`. The client half is
  CI-compile-only → `CLIENT-TEST-CHECKLIST` rows.
- **See:** the next dead sim announces itself in one heartbeat instead of costing 2.7 minutes of confusion.
- **Deps:** none. **Why so early:** it is the gauge that makes every future live test trustworthy.

### S1d — Stop the AI re-ordering a fleet to where it already is (live bug, audit §9/G5) · cheap-wire
`ConquerResolver.cs:172` and `:202` guard the strike order with **only** `!FleetIsMoving(strikeFleet)`. A fleet
that has *arrived* is not moving, so it is re-issued a sail order to its current location every cycle — five
identical orders produced **six warp departures at 0 Gm**, and those co-located arrivals are what threw the
`Speed Result is NaN` that killed the clock. The NaN is patched at HEAD; **this cause is not.**
- **Build:** an at-target check beside the moving check (already-at-body ⇒ don't re-issue; the next rung
  should take over).
- **Gate:** a resolver-driven test — a strike fleet parked at its target gets **no** new sail order, and the
  order count stops growing across repeated ticks.
- **See:** `[WARP]` stops showing 0 Gm departures.
- **Note:** this does not fix G6 (the AI idling 135/144 cycles while at war and never landing) — that is a
  separate, larger investigation, recorded but not scheduled here.

### S1e — ⭐ FIX THE BLIND AI: the degenerate detection-quality read (audit §9/G10) · medium
**All 288 AI decisions in the real play log read `vs no threat`** — because every ship contact reports
`sig=0kW` while the star reports 1.4 M kW. `GreatestThreatTo` sums `SignalStrength_kW`
(`ThreatAssessment.cs:39`) → `LatestDetectionQuality.SignalStrength_kW` (`SensorContact.cs:54`), and that value
is zeroed even though the contact must have passed `> 0` at scan time (`SensorScan.cs:132`).
- **Why it outranks most depth work:** `CombatRisk.WouldEngage` deliberately returns **true** when the enemy
  estimate is non-positive (`CombatRisk.cs:41`) — a sensible fallback whose input is *always* zero, so **the
  AI's entire risk appetite never evaluates anything**, at the commit gate or anywhere else. Two treaty
  behaviours can never fire either.
- **⚠ ONE JUSTIFICATION WITHDRAWN (Phase B, my own overreach).** I wrote that this is *"the keystone prerequisite
  `DIPLOMACY-DESIGN` already names."* **It is not.** That doc **dissolved** its detection-quality keystone on
  2026-07-07 — the hidden-info gradient moved to the Information Ledger. **The slice still stands on its own
  evidence** (288 of 288 real AI decisions read `vs no threat`), it just is not a named prerequisite for anything.
- **⭐ A CONCRETE ROOT-CAUSE LEAD (Phase B — the plan said "unverified, start there, do not guess").** Two facts:
  `ThreatAssessment.cs:11` documents that it *deliberately* uses signal **STRENGTH** because the `SignalQuality`
  path was design-cut — so the field being read is the intended one, and it is the one reading zero. And
  `SensorTools.cs:69` carries a **commented-out** `if(detectionValue.SignalStrength_kW > 0)` guard, ~150 lines
  above the setter that assigns it (`:220 SignalStrength_kW = detectedMagnatude`). **Start at those two lines.**
  *(`SignalQuality` itself is NOT cut from the code — it is live in 9 files and gates survey reveal at 0.20/0.80,
  `SystemBodyInfoDB.cs:154-160`. Do not delete it.)*
- **⭐ ROOT CAUSE FOUND 2026-07-28 (Phase C re-sweep) — this is no longer "unverified, do not guess," and the
  slice changes shape: it is a DESIGN question, not a bug fix.** Two facts, both proven in source:
  1. **`SignalStrength_kW` is not loudness — it is a detection MARGIN.** Both assignment sites subtract the
     receiver's own noise floor: `SensorTools.cs:193` (`intersectPointY - recever.BestSensitivity_kW`) and `:197`
     (`signalWaveSpectraMagnatude_kW - recever.BestSensitivity_kW`). A ship at realistic range clears the floor by
     almost nothing ⇒ **~0**. A star clears it by a vast amount ⇒ **1.4 M kW**. *That is the whole observed
     symptom*, and it is not a wrapped byte or an uninitialised field.
  2. **`ThreatAssessment.cs:39` sums that margin believing it is size** — its own comment reads *"loudness = the
     fog-limited size proxy."* The AI is reading a threshold-clearance number as an order-of-battle estimate.
  **The doc chain that produced it** (worth reading before choosing a fix): `docs/combat/DETECTION-DESIGN.md`
  decided detection would *"collapse to strength only"*, and `docs/ai/AI-BRAIN-BUILD-TRACKER.md` (F-A1/F-B1) duly
  built the AI's eyes on **strength** — so the design deliberately routed all threat perception onto the one field
  whose semantics cannot carry it. Both docs now carry the correction.
  **A compounding factor, already documented and still open:** the per-band loop **overwrites** both
  `detectedMagnatude` and `quality` each iteration and returns whatever the **last** detectable band left
  (`SensorTools.cs:218-222`) — the "multi-band overwrite" quirk flagged in `Sensors/CLAUDE.md`. So a marginal band
  can clobber a strong one. `HighestDetectionQuality` (a max over time) smooths this; `LatestDetectionQuality`
  does not — **which is exactly why Latest reads 0 and Highest does not.**
- **Build (re-shaped):** decide what the AI's threat input should *be*, then wire it. Cheapest honest option: give
  the contact an explicit loudness field from the **pre-subtraction** `signalWaveSpectraMagnatude_kW`, leaving
  `SignalStrength_kW`'s margin semantics untouched so nothing else shifts. *(Taking the max across bands instead of
  the last is a separate, behaviour-changing fix that wants its own test — do not ride it on this slice.)*
- **⚠ Do NOT "fix" this by deleting `SignalQuality`,** which `DETECTION-DESIGN.md` still reads as an executed
  deletion. That field is **live in 9 files** and gates survey reveal at `> 0.20` / `> 0.80`
  (`SystemBodyInfoDB.cs:154-160`, `StarInfoDB.cs:130`). Its byte-overflow bug was already fixed (2026-06-28,
  CI-gauged by `SensorQualityTests`).
- **Gate:** a fixture with two detected hostile fleets asserts `GreatestThreatTo` names the rival with a
  **non-zero** strength, and that `WouldEngage` **returns false** for a hopeless attacker — i.e. the risk band is
  actually exercised, which no test does today.
- **See:** the `[AI]` tape stops saying `vs no threat` and starts naming a rival with a number.
- **Deps:** none. **Caution:** the same quality value drives planet/star survey reveal, so changing it touches
  detection *and* survey — map that blast radius before editing (Prime Directive).
### S1f — ⭐ The AI's garrison rebuild builds CARGO, not soldiers (live bug, audit §11/W14) · cheap-wire
`ConquerResolver.cs:388-396` queues a ground-unit job and **never sets `job.InstallOn`**. `InstallOn` is read at
`ComponentDesign.cs:70,73` and only installs the finished component when non-null; a ground unit is raised by
that *installation* firing `GroundUnitAtb`. There is no generic default either — the fallback in
`IndustryTools.cs:66-75` is **commented out**. Every player path sets it (`IndustryOrder.cs:165`,
`IndustryDisplay.cs:417,424`, `IndustryPanel.cs:302,377`) and so does the costed tile queue
(`GroundBuild.cs:63`).
- **Effect:** the AI notices a depleted garrison, spends the materials, and produces **a crate in cargo** — no
  soldier, forever. It then still reads as depleted, so it can do it again.
- **Build:** set `job.InstallOn = colonyEntity` at that call site. **One line.** *(Consider also un-commenting
  the generic default — but that is a wider blast radius, so treat it as a separate decision, not a freebie.)*
- **Gate:** drive the rung to completion and assert a **unit appears on the roster**, not an item in cargo.
- **See:** the `[AI]` tape's RebuildGarrison action followed by an actual garrison count increase.
- **Deps:** none. Pairs naturally with S1b/S1d/S1e — all four are small, concrete AI defects.
### S2 — The five behaviour flags into the save (#27a) · medium
Corrected premise **[V]**: they're set on the **normal** New Game path too (`NewGameMenu.cs:562-581`), not
just DevTest. The defect is that **loading a save never sets them**, so a save plays differently depending
on whether you started a new game earlier in the same process.
- **Build:** move the five onto saved game settings; New Game writes them, load reads them.
- **Landmine:** `TypeNameHandling.Objects` — **append fields to an existing settings blob** if one exists;
  a brand-new `*DB` introduces a new type name. **[?]** find the right home before building.
- **Gate:** save → load → the five flags match what the game was created with; and a CI run can now test
  the configuration players actually run.
- **See:** a one-line `[STATE]` dump of the five at load.
- **Deps:** none (independent of S1).

### S3 — Promote the scenario start (#27b + audit P1) · cheap-wire · **GATED ON Q2**
Rename/promote the existing ungated **DevTest** main-menu button into a first-class **Scenario / Skirmish**
start. No new machinery **[V]**.
- **Gate:** a `BaseModIntegrity`-style assert that the scenario start yields ≥1 hostile garrison and a
  takeable target, so it can't silently empty out later.
- **Reach:** Main menu → **Scenario** → you have rivals and something to take.
- **See:** the AI tape (`[AI]`) plus S1's ground log.

### S4 — Unstick the designer door (BREAK 3) · cheap-wire (client)
**[V]** `ShipDesignWindow.cs:166` lists only `ShipDesigns`; `:223` documents ground designs as
unreachable; `:592` registers them into `IndustryDesigns`.
- **Build:** list ground designs from `IndustryDesigns` in the picker; let the ground panels render without
  a pre-existing ship design.
- **Gate:** CI `build-client` compile + an engine-level assert that a registered ground design is
  retrievable by the same call the picker uses.
- **Reach:** Main menu → New Game → Entity Assembler → **Ground** → design → save → **reopen it**.
- **See:** local runtime only (CI can't run the client) → a row in `docs/CLIENT-TEST-CHECKLIST.md`.

### ⛳ M1 — THE LIVE CRADLE-TO-GRAVE SITTING (audit P2) · developer, on Windows
**Not a code slice — the milestone that makes everything after it honest.** After S1–S4: one recorded
sitting — survey → colonize → mine → design+build a unit → load → sail → win orbit → land → fight → capture
— capturing `console_output.txt` + `game_logs/`. Rows added to `CLIENT-TEST-CHECKLIST.md` and
`TESTING-TRACKER.md`. **Everything below is deepening an unproven system until this fires once.**

### S5 — ONE build queue, with destinations (#10 + #9 + #6 + #11) · large · **#9 and #10 TOGETHER**

> **[V2] RE-SIZED by the rulings matrix.** Three corrections: there are **FIVE** live build paths, not four;
> there are **TWO free ones to kill** (the "Build here" order **and** the `LocalConstruction` queue, which
> spends only `PointsPerDay`, never `ResourceCosts`, yet lists infantry/armor/artillery and raises real units
> — and **no test covers it**); and the fortification trap is sharper than stated: **`GroundFortification`
> reads only `Region.InstallationIds`, which the costed queue never writes**, so cutting the free path leaves
> a colony player with *no buildable fortification at all* — **and CI would stay green through it.** Write
> that list in this slice and gauge it. **#11 is NOT a build:** `GroundFootprintAtb` is already the single
> attribute (presence = objective, `TileFootprint` = occupancy, both read live). The gap is **data — only
> **2 of 51** installation templates carry it *(corrected 2026-07-28)*.**
Collapse the divergent build paths into one RTS-style queue where every entry carries its destination and
shows progress; delete the free "Build here" path; make muster a rally-point setting (#6); make "occupies a
tile" and "is a war-map objective" one attribute (#11).
- **Must-not-break:** the free path is the only current producer of a fortifying building **[A24]** — the
  costed queue must write the region list `GroundFortification` reads *in the same slice*.
- **Also closes:** the "building built after game start is located nowhere" hole **[A24]**.
- **Gate:** queue a unit *and* a building with destinations; assert both arrive at the named place, the
  building fortifies its region, and nothing can be created free.
- **Reach:** Colony → Production → queue → destination picker → watch progress.
- **See:** queue progress in the UI + a `[Build]` completion line naming the destination.

### S6 — ⛔ **REPLACED 2026-07-28 by the CANON MOVEMENT RULINGS (M1–M12)** — see the box below, then the slice

> # 🔒 S6 IS NOW "ONE LAYER, TWO ADDRESSES, ONE ORDER SURFACE"
>
> **Authority:** `docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` → the 2026-07-28 addendum. **Everything in the
> older S6 text below that assumes a region movement layer is void.** The governing law is **One Verb, Both Seats**
> (root `CLAUDE.md`): *if the AI cannot use a mechanic with the same primitive the player uses, it is too complex.*
>
> **⭐ The load-bearing discovery: this is mostly a DELETE, and the hard parts are already built.**
> - The **two-part address is the live distance function** — `GroundMiniHex.ContinuousPosKm(globalQ,globalR,miniQ,miniR,…)`
>   = coarse-hex km **+** mini-hex km, plus a second overload adding a real **sub-mini-hex offset**. Continuous **three
>   levels deep**, so a weapon range shorter than a 37 km mini tile decides a fight.
> - The **metre proximity gate is built and ON for menu games** — `WeaponReaches` → `CombatKernel.WithinReach(range_m,
>   RealGapMetres(…))` behind `EnableMiniHexCombat`.
> - **The coarse-hex edge problem is ALREADY SOLVED**, and the code says so (`GroundMiniHex.cs:70-71`): *"two units at a
>   shared coarse-hex edge read a small gap regardless of which coarse hex each is filed under."*
>
> **⛔ THE ONE REAL BLOCKER — the region bucket.** `GroundForcesProcessor` groups units into `byRegion` keyed on
> `unit.RegionIndex` (`:268-278`) and calls `ResolveRegionCombat` **once per bucket** (`:295-304`). Two units in
> different regions **never enter the same list**, so they are never compared — however close in metres. The metre gate
> only runs *inside* a bucket. **Deleting that bucket is what makes the planet seamlessly connected.**
>
> ### S6 splits into five slices, in this order
>
> | # | Slice | What | Size |
> |---|---|---|---|
> | **S6a** | **Unbucket combat** | Replace `byRegion` with proximity clustering on the continuous surface. **The keystone — do it first**; everything else is cosmetic until two units 1 km apart across a region line actually fight. | medium |
> | **S6b** | **One movement layer** | Delete `OrderMove` (the adjacency gate `Neighbors.Contains`, the `CrossingTimeSeconds ÷ speed` clock, `MovingToRegion` and its precedence guards) **and** the region-local hex march (`HexPath`). The global grid becomes the only layer. | medium |
> | **S6c** | **The two-part order verb** | One order type taking **(regional hex, mini hex)** — `MoveToHex` generalised — issued the **same way by player and AI** through the order queue. **Deletes the client's direct `OrderMoveToGlobalHex` call** (a One-Verb violation: no issuer marker, unsequenceable, AI-invisible). Add the two-layer coordinate **formatter** — genuinely absent today. | medium |
> | **S6d** | **Transitional hexes at the regional-hex level** | Connective ground between the per-regional-hex mini patches (`2·radius+1` = 13 across, so today they are addressing islands) so A* spans the planet as **one connected graph** and there is somewhere to stand on a boundary. **NOT needed for range across a boundary — that already works.** | large |
> | **S6e** | **Orders only from Force Management** | Move March / raze / seize / hold / ROE off the planet view into the Force Management window. | medium |
>
> - **Gate (S6a, the one that matters):** two units placed **1 km apart across a region boundary** exchange fire. Today
>   they cannot, and **no test covers it** — which is why the bug survived.
> - **Gate (S6c):** the same destination order is issued by the player **and** by the AI through the identical primitive,
>   and round-trips through save/load. ⚠ **`MoveToRegion` currently carries the ONLY save/load gauge of ground movement
>   (`MidCampaignSaveLoadTests`) — move that fixture in the same slice or the last gauge dies with it.**
> - **Reach:** **Force Management → select a formation → order to regional hex (17,09), mini hex (22,47)** (M4/M9).
> - **See:** the S1 log names both addresses; the mini-hex map draws **RED** weapons-range borders and **WHITE**
>   sensor borders (M7).
>
> ### ⚠ Knock-ons this ruling creates OUTSIDE S6 — scheduled, not silently dropped
> | Ruling | Lands in |
> |---|---|
> | **M8 + M16** capture is a **HYBRID**: **mini-hex ownership is the stored truth; a regional hex flips on the MAJORITY OF ITS OCCUPIED mini hexes** (occupied = a living unit *or* a building; tie ⇒ no flip; empty ⇒ taken by arriving). Reuses the **existing roll-up invariant** (`CityGrid.cs:14`) rather than a new system. Region capture + `TryCapturePlanet`'s all-regions test deleted | **new S6f.** Needs **ONE new field** — `CityTile.OwnerFactionID` (there is no mini-level owner today: `CityTile.cs:18-24`), added to the `[JsonProperty]` set **and the copy-ctor**. Then `GroundHex.OwnerFactionID` becomes a derived roll-up and `CaptureRegionHexContents` (`GroundBuildings.cs:187`) **inverts** — ownership only flows UP. 🧨 **Guard: ONE stored source of truth, or we rebuild the region-vs-hex drift M2/M6 deleted.** Upstream of **S12** (#21, still OPEN); victory condition = **written deferral** |
> | **M10** delete `GroundFortification.SumAdjacent` region shielding | **S5** (it already touches the fortification trap) |
> | **M11** hazards per-hex from hex type + geography | **S11** — replaces the per-region declaration; pairs with the `SYSTEM-GENERATION` **G6** physics-terrain item |
> | **M12/M14** hybrid AI brain — **CONFIRMED: coarse at the regional-hex level for *where to go*, fine at the mini-hex level for *how to fight*** (the same split as the two-part address, which is what keeps it inside One Verb, Both Seats) | **S7/D3a** — the maneuver slice already consults doctrine |
| **M18** the order set is **THREE battalion orders (MOVE · RAZE · SET DOCTRINE) + two transport (EMBARK/LAND)**; **HOLD is the default (empty queue)**, no timed hold. Enum goes **7 → 3**: delete `MoveToRegion` (M2), `HoldFor` (M18), `CaptureInfrastructure` (**M16 derives it**); fold `SetStance`+`SetEngagement` → **SET DOCTRINE**; port `MoveToHex`→MOVE and `DestroyInfrastructure`→RAZE to the two-part address (**which also kills the hardcoded `(0,0)` target bug**). ⭐ **The AI can drive ALL of them** — and the "AI never seizes" gap **closed itself**, because taking ground is now just MOVE | **S6c** — and it *shrinks* S6c: mostly deletion + one fold. Remaining AI fix: its doctrine application is a **direct call**, not a queued order (the third One-Verb violation) |
| **M17** the ground threat read is **ONLY what the units can see** — union of per-unit `GroundSensorAtb` reach vs the same `RealGapMetres` the weapon gate uses. Deletes three omniscience leaks (standing-in-region · **owning** the region · having **scouted** it) plus the un-gated own-region base case. ✅ Safe: the `Blind` valve already demands 1.5× odds and forbids pressing while blind — but **`Blind`'s definition must port from "un-scouted neighbour region" to "ground no unit of mine can see"** | **S6a** — it is the same unbucketing, same distance function, so it rides the keystone slice. ⭐ Makes the WHITE sensor border (M7) *literally* the picture of what the AI knows |
| **M13** the planet map stays **clickable for SELECTION + DISPLAY** (click a unit → its RED weapons-range and WHITE sensor borders) but **never for orders** | **S6e** — so S6e *moves* the order buttons out, it does **not** make the map read-only |
| **M15** ONE NAME: **"regional hex"** (big) / **"mini hex"** (small). The same object had **five** competing names and the developer's own term was the only one absent from the code. Docs + UI strings now; the `GlobalQ` field rename is an optional later tidy (it is a `[JsonProperty]`, so it lives in save files) | a **doc/UI sweep**, plus a naming note wherever S6a–S6f touch a string |

<details><summary>Superseded S6 text (kept for provenance — do not build from it)</summary>

#### ~~S6 — Movement rework (#14 + #16 + #17)~~ · ~~large~~ → ~~cheap-wire + a retirement~~

> **⭐ RE-SIZED DOWN by Phase B's B1 verdict (audit §17). Read this before planning the slice.** The old sizing
> rested on *"march-to-region is the ONLY fully-wired move verb,"* which made #14 read as *"delete the only thing
> that works and build a replacement."* **That quantifier is refuted.** `MoveToHex` is already wired at four of
> seven ends — order enum (`GroundForcesDB.cs:292`), factory (`:342`), formatter (`:355`), **processor execution**
> (`GroundForcesProcessor.cs:918`) — and, the part nobody had noticed, **the client already DRAWS its waypoint
> path** (`PlanetViewWindow.cs:505`, on **global cylinder coordinates** — precisely the *global* half of the
> two-layer scheme #14 asks for). What `MoveToHex` lacks is **ISSUERS**: no client button, no AI call, no test.
> **So #14 is three smaller pieces, in this order:**
> 1. **Wire `MoveToHex`'s issuers** — a client button (the view already renders the result) and an AI call in
>    `GroundTacticalBrain` beside the existing `MoveRegion(…)` at `:205`. *cheap-wire.*
> 2. **Add the two-layer coordinate formatter** — genuinely absent (the two existing formatters print one layer
>    each), and cheap: one function, then point both readouts at it.
> 3. **THEN retire `MoveToRegion`** — last, not first, with its client (`PlanetViewWindow.cs:1442,1445`) and AI
>    (`GroundTacticalBrain.cs:205`) issuers migrated.
>
> **⚠ Retirement caution:** `MoveToRegion` carries the **only save/load coverage of ground movement**
> (`MidCampaignSaveLoadTests`). Retiring it without moving that fixture to `MoveToHex` **drops the only gauge
> watching movement survive a save.** Move the gauge in the same slice.
>
> **Confirmed unchanged:** **four** coordinate systems coexist — `RegionIndex` (`:46`), `HexQ/HexR` (`:155,157`),
> `GlobalQ/GlobalR` (`:174,176`), `MiniQ/MiniR` (`:185,187`) — plus the order's own `TargetQ/TargetR` vs
> `TargetRegion` split (`:328-330`). *(Do not conflate `MoveToHex` with `OrderFormationTreeMoveToHex`, a formation-**tree**
> variant that genuinely has zero callers.)*

> **[V2] RE-SIZED.** "March to region" is **the only fully-wired planetary move verb** — live in the
> primitive, the order enum, the processor, the **AI tactical brain**, **both** client windows, the Site
> engine, and a save/load fixture, with **four** coordinate systems coexisting and no formatter for the
> combined address. Deleting it before the replacement works removes the only way anything moves, including
> for the AI: build the two-layer verb, migrate every caller, *then* cut. Cheap consolation: **#17 is
> cheap-wire** (all four numbers already exist as engine state; none is displayed), and **#16 is genuinely
> absent** — no order type, and the city-zoom click handler has no move branch at all.
Delete "march to region" **[A24]** (it never restamped the global position, so the token never moved), move
to the two-layer address `(17,09)(22,47)`, mini-hex moves with the same verb, and show all four march
numbers.
- **Gate:** an order moves a unit and its **global position actually changes**; the four readouts are
  non-zero and consistent (`Speed_kmh` is already real and read **[V]**).
- **Reach:** Force Management → battalion → Move → pick a two-layer address.
- **See:** the token moves; `[Ground]` march lines from S1.

</details>

### S7 — Doctrine becomes the steering wheel (**D0** → D2 → D3a → D3b; #15/#18/#19/#20) · medium ×4

> **⭐ D0 IS NEW AND COMES FIRST (finding C4) · cheap-wire.** `FleetDoctrine.TrySetDoctrine`
> (`FleetDoctrine.cs:54-64`) copies the raw blueprint and **silently drops `EngagementPosture`,
> `TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds` and `Pursues`** — and 9 of the 10 reader
> functions in `CombatDoctrine` have zero non-test engine callers. So the 25-entry catalog is authored data
> with **no consumer**, and #15/#18/#19/#20 all resolve into a JSON file nothing reads.
> **Build:** route `FleetDoctrine.cs:58-64` through `CombatDoctrine.Effective*`/`ParsePosture` so an assigned
> doctrine actually *carries* its fields. **Gate:** assign a doctrine, read it back, and assert all five
> previously-dropped fields survive — the test that would have caught this. **Without D0, every slice below
> decorates a value that is discarded on assignment.**

Your frame: *everything goes through the doctrines.* Catalog is built and green (25 entries) but **nothing
reads the behaviour fields** **[V]**.
- **D2:** ground reads the unified catalog; retire `groundStances.json`.
- **D3a (movement):** add `ClosingIntent`; make the maneuver consult doctrine **first**, role as fallback;
  fold `GroundEngagementStance` in; make `SpeedMult` finally bite. Then "Standoff Barrage" can be added.
- **D3b (fire):** target priority (#18), per-doctrine retreat threshold, **the retreat verb + break-away
  timer + the engagement lock** (#15), pursuit-as-doctrine, and **leader modulation** (mirror the space
  read of `OfficerCharacter.Blend`/`TenureWeight`, or record the deferral explicitly).
- **Gate per sub-slice:** two identical forces differing only in assigned doctrine produce **different,
  named** behaviour — and each is visible in the S1 log.
- **See:** the log says which doctrine chose what. *(Without S1 these three slices are unverifiable.)*

> **[V2] D3b IS MUCH CHEAPER THAN IT LOOKS.** The dials for **#18 (`TargetPriority`)** and **#15
> (`BreakAwaySeconds` / `Pursue`)** are **already built, parsed and authored on all 25 catalog entries** —
> every caller today is a unit test, so most of the work is *reading what is already there*. What is
> genuinely missing: ground fire is spread across all reachable enemies **weighted by current health**, so a
> cripple is never finished; **walking out of a ground fight is FREE** (a region march simply removes the
> unit from the resolver roster — there is no lock to bypass yet); and space still prices its exit with a
> hardcoded `const RetreatCasualtyThreshold = 0.5` while the doctrine's own fields sit unread.

### S8 — The tick and the rate model, **in ONE slice** (#23) · large (re-sized up) · **GATED ON Q3/Q4/Q5**

> **⚠ RE-SIZED AND RE-SHAPED by findings C2 + C3. Read them before planning this.**
> **C2:** the salvo pool is **not** `deltaSeconds`-scaled (`GroundForcesProcessor.cs:487,491,520,543`), so
> shortening the tick alone multiplies ground damage by the shortening factor (1 h → 5 s = **720×**), and
> ammo drain + infra bombardment scale with it while attrition, shield regen and closing do not — the balance
> **inverts**. **Therefore the tick change and the rate model cannot be separate slices.**
> **C3:** a committed CI spec already pins a **5 s** ground quantum plus the 720-per-hour divisibility and
> determinism invariants (`Resolver2DJointsSpecTests.cs:210,216,232,253`) and **nothing implements it** — so
> this slice has an acceptance spec waiting for it, and my earlier 60 s suggestion is withdrawn.

> **⭐ RE-SHAPED 2026-07-28 by Phase B's V2 verdict (audit §18d/§18e): THE RATE MODEL ALREADY EXISTS. This slice
> is a CONNECT, not an invention.** `RoundsPerSecond` is a real, authored dial — the base mod describes it in its
> own words (*"Rate of fire. Drives both damage/sec and saturation"*, `weapons.json:394,1036`; *"Saturation =
> rounds/sec × pellets/shot"*, `:570`) — and **`GroundCombat/SpaceWeaponGround.cs:64-79` already computes true
> damage-per-second from it** for ground-mounted designer weapons (`WeaponSupply.cs:85-95` uses the same product to
> size the reactor draw). **`GroundForcesProcessor` has ZERO references to any of it** and resolves flat
> `Attack × SalvoScale` per tick (`:491`).
> - **NOT needed:** design a rate model · choose per-weapon rates · add a rate field.
> - **Needed:** make the resolver **read** the per-second value, integrate it over the elapsed tick, and audit every
>   other per-tick term for `deltaSeconds` scaling in the same change (**C2** — still the hard part, and still why
>   the tick and the rate model cannot be separate slices).
> - **The real residue:** units with **no designer weapon** — garrison/base-mod units built from a flat `Attack`
>   with no `RoundsPerSecond` behind them. Those need a rate *assigned*. That is Q5's actual remaining question.
> - *Note the asymmetry this leaves untouched: ground **saturation** is still two hardcoded category constants
>   (`AreaSaturation`/`PointSaturation`, `GroundCombatant.cs:40,44`) while ships derive saturation from the rate.
>   Confirmed, and deliberately NOT in this slice.*

Fine-step while a battle is live, hourly otherwise. A weapon carries damage/second; the resolver integrates
it over the elapsed tick against available targets. **Every per-tick damage term must be audited for
`deltaSeconds` scaling in the same change** — that audit *is* the slice.
- **Gate:** with rates derived from today's `Attack` ÷ the chosen tick, a reference fight resolves
  **identically to today** (proves the refactor before any tuning), then a rate change moves the outcome the
  expected way. **Plus the C2 guard: the same fight run at two different tick lengths must produce the same
  result** — that is the assertion that catches an unscaled damage term, and it is the one that matters most
  here. Determinism preserved (fast-forward == watch), against the existing spec's invariants.
- **FLAGGED:** the tick value, every per-weapon rate, and the overkill rule.

### S9 — The bombardment joint (audit P3) · medium
A region-targeted `BombardColonyOrder` + client button routing into the already-wired
`DamageProcessor.ApplyGroundBombardment`, plus a `ConquerResolver` bombard rung **above** LAND so the AI
softens the beach too **[A24]**.
- **Gate:** an order damages the *targeted region's* defenders; the AI rung fires before landing.
- **See:** a bombardment line in the S1 log; the colony readout loses a building.

### S10 — The designer chain, in the forced order #2 → #4 → #1, then #3 · large
**Order is not negotiable:** penetration/per-shot-energy onto the **weapon** (#2) → research costs (#4) →
*then* delete the three prebuilt templates (#1), with a garrison-composition replacement, because the
prebuilts are today the only carrier of those dials **and** what `GroundStartGarrison` raises. Then block
invalid designs from **saving** (#3).
- **Landmine:** the **JSON atb binder is exact-arity** — adding dials means updating **every** template
  binding that atb in the same change, or they all fail to bind. Six-point registration applies.
- **Gate:** `BaseModIntegrityTests` green with zero skipped entries; a garrison still raises after the
  prebuilts are gone; an invalid design cannot be saved.
- **[V2] the "[?]" is SETTLED and #3 is cheap-wire:** validity **is** computed and **is** displayed, but
  `SaveGroundDesign` never reads `r.Valid` and registration proceeds regardless, so an invalid design lands in
  `IndustryDesigns` as buildable (`GroundUnitAssembly.cs:264,352`; `ShipDesignWindow.cs:520,557,592`). One
  gate at the save step closes it.
- **[V2] #1's real blocker is NOT the start garrison** — that builds its own throwaway C# designs
  (`GroundStartGarrison.cs:90-103`). It is that the prebuilts are **the AI's only buildable ground unit**
  (`ConquerResolver.cs:377` → `GroundReinforcement.cs:125`), so #1 needs a scenario/AI-authorable design
  source first or the AI can no longer reinforce. The forced order #2 → #4 → #1 still holds.

### 🔬 THE 7-PASS DESIGNER AUDIT — 40 findings, SEVEN root causes, and THREE decisions that gate the rest

**Full record:** `docs/economy/DESIGNER-AUDIT-2026-07-28.md` (the pass log + the consolidation, every finding with
file:line and the docs read before flagging it). Opened after the developer's call that *"if we're going to get combat
to work we need the designers fully functioning"* — correct, because canon **M19** makes the resolver's job to simulate
*"any collection of components"*, so a component recorded wrongly poisons every fix downstream.

**It asks a DIFFERENT question than the resolver audit and than the 2026-07-08 `docs/DESIGNER-AUDIT/`.** That one asks
*is the designer UNIVERSAL* (can a part mount on many hosts); this one asks **is it FAITHFUL** — does a dial you turn
get recorded correctly and *arrive* at the thing that reads it.

**Tally:** 40 findings — 9 🔴 · 15 🟠 · 8 🟡 · 5 🔵 · 3 ✅. **Stopped at seven passes** not because findings dried up
(Pass 7 found five) but because **novelty of kind** did: every Pass 5/6/7 finding landed in a bucket already on the
board, and the space/ground split alone surfaced **four times from four independent directions**.

| # | Root cause | Symptom in one line | Key findings |
|---|---|---|---|
| **RC-1** | **The base mod is untyped text nothing checks** | *A namespace, an arity, a material id and a mount flag are all just strings that happen to be right.* | **D2-1 (six `AttributeType` strings name a namespace that does not exist → FOUR live designer doors throw, including the whole missile-warhead designer)** · D2-3 (one arity gap) · D2-4 (three undefined materials **silently dropped** from build costs) · D2-5 (a numeric mount flag makes a solar array ship-only) · D1-1/D1-2 (**five duplicate ids**, three of them ground stances in two files → **load order decides behaviour**) · D2-6 (no gauge) |
| **RC-2** | **SPACE AGGREGATES, GROUND INDIVIDUATES — one unstated decision, four expressions** | *In space the fleet is the unit of account; on the ground it's the unit.* | D6-3 (**one shield pool per fleet**) · D5-2 (**armour hardening fleet-averaged** — one hardened hull protects the freighters) · D7-3 (*"engagement"* means two different things and ground never reads the space field) · + resolver root cause **B** |
| **RC-3** | **The merge reached the ARITHMETIC and stopped before the STRUCTURE** | *Both sides call the same formulas and disagree on the shape of the fight.* | **D6-1 (`CombatKernel.Combatant` has ZERO production consumers; no shared salvo loop)** · **D5-1 (two armour models: flat per-source on the ground, hit-points in space)** · D4-3 (so `Penetration`/`PerShotEnergy` on ships are not un-wired — they are **undefined**) · D6-2 (the dead bridge would **regress** the working ground shield-regen dial) |
| **RC-4** | **Dials that don't arrive — or don't exist to be turned** | *The audit's founding question, in three flavours.* | **D4-1 (you cannot design a weapon's RANGE except on a beam — five classes use engine constants, and the X9 rule runs off a ladder no design can reorder)** · **D4-2 (a missile carries 0 of `WeaponProfile`'s 10 fields)** · D4-4 (ground velocity/tracking/saturation are three constants picked by a dropdown) · D7-1 (**4 of 14 doctrine dials never reach the fight — three are the 2026-07-24 behaviour rulings**) · D3-2 (`Amphibious` **doubles the part's mass** and is read by nothing) · D3-3 · D7-4 · D2-7 · D4-6 |
| **RC-5** | **The combat value is computed once and never again** | *Frozen at `ShipFactory.cs:144`; nothing invalidates it.* | D5-3 (**falsifies the "grave rung" written into six attribute doc-comments**) · D5-4 (any refit is stale) · D5-5 (**the AI's own-strength, threat assessment AND decision log read the frozen number**) · = resolver **X14**/**P11-1** |
| **RC-6** | **Gauges pointed at the wrong thing — the most dangerous one** | *A gauge reading normal while the system is inert.* | **D7-2 (`UnifiedDoctrineTests` is described as proving the behaviours are "delivered" and asserts that a JSON file contains the values — green while nothing pursues, nothing finishes the wounded, nothing takes time to break away)** · D2-6 (no designer gauge, which is why RC-1 went unseen) |
| **RC-7** | **Ground combat has NO research tree** | *An absent system, not a wiring fault.* | **D3-1 (only 18 of 96 templates carry any tech gate; 42 are free AND ungated — every ground part among them; a turn-one rifle dials to 5000 attack / 100 km)** · D3-6 (research where it exists is mostly a price tag, not a ceiling) |

**⛔ THREE DECISIONS GATE THE REST — the developer's calls, not inferable:**
**Q-A** does the fight **aggregate or individuate** (RC-2)? · **Q-B** **which armour model wins** (RC-3/D5-1 — flat
per-source is what the shared kernel already implements; penetration and alpha only mean anything under it)? ·
**Q-C** the carried-over **P1-4** conflict — July's health-weighted bucketing vs the 2026-07-28 no-roll-over/real-targeting
ruling (**`TargetPriority` cannot be built until this is answered**). *(#21 — what a capture transfers — also still open.)*

**Ordered action list (full version in the audit's CONSOLIDATION):** ① build the designer gauge (~20 lines, proves
every fix below) → ② fix the six namespace strings → ③ re-point the four doctrine assertions at behaviour → ④
recompute the combat value → ⑤ the small data fixes → ⑥ give five ship weapon classes a real `Range` dial, ceiling
authored as `TechData(...)` so it starts closing RC-7 too → ⑦ the ground research tree → **⑧ gated on Q-A+Q-B** the
structural merge → **⑨ gated on Q-B+Q-C** `TargetPriority`, written ONCE in the shared kernel → ⑩ missiles, only after
② and ⑧.

**Hold new work to what the audit confirmed is GOOD:** **D6-4 — the ground shield chain** is the only end-to-end
example in the codebase of a dial that is designed, assembled, delivered, resolved **and gauged on the decision it
creates**. That is what "done" looks like. Also verified sound: the designer→assembler hop (**41 of 43** ground dials
have a reader), ten of fourteen doctrine dials, the ship armour nature matchup, and the whole formula layer
(675/675 `PropertyValue`, 58/58 `TechData`, 119/120 arities).

### ⚖ CROSS-AUDIT RECONCILIATION — where the two audits DISAGREE (read this before acting on either)

Two audits, days apart, different lenses: the **resolver audit** read code + design docs; the **designer audit** read
base-mod data + the code consuming it. The disagreements are worth more than the agreements. Full table:
`docs/economy/DESIGNER-AUDIT-2026-07-28.md` → **CROSS-AUDIT RECONCILIATION**.

- **C-2 🔴 NEW BLOCKER, found only by crossing them — and it changes a PLANNED DELETION.** X5 said the assembler never
  writes penetration/alpha; the designer ledger said both arrive. **Both true, of different paths.**
  `GroundWeaponAtb` has five fields and **none is penetration or per-shot energy**, so a unit designed **from parts can
  never be armour-piercing**. Only `infantry-unit`/`armor-unit`/`artillery-unit` carry them (via `GroundUnitAtb`) — and
  those three are **marked for eventual removal**. ⇒ **Add the two dials to `GroundWeaponAtb` BEFORE retiring the
  prebuilts, or armour penetration leaves the ground game.**
- **C-3 🟠 the plan's "the ground feed is lossy" framing is BACKWARDS.** True per-field: ground **6 of 10** (prebuilt) /
  **4 of 10** (parts) · railgun **4 of 10** (not "all designed") · **missile 0 of 10**. A prebuilt ground unit carries
  more designed fidelity into the fight than any ship weapon except the beam.
- **C-1 🔴 the doctrine inert-list is FIVE, not four**, and matches resolver **P2-1** exactly: `TargetPriority`,
  `RetreatCasualtyThreshold`, `BreakAwaySeconds`, `Pursues`, `SpeedMult`. The fix for four of them is a **save-schema
  change** (`FleetDoctrineDB` has nowhere to put them) — not a copy bug.
- **C-5 🔵 carry this sentence into the recompute slice:** **freeze on RESEARCH · refresh on DAMAGE and REFIT.** Without
  it, resolver **P17-1**'s ✅ ("a built ship keeping its build-time numbers is correct") reads as blessing the frozen
  value that RC-5 exists to fix.
- **C-4 🟡** the all-beam-closes-to-point-blank case is **unreachable with base-mod data** (both beams have Range
  `MinFormula: 1000`) — latent trap, not live misbehaviour.
- **C-7 🔵 coverage map:** the resolver lens could never have found RC-1 (it did not read the JSON); the designer lens
  could never have found the disengage refill or the battle-report fog leak (it never ran a battle). **Neither swept the
  engine↔client seam — the one surface that yielded a 🔴 on first contact. Point the next pass there.**
- **C-8 ✅** both audits independently produced the same sentence — *the designer models COMPONENTS, the resolver models
  TOTALS.* Two differently-pointed methods converging is the strongest evidence either document contains.

### 🔬 THE 17-PASS RESOLVER AUDIT — 49 findings, and they are FIVE root causes

**Full record:** `docs/combat/RESOLVER-AUDIT-2026-07-28.md` (the pass log, every finding with file:line and the docs
read before flagging it). **Canon + the X1–X15 table:** `GROUND-GAMEPLAY-DECISIONS-2026-07-24.md` M19.

**The load-bearing conclusion: there are not 49 problems. There are five, and one outlier.** Every finding is a
symptom of one of these — which is why the fix order below is a *dependency* order, not a preference.

| # | Root cause | Symptom in one line | Findings it explains |
|---|---|---|---|
| **A** | **The design→resolver FEED is lossy** | *The designer's numbers don't arrive.* | X5 (ground weapon: **2 of 10** values designed) · X6 (**alpha zeroed in BOTH domains**) · **P7-1 (a missile: 0 of 5)** · P3-1 (a fire-control dial gated by a flag nothing switches on) · X14 (combat values **frozen at build**, never refreshed by damage) · P11-1 (a gutted hull still reads as an armed warship) |
| **B** | **The battlefield is a CONTAINER, not an ENGAGEMENT** | *Everything aggregates at the fleet/region level, which is the wrong level.* | X7 (a **star system**/a **region** is the battlefield) · P1-6 (`ResolveRegionCombat` is **O(units²)**) · X11 (**wings have no position** to manoeuvre with) · P5-1/2/3 (fleet-wide range decisions; **one long-range gun hijacks the fleet**; an **all-beam fleet closes to point-blank**) · P8-1 (**one shield pool for a whole fleet**) |
| **C** | **Combat state is EPHEMERAL** | *Nothing a battle does persists.* | **P6-5 (disengaging refills ammo, shields, heat, manoeuvre — and deletes accumulated damage)** · X15 (ships are **whole-or-dead**, no wounded ship) · X14 again · P15-3 (the report dies on quit) |
| **D** | **Doctrine cannot carry its own behaviour** | *The wheel is not connected to the wheels.* | P2-1 (**the runtime blob has no fields for 5 of the 14 authored dials**) · P2-2 (**different names per domain, one pair a RECIPROCAL**) · P2-3 (incompatible posture enums) · P2-4 (ground has no `SpeedMult` to land on) · X1 (**role** is a second driver, live in a real game) · P6-2/P6-3 (**personality** is a third driver — it modulates the retreat threshold) · X9/X10 (computed range + a hardcoded opening spread **override** doctrine) |
| **E** | **TWO WORLDS — the live sim and the auto-resolver** | *The same designed thing behaves differently depending on which code path runs.* | **P7-2 (two missile models, 500×–50,000× apart)** · P7-3 (the auto-resolver never spawns ordnance) · P13-1 (**bombardment exists only in the path that does not run the battle**) · X13 (**reach 0 means opposite things** in the two domains) · X12 (the feed and the closing model are still unshared) · P4-5/P4-6 (**hazards: each domain built the half the other lacks**) |
| **⭐ OUTLIER** | **The engine records everything; the CLIENT filters nothing** | *A fog-of-war leak.* | **P15-1 — the Battle Report shows EVERY faction's battles galaxy-wide.** The `FactionId` needed to filter is **already on the event struct** and nothing reads it. **Found only by crossing engine→client — a seam barely swept, which yielded a 🔴 on first contact.** |

**⚡ THE FOUR CHEAP ONES — hours, not weeks, and each closes a whole class:**
**P15-1** the fog filter (the field exists) · **P3-1** the fire-control flag (one line) · **X13** pick ONE reach
convention (`0` = no reach, unbounded = `PositiveInfinity`, which the range check already handles) · **P2-1** add the
five missing doctrine fields (a save-schema addition, and it un-blocks **every** doctrine slice).

**THE DEPENDENCY ORDER (not a preference — each unlocks the next):**
> **S1 (see it)** → **D-fields** (doctrine can hold its dials) → **A** (the designer's numbers arrive) → **B**
> (cluster the battlefield — *and it makes everything cheaper, not dearer*) → **C** (state persists) → **E** (unify the
> two worlds). ⚠ **R3 (real targeting) needs A first** — wasting alpha means nothing until alpha exists — **and B
> before it**, because clustering buys the budget targeting spends.

### ⚔ R1–R5 — THE RESOLVER RETROFIT (canon **M19**, 2026-07-28) · large, five slices

> **Cause established, not assumed.** The kernel is already shared; the **FEED** into it and the **WRAP** around it are
> duplicated, and **all eight recorded contradictions live in that duplication** (canon M19's audit table X1–X8). The
> decisive argument is not the bug count: **the DESIGNER is already unified** — `SpaceWeaponGround` exists so the same
> laser/railgun/flak a ship mounts works on a chassis, reading *"the SAME fields `ShipCombatValueDB` reads"* — and the
> ground profile converter then **throws that design away**, hardcoding velocity/tracking/saturation from a 4-value
> enum. **A railgun on a tank stops being a railgun.**
>
> | # | Slice | Fixes |
> |---|---|---|
> | **R1** | **Unify the FEED** — one `design → WeaponProfile` converter both domains call; the assembler writes `Penetration`/`PerShotEnergy`; ground gains component-health damage scaling + recoil-vs-mass tracking | X5, X6 |
> | **R2** | **Unify the BATTLEFIELD — per-engagement clustering** *(developer-ruled)*. A battle is a cluster in contact, not a star system or a region. **Cheaper, not dearer** — 100 fights of 4 ≈ 1,600 comparisons vs one fight of 400 ≈ 160,000 | X7 |
> | **R3** | **Unify ALLOCATION — real targeting, NO roll-over** *(developer-ruled)*. Excess over a target's remaining health is **wasted**. Replaces the health-weighted pool smear (which never finishes a cripple) and the ship bucket-aggregate. **Needs R1** (alpha must exist before wasting it means anything) and **R2** (which buys its budget) | X6, #18 |
> | **R4** | **Unify the TICK** — ground to the committed **5 s** quantum, per-second like space. **R-g makes this CORRECTNESS**: at an hourly tick a battle can finish in one step, so *"simulated throughout the entirety of the battle"* cannot hold | X4 |
> | **R5** | **Unify ARMOUR + the HIERARCHY** — one armour definition; **cap nesting at TWO named tiers, Fleet/Battalion over Wing/Formation** (both domains already nest to *arbitrary* depth via a parent/child tree, so this **constrains and names rather than builds**); and **doctrine addresses roles** so doctrine is the sole behavioural driver. **PLUS the three findings that govern the SHAPE of a battle: doctrine must decide the ENGAGEMENT RANGE and who dictates it (today `FleetDesiredRange`/`FleetManeuver` compute it from ship stats with no doctrine argument), doctrine must set the OPENING POSITIONS (today a hardcoded holder-stays-others-pushed-back rule), and WINGS/FORMATIONS NEED POSITIONS OF THEIR OWN — space has one anchor per FLEET and its own comment defers per-role points to an unbuilt slice, so R-f is not implemented at all** | X1, X2, X3, X8, **X9, X10, X11** |
>
> **⚠ X1 is LIVE IN A REAL GAME.** `EnableGroundRoleManeuver = true` on the **normal New Game path**
> (`NewGameMenu.cs:567`), and role independently decides whether a unit backs off — a second behavioural driver
> alongside doctrine, which **R-c forbids**. The fix keeps the useful part: space's group-plane already positions
> sub-fleets by *"anchor + **doctrine** RoleOffset"*, so **make ground do what space already does.**
>
> - **Gate (R1):** the SAME designed weapon mounted on a ship and on a ground chassis produces the **same
>   `WeaponProfile`** (modulo mount/platform terms) — the test that would have caught the whole class.
> - **Gate (R2):** two separated pairs of hostile forces in one system/region resolve as **two independent battles**,
>   and a third pair joining one does not perturb the other.
> - **Gate (R3):** a shot that over-kills its target **wastes** the excess — a high-alpha weapon measurably
>   under-performs a low-alpha one of equal DPS against many small targets, and out-performs it against armour.
> - **Gate (R4):** the same battle at two tick lengths produces the same result (the C2 guard).
> - **Order:** **S1 first** (you cannot tune what you cannot watch) → R1 → R2 → R3 → R4 → R5.

### S11 — Depth, cradle-to-grave · large, many small slices
Each is independently shippable: units cost **people** permanently (#7 — `CrewReq` exists, unread);
**ammo bites** (#8); **hazard counters as specific gear** (#5); **`CasualtyTier` + damage ledger** (#22 —
build, don't redesign); **employment jobs + power demand** (#12 — mostly JSON); **semantic tile bonuses**
(#13); **M4 per-mini-tile terrain**; **hex/tile naming** (Layer 6); **client per-faction fog**; the
**grave rung** (pop→0 / rebellion expiry → colony collapse); **hex deposits as the mined truth**; and the
**G6b** single-hex-model cleanup.
- **The `SYSTEM-GENERATION` G1–G6 track — recommendation CORRECTED 2026-07-27 after actually reading
  `docs/environment/SYSTEM-GENERATION-AND-PERSISTENCE-DESIGN.md`.** My first pass called the whole track a
  written deferral *without having read it*. Too blunt: the doc's own EXISTS/MISSING ledger shows **most of the
  substrate is already built** — a seed-deterministic generator, the file **format** (`SystemBlueprint`, which is
  what a real New Game already loads), per-body terrain from physics (lazy), and located deposits. Three things
  are missing and they are **not equal**:
  - **G1 — the WRITER + round-trip gauge: PULL FORWARD, it is cheap.** Serialize a generated system into the
    **existing** `SystemBlueprint` shape, then generate → write → `LoadFromBlueprint` → assert identical.
    Verified: **no writer exists anywhere** in `GameEngine/Galaxy/` (no `File.WriteAllText`/`SerializeObject`),
    and `Engine/Blueprints/SystemBlueprint.cs` is already there to write into. **Additive — nothing reads the
    file yet** — and gauge-first, which is this plan's own spine. Hand-editability (decision #4) falls out free,
    because it writes the shape Sol already uses.
  - **G2 — belts/comets/Oort in procedural systems: a CONCRETE CONTENT BUG, not a design want.** Verified:
    `GenerateAsteroidBelt` (`StarSystemFactory.cs:481`) has **exactly one caller** — `:573`, inside
    `LoadFromBlueprint`, the **authored** path. **A procedurally generated system therefore gets no belts at
    all.** Landmine: draw from a dedicated RNG stream, never the shared `StarSystem.RNG` (the `RuinsDB` lesson).
  - **G3–G6 — DEFER as written** (write-on-first-generation · the hybrid observation freeze · the galaxy-setup
    choice · terrain from stellar/orbital physics). **G6 is the one M4 depends on** — *"a rich terrain display
    over a uniform generator is a lie"* — so schedule G6 **with** M4, never before it.

### S12 — What capture transfers (#21) · **BLOCKED ON Q1 — do not start**

---

## 4a. REACH + SEE for every slice — the click-path table (completed 2026-07-28)

**Why this table exists.** Phase D requires *every* slice to carry a **reachability criterion written as a literal
click-path** and an **observability criterion**. An audit of §4 found only 6 of 19 slices had one — the rest were
blank, and **a blank reads as "nobody thought about it"** rather than "there is no player path." So every slice is
listed here, and where a slice genuinely has **no player-facing path**, it says so explicitly and names what the
player encounters instead. *(Slices whose body already carries `Reach:`/`See:` are repeated here for one-glance use.)*

| Slice | REACH — the literal path | SEE — what proves it live |
|---|---|---|
| **S0** scale gauge | **No player path — CI-only by design.** It is a readout, not a feature. | The CI job log prints `HexPitchKm` + `MiniPitchKm` for Earth / Mars / Luna, and the two are related by exactly ÷13. |
| **S1** ground battle log ⭐ | Main menu → **DevTest** → advance the clock until a ground fight starts. *(Or a menu game with a garrison.)* | `[Ground…]` lines in `game_logs/`, and the events appear in the **Battle Report** the combat interrupt already opens. |
| **S1b** colony registry | Main menu → **DevTest** (it starts a war) → let the AI take a world, or take one yourself. | The `[AI]` tape **stops** proposing an invasion of a world the faction already owns. |
| **S1c** sim-health gauges ⭐ | Launch via **`launch.bat`** → play → force/observe a sim fault. The reach criterion *is* that a dead sim announces itself. | `console_output.txt` names the dead sim on the **first** frozen heartbeat; the fault tally counts `[FATAL]`/`[HANG]`; every pause states its reason; the play button says it's dead instead of doing nothing silently. |
| **S1d** at-target guard | Main menu → **DevTest** → let an AI strike fleet reach its target and sit there. | `[WARP]` lines **stop** showing 0 Gm departures; the order count stops growing across ticks. |
| **S1e** blind AI ⭐ | Main menu → **DevTest** → let two hostile fleets come into sensor range of each other. | The `[AI]` tape stops saying `vs no threat` and names a rival **with a number**. |
| **S1f** garrison rebuild ⭐ | Main menu → **DevTest** → attrit an AI garrison below its target and let the rebuild rung fire. | The `[AI]` tape's `RebuildGarrison` action is followed by an actual **garrison count increase** — not a cargo item. |
| **S2** flags into save | **New Game** (set the options) → **Save** → quit to menu → **Load** that save. | The five flags read back identical to what the game was created with — and a save now plays the same regardless of what was started earlier in the process. |
| **S3** scenario start | **Main menu → the renamed "Scenario / Skirmish" button** (today: "DevTest"). ⚠ **GATED ON Q2.** | You start with two developed rivals at war and every ground switch on, from a supported front door rather than a dev toy. |
| **S4** designer door | **Main menu → Entity Assembler** → open a **saved ground unit design** → its panels are editable without first selecting a ship design. | The design reopens with its parts intact and can be edited and re-saved. |
| **⛳ M1** live sitting | **This slice IS the click-path** — the full chain on Windows: design → build → field → move → fight → take a region → lose a unit. | Every rung writes a line naming what happened; no `[FATAL]`; the clock keeps running. **This is the Layer-3 gauge** (row in `docs/TESTING-TRACKER.md`). |
| **S5** one build queue | **Colony Management → Production** → queue a ground unit **and pick its destination**; watch one progress bar. | One queue holds every ground build with a visible destination and progress; the *free* path no longer offers a competing route. |
| **S6** movement | **Planet view → select a battalion → click a destination hex** (the path overlay already draws waypoints). | The two-layer coordinate appears in the readout (`(17,09)(22,47)` shape), and the unit walks the drawn path. |
| **S7 / D0** doctrine carries its fields | **No new player path — D0 is a repair.** The existing path is Fleet/Battalion → assign a doctrine. | Assign a doctrine, read it back: the five previously-dropped fields survive. Without this, every slice below decorates a discarded value. |
| **S7 / D2·D3a·D3b** doctrine steering | **Fleet Management → Battalions → select → Stance / ROE**, and the same doctrine list the fleets use. | The **S1 log** says *which doctrine chose what* — two identical forces differing only in doctrine produce different, named behaviour. *(Unverifiable without S1 — that is the dependency.)* |
| **S8** tick + rate model | **No click — you watch.** Start a fight and let it resolve; the reach criterion is that the fight resolves in fine steps while live and hourly otherwise. | **S1 log timestamps** show fine-step resolution during a battle and hourly outside one; the same fight run at two tick lengths produces the **same result** (the C2 guard). |
| **S9** bombardment joint | **Fleet window → a ship holding orbit → Bombard → pick the target region.** | A bombardment line in the **S1 log**; the colony readout **loses a building**; and the AI softens the beach before it lands. |
| **S10** designer chain | **Entity Assembler → design a ground unit**: weapon dials carry penetration/per-shot energy; parts cost research; **saving an invalid design is refused**; the 3 prebuilts are gone and the garrison composition replaces them. | The designer's own validity readout **blocks** the save (today it only warns), and a built unit's stats reflect the dials rather than zeros. |
| **S11** depth | **Per sub-slice — each writes its own Reach when it is scheduled.** S11 is explicitly *not* enterable as one unit (audit §13c: 13 items, one gauge between them). | Per sub-slice. The one pulled forward, `SYSTEM-GENERATION` **G1**, is CI-only: generate → write → reload → assert identical. |
| **S12** capture transfer | ⚠ **BLOCKED ON Q1 — no path can be written until the ruling says what transfers.** Gauge-blocked, not gauge-missing. | Blocked. |

**Two honest patterns in this table, stated rather than hidden:**
1. **Five slices (S1b · S1d · S1e · S1f) have no click-path because they are AI defect fixes** — the player's
   "reach" is that the AI stops doing something visibly stupid. Their observability all runs through the **`[AI]`
   tape**, which is why those four are worth nothing until **S1** and **S1c** make the tape readable. *That is the
   real argument for the observability-first spine, and it is stronger than "you can't tune what you can't watch."*
2. **Three slices are deliberately not player-facing** (S0 a CI readout · S7/D0 a repair · S8 an internal
   re-timing). Recorded as such so a future reader does not go looking for a missing button.

---

## 4b. The executable half — `close-planetary-delta`

The slices above are encoded as a committed, re-runnable workflow:
**`.claude/workflows/close-planetary-delta.js`**. Invoke one slice at a time —
`{slice:"S1"}` — from the branch that owns the work; the session commits and gates CI (~33 min) between
slices. Each slice runs **implement → adversarial verify (default to refuted) → repair → a docs agent that
flips the dashboard rows**, then a **completeness critic** whose findings become the next slice's work list.

Key mapping, kept deliberately in step with §4: **S7 is split into `D2` / `D3a` / `D3b`** (three pushes,
three gauges, matching the doctrine build plan's own names). **S11 (depth) is intentionally not encoded
yet** — it is many small independent slices gated behind milestone **M1**, and writing their prompts before
the live sitting has been observed would be guessing. **S12 refuses to run** and emits the Q1 decision aid
instead.

**The workflow has never been invoked.** It was validated statically only (`node --check`, meta-shape diff
against `.claude/workflows/earthfall-campaign.js`, phases/slice-key audit). Running it starts the build, and
that is the developer's call.

---

## 5. Every balance number this plan introduces (nothing chosen silently)

| Number | Where | Status |
|---|---|---|
| Ground fine-step tick length | S8 | **OPEN (Q3)** — recommend 60 s, not space's 5 s |
| Per-weapon damage/second (all weapons) | S8 | **OPEN (Q5)** — recommend deriving from `Attack` ÷ tick so v1 is behaviour-identical |
| Mid-tick overkill rule | S8 | **OPEN (Q4)** — recommend sequential down the priority list |
| Break-away timer (~1–2 h) | S7 D3b | from ruling #15; exact value FLAGGED |
| `BattleLog.MaxEvents` (250 today) | S1 | FLAGGED — sized for fleets, not battalions |
| Research cost curve vs complexity | S10 | FLAGGED |
| Employment jobs per template; per-capita power demand | S11 | FLAGGED |
| Terrain cost multipliers ("price it, don't ban it") | S11 | FLAGGED — and faction-modulated per Layer 6's law |

---

## 6. What is still owed on the evidence side

The fan-out died, so these remain **unverified** and each is marked **[?]** above. A resumed pass should run
them first (briefs are preserved — `docs/DOCS-AUDIT-2026-07-27.md` §5): the **game-log forensics** of the
2026-07-23 session (what actually ran, what failed, what stayed silent); the **rulings-compliance matrix**
for all 27; the **validity-gate** question that sizes #3; the remaining **doc-claim sweep** (`docs/combat/*`,
the subsystem `CLAUDE.md`s, `MVP.md` / `PLAY-TO-MARS`, the `SYSTEMS-STATUS-AND-TEST-PLAN` retirement); and
the **gauge ledger** truth-check.
