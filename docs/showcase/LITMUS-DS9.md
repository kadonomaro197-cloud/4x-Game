> ⚠ **SURVEY DRAFT (adversarially verified 2026-07-21).** This per-battle spec was attacked by a verifier on
> authenticity + engine-reality + completeness; it carries known load-bearing errata (e.g. the ground-vs-space
> weapon-composition gap, and battle-specific canon fixes) that are **reconciled in `FRANCHISE-LITMUS-TEST.md`**
> and corrected at build time. Treat the master doc as the authority where they differ.

# Litmus Test — The Battle for Deep Space 9 (Star Trek: the Dominion War)

**"Operation Return" / "Sacrifice of Angels."** A franchise litmus test in the sense the developer asked for: take an iconic battle and prove that **every ship and the station in it can be built through the game's real cradle-to-grave chain** — mined mineral → refined material → part built at a colony → component designed in the Component Designer → gated by research → bolted onto a chassis in the Entity Assembler → the ship fights → a component is shot off and lost. Not lore. A build spec, mapped onto the templates the game **actually** ships today, with an honest ledger everywhere it doesn't fit.

**As of:** 2026-07-21 · verified against the live base-mod JSON templates and the combat engine, not the design docs. Every template id below was grepped and read; file:line citations are given so the developer can open the exact part.

> **Read this first — the one-sentence verdict.** Star Trek's *ships* fall out of the existing template library almost perfectly: the game already has an anti-shield exotic beam (the Dominion polaron / Klingon disruptor), a regenerating shield pool, a cloak, ablative armour, guided torpedoes, and a nimble-heavy-ship dodge drive. **Thirteen of the fourteen ships are buildable today** with stock parts and, at most, a one-word mount-flag edit. The **one thing that genuinely breaks is Deep Space 9 itself** — a *weaponised station*. The game can build the station and can *destroy* it, but a station cannot yet *fight back* inside a fleet battle. The whole point of this engagement — a fortress that must hold the line — is the one rung the engine is missing.

---

## 1. The battle in one paragraph

The Dominion (the Gamma-Quadrant empire) and its Cardassian allies have seized the space station **Deep Space 9**, which sits at the mouth of the **Bajoran wormhole** — the only quick door between the two halves of the galaxy. Starfleet mines the wormhole shut so no Dominion reinforcements can come through, and the Dominion masses a huge fleet to take the minefield down before the mines finish arming. Captain Sisko leads a Federation-Klingon armada — outnumbered better than two to one — to punch through that fleet, retake the station, and hold the door. It is the largest fleet action in *Deep Space Nine*: **capital ships trading phaser and disruptor fire, torpedo salvos, shields buckling, cloaked Klingon raiders, and clouds of small Dominion attack ships swarming and ramming**, all fought around a **weaponised station** that is itself a combatant. It is the perfect stress test because it exercises the full menu: capital ships, a fortress-station, shields versus shield-piercing weapons, cloak, massed cheap swarm craft, and guided ordnance.

---

## 2. Force composition (the real order of battle)

Numbers are the canonical on-screen scale (rounded; Trek fleets were shown in the hundreds-to-thousands).

**Federation–Klingon Alliance (attacker, ~600 ships)** — Sisko's task force, built to *break through*, not to win a slugging match:
- **Cruiser / battleship line:** *Galaxy*-class, *Nebula*-class, *Excelsior*-class, *Ambassador*-class (older heavy cruisers), *Akira*-class (torpedo-heavy).
- **Light line / cannon fodder:** *Miranda*-class, *Saber*-class, *Steamrunner*-class escorts.
- **The tip of the spear:** USS *Defiant* — one small, absurdly over-gunned warship, Sisko's flagship.
- **Klingon squadron (Martok's fleet, arrives as the cavalry):** *B'rel*/*K'vort* Birds-of-Prey (raiders), *Vor'cha*-class attack cruisers, and the *Negh'Var*-class — Martok's battleship flagship.

**Dominion–Cardassian force (defender, ~1,254 ships)** — holding the station and the minefield:
- **The swarm:** *Jem'Hadar* attack ships (small, cheap, thrown by the hundreds) and *Jem'Hadar* battlecruisers (the heavy version).
- **Cardassian line:** *Galor*-class and the upgraded *Keldon*-class cruisers.
- **The fortress:** **Deep Space 9** — a Cardassian-built station the Federation retrofitted with phaser banks, photon-torpedo launchers, and deflector shields. It anchors the whole defence and covers the minefield.

**The battlefield:** open space around the station, plus the **Bajoran wormhole** — a naturally stable wormhole out of which reinforcements pour. In engine terms the wormhole is a **jump point** (see the gap ledger, hole H8).

---

## 3. The units — per-unit build spec

**How to read a block.** *AUTHENTIC CAPABILITIES* is the canon, terse. *BUILD* is the real chassis + components, each named by its **template id** (the part) with the **dial values** you'd set in the designer. *LEDGER* is the honest EXISTS / NEEDS-CHANGE / MISSING accounting. *VERDICT* is one of **BUILDABLE-TODAY** (stock templates, maybe a one-word mount-flag add), **BUILDABLE-WITH-NEW-DATA** (needs a new JSON template/ordnance but no engine code), or **NEEDS-ENGINE-WORK** (needs a new mechanic).

**The parts shelf they all draw from** (grepped and verified — this is the real inventory):

| Trek thing | Game part (template id) | Where | What it does |
|---|---|---|---|
| Ship frame | `ship-hull` (light/medium/heavy tiers) | `installations.json:68` | Hull mass + a mass budget the mounted parts must fit inside — the supply/budget gate |
| Phaser (energy beam) | `laser-weapon` (EMaser) / `pulse-laser` | `weapons.json:5` / `:862` | Continuous or pulsed energy beam; `pulse-laser` runs hot and needs radiators |
| Disruptor / polaron beam (**shield-piercing**) | `disruptor-weapon` (Ion Disruptor) | `weapons.json:942` | **Exotic** nature, light-speed → **bypasses the shield pool and hits the hull** |
| Photon / quantum torpedo | `missile-launcher` + a missile ordnance design | `weapons.json:235`; `ordnance.json` | Guided, explosive warhead; the torpedo is itself a mini-assembly |
| Deflector shield | `deflector-array` (ShieldAtb) | `weapons.json:600` | A regenerating joule pool; soaks kinetic 100%, energy 50%, **exotic 0%** |
| Ablative armour | `armour-hardening` + hull armour `ablative-composite-armor` | `weapons.json:653`; `armor.json:89` | Nature-tuned plating that soaks a fraction of a chosen damage type |
| Cloak | `cloak-device` (CloakAtb) | `electronics.json:375` | Damps emitted signature → detected only at short range |
| Warp drive | `alcubierre-warp-drive` | `engines.json:174` | FTL between systems |
| Impulse engine | `conventional-engine` / `scntr-engine` | `engines.json:3` / `:89` | Newtonian sublight thrust; also drives evasion |
| Nimble-heavy dodge | `inertialess-drive` | `engines.json:256` | Gives a heavy hull a fighter's evasion floor |
| Reactor | `reactor` / `steam-turbine-reactor` | `energy.json:5` / `:256` | Power for beams, shields, drives |
| Fire control / turret | `beam-fire-control` | `electronics.json:182` | Raises beam tracking (hit chance vs a dodging target) |
| Point defense | `point-defense-mount` / `pd-director` | `weapons.json:815`; `electronics.json:426` | Shoots down incoming torpedoes |
| Bridge / command | `ship-command` (Ship Bridge) | `storage.json:245` | Command spaces + admin level |
| Veteran crew | `unit-caliber` (Veteran Cadre) | `electronics.json:683` | Multiplies firepower + toughness (elite crew) |
| Station frame | `station-chassis` | `installations.json:1527` | The structural budget a station's modules bolt onto |

Every one of these already has the **full six-point registration** (template + design + starting-item unlock, both ends) that keeps New Game from crashing — they're on the real warships in `shipDesigns.json`, so they build in an actual game today. The existing test warships are, in effect, pre-made Trek archetypes: the **Bastion Shielded Cruiser** = shielded energy warship, the **Ravager Ion Frigate** = shield-piercing disruptor boat, the **Wraith Stealth Cruiser** = cloaked raider, the **Ember Pulse Cruiser** = hot-beam ship, the **Phantom Inertialess Cruiser** = the nimble heavy hull, the **Javelin Missile Cruiser** = torpedo boat, the **Praetorian Veteran Cruiser** = elite crew. Building a Trek fleet is mostly *renaming and re-proportioning these.*

---

### SIDE A — FEDERATION / STARFLEET (chassis: ship)

Every Starfleet ship shares one recipe — **hull + phasers (energy beam) + photon torpedoes (guided) + deflector shields + ablative armour + warp + impulse + reactor + fire control** — and differs only in **hull tier, weapon count, and one or two specials.** So I give the recipe once in full on the Galaxy, then only the *deltas* on the rest.

---

**UNIT: *Galaxy*-class explorer/battleship** (capital cruiser, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** The big one — a 42-deck explorer that doubles as a battleship. 12 Type-X phaser arrays (continuous, wide-arc energy beams that sweep across an arc), photon torpedo launchers fore and aft, high-capacity regenerative deflector shields, a science suite and civilian decks, warp + impulse. Signature trick: **saucer separation** (splits into two independent ships).
- **BUILD (cradle-to-grave, exact):**
  - Chassis: `ship-hull` **heavy tier** {Hull Mass 25000, Mass Budget 180000} → the big frame (`default-design-ship-hull-heavy`).
  - Hull armour: `ablative-composite-armor` {Thickness ~6} → the ablative skin (chosen on the design's `Armor` field, as every ship in `shipDesigns.json` does).
  - Phasers: `pulse-laser` ×~8 {Range 5000, Pulse Energy 500000, Charge Period 1, Combat Heat 300000} → the energy-beam main battery (undodgeable, half-soaked by shields). *(Or `laser-weapon` for a truly continuous beam — see the phaser note in the ledger.)*
  - Cooling: `heat-radiator` ×2 {Heat Capacity 500000} → sheds the pulse-laser waste heat so the phasers don't throttle mid-fight.
  - Torpedoes: `missile-launcher` ×2 {Max Calibre 250, Max Mass 500} firing a photon-torpedo ordnance design (guided, explosive).
  - Shields: `deflector-array` ×2 {Shield Capacity 5000000, Recharge Rate 100000} → the regenerating deflector pool.
  - Fire control: `beam-fire-control` {Range 100, Tracking Speed 5000} → phaser accuracy.
  - Command: `ship-command` {Admin Level 2, Console Space 10} → the bridge.
  - Sensors: `passive-sensor` (Deep-Space array dials) → the science suite (detection half).
  - Power: `reactor` or `steam-turbine-reactor` ×2 → the warp-core stand-in.
  - Propulsion: `alcubierre-warp-drive` ×4 (warp) + `scntr-engine` ×3 (impulse) + `battery-bank` + `fuel tanks`.
- **LEDGER:**
  - EXISTS: every part above is a stock template already on a base-mod ship — nothing invented. The heavy hull's **mass budget is the gate** that makes this ship *possible* but *not free*: fill it with guns and you can't also carry the science suite, which is the correct Galaxy-class trade.
  - NEEDS-CHANGE: none for the ship to fight.
  - MISSING / deferred: **saucer separation** = design-hole **H5** (one chassis → two independent units). Purely cosmetic for this battle — skip it. **Wide-arc "one phaser sweeps several targets"** is not modeled; the resolver aggregates fire (see phaser note). Cosmetic.
- **VERDICT: BUILDABLE-TODAY.** Stock heavy-hull warship. The developer could copy the **Bastion Shielded Cruiser** design, add torpedo tubes + radiators, rename it *Galaxy*, and it fights.

---

**UNIT: *Nebula*-class cruiser** (capital cruiser, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** A Galaxy-class saucer on a smaller drive section with a big modular "sensor pod" or "torpedo pod" on the spine. Same phasers/torpedoes/shields, a touch lighter.
- **BUILD:** Same recipe as the Galaxy, **heavy hull**, one fewer phaser, and swap emphasis via the swappable pod: either **+1 `passive-sensor`** (Deep-Space Listening Array dials → the sensor pod) *or* **+2 `missile-launcher`** (the torpedo pod). That "pick one pod" is exactly a designer choice within the mass budget.
- **LEDGER:** EXISTS: all parts. The modular pod is *emulated* by choosing which components you mount — the game has no hot-swappable pod slot (that would be H5 config-state), but the two Nebula variants are simply two saved designs.
- **VERDICT: BUILDABLE-TODAY.**

---

**UNIT: *Excelsior*-class cruiser** (older workhorse cruiser, chassis: ship / medium hull)

- **AUTHENTIC CAPABILITIES:** The reliable century-old cruiser — phasers, photon torpedoes, shields; solid but outclassed by a Galaxy. The backbone "line ship" of the fleet.
- **BUILD:** `ship-hull` **medium** {10000/90000}; `laser-weapon` ×4 (phasers); `missile-launcher` ×1 (torpedoes); `deflector-array` ×1; `beam-fire-control` ×1; `ship-command`; reactor + warp + impulse. This is almost exactly the **Aegis Test Warship** (`default-ship-design-test-warship`) with a shield and a torpedo tube added.
- **LEDGER:** EXISTS: all parts. VERDICT: **BUILDABLE-TODAY.**

---

**UNIT: *Miranda*-class light cruiser** (old light cruiser / cannon fodder, chassis: ship / medium hull)

- **AUTHENTIC CAPABILITIES:** A century-old small cruiser (the *Reliant*), often with a distinctive "rollbar" torpedo/sensor pod. Light phasers, a torpedo rack, modest shields. The ship that dies by the dozen to buy the capitals time.
- **BUILD:** `ship-hull` **medium** or **light**; `laser-weapon` ×2 (phasers); `missile-launcher` ×1 on the rollbar (torpedoes); `deflector-array` ×1 (thin); `reactor` ×1; warp + impulse. Cheap and fragile — few parts, small budget.
- **LEDGER:** EXISTS: all parts. VERDICT: **BUILDABLE-TODAY.** This is the archetype the auto-resolver's "combatants die before utility hulls, weaker fleet still grinds kills" math is built for.

---

**UNIT: *Akira*-class heavy escort** (torpedo-heavy cruiser, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** A muscular escort **defined by its ordnance** — as many as 15 torpedo launchers in a "torpedo pod" across the top, plus phasers. The photon-torpedo hammer of the fleet.
- **BUILD:** `ship-hull` **heavy**; `missile-launcher` ×4 (the torpedo pod — this is the **Javelin Missile Cruiser** archetype, `default-ship-design-test-missile`); `laser-weapon` ×2 (phasers); `deflector-array` ×1; `reactor` ×2; warp + impulse + battery. To sustain the torpedo volume, add an ordnance hold (`ordnance-cargo-hold`) so it doesn't run its magazine dry.
- **LEDGER:** EXISTS: all parts. The Akira's whole identity — massed guided ordnance — is exactly what the missile system + point-defense counter-play were built to express. NEEDS-CHANGE: none. MISSING: a distinct **photon-torpedo ordnance design** for flavour (see the torpedo note) — new *data*, not required to fight.
- **VERDICT: BUILDABLE-TODAY.**

---

**UNIT: USS *Defiant*** (over-gunned glass cannon, chassis: ship / **light** hull — Sisko's flagship)

- **AUTHENTIC CAPABILITIES:** A tiny warship with a capital ship's teeth, built specifically to fight the Borg/Dominion. **Pulse phaser cannons** (rapid, hard-hitting), **quantum torpedoes** (a higher-yield torpedo than the photon), **ablative armour** (soaks damage without shields), and — uniquely — a **Romulan cloaking device** (on loan). Famously "over-designed" and structurally overstressed for its size.
- **BUILD:**
  - Chassis: `ship-hull` **light** {500/25000} — deliberately small. **This is the validation case.**
  - Pulse phasers: `pulse-laser` ×3 {high Pulse Energy}.
  - Quantum torpedoes: `missile-launcher` ×2 firing a **high-yield** missile-payload ordnance.
  - Ablative armour: `armour-hardening` {Soak vs Energy 0.4, Soak vs Kinetic 0.2} **and** hull `ablative-composite-armor` {Thickness ~4}.
  - Cloak: `cloak-device` {Signature Multiplier 0.2}.
  - Nimble: `scntr-engine` for high thrust (small + high thrust = high evasion), optionally `inertialess-drive` for the "impossibly agile for its size" feel.
  - Power: `reactor` ×1 (crammed).
- **LEDGER:**
  - EXISTS: every part.
  - **This unit's job is to make the supply/budget gate BITE — and it does.** A light hull is a 25000 kg budget. Three pulse-lasers + two torpedo launchers + ablative plating + a cloak + a reactor + engines will *blow past* that budget, and the assembler will **correctly refuse the design** until you drop something or step up a hull tier — which is exactly the Defiant's canonical problem (over-gunned, overstressed). The litmus test *passes by failing:* the game tells you the same thing Starfleet engineers said.
  - MISSING / hole: the **cloak works as stealth** (short detection range) but there is **no "you can't fire or raise shields while cloaked" tradeoff** — the canonical cloak gambit. That's design-hole **H5** (a chassis config-state toggle). Cosmetic-to-medium: the ship still fights; it just doesn't pay the cloak's price.
- **VERDICT: BUILDABLE-TODAY** — and the single best validation of the budget gate in this whole battle.

---

**UNIT: *Saber*/*Steamrunner*-class escort** (light escort, chassis: ship / light hull)

- **AUTHENTIC CAPABILITIES:** Small, fast Starfleet escorts — a couple of phasers, a torpedo launcher, light shields. Screen and flank ships.
- **BUILD:** `ship-hull` **light**; `laser-weapon` ×2; `missile-launcher` ×1; `deflector-array` ×1 (thin); `scntr-engine` for speed; `reactor` ×1. This is the **Picket Test Corvette** / **Wasp Strike Fighter** class of hull.
- **LEDGER:** EXISTS: all parts. VERDICT: **BUILDABLE-TODAY.**

---

### SIDE B — KLINGON DEFENSE FORCE (chassis: ship) — Martok's fleet

Klingon doctrine = **cloak + disruptors + a decloak alpha strike.** Every Klingon ship is a cloaked raider that decloaks, fires, and re-cloaks.

---

**UNIT: *B'rel*/*K'vort* Bird-of-Prey** (cloaked raider, chassis: ship / **light** hull)

- **AUTHENTIC CAPABILITIES:** The iconic green raider. **Cloaking device**, wing-mounted **disruptor cannons**, one or two photon torpedo tubes, light shields, agile. Decloaks, strikes, vanishes. Cannot fire while cloaked (canon).
- **BUILD:**
  - Chassis: `ship-hull` **light** {500/25000}.
  - Cloak: `cloak-device` {Signature Multiplier 0.15–0.2} → the defining part. This is the **Wraith Stealth Cruiser** archetype (`default-ship-design-test-wraith`), on a light hull.
  - Disruptors: `disruptor-weapon` ×2 {Energy Per Shot 150000, Rounds Per Second 2}. **Exotic nature = bypasses enemy shields** — perfectly on-canon for a disruptor.
  - Torpedoes: `missile-launcher` ×1.
  - Shields: `deflector-array` ×1 (light).
  - Propulsion: `scntr-engine` (agile) + `alcubierre-warp-drive` + `reactor`.
- **LEDGER:**
  - EXISTS: cloak, disruptor (exotic/shield-piercing), torpedo, shield, drive — all stock.
  - MISSING / hole **H5**: the **decloak-to-fire lockout** (a Bird-of-Prey must drop cloak to shoot, exposing itself for one volley) is not modeled — the cloak is a passive signature reducer. This is the *heart of Klingon tactics*, so for a faithful Klingon feel this is more load-bearing than it is for the Defiant. Still: the ship fights today; it just doesn't pay the decloak price.
- **VERDICT: BUILDABLE-TODAY** (cloak + shield-piercing disruptors work); the decloak-to-fire *tactic* is NEEDS-ENGINE (H5).

---

**UNIT: *Vor'cha*-class attack cruiser** (heavy cruiser, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** The Klingon workhorse capital — a forward **disruptor cannon** (heavy), wing disruptors, photon torpedoes, cloak, strong shields. The bulk of Martok's line.
- **BUILD:** `ship-hull` **heavy**; `disruptor-weapon` ×3 (one heavy: {Energy Per Shot 300000} — this is the **Kithrin "Phase Disruptor"** design, `kithrin-phase-disruptor`, already in the base mod); `missile-launcher` ×1 (torpedoes); `deflector-array` ×2; `cloak-device` ×1; `reactor` ×2; warp + impulse. Essentially the **Kithrin Hive Cruiser** (`default-ship-design-hive-cruiser` — heavy hull, 3 disruptors, 2 deflectors) with a cloak added.
- **LEDGER:** EXISTS: all parts (this is nearly a pre-built base-mod ship). NEEDS-CHANGE: none. Hole **H5** decloak-to-fire as above.
- **VERDICT: BUILDABLE-TODAY.**

---

**UNIT: *Negh'Var*-class battleship** (super-heavy flagship, chassis: ship / heavy hull — Martok's flagship)

- **AUTHENTIC CAPABILITIES:** The largest Klingon warship — a battleship bristling with disruptor arrays and torpedo launchers, heavy shields, cloak, and (in-fiction) an elite hand-picked crew. The command ship.
- **BUILD:** `ship-hull` **heavy** (max out the Mass Budget toward the 180000 tier or a custom bigger hull — the `ship-hull` template's Mass Budget dial goes to 100,000,000, so a genuinely huge flagship hull is a legal design); `disruptor-weapon` ×4 (heavy); `missile-launcher` ×2; `deflector-array` ×3; `cloak-device` ×1; **`unit-caliber` {Firepower 1.4, Toughness 1.3}** → the elite crew (the Veteran Cadre — this is the **Praetorian Veteran Cruiser** trick, and it draws from the scarce officer talent pool, so you can't spam flagships); `reactor` ×3; warp + impulse.
- **LEDGER:** EXISTS: all parts, including the elite-crew multiplier and a hull big enough. NEEDS-CHANGE: none. Hole **H5** decloak-to-fire.
- **VERDICT: BUILDABLE-TODAY** — and the `unit-caliber` part means the flagship genuinely out-fights an identical hull, which is the point of a flagship.

---

### SIDE C — DOMINION / CARDASSIAN (chassis: ship + one station) — the defenders

Dominion doctrine = **shield-piercing polaron beams + overwhelming numbers of cheap attack ships + suicide ramming.** This is the side the shield/exotic-weapon system was practically designed to model.

---

**UNIT: *Jem'Hadar* attack ship** (light attack craft / swarm, chassis: ship / **light** hull)

- **AUTHENTIC CAPABILITIES:** A small, tough, mass-produced attack craft. **Phased polaron beams** (the Dominion's signature — they *pass through Federation shields* and hit the hull directly), disruptor-style secondary fire, and a reinforced hull that makes it a viable **kamikaze**: a Jem'Hadar ship will deliberately **ram** an enemy in a suicide run. Thrown into battle by the hundreds.
- **BUILD:**
  - Chassis: `ship-hull` **light** {500/25000} — small and cheap so you can build a thousand.
  - **Polaron beam:** `disruptor-weapon` {Energy Per Shot 150000, Rounds Per Second 2}. **This is the whole reason the disruptor template exists** — `WeaponNature.Exotic`, light-speed, and the shield's exotic-soak is **0.0**, so it **bypasses the deflector pool and lands on the hull** (`weapons.json:942`; the resolve wiring is in `Combat/CLAUDE.md` Phase B/D). A Federation ship's shields do *nothing* against it — exactly the on-screen terror of the polaron beam. This is the **Ravager Ion Frigate** archetype (`default-ship-design-test-disruptor`) on a light hull.
  - Toughness: hull `armour` (a light plate) — the Jem'Hadar ship is famously durable for its size.
  - Propulsion: `scntr-engine` (fast) + `reactor` ×1. No cloak, minimal frills — it's a cheap hull.
- **LEDGER:**
  - EXISTS: the shield-piercing polaron beam maps **perfectly** to `disruptor-weapon` — this is the cleanest single mapping in the whole battle. The swarm angle works because these are cheap light hulls; the auto-resolver already handles many-vs-many and divides fire.
  - MISSING / hole: the **suicide ram** has no mechanic. There is no ram order and no collision-damage resolver (kinetic energy of one hull hitting another). The nearest honest compromise is to treat a "kamikaze" as a fast light ship closing to knife range and dying — but the ram *itself* (delivering a big lump of collision damage on contact) is **NEEDS-ENGINE** (a Ram order + a mass×velocity collision resolver; H5-adjacent).
- **VERDICT: BUILDABLE-TODAY** as a shield-piercing swarm craft; the **suicide-ram** capability is NEEDS-ENGINE.

---

**UNIT: *Jem'Hadar* battlecruiser** (heavy warship, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** The scaled-up Jem'Hadar warship — heavier polaron banks, torpedoes, tougher. The Dominion's line battleship.
- **BUILD:** `ship-hull` **heavy**; `disruptor-weapon` ×3–4 (heavier polaron, {Energy Per Shot 300000}); `missile-launcher` ×1; optionally a `deflector-array` (the Dominion did use shields too); `reactor` ×2; warp + impulse. Structurally the **Kithrin Harbinger** (`kithrin-ship-harbinger` — heavy hull, 3 phase-disruptors, 2 plasma casters, 2 shields) is a ready-made stand-in.
- **LEDGER:** EXISTS: all parts. VERDICT: **BUILDABLE-TODAY.**

---

**UNIT: Cardassian *Galor*-class cruiser** (workhorse cruiser, chassis: ship / medium hull)

- **AUTHENTIC CAPABILITIES:** The standard Cardassian warship — **spiral-wave disruptors** (their main gun), photon-style torpedoes, deflector shields. Fights in the Dominion line en masse.
- **BUILD:** `ship-hull` **medium**; `disruptor-weapon` ×2 (spiral-wave disruptors — {Energy Per Shot ~150000}); `missile-launcher` ×1 (torpedoes); `deflector-array` ×1; `beam-fire-control` ×1; `reactor` ×1; warp + impulse.
- **LEDGER:** EXISTS: all parts. The spiral-wave disruptor is again the `disruptor-weapon` (exotic beam) — Cardassian and Dominion weapons share the shield-piercing template, which is period-correct. VERDICT: **BUILDABLE-TODAY.**

---

**UNIT: Cardassian *Keldon*-class cruiser** (upgraded Galor, chassis: ship / heavy hull)

- **AUTHENTIC CAPABILITIES:** A larger, up-gunned Galor with more disruptors, more torpedoes, and (in Dominion service) sometimes extra shielding. The Cardassian heavy.
- **BUILD:** As the Galor but on a **heavy hull** with `disruptor-weapon` ×3, `missile-launcher` ×2, `deflector-array` ×2. A saved "up-tier" of the Galor design.
- **LEDGER:** EXISTS: all parts. VERDICT: **BUILDABLE-TODAY.**

---

### THE FORTRESS — DEEP SPACE 9 (chassis: **station**) — the one real break

**UNIT: Deep Space 9** (weaponised space station, chassis: `station-chassis`)

- **AUTHENTIC CAPABILITIES:** A Cardassian-built ore-processing station (Nor-class) the Federation retrofitted into a fortress. **Phaser banks and photon-torpedo launchers** hidden in the docking pylons and habitat ring, **deflector shields** strong enough to shrug off a fleet's fire for a while, industrial/habitat/docking infrastructure, and it **anchors the defence of the wormhole and the minefield.** In the episode it is a full combatant — it *shoots down attackers and soaks a pounding.*
- **BUILD (as far as the game goes today):**
  - Chassis: `station-chassis` {Structural Budget 2000, up to 10000} → the frame (`default-design-station-chassis`, `installations.json:1527`). **This part is fully six-point-registered and buildable in a real game today** (`Stations/CLAUDE.md`).
  - Power: `reactor` — **already carries the `Station` mount flag** (`energy.json:24`), so a station can be powered. ✅
  - Infrastructure it *can* mount today (all gained the `Station` flag): `research-lab`, `factory`, `refinery`, `mine`, plus `space-habitat` for the crew. So the *civilian* DS9 — a mining/industry/habitat station — **builds today.**
  - Weapons it *should* mount: `pulse-laser`/`laser-weapon` (phasers), `missile-launcher` (photon torpedoes), `deflector-array` (shields).
- **LEDGER — this is where the battle breaks:**
  - EXISTS: the station chassis, its power, and its infrastructure modules are real and buildable. A station can even be **destroyed** — the direct weapon-hit path is wired: a ship firing on a station runs `DamageProcessor.OnTakingDamage → OnStationDamage`, which drains `StationInfoDB.StructuralIntegrity` and calls `StationFactory.DestroyStation` at zero (`Stations/CLAUDE.md`, Slice B). So DS9 can be *blown up.*
  - **NEEDS-CHANGE (cheap):** the weapons and the shield do **not** carry the `Station` mount flag. `laser-weapon`, `pulse-laser`, `disruptor-weapon`, `railgun-weapon`, `flak-weapon`, `plasma-repeater` are flagged `ShipComponent, ShipCargo, PlanetInstallation, GroundUnit` — **no `Station`** (`weapons.json:29,367,533,880,963,1019`). `deflector-array` is `ShipComponent, ShipCargo, PlanetInstallation` — **no `Station`** (`weapons.json:618`). `missile-launcher` is only `ShipComponent, ShipCargo` (`weapons.json:257`). Adding `Station` to these `MountType` flags is **purely additive JSON** — exactly the one-word edit that already gave `reactor`/`factory`/`refinery`/`mine` their `Station` flag. After that edit, DS9 can *carry* phasers, torpedoes, and shields.
  - **NEEDS-ENGINE (load-bearing — the real break):** even mounting the guns, **a station cannot fight in a battle.** Two reasons, both verified: (1) a station never gets a `ShipCombatValueDB` — that spec-sheet is computed only in `ShipFactory.CreateShip` (`Combat/CLAUDE.md`), so a station has no firepower/toughness/evasion the resolver can read; and (2) the whole auto-resolve engine (`CombatEngagement`, `AutoResolve`, `BattleTriggerProcessor`) is **`FleetDB`-keyed** — it enrols *fleets of ships*, and `Combat/CLAUDE.md` states outright that "a station is **not yet a target inside a fleet battle** — that needs a non-fleet 'installation combatant' representation (or a dedicated bombardment order)." So today: DS9 can be shot at and killed by a single ship's direct fire, **but it cannot shoot back, cannot be part of the fleet engagement, and cannot soak a fleet's fire as a combatant.** The fortress that must *hold the line* can't hold anything.
- **VERDICT: NEEDS-ENGINE-WORK.** The station is buildable and killable; the missing piece is the **weaponised-station combatant** — a view that (a) computes a station's combat value from its mounted weapons/shields and (b) lets the auto-resolver enrol it as a stationary combatant on a faction's side. This is the single highest-value gap this whole battle surfaces, because "a fortress-station that fights" recurs across nearly every franchise.

---

## 4. The gap ledger — everything this battle surfaced

| # | Gap | Type | Proposed home / fix | Hole |
|---|-----|------|---------------------|------|
| 1 | **Weaponised station can't fight.** DS9 can be built and destroyed but has no combat value and can't be enrolled in the fleet auto-resolver — it can't fire on or soak an attacking fleet. | **NEEDS-ENGINE** | An "installation combatant" view: compute a `ShipCombatValueDB`-equivalent from a station's mounted weapons/shields, and let `CombatEngagement` enrol a station as a stationary combatant (or add a bombardment/defense resolve). `Combat/CLAUDE.md` already names this as the follow-on to Stations Slice B. | — |
| 2 | **Weapons/shields/torpedo-launcher lack the `Station` mount flag.** They can't be mounted on `station-chassis`. | **NEEDS-CHANGE** (data) | Add `Station` to the `MountType` flags of `laser-weapon`, `pulse-laser`, `disruptor-weapon`, `missile-launcher`, `deflector-array` — purely additive, identical to the edit that gave `reactor`/`factory` their `Station` flag. `weapons.json`. | — |
| 3 | **Decloak-to-fire / cloak-fire lockout.** A cloaked ship (Klingon BoP, Defiant) can currently fire *and* stay cloaked — the cloak has no "can't shoot/shield while hidden" price, so the ambush gamble is gone. | **NEEDS-ENGINE** | A chassis **config-state** toggle: while the cloak state is on, bar weapons/shields; the alpha strike is the payoff for dropping it. | **H5** |
| 4 | **Suicide ram / collision attack.** The Jem'Hadar kamikaze run has no mechanic — no ram order, no collision-damage resolver. | **NEEDS-ENGINE** | A `RamOrder` + a collision resolve (a lump of kinetic damage = ½·mass·velocity² onto both hulls; attacker likely destroyed). Small, reusable. | H5-adjacent |
| 5 | **Self-replicating minefield sealing the wormhole** — the episode's centrepiece. No weapon-mine exists (`mine` is a *mineral* mine); no autonomous defensive-swarm or self-replication. | **NEEDS-ENGINE** (+ content) | A deployable mine/autonomous-defender unit (a `swarm-frame` body with a proximity warhead) + optional self-replication. | **H3** |
| 6 | **Photon vs quantum torpedo warhead tiers.** The `missile-launcher` fires, but there's no distinct photon/quantum ordnance design (yield difference). | **BUILDABLE-WITH-NEW-DATA** | Two ordnance designs off `missile-payload`: photon = baseline explosive/TNT-equiv; quantum = higher yield. JSON only, no engine work. `ordnance.json`. | — |
| 7 | **Continuous wide-arc phaser sweep.** A phaser array sweeps an arc and can rake several targets; modeled as a single-target `pulse-laser`/`laser-weapon`. The aggregate resolver doesn't do multi-target beam sweep. | **Fidelity compromise** | Accept the single-beam model (dial choice: `laser-weapon` for continuous, `pulse-laser` for pulsed). Multi-target sweep is a resolver nicety, not load-bearing. | — |
| 8 | **Saucer separation (Galaxy-class).** One chassis splitting into two independent ships. | **Deferred / cosmetic** | Chassis separable sub-chassis. Not needed for this battle. | **H5** |
| 9 | **Bajoran wormhole as a battlefield feature.** Ships pour out of a stable wormhole. | **EXISTS (content)** | A natural stable **jump point** already models this (`JumpPoints/`). The "chokepoint you fight over" framing is scenario content, not engine. | **H8** (mostly covered) |

**The honest weighting:** rows 1 and 2 together are the *only* thing standing between "we can stage this battle" and "we can't" — and row 2 is a one-word data edit, so **the whole battle hinges on one engine feature: the weaponised-station combatant.** Rows 3–5 add authentic *flavour* (the cloak gamble, the kamikaze, the minefield) but the fleets fight without them. Rows 6–9 are cosmetic or data-only.

---

## 5. Verdict — how completely can the game stage this battle today?

**The ship war: essentially complete.** Fourteen of the fifteen distinct unit types are **BUILDABLE-TODAY** from stock templates, most of them near-copies of warships already in the base mod. The reason is that Star Trek's tech tree lines up with parts the game already built for other reasons:

- **Shields vs shield-piercing** — the defining Dominion-War dynamic — is *already implemented*: `deflector-array` is a regenerating pool with a nature-based soak, and `disruptor-weapon` is an **exotic, shield-bypassing** beam. The Dominion polaron beam, the Klingon disruptor, and the Cardassian spiral-wave disruptor are all the same shield-piercing template, which is period-correct. This is the cleanest mapping in the battle.
- **Cloak** (`cloak-device`), **ablative armour** (`armour-hardening` + `ablative-composite-armor`), **guided torpedoes** (`missile-launcher`), **the nimble-heavy-ship dodge** (`inertialess-drive`), **point defense** against torpedoes (`point-defense-mount`), and **elite crews** (`unit-caliber`) all exist and are on real ships.
- **The Defiant validates the supply/budget gate exactly as intended** — cram a light hull with pulse phasers, quantum torpedoes, ablative armour, and a cloak, and the assembler refuses it until you compromise, which is the ship's canonical flaw.

**The one real break is the fortress.** Deep Space 9 — a *weaponised station* — is the load-bearing gap. The station builds and can be destroyed, but it cannot fight: it has no combat value and the auto-resolver only understands fleets. **This is what "you can take a planet" looks like for a station:** the game can currently *kill* DS9 but can't let DS9 *defend itself*, and a defence is the entire drama of the engagement.

**Shortest path to "playable":**
1. **(One-word data edit)** Add the `Station` mount flag to phasers, torpedoes, and shields — DS9 can now *carry* its guns. (Row 2.)
2. **(The one real engine job)** Give a station a combat value from its mounted weapons/shields and let `CombatEngagement` enrol it as a stationary combatant. Now DS9 *fights.* (Row 1.) This is the same feature every "defended station/orbital platform/planetary gun" needs, so it pays off far beyond this battle.
3. **(Optional flavour, in priority order)** the cloak decloak-to-fire tradeoff (H5), the suicide-ram order, distinct photon/quantum torpedo ordnance data, and the self-replicating minefield.

With steps 1 and 2 done, the Battle for Deep Space 9 is stageable end-to-end: a Dominion–Cardassian fleet of shield-piercing swarm craft and cruisers assaulting a shielded, phaser-and-torpedo-armed fortress, met by an outnumbered Federation–Klingon armada of cloaked raiders and torpedo cruisers. Everything else is already on the shelf.
