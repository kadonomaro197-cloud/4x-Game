# Designer Audit — Executive Summary

**What this is:** a complete, cited survey of **everything a player can design, make, or build in Pulsar4X**, generalized and categorized, written so the developer's problem — *"the in-game designers are not universal"* — can be fixed system by system. Seven detail files (01–07) hold the file:line evidence; this file is the map and the plan.

**As of:** 2026-07-08 · branch `claude/sol-playtest-earth-map-8r59j6` · produced by a 7-way parallel code audit.

**The one-sentence verdict:** the engine already has a *universal* buildable model at the bottom (a part is a `ComponentDesign` carrying an ability, built by an industry that doesn't care what host it's on) — but the **middle band** (which designer offers a part, which validator lets it in, and whether the ability actually *functions* on that host) was built four separate times, once per host, so the universality is real in the basement and lost on the main floor.

---

## 1. The generalized model — everything a player makes is one stack

Strip away the names and every buildable in the game is a rung on **one ladder**. This is the cradle-to-grave chain the project already believes in, drawn as the audit found it:

```
  MINERAL      mined from a deposit                     (rung 0)
     │  refine
  MATERIAL     a ProcessedMaterial                      (rung 1)
     │  consume to build
  COMPONENT    a ComponentDesign  =  a TEMPLATE  +  one or more ABILITIES
     │            (the ability = an IComponentDesignAttribute; a "rating plate" on the part)
     │            designed in the ONE universal Component Designer
     │  compose many components into...
  ASSEMBLY     a SHIP / STATION / GROUND UNIT / MISSILE  (rung 3)
     │  install / deploy
  IN PLAY      the thing on the map that fights, mines, senses, moves
```

Two support systems wrap the whole ladder:
- **INDUSTRY** turns materials into components and components into assemblies (the "make" engine).
- **RESEARCH** unlocks *which* templates a faction is allowed to design, and *scales* a template's numbers as you tech up.

**Everything a player can make is one of just five kinds of thing:**

| Kind | What it is | Designed/made where | Detail file |
|------|------------|---------------------|-------------|
| **Refined material** | a `ProcessedMaterial` from minerals | queued in the refinery (not "designed") | 06 |
| **Component** | a `ComponentDesign` carrying ability attributes (a weapon, engine, reactor, sensor, cargo hold, mine, lab, frame, magazine, radar, drive…) | the **Component Designer** (universal) | 03, 04, 07 |
| **Ship** | an assembly of components on a hull | the **Ship Designer** | 01, 02, 05 |
| **Space station** | an assembly of modules (no design class — deployed then furnished) | **deploy order → in-situ build** | 02, 05 |
| **Ground unit** | an assembly of a frame + parts | `GroundUnitAssembly` (no player UI yet) | 02, 05 |
| **Missile / ordnance** | an assembly of payload + engine + electronics | the **Ordnance Designer** | 02, 05 |

That's the whole catalog. **67 base-mod component templates**, **5 industry job categories**, **5 concrete buildable C# types**, and **~50 ability attributes** — all enumerated with file:line in the detail files.

---

## 2. The diagnosis — WHY the designers aren't universal (two locks, not one)

The developer's instinct — *"a radar I designed should mount on a ship OR a station OR a ground installation"* — is exactly right, and the engine was clearly built to allow it. But an ability reaching a host has to pass **two separate gates**, and they are not the same gate. This is the crux of the whole audit:

### Lock #1 — the mount flag (shallow, cheap to fix)
Every template carries a hand-authored `ComponentMountType` — a `[Flags]` field (Ship / Cargo / Colony / PDC / Fighter / Missile / GroundUnit). Because it's flags, one part *can* legally be tagged for many hosts, and the base mod already does this (a fuel tank is tagged for four hosts at once). **The one universal Component Designer honors this correctly — it offers every template and lets the flag decide (01).**

The break: the **four downstream assemblers each hardcode a single flag test.** The Ship Designer only shows `ShipComponent` parts, the Ordnance Designer only `Missile`, colony construction only `PlanetInstallation`, the ground placer only `PlanetInstallation` (01, file:line inside). So a part tagged for three hosts still surfaces in only one builder. On top of that the flags themselves are authored **inconsistently** in the JSON (04): a solar array is locked ship-only, energy reactors reach ground but the battery next to them doesn't, weapons carry the GroundUnit flag but sensors don't, and there's a duplicate `spaceport` ID that silently shadows itself. **This layer is a data + UI-filter problem — a few days of careful editing, no new architecture.**

### Lock #2 — the processor reader (deep, the real wall)
Even if you fix every flag, a mounted ability only *does something* if some **processor reads that attribute off that host.** Think of the attribute as a rating plate bolted to a part: the plate only matters if a watch-stander is trained to read *that* plate on *that* kind of equipment. In this codebase almost every "watch-stander" (processor) lives inside one host's subsystem, so the **same real capability got built twice** — once as a space attribute read by a space processor, once as a ground attribute read by a ground processor (03).

The audit found **nine parallel/duplicated ability pairs** — the same real thing modeled as two incompatible parts:

| # | The capability | Space attribute | Ground/other twin | Notes |
|---|----------------|-----------------|-------------------|-------|
| 1 | **Radar / detection** | `SensorReceiverAtb` | `GroundSensorAtb` | the one that started this |
| 2 | **Weapons** | `Beam/Railgun/Flak/Disruptor/Plasma` atbs | `GroundWeaponAtb` | **already mid-merge** (`WeaponSupply` classifies the space atbs for ground) |
| 3 | **Shields** | `ShieldAtb` | `GroundAugmentAtb.Shield` | |
| 4 | **Evasion / dodge** | ship dodge model | `GroundAugmentAtb.EvasionBonus` | |
| 5 | **Propulsion / mobility** | `NewtonionThrustAtb` + `WarpDriveAtb` | `GroundLocomotionAtb` + `GroundChassisAtb` | |
| 6 | **Carry capacity** | `CargoStorageAtb` | `GroundBayAtb` | *both already mount on ships* |
| 7 | **Ammo magazine** | `GenericWeaponAtb` mag / ordnance | `GroundMagazineAtb` | |
| 8 | **Hazard resistance** | `HazardResistanceAtb` | ground `EnvResistance` (a design map, **not even a component**) | weaker side must be promoted first |
| 9 | **Armour** | ship armour (a `ShipDesign` property, **not a component**) | `GroundArmorAtb` | modelling mismatch both ways |

**This is the deeper wall the mount flags hide (04's phrase: "the attribute plumbing is a deeper wall than the mount flags").** Unifying a pair means: pick ONE attribute, and make *both* host processors read it — then retire the twin.

### The proof it's fixable — the pattern is already in the tree
Three parts of the codebase already do universality correctly, and they're the templates to copy:

- **Industry is fully host-uniform (06).** Every "make" verb is an installed component carrying an `*Atb`, and every industry/mining/research processor finds its work by an **ability DataBlob, never by host type**. A space station with a factory builds exactly like a colony, with *zero* station-aware code. This is the target pattern.
- **Research/unlock is fully host-uniform (07).** One faction data store, one `ComponentTemplates` dictionary, no per-host copy. Researching a radar unlocks it for the faction, full stop — never "unlocked for ships but not colonies." Tech decides *whether*, never *where*.
- **`EnergyGenerationAtb` (reactors) is the one already-universal ability (03).** It is read by a **space** processor *and* a **ground** system off the same attribute. Reactors already work everywhere. Every other capability should look like this.

So the fix is not "invent universality" — it's "make the middle band conform to the universality the basement already has."

---

## 3. The structural findings (the shape underneath)

Beyond the flag/reader locks, the audit found the scaffolding that keeps the four worlds apart (02, 05):

- **No shared assembly engine.** Ship, station, ground unit, and missile are composed by four different mechanisms, validated by four different rules, and materialized by four different factories. Only the **bottom two rungs are shared** — the `ComponentDesign`+attribute, and `Entity.AddComponent → ComponentInstancesDB`. Everything above (validation / frame / stat-sum / instantiate / teardown) is triplicated.
- **`ComponentMountType` is enforced in only ~1.5 of 4 engine paths**, and the *deepest* validator (`GroundUnitAssembly.Compute`) ignores the flag entirely — it gates on ground-specific attributes instead. So the enum that looks like the universal legality system is barely honored.
- **Mount legality lives only on `ComponentDesign`.** For ships/missiles/ground units the host is baked into *which C# class* wraps the design, so a capability can't be re-hosted without changing its class.
- **Stations have no design class at all** (deploy-then-furnish), an asymmetry with the ship's design-then-build.
- **`PDC` and `Fighter` are dangling mount flags** — defined, used by zero templates, served by zero designer.

---

## 4. The fix plan (ranked — cheapest, highest-visibility first)

The work sorts cleanly into four tiers. Tiers 1–2 make the developer's literal request true for every capability whose ability *already* has a universal reader; Tier 3 is the per-capability deep work; Tier 4 is the structural convergence.

### Tier 1 — Make the designers offer parts by mount flag, not by hardcoded host *(cheap, high visibility)*
1. **Fix the mount-flag data (04).** Audit every template's `MountType`; correct the inconsistencies (solar array, battery, sensors-vs-weapons); dedupe the `spaceport` ID; for each capability decide the *intended* host set and author it.
2. **Make the four assembler UIs filter by flag, uniformly** — offer any part whose `MountType` includes the host being built, exactly as the Component Designer already does. This alone lets a radar, reactor, cargo hold, lab, or sensor (abilities that already have universal readers) appear in every builder they're tagged for — the literal *"build it once, slap it on a station or a planet installation"* request.

### Tier 2 — One engine-enforced legality rule *(moderate)*
3. Replace the ~1.5 scattered mount checks with **one shared "can this component mount on this host" rule** used by all four assemblers (and add it to `GroundUnitAssembly.Compute`, which currently ignores the flag). Legality stops depending on which designer you happened to open.

### Tier 3 — Unify the nine parallel ability pairs *(the deep work — the reactor pattern, ranked cheapest first)*
For each pair: keep ONE attribute, make both host processors read it, retire the twin. Suggested order by cost (from 03):
4. **Weapons** — nearly done; finish the in-flight merge.
5. **Cargo / carry** (`CargoStorageAtb` ↔ `GroundBayAtb`) — both already mount on ships; easiest true-merge.
6. **Ammo magazine**, then **shields / evasion**, then **propulsion/mobility**.
7. **Radar** (`SensorReceiverAtb` ↔ `GroundSensorAtb`) — harder, because space detection and ground surface-reveal genuinely do different things; likely "one design carries both abilities, each host reads its half" rather than one attribute.
8. **Armour** and **hazard-resistance** — must first **promote the weaker side to a real component** (ship armour and ground `EnvResistance` aren't components today) before they can be unified.

### Tier 4 — Structural convergence *(largest, do last)*
9. A shared **assembly base / one universal assembler** so ship/station/ground-unit/missile stop being four copies (see `docs/UNIVERSAL-ASSEMBLY-DESIGN.md`).
10. Give **stations a design class**; resolve the **dangling `PDC`/`Fighter`** flags (wire or remove).

---

## 5. How to read the detail files

| File | What it proves | Read it when |
|------|----------------|--------------|
| `01-DESIGNER-UIS.md` | the client has ONE universal designer; the 4 assemblers each hardcode one host flag | fixing Tier 1/2 (the UI filters) |
| `02-DESIGNABLE-TYPES.md` | 5 buildable C# types; mount legality only on `ComponentDesign`; no shared assembly base | fixing Tier 4 (the type hierarchy) |
| `03-ABILITIES-AND-MOUNTS.md` | the ~50 abilities, their readers, the 9 duplicated pairs, the reactor counter-example | **the crux** — fixing Tier 3 (ability unification) |
| `04-BASEMOD-TEMPLATES.md` | 67 templates categorized; the mount-flag data inconsistencies | fixing Tier 1 (the data) |
| `05-ASSEMBLIES.md` | 4 triplicated assemblers; only bottom 2 rungs shared; flag honored by ~1.5/4 | fixing Tier 2/4 |
| `06-INDUSTRY-AND-MATERIALS.md` | industry is ALREADY host-uniform — the pattern to copy | designing the fix (the target shape) |
| `07-RESEARCH-AND-UNLOCKS.md` | unlock/tech is ALREADY host-uniform; not the source of the bug | ruling the tech layer in/out |

**Related existing design docs** (already circling this problem): `docs/UNIVERSAL-ASSEMBLY-DESIGN.md`, `docs/WEAPON-UNIFICATION-DESIGN.md`, `docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md`.
