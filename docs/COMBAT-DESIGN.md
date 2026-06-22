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

## Before Working on Any System Here

Apply the Prime Directive from the root CLAUDE.md: map every connection before making decisions. Each system entry below lists its known connections. Before touching a system, verify those connections are still accurate — the codebase changes and the docs may lag. If you find a connection not listed here, add it before writing code.

**The four questions for every system:**
1. What feeds into it? (inputs, dependencies)
2. What does it feed into? (outputs, consumers)
3. What shares state with it? (same DataBlob, same entity, same global table)
4. What does it trigger? (events, processor chains)

---

## The Eleven Required Systems

These systems are needed for all combat scenario types — ambush, fleet engagement, orbital bombardment, patrol intercept, siege, covert insertion, and anything else. Listed in build order; each one depends on those above it.

---

### System 1 — Weapon Range

**What it is:** Every weapon has an effective range band. Shots outside that band miss or deal reduced damage. Currently missing — all weapons fire regardless of distance.

**Why it matters first:** Range is the foundation of every other combat decision. Doctrine, retreat thresholds, EMCON, and environmental hazards all assume range is meaningful. Without it, positioning is irrelevant and combat is a coin flip.

**Connections:**
- Feeds into: System 4 (doctrine sets engagement range relative to weapon range), System 9 (auto-resolution uses range-adjusted strength), hit-chance calculation in `WeaponUtils.ToHitChance()`
- Fed by: Component design attributes (`MaxRange_m` on weapon `*Atb` classes), PositionDB (distance to target calculated at fire time)
- Shares state with: `GenericFiringWeaponsDB` (internal magazine, weapon state), `BeamInfoDB` (beam in-flight state), `FireControlAbilityState` (assigned target)
- Triggers: Nothing new — modifies the existing fire/no-fire decision in `GenericFiringWeaponsProcessor`
- Environmental connection (System 8): Some weapon types (beams) scatter in gas clouds. Range effective distance may be shorter in certain zones. Build the base range system first; add zone modifiers when System 8 is built.

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

**Connections:**
- Feeds into: System 3 (IFF classifies contacts created here), System 4 (doctrine acts on contact list), System 6 (commander discretion reads contacts to make decisions), System 9 (auto-resolution first-round surprise depends on whether contact was detected), `FireControlAbilityState` (can only assign a weapon to a locked contact)
- Fed by: Sensor component attributes (sensor range), System 7 EMCON (target's current EM signature shrinks their detection radius), System 8 environments (gas clouds reduce sensor range), PositionDB (distance between observer and contact)
- Shares state with: `FireControlAbilityDB` (the contact a fire control is assigned to must be a valid locked contact), `GenericFiringWeaponsDB` (target must be locked to fire)
- Triggers: Contact creation → IFF check (System 3). Contact lost → fire control loses target → `GenericFiringWeaponsProcessor` stops firing.
- Economy connection (not combat): Sensors also detect mineral signatures, jump points, and survey anomalies. The sensor range system likely already exists partially for these. Read `Sensors/CLAUDE.md` carefully — you may be extending an existing system, not building from scratch.

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

**Connections:**
- Feeds into: System 4 (doctrine rules reference contact classification), System 6 (commander decides what to do with Unknown contacts), System 9 (auto-resolution needs to know who is hostile to who), `GenericFiringWeaponsProcessor` (should not fire on Friendly contacts)
- Fed by: System 2 (contacts must be detected before IFF runs), System 7 EMCON (transponder off → Unknown, overrides faction-match check), `FactionDB` or equivalent (roster of known Friendly factions)
- Shares state with: `ContactDB` (IFF result is stored on the contact per observer), faction relationship data (which factions are at war)
- Triggers: Classification change events — Unknown → Hostile (ship fires on us) triggers doctrine response in System 4 and potentially auto-engage in System 6. Friendly → Unknown (transponder cut) should alert the commander.
- Faction connection: The faction system already tracks diplomatic state between factions. IFF resolution should read that state, not duplicate it. Read `GameEngine/Factions/` before building.

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

**Connections:**
- Feeds into: System 5 (retreat threshold is part of doctrine), System 6 (commander operates within doctrine bounds), System 9 (doctrine category = strength multiplier in auto-resolution), fleet AI order generation at Tier 1/2
- Fed by: System 1 (doctrine's engagement range is relative to weapon ranges), System 2 + 3 (doctrine only acts when contacts are detected and classified), player orders (doctrine change mid-battle), System 6 (commander can push doctrine thresholds within limits)
- Shares state with: `FleetDB` or equivalent (read Fleets/CLAUDE.md — fleets may already have an orders/state structure that doctrine should live on rather than a separate DataBlob)
- Triggers: Engagement range crossed → begin engaging contacts per doctrine. Retreat threshold crossed → trigger System 5. Mission complete (Utility) → return to holding behavior.
- Logistics connection: Doctrine affects fuel consumption. Offensive doctrine that pursues retreating enemies burns more fuel. Utility doctrine on a resupply run protects cargo ships — if those ships are destroyed, the mission fails and doctrine should self-report that. Read `GameEngine/Logistics/CLAUDE.md` before setting doctrine's interaction with cargo/fuel.
- Orders system connection: Doctrine is a standing order, not a single command. It should integrate with `OrderableDB` / `EntityCommand` pattern rather than being a free-floating DataBlob. Read `Engine/Orders/` before building.

**What to build:**
- `FleetDoctrineDB`: doctrine category, engagement range threshold, retreat threshold (% losses), focus-fire preference (by class)
- Doctrine is readable by Tier 0 auto-resolution as a strength multiplier
- Doctrine generates AI orders at Tier 1/2
- Player can change doctrine mid-battle. Takes effect on the next salvo exchange (not instant — ships need time to reposition)

**Files:** New `FleetDoctrineDB.cs`, wired into `FleetDB` or similar (read Fleets/CLAUDE.md first)

---

### System 4 (detailed design) — Fleet Components & Switchable Doctrine

*This refines System 4 above and extends System 6 (Commander Discretion). System 4 puts one doctrine on the whole fleet; this pushes doctrine down to **components within a fleet**, so different parts of the same fleet can fight differently. Captured from the developer 2026-06-22.*

**Components.** A fleet is divided into named components — Front Line, Flank, Rear Guard, Artillery, … (names are data, open-ended). No rules on how ships are distributed: a component holds 1 ship or 50, and a ship is in exactly one component of its fleet. **Cheapest implementation: a component is a sub-fleet.** `FleetDB` already nests (`TreeHierarchyDB`) with order inheritance and detach/reattach, so ship assignment and movement already work — the new part is per-component doctrine state, not a new membership system. (See `Fleets/CLAUDE.md`. Decide later whether a lighter `FleetComponentDB` is cleaner than full sub-fleets.)

**Doctrine = posture with named options.** Each component runs one doctrine from the three families above (Offensive / Defensive / Utilitarian), but each family has **multiple named options, each with both an upside and a downside** whose value depends on the situation — that's where the player's judgment lives. Worked example: Front Line, Defensive, **"Fighter Screen"** → −25% main-weapon fire rate, − fleet movement speed, + screening/interception for the ships behind it. Great against an ordnance swarm; bad in a gun duel.

**Two data pieces (don't confuse them):**
- `FleetDoctrineDB` (System 4's runtime blob, now living **per component**) holds the *active selection*: which doctrine option is set, its thresholds, and the switch-cooldown clock.
- `CombatDoctrineBlueprint` (**new, moddable JSON** loaded by `ModLoader`) is the *catalog* of selectable options, so doctrines can be added/tuned without code and validated by a base-mod integrity test (same pattern as `Pulsar4X.Tests/Modding/BaseModIntegrityTests.cs`):
  ```
  CombatDoctrineBlueprint : Blueprint
    UniqueID        "fighter-screen"
    Family          Offensive | Defensive | Utilitarian
    DisplayName     "Fighter Screen"
    Effects         [ { Stat, Multiplier/Delta } ]   // WeaponFireRate ×0.75, FleetSpeed ×0.8, …
    CooldownSeconds game-time gate before this component can switch again
  ```
Effects are **modifiers applied at read time** (match the `BonusesDB` pattern) — never bake them into a ship's base stats, so toggling is reversible. The levers already exist: weapon fire rate rides the same throttle as thermal suppression (`WeaponState` / `GenericFiringWeaponsProcessor`); fleet speed rides fleet movement.

**Switching.** Set-and-forget, or changed live by player or NPC, gated by a **game-time cooldown** per component (System 4 already says doctrine changes aren't instant — this makes it an explicit timer). NPC choice reads the existing `DoctrineVector` on `FactionInfoDB` (Factions) — e.g. high Military weight → favour Offensive.

**Operational discretion (extends System 6).** A fleet commander can be granted authority to switch the doctrine of **specific components** on their own; the player/faction decides which commanders get it and over which components. With it off, only the player/faction switches. This is System 6's commander discretion, scoped per component.

**UI (extends the Fleet panel — you can't render 100 ships, so don't).** A Fleet Combat panel (extend `FleetWindow.cs`) shows a fleet's components; selecting one shows a **table of its ships** with the stats/systems that matter (weapons, armour/health, speed, fuel, current target), a doctrine dropdown (greyed while the cooldown runs, remaining game-time shown), and the discretion toggles. The table *is* the interface.

**Open decisions:** components = sub-fleets vs a new `FleetComponentDB`; which stats v1 doctrines may touch (start with the two from the example — fire rate + fleet speed); cooldown length (per-doctrine data or global); discretion granularity (per role vs per specific component); stacking (recommend component overrides fleet).

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

**Connections:**
- Feeds into: System 9 (retreat ends the auto-resolution loop and packages the result), System 10 (ships destroyed during Breaking Off phase spawn debris), movement system (Withdrawing state generates a movement order toward the rally point)
- Fed by: System 4 (retreat threshold comes from doctrine), System 6 (commander can trigger retreat ahead of threshold), player direct order, damage system (casualty count drives the threshold check)
- Shares state with: `FleetDB` / fleet order state (retreat is a fleet-level state, not per-ship — individual ships follow), `NewtonMoveDB` or equivalent movement DataBlob (Withdrawing ships need a destination and thrust authorization)
- Triggers: Breaking Off → Pursuing fleet AI activates (System 4 Offensive doctrine). Withdrawal complete → fleet reports Safe → can receive new orders. Rally and re-engage → Engaged state resumes.
- Movement connection: Retreat moves ships. The movement system (`GameEngine/Movement/`) controls thrust and intercept calculations. Retreat must issue movement orders it can execute — a ship with no fuel can't retreat under thrust. Read `Movement/CLAUDE.md` before building the Withdrawing state.
- Jump point connection: Fleets can retreat through jump points to escape a system entirely. This is a valid retreat destination. The jump point system needs to recognize a retreating fleet as authorized to transit.

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

**Connections:**
- Feeds into: System 4 (commander can adjust doctrine thresholds within limits), System 5 (commander can trigger early retreat), System 9 (commander skill is a multiplier on auto-resolution effective strength), fire control assignments (commander redirects weapons toward priority targets)
- Fed by: System 2 (commander reads the contact list), System 3 (commander knows contact classifications), System 4 (doctrine is the boundary commander operates within), System 7 (commander knows the EMCON state), System 8 (commander knows the environmental map), damage reporting (commander tracks casualty count to assess odds)
- Shares state with: `PeopleDB` or equivalent (commander attributes live here — read `People/CLAUDE.md` before building), `FleetDB` (commander is assigned to a fleet — the assignment is probably already modeled)
- Triggers: Commander decision → fleet order delta → Tier 1/2 formation repositions or Tier 0 strength modifier updates. Commander fires retreat → System 5 state machine activates.
- Research connection: Commander attributes (TacticalSkill, Initiative) may improve with experience or via tech unlocks. The Tech/Research system needs a hook to advance commander stats. Read `Tech/CLAUDE.md` before hardcoding attribute values as constants.
- People/career connection: Commanders are named entities with careers. A commander lost in battle is a permanent loss. The People system may already track this — death of a commander entity should remove them from fleet assignment. Confirm before writing the "commander survives retreat" vs "commander killed" logic.

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

**Connections:**
- Feeds into: System 2 (target's signature shrinks the observer's detection radius for that target), System 3 (Silent EMCON forces the transponder off → contact becomes Unknown), System 9 (silent fleet arriving = first-strike surprise modifier in auto-resolution)
- Fed by: Movement system (thrust level drives the signature value — `NewtonMoveDB` thrust commands affect EMCON), fire control (`GenericFiringWeaponsProcessor` charging/firing increases signature), sensor state (active vs passive scan mode)
- Shares state with: `NewtonMoveDB` or thrust DataBlob (thrust magnitude is the primary signature driver — EMCON Silent caps allowable thrust), `TransponderDB` (System 3), `FireControlAbilityDB` (weapon charging state)
- Triggers: Signature change → detection radius recalculated for all observers of that ship. EMCON state change → System 3 transponder check. Thrust burst while Silent → signature spike, detection radius temporarily widens.
- Propulsion connection: EMCON Silent imposes a max thrust limit. The movement system enforces thrust limits (via `NewtonThrustCommand` or equivalent). EMCON must be able to cap or override the movement system's authorized thrust. Coordinate with Movement/CLAUDE.md before building this — don't create two separate systems fighting over max speed.
- Weapons connection: Weapon charging is a signature source. A ship that charges weapons while Silent breaks EMCON. This must feed back to `GenericFiringWeaponsProcessor` — charging should only be allowed at EMCON levels that permit it, or auto-promotes EMCON to a higher level.

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

**Connections:**
- Feeds into: System 2 (sensor modifier per zone), System 7 (signature modifier per zone — gas cloud provides cover), System 9 (auto-resolution pre-bakes environmental modifiers into effective strength), System 1 (beam weapon range/accuracy reduced in some zones), System 6 (commander reads zone map to decide whether to use cover)
- Fed by: Galaxy/system generation (zones are placed when the star system is generated — read `Galaxy/CLAUDE.md` before building), System 10 (debris from destroyed ships creates new debris-field zones mid-battle), orbital mechanics (gravity wells are already modeled in the orbit system — link to that data, don't duplicate it)
- Shares state with: `SystemBodyInfoDB` or star system geometry DataBlobs (zones are spatial — they overlap with system geography), `PositionDB` (zone membership is checked against ship position)
- Triggers: Ship enters zone → modifiers activate. Ship leaves zone → modifiers deactivate. Debris spawned (System 10) → zone created or expanded.
- Galaxy generation connection: This is the most important connection to check. The galaxy generator already places asteroid belts, nebulae, and other features as system bodies. Environmental hazards should READ that existing data, not duplicate it. If the generator already creates an object called "nebula," the hazard system should attach to it, not create a parallel "EnvironmentalZoneDB" that duplicates the same region. Read `Galaxy/CLAUDE.md` and understand what the generator already models before building a new zone type.
- Ground combat connection: Terrain on the ground (dense jungle, urban areas, mountain passes) applies the same modifier logic as space hazards. The `EnvironmentalZoneDB` architecture should be designed from the start to serve both theaters — not just space.

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

**Connections:**
- Feeds into: Nothing directly — this is the terminal resolution engine. Output packages go to: `FleetDB` (fleet composition updated with casualties), movement system (retreat vector applied), System 10 (debris spawned from casualties), event log (battle summary published)
- Fed by: ALL seven systems above it. System 1 (range modifier), System 2 (detection = first-strike opportunity), System 3 (IFF = who is hostile to who, determines sides), System 4 (doctrine modifier), System 5 (retreat trigger and result packaging), System 6 (commander skill modifier), System 7 (EMCON modifier), System 8 (environmental modifier). If any of those systems is missing or returns bad data, auto-resolution produces wrong results.
- Shares state with: `MasterTimePulse` / `ManagerSubPulse` (the scheduler that decides when to call the resolution loop — must not run faster than the slowest of the input systems that feed it), `FleetDB` (fleet composition and state)
- Triggers: Casualty → `ShipCombatValueDB` recalculated for damaged ships. Fleet wiped or retreated → battle end event → subscribing systems notified (UI, event log, faction state). Battle end in a system with a colony → colony defense status updated.
- Economy connection: Ammo and fuel are consumed per-round. These are cargo items on ships (`CargoStorageDB` or equivalent). The auto-resolution engine must deduct them. If a ship runs out of ammo mid-battle, its DPS drops to zero. If a ship runs out of fuel, it cannot retreat. This is a significant connection to the logistics system — read `Logistics/CLAUDE.md` before building the consumption model.
- Production connection: Ships destroyed in auto-resolution are permanently gone. Ship production (Industry) is what replaces them. The industry system needs to know what the fleet lost. The result package should be written in a format the industry system can read for replacement priority queuing.

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

**Connections:**
- Feeds into: System 8 (debris field = new environmental zone), economy (salvaged materials return as raw resources), industry (salvaged components may be recoverable — partial tech unlock, or skip production cost)
- Fed by: System 9 (casualty list from auto-resolution), Tier 2 damage (`DamageProcessor.SpawnWreck()` hook at line 223 — currently empty), ship design (composition of the wreck depends on what the ship was made of)
- Shares state with: `PositionDB` (debris spawns at the destroyed ship's last known position), cargo system (salvage recovery puts materials into a ship's cargo hold), `MassVolumeDB` (wreck has a mass — it's a real object in the system)
- Triggers: Debris spawned → System 8 zone check (is the field large enough to affect sensor/navigation?). Salvage complete → cargo updated → industry system can use the materials.
- Technology connection: Salvaging enemy technology may be how you reverse-engineer components you haven't researched. The Tech/Research system needs a hook. Don't hardcode salvage as "raw materials only" — design the data structure to carry component type, so a future tech hook can read it.
- Orbital connection: Debris orbits. A debris field from a battle in orbit will drift along the orbital path, not stay stationary. Whether this level of fidelity is worth modeling depends on the time scales involved — at Aurora's typical timescales (months to years), an uncleared debris field in orbit is a persistent hazard. Check with the orbital system whether stationary objects in a star system are already handled or whether everything requires an orbit.

**What to build:**
- On ship destruction: spawn `DebrisFieldDB` at location with mass and material composition from the ship design
- Large debris fields become Environmental Hazards (System 8): reduce sensor range, require cautious navigation
- Salvage order: direct a ship to collect materials from a debris field (returns raw materials to cargo)
- `SpawnWreck()` in `DamageProcessor.cs` is the intended hook — currently empty (see line 223)

---

### System 11 — Ground Combat Interface

**What it is:** The bridge between space combat and planetary combat. Orbital bombardment, troop landing, and siege mechanics connect the two theaters. This is a Phase 4 task — it depends on all ten systems above being functional.

**Connections:**
- Feeds into: Colony system (bombardment damages installations and kills population), ground combat system (landed troops need a simulation to fight with), logistics (a sieged colony stops receiving supply convoys)
- Fed by: ALL ten space combat systems (orbital bombardment is just System 9 auto-resolution where one side is stationary and on the surface), fleet system (transport ships carry the ground units), colony system (`ColonyInfoDB`, `ComponentInstancesDB` for installation damage targets)
- Shares state with: `ColonyInfoDB` (population, atmosphere, installation count), `ComponentInstancesDB` on the colony entity (bombardment damages these), `SystemBodyInfoDB` (planet's gravity affects re-entry and descent, atmosphere affects weapons — read `Galaxy/CLAUDE.md`)
- Triggers: Successful landing → ground combat entity created on planet. Successful siege → colony resupply routes blocked → population growth slows, installations degrade. Successful bombardment → colony damage event → population and installation loss.
- Population connection: Bombardment kills people. Population math (Phase 2c, `PopulationProcessor.cs`) determines how many people are on the planet and what the minimum viable colony size is. A planet bombarded below that threshold becomes uninhabitable. These systems must agree on population units.
- The commented-out colony damage block in `DamageProcessor.cs` (~lines 101–181) is the original design intent for this system. Read it before building — it shows what types were expected (`ComponentInfoDB`, `ComponentInstanceData`) and which may have been renamed or removed.

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

## Open Defects — Status (updated 2026-06-21)

All three pre-System-4 defects are now fixed.

| Defect | Status | Fix Applied |
|--------|--------|-------------|
| Off-by-one: first component never targeted | ✅ Fixed | `DamageProcessor.cs`: `componentIdx = damage.id - 1` with `>= 0` guard in both damage loops |
| One-hit destroys all components | ✅ Fixed | `DamageProcessor.cs`: `HealthPercent -= damageAmount * 0.001f` (1000 points = 100% health) |
| Material table nearly empty | ✅ Fixed | `DamageResistBlueprint` constructor uses `[JsonProperty("UniqueID")]` to map the JSON key to the byte IDCode |

**Remaining known calibration issue (not a bug, a tuning decision):** Missile kinetic impact energy is orders of magnitude higher than beam weapon energy. When `MissileImpactProcessor` delivers kinetic damage through `DealDamageEnergyBeamSim`, ships will be instantly destroyed. Tune the energy divisor in `MissileImpactProcessor.ProcessEntity()` or add a warhead-energy lookup from `OrdnanceExplosivePayload.ExposiveTnTEQMass` once warhead energy values are finalized in `ordnanceDesigns/`.
