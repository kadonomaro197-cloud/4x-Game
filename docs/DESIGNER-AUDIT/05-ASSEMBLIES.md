# Designer Audit 05 — The Assemblies (Ships / Stations / Ground Units)

**Scope:** how the three higher-order buildables — a **ship**, a **space station**, a **ground unit** — are composed from `ComponentDesign`s, validated, instantiated as entities, and built through industry; and how much of that machinery is shared vs. copy-pasted.

---

## The one-paragraph answer

There is **no shared assembler.** Each of the three higher-order buildables is composed by a *different* mechanism, validates its parts by a *different* rule, and comes into existence through a *different* factory. A ship is a hand-listed parts list with no engine-side legality check (the mount-type gate lives only in the UI). A station is not "designed" at all — it is deployed bare and grown module-by-module in place. A ground unit is the *only* one with a real engine-side assembler/validator, and that validator gates on ground-specific attributes, not on the shared `ComponentMountType` flags at all. The three do share exactly two low-level primitives underneath: the `ComponentDesign` part itself, and the `Entity.AddComponent` → `ComponentInstancesDB` install step. Everything above those two primitives is triplicated.

---

## Comparison table

| Assembly | Design class (file) | Frame / hull? | Component-validation mechanism | Instantiation factory | Built via industry | Shared with others? |
|----------|--------------------|---------------|-------------------------------|----------------------|--------------------|--------------------|
| **Ship** | `ShipDesign : ICargoable, IConstructableDesign, ISerializable` (`Ships/ShipDesign.cs:22`) | **No frame.** A `List<(ComponentDesign, count)>` + a separate `(ArmorBlueprint, thickness)` armor tuple (`ShipDesign.cs:49-50`). Armor is a *skin*, not a designed part. | **NONE in the engine.** `ShipDesign` accepts any component list and sums it (`Recalculate`, `:137`). The `ComponentMountType.ShipComponent` legality filter exists **only in the UI** (`ShipDesignWindow.cs:568` — `continue` past non-ship parts). Nothing gates crew/power/mass. | `ShipFactory.CreateShip` (`Ships/ShipFactory.cs:94`) → new `Entity` + shared blobs + `foreach shipDesign.Components → ship.AddComponent` (`:132-135`). | `IConstructableDesign`, `IndustryTypeID = "ship-assembly"` (`ShipDesign.cs:61`); `OnConstructionComplete` (`:64`) calls `ShipFactory.CreateShip` (or queues to a `LaunchComplexDB`). | Only the shared primitives (`ComponentDesign`, `AddComponent`). |
| **Space station** | **No design class exists.** (Confirmed: no `StationDesign` type anywhere.) | **No frame designed.** The "frame" is a placeholder *material cost constant* charged at deploy — `DeployStationOrder.FrameMaterialId = "stainless-steel"` etc. (per Stations/CLAUDE.md Slice F). No parts list, no shape. | **NONE.** A station is deployed **bare** (`StationFactory.CreateStation` attaches a fixed blob set, `Stations/StationFactory.cs:31-83`), then modules are built **in-situ** one at a time. Which modules may be installed is bounded only by the same UI/industry `PlanetInstallation` filtering (`IndustryDisplay.cs:415`), not by any station assembler. | `StationFactory.CreateStation` (`:31`) → entity + hardcoded blob list (incl. `ComponentInstancesDB`, `:60`). Modules added later via the host-agnostic industry `AddComponent` path. | Blob set **mirrors `ColonyFactory` by hand** (see Stations/CLAUDE.md "shared chassis"); reuses the industry install path. No ship/ground reuse. |
| **Ground unit** | `GroundUnitDesign : IConstructableDesign` (`GroundCombat/GroundUnitDesign.cs:25`) — flat combat stats **+** a `ComponentDesignIds` frame+parts dict (`:81`). | **Yes — a designed frame.** The frame is a `ComponentDesign` carrying `GroundChassisAtb`; the assembler requires exactly one (`GroundUnitAssembly.cs:57-62`). Host-specific: identified by `GroundChassisAtb`, and carry-class/HP/strength come from it. | **The only real engine validator.** `GroundUnitAssembly.Compute` (`:54`) checks: exactly-one-chassis (`:57`), **carry-capacity gate** (Σ part mass ≤ frame strength + augments, `:134`), **per-item weight gate** (`:125`), **power gate** (weapon draw ≤ reactor supply, `:137`), **ammo gate** (`:140`). Validates by **ground ATBs** (`GroundWeaponAtb`/`GroundArmorAtb`/`GroundAugmentAtb`/`GroundChassisAtb`), **not** by `ComponentMountType`. | Two paths: (a) `GroundUnitAtb` component installed on a colony → `OnComponentInstallation` raises + self-removes (see GroundCombat/CLAUDE.md A1); (b) `GroundUnitDesign.OnConstructionComplete` (`:89`) → `GroundForces.RaiseUnit` places a **data object** (not an entity) on the planet's `GroundForcesDB`, with an optional inert **backing entity** (`GroundUnitEntity.BuildBacking`, `GroundUnitEntity.cs:30`). | Reuses `ComponentDesign` parts, `IConstructableDesign` rails, and (for the inert backing only) `ComponentInstancesDB`. Assembler + validator are bespoke. |

*(A fourth assembly, out of primary scope but relevant to the universality question: **missiles** — `OrdnanceDesign : IConstructableDesign` (`Weapons/OrdnanceDesign.cs`) — is a component list like a ship, but unlike a ship it **does** check the mount flag in the engine, silently ignoring any part whose `ComponentMountType` lacks `Missile` (`OrdnanceDesign.cs:97`). So even among the parts-list designs, the mount-legality convention is inconsistent.)*

---

## Shared vs. duplicated assembly logic

**What is genuinely shared — two primitives, both below the assembler:**

1. **The part** — every assembly is built from `ComponentDesign` objects carrying `IComponentDesignAttribute`s, produced by the one `ComponentDesigner` (`Engine/Components/ComponentDesigner.cs`). This is the real universal layer, and it works.
2. **The install step** — `Entity.AddComponent(design)` → `new ComponentInstance` → `ComponentInstancesDB.AddComponentInstance` + fire each atb's `OnComponentInstallation` + `ReCalcAbilities` (`Engine/Entities/Entity.cs:125-145`). Ships use it directly (`ShipFactory.cs:134`); stations use it via the in-situ industry path; ground units use only its low-level half for the inert backing (`GroundUnitEntity.cs:57`, deliberately bypassing the install hooks).

**What is duplicated / divergent (everything else):**

- **Three different "what is a valid part" answers.** Ship: UI-only `ShipComponent` flag filter (engine trusts the list). Missile: engine-side `Missile` flag filter. Ground unit: engine-side **ATB-presence** check (chassis/weapon/armor/augment), ignoring the mount flags entirely. Station: no check at all. So the `[Flags] ComponentMountType` enum — the thing that *looks* like the universal legality system — is honored by **only one and a half** of the four paths, and the one path with the deepest validator (ground) doesn't consult it.
- **Three emergent-stat calculators, each re-summing the parts.** `ShipDesign.Recalculate` (`:137`) sums mass/crew/cost/volume + armor mass. `GroundUnitAssembly.Compute` (`:54`) sums attack/defense/HP/mass/carry + runs the gates. A station has none (upkeep is computed post-hoc from installed-module count in `StationEconomyDB.OperatingCost`). The **summation pattern is copy-pasted, not factored** — `GroundUnitAssembly.ToGroundUnitDesign` even documents that it does "the same sum the ship designer does" (`GroundUnitAssembly.cs:172`).
- **Three instantiation shapes.** Ship = a full `Entity` (position/orbit/mass/damage-profile/components). Station = a full `Entity` with a *hand-written, colony-mirrored* blob set. Ground unit = a serializable **data object** inside a blob on the planet body, *not* an entity (optionally shadowed by an inert component-only backing entity).
- **Three destroy paths, all hand-written to mirror each other.** `ShipFactory.DestroyShip` (`ShipFactory.cs:212`), `StationFactory.DestroyStation` (`StationFactory.cs:101`, explicitly "mirroring `ShipFactory.DestroyShip`"), and the ground capture/removal path. Each re-implements faction-unregister + sub-entity teardown separately.
- **Crew/mass/cost derivation diverges.** Ship crew = Σ component `CrewReq` (`ShipDesign.cs:152`), committed from a colony manpower pool at build (`ShipDesign.cs:103`). Ground unit has flat stats; crew is not a first-class emergent gate (a v2 "falls out of components" promotion is noted in GroundCombat/CLAUDE.md). Station has no crew-from-parts model (population is a separate habitat-capacity system).

---

## Universality assessment

**There is not one assembly engine; there are three (four with missiles), sharing only the part and the install call.** The mount-type system that should make them universal is applied inconsistently and, in the deepest validator, not at all.

A single **universal assembler** would need:

1. **One assembly type.** A generic `Assembly` (host-tag + frame + `List<(ComponentDesign, count)>`) that ship, station, and ground unit are all *configurations* of — instead of `ShipDesign` (list+armor), no-station-design, and `GroundUnitDesign` (stats+ids) being three unrelated classes.
2. **One legality rule, honored everywhere.** Make `ComponentMountType` the single gate, enforced **in the engine** (not the UI), for every host. That means (a) moving the ship gate out of `ShipDesignWindow.cs:568` into `ShipDesign`, (b) giving stations a real design + gate, and (c) teaching the ground assembler to consult `ComponentMountType.GroundUnit` alongside (or instead of) its ATB-presence checks. Today a part legal on one host is legal on another only by coincidence of which check each path happens to run.
3. **One frame concept.** Decide whether every assembly has a designed frame/hull. Ground units do (`GroundChassisAtb`); ships do not (armor is a skin); stations have only a placeholder cost. A universal assembler needs frames to be either universal or universally optional — and if a "frame" carries host-specific budgets (carry capacity, mount slots, hardpoints), that budget model must be generalized, not chassis-only.
4. **One emergent-stat pass.** A single "recalculate derived stats from parts + gates" routine the three configs parameterize, replacing the three hand-summed calculators.
5. **One instantiation + one teardown.** A shared "materialize an assembly into the world" (entity vs. data-object is the hard part — ground units aren't entities) and a shared destroy that handles faction-unregister + sub-entity cleanup once.

The good news the audit surfaces: the **bottom two rungs are already universal and solid** (`ComponentDesign` + `AddComponent`/`ComponentInstancesDB`). The non-universality is entirely in the **middle band** — validation, frame, stat-summation, and instantiation — where three parallel implementations grew independently.

---

## Open questions / gaps

- **Ground unit as data-object vs. entity** is the deepest obstacle to a truly universal assembler: a ship/station is an `Entity`; a `GroundUnit` is a serialized struct in `GroundForcesDB` (with only an *inert* backing entity). A universal "instantiate" step must reconcile these two representations (the "units-as-entities" migration, GroundCombat/CLAUDE.md, is the in-progress path toward it).
- **Should the mount-type gate be authoritative?** Right now ground uses ATB-presence and the base-mod ground parts are given `PlanetInstallation` mounts (per A1/A2 notes), so the `GroundUnit` mount flag exists but the assembler doesn't check it. Decide which is the source of truth before generalizing.
- **Stations need a design at all?** The "deploy bare, build in-situ" model means a station is never validated as a whole. If universality requires a `StationDesign`, that's a new class + a new validation surface; if not, the universal assembler must tolerate one host that is grown, not designed.
- **Armor generalization.** Ship armor is a bespoke `(ArmorBlueprint, thickness)` tuple with its own mass math (`ShipDesign.GetArmorMass`, `:185`). Ground uses `GroundArmorAtb` parts. A universal assembler must pick one armor model.
- **Crew as an emergent gate** is a ship-only concept today; whether a universal assembler makes crew a Σ-parts budget for all hosts (like the ground power/carry gates) is unresolved.
- **`ShipDesign` has no legality check whatsoever in the engine** — a programmatically-built ship design (tests, NPC AI, a future universal assembler) can include illegal parts and will silently sum them. This is a latent correctness gap independent of universality.
