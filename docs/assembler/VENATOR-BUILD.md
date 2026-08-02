# The Venator, built in the Entity Assembler — a franchise litmus

> **Why this doc exists.** The first pass at a Venator preset was hand-waved (8 turbolasers, "~52" point-defense,
> loose counts, missing whole systems). This is the do-it-properly version: the Venator's **actual** systems from
> canon, each mapped to a real assembler component at the **real count**, with an honest verdict on what the
> engine can and can't yet express. It doubles as the catalog check — building a real capital ship is how you find
> out which component *types* are missing.
>
> **The test behind it** (`docs/NORTH-STAR-VISION.md`): can the designer + assembler let a player stage *specific
> aspects* of the great sci-fi universes? A Venator is a hard case because it's a **carrier first, a gunship
> second** — and the carrier rung is exactly where the engine has a gap.

## The canonical Venator (sources at the bottom)

| System | Real count |
|---|---|
| Heavy dual turbolaser turrets (DBY-827) | **8** |
| Medium dual turbolaser cannons | **2** |
| Point-defense laser cannons | **52** |
| Heavy proton torpedo tubes | **4** |
| Tractor beam projectors | **6** |
| Starfighters (192 V-wing/V-19 + 192 Eta-2 + 36 ARC-170) | **420** |
| LAAT/i gunships | **40** |
| AT-TE walkers | **24** |
| Crew | **7,400** |
| Hyperdrive | Class 1 |
| Length | 1,137 m |

## The catalog was missing three types — now added

Building the real ship surfaced three component types the bench didn't have. All three are now in the roster:

| Added component | Why the Venator needs it | Honesty (does the engine read it?) |
|---|---|---|
| **Medium Turbolaser** | the 2 medium dual turbolasers — a mid-weight main gun between the PD screen and the heavy batteries | **LIVE** — energy weapon, feeds Firepower like any other |
| **Tractor Beam Projector** | the 6 tractor beam projectors | **PENDING** — grips/drags for docking, salvage, capture, but capture is still a bare ownership flip (an open ruling) and salvage isn't built (`SpawnWreck` is a stub) |
| **Vehicle & Troop Bay** | the 40 gunships + 24 walkers + embarked troops | **LIVE** — `GroundBayAtb` → `GroundTransport` is the invasion chain the game actually runs |

## A second pass — making the canon ship combat-viable

Canon-accurate isn't the same as fightable. Building the real Venator exposed two ways it *couldn't actually
fight*, both fixed by **adding** parts (not changing the canon armament):

| Problem the tool flagged | Fix (a part added to the catalog) |
|---|---|
| **It couldn't see as far as it shot** — 4 sensors reached 170 km, but the torpedoes fly 400 km. You'd be firing blind past your own vision. | **Long-Range Sensor** (deep-search array, 450 km) ×2 → detection **470 km**, now past every gun. The ruler's grey Detection bar becomes the longest one. |
| **5 minutes of ammunition** — 52 point-defense guns + torpedoes drained 5 magazines in ~5 min of sustained fire. | **Ammo Bunker** (deep magazine, 300 t) ×5 → **61 minutes**. Also tuned point-defense to burst-fire consumption (0.5 → 0.3 t/min), since 52 flak guns don't run full-auto continuously. |

Both are *live* systems (detection is a real reader; ammo is the real dry-magazine gate), so closing these gaps
makes the ship genuinely combat-worthy, not just paper-accurate.

**A third fix was a readout, not a part.** The tool showed "Fuel: 67 h" as if it were the deployment limit — but
in Newtonian flight a patrol ship *coasts*, burning propellant only to change course. 67 h is a **maneuvering (Δv)
reserve**, not a deployment clock. So endurance was split into the two clocks that actually govern a patrol:
**Deployment** (~500 days on station — capped by the reactor fuel core, since hydroponics makes food a closed
loop) and **Maneuver fuel** (the Δv reserve, spent in bursts). It's the nuclear-carrier distinction: steam for
years on the core, limited by stores — and with a farm aboard, not even by stores. *(The escort, with no
hydroponics, comes out provisions-limited at ~98 days — the limiter swaps with the build.)*

## The full mapping — every Venator system → a component → the count

| Venator system | Assembler component | Count | Reaches the sim? |
|---|---|---|---|
| Heavy dual turbolaser turrets | Heavy Turbolaser Battery | 8 | ✅ Firepower |
| Medium dual turbolasers | Medium Turbolaser *(new)* | 2 | ✅ Firepower |
| Point-defense laser cannons | Point-Defense Battery | 52 | ✅ Firepower + saturation (the flak screen) |
| Proton torpedo tubes | Missile Launcher | 4 | ✅ Firepower; guided → the only weapon PD can intercept |
| Tractor beam projectors | Tractor Beam Projector *(new)* | 6 | ⚠ **PENDING** |
| Starfighter wing (420) | Fighter Bay | 6 (×70) | ⚠ **PENDING** — strike-launch has no live consumer |
| Gunships + walkers (64) | Vehicle & Troop Bay *(new)* | 4 (×16) | ✅ **LIVE** — lands on a surface |
| Hyperdrive | Warp Drive | 1 | ✅ FTL cruise + sustain draw |
| Sublight engines | Ion Drive | 4 | ✅ acceleration · Δv · evasion |
| Deflector shields | Shield Generator | 5 | ✅ Shield pool + regen |
| Hull armor | Composite Armour Plating | 20 | ✅ Toughness |
| Reactors | Capital Reactor | 4 | ✅ powers the guns; loud on sensors |
| Heat rejection | Radiator Bank | 5 | ✅ clears the turbolaser heat (or the guns throttle) |
| Propellant | Fuel Tank | 3 | ✅ Δv + fuel endurance |
| Ammunition | Magazine | 5 | ✅ ammo endurance |
| Life support (7,400 crew) | Provisions Store + Hydroponics Bay | 4 + 6 | ✅ crew endurance; hydroponics = a closed food loop |
| Sensors | Sensor Array | 2 | ✅ detection range |
| Bridge towers (two) | Command Bridge | 2 | ✅ seats a commander (the admin-scope dial is dead) |

## What the assembler says when it builds one

Load the **▲ Venator-class carrier** preset (host = Warship):

| Readout | Value |
|---|---|
| Hull mass | 17,468 / 20,000 t ✓ |
| Power | 398 / 480 MW ✓ |
| **Firepower** | **2,236 dmg/s** (8 heavy + 2 medium turbolasers + 52 PD + 4 torpedoes) |
| **Toughness** | **393** |
| Shields | 7,500 J (+375/s) |
| Evasion | 4% (a slow capital ship) |
| FTL | warp-capable; 15 MW to sustain |
| **Deployment** | **~500 d (~1.4 yr) on station** — capped by the reactor fuel core; food is a closed loop (hydroponics) |
| Maneuver fuel | 67 h of full burn · Δv 3.9 km/s — a reserve spent in bursts (you coast between) |
| **Detection** | **470 km** — reaches past the 400 km torpedoes (see-first) |
| Crew provisions | **∞** — hydroponics feeds 1,200 ≥ 789 crew (closed loop) |
| Heat margin | +20 MW (radiators clear it) |
| **Ammo** | **61 min** of sustained fire (was 5 — fixed with ammo bunkers) |
| Cost | ~24k build-points · ~2.7M credits · ~19k t across 10 materials |

## The litmus verdict — what's real, what's pending

**Expressible today, reaching the resolver:** the hull, all four weapon types at the right counts, the armor, the
shields, the reactors and their heat load, the FTL drive, the fuel and Δv, the ammo and crew endurance, the
sensors — **and the ground assault**. A Venator can be built as a warship cradle-to-grave right now, and it can
**land its walkers, gunships, and troops** on a planet, because that invasion chain is live.

**The two honest gaps, both flagged in the tool:**
1. **The starfighter wing (420 fighters) can't sortie.** Strike-launch is a pending output — the fighters embark
   but no live consumer flies them off the deck in space combat. This is the single biggest gap for expressing a
   *carrier*, and it's the same dead output the outputs verification caught (`ParasiteLauncherReady`, 0 emitters).
2. **The tractor beams have no battlefield payoff yet.** Docking, salvage, and capture are pending/open rulings.

So the Venator is the perfect stress test: **everything that makes it a warship works; the thing that makes it a
*Venator* — a flight deck launching hundreds of fighters — is the one rung still to build.** Fixing #1 (a
strike-bay component whose launch output has a live reader) is the highest-value carrier-enabling change.

## Two honest caveats on the model

- **The counts are canonical; the per-part *numbers* (mass, damage, power) are the assembler's own tuning**, not
  Star Wars figures — they exist to make the budgets and gates behave, not to claim a turbolaser's real yield.
- **Crew is abstracted.** The tool computes ~767 crew from components; the real Venator runs 7,400. The tool
  models the *ratio* (this ship needs a lot of people), not the headcount.

---

*Sources: [Venator-class Star Destroyer — Wookieepedia](https://starwars.fandom.com/wiki/Venator-class_Star_Destroyer/Legends)
· [Venator-class Star Destroyer — Star Wars RPG (FFG) Wiki](https://star-wars-rpg-ffg.fandom.com/wiki/Venator-class_Star_Destroyer)
· [Venator-class Star Destroyer — The Clone Wars Wiki](https://clonewars.fandom.com/wiki/Venator-class_Star_Destroyer).
Design/reference only — nothing here is wired into the engine, and no franchise assets are reproduced. Built 2026-08-02.*
