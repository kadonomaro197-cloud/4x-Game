> ⚠ **SURVEY DRAFT (adversarially verified 2026-07-21).** This per-battle spec was attacked by a verifier on
> authenticity + engine-reality + completeness; it carries known load-bearing errata (e.g. the ground-vs-space
> weapon-composition gap, and battle-specific canon fixes) that are **reconciled in `FRANCHISE-LITMUS-TEST.md`**
> and corrected at build time. Treat the master doc as the authority where they differ.

# Franchise Litmus Test — The Battle at the Ori Supergate (Stargate SG-1)

**As of:** 2026-07-21 · **status: SPEC (read-only survey — no engine/data changed).** A per-unit
cradle-to-grave BUILD SPEC: every ship, fighter, and the great gate that fought at the Ori Supergate, mapped onto
the components and chassis the game can **actually build today**, with an honest gap ledger where it can't.

> **What this doc is.** Not lore. It answers the one question the developer asked: *"if a battle has a Stormtrooper
> in it, can I actually build that Stormtrooper — the E-11 blaster and all — out of the Component Designer and
> Entity Assembler?"* The Ori Supergate is the **pure capital-ship probe** of the litmus battles. Where Geonosis
> stress-tested every ground chassis and Deep Space 9 stress-tested a fortress-station, this one is almost entirely
> **ships and one enormous piece of infrastructure** — and it is where the game's most *advanced* franchise
> capabilities all show up at once: matter teleportation (Asgard beaming), a carrier that launches its own fighters,
> a self-repairing enemy that is *designed to be unbeatable*, near-unlimited alien power, and a **buildable,
> dial-a-destination gate** the size of a moon. If the build system can express this fight, it can express the
> deep end of science fiction.
>
> **How to read a unit block.** Each unit lists what makes it *itself* in canon (terse), then the exact
> **Chassis + components** you'd assemble (citing the real template id and the file it lives in), then a
> **LEDGER** (EXISTS / MISSING / NEEDS-CHANGE), then a one-line **VERDICT**. The verdicts are the point — a litmus
> test earns its keep by exposing what *breaks*, not by pretending everything fits.

> **The one-sentence verdict.** The *conventional* warships fall out of the template library almost perfectly — a
> BC-304 is a shielded, railgun-plus-energy-beam heavy hull with a missile rack, and a Ha'tak is the same recipe
> with different guns, both **buildable today** as near-copies of ships already in the base mod. **Six of the seven
> unit types build today as fighting hulls.** But this battle surfaces the game's single densest cluster of the
> *hardest* catalogued holes: **Asgard beaming (H1, matter teleport), the F-302 hangar (H6, carrier-nesting), the
> Supergate itself (H8, a buildable addressable gate), the Ori ship's self-repair (H2), and Asgard near-unlimited
> power (H12)** — and the same **weaponised-station** engine gap Deep Space 9 hit, because the Supergate is a
> strategic objective you must hold or destroy. The hulls are here; the *marvels* are the frontier.

---

## 1. The battle in one paragraph

The **Ori** are ascended zealots from another galaxy who convert or exterminate everyone in their path, and they
have found a way in: a **Supergate** — a Stargate the size of a small moon, assembled in space from the matter of a
black hole, that opens a *stable, permanent* wormhole big enough for a warship to fly through, straight from the Ori
home galaxy into ours. To stop the invasion at the door, an unprecedented **allied fleet** masses at the gate: Earth's
new **BC-304 battlecruisers** (the *Odyssey*, and the Russian *Korolev*), a fleet of **Goa'uld/Free-Jaffa Ha'tak
motherships**, and — as the technological high-water mark of the whole show — the **Asgard**, whose ships beam
warheads through enemy shields and fire plasma beams that gut a hull in one pass. Against all of it come the first
**four Ori warships**: colossal, mushroom-hulled capitals with a single devastating energy beam, shields with *no
known weakness*, and a hull that *repairs itself*. The allied fleet pours everything it has into them — railgun slugs,
Asgard plasma, nuclear missiles, F-302 fighter strikes — and the Ori shields barely flicker. It is the moment the
galaxy learns it is outmatched: the allied armada is shattered, the *Korolev* is destroyed with a beam still charging,
and the Ori march through the gate. It is the perfect stress test precisely because it is a fight the *good guys lose*
— the Ori warship is a deliberately over-powered probe, and the question is whether the game's budget/supply model can
even *express* something that overwhelming, and whether the tricks that might beat it (beam a bomb past the shields,
hold the gate) have any home in the engine.

---

## 2. Force composition (the order of battle)

This is a fleet action in open space around a single, enormous fixed structure. No ground war — the chassis exercised
are **ship**, **fighter**, and **station** (the gate), which is exactly why it complements the ground-heavy Geonosis
and the fortress-centric Deep Space 9.

**The Allied Fleet (attacker/defender-of-the-galaxy, ~30+ capital ships):**
- **Tau'ri (Earth):** **BC-304 *Daedalus*-class battlecruisers** — the *Odyssey* and the *Korolev* (a Russian
  BC-304). Each carries a wing of **F-302 fighter-interceptors** in its hangar bays.
- **Free Jaffa Nation + allied Goa'uld:** a fleet of **Ha'tak-class motherships** (pyramid-hulled Goa'uld capitals),
  each with its own complement of **Death Gliders** (Jaffa fighters).
- **The Asgard (the technological pinnacle — reference unit):** the **O'Neill-class Asgard warship** — the single most
  advanced ship any ally fields, and the best possible probe for the exotic holes (beaming, plasma beams,
  near-unlimited power, self-repair). *(In the strictest on-screen continuity the Asgard sent technology rather than a
  ship to this exact engagement; the task explicitly asks it be specced as the pinnacle unit, and it is the cleanest
  test of the deep-end capabilities, so it is included as an allied capital.)*

**The Ori (the antagonist, 4 warships through the gate):**
- **Ori warships** — enormous energy-beam capitals with near-impervious shields and self-repair. Deliberately,
  narratively **over-powered** — the "can the game even hold this much ship, and is it beatable?" probe.

**The objective itself (not a combatant — the prize):**
- **The Supergate** — a ring of segments assembled in orbit from black-hole matter, forming a **buildable, addressable
  gate node** that opens a stable intergalactic wormhole. The thing the whole battle is fought over: hold it closed,
  or destroy it. In engine terms it is a **station chassis** carrying a **gate/jump-point** ability — see the gap
  ledger, hole **H8**.

---

## The parts shelf they all draw from (grepped and verified — the real inventory)

Every template id below was read in the live base-mod JSON. This battle draws almost entirely from the **ship** side
of the shelf, so the table leans that way.

| Stargate thing | Game part (template id) | Where | What it does |
|---|---|---|---|
| Ship frame | `ship-hull` | `installations.json:68` | Hull mass + a **mass budget** the mounted parts must fit inside — the supply/budget gate. Budget dial goes to **100,000,000 kg**, so a genuinely huge capital is a legal design |
| Railgun (kinetic slug) | `railgun-weapon` | `weapons.json:346` | Finite-velocity kinetic slug — **dodgeable** by a nimble target, brutal on a slow capital. Carries the `GroundUnit` mount flag too |
| Asgard plasma beam / staff cannon (energy beam) | `pulse-laser` / `laser-weapon` | `weapons.json:862` / `:5` | High-energy directed beam; **undodgeable** (light-speed), a shield half-soaks it. `pulse-laser` runs hot → needs radiators |
| Ori energy beam (huge single beam) | `pulse-laser` dialed to max | `weapons.json:862` | Pulse Energy dial to **10 MJ**/pulse — one enormous beam per second |
| Staff-cannon / plasma-bolt (dodgeable energy) | `plasma-repeater` | `weapons.json:1000` | Energy nature (shield half-soaks) but **finite bolt velocity** (dodgeable) — exactly a plasma bolt |
| Nuclear missile (guided warhead) | `missile-launcher` + a `missile-payload` ordnance | `weapons.json:235`; `ordnance.json:5` | Guided; the warhead is a separate ordnance design with a tunable **TNT Equivalent** yield |
| Shields | `deflector-array` (ShieldAtb) | `weapons.json:600` | A regenerating joule pool; soaks kinetic 100%, energy 50%, **exotic 0%** |
| Anti-shield exotic weapon | `disruptor-weapon` (Ion Disruptor) | `weapons.json:944` | Light-speed **Exotic** beam that **bypasses the shield pool** and hits the hull |
| Hull armour (nature-tuned) | `armour-hardening` | `weapons.json:655` | Soaks a chosen fraction of a chosen damage nature after the shield |
| Heat sink | `heat-radiator` | `weapons.json:773` | Sheds the pulse-laser's waste heat so the beams don't throttle |
| Point defense (shoot down missiles) | `point-defense-mount` | `weapons.json:817` | Intercepts a saturating fraction of incoming guided (missile) fire |
| Ammo store | `ship-magazine` | `weapons.json:729` | Feeds railguns/missiles; runs dry mid-fight if too small |
| Hyperdrive (FTL) | `alcubierre-warp-drive` | `engines.json:176` | Faster-than-light travel; mounts on `ShipComponent, ShipCargo, **Fighter**` |
| Sublight drive | `conventional-engine` / `scntr-engine` | `engines.json:5` / `:91` | Newtonian thrust; also drives evasion. `Fighter` mount |
| Naquadah reactor (power) | `reactor` / `steam-turbine-reactor` | `energy.json:5` / `:258` | Power for beams/shields/drives. Mounts on Ship/Fighter/GroundUnit/**Station** |
| Near-unlimited power (exotic) | *(no template — see H12)* | — | Reactor output caps at ~1.25 GW/unit; a genuinely "unlimited" source is a Power source-dial gap |
| Fire control | `beam-fire-control` | `electronics.json:184` | Raises beam tracking (hit chance vs a dodging target) |
| Bridge / command | `ship-command` (Ship Bridge) | `storage.json:246` | Command spaces + admin level |
| Veteran crew | `unit-caliber` (Veteran Cadre) | `electronics.json:685` | Multiplies firepower + toughness (elite crew) |
| Minimal-crew automation | `crew-automation` (Automation Suite) | `electronics.json:750` | Cuts the bulk crew requirement toward zero |
| Fighter/ground bay | `troop-bay` (GroundBayAtb) | `storage.json:301` | Carries **ground units** from orbit — **not** space fighters (see H6) |
| Station frame | `station-chassis` | `installations.json:1527` | The structural budget a station's modules bolt onto; `Station, ShipCargo` mount |
| Gate / wormhole node | `JPFactory` / `JumpPointDB` | `GameEngine/JumpPoints/` | A jump point — but **generated at system-gen, not player-built or addressable** (see H8) |

The existing base-mod warships are, in effect, pre-made Stargate archetypes to copy and re-proportion: the **Bastion
Shielded Cruiser** (`default-ship-design-test-shielded`) = a shielded energy warship; the **Ravager Ion Frigate**
(`test-disruptor`) = the shield-piercing exotic boat; the **Ember Pulse Cruiser** (`test-ember`) = a hot-beam ship
with its radiators; the **Leviathan Battleship** (`test-capital`) = a big hull; the **Javelin Missile Cruiser**
(`test-missile`) = a torpedo/missile boat; the **Barricade PD Escort** (`test-screen`) = a point-defense screen; the
**Praetorian Veteran Cruiser** (`test-praetorian`) = elite crew; the **Wasp Strike Fighter** (`test-fighter`) = a
fighter hull; the **Kithrin Hive Cruiser** (`hive-cruiser`) = an alien capital with 3 disruptors + 2 deflectors.
Building this fleet is mostly *renaming and re-proportioning these.*

---

## 3. THE UNITS

Notation in each BUILD line: `template-id {dial: value}` → what it contributes.

---

### SIDE A — TAU'RI / EARTH (chassis: ship + fighter)

#### UNIT: BC-304 *Daedalus*-class Battlecruiser (capital warship + carrier, chassis: ship / heavy hull) — THE CAPITAL PROBE

**AUTHENTIC CAPABILITIES:** Earth's deep-space battlecruiser — the *Daedalus*, *Odyssey*, *Korolev*. A big
reverse-engineered hull carrying: banks of **rail guns** (kinetic mass-drivers, the anti-fighter/anti-missile
volume-of-fire weapon); **Asgard plasma beam weapons** (a very-high-energy directed beam bolted on as an upgrade — the
ship's real ship-killer); **nuclear-tipped missiles** (guided, high-yield); **deflector shields**; **Asgard beaming
technology** (transport matter and personnel — including a *bomb* — instantly, through shields, ship-to-ship or
ship-to-surface); a **hangar bay of F-302 fighters** (it launches and recovers its own strike wing); an intergalactic
**hyperdrive**; and sublight engines. A Swiss-army capital — warship, carrier, and transport in one hull.

**BUILD (cradle-to-grave, exact):**
- Chassis: `ship-hull` **heavy tier** {Hull Mass ~25000, Mass Budget ~180000} (`installations.json:68`) → the big
  battlecruiser frame. The **mass budget is the gate** that makes the BC-304's everything-at-once loadout a *real
  trade*: guns, shields, hangar, and drives all bill against one ceiling.
- Rail guns: `railgun-weapon` ×4 {Muzzle Velocity 50000, Kinetic Energy 200000, Rounds/Sec 5} (`weapons.json:346`) →
  the kinetic batteries — dodgeable slugs, high volume of fire.
- Asgard plasma beams: `pulse-laser` ×2 {Range 5000, Pulse Energy 800000+, Charge Period 1, Combat Heat 300000}
  (`weapons.json:862`) → the high-energy ship-killer beam (undodgeable; a shield half-bleeds). *This is the ship's real
  teeth — the "Asgard upgrade."*
- Cooling: `heat-radiator` ×2 {Heat Capacity 500000} (`weapons.json:773`) → sheds the plasma-beam waste heat so the
  beams don't throttle mid-fight.
- Nuclear missiles: `missile-launcher` ×2 {Max Calibre 250, Max Mass 500} (`weapons.json:235`) firing a **nuclear
  `missile-payload`** ordnance {Payload Type explosive, high Explosive Mass → high TNT Equivalent} (`ordnance.json:5`).
- Ammo feed: `ship-magazine` {Ammo Capacity 5000} (`weapons.json:729`) → keeps the railguns and missiles fed.
- Shields: `deflector-array` ×2 {Shield Capacity 5000000, Recharge Rate 100000} (`weapons.json:600`) → the deflector
  pool.
- Fire control: `beam-fire-control` {Range 100, Tracking Speed 5000} (`electronics.json:184`) → plasma-beam accuracy
  against jinking Ori.
- Point defense: `point-defense-mount` (`weapons.json:817`) → the railguns' anti-missile role, formalised.
- Command: `ship-command` {Admin Level 2, Console Space 10} (`storage.json:246`) → the bridge.
- Power: `reactor` ×2 **or** `steam-turbine-reactor` (`energy.json:5`/`:258`) → the naquadah-generator stand-in.
- Propulsion: `alcubierre-warp-drive` ×2 (hyperdrive) + `scntr-engine` ×2 (sublight) + `battery-bank` + fuel.
- **F-302 hangar:** *here is the first real gap* — the bay that carries **space fighters** has no clean home (see
  LEDGER / H6).
- **Asgard beaming:** *the second real gap* — instant matter transport has no component (see LEDGER / H1).

**LEDGER:**
- **EXISTS:** the whole *warship* — heavy hull, railguns, plasma beams, radiators, guided nuclear missiles, magazine,
  shields, fire control, point defense, bridge, reactors, hyperdrive, sublight. Every one is a stock template already
  on a base-mod ship; the BC-304 is essentially a **Bastion Shielded Cruiser + Ember Pulse Cruiser + Javelin Missile
  Cruiser** merged and re-proportioned. The mass-budget gate correctly forces the trade between "more guns" and "more
  hangar/shields" — the BC-304's real design tension.
- **MISSING — design-hole H1 (matter teleportation):** **Asgard beaming has no component and must not be faked as a
  normal cargo transfer.** It is instant, *ranged*, ship-to-ship, and passes *through shields* — neither a warp drive
  (moves the whole hull) nor an ordinary cargo hold (local, docked, rate-limited). It is catalogued as **H1** with a
  named home (**Logistical ▸ Transfer "teleport mode"** — instant, ranged, mass/cycle-limited, heavy power draw) but
  that mode is **not built**. And the battle's signature Earth tactic — **beam a nuclear warhead directly inside an
  enemy ship, past its shields** — is H1 pointed *offensively*: teleport-as-weapon-delivery, with no home at all today.
- **MISSING — design-hole H6 (carrier / nested chassis):** the **F-302 hangar** carries and launches *smaller ships*.
  The one bay template, `troop-bay` (`storage.json:301`), carries **ground units** (`GroundBayAtb`, CarryClass
  Personnel/Vehicle) from orbit to a surface — it does **not** hold or launch space **fighters**. A ship that nests and
  launches child *ships* is the H6 carrier-nesting frontier ("reuse units-as-entities — a unit whose bay holds child
  units"): the entity model should support a unit owning child units, but the launch/recover of fighter-chassis craft
  is unverified and there is no fighter-bay component.
- **NEEDS-CHANGE:** none for the ship to fight as a gun platform.
- **Cradle-to-grave:** intact for the warship — mine iron/copper/tungsten → refine steel/aluminium → build each part at
  a colony → design in the Component Designer → research-gate (beam range, kinetic yield) → assemble on the heavy hull →
  it fights → a shot-off plasma beam or a drained magazine is a real component-level loss.
**VERDICT:** **BUILDABLE-TODAY as a warship** (railgun + plasma-beam + nuclear-missile + shielded heavy hull — a
straight merge of three base-mod ships) — but the **two things that make a BC-304 more than a gunboat**, the **beaming
(H1)** and the **F-302 hangar (H6)**, are the frontier. The litmus test's job: the hull is trivial; the marvels are named
holes.

---

#### UNIT: F-302 Fighter-Interceptor (space/air superiority fighter, chassis: ship / fighter hull)

**AUTHENTIC CAPABILITIES:** A two-seat space-superiority fighter reverse-engineered from the Goa'uld death glider —
**rail guns** (kinetic cannon), **missiles** (AIM-120-derived and naquadah-enhanced), a **short-range hyperspace
window generator** (a one-jump micro-hyperdrive), no shields (too small), and full **atmospheric + space** flight. The
carrier's strike wing — it swarms capital ships and dogfights enemy gliders.

**BUILD:**
- Chassis: `ship-hull` **fighter/light tier** {Hull Mass 500, Mass Budget 25000} (`installations.json:68`) → the tiny
  agile frame. The **`Fighter` mount flag** on the drives and reactor is what lets fighter-scale parts fit.
- Rail guns: `railgun-weapon` ×1 {small dial} (`weapons.json:346`) → the kinetic cannon.
- Missiles: `missile-launcher` ×1 {small} (`weapons.json:235`) firing a light `missile-payload` → the AIM-120/naquadah
  missiles.
- Power: `reactor` {small, `Fighter` mount} (`energy.json:5`) → the fighter's power.
- Sublight: `scntr-engine` / `conventional-engine` {`Fighter` mount} (`engines.json:5`/`:91`) → high thrust on a light
  hull = high evasion (the dogfighter's edge).
- Micro-hyperdrive: `alcubierre-warp-drive` {min mass} (`engines.json:176`, `Fighter` mount) → the short hyperspace
  jump. **This is a validation:** a hyperdrive on a fighter hull "just works" (universal mounting — the X-wing lesson).
- No shields (correctly — no room in the budget for a `deflector-array`).

**LEDGER:**
- **EXISTS:** the whole fighter — a small ship assembly, validated by the engines and reactor carrying the `Fighter`
  mount flag (a drive/reactor on a fighter hull just works). This is nearly the **Wasp Strike Fighter**
  (`default-ship-design-test-fighter`) with a micro-hyperdrive added.
- **MISSING:** (1) the F-302 lives in the **orbital/space** combat layer, so it can strafe capital ships in space fine —
  but **atmospheric flight over a planet** hits the same **missing air-combat layer** the Geonosis LAAT gunship hit
  (there is no altitude band coupling air to the ground map). Irrelevant *at the Supergate* (a space battle), a gap only
  if an F-302 flies air support in an atmosphere. (2) It rides in the BC-304's hangar → the H6 carrier gap above.
**VERDICT:** **BUILDABLE-TODAY** — a fighter-scale ship with a micro-hyperdrive; validates universal mounting. Only its
*basing* (the hangar, H6) and *atmospheric* role (the air layer) are gaps, neither of which bites in this space fight.

---

### SIDE B — FREE JAFFA / GOA'ULD (chassis: ship + fighter)

#### UNIT: Ha'tak-class Mothership (alien capital warship + carrier, chassis: ship / heavy hull)

**AUTHENTIC CAPABILITIES:** The pyramid-hulled Goa'uld mothership — the workhorse capital of the galaxy. **Staff
cannon batteries** (arrays of plasma-bolt emitters — energy-nature bolts), a heavier **belly/main plasma weapon**,
strong **deflector shields**, a naquadah-powered **hyperdrive**, **ring transporters** (short-range matter transport
between ships/surface), and a hangar of **Death Gliders** (its fighter wing). Tough, numerous, and — against the Ori —
tragically outclassed.

**BUILD:**
- Chassis: `ship-hull` **heavy tier** {Hull Mass ~25000, Mass Budget ~180000} (`installations.json:68`) → the pyramid
  capital frame.
- Staff cannons: `plasma-repeater` ×4 {Energy Per Shot 100000, Rounds/Sec 3, Bolt Velocity 200000} (`weapons.json:1000`)
  → the plasma-bolt batteries. **This is the cleanest possible mapping for a staff cannon:** energy nature (a shield
  half-soaks it, like a beam) but a *finite bolt velocity* (a nimble target can dodge it, unlike a beam) — exactly how a
  staff blast behaves. This is the **Tempest Plasma Lancer** / **Vanguard Plasma Skirmisher** archetype.
- Main plasma weapon: `pulse-laser` ×1 {high Pulse Energy} (`weapons.json:862`) → the heavy belly cannon (the
  undodgeable main gun) + `heat-radiator` ×1 to cool it.
- Shields: `deflector-array` ×2 {Shield Capacity 5000000} (`weapons.json:600`) → the Goa'uld shield.
- Power: `reactor` ×2 (`energy.json:5`) → the naquadah reactor.
- Propulsion: `alcubierre-warp-drive` ×2 (hyperdrive) + `scntr-engine` ×2 (sublight) + `battery-bank` + fuel.
- Command: `ship-command` (`storage.json:246`) → the bridge/peltac.
- **Ring transporters:** the same **H1 (matter teleport)** gap as Asgard beaming — no component (see LEDGER).
- **Death Glider hangar:** the same **H6 (carrier-nesting)** gap as the F-302 hangar.

**LEDGER:**
- **EXISTS:** the whole warship — heavy hull, plasma-bolt batteries (`plasma-repeater` is a near-perfect staff-cannon),
  a heavy beam, shields, hyperdrive, reactors. Structurally almost identical to the BC-304 recipe with the guns swapped
  to plasma bolts — which is period-correct (both are shielded plasma-and-missile capitals). Nearly the **Kithrin Hive
  Cruiser** with plasma repeaters.
- **MISSING:** **ring transporters → H1 (matter teleport)** and the **Death Glider hangar → H6 (carrier-nesting)** —
  identical to the BC-304's two holes. The Ha'tak confirms these aren't Earth-specific: *every* Stargate capital
  teleports and carries fighters.
- **NEEDS-CHANGE:** none to fight as a gun platform.
**VERDICT:** **BUILDABLE-TODAY** — a shielded plasma-bolt heavy capital; the `plasma-repeater` maps the staff cannon
cleanly. Rings (H1) and the glider bay (H6) are the same two frontier holes as the BC-304.

---

#### UNIT: Death Glider (Jaffa fighter, chassis: ship / fighter hull)

**AUTHENTIC CAPABILITIES:** The Goa'uld two-seat attack fighter — twin **staff cannons** (plasma-bolt emitters), fast
and agile, atmospheric and space, no shields, launched in swarms from a Ha'tak's bays.

**BUILD:**
- Chassis: `ship-hull` **fighter tier** (`installations.json:68`) → the small glider frame (`Fighter` mount).
- Staff cannons: `plasma-repeater` ×1 {small} (`weapons.json:1000`) → the twin plasma emitters.
- Power + sublight: `reactor` {small, `Fighter`} + `scntr-engine` {`Fighter`} → power and agile thrust.
- No shields, no hyperdrive (a plain glider is short-legged) — a cheap, fragile swarm craft.

**LEDGER:**
- **EXISTS:** the whole glider — the fighter-scale twin of the F-302 with plasma bolts instead of railguns.
- **MISSING:** carrier basing (H6) and — over a planet — the air-combat layer, exactly as the F-302. Neither bites at
  the Supergate.
**VERDICT:** **BUILDABLE-TODAY** — a plasma-armed fighter; same H6 basing note as every fighter here.

---

### SIDE C — THE ASGARD (the pinnacle — reference unit, chassis: ship)

#### UNIT: O'Neill-class Asgard Warship (apex capital, chassis: ship / mega-heavy hull) — THE DEEP-END PROBE

**AUTHENTIC CAPABILITIES:** The single most advanced ship any ally fields. A large, smooth-hulled capital powered by
what is effectively **near-unlimited power** (an exotic power core with no meaningful fuel constraint); **Asgard plasma
beam weapons** that slice through a hull in one pass; **transport beaming** that puts a warhead inside an enemy ship
past its shields; extremely powerful **shields**; a **self-repairing** hull and systems; and a crew of *almost nobody*
(the Asgard run their ships with a tiny crew or a holographic interface — effectively automated). The high-water mark:
if the designer can express an O'Neill-class, it can express the apex of the setting.

**BUILD (the buildable half, then the holes):**
- Chassis: `ship-hull` **very large** {Mass Budget dialed high, up toward the 100,000,000 kg ceiling}
  (`installations.json:68`) → the big apex frame. The budget dial *does* go this high, so the hull is a legal design.
- Plasma beams: `pulse-laser` ×3 {Pulse Energy near max 10000000, Charge Period 1} (`weapons.json:862`) + `heat-radiator`
  ×3 → the hull-gutting Asgard beams. Dialed near the template ceiling, these are the hardest-hitting beams in the game.
- Shields: `deflector-array` ×3 {Shield Capacity near max 50000000 each} (`weapons.json:600`) → the very powerful
  Asgard shields (stack several near the 50 MJ/pool ceiling).
- Minimal crew: `crew-automation` {Crew Reduction near max 200} (`electronics.json:750`) → the "run with almost no
  crew" Asgard trait. **This is a validation — the Automation Suite maps the near-crewless Asgard ship cleanly** (an
  economy×engineering trade: mass + high tech instead of scarce workforce).
- Elite systems: `unit-caliber` {Firepower 2.0, Toughness 2.0} (`electronics.json:685`) → the tech-superiority stamp
  (an Asgard hull genuinely out-fights an identical chassis).
- Power: `reactor` ×many / `steam-turbine-reactor` (`energy.json:5`/`:258`) → **but this is where power breaks** (H12).
- Propulsion: `alcubierre-warp-drive` (intergalactic hyperdrive) + sublight; optionally `reactionless-drive`
  (`engines.json:314`, no-fuel thrust, unlimited delta-V) → the "never runs dry" Asgard maneuvering.

**LEDGER:**
- **EXISTS:** the *hull, the beams, the shields, the near-crewless automation, the elite-systems multiplier, and the
  no-fuel drive* — an impressively large fraction of an apex ship falls straight out of the shelf. `crew-automation`
  and `unit-caliber` together are a genuinely good Asgard fit.
- **MISSING — design-hole H12 (exotic / near-unlimited power):** the base `reactor` caps at **Power Output = 50 × Mass**
  with Mass ≤ 25000 → **~1.25 GW per reactor**; the `steam-turbine-reactor` tops out around **~7 GW**. You can stack many,
  but each unit is capped and fuel-hungry — there is **no "effectively unlimited, no-fuel" power source**. Catalogued as
  **H12** with a named home (a **Power ▸ Generation source dial** — very high output, no fuel — low priority) but not
  built. For the Asgard this is real: their whole identity is *power to spare*.
- **MISSING — design-hole H1 (transport beaming):** the warhead-through-shields beaming, same as the BC-304 — H1, no home
  built.
- **MISSING — design-hole H2 (self-repair / adaptive):** the Asgard hull *regenerates*. There is **no regen/self-repair
  dial** — `ShipCombatValueDB` toughness is a static pool computed at build; a damaged component stays damaged until a
  yard repairs it. H2 is catalogued (an **Enhancers ▸ Systems "regen" dial** + a Defense **adaptation** dial) but
  **blocked on the parked per-component degraded-condition model** — genuine engine work.
- **NEEDS-CHANGE:** none for the ship to fight; the missing pieces are its *superiority*, not its ability to shoot.
**VERDICT:** **BUILDABLE-WITH-NEW-DATA + NEEDS-ENGINE-WORK.** As a very large, elite-crewed, near-crewless beam
battleship it builds today (a maxed hull + `unit-caliber` + `crew-automation` + big beams/shields). But the three
things that make the Asgard *the Asgard* — **near-unlimited power (H12), beaming (H1), and self-repair (H2)** — are all
holes, two of them (H1/H2) genuine engine work. The apex ship is the densest concentration of exotic holes in the
allied fleet.

---

### SIDE D — THE ORI (the antagonist capital, chassis: ship)

#### UNIT: Ori Warship (deliberately over-powered capital, chassis: ship / mega hull) — THE OVER-POWER PROBE

**AUTHENTIC CAPABILITIES:** An enormous mushroom-hulled capital, several times the mass of a Ha'tak. Its armament is a
**single, colossal energy beam** that one-shots most allied ships. Its **shields have no known weakness** — allied
weapons splash off with no visible effect. Its hull is **self-repairing**. It is written to be, at this point in the
story, **effectively unbeatable** — the narrative probe for "can the game hold a ship this overwhelming, and does the
budget/supply model let a big-enough fleet still, eventually, grind it down?"

**BUILD (as far as the game goes today):**
- Chassis: `ship-hull` **mega tier** {Hull Mass toward 50,000,000, Mass Budget toward 100,000,000}
  (`installations.json:68`) → **yes, the game can express a hull this big** — the budget dial genuinely reaches
  100,000 tonnes, so an Ori-scale capital is a *legal design*, not a special case. This is a real answer to the
  question: the supply/budget model **does** let you express something overwhelming.
- The great beam: `pulse-laser` ×1 {Pulse Energy at the 10000000 ceiling, Charge Period 1, Combat Heat high}
  (`weapons.json:862`) + `heat-radiator` ×several → the single devastating energy beam. Dialed to the template max, it
  is the hardest-hitting single weapon buildable.
- Near-impervious shields: `deflector-array` ×many {Shield Capacity 50000000 each — the 50 MJ ceiling}
  (`weapons.json:600`) → an enormous stacked shield pool. **Nuance (a real, on-theme in-game divergence):** the game's
  deflector has **exotic-soak 0.0**, so an **`disruptor-weapon` (Ion Disruptor, exotic) would bypass it** — i.e. in the
  engine the Ori shield *does* have a weakness (anti-shield exotic weapons), whereas canon says it had *none*. To model
  a truly weakness-less shield you'd either dial the pool astronomically high (brute force) or need a **"no-bypass"
  shield variant** (a small Defense dial gap).
- Toughness: `armour-hardening` ×several {high soak across all four natures} (`weapons.json:655`) → thick nature-tuned
  plate under the shields.
- Power/propulsion: `reactor` ×many + `alcubierre-warp-drive` → power and FTL (bounded by the same H12 power cap).
- **Self-repair:** *the real capability gap* — no regen dial (see LEDGER / H2).

**LEDGER:**
- **EXISTS:** astonishingly, **most of the over-powered ship is expressible** — the mega hull is a legal budget, the
  maxed beam is the biggest single gun, and stacked deflectors give a huge shield pool. The auto-resolver is **pure
  strength math** (`AutoResolve` sums firepower vs toughness over time), so a big-enough allied fleet *can* still grind
  an Ori ship down over enough rounds — **it is beatable in principle**, which matches the eventual arc (the Ori ships
  were *later* defeated). The budget/supply model passing this probe is a genuine win: the game can hold a deliberately
  overwhelming ship without breaking.
- **MISSING — design-hole H2 (self-repair):** the Ori hull *repairs itself mid-fight*. No regen mechanic exists;
  toughness is a static pool. This is the single biggest reason the Ori ship feels unkillable on screen, and it is a
  genuine engine hole (H2 — blocked on the per-component degraded-condition model).
- **MISSING / NEEDS-CHANGE — "shield with no weakness":** the exotic-bypass built into every game shield means the Ori
  shield is *not* truly impervious in-engine. A **no-bypass shield dial** (a Defense option: this pool soaks exotic too)
  would let you build a genuinely weakness-less shield. Small, on-theme, and reusable for any "perfect shield" franchise
  unit.
- **NEEDS-ENGINE (balance question, not a build blocker):** an Ori ship dialed to its ceilings is so far above an
  allied hull that the auto-resolver's whole-ship-casualty step may wipe the allied fleet before it accumulates enough
  pool to kill even one Ori ship — which is *narratively correct here* (the allies lose), but a designer wanting a
  *winnable* Ori fight needs the levers (numbers, beaming-a-bomb-past-shields = H1, hold-the-gate) that this battle
  shows are partly missing.
**VERDICT:** **BUILDABLE-TODAY as an over-powered gun platform** — the mega hull, the maxed beam, and the stacked
shields all exist, and the pure-math resolver keeps it *beatable in principle* (the budget model passes the over-power
probe). The two things that make it *feel* unbeatable — **self-repair (H2)** and a **truly weakness-less shield** — are
the gaps, one of them real engine work.

---

### THE OBJECTIVE — THE SUPERGATE (chassis: station + a gate ability) — THE INFRASTRUCTURE PROBE

#### UNIT: The Ori Supergate (buildable, addressable intergalactic gate, chassis: `station-chassis` + jump-point node)

**AUTHENTIC CAPABILITIES:** A ring of segments **assembled in space** (built from the matter of a black hole) into a
gate large enough to fly a warship through. It opens a **stable, permanent, intergalactic wormhole** to a *specific
dialed address* (the Ori galaxy). It is not a weapon and not a ship — it is **strategic infrastructure and the objective
of the whole battle**: the allies fight to keep it from opening / to destroy it, and later to *use* it themselves. Two
distinct engine problems in one object: (1) a **buildable, addressable gate node**, and (2) a **fought-over fixed
structure**.

**BUILD (as far as the game goes today):**
- Chassis: `station-chassis` {Structural Budget up to 10000} (`installations.json:1527`, `MountType: Station, ShipCargo`)
  → the assembled ring's frame. **This part is fully six-point-registered and buildable in a real game today** (the
  station design path — `StationDesign` / `StationFactory`). The "assembled in space from raw matter" is exactly how a
  station is built: haul the materials, assemble on-site. So a **big fixed structure in orbit builds today.**
- Power / structure modules it can mount: `reactor` (`energy.json:24`, carries the `Station` flag), plus civic/industrial
  modules — the *physical ring* is buildable.
- **The gate function:** *the first real gap.* A wormhole node is the **JumpPoints** subsystem (`JPFactory`,
  `JumpPointDB`, `GameEngine/JumpPoints/`) — but jump points are **generated at system-generation and linked
  programmatically** (`JPFactory.GenerateJumpPoints` / `LinkAllJumpPoints`); they are **not player-built, and not
  addressable on demand** to a chosen destination. There is no "dial this gate to that node" mechanic and no "construct
  a jump point" order (see LEDGER / H8).
- **Fighting over it:** *the second real gap* — a station can be destroyed but cannot fight (see LEDGER — the same
  weaponised-station break Deep Space 9 hit).

**LEDGER — this is where the objective breaks in two places:**
- **EXISTS:** the **physical structure** — a `station-chassis` with a big structural budget, assembled on-site from
  hauled materials, powered by a `reactor`. And a station **can be destroyed**: the direct-weapon-hit path is wired —
  a ship firing on a station runs `DamageProcessor.OnTakingDamage → OnStationDamage`, which drains
  `StationInfoDB.StructuralIntegrity` and calls `StationFactory.DestroyStation` at zero (`Stations/CLAUDE.md`, Slice B).
  So the Supergate-as-structure can be **built and blown up.** Half of "hold or destroy the gate" is real today.
- **MISSING — design-hole H8 (buildable / addressable gate network):** the gate *function* is unbuilt. H8 is
  catalogued with a named home — **reuse the JumpPoints subsystem: a Stargate = a buildable, addressable jump-point
  node** (Chassis ▸ Structure + a "gate" ability that registers it in a named network; dial a destination node →
  traverse; the DHD = the addressing UI) — but today jump points are natural, generated, and non-addressable. Building a
  gate, and *dialing* it to open a wormhole to a chosen system, is the gap. **Good news: the reuse target exists** — the
  whole jump/inter-system-travel machinery (`JumpOrder`, `JumpPointDB`) is in the tree; H8 is "make a jump point
  player-built and addressable," not "invent FTL."
- **NEEDS-ENGINE (the weaponised-station break, load-bearing — same as DS9):** even setting the gate function aside, the
  Supergate as a **fought-over fixed objective** hits the exact gap Deep Space 9 surfaced: **a station cannot fight
  inside a fleet battle.** Two verified reasons: (1) a station never gets a `ShipCombatValueDB` (that spec-sheet is
  computed only in `ShipFactory.CreateShip`), so it has no firepower/toughness/evasion the resolver can read; and (2)
  the auto-resolve engine (`CombatEngagement`, `AutoResolve`, `BattleTriggerProcessor`) is **`FleetDB`-keyed** —
  `Stations/CLAUDE.md` (Slice B) states outright that a station "is **not yet a target inside a fleet battle** — that
  needs a non-fleet 'installation combatant' representation (or a dedicated bombardment order)." So today the Supergate
  can be shot at and killed by a *single ship's direct fire*, **but it cannot be enrolled as a defended objective inside
  the fleet melee** the way the battle demands. (If you *wanted* the gate to shoot back — some franchises arm their
  gates — you'd also need the `Station` mount flag added to weapons/`deflector-array`/`missile-launcher`, which today
  lack it — purely additive JSON, the same one-word edit that gave `reactor` its `Station` flag.)
**VERDICT:** **NEEDS-ENGINE-WORK.** The Supergate's *structure* builds and can be destroyed today, but its two defining
roles are gaps: the **buildable, addressable gate (H8)** — reuse-JumpPoints, a bounded job — and the **fought-over
objective inside a fleet battle** (the weaponised/enrolled-station combatant, the same highest-value gap DS9 named,
which recurs across nearly every franchise).

---

## 4. The gap ledger — everything this battle surfaced

| # | Gap (what broke) | Which units hit it | Kind | Proposed home / fix | Hole # |
|---|------------------|--------------------|------|---------------------|--------|
| S1 | **Matter teleportation** — instant, ranged, ship-to-ship transport that passes *through shields* (people, cargo, and a warhead). Neither a warp drive nor a normal cargo hold. | Asgard beaming (BC-304 + O'Neill); Ha'tak ring transporters | **NEEDS-ENGINE-WORK** | The catalogued home: **Logistical ▸ Transfer "teleport mode"** — instant, ranged, mass/cycle-limited, heavy power draw. Recurs in ALL THREE franchises → high priority. | **H1** |
| S2 | **Beaming a bomb past the shield** — H1 pointed *offensively*: teleport a nuclear warhead *inside* an enemy hull, bypassing its shields. The signature anti-Ori tactic. | BC-304, O'Neill vs Ori | **NEEDS-ENGINE-WORK** | H1's teleport-mode + a weapon-delivery hook (deposit ordnance inside a target, bypassing the shield pool). The one lever that makes an Ori ship *winnable*. | **H1** |
| S3 | **Carrier / nested chassis** — a ship that houses and **launches space fighters** (child ships). The one bay template (`troop-bay`, `storage.json:301`) carries *ground units*, not fighters. | BC-304 (F-302 hangar); Ha'tak (Death Glider bay) | **NEEDS-ENGINE-WORK (verify)** | Reuse units-as-entities: a bay mode that holds/launches *child ship-units*. The entity model should support a unit owning children; launch/recover of fighter-chassis is the verify item. | **H6** |
| S4 | **Buildable, addressable gate** — a player-constructed wormhole node you dial to a chosen destination. Jump points are generated at system-gen and non-addressable (`JPFactory`). | The Supergate | **NEEDS-ENGINE-WORK (reuse)** | Reuse the **JumpPoints** subsystem: a station-chassis + a "gate" ability that registers an addressable node; a build order + a dial-destination order. The FTL machinery already exists. | **H8** |
| S5 | **Weaponised / enrolled station in a fleet battle** — a fixed structure can be destroyed by direct fire but has no `ShipCombatValueDB` and can't be enrolled in the `FleetDB`-keyed auto-resolver as a fought-over objective (or a gun platform). | The Supergate (objective); any armed gate/platform | **NEEDS-ENGINE-WORK** | An "installation combatant" view: compute a station's combat value from its modules and let `CombatEngagement` enrol it as a stationary combatant (or a bombardment resolve). The highest-value cross-franchise gap (also DS9). | — (recurring) |
| S6 | **Self-repairing / adaptive hull** — a ship that regenerates hull/systems mid-fight (and a shield that becomes immune after a hit). Toughness is a static pool computed at build. | Ori warship; Asgard O'Neill | **NEEDS-ENGINE-WORK** | An **Enhancers ▸ Systems "regen" dial** + a Defense **adaptation** dial. Blocked on the parked per-component degraded-condition model. | **H2** |
| S7 | **Near-unlimited / no-fuel power** — an exotic core with effectively unlimited output. The base `reactor` caps at ~1.25 GW/unit (`50 × Mass`, Mass ≤ 25000); steam-turbine ~7 GW; all fuel-hungry. | Asgard O'Neill (naquadah/exotic core) | **BUILDABLE-WITH-NEW-DATA** | A **Power ▸ Generation source dial** — very high output, no fuel, behind high tech. New JSON template, no engine work. Low priority. | **H12** |
| S8 | **Shield with no weakness** — every game `deflector-array` has exotic-soak 0.0, so an exotic weapon bypasses it; a *truly* impervious shield can't be built (only brute-forced with a huge pool). | Ori warship | **BUILDABLE-WITH-NEW-DATA** | A **no-bypass shield dial** (this pool soaks exotic too) — a small additive Defense option; reusable for any "perfect shield" franchise unit. | — (new, small) |
| S9 | **Nuclear-warhead ordnance tier** — the `missile-launcher` fires, but the specific high-yield nuclear payload is a data design, not present. | BC-304, F-302, Ha'tak | **BUILDABLE-WITH-NEW-DATA** | A `missile-payload` design (`ordnance.json:5`) with a high **Explosive Mass / TNT Equivalent** (a nuclear payload type). JSON only, no engine work. | — |
| S10 | **`Station` mount flag on weapons/shields** — *if* you want an armed gate, weapons/`deflector-array`/`missile-launcher` lack the `Station` flag, so they can't mount on `station-chassis`. | The Supergate (only if armed) | **NEEDS-CHANGE (data)** | Add `Station` to those `MountType` flags — purely additive, identical to the edit that gave `reactor` its `Station` flag. Only needed for an armed variant. | — |
| S11 | **Atmospheric air-combat layer** — a fighter flying *air support over a planet* (as opposed to space) has no home. | F-302, Death Glider (only in atmosphere) | **NEEDS-ENGINE-WORK** | The same missing air/altitude band Geonosis surfaced (the LAAT). **Does not bite at the Supergate** — this is a pure space battle. | — (from Geonosis) |

**The shape of the gaps:** this battle is a **frontier concentrator.** Of its eleven gaps, **five are genuine engine
work** (H1 teleport ×2, H6 carrier-nesting, H8 the gate, the S5 station-combatant, H2 self-repair) — the largest such
cluster of the litmus battles, because the Supergate fight is where the *most advanced* franchise capabilities live.
Three are cheap **data/dial** fixes (H12 power, S8 no-bypass shield, S9 nuclear ordnance), one is a **mount-flag** edit
(S10, only for an armed gate), and one (S11 air layer) is inherited from Geonosis and **doesn't bite here** (space
battle). Notably, the **budget/supply model passes the over-power probe**: the game *can* express an Ori-scale hull and
still keep it beatable by strength math — a real win.

---

## 5. Verdict — how completely can the game stage this battle today?

**Headline: the fleet's *hulls* are ~90% buildable-today, but this battle surfaces the game's densest cluster of
load-bearing frontier holes — because it is the fight where science fiction's *marvels*, not its gunships, decide the
outcome.** Every warship in the order of battle builds today as a fighting hull: the **BC-304** is a straight merge of
three base-mod ships (shielded cruiser + hot-beam cruiser + missile cruiser); the **Ha'tak** is the same recipe with
`plasma-repeater` staff cannons (a near-perfect map — energy-nature, dodgeable bolts); the **F-302** and **Death
Glider** are fighter-scale ships (with a validated micro-hyperdrive); the **Asgard O'Neill** builds as a maxed,
elite-crewed, near-crewless beam battleship (`unit-caliber` + `crew-automation` are a clean Asgard fit); and — the real
surprise — the **Ori warship** is *expressible*: the hull budget genuinely reaches 100,000 tonnes, the maxed
`pulse-laser` is the biggest single gun, stacked deflectors give a huge shield pool, and the pure-math resolver keeps it
*beatable in principle*. Each of these is reachable cradle-to-grave: mine and refine the metals, build the part at a
colony, design it in the Component Designer, research-gate it, assemble it on a hull in the Entity Assembler, and lose a
component when it's shot off.

**What's load-bearing vs. cosmetic:**
- **Load-bearing (must build to stage this battle recognizably):**
  1. **The weaponised/enrolled-station combatant (S5)** — the Supergate is the *objective of the whole battle*, and a
     station today can be destroyed by a single ship's direct fire but **cannot be a fought-over target inside the fleet
     melee.** This is the same highest-value gap Deep Space 9 named, and it recurs across nearly every franchise's
     defended-installation.
  2. **Matter teleportation (S1/S2, H1)** — beaming is not garnish in Stargate; it is *how the good guys fight*. Beaming
     a warhead past the Ori shields (S2) is the single lever that turns an unwinnable Ori fight into a winnable one, and
     it has **no home** today. Named home (Logistical ▸ Transfer teleport-mode); real engine work.
  3. **The buildable, addressable gate (S4, H8)** — the Supergate *is* the plot. Reusing JumpPoints makes this a
     *bounded* job (the FTL machinery exists; the gap is "player-built + dial-a-destination"), but without it the
     objective is inert scenery.
- **Cosmetic-to-medium (improves fidelity, doesn't block a recognizable fight):** the F-302/Glider **hangars (S3, H6 —
  carrier-nesting)** are a verify item (fly the fighters as independent ships and skip the basing); **near-unlimited
  power (S7, H12)** and the **no-weakness shield (S8)** are cheap data dials; the **nuclear ordnance (S9)** is a data
  design; the **`Station` mount flag (S10)** is a one-word edit needed only if the gate shoots back; the **air layer
  (S11)** doesn't bite in space.
- **The honest asterisk — self-repair (S6, H2):** the Ori ship's regenerating hull is the biggest reason it *feels*
  unbeatable, and it's genuine engine work (blocked on the per-component degraded-condition model). You can field an
  overwhelming Ori ship today; you can't yet field one that *heals*.

**Shortest path to "playable Ori Supergate":** (1) build the **installation-combatant** view so the Supergate can be a
fought-over objective inside the fleet battle (S5) — the same feature every defended station/platform/gate needs, so it
pays off far beyond this fight; (2) reuse **JumpPoints** to make the gate **buildable + addressable** (S4/H8) so the
objective is real; then (3) build the **teleport-mode (H1)**, whose offensive use — beaming a bomb past a shield (S2) —
is the one lever that makes the Ori beatable and the battle a *game* rather than a cutscene. Ship it with the cheap data
dials (nuclear ordnance, a no-bypass shield, an exotic power source) and defer the carrier-nesting and self-repair to
follow-on passes. That sequence turns a fleet of ~90%-buildable hulls into a fully recognizable — and, unlike the show,
*winnable* — stand at the gate.
