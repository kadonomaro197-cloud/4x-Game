# The Component Designer — DIAL BUILD LEDGER (what's actually WIRED)

**As of:** 2026-07-12 · the honest **code** companion to `docs/economy/COMPONENT-DESIGNER-DIALS.md` (the design spec).

## Why this doc exists

`docs/economy/COMPONENT-DESIGNER-DIALS.md` is a **design** spec. Every one of its 37 doors is stamped **🔒 LOCKED**, which means *the design decision is made* — **it does NOT mean the code is built.** That one word does a lot of damage: 579 KB of uniform 🔒 reads as "the component designer is finished," when in truth the design is finished and the **code is a fraction of it**. This ledger measures the axis the spec doesn't: **what actually moves a number the simulation reads.**

**The test for "wired" (all three rungs, verified in code — file:line, never trusted from the spec):**
1. an attribute field on the door's `*Atb` class carries the dial,
2. a **resolver/processor reads** the derived stat (not just stores it), and
3. a **test** proves it.

A dial that has the field but no resolver read is **⚫ design-only / dead knob**, no matter what the spec says. This ledger was built from a 7-agent code audit (1 door-level + 6 dial-level) against the live engine.

> **Read `docs/economy/COMPONENT-DESIGNER-DIALS.md` for the *design* of each dial; read THIS for whether it's *built*, and if not, the exact wire+test to build it.**

---

## Two-axis truth — DESIGN vs CODE

| Symbol | Meaning |
|--------|---------|
| 🔒 | **Design** locked (the spec's axis) |
| 🟢 BUILT | **Code**: the door has ≥1 fully-wired dial (atb + resolver read + test) |
| 🟡 PARTIAL | **Code**: some rung exists but it's ground-only / bypasses the designer / a stored-but-unread field |
| ⚫ DESIGN-ONLY | **Code**: no meaningful code — spec only |
| `(n/m)` | of the spec's `m` dials for this door, `n` are fully wired |

### Door-level summary (37 doors)

| # | Door | Design | Code | Dials wired |
|---|------|:------:|:----:|-------------|
| 1 | Weapons ▸ Energy | 🔒 | 🟢 BUILT | 8/17 |
| 2 | Weapons ▸ Ballistic | 🔒 | 🟢 BUILT | 7/11 |
| 3 | Weapons ▸ Melee | 🔒 | 🟡 PARTIAL | folded into ground weapons; no distinct door |
| 4 | Weapons ▸ Guided | 🔒 | 🟢 BUILT | 4/9 (warhead is a **stub** — see below) |
| 5 | Weapons ▸ Exotic | 🔒 | ⚫ DESIGN-ONLY | 0 (SWAY/bioweapon) |
| 6 | Propulsion ▸ Reaction | 🔒 | 🟢 BUILT | 4/4 ✅ full |
| 7 | Propulsion ▸ Traction | 🔒 | 🟢 BUILT | 3/6 |
| 8 | Propulsion ▸ Fluid | 🔒 | ⚫ DESIGN-ONLY | 0 |
| 9 | Propulsion ▸ Warp | 🔒 | 🟢 BUILT | 5/6 |
| 10 | Propulsion ▸ Exotic | 🔒 | 🟢 BUILT | 2/4 (2 deferred subsystems) |
| 11 | Sensors ▸ Detection | 🔒 | 🟢 BUILT | 4/5 |
| 12 | Sensors ▸ Survey | 🔒 | 🟢 BUILT | 3/10 |
| 13 | Sensors ▸ Fire Control | 🔒 | 🟢 BUILT | 3/4 |
| 14 | Sensors ▸ Electronic Warfare | 🔒 | 🟢 BUILT | 2/5 |
| 15 | Power ▸ Generation | 🔒 | 🟢 BUILT | 6/9 (reactor-heat→EMCON corrected to built 2026-07-12) |
| 16 | Power ▸ Storage | 🔒 | 🟢 BUILT | 2/5 |
| 17 | Defense ▸ Armor | 🔒 | 🟢 BUILT | 3/5 (flat-per-source soak DEAD on ships) |
| 18 | Defense ▸ Shields | 🔒 | 🟢 BUILT | 3/5 |
| 19 | Defense ▸ Hardening | 🔒 | 🟢 BUILT | 2/5 (hazard-side; correctly no combat read) |
| 20 | Defense ▸ Fortification | 🔒 | 🟡 PARTIAL | ground-only; no ship/station dial |
| 21 | Enhancers ▸ Bio-augmentation | 🔒 | 🟡 PARTIAL | ground-only (`GroundAugmentAtb`) |
| 22 | Enhancers ▸ Training / Caliber | 🔒 | 🟢 BUILT | 3/3 ✅ full |
| 23 | Enhancers ▸ Systems | 🔒 | 🟡 PARTIAL | one dial (`CrewAutomationAtb`) |
| 24 | Industrial ▸ Extraction | 🔒 | 🟢 BUILT | 4/8 |
| 25 | Industrial ▸ Fabrication | 🔒 | 🟢 BUILT | 6/11 (refining WORKS — spec is stale) |
| 26 | Logistical ▸ Storage | 🔒 | 🟢 BUILT | 3/6 |
| 27 | Logistical ▸ Transfer | 🔒 | 🟢 BUILT | 3/10 |
| 28 | Civic ▸ Habitation | 🔒 | 🟢 BUILT | 4/6 (sustenance inert, employment data-dead) |
| 29 | Civic ▸ Development | 🔒 | 🟡 PARTIAL | research wired; academy/terraform unbuilt |
| 30 | Command ▸ Command | 🔒 | 🟡 PARTIAL | seat substrate only; consequences stub |
| 31 | Chassis ▸ Personnel | 🔒 | 🟢 BUILT | 4/5 |
| 32 | Chassis ▸ Vehicle | 🔒 | 🟢 BUILT | 3/5 |
| 33 | Chassis ▸ Hull (ship) | 🔒 | 🟢 BUILT | 1/4 (mass-budget wired + enforced client-side — CORRECTED 2026-07-12; hardpoint/hull-HP are the extension) |
| 34 | Chassis ▸ Structure (station) | 🔒 | 🟡 PARTIAL | station entity exists; no designer hull dial |
| 35 | Chassis ▸ Mega | 🔒 | ⚫ DESIGN-ONLY | 0 |
| 36 | Command ▸ Relay (C3) — "NEW DOOR" | 🔒 | ⚫ DESIGN-ONLY | 0 (marked "wired alongside" — absent) |
| 37 | Route Works (gate/wormhole) — "NEW" | 🔒 | ⚫ DESIGN-ONLY | 0 (dead stub `JPFactory.CreateConnection:124`) |

**Tally:** 25 BUILT · 7 PARTIAL · 5 DESIGN-ONLY. But at the **dial** level the "BUILT" doors are far thinner than the badge implies — Survey 3/10, Transfer 3/10, Guided 4/9, Storage 2/5, Hull 1/4. **Door-built ≠ dial-set-built.**

---

## The headline — the single highest-value wire

**Ship armour-penetration / flat-per-source soak is BUILT + tested on the ground, DEAD in space.** `WeaponProfile.Penetration` (`WeaponProfile.cs:108`) and `PerShotEnergy` (`:118`) exist and are read by the **ground** resolver (`GroundForcesProcessor.cs:363-368` → `CombatKernel.BurstShotCount`/`ArmourSoakBurst`), and `Combatant.Armour` + the swarm-bounce math live in the shared `CombatKernel`. But **every ship weapon passes `0`**, and the space salvo (`CombatEngagement.FleetArmourSoakFraction`, `:705,1298`) reads nature-soak only — never penetration or per-shot alpha. So on ships:
- "many small hits bounce, one alpha punches through flat armour" — **doesn't happen**,
- lance-focus (beam), sabot/AP (railgun), and shaped-warhead (missile) — **all inert**.

**One wire** — a per-salvo `CombatKernel.ArmourSoakBurst` call in `StepEngagementGroup` reading `w.Penetration`/`w.PerShotEnergy`, fed from the ship armour value — lights up armour-vs-alpha across **all three ship weapon doors at once**, reusing code already CI-green on the ground. This is the top of the wire-plan below.

---

## Dial-level truth — per BUILT door

Only ⚫ unwired dials carry a wire+test plan; ✅ rows are done. Evidence is abbreviated file:line (full detail in the audit transcripts).

### Weapons ▸ Energy (8/17) — `GenericBeamWeaponAtb` / `PlasmaBoltWeaponAtb` / `DisruptorWeaponAtb`
✅ Output×Rate→dps · Tracking · Delivery(beam/bolt) · Nature(thermal/ion/exotic) · Range(S9) · Cooling/Heat(`ShipHeatTests`) · Charge-profile(native) · Point-defense(`ShipPointDefenseTests`)
⚫ **Penetration** (field exists, ship passes 0) → add to beam/plasma atb+JSON, read via the headline `ArmourSoakBurst` wire; test: lance beam cracks armour a plain beam bounces off.
⚫ **Per-shot alpha (`PerShotEnergy`)** → same wire; test: one big-alpha beam punches flat armour a repeater of equal dps chips off.
⚫ **Focus (lance↔cone)** → `SpreadFactor` feeding `Saturation`+`Penetration`; test: wide cone floors a swarm's dodge, lance concentrates.
⚫ Thermal-bloom→signature (detection-side, out of combat scope) · ⚫ deferred: charge-telegraph, overcharge/burnout, frequency-modulation, medium.

### Weapons ▸ Ballistic (7/11) — `RailgunWeaponAtb` / `FlakWeaponAtb`
✅ Muzzle-velocity→dodge(S10/S14) · Nature+saturation(flak S12) · Rate/caliber · Fuzing/airburst · Recoil→tracking(`ShipRecoilTests`) · Ammo runs-dry(`ShipAmmoTests`) · Power(build-side)
⚫ **Sabot/AP penetration** → `Penetration` on `RailgunWeaponAtb`+JSON, headline wire; test: sabot cracks armour a slug bounces off.
⚫ **Multi-ammo switch (AP/HE/flak)** → hold N `WeaponProfile`s per launcher, swap active; test: switch mid-fight changes the matchup.
⚫ **Recoilless bleed** → `RecoillessBleed` cutting `Range`/`Penetration`; test: recoilless variant shorter-reach/weaker-pen. · ⚫ deferred: indirect/arcing (needs LOS).

### Weapons ▸ Guided (4/9) — `MissileLauncherAtb` / `OrdnanceDesign` / `PointDefenseAtb`
✅ PD intercepts a missile(`ShipPointDefenseTests`) · Salvo saturates PD · Projectile mini-assembly(design-side) · Ammo runs-dry
⚫ **Warhead output · seeker tracking · range** — resolver reads FIXED stubs (`MissileLauncherFirepowerStub 100k`, `MissileVelocityStub`, `MissileTrackingStub`, `MissileRange_m`, `ShipCombatValueDB.cs:429-431`); the real `OrdnanceDesign` warhead is **not read**. → read `AssignedOrdnance` warhead J + seeker + engine velocity into the missile `WeaponProfile`; test: a torpedo out-rates an interceptor; a homing seeker tracks where unguided misses. **(This is the Guided equivalent of the headline — the ordnance system is real but disconnected from the resolver.)**
⚫ **Warhead type→Nature** (hardcoded Explosive) → map payload→`Nature` (shaped→Penetration, nuke→Exotic); test: shaped cracks armour, HE splashes soft.
⚫ **Sprint vs cruise** → flight-time vs PD window into intercept fraction. · ⚫ Recoverable drone (H6/carriers) · ⚫ deferred: seeker jamming (needs EW).

### Propulsion ▸ Reaction (4/4 ✅ full) — `NewtonionThrustAtb`
✅ Thrust-class · Exhaust-velocity/Isp · Fuel-type/burn-rate · Drive-mass. Fully wired, spec-accurate.

### Propulsion ▸ Traction (3/6) — `GroundLocomotionAtb` *(spec CLAIMS ✅ on 3 dead dials — see corrections)*
✅ SpeedFactor · RoughHandling · Terrain-combat-bonus
⚫ **Amphibious** (field+JSON exist; `HexPathfinder.IsImpassable:42` ignores it — ocean stays a wall) → thread the marching unit's `Amphibious` into `FindPath`; test: amphibious unit paths across ocean a land unit can't.
⚫ **Motive power** (no power-draw field on the drive) → `PowerDraw_W` on `GroundLocomotionAtb` into the assembly power gate; test: hover drive needs a reactor.
⚫ **Drive-mass→speed feedback** (`GroundMobility` reads SpeedFactor only) → divide march-time by mass/refMass; test: heavier drive, equal SpeedFactor, marches slower.

### Propulsion ▸ Warp (5/6) — `WarpDriveAtb`
✅ FTL method · Warp-speed · Bubble power · Drive-mass · Fleet-coupling
⚫ **Gate-user/network node** (H8) — `JPFactory.CreateConnection:124` is a dead stub → revive it + a gate atb + addressing; whole-subsystem, correctly deferred.

### Propulsion ▸ Exotic (2/4) — `ReactionlessThrustAtb` / `InertialessDriveAtb`
✅ Reactionless-thrust(`ShipReactionlessDriveTests`) · Inertialess-maneuver(`ShipInertialessDriveTests`)
⚫ Gravitic/medium-independent (needs medium layer) · ⚫ Teleport (H1, needs Transfer-teleport) — both correctly deferred subsystems.

### Sensors ▸ Detection (4/5) — `SensorReceiverAtb`
✅ Sensitivity/reach · Waveform-band · Active↔passive(EMCON) · Refresh-rate
⚫ **Resolution** (field stored `:30`, sole read is in a commented-out block) — spec design-CUT this (`SignalQuality`); leave deferred / re-home to the Information Ledger, not detection.

### Sensors ▸ Survey (3/10) — `GeoSurveyAtb` / `GravSurveyAtb`
✅ Geological · Gravitational (no test) · Survey-speed
⚫ **Survey depth/resolution** → `Resolution` field gating reveal detail (tonnage vs "present"); test: deep survey reveals sizes.
⚫ **Reach/access** (hardcoded `100000` literal `JPSurveyProcessor.cs:47 // FIXME`) → `Range_m` on the atb; test: long-reach rig surveys from >100 km.
⚫ Stellar/Atmospheric/Biosphere/Hazard modes (data exists in `SystemBodyInfoDB`/`Hazards`, expose as survey results) · ⚫ Anomaly/Xenoarchaeology (net-new field-site loop) · ⚫ Data-center, Probe-autonomy (data items / defer).
◆ *test gap:* gravitational `Speed` is wired but has **no gauge** — add `JPSurveyTests`.

### Sensors ▸ Fire Control (3/4) — `BeamFireControlAtbDB` *(spec prose calls these "dead knobs" — STALE, see corrections)*
✅ Tracking-speed(`ShipFireControlTests`) · Fire-control-range(`ShipFireControlRangeTests`) · PD-only allocation(`ShipFireControlPDTests`) — all live behind client-on flags.
⚫ **Multi-target (targets-tracked)** (no field; fire-split uses raw enemy count) → `TargetsTracked` on the atb capping simultaneous targets; test: a multi-target director spreads fire where a single-target one over-kills.

### Sensors ▸ Electronic Warfare (2/5) — `JammerAtb` / `CloakAtb`
✅ Barrage-jamming · Cloak/signature-damping
⚫ Targeted jamming (v1 barrage-only) · ⚫ Spoofing/decoys (inject false `SensorContact`) · ⚫ Counter-EW/ECCM (resist term) — all spec-deferred; decoy is the cheapest (a `DecoyAtb` adding a phantom contact; test: enemy track table gains a ghost).

### Power ▸ Generation (5/9) — `EnergyGenerationAtb` / `EnergySolarGenerationAtb`
✅ Fission/fusion output · Solar (no test) · Output-level · Fuel/lifetime · Load/baseload
✅ **Reactor signature/heat→EMCON** — *CORRECTED 2026-07-12: already BUILT (gated).* `EmconActivityProcessor.cs:115` reads reactor `Load` into `ActivityMultiplier` behind `EnableReactorHeat`; gauged by `ReactorHeatTests`. Only the client flag-flip remains (this row previously read "⚫ hook flagged, unread").
⚫ RTG fuel-free framing (`IsFuelFree` flag) · ⚫ Antimatter/exotic (needs containment) · ⚫ Safety/containment breach (new meltdown rule).
◆ *test gap:* solar generation wired but no unit test — add `SolarGenTests`.

### Power ▸ Storage (2/5) — `EnergyStoreAtb`
✅ Capacity(warp/fire gate) · Charge-behaviour
⚫ **Burst/discharge rate (capacitor≠battery)** — no discharge field; battery & capacitor differ only by size → `MaxDischarge_KJps` gating spike draws; test: capacitor dumps an alpha in one step, battery can't.
⚫ **Charge rate (volley cadence)** → `ChargeRate_KJps` capping refill; test: fast-charge cell refills between volleys. · ⚫ Safety/breach (shared containment).

### Defense ▸ Armor (3/5) — material-driven + `ArmourHardeningAtb`
✅ Thickness/HP→Toughness · **Material/nature-resistance** (`ArmourHardeningAtb.SoakVs*`, wired both domains 2026-07-11) · Mass→evasion
⚫ **Flat-per-source hardness (`Combatant.Armour`) — DEAD on ship** (kernel math exists, unfed) → **the headline wire**; test: a ship bounces a swarm, one alpha punches through.
⚫ Coverage (belt vs all-round) — positional, aggregate resolver can't; defer.

### Defense ▸ Shields (3/5) — `ShieldAtb`
✅ Capacity→pool · Regen · Grave-rung(dead generator drops pool)
⚫ **Nature tuning** (matchup is a fixed engine table, not per-component) → per-nature soak fields mirroring `ArmourHardeningAtb`; test: energy-hardened shield closes the 0.5 energy bleed.
⚫ **Per-ship coverage** (`Combatant.ShieldPool` field exists, unfed per-ship; space pool is per-fleet) → seed per-ship in resolve; test: a focused strike drops one ship's shield while fleetmates hold.

### Defense ▸ Hardening (2/5) — `HazardResistanceAtb` *(correctly hazard-side, NOT combat)*
✅ Resisted-effect · Resistance-fraction (consumer = hazards/attrition per §0f, not the salvo)
⚫ Redundancy/damage-control (needs degraded-condition model H2) · ⚫ Cultural-insulation (belief-resist, needs `ExternalBeliefPressure`) · ⚫ Radiation/EMP crew-survival (needs crew/system-integrity).

### Industrial ▸ Extraction (4/8) — `MineResourcesAtbDB`
✅ Extraction-rate · Depletion(cubic) · Colony-bonus · Automine
◐ Host colony↔station (works, no station-mining test) · ◐ Per-mineral focus (field is per-mineral, templates fill flat)
⚫ **Gas/atmosphere skimmer** (no harvest atb) → `GasHarvestAtbDB` pulling sorium from gas-giant `AtmosphereDB`; test: skimmer adds fuel. · ⚫ Per-hex deposit as source-of-truth (hex seeds are view-only) → wire per-hex deplete into `MineResourcesProcessor`.

### Industrial ▸ Fabrication (6/11) — `IndustryAtb` / `LocalConstructionAtb`
✅ Industry-routing · Throughput×infra · Resource-consumption · Specialization · Host colony↔station · **Refining (mineral→material)** — *spec says "broken", code WORKS (`EconomyReadoutTests.cs:100`, re-enabled 2026-07-10)*
⚫ **Unit-assembly type (tanks/walkers)** (no `unit-assembly` IndustryTypeID) → add the type + a foundry template; test: vehicle-foundry builds a tank via its own line.
⚫ **Assembly-bay size gate (`MaxVolume`)** — stored (`IndustryAbilityDB.cs:15`), **never read** → compare vs design mass in `ConstructStuff`; test: light bay refuses a capital.
⚫ Repair/refit (needs condition model) · ⚫ Recycle/scrap (material reclaim) · ◐ dual construction paths (unify `IndustryAtb`+`LocalConstructionAtb`).

### Logistical ▸ Storage (3/6) — `CargoStorageAtb`
✅ Cargo-type+volume · Fuel→Δv · Ground-magazine
◐ Ship ordnance-hold (cargo type defined, no template) · ◐ bare-hold silent no-op (guard needed)
⚫ **Mothball/reserve** — **blocked: there is NO ship/army upkeep clock** (only `StationUpkeep`) → two-part: (1) a ship/unit upkeep clock mirroring `StationUpkeepProcessor` + new `TransactionCategory`; (2) a stored-flag discount + reactivation delay. *The missing upkeep clock is an economic-pressure system worth building on its own.*

### Logistical ▸ Transfer (3/10) — `CargoTransferAtb` / `LogiBaseAtb`
✅ Transfer-rate+range · Escrow+Δv-gate · Faction-access-gate
◐ Spaceport (untested) · ◐ Launch-complex (untested) · ◐ Automated supply network (only the faction-gate tested) · ◐ `LogiBaseDB.Clone` drops `DesiredLevels`+`ItemsInTransit` (save-bug)
⚫ Tow/grapple (blocked on `SpawnWreck` stub) · ⚫ Covert-insertion bay · ⚫ Missile transfer-range (stub `IsInRange=>true`).

### Civic ▸ Habitation (4/6) — `PopulationSupportAtbDB` / `InfrastructureCapacityAtb` / `HousingAtbDB`
✅ Life-support/pop-capacity · Infrastructure→economy-multiplier · Housing→morale · Sealed-habitat
◐ **Sustenance (power/food) — INERT** (`ColonySustenanceDB` demand defaults 0; no food good) → calibrate `SetDemand` + add a food good; test: pop×demand>supply → shortage → morale hit.
◐ **Employment — DATA-DEAD** (`EmploymentAtbDB.Jobs` read by morale, but **no JSON grants Jobs**) → add `Jobs` to work-building templates; test: jobs<pop reads an unemployment debuff.

### Chassis ▸ Personnel (4/5) & Vehicle (3/5) — `GroundChassisAtb` / `GroundLocomotionAtb`
✅ BaseStrength(§0b carry cap, hard) · BaseHP · CarryClass · Locomotion(indirect)
⚫ **Size** (field stored `GroundChassisAtb.cs:43`, **not read** by assembly; no transport bay consumes it) → a Logistical bay resolver summing `Size×count` vs bay `MaxVolume`; test: a larger frame overflows a small bay.
⚫ Walker/Swarm as distinct carry-class (enum has only Personnel/Vehicle) → extend `GroundCarryClass`; test: swarm frame → own bay.

### Chassis ▸ Hull (1/4) — `ShipHullAtb`
✅ Tonnage/volume ceiling (`MassBudget` computed) — **and ENFORCED**: `ShipDesign.EnforceMassBudget` is set `true` in the client (`PulsarMainWindow.cs:139`), every base-mod ship mounts a hull, and `ShipMassBudgetEnforcementTests`/`ShipHullBaseModTests` pass in CI. *(CORRECTED 2026-07-12 — this row previously read "enforcement OFF"; the code moved past that. The remaining work is the hardpoint/hull-HP extension + a calibration tripwire, not "make it bite.")*
⚫ **Hardpoint count** (no field) → `HardpointBudget` + count weapon mounts (mirror `OverMassBudget`); test: N+1 weapons on an N-hardpoint hull → invalid.
⚫ **Hull structural HP** (no `BaseHP`; ground chassis has one) → add + fold into `Toughness`; test: a hull raises ship Toughness. (Re-baselines combat.)
⚫ Size-class (derive from budget bands; gate Mega-only mounts).

### Enhancers ▸ Training / Caliber (3/3 ✅ full) — `UnitCaliberAtb`
✅ FirepowerMult · ToughnessMult · Talent-scarcity draw (`ManpowerTools` TalentPool). Fully wired — the model door for "a dial with a cost."

---

## The wire-plan — ranked (highest value first)

Every item is "wire an existing/near-existing field into a resolver + add a test." Ordered by payoff × cheapness.

| # | Wire | Doors lit | Cost | Test |
|---|------|-----------|------|------|
| 1 | **Ship armour penetration + per-shot alpha** — feed ship `Combatant.Armour` + call `CombatKernel.ArmourSoakBurst`/pen in `StepEngagementGroup` reading `w.Penetration`/`w.PerShotEnergy` | Energy, Ballistic, (Guided) — 3 at once | 1 resolver wire (ground code reused) | swarm bounces, alpha/AP punches through |
| 2 | **De-stub the missile warhead** — read `OrdnanceDesign` warhead J + seeker + velocity into the missile `WeaponProfile` (replace the 4 `Missile*Stub`s) | Guided | medium (map ordnance→profile) | torpedo out-rates interceptor; homing tracks |
| 3 | **Assembly-bay size gate** — read `IndustryAbilityDB.MaxVolume` vs design mass in `ConstructStuff` | Fabrication | 1 comparison | light bay refuses a capital |
| 4 | **Make ship mass-cap bite** — flip/calibrate `EnforceMassBudget` | Hull (§0b at ship scale) | flag + calibrate | over-budget loadout invalid |
| 5 | **Survey reach + depth** — `Range_m` (kill the `100000` FIXME) + `Resolution` on survey atbs | Survey | 2 fields | long-reach rig surveys far; deep reveals sizes |
| 6 | **Traction amphibious** — thread `Amphibious` into `HexPathfinder.FindPath` | Traction | 1 param | amphibious crosses ocean |
| 7 | **Sustenance + employment data** — non-zero `SetDemand` + a food good; `Jobs` on work templates | Habitation | data + calibrate | shortage → morale; unemployment debuff |
| 8 | **Battery vs capacitor** — `MaxDischarge_KJps`/`ChargeRate_KJps` on `EnergyStoreAtb` | Storage | 2 fields + gate | capacitor dumps an alpha, battery can't |
| 9 | **Multi-target fire control** — `TargetsTracked` capping fire-spread | Fire Control | 1 field | director spreads vs over-kills |
| 10 | **Fill test gaps** — `SolarGenTests`, `JPSurveyTests` (wired, ungauged) | Generation, Survey | test-only | close the untested-but-wired holes |

**Bigger, deferred (need a prerequisite subsystem, not a wire):** fleet/army upkeep clock (unblocks mothball) · gas-giant skimmer · reactor/capacitor breach damage · Route Works gate network (H8) · Mega chassis · Fluid/Exotic medium layer · Weapons▸Exotic SWAY/bioweapon.

---

## Spec corrections — where `docs/economy/COMPONENT-DESIGNER-DIALS.md` drifted from code (BOTH directions)

The ledger found the spec wrong in two directions — it over-claims wired dials AND under-claims a working one:

**Over-claims (spec says ✅/wired, code is ⚫ dead):**
- **Traction ▸ Amphibious / Motive-power / Drive-mass-feedback** — §2.2 grades these ✅; the fields exist but **no resolver reads them** (`HexPathfinder.IsImpassable` ignores Amphibious; no power-draw field; `GroundMobility` reads SpeedFactor only). Dead knobs.
- **Command ▸ Relay** and **Route Works** — marked 🔒 LOCKED / "wired alongside" in the Progress table and essence-extension list; **zero code**. Same badge as shipped weapons.

**Under-claims (spec says broken, code WORKS):**
- **Industrial ▸ Refining** — the spec repeatedly calls the refining feed "broken / test quarantined." **Stale:** `EconomyReadoutTests.cs:100` (re-enabled 2026-07-10) asserts the refinery produces Space-Crete over a game-year and passes. The old bug was a storage cap, since fixed. Only *cross-colony* auto-supply is still a gap.

**Stale framing (spec argues against itself):**
- **Fire Control** — §3.3 prose calls Range/TrackingSpeed/FinalFireOnly "dead knobs the resolver doesn't read"; the code reads all three (`ShipCombatValueDB.cs:287,303,319`) behind client-on flags. The later table rows are correct; the section intro is stale.

**Maintenance rule (same discipline as the root CLAUDE.md):** when a dial's code state changes, flip its row here **in the same commit** — this ledger is only worth keeping if it tracks the code, not the intentions.
