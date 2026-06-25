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
3. ✅ **Railgun / slug weapon type** — `RailgunWeaponAtb` (`GameEngine/Weapons/WeaponRailgun/`) + the `railgun-weapon`
   JSON template (weapons.json) + component design + a `Lancer` cruiser design; finite muzzle velocity, ballistic
   (near-zero tracking), rate-of-fire → saturation. `ShipCombatValueDB.Calculate` reads it into a `Railgun`
   `WeaponProfile`. The Atb implements only `IComponentDesignAttribute` (no `IFireWeaponInstr`), so it feeds the
   auto-resolve but is invisible to the parked firing sim. **CI-green** via `RailgunWeaponTests` (builds the real
   JSON design and asserts the flavor stats + that it's dodgeable) — and `BaseModIntegrityTests` builds it from the
   real data path, so the gotcha-10 JSON→Atb binding is gauged in CI, not just on the developer's New Game (a live
   New Game spawn from DevTools is still the final confirmation).
4. ✅ **Flak / point-defense weapon type** — `FlakWeaponAtb` (`GameEngine/Weapons/WeaponFlak/`) + the `flak-weapon`
   JSON template + component design + a `Bulwark` escort; HIGH saturation (rounds/sec × pellets/shot), low per-pellet
   damage, moderate velocity. `ShipCombatValueDB.Calculate` reads it into a `Flak` `WeaponProfile` whose saturation
   FLOORS the dodge — it catches the nimble (fighters/missiles) a railgun misses. **CI-green** via `FlakWeaponTests`
   (builds the real JSON design; asserts saturation = rof×pellets and that it lands heavily on a hard dodger where a
   slug is juked). Same six-point registration as the railgun (template → StartingItems → ComponentDesigns →
   ShipDesigns), so the gotcha-10 binding is CI-gauged.
5. ✅ **Dodge + (emergent) triangle in the resolve** — `BuildFireMix`/`LandedFraction`/`HitFraction`, effective
   toughness ÷ landed fraction, hittable-first casualties. **Gauge:** slug fire kills the battleship while the
   same-toughness fighter dodges; beams ignore evasion; flak floors it. `DodgeResolveTests`. **CI-green.**
   *(The explicit `TriangleBonus` + the Capital▸Beam range edge are still refinements on top.)*
6. ✅ **Example fleets** — buildable `Wasp` fighter (tiny, 4 engines, evasive) + `Leviathan` battleship (4 railguns,
   8 armour, sluggish) anchor the dodge axis; the existing `Aegis` beam warship + `Bulwark` flak escort are the
   other corners. **CI-green** via `WeaponTriangleTests`, which proves on REAL built ships: FIGHTER ▸ railgun (the
   fighter dodges slugs the capital eats), BEAM ▸ fighter (light-speed ignores the dodge), FLAK ▸ fighter
   (saturation floors the dodge). All four spawnable from DevTools to watch the triangle live. *(The CAPITAL ▸ beam
   edge needs weapon RANGE — a v1 stub — so it's the one edge still on the v2 list.)*
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

**Stress-lab findings (2026-06-25, post-rebalance — `CombatStressLab`, 10 extreme sims; real numbers in the test
messages):**
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

**Battle-sim findings (2026-06-25, `CombatBattleSims` — the "10 more", whole-battle scenarios at the rebalanced
pace):**
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
