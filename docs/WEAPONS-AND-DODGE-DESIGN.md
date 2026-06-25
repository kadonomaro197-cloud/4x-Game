# Weapons, Dodge & the Weapon Triangle — Combat-Depth Design

*Status: design + in-progress build (started 2026-06-25). The developer crossed the `docs/MVP.md` scope firewall
on purpose to add space-combat depth. This is the spec for that arc. Master combat design stays
`docs/COMBAT-DESIGN.md`; this is the weapon-flavor/dodge layer on top of the v1 auto-resolve spine.*

---

## What this adds, in one breath

The v1 auto-resolve engine treats every weapon as one **firepower** number. This pass gives weapons **flavors**
— beams, slugs/railguns, missiles, flak — each with real pros and cons, and adds **dodge** so a small nimble
fighter survives fire that a lumbering battleship eats. The payoff the developer wants to *see*: a front-line
component of fighters + battleships under ship-to-ship fire → the **battleship count bleeds while the fighter
count holds.**

---

## The four "flavor" stats every weapon carries

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

---

## Dodge — the hit-fraction

For a given weapon firing at a given target, what fraction of its damage actually lands:

```
hitFraction = clamp( baseTrack(velocity, tracking) − target.Evasion , saturationFloor , 1 )
```

- A **beam** (≈ light-speed, high tracking) → `baseTrack ≈ 1`, so hitFraction ≈ 1 vs anything. You can't dodge light.
- A **ballistic slug** vs a high-Evasion fighter → low `baseTrack` minus high Evasion → near the floor → juked.
- A **high-saturation flak** vs that same fighter → the `saturationFloor` keeps hitFraction meaningful → the
  sky is full, the fighter eats some.

`Evasion` (built piece 1) = small size + high agility (acceleration = thrust ÷ mass). See `ShipCombatValueDB`.

---

## The weapon triangle (Fire Emblem approach)

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

---

## Performance — fleets of 100s of ships

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
a CI test will prove a 500-ship fight resolves in milliseconds.

---

## Build order (each piece its own commit, gauged in CI)

1. ✅ **Evasion stat** — size + agility (`ShipCombatValueDB.CalculateEvasion`). `ShipEvasionTests`.
2. **Weapon profiles** — the four flavor stats per weapon; saturation computed from rate-of-fire. Read real
   beams + missile stub into a `WeaponProfile` list on `ShipCombatValueDB`. Test: beam = light-speed/high-track.
3. **Railgun / slug weapon type** — new `*Atb` + weapons.json template + component. Finite velocity, ballistic
   (≈0 tracking), rate-of-fire drives saturation. The thing fighters dodge.
4. **Flak / point-defense weapon type** — high saturation, short range, low per-shot. The fighter/missile killer.
5. **Bucketed dodge + triangle in the resolve** — the aggregate model above. **Gauge:** fighters survive
   railgun fire and die to beams/flak; capitals die to railguns; the triangle holds; missiles answered by flak.
6. **Example fleets** — fighter / capital / beam-cruiser / missile-boat / flak-escort designs that demonstrate
   the triangle. Tests: the matchups resolve as designed.
7. **Performance** — a 500-ship battle benchmark + a CI test asserting it stays cheap.
8. **Docs capstone** — `Weapons/CLAUDE.md`, `Combat/CLAUDE.md`, systems map, this doc finalized.

---

## v1 stubs (honest flags, to deepen later)

- Range is in/out per system (real weapon-range geometry is v2).
- Sensors and crew **experience** do not yet feed Evasion or tracking (the developer's named next deepening).
- The new weapon types are wired for the **auto-resolve** (their design stats) — the parked per-pixel firing
  sim is NOT extended to them (it deposits ~0 damage and is a v2 visual skin).
- Triangle bonus values + evasion/saturation tuning constants are first-pass ("make it work before fair").
