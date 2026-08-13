# Engine Wiring Backlog — making the design-tool intentions real

**As of 2026-08-06 (TIER 2.6 added 2026-08-10). THIS IS A BACKLOG OF INTENTIONS — nothing here is implemented.**
It is the engine (C#) side of the [`SIM-DRIVEN-DESIGNER-AUDIT-2026-08-05.md`](SIM-DRIVEN-DESIGNER-AUDIT-2026-08-05.md)
fix list.

---

## Read this first — what this doc IS and what it is NOT

Our HTML design tools — the **12 door designers** (`docs/Actual HTMLs Of designers/*derived.html`), the
**Entity Assembler** (`entityassembler.html`), and the **auto-resolver sim** (`resolversim.html`) — are
**intention documents**. They already draw every capability the designer is *meant* to have, and they
already flag — honestly and in red where needed — every dial whose engine wiring isn't built yet. They are
the *blueprint*. They are done, and this doc does **not** ask for a single change to them.

What is *not* done is the **engine** catching up to the blueprint. When the ground-battle sim forced me to
type in the Tyranids' claw penetration by hand, that wasn't a sim bug — it was the engine's `GroundUnitAssembly`
still dropping a stat the HTML assembler already promises. **Every item below is a place where the engine has
to be wired so a value the tool already shows actually flows through and gets read in a fight.**

Think of it like a ship's schematic drawing versus the ship. The drawings (the HTML) show every valve, every
run of pipe, every gauge. This backlog is the punch-list of pipe that's drawn on the schematic but not yet
welded in the hull. When a run gets welded, you flip its row here to ✅, flip the matching honesty flag in the
design tool, and update `docs/DOCS-INDEX.md` — all in the same commit.

**The rule that governs every item** (`docs/economy/DESIGNER-NORTH-STAR.md:74-76`): a dial that *"writes none
and costs nothing"* is a bug in the design. So each fix below is one of two shapes:
- **WELD THE PIPE** — a value the tool shows has a live reader waiting; wire the producer→reader run so the
  dial actually writes something. (Items 1–5.)
- **CUT THE DEAD KNOB** — a dial the tool *had* to draw for completeness but which writes nowhere and never
  will without new engine subsystems; either delete it or build the subsystem, and until then keep it flagged,
  never sold. (Items 6–11.)

Ordered by **impact × cheapness** — the same ranking as the audit.

---

## TIER 1 — The root cause: the assembler's ground path is a rung behind its own base-mod path

### 1. Carry `Penetration` + `PerShotEnergy` through the ground assembler  ✅ BUILT (2026-08-13, OPERATION BLUEPRINT-TO-STEEL A2)
**This is the fix that would have stopped the "invented stats" sin. Done first.**

> ✅ **DONE.** `GroundWeaponAtb` gained `Penetration` + `PerShotEnergy` as its 6th/7th ctor args (the exact K1 `Range_m`
> pattern — trailing, defaulted, clamped); `GroundUnitAssembly.Compute` reads them onto each `GroundWeaponMount` **and**
> the unit-level design (the primary-weapon fallback), `ToGroundUnitDesign` sets the design fields, and
> `GroundCombatant.ToWeaponProfile(unit, mount)` now reads the MOUNT's own pen/per-shot (per-mount honesty — a
> rifle+railgun unit cracks plate only with the railgun). All 5 base-mod weapon templates went to 7 `AtbConstrArgs` in
> lockstep (cannon 20/140 = the monolithic Armor gun · autocannon 6/40 · energy 10/90 · rifle 0/10 · claw 0/10 —
> **FLAGGED** balance values the developer owns). The entityassembler.html badge already read "LIVE on ground" — this
> makes it true for the assembler path too, so no HTML flip was needed. Gauge: `GroundWeaponPenetrationAssemblyTests`.
> **Non-byte-identical (intended):** assembled cannon units now crack plate; no existing resolver test fields an
> assembled unit, so nothing re-baselined. `GroundWeaponAttackCostTests` (build-mass/carry unchanged — pen/per-shot are
> NOT in the Mass formula) + `BaseModIntegrityTests` (the 7-arg JSON bind) stay green as tripwires.

- **What the tool already promises.** The Entity Assembler draws a *penetration* and a *per-shot* number on
  every ground weapon and states plainly that they are **"LIVE on ground, INERT on ship"**
  (`entityassembler.html:336-375, 993-994, 1105-1106`). The resolver sim already reads penetration in its
  shared armour kernel and carries authentic pen/per-shot on every ground weapon
  (`resolversim.html:386-410, 574-583`). The blueprint says: a player-built AP weapon cracks plate that a
  small-arms weapon bounces off.
- **The engine gap.** A weapon *designed in the assembler* comes out with **penetration 0 and one single lump
  of a shot**, because the component attribute has no dial for either number:
  - `GroundWeaponAtb.cs:30-60` — only carries `Mass / Attack / Range / Range_m / Mode`. No `Penetration`, no
    `PerShotEnergy`.
  - `GroundUnitAssembly.cs` (the `Compute` weapon loop + `ToGroundUnitDesign`, ~`:278-301`) — can't read what
    isn't there, so it never sets them on the design or the per-weapon mount → the raised unit snapshots 0.
  - The **reader is already live and waiting**: `GroundForcesProcessor.cs:437` → `CombatKernel` armour-soak
    (the `armourSoak`/`armourSoakBurst` math the resolver sim mirrors). Every fielded assembler unit feeds it 0.
- **The tell that this is a rung behind, not unbuilt.** The *monolithic base-mod* units already carry both
  dials — armour unit pen **20** / per-shot **140**, artillery **8** / **80**, infantry **0** / **10** (the
  W1c/W2c dials, `GroundCombat/CLAUDE.md`). So the numbers exist and are read; only the **player-facing
  assembler path** drops them.
- **What to build.**
  1. Add `Penetration` and `PerShotEnergy` as `GroundWeaponAtb` fields — as **the 6th and 7th ctor args**
     (it already takes 5: mass, attack, range, mode, range_m).
  2. Read and sum them in `GroundUnitAssembly.Compute`; set them on `GroundUnitDesign` **and** per
     `GroundWeaponMount` in `ToGroundUnitDesign` (per-mount is the honest home — a unit with a rifle and a
     railgun should crack plate only with the railgun).
- **⚠ LANDMINE — gotcha 6 (the exact-arity JSON binder).** Adding a ctor arg to a JSON-bound `*Atb` is **not**
  additive. `Activator.CreateInstance` needs the template to pass *exactly* the new arity. So **every base-mod
  ground-weapon template must add both values in lockstep** — `ground-rifle`, `ground-autocannon`,
  `ground-cannon`, `energy-weapon`, `claw-weapon` in `installations.json` each need a `Penetration` +
  `PerShotEnergy` Property and two more `AtbConstrArgs` values. This is exactly how `Range_m` (K1) was added.
  Miss one and `BaseModIntegrityTests` red-lights the whole suite with *"the arguments did not match any
  constructors."*
- **The gauge (write it).** A new test: *an assembler-built AP claw cracks plate a small-arms weapon of equal
  Attack bounces off* — plus `GroundWeaponAttackCostTests` stays green as the byte-identity tripwire.
- **Cradle to grave.** designer dial (real km + pen + per-shot) → assembler mounts it → `GroundUnitDesign` →
  raised `GroundUnit` snapshot → resolver reads pen in the armour soak → the shot cracks or bounces → shoot
  the weapon component off and the unit loses its AP bite.
- **Unblocks.** Genestealer *rending claws*, the Clone DC-15A's AP mode, the AT-TE mass-driver — **3 of the 7
  franchise ground units** — stop needing hand-typed numbers.

---

## TIER 2 — The cheapest base-game fix (every colony, every game)

### 2. An Employment producer — feed the morale term that can never fire  ✅ BUILT — FLAG-GATED (2026-08-13, OPERATION BLUEPRINT-TO-STEEL A1)

> ✅ **PRODUCER BUILT (option a).** `ComponentInstancesDBExtensions.GetTotalJobs` now sources each installed building's
> operating-CREW requirement (`ComponentDesign.CrewReq`) as its employment — the civic-door design exactly, no new data
> (an explicit `EmploymentAtbDB.Jobs` still overrides where a template declares one, so the attribute stays live, not
> dead). So the morale term's producer is no longer 0. The ±40 employment→morale term is **FLAG-GATED**
> (`PopulationProcessor.EnableEmploymentMorale`, default OFF → byte-identical; the -1.0 neutral sentinel and
> `MoraleTests` stay green) at all three consumers (`PopulationProcessor` ×2 + `StationPopulationProcessor`). Gauge:
> `EmploymentMoraleTests`. **⚠ CALIBRATION PARKED FOR THE DEVELOPER (turn-on decision):** CrewReq was authored as
> operating-crew (0..1,000,000 across templates), so against a billions-pop workforce the ratio reads heavy
> unemployment — flipping the flag on unbaselined pushes every colony's morale down. The denominator (full workforce vs
> a smaller "employable" figure) is the developer's call before the client/menu turns it on. The mechanism is proven;
> the number needs tuning. (Recorded in `docs/IMPLEMENTATION-CAMPAIGN-LOG.md` ADJUDICATION QUEUE.)
- **What the tool already promises.** The civic door's whole thesis is *"the morale term that can never fire"*
  (`civicderived.html:152-279`): the intended design is that a colony's **jobs** move morale in a ±40 band,
  and jobs are *"published from every industry building's `CrewReq`, an emergent colony total"*
  (`civicderived.html:206`).
- **The engine gap.** The entire chain is welded **except the first rung — nothing declares a job**:
  - Consumer LIVE: `PopulationProcessor.cs:74` moves morale on jobs ÷ workforce.
  - Aggregator LIVE: `ComponentInstancesDBExtensions.GetTotalJobs` sums `EmploymentAtbDB.Jobs`.
  - Attribute LIVE: `EmploymentAtbDB` exists, with `Clone` and the NCalc binder overload.
  - **Producer MISSING:** grep every template — **zero** write `EmploymentAtbDB.Jobs`. So the widest morale
    term reads **0 in every colony, every game.**
- **What to build (pick the shape the civic door intends).** Either (a) make `GetTotalJobs` read every
  industry building's existing `CrewReq` (the civic door's stated intention — jobs as an emergent total, no new
  data), or (b) add an `EmploymentAtbDB.Jobs` value to the relevant base-mod templates (six-point registration,
  gotcha 10). Option (a) matches the blueprint and needs no new dial.
- **⚠ This is the biggest live behaviour change in the whole backlog** (the civic door flags this itself,
  `:173`): turning employment on moves *every* colony's morale in both directions, and morale feeds migration,
  tax income and legitimacy. **Flag-gate it and baseline against the existing `MoraleTests`** — the same
  treatment the fuel gate got.
- **The gauge.** A colony with staffed industry reads a non-zero employment morale term; an empty colony still
  reads the documented neutral sentinel (not 100% unemployment).
- **Unblocks.** The base-game colony morale loop — the single cheapest high-impact fix in the set.

---

## TIER 2.5 — The component RUN-COST vector (the unifying frame)  ⬜ NOT BUILT

**One pattern, four costs — from the Capitol-world sim
(`docs/economy/CAPITOL-WORLD-INFRASTRUCTURE-2026-08-06.md §8`).** The four "does a built world cost anything to
*run*" loops — **power · jobs · food · upkeep** — are ONE design, not four. Every component already carries a
**build** cost vector (materials/credits/build-points) that the Assembler sums into its cost surface. It should
carry a **run** cost vector too, summed the same way — *"what you mount is what you pay."*

The shape to build each one to is **already proven** by the infrastructure grid (§5.1 of the Capitol doc):
**a per-component dial → a colony/entity Σ → a reader that bites** (`InfrastructureCapacityAtb.Capacity` →
`InfrastructureProcessor` provided-vs-required → efficiency throttles all production). Build the other three to
that exact shape and the surface economy gains its missing half.

**The Entity Assembler now SHOWS this** — a **Run cost** panel (power/jobs/food/upkeep), each badged live/pending,
sitting next to the build cost surface (`entityassembler.html`, `renderRunCost`). That's the intention document;
here is the engine work to make each badge go green:

| Run-cost | Per-component dial (exists?) | Colony/entity Σ (exists?) | Reader (exists?) | What to build | Effort |
|----------|------------------------------|---------------------------|------------------|---------------|--------|
| **Jobs → employment** | `CrewReq` ✅ | `GetTotalJobs` ✅ | `PopulationProcessor:74` ✅ | **the producer** — publish `CrewReq` as `EmploymentAtbDB.Jobs` (**= item #2 above**) | low |
| **Food** | `FoodOutput` ✅ · demand ✅ | `SustenanceProcessor` ✅ | starvation + quality→morale ✅ | **one coefficient** — `PerCapitaFoodDemand` = 0 (`ColonySustenanceDB.cs:23`); set it > 0, flag-gate, baseline `MoraleTests` | low |
| **Upkeep** | `UpkeepCredits` ⚠ (ground/lab only) | `GroundUpkeep` / `StationUpkeepProcessor` ✅ | `Money` ledger ✅ | **generalize** — a per-installation upkeep dial + a colony biller walking `ComponentInstancesDB` (mirror `GroundUpkeep.BillIfDue`) | low–med |
| **Power** | `WeaponSupply.PowerDraw_W` ⚠ (weapons/warp only) | — (no colony power Σ) | ground-unit power gate ✅ | **the only real build** — a generic component `PowerDraw` + a colony power Σ vs Solar/reactor supply → brownout/efficiency, same shape as infra (**C12**) | med |

**Why this is ONE item, not four:** three of the four are a *producer*, a *coefficient*, or a *generalization* —
the readers and the sums already exist. Only generic **power draw** is a genuinely new subsystem, and even it
should be built to the infrastructure grid's exact shape. **Do them together** and a built world finally costs
power, employs people, eats food, and drains the treasury — all driven by what's mounted, closing the four holes
the Capitol-world sim found.

- **⚠ Landmine.** An upkeep/power dial on a JSON-bound `*Atb` is exact-arity (gotcha 6) — add it in lockstep
  with every template that binds the atb, **or** put it on the design (as `GroundUnitDesign.UpkeepCredits` does)
  when it needn't be JSON-authorable.
- **Cradle to grave.** mineral → material → **build cost** (already summed) → component → **run cost** (this) →
  the decision → the loss. This is the missing *middle* of the chain: today a component costs to build and costs
  to lose, but nothing to *keep*.
- **Gauge.** Per cost: a built entity reports a non-zero run-cost, an empty one reports zero, and the colony Σ
  throttles/bills correctly (mirror `EconomyReadoutTests` + `MoraleTests` + `GroundUpkeepTests`).

---

## TIER 2.6 — The workforce → production STAFFING model  ⬜ NOT BUILT  *(NEW — developer ruling, 2026-08-10)*

**The developer's ruling, in his own words:** *"a population [should] be fractioned off into work force, and that
workforce is where crew and leaders take off off — but that workforce is useful because it ties into production
facility build rate … the way the workforce and population connect with production shouldn't be only for the design
but needs to be flagged to be in game so it plugs into the designers."* This is that flag: the model is proven in
`docs/ground/planetview.html` (rev-R), and this row is its engine punch-list so it becomes a real in-game wire that
every door designer plugs into.

- **What the model is, in plain English.** A colony's people split into a **workforce** (a fraction of the
  population — `ColonyManpowerDB.WorkforceFraction`, 0.5 today). Ships and officers **draw down** that workforce when
  you build/crew them (crew + leaders come off the top). Whatever workforce is **left** is what actually staffs the
  colony's factories, mines and yards — and **that staffing sets how fast they build.** A half-manned factory builds
  at half rate. It is the missing middle between "people" and "production": today population only *gates* a build
  (yes/no), it never *paces* one.
- **The engine gap — two readers, one that only gates.**
  - **Rate throttle TODAY = infrastructure only.** `IndustryTools.ConstructStuff` scales every production line's rate
    by exactly one factor — `rate.Value * infraEfficiency` (`IndustryTools.cs:117-121`). Nothing about how many
    workers are actually available touches the rate.
  - **Population is a BINARY GATE, not a throttle.** The one place manpower meets a build is the crew gate
    (`IndustryTools.cs:152-165` → `ManpowerTools.ResolveBuild`): it answers *CanBuild* yes/no and **holds** the job
    if short — it never scales the rate down when the workforce is thin. So a colony at 50 % population and one at
    90 % population build a Venator at the **same** speed right up until the gate trips (the planetview dig that
    produced this ruling).
  - **`ColonyManpowerDB` already carries the split** — `WorkforceFraction`, `AvailableBulk = Workforce −
    CommittedBulk` — but no production reader consults `AvailableBulk` against the colony's staffing *demand*.
- **What to build.** Add the workforce factor as a **second multiplier on the production rate**, exactly parallel to
  infra efficiency:
  > **production rate = base rate × workforce-staffing × infra-efficiency**
  > where **workforce-staffing = min(1, available-workforce ÷ Σ (every producing facility's `CrewReq`))**,
  > and **available-workforce = population × `WorkforceFraction` − committed (crew + officers drawn off)**.

  Concretely: sum every industry/mine/refinery/yard building's `CrewReq` into a colony **staffing demand**; divide
  the colony's `AvailableBulk` by it, cap at 1.0; multiply the rate by it at `IndustryTools.cs:121`. When the
  workforce covers demand, staffing = 1.0 and behaviour is byte-identical to today (so it flag-gates cleanly).
- **⚠ This is why "it plugs into the designers" matters — and it's the good kind of leverage.** `CrewReq` is set on
  **every** building in the door designers (`industrialderived.html`, `civicderived.html`) — a factory's crew, a
  mine's crew, a refinery's crew. The moment this reader is live, **each of those `CrewReq` numbers becomes a live
  staffing DEMAND**, not just a morale/employment number. Turning the mine's crew coefficient up in the industrial
  door now costs you *build rate* if the colony can't man it. That's the wire the developer is asking for: the
  designer dial and the production rate become the same conversation.
- **⚠ Interaction with the run-cost jobs item (#2).** The **same producer** — every facility's `CrewReq` — feeds
  *two* readers: the employment→morale term (#2) and this staffing→rate throttle. Build them to read one shared
  colony jobs total (`GetTotalJobs`), don't invent a second demand sum. One producer, two consumers.
- **Flag-gate + baseline (biggest-behaviour-change discipline, same as #2).** Turning this on slows every
  under-manned colony's production. Gate it behind a flag and baseline against `ProductionBuildTests` /
  `EconomyReadoutTests` so a fully-manned colony reads identical rates and only the under-manned case changes.
- **The gauge (write it).** A colony whose workforce fully covers its facility `CrewReq` builds at today's rate
  (byte-identical); the **same colony at half the population builds at ~half rate**; a colony that commits a big
  fleet's crew (drawing `AvailableBulk` down) sees its build rate drop by the committed share. (This is exactly the
  50 %-vs-90 % table the planetview Venator panel now shows against the *proposed* model.)
- **Cradle to grave.** population → `WorkforceFraction` splits off a workforce → crew/officers draw it down → the
  remainder staffs facilities (their `CrewReq`, set in the designer) → **staffing throttles the build rate** → a
  destroyed colony / a drafted fleet thins the workforce → production slows. Every rung is a place the player already
  acts.
- **Unblocks.** Population becoming a *decision that paces the economy* rather than a yes/no wall — and the door
  designers' `CrewReq` dials becoming a live rate lever. Modelled end-to-end in `planetview.html` rev-R
  (`renderVenator` → `proposed(popPct, committedM)`).

---

## TIER 3 — Engine readers the tools already flag (weld the reader; the tool keeps flagging until you do)

### 3. Guided-weapon damage — read the real warhead instead of the flat stub  ⬜ NOT BUILT
- **What the tool already promises.** The assembler shows a torpedo's stubbed `0.1 MJ/s` and says outright:
  *"the AUTO-RESOLVER stubs a missile at a flat 0.1 MJ/s … a real impact is GJ-scale … An engine gap, flagged
  not faked"* (`entityassembler.html:348`). The resolver sim tags every torpedo `stub:true` with a
  warhead-real MODEL toggle.
- **The engine gap.** `ShipCombatValueDB.cs:38,446` assigns a guided weapon a flat `MissileLauncherFirepowerStub`
  (0.1 MJ/s) and **never reads the real warhead** — `OrdnancePayloadAtb.tntEqMass` is sitting right there
  (`OrdnancePayloadAtb.cs:41-55`). So every torpedo ship's auto-resolve firepower is grossly under-counted
  (Sovereign, Miranda, ARC-170, the whole doubled-Federation fight).
- **What to build.** In the firepower sum, read the mounted ordnance's real warhead energy for a guided mount
  instead of the flat stub. One read site. (Calibration note: kinetic/warhead energy at closing speeds is
  GJ-scale vs kJ–MJ beam tuning — expect to divide it down, same open calibration the live
  `MissileImpactProcessor` carries.)
- **The gauge.** A torpedo ship's `ShipCombatValueDB` firepower scales with its warhead choice, not a constant.
- **Unblocks.** Any missile/torpedo ship's true firepower reading in auto-resolve.

### 4. Sensors band-match bug — consult the receiver's upper edge  ⬜ NOT BUILT
- **What the tool already promises.** The sensors door leads with this in red: *"the band-matching gate never
  checks the receiver's upper edge … the game currently depends on that bug"* and prescribes the exact fix
  (`sensorsderived.html:150, 219-233`).
- **The engine gap.** `SensorTools.cs:147` tests only `bandMin < sigMax` — the receiver's *upper* edge is never
  consulted, so a visible-light receiver detects infrared it physically can't see, and the band-centre dial has
  a single dominant (shortest) setting.
- **What to build.** One line — a standard overlap test: `max(recvMin, sigMin) < min(recvMax, sigMax)`.
- **⚠** This is a **correctness fix that changes detection behaviour** — it will move what gets detected, so
  baseline it against the sensor tests and expect to re-tune emitter/receiver bands so detection still works
  *by design* rather than *by bug*.
- **Unblocks.** Every EMCON/detection interaction — and the environment layer, whose first lever is detection.

### 5. `GroundMobility.SpeedMultForUnit` — SCALE the frame mode, don't replace it  ⬜ NOT BUILT
- **What the tool already promises.** The propulsion door presents the four locomotion modes (Foot ×1 /
  Tracked ×2 / Walker ×1.5 / Hover ×3) as *"the four modes the simulation already reads"*
  (`propulsionderived.html:269-278`) — i.e. the frame you pick is supposed to matter.
- **The engine gap.** `GroundMobility.SpeedMultForUnit` returns a designed `GroundLocomotionAtb.SpeedFactor`
  **outright** — it *replaces* the frame mode instead of scaling it. So the moment a unit carries a designed
  drive, whether its frame is Foot or Hover stops affecting speed at all; the four modes are dead weight on any
  properly-designed unit (flagged in `GroundCombat/CLAUDE.md`).
- **What to build.** Multiply the frame mode's multiplier by the designed `SpeedFactor` rather than replacing
  it. Small change to the one line.
- **⚠** Behaviour change → re-baseline the mobility gauges; **the developer's call** on the exact combine
  (multiply vs a weighted blend).
- **Unblocks.** Ground chassis frame choice becoming a real decision again.

---

## TIER 4 — North-star dead-knob cleanup (cut it, or build the subsystem — until then, keep it flagged)

These are dials the tools had to draw for completeness but which write nowhere. Each is already flagged honestly
in its door. The engine decision is **drop the knob** or **build the reader**; do not leave it looking live.

### 6. Console Space on a ship bridge  ⬜ DECISION PENDING
- **Flagged in:** command door.
- **Gap.** The dial `AdminSpaceAtb.cs:25` — its only reader (`ColonyHexMapProcessor.cs:67`) is gated on
  `ColonyInfoDB`, which a ship doesn't have. So on a ship bridge it writes nothing. The intended target is a
  ship-side command **`Span`** (`Sites/CommandBerthDB.cs:16`).
- **Do.** Cheapest: relabel/hide it on ship mounts. Better: build a ship-side `Span` reader so a bridge's seat
  count actually governs command capacity.

### 7. Chassis √-law structural-efficiency slider  ⬜ DECISION PENDING
- **Flagged in:** chassis door.
- **Gap.** Writes no engine field — `K_SHIP` has zero hits, research = 0 (`06-OUTPUTS-BY-DOOR.md:139`,
  `02-IO-MATRIX.md:81`). A slider that costs nothing and writes nothing = the exact north-star bug.
- **Do.** Drop it, or build the structural-efficiency research axis it implies. (Cheap to drop.)

### 8. Fighter Construction Points  ⬜ DECISION PENDING
- **Flagged in:** industrial door (`industrialderived.html:179, 239, 353, 364` — *"a real slider … NOT in the
  DataDict, no industry type"*).
- **Gap.** There is no `fighter-construction` industry type for it to feed; it's absent from the rate
  dictionary. `06-OUTPUTS-BY-DOOR.md:194`.
- **Do.** Drop the dial, or add the 6th industry type (fighter production) so it feeds something.

### 9. Carrier / parasite LAUNCH — build the sortie order + processor  ⬜ NOT BUILT
- **Flagged in:** the assembler ("Generalized bay — deployable / carried units" gate reads *pending*,
  `entityassembler.html:857`).
- **Gap.** The assembler now lets you *designate* a wing as deployable, but nothing launches it:
  `ParasiteLauncherReady` (`EventTypes.cs:400`) has zero emitters and `FighterStockpile`
  (`ColonyInfoDB.cs:36`) is write-only. No order sorties a wing.
- **Do (medium — a real new subsystem).** Build the launch order + a processor that consumes the carried wing
  and puts fighters into the fight, and the recovery/rearm side. This is the missing half of the carrier model.
- **Unblocks.** Every Star Wars fighter carrier — Venator's 420, ARC-170 parasites, the ISD's TIE wing, the
  Death Star's 7,000, and the 50-ARC-170 sortie from the franchise fleet battle.

### 10. Chassis Substrate axis (organic / bio frames)  ⬜ NOT BUILT
- **Flagged in:** chassis door.
- **Gap.** 8 substrate chips are offered in the designer UI (`01-IO-chassis.md:24-31`) but there is **no
  `Substrate` field** on any chassis attribute (`FRANCHISE-UNITS-BUILD.md:749`), so 7 of 8 write nothing. An
  organic unit is cosmetic today — all four ground frames are Mechanical.
- **Do.** Near-term: keep it flagged / stop implying it's live. Later: add a `Substrate` member to the chassis
  atb + the feed/regen/no-power consequences an organic frame implies.
- **Unblocks.** Organic/bio units (the Tyranids' *skin*).

### 11. Aura door — synapse / buff-and-debuff auras  ⬜ NOT BUILT (keep as an unbuilt proposal)
- **Flagged in:** aura door (marked PROPOSED / PENDING throughout).
- **Gap.** The entire door is 0/4 — zero engine `aura` matches, no `AuraAtb`, no per-tick neighbour-sweep pass
  (`06-OUTPUTS-BY-DOOR.md:259-263`). This is *correctly* an unbuilt proposal, not a law violation.
- **Do (most expensive — a new per-tick pass).** When funded: a neighbour-sweep pass that lets a unit project a
  morale/fury/buff field onto nearby friendly units (and a debuff onto enemies). Until then it stays a marked
  proposal.
- **Unblocks.** Tyranid synapse; any buff/debuff commander aura.

---

## NOT on this backlog — absent capabilities the audit refuted (don't chase them)

For honesty about where the line is, the audit corrected two dossier phrasings. These are **not** dead dials to
fix — the fields don't exist at all, so there's nothing to route:
- **Medbay "+health"** — no such dial; a sickbay is a readiness/coverage note. (The +health *output* is a
  separate engine-pending idea, already flagged as such in the assembler's medical gate.)
- **Propulsion navigator / "quietness" / suppression** — "quietness" is already the wired **Sensor Signature**
  dial; a navigation bonus is explicitly unbuilt. A **rear firing-arc** likewise has no dial and no model, and
  adding one would cut against the aggregate / whole-or-dead combat design (and *One Verb, Both Seats*).

These would be **net-new builds if ever wanted**, not wiring fixes.

---

## Also NOT here — the pure sim artifacts (the simulator's own honest abstractions)

These are the resolver sim's *own* modelling choices, correctly kept **out** of the unit specs (the developer's
"don't change unit specs for the sim's sake"), so they are neither engine bugs nor tool gaps:
- the **closing-speed** stand-in (a ship's sublight speed isn't a `ShipCombatValueDB` field — it's derived from
  thrust ÷ mass; the sim uses an editable stand-in);
- the metre-scale **`minGap`** contact floor and the **`salvoScale`** pace dial added for the ground battle;
- the **LD-30 arena** and **degrade-health** models (design targets the shipped resolver doesn't run yet);
- **space combat's environment-blindness** (the env layer is a design hook — see
  `docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md`).

---

## How to close a row

When you weld one of these pipes:
1. Build it (with its gauge) behind a flag where it changes behaviour; get CI green (`test` + `build-client`).
2. Flip the item's ⬜ to ✅ here, and flip the matching honesty flag in the design tool (the door / assembler /
   sim) from *pending/dead* to *live* — the tool and the engine must never disagree about what's real.
3. Update `docs/DOCS-INDEX.md` and `docs/TESTING-TRACKER.md` in the **same commit**.

*Companions: the ranked analysis [`SIM-DRIVEN-DESIGNER-AUDIT-2026-08-05.md`](SIM-DRIVEN-DESIGNER-AUDIT-2026-08-05.md);
the canonical per-door reader trace `docs/assembler/06-OUTPUTS-BY-DOOR.md`; the law
`docs/economy/DESIGNER-NORTH-STAR.md:74-76`.*
