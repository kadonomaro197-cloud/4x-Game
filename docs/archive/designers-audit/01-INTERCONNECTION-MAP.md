> # 🗄 ARCHIVED 2026-08-03 — DO NOT FOLLOW AS THE LIVE DESIGNER SPEC
>
> **This is the earlier "Designer Interconnection Audit" (2026-08-01), moved to `docs/archive/` on 2026-08-03 in the designer-docs cleanup.** The CANONICAL component-designer source of truth is now the **12 door HTMLs** in `docs/Actual HTMLs Of designers/` + **`docs/economy/DESIGNER-NORTH-STAR.md`** (the derive-a-dial method) + the **assembler suite** in `docs/assembler/`. Read those to design; read this for history. **STILL LIVE from this audit:** its six open developer rulings **R1–R6** (`06-FINAL-REPORT.md` → "The open developer rulings") remain UNDECIDED — the cleanup did not resolve them. Locked decisions live in `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`.

# 01 — INTERCONNECTION MAP (Phase 1 deliverable)

> **What this is, in one breath:** the eleven component designers are supposed to snap together like the
> parts of one engine. This file lays every one of them on the bench, lists what each *produces* and
> what each claims to *plug into*, and then draws the wiring diagram between them — a designer×designer
> grid showing which output of one is the input of another. This is the **paper** map (what the designs
> *claim*). Phase 2 opens the engine and checks whether the wire is actually soldered on the other end.
>
> **Read the mission in `00-MISSION-AND-STATE.md` before this.** Method: `docs/economy/DESIGNER-NORTH-STAR.md`
> (a dial is real only if it writes a variable the sim reads). Engine graph: `docs/SYSTEM-CONNECTION-MAP.md`.

---

## 0. How to read this map

Every designer makes **components** (a reactor, a gun, a cargo hold, an academy). A component is a
bundle of **outputs** — named numbers or flags it writes onto the thing it's installed on. Another
designer's component **consumes** an output when its own behaviour is gated or fed by that number.

The whole point of the audit is that a produced number with nobody reading it, and a reader with
nobody producing its number, are **both broken** — that's the gotcha-10 two-ends rule. This file finds
the *pairs*; Phase 2 proves each end against engine source.

**Three verdicts per connection (this phase — paper coherence only):**

| Mark | Verdict | Means |
|------|---------|-------|
| ● | **CONNECTED** | Both designers name the *same* output/variable and the direction is coherent — one writes it, the other reads it. On paper it fits. |
| ○ | **CLAIMED-BUT-UNVERIFIED** | One side names the link but the other doesn't clearly pick it up, OR the design itself flags it as not-built-yet / needs an engine check. A soft wire. |
| · | **ABSENT** | No connection claimed between these two. |

A ● here is **not** a guarantee the engine reads it — it means the *two designs agree on paper*. The
engine truth is Phase 2's job. Some of the most important findings below are ● pairs that I already
suspect Phase 2 will knock down to a dead-end.

---

## 1. The four backbones — the many-to-many spines everything else hangs on

Before the pairwise grid, four connections are **not** one-to-one — they are hubs every designer
touches. If you understand these four, the grid is mostly detail.

### Backbone A — the **mass/budget hub** (Chassis is the frame; everyone spends against it)
The Chassis door hands out a **budget** (kilograms of mass, or carry-strength, or volume) and every
other ship/unit component **spends mass** back against it. So Chassis feeds *out* to all of them
(budget + the requirement gates — "an energy weapon demands a reactor"), and all of them feed *back in*
(their Mass lands on the hull). This is why the Chassis row and the Chassis column are both nearly full.
Named engine currency: `ShipDesign.Recalculate`, mass → credits/BP/crew. Every "→ Chassis budget" note
in the extractions is one strand of this hub.

### Backbone B — the **manpower pool** (Civic grows the people; every crewed part draws them)
Every component with a `CrewReq` — a reactor, a beam, a sensor, a cargo bay, a mine — draws from **one
shared workforce pool** (`ManpowerTools.ResolveBuild`). The **Civic** door is what fills that pool
(residency → population). So Civic feeds *out* to essentially every crewed component, but through **one
pool**, not eight private wires. When you see Civic → Power, Civic → Sensors, Civic → Logistical below,
read it as "all of them drink from the same well Civic fills." The Enhancers **crew-automation** suite
is the one thing that *reduces* a draw on this pool.

### Backbone C — the **build + tech overlay** (Industrial makes it real, research says what's allowed)
The **Industrial** door designs the *factories*. Its construction/assembly/refinery points are what
physically **build** every component the other ten doors design — that's the cradle-to-grave "built at a
colony" rung for all of them. Separately, its **research-lab** points feed the tech tree, and **tech
gates what every door is allowed to design at all**. So Industrial has two outward fans: "I build your
component" and "my research unlocks your component." Both touch all ten other doors.

### Backbone D — the **agency overlay** (Command doesn't feed a number; it seats the operator)
The **Command** door is different in kind. It mostly does **not** write a variable another component
reads. It produces a **seat** and the **leader** who sits in it, and that leader *operates* a whole
system — a fleet of ships (Weapons/Defense/Propulsion/Sensors), a battalion of ground units, a colony's
industry, a research team. So Command's outward edges are "a seated officer drives this," not "this
number flows into that." That's why the Command row is all ○ (agency), while the strong ● *into*
Command comes from **Civic** (it trains the leaders and sizes the seats) and **Sensors** (the intel
directorate).

> **Keep these four in mind reading the grid.** A cell isn't a bespoke pipe — often it's this designer's
> single strand of one of the four hubs above.

---

## 2. The eleven, in capsule — doors · key dials · named outputs · what each claims to feed

*(Compact. The full per-door extraction is the source; this is the reference card for the grid.)*

### Weapons — "two choices, four sliders"
- **Doors:** Delivery (Contact / Beam) × Nature (Kinetic / Energy).
- **Outputs:** damage-per-second, damage-per-shot, rate-of-fire, shot-speed, guidance(follows-a-dodge),
  volume-of-fire, **nature = the shield answer**, **armour-pierced**, range/reach, heat/sec,
  supply(power-or-ammo), interceptable.
- **Feeds:** the combat resolver / `CombatKernel` (reads the values); **Defense** shields (the nature
  answer) and armour (pierce cancels plate); **Chassis** budget (mass); **Power** (beam draw); **Logistical**
  (ammo); **ground resolver / `GroundDamageMatrix`**; **point-defence** (interceptable).

### Defense — "the four layers"
- **Doors:** incoming-damage nature (test input) × plate-tuning target (zero-sum, sum=4.0) × which of the
  four layers live at this door.
- **Outputs:** Shield (cap+regen), Armour (thickness + 4 nature-resists), **Structure (raw HP — *sourced
  from Chassis, not Defense*)**, **Evasion (*sourced from Chassis + Propulsion, not Defense*)**,
  EnvironmentalResistance, fortification divisor, nature-tuning factors.
- **Feeds:** the resolver/`CombatKernel` (shield-peel + armour step); `GroundForcesProcessor.cs:235`
  (environmental attrition); **Weapons** ("the mirror"); intel-load-bearing tuning read by the **Sensor**
  and **espionage** layers.

### Chassis — "the assembler's first call" (the hub, Backbone A)
- **Doors:** Environment (5 values) × operating band × Unit-vs-Infrastructure × substrate (8).
- **Outputs:** **Budget**, **Mass**, structural-efficiency (→ faction reach), the **REQUIREMENT SET it
  declares** (energy-weapon→reactor gate `WeaponSupply.PowerDraw_W`; ammo→magazine gate; crew gate),
  part-mount target, runs-on channel, **JOBS declared (infrastructure)** → `EmploymentAtbDB`, scale-word/name,
  envelope multiplier, CarrySizeOf/frame-Size.
- **Feeds:** all four assemblers' budget; **Civic** (declares JOBS — "the missing producer"); **Command**
  (Psionic seat); **Power/Logistical/Industrial** (requirement + envelope); the Entity Assembler.

### Civic — residency / academy / colony buildings (the people hub, Backbone B)
- **Doors:** the civic building JOB selector × leader-TYPE (academy only: Navy/Ground/Gov/Sci) × price-checkbox.
- **Outputs:** **`EmploymentAtbDB.Jobs`** (→ `GetTotalJobs` → `ColonyMoraleDB` — *currently ZERO producer*),
  Housing/Comfort (→ `ColonyMoraleDB.MaxComfortBonus` +20), population-support (`PopulationSupportAtbDB`),
  food (`SustenanceProcessor`), **officer graduates (→ Command roles)**, **admin capacity (→ `AdminSpaceAtb`
  seats)**, security (`LegitimacyProcessor`), health, aggregate morale, power-shortage term, tax term.
- **Feeds:** **Command** (leaders + seats — the strongest cross-door edge); the whole crew pool; morale.

### Command — leader seats / command components (the agency hub, Backbone D)
- **Doors:** Admin-Level ladder (11) × Site-Role (5) × template; proposed Scope × Domain doors.
- **Outputs:** `ColonyHexMapDB` (buildable hex map), a **command SEAT** (via `AdminSpaceAtb`),
  **SeatType/AdminLevel (*nothing enforces it*)**, site-work-rate (`SiteWorkProcessor`), site-hazard
  incident roll (the grave rung), `CommanderID`, **delegated command of a scope** → fleets / battalions /
  economy / research.
- **Feeds (as agency):** fleets, ground campaign, economy, research; **Enhancers** (doctrine switch cadence).
- **Reads:** **Civic** (leader type + admin span), **Sensors** (`IntelDirectorateAtb`).

### Enhancers — "what kind of better?"
- **Doors:** kind-of-better (3 locked sub-categories) × which-template × price-checkbox.
- **Outputs:** Firepower-Caliber & Toughness-Caliber (→ `ShipCombatValueDB`), TrainingMultiplier /
  StrengthBonus / EvasionBonus / ToughnessBonus / Shield / ShieldRegen (→ `GroundUnitAssembly`),
  **Crew-Reduction (→ manpower pool)**, **SwitchableAfter (→ `FleetDoctrine.TrySetDoctrine`)**,
  **self-repair (→ BLOCKED — no damaged state)**, sealing (env-survival gate), Mass.
- **Feeds:** **Weapons** & **Defense** (calibers), **Propulsion** (evasion bonus), **Chassis** (host + mass),
  **Command** (doctrine), the manpower pool.

### Industrial — "how much plant, and of what kind" (build+tech hub, Backbone C)
- **Doors:** industry-KIND selector × unit-factory DOMAIN (yard only) × research-specialty (lab).
- **Outputs:** minerals, refinery-points, component/installation/ordnance construction-points (→
  `IndustryAtb` rate table), **Fighter-Construction-Points (→ NOTHING — missing industry type)**,
  ship-assembly-points + size-limit (**domain-gated by chassis environment**), construction/lift,
  research-points (→ research processor + `bonusCategory`), **Cost-Per-Day (→ NOTHING — `_costPerDay`
  unread)**, fortification (→ ground map), TileFootprint, Support-Capacity (→ `InfrastructureCapacityAtb`),
  **housing/pop-support/tolerance outputs (overlap with Civic)**.
- **Feeds:** builds every buildable door; research gates every door; **Civic** (housing — contested),
  **Logistical** (support capacity), **Command** (fortification).

### Logistical — cargo / storage / transfer / people / dock
- **Doors:** HOLD-vs-MOVER × cargo-TypeID × colonist-carry-mode × cargo-CLASS taxonomy × DOCK split.
- **Outputs:** `CargoStorageAtb` per class — general (→ cargo system), **fuel (→ NewtonThrust fuel draw,
  Propulsion)**, ordnance (→ missile launcher), **ammo (→ dry-magazine gate + `GroundUnitAssembly`)**,
  **troops (→ `GroundTransport`, the invasion chain)**, **passenger/cryo (→ 🔴 NOTHING reads colonists)**,
  perishable (→ `SustenanceProcessor`); `CargoTransferAtb`; **`LogiBaseAtb` (→ 🔴 NOTHING)**; **frame-Size
  (→ 🔴 nothing today)**; `DockBayAtb` (→ `DockedShipsDB`); cargo Mass (→ hull budget); crew-req (→ pool).
- **Feeds:** **Propulsion** (fuel), **Weapons** (ammo/ordnance), **Chassis** (mass), **Command/ground**
  (troops → invasion), **Civic/population** (colonists — dead today).

### Power — "generate or collect"
- **Doors:** energy-source (Generate/Collect) × generator-kind (Reactor/RTG/turbine) × apply-RTG-law checkbox.
- **Outputs:** **TotalOutputMax kW** (→ `SustenanceProcessor:51`, warp-sustain, ground-laser supply),
  **LocalFuel gate** (→ `WarpMoveCommand:258`, `WeaponSupply`, `MilitaryReach:156`), EnergyStoreMax
  (→ warp-create gate), **Signature (→ `SensorSignatureAtb`)**, solar-band-efficiency (shares sensor band
  code), CrewReq, ResourceCost, Mass.
- **Feeds:** **Weapons** (beam/energy supply gate), **Propulsion** (warp power), **Sensors** (signature +
  shared band code), **Chassis** (mass), **Civic** (colony sustenance + crew), **ground** (laser supply).

### Propulsion — "one door for everything that moves"
- **Doors:** drive-kind × FTL route × FTL transit × FTL trackability × navigator-gated checkbox.
- **Outputs:** **Thrust÷mass (→ `CalculateEvasion` — the evasion Defense claims)**, **Thrust-as-signature
  (→ `SensorSignatureAtb`)**, Thrust (→ `FleetManeuver`), Δv (→ `DeltaVFloor`/`ManeuverBudget`),
  warp-speed (→ `WarpSpeedFloor`), bubble-creation (→ `WarpMoveCommand`), bubble-sustain (→ Power),
  **speed-factor (→ ground march)**, rough-handling, amphibious (→ `HexPathfinder`), **fuel-burn
  (→ Logistics)**, drive-mass (→ Chassis), drive-destroyed (→ Damage), **drive-heat (→ GAP, NOT emitted)**.
- **Feeds:** **Defense** (evasion), **Sensors** (signature), **Logistical** (fuel burn), **Chassis** (mass),
  **Power** (warp sustain), **ground/Command** (march + navigator seat).

### Sensors — listen / look / track / hide
- **Doors:** the forced job selector (Listen/Look/Track/Hide) × per-family dials.
- **Outputs:** Window/band (→ **the band-matching gate** in `SensorTools.cs`), sensitivity/threshold,
  **detection-range (→ fog-of-war + combat trigger)**, coverage×range², **track-range (→ fire control →
  Weapons)**, fire-control-mass (→ Chassis, the phantom-dial exploit), signature/Hide (→ enemy sensor),
  Blind (→ enemy threshold + resolver), Mass (→ Chassis), scan-interval, **`IntelDirectorateAtb`
  (→ Command door)**.
- **Feeds:** **Weapons** (fire control), **Command** (intel directorate), **Chassis** (mass), **Defense**
  (intel→tuning), enemy detection.

---

## 3. THE MATRIX — who produces (row) → who consumes (column)

Read a cell as: *does the ROW designer's output feed the COLUMN designer's input or gate?*
`●` CONNECTED · `○` CLAIMED-BUT-UNVERIFIED · `·` ABSENT · `—` self.

```
   consumer→   Wpn  Def  Cha  Civ  Cmd  Enh  Ind  Log  Pow  Pro  Sen
producer↓
Weapons  Wpn     —    ●    ●    ·    ○    ·    ·    ●    ●    ·    ·
Defense  Def     ●    —    ●    ○    ○    ·    ·    ·    ·    ·    ·
Chassis  Cha     ●    ●    —    ○    ○    ●    ○    ●    ●    ●    ●
Civic    Civ     ●    ○    ●    —    ●    ·    ●    ●    ●    ●    ●
Command  Cmd     ○    ○    ·    ·    —    ○    ○    ○    ○    ○    ○
Enhancer Enh     ●    ●    ●    ○    ○    —    ·    ·    ·    ○    ○
Industri Ind     ●    ●    ●    ○    ○    ●    —    ○    ●    ●    ●
Logistic Log     ●    ·    ●    ○    ●    ·    ·    —    ○    ●    ·
Power    Pow     ●    ·    ●    ●    ○    ·    ·    ·    —    ●    ●
Propulsi Pro     ○    ●    ●    ·    ○    ·    ·    ●    ●    —    ●
Sensors  Sen     ●    ○    ●    ·    ●    ·    ·    ·    ·    ·    —
```

**Row/column shape tells the story:**
- **Chassis, Civic, Industrial** rows are the fullest — the three hubs (mass, people, build/tech) fan out
  to nearly everyone. **Command** row is all-○ — it's the agency overlay, it seats operators, it doesn't
  pipe numbers.
- The **Chassis column** is the fullest column: everything's Mass lands back on the frame. Second-fullest
  column is **Weapons** — it's the sink the whole ship is built to point (Power, Logistical, Sensors,
  Enhancers, Defense, Propulsion all feed the gun).
- **Defense** and **Sensors** rows are sparse *producing* — they mostly *receive* (evasion from Propulsion,
  structure from Chassis; track from own sensors into weapons). Defense in particular **outsources two of
  its own four layers** (Structure, Evasion) — a boundary crack, §5-C9.

---

## 4. The directed edge ledger — every non-absent cell, with the shared variable

Grouped by producer. Each row: the output that flows, the consumer's read-point (as *claimed* — Phase 2
verifies), and the paper verdict.

### From Weapons
| → | Output that flows | Consumer reads it as | Verdict |
|---|---|---|---|
| Def | nature (shield answer) + armour-pierced | resolver shield/armour step — "the mirror" | ● |
| Cha | weapon Mass | hull mass budget (`ShipDesign.Recalculate`) | ● |
| Log | ammo/ordnance consumption | dry-magazine gate + `CargoStorageAtb('ammo')` | ● |
| Pow | beam power-draw | `WeaponSupply` energy gate | ● |
| Cmd | ground-weapon damage | `GroundDamageMatrix` → battalion a seat drives | ○ (agency-ground) |

### From Defense
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn | shield/armour to defeat | resolver — "the mirror" | ● |
| Cha | armour Mass | hull budget | ● |
| Civ | (shield crew, if any) | manpower pool | ○ |
| Cmd | ground-unit toughness + `EnvironmentalResistance` | `GroundForcesProcessor.cs:235` attrition (ground unit a seat drives) | ○ (agency-ground) |

### From Chassis (Backbone A out)
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn | mount budget + requirement | weapon design's mass allowance | ● |
| Def | **Structure (raw HP)** | Defense's own layer-4 (sourced here, §5-C9) | ● |
| Enh | host frame + budget | enhancer attaches to a host | ● |
| Log | envelope volume (+ frame-Size ○) | cargo/fuel hold volume | ● |
| Pow | space + reactor requirement channel | reactor gate | ● |
| Pro | mass-to-move + drive-mass budget | acceleration = thrust÷mass | ● |
| Sen | mount budget | sensor mass allowance | ● |
| Civ | **JOBS declared** (`EmploymentAtbDB`) | contested producer for morale employment term (§5-C1) | ○ |
| Cmd | Psionic-substrate seat | command seat requirement | ○ |
| Ind | environment tags | Unit-Assembly DOMAIN gate ("which environment it can build") | ○ |

### From Civic (Backbone B out)
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Cmd | **officer graduates + admin capacity** | Command roles + `AdminSpaceAtb` seats | ● (strongest cross-door edge) |
| Cha | population → crew | crew gate (via manpower pool) | ● |
| Ind | population → labor | industry crew (1/point, via pool) | ● |
| Pow | population → crew | reactor/turbine crew (via pool) | ● |
| Pro | population → crew | drive crew (via pool) | ● |
| Sen | population → crew | sensor crew (via pool) | ● |
| Log | population → crew | cargo/bay crew (via pool) | ● |
| Wpn | population → crew | beam crew (via pool) | ● |
| Def | (crew, if shields) | pool | ○ |

### From Command (Backbone D — agency, all ○)
| → | What it drives | Consumer | Verdict |
|---|---|---|---|
| Wpn | fleet fire-control ownership | seated commander operates the fleet's guns | ○ |
| Def | fleet/ground survivability decisions | seated commander | ○ |
| Enh | doctrine switch cadence | `FleetDoctrine.TrySetDoctrine` (seat sets it, enhancer gates speed) | ○ |
| Ind | delegated economy (Yard Master / Governor) | industry run by a seated officer | ○ |
| Log | delegated logistics | `SetLogisticsOrder` / `LogiBaseDB` | ○ |
| Pow | ground-laser fire decision | seated ground commander | ○ |
| Pro | navigator seat + march order | `CommandBerthAtb` / march driven by a seat | ○ |
| Sen | EMCON posture order | active/dark set by a commander | ○ |

### From Enhancers
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn | Firepower Caliber | `ShipCombatValueDB` firepower mult | ● |
| Def | Toughness Caliber + Shield/Toughness/Evasion bonuses | `ShipCombatValueDB` / `GroundUnitAssembly` | ● |
| Cha | enhancer Mass + Crew-Reduction | host budget + hull crew | ● |
| Pro | EvasionBonus | evasion stat (Propulsion-sourced) | ○ |
| Civ | Crew-Reduction | *relieves* the manpower pool draw | ○ |
| Cmd | SwitchableAfter | doctrine cadence a commander uses | ○ |
| Sen | "Foresight/react-before-react" | detection/reaction — **no reaction variable exists** (§5-C14) | ○ |

### From Industrial (Backbone C out — build + tech overlay)
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn/Def/Enh/Pow/Pro/Cha | construction/assembly points | **builds** the component + research gates it | ● |
| Sen | construction points + research | builds/gates sensors | ● |
| Civ | housing/pop-support/tolerance | **overlaps Civic's own outputs** (§5-C10) | ○ |
| Log | Support-Capacity | `InfrastructureCapacityAtb` / `LogiBaseAtb.LogisticCapacity` | ○ |
| Cmd | fortification (LocalFortify + AdjacentProjection) | ground map (region a battalion holds) | ○ |

### From Logistical
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Pro | `CargoStorageAtb('fuel-storage')` | NewtonThrust fuel draw | ● |
| Wpn | ordnance + ammo storage | missile launcher + dry-magazine gate | ● |
| Cha | cargo Mass | hull budget | ● |
| Cmd | `CargoStorageAtb('troops')` | `GroundTransport` invasion chain | ● (load-bearing for "take a planet") |
| Pow | fuel/resource storage | reactor fissile-fuel draw | ○ |
| Civ | passenger/cryo colonists | `ColonyInfoDB.Population` — **🔴 nothing reads it** (§5-C3) | ○ |

### From Power
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn | TotalOutput + LocalFuel | `WeaponSupply` energy gate | ● |
| Pro | warp-sustain kW + EnergyStoreMax + LocalFuel | `WarpMoveCommand:258` warp gate | ● |
| Sen | Signature + shared solar/receiver band code | `SensorSignatureAtb` | ● |
| Cha | Mass + ResourceCost | hull budget | ● |
| Civ | TotalOutput + CrewReq | `SustenanceProcessor:51` power-shortage morale + crew | ● |
| Cmd | ground-laser supply (`GROUND_LASER_W`) | ground energy-weapon fire | ○ |

### From Propulsion
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Def | **Thrust÷mass → evasion** | `CalculateEvasion` — "best defence bought here, not at Defense" | ● |
| Sen | Thrust-as-signature | `SensorSignatureAtb` (3500 K) | ● |
| Log | fuel-burn | tankers/refuelling demand | ● |
| Cha | drive-mass | hull budget | ● |
| Pow | bubble-sustain demand | Power charged per second in transit | ● |
| Wpn | Thrust → `FleetManeuver` → range | engagement range weapons fight at | ○ |
| Cmd | speed-factor + navigator | ground march + `CommandBerthAtb` seat | ○ |

### From Sensors
| → | Output | Consumer | Verdict |
|---|---|---|---|
| Wpn | track-range + tracking-speed | fire control → "the weapon that shoots" | ● |
| Cha | fire-control-mass + sensor Mass | hull budget (phantom-dial exploit flagged) | ● |
| Cmd | `IntelDirectorateAtb` | Command door (officer + post + capacity) — both sides name it | ● |
| Def | detection/intel | informs armour-tuning decision (soft input) | ○ |

---

## 5. Early cracks — the connections that already look broken on paper

These are the pairs where the wiring diagram already shows a gap, a short, or a fight over who owns a
terminal. I'm flagging them now; **Phase 2 will give each a sourced engine verdict** (the tag in
brackets is the verdict I expect Phase 2 to confirm). Every one of these feeds Phase 3 (a correction)
or Phase 4 (a missing design).

**C1 — `EmploymentAtbDB.Jobs`: a reader with a contested/absent producer.** [MISSING/CONTESTED-PRODUCER]
The consumer is real and wired: `GetTotalJobs` → `ColonyMoraleDB` employment term. But the extraction
says the producer is **ZERO today** ("`employmentRatio` permanently ..."). And **three** doors gesture at
producing it — Chassis ("infrastructure declares JOBS"), Civic (the user's ruling: "jobs come from the
colony's industry buildings"), and Industrial (industry buildings). Nobody clearly *is* the producer.
This is the single most important Phase-1 crack: a morale input the game already reads, that nothing fills.

**C2 — `LogiBaseAtb`: producer with zero readers.** [DEAD-END] Cited by Logistical, Civic, *and*
Industrial as a mirror/example, and every one of them notes it has **zero readers outside its own file**.
A produced capacity number nobody consumes.

**C3 — passenger / cryo storage → colonists: reader absent.** [MISSING-CONSUMER] `CargoStorageAtb('passenger-storage')`
is produced, but **🔴 nothing reads colonists today**; the proposed consumer (unload → `ColonyInfoDB.Population`)
isn't wired. Breaks any "ship colonists to a new world" flow.

**C4 — research-lab `Cost Per Day` → nobody.** [DEAD-END] `ResearchPointsAtbDB` stores `_costPerDay` with
**no reader**. A dial that costs the player nothing because nothing consults it — a North-Star violation
(a dial that writes no variable the sim reads).

**C5 — Fighter Construction Points → nobody.** [MISSING-CONSUMER] The factory can output fighter-construction
points, but there is **no `fighter-construction` industry type** in the `IndustryAtb` rate table (the five
that exist: refining, component-, installation-, ordnance-construction, ship-assembly). The output has no
matching build channel.

**C6 — drive-heat: producer absent.** [MISSING-PRODUCER] Weapons emit heat into a fleet heat pool;
Propulsion drives **do not emit drive-heat at all** (flagged GAP). An asymmetry — the heat/EMCON penalty
sees your guns but not your engines.

**C7 — the sensor band-matching gate is written wrong.** [LOGIC-MISMATCH] As-written:
`max(recvMin,sigMin) < max(sigMin,sigMax)`. Correct overlap test:
`max(recvMin,sigMin) < min(recvMax,sigMax)`. **Every** detection connection — fog-of-war, fire-control,
signature, who-shoots-first — rides on this one comparison, and it's the wrong bound. High blast radius.

**C8 — command-seat `SeatType`/`AdminLevel`: producer with no enforcing reader.** [DEAD-END] The seat
carries a scope/altitude label, but per the extraction "**nothing enforces it — consulted by no rule;
only displayed**." The whole span-of-control idea is a label no rule reads.

**C9 — Defense outsources two of its own four layers.** [NEEDS-CHANGE — door boundary] Defense's extraction
says **Structure** comes from Chassis and **Evasion** comes from Chassis + Propulsion, *not* Defense. So
the "four layers" door only truly owns two (Shield, Armour). Not a dead wire — a **boundary dispute**: who
is the door of record for evasion and structure? Must be resolved so two designers don't both claim to set
the same number (or neither does).

**C10 — Industrial and Civic both claim housing / population-support.** [DUPLICATE-PRODUCER] Industrial's
outputs include "housing / population-support / tolerance → the Civic door," and Civic produces exactly
those (`MaxComfortBonus`, `PopulationSupportAtbDB`). Two doors producing the same colony outputs — a
boundary that must be drawn or they'll fight (or double-count).

**C11 — Chassis frame-Size → `GroundTransport.CarrySizeOf`: dead dial.** [MISSING-CONSUMER] Chassis (and
Logistical) document a frame-Size dial that "feeds transport carry size," but **`CarrySizeOf` is hard-coded
today** and reads nothing. The one number that should decide "how big a unit fits in this transport" is
ignored — directly threatens the invasion/transport chain.

**C12 — generic component power-draw is missing.** [MISSING-MECHANISM] Only weapons and warp drives draw
power. Logistical flags that "a generic component POWER DRAW is missing" — active sensors, and anything
else that should cost power, have no draw wire. Power's supply gate can't gate consumers that don't
declare a draw.

**C13 — Enhancers self-repair: blocked by the engine model.** [BLOCKED] Self-repair wants a
damaged/degraded state to heal; the engine is **whole-or-dead** (no partial-damage state exists). The
output has nowhere to write.

**C14 — Enhancers "Foresight / react-before-react": no variable exists.** [MISSING-VARIABLE] Combat is
**simultaneous** — there is no reaction/initiative variable (`AutoResolve.cs`, `GroundForcesProcessor`).
An enhancer that buys "act first" has no field to write.

**C15 — shields appear to draw no power.** [POSSIBLE MISSING-CONNECTION] Power → Defense is ABSENT in the
grid: no extraction claims shields draw power. Either shields are deliberately passive, or this is a
missing Power→Defense wire. Phase 2 checks whether `Shield` regen has any power dependency.

---

## 6. What Phase 2 must verify (the adversarial target list)

Phase 2 opens `GameEngine/...` and, for every ● and ○ above, answers with file:line: **does the engine
actually read this output, and does the *named* consumer exist?** Priority targets — the connections
whose failure would break the most:

1. **Backbone integrity** — does `ShipDesign.Recalculate` actually read every component's Mass (Backbone A)?
   Does `ManpowerTools.ResolveBuild` actually pool crew from Civic population (Backbone B)? Does the
   `IndustryAtb` rate table actually build all ten doors' components (Backbone C)? Does a seat actually
   drive a fleet/battalion/colony (Backbone D)?
2. **The C1 producer hunt** — trace `EmploymentAtbDB.Jobs`: is there *any* code path that writes it, or is
   the whole employment-morale loop open?
3. **The C7 band gate** — read `SensorTools.cs:125` and confirm the comparison; it gates every detection ●.
4. **The dead-ends (C2, C4, C8)** — grep for readers of `LogiBaseAtb`, `_costPerDay`, `SeatType/AdminLevel`;
   confirm zero.
5. **The transport chain (C3, C11)** — confirm `CarrySizeOf` is hard-coded and colonist-unload is unwired;
   these two together decide whether "take a planet" and "settle a planet" can be built at all.
6. **The boundary disputes (C9, C10)** — confirm from source who actually writes Evasion, Structure, and
   colony housing, so Phase 3 can assign each a single door of record.

For each claimed connection I will spawn a **skeptic agent** whose only job is to *refute* it from
source — a connection survives only if the skeptic can't kill it.

---

*Phase 1 complete. Next: Phase 2 — Output Readability Audit (`02-OUTPUT-READABILITY-AUDIT.md`), engine
evidence for every edge above, adversarially verified.*
