# Weapon Unification — one universal weapon, both resolvers

**Status:** **decisions LOCKED (2026-07-06)** — see §0. The developer answered the open questions; this is now a build
plan, executed in careful CI-gated phases (it touches two live combat resolvers + deletes a system).

> Governs by `UNIVERSAL-ASSEMBLY-DESIGN.md` §2a: *a weapon is universal by TYPE, not by setting; the mount + carry
> decide where it fights.* The visible half (one designer **category**) is done; this is the **deep** half (one weapon
> **model** both combat systems read).

---

## 0. DECIDED (2026-07-06, the developer's call)

**ONE weapon designer, full stop.** Flow: Designer → pick damage class (Energy) → pick type (Beam) → spec it. No
separate ground designer. The existing space weapon templates (`laser-weapon` / `railgun-weapon` / `flak-weapon` + the
class→type hierarchy) ARE the weapon designer for everything.

1. **Fidelity = FULL.** A weapon carries its whole profile and its **triangle position is intrinsic** — wherever it
   falls in the weapon triangle, that's how it resolves, *the same in ground and space*. One combat model. (NOT the
   simplified projection — the developer chose full fidelity: *"however that weapon falls into the triangle that's
   where it falls."*)
2. **Mount model = ONE design, mounts anywhere the chassis can SUPPLY it.** *"If I have a Titan that can put out all the
   energy and ammo requirements a space ship can, then it goes on a Titan or tank with all the specs."* The weapon is
   the same design; the **gate is whether the chassis meets its requirements — mass/carry + energy + ammo.** A big
   enough, well-powered ground chassis mounts a ship-scale weapon; infantry can't (the gate forbids it).
3. **DELETE the parallel ground weapon system:** `GroundWeaponAtb`, the five ground-weapon templates
   (rifle/autocannon/cannon/plasma/claws), `GroundWeaponMode`, and the ground-only `GroundDamageMatrix` flavour
   plumbing where it duplicates the weapon triangle. The universal weapons replace them.
4. **Same logic for EVERY component kind** (armour/sensor/engine/reactor/…): one designer per kind, works everywhere,
   gated by the chassis. Weapons is the first; the pattern generalises.
5. **Growth = MULTIPLICATIVE** — done 2026-07-06: `tech-beam-range` / `tech-kinetic-yield` DataFormulas are now
   `base * Pow(2, [Level])` (double each research level; level 0 == the old cap so start is unchanged; ~1024× at max
   level 10 → ~10,000 km beam range / ~10 GJ kinetic). **FLAGGED tunables:** the ×2 multiplier and `MaxLevel 10` set
   the ceiling — steepen/extend to taste.

### Revised phased plan (per §0)

- **P0 ✅ Growth curves multiplicative** (techs.json). Done.
- **P1 ✅ Universal weapons mountable on ground (2026-07-06).** The five base-mod direct-fire weapon templates —
  `laser-weapon` / `railgun-weapon` / `flak-weapon` / `disruptor-weapon` / `plasma-repeater` (`weapons.json`) — now carry
  `ComponentMountType.GroundUnit` alongside their existing Ship/PDC/installation mounts, so the ONE designer offers the
  SAME weapon design for a ground chassis. Deliberately weapons-only: the `missile-launcher` (an ordnance/ammo subsystem)
  and the `deflector-array` (defensive, not a weapon) are left ship-only for this slice. **Purely additive — no resolver
  change** (a ground unit that mounts one contributes nothing to combat until P2/P3 wire the supply gate + profile read;
  the enum flag has no consumer yet, so no player-facing behaviour changes). No new gameplay numbers. Gauge:
  `WeaponGroundMountTests` (the direct-fire five carry the flag + keep their ship mount; the excluded two don't).
- **P2 — The SUPPLY gate** (the "a Titan can, infantry can't" rule). **Decided 2026-07-06 (developer's call): power is a
  mounted REACTOR COMPONENT, not a magic frame stat — the same part a ship uses (full cradle-to-grave), and the gate is
  HARD (an under-powered design is illegal, mirroring the carry gate).** Key finding — it's mostly CONNECT: the supply
  part exists (`Energy/EnergyGenerationAtb.PowerOutputMax`, kW; three buildable generators — `reactor`/`rtg`/
  `steam-turbine-reactor` — that even carry fuel + lifetime, a free grave/logistics rung), and the demand exists (a
  beam's `Energy` J ÷ `ChargePeriod` s = watts drawn). **The two gates COMPOSE — "infantry can't power the big laser" is
  really "infantry can't CARRY a reactor big enough," so no new "infantry vs Titan" balance knob is needed** (it falls
  out of the existing carry gate + reactor mass). Two slices:
    - **P2a ✅ Reactors ground-mountable (2026-07-06).** The three fuel-burning generators carry
      `ComponentMountType.GroundUnit` (the P1 move, supply side); `battery-bank` (storage) + `solarArray` stay ship-only.
      Purely additive, no new numbers. Gauge: `PowerPlantGroundMountTests`.
    - **P2b ✅ the assembler power gate + supply mode (2026-07-06).** `WeaponSupply` (pure, the ground echo of
      `WeaponClassifier`): a weapon's **supply mode** — Energy / Ammo / Both — with smart defaults from its own physics
      (laser/plasma/disruptor→Energy, railgun→**Both** [power+slug], flak→**Ammo**), and its `PowerDraw_W` = its own energy
      flux (energy/shot × rate, or beam energy ÷ charge — **no efficiency coefficient invented**; that's a later flagged
      knob). `GroundUnitAssembly.Compute` now sums reactor output (`EnergyGenerationAtb.PowerOutputMax` × 1000, kW→W) vs Σ
      weapon watts and hard-refuses the design if draw > supply (exposes `EnergyDemand_W`/`ReactorSupply_W` gauges). Also:
      a non-ground part (universal weapon / reactor) now counts its component mass against the carry budget, so the two
      gates COMPOSE. **Developer's supply-mode calls (2026-07-06): railgun = Both (confirmed); plasma = the PLAYER's option
      (Energy or Both) — so the mode is an overridable designer setting, landing with P2c when Energy-vs-Both changes cost.**
      The only flagged number is the kW→W unit constant (arithmetic). Gauge: `GroundPowerGateTests` (calibration-independent:
      laser draws / flak doesn't / a reactor clears the gate). **Note:** old ground weapons (`GroundWeaponAtb`) draw 0 —
      only the unified space weapons are power-gated (they merge into one at P4).
    - **P2c — the AMMO gate (next).** Build the magazine component + the ammo side: an Ammo/Both weapon (flak, railgun,
      missiles) needs a magazine to fire, and magazine size = how long it fights before resupply (a real logistics lever).
      Ammo store does NOT exist yet for guns (only missiles have ordnance) — this one is more build than connect. The plasma
      Energy-vs-Both player choice ships here (it only bites once ammo is a cost).
- **P3 — Ground reads the weapon PROFILE + triangle.** `GroundUnit` derives its combat contribution from its mounted
  universal weapons' `WeaponProfile`s; `GroundForcesProcessor` resolves via the weapon triangle (shared with space),
  behind a parallel/flag until numbers match the current garrison (existing ground gauges stay green).
- **P4 — DELETE the ground weapon system** once P3 carries it: `GroundWeaponAtb`, the 5 templates, `GroundWeaponMode`,
  the duplicate matchup. Migrate the base-mod ground units to universal weapons.
- **P5 — Generalise** the "one designer per kind" pattern to the other component categories.

**Calibration note:** because it's the SAME weapon with the SAME specs everywhere, there is *no* dps→Attack conversion
to guess — ground reads the weapon's real profile. Any remaining scale knobs (e.g. a ground-hex range from a metric
weapon range via `GroundRangeTools.HexPitchKm`) are derived, and flagged where a coefficient appears.

---

## 1. The discrepancy, precisely

There are **two parallel weapon systems** today, and an "energy weapon" is modelled **twice**:

| | Ground | Space |
|---|--------|-------|
| Weapon attribute | `GroundWeaponAtb` (`Attack`, `Range` in hexes, `Mode`) | `GenericBeamWeaponAtb` / `RailgunWeaponAtb` / `FlakWeaponAtb` (per-type, physical stats) |
| Type selector | `GroundWeaponMode` = Melee / Ballistic / Energy / Artillery | `WeaponClass` = Beam / Railgun / Flak / … |
| Resolver | `GroundForcesProcessor.ResolveRegionCombat` reads `unit.Attack` × triangle × terrain × the dodge/shield/armour matchup (`GroundDamageMatrix`) | `ShipCombatValueDB.Calculate` projects each atb into a `WeaponProfile(class, dps, velocity, tracking, saturation, range)`; `AutoResolve` consumes the profiles |
| Damage flavour used in combat | `GroundWeaponMode` → `GroundDamageMatrix` (dodge/shield/armour) | `WeaponClass` → the weapon triangle + dodge/saturation |

**The key observation:** the space side ALREADY normalises every weapon type into ONE intermediate — `WeaponProfile`
carrying a `WeaponClass`. That is the natural convergence point. `GroundWeaponMode` is a coarser parallel of
`WeaponClass`; `GroundUnit.Attack` is a coarser parallel of `WeaponProfile.dps`. The ground side just consumes a
*simpler projection* of the same idea.

---

## 2. Target

**One weapon design, carrying a TYPE + specs, projects into ONE shared weapon representation that BOTH resolvers
read.** "Energy weapon" is defined once. Whether that weapon rides infantry, a tank, or a ship spinal mount is the
**mount + the chassis carry gate + scale**, not a second attribute class.

The convergence artefact is the existing **`WeaponProfile` / `WeaponClass`** (extended as needed). A weapon design
(beam/kinetic/flak/…) produces a `WeaponProfile`; the **space** resolver consumes it as it does now; the **ground**
resolver derives what it needs (`Attack` from `dps`, hex `Range` from the profile's metric range via the *existing*
`GroundRangeTools.HexPitchKm` bridge, damage flavour from `WeaponClass`).

---

## 3. Candidate approaches

**A — Converge on `WeaponProfile` (recommended).** Make a weapon design's canonical output a `WeaponProfile`. Ground
units reference real weapon designs; `GroundForcesProcessor` derives `Attack`/`Range`/`DamageType` from the profile
(a small projection function, unit-testable). `GroundWeaponAtb` becomes either a thin *projector into* `WeaponProfile`
or is retired in favour of the typed weapon atbs. **Pro:** reuses the space side's already-unified representation;
`WeaponClass`↔`GroundWeaponMode` is a table; the hex↔metric bridge already exists. **Con:** the ground resolver must
learn to read a profile; the projection (how much `dps` = how much ground `Attack`) is a **calibration** the developer
tunes.

**B — Shared typed `WeaponAtb`.** Replace `GroundWeaponAtb` AND the three space atbs with ONE typed `WeaponAtb`
(`WeaponClass` + physical specs) that both resolvers read. **Pro:** one attribute, cleanest end state. **Con:** biggest
change; touches every space-combat gauge; higher risk.

**C — Shared spec value only.** Keep both atbs but have each project into a shared `WeaponSpec` struct both resolvers
consume. **Pro:** least invasive. **Con:** leaves the two atbs (the duplication) standing — doesn't really unify, just
bridges.

**Recommendation: A**, phased — it leans on the `WeaponProfile` normalisation the space side already proves, and lets
the ground resolver converge without a big-bang atb rewrite. B is the eventual clean end-state once A has de-risked it.

---

## 4. Phased plan (each a CI-gated slice)

1. **Map the vocabularies.** A pure, tested table `GroundWeaponMode ⇄ WeaponClass` (Energy⇄Beam, Ballistic⇄Railgun,
   Artillery⇄?, Melee⇄?). Add the missing `WeaponClass` members if needed. No behaviour change.
2. **Weapon → `WeaponProfile` for ground.** A projection `WeaponProfile → (Attack, hexRange, DamageType)` using
   `GroundRangeTools.HexPitchKm` for the range conversion. Pure + gauged. Still nothing wired.
3. **Ground resolver reads the profile.** `GroundForcesProcessor` derives a unit's combat stats from its weapon
   design's `WeaponProfile` instead of the raw `GroundWeaponAtb` fields (kept behind a flag / parallel until the
   numbers match the current garrison, so existing ground gauges stay green).
4. **Unify the designer end-to-end.** A single weapon design is mountable on a ground unit (its stats flow through the
   profile) — proving "design one beam, use it on infantry or a ship." Retire the `GroundWeaponAtb` duplication (or
   demote it to a pure projector).
5. **Extend the pattern to the other component kinds** (armour, sensor, engine…): one designer per kind, pick a type,
   spec it — the same convergence.

---

## 5. Open questions for the developer (needed before slice 3)

- **Fidelity:** should ground combat read the FULL space weapon profile (rate-of-fire, saturation, projectile
  velocity, tracking → dodge) — i.e. the weapon triangle applies on the ground too — or a **simplified projection**
  (just `dps`→`Attack` + a flavour)? This decides how deep the ground resolver converges.
- **Calibration:** the `dps → ground Attack` scale (a ship beam is GJ/s; a rifle is not). Likely a per-scale divisor —
  a flagged number, your call.
- **hex ⇄ metric range:** confirm `GroundRangeTools.HexPitchKm` is the intended bridge (a 5 km ship laser = how many
  hexes on Earth vs Io — already a visible readout).
- **One design on both, or one TYPE on both?** Is a *single weapon design instance* literally mountable on a ground
  unit AND a ship, or do they share the TYPE + designer but instantiate separately at their own scale? (The carry gate
  suggests the latter — same designer, scale-appropriate instances.)

---

## 6. Risks

- Two live resolvers + every space-combat gauge (`CombatStressLab`, `WeaponTriangle*`, etc.) — a projection that
  shifts numbers reds them. Mitigate with the parallel/flagged path in slice 3 (converge only once numbers match).
- `WeaponProfile` is currently a *space* combat-value type; widening its home (namespace/assembly) so ground can read
  it needs care (`GameEngine/Combat` ↔ `GameEngine/GroundCombat`).
- Calibration numbers are the developer's — do not bake scale divisors without flagging.
