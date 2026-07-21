> ⚠ **SURVEY DRAFT (adversarially verified 2026-07-21).** This per-battle spec was attacked by a verifier on
> authenticity + engine-reality + completeness; it carries known load-bearing errata (e.g. the ground-vs-space
> weapon-composition gap, and battle-specific canon fixes) that are **reconciled in `FRANCHISE-LITMUS-TEST.md`**
> and corrected at build time. Treat the master doc as the authority where they differ.

# Franchise Litmus Test — The Battle of Geonosis (Star Wars)

**As of:** 2026-07-21 · **status: SPEC (read-only survey — no engine/data changed).** A per-unit
cradle-to-grave BUILD SPEC: every unit that fought at Geonosis, mapped onto the components and chassis the
game can **actually build today**, with an honest gap ledger where it can't.

> **What this doc is.** Not lore. It is the answer to one question the developer asked: *"if a battle has a
> Stormtrooper in it, can I actually build that Stormtrooper — the E-11 blaster and all — out of the Component
> Designer and Entity Assembler?"* Geonosis is the widest probe of the four litmus battles because it spans
> **every chassis the game has**: foot soldiers, walkers, tanks, gunships, fighters, and capital ships, on two
> completely different force philosophies (a cloned human army vs. a mass-produced droid army). If the build
> system can stage Geonosis, it can stage almost anything.
>
> **How to read a unit block.** Each unit lists what makes it *itself* in canon, then the exact
> **Chassis + components** you'd assemble (citing the real template id and the file it lives in), then a
> **LEDGER** (what EXISTS, what's MISSING, what NEEDS-CHANGE), then a one-line **VERDICT**. The verdicts are the
> point — a litmus test earns its keep by exposing what *breaks*, not by pretending everything fits.

---

## 1. The battle in one paragraph

Geonosis is the opening battle of the Clone Wars — the moment a cold war turns hot. On the desert world of
Geonosis, a Jedi rescue mission gets pinned in a gladiatorial arena, and just as the Separatists' droid army is
about to execute the prisoners, a fresh, secret **Grand Army of the Republic** — 192,000 clone troopers ferried
in on **Acclamator assault ships** — drops out of the sky. What follows is the game's first true combined-arms
land battle: clone infantry and Jedi backed by six-legged **AT-TE walkers**, **SPHA-T** heavy-artillery walkers,
and swarms of **LAAT/i gunships**, against a **Confederacy droid army** — battle droids, super battle droids,
rolling **droidekas**, missile-slinging **Hailfire droids**, **spider-droid** walkers, and native winged
**Geonosian** warriors — all pouring out of the buried **Trade Federation Core Ships** (the sphere-hulled droid
control ships). It is iconic because it is the first time you see the *whole* Star Wars war machine on one field:
the Force and the lightsaber next to the mass-driver and the missile rack. That range is exactly why it's the
right stress test.

---

## 2. Force composition (the order of battle)

**Grand Army of the Republic (attacker, dropping from orbit):**
- **Infantry:** Phase I clone troopers (DC-15A rifle / DC-15S carbine), organized in legions; **ARC troopers**
  (elite commandos); **clone commanders** leading each unit.
- **The Jedi strike team:** ~212 Jedi under Mace Windu, plus Yoda arriving with the fleet — lightsabers + the Force.
- **Armor / walkers:** **AT-TE** six-legged assault walkers (the workhorse), **SPHA-T** self-propelled heavy
  artillery walkers (the anti-capital beam), **TX-130 Saber** repulsor fighter-tanks.
- **Air:** **LAAT/i** ("Low Altitude Assault Transport / infantry") gunships — flying APCs with door guns and
  rocket pods, the thing that actually delivers the troops onto the field.
- **Fleet:** a wall of **Acclamator-class** assault ships in low orbit — the source of every clone, walker, and
  gunship on the ground.

**Confederacy of Independent Systems (defender, disgorged from the Core Ships):**
- **Infantry (droids):** **B1 battle droids** (E-5 blaster, the cheap swarm), **B2 super battle droids** (armored,
  wrist blaster), **droidekas / destroyer droids** (twin blasters + a personal shield, roll into position).
- **Native infantry:** **Geonosian warriors** — winged, carrying sonic blasters.
- **Vehicles / walkers:** **Hailfire droids** (twin missile racks on two giant hoop-wheels — fast missile tanks),
  **homing/dwarf spider droids** (beam-cannon walkers).
- **Air / fighters:** **Geonosian (Nantex-class) starfighters**, **Vulture droid** starfighters (transforming).
- **Fleet / hulls:** **Trade Federation Core Ships** (the Lucrehulk-derived sphere droid-control ships) buried in
  the crust, plus the droid **foundries** stamping out the army.

**The setting itself** (not units, but where the fight sits in the engine): the **Petranaki arena** and the desert
are **terrain/regions** on the planet surface map (`PlanetRegionsDB`); the **droid foundries** are **factory
installations** (the `factory` template, `installations.json:380`) that a captured Geonosis would hand you. The
game already models "capture the region, capture the buildings on it" — so the foundries are a real prize, not set
dressing.

---

## 3. THE UNITS

Notation in each BUILD line: `template-id {dial: value}` → what it contributes. Every ground component below is a
real base-mod template; the ground kit all bills to **stainless-steel + aluminium**, which every start faction
already has unlocked (the same minerals the Bunker uses), so the **cradle-to-grave chain is intact** for the stock
parts: iron/bauxite mined → steel/aluminium refined → part built at a colony → designed in the Component Designer →
research-gated → assembled on a frame in the Entity Assembler → the unit fights → a component is shot off and lost.

### How ground weapons map to blasters (read once, applies to every trooper below)

The ground weapon component (`GroundWeaponAtb`) carries four dials: **CarryMass** (what the frame must lug),
**Attack** (firepower), **Range** (in hexes), and **Mode** — `0=Melee 1=Ballistic 2=Energy 3=Artillery`
(`ground-rifle`, `installations.json:2073`). A **blaster bolt** — a finite-velocity plasma/laser bolt — maps to
**Mode 2 (Energy)**: the resolver treats Energy as "dodgeable but a shield only half-soaks it," which is exactly
how a blaster behaves (a fast target or a Jedi can beat it; a shield bleeds). So a blaster rifle = the
`energy-weapon` template ("Plasma Projector," `installations.json:2775`) dialed to the right Attack/Range, **not**
a new template. That one mapping unlocks nearly every trooper in this battle.

---

## REPUBLIC — Infantry (chassis: human)

### UNIT: Phase I Clone Trooper (line infantry, chassis: human)
**AUTHENTIC CAPABILITIES:** A cloned human soldier in white Phase I plastoid armor, carrying the **DC-15A blaster
rifle** (a long-range plasma-bolt rifle) or the shorter **DC-15S carbine**; disciplined, uniform, mass-produced,
average toughness.
**BUILD:**
- Chassis `human-frame` {BaseStrength 100, BaseHP 200, Locomotion 0=Foot} (`installations.json:1994`) → the soldier's body + carry budget.
- `energy-weapon` {Attack ~60, Range 3, Mode 2=Energy} (`installations.json:2775`) → the DC-15A blaster rifle (long reach, energy nature). *Carbine variant = same template, Range 1–2, slightly lower Attack — a dial, not a new part.*
- `ground-plating` {HP 150, Defense 5} (`installations.json:2143`) → Phase I plastoid armor (health + a little flat soak).
**LEDGER:**
- **EXISTS:** the whole soldier — frame + energy blaster + body armor are all stock ground templates; the carry gate (`GroundUnitAssembly.Compute`) confirms a human frame can shoulder a rifle + light plate.
- **MISSING / NEEDS-CHANGE:** none. Stun-vs-kill mode (the blaster's secondary) is a firing-mode dial the ground resolver doesn't expose yet — cosmetic here.
**VERDICT:** **BUILDABLE-TODAY** — the baseline "E-11-grade done correctly" case; a blaster is an Energy-mode ground weapon on a human frame.

### UNIT: ARC Trooper (elite commando, chassis: human)
**AUTHENTIC CAPABILITIES:** Advanced Recon Commando — a clone bred and trained past the line standard: harder,
faster, twin blaster pistols, kama and pauldron, independent-minded. The "elite version of the same body."
**BUILD:**
- Chassis `human-frame` + `energy-weapon` (twin blasters = one weapon component tuned for higher rate/Attack) + `ground-plating`.
- `ground-training-cadre` {TrainingMultiplier ~1.5} (`installations.json:3091`) → the veterancy stamp: multiplies the fielded unit's Attack **and** toughness (the ground echo of a ship's `unit-caliber` elite crew, `electronics.json:685`). This is *the* component that makes an ARC ≠ a line clone on the same frame.
- `reflex-booster` {EvasionBonus +0.4} (`installations.json:3012`) → the commando's edge (dodge).
**LEDGER:**
- **EXISTS:** veterancy is a real, costed component (`GroundTrainingAtb`) — a more-trained unit is genuinely dearer to field (its mass scales with the multiplier). Elite-ness is earned cradle-to-grave, not a label.
- **MISSING:** nothing load-bearing.
**VERDICT:** **BUILDABLE-TODAY** — validates the Enhancers layer (a veteran cadre is a buildable part).

### UNIT: Clone Commander (officer, chassis: human)
**AUTHENTIC CAPABILITIES:** A clone (Cody, Bly, etc.) who **leads** a formation — his presence coordinates and
stiffens the troops around him; personally a hardened veteran with command gear.
**BUILD:**
- Chassis `human-frame` + `energy-weapon` + `ground-plating` + `ground-training-cadre` (a veteran).
- Set as the **formation leader** (`GroundFormation.LeaderUnitId`) — the ground echo of a fleet flagship. Leadership already exists; a formation marches and fights as one under its leader, and leader-loss reassigns cleanly (no death spiral).
**LEDGER:**
- **EXISTS:** the leader *slot* (a commander is the unit you name as the formation's leader).
- **NEEDS-CHANGE:** there is **no ground command component** that grants a combat *bonus* to nearby units. The ship command node (`ship-command` → `AdminSpaceAtb`, `storage.json:246`) is **`MountType: ShipComponent` only** — it will not mount on a `GroundUnit`. So a clone commander today is "a veteran who happens to be the leader," not "a leader who buffs his squad." Fix = a small **ground command component** (`GroundUnitCommandAtb` or add the `GroundUnit` flag to a command part) that the resolver reads as a formation-wide Attack/morale multiplier. Ties to design-hole **H10b** (command-as-a-dial).
**VERDICT:** **BUILDABLE-TODAY** (as a veteran leader) — but the "command aura" that makes a commander *matter* is a missing component; flagged NEEDS-CHANGE.

### UNIT: Jedi Knight (Kenobi / Windu / Yoda) (hero infantry, chassis: human) — THE BOUNDARY PROBE
**AUTHENTIC CAPABILITIES:** A **lightsaber** — a melee energy blade that also **deflects blaster bolts back at the
shooter** (a weapon that is simultaneously a defense); plus **the Force** — precognition (dodge everything),
telekinesis (throw droids), enhanced speed and leaping. The single hardest unit in the whole battle to model
honestly.
**BUILD (the buildable half):**
- Chassis `human-frame` + `claw-weapon` re-natured to Energy melee {Attack high, Range 0=Melee, Mode 2/0} (`installations.json:3256`) → the lightsaber's *cutting* — a melee weapon of energy nature falls straight out of the doors (Melee door × Energy nature).
- `reflex-booster` {EvasionBonus high} (`installations.json:3012`) → the *closest stand-in* for Force precognition: a very high innate dodge.
**LEDGER — this is where it breaks, honestly:**
- **EXISTS:** the lightsaber *as a melee weapon* (an energy-nature melee) and a high-dodge unit.
- **MISSING — design-hole H7 (hybrid weapon+defense):** the saber **deflecting bolts** is a weapon that is *also* a defense. No component today does both. This is catalogued as H7 and has no engine support — a deflect effect (incoming aimed-energy fire has a chance to be nullified or reflected) would be a new resolver rule.
- **MISSING — design-hole H4 (innate powers are NOT gear):** **the Force is not a component and must never be faked as one.** Per the locked designer boundary, telekinesis/precognition/mind-trick are **People-system traits** (a pilot/commander trait that emits effects), not something you build in the Component Designer. The `reflex-booster` stand-in gets you "dodges well," but it is not the Force — it's a costume. The real home is a Jedi **trait** on the People layer that emits a dodge buff + a push effect + a subvert effect onto the unit it inhabits.
**VERDICT:** **NEEDS-ENGINE-WORK** — the *cutting* is buildable today, but the two things that make a Jedi a Jedi (bolt-deflection = H7, the Force = H4/People traits) are neither in the parts bin nor should the Force ever be. This is the litmus test doing its job: it names the exact edge of the designer.

---

## REPUBLIC — Walkers & Vehicles

### UNIT: AT-TE (All Terrain Tactical Enforcer) (assault walker, chassis: walker)
**AUTHENTIC CAPABILITIES:** A six-legged armored assault walker; one heavy **projectile mass-driver cannon**
(top turret), **six anti-personnel laser turrets**, thick armor, climbs sheer terrain, and carries a **squad of
troops** in its belly.
**BUILD:**
- Chassis `walker-frame` {BaseStrength 400, BaseHP 1000, Locomotion 2=Walker} (`installations.json:2696`) → the striding war-machine body (scale the dials up for the AT-TE's bulk).
- `ground-cannon` {Attack 220+, Range 3, Mode 3=Artillery} (`installations.json:2626`) → the heavy top mass-driver (a projectile, indirect-fire nature).
- `energy-weapon` ×6 {low Attack, Range 1–2, Mode 2=Energy} → the six anti-personnel laser turrets (many light energy mounts).
- `ground-plating` (heavy dial) → the walker's thick armor.
- `ground-locomotion` {RoughHandling ~1.0} (`installations.json:3370`) → all-terrain leg handling (crosses broken ground evenly — a real combat edge on Rough terrain via `GroundTerrain.LocomotionTerrainMult`).
**LEDGER:**
- **EXISTS:** the walker frame, the heavy cannon, the laser turrets, heavy armor, and the "wrap the legs to cripple it" weak point — kill the locomotion component and the walker is immobilized, which is exactly the existing component-level damage model. The famous vulnerability falls out for free.
- **MISSING — design-hole H6 (carrier / nested units):** the AT-TE's **troop bay** carrying infantry has no ground home. The troop-bay component (`GroundBayAtb`, `storage.json:301`) is **`MountType: ShipComponent` only** — it carries troops *from orbit on a ship*, not on a ground vehicle. A ground vehicle that ferries infantry needs the `GroundUnit` mount flag added to a bay component (H6, "reuse units-as-entities — a unit whose bay holds child units").
**VERDICT:** **BUILDABLE-TODAY** (as a fighting walker, weak point and all) — minus the troop-ferry role, which is a mount-flag gap (H6).

### UNIT: SPHA-T (Self-Propelled Heavy Artillery — Turbolaser) (siege walker, chassis: walker)
**AUTHENTIC CAPABILITIES:** A twelve-legged self-propelled artillery walker whose single enormous **anti-armor
beam** can hurt a capital ship; slow, fragile up close, devastating at range; needs a huge power feed to charge.
**BUILD:**
- Chassis `walker-frame` (scaled large) → the striding artillery platform.
- `energy-weapon` {Attack very high, Range 10 (max hexes), Mode 2=Energy} → the anti-armor turbolaser beam (long-reach directed energy). *For capital-grade punch you can instead mount the ship-grade `pulse-laser` (`weapons.json:862`) — it already carries the `GroundUnit` mount flag* — a ship's beam on a walker "just works" (universal mounting).
- `reactor` {big Mass} (`energy.json:5`, `MountType` includes `GroundUnit`) → the power feed the big beam draws (the supply gate refuses the beam without enough reactor — exactly the SPHA-T's "must deploy and charge" identity).
- `ground-plating` (thin) → lightly armored (the SPHA-T is fragile; that's authentic).
**LEDGER:**
- **EXISTS:** everything — the long-range energy beam, the reactor supply feed, and the **power gate** (`WeaponSupply` / `GroundUnitAssembly` refuses an under-powered energy weapon) that makes "artillery must bring its own reactor" a real constraint, not flavor.
- **MISSING:** nothing load-bearing. (Anti-*capital* fire from a ground unit up at ships in orbit is the ground→space link, which is one-directional today via bombardment; ground-to-orbit is a frontier, not a Geonosis blocker.)
**VERDICT:** **BUILDABLE-TODAY** — a marquee validation of the supply/budget gate (a giant beam forces a giant reactor).

### UNIT: TX-130 Saber-class Fighter Tank (repulsor tank, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** A fast **repulsorlift** (hover) tank with twin laser cannons and a beam/missile mount;
nimble, hit-and-run, floats over terrain.
**BUILD:**
- Chassis `vehicle-frame` {BaseStrength 800, BaseHP 1500} (`installations.json:2547`) with `ground-locomotion` {SpeedFactor high, Locomotion→Hover} → the repulsor tank body (fast, floats).
- `energy-weapon` ×2 {Range 2–3, Mode 2=Energy} → twin laser cannons.
- `ground-plating` (medium) → tank armor.
**LEDGER:**
- **EXISTS:** the whole tank. Hover locomotion is a chassis `Locomotion` value (3=Hover) / a `ground-locomotion` dial; the speed edge is real.
- **MISSING:** nothing. (The "repulsorlift floats over water/rough" nuance = the `ground-locomotion` `Amphibious`/`RoughHandling` dials — already present.)
**VERDICT:** **BUILDABLE-TODAY** — a clean vehicle-frame + hover-drive + twin energy mounts.

---

## REPUBLIC — Air & Ships

### UNIT: LAAT/i Gunship ("the flying APC") (assault gunship, chassis: air — GAP) — THE AIR PROBE
**AUTHENTIC CAPABILITIES:** A **low-altitude assault transport**: it *flies*, carries a platoon of clones (and a
speeder), fires from **door laser turrets** and **chin/wing beam turrets**, and slings **anti-armor rocket/missile
pods**. The thing that makes Geonosis a *gunship* battle — it strafes the field and drops troops.
**BUILD (two honest compromises, neither exact):**
- **Compromise A — model it as a small ship:** `ship-hull` (fighter/small, `installations.json:68`) + `troop-bay` (`storage.json:301`) + `laser-weapon` (`weapons.json:5`) + `missile-launcher` (`weapons.json:235`) + engine. This is buildable *today* — but it then lives in the **space/orbital** combat layer, not on the ground hex map, so it can't strafe ground troops.
- **Compromise B — model it as a fast Hover ground vehicle:** `vehicle-frame` + `ground-locomotion` {Hover, high SpeedFactor} + `energy-weapon` ×2 (door guns). But ground vehicles can't carry troops (the H6 bay gap above), and a hover unit isn't *airborne* — it's on the 2D surface.
**LEDGER:**
- **EXISTS:** the guns, the missiles, the hull, and (ship-side) the troop bay.
- **MISSING — a battle-surfaced engine gap not in the catalogued 12 holes: there is no atmospheric AIR-COMBAT layer.** The ground war is a 2D surface hex map; the space war is the orbital layer. A **gunship that flies *above* the ground battle, strafing it and landing troops into it**, is neither — it needs an air/altitude band that couples to the ground layer. Plus the ground **troop-ferry** gap (H6).
**VERDICT:** **NEEDS-ENGINE-WORK** — the *hardware* (guns, missiles, troop bay) all exists, but the LAAT's defining role (close air support + air-mobile troop insertion) has no home. This is Geonosis's biggest load-bearing gap: at this battle, air power *is* the story.

### UNIT: Acclamator-class Assault Ship (orbital assault transport, chassis: ship)
**AUTHENTIC CAPABILITIES:** A wedge-hulled assault ship that carries the whole landing force — thousands of clones,
plus AT-TEs, SPHA-Ts, and LAAT gunships — down from orbit; armed with turbolaser and point-defense batteries;
hyperdrive-capable.
**BUILD:**
- `ship-hull` (large, high Mass Budget) (`installations.json:68`) → the assault-ship frame.
- `troop-bay` ×many (`storage.json:301`) → the clone/vehicle holds (this is the ship-side troop carry that works today).
- `laser-weapon` batteries (`weapons.json:5`) + `flak-weapon` / `point-defense-mount` (`weapons.json:512` / `weapons.json:815`) → turbolasers + point defense.
- `reactor` + `battery-bank` + `conventional-engine`/`scntr-engine` + `alcubierre-warp-drive` (`engines.json:176`) → power + sublight + hyperdrive.
**LEDGER:**
- **EXISTS:** the entire ship — this is the game's home turf (ship = hull + component list + armor, exactly the `default-ship-design-trooper` "Lander Troop Transport" already in `shipDesigns.json`).
- **MISSING — design-hole H6 (carrier/nesting):** an Acclamator *houses and launches* AT-TEs and LAATs (child units), which is the carrier-nesting verify item (troop bay holds *troops* fine; launching *vehicle/aircraft child units* is the H6 frontier).
**VERDICT:** **BUILDABLE-TODAY** — a straight ship assembly; only the "launches its own smaller craft" richness is the H6 verify item.

---

## SEPARATIST — Infantry (droids)

### UNIT: B1 Battle Droid (line droid, chassis: human/droid)
**AUTHENTIC CAPABILITIES:** A thin, cheap, mass-produced humanoid droid with an **E-5 blaster rifle**; individually
weak and a poor shot, dangerous only in huge numbers; **no crew** (it *is* the machine).
**BUILD:**
- Chassis `human-frame` (dialed low: BaseHP low, BaseStrength low — a flimsy body) (`installations.json:1994`).
- `energy-weapon` {Attack low, Range 2, Mode 2=Energy} → the E-5 blaster (weak).
- `crew-automation` (`electronics.json:750`) → drives the crew requirement toward zero (a droid has no crew) — design-hole **H10a**, and it's a real ship-side component today.
- Build *many* — the cheap human frame + cheap blaster keeps the per-unit cost low (the swarm identity comes from cost, not a special part).
**LEDGER:**
- **EXISTS:** frame, weak blaster, and the automation component that zeroes crew (H10a is covered).
- **MISSING:** nothing load-bearing. (A dedicated "droid frame" at lower cost than a human frame would be a nice **new template** — a dial-tuned `human-frame` gets you 90% there today.)
**VERDICT:** **BUILDABLE-TODAY** — the cheap-swarm archetype; "droid = crewless" is the `crew-automation` component.

### UNIT: B2 Super Battle Droid (heavy droid, chassis: human/droid)
**AUTHENTIC CAPABILITIES:** A bulky, heavily **armored** combat droid with an **integral wrist blaster** (no
separate weapon to drop); tougher and harder-hitting than a B1.
**BUILD:**
- Chassis `human-frame` {BaseHP high} + `ground-plating` (heavy dial) → the armored bulk.
- `energy-weapon` {Attack medium, Range 2, Mode 2=Energy} → the wrist blaster (integral = just a mounted weapon; the "can't be disarmed" nuance is cosmetic).
- `crew-automation` → crewless.
**LEDGER:**
- **EXISTS:** everything — armored frame + energy weapon + automation.
- **MISSING:** nothing.
**VERDICT:** **BUILDABLE-TODAY** — a tougher dial-up of the B1.

### UNIT: Droideka / Destroyer Droid (shielded assault droid, chassis: human/droid)
**AUTHENTIC CAPABILITIES:** A tri-legged droid that **rolls into position** as a wheel, unfolds, and lays down
**twin rapid-fire blasters** behind a **personal deflector shield** — a regenerating energy bubble that soaks fire
until it's overwhelmed. The single best test of the ground shield mechanic.
**BUILD:**
- Chassis `human-frame` (or a tuned droid frame) + `crew-automation` (crewless).
- `energy-weapon` {Attack medium, high rate, Range 2, Mode 2=Energy} → twin rapid blasters.
- `shield-generator` {Shield 150} (`installations.json:2845`) **or** `ward-projector` {Shield 60, ShieldRegenFraction 1.0} (`installations.json:2924`) → the deflector shield. This is *exactly* the game's ground shield: a **depleting pool** that soaks damage before HP, regenerates between salvos, and drops when overwhelmed (`GroundUnit.CurrentShield`). The droideka's "shield holds until saturated, then collapses" behavior is native.
- `ground-locomotion` {SpeedFactor high, Locomotion→wheel} → the rolling-ball fast approach (a fast wheeled drive).
**LEDGER:**
- **EXISTS:** the shield-as-depleting-pool is a real, tuned mechanic (capacity-vs-recharge is even a design decision — a small fast-recharge ward vs a big slow shield). The rolling mobility is a fast-locomotion dial. This unit is a *validation*, not a gap.
- **MISSING:** the "fold into a wheel = defenseless while rolling" config-toggle is design-hole **H5a** (config-states) — cosmetic here.
**VERDICT:** **BUILDABLE-TODAY** — validates the ground shield pool and fast locomotion; the marquee droid.

### UNIT: Geonosian Warrior (native infantry, chassis: human — with an air caveat)
**AUTHENTIC CAPABILITIES:** An insectoid native who **flies on wings** and carries a **sonic blaster** (a
concussive sound-pulse weapon that ignores some armor); fragile, fast, swarms.
**BUILD:**
- Chassis `human-frame` {BaseHP low} → the fragile insectoid body.
- `energy-weapon` re-natured to **Mode 3=Artillery or a concussive dial** {Attack medium, Range 2} → the sonic blaster. Sonic isn't one of the four damage natures (Kinetic/Energy/Explosive/Exotic); closest is **Explosive** (a concussive area pulse) — a dial choice, a small fidelity compromise.
- Wings → **Hover locomotion** as a compromise (fast, floats), pending the air layer.
**LEDGER:**
- **EXISTS:** the fragile frame + a mid weapon; Hover approximates the flight.
- **MISSING:** (1) **sonic** has no exact damage nature — map to Explosive/Exotic via a dial (minor); (2) **flight** hits the same missing air-combat layer as the LAAT (Hover is a surface stand-in, not true airborne).
**VERDICT:** **BUILDABLE-TODAY** (as a fast, fragile skirmisher with a concussive weapon) — flight and "sonic" are fidelity compromises, not blockers.

---

## SEPARATIST — Walkers & Vehicles

### UNIT: Hailfire Droid (missile tank, chassis: vehicle) — THE MISSILE PROBE
**AUTHENTIC CAPABILITIES:** An IG-227 droid that rides **two giant hoop-wheels** at high speed, carrying **two racks
of ~15 guided anti-armor missiles** each — a fast, fragile, standoff **missile artillery** platform.
**BUILD:**
- Chassis `vehicle-frame` {light dial} + `ground-locomotion` {SpeedFactor very high} → the fast hoop-wheel body.
- **The missiles — here's the gap.** Ground weapons have **no Guided mode** (`GroundWeaponAtb.Mode` is only Melee/Ballistic/Energy/Artillery). And the ship missile launcher (`missile-launcher`, `weapons.json:235`) is **`MountType: ShipComponent, ShipCargo`** — **no `GroundUnit` flag**. So today the honest build is `ground-cannon` {Mode 3=Artillery, Range 3 (max)} → indirect, undodgeable "missile-like" fire. That captures *standoff artillery* but not *guided missiles* (tracking, point-defense-intercept, a finite salvo that saturates).
**LEDGER:**
- **EXISTS:** a fast light vehicle + long-range indirect (Artillery-mode) fire — a recognizable standoff platform.
- **MISSING — design-hole (guided delivery on the ground):** a **guided ground weapon** (a 5th Mode, or the `GroundUnit` mount flag added to `missile-launcher` + the ground resolver reading guided/tracking/point-defense). The whole *character* of a Hailfire — a missile swarm that a point-defense screen can shoot down — is unmodeled on the ground.
**VERDICT:** **BUILDABLE-WITH-NEW-DATA** — a recognizable standoff missile-tank is an Artillery-mode compromise *today*; a *true* guided-missile ground weapon needs a new Mode/mount (data + a small resolver hook), not deep engine work.

### UNIT: Homing / Dwarf Spider Droid (beam walker, chassis: walker)
**AUTHENTIC CAPABILITIES:** A four-legged droid walker with a central **beam cannon** (the dwarf spider fires a
downward-sweeping energy beam); slow, methodical, decent armor.
**BUILD:**
- Chassis `walker-frame` (`installations.json:2696`) + `crew-automation` (crewless).
- `energy-weapon` {Attack medium-high, Range 2–3, Mode 2=Energy} → the central beam cannon.
- `ground-plating` (medium) → the droid's plating.
**LEDGER:**
- **EXISTS:** everything — walker + energy beam + armor + automation.
- **MISSING:** nothing.
**VERDICT:** **BUILDABLE-TODAY** — a clean walker-frame beam platform.

---

## SEPARATIST — Fighters & Ships

### UNIT: Geonosian (Nantex-class) Starfighter (fighter, chassis: ship/fighter)
**AUTHENTIC CAPABILITIES:** A needle-nosed insectoid starfighter — laser cannon + a tractor/beam weapon; extremely
agile; flown by a single Geonosian.
**BUILD:**
- `ship-hull` (fighter tier — small Mass Budget) (`installations.json:68`) → the tiny agile frame; the `Fighter` mount flag lets fighter-scale parts fit.
- `laser-weapon` {small} (`weapons.json:5`) → the laser cannon.
- `conventional-engine`/`scntr-engine` (`engines.json`) {`Fighter` mount} → the sublight drive.
**LEDGER:**
- **EXISTS:** the fighter is a small ship assembly — validated by the engines carrying a `Fighter` mount flag (a drive on a fighter hull just works).
- **MISSING:** the tractor/laser-whip is an Exotic-weapon nuance (a "grab" effect, H1-adjacent) — cosmetic here.
**VERDICT:** **BUILDABLE-TODAY** — a fighter-scale ship.

### UNIT: Vulture Droid Starfighter (transforming droid fighter, chassis: ship/fighter)
**AUTHENTIC CAPABILITIES:** A droid starfighter (no pilot) that **transforms** between a flight mode and a
walking/perched mode; blaster cannons + energy torpedoes; deployed in huge automated swarms.
**BUILD:**
- `ship-hull` (fighter) + `laser-weapon` + `missile-launcher` (energy torpedoes, `weapons.json:235`) + engine + `crew-automation` (crewless droid).
**LEDGER:**
- **EXISTS:** the fighter, guns, torpedoes, and crewlessness (the ship-side automation).
- **MISSING — design-hole H5a (config-states):** the **flight ↔ walk transformation** is a config toggle with no support (a cheap reuse of the doctrine/stance-switch mechanic one level down, per H5a). Cosmetic for a Geonosis dogfight.
**VERDICT:** **BUILDABLE-TODAY** — a crewless fighter; the transform is a flagged config-state (H5a), not a blocker.

### UNIT: Trade Federation Core Ship / Lucrehulk (capital control ship, chassis: ship/mega)
**AUTHENTIC CAPABILITIES:** A sphere-hulled droid **control ship** and troop/vehicle **carrier** — it disgorges the
droid army, and (in the Trade Federation era) its signal coordinates the droids; heavy hull, hangars full of
fighters and landing craft, hyperdrive.
**BUILD:**
- `ship-hull` (mega tier — huge Mass Budget) (`installations.json:68`) → the sphere.
- `laser-weapon` batteries + `point-defense-mount` (`weapons.json:815`) → its guns + PD.
- `troop-bay` ×many + `general-cargo-hold` (`storage.json`) → the droid/vehicle holds and hangars.
- `reactor` ×many + `alcubierre-warp-drive` → power + hyperdrive.
**LEDGER:**
- **EXISTS:** the capital hull, guns, holds, power, and FTL — all stock ship assembly at the mega scale.
- **MISSING:** (1) **H6 carrier/nesting** — launching fighter/lander child units (the verify item); (2) the "**central control signal** — kill the ship and the droids drop" is a **hive-command** relationship (design-hole **H10b**, distributed command) — a genuinely cool mechanic (decapitation strike collapses the army) that isn't wired for ground droids yet.
**VERDICT:** **BUILDABLE-TODAY** (as a capital carrier) — the hive-control "shoot the ship, stop the droids" wrinkle is an H10b frontier, not a build blocker.

---

## 4. The gap ledger (everything this battle surfaced)

| # | Gap (what broke) | Which units hit it | Kind | Proposed home / fix | Hole # |
|---|------------------|--------------------|------|---------------------|--------|
| G1 | **No atmospheric AIR-COMBAT layer** — a flyer that operates *above* the ground battle (strafe + air-mobile troop insertion) has no home; it's neither the 2D surface nor the orbital layer. | LAAT/i gunship (load-bearing); Geonosian warrior wings, Vulture/Nantex over the field | **NEEDS-ENGINE-WORK** | A new altitude/air band coupled to the ground hex layer (units with an "airborne" flag fire into ground hexes, are hit by AA, land to disembark). Not one of the catalogued 12 holes — a **new** battle-surfaced gap. | — (new) |
| G2 | **Lightsaber bolt-deflection** — a weapon that is also a defense (reflects aimed energy fire). | Jedi | **NEEDS-ENGINE-WORK** | A "deflect" effect: aimed-Energy fire at the unit has a chance to be nullified/reflected. A new resolver rule + a hybrid-component permission. | **H7** |
| G3 | **The Force is not gear** — precognition / telekinesis / mind-trick must be a People-system trait, never a component. | Jedi | **NEEDS-ENGINE-WORK (People layer)** | A Jedi **trait** on the People/commander layer that emits effects (dodge buff, push, subvert) onto its unit — the shared effect-bus. Do **not** build a "Force component." | **H4** |
| G4 | **No guided-missile ground weapon** — `GroundWeaponAtb.Mode` has no Guided; `missile-launcher` has no `GroundUnit` mount. | Hailfire droid; any missile infantry | **BUILDABLE-WITH-NEW-DATA** | Add a Guided Mode (tracking + point-defense-intercept + finite salvo) **or** add the `GroundUnit` flag to `missile-launcher` and let the ground resolver read guided. Today's compromise: Artillery mode (indirect, undodgeable). | — (delivery-axis) |
| G5 | **No ground troop-ferry** — a *ground vehicle* carrying infantry; the troop bay (`GroundBayAtb`, `storage.json:301`) is `ShipComponent`-only. | AT-TE belly bay; LAAT (with G1) | **NEEDS-CHANGE (mount flag)** | Add `GroundUnit` to a bay component so a walker/vehicle holds child units (units-as-entities already supports a unit owning children). | **H6** |
| G6 | **No ground command component** — a commander who *buffs* his formation; `ship-command`/`AdminSpaceAtb` (`storage.json:246`) is `ShipComponent`-only, so the leader gives no combat bonus. | Clone commander | **NEEDS-CHANGE (mount flag + resolver read)** | A `GroundUnitCommandAtb` (or the `GroundUnit` flag on a command part) the resolver reads as a formation-wide Attack/morale multiplier. | **H10b** |
| G7 | **No "sonic" damage nature** — sound/concussion isn't one of the four natures. | Geonosian sonic blaster | cosmetic (dial) | Map to Explosive (concussive) or Exotic via a dial; add a nature only if a franchise leans on it. | — |
| G8 | **Config-state toggles** — fold-into-wheel (droideka), flight↔walk (Vulture). | Droideka, Vulture droid | cosmetic (H5a) | Reuse the doctrine/stance-switch mechanic one level down (a named config that swaps stats/mounts). | **H5a** |
| G9 | **Carrier launch of vehicle/aircraft child units** — troop bay holds troops, but *launching* AT-TEs/LAATs/fighters is the verify item. | Acclamator, Core Ship, AT-TE | verify (H6) | Confirm a unit's bay can hold + launch/recover *child units* (the entity model should support it). | **H6** |
| G10 | **Hive / central-control decapitation** — kill the control ship, the droids drop. | Core Ship ⟷ all droids | frontier (H10b) | Distributed/hive command: a control node whose loss disrupts the units it commands. | **H10b** |

**The shape of the gaps:** two are genuine engine work (**G1 air layer** — the biggest, because Geonosis *is* an
air-and-armor battle; **G2/G3 the Jedi** — H7 deflection + H4 the Force-as-a-People-trait). Three are cheap
**mount-flag / small-data** fixes (**G4 guided ground missiles, G5 ground troop bay, G6 ground command**) that each
unlock a whole class of unit. The rest are cosmetic dials or already-catalogued config/carrier/hive frontiers.

---

## 5. Verdict — how completely can the game stage Geonosis today?

**Headline: roughly 85% of the order of battle is buildable-today from stock templates, and the 15% that breaks
breaks in exactly the two places you'd predict — the airspace and the Jedi.** The ground war's *hardware* is
almost entirely there: clone infantry, ARC troopers, B1/B2/droideka droids, AT-TE and spider-droid walkers, the
SPHA-T artillery walker (a marquee win — the supply gate forces its giant beam to carry a giant reactor), and the
TX-130 tank all fall straight out of the chassis + component parts bin. The droideka in particular *validates* the
ground shield-as-depleting-pool mechanic, and "droid = crewless" is a real component (`crew-automation`). The whole
fleet — Acclamators and Core Ships — is home-turf ship assembly. Every one of these is reachable cradle-to-grave:
mine steel and aluminium, refine them, build the part at a colony, design it in the Component Designer,
research-gate it, assemble it on a frame in the Entity Assembler, and lose a component when it's shot off.

**What's load-bearing vs. cosmetic:**
- **Load-bearing (must build to stage this battle recognizably):**
  1. **The air-combat layer (G1)** — without it, the LAAT gunships (and to a lesser degree the fighters and winged
     Geonosians over the field) simply aren't in the *ground* fight. At Geonosis, close air support is not a garnish;
     it's how the Republic wins the opening. This is the single biggest gap and it is real engine work.
  2. **Guided ground missiles (G4)** and **the ground troop bay (G5)** — cheap mount-flag/small-data fixes, but the
     Hailfire droid and "walkers that carry troops" are core to the battle's texture. Fix these two and the roster
     jumps from ~85% to ~95%.
- **Cosmetic (skip for a playable first pass):** the sonic damage nature (G7), the droideka fold and Vulture
  transform (G8, config-states), and the carrier-launch/hive-control richness (G9/G10) — all improve fidelity, none
  block a recognizable fight.
- **The honest asterisk — the Jedi (G2/G3):** you can field a lightsaber-wielding, high-dodge hero *today*, but the
  bolt-deflection (H7) and, above all, **the Force (H4)** are correctly *outside* the Component Designer. The Force
  is a **People-system trait**, and the litmus test's real value here is confirming that boundary: the game should
  build a Jedi's *gear* and let the People layer carry the *being*. Don't fake a "Force component."

**Shortest path to "playable Geonosis":** (1) add the three cheap fixes — `GroundUnit` mount flags on a bay
component (G5) and a command component (G6), and a guided ground-weapon Mode or `missile-launcher` ground mount
(G4); then (2) tackle the one big system — a minimal **air-combat band** coupled to the ground map (G1) so gunships
can strafe and insert. That sequence turns a ~85%-buildable battle into a fully recognizable one, and it defers the
Jedi's Force to the People system where it belongs — exactly where the litmus test says it should live.
