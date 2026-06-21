# Combat System Design

**The design principle:** Give players meaningful choices and simulate those choices honestly. Not spectacular visuals — correct outcomes from correct decisions. A clever trap should work. A numbers advantage should matter. Poor doctrine should cost ships.

---

## Hardware Constraints

Target machine: Lenovo ThinkPad, Intel i5-6300U (2-core mobile, 2.4 GHz, 2016), 16 GB RAM, Intel HD 520 (128 MB shared VRAM).

Goal: Thousands of units, multiple simultaneous engagements across a galaxy.

**What this rules out:**
- Rendering individual units in off-screen battles
- Per-projectile physics simulation at scale
- Real-time spatial collision detection for 1000s of actors
- Pixel-accurate bitmap damage for auto-resolved battles

**What this enables:**
- Math-heavy auto-resolution that runs in microseconds per battle (CPU-bound, no GPU)
- Full individual simulation only for battles the player is actively watching
- Formation-level bookkeeping as the default everywhere else

The key insight: most battles at any given moment are happening off-screen. If those resolve as math rather than simulation, the ThinkPad can handle the galaxy.

---

## Level-of-Detail (LOD) Combat Model

Every battle lives at exactly one tier at any moment. Tier changes when the player starts or stops watching — the transition is seamless, no battle interruption.

### Tier 0 — Auto-Resolution (Off-Screen)

Pure math. No spatial positions, no rendering, no individual unit tracking.

**Used when:** No player is watching the battle.

**What it models:**
- Fleet effective strength (adjusted for doctrine, commander skill, range, EMCON, environmental modifiers)
- Salvo exchange rounds: each round, both sides deal casualties proportional to strength
- Retreat trigger: when losses exceed doctrine threshold or commander discretion fires
- Duration: computed from weapon ranges and fleet speeds

**Output per battle:**
- Ships destroyed per side (which designs were lost)
- Ammunition and fuel consumed
- Retreat vector for the losing side
- Debris field spawned at engagement location
- Time elapsed (written back to the game clock)

**Cost:** O(fleets), not O(ships). Two fleets of 500 ships each costs the same CPU as two fleets of 5. This is what makes thousands of simultaneous engagements possible.

---

### Tier 1 — Formation-Level Watched Battle

**Used when:** Player opens a battle view for a large engagement (over the per-screen unit cap).

Tier 1 upgrades Tier 0 with:
- Spatial positions for formations (not individual ships) — one icon per formation, not one per hull
- Visual: formation icons, range rings, doctrine overlays
- Player can issue formation-level orders: advance, hold, retreat, focus fire on class

---

### Tier 2 — Full Individual Simulation

**Used when:** Player is watching a battle under the unit cap.

**What it simulates:**
- Individual ship positions (PositionDB)
- Individual weapon fire (BeamWeaponProcessor, MissileProcessor — the existing path)
- Individual damage (DamageProcessor.OnTakingDamage — Phase 1a, now wired)
- Individual retreat and maneuver AI

**Unit cap:** TBD by profiling — likely 100–200 ships before frame rate degrades on the ThinkPad. Above the cap, the battle falls back to Tier 1.

**Promotion/demotion:**
- Tier 0 → Tier 2: player opens the battle. The sim picks up from wherever Tier 0 left it — ship count, remaining ammo, fleet positions (interpolated).
- Tier 2 → Tier 0: player closes the battle view. Individual state is collapsed back to fleet aggregate and math takes over again.

---

## The Eleven Required Systems

These systems are needed for all combat scenario types — ambush, fleet engagement, orbital bombardment, patrol intercept, siege, covert insertion, and anything else. Listed in build order; each one depends on those above it.

---

### System 1 — Weapon Range

**What it is:** Every weapon has an effective range band. Shots outside that band miss or deal reduced damage. Currently missing — all weapons fire regardless of distance.

**Why it matters first:** Range is the foundation of every other combat decision. Doctrine, retreat thresholds, EMCON, and environmental hazards all assume range is meaningful. Without it, positioning is irrelevant and combat is a coin flip.

**What to build:**
- Add `MaxRange_m` and `OptimalRange_m` to weapon attributes (beam, missile, generic)
- Modify `WeaponUtils.ToHitChance()` to apply a range penalty curve (full accuracy at optimal, falling off toward max, zero beyond max)
- Modify `GenericFiringWeaponsProcessor` to skip fire if target is out of max range
- Add range-adjusted strength modifier to Tier 0 auto-resolution

**Files:** `WeaponGeneric/GenericWeaponAtb.cs`, `WeaponBeam/GenericBeamWeaponAtb.cs`, `WeaponMissile/MissleLauncherAtb.cs`, `WeaponUtils.cs`, `GenericFiringWeaponsProcessor.cs`

---

### System 2 — Sensor Range + Contact Model

**What it is:** Ships can only engage targets they detect. Detection has range. Detected contacts have a confidence level: detected (something is there), identified (we know its class), or locked (weapons can target it).

**Why it matters:** Without sensors, ships automatically know everything. That breaks ambush scenarios, EMCON, and commander discretion — you can't decide to retreat from a threat you haven't seen.

**What to build:**
- Read `GameEngine/Sensors/CLAUDE.md` first — sensor range infrastructure may partially exist
- `ContactDB`: one per detected entity per observer, holds confidence level and last-known position
- Detection check runs every sensor pulse: distance vs. (sensor range × target signature)
- Within detection range: contact created. Within lock range: weaponable.
- Contacts degrade if target moves out of range (last known position retained, confidence drops)

**Files:** `GameEngine/Sensors/` (read first), new `ContactDB.cs`

---

### System 3 — Transponder / IFF

**What it is:** Each ship broadcasts a friend-or-foe signal. The signal can be turned off (EMCON). Without a valid signal, a detected contact is Unknown, not Friendly.

**Three contact states:**
- **Friendly** — valid transponder, confirmed on our roster
- **Unknown** — detected but no valid transponder, or transponder off
- **Hostile** — confirmed enemy (IFF match to enemy faction, or fired on us first)

**Commander rules for Unknown contacts** (set by doctrine, overridable by player):
- Ignore / Monitor / Engage if threatening / Engage on sight

**What to build:**
- `TransponderDB`: on/off flag, faction ID, broadcast range
- IFF resolution: per-contact, per-observer (two ships can read the same contact differently if one has better sensors)
- Transponder off = Unknown to all observers, regardless of range
- Hostile confirmation rules: match doctrine, or direct fire event

**Applies to:** Civilian ships, neutral parties, covert ops, ambush, any scenario with mixed actors.

---

### System 4 — Fleet Doctrine

**What it is:** Each fleet has a standing doctrine that governs when and how it engages. Doctrine is the player's primary lever for battles they aren't watching. Commanders execute within it.

**Three doctrine categories:**

| Category | Behavior |
|----------|----------|
| **Defensive** | Hold position. Engage only if fired upon or hostiles cross the engagement threshold. Retreat at defined casualty %. Protect utility ships. |
| **Offensive** | Close to optimal weapon range. Engage all hostiles. Accept casualties to press the advantage. Pursue retreating enemies if fuel permits. |
| **Utility** | Mission-defined (escort, patrol, intercept, resupply). Protect the mission objective. Commander uses discretion for anything the mission orders don't cover. |

**What to build:**
- `FleetDoctrineDB`: doctrine category, engagement range threshold, retreat threshold (% losses), focus-fire preference (by class)
- Doctrine is readable by Tier 0 auto-resolution as a strength multiplier
- Doctrine generates AI orders at Tier 1/2
- Player can change doctrine mid-battle. Takes effect on the next salvo exchange (not instant — ships need time to reposition)

**Files:** New `FleetDoctrineDB.cs`, wired into `FleetDB` or similar (read Fleets/CLAUDE.md first)

---

### System 5 — Retreat and Rally

**What it is:** Fleets can break off an engagement. Retreating ships are briefly vulnerable. Rallied ships can re-enter.

**Retreat triggers (any one of these fires it):**
- Casualties exceed doctrine retreat threshold
- Commander discretion determines odds are unacceptable (see System 6)
- Player issues direct retreat order

**States:**
```
Engaged → Breaking Off (1 salvo window, still taking fire) → Withdrawing (moving, not engaging) → Safe (at rally point)
```

**Pursuit:** A fleet with Offensive doctrine can pursue a retreating fleet. Costs fuel, extends the engagement, risks overextension.

**What to build:**
- Retreat state machine per fleet (not per ship — fleets retreat together unless detached)
- Rally point: player-settable before battle, or auto-calculated as "last safe position + direction away from contact"
- Tier 0: retreat ends the salvo exchange loop with a result that includes retreat vector
- Tier 1/2: ships actually maneuver away, pursuit AI fires if doctrine allows

---

### System 6 — Commander Discretion

**What it is:** Commanders observe the battle and fill in gaps that doctrine doesn't explicitly cover. A skilled commander makes better choices within their doctrine. They can also modify doctrine thresholds within bounds if the situation demands it.

**What discretion acts on:**
- Unknown contacts (identify, avoid, or engage based on assessment)
- Retreat threshold (lower it if the tactical picture deteriorates faster than expected)
- Weapon priority (focus fire on most dangerous ship class first, spread damage if swarming)
- Environmental use (move into the gas cloud for sensor cover)
- Detachment (split a formation to pursue or protect a flanked element)

**Commander attributes:**
- `TacticalSkill` (0–100): quality of discretionary decisions — higher = better threat assessment, better target selection
- `Aggressiveness`: weights toward Offensive choices within doctrine
- `Caution`: weights toward Defensive choices within doctrine
- `Initiative`: willingness to act without player orders; low-initiative commanders wait, high-initiative commanders don't

**What to build:**
- Commander evaluation function: reads doctrine + current tactical picture (contact list, casualty count, remaining ammo/fuel, range status) → outputs order deltas
- Plugs into all three tiers: strength modifier in Tier 0, formation order override in Tier 1, ship AI bias in Tier 2
- Player can override any commander decision via explicit order

**Files:** `GameEngine/People/CLAUDE.md` (read first — commanders exist as entities), new evaluation function

---

### System 7 — EMCON (Emissions Control)

**What it is:** Ships produce EM signatures. EMCON lets ships reduce their signature deliberately, at the cost of capability. A ship running silent is hard to detect but also nearly blind and slow.

**Signature sources (in order of contribution):**
1. Main drive thrust (largest)
2. Active sensor pings
3. Weapon charging and firing
4. Transponder broadcast

**EMCON levels:**

| Level | Signature | Capability |
|-------|-----------|------------|
| Full Active | Max | Full speed, active sensors, transponder on, all weapons ready |
| Reduced | Medium | Passive sensors only, reduced speed, transponder on |
| Silent | Minimum | Passive sensors only, minimum speed, transponder off |

Switching levels is not instant. A ship that burns full thrust to close range cannot immediately go silent — the drive signature persists for a time window.

**What to build:**
- `EMCONStateDB`: current level, current signature value (computed from active systems)
- Sensor range check factors in target's current signature (lower sig = shorter detection range)
- Drive thrust updates signature in real-time; decay curve when drive is cut
- Tier 0: EMCON affects whether a fleet gets a first-strike advantage (silent fleet detected late → defender gets reduced first-round defense)

**Applies to:** Ambush, covert insertion, patrol, recon, picket duty.

---

### System 8 — Environmental Hazards

**What it is:** Space isn't empty. Gas clouds, radiation belts, debris fields, magnetic anomalies, and asteroid fields all affect what ships can do.

**Effect categories:**

| Hazard | Effect |
|--------|--------|
| Gas cloud / nebula | Reduces sensor range, scatters active pings, reduces EM signature (cover) |
| Radiation belt | Increases electronic failure rate, may degrade sensor precision |
| Debris field | Forces reduced speed or risks hull impact; also reduces sensor range |
| Gravity anomaly | Affects missile trajectory and burn time |
| Asteroid field | Navigation hazard, line-of-sight blocks on some weapons |

**What to build:**
- `EnvironmentalZoneDB`: zone type, position, radius, modifier table (sensor mult, signature mult, weapon accuracy mult, speed penalty)
- Modifier table is looked up by zone type at combat start — pre-baked, not recalculated each round
- Tier 0: apply environmental modifiers to effective strength
- Tier 1/2: zones affect individual sensor checks and weapon fire calculations
- Commander discretion (System 6) can issue orders to use or avoid a zone

**Applies to:** Any scenario with a non-empty map. System generation should populate these. Terrain matters on the ground too — this system's architecture applies to both theaters.

---

### System 9 — Auto-Resolution Engine

**What it is:** The math engine that resolves off-screen battles. This is the single most important system for reaching the hardware target. Everything else feeds into it.

**Algorithm — one round per salvo exchange:**

```
Fleet Effective Strength (per fleet):
  = Σ(ship combat value for each ship)
  × doctrineModifier         (System 4)
  × commanderSkillModifier   (System 6)
  × environmentalModifier    (System 8)
  × EMCONModifier            (System 7 — first-round surprise if applicable)
  × rangeModifier            (System 1 — are we in our optimal range band?)

Salvo Exchange:
  casualties_A = f(strength_B, defenseRating_A, variance_roll)
  casualties_B = f(strength_A, defenseRating_B, variance_roll)

Apply casualties:
  Most exposed ships absorb first (front-of-formation combatants)
  Utility/transport ships absorb last
  Update fleet combat value

Check retreat triggers (System 5):
  If fleet A's losses exceed doctrine threshold → fleet A retreats
  If commander discretion fires → fleet retreats

Repeat until: one side destroyed, one side retreated, or time limit reached.
```

**Ship Combat Value** — pre-calculated from design at build time, not at combat time:
- DPS contribution at optimal range
- Defense rating (armor thickness + hull integrity)
- Effective range band
- Role weight (combatant vs utility vs transport — lower weight = lower priority target, less combat value contributed)

Cached in `ShipCombatValueDB`, recalculated when a ship takes significant damage (component loss triggers a recalc).

**What to build:**
- `ShipCombatValueDB`: cached at ship completion, updated on component loss
- `FleetCombatStateDB`: round-by-round state for an ongoing auto-resolved engagement (fleet strengths, casualty counts, round number, EMCON status, retreat state)
- Auto-resolution loop: called by `MasterTimePulse` (or `ManagerSubPulse`) when two fleets are within weapon range of each other
- Casualty application: probabilistic ship selection from fleet (weighted by role and position)
- Result packaging: destroyed ship list, retreat vectors, ammo/fuel cost, debris spawned, time elapsed

**Applies to:** Every off-screen battle. This is the system that makes the ThinkPad target achievable.

---

### System 10 — Debris and Salvage

**What it is:** Destroyed ships leave wreckage. Wreckage has salvage value, affects navigation, and affects sensor range. This connects combat outcomes to the economy.

**What to build:**
- On ship destruction: spawn `DebrisFieldDB` at location with mass and material composition from the ship design
- Large debris fields become Environmental Hazards (System 8): reduce sensor range, require cautious navigation
- Salvage order: direct a ship to collect materials from a debris field (returns raw materials to cargo)
- `SpawnWreck()` in `DamageProcessor.cs` is the intended hook — currently empty (see line 223)

---

### System 11 — Ground Combat Interface

**What it is:** The bridge between space combat and planetary combat. Orbital bombardment, troop landing, and siege mechanics connect the two theaters. This is a Phase 4 task — it depends on all ten systems above being functional.

**Components:**
- **Bombardment**: Beam weapons and missiles target surface installations. The colony damage block in `DamageProcessor.cs` (~lines 101–181) contains the original design intent — read it before building.
- **Landing**: Transport ships deploy ground units. Connects to `GameEngine/Fleets/CLAUDE.md`.
- **Siege**: Orbital interdiction prevents resupply convoys from reaching a colony, degrading it over time.

---

## Build Order Summary

| Order | System | Depends On | Why This Position |
|-------|--------|-----------|-------------------|
| 1 | Weapon Range | — | Gates everything: no range = no positioning |
| 2 | Sensor Range + Contact Model | 1 | Contacts need range to mean anything |
| 3 | Transponder / IFF | 2 | IFF classifies contacts — needs contacts first |
| 4 | Fleet Doctrine | 3 | Doctrine acts on contact states |
| 5 | Retreat and Rally | 4 | Retreat threshold is part of doctrine |
| 6 | Commander Discretion | 4, 5 | Commander modifies doctrine and retreat |
| 7 | EMCON | 2, 3 | Signature affects detection; can build in parallel with 4–6 |
| 8 | Environmental Hazards | 2 | Modifies sensors; can build in parallel with 4–6 |
| 9 | Auto-Resolution Engine | 1–8 | Tier 0 uses all modifiers — build last |
| 10 | Debris and Salvage | 9 | Debris spawns from auto-resolution casualties |
| 11 | Ground Combat Interface | 1–10 | Phase 4 — requires the full space combat stack |

Systems 7 and 8 can be built in parallel with Systems 4–6 because they are modifier inputs, not structural dependencies.

---

## What Already Exists

| Component | Status | Notes |
|-----------|--------|-------|
| Beam weapon fire | Working | BeamWeaponProcessor, fires and hits |
| Missile launch | Working | MissileProcessor, launches but guidance is partial |
| Hit calculation | Placeholder | 95% flat — no range, no modifiers |
| Damage (DamageComplex) | Wired (Phase 1a) | Three calibration issues deferred |
| Sensor system | Partial | Read Sensors/CLAUDE.md before touching |
| Fleet system | Partial | Read Fleets/CLAUDE.md before touching |
| Commander (People) | Exists as entity | Not wired to any combat decision |
| Doctrine | Not started | — |
| Retreat | Not started | — |
| EMCON | Not started | — |
| Auto-resolution | Not started | — |
| Environmental hazards | Not started | — |
| Debris/salvage | Not started | SpawnWreck() is empty hook |
| Ground combat interface | Not started | Colony damage block commented out |

---

## Open Defects to Fix Before System 4

These bugs exist in the current damage path. They don't block Systems 1–3 but will produce wrong results in auto-resolution if not fixed before System 4.

| Defect | File | Root Cause | Fix |
|--------|------|-----------|-----|
| Off-by-one: first component never targeted | `DamageProcessor.cs`, `ComponentPlacement.cs` | G-channel bitmap is 1-indexed; `ComponentLookupTable` is 0-indexed | Pre-decrement G value by 1 when looking up table, or start componentInstance counter at 0 |
| One-hit destroys all components | `DamageProcessor.cs` | `HealthPercent` is a float starting at 1.0; `damageAmount` is an int with value 1; 1.0 − 1 = 0 on first hit | Normalize units: either scale damageAmount to float 0.0–1.0 range, or scale HealthPercent to integer HTK |
| Material table nearly empty | `DamageTools.cs`, `damageResistance.json` | JSON field is `UniqueID` but constructor param is `iDCode` — Newtonsoft assigns default 0 for all entries | Fix JSON field name to match constructor, or add `[JsonProperty("UniqueID")]` to the constructor param |
