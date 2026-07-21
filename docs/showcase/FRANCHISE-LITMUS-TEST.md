# Franchise Litmus Test — The Master Reading

**As of:** 2026-07-21 · **status: SYNTHESIS (read-only survey — no engine/data changed).** This is the
combined verdict across all four litmus battles — Geonosis (Star Wars), Deep Space 9 (Star Trek), the Ori
Supergate (Stargate), and Cold Steel Ridge / Macragge (Warhammer 40K). It answers the one question the
developer asked: *"if a battle has a Stormtrooper in it, can I actually build that Stormtrooper — the E-11
blaster and all — out of the Component Designer and the Entity Assembler?"* — pointed at 66 units across four
franchises, and boiled down to what to build, in what order, and why.

The four per-battle specs live alongside this file: `LITMUS-GEONOSIS.md`, `LITMUS-DS9.md`,
`LITMUS-ORI-SUPERGATE.md`, `LITMUS-COLD-STEEL-RIDGE.md`. Read those for the per-unit build recipes; read this
for the fleet-wide picture.

> **A note on honesty (read this — it changes the numbers).** Each battle spec was adversarially
> verified against the live engine, and the verifiers caught real errors. Where a source doc's claim was
> wrong, **this synthesis uses the corrected engine truth, not the doc's original wording** — and every
> correction below was re-grepped by hand for this reading, not taken on faith. The corrections that matter
> are called out in a box in §3 and §5, because two of them change *which* fix is highest-value. The value of
> a litmus test is exposing what breaks; the value of *this* reading is not repeating a break the verifiers
> already found.

---

## 1. THE VERDICT — does the build system pass?

**Yes, with three named exceptions — and the exceptions are the whole point.**

Of the **66 units** across the four battles, **59 (~89%) are buildable today** from the ~67 hand-authored JSON
templates the game already ships, most of them near-copies of warships and ground units already in the base
mod. Add a fistful of **cheap JSON data** (a nuclear warhead payload, a biomass material, a "no-bypass" shield
dial) and it's **61 of 66 (~92%)**. Only **5 units (~8%)** need a genuinely new engine mechanic to be
recognizable at all.

That is a strong pass. The parts bin is deep, the chassis system is genuinely universal, and a startling
amount of "the deep end of science fiction" — a shield-piercing beam, a regenerating shield pool, a cloak, a
personal deflector, a veteran-crew multiplier, a near-crewless automated hull, an Ori-scale mega-capital —
**already falls out of the shelf.**

**But the 89% comes with an asterisk you must not gloss over.** "Buildable today" means *the ship or soldier
fields and fights in a way you'd recognize* — the hull, the frame, the guns, the armor. It does **not** mean
every signature trick is modeled. Roughly **half of the buildable-today units are still missing one signature
wrinkle** — a true mass-reactive bolt round, beaming a bomb past a shield, a commander who actually buffs his
squad — and those wrinkles cluster onto a **short shared backlog** (§3). The good news is the clustering: the
gaps *repeat across battles*, so a handful of well-chosen builds light up dozens of units at once.

**The three load-bearing gaps — the ones that turn "we can't stage this" into "we can":**

1. **A station that can fight.** The game can *build* Deep Space 9 and the Ori Supergate, and can *destroy*
   them — but a station cannot *shoot back or soak a fleet's fire as a combatant*. Two of the four battles
   hinge on this. It's the single highest-value gap in the whole reading, because "a fortress that must hold
   the line" recurs in nearly every franchise.
2. **A ground weapon that carries its real punch.** The rich, multi-axis weapons (nature, armor-penetration,
   per-shot alpha) already exist *and already carry the ground-mount flag* — but the ground-unit assembler
   only reads a stripped-down weapon and ignores them. Fix that one read and the entire Imperium's signature
   guns (the bolter, the lascannon) come to life, *and* Geonosis's SPHA-T finally has to carry a reactor.
3. **Matter teleportation.** Beaming — people, cargo, and above all *a warhead through a shield* — is how the
   good guys fight in Stargate, and it has no home. It's also the one lever that makes the "unbeatable" Ori
   ship beatable.

Everything else is either cheap data, a mount-flag edit, or an honest fidelity compromise you can ship
without. And two "gaps" turn out to be the litmus test *confirming a boundary the design already drew
correctly*: the **Force** and the **Tyranid Hive Mind** are **People-system traits, not components** — you
build the Jedi's lightsaber and the Swarmlord's bonesabres, and you let the People layer carry the *being*.
Do not build a "Force component." The litmus test's real service here is proving that line holds twice.

---

## 2. THE SCOREBOARD

Each unit is scored by the rung its **chassis + body + primary weapons** need to field recognizably (the
per-battle docs' verdicts), corrected for the engine-reality fixes in §3.

| Battle | Units | Buildable today | With new JSON data | Needs engine work |
|---|---|---|---|---|
| **Geonosis** (Star Wars) | 18 | 15 | 1 | 2 |
| **Deep Space 9** (Star Trek) | 15 | 14 | 0 | 1 |
| **Ori Supergate** (Stargate) | 7 | 6 | 0 | 1 |
| **Cold Steel Ridge** (40K) | 26 | 24 | 1 | 1 |
| **GRAND TOTAL** | **66** | **59 (~89%)** | **2 (~92% cumulative)** | **5 (~8%)** |

**The 5 that need engine work** (the honest breaks):

| Unit | Battle | Why it breaks | Shared fix |
|---|---|---|---|
| Deep Space 9 (station) | DS9 | A weaponised station can't fight in a fleet battle | **Station combatant** (§3 #1) |
| The Ori Supergate | Ori | Same station-combat gap + a buildable/addressable gate | Station combatant + gate (H8) |
| Jedi Knight | Geonosis | Bolt-deflection (H7) + the Force is a People trait (H4) | People layer (not a component) |
| The Swarmlord | Cold Steel | Hive Mind is a People trait (H4) + shared-span command (H10) | People layer + command aura |
| LAAT/i gunship | Geonosis | No atmospheric air-combat layer (strafe + air-mobile insert) | **Air layer** (§3 #7) |

**The asterisk on the 59 "buildable-today."** These field and fight recognizably, but about half carry one
signature wrinkle that lives on the shared backlog: ~10 Imperial units want the real bolt/lascannon punch;
the BC-304, Ha'tak, O'Neill and Ori want beaming or self-repair; ~7 vehicles want a ground troop bay; the
Klingon ships and the Defiant want the decloak-to-fire gamble; all the Tyranids want a biomass reskin. **None
of those stop a recognizable fight** — but closing them is what turns "recognizable" into "faithful," and §3
is how you close a dozen at a time.

---

## 3. THE SHARED-COMPONENT BACKLOG — build once, unlock many

This is the highest-value section. These are the fixes that **recur across battles** — the reason the reading
is optimistic. Ranked by *(units unblocked × cheapness)*, cheapest lever first per the designer rule: **a dial
beats a new template beats engine work; name the dial before you reach for a new door.**

> **⚠️ Engine-reality corrections baked into this ranking (re-grepped for this synthesis — trust these over
> the per-battle docs where they differ):**
> - **The ground-weapon "root cause" (Geonosis + Cold Steel).** `GroundUnitAssembly.Compute` sums a unit's
>   Attack **only** from `GroundWeaponAtb` parts (`GroundUnitAssembly.cs:137`). But **seven rich unified
>   weapons already carry the `GroundUnit` mount flag** — laser (`weapons.json:29`), railgun (`:367`),
>   siege-railgun (`:445`), flak (`:533`), pulse-laser (`:880`), disruptor (`:963`), plasma-repeater
>   (`:1019`) — each with the full `WeaponProfile` axes (Nature, Penetration, PerShotEnergy). So the cleanest
>   fix is **NOT "add dials to the impoverished ground weapon"** (as Cold Steel proposed) — it's **wire the
>   assembler to read the unified weapon that's already mountable.** One read, and the bolter gets its real
>   AP/alpha *and* ground energy weapons start drawing reactor power.
> - **The SPHA-T supply gate is not real yet (Geonosis).** A ground `energy-weapon` draws **0 W**
>   (`WeaponSupply.PowerDraw_W`, `WeaponSupply.cs:73-97` returns nonzero only for the space beam atbs). So the
>   Geonosis doc's "a giant beam forces a giant reactor" is **false as written** — the power gate never trips
>   on a ground energy weapon. It becomes true *the moment* the root-cause read above lands. That's why the
>   read is the top item: it fixes two battles' marquee claims at once.
> - **Droids are crewless for free (Geonosis).** `crew-automation` has **zero `GroundUnit` occurrences** in
>   `electronics.json` — it can't even mount on a ground frame — and `Compute` never gates crew at all. A
>   battle droid is simply a native, crewless ground unit. Ignore the "crew-automation = droid" framing; there
>   is nothing to build here.
> - **The Fighter mount flag is inert (Ori).** No chassis maps to `ComponentMountType.Fighter` (the only four
>   `PartMount`s are ShipComponent/GroundUnit/Station/PlanetInstallation). A fighter is a **small ship-hull
>   using ShipComponent parts** — fighters still build today, just not via a "Fighter mount." Don't cite the
>   Fighter flag as proof of universal mounting.
> - **No anti-shield weapon needs building (DS9).** `disruptor-weapon` (Exotic, shield-bypass) already exists
>   and is a *win* — but reserve it for **true** shield-piercers (the Dominion polaron, the Ori-shield-cracker).
>   Ordinary Klingon/Cardassian disruptors are **Energy-nature** = `laser-weapon`/`pulse-laser` (half-soaked by
>   shields). This is a *mapping* fix, not a build.
> - **Sealing doesn't fire on a frozen habitable world (Cold Steel).** There is **no Cold hazard** in
>   `HazardEffectType`; `sealed-systems` negates only **Vacuum + ToxicAtmosphere** (airless/corrosive worlds).
>   On Macragge the Marine's edge is **armor/toughness, not sealing**. `sealed-systems` is still a real win —
>   on the Moon or a Venus, not on an ice ridge.

### Tier 1 — cheap and high-leverage (build these first)

| Rank | Component / fix | Kind | Battles | Units unblocked | Why it's top |
|---|---|---|---|---|---|
| **1** | **Ground-unit assembler reads the unified weapon.** Have `GroundUnitAssembly.Compute` read the already-ground-mountable weapons' `WeaponProfile` (Nature, Penetration, PerShotEnergy) **and** their power draw, instead of only `GroundWeaponAtb.Attack`. | **Engine (an assembler read — no new combat math; the kernel already has the axes)** | Geonosis + Cold Steel | The whole Imperium's signature guns (bolter, lascannon, meltagun, battle cannon, power fist), the Genestealer's rending claws — **and** it makes the SPHA-T's beam draw power so the supply gate finally bites. ~12 units. | Single read fixes the #1 Cold Steel lever *and* the false Geonosis marquee. Best ratio in the reading. |
| **2** | **Ground troop bay** — add the `GroundUnit` mount flag to a bay component so a ground vehicle carries a squad. (Units-as-entities already lets a unit own children; `troop-bay` already has a Personnel/Vehicle CarryClass dial — `storage.json:301`, currently ShipComponent-only.) | **Data (mount flag)** | Geonosis + Cold Steel | AT-TE belly, Rhino, Razorback, Land Raider — the entire APC/assault-transport class. ~5 units. | A one-word JSON edit unlocks a whole vehicle role in two battles. |
| **3** | **Ground command aura (+ synapse/decapitation variant).** A `GroundUnitCommandAtb` (or the `GroundUnit` flag on a command part) the resolver reads as a formation-wide Attack/morale multiplier; a "synapse-loss" variant *drops* the buff when the node dies. (`ship-command`/`command-berth` are ShipComponent/PlanetInstallation-only today — design-hole **H10b**.) | **Data + small resolver read** | Geonosis + Cold Steel | Clone commander, the droid control-ship decapitation, Marneus Calgar, the Tyranid Warrior's synapse, the Swarmlord's mechanical half. ~5 units + the "decapitation" drama. | Makes commanders *matter* on both ground armies; the same part serves the hero-buff and the hive-node-death. |

### Tier 2 — the load-bearing engine job (build second)

| Rank | Component / fix | Kind | Battles | Units unblocked | Why it's essential |
|---|---|---|---|---|---|
| **4** | **The weaponised / enrolled-station combatant.** Compute a station's combat value from its mounted weapons/shields (today `ShipCombatValueDB.Calculate` runs *only* in `ShipFactory.cs:144`, so a station has none), and let the `FleetDB`-keyed auto-resolver enrol a station as a stationary combatant. Prereq: add the `Station` mount flag to weapons/`deflector-array`/`missile-launcher` (one-word JSON, identical to the edit that gave `reactor` its Station flag). | **Engine (a new combat view + enrol path) + a data prereq** | DS9 + Ori (+ Cold Steel's orbital forts) | Deep Space 9 *is* this feature; the Supergate objective needs it; every defended platform/orbital gun/fortress-station across franchises. | Two battles **hinge** on it. It's not the cheapest, but it's the highest-stakes — it turns "can't stage" into "can" for two of four battles. |

### Tier 3 — high value, costlier (build as their battle comes up)

| Rank | Component / fix | Kind | Battles | Notes |
|---|---|---|---|---|
| **5** | **Matter teleportation (H1)** — a Logistical "teleport mode": instant, ranged, mass/cycle-limited, heavy power draw; and its offensive use, **beam a warhead past a shield**. | **Engine** | Ori (load-bearing) + Cold Steel (Terminator deep-strike) + DS9 (transporters) | Recurs in 3 franchises → high priority. The bomb-past-shields use is the single lever that makes the Ori fight *winnable*. |
| **6** | **Carrier / fighter bay (H6)** — a bay mode that holds and launches *child ship-units* (not just embarked ground troops). | **Engine (verify — entity model may already support ownership)** | Ori (F-302/Glider) + Geonosis (Acclamator/Core Ship) + Cold Steel (Thunderhawks) | Recurs in 3-4 battles. Medium cost; fighters fly fine as independent ships without it, so it's fidelity, not a blocker. |
| **7** | **Atmospheric air-combat layer** — an altitude band coupled to the 2D ground hex map (strafe + air-mobile troop insertion). | **Engine (the biggest single system)** | Geonosis (**load-bearing** — the LAAT) + Cold Steel (jump packs — cosmetic) | Only truly load-bearing for Geonosis, where "close air support *is* the story." Highest cost; defer until Geonosis. |

### Tier 4 — cheap data / single-battle flavor (ship opportunistically)

- **Biomass material + bio templates + hive-as-fabricator (H3)** — data reskin so the Tyranids are "grown, not
  built" (Cold Steel only; they fight fine as metal today).
- **Buildable/addressable gate (H8)** — reuse the JumpPoints subsystem so a station-chassis can be a
  player-built, dial-a-destination gate (Ori only, but load-bearing there — the FTL machinery already exists).
- **Nuclear/quantum/photon warhead ordnance tiers** — `missile-payload` designs with tuned TNT-equivalent
  (Ori, DS9). JSON only.
- **"No-bypass" shield dial** — a `deflector-array` variant that soaks exotic too, for a truly weakness-less
  shield (the Ori shield). Small additive Defense option.
- **Exotic / no-fuel power source (H12)** — a Power-generation dial with very high output, no fuel, behind
  high tech (the Asgard core). New template, no engine work.
- **Self-repair / regeneration (H2)** — an Enhancers regen dial (Ori self-heal, Carnifex, living hulls).
  Blocked on the parked per-component degraded-condition model; genuine engine work, but non-blocking.
- **Decloak-to-fire config-state (H5)** — bar weapons/shields while cloaked (Klingon BoP, Defiant, droideka
  fold, Vulture transform). Medium; the ships fight without it.
- **Overheat / suicide delivery hooks** — a plasma-gun overheat dial (Cold Steel) and a one-shot spore-mine /
  ram behavior (Cold Steel + DS9). Small.

---

## 4. THE BUILD PLAN — a phased, gated recommendation

**Build Cold Steel Ridge FIRST.** It is the most buildable-today (24/26, 92%), it exercises the three cheapest
top-leverage shared components (the weapon read, the troop bay, the command aura), its marquee units *validate*
the system (the Space Marine's augment stack, the swarm-frame horde), and its only true engine gap — the
Swarmlord's Hive Mind — is *correctly out of scope* (a People trait: you build the body, defer the mind). It's
a self-contained ground war that needs **neither** of the two most expensive systems (the station combatant,
the air layer). Building it first hardens the ground-combat assembler path that all four battles lean on.

Then **Deep Space 9 second**, because completing it needs exactly **one** engine feature — the station
combatant — and *that same feature also unlocks the Ori Supergate*. One build, two battles gated.

Each phase maps to the **cradle-to-grave chain**: every new component gets its mineral → material →
production → design → research → install → decision → loss, and each phase's **CI gate** is a
`BaseModIntegrityTests`-style "every unit in this battle's roster assembles with zero Problems," plus a
behavior assertion that proves the *decision* the new lever creates.

| Phase | Battle / theme | Build (from §3) | Cradle-to-grave rung it completes | CI gate (the "every unit builds" sensor + a behavior check) |
|---|---|---|---|---|
| **P0** | **Data-only foundation** (no engine) | Station mount flag on weapons/shields; nuclear + photon/quantum ordnance; no-bypass shield dial; biomass material + bio templates; exotic power dial | *material → production → design* rungs for the cheap stuff | `BaseModIntegrityTests`: every new design builds, base mod loads with **zero skipped entries** (gotcha #10 — check both ends of every new cost). |
| **P1** | **Cold Steel Ridge** (ground) | #1 weapon read, #2 troop bay, #3 command aura | *component → install → decision → loss* for ground combat | A `MacraggeRosterTests`: assemble every Imperial + Tyranid unit via `GroundUnitAssembly`, assert **no Problems**; assert a bolter's Nature/Penetration is set from the unified weapon; assert a commander's aura multiplies formation Attack and drops on his death. |
| **P2** | **Deep Space 9** (station) | #4 station combatant | *decision → loss* for a fixed installation | A `StationCombatTests`: a weaponised station computes a combat value, **fires on** an attacking fleet, and **soaks** its fire as an enrolled combatant (not just gets destroyed by one ship). |
| **P3** | **Geonosis** (air + armor) | #7 air layer + guided ground-missile mode | *component → decision* for air-mobile forces | A `GeonosisRosterTests` + an air-layer behavior test: a gunship strafes a ground hex and inserts a troop unit; a Hailfire's guided salvo can be intercepted. |
| **P4** | **Ori Supergate** (frontier) | #5 teleport (+ bomb-past-shields), #6 carrier bay, H8 addressable gate (reuses P2's station combatant for the objective) | *the exotic rungs* — teleport delivery, gate addressing | An `OriSupergateTests`: beam a warhead inside a shielded hull (shield bypassed); dial a player-built gate to a destination and traverse. |

**Gate discipline (from root CLAUDE.md):** one slice at a time, push, and **wait for CI green (both the `test`
and `build-client` jobs) before stacking the next slice.** CI is the only correctness gauge in the cloud
environment. A verified base is worth the ~30-minute wait.

---

## 5. DESIGNER REPORT CARD — the 12 known holes, graded per battle

From `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md §5`. For each hole: which battles hit it, and whether it's
**cosmetic** (skip it, the fight is still recognizable) or **load-bearing** (the battle needs it).

| Hole | What it is | Geonosis | DS9 | Ori | Cold Steel | Verdict |
|---|---|---|---|---|---|---|
| **H1** | Matter teleport | Cosmetic (—) | Cosmetic (transporters) | **Load-bearing** (Asgard beam / rings / bomb-past-shields) | Cosmetic (Terminator deep-strike) | **Load-bearing overall** — the top Tier-3 build; recurs in 3 franchises. |
| **H2** | Adaptive / self-repair | — | — | Medium (Ori "feels unbeatable"; Asgard*) | Cosmetic (Carnifex, living hulls) | **Non-blocking** — units field as very tough; blocked on the parked degraded-condition model. *(*Asgard self-repair is a canon reach — it's an Ori/Replicator signature; the Asgard's real edge is beaming + plasma + power.)* |
| **H3** | Self-replication / mobile fab | — | Medium (self-replicating minefield — the DS9 centerpiece, but the fleets fight without it) | — | Medium (biomass "grown, not built" framing) | **Data/framing, non-blocking** — a `biomass` material + hive-as-fabricator; the mine-swarm is a nice-to-have. |
| **H4** | Innate powers = **People trait, NOT a component** | The Force (Jedi) | — | — | The Hive Mind (Swarmlord) | **This is the correct boundary, confirmed twice — not a gap to fill in the Designer.** Build the saber/bonesabres; let the People layer carry the being. Never fake a "Force component." |
| **H5** | Config-states / separable chassis | Cosmetic (droideka fold, Vulture transform) | Medium (decloak-to-fire; saucer sep) | — | — | **Medium** — the decloak-to-fire gamble is the heart of Klingon tactics; the ships still fight. |
| **H6** | Carrier / nesting | **Load-bearing, cheap half** (ground troop bay) + cosmetic (Core Ship launch) | — | Cosmetic-to-medium (F-302 hangar) | **Load-bearing, cheap half** (Rhino/Land Raider bay) + cosmetic (Thunderhawks) | **Split** — the *ground troop bay* mount flag is cheap + load-bearing (Tier-1 #2); the *fighter-launch* is medium + cosmetic (Tier-3 #6). |
| **H7** | Hybrid weapon+defense | Cosmetic (lightsaber bolt-deflection — Jedi only) | — | — | Adjacent (the bolter's nature axis, handled by Tier-1 #1) | **Cosmetic** — one hero unit; the nature-axis half is covered by the weapon read. |
| **H8** | Gate networks | — | Mostly covered (wormhole = existing jump point) | **Load-bearing** (the Supergate *is* the objective) | — | **Load-bearing for Ori only** — but a *bounded* job (reuse JumpPoints; the FTL machinery exists). |
| **H9** | Conversion / assimilation | — | — | — | Adjacent (Tyranid biomass reprocessing → handled as H3) | **Not hit directly** — cosmetic/absent in these four. |
| **H10** | Crewless / hive-mind (shared-span command) | Droid control-ship decapitation (H10b). *Crewless = a non-issue: ground droids are natively crewless.* | — | — | Synapse / Swarmlord shared-span (H10b + H10) | **Load-bearing (command half)** — the ground command aura (Tier-1 #3). The "crewless" half needs **nothing** — ground units are crewless for free. |
| **H11** | Scale extremes | — | — | **Passed** (Ori mega-hull, moon-size gate — the budget dial reaches 100,000 t) | — | **A win, not a gap** — the budget/supply model expresses the over-power probe and keeps it beatable by strength math. |
| **H12** | Exotic / near-unlimited power | — | — | Medium (Asgard core; reactors cap ~1.25 GW/unit) | — | **Cheap new-data dial, non-blocking** — a high-output no-fuel Power source behind high tech. |

**How to read the card:** the only holes that are *load-bearing and expensive* are **H1 (teleport)**, **H8
(the Ori gate)**, and the air layer (a battle-surfaced gap outside the catalogued 12). **H4 is not a gap at
all** — it's the boundary the design already drew, and the litmus test's job was to confirm it holds (it does,
twice). Everything else is cheap data, a mount flag, or an honest compromise. The scariest-sounding capability
in the box — a self-repairing, weakness-less, moon-eating enemy — is *mostly expressible today*; it's the
humble station gun and the infantryman's bolt round that quietly break.

---

## 6. Bottom line for the next builder

The Component Designer + Entity Assembler **passes the litmus test.** Nine in ten units across four franchises
build today; the gaps repeat, so a short backlog unlocks the rest. Start with **Cold Steel Ridge** and the
three cheap ground fixes (the weapon read, the troop bay, the command aura) — they own the most units for the
least work and harden the assembler path everything leans on. Then build **the station combatant** once, and
collect **both** Deep Space 9 and the Ori Supergate. Defer the air layer to Geonosis and teleportation to the
Ori fight, where each is load-bearing. And leave the Force and the Hive Mind exactly where they belong — on the
People layer — because that's not a hole to plug, it's a line the design got right.
