# 04 — Base-Mod Templates (the player-buildable catalog)

**Scope:** Every `ComponentTemplate` in `Pulsar4X/GameData/basemod/TemplateFiles/*.json` — the JSON data that defines what a player can design in an in-game designer — categorized by function, with its host-legality (`MountType`) flags tabulated to expose non-universality.

---

## What this is, in plain English

A "template" is the blank blueprint a designer starts from. When a player opens a designer and tweaks sliders, they are filling in one of these templates to produce a `ComponentDesign`. The template decides three things that matter for universality:

- **`MountType`** — which *hosts* the finished component may be installed on. It's a bitfield: `ShipComponent` (1), `ShipCargo` (2), `PlanetInstallation` (4), `PDC` (8), `Fighter` (16), `Missile` (32), `GroundUnit` (64). A template listing several is legal on all of them; one listing a single flag is host-locked.
- **`IndustryTypeID`** — which production line builds it (`component-construction` vs `installation-construction`).
- **The attributes** (`AttributeType` inside each Property) — the actual capability the component grants.

**Totals:** 67 `ComponentTemplate` payloads across 7 files. **66 distinct UniqueIDs** — `spaceport` is defined twice (see inconsistency I-1). No `PDC` flag is used by any template. Only weapons and one energy attribute reach `GroundUnit` from the ship side.

Files that carry templates: `weapons.json` (7), `installations.json` (37), `storage.json` (6), `energy.json` (5), `electronics.json` (5), `ordnance.json` (4), `engines.json` (3). All other `TemplateFiles/*.json` are data tables (materials, minerals, techs, doctrines, etc.), not templates.

---

## Per-category tables

### 1. Weapons — space (`weapons.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| laser-weapon | EMaser Weapon | Weapon | ShipComponent, ShipCargo, PlanetInstallation, **GroundUnit** | component-construction | GenericBeamWeaponAtb, GenericWeaponAtb |
| railgun-weapon | Railgun | Weapon | ShipComponent, ShipCargo, PlanetInstallation, **GroundUnit** | component-construction | RailgunWeaponAtb |
| flak-weapon | Flak / Point-Defense Gun | Weapon | ShipComponent, ShipCargo, PlanetInstallation, **GroundUnit** | component-construction | FlakWeaponAtb |
| disruptor-weapon | Ion Disruptor | Weapon | ShipComponent, ShipCargo, PlanetInstallation, **GroundUnit** | component-construction | DisruptorWeaponAtb |
| plasma-repeater | Plasma Repeater | Weapon | ShipComponent, ShipCargo, PlanetInstallation, **GroundUnit** | component-construction | PlasmaBoltWeaponAtb |
| missile-launcher | Missile Launcher | Weapon | ShipComponent, ShipCargo | component-construction | GenericWeaponAtb, MissileLauncherAtb |

### 2. Weapons — ground (`installations.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| ground-rifle | Service Rifle | Weapon | GroundUnit | installation-construction | GroundWeaponAtb |
| ground-autocannon | Heavy Autocannon | Weapon | GroundUnit | installation-construction | GroundWeaponAtb |
| ground-cannon | Tank Cannon | Weapon | GroundUnit | installation-construction | GroundWeaponAtb |
| energy-weapon | Plasma Projector | Weapon | GroundUnit | installation-construction | GroundWeaponAtb |
| claw-weapon | Rending Claws | Weapon | GroundUnit | installation-construction | GroundWeaponAtb |

### 3. Sensors / fire-control (`electronics.json`, `installations.json`, `ordnance.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| passive-sensor | Passive Sensor | Sensor | PlanetInstallation, ShipComponent, ShipCargo, Fighter | component-construction | SensorReceiverAtb |
| sensor-hardening-module | Sensor Hardening Module | Sensor | ShipComponent, ShipCargo, PlanetInstallation, Fighter | component-construction | HazardResistanceAtb |
| beam-fire-control | Laser Weapon Turret | Weapon | ShipComponent, ShipCargo | component-construction | BeamFireControlAtbDB |
| ground-radar | Ground Radar | Sensor | GroundUnit | installation-construction | GroundSensorAtb |
| missile-electronics-suite | Missile Electronics Suite | Sensor | Missile | component-construction | ElectronicsSuite, SensorReceiverAtb |

### 4. Power / energy (`energy.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| reactor | Reactor | Facility | ShipComponent, ShipCargo, Fighter, **GroundUnit** | component-construction | EnergyGenerationAtb, SensorSignatureAtb |
| rtg | RTG | Energy Generator | ShipComponent, ShipCargo, Fighter, **GroundUnit** | component-construction | EnergyGenerationAtb, SensorSignatureAtb |
| steam-turbine-reactor | Steam Turbine Reactor | Energy Generator | ShipComponent, ShipCargo, Fighter, **GroundUnit** | component-construction | EnergyGenerationAtb, SensorSignatureAtb |
| battery-bank | Battery Bank | Energy Storage | ShipComponent, ShipCargo, Fighter | component-construction | EnergyStoreAtb |
| solarArray | Solar Array | Energy Generation | **`1` (ShipComponent only — raw int!)** | component-construction | EnergySolarGenerationAtb |

### 5. Propulsion (`engines.json`, `ordnance.json`, `installations.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| conventional-engine | Conventional Rocket Engine | Engine | ShipComponent, ShipCargo, Fighter | component-construction | NewtonionThrustAtb, SensorSignatureAtb |
| scntr-engine | Solid Core Nuclear Thermal Engine | Engine | ShipComponent, ShipCargo, Fighter | component-construction | NewtonionThrustAtb, SensorSignatureAtb |
| alcubierre-warp-drive | Alcubierre Warp Drive | Engine | ShipComponent, ShipCargo, Fighter | component-construction | WarpDriveAtb |
| missile-srb | Missile SRB | Engine | Missile | component-construction | NewtonionThrustAtb, SensorSignatureAtb |
| ground-locomotion | Locomotion Drive | Engine | GroundUnit | installation-construction | GroundLocomotionAtb |

### 6. Cargo / fuel / storage (`installations.json`, `storage.json`, `ordnance.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| fuel-cargo-hold | Fuel Storage | Fuel Storage | ShipComponent, ShipCargo, PlanetInstallation, Fighter | installation-construction | CargoStorageAtb |
| stainless-steel-fuel-tank | Stainless Steel Fuel Tank | Fuel Storage | ShipComponent, ShipCargo, PlanetInstallation, Fighter | installation-construction | CargoStorageAtb |
| ordnance-cargo-hold | Ordnance Storage | Cargo Hold | ShipComponent, ShipCargo, PlanetInstallation | component-construction | StorageTransferRateAtbDB, VolumeStorageAtb |
| general-cargo-hold | General Cargo Hold | Cargo Hold | ShipComponent, ShipCargo | installation-construction | CargoStorageAtb |
| warehouse-facility | Warehouse faciliy | Cargo Hold | ShipCargo, PlanetInstallation | installation-construction | CargoStorageAtb |
| cargo-Shuttlebay | Cargo Shuttlebay | Cargo Hold | ShipComponent, ShipCargo | installation-construction | CargoTransferAtb |
| space-port | Space Port | Facility | ShipComponent, ShipCargo, PlanetInstallation | installation-construction | CargoTransferAtb |
| spaceport *(storage.json)* | Space Port | Cargo Hold | ShipCargo, PlanetInstallation | installation-construction | CargoTransferAtb |
| troop-bay | Troop Bay | Troop Bay | **ShipComponent** | installation-construction | GroundBayAtb |

### 7. Armour / defence (`weapons.json`, `installations.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| deflector-array | Deflector Array | Defense | ShipComponent, ShipCargo, PlanetInstallation | component-construction | ShieldAtb |
| bunker | Bunker | Facility | PlanetInstallation | installation-construction | GroundDefenseAtb, GroundFootprintAtb |
| ground-plating | Composite Plating | Ground Armour | GroundUnit | installation-construction | GroundArmorAtb |
| power-armor | Power Armour | Augment | GroundUnit | installation-construction | GroundAugmentAtb |
| shield-generator | Shield Generator | Augment | GroundUnit | installation-construction | GroundAugmentAtb |
| reflex-booster | Reflex Booster | Augment | GroundUnit | installation-construction | GroundAugmentAtb |

### 8. Industry / mining (`installations.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| mine | Mine | Facility | **PlanetInstallation** | installation-construction | MineResourcesAtbDB |
| automine | RoboMiner | Facility | ShipCargo, PlanetInstallation | installation-construction | MineResourcesAtbDB |
| refinery | Refinery | Facility | ShipCargo, PlanetInstallation | installation-construction | IndustryAtb |
| factory | Factory | Facility | ShipCargo, PlanetInstallation | installation-construction | IndustryAtb |
| shipyard | Ship Yard | Facility | ShipCargo, PlanetInstallation | installation-construction | IndustryAtb |
| local-construction | Construction Services | Facility | PlanetInstallation | installation-construction | LocalConstructionAtb |
| launch-complex | Launch Complex | Facility | PlanetInstallation | installation-construction | LaunchComplexAtb |

### 9. Research / survey / academy (`installations.json`, `electronics.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| research-lab | Research Lab | Facility | ShipCargo, PlanetInstallation | installation-construction | ResearchPointsAtbDB |
| naval-academy | Naval Academy | Facility | PlanetInstallation | installation-construction | NavalAcademyAtb |
| geo-surveyor | Geological Surveyor | Science | ShipComponent, ShipCargo | component-construction | GeoSurveyAtb |
| gravitational-surveyor | Gravitational Surveyor | Science | ShipComponent, ShipCargo | component-construction | GravSurveyAtb |

### 10. Population / infrastructure / admin / logistics (`installations.json`, `storage.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| infrastructure | Infrastructure | Infrastructure | ShipCargo, PlanetInstallation | installation-construction | CargoStorageAtb, GravityToleranceAtb, HousingAtbDB, InfrastructureCapacityAtb, PopulationSupportAtbDB, PressureToleranceAtb |
| space-habitat | Space Habitat | Infrastructure | ShipCargo, PlanetInstallation | installation-construction | CargoStorageAtb, HousingAtbDB, InfrastructureCapacityAtb, PopulationSupportAtbDB |
| admin-complex | Administrative Complex | Facility | PlanetInstallation | installation-construction | AdminSpaceAtb |
| logistics-office | Logistics Office | Facility | ShipCargo, PlanetInstallation | installation-construction | LogiBaseAtb |
| spaceport *(installations.json)* | Planetary Spaceport Complex | Facility | PlanetInstallation | installation-construction | CargoStorageAtb, CargoTransferAtbDB |
| ship-command | Ship Bridge | Admin | ShipComponent, ShipCargo | installation-construction | AdminSpaceAtb |

### 11. Ground-unit parts — chassis, magazine, whole units (`installations.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| human-frame | Human Frame | Chassis | GroundUnit | installation-construction | GroundChassisAtb |
| vehicle-frame | Tracked Vehicle Frame | Chassis | GroundUnit | installation-construction | GroundChassisAtb |
| walker-frame | Bipedal Walker Frame | Chassis | GroundUnit | installation-construction | GroundChassisAtb |
| swarm-frame | Swarmling Frame | Chassis | GroundUnit | installation-construction | GroundChassisAtb |
| ground-magazine | Ammo Magazine | Facility | GroundUnit | installation-construction | GroundMagazineAtb |
| infantry-unit | Infantry | Facility | PlanetInstallation | installation-construction | GroundUnitAtb |
| armor-unit | Armor | Facility | PlanetInstallation | installation-construction | GroundUnitAtb |
| artillery-unit | Artillery | Facility | PlanetInstallation | installation-construction | GroundUnitAtb |

### 12. Ordnance payload (`ordnance.json`)

| UniqueID | Name | ComponentType | MountType | IndustryTypeID | Attributes |
|---|---|---|---|---|---|
| missile-payload | Missile Payload | Weapon | Missile | component-construction | OrdnanceExplosivePayload, OrdnanceShapedPayload, OrdnanceSubmunitionsPayload |

*(The other three Missile-mount parts — `missile-srb`, `missile-electronics-suite` — appear under Propulsion and Sensors above; together with `missile-payload` they are the missile-designer parts.)*

---

## MountType coverage analysis

### Host reach at a glance

| Host flag | # templates that allow it |
|---|---|
| ShipComponent | 34 |
| ShipCargo | 32 |
| PlanetInstallation | 27 |
| Fighter | 7 |
| GroundUnit | 27 (but 22 are ground-ONLY parts; only 8 space-side templates reach it) |
| Missile | 4 |
| PDC | **0 — the flag exists but NO template uses it** |

### The core non-universality: two parallel, non-overlapping worlds

The catalog is effectively **two designers that do not share parts**, split by `IndustryTypeID` *and* by mount vocabulary:

- **Space/ship world** (`component-construction` for weapons/electronics/energy/engines; `installation-construction` for storage/facilities) speaks `ShipComponent / ShipCargo / PlanetInstallation / Fighter`.
- **Ground world** (all `GroundUnit`, all `installation-construction`) speaks only `GroundUnit` and uses an entirely separate attribute family (`Ground*Atb`).

A `GroundUnit`-mount part carries a `Ground*Atb` attribute (`GroundWeaponAtb`, `GroundChassisAtb`, `GroundArmorAtb`, `GroundAugmentAtb`, `GroundSensorAtb`, `GroundLocomotionAtb`, `GroundMagazineAtb`), which the ship systems don't read; conversely the ship attributes (`GenericWeaponAtb`, `SensorReceiverAtb`, `NewtonionThrustAtb`) aren't read by ground processors. So even where a mount flag *would* let a part cross over, the capability behind it wouldn't function. **Mount flags are necessary but not sufficient for universality — the attribute plumbing is the deeper wall.**

### Host-locked capabilities (single-flag templates)

- **PlanetInstallation-only:** `mine`, `naval-academy`, `spaceport`(installations), `admin-complex`, `local-construction`, `launch-complex`, `bunker`, `infantry-unit`, `armor-unit`, `artillery-unit`. (Mining, academies, admin, launch, and the whole-unit ground pieces are planet-bound.)
- **GroundUnit-only:** all 22 ground parts (5 weapons, 4 chassis, 3 augments, plating, radar, locomotion, magazine, etc.).
- **Missile-only:** `missile-payload`, `missile-srb`, `missile-electronics-suite`.
- **ShipComponent-only:** `troop-bay` (GroundBayAtb), and `solarArray` (by the raw-int bug below).

### Confirmed / quantified inconsistencies

**I-1 — Duplicate UniqueID `spaceport`.** Defined in BOTH `installations.json` ("Planetary Spaceport Complex", `CargoStorageAtb + CargoTransferAtbDB`, PlanetInstallation-only) AND `storage.json` ("Space Port", `CargoTransferAtb`, ShipCargo+PlanetInstallation). Same key, different Name, different attributes, different mounts. Load order decides which wins; the other is silently shadowed. There is *also* a third, distinctly-keyed `space-port` in `installations.json`. Three near-identical port templates, two colliding on one ID.

**I-2 — `solarArray` mount authored as raw integer `1`.** Every other template uses the comma-string form (`"ShipComponent, ..."`); `solarArray` alone has `"MountType": 1`. That resolves to `ShipComponent` **only** — a Solar Array cannot be a `PlanetInstallation` or go on a `GroundUnit`, even though the other three generators (`reactor`, `rtg`, `steam-turbine-reactor`) all reach `ShipComponent+ShipCargo+Fighter+GroundUnit`. A solar farm being un-buildable on a planet is the clearest "logically-should-mount-elsewhere" miss in the file.

**I-3 — Space weapons reach GroundUnit; sensors and fire-control do not.** All six ship weapons except `missile-launcher` list `GroundUnit` (`laser-weapon`, `railgun-weapon`, `flak-weapon`, `disruptor-weapon`, `plasma-repeater`). But the ship `passive-sensor` and `beam-fire-control` do NOT include `GroundUnit`, and ground units instead get a separate `ground-radar` (`GroundSensorAtb`). So a ground unit could nominally mount a ship laser but not a ship sensor to aim it — an asymmetric, probably-unintended crossover. (Caveat: the ship weapons carry `GenericWeaponAtb`, not `GroundWeaponAtb`, so whether that `GroundUnit` flag actually *functions* on a ground unit is doubtful — likely a copy-paste of the flag list rather than a wired capability. Worth an engine check.)

**I-4 — Energy generators reach GroundUnit but energy STORAGE does not.** `reactor`/`rtg`/`steam-turbine-reactor` allow `GroundUnit`; `battery-bank` (the `EnergyStoreAtb`) stops at `ShipComponent+ShipCargo+Fighter`. A ground unit can generate power but can't store it.

**I-5 — `IndustryTypeID` split is inconsistent within storage.** Storage/cargo templates are mostly `installation-construction`, but `ordnance-cargo-hold` (also a Cargo Hold) is `component-construction`. Similarly the fuel tanks (`installation-construction`) sit next to weapons/engines (`component-construction`) though all can mount on a ship. The build-line routing doesn't track the host cleanly.

**I-6 — Ground weapons vs. space weapons are duplicated concepts on different rails.** `energy-weapon` (Plasma Projector, GroundUnit, `GroundWeaponAtb`) vs. `plasma-repeater` (GroundUnit-flagged, `PlasmaBoltWeaponAtb`); `ground-cannon`/`ground-autocannon` vs. `railgun-weapon`/`flak-weapon`. The same physical idea exists twice — once as a ground part with `Ground*Atb`, once as a ship part flagged for ground — because the two designers can't share one weapon definition.

**I-7 — Whole ground UNITS and ground PARTS are mixed in the same file at different mount levels.** `infantry-unit`/`armor-unit`/`artillery-unit` are `PlanetInstallation`-mount `Facility` objects carrying `GroundUnitAtb` (a pre-built unit you build at a colony), while `human-frame`+`ground-rifle`+`ground-plating` are `GroundUnit`-mount parts you assemble into a custom unit. Two different construction paradigms for "a soldier" coexist.

---

## Open questions / gaps

1. **`PDC` flag is dead in the data.** No template uses it. Is Planetary Defense Center intended to be a distinct host, or should PDC weapons just be `PlanetInstallation` weapons? Right now a player cannot design anything for a PDC.
2. **Does the `GroundUnit` flag on ship weapons actually work?** Those weapons carry ship-side `Generic*Atb`/`RailgunWeaponAtb`, not `GroundWeaponAtb`. Confirm in the engine whether a ground unit can mount and *fire* a `laser-weapon`, or whether the flag is inert (I-3). This decides whether the fix is "add the flag everywhere" or "unify the attribute families."
3. **Is `solarArray`'s `1` a bug or a deliberate ship-only choice?** Recommend normalizing to the string form and deciding its true host set (I-2).
4. **Resolve the `spaceport` UniqueID collision** — pick one owner file, rename or merge (I-1).
5. **Universality target:** the deepest barrier is not the mount flag but the **split attribute vocabulary** (`Ground*Atb` vs ship attributes) and the **split `IndustryTypeID`**. A truly universal designer needs either one attribute family both worlds read, or an explicit adapter — flags alone won't merge the two designers.
6. **`troop-bay` is `ShipComponent`-only** — intentional (troops ride ships), but note it can't be a `PlanetInstallation` staging bay; confirm that's desired.
7. Several `ComponentType` free-strings are near-synonyms (`Energy Generator` vs `Energy Generation` vs `Facility` for power; `Cargo Hold` vs `Fuel Storage` vs `Facility` for storage). The category string is not a reliable grouping key — mount + attribute is.
