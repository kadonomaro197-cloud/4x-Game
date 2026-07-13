# Weapons — Design (Taxonomy, Dodge & the Weapon Triangle)

*What this is: the full weapon-design spec for the fork — how a weapon is built (the designer's decision tree),
what it does to a target (nature vs defence), how it lands or gets dodged (delivery vs evasion), and how its
combat identity (its triangle position) falls out of the numbers. Master combat design stays
`docs/combat/COMBAT-DESIGN.md`; this is the weapon-flavor / dodge / taxonomy layer on top of the v1 auto-resolve spine.*

**Consolidated 2026-07-13 from:** `docs/combat/WEAPONS-DESIGN.md`, `docs/combat/WEAPONS-DESIGN.md`.

**Reconciliation note (read first):** two frames grew here. The older one (from the dodge depth-pass, started
2026-06-25) described weapons by **four "flavor" stats** and a **four-class enum** `WeaponClass = { Beam,
Railgun, Missile, Flak }`. The newer one (the taxonomy survey, 2026-07-06, then BUILT) found that single enum
secretly bundles **two independent axes** — **Nature** (what meets the defence) × **Delivery** (what meets the
dodge) — and that the four-class label should be *computed* from the specs, not authored. **The two-axis
taxonomy is the governing frame and the current code truth: `WeaponClass` is now a computed read-out, and the
authored type slot has been deleted from the code.** The four flavor stats and the weapon triangle are NOT
superseded — they are the *resolve mechanics* the taxonomy feeds into. This doc leads with the taxonomy, then
folds the dodge/triangle/saturation/performance depth-pass in underneath it as the mechanics of how a built
weapon actually fights.

---

## Part 1 — The Weapon Taxonomy (the governing frame)

*Source: `docs/combat/WEAPONS-DESIGN.md`. Status: DECIDED 2026-07-06, foundation + several phases BUILT (see build
ledger at the end of this Part). This is the current code truth.*

### 1.0 What it's for, in one breath

A weapon is **not a fixed thing you pick off a list.** You choose its **nature**, choose its **delivery**, dial
the **numbers**, and its combat identity — its position in the weapon triangle — *falls out*. One designer's
decision tree must be able to build *any* sci-fi weapon's essence: a Trek phaser, a Star Wars blaster, a Halo
MAC, an Expanse PDC. This Part maps a broad franchise survey onto that tree and shows where each falls.

> Governs by `docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md`: the essence emerges from the specs.

### 1.1 The headline finding — TWO axes, not one

The old model had **one** label, `WeaponClass = { Beam, Railgun, Missile, Flak }`, and it secretly bundled two
*independent* things:

- **Damage NATURE** — energy vs kinetic vs explosive vs exotic. This is what meets the *defence*: shields eat
  kinetic but bleed to energy, armour ablates, exotic bypasses. (Ground `GroundDamageMatrix` + ship armour
  already care about this.)
- **Delivery PHYSICS** — velocity regime (light-speed / fast / slow), continuous-vs-discrete, single-vs-cloud,
  guided. This is what meets the *dodge*: `Velocity` → can-you-evade-it, `Saturation` → does-volume-floor-the-
  dodge, `Tracking` → does-it-follow-you. (`WeaponProfile` already carries all three.)

**`Beam`/`Railgun`/`Flak`/`Missile` are FUSED shorthands** (e.g. "Beam" = energy-nature *and* light-speed-
delivery). That fusion is exactly why a **blaster pistol couldn't be built** in the old model: it's *energy*
nature but *slow, dodgeable* delivery — no fused label covers "slow energy bolt." Star Wars blaster bolts are
famously *dodgeable*; a phaser beam is not. Same damage nature, opposite delivery, opposite triangle position.

**The fix:** the designer picks **Nature × Delivery** separately, dials the specs, and the **triangle position
EMERGES** from `Velocity`/`Saturation`/`Tracking` — it is *derived*, never chosen. `WeaponClass` becomes a
*computed* readout ("this build behaves like a Beam-corner weapon"), not an authored enum.

### 1.2 The decision tree

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
| **Blaster pistol** (Wars) | Energy | **Bolt** (discrete, *finite-v*) | low mass, slow bolt → **dodgeable** | **≈Railgun-corner behaviour with Energy nature** — the build the old model CAN'T express |
| **Plasma rifle** (many) | Energy (+thermal) | Bolt / small Blast | finite-v projectile, small AoE, overheat | between Bolt & Blast |
| **Multi-barrel machine coilgun** | Kinetic | Slug ×(many barrels) | EM-launched fast slugs, VERY high rate → high **saturation** | **between Railgun and Flak** — fast slugs that also saturate |

The coilgun is the proof that the triangle is a *continuum*: a fast-slug weapon dialed to a high enough
rate/barrel count drifts from the Railgun corner toward the Flak corner. You don't pick "Flak" — you build
saturation and it *becomes* flak-like.

### 1.3 The franchise survey (representative, meant to grow)

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
- **Damage nature** must include **Exotic** (EMP/ion, stun/zat, matter-strip/gauss, shield-interaction/Dune,
  chain/tesla) — not just Energy/Kinetic/Explosive. Exotic = "does something *other than* raw damage" (disable,
  stun, bypass).
- **Delivery** must include a **Bolt** (finite-velocity, dodgeable — energy *or* kinetic) distinct from **Beam**
  (~c), a **Cloud** (saturation), **Guided** (tracking — covers missiles AND beam-funnels/needlers), and
  **Blast** (AoE).
- **Platform** hint: funnels/needlers/drones = a *guided* delivery from a remote sub-unit — likely a Guided
  delivery + a "remote platform" special, not a new axis.

### 1.4 Gaps in the OLD model (`WeaponClass{Beam,Railgun,Missile,Flak}`) — what the two-axis split fixed

1. **No slow ENERGY weapon.** `Beam` was hard-wired ~light-speed/undodgeable. A blaster/plasma/staff bolt is
   energy that is *finite-velocity and dodgeable* — was unbuildable. **Fix:** split nature from delivery;
   `Velocity` already existed on `WeaponProfile`, so a "slow energy" build is one line away once the axes
   separate.
2. **No Exotic nature.** EMP/ion, stun, matter-strip, shield-interaction, chain — a huge slice of franchises —
   had no home. Needs an Exotic nature + a small **effect** vocabulary (disable / stun / bypass-shield /
   bypass-armour / chain), modelled as a **special-ability component or an effect flag on the weapon**
   (`CONVENTIONS §6`).
3. **No AoE/Blast delivery.** Fuel-rod, BFG, grenade launchers, artillery shells — area weapons — collapsed to a
   slug.
4. **`WeaponClass` was authored, not derived.** It should be a *computed* readout of the specs
   (velocity+saturation+tracking), so a build can sit *between* corners (the multi-barrel coilgun) instead of
   being forced into one.

**Net:** the old 4-class enum is a coarse *projection* of a 2-axis continuous space. The unification widened it:
**Nature {Kinetic·Energy·Explosive·Exotic} × Delivery {Beam·Bolt·Slug·Cloud·Guided·Blast} + specs → emergent
triangle.**

### 1.5 The same tree for the OTHER components (the parallel)

The developer: *"the same goes for the other components."* The class→type→spec→emergent-identity tree
generalises — each component kind has a **nature/type axis** and **specs**, and its role *emerges*:

- **Armour / defence:** type {ablative plate · reactive · deflector · energy shield · point-defence} × specs
  (thickness, coverage, regen) → the dodge/shield/armour axes the matchup already reads.
- **Sensors:** type {passive EM · active radar/lidar · thermal · gravitic · subspace/FTL} × specs (aperture,
  sensitivity, band) → detection profile (already `TechData`-driven).
- **Propulsion:** type {chemical · ion · fusion torch · antimatter · gravitic · warp/Alcubierre · jump} × specs
  (thrust, ISP, efficiency) → dV/accel/strategic-mobility.
- **Power:** type {fission · fusion · antimatter · exotic (ZPM/naquadah)} × specs (output, mass) → the supply
  that gates weapons (the P2 supply-gate in the weapon merge).

Each is its own designer; each is universal (ground + space) and gated by the chassis. Weapons is the template.

### 1.6 Open questions raised — and how they were DECIDED

Original open questions (before P1 widened the tree): confirm the two-axis split; the v1 Nature set; the v1
Delivery set; how Exotic effects model (nature-carries-effect vs separate special-ability component); scope for
v1 vs later. **Recommendation at the time:** confirm the split and do the widening as part of the merge — the
unification was already going to touch the weapon model, so widen it once rather than twice.

**DECIDED (2026-07-06, the developer):**

**Q1–Q3 = strong YES.** The two-axis split is confirmed; v1 supports all four **Natures** (Kinetic/Energy/
Explosive/**Exotic**) and all six **Deliveries** (Beam/Bolt/Slug/Cloud/Guided/Blast); the triangle position
EMERGES from the specs.

**Q4 (Exotic) — conditional: "only works if we can determine how it will be demonstrated in game."** Correct bar
— an exotic effect is real only if it produces a VISIBLE state change. Worked through each candidate against the
actual combat model:

| Exotic effect | In-game DEMONSTRATION | Hook that exists today | Verdict |
|---|---|---|---|
| **Disable / EMP / ion** | temporarily zeroes a target's firepower (systems offline N s) WITHOUT killing it → shows as a firepower drop / "DISABLED" status in the Battle Report; a ground unit fires nothing that salvo | `ShipCombatValueDB.Firepower` (a temporary suppression mult, same shape as `HealthPercent`); ground unit's Attack | **v1** — clean, visible |
| **Bypass-armour / matter-strip (gauss)** | ignores the armour term → kills an armoured capital faster, visibly out-of-proportion vs its toughness | skip the armour contribution to `Toughness` for that weapon; ground `Defense` (`GroundDamageMatrix`) | **v1** — visible |
| **Bypass-shield / anti-shield** | ignores the shield reduction → energy-vs-shield but total | ground `Shield` (`GroundDamageMatrix`) exists; **space has NO shield layer yet** | **v1 GROUND only** — space needs a shield system first (flagged, now BUILT — see 1.7) |
| **Stun (zat)** | incapacitate → **capture** instead of destroy | needs a NEW incapacitated/captured state (no such state today) | **DEFER** — build the capture state first |
| **Chain (tesla)** | arcs to several targets | overlaps the **Blast** delivery (area/multi-target) | **FOLD into Blast** — not a separate exotic |

**Decision:** model exotic effects as **bolt-on special-ability components** (`CONVENTIONS §6`) that any weapon
can carry — so "an ion *version* of any gun" — but ship v1 with ONLY the demonstrable ones: **Disable** and
**Bypass-armour** (both space+ground, visible in the Battle Report), plus **Bypass-shield** on the ground (space
once the shield layer exists — now built). **Stun/capture** waits for a capture state; **Chain** folds into
Blast. Each shipped exotic MUST write a visible line to the combat readout — that's the acceptance test the
developer set.

### 1.7 Space SHIELD layer — DECIDED YES (2026-07-06), and how it demonstrates

Space gets a real shield layer (it only had toughness/armour; ground already has a shield in
`GroundDamageMatrix`). This is the "shield" mechanism on the defence axis (`docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md` §2b) —
the space twin of the ground rule, **reusing the same nature-matchup**: a shield soaks **Kinetic** well, **Energy
bleeds through**, Explosive partial, **Exotic anti-shield bypasses** (and the Dune lasgun-vs-shield interaction
becomes a special-ability effect). Cradle-to-grave: a **shield-generator component** (`ShieldAtb`)
researched→built→installed→lost; `ShipCombatValueDB` sums it into a `Shield` value (0 if none → **additive-safe:
every existing combat fixture is byte-identical**); the resolve applies it *before* toughness.

**The one sub-decision (the demonstration knob):**
- **A — % reduction** (like ground v1): shield = an innate % damage cut, weaker vs energy. Stateless, drops into
  the aggregate resolver as a multiplier. *Less demonstrable* — no "shields at 40%."
- **B — depleting + regenerating POOL** (recommended, CHOSEN): shield = an HP pool that absorbs, **depletes**
  ("shields at 40%!"), regenerates between salvos, and once down the hull takes hits. *Highly demonstrable*
  (Battle Report shows shield %, "shields down!" is a real beat) — matches the developer's must-be-visible bar.
  Feasible in the bucketed resolver via the **already-flagged condition-tier seam** (ships bucket by combat
  value; shield-remaining is another bucket dimension — same mechanism as the parked "aggregate force condition"
  tiers, see Part 4). More work than A, but on-brand and reuses an existing seam.

### 1.8 Taxonomy build ledger — what LANDED (2026-07-06)

*Build-state honesty: these are engine + base-mod changes gated by CI tests only. "Built-and-CI-green" means the
engine logic compiled and its NUnit test passes on the Linux runner — it does NOT mean a human watched it run in
the client. A live New-Game / DevTools spawn is still the final confirmation for anything player-facing.*

- **Foundation LANDED (built-and-CI-green).** `WeaponNature` + `WeaponDelivery` enums + `WeaponProfile.Nature` /
  `.Delivery` (additive; `WeaponClass` untouched at first so combat was byte-identical); the base-mod
  beam/railgun/flak/missile profiles are tagged (Energy·Beam / Kinetic·Slug / Kinetic·Cloud / Explosive·Guided).
  Gauge: `WeaponAxesTests` (a blaster = Energy + dodgeable Bolt is now expressible).

- **Space SHIELD — Phase A + B BUILT (built-and-CI-green).**
  - **Phase A** — `ShieldAtb` (a shield-generator component — `Capacity_J` pool + `RegenRate_Jps`) +
    `ShipCombatValueDB.ShieldCapacity_J` / `.ShieldRegen_Jps` (summed, health-scaled; 0 if no generator →
    **additive, combat byte-identical**).
  - **Phase B** — the pool is WIRED into the live stepped resolver (`CombatEngagement`): each salvo a fleet's
    aggregate pool (`FleetCombatStateDB.ShieldPool_J`, lazy-seeded full at first contact) soaks the **soakable**
    part of the incoming fire — the **nature matchup** mirrored from ground `GroundDamageMatrix`: Kinetic `1.0` /
    Energy `0.5` (bleeds) / Explosive `0.75` / Exotic `0.0` (anti-shield bypass) — before the hull's
    `DamageTakenPool`, then regenerates toward capacity; the Battle Report narrates "shields at X% … DOWN". To
    keep the matchup alive through the O(ships) class-bucket aggregation, the fire mix now carries **Nature**.
    Still additive (unshielded = byte-identical). Gauges: `ShieldTests` (pure soak-fraction + drain/regen math;
    end-to-end — a shielded hull takes less kinetic fire, an exotic attacker bypasses). **Flagged v1
    simplifications:** one aggregate pool per fleet, regen only under fire, capacity not doctrine-scaled, the four
    soak fractions are balance defaults.
  - **Phase C** — the base-mod **Deflector Array** (a ship shield generator — `deflector-array` template binding
    `Combat.ShieldAtb`, `Shield Capacity` + `Recharge Rate` dials; six-point registration) + a shielded example
    ship, the **Bastion Shielded Cruiser** (`default-ship-design-test-shielded` — energy lasers + 2 deflectors =
    the Enterprise archetype). Closes cradle-to-grave (researched→built→installed→lost). Gauge:
    `ShieldBaseModTests` (the JSON `deflector-array` → `ShieldAtb` → `ShipCombatValueDB` pool binding through the
    real build path, the shield twin of `RailgunWeaponTests`; an unshielded Aegis reads 0). **Flagged new numbers**
    (JSON defaults for the developer's balance pass): Shield Capacity **5 MJ**, Recharge Rate **100 kJ/s**, and
    the mass/cost coefficients.
  - **Phase D** — the **anti-shield exotic weapon** is BUILT: the base-mod **Ion Disruptor** (`disruptor-weapon`
    template binding `Weapons.DisruptorWeaponAtb`; `ShipCombatValueDB` reads it into a **light-speed (undodgeable),
    `WeaponNature.Exotic`** `WeaponProfile`) + a shield-piercing example ship, the **Ravager Ion Frigate**
    (`default-ship-design-test-disruptor`). Because the shield's exotic-soak is 0 (Phase B), it BYPASSES the pool
    and strikes the hull — the rock to the shield's scissors, and it isn't a strictly-better beam (modest raw
    yield, its whole value is the matchup). Six-point registration; cradle-to-grave closed. Gauge:
    `DisruptorWeaponTests` (JSON→atb→exotic profile through the real build path; end-to-end a shielded hull takes
    the same disruptor fire as an unshielded one, while an equal-power kinetic gun is soaked). **Flagged new
    numbers** (JSON defaults): Energy/Shot **150 kJ**, RoF **2/s**, `DisruptorRange_m` **400 km**, mass/cost
    coefficients.

- **Unification step 1 BUILT (built-and-CI-green):** `WeaponClassifier.Classify(delivery, velocity, tracking,
  saturation)` DERIVES the triangle corner (`WeaponClass`) from the Delivery axis + specs. Gauged by
  `WeaponClassifierTests` (every real base-mod weapon computes to its expected corner: laser→Beam,
  railgun→Railgun, flak→Flak, ion-disruptor→Beam). Nature is a separate axis, so an exotic ion beam still
  classifies as Beam by delivery — the two axes stay independent. (This was the foundation for making `Class`
  fully computed + deleting the type slot.)

- **The two-axis payoff DEMONSTRATED (built-and-CI-green):** the base-mod **Plasma Repeater** (`plasma-repeater`
  template binding `Weapons.PlasmaBoltWeaponAtb`; example ship the **Vanguard**, `default-ship-design-test-plasma`)
  is the corner the fused enum had no cell for — `ShipCombatValueDB` reads it into a **`WeaponNature.Energy` +
  `WeaponDelivery.Bolt`** profile: a finite-velocity bolt that reads as **Railgun-CLASS in the dodge model** (a
  nimble ship jukes it, like a slug) but whose **Energy nature** means a shield only HALF-soaks it (it bleeds
  through, like a beam). Gauge `PlasmaBoltWeaponTests` proves it directly: the plasma bolt and a kinetic railgun
  are the SAME dodge-class yet the shield soaks them differently (0.5 vs 1.0) — same Delivery, different Nature,
  the axes independent. **Flagged JSON defaults:** Energy/Shot **100 kJ**, RoF **3/s**, Bolt Velocity **200 km/s**,
  Tracking **0.1**, mass/cost coefficients.

- **Aggregation now carries both axes (built-and-CI-green):** `CombatEngagement.BuildFireMix` buckets by (class,
  **nature, delivery**) — so the aggregated fire-mix keeps each weapon's real `Delivery`, and the computed corner
  survives aggregation (gauged by `WeaponClassifierTests.Aggregation_PreservesNatureAndDelivery` on a real
  Vanguard). Without this a missile would aggregate to the default Slug delivery and misclassify as a railgun —
  the latent blocker to making `Class` emergent everywhere, cleared on the engine side.

- **`WeaponClass` is now COMPUTED, and the type slot is GONE (built-and-CI-green, developer-approved + clean pass
  done).** `WeaponProfile.Class` is an expression-bodied read-out — `=> WeaponClassifier.Classify(Delivery,
  Velocity, Tracking, Saturation)` — no longer an authored, serialized field. This is the developer's "the axes
  are the filing-cabinet path (Nature × Delivery); the type EMERGES from the drawer you opened + the dials, not a
  hand-picked label" — so **the dials always win**. Landed in two commits: (1) the safe incremental (compute
  `Class`, keep an ignored `cls` ctor arg) — proved byte-identical by CI; then (2) the **clean pass** — the type
  argument is deleted from the ctor and all ~86 call sites (the 3 copy-ctor calls untouched), and the redundant
  `ComputedClass` alias removed. So a `new WeaponProfile(...)` now takes only the dials; there is no way to
  hand-pick a type. Byte-identical was proved by audit + CI: `.Class` is only READ in the fire-mix bucket key +
  the readout; every real base-mod weapon computes to its expected corner (the invariant test), the one
  mixed-weapon fixture (`CombatBattleSims` B05 railgun+flak) computes cleanly, and the deliberately-contradictory
  stress weapons are LONE weapons whose bucket label is inert. Serialization-safe (recomputed from the serialized
  axes).

**Next / still with-the-developer:** the fuller P1–P5 designer merge (`WEAPON-UNIFICATION-DESIGN.md`); and the
Dune lasgun-vs-shield special effect (a lasgun hitting a shield = mutual catastrophic destruction — a bespoke
interaction, a later slice).

---

## Part 2 — The Dodge Depth-Pass: flavor stats, dodge, triangle, performance

*Source: `docs/combat/WEAPONS-DESIGN.md`. Status: design + build started 2026-06-25; the dodge MODEL + the
buildable weapon components are built-and-CI-green (ledger below). This is the RESOLVE MECHANICS the taxonomy
above feeds into — how a built weapon actually lands or misses. The four "flavor" stats here are NOT superseded
by the taxonomy; they ARE the specs the taxonomy dials, and `WeaponClass` (Beam/Railgun/Missile/Flak) is the
computed corner they emerge into.*

### 2.0 What this adds, in one breath

The v1 auto-resolve engine treats every weapon as one **firepower** number. This pass gives weapons **flavors**
— beams, slugs/railguns, missiles, flak — each with real pros and cons, and adds **dodge** so a small nimble
fighter survives fire that a lumbering battleship eats. The payoff the developer wants to *see*: a front-line
component of fighters + battleships under ship-to-ship fire → the **battleship count bleeds while the fighter
count holds.**

### 2.1 The four "flavor" stats every weapon carries

*(These map onto the taxonomy's Delivery physics + specs — they are how a weapon meets the dodge.)*

| Stat | What it means | Beam | Railgun/Slug | Missile | Flak/PD |
|------|---------------|------|--------------|---------|---------|
| **Damage/sec** (J/s) | what a hit does | high | high (single) | high (warhead) | low |
| **Velocity** (m/s) | how fast the shot travels → *dodgeability* | ≈ light-speed | fast, finite | slow | short-flight |
| **Tracking** (0..1) | how well it follows an evasive target | high | ~0 (ballistic) | high (guided) | medium |
| **Saturation** (tracks/sec) | *computed* from rate-of-fire × projectiles × spread — the **floor** on how much still lands on the evasive | low | scales w/ RoF | wave-size | **high** |
| Range *(v1 stub)* | in/out per system; real range is v2 | med | long | long | short |
| Damage type / wavelength | energy vs kinetic (beams carry wavelength → armour absorption already exists) | energy | kinetic | kinetic | kinetic |

**Saturation is derived, never hand-set.** A flak cannon firing once a minute is high-spread but useless; a
1000-round/sec spinal slug saturates the sky. So `Saturation = rateOfFire × projectilesPerShot × spreadFactor`.
Rate of fire for a beam comes from its `ChargePeriod`; for ammo weapons from `ReloadAmountPerSec / AmountPerShot`.

### 2.2 Dodge — the hit-fraction

For a given weapon firing at a given target, what fraction of its damage actually lands:

```
hitFraction = clamp( baseTrack(velocity, tracking) − target.Evasion , saturationFloor , 1 )
```

- A **beam** (≈ light-speed, high tracking) → `baseTrack ≈ 1`, so hitFraction ≈ 1 vs anything. You can't dodge
  light.
- A **ballistic slug** vs a high-Evasion fighter → low `baseTrack` minus high Evasion → near the floor → juked.
- A **high-saturation flak** vs that same fighter → the `saturationFloor` keeps hitFraction meaningful → the
  sky is full, the fighter eats some.

`Evasion` (built piece 1) = small size + high agility (acceleration = thrust ÷ mass). See `ShipCombatValueDB`.

### 2.3 The weapon triangle (Fire Emblem approach)

A core rock-paper-scissors that **emerges from the stats above**, then sharpened with a small tunable bonus
(the FE "triangle bonus" — a JSON modifier, not a rewrite):

```
        BEAM ──beats──▶ FIGHTER
          ▲               │
        beats           beats
          │               ▼
        CAPITAL ◀──────────┘   (railgun + armour)
```

- **Beam ▸ Fighter** — light-speed ignores evasion.
- **Fighter ▸ Capital** — dodges the slow railgun slugs, closes to knife range.
- **Capital ▸ Beam** — armour + range out-tank and out-reach the power-hungry lasers.

**Off the triangle — the pieces that sit outside it (FE's bows/magic):**
- **Missiles** — long reach, *track* (hit the evasive), saturate in waves. The threat every fleet must answer.
- **Flak / Point-defense** — the hard counter to **both** missiles and fighters (high saturation, short range),
  but only tickles a capital. The **support/center** node. A balanced fleet wants some.

So the shape is a **triangle with a Missile⟷Flak axis crossing through it.** Most of it falls out of
velocity/tracking/evasion/armour/range; the explicit `TriangleBonus[attackerType][targetType]` (JSON) is only
there to make the feel crisp.

**Connection to the taxonomy (Part 1):** these four corners (Beam/Railgun/Missile/Flak) are exactly the
`WeaponClass` enum — which is now **computed** from the Nature × Delivery specs, not authored. A build can sit
*between* corners (the taxonomy's multi-barrel coilgun / the plasma bolt). So the triangle is the read-out of
where a designed weapon lands, and the corners are poles of a continuum, not a fixed menu.

### 2.4 Performance — fleets of 100s of ships

**The trap:** computing dodge *per ship × per weapon × per target* is O(ships²) and dies at ~200 ships a side.

**The architecture (built in from the first line of the resolve):** keep the math **aggregate / bucketed.**
1. Bucket each side's ships into a handful of **evasion bands** (e.g. 5: 0–.2, .2–.4, …) and a toughness figure.
2. Bucket each side's fire by **weapon type** (beam / railgun / missile / flak), each carrying its
   velocity / tracking / saturation.
3. For each weapon type, spread its **total** damage across the enemy's evasion bands, weighted by
   `hitFraction(type, band)`. Apply the triangle bonus here.
4. Remove **whole ships** from each band (combatants before utility, as today). No per-hitpoint bookkeeping.

Cost per step = **O(weaponTypes × evasionBands) ≈ constant.** Ship count only enters the once-per-step
bucketing (O(ships)). **A 500-ship battle costs about the same as a 50-ship one.** A BenchmarkDotNet bench +
a CI test proves a 500-ship fight resolves in milliseconds.

### 2.5 Dodge build order (each piece its own commit, gauged in CI)

**The dodge MODEL is built and CI-green (done in order 1→2→5→7). The real player-buildable weapon COMPONENTS
(3, 4) and the fleet demos (6) are the remaining plumbing.** Build-state honesty: "CI-green" = engine test passes
on the Linux runner; a live New-Game / DevTools spawn is the final confirmation for player-facing pieces.

1. **Evasion stat** — size + agility (`ShipCombatValueDB.CalculateEvasion`). `ShipEvasionTests`. **CI-green.**
2. **Weapon profiles** — the four flavor stats per weapon; saturation from rate-of-fire. `WeaponProfile` list
   on `ShipCombatValueDB`; beams read real, missiles stubbed; Firepower = sum. `WeaponProfileTests`. **CI-green.**
3. **Railgun / slug weapon type** — `RailgunWeaponAtb` (`GameEngine/Weapons/WeaponRailgun/`) + the
   `railgun-weapon` JSON template (weapons.json) + component design + a `Lancer` cruiser design; finite muzzle
   velocity, ballistic (near-zero tracking), rate-of-fire → saturation. `ShipCombatValueDB.Calculate` reads it
   into a `Railgun` `WeaponProfile`. The Atb implements only `IComponentDesignAttribute` (no `IFireWeaponInstr`),
   so it feeds the auto-resolve but is invisible to the parked firing sim. **CI-green** via `RailgunWeaponTests`
   (builds the real JSON design and asserts the flavor stats + that it's dodgeable) — and `BaseModIntegrityTests`
   builds it from the real data path, so the gotcha-10 JSON→Atb binding is gauged in CI, not just on the
   developer's New Game (a live New Game spawn from DevTools is still the final confirmation).
4. **Flak / point-defense weapon type** — `FlakWeaponAtb` (`GameEngine/Weapons/WeaponFlak/`) + the `flak-weapon`
   JSON template + component design + a `Bulwark` escort; HIGH saturation (rounds/sec × pellets/shot), low
   per-pellet damage, moderate velocity. `ShipCombatValueDB.Calculate` reads it into a `Flak` `WeaponProfile`
   whose saturation FLOORS the dodge — it catches the nimble (fighters/missiles) a railgun misses. **CI-green**
   via `FlakWeaponTests` (builds the real JSON design; asserts saturation = rof×pellets and that it lands heavily
   on a hard dodger where a slug is juked). Same six-point registration as the railgun (template → StartingItems
   → ComponentDesigns → ShipDesigns), so the gotcha-10 binding is CI-gauged.
5. **Dodge + (emergent) triangle in the resolve** — `BuildFireMix`/`LandedFraction`/`HitFraction`, effective
   toughness ÷ landed fraction, hittable-first casualties. **Gauge:** slug fire kills the battleship while the
   same-toughness fighter dodges; beams ignore evasion; flak floors it. `DodgeResolveTests`. **CI-green.**
   *(The explicit `TriangleBonus` + the Capital▸Beam range edge are still refinements on top.)*
6. **Example fleets** — buildable `Wasp` fighter (tiny, 4 engines, evasive) + `Leviathan` battleship (4 railguns,
   8 armour, sluggish) anchor the dodge axis; the existing `Aegis` beam warship + `Bulwark` flak escort are the
   other corners. **CI-green** via `WeaponTriangleTests`, which proves on REAL built ships: FIGHTER ▸ railgun (the
   fighter dodges slugs the capital eats), BEAM ▸ fighter (light-speed ignores the dodge), FLAK ▸ fighter
   (saturation floors the dodge). All four spawnable from DevTools to watch the triangle live. *(The CAPITAL ▸
   beam edge needs weapon RANGE — a v1 stub — so it's the one edge still on the v2 list.)*
7. **Performance** — fire aggregated by weapon class → O(ships) per step; `CombatPerformanceTests` resolves
   200 real warships in milliseconds. **CI-green.**
8. **Docs capstone** — `Combat/CLAUDE.md` (per piece), this doc, systems map, test inventory, SESSION_STATE.

### 2.6 v1 stubs (honest flags, to deepen later)

- Range is in/out per system (real weapon-range geometry is v2).
- Sensors and crew **experience** do not yet feed Evasion or tracking (the developer's named next deepening).
- The new weapon types are wired for the **auto-resolve** (their design stats) — the parked per-pixel firing
  sim is NOT extended to them (it deposits ~0 damage and is a v2 visual skin).
- Triangle bonus values + evasion/saturation tuning constants are first-pass ("make it work before fair").
- **Damage-vs-toughness pace — REBALANCED 2026-06-25 (was HOT).** Raw numbers had a railgun at ~1 MJ/s vs a
  ~1 MJ hull, so a volley one-shot a wing of fighters and whole fleet battles ended in **2–4 salvos (10–20
  game-seconds)** — over before the default 1-hour master tick. Fixed by **`CombatEngagement.SalvoDamageScale`
  (0.1)**: a salvo now deposits a tenth of its raw energy toward kills, so a ship lasts **~10× more salvos** and
  the rock-paper-scissors plays out gradually (a standard 50v50 now runs 38 salvos ≈ 190 game-seconds — watchable
  and steerable). The scale is **uniform**, so it changed battle DURATION, not who wins — every triangle / dodge /
  doctrine finding held (see `CombatStressLab` + `CombatBattleSims`). One emergent shift worth knowing: the slower
  pace lets the **50%-loss retreat actually trigger**, so a few matchups that used to be wipes are now break-offs
  — e.g. a 150-fighter swarm now *retreats* from a super-capital it used to wipe; it takes ~400 to overwhelm it.
  Tune via the one constant (see the constants table in `GameEngine/Combat/CLAUDE.md`).

### 2.7 Stress-lab findings (2026-06-25, post-rebalance)

*(`CombatStressLab`, 10 extreme sims; real numbers in the test messages.)*
- **Three independent ways to defeat evasion fall out of the model**, not just flak: high **saturation** (a spinal
  slug at rof 1000 killed 38/100 nimble fighters vs 5/100 for a normal railgun), high **velocity** (a near-light
  railgun: 39/100 vs 5/100), and a beam (both at once). Good emergent design space — a "fighter-killer" can be a
  fast-firing OR a high-velocity gun.
- **Nothing is untouchable** — the `MinLandedFraction` floor means even a normal slug grinds down max-evasion
  (0.95) fighters over time; extreme saturation wipes them outright (39/100 vs 3/100 in 16 salvos).
- **Fair + scalable**: a 100v100 mirror resolves *exactly* even (now **50–50** — the slower pace lands both right
  on the 50% retreat line, no first-mover edge); the dodge advantage still shows at fleet scale (30 railgun ships
  leave 85/100 of an evasive screen, 30 equal-firepower beam ships leave 49/100); doctrine ×2 swings a 50v50 fight
  ~1.6:1 (40–25, the loser breaking off at half losses).
- **Exchange ratio quantified**: one capital is worth ~25–50 of these fighters (break-even with survivors at
  N=50). The rebalance made **retreat bite**: a super-capital now tanks long enough that a 150-swarm breaks off
  before killing it — it takes ~400 fighters to overwhelm it (at hot damage the swarm wiped it in one volley).

### 2.8 Battle-sim findings (2026-06-25)

*(`CombatBattleSims` — the "10 more", whole-battle scenarios at the rebalanced pace.)*
- **Battles last many salvos now**: a standard 50v50 mirror runs **38 salvos = 190 game-seconds** (was 2–4), and
  duration scales ~linearly with toughness (×1/×4/×16 → 38 / 150 / 599 salvos) — the pace dial is predictable.
- **The frontiers are smooth curves**: saturation 1/10/100/1000 → 5 / 6 / 26 / 38 kills of 100; evasion
  0/.3/.6/.9/.95 → 40 / 28 / 17 / 5 / 3 kills — the dodge model is a gradient, not a cliff.
- **Combined arms > mono** at equal firepower vs a mixed enemy (railgun+flak leaves 50 of 100 alive, mono railgun
  66). **Quality endures over quantity** at equal aggregate firepower *and* toughness (5 heavy keep 60%, 50 light
  keep 48% — the dispersed force hits its break-off threshold first).
- **Multi-party + steering work as battles**: a symmetric 3-way free-for-all resolves symmetrically (5/5/5); a
  15-ship reinforcement joining a losing 10-v-20 drops the enemy from 18 survivors to 10 (break-off); a mid-fight
  doctrine switch turns an even 30v30 mirror (15–15) into a 22–15 win. Extreme asymmetry stays cheap: 1
  dreadnought vs **1000** gnats resolves in **9 ms** (the O(buckets) resolve holds).

---

## Part 3 — Future depth: aggregate force condition ("Degraded" tiers)

*Captured 2026-06-25 from the developer. The natural next layer once damage persists between fights, and it
slots into the class-bucketing resolve almost for free. NOT built — this is a design capture. It is also the seam
the shield POOL (Part 1.7 Phase B) reused, so it is half-realized in that specific dimension.*

**The idea.** Don't track individual ship components at fleet scale. Bucket ships within a class by a coarse
**condition tier** — Pristine / Lightly / Moderately / Severely Degraded — and apply a debuff per tier (reduced
firepower / toughness / evasion / speed). The fleet readout drills **Fleet → Component → Class → Condition**
("this carrier wing: 60% Pristine, 25% Lightly, 15% Severely Degraded"), and combat applies the per-tier
modifiers. The commander then decides *with condition in view*: a standing order becomes **"Launch all
Non-Degraded Fighters,"** not "Launch All Fighters" — you think twice before committing a beat-up wing.

**Why it's nearly free given the bucketing.** The dodge resolve buckets ships by their combat-relevant stats
(everything that decides how a ship fights and dies). A degraded ship simply has a *different* combat value →
it lands in a *different* bucket, automatically. So "condition tier" is just one more reason two same-class
ships aren't interchangeable — the *same* mechanism that already separates a fighter from a battleship. **No new
resolve code.** The one prerequisite is the piece deliberately parked in v1: a combat value that DEGRADES as a
ship takes damage (the v2 "recalc-on-damage" hook on `ShipCombatValueDB`).

**Connected systems (map before building):**
- **Damage** — needs *cumulative* ship damage to move a ship between tiers (or to recompute its combat value).
  Today combat value is fixed at build (v1, whole-ship removal). This is the v2 recalc-on-damage hook.
- **Repair / maintenance / logistics** — a ship climbs back up the tiers only via repair (shipyard, an
  Aurora-style maintenance-supply economy). A degraded fleet becomes a logistics liability → *condition
  management is strategy.* Connects to colonies / industry / logistics.
- **Fleet orders** — "launch / commit / hold *by condition*" needs orders that filter a class by tier. Connects
  to the order system + carrier/fighter launch (`ColonyInfoDB.FighterStockpile`, parasite craft).
- **Carrier / fighter system** — the motivating example: fighters launch, fight, return, repair; condition
  makes "commit the damaged wing?" a real call.
- **UI** — a Fleet → Component → Class → Condition table (extends the System-4 fleet-combat table).
- **NPC doctrine** — an AI deciding whether to spend degraded forces reads the tier.

**The principle it expresses (and the developer's meta-observation, captured):**
> **Simulate at the granularity of the DECISION, not the entity.** The player decides at fleet → component →
> class → condition, so model *there* — in counts and tiers — and reify down to a specific ship entity only
> where an individual carries meaning (a flagship, a named commander, the objective). The individual isn't
> lost; it's recovered on demand. This is Lanchester's attrition math / operations-research thinking, and how
> real navies track *readiness states* rather than every rivet.
>
> Corollary (the developer's words): *"the further this goes, the less it's about the individual and the more
> about the assembly of individuals."* That is correct and intended — it is what lets the model scale to
> thousands of ships and stay legible.
