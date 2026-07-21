> ⚠ **SURVEY DRAFT (adversarially verified 2026-07-21).** This per-battle spec was attacked by a verifier on
> authenticity + engine-reality + completeness; it carries known load-bearing errata (e.g. the ground-vs-space
> weapon-composition gap, and battle-specific canon fixes) that are **reconciled in `FRANCHISE-LITMUS-TEST.md`**
> and corrected at build time. Treat the master doc as the authority where they differ.

# Franchise Litmus Test — The Battle of Cold Steel Ridge (Warhammer 40,000)

**As of:** 2026-07-21 · **status: SPEC (read-only survey — no engine/data changed).** A per-unit
cradle-to-grave BUILD SPEC: every unit that fought at the Battle for Macragge — Ultramarines vs Hive Fleet
Behemoth — mapped onto the components and chassis the game can **actually build today**, with an honest gap
ledger where it can't.

> **What this doc is.** Not lore. It is the answer to the developer's litmus question — *"if a battle has a
> Stormtrooper in it, can I actually build that Stormtrooper — the E-11 blaster and all — out of the Component
> Designer and Entity Assembler?"* — pointed at the hardest test case in the box. Macragge is the **materials
> probe** of the four litmus battles: two armies built on *opposite philosophies of matter itself*. The
> Imperium is the game's home turf (metal frames, power armour, guns, tanks, ships). The Tyranids are the
> edge case — an army that is **grown, not built**: no metal, no factory, no crew, one mind. If the build
> chain can stage a Space Marine AND a Tyranid horde out of the same parts bin, it can stage almost any
> ground war. The Space Marine's **bolter** is this battle's "E-11 done correctly" — get it right and half
> the Imperium falls out; find where it breaks and you've found the real edge of the ground weapon system.
>
> **How to read a unit block.** Each unit lists what makes it *itself* in canon, then the exact
> **Chassis + components** you'd assemble (citing the real template id and the file it lives in), then a
> **LEDGER** (what EXISTS, what's MISSING, what NEEDS-CHANGE), then a one-line **VERDICT**. The verdicts are
> the point — a litmus test earns its keep by exposing what *breaks*, not by pretending everything fits.

---

## 1. The battle in one paragraph

Cold Steel Ridge is a chapter of the **Battle for Macragge**, the desperate defence of the Ultramarines'
home world against **Hive Fleet Behemoth** — the first great Tyranid invasion the Imperium ever faced. The
Tyranids are a galactic swarm that arrives by the billion, eats everything living, and *reprocesses the
biomass* into more of itself. Their bio-ships blot out the sun over Macragge; **mycetic spores** rain the
swarm onto the surface; and the Ultramarines — genetically-engineered superhuman warriors in sealed
**power armour** — dig into the planet's frozen polar fortresses and hold the line while the ridge is buried
under an unending tide of claws and teeth. It is iconic because it is the purest **quality-vs-quantity**
battle in the setting: a handful of the finest soldiers ever made, each worth a hundred men, standing against
a living ocean that does not fear death and does not run out. That contrast — the elite, expensive,
individually-modelled Marine against the cheap, disposable, thousand-strong gaunt — is exactly the stress
test, because the game has to express *both extremes on the same chassis-and-components system.*

---

## 2. Force composition (the order of battle)

**The Imperium (defenders, dug in on the polar fortress):**
- **Ultramarines (Adeptus Astartes) infantry:** Tactical Squads (boltgun), Assault Squads (chainsword +
  bolt pistol, jump packs), Devastator Squads (heavy weapons — lascannon / heavy bolter / missile launcher),
  special-weapon Marines (plasma gun, meltagun), and **Terminators** in Tactical Dreadnought Armour (storm
  bolter + power fist). Led by **Chapter Master Marneus Calgar** and his command squad.
- **Astartes walkers & armour:** **Dreadnoughts** (a mortally-wounded Marine interred in a walking
  sarcophagus), **Rhino** and **Razorback** APCs, **Predator** and **Land Raider** battle tanks, **Whirlwind**
  missile-artillery.
- **Astra Militarum (Imperial Guard / Macragge PDF):** massed lasgun infantry, **Leman Russ** battle tanks,
  **Basilisk** self-propelled artillery — the cheap human line that fights alongside the Marines.
- **Fortress & fleet:** the polar **fortress-monasteries** (bunker lines on ice), and in orbit the Space
  Marine **strike cruisers / battle barges** that fought Behemoth in the void.

**Hive Fleet Behemoth (attackers, disgorged from orbit):**
- **The swarm (infantry):** **Termagants** (fleshborer bio-guns — the shooting gaunt), **Hormagaunts**
  (scything talons — the fast melee gaunt), **Genestealers** (elite melee infiltrators, murderously fast),
  **Tyranid Warriors** (bigger bio-soldiers that are also **synapse creatures** — the local "will" of the
  hive), and **spore mines** (drifting living bombs).
- **Bio-titans / heavies:** the **Carnifex** (a living battering-ram of chitin and claws).
- **The synapse web / command:** **The Swarmlord** — the Hive Mind's battlefield avatar, a synapse-node that
  makes the whole swarm move as one. *(The Hive Mind is not a unit or a component — see the Swarmlord block.)*
- **Fleet & delivery:** **Hive Ships / bio-cruisers** in orbit, and **mycetic spores** — the living
  drop-pods that vomit the swarm onto the surface.

**The setting itself** (not units, but where the fight sits in the engine): the **frozen polar ridge** is
**Rough / Ice terrain** on the planet surface map (`HexPathfinder.HexMoveMult` — Ice is *passable-rough*, so
the swarm crosses it slowed, not blocked). The cold, thin polar air is an **environmental hazard**
(`PlanetEnvironmentFactory` attrition) — which the Marines' **sealed power armour** shrugs off and unsealed
Guardsmen do not (`GroundSealAtb`, the E4 counter). And the **fortress bunkers** are real
**fortification installations** (`bunker`, `installations.json:1454`, carrying `GroundDefenseAtb`) — the ridge
line is a buildable, bombardable, defendable thing, not set dressing.

---

## 3. THE UNITS

Notation in each BUILD line: `template-id {dial: value}` → what it contributes. Every ground component below
is a real base-mod template, already registered as a start-unlocked design (`componentDesigns.json:762-1066`),
so a stock New Game can build them. The ground kit all bills to **stainless-steel + aluminium** — the same
minerals the Bunker uses — so the **cradle-to-grave chain is intact** for the Imperial parts: iron/bauxite
mined → steel/aluminium refined → part built at a colony → designed in the Component Designer → research-gated
→ assembled on a frame in the Entity Assembler → the unit fights → a component is shot off and lost.

### Read once — three mappings that apply to nearly every unit below

**(A) How a ground weapon works.** The ground weapon component (`GroundWeaponAtb`) carries four dials:
**CarryMass** (what the frame must lug), **Attack** (firepower), **Range** (in hexes), and **Mode** —
`0=Melee 1=Ballistic 2=Energy 3=Artillery` (`ground-rifle`, `installations.json:2073`). The resolver bridge
(`GroundCombatant.NatureDeliveryFor`, `GroundCombatant.cs:54`) turns Mode into a real damage *nature × delivery*:
Melee→Kinetic-contact (undodgeable), **Ballistic→Kinetic-slug** (aimed, dodgeable, armour stops it best),
**Energy→Energy-bolt** (aimed, dodgeable, but a shield only *half*-soaks it — `ShieldEffVsEnergy 0.5`,
`GroundDamageMatrix.cs:37`), **Artillery→Explosive-blast** (area, undodgeable, partly bypasses shields). Hold
that map — it is where most of the honest gaps live.

**(B) The Space Marine kit is a real stack of components, and it already exists.** This is the marquee win of
this battle. A Space Marine is not one "super-soldier" stat block — he is a *human frame wearing four buildable
augments*: **power armour** (`power-armor`, `installations.json:2398` — StrengthBonus +300 so he can lug a
weapon a bare human can't, plus a Toughness multiplier), a **sealed life-support envelope** (`sealed-systems`,
`installations.json:3134` → `GroundSealAtb`, negates vacuum/toxic/cold attrition), and a **veteran training
cadre** (`ground-training-cadre`, `installations.json:3091` — multiplies Attack *and* toughness at raise; the
Chapter's centuries of doctrine baked in). Those three parts *are* "he is superhuman," and each is earned
cradle-to-grave (a more-trained, better-armoured Marine is genuinely dearer to field — the cadre's mass scales
with the multiplier). That is the litmus test *passing loudly*: the Space Marine's defining edge is buildable
from stock parts today.

**(C) The Tyranid materials problem — "grown, not built."** Every Tyranid organism is *grown* from reclaimed
biomass aboard the hive fleet — no mines, no factories, no metal, no crew. The game's chassis and components
**all bill to `stainless-steel` + `aluminium`** (grep any template's `ResourceCost`) and are **built at a
colony's industry**. There is **no biomass mineral/material** in the game (`minerals.json` / `materials.json`
have none — verified). So the cradle-to-grave chain *structurally* fits a Tyranid (there is a resource cost, a
production step, a design, a raise, a death) but the *specific rungs are wrong*: a gaunt should bill to
**biomass**, be "produced" by a **hive** (not a colony factory), and need **no crew** (the swarm-frame already
sets CrewReq 0 — that rung is right). This is a **DATA + framing** gap, not deep engine work: add a `biomass`
mineral/material and a set of bio-flavoured templates that bill to it, and (for the "grown at the hive, not a
planet" flavour) treat a hive-ship as a mobile fabricator (design-hole **H3**). Until then, the honest
compromise is: **a Tyranid is a swarm-frame unit that bills to metal** — mechanically a perfect swarm, but its
recipe reads "steel and aluminium" instead of "regurgitated biomass." Flagged per-unit below as the
**bio-material compromise**.

---

## IMPERIUM — Infantry (chassis: human)

### UNIT: Tactical Space Marine (line infantry, chassis: human) — THE "BOLTER DONE CORRECTLY" PROBE
**AUTHENTIC CAPABILITIES:** A superhuman warrior in sealed ceramite **power armour**, carrying a **boltgun** —
a weapon that fires **mass-reactive .75-calibre self-propelled rocket-shells** (a solid round that rockets to
the target, punches *through* the armour, and *then* detonates inside). Tough, disciplined, elite, holds ground
against impossible odds; frag & krak grenades.
**BUILD:**
- Chassis `human-frame` {BaseStrength 100, BaseHP 200, Locomotion 0=Foot} (`installations.json:1994`) → the Marine's body.
- `power-armor` {StrengthBonus 300, ToughnessBonus 0.2} (`installations.json:2398`) → the power armour: raises the carry budget so he can shoulder the heavy boltgun, and hardens the whole HP pool. **This is the load-bearing component** — without it a bare human frame can't legally carry a Marine's kit (the carry gate, `GroundUnitAssembly.Compute`, refuses it).
- `sealed-systems` {Sealing 0.9} (`installations.json:3134`) → the sealed helm/armour that lets him fight on the airless/toxic/frozen ridge an unsealed Guardsman dies on (wires to `EnvironmentalResistance{Vacuum,ToxicAtmosphere}`).
- `ground-training-cadre` {TrainingMultiplier ~1.6} (`installations.json:3091`) → the Chapter's veterancy: multiplies Attack + toughness at raise (a Marine ≠ a militiaman on the same body).
- `ground-plating` {HP 150, Defense 5} (`installations.json:2143`) → the ceramite plate's flat soak (stacks on the frame + power-armour toughness).
- **The boltgun** → `ground-rifle` re-dialed {Attack ~90, Range 3, Mode 1=Ballistic} (`installations.json:2073`) → a hard-hitting, long-reach direct-fire rifle.
**LEDGER:**
- **EXISTS:** the entire *soldier* — frame + power armour + seal + veterancy + plate + a ballistic rifle are all stock, start-unlocked ground templates. The carry gate confirms the power armour is what makes the heavy kit legal. **The Space Marine's identity is buildable today.**
- **NEEDS-CHANGE — the *bolt round* itself (this battle's E-11 edge):** a bolt is **mass-reactive (explosive) AND armour-piercing AND direct-fire aimed**. The ground weapon collapses *nature* into *delivery*: Mode 1=Ballistic forces **Kinetic** nature (`GroundCombatant.cs:57`), and the only **Explosive**-nature ground mode is **Artillery** — which is *area/undodgeable*, wrong for an aimed rifle. So today's honest bolter is "a Kinetic slug rifle" — it loses the *detonate-inside* (Explosive) character. Worse, the two dials that would model *punch-through-then-burst* — **Penetration** (AP) and **PerShotEnergy** (alpha) — exist on the resolver (`GroundUnit.Penetration/PerShotEnergy`, read by `GroundDamageMatrix.ArmourSoak`) and on the *monolithic* base-mod units, but are **NOT derived by the player-assembler**: `GroundUnitAssembly.ToGroundUnitDesign` (`GroundUnitAssembly.cs:239`) never sets Penetration/PerShotEnergy, and the `GroundWeaponAtb` template has no dial for them. **Fix = decouple nature from Mode on the ground weapon (a `Nature` dial the bridge reads) + add `Penetration`/`PerShotEnergy` dials to `GroundWeaponAtb` + have the assembler read the heaviest weapon's values.** The kernel already has all three axes (`WeaponProfile` takes Nature, Penetration, PerShotEnergy) — this is exposing them on the ground weapon, not new combat math.
**VERDICT:** **BUILDABLE-TODAY** (the Marine fields recognizably from stock parts — the marquee power-armour win) — but the **bolter's mass-reactive AP round** is the load-bearing NEEDS-CHANGE: a small, well-scoped ground-weapon change (nature + penetration + alpha dials) that the ship kernel is already built for.

### UNIT: Assault Space Marine (jump infantry, chassis: human)
**AUTHENTIC CAPABILITIES:** A Marine with a **jump pack** for short rocket-assisted flight, a **chainsword**
(a motorised toothed melee blade) and a **bolt pistol** — a fast close-assault shock troop that leaps over the
line and hits in melee.
**BUILD:**
- Chassis `human-frame` + `power-armor` + `sealed-systems` + `ground-training-cadre` (the Marine stack).
- **Chainsword** → `claw-weapon` re-dialed {Attack ~60, Range 0=Melee, Mode 0} (`installations.json:3256`) → a motorised melee blade: Kinetic-contact, undodgeable (`GroundCombatant.cs:56`). Melee is fully modelled.
- **Bolt pistol** → `ground-rifle` {Attack low, Range 1, Mode 1} → the sidearm.
- **Jump pack** → `ground-locomotion` {SpeedFactor high, Locomotion→3=Hover} (`installations.json:3370`) → a fast, terrain-skipping drive (the surface stand-in for the leap).
**LEDGER:**
- **EXISTS:** the melee blade, the pistol, and a fast hover-drive — a recognizable close-assault Marine.
- **MISSING — the air layer (shared engine gap, not in the 12 holes):** a **jump pack** is a *short vertical hop over the battle*, and the ground war is a flat 2D hex surface — there is no altitude/air band (the same gap the Geonosis LAAT hit). Hover locomotion approximates "fast and skips terrain" but not "leaps a wall / drops behind the line." Cosmetic for a Macragge slugging match; load-bearing for a franchise where air-mobility *is* the tactic.
**VERDICT:** **BUILDABLE-TODAY** (as a fast melee Marine) — the true *jump* is the shared air-layer gap, a Hover compromise today.

### UNIT: Plasma / Melta Space Marine (special-weapon infantry, chassis: human) — THE RISK-DIAL PROBE
**AUTHENTIC CAPABILITIES:** A Marine carrying a **plasma gun** — a superheated-hydrogen energy weapon that
hits like a tank gun but **can overheat and cook the firer** ("Gets Hot" — a risk you take for the payoff) — or
a **meltagun** (a short-range fusion torch that vaporises armour).
**BUILD:**
- The Marine stack (frame + power-armor + seal + cadre + plate).
- **Plasma gun** → `energy-weapon` {Attack ~180, Range 3, Mode 2=Energy} (`installations.json:2775`) → directed energy: aimed, dodgeable, and a shield only *half*-soaks it (`ShieldEffVsEnergy 0.5`) — exactly plasma's "bleeds through shields."
- **Meltagun** → `energy-weapon` {Attack very high, Range 1} → a short-range, brutal energy weapon (the alpha/AP the melta is famous for wants the same Penetration/PerShotEnergy dials the bolter does — see that block).
**LEDGER:**
- **EXISTS:** the energy weapon, its half-soaked-by-shield behaviour, and the reach/attack trade.
- **MISSING — no overheat/overload risk dial:** the plasma gun's whole *character* is the **gamble** — a hot shot can damage or kill the firer. No ground weapon has a self-damage/overload dial. The ship plasma weapon tracks "Combat Heat" (`weapons.json:915`), but the ground `energy-weapon` has no equivalent. **Fix = a small `OverheatChance`/`OverloadDamage` dial** on the energy weapon that the resolver rolls on fire (a self-inflicted casualty chance). Cheap, and it recurs (any risky-power weapon).
**VERDICT:** **BUILDABLE-TODAY** (a hard-hitting energy Marine) — the plasma gun's signature **overheat gamble** is a missing risk dial (NEEDS-CHANGE, small).

### UNIT: Devastator Space Marine (heavy-weapons infantry, chassis: human)
**AUTHENTIC CAPABILITIES:** A Marine hauling a **heavy weapon** — a **lascannon** (anti-tank beam), a **heavy
bolter** (rapid-fire bolt-shells), or a **missile launcher** (krak/frag). The Squad's long-range anti-armour
punch, only carryable *because* of power armour.
**BUILD:**
- The Marine stack. The **power armour's StrengthBonus +300 is what makes the heavy weapon legal** — the carry gate would refuse a lascannon on a bare human frame, exactly as it should (`GroundUnitAssembly.Compute`, per-item weight limit). A clean validation of the augment→carry-budget rule.
- **Lascannon** → `energy-weapon` {Attack very high, Range 6+, Mode 2} → long-reach anti-armour beam.
- **Heavy bolter** → `ground-autocannon` {Attack ~150, Range 3, Mode 1=Ballistic} (`installations.json:2477`) → the rapid heavy slug-thrower (also gated to "needs a strong frame / power armour," which is authentic).
- **Missile launcher** → `ground-cannon` {Mode 3=Artillery, Range 3} (`installations.json:2626`) → indirect, area, undodgeable — a good fit for the frag missile; the *guided krak-at-one-tank* variant is the missile gap below.
**LEDGER:**
- **EXISTS:** the heavy energy/ballistic/artillery weapons, and — crucially — the **carry gate that makes power armour *matter*** (the heavy weapon is only legal on the augmented frame).
- **NEEDS-CHANGE:** the **lascannon's anti-tank punch** wants **Penetration + PerShotEnergy** (crack the plate, one big shot) — the same assembler gap as the bolter. Without it, an assembled lascannon out-damages a rifle by *volume of Attack*, not by *cracking armour a rifle bounces off*. (The monolithic base-mod Armor unit *does* get Penetration 20 / alpha 140 — so the dials work; they're just not wired to the player-assembler.)
- **MISSING — guided ground missile:** `missile-launcher` (`weapons.json:235`) is **`MountType: ShipComponent, ShipCargo`** — no `GroundUnit` flag, and `GroundWeaponAtb.Mode` has no Guided value. A krak missile that *tracks one tank* is Artillery-mode (area) today. (Same gap the Geonosis Hailfire droid hit.)
**VERDICT:** **BUILDABLE-TODAY** (a long-range heavy Marine, and a clean power-armour-carry validation) — anti-tank *quality* (penetration/alpha) is the assembler gap; a *guided* missile is a mount/mode gap.

### UNIT: Terminator (elite heavy infantry, chassis: human)
**AUTHENTIC CAPABILITIES:** A veteran in **Tactical Dreadnought Armour** — the heaviest personal armour there
is, nearly a walking tank — with a **storm bolter** (twin-linked boltgun) and a **power fist** (a crushing
energy-sheathed gauntlet). Can **teleport** onto the battlefield ("deep strike"). Slow, almost unkillable,
devastating.
**BUILD:**
- Chassis `human-frame` + `power-armor` dialed heavy {ToughnessBonus high} + a thick `ground-plating` {HP high, Defense high} + `ground-training-cadre` {high} → the "walking tank" survivability. Sealed as standard.
- **Storm bolter** → `ground-autocannon` {Mode 1=Ballistic, high rate} → the twin-linked boltgun (heavy slug volume).
- **Power fist** → `claw-weapon` {Attack very high, Range 0=Melee} → the crushing melee gauntlet (undodgeable contact; wants the alpha/penetration dial to properly *crack* what it hits).
**LEDGER:**
- **EXISTS:** the whole armoured brute — heavy augments + heavy plate + a ballistic gun + a huge melee fist. Terminators validate the *depth* of the augment stack (you can build a much tougher man on the same frame just by dialing the parts up).
- **MISSING — teleport (design-hole H1):** deep-strike **matter teleportation** has no home (catalogued H1 — the transporter/ring-transport gap; HIGH priority because every franchise has it). Non-blocking for Macragge (Terminators can also just walk or drop from a Land Raider / spore-equivalent).
**VERDICT:** **BUILDABLE-TODAY** (as a near-unkillable heavy infantryman) — **teleport deep-strike = H1**, a known hole, not a Macragge blocker.

### UNIT: Marneus Calgar (hero / commander, chassis: human) — THE COMMAND PROBE
**AUTHENTIC CAPABILITIES:** The Chapter Master of the Ultramarines — the single best warrior on the field,
in artificer armour, wielding the **Gauntlets of Ultramar** (power fists with built-in bolters). But his real
weapon is **command**: his presence steadies and stiffens every Marine around him.
**BUILD:**
- The Marine stack maxed — `power-armor` {ToughnessBonus max}, `ground-training-cadre` {TrainingMultiplier max} → the peerless-warrior stats, earned via the dearest cadre.
- **Gauntlets of Ultramar** → `claw-weapon` {Attack max, Melee} + `ground-rifle` {integral bolters} → the dual-role fists.
- Set as the **formation leader** (`GroundFormation.LeaderUnitId`) — the ground echo of a fleet flagship; leader-loss reassigns cleanly, no death spiral.
**LEDGER:**
- **EXISTS:** the peerless-warrior *body* (maxed augments + cadre) and the *leader slot*.
- **NEEDS-CHANGE — no ground command aura:** there is no ground component that lets a leader **buff his formation**. The `command-berth` (`installations.json:853`, with a `Span` dial) and `ship-command` (`storage.json:246`) are **`ShipComponent`/`PlanetInstallation` mounts — not `GroundUnit`**, and the berth is a *field-site worker*, not a combat multiplier. So Calgar today is "the best fighter who happens to be the leader," not "a leader who makes the squad around him better." **Fix = a `GroundUnitCommandAtb` (or the `GroundUnit` flag on a command part) the resolver reads as a formation-wide Attack/morale multiplier** — design-hole **H10b** (command-as-a-dial). (Same gap the Geonosis clone commander hit.) His truly *heroic* qualities (Rites of Battle, unbreakable will) are **People-system commander traits** (design-hole H4), not gear — correctly outside the Component Designer.
**VERDICT:** **BUILDABLE-TODAY** (as a maxed veteran leader) — the **command aura** that makes a Chapter Master *matter* is the missing ground-command component (H10b); his legend is a People trait (H4).

### UNIT: Imperial Guardsman (line infantry, chassis: human) — THE BASELINE CONTRAST
**AUTHENTIC CAPABILITIES:** An ordinary human soldier of the Astra Militarum / Macragge PDF — **flak armour**
(cloth-and-plate, not power armour), a **lasgun** ("flashlight" — a reliable low-power energy carbine), and
*numbers*. Fragile alone; the anvil the Marines are the hammer to.
**BUILD:**
- Chassis `human-frame` (no power armour, no cadre) → a plain man.
- **Lasgun** → `energy-weapon` {Attack ~40, Range 2, Mode 2=Energy} → a low-power energy carbine.
- **Flak armour** → `ground-plating` {HP modest, Defense low} → light body armour.
**LEDGER:**
- **EXISTS:** the whole soldier — and he is the *proof of the elite mechanic*. The Guardsman is a `human-frame` with a cheap energy weapon and light plate and **no augments**; the Marine is the *same frame* with power armour + seal + a veteran cadre. On the frozen ridge, the unsealed Guardsman also **bleeds to the cold-air hazard** (E4 attrition) the sealed Marine ignores — the quality gap is mechanical and cradle-to-grave, not a label.
- **MISSING:** nothing.
**VERDICT:** **BUILDABLE-TODAY** — the clean baseline that makes the Space Marine's augment stack *legible* by contrast.

---

## IMPERIUM — Walkers & Vehicles

### UNIT: Dreadnought (walker, chassis: walker)
**AUTHENTIC CAPABILITIES:** A mortally-wounded Marine interred in an armoured **walking sarcophagus** — one
arm a heavy gun (assault cannon / lascannon), the other a **power fist** with a built-in flamer; thick armour,
strides through fire.
**BUILD:**
- Chassis `walker-frame` {BaseStrength 400, BaseHP 1000, Locomotion 2=Walker} (`installations.json:2696`) → the striding war-body.
- **Assault cannon** → `ground-autocannon` {Mode 1, high Attack} + **lascannon** → `energy-weapon` {high} → the gun arm.
- **Dreadnought close-combat weapon** → `claw-weapon` {Attack very high, Melee} → the power fist arm.
- `ground-plating` (heavy) → the thick armour. `ground-locomotion` {RoughHandling ~1.0} → strides evenly over the broken ridge (a real edge on Rough/Ice terrain via `GroundTerrain.LocomotionTerrainMult`).
**LEDGER:**
- **EXISTS:** the walker, the heavy gun, the melee fist, the armour, and the terrain-striding edge. Shoot the locomotion component off and the Dreadnought is crippled in place — the existing component-level damage model gives you the weak-point for free.
- **MISSING:** nothing load-bearing. The "interred dying hero" is People-layer flavour (a commander/pilot record), not a component — correctly so.
**VERDICT:** **BUILDABLE-TODAY** — a clean walker + heavy-gun + melee-fist + heavy-armour assembly.

### UNIT: Rhino (armoured transport, chassis: vehicle) — THE APC PROBE
**AUTHENTIC CAPABILITIES:** The workhorse **APC** — a tracked box that carries a squad of Marines across the
field under armour, with a pintle storm bolter. Fast, cheap, everywhere.
**BUILD:**
- Chassis `vehicle-frame` {BaseStrength 800, BaseHP 1500, Locomotion 1=Tracked} (`installations.json:2547`) → the tracked hull.
- **Storm bolter** → `ground-autocannon` {Mode 1} → the pintle gun.
- `ground-plating` (medium) → the hull armour.
**LEDGER:**
- **EXISTS:** the armoured tracked vehicle and its gun.
- **MISSING — no ground troop-ferry (design-hole H6):** a Rhino's *entire job* is carrying infantry, and the troop-bay component (`troop-bay`, `storage.json:301`) is **`MountType: ShipComponent` only** — it ferries troops *from orbit on a ship*, not on a ground vehicle. A ground vehicle that carries a squad needs the **`GroundUnit` mount flag added to a bay component** (H6, "a unit whose bay holds child units" — units-as-entities already supports a unit owning children). (Same gap the Geonosis AT-TE belly bay hit.)
**VERDICT:** **BUILDABLE-TODAY** (as an armoured gun-vehicle) — its defining *troop-ferry* role is the H6 ground-bay mount-flag gap.

### UNIT: Razorback (transport + turret, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** A Rhino variant that trades some transport capacity for a **turret** — twin heavy
bolter, twin lascannon, or an assault cannon.
**BUILD:** exactly the Rhino, plus a turret weapon: `ground-cannon` {Attack high} (twin lascannon → `energy-weapon` ×2, or twin heavy bolter → `ground-autocannon` ×2).
**LEDGER:** as Rhino — the turret is stock; the **troop-ferry is the same H6 gap**.
**VERDICT:** **BUILDABLE-TODAY** (turreted gun-vehicle) — troop-ferry = H6.

### UNIT: Predator (battle tank, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** A Rhino-chassis **gun tank** — an autocannon or twin-lascannon turret plus
sponson weapons (heavy bolter / lascannon). No transport; pure firepower.
**BUILD:**
- Chassis `vehicle-frame` {heavy dial} + `ground-locomotion` {Tracked, RoughHandling high}.
- **Turret + sponsons** → `ground-cannon` {Mode 1, high Attack} (autocannon) + `energy-weapon` ×2 (lascannon sponsons).
- `ground-plating` (heavy) → tank armour.
**LEDGER:**
- **EXISTS:** the whole tank — heavy tracked frame, a main gun, sponson guns, heavy armour. A clean vehicle assembly.
- **NEEDS-CHANGE:** the lascannon's armour-crack again wants the Penetration/alpha dial on the assembled weapon (the *monolithic* Armor unit already carries Penetration 20 / alpha 140, so a code-built Predator cracks plate today; a *player-assembled* one doesn't yet).
**VERDICT:** **BUILDABLE-TODAY** — a marquee tank; anti-armour *quality* rides the shared assembler penetration gap.

### UNIT: Land Raider (heavy tank + transport, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** The Imperium's premier battle tank — sponson **twin-linked lascannons**, a hull
heavy bolter, near-impervious armour — *and* it carries Terminators into the teeth of the enemy.
**BUILD:**
- Chassis `vehicle-frame` {BaseStrength max, BaseHP max} + `ground-plating` (max Defense) → the near-impervious hull.
- **Sponson lascannons** → `energy-weapon` ×2 {very high Attack}; **hull heavy bolter** → `ground-autocannon`.
**LEDGER:**
- **EXISTS:** the heavy guns and the near-impervious hull (the frame's huge BaseHP + max armour plate is exactly the Land Raider's identity).
- **MISSING:** the **Terminator transport** = the same **H6 ground troop-bay** gap as the Rhino, but for *vehicle-class* passengers (`troop-bay` has a CarryClass dial for Vehicle vs Personnel, `storage.json` — so the concept exists on the ship side; it just needs a ground mount).
**VERDICT:** **BUILDABLE-TODAY** (as an armoured heavy-gun tank) — its *assault-transport* role is the H6 ground-bay gap.

### UNIT: Whirlwind (missile artillery, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** A Rhino-chassis **missile-artillery** platform — a rack that saturates an area with
frag/incendiary missiles from behind the line. Indirect, area-suppression fire.
**BUILD:**
- Chassis `vehicle-frame` + `ground-locomotion`.
- **Missile rack** → `ground-cannon` {Mode 3=Artillery, Range max} (`installations.json:2626`) → indirect, area, undodgeable Explosive-nature fire — an excellent fit for *area-saturation* missiles (`GroundCombatant.cs:59` maps Artillery→Explosive-blast).
**LEDGER:**
- **EXISTS:** the whole platform — a mobile vehicle firing indirect area Explosive rounds. The Whirlwind is actually the *best-fit* missile unit in this battle, because its real role (blanket an area, not track one tank) is exactly what Artillery-mode *is*.
- **MISSING (upgrade only):** a *true guided* missile (track a specific target, be shot down by point-defence, a finite salvo that saturates) is the ground-guided-weapon gap — but the Whirlwind doesn't need it; area fire is its identity.
**VERDICT:** **BUILDABLE-TODAY** — Artillery-mode is a genuine fit, not a compromise, for area-suppression missile artillery.

### UNIT: Leman Russ (Imperial Guard battle tank, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** The Guard's rugged main battle tank — a turret **battle cannon** (big HE/AP shell)
plus hull and sponson weapons; slow, heavily armoured, simple.
**BUILD:** Chassis `vehicle-frame` + `ground-cannon` {Mode 1, high Attack} (battle cannon) + `ground-autocannon`/`energy-weapon` sponsons + heavy `ground-plating`.
**LEDGER:** **EXISTS** — a clean gun tank, indistinguishable in the parts bin from the Predator (the flavour difference is dials, not templates). Penetration/alpha for the battle cannon rides the same assembler gap.
**VERDICT:** **BUILDABLE-TODAY** — the workhorse tank; a dial variant of the Predator.

### UNIT: Basilisk (Imperial Guard self-propelled artillery, chassis: vehicle)
**AUTHENTIC CAPABILITIES:** A tracked chassis carrying the **Earthshaker cannon** — long-range indirect
artillery that shells the enemy from far behind the line.
**BUILD:** Chassis `vehicle-frame` + `ground-cannon` {Mode 3=Artillery, Range 10 (max hexes), very high Attack} + light `ground-plating`.
**LEDGER:** **EXISTS** — long-range indirect Explosive-blast artillery is a native Mode. The `RealReachKm` readout (`GroundRangeTools`) even converts its hex range to real km on Macragge. Fragile up close (light armour) is authentic.
**VERDICT:** **BUILDABLE-TODAY** — a marquee validation of long-range Artillery-mode fire.

---

## IMPERIUM — Ships & Fortress

### UNIT: Space Marine Strike Cruiser / Battle Barge (capital ship, chassis: ship)
**AUTHENTIC CAPABILITIES:** The Astartes warship — bombardment cannon and lance/laser batteries, thick armour,
a **launch bay full of drop pods, Thunderhawks, and boarding torpedoes**, warp-capable. It fought Behemoth in
the void and rained the Chapter onto the surface.
**BUILD:**
- `ship-hull` (large, high Mass Budget) (`installations.json:68`) → the warship frame.
- `laser-weapon` batteries (`weapons.json:5`) + `railgun-weapon` (`weapons.json:346`, macro-cannon) + `flak-weapon` / `point-defense-mount` (`weapons.json:512` / `817`) → the gun batteries + point defence.
- `troop-bay` ×many (`storage.json:301`) → the Astartes + vehicle holds (the ship-side troop carry that works today — the drop-pod delivery).
- `reactor` + `battery-bank` + engine + `alcubierre-warp-drive` (`engines.json`) → power + sublight + warp.
- Armour: a thick `titanium-armor`/`ablative-composite-armor` layer (as the base-mod cruisers do).
**LEDGER:**
- **EXISTS:** the entire ship — this is the game's home turf (hull + component list + armour + a troop bay), essentially the `Lander Troop Transport` / the base-mod cruisers scaled up. The strike cruiser drops troops onto Macragge via the ship troop-bay exactly as designed.
- **MISSING — design-hole H6 (carrier/nesting):** *launching* Thunderhawk/drop-pod *child craft* (not just embarked troops) is the H6 verify item — the bay holds *troops* fine; spawning smaller **chassis** as independent units is the frontier.
**VERDICT:** **BUILDABLE-TODAY** — a straight capital-ship assembly; only the "launches its own smaller craft" richness is the H6 verify item.

### UNIT: Polar Fortress-Monastery / Orbital Defence (fortification, chassis: station / installation)
**AUTHENTIC CAPABILITIES:** The Ultramarines' **fortress-monasteries** on the frozen poles — bunker lines,
gun emplacements, void shields — that the defenders hold the ridge from; and orbital defence platforms.
**BUILD:**
- **Ground fortress** → `bunker` (`installations.json:1454`, carries `GroundDefenseAtb` + `GroundFootprintAtb`) placed in the polar region → hardens its region and projects defence to adjacent regions (`GroundFortification.DefenseMult`, capped +100%). This is a *real, located, bombardable* fortification — the ridge line you defend and can lose region-by-region.
- **Emplaced guns** → any `PlanetInstallation`-mount weapon (railgun/flak/laser all carry `PlanetInstallation`) → the fortress batteries.
- **Orbital defence platform** → `station-chassis` (`installations.json:1527`) + gun + reactor + `deflector-array` (void shield) → a fixed orbital fort.
**LEDGER:**
- **EXISTS:** the whole fortification concept — a bunker that fortifies its region, projects to neighbours, is captured hex-by-hex, and is destroyed by orbital bombardment (`GroundBuildings.BombardHex` — the grave rung). Void shields = the ship/station `deflector-array`.
- **MISSING:** nothing load-bearing.
**VERDICT:** **BUILDABLE-TODAY** — the fortress line is a real, defendable, losable thing; a marquee use of the fortification + capture model.

---

## HIVE FLEET BEHEMOTH — The Swarm (chassis: swarm)

> **Every Tyranid below carries the bio-material compromise from mapping (C):** mechanically these are perfect
> swarm-frame units, but their recipe reads *steel + aluminium* instead of *biomass*, and they're "built at a
> colony" instead of "grown at the hive." That's a **DATA + framing** gap (add a `biomass` material + bio
> templates; treat a hive-ship as a mobile fabricator, design-hole **H3**), not per-unit engine work — so it's
> stated once here and not repeated in every block.

### UNIT: Termagant (line swarm — shooting, chassis: swarm)
**AUTHENTIC CAPABILITIES:** A small, cheap, expendable bio-form carrying a **fleshborer** — a living gun that
spits burrowing beetle-grubs. Weak alone, terrifying in a horde of thousands.
**BUILD:**
- Chassis `swarm-frame` {BaseStrength 30, BaseHP 40, Size 1} (`installations.json:3177`) → the tiny fragile body ("almost free to build a thousand of"; a bay holds a horde because Size is 1).
- **Fleshborer** → `ground-rifle` {Attack low, Range 2, Mode 1=Ballistic} → a weak living slug-thrower.
- No armour, no augments — cheapness *is* the identity.
**LEDGER:**
- **EXISTS:** the whole shooty gaunt — the swarm-frame is *purpose-built* for this (cheap, fragile, hordeable, crewless by default). This is the game's answer to "quantity as a strategy."
- **MISSING:** only the bio-material compromise (above).
**VERDICT:** **BUILDABLE-TODAY** — the swarm-frame validates the cheap-horde archetype; the ranged bio-gun is a Ballistic ground weapon.

### UNIT: Hormagaunt (line swarm — melee, chassis: swarm)
**AUTHENTIC CAPABILITIES:** The Termagant's melee cousin — **scything talons** and a bounding, terrifying
speed that closes the gap before you can fire enough.
**BUILD:**
- Chassis `swarm-frame` + **scything talons** → `claw-weapon` {Attack ~20, Range 0=Melee, Mode 0} (`installations.json:3256`) → undodgeable contact damage.
- `ground-locomotion` {SpeedFactor very high, RoughHandling high} → the bounding sprint (and it crosses the Rough/Ice ridge fast).
**LEDGER:**
- **EXISTS:** everything — a fast, cheap, melee swarm-form. The melee is undodgeable contact; the speed is a real drive dial. The clone-vs-zerg *closing* dynamic (a fast melee swarm racing a longer-ranged shooter) is exactly what the ROE/closing model (`ApplyEngagementManeuvers`, H3 range) plays out.
- **MISSING:** bio-material compromise only.
**VERDICT:** **BUILDABLE-TODAY** — a fast melee swarm; validates locomotion + melee + the closing fight.

### UNIT: Genestealer (elite infiltrator, chassis: swarm/human)
**AUTHENTIC CAPABILITIES:** A larger, murderously fast melee predator with **rending claws** that shred armour;
infiltrates ahead of the swarm and is almost impossible to hit as it closes. The single scariest melee unit at
Macragge.
**BUILD:**
- Chassis `swarm-frame` (tough dial) or a light `human-frame` → a bigger bio-body.
- **Rending claws** → `claw-weapon` {Attack high, Range 0=Melee} → shredding melee.
- `reflex-booster` {EvasionBonus ~0.4} (`installations.json:3012`) → the "impossible to hit as it closes" dodge (aimed fire is reduced by evasion; melee/area still lands — "saturation beats dodge," `GroundDamageMatrix.IsAimed`). A genuinely good fit: shooting a charging Genestealer *is* hard.
- `ground-locomotion` {SpeedFactor high} → the sprint.
**LEDGER:**
- **EXISTS:** the fast, high-evasion, high-melee predator — a clean validation of the dodge mechanic (it beats aimed rifle fire but not a flamer/shell/melee).
- **NEEDS-CHANGE (minor):** "rending *shreds armour*" wants the Penetration dial on the assembled melee weapon — the same shared assembler gap; without it the claws out-damage by Attack volume, not by cracking plate.
**VERDICT:** **BUILDABLE-TODAY** — a marquee validation of dodge + fast melee; armour-shredding rides the shared penetration gap.

### UNIT: Tyranid Warrior (synapse creature, chassis: human) — THE SYNAPSE PROBE
**AUTHENTIC CAPABILITIES:** A bigger, tougher bio-soldier with **devourer/deathspitter** guns and **scything
talons** — but its real importance is being a **synapse creature**: a relay of the Hive Mind's will. Gaunts
near a Warrior fight with cohesion; kill the Warrior and the little ones near it **lose direction and go
feral**.
**BUILD:**
- Chassis `human-frame` (tough dial, +`ground-plating` for chitin) → the bigger bio-body.
- **Devourer** → `ground-rifle` {Mode 1, medium} + **scything talons** → `claw-weapon` {Melee} → the dual ranged/melee kit.
- Set as the **formation leader** of nearby gaunts (`GroundFormation.LeaderUnitId`).
**LEDGER:**
- **EXISTS:** the *body* (a tougher armed bio-soldier) and the *leader slot*.
- **NEEDS-CHANGE — synapse = ground command aura + decapitation (design-hole H10b):** the whole *point* of a synapse creature is that (a) it **buffs** the swarm around it and (b) **killing it degrades** them — the exact same missing mechanic as Marneus Calgar's command aura *and* the Geonosis droid-control-ship decapitation. There is no ground command component and no "leader-loss debuffs the formation" rule (leader-loss today just *reassigns*, no penalty — right for a Chapter Master's chain of command, wrong for a hive-node whose death *is* the disruption). **Fix = the same `GroundUnitCommandAtb` (H10b) formation buff, plus a "synapse-loss" variant where the buff drops when the node dies.**
**VERDICT:** **BUILDABLE-TODAY** (as a tough armed body / leader) — **synapse cohesion + go-feral-on-death** is the ground-command + decapitation gap (H10b), the swarm's version of the same hole Calgar and the droid control ship hit.

### UNIT: Spore Mine (drifting bomb, chassis: swarm) — THE ONE-SHOT PROBE
**AUTHENTIC CAPABILITIES:** A cheap, brainless, gas-filled living **bomb** that drifts toward the enemy and
**detonates on contact**, then is gone. Disposable area denial.
**BUILD:**
- Chassis `swarm-frame` {cheapest dial} → the near-free floating body.
- **Detonation** → `ground-cannon`/`claw-weapon` re-dialed {Mode 3=Artillery or Melee, high one-hit Attack} → an area/contact Explosive burst.
**LEDGER:**
- **EXISTS:** a dirt-cheap swarm body with an area/contact explosive — recognizable as a mine.
- **MISSING — no one-shot / suicide delivery:** a spore mine's identity is **detonate once, then remove itself** — a self-consuming attack. The resolver has no "suicide" weapon behaviour (fire once → deal area damage → destroy the firing unit). **Fix = a small resolver hook: a `OneShot`/`Suicide` weapon flag that, on firing, applies its damage and removes the unit.** Small engine work (a delivery behaviour, not new damage math). The "drifts on the wind" flight is the shared air-layer gap (Hover stand-in on the surface).
**VERDICT:** **BUILDABLE-WITH-NEW-DATA** (leaning small-engine) — a cheap explosive swarm body is buildable today; the **detonate-and-die** behaviour is a small new delivery hook.

---

## HIVE FLEET BEHEMOTH — Bio-Titans & Command

### UNIT: Carnifex (bio-walker heavy, chassis: walker)
**AUTHENTIC CAPABILITIES:** A living battering-ram — tons of chitin and muscle, **crushing claws / scything
talons** and a **bio-cannon**, near-unstoppable in a charge, regenerates wounds. The swarm's answer to a tank.
**BUILD:**
- Chassis `walker-frame` (bio, heavy dial) (`installations.json:2696`) → the huge striding body.
- **Crushing claws** → `claw-weapon` {Attack very high, Melee}; **bio-cannon** → `ground-cannon` {Mode 1 or 3}.
- `ground-plating` (heavy, chitin) → the armoured shell. `ground-locomotion` {RoughHandling high} → charges across the ridge.
**LEDGER:**
- **EXISTS:** the whole bio-tank — a walker with a huge melee + a heavy gun + thick armour. Mechanically a Dreadnought's twin (chassis + heavy melee + gun + armour), which is fine — a walking bio-heavy *is* a mech in the parts bin.
- **MISSING — regeneration (design-hole H2):** the Carnifex **heals wounds** mid-fight — a self-repair pool over time. Catalogued H2 (adaptive/self-repairing defence), no engine support today. Non-blocking (it fields as a very tough walker; it just doesn't heal).
**VERDICT:** **BUILDABLE-TODAY** (as a bio-heavy walker) — **regeneration = H2**, a known hole, non-blocking.

### UNIT: The Swarmlord (Hive Mind avatar / synapse hero, chassis: walker) — THE HIVE-MIND BOUNDARY PROBE
**AUTHENTIC CAPABILITIES:** The Hive Mind's greatest battlefield avatar — four **bonesabres** (murderous
melee), immense psychic power, and above all it **is the will of the swarm made flesh**: it coordinates the
entire Tyranid army, and its presence makes every organism nearby fight as one perfect organism. The single
hardest unit in this battle to model honestly — the Tyranid twin of the Jedi.
**BUILD (the buildable half):**
- Chassis `walker-frame` (bio, large) + **bonesabres** → `claw-weapon` {Attack max, Melee} → the cutting is buildable (a huge melee bio-body).
- `ground-plating` (heavy) + `reflex-booster` → tough and hard to hit.
**LEDGER — this is where it breaks, honestly:**
- **EXISTS:** the *body* — a giant, hard-hitting, tough melee walker.
- **MISSING — the HIVE MIND is not gear (design-hole H4) and is not a component:** the Swarmlord's defining trait is **being the mind of the army** — synapse coordination across the whole swarm, psychic domination. Per the locked designer boundary (mapping B, §3 of `COMPONENT-DESIGNER-CATEGORIES.md`), **innate/psychic "will" is a People-system trait, never a component** — the same boundary the Jedi's Force draws. You must **not** fake a "hive mind component."
- **MISSING — hive/shared-span command (design-hole H10):** even the *mechanical* half — one mind driving many bodies, and the whole army faltering if that mind is broken — is the **hive / shared-span command** model (H10) layered on top of the H10b ground-command aura. A single node whose loss disrupts *the entire formation network*, not just one squad.
**VERDICT:** **NEEDS-ENGINE-WORK** — the *cutting* is buildable today, but the two things that make the Swarmlord the Swarmlord (the **Hive Mind = H4/People-trait**, and **shared-span hive command = H10**) are neither in the parts bin nor should the mind ever be. This is the litmus test doing its job: it names the exact edge of the designer, and confirms the boundary — build the swarm's *bodies*, let the People/Command layer carry the *one mind*.

---

## HIVE FLEET BEHEMOTH — The Fleet (chassis: ship)

### UNIT: Hive Ship / Bio-Cruiser (capital bio-ship, chassis: ship) — ALREADY EXISTS IN-GAME
**AUTHENTIC CAPABILITIES:** A living warship — grown, not built — armed with **bio-plasma and symbiotic
living weapons**, regenerating chitin hull, and it carries and spawns the swarm. The heart of the hive fleet
in orbit over Macragge.
**BUILD:** the base mod already ships this as **`default-ship-design-hive-cruiser` ("Kithrin Hive Cruiser," `shipDesigns.json:829`)**: `ship-hull-heavy` + `disruptor-weapon` ×3 (Ion Disruptor — the exotic anti-shield "living weapon") + `deflector-array` ×2 (the regenerating shield/hull stand-in) + sensor + warp drives + reactors, armoured in titanium.
**LEDGER:**
- **EXISTS:** the whole bio-cruiser, as a real, pre-built base-mod ship design. The `disruptor-weapon` (anti-shield exotic) is a good fit for a corrosive living weapon.
- **MISSING:** the *living/regenerating* hull is the H2 self-repair hole (cosmetic here); the bio-material framing (mapping C) applies to the hull material.
**VERDICT:** **BUILDABLE-TODAY** — literally already a base-mod ship; the swarm's orbital arm needs no new work.

### UNIT: Mycetic Spore / Spore Lander (drop-ship, chassis: ship) — ALREADY EXISTS IN-GAME
**AUTHENTIC CAPABILITIES:** A living **drop-pod** — a giant spore fired from orbit that slams into the surface
and disgorges a brood of the swarm. The delivery mechanism that puts the horde onto Cold Steel Ridge.
**BUILD:** the base mod already ships this as **`default-ship-design-spore-lander` ("Kithrin Spore Lander," `shipDesigns.json:849`)**: `ship-hull-heavy` + `troop-bay` ×2 (the horde holds — swarm-frame units are Size 1, so a bay carries hundreds) + warp + reactors, armoured.
**LEDGER:**
- **EXISTS:** the whole drop chain — the spore-lander loads swarm-frame units at the hive, wins the orbit, and lands them to invade (the ship `troop-bay` orbital-delivery path that works today).
- **MISSING:** *precision* spore drops (deep-strike onto a chosen hex) touch the H1 teleport edge; bulk delivery works today.
**VERDICT:** **BUILDABLE-TODAY** — literally already a base-mod ship; the surface-delivery of the swarm is built.

---

## 4. The gap ledger (everything this battle surfaced)

| # | Gap (what broke) | Which units hit it | Kind | Proposed home / fix | Hole # |
|---|------------------|--------------------|------|---------------------|--------|
| C1 | **Ground weapon collapses NATURE into DELIVERY** — a direct-fire *Explosive/AP* weapon has no home; Mode 1=Ballistic forces Kinetic nature, and the only Explosive mode (Artillery) is area/undodgeable. So the **bolter's mass-reactive round**, and every "AP rocket-rifle," reads as a plain slug. | Bolter (Tactical/Assault/Terminator), lascannon, deathspitter | **NEEDS-CHANGE (data + small bridge)** | Add a **`Nature` dial** to `GroundWeaponAtb` (decouple from Mode) that `GroundCombatant.NatureDeliveryFor` reads — the kernel `WeaponProfile` already takes Nature independently. | **H7-adjacent** (nature axis) |
| C2 | **Assembled ground weapons carry no Penetration / PerShotEnergy** — `GroundUnitAssembly.ToGroundUnitDesign` (`GroundUnitAssembly.cs:239`) never sets them; the `GroundWeaponAtb` template has no dial. So a *player-assembled* AP/melta/power-fist can't crack armour a rifle bounces off (the monolithic base-mod units already can). | Bolter, lascannon, meltagun, power fist, rending claws, battle cannon | **NEEDS-CHANGE (data + assembler read)** | Add `Penetration`/`PerShotEnergy` dials to `GroundWeaponAtb`; have `Compute`/`ToGroundUnitDesign` read the heaviest weapon's values into the design (they already flow to the resolver via `GroundUnit`). | — (assembler) |
| C3 | **No ground command aura / synapse** — no `GroundUnit`-mountable command component that *buffs* a formation, and no "leader-loss debuffs the swarm." `command-berth`/`ship-command` are ShipComponent/PlanetInstallation only. | Marneus Calgar, Tyranid Warrior, the Swarmlord | **NEEDS-CHANGE (mount flag + resolver read)** | A `GroundUnitCommandAtb` (or `GroundUnit` flag on a command part) read as a formation-wide Attack/morale multiplier; a **synapse-loss** variant drops the buff when the node dies. | **H10b** |
| C4 | **The Hive Mind is not a component** — one mind driving the whole army, psychic domination. | The Swarmlord (and every synapse creature's "will") | **NEEDS-ENGINE-WORK (People + Command)** | The *psychic will* is a **People-system trait** (never a component, the H4 boundary); the *mechanical* "one mind, many bodies, breaks if the mind dies" is **hive / shared-span command** (H10) on top of C3. Do **not** build a "hive-mind component." | **H4 + H10** |
| C5 | **No ground troop-ferry** — a *ground vehicle* carrying infantry; `troop-bay` (`storage.json:301`) is `ShipComponent`-only. | Rhino, Razorback, Land Raider (Terminator transport) | **NEEDS-CHANGE (mount flag)** | Add `GroundUnit` to a bay component so a vehicle holds child units (units-as-entities supports a unit owning children; troop-bay already has a Personnel/Vehicle CarryClass dial). | **H6** |
| C6 | **No atmospheric AIR layer** — a unit that operates *above* the 2D ground surface (short vertical hop / drift). | Assault Marine jump pack; spore-mine drift; (gargoyles) | **NEEDS-ENGINE-WORK** | A minimal altitude/air band coupled to the ground hex map. Hover locomotion is the surface stand-in. (Same gap Geonosis's LAAT hit — a cross-battle need.) | — (new) |
| C7 | **"Grown, not built" — no biomass material / hive fabrication** — every Tyranid bills to `steel + aluminium` and is "built at a colony"; there's no bio material and no "grown at the hive." | Every Tyranid ground unit | **BUILDABLE-WITH-NEW-DATA (+ H3 framing)** | Add a `biomass` mineral/material + bio-flavoured templates that bill to it; treat a hive-ship as a **mobile fabricator** (H3) so units are "grown" off-colony. The chain *structure* fits; the *rungs* are reskinned. | **H3** |
| C8 | **No overheat / overload risk dial** — a weapon whose *gamble* is that a hot shot can hurt the firer. | Plasma-gun Marine | **NEEDS-CHANGE (small dial)** | A `OverheatChance`/`OverloadDamage` dial on the energy weapon the resolver rolls on fire (a self-inflicted casualty chance). Recurs (any risky-power weapon). | — |
| C9 | **No one-shot / suicide delivery** — fire once, deal area damage, remove self. | Spore mine (kamikaze) | **NEEDS-ENGINE-WORK (small hook)** | A `OneShot`/`Suicide` weapon behaviour: apply damage on fire, then destroy the firing unit. A delivery behaviour, not new damage math. | — |
| C10 | **Teleport deep-strike** — instant matter-transport onto the field. | Terminators (deep strike); precision spore drops | **NEEDS-ENGINE-WORK** | Design-hole H1 (transporter/ring/gate). HIGH-priority recurring hole; non-blocking here (Terminators can walk / ride / drop). | **H1** |
| C11 | **Self-repair / regeneration** — heal a wound pool mid-fight; living regenerating hull. | Carnifex; Hive Ship hull | **NEEDS-ENGINE-WORK** | Design-hole H2 (adaptive/self-repair) — an Enhancers ▸ Systems regen dial. Non-blocking (units field as very tough). | **H2** |
| C12 | **Carrier launch of child craft** — a hull that *launches* smaller chassis (not just embarked troops). | Strike cruiser (Thunderhawks/drop pods); spore-lander broods | verify (H6) | Confirm a bay can hold + launch/recover *child units*; the entity model should support it. | **H6** |

**The shape of the gaps:** the two most *load-bearing* are **C1+C2 (the bolter done correctly)** — decouple
nature from delivery and expose penetration/alpha on the assembled ground weapon, so the Imperium's signature
AP guns actually crack armour — and **C3+C4 (command & the hive mind)** — the ground command aura + synapse
decapitation, and the correct boundary that the *mind* is a People trait. **C5 (ground troop bay)** and **C6
(air layer)** are cross-battle needs (Geonosis hit both). The rest are cheap dials (C8 overheat), small hooks
(C9 suicide), catalogued holes (C10 H1, C11 H2), a verify item (C12 H6), and the Tyranid **data reskin** (C7).

---

## 5. Verdict — how completely can the game stage Cold Steel Ridge today?

**Headline: about 90% of the order of battle is buildable-today from stock templates — and this battle scores
*higher* than you'd fear on its two scariest units (the Space Marine and the Tyranid horde), because the exact
components that make each one *itself* already exist.** The Space Marine is the marquee win: `power-armor` +
`sealed-systems` + `ground-training-cadre` are real, start-unlocked, cradle-to-grave components, and stacked on
a `human-frame` they *are* "he is superhuman" — the carry gate even makes the power armour *matter* (it's what
lets him shoulder a heavy weapon a Guardsman can't). The Tyranid horde is the other win: the `swarm-frame` is
purpose-built for cheap, fragile, crewless, thousand-strong bio-forms, and the whole quantity-vs-quality
contrast — the elite augment-stacked Marine against the disposable swarm-body — falls straight out of the same
chassis-and-components system. The tanks, walkers, artillery, fortress bunkers, and *both fleets* (the Hive
Cruiser and Spore Lander are literally already base-mod ship designs) are home turf.

**What's load-bearing vs. cosmetic:**
- **Load-bearing (build these to stage the battle *recognizably*):**
  1. **The bolter, done correctly (C1 + C2)** — this is the battle's "E-11" bar. Decouple weapon *nature* from
     *delivery* on the ground weapon, and expose *penetration/alpha* on the player-assembler, so a bolter is a
     mass-reactive AP round and a lascannon cracks a tank. Both are *data + small bridge* changes — the ship
     combat kernel already has all three axes; they're just not surfaced on the ground weapon. This is the
     single highest-value fix, and it lights up the entire Imperium.
  2. **Command & synapse (C3 + C4)** — a ground command aura (H10b) plus the synapse "buff-and-decapitate"
     variant. Without it, Marneus Calgar is just a good swordsman and a Tyranid Warrior is just a big body — the
     command layer that makes *both* armies more than the sum of their units is missing. The *psychic* half of
     the hive mind is correctly a People trait (don't fake it as a component — the boundary this battle
     confirms, exactly like the Jedi's Force).
  3. **Ground troop bay (C5)** — a mount-flag fix so the Rhino/Land Raider can carry a squad on the surface
     (shared with Geonosis's AT-TE). Cheap, unlocks a whole class of vehicle.
- **Cosmetic / non-blocking (skip for a playable first pass):** the air layer (C6 — jump packs and drifting
  spores work as Hover stand-ins on the surface), overheat (C8), suicide mines (C9), teleport deep-strike
  (C10/H1 — they can walk or drop), regeneration (C11/H2), and carrier launch (C12/H6). All improve fidelity;
  none stop a recognizable fight on the ridge.
- **The Tyranid asterisk (C7 — "grown, not built"):** the swarm *fights* perfectly today; its **recipe** is
  the honest wart — it bills to steel and aluminium and is built at a colony, not grown from biomass at the
  hive. This is a **data reskin** (a `biomass` material + bio templates + the hive-as-mobile-fabricator
  framing, H3), not engine work — the cradle-to-grave *chain* is intact, only its rungs are wearing the wrong
  material.
- **The honest boundary — the Swarmlord (C4):** you can field a giant, bonesabre-wielding melee monster today,
  but the **Hive Mind** is correctly *outside* the Component Designer — it's the Tyranid Force, a People-system
  trait. The litmus test's real value here is confirming that boundary a second time: build the swarm's
  *bodies*; let the People/Command layer carry the *one mind*.

**Shortest path to "playable Cold Steel Ridge":** (1) do the two data+bridge fixes that own the Imperium —
**C1 nature-dial + C2 penetration/alpha on the assembled ground weapon** (the bolter done correctly); (2) add
the two mount-flag/aura fixes — **C3 ground command aura (+ synapse-loss)** and **C5 ground troop bay** — each
of which also pays off in the other litmus battles; then (3) reskin the Tyranids with a **biomass material
(C7)** so "grown, not built" reads true. That sequence turns a ~90%-buildable battle into a fully recognizable
one, and it defers the Hive Mind (like the Jedi's Force) to the People system where it belongs — exactly where
the litmus test says it should live.
