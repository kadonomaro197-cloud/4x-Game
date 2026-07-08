# Auto-Resolver Anatomy & Dial-Insertion Map

**As of:** 2026-07-08 · the resolver taken apart bit by bit, so every designer dial has a **known insertion point** and battles are authentic. Companion to `COMPONENT-DESIGNER-DIALS.md` (§0e is the number anchor). Source of truth: `GameEngine/Combat/` (`ShipCombatValueDB.cs`, `CombatEngagement.cs`, `WeaponProfile.cs`) + `Combat/CLAUDE.md`.

**The finding in one line:** the resolver reads a **small, fixed input surface** — one `WeaponProfile` per weapon (**7 fields**) + a handful of per-ship values + ~10 global constants. A dial is authentic **only if it lands on that surface.** Most weapon dials already do; the rest need a **named new `WeaponProfile` field + one resolver term** (a short, concrete backlog) or a **deferred mechanic**.

---

## 1. The data flow (what happens when two fleets fight)

```
BUILD TIME ─ ShipCombatValueDB.Calculate(ship)            [ShipCombatValueDB.cs:161]
  reads the ship's components → produces the ship's combat spec:
    Firepower  (J/s)  = Σ weapon.DamagePerSecond                         [:294]
    Toughness  (J)    = Σ component.HealthPercent × 100 kJ + armour.thickness × 100 kJ   [:170,:291]
    Evasion    (0..0.95) = EvasionCap × sizeFactor × agilityFactor       [:314 CalculateEvasion]
    RoleWeight (1.0 / 0.25 utility)                                       [:298]
    ShieldCapacity_J, ShieldRegen_Jps  (Σ shield generators)             [:276]
    Weapons: List<WeaponProfile>  ← THE per-weapon footprint             [:122]

BATTLE ─ CombatEngagement.StepEngagementGroup(members, dt)   (every 5 s game-time)
  1. BuildFireMix(ships, separation)  → aggregate each fleet's fire BY CLASS (≤~6 buckets),
     each bucket carrying {damage, velocity, tracking, saturation, Nature}; a weapon is
     dropped if Range_m < separation (only when EnableClosingRange is on).
  2. Each fleet takes the COMBINED fire of hostile fleets; an attacker facing N enemies
     divides its fire 1/N (firepower is conserved).
  3. LandedFraction(fireMix, evasion, separation) = damage-weighted avg HitFraction(...)
       HitFraction: velocityTerm = velocity/(velocity + 1e6);
                    trackingEff   = max(velocityTerm, tracking);
                    dodge         = evasion × (1 − trackingEff)  [+ range/flight-time term]
                    landed        = clamp(1 − dodge, saturationFloor, 1)   ← saturation floors it
  4. ApplyShield: the fleet's aggregate shield pool soaks the SOAKABLE part per Nature
       SoakFractionOf: Kinetic 1.0 · Energy 0.5 · Explosive 0.75 · Exotic 0.0   [:106-117]
     drains before toughness, then regenerates.
  5. ApplyCasualties: bucket defenders by (toughnessMult, evasion, toughness, role);
       effective toughness = Toughness × ToughnessMult ÷ LandedFraction;
       ARMOUR soak is applied flat-per-source (ground twin GroundDamageMatrix.ArmourSoak);
       damage × SalvoDamageScale 0.1;  kill WHOLE ships, combatants (RoleWeight) first.
```

**Casualty model (v1):** whole-ship removal, no per-component damage (parked). Ships bucket by combat value → cost is O(buckets), not O(ships).

---

## 2. The resolver's INPUT SURFACE — everything a dial can touch

**This is the whole target.** A dial is authentic iff it writes one of these.

### Per-weapon — `WeaponProfile` (the 7 fields the salvo math reads)
| Field | Type | Read by | What it decides |
|-------|------|---------|-----------------|
| **DamagePerSecond** | J/s | firepower + fire mix | how much hurt |
| **Velocity** | m/s | `HitFraction` velocityTerm | beam (≥10⁷ hitscan) vs dodgeable |
| **Tracking** | 0..1 | `HitFraction` | how well it beats evasion |
| **Saturation** | tracks/s | `HitFraction` floor | floods dodge (flak) |
| **Range_m** | m | `BuildFireMix` gate + range term | reach / accuracy-at-distance |
| **Nature** | Kinetic/Energy/Explosive/Exotic | `SoakFractionOf` | the shield matchup |
| **Delivery** | Beam/Bolt/Slug/Cloud/Guided/Blast | `Class` (computed) | dodge behaviour flavour |

### Per-ship
| Value | Read by |
|-------|---------|
| **Toughness** (J) | casualty math (effective toughness) |
| **Evasion** (0..0.95) | `HitFraction` |
| **RoleWeight** | casualty priority |
| **ShieldCapacity_J / Regen** | `ApplyShield` |
| **doctrine Firepower/Toughness mult** | strength + casualties |

### Global tuning constants (the balance knobs)
`SalvoDamageScale 0.1` · `VelocityReference 1e6` · `SaturationReference 50` · `EvasionCap 0.95` · shield-soak-vs-Nature table · `RangeBaseMiss 0.9` · `FlightTimeReference 10s` · `MinLandedFraction 0.02` · `ComponentHitPoints_J 100k` · `ArmourSoakPerPoint 1.5`.

**That's the entire surface.** Anything a dial wants to do must reduce to writing one of these — or the surface must be *extended* (a new field + a term), which is the backlog in §4.

---

## 3. Dial-insertion map — every weapon dial → where it lands

Legend: **✅ field exists** (dial writes it today) · **➕ new field** (add a `WeaponProfile` field + one resolver term) · **⚙ new mechanic** (deferred; needs a subsystem first).

### Energy / Ballistic / Melee (direct-fire) — shared insertion points
| Dial | Lands on | Status |
|------|----------|--------|
| Output (J/shot) × Rate | **DamagePerSecond** (energy×rate) + **Saturation** (rate) | ✅ |
| Delivery: beam/bolt/scatter | **Velocity** (vs 10⁷) + **Delivery** + **Saturation** (scatter) | ✅ |
| Nature (thermal/ion/kinetic/…) | **Nature** → shield soak | ✅ |
| Tracking / accuracy | **Tracking** | ✅ |
| Range / standoff | **Range_m** (+ range term) | ✅ (bites when closing on) |
| Muzzle velocity (ballistic) | **Velocity** | ✅ |
| Melee = undodgeable, must close | **Velocity=∞-equiv / Delivery** (Matchup ×1) + Range_m≈0 | ✅ (ground matrix) |
| **Focus lance↔wide** | wide → **Saturation** ✅ · lance → **Penetration** | ➕ Penetration |
| **Penetration ↔ Splash** | **Penetration** (armour-pen) vs splash→Saturation | ➕ Penetration |
| **Linked fire / per-shot alpha** | **PerShotEnergy** (so alpha beats flat armour, chip bounces) | ➕ PerShotEnergy |
| **Recoil → accuracy** (ballistic) | reduce effective **Tracking** by recoil÷chassis-mass | ➕ a recoil term |
| **Cooling / heat → sustained rate** | lower effective **DamagePerSecond** under sustained fire | ➕ a heat/rate term |
| **Charge damage profile** | hi **DamagePerSecond** / lo **Saturation** | ✅ |
| Charge **telegraph window** | (no per-shot timing in the aggregate resolver) | ⚙ per-shot timing |
| **Overcharge / burnout** | self-damage on fire | ⚙ self-damage rule |
| **Multi-ammo switch** | swap the active **WeaponProfile** | ⚙ profile-swap |
| **Frequency modulation** | vs adaptive-shield resistance | ⚙ adaptive shields |
| **Medium (atmo/water)** | scale output/range by combat medium | ⚙ environment modifier |
| Efficiency (dmg/watt) | power draw → **Mass** (build), **not** the resolver | — build-side (correct) |
| Thermal bloom / signature | `SensorProfileDB.ActivityMultiplier` (detection) | — detection-side (correct) |
| Mount arc / traverse | positional; the aggregate resolver is non-positional | ⚙ or drop (flavour) |

### Guided (missiles) — extra insertion points
| Dial | Lands on | Status |
|------|----------|--------|
| Warhead output · seeker tracking · range | **DamagePerSecond / Tracking / Range_m** | ✅ (missile is a stub today → wire real values) |
| Salvo size vs PD | **Saturation** vs `SaturationReference 50` | ✅ (proxy) |
| **Ammo / runs-dry mid-battle** | **AmmoPool** drained per salvo (exists ground-side `GroundAmmo`) | ➕ wire the space resolver |
| **Point-defense intercepts a missile** | missiles as **resolvable targets** in the salvo loop, PD-capable flag | ⚙ missiles-as-targets |
| Seeker jamming | vs EW | ⚙ EW door |

---

## 4. The resolver-extension backlog (the concrete build list)

To make the **➕** dials authentic, the resolver needs exactly this — small, additive, each with a gauge. Ordered by payoff:

1. **`Penetration` field + `ArmourSoak` term.** Today armour is a flat soak by target Defense (`GroundDamageMatrix.ArmourSoak`, `ArmourSoakPerPoint 1.5`) with **no per-weapon penetration**. Add `WeaponProfile.Penetration`; `ArmourSoak` reduces its flat soak by penetration. *Unlocks:* lance/sabot/AP/piercing (Energy, Ballistic, Melee) as real armour-crackers, and Splash as the anti-swarm opposite. **Highest payoff — it's the armour half of the matchup.**
2. **`PerShotEnergy` field.** `BuildFireMix` aggregates dps and loses per-shot size, so the "one big alpha punches armour, a swarm of chips bounces" identity (the whole point of flat armour) can't be expressed by a beam vs a repeater of equal dps. Add per-shot energy so `ArmourSoak` sees alpha vs chip. *Unlocks:* Linked-fire, charge-alpha, the swarm-vs-alpha texture.
3. **Mid-battle `AmmoPool` drain.** Ground already has it (`GroundAmmo.MaxAmmo_kg`/`IsDry`); the space stepped resolve doesn't drain ammo, so a long fight never dries out a magazine. Wire the pool into `StepEngagementGroup`. *Unlocks:* Ballistic/Guided magazine depletion, resupply as a real combat pressure.
4. **Recoil → Tracking term.** Effective Tracking −= f(recoil ÷ chassis-mass). *Unlocks:* Ballistic recoil (big gun on small hull = can't aim).
5. **Heat → sustained-rate term.** Effective dps throttles under sustained fire unless cooled. *Unlocks:* Energy cooling, burst-vs-sustained.
6. **Missiles as resolvable targets + PD-capable flag.** Missiles are a firepower stub, not projectiles the salvo loop can shoot down; flak's saturation is only a proxy. Model an in-flight missile pool that PD weapons deplete. *Unlocks:* real point-defense, the salvo-vs-PD saturation duel, drone/fighter interception.

**Deferred mechanics (⚙ — a subsystem each, they gate their dials):** adaptive shields (→ frequency modulation) · combat-environment modifier (→ medium) · per-shot timing (→ charge telegraph) · self-damage rule (→ overcharge) · profile-swap (→ multi-ammo) · the effect bus + capture (→ stun/conversion/Exotic effects) · positional/arc (→ mount traverse, or drop as flavour).

---

## 5. What this proves

- The **fight-deciding core** (output, rate, nature, velocity, tracking, saturation, range, evasion, toughness, shields, doctrine) is **fully wired today** — the resolver already reads it, and §0e calibrated it to real numbers.
- The **depth dials** are not vaporware: each has a **named home** — an existing field, one of **six concrete resolver extensions** (§4), or a deferred subsystem. Nothing is cosmetic; nothing is hand-waved.
- Because the resolver is an **aggregate salvo engine** (non-positional, non-per-shot-timed, whole-ship casualties), a few dials (arc, charge-telegraph) can't be authentic without changing the resolver's *nature* — those are honestly marked ⚙/drop, not pretended into the salvo math.

**The build order that falls out:** ship the ✅ dials with the doors; land the six §4 extensions (Penetration first — it's the armour half of the matchup) so the ➕ dials go live; schedule the ⚙ mechanics as their own gated slices (they're the same list the effect bus + adaptive-shield work already owns). Calibrate each against the §0e joule scale and sanity-check one exchange, exactly as the Weapons doors already do.

---

## 6. Scale & composition — VERIFIED (any number of ships/soldiers, any combination)

Checked against the code, because the guarantee has to be real, not asserted.

**Ships — VERIFIED ✅.** `StepEngagementGroup` fire-mixes by weapon **class** (≤~6 buckets) and `ApplyCasualties` buckets defenders by combat value `(toughnessMult, evasion, toughness, role)` → cost is **O(buckets), not O(ships)**. Multi-party is native — any number of fleets, either side, fire divided 1/N (firepower conserved). Proven: `CombatPerformanceTests` (200 real warships in ms), `CombatBattleSims` B10 (1 dreadnought vs **1000** gnats ~9 ms), `MultiPartyEngagementTests` (assist / join mid-fight / fire-split). **So a dial that writes a `WeaponProfile` field impacts the resolve identically at any N and any composition** — a 1000-ship bucket resolves exactly as the per-ship math; a mixed fleet is just more class-buckets.

**Soldiers — AUTHENTIC MATH, but a PARALLEL resolver (the honest caveat).** `GroundForcesProcessor.ResolveRegionCombat` reads the **same matchup** (`GroundDamageMatrix.Matchup`/`ArmourSoak`, the triangle, cover/fortification, stance) — so a dial that writes the shared matchup DOES impact ground combat authentically. **But** it is a **separate implementation**, and it is **per-unit pairwise — O(units²) per region, NOT bucketed** (`foreach attacker-faction → foreach defender-faction → foreach unit → foreach reachable target`), over ground-specific stat fields on `GroundUnit` (a parallel to `WeaponProfile`). Consequences: (a) huge battalion counts scale **worse** than ships and have **no perf gauge**; (b) **every new dial term must be built TWICE** (ship `WeaponProfile` + ground `GroundUnit`) until the resolvers merge.

**The prerequisite that makes the guarantee uniform — the resolver MERGE (DECIDED 2026-07-06, `Combat/CLAUDE.md`).** Extract the shared salvo/matchup math onto a neutral **COMBATANT** view that both a ship entity and a soldier present, route both through the ONE bucketed resolver, delete the ground duplicate. After the merge: **one bucketed O(buckets) path for ships AND soldiers**, and every dial term is wired **once**.

> **Recommendation:** land the resolver merge **before (or as the first slice of)** the §4 resolver-extensions — so each dial's resolver term is built once, bucketed, and scales for **both** fleets and battalions. Until then, the "any number, any combination" guarantee is **fully true for ships** and **true-but-un-bucketed-and-duplicated for soldiers.**
