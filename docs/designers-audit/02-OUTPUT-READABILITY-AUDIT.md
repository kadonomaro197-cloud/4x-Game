# 02 — OUTPUT READABILITY AUDIT (Phase 2 deliverable)

> **What this is, in one breath:** Phase 1 drew the wiring diagram from what the designs *claim*. This
> file opens the actual engine and checks, for every wire, whether it is really soldered on the far end —
> with a `file:line` citation for each. Then a **skeptic** agent tried to disprove each verdict from
> source; a verdict only survives here if the skeptic couldn't kill it. Where the two disagreed, I went
> to the source myself and ruled.
>
> **Method:** `GameEngine/...` file:line evidence for every claim. Verdicts: **READS** (the game reads it
> today) · **DEAD-END** (produced, zero readers) · **MISSING-CONSUMER** (the named reader doesn't read it /
> is hard-coded / doesn't exist) · **MISSING-PRODUCER** (a reader exists, nothing writes it) · **TYPE-MISMATCH**
> (both ends exist but the wire is the wrong shape). Read `00-MISSION-AND-STATE.md` and `01-INTERCONNECTION-MAP.md`
> first.
>
> **How it was run:** 12 agents (6 subsystem verifiers + 6 shadow skeptics), plus 7 of my own independent
> spot-checks to cross-check them. The agents caught **one error of mine** (C4 — see §4). That's the point.

---

## 1. THE HEADLINE — five things that are truer than any single wire

Reading all ~50 verdicts together, five patterns explain almost everything. **These are the real Phase-2
findings** — more important than any one edge.

### Theme 1 — "Missing" is mostly **BUILT-BUT-DORMANT**, not unbuilt. (the biggest, most hopeful finding)
Over and over, the wire exists end-to-end but is **switched off or unfed by default**. Four flavours:
- **Default-false feature flags:** `ShipDesign.EnforceMassBudget = false` (mass budget is computed and
  displayed but never invalidates a design), `EnergyGenProcessor.EnableFuelExhaustion = false` (fuel
  starvation never bites), `EmconActivityProcessor.EnableReactorHeat = false` (reactor-load signature
  never added), the `EnableFireControl*` flags (fire-control tracking off).
- **Zero-valued demand coefficients:** `PerCapitaFoodDemand = 0` and `PerCapitaPowerDemand = 0` — the
  food-shortage and power-shortage morale wires are fully built and read every tick, but compute to a
  neutral 0 until someone sets the per-capita demand.
- **Producing component on no starting build list:** the naval academy and the intel directorate are
  fully wired, but a default game can't build them (so their downstream loops never light up).
- **The attribute exists but no template declares it:** `EmploymentAtbDB` (C1) — the summer and the morale
  consumer are built; no building carries the jobs attribute, so it's permanently 0.

**Why this matters:** the fix for most of these is a config value or a data line, not an engine build. The
designer system is far more *connected* than a naive "does it work in a default game?" test would show.

### Theme 2 — **Space and ground are asymmetric; the GROUND resolver is the stricter, more complete one.**
Several gates the designs assume are universal exist **only on the ground assembler** (`GroundUnitAssembly.Compute`):
- energy-weapon-needs-a-reactor gate (`GroundUnitAssembly.cs:258` → design invalid) — **ships have none**
  (`ShipDesign.Recalculate` never reads `PowerDraw_W`).
- ammo-weapon-needs-a-magazine gate (`:261`) — **ships have none**.
- weapon **penetration** (`GroundForcesProcessor.cs:437` → `CombatKernel.cs:299 armour − penetration`) —
  **on the ship side penetration is hard-wired 0**, so it's dead.
- the shared **`CombatKernel` flat ArmourSoak** is exercised **only** by the ground resolver; the ship path
  uses its own inline `FleetArmourSoakFraction`.

**Why this matters:** "the same depth space combat has" (the north star) is inverted in these spots — ground
is ahead. A correction that mirrors the ground gates onto ships would close it.

### Theme 3 — **There are TWO combat resolvers, and `AutoResolve` is TEST-ONLY.**
The live combat path is `CombatEngagement.StepEngagementGroup` (driven by the registered
`BattleTriggerProcessor.Tick`). The `AutoResolve.Resolve` engine that the docs describe as the strength-math
resolver has **no live caller** — its only callers are `Pulsar4X.Tests`. This has a sharp consequence:
**the Enhancers Firepower-Caliber bonus is dead in real combat.** The caliber firepower multiplier is applied
only to the aggregate `cv.Firepower` field (`ShipCombatValueDB.cs:529`), and only `AutoResolve` sums that
field — the live `BuildFireMix` reads **per-weapon dps** (`CombatEngagement.cs:1340`), which the caliber never
re-scales. (The Toughness-Caliber twin **is** live, because `ApplyCasualties` reads `cv.Toughness` directly.)

### Theme 4 — **Named-consumer misattribution: the data flows, but not where the design thinks.**
Multiple wires DO connect, just through a different consumer than the designer names. The value is real; the
mental model of *where it's read* is off:
- **Armour nature-resist** → read by inline `FleetArmourSoakFraction`, **not** `CombatKernel` (ship side).
- **Reactor-load & thrust signature** → read by `EmconActivityProcessor`, **not** the static `SensorSignatureAtb`.
- **IntelDirectorateAtb** → lives in `Factions/`, consumed by `IntelDirectorateProcessor`/`Espionage`, **not** `Sensors/`.
- **Detection-range (`RangeForSignal`)** → feeds map/coverage readouts; the **combat trigger** reads
  `SensorContactExists` (the live scan), not the reverse-solved range.

**Why this matters:** these aren't broken wires — they're **documentation/ownership** corrections. Phase 3
should re-label them so the designer's outputs point at the real consumer.

### Theme 5 — **Wrong-mechanism: some named producers don't exist; the real one is a bespoke component.**
The Logistical designer's "cargo class taxonomy" claims `CargoStorageAtb('ammo')` and `CargoStorageAtb('troops')`
holds. **Neither cargo class exists.** Ammo is a dedicated `ShipMagazineAtb` (space) / `GroundMagazineAtb`
(ground); troops ride a dedicated `GroundBayAtb`. The invasion and dry-magazine chains **work** — but on
those bespoke components, not on a cargo-storage type. So the designer's taxonomy over-promises: it lists two
"cargo classes" that are really separate doors.

---

## 2. THE FULL LEDGER — every edge, sourced

`P:` = producer evidence, `C:` = consumer evidence (both `file:line` under `GameEngine/`). Verdict is the
**post-skeptic** ruling. "dormant" = wired but off/unfed by default (Theme 1).

### Cluster A — Combat kernel & calibers
| Edge | Verdict | Evidence |
|------|---------|----------|
| Weapon **nature** → shield-soak | **READS** | P:`ShipCombatValueDB.cs:359/381/417/448` C:`CombatKernel.cs:182-189,243` via `CombatEngagement.cs:764 ApplyShield` |
| Weapon **penetration** → armour | **READS (ground only)** | C:`GroundForcesProcessor.cs:437`→`CombatKernel.cs:299`. **Ship side: penetration hard-0, dead** (`BuildFireMix:1368`) |
| Defense **Shield** cap+regen → peel | **READS** | P:`ShipCombatValueDB.cs:461-462` C:`CombatEngagement.cs:1391-1392,1585`→`CombatKernel.cs:253-266` |
| Defense **Armour** nature-resist → soak | **READS** *(misattributed)* | P:`ShipCombatValueDB.cs:476-479` C:`CombatEngagement.cs:1536-1537,770-771` — **inline `FleetArmourSoakFraction`, NOT CombatKernel** |
| Enhancer **Firepower Caliber** → combat | **MISSING-CONSUMER** | Applied only to `cv.Firepower` (`:529`); live `BuildFireMix:1340` reads per-weapon dps; `cv.Firepower` summed only by **test-only** `AutoResolve.cs:64` |
| Enhancer **Toughness Caliber** → combat | **READS** | P:`ShipCombatValueDB.cs:530` C:`CombatEngagement.cs:896 ApplyCasualties` (live) |
| Sensors track → **weapon may fire** | **TYPE-MISMATCH** | Fire gated by hard-coded `WeaponProfile.Range_m` consts (`ShipCombatValueDB.cs:52/62/68`); sensors gate only whether the **engagement forms** (`FleetDetects`) |
| Propulsion thrust/mass → **evasion** → dodge | **READS** | P:`ShipCombatValueDB.cs:581-585` C:`CombatKernel.cs:196-200` via `ApplyCasualties`; ground `GroundForcesProcessor.cs:423` |
| Ground weapon+augments → resolver | **READS** | P:`GroundUnitAssembly.cs:141/107/189/191/229` C:`GroundForcesProcessor.cs:422-437` |

### Cluster B — Mass/budget hub (Backbone A) + chassis gates
| Edge | Verdict | Evidence |
|------|---------|----------|
| Component **Mass** → ShipDesign sum | **READS** *(cap not enforced)* | P/C:`ShipDesign.cs:230-232,263`. Budget CAP computed `:285-286` but **`EnforceMassBudget=false`** (`:54,290`) → advisory only |
| Mass → **credits / crew** | **TYPE-MISMATCH** | Only `IndustryPointCosts = mass×0.1` (`:267`). `CreditCost` (`:244`) & `CrewReq` (`:233`) are plain per-component sums, **not** mass-derived |
| Energy weapon → **reactor gate** | **READS (ground only)** | C:`GroundUnitAssembly.cs:218,258-259,264`. **No ship equivalent** — ship energy weapon w/o reactor is unenforced |
| Ammo weapon → **magazine gate** | **READS (ground only)** | C:`GroundUnitAssembly.cs:221,261,264`. **No ship equivalent** |
| Crew req → **ManpowerTools gate** | **READS** *(inert unmanned)* | P:`ShipDesign.cs:233` C:`IndustryTools.cs:158-164`; `ManpowerTools.cs:41-42` allows when host has no `ColonyManpowerDB` |
| Operating-envelope multiplier / band cost | **MISSING-PRODUCER** | NONE-FOUND — the field **does not exist** anywhere. Nothing prices an operating band |
| Part `ComponentMountType` → install gate | **MISSING-CONSUMER** | Only gate is `OrdnanceDesign.cs:97` (missile filter). `Entity.AddComponent`/`ShipDesign.Recalculate`/`GroundUnitAssembly` never check `IChassisAtb.PartMount` |

### Cluster C — People: manpower pool + Civic + Command seats
| Edge | Verdict | Evidence |
|------|---------|----------|
| **`EmploymentAtbDB.Jobs`** → morale | **MISSING-PRODUCER (C1)** | C:`ComponentInstancesDBExtensions.cs:24`→`PopulationProcessor.cs:74`→`ColonyMoraleDB.cs:133-139`. **No template declares `EmploymentAtbDB`** → always 0 |
| Housing/**Comfort** → morale | **READS** | P:`HousingAtbDB.cs:21`+`installations.json:1143/1245` C:`ComponentInstancesDBExtensions.cs:40`→`ColonyMoraleDB.cs:145` |
| **Population-support** → crowding | **READS** | P:`installations.json:1131/1233` C:`ComponentInstancesDBExtensions.cs:108`→`PopulationProcessor.cs:66`→morale |
| **Food** → morale | **READS** *(dormant)* | P:`SustenanceProcessor.cs:68,80`+`installations.json:60` C:`PopulationProcessor.cs:56`. `PerCapitaFoodDemand=0` → neutral by default |
| Academy **graduate** → seatable commander | **READS** *(unreachable start)* | P:`NavalAcademyProcessor.cs:23,53`→`CommanderFactory.cs:33` C:`AssignAdministratorOrder.cs:60-79`. Academy on no start build; nothing auto-seats |
| Admin capacity → **seats** | **READS** *(occupancy unread)* | P:`AdminSpaceAtb.cs:34-38`+`AdminSpaceProcessor.cs:34-47`+`installations.json:1356` C:`AssignAdministratorOrder.cs:50-79` |
| Backbone B: CrewReq → **manpower pool** | **READS** | P:`ShipDesign.cs:233` C:`IndustryTools.cs:158`→`ManpowerTools.cs:44-46` |
| Seat **SeatType/AdminLevel** → span rule | **DEAD-END (C8)** | Written `AdminSpaceProcessor.cs:43`; **no rule compares/branches on it** (grep clean) |
| Seated **CommanderID** → drives anything | **DEAD-END** | Only seat-management reads. The live people→combat loop reads **`ShipInfoDB.CommanderID`** (flagship, `CombatEngagement.cs:1704`), **not** the admin seat — occupancy is cosmetic |

### Cluster D — Industrial build + tech (Backbone C)
| Edge | Verdict | Evidence |
|------|---------|----------|
| IndustryAtb **rate table** (5 types) | **READS** | P:`industryTypes.json:5,12,19,26,33` C:`IndustryTools.cs:135,323`. Types: refining, component-/installation-/ordnance-construction, ship-assembly |
| **Fighter Construction Points** → channel | **MISSING-CONSUMER (C5)** | P:`installations.json:424-430` (property, formula `'0'`) C:NONE — no `fighter-construction` type; the "Construction Points" DataDict (`:441-443`) omits it |
| Research **bonusCategory** → bonus | **READS** | P:`ResearchPointsAtbDB.cs:77` C:`ResearchProcessor.cs:327,338` (live via `DoResearch:170`) |
| Research **`_costPerDay`** → daily cost | **READS** *(was C4 — see §4)* | P:`ResearchPointsAtbDB.cs:71` (install copies into `ResearcherDB.CostPerDay`) C:`ResearchProcessor.cs:107,109,114` |
| **ProductionLines** → built component | **READS** *(Backbone C)* | P:`IndustryTools.cs:130,135` C:`:227`→`ComponentDesign.cs:63-83` (builds `ComponentInstance`, installs or stockpiles) |
| **`LogiBaseAtb`** capacity → consumer | **DEAD-END (C2)** | P:`LogiBaseAtb.cs:27` C:NONE outside its own file; `LogisticsProcessor` never reads `.Capacity` |
| **InfrastructureCapacity** → efficiency | **READS** | P:`InfrastructureProcessor.cs:75` C:`InfrastructureDB.cs:31-37`→`IndustryTools.cs:115,121` (scales every rate) |
| Bunker **fortification** → damage divisor | **READS** | P:`GroundDefenseAtb.cs:30,32`→`GroundFortification.cs:49,61,92` C:`GroundForcesProcessor.cs:376,492,525` |

### Cluster E — Power / Propulsion / warp / signature
| Edge | Verdict | Evidence |
|------|---------|----------|
| Power **TotalOutput** → colony sustenance | **READS** *(dormant)* | P:`EnergyGenAbilityDB.cs:17-20` C:`SustenanceProcessor.cs:60-61`. `PerCapitaPowerDemand=0` default |
| Power **stored** → warp departure gate | **READS** | P:`EnergyGenProcessor.cs:64` C:`WarpMoveCommand.cs:265-268` (`creationCost > EnergyStored → return`) |
| Power **fuel-starve** (`LocalFuel`/`IsFuelStarved`) → weapon/AI | **MISSING-CONSUMER** | `IsFuelStarved` has **zero** code readers; `LocalFuel` gate only inside `EnergyGenProcessor.cs:44` behind `EnableFuelExhaustion=false`. Named consumers read other fields |
| Power **EnergyStoreMax** → warp-create | **MISSING-CONSUMER** | Warp gate reads `EnergyStored` (current), never `EnergyStoreMax` (cap). No `WARP_CREATE` const exists |
| Power **signature** (reactor Load) → detect | **MISSING-CONSUMER** *(dormant)* | Static `SensorSignatureAtb` doesn't read Load; real path `EmconActivityProcessor.cs:64,115` gated by **`EnableReactorHeat=false`** |
| Propulsion **thrust-signature** → detect | **MISSING-CONSUMER** *(wrong shape)* | Real path `EmconActivityProcessor.cs:114` is **live** but boolean×const (`ThrustHeat=4.0` when burning at all, `:42,71`), **not magnitude=thrust** |
| Propulsion **bubble-sustain** → power demand | **READS** | P:`WarpMoveProcessor.cs:246 AddDemand` C:`EnergyGenAbilityDB.cs:45-50`→`EnergyGenProcessor.cs:59-62` |
| Propulsion **fuel-burn** → cargo draw | **READS** | P:`NewtonianMovementProcessor.cs:112,119` C:`:121-123 AddRemoveCargoMass(−FuelBurned)` |
| **Drive-heat** → fleet heat pool | **MISSING-PRODUCER (C6)** | `HeatPool_kJ` fed only by weapon `HeatPerSecond` (`CombatEngagement.cs:717,1429`); no drive term |
| **Generic component power-draw** | **MISSING-CONSUMER (C12)** | Only warp (`AddDemand`) + energy weapons (direct `EnergyStored` subtract) draw power; no generic `PowerDraw` field the balance nets |
| **Shield draws power** (Power→Defense) | **DEAD-END (C15)** | `ResolveShield` (`CombatKernel.cs:253-264`) regenerates from `regen*dt` with **no reactor read**. Shields recharge free |

### Cluster F — Logistical transport + Sensors detection
| Edge | Verdict | Evidence |
|------|---------|----------|
| **Fuel-storage** → NewtonThrust | **READS** | P:`CargoStorageAtb` fuel bay C:`NewtonSimpleProcessor.cs:66-70,98-99` (drains fuel type per burn) |
| **`CargoStorageAtb('ammo')`** → magazine gate | **MISSING-CONSUMER** *(wrong mechanism)* | Ammo is **not** a cargo class; gates read `ShipMagazineAtb` (`ShipCombatValueDB.cs:485-489`) / `GroundMagazineAtb` (`WeaponSupply.cs:66-67`) |
| **`CargoStorageAtb('troops')`** → invasion | **MISSING-CONSUMER** *(wrong mechanism)* | Troops ride bespoke `GroundBayAtb`; chain `LoadTroopsOrder.cs:62`→`GroundTransport.cs:43-50` reads that, not cargo |
| Frame-**Size** → `CarrySizeOf` | **MISSING-CONSUMER (C11)** | `GroundTransport.cs:29-35` hard-coded switch (Inf=1/Art=2/Arm=3); own comment admits "become strength-scaled once the designer has a size knob" |
| **Passenger/cryo** → colony Population | **MISSING-CONSUMER (C3)** | Only reader of passenger-storage is `TeamObject.cs:66` (scientist/commander teams, not colonists). No unload→`Population` path exists |
| **Perishable** → food draw | **READS** *(dormant)* | C:`SustenanceProcessor.cs:121,130` (`DrawStoredFood`). Inert while `PerCapitaFoodDemand=0` |
| **DockBay** → DockedShips registry | **READS** *(no game caller yet)* | P/C:`DockTools.cs:40-49,153-161`. Internally complete; **no order/processor calls `TryDock`** — "the order is the next slice" |
| Sensor **band gate** overlap | **TYPE-MISMATCH (C7)** | `SensorTools.cs:147` uses `max(recvMin,sigMin) < max(sigMin,sigMax)` — drops `recvMax`; correct is `< min(recvMax,sigMax)` |
| **`RangeForSignal`** → fog / combat trigger | **READS** *(trigger via other path)* | P:`SensorTools.cs:435` C:`SensorScan.cs:125`, `FleetCombat.cs:116` (readout/coverage). Combat trigger reads `SensorContactExists` (live scan), not this |
| **`IntelDirectorateAtb`** → op-capacity | **READS** *(in Factions/, dormant start)* | P:`IntelDirectorateAtb.cs:44-59` C:`IntelDirectorateProcessor.cs:39`, `Espionage.cs:63` |

---

## 3. Crack scorecard — the 15 Phase-1 cracks, ruled

| # | Phase-1 crack | Phase-2 verdict | Status |
|---|---------------|-----------------|--------|
| C1 | `EmploymentAtbDB.Jobs` producer | **MISSING-PRODUCER** — no template declares the attribute | **CONFIRMED** (crack) |
| C2 | `LogiBaseAtb` readers | **DEAD-END** — zero external readers | **CONFIRMED** (crack) |
| C3 | passenger → colonists | **MISSING-CONSUMER** — no unload→Population path | **CONFIRMED** (crack) |
| C4 | research Cost-Per-Day | **READS** — install copies `_costPerDay`→`ResearcherDB.CostPerDay`, charged daily | **NOT A CRACK** (I was wrong — §4) |
| C5 | Fighter Construction Points | **MISSING-CONSUMER** — no `fighter-construction` type | **CONFIRMED** (crack) |
| C6 | drive-heat | **MISSING-PRODUCER** — drives emit no heat to the fleet pool | **CONFIRMED** (crack) |
| C7 | sensor band gate math | **TYPE-MISMATCH** — `SensorTools.cs:147` wrong upper bound | **CONFIRMED** (crack) |
| C8 | seat `SeatType/AdminLevel` enforcement | **DEAD-END** — label no rule reads | **CONFIRMED** (crack) |
| C9 | Defense outsources Structure/Evasion | **CONFIRMED** — Structure from Chassis, Evasion from Propulsion (both `READS` at those doors) | **CONFIRMED** (boundary) |
| C10 | Industrial vs Civic housing overlap | **PARTIAL** — both can carry housing attrs; needs a door-of-record ruling (Phase 3) | **CONFIRMED** (boundary) |
| C11 | frame-Size → `CarrySizeOf` | **MISSING-CONSUMER** — hard-coded 3-value switch | **CONFIRMED** (crack) |
| C12 | generic component power-draw | **MISSING-CONSUMER** — no generic mechanism | **CONFIRMED** (crack) |
| C13 | Enhancers self-repair | **BLOCKED** — whole-or-dead, no damaged state (confirmed by absence) | **CONFIRMED** (blocked) |
| C14 | Enhancers foresight/react | **MISSING-VARIABLE** — combat simultaneous, no initiative field | **CONFIRMED** (blocked) |
| C15 | shields draw power | **DEAD-END** — shield regen reads no reactor | **CONFIRMED** (absent wire) |

**14 of 15 cracks confirmed; 1 (C4) overturned by verification.** Plus the five cross-cutting themes in §1 —
especially the **built-but-dormant** finding, which reframes many "missing" verdicts as "switched off."

---

## 4. Self-correction log (honesty guard)

**C4 — research-lab Cost-Per-Day. I called it a DEAD-END in my first-pass chat update and my own note. That
was WRONG.** Both cluster-D agents (verifier and skeptic) ruled READS with specific evidence, and when I
re-read `ResearchPointsAtbDB.cs:56-86` I confirmed them: `OnComponentInstallation` builds a `new ResearcherDB`
and at line 71 copies the designer's `_costPerDay` into the live `ResearcherDB.CostPerDay`, which
`ResearchProcessor.cs:107` reads and charges every day (`:114 Money.AddExpense`). My error was concluding from
a grep that line 71 was "internal/dead" — it is actually the object-initializer that *seeds the live blob*.
My "same-named decoy" framing was also wrong: funding modifiers scale that same seeded value, they don't
replace it. **C4 is a fully connected wire and is removed from the crack list.** This is exactly why the
mission mandates adversarial verification — the skeptics caught my mistake.

*(My other six independent spot-checks — C1, C7, C11, C2, C5, and Backbone D — all matched the agents. Only
C4 was mine to correct.)*

---

## 5. What this hands Phase 3 — the correction buckets

The verdicts sort into five repair kinds, cheapest first. Phase 3 will dependency-order them with gauges.

1. **FLIP A FLAG / SET A COEFFICIENT (cheapest — built, just off).** `EnforceMassBudget`,
   `EnableFuelExhaustion`, `EnableReactorHeat`, `PerCapitaFoodDemand`, `PerCapitaPowerDemand`. Each is a
   value change that lights a wire that already reads correctly. *Caution:* flipping `EnforceMassBudget`
   turns an advisory into a hard invalidation — blast radius is every existing design.
2. **ADD A DATA LINE (cheap — attribute exists, no template carries it).** C1 (`EmploymentAtbDB` on
   industry/civic building templates). Possibly C5 if a `fighter-construction` type is added to
   `industryTypes.json`.
3. **RE-LABEL THE DESIGNER'S OUTPUT (documentation, zero engine risk).** Theme 4 — point the designer at
   the real consumer: armour→`FleetArmourSoakFraction`, signature→`EmconActivityProcessor`,
   intel→`Factions/`, detection-trigger→`SensorContactExists`. And Theme 5 — the Logistical designer must
   stop calling ammo/troops "cargo classes" and name the real components (`ShipMagazineAtb`/`GroundMagazineAtb`/`GroundBayAtb`).
4. **MIRROR A GROUND GATE ONTO SHIPS (small engine build).** Theme 2 — the reactor gate, magazine gate,
   and penetration/flat-armour path exist on ground; add the ship equivalents in `ShipDesign.Recalculate` /
   the ship resolver. Also fix the Firepower-Caliber live-path bug (Theme 3): re-scale per-weapon dps, or
   route the live resolver through the calibered `cv.Firepower`.
5. **BUILD A MISSING MECHANISM (real design + build → feeds Phase 4).** C3 (colonist unload→Population),
   C6 (drive-heat producer), C11 (frame-Size→carry), C12 (generic power-draw), C15 (shield power cost),
   the C8/C9/C10 boundary rulings, and the dead `LogiBaseAtb` (C2 — decide its consumer or cut it). C13/C14
   are blocked on engine-model changes (a damaged state; an initiative variable) and may be deferred.

---

*Phase 2 complete. Next: Phase 3 — Correction Plan (`03-CORRECTION-PLAN.md`): the smallest design fix for
each broken/absent connection, dependency-ordered, each with its gauge and blast radius.*
