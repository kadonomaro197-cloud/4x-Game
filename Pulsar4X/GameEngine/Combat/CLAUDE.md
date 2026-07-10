# Combat — Subsystem Reference (Auto-Resolve Engine)

The **one** combat engine for this fork: a math loop that resolves fleet battles off each ship's "spec sheet," with **doctrine** as the player's only lever. Lives in `GameEngine/Combat/`. This is the v1 spine described in `docs/COMBAT-DESIGN.md` -> "What we're building (v1)".

It deliberately does **not** use the per-pixel damage sim (`Damage/DamageComplex` / `DamageVeryComplex`). That path deposits ~0 damage today (see `Damage/CLAUDE.md`) and is parked as a v2 visual skin. The auto-resolver decides casualties by **strength math**, not by simulating hits.

> **Status: under construction.** Built piece-by-piece, each under a test, in the order in `docs/COMBAT-DESIGN.md` -> "Build order". This file grows as each piece lands.

> **DECIDED 2026-07-06 — this resolver becomes THE resolver (ship AND planetary).** The developer's call: *"there is no ground tactics, just tactics; there is one weapon triangle; why a separate resolver?"* Today `GroundForcesProcessor.ResolveRegionCombat` DUPLICATES this salvo/triangle/dodge/shield/armour math over data-object `GroundUnit`s (only because they aren't entities). The next combat branch **extracts the shared damage math onto a neutral COMBATANT view** both a ship entity and a planetary unit present, routes both through it, and deletes the ground duplicate. Planetary combat then contributes terrain + **absolute metric range** (a 100 km weapon is 100 km on a surface too; hexes are just the board — `GroundRangeTools.RealReachKm`) + the **air/altitude layer**; the Armor▸Infantry▸Artillery type-triangle DISSOLVES into weapon×armour matchups. It's a rewrite of the only green combat code — do it as additive CI-gated slices (kernel first, no behaviour change), with the developer's go. Full plan: `docs/RESOLVER-MERGE-DESIGN.md` (+ `docs/WEAPON-UNIFICATION-DESIGN.md` §0 pt 6).
>
> **Slices 1–2 ✅ LANDED 2026-07-08 — `CombatKernel.cs` (shared kernel; ship side wired).** The shared home for the pure salvo math now exists AND the ship resolver runs on it. Slice 1 added the neutral `CombatKernel.Combatant` view (value fields + `WeaponProfile`s + a 1-D `Position_m`, no hex/`Entity`) plus the pure functions `HitFraction` / `LandedFraction` / `SoakFractionOf` / `ResolveShield` / `ShieldSoakFraction` / `ArmourSoak`. Slice 2 **routed the ship side through it**: `CombatEngagement`'s `HitFraction` / `LandedFraction` / `SoakFractionOf` / `ResolveShield` / `ShieldSoakFraction` are now thin `=> CombatKernel.X(...)` delegators, its dodge/shield tuning constants forward to the kernel's, and `RangeBaseMiss` is a forwarding property — so there is ONE definition of the ship dodge/shield arithmetic and no drift is possible. Byte-identical by construction (delegation to identical code); the ship combat fixtures (CombatPerformance / Dodge / Shield / Triangle / Stress / BattleSims) are the tripwire. **Next — slice 3:** give a `GroundUnit` a `Combatant` view and route `ResolveRegionCombat` through the kernel (this is where the ground triangle dissolves into weapon×armour and `ArmourSoak`'s duplication collapses); the ground behaviour change is the deliberate one, re-baselined with a written reason.

---

## File Map

| File | Purpose | Status |
|------|---------|--------|
| `CombatKernel.cs` | **NEW (resolver merge, slices 1–2, 2026-07-08)** The shared, domain-neutral salvo KERNEL both a ship battle and a planetary battle run through. `CombatKernel.Combatant` = the neutral view (FactionId · Health/MaxHealth · Evasion · Armour · Shield pool/capacity/regen · `List<WeaponProfile>` Weapons · 1-D `Position_m`) — no hex, no `Entity`. Pure static math: `HitFraction`, `LandedFraction`, `SoakFractionOf`, `ResolveShield`, `ShieldSoakFraction` + the ground `ArmourSoak`. **SHIP side WIRED as of slice 2** — `CombatEngagement`'s same-named helpers are now thin delegators to this class and its tuning constants forward here, so the kernel is the single source of truth for the ship dodge/shield math (byte-identical; the ship fixtures are the tripwire). `ArmourSoak` is the shared flat-armour math for ship + ground (slice 3a). **+ PENETRATION (Weapons pilot W1a, 2026-07-10):** a 3-arg overload `ArmourSoak(armour, sourceDamage, penetration)` — penetration cancels armour point-for-point BEFORE the flat soak (an AP round with penetration ≥ armour lands in full; a normal round, penetration 0, is byte-for-byte the old 2-arg soak, which now forwards to it). Clamped so penetration can't make armour stronger. Byte-identical this slice (nothing passes penetration > 0 yet; the ground resolver wires it in W1b). **+ PER-SHOT-ENERGY BURST SOAK (W2a, 2026-07-10):** `BurstShotCount(w)` (= dps÷`WeaponProfile.PerShotEnergy`, clamped [1, `BurstSoakMaxShots` 1000]) + `ArmourSoakBurst(armour, dmg, shotCount, penetration)` split a source into N equal shots and soak each flat — the alpha-vs-chip identity (one big shot punches, a swarm bounces). shotCount ≤ 1 forwards to the flat soak (byte-identical); W2b routes the ground resolver through it. Gauge `CombatKernelTests`. Design: `docs/RESOLVER-MERGE-DESIGN.md` + `docs/COMPONENT-DESIGNER-DIALS.md` ⚙1. | ✅ slices 1–2 (ship wired) + Penetration W1a + PerShotEnergy W2a |
| `ShipCombatValueDB.cs` | DataBlob: a ship's **Firepower** (joules/sec from beams + **railguns** + **flak** + a missile-launcher stub), **Toughness** (live components + armour), **RoleWeight**, **Evasion** (size + agility), and a **`Weapons`** list of per-weapon flavor profiles. Computed once at build. `Calculate(Entity)` + `CalculateEvasion(Entity)`. **+ `AmmoCapacity_kg` (W3 — sum of `ShipMagazineAtb`, seeds the fleet ammo pool). + RECOIL→tracking (W4 — a kinetic weapon's `Recoil` reduces its built `WeaponProfile.Tracking` by `RecoilTrackingFactor(recoil, chassisMass)` = `mass/(mass+recoil)`; a heavy gun on a light hull tracks worse. Recoil 0 on every base-mod weapon → byte-identical; gauge `ShipRecoilTests`).** | ✅ built (spine step 2; +Evasion/Weapons; +railgun P3, +flak P4; +AmmoCapacity W3; +Recoil W4) |
| `WeaponProfile.cs` | The per-weapon flavor: `WeaponClass` enum (Beam/Railgun/Missile/Flak) + `WeaponProfile` (damage/velocity/tracking/saturation/**`Range_m`**). **+ TWO-AXIS foundation (2026-07-06, `docs/WEAPON-TAXONOMY-DESIGN.md`):** `WeaponNature` (Kinetic/Energy/Explosive/Exotic — meets the defence) × `WeaponDelivery` (Beam/Bolt/Slug/Cloud/Guided/Blast — meets the dodge) as separate `WeaponProfile.Nature`/`.Delivery` fields; the fused `WeaponClass` is transitional and will become a COMPUTED readout of the specs (so a build can sit between corners; a blaster = Energy+dodgeable-Bolt becomes expressible). Additive — `WeaponClass` + all combat is byte-identical this slice; base-mod beam/railgun/flak/missile tagged Energy·Beam / Kinetic·Slug / Kinetic·Cloud / Explosive·Guided. Gauge `WeaponAxesTests`. The breakdown the dodge model + weapon triangle read. **+`Range_m` (Root A, 2026-06-27)** — how far the weapon reaches (0 = unbounded, the beam `IsInRange` convention). **Authentic-closing ranges (2026-06-27):** beams carry their design `MaxRange` (~knife range); **flak = short** (`FlakRange_m` 50 km, point-defense hard cutoff); **missile = long** (`MissileRange_m` 1000 km, the standoff opener); **railgun = MID** (`RailgunRange_m` 500 km, between flak and missile — **finite as of 2026-06-28**, was 0/unbounded). That gives the closing fight its range LAYERING: missile (1000 km) → railgun (500 km) → flak (50 km) → beam (knife). **Why railgun went finite:** rangeless (0) meant `MaxReach` returned `PositiveInfinity` → a railgun fleet was "IN RANGE" at ANY gap, so it fired across the whole 1 Gm engagement bubble — the live "ships firing at each other outside their detection range" report (2026-06-28). A finite range makes the closing model hold its fire until the gap is within 500 km (FAR inside any sensor reach), so a slug only lands on a target the ship has closed with. Accuracy still falls off with distance within that range (the `HitFraction` range term). Flak/missile/railgun ranges are v1 class-defaults in `ShipCombatValueDB`; per-design (paid-for) range fields are the follow-up. **Tests stay green** because the range gate only bites when `EnableClosingRange` is on (live client) — with it off (every headless fixture) `SeparationOf` returns 0, so the gate is a no-op (`RailgunWeaponTests` asserts the finite range; the resolve fixtures are unchanged). The data the closing-fight model reads (`docs/FLEET-COMBAT-CLOSING-DESIGN.md`). **+`Penetration` (Weapons pilot W1a, 2026-07-10)** — the armour-crack dial (how much of the target's flat armour the shot IGNORES); 0 = a normal round, high = an AP/sabot/lance cracker. Read by the kernel's 3-arg `ArmourSoak`. **+`PerShotEnergy` (Weapons pilot W2a, 2026-07-10)** — the alpha-vs-chip dial (joules in one shot); the kernel's `BurstShotCount` (= dps÷PerShotEnergy) + `ArmourSoakBurst` split a source into that many flat-soaked shots, so a swarm of chips bounces off plate while one alpha of equal total punches through. Both serialized + carried through both ctors + the copy-ctor; **additive & byte-identical** (default 0 → single lump, and nothing calls the burst soak from live code yet — W2b wires the ground resolver). `⚙1` resolver backlog #1/#2 (`docs/COMPONENT-DESIGNER-DIALS.md`). | ✅ built (depth pass P2; +Range_m Root A; +authentic ranges; +Penetration W1a; +PerShotEnergy W2a) |
| `FleetCombat.cs` | **Root B (2026-06-27): fleet capability aggregation** — pure read-models, no behaviour change. `WarpSpeedFloor`/`DeltaVFloor` (min over ships — the fleet moves as one, bound by its slowest/shortest-legged), `FirepowerAtRange(R)` (the firepower-vs-range curve: sum of weapons whose `Range_m` reaches R), `SensorReach` (max over ships — sensors run parallel, envelope = max not sum), `Ships(fleet)` (tree walk, recursing components). The numbers the closing resolve (Phase 1+) + the battle readout will read. | ✅ built (closing Root B) |
| `AutoResolve.cs` | The salvo-exchange resolver: `AutoResolve.Resolve(sideA, sideB, config)` runs the math loop (strength → damage pools → whole-ship casualties, combatants first) until one side is gone, both are, or a frozen fight hits the round cap. Returns an `AutoResolveResult` (outcome + casualty lists); **pure** — it reports casualties, it does not destroy them. Plus `AutoResolveConfig`, `BattleOutcome`. | ✅ built (spine step 3) |
| `FleetCombatStateDB.cs` | DataBlob marking a fleet as **engaged** (a *representative* opponent id for readout, accumulated damage pool, steps fought, starting ship count for the retreat threshold). On **every** fleet in a battle (state is per-fleet); removed from a fleet when it leaves. Its presence is the "in combat" flag the engagement lock (step 11) keys on. | ✅ built (spine steps 4, 7; multi-party) |
| `CombatEngagement.cs` | The trigger's engine logic: `Tick(manager, dt)` engages/JOINS hostile fleets in range, then steps the **multi-party** engagement (every in-combat fleet in the system, sides = factions) incrementally over game-time. `StepEngagementGroup(members, dt)` is the resolver; `StepEngagement(a,b,dt)` is its n=2 special case; `EnsureInCombat` is the join primitive. `GetCombatShips` tags ships with their **component's** doctrine mults (step 6); `GetFleetShips` is the flat list (counts/detection). Hostility/range/sides are v1 stubs. Testable directly. | ✅ built (spine steps 4, 6; multi-party) |
| `BattleTriggerProcessor.cs` | `IHotloopProcessor` (every 5 s) that calls `CombatEngagement.Tick` per star system. Keyed to `StarInfoDB` (FleetDB is already taken). | ✅ built (spine step 4) |
| `FleetDoctrineDB.cs` | DataBlob: a fleet's **active** combat posture (doctrine id, firepower/toughness/speed multipliers, retreat flag, switch-cooldown clock). Read by the engagement as a strength/toughness modifier. | ✅ built (spine step 5) |
| `FleetDoctrine.cs` | Helpers: `FirepowerMult`/`ToughnessMult`/`IsRetreat(fleet)` reads; `TrySetDoctrine(fleet, blueprint, now)` sets a posture from the catalog, honouring the cooldown. | ✅ built (spine step 5) |
| `CombatDoctrineBlueprint` (`Engine/Blueprints/`) | The moddable **catalog** of postures (JSON → `ModDataStore.CombatDoctrines`): family, display name, the multipliers, cooldown, retreat flag. | ✅ built (spine step 5) |
| `FleetRetreatDB.cs` | DataBlob recording that a fleet **broke off** (flag + a withdraw vector away from the enemy + who it fled from). Attached when a fleet retreats; persists after the engagement ends (so the outcome stays visible). v1 records the vector only — no move order. | ✅ built (spine step 7) |
| `CombatSandbox.cs` | Dev/test utility: `SpawnHostileFleet(...)` stands up a **registered hostile faction + fleet + ships** at a body so the trigger auto-engages them. **+ premade scenario (2026-06-27; multi-faction + beefed 2026-07-03):** `SpawnCombatScenario(game, system, playerFaction)` = 2 well-rounded PLAYER task forces at Earth + a **beefed-up HOSTILE squadron, each its OWN rival faction**, at **Luna/Venus/Mercury/Mars** (Luna is inside auto-engage range → instant fight; the rest are sail-to closing targets). **FOUR distinct enemy factions** (Lunar Free State / Venusian Compact / Mercury Combine / Martian Directorate — the developer's "make them different factions"), so it also exercises multi-faction combat / IFF; returns `List<Entity>` of the factions. `WellRoundedDesignSet` = beam+railgun+flak+2 fighters (the PLAYER set — range spread = rich closing/dodge data); `HostileSquadronSet` = **capital-led beef** (Leviathan + 2 Aegis + Lancer + Bulwark + 2 Wasps = 7 ships); `SetupHostileFaction` = the shared "persist like a real NPC" recipe (CreateBasicFaction + KnownSystems + copy ShipDesigns); `SpawnMixedFleet` builds one fuelled+charged mixed fleet; `FindBody(system, name)` finds a body by default name. DevTools "Spawn Combat Scenario" button (+ "Spawn Hostile Fleet" now rotates distinct faction names) + `CombatScenarioTests`. Lives in the ENGINE so it's CI-verified. **Now runs on EVERY New Game / Quickstart by default (2026-07-03)** — `NewGameMenu.CreateGameCore` calls `SpawnCombatScenario` before `PostNewGameInitialization` when `NewGameMenu.AutoSpawnCombatScenario` is true (the developer's "make the button happen on default"); wrapped in try/catch so a spawn hiccup never breaks New Game, and toggleable in DevTools (affects the NEXT new game). Enemies sit at other bodies, so nothing auto-engages on spawn — you close to fight. **+ Mars BEACHHEAD (2026-07-06, gated OFF):** `SetupGroundBeachhead(game, faction, species, body, pop)` gives the Martian Directorate a minimal, capturable COLONY + a defending ground GARRISON on Mars (reusing the player's species — NPC "human" colony; pop flagged 500M), so Mars is a world you can invade and TAKE, not just a fleet to shoot. It then RE-FOGS Mars (`ColonyFactory.CreateColony` reveals the world at colony creation — survey is world-level v1; re-fogging keeps the "only your home starts surveyed" rule so the player must still send a survey ship). **Gated behind `CombatSandbox.SpawnMarsBeachhead` (default FALSE)** — the developer's "hold off on the Earth-Mars war" call: a normal New Game has the fleet-only Mars rival, no ground war; flip the flag on (DevTools/test) to stage the take-Mars playtest. The work stays available, off by default. The other rivals stay fleet-only. Gauge: `MarsBeachheadTests` (flips the flag on, asserts enemy colony + garrison rival-owned + Mars re-fogged). | ✅ built (combat-test enabler + Mars beachhead, off by default) |

*Fleet components (step 6) reuse `FleetDB` sub-fleets + `FleetDoctrineDB` — no new file; see "Fleet components" below. (Retreat, step 7, adds rows as it lands.)*

| `ShieldAtb.cs` | **NEW (space SHIELD layer, 2026-07-06)** The shield-generator **component** (`IComponentDesignAttribute`; `Capacity_J` pool + `RegenRate_Jps`, double-arg NCalc ctor L7) — the space "shield" on the defence axis (`docs/UNIVERSAL-ASSEMBLY-DESIGN.md` §2b), a depleting/regen POOL (option B). `ShipCombatValueDB.Calculate` sums installed generators (health-scaled) into `ShieldCapacity_J`/`ShieldRegen_Jps` — **0 if none → additive, combat byte-identical**. Cradle-to-grave (a shot-off generator drops the shield). **Phase A = the component + the combat-value hook. Phase B (below) = the resolver wiring.** Gauge `ShieldTests`. | ✅ A + B |
| `ShipMagazineAtb.cs` | **NEW (Weapons pilot W3a, 2026-07-10)** The ship AMMO MAGAZINE **component** (`IComponentDesignAttribute`; `Capacity_kg`, double-arg NCalc ctor L7) — the SPACE echo of the ground `GroundMagazineAtb`, mid-battle ammo depletion for the ship resolver (the item ground had and space lacked). `ShipCombatValueDB.Calculate` sums installed magazines (health-scaled) into **`AmmoCapacity_kg`** — **0 if none → additive, combat byte-identical**. Seeds the fleet's `FleetCombatStateDB.AmmoPool_kg` (-1 unseeded, lazy-fill, mirroring the shield pool). Cradle-to-grave (a shot-off magazine feeds less). **W3a = component + combat-value hook + pool field (byte-identical). W3b ✅ = the drain/silence in `StepEngagementGroup`** (an AMMO pass before the damage phase: a fleet's ammo-fed = Kinetic/Explosive fire drains `AmmoPool_kg` by `(ammo J/s × dt) × AmmoBurnKgPerJoule`; dry → `SilenceAmmoWeapons` drops those profiles, energy weapons fight on; gated on capacity>0 so magazine-less fleets are byte-identical). **W3c ✅ = the buildable base-mod magazine** — the `ship-magazine` template (Ammo-Capacity dial → `ShipMagazineAtb`, six-point registration) mounted on a NEW example ship, the **Sabre Munitions Cruiser** (railguns + a 5000 kg magazine on a heavy hull; a new ship like the shield's Bastion, so no battle fixture is perturbed). Gauge `ShipAmmoTests` (the Sabre's magazine binds from JSON — the gotcha-10 sensor). Follow-up: in-combat ship resupply. | ✅ W3a + W3b + W3c |
| `RadiatorAtb.cs` | **NEW (Weapons pilot W5a, 2026-07-10)** The HEAT RADIATOR **component** (`IComponentDesignAttribute`; `Capacity_kJ`, double-arg NCalc ctor L7) — the heat twin of the ammo magazine (magazine limits KINETIC sustained fire; radiator limits ENERGY sustained fire). `ShipCombatValueDB.Calculate` sums installed radiators (health-scaled) into **`HeatCapacity_kJ`** — **0 if none → additive, byte-identical**. Sets the fleet's heat ceiling + cooling; `FleetCombatStateDB.HeatPool_kJ` (starts cold) accumulates energy-fire heat and throttles the energy guns over capacity. **W5a = component + combat-value hook + pool field (byte-identical). W5b = the heat accumulate/throttle in `StepEngagementGroup`; W5c = the buildable base-mod radiator on a beam-heavy ship.** Gauge `ShipHeatTests`. | ✅ W5a |
| **Base-mod Deflector Array + Bastion ship** (`weapons.json` `deflector-array` + `componentDesigns.json` + `earth.json` + `shipDesigns.json` `default-ship-design-test-shielded`) | **NEW (space SHIELD layer, Phase C, 2026-07-06)** A player-buildable ship shield generator — the **Deflector Array** (`deflector-array` `ComponentType: Defense`, `MountType: ShipComponent,ShipCargo,PlanetInstallation`, dials `Shield Capacity`/`Recharge Rate`, `AtbConstrArgs(...) → Pulsar4X.Combat.ShieldAtb`) via the **six-point registration** (gotcha #10: template + `default-design-deflector-array` + `earth.json` StartingItems + ComponentDesigns; materials reuse stocked stainless-steel/aluminium/copper). Plus a shielded example ship, the **Bastion Shielded Cruiser** (3 lasers + 2 deflectors — the energy/shield archetype). Closes cradle-to-grave. Gauge: `ShieldBaseModTests` (JSON→ShieldAtb→ShipCombatValueDB through the real build path; unshielded Aegis reads 0) + `BaseModIntegrityTests`. **Flagged new numbers** (JSON defaults): Capacity 5 MJ, Recharge 100 kJ/s, mass/cost coefficients. **+ fleet readout (2026-07-06):** `FleetCombat.ShieldCapacity(fleet)` / `ShieldRegen(fleet)` sum a fleet's deflector pools (the defensive twin of `FirepowerAtRange` — what the Fleet Combat tab shows as "Shields"; sums, matching the per-fleet pooling in the resolve). Gauge in `ShieldBaseModTests`. | ✅ Phase C |
| **Ion Disruptor — anti-shield exotic** (`Weapons/WeaponDisruptor/DisruptorWeaponAtb.cs`; `ShipCombatValueDB` read + `LightSpeed_mps`/`DisruptorRange_m`; `disruptor-weapon` + `default-ship-design-test-disruptor`) | **NEW (space SHIELD layer, Phase D, 2026-07-06)** The weapon a shield can't stop. A base-mod **Ion Disruptor** (six-point registration) that `ShipCombatValueDB.Calculate` reads into a **light-speed (undodgeable), `WeaponNature.Exotic`** `WeaponProfile` — so the Phase-B shield's exotic-soak (0) makes it BYPASS the pool and hit the hull. Modest raw yield (not a better beam); its value is the matchup. Example ship: the **Ravager Ion Frigate**. Closes the anti-shield exotic cradle-to-grave. Gauge: `DisruptorWeaponTests` (JSON→atb→exotic profile; end-to-end a shielded hull takes the same disruptor fire as unshielded, kinetic is soaked) + `BaseModIntegrityTests`. **Flagged numbers**: Energy/Shot 150 kJ, RoF 2/s, `DisruptorRange_m` 400 km. | ✅ Phase D |
| **Shield in the resolve** (`CombatEngagement.cs` — `ApplyShield`/`ResolveShield`/`SoakFractionOf`/`FleetShield`, `FleetCombatStateDB.ShieldPool_J`) | **NEW (space SHIELD layer, Phase B, 2026-07-06)** The pool is now WIRED into the live stepped resolve. Each salvo, a fleet's aggregate shield pool (`FleetCombatStateDB.ShieldPool_J`, lazy-seeded to full capacity at first contact) soaks the **soakable** part of the incoming salvo — the **nature matchup** mirrored from ground `GroundDamageMatrix`: `ShieldSoakVsKinetic 1.0` / `VsEnergy 0.5` (bleeds) / `VsExplosive 0.75` / `VsExotic 0.0` (anti-shield bypass) — draining before the hull's `DamageTakenPool`, then regenerating toward capacity for the next volley (narrated "shields at X% … DOWN!" when `NarrateToLog`). To keep the matchup alive through the O(ships) class-bucket aggregation, `BuildFireMix`/`AddScaledFire` now bucket+carry **Nature**. **ADDITIVE**: the exact aggregate salvo damage is preserved and the shield only ever SUBTRACTS what it absorbs, so an unshielded fleet (0 capacity) is byte-identical. Gauges: `ShieldTests` (pure soak-fraction + drain/regen math, and end-to-end — a shielded hull takes less kinetic fire; an exotic attacker bypasses). **v1 flags:** one aggregate pool per fleet (not per-ship); regen only on salvos under fire; capacity not doctrine-scaled; all four soak fractions are balance-pass defaults. Next: base-mod generator + shielded example ship (Phase C), anti-shield exotic weapon + Dune lasgun interaction (Phase D). | ✅ Phase B |

---

## ShipCombatValueDB — the spec sheet

**What it is.** Two numbers that rate a ship for the auto-resolver, read from the ship's REAL parts the moment it's built and cached on the entity:

- **`Firepower`** — hurt-per-second. Each beam weapon contributes `Energy ÷ ChargePeriod` (joules/sec), scaled by that component's `HealthPercent`. Each missile launcher adds a flat `MissileLauncherFirepowerStub` (v1 stub).
- **`Toughness`** — how much it can take, **in joules absorbed**. Each live component contributes `HealthPercent × ComponentHitPoints_J` (1e5 J kills a component — straight from the damage tuning: 1000 dmg-points × 100 J), plus `armour.thickness × ArmorHitPointsPerThickness_J`. Same currency as `Firepower × time`, so the salvo loop's time-to-kill comes out in seconds.
- **`RoleWeight`** — `1.0` for anything that can shoot, `UtilityRoleWeight` (0.25) for a utility hull. The auto-resolver uses it so utility/transport ships are low-priority targets (absorb casualties last) and contribute less strength. v1 stub.
- **`Evasion`** — how hard the ship is to **hit** (0 = a sitting brick, capped at `EvasionCap` 0.95 = a nimble fighter), from `CalculateEvasion`: size (small = hard to hit, via `MassVolumeDB.Volume_m3`) × agility (acceleration = `NewtonThrustAbilityDB.ThrustInNewtons ÷ MassDry`, the *rate it changes vector*). Distinct from Toughness — toughness soaks what lands, evasion is not getting hit, and (unlike toughness) it depends on the **weapon** (you can't dodge a beam). A ship with no engine can't dodge (evasion 0). This is the input the dodge model uses; v1 stub leaves sensors + crew experience out (flagged for v2).
- **`Weapons`** (`List<WeaponProfile>`) — each weapon's flavor: `Class` (Beam/Railgun/Missile/Flak), `DamagePerSecond`, `Velocity` (m/s — beam ≈ light-speed), `Tracking` (0..1), `Saturation` (tracks/sec, = rate-of-fire). `Firepower` is the SUM of these profiles' damage (so the old number is unchanged), but the per-weapon breakdown is what the dodge model + weapon triangle read in the resolve. Beams are read real (`BeamSpeed`/`Energy`/`ChargePeriod`/`BaseHitChance`); **railguns** are read real from `RailgunWeaponAtb` (`MuzzleVelocity_mps`/`KineticEnergyPerShot_J`/`RoundsPerSecond`/`Tracking` → finite-velocity ballistic kinetic; dps = energy×rof, saturation = rof — so it's dodgeable, unlike a beam); **flak** is read real from `FlakWeaponAtb` (`MuzzleVelocity_mps`/`DamagePerPellet_J`/`RoundsPerSecond`/`PelletsPerShot`/`Tracking` → high saturation = rof×pellets, low per-pellet damage; the saturation FLOORS the dodge → the fighter/missile killer); missiles are a v1 stub. See `WeaponProfile.cs` + `docs/WEAPONS-AND-DODGE-DESIGN.md`. **Two-axis (2026-07-06):** a profile also carries `Nature` (Kinetic/Energy/Explosive/Exotic — what meets the shield/armour) and `Delivery` (Beam/Bolt/Slug/Cloud/Guided/Blast — what meets the dodge). The `Class` above is now a **COMPUTED read-out** (2026-07-06, developer-approved): `WeaponProfile.Class => WeaponClassifier.Classify(Delivery, Velocity, Tracking, Saturation)` — no longer an authored/serialized field (the dials win; the type emerges from Nature × Delivery + specs). **The type slot is GONE**: the ctor takes NO class arg — `new WeaponProfile(dps, vel, trk, sat, range, nature, delivery)` — you set the dials and the corner falls out. `.Class` is only read in `BuildFireMix`'s bucket key + the readout; proven byte-identical (real weapons compute to their expected corner; the lone mixed fixture B05 computes cleanly; deliberately-contradictory stress weapons are bucket-inert). See `docs/WEAPON-TAXONOMY-DESIGN.md`.

**Where it's computed.** `ShipFactory.CreateShip()` calls `ship.SetDataBlob(ShipCombatValueDB.Calculate(ship))` after the components are installed. `Calculate` is defensive — a part-less ship rates 0/0 and never throws.

**Prime Directive — connections:**
- **Feeds IN:** `ComponentInstancesDB.AllComponents` (live components + `HealthPercent`); `GenericBeamWeaponAtb` (`Energy`, `ChargePeriod`) via `TryGetComponentsByAttribute`; `MissileLauncherAtb` (presence only, v1); `EntityDamageProfileDB.Armor.thickness`.
- **Feeds OUT:** the auto-resolve loop (spine step 3) sums `Firepower`/`Toughness`/`RoleWeight` over a fleet's ships to get fleet strength. Nothing else reads it yet.
- **Shares STATE:** lives on the ship entity alongside `ComponentInstancesDB` and `EntityDamageProfileDB` (reads them; does not write them).
- **Triggers:** nothing — it's a passive cached value.

**Test:** `Pulsar4X.Tests/ShipCombatValueTests.cs` — builds every starting design, asserts each gets a `ShipCombatValueDB` with toughness > 0, logs firepower (`[combat-value]`), and asserts firepower > 0 for any design carrying a beam weapon.

---

## AutoResolve — the salvo loop

**What it is.** `AutoResolve.Resolve(IList<Entity> sideA, IList<Entity> sideB, AutoResolveConfig)` fights two flat lists of ships and returns an `AutoResolveResult`. Per round:

1. Each side's strength = Σ `Firepower` of its surviving ships (later × doctrine/commander/range — stubbed at ×1 for now).
2. Each side adds `strength × RoundSeconds` joules to the **other** side's damage pool.
3. The pool removes WHOLE ships, **combatants first** (highest `RoleWeight`), then utility hulls. Leftover damage stays in the pool for next round, so a weaker fleet still grinds kills over time.
4. Repeat until one side is empty (victory), both empty (mutual destruction), neither can deal damage, or `MaxRounds` (stalemate).

**Pure by design.** It does the math and *reports* casualties (`DestroyedA`/`DestroyedB` entity lists) — it does **not** destroy entities, advance the clock, RNG, or touch the per-pixel damage sim. The battle trigger (step 4) flattens fleets into ship lists, calls `Resolve`, then destroys the reported casualties.

**Why joules.** `Firepower` is J/s and `Toughness` is J (see `ShipCombatValueDB`), so `Firepower × RoundSeconds` is joules and subtracts cleanly from toughness — time-to-kill is in seconds.

**Connections (Prime Directive):**
- **Feeds IN:** `ShipCombatValueDB` per ship (falls back to `Calculate` if a ship somehow lacks one).
- **Feeds OUT:** `AutoResolveResult` → the battle trigger destroys casualties, applies retreat (step 7), writes the event log.
- **Triggers:** nothing itself — pure function. The *caller* destroys ships.

**Test:** `Pulsar4X.Tests/AutoResolveTests.cs` — stronger fleet wins & wipes the weaker; zero-firepower = stalemate; combatants die before utility hulls. Deterministic (ships stamped with known combat values).

**Not yet (later steps):** doctrine/commander/range multipliers on strength (steps 4–6); retreat threshold ending a side early with a vector (step 7); `FleetCombatStateDB` to mark fleets "engaged" and optionally spread rounds across game-time (steps 4 / 11).

---

## The battle trigger — hostile fleets auto-engage

**What it is.** `CombatEngagement.Tick(manager, dt)` is run every ~5 s of game-time per star system by `BattleTriggerProcessor`. It:

1. **Engages / joins.** Any two hostile fleets in range are both put "in combat" — `EnsureInCombat` attaches `FleetCombatStateDB` to each (this is also what trips the engagement lock, step 11). This is *also* the JOIN path: a fleet arriving in range of an enemy gains combat state here, on its faction's side — there is no separate "reinforce" call (see "Multi-party engagements" below).
2. **Steps** the engagement forward by `dt` game-seconds via `StepEngagementGroup`: every in-combat fleet in the system fights at once (sides = factions); each fleet adds `strength × dt` joules to the pools of the fleets hostile to it and loses whole ships (combatants first) — the same math as `AutoResolve`, but spread across game-time so a battle plays out instead of resolving instantly.
3. **Releases** each fleet as it drops out (wiped, breaks off, or no enemy left), and ends the whole engagement when fewer than two hostile sides remain (or it stalls) — removing `FleetCombatStateDB` (clearing the lock).

Battles spanning game-time is what makes "watch a battle / change doctrine mid-fight / orders freeze while engaged" real — instant resolution would give none of that.

**v1 stubs (flagged):**
- **Hostility** = different non-neutral faction. There is **no diplomacy/relations system** in the engine yet, and the engine's own `EntityFilter.Hostile` additionally requires a *sensor contact* — which the v1 plan stubs as "everyone sees everyone." So the trigger ignores sensors and treats any two different-faction fleets as enemies. Real IFF/relations is a v2 layer.
- **Range** = a flat `EngagementRange_m` (1 million km) as the *coarse* pre-gate, then the **real weapon-range gate** `WithinWeaponRange` when **`RequireWeaponRangeToEngage`** is on (default off; **client on** as of 2026-07-02). So a battle auto-starts only when someone's guns can actually reach — the developer's "they can see each other all they want but won't fire until in weapons range." Seeing (detection) ≠ firing (weapon range). An explicit `OrderAttack` still bypasses. Paired with `RequireWeaponsReleaseToEngage` (now **also client-on**): "in weapon range AND at least one side Weapons Free." See "Weapon-range trigger" below.
- **Casualties** use `Entity.Destroy()` (lightweight: flips `IsValid` false at once, no order re-entrancy). Commander death, debris, and fleet-roster cleanup are v2.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB.Children` (a fleet's ships, recursing into sub-fleets); `ShipCombatValueDB` per ship; `PositionDB` (a fleet's position = its first ship's position — a fleet entity has none of its own).
- **Feeds OUT:** destroys casualty ships; sets/clears `FleetCombatStateDB` (read by the engagement lock, step 11).
- **Triggers:** ship destruction — this is the one combat piece with *side effects on the live game*.

**Behaviour change to be aware of:** any two hostile fleets that get within range now **fight automatically** — including in an existing save the moment it loads. That's the feature, but it is new behaviour. In a single-faction game nothing happens (no hostile pairs).

**Test:** `Pulsar4X.Tests/BattleTriggerTests.cs` — hostile fleets in range auto-engage and the weaker is wiped (state cleared); same-faction fleets never engage. Integration tests (advance the clock, let the real processor run) so they also prove the live-loop hook doesn't throw.

---

## Multi-party engagements — any number of fleets, either side, any time

**What it is.** A battle is **not** locked to two fleets. Any number of fleets fight at once, and a fleet can **join a fight already underway** just by coming into range — the developer's "all combat can be multi party at anytime… I can send in another fleet to assist." `StepEngagementGroup(List<Entity> members, dt)` is the resolver; the old two-fleet `StepEngagement(a, b, dt)` is just its **n = 2 special case** (it calls the group resolver with `[a, b]`), so a 1-v-1 and a 10-fleet melee run the **same** code.

**The model (one step):**
1. **Snapshot** every in-combat fleet's ships, outgoing fire mix (`BuildFireMix`), and **enemy set** (the in-combat fleets hostile to it, with ships) — *before* any casualties, so the exchange is simultaneous.
2. **Damage.** Each fleet takes the **combined** fire of all fleets hostile to it. An attacker facing several enemy fleets **divides** its fire across them (`1 / enemyCount` to each) — outnumbering a side doesn't multiply your guns (firepower is conserved). Within a target fleet, the bucketed dodge resolve still concentrates on the most-hittable ships.
3. **Resolution.** A fleet drops out (its `FleetCombatStateDB` is removed) when it is **wiped**, **breaks off** (retreat), or has **no enemy left**. When fewer than two mutually-hostile fleets remain (battle decided), or the fight is frozen / timed out, everyone still in is released.

**Sides = factions (v1 stub).** Two fleets are on the same side iff same faction; different non-neutral factions are hostile. There is no alliance/diplomacy model yet (v2). Because hostility is computed per-pair, a true 3-way free-for-all already resolves (each fleet simply has enemies on more than one "side") — it's only the *fire-division* and *side* heuristics that are first-pass.

**One system = one battlefield (v1 stub).** `Tick` runs per star system, and every in-combat fleet in that manager is in the **same** engagement (v1 range ≈ whole system). Distinct simultaneous battles in one system — clustering by real weapon range — is a v2 layer; `InRange` today only gates *joining*.

**Joining is idempotent.** `EnsureInCombat(fleet, repId)` only attaches state if the fleet doesn't already have it, so a reinforcement that's been in the fight for many ticks keeps its accumulated damage pool / step count — joining never resets an in-progress fight.

**Reduces exactly to the old behaviour for n = 2** (verified against every existing combat fixture — `BattleTrigger`, `Retreat`, `Performance`, `Dodge` all drive the 2-fleet path and stay green): one enemy → no fire division, one pool each, both released when the loser is wiped/retreats.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB` (membership = all in-combat fleets in the manager); per-fleet `FleetCombatStateDB`; `GetCombatShips` (per-component doctrine); `AreHostile` (sides).
- **Feeds OUT:** destroys casualty ships; sets/clears `FleetCombatStateDB` per fleet (engagement lock); records `FleetRetreatDB` on a fleet that breaks off.
- **Triggers:** ship destruction and engagement end — same side effects as the two-fleet trigger, now per fleet.

**Test:** `Pulsar4X.Tests/MultiPartyEngagementTests.cs` — **assist** (two fleets ganging up beat a lone equal enemy), **same-faction side** (a friendly reinforcement shares a side — no friendly fire, both disengage cleanly when the enemy dies; sized so an ally would die if friendly fire ever regressed in), **join** (a reinforcement pulled in mid-battle through the real `Tick` tips a 1-v-1 it then wins), **fire-split** (one fleet vs two enemies divides its fire — can't kill both in the step it could kill one, but the fire reaches both).

---

## Order a fleet to ATTACK (player agency — `OrderAttack`, added 2026-06-27)

The auto-trigger fights hostile fleets that are detected + in range + Weapons Free. But once a fight ends, two
fleets can sit in range doing nothing — one **holding fire**, or an enemy that **broke off** (carries
`FleetRetreatDB`, which `Tick`'s engage pass deliberately skips so it doesn't thrash). The developer's "they were
just staring at each other menacingly." `CombatEngagement.OrderAttack(attacker, target)` is the explicit override:
- Clears the attacker's `FleetRetreatDB` (re-commits — it's back in the fight).
- Sets the attacker **Weapons Free** (`FleetDoctrine.SetEngagementPosture`), or it would keep holding fire.
- Forces both into combat **now**: `StartEngagement` if neither is engaged (seeds the closing gap from real
  distance), else `EnsureInCombat` both (joins a running fight without resetting it).
It's a **direct call** (like doctrine/EMCON), so it bypasses the auto-trigger's detection/posture/retreat gates —
the player is taking the shot deliberately. No-ops on a friendly target (`AreHostile` guard), an empty fleet, or
self. `OrderAttackNearestHostile(fleet)` is the one-click convenience the Fleet-window button uses: finds the
nearest hostile fleet with ships (`FleetSeparation`) and `OrderAttack`s it (targeting a SPECIFIC enemy fleet by
map-click is the follow-up). **Connections:** writes `FleetCombatStateDB` (engage), clears `FleetRetreatDB`, sets
`EngagementPosture` — the resolver/closing model then runs the fight. **Client:** `FleetWindow.DisplayEngageButton`
("Attack nearest hostile fleet", Combat tab). Gauges: `OrderAttackTests` (forces engagement past a hold + retreat;
no-ops on a friendly; nearest-hostile finds + engages, null when none).

## Switchable doctrine

**What it is.** Each fleet can fly an active **combat posture** — its doctrine — set by the player (or NPC). The auto-resolver reads it as a read-time multiplier on that fleet's strength and toughness, so the *same* fleet fights differently under a different posture. Two pieces:
- `FleetDoctrineDB` (on a fleet) = the **active selection**: the chosen posture's id + its `FirepowerMult` / `ToughnessMult` / `SpeedMult` / `IsRetreat`, plus `SwitchableAfter` (the switch-cooldown clock).
- `CombatDoctrineBlueprint` (in `ModDataStore.CombatDoctrines`, loaded from `GameData/basemod/TemplateFiles/combatDoctrines.json`) = the **moddable catalog** of selectable postures. Wired through the standard mod pipeline: a `DataType.CombatDoctrine` case in `ModInstruction` + `ModLoader`, a dict in `ModDataStore`, an entry in `modInfo.json`.

**Setting a posture.** `FleetDoctrine.TrySetDoctrine(fleet, blueprint, now)` copies the blueprint's effects into the fleet's `FleetDoctrineDB` and starts the cooldown; it returns `false` (no change) if the fleet is still within `SwitchableAfter`. Effects apply **at read time** (the `BonusesDB` pattern) — never baked into ship stats, so switching is reversible.

**How combat reads it.** `CombatEngagement.StepEngagement` works off `GetCombatShips(fleet)`, which tags every ship with the firepower/toughness multipliers of the **component it sits in** (see "Fleet components" below) — so a posture set on the whole fleet applies to the ships directly in it, and a posture set on a sub-fleet applies to that sub-fleet's ships. A fleet/component with no `FleetDoctrineDB` reads ×1.0 (neutral), so doctrine is purely additive over step 4. `AutoResolve` (the pure ship-list variant) stays doctrine-free — doctrine lives where fleets do (the engagement).

**Don't confuse with `FactionInfoDB.Doctrine`** — that's the strategic Economic/Military/Tech/Expansion AI vector (a different system). Same word.

**Base catalog (4 postures):** `balanced` (Utilitarian), `all-out-attack` (Offensive: +firepower / −toughness), `defensive-line` (Defensive: −firepower / +toughness), `fighting-withdrawal` (Defensive, `IsRetreat=true` — the withdraw posture for step 7).

**Tests:** `Pulsar4X.Tests/FleetDoctrineTests.cs` — the catalog loads from JSON; `TrySetDoctrine` applies the multipliers and honours the cooldown; an aggressive (×2 firepower) fleet beats the identical enemy that has none. `BaseModIntegrityTests` (existing) also validates the JSON loads with zero skipped entries.

**Not yet:** a player-facing order to change doctrine mid-fight (the engagement lock, step 11, will allow that one order during combat); `SpeedMult` is stored but not yet applied to movement.

---

## Fleet components — per-component doctrine

**What it is.** A fleet can be split into named **components** — Front Line, Flank, Rear Guard, Artillery — so different parts of one fleet fight with different postures in the *same* engagement (docs/COMBAT-DESIGN.md System 4, detailed design). A component is just a **sub-fleet**: `FleetDB` already nests via `TreeHierarchyDB`, so ship assignment, movement, and detach/reattach all already work — the only new part is that each sub-fleet can carry its own `FleetDoctrineDB`.

**How it works.** `CombatEngagement.GetCombatShips(fleet)` walks the fleet tree and returns a `List<CombatShip>` — each `CombatShip` is `{ Entity Ship, double FirepowerMult, double ToughnessMult }`, where the multipliers come from the doctrine of the **immediate component** that ship sits in. A ship directly in the top fleet gets the top fleet's posture; a ship in a sub-fleet gets the sub-fleet's. `StepEngagement` then sums `Firepower × FirepowerMult` for strength and scales each ship's casualty-toughness by its own `ToughnessMult`.

**v1 stacking rule: component overrides fleet, no inheritance.** A sub-fleet with no `FleetDoctrineDB` reads ×1.0 (neutral) — it does **not** inherit the parent fleet's multiplier. This keeps the math predictable (one posture per ship, no multiplicative stacking). Revisit if v2 wants fleet-wide buffs that layer onto component postures.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDB.Children` (the tree — ships and sub-fleets); `FleetDoctrineDB` per fleet-node (via `FleetDoctrine.FirepowerMult`/`ToughnessMult`).
- **Feeds OUT:** `GetCombatShips` is consumed by `StepEngagement` (strength + casualties). `GetFleetShips` (the flat `List<Entity>`, no multipliers) is unchanged and still used for count checks, the battle-trigger detection, range, and tests.
- **Triggers:** nothing new — same casualty side effects as the battle trigger.

**Test:** `Pulsar4X.Tests/FleetComponentTests.cs` — a ship in an offensive sub-component reads ×2 while a ship directly in the fleet reads ×1 (doctrine is per-component, not whole-fleet); and a component's ×2 firepower flips a battle a raw 6k-vs-10k hull would have lost.

---

## Retreat — breaking off a fight

**What it is.** A fleet can **break off** an engagement instead of fighting to extinction (docs/COMBAT-DESIGN.md System 5). v1 is a **math outcome**: breaking off attaches a `FleetRetreatDB` (the flag + a withdraw vector + who it fled from) and ends the engagement. It does **not** issue a movement order — ships don't physically run yet; that's a v2 movement-system layer. `FleetRetreatDB` is the hook that layer will read.

**Two triggers (both in `CombatEngagement.ShouldRetreat`):**
- **Posture** — the fleet flies a withdraw doctrine (a `FleetDoctrineDB` with `IsRetreat=true`, e.g. the base catalog's `fighting-withdrawal`). The posture *is* a standing retreat order, so the fleet breaks off after one salvo window.
- **Threshold** — the fleet has lost at least `RetreatCasualtyThreshold` (v1 flat **0.5**) of the ship count it started the engagement with (`FleetCombatStateDB.InitialShipCount`, captured at `StartEngagement`).

A **wiped** fleet (0 ships) is destroyed, not retreated — `ShouldRetreat` returns false at count 0, so a retreat always leaves survivors.

**The withdraw vector.** `RecordRetreat` sets a unit vector pointing from the enemy fleet toward the retreating fleet (the way it would run). If fleet positions aren't available or coincide (common in a headless test where ships share a body), it records `Vector3.Zero` — best-effort; the flag and `FledFromFleetId` are always recorded.

**Connections (Prime Directive):**
- **Feeds IN:** `FleetDoctrine.IsRetreat(fleet)` (posture); `FleetCombatStateDB.InitialShipCount` + current survivor count (threshold); fleet positions (vector).
- **Feeds OUT:** `FleetRetreatDB` on the fleet (persists past the engagement) — the v2 movement layer and any "did this fleet retreat?" readout/UI consume it. Ends the engagement (clears `FleetCombatStateDB`, releasing the engagement lock, step 11).
- **Triggers:** engagement end (same path as a wipe).

**Test:** `Pulsar4X.Tests/FleetRetreatTests.cs` — a fleet on `fighting-withdrawal` breaks off intact (posture); a 4-ship fleet that loses half retreats with its survivors (threshold). Both assert the `FleetRetreatDB` is recorded and the engagement ended.

**Not yet (v2):** the actual withdraw **movement** (Breaking Off → Withdrawing → Safe state machine, rally point, pursuit); per-doctrine retreat thresholds (v1 uses one flat constant); commander-triggered early retreat (System 6).

---

## Engagement lock — engaged fleets can't be re-tasked

**What it is.** Once a fleet is in a battle, you can't re-task it — its regular orders are refused until the fight ends. The *only* thing you can still do is change its **doctrine**. This is what makes the combat model "set the fight up, then steer it with doctrine, not micromanagement" (the developer's requirement, step 11).

**How it works.** The lock lives in the order handler — `StandAloneOrderHandler.HandleOrder` — not in the Combat subsystem, but it keys on a combat blob, so it's documented here too. After an order passes `IsValidCommand`, `IsEngagementLocked` rejects it (silently, no execute) when:
- the order's `EntityCommanding` is a fleet that has a `FleetCombatStateDB` (i.e. it's engaged — the battle trigger attaches this on both fleets), **and**
- the order is not flagged `EntityCommand.IsAllowedDuringEngagement` (default false).

**Why doctrine still works.** Doctrine changes go through a **direct call** — `FleetDoctrine.TrySetDoctrine` — not an `EntityCommand`, so they never reach the order handler and are unaffected by the lock. That's the v1 mechanism for "only doctrine changes apply." The `IsAllowedDuringEngagement` hook is the path for any *future* combat-time order (e.g. an explicit retreat order) to opt back in.

**Scope (v1).** The lock is fleet-level: it blocks orders whose commanding entity is an engaged fleet (`FleetCombatStateDB` is only ever on fleets). Orders on an individual ship inside an engaged fleet are not blocked by this check — re-tasking individual ships mid-battle is a v2 tightening. The refusal is silent at the engine level; surfacing a player-facing "fleet is engaged — orders locked" message is the UI's job (it reads `FleetCombatStateDB` to show the locked state).

**Connections (Prime Directive):**
- **Feeds IN:** `FleetCombatStateDB` presence on the commanding fleet (set/cleared by the battle trigger + retreat); `EntityCommand.IsAllowedDuringEngagement`.
- **Feeds OUT:** order acceptance/refusal — every `FleetOrder` / movement order routed through `StandAloneOrderHandler` is now gated by it.
- **Triggers:** nothing — it only gates existing order execution.

**Test:** `Pulsar4X.Tests/EngagementLockTests.cs` — a fleet with a `FleetCombatStateDB` refuses an AssignShip order (ship count unchanged); a `TrySetDoctrine` on the same engaged fleet still applies; removing the combat state lets the order through.

---

## Example combat-test ships (testing enabler)

Two purpose-built armed designs ship in the base mod for setting up a fight (spine step 10), in
`GameData/basemod/ScenarioFiles/designs/shipDesigns.json` and listed in `colony-earth`'s `ShipDesigns` so the
starting faction has them (spawnable from DevTools):

| Design id | Name | Build | Role |
|-----------|------|-------|------|
| `default-ship-design-test-warship` | Aegis Test Warship | 4 lasers, plastic armour ×6, 2 reactors/4 batteries/4 engines | the **strong** side |
| `default-ship-design-test-corvette` | Picket Test Corvette | 1 laser, plastic armour ×1, 1 reactor/1 battery/1 engine | the **weak** side |

Both reuse only gunship-proven components, so they stay buildable (`BaseModIntegrityTests`) and are auto-rated by
`ShipCombatValueTests`. The existing `default-ship-design-gunship` / `-dropship` are also armed (2 lasers each) if
you want an even matchup. Spawn an Aegis fleet for one faction and a Picket fleet for the other (DevTools faction
switcher, step 9) to watch the auto-resolver decide it.

**Weapon-flavor + triangle roster (added with the depth pass P3/P4/P6) — all in `shipDesigns.json` + `earth.json`:**

| Design id | Name | Mounts | Triangle role |
|-----------|------|--------|---------------|
| `default-ship-design-test-railgun` | Lancer Railgun Cruiser | 4 railguns | finite-velocity kinetic — brutal vs slow, dodged by fast |
| `default-ship-design-test-flak` | Bulwark Flak Escort | 4 flak guns | high-saturation PD — the fighter/missile killer |
| `default-ship-design-test-fighter` | Wasp Strike Fighter | 1 railgun, 4 engines | small + agile = **evasive** (dodges railguns) |
| `default-ship-design-test-capital` | Leviathan Battleship | 4 railguns, 8 armour, 2 engines | big + sluggish = **tanky, can't dodge** |

These compose already-registered weapon components (no new `weapons.json` templates), so a new such design needs
only its entry in `shipDesigns.json` + the `ShipDesigns` list in `earth.json` — NOT the full six-point chain
below (that's only for a brand-new weapon TYPE). `WeaponTriangleTests` proves the dodge edges off the Wasp/
Leviathan real combat values; `RailgunWeaponTests` / `FlakWeaponTests` prove the Lancer/Bulwark weapon profiles.

**Warp drives — these are combat ships, but they couldn't travel (added 2026-06-27).** Every design above also
mounts an **Alcubierre warp drive**: `default-design-alcubierre-2k` on the four heavies (Aegis / Lancer / Bulwark /
Leviathan), the lighter `default-design-alcubierre-500` on the Picket corvette and Wasp fighter. It's the **same
drive the utility ships** (Courier / Freighter / Surveyor) already carry, so its materials are already in the
colony's `StartingItems` and it stays buildable (`BaseModIntegrityTests` covers it). Without a warp drive,
`WarpMoveCommand` now logs `[WARP] … CAN'T WARP — no warp drive` and the ship just sits there on a move order — the
exact "I ordered it to move and nothing happened" symptom (see `Movement/CLAUDE.md`). The drive is **travel-only**
and is deliberately **not** in the weapon/armour/engine counts in the tables above — those counts are the sublight
NTR1.8 thrusters that set in-system evasion, which the warp drive does not change. The lighter 500-class drive went
on the **Wasp on purpose**: any added mass nudges evasion down, and the Wasp's evasion is exactly what
`WeaponTriangleTests` measures — the small drive was chosen to keep that calibration intact (CI is the check).

**Test:** `Pulsar4X.Tests/CombatTestShipsTests.cs` — the two designs load onto the faction and rate strong-vs-weak
(warship out-guns + out-armours the corvette); a 3v3 auto-resolve is a decisive `SideAVictory` with all corvettes lost.

**Adding a new player-buildable weapon + armed design — it touches SIX registration points; miss any and New
Game / `CreateWithColony` crashes (gotcha-10, learned the hard way twice on railgun P3).** The colony build
chains template → component design → ship design, each looked up in the faction's data store, so ALL of:

1. **`*Atb` C# class** (`GameEngine/Weapons/Weapon<X>/`) — implements `IComponentDesignAttribute`; ctor arg order MUST match the JSON `AtbConstrArgs(...)`.
2. **`ComponentTemplate`** in `weapons.json` — `UniqueID` + the `AtbConstrArgs` property with the exact `AttributeType` namespace, ResourceCost keys that are all defined materials.
3. **`ComponentDesign`** in `componentDesigns.json` — `UniqueId` (lowercase d) + `TemplateId` pointing at #2.
4. **`earth.json` `StartingItems`** — add the **template** id (e.g. `"railgun-weapon"`). *Unlocks the template for the faction.* Miss this → `ComponentDesignFromJson`: `"<template> was not found in the faction data store"`.
5. **`earth.json` `ComponentDesigns`** — add the **component-design** id (e.g. `"default-design-railgun-weapon"`). *Builds it into `InternalComponentDesigns`.* Miss this → `ShipDesignFromJson`: `KeyNotFoundException: '<component-design-id>'`.
6. **`earth.json` `ShipDesigns`** + the ship design in `shipDesigns.json` — the design that mounts the weapon (its `Components[].Id` = the #5 design id).

The two failures are distinct and both crash **every** `CreateWithColony` test at setup (not just one). `BaseModIntegrityTests` did **not** catch either (it checks the mod store + industry buildability, not the faction StartingItems-unlock → ComponentDesigns-build → ShipDesigns-resolve chain); the harness itself is the standing sensor — once a design is in `earth.json`, every harness test builds it end-to-end, so a missing registration fails loudly and immediately.

---

## Dodge in the resolve (weapon flavor decides WHO gets hit)

**What it is.** `StepEngagement` no longer pours one flat firepower number into the enemy's pool. It builds each
side's **fire mix** (`BuildFireMix` → a list of `WeaponProfile`s, each weapon's damage scaled by its doctrine
firepower mult), and `ApplyCasualties` is now **dodge-aware**: a ship's *effective* toughness is its raw
toughness ÷ the **landed fraction** of the incoming fire, and ships fall **most-hittable first**. So the big
slow hull dies while the nimble fighter holds — the developer's acceptance test.

**The math (all in `CombatEngagement`, see `docs/WEAPONS-AND-DODGE-DESIGN.md`):**
- `HitFraction(weapon, evasion, separation_m = 0)` (internal, unit-tested): `velocityTerm = velocity/(velocity+VelocityReference)`;
  `trackingEffectiveness = max(velocityTerm, tracking)`; `dodgeChance = evasion × (1 − trackingEffectiveness)`;
  result `= clamp(1 − dodgeChance, saturationFloor, 1)`. A beam (≈light-speed) → ~1 (can't dodge light); a slug
  (finite, ballistic) → low vs the evasive; flak's high saturation floors it up.
- **RANGE term — accuracy falls off with distance (2026-06-27, the "authentic closing" pass).** When a closing
  `separation_m > 0` is in play, `dodgeChance` gains `evasion × timeFactor × (1 − tracking)` where
  `timeFactor = flightTime/(flightTime + FlightTimeReference_s)` and `flightTime = separation/velocity`. So a shot
  that takes longer to cross the gap is easier to dodge — a **dumb slug** loses a lot of accuracy at range, a
  **guided** weapon (high `Tracking`) barely any, and a **beam** (≈light-speed → ~0 flight time) **none**. This is
  what makes the closing fight authentic: railguns harass inaccurately from afar and want to close, missiles hold
  accuracy at standoff, beams knife-fight. **`separation_m = 0` (closing off / point blank) zeroes the term, so the
  pre-closing resolve is byte-identical** — threaded as a default-0 param through `LandedFraction` → `ApplyCasualties`
  (the caller passes `SeparationOf(defender)`). Tunable: `FlightTimeReference_s` (default 10 s). Gauge:
  `DodgeResolveTests.HitFraction_RangeDegradesBallistics_NotBeamsOrGuided`.
- `LandedFraction(fireMix, evasion, separation_m = 0)` = damage-weighted average `HitFraction` over the mix.
- Effective toughness in `ApplyCasualties` = `Toughness × ToughnessMult ÷ LandedFraction`.

**Backward-compatible (the green spine stays green).** A ship with **no** weapon profiles but real firepower
(old-style combat value) fires as a `FallbackBeamVelocity` always-hit beam, and a target with **0 evasion** has
`LandedFraction = 1` — so an all-old-style fight (every existing combat test) behaves EXACTLY as before. Dodge
only changes outcomes once ships carry weapon profiles + evasion.

**Performance — bucketed both ways.** Outgoing: `BuildFireMix` aggregates fire **by weapon class** (≤4 entries),
so each `LandedFraction` iterates a handful of classes, not every enemy weapon. Incoming: `ApplyCasualties`
buckets defenders by their **combat value** — key `(doctrine toughness mult, evasion, toughness, role)`, i.e.
everything that decides how a ship dies — and computes the landed fraction + effective toughness ONCE per
bucket, killing whole ships as a count (`CasualtyBucket`). So **500 identical fighters cost the same as 5**; the
costly work is O(buckets), not O(ships). Proven by `CombatPerformanceTests` (200 real warships resolve in
milliseconds) — **the tripwire if either aggregation ever breaks.** Aggregating outgoing fire uses a
damage-weighted velocity/tracking/saturation (a fine v1 approximation; same-class weapons are similar).

**The casualty bucket is the seam for "degraded" condition tiers.** Because ships bucket by combat value, a
damaged ship with a degraded combat value lands in a *different* bucket automatically — so the
aggregate-force-condition model (Pristine / Lightly / Moderately / Severely Degraded, per-tier debuffs, "launch
only Non-Degraded" orders) needs **no new resolve code**, only the parked v2 "recalc combat value on damage"
hook. Design + connected systems: `docs/WEAPONS-AND-DODGE-DESIGN.md` → "Future depth — aggregate force
condition." Principle: *simulate at the granularity of the decision, not the entity.*

**v1 scope.** This delivers the dodge-driven triangle edges (Beam▸Fighter, Fighter▸ballistic-Capital). The
explicit `TriangleBonus` (a tunable class-vs-class modifier) and the Capital▸Beam edge (which needs weapon
**range**, a v1 stub) are refinements on top. `AutoResolve` (the pure ship-list variant) stays dodge-free, like
it stays doctrine-free.

**Combat pace — the hot-damage rebalance (2026-06-25).** The raw numbers were "hot": a hull is built from
~100 kJ components but a gun pours ~1 MJ/sec, so ships died in ~1 second of fire and whole fleet battles ended in
**2–4 salvos (10–20 game-seconds)** — faster than the default 1-hour master tick, i.e. over before you could
watch or steer them. `StepEngagementGroup` now multiplies each salvo's pooled damage by **`SalvoDamageScale`**
(0.1), so only a tenth of the raw energy counts toward kills per step — the same fight now plays out over **~10×
more salvos** (a couple of game-minutes you can watch and change doctrine inside). Because the scale is **uniform**
(every fleet's incoming fire is scaled by the same factor), it changes battle **duration, not the outcome**: who
wins, the exchange ratio, and every weapon-triangle/dodge/doctrine finding are unchanged — only the salvo count
to get there rises. It is the single tunable for the "per-shot-energy ÷ hull-toughness" balance the
`WeaponTriangleBattleTests` docstring flagged as a v2 pass. It lives ONLY on the stepped (live, watchable)
resolve; `AutoResolve` (the instant off-screen resolver) stays unscaled on purpose — a battle nobody watches
needn't be paced out. Measured before/after numbers: `CombatStressLab` + `CombatBattleSims`.

**Test:** `Pulsar4X.Tests/DodgeResolveTests.cs` — the `HitFraction` curve (beams ignore evasion, slugs are
dodged, flak floors it); and through the resolve, slug fire kills the un-evasive battleship while the fighter
(same toughness, only evasion differs) dodges and survives.

---

## Combat sandbox — spawning an enemy to fight (the live-test enabler)

**What it is.** `CombatSandbox.SpawnHostileFleet(...)` stands up the OTHER side of a battle: a fresh registered
faction + a fleet + N ships (built from the player's designs, owner-flipped to the enemy), parked at a body so
the `BattleTriggerProcessor` auto-engages them. A fresh game has only the player and an empty sky, so without this
there's nothing to fight — it's what makes "spawn an enemy and press play" possible. The DevTools "Spawn Hostile
Fleet" button is a thin wrapper over it.

**Why it's in the ENGINE, not the client.** The combat test fixtures (`BattleTriggerTests`, `MultiParty…`) drive
`CombatEngagement.Tick` DIRECTLY and never advance the game clock, with a standing note that a *bare* enemy
faction's owner-flipped ships "don't survive movement processing across a clock advance." That made the live path
(spawn enemy → press play → full per-tick processor sweep) an unproven, CI-blind unknown. Putting the spawn in the
engine lets **`CombatSandboxTests` advance the REAL clock (`game.TimePulse.TimeStep()`) and assert the spawned
enemy survives + engages** — so the live button is proven in CI, not on the developer's Windows build. The recipe
that makes the flipped ships persist like a real NPC's: `CreateBasicFaction` (registers it in `game.Factions` +
gives a root `FleetDB`), add the system to the faction's `KnownSystems`, and copy the player's `ShipDesigns`. (The
ships are still built under the player faction because `ComponentDesigns` is a read-only view and the enemy hasn't
unlocked components; combat only reads `FactionOwnerID`, so the flip is enough.)

**Fuelling (added 2026-06-25).** `ShipFactory.CreateShip` builds ships with **empty** tanks **on purpose** —
production-built ships are meant to be fuelled at a colony, so CreateShip must not hand out free fuel (that would
break the fuel economy). The natural start fleet is fuelled by the start setup; **manually-spawned ships are not**,
which is why a DevTools-spawned ship showed 0 fuel. The shared fix is **`ShipFactory.FillFuelTanks(ship,
factionInfo)`** (returns units stored): `SpawnHostileFleet` calls it **before the owner flip** (fuel resolves
through the ship's *faction* library — `UpdateMassFuelAndDeltaV` → `GetFactionCargoDefinitions`, unlocked
`CargoGoods` — and only the player has fuel unlocked, so it must run while the ship is still player-owned), and the
DevTools **"Spawn Ship"** button calls the same helper. It reads the ship's own `NewtonThrustAbilityDB.FuelType`
and fills the tank via `CargoTransferProcessor.AddCargoItems` (`CargoMath.AddCargoByUnit` caps at tank free volume).
**Key gotcha:** an engine may burn a fuel the faction hasn't *unlocked* — the Leviathan's NTR burns **`ntp`**
("Nuclear Thruster Propellant", an `"ntr"`-category fuel). All fuels share `CargoTypeID "fuel-storage"`, so the
tank accepts any of them; `FillFuelTanks` looks the fuel material up in `CargoGoods` **then falls back to
`LockedCargoGoods`** so a not-yet-researched fuel still stocks. A ship with no thruster / no fuel-tank bay / a
`FuelType` that resolves to no material is left empty (returns 0, no crash). (The auto-resolve battle doesn't burn
fuel — v1 combat is math, not maneuvering — this is for realism + a future maneuver/retreat layer.)

**Charging (added 2026-06-27) — the sibling fix that actually makes a spawned ship MOVE.** Fuel alone wasn't
enough: **warp is paid from stored electricity, not fuel**, and `CreateShip` leaves stored energy at **0** too
(`EnergyStoreAtb` inits `EnergyStored = 0`). A spawned ship had full tanks but a dead battery, so a move order did
nothing — `WarpMoveCommand` blocks while `EnergyStored < BubbleCreationCost`. The start fleet dodges this because
`DefaultStartFactory` hand-charges each ship to `EnergyStored = 2,750,000` — *that* was "what the premade ships have
that a spawned one doesn't." Fix: **`ShipFactory.ChargeReactors(ship)`** (the energy sibling of `FillFuelTanks`),
called by both spawn paths right after the fuel fill (`SpawnHostileFleet` here + DevTools "Spawn Ship"). It tops
`EnergyStored` to the ship's own `EnergyStoreMax`; a charged base-mod battery holds **2×–4× one warp bubble** at
starting tech (battery-2t = 1,000,000 KJ each; alcubierre-2k bubble = 1,000,000 KJ, alcubierre-500 = 250,000 KJ), so
a charged ship can always warp — and can FIRE (weapons draw stored energy too). Sensor:
`CombatTestShipsTests.ChargeReactors_FillsStoredEnergy_SoASpawnedShipCanWarp`; full warp chain in `Movement/CLAUDE.md`.

> **Spawn-parity rule:** "ready to fly" is the hull PLUS what `DefaultStartFactory` does after the build. There are
> **two** convenience-spawn paths — `CombatSandbox.SpawnHostileFleet` and `DevToolsWindow` "Spawn Ship" — and both
> must mirror the start setup. `FillFuelTanks` + `ChargeReactors` are the pair that does it today; **when you add any
> new "ship is ready" state to the start factory (crew, ammo, ECM charge…), extend BOTH spawn helpers beside them** —
> or a spawned unit silently behaves differently from a native one.

**Test:** `Pulsar4X.Tests/CombatSandboxTests.cs` proves three things separately: **(1) persistence** — spawn 3
hostiles, advance the real clock, assert they're still there (3/3); **(2) engageable** — drive
`CombatEngagement.Tick` over the system (the proven path) and assert the unarmed player ship is destroyed (only
possible if the spawned hostiles are real, in-range enemies the trigger fights); and **(3) fuelled** — it spawns
the **Leviathan** (the `ntp`-burning NTR design, the trickiest fuel path) and asserts every ship's `FuelType`
**resolves to a real fuel material** (catches a category-vs-material mapping bug that would otherwise silently
no-op) and that every fuel-capable ship comes out with `TotalFuel_kg > 0` (asserts only for ships with a matching
fuel bay, so a no-bay design can't falsely fail it).

**Live battle narration (added 2026-06-25).** `CombatEngagement.NarrateToLog` (a `public static bool`, default
**false**) makes the engine write plain-language `[Combat]` lines to the captured log (`game_log.txt`) on each
**state change** — `enters combat`, `salvo N: <fleet> lost K ship(s), M left`, `breaks off — retreats`,
`disengages` — so a live fight is visible in the log, not only the Fleet Combat tab. It's logged only on
transitions (never per-tick), so it reads like a play-by-play without flooding. **Default off so it never slows
the timed battle tests** (`CombatPerformanceTests`, `CombatStressLab`, the 1000-ship `B10`); the client turns it
**on** at startup (`PulsarMainWindow` ctor: `CombatEngagement.NarrateToLog = true`). Helpers `CombatLog`/`FleetLabel`
are in `CombatEngagement.cs`; the per-salvo line guards on `NarrateToLog` before building its string so the hot
casualty path allocates nothing when narration is off. **Bonus diagnostic:** because the live auto-trigger
(`BattleTriggerProcessor` → `CombatEngagement.Tick`) runs this, an `[Combat] … enters combat` line on PLAY confirms
the auto-trigger fired; its absence (while DevTools "Tick Combat" *does* produce lines) localises the open
"does PLAY auto-start combat live?" question to the trigger/scheduler, not the resolve.

> **RESOLVED (2026-06-27, live log):** a player session confirmed the auto-trigger DOES fire on play — the captured
> `game_logs/` pages show `[Combat] Military Fleet #803 enters combat (3 ships)` / `Hostiles Fleet #818 enters combat`
> → `[ACTION] COMBAT INTERRUPT` → `salvo 5: Hostiles … lost 1 ship, 0 left` → both disengage. So the whole live chain
> (trigger → engage → auto-resolve → auto-pause → win) works end-to-end in the running game. The remaining gap was
> purely **visibility** (it's math, with no on-map cue and over in a blink) — which the Battle Report / marker /
> readout below address.

**Battle Report data — `BattleLog` (added 2026-06-27, the combat-visibility feature).** The `[Combat]` console lines
above are great for a *log* review but VANISH from the game the instant a fight ends (the live `FleetCombatStateDB`
is removed on disengage), so a battle you blinked and missed leaves nothing on screen. `BattleLog`
(`Combat/BattleLog.cs`) is the fix: a thread-safe, capped (`MaxEvents` = 250) ring buffer of structured
`BattleEvent` records — `{ When (game time), FleetId, FleetName, FactionId, Type (Engaged / Salvo / Retreat /
Disengaged), ShipsLost, ShipsLeft, Step, Note }` — captured **unconditionally** (NOT gated on `NarrateToLog`, so the
report works regardless of the console-log flag) at the SAME five state-change sites the narration uses:
`StartEngagement`, `EnsureInCombat`, `ApplyCasualties` (Salvo, with the kill count), `RecordRetreat`,
`EndEngagement`. `RecordBattleEvent` in `CombatEngagement` is the single capture helper (defensive — never throws;
reads game time via `fleet.Manager?.Game?.TimePulse?.GameGlobalDateTime`). The client's **persistent Battle Report**
panel reads `BattleLog.Recent()` (a snapshot array copy, safe on any thread) to list recent fights AFTER they end.
Runtime-only (not save/load) — a "recent battles" readout, not game state; thread-safe because combat ticks run
per-system in parallel. Sensor: `BattleLogTests` (records survive the fight; ring buffer caps at MaxEvents).

**Per-salvo PLAY-BY-PLAY in the Salvo note (added 2026-06-27, "salvo means nothing" feedback).** The `Salvo`
event's `Note` is now a detailed line — `took Railgun + Beam fire at 8.5 km from <attacker> — 42% on target (58%
dodged), 0.82 GJ dealt; destroyed 'Cargo Courier'; 3 left` — built in `ApplyCasualties` from the data the
**aggregate** resolve actually has: the weapon **CLASSES** in the incoming fire mix (`DescribeFireMix`), the
ship-count-weighted **landed fraction** (hit-vs-dodge rate), the **damage** dealt that salvo, and the ships
**destroyed by name** (`ShipName`). **Honest limits of the aggregate model (flagged for the player):** there is NO
per-shot hit/miss and NO per-component loss — ships are whole-or-dead in v1 (the per-component damage sim is parked,
gotcha #1), and damage is a fleet pool, so "ship X is at 60% hull" isn't tracked. Those need the degraded-condition
model below. **Volume control:** the rich note + event is recorded **every** salvo a fleet takes fire **when
`NarrateToLog` is on** (the client sets it true → full Battle-Report play-by-play); with it **off** (the headless
combat tests + perf sims) only **casualty** salvos record, exactly as before — so every fixture's event volume and
the O(fleets) note cost are unchanged (note is per-fleet-per-salvo, never per-ship). Gauge:
`BattleLogTests.BattleLog_SalvoNote_NamesWeaponHitRateDamageAndDestroyedShip`.

**Future: degraded / damaged-component condition (the design the play-by-play points at).** The per-salvo note's
limits are exactly where the parked **aggregate force-condition** model plugs in (`docs/WEAPONS-AND-DODGE-DESIGN.md`
→ "Future depth — aggregate force condition"). The seam is already here: `ApplyCasualties` **buckets ships by combat
value** (`(toughnessMult, evasion, toughness, role)`), so a *damaged* ship with a recalculated, lower combat value
lands in a **different bucket automatically** — Pristine / Lightly / Moderately / Severely Degraded tiers fall out
with **no new resolve code**, only the parked "recalc `ShipCombatValueDB` on component loss" hook (gotcha #2) + a
per-tier debuff table. At that point the note can read `'Aegis' degraded to Moderate (lost a railgun + a reactor)`
because the recalc knows which components dropped. Principle (unchanged): **simulate at the granularity of the
DECISION, not the entity** — the player decides at the force/tier level ("pull back the Moderately-Degraded wing"),
so that's the granularity to model, rather than a full per-component-per-shot sim that buys cost without a decision.

**Determinism caveat for the future RNG-damage hook (audit, 2026-06-27).** The play-by-play floated an *RNG-based
"received-damage" model* — each tick, roll which components a degraded ship loses and feed that back into its combat
value. That is buildable, but it has a **hard constraint the current aggregate model doesn't**: combat MUST stay
deterministic (the locked rule — *fast-forward must equal watch*; see `docs/FLEET-COMBAT-CLOSING-DESIGN.md`). Today's
resolve is pure arithmetic (no draws), so it's deterministic for free. The moment you add per-tick random component
loss you introduce RNG, and the result will only match between a watched battle and a fast-forwarded one if **every
draw is reproducible**: it must pull from a *seeded* stream and the draw order must be **independent of fleet/ship
iteration order** (e.g. seed a per-ship or per-(attacker,target,tick) sub-stream, don't share one cursor across a
loop whose order can shift). `StarSystem.RNG` is shared across all processors, so reusing it directly would make a
combat draw's value depend on how many *other* systems happened to roll first that tick — non-reproducible. So the
hook needs **its own seeded RNG, keyed for order-independence**, before any RNG-damage model ships. Flagged here so
it's designed in, not bolted on.

**Engage/disengage THRASH fix (2026-06-27).** Live symptom: the combat log spamming `enters combat` / `disengages`
over and over for the same fleets. Root cause (both flavours): **a fleet LEAVES a fight but stays physically in
range, so `Tick`'s engage pass re-grabs it the very next tick.** Two ways it happened:
1. **Stalemate** — two fleets that can't damage each other (no firepower) entered, `StepEngagementGroup` ended them
   as `frozen` (totalFire ≤ 0), and they re-engaged next tick. Fix: the engage pass (and `NewEngagementImminent`)
   **skips a hostile pair when NEITHER side has firepower** (`FleetHasFirepower`) — no fight that can't resolve.
2. **Retreat** — a fleet that broke off gets a `FleetRetreatDB`, but v1 retreat records the withdraw vector and
   **issues no move order**, so the fleet stays put, in range → re-engaged → retreats again. Fix: the engage pass
   **skips a fleet that holds `FleetRetreatDB`** (it has withdrawn — don't yank it back). `FleetRetreatDB` had **no
   lifecycle** (never cleared), so to avoid a permanently-pacifist fleet, the top of `Tick` **clears a stale
   `FleetRetreatDB` once no hostile is in range** (`AnyHostileInRange`) — the enemy died/left, or the player moved
   the fleet out of range (the v1 "re-commit" path until the v2 movement layer actually sails the vector).
Net: a withdrawn fleet stays withdrawn while threatened and rejoins the fight once clear; two unarmed fleets never
"fight." Sensors: `CombatReengageTests` (unarmed pair never engages; a retreated fleet isn't re-grabbed, then its
flag clears when the threat is gone). **Connections:** reads `FleetRetreatDB` (Feeds IN) + clears it (Feeds OUT);
the firepower guard reads each fleet's `ShipCombatValueDB`. The directly-driven `StartEngagement`/`StepEngagement`
test entry points are unchanged — only the auto-trigger `Tick` gained the guards, so the existing fixtures still pass.

**Gotchas the gauge surfaced (two, both load-bearing for the live test):**
1. **The flipped-faction enemy ships DO persist through a clock advance** with the sandbox's faction setup
   (`CreateBasicFaction` + `KnownSystems` + copied `ShipDesigns`) — the old "don't survive movement processing"
   warning was about a *bare* faction. Proven: 3/3 survive.
2. **The lightweight colony test harness does NOT auto-fire hotloop processors on `TimeStep`.** Setting the system
   Foreground (`IncrementExternalObserver(true)`) was *not* enough to make the battle trigger run on the clock
   advance in the test — so the gauge drives `CombatEngagement.Tick` DIRECTLY for the engagement proof (same as
   every other combat fixture). Whether the trigger auto-fires on a clock advance in the **full live game** is a
   separate question the test can't settle — `MasterTimePulse` only processes systems whose `ActivityState !=
   Stasis` (a colony system is Background, a watched one Foreground), but the harness's `TimeStep` path didn't
   exercise it here. **Live-test implication: confirm in the running client whether pressing play auto-starts the
   battle; if not, the fallback is a "Force Engagement" control that calls `CombatEngagement.Tick`.** The gauge
   logs whether the system clock even advanced, to tell harness-quirk from a real "trigger never fires" bug.
3. **The spawned hostiles are the FIRST foreign-faction entities a player can click — they expose latent
   client crashes that assume player-owned data (found + fixed live 2026-06-25).** The enemy faction is bare:
   `FactionDataStore.CargoTypes` is empty (everything sits in `LockedCargoTypes` until tech unlock). Clicking a
   hostile "Cargo Courier" opened its `EntityWindow`, which hard-indexed the OWNER faction's `CargoTypes[sid]` →
   `KeyNotFoundException` → whole-client crash (invisible in `game_log.txt` because the trace goes to stderr).
   Fixed client-side (defensive cargo-type lookup at three sites + a `SafeRender` render-loop gauge) — see
   `Pulsar4X.Client/CLAUDE.md` gotchas #11–#12. **Implication for this tool: any new "show me data about an
   entity" client panel must tolerate a foreign/NPC owner with empty unlocked data; the spawn button is the way
   to flush these out before real NPCs exist.**

---

## Model-coupled / tuning constants

| Constant | Value | Meaning | Where |
|----------|-------|---------|-------|
| `MissileLauncherFirepowerStub` | 100,000 | flat firepower (J/s) per missile launcher until ordnance warhead energy is wired (v2) | `ShipCombatValueDB.cs` |
| `UtilityRoleWeight` | 0.25 | combat-value role weight of a hull with no weapons | `ShipCombatValueDB.cs` |
| `ComponentHitPoints_J` | 100,000 | joules one component absorbs before destruction (= the damage tuning's "100 kJ kills a component") | `ShipCombatValueDB.cs` |
| `ArmorHitPointsPerThickness_J` | 100,000 | joules of toughness added per unit of armour thickness | `ShipCombatValueDB.cs` |
| `SizeReference_m3` | 1,000 | ship volume (m³) at which the size half-contributes to evasion (bigger = easier to hit) | `ShipCombatValueDB.cs` |
| `AgilityReference_mps2` | 5.0 | acceleration (m/s²) at which agility half-contributes to evasion (thrust ÷ mass) | `ShipCombatValueDB.cs` |
| `EvasionCap` | 0.95 | hard ceiling on Evasion — nothing is ever fully untouchable | `ShipCombatValueDB.cs` |
| `AutoResolveConfig.RoundSeconds` | 5.0 | game-seconds of fire per salvo round | `AutoResolve.cs` |
| `AutoResolveConfig.MaxRounds` | 2000 | round-cap backstop; hitting it = Stalemate | `AutoResolve.cs` |
| `CombatEngagement.EngagementRange_m` | 1e9 (1M km) | the COARSE proximity pre-gate; the real trigger is `WithinWeaponRange` when `RequireWeaponRangeToEngage` is on (client-on 2026-07-02) | `CombatEngagement.cs` |
| `CombatEngagement.RequireWeaponRangeToEngage` | false (client: true) | auto-start a battle only within actual WEAPON range (`WithinWeaponRange`), not the 1 Gm bubble — "seeing ≠ firing." Must be gated in BOTH `Tick` and `NewEngagementImminent`. Gauge `WeaponRangeTriggerTests` | `CombatEngagement.cs` |
| `CombatEngagement.RequireWeaponsReleaseToEngage` | false (client: true 2026-07-02) | a battle needs ≥1 side Weapons Free (fire-at-will); two holding fleets in weapon range sit in a standoff. Now also gated in `NewEngagementImminent` | `CombatEngagement.cs` |
| `CombatEngagement.MaxSteps` | 5000 | per-engagement step cap (stalemate backstop) | `CombatEngagement.cs` |
| `CombatEngagement.RetreatCasualtyThreshold` | 0.5 | fraction of starting ships a fleet must lose to break off (v1 flat; real value = per-doctrine, v2) | `CombatEngagement.cs` |
| `CombatEngagement.VelocityReference_mps` | 1e6 | shot velocity at which a weapon half-defeats evasion (beam ≫ this, slug ≪ this) | `CombatEngagement.cs` |
| `CombatEngagement.SaturationReference` | 50 | saturation (tracks/sec) at which a weapon half-guarantees a hit regardless of dodge (flak ≫ this) | `CombatEngagement.cs` |
| `CombatEngagement.MinLandedFraction` | 0.02 | floor on fire that lands — enough volume kills even a perfect dodger | `CombatEngagement.cs` |
| `CombatEngagement.FallbackBeamVelocity_mps` | 1e8 | an unarmed-profile (old-style) ship fires as this light-speed always-hit beam → dodge degrades to old behaviour | `CombatEngagement.cs` |
| `CombatEngagement.SalvoDamageScale` | 0.1 | **the combat-pace dial** — fraction of a salvo's raw energy that counts toward kills; <1 stretches a battle over more salvos (effectively "every hull is 1/scale tougher"). Uniform, so it changes battle DURATION, not who wins. The "per-shot-energy ÷ hull-toughness" knob (hot-damage rebalance 2026-06-25) | `CombatEngagement.cs` |
| `BattleTriggerProcessor` run frequency | 5 s | how often each system is scanned for battles | `BattleTriggerProcessor.cs` |

---

## Combat interrupt — stop the clock at first contact (added 2026-06-26)

**The problem it fixes.** Combat is paced in **game-time** — one salvo every 5 s (`BattleTriggerProcessor`), stretched over minutes by `SalvoDamageScale`. But the player advances game-time in **chunks** (default 1-hour step / continuous play). Advancing an hour silently runs all ~720 of the 5-second sub-pulses inside one button press, so a whole battle — first contact to total loss — resolves *invisibly* between two frames. The developer's report (2026-06-26): "I had no notice of when combat began and no time to intervene; all fleets destroyed in one tick." The pacing was fine; there was **no interrupt** to hand control back when fighting started.

**The fix has TWO halves — request the halt, and honour it at first contact.**

*Half 1 — request the halt.* `CombatEngagement.EnsureInCombat` (the JOIN primitive — fires once per fleet's first entry, it's guarded) calls `fleet.Manager.Game.TimePulse.RequestCombatHalt()` when `CombatEngagement.InterruptTimeOnNewEngagement` is true. `RequestCombatHalt` (`MasterTimePulse`) sets `CombatInterruptPending` and cancels `_timeSimulationCts` — the **exact** cancellation `PauseTime` uses — and the next `StartTime`/`TimeStep` restarts cleanly (each makes a fresh CTS).

*Half 2 — honour it at first contact (the granularity fix, 2026-06-26 round 2).* Cancelling the token is **not enough on its own**: `MasterTimePulse.SimulateTimeUntil` only checks the token at its master-loop boundary, and when nothing schedules a cross-system interrupt a whole `Ticklength` (default **1 game-hour**) is processed inside ONE `ManagerSubpulses.ProcessSystem` call. So the first version still ran the entire battle before the cancel was seen — the developer's round-2 report: *"combat initiation still happened suddenly… the second round between both leviathans should have given me more time."* The fix: when the interrupt is armed **and a brand-NEW engagement is about to fire**, the master loop advances in **fine sub-steps** of `MasterTimePulse.CombatReactionStep` (**5 s**, matched to the battle-trigger frequency) instead of a full Ticklength — so the loop returns to its boundary, sees the cancelled token, and stops **within one 5-second sub-pulse of first contact**. The gate is `CombatEngagement.NewEngagementImminent(manager)` (read-only: any two hostile fleets with ships in `EngagementRange_m` where **at least one is not yet in combat** — i.e. an entry/join would fire on the next trigger pass), checked per active system via `MasterTimePulse.AnyNewEngagementImminent()`. The clock stops **after the engaging tick's first salvo** (Tick engages *and* steps once), so the player lands in a barely-scratched battle with full control: change doctrine, assess, then drive forward at whatever speed they choose.

**Combat runs at the player's set speed — the engine only takes the wheel as a fight is BORN (the design choice, developer's call 2026-06-26).** The gate is keyed on a *new* engagement, **not** "any combat active". So the fine 5-second stepping kicks in only on the iteration a battle is forming — just long enough to land the auto-pause — and then the clock is handed straight back at the player's chosen `Ticklength` for the ONGOING exchange. Set a 1-second step to crawl through it salvo by salvo; set 10 minutes or an hour to resolve it fast. This was a deliberate reversal of the first cut (which forced 5 s through the *whole* battle and so overrode the time slider mid-fight — the developer correctly flagged that as a hidden second clock). The auto-pause is the only special behaviour: it guarantees you are never blindsided by a fight's START, then gets out of the way. A fresh fleet JOINING later (round 2) is itself a new engagement, so it re-pauses the same way.

**Why fine-stepping is cheap away from combat.** The gate is only consulted when a cap could apply (`GameGlobalDateTime + CombatReactionStep < target`) **and** the interrupt flag is on — and with no new engagement near, `NewEngagementImminent` returns at once and the loop takes a **full Ticklength** step exactly as before. So peacetime fast-forward *and* an ongoing battle both run at the set speed; the 5-second granularity only appears for the instant a fight is born. Lockstep is preserved — all systems still advance to the same (capped or full) target each iteration, so there is **no cross-system time desync** (a system left behind would trip the `Temporal Anomaly` guard).

**The flag (mirrors `NarrateToLog`).** `InterruptTimeOnNewEngagement` defaults **false** so every headless/combat test advances deterministically (a halt mid-`AdvanceTime` would break the timed sims, and the fine-step gate stays inert); the **client turns it on** at startup (`PulsarMainWindow` ctor, next to `NarrateToLog = true`). The client surfaces the halt: `PulsarMainWindow.PostFrameUpdate` reads+clears `TimePulse.CombatInterruptPending`, writes a `[ACTION] COMBAT INTERRUPT` SessionLog line, and arms an on-screen banner (`RenderDebugText`, ~8 s) — so the auto-pause reads as "combat started," not a mystery stop.

**v1 stubs / follow-ups.** (a) Halts on **any** new engagement, not only the player's — the engine has no client-side "player faction" concept; "only interrupt *my* battles" is a v2 refinement once that's known engine-side. (b) One salvo of damage lands before the halt (engage + step share a Tick); halting *before* the first salvo needs engage-this-tick / step-next-tick separation (v2). (c) The fine-step gate keys on **present, in-range** state, so a fleet that crosses from out of range to engaged inside a single step (e.g. a long warp resolved under a big Ticklength) is resolved without a pause — a closing-prediction lookahead is v2; the common case (fleets already parked together, e.g. at the same body, or a player stepping carefully through a fight) is fully covered. (d) Because the gate only fine-steps a *new* engagement, an ONGOING fight runs at the player's set step — so a big step resumed mid-battle resolves a lot of it in one click (their choice); to watch it unfold they set a small step. That also means a JOIN that occurs *mid-big-step* (rather than at step start) won't pause until the step ends — same root as (c).

**Connections (Prime Directive):** **Feeds IN** — `InterruptTimeOnNewEngagement` flag; `EnsureInCombat` join event; `fleet.Manager.Game.TimePulse`; `NewEngagementImminent` reads every active system's fleets/combat state. **Feeds OUT** — `MasterTimePulse.RequestCombatHalt` (cancels the sim) + `CombatInterruptPending` (UI notice); `SimulateTimeUntil` caps its per-iteration target only while a new engagement is forming. **Triggers** — the time loop stops (same mechanism as `PauseTime`) and, for the instant a fight is born, runs at 5-second granularity; otherwise the player's set step. **Tests:** `BattleTriggerTests.Tick_NewEngagement_RequestsCombatHalt_WhenInterruptEnabled` (flag on → a new engagement sets `CombatInterruptPending`; asserts the flag defaults off; try/finally so the static flag never leaks) and `NewEngagementImminent_*` (the gate reads true for two hostile un-engaged-in-range and for one-engaged-one-joining; **false for BOTH-already-engaged** — the ongoing fight runs at the set speed — and for friendly-only / no-fleets). The *clock-stops-early* integration is verified **live** (CI's colony harness doesn't reliably auto-fire the battle-trigger hotloop on `TimeStep`, the same reason the combat fixtures drive `Tick` directly).

---

## Fog of war — combat only acts on what you DETECT (added 2026-06-26, detection slice 2)

**The seam that makes detection × weapons real.** The battle trigger used to read *every* fleet present (`GetAllEntitiesWithDataBlob<FleetDB>()`) — omniscient. With `CombatEngagement.RequireDetectionToEngage` on, the engage pass also requires the two hostile fleets to **detect** each other: `FleetDetects(a, b)` checks the per-faction track table (`EntityManager.GetSensorContacts(factionId).SensorContactExists(shipId)`) the sensor scan populates. An undetected hostile can't pull you into a battle — you fight what you *see*.

- **A fight forms once EITHER side detects the other (updated — first-strike, slice 5).** The engage pass now gates on `FleetDetects(a, b) || FleetDetects(b, a)` (was `&&` / mutual). Both fleets enter combat so the resolver has the target present to be shot; the BLIND one simply doesn't shoot back. (The old mutual rule deliberately avoided one-way thrash, but the directed-fire resolver below handles the one-sided case cleanly, so either-detects is now correct.)
- **Live diagnostics (for the CI-blind client play-test).** `CombatEngagement.TickCount` (a `public static long`, Interlocked-incremented at the top of `Tick`) is a liveness counter the client's `SessionLog` heartbeat logs as `[ENGINE] battle-trigger passes M (+delta)` — so a remote review can tell "no battle because nothing's hostile/in-range/detected" from "the trigger never fired on play" (the documented open question). And when fog is on and a NEW engagement forms with one side blind, `Tick` writes a one-time `[Combat] FIRST-STRIKE: …` line (gated on `NarrateToLog` + `RequireDetectionToEngage`, so it's inert in tests and never re-logs once both fleets hold combat state). Sibling counter: `SensorScan.ScanCount` for the detection engine.
- **First-strike — directed fire in the resolver (slice 5).** `StepEngagementGroup` no longer builds a symmetric enemy set. It builds **directed** `targetsOf[i]` (who i can shoot) and `attackersOf[i]` (who can shoot i) via `CanEngageTarget(attacker, target)` = *fog-off → always; fog-on → attacker DETECTS target*. So the side that sees first shoots first, and a fleet that hasn't detected its attacker takes fire without returning it (until its own scan finds the shooter, or it's wiped). **With fog OFF, `CanEngageTarget` is always true, so both directions fill and this is byte-identical to the old symmetric exchange — every existing combat fixture is unchanged.** A fleet drops out only when it can neither shoot anyone NOR be shot (a one-sided aggressor stays in to keep firing; a blind victim stays in to keep taking fire); whole-engagement end is unchanged (hostility-based, so the fight runs until one side is gone).
- **The flag (mirrors `NarrateToLog` / `InterruptTimeOnNewEngagement`).** Default **FALSE** so every existing combat fixture stays deterministic (they don't stand up sensors). **ON in the client as of 2026-06-27** (the developer's deliberate call after a play-test showed an undetected enemy at Venus was visible on a move order): `PulsarMainWindow` sets `RequireDetectionToEngage = true` next to the other combat flags, so visibility + combat are detection-gated in the running game. It changes *when* combat triggers (you wait for a sensor sweep); DevTools → Detection / Fog of War toggles it back off live if needed.
- **Tests:** `BattleTriggerTests.Tick_RequireDetection_NoBattleUntilDetected` — flag on, two hostile *sensor-capable* fleets in range: **no scan → no battle**; fire the scan → **battle**. And `FirstStrike_SeerWipesBlindEnemy_Unscathed` — two EQUAL armed fleets, the enemy BLINDED (sensors shot off — the grave-rung path), fog on: the player wipes it taking **zero** losses (equal forces both firing would be mutual destruction, so player-intact + enemy-wiped is the proof). Both assert the flag defaults off + try/finally so it never leaks.
- **Connections (Prime Directive / cradle-to-grave):** contacts come from sensor **components** (researched/built/installed) → a **destroyed** sensor (the grave rung — **now wired**: `SensorTools.SetInstances` via `ReCalcProcessor` on damage empties the ship's receiver cache so it stops scanning; see `Sensors/CLAUDE.md` → "Grave rung") = you go blind. **Stacks with** the combat interrupt (you're paused when a *detected* battle begins) and doctrine (detect-first = set posture before contact).
- **The fog gate MUST be applied in BOTH `Tick` (engage) AND `NewEngagementImminent` (the interrupt's fine-step gate) — they have to agree (live bug, 2026-06-27).** `NewEngagementImminent` originally checked only hostile + in-range + not-yet-engaged. With fog ON, a fleet parked in range of UNDETECTED hostiles (the player's at Earth, ~384,000 km from a fogged Luna squadron, inside the 1e9 m `EngagementRange`) made it return `true` forever — a battle that can never form keeps looking imminent — so `MasterTimePulse` dropped to 5 s sub-steps permanently and game-time crawled (the developer's "time just stopped moving" report; the engage pass correctly produced **0** `[Combat]` lines, which is how the log pinned it). Fix: `NewEngagementImminent` now applies the same `RequireDetectionToEngage && !(FleetDetects(a,b)||FleetDetects(b,a))` gate as `Tick`. Gauge: `BattleTriggerTests.NewEngagementImminent_FogOn_NotImminentUntilDetected` (fog on + no scan → not imminent; after scan → imminent). **Rule: any change to what counts as an engageable pair must be made in both places, or the clock and the combat disagree.**

---

## Closing distance + Rules of Engagement (Phases 1–3, 2026-06-27)

The auto-resolver is becoming a **closing fight** where range/speed/detection/doctrine decide who can hit whom — the
build plan + locked decisions are `docs/FLEET-COMBAT-CLOSING-DESIGN.md`. Each phase is behind a **default-OFF flag**
(the `RequireDetectionToEngage` pattern), so every pre-existing fixture is byte-identical; the client turns the flags
on when the model is live. All deterministic (no wall-clock/RNG) so fast-forward == watch.

- **Root A — `WeaponProfile.Range_m`** (0 = unbounded, the beam `IsInRange` convention). Beams carry their `MaxRange`;
  railgun/flak/missile rangeless-for-now (flagged). **Root B — `FleetCombat.cs`**: `WarpSpeedFloor`/`DeltaVFloor` (min
  = the fleet moves as one), `FirepowerAtRange(R)` (the firepower-vs-range curve), `SensorReach` (max = parallel sensors).
- **P1 `EnableClosingRange`** — `FleetCombatStateDB.Separation_m` (the gap, seeded from real distance at `StartEngagement`).
  `BuildFireMix(ships, separation)` gates each weapon on `Range_m ≥ gap` (0 separation = no-op). `AdvanceClosing` moves the
  gap toward the FASTER side's preferred range (controller = highest `FleetManeuver` = min evasion over its ships; desired
  = longest finite weapon range). Tunables `ClosingSpeedScale_mps` (0 = freeze), `InitialSeparationDefault_m`. → a fast
  long-range fleet kites, a fast brawler forces the merge.
- **P2 (kiting clock)** — `FleetCombatStateDB.ManeuverBudget` (Δv reserve, seeded from `DeltaVFloor`). Only a fleet with
  budget can be the controller; it spends `ManeuverBurnRate × dt` each step. A burned-out kiter loses control and the enemy
  closes — you can't kite forever. (Interceptors are emergent from P1's speed rule.)
- **P3 `RequireWeaponsReleaseToEngage`** — `EngagementPosture` (WeaponsFree/WeaponsHold/ReturnFire) on `FleetDoctrineDB`
  (the first ROE knob; `FleetDoctrine.PostureOf`/`SetEngagementPosture`, preserved across a doctrine switch). A battle
  erupts only if a side is WeaponsFree; two holding fleets in range sit in a tense **standoff**. Default WeaponsFree, so
  flag-off = proximity engages as before.
- **Gauges:** `FleetAggregationTests` (Roots), `ClosingTests` (P1 range gate / determinism / flag-off / who-dictates;
  P2 kiting clock), `WeaponsReleaseTests` (P3 standoff). **Open (the developer's play-test):** live calibration of the
  closing-rate / burn-rate tunables and the "is standoff-vs-brawl FUN" gut-check.

> **Calibration pass 2026-06-27 — "no combat within weapons range" play-test.** A live fight resolved at a ~10,000 km
> standoff in 2 salvos via **unbounded railguns**, and the gap closed only ~25 km/salvo — so the fleets never reached
> beam/flak range (the `[Combat] closing:` log: `gap 9,975,238m IN RANGE (reach unlimited)`). Two root causes, both
> fixed: (1) **the range-accuracy falloff scaled ONLY by the target's evasion**, so a 0-evasion battleship was hit
> perfectly at any range — added `RangeBaseMiss` (0.9), an evasion-INDEPENDENT base miss in `HitFraction` (still
> scaled by flight-time × (1−Tracking), so beams/guided shrug it off, a dumb slug at long range doesn't) → unbounded
> railguns are now weak at standoff and can't decide a fight before the merge. (2) **the gap barely closed** —
> `ClosingSpeedScale_mps` 100k→**1e6** (10×) so a low-evasion fleet closes the weapon envelope in a watchable handful
> of salvos, and `InitialSeparationDefault_m` 10,000 km→**1,000 km** (missile range) so a fallback-seeded fight OPENS
> at the outer weapon envelope, not 10× beyond it. Net intent: **missile → flak → beam as they close**, the decisive
> blows at weapons range. All three are `public static` dials (provisional, live-tuned). The range term is inert at
> separation 0, so every closing-OFF combat fixture (the sims, triangle, stress lab) is byte-identical; gauges:
> `DodgeResolveTests.HitFraction_RangeDegradesBallistics_EvenVsSittingTarget` (the 0-evasion falloff) + `ClosingTests`.
- **Weapon-range trigger — a battle starts at WEAPON range, not proximity (2026-07-02, the developer's rule).** The
  auto-trigger's range check was a flat `EngagementRange_m` (1 Gm) — so two fleets 752 km apart with 500 km guns
  "entered combat" (interrupt, Battle Report, time-pause) but dealt 0 damage and immediately disengaged (the live
  "they're not actually in battle" report — detection range ≫ weapon range, so proximity triggered a fight nobody
  could fight). Fixed with **`RequireWeaponRangeToEngage`** (default off, **client on**): the engage pass now also
  requires **`WithinWeaponRange(a, b)`** — real `FleetSeparation` ≤ the LONGER of the two fleets' `MaxReach` (the
  long-range side opens the fight; an unbounded weapon reaches any gap; pure `(sep, reachA, reachB)` overload is
  unit-tested). So a fight auto-starts only when someone can actually shoot; otherwise the fleets sit in sensor range
  and the **player closes them (navigation) or issues an explicit Attack order** (`OrderAttack` bypasses the gate).
  Also turned **`RequireWeaponsReleaseToEngage` on in the client** (it was off) — the fire-at-will half: ≥1 side must
  be Weapons Free (default posture, so they fight by default; set Hold Fire on both → standoff). **Both gates are
  mirrored in `NewEngagementImminent`** (the combat-interrupt fine-step gate) — the weapons-release gate was MISSING
  there, which would have re-created the "time crawls at 5 s forever" bug once the flag went on (the same lesson the
  fog gate learned: the imminent-gate and engage-gate MUST agree). Gauge: **`WeaponRangeTriggerTests`** (pure math;
  752 km no-engage → 400 km engage through the real `Tick`; flag-off still engages at 752 km; imminent agreement;
  both-hold-fire not imminent). **This resolves BOTH live issues at once:** #1 (battles starting out of range) and #2
  (the closing model "not closing" — moot, since closing-into-range is now the player's move, not an auto-trigger job).
- **Next: P4 — per-sub-fleet ranges** (each component its own gap, so a fighter wing closes while the capitals hold).

## Gotchas

1. **This engine does not touch the per-pixel damage sim.** Casualties are whole-ship removal driven by strength math, not `DamageProcessor.OnTakingDamage`. Do not wire combat value into the pixel sim — that path is broken and parked for v2.
2. **Combat value is computed once at build (v1).** Recalc-on-damage is a v2 refinement; in v1 a ship is alive at full value or removed whole, so a value cached at build is sufficient.
3. **`ComponentInstancesDB.AllComponents` is `internal`.** Combat code reads it because it lives in the same `GameEngine` assembly; tests reach it via `InternalsVisibleTo("Pulsar4X.Tests")`.
4. **Firepower mixes precise beam J/s with a flat missile stub.** The number is a *relative* strength figure for the salvo math, not a physical unit — don't read absolute meaning into it until missile ordnance energy is wired.
5. **The battle trigger is the one combat piece with live side effects.** `BattleTriggerProcessor` runs every 5 s on every star system and **destroys ships** when hostile fleets fight. It is keyed to `StarInfoDB` (not `FleetDB`, which `FleetOrderProcessor` already owns — one processor per DataBlob type) and returns `>= 1` so it never sleeps. Keep its constructor trivial and `CombatEngagement.Tick` defensive (no throws) — a throwing hotloop processor crashes the whole game loop and CI's `GameLoopSmokeTests`.
6. **Hostility = "different non-neutral faction" by DEFAULT, now SUPPRESSED by diplomacy AND signed treaties (2026-07-01).** `AreHostile` still starts from the v1 rule (different non-neutral factions fight), but it now reads each faction's `DiplomacyDB` (`Pulsar4X.Factions`) and returns *not hostile* when **both** sides are at peace (`AtPeace` helper): a mutual Friendly/Allied **stance**, OR a signed **`NonAggressionPact`/`DefensivePact`** flag (so `Treaties.Propose(NonAggression)` actually stops a fight even at a neutral score — the treaty→combat loop is closed; gauge `DiplomacyIffTests.SignedPact_StopsTheFight`). The defensive pact's "drag me into your ally's wars" entanglement is a separate later slice — here a pact only means the two signatories don't fight each other. Diplomacy can only SUPPRESS the default, never create it: an unmet stranger (no stored relationship) falls straight through to the old result, and a one-sided friendly declaration does NOT disarm you — so every existing combat fixture is byte-identical (nothing writes relationships in those). `AreHostile` is now `internal` (gauge: `DiplomacyIffTests`). Still-open v1 stub: the engine's `EntityFilter.Hostile` sensor-contact requirement is separate (fog is handled by `RequireDetectionToEngage`, not here). Next diplomacy-behavior slices (first-contact event, casus belli, commerce gating) are TESTING-TRACKER C6.
7. **`FleetDoctrineDB` (fleet posture) ≠ `FactionInfoDB.Doctrine` (strategic AI vector).** Same word, different systems. Doctrine effects are read-time multipliers (`BonusesDB` pattern) applied in `StepEngagementGroup` — never bake them into ship stats. `AutoResolve` (pure ship-list) intentionally has no doctrine; it lives in the engagement where fleets do.
8. **`StepEngagement(a, b, dt)` is the n = 2 special case of `StepEngagementGroup` — they MUST stay behaviourally identical for two fleets.** Every existing combat fixture (`BattleTrigger`, `Retreat`, `Performance`, `Dodge`) drives the two-fleet path and is the tripwire: if a change to the group resolver diverges from the old two-fleet exchange (fire division, pool carry-over, combatants-first casualties, retreat/end ordering), those go red. The reduction holds because with one enemy: fire is divided by 1 (= full), each fleet has its own pool, and "fewer than two hostile sides remain" releases both exactly when the loser is wiped/retreats.
9. **`FleetCombatStateDB.OpponentFleetId` is a *representative* opponent, not the sole one (multi-party).** State is per-fleet; a fleet can face many hostiles. The resolver keeps `OpponentFleetId` pointed at one live enemy for the readout — do not treat it as "the" opponent or rebuild pairing from it (the old lower-Id-drives pairing is gone). Membership is "all in-combat fleets in the system."
10. **Multi-party sides = factions, and one system = one engagement (v1 stubs).** Hostility is per-pair (`AreHostile`), so a 3-way already resolves, but fire-division and "side" are first-pass. Every in-combat fleet in a manager is one battle (range ≈ whole system); real weapon-range clustering into distinct simultaneous battles is v2. When sensors/diplomacy land, route sides + clustering through them.
