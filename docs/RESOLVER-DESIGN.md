# The Auto-Resolver — Anatomy, Dial-Insertion Map, and the Ship↔Ground Merge

**What this is:** the one place that explains how Pulsar's auto-resolve combat engine is actually built — how the numbers flow when two forces fight, every place a designer's "dial" can plug in, and the plan (mostly landed) to make **one** engine run both space battles and planetary battles instead of two duplicate ones.

**Consolidated 2026-07-13 from:** `docs/AUTO-RESOLVER-ANATOMY.md` (the resolver taken apart bit by bit + the dial-insertion map + the extension backlog) and `docs/RESOLVER-MERGE-DESIGN.md` (the one-kernel-for-ships-and-ground plan + the landed slices).

**Read this in two halves.** Part A (the ANATOMY) is the map — the data flow, the small fixed set of inputs the engine reads, and where every weapon and propulsion dial lands on that surface. Part B (the MERGE) is the plan that took the ship engine's math and made a soldier run through the *same* math, so a capability built once works in both space and on a planet.

**Source of truth for live build-state:** `GameEngine/Combat/` (`ShipCombatValueDB.cs`, `CombatEngagement.cs`, `WeaponProfile.cs`, `CombatKernel.cs`) + `Combat/CLAUDE.md`. This doc's status markers are a **snapshot, not the gauge** — for whether any one dial is live, read the source. CI cannot run the client, so nothing here is claimed to be verified at runtime; where a thing is "built and wired," that means it exists and compiles, not that it's been watched working.

Companion docs: `COMPONENT-DESIGNER-DIALS.md` (§0e is the number anchor; the per-category insertion points from Part A are now folded into each category's ⚙ Wiring Dossier there, `⚙ 1`…`⚙ 11`). This doc remains the **cross-category resolver anatomy + the extension backlog** (the origin of those insertion points) and the **merge plan**.

**The finding in one line:** the resolver reads a **small, fixed input surface** — one `WeaponProfile` per weapon (**10 fields as of 2026-07-13** — the original 7 plus `Penetration`, `PerShotEnergy`, `HeatPerSecond`, `WeaponProfile.cs:108,118,126`) + a handful of per-ship values + ~10 global constants. A dial is authentic **only if it lands on that surface.** Most weapon dials already do; the few that don't need a **deferred mechanic** (a subsystem built first).

---

# PART A — THE ANATOMY & DIAL-INSERTION MAP

> **⚠ Status note (2026-07-13):** most of what Part A's §A4 and §A7d once listed as **➕ backlog** has since been **built and wired in code** (Penetration, PerShotEnergy, HeatPerSecond fields; mid-battle ammo drain; heat throttle; point-defense; inertialess evasion; reactionless drive). Those rows are flipped to ✅ below. **This doc no longer carries the authoritative build-state** — read `Combat/CLAUDE.md` and the source. Runtime behaviour is unverified by CI (it can't run the client); these exist and are wired.

## A1. The data flow (what happens when two fleets fight)

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

## A2. The resolver's INPUT SURFACE — everything a dial can touch

**This is the whole target.** A dial is authentic iff it writes one of these.

### Per-weapon — `WeaponProfile` (the fields the salvo math reads)
| Field | Type | Read by | What it decides |
|-------|------|---------|-----------------|
| **DamagePerSecond** | J/s | firepower + fire mix | how much hurt |
| **Velocity** | m/s | `HitFraction` velocityTerm | beam (≥10⁷ hitscan) vs dodgeable |
| **Tracking** | 0..1 | `HitFraction` | how well it beats evasion |
| **Saturation** | tracks/s | `HitFraction` floor | floods dodge (flak) |
| **Range_m** | m | `BuildFireMix` gate + range term | reach / accuracy-at-distance |
| **Nature** | Kinetic/Energy/Explosive/Exotic | `SoakFractionOf` | the shield matchup |
| **Delivery** | Beam/Bolt/Slug/Cloud/Guided/Blast | `Class` (computed) | dodge behaviour flavour |
| **Penetration** ✅ | double | `ArmourSoak` (`WeaponProfile.cs:108`) | cancels flat armour point-for-point (AP/sabot) |
| **PerShotEnergy** ✅ | J | `ArmourSoakBurst` / burst-shot count (`WeaponProfile.cs:118`) | alpha vs chip vs flat armour |
| **HeatPerSecond** ✅ | kJ/s | fleet `HeatPool_kJ` throttle (`WeaponProfile.cs:126`) | sustained-fire heat (inert while base-mod = 0) |

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

**That's the entire surface.** Anything a dial wants to do must reduce to writing one of these — or the surface must be *extended* (a new field + a term), which is the backlog in §A4.

## A3. Dial-insertion map — every weapon dial → where it lands

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
| **Focus lance↔wide** | wide → **Saturation** ✅ · lance → **Penetration** | ✅ Penetration field (`WeaponProfile.cs:108`) |
| **Penetration ↔ Splash** | **Penetration** (armour-pen) vs splash→Saturation | ✅ Penetration field |
| **Linked fire / per-shot alpha** | **PerShotEnergy** (so alpha beats flat armour, chip bounces) | ✅ PerShotEnergy field (`WeaponProfile.cs:118`) |
| **Recoil → accuracy** (ballistic) | reduce effective **Tracking** by recoil÷chassis-mass | ✅ recoil→tracking (`ShipCombatValueDB`, `RecoilTrackingFactor`) |
| **Cooling / heat → sustained rate** | lower effective **DamagePerSecond** under sustained fire | ✅ HeatPool throttle (`CombatEngagement.cs:650-657`; inert while base-mod HeatPerSecond=0) |
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
| **Ammo / runs-dry mid-battle** | **AmmoPool** drained per salvo (`FleetCombatStateDB.AmmoPool_kg`) | ✅ wired (`CombatEngagement.cs:627-635`; inert until a magazine seeds a pool) |
| **Point-defense intercepts a missile** | missile-damage intercepted by fleet PD rating | ✅ `FleetPointDefense` intercept (`CombatEngagement.cs:690`); full missiles-as-resolvable-targets still ⚙ |
| Seeker jamming | vs EW | ⚙ EW door |

## A4. The resolver-extension backlog — MOSTLY BUILT (status pass 2026-07-13)

The six extensions this section once listed as ➕ backlog have **landed in code** as of 2026-07-13. They are wired; most are **inert by default** (the base-mod weapons don't yet dial them, so live combat is byte-identical until a design turns them on) — that's build-state (b) built-but-gated-off/unwired-by-default, not (c) built-live-and-tested. CI can't run the client, so runtime behaviour is unverified. Read `Combat/CLAUDE.md` for the live status of each.

1. **`Penetration` field + `ArmourSoak` term. ✅ BUILT.** `WeaponProfile.Penetration` (`WeaponProfile.cs:108`) cancels the target's flat armour point-for-point; `GroundDamageMatrix.ArmourSoak`'s 3-arg overload forwards to the shared `CombatKernel` (resolver merge slice 3a). *Unlocks:* lance/sabot/AP/piercing as real armour-crackers. (Base-mod weapons default Penetration 0 → byte-identical until a design dials it.)
2. **`PerShotEnergy` field. ✅ BUILT.** `WeaponProfile.PerShotEnergy` (`WeaponProfile.cs:118`) → `CombatKernel.BurstShotCount` splits a source into flat-soaked shots, so an alpha punches armour a chip bounces off. *Unlocks:* Linked-fire, charge-alpha, the swarm-vs-alpha texture. (Default 0 → byte-identical.)
3. **Mid-battle `AmmoPool` drain. ✅ BUILT.** `CombatEngagement.cs:627-635` drains `FleetCombatStateDB.AmmoPool_kg` per salvo and silences dry weapons (W3b). *Unlocks:* Ballistic/Guided magazine depletion. (Inert until a `ShipMagazineAtb` seeds a pool.)
4. **Recoil → Tracking term. ✅ BUILT.** `ShipCombatValueDB.RecoilTrackingFactor(recoil, chassisMass)` cuts a kinetic weapon's built Tracking (`ShipCombatValueDB.cs:358,381`, W4). *Unlocks:* big gun on a small hull can't aim. (Every base-mod weapon Recoil 0 → byte-identical.)
5. **Heat → sustained-rate throttle. ✅ BUILT.** `CombatEngagement.cs:650-657` accumulates each fleet's `HeatPool_kJ` (Σ HeatPerSecond × dt) and throttles the energy guns over the cap (W5b). *Unlocks:* Energy cooling, burst-vs-sustained. (Self-gating: base-mod HeatPerSecond 0 → skipped → byte-identical.)
6. **Point-defense missile intercept. ✅ BUILT (partial).** `CombatEngagement.cs:690` applies `FleetPointDefense` intercept to incoming missile damage (W6b). The **full** "missiles as individually-resolvable in-flight targets" model is still ⚙ — today it's a fleet-PD-rating-vs-missile-damage intercept fraction, not a per-projectile shootdown loop.

**Resolver merge (was a §6 prerequisite) — slices 3a/3c LANDED.** The shared flat-armour + dodge/shield math now lives in `Combat/CombatKernel.cs`; ground combat routes through it via `GroundCombatant.ToWeaponProfile` (`GroundCombat/GroundCombatant.cs:66`, called from `GroundForcesProcessor.cs:340`). So Penetration/PerShotEnergy/shield are built **once** on the kernel and shared ship↔ground, not twice. See Part B and `Combat/CLAUDE.md`.

**Still deferred (⚙ — a subsystem each, they gate their dials):** adaptive shields (→ frequency modulation) · combat-environment modifier (→ medium) · per-shot timing (→ charge telegraph) · self-damage rule (→ overcharge) · profile-swap (→ multi-ammo) · the effect bus + capture (→ stun/conversion/Exotic effects) · positional/arc (→ mount traverse, or drop as flavour) · full missiles-as-resolvable-targets (→ per-projectile PD).

## A5. What the map shows

- The **fight-deciding core** (output, rate, nature, velocity, tracking, saturation, range, evasion, toughness, shields, doctrine) is **wired** — the resolver reads it, and §0e calibrated it to real numbers.
- The **depth dials** each have a **named home** — an existing field, one of the six §A4 resolver extensions (**now built**, mostly inert-by-default), or a deferred subsystem.
- Because the resolver is an **aggregate salvo engine** (non-positional, non-per-shot-timed, whole-ship casualties), a few dials (arc, charge-telegraph) can't be expressed without changing the resolver's *nature* — those are marked ⚙/drop, not folded into the salvo math.

**Where things stand:** the ✅ core dials and the six §A4 extensions are in code (the extensions inert until a design dials them; runtime unverified — CI can't run the client). The remaining ⚙ mechanics are the gated slices left. For the authoritative live status of any one dial, read `Combat/CLAUDE.md` and the source — not this doc.

## A6. Scale & composition — VERIFIED (any number of ships/soldiers, any combination)

Checked against the code, because the guarantee has to be real, not asserted.

**Ships — VERIFIED ✅.** `StepEngagementGroup` fire-mixes by weapon **class** (≤~6 buckets) and `ApplyCasualties` buckets defenders by combat value `(toughnessMult, evasion, toughness, role)` → cost is **O(buckets), not O(ships)**. Multi-party is native — any number of fleets, either side, fire divided 1/N (firepower conserved). Proven: `CombatPerformanceTests` (200 real warships in ms), `CombatBattleSims` B10 (1 dreadnought vs **1000** gnats ~9 ms), `MultiPartyEngagementTests` (assist / join mid-fight / fire-split). **So a dial that writes a `WeaponProfile` field impacts the resolve identically at any N and any composition** — a 1000-ship bucket resolves exactly as the per-ship math; a mixed fleet is just more class-buckets.

**Soldiers — AUTHENTIC MATH, but historically a PARALLEL resolver (the honest caveat, now partly closed by the merge).** `GroundForcesProcessor.ResolveRegionCombat` reads the **same matchup** (`GroundDamageMatrix.Matchup`/`ArmourSoak`, the triangle, cover/fortification, stance) — so a dial that writes the shared matchup DOES impact ground combat authentically. **But** it was a **separate implementation**, and it is still **per-unit pairwise — O(units²) per region, NOT bucketed** (`foreach attacker-faction → foreach defender-faction → foreach unit → foreach reachable target`), over ground-specific stat fields on `GroundUnit` (a parallel to `WeaponProfile`). Consequences: (a) huge battalion counts scale **worse** than ships and have **no perf gauge**; (b) each new dial term historically had to be built TWICE (ship `WeaponProfile` + ground `GroundUnit`) until the resolvers merged.

**The prerequisite that makes the guarantee uniform — the resolver MERGE (DECIDED 2026-07-06, `Combat/CLAUDE.md`; slices 3a/3c LANDED 2026-07-08).** Extract the shared salvo/matchup math onto a neutral **COMBATANT** view that both a ship entity and a soldier present, route both through the ONE resolver, delete the ground duplicate. **Status (2026-07-13):** the shared flat-armour + dodge/shield math is now in `Combat/CombatKernel.cs` and ground routes through it (`GroundCombatant.ToWeaponProfile` → `GroundForcesProcessor.cs:340`) — so Penetration/PerShotEnergy/shield are built **once**, ship↔ground. **What's NOT yet merged:** ground is still a **separate, per-unit O(units²) loop** (not the ship bucketing) — the math is shared, the *bucketing* isn't. So a dial term written on the kernel is wired once, but ground large-battle perf still lags ships. After full merge: **one bucketed O(buckets) path for ships AND soldiers**. The full merge plan and its landed slices are Part B below.

> **Status (2026-07-13):** the merge's *shared-math* slices landed alongside the §A4 extensions, so each dial's kernel term is built once, ship↔ground. The *bucketing* half is still outstanding — so the "any number, any combination" guarantee is **fully true (bucketed) for ships** and **true-but-un-bucketed for soldiers** (ground shares the math but not the O(buckets) scaling). Part B §B5c is the build plan that closes the bucketing half.

## A7. Propulsion dial-insertion map — the MOVEMENT/CLOSING surface (Propulsion category, §2)

The weapon map above (§A2–§A4) is the **salvo-damage** surface. Propulsion lands on a **second, adjacent surface** the resolver already reads: **Evasion** (per-ship, feeds `HitFraction`) + the **closing-fight state** (`FleetCombatStateDB.Separation_m` / `ManeuverBudget`, and `FleetCombat`'s fleet-aggregation reads). The headline finding is the opposite of weapons: **the locked Propulsion doors need ZERO new resolver fields — the engine already runs Newtonian physics, and every dial writes a stat the resolver reads today.** The two exotic extensions this once called ➕ (inertialess evasion, reactionless drive) are now **built** (§A7d); three exotic/fluid dials still defer on a named mechanic (⚙).

### A7a. The propulsion input surface (what a propulsion dial can touch)
| Stat | Where it lives | Read by | Source dial |
|------|----------------|---------|-------------|
| **Evasion** (0..0.95) | per-ship `ShipCombatValueDB.Evasion` | `HitFraction` (dodge) | Reaction thrust → `accel = Thrust ÷ MassDry` → `CalculateEvasion` (`ShipCombatValueDB.cs:314`) |
| **DeltaV** (m/s) | per-ship `NewtonThrustAbilityDB.DeltaV` | `FleetCombat.DeltaVFloor` (min over fleet, `:60`) → seeds `ManeuverBudget` (`CombatEngagement.cs:449,524`) | Reaction exhaust-velocity/Isp (Tsiolkovsky) |
| **ManeuverBudget** (Δv reserve) | `FleetCombatStateDB.ManeuverBudget` (`:45`) | `AdvanceClosing` — only a fleet with budget controls the range; spends `ManeuverBurnRate × dt` | Δv (above) → the kiting clock |
| **Controller (who dictates range)** | `FleetManeuver(ships)` (`CombatEngagement.cs:889`) | `AdvanceClosing` (`:847`) — highest maneuver = min evasion picks the closing direction | Reaction thrust/accel (via Evasion) |
| **Separation_m** (the gap) | `FleetCombatStateDB.Separation_m` (`:53`) | `BuildFireMix` weapon-range gate + `HitFraction` range term | closing rate (below) × drives |
| **Closing rate** | `ClosingSpeedScale_mps × dt` (`CombatEngagement.cs:353`) | `AdvanceClosing` moves the gap toward the controller's preferred range | Reaction accel (fast brawler forces merge / fast kiter opens) |
| **WarpSpeedFloor** (m/s) | `FleetCombat.WarpSpeedFloor` (min over fleet, `:44`) | strategic-map transit + fleet-moves-as-one | Warp `WarpAbilityDB.MaxSpeed` |
| **Ground SpeedMult** | `GroundMobility.SpeedMultForUnit` | the ground hex-march (`OrderMove`) + H3 closing | Traction `GroundLocomotionAtb.SpeedFactor` |

### A7b. Calibration anchors (the movement/closing constants — already tuned, live)
| Constant | Value | Role |
|----------|-------|------|
| `AgilityReference_mps2` | 5.0 | accel at which agility half-contributes to Evasion (thrust ÷ mass) |
| `EvasionCap` | 0.95 | hard ceiling on Evasion — nothing untouchable |
| `SizeReference_m3` | 1,000 | volume at which size half-contributes to Evasion (big = easy to hit) |
| `ClosingSpeedScale_mps` | 1e6 | how fast the gap closes per step (live-tuned 2026-06-27) |
| `InitialSeparationDefault_m` | 1e6 (missile range) | where a fallback-seeded fight opens |
| `ManeuverBurnRate` | 5.0 | Δv the controller spends per step to hold the range — the kiting clock's drain |

These are the propulsion equivalents of §0e's joule anchors: a Reaction dial expresses itself in **m/s² of accel** (vs `AgilityReference 5.0`), **m/s of Δv** (→ `ManeuverBudget`), and **m/s of warp speed** — the scale the closing resolver already reads.

### A7c. Dial-insertion map — every LOCKED propulsion dial → where it lands
Legend as §A3: **✅ field exists** · **➕ new term** · **⚙ deferred mechanic**.

| Door | Dial | Lands on | Status |
|------|------|----------|--------|
| **Reaction** | Thrust class | **Evasion** (`accel = Thrust÷MassDry` → `CalculateEvasion`) + **FleetManeuver** (who dictates range) | ✅ |
| Reaction | Exhaust velocity / Isp | **DeltaV** (Tsiolkovsky) → **DeltaVFloor** → **ManeuverBudget** (kiting clock) | ✅ |
| Reaction | Fuel type | fuel economy / `FillFuelTanks` — **build/logistics-side**, not the salvo (correct) | — (economy-side) |
| Reaction | Drive mass (emergent) | **MassDry** → feeds back into Evasion + chassis budget | ✅ |
| **Traction** | Drive type / SpeedFactor | **GroundMobility.SpeedMultForUnit** → hex-march + H3 closing | ✅ |
| Traction | Terrain handling / Amphibious | `HexPathfinder` terrain-cost + water passability | ✅ |
| Traction | Terrain **combat** bonus | a matchup term for fighting on your preferred terrain | ➕ **H3 hex-terrain-in-combat** |
| **Fluid** | Medium access / cross-water | rides `Amphibious` + passability (the *access* half) | ✅ (access) |
| Fluid | Vacuum/medium constraint | a "needs atmosphere/water" tag gates where the drive works | ➕ medium tag |
| Fluid | Altitude/depth band (the *combat* payoff) | air-superiority / sub-stealth / over-under fire | ⚙ **air/altitude/depth combat layer** |
| **Warp** | MaxSpeed | **WarpSpeedFloor** (strategic transit; fleet moves as one) | ✅ |
| Warp | Bubble power (create/sustain) | stored-electricity gate (`ChargeReactors`) — **movement-side**, not the salvo | ✅ (movement) |
| Warp | Jump-drive | jump-point network (`JumpOrder`, `InterSystemJumpProcessor`) | ✅ |
| Warp | Gate-user / network node | which gate reaches which (Stargate) | ⚙ **H8 gate-network/addressing** |
| **Exotic** | Reactionless thrust | Reaction drive with **FuelBurnRate=0** (Δv only bounded by fuel; direct `ThrustInNewtons`) | ✅ `ReactionlessThrustAtb` (`NewtonMove/ReactionlessThrustAtb.cs`; base-mod Nomad; gauge `ShipReactionlessDriveTests`) |
| Exotic | Inertialess maneuver | **evasion-override term** bypassing `accel = Thrust÷MassDry` in `CalculateEvasion` | ✅ `InertialessDriveAtb` → `InertialessEvasionFloor` (`ShipCombatValueDB.cs:575,584`; gauge `ShipInertialessDriveTests`) |
| Exotic | Gravitic / medium-independent | works in any medium, no fuel | ⚙ medium layer (shared w/ Fluid) |
| Exotic | Teleport / rings (H1) | instant point-to-point matter move | ⚙ **Transfer ▸ teleport (H1)** |

### A7d. The propulsion resolver-extension backlog — TWO OF THREE BUILT (status pass 2026-07-13)
Because the movement surface is already wired, the propulsion doors added only three small terms — two now in code:
1. **Evasion-override term** (Exotic ▸ inertialess) — ✅ **BUILT.** `InertialessDriveAtb.EvasionOverride` → `ShipCombatValueDB.InertialessEvasionFloor` reads it as a health-scaled floor in `CalculateEvasion` (`ShipCombatValueDB.cs:575,584`), decoupling evasion from `accel = Thrust÷MassDry`. Base-mod `inertialess-drive` on the Phantom Inertialess Cruiser; gauge `ShipInertialessDriveTests`. (0 = no drive → byte-identical.) *Unlocks:* a capital that dodges like a fighter.
2. **Reactionless no-fuel flag** (Exotic) — ✅ **BUILT.** `ReactionlessThrustAtb` sets `NewtonThrustAbilityDB.ThrustInNewtons` directly + marks `Reactionless` true (unlimited Δv → `ManeuverBudget` never depletes) (`NewtonMove/ReactionlessThrustAtb.cs`, `NewtonThrustAbilityDB.cs`). Base-mod `reactionless-drive` on the Nomad; gauge `ShipReactionlessDriveTests`. **v1 = the combat/closing payoff + strategic Δv readout; the in-space burn model without consuming fuel is a flagged follow-up.** (default false → byte-identical.)
3. **Terrain-combat term** (Traction) — ⚙ still open. The H3 hex-terrain-in-combat follow-on already on the ground roadmap; a drive's preferred terrain gives a matchup edge, not just a movement one.

**Deferred mechanics (⚙ — each gates its dials):** the **air/altitude/depth combat layer** (Fluid's deep half + Exotic-gravitic) · the **H8 gate-network/addressing** (Warp-gate/Stargate) · the **Transfer ▸ teleport mode** (Exotic-teleport/H1). These are the same two prerequisite mechanics the §2 Propulsion category footer names — designed-in, not bolted-on.

### A7e. What the propulsion map shows
- The **closing/evasion core** (thrust→evasion→dodge, Δv→kiting clock→who-dictates-range, warp speed→transit, ground speed→the march) is **wired and gauged** — `ShipEvasionTests` (thrust/mass → evasion), `ClosingTests` (who-dictates + the P2 kiting clock), `FleetAggregationTests` (the fleet floors), `GroundForcesTests` (the hex-march). (These are engine gauges; client runtime is unverified — CI can't run it.)
- Propulsion needed **zero new resolver fields for the four Reaction/Traction/Fluid-access/Warp cores**; of the three small §A7d extensions, **two (inertialess evasion, reactionless drive) are now built**, one (terrain-combat term) is still open, plus the same two deferred mechanics the category flagged. Calibration is inherited (the closing constants were live-tuned 2026-06-27), so a Reaction dial sanity-checks as the doors show (ev 0.48 sprinter vs 0.02 cruiser, §2.1).

---

# PART B — THE RESOLVER MERGE (one combatant kernel for space AND planetary combat)

**As of:** 2026-07-08 · the DECIDED 2026-07-06 merge (`Combat/CLAUDE.md`), designed before code. Goal: one bucketed salvo kernel both a **ship entity** and a **planetary unit** run through; delete the duplicate ground resolver (`GroundForcesProcessor.ResolveRegionCombat`). Blocking gate — *nothing else moves until space AND planetary combat both resolve end-to-end on the shared kernel* (developer, 2026-07-08).

## B1. The honest reality (why it's a reconciliation, not a move)

The ship resolver (`CombatEngagement`) and the ground resolver (`GroundForcesProcessor.ResolveRegionCombat`) implement the **same concepts differently**:

| Concept | Ship resolver | Ground resolver |
|---------|---------------|-----------------|
| Weapon | real `WeaponProfile` (Nature×Delivery, velocity, tracking, saturation, range_m) | `GroundWeaponMode` triangle (Attack, DamageType, hex Range) |
| Dodge | `HitFraction(weapon, evasion, sep)` → effective toughness | `GroundDamageMatrix.Matchup` (IsAimed × (1−evasion)) |
| Shield | depleting POOL (`FleetCombatStateDB.ShieldPool_J`, `SoakFractionOf` by nature) | innate % stat (`Shield/(Shield+150)`, weaker vs energy) |
| Armour | folded into Toughness (component HP + thickness, joules) | flat-per-source `ArmourSoak(defense, dmg)` |
| Casualty | WHOLE ship, bucketed by combat value (O(buckets)) | continuous `unit.Health` drain (per-unit) |
| Range | metric separation between fleets (closing model) | hex distance ≤ `unit.Range` (hexes) |

**The direction (developer's call): planetary ADOPTS the ship kernel.** The ground triangle "dissolves into weapon×armour matchups"; planetary units come to carry real `WeaponProfile`s (which the universal weapon designer already produces), run the same `HitFraction`/shield/armour math, and differ ONLY in **range/movement on the hex board** (+ terrain, + the air/altitude layer later). The **ship side stays byte-identical** (it already *is* the kernel).

## B2. The neutral COMBATANT view (the seam)

A struct/interface both a ship entity and a `GroundUnit` present to the kernel — nothing entity- or hex-specific leaks in:

```
struct Combatant {
  int    FactionId;
  double Health;        // current hit-points, joules-scale (ship Toughness | unit Health×k)
  double MaxHealth;
  double Evasion;       // 0..cap
  double ShieldPool;    // depleting shield (0 if none); ground unit seeds from its shield stat×capacity
  double ShieldRegen;
  List<WeaponProfile> Weapons;   // what it fires — SAME type both domains
  double Position_m;    // 1-D range coordinate: fleet separation (space) | hexDist×HexPitchKm×1000 (planet)
  // read-model back-refs the caller uses to apply results (ship entity id | GroundUnit ref)
}
```

The kernel never sees a hex or an `Entity` — only `Position_m` (metric) and `Weapons`. **"A 100 km weapon is 100 km on a surface too; hexes are just the board"** → planetary maps hex distance to metres via `GroundRangeTools.HexPitchKm(region)`.

## B3. The shared KERNEL (the pure salvo math)

`CombatKernel.ResolveSalvo(attackers, defenders, dt, ctx)` — the extracted ship math, made domain-neutral:

1. **Fire mix** — aggregate attackers' `Weapons` by class bucket (`BuildFireMix`), gated on `Range_m ≥ |Δposition|`.
2. **Landed fraction** — `LandedFraction(mix, defender.Evasion, separation)` = damage-weighted `HitFraction` (velocity/tracking/saturation/range term). *(the existing ship dodge curve, verbatim.)*
3. **Shield** — `ResolveShield` drains the defender's `ShieldPool` by the soakable fraction (`SoakFractionOf` by Nature); regen toward capacity.
4. **Armour** — `ArmourSoak(defender.armour, perSourceDamage)` flat-per-source *(the existing `GroundDamageMatrix.ArmourSoak`, promoted to the shared kernel — the space side gains real armour-per-source instead of only folding it into toughness; verify byte-identity or gate behind the closing flag).*
5. **Apply** — drain `defender.Health`; caller removes it (ship: whole-ship bucket; unit: Health≤0). `× SalvoDamageScale`.
6. **Bucketing** — defenders keyed by `(evasion, health, shield, armour, doctrineMult)` → O(buckets) for **both** ships AND planetary units *(this is what fixes the ground O(units²) loop — §A6.)*

The kernel is **pure** (no entity mutation, no RNG) — deterministic (fast-forward == watch, the locked rule).

## B4. Planetary specifics (what stays different — range & movement on the hex board)

Everything above is shared. Planetary combat adds, in its CALLER (a slimmed `ResolveRegionCombat`), only:

- **Range in metres from hexes:** a unit's weapon reaches `Range_m`; its target's `Position_m` = `hexDistance × HexPitchKm(region) × 1000`. So the SAME weapon-range gate as space, on the hex board.
- **Movement changes range AS THE FIGHT PROGRESSES** (the developer's core point): each tick, units advance/kite on the hex grid by **move speed** (the existing `GroundMobility` + hex pathing/ROE — `ApplyEngagementManeuvers` already steps units toward/away by one hex), so `Position_m` shifts and who-can-hit-whom evolves salvo to salvo. This is the ground twin of the space **closing model** — same idea (range decided by speed × firepower), different board.
- **Terrain × fortification × stance** — kept as multipliers on the kernel's inputs (cover/fort divide incoming; stance scales attack/damage-taken) exactly as today.
- **The type-triangle DISSOLVES** — Armor▸Infantry▸Artillery becomes emergent from weapon Nature × target armour/evasion (a tank = heavy armour + kinetic gun; artillery = long range + splash), so `AvgTriangleVs` retires.

## B5. Slice sequence (each additive, CI-GREEN before the next)

1. **Kernel skeleton (additive, byte-identical). ✅ LANDED 2026-07-08.** New `Combat/CombatKernel.cs`: the neutral `Combatant` view + the pure math (`HitFraction`, `SoakFractionOf`, `ResolveShield`, `ArmourSoak`, `LandedFraction`, `ShieldSoakFraction`) written byte-for-byte from the live ship math (`CombatEngagement`) + ground math (`GroundDamageMatrix`). **Realized as PURELY ADDITIVE, not "moved + shim":** the kernel is a new home for the arithmetic and is *unwired* this slice — nothing calls it yet, so ship + ground behaviour is byte-identical because nothing existing was touched. The duplication is deliberate and lives for exactly one slice; **`CombatKernelTests`** both PINS the kernel outputs to hand-computed values AND CROSS-CHECKS them against the live `CombatEngagement`/`GroundDamageMatrix` functions over an input sweep, so any drift between the two copies goes red. Slice 2 routes the ship resolver through the kernel and DELETES the ship copies (the ship fixtures become the byte-identity tripwire), which is where the "move + remove duplication" actually happens. **Gauge:** every existing combat + ground fixture stays green (nothing rewired) + `CombatKernelTests` green.
2. **Ship routes through the kernel. ✅ LANDED 2026-07-08.** Realized as **delegation, not a monolithic `ResolveSalvo`**: the pure dodge/shield ARITHMETIC (`HitFraction`, `LandedFraction`, `SoakFractionOf`, `ResolveShield`, `ShieldSoakFraction`) now lives ONLY in `CombatKernel`; `CombatEngagement`'s same-named helpers became thin `=> CombatKernel.X(...)` delegators, its tuning constants (`VelocityReference_mps`, `SaturationReference`, `MinLandedFraction`, `FlightTimeReference_s`, the four `ShieldSoakVs*`) forward to the kernel's, and `RangeBaseMiss` is a forwarding property — so there is a single source of truth for the ship path and no drift is possible. The fleet/entity ORCHESTRATION (multi-fleet fire-division, per-fleet `DamageTakenPool`, the casualty bucketing in `ApplyCasualties`, closing, retreat, narration) stays in `CombatEngagement` — that's the ship-specific layer the planetary side will meet at the kernel in slice 3. **Why delegation over a `ResolveSalvo` rewrite:** delegation to identical code is provably byte-identical (zero number moves), whereas folding `ApplyCasualties`' bucketing + pool carry-over into a neutral entry point is the genuinely new interface that belongs in slice 3, where BOTH domains adopt it at once. **Gauge:** all ship fixtures stay green (`CombatPerformance`, `Dodge`, `Shield`, `Triangle`, `Stress`, `BattleSims`, `Multiparty`, weapon-triangle) — the byte-identity tripwire.
3. **Planetary side adopts the kernel — split into 3a/3b/3c (the ledger, /scratchpad slice3-ledger, said only ONE piece is truly byte-identical today):**
   - **3a — flat armour (byte-identical). ✅ LANDED 2026-07-08.** `GroundDamageMatrix.ArmourSoak` + its two constants (`ArmourSoakPerPoint`, `ArmourMinPassFraction`) now delegate/forward to `CombatKernel` — the last piece of math that was already identical on both sides. Zero behaviour change; `GroundDamageMatrixTests` / `GroundForcesTests` / `GroundBombardmentTests` stay green. Collapses the ship↔ground armour duplication.
   - **3b-i — the BRIDGE (additive, byte-identical). ✅ LANDED 2026-07-08.** `GroundCombat/GroundCombatant.cs` maps a `GroundUnit` → `CombatKernel.Combatant`: its Attack/`GroundWeaponMode`/hex-Range become a real `WeaponProfile` whose velocity/tracking/saturation are chosen so the kernel REPRODUCES the ground dodge/shield semantics — ballistic ≈(1−evasion) dodgeable, artillery/melee undodgeable, energy dodgeable-but-shield-bleeds, kinetic fully-soaked. **Unwired** (ResolveRegionCombat doesn't call it yet → live combat byte-identical). `GroundKernelBridgeTests` PROVES the triangle/dodge/shield fall out of the kernel with sensible specs — the verification that de-risks the swap.
   - **3b-ii — the swap: TRIANGLE DISSOLVES. ✅ LANDED 2026-07-08 (developer chose "option 1: type edge lives in stats").** `ResolveRegionCombat` no longer multiplies the attack pool by `AvgTriangleVs` — the Armor▸Infantry▸Artillery type edge is no longer a flat ×1.5/×0.67 and now emerges from raw stats (attack/armour/HP) × weapon-nature vs the target's evasion/shield/armour, the ship way. `AvgTriangleVs` retired; `GroundTerrain.TriangleMult` kept as a readout/helper. **Zero test re-baseline needed** — every combat-outcome fixture is same-type (triangle was 1.0), and the triangle test checks only the `TriangleMult` helper; the cross-type numbers are gauged by `GroundResolverComparisonReadout` (CI-certified before/after: Armor→Inf 258→167, Inf→Armor 44.5→77.5, same-type unchanged). Dodge/shield stay on the existing `GroundDamageMatrix.Matchup` this slice (the 3b-i bridge proved the kernel reproduces them; routing dodge fully through `CombatKernel.HitFraction` lands in **slice 4** when its RANGE term goes live — doing it now, at separation 0, would be churn with no behaviour change). The innate-%→depleting-**pool** shield conversion is re-scoped to its own later slice (**3c**), since a faithful per-unit pool needs a persistent `CurrentShield` field + a nature-mix soak — a real change, not to be rushed under this one.
   - **3c — shield → depleting POOL + dodge on the kernel. ✅ LANDED 2026-07-08.** `GroundUnit` gains a persistent `CurrentShield` pool (seeded = `Shield` at raise, save-safe, deep-copied). `ResolveRegionCombat` now builds each attacker's `WeaponProfile` (`GroundCombatant.ToWeaponProfile`) and resolves **dodge via `CombatKernel.HitFraction`** and **shield via a depleting pool** (drained by the nature soak-fraction `CombatKernel.ShieldSoakFraction` before armour, regenerating toward capacity between salvos at `ShieldRegenPerHourFraction`) — so a ground shield is burst-resistant then brittle (the ship model), and ground per-source damage is now **fully on the shared kernel**. `GroundDamageMatrix.Matchup` is retired to readout/bombardment-only (kept for `GroundDamageMatrixTests` + the comparison readout + orbital bombardment). **Byte-identical for unshielded/0-evasion units** (every existing combat fixture), so zero re-baseline; the pool + nature matchup are gauged by `GroundForcesTests.GroundShield_IsADepletingPool_EnergyBleedsWhereKineticIsSoaked`. *(v1 flag: a unit regenerates its shield only while in a contested region; and orbital bombardment still reads the innate `Shield` %, not the pool — unifying that is a follow-up.)* **With 3c the ground damage model IS the kernel — the merge's damage half is complete for both domains.**
4. **The CLOSING model on the hex board — the north star (§B7). LARGELY ALREADY BUILT (investigation 2026-07-08).** The Prime-Directive read found the ground closing fight is substantially wired: units carry hex positions; `ResolveRegionCombat` gates fire by hex range (`HexDist ≤ Range` — a longer-ranged unit hits a closer one WITHOUT being hit back, the first-strike); `ApplyEngagementManeuvers` auto-closes (CloseToEngage) / auto-kites (StandOff) one hex/tick under each formation's `GroundEngagementStance`; `GroundMobility` makes a faster unit close sooner; a hex-marching unit still fights. So the zergling-rush / Titan-kite behaviour EXISTS — the gap was that no test ran the FULL multi-tick fight to prove the range advantage decides it. **4a ✅ LANDED 2026-07-08:** `GroundForcesTests.ClosingFight_LongRangeWhittlesTheRusherDuringTheApproach` — an equal-stats long-range unit vs a rushing short-range one, 3 hexes apart, played out over 60 game-hours; the rusher ends more damaged (or dead) because the kiter fired across the whole approach. The north-star fight is proven end-to-end. **4b ✅ LANDED 2026-07-08 — the ground combat interrupt.** `GroundForcesProcessor` now halts the clock (`MasterTimePulse.RequestCombatHalt`) the first tick a NEW planetary battle forms — the ground mirror of the space combat-pause, sharing the client's `CombatInterruptPending` banner. `ResolveRegionCombat` returns whether a real exchange happened; a runtime `GroundForcesDB.WasInBattle` latch makes it a not-fighting → fighting TRANSITION (an ongoing fight doesn't re-halt). Gated by `GroundForcesProcessor.InterruptTimeOnNewBattle` (default off; client turns it on next to the space combat flags). Gauge: `GroundForcesTests.GroundCombatInterrupt_HaltsTheClockOnANewBattle_NotEveryTick`. So a planetary battle no longer resolves invisibly on fast-forward — the developer's "give me notice of combat" need, now met for ground too. *(v1 flags: coarse to the hourly processor tick, not the space 5 s fine-step — a planetary battle plays over hours so that's adequate; halts on ANY new battle, not only the player's, mirroring the space v1 stub.)* **Slice 4 COMPLETE: the closing fight plays out (4a) AND you're notified when it starts (4b).**
5. **Battalion▸Formation fleet-parity + sub-formation ranges + delete the duplicate.** Investigation found much already built: formation NESTING exists (`GroundFormation.ParentFormationId` / `SetParentFormation` / `ChildFormations` / `OrderFormationTreeMoveToHex` = the Battalion▸Formation tree), per-formation doctrine + ROE close/kite exist, and the closing fight is proven (slice 4). Remaining, as sub-slices:
   - **5a — formation AGGREGATION (fleet-parity reads + cohesive march). ✅ LANDED 2026-07-08.** `GroundFormationTools.FormationSpeedMult` (MIN speed — "moves at the slowest"), `FormationReachHexes` (MAX range — "sees/strikes as far as the longest"), `FormationStrength` (Σ attack — "hits with the sum"), `FormationHealth` — the ground twin of `Combat.FleetCombat`'s `WarpSpeedFloor`/`SensorReach`/summed-Firepower. And the **cohesive march**: `OrderFormationMove`/`OrderFormationMoveToHex` now time every member on the shared SLOWEST pace (`OrderMove`/`OrderMoveToHex` gained a `speedMultOverride`), so a joined block arrives together instead of the fast units outrunning the slow. Gauge: `GroundForcesTests.FormationAggregation_SlowestSpeed_LongestReach_SummedStrength_AndCohesiveMarch`. *(v1: aggregates a formation's DIRECT members; tree roll-up over sub-formations is a follow-up.)*
   - **5b (remaining) — vocabulary + the client battalion sheet:** rename the UI to Fleet▸Group / Battalion▸Formation and surface the 5a aggregation as a battalion readout (the ground Fleet-Combat-tab); a UI-heavy, runtime-verify pass best done with the developer's local build.
   - **5c (remaining) — bucket the ground resolver:** turn the O(units²) pairwise loop into O(buckets) so 10,000 units cost like a handful (the "compute one representative × distribute across N"), with a large-battalion perf gauge. Coupled to the position model (units carry per-hex positions), so a real design pass. Full build plan in §B7.2.
   - The triangle is already deleted (3b-ii); the shared-kernel damage is done (3c).

## B6. Risks / invariants

- **Determinism** (fast-forward == watch) — the kernel is pure arithmetic; keep it RNG-free.
- **Byte-identity for ships** — slices 1–2 must not move a single ship-combat number; the ship fixtures are the tripwire.
- **The ground behaviour change is deliberate** (triangle→matchup) — slice 3b re-baselines `GroundForcesTests` with the reason written down; it is the one place outcomes legitimately shift. Slice 3a (armour) is byte-identical.
- **CI-blind** — one slice per push, both jobs green before the next; no stacking.
- **Bucketing both sides** is the payoff — it's what makes "any number of soldiers, any battalion combination" O(buckets), closing the §A6 caveat.

## B7. THE NORTH STAR — the closing fight, one model, both domains (developer, 2026-07-08)

The definitive statement of what this merge is FOR. Quoted essence: *"ground units work the same as ships as far as fleets or battalions are concerned."* One shape, both domains:

**Grouping = fleets, exactly.** A **formation/battalion IS a fleet**. You can raise 50–100 zerglings that hold a formation, an armor column with a Titan in another, each its own battalion (fleet-equivalent). **Join them → the joined force moves at the pace of its SLOWEST unit but sees as far as its LONGEST sensor** — byte-for-byte the fleet rule (`FleetCombat.WarpSpeedFloor`/`DeltaVFloor` = min, `SensorReach` = max). They march **hex-by-hex across regions as one unit**.

**Trigger = weapon range of the longest-ranged unit.** The Titan sees 5 hexes but shoots 3. Combat starts when an enemy comes within **3** hexes — *because of the Titan* — and the auto-resolver takes over. This is the space rule already (`WithinWeaponRange` opens the fight at `Max(reachA, reachB)`; detection range ≫ weapon range).

**The fight = the auto-resolver simulates the closing, tick by tick, over the battle's duration.** With a doctrine (Titan kites / armor column screens the Titan / zerglings advance — as **sub-formations**), each sub-formation moves at its **true speed** under its stance: the zerglings sprint at the enemy, and *the resolver accounts for the 3-hex distance* — as battle-time passes they close based on their move speed and who's left, **only landing damage once they reach their range**, and the resolver keeps computing damage/losses continuously until it's decided. **You keep what survives.** This is the space **closing model** (`FleetCombatStateDB.Separation_m` + `AdvanceClosing` + the `BuildFireMix` range gate + the `HitFraction` range term) — on the hex board, with the gap in hexes×HexPitch = metres.

> *"This IS how space combat should be and how planetary combat MUST be. This is the north star of combat and what you are building towards this whole time."*

**Why the kernel merge is the enabler:** the closing model + the damage math must be **one** implementation, or the two domains drift. Slices 1–2 put the ship damage math in `CombatKernel`; 3a puts armour there; 3b puts the ground damage on it; slice 4 brings the closing model to ground; slice 5 gives formations fleet-parity aggregation + sub-formation ranges + buckets the ground side. The zergling/Titan example is the acceptance test for "done."

**Bucketing resolves the scale (corrected 2026-07-08):** 100 interchangeable zerglings collapse to **one** combat-value bucket; the unique Titan is a **bucket of one** — exactly the ship model (100 fighters + 1 dreadnought). Per-unit identity lives at the **sub-formation** level (swarm / column / Titan), not the individual, so bucketing loses nothing the player cares about.

### B7.1 The vocabulary + the "compute one, distribute across N" model (developer, 2026-07-08)

**Locked naming — the SAME two-level shape, one label set per domain:**

| Level | Space | Planetary |
|-------|-------|-----------|
| The whole force (the fleet-equivalent you select + order) | **Fleet** | **Battalion** |
| A sub-group inside it with its OWN doctrine | **Group** (was "sub-fleet") | **Formation** |
| The individuals | units (ships) | units (space marines, clone troopers…) |

So: **Fleet ▸ Groups ▸ ships** and **Battalion ▸ Formations ▸ soldiers**. Selecting a Fleet/Battalion and opening its info shows a **Group/Formation breakdown, split by units** (e.g. space marines and clone troopers in the same Battalion but different Formations). *(Naming reconciliation for slice 5: today's `GroundFormation` class sits at the **fleet/Battalion** level — "the ground echo of a fleet." Under this vocabulary a **Battalion** is that top grouping and a **Formation** is the sub-group. Slice 5 either renames or nests so the ground hierarchy reads Battalion ▸ Formation ▸ units. Pure orchestration above the kernel — no kernel change.)*

**Doctrine lives on the Group/Formation, not the whole force.** Each Group/Formation carries its own doctrine that dictates its behaviour in the fight — hang back, retreat out of range, rush the enemy, kite, hold, etc. (space: `FleetDoctrineDB` per Group — already the per-component doctrine model; ground: `GroundFormationDoctrine` + `GroundEngagementStance` per Formation — already built). The closing model (slice 4) reads each Group/Formation's doctrine to decide close/kite/hold, so the Titan's Group kites while the zergling Formation rushes, in one battle.

**The math the developer wants — "do the math for ONE unit + its doctrine, then distribute across N":** the resolver computes the outcome for a **single representative** — one unit, folding in its Group/Formation's doctrine, the opposing side, environmental effects, unit statuses, and any other modifiers — and then **distributes that result across the 50 / 100 / 10,000 identical units** in that bucket. This IS the bucketing model, stated as a design intent: the expensive per-salvo math (`CombatKernel.HitFraction`/shield/armour) runs **once per (representative × doctrine × situation) bucket**, and the caller scales it by the unit count. It's why "any number of soldiers, any battalion combination" stays O(buckets), not O(units) — and why the kernel is a set of **pure per-combatant functions** (compute-one) with the count/distribution handled by the orchestration (distribute-across-N). The refinement CONFIRMS the slice 1–3 kernel shape; nothing changes below the orchestration.

### B7.2 Slice 5c BUILD PLAN — bucket the ground resolver (drafted 2026-07-09, ready to build)

**Problem.** `ResolveRegionCombat` is O(units²): nested `foreach attacker → foreach reachable defender`. At 10,000 units that's ~10⁸ ops/tick. The fix is the ship model — compute per **bucket**, distribute by count — but ground units carry per-hex positions + continuous health, so the bucket key differs from a ship's.

**The bucket key = `(FactionId, HexQ, HexR, UnitType, Attack, Defense, Evasion, Shield, DamageType, Range, doctrineAttackMult, doctrineDamageTakenMult)`.** Units sharing it are interchangeable AND co-located, so they have the SAME reachable set and take damage identically. The "100 zerglings stacked on a muster hex" case → **one bucket**; a lone Titan → a bucket of one — exactly the ship model, with hex position added to the key (that's the only ground-specific part).

**The rewrite (keeps the current damage math verbatim — this is a LOOP restructure, not a math change):**
1. **Build buckets** from `units`: group by the key above; each bucket carries `Count`, a representative unit's stats, and its hex. (Health: bucket only units at the same *health tier* — round health to a coarse band so a lightly-vs-heavily-damaged zergling split into 2 buckets; the band granularity is a tunable, `HealthBandSize`.)
2. **Reach between buckets** = `HexDist(aBucket, dBucket) ≤ aBucket.Range` — computed once per bucket-PAIR (O(buckets²), and buckets ≪ units).
3. **Damage:** an attacker bucket of `Na` units pours `Na ×` its per-unit `pool` over its reachable defender buckets, health-weighted by `dBucket.Count × dBucket.Health`. Per defender bucket: run the SAME kernel chain ONCE (`HitFraction` dodge → shield-pool drain → `ArmourSoak`), then multiply by the counts. Accumulate `incoming[dBucket]`.
   - **Shield pool:** drain the bucket's *representative* pool once and apply to all — since co-located identical units share the profile, the per-unit absorbed is identical (the pool is per-unit, so `absorbed` is computed per-representative and applied to each). Deterministic + order-independent at the bucket level (better than today's per-source drain).
4. **Apply:** `incoming[dBucket]` is per-unit damage; drain each unit in the bucket by it (uniform, since identical), removing whole units whose health hits 0 → `casualties = the count that dies`, the rest share the residual — same continuous-health model, just computed once and distributed.
5. **Gauge:** `GroundBucketPerfTests` — a 5,000-unit region resolves in milliseconds (the ground twin of `CombatPerformanceTests`); and an **equivalence test** — a bucketed resolve of a known small fight matches the current per-unit resolve unit-for-unit (proves the restructure moved no number for the interchangeable case).

**Risk / invariants.** Determinism (bucket iteration keyed on the sortable key, not dict order); the continuous-health distribution must match the per-unit focus-fire for identical units (it does — identical units drain identically); a NON-uniform mix (units of different health/stats on one hex) simply forms more buckets, never wrong, just less compression. The per-hex position in the key is what makes a *spread-out* army less compressible than a *stacked* one — which is realistic (a concentrated swarm is the cheap case, matching the "muster hex" reality).

**Why deferred, not rushed:** it's a full resolver-loop rewrite with a real equivalence-proof burden (the interchangeable-case byte-match), best landed as its own CI-gated slice with the perf + equivalence gauges above — not stacked under another change.
