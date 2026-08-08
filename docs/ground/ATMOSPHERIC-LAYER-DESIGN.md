# The Atmospheric Layer — aircraft that fly and fight over a planet

**As of 2026-08-08. DESIGN ONLY — no engine work yet (the developer's explicit call).** How an aircraft — an F-22, an
AH-64 Apache, a Death Glider, a LAAT gunship — is *designed, moved, and fought* over a planet surface, grounded in what
the engine and the designers already do. This doc is the record; the engine slices wait for a separate go-ahead.

> **One-line version:** an aircraft is a **ground unit that carries an altitude**, and altitude is just **a third leg on
> the distance ruler the ground fight already uses**. From that one change — measure the *diagonal* gap (with height),
> not the flat gap — air-to-air, close air support, and anti-air all fall out **through the combat math that already
> exists**. No air domain, no air map, no air resolver, no new order.

---

## 1. The point (plain English)

Pulsar4X has exactly two boxes today: **ships in space**, and **ground units marching on the surface**. There is nothing
in between — no altitude, no airspace, no dogfight, no anti-aircraft. An aircraft is a *pure flyer over a surface*, and it
has no native home. That gap is real and already flagged as the single biggest missing planetary system.

An aircraft is the cleanest thing to expose it, and the F-22 and the Apache expose it two different ways:

- The **F-22 Raptor** is an **air-superiority** fighter — its whole job is to kill *other aircraft* and own the sky.
- The **AH-64 Apache** is **close air support (CAS)** — loiter low over the battle and pound *ground* troops, working with
  the infantry.

This design gives both of them a home **without building a new combat domain** — because the engine already has the two
pieces that matter (a shared damage kernel, and a real-distance range gate), and your own chassis designer already draws
the altitude bands. We are wiring what exists, not inventing a parallel system.

**Analogy.** Think of the ground fight as a gun crew that only knows how far away a target is *along the ground*. Give
that crew a rangefinder that also reads *height*, and suddenly a rifleman can't touch a jet 30 km overhead (it's 30 km
away on the slant), but a surface-to-air missile that reaches 30 km *can*. Nothing else about the gun crew changes. That
rangefinder-with-a-height-dial is the entire mechanism.

---

## 2. The core insight — altitude is a third leg on the ruler

The ground resolver already decides "can this weapon hit that target?" by measuring the **real gap in metres** between two
units and comparing it to the weapon's **real range in metres**. That gap is computed by one function —
`GroundMiniHex.RealGapMetres(unitA, unitB, body)` (`GroundMiniHex.cs:130`) — and it is fed into the shared range gate
`CombatKernel.WithinReach` at exactly one place (`GroundForcesProcessor.cs:568`), and into the dodge accuracy
`CombatKernel.HitFraction(profile, evasion, sep_m)` at one place (`GroundForcesProcessor.cs:423`).

**Give each unit an altitude in metres, and change one function:**

```
RealGap3D(a, b) = √( RealGapMetres(a, b)²  +  (altitude_a − altitude_b)² )
```

That is the whole combat change — Pythagoras with a vertical leg. Feed `RealGap3D` where the resolver today feeds
`RealGapMetres`, behind a flag, and:

- It is **byte-identical for an all-surface battle** — at equal altitude the height leg is zero and `RealGap3D` collapses
  back to `RealGapMetres`. Every existing ground/space combat gauge stays green with the flag off.
- **All three air interactions fall out of the unchanged shared kernel:**

| Interaction | How it emerges from the 3D gap + the existing kernel |
|---|---|
| **Air-to-air** (F-22) | Two units at altitude, close diagonal gap → the ordinary kernel fight. Fighters carry high **evasion**, so `HitFraction` lets only **guided / high-tracking** weapons land — you can't cannon a jet; a missile can. Thin armour → one hit kills. |
| **Air-to-ground / CAS** (Apache) | An airborne unit fires **down**; the height leg means only a **long-range** weapon reaches the surface. And any enemy that can shoot *up* fires back **in the same loop** — so CAS is only safe once the sky is won. |
| **Ground-to-air / anti-air** (the counter) | A surface unit fires **up**; a 500 m rifle physically cannot cover the 30 km slant to a high flyer, a 30 km **SAM** can. "Deny the sky" is a pure **range-vs-altitude fact** — *no anti-aircraft code is written at all.* |

The fighter ▸ CAS ▸ AA rock-paper-scissors is **emergent from altitude × weapon range × the kernel**, exactly the way the
ground type-triangle was already dissolved into raw stats × weapon nature. Nothing bespoke.

---

## 3. Why NOT a new air domain (three depths considered)

We worked three depths and judged them against the project laws (name the decision · derive don't invent · one verb both
seats · cradle-to-grave · reuse · MVP honesty · gauge-ability). The middle one won decisively.

| Depth | What it is | Why not / why |
|---|---|---|
| **A — Overlay** | A binary `Surface/Airborne` tag + a new weapon "can-hit-these-bands" **mask** | Cheapest core, but the mask is a **categorical** gate invented where a **geometric** range gate already exists — and it is a *different wiring* than C. "Ship A, grow into C later" = **rip the mask out**, rework disguised as phasing. Rejected. |
| **B — Third plane** | A real air **battle-plane** (like space + ground), live climb/dive, a **separate air resolver**, an `Air` combat domain | Richest decision set, but **XL** for a deferred layer; its two signature additions are the ones the laws push back on — a **new movement verb** (`SetAltitudeBand`) and **opening the closed `{Both,Space,Ground}` domain enum** (`TargetPriority.cs:40-48`), against the "one triangle, why a separate resolver" north star. Its separate resolver is **redundant** — the shared gate already suffices. Reserved as an *optional later graft only*. |
| **C — Environment-unified** ✅ | Altitude = a **static band** on the unit; the 3D-gap swap into the **existing** gate | **The recommendation.** Cheapest design (S–M) that still creates a real stacking decision; **highest reuse** (one changed argument into a gate that is already gap-fed and kernel-shared); **purest derive-from-what-the-sim-reads** (it adds *no* new weapon dial — it reuses `Range_m`, which the resolver already reads); **zero new AI code**. |

**The ruling: build the C spine.** It reaches win-sky / deny-sky / CAS-needs-superiority / AA-as-a-range-fact /
altitude-as-a-design-tradeoff at S–M, and it never ships a throwaway. If richer *live* altitude is ever wanted, it grafts
onto C's spine later (see §11 open ruling) — without a separate resolver and without a new domain.

---

## 4. The recommended design (Depth C)

### 4a. Position — movement

An aircraft is a plain **`GroundUnit`** data object in the planet body's `GroundForcesDB` — **never a ship, never enrolled
in the space resolver** (this holds the same ship/ground symmetry the engine already keeps: a ship is never a ground
combatant). It is cheap, save-safe, and — critically — the AI already drives it.

- **Horizontal position** reuses the existing continuous field verbatim: the two-part `(regional hex)(mini hex)` address
  plus the sub-tile offset, measured by `GroundMiniHex.RealGapMetres` (the live distance function, `GroundMiniHex.cs:130`).
- **Terrain freedom** is the already-scoped **SMALL ADD** from `docs/ground/GROUND-UNIT-VARIABLES.md` Q3: a **`Flight`
  dial** on `GroundLocomotionAtb` (the same shape as its existing `Amphibious` dial, `GroundLocomotionAtb.cs:32`) plus a
  guarded branch in the two pure functions the whole march already routes through — `HexPathfinder.IsImpassable`
  (`:63` → ocean becomes passable) and `HexPathfinder.HexMoveMult` (`:71` → returns `Move_Open`), and skip the rough
  timing penalty. A flyer crosses water and mountains at open-ground speed. That is the entire movement change.
- **Vertical** is **one new field**: an altitude **band** (0 terrain-following → 3 near-space — the chassis door's own
  operating levels), snapshotted at raise exactly like `Range_m`/`Speed_kmh` are today, plus a 4-value `BandAltitude_m`
  lookup so the bands are **real metres**, not an abstract index.

There is **no climb/dive** in the recommended spine — altitude is a **design-time envelope**, not a live maneuver (see the
open ruling, §11). Honest limit, deliberately taken for the cheap path.

### 4b. Combat — one resolver, one kernel, one changed argument

Add `GroundMiniHex.RealGap3D(a, b, body)` (§2) and feed it wherever the resolver feeds `RealGapMetres` today — into
`WeaponReaches → CombatKernel.WithinReach` (`GroundForcesProcessor.cs:568`) and into the separation arg of
`CombatKernel.HitFraction` (`GroundForcesProcessor.cs:423`) — behind an `EnableAltitudeCombat` flag (menu-on, default off
so the CI suite stays byte-identical). From that one substitution the three interactions of §2 emerge through the
**unchanged** shared `CombatKernel` — dodge, depleting shield pool, flat-per-source armour, penetration, nature — all of
it reused, none of it rewritten.

CAS also **generalizes** the orbital-bombardment routine you already have — `DamageProcessor.ApplyGroundBombardment`
(`DamageProcessor.cs:349`, the "thing above hits units below, armour-aware" primitive) — past its three current limits
(colony-triggered → any air unit; defender-only → an enemy filter; whole-surface → the 3D gate lands it on specific units
in reach). But that touches the **live** colony-bombardment wire, so it is a **later, flag-gated slice with its own
re-baselined gauge**, not part of the cheap spine.

### 4c. Designer expression — derive, don't invent

Almost entirely reuse of doors you already built:

- **Chassis door** (`chassisderived.html`) — author the currently-empty **`Environment = Atmospheric`** cell. Its
  **operating-level chips ARE the bands** (Terrain-following → Medium → High → Near-space), and the tech-capped
  **"operating envelope"** slider sets which band(s) a unit occupies — reusing an existing dial *type*, adding only the
  band list + altitudes. Selecting Atmospheric **derives** the unit's home band — it is **not** a free slider: you can't
  mark a unit "airborne" without knowing it mounts flight, so band is an **assembly/emergent** read-out, never a
  Component-Designer dial (the DESIGNER-NORTH-STAR intrinsic-test boundary, honored).
- **Propulsion door** (`propulsionderived.html`) — the drive is a **Reaction drive with `MediumRequirement = Atmosphere`**
  (a jet scoops air): it works in atmospheric bands, fades toward near-space, dies in vacuum. The medium-gate is the one
  genuinely-missing wire, enforced by the assembler. Air is **not** a new drive family — the propulsion door already
  ruled that.
- **Combat doctrine gains nothing** — an air unit is a **Ground-domain** unit; `DoctrineDomain` stays `{Both, Space,
  Ground}`. One triangle, per the north star.

**The two test units, fully expressed:**

- **F-22** = Atmospheric chassis · **High** operating envelope · medium-gated jet drive · long-range **guided** A2A
  weapons · high evasion from low mass.
- **AH-64 Apache** = Atmospheric chassis · **Terrain-following / Medium** envelope · rotor drive (a lift Reaction drive) ·
  high-**penetration** CAS weapons.

---

## 5. EXISTS / EXTEND / NEW ledger (file:line)

The Prime-Directive investigation — what this plugs into, and precisely what changes. Verified against source 2026-08-08.

| Piece | State | Where |
|---|---|---|
| Shared damage kernel (dodge/shield/armour/penetration/nature) | **EXISTS — reused verbatim** | `Combat/CombatKernel.cs` (`HitFraction`, `WithinReach`, `ArmourSoak`), already called by BOTH resolvers |
| The ground range gate | **EXISTS — reused verbatim** | `GroundForcesProcessor.cs:561-569` `WeaponReaches` → `CombatKernel.WithinReach` fed by `GroundMiniHex.RealGapMetres` |
| The live distance function | **EXTEND (add a 2D→3D sibling)** | `GroundMiniHex.cs:130` `RealGapMetres(a,b,body)` → new `RealGap3D` (Pythagoras + altitude leg); band-0 identity = byte-identical |
| The dodge accuracy call | **EXTEND (feed it the 3D gap)** | `GroundForcesProcessor.cs:423` `HitFraction(profile, evasion, sep_m)` |
| Terrain freedom for a flyer | **EXTEND (a `Flight` dial + 2 guarded branches)** | `GroundLocomotionAtb.cs` (dials at `:27/:30/:32`) + `HexPathfinder.IsImpassable`(`:63`)/`HexMoveMult`(`:71`) — the exact shape `GROUND-UNIT-VARIABLES.md` Q3 scoped |
| Altitude band on the unit | **NEW (one int field + a 4-value lookup)** | on `GroundUnit`, snapshotted at `GroundForces.RaiseUnit` like `Range_m`/`Speed_kmh` |
| CAS = above-hits-below, armour-aware | **EXISTS — generalize its 3 limits (later slice)** | `DamageProcessor.cs:349` `ApplyGroundBombardment` (colony-only → any air unit; defender-only → enemy filter; whole-surface → 3D-targeted) |
| Loiter / repeat-strike cadence | **EXISTS — reused** | `GroundForcesProcessor` hourly hotloop + `FireWeaponAtReachable` (`:406`) |
| Air combat domain / air map / air resolver | **NOT NEEDED — deliberately none** | `DoctrineDomain` stays `{Both,Space,Ground}` (`TargetPriority.cs:40-48`) |
| The chassis Atmospheric cell + altitude bands + envelope dial | **EXISTS in the designer (empty cell) — this fills it** | `chassisderived.html` Step ①/② |
| The drive medium-gate | **NEW (one requirement wire)** | `propulsionderived.html` ruling — Reaction drive gated to `Atmosphere` |

**Net new code surface is tiny:** one distance function, one unit field + lookup, one locomotion dial + two guarded
branches, and (later) one generalization of an existing routine. Everything that does the *fighting* is reused.

---

## 6. One Verb, Both Seats

Because altitude is a **static design property**, there is **no new order and zero new AI code**. The primitive both seats
issue is the *existing* queued ground order (`MoveRegion` / `MoveToHex` via `GroundForces.QueueFormationOrder`, stamped
`Player | Ai`) plus `SetStance`/`SetEngagement` — the identical orders a tank uses. An F-22 is **one more unit** in
`GroundTacticalBrain`'s roster: it already forms up, reads fog-honest threat, picks a posture, and emits `Ai`-stamped move
orders for any `GroundUnit`. The air fight, like every fight here, **auto-resolves once units are positioned**. The AI can
fly and fight an aircraft with the same verb the player uses — the law holds, cheaply.

---

## 7. Cradle to grave

> **bauxite / titanium / REE** (mined) → **aluminium / titanium alloy + electronics** (refined — reuse already-unlocked
> materials, so `StartingItems` is untouched) → a **`GroundUnitDesign`** built on the industry rails → carrying an
> **Atmospheric chassis + medium-gated jet drive + band-aware weapons + Flight locomotion** (components, so
> research-gating / cost / save-load / the design UI come **for free**) → gated by **tech** (the Atmospheric chassis, the
> envelope-widening techs, guided AA, higher-thrust jets) → **raised airborne** into `GroundForcesDB` at band ≥ 1 → the
> **in-play lever** (*win air superiority → open the CAS window → crack the armour stalling your assault*; or *deny the
> sky with SAMs*) → **destroyed / lost**: shot down (`Health ≤ 0`), or shoot off its **drive** and it's grounded, or its
> **sensor** and it's blinded — re-research / re-mine / re-build.

Every rung is real and component-shaped. No parachuted engine abstraction.

---

## 8. The player decisions it creates (why it earns its weight)

Per the weight firewall (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`), a system earns its keep only as the source of a **stacking**
decision. This layer creates four that stack on top of the ground fight:

1. **Win the sky first, or don't.** Air superiority is a *prerequisite* — send the Apache in before the F-22s clear the
   sky and it gets shot down. (Air-to-air gates CAS.)
2. **Deny the sky.** Fielding SAMs turns your ground force into a no-fly zone the enemy's CAS can't enter — a hard counter
   you *choose* to invest in. (AA vs air.)
3. **Altitude as a design trade.** A high-flying interceptor out-reaches everything but is useless for CAS; a low gunship
   is deadly to ground but exposed to AA. The band you build for is a real fork. (Envelope vs role.)
4. **CAS cracks the stalemate.** A high-penetration gunship punches armour that has stalled your infantry assault — the
   air arm is how you break a dug-in defender. (Air × the ground armour matchup.)

None of these is fidelity you can't act on. Each changes what you build and how you fight.

---

## 9. Scope honesty

This is a **ceiling-raiser**, not the "take a planet" MVP milestone — the surface ground fight *is* the milestone, and
this rides on top of it. Build the ground fight first. The recommended spine is **S–M** and ships the full decision set
above; the honest ceiling of the static-band spine is: **no dogfight micro, no energy fight, no in-flight fuel burn, no
true surface-to-orbit transit** (near-space only "borders orbit" via the existing bombardment edge). Those are Depth-B
territory and explicitly deferred.

---

## 10. Build slices (for WHEN engine work is authorized — not now)

Recorded so the path is on the shelf; **no code until a separate go-ahead.** CI-gated, one per push, both jobs green
before the next.

| Slice | Ships | Gauge |
|---|---|---|
| **A0** | `Band` field + `RealGap3D` + `BandAltitude_m`, unread | Band round-trips; `RealGap3D == RealGapMetres` at band 0 (byte-identical) |
| **A1** | `Flight` locomotion dial + the two Q3 guarded branches | A flyer crosses ocean/mountain at open cost; non-flyers unchanged |
| **A2** | Feed `RealGap3D` into `WeaponReaches`/`HitFraction`, behind `EnableAltitudeCombat` | Rifle can't reach a high flyer, SAM can, two flyers fight; every existing ClosingFight/RangeCombat gauge green with flag off |
| **A3** | Author the Atmospheric chassis + drive medium-gate + F-22/Apache/SAM presets (six-point registration) | `BaseModIntegrityTests` binds them; a jet in vacuum is rejected |
| **A4** *(opt)* | Unify CAS through the per-unit salvo; retire `ApplyGroundBombardment`'s whole-surface path | CAS enemy-filtered onto units in reach; bombardment gauges re-baselined |
| **A5** | AI values air: extend the pure `GroundTactics.DecidePosture` to sequence superiority-before-CAS and build AA when out-ranged aloft | AI clears the sky then strafes; builds AA when it lacks fighters |

---

## 11. Open developer rulings

1. **Design-time altitude vs live climb/dive (the load-bearing one).** The recommended spine makes altitude a **design
   envelope** (fixed for the fight) — cheapest, no new verb, no new AI code. The alternative is a **live `SetAltitudeBand`
   order** for in-play climb/dive (Depth B's one good idea), which *must* ship in the same slice as its
   `GroundTactics.DecidePosture` extension so One-Verb holds. **Default taken (absent a ruling): design-time first**, live
   altitude reserved for a later graft only if playtesting shows the sky earns more weight. This ruling changes the AI
   surface, so it's worth calling up front.
2. **How many bands at v1** — the cheap spine can ship with 2 bands (surface / airborne) and add the 3-band standoff
   (medium/high/near-space) as **pure data** later, or author all 4 from the start. No code difference; a content call.
3. **CAS unification timing (slice A4)** — retiring the whole-surface bombardment path is a behavior change to a *wired*
   system (fleet → colony bombardment). Do it now (cleaner) or leave the old path and add CAS beside it (safer)?
4. **Do near-space aircraft touch the space plane at all**, or is "borders orbit" purely flavor for v1? (The existing
   orbital-bombardment edge is the only vertical space↔surface coupling today.)

---

## 12. Connections (Prime Directive)

- **Ground combat / `GroundForcesProcessor` + `GroundForcesDB`** — an aircraft is a `GroundUnit`; it moves, spreads,
  closes, and fights on the same grid and hotloop. The 3D gap changes only the range/accuracy read.
- **The shared kernel / `Combat.CombatKernel`** — the single resolver both space and ground use; the air fight rides it
  untouched. This is the one genuine leverage point.
- **Distance / `GroundMiniHex`** — `RealGapMetres` is the seam; `RealGap3D` is its extension.
- **Movement / `HexPathfinder` + `GroundLocomotionAtb`** — the `Flight` dial + the two guarded branches (the same wire as
  `Amphibious`).
- **Damage / `DamageProcessor.ApplyGroundBombardment`** — the CAS primitive to generalize (later slice); touches the live
  colony-bombardment path from fleet combat.
- **Designers / chassis + propulsion doors** — the Atmospheric environment cell + altitude bands + the drive medium-gate;
  the intention documents this design fills in.
- **AI / `GroundTacticalBrain` + `GroundTactics`** — no new code for the static spine; a `DecidePosture` extension only if
  live altitude is later ruled in.
- **Sensors / fog** — an aircraft is detected/revealed by the same per-faction ground fog as any unit; a high flyer's
  detection profile is a natural later dial, not v1.

---

## 13. Sources

- **This session's verification** — the two-box model confirmed (no `Air` `DoctrineDomain`, `TargetPriority.cs:40-48`);
  the shared kernel used by both resolvers; `RealGapMetres`/`WeaponReaches`/`HitFraction` seams (file:line in §5); the
  chassis door's Atmospheric cell + bands + envelope (`chassisderived.html`); the propulsion door's "air is not a family,
  it's Reaction + a medium requirement" (`propulsionderived.html`).
- `docs/ground/GROUND-UNIT-VARIABLES.md` Q3 — the flyer-over-terrain `Flight`/`IgnoresTerrain` dial as a SMALL ADD (the
  movement half).
- `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §13.4 — the multi-plane BattleTheater model + the air/altitude layer
  parked as deferred; §12 — the real-distance (km/metre) ground combat locked principle.
- `docs/REALISM-VS-GAMEPLAY-AUDIT.md` — the weight firewall (name the decision).
- Root `CLAUDE.md` — One Verb, Both Seats; Cradle to Grave; derive-don't-invent (`docs/economy/DESIGNER-NORTH-STAR.md`).

*Design/reference only — no franchise assets; the F-22 / Apache are translation test cases, built from generic
atmospheric parts. No engine code changed by this doc.*
