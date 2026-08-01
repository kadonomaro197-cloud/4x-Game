# 03 — CORRECTION PLAN (Phase 3 deliverable)

> **What this is:** for every broken or absent connection Phase 2 found, the **smallest correction** that
> makes it work — stated as a change to the DESIGN (what a designer must output, what a consumer must
> accept), **dependency-ordered** (what unblocks what), each with its **gauge** (the test that proves it)
> and its **blast radius** (what else it touches). **This is a plan for the developer to approve — nothing
> here is implemented.** Read `02-OUTPUT-READABILITY-AUDIT.md` first; every correction cites the Phase-2
> verdict it answers.
>
> **The organizing idea (from Phase 2 §1, Theme 1):** most of these are cheap, because most "missing" is
> really *built-but-dormant*. The plan is sequenced cheapest-and-safest first, so early wins light up wires
> that already read correctly, and the expensive builds come last — after the boundary rulings that govern
> them.

---

## 0. The five correction kinds (buckets), cheapest first

| Bucket | Kind | Engine risk | Count |
|--------|------|-------------|-------|
| **C** | Re-label the designer's output (documentation) | none | 5 |
| **B** | Add a data line (attribute exists, no template carries it) | low | 2 |
| **A** | Flip a flag / set a coefficient (built, switched off) | **medium–high** (turns advisory into enforced) | 4 |
| **D** | Mirror a ground gate onto ships (small engine build) | medium | 3 |
| **E** | Build a missing mechanism (design → feeds Phase 4) | high (real build) | 11 |
| **F** | Deferred / blocked on an engine-model change | — | 2 |

---

## 1. THE MASTER TABLE — every correction, at a glance

| ID | Answers | Correction (smallest) | Gauge | Depends on |
|----|---------|-----------------------|-------|------------|
| **C-relabel-1** | A-armour misattribution | Designer says armour nature-resist is read by `FleetArmourSoakFraction`, not `CombatKernel` | doc grep | — |
| **C-relabel-2** | E-signature misattribution | Signature output points at `EmconActivityProcessor`, not static `SensorSignatureAtb` | doc grep | — |
| **C-relabel-3** | F-intel misfile | IntelDirectorate output points at `Factions/`, not `Sensors/` | doc grep | — |
| **C-relabel-4** | F-detection trigger | Detection-range feeds READOUT/coverage; the combat TRIGGER reads `SensorContactExists` | doc grep | — |
| **C-relabel-5** | Theme 5 wrong-mechanism | Logistical stops calling ammo/troops "cargo classes"; names `ShipMagazineAtb`/`GroundMagazineAtb`/`GroundBayAtb` | doc grep | — |
| **B-data-1** | C1 jobs producer | Declare `EmploymentAtbDB` — **but scale Jobs to the workforce first** (denominator is pop×0.5 = billions) | `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld` (existing tripwire) + a jobs-ratio sensor | **the denominator ruling** (see detail) + E-rule-2 |
| **B-data-2** | C5 fighter build | Cut the dead slider+stockpile (fighters = small ships) **or** build the full channel | `BaseModIntegrityTests` (cut) / fighter-lands-in-stockpile (build) | developer choice |
| **A-flip-1** | B-mass advisory | **Already enforced client-side** — don't flip the engine default; add a scenario-faction enforcement gauge | new cross-faction `OverMassBudget==false` test | none (already ships) |
| **A-flip-2** | C-food/E-power dormant | Per-scenario `strain` node, **food-only, supply-building-first**; power stays 0 | new-game colony `FoodShortage==0` after 30d | add a farm to earth.json first; **power blocked: no colony power building exists** |
| **A-flip-3a** | E-reactor-heat dormant | Enable `EnableReactorHeat` (low blast, additive to signature) | re-baseline `DetectionTuningTests` with flag on | re-baseline detection gauges |
| **A-flip-3b** | E-fuel dormant | Enable `EnableFuelExhaustion` — **later**; slow lockout risk | endurance gauge (drain past reactor Lifetime) | fuel READOUT + reactor-Lifetime audit + save migration |
| **A-flip-4** | fire-control off | Enable `EnableFireControl*` so track/range affects fire | detection×weapons combat test | C7 band-gate fix |
| **D-gate-1** | B-ship gates | Mirror reactor/magazine gate — **behind a default-off `EnforceWeaponSupplyGates` flag** (invalidates 12+ ships naively) | new: all armed designs stay `IsValid` with flag off; arithmetic calibration readout | add magazines to ammo ships; **resolve battery-buffer power model** |
| **D-gate-2** | A-firepower caliber | Fold `UnitCaliberFirepowerMult` into per-weapon `dps` at build; drop the redundant aggregate multiply | two ships differing only by a firepower cadre — calibered one wins live | none (no double-count: branches mutually exclusive) |
| **D-gate-3** | A-ship penetration | Wire ship-weapon penetration (currently hard-0) | ship armour-pierce combat test | — |
| **E-build-1** | C3 colonist transport | Colonist unload adds to `ColonyInfoDB.Population` | settle-a-world integration test | C-relabel-5 |
| **E-build-2** | C6 drive-heat | Drives emit heat into `FleetCombatStateDB.HeatPool_kJ` | heat-vs-EMCON combat test | A-flip-3 |
| **E-build-3** | C11 frame-size carry | `CarrySizeOf` reads a frame Size dial, not a 3-value switch | transport-capacity test | E-rule-1 |
| **E-build-4** | C12 generic power-draw | A generic `PowerDraw` field the balance nets against output | power-balance test | — |
| **E-build-5** | C15 shield power | Shield capacity/regen costs reactor power | shield-under-brownout test | E-build-4 |
| **E-build-6** | C8/C9 command agency | A seat's occupancy OR its `AdminLevel` drives a real effect | seated-officer-changes-outcome test | E-rule-3 |
| **E-rule-1** | C9 door boundary | RULE: Structure=Chassis, Evasion=Propulsion; Defense owns Shield+Armour only | doc + design-time assertion | — |
| **E-rule-2** | C10 door boundary | RULE: one door of record for colony housing/pop-support | doc + no-double-count test | — |
| **E-rule-3** | C8 span meaning | RULE: what `AdminLevel`/span actually constrains | doc | — |
| **E-build-7** | C2 LogiBase | Decide `LogiBaseAtb` capacity's consumer, or cut the dial | logistics-cap test, or removal | — |
| **E-build-8** | DockBay unused | An order that calls `DockTools.TryDock` | dock/undock order test | — |
| **E-build-9** | B7 mount gate | `Entity.AddComponent`/design validates part vs `IChassisAtb.PartMount` | wrong-mount-rejected test | — |
| **E-build-10** | B6 envelope | If the Chassis designer needs an operating-band price, build the field | band-cost design test | — |
| **F-defer-1** | C13 self-repair | Needs a damaged/degraded state (whole-or-dead today) | — | engine-model change |
| **F-defer-2** | C14 foresight/initiative | Needs an initiative variable (combat is simultaneous) | — | engine-model change |

---

## 2. DEPENDENCY-ORDERED BUILD ORDER

The safe sequence. Each wave only starts once the wave it needs is done.

**Wave 0 — free & unblocking (do first, zero engine risk):**
- All **C-relabel-1..5** (documentation — correct where the designer's outputs point).
- All **E-rule-1..3** (the boundary RULINGS — decide who owns Structure/Evasion, housing, and what span
  means). These cost nothing to *decide* and every later build that touches those numbers depends on them.

**Wave 1 — cheap data & self-contained fixes (no ruling, no risky flip):**
- **A-flip-1** (add the scenario-faction mass-enforcement gauge — mass is *already* enforced client-side, this
  just closes the umf/kithrin blind spot), **B-data-2** (cut-or-build fighters), **D-gate-2** (firepower-caliber
  live fix), **D-gate-3** (ship penetration), **E-build-4** (generic power-draw), **E-build-9** (mount gate),
  **E-build-7** (LogiBase decide-or-cut), **A-flip-3a** (reactor-heat signature, after re-baselining the two
  detection gauges).

**Wave 2 — the jobs & demand loop (needs a ruling + producers in place):**
- **B-data-1** (jobs producer) — after the **denominator ruling** and **E-rule-2** (housing/jobs door of
  record). This is the highest-value morale fix but the one most likely to backfire — do it carefully.
- **A-flip-2 food half** (per-capita food demand) — after a farm is added to `earth.json` in the same commit.
- **E-build-11** (colony power generator) — the hard prerequisite for the *power* half of A-flip-2.

**Wave 3 — the enforced gates & later flips (need valid start content):**
- **D-gate-1** (ship reactor/magazine gates) — behind the default-off `EnforceWeaponSupplyGates` flag; add
  magazines to the ammo ships and resolve the battery-buffer power model **before** the flag can bite.
- **A-flip-2 power half** — only after **E-build-11** (a colony can produce power).
- **A-flip-3b** (fuel exhaustion) — after the fuel readout + reactor-Lifetime audit + save migration.
- **A-flip-4** (fire control) — after the **C7** band-gate fix.

**Wave 4 — the missing mechanisms (real builds → many become Phase-4 designs):**
- **E-build-1** (colonist transport), **E-build-2** (drive-heat), **E-build-3** (frame-size carry, after
  **E-rule-1**), **E-build-5** (shield power, after generic power-draw), **E-build-6** (command agency, after
  **E-rule-3**), **E-build-8** (dock order), **E-build-10** (envelope, if wanted).

**Wave 5 — deferred:** **F-defer-1/2** — only after the engine gains a damaged state / an initiative variable.

**Also independent of all waves:** the **C7 band-gate math fix** (`SensorTools.cs:147`) — a one-line
comparison correction with high value (every detection wire rides it). It's not a designer-connection fix per
se (it's an engine bug), so it lives outside the buckets, but it gates **A-flip-4** and should be done early.

---

## 3. THE CORRECTIONS IN DETAIL

### Bucket C — Re-label the designer's output (Wave 0, zero engine risk)

These are the cheapest corrections in the whole plan: the *data already flows correctly*; the designer's
description of **where** it goes is wrong (Phase 2 Theme 4/5). Fix the labels so every later build targets the
real consumer. (These are edits to the designer docs, which the developer applies — this plan only specifies them.)

- **C-relabel-1 — Armour.** Defense's armour nature-resist output is consumed by the inline
  `CombatEngagement.FleetArmourSoakFraction` (`:1536-1537`), **not** `CombatKernel.ArmourSoak` (which is the
  ground path). *Gauge:* grep the designer text for "CombatKernel armour" and repoint.
- **C-relabel-2 — Signature.** Power's reactor-load signature and Propulsion's thrust signature are read by
  `EmconActivityProcessor` (`:64/:114-115`), **not** the static `SensorSignatureAtb` (design-time only).
- **C-relabel-3 — Intel.** `IntelDirectorateAtb` lives in `Factions/` and is consumed by
  `IntelDirectorateProcessor`/`Espionage`, **not** `Sensors/`.
- **C-relabel-4 — Detection trigger.** `RangeForSignal` feeds map/coverage READOUTS; the combat TRIGGER
  reads `SensorContactExists` (the live scan). The Sensors designer must not claim its reverse-solved range
  triggers combat.
- **C-relabel-5 — Ammo/troops mechanism.** The Logistical designer's "cargo class taxonomy" must stop listing
  **ammo** and **troops** as `CargoStorageAtb` classes — they are separate components (`ShipMagazineAtb` /
  `GroundMagazineAtb` for ammo, `GroundBayAtb` for troops). Fuel and general/perishable cargo ARE real cargo
  classes; ammo/troops are not.

### Bucket B — Add a data line

- **B-data-1 — Jobs producer (C1). ⚠ A DENOMINATOR problem, not just a data add.** *Problem:*
  `GetTotalJobs`→`ColonyMoraleDB` employment term is fully wired but always 0 because **no template declares
  `EmploymentAtbDB`**. *The trap the trace found:* `employmentRatio = jobs / workforce`, and
  **`workforce = population × 0.5`** (`ColonyManpowerDB.cs:25,45`) — on Earth that's **~4.1 billion**. The
  `-1` "no job data" sentinel is what keeps the term neutral today; the instant *any* installation declares
  `Jobs>0`, the sentinel vanishes and `jobs / 4.1e9` is read **literally**. It's a **step, not a ramp**: 0
  jobs = neutral; any small fixed jobs number (thousands) = ratio ≈ 0 = read as near-total **unemployment,
  −25 morale**. A naive per-building constant flips homeworld morale 50 → ~25 on the first population tick and
  **reds `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld`** (morale < 50 and emigration shrinks pop).
  This is a units mismatch baked into the M2 design (jobs conceived as installation slots, divided by a
  billions-scale workforce). *Correction — settle the denominator FIRST, then seed at population scale:* pick
  one of (1) **re-scope the denominator** (measure jobs against the workers an installation actually needs, or
  against a population *segment* that must be employed, not the whole 4.1e9); (2) **author `Jobs` as a
  population-scaled formula** — an NCalc `PropertyFormula` that scales like `Support Colonists`, so total jobs
  track the population a building holds (ratio ≈ 1.0), not a flat integer; or (3) **exempt native/homeworld
  colonies** (keep the sentinel there) and light the term only on small frontier colonies where jobs and
  workforce are the same order of magnitude. *Smallest safe form:* add ONE `Jobs` Property to the
  `default-design-infrastructure` template (every colony carries it — `installations.json:1021`) whose formula
  scales with its `Support Colonists` capacity, wired exactly like the sibling `HousingAtbDB`
  (`installations.json:1140-1144`) via `AtbConstrArgs(PropertyValue('Jobs'))`; add it to one template + one
  colony (Earth), watch the tripwire, then widen. `EmploymentAtbDB`'s `double` ctor accepts the NCalc value,
  so no L13 ctor-overload save risk. *Gauge:* the existing `MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld`
  tripwire + a new sensor asserting `GetTotalJobs()/Workforce ≈ 1.0` (full employment, not a penalty).
  *Depends on:* the denominator ruling above **and** **E-rule-2** (jobs on the Civic or Industrial door — no
  double-count with housing).

- **B-data-2 — Fighter build channel (C5).** *Problem:* the factory designer exposes "Fighter Construction
  Points" but there is no `fighter-construction` industry type (`industryTypes.json` has exactly five:
  refining, component-, installation-, ordnance-construction, ship-assembly), so the slider is dead. **Open
  question now resolved (my check):** there is *no* bespoke fighter build path either — nothing writes
  `ColonyInfoDB.FighterStockpile` (grep for writers is empty); it is a vestigial list with a producer on
  neither end. So **both** the slider and the stockpile are dead, with no path between them. *Correction —
  a developer decision between two clean options:*
  - **(a) Cut both.** Treat fighters as small **ships/components** built through the existing `ship-assembly`
    / `component-construction` types; delete the dead "Fighter Construction Points" property and the unused
    `FighterStockpile`. Least code, honours "abilities are components — don't invent parallel systems."
  - **(b) Build the full channel.** Add a `fighter-construction` UniqueID to `industryTypes.json`, map the
    property into the "Construction Points" DataDict (`installations.json:441-443`), **and** add a
    build-completion path that deposits finished fighters into `FighterStockpile`. More work; only worth it
    if fighters must be a distinct production class from ships.
  *Recommendation:* (a) unless the developer wants fighters as their own build class. *Gauge:* for (a),
  `BaseModIntegrityTests` still green after the slider/stockpile removal; for (b), a fighter built through
  the new line lands in `FighterStockpile`.

### Bucket A — Flip a flag / set a coefficient (blast radii traced — several are NOT simple flips)

> The blast-radius trace (workflow `wzclp795b`) overturned the naive "just flip it" framing for most of these.
> The engine's ~16 `Enable*`/`Enforce*` static flags default **false on purpose** — to keep the CI
> byte-identity gauges honest — and the **client** flips the ones the live game wants at startup
> (`PulsarMainWindow.cs:144`). So "flip the flag" often means "the client already did; the real gap is
> elsewhere." Details below.

- **A-flip-1 — Mass budget is ALREADY enforced in-game.** *Finding:* `ShipDesign.EnforceMassBudget=false`
  is the engine default, but the **client sets it true at startup** (`PulsarMainWindow.cs:144`), and **nothing
  in the engine reads `ShipDesign.IsValid`** — the only build-gating consumer is the client's production-list
  filter (`IndustryDisplay.cs:112 if(!design.IsValid) continue;`). So an over-budget design is already hidden
  from the build dropdown; flipping the engine default buys nothing and would make the byte-identity test
  comments stale. *The real correction:* the base-mod ships are proven under budget, but the **scenario-faction
  ships (umf/kithrin/uef-devtest/shipDesigns.json) have no enforcement gauge** — an over-budget UMF/Kithrin
  warship would silently vanish from the build list. *Gauge to ADD:* mirror `DevTestFleetRoleReadoutTests`
  (loads all factions) with `EnforceMassBudget=true`, and assert `OverMassBudget==false` for every scenario
  faction's ships. *Smallest safe form:* keep the client-only flip; if per-hull control is later wanted, add a
  `bool EnforceBudget` on `ShipHullAtb` (a JSON dial) rather than a global behaviour change.

- **A-flip-2 — Per-capita demand: food-only, per-scenario, supply-first. Power is HARD-BLOCKED.** *Finding:*
  `PerCapitaFoodDemand`/`PerCapitaPowerDemand` default 0 (`ColonySustenanceDB.cs:21,23`) as a deliberate
  neutral-when-absent guard. The intended write path is **per-scenario** — the faction `strain` node →
  `FactionFactory.ApplyOpeningStrain` → `SetDemand` (the mechanism DevTest already uses: `umf.json:577-580`,
  `uef-devtest.json:257-259`). **The default `earth.json` colony installs no farm and no power plant.** So any
  nonzero demand on it makes `Shortage(demand, 0)=1.0` for both → on the 8.2-billion homeworld:
  FoodShortage=1.0 → −40 morale **and 10%/month starvation deaths**; PowerShortage=1.0 → −30 morale → morale
  clamps to 0 → mass emigration → legitimacy collapse → rebellion. *Correction (food half):* in ONE commit,
  add a food-production building to `earth.json` sized to the population **and** a `strain` node on `uef.json`
  with `foodDemandPerCapita` set so `pop × rate ≤ installed food output` (`powerDemandPerCapita` stays 0).
  Apply the gotcha-10 two-ended data rule (the farm's materials + template id in `StartingItems`). *Power half
  is BLOCKED:* **no colony-installable power generator exists** — `SustenanceProcessor.cs:60` reads
  `EnergyGenAbilityDB.TotalOutputMax`, but no `installations.json` template grants it (`Colonies/CLAUDE.md`
  confirms "a power-supply component is the remaining gap"). Power demand must stay 0 until that component is
  built (a Bucket-E build). *Gauge:* on a fresh New Game, after the first `SustenanceProcessor` run,
  `colony-earth.FoodShortage==0`; plus a `BaseModIntegrityTests`-style check that every starting colony with
  nonzero demand has installed food output ≥ pop × demand.

- **A-flip-3a — Enable reactor-heat signature (low blast, do FIRST).** *Finding:*
  `EmconActivityProcessor.EnableReactorHeat=false` (`:57`). Turning it on folds `1 + ReactorHeat × Load` into
  every powered ship's emitted-signature `ActivityMultiplier` — purely **additive to detection range,
  reversible, no lockout**, and its one prerequisite (the `CalcLoad` bug) is already fixed. *Correction:*
  enable it. *Blast radius:* detection/detectability ranges shift for all powered ships at once (fog-of-war
  contact ranges, first-contact timing, auto-pause alerts) — but combat targeting/damage is untouched.
  *Gauge:* re-baseline the absolute-range asserts in `DetectionTuningTests`/`RangeReadoutTests` with the flag
  on (they're baked at `ActivityMultiplier=1.0`). *Separate smaller fix:* the **thrust signature is boolean ×
  const** (`ThrustHeat=4.0` when `ManuverDeltaVLen>0` — a light burn is as loud as a full burn,
  `EmconActivityProcessor.cs:42,70-82`); if loudness should scale with burn intensity, replace it with a
  magnitude term — its own change with its own detection gauge.

- **A-flip-3b — Enable fuel-exhaustion (LATER; slow lockout risk).** *Finding:*
  `EnergyGenProcessor.EnableFuelExhaustion=false` (`:32`). *Blast radius:* no immediate break (start ships
  mount the ~12.7-yr steam-turbine and `LocalFuel` seeds positive), but **reactors are non-refuelable**, so
  over a long game every fuel-burner dries → capacity 0 → `EnergyStored` drains → ships silently can't warp
  (`WarpMoveCommand`) or fire energy weapons (`GenericFiringWeaponsProcessor`), and the AI (`MilitaryReach`)
  stops planning attacks with them. A 1-yr fission-reactor strands a mid-game ship. *Prerequisites before
  enabling:* (a) a player-visible **fuel/endurance readout** (Visibility Gate — a ship going dark from empty
  fuel must be *seen*, not a silent freeze), (b) an audit of every reactor's `Lifetime` and/or a refuel
  mechanic, (c) a save-migration pass so a pre-floor save with `LocalFuel<0` doesn't instant-starve on load.
  *Smallest safe form:* arm per-scenario/difficulty first, validated on the DevTest sandbox, paired with the
  readout. *(Colony life-support is safe — `SustenanceProcessor` reads `TotalOutputMax`, which starvation
  deliberately leaves untouched.)*

- **A-flip-4 — Fire control.** The `EnableFireControl*` flags are off, so `BeamFireControlAtbDB` tracking/range
  don't affect the base fire gate (Theme-3 TYPE-MISMATCH: fire is hard-coded-range only). *Correction:* enable
  so sensors/fire-control gate weapon fire. *Gauge:* a detection×weapons combat test (a blind fleet can't hit).
  *Depends on:* the **C7** band-gate fix (fire control rides detection).

### Bucket D — Mirror a ground gate onto ships (Wave 1/3)

- **D-gate-1 — Ship reactor + magazine gates. ⚠ Behind a default-off flag, and NOT verbatim from ground.**
  *Problem:* the "energy weapon needs a reactor" and "ammo weapon needs a magazine" gates exist only on
  `GroundUnitAssembly` (`:258/:261`); `ShipDesign.Recalculate` reads neither. *The trap the trace found:*
  adding these UNCONDITIONALLY invalidates the shipping roster. The **ammo gate** would mark **~12+ start/
  scenario ships invalid** — every railgun/flak ship except the one Sabre that carries a magazine (Lancer,
  Bulwark, Wasp, Leviathan, Redoubt, Culverin, Bombard, plus the **entire UMF/Mars rival fleet** — the AI's
  whole surface navy). The **energy gate rests on a power-model mismatch**: ships use **battery-buffered draw**
  (a beam draws from *stored* energy over its charge period; the reactor recharges the battery between shots —
  that's why `EnergyStoreMax`/`ChargeReactors` exist), so the ground gate's "instantaneous flux > reactor
  output" test would wrongly invalidate every battery-buffered energy warship (Aegis, Picket, Bastion,
  Praetorian…). Downstream: an invalidated design silently disappears from the client build list
  (`IndustryDisplay.cs:112`) — no crash (the engine build path never reads `IsValid`), but the player/AI can't
  queue it. *Correction — mirror the MassBudget pattern exactly:* add a `public static bool
  EnforceWeaponSupplyGates = false` flag guarding both gates, set-`IsValid=false`-only, so with the flag off
  every design is byte-identical. *Prerequisites before the flag can bite:* (a) **ammo** — add a ship magazine
  to every ammo-weapon start/scenario design (gotcha-10 registration if a new id); (b) **energy** — first
  RESOLVE the power-model question (must a reactor sustain peak flux, or is battery-buffered draw legitimate?)
  — if buffered draw is legitimate, the gate must account for `EnergyStore` capacity/recharge, **not** just
  `PowerOutputMax`; (c) note `WeaponSupply.MagazineCapacity_kg` reads `GroundMagazineAtb` — a **ship variant**
  reading `ShipMagazineAtb` is needed. *Gauge:* a new test building every `shipDesigns.json` design and
  asserting `IsValid` with the flag off, plus an arithmetic readout (`Σ PowerDraw ≤ Σ ReactorOutput` per
  energy ship; `AmmoCapacity_kg>0` per ammo ship) run *before* flipping, so the would-be-invalid set is known.

- **D-gate-2 — Firepower-Caliber live fix (no double-count).** *Problem (Theme 3):* the caliber firepower
  multiplier scales only the aggregate `cv.Firepower` (`ShipCombatValueDB.cs:529`), which only **test-only**
  `AutoResolve` sums; the live `CombatEngagement.BuildFireMix` reads **per-weapon `w.DamagePerSecond`**
  (`:1348`), which the caliber never touches — so the bonus is inert in real combat. *Key detail I verified:*
  the per-weapon term is `w.DamagePerSecond * cs.FirepowerMult`, where `cs.FirepowerMult` is the **fleet-doctrine
  posture** mult (`FleetDoctrineDB.FirepowerMult`), **not** the caliber. And the two live branches — per-weapon
  (`if cv.Weapons.Count>0`) vs the `cv.Firepower` fallback (`else if cv.Firepower>0`) — are **mutually
  exclusive**, so a ship uses one or the other, never both. *Correction:* fold `UnitCaliberFirepowerMult(ship)`
  into each per-weapon `dps` where `cv.Weapons` is built (`ShipCombatValueDB.cs:359/381/400/448`), and **drop
  the now-redundant aggregate `firepower *= UnitCaliberFirepowerMult` at `:529`** (the summed `cv.Firepower` at
  `:524` inherits the caliber from the already-scaled per-weapon dps). *No double-count:* the per-weapon and
  fallback paths never combine, and removing `:529` prevents the aggregate from scaling twice. *Gauge:* two
  identical ships differing only by a firepower cadre — the calibered one deals more damage in the LIVE
  resolver; `AutoResolveTests` still pass because `cv.Firepower` ends up the same value.

- **D-gate-3 — Ship penetration.** *Problem:* `WeaponProfile.Penetration` is hard-0 on the ship side, so armour
  penetration only works on the ground. *Correction:* let ship weapon builders write penetration (as the ground
  weapons do) so the ship armour step honours it. *Gauge:* a ship armour-pierce combat test. *Note:* the ship
  armour path uses `FleetArmourSoakFraction`, not the kernel flat `ArmourSoak` — so this correction must decide
  whether ship armour adopts the kernel's `armour − penetration` step or adds penetration to the inline path.

### Bucket E — Build a missing mechanism (Wave 0 rulings, then Wave 4 builds)

**The three RULINGS first (E-rule-1..3, Wave 0 — decisions, not builds):**
- **E-rule-1 — Structure/Evasion door of record (C9).** Phase 2 confirms Structure is sourced from Chassis and
  Evasion from Propulsion (both `READS` there), *not* Defense. *Ruling to make:* Defense owns **Shield + Armour**
  only; the Defense designer must READ Structure (Chassis) and Evasion (Propulsion) as inputs, not claim to set
  them. This unblocks **E-build-3** (frame-size, a Chassis-side number) and clarifies the Defense designer.
- **E-rule-2 — Housing door of record (C10).** Both Industrial and Civic can carry housing/pop-support outputs.
  *Ruling to make:* one door owns colony housing (the natural choice is **Civic** — residency/comfort/pop-support
  — with Industrial buildings only *providing jobs*, not housing). This unblocks **B-data-1** (so jobs and
  housing don't double-count in morale).
- **E-rule-3 — Span meaning (C8).** `AdminLevel`/`SeatType` is a label no rule reads. *Ruling to make:* decide
  what span-of-control actually constrains (how many fleets/colonies a seat may command, or a competence cap by
  scope) — or formally park it. This unblocks **E-build-6**.

**Then the builds (Wave 4):**
- **E-build-1 — Colonist transport (C3).** Design + build the unload path: `CargoStorageAtb('passenger'/'cryogenic')`
  → on dock/unload, add carried colonists to `ColonyInfoDB.Population`. *Gauge:* load colonists at A, fly to B,
  unload, assert B's population rose by the carried count. *Feeds Phase 4* (the "settle a world" design) and
  Phase 5's settle simulation. *Depends on:* **C-relabel-5** (name the real component).
- **E-build-2 — Drive-heat (C6).** Design + build a drive-heat producer feeding `FleetCombatStateDB.HeatPool_kJ`
  the way weapons do (`CombatEngagement.cs:717`), so a hard-burning fleet heats up (and, with EMCON on, lights
  up). *Gauge:* a fleet that burns hard gains heat/signature. *Depends on:* **A-flip-3** (EMCON on) to be visible.
- **E-build-3 — Frame-size carry (C11).** Replace the hard-coded 3-value `CarrySizeOf` switch
  (`GroundTransport.cs:29-35`) with a read of the unit's frame Size dial (the code comment already anticipates
  this: "become strength-scaled once the designer has a size knob"). *Gauge:* a bigger-frame unit takes more
  transport room. *Feeds Phase 5* take-a-planet. *Depends on:* **E-rule-1** (Size is a Chassis-door number).
- **E-build-4 — Generic power-draw (C12).** Add a generic per-component `PowerDraw` field that the power balance
  (`EnergyGenProcessor.cs:59-62`) nets against output, so sensors/shields/life-support cost power (today only
  warp + energy weapons draw). *Gauge:* installing a power-hungry component reduces net available power. *Unblocks*
  **E-build-5**.
- **E-build-5 — Shield power (C15).** Once generic power-draw exists, make shield capacity/regen draw it, so a
  browned-out reactor can't hold shields (`ResolveShield` currently regens free). *Gauge:* shield regen falls
  under a power shortage. *Depends on:* **E-build-4**.
- **E-build-6 — Command agency (C8/C9).** Make occupying a seat *do something* — the seated commander's competence
  drives the delegated system's rate/quality (mirroring how the flagship `ShipInfoDB.CommanderID` already drives
  combat competence). *Gauge:* a colony with a skilled governor seated outproduces one without. *Depends on:*
  **E-rule-3**. *(Note: the competence machinery already exists — `CommanderBonuses`, `SiteWorkProcessor`,
  `LegitimacyDB.GovernorCompetence` — so this is largely wiring the admin-seat occupant into those, not new math.)*
- **E-build-7 — LogiBase decide-or-cut (C2).** `LogiBaseAtb` capacity has zero readers. *Correction:* either give
  it a consumer (a real cap on listings/transfers/ship-bids in `LogisticsProcessor`) or cut the dial. *Gauge:* a
  logistics-capacity test, or its removal with no behaviour change.
- **E-build-8 — Dock order.** `DockTools.TryDock` is complete but has no game caller ("the order is the next
  slice"). *Correction:* add the dock/undock order. *Gauge:* a dock/undock order test.
- **E-build-9 — Mount gate (B7).** `Entity.AddComponent`/design accepts any component regardless of
  `ComponentMountType`/`IChassisAtb.PartMount` (only ordnance filters). *Correction:* validate a part against
  its chassis' allowed mount. *Gauge:* a wrong-mount part is rejected.
- **E-build-10 — Envelope price (B6).** No operating-envelope multiplier field exists. *If* the Chassis designer's
  operating-band choice must cost something, build the field and price it. *Gauge:* a band-cost design test.
  *(Lower priority — flagged only because the Chassis designer references it.)*
- **E-build-11 — Colony power generator (NEW — found by the A-flip-2 trace).** *Problem:* there is **no
  colony-installable power-generation component**. `SustenanceProcessor.cs:60` reads
  `EnergyGenAbilityDB.TotalOutputMax` for the colony power-shortage morale term, but no `installations.json`
  template grants `EnergyGenAbilityDB` (`energy.json`'s reactor/RTG/turbine/solar carry `EnergyGenerationAtb`,
  and only `solarArray` is `PlanetInstallation`-mountable). `Colonies/CLAUDE.md` calls this "the remaining
  gap." *Correction:* make a power-generation building installable on a colony (the `solarArray` is the
  natural candidate — confirm it grants a colony `EnergyGenAbilityDB`). *This is the hard prerequisite for the
  power half of A-flip-2* — the power-shortage morale wire cannot come alive until a colony can produce power.
  *Gauge:* a colony with the power building has `EnergyGenAbilityDB.TotalOutputMax > 0`; then power demand can
  be set. *Feeds Phase 4* (a "colony power" design) if the developer wants power as a designed capability.

### Bucket F — Deferred / blocked

- **F-defer-1 — Self-repair (C13).** Enhancer self-repair needs a damaged/degraded state; the engine is
  whole-or-dead. Defer until a partial-damage state exists (a large engine-model change).
- **F-defer-2 — Foresight/initiative (C14).** An enhancer that buys "act first" needs an initiative variable;
  combat is simultaneous. Defer until an initiative/reaction dimension is added.

---

## 4. What this hands the next phases

- **Phase 4 (design the missing)** takes the **E-build** items that are genuine new mechanisms needing a
  designed door/dial at the full standard: colonist transport (E-build-1), drive-heat (E-build-2),
  generic power-draw + shield power (E-build-4/5), command agency (E-build-6), and the envelope price
  (E-build-10) if wanted. Each gets a `04-MISSING-DESIGNS/` file.
- **Phase 5 (simulation)** will stress the *ordering* here: the bootstrap sim checks the jobs/demand loop
  (B-data-1 + A-flip-2), the take-a-planet sim checks the transport chain (E-build-1/3 + C-relabel-5), and
  the economy sim checks the housing/jobs ruling (E-rule-2). Any deadlock the sims find feeds back here.

---

## 5. Five things the blast-radius trace changed about this plan

The source trace (workflow `wzclp795b`) overturned the naive framing of the riskiest corrections — worth
stating plainly, because each was a "just flip it" item that turned out to be a trap:

1. **Mass-budget enforcement is already on** in the live game (client sets it; the engine ignores `IsValid`).
   The correction isn't a flip — it's a **missing test** for scenario-faction ships.
2. **The jobs fix will backfire if done naively.** `employmentRatio = jobs / (pop×0.5)` — a fixed jobs number
   on the billions-scale homeworld reads as −25 morale and reds CI on turn 1. Jobs must be **population-scaled**
   or the denominator re-scoped first.
3. **The ship reactor/magazine gates would delete ~12+ shipping designs** (including the AI's whole fleet) if
   added unconditionally, and the energy gate fights the **battery-buffered** power model. Must go behind a
   default-off flag with the start designs fixed first.
4. **Turning on food/power demand starves the start colony on turn 1** — `earth.json` has neither a farm nor a
   power plant. Food is a per-scenario, supply-first fix; **power is blocked entirely** because no
   colony-installable power generator exists (new build **E-build-11**).
5. **The firepower-caliber fix is clean and safe** (fold into per-weapon dps, drop the redundant aggregate
   multiply — the two live branches are mutually exclusive, so no double-count).

*Phase 3 complete. Next: Phase 4 — design the genuinely-missing mechanisms (the E-build items) at the full
designer standard.*
