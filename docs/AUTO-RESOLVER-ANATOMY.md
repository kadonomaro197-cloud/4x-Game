# Auto-Resolver Anatomy & Dial-Insertion Map

**As of:** 2026-07-13 · the resolver taken apart bit by bit, so every designer dial has a **known insertion point** and battles are authentic. Companion to `COMPONENT-DESIGNER-DIALS.md` (§0e is the number anchor). Source of truth: `GameEngine/Combat/` (`ShipCombatValueDB.cs`, `CombatEngagement.cs`, `WeaponProfile.cs`) + `Combat/CLAUDE.md`.

> **⚠ Status note (2026-07-13):** most of what this doc's §4 and §7d once listed as **➕ backlog** has since been **built and wired in code** (Penetration, PerShotEnergy, HeatPerSecond fields; mid-battle ammo drain; heat throttle; point-defense; inertialess evasion; reactionless drive). Those rows are flipped to ✅ below. **This doc no longer carries the authoritative build-state** — for whether any given dial is live, read `Combat/CLAUDE.md` and the source; the status markers here are a snapshot, not the gauge. Runtime behaviour of these is unverified by CI (it can't run the client); they exist and are wired.

> **⚙ Distributed 2026-07-09.** The per-category insertion points from this doc are now **folded into each category's ⚙ Wiring Dossier** in `COMPONENT-DESIGNER-DIALS.md` (`⚙ 1`…`⚙ 11`) — the self-contained wiring reference for each category. This doc remains the **cross-category resolver anatomy + the ➕ extension backlog** (the origin of those insertion points); when you wire a category, read its dossier, and come here only for the whole-resolver picture or the shared backlog.

**The finding in one line:** the resolver reads a **small, fixed input surface** — one `WeaponProfile` per weapon (**10 fields as of 2026-07-13** — the original 7 plus `Penetration`, `PerShotEnergy`, `HeatPerSecond`, `WeaponProfile.cs:108,118,126`) + a handful of per-ship values + ~10 global constants. A dial is authentic **only if it lands on that surface.** Most weapon dials already do; the few that don't need a **deferred mechanic** (a subsystem first).

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

---

## 4. The resolver-extension backlog — MOSTLY BUILT (status pass 2026-07-13)

The six extensions this section once listed as ➕ backlog have **landed in code** as of 2026-07-13. They are wired; most are **inert by default** (the base-mod weapons don't yet dial them, so live combat is byte-identical until a design turns them on) — that's build-state (b), not (c). CI can't run the client, so runtime behaviour is unverified. Read `Combat/CLAUDE.md` for the live status of each.

1. **`Penetration` field + `ArmourSoak` term. ✅ BUILT.** `WeaponProfile.Penetration` (`WeaponProfile.cs:108`) cancels the target's flat armour point-for-point; `GroundDamageMatrix.ArmourSoak`'s 3-arg overload forwards to the shared `CombatKernel` (resolver merge slice 3a). *Unlocks:* lance/sabot/AP/piercing as real armour-crackers. (Base-mod weapons default Penetration 0 → byte-identical until a design dials it.)
2. **`PerShotEnergy` field. ✅ BUILT.** `WeaponProfile.PerShotEnergy` (`WeaponProfile.cs:118`) → `CombatKernel.BurstShotCount` splits a source into flat-soaked shots, so an alpha punches armour a chip bounces off. *Unlocks:* Linked-fire, charge-alpha, the swarm-vs-alpha texture. (Default 0 → byte-identical.)
3. **Mid-battle `AmmoPool` drain. ✅ BUILT.** `CombatEngagement.cs:627-635` drains `FleetCombatStateDB.AmmoPool_kg` per salvo and silences dry weapons (W3b). *Unlocks:* Ballistic/Guided magazine depletion. (Inert until a `ShipMagazineAtb` seeds a pool.)
4. **Recoil → Tracking term. ✅ BUILT.** `ShipCombatValueDB.RecoilTrackingFactor(recoil, chassisMass)` cuts a kinetic weapon's built Tracking (`ShipCombatValueDB.cs:358,381`, W4). *Unlocks:* big gun on a small hull can't aim. (Every base-mod weapon Recoil 0 → byte-identical.)
5. **Heat → sustained-rate throttle. ✅ BUILT.** `CombatEngagement.cs:650-657` accumulates each fleet's `HeatPool_kJ` (Σ HeatPerSecond × dt) and throttles the energy guns over the cap (W5b). *Unlocks:* Energy cooling, burst-vs-sustained. (Self-gating: base-mod HeatPerSecond 0 → skipped → byte-identical.)
6. **Point-defense missile intercept. ✅ BUILT (partial).** `CombatEngagement.cs:690` applies `FleetPointDefense` intercept to incoming missile damage (W6b). The **full** "missiles as individually-resolvable in-flight targets" model is still ⚙ — today it's a fleet-PD-rating-vs-missile-damage intercept fraction, not a per-projectile shootdown loop.

**Resolver merge (was a §6 prerequisite) — slices 3a/3c LANDED.** The shared flat-armour + dodge/shield math now lives in `Combat/CombatKernel.cs`; ground combat routes through it via `GroundCombatant.ToWeaponProfile` (`GroundCombat/GroundCombatant.cs:66`, called from `GroundForcesProcessor.cs:340`). So Penetration/PerShotEnergy/shield are built **once** on the kernel and shared ship↔ground, not twice. See `Combat/CLAUDE.md` and `docs/RESOLVER-MERGE-DESIGN.md`.

**Still deferred (⚙ — a subsystem each, they gate their dials):** adaptive shields (→ frequency modulation) · combat-environment modifier (→ medium) · per-shot timing (→ charge telegraph) · self-damage rule (→ overcharge) · profile-swap (→ multi-ammo) · the effect bus + capture (→ stun/conversion/Exotic effects) · positional/arc (→ mount traverse, or drop as flavour) · full missiles-as-resolvable-targets (→ per-projectile PD).

---

## 5. What the map shows

- The **fight-deciding core** (output, rate, nature, velocity, tracking, saturation, range, evasion, toughness, shields, doctrine) is **wired** — the resolver reads it, and §0e calibrated it to real numbers.
- The **depth dials** each have a **named home** — an existing field, one of the six §4 resolver extensions (**now built**, mostly inert-by-default), or a deferred subsystem.
- Because the resolver is an **aggregate salvo engine** (non-positional, non-per-shot-timed, whole-ship casualties), a few dials (arc, charge-telegraph) can't be expressed without changing the resolver's *nature* — those are marked ⚙/drop, not folded into the salvo math.

**Where things stand:** the ✅ core dials and the six §4 extensions are in code (the extensions inert until a design dials them; runtime unverified — CI can't run the client). The remaining ⚙ mechanics are the gated slices left. For the authoritative live status of any one dial, read `Combat/CLAUDE.md` and the source — not this doc.

---

## 6. Scale & composition — VERIFIED (any number of ships/soldiers, any combination)

Checked against the code, because the guarantee has to be real, not asserted.

**Ships — VERIFIED ✅.** `StepEngagementGroup` fire-mixes by weapon **class** (≤~6 buckets) and `ApplyCasualties` buckets defenders by combat value `(toughnessMult, evasion, toughness, role)` → cost is **O(buckets), not O(ships)**. Multi-party is native — any number of fleets, either side, fire divided 1/N (firepower conserved). Proven: `CombatPerformanceTests` (200 real warships in ms), `CombatBattleSims` B10 (1 dreadnought vs **1000** gnats ~9 ms), `MultiPartyEngagementTests` (assist / join mid-fight / fire-split). **So a dial that writes a `WeaponProfile` field impacts the resolve identically at any N and any composition** — a 1000-ship bucket resolves exactly as the per-ship math; a mixed fleet is just more class-buckets.

**Soldiers — AUTHENTIC MATH, but a PARALLEL resolver (the honest caveat).** `GroundForcesProcessor.ResolveRegionCombat` reads the **same matchup** (`GroundDamageMatrix.Matchup`/`ArmourSoak`, the triangle, cover/fortification, stance) — so a dial that writes the shared matchup DOES impact ground combat authentically. **But** it is a **separate implementation**, and it is **per-unit pairwise — O(units²) per region, NOT bucketed** (`foreach attacker-faction → foreach defender-faction → foreach unit → foreach reachable target`), over ground-specific stat fields on `GroundUnit` (a parallel to `WeaponProfile`). Consequences: (a) huge battalion counts scale **worse** than ships and have **no perf gauge**; (b) **every new dial term must be built TWICE** (ship `WeaponProfile` + ground `GroundUnit`) until the resolvers merge.

**The prerequisite that makes the guarantee uniform — the resolver MERGE (DECIDED 2026-07-06, `Combat/CLAUDE.md`; slices 3a/3c LANDED 2026-07-08).** Extract the shared salvo/matchup math onto a neutral **COMBATANT** view that both a ship entity and a soldier present, route both through the ONE resolver, delete the ground duplicate. **Status (2026-07-13):** the shared flat-armour + dodge/shield math is now in `Combat/CombatKernel.cs` and ground routes through it (`GroundCombatant.ToWeaponProfile` → `GroundForcesProcessor.cs:340`) — so Penetration/PerShotEnergy/shield are built **once**, ship↔ground. **What's NOT yet merged:** ground is still a **separate, per-unit O(units²) loop** (not the ship bucketing) — the math is shared, the *bucketing* isn't. So a dial term written on the kernel is wired once, but ground large-battle perf still lags ships. After full merge: **one bucketed O(buckets) path for ships AND soldiers**.

> **Status (2026-07-13):** the merge's *shared-math* slices landed alongside the §4 extensions, so each dial's kernel term is built once, ship↔ground. The *bucketing* half is still outstanding — so the "any number, any combination" guarantee is **fully true (bucketed) for ships** and **true-but-un-bucketed for soldiers** (ground shares the math but not the O(buckets) scaling).

---

## 7. Propulsion dial-insertion map — the MOVEMENT/CLOSING surface (Propulsion category, §2)

The weapon map above (§2–§4) is the **salvo-damage** surface. Propulsion lands on a **second, adjacent surface** the resolver already reads: **Evasion** (per-ship, feeds `HitFraction`) + the **closing-fight state** (`FleetCombatStateDB.Separation_m` / `ManeuverBudget`, and `FleetCombat`'s fleet-aggregation reads). The headline finding is the opposite of weapons: **the locked Propulsion doors need ZERO new resolver fields — the engine already runs Newtonian physics, and every dial writes a stat the resolver reads today.** The two exotic extensions this once called ➕ (inertialess evasion, reactionless drive) are now **built** (§7d); three exotic/fluid dials still defer on a named mechanic (⚙).

### 7a. The propulsion input surface (what a propulsion dial can touch)
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

### 7b. Calibration anchors (the movement/closing constants — already tuned, live)
| Constant | Value | Role |
|----------|-------|------|
| `AgilityReference_mps2` | 5.0 | accel at which agility half-contributes to Evasion (thrust ÷ mass) |
| `EvasionCap` | 0.95 | hard ceiling on Evasion — nothing untouchable |
| `SizeReference_m3` | 1,000 | volume at which size half-contributes to Evasion (big = easy to hit) |
| `ClosingSpeedScale_mps` | 1e6 | how fast the gap closes per step (live-tuned 2026-06-27) |
| `InitialSeparationDefault_m` | 1e6 (missile range) | where a fallback-seeded fight opens |
| `ManeuverBurnRate` | 5.0 | Δv the controller spends per step to hold the range — the kiting clock's drain |

These are the propulsion equivalents of §0e's joule anchors: a Reaction dial expresses itself in **m/s² of accel** (vs `AgilityReference 5.0`), **m/s of Δv** (→ `ManeuverBudget`), and **m/s of warp speed** — the scale the closing resolver already reads.

### 7c. Dial-insertion map — every LOCKED propulsion dial → where it lands
Legend as §3: **✅ field exists** · **➕ new term** · **⚙ deferred mechanic**.

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

### 7d. The propulsion resolver-extension backlog — TWO OF THREE BUILT (status pass 2026-07-13)
Because the movement surface is already wired, the propulsion doors added only three small terms — two now in code:
1. **Evasion-override term** (Exotic ▸ inertialess) — ✅ **BUILT.** `InertialessDriveAtb.EvasionOverride` → `ShipCombatValueDB.InertialessEvasionFloor` reads it as a health-scaled floor in `CalculateEvasion` (`ShipCombatValueDB.cs:575,584`), decoupling evasion from `accel = Thrust÷MassDry`. Base-mod `inertialess-drive` on the Phantom Inertialess Cruiser; gauge `ShipInertialessDriveTests`. (0 = no drive → byte-identical.) *Unlocks:* a capital that dodges like a fighter.
2. **Reactionless no-fuel flag** (Exotic) — ✅ **BUILT.** `ReactionlessThrustAtb` sets `NewtonThrustAbilityDB.ThrustInNewtons` directly + marks `Reactionless` true (unlimited Δv → `ManeuverBudget` never depletes) (`NewtonMove/ReactionlessThrustAtb.cs`, `NewtonThrustAbilityDB.cs`). Base-mod `reactionless-drive` on the Nomad; gauge `ShipReactionlessDriveTests`. **v1 = the combat/closing payoff + strategic Δv readout; the in-space burn model without consuming fuel is a flagged follow-up.** (default false → byte-identical.)
3. **Terrain-combat term** (Traction) — ⚙ still open. The H3 hex-terrain-in-combat follow-on already on the ground roadmap; a drive's preferred terrain gives a matchup edge, not just a movement one.

**Deferred mechanics (⚙ — each gates its dials):** the **air/altitude/depth combat layer** (Fluid's deep half + Exotic-gravitic) · the **H8 gate-network/addressing** (Warp-gate/Stargate) · the **Transfer ▸ teleport mode** (Exotic-teleport/H1). These are the same two prerequisite mechanics the §2 Propulsion category footer names — designed-in, not bolted-on.

### 7e. What the propulsion map shows
- The **closing/evasion core** (thrust→evasion→dodge, Δv→kiting clock→who-dictates-range, warp speed→transit, ground speed→the march) is **wired and gauged** — `ShipEvasionTests` (thrust/mass → evasion), `ClosingTests` (who-dictates + the P2 kiting clock), `FleetAggregationTests` (the fleet floors), `GroundForcesTests` (the hex-march). (These are engine gauges; client runtime is unverified — CI can't run it.)
- Propulsion needed **zero new resolver fields for the four Reaction/Traction/Fluid-access/Warp cores**; of the three small §7d extensions, **two (inertialess evasion, reactionless drive) are now built**, one (terrain-combat term) is still open, plus the same two deferred mechanics the category flagged. Calibration is inherited (the closing constants were live-tuned 2026-06-27), so a Reaction dial sanity-checks as the doors show (ev 0.48 sprinter vs 0.02 cruiser, §2.1).
