# The Universal Component Designer — Categories, Doors & Dials

**As of:** 2026-07-09 · branch `claude/sol-playtest-earth-map-8r59j6` · **status: DESIGN-LOCKED — all 11 categories / 37 doors locked, stress-tested, holes catalogued + dispositioned, and WIRING-READY.**

> **Consolidated 2026-07-13 from:** `docs/COMPONENT-DESIGNER-CATEGORIES.md` + `docs/COMPONENT-DESIGNER-STRESS-TEST.md`. The full unit-by-unit stress-test build, the shared-effect-bus insight, and the tier-by-cost hole-plugging catalogue that used to live in the companion `COMPONENT-DESIGNER-STRESS-TEST.md` are now folded in below as **"Appendix: Franchise Stress-Test & Hole Resolutions."** §4/§5 remain the executive summary; the appendix is the full detail.

> **⚙ WIRING-READY (2026-07-09).** This doc is the **map** (the 11 categories, the boundary, the stress test, the holes). The **wiring reference for each category is its self-contained ⚙ Wiring Dossier** in `COMPONENT-DESIGNER-DIALS.md` (`⚙ 1`…`⚙ 11`) — dials → engine-wire (file:line) → resolver insertion → dead stubs → §0g stamp, all verified against the live engine. **To wire a category, read its dossier; read this doc for the boundary rules and the hole it owns.** Together, these two docs are the only reference needed to wire — every external source (the resolver anatomy, the five merged strategic docs) has been transferred, translated, and folded into the dossiers.

> The decision doc for collapsing Pulsar's **67 hand-authored component templates** into **11 parametric designer categories**. Supersedes the "author a new template per thing" model and the "reconcile the two parallel (space/ground) systems" plan (`WEAPON-UNIFICATION-DESIGN.md`, deleted). The current-state evidence this replaces is `docs/DESIGNER-AUDIT/`. The governing principle is `docs/UNIVERSAL-ASSEMBLY-DESIGN.md`.

---

## 1. The idea in one line

A weapon (or engine, or sensor…) is not a *type you pick off a shelf* — it's a **family you design inside.** Click **Weapons**, get five doors (Energy / Ballistic / Melee / Guided / Exotic); behind each door is a wall of sliders. "Laser," "phaser," "railgun," "disruptor" aren't authored parts — they're **slider settings** that *fall out* of the family. Fewer categories, each one bottomless.

**A buildable thing = a Chassis + components**, where every component is a `category ▸ door` design shaped by dials, mounted on any chassis that can carry and *supply* it (power / ammo / crew / structural budget). The same act builds a power-armored trooper, a stealth frigate, or a research station — they differ by **scale and fit, not kind.**

This is the realization of `UNIVERSAL-ASSEMBLY-DESIGN.md`: everything is a chassis (a structural budget) + components (that spend the budget and contribute stats), emergent, gated by physics, at any scale within tech/scale caps.

---

## 2. The 11 categories

| # | Category | Doors | Absorbs (old templates → gone) |
|---|----------|-------|-------------------------------|
| 1 | **Weapons** | Energy · Ballistic · Melee · Guided · Exotic | laser/railgun/flak/disruptor/plasma/missile-launcher + all ground weapons + missile payload/electronics |
| 2 | **Propulsion** | Traction · Fluid · Reaction · Warp · Exotic | conventional/scntr engine, warp drive, ground-locomotion, missile-srb |
| 3 | **Sensors** | Detection · Survey · Fire Control · **Electronic Warfare** | passive-sensor, ground-radar, geo/grav-surveyor, beam-fire-control |
| 4 | **Power** | Generation · Storage | reactor, rtg, steam-turbine, solarArray, battery-bank |
| 5 | **Defense** | Armor · Shields · Hardening · Fortification | ground-plating, deflector-array, shield-generator, sensor-hardening, bunker |
| 6 | **Enhancers** | Bio-augmentation · Training/Doctrine · Systems | reflex-booster, power-armor *(net-new category otherwise)* |
| 7 | **Industrial** | Extraction · Fabrication | mine, automine, factory, refinery, shipyard, local-construction, launch-complex |
| 8 | **Logistical** | Storage · Transfer | cargo/fuel holds, warehouse, tanks, spaceports, ordnance hold, troop-bay, ground-magazine, logistics-office |
| 9 | **Civic** | Habitation · Development | infrastructure, space-habitat, research-lab, naval-academy |
| 10 | **Command** | Command *(colony/fleet scale dial)* | admin-complex, ship-command |
| 11 | **Chassis** | Personnel · Vehicle · Hull · Structure · Mega | human/vehicle/walker/swarm frames; **kills** the whole-unit shortcuts (infantry/armor/artillery-unit) |

**~37 doors replace 67 templates.**

### Dials are the depth; roles are dials, not doors
The recurring lesson: what *feels* like a type is usually a **dial**. Point-defense = a Ballistic weapon with rate-of-fire + saturation up. Anti-shield = a damage-nature dial (so a "disruptor" is just an Energy weapon with shield-piercing up). Amphibious/hover/airbreathing = Propulsion ▸ Fluid dials. Stealth = an Armor layer. Solar = a Power ▸ Generation *source* dial. Colony-vs-fleet command = one Command door with a scale dial. **Name the dial before you reach for a new door.**

---

## 3. What this fixes / adds (beyond the shrink)

- **Two net-new capabilities the game never had:** **Electronic Warfare** (jam / spoof / cloak / decoys — the counter to detection) and **Enhancers** (the force-multiplier layer that makes a veteran ≠ a conscript).
- **Ship hulls become real designs** (today they're implicit in `ShipDesign`).
- **Stations finally get a design class** (Chassis ▸ Structure) — the `DESIGNER-AUDIT` §02/§05 "no station design class" gap, closed.
- **Data bugs dissolve:** the `solarArray` ship-only mount and the duplicate `spaceport` both vanish (they become Power ▸ Generation and Logistical ▸ Transfer designs).
- **The parallel ground systems die for good:** `GroundWeaponAtb`, `GroundSensorAtb`, `GroundLocomotionAtb`, `GroundAugmentAtb`, `GroundArmorAtb`, `GroundMagazineAtb` etc. are absorbed into the universal doors (the 9 duplicated ability pairs from `DESIGNER-AUDIT/03`, resolved by deletion not merger).

### The designer's boundary (what it does NOT make)
The designer makes **gear**. It does **not** make the *being* that uses it. Innate/pilot abilities — the Force, psionics, a species trait, raw veterancy — live in the **People / crew / commander / morale** system, not the component store. Enhancers is the *bridge* (buildable bio/training/systems mods), but a Jedi's telekinesis is a **pilot trait**, not a component. This boundary is load-bearing (see hole H4).

---

## 4. Stress test — 12 iconic units across three franchises

Each unit was "built" through the designer to find where it strains. ✅ = maps cleanly. ⚠ = exposes a hole (catalogued in §5). **Full door-by-door build of every unit + the detailed hole-plugging analysis is in the companion `docs/COMPONENT-DESIGNER-STRESS-TEST.md`.**

### Star Trek
1. **Galaxy-class *Enterprise*** — Chassis ▸ Hull (large) · Propulsion ▸ Warp + Reaction (impulse) · Weapons ▸ Energy (phasers) + Guided (photon torpedoes) · Defense ▸ Shields · Sensors ▸ Detection + Survey (science ship) · Civic ▸ Habitation + Development (families + labs aboard). ✅ mostly — but **saucer separation** ⚠H5 (one chassis → two operational ships) and the **transporter** ⚠H1 (beam matter/people point-to-point) have no home.
2. **Borg Cube** — Chassis ▸ Structure/Mega (cube) · distributed, **no bridge** ⚠H10 (crewless collective, no Command node) · Defense ▸ **adaptive** shields ⚠H2 (immune to a weapon after one hit) + self-regenerating hull ⚠H2 · Weapons ▸ Exotic (cutting beam) + **assimilation** ⚠H9 (convert enemy into you) · Propulsion ▸ Warp (transwarp). Heavily holes-y — the Cube is a hole magnet.
3. **USS *Defiant*** — Chassis ▸ Hull (small) · Weapons ▸ Guided (quantum torpedoes) + Energy (pulse phasers) — *over-gunned for its hull*, which the **supply/budget gate** correctly stresses ✅ (a small chassis can't power it all → the gate bites, exactly as intended) · Sensors ▸ **EW cloak** ✅ (validates the new EW door) · Defense ▸ Armor (ablative — but *regenerating* ablative ⚠H2).
4. **Security officer + hand phaser + tricorder** — Chassis ▸ Personnel · Weapons ▸ Energy (tiny, **stun/kill mode dial** ✅) · Sensors ▸ Detection/Survey (tricorder at personnel scale ✅). Clean — validates the small end.

### Star Wars
5. **Imperial Star Destroyer** — Chassis ▸ Mega/Hull · Weapons ▸ Energy (turbolasers) + Ballistic (ion cannons) · Propulsion ▸ Warp (hyperdrive) · Command ▸ (fleet flagship ✅) · Logistical ▸ Storage (TIE hangar) — a **carrier** ⚠H6 (houses & launches Fighter-chassis units; nested assembly) · Exotic (tractor beam) ⚠H1-adjacent. Mostly ✅; carrier nesting to verify.
6. **X-wing** — Chassis ▸ Hull (fighter) · Weapons ▸ Energy (laser cannons) + Guided (proton torpedoes) · Propulsion ▸ **Warp on a fighter** ✅ (universal mounting — a hyperdrive on a small hull just works) · Defense ▸ Shields · **astromech droid** ⚠ (an AI "crew slot" = Enhancers ▸ Systems, ✅-ish) · **S-foils** ⚠H5 (a config toggle: cruise vs attack). Validates universal mounting; flags config-states.
7. **AT-AT** — Chassis ▸ Vehicle · Propulsion ▸ Traction (walker legs) · Weapons ▸ Energy (heavy laser) · Defense ▸ Armor (heavy) · Logistical ▸ Storage (troop bay) ⚠H6 · atmosphere-only (a Propulsion medium constraint ✅). Clean — validates Vehicle + Traction + carrier.
8. **Jedi + lightsaber** — Weapons ▸ Melee (an *energy* melee — Melee door, Energy nature ✅, so nature is cross-cutting) — but the blade **deflects blaster bolts** ⚠H7 (a weapon that is also a defense) · and **the Force** ⚠H4 (telekinesis/precognition — NOT a component; a pilot trait, the designer's hard boundary). The single best boundary probe.

### Stargate
9. **The Stargate (+ DHD)** — Chassis ▸ Structure · Propulsion ▸ **Warp ▸ gate** — but a gate is a **network node** ⚠H8 (addressable, instant, galaxy-spanning many-to-one), not a drive; and it transports *matter/people through itself* ⚠H1. The purest network/teleport probe.
10. **Goa'uld Ha'tak** — Chassis ▸ Hull (pyramid) · Weapons ▸ Energy (staff cannons) · Defense ▸ Shields · Propulsion ▸ Warp (hyperdrive) · Logistical ▸ Storage (Jaffa + gliders — carrier ⚠H6) · **ring transporter** ⚠H1. Conventional except the rings.
11. **Replicator** — Chassis ▸ Personnel/sub-personnel (modular blocks) · **self-replication** ⚠H3 (builds copies of itself from raw matter — mobile Industrial on a unit) · **reconfigurable** ⚠H5 (blocks rearrange) · Defense ▸ **adaptive** ⚠H2. The single worst-case: three holes at once. If the designer can express a Replicator, it can express almost anything.
12. **Jaffa warrior** — Chassis ▸ Personnel · Weapons ▸ **staff weapon** ⚠H7 (plasma bolt **and** blunt melee — one item, two weapon doors) · Defense ▸ Armor · Enhancers ▸ **Bio-augmentation** (the symbiote grants health/longevity ✅ — validates Enhancers). Clean except the dual-mode weapon.

**Verdict: ~80% of every franchise maps cleanly** — conventional hulls, guns, engines, shields, sensors, carriers, and personnel kit all fall out of the doors and dials, and two new doors (EW, Enhancers) earn their keep immediately (cloak, symbiote). **The exotic 20% clusters into a dozen holes**, below.

---

## 5. Holes — the designer's gaps (prioritized)

Ranked by how often they recurred across franchises and how load-bearing they are.

| # | Hole | Where it hit | Proposed home / fix | Priority |
|---|------|--------------|---------------------|----------|
| **H1** | **Matter teleportation** — beam people/cargo point-to-point instantly | transporter, ring transport, Asgard beaming, the Stargate itself | a **Logistical ▸ Transfer "ranged/teleport" mode** (instant, ranged, capacity-limited), OR an Exotic utility. Recurs in ALL THREE franchises. | **HIGH** |
| **H4** | **Innate / pilot "magic"** — the Force, psionics, hand-device | Jedi, Trek psychics, Goa'uld | NOT a component. Belongs to **People / crew / commander traits**. Defines the designer's boundary (§3). Enhancers is the buildable bridge; the innate part is the People system. | **HIGH (conceptual)** |
| **H2** | **Adaptive / self-repairing defense** — become immune after a hit; regrow armor/hull | Borg shields, Replicator, ablative regen | a Defense **"adaptation"** dial (resistance grows with exposure) + **Enhancers ▸ Systems** self-repair (regen a pool over time). | MEDIUM-HIGH |
| **H5** | **Modular / config / separable chassis** — split into two, or toggle a combat config | saucer sep, S-foils, reconfigurable blocks | **(a)** chassis **config-states** (a toggle that swaps stats/mounts — cheap); **(b)** **separable sub-chassis** (one design → two independent units — harder). | MEDIUM |
| **H3** | **Self-replication / mobile fabrication** — a unit that builds itself or others | Replicators, carriers building fighters, mobile shipyards | let **Industrial doors mount on ANY chassis** (mobile fabrication — a small universal-mount change); **self-replication** (build a copy of your own design) is a deeper new mechanic. | MEDIUM |
| **H7** | **Hybrid / cross-category components** — one item, two roles | staff weapon (Energy+Melee), lightsaber (weapon+defense) | allow a component to carry **two doors** (a weapon spanning families), and **weapon×defense** cross-links (a blade that parries). | MEDIUM |
| **H8** | **Network / relay infrastructure** — addressable, instant, many-to-one | Stargate network, subspace relays, hyperlane gates | **Propulsion ▸ Warp ▸ gate** needs a **network/addressing** layer (which node connects to which) — more than a drive. | MEDIUM |
| **H6** | **Carrier / nested assemblies** — a chassis that houses & launches smaller chassis | Star Destroyer TIEs, Ha'tak gliders, AT-AT troops | mostly covered (Logistical ▸ Storage holds units, Transfer launches) — **verify nested-chassis + launch/recover** actually resolves. | LOW-MEDIUM |
| **H9** | **Conversion / assimilation weapons** — turn an enemy into you; mind-control | Borg, mind control | unit-level **capture/convert/subvert** (ties to the espionage system, not just region-capture). | LOW-MEDIUM |
| **H10** | **Autonomous / crewless / hive units** — zero crew, or many sharing one mind | Borg collective, Replicators, drones | **Enhancers ▸ Systems** automation → crew to zero (AI); **hive** = a Command/formation variant (shared span). | LOW-MEDIUM |
| **H11** | **Scale extremes** — planet-sized (Death Star, Cube) to nanite (Replicator block) | Mega + sub-Personnel ends | validate the **Chassis scale dial + tech/scale caps** span ~10 orders of magnitude cleanly. | LOW |
| **H12** | **Exotic power source** — effectively unlimited / no-fuel | Asgard, advanced tech | likely just a **Power ▸ Generation source dial** (very high output, no fuel). Validates Power. | LOW |

### Hole disposition (2026-07-09) — where each hole now lives, per the ⚙ Wiring Dossiers
Every hole now has a named home + status in a category dossier (`COMPONENT-DESIGNER-DIALS.md`):
- **H1** teleport → Logistical ▸ Transfer teleport-mode *(open; home named — dossier ⚙ 8)*.
- **H2** adaptive/self-repair → Defense adaptation dial + Enhancers self-repair regen *(both blocked on the parked per-component degraded-condition model — ⚙ 5 / ⚙ 6)*.
- **H3** self-replication/mobile-fab → Industrial: mobile-fabrication = a cheap universal-mount *(home Industrial ⚙ 7)*; self-replication deferred.
- **H4** innate/pilot "magic" → **owned** as the gear-vs-being boundary (Enhancers ⚙ 6 is the buildable bridge; the innate part is People).
- **H5** modular/config/separable chassis → Chassis *(open — ⚙ 11)*.
- **H6** carrier/nested → Logistical *(resolved for ground units; one verify item: nested-**chassis** launch reuses the tow re-parent — ⚙ 8)*.
- **H7** hybrid/cross-category weapon → Weapons *(a component carrying two doors — ⚙ 1)*.
- **H8** network/relay → **the two new-door candidates fill it**: the C3 Relay (Command §10.2, ⚙ 10) and Route Works / jump-gate (⚙ 2 / exploration) — the same physical shape.
- **H9** conversion/assimilation → Weapons ▸ Exotic `Selectivity = convert` via the §0h projector tag, graded against the legitimacy resolver *(⚙ 1)*.
- **H10** crewless/hive → Enhancers ▸ Systems automation + a Command shared-span variant *(⚙ 6 / ⚙ 10)*.
- **H11** scale extremes → Chassis ▸ Mega *(⚙ 11)*.
- **H12** exotic power → Power ▸ Generation source dial *(low; its one net-new need is the containment/meltdown mechanic, which the reactor grave-rung fix feeds — ⚙ 4)*.

### The two holes that matter most
- **H1 (teleportation)** is the most *recurring* — every franchise has instant matter transport, and it's neither Warp (moves the whole chassis) nor normal Transfer (local, rate-limited). It wants a **Transfer ▸ teleport mode**, and it's cousin to **H8 (gate networks)** — both are "instant point-to-point," one for matter, one for the whole ship.
- **H4 (innate powers)** is the most *conceptual* — it draws the designer's edge. The designer builds **gear**; the **People system** provides the **being**. Getting this line right is what keeps "the Force" from becoming a fake component. It's the same boundary Enhancers walks: buildable augments on the component side, innate traits on the People side.

### What the stress test VALIDATED (the wins)
Cloaking → **EW** door (new, earns it). The Goa'uld symbiote → **Enhancers ▸ Bio-augmentation** (new, earns it). A hyperdrive on an X-wing → **universal mounting** (a Warp drive on a fighter hull "just works"). The over-gunned Defiant → the **supply/budget gate** correctly refuses to power it all. Phaser-to-Death-Star → the **Chassis scale ladder** (Personnel → Mega). Stun/kill, amphibious, solar, colony/fleet command → all **dials, not new doors** (the core lesson held).

---

## 6. Next steps

This doc locks the **categories**; the design is complete and **wiring-ready** (each category's ⚙ Wiring Dossier is the reference). The path to code:
1. ~~**Resolve the top holes**~~ — DONE: every hole is dispositioned above and homed in a dossier.
2. **Land the shared prerequisites first** (they recur across dossiers, cheap, and unblock many doors): the **resolver merge** (`RESOLVER-MERGE-DESIGN.md` — so each dial's term is built once, bucketed, for ships AND soldiers); the **§0b mass-budget cap ported to ships+stations** (⚙ 11 — makes "the numbers force the build" real at all scales); the **durable-seat + `LeaderLost` + `OnComponentUninstallation`** fixes (⚙ 10 — unblocks all of Command); the **broken refining feed** (⚙ 7 — unblocks the materials economy); and the **`BonusCategory` combat entry + the copyable rung-4 wire** (⚙ 6 — lights commander competence AND unit-caliber elites at once).
3. Weapons is the pilot category (most-designed): build the 5-door Weapons designer first, prove the parametric + universal-mount + supply-gate loop end-to-end against dossier ⚙ 1, then replicate the pattern across the other ten — each already carries its own wiring dossier.

**The bar:** a franchise fan should be able to sit down at **Weapons ▸ Energy** and build a phaser, a lazpistol, and a Death-Star beam from the same sliders — and have each mount on any chassis that can supply it.

---

# Appendix: Franchise Stress-Test & Hole Resolutions

**Folded in 2026-07-13 from `COMPONENT-DESIGNER-STRESS-TEST.md` (as of 2026-07-08).** This is the full unit-by-unit build behind the §4 summary, plus the effect-bus insight and the tier-by-cost plug catalogue behind the §5 hole table.

**Headline:** ~80% of every franchise falls straight out of the doors+dials. Of the 12 holes, **11 plug with a dial/mode on an existing door or by reusing an engine system Pulsar already has** (jump points, capture, doctrine-switch, shield-regen, the crew-supply gate, units-as-entities). Only a few need a genuinely new mechanic — and the deepest one (H4) is a *boundary decision*, not a new parallel system. The categorization holds.

---

## Appendix Part 1 — The 12 units, built door by door

Notation: `Category ▸ Door (dial notes)`. ✅ clean · ⚠Hn strains hole n.

### Star Trek

**1. Galaxy-class *Enterprise* — the explorer flagship**
- Chassis ▸ Hull *(large, high budget, many hardpoints)*
- Propulsion ▸ Warp *(cruise dial high)* + Reaction *(impulse, sublight maneuver)*
- Power ▸ Generation *(warp core = very-high-output source)* + Storage
- Weapons ▸ Energy *(phaser arrays — wide-arc, continuous-beam)* + Guided *(photon torpedoes)*
- Defense ▸ Shields *(large regen pool)* + Armor *(light)*
- Sensors ▸ Detection *(long range)* + Survey *(deep — a science ship)* + Fire Control
- Civic ▸ Habitation *(families aboard — big)* + Development *(labs = Research)*
- Logistical ▸ Storage + Transfer *(shuttlebay, cargo)* · Command ▸ *(flagship span)* · Enhancers ▸ Systems *(computer core)*
- **Strains:** ⚠H5 saucer separation (one hull → two ships); ⚠H1 transporter. Holodeck = a Habitation luxury dial (fine).
- **Insight:** a civilian+science+warship on one hull — the categories fuse it effortlessly. Only transporter + saucer-sep miss.

**2. Borg Cube — the hole magnet**
- Chassis ▸ Structure/Mega *(cube — regular, no "front")*
- Command ▸ **distributed/hive** ⚠H10 *(no bridge)*
- Power ▸ Generation *(massive)* · Propulsion ▸ Warp *(transwarp)* + Exotic *(transwarp conduits ≈ gate network)* ⚠H8
- Weapons ▸ Exotic *(cutting beam)* + **assimilation** ⚠H9 *(convert, don't destroy)*
- Defense ▸ **adaptive** Shields ⚠H2b *(immune to a weapon after one hit)* + **self-regenerating** structure ⚠H2a
- Industrial ▸ Fabrication ⚠H3 *(builds more Borg internally)* · Logistical ▸ Storage *(drones)*
- **Strains:** five at once — H10, H8, H9, H2a, H2b, H3.
- **Insight:** if the designer can express a Cube, it's essentially complete. It's the concentrated worst case alongside the Replicator.

**3. USS *Defiant* — the glass cannon**
- Chassis ▸ Hull *(small, tight budget)*
- Weapons ▸ Guided *(quantum torpedoes)* + Energy *(pulse phaser cannons)* — **deliberately over-armed**
- Power ▸ Generation *(oversized core — costs budget+mass)* · Propulsion ▸ Warp + Reaction
- Defense ▸ Armor *(ablative — regenerating* ⚠H2a*)* + Shields
- Sensors ▸ **EW cloak** ✅ *(the Romulan device — validates the new EW door)*
- **Strains:** only ⚠H2a (regenerating ablative armor). The "too much gun for the hull" is **not a hole** — the **supply/budget gate is supposed to bite, and it does** ✅ (the Defiant only works by also mounting an oversized core, which eats budget/mass — exactly the tradeoff the gate enforces).
- **Insight:** the Defiant *validates the supply gate* — the canonical frame-straining glass cannon, modeled natively.

**4. Security officer + hand phaser + tricorder — the small end**
- Chassis ▸ Personnel
- Weapons ▸ Energy *(hand phaser — tiny output, **stun/kill mode dial** ✅)*
- Sensors ▸ Detection + Survey *(tricorder — a survey sensor on a soldier ✅ universal mounting)*
- Defense ▸ Armor *(uniform, minimal)* · Logistical ▸ Storage *(kit)*
- **Strains:** none. **Insight:** validates the Personnel end, stun/kill as a **dial**, and universal mounting *downward* (a survey rig on a person).

### Star Wars

**5. Imperial Star Destroyer — the capital**
- Chassis ▸ Mega/Hull *(capital)*
- Weapons ▸ Energy *(turbolaser batteries, wide-arc)* + Ballistic *(ion cannons — "disable" nature)* + Exotic *(tractor beam ⚠ — a ranged grab)*
- Propulsion ▸ Warp *(hyperdrive)* + Reaction · Power ▸ Generation *(huge)*
- Defense ▸ Shields *(deflector)* + Armor · Command ▸ *(fleet flagship — big span)*
- Logistical ▸ Storage *(**TIE hangar** = carrier* ⚠H6*)* + Transfer *(launch)* · Sensors ▸ Detection + FC · Civic ▸ Habitation *(crew of thousands)*
- **Strains:** ⚠H6 carrier (houses/launches fighter chassis); the tractor beam = an **effect on an external unit** (cousin of H1/H9).
- **Insight:** a mostly-clean capital; its exotic bits point at nested-chassis (H6) and the "act on another unit" effect family (tractor/capture/assimilate share a shape).

**6. X-wing — the fighter**
- Chassis ▸ Hull *(fighter — tiny budget)*
- Weapons ▸ Energy *(4 laser cannons — linked-fire dial)* + Guided *(proton torpedoes)*
- Propulsion ▸ Reaction + **Warp (hyperdrive on a fighter)** ✅
- Power ▸ Generation + Storage · Defense ▸ Shields *(small)* + Armor *(titanium)*
- Enhancers ▸ Systems *(**astromech droid** — AI + in-flight repair ≈H2a)* · Sensors ▸ Detection + FC
- **Strains:** ⚠H5a S-foils *(config toggle: cruise vs attack)*; astromech = *crew-that's-a-component* (a droid slot — mostly fits Systems).
- **Insight:** validates **universal mounting** (a hyperdrive on a fighter just works) and flags **config-states** (cheap, a doctrine-switch-style dial). Droid=component vs pilot=being shows the boundary is workable.

**7. AT-AT — the walker**
- Chassis ▸ Vehicle *(large)*
- Propulsion ▸ Traction *(**walker legs** — rough-handling high, slow, **ground-only medium** ✅)*
- Weapons ▸ Energy *(heavy head lasers)* · Defense ▸ Armor *(very heavy — blaster-immune)*
- Logistical ▸ Storage *(troop bay = carrier* ⚠H6*)* · Command ▸ *(command walker)*
- **Strains:** ⚠H6 carrier. The famous "wrap the legs" weak point is **already modeled** — kill the Traction component → immobilized (component-level damage).
- **Insight:** validates Vehicle + Traction + heavy Armor + a medium constraint; its weak point falls out of the existing damage model. Clean.

**8. Jedi + lightsaber — the boundary probe**
- Weapons ▸ Melee *(an **energy** melee — Melee door, **Energy nature**: nature is cross-cutting ✅)*; wielder = Chassis ▸ Personnel
- **Strains:** the two deepest. ⚠H7 the blade **deflects blaster bolts** (a weapon that is *also* a defense); ⚠H4 **the Force** (telekinesis/precognition/mind-trick — NOT gear; it comes from the Jedi).
- **Insight:** the best **boundary probe.** The saber = gear (Melee/Energy + a "deflect" defense-effect, H7). The Force = the **being** — a **People trait** that *emits the same effects a component would* (precognition→evasion buff, telekinesis→a push effect, mind-trick→a subvert effect). This is the exact shape of the H4 plug.

### Stargate

**9. The Stargate (+ DHD) — the network/teleport probe**
- Chassis ▸ Structure *(a fixed ring)*
- Transport ▸ **network node** ⚠H8 *(dials another gate by address, opens a wormhole)* + **matter transport through it** ⚠H1
- DHD = a Command/control interface *(the dialer)*
- **Strains:** the purest H8 + H1. A gate isn't a drive (it doesn't move itself) — it's an **addressable node** that sends *other* things to another node.
- **Insight:** **reuse Pulsar's existing JumpPoints subsystem** — a Stargate is a *buildable, addressable jump-point node.* Collapses H8 into a system already in the tree.

**10. Goa'uld Ha'tak — the clean alien capital**
- Chassis ▸ Hull *(pyramid)* · Weapons ▸ Energy *(staff cannons + belly plasma)* · Defense ▸ Shields
- Propulsion ▸ Warp *(hyperdrive)* + Reaction · Power ▸ Generation *(naquadah — high source)*
- Logistical ▸ Storage *(Jaffa + death gliders = carrier* ⚠H6*)* + **ring transporter** ⚠H1 · Command ▸ *(System Lord flagship)*
- **Strains:** only H6 + H1. **Insight:** a clean alien capital — proves the categories aren't Trek/Wars-specific. Rings = H1 recurrence.

**11. Replicator — the boss fight**
- Chassis ▸ Personnel/sub-personnel *(**modular blocks** — the chassis IS the swarm)*
- Industrial ▸ **Fabrication = replicate-self** ⚠H3 *(consumes matter → copies of its own design)*
- Defense ▸ **adaptive** ⚠H2b · Weapons ▸ Ballistic/Melee *(block projectiles)*
- **reconfigurable** ⚠H5 *(blocks rearrange)* · Command ▸ distributed/hive ⚠H10
- **Strains:** the worst case — H3 + H5 + H2b + H10 at once.
- **Insight:** the stress-test's boss. Its four dials (self-fabricate, config-states, adaptive shield, hive command) are exactly the plugs below — if they exist, a Replicator is designable, which means the model is franchise-complete.

**12. Jaffa warrior — the Enhancers validator**
- Chassis ▸ Personnel
- Weapons ▸ **staff weapon** ⚠H7 *(ONE item, TWO doors: a **plasma bolt** (Energy, ranged) + a **blunt club** (Melee))*
- Defense ▸ Armor *(serpent armor + helmet)*
- Enhancers ▸ **Bio-augmentation** *(the **symbiote** grants health/healing/longevity — validates Enhancers ✅)*
- Sensors ▸ *(helmet optics)* · Logistical ▸ Storage
- **Strains:** only ⚠H7 (dual-mode weapon). **Insight:** validates Enhancers ▸ Bio-augmentation (the symbiote is *exactly* "a bio-mod that makes this soldier different") and flags H7 (multi-role components).

---

## Appendix Part 2 — The unifying insight: a shared EFFECT bus

Before the per-hole plugs, the pattern under half of them: **the same EFFECT keeps showing up from different SOURCES.** A transporter, a Force push, a tractor beam, an assimilation beam, a self-repair field, a lightsaber parry — these are *effects* (teleport, push/grab, capture/subvert, regen, deflect) emitted by different things (a component, a pilot's innate trait, a weapon firing mode, a chassis config).

**The plug that makes the others coherent: define a shared vocabulary of EFFECTS, and let any SOURCE emit any effect.**

```
  EFFECTS (the bus):  damage · capture/subvert · teleport · grab/push ·
                      regen/repair · resist-buff · evasion-buff · jam · reveal
  SOURCES that emit:  a COMPONENT (gear — the designer)
                      a WEAPON FIRING MODE (stun vs kill, kill vs capture)
                      a CHASSIS CONFIG (S-foils open → +fire arc)
                      a PEOPLE TRAIT (the being — the Force, psionics, veterancy)
```

With this, five holes stop being special cases: **H1** (teleport effect), **H4** (traits emit effects), **H7** (a weapon emitting a defense effect), **H9** (a weapon emitting capture), and the tractor beam (grab effect) are all "a source emitting an effect from the bus." You build the bus once; you never build a bespoke "transporter system" or "Force system."

And it fixes the **boundary** cleanly:
> **Gear = the designer. The being = the People system. Effects = the shared bus both emit into.**

That is the answer to H4: the designer never makes a Jedi; the **People system** carries the Force-sensitivity trait, which emits *the same effects* a component could. No parallel "powers" system, no fake component.

---

## Appendix Part 3 — Plugging every hole (best fix, by cost)

### Tier A — a dial/mode on an existing door (cheapest; no new system)

| Hole | Plug |
|------|------|
| **H1 teleport** | **Logistical ▸ Transfer "delivery mode" dial**: physical (docking/conveyor, short) vs **teleport** (instant, ranged, mass/cycle-limited, heavy power draw). Transporter, rings, beaming = the same dial at different range/mass. Emits the **teleport effect** on the bus. |
| **H2a self-repair** | **Enhancers ▸ Systems "regen" dial** — regenerate a pool (hull/armor/shield) per tick. Reuses the shield-pool regen mechanic; "what it repairs" is a dial. |
| **H3a mobile fabrication** | **Universal mounting** — let Industrial doors mount on Hull/Vehicle/Personnel, not just Structure. The engine already discovers industry by ability-blob, not host (`DESIGNER-AUDIT/06`); it's a mount-flag change. A factory ship / construction rig falls out. |
| **H3b self-replication** | **Industrial ▸ Fabrication "output = own design" mode** — consume matter, build a copy of self. Grey-goo is bounded by tech/scale caps + matter. Reuses Fabrication. |
| **H5a config-states** | **Chassis config-states** — a design carries 2–3 named configs (stat/active-component profiles), switched in-play on a cooldown. **Reuse the fleet-doctrine / ground-stance switch mechanic** verbatim, one level down. S-foils, combat/travel mode, dig-in. |
| **H7 hybrid components** | **Multi-role components** — a single design carries abilities from >1 door (already how templates work: a reactor carries EnergyGen + SensorSignature). Staff = Energy + Melee; lightsaber = Melee + a deflect effect. The designer just needs to *permit* adding a second role. |
| **H9 conversion** | **Weapons ▸ Exotic effect = capture/subvert** — "damage" resolves to a capture roll (flip owner) instead of HP. **Reuse the existing capture primitive** (region-flip / boarding-capture) at unit scale. Emits the **capture effect** on the bus. |
| **H10a crewless** | **Enhancers ▸ Systems "automation" dial** — reduces the crew requirement; at max, crew→0 (a drone). Reuses the crew term of the supply gate. |
| **H10b hive** | **Command "structure" dial** — hierarchical vs **distributed/hive** (one node's span covers many units, no per-unit command). Lose the node → the hive is disrupted (the counter). A Command dial. |
| **H11 scale extremes** | **Tune the Chassis scale dial + tech/scale caps** to span Personnel(sub-kg) → Mega(planet); verify emergent-stat formulas stay sane across ~10 magnitudes. No new mechanic. |
| **H12 exotic power** | **Power ▸ Generation "source" dial** — high-end, no-fuel sources behind tech (naquadah, zero-point). Already the source-dial concept. |

### Tier B — reuse an engine system Pulsar already has

| Hole | Plug |
|------|------|
| **H8 gate network** | **Reuse the JumpPoints subsystem.** A Stargate = a *buildable, addressable jump-point node* (Chassis ▸ Structure + a "gate" ability that registers it in a named network). Dial a destination node → traverse. The DHD = the addressing UI. Subspace relays = the sensor/comms variant (extend Detection/EW across nodes). |
| **H6 carrier / nesting** | **Reuse units-as-entities.** Logistical ▸ Storage gains a "bay" mode that holds *unit-entities* (fighters/troops/gliders); Transfer launches/recovers them. A carrier = a unit whose bay contains child units. Verify the entity model supports a unit holding child units (it should). |
| **H5b separable chassis** | **The carrier mechanic taken to the limit** — saucer separation = a design with a *detachable primary module* that is itself a mini-chassis; "separate" = the module becomes an independent unit (launch), the mothership continues degraded. Solve H6 → saucer-sep falls out + a "detach" order. |

### Tier C — a genuinely new mechanic (the real, bounded work)

| Hole | Plug |
|------|------|
| **H4 innate powers** | **The People-trait → effect-bus layer.** A commander/pilot/species carries innate **traits** that emit effects onto their crewed unit (the Force → evasion buff + push + subvert; psionics; veterancy). This is the biggest, but it's a **boundary/architecture decision**, not a parallel system — traits emit into the *same* effect bus components use. Pulsar already has commanders giving unit bonuses; this generalizes that into the shared bus. |
| **H2b adaptation** | **A new resolver rule: resistance-climbs-with-exposure.** A Shields "adaptive" dial: after taking damage of a given *nature*, resistance to that nature rises (capped, decays). Counter is already in the weapon model — **modulate weapon nature** (rotate frequencies) to stay ahead. Small, self-contained resolver addition; hooks the existing weapon-nature system. |

### The cost picture
- **12 of ~16 sub-plugs are Tier A** — a dial or mode on a door the parametric model already has.
- **3 reuse an existing subsystem** (jump points, capture, units-as-entities/doctrine-switch).
- **Only 2 are new mechanics**, and both are bounded: the **effect-bus + People traits** (H4 — the one architectural decision worth making early, because it also plugs H1/H7/H9 and defines the designer's edge) and **adaptive resistance** (H2b — a small resolver rule).

**Conclusion:** the holes do **not** threaten the 11-category model — they largely *confirm* it. The parametric approach was built to absorb "types" as dials, and that's exactly what most plugs are. The one high-value early decision is the **shared effect bus with the gear/being boundary** (build once, plug five holes, keep "the Force" honest). Everything else is a dial, a mount-flag, or reuse of a system already in the tree.
