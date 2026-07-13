# Designer Audit 03 — Component ABILITIES × HOSTS (the universality matrix)

**Scope:** Every `IComponentDesignAttribute` in the engine — the ABILITY a designed part grants — mapped to the PROCESSOR that reads it and therefore the HOST(S) on which it actually does anything. Then the crucial finding: the **parallel/duplicated abilities** — one real-world capability modelled by two different attributes for two different hosts, which is *why* the designers aren't universal.

> **Dated snapshot — this is the state of the code as of 2026-07-08.** File:line references drift as HEAD moves; re-grep before trusting an exact line number.
>
> Companion to `01-DESIGNER-UIS.md` (mount legality) and `02-DESIGNABLE-TYPES.md` (design classes). Related design docs already circling this problem: `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md`, `docs/combat/WEAPONS-DESIGN.md` (was cited as the since-deleted `WEAPON-UNIFICATION-DESIGN.md`), `docs/ground/GROUND-UNIT-DESIGNER-DESIGN.md` (was cited as the since-deleted `GROUND-UNITS-AS-ENTITIES-DESIGN.md`).

---

## The one-paragraph finding

An ability is real on a host **only if some processor reads that attribute off that host's `ComponentInstancesDB`.** Two separate gates decide universality, and they are *not* the same gate:

1. **The mount flag** (`ComponentMountType`, `Engine/DataStructures/Enums.cs:125`) decides which **designer/host the UI lets you put the part on.** It's a portable `[Flags]` — in principle one part could mount ship+colony+ground.
2. **The processor reader** decides whether the ability **does anything once mounted.** This is the real lock. A processor that iterates only ship entities (or only colonies, or only ground units) makes the ability host-locked *no matter what the mount flag says.*

The generic infrastructure (`ComponentInstancesDB.TryGetComponentsByAttribute<T>`, `Engine/Components/ComponentInstancesDB.cs:46`) is genuinely host-agnostic — it just returns whatever components carry the attribute. But almost every **reader** is written inside a host-specific subsystem, so the same real capability got modelled **twice**: once as a space attribute read by a space processor, once as a ground attribute read by a ground processor. The clearest proof it *doesn't have to be this way* is `EnergyGenerationAtb` — the one attribute a **ground** system reads directly (`GroundCombat/WeaponSupply.cs:104`) *and* a **space** system reads (`Energy/EnergyGenProcessor.cs`). Reactors are universal; almost nothing else is.

---

## How an ability reaches a host (the mechanism, plain English)

Think of each attribute as a rating plate bolted to a part. Two things have to line up for the plate to matter:

- **`OnComponentInstallation(parentEntity, instance)`** (`Engine/Components/IComponentDesignAttribute.cs:15`) fires when the part is installed on ANY entity (`Engine/Entities/Entity.cs:131,168`). Some abilities wire themselves in here (e.g. a solar panel builds an `EnergyGenAbilityDB` on its host — `Energy/EnergySolarGenerationAtb.cs:48`; a sensor schedules its first scan — `Sensors/SensorRecever/SensorReceiverAtb.cs:117`). Most **ground** attributes make this a **no-op** ("inert on install") and are read on-demand instead.
- **A processor** later calls `host.ComponentInstancesDB.TryGetComponentsByAttribute<TAtb>(...)` (or `design.GetAttribute<TAtb>()`). **This is the host lock.** If no processor that iterates *this host type* ever asks for the attribute, the ability is dead weight on that host even though the mount flag allowed it.

So "the designer isn't universal" = "the ability's only reader lives in a subsystem that only iterates one kind of host."

---

## Master table — every attribute, its reader, its hosts

Hosts: **Ship** (incl. Station — stations reuse the ship/colony component framework), **Colony** (`PlanetInstallation`), **Ground** (a `GroundUnit`'s backing entity, units-as-entities Option A), **Missile** (`OrdnanceDesign`). "Read by" cites the load-bearing reader.

| Attribute | File | Ability it grants | Read by (processor:line) | Hosts that USE it | Duplicate-of? |
|---|---|---|---|---|---|
| **GenericBeamWeaponAtb** | `Weapons/WeaponBeam/GenericBeamWeaponAtb.cs:14` | Beam/laser weapon | `Combat/ShipCombatValueDB.cs:175`; `Weapons/WeaponUtils.cs:48`; ground **classifier** `GroundCombat/WeaponSupply.cs:41,76` | Ship; **Ground (partial, P2b)** | ↔ GroundWeaponAtb |
| **RailgunWeaponAtb** | `Weapons/WeaponRailgun/RailgunWeaponAtb.cs:21` | Railgun (EM slug) | `Combat/ShipCombatValueDB.cs:192`; `GroundCombat/WeaponSupply.cs:44,92` | Ship; **Ground (partial)** | ↔ GroundWeaponAtb |
| **FlakWeaponAtb** | `Weapons/WeaponFlak/FlakWeaponAtb.cs:23` | Flak / point-defence | `Combat/ShipCombatValueDB.cs:212`; `GroundCombat/WeaponSupply.cs:45` | Ship; **Ground (partial)** | ↔ GroundWeaponAtb |
| **DisruptorWeaponAtb** | `Weapons/WeaponDisruptor/DisruptorWeaponAtb.cs:24` | Ion/disruptor beam | `Combat/ShipCombatValueDB.cs:229`; `GroundCombat/WeaponSupply.cs:42,82` | Ship; **Ground (partial)** | ↔ GroundWeaponAtb |
| **PlasmaBoltWeaponAtb** | `Weapons/WeaponPlasma/PlasmaBoltWeaponAtb.cs:23` | Plasma bolt | `Combat/ShipCombatValueDB.cs:247`; `GroundCombat/WeaponSupply.cs:43,87` | Ship; **Ground (partial)** | ↔ GroundWeaponAtb |
| **MissileLauncherAtb** | `Weapons/WeaponMissile/MissleLauncherAtb.cs:8` | Launches ordnance | `Combat/ShipCombatValueDB.cs:263`; `Weapons/WeaponGeneric/GenericFiringWeaponsDB.cs:102` | Ship | — |
| **GenericWeaponAtb** | `Weapons/WeaponGeneric/GenericWeaponAtb.cs:7` | Generic firing weapon (rate/mag) | `Weapons/WeaponGeneric/GenericFiringWeaponsDB.cs:89,232`; `WeaponState.cs:52` | Ship | (mag ↔ GroundMagazineAtb) |
| **BeamFireControlAtbDB** | `Weapons/WeaponFireControl/BeamFireControlAtbDB.cs:9` | Fire-control / targeting | `Combat/ShipCombatValueDB.cs:287,303` (TrackingSpeed→beam tracking, Range→reach) + `:150,319` (FinalFireOnly→CIWS/PD) — **wired behind gated flags, default OFF**; the older `FireControlAbilityDB.cs` path is commented out | Ship (wired, gated off) | — |
| **ShieldAtb** | `Combat/ShieldAtb.cs:18` | Energy shield (damage soak) | `Combat/ShipCombatValueDB.cs:276` | Ship | ↔ GroundAugmentAtb.Shield |
| **OrdnancePayloadAtb** | `Weapons/OrdnancePayloadAtb.cs:9` | Missile warhead payload | `Weapons/OrdnanceDesign.cs` (payload roll-up) | Missile | — |
| **ElectronicsSuite** | `Weapons/OrdnancePayloadAtb.cs:86` | Missile guidance/ECM | ordnance design roll-up | Missile | (↔ sensors, loosely) |
| **NewtonionThrustAtb** | `Movement/NewtonMove/NewtonionThrustAtb.cs:8` | Newtonian engine thrust | `Engine/Entities/EntityExtensions.cs:93,120`; `Weapons/OrdnanceDesign.cs:112` | Ship; Missile | ↔ GroundLocomotionAtb / GroundChassisAtb |
| **WarpDriveAtb** | `Movement/WarpMove/WarpDriveAtb.cs:8` | FTL warp drive | `Engine/Components/ComponentInstancesDBExtensions.cs:116` | Ship | ↔ GroundLocomotionAtb (mobility) |
| **SensorReceiverAtb** | `Sensors/SensorRecever/SensorReceiverAtb.cs:10` | RADAR / EM detection | `Sensors/SensorRecever/SensorTools.cs:216`; `SensorScan.cs` | Ship; **Colony/Station** | **↔ GroundSensorAtb** |
| **SensorSignatureAtb** | `Sensors/SensorEmitter/SensorSignatureAtb.cs:7` | EM signature (be-seen) | `Sensors/SensorEmitter/SensorProfileTools.cs:25` | Ship; Colony/Station | — |
| **EnergySolarGenerationAtb** | `Energy/EnergySolarGenerationAtb.cs:12` | Solar power gen | `Energy/EnergySolarGenProcessor.cs:26` (via `EnergyGenAbilityDB`) | Ship; Colony/Station | — |
| **EnergyGenerationAtb** | `Energy/EnergyGenerationAtb.cs:10` | Reactor power gen | `Energy/EnergyGenProcessor.cs:13`; **ground** `GroundCombat/WeaponSupply.cs:104` | **Ship; Colony; Ground** | **(the universal one — no dup)** |
| **EnergyStoreAtb** | `Energy/EnergyStoreAtb.cs:7` | Battery / power storage | `Energy/EnergyGenAbilityDB` (built in `OnComponentInstallation`) | Ship; Colony/Station | (↔ GroundMagazineAtb, loosely — stored throughput) |
| **CargoStorageAtb** | `Storage/CargoStorageAtb.cs:7` | Cargo hold capacity | `Storage/StorageSpaceProcessor.cs:40,93` | Ship; Colony/Station | ↔ GroundBayAtb (carry capacity) |
| **CargoTransferAtb** | `Storage/CargoTransferAtb.cs:9` | Cargo transfer rate | `Storage/StorageSpaceProcessor.cs:66,112` | Ship; Colony/Station | — |
| **CargoAbleTypeDB** | `Storage/CargoableTypeDB.cs:16` | Marks a part as cargo-able (mass/volume) | cargo system (ICargoable) | Any (cargo) | — |
| **MineResourcesAtbDB** | `Industry/MineResourcesAtbDB.cs:9` | Mines minerals | `Industry/MineResourcesProcessor.cs:118` | Colony | — |
| **IndustryAtb** | `Industry/IndustryAtb.cs:11` | Construction/refining points | Industry processor (iterates colony `IndustryAbilityDB`) | Colony/Station | — |
| **LocalConstructionAtb** | `Industry/LocalConstructionAtb.cs:7` | Local build capacity | Industry construction | Colony | — |
| **InfrastructureCapacityAtb** | `Industry/InfrastructureCapacityAtb.cs:13` | Population infrastructure cap | `Industry/InfrastructureProcessor.cs:75` | Colony | — |
| **ResearchPointsAtbDB** | `Tech/ResearchPointsAtbDB.cs:9` | Research lab output | research processor (iterates `ResearchDB`) | Colony/Station | — |
| **NavalAcademyAtb** | `People/NavalAcademyAtb.cs:9` | Trains officers | academy processor | Colony | — |
| **AdminSpaceAtb** | `People/AdminSpaceAtb.cs:22` | Admin/command capacity | `People/AdminSpaceProcessor.cs:34`; `Colonies/ColonyHexMapProcessor.cs:58` | Ship; Colony | — |
| **LogiBaseAtb** | `Logistics/LogiBaseAtb.cs:9` | Logistics base node | `Logistics/LogiBaseAtb.cs:19` | Colony/Station (+ Ship) | — |
| **HousingAtbDB** | `Colonies/HousingAtbDB.cs:18` | Population housing/comfort | `Engine/Components/ComponentInstancesDBExtensions.cs:40` | Colony | — |
| **EmploymentAtbDB** | `Colonies/EmploymentAtbDB.cs:17` | Jobs for population | `Engine/Components/ComponentInstancesDBExtensions.cs:24` | Colony | — |
| **PopulationSupportAtbDB** | `Galaxy/PopulationSupportAtbDB.cs:14` | Life-support pop capacity | `Engine/Components/ComponentInstancesDBExtensions.cs:70` | Colony (habitat Station) | — |
| **GravityToleranceAtb** | `Galaxy/GravityToleranceAtb.cs:12` | Gravity envelope of a habitat | `Industry/InfrastructureProcessor.cs:67`; `ComponentInstancesDBExtensions.cs:62` | Colony | — |
| **PressureToleranceAtb** | `Galaxy/PressureToleranceAtb.cs:13` | Pressure envelope of a habitat | `Industry/InfrastructureProcessor.cs:71`; `ComponentInstancesDBExtensions.cs:66` | Colony | — |
| **HazardResistanceAtb** | `Hazards/HazardResistanceAtb.cs:20` | Survives space hazards | `Hazards/SpaceHazardTools.cs:97` | Ship | ↔ Ground `EnvResistance` (not an atb) |
| **GeoSurveyAtb** | `GeoSurveys/GeoSurveyAtb.cs:8` | Geological survey | GeoSurvey processor | Ship | — |
| **GravSurveyAtb** | `JumpPoints/GravSurveyAtb.cs:9` | Gravitational (jump-point) survey | grav-survey processor | Ship | — |
| **LaunchComplexAtb** | `Ships/LaunchComplexAtb.cs:8` | Launches ships/fighters | `LaunchComplexDB` (build-and-launch) | Ship; Colony | — |
| — **GROUND-ONLY BELOW** — | | | | | |
| **GroundWeaponAtb** | `GroundCombat/GroundWeaponAtb.cs:30` | Ground unit weapon | `GroundCombat/GroundUnitAssembly.cs:89` | Ground | **↔ space weapon atbs** |
| **GroundArmorAtb** | `GroundCombat/GroundArmorAtb.cs:18` | Armour plating (HP + soak) | `GroundCombat/GroundUnitAssembly.cs:97` | Ground | ↔ ship armour (a ship PROPERTY, not an atb) |
| **GroundAugmentAtb** | `GroundCombat/GroundAugmentAtb.cs:26` | Strength / **evasion / shield** / toughness | `GroundCombat/GroundUnitAssembly.cs:73,104` | Ground | Shield ↔ ShieldAtb; evasion ↔ ship dodge |
| **GroundChassisAtb** | `GroundCombat/GroundChassisAtb.cs:36` | Unit frame (carry budget + locomotion class) | `GroundCombat/GroundUnitAssembly.cs:57`; `GroundMobility.cs:58` | Ground | ↔ hull/engine mount |
| **GroundLocomotionAtb** | `GroundCombat/GroundLocomotionAtb.cs:24` | Parametric drive (speed/rough/amphib) | `GroundCombat/GroundMobility.cs:47,77` | Ground | **↔ NewtonionThrustAtb / WarpDriveAtb** |
| **GroundSensorAtb** | `GroundCombat/GroundSensorAtb.cs:21` | RADAR (reveal ground) | `GroundCombat/GroundSensors.cs:69` | Ground | **↔ SensorReceiverAtb** |
| **GroundMagazineAtb** | `GroundCombat/GroundMagazineAtb.cs:21` | Ammo magazine (kg) | `GroundCombat/WeaponSupply.cs:66`; `GroundUnitAssembly.cs` (gate) | Ground | ↔ ordnance magazine / `GenericWeaponAtb.InternalMagSize` |
| **GroundBayAtb** | `GroundCombat/GroundBayAtb.cs:34` | Troop/vehicle transport bay | `GroundCombat/GroundTransport.cs:44` | **Ship (transport)** | **↔ CargoStorageAtb** |
| **GroundDefenseAtb** | `GroundCombat/GroundDefenseAtb.cs:27` | Fortification (harden region) | `GroundCombat/GroundFortification.cs:92` | Colony installation | — |
| **GroundFootprintAtb** | `GroundCombat/GroundFootprintAtb.cs:27` | War-map presence (capture/bombard target) | `GroundCombat/GroundBuildings.cs:28,296` | Colony installation | — |
| **GroundUnitAtb** | `GroundCombat/GroundUnitAtb.cs:27` | Monolithic unit (raises a `GroundUnit` on install) | its own `OnComponentInstallation` (raise+remove) | Colony→Ground | — |

---

## PARALLEL / DUPLICATED ABILITIES — the crucial finding

Each pair is **the same real capability, modelled twice**, because the space reader iterates ships and the ground reader iterates ground units. In every case the mount flag *could* be widened; the blocker is that the ability's reader lives in a host-specific subsystem.

### 1. RADAR — `SensorReceiverAtb` (space) ↔ `GroundSensorAtb` (ground)
- **Space:** `Sensors/SensorRecever/SensorReceiverAtb.cs:10` — a full EM model (peak wavelength, bandwidth, best/worst sensitivity in kW, resolution, scan time). Read by `SensorTools.cs:216` / `SensorScan`, which iterate sensor-bearing **ships/colonies**.
- **Ground:** `GroundCombat/GroundSensorAtb.cs:21` — one stat, `Range_km`. Read by `GroundSensors.cs:69`, which iterates **ground units'** backing entities and reveals hex bands.
- **Why separate:** the space sensor speaks EM-spectrum-and-signal-quality; the ground one speaks flat kilometres translated to hexes. The two readers (`SensorScan` vs `GroundSensors.RevealFromUnits`) never touch each other. The Sensors CLAUDE.md even states the rule explicitly: *"Do not reuse `SensorScan` for ground unit spotting; it's the wrong tool."*
- **To unify:** one detection attribute with a range expressed portably, plus a reader that runs on both a space contact model and a ground hex reveal — i.e. a shared "detection" service both subsystems call, or a ground reader that consumes `SensorReceiverAtb.BestSensitivity_kW` reduced to an effective range.

### 2. WEAPONS — space weapon atbs ↔ `GroundWeaponAtb` (mid-merge)
- **Space:** `GenericBeamWeaponAtb`, `RailgunWeaponAtb`, `FlakWeaponAtb`, `DisruptorWeaponAtb`, `PlasmaBoltWeaponAtb`, `MissileLauncherAtb` — read by `Combat/ShipCombatValueDB.cs:175-263` and `GenericFiringWeaponsDB`.
- **Ground:** `GroundCombat/GroundWeaponAtb.cs:30` (Mass/Attack/Range/Mode) — read by `GroundUnitAssembly.cs:89`.
- **Status — partially unified already:** `GroundCombat/WeaponSupply.cs:41-95` classifies the **space** weapon atbs (laser→Energy, railgun→Both, flak→Ammo, etc.) for the ground power/ammo gate, so a ground unit can *carry* a space weapon and be supply-gated. So today a ground unit can mount **either** the old `GroundWeaponAtb` **or** a real space weapon — the classic duplicate-mid-refactor. `docs/combat/WEAPONS-DESIGN.md` tracks the planned P4 merge that retires `GroundWeaponAtb`.
- **To unify:** finish P4 — make the ground resolver read damage off the space weapon atbs (via a shared classifier) and delete `GroundWeaponAtb`.

### 3. SHIELDS — `ShieldAtb` (space) ↔ `GroundAugmentAtb.Shield` (ground)
- **Space:** `Combat/ShieldAtb.cs:18` — read by `ShipCombatValueDB.cs:276`.
- **Ground:** the `Shield` field on `GroundCombat/GroundAugmentAtb.cs:36` — read by `GroundUnitAssembly.cs` and applied by `GroundDamageMatrix`. The augment doc even says it *"rides the same evasion the ship dodge model uses"* — the intent to share is stated, the type is not.
- **To unify:** hoist shield (and evasion — see below) into one attribute both resolvers read.

### 4. EVASION / DODGE — ship dodge model ↔ `GroundAugmentAtb.EvasionBonus`
- Ship dodge lives in the weapons/dodge resolver (`docs/combat/WEAPONS-DESIGN.md`); ground evasion is `GroundAugmentAtb.EvasionBonus` (`GroundAugmentAtb.cs:32`), read by `GroundDamageMatrix`. Same currency by design, two code paths.

### 5. PROPULSION / MOBILITY — `NewtonionThrustAtb` + `WarpDriveAtb` (space) ↔ `GroundLocomotionAtb` + `GroundChassisAtb` (ground)
- **Space:** `Movement/NewtonMove/NewtonionThrustAtb.cs:8` (read `EntityExtensions.cs:93`) and `Movement/WarpMove/WarpDriveAtb.cs:8` (read `ComponentInstancesDBExtensions.cs:116`) turn power+fuel into velocity.
- **Ground:** `GroundCombat/GroundLocomotionAtb.cs:24` (SpeedFactor/RoughHandling/Amphibious) and the frame's `GroundChassisAtb` locomotion class — read by `GroundMobility.cs:47,58` to divide march time.
- **Why separate:** space movement is Newtonian delta-V through a physics processor; ground movement is a hex move-time multiplier. Completely different math, no shared "propulsion" abstraction.

### 6. CARRY CAPACITY — `CargoStorageAtb` (space) ↔ `GroundBayAtb` (transport)
- **Space:** `Storage/CargoStorageAtb.cs:7` — read by `StorageSpaceProcessor.cs:40`.
- **Transport:** `GroundCombat/GroundBayAtb.cs:34` — read by `GroundTransport.cs:44`. **Both mount on a ship**, yet they're two attributes for "this part holds N units of stuff." A troop/vehicle bay is a specialised cargo hold with a class filter.
- **To unify:** a bay could be a `CargoStorageAtb` with a cargo-type restricted to "ground unit," rather than a parallel attribute with its own reader.

### 7. AMMO / MAGAZINE — ordnance & `GenericWeaponAtb.InternalMagSize` (space) ↔ `GroundMagazineAtb` (ground)
- **Space:** ammo lives as `GenericWeaponAtb.InternalMagSize` (`WeaponState.cs:52`) and as stockpiled `OrdnanceDesign` cargo.
- **Ground:** `GroundCombat/GroundMagazineAtb.cs:21` — a mass-based `Capacity_kg` pool, read by `WeaponSupply.cs:66`.
- Two different "how much ammo does this hold" models.

### 8. HAZARD / ENVIRONMENT RESISTANCE — `HazardResistanceAtb` (space) ↔ ground `EnvResistance` (not even an attribute)
- **Space:** `Hazards/HazardResistanceAtb.cs:20` — a real component attribute, read by `SpaceHazardTools.cs:97`.
- **Ground:** folded into `GroundUnitDesign.EnvironmentalResistance` (a design map, snapshotted onto the `GroundUnit`), **not** modelled as an installable/strippable component at all (GroundCombat CLAUDE.md flags this: gear-as-a-real-component is the v2 promotion). So the ground side isn't just a duplicate attribute — it's a *weaker* modelling (no cradle-to-grave grave rung).

### 9. ARMOUR — ship armour (a ship-level property) ↔ `GroundArmorAtb` (a component)
- **Ship armour is not an `IComponentDesignAttribute` at all** — it's an armour layer/property on `ShipDesign`. **Ground armour IS a component** (`GroundArmorAtb.cs:18`). So the same concept is a monolithic ship property on one host and a mounted, losable component on the other — a *modelling mismatch*, the inverse of the hazard case.

### The counter-example that proves unification is possible — `EnergyGenerationAtb`
`Energy/EnergyGenerationAtb.cs:10` is read by the **space** power system (`EnergyGenProcessor.cs:13`, via `EnergyGenAbilityDB` built in `OnComponentInstallation`) **and** directly by the **ground** power gate (`GroundCombat/WeaponSupply.cs:104`). It is the single attribute that is genuinely universal across space and ground. It shows the fix pattern: a ground subsystem reading a *space* attribute rather than inventing a parallel one.

---

## Universality matrix (ability × host, ✓ = a processor on that host reads it)

| Ability (real capability) | Ship | Colony/Station | Ground unit | Missile | Modelled by |
|---|:--:|:--:|:--:|:--:|---|
| Beam/energy weapon | ✓ | ✗ | ◐ | ✗ | GenericBeamWeaponAtb (+ GroundWeaponAtb) |
| Kinetic/railgun/flak weapon | ✓ | ✗ | ◐ | ✗ | Railgun/Flak/PlasmaAtb (+ GroundWeaponAtb) |
| Missile launcher | ✓ | ✗ | ✗ | ✗ | MissileLauncherAtb |
| Fire control | ◐ (wired, gated off) | ✗ | ✗ | ✗ | BeamFireControlAtbDB |
| **Radar / detection** | ✓ | ✓ | ✓* | ✗ | **SensorReceiverAtb ∥ GroundSensorAtb** |
| EM signature (be-seen) | ✓ | ✓ | ✗ | ✗ | SensorSignatureAtb |
| **Shield** | ✓ | ✗ | ✓* | ✗ | **ShieldAtb ∥ GroundAugmentAtb.Shield** |
| **Evasion / dodge** | ✓ | ✗ | ✓* | ✗ | **dodge model ∥ GroundAugmentAtb.Evasion** |
| **Armour** | ✓(property) | ✗ | ✓(component) | ✗ | **ship armour ∥ GroundArmorAtb** |
| **Propulsion / mobility** | ✓ | ✗ | ✓* | ✓ | **Newton/WarpAtb ∥ GroundLocomotion/Chassis** |
| **Cargo / carry capacity** | ✓ | ✓ | ✗ | ✗ | **CargoStorageAtb ∥ GroundBayAtb** |
| **Ammo magazine** | ✓ | ✗ | ✓* | ✗ | **GenericWeaponAtb.Mag ∥ GroundMagazineAtb** |
| **Reactor / power gen** | ✓ | ✓ | ✓ | ✗ | **EnergyGenerationAtb (UNIFIED)** |
| Power storage | ✓ | ✓ | ✗ | ✗ | EnergyStoreAtb |
| Solar power gen | ✓ | ✓ | ✗ | ✗ | EnergySolarGenerationAtb |
| **Hazard/env resistance** | ✓ | ✗ | ◐(design, not atb) | ✗ | **HazardResistanceAtb ∥ EnvResistance** |
| Mining | ✗ | ✓ | ✗ | ✗ | MineResourcesAtbDB |
| Industry / construction | ✗ | ✓ | ✗ | ✗ | IndustryAtb / LocalConstructionAtb |
| Research | ✗ | ✓ | ✗ | ✗ | ResearchPointsAtbDB |
| Housing / employment / life-support | ✗ | ✓ | ✗ | ✗ | Housing/Employment/PopulationSupportAtbDB |
| Grav/pressure tolerance | ✗ | ✓ | ✗ | ✗ | Gravity/PressureToleranceAtb |
| Admin / command | ✓ | ✓ | ✗ | ✗ | AdminSpaceAtb |
| Officer training | ✗ | ✓ | ✗ | ✗ | NavalAcademyAtb |
| Geo / grav survey | ✓ | ✗ | ✗ | ✗ | GeoSurveyAtb / GravSurveyAtb |
| Logistics base | ◐ | ✓ | ✗ | ✗ | LogiBaseAtb |
| Fortification | ✗ | ✓(install) | ✗ | ✗ | GroundDefenseAtb |
| War-map presence | ✗ | ✓(install) | ✗ | ✗ | GroundFootprintAtb |
| Troop/vehicle bay | ✓(transport) | ✗ | ✗ | ✗ | GroundBayAtb |

Legend: ✓ read on that host · ◐ partial/latent/property-not-attribute · ✓* on the `GroundUnit`'s backing entity (units-as-entities) · **∥ = duplicated across two attributes** (a bold row is a parallelism to fix).

---

## Open questions / gaps

1. **The mount flag and the reader disagree, silently.** Nothing checks that a part whose `ComponentMountType` allows `GroundUnit` actually has a *ground reader* for its abilities (or vice-versa). A designer could legally mount a `SensorReceiverAtb` part on a ground unit and it would do **nothing** — no ground processor reads it. A universality fix needs either a validation pass (mount flag ⇒ a reader exists on that host) or the shared readers that make the flag meaningful.
2. **Ground abilities are read by an ASSEMBLER, not a live hotloop.** Most ground atbs (`GroundWeaponAtb`, `GroundArmorAtb`, `GroundAugmentAtb`, `GroundChassisAtb`) are read once by `GroundUnitAssembly` at design/build time and flattened into `GroundUnitDesign` stats — not re-read live off `ComponentInstancesDB`. The units-as-entities backing store exists but only `GroundSensorAtb`/`GroundLocomotionAtb` are read off it live. So even *within* ground, the "component on a host" contract is half-wired. (Tracked in `docs/ground/GROUND-UNIT-DESIGNER-DESIGN.md`.)
3. **Which pairs are cheapest to unify?** Ranked by how close the two sides already are:
   - **Weapons** — nearly done; `WeaponSupply` already classifies space atbs on ground. Finish P4.
   - **Reactor** — already unified; use it as the template.
   - **Cargo vs GroundBay** — both mount on ships and both are "capacity + a type filter"; a `CargoStorageAtb` with a ground-unit cargo type could delete `GroundBayAtb`.
   - **Shield/evasion** — the augment doc already says it uses the ship currency; hoist to one attribute.
   - **Radar** — hardest: the two are genuinely different math (EM-spectrum vs km-to-hex). Needs a shared detection *service*, not just a shared attribute.
4. **Armour and hazard-resistance are modelling mismatches, not just duplicate attributes** — one host models the capability as a component, the other as a baked property/design-map. Unifying these means promoting the weaker side to a real component first (a bigger job than merging two attributes).
5. **Stations inherit ship/colony readers "for free"** because they reuse the same component framework — so stations are the accidental proof that a host with no bespoke processors still gets every ability whose reader iterates by DataBlob type rather than by host class. The lesson for universality: **readers keyed to the ABILITY's derived DataBlob (like `EnergyGenAbilityDB`) are automatically portable; readers keyed to a host blob (`ShipInfoDB`, `ColonyInfoDB`, `GroundForcesDB`) are the locks.**
