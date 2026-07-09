# The Universal Component Designer — Categories, Doors & Dials

**As of:** 2026-07-09 · branch `claude/sol-playtest-earth-map-8r59j6` · **status: DESIGN-LOCKED — all 11 categories / 37 doors locked, stress-tested, holes catalogued + dispositioned, and WIRING-READY.**

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
