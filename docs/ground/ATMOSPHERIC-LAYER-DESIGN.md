# The Atmospheric Layer — aircraft that fly and fight over a planet

**As of 2026-08-08. DESIGN ONLY — no engine work yet (the developer's explicit call).** How an aircraft — an F-22, an
AH-64 Apache, a Death Glider, a LAAT gunship — is *designed, based, moved, and fought* over a planet surface, grounded in
what the engine and the designers already do. **All six design decisions are now LOCKED (see §11).** This doc is the
record; the engine slices wait for a separate go-ahead.

> **One-line version:** an aircraft is a **ground unit that carries an altitude**, altitude is **a third leg on the
> distance ruler the ground fight already uses**, and it **flies sorties from a base** whose endurance falls out of its
> fuel + engine + mass by the same arithmetic a ship's range already does. From those, air-to-air, close air support, and
> anti-air fall out **through the combat math that already exists** — no air domain, no air map, no air resolver.

---

## 1. The point (plain English)

Pulsar4X has exactly two boxes today: **ships in space**, and **ground units marching on the surface**. There is nothing
in between — no altitude, no airspace, no dogfight, no anti-aircraft. An aircraft is a *pure flyer over a surface*, and it
has no native home. That gap is real and already flagged as the single biggest missing planetary system.

An aircraft is the cleanest thing to expose it, and the F-22 and the Apache expose it two different ways:

- The **F-22 Raptor** is an **air-superiority** fighter — its whole job is to kill *other aircraft* and own the sky.
- The **AH-64 Apache** is **close air support (CAS)** — loiter low over the battle and pound *ground* troops.

This design gives both a home **without building a new combat domain** — because the engine already has the pieces that
matter (a shared damage kernel, a real-distance range gate, and an emergent fuel/endurance model), and your own chassis
designer already draws the altitude bands. We are wiring what exists, not inventing a parallel system.

---

## 2. The core insight — altitude is a third leg on the ruler

The ground resolver already decides "can this weapon hit that target?" by measuring the **real gap in metres** between two
units and comparing it to the weapon's **real range in metres** — one function, `GroundMiniHex.RealGapMetres` (`GroundMiniHex.cs:130`),
fed into the shared gate `CombatKernel.WithinReach` (`GroundForcesProcessor.cs:568`) and the dodge accuracy
`CombatKernel.HitFraction` (`GroundForcesProcessor.cs:423`).

**Give each unit an altitude in metres, and change one function:**

```
RealGap3D(a, b) = √( RealGapMetres(a, b)²  +  (altitude_a − altitude_b)² )
```

Pythagoras with a vertical leg. Feed `RealGap3D` where the resolver feeds `RealGapMetres`, behind a flag, and:

- It is **byte-identical for an all-surface battle** — at equal altitude the height leg is zero and `RealGap3D` collapses
  to `RealGapMetres`, so every existing gauge stays green with the flag off.
- All three air interactions fall out of the **unchanged** shared kernel:

| Interaction | How it emerges |
|---|---|
| **Air-to-air** (F-22) | Two units at altitude, close diagonal gap → the ordinary kernel fight. Fighters carry high **evasion**, so `HitFraction` lets only **guided / high-tracking** weapons land — you can't cannon a jet; a missile can. Thin armour → one hit kills. |
| **Air-to-ground / CAS** (Apache) | An airborne unit fires **down**; the height leg means only a **long-range** weapon reaches the surface, and any enemy that can shoot *up* fires back **in the same loop** — CAS is safe only once the sky is won. |
| **Ground-to-air / anti-air** | A surface weapon fires **up** only if (a) its **`EngageBands` explicitly includes** the flyer's band *and* (b) its range covers the diagonal slant. A dedicated SAM does; a tank gun does not (see §11 Call 3). |

**One honest change from the first draft:** you chose an **explicit "engages-air" weapon property** over letting anti-air
emerge from pure range (§11 Call 3). So AA is *one small weapon dial + the geometry* — not "no anti-air code," but nothing
like a whole anti-air subsystem either. The dial buys you **control**: you decide exactly what can shoot up, so a
coincidentally-long-ranged artillery piece isn't secretly a SAM.

### 2a. Endurance is emergent — the reason "sortie-based" is cheap (verified)

You spotted this, and it checks out in source. A ship's operational **range / endurance / delta-V is never authored** —
it *falls out* of the parts you mount:

- thrust = `ExhaustVelocity × FuelBurnRate` (`NewtonionThrustAtb.cs:57`) — a product of dials, not a typed-in number;
- delta-V = the Tsiolkovsky rocket equation on wet/dry mass (`NewtonThrustAbilityDB.cs:51` private-set; `OrbitalMath.cs:1461`);
- endurance = `BurnTime = fuel ÷ burn rate` (`OrbitMath.cs:443`);
- `ShipDesign` carries **no** range/endurance/dV field at all — they're computed on demand.

The propulsion door already states the rule in plain English: acceleration / dV / range are **"emergent — every one needs
the finished hull's mass … an emergent readout, never a dial"** (`propulsionderived.html:230,240`). So when you design an
F-22 — fuel tank + jet drive (exhaust velocity + burn rate) + mass — its **loiter time, combat radius, and sortie range
fall out by the same arithmetic, with no aircraft-specific stat authored.** That is why "sortie-based air power" (§11
Call 1) is *cheap*: the numbers are free by construction; only the launch/recover *loop* is new, and it reuses patterns
already in the engine. **One caveat:** today's model is a rocket delta-V budget (impulse against vacuum), not aerodynamic
drag — the *structure* needs no separate endurance stat, but a true jet's loiter-against-drag would add a drag/flight-time
term on the air-breathing drive later. The emergent shape is already exactly right to carry it.

---

## 3. Why NOT a new air domain (three depths considered)

Three depths were worked and judged against the project laws. The middle won decisively.

| Depth | What it is | Verdict |
|---|---|---|
| **A — Overlay** | binary Surface/Airborne tag + a weapon band-mask | Cheapest core, but a *categorical* gate where a geometric one exists, and not a clean graft toward C. Rejected. |
| **B — Third plane** | a real air battle-plane, live climb/dive, a separate air resolver, an `Air` domain | Richest, but XL for a deferred layer; a new verb + opening the closed `{Both,Space,Ground}` enum, both of which the laws push back on. Reserved as an optional later graft. |
| **C — Environment-unified** ✅ | altitude = a band on the unit; the 3D-gap swap into the *existing* gate | Cheapest design that still delivers the full decision set; highest reuse; zero new AI code for the static spine. **The recommendation, and the locked spine.** |

---

## 4. The design (Depth C, all rulings locked)

### 4a. Position, altitude, and basing — movement

An aircraft is a plain **`GroundUnit`** in the planet body's `GroundForcesDB` — **never a ship, never enrolled in the
space resolver** (this holds the same ship/ground symmetry the engine already keeps).

- **Horizontal position** reuses the existing continuous field: the `(regional hex)(mini hex)` address + sub-tile offset,
  measured by `GroundMiniHex.RealGapMetres`.
- **Terrain freedom** is the already-scoped SMALL ADD (`GROUND-UNIT-VARIABLES.md` Q3): a **`Flight` dial** on
  `GroundLocomotionAtb` (same shape as its existing `Amphibious` dial, `GroundLocomotionAtb.cs:32`) + a guarded branch in
  the two pure functions the march routes through — `HexPathfinder.IsImpassable` (ocean passable) and `HexMoveMult`
  (open cost). A flyer crosses water and mountains at open-ground speed.
- **Altitude** is **one band field**, and it is **design-time, not a live maneuver** (§11 Call 2). **Three bands** —
  **Low (terrain-following) / Medium / High** (§11 Call 4); **near-space is deferred** (the chassis door's 4th band is
  authored only if/when space-coupling is designed, §11 Call 5). Each band maps to a real-metre altitude, so `RealGap3D`
  works in true metres. An F-22 *is* a high-altitude interceptor; an Apache *is* a low gunship — fixed for the fight.
- **Basing + sortie (§11 Call 1).** An aircraft has a **home base** (an airfield building, or a carrier) and cycles
  Based → Airborne → Returning → Rearming:
  - **Endurance is emergent** (§2a) — fuel ÷ burn rate; no authored loiter stat. The one new runtime field is a **fuel
    pool on the aircraft, mirroring the ammo pool it already carries** (`GroundUnit.MaxAmmo_kg`/`CurrentAmmo_kg`,
    `GroundForcesDB.cs:67-71`, snapshot at raise `:612-614`).
  - **Launch/recover is an ORDER, not a bespoke call** — it reuses the docking undock/dock order the engine is already
    adding (`Docking/DockTools`, "the order is the next slice") for a carrier-based aircraft, or a return-to-base order
    for a land-based one. Because it's an order, **the AI drives it identically** (One Verb, Both Seats — §6).
  - **Rearm/refuel at base reuses friendly-ground resupply** (`GroundForces.ResupplyUnit`, `GroundForcesDB.cs:664`, which
    already tops a unit's pool to full on friendly-held ground).
  - **The sortie loop is a ground-tick step** (L9 — inside the existing hotloop, not a new processor): an airborne
    aircraft burns fuel; at low fuel it auto-returns (or the player recalls); at base it refuels/rearms; relaunch.

### 4b. Combat — one resolver, one kernel, one changed argument + one weapon dial

Add `GroundMiniHex.RealGap3D` (§2) and feed it wherever the resolver feeds `RealGapMetres` — the range gate
(`GroundForcesProcessor.cs:479/506/561`→`:568`) and dodge accuracy (`:423`) — behind an `EnableAltitudeCombat` flag
(menu-on, default off). From that one substitution the three interactions fall out of the **unchanged** shared
`CombatKernel`.

**The one new weapon dial (§11 Call 3): `EngageBands`.** Beside the geometric range gate, a weapon carries an explicit
mask of which altitude bands it may fire at. A shot lands only if **`EngageBands` includes the target's band AND** the 3D
range covers the slant. So a SAM (`EngageBands ⊇ {air bands}`, long range) is your anti-air; a rifle (`{Surface}`) can't
touch a flyer regardless of geometry; a tank gun with huge range but `{Surface}` can't accidentally snipe jets. It passes
derive-don't-invent — it writes a variable the resolver reads at the gate.

CAS also **generalizes** the existing orbital-bombardment routine (`DamageProcessor.ApplyGroundBombardment`,
`DamageProcessor.cs:349`) past its three limits — but that's a **later, flag-gated slice** (§11 Call 6) because it touches
the live colony-bombardment wire.

### 4c. Designer expression — derive, don't invent

Almost entirely reuse:

- **Chassis door** — author the empty **`Environment = Atmospheric`** cell; its **operating-level chips ARE the bands**
  (Low/Medium/High), and the tech-capped **"operating envelope"** slider sets which band(s) a unit occupies. Selecting
  Atmospheric **derives** the unit's home band — not a free slider (the DESIGNER-NORTH-STAR intrinsic-test boundary).
- **Propulsion door** — the drive is a **Reaction drive with `MediumRequirement = Atmosphere`** (a jet scoops air); works
  in atmospheric bands, dies in vacuum. Its fuel + burn rate is what makes endurance emergent (§2a).
- **Weapon door** — one new `EngageBands` dial (§4b).
- **Combat doctrine gains nothing** — an air unit is a **Ground-domain** unit; `DoctrineDomain` stays `{Both, Space,
  Ground}`. One triangle.

**The two test units:** **F-22** = Atmospheric chassis · High band · jet drive · long-range guided A2A weapons
(`EngageBands ⊇ air`) · high evasion. **Apache** = Atmospheric chassis · Low band · rotor drive · high-penetration CAS
weapons (`EngageBands ⊇ {Surface}`).

---

## 5. EXISTS / EXTEND / NEW ledger (file:line)

Verified against source 2026-08-08.

| Piece | State | Where |
|---|---|---|
| Shared damage kernel (dodge/shield/armour/penetration/nature) | **EXISTS — reused verbatim** | `Combat/CombatKernel.cs` (used by both resolvers) |
| The ground range gate | **EXISTS — reused verbatim** | `GroundForcesProcessor.cs:561-569` → `CombatKernel.WithinReach` fed by `RealGapMetres` |
| Emergent endurance/range (fuel ÷ burn, Tsiolkovsky) | **EXISTS — the model an aircraft reuses** | `NewtonionThrustAtb.cs:57`, `NewtonThrustAbilityDB.cs:51`, `OrbitMath.cs:443`, `OrbitalMath.cs:1461` |
| The ammo POOL pattern (to mirror for fuel) | **EXISTS — pattern to copy** | `GroundForcesDB.cs:67-71` (`MaxAmmo_kg`/`CurrentAmmo_kg`), `:664` (`ResupplyUnit`) |
| Docking dock/undock order + carrier StrikeCraft launch/recover | **EXISTS — the sortie launch/recover reuses it** | `Docking/DockTools` ("the order is the next slice"); `docs/combat/CARRIER-DESIGN.md` |
| The live distance function | **EXTEND (add `RealGap3D`)** | `GroundMiniHex.cs:130` `RealGapMetres` → 2D→3D sibling; band-0 identity = byte-identical |
| The dodge accuracy call | **EXTEND (feed the 3D gap)** | `GroundForcesProcessor.cs:423` |
| Terrain freedom for a flyer | **EXTEND (`Flight` dial + 2 guarded branches)** | `GroundLocomotionAtb.cs` + `HexPathfinder.IsImpassable`/`HexMoveMult` |
| Altitude band on the unit | **NEW (one int field + a 3-value metre lookup)** | on `GroundUnit`, snapshot at `RaiseUnit` like `Range_m`/`Speed_kmh` |
| Fuel/endurance runtime field on the aircraft | **NEW (mirrors the ammo pool)** | on `GroundUnit`, beside `MaxAmmo_kg` |
| `EngageBands` weapon property (the air gate) | **NEW (one weapon dial, derive-clean)** | on the weapon atb; checked beside `WeaponReaches` |
| CAS = above-hits-below, armour-aware | **EXISTS — generalize its 3 limits (later slice)** | `DamageProcessor.cs:349` |
| Air combat domain / air map / air resolver | **NOT NEEDED — deliberately none** | `DoctrineDomain` stays `{Both,Space,Ground}` (`TargetPriority.cs:40-48`) |

---

## 6. One Verb, Both Seats

- **Altitude adds no verb** — it's a static design property (§11 Call 2), so zero new AI code for positioning; an F-22 is
  one more unit in `GroundTacticalBrain`'s roster, driven by the same move/stance orders as a tank.
- **The sortie loop's only verb is launch/recover**, and it **reuses the docking undock/dock order** (or a return-to-base
  order) the engine is already adding — *not* a bespoke bypass. Because it's a queued order, the AI issues it identically
  to the player. That is the whole reason Call 1's sortie model stays legal under One-Verb.
- **`EngageBands`** is a design-time weapon property, not an order — nothing to drive at runtime.

The law holds: everything the player does to fly, base, and fight an aircraft, the AI does through the same primitives.

---

## 7. Cradle to grave

> **bauxite / titanium / REE** (mined) → **aluminium / titanium alloy + electronics** (refined, reuse already-unlocked
> materials) → a **`GroundUnitDesign`** built on the industry rails → carrying an **Atmospheric chassis + medium-gated jet
> drive (fuel tank + burn rate) + band-aware weapons (`EngageBands`) + Flight locomotion** (components → research/cost/
> save/design-UI for free) → gated by **tech** (Atmospheric chassis, envelope-widening techs, guided AA, higher-thrust
> jets) → **based** at an airfield/carrier, **launched** into a band → the **in-play levers** (*win the sky → open the CAS
> window*; *deny the sky with SAMs*; *manage sortie tempo — commit air now or hold fuel/airframes*) → **destroyed / lost**:
> shot down (`Health ≤ 0`), or shoot off its **drive** (grounded) / **sensor** (blinded), or it runs dry between bases —
> re-research / re-mine / re-build.

---

## 8. The player decisions it creates

1. **Win the sky first, or don't** — air superiority gates safe CAS.
2. **Deny the sky** — building SAMs (`EngageBands ⊇ air`) makes a no-fly zone the enemy's CAS can't enter.
3. **Altitude as a design trade** — a high interceptor out-reaches everything but can't strafe; a low gunship is deadly to
   ground but exposed to AA. The band you build for is a fork.
4. **CAS cracks the stalemate** — a high-penetration gunship punches armour that has stalled your infantry.
5. **Sortie tempo (new, from Call 1)** — aircraft launch, loiter for their emergent endurance, and must return to
   refuel/rearm. *When* you commit your air, and whether your basing keeps it on-station, is a real availability decision —
   the air arm can't be everywhere at once.

---

## 9. Scope honesty

A **ceiling-raiser** stacked on the ground fight that *is* the MVP — build the surface fight first. With the rulings, the
spine is **S–M plus the sortie loop (M)**. The honest remaining ceiling: **no aerodynamic drag model** (endurance is a
rocket-style fuel budget, flagged in §2a), **no live climb/dive** (design-time altitude, Call 2), and **no near-space ↔
space coupling** (Call 5). Those are later / Depth-B territory.

---

## 10. Build slices (for WHEN engine work is authorized — not now)

CI-gated, one per push, both jobs green before the next.

| Slice | Ships | Gauge |
|---|---|---|
| **A0** | `Band` field (3 bands) + `RealGap3D` + metre lookup, unread | Band round-trips; `RealGap3D == RealGapMetres` at equal band (byte-identical) |
| **A1** | `Flight` locomotion dial + the two Q3 guarded branches | A flyer crosses ocean/mountain at open cost; others unchanged |
| **A2** | Feed `RealGap3D` into the range gate + dodge, behind `EnableAltitudeCombat` | Rifle can't reach a high flyer, SAM can, two flyers fight; existing gauges green flag-off |
| **A3** | `EngageBands` weapon dial + the gate check | A SAM downs a flyer; a `{Surface}` tank gun can't, even in range |
| **A4** | Author the Atmospheric chassis + jet drive medium-gate + F-22/Apache/SAM presets | `BaseModIntegrityTests` binds them; a jet in vacuum is rejected |
| **A5** | Fuel pool (mirrors ammo) + basing state + the sortie loop (launch/recover order + resupply-at-base) | An aircraft launches, burns fuel, auto-returns dry, refuels, relaunches; the AI runs the same loop |
| **A6** *(opt)* | Unify CAS through the salvo (Call 6-C) — generalize `ApplyGroundBombardment`, orbital byte-identical | CAS region+enemy-scoped; orbital unchanged; bombardment gauges green |
| **A7** | AI values air: extend `GroundTactics.DecidePosture` — clear sky before CAS, build AA when out-ranged aloft, manage sorties | AI wins the sky then strafes; builds AA when it lacks fighters |

---

## 11. RESOLVED DECISIONS (locked 2026-08-08)

| # | Decision | Ruling | Why |
|---|---|---|---|
| **1** | Persistent vs **sortie-based** air power | **Sortie-based (B).** Aircraft launch from a base, loiter for their endurance, must return to refuel/rearm. | The developer's insight, verified (§2a): endurance/range **fall out of fuel + engine + mass** with no authored stat, so sortie is cheap — only the launch/recover loop is new, and it reuses docking + carrier + resupply. Adds a real *availability/tempo* decision. |
| **2** | **Design-time** vs live altitude | **Design-time (A).** A unit's band is fixed when built; no climb/dive order. | Delivers the full win-sky/CAS/AA decision at the lowest cost, and keeps One-Verb trivially (no altitude verb, zero new AI code). Live altitude reserved as a later graft only if playtesting earns it. |
| **3** | Emergent vs **explicit** anti-air | **Explicit gate (B).** A weapon carries an `EngageBands` mask of which bands it can fire at, checked beside the geometric range gate. | You wanted **control** over exactly what can shoot air (so a long-ranged artillery piece isn't secretly a SAM). One small derive-clean weapon dial, not a subsystem. |
| **4** | Band count | **3 bands (B):** Low (terrain-following) / Medium / High. | Enough spread for real lanes (interceptor vs gunship); defers near-space, which is the only band that flirts with orbit. |
| **5** | Near-space ↔ space coupling | **Flavor only (A)** — and moot, since Call 4 defers the near-space band. The only air↔space link stays the existing orbital-bombardment edge. | Keeps the air layer a clean ground-plane overlay; space coupling is a separate later design. |
| **6** | CAS unification timing | **Unify the function, orbital byte-identical (C).** Generalize `ApplyGroundBombardment` to take a region + enemy filter; orbital passes neither (unchanged), CAS passes both. | One "damage from above" routine, no parallel path, and orbital behavior can't change because it doesn't use the new filters. A later, flag-gated slice (A6). |

---

## 12. Connections (Prime Directive)

- **Ground combat / `GroundForcesProcessor` + `GroundForcesDB`** — an aircraft is a `GroundUnit`; moves, fights, and now
  bases + sorties on the same hotloop. The 3D gap + `EngageBands` change only the range/target read.
- **The shared kernel / `Combat.CombatKernel`** — the one resolver both space and ground use; the air fight rides it
  untouched.
- **Distance / `GroundMiniHex`** — `RealGapMetres` is the seam; `RealGap3D` extends it.
- **Movement / `HexPathfinder` + `GroundLocomotionAtb`** — the `Flight` dial + two guarded branches.
- **Propulsion / thrust model (`NewtonThrustAbilityDB` + `OrbitMath.BurnTime`)** — the emergent endurance an aircraft's
  sortie loop reads.
- **Docking + Carrier (`Docking/DockTools`, `CARRIER-DESIGN.md`)** — the launch/recover order the sortie loop reuses.
- **Ammo/resupply (`GroundAmmo` / `GroundForces.ResupplyUnit`)** — the pool pattern the fuel field mirrors, and the
  rearm-at-base mechanic.
- **Damage / `DamageProcessor.ApplyGroundBombardment`** — the CAS primitive to generalize (Call 6, slice A6).
- **AI / `GroundTacticalBrain` + `GroundTactics`** — no new code for the static spine; a `DecidePosture` extension for
  air-superiority sequencing + sortie management (slice A7).
- **Designers / chassis + propulsion + weapon doors** — the Atmospheric cell + bands + medium-gate + `EngageBands`.

---

## 13. Sources

- **This session's source verification** — the two-box model (no `Air` `DoctrineDomain`, `TargetPriority.cs:40-48`); the
  shared kernel; the `RealGapMetres`/`WeaponReaches`/`HitFraction` seams; the chassis door's Atmospheric cell + bands +
  envelope (`chassisderived.html`); the propulsion door's "air is Reaction + a medium requirement" and
  "endurance/range/dV are emergent readouts, never a dial" (`propulsionderived.html:230,240`); the emergent thrust/dV/
  endurance chain (`NewtonionThrustAtb.cs:57`, `NewtonThrustAbilityDB.cs:51`, `OrbitMath.cs:443`, `OrbitalMath.cs:1461`);
  the ground ammo-pool pattern (`GroundForcesDB.cs:67-71,664`); docking (`Docking/DockTools`).
- `docs/ground/GROUND-UNIT-VARIABLES.md` Q3 — the flyer-over-terrain `Flight`/`IgnoresTerrain` dial as a SMALL ADD.
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §13.4 (multi-plane BattleTheater, air layer parked) + §12 (real-distance
  ground combat).
- `docs/combat/CARRIER-DESIGN.md` — the StrikeCraft launch→strike→recover→rearm model the sortie loop echoes.
- `docs/REALISM-VS-GAMEPLAY-AUDIT.md` (name the decision); root `CLAUDE.md` (One Verb Both Seats; Cradle to Grave);
  `docs/economy/DESIGNER-NORTH-STAR.md` (derive-don't-invent, the intrinsic test).

*Design/reference only — no franchise assets; the F-22 / Apache are translation test cases, built from generic
atmospheric parts. No engine code changed by this doc.*
