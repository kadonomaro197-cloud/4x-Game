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

**The dodge MODEL is built and CI-green (done in order 1→2→5→7). The real player-buildable weapon COMPONENTS
(3, 4) and the fleet demos (6) are the remaining plumbing — see the note below.**

1. ✅ **Evasion stat** — size + agility (`ShipCombatValueDB.CalculateEvasion`). `ShipEvasionTests`. **CI-green.**
2. ✅ **Weapon profiles** — the four flavor stats per weapon; saturation from rate-of-fire. `WeaponProfile` list
   on `ShipCombatValueDB`; beams read real, missiles stubbed; Firepower = sum. `WeaponProfileTests`. **CI-green.**
3. ⏳ **Railgun / slug weapon type** — new `*Atb` + weapons.json template + component (finite velocity, ballistic,
   rate-of-fire → saturation). **Remaining.** Heaviest piece: it needs the NCalc component-designer template
   system. Risky to do blind (the runtime template→attribute path isn't fully covered by CI — gotcha 10);
   wants a careful pass + a local New Game check. *Until then the dodge model is exercised by stamping Railgun
   `WeaponProfile`s directly in tests, which is enough to prove the behavior.*
4. ⏳ **Flak / point-defense weapon type** — high saturation, short range, low per-shot. Same plumbing as #3.
   **Remaining.**
5. ✅ **Dodge + (emergent) triangle in the resolve** — `BuildFireMix`/`LandedFraction`/`HitFraction`, effective
   toughness ÷ landed fraction, hittable-first casualties. **Gauge:** slug fire kills the battleship while the
   same-toughness fighter dodges; beams ignore evasion; flak floors it. `DodgeResolveTests`. **CI-green.**
   *(The explicit `TriangleBonus` + the Capital▸Beam range edge are still refinements on top.)*
6. ⏳ **Example fleets** — fighter / capital / beam-cruiser / missile-boat / flak-escort designs that demonstrate
   the triangle live. **Remaining** (depends on the real components from 3/4 to be player-buildable).
7. ✅ **Performance** — fire aggregated by weapon class → O(ships) per step; `CombatPerformanceTests` resolves
   200 real warships in milliseconds. **CI-green.**
8. ✅ **Docs capstone** — `Combat/CLAUDE.md` (per piece), this doc, systems map, test inventory, SESSION_STATE.

---

## v1 stubs (honest flags, to deepen later)

- Range is in/out per system (real weapon-range geometry is v2).
- Sensors and crew **experience** do not yet feed Evasion or tracking (the developer's named next deepening).
- The new weapon types are wired for the **auto-resolve** (their design stats) — the parked per-pixel firing
  sim is NOT extended to them (it deposits ~0 damage and is a v2 visual skin).
- Triangle bonus values + evasion/saturation tuning constants are first-pass ("make it work before fair").

---

## Future depth — aggregate force condition ("Degraded" tiers)

*Captured 2026-06-25 from the developer. The natural next layer once damage persists between fights, and it
slots into the class-bucketing resolve almost for free.*

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
