# Real-Distance Combat — the LOCKED design (2026-07-22)

**Status:** 🔒 design-locked · Slice 1 (additive foundation) building.
**Owner concept:** the developer's rule, in his words —

> *"A hex on Mars is a COMPLETELY different distance than a hex on Earth. HEXes are a visual
> indication of distances and locations. REGIONS are not important either — they're just a method
> of viewing hexes. What matters is the actual numbers on the weapon/entity itself."*

This doc is the plan to make that true **everywhere** combat measures range — so the numbers on the
gun (kilometres, kilometres-per-hour) are the truth, and the hex grid is only the ruler we draw them
on.

---

## 1. The point, before the plumbing

Right now the ground war measures range in **hexes** — "this rifle reaches 1 hex, this cannon reaches
3." That was fine as a placeholder, but it hides a physical lie: **a hex is not a fixed distance.**
The engine already knows this and even prints it (`GroundRangeTools.RealReachKm`): on Earth one combat
hex is about **560 km** across; on a small moon it might be a few km. So "a 3-hex cannon" reaches
~1,680 km on Earth and maybe 15 km on Io — the *same gun*, wildly different real reach, purely because
of what rock it's standing on. That's backwards. The gun's reach is a property of the gun, not the
planet.

**The fix, in one sentence:** stop treating a hex-count as the weapon's range. Give every weapon,
sensor, and unit a **real distance** (km) and a **real speed** (km/h) as its truth, and let the hex
grid be nothing but the **display ruler** that shows where things are — a ruler whose tick marks mean a
different real distance on each world.

Think of it like a ship's chart. The chart has a scale bar ("1 inch = 1 nautical mile"). The gun's
range is 12 nautical miles — a real number. On a chart of the harbor, 12 miles is most of the page; on
a chart of the whole ocean, it's a speck. The **range never changed** — only how big it looks on the
paper. Hexes are our scale bar. The scale is different on Earth than on Io, and that's exactly the
point.

---

## 2. What this changes about how a fight works

The developer walked through the marine-vs-zerglings example and landed the model precisely. Here it
is as the locked two-layer model.

### Layer A — the STRATEGIC grid (hexes = the chart)

Units stand on hexes. A hex represents a real distance on that body (`HexPitchKm(region)` — already
built). Two things happen on this layer:

- **Seeing.** A unit's radar is a real range (e.g. a 12 km ground radar). Whether it can *see* an
  enemy hex is: `real gap ≤ real radar range`. On Earth, 12 km is a tiny fraction of a 560 km hex — so
  the marine only detects enemies **in his own hex**. Three hexes away (≈1,680 km) he has no idea the
  zerglings exist. (This is already how `GroundSensors` reveals the map: km ÷ hex-pitch. We're making
  the *combat* gate honest the same way.)
- **Making contact.** A weapon is a real range (e.g. a 1 km gatling). The fight can only start when
  `real gap ≤ real weapon range`. On Earth, 1 km ≪ 560 km, so **the two forces must be in the same
  hex** before anyone can fire. The developer accepted this explicitly: *"the 2 battalions must occupy
  the same exact hex in order for combat to start."* On a tiny moon where a hex is only 2 km, a 3 km
  gun could reach an *adjacent* hex — and that falls out of the same rule with no special case. The
  hex is just the chart; the km decides.

### Layer B — the TACTICAL resolve (continuous real metres + seconds)

Once two forces are in contact (same hex), they are **not** at zero distance — they're somewhere
inside that big hex, a real sub-hex gap apart. This is where the auto-resolver runs the **real closing
fight**, exactly the way space already does it (`CombatEngagement.AdvanceClosing` + `Separation_m`):

1. Start the two sides at a real separation in **metres** (from detection range, or a sensible default
   engagement distance).
2. Each game-step, the sides **close at their real move speed** (km/h → m/s). The gap shrinks.
3. A weapon **fires the instant the gap falls inside its real range** (`Separation_m ≤ Range_m`) — so a
   long gun opens up while a short one is still silent, and the marine's gatling speaks the moment the
   zerglings cross into 1 km.
4. **Environmental hazards damage the crosser during the approach** — a unit walking across a fire
   field or vacuum bleeds *before* it ever reaches the defender (the ground echo of a ship crossing a
   hazard). This is the "environmental effects count even before the marine takes any damage" the
   developer called out.
5. Damage accrues as **damage-per-time** (dps) against accuracy, evasion, armour, shields, health —
   all of which already exist in the shared `CombatKernel`. The outcome falls out of *movement ×
   distance × time × dps × the defences*, not a one-shot strength-compare.

**Both layers run on real distances.** Layer A uses hexes only to *place* things and to *decide when
contact happens*; Layer B is pure metres and seconds. Nothing about the outcome depends on how big a
hex is — only on the real numbers on the units.

---

## 3. Why this is mostly CONNECT, not build (the honest survey)

Per the Prime Directive, here's the EXISTS / MISSING ledger — because a surprising amount is already in
place, and the danger is rebuilding what's there.

**EXISTS (real-distance machinery already in the tree):**
- `GroundRangeTools.HexPitchKm(region)` — real km per hex on a given body (from the region's true area ÷
  hex count). `GroundRangeTools.cs:34`.
- `GroundRangeTools.RealReachKm(hexRange, region)` — the real-range readout, already shown to the
  player. `GroundRangeTools.cs:44`; consumed at `PlanetViewWindow.cs:474`.
- `GroundSensors` radar reveal — real radar km ÷ hex-pitch → hex reach → reveal. `GroundSensors.cs:42`;
  client overlay `PlanetViewWindow.RadarReachHexesFor` (`:545`).
- Space combat is **already on real metres**: `WeaponProfile.Range_m`, `FleetCombatStateDB.Separation_m`,
  `CombatKernel.HitFraction(w, evasion, separation_m)` (`CombatKernel.cs:147`), the `EnableClosingRange`
  closing model, metre-based detection.
- `GroundCombatant.ToWeaponProfile` already builds a **metric** `WeaponProfile` with `Range_m`, via a
  `NominalHexPitch_m = 1000` placeholder — with a standing note that "slice 4 wires the true pitch."
  `GroundCombatant.cs:50,66`.

**MISSING / NEEDS-CHANGE (the actual work):**
- **The inverse translation.** `GroundRangeTools` converts hexes → km but not **km → hexes**. Layer A
  needs both directions. → *Slice 1 (additive).*
- **The resolver range gate is hex-count-based, not real-distance-based.** Two sites:
  `GroundForcesProcessor.cs:437` (`HexDist(u,t) > m.RangeHexes`) and `:464` (`HexDist(u,t) > u.Range`).
  → *Slice 2 (behaviour change, re-baseline).*
- **No continuous closing fight on the ground.** The ground resolve is instant-per-tick at a hex gate;
  it never evolves a real `Separation_m` over real time. The ground path calls
  `HitFraction(profile, evasion)` with **separation defaulting to 0**, so `Range_m` is currently inert
  for ground. → *Slice 3 (behaviour change).*
- **Weapon range is authored/stored in hexes** (`GroundUnit.Range`, `GroundWeaponMount.RangeHexes`,
  base-mod Infantry/Armor 1 / Artillery 3), not as a real stat. → *Slice 2 defines the real range;
  Slice 5 retires the hex field.*

**The load-bearing safety fact:** because the ground resolver's own hex gate enforces range and passes
`separation_m = 0` to the kernel, **the entire real-metric layer can be added without changing one
number of live ground damage** until we deliberately flip the gate. That is what makes a clean,
CI-gated, byte-identical foundation possible.

---

## 4. The slice plan (each one CI-gated; combat is the only green combat code, so we land one at a time)

> **Discipline:** combat is the only green combat code in the tree; CI (~30 min) is the only compile +
> correctness gauge (no local SDK). So every slice is: **additive/verified → push → WAIT for both CI
> jobs green → build the next on top.** Never stack a behaviour change on an unproven base.

### Slice 1 — the real-metric FOUNDATION (additive, **byte-identical**) ← building now
- Add the two-way translation to `GroundRangeTools`: `MetresForHexes` / `HexesForMetres` (and km
  convenience wrappers) — the inverse of the existing hexes→km, so Layer A can translate a real weapon/
  radar range into a hex reach and back.
- Add `GroundRangeTools.RealRangeKmFor` as the **single seam** where "a weapon's real range" is defined.
  In Slice 1 it equals the current readout (`RealReachKm(hexRange, region)`) so nothing moves; Slice 2
  substitutes a real per-weapon stat here.
- **No engine gate change, no resolver change, no `ToWeaponProfile` change.** CI stays green,
  byte-identical.
- Gauge: `Pulsar4X.Tests/RealDistanceRangeTests.cs` — round-trip (`HexesForMetres(MetresForHexes(h)) ≈
  h`), a 1 km weapon on an Earth-scale region resolves to < 1 hex (same-hex-only), the same weapon on a
  small-moon-scale region resolves to several hexes (adjacent-hex fire), and a zero/degenerate region
  returns 0 without throwing.

### Slice 2 — flip the STRATEGIC gate to `real gap ≤ real range` (behaviour change, re-baseline)
- Give each weapon a real range: `GroundUnit.Range_m` / `GroundWeaponMount.Range_m`, seeded from the
  authored hex range × a **fixed reference pitch** (a chosen real metres-per-hex constant, NOT the body
  pitch) so a gun's reach is the same real distance on every world. (Or author real ranges directly in
  the base-mod JSON; decide at slice start.)
- Change both resolver gates from `HexDist > hexRange` to
  `HexDist × HexPitchKm(region) > realRange_km`.
- **Byte-identical for co-located fights** (`HexDist == 0` passes both gates identically) — only
  fights at `HexDist ≥ 1` change. Re-baseline the `ClosingFight_*` / ROE-kiting gauges in
  `GroundForcesTests` against the real-distance behaviour and record the new expected numbers.

### Slice 3 — the real CLOSING fight (behaviour change)
- Once two forces are in contact, run the continuous metres+seconds closing loop on the ground, mirror
  of `CombatEngagement.AdvanceClosing`: evolve a real `Separation_m` by real move speed, fire each
  weapon when `Separation_m ≤ Range_m`, and apply **environmental attrition to the crosser during the
  approach** (reuse the E3/E4 hazard path against the closing unit). Wire `GroundCombatant.ToWeaponProfile`
  to the **true** `HexPitchKm(region)` (retire the `NominalHexPitch_m` placeholder).
- Gauge: a closing-fight test where a long-range unit lands damage before a short-range one can reply,
  and a unit crossing a hazard arrives already hurt.

### Slice 4 — real-metre DETECTION / fog gate on combat (behaviour change)
- Gate contact/engagement on real radar km → hex reach (the `RadarReachHexes` conversion already
  exists), so an **undetected** enemy can't trigger a fight — the marine who can't see the zerglings
  three hexes off doesn't engage them. Mirrors the space `RequireDetectionToEngage` rule.

### Slice 5 — RETIRE the hex range fields (cleanup)
- Once ranges are real end-to-end, demote `GroundUnit.Range` / `RangeHexes` / `DefaultRangeFor` to
  display-only (or remove), so there is one source of truth: the real km on the unit.

**Space check (universality):** space combat is already real-metre, so the "universal" requirement is
mostly *don't regress space* + *bring ground up to the same footing*. After Slice 3, both domains run
the same real-distance closing model over the same `CombatKernel`. Any place a hex-count leaks into a
range decision is a bug to fix under this doc.

---

## 5. Marine vs. zerglings — before / after (the acceptance story)

*One space marine (gatling, real range ~1 km; radar ~12 km) vs. 10 melee zerglings, on Earth.*

**Before (hex ranges):** "gatling range = 1 hex" means the marine reaches ~560 km — absurd — and a
"6-hex" reading would reach ~3,300 km. Range differences between weapons are meaningless at continental
hex scale, and detection isn't gated by real radar at all.

**After (this design):**
- **Layer A:** The marine's 12 km radar is a sliver of a 560 km hex — he sees only his own hex. The
  zerglings three hexes away (≈1,680 km) are invisible; no fight. As they march hex-by-hex toward him,
  he still can't see or shoot until they enter **his hex**. Contact begins in one shared hex, exactly as
  the developer specified.
- **Layer B:** In-hex, the two sides start a real distance apart. The zerglings (melee, range ≈ contact)
  must **cross that real gap at their real move speed**. The marine's gatling (1 km real range) opens
  fire the instant they're inside 1 km and keeps firing as they close — real dps against their evasion,
  armour, and health, for the whole crossing time — while the zerglings deal **zero** until they reach
  contact. If the ground between them is on fire, the zerglings arrive already bleeding. Whether the
  marine wins is now a real question of *how many he drops per second × how long the crossing takes ×
  how much punishment the crossing itself inflicts* — not a hex-count and not a one-shot strength-compare.

That's the whole target: the numbers on the gun are the truth; the hex is just where it's drawn.

---

## 6. Connections (Prime Directive)

- **Ground resolver** (`GroundForcesProcessor.ResolveRegionCombat`) — the two hex gates it owns are the
  behaviour that Slices 2–4 rebase. L9: it is the ONE hotloop on `GroundForcesDB`; all changes stay
  inside it, no new processor.
- **`CombatKernel`** — the shared dodge/shield/armour/`HitFraction(separation_m)` math both domains
  already use; Slice 3 feeds it a real ground `Separation_m` instead of the constant 0.
- **`GroundRangeTools` / `GroundSensors`** — the km↔hex translation layer; Slice 1 completes it.
- **Client** (`PlanetViewWindow`) — already reads `RealReachKm` for the readout and the range/radar
  overlay; as ranges become real stats it reads the stat instead of a hex-derived number (compile-safe;
  runtime is the developer's local build — CI can't run the client).
- **Base-mod data** — the ground-unit templates author range in hexes today; Slice 2/5 move that to a
  real distance (gotcha #6: the ground-unit atb binder is exact-arity — any new ctor dial updates every
  template in lockstep; a design-level-only stat goes on `GroundUnitDesign`, not the atb ctor).

---

## 7. Cradle-to-grave

The real-range weapon is still a **component** (a designed/researched/built/mounted/lost part) — this
design changes only how its range *number* is interpreted (real km, not hex-count), not the vertical
chain. A destroyed weapon still stops reaching; a destroyed radar still blinds the unit (Layer A goes
dark). Nothing here parachutes in an engine abstraction the player can't reach — it makes the range the
player already designs *mean* a real distance.
