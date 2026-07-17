<!-- Provenance: generated 2026-07-17 by an 8-component read-only survey of the combat resolver (space CombatEngagement/AutoResolve + ground GroundForcesProcessor + the shared CombatKernel), each component mapped from source then adversarially audited against a scenario matrix, then synthesized. READ-ONLY: no resolver code was changed. Line numbers are as-of that date — re-verify before editing. This is the developer's reference for taking the auto-resolver apart (the 6-man-squad / unified-resolver work he owns). -->

# The Auto-Resolver Teardown — A Field Manual for Taking It Apart

*Written for the developer about to open the combat resolver himself. Plain English first, plumbing second. Every claim is cited to a file and line so you can go read the exact spot. Where the eight survey agents disagreed or admitted they hadn't read something, this document says so out loud rather than smoothing it over.*

---

## 1. The Big Picture

### What the auto-resolver actually is

Think of the combat system as an **automatic damage-control-and-gunnery station** that the game runs for you. Two forces come into range, and instead of you clicking every trigger, the engine does one exchange of fire per "tick," figures out who got hit, removes the dead, and decides whether anyone runs. It repeats until one side is gone, runs, or the fight stalls out.

There is one important design fact to hold onto before anything else: **a ship or a ground unit is "whole-or-dead" in this version.** There is no half-wrecked-but-still-fighting ship. The detailed per-component damage simulator (`DamageComplex`) exists in the codebase but is **parked** — combat does *not* route through it (root `CLAUDE.md` gotcha L10). Every survey agent independently confirmed this. It is the single most important limitation behind your Blood Angels problem, so keep it in mind through the whole read.

### The two resolvers and the one shared kernel

There are **two separate resolvers**, plus **one shared math library** they both call. This is the spine of the whole system:

| Piece | File | What it is |
|---|---|---|
| **Space resolver (live)** | `Combat/CombatEngagement.cs` — `StepEngagementGroup` at `:622` | The watchable, stepped, multi-fleet space battle. Has dodge, shields, ammo, heat, point-defense, doctrine, retreat, fog. This is the "real" space fight. |
| **Space resolver (instant)** | `Combat/AutoResolve.cs` — `Resolve` at `:74` | A *second, much simpler* space resolver for off-screen fights. Pure strength-math: firepower × seconds subtracts from toughness, whole ships die off a sorted list. **No dodge, no evasion, no shields, no ammo, no heat, no doctrine, no retreat** (`AutoResolve.cs:74-116`). It reports casualties but does not even destroy the entities itself. |
| **Ground resolver** | `GroundCombat/GroundForcesProcessor.cs` — `ResolveRegionCombat` at `:266` | The planet-surface fight, resolved per contested region, per hour. |
| **The shared kernel** | `Combat/CombatKernel.cs` (full file, `:1-291`) | The domain-neutral salvo math — the dodge curve, the shield-soak fractions, the flat-armour math. Pure functions, no side effects. **Both** the live space resolver and the ground resolver call into it, so dodge/shield/armour are defined *once* for the whole game. |

> ⚠️ **Naming trap the surveys flagged twice.** The word "AutoResolve" (the file `AutoResolve.cs`) is **not** where the interesting math lives. Two agents warned that a reader expecting bucketing, retreat, or dodge inside `AutoResolve.cs` will be surprised — those are all in `CombatEngagement.cs`. `AutoResolve.cs` is the stripped-down instant path only. When people say "the auto-resolver" loosely, they almost always mean the live `StepEngagementGroup` engine.

### Plain-English data flow: "two forces in range" → "who died"

**Space (the live path):**

1. **The clock is the prime mover.** `MasterTimePulse.SimulateTimeUntil` (`MasterTimePulse.cs:323`) advances game-time, normally in one-hour jumps (`Ticklength` default 3600s, `MasterTimePulse.cs:83`).
2. **A trigger sweeps each star system every 5 seconds of game-time.** `BattleTriggerProcessor` (a processor keyed to `StarInfoDB`) calls `CombatEngagement.Tick` (`CombatEngagement.cs:173`).
3. **Tick, pass 1 — form the fight.** It checks every pair of fleets: are they hostile (`AreHostile:1667`), both crewed, within the coarse 1-gigameter bubble (`InRange:1796`, `EngagementRange_m = 1_000_000_000.0` at `:40`), and (if the client's flags are on) within weapon range, detected, weapons-free. If a pair passes, both fleets get an `FleetCombatStateDB` stamped on them (`EnsureInCombat:572`) — that's the "in combat" flag.
4. **Tick, pass 2 — fight.** Every in-combat fleet in the system is gathered and `StepEngagementGroup` (`:622`) runs *one* exchange. In this version **the whole star system is one battlefield** — range only gated *joining*, not who fights whom once in (`:264-267`).
5. **Inside one exchange:** snapshot each fleet's ships and outgoing fire → drain ammo → build up heat → apply damage (shields soak first, then armour, then whole ships die) → move the range gap if closing is on → decide who drops out or retreats.
6. **Repeat** every tick until fewer than two hostile sides remain.

**Ground (the live path):**

1. One processor, `GroundForcesProcessor`, runs **once per hour** per planet (`RunFrequency=1h`, `:29`).
2. `ProcessBody` (`:71`) does the whole surface turn in order: reveal map, bill upkeep, move units (region-hop or hex-march), bleed hazard attrition, run the ROE maneuver step, then **group units by region**.
3. Any region with **2+ factions present** fights: `ResolveRegionCombat` (`:266`) runs one salvo.
4. **Inside one salvo:** classify terrain → compute the defender's cover×fortification divisor → regen shields → for every attacker-vs-defender-faction pair, gate fire by hex range, scale the attack pool by terrain and stance, route each shot through the shared kernel for dodge/shield/armour, accumulate damage per target from *pre-salvo* health (so the exchange is simultaneous) → apply it.
5. After all regions resolve: remove the dead (`:242`), flip captured regions and planets.

---

## 2. Component-by-Component Teardown

### 2.1 `CombatKernel.cs` — the shared salvo math (read this first, it's the heart)

**What it does:** the pure arithmetic of a single salvo — how much of a weapon's fire lands, how much a shield soaks, how much flat armour bounces. No entities, no random numbers, no clock. That purity is load-bearing: it is what makes combat **deterministic** — fast-forwarding a battle gives the exact same result as watching it (`CombatKernel.cs:16-18` docstring).

**Entry points and the real math:**

- **`HitFraction(weapon, evasion, separation=0)`** (`:147-169`) — the dodge curve. The fraction of one weapon's shots that land:
  - `velocityTerm = Velocity / (Velocity + 1_000_000)` (`:149`) — a light-speed beam → ~1, a slow slug → ~0.
  - `trackingEffectiveness = max(velocityTerm, Tracking)` (`:150`)
  - `dodgeChance = evasion × (1 − trackingEffectiveness)` (`:151`) — **you cannot dodge light** (a beam's velocityTerm ~1 zeroes out evasion).
  - `saturationFloor = Saturation / (Saturation + 50)`, floored at `MinLandedFraction = 0.02` (`:162-163`) — enough volume always lands *something*; a perfect dodger still eventually dies.
  - `hit = clamp(1 − dodgeChance, saturationFloor, 1)` (`:165`).
  - Constants: `VelocityReference_mps = 1e6` (`:38`), `SaturationReference = 50` (`:42`), `MinLandedFraction = 0.02` (`:46`), `FlightTimeReference_s = 10` (`:51`), `RangeBaseMiss = 0.9` (`:57`, **mutable/live-tuned** — see note below).
- **`LandedFraction(fireMix, evasion, separation)`** (`:174-183`) — damage-weighted average of `HitFraction` over a whole mix of weapons.
- **`ShieldSoakFraction(nature)`** (`:133-140`) — how much of a nature a shield *can* stop: **Kinetic 1.0 / Energy 0.5 / Explosive 0.75 / Exotic 0.0** (`:62-68`). Exotic bypasses shields entirely.
- **`ResolveShield(pool, capacity, regen, salvoDamage, soakFraction, dt)`** (`:204-217`) — drain the pool by `min(salvoDamage × soakFraction, pool)`, then regenerate. Returns `(absorbed, newPool)`.
- **`ArmourSoak(armour, sourceDamage, penetration, natureFactor)`** (`:245-255`) — the flat-per-source armour bounce:
  - `effectiveArmour = armour − penetration` (`:250`); if ≤0, full pass (`:251`).
  - `after = sourceDamage − effectiveArmour × 1.5 × natureFactor`, floored at `sourceDamage × 0.1` (`:253-254`). Constants `ArmourSoakPerPoint = 1.5` (`:75`), `ArmourMinPassFraction = 0.1` (`:78`).
- **`BurstShotCount(weapon)`** (`:266-273`) — `round(DPS / PerShotEnergy)`, clamped `[1, 1000]` (`BurstSoakMaxShots = 1000`, `:259`).
- **`ArmourSoakBurst(armour, sourceDamage, shotCount, ...)`** (`:282-288`) — splits a source into `shotCount` equal chunks, soaks each flat, sums. **This is the "many small hits bounce, one big hit punches through" identity:** N small chunks each lose the flat amount; one big lump loses it once.

**The `Combatant` class (`:87-126`)** is the neutral view a ship OR ground unit hands to the kernel. **Two corrections the surveys made against the task's assumptions:** (1) it is a `sealed class`, not a struct (`:87`); (2) **no kernel function actually takes a `Combatant`** — every function takes primitives (evasion, armour, a weapon list). The `Combatant` is just a caller-side data holder. It carries `Weapons` as a `List<WeaponProfile>` (`:120`), so at the kernel level a combatant can hold many weapons.

**Feeds in:** `WeaponProfile` objects + scalar target state. **Feeds out:** pure numbers — a landed fraction, a soak fraction, an `(absorbed, pool)` tuple, a post-armour damage number. **The kernel computes; the caller applies** (destroys the ship / drains the unit's health).

> ⚠️ **The one thing that breaks purity:** `RangeBaseMiss` (`:57`) is a *mutable public static*, live-tuned. Two runs with different `RangeBaseMiss` give different results. Everything else is pure w.r.t. its arguments.

### 2.2 `ShipCombatValueDB.cs` — a ship's combat value (built once at construction)

**What it does:** turns a ship's real installed parts into the flat numbers combat reads — like stamping a nameplate rating on a machine when you build it, then never re-deriving it under load. `Calculate(ship)` (`:281-549`) runs **once** at `ShipFactory.CreateShip` and caches the result on the entity.

**The real math:**
- **Toughness** = Σ (component health% × `ComponentHitPoints_J`) + armour.thickness × `ArmorHitPointsPerThickness_J` (`:297-298, 519-520`). Both constants = `100000` (`:81, :84`).
- **Firepower** = Σ each weapon's damage/second: beam = Energy/ChargePeriod (`:346`); railgun = KineticEnergyPerShot × RoundsPerSecond (`:373`); flak = DamagePerPellet × (RoundsPerSecond × PelletsPerShot) (`:395-396`); missile launcher = a **flat stub of 100000** (`MissileLauncherFirepowerStub`, `:446`).
- **Evasion** (`CalculateEvasion:571-596`) = `0.95 × [1000/(1000+Volume)] × [accel/(5+accel)]`, capped at `EvasionCap = 0.95` (`:96`). Small + agile = hard to hit.
- **RoleWeight** = 1.0 if armed, else 0.25 (`UtilityRoleWeight`, `:76`) — combatants die last.

> ⚠️ **Critical gotcha every salvo agent flagged:** combat value is **cached at build and never recalculated on damage** (`:31-33`). A damaged-but-alive ship still rates at *full* value. This is the direct consequence of whole-or-dead. It's also the intended future seam: recalculating a damaged ship's value would land it in a different casualty "bucket" (a degraded tier), but that is **not built**.

### 2.3 `WeaponProfile.cs` + `WeaponClassifier.cs` — the two-axis weapon model

**What it does:** every weapon is described by **two authored axes** and its old "class" falls out as a computed label:
- **Nature** (`:75`): Kinetic / Energy / Explosive / Exotic — how it meets the *defence* (shields, armour).
- **Delivery** (`:78`): Beam / Bolt / Slug / Cloud / Guided / Blast — how it meets the *dodge*.
- **Class** (`:72`, `[JsonIgnore]`) is *derived*: `WeaponClassifier.Classify(Delivery, Velocity, Tracking, Saturation)`. Thresholds: `BeamVelocityThreshold_mps = 10_000_000` and `FlakSaturationThreshold = 50` (`WeaponClassifier.cs:19,23`). You set dials; the triangle corner emerges.

**Feeds in:** built by `ShipCombatValueDB.Calculate` (ship side) and `GroundCombatant.ToWeaponProfile` (ground side). **Consumed by:** the fire-mix aggregator and the kernel.

### 2.4 `CombatEngagement.cs` — the live space resolver (the big one)

This is ~1827 lines; no single agent read all of it, but the combat-critical spans were covered. Key pieces:

- **`Tick` (`:173`)** — the trigger. Pass 1 forms fights (the gate chain at `:200-261`); pass 2 collects in-combat fleets and calls `StepEngagementGroup` once (`:263-276`).
- **`BuildFireMix(ships, separation)` (`:1139-1191`)** — **the aggregator that keeps 100-ship battles cheap.** It walks every ship's every weapon and buckets them by `(Class, Nature, Delivery)` into a handful of entries. Within a bucket: damage summed, velocity/tracking/saturation collapsed to *damage-weighted averages* (`:1156, :1188`). Range-gates finite-range weapons against the current gap (`:1169`). **Note:** the aggregated ship profile hard-zeroes `Penetration` and `PerShotEnergy` (`:1188`) — this is why flat-armour bounce does *not* work on ships (see the matrix, scenario 6).
- **`StepEngagementGroup` (`:622-815`)** — one exchange: snapshot → ammo (`:669`) → heat (`:693`) → damage (`:714`) → closing (`:765`) → resolution (`:771`).
- **`ApplyCasualties` (`:860-952`)** — buckets *defenders* by `(toughnessMult, evasion, toughness, role)`, computes `LandedFraction` once per bucket, sets `EffToughness = Toughness × toughnessMult / landed` (`:878`), and kills **whole ships as a count**: `kills = floor(DamageTakenPool / EffToughness)` (`:908`). Combatants-first, then most-hittable-first. **This is where "500 identical fighters cost the same as 5" comes from.**
- **`ShouldRetreat` (`:1455-1467`)** — retreat if flying a retreat posture OR `lost ≥ InitialShipCount × threshold`, where `threshold = 0.5 + (collectivism − 0.5) × 2 × 0.4`, clamped `[0.05, 0.95]` (`:1487-1491`). Constants `RetreatCasualtyThreshold = 0.5` (`:48`), `CollectivismRetreatSwing = 0.4` (`:55`).

**Key pace dial:** `SalvoDamageScale = 0.1` (`:103`) — only 10% of raw salvo energy counts toward kills each step. Two agents note this changes battle *duration*, not outcome, because it scales every fleet's incoming fire uniformly.

### 2.5 `GroundForcesProcessor.cs` — the ground resolver

**What it does:** one hourly processor doing movement, ROE, the fight, and capture for a whole planet.

- **`ResolveRegionCombat` (`:266-394`)** — the ground salvo. Terrain classified (`:269`); the defender's `coverFort = CoverDefenseMult × Fortification.DefenseMult` (`:272-273`) divides *only the defender's* incoming; shields regen (`:285-291`); then the **nested faction loop** (`:297-379`): for attacker-faction f, defender-faction g (skip f==g), each attacker u builds a `reachable` list gated by `HexDist(u,t) > u.Range → continue` (`:312`), scales its pool by terrain and stance, and routes each contribution through the kernel.
- **The attack pool (`:328-333`):** `atk = Attack × TerrainAttackMult × LocomotionTerrainMult × AttackMult`, then `pool = atk × SalvoScale` (`SalvoScale = 1.0`, `:35`), then `pool /= coverFort` for the defender.
- **`HexDist` (`:397-398`)** — cube hex distance; this is the **entire** range model on the ground side.

**The ground first-strike (well-built, the surveys praised it):** directed fire per-attacker means a Range-3 artillery unit hits a Range-1 infantry unit from 2 hexes away, and on the infantry's mirror pass `reachable` is empty → it fires nothing → **no reply** until it closes (`:308-315`). The ROE step `ApplyEngagementManeuvers` (`:408`) walks a unit one hex per tick toward or away, and a unit **fires while repositioning** — it "shoots while backing away."

### 2.6 `GroundCombatant.cs` — the ground→kernel bridge

**What it does:** turns a `GroundUnit`'s flat stats into the kernel's `WeaponProfile` so ground dodge/shield/armour *emerge from the same math as space*.

- **`ToWeaponProfile(unit)` (`:66-85`)** — the **only** method the live resolver calls. Synthesizes ONE profile: nature/delivery from `NatureDeliveryFor` (`:54`), and velocity/tracking/saturation from a switch on `DamageType` (`:72-83`): **Melee** 1/1.0/1 (undodgeable via tracking); **Artillery** 300/0/100000 (undodgeable via saturation); **Ballistic/Energy** 1000/0/1 (dodgeable, hit ≈ 1−evasion).
- **`ToCombatant` (`:92)`** — builds the full neutral view, but **is not called by the live resolver** — only by `GroundKernelBridgeTests`. The resolver reads `GroundUnit` fields directly.

> ⚠️ **Structural limits the bridge enforces** (multiple agents): a ground unit has exactly **one** weapon (`:104` hardcodes a single-element list); velocity/tracking/saturation are synthesized from `DamageType` alone, so two Ballistic units *cannot* differ in muzzle velocity or rate of fire; **Exotic nature and Beam/Cloud/Guided delivery are unreachable** from a ground unit (`NatureDeliveryFor` never emits them, `:56-60`); heat is always 0.

### 2.7 `GroundDamageMatrix.cs` — ground armour + the old matchup

**What it does:** its `ArmourSoak` overloads (`:93,100,108,116`) **all forward to `CombatKernel.ArmourSoak/ArmourSoakBurst`**, and its two constants are literally `= CombatKernel.ArmourSoakPerPoint/ArmourMinPassFraction` (`:45,49`) — so ship and ground read **identical** armour numbers. The old `Matchup` method (`:63`) is now **readout/bombardment-only** — the live per-source fight uses the kernel instead.

### 2.8 `AutoResolve.cs` — the instant off-screen resolver

**What it does:** the pure strength-math path. `Resolve` (`:74-116`) sorts each side by RoleWeight, then each round adds `Σfirepower × RoundSeconds` to the other side's pool and removes whole ships. Constants `RoundSeconds = 5` (`:19`), `MaxRounds = 2000` (`:23`). **It reports `DestroyedA/DestroyedB` but destroys nothing itself** (`:26-27`) — the caller does. The live `StepEngagementGroup` does **not** use it.

### 2.9 Doctrine / Retreat / Battle-record blobs

- **`FleetDoctrineDB.cs`** — `FirepowerMult/ToughnessMult/SpeedMult` (default 1.0, `:31-33`), `IsRetreat`, `Posture`, `SwitchableAfter` cooldown. **`SpeedMult` is stored but never read by movement** (dead dial — flagged by two agents).
- **`FleetRetreatDB.cs`** — set on break-off, carries a withdraw vector + `FledFromFleetId`. **It is a dead-end hook: no survivors actually move** — a v2 movement layer is meant to read it; nothing does yet.
- **`BattleLog.cs`** — runtime-only, 250-event ring buffer (`MaxEvents = 250`, `:73`), **not saved**. `BattleEvent` (`:27`) is **per-fleet**: whole-ship `ShipsLost/ShipsLeft` + a free-text `Note`. **Ground has no battle log at all.**

---

## 3. The Scenario-Correctness Matrix

This is the heart. Each row is one combat scenario; the columns are the **space verdict** and the **ground verdict** as the surveys found them. HANDLED / PARTIAL / GAP. Where agents disagreed, the cell says so.

| # | Scenario | Space | Ground | One-line note |
|---|----------|:-----:|:------:|---------------|
| 1 | **N bodies vs one HP lump** (your 6-man squad) | HANDLED\* | **GAP** | Space models N *ships*, each whole-or-dead (`ApplyCasualties:900-919`). *Weapon-profiles agent called space PARTIAL — no intra-ship HP.* Ground `GroundUnit` is **one Health scalar, no model count** (`GroundForcesDB.cs:53-55`). |
| 2 | **One unit, multiple different weapons** | HANDLED | **GAP** | Ship carries `List<WeaponProfile>`, all fire, bucketed by class (`BuildFireMix:1163`). Ground unit collapses to **one** `WeaponProfile` (`GroundCombatant.cs:104`). |
| 3 | **Ranged vs melee closing** | **PARTIAL** | HANDLED | Ground: clean hex-range first-strike + ROE march (`:312, :408`). Space: closing exists but **`EnableClosingRange` defaults OFF** (`:399`) → at gap 0 all weapons fire regardless of range; also a 0-range weapon reads as *unbounded* (`MaxReach:1060`). |
| 4 | **Evasion / dodge** (fast light vs heavy) | HANDLED | HANDLED | Shared kernel `HitFraction:151`; evasion is a per-target bucket key. Jump-pack = a component raising the Evasion stat. |
| 5 | **Saturation / area fire floors a dodge** | HANDLED | HANDLED | `saturationFloor` (`CombatKernel.cs:162`). *Space-engagement agent marked this UNCERTAIN because it hadn't read the kernel; the shared-kernel and salvo agents who did read it confirm HANDLED.* |
| 6 | **Flat armour bounces many small hits, not one big** | **GAP** | HANDLED | Ground applies real per-source flat soak + burst split (`:368-373` → `ArmourSoakBurst`). Ship folds armour into Toughness and applies a *proportional* fraction; `BuildFireMix` **zeroes PerShotEnergy** (`:1188`) so `BurstShotCount` is always 1. Deliberate v1 deferral (`WeaponProfile.cs:104-117`). |
| 7 | **Shields deplete → collapse; nature matchup** | HANDLED | HANDLED\* | Both use `ResolveShield` + `ShieldSoakFraction` (Kinetic 1.0 / Energy 0.5 / Exotic 0.0). *Ground caveat: **Exotic is unreachable** from a ground weapon, so the shield-bypass corner can't be fielded on the surface.* |
| 8 | **Ammo runs dry mid-fight** | HANDLED | **GAP** | Space drains `AmmoPool_kg`, silences dry weapons (`:669-684`). Ground has the pool + `GroundAmmo.Consume/IsDry` helpers but **the resolver never drains it** — units fire forever. |
| 9 | **Retreat + per-weapon / per-side casualty stats** | **PARTIAL** | **GAP** | See §6. Space: retreat works, per-*side* whole-ship counts in `BattleLog`, **no per-weapon, no wounded**. Ground: **no retreat threshold at all, no battle log at all**. |
| 10 | **Multi-party (3+ sides, reinforcement join)** | HANDLED | **PARTIAL** | Space: directed graph, fire divided `1/split` across enemies (`:727-729`). Ground: 3+ sides fight, **but firepower is NOT conserved** — a unit applies its *full* pool to *each* enemy faction (loop structure `:297-332`). |
| 11 | **Doctrine / stance swing; terrain / cover** | HANDLED | HANDLED | Mid-fight posture switch is a direct call past the engagement lock. Ground adds real terrain/cover/fortification divisors (`:271-273`). |
| 12 | **Fog of war / first-strike (blind enemy)** | HANDLED | **GAP** (detection) | Space: detection asymmetry — seer shoots a blind target that can't reply (`CanEngageTarget:1793`). Ground has **no detection combat gate** — its first-strike is *range-based only* (radar reveals the map, not who may shoot). |
| 13 | **Wildly mismatched forces resolve cheaply** | HANDLED | **PARTIAL** | Space buckets by combat value → O(buckets). Ground gets the right *outcome* (flat armour bounces the swarm) but is **O(units²)** — no bucketing; a large *symmetric* ground battle is far costlier than the space equivalent. |
| 14 | **Defensive posture as improvised armour** | HANDLED | HANDLED | `ToughnessMult` / `DamageTakenMult` + hardened plating all stack. Missing only the per-source bounce (row 6) on the space side. |

**\*Honest disagreements to note:** Row 1 space is where agents split — `space-engagement` and `space-salvo` call the fleet-of-N-ships case HANDLED (N discrete kills); `weapon-profiles` calls it PARTIAL (no per-*ship* hull tracking). Both are describing the same fact — bodies are modeled, wounds within a body are not. Row 5 space had one UNCERTAIN (an agent that hadn't opened the kernel) later resolved to HANDLED by the agents who did.

---

## 4. The 6-Man Squad, Specifically

This is your marquee test, so here is exactly what happens today when you field a 6-model Blood Angels squad.

### How the resolver treats it TODAY

A ground squad becomes **one `GroundUnit`** — a single serializable data object. It has:
- **One `Health` number** (`GroundForcesDB.cs:53-55`) — not six. There is no `Count`, `Models`, or `Strength` field anywhere on the unit (the doctrine-multiparty agent grepped for it and found nothing).
- **One `Attack` number** (`:49`).
- **One `Range`** in hexes (`:70`).
- **One `DamageType`** — described in the source comment as "from its heaviest weapon" (`GroundForcesDB.cs:93-94`), collapsed to one `WeaponProfile` by `ToWeaponProfile` (`GroundCombatant.cs:66`).

When the squad takes fire, the resolver subtracts floating-point damage from that one Health pool (`GroundForcesProcessor.cs:390`) and, when it hits zero, `RemoveAll(Health<=0)` deletes the whole unit (`:242`). It is a **single HP bar that vanishes at zero.**

### What is lost vs modeling 6 bodies

1. **Firepower doesn't degrade with casualties.** The attack pool at `:328` uses **full `u.Attack` regardless of `u.Health`** — a squad at 1% health hits exactly as hard as at full, right up until it evaporates. Losing 5 of 6 bolters *should* cut output ~83%; today it cuts it 0% until death. (The ground-resolver agent flagged this as the single cleanest fix — see below.)
2. **No mixed loadout.** Your squad can't carry a bolter *and* a melee weapon *and* a special weapon firing together — it's one `DamageType`, one nature, one range (scenario 2 GAP). A combat squad that should lose its plasma gun first can't.
3. **No wounded/killed/untouched distinction.** Whole-or-dead means there is no "3 wounded, 2 dead, 1 fresh" state to report or act on (scenario 9).
4. **No per-model targeting or per-model armour saves** — the flat-armour math treats the squad as one source/one target, not six independent saves.

### The smallest set of changes that would fix it

The surveys converge on a staged fix, cheapest first:

- **Fix A (one line, biggest bang): scale outgoing fire by health.** At `GroundForcesProcessor.cs:328`, multiply the attack pool by `(u.Health / u.MaxHealth)`. Now a battered squad fires proportionally less — the firepower-degradation problem is solved without any new data model. This is the ground-resolver agent's explicit "smallest fix."
- **Fix B (a real slice): add a model count.** Give `GroundUnit` a `ModelCount` + per-model HP, and have `ResolveRegionCombat` apply casualties model-by-model. This gets you "4 of 6 dead," morale-by-losses, and a wounded/killed/untouched tally. This is the bridge/multiparty agents' recommended structural fix and it is a genuine build, not a tweak.
- **Fix C (combined arms): a weapon list.** Change `GroundCombatant.ToWeaponProfile` to emit a `List<WeaponProfile>` (one per mounted `GroundWeaponAtb`) and loop each in the fire-pool build (`:328-376`), range-gating each separately. Now the squad's bolter, fist, and special all fire on their own terms — mirroring how ships already work.

Do them in order A → B → C; each is independently shippable and each is byte-identical when its new data is absent (an un-updated unit behaves exactly as today).

---

## 5. The Unified-Resolver Gaps

You want **one resolver where the same rules apply everywhere unless you specifically say otherwise.** Today space and ground share the *kernel math* but diverge in the *harness* around it. Here is every place they diverge and what unifying it costs.

| Divergence | Space | Ground | To unify |
|---|---|---|---|
| **Range model** | Metric separation in meters, gated behind `EnableClosingRange` (default OFF) → usually gap 0 (`SeparationOf` returns 0). | Integer **hex** distance, always on (`HexDist ≤ Range`, `:312`). | Pick one axis. The kernel already takes a metric `separation_m`; the ground bridge computes `Range_m` (`GroundCombatant.cs:69`) but **passes `separation=0`** so it's inert. Either feed hex×pitch as metric separation into the ground kernel calls, or promote the hex gate to space. **Latent risk:** the bridge agent warns that if a future slice sets ground `Position_m`, the *untested* kernel range term (`RangeBaseMiss=0.9`) suddenly activates and changes every ground outcome. |
| **Fog / detection** | Detection asymmetry — `CanEngageTarget` gates who can shoot whom via the sensor track table (`:1793`); blind enemy takes fire without reply. Behind `RequireDetectionToEngage` (default OFF). | **No detection combat gate at all.** Radar reveals the *map* only (`GroundSensors.RevealFromUnits`); the fight uses raw faction difference + hex range. | Add a per-faction detection predicate in `ResolveRegionCombat`'s target loop (`:309-314`) mirroring `CanEngageTarget`. The ground side even has a not-yet-wired `SensorJam` comment (`:146-147`) marking where this was meant to go. |
| **Multi-weapon** | `List<WeaponProfile>` per ship, all fire. | One `WeaponProfile` per unit. | Ground unit → weapon list (Fix C above). |
| **Firepower conservation (3+ sides)** | Conserved: `1/split` across enemies (`:727-729`). | **Not conserved:** full pool to each enemy faction (`:297-332`). | Hoist the attacker's pool/reachable computation *above* the per-defender-faction loop and divide across all reachable enemies regardless of faction. |
| **Retreat** | Real threshold + `FleetRetreatDB` (though survivors don't yet move). | **None** — units fight to annihilation unless manually ordered to another region. | Give ground a casualty-threshold break-off mirroring `ShouldRetreat`, and a ground `RetreatDB` echo. |
| **Battle stats** | Per-fleet `BattleLog` events (whole-ship counts). | **Nothing** — units die and vanish silently. | Give ground a battle-log echo (see §6). |
| **Hostility test** | `AreHostile` reads `DiplomacyDB` (pacts, war latch, stances). | Raw `FactionOwnerID` difference (`:301`) — **no diplomacy**, so a neutral or ally sharing a region would be attacked. | Route ground faction-pairing through `AreHostile`. |
| **Pace dial** | `SalvoDamageScale = 0.1` (`:103`). | `SalvoScale = 1.0` (`:35`). | Cosmetic — both just scale duration; pick a shared constant if you want identical pacing. |
| **Two space paths** | The instant `AutoResolve` (no dodge/shields/doctrine) vs the live `StepEngagementGroup` (everything). | One path only. | If you want a single rule set, `AutoResolve` should eventually call the same kernel/bucketing as the live path — right now they're two different resolvers with different fidelity. |
| **Performance model** | Buckets by combat value → O(buckets). | O(units²), no bucketing. | Bucket identical `GroundUnit`s the way `ApplyCasualties` buckets ships, so a 500-unit ground battle is as cheap as the space equivalent. |

**The good news:** the *hardest* part of unification — the actual damage arithmetic — is already unified in `CombatKernel.cs`. Both resolvers call `HitFraction`, `ShieldSoakFraction`, `ArmourSoak`, `BurstShotCount` with identical constants. What diverges is the **harness** (range, fog, retreat, stats, multi-weapon, conservation), and every one of those is a bounded, independently-shippable slice.

---

## 6. Battle Statistics — Stated Plainly

**Is killed / wounded / untouched, per-weapon, per-side, recorded anywhere today?** The doctrine-multiparty agent investigated this as its central question and grepped both subsystems. The answer, in order of how badly it's missing:

1. **Per-weapon kill attribution: NONE, anywhere.** Not space, not ground. The richest artifact is the space "Salvo Note" (`CombatEngagement.cs:935-951`), which names weapon *classes* that fired ("Railgun + Beam") and an aggregate hit/dodge% and total damage — but attributes **zero kills** to any specific weapon.
2. **Wounded / untouched: STRUCTURALLY IMPOSSIBLE.** Ships and ground units are whole-or-dead. There is no partial state to tally. This can't be fixed without un-parking the per-component damage sim (or adding Fix B's model count).
3. **Per-side, per-fleet counts: PARTIAL, space only.** `BattleLog` records per-fleet `Salvo` events with whole-ship `ShipsLost`/`ShipsLeft` (`BattleLog.cs:27`, recorded at `CombatEngagement.cs:950`). A caller *could* group these by `FactionId`, but **nothing generates a per-side summary**, and on retreat/end the events carry `ShipsLost=0` (`:1557, :829`) — there is **no end-of-battle casualty summary record** at all. And it's **runtime-only, not saved**.
4. **Ground: NOTHING.** No battle log, no event, no statistics of any kind (confirmed by grep across the whole `GroundCombat` dir). `ResolveRegionCombat` returns a bare `bool` (`:393`). Units die and vanish.
5. **`AutoResolve`** *does* return `DestroyedA/DestroyedB` lists (`AutoResolve.cs:28-38`) — a genuine per-side casualty list — but grep confirms they're consumed **only inside the resolver and tests**, never persisted, and the live path doesn't use `AutoResolve`.

**Where it would hook in.** The cheapest real win, per the surveys: on `RecordRetreat`/`EndEngagement`, emit a **per-fleet end-of-battle summary** by summing that fleet's `Salvo` events, and carry a **per-`WeaponClass` kill counter** accumulated inside `ApplyCasualties` (keyed by the dominant class in the incoming fire — that's the only weapon identity that survives aggregation). Full per-*weapon* credit requires the parked damage sim; per-*side* totals and per-*class* approximations are cheap. For ground, the template is the space `BattleEvent` shape — add the same struct and record it from `ResolveRegionCombat`.

---

## 7. Recommended Teardown Order (Safest First)

Read and modify the files in this sequence. It goes shared-math → readers → harness → the two resolvers, so you understand each layer before touching the one that depends on it. Each step names the CI gauge that proves you didn't break it.

1. **`Combat/CombatKernel.cs` (`:1-291`) — read only, first.** It's small, pure, fully read by the surveys, and it's the one definition both resolvers share. Understand `HitFraction`, `ShieldSoakFraction`, `ArmourSoak`/`ArmourSoakBurst` before anything else. Gauge: `CombatKernelTests`. **Do not add RNG or a clock read here — it would break fast-forward==watch.**
2. **`Combat/WeaponProfile.cs` + `WeaponClassifier.cs` — read only.** The two-axis model. Small, fully read.
3. **`Combat/ShipCombatValueDB.cs` — read only.** Understand that combat value is cached at build and never updated on damage — this single fact explains whole-or-dead.
4. **`Combat/AutoResolve.cs` — read only.** Get the *simple* space path in your head before the complex one. 157 lines, fully read.
5. **`Combat/CombatEngagement.cs` — the big one, read in spans.** Focus, in order: `Tick` (`:173`), `BuildFireMix` (`:1139`), `StepEngagementGroup` (`:622`), `ApplyCasualties` (`:860`), `ShouldRetreat` (`:1455`). This is the live space resolver. Gauges: `CombatPerformanceTests`, `DodgeTests`, `ShieldTests`, the battle-sim fixtures.
6. **`GroundCombat/GroundCombatant.cs` + `GroundDamageMatrix.cs` — read the bridge.** See exactly how a ground unit is presented to the kernel and where the structural limits (one weapon, no Exotic, synthesized velocity) live. Gauge: `GroundKernelBridgeTests`.
7. **`GroundCombat/GroundForcesProcessor.cs` — the ground resolver.** `ResolveRegionCombat` (`:266`), the hex range gate (`:312`), `ApplyEngagementManeuvers` (`:408`). This is where your squad fix lands.

**Then, when you start modifying — safest changes first:**

8. **Fix A: health-scaled ground firepower** (`GroundForcesProcessor.cs:328`, one line). Byte-identical to nobody; only changes wounded-unit output. Cheapest real improvement to the squad problem.
9. **Ground battle-stats record** (new `BattleEvent` echo from `ResolveRegionCombat`). Additive, read-only, breaks nothing.
10. **Ground firepower conservation across factions** (hoist the pool above the g-loop). Changes 3+-faction outcomes only.
11. **Fix C: ground multi-weapon list**, then **Fix B: model count.** These are real slices — do them one at a time, push, and **wait for CI green before stacking the next** (root `CLAUDE.md` pre-flight step 6; the SDK can't build in your cloud container, so CI is your only correctness gauge and it takes ~30 min).

**Two standing cautions the whole read depends on:**
- **The map is bigger than combat.** Before you touch the ground resolver, open `docs/SYSTEMS-STATUS-AND-TEST-PLAN.md` and read the ground-combat row's "Connected to" column — capture flips region ownership which flips colony ownership; changing casualty handling touches `MaintainFormations` and orbital bombardment softening.
- **CI can't run the client.** These are engine changes, so CI's `test` job will catch logic breaks — but any *runtime* feel (does the squad fight look right on screen?) only shows on your local Windows build. Add each squad change to `docs/CLIENT-TEST-CHECKLIST.md` for a local run.

---

*Sources: eight component surveys + scenario audits covering `CombatEngagement.cs`, `AutoResolve.cs`, `ShipCombatValueDB.cs`, `CombatKernel.cs`, `WeaponProfile.cs`/`WeaponClassifier.cs`, `GroundForcesProcessor.cs`, `GroundCombatant.cs`/`GroundDamageMatrix.cs`, and the doctrine/retreat/battle-log blobs. Where an agent explicitly did not read a file (e.g. the tail of `CombatEngagement.cs` past ~1714, or the `combatDoctrines.json`/`groundStances.json` data files), the claims resting on that are called out inline as docstring- or CLAUDE.md-sourced rather than verified in source.*