# 02 — THE MASTER I/O MATRIX (the wiring reference for the in-game build)

> **What this is, in plain English.** The twelve door-designers each stamp out **components** — a gun, a
> reactor, a cargo hold, a suit of power armour. The combat resolvers and the economy don't read components;
> they read **totals** — one Firepower number, one Toughness number, one Evasion number for the whole ship or
> squad. The **Entity Assembler** is the machine in the middle that turns a pile of designed components into
> those totals. **This file is the wiring diagram for that machine:** every number a door produces, every wire
> that runs from one door to another, and exactly what the Assembler has to read versus what it has to compute
> itself. When we build the Assembler in the game, this is the checklist that says "hook this to that."
>
> Built from the twelve verified census records (`01-IO-<door>.md`, Phase 2 pass), the resolver's own input
> surface (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §6.1), and the interconnection map's edge ledger
> (`docs/archive/designers-audit/01-INTERCONNECTION-MAP.md` §4). Every row traces to a cited census.

## How to read the build-state column (used in all three tables)

| State | Means |
|-------|-------|
| **live** | The number reaches the running simulation today — a real engine variable a processor reads. |
| **read** | Set/owned by a *different* door; this door only displays it. A wire the Assembler must carry, not a value it makes. |
| **emergent** | Computed from the *whole assembled entity* (or the battlefield). Never a dial — the Assembler's job to compute. |
| **pending** | No engine variable exists yet. The dial is designed; the thing it writes has to be built first. The row says what it waits on. |

> ⚠ **One honesty note carried from the censuses:** several doors are *re-derivations* — the interactive panel
> shows a **proposed** system next to what ships today (Chassis, Civic, Industrial, Power, and the whole
> Command builder and Weapons door). Where a door is a proposal, the *variable it writes* is usually already
> real (that's what makes it a valid dial); it's the **pricing/derivation** that's pending. Rows below mark the
> variable's state, and note "(proposed door)" where the designer itself is not yet the shipped one.

---

## TABLE A — every sim-reaching variable in the designer surface

One row per variable the simulation reads (plus the load-bearing pending ones). **This is the master wiring
list**: variable → which door owns it → the control that sets it → range/default → the engine consumer →
build-state. Grouped by owning door.

> **⚠ Reconciled to the reader-verified ledger (`06-OUTPUTS-BY-DOOR.md`) on 2026-08-06.** The `State` and
> `Consumer` cells below now carry `06`'s forensic per-door findings — the **13 mislabels** its reader-end trace
> found are folded into the rows (no longer only summarized in this banner), and each pending/dead row carries a
> **`backlog #N`** pointer into `ENGINE-WIRING-BACKLOG-2026-08-06.md`. `06` remains the **file:line reader ledger**
> (every state cited to its consumer); this table is the wiring view. The engine (C#) has not changed since `06`
> (2026-08-02), so these states are current. The load-bearing corrections now in the rows:
> **Enhancers iface `SwitchableAfter`** is DEAD (no component writes the cut), not "live"; **Defense shield/regen**
> drain **inline in the ground resolver**, not `CombatKernel.ResolveShield`; **Logistical ammo/troops** feed
> dedicated `ShipMagazineAtb`/`GroundBayAtb`, the typed cargo stores **UNREAD**; **`LogiBaseAtb`** is engine-dead
> but **client-live**; **Chassis Structure→HP and Weapons penetration/per-shot/rate** are **ground-only** — inert
> on a ship; **Command Office-radius** is a dead-ending write (client display only).

### A1 · Weapons  *(proposed door; the ten variables ARE the resolver's `WeaponProfile` — `01-IO-weapons.md`)*
| Variable (engine name) | Control that sets it | Range / default | Consumer | State |
|---|---|---|---|---|
| `DamagePerSecond` | Total-damage slider | 200 – ~745,600 J/s · def scale 45 | `CombatKernel` firepower + fire-mix; **Σ = ship Firepower** | live |
| `DamagePerShot` | Shot-size↔rate slider | 10^((shot−50)/26) of dps · def shot 35 | `ArmourSoakBurst` `CombatKernel.cs:331` **via ground resolver** `GroundForcesProcessor.cs:435`; **ship zeroes it** `CombatEngagement.cs:1368` | **live (ground-only)** |
| `Rate` | Shot-size↔rate slider (coupled) | dps/perShot · def shot 35 | folded coupling `BurstShotCount = dps/PerShotEnergy` `CombatKernel.cs:318` (no standalone field) | **live (ground-only)** |
| `VolumeOfFire` | Focus slider | `max(1, rate*(0.4+3.6*spread))` · def foc 50 | `CombatKernel` saturation floor (flak vs dodge) | live |
| `Range_m` | Range slider (per family band) | family band × 1000 m · def rng 40 · **0 = unbounded** | `BuildFireMix` gate + range term | live |
| `Penetration` | Focus slider | `foc*0.4|0.9|0.6` · def foc 50 | `ArmourSoak` `CombatKernel.cs:294` **via ground resolver** `GroundForcesProcessor.cs:437`; **ship zeroes it** `CombatEngagement.cs:1368`. **Carrying it through the assembler = backlog #1** | **live (ground-only)** |
| `Velocity` (shot speed) | Delivery chip (forced) | Beam 3e8 / Proj 2e4 / Guided 1.2e3 m/s | `HitFraction` velocityTerm (you cannot dodge light) | live |
| `Tracking` (dodge-following) | Delivery chip (forced) | Proj 0.05 / Guided 0.90 | `HitFraction` trackingEff | live |
| `Nature` soak fraction | Nature chip (forced) | K 1.0 / E 0.5 / X 0.75 / O 0.0 | `ShieldSoakFraction` — the shield matchup (**attacker-fixed**) | live |
| PD-answerable flag | Delivery chip (forced) | Guided = yes | point-defence resolver | live |
| runs-on (power/ammo/none) | Delivery chip (forced) | Beam power / Proj+Guided ammo | ammo derived from **Nature** `CombatEngagement.cs:1402` → drain `:699`; power/none have **no reader** (keyed on the wrong axis — corr #6) | **partial** |
| `HeatPerSecond` (door doesn't emit it) | *un-emitted output* | — | **live reader** `WeaponProfile.cs:126` → heat throttle `CombatEngagement.cs:714` | **live reader, output not emitted** (corr #11) |

### A2 · Defense  *(`01-IO-defense.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| Shield pool (`ShieldCapacity`) | Capacity slider | 0 – 1500 pts · def 10→150 | **inline drain** `GroundForcesProcessor.cs:426` (`t.CurrentShield -= absorbed`) — **NOT** `CombatKernel.ResolveShield` (that's a separate ship `ShieldAtb`) | **live (ground-inline)** |
| `ShieldRegenFraction` | Regen slider | 0 – 5.0 /s · def 0 | **inline regen** `GroundForcesProcessor.cs:391` (fraction/hr) — not the kernel's Jps arg | **live (ground-inline)** |
| Armour `Defense` (thickness) | Plate-thickness slider | 0 – 50 · def 10→5 | `CombatKernel.ArmourSoak` (`×1.5`/pt, floor 10%) | live |
| 4 nature resists (`vk/ve/vx/vo`) | Four resist sliders (zero-sum → **4.0**) | each renorm to sum 4.0 · def 25 each | `ArmourResistFor` `GroundForcesProcessor.cs:436` → `CombatKernel` natureFactor (**renorm-to-4.0 is door-only; the engine does not renorm**) | live |
| Evasion mult | — | *read from Chassis+Propulsion* | `CalculateEvasion` | **read** |
| Structure (HP) | — | *read from Chassis* | `CombatKernel` (raw HP) | **read** |

### A3 · Chassis  *(re-derivation; `01-IO-chassis.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `Mass` (base) | Frame-size slider | today a **template constant** · def d1 18 | the 4 assemblers → `ShipDesign.Recalculate` etc. | live (constant today) |
| Budget (MassBudget/BaseStrength/Structural/Footprint) | Frame-size + kind | free dial today; derived `eff·√size` proposed | the 4 assemblers (`hullBudget += MassBudget*count`); **soft gate — `EnforceMassBudget` defaults false** `ShipDesign.cs:54` | live (free dial); √-law **pending (backlog #7)** |
| Requirement gate: reactor-for-energy-weapon | env×kind×substrate declares it | green in surface-unit cell only | `WeaponSupply.PowerDraw_W` / `ReactorOutput_W` | live (1 cell) / **pending** (other cells) |
| Requirement gate: ammo-magazine | " | " | `MagazineCapacity_kg` / `DrawsAmmo` | live (1 cell) / **pending** |
| Requirement gate: crew / seated-leader | " | " | `ManpowerTools` / `CommandBerthAtb.GetWorkingBerth` | mixed |
| Requirement gate: env-seal / feed / fuel | " | " | `EnvironmentalResistance` / `FoodProductionAtbDB` / `IsFuelStarved` | mixed |
| structural efficiency | Efficiency slider (`K_SHIP·10^(d2·0.06)`) | 1128 – 1.1e9 · def d2 0 | — (`K_SHIP` grep = **zero hits**; `research=0` on every chassis → **writes nothing**) | **dead-end / pending** (no field — **backlog #7**) |

### A4 · Civic  *(re-derivation; `01-IO-civic.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| capacity/output (food, housing, beds, cadets, span…) | Dial 1 (per job) | per-job `lo..hi` · def per STOCK | `SustenanceProcessor` / `PopulationSupportAtbDB` / `NavalAcademyAtb` / `AdminSpaceAtb` | live |
| `CrewReq` | crew formula (per job) | `Max(1, v·k·(1−auto))` | `InfrastructureProcessor.cs:95` (×CapacityPerCrew → infra demand) | live |
| comfort | Dial 2 (residency/rec/habitat) | 0 – 50 · **capped at `MaxComfortBonus` 20** | `ColonyMoraleDB.ComputeMorale` (`GetHousingComfort`) | live |
| `EmploymentAtbDB.Jobs` | *proposed: publish CrewReq as Jobs* | — | `GetTotalJobs` `:22` → `PopulationProcessor.cs:74` → `ColonyMoraleDB` employment (±40) | **pending — consumer LIVE, NO producer (crack C1 · backlog #2)**; ratio pinned −1.0 → term **0 forever** |
| medical health / security legitimacy | Dial 2 (medical/security) | — | `LegitimacyProcessor` (not in the 6-input morale model) | **pending** |

### A5 · Command  *(interactive builder = PROPOSAL; `01-IO-command.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| leader-death roll | Survivability (as-built `CommandBerthAtb.Survivability`) | 0 – 95 (capped) · def 20 | `SiteHazard` incident roll (kills the seated officer) | **live** (the one live wire) |
| site work rate | `CommandBerthAtb.Grade` | 1 – 10 · def 4 | `SiteWorkProcessor` (×10 at grade 10) | live (as-built) |
| buildable hex radius | admin-complex `Office Space` | 10 – 10,000 (`Mass = ×100`) | `ColonyHexMapProcessor.cs:67` → `UpdateMaxRadius`; formula `ceil(sqrt(officeSpace/100))` — but `MaxRadius` read **only by a client display**, on the save-unsafe `ColonyHexMapDB` (L12) | **live-write / dead-ending value** |
| `AdminLevel` (seat scope) | Admin-Level ladder (11 values) | — | **read by no rule** (printed only) | **dead** (crack C8) |
| `Console Space` (bridge) | ship-command dial | 1 – 20 | consumer gated on `ColonyInfoDB` a ship lacks → nothing | **dead** |
| the whole Scope×Domain proposal | proposal builder (Grade/Support/Survivability) | — | the **reorg/framing** is unbuilt — but the Grade/Support/Survivability **dials ARE built & live** (rows above) | **pending (framing only)** (corr #9) |
> Grid: **27 filled roles / 5 justified blanks / 32 cells** (corrected in Phase 2; census originally said 21/11).

### A6 · Enhancers  *(`01-IO-enhancers.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `Firepower Caliber` / `Toughness Caliber` | caliber capability | 1 – 2 · def 1.3 | `ShipCombatValueDB` firepower/toughness **multiplier** | live |
| `TrainingMultiplier` | training capability | 1 – 2 · def 1.2 | `GroundUnitAssembly` | live |
| `StrengthBonus` | carry (power armour) | 0 – 3000 · def 300 | `GroundUnitAssembly` (**adds to carry budget** — bootstrap) | live |
| `EvasionBonus` | dodge (reflex) | 0 – 1 · def 0.4 | `GroundAugmentAtb` evasion | live |
| `Shield` (+ regen) | shield (ward) | 0 – 1500 · def 150 | `GroundAugmentAtb` (ADD across parts) | live |
| `Crew Reduction` | crew (automation) | 0 – 200 · def 30 | manpower pool (buys back people) | live |
| `SwitchableAfter` | iface | 0 – 0.75 cut · def 0.4 · floor 20 s | cooldown machinery is live, but `SwitchableAfter` is set only from a blueprint's fixed `CooldownSeconds` — **no `InterfaceAtb` writes a cut** (grep zero) | **dead-end** (matrix said "live"; corr #1) |
| self-repair rate | fieldrep | 0 – 1 · def 0.3 | — (whole-or-dead: no damaged state) | **pending** |
| morale / fury | fury | 0 – 1 · def 0.6 | — (no morale field) | **pending** |

### A7 · Industrial  *(re-derivation; `01-IO-industrial.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| industry rate | primary dial (per plant) | per-template | `IndustryAtb` rate table (`refining`/`component-`/`installation-`/`ordnance-construction`/`ship-assembly`) | live |
| `ResearchPoints` | lab primary dial | 1 – 100 · def 10 | research processor + `bonusCategory` | live |
| `Cost Per Day` | lab (prose dial) | — | **`ResearcherDB.CostPerDay`** (`ResearchPointsAtbDB.cs:71`) | **live** *(HTML calls it dead — corrected)* |
| mineral output | mine/automine dial | per-template | mining processor | live |
| `CrewReq` | crew formula | per-template | **infra-demand** `InfrastructureProcessor.SumRequiredCapacity:95` — **NOT** the `ManpowerTools` pool (that's ship-only) | live (corr #12) |
| Support Capacity | infrastructure | — | `InfrastructureCapacityAtb` (**Industrial's, not Civic's**) | live |
| fortification (LocalFortify/AdjacentProjection) | bunker dials | 0 – 1 | ground map | live |
| Fighter Construction Points | factory dial | 0 – 1000 | — (no `fighter-construction` industry type) | **dead (crack C5)** |

### A8 · Logistical  *(`01-IO-logistical.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `CargoStorageAtb.maxVolume` | Size slider | per-type `lo..hi` · def 35 | ship/colony cargo store | live |
| `CargoStorageAtb.storeTypeID` (`CargoTypeID`) | Cargo-type chip | 7 types | **selects the downstream consumer** | live |
| `CargoTransferAtb.rate` / `.range` | Rate↔Range slider | rvr 1–9 · def 12 | transfer / logistics orders | live |
| — fuel-storage | cargo-type = fuel | — | `NewtonThrust` fuel draw (Propulsion) | live |
| — ammo | cargo-type = ammo | — | ⚠ the gate reads a dedicated `ShipMagazineAtb.Capacity_kg`; the typed **"ammo" cargo store has zero readers** | **misattributed — store UNREAD** (corr #3) |
| — troops | cargo-type = troops | — | ⚠ `GroundTransport.cs:40` reads a dedicated `GroundBayAtb`; the **"troops" store is never read** | **misattributed — store UNREAD** (corr #4) |
| — ordnance | cargo-type = ordnance | — | missile launcher | live |
| — passenger/cryo (colonists) | cargo-type = people | — | `ColonyInfoDB.Population` (**no additive path**) | **pending** |
| — strikecraft-bay (berth) | cargo-type = strike | — | **no** strikecraft-bay provider/reader ("store-live" was overstated) · launch event `ParasiteLauncherReady` `EventTypes.cs:400` **never fired** | **pending store / dead-end launch (backlog #9)** |
| `LogiBaseAtb.LogisticCapacity` | logistics-office (no chip) | 5 – 100 | zero **engine** readers, but a **live client gate** `ColonyLogisticsDisplay.cs:215` (caps listable items) | **engine-dead / client-live** (corr #5) |

### A9 · Power  *(re-derivation; `01-IO-power.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `TotalOutputMax` (`PowerOutputMax`) | Mass / panel-area | reactor `50·mass·ove` | warp, ground lasers, `SustenanceProcessor:51` | live |
| `Lifetime` → `LocalFuel` | Output↔endurance (d2) | ~876 – 87,600 h | `EnergyGenProcessor` burn + `EnableFuelExhaustion` gate | live (gate flag default off) |
| `EnergyStoreMax` | battery mass | `mass·500·T_BATT` | `EnergyStoreAtb.cs:37` → caps `EnergyStored` → warp departure buffer | **live (indirect)** — ⚠ defect: reactor adds **kW into a kJ store** (`EnergyGenerationAtb.cs:65`) |
| `SensorSignatureAtb` (1700 K) | out×mass | — | detection / who-shoots-first | live |
| `CrewReq`, `ResourceCost` (fissile) | mass-derived | — | manpower / industry | live |
| reactor RTG-law (`output×lifetime=const`) | `fix` checkbox | — | — (reactor output still linear in mass) | **pending (slice P1)** |

### A10 · Propulsion  *(`01-IO-propulsion.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `NewtonionThrustAtb` (thrust) | size + split | `thrust=2·power/ve` | `CalculateEvasion`, `SensorSignatureAtb`, `FleetManeuver` | live |
| `NewtonionThrustAtb.ExhaustVelocity` | split | `F.ev·(0.55+split·0.009)` | Δv / propulsion math | live |
| `GroundLocomotionAtb.SpeedFactor` | speed (surface) | `L.speed·(1.6−split·0.011)` | `GroundMobility.SpeedMultForUnit:54` → `Speed_kmh` march (**REPLACES the frame mode instead of scaling it — backlog #5**) | live |
| `GroundLocomotionAtb.RoughHandling` | rough (surface) | 0.05 – 0.98 | march time + combat multiplier | live |
| `SensorSignatureAtb` (3500 K = Thrust) | thrust | — | detection / Sensors | live |
| warp create / sustain | FTL box 1 (k) | `power·0.5·k` / `power·0.001/k` | `WarpMoveCommand` (departure/sustain) | live (box 1 only) |
| acceleration · Δv · cruise speed · **evasion mult** | — | need finished hull mass | `CalculateEvasion` / `DeltaVFloor` / `WarpSpeedFloor` | **emergent** |
| signature suppression / quietness / navigator | supp / quiet / nav | — | — | **pending** |

### A11 · Sensors  *(`01-IO-sensors.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| `SensorReceiverAtb` (band min/max) | band centre + bandwidth | peak ± bw/2 · def d2 68 / d3 50 | detection band-match gate `SensorTools.cs:147` — ⚠ **C7 bug: upper edge never consulted (backlog #4)** | live |
| sensor threshold_kW (sensitivity) | antenna size + bandwidth | `T_SENS/(effSize²·eff)` · def d1 0 | `SensorTools.RangeForSignal` | live |
| fire-control range | Track d1 | 10 – 175 km · def 20 | `ShipCombatValueDB.cs:326` → beam MaxRange | **live (gated `EnableFireControlRange`, default off)** |
| fire-control tracking speed | Track d3 | 1250 – 25000 km/s · def 5000 | `ShipCombatValueDB.cs:310` → beam Tracking | **live (gated `EnableFireControlTracking`)** |
| cloak signature multiplier | Hide d1 | 1.0 – 0.05 · def 0.2 | detection (source-reducer) | live |
| jammer barrage strength | Blind d1 | 1 – 16 · def 4 | `JammingDivisorAgainst:96` → detection | **live (gated `EnableJamming`)** |
| jammer reach | Blind d3 | 0.1 – 5 Gm · def 1 | `JammingDivisorAgainst:96` hard cutoff | **live (gated `EnableJamming`)** |
| survey speed | Look d1 | 1 – 10 · def 1 | survey processor | live |
| jammer self-noise coupling | Blind (proposed) | — | detection self-signature | **pending** |

### A12 · Aura  *(proposed 12th door — ALL pending; `01-IO-aura.md`)*
| Variable | Control | Range / default | Consumer | State |
|---|---|---|---|---|
| effect magnitude (morale / cooldown / detection / shield) | Strength slider | per-effect · def str 45 | the **aura pass** (a per-tick neighbour sweep — unbuilt) | **pending** |
| radius | Radius slider | 50 – 5000 m · def rad 40 | aura pass sweep radius | **pending** |
| target (IFF filter) | Target chip | friends/foes/all | aura pass IFF filter | **pending** |
| — Jamming's detection-range penalty | effect = jamming | — | **the ONE whose target variable EXISTS** (detection range) | **pending (pass only)** |

---

## TABLE B — the cross-door wires (producer → consumer)

Every value one door produces that **another door reads**. This is what the Assembler must physically carry
between component bundles. "Breaks if dropped" = the failure if the wire is not run. (Verified against §4 of
the interconnection map + each census.) The four **backbones** are hubs, not single wires.

| # | Producer | Variable | Consumer | Breaks if dropped |
|---|---|---|---|---|
| **Backbone A — mass/budget** | Chassis | budget + Mass + requirement set | ALL (every mounted component) | no frame to spend against; gates don't fire |
| **Backbone B — manpower** | Civic | population → crew pool | ALL crewed parts (`ManpowerTools.ResolveBuild`) | nothing can be manned/built |
| **Backbone C — build+tech** | Industrial | construction points + research | ALL buildables | nothing gets built; nothing unlocks |
| **Backbone D — agency** | Command | a seated officer (not a number) | fleets / battalions / economy / research | systems run, but nobody commands them |
| B1 | Weapons | `Nature` (shield answer) + `Penetration` | Defense (the mirror) | shield/armour math has no attacker input |
| B2 | Defense | shield/armour to defeat | Weapons (the mirror) | weapon effectiveness undefined |
| B3 | Enhancers | Firepower/Toughness Caliber | Weapons/Defense → `ShipCombatValueDB` | veteran cadre does nothing |
| B4 | Enhancers | `EvasionBonus` | Propulsion-sourced evasion stat | reflex booster does nothing |
| B5 | Enhancers | `Crew Reduction` | manpower pool (relieves it) | automation saves nobody |
| B6 | Enhancers | `SwitchableAfter` | doctrine/stance switch cadence | **wire currently DEAD** — no `InterfaceAtb` writes the cut (corr #1) |
| B7 | Propulsion | thrust ÷ mass | Defense → `CalculateEvasion` | **evasion (the best defence) has no source** |
| B8 | Propulsion | thrust-as-signature (3500 K) | Sensors → `SensorSignatureAtb` | engines are invisible to detection |
| B9 | Propulsion | fuel burn | Logistical (fuel demand) | tankers have nothing to carry |
| B10 | Power | `TotalOutputMax` + LocalFuel | Weapons → `WeaponSupply` gate | energy weapons fire with no power |
| B11 | Power | warp sustain kW + `EnergyStoreMax` | Propulsion → `WarpMoveCommand` | FTL can't depart/hold a bubble |
| B12 | Power | signature (1700 K) + shared band code | Sensors → `SensorSignatureAtb` | reactors invisible; solar band unshared |
| B13 | Logistical | `CargoStorageAtb('fuel-storage')` | Propulsion → `NewtonThrust` fuel draw | engines have no fuel |
| B14 | Logistical | ammo / ordnance storage | Weapons → dry-magazine gate + launcher | guns run dry / missiles unstored |
| B15 | Logistical | `CargoStorageAtb('troops')` | Command/ground → `GroundTransport` | **can't ship an invasion (the MVP finish line)** |
| B16 | Sensors | track-range + tracking-speed | Weapons → fire control | the gun has nothing to aim |
| B17 | Sensors | `IntelDirectorateAtb` | Command (officer + post) | no intelligence directorate seat |
| B18 | Chassis | `Structure` (raw HP) | Defense layer 4 (**sourced here, crack C9**) | **ground-only** — ship hull has no HP field (corr #7) |
| B19 | Chassis | Environment tags | Industrial → yard DOMAIN gate | **PENDING** — no environment field on any chassis atb; routing is by `IndustryTypeID` (corr #8) |
| B20 | Chassis | JOBS declared (infra) | Civic → `EmploymentAtbDB.Jobs` (the **missing producer**) | employment-morale term never fires (C1) |
| B21 | Civic | officer graduates + admin capacity | Command roles + `AdminSpaceAtb` seats | no leaders, no seats to fill |
| B22 | Industrial | Support Capacity | Logistical → `InfrastructureCapacityAtb` | logistics route cap undefined |
| B23 | Power/Propulsion | emitter signatures | Sensors → detection first-detect | nothing to detect |

> **Boundary disputes to resolve when wiring (from Phase 2):** C9 — **Evasion** and **Structure** are shown by
> Defense but *owned by Chassis+Propulsion*; the Assembler is where they actually get computed, so it is the
> door of record. C10 — Industrial and Civic both list housing/pop-support; **Support Capacity is Industrial's**,
> the rest Civic's — don't double-count. C1 — `EmploymentAtbDB.Jobs` has a live consumer and **no producer**;
> the Assembler (or the infra host) is the natural producer.

---

## TABLE C — the Assembler's input contract

**This is the heart of the file:** exactly what the Entity Assembler *reads* from the doors, and what it must
*compute itself* because no single door owns it. This is the spec the in-game assembler mirrors.

### C1 · What the Assembler READS from each door (the output bundle it consumes)
| Door | Bundle the Assembler consumes | Becomes part of… |
|---|---|---|
| **Chassis** | host type (ship/ground/station/infra), budget, base Mass, **the requirement set** (reactor/magazine/crew/seat/seal) | the FRAME + the budget everything spends against + the gates |
| **Weapons** | a `WeaponProfile` (10 fields) per weapon component | the **Firepower total** + the kept `List<WeaponProfile>` |
| **Defense** | Shield pool + regen; Armour thickness + 4 resists | **Toughness** + **ShieldCapacity** totals |
| **Enhancers** | multipliers: caliber, training, StrengthBonus, EvasionBonus, Shield, Crew-Reduction, SwitchableAfter | applied *onto* the totals (must not double-count the carry bootstrap) |
| **Power** | `TotalOutputMax`, `EnergyStoreMax`, Lifetime→LocalFuel | the **power supply** side of the power gate |
| **Propulsion** | thrust, exhaust velocity, drive mass; SpeedFactor/RoughHandling (ground) | the **Evasion** + movement terms |
| **Sensors** | detection threshold/range, track-range, signature, cloak/jammer | the **detection footprint** + fire-control |
| **Logistical** | cargo stores (fuel/ammo/troops/ordnance), transfer, Mass | the **supply** the entity carries + spends |
| **Industrial** | build rates, research points, `CrewReq` | the **cost surface** (construction time, research) |
| **Civic** (infra hosts) | jobs, housing, comfort, capacity | the colony/infra host's morale + population terms |
| **Command** | the **seat** (who operates the entity) + leader-death risk | the delegate that flies/fights it (agency, not a number) |
| **Aura** (pending) | projected effect + radius + target | a mountable projector; every output flagged pending |

### C2 · What the Assembler COMPUTES that NO door owns (the totals — from resolver §6.1)
These are the numbers the resolvers actually read, and **not one door produces them** — they are sums and
combinations across the whole assembled entity. This list is the Assembler's real job.

| Total the Assembler computes | Formula / rule | Reads from | Resolver consumer |
|---|---|---|---|
| **Firepower (J/s)** | `Σ weapon.DamagePerSecond × mountCount × enhancer.Caliber` | Weapons × count × Enhancers | `ShipCombatValueDB.Calculate` — ⚠ **guided/missile damage stubbed at 0.1 MJ/s, ignores the real warhead (backlog #3)** |
| **Toughness (J)** | `Σ component.HealthPercent × 100 kJ + armour.thickness × 100 kJ` | every component + Defense | `ShipCombatValueDB` |
| **Evasion (0..0.95)** | `EvasionCap × sizeFactor(chassis volume) × agilityFactor(thrust/mass)` | **Chassis + Propulsion** (the C9 seam) | `CalculateEvasion` |
| **RoleWeight** (1.0 armed / 0.25 utility) | derived from whether weapons are mounted | the weapon list | `ShipCombatValueDB` |
| **ShieldCapacity_J / Regen** | `Σ shield components (ADD across parts)` | Defense + Enhancers | ground: **inline drain/regen** `GroundForcesProcessor`; ship: separate `ShieldAtb` → `ResolveShield` |
| **`List<WeaponProfile>`** | the kept per-weapon footprint, **each mount a separate profile × its count** | Weapons (per component) | `BuildFireMix` |
| **Effective health** | `Health × evasion-mult × armour-mult (+ shield pool)` | the above combined | (readout) |
| **Mass/carry budget check** | `Σ component Mass vs chassis budget` — a pass/fail gate | Chassis + all components | `ShipDesign.Recalculate` / `IsValid` |
| **Power supply vs draw** | `Power.TotalOutput vs Σ (weapon beam draw + warp draw)` | Power vs Weapons+Propulsion | `WeaponSupply` / `WarpMoveCommand` |
| **Crew required vs available** | `Σ CrewReq − Σ Crew-Reduction vs manpower pool` | all components + Enhancers + Civic | `ManpowerTools.ResolveBuild` |
| **Ammo / magazine feed** | `magazine capacity vs Σ weapon ammo draw` | Logistical vs Weapons | dry-magazine gate |
| **The cost surface** *(BUILD — live)* | `Σ credits + materials + research + crew + build-time` across all components | ALL doors + Industrial rates | industry / research / treasury |
| **The RUN-cost surface** *(RUN — 🔨 PLANNED, backlog TIER 2.5)* | `Σ power draw + jobs + food + upkeep` across all components — the ongoing twin of the build cost. **Shown in the assembler now** (`renderRunCost`), each line badged live/pending | ALL doors (per-component run dials) | power grid · employment · sustenance · upkeep biller |
| **Detection footprint** | combined signature (Power 1700 K + Propulsion 3500 K + emitters) vs observer threshold | Power + Propulsion + Sensors | detection (emergent — depends on observer) |
| **Model count / multiplicity** | `component × COUNT`, and "this design fields **N models**" | an **Assembler dial** (the developer's #1 ask) | ground resolver bucketing (**model count pending**) |

### C3 · The three Assembler-only dials (pass the intrinsic test only at assembly time)
The intrinsic test evicts these from every component designer — you cannot set them knowing only one part —
so they are the Assembler's own controls:
1. **Which host** (Chassis choice: ship hull / ground frame / station-installation) — the first forced choice.
2. **Which components, and HOW MANY of each** (`component × count`) — multiplicity is THE assembler dial.
   *Different weapons stay SEPARATE profiles, each × its own mount count — never blended into one averaged gun.*
3. **How many models this design fields** (the six-man squad) — engine-pending (the ground resolver has no
   per-model count yet), but the dial is designed now.

---

## The one-screen summary (for wiring the game)
- **Doors make components; the Assembler makes totals.** Table A is every component number; Table C2 is every
  total no door owns. Wire A into C2 and the resolver reads the result.
- **The Assembler is host-agnostic** — a ship, a ground unit, and a station all produce the *same* totals
  bundle (Firepower/Toughness/Evasion/Shield/WeaponProfiles + cost); only the Chassis choice, the gates that
  apply, and the model-count differ. That is the "ground units work the same as ships" north star, made
  concrete.
- **Evasion and Structure are the Assembler's, not Defense's** (crack C9) — it's where Chassis and Propulsion
  finally combine.
- **The load-bearing pending items** the Assembler must flag, never fake: the **morale field** (fury, rally,
  dread), the **aura pass**, **model count**, the **colonist unload path**, and the **strike-craft launch
  caller**. Each is designed; each waits on one engine primitive.

*Phase 3 complete. Next: Phase 4 — the Entity Assembler design HTML (`entityassembler.html`), staged.*
