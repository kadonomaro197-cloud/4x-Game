# Nine franchise units, through the Entity Assembler — a reproduction dossier

> **What this is.** Two starships, five ground units, and two capital ships from three universes (Star Wars, Star Trek, Warhammer 40,000), deep-researched to canon and driven through the reproduction pipeline in `docs/assembler/DESIGNER-DRIVER-PLAYBOOK.md` — the same one that built the Venator and the Sovereign. Every capability is graded by **THE LAW: authenticity to limitations** — traced to a real engine reader (LIVE), or flagged where the fantasy outruns the simulation (PENDING/DEAD). The interactive dossier is the published **Entity Assembler dossier** artifact; this doc is the durable record + the deferred backlog.

**Research:** 7 agents for the first seven; a research→adversarial-verify pipeline for the two capitals (canon re-checked by hand when the live fetch failed). The two capitals add the **min/max pass** — every gate closed arithmetically against the live catalog via a build harness, then the residual filled to the wall.


## The slate

| Unit | Franchise | Host | Role in one line |
|---|---|---|---|
| **ARC-170 Starfighter** | Star Wars (Clone Wars — Legends/Canon composite) | ship | heavy recon-fighter/bomber — sees first, then strikes |
| **Miranda-class Starship (USS Reliant configuration)** | Star Trek | ship | Starfleet light cruiser — phaser-led, torpedo-signature |
| **Tyranid Termagant (fleshborer gaunt)** | Warhammer 40,000 | ground | ranged swarm chaff — win by numbers |
| **Tyranid Genestealer (Purestrain)** | Warhammer 40,000 | ground | elite melee infiltrator — undodgeable in contact |
| **Tyranid Carnifex** | Warhammer 40,000 | ground | heavy bio-monster — a walking super-heavy |
| **Republic Clone Trooper (Phase II) — Grand Army of the Republic line infantry** | Star Wars (Clone Wars / Legends + canon) | ground | sealed, elite-trained line infantry |
| **AT-TE (All Terrain Tactical Enforcer)** | Star Wars (Clone Wars) | ground | six-legged assault gun-walker + troop transport |
| **Prometheus-class Starship (USS Prometheus, NX-74913)** | Star Trek | ship | advanced tactical prototype — a glass cannon, min/maxed for firepower-density |
| **Imperial I-class Star Destroyer** | Star Wars | dreadnought | the do-everything conqueror — a mobile Dreadnought filled to 96% mass |

**Tally across all 9:** **99** capabilities LIVE (incl. gated / host-split / emergent / read-elsewhere) · **26** PENDING (each names the missing wire) · **3** DEAD.


---

## ARC-170 Starfighter

*Star Wars (Clone Wars — Legends/Canon composite) · ship host.* The ARC-170 is a hyperdrive-equipped heavy recon-fighter/bomber built as the SMALLEST ship host in the assembler. Its role stack — ① see first (the recon suite that names it) → ② proton-torpedo alpha strike → ③ jump itself into/out of the fight → ④ survive on shields + evasion + a tail turret → ⑤ endure a 5-day patrol on 3 crew + astromech — trades a pure interceptor's nimbleness for sensors, a hyperdrive, and multi-crew sustainment. The happy result, unlike the Venator (whose carrier signature is PENDING): the ARC-170's OWN signature — reconnaissance — rides a genuinely LIVE detection reader, so the thing that defines it actually works in-sim.

**Canon —** **Physical:** Length ~12.71 m (Legends) — some sources cite ~14.5 m; wingspan ~20 m (S-foils spread). Mass ~22 t (RPG/tech figure). All three numbers are canon-sourced but vary by source; length and mass are the two most-disputed. Height/exact volume not consistently given (estimated small). **Defense:** Deflector/starship shields (good enough to survive brushing a larger fighter force); the S-foil system radiates air-resistance/weapon heat and aids the shielding. Light hull armour. Shield rating is qualitative in canon, not a fixed number. **Mobility:** Twin ion sublight engines, ~1,050 kph in atmosphere (hypersonic, heat-shed by shields/S-foils). Class 1.5 hyperdrive (x1.5 multiplier), astromech-navigated with a 10-jump nav memory — a fighter that self-deploys across systems. Highly maneuverable for its mass but heavier/less nimble than a pure interceptor. **Crew/control:** 3 clone crew — pilot, forward gunner (wingtip lasers), tail gunner (rear turret) — PLUS 1 astromech droid that handles nav/systems. A multi-crew fighter, unusual for the class. CANON.

**★ Signature that must be expressed:** Three signature traits that MUST be expressed: (1) the reconnaissance sensor suite — it is literally in the name; (2) the rear-facing tail-gun turret (defensive rear arc); (3) a hyperdrive-equipped, self-deploying heavy fighter/bomber with its own astromech. Consumables: 5 days. Cargo: 170 kg. Manufacturer: Incom/Subpro.

**Armament (canon):**
- 2× wingtip-mounted MEDIUM laser cannons (unusually large for a fighter) — forward battery, fired by the forward gunner — CANON
- 2× aft-mounted LIGHT laser cannons (one dorsal, one ventral) forming a rear-firing turret worked by the tail gunner — the signature tail-guard — CANON
- 2× proton-torpedo launchers carrying 6 torpedoes total — the bomber punch — CANON

**Chassis / host:** Smallest SHIP host — Starfighter/Fighter tier, one rung below the corvette/escort on the scaling ladder. Mass budget set ~25 t to seat the canon ~22 t of components. The binding constraint is MASS (on a fighter every budget is scarce, so counts are held to the canon minimum). No room for a hydroponics loop → provisions-limited, which HAPPILY reproduces the canon 5-day consumables clock. Carry/mass budget: ~22/25 t used; volume tight, mass-bound.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Recon (see first) | Reconnaissance Sensor Array (deep-search) | 1 | Sensors |
| Fire control | Fire-Control Sensor | 1 | Sensors |
| Forward strafe | Laser Cannon (light energy) | 2 | Weapons |
| Guard the tail | Tail-Turret Laser (light energy) | 2 | Weapons |
| Bomber strike | Proton Torpedo Launcher (Missile Launcher) | 2 | Weapons |
| Shield | Deflector Shield Generator | 1 | Defense |
| Armour | Hull Armour Plating (light) | 1 | Defense |
| Power | Fusion Reactor (small) | 1 | Power |
| Open the hyperspace jump | Capacitor Bank | 1 | Power |
| Reject heat (S-foils) | Radiator / S-foil Heat Sink | 1 | Power |
| Sublight maneuver | Ion Sublight Drive | 2 | Propulsion |
| Self-deploy (FTL) | Class-1.5 Hyperdrive (Warp Drive) | 1 | Propulsion |
| Δv / fuel | Propellant Tank | 1 | Logistical |
| Feed the torpedoes | Ordnance Magazine | 1 | Logistical |
| Cargo | Cargo Bay (170 kg) | 1 | Logistical |
| Endure 5 days | Provisions Store | 1 | Civic |
| Automation / self-nav | Astromech Automation Unit | 1 | Enhancers |
| Crew / operator seat | Cockpit / Command Seat | 1 | Command |

**Gates closed:**
- **mass/carry:** Σ components ~22 t ≤ ~25 t fighter-host budget. MASS is the binding constraint; counts held to the canon minimum (2+2 lasers, 2 launchers, single reactor/shield/hyperdrive).
- **power supply≥draw:** Small fusion reactor sized above the 4 laser draws + shield + warp sustain; margin left for the recon array. WeaponSupply.cs:104 hard gate passes.
- **heat:** Radiator / S-foil heat sink clears the laser heat load so the guns don't throttle (CombatEngagement.cs:714). Canon-honest — S-foils radiate heat.
- **ammo feed:** Ordnance Magazine present → torpedoes fire (MissleProcessor.cs:31). Deliberately shallow (6 torpedoes) — a bomber alpha strike, not sustained fire. Lasers are energy (no magazine).
- **crew/berths:** Cockpit seats the 3 crew + astromech; astromech Crew-Reduction trims the manpower draw. Crew is modeled as a ratio/gate, not per-station.
- **seal:** Fighter is atmosphere- and vacuum-capable (canon 1,050 kph hypersonic). Env-seal is a Chassis stat (GroundUnitAssembly.cs:313 shape); on a ship it's assumed sealed — the distinct env-tag field is PENDING (06 Chassis).
- **detection≥weapon:** The recon array is the SIGNATURE and is deliberately the longest bar — it out-ranges its own guided torpedoes (see-first). This is authentic: the ARC-170 is a scout that shoots, not a gun that scouts.
- **mobility:** 2 ion drives give a high thrust/mass on a ~22 t hull → high emergent Evasion; Class-1.5 hyperdrive + capacitor open the jump; propellant tank supplies a modest burst Δv. Two clocks kept distinct: 5-day provisions deployment ≠ maneuver Δv.

**Emergent (what the assembler computes):** Firepower: LOW — 4 light laser cannons + 2 torpedo launchers, on the order of a few MJ/s, tiny beside a Venator's 143 MJ/s. Correct: it's a fighter, and ①recon > ②strike in the stack. Toughness/HP: LOW — light armour on a 22 t hull; whole-or-dead, one solid capital hit ends it. Evasion: HIGH — this is the fighter's defining emergent stat: small hull + strong thrust/mass drives evasion toward the cap (~0.75–0.90) versus the Venator's ~12%. That single number is what makes it READ as a starfighter and not a small warship. Shields: a small pool (~1–5 MJ). Detection: the longest bar on the ship, reaching past its own torpedoes — the recon signature made numeric.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Reconnaissance sensor suite (THE SIGNATURE) | SensorReceiverAtb band + Threshold_kW → SensorTools.RangeForSignal / detection gate (06 Sensors=LIVE; 02-IO A11). Caveat: the C7 band-match bug (SensorTools.cs:147) exists, but detection RANGE is genuinely read. Unlike the Venator, the ARC-170's own signature is LIVE. |
| ✅ `LIVE` | Forward laser cannons | Firepower Σ → ShipCombatValueDB.cs:524; BuildFireMix CombatEngagement.cs:1350; CombatKernel.cs:228 (06 Weapons, ship+ground). |
| ✅ `LIVE` | Rear tail-turret guns (Firepower) | Same Firepower path as above — the guns contribute damage. |
| 🟠 `PENDING` | Rear tail-turret ARC coverage (the 'guards the tail' trait) | No firing-arc / facing model exists — combat is aggregate/bucketed (AUTO-RESOLVER-GROUND-TRUTH §6.1). The defensive rear arc has no reader; only the raw Firepower lands. |
| ✅ `LIVE` | Proton torpedoes (bomber strike) | Firepower LIVE; guided → IsInterceptable CombatEngagement.cs:1465 → InterceptMissiles:1503 (the only PD-answerable weapon). Ordnance feed MissleProcessor.cs:31. |
| ✅ `LIVE` | Deflector / S-foil shields | Ship shield pool → CombatKernel.ResolveShield / ShipCombatValueDB ShieldCapacity_J (02-IO A2:65). Note the 06 door-split: the Defense door nominally builds ground augments; ship shields ride the ShipCombatValueDB/ShieldAtb path. |
| ✅ `LIVE` | Hull armour | Toughness → ShipCombatValueDB.cs:297,519 (ArmourSoak is ground-only, but the HP/Toughness pool is read ship-side). |
| 🟦 `LIVE-gated` | Fire control | BeamFireControlAtbDB → ShipCombatValueDB.cs:310,326; behind EnableFireControlRange/Tracking (default-off, client-on) — 06 Sensors. |
| ✅ `LIVE` | Class-1.5 hyperdrive (self-deploy) | Warp create/sustain box 1 — WarpMoveCommand.cs:266; sustain WarpMoveProcessor.cs:246; departure gated on EnergyStored ≥ jump cost (Capacitor Bank). |
| 🟪 `EMERGENT` | Sublight evasion (fighter agility) | CalculateEvasion ShipCombatValueDB.cs:571-596,581 (accel = thrust/MassDry). Computed from the finished hull; high because the hull is tiny. |
| ✅ `LIVE` | Astromech automation (crew relief) | Crew-Reduction lowers CrewReq → manpower pool, ShipDesign.cs:242-260. |
| 🟠 `PENDING` | Astromech navigator (10-jump nav memory) | Propulsion 'navigator/quietness/suppression' fields have zero engine readers (grep empty, 02-IO A10; 06 Propulsion PENDING). |
| ✅ `LIVE` | 5-day consumables / provisions endurance | SustenanceProcessor.cs:67; no hydroponics → provisions-limited, reproducing the canon 5-day clock. |
| ✅ `LIVE` | Cargo (170 kg) | CargoStorageAtb.MaxVolume → StorageSpaceProcessor.cs:88 / CargoMath.GetFreeVolume. |
| ✅ `LIVE` | Crew operator seat | CommandBerthAtb.Survivability → SiteHazard incident roll → DestroyCommander, SiteWorkProcessor.cs:297 (LIVE but dormant until a site is spawned). |
| 🟥 `DEAD` | Carried & LAUNCHED as a parasite from a Venator's deck | ParasiteLauncherReady — EventTypes.cs:400, zero emitters/consumers (06 Logistical). As a STANDALONE ship the ARC-170 is fully expressible; as a fighter flown off a carrier's deck in space combat, the launch has no live reader. This is the honest signature caveat for any Star Wars fighter. |
| ✅ `LIVE` | Power supply + reactor heat | TotalOutputMax SustenanceProcessor.cs:60 / WeaponSupply.cs:104; heat WeaponProfile.cs:126 → CombatEngagement.cs:714. |

**Authenticity notes:**
- FAITHFUL — the signature works: the ARC-170 is named for its RECONNAISSANCE, and detection range is a genuinely LIVE engine reader, so the recon array can be made the longest bar (see-first). This is the opposite of the Venator, whose carrier signature (fighter launch) is PENDING — here the defining trait actually reaches the sim.
- FAITHFUL — proton-torpedo bomber punch is LIVE and guided, making it the one weapon enemy point-defense can intercept; the 6-torpedo shallow magazine honestly models an alpha-strike bomber, not a sustained gunship.
- FAITHFUL — a Class-1.5 self-deploying hyperdrive on a fighter is fully expressible (warp create/sustain box 1 + a capacitor to open the jump); the ARC-170 that jumps itself between systems reads correctly.
- FAITHFUL (emergent) — high Evasion falls out of the small hull + strong thrust/mass, so it reads as a nimble fighter (~0.75–0.90) against the Venator's ~12%; shields + light armour are LIVE.
- FAITHFUL — the 5-day consumables clock reproduces naturally: a fighter has no room for a hydroponics loop, so it comes out provisions-limited, exactly the canon figure. S-foils map cleanly to the radiator/heat-sink gate.
- PENDING — the rear-facing tail turret's ARC COVERAGE (its whole point, guarding the six) has no reader: combat is aggregate/bucketed with no firing-arc model. The tail guns still contribute Firepower, but 'covers the rear' is engine-pending.
- PENDING — the astromech's 10-jump NAVIGATOR role has no reader (propulsion navigator field is unbuilt); only its crew-automation role is LIVE.
- DEAD (the honest fighter caveat) — being CARRIED and LAUNCHED off a Venator's flight deck cannot be expressed: ParasiteLauncherReady (EventTypes.cs:400) has zero emitters. Built as its own ship the ARC-170 is complete; as a parasite fighter sortieing from a carrier it can't, and that is the same gap the Venator build flagged from the mothership's end.
- MODEL CAVEAT — per-part masses/damage/power are the assembler's own tuning (to make the mass/power/heat gates behave), not Star Wars figures; only the COUNTS (2 forward lasers, 2 tail cannons, 2 torpedo launchers, 3 crew + astromech, 1 hyperdrive) are canon.

**Sources:** Wookieepedia — Aggressive ReConnaissance-170 starfighter (starwars.fandom.com) · Star Wars Saga Edition Wiki — ARC-170 Starfighter (swse.fandom.com / swse.miraheze.org) · Star Wars RPG (FFG) Wiki — ARC-170 Heavy Starfighter (star-wars-rpg-ffg.fandom.com) · VS Battles Wiki — ARC-170 Starfighter (vsbattles.fandom.com) · Assembler docs cross-checked: docs/assembler/DESIGNER-DRIVER-PLAYBOOK.md, 02-IO-MATRIX.md, 06-OUTPUTS-BY-DOOR.md, VENATOR-BUILD.md

---

## Miranda-class Starship (USS Reliant configuration)

*Star Trek · ship host.* A Federation light cruiser / frigate — the versatile survey-and-patrol workhorse the USS Reliant was pressed into a warship in Star Trek II. Role stack: (1) light-cruiser gunline — phaser banks + the signature dorsal rollbar photon-torpedo pod behind deflector shields, (2) see-first science/sensor suite (it is a survey ship at heart), (3) modest patrol endurance on a small crew. It wins by hitting above its tonnage with torpedoes and sensors; it sacrifices hull, crew depth, and staying power — deliberately a frigate, not a capital.

**Canon —** **Physical:** Length 277.76 m; beam 173.98 m; height 65.23 m; mass 655,000 metric tons. (CANON production/MSD figures per Memory Alpha. NOTE the dispute: the studio-model scale implies a smaller ~237 m ship — the task hint's figure — so length is canon-contested; 277.76 m is the on-record MSD number, ~237 m the model-scale estimate.) **Defense:** Deflector shields as the primary defense (canon, unquantified — estimated as its doctrine). Light structural hull / composite armour — the class trusts shields over plating (on-screen it takes heavy torpedo damage from Reliant-vs-Enterprise, consistent with a light frigate hull). **Mobility:** Warp drive from a single 1,500+ Cochrane core feeding two nacelles; sustained Warp 9.2 for 12 hours (CANON). One impulse system for sublight. More agile than a Galaxy/Sovereign capital for its smaller 277 m frame (estimated). **Crew/control:** 220 officers and crew; 500 personal evacuation limit (CANON).

**★ Signature that must be expressed:** THE ROLLBAR — the distinctive dorsal weapons pod carrying the photon-torpedo launcher, first seen on USS Reliant in The Wrath of Khan and the class's single most iconic feature. The build MUST express torpedoes fed from a dedicated magazine as the signature strike weapon.

**Armament (canon):**
- Six Type-7 phaser emitters / dual phaser banks (CANON) — main energy battery
- Two pulse phaser cannons (CANON) — heavier, faster-cycling pulse energy weapons
- Photon torpedo launchers in the dorsal ROLLBAR pod — 2 on the base Miranda, 4 in the USS Reliant TWOK config (CANON). The rollbar-mounted launcher is the class's iconic signature.
- Deflector shields (CANON, magnitude never given on-screen — estimated)

**Chassis / host:** Light-cruiser / frigate ship-hull tier — an ~8,000 t mass/carry budget (roughly 40% of the 20,000 t capital tier the Sovereign build used, matching the Miranda's ~277 m vs the Sovereign's ~685 m). Ship host = the Chassis door; mass/budget is a LIVE soft gate (EnforceMassBudget defaults false, ShipDesign.cs:54). Small-hull tier keeps the fit honest: it cannot carry a capital's weapon count, which is correct for a frigate.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Main phaser battery | Phaser Bank (energy beam) | 6 | Weapons |
| Pulse phaser cannons | Pulse Phaser Cannon (beam, high rate) | 2 | Weapons |
| Signature rollbar torpedo pod | Photon Torpedo Launcher (guided) | 4 | Weapons |
| Point-defense screen | Point-Defense Battery | 4 | Weapons |
| Deflector shields (primary defense) | Shield Generator | 8 | Defense |
| Light hull armour | Composite Armour (light) | light | Defense |
| Frame / budget | Light-Cruiser Ship Hull | 1 | Chassis |
| Reactor power | Warp Core Reactor | 2 | Power |
| Warp departure buffer | Capacitor / Battery Bank | 2 | Power |
| Heat rejection | Radiator | 3 | Power |
| FTL drive | Warp Drive (nacelle) | 2 | Propulsion |
| Sublight / evasion | Ion (Impulse) Drive | 2 | Propulsion |
| Maneuver fuel | Fuel Tank (cargo store) | 2 | Logistical |
| Detection / science suite | Sensor + Long-Range Sensor | 2 | Sensors |
| Fire control | Fire-Control Sensor | 1 | Sensors |
| ECM | ECM / Jammer | 1 | Sensors |
| Torpedo feed | Ordnance Magazine | 1 | Logistical |
| Crew berthing | Crew Quarters + Life Support | sized for 220 | Civic |
| Medical | Medical Bay | 1 | Civic |
| Command seat | Bridge / Command Berth | 1 | Command |

**Gates closed:**
- **mass/carry (Σ ≤ ~8,000 t budget):** A frigate weapon fit (6 phaser + 2 pulse + 4 torpedo + 4 PD) plus 8 shields, 2 reactors, 2 warp + 2 ion drives, sensors and a 220-crew city fits inside the ~8,000 t light-cruiser tier. Soft gate (EnforceMassBudget default false, ShipDesign.cs:54) — LIVE-gated; the hull deliberately can't carry a capital's count.
- **power supply ≥ draw:** 2 warp-core reactors sized so TotalOutputMax ≥ (6 phaser + 2 pulse beam draw) + warp sustain. Reader SustenanceProcessor.cs:60 / EnergyGenProcessor.cs:47. Torpedoes draw ammo, not power — no power load.
- **heat (radiators ≥ energy-weapon heat):** 3 radiators reject phaser/pulse heat; if unmet the guns throttle (CombatEngagement.cs:714). Sized non-negative.
- **ammo feed (magazine for the torpedoes):** 1 Ordnance Magazine feeds the 4-launcher rollbar pod; ordnance-storage → MissleProcessor.cs:31. The signature weapon's supply wire is LIVE.
- **crew / berths ≥ crew:** Crew Quarters + Life Support sized for the canon 220 complement; PopulationSupportAtbDB → PopulationProcessor.cs:26.
- **seal (spaceworthy hull):** Ship hull is sealed by host type — a ship chassis, not a surface frame; no open-air penalty.
- **detection ≥ longest weapon:** Sensor + Long-Range + Fire-Control sized so detection range exceeds photon-torpedo reach — a survey cruiser sees first. Detection gate SensorTools.cs:145/147; fire-control ShipCombatValueDB.cs:326.
- **mobility (warp + sublight + evasion):** 2 warp nacelles (create/sustain box-1 LIVE) with capacitors covering the departure charge; 2 ion drives give a frigate-appropriate emergent evasion ~0.20 (CalculateEvasion ShipCombatValueDB.cs:581). Maneuver fuel (2 tanks) is a separate clock from patrol deployment.

**Emergent (what the assembler computes):** FIREPOWER: phaser-led ~25 MJ/s (6 banks + 2 pulse + 4 PD) — about HALF a base Sovereign (~59 MJ/s with its 16+14 fit), correct for a light cruiser at ~40% the tonnage. The 4 photon torpedoes add little to this TOTAL because the auto-resolver stubs guided damage at 0.1 MJ/s — their real GJ-scale punch is LIVE-sim only. TOUGHNESS/HP: light — 8 shield generators ≈ 40 MJ deflector pool + thin composite armour folded into Toughness; well below the Sovereign's 120 MJ (min/maxed) wall, and almost no armour, a brittle frigate by design. EVASION: ~0.20, HIGHER than the Sovereign's 0.12 because a 277 m frigate has more thrust-per-mass agility than a 685 m capital (emergent from Chassis volume × Propulsion thrust/mass, CalculateEvasion ShipCombatValueDB.cs:571-596). VS BASE GAME: slots as a light cruiser — ~half a Sovereign's firepower, a third of its shields, but nimbler; above a corvette/escort, well below any capital. It punches above its weight only through torpedoes, and only in the live sim where those torpedoes actually deal their damage.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Phaser banks + pulse cannons (firepower / saturation / range) | ShipCombatValueDB.cs:524 (Firepower sum) → CombatKernel.cs:228 (kernel); range/velocity/tracking CombatKernel.cs:198-202 |
| 🟦 `host-split` | Point-defense phasers (intercept torpedoes/fighters) | LIVE ship-only — CombatEngagement.cs:1465 IsInterceptable → :1503 InterceptMissiles; ground resolver has no intercept step (inert on a ground host) |
| ✅ `LIVE` | ROLLBAR photon torpedoes — guidance & delivery (the signature, in the LIVE sim) | Guided/PD-answerable CombatEngagement.cs:1465; live impact MissileImpactProcessor (directAttack=true since 2026-06-21, delivers kinetic damage ≤1000 m) |
| 🟠 `PENDING` | ROLLBAR photon torpedoes — damage in the AUTO-RESOLVE total | Auto-resolver stubs guided-weapon damage at a flat 0.1 MJ/s (Sovereign-build caveat) — missing wire: guided-weapon damage in the BuildFireMix/CombatKernel firepower sum. The signature punch is under-counted in strength-math battles. |
| ✅ `LIVE` | Deflector shields (primary defense) | Ship ShieldCapacity_J total → CombatKernel.ResolveShield (02-IO-MATRIX A2:65). SEAM flagged: the Defense door itself builds ground GroundAugment shields; on a ship the pool is the assembler's ShieldCapacity total, a different reader per host (06 ledger) |
| ✅ `LIVE` | Light composite armour | Armour thickness ×100 kJ folds into Toughness on the ship path — ShipCombatValueDB.cs:297,519 |
| 🟦 `LIVE-gated` | Hull mass / budget | ShipDesign.cs:281 (hullBudget); soft gate EnforceMassBudget defaults false (ShipDesign.cs:54), client-enforced |
| 🟪 `EMERGENT` | Evasion (frigate agility ~0.20) | CalculateEvasion ShipCombatValueDB.cs:571-596 (Chassis volume × Propulsion thrust/mass), computed by the assembler |
| ✅ `LIVE` | Reactor power ≥ phaser + warp draw | TotalOutputMax → SustenanceProcessor.cs:60 / EnergyGenProcessor.cs:47,80 |
| ✅ `LIVE` | Warp drive (FTL) | Create/sustain box-1: WarpMoveCommand.cs:266 (departure gate) + WarpMoveProcessor.cs:246 (sustain draw); boxes 2-6 pending (correct) |
| ✅ `LIVE` | Warp capacitor / departure buffer | EnergyStoreMax EnergyStoreAtb.cs:37 → EnergyStored charged EnergyGenProcessor.cs:64 → WarpMoveCommand.cs:265-268 |
| ✅ `LIVE` | Radiators / heat rejection | WeaponProfile.HeatPerSecond → heat throttle CombatEngagement.cs:714 |
| ✅ `LIVE` | Ion/impulse thrust (sublight) | Thrust → CalculateEvasion ShipCombatValueDB.cs:581 (accel = thrust/MassDry); also signature 3500 K → detection |
| ✅ `LIVE` | Maneuver fuel | Fuel-storage cargo type → NewtonianMovementProcessor.cs:121 drain |
| ✅ `LIVE` | Ordnance magazine (torpedo feed) | Ordnance-storage → MissleProcessor.cs:31 |
| ✅ `LIVE` | Sensors / detection | Detection gate SensorTools.cs:145; band-match :147 (engine-wide C7 band bug noted, not this build's) |
| 🟦 `LIVE-gated` | Fire-control range + tracking | ShipCombatValueDB.cs:326 (range) / :310 (tracking); behind EnableFireControlRange / EnableFireControlTracking (default off, client-on) |
| 🟦 `LIVE-gated` | ECM / jammer | JammingDivisorAgainst:96 → SensorTools.cs:26; behind EnableJamming |
| ✅ `LIVE` | Crew quarters + life support (220 crew) | PopulationSupportAtbDB.PopulationCapacity:108 → PopulationProcessor.cs:26 |
| 🟠 `PENDING` | Medical bay (+health stat) | No consumer — MoraleInputs has no health field; LegitimacyProcessor reads morale+war only (06 Civic). Berthing benefit real; the +health number is unread |
| ✅ `LIVE` | Command seat (bridge / agency) | The seated officer is the delegate that fights the ship (Table C1 Command) |
| 🟥 `DEAD` | Ship bridge 'Console Space' seat-count dial | Reader gated on ColonyInfoDB a ship lacks — 1-console and 20-console bridge both yield one seat (06 Command dead-end) |

**Authenticity notes:**
- EXPRESSED FAITHFULLY: phaser banks + pulse cannons as energy beams (firepower/saturation/range all LIVE), deflector shields as the primary defense (ship ShieldCapacity total, LIVE), light armour folded into Toughness, warp + impulse mobility with a frigate-appropriate ~0.20 evasion (higher than the Sovereign's 0.12), a see-first sensor/science suite past its own weapon range, and the 220-crew city — all reach the sim.
- THE SIGNATURE ROLLBAR — expressed as 4 photon-torpedo launchers fed by a dedicated ordnance magazine (Logistical→Weapons guided). The physical 'rollbar' is cosmetic; the mechanic (a distinct torpedo battery) is what matters and it is LIVE in the live sim: missiles guide and impact (directAttack=true since 2026-06-21, MissileImpactProcessor delivers kinetic damage).
- PENDING / HONEST GAP on the signature: the AUTO-RESOLVER stubs torpedo/missile damage at a flat 0.1 MJ/s (same caveat the Sovereign build flagged) — so in an auto-resolved battle the Miranda reads phaser-led, its torpedo punch under-counted. The rollbar's real GJ-scale hit lands only in the LIVE tactical sim, not the strength-math total. Missing wire: guided-weapon damage in the auto-resolve firepower sum.
- CANON DISPUTE flagged, not hidden: length is contested (277.76 m MSD figure vs ~237 m model-scale estimate); shield and sensor magnitudes are never quantified on-screen and are estimated to the ship's doctrine, not invented as canon.
- DEAD/PENDING minor: the ship bridge 'Console Space' dial dead-ends on a ship host; the Medical bay's health stat has no reader (berthing is real, the +health number is not) — both flagged, neither sold as working.

**Sources:** Memory Alpha — Miranda class (https://memory-alpha.fandom.com/wiki/Miranda_class): length 277.76 m, beam 173.98 m, height 65.23 m, mass 655,000 t, crew 220 / evac 500, warp 9.2 for 12 hr, six Type-7 phaser emitters + two pulse phaser cannons + two photon torpedo launchers · Memory Alpha — USS Reliant (NCC-1864) (https://memory-alpha.fandom.com/wiki/USS_Reliant_(NCC-1864)): the TWOK rollbar torpedo-pod configuration · Ex Astris Scientia — Miranda Class Variations (https://www.ex-astris-scientia.org/articles/miranda.htm): rollbar-with-torpedo-launcher on later Miranda variants; the Reliant loadout (6 dual phaser banks / 2 pulse phaser cannons / 4 photon torpedo launchers) · docs/assembler/DESIGNER-DRIVER-PLAYBOOK.md (REPRODUCTION mode + THE LAW) · docs/assembler/06-OUTPUTS-BY-DOOR.md (per-door verified reader/PENDING ledger) · docs/assembler/02-IO-MATRIX.md (per-door sim-reaching variables + assembler contract) · docs/assembler/SOVEREIGN-BUILD.md (Star Trek reference build + the torpedo auto-resolve stub caveat)

---

## Tyranid Termagant (fleshborer gaunt)

*Warhammer 40,000 · ground host.* A Termagant is Tyranid chaff: a fast, fragile, roughly 2-metre bio-organism that shoots a living Fleshborer bio-weapon and swarms elite foes to death by weight of numbers, so long as the Hive Mind's synapse holds it together. The build reproduces it on the tiny swarm-frame with a brood-sized batch dial (UnitsPerBuild), a Fleshborer + rending-claws loadout, light chitin, and an agility dodge augment. Role stack: (1) be cheap horde chaff, (2) put short-range bio-fire and teeth downrange, (3) dodge/scuttle to survive, (4) die easily and be spawned again — and it explicitly SACRIFICES per-unit firepower, toughness, and any independent will, since the one trait it is most defined by (falling to feral instinct without a nearby synapse creature) is the one the engine cannot yet express.

**Canon —** **Physical:** Canon: 'slightly more than two metres from head to tail' (Lexicanum) — a hunched, quadi-bipedal exoskeletal gaunt. Mass is NOT given in canon; ESTIMATED ~40-60 kg (a light, hollow-boned swarm organism). Bred/spawned in enormous numbers aboard hive ships and by a Tervigon at the front (living transport + spawning station). Scale in-build: mapped to the ~5-mass 'Swarmling Frame' (BaseStrength 30 carry, BaseHP 40, Size 1). **Defense:** Light chitin exoskeleton — explicitly 'ineffective against more advanced foes'. No shields, no regeneration on the individual (the HIVE reclaims and re-spawns the biomass — survivability is at the swarm level, not the model). Survives by speed, agility, and numbers; a Termagant is meant to die. **Mobility:** Fast and agile bipedal scuttle; no vehicle drive. Delivered en masse to the front by a Tervigon (living transport/spawning) or bred aboard hive ships, so it 'arrives' already in overwhelming numbers rather than by fast march. **Crew/control:** NOT crewed and NOT independently willed — a living organism run by the Hive Mind through SYNAPSE creatures (Warriors, Tervigon, Tyrant). Within ~synapse range it is Fearless and coordinated; OUTSIDE it, it must test against instinct and can rout, freeze, or turn on itself (Instinctive Behaviour). 'Shadow in the Warp' additionally degrades nearby enemy morale/psykers.

**★ Signature that must be expressed:** SYNAPSE DEPENDENCE is THE signature — the Termagant is a fearless, lethal swarm instrument only while a synapse creature is near; cut the synapse and the brood reverts to feral instinct and can break. The secondary signature is swarm-by-numbers: individually trivial, collectively able to drown an elite, with chitin that bounces off nothing advanced.

**Armament (canon):**
- Fleshborer (canon standard) — a symbiote bio-weapon that fires a stream of living borer-beetles which chew through flesh and light armour; short-ranged assault weapon; 'living ammunition' that is self-replenishing (no reload)
- Devourer (variant) — fires ravenous leech-maggots; heavier anti-infantry bio-fire
- Spinefists (variant, Spinegaunt) — paired symbiote pistols spitting chitin spines; rapid, very short range
- Spike Rifle / Strangleweb / Shardlauncher (situational variants) — same forelimb symbiote slot, different bio-payload
- Claws & teeth (natural) — minor melee; a Termagant is a poor close-combatant (that is the Hormagaunt's role)

**Chassis / host:** Swarm-frame ('Swarmling Frame', UniqueID swarm-frame, installations.json:119993) — the tiny/cheap/fragile organism-body frame, chosen because its own description IS a Termagant ('a tiny, cheap, fragile organism/drone frame — the swarm body ... pair with the Count bunch'). Ground host, Personnel carry-class, Foot locomotion. Budget: carry-strength 30 (the mounted-mass budget), BaseHP 40, frame mass 5, per-item cap = 30 x 0.5 = 15. The horde is expressed with the assembler's UnitsPerBuild dial set to a brood (20), so one build order stamps out a full brood at 20x cost. Substrate is Mechanical because the organic/bio substrate axis does not exist in the engine (chassis substrate is PENDING, 01-IO-chassis.md D-notes) — the 'bio' is cosmetic.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Cheap fragile swarm body + brood batch | swarm-frame (Swarmling Frame) + UnitsPerBuild=20 | 1 frame per gaunt; UnitsPerBuild 20 = one brood per build | Chassis |
| Fleshborer ranged bio-fire | ground-rifle reskinned 'Fleshborer' (GroundWeaponAtb, Ballistic) | 1 | Weapons |
| Rending claws / teeth (melee secondary) | claw-weapon ('Rending Claws', GroundWeaponAtb, Melee) | 1 | Weapons |
| Agility / survival-by-dodge | adrenal agility augment (GroundAugmentAtb, EvasionBonus) | 1 | Enhancers |
| Light chitin armour | chitin plating (GroundArmorAtb, plain plate) | 1 | Defense |
| Synapse control (the brood driven as one) | GroundFormation + a synapse-node LeaderUnitId + GroundTacticalBrain | brood grouped into 1 formation under a synapse leader | Command |
| Ride to the front / be deployed | swarm-frame CarryClass=Personnel | 1 (frame property) | Logistical |

**Gates closed:**
- **mass / carry:** used = Fleshborer 10 + claws 2 + chitin 5 + augment 6 = 23 <= carry budget 30. PASS. GroundUnitAssembly.Compute carry gate (GroundUnitAssembly.cs:255).
- **per-item cap:** cap = 30 x 0.5 = 15; heaviest item is the Fleshborer at 10 <= 15. PASS (GroundUnitAssembly.cs:109,225). Nothing on this frame is a 'too heavy to shoulder' item.
- **power supply >= draw:** native bio-weapons draw 0 W (WeaponSupply.PowerDraw_W=0 for a plain GroundWeaponAtb) => demand 0 <= supply 0. PASS with NO reactor — authentic: a Fleshborer needs no power core.
- **ammo feed:** native ground weapons return DrawsAmmo=false (WeaponSupply.cs:57) => no magazine required. PASS — the 'living ammunition' never needs a magazine, matching canon self-replenishment (happy accident of the native-weapon path, not a modeled bio-regen).
- **crew / berths:** swarm-frame CrewReq 0 (installations.json) — the gaunt IS the organism; no crew or berth. N/A by design.
- **seal:** DELIBERATELY OPEN — no GroundSealAtb mounted, so on airless/toxic worlds the gaunt bleeds via E4 attrition (PlanetEnvironmentFactory Vacuum/ToxicAtmosphere). A basic Termagant is not void-adapted; a sealed variant would add sealed-systems (GroundSealAtb).
- **detection >= weapon:** Fleshborer reaches ~500 m (hex 1); combat is co-located/adjacent so no long-range detection is needed. No ground-radar mounted — chaff does not scout; the hive's larger bioforms + synapse supply awareness. PASS trivially for a short-ranged unit.
- **mobility:** Foot baseline march (GroundMobility). NO locomotion drive — a Locomotion Drive is mass ~100 and will not fit the 30-carry swarm frame — so 'fast/agile' is expressed as EVASION (dodge), NOT as elevated march speed. Honest limitation, flagged.

**Emergent (what the assembler computes):** Per gaunt: Attack = 40 (Fleshborer) + 20 (claws) = 60, split by W2 into 40 at hex 1 and 20 at hex 0; HP = frame 40 + chitin 10 = 50 (no toughness mult); Evasion ~0.35; Defense (flat soak) 3; Penetration 0, PerShotEnergy 0 (one chip-shot). Versus the base ground units (Infantry 100/10/500, Armor 140/15/700, Artillery 160/5/400) a single gaunt is markedly WEAKER (0.6x an Infantry's Attack, 0.1x its HP) but DODGIER (evasion 0.35 vs 0). A brood build (UnitsPerBuild 20) fields 20 bodies x (50 HP, Attack 60) at 20x cost — ~1200 aggregate Attack across 20 fragile chip-sources. Against a heavily-armoured, high-nature-resist target the many small sources are each flat-soaked and mostly bounce (GroundDamageMatrix per-source soak x low PerShotEnergy), so it must WIN BY VOLUME or by a synapse-boosted heavier beast landing the alpha — reproducing 'chitin ineffective vs advanced foes / needs numbers' as an emergent, not a scripted, outcome.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Cheap expendable brood (UnitsPerBuild + swarm-frame) | GroundUnitDesign.OnConstructionComplete raises Max(1,UnitsPerBuild); cost x N at GroundUnitAssembly.cs:327-332; frame mass constant read at GroundUnitAssembly.cs:98 |
| ✅ `LIVE` | Fleshborer ranged fire (Attack + reach) | Attack sum GroundUnitAssembly.cs:141 -> Firepower ShipCombatValueDB.cs:524 / kernel CombatKernel.cs:228; range gate WeaponReaches CombatKernel.cs:174 (06-OUTPUTS Weapons) |
| ✅ `LIVE` | Fleshborer needs no power / no magazine | WeaponSupply.PowerDraw_W=0 + DrawsAmmo=false for a native GroundWeaponAtb (WeaponSupply.cs:57,73) — the power/ammo gates pass with nothing mounted |
| ✅ `LIVE` | Rending claws as a distinct 2nd weapon in its own range band (W1/W2) | per-weapon mount GroundUnitAssembly.cs:151; per-band fire in ResolveRegionCombat (GroundForcesProcessor, W2) |
| ✅ `LIVE` | Agility / dodge (evasion augment) | EvasionBonus -> design.Evasion GroundUnitAssembly.cs:189 -> CombatKernel.HitFraction dodge (06-OUTPUTS Enhancers) |
| ✅ `LIVE` | Light chitin armour (HP + flat soak) | GroundArmorAtb HP/Defense GroundUnitAssembly.cs:177-178 -> ArmourSoak CombatKernel.cs:294 (06-OUTPUTS Defense, ground-only) |
| 🟪 `EMERGENT` | Swarm-vs-armour: many small sources bounce, numbers win | computed from the whole brood: flat-per-source ArmourSoak x PerShotEnergy shotCount (CombatKernel BurstShotCount) — falls out of the assembled units, not a stored dial |
| 🟦 `LIVE-gated` | Hive-Mind control: brood driven as one formation | GroundFormation grouping (GroundForces.CreateFormation) + GroundTacticalBrain.Run behind EnableGroundTacticalAI (default off, flipped on for menu games in NewGameMenu) |
| 🟠 `PENDING` | SYNAPSE DEPENDENCE — rout / feral instinctive behaviour when the synapse node dies or is out of range (THE signature) | NO engine wire. No per-unit combat morale/synapse field on GroundUnit (06-OUTPUTS Enhancers 'Morale/fury' = PENDING; grep GroundCombat morale/fury/rout = comment-only). Formations do the OPPOSITE — 'Leader loss = reassign, no penalty' (GroundForcesProcessor.MaintainFormations). Shadow-in-the-Warp aura -> Aura door PENDING (zero engine matches, 06-OUTPUTS Aura). Missing wire: GroundUnit.SynapseLink/Morale field + a proximity check to a synapse component/leader in GroundForcesProcessor + an instinctive-behaviour/rout effect (+ optionally the Aura pass for Shadow in the Warp) |
| 🟦 `LIVE-gated` | Rout-when-losing (partial echo of the above, but wrong trigger) | GroundTactics.DecidePosture 'LOSING HARD -> retreat' behind EnableGroundTacticalAI — a rout-like behaviour, but keyed to combat ODDS, not synapse presence; it is NOT the synapse dependency and does not fire on losing the synapse node |
| ✅ `LIVE` | Ride to the front in a troop bay | CarryClass Personnel -> GroundTransport reads GroundBayAtb (06-OUTPUTS Logistical:249 troops -> GroundTransport.cs:40) |
| ✅ `LIVE` | Cradle-to-grave loss (killed whole; brood softenable by bombardment) | whole-or-dead removal in ResolveRegionCombat; orbital softening ApplyGroundBombardment via DamageProcessor.OnColonyDamage (GroundCombat/CLAUDE.md Capture) |
| ✅ `LIVE` | Standing upkeep (a brood costs money as it stands) | assembled unit sets UpkeepCredits = Mass x 0.1 (GroundUnitAssembly.cs:299) -> GroundUpkeep.BillIfDue monthly; a brood bills 20x (per-unit) |
| 🟠 `PENDING` | March SPEED as 'fast' (elevated km/h) | no drive fits the 30-carry frame (Locomotion Drive mass ~100), so march speed stays Foot baseline; agility is expressed only as evasion. Missing wire: a light/free locomotion factor for a bare organic frame (or scaling the frame Foot mode) — GroundMobility.SpeedMultForUnit |
| 🟠 `PENDING` | Organic / bio substrate (self-feeding, regenerating, no crew) | chassis substrate axis does not exist as engine state (01-IO-chassis.md — all four frames are Mechanical); the 'bio' is cosmetic. Missing wire: a Substrate member on the chassis atb + its feed/regen/no-power requirements |
| ✅ `LIVE` | Devourer / Spinefists / etc. weapon swaps | same GroundWeaponAtb with different Attack/Range/Mode — the generalise-by-function payoff (GroundWeaponAtb one attribute, no bespoke weapon types) |

**Authenticity notes:**
- FAITHFUL: the swarm identity is expressed structurally, not by a stat fudge — a near-free swarm-frame (mass 5, HP 40) plus a brood-sized UnitsPerBuild that multiplies cost, so 'thrown in hundreds' is a real economic choice, and the flat-per-source armour math makes the brood bounce off elite plate exactly as canon ('chitin ineffective vs advanced foes; needs numbers').
- FAITHFUL: the Fleshborer's 'living ammunition, never reloads, no power pack' comes out correct for free — a native ground weapon draws 0 power and needs no magazine, so the bio-weapon gate closes with nothing bolted on.
- FAITHFUL: multi-weapon range bands (W1/W2) reproduce a gaunt's Fleshborer reaching at ~500 m while its teeth only bite in contact — one build, two distinct weapon bands.
- FAITHFUL: 'fast, agile, survives without armour' is modeled as EVASION (dodges aimed fire) with the honest asymmetry that 'saturation beats dodge' — so the gaunt scuttles under rifles but is shredded by blast/artillery, which is exactly how gaunts die on the table.
- PENDING — THE SIGNATURE CANNOT BE EXPRESSED: synapse dependence. There is no per-unit morale/synapse field, and the formation system does the literal opposite (leader loss = seamless reassign, no penalty). A Termagant that never breaks when cut off from the Hive Mind is a Termagant with its defining trait removed. Needs: a GroundUnit synapse-link/morale field + a proximity-to-synapse check in the ground processor + an instinctive-behaviour/rout effect (and the Aura pass for Shadow in the Warp). Flagged, not sold.
- PENDING — partial-only: 'rout when losing' exists (GroundTactics retreat-on-bad-odds) but it fires on combat odds, not on losing the synapse node, so it is a different mechanic wearing the same word.
- PENDING — cosmetic gaps: the organic/bio substrate axis and the Tervigon-as-living-transport/spawner are not modeled (frame builds as Mechanical; deployment uses a normal troop bay). 'Fast' march speed is capped at Foot baseline because no drive fits the tiny frame — agility survives only as dodge.
- ESTIMATED vs CANON: length ('slightly more than two metres') is canon; mass (~40-60 kg) and all in-build numbers (Attack 40/20, HP 50, Evasion 0.35, brood size 20) are ESTIMATED/derived from role + the base-mod stat band and the assembler gates — Warhammer gives no engineering figures for a single Termagant.

**Sources:** Lexicanum — Termagant (size 'slightly more than two metres head to tail'; role; weapon options) · Lexicanum — Gaunt (Tyranid) (fleshborer standard arm; expendable warrior organisms; numbers over fragility) · Lexicanum — Fleshborer (bio-weapon fired by Termagants/Gargoyles) · Lexicanum — Devourer (bio-weapon; leech-maggot rounds; carried by Termagants among others) · Warhammer40k Fandom — Termagant (fast/agile/cunning; chitin 'ineffective against more advanced foes'; spawned to hunt hive-ship passages) · Warhammer40k Fandom — Synapse creatures (synapse conduits override instinct; without synapse most Tyranids revert to feral state) · 1d6chan — Termagant (horde chaff; '38 Termagants ~ a Telemon Heavy Dreadnought'; needs numbers to be borderline effective) · Wahapedia — Termagants (10th ed. unit datasheet; brood/weapon options) · Bell of Lost Souls / Frontline Gaming — Tyranid Synapse & Instinctive Behaviour (Fearless in synapse; Leadership tests / Feed result outside it; Shadow in the Warp)

---

## Tyranid Genestealer (Purestrain)

*Warhammer 40,000 · ground host.* A Purestrain Genestealer built on the human-frame in the Entity Assembler as an elite melee shock-infiltrator: rending-claw melee, extreme speed, lightning-reflex dodge, void-hardened chitin, raised as a brood (squad). Role stack — ① close and shred in melee → ② reach melee via speed/ambush → ③ survive the approach by dodging (not armour) → ④ chitin resilience → ⑤ brood/synapse coordination → ⑥ subvert a population (the Kiss). It out-hits and out-evades line infantry and its claws are UNDODGEABLE in contact; the honest sacrifice is that the assembler cannot express the actual armour-SHRED (rending), the synapse link, infiltration, or the Genestealer's Kiss — those are PENDING for the reasons named below.

**Canon —** **Physical:** Bipedal, stands in a perpetual crouch ~1.8-2.0 m tall; dense diamond-hard chitin; TWO pairs of arms — one humanoid pair, one pair of overdeveloped rending claws — driven by hypertrophied musculature and steel-like tendons. Mass is not hard-canon; ESTIMATED ~150-250 kg of muscle and chitin (invented/estimated). Tabletop scale: single-model elite that fields in broods of ~5-20. **Defense:** Tough chitin carapace (MODERATE — tabletop Toughness ~4, ~5+ armour save); NO energy shield. Survives mainly by speed and reflexes (an invulnerable/dodge save in several editions) and by multiple wounds, not by heavy plating. The classic 'survives by dodging, not armouring' xenos. **Mobility:** Extremely fast — among the fastest infantry (tabletop Move ~8in, high threat range); agile; INFILTRATES (deploys forward / from ambush) and often boards via Space Hulks. Closes the gap in a single bound. **Crew/control:** None — an autonomous bio-organism. It is 'manned' by the Hive Mind: a synapse creature / the broodmind directs it, and it acts with cunning purpose while linked. Comes in broods.

**★ Signature that must be expressed:** Rending claws that shred armour + INFILTRATION + the SYNAPSE/broodmind link + the GENESTEALER'S KISS (implants a genome that grows a hidden cult inside a population) — the archetypal Space Hulk boarder.

**Armament (canon):**
- Rending Claws (a.k.a. Genestealer claws and talons) — short claws tipped in extremely dense diamond-hard chitin; multiple attacks per model; rip open ceramite, power armour and light vehicles as well as flesh/bone. Tabletop: high number of attacks, strong weapon skill, Rending / high-AP (canon armour-shred).
- NO ranged weapon — a pure melee organism; its entire threat is contact.

**Chassis / host:** human-frame (GroundChassisAtb: BaseStrength 100 carry-budget, BaseHP 200, Foot locomotion, Personnel carry-class) — a Genestealer is bipedal and roughly human-sized, so the human frame is the honest chassis (NOT the swarm-frame: a Purestrain is a distinct elite killer, not a tiny throwaway body). The brood is expressed with UnitsPerBuild = 8 (one build stamps out a brood; cost x8). Carry budget starts at 100 but the hyper-musculature augment raises it to ~1000 — which is exactly the canon reason it can wield heavy rending claws and still move fast. Total build mass ~1074 kg (frame 20 + parts).

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Rending-claw melee | Rending Claws (claw-weapon → GroundWeaponAtb, Mode=Melee, Range 0/Range_m 0, Attack dialed to 180) | 1 | Weapons |
| Agility + musculature | Hyper-musculature augment (GroundAugmentAtb, patterned on power-armor: StrengthBonus +900, EvasionBonus +0.55, ToughnessBonus +0.2, Shield 0, Mass ~40) | 1 | Enhancers |
| Speed / fast close | Bio-drive (ground-locomotion → GroundLocomotionAtb, SpeedFactor 1.8, RoughHandling 0.6, Amphibious 0) | 1 | Propulsion |
| Chitin resilience | Chitin carapace (GroundArmorAtb: HP 150, Defense 8, Mass 40) | 1 | Defense |
| Void-hardening | Void-hardened seal (sealed-systems → GroundSealAtb, Sealing 0.9) | 1 | Defense |
| Vanguard recon | Sensory radar (ground-radar → GroundSensorAtb, Range 200 km) | 1 | Sensors |
| Purestrain veterancy | Purestrain instinct-cadre (ground-training-cadre → GroundTrainingAtb, TrainingMultiplier 1.5) | 1 | Enhancers |

**Gates closed:**
- **mass/carry:** Σ used = claws 162 + augment 40 + drive 288 + carapace 40 + seal 94 + radar 100 + cadre 150 = 874 ≤ capacity 1000 (base 100 + musculature StrengthBonus 900). Per-item cap = 0.5*1000 = 500; heaviest item is the drive at 288 ✓. The StrengthBonus augment is what CLOSES this gate — canon-faithful.
- **power supply≥draw:** Trivially closed: NO energy weapons (claws are Melee), so EnergyDemand_W = 0, no reactor needed (0 ≥ 0). A Genestealer needs no power plant — honest.
- **ammo feed:** Trivially closed: Melee weapon → DrawsAmmo false → no magazine required. A bio-melee organism carries no ammo — honest.
- **crew/berths:** human-frame CrewReq 0 (surface-unit frames author 0); the assembler carry gate does not hard-gate crew for ground units (the crew gate lives at construction, 06-Chassis). Closed/NA — a Genestealer is uncrewed.
- **seal:** Sealing 0.9 → EnvironmentalResistance{Vacuum:0.9, ToxicAtmosphere:0.9} → survives the airless/toxic worlds and Space Hulks the E4 attrition step would otherwise bleed it on.
- **detection≥weapon:** Radar 200 km reveal ≥ melee range 0 — trivially satisfied; the recon is for the scout role, not for targeting.
- **mobility:** SpeedFactor 1.8 → ~752 km/h march + Evasion 0.55 ≥ 0.5 → GroundRole.Screen (leads to contact). It closes the gap fast and is classified as the charging screen — the Genestealer maneuver.

**Emergent (what the assembler computes):** Attack ≈ 270 (claw 180 × cadre 1.5) vs base-mod infantry 100 — ~2.7× a line trooper, and UNDODGEABLE in melee. HitPoints ≈ 630 ((frame 200 + carapace 150) × toughness 1.2 × cadre 1.5) vs infantry 500 — resilient but no tank (armour 700). Evasion 0.55 vs infantry ~0 — the standout: very hard to shoot on the approach (dodges ballistic/energy). Speed ≈ 752 km/h; DamageType Melee; Role Screen (closes to contact). Net: an elite melee shock unit that out-hits and out-evades line infantry and reliably reaches contact — but has NO ranged answer, and (the honest gap) WITHOUT penetration its claws win by undodgeable VOLUME, not by cracking heavy flat-armour the way canon rending does.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Rending-claw melee (high, undodgeable Attack) | GroundWeaponAtb.Attack → GroundUnitAssembly.cs:141 (Σ Attack) → resolver FireWeaponAtReachable → CombatKernel (06 Weapons DamagePerSecond LIVE, CombatKernel.cs:228); Melee mode → NatureDeliveryFor makes it undodgeable (GroundCombatant). |
| 🟠 `PENDING` | RENDING = armour-shred (penetration / per-shot alpha) | Penetration + PerShotEnergy exist ONLY on the monolithic GroundUnitAtb (base-mod path); they are NOT dials on GroundWeaponAtb and NOT set by GroundUnitAssembly.ToGroundUnitDesign — so an assembler-built claw yields GroundUnit.Penetration=0. Missing wire: add Penetration/PerShotEnergy to GroundWeaponAtb + carry them through the assembler. This is the #1 authenticity gap — the very thing 'rending' means. |
| ✅ `LIVE` | Agility / dodge (Evasion) | GroundAugmentAtb.EvasionBonus → GroundUnitAssembly.cs:189 → design.Evasion → GroundUnit.Evasion → CombatKernel.HitFraction (06 Enhancers EvasionBonus LIVE). |
| ✅ `LIVE` | Speed / fast close (SpeedFactor) | GroundLocomotionAtb.SpeedFactor → GroundMobility.cs:54 → Speed_kmh GroundForcesDB.cs:656 → march/closing step (06 Propulsion Ground SpeedFactor LIVE). |
| ✅ `LIVE` | Hyper-musculature (carry-budget raise) | GroundAugmentAtb.StrengthBonus → GroundUnitAssembly.cs:105-107 (capacity +=) (06 Enhancers StrengthBonus LIVE — the bootstrap that lets the claws fit). |
| ✅ `LIVE` | Chitin toughness (armour HP + flat Defense) | GroundArmorAtb HP/Defense → GroundUnitAssembly.cs:177-178 → HitPoints/Defense → ArmourSoak CombatKernel.cs:294 (06 Defense Armour LIVE). |
| ✅ `LIVE` | Purestrain veterancy (TrainingMultiplier) | GroundTrainingAtb.TrainingMultiplier → GroundUnitAssembly → design → GroundForces.RaiseUnit bakes Attack + MaxHealth (06 Enhancers TrainingMultiplier LIVE, then a readout). |
| 🟦 `host-split` | Void-hardening (survive vacuum / Space Hulk) | GroundSealAtb.Sealing → GroundUnitAssembly → EnvironmentalResistance{Vacuum,ToxicAtmosphere} → GroundForcesProcessor E4 attrition. LIVE on GROUND only (a ground-resolver reader; inert on a ship host). |
| ✅ `LIVE` | Vanguard recon (reveal the ground) | GroundSensorAtb.Range_km → GroundSensors.RevealFromUnits → PlanetRegionsDB.RevealRegionFor (read off the unit's backing component store). |
| ✅ `LIVE` | Brood cohesion (formation + leader) | GroundFormation / GroundFormationDoctrine AttackMult/DamageTakenMult read per-unit in ResolveRegionCombat; leader-loss = reassign (the fleet echo). Gives cohesion — but NOT the synapse buff below. |
| 🟠 `PENDING` | Synapse / hive-mind BUFF (link boosts the brood; morale/fury) | No unit-combat morale/fury field (06 Enhancers Morale/fury PENDING) and NO Aura door (06 Aura: zero engine matches, all PENDING). Missing wire: an aura-pass neighbour sweep + a per-unit morale/buff field so a synapse creature's presence raises nearby broods (and its death drops them). |
| 🟠 `PENDING` | Infiltration / ambush first-strike deploy | No ground stealth / signature-suppression / ambush-deploy wire. SpreadNewlyContestedRegions always opens a gap at the HOLDER's range (M3), and fog detection has no per-unit stealth dial to reduce it — so an infiltrator cannot choose to open the fight already inside melee range. Missing wire: a ground signature-suppression field + an ambush-deploy override of the initial spread. |
| 🟠 `PENDING` | Genestealer's Kiss / cult implantation (subvert a population) | No reader anywhere. Nothing in the engine converts a colony's population or spawns hybrid/cult units from an enemy world. Closest DESIGN is the espionage 'sow-unrest' catalog (docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md), unbuilt and not wired to ground units. Missing wire: a population-conversion/reproduction mechanic. |

**Authenticity notes:**
- FAITHFUL — Undodgeable melee: the claw is Mode=Melee, which GroundCombatant.NatureDeliveryFor maps to an undodgeable kernel profile, so 'you cannot dodge a Genestealer in contact' falls straight out of the engine (LIVE).
- FAITHFUL — Survives by dodging, not armour: high Evasion (0.55) + modest carapace exactly reproduces the canon T4/Sv~5+/lightning-reflexes profile — it out-lasts fire by being hard to hit on the approach, and the resolver reads Evasion via CombatKernel.HitFraction (LIVE).
- FAITHFUL — Hyper-musculature is load-bearing, literally: the StrengthBonus augment is what raises the carry budget (100→1000) so the heavy rending claws fit — the mechanical echo of the canon 'overdeveloped musculature and steel-like tendons.' The gate CLOSES because of it (LIVE).
- FAITHFUL — Void boarder: the seal (Sealing 0.9 → Vacuum/ToxicAtmosphere resistance) lets it hold an airless Space Hulk, the classic boarding role (LIVE, ground-only).
- FAITHFUL — Vanguard scout: a mounted radar reveals the ground each tick — the canon 'advance ahead of the Hive Fleet to pinpoint worlds' role (LIVE). Brood = UnitsPerBuild 8 + a formation (LIVE cohesion).
- PENDING — the actual RENDING (armour-shred): Penetration/PerShotEnergy are not assembler-part dials (only on the monolithic GroundUnitAtb), so via the Entity Assembler the claws win by undodgeable VOLUME of Attack, not by cracking ceramite/flat-armour. This is the single biggest gap and it is the defining trait — flagged, not sold. Missing wire: put Penetration/PerShotEnergy on GroundWeaponAtb and carry them through GroundUnitAssembly.ToGroundUnitDesign.
- PENDING — the SYNAPSE / broodmind link: no unit-combat morale/fury field and no Aura door exist (06 ledger), so a synapse creature buffing nearby Genestealers (and the brood faltering when it dies) cannot be expressed. Formations give cohesion, not the buff.
- PENDING — INFILTRATION / ambush first-strike: no ground stealth or ambush-deploy wire; the initial-engagement spread always opens a gap at the holder's range, so the Genestealer cannot start the fight already in melee.
- PENDING — the GENESTEALER'S KISS / cult subversion: no engine wire converts a population or spawns hybrids from an enemy colony — a strategic-layer mechanic that does not exist (no reader at all).

**Sources:** wh40k.lexicanum.com/wiki/Genestealer (lore, physical description, shock-trooper/vanguard role, two arm-pairs) · wh40k.lexicanum.com/wiki/Rending_Claws (diamond-hard chitin claws; rip ceramite/thick armour) · warhammer40k.fandom.com/wiki/Genestealer (bipedal crouch, human-like + claw arms, rip apart a Space Marine in Power Armour) · tvtropes.org/pmwiki/pmwiki.php/Characters/Warhammer40000GenestealerCults (Purestrain traits: high speed/threat range, multiple rending attacks, infiltration) · General Warhammer 40,000 datasheet knowledge (Purestrain Genestealer tabletop: Move ~8in, Toughness ~4, ~5+ save, multiple attacks, Infiltrators, Rending/high-AP claws) — statline varies by edition, cited as approximate not hard-canon

---

## Tyranid Carnifex

*Warhammer 40,000 · ground host.* The Carnifex is the Tyranids' living battering ram — an organic main battle tank the Hive Mind grows to crush armour and smash fortifications, wrapped in metres of chitin and hard to put down. Built as a single ground monster, its role stack is ① soak heavy fire (thick carapace) ② devastate in melee (scything talons/crushing claws) ③ lob a bio-cannon, and it deliberately sacrifices speed and any dodge — it is an armour-and-HP tank, not an evader. The engine expresses the tank faithfully; its two soul traits — synapse/Hive-Mind control and self-regeneration — land PENDING, because no engine wire reads them.

**Canon —** **Physical:** Stands 'as tall as the largest recorded African elephant' (~3.5-4 m at the carapace; INVENTED precise figure — canon gives no metres). Heavy, monster/vehicle scale — mass ESTIMATED at ~3-5 tonnes (no published tonnage). Tabletop stat-line (10th ed): Toughness 9, 2+ armour save, 8 Wounds, monster keyword — used here only as a relative hardness anchor, not literal HP. **Defense:** Very thick chitinous carapace giving excellent protection from physical damage (2+ save / T9). No energy shield in canon — survivability is armour + wounds + optional REGENERATION (living tissue knits shut, 'extremely difficult to kill'). It does NOT dodge — it is slow and relies on plate + bulk. **Mobility:** Slow-ish for a Tyranid ('their brute force more than makes up for it'). Multi-legged organic gait; crushes through fortified walls and obstacles rather than going around. No flight, no FTL — a surface assault beast delivered by the hive fleet. **Crew/control:** Uncrewed — a bred bio-construct. 'Manned' by the HIVE MIND via synapse: within ~24 in of a synapse creature it acts with the Hive Mind's iron will; OUTSIDE synapse range instinctive behaviour takes over and it degrades (−1 to hit non-nearest, −2 to charge). Synapse IS its command-and-control.

**★ Signature that must be expressed:** THREE signatures that MUST be expressed: (1) LIVING BATTERING RAM — a charge that smashes armour/walls and deals bonus mortal-wound damage on impact; (2) SYNAPSE / instinctive-behaviour — Hive-Mind control that degrades the beast when out of synapse range; (3) REGENERATION — it heals battle damage and is 'extremely difficult to kill'.

**Armament (canon):**
- Carnifex scything talons — signature melee, re-roll-to-hit heavy cutting claws (CANON name)
- Carnifex extra scything talons / crushing claws — the second melee pair the Living Battering Ram swings (CANON options)
- Heavy venom cannon — ranged poison/toxin bio-cannon (CANON)
- Stranglethorn / barbed strangler cannon — ranged barbed-seed blast (CANON)
- Devourer / deathspitter — ranged devouring-worm / acid bio-guns (CANON)
- Bio-plasma — a short-ranged plasma-analogue spat from the maw (CANON upgrade)

**Chassis / host:** GROUND host, Walker-locomotion vehicle-frame (GroundChassisAtb, CarryClass=Vehicle → DeriveType returns Armor = 'organic main battle tank'). A single GroundUnit, UnitsPerBuild=1 (a Carnifex is one monster, not a squad). Dialed budget: BaseStrength ~800 (carry budget for heavy chitin + talons), BaseHP ~800. Carry budget after the musculature augment ≈ 1,300 carry-units; total build mass ≈ 3.4 t of parts — monster/vehicle scale, inside the vehicle frame. NOTE: the frame is skinned 'organic' but the engine has NO substrate axis (docs/assembler/01-IO-chassis.md §D — substrate PENDING), so mechanically it is a Walker vehicle-frame; biomass-not-reactor upkeep is not modelled.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Chassis / frame | Carnifex Bio-Frame (GroundChassisAtb: Walker, CarryClass=Vehicle, BaseStrength~800, BaseHP~800) | 1 | Chassis |
| Soak heavy fire | Chitin Carapace plating (GroundArmorAtb: HP +700, Defense +45 each, VsKinetic 1.4 / VsEnergy 0.8 / VsExplosive 1.1 / VsExotic 1.0, Mass ~300) | 2 | Defense |
| Melee devastation | Scything Talons / Crushing Claws (GroundWeaponAtb: Mode=Melee, Attack ~180, Range 0 hex / Range_m 0, Mass ~120) | 2 | Weapons |
| Ranged bio-weapon | Heavy Venom / Strangler Bio-Cannon (GroundWeaponAtb: Mode=Ballistic, Attack ~120, Range 3 hex / Range_m ~4000, Mass ~80) | 1 | Weapons |
| Bulk + carry + toughness | Monstrous Musculature (GroundAugmentAtb: StrengthBonus +500, ToughnessBonus +0.3, EvasionBonus 0, Shield 0, Mass ~150) | 1 | Enhancers |
| All-terrain advance | Bio-Locomotion / multi-legged gait (GroundLocomotionAtb: SpeedFactor 0.8 slow, RoughHandling 0.7 all-terrain, Mass ~120) | 1 | Propulsion |
| Fight in any environment | Chitin Bio-Seal (GroundSealAtb: Sealing 1.0, Mass ~30) | 1 | Defense |
| SIGNATURE regeneration | Regenerating Node (self-repair) — NOT BUILDABLE | 1 (aspirational) | Enhancers |
| SIGNATURE synapse control | Synaptic Link (Hive-Mind C2) — NOT BUILDABLE | 1 (aspirational) | Command / Aura |

**Gates closed:**
- **mass/carry:** CLOSED. Capacity = BaseStrength 800 + augment StrengthBonus 500 = 1,300; MaxItemWeight = 0.5×1,300 = 650. Used ≈ 2 carapace(300)=600 + 2 talons(max(120,180×0.1)=120)=240 + cannon 80 + augment 150 + locomotion 120 + seal 30 ≈ 1,220 ≤ 1,300. Every single item (max 300) ≤ 650. GroundUnitAssembly.cs:255 passes.
- **power supply≥draw:** CLOSED trivially. All weapons are native GroundWeaponAtb, so WeaponSupply.PowerDraw_W = 0; reactorSupply 0. 0 ≤ 0 → no under-powered problem (GroundUnitAssembly.cs:258). Organic — no reactor needed, which is correct in spirit.
- **ammo feed:** CLOSED trivially. Native ground weapons → WeaponSupply.DrawsAmmo = false → anyAmmoWeapon false → no magazine required (cs:261). Bio-weapons are self-generating; no magazine part needed.
- **crew/berths:** N/A on the surface. Ground frames author CrewReq 0; the beast is 'manned' by the Hive Mind, which is the SYNAPSE trait — and that is PENDING (no synapse/command reader). No berth on a ground unit.
- **seal:** CLOSED. GroundSealAtb Sealing 1.0 folds into EnvironmentalResistance {Vacuum, ToxicAtmosphere} (GroundUnitAssembly.cs:311-315) → the E4 attrition step negates airless/toxic bleed. Fights anywhere.
- **detection≥weapon:** N/A. Ground has no fire-control-range gate the way ships do; a weapon fires if its own Range_m reaches the target (WeaponReaches). The Carnifex's longest reach is the bio-cannon ~4 km — no separate sensor is required to shoot it.
- **mobility:** CLOSED. GroundLocomotionAtb SpeedFactor 0.8 → GroundMobility.Speed_kmh → march timing (slow); RoughHandling 0.7 → terrain edge. It can move and it can fight while repositioning.

**Emergent (what the assembler computes):** Attack ≈ 480 (2×180 talons + 120 cannon). HitPoints ≈ (BaseHP 800 + 2×700 carapace = 2,200) × 1.3 toughness ≈ 2,860. Flat Defense ≈ 90 soaked off EACH incoming source (2×45), kinetic-tuned ×1.4. Evasion 0, Shield 0. Damage flavour = Melee (undodgeable). Range_m ≈ 4,000 (the cannon; talons 0). Vs the base-mod tank (garrison Armor 140/15/700): ~3.4× the HP, ~3.4× the Attack, ~6× the flat soak — a genuine super-heavy monster that a swarm's chip-fire bounces off while one alpha strike can still crack it. Slow and un-dodgy on purpose. All magnitudes are DESIGNED/FLAGGED anchors relative to the tank, not canon lifts.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Firepower / Attack (talons + cannon) | Σ→Firepower ShipCombatValueDB.cs:524; ground kernel CombatKernel.cs:228 via GroundForcesProcessor; 06-OUTPUTS §Weapons DamagePerSecond LIVE(ship+ground) |
| ✅ `LIVE` | Melee undodgeable crush | GroundCombatant.NatureDeliveryFor → ToWeaponProfile → CombatKernel.HitFraction (melee/artillery undodgeable); GroundForcesProcessor.cs:340 |
| ✅ `LIVE` | HP pool (frame + carapace, × toughness) | GroundChassisAtb.BaseHP:50 → GroundUnit.MaxHealth (06 §Chassis 'LIVE ground-only'); armour HP add GroundUnitAssembly.cs:177; toughness × at cs:229 |
| ✅ `LIVE` | Flat per-source Defense soak | GroundArmorAtb.Defense → ArmourSoak GroundForcesProcessor.cs:437 → CombatKernel.cs:294; 06 §Defense 'Armour Defense LIVE' |
| ✅ `LIVE` | Armour NATURE tuning (chitin vs kinetic/energy) | ArmourResistFor(nature) → natureFactor CombatKernel.cs:302; 06 §Defense 'Four nature resists LIVE' |
| ✅ `LIVE` | Carry budget from musculature (StrengthBonus) | GroundUnitAssembly.cs:105-107 (capacity +=); 06 §Enhancers 'StrengthBonus (carry) LIVE' |
| ✅ `LIVE` | Toughness multiplier (musculature) | GroundUnitAssembly.cs:191 accumulate → cs:229 HitPoints *= 1+toughness |
| 🟪 `EMERGENT` | Evasion (=0, deliberate) | GroundUnitAssembly.cs:189 → design.Evasion; 06 §Defense 'Evasion READ (Chassis+Propulsion)'. Set to 0 — an armour tank, not a dodger. |
| ✅ `LIVE` | Locomotion speed (slow) + rough-terrain edge | GroundMobility.cs:54 SpeedFactor → Speed_kmh → march; RoughHandling → GroundForcesProcessor.cs:489,522; 06 §Propulsion LIVE |
| ✅ `LIVE` | Environmental seal (vacuum/toxic) | GroundUnitAssembly.cs:311-315 → EnvironmentalResistance → E4 GroundForcesProcessor.IsDamageEffect |
| 🟦 `LIVE-gated` | Real-distance range gate (bio-cannon reaches on approach) | WeaponReaches → CombatKernel.WithinReach; behind EnableMiniHexCombat/EnableInitialEngagementSpread — default off in CI, flipped ON for menu games (NewGameMenu) |
| ✅ `LIVE` | Standing upkeep + build cost (cradle-to-grave) | UpkeepCredits = Mass×0.1 GroundUnitAssembly.cs:299 → GroundUpkeep.BillIfDue; ResourceCosts summed cs:317-323 |
| ✅ `LIVE` | Whole-or-dead loss (grave rung) | GroundUnit removed at 0 health in GroundForcesProcessor casualty step — the loss is real, but see regeneration below (no damaged/heal state) |
| 🟠 `PENDING` | SIGNATURE Living-Battering-Ram CHARGE bonus | No momentum/charge-impact term in the ground resolver — the melee Attack lands but the canon 'after-charge bonus damage + to-hit' has no reader. Missing: a closing-impact/charge term in ResolveRegionCombat. |
| 🟠 `PENDING` | SIGNATURE Synapse / Hive-Mind control (instinctive-behaviour degrade) | 06 §Enhancers 'Morale/fury → PENDING (grep zero)'; 06 §Aura 'zero engine matches'. Missing: a unit-combat morale/instinctive field + an aura-pass to project the synapse radius. |
| 🟠 `PENDING` | SIGNATURE Regeneration (heal, extremely hard to kill) | 06 §Enhancers 'Self-repair rate (fieldrep) → PENDING (whole-or-dead, no damaged state)'. Missing: a per-tick heal reader + a sub-max damaged unit state. |
| 🟠 `PENDING` | Organic SUBSTRATE (biomass upkeep, no reactor/crew as an axis) | docs/assembler/01-IO-chassis.md §D — no substrate field on any chassis atb; frame is mechanically a Walker vehicle-frame. The 0-power/0-ammo behaviour mimics 'organic', but biomass-not-fuel is not modelled. |

**Authenticity notes:**
- FAITHFUL — the armour tank: the double chitin carapace gives a huge HP pool AND flat per-source Defense soak, and flat-per-source is the whole point — a swarm's many small volleys bounce, one big alpha punches through. That is exactly the canon 'soaks up heavy firepower, hard to kill by small-arms' feel, and it is genuinely LIVE (CombatKernel.cs:294).
- FAITHFUL — armour NATURE: chitin tuned VsKinetic 1.4 / VsEnergy 0.8 reproduces 'excellent vs physical, cut open by melta/plasma'. LIVE via ArmourResistFor → CombatKernel natureFactor (CombatKernel.cs:302).
- FAITHFUL — the living battering ram's CRUSH lands: melee talons map to an undodgeable profile, so the hit connects regardless of the target's evasion. LIVE. (The after-charge BONUS is the part that's PENDING.)
- FAITHFUL — slow but relentless: SpeedFactor 0.8 makes it slow, RoughHandling 0.7 lets it crush through rough terrain where a wheeled unit bogs down. Both LIVE (GroundMobility.cs:54, GroundForcesProcessor.cs:489).
- FAITHFUL — no dodge, no shield: EvasionBonus 0 and Shield 0 are deliberate. A Carnifex survives by plate and wounds, not by evading — the build refuses the dodge/shield path on purpose.
- FAITHFUL — fights anywhere: the bio-seal negates vacuum/toxic attrition (E4), matching Tyranids boarding ships and fighting hostile worlds. LIVE.
- FAITHFUL (mechanically) — organic 'no crew, no reactor, no ammo': the power gate passes at 0 draw and no magazine is required, so the bio-weapons need no supply train — the right SHAPE for an organism, even though the engine reaches it by the frame drawing nothing rather than by a real organic substrate.
- PENDING — SYNAPSE / Hive-Mind control (the #1 soul trait): no unit-combat morale/fury field and no aura pass, so 'degrades to instinctive behaviour outside synapse range' cannot be expressed. Missing wire: a morale/instinctive modulator + a neighbour-sweep aura (06 §Enhancers, §Aura).
- PENDING — REGENERATION (the #2 soul trait): units are whole-or-dead with no damaged state and no heal reader, so 'living tissue knits shut, extremely hard to kill' has nothing to write to. Missing wire: a per-tick self-repair reader + a sub-max damaged state (06 §Enhancers).
- PENDING — the LIVING-BATTERING-RAM charge BONUS: the melee crush lands, but the canon 'after a charge, extra impact/mortal-wound damage + to-hit' has no momentum/charge reader in the ground resolver.
- ESTIMATED numbers flagged: no canon gives the Carnifex a mass in tonnes or a height in metres, and the engine magnitudes (BaseHP ~800, Attack ~180, Defense ~45) are DESIGNED to sit ~3-4× a base-mod tank (garrison Armor 140/15/700) — a relative anchor, tunable, not a canon lift.

**Sources:** wh40k.lexicanum.com/wiki/Carnifex (Lexicanum — armour/carapace, weapon options, living-battering-ram role; page body via search, direct fetch 403) · warhammer40k.fandom.com/wiki/Carnifex (Fandom — scything talons/crushing claws/bio-cannon loadout, size 'larger than a man') · warhammer-community.com — Tyranid monsters (10th-ed T9/2+/8W hardness anchor) · wahapedia.ru/wh40k10ed/factions/tyranids/Carnifexes (rules: Living Battering Ram, synapse −1 to hit / −2 charge instinctive behaviour) · 1d6chan.miraheze.org/wiki/Carnifex (elephant-height size, regeneration upgrade, mindless-without-synapse lore) · warhammerguild.com Carnifex guide (loadouts: heavy venom cannon, stranglethorn, devourer, deathspitter, bio-plasma)

---

## Republic Clone Trooper (Phase II) — Grand Army of the Republic line infantry

*Star Wars (Clone Wars / Legends + canon) · ground host.* A single flash-trained GAR infantryman built on the human ground frame: a DC-15A energy blaster and a thermal detonator for punch, plastoid ablative plate tuned against blaster bolts, a fully-sealed all-environment life-support envelope, and a training cadre that bakes elite discipline into attack and toughness. Role stack: ① reliable ranged energy fire → ② discipline/veterancy that beats a levy on the same body → ③ survive contact (modest plate, no shields) → ④ fight anywhere (vacuum/toxic sealed). It sacrifices raw toughness and heavy weapons — a human frame out-disciplines and out-seals a conscript but cannot out-muscle power armour or a tank, and every kilo of seal + training eats the carry budget.

**Canon —** **Physical:** Height 1.83 m, unarmoured mass ~79–80 kg (canon). Loaded with Phase II plastoid armour, DC-15A (4.15 kg, canon) and kit, combat mass ~100 kg (estimated). Modeled on the engine's Human Frame (the smallest ground chassis). **Defense:** Phase II plastoid-composite plate over a black body glove — modest kinetic protection, better against glancing blaster fire than a direct hit (canon: troopers die to single bolts). No deflector shield, no evasion focus (clones are disciplined, not acrobats). Survivability comes from armour + training + numbers, not shields. **Mobility:** On foot (canon infantry). No organic vehicle mobility; deployed by LAAT/i gunship and transport. Modeled as Foot locomotion. **Crew/control:** A single trooper is the unit. Flash-cloned on Kamino, accelerated growth, flash-trained from birth to unwavering discipline — the 'manning' is the training itself, modeled as a Training Cadre veterancy stamp.

**★ Signature that must be expressed:** The SEALED, ALL-ENVIRONMENT trooper who is also ELITE-TRAINED — a Phase II clone can fight in vacuum or a poison sky where a militia levy suffocates, and hits/soaks harder than an identical untrained body. Those two traits (sealed life support + baked-in discipline) are the signature that MUST be expressed, and both have live engine wires.

**Armament (canon):**
- DC-15A blaster rifle (canon): BlasTech heavy blaster rifle, 4.15 kg, fires blue energy/plasma bolts; can punch through laminate armour at 1,000 m, effective ~700 m standard / 10 km tripod-mounted. Energy-nature ranged weapon.
- DC-15S blaster carbine (canon): shorter close-quarters variant — same energy flavour, less reach (represented as a loadout swap, not a second mount).
- Thermal detonator (canon): baradium-core grenade, blast radius ~5–20 m depending on model — a short-range explosive/area munition.

**Chassis / host:** Human Frame (GroundChassisAtb) — the smallest ground frame, Locomotion=Foot, CarryClass=Personnel. BaseHP 200 (template default), BaseStrength dialed UP from the default 100 to ~260 carry units. WHY the dial-up (flagged): the two signature clone capabilities — a full sealed life-support envelope and an elite training cadre — each carry a large mass, and the per-item carry cap (0.5 × capacity, GroundUnitAssembly.MaxItemFraction) plus the total-carry gate BLOCK them on a default-100 militia frame. Raising BaseStrength to 260 (still a 'human frame', within the 1–1000 template range) represents a fully-equipped GAR trooper's load-out vs a bare conscript, and lets the full kit legally close. Carry budget 260; per-item cap 130; total kit ≈ 246.5 used.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Carry & structural budget | Human Frame (GroundChassisAtb) — BaseStrength 260 (flagged up from 100), BaseHP 200, Foot, Personnel | 1 | Chassis |
| Primary ranged fire (DC-15A) | Energy Weapon 'Plasma Projector' template, dialed to a blaster rifle: CarryMass 12, Attack 55, Mode=Energy, Range_m 1000 m | 1 | Weapons |
| Explosive/area option (thermal detonator) | Ground Weapon template, Mode=Artillery: CarryMass 4, Attack 45, Range_m ~25 m | 1 | Weapons |
| Body armour (plastoid plate) | Ablative Laminate (GroundArmorAtb): CarryMass 20, HP 120, Defense 4, VsEnergy 1.3, VsKinetic 0.8, VsExplosive 1.0, VsExotic 1.0 | 1 | Defense |
| Sealed all-environment life support (SIGNATURE) | Sealed Systems (GroundSealAtb): Sealing 1.0 → MassPerUnit 100 | 1 | Defense |
| Elite discipline / veterancy (SIGNATURE) | Training Cadre (GroundTrainingAtb): TrainingMultiplier 1.3 → MassPerUnit 110 | 1 | Enhancers |
| Squad size + standing cost | UnitsPerBuild dial (assembler) + GroundUpkeep | UnitsPerBuild ~9 per build (a clone squad) | Logistical |

**Gates closed:**
- **mass/carry (Σ part carry ≤ frame budget):** Rifle 12 + detonator 4.5 + plate 20 + seal 100 + cadre 110 = 246.5 ≤ 260 budget. Closed ONLY by dialing BaseStrength to 260 (flagged); a default-100 frame fails.
- **per-item cap (each item ≤ 0.5×capacity):** Cap = 130. Heaviest item is the training cadre at 110 ≤ 130. On a default-100 frame the cap would be 50 and BOTH the seal (100) and cadre (110) would be rejected — this is the binding finding.
- **power supply ≥ draw:** Trivially — native GroundWeaponAtb weapons (energy blaster, detonator) draw 0 W in the current supply gate (only unified SPACE weapons are power-gated). EnergyDemand 0 ≤ ReactorSupply 0. No reactor needed.
- **ammo feed:** No native ground weapon triggers the ammo gate (WeaponSupply.DrawsAmmo false for GroundWeaponAtb), so no magazine required. NOTE: a thermal detonator SHOULD be finite — see honesty ledger (PENDING).
- **seal / environment:** Sealing 1.0 → EnvironmentalResistance{Vacuum:1.0, ToxicAtmosphere:1.0}; the E4 attrition step negates 100% of vacuum/toxic bleed. This is the signature capability's live gate.
- **detection ≥ weapon:** Not required for this build — the trooper is a shooter, not a scout; no GroundSensorAtb mounted. Squad/army sensing is provided by attached recon units, not the individual.
- **mobility:** Foot locomotion; march speed falls out of GroundMobility. No terrain-drive edge (a foot soldier), which is correct for infantry.

**Emergent (what the assembler computes):** Attack: Σ weapons 55+45 = 100, then ×TrainingMultiplier 1.3 baked at raise → ~130. HitPoints: frame 200 + plate 120 = 320, ×1.3 training → ~416. Defense (flat soak) 4; ArmourVsEnergy 1.3 / VsKinetic 0.8. Evasion 0, Shield 0 (no augment). Reach: Range_m 1000 (energy), hex Range ~2; DamageType Energy (heaviest weapon). Build mass ~266 → upkeep ~27 cr/mo. VS the base-mod generic Infantry (Attack 100 / Defense 10 / HP 500): the clone hits HARDER (130 vs 100) and reaches FURTHER (1000 m energy vs a 500 m ballistic rifle), with energy-biased armour and full environmental sealing + veterancy — but has LESS flat Defense (4 vs 10) and LESS raw HP (416 vs 500). It is a specialist line trooper, not a bullet-sponge: it wins the ranged/all-terrain exchange and the discipline edge, and loses a straight slugging match to a heavier-armoured unit.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | DC-15A ranged energy fire (attack + reach + energy nature) | 06-OUTPUTS-BY-DOOR.md Weapons row: DamagePerSecond→Firepower ShipCombatValueDB.cs:524 & CombatKernel.cs:228 (ship+ground); Nature/shield-soak CombatKernel.cs:182; ground path builds WeaponProfile in GroundForcesProcessor (ToWeaponProfile). |
| 🟦 `LIVE-gated` | Range_m gate (real-distance reach on the mini-hex field) | WeaponReaches→GroundMiniHex.RealGapMetres ≤ range_m behind EnableMiniHexCombat (default OFF in CI, flipped ON for menu games in NewGameMenu). K3, GroundCombat/CLAUDE.md. |
| 🟪 `EMERGENT` | Elite training / discipline (×1.3 attack + toughness) — SIGNATURE | Baked at raise into Attack+MaxHealth: 06 Enhancers row TrainingMultiplier → GroundForcesDB.cs:607-611; assembler reads best cadre GroundUnitAssembly.cs:203,249. Applied once at raise, then a readout — never re-read by the resolver (by design). |
| ✅ `LIVE` | Sealed all-environment survival (vacuum + toxic) — SIGNATURE | Seal folded to EnvironmentalResistance at GroundUnitAssembly.cs:311-315 → GroundUnit.EnvResistance → E4 attrition GroundForcesProcessor.IsDamageEffect (reads unit.ResistanceTo). Ground-only capability; the live surface-support hazards are generated by PlanetEnvironmentFactory. |
| 🟦 `host-split` | Plastoid plate — flat armour soak + energy-biased nature | LIVE on a GROUND unit: ArmourSoak GroundForcesProcessor.cs:437→CombatKernel.cs:294; nature ArmourResistFor:436→natureFactor:302 (06 Defense row). The SAME armour is inert on a ship (ship folds armour into one Toughness pool) — correct here since the host is ground. |
| ✅ `LIVE` | Thermal detonator — explosive, undodgeable area | Artillery mode → Explosive nature + Blast delivery (undodgeable, partly bypasses shields): GroundCombatant.NatureDeliveryFor GroundCombatant.cs:59; hits target VsExplosive. Fires in its own short range band via W2 per-weapon banding. |
| 🟠 `PENDING` | Thermal detonator as a FINITE munition (throw count / exhaustion) | No ammo gate for native GroundWeaponAtb — WeaponSupply.DrawsAmmo returns false for a native ground weapon (only unified SPACE weapons feed the magazine gate). A grenade never runs out; there is no per-mount munition counter. Missing wire: an ammo/charge count on GroundWeaponAtb + a per-weapon depletion read in ResolveRegionCombat (ammo is a whole-unit pool today, W2 v1 limit). |
| 🟠 `PENDING` | DC-15A 'punches laminate armour' (weapon armour-penetration) | An ASSEMBLED unit's per-weapon Penetration is 0 — the Penetration/PerShotEnergy dials live only on the monolithic base-mod GroundUnitAtb templates (installations.json W1c), not on the GroundWeaponAtb part the assembler mounts. GroundUnit.Penetration defaults 0 → the blaster cannot express its canon armour-crack. Missing wire: a Penetration/PerShotEnergy field on GroundWeaponAtb flowed through GroundUnitAssembly into GroundWeaponMount. |
| ✅ `LIVE` | Squad size + standing upkeep (an army costs money) | UnitsPerBuild scales ResourceCosts/IndustryPointCosts GroundUnitAssembly.cs:327-332; UpkeepCredits = Mass×0.1 GroundUnitAssembly.cs:299 → billed monthly GroundUpkeep.BillIfDue (GroundForcesProcessor step 0b). |
| ✅ `LIVE` | Formation cohesion (clone squad/platoon fights as one) | GroundFormation + GroundForces.CreateFormation/AssignUnit; stance/ROE via GroundFormationDoctrine; auto-form via GroundAssembly.FormUpLoose behind AutoFormUp (menu-on). |
| ✅ `LIVE` | Carry gate closes only on a dialed-up frame (the load-out finding) | The total-carry + per-item gates are real and enforced: GroundUnitAssembly.cs:255 (over-capacity) and :225 (per-item). This is WHY BaseStrength must be raised — the gate correctly refuses a full seal+cadre on a 100-budget militia frame. |

**Authenticity notes:**
- SIGNATURE #1 — Sealed all-environment survival: LIVE. Sealing 1.0 → EnvironmentalResistance{Vacuum, ToxicAtmosphere} → the E4 attrition step actually negates the vacuum/toxic bleed a levy dies to (GroundUnitAssembly.cs:311-315 → GroundForcesProcessor.IsDamageEffect). A Phase II clone genuinely holds an airless/poison world in-sim.
- SIGNATURE #2 — Elite flash-training discipline: LIVE, baked at raise. TrainingMultiplier 1.3 lifts Attack + MaxHealth (GroundForcesDB.cs:607-611); an identical untrained human body fields weaker. Expressed, but note it is a RAISE-TIME bake, not a live resolver read.
- HONEST GAP — DC-15A 'punches laminate at 1000 m': PENDING. Assembled units carry per-weapon Penetration 0 (that dial lives only on the monolithic GroundUnitAtb base-mod path, not on the GroundWeaponAtb the assembler mounts), so the blaster's canon armour-crack cannot be set. Missing wire named: Penetration/PerShotEnergy on GroundWeaponAtb.
- HONEST GAP — Thermal detonator is UNLIMITED: PENDING. Explosive/undodgeable area fire is LIVE (Artillery→Explosive+Blast), but a native ground weapon is never ammo-gated, so a grenade never runs out. Missing wire: a per-weapon munition counter + depletion in ResolveRegionCombat.
- BUILD-TENSION FINDING — the carry gate blocks the signature kit on a stock human frame. Sealed Systems (mass 100) and Training Cadre (mass 110) each exceed the 0.5×100 per-item cap of the default frame; BaseStrength had to be dialed to ~260 (flagged) to legally mount the two traits that MAKE it a clone. The gate is doing its job; the tuned frame is the honest way to close it.
- MODELING NOTE — no personal shield, low flat Defense: deliberate and authentic. Clones survive by armour + discipline + numbers, not deflectors; the build mounts no augment shield/evasion, so it under-tanks the generic base-mod Infantry (Defense 4 vs 10, HP 416 vs 500) while out-shooting and out-ranging it — a specialist line trooper, not a bullet-sponge.
- CANON-vs-ENGINE NOTE — 'Katarn-type' in the brief is a slight conflation: Katarn-class was clone COMMANDO armour; the standard GAR trooper wears Phase II clone trooper armour. Both share the sealed-helmet life support this build expresses. DC-15A modeled as an Energy weapon (blaster bolts are energy/plasma, not slugs) at Range_m 1000 m (canon laminate-punch reach; ~700 m effective).

**Sources:** Wookieepedia — Clone trooper armor (starwars.fandom.com/wiki/Clone_trooper_armor) · Wookieepedia — Phase II clone trooper armor/Legends (starwars.fandom.com/wiki/Phase_II_clone_trooper_armor/Legends) · Wookieepedia — DC-15A blaster rifle (starwars.fandom.com/wiki/DC-15A_blaster_rifle): 4.15 kg, blue energy bolts, punches laminate armour at 1000 m; ~700 m effective / 10 km tripod-mounted · Wookieepedia — DC-15S carbine, DC-15A blaster carbine (close-quarters variants) · Wookieepedia — Thermal detonator / Class-A thermal detonator / Baradium (blast radius ~5–20 m depending on model) · Clone trooper physical canon — height 1.83 m, mass ~79–80 kg (Wookieepedia / clonetrooper.fandom talk pages) · Pulsar4X engine source: GroundUnitAssembly.cs, GroundChassisAtb.cs, GroundWeaponAtb.cs, GroundArmorAtb.cs, GroundSealAtb.cs, GroundTrainingAtb.cs, GroundAugmentAtb.cs, GroundCombatant.cs; GameData/basemod/TemplateFiles/installations.json (human-frame, energy-weapon, ground-plating, ablative-plating, sealed-systems, ground-training-cadre) · docs/assembler/06-OUTPUTS-BY-DOOR.md (verified reader/PENDING ledger), 01-IO-chassis.md, DESIGNER-DRIVER-PLAYBOOK.md; GroundCombat/CLAUDE.md (E4 seal, W1c penetration, K1/K3 real-distance, carry gates)

---

## AT-TE (All Terrain Tactical Enforcer)

*Star Wars (Clone Wars) · ground host.* The AT-TE is a six-legged Republic assault walker that is three things at once: a heavy armour-cracking gun platform (one top-mounted mass-driver cannon), an anti-personnel screen (six laser cannons), and a battlefield troop transport (~20 clone troopers) — all on a heavily-armoured, sheer-cliff-climbing all-terrain frame. Built on the engine as a Walker-class GroundChassis carrying a railgun (the mass-driver), six energy laser cannons, a reactor + magazine to feed them, heavy plating, and an all-terrain drive: its gun/armour/mobility land LIVE, but its two defining traits — carrying troops and the mass-driver's armour-penetrating alpha — have no engine wire and land PENDING. The gun and the legs are real; the "assault transport" half is honest amber.

**Canon —** **Physical:** Length 12.4 m (canon; Legends variants cite ~13.2 m — the 22.02 m figure is a known data error). Height ~5.02 m, width ~5.32 m (Legends, estimated). Mass: NOT reliably canonized — estimated multiple tens of tonnes for a 12 m armoured six-leg walker (INVENTED/estimated). Nicknamed 'six legs' by the Separatist droid army. Body is two armoured halves joined by a flexible concertina section. **Defense:** Heavily armoured — thick plating over a two-part hull; the AT-TE relied on ARMOUR, not deflector shields (canon: no shield generator). Not evasive — a slow walking fortress that soaks fire rather than dodging it. **Mobility:** Six articulated legs (all-terrain locomotion); can traverse rough ground and CLIMB SHEER/VERTICAL SURFACES — the signature trait. Top speed ~60 km/h (Legends, estimated). The flexible concertina mid-section increases field mobility. **Crew/control:** ~7 crew (canon): 1 pilot, 1 co-pilot/spotter, 1 vehicle commander, up to 5 gunners. PLUS a troop complement of up to ~20 clone troopers with full combat gear (Wookieepedia primary; some sources cite up to 38). The troopers are carried/deployed, not crew.

**★ Signature that must be expressed:** THE dual identity: it is simultaneously a heavy gun-walker AND a troop transport — an 'assault transport,' not just a tank. That troop-carry, plus the mass-driver's armour-piercing solid-shot and the legs' ability to climb sheer cliffs, are the three traits that MUST be expressed for it to read as an AT-TE.

**Armament (canon):**
- Firefont/Tup-2b heavy mass-driver cannon ×1 — top-mounted turret; an electromagnetic projectile gun that fires solid armour-piercing slugs (also sonic charges or heat-seeking missiles per mission). Slow rate of fire, poor accuracy, but a fearsome armour-cracker (canon)
- Anti-personnel laser cannon ×6 — 4 forward, 2 covering the rear; used against infantry and light vehicles/speeders (canon)

**Chassis / host:** walker-frame (GroundChassisAtb) — Locomotion = Walker, CarryClass = Vehicle → the assembler derives GroundUnitType.Armor (GroundUnitAssembly.DeriveType, cs:365-369). Base template is BaseStrength 400 / BaseHP 1000 / Size 4 (installations.json walker-frame :2752-2775), but all three are GuiSelectionMaxMin dials — for a 12 m AT-TE dial BaseStrength up toward ~3000 (max 4000) and BaseHP up toward ~2500 to carry and armour a heavy gun-walker. Carry/mass budget: BaseStrength IS the carry-capacity currency (cs:99); the per-item cap (0.5×capacity, cs:109) forces BaseStrength ≥ 2× the mass-driver's carry-mass. The frame is the right host: a striding multi-leg war-machine, hauled by a Vehicle bay (matching CarryClass, so an AT-TE is itself transportable by a dropship).

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Heavy fire support — the mass-driver cannon | Space Railgun weapon (RailgunWeaponAtb) mounted on the ground chassis (W1b) | 1 | Weapons |
| Anti-personnel / anti-light screen | Space Beam laser (GenericBeamWeaponAtb) mounted on the ground chassis (W1b) | 6 | Weapons |
| Power self-supply (feed the energy weapons) | Reactor (EnergyGenerationAtb) | 1 (2 if one reactor's PowerOutputMax < Σ draw) | Power |
| Ammo self-supply (feed the mass-driver) | Ammo Magazine (GroundMagazineAtb) | 1 | Logistical |
| Heavy armour survivability | Heavy plating (GroundArmorAtb, nature-tuned 7-arg) | 4 | Defense |
| All-terrain / sheer-surface mobility | Locomotion drive (GroundLocomotionAtb) | 1 | Propulsion |
| Carry & deploy ~20 troopers (SIGNATURE) | — none exists — a surface troop-carry bay for a ground unit | 0 (PENDING) | Logistical |
| Crew of ~7 | — not modeled on ground units — | n/a | Civic |

**Gates closed:**
- **mass/carry (Σ part carry-mass ≤ BaseStrength + Σ augment strength):** Dial walker-frame BaseStrength up (~3000, max 4000) so railgun + 6 lasers + reactor + magazine + 4 plates + drive all fit. Gate: GroundUnitAssembly.cs:255 ('over carry capacity'). If short, add a strength augment (GroundAugmentAtb.StrengthBonus, cs:107) or step to vehicle-frame (BaseStrength max 8000).
- **per-item cap (heaviest part ≤ 0.5 × capacity):** The mass-driver railgun is heaviest → BaseStrength ≥ 2× its carry-mass. Gate: GroundUnitAssembly.cs:109 (MaxItemWeight) + cs:225 ('too heavy for this frame'). BaseStrength ~3000 gives a 1500 ceiling, above a heavy railgun's mass.
- **power supply ≥ draw (hard):** Size the reactor so ReactorOutput_W ≥ Σ energy draw of the railgun + 6 lasers. Gate: GroundUnitAssembly.cs:218-219 (accumulate) + cs:258 ('under-powered' → Invalid); WeaponSupply.cs:73,101. Add a 2nd reactor if one is short.
- **ammo feed (magazine for the ammo weapon):** The railgun mass-driver is ammo-fed → mount ≥1 GroundMagazineAtb so AmmoCapacity_kg > 0. Gate: GroundUnitAssembly.cs:221 (accumulate) + cs:261 ('no magazine' → Invalid).
- **crew / berths:** NOT ENFORCED for ground units — walker-frame CrewReq 0, no crew gate exists on the surface cell (01-IO-chassis §D). The 7-crew complement is unmodeled; the gate is vacuously closed.
- **seal (environmental):** Not needed for the AT-TE's canonical worlds. If sent to a vacuum/toxic world, add a GroundSealAtb (sealed-systems) → folds into EnvironmentalResistance {Vacuum,ToxicAtmosphere} at GroundUnitAssembly.cs:311-315 (LIVE E4 attrition). Omitted — an open design, byte-identical.
- **detection ≥ weapon:** Not gated on ground units — a unit fires within its own reach with no separate detection requirement (the spotter is flavour). An optional GroundSensorAtb radar reveals the map but is not a firing prerequisite. Left off — the AT-TE is a gun platform, not a scout.
- **mobility (accel / terrain match):** GroundLocomotionAtb SpeedFactor → GroundUnit.Speed_kmh (GroundMobility.cs:54 → GroundForcesDB.cs:656); RoughHandling → terrain combat mult + eased march (GroundForcesProcessor.cs:489,522). Walker locomotion on the frame is consistent with the drive. Closed.

**Emergent (what the assembler computes):** Attack ≈ 634 (railgun ~400 + 6 lasers ~234) — ~4× a base-mod Armor unit's 140, correct for a 12 m assault walker. HitPoints ≈ 4,500-5,500 (walker BaseHP ~2500 + ~4 plates × ~500 HP, × any toughness augment) — ~6-8× the base Armor unit's 700 MaxHealth: a walking fortress. Defense ≈ 60-80 flat per-source soak (4 plates) → shrugs off swarm/blaster chip-fire (anti-infantry survivability). Evasion ≈ 0 (a walking bunker — soaks, never dodges). ArmourVsKinetic/VsEnergy ≈ 1.1-1.2. Speed_kmh ≈ 250 (engine-abstract: SpeedFactor 0.6 × BaseMarchSpeed 417.6 — the march-timer readout, not canon 60 km/h). Reach: railgun Ballistic ~500 km (RailgunRange_m) / lasers beam hex 4 — but K4 round-down (GroundRangeTools.DescribeReach) floors these to ~0-1 hex on Earth-scale (effectively same-hex/adjacent combat), more on a small moon. Net: slow, near-unmissable, extremely tough heavy hitter — reads correctly as an AT-TE against every base-game unit.

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Mass-driver cannon — firepower (Attack) | SpaceWeaponGround.MountFor → GroundUnitAssembly.cs:166 (r.Attack); resolver reads it — 06-OUTPUTS-BY-DOOR Weapons DamagePerSecond LIVE (ground), CombatKernel.cs:228 |
| 🟠 `PENDING` | Mass-driver — armour-PENETRATION + heavy ALPHA (the 'cracks plate' identity) | SpaceWeaponGround.MountFor sets only Attack/Range/Mode; GroundUnit.Penetration & PerShotEnergy default 0 for an assembled unit — the assembler never wires them from a space weapon. Only the monolithic GroundUnitAtb base-mod path sets them (GroundCombat/CLAUDE.md W1c/W2c). Missing wire: per-mount Penetration/PerShotEnergy through GroundUnitAssembly |
| ✅ `LIVE` | Six anti-personnel laser cannons — firepower | SpaceWeaponGround.MountFor (Energy mode) → GroundUnitAssembly.cs:166; 06-ledger Weapons LIVE (ground), dodge/shield via CombatKernel |
| 🟦 `LIVE-gated` | Power self-supply gate (reactor ≥ weapon draw) | GroundUnitAssembly.cs:258 (hard gate) reading WeaponSupply.PowerDraw_W/ReactorOutput_W (WeaponSupply.cs:73,101); 06-ledger Power PowerOutputMax LIVE (WeaponSupply.cs:104). Genuinely fires because the weapons are space railgun/beam (native ground weapons draw 0) |
| 🟦 `LIVE-gated` | Ammo self-supply gate (magazine feeds the mass-driver) | GroundUnitAssembly.cs:261 (hard gate) reading WeaponSupply.MagazineCapacity_kg (GroundMagazineAtb.Capacity_kg); the railgun's 'Both' mode is what makes DrawsAmmo true |
| ✅ `LIVE` | Heavy armour — HP, flat Defense, nature soak | GroundArmorAtb → GroundForcesProcessor.cs:436-437 → CombatKernel armour soak (06-ledger Defense: Armour LIVE cs:437 → CombatKernel.cs:294; four nature resists LIVE cs:436) |
| 🟦 `host-split` | Hit points / toughness (frame + plating) | GroundChassisAtb.BaseHP → GroundUnit.MaxHealth (06-ledger Chassis Structure(HP) LIVE ground-only, cs:140 — a ship hull has no HP field; live here because the host is ground) |
| ✅ `LIVE` | All-terrain speed | GroundLocomotionAtb.SpeedFactor → GroundMobility.cs:54 → GroundUnit.Speed_kmh (GroundForcesDB.cs:656); 06-ledger Propulsion Ground SpeedFactor LIVE |
| ✅ `LIVE` | Rough-terrain / cliff-climb combat + march edge | GroundLocomotionAtb.RoughHandling → GroundMobility.cs:129 (march) + GroundForcesProcessor.cs:489,522 (combat); 06-ledger Propulsion Ground RoughHandling LIVE. NOTE: literal vertical 'sheer-surface climbing' has no distinct wire — RoughHandling is the honest proxy |
| 🟪 `EMERGENT` | GroundUnitType.Armor role + Evasion ~0 + flat-soak identity | DeriveType GroundUnitAssembly.cs:365 (Vehicle carryclass → Armor); Evasion/HP fall out of the assembled parts via CombatKernel — no bespoke walker class (LOCKED PRINCIPLE) |
| 🟠 `PENDING` | Carry & deploy ~20 troopers (the assault-transport signature) | No ground component carries/deploys other ground units; GroundBayAtb is ship-only (GroundTransport.cs:761 reads it on a ship). Missing wire: a surface troop-carry-bay atb + an embark/deploy-on-surface order for ground units |
| 🟥 `DEAD` | Crew of ~7 (pilot/spotter/commander/gunners) | Ground frames author CrewReq 0 (installations.json walker-frame :2737); no ground crew gate exists (01-IO-chassis §D). The complement is unmodeled flavour |
| ⬜ `READ` | No shields (armour-only defence) | Canon-correct: no GroundAugmentAtb shield mounted → GroundUnit.Shield = 0; the shield-pool reader (GroundForcesProcessor.cs:426-430) is inert by design — a pure-armour tank |

**Authenticity notes:**
- LIVE — the gun-walker core: railgun mass-driver + 6 laser cannons contribute real ground Attack (SpaceWeaponGround.MountFor → GroundUnitAssembly.cs:166; resolver reads it, 06-ledger Weapons DamagePerSecond LIVE ground). Heavy armour HP/Defense/nature LIVE (GroundForcesProcessor.cs:436-437 → CombatKernel). All-terrain mobility LIVE (GroundMobility.cs:54, GroundForcesProcessor.cs:489,522).
- LIVE-gated (two hard gates genuinely closed) — the power gate (reactor ≥ Σ energy-weapon draw, GroundUnitAssembly.cs:258) and the ammo gate (magazine feeds the railgun, cs:261) both really run for THIS build because the weapons are space railgun/beam, not native ground weapons (native GroundWeaponAtb draws 0 power and triggers no ammo gate). Choosing the space weapons is what makes the supply chain real.
- PENDING — the troop-carry signature. The AT-TE carries ~20 troopers; NO ground component lets a ground unit embark/deploy other ground units. GroundBayAtb is ship-only (GroundTransport.cs:761). Missing wire: a surface troop-carry bay atb + an embark/deploy-on-surface order. The 'assault transport' half cannot be expressed as a ground unit today — the single biggest authenticity gap.
- PENDING — the mass-driver's armour-PENETRATION and heavy-ALPHA. 'Solid shot for penetrating armour' = Penetration + PerShotEnergy (the alpha-cracks-plate dials). The assembler does NOT wire these from a mounted SPACE weapon (MountFor sets only Attack/Range/Mode; both default 0 for an assembled unit). So the mass-driver hits HARD (high Attack, kinetic) but does not model armour-piercing alpha — flagged, not faked. Only the monolithic GroundUnitAtb path sets them.
- PENDING/DEAD — the 7-man crew. Ground frames author CrewReq 0 and there is no ground crew gate (01-IO-chassis §D); the pilot/spotter/commander/gunners are flavour with no LIVE wire.
- LIVE-proxy with a PENDING edge — 'climbs sheer surfaces.' Expressed as GroundLocomotionAtb.RoughHandling (LIVE combat + march edge), but literal vertical/surface-climbing is not a distinct engine concept — the closest honest wire is high RoughHandling, so the trait lands as 'fights/marches well on broken ground,' not literal wall-climbing.
- EMERGENT — GroundUnitType.Armor, Evasion ~0, HP pool, and flat-soak identity all fall out of the assembled parts (DeriveType cs:365; CombatKernel), not from a bespoke 'walker' class — exactly the LOCKED PRINCIPLE (a role emerges from components, never a hardcoded unit type).
- READ/flavour — no shields (canon-correct: the AT-TE relied on armour). No GroundAugmentAtb shield mounted, so Shield = 0 — faithfully a pure-armour tank, not a shield-tank.

**Sources:** Wookieepedia — All Terrain Tactical Enforcer (length 12.4 m; crew 1 pilot + 1 co-pilot/spotter + 1 commander + up to 5 gunners; up to 20 troops; 1 top mass-driver cannon + 6 laser cannons; two armoured halves + concertina section; six articulated legs) · Wookieepedia — Mass-driver cannon (Firefont/Tup-2b; fires sonic charges, heat-seeking missiles, or armour-penetrating solid shots; slow rate of fire, poor accuracy) · Clone Wiki / Clone Army Wiki — AT-TE (4 front + 2 rear laser cannons for anti-personnel/anti-light-vehicle; troop complement up to ~38 in some sources) · Star Wars RPG (FFG) Wiki / SWRPGGM — AT-TE walker (all-terrain, sheer-surface climbing; ~60 km/h speed, Legends) · Pulsar4X engine source: GroundUnitAssembly.cs, GroundChassisAtb/GroundArmorAtb/GroundLocomotionAtb/GroundMagazineAtb/GroundSealAtb, SpaceWeaponGround.cs, WeaponSupply.cs, GroundTransport.cs; installations.json (walker-frame); docs/assembler/06-OUTPUTS-BY-DOOR.md + 01-IO-chassis.md (LIVE/PENDING reader ledger)

---

## Prometheus-class Starship (USS Prometheus, NX-74913)

*Star Trek · ship host.* A late-24th-century advanced TACTICAL prototype built to fight outnumbered: small, heavily-automated, and armed far above its size. Role stack — ① out-hit anything its size (firepower density) → ② soak with regenerative shields + ablative armour → ③ split into three to attack in 3D (MVAM) → ④ run on a skeleton crew. It trades a capital's size and crew redundancy for the highest firepower-per-tonne in the set.

**Canon —** **Physical:** Length ~415 m; 15 decks (Memory Beta). Mass is NOT canon — any tonnage here is an assembler-budget abstraction. Source dispute: length ~400–416 m across sources; 415 m best-supported. **Defense:** Regenerative shields (recharge mid-fight) + ABLATIVE hull armour — it tanks disproportionate fire on screen. No reliance on evasion; it out-lasts by shields + plating, not dodging. **Mobility:** Warp 9.9 (very fast). Three warp cores / three impulse sections feed MVAM. Agile for its size but its identity is firepower, not evasion. **Crew/control:** Designed for a MINIMAL, heavily-AUTOMATED crew — on screen the EMH Mark II ran it near-autonomously. Memory Beta lists ~171 as a secondary upper bound; the design intent is a skeleton complement.

**★ Signature that must be expressed:** MULTI-VECTOR ASSAULT MODE (MVAM): the hull separates into THREE independently warp-capable, independently-armed sections (Alpha/Beta/Gamma), attacks in coordinated 3D, then recombines. Three warships in one — the defining capability that MUST be expressed.

**Armament (canon):**
- ~16 Type-XII phaser arrays (Type-XII is a generation above the Galaxy's Type-X) — secondary/technical figure, disputed 12–16, not screen-confirmed
- Photon torpedo launchers (several; exact count not firmly canon)
- Quantum torpedo launchers (advanced warhead)
- Regenerative shields (canon, screen-shown)
- Ablative hull armour (canon, screen-shown)

**Chassis / host:** Stock ~20,000 t / 14,000 m³ ship host. The min/maxed build sits at 69% mass / 43% volume — deliberately sub-capital in absolute size (a 415 m ship), maxed on density.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Type-XII phaser arrays (main battery) | Medium Turbolaser (energy beam) — 'Type-XII phaser array' | 28 | Weapons |
| Secondary phaser strips | Laser Cannon (light energy) | 8 | Weapons |
| Photon + quantum torpedoes | Missile Launcher (guided) | 8 | Weapons |
| Point defense | Point-Defense Battery | 6 | Weapons |
| Regenerative shields (the defense signature) | Shield Generator | 26 | Defense |
| Ablative hull armour | Composite Armour Plating | 14 | Defense |
| Reactor power | Capital Reactor | 1 | Power |
| Heat rejection | Radiator Bank | 12 | Power |
| Warp departure buffer | Capacitor Bank | 2 | Power |
| Warp 9.9 + sublight | Warp Drive + Ion Drive | 1 + 4 | Propulsion |
| Torpedo feed | Magazine | 3 | Logistical |
| Skeleton-crew sustainment | Quarters + Life Support + Medbay + Provisions | 5 + 3 + 1 + 3 | Civic |
| See-first sensors + fire control + ECM | Sensor + Long-Range + Fire-Control + Targeting + ECM + Bridge | 2+2+1+1+1+1 | Sensors/Command |

**Gates closed:**
- **mass ≤ budget:** 13,855 / 20,000 t (69.3%) — binding; room left is host-scale slack, not ship slack
- **volume ≤ cap:** 5,988 / 14,000 m³ (42.8%)
- **power ≥ draw:** draw 133 MW / supply 800 MW — one capital reactor, huge headroom
- **heat ≥ 0:** dissip 480 − gen 328 = +152 MW margin
- **berths ≥ crew:** 750 berths / 617 crew
- **life ≥ crew:** 750 / 617
- **detect ≥ weapon:** 470 km detect / 400 km longest weapon (torpedo)
- **warp battery ≥ jump:** 1.50 MJ stored / 1.25 MJ needed

**Host scale:** Fits the stock ~20,000 t ship host (the Venator/Sovereign tier) easily — at ~415 m it is SMALLER than the 685 m Sovereign, so the canon baseline fills only ~39% of the host. Consequence for the min/max: a compact frigate in a capital-sized host, so the honest fill maxes firepower DENSITY (metric-per-tonne), not absolute size — pushing it to the host cap would misrepresent its canon scale.


**Emergent (what the assembler computes):** Firepower 126.8 MJ/s · Toughness 349 · Shield 130 MJ (regen +2.6 MJ/s) · Evasion 16%. Against the min/maxed Sovereign (~84 MJ/s in a 685 m hull), the Prometheus delivers MORE absolute firepower at ~two-thirds the mass — the highest firepower-per-tonne of the Trek builds, which is exactly the canon 'a small ship that out-fights bigger ones.' Its deep regenerative shield (130 MJ, +2.6 MJ/s) is its survivability, not evasion (16%).


**Min/max — filling the empty space:** binding = *MASS (69.3% used vs 42.8% volume). The residual is host-scale slack — a 415 m frigate in a 20,000 t capital host — so the fill maxes DENSITY, not size.*; metric = Firepower throughput (a glass-cannon tactical striker), with the regenerative shield pool as the secondary sink.. Residual poured in: +16 Type-XII phaser arrays (medturbo 12→28) — in-family density fill, each paying its radiator (heat) + berth (crew); +4 laser strips (4→8); +14 shield generators (12→26) — deepen the regenerative pool (its defense signature); +2 torpedo tubes (6→8) + a magazine; +7 radiators to hold heat margin ≥ 0 (the wall) + berths for the added gun crews. **Result: Firepower 55.7 → 126.8 MJ/s (2.3×) · Shield 60 → 130 MJ · Toughness 201 → 349 — all while staying at 69% mass (deliberately sub-Sovereign size).** (All 8 gates green: reactor 800 MW ≫ draw 133; heat +152 MW margin; berths 750 ≥ crew 617; warp buffer 1.5 ≥ 1.25 MJ. Stopped at the density ceiling, not the host cap — pushing to 20,000 t would make a 415 m ship as massive as a 685 m Sovereign.)

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Type-XII phaser firepower + torpedoes | Σ weapon dps → ShipCombatValueDB.cs:524 → CombatKernel.cs:228 (energy beams + guided). Firepower ×1.4 from targeting(1.25)+fire-control(1.12), both LIVE-gated. |
| ✅ `LIVE` | Regenerative shields (deep pool + regen) | ShieldCapacity_J + regen → CombatKernel.ResolveShield (drain then regen per salvo). The 'regenerative' name is exactly the engine's regen field — a rare on-the-nose match. |
| ✅ `LIVE` | Ablative hull armour | Armour thickness folds into the ship Toughness pool — ShipCombatValueDB.cs:297,519. |
| ✅ `LIVE` | Warp 9.9 + sublight + warp buffer | Warp create/sustain (WarpMoveCommand.cs:266) gated on stored energy ≥ jump cost (capacitors). |
| ✅ `LIVE` | See-first detection | Sensor detection range → the contact/fog model; opens past its own torpedoes. |
| 🟠 `PENDING` | Torpedo damage in the AUTO-RESOLVE total | The auto-resolver stubs guided damage at a flat 0.1 MJ/s (the Sovereign/Miranda caveat) — the photon/quantum punch is under-counted in strength-math battles; it lands in the live tactical sim (MissileImpactProcessor). |
| 🟠 `PENDING` | Minimal / EMH-automated crew | No ship crew-automation dial in the assembler — the build berths the gun/reactor crews (617) the gates count; the canon 'runs on the EMH' automation has no wire. (The engine has CrewAutomationAtb ship-side, not exposed in the assembler.) |
| 🟠 `PENDING` | MULTI-VECTOR ASSAULT MODE (the signature) | NO wire — the engine is one-host-per-ship; a hull cannot split into three independently-warp-capable, independently-fighting sub-entities mid-combat and recombine. This is the defining trait and it is the least-expressible thing on the ship. Missing wire: a ship-fission model (spawn N sub-hulls sharing the parent's parts, each an independent combatant, re-merge). |

**Authenticity notes:**
- FAITHFUL — the glass-cannon identity is the MIN/MAX itself: a small hull maxed for firepower-per-tonne reaches Sovereign-class absolute output at two-thirds the mass. That IS 'a Prometheus out-fights bigger ships.'
- FAITHFUL — 'regenerative shields' maps to the engine's literal shield regen field, and ablative armour to the Toughness pool; both LIVE. Its survivability is shields+plating, not dodging (evasion 16%), exactly as on screen.
- PENDING — MVAM cannot be expressed: the one-host-per-ship engine has no ship-fission model. Marked PENDING, named the missing wire. The defining trait is the least-wired thing on the ship — the honest headline.
- PENDING — the minimal EMH-run crew: the assembler has no ship crew-automation dial, so the build berths the gun crews the gates count; the canon skeleton-crew automation is flagged, not faked.
- CANON DISPUTE flagged: ~16 phaser arrays is a technical-manual figure (12–16, not screen-confirmed); torpedo counts and crew (~171) are secondary; mass is not canon at all. Length ~415 m is best-supported. VERIFIED against Memory-Alpha/Beta knowledge (the workflow's live fetch failed this run; canon re-checked by hand).
- MODEL CAVEAT — per-part masses/damage/heat are the assembler's tuning (to make the gates behave), computed against the LIVE catalog via the build harness; the phaser/torpedo COUNTS are the canon shape, up-gunned transparently in the min/max.

**Sources:** Memory Alpha — Prometheus class · USS Prometheus (NX-74913); VOY 'Message in a Bottle' (2374) · Memory Beta — Prometheus-class recognition data (~415 m, 15 decks, secondary armament/crew figures) · Ex Astris Scientia — Prometheus-class / MVAM analysis · Canon re-verified by hand (the workflow's live fetch failed this run — see flags); numbers computed against docs/assembler/entityassembler.html via the build harness

---

## Imperial I-class Star Destroyer

*Star Wars · dreadnought host.* A 1,600 m Imperial wedge capital built as a do-everything conqueror: a line battleship (60 heavy turbolasers + 60 ion cannons), a full fighter wing (72 TIEs), an invasion army (20 AT-ATs, 30 AT-STs, ~9,700 stormtroopers, a prefab garrison), and an orbital-bombardment platform — all in one hull. It wins by needing no support fleet to take a system; it sacrifices per-role peak (a dedicated carrier out-carries it, a dedicated battleship out-guns it) for the fact that neither can do all four jobs alone.

**Canon —** **Physical:** 1,600 m long (firm across canon + Legends — the single most consistent ISD figure); beam ~900 m, height ~375 m. Deck count not fixed. MASS is NOT canonically stated by any primary source — scaled estimates run to tens of millions of tonnes, i.e. far beyond the ~20,000 t ship budget, which is why it needs a bigger host tier. **Defense:** Heavy durasteel armour + deflector shields (bow shields especially strong). A brawler that tanks, not dodges — evasion ~0 for a 1,600 m capital. **Mobility:** Solar-ionization reactor; Class-2 hyperdrive (Class-8 backup); sublight ion drives. Not agile — a slow, inexorable wedge. **Crew/control:** ~37,085 officers and enlisted (incl. ~275 gunners) + a ~9,700 stormtrooper ground contingent. Total complement ~46,785 souls.

**★ Signature that must be expressed:** THE SIGNATURE: the do-everything capital — one ISD combines battle line + carrier + army + garrison + bombardment and can conquer a planet with no support fleet. No separation gimmick (that's Trek's Prometheus); the ISD's signature is monolithic multi-role self-sufficiency.

**Armament (canon):**
- Taim & Bak XX-9 heavy turbolasers ×60 (well-supported: ICS + West End + Wookieepedia)
- Borstel NK-7 ion cannons ×60 (well-supported)
- Phylon Q7 tractor beam projectors ×6 (DK Incredible Cross-Sections / Wookieepedia) — DISPUTED: West End Games lists 10
- Lighter turbolaser / laser point-defense batteries (mentioned for anti-fighter work; not consistently counted)

**Chassis / host:** NEW mobile DREADNOUGHT host — 60,000 t / 44,000 m³ / crew-ref 40,000. A big warship (dodges ~0 at this size, FTL-capable), inheriting every ship- and station-scale part. Added to the assembler for this build.

**Build — capability → component → count → door:**

| Capability | Component | Count | Door |
|---|---|---|---|
| Heavy turbolaser battle line | Heavy Turbolaser Battery | 78 | Weapons |
| Ion cannon battery | Ion Cannon | 60 | Weapons |
| Anti-fighter point defense | Point-Defense Battery | 12 | Weapons |
| Tractor projectors | Tractor Beam Projector | 6 | Utility |
| 72-TIE fighter wing | Flight Deck — StrikeCraft bay | 2 | Bay |
| AT-AT / AT-ST invasion force | Troop & Vehicle Bay | 4 | Bay |
| ~9,700 stormtroopers + garrison | Passenger Berths | 25 | Bay |
| Deflector shields | Shield Generator | 14 | Defense |
| Heavy armour | Composite Armour Plating | 22 | Defense |
| Reactor power | Capital Reactor | 3 | Power |
| Heat rejection | Radiator Bank | 60 | Power |
| Hyperdrive + jump buffer | Warp Drive + Capacitor ×6 | 1 + 6 | Propulsion/Power |
| Sublight + fuel | Ion Drive ×6 + Fuel Tank ×4 | 6 + 4 | Propulsion |
| Ammunition + stores + endurance | Magazine·2 + Ammo Bunker·2 + Cargo·6 + Provisions·6 + Hydroponics·4 | — | Logistical |
| Crew city (37,085) + command | Quarters·34 + Life Support·20 + Medbay·6 + Bridge·2 + Flag Suite·1 | — | Civic/Command |
| Detection + fire control + ECM | Sensor·4 + Long-Range·3 + Fire-Control·2 + Targeting·2 + ECM·2 | — | Sensors |

**Gates closed:**
- **mass ≤ budget:** 57,684 / 60,000 t (96.1%) — filled to the wall; BINDING
- **volume ≤ cap:** 32,766 / 44,000 m³ (74.5%) — ~11,000 m³ stranded (the roomy budget)
- **power ≥ draw:** draw 1,068 MW / supply 2,400 MW (3 capital reactors)
- **heat ≥ 0:** dissip 2,400 − gen 2,040 = +360 MW margin (60 radiators — the ISD's real cost)
- **berths ≥ crew:** 5,100 berths / 3,420 assembler crew
- **life ≥ crew:** 5,000 / 3,420
- **detect ≥ weapon:** 490 km detect / 220 km longest weapon
- **warp battery ≥ jump:** 4.50 MJ stored / 1.25 MJ needed

**Host scale:** OVERFLOWS the stock ~20,000 t ship host (the Venator/Sovereign tier). The Venator is 1,137 m and fills that host; the ISD is 1,600 m (~1.4× longer, ~2.8× hull volume) AND carries a strictly larger stack — a Venator-class fighter wing PLUS a full ground army PLUS a heavier gun line. So — exactly as the Death Star needed a new Battle Station host — this build ADDS a mobile DREADNOUGHT / super-capital tier between the ship and the megastructure: 60,000 t / 44,000 m³ (≈3× the ship host), sized so the four roles seat without cannibalizing each other. Verified: it closes at 96% mass on that budget.


**Emergent (what the assembler computes):** Firepower 1,130.6 MJ/s · Toughness 968 · Shield 70 MJ · Evasion 13% · 140-fighter deck (72-TIE wing) · 64 vehicles · 10,000 troops · deploy ~9 days. Against the min/maxed Venator (143 MJ/s), the ISD delivers ~8× the firepower (canon: 60 heavy turbolasers vs the Venator's 8 — the ratio is right) and out-armours it, while matching its carrier + army role-count. It is the highest-firepower MOBILE ship in the set (below only the Death Star megastructure). The whole point: no single-role peak, but no peer at doing ALL of it alone.


**Min/max — filling the empty space:** binding = *MASS (96.1% used vs 74.5% volume) — the real compute corrects the research guess of volume-bound. The ~11,000 m³ of volume is STRANDED: you literally cannot spend it once mass caps.*; metric = FIREPOWER (heavy-energy tube count) — the battle-line half of the do-everything identity, with toughness as the secondary sink once the roles are seated.. Residual poured in: +18 heavy turbolasers (60→78) — each paying its FORCED costs: +8 MW draw (→ +1 capital reactor), +20 MW heat (→ +radiators), +22 gun-crew (→ +berths); +12 armour (10→22) — the best firepower-neutral use of the last stranded mass: dense, near-zero volume, pure toughness; +6 shields (8→14) — deeper deflectors; +15 radiators (45→60) to hold heat margin ≥ 0 (the wall) · +1 capital reactor · +berths for the added gun crews. **Result: Firepower 929 → 1,130.6 MJ/s (+22%) · Toughness 750 → 968 (+29%) · Shield 40 → 70 MJ. Mass 80.5% → 96.1% (filled to the wall); ~11,000 m³ volume left stranded — the textbook min/max shape (optimise the scarce budget, ignore the roomy one).** (All 8 gates green after the fill: reactor 2,400 MW ≥ draw 1,068; radiators 60 hold +360 MW heat margin with 78 turbolasers + 60 ion + 12 PD firing; berths 5,100 ≥ crew 3,420; warp buffer 4.5 ≥ 1.25 MJ. Stopped at 96.1% mass — the next turbolaser train (gun + reactor + radiator + berth) trips the mass cap.)

**Honesty ledger:**

| | Capability | Reader (LIVE) or missing wire (PENDING) |
|---|---|---|
| ✅ `LIVE` | Heavy turbolaser firepower | Σ weapon dps → ShipCombatValueDB.Calculate → AutoResolve (Combat CLAUDE.md); design-time draw≤supply gate. |
| 🟠 `PENDING` | Ion cannon (disable-not-destroy) | Raw firepower counts (LIVE), but no wire distinguishes an ion 'disable' from a turbolaser kill — the signature ion effect has no dedicated reader. |
| ✅ `LIVE` | Deflector shields + heavy armour | Shield pool → CombatKernel.ResolveShield; armour → Toughness (DamageComplex path). |
| 🟦 `LIVE-gated` | Reactor / heat / crew / warp gates | All four design-time gates close arithmetically against the live catalog (draw≤supply, heat≥0, berths≥crew, warp battery≥jump) — computed via the build harness. |
| ✅ `LIVE` | Long-range detection | Sensor engine + contact model (Sensors CLAUDE.md) — rigorous and read; opens past its guns. |
| ✅ `LIVE` | AT-AT / AT-ST / stormtrooper invasion (land the army) | Vehicle + Personnel carry-class → GroundBayAtb → GroundTransport — the invasion chain the game actually runs. The ground-campaign half is the ISD's most-wired role. |
| 🟦 `host-split` | Orbital bombardment (Base Delta Zero) | The guided path CAN hit a surface (DamageProcessor.OnColonyDamage, gotcha #8), so missile bombardment is wired; routing the TURBOLASER gun line to surface fire is not (EMERGENT/PENDING). |
| 🟠 `PENDING` | 72-TIE fighter wing (carrier ops) | The deck STORES the wing but nothing launches it — no fighter launch/recovery or small-craft combat wire (the Venator's exact gap). |
| 🟠 `PENDING` | Tractor beam (pull / capture / hold) | tractor is in the part enum but has no combat/movement reader — nothing pulls or holds a target. |
| ⬜ `READ` | Sector-flagship command | Flag/admin suite is read (AdminSpaceAtb/AdministratorDB) but the active command-behaviour arm is gated off by default. |
| 🟠 `PENDING` | DO-EVERYTHING SIGNATURE (solo planetary conquest) | No single wire expresses 'this one hull takes a planet unaided.' It is an emergent composition needing carrier ops + surface-fire bombardment BOTH live — and both are pending — on top of the (live) ground landing. The signature is the least-wired thing on the ship. |

**Authenticity notes:**
- FAITHFUL — the do-everything capital is expressed as four SEATED roles at once: 78-turbolaser battle line, 72-TIE deck, 64-vehicle + 10,000-troop army, bombardment path — and the ground-invasion half is genuinely LIVE (GroundBayAtb→GroundTransport). No other ship in the set carries all four.
- FAITHFUL — needed a new host tier, exactly like the Death Star: a 1,600 m capital does not fit the 20,000 t ship budget, so a mobile 60,000 t Dreadnought tier was added. It closes at 96% mass, verified against the live catalog.
- FAITHFUL (min/max) — mass-bound, so the residual went to the density king (heavy turbolasers) + armour (dense, volume-free), paying every forced cost; heat is the ISD's true price (60 radiators). Firepower 929→1,131, ~8× a Venator — the canon 60-vs-8 turbolaser ratio.
- PENDING — the SIGNATURE is the least-wired thing: 'solo planetary conquest' needs carrier launch (pending) + surface turbolaser-fire (pending) on top of the live ground landing. Two of the three legs of the do-everything identity are engine-pending — flagged, not sold.
- PENDING — ion 'disable', tractor 'capture', and TIE 'launch' all echo gaps the Venator/Death Star builds flagged: firepower counts, but the distinctive non-damage effects have no readers.
- CANON DISPUTE flagged: tractor projectors 6 (ICS) vs 10 (West End); mass not canon at all; lighter PD-battery + torpedo counts interpretive. 1,600 m / 37,085 crew / 72 TIE / 20 AT-AT / 30 AT-ST / ~9,700 troops are well-established Legends figures (Disney echoes them). VERIFIED by hand (the workflow's live fetch failed this run).
- MODEL CAVEAT — every gate is closed ARITHMETICALLY against the live catalog via the build harness (not reasoned) — the research agent could not read the file this run, so these numbers are the corrected, computed ones.

**Sources:** Wookieepedia — Imperial I-class Star Destroyer (1,600 m; crew 37,085; 72 TIE / 20 AT-AT / 30 AT-ST / ~9,700 troops) · Star Wars: Incredible Cross-Sections (DK) — 60 heavy turbolasers, 60 ion cannons, 6 tractor projectors · West End Games — Imperial Sourcebook (60/60/10 — the tractor-count dispute) · Star Wars: The Essential Guide to Vehicles and Vessels — role/complement · Canon re-verified by hand (the workflow's live fetch failed this run — see flags); every gate computed against docs/assembler/entityassembler.html via the build harness


---

## The deferred backlog — engine work these builds surfaced (PENDING wires, prioritized)

*Every PENDING above names a missing wire. Deduplicated and grouped, these are the ground-combat features that would let the assembler express these units faithfully — a real, sourced work list, not a wishlist.*

| # | Missing wire | Who needs it | Detail |
|---|---|---|---|
| 1 | **Ground weapon PENETRATION + per-shot ALPHA** | **#1 — hit by 3 of 7 units** (Genestealer rending, Clone DC-15A, AT-TE mass-driver). | `Penetration`/`PerShotEnergy` exist only on the monolithic base-mod `GroundUnitAtb`, not on the `GroundWeaponAtb`/space-weapon-mount the assembler uses, so an assembled unit's weapon reads `Penetration=0` — it wins by undodgeable VOLUME, never by cracking armour. **Wire:** add `Penetration`/`PerShotEnergy` to `GroundWeaponAtb` + carry them through `GroundUnitAssembly.ToGroundUnitDesign` into `GroundWeaponMount`. |
| 2 | **Synapse / instinctive-behaviour (the Tyranid signature)** | **Hit by all 3 Tyranids** (Termagant dependence, Genestealer/Carnifex buff+degrade). | No per-unit combat morale/synapse/fury field on `GroundUnit`; the formation system does the OPPOSITE (leader-loss = seamless reassign, no penalty). **Wire:** a `GroundUnit.SynapseLink`/morale field + a proximity-to-synapse check in `GroundForcesProcessor` + an instinctive-behaviour/rout effect. Pairs with the **Aura door** (Shadow-in-the-Warp / synapse radius — currently zero engine matches). |
| 3 | **A surface troop-carry BAY for ground units** | **The AT-TE signature.** | No ground component carries/deploys other ground units — `GroundBayAtb` is ship-only (`GroundTransport.cs:761`). **Wire:** a surface troop-carry-bay atb + an embark/deploy-on-surface order for ground units. (The AT-TE rides a dropship fine — Vehicle carry-class LIVE — it just can't carry its own squad.) |
| 4 | **Regeneration / a damaged-and-healing unit state** | **The Carnifex signature.** | Ground units are whole-or-dead; there is no sub-max damaged state and no per-tick heal reader. **Wire:** a damaged-unit HP state + a `SelfRepairRate` reader in the ground casualty step. |
| 5 | **A charge / closing-impact term** | **The Carnifex 'Living Battering Ram'.** | The melee Attack lands, but there is no momentum/charge bonus on contact. **Wire:** a closing-impact term in `ResolveRegionCombat` keyed on distance-closed. |
| 6 | **Ground infiltration / ambush deploy + a stealth dial** | **The Genestealer.** | The initial-engagement spread always opens a gap at the holder's range; no per-unit signature-suppression. **Wire:** a ground stealth/signature field + an ambush-deploy override of the initial spread. |
| 7 | **A firing-ARC / facing model** | **The ARC-170 tail turret.** | Combat is aggregate/bucketed; a rear-arc defensive turret contributes raw Firepower but 'covers the six' has no reader. **Wire:** optional per-weapon arc + a facing state (a large model change — flagged, low priority). |
| 8 | **Guided-weapon damage in the AUTO-RESOLVE total** | **The Miranda rollbar; every torpedo ship.** | The auto-resolver stubs guided damage at a flat 0.1 MJ/s, so torpedoes read under-counted in strength-math battles (they guide + impact fine in the live tactical sim). **Wire:** real guided-weapon damage in `BuildFireMix`/the `CombatKernel` firepower sum. |
| 9 | **Parasite-fighter launch (fly off a carrier's deck)** | **Every Star Wars fighter; the Venator's other half.** | `ParasiteLauncherReady` (`EventTypes.cs:400`) has zero emitters — a fighter built as its own ship is complete, but sortieing off a carrier deck in space combat has no wire. DEAD. |
| 10 | **Chassis SUBSTRATE axis (organic / bio)** | **Cosmetic across the Tyranids.** | No substrate field on any chassis atb; all four frames are Mechanical, so 'bio' (self-feeding, no reactor/crew, biomass upkeep) is skin. **Wire:** a `Substrate` member + its feed/regen/no-power requirement rules (design-locked in `docs/economy/DESIGNER-NORTH-STAR.md`, unbuilt). |


---

## Assembler additions these ground builds REQUIRE (LIVE engine wires — designed, not yet in the interactive tool)

**Honest status:** the two capital ships above (Prometheus, ISD) are **clickable presets in the published Entity Assembler** — they were built entirely from parts already in the tool and every gate was re-verified through its real `compute()`. The five ground units and the two earlier ship units (ARC-170, Miranda) are **dossier cards** authored from the engine research; they are **not yet clickable presets**. Three of them additionally need ground components the interactive tool does **not yet expose** — listed below. Each is a genuinely **LIVE** engine wire (traced to a reader at file:line), so adding it is exposure, not invention — but until it is added to `entityassembler.html`, treat this as the designed next step, not a shipped feature:

- **Sealed Systems** → `GroundSealAtb.Sealing` → `EnvironmentalResistance{Vacuum,ToxicAtmosphere}` → E4 attrition (`GroundUnitAssembly.cs:311-315` → `GroundForcesProcessor.IsDamageEffect`). *Clone trooper, Genestealer, Carnifex.*

- **Training Cadre** → `GroundTrainingAtb.TrainingMultiplier` → baked into Attack + MaxHealth at raise (`GroundForcesDB.cs:607-611`; assembler reads best cadre `GroundUnitAssembly.cs:203,249`). *Clone trooper, Genestealer.*

- **Bio-Musculature / Ground Augment** → `GroundAugmentAtb` StrengthBonus (carry) / ToughnessBonus (HP) / EvasionBonus (dodge) → `GroundUnitAssembly.cs:105-107,189,191`. *Genestealer (the load-bearing carry-raise that lets the claws fit), Carnifex, Termagant.*


*Doc generated from the 7-agent research pass, 2026-08-04. Companion: the published Entity Assembler dossier artifact.*
