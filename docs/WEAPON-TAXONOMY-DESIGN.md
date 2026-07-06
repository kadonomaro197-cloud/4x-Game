# Weapon Taxonomy — the decision tree, surveyed against sci-fi

**Status:** design survey (2026-07-06). Feeds the weapon unification (`WEAPON-UNIFICATION-DESIGN.md` §0, task #3):
the ONE designer's decision tree must be able to build *any* sci-fi weapon's essence. This maps a broad franchise
survey onto the tree, finds where each falls, and exposes the gaps in the current 4-class model.

> Governs by `UNIVERSAL-ASSEMBLY-DESIGN.md`: the essence emerges from the specs. A weapon is not a fixed thing you
> pick off a list — you choose its **nature**, choose its **delivery**, dial the **numbers**, and its combat identity
> (its triangle position) *falls out*.

---

## 0. The headline finding — TWO axes, not one

The current model has **one** label, `WeaponClass = { Beam, Railgun, Missile, Flak }`, and it secretly bundles two
*independent* things:

- **Damage NATURE** — energy vs kinetic vs explosive vs exotic. This is what meets the *defence*: shields eat kinetic
  but bleed to energy, armour ablates, exotic bypasses. (Ground `GroundDamageMatrix` + ship armour already care about
  this.)
- **Delivery PHYSICS** — velocity regime (light-speed / fast / slow), continuous-vs-discrete, single-vs-cloud, guided.
  This is what meets the *dodge*: `Velocity` → can-you-evade-it, `Saturation` → does-volume-floor-the-dodge,
  `Tracking` → does-it-follow-you. (`WeaponProfile` already carries all three.)

**`Beam`/`Railgun`/`Flak`/`Missile` are FUSED shorthands** (e.g. "Beam" = energy-nature *and* light-speed-delivery).
That fusion is exactly why a **blaster pistol can't be built**: it's *energy* nature but *slow, dodgeable* delivery —
no fused label covers "slow energy bolt." Star Wars blaster bolts are famously *dodgeable*; a phaser beam is not. Same
damage nature, opposite delivery, opposite triangle position.

**The fix the survey points to:** the designer picks **Nature × Delivery** separately, dials the specs, and the
**triangle position EMERGES** from `Velocity`/`Saturation`/`Tracking` — it is *derived*, never chosen. `WeaponClass`
becomes a *computed* readout ("this build behaves like a Beam-corner weapon"), not an authored enum.

---

## 1. The decision tree

```
Weapon designer
├── 1. Damage NATURE  (what it does to the defence)
│      Kinetic · Energy · Explosive · Exotic(EMP/stun/anti-shield/matter-strip/…)
├── 2. Delivery TYPE  (the physics template — sets the velocity regime + pattern)
│      Beam(continuous, ~c)  · Bolt(discrete, finite-v)  · Slug(single projectile)
│      · Cloud(many pellets, saturation)  · Guided(tracks)  · Blast(AoE)
└── 3. SPECS  (dial the numbers)
       energy/damage · velocity · rate-of-fire · projectiles-per-shot · AoE radius
       · range · tracking · special (overheat, charge-up, chain, stun…)
   → TRIANGLE POSITION EMERGES: fast+low-saturation = dodgeable (Railgun corner);
     ~c or high-tracking = un-dodgeable (Beam corner); high rate×pellets = saturation
     (Flak corner); guided+alpha = Missile corner. A build can sit BETWEEN corners.
```

The developer's four examples, placed:

| Weapon | Nature | Delivery | Key specs | Emergent triangle |
|--------|--------|----------|-----------|-------------------|
| **Phaser** (Trek) | Energy | Beam (~c, continuous) | high dps, light-speed, has "settings" (stun→kill = a damage dial) | **Beam** — undodgeable |
| **Blaster pistol** (Wars) | Energy | **Bolt** (discrete, *finite-v*) | low mass, slow bolt → **dodgeable** | **≈Railgun-corner behaviour with Energy nature** — the build the current model CAN'T express |
| **Plasma rifle** (many) | Energy (+thermal) | Bolt / small Blast | finite-v projectile, small AoE, overheat | between Bolt & Blast |
| **Multi-barrel machine coilgun** | Kinetic | Slug ×(many barrels) | EM-launched fast slugs, VERY high rate → high **saturation** | **between Railgun and Flak** — fast slugs that also saturate |

The coilgun is the proof that the triangle is a *continuum*: a fast-slug weapon dialed to a high enough rate/barrel
count drifts from the Railgun corner toward the Flak corner. You don't pick "Flak" — you build saturation and it
*becomes* flak-like.

---

## 2. The franchise survey (representative, meant to grow)

Nature: **K**inetic / **E**nergy / e**X**plosive / e**O**tic. Delivery: Beam/Bolt/Slug/Cloud/Guided/Blast.

| Weapon (franchise) | Nat | Delivery | What makes it distinct → where it falls |
|---|---|---|---|
| Phaser (Trek) | E | Beam | continuous ~c; stun/kill/disintegrate = a damage-magnitude dial → Beam |
| Disruptor (Trek/Wars) | E | Bolt | pulsed energy, finite-v → dodgeable Beam-nature |
| Photon/Quantum torpedo (Trek) | X | Guided | warhead + seeker → Missile |
| Blaster (Wars) | E | Bolt | slow visible bolt → **dodgeable energy** (the gap) |
| Turbolaser (Wars) | E | Bolt | blaster scaled to capital → big Bolt |
| Ion cannon (Wars) | O | Bolt | disables systems/shields, low hull dmg → **Exotic (EMP)** |
| Death Star superlaser (Wars) | E | Beam | planet-scale continuous beam → Beam at max scale |
| MAC / Gauss cannon (Halo) | K | Slug | hypervelocity slug → Railgun |
| Plasma rifle/pistol (Halo) | E | Bolt | finite-v plasma, overheat, tracks a bit → Bolt |
| Needler (Halo) | K/O | Guided | homing shards that "supercombine" → Guided + saturation |
| Spartan laser (Halo) | E | Beam | charge-up then ~c → Beam w/ charge spec |
| Bolter (40k) | K | Slug | rocket-propelled explosive-tipped round, rapid → Kinetic+X, high rate |
| Lasgun (40k) | E | Bolt | infantry las → small Bolt |
| Plasma / Melta (40k) | E | Bolt / Beam | melta = short-range anti-armour beam; plasma overheats → Beam/Bolt |
| Gauss flayer (40k) | O | Beam | strips matter atom-by-atom → **Exotic** beam |
| Mass accelerator (Mass Effect) | K | Slug | tiny slug near-c, high rate → Railgun→hitscan |
| Particle beam (Mass Effect) | E | Beam | continuous → Beam |
| Railgun (Expanse) | K | Slug | very-high-v tungsten slug → Railgun |
| PDC (Expanse) | K | Cloud | rapid autocannon wall → **Flak** |
| Torpedo (Expanse/BSG) | X | Guided | drives + warhead → Missile |
| Staff weapon / Zat (Stargate) | E/O | Bolt | staff = plasma bolt; zat = stun (Exotic) → Bolt / Exotic |
| Asgard/Ori beam (Stargate) | E | Beam | continuous → Beam |
| Lasgun (Dune) | E | Beam | **explodes if it hits a Holtzman shield** → Exotic *interaction* |
| Beam rifle / Funnel (Gundam) | E | Beam / Guided | funnel = a remote drone that fires beams → Beam + **Guided** platform |
| Vulcan / minigun (many) | K | Cloud | rapid low-cal → Flak |
| Tesla coil / Obelisk (C&C) | O/E | Beam | chain-arc lightning → Exotic chain / Beam |
| BFG / fuel rod (Doom/Halo) | E/X | Blast | AoE energy/explosive lob → **Blast** |

**What the survey demands the tree support (the dimensions):**
- **Damage nature** must include **Exotic** (EMP/ion, stun/zat, matter-strip/gauss, shield-interaction/Dune, chain/tesla)
  — not just Energy/Kinetic/Explosive. Exotic = "does something *other than* raw damage" (disable, stun, bypass).
- **Delivery** must include a **Bolt** (finite-velocity, dodgeable — energy *or* kinetic) distinct from **Beam** (~c),
  a **Cloud** (saturation), **Guided** (tracking — covers missiles AND beam-funnels/needlers), and **Blast** (AoE).
- **Platform** hint: funnels/needlers/drones = a *guided* delivery from a remote sub-unit — likely a Guided delivery +
  a "remote platform" special, not a new axis.

---

## 3. Gaps in the CURRENT model (`WeaponClass{Beam,Railgun,Missile,Flak}`)

1. **No slow ENERGY weapon.** `Beam` is hard-wired ~light-speed/undodgeable. A blaster/plasma/staff bolt is energy that
   is *finite-velocity and dodgeable* — unbuildable today. **Fix:** split nature from delivery; `Velocity` already
   exists on `WeaponProfile`, so a "slow energy" build is one line away once the axes separate.
2. **No Exotic nature.** EMP/ion, stun, matter-strip, shield-interaction, chain — a huge slice of franchises — has no
   home. Needs an Exotic nature + a small **effect** vocabulary (disable / stun / bypass-shield / bypass-armour /
   chain), likely modelled as a **special-ability component or an effect flag on the weapon** (`CONVENTIONS §6`).
3. **No AoE/Blast delivery.** Fuel-rod, BFG, grenade launchers, artillery shells — area weapons — collapse to a slug.
4. **`WeaponClass` is authored, not derived.** It should be a *computed* readout of the specs (velocity+saturation+
   tracking), so a build can sit *between* corners (the multi-barrel coilgun) instead of being forced into one.

**Net:** the current 4-class enum is a coarse *projection* of a 2-axis continuous space. Unifying (task #3) is the
moment to widen it: **Nature {Kinetic·Energy·Explosive·Exotic} × Delivery {Beam·Bolt·Slug·Cloud·Guided·Blast} + specs
→ emergent triangle.**

---

## 4. The same tree for the OTHER components (the parallel)

The developer: *"the same goes for the other components."* The class→type→spec→emergent-identity tree generalises —
each component kind has a **nature/type axis** and **specs**, and its role *emerges*:

- **Armour / defence:** type {ablative plate · reactive · deflector · energy shield · point-defence} × specs
  (thickness, coverage, regen) → the dodge/shield/armour axes the matchup already reads.
- **Sensors:** type {passive EM · active radar/lidar · thermal · gravitic · subspace/FTL} × specs (aperture,
  sensitivity, band) → detection profile (already `TechData`-driven).
- **Propulsion:** type {chemical · ion · fusion torch · antimatter · gravitic · warp/Alcubierre · jump} × specs
  (thrust, ISP, efficiency) → dV/accel/strategic-mobility.
- **Power:** type {fission · fusion · antimatter · exotic (ZPM/naquadah)} × specs (output, mass) → the supply that
  gates weapons (the P2 supply-gate in the weapon merge).

Each is its own designer; each is universal (ground + space) and gated by the chassis. Weapons is the template.

---

## 5. Open questions for the developer (before P1 widens the tree)

1. **Confirm the two-axis split** (Nature × Delivery, triangle EMERGES from specs, `WeaponClass` becomes derived). This
   is the load-bearing decision — it's what makes the blaster-vs-phaser distinction buildable.
2. **v1 Nature set:** Kinetic / Energy / Explosive / **Exotic**? (Exotic is the big scope-adder — franchises lean on it.)
3. **v1 Delivery set:** Beam / Bolt / Slug / Cloud / Guided / **Blast**? (Bolt + Blast are the two the current model
   lacks.)
4. **How do Exotic effects model** — a damage-nature that carries an *effect* (disable/stun/bypass), or a separate
   **special-ability component** bolted onto any weapon (so "an ion *version* of any gun")? The latter is more
   universal but bigger.
5. **Scope for v1 vs later:** widen the full 2-axis tree now, or ship Nature×{Beam,Bolt,Slug,Cloud} first and add
   Exotic/Blast/Guided-platforms as a depth pass?

**Recommendation:** confirm the split (Q1) and do the widening as part of P1/P3 of the merge — the unification is
already going to touch the weapon model, so widen it once rather than twice.

---

## 6. DECIDED (2026-07-06, the developer)

**Q1–Q3 = strong YES.** The two-axis split is confirmed; v1 supports all four **Natures** (Kinetic/Energy/Explosive/
**Exotic**) and all six **Deliveries** (Beam/Bolt/Slug/Cloud/Guided/Blast); the triangle position EMERGES from the
specs. **Foundation LANDED (2026-07-06):** `WeaponNature` + `WeaponDelivery` enums + `WeaponProfile.Nature`/`.Delivery`
(additive; `WeaponClass` untouched so combat is byte-identical); the base-mod beam/railgun/flak/missile profiles are
tagged (Energy·Beam / Kinetic·Slug / Kinetic·Cloud / Explosive·Guided). Gauge: `WeaponAxesTests` (a blaster = Energy +
dodgeable Bolt is now expressible). Next: make `WeaponClass` a *computed* readout of the specs, then add Bolt/Blast to
the resolve + the base-mod bolt/plasma weapons.

**Q4 (Exotic) — conditional: "only works if we can determine how it will be demonstrated in game."** Correct bar —
an exotic effect is real only if it produces a VISIBLE state change. Worked through each candidate against the actual
combat model:

| Exotic effect | In-game DEMONSTRATION | Hook that exists today | Verdict |
|---|---|---|---|
| **Disable / EMP / ion** | temporarily zeroes a target's firepower (systems offline N s) WITHOUT killing it → shows as a firepower drop / "DISABLED" status in the Battle Report; a ground unit fires nothing that salvo | `ShipCombatValueDB.Firepower` (a temporary suppression mult, same shape as `HealthPercent`); ground unit's Attack | **✅ v1** — clean, visible |
| **Bypass-armour / matter-strip (gauss)** | ignores the armour term → kills an armoured capital faster, visibly out-of-proportion vs its toughness | skip the armour contribution to `Toughness` for that weapon; ground `Defense` (`GroundDamageMatrix`) | **✅ v1** — visible |
| **Bypass-shield / anti-shield** | ignores the shield reduction → energy-vs-shield but total | ground `Shield` (`GroundDamageMatrix`) exists; **space has NO shield layer yet** | **⚠ v1 GROUND only** — space needs a shield system first (flagged) |
| **Stun (zat)** | incapacitate → **capture** instead of destroy | needs a NEW incapacitated/captured state (no such state today) | **⏳ DEFER** — build the capture state first |
| **Chain (tesla)** | arcs to several targets | overlaps the **Blast** delivery (area/multi-target) | **↩ FOLD into Blast** — not a separate exotic |

**Decision:** model exotic effects as **bolt-on special-ability components** (`CONVENTIONS §6`) that any weapon can
carry — so "an ion *version* of any gun" — but ship v1 with ONLY the demonstrable ones: **Disable** and
**Bypass-armour** (both space+ground, visible in the Battle Report), plus **Bypass-shield** on the ground (space when a
shield layer exists). **Stun/capture** waits for a capture state; **Chain** folds into Blast. Each shipped exotic MUST
write a visible line to the combat readout — that's the acceptance test the developer set.

**Space SHIELD layer — DECIDED YES (2026-07-06).** Space gets a real shield layer (it only had toughness/armour;
ground already has a shield in `GroundDamageMatrix`). This is the "shield" mechanism on the defence axis
(`UNIVERSAL-ASSEMBLY-DESIGN.md` §2b) — the space twin of the ground rule, **reusing the same nature-matchup**: a
shield soaks **Kinetic** well, **Energy bleeds through**, Explosive partial, **Exotic anti-shield bypasses** (and the
Dune lasgun-vs-shield interaction becomes a special-ability effect). Cradle-to-grave: a **shield-generator component**
(`ShieldAtb`) researched→built→installed→lost; `ShipCombatValueDB` sums it into a `Shield` value (0 if none →
**additive-safe: every existing combat fixture is byte-identical**); the resolve applies it *before* toughness.

**The one sub-decision (the demonstration knob):**
- **A — % reduction** (like ground v1): shield = an innate % damage cut, weaker vs energy. Stateless, drops into the
  aggregate resolver as a multiplier. *Less demonstrable* — no "shields at 40%."
- **B — depleting + regenerating POOL** (recommended): shield = an HP pool that absorbs, **depletes** ("shields at
  40%!"), regenerates between salvos, and once down the hull takes hits. *Highly demonstrable* (Battle Report shows
  shield %, "shields down!" is a real beat) — matches the developer's must-be-visible bar. Feasible in the bucketed
  resolver via the **already-flagged condition-tier seam** (ships bucket by combat value; shield-remaining is another
  bucket dimension — same mechanism as the parked "aggregate force condition" tiers, `Combat/CLAUDE.md`). More work
  than A, but on-brand and reuses an existing seam.

**DECIDED: B (the pool). Phase A + B BUILT (2026-07-06).**
- **Phase A** — `ShieldAtb` (a shield-generator component — `Capacity_J` pool + `RegenRate_Jps`) +
  `ShipCombatValueDB.ShieldCapacity_J`/`.ShieldRegen_Jps` (summed, health-scaled; 0 if no generator → **additive, combat
  byte-identical**).
- **Phase B** — the pool is WIRED into the live stepped resolver (`CombatEngagement`): each salvo a fleet's aggregate
  pool (`FleetCombatStateDB.ShieldPool_J`, lazy-seeded full at first contact) soaks the **soakable** part of the incoming
  fire — the **nature matchup** mirrored from ground `GroundDamageMatrix`: Kinetic `1.0` / Energy `0.5` (bleeds) /
  Explosive `0.75` / Exotic `0.0` (anti-shield bypass) — before the hull's `DamageTakenPool`, then regenerates toward
  capacity; the Battle Report narrates "shields at X% … DOWN". To keep the matchup alive through the O(ships) class-bucket
  aggregation, the fire mix now carries **Nature**. Still additive (unshielded = byte-identical). Gauges: `ShieldTests`
  (pure soak-fraction + drain/regen math; end-to-end — a shielded hull takes less kinetic fire, an exotic attacker
  bypasses). Flagged v1 simplifications: one aggregate pool per fleet, regen only under fire, capacity not doctrine-scaled,
  the four soak fractions are balance defaults.

- **Phase C** — the base-mod **Deflector Array** (a ship shield generator — `deflector-array` template binding
  `Combat.ShieldAtb`, `Shield Capacity` + `Recharge Rate` dials; six-point registration) + a shielded example ship, the
  **Bastion Shielded Cruiser** (`default-ship-design-test-shielded` — energy lasers + 2 deflectors = the Enterprise
  archetype). Closes cradle-to-grave (researched→built→installed→lost). Gauge: `ShieldBaseModTests` (the JSON
  `deflector-array` → `ShieldAtb` → `ShipCombatValueDB` pool binding through the real build path, the shield twin of
  `RailgunWeaponTests`; an unshielded Aegis reads 0). Flagged new numbers (JSON defaults for the developer's balance
  pass): Shield Capacity **5 MJ**, Recharge Rate **100 kJ/s**, and the mass/cost coefficients.

- **Phase D** — the **anti-shield exotic weapon** is BUILT: the base-mod **Ion Disruptor** (`disruptor-weapon` template
  binding `Weapons.DisruptorWeaponAtb`; `ShipCombatValueDB` reads it into a **light-speed (undodgeable), `WeaponNature.Exotic`**
  `WeaponProfile`) + a shield-piercing example ship, the **Ravager Ion Frigate** (`default-ship-design-test-disruptor`).
  Because the shield's exotic-soak is 0 (Phase B), it BYPASSES the pool and strikes the hull — the rock to the shield's
  scissors, and it isn't a strictly-better beam (modest raw yield, its whole value is the matchup). Six-point registration;
  cradle-to-grave closed. Gauge: `DisruptorWeaponTests` (JSON→atb→exotic profile through the real build path; end-to-end a
  shielded hull takes the same disruptor fire as an unshielded one, while an equal-power kinetic gun is soaked).
  Flagged new numbers (JSON defaults): Energy/Shot **150 kJ**, RoF **2/s**, `DisruptorRange_m` **400 km**, mass/cost coefficients.

**Unification step 1 BUILT (2026-07-06):** `WeaponClassifier.Classify(delivery, velocity, tracking, saturation)` DERIVES
the triangle corner (`WeaponClass`) from the Delivery axis + specs. Gauged by `WeaponClassifierTests` (every real base-mod
weapon computes to its expected corner: laser→Beam, railgun→Railgun, flak→Flak, ion-disruptor→Beam). Nature is a separate
axis, so an exotic ion beam still classifies as Beam by delivery — the two axes stay independent. (This was the foundation
for making `Class` fully computed + deleting the type slot — see below.)

**The two-axis payoff DEMONSTRATED (2026-07-06):** the base-mod **Plasma Repeater** (`plasma-repeater` template binding
`Weapons.PlasmaBoltWeaponAtb`; example ship the **Vanguard**, `default-ship-design-test-plasma`) is the corner the fused
enum had no cell for — `ShipCombatValueDB` reads it into a **`WeaponNature.Energy` + `WeaponDelivery.Bolt`** profile: a
finite-velocity bolt that reads as **Railgun-CLASS in the dodge model** (a nimble ship jukes it, like a slug) but whose
**Energy nature** means a shield only HALF-soaks it (it bleeds through, like a beam). Gauge `PlasmaBoltWeaponTests` proves
it directly: the plasma bolt and a kinetic railgun are the SAME dodge-class yet the shield soaks them differently (0.5 vs
1.0) — same Delivery, different Nature, the axes independent. Flagged JSON defaults: Energy/Shot **100 kJ**, RoF **3/s**,
Bolt Velocity **200 km/s**, Tracking **0.1**, mass/cost coefficients.

**Aggregation now carries both axes (2026-07-06):** `CombatEngagement.BuildFireMix` buckets by (class, **nature,
delivery**) — so the aggregated fire-mix keeps each weapon's real `Delivery`, and the computed corner survives
aggregation (gauged by `WeaponClassifierTests.Aggregation_PreservesNatureAndDelivery` on a real Vanguard). Without this a
missile would aggregate to the default Slug delivery and misclassify as a railgun — the latent blocker to making `Class`
emergent everywhere, cleared on the engine side.

**`WeaponClass` is now COMPUTED, and the type slot is GONE (2026-07-06, developer-approved + clean pass done).**
`WeaponProfile.Class` is an expression-bodied read-out — `=> WeaponClassifier.Classify(Delivery, Velocity, Tracking,
Saturation)` — no longer an authored, serialized field. This is the developer's "the axes are the filing-cabinet path
(Nature × Delivery); the type EMERGES from the drawer you opened + the dials, not a hand-picked label" — so **the dials
always win**. Landed in two commits: (1) the safe incremental (compute `Class`, keep an ignored `cls` ctor arg) — proved
byte-identical by CI; then (2) the **clean pass** — **the type argument is deleted from the ctor and all ~86 call sites**
(the 3 copy-ctor calls untouched), and the redundant `ComputedClass` alias removed. So a `new WeaponProfile(...)` now
takes only the dials; there is no way to hand-pick a type. Byte-identical was proved by audit + CI: `.Class` is only READ
in the fire-mix bucket key + the readout; every real base-mod weapon computes to its expected corner (the invariant test),
the one mixed-weapon fixture (`CombatBattleSims` B05 railgun+flak) computes cleanly, and the deliberately-contradictory
stress weapons are LONE weapons whose bucket label is inert. Serialization-safe (recomputed from the serialized axes).

**Next / still with-the-developer:** the fuller P1–P5 designer merge (`WEAPON-UNIFICATION-DESIGN.md`); and the Dune lasgun-vs-shield
special effect (a lasgun hitting a shield = mutual catastrophic destruction — a bespoke interaction, a later slice).
