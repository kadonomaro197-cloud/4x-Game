# The Universal Assembly Principle

**Status:** principle-locked (2026-07-05, the developer's call). Cross-cutting — governs the WHOLE build system.
**Read this before designing or building ANY "buildable thing" at any scale.**

> This is the top-level principle. `docs/GROUND-UNIT-DESIGNER-DESIGN.md` is its first full realization (at unit
> scale); the ship designer is its existing precedent (at ship scale). Everything else buildable is meant to
> converge onto it.

---

## 1. The principle

**Everything a player builds is the SAME kind of object: a _chassis_ that provides a structural budget, plus
_components_ that consume the budget and contribute the thing's capabilities — with every stat emerging from the
sum of the parts, gated by physics (structure vs. mass), at any scale, within reason.**

- A rifleman, a battleship, a mining base, a space station, a super-weapon, a world-ship differ in **number and
  scale, not in kind.**
- "Within reason" is enforced by **physics + tech + scale caps**, never by a category whitelist. (The developer's
  bound: some fiction has machines that turn planets into AU-spanning black holes — that's where the caps live,
  not a hard "you can't build weapons" rule.)
- **The success criterion:** if you can *imagine* a buildable thing and *can't* build its essence, the parts bin
  or the scale ladder has a hole.

This is the same objective the ground-unit system locked ("recreate ANY sci-fi unit's essence"), raised one level:
**recreate any sci-fi *buildable* — unit, ship, installation, station, super-weapon, world-ship — essence-exact.**

---

## 2. Why this is UNIFICATION, not a rewrite

The engine already leans hard this way — we are naming and completing a pattern, not inventing one:

- **Components are already the universal building block** (`CONVENTIONS.md` §6: an ability is a component). A
  weapon, an engine, a sensor, a fortification, a troop bay, a ground weapon — all the same `ComponentDesign`
  shape with an ability attribute.
- **`IConstructableDesign` is the shared "buildable" seam** — `ShipDesign`, `ComponentDesign`, `GroundUnitDesign`,
  `ProcessedMaterial` all implement it and ride the same industry rails.
- **Ships are already an assembly** — `ShipDesign` = a hull + a list of `(ComponentDesign, count)` + armor, and
  `ShipDesign.Recalculate` makes mass/crew/cost/stats the **sum of the parts**.
- **The ground assembler just built (`GroundUnitAssembly.Compute`) is the general-purpose core** — *"a
  budget-provider chassis + consuming parts → emergent stats + a physics gate."* Nothing in its logic is
  ground-specific; it is the reference implementation of this principle.

So the ground-unit track is **the prototype of the universal assembler**, not a siloed feature.

---

## 2a. Components are universal by TYPE, not by SETTING (developer's call, 2026-07-05)

**There is no per-setting component category. One designer per component KIND. To make a weapon you pick a TYPE
(beam / kinetic / missile / melee …) and dial its specs — full stop. The resulting design is setting-agnostic;
whether it ends up on infantry, a tank, a ship, or an installation is decided by the MOUNT + the chassis's carry
capacity, never by a category label.** The same rule holds for every component kind (armor, sensor, engine,
reactor, …): pick the type, spec it, and it's usable anywhere its mount and scale allow.

- **"Ground weapon" is NOT a category.** A laser is a laser whether it's an infantry sidearm, a tank turret, or a
  ship spinal mount — same TYPE, different scale/mount. The player designs *a beam weapon*, then the carry gate +
  the resolver decide where it can fight.
- **The scale/upgrade techs are per-TYPE and setting-agnostic** — "Beam Focusing Range," "Kinetic Yield" — not
  "Ground Weapon Yield." A research investment in beams improves *every* beam, on the ground or in space.

**Unifying (task #3) — progress + what's left:**
- ✅ **Designer category unified (2026-07-05).** The ground weapons' `ComponentType` was `"Ground Weapon"` (a second
  weapon category — `ComponentDesignWindow` groups tabs by `ComponentType`); changed to `"Weapon"` so all weapons sit
  in ONE designer category. The `MountType` (`GroundUnit`/`ShipComponent`) still gates where each installs. Gauge:
  `WeaponScaleGateTests.Weapons_ShareOneDesignerCategory_NotSplitBySetting`.
- ✅ **The per-setting weapon tech reverted.** `tech-ground-weapon-yield` / `tech-category-ground-combat` (a ground-only
  weapon-yield tech) was removed; weapon-scale techs stay per-TYPE (e.g. `tech-beam-range` under Energy Weapons).
- ⏳ **The DEEP one (still owed):** ground combat carries `GroundWeaponAtb` (Attack/Range/Mode, read by the
  `GroundForcesProcessor` hex resolver) as a **separate weapon system** from the space weapon attributes
  (`GenericBeamWeaponAtb`/`RailgunWeaponAtb`/`FlakWeaponAtb`, read by `ShipCombatValueDB` auto-resolve). So an "energy
  weapon" still exists **twice** (`GroundWeaponAtb` `Mode=Energy` vs `GenericBeamWeaponAtb`). **Target:** one universal
  weapon design (TYPE + specs) that BOTH resolvers read; the mount decides the setting. This is the architectural piece
  — it needs a design doc + phased plan (two live combat resolvers) **before** code, not a blind refactor. Sequenced
  with the developer.

---

## 2b. The AXIS pattern — every component designer, not just weapons (developer's call, 2026-07-06)

The weapon taxonomy (`WEAPONS-DESIGN.md`) found the *shape* of a component designer, and **it is the shape
of ALL of them:** you pick a component's **1–2 defining AXES** (a "what/nature" axis, usually × a "how/mode" axis),
dial the **SPECS**, and the component's **role/identity EMERGES from the numbers** — never a picked label. Weapons
proved it (Nature × Delivery → the triangle position emerges from velocity/saturation/tracking). The pattern
generalises; only the axes change per kind:

| Component | Axis 1 — nature / what | Axis 2 — mode / how | Role EMERGES from the specs |
|---|---|---|---|
| **Weapon** | Kinetic · Energy · Explosive · Exotic | Beam · Bolt · Slug · Cloud · Guided · Blast | triangle position (velocity → dodge, saturation → floor, tracking → follow) |
| **Armour / defence** | soak-plate · ablative · reactive · shield · deflector | *vs which nature* it resists best | survivability profile — flat-soak (bounces a swarm) vs %-shield (weak to alpha) vs dodge (beats aimed) |
| **Sensor** | EM/radar · thermal · optical · gravitic · subspace/FTL | **active** (loud, sees far, is seen) vs **passive** (quiet, shorter) | detection profile — range vs stealth vs resolution vs what-bands |
| **Propulsion** | reaction (chem · ion · fusion-torch · antimatter) · field (gravitic · warp · jump) | sublight-maneuver vs strategic-FTL | thrust-vs-efficiency, Δv, FTL reach — a burst fighter drive vs a freighter cruiser vs a jump drive |
| **Power** | fission · fusion · antimatter · exotic (ZPM/naquadah) | baseload vs burst/capacitor | output-per-mass — AND it's the **supply** that gates weapons (a reactor powers a beam; the weapon-merge P2 gate) |

**The load-bearing unification:** the **defence axes are the MIRROR of the weapon axes** — armour resists a *nature*
(kinetic/energy/…), a dodge/shield beats a *delivery* (aimed vs saturation vs guided). So the weapon triangle and the
armour matchup are **two halves of ONE system** — exactly what `GroundDamageMatrix` already is (dodge × shield ×
armour vs a weapon's flavour). Getting weapons' two axes right *defines the defence axes for free*, and the same
`nature × mode → emergent role` engine is reused, not rebuilt, for each kind.

**Build rule:** when we build ANY component designer, name its two axes first, make the role a **computed readout** of
the specs (like `WeaponClass` is becoming), and gate the top of its scale by research (`TechData`, per-type). One
engine, N kinds. This is the through-line for the whole build.

---

## 3. Current state (survey, 2026-07-05)

| Buildable | Assembly of components today? | Structural gate? | Notes |
|-----------|-------------------------------|------------------|-------|
| **Ship** | ✅ `ShipDesign` (hull + components + armor) | ❌ any size; mass just needs thrust | the existing precedent |
| **Ground unit** | ✅ `GroundUnitAssembly` (frame + parts) | ✅ **the carry gate (new)** | the prototype + first full realization |
| **Installation** (bunker/factory) | ➖ a *single* component; the **colony** is the assembly of installed components | ❌ | leaf part, not itself an assembly |
| **Station** | ➖ a *host* that accumulates installed components (colony-like) | ❌ | `Stations/` — being generalized off `PlanetEntity` (see `OFF-WORLD-INFRASTRUCTURE-DESIGN.md`) |
| **Super-weapon** | ⚫ doesn't exist | — | would be a huge designed assembly (ship-like) or a colossal installation |
| **World-ship / megastructure** | ⚫ doesn't exist | — | a *mobile host* — a colony/station that flies (bridges the two patterns) |

### The two patterns today (and how they unify)

1. **Designed assembly** — you design the thing as a bag of parts, then build copies. (`ShipDesign`,
   `GroundUnitAssembly`.)
2. **Host accumulates components** — a place (colony, station) that holds installations you build one at a time.

Under the universal principle these are the **same** thing at different mobility/scale: a **chassis** (hull /
frame / foundation / station-core / world-ship-spine) provides a structural budget, and **components** fill it.
A colony is "a planet-sized immobile chassis." A world-ship is "a colony that flies." The distinction is a
*mobility flag + a scale number*, not a different system.

---

## 4. The convergence roadmap (incremental, NON-breaking)

Do **not** big-bang refactor the working ship/station systems. Converge deliberately:

1. **Record the principle** (this doc). ✅
2. **Harden the assembler on ground** — the proving ground (G-D track). *(in progress)*
3. **Generalize the assembler + gate** to a scale-agnostic engine: the "budget-provider attribute + consuming
   attributes" model, with a **per-domain structural gate** (a ship's gate = hull integrity; a station's = its
   foundation/core; a ground unit's = frame strength). Ships opt in without losing `Recalculate`.
4. **Extend the scale ladder** — add chassis at new scales as we build them: super-weapon platforms (huge designed
   assemblies), world-ships (mobile hosts). Tech + scale caps are the "within reason" bound.
5. **Converge the designer UX last** — one "pick a chassis, add parts, see the budget bar + emergent stats + cost"
   window, scale-agnostic (unit → ship → station → super-weapon → world-ship). This resolves the
   `GROUND-UNIT-DESIGNER-DESIGN.md` §11 convergence question: **yes, converge.**

**Rule (unchanged):** every new gameplay number (chassis budgets, gate fractions, part stats, scale caps) is a
flagged JSON default in the slice report — never silently hardcoded.

---

## 5. Open questions (to design before the later scales)

- **The structural gate per domain.** Ground uses strength-vs-mass. What is a *ship's* gate (hull integrity vs.
  mounted mass?), a *station's* (foundation/core capacity?), a *super-weapon's*? Or is the gate optional per
  domain?
- **Mobility as a flag.** Is "colony vs. station vs. world-ship" really just a mobility flag + scale on one host
  model? (The stations work already points here — `OFF-WORLD-INFRASTRUCTURE-DESIGN.md`.)
- **Scale caps.** Where do the "within reason" ceilings live (tech tree? a hard engine cap?) so the AU-black-hole
  gun is reachable only if the developer wants it and is bounded.
- **UX convergence.** One designer window or a shared component with per-scale skins?

None of these block the current ground track — they're the map for taking the principle game-wide.
