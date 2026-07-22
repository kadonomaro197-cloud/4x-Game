# Real-Distance Combat — the LOCKED design (2026-07-22)

**Status:** 🔒 design-locked. Slice 1 is BUILT — 1a (the metres↔hex translation helper), **1b (real-metre stat
fields: `GroundWeaponMount.Range_m`, `GroundUnit.Range_m`/`Speed_kmh`, `GroundUnitDesign.Range_m`, populated at the
assembler + `RaiseUnit`) and 1c (the client weapon ring drawn from the real km stat per-body)** all landed additive/
byte-identical (the resolver still gates on hexes; nothing live changed). Slices 2–5 are planned below.

> **DOCS-INDEX:** this doc needs its row added/flipped in `docs/DOCS-INDEX.md` **in the same commit**
> (per root `CLAUDE.md` → "Docs layout & upkeep", rule 1). It lives in `docs/combat/`.

**The developer's rule, in his words:**

> *"A hex on Mars is a COMPLETELY different distance than a hex on Earth. HEXes are a visual
> indication of distances and locations. REGIONS are not important either — they're just a method of
> viewing hexes. What matters is the actual numbers on the weapon/entity itself."*

This is the plan to make that true **everywhere** combat measures distance: the numbers stamped on the
gun (kilometres) and the drive (kilometres-per-hour) are the truth, and the hex grid is only the ruler
we draw them on. Space combat already works this way; the ground war is the half that still cheats, and
this doc mirrors the ground onto the space model, one CI-gated slice at a time.

---

## 1. The principle — real stats are the truth, hexes are just the chart

**Lead with the point:** a weapon's reach is a property of the *weapon*, not of the *planet it's
standing on*. A 100 km gun reaches 100 km on Earth, on Mars, on a tiny moon — always. Right now the
ground war doesn't work that way: it measures reach in **hexes** ("this rifle reaches 1 hex, this
cannon reaches 3"), and a hex is a different real distance on every world. So today "a 3-hex cannon"
reaches roughly 1,680 km on Earth and maybe 15 km on a small moon — the *same gun*, wildly different
real reach, purely because of what rock it's on. That's backwards.

Think of a ship's navigation chart. The chart has a **scale bar** — "1 inch = 1 nautical mile." Your
gun's range is 12 nautical miles, a real number. On a chart of the harbor, 12 miles fills most of the
page; on a chart of the whole ocean, 12 miles is a speck. **The range never changed — only how big it
looks on the paper.** Hexes are our scale bar, and the scale is different on Earth than on a moon. That
difference is the *feature*, not a bug: the same battalion covers more hexes of a small world per step
than it does of Earth, exactly as it should.

**Space already runs on this principle — the ground copies it.** In space, the only combat distances
are real metres:

- `WeaponProfile.Range_m` — how far a weapon reaches, in metres (`Weapons/../Combat/WeaponProfile.cs`;
  `0` = unbounded). This is the number the ground weapon must carry too.
- `FleetCombatStateDB.Separation_m` — the live gap between two forces, in metres, that shrinks over
  time as they close (`Combat/FleetCombatStateDB.cs:55`).
- The range gate that decides when a weapon fires:
  `if (separation_m > 0 && w.Range_m > 0 && w.Range_m < separation_m) continue;`
  (`Combat/CombatEngagement.cs:1347`) — a weapon holds fire until the real gap falls inside its real
  range. (`FleetCombat.cs:99` is the same rule in the instant resolver.)
- The closing loop that shrinks the gap at the faster side's real speed
  (`Combat/CombatEngagement.cs:1028`, `AdvanceClosing`).
- Detection gated on real metres — you only fight what your sensors actually reach.

None of those touch a hex. **The ground job is to reach the same state:** real km on the gun, real km/h
on the drive, real metres for the gap — and let the hex grid be nothing but the display ruler.

---

## 2. The metres ↔ hex translation — one number per body, for DISPLAY only

Every body already knows its own **metres-per-hex** — the real ground distance from the centre of one
hex to the centre of the next. It's computed from the body's true surface area spread over the hexes we
generated on it:

- `GroundRangeTools.HexPitchKm(region)` (`GameEngine/GroundCombat/GroundRangeTools.cs:38`) — real km per
  hex on this body, from `region.Area_km2 ÷ hex count`. This is *why* a hex on Earth ≠ a hex on a moon,
  computed and correct. **This is the hardest piece of the whole job, and it already exists** — used
  today only as a readout and by ground radar, never in the combat math.

The rule is a one-way street: **the km stat is the truth; the hex reach is DERIVED from it for drawing,
per body.** The combat math never converts *from* hexes — it works in metres. The hex count is only
ever computed at draw time, so a unit can be *shown* on the tile it occupies without the tile ever
deciding anything.

The conversions in both directions are all in `GroundRangeTools` (Slice 1a — built):

- `RealReachKm(hexRange, region)` (`:48`) — hexes → km (the existing readout).
- `HexesForKm(km, region)` (`:65`) / `HexesForMetres(m, region)` (`:76`) — km/m → hexes (the inverse:
  put a real range onto the chart).
- `MetresForHexes(hexes, region)` (`:82`) — hexes → real metres (turn "how many hexes apart" into "how
  many metres apart" for the gate).
- `RealRangeKmFor(hexRange, region)` (`:94`) — **THE single seam** where "a weapon's real range" is
  defined. Today it equals `RealReachKm` (byte-identical); a later slice substitutes a real per-weapon
  stat here, and that one substitution flips the whole model.

### Worked example — a 1 km gatling and a 12 km radar

Suppose Earth's combat hexes come out around **560 km across** (a big continental tile), and a small
moon's hexes come out around **2 km across**.

| Weapon/sensor | Real stat (the truth) | On Earth (~560 km/hex) | On the moon (~2 km/hex) |
|---|---|---|---|
| Gatling | **1 km** reach | `1 ÷ 560 ≈ 0.002` hex → **same hex only** | `1 ÷ 2 = 0.5` hex → **same hex only** |
| Radar | **12 km** reach | `12 ÷ 560 ≈ 0.02` hex → **sees own hex only** | `12 ÷ 2 = 6` hex → **sees 6 hexes out** |
| A 3 km gun | **3 km** reach | `3 ÷ 560 ≈ 0.005` hex → same hex | `3 ÷ 2 = 1.5` hex → **reaches an adjacent hex** |

Read down the columns: the *stat never changed* — a 1 km gun is 1 km everywhere. But on Earth, 1 km is a
rounding error inside a 560 km tile, so the two forces have to be **in the same hex** before anyone can
fire; on the moon a 3 km gun already reaches the next tile over. That "1 hex on Earth ≠ 1 hex on a moon"
behaviour **falls out of the same divide, with no special case** — it's just `stat_km ÷ HexPitchKm`.
The developer accepted the Earth consequence explicitly: *"the 2 battalions must occupy the same exact
hex in order for combat to start."*

Detection works the identical way and is **already** correct on the ground: ground radar stores a real
km reach (`GroundSensorAtb.Range_km`) and the client draws its ring by dividing by `HexPitchKm`
(`PlanetViewWindow.RadarReachHexesFor`, `:545`; the engine twin is `GroundSensors.RadarReachHexes`).
Weapons are the last stat that still lives in hexes.

---

## 3. The resolver as a real closing fight (reuse the shared kernel; mirror `AdvanceClosing`)

Once two forces make contact, they are **not** stacked at zero distance — they're somewhere inside that
big hex, a real sub-hex gap apart. That gap is where the fight actually plays out, and it must play out
the way space already does — over real distance and real time, not a one-shot compare. The locked
sequence, per tick:

1. **Start at a real gap.** Seed the metre separation between the two forces from their real distance
   (detection range, or a sensible default engagement distance). This mirrors space seeding
   `Separation_m` from the real fleet distance at `StartEngagement` (`Combat/CombatEngagement.cs:508`).
2. **Cross at real speed → a real crossing time.** Each game-step, the attacker closes the gap at its
   **real ground speed** (km/h → m/s). A gap `G` closed at speed `S` takes `G ÷ S` seconds — a real
   crossing time. This mirrors `AdvanceClosing` (`:1028`), which shrinks the space gap toward the
   controlling (faster) side's preferred range at its real speed × dt.
3. **Hazards bite the crossers DURING the crossing, BEFORE the defender fires.** A unit walking across a
   fire field, vacuum, or corrosive band bleeds *while it approaches* — it arrives already hurt, and it
   took that damage before it could shoot or be shot. This is the developer's "environmental effects
   count even before the marine takes any damage." The ground already has the per-tick attrition math
   (the E3/E4 hazard step in `ProcessBody`, `GroundForcesProcessor.cs` ~`:192-212`); this slice applies
   it to the *crossers over the crossing time*, not only to units standing still in a hazard.
4. **Fire when the real gap ≤ the real range.** A weapon opens up the instant `gap_m ≤ Range_m` — so a
   long gun speaks while a short one is still silent, exactly the space per-weapon range gate at
   `CombatEngagement.cs:1347`. On the ground this replaces the hex gate at
   `GroundForcesProcessor.cs:437` (per-weapon) and `:464` (collapsed single-weapon).
5. **Resolve damage-per-second with the shared kernel.** While in range, damage accrues over time
   against accuracy, evasion, shields, and armour — and **all of that math already exists and is already
   shared.** The ground resolver already routes each shot through `CombatKernel` (dodge, depleting
   shield pool, flat-per-source armour): `FireWeaponAtReachable` in
   `GroundForcesProcessor.cs:371` calls `CombatKernel.HitFraction`, `ShieldSoakFraction`, `ArmourSoak`,
   `BurstShotCount`. **Reuse it verbatim.** The one missing input is the real gap: today the ground
   calls `CombatKernel.HitFraction(profile, t.Evasion)` (`:381`) — the 2-argument form, so `separation_m`
   **defaults to 0** (point-blank, no range-accuracy falloff). Passing the real gap into the 3-argument
   `HitFraction(w, evasion, separation_m)` (`Combat/CombatKernel.cs:147`) turns on distance-falloff on
   the ground exactly as in space — a dumb slug loses accuracy across the gap, a guided/area weapon
   holds it. The kernel's `Combatant` even carries a 1-D `Position_m` axis built for exactly this
   ("fleet separation in space, or hex-distance × metres-per-hex on a planet", `CombatKernel.cs:125`).

**In one line:** real gap → cross at real speed → crossing time → hazards bleed the crossers → fire when
gap ≤ real range → the shared kernel resolves dodge/shield/armour/health per second in range. The kernel
is done; the closing loop + the real gap are what the ground still needs.

---

## 4. Sub-hex — track REAL continuous positions in metres (developer-confirmed)

The developer's explicit call: **track real continuous positions in metres; the hex is purely how we
DRAW a unit.** Two units shown on the same tile are only *visually* the same — the real metre gap
between them decides everything. A 1 km gun does **not** fire until the real gap is ≤ 1 km, even if the
board can only show them on the same tile.

Today a `GroundUnit` has no continuous position — only integer hex coordinates (`HexQ`/`HexR`,
`GlobalQ`/`GlobalR` in `GroundForcesDB.cs:139-160`). So the sub-hex truth the principle requires has
nowhere to live yet. The fix (Slice 3) is a real metre position (or, at minimum, a running per-fight
`Separation_m` on the engaged units) — the ground echo of `FleetCombatStateDB.Separation_m`. The hex
`(Q,R)` becomes a *derived* render coordinate: we compute it from the metre position at draw time and
never read it back into the math.

---

## 5. The numbered, CI-gated slice plan

> **Discipline (non-negotiable):** combat is the ONLY green combat code in the tree, and there is no
> local .NET SDK — CI (~30 min/push) is the only compile + correctness gauge. So every slice is:
> **additive/verified → push → WAIT for both CI jobs green → build the next on top.** Never stack a
> behaviour change on an unproven base. Each behaviour change ships behind a **default-OFF flag** (the
> space `EnableClosingRange` pattern) so a co-located fight (gap 0) stays byte-identical until the flag
> flips — that's how space rewrote its resolver without breaking the green suite.

The order lands the **foundation green first**, then flips behaviour one gate at a time.

---

### Slice 1 — the additive real-metric FOUNDATION (byte-identical; the engine resolver is untouched)

**Goal:** put the real-metre truth *in place* (fields + helper + client rings) without changing one
number of live combat. The resolver keeps gating on hexes; the real stats ride alongside, unused by the
gate, so CI stays green.

**1a — the metres↔hex helper. ✅ BUILT (2026-07-22).**
- Files/functions: `GroundRangeTools.HexesForKm` (`:65`), `HexesForMetres` (`:76`),
  `MetresForHexes` (`:82`), `RealRangeKmFor` (`:94`) — the inverse of the existing `HexPitchKm`/
  `RealReachKm`, plus the single seam.
- Byte-identical: nothing in the resolver reads these; `RealRangeKmFor == RealReachKm` this slice.
- **Gauge:** `Pulsar4X.Tests/GroundForcesTests.RealDistance_HexTranslation_BothWays_AndByBody` — the
  round-trip + the by-body asserts, incl. the tripwire that `RealRangeKmFor == RealReachKm` (proves 1a
  stayed byte-identical). Keep green through 1b/1c.

**1b — real-metre stat fields on ground units + weapons (populated from the design; KEEP the hex fields).**
- Add, alongside (not replacing) the existing hex fields:
  - a real weapon reach — `GroundUnit.Range_m` (double) beside `GroundUnit.Range` (int hex,
    `GroundForcesDB.cs:75`), and `GroundWeaponMount.Range_m` beside `GroundWeaponMount.RangeHexes`.
  - a real radar reach — already real (`GroundSensorAtb.Range_km`); just snapshot it onto the unit if
    not already present, for symmetry.
  - a real drive speed — a `GroundUnit.Speed_kmh` (or m/s) derived from the design's locomotion, beside
    the existing `GroundLocomotionAtb.SpeedFactor` multiplier (which stays the timing driver for now).
- Populate them at raise: `GroundForces.RaiseUnit` (`GroundForcesDB.cs`) snapshots the design's real
  values, exactly as it already snapshots Attack/Defense/Range. The design-builder sites
  (`GroundUnitAssembly.ToGroundUnitDesign`, `GroundStartGarrison`) set the real values; for now derive
  them from the hex range × a **fixed reference pitch** so the number is populated (a real body-pitch
  conversion is deferred to Slice 2/3, where the gate actually reads it).
- **Byte-identical:** these are additive fields (`[JsonProperty]`, default 0, deep-copied in the
  copy-ctor). Nothing reads them for gating; the resolver still gates on the hex fields. Compile risk is
  low (additive fields, no ctor-arity change — see gotcha 6: do NOT add them as `*Atb` ctor args, which
  would orphan every base-mod template; add them on `GroundUnitDesign`/`GroundUnit` and set them at the
  design-builder sites, exactly how `UpkeepCredits` was added).
- **Gauge:** a new `RealDistanceRangeTests` — a raised unit carries a real `Range_m`/`Speed` matching its
  design; every existing `GroundForcesTests`/`GroundMobilityTests`/`GroundWeaponBandingTests` stays green
  (they never read the new fields).

**1c — client range/radar RINGS drawn from real km per-body (display-only; engine untouched).**
- The **radar ring is already correct** — `PlanetViewWindow.RadarReachHexesFor` (`:545`) reads
  `GroundSensorAtb.Range_km ÷ HexPitchKm(region)`. Keep it (optionally dedupe onto the engine twin
  `GroundSensors.RadarReachHexes`).
- The **weapon ring is the fix**: today the overlay reads `int weaponHexes = Math.Max(0, u.Range)`
  (`PlanetViewWindow.cs:366`) — raw hexes, never through `HexPitchKm`. Point it at the new real stat:
  `weaponHexes = HexesForKm(u.Range_m/1000, region)` — the same km→hex shape as the radar branch one
  line below it (`:367`). Also invert the caption at `PlanetViewWindow.cs` ~`:472-474` so it leads with
  km ("strike range N km (H hex on this body)") and add the per-body ruler to the region-detail line
  (~`:1374`): "hex ≈ {HexPitchKm} km across."
- Same for the Force-Management "Reach" column (`FleetWindow.cs` ~`:1246`) and the Entity Assembler
  "Range (hex)" row (`ShipDesignWindow.cs` ~`:466`) — show km, derived per body where a body is in hand,
  and km-only in the designer (a design has no body, so hex is meaningless there).
- **Byte-identical to the engine:** this is a display change only. It shares the existing
  `GlobalUIState.ShowAllRangeRings` toggle (`PlanetViewWindow.cs:357`), fog-honest (own units only). CI
  compiles the client but **cannot run it** — so 1c is compile-checked here and its *look* is verified on
  the developer's local build (add a `docs/CLIENT-TEST-CHECKLIST.md` line).

---

### Slice 2 — flip the resolver range gate to `real gap ≤ real range` (behaviour change; RE-BASELINE)

**Goal:** the fight's range decision stops being a hex-count and becomes real metres — the core of the
whole design.

- **Files/functions:** change both gates in `GroundForcesProcessor.ResolveRegionCombat` from hex to
  metres:
  - per-weapon (W2 path): `GroundForcesProcessor.cs:437` — `HexDist(u,t) > m.RangeHexes` →
    `realGap_m > mount.Range_m` where `realGap_m = HexDist(u,t) × HexPitchKm(region) × 1000` (via
    `MetresForHexes`).
  - collapsed single-weapon path: `GroundForcesProcessor.cs:464` — `HexDist(u,t) > u.Range` →
    `realGap_m > u.Range_m`.
  - flip `RealRangeKmFor` (`GroundRangeTools.cs:94`) to return a real per-weapon km stat (decoupled from
    the body pitch) so a gun's reach stops scaling with hex size — this is the one substitution that
    changes behaviour, and it's in ONE method by design.
- Both gates MUST change together (same discipline as the space "gate must match in `Tick` and
  `NewEngagementImminent`" rule) or the two ground paths disagree.
- **Behind a default-OFF flag** (e.g. `EnableGroundRealRange`, the `EnableClosingRange` pattern). With it
  off, a co-located fight (`HexDist == 0` → gap 0) passes both the old and new gate identically →
  byte-identical.
- **RE-BASELINE (name the tripwires):** the fights authored *in hexes* change once the flag is on. In the
  SAME commit, re-express the setup distances (an "N hexes apart" becomes a real metre gap via
  `HexPitchKm × N`, an authored hex Range becomes a real km) and record the new expected numbers for:
  `GroundForcesTests.RangeCombat_OutRangerHitsCloserUnitFirst_CloneVsZerg` (~`:1040`),
  `GroundForcesTests.ClosingFight_LongRangeWhittlesTheRusherDuringTheApproach` (~`:1299` — the north-star
  gauge), `GroundWeaponBandingTests` (the rifle+cannon bands), and the ROE kite/close gauges
  `Roe_StandOff_*` / `Roe_CloseToEngage_*` / `GroundRoleManeuverTests`. The **directional** assertions
  (out-ranger hits first, cannon reaches at range while rifle is silent, artillery kites while the line
  closes) all HOLD — only the numeric setup re-derives. Copy the metre-gap setup idiom from the space
  `ClosingTests.cs`.
- **Gauge:** the re-baselined north-star gauges above, plus a new metre-native `RangeGate_FiresOnRealGap`
  test.

---

### Slice 3 — the real closing-over-time loop + hazard-during-crossing (behaviour change)

**Goal:** the fight becomes a continuous close, not an instant per-tick exchange — the Section 3 sequence,
in code.

- **Add the continuous position / running gap.** A real metre position on `GroundUnit` (or a per-fight
  `Separation_m` on the engaged units) — the ground echo of `FleetCombatStateDB.Separation_m`
  (`Combat/FleetCombatStateDB.cs:55`). Additive `[JsonProperty]` field, deep-copied, default 0 →
  low compile risk.
- **Add `AdvanceGroundClosing`** as a `ProcessBody` step mirroring `CombatEngagement.AdvanceClosing`
  (`Combat/CombatEngagement.cs:1028`): seed the gap from the real distance at first contact (mirror
  `StartEngagement` seeding, `:508`), then each tick shrink it toward the controller's desired range at
  the attacker's **real speed** (`GroundUnit.Speed` from Slice 1b) — controller = faster force (min
  speed over the formation), desired range = the formation's longest real weapon reach (the ground echo
  of `FleetManeuver`/`FleetDesiredRange`).
- **Thread the real gap into the kernel.** Change the `FireWeaponAtReachable` call
  (`GroundForcesProcessor.cs:381`) from `HitFraction(profile, t.Evasion)` (separation 0) to
  `HitFraction(profile, t.Evasion, realGap_m)` — turning on range-accuracy falloff, byte-identical at
  gap 0.
- **Hazards bite the crossers during the crossing.** In the closing loop, apply the E3/E4 attrition
  (`GroundForcesProcessor.cs` ~`:192-212`) to units *crossing* the gap over the crossing time, before
  they reach weapon range — and drop the coarse-region-hop exemption so a region-hopping crosser bleeds
  like a hex-marching one. This is a ground ADD on top of the mirrored loop (space has no hazard-during-
  crossing twin).
- Wire `GroundCombatant.ToWeaponProfile` (`GroundCombatant.cs:66,96`) to the **true**
  `HexPitchKm(region)` and retire the `NominalHexPitch_m = 1000` placeholder (`:50`).
- **Behind the Slice-2 flag** (or its own): byte-identical off; the standing-attrition math stays pinned.
- **RE-BASELINE / tripwires:** keep the standing-attrition gauges byte-identical
  (`Environment_DamagesAUnitStandingInIt` ~`:562`, `EnvironmentalGear_ReducesHazardAttrition` ~`:587` —
  they use a *stationary* unit, so the crosser ADD can't perturb them); write a NEW gauge for the ADD (a
  unit crossing a hazard band arrives with reduced health; a sealed one survives) and a closing-fight
  gauge (a long-range unit lands damage before a short-range one can reply).
- **Gauge:** the new crosser-bleed test + a `GroundClosing_GapShrinksAtRealSpeed` test mirroring
  `ClosingTests`.

---

### Slice 4 — real-metre DETECTION / fog gate on combat (behaviour change)

**Goal:** you only fight what you actually detect, on real radar km — the ground echo of the space
`RequireDetectionToEngage` rule.

- **Files/functions:** gate contact/engagement on the unit's real radar reach vs the real gap. The radar
  stat is already real (`GroundSensorAtb.Range_km`) and the km→hex conversion already exists
  (`GroundSensors.RadarReachHexes`); the change is the **consumption** — reveal/engage on a real-metre
  radius from the unit's real position, not on translated coarse region bands (`GroundSensors.cs` reveal
  path). Mirror the shape of the space `CanEngageTarget`/`FleetDetects` gate — fog off → always; fog on →
  the attacker's faction detects the target — using the ground detection table (`GroundThreat` is already
  fog-honest: an undetected enemy counts zero), NOT the space EM sensor scan.
- Apply the gate in **both** the engage trigger and any interrupt-imminent check (the same two-places
  rule that bit space's fog gate).
- **Behind a default-OFF flag**; byte-identical off.
- **Gauge:** a `GroundFog_UndetectedEnemyDoesNotEngage` test (no scan → no fight; scan → fight), mirroring
  `BattleTriggerTests.Tick_RequireDetection_*`.

---

### Slice 5 — RETIRE the hex range fields (cleanup)

**Goal:** one source of truth.

- Once ranges are real end-to-end and the gates read the real fields, demote `GroundUnit.Range` /
  `GroundWeaponMount.RangeHexes` / `GroundUnitDesign.Range` / `GroundRangeTools.DefaultRangeFor` to
  **display-only** (or remove), so the real km on the unit is the only reach that exists and the hex is
  purely a render coordinate.
- Update `GroundUnitDesign.cs`'s "Range is in HEXES" docstring and the base-mod ground-unit templates
  (`installations.json` Range properties → author real km, `Units:"km"` like the radar already does).
  Six-point registration + `BaseModIntegrityTests` (gotcha #10); the atb binder is exact-arity (gotcha 6)
  — change every template that binds the atb in lockstep, or use a design-level field.
- **Gauge:** `BaseModIntegrityTests` (JSON loads, zero skipped) + the full ground suite green on real
  ranges.

**Universality check (don't regress space).** Space is already real-metre. After Slice 3 both domains run
the same real-distance closing model over the same `CombatKernel`; the space closing/range fixtures
(`ClosingTests`, `WeaponRangeTriggerTests`, `DodgeResolveTests`) must stay byte-identical throughout —
they touch no ground state, so a ground-only slice can't perturb them, and a green space suite after each
ground slice confirms no cross-contamination. Any place a hex-count leaks back into a range decision is a
bug to fix under this doc.

---

## 6. Marine vs. zerglings — before / after (the acceptance story)

*One space marine (gatling, real range ~1 km; radar ~12 km; foot speed) vs. 10 melee zerglings, on Earth
(~560 km per hex).*

**Before (hex ranges — today):** "gatling range = 1 hex" means the marine reaches ~560 km — absurd — and
a "3-hex" cannon reads ~1,680 km. Weapon-to-weapon range differences are meaningless at continental hex
scale; the fight is an instant per-tick exchange the moment two units share a region; detection isn't
gated by real radar at all; hazards only bite a unit standing still in them, not a unit crossing to
attack.

**After (this design):**

- **Detection (Slice 4).** The marine's 12 km radar is a sliver of a 560 km hex — he sees only his own
  hex. The zerglings three hexes away (~1,680 km) are **invisible**; no fight forms. As they march
  hex-by-hex toward him, he still can't see or shoot until they enter **his hex**.
- **Contact + closing (Slices 2–3).** Contact begins in one shared hex — but they are **not** stacked; a
  real sub-hex gap separates them. The zerglings (melee, reach ≈ contact) must **cross that real gap at
  their real foot speed**, which takes a real crossing time. The marine's gatling (1 km real range) opens
  fire the instant the gap is ≤ 1 km and keeps firing every second as they close — real
  damage-per-second against their evasion, armour, and health for the whole crossing — while the
  zerglings deal **zero** until they reach contact.
- **Hazards (Slice 3).** If the ground between them is on fire (or vacuum), the zerglings **arrive already
  bleeding** — they paid a health cost to cross that they never paid before, before the marine even opened
  up.
- **The outcome is now a real question:** *how many zerglings does the marine drop per second × how long
  the crossing takes × how much the crossing itself costs them* — not a hex-count and not a one-shot
  strength-compare. Give the zerglings a longer, hazard-swept approach and the marine wins that he'd lose
  across open ground at knife range. That's the whole point.

**And the same numbers on a small moon (~2 km/hex) behave differently for free:** the marine's 1 km gun
is still same-hex-only, but a 3 km gun would reach the next tile; his 12 km radar now sees 6 hexes out
instead of one. Nothing in the code changed between the two worlds — only `HexPitchKm` did. The numbers on
the gun are the truth; the hex is just where it's drawn.

---

## 7. Connections (Prime Directive) & cradle-to-grave

- **Ground resolver** (`GroundForcesProcessor.ResolveRegionCombat`) — owns the two hex gates (`:437`,
  `:464`) Slices 2–4 rebase; L9: it is the ONE hotloop on `GroundForcesDB`, so all changes stay inside
  it, no new processor.
- **`CombatKernel`** — the shared dodge/shield/armour/`HitFraction(separation_m)` math both domains
  already use (ground wired at `GroundForcesProcessor.cs:371,381`); Slice 3 feeds it a real ground gap
  instead of the constant 0. No kernel edit needed — it was built for this (`CombatKernel.cs:125,147`).
- **`GroundRangeTools` / `GroundSensors`** — the km↔hex translation layer (1a done) + the already-real
  radar path Slice 4 repoints.
- **Client** (`PlanetViewWindow`, `FleetWindow`, `ShipDesignWindow`) — reads the real km stat and derives
  the hex ring per body (Slice 1c). Compile-safe; runtime is the developer's local build (CI can't run
  the client).
- **Base-mod data** — the ground-unit templates author range in hexes today; Slices 2/5 move that to real
  km (gotcha #6 exact-arity binder; a design-level-only stat goes on `GroundUnitDesign`, not the atb
  ctor — the `UpkeepCredits` precedent).

**Cradle-to-grave is preserved:** a ground weapon is still a **component** — designed, researched, built
from materials, mounted, and lost. This design changes only how its range *number* is interpreted (real
km, not a hex-count); it does not parachute in any engine abstraction the player can't reach. A destroyed
weapon still stops reaching; a destroyed radar still blinds the unit (its detection goes dark, Slice 4).
The range the player already designs simply starts *meaning* a real distance.
