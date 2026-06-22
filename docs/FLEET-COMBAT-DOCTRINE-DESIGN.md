# Fleet Combat Doctrine — Design Capture

**Status:** Design only. Nothing here is built yet. This is the developer's concept written down so it isn't lost, with the connections to existing code mapped so a future session can implement it without re-deriving everything.

**Captured:** 2026-06-22, from the developer's description.

---

## The point first (what it is and why it matters)

Right now a fleet is just a bag of ships that all move and fight the same way. This design gives a fleet **internal structure and a brain**: you split a fleet into **components** (Front Line, Flank, Rear Guard, Artillery, etc.), and each component runs a **combat doctrine** you can set and forget or change on the fly. A doctrine is a standing order with trade-offs — it makes the component better at one thing and worse at another, and whether that trade is good depends on the battle.

Think of it like **general quarters stations on a ship**: each station has a job and a posture. You can tell the forward battery to go to a defensive posture (slower rate of fire, but it's screening the fleet) without touching the rest of the ship. The captain sets the posture; a good watch officer can be given the authority to change it when the situation shifts.

Why it matters: it's depth that doesn't require rendering or micromanaging 100 ships. The player commands **postures and groupings**, reads a **table of ship stats**, and the engine resolves the rest. It also gives the NPC AI something concrete to "decide" — which plugs into the doctrine weights that already exist for NPC factions.

---

## Core concepts

### 1. Components (the grouping)
- A fleet is divided into named **components**: Front Line, Flank, Rear Guard, Artillery, … (the set is open-ended; treat the names as data, not hard-coded).
- **There are no rules about how ships are distributed.** The player (or NPC) puts whatever ships they want into whatever component they want. A component can hold 1 ship or 50.
- A ship belongs to exactly one component of its fleet at a time.

### 2. Doctrine (the posture)
Each component has **one active combat doctrine** at a time, chosen from three families:

| Family | Intent | Feel |
|--------|--------|------|
| **Offensive** | Maximize damage output / pressure | Trade safety and economy for killing power |
| **Defensive** | Maximize survival / screening | Trade offense for protection and staying power |
| **Utilitarian** | Support / efficiency / flexibility | Trade peak performance for fuel, repair, sensors, EW, etc. |

Within each family there are **multiple named options**, each with **both positive and negative effects** whose value depends on the situation. The options are the interesting part — they are where the player's judgment lives.

**Worked example (the developer's):** Front Line component, **Defensive** doctrine, option **"Fighter Screen"**:
- − Main-weapon fire rate −25% (they're flying screen, not slugging).
- − Overall fleet movement speed reduced (the screen sets the fleet's pace).
- + (implied) better protection / interception for the ships behind them.

The sign of each effect is fixed; whether the **net** is good depends on whether you're being swarmed by ordnance (great) or trying to win a gun duel (bad).

### 3. Switching (set-and-forget OR real-time)
- Doctrines can be **set once and ignored**, or **changed during combat in real time** by the player or NPC.
- **A cooldown timer gates switching** so you (or the AI) can't spam-flip postures every tick. (Posture changes are like re-stationing the crew — they take time to take effect.)
- Because game time is compressed, the cooldown is measured in **game time**, not wall-clock.

### 4. Operational discretion (delegating to commanders)
- A fleet commander (a People/Commander entity) can be granted **"operational discretion"** — an option set that lets *that commander* switch the doctrines of *certain components* on their own.
- The player or NPC faction decides **which** commanders get discretion and over **which** components. (Captain delegates to the watch officer, but only for the stations he trusts him with.)
- With discretion off, only the player/faction changes doctrines. With it on, the commander reacts inside the bounds you set.

### 5. Who drives it
- **Player** sets components, doctrines, and discretion through the UI.
- **NPC factions** do the same through their decision logic — this is the natural consumer of the existing `DoctrineVector` (see connections below).

---

## The four connection questions (Prime Directive)

Before any of this is built, here is how it ties into what already exists. Read these files first.

### What feeds INTO this system?
| Input | Where it lives today | Note |
|-------|---------------------|------|
| Fleet + ship membership | `GameEngine/Fleets/FleetDB.cs` (extends `TreeHierarchyDB`); ships assigned via `FleetOrder.AssignShip` | Components are a **grouping inside a fleet** — see "Components as sub-fleets" below |
| Commander entities | `GameEngine/People/` (Commander), referenced by fleets | Source of "operational discretion" holders |
| Doctrine definitions | **NEW JSON** under `GameData/basemod/` | Make doctrines moddable data, not hard-coded C# (see data model) |
| NPC strategic weights | `GameEngine/Factions/DoctrineVector.cs` (Economic/Military/Tech/Expansion) + `FactionInfoDB.Doctrine` | Already exists — NPC doctrine choice should read this |

### What does this system feed INTO?
| Output | Consumer today | Effect to apply |
|--------|----------------|-----------------|
| Weapon fire-rate multiplier | `Weapons/WeaponGeneric/WeaponState.cs` (reload/heat) + `GenericFiringWeaponsProcessor.cs` | There is already precedent: thermal suppression throttles fire rate. A doctrine multiplier rides the same lever. |
| Fleet movement speed | Fleet movement (`Movement/`, fleet orders) | "Screen sets the pace" = a fleet-speed multiplier |
| Hit chance / targeting priority | `Weapons/WeaponBeam/BeamWeaponProcessor.cs` (`CalculateHit`, `BaseHitChance`) and fire control | Offensive/Defensive options can nudge these |
| Damage taken / screening | `Damage/` path (`DamageProcessor.OnTakingDamage`) | Defensive options can bias which component soaks hits |

### What shares STATE with this system?
- The **same ship entities** are shared with movement, damage, sensors, and cargo. A doctrine effect is a **modifier layered on top** of a ship's base stats — it must not overwrite base values (store the multiplier, apply it at read time, exactly like a buff). Match the existing `BonusesDB`/modifier pattern rather than mutating component stats in place.
- `FleetDB` is shared with the existing fleet order system — adding component/doctrine state must not break `FleetOrder` / `FleetOrderProcessor`.

### What does this system TRIGGER?
- A doctrine change **publishes an event** and **starts a cooldown** on that component.
- The NPC decision processor (`Factions/NPCDecisionProcessor.cs`, currently a skeleton) would **issue doctrine-change orders** the same way the player does — through an order/command, not by poking state directly.
- Look one hop further: changing the Front Line to a slow screen changes **fleet movement**, which changes **time-to-target**, which changes **fuel** and **interception geometry**. Doctrine is not a local knob.

---

## Components as sub-fleets (the cheap path)

Pulsar fleets **already nest** — `FleetDB extends TreeHierarchyDB`, and sub-fleets can inherit orders, detach, and reattach (`FleetOrder.ChangeParent`). The lowest-effort, lowest-risk way to get "components" is to make each component a **sub-fleet** of the parent fleet:

- Front Line / Flank / Rear Guard / Artillery = named sub-fleets under the battle fleet.
- Ship assignment, movement inheritance, and detachment **already work** — no new membership system.
- The **new** piece is a per-component **doctrine state** (active doctrine + cooldown), plus the effect application.

Trade-off to decide: sub-fleets reuse a lot, but if you want a ship to be in a component **without** the full sub-fleet order machinery, a lighter `FleetComponentDB` on the parent fleet (mapping component name → ship IDs + doctrine state) may be cleaner. **Recommendation:** prototype with sub-fleets first; only build a dedicated DataBlob if sub-fleets get in the way. (See `Fleets/CLAUDE.md`.)

---

## Proposed data model (sketch — confirm against `CONVENTIONS.md` before building)

**Doctrines as moddable JSON** (a new `Blueprint` type, loaded by `ModLoader`):
```
CombatDoctrineBlueprint : Blueprint
  UniqueID        e.g. "fighter-screen"
  Family          Offensive | Defensive | Utilitarian
  DisplayName     "Fighter Screen"
  Effects         list of { Stat, Multiplier/Delta }   // e.g. WeaponFireRate × 0.75, FleetSpeed × 0.8
  CooldownSeconds game-time gate before it can change again
```
Effects are **data**, so you can add/tune doctrines without code (this also lets the integrity test, below, validate them).

**Per-component runtime state** (new DataBlob on the component/sub-fleet entity, or entries in a `FleetComponentDB`):
```
ActiveDoctrineId    string (UniqueID of the CombatDoctrineBlueprint)
DoctrineCooldownUntil DateTime (game time)
ComponentRole       "FrontLine" | "Flank" | ...   // also data-driven
```

**Operational discretion** (on the commander or the fleet):
```
DiscretionComponents  set of component roles this commander may re-posture
```

**Effect application:** a processor (or read-time modifier layer) turns `ActiveDoctrineId.Effects` into the live multipliers consumed by the weapon/movement/damage code listed above. **Do not bake the multiplier into base stats** — apply it at read time so toggling a doctrine is reversible.

---

## UI (you can't draw 100 ships — so don't)

- New **Fleet Combat panel** (extend `FleetWindow.cs`, which already lists/selects fleets):
  - Pick a fleet → see its **components** as tabs or a list.
  - Select a component → a **table of its ships** with columns for the stats and systems that matter (weapons, armor/health, speed, fuel, current target). This is the `Helpers.RenderImgUITextTable` pattern already used elsewhere.
  - A **doctrine dropdown** per component (greyed out while the cooldown is running, with the remaining game-time shown).
  - A **discretion** toggle/list for assigning commander authority.
- No system-map rendering of individual ships is required — the table *is* the interface. (This mirrors how Aurora handles large fleets.)

---

## Open decisions for the developer

1. **Components = sub-fleets, or a new `FleetComponentDB`?** (Recommend sub-fleets first.)
2. **Effect set:** which stats can doctrines touch in v1? (Suggest starting with the two from your example: weapon fire rate and fleet speed — both already have a clear lever — then expand.)
3. **Cooldown length** and whether it's per-doctrine (data) or global.
4. **Discretion granularity:** per-component-role, or per-specific-component-instance?
5. **NPC behaviour:** how `DoctrineVector` weights map to doctrine-family choice (e.g. high Military → favour Offensive).
6. **Stacking:** can a fleet-wide posture and a component posture both apply? (Recommend: component overrides fleet to keep it simple.)

---

## Before implementing — read these

- `CONVENTIONS.md` (DataBlob idioms, `[JsonProperty]`, modifier patterns)
- `GameEngine/Fleets/CLAUDE.md` (sub-fleet tree hierarchy, `FleetOrder`)
- `GameEngine/Weapons/CLAUDE.md` (`WeaponState`, fire-rate / thermal throttle — the existing fire-rate lever)
- `GameEngine/Factions/CLAUDE.md` (`DoctrineVector`, `NPCDecisionProcessor` skeleton — the NPC hook)
- `docs/COMBAT-DESIGN.md` (overall combat design context)
- Add tests: doctrines are data, so a base-mod integrity test should assert every `CombatDoctrineBlueprint` is valid and every referenced doctrine id exists — same pattern as `Pulsar4X.Tests/Modding/BaseModIntegrityTests.cs`.
