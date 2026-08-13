# 06 — EVERY OUTPUT BY DOOR (does the game read them back?)

> **The objective, stated plainly.** `05-MATERIAL-INPUTS-BY-DOOR.md` asked the supply question: for everything a
> component *consumes*, does the game **provide** it? This file asks the mirror question: for everything a
> component *produces* — every number a door's dials write into the simulation — does the game actually **read it
> back**? An output nobody reads is a dial that does nothing: you can turn it, the design changes on paper, and
> the running game never notices. That's the output-side of the two-ends rule (gotcha #10): *a produced number
> nobody reads is just as broken as a required number nobody supplies.*
>
> **Method — the same forensics as the inputs pass.** The inputs check earned its confidence by scanning **every**
> `ResourceCost` key against the defined ids until each one resolved. This check earns it the same way: one
> forensic verifier per door went into the C# engine source, found the **actual reader** of each output (grep the
> consumer symbol, read the method), and cited it **file:line** — or proved zero readers exist. Twelve doors, each
> output traced to its consumer or to a dead end. Verified 2026-08-02 against `Pulsar4X/GameEngine/`.
>
> **The baseline it checks.** The demand-side list is `02-IO-MATRIX.md` Table A (every sim-reaching variable per
> door, with its claimed consumer and build-state). This file is that table **re-verified from the reader's end** —
> and it found real corrections, listed at the bottom.

---

## HOW TO READ THE VERIFIED-STATE COLUMN

Same four states the matrix uses, plus the two honest sub-cases the source forced us to add:

| State | Plain meaning |
|-------|---------------|
| **LIVE** | A processor in the running game reads this number today. Cited file:line. |
| **LIVE (gated)** | The reader is real and wired, but sits behind a default-**off** flag the client flips on. Byte-identical to off in a headless run; a genuine reader the moment the client enables it. |
| **LIVE (ground-only)** | The reader exists, but **only the ground resolver reaches it.** On a **ship** the same output is written to no effect (the ship resolver takes a different path). The single most important structural finding below. |
| **READ** | This door only *displays* the number; a **different** door owns and writes it. A wire to carry, not a value to make. |
| **EMERGENT** | Computed by the Assembler/resolver from the **whole assembled entity** (needs finished hull mass), never a stored dial. Live once assembled. |
| **DEAD-END** | The number is written but **no gameplay rule reads it** — zero readers, or only a client display / an unfired event. A dial that does nothing. |
| **PENDING** | **No engine variable exists yet at all.** The dial is designed; the thing it would write hasn't been built. |

---

## THE SUMMARY — one row per door

**Reading it:** "live" counts every output with a real reader (including gated / ground-only). "Dead" = written but
unread. "Pending" = no engine target yet. "Read/emergent" = owned or computed elsewhere (correct by design).

| Door | Outputs | Live | Read / Emergent | Dead-end | Pending | Verdict |
|------|:------:|:----:|:----:|:----:|:----:|---------|
| **Weapons** | 10 (+heat) | 10 | — | 0 | 0 | ✅ all read — but 3 are **ground-only**, PD is ship-only, one live field (`HeatPerSecond`) the door forgot to emit |
| **Defense** | 6 | 4 | 2 read | 0 | 0 | ✅ all read — but shields are **ground-inline**, not the ship kernel the matrix named |
| **Chassis** | 9 | ~4 | 2 mixed | 2 | rest | ⚠ taxonomy live as free dials; the **derived** half (√-law, env-tag) has no field |
| **Civic** | 10 | 6 | — | 0 | 3 | ⚠ **crack C1**: employment consumer live, **producer absent** → term = 0 forever |
| **Command** | 6 | 2 | — | 3 | 1 | ⚠ one live wire (leader-death); 2 dead, 1 live-but-dead-ending (radius) |
| **Enhancers** | 9 | 6 | — | 1 | 2 | ⚠ **iface `SwitchableAfter` has no writer** (matrix said live) |
| **Industrial** | 8 | 7 | — | 1 | 0 | ✅ economy fully read; only Fighter-Construction-Points is dead |
| **Logistical** | 10 | 5 | — | 2 | 3 | ⚠ **3 of 7 cargo-types feed a *different* component** than the matrix named |
| **Power** | 10 | 9 | — | 0 | 1 | ✅ cleanest door; only the RTG-law is pending |
| **Propulsion** | 8 | 6 | 1 emergent | 0 | 1 | ✅ all read; cruise/Δv under-stated as "readout" (they drive real transit) |
| **Sensors** | 9 | 8 | — | 0 (+1) | 1 | ✅ all read (4 gated) — but the **band-match gate has a real bug (C7)** |
| **Aura** | 4 | 0 | — | 0 | 4 | ⏳ proposed door — **zero** engine matches; nothing built |

---

## THE HEADLINE

**Unlike the inputs pass — where the honest answer was a clean "all 12 doors provided" — the outputs are a
*mixed* picture, and that's the useful finding.** Three distinct things are true at once:

1. **The combat-and-economy core is genuinely wired.** Every output of **Weapons, Power, Industrial, Propulsion,
   Sensors, and Defense** reaches a live reader. The numbers that decide a fight (damage, armour, evasion,
   detection, power) and the numbers that run an economy (industry rate, research, mining, crew, infrastructure)
   are all read back. This is the good news and it's most of the surface.

2. **There is a real dead-and-pending residue** — the output-side equivalent of the "17 unconsumed materials."
   Six outputs are written and **never read** (true dead ends), and about a dozen more are **pending** (the dial
   is designed, the engine target doesn't exist yet). The full list is below; none are load-bearing for the MVP,
   but each is a dial that currently does nothing.

3. **The biggest finding isn't dead outputs — it's *mislabeled* ones.** Several outputs are fully **live**, but
   the matrix named the **wrong consumer**. The standout: **the same weapon or defense component behaves
   differently depending on whether it's mounted on a ship or a ground unit**, because the ship resolver and the
   ground resolver read a *different subset* of the outputs. That's not a bug — it's the ship-vs-ground split
   baked into the two resolvers — but the Assembler has to know it, because it changes what a component "does"
   based on its host.

**Bottom line for the Assembler:** the outputs it must carry are real and mostly read. But it cannot treat "the
door produced a number" as "the game uses that number" — for six outputs that's false, for a dozen more the target
doesn't exist yet, and for the combat outputs *which* reader fires depends on the host. The verified per-output
truth is below.

---

## THE LIST, BY DOOR

Legend: **file:line** = the actual reader in `Pulsar4X/GameEngine/`. ✅ = matrix was right · ⚠ = matrix
correction (folded into the corrections table at the end).

### ✅ Weapons — 10 outputs (the resolver's `WeaponProfile`), all read; 3 ground-only
| Output | Verified reader (file:line) | State |
|---|---|---|
| DamagePerSecond | Σ→Firepower `ShipCombatValueDB.cs:524`; `BuildFireMix` `CombatEngagement.cs:1350`; kernel `CombatKernel.cs:228` | **LIVE** (ship+ground) |
| VolumeOfFire (`Saturation`) | saturation floor `CombatKernel.cs:211` | **LIVE** (ship+ground) |
| Range_m | `WeaponReaches` gate `CombatKernel.cs:174`; range term `:202` | **LIVE** (ship+ground) |
| Velocity | velocityTerm `CombatKernel.cs:198` | **LIVE** (ship+ground) |
| Tracking | trackingEff `CombatKernel.cs:199` | **LIVE** (ship+ground) |
| Nature (shield soak) | `ShieldSoakFraction` `CombatKernel.cs:182`; ammo-nature `CombatEngagement.cs:1402` | **LIVE** (ship+ground) |
| DamagePerShot (`PerShotEnergy`) | `ArmourSoakBurst` `CombatKernel.cs:331` via `GroundForcesProcessor.cs:435` | ⚠ **LIVE (ground-only)** — ship zeroes it `CombatEngagement.cs:1368` |
| Rate | folded coupling `BurstShotCount = dps/PerShotEnergy` `CombatKernel.cs:318` (no standalone field) | ⚠ **LIVE (ground-only)** |
| Penetration | `ArmourSoak` `CombatKernel.cs:294` via `GroundForcesProcessor.cs:437` | ⚠ **LIVE (ground-only)** — ship zeroes it `CombatEngagement.cs:1368` |
| PD-answerable (`Delivery==Guided`) | `IsInterceptable` `CombatEngagement.cs:1465` → `InterceptMissiles:1503` | **LIVE (ship-only)** — ground has no intercept step |
| runs-on (power/ammo/none) | ammo derived from **Nature** `CombatEngagement.cs:1402` → drain `:699` | ⚠ **PARTIAL** — ammo yes; power/none no reader; keyed on wrong axis (see corrections) |
| *(HeatPerSecond)* | **live field** `WeaponProfile.cs:126` → heat throttle `CombatEngagement.cs:714`; door doesn't emit it | ⚠ **LIVE reader, un-emitted output** |

**Structural finding:** the three flat-armour outputs (per-shot energy, rate, penetration) have live readers **only
in the ground/bombardment resolver.** A weapon on a **ship** writes them to no effect — the ship path folds armour
into one Toughness pool and zeroes penetration/per-shot in `BuildFireMix` (`CombatEngagement.cs:1366-1368`). Same
component, different behaviour by host. This matches the weapon file's own caveat (`WeaponProfile.cs:105-118`).

### ✅ Defense — 6 outputs; shields are ground-inline, not the ship kernel
| Output | Verified reader (file:line) | State |
|---|---|---|
| Shield pool | inline drain `GroundForcesProcessor.cs:426-430` (`t.CurrentShield -= absorbed`) | ⚠ **LIVE (ground-inline)** — NOT `CombatKernel.ResolveShield` |
| ShieldRegenFraction | inline regen `GroundForcesProcessor.cs:391-395` (fraction/hr) | ⚠ **LIVE (ground-inline)** — not the kernel's Jps arg |
| Armour Defense | `ArmourSoak` `GroundForcesProcessor.cs:437` → `CombatKernel.cs:294` (×1.5/pt, floor 10%) | ✅ **LIVE** |
| Four nature resists | `ArmourResistFor` `GroundForcesProcessor.cs:436` → natureFactor `CombatKernel.cs:302` | ✅ **LIVE** (engine does **not** renorm to 4.0 — that's door-only) |
| Evasion mult | `CalculateEvasion` reads Chassis+Propulsion `ShipCombatValueDB.cs:571-596`; door shows constant `EVA_READ=3.2` | ✅ **READ** (Chassis+Propulsion — crack C9 confirmed) |
| Structure (HP) | kernel HP from Toughness `ShipCombatValueDB.cs:297,519`; door shows constant `HP_READ=4000` | ✅ **READ** (Chassis/emergent — C9) |

*The Defense door builds **ground** components (`GroundAugmentAtb`/`GroundArmorAtb`). The matrix named ship-side
consumers for the shield rows — the outputs are live, but drained/regenerated inline in the ground resolver, never
through `CombatKernel.ResolveShield` (which belongs to a separate ship `ShieldAtb`).* Also: the neutral kernel field
`Combatant.ShieldCapacity` set in `GroundCombatant.ToCombatant:131` has **zero callers** → a dead field.

### ⚠ Chassis — taxonomy live as free dials; the derived half unbuilt
| Output | Verified reader (file:line) | State |
|---|---|---|
| Mass (base) | `GroundUnitAssembly.cs:98`, `StationAssembly.cs:61`, `BuildingAssembly.cs:46`, ShipDesign `MassPerUnit` | **LIVE** (constant today; ship = `Hull Mass` property) |
| Budget (MassBudget/BaseStrength/…) | `ShipDesign.cs:281` (`hullBudget += MassBudget*count`) + 3 other assemblers | **LIVE** (free dial; soft gate — `EnforceMassBudget` defaults **false** `ShipDesign.cs:54`) |
| Reactor-for-energy-weapon gate | `GroundUnitAssembly.cs:218,258` only (via `WeaponSupply.cs:73,101`) | **LIVE (1 cell only)** — pending in ship/station/building |
| Ammo-magazine gate | `GroundUnitAssembly.cs:221,261` only | **LIVE (1 cell only)** |
| Crew / seated-leader gate | crew gate at **construction** (`IndustryTools.cs:158`); berth in Sites (`SiteWorkProcessor`) | **MIXED** — live in another system, not chassis-declared |
| Env-seal / feed / fuel gate | env-seal a **stat** `GroundUnitAssembly.cs:313`; food/fuel separate systems | **MIXED** — live stats elsewhere |
| Structural efficiency (`K_SHIP·…`) | grep `K_SHIP`/`StructuralEfficiency` = **zero hits**; `research="0"` on every chassis | **DEAD-END / PENDING** — no field exists |
| Structure (HP) → Defense | ground: `GroundChassisAtb.BaseHP:50` → `GroundUnit.MaxHealth`; **ship hull has NO HP field** | ⚠ **LIVE (ground-only)** — matrix over-generalized |
| Environment tags → yard domain gate | **no `Environment` field** on any chassis atb (grep empty); routing is `IndustryTypeID` | ⚠ **PENDING** — the tag doesn't exist as engine state |

*Bonus: `IChassisAtb.StructuralBudget` is read by only 2 of the 4 assemblers (station+building via the interface;
ship+ground bypass to the concrete property). The "uniform chassis view" is wired in 2 of 4 cells.*

### ⚠ Civic — crack C1 confirmed: consumer live, producer absent
| Output | Verified reader (file:line) | State |
|---|---|---|
| Food output | `SustenanceProcessor.cs:67` → sums `FoodProductionAtbDB.FoodOutput` `ComponentInstancesDBExtensions.cs:52` | **LIVE** (producer template exists) |
| Life-support / habitat capacity | `PopulationSupportAtbDB.PopulationCapacity` `:108` → `PopulationProcessor.cs:26` (carrying cap) | **LIVE** |
| Residency housing capacity | same pop-support path | **LIVE** |
| Academy cadets | `NavalAcademyAtb.ClassSize:11` → `NavalAcademyProcessor` (live code) | **LIVE (code) / UNREACHABLE** — academy on no start colony's build list |
| Admin span | `AdminSpaceProcessor.cs:42` → `ReconcileSeats` → `AdminSpaceDB.CommanderSeats` | **LIVE** (seat mechanism; numeric span itself unread) |
| CrewReq | `InfrastructureProcessor.cs:95` (×CapacityPerCrew → infra demand) | **LIVE** |
| Comfort (cap 20) | `GetHousingComfort` `:35` → `ColonyMoraleDB.cs:145` | **LIVE** (dials to 50 but morale caps 20 → top ~60% inert) |
| **Employment — consumer** | `GetTotalJobs` `:22` → `PopulationProcessor.cs:74` → `ColonyMoraleDB.cs:132` (±40 band) | **LIVE (consumer)** |
| **Employment — producer** | grep `new EmploymentAtbDB(` = only ctor defs; no template declares it | ⚠ **PENDING — PRODUCER ABSENT (C1)**: employmentRatio pinned at `-1.0` → term = **0 forever** |
| Medical "+N health" | `MoraleInputs` has no health field; `LegitimacyProcessor` reads morale+war only | **PENDING** — no consumer |
| Security "−N unrest" | `LegitimacyProcessor` live (`:81-115`) but reads morale+war only; no security atb | **PENDING** — output unread |

### ⚠ Command — one live wire; two dead, one live-but-dead-ending
| Output | Verified reader (file:line) | State |
|---|---|---|
| Survivability (leader-death) | `SiteWorkProcessor.cs:297` `SiteHazard.IncidentChance` → `DestroyCommander:305`; call site `:178` | **LIVE** (dormant until a site is spawned) |
| Grade (site work rate) | `SiteWorkProcessor.MultiplierOf:279` (×10 at grade 10) → `Accrue:175` | **LIVE** (same dormancy) |
| Office Space → hex radius | `ColonyHexMapProcessor.cs:67` → `UpdateMaxRadius:44` → `ColonyHexMapDB.cs:53` | ⚠ **LIVE-WRITE, DEAD-ENDING VALUE** — `MaxRadius` read only by a client display, on the save-unsafe `ColonyHexMapDB` (landmine L12). Formula is `ceil(sqrt(officeSpace/100))` (census said `/100`) |
| AdminLevel (seat scope) | all `SeatType` readers = `AdminWindow.cs:172,210` (display) + 1 test; rules match by ComponentName | **DEAD-END** — zero rule consumers (C8) |
| Console Space (ship bridge) | sole reader gated on `ColonyInfoDB` (`ColonyHexMapProcessor.cs:17`) a ship lacks | **DEAD-END** — 1-console and 20-console bridge both yield one seat |
| Scope×Domain proposal | no 4-scope enum / 8-domain widen exists | **PENDING** (structure) — but its Grade/Support/Survivability dials **are** built & live |

### ⚠ Enhancers — one output has no writer (matrix said live)
| Output | Verified reader (file:line) | State |
|---|---|---|
| Firepower/Toughness Caliber | `ShipCombatValueDB.cs:529-530` (firepower/toughness ×mult), best-installed | **LIVE** |
| TrainingMultiplier | baked at raise into Attack+Health `GroundForcesDB.cs:607-611` | **LIVE** (then a readout only) |
| StrengthBonus (carry) | `GroundUnitAssembly.cs:105-107` (capacity += ) | **LIVE** (bootstrap: adds while itemMass spends) |
| EvasionBonus | `GroundUnitAssembly.cs:189` → design.Evasion | **LIVE** |
| Shield + regen | `GroundUnitAssembly.cs:190-196` (Shield ADDs; regen weight-averaged) | **LIVE** |
| Crew Reduction | `ShipDesign.cs:242-260` lowers CrewReq (clamped ≥ TalentReq) → manpool | **LIVE** (indirect via CrewReq) |
| **SwitchableAfter cut (iface)** | `SwitchableAfter` set only from blueprint's fixed `CooldownSeconds`; **no InterfaceAtb exists** (grep zero) | ⚠ **DEAD-END** — matrix said "live"; the cooldown machinery is live but **no enhancer writes a cut** |
| Self-repair rate (fieldrep) | grep `fieldrep`/`SelfRepair`/`RepairRate` = zero | **PENDING** (whole-or-dead, no damaged state) |
| Morale / fury | grep GroundCombat morale/fury/rout = comment only; no field | **PENDING** (no unit-combat morale field) |

### ✅ Industrial — economy fully read; one dead output
| Output | Verified reader (file:line) | State |
|---|---|---|
| Industry rate (5 types) | `IndustryTools.ConstructStuff` `:120,135,201,214`; all 5 types both emitted & consumed | **LIVE** (refining/component/installation/ordnance/ship-assembly all process) |
| ResearchPoints + bonusCategory | `ResearchProcessor.cs:97`; bonus `:77` | **LIVE** |
| **Cost Per Day** | write `ResearchPointsAtbDB.cs:71` → read `ResearchProcessor.cs:107`, pays `:114-118` | ⚠ **LIVE** — HTML/census still say "dead" (grep-blind; matrix already corrected) |
| Mineral output | `MineResourcesProcessor.cs:60,80,90,100` (rate → infra-scale → cargo → deplete) | **LIVE** |
| CrewReq | `InfrastructureProcessor.SumRequiredCapacity:95` (infra demand) | ⚠ **LIVE** — but via infra-demand, **not** the ManpowerTools pool (that's ship-only) |
| Support Capacity | `InfrastructureProcessor.cs:75` → Efficiency → scales production `:115` + mining `:64` | **LIVE** (owned by Industrial, not Civic — confirmed) |
| Fortification (LocalFortify/Adjacent) | `GroundDefenseAtb.cs:30,32` → `GroundFortification.DefenseMult` → `GroundForcesProcessor.cs:377` | **LIVE** (Adjacent scheduled for M10 deletion, but live as-built) |
| **Fighter Construction Points** | property writes `0`; absent from the rate table; no `fighter-construction` industry type exists (only 5) | **DEAD-END** (C5 confirmed) |

### ✅ Power — the cleanest door; only the RTG-law pending
| Output | Verified reader (file:line) | State |
|---|---|---|
| TotalOutputMax | `SustenanceProcessor.cs:60` (→ PowerShortage); `EnergyGenProcessor.cs:47,80` | **LIVE** |
| PowerOutputMax (per-component) | `WeaponSupply.cs:104` → ground hard power gate (draw>supply→Invalid) | **LIVE** (distinct field from TotalOutputMax) |
| → warp (via EnergyStored) | `WarpMoveCommand.cs:265-268` reads `EnergyStored`, charged from output `EnergyGenProcessor.cs:64` | **LIVE (indirect)** |
| Lifetime → LocalFuel | burn `EnergyGenProcessor.cs:86`; gate `:42-44` | **LIVE** (`EnableFuelExhaustion` defaults **off** `:32`) |
| EnergyStoreMax | `EnergyStoreAtb.cs:37`; caps `EnergyStored` `:56` → warp | **LIVE (indirect)** — confirmed defect: reactor adds **kW into a kJ store** `EnergyGenerationAtb.cs:65,70` |
| SensorSignatureAtb (1700 K) | `SetProfileDB:56` → `SensorProfileTools.cs:25` → detection | **LIVE** |
| CrewReq / ResourceCost (fissile) | template `energy.json:12,21` → manpower / industry | **LIVE** |
| Reactor RTG-law (`fix` checkbox) | `energy.json:53` = `50*[Mass]*OvE`, **linear**; `fix` exists only in the HTML prototype | **PENDING** (slice P1) |

### ✅ Propulsion — all read; cruise/Δv under-stated as "readout"
| Output | Verified reader (file:line) | State |
|---|---|---|
| Thrust (`NewtonionThrustAtb`) | `CalculateEvasion` `ShipCombatValueDB.cs:581` (accel = thrust/MassDry) | **LIVE** (→evasion; `FleetManeuver` reads evasion, not thrust directly) |
| Exhaust velocity | Tsiolkovsky Δv `WarpMoveProcessor.cs:424`; `CargoTransferProcessor` Δv | **LIVE** |
| Ground SpeedFactor | `GroundMobility.cs:54` → `Speed_kmh` `GroundForcesDB.cs:656` → march | **LIVE** |
| Ground RoughHandling | march `GroundMobility.cs:129`; combat `GroundForcesProcessor.cs:489,522` | **LIVE** (one dial, two systems) |
| Signature (3500 K) | `SetProfileDB:56` → detection scan | **LIVE** (magnitude is static design-time, not runtime `=thrust`) |
| Warp create / sustain (box 1) | gate `WarpMoveCommand.cs:266`; sustain draw `WarpMoveProcessor.cs:246` | **LIVE** (boxes 2–6 pending, correct) |
| Accel · Δv · cruise · evasion-mult | `CalculateEvasion` / `DeltaVFloor` `FleetCombat.cs:74` / `WarpMath.cs:41` (need finished mass) | **EMERGENT** — live once assembled; also drives real transit + kiting clock (census under-states) |
| Signature suppression / quietness / navigator | no field on any propulsion atb (grep zero) | **PENDING** (no dead var written — genuinely unbuilt) |

### ✅ Sensors — all read (4 gated); but the band-match gate has a bug
| Output | Verified reader (file:line) | State |
|---|---|---|
| SensorReceiverAtb band min/max | gate `SensorTools.cs:147` | **LIVE** — ⚠ **C7 BUG** (see below) |
| Threshold_kW (sensitivity) | detection gate `SensorTools.cs:145`; `RangeForSignal:435` | **LIVE** |
| Fire-control range | `ShipCombatValueDB.cs:326` → beam MaxRange | **LIVE (gated** `EnableFireControlRange` default off**)** |
| Fire-control tracking speed | `ShipCombatValueDB.cs:310` → beam Tracking | **LIVE (gated** `EnableFireControlTracking`**)** |
| Cloak signature multiplier | `EmconActivityProcessor.cs:118` → scales emitted signature | **LIVE (ungated; 1.0 when absent)** |
| Jammer barrage strength | `JammingDivisorAgainst:96` → `SensorTools.cs:26` | **LIVE (gated** `EnableJamming`**)** — direct reader is detection, not the resolver |
| Jammer reach | `JammingDivisorAgainst:96` hard cutoff | **LIVE (gated)** |
| Survey speed | `GeoSurveyProcessor.cs:101` → progress `:90` | **LIVE** |
| Jammer self-noise **coupling** | self-noise dial is live (`EmconActivityProcessor.cs:119`) but is an independent arg, **not** derived from degrade | **PENDING** (the coupling; the dial itself is live) |

**C7 band-match bug (confirmed in engine, not just the HTML):** the detection gate at `SensorTools.cs:147` reads
the receiver's *lower* edge but **never its upper edge** — the test reduces to `max(rec.min, sig.min) < sig.max`,
so a receiver tuned *below* the signal still passes. Worked case: a visible-light receiver ([550,650] nm) detects
an infrared reactor ([1500,1900] nm). Correct interval-overlap is `max(rec.min,sig.min) < min(rec.max,sig.max)`.
*(Separately, `Resolution` at `SensorTools.cs:125` sits inside a comment block — it has zero live readers, a
confirmed dead field, though it isn't one of the nine outputs.)*

### ⚠ Logistical — 3 of 7 cargo-types feed a *different* component than the matrix named
| Output | Verified reader (file:line) | State |
|---|---|---|
| CargoStorageAtb.MaxVolume | `StorageSpaceProcessor.cs:88` + `CargoMath.GetFreeVolume` | **LIVE** |
| CargoStorageAtb.StoreTypeID (7 types) | keyed lookups `LaunchComplexProcessor.cs:152`, `MissleProcessor.cs:31` | **LIVE** |
| CargoTransferAtb.rate / .range | `StorageSpaceProcessor.cs:77` → `CargoTransferProcessor.cs:49,276,314` | **LIVE** |
| fuel-storage | `NewtonianMovementProcessor.cs:121` (drain) | **LIVE** — a load-bearing cargo-store wire |
| ordnance-storage | `MissleProcessor.cs:31` | **LIVE** — the other load-bearing wire |
| **ammo** (cargo-type) | resolver gate live but reads `ShipMagazineAtb.Capacity_kg` `:29`; the literal "ammo" store has **zero readers** | ⚠ **MISATTRIBUTED** — consumer real, wrong producer |
| **troops** (cargo-type) | `GroundTransport.cs:40` reads `GroundBayAtb`; the "troops" store is **never read** | ⚠ **MISATTRIBUTED** — invasion reads a dedicated bay |
| people / colonists | every `Population` write is a SET; no `+=`, no delivery path (`PassengerPacking.cs:16`) | **PENDING** — no additive path (accurate) |
| strike — store | no strikecraft-bay provider/reader; docking is a separate uncalled system | **PENDING** — "store-live" overstated |
| strike — launch (`ParasiteLauncherReady`) | enum def `EventTypes.cs:400` only; zero emitters/consumers | **DEAD-END** — event defined, never fired |
| **LogiBaseAtb.LogisicCapacity** | zero **engine** readers; but **client** gate `ColonyLogisticsDisplay.cs:215` caps items a colony can list | ⚠ **ENGINE-DEAD / CLIENT-LIVE** — crack C2 "totally dead" is wrong |

*Only **fuel** and **ordnance** are load-bearing live cargo-store→consumer wires. The other three typed stores
(ammo, troops, strike) are shadowed by **parallel dedicated components** (`ShipMagazineAtb`, `GroundBayAtb`,
docking) that re-invent single-type storage — the census's own "four parallel stores" note.*

### ⏳ Aura — proposed 12th door; nothing built
`grep [Aa]ura` across the whole GameEngine returns **zero files.** No `AuraAtb`, no aura-pass processor, no aura
variable. Every output (effect magnitude, radius, target/IFF, the jamming detection-penalty) is **PENDING** — the
target primitive (a per-tick neighbour sweep) does not exist. Even the one effect whose target variable *does*
exist (jamming → detection range) has no pass to apply it.

---

## THE OUTPUT RESIDUE — every dial that currently does nothing

This is the output-side of the "17 unconsumed materials." Two buckets: **dead ends** (written, unread — a wasted
dial today) and **pending** (no engine target yet — a dial waiting on a build).

### Dead ends (written, zero gameplay readers) — 6 + edge cases
| Dead output | Door | Why it's dead | Fix shape |
|---|---|---|---|
| **AdminLevel** (seat scope) | Command | rules match seats by ComponentName, never by level; printed only | give a rule that reads seat scope, or drop the ladder to a label |
| **Console Space** (ship bridge) | Command | its only reader is gated on `ColonyInfoDB`, which ships lack | make a ship-side reader, or make the bridge grant seats by console count |
| **Fighter Construction Points** | Industrial | no `fighter-construction` industry type exists (only 5) | add the 6th industry type, or delete the dial |
| **SwitchableAfter cut (iface)** | Enhancers | cooldown is a fixed per-blueprint constant; **no enhancer component writes a cut** | build the `InterfaceAtb` that actually reduces the cooldown |
| **ParasiteLauncherReady** (strike launch) | Logistical | event defined, zero emitters/consumers | build the strike-launch caller |
| **Office-Space → hex radius** | Command | writes to the save-unsafe `ColonyHexMapDB`; read only by a client display, no engine rule | move the radius onto the live blob and give it a rule |
| *edge:* `Combatant.ShieldCapacity` | Defense | neutral kernel field; `ToCombatant` has zero callers | — (kernel plumbing, not a door dial) |
| *edge:* `Resolution` | Sensors | sits inside a comment block | uncomment + wire, or delete |
| *half:* `LogisicCapacity` | Logistical | zero **engine** readers; but a **live client gate** | wire the engine bidding to read it (client already does) |

### Pending (no engine variable yet) — the designed-but-unbuilt dials
| Pending output | Door | Waits on |
|---|---|---|
| Employment **producer** (C1) | Civic | a component that writes `EmploymentAtbDB.Jobs` (consumer is live; term = 0 until then) |
| Medical health · Security legitimacy | Civic | a 7th morale input / a security-building reader in `LegitimacyProcessor` |
| Structural efficiency (√-law) · Environment tag | Chassis | the research axis field / an environment tag on the chassis atb |
| Self-repair rate · Morale / fury | Enhancers | a damaged-state / a unit-combat morale field (both whole-or-dead today) |
| Reactor RTG-law | Power | the output×lifetime coupling (slice P1) |
| Signature suppression / quietness / navigator | Propulsion | the suppression fields + the FTL→berth wire |
| Jammer self-noise coupling | Sensors | derive self-noise from degrade (one dial, not two) |
| Colonist → population add · Strike store & launch | Logistical | an additive population path / a strike-bay component + launcher |
| All 4 Aura outputs | Aura | the aura-pass processor (nothing built) |

---

## CORRECTIONS TO THE MATRIX (Table A) — what the source verification changed

The inputs pass found 3 undefined minerals to fix. The outputs pass found no new *crashes* — but it found
**mislabels**: outputs the matrix called one thing that the source shows are another. These should be folded back
into `02-IO-MATRIX.md` Table A and the affected censuses.

| # | Matrix said | Source shows | Action |
|---|---|---|---|
| 1 | Enhancers **iface / SwitchableAfter = live** | no enhancer component writes the cut; cooldown is a fixed blueprint constant | **re-mark iface DEAD-END/PENDING** (of 7 "live" enhancer templates, only 6 are live) |
| 2 | Defense **Shield/regen → `CombatKernel.ResolveShield` (ship)** | drained/regenerated **inline in the ground resolver**; the ship kernel belongs to a separate `ShieldAtb` | **rename the consumer to the ground inline path** |
| 3 | Logistical **ammo → dry-magazine gate** | gate reads `ShipMagazineAtb`; the "ammo" cargo store has zero readers | **note the parallel component; the typed store is unconsumed** |
| 4 | Logistical **troops → GroundTransport** | reads `GroundBayAtb`; the "troops" cargo store is never read | **same — dedicated bay, not the store** |
| 5 | Logistical **LogiBaseAtb = dead (C2)** | zero **engine** readers but a **live client gate** | **re-mark "engine-dead / client-live"** |
| 6 | Weapons **runs-on forced by Delivery** | engine derives ammo-vs-power from **Nature**; power/none have no reader | **note the axis mismatch (a Bolt+Energy weapon labeled "ammo" drains none)** |
| 7 | Chassis **Structure → Defense (chassis is the source)** | true for **ground** only; the **ship hull has no HP field** | **qualify to ground-only** |
| 8 | Chassis **Environment tags → yard domain gate** | no environment field exists on any chassis atb | **re-mark PENDING (no engine tag)** |
| 9 | Command **"Scope×Domain: none of this exists"** | the Grade/Support/Survivability **dials are built and live**; only the framing is pending | **narrow "pending" to the reorg, not the dials** |
| 10 | Command radius formula `ceil(officeSpace/100)` | actual `ceil(sqrt(officeSpace/100))` | **fix the formula in the census** |
| 11 | Weapons: heat is a "computed readout, no engine variable" | `HeatPerSecond` is a **live field with a live reader**; the door just doesn't emit it | **add HeatPerSecond as an 11th weapon output** |
| 12 | Industrial **CrewReq → ManpowerTools pool** | for **installations** it feeds infra-demand; the pool is **ship-only** | **split the consumer by host** |
| 13 | Industrial **Cost Per Day = dead** (HTML/census) | live (`ResearchProcessor.cs:107` pays it) | **already fixed in the matrix; fix the HTML/census prose** |

**None of these is a crash or a data break** — every mislabeled output still has a real reader (or is honestly
pending). The corrections are about *which* reader, and about not counting a client-only or ground-only reader as
a universal one. That distinction is exactly what the Assembler needs to wire the right consumer to the right
output for the right host.

---

## THE ONE-SCREEN SUMMARY

- **The inputs are all provided; the outputs are mostly read — but not uniformly.** Weapons, Power, Industrial,
  Propulsion, Sensors, and Defense have every output wired to a live reader. That's the combat-and-economy core,
  and it's real.
- **Six outputs are true dead ends** (AdminLevel, ship-bridge Console Space, Fighter Construction Points, the
  iface cooldown-cut, strike launch, and the Command hex-radius) — dials that currently do nothing. About a dozen
  more are **pending** (designed, no engine target yet). None blocks the MVP.
- **The load-bearing finding is the host split.** A weapon's penetration / per-shot / rate and a defense
  component's shields are **live on a ground unit and inert on a ship** (the two resolvers read different subsets).
  The Assembler must wire consumers **per host**, not once for all.
- **Crack C1 is the one that silently costs gameplay:** the employment morale term is fully wired on the reader
  side and contributes **zero forever** because nothing writes `EmploymentAtbDB.Jobs`. A missing producer, not a
  missing consumer — the exact inverse of a dead output.

*Verified 2026-08-02 by twelve per-door source traces (one per door; Aura by hand) against `Pulsar4X/GameEngine/`.
Companion to `05-MATERIAL-INPUTS-BY-DOOR.md` (supply side) and `02-IO-MATRIX.md` Table A (the baseline this
re-verifies).*
