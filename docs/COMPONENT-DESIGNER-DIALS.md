# The Component Designer — Dial Specifications (living doc)

**As of:** 2026-07-09 · the lowest-level spec: for each of the ~37 doors (`COMPONENT-DESIGNER-CATEGORIES.md`), the actual **dials** a player turns, the **derived stats** they produce, and the **dependencies** they require. Locked door-by-door; this doc is updated every time a decision is locked in.

**The governing rule:** *a dial is only real if it moves a number the simulation reads.* No cosmetic knobs. Every dial bottoms out in an emergent stat a resolver consumes, and every benefit shows a cost (the `CONVENTIONS §16` transparency rule).

> **⚙ WIRING-READY (2026-07-09).** Every category now carries a self-contained **⚙ Wiring Dossier** at the end of its section (`⚙ 1`…`⚙ 11`). **To wire a category, that dossier is the only reference you need** — it consolidates, verified against the live engine (file:line): the complete dial → derived-stat → engine-wire table (with the essence-extension dials folded in as first-class rows), the resolver/system insertion points (from `AUTO-RESOLVER-ANATOMY.md`, adapted), the prerequisite fixes & dead stubs, the design essence captured inline (so the five merged strategic docs aren't needed), the §0g Reachable/Mirrored/Observable stamp, the cross-category shared state, and the holes the category owns. The cross-cutting appendices (📎 Main-Merge Delta, 🧭 Essence Extensions) and `AUTO-RESOLVER-ANATOMY.md` remain the *origin/rationale*; their per-category content is now **distributed into the dossiers**, which are the wiring authority.

## Progress

| # | Category | Door | State |
|---|----------|------|-------|
| — | *(framework)* | Universal dials + Emergent-constraint (physical budget) model | 🔒 **LOCKED §0** |
| 1 | Weapons | **Energy** | 🔒 **LOCKED §1.1** |
| 2 | Weapons | **Ballistic** | 🔒 **LOCKED §1.2** |
| 3 | Weapons | **Melee** | 🔒 **LOCKED §1.3** |
| 4 | Weapons | **Guided** | 🔒 **LOCKED §1.4** |
| 5 | Weapons | **Exotic** | 🔒 **LOCKED §1.5** (bioweapon + SWAY effects settled by the essence pass) |
| 6 | Propulsion | **Reaction** (Newtonian main-drive) | 🔒 **LOCKED §2.1** |
| 7 | Propulsion | **Traction** (surface locomotion) | 🔒 **LOCKED §2.2** |
| 8 | Propulsion | **Fluid** (atmospheric + naval) | 🔒 **LOCKED §2.3** |
| 9 | Propulsion | **Warp** (FTL — warp bubble + jump) | 🔒 **LOCKED §2.4** |
| 10 | Propulsion | **Exotic** (reactionless / gravitic) | 🔒 **LOCKED §2.5** |
| 11 | Sensors | **Detection** | 🔒 **LOCKED §3.1** |
| 12 | Sensors | **Survey** | 🔒 **LOCKED §3.2** |
| 13 | Sensors | **Fire Control** | 🔒 **LOCKED §3.3** |
| 14 | Sensors | **Electronic Warfare** | 🔒 **LOCKED §3.4** |
| 15 | Power | **Generation** | 🔒 **LOCKED §4.1** |
| 16 | Power | **Storage** | 🔒 **LOCKED §4.2** |
| 17 | Defense | **Armor** | 🔒 **LOCKED §5.1** |
| 18 | Defense | **Shields** | 🔒 **LOCKED §5.2** |
| 19 | Defense | **Hardening** | 🔒 **LOCKED §5.3** |
| 20 | Defense | **Fortification** | 🔒 **LOCKED §5.4** |
| 21 | Enhancers | **Bio-augmentation** | 🔒 **LOCKED §6.1** |
| 22 | Enhancers | **Training / Doctrine** (Unit Caliber) | 🔒 **LOCKED §6.2** |
| 23 | Enhancers | **Systems** | 🔒 **LOCKED §6.3** |
| 24 | Industrial | **Extraction** | 🔒 **LOCKED §7.1** |
| 25 | Industrial | **Fabrication** (+ unit-assembly, bay-size, reverse-ops) | 🔒 **LOCKED §7.2** |
| 26 | Logistical | **Storage** | 🔒 **LOCKED §8.1** |
| 27 | Logistical | **Transfer** | 🔒 **LOCKED §8.2** |
| 28 | Civic | **Habitation** | 🔒 **LOCKED §9.1** |
| 29 | Civic | **Development** | 🔒 **LOCKED §9.2** |
| 30 | Command | **Command** (colony/fleet scale dial) | 🔒 **LOCKED §10.1** |
| 31 | Chassis | **Personnel** | 🔒 **LOCKED §11.1** |
| 32 | Chassis | **Vehicle** | 🔒 **LOCKED §11.2** |
| 33 | Chassis | **Hull** (ship) | 🔒 **LOCKED §11.3** |
| 34 | Chassis | **Structure** (station) | 🔒 **LOCKED §11.4** |
| 35 | Chassis | **Mega** | 🔒 **LOCKED §11.5** |
| **— essence extensions (LOCKED 2026-07-09, first-class, wired alongside) —** | | | |
| 36 | Command | **Relay** (C3 network node) — **NEW DOOR** | 🔒 **LOCKED §10.2** · fills H8 · dossier ⚙10 |
| 37 | Warp + Industrial | **Route Works** (gate / wormhole construction) — **NEW cross-door capability** | 🔒 **LOCKED** · fills H8 · reuses the jump-network resolver (§0i) · dossiers ⚙2/⚙7 |

**Essence-extension DIALS — LOCKED 2026-07-09, first-class on already-locked doors** (full wiring in each category's ⚙ dossier + §🧭 E1): Weapons▸Exotic **SWAY** belief-effect + **bioweapon** Selectivity/incubation/detectability · Sensors▸Detection **ELINT/SIGINT** flavor (re-homes the cut `Resolution` → Information Ledger) · Sensors▸EW **counter-intel/cipher** + **broadcast-jamming** · Sensors▸Survey **counter-interference** · Defense▸Hardening **cultural insulation** · Command §10.1 **Command Reach** + **Redundancy/Hardening** + **Contracts** · Civic▸Development **Academy Type/Tier** + **Field-Lab deployment** · Industrial▸Extraction **excavation medium** · Industrial▸Fabrication **salvage/recovery** · Logistical▸Transfer **tow/grapple** + **covert-insertion bay**. Framework rules **§0g** (three acceptance criteria) · **§0h** (projector⛓counter tag) · **§0i** (count-resolvers) are locked into §0. *One new engine object underpins several: the **Information Ledger** (§🧭E1d); one new field: **`ExternalBeliefPressure`** on `LegitimacyInputs` (§🧭E1c).*

---

## §0 — The framework (applies to every door)

### 0a. Universal dials (every component, whatever the door)
| Dial | Drives (real stat) |
|------|--------------------|
| **Mass** | consumes the chassis structural budget; drags mobility |
| **Size / mount footprint** | which chassis hardpoint it fits |
| **Materials / cost** | what it's built from (ties to the Industrial economy + build time) |
| **Durability (HTK)** | how much damage destroys *this component* — the grave rung |
| **Crew requirement** | feeds the crew term of the supply gate |
| **Tech level** | gates availability AND scales the achievable range of every other dial (`TechData`) |
| **Signature contribution** | does it emit? feeds Detection / EW |

Each door adds its *specific* dials on top of these seven.

### 0b. Emergent constraint — the NUMBERS force the build, never a rulebook
**The player is never told "you must add X."** They dial the **outcome they want**; every dial emits **physical quantities** (energy demand, mass, volume, heat, recoil, crew-load…); the assembly bookkeeps them; and physics does the forcing. There is **no authored requirement graph** — just three physical facts:

1. **Unmet demand throttles the output (soft, and visible).** A beam with no power source reads **output: 0 (no power)** — so the player naturally bolts on a generator. Under-fed, the beam auto-throttles to the power actually available. The player is steered by *the result being wrong*, not by a rule.
2. **Mass/volume is the one hard wall — and it's a physical *container*, not a rule.** Everything mounted (the beam + the generator feeding it + the capacitor buffering it) has mass. The **sum vs the chassis's structural budget** is the only hard gate, and it's just "a box can't hold more than it can hold." Nothing says "planet-cracker → Mega"; the *tonnage* of a planet-cracker says it.
3. **The dials auto-accommodate.** Mount an over-spec part on an under-spec chassis and its **effective** stats throttle down to what the chassis can feed/hold — shown live. Your Death-Star-dialed beam performs like a pop-gun on a frigate, telling you plainly: *find more chassis.*

**The Death Star, done right (numbers, not rules):**
1. Player wants a planet-cracker → dials beam **output** to max.
2. That output demands enormous **energy** → the beam does nothing until fed → player adds a **generator** big enough to feed it.
3. Beam mass and generator mass both scale with output → their combined mass is astronomical.
4. That mass **overflows** every Personnel/Vehicle/Hull chassis → the only container with the budget is **Mega**.
5. The player never set out to "use a big chassis." **The numbers put them there.**

> An authored graph *tells* you the rules. The physical budget *lets you find out.* We build the second.

**This is already how Pulsar computes things** — a component's `Mass` is derived from its design dials (the NCalc formulas), and a hull carries a tonnage/volume budget. So "the numbers force it" is *native*, not a new system to bolt on. The only engine work is (a) making under-supply **throttle** the effective stat instead of hard-failing, and (b) surfacing the live effective-vs-dialed readout so the forcing is legible.

### 0c. The supply currency scales with the chassis
The *thing* being supplied changes with scale, but the gate is the same:
- **Ship / Vehicle / Station:** watts (power), ammo (mass), crew.
- **Personnel:** carry-weight / stamina (a soldier has no reactor; a self-powered sidearm just *weighs* something).
So "requires power" means **reactor watts** on a tank and **battery/cell + carry-weight** on a trooper — same ⛓ edge, chassis-appropriate currency.

### 0d. The modellability test — the standing gate on EVERY dial
Before a dial ships, it must answer: **can the sim actually model this decision? If not, what's the prerequisite?** Three verdicts:
- ✅ **Modelled** — the resolver already reads an equivalent stat. Ship it.
- ◐ **Wire** — an adjacent system exists; it needs a small hook. Ship it *with* the hook.
- ⏳ **Needs-mechanic-first** — nothing reads it yet. **Name the prerequisite mechanic and DEFER the dial** until that's built. *Never ship a dead knob* — a dial the sim ignores is exactly the "fidelity nobody acts on" the audit warns against.

The yardstick for **weapon** dials is the aggregate combat resolver: `ShipCombatValueDB` → `WeaponProfile` (**nature × delivery × velocity × tracking × saturation × rate**) resolved through the **dodge / shield / armour matchup** (`GroundDamageMatrix`, `SoakFractionOf`, `ArmourSoak`, the `HitFraction` curve). A weapon dial is ✅ if it maps onto one of those axes. Each category will get its own yardstick (Propulsion → the Newtonian move/closing model; Sensors → the detection math; etc.).

**This test also drives the build order:** a ⏳ dial's prerequisite mechanic is a work item that must land *before* the dial. It's how the designer stays honest — every knob is wired to a consequence.

### 0e. The number model — calibration anchor (battles auto-resolve on THESE numbers)
Battles auto-resolve on `ShipCombatValueDB` (firepower vs toughness) through `CombatEngagement` / `AutoResolve`. **We do not invent a number scale — we express every dial in the scale the resolver already reads.** The anchor, from the live code:

**One currency: ENERGY (joules) — for damage AND hit-points.**
| Anchor | Value | Source |
|--------|-------|--------|
| A component's hit-points | **100 kJ** (`ComponentHitPoints_J = 100_000`) | `ShipCombatValueDB.cs:81` |
| Armour hit-points | `thickness × 100 kJ` (`ArmorHitPointsPerThickness_J`) | `:84` |
| **Toughness** of a unit | Σ component-HP + armour, in joules | ShipCombatValueDB |
| **Firepower** of a unit | Σ weapon salvo-energy (J) × hit-fraction × matchup | ShipCombatValueDB |
| **Combat-pace dial** | `SalvoDamageScale = 0.1` — only 10% of a salvo's energy counts toward kills | `CombatEngagement.cs:94` |

**The balance constants are already in the code — the dials feed *these*, they don't replace them:**
| Constant | Value | Role |
|----------|-------|------|
| `EvasionCap` | 0.9–0.95 | most a dodger avoids (nothing untouchable) |
| Shield reduction | `Shield/(Shield+150)`, cap 0.75 | innate % soak (`ShieldRefK=150`) |
| Shield-vs-nature | Kinetic **1.0** · Explosive 0.75 · Energy **0.5** · Exotic **0.0** | the nature matchup (`ShieldSoakVsX`) |
| Armour soak | **1.5 / point, flat, per source**, floored at 0.1 | swarm-bounces / alpha-punches (`ArmourSoakPerPoint`) |
| `SaturationReference` | 50 | saturation at/above this floors dodge (flak) |
| `BeamVelocityThreshold` | 10^7 m/s | at/above = hitscan (undodgeable); below = dodgeable |
| Range ladder | flak 50 km · disruptor 400 km · railgun 500 km · missile 1000 km · engagement bubble 1 Gm | the reference envelope |

**The scale ladder (orders of magnitude by chassis tier — all joules):**
| Tier | Damage / HP range | e.g. |
|------|-------------------|------|
| Personnel | ~10³–10⁵ J (kJ) | rifle shot, a soldier |
| Vehicle | ~10⁵–10⁷ J | tank gun, a tank |
| Hull (ship) | ~10⁶–10⁹ J (MJ–GJ) | beam, missile, a warship |
| Structure / Mega | ~10⁹–10¹⁵⁺ J | station, a Death-Star beam |
Tiers overlap by ~an order of magnitude; **tech multiplies the achievable max** (multiplicative growth, `UNIVERSAL-ASSEMBLY-DESIGN.md`; `TechData` formulas like beam-range `10000 × 2^level`).

**How a dial becomes a number, and how the forcing emerges:**
1. A dial sets a physical quantity in the anchor units (output in J, velocity in m/s vs the 10⁷ threshold, saturation vs 50, range in m, nature → the soak row).
2. `Mass = f(those quantities, tech)` — output/rate scale mass; **this is the cascade** (bigger number → bigger mass → bigger chassis). Already how Pulsar computes `Mass` (the NCalc formulas).
3. The resolver combines firepower vs toughness through the matchup constants above. **Balance is holistic because every weapon lands on the same joule scale and passes through the same matchup** — a build is strong only where the matchup favours it (energy vs unshielded, kinetic vs shielded, saturation vs dodgers, alpha vs armour). *We inherit the balance; we don't re-derive it.*

**Calibration method going forward:** for each door, give every dial its **unit + range + the anchor it pins to**, then a **numeric preset table**, then sanity-check one exchange through the resolver (does a reference weapon kill a reference hull in a sane number of salvos?).

**The resolver's exact input surface + where every dial inserts is audited in `docs/AUTO-RESOLVER-ANATOMY.md`** — the salvo engine taken apart bit by bit: the `WeaponProfile` 7-field footprint the salvo math reads, which dials write an existing field (✅), which need one of six named new fields/terms (➕ — Penetration first), and which need a deferred mechanic (⚙). That doc is the resolver-wiring backlog behind these dials.

### 0f. Every component serves the WHOLE game, not just war — the multi-consumer rule (applies to EVERY door)
**A dial is real if *a sim system* acts on it — combat is only ONE consumer, and rarely the main one.** Weapons was combat-first because that's what weapons are *for*; almost every other category is not. A survey sensor that reveals a habitable world drives a **colonization** decision as real as a beam driving a combat decision. So when grading a non-weapon door, do **not** measure it against the combat resolver alone — measure it against **whatever system consumes it**, and name that system. The consumers, all first-class:

| Consumer system | Example dial that feeds it | The decision it creates |
|-----------------|----------------------------|-------------------------|
| **Combat resolver** | weapon output, evasion, detection (fog) | who wins the fight |
| **Economy / industry** | geo-survey → minerals; extraction rate | what you can mine/build |
| **Expansion / navigation** | grav-survey → jump points; hazard-mapping | where you can go safely |
| **Colonization / habitability** | atmospheric survey → world viability | where you can settle |
| **Research / tech** | anomaly + xenoarchaeology survey; a lab's output | what you can unlock |
| **Diplomacy / first-contact** | life-sign + civ detection | who you meet, and how |
| **Population / morale / logistics** | habitation, storage, life-support | whether people stay + supplied |

**The corollary — a component is for ALL chassis, civilian and military, ship AND facility.** The same sensor door that arms a warship's targeting also builds a colony's **deep-space observatory**, a spaceport's **traffic-control array**, and a survey ship's **science suite** — because a component mounts on any Chassis that can supply it (§0b), a *facility* is just another Chassis (Structure/Mega). So every door must ask "what does the **civilian / scientific / industrial** version of this look like, on a **station or colony**, not just a warship?" — and the modellability audit grades it against **that** consumer too. *A door graded only by its combat use is a door half-designed.* **This rule governs every remaining category and every followup — Sensors first applies it in earnest (Survey especially), and it carries forward to Power, Industrial, Logistical, Civic, and the rest.**

### 0g. The THREE acceptance criteria — a capability is real only if all three hold (applies to EVERY door)
*(Promoted to the framework 2026-07-09 from the Essence Extensions synthesis §E0b. Full rationale in §E.)* Cradle-to-grave alone is a **solitaire, player-side** test — it proves *the player* can reach and lose a thing. It says nothing about the opponent or the gauge. The complete "is this real" triad, stamped on every door's audit alongside its dials:

1. **Reachable** — the cradle-to-grave chain (mineral → material → component → research → unit → decision → loss). Miss it → a *parachuted-in engine abstraction*.
2. **Mirrored** — name how the **NPC runs this same gear/dial against the player through the identical order path** (delegation = NPC AI; there is no player-only code path — `AI-SELF-PLAY-DESIGN.md`). Miss it → *solitaire* (the inert-AI trap). *Nuance: a pure-infrastructure door the NPC uses FOR itself, not AT you — but it must still USE it.*
3. **Observable** — name the **gauge that shows both sides** the gear working (when the player uses it, and when it's used on them). Miss it → *pretty / an invisible bug* — usually cheap Failure-A (the number exists, just unwired; `INFORMATION-DELTA-DESIGN.md`), so build the readout. A door that passes only #1 is a display piece.

Both new criteria are nearly free because they **reuse the designer's own machinery**: the Mirror is cheap *because* a capability is a component on a shared door (the NPC mounting it is the same act as the player mounting it), and Visibility reuses §0b's live effective-vs-dialed throttle readout, extended to "shown to the target too."

### 0h. The PROJECTOR ⛓ COUNTER tag — one dial-spine across all four conflict pillars (applies to every projection/counter door)
*(Promoted 2026-07-09 from §E0a.)* Every conflict pillar is the **same skeleton with a different medium** (`INFLUENCE-PILLAR-DESIGN.md`): **Military · Espionage · Diplomacy · Influence**, each with a **Projection** slot and a **Counter** slot. The `WeaponProfile` (nature × delivery × velocity × tracking × saturation × rate, beaten by armour/shields/PD) already *is* a **Reach · Cadence · Selectivity · Delivery ⛓ Counter** grammar — it has just been pretending it's only about weapons. So every projector/counter door carries two tags: **`pillar`** (which of the four) + **`skeleton-role`** (Projection / Counter / Medium / Detection). What the tag buys: (a) the §0d modellability test runs **per-pillar** — an influence projector is graded against the *legitimacy resolver*, an espionage projector against the *Information Ledger*, never the combat resolver it doesn't belong to (§0f, lifted from "consumers" to "media"); (b) the anti-dominance law (§1.1: every advantage is bought; every projector has a counter) becomes **cross-pillar** — you cannot ship a belief-projector without naming the insulation that beats it. **Zero new doors** — the tag makes the four-pillar skeleton the designer's spine.

### 0i. The authoring law — COUNT RESOLVERS, NOT DOORS (the test before adding anything)
*(Promoted 2026-07-09 from §E0c.)* The 37 doors are **not 37 loops** — they are **~7 resolver-shaped loops, and the doors + hundreds of presets are DATA those loops read**: the §0b physical-budget loop (all 37 doors) · the combat resolver (Weapons + Defense) · the detection math (Sensors) · the move/closing model (Propulsion) · the industry/economy loop (Industrial + Logistical) · the field-site loop (all 11 exploration types = the lab loop reused) · the delegation loop (all 19 leader roles = config on one pipeline). **The test before adding any door / flavor / site-type / leader-role: "is this a new LOOP, or DATA for an existing loop?"** A new door with no new resolver is **data** — tag it, table it, done. A door claiming a new resolver must **prove** it's genuinely distinct under §0d — otherwise it is a *director in a costume* (`LIVING-GALAXY-DESIGN.md`'s cardinal sin). *Influence passes perfectly: "the rebellion model IS the kill; influence is just a new attacker on the same battlefield" — one new input, zero new loops.*

---

## §1 — Weapons

### 1.0 Shared weapon dials (common to all five weapon doors)
On top of the universal seven, every weapon has:
| Dial | Drives |
|------|--------|
| **Damage / output per shot** | base hit magnitude |
| **Rate of fire** | sustained DPS; feeds draw/heat |
| **Range** (optimal + falloff curve) | engagement envelope |
| **Tracking / accuracy** | hit-fraction vs evasive targets |
| **Mount / traverse** (fixed-forward ↔ turret arc) | which targets it can bear on (nose gun vs broadside) |
| **Firing modes** | secondary behaviors — incl. the **effect-bus** hooks (stun/non-lethal = *capture* effect vs *damage*) |

The five doors differ in **how the hit is delivered and what it beats** (the nature × delivery axes). Door-specific dials below.

### 1.1 Weapons ▸ ENERGY  🔒 *locked*
*Directed energy: light, particle, plasma, ion. Everything from a lazpistol to a Death-Star beam falls out of these dials.*

**The anti-dominance rule (the whole point):** every option must **buy** its advantage with a real cost, so no setting is a free win. "A beam can't be dodged" is only balanced because a beam **pays** for it (dwell, range, power). Each dial below lists its options with *why you'd pick it* AND *the catch* — if an option has no catch, it's a bug in the design.

**A. Delivery — how the energy arrives**
| Option | Why pick it | The catch (why not always) |
|--------|-------------|----------------------------|
| **Continuous Beam** | **undodgeable** — hits any target it can track | must **dwell** (damage ramps while locked; a juking target that breaks your track resets it); **short** effective range (beam divergence/bloom); **heavy continuous** power drain while firing |
| **Pulse** | balanced — partial dodge, decent range, recovers power between pulses | master of nothing; a nimble target still juki some pulses |
| **Bolt** | **long range** + **power-efficient** (fire-recover, reactor rests) + big **per-shot alpha** (punch-through / first-strike) | **dodgeable** (travel time) — wasted on fast evasive targets |
| **Scatter / Burst** | many weak packets = **saturation floors dodge** (the energy-flak — anti-fighter/anti-missile) | **short range**, low per-hit, bounces off armor |

*So you pick Bolt over Beam when the enemy is slow (dodge is moot), or you need reach, or you're power-starved, or you want alpha. You pick Beam when hunting fast dodgers up close with power to spare.* Both are non-dominated.

**B. Focus — beam geometry**
| Option | Why | Catch |
|--------|-----|-------|
| **Lance (tight)** | max **range** + **penetration**, all energy on one point | single target; misses reward evasion |
| **Standard** | balanced | — |
| **Wide cone** | hits an **arc / multiple** targets; the wide-stun / crowd-sweep | **short** range, low per-target damage |

**C. Charge behavior**
| Option | Why | Catch |
|--------|-----|-------|
| **Instant / repeater** | fires now — **responsive**, high rate, **no capacitor needed** | low per-shot alpha |
| **Capacitor / charge** | huge **alpha** (one-shot kills, first-strike) | wind-up **telegraph** (vulnerable while charging) + ⛓ **requires a capacitor** |
| **Overload / hold** | the longer you hold, the bigger the dump | risks **burnout** (component HTK) + big signature |

**D. Nature — the energy type (the matchup; this is *what it beats*)**
| Option | Strong vs | Weak vs / catch |
|--------|-----------|-----------------|
| **Thermal (laser)** | cheap, hitscan-fast, high volume; great vs **unshielded** | **mirror/ablative armor scatters it**; blooms in atmosphere |
| **Particle** | all-rounder; partial shield bleed-through | jack of all trades |
| **Plasma** | heavy **anti-armor** damage + splash | slow (bolt = dodgeable), heavy, hot |
| **Ion / Disruptor** | **bypasses/overloads shields** | **wasted vs unshielded** (low HP damage) — a real reason not to default to it |
| **Graviton / Exotic** | ignores conventional defense | enormous **cost + tech**; overkill vs cheap targets |

**E. Penetration ↔ Splash**
| Option | Why | Catch |
|--------|-----|-------|
| **Penetrating** | punches **through armor** into one hard target | wasted on soft/swarm (over-penetrates) |
| **Splash / AoE** | clears **swarms / soft targets** | **bounces off armor** |

**F. Range profile**
| Option | Why | Catch |
|--------|-----|-------|
| **Point-blank** | massive close damage; brawler / PD | falls off fast — useless at standoff |
| **Balanced** | flexible | peak nowhere |
| **Standoff / sniper** | out-ranges the enemy (first-shot advantage) | low close-in damage; needs ◐ Fire Control to land |

**G. Frequency modulation** *(anti-adaptation)*
| Option | Why | Catch |
|--------|-----|-------|
| **Fixed** | cheapest, max output | **adaptive shields learn it** (Borg counter, H2b) |
| **Modulating** | stays ahead of adaptive defenses | costs output + mass — **wasted vs enemies that don't adapt** (most of them) |

**H. Thermal signature** *(the stealth tradeoff)*
| Option | Why | Catch |
|--------|-----|-------|
| **Hot / unbaffled** | max output/rate | **firing spikes your signature** — seen from far (Detection/EW) |
| **Baffled / cool** | ambush-friendly, low emission | lower output/rate |

**I. Cooling**
| Option | Why | Catch |
|--------|-----|-------|
| **Uncooled** | lighter, cheaper | sustained rate **throttles** under heat — a burst weapon |
| **Cooled** | sustains high rate-of-fire | heavier + ◐ requires a cooling system |

**J. Medium performance** *(where it works)*
| Option | Why | Catch |
|--------|-----|-------|
| **Vacuum-tuned** | best in space | **scatters in atmosphere / water** |
| **Atmospheric / submersible** | works in air/fog/underwater (a sub's or tank's energy gun) | lower peak range/output |

**K. Efficiency** *(damage per watt — a supply/mass decision)*
| Option | Why | Catch |
|--------|-----|-------|
| **High-efficiency** | less draw → a smaller/lighter generator → lighter build | lower peak output per shot |
| **Brute-force** | max damage | power-hungry → a bigger generator → heavier build (the mass forcing bites harder) |

**L. Linked fire** *(emitter count — alpha vs coverage)*
| Option | Why | Catch |
|--------|-----|-------|
| **Linked** | all emitters fire as one → **big combined hit** (beats flat armour) | one shot = one target; a miss wastes all of it |
| **Independent** | each emitter engages separately → **splits fire** across targets | small per-hit — bounces off armour |

**M. Point-defense capability** *(can it shoot down incoming?)*
| Option | Why | Catch |
|--------|-----|-------|
| **PD-capable** | intercepts incoming **missiles / fighters** | needs fast tracking + short range — a compromise vs its anti-ship role |
| **Anti-ship only** | full output/range vs big targets | helpless against a saturating missile swarm |

**Derived stats (computed, shown to the player):**
`power draw = output × rate ÷ efficiency` · damage/salvo · sustained DPS · dodge-fraction (delivery × tracking) · signature spike (output × thermal-bloom) · mass · cost.

**Modellability audit (the §0d gate applied — what the resolver actually reads):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Output · Rate · Tracking | ✅ | firepower / sustained-DPS / hit-fraction in `ShipCombatValueDB` |
| Delivery (beam/bolt/scatter) | ✅ | `WeaponProfile.Delivery` → the `HitFraction` dodge curve (beam ignores evasion; scatter = saturation floors it) |
| Nature (thermal/ion/…) | ✅ | shield `SoakFractionOf` + `WeaponNature` matchup (ion = exotic bypass, per `DisruptorWeaponTests`) |
| Penetration ↔ Splash | ✅ | `ArmourSoak` (flat-per-source — penetration beats it, splash bounces) |
| Focus · Linked fire | ✅ | saturation + the existing **fire-split** across targets (`MultiPartyEngagement`) |
| Range profile | ✅ | the weapon-range engagement trigger / closing model |
| Efficiency | ✅ | scales power draw → feeds the mass-forcing (§0b) |
| Thermal bloom / signature | ◐ **wire** | EMCON *already* reads `ShotsFiredThisTick` → activity (`EmconActivityProcessor`); the dial just scales that firing-heat term |
| Cooling / heat | ◐ **wire** | effective rate = f(heat, cooling); the resolver already reads rate |
| Overcharge / burnout | ◐ **wire** | a self-damage-on-overcharge rule (the damage system already removes components) |
| Point-defense | ◐ **wire** | intercept incoming ordnance (`MissileImpactProcessor` + the flak anti-missile role exist; needs the PD-targeting hook) |
| Charge (the damage profile) | ✅ | high-damage / low-effective-rate — the aggregate resolver represents this natively |
| Charge (the "vulnerable telegraph" window) | ⏳ **defer** | the salvo resolver isn't per-shot-timed; **abstract to hi-dmg/lo-rate for now**, revisit if combat goes finer-grained |
| **Frequency modulation** | ⏳ **defer** | needs **adaptive shields (H2b)** built first — with no adaptive defense there is nothing to modulate against |
| **Medium performance** | ⏳ **defer** | needs a **combat-environment modifier** (space / atmosphere / water) — space combat doesn't tag a medium yet |

**Reading of the audit:** the Energy door is **overwhelmingly modellable today** — the aggregate resolver already speaks in nature × delivery × saturation × tracking × armour/shield, which is most of these dials. Four need a small **wire** to an adjacent system that already exists. Only **three** must **wait on a prerequisite mechanic** (adaptive shields, a combat-medium modifier, and finer combat timing) — and those three become explicit build items *before* their dials ship. No dead knobs.

**Numbers (calibrated to §0e — units, ranges, and the anchor each pins to):**
| Dial | Unit | Range (tech-scaled max) | Pins to |
|------|------|--------------------------|---------|
| **Output** | J / shot | ~1 kJ (lazpistol) → 10¹⁵⁺ J (Death Star) | component HP = 100 kJ (so 100 kJ ≈ one component/shot) |
| **Rate** | shots/s | 0.001 (charge siege) → ~10 (repeater) | firepower = output × rate |
| **Delivery (velocity)** | m/s | bolt 10⁴–10⁶ · beam ≥ **10⁷** (hitscan) · light 3×10⁸ | `BeamVelocityThreshold 10⁷` → dodge on/off |
| **Tracking** | 0–1 | 0.1 (dumb) → 0.9 (`MissileTrackingStub`) | vs `EvasionCap 0.9` |
| **Nature** | enum | Thermal/Particle/Plasma = **Energy soak 0.5** · Ion/Graviton = **Exotic 0.0** (bypass) | `ShieldSoakVsX` |
| **Saturation (scatter/focus)** | pellets·rate | 1 (lance) → ≥ **50** (flak floors dodge) | `SaturationReference 50` |
| **Range** | m | 500 m (pistol) → 400 km (disruptor) → farther w/ tech | the range ladder |
| **Power draw** (derived) | W | output × rate ÷ efficiency | the generator-mass cascade |
| **Mass** (derived) | kg | scales with output × rate × tech | the chassis-budget forcing |

**Sanity-check one exchange (does it resolve sanely?):** a **1 MJ hitscan beam** (velocity ≥ 10⁷ → undodgeable) vs a **5-component frigate** (toughness 5 × 100 kJ = 500 kJ), thermal nature vs an unshielded hull (soak 1.0): landed = 1 MJ × `SalvoDamageScale 0.1` = **100 kJ/salvo** → **~5 salvos to kill**. Against a *shielded* hull the same beam is halved (Energy soak 0.5) → ~10 salvos; a *kinetic* railgun of equal energy is soaked 1.0 → ~5 — so nature flips the fight, exactly as the matchup intends. This lands in the same ballpark as the measured `CombatStressLab` battles (a 50v50 mirror ≈ 38 salvos), so the calibration is consistent with the live resolver.

**Physical demands (what the dials cost — throttles and mass, never a rulebook, per §0b):**
- **Power draw** = output × rate. Not "required" — *unmet → the beam auto-throttles to available power* (a Death-Star dial on a frigate fires like a pop-gun). The player adds a generator because the readout shows the shortfall, then finds the generator's **mass** funnels the chassis size.
- **Buffered energy (capacitor)** — a Charge/Overload shot can only accumulate as much as you can store; **no capacitor = no big alpha** (the dial caps itself), not an error.
- **Heat** — sustained fire builds heat; undissipated heat **throttles rate** automatically. Add cooling (which adds mass) to sustain it.
- **Accuracy at range** — without Fire Control the standoff hit-chance is just *low* (a number); a standoff build naturally wants one.
- **Mass** — the real forcer. Output/rate/nature/cooling all add mass; that mass + the generator's + the capacitor's is the tonnage the chassis budget must physically hold. This is the only hard wall, and it's a container, not a rule.

**Preset coordinates — proof the dials span the franchises (each a distinct point in the option space):**
| Weapon | Delivery | Focus | Charge | Nature | Range | The trade it chose |
|--------|----------|-------|--------|--------|-------|--------------------|
| **Lazpistol** | Bolt | Lance | Instant | Thermal | Point-blank | cheap + light (fits Personnel), pays in range/alpha |
| **Phaser array** | Beam | Wide↔Lance | Instant | Particle | Balanced | undodgeable + stun-mode, pays in reactor draw |
| **Turbolaser** | Bolt | Lance | Instant | Plasma | Standoff | reach + anti-armor alpha, pays in dodge-ability + ⛓ big reactor |
| **Ion / disruptor** | Bolt | Standard | Instant | **Ion** | Balanced | ignores shields, **useless vs unshielded** |
| **Flak-laser (PD)** | Scatter | Wide | Instant | Thermal | Point-blank | floors fighter dodge, pays in range |
| **Death-Star beam** | Beam | Lance | **Charge** | Graviton | Standoff | plate-cracking alpha; its output demands a huge generator + capacitor, whose **mass** overflows everything below **Mega** — plus a long telegraph |

The Death Star is the payoff: you can *dial* a planet-cracker onto any chassis, but the **tonnage** of the beam + the generator that feeds it + the capacitor that buffers it won't fit anything under Mega — and on a smaller hull the effective output just throttles to a pop-gun. The design falls out of the dials; the Mega chassis falls out of the *numbers*, never a rule.

---

### 1.2 Weapons ▸ BALLISTIC  🔒 *locked*
*Kinetic projectiles: rifles, autocannons, tank guns, railguns, flak, mass drivers. Same scaffold as Energy, but the physics **forces differently** — the cascade is **ammo mass** (a magazine that runs dry) instead of power draw, and the stability cost is **recoil** instead of heat. Everything from an assault rifle to a planet-bombarding mass driver falls out of these dials.*

**Anti-dominance rule applies** (every option buys its edge with a real cost). And two ballistic-only forcings you'll see throughout:
- **Ammo mass cascade** — shells have mass; magazine mass = shell-mass × capacity. A high-rate big-bore gun demands a huge magazine → huge mass → a bigger chassis (the ammo twin of Energy's generator-mass cascade). And it **runs dry** → resupply (this is *already built*: `GroundAmmo` — `MaxAmmo_kg`/`CurrentAmmo_kg`/`IsDry`/resupply).
- **Recoil forcing** — firing shoves the platform; recoil is absorbed by **chassis mass**. A battleship gun on a frigate can't be aimed (or shoves the hull) → another reason big guns funnel to big chassis, by the numbers.

**A. Projectile — what you throw (the core ballistic axis)**
| Option | Why | The catch |
|--------|-----|-----------|
| **Solid slug (kinetic)** | cheap, dense, high **penetration** by pure KE | no splash (over-penetrates soft targets); dodgeable |
| **Explosive shell (HE)** | **splash/AoE** — clears soft targets & clusters | bounces off heavy armour; heavier per round (magazine fills faster) |
| **Sabot / AP dart** | **max armour penetration** | wasted on soft/swarm; costly ammo |
| **Flak / canister (pellets)** | many pellets = **saturation floors dodge** (anti-fighter/missile) | short range, low per-hit, useless vs armour |

**B. Muzzle velocity — the accelerator (ballistic "delivery")**
| Option | Why | The catch |
|--------|-----|-----------|
| **Low (howitzer/mortar)** | lobs heavy shells, **low recoil**, cheap, can arc indirect | slow → very **dodgeable**, short direct range |
| **High (cannon)** | flatter, longer direct range, more KE | more recoil, barrel wear |
| **Hyper (railgun/gauss)** | near-hitscan → **hard to dodge**, extreme range + KE | huge recoil + **draws power to accelerate the slug** (this is what naturally makes a railgun "ammo **and** power" — the "Both" supply, emergent from the dial, no special rule) |

**C. Rate / feed**
| Option | Why | Catch |
|--------|-----|-------|
| **Single-shot** | max per-shot power, light, sips ammo | slow — misses hurt, bad vs fast targets |
| **Autocannon (rapid)** | high rate + **suppression/saturation** | **drains the magazine fast** + more recoil + wear |

**D. Caliber / shell mass**
| Option | Why | Catch |
|--------|-----|-------|
| **Small** | light ammo (more rounds per magazine-mass), high rate | weak vs armour |
| **Large** | huge per-hit, anti-armour/capital | heavy ammo (magazine fills fast), slow, big recoil |

**E. Recoil management — the stability cost (ballistic analog of cooling)**
| Option | Why | Catch |
|--------|-----|-------|
| **Unbraced** | light, cheap | recoil **wrecks accuracy on a light chassis** (penalty scales recoil ÷ chassis-mass); can shove a small unit |
| **Compensated (brake/dampers)** | stable on lighter platforms | adds mass |
| **Recoilless** | fire a big gun off a tiny frame | **bleeds muzzle energy** → less range/penetration |

**F. Fuzing (unguided — not the Guided door)**
| Option | Why | Catch |
|--------|-----|-------|
| **Contact** | full damage on a direct hit | must hit directly |
| **Proximity (airburst)** | detonates near the target → **floors evasion** without a direct hit (the flak fuze) | needs a sensor fuze (cost); less than a direct-hit's damage |
| **Delayed/penetrating** | punch in *then* detonate (bunker-buster) | needs the right depth setting; wasted on soft targets |

**G. Ammo loadout — selectable rounds**
| Option | Why | Catch |
|--------|-----|-------|
| **Single type** | full magazine of one round, simple | wrong vs off-matchup targets |
| **Multi-ammo (switchable)** | swap AP/HE/flak to match the target in-fight | splits magazine capacity; a switch costs a reload cycle |

**H. Trajectory**
| Option | Why | Catch |
|--------|-----|-------|
| **Direct-fire** | flat, precise, immediate | blocked by terrain/cover |
| **Indirect (arcing artillery)** | lobs over cover/terrain, hits from beyond line-of-sight | needs a **spotter** for accuracy; slow shells (dodgeable) |

**Physical demands (the forcing — mass + recoil, never a rulebook, per §0b):**
- **Ammo mass** = shell-mass × capacity → the magazine's mass funnels the chassis size, and the gun **runs dry** → resupply (Logistical). *Already modelled* (`GroundAmmo`).
- **Recoil** → accuracy penalty = recoil ÷ chassis-mass; big gun on a small hull = can't aim. The chassis mass is the recoil sink → big guns force big chassis.
- **Power** — *only* if velocity is dialed to hyper (railgun); the power demand appears on its own and cascades the generator mass, exactly as in Energy. This is where a railgun becomes a dual-supply weapon with no authored flag.
- **Mass** of the gun scales with caliber × velocity.

**Modellability audit (§0d — what the resolver reads):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Projectile (slug/HE/sabot/flak) | ✅ | `WeaponNature` kinetic + splash/penetration (`ArmourSoak`) + flak saturation (`FlakWeaponTests`) |
| Muzzle velocity | ✅ | velocity is a `WeaponProfile` axis → dodge-ability (`RailgunWeaponTests`: finite-velocity, dodgeable) |
| Rate · Caliber | ✅ | firepower / saturation / rate |
| **Ammo mass / runs-dry** | ✅ | **already built** — `GroundAmmo` (`MaxAmmo_kg`, `IsDry`, resupply) |
| Fuzing — proximity/airburst | ✅ | saturation-floors-dodge (the flak model) |
| Power (hyper-velocity) | ✅ | the supply model already rates railgun as ammo+power ("Both") |
| **Recoil → accuracy** | ◐ **wire** | accuracy = f(recoil ÷ chassis-mass); resolver reads accuracy, the chassis-mass term needs a hook |
| Multi-ammo switching | ◐ **wire** | swap the active `WeaponProfile` (firing-mode style); resolver reads one profile at a time |
| Recoilless (energy bleed) | ◐ **wire** | a range/penetration reduction term |
| **Indirect / arcing + spotter** | ⏳ **defer** | needs **line-of-sight/terrain blocking + a spotter relay**; ground has terrain+hexes (partial), space has no LOS block — build the LOS+spotter mechanic before this dial |

**Reading:** Ballistic is **even more ready than Energy** — the ammo half (the biggest new system a projectile weapon needs) is *already built* (`GroundAmmo`), and velocity/flak/nature all map to the resolver. Three small **wires** (recoil→accuracy, multi-ammo switch, recoilless bleed) and one **deferred** dial (indirect-fire needs LOS + a spotter mechanic first).

**Numbers (calibrated §0e):**
| Dial | Unit | Range (tech-scaled) | Pins to |
|------|------|---------------------|---------|
| **Output (KE = ½mv²)** | J | rifle ~2 kJ → sabot ~2 MJ → railgun ~100 MJ → mass driver 10¹²⁺ J | damage in joules; **velocity dominates (v²)** |
| **Muzzle velocity** | m/s | howitzer ~300 · cannon ~1500 · railgun 10⁶–10⁷ | dodge (< 10⁷ dodgeable; railgun nears hitscan) |
| **Caliber / shell mass** | kg | bullet 0.01 · shell 10 · railgun slug 100 · driver round tons | KE + magazine mass |
| **Rate** | rounds/s | 0.2 (tank) → 20 (autocannon) | firepower + ammo drain |
| **Magazine** | kg (Σ shell mass) | the ammo cascade (`GroundAmmo.MaxAmmo_kg`) | chassis-mass forcing + runs-dry |
| **Recoil** | N·s (= m×v) | accuracy penalty = recoil ÷ chassis-mass | big gun on small hull can't aim |
| **Nature** | kinetic | shield soak **1.0** (shields wall kinetic); armour is the counter (penetration) | `ShieldSoakVsKinetic 1.0` |
| **Power** (railgun only) | W | KE × rate ÷ eff (hyper-velocity → huge) | dual-supply cascade |
| **Range** | m | autocannon ~50 km → railgun 500 km | range ladder |

**Sanity-check:** a **2 MJ sabot** vs the 500 kJ frigate, kinetic + penetration beating armour, unshielded: 2 MJ × 0.1 = 200 kJ/salvo → **~3 salvos.** But vs a **shielded** hull, kinetic is *fully* soaked (1.0) → walled until the shield drops. So Ballistic is the **mirror of Energy**: armour-cracker but shield-walled, where Energy is shield-bleeding but armour-scattered. The matchup is symmetric — neither dominates.

**Preset coordinates — the span, each a distinct point:**
| Weapon | Projectile | Velocity | Rate | The trade it chose |
|--------|-----------|----------|------|--------------------|
| **Assault rifle** | small slug | high | rapid | light, sips ammo (fits Personnel); weak vs armour |
| **Autocannon (PD)** | HE/flak | high | rapid | saturation vs fighters/missiles; short range |
| **Tank cannon** | sabot/HE | high | single | anti-armour alpha; big recoil → needs a Vehicle to brace |
| **Railgun** | slug | **hyper** | single/burst | undodgeable + reach; pays in **recoil + power** (dual-supply) |
| **Howitzer** | large HE | **low, indirect** | single | lobs over cover; ⏳ needs a spotter; slow shells |
| **Mass driver** | huge slug | hyper | single | planet bombardment; its **shell mass + recoil + power** overflow everything below a Mega chassis |

Same lesson as the Death Star, kinetic flavour: the **mass driver** confines itself to Mega because a planet-cracking slug's mass, magazine, recoil sink, and railgun power simply won't fit smaller — the numbers force it.

---

### 1.3 Weapons ▸ MELEE  🔒 *locked*
*Close combat: blades, clubs, claws, power fists, energy swords, boarding gear, ramming prows. The forcing inverts — **no ammo and little/no power** (melee's whole payoff: no magazine mass, no fuel, no logistics, never runs dry) — but **range ≈ 0**, so the weapon is worthless until the unit **closes to contact.** Melee trades all the weapon-supply overhead for a *positioning* problem, and pays it back as **undodgeable, high-alpha contact damage** (you can't juke a sword in a grapple).*

**The melee identity (the standing trade):** you save the mass a gun spends on ammo/reactor, and you spend it instead on **mobility + armour to survive the approach.** A slow melee unit is dead weight; a fast or armoured one that reaches you is terrifying. Damage also scales with the chassis's **strength** (servo/mass) and **Enhancers** (a power-armour bio-mod hits harder) — so melee is partly emergent from the *body*, not just the weapon.

**A. Reach**
| Option | Why | The catch |
|--------|-----|-----------|
| **Short (knife/fist/claw)** | fast, light, concealable | body-to-body — zero reach, must fully close |
| **Long (spear/pike/lance)** | **out-reaches** other melee → strikes first in the clash | slower, unwieldy in tight quarters |
| **Flexible (whip/flail)** | reach + a sweeping **arc** (multi-target) | low per-hit, hard to control |

**B. Damage mode**
| Option | Why | Catch |
|--------|-----|-------|
| **Cutting (blade)** | clean high damage, sever | glances off very hard armour |
| **Crushing (club/hammer/fist)** | **ignores armour by brute force**, staggers/knockback | slow, no finesse |
| **Piercing (spike/drill)** | **penetrates armour** | narrow, no splash |
| **Energy (plasma blade/lightsaber)** | cuts nearly anything + can **parry** | draws a little power; a dead reactor = a heavy stick |

**C. Power source**
| Option | Why | Catch |
|--------|-----|-------|
| **Unpowered** | zero power, zero ammo, zero logistics — brutally simple | capped by muscle/servo strength (leans on chassis + Enhancers) |
| **Powered (chainsword/power fist/energy blade)** | far higher damage, cuts armour, enables parry | small power draw; loses its edge if the host is drained |

**D. Strike speed**
| Option | Why | Catch |
|--------|-----|-------|
| **Flurry (fast)** | many strikes → overwhelms even a dodger, good vs light foes | low per-hit; stamina |
| **Heavy (wind-up)** | massive per-blow **alpha**, staggers | slow enough that a nimble foe *can* slip between blows (the one way melee gets evaded) |

**E. Parry — the defensive function (H7: weapon that's also defense)**
| Option | Why | Catch |
|--------|-----|-------|
| **Offense-only** | full damage | no protection |
| **Parry-capable** | deflects incoming **melee**, and (energy blade) some **ranged** — the sword-and-board / lightsaber | trades some attack for defense; parrying *ranged* takes skill (a People/Enhancer trait, not the weapon alone) |

**F. Control — strike vs grapple**
| Option | Why | Catch |
|--------|-----|-------|
| **Strike** | pure damage | — |
| **Grapple / bind (claw, net, boarding clamp)** | immobilizes or **captures** the target — boarding a ship, taking a unit intact (the capture effect) | no damage; loses if the target is stronger |

**G. Closer — melee's answer to its own range problem**
| Option | Why | Catch |
|--------|-----|-------|
| **Static** | relies on the chassis's own mobility to close | a slow platform never reaches the enemy — melee dead weight |
| **Lunge / charge / boarding tube / grapnel** | a built-in dash that **closes the gap** to strike | one-shot/cooldown, and **committing** — exposed if it whiffs |

**Physical demands (the forcing — reach & closing, not mass):**
- **No ammo, minimal power** → the lightest weapon-supply footprint in the game; this saved mass is melee's dividend.
- **Range ≈ 0** → the real cost. The weapon is gated by the unit's **mobility** (Propulsion) — melee "forces" a mobility/armour investment the way a gun forces a magazine/reactor. The trade is spatial, not tonnage.
- **Damage scales with chassis strength + Enhancers** → a bigger/stronger body hits harder (a Titan's fist, a power-armoured marine) — emergent from the host, not authored on the weapon.

**Modellability audit (§0d):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Melee = **undodgeable contact** | ✅ | **already built** — `GroundDamageMatrix` ("dodge beats aimed fire only, *not* area/melee") |
| Damage · strike speed | ✅ | firepower / rate |
| Reach (range 0/1) + must-close | ✅ | the ground H3 range model + the closing/movement system (a melee unit has to reach the target) |
| Crushing-ignores-armour · piercing-penetration | ✅ | `ArmourSoak` variants (a nature that beats flat armour) |
| Powered (energy blade) draw | ✅ | the supply model |
| Damage-scales-with-strength | ◐ **wire** | read the chassis strength/mass + Enhancer bonus into the melee damage term |
| Parry (melee) · Grapple→capture | ◐ **wire** | a melee-vs-melee defense mod; capture reuses the boarding-capture primitive (H9) |
| Lunge / charge closer | ◐ **wire** | a movement burst (movement system exists) |
| **Parry RANGED (the lightsaber deflect)** | ⏳ **defer** | needs the **effect-bus + a skill/People trait** (H4/H7) — the saber is gear, the deflect is the *being* |

**Reading:** melee is **strongly modellable** — the resolver *already* treats melee as undodgeable (`GroundDamageMatrix`), and the closing/range model already governs "you must reach the enemy." A few **wires** (strength-scaling, parry-melee, grapple-capture, lunge), and only the **lightsaber's ranged-deflect** is deferred to the effect-bus/People-trait work.

**Numbers (calibrated §0e):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **Damage / strike** | J (× strength) | knife ~2 kJ → power fist ~500 kJ → chainfist ~10 MJ → ram = ½mv² of the hull (GJ⁺) | joules; scales with chassis strength + Enhancers |
| **Strike rate** | strikes/s | 0.5 (heavy) → 5 (flurry) | firepower |
| **Reach** | m | 0 (fist) → ~5 (lance); ≈ 1 hex | must close (the range/closing model) |
| **Nature** | physical | **undodgeable** (Matchup ×1 vs evasion); shield soak **1.0** (shields wall melee) | `GroundDamageMatrix` (melee ignores dodge) |
| **Power** (energy blade) | W | small (powered) / 0 (unpowered) | supply |
| **Strength mult** | × | 1 (baseline) → high (Titan / power-armour) | chassis + Enhancers |

**Sanity-check:** a **500 kJ power-fist** strike, undodgeable, unshielded: 500 kJ × 0.1 = 50 kJ/salvo → **~10 strikes** to fell the 500 kJ frigate — *once it has closed to range 0.* Against a **0.9-evasion dodger** a gun lands ×0.1 but the fist lands ×1 → melee is **10× better vs dodgers**; against a **shield** it's soaked 1.0 → walled. So melee is the **anti-dodger, shield-walled, contact-gated** corner — devastating if it arrives, useless if it can't.

**Preset coordinates — the span:**
| Weapon | Reach | Mode | Power | The trade it chose |
|--------|-------|------|-------|--------------------|
| **Combat knife** | short | cutting | unpowered | free backup — zero supply, zero reach |
| **Power fist / chainsword** | short | crushing/cutting | powered | armour-cleaving alpha (the marine); must close, draws power |
| **Lightsaber** | short | energy | powered | cuts anything + parries ranged *(deferred)*; the saber is gear, the Force does the deflect |
| **Boarding claw** | short | grapple | powered | **captures** a ship intact; no damage, needs to dock |
| **Titan chainfist** | short | crushing | powered | wrecks a building/capital up close; needs a huge strong chassis to swing |
| **Ramming prow** | contact | piercing (kinetic) | unpowered | a hull *is* the weapon — massive one-shot; you must survive closing to zero range |

The melee lesson: it's the one door where the forcing isn't tonnage but **space** — the numbers don't funnel you to a bigger chassis, they punish you for not being able to *arrive*.

---

### 1.4 Weapons ▸ GUIDED  🔒 *locked*
*Self-propelled seeking munitions: missiles, torpedoes, drones, guided bombs. The **two-stage** door — you design a **launcher** (mounted on the chassis) AND a **projectile** (a mini-assembly with its own engine, seeker, and warhead that flies to the target and is consumed). This is where the designer **recurses**: a missile is a tiny unit built from the same category doors, at mini-scale.*

**The Guided identity (the standing trade):** enormous **range** + heavy **payload** + **fire-and-forget** — but every shot is a **costly consumable** that **takes time to arrive** and **can be shot down** in flight. So Guided doesn't force a bigger *chassis* so much as a bigger *economy* (keep building and hauling missiles — it ties Weapons to Industrial/Logistical), and it rewards **saturation** (alpha-strike to swamp the enemy's point-defense).

#### Stage 1 — the LAUNCHER (mounted on the host)
**A. Launch method**
| Option | Why | The catch |
|--------|-----|-----------|
| **Cell / VLS** | protected, reliable, reloads from the magazine | launch rate capped by cell count |
| **Rail / arm** | fast cheap salvo | exposed, slow reload |
| **Bay (drone launch)** | launches big **recoverable** projectiles | slow cycle, needs recovery |

**B. Salvo discipline**
| Option | Why | Catch |
|--------|-----|-------|
| **Trickle fire** | steady pressure, ammo lasts | PD **picks them off one by one** |
| **Alpha salvo** | **saturates point-defense** — too many to intercept | empties the magazine; a mis-timed salvo is a huge ammo/cost loss |

**C. Magazine size** — the ammo-mass cascade (like Ballistic): more missiles = more mass = bigger chassis; runs dry → resupply (`GroundAmmo` / ordnance logistics).

#### Stage 2 — the PROJECTILE (a mini-assembly — recurses the whole designer)
**D. Warhead** *(Weapons-in-miniature — what it delivers on arrival)*
| Option | Why | Catch |
|--------|-----|-------|
| **HE / blast** | reliable splash | soaked by armour/shields |
| **Shaped / AP** | cracks a capital's armour | wasted on soft/swarm |
| **Kinetic (torpedo)** | mass × velocity, nothing to soak | must actually connect |
| **Nuclear / antimatter** | fleet-clearing AoE | scarce, costly, **escalatory** (political weight) |
| **MIRV / submunitions** | splits → saturates PD, hits many | each sub is weak |
| **Special (EMP / ion / bio)** | disables instead of destroying | situational |

**E. Projectile propulsion** *(its engine → range vs survival)*
| Option | Why | Catch |
|--------|-----|-------|
| **Sprint (fast, short burn)** | crosses the gap fast → **less time to be intercepted/dodged** | short range |
| **Cruise (efficient, long burn)** | **strike from beyond return fire** | slow → a big interception/evasion window |

**F. Seeker / guidance** *(its sensor → how well it tracks)*
| Option | Why | Catch |
|--------|-----|-------|
| **Unguided ballistic** | cheap, **un-jammable** | only hits slow/dumb targets |
| **Active homing** | fire-and-forget, tracks maneuvering targets | **jammable/spoofable** (EW counter) + a seeker costs mass/money |
| **Command / wire-guided** | precise, jam-resistant | ties up your fire control; link-range-limited |

**G. Projectile survivability** *(vs point-defense)*
| Option | Why | Catch |
|--------|-----|-------|
| **Bare** | cheap, small, fast | PD swats it |
| **Hardened / evasive / stealthed** | survives to reach the target | heavier/costlier → fewer carried |

**H. Reusability** *(where Guided blurs into a carrier)*
| Option | Why | Catch |
|--------|-----|-------|
| **Expendable (missile)** | high performance, one-way | gone after one shot — a pure consumable |
| **Recoverable (drone/fighter)** | returns to rearm — amortized, persistent | slow cycle, needs a bay + recovery; a kill is a big loss. *(At the limit this **is** a Fighter — Chassis ▸ Hull + weapons — launched from a carrier bay, H6.)* |

**Physical demands (the forcing — economy + logistics, not chassis tonnage):**
- **Magazine mass** → finite shots, resupply; the mass cascade (like Ballistic).
- **Cost per shot** → each missile is a fully-built consumable (materials + build time) → **you cannot spam nukes**; Guided is throttled by your Industrial output, not your reactor. The launcher itself sips power (missiles are self-powered).
- **Missile mass vs count** → a big long-range nuke = few carried; a small sprint interceptor = many. The projectile's own design trades against how many fit in the magazine.

**Modellability audit (§0d):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Missile flight / impact | ✅ | **already built** — `OrdnanceDesign`, `MissileLauncherAtb`, `MissileImpactProcessor` (guidance fixed 2026-06-21, `ThrustToTargetCmd`) |
| Warhead types | ✅ | `OrdnancePayloadAtb` (explosive / shaped / submunitions in `ordnance.json`) |
| Projectile = mini-assembly (engine/seeker/warhead) | ✅ | `OrdnanceDesign` already assembles the three missile-part templates |
| Salvo saturates PD · PD interception | ✅ | the flak anti-missile saturation model (`FlakWeaponTests` — "the fighter/missile killer") |
| Cost-per-shot / build-and-haul | ✅ | ordnance rides the industry rails (`OrdnanceDesign : IConstructableDesign`) |
| Sprint-vs-cruise (time-to-target window) | ◐ **wire** | the closing/interception timing — missile speed vs PD engagement window |
| Recoverable drones | ◐ **wire** | launch/recover via the carrier bay (units-as-entities, H6) |
| **Seeker jamming / spoofing** | ⏳ **defer** | needs the **EW** door built (jam/spoof is itself a new capability) |

**Reading:** Guided is **the most-already-built weapon door** — Pulsar's ordnance system is real and functional (design a missile, launch it, it flies and impacts), and the projectile is *already* a mini-assembly. The saturation-vs-PD dynamic maps to the flak model. Wires: interception timing, drone recovery. Only **seeker jamming** waits — on the EW door.

**Numbers (calibrated §0e — anchored to the live missile stubs):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **Warhead output** | J | ASM ~5 MJ → torpedo ~1 GJ → nuke 10¹²⁺ J → MIRV = N×small | joules (`MissileLauncherFirepowerStub 100 kJ` → GJ kinetic on impact) |
| **Projectile velocity** | m/s | sprint ~10⁴ · cruise slower (`MissileVelocityStub 5000`) | below beam threshold → **interceptable/dodgeable** (the point) |
| **Range** | m | 1000 km (`MissileRange_m`) → strategic farther | range ladder |
| **Missile mass** | kg | 100s kg → tons | magazine cascade + fewer-carried tradeoff |
| **Salvo size** | count | alpha **> 50** saturates PD | `SaturationReference 50` |
| **Cost / shot** | materials + build-time | each a built consumable | the **economy** forcing (not chassis) |
| **Seeker tracking** | 0–1 | 0.9 homing (`MissileTrackingStub`) · 0 unguided | vs evasion; jammable (EW) |
| **Warhead nature** | HE 0.75 · kinetic 1.0 · nuke exotic-ish | shield soak | `ShieldSoakVsExplosive 0.75` |

**Sanity-check:** an **ASM ~5 MJ warhead** reaching the 500 kJ frigate: 5 MJ × 0.1 = 500 kJ → **one-shots it** (matches the live "missiles one-shot" calibration note). *But* it can be **intercepted in flight** (flak saturation vs the missile's own evasion), it **costs** a whole built missile, and it **arrives late** (flight time). So Guided is balanced **not by the joule scale but by point-defense + economy + delay** — huge alpha you have to *afford, protect, and time.*

**Preset coordinates — the span:**
| Weapon | Warhead | Engine | Seeker | The trade it chose |
|--------|---------|--------|--------|--------------------|
| **Anti-ship missile** | shaped | cruise | homing | standoff strike; slow enough to intercept |
| **Interceptor (PD missile)** | small HE | sprint | homing | kills other missiles/fighters; short range |
| **Torpedo (photon/proton)** | kinetic/big | short | homing | capital-cracker; must survive to connect |
| **Nuke / antimatter missile** | nuclear | cruise | homing | fleet-clearing; scarce + escalatory |
| **MIRV** | submunitions | cruise | homing | swamps PD; weak per sub |
| **Drone / fighter** | (its own guns) | reusable | its own sensors | persistent, returns to rearm — **it's a Fighter** (carrier link) |

The Guided lesson: it's the door that recurses (a missile is a mini-unit) and the one forced by your **factories**, not your frame — the constraint is how fast you can *build and replace* what you fire.

---

### 1.5 Weapons ▸ EXOTIC  🔒 *locked*
*Everything that doesn't obey the other four families' physics: gravitic, EMP, temporal, bio, warp-disruption, mind-control, annihilation. The key reframe: **Exotic is not a weapon type — it's the extensibility slot.** The other four doors deal conventional **damage**; an Exotic weapon delivers an **effect from the shared EFFECT BUS** (`COMPONENT-DESIGNER-STRESS-TEST.md` §2) — disable, drain, grab, capture, phase, kill-crew — usually *instead of* HP damage. Its dials **compose an effect**, so adding a new sci-fi weapon later = adding an effect to the bus and exposing it here, **never a new door.***

**The Exotic identity (the standing trade):** hyper-specialist and late-game. An Exotic weapon is typically **"I win vs X, useless vs Y"** — the most extreme anti-dominance in the game — gated by heavy **tech**, and usually carrying a **cost/instability** or **political** price. It's the end-game frontier, and (honestly) the **least-built** door — most of its effects are ⏳ *build-the-mechanic-first*.

**A. Effect — what it does (drawn from the effect bus; this is the core dial)**
| Effect | What it does | Countered by |
|--------|-------------|--------------|
| **Disable / EMP** | shuts a subsystem (power/shields/sensors) down for a duration | Defense ▸ Hardening |
| **Drain** | steals energy/shields/ammo → to you | insulation/shielding |
| **Gravitic / tractor** | grab, pin, pull, crush — move or immobilize a target | mass + engine thrust to break free |
| **Warp-disruption (interdictor)** | prevents FTL/jump — pins a fleet in realspace (can't flee) | a counter-field / anchor |
| **Temporal** | slows the target's action-rate (time dilation) | temporal shielding |
| **Conversion / subversion** | flips the target to your side (assimilate, mind-control) | counter-intel / firewalls |
| **Bio / plague** | kills crew/population, spreads, leaves the hull intact | quarantine / sealed environments |
| **Annihilation** | deletes matter — bypasses armour AND shields | *(nothing — but extreme cost)* |

**B. Delivery** *(the effect is portable — it can ride other doors' delivery)*
| Option | Why | Catch |
|--------|-----|-------|
| **Beam / field (direct)** | precise, immediate | LOS + range-limited |
| **Area / burst** | hits a whole zone | **indiscriminate** — friendly-fire risk |
| **Loaded on a Guided projectile** | a plague-missile, an EMP-torpedo — reach + fire-and-forget | interceptable; the effect bus makes effects portable across delivery |
| **Persistent zone** | a lingering field (grav-well, temporal minefield) | static, telegraphed |

**C. Persistence**
| Option | Why | Catch |
|--------|-----|-------|
| **Pulse** | one-shot | wears off — must re-apply |
| **Sustained** | holds while maintained | ties up the weapon + power |
| **Permanent** | irreversible (annihilate, convert) | huge cost |

**D. Instability — the exotic tax**
| Option | Why | Catch |
|--------|-----|-------|
| **Stable** | reliable | weaker effect |
| **Unstable / overload** | devastating | **backlash** — self-damage, AoE-on-self, or one-use |

**E. Selectivity — the matchup skew (Exotic's signature)**
| Option | Why | Catch |
|--------|-----|-------|
| **Broad** | affects most targets | mild effect |
| **Specialist** | *devastating* vs its type (anti-machine / anti-organic / anti-shield / anti-FTL) | **no effect at all** vs everything else |

**F. Escalation — the political price** *(some exotics have consequences past the battle)*
| Option | Why | Catch |
|--------|-----|-------|
| **Conventional** | no fallout | — |
| **Taboo (bio / temporal / annihilation)** | wins the fight | **diplomatic blowback** — using it turns others against you (a casus belli), via the Diplomacy/Government systems |

**Physical demands (the forcing — tech + instability + politics, not tonnage):**
Exotic's gate is rarely mass — it's **research** (deep tech to unlock the effect), **instability** (backlash risk), and sometimes **diplomatic cost** (taboo weapons). A few (annihilation, gravitic) still carry enormous power/mass demands that cascade the chassis like Energy.

**Modellability audit (§0d) — Exotic is where the deferrals live, and that's correct:**
| Effect | Verdict | Prerequisite / how |
|--------|---------|--------------------|
| Warp-disruption (interdictor) | ◐ **wire** | Pulsar has jump/warp mechanics (`JumpPoints`) — hook a jump-inhibit field |
| Conversion / subversion | ◐ **wire** | reuse the **capture** primitive (owner-flip, H9) |
| Bio / plague | ◐ **wire** | crew/population attrition **exists** (colony damage, morale); the *spread* is new |
| Annihilation | ◐ **wire** | a bypass-all damage nature (like Energy's graviton) |
| Escalation / taboo | ◐ **wire** | Diplomacy exists (`DiplomacyDB`, casus belli) — hook "used a WMD → relation hit" |
| Instability / backlash | ◐ **wire** | a self-damage rule (like overcharge) |
| **Disable / EMP** | ⏳ **defer** | needs a **temporary-subsystem-debuff (status)** system — new |
| **Drain** | ⏳ **defer** | needs **resource-transfer-between-units** — new |
| **Gravitic / tractor** | ⏳ **defer** | needs the **grab/push effect** (move another unit) — the effect bus, new |
| **Temporal** | ⏳ **defer** | needs an **action-rate debuff** — new |

**Reading:** Exotic is **honestly the least-ready door** — several effects need a new mechanic first (a status/debuff system, resource-transfer, the grab effect, action-rate dilation). That is the *right* answer: Exotic is the late-game frontier, and the modellability test turns each effect into an explicit build item, gated behind the **effect bus**. Exotic is future-proof *by design* — new effects slot in without new doors.

**Numbers (calibrated §0e — most finalize *with* their prerequisite mechanic; the framework is set now):**
| Effect | Magnitude unit | Anchor / note |
|--------|----------------|---------------|
| **Annihilation** | J, **Exotic nature (shield soak 0.0)** + bypasses armour | the one pure-damage exotic — anchors cleanly to the joule scale, ignores all defence (`ShieldSoakVsExotic 0.0`) |
| **Gravitic** | N (force) · m (pull/pin) | vs target mass × engine thrust — pins if force > thrust (deferred: grab effect) |
| **Disable / EMP** | seconds (subsystem offline) | deferred: needs the status/debuff mechanic |
| **Drain** | J/s transferred | deferred: resource-transfer mechanic |
| **Temporal** | × action-rate (0–1) | deferred: action-rate debuff |
| **Conversion** | probability 0–1 per hit | reuses the capture primitive |
| **Plague** | crew-kills/s + spread rate | partial: colony/crew attrition exists |
| **Gate (all exotics)** | tech-level (very high) + instability = backlash self-damage (J) | tech is the primary gate, not mass |

**Sanity-check (the one damage-exotic):** an **annihilation beam ~2 MJ, Exotic nature** vs the 500 kJ frigate — shields soak **0.0** and armour is bypassed, so it lands the full 2 MJ × 0.1 = **200 kJ/salvo → ~3 salvos, and *nothing* the target builds reduces it.** That's why Exotic is gated by **ruinous tech + instability** rather than the matchup: it's the "no counter, so make it rare and costly" corner. The effect-only exotics (EMP/drain/grab/temporal) get their numbers when their mechanic lands — the calibration *slot* is reserved, not filled.

**Preset coordinates — the span:**
| Weapon | Effect | Delivery | The trade it chose |
|--------|--------|----------|--------------------|
| **Tractor beam** | gravitic grab | beam | pins/pulls a target; no damage, needs LOS + power |
| **EMP cannon** | disable | burst | shuts down systems; nothing vs hardened, wears off |
| **Interdictor** | warp-disruption | persistent zone | stops the enemy fleeing; static + telegraphed |
| **Stasis projector** | temporal | beam | freezes a threat; extreme tech, sustained |
| **Plague warhead** | bio | Guided projectile | kills crews, takes the hull intact; **taboo** (diplomatic blowback) |
| **Mind-control ray** | conversion | beam | steals a unit; countered by counter-intel |
| **Disintegrator** | annihilation | beam | deletes matter, bypasses all defense; ruinous cost + instability |

The Exotic lesson: it's the door that **grows with the game** — every deferred effect is a future mechanic, and once the effect bus exists, "invent a weird weapon" becomes "pick an effect + a delivery + a cost," with no new category ever needed.

---

## ✅ Weapons category — COMPLETE (5/5 doors 🔒 LOCKED)
Energy 🔒 · Ballistic 🔒 · Melee 🔒 · Guided 🔒 · Exotic 🔒. **The pattern for the other 32 doors is now set:** for each door — (1) dials with *every option justified* (anti-dominance), (2) the *physical forcing* (what number funnels the build — power/ammo/recoil/reach/economy/tech), (3) the *modellability audit* (§0d — Modelled / Wire / Defer), (4) a *preset table* proving the franchise span. The four concrete doors are grounded in real physics + the existing combat resolver; the fifth (Exotic) is the open extensibility slot that grows with the effect bus.

*(All 11 categories are now locked; each carries its own ⚙ Wiring Dossier below.)*

---

## ⚙ 1 — WEAPONS · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

The Weapons category has **five doors** — Energy · Ballistic · Melee · Guided · Exotic. All four conventional doors deal **damage** through one shared salvo resolver; Exotic is the **extensibility slot** that delivers an *effect from the effect bus* (disable/drain/capture/convert/bio/belief) usually *instead of* HP damage. The core finding: a weapon dial is authentic **only if it writes the resolver's input surface** — the 7-field `WeaponProfile` + a few per-ship values (§0i). Most weapon dials already do; the rest need a **named new `WeaponProfile` field + one resolver term** (a short, ordered backlog, Penetration first) or a **deferred subsystem**.

---

### Pillar tags (§0h) — each door → (pillar · skeleton-role)

| Door | pillar · skeleton-role | Graded against which resolver (§0i) |
|------|------------------------|--------------------------------------|
| **Energy** | Military · **Projection** | combat salvo resolver (`CombatEngagement`/`CombatKernel`) |
| **Ballistic** | Military · **Projection** | combat salvo resolver (+ ammo economy) |
| **Melee** | Military · **Projection** | combat salvo resolver (undodgeable path) / ground `GroundDamageMatrix` |
| **Guided** | Military · **Projection** | combat salvo resolver (+ ordnance/industry economy) |
| **Exotic — pure-damage effects** (annihilation) | Military · **Projection** | combat salvo resolver (`Nature=Exotic`, shield soak 0.0) |
| **Exotic — bioweapon/plague effect** | **Espionage** · **Projection** | population/legitimacy resolver (`OnColonyDamage` + morale/`Legitimacy`), NOT combat |
| **Exotic — SWAY belief-pressure effect** | **Influence** · **Projection** | legitimacy/rebellion resolver (`ComputeLegitimacy` → `RebellionDB`), NOT combat |

The ⛓ **counter** for every Projection door (§0h co-required slot) lives in the **Defense** category, not Weapons — Energy/Ballistic/Melee/Guided are countered by armor/shields/PD; the Exotic bio/belief projectors are countered by Hardening (sensor/bio hardening → Cultural Insulation) and counter-intel/censorship (EW). Anti-dominance law: you cannot ship a projector without naming the insulation that beats it.

---

### Complete dial → derived-stat → engine-wire table (VERIFIED)

Grade: ✅ resolver reads it today · ◐ small wire to an existing adjacent system · ⏳ deferred (needs a prerequisite subsystem). Resolver-insertion legend: **✅ field** = writes an existing `WeaponProfile`/per-ship field · **➕** = needs a named new field + one resolver term · **⚙** = deferred mechanic.

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | Resolver insertion |
|------|------|--------------|-------|--------------------------|---------------------|
| Shared | Output (J/shot) × Rate | DamagePerSecond (J/s) + Saturation (rate) | ✅ | `ShipCombatValueDB.cs:295` sums `w.DamagePerSecond`→Firepower; beam dps `:184` | ✅ `DamagePerSecond` + `Saturation` |
| Shared | Tracking / accuracy | Tracking 0..1 | ✅ | `WeaponProfile.cs:87`; read `CombatKernel.cs:147` HitFraction | ✅ `Tracking` |
| Shared | Range / standoff | Range_m (m; 0=unbounded) | ✅ | `WeaponProfile.cs:99`; gate `CombatEngagement.cs:574` BuildFireMix | ✅ `Range_m` (+ range term) |
| Shared | Mount / traverse arc | which targets bear | ⏳ | — (aggregate resolver is non-positional) | ⚙ positional (or drop as flavour) |
| Shared | Firing modes / effect-bus (stun→capture) | non-lethal effect | ⏳ | — | ⚙ effect bus + capture primitive |
| **Energy** | Delivery: Beam/Bolt/Scatter | Velocity + Delivery + Saturation | ✅ | beam profile `ShipCombatValueDB.cs:184` (`Delivery.Beam`); classify `WeaponClassifier.cs:33,41` | ✅ `Velocity`/`Delivery` |
| Energy | Nature: thermal/ion/plasma/graviton | Nature → shield soak | ✅ | `WeaponProfile.cs:75`; soak `CombatKernel.cs:135-139`; ion=Exotic bypass, disruptor `ShipCombatValueDB.cs:238` | ✅ `Nature` |
| Energy | Focus: lance ↔ wide cone | wide→Saturation; lance→Penetration | ◐ (wide ✅ / lance ➕) | wide = saturation ✅; lance armour-pen has **no field** | ➕ **Penetration** |
| Energy | Penetration ↔ Splash | armour-crack vs anti-swarm | ➕ | `CombatKernel.ArmourSoak(armour, sourceDamage)` `:224` has **NO per-weapon penetration arg** (verified — grep found no `Penetration` in Combat/ or Weapons/) | ➕ **Penetration** (armour-pen term) |
| Energy | Linked fire / per-shot alpha | PerShotEnergy | ➕ | `BuildFireMix` `CombatEngagement.cs:1005` aggregates dps and **loses per-shot size** — a beam and a repeater of equal dps are indistinguishable to armour | ➕ **PerShotEnergy** |
| Energy | Charge damage profile (hi-alpha/lo-rate) | high dps / low saturation | ✅ | represented natively by dps×saturation | ✅ (dials) |
| Energy | Charge telegraph window | per-shot timing | ⏳ | — (salvo resolver isn't per-shot-timed) | ⚙ per-shot timing |
| Energy | Overcharge / burnout | self-damage on fire | ⏳ | damage system removes components (`DamageProcessor`) but no self-damage rule | ⚙ self-damage rule |
| Energy | Frequency modulation | vs adaptive shields | ⏳ | no adaptive-shield mechanic (H2b) | ⚙ adaptive shields |
| Energy | Cooling / heat → sustained rate | effective dps under heat | ◐ | resolver reads rate; needs a heat term | ➕ heat/rate term |
| Energy | Thermal bloom / signature | firing-heat → detection | ◐ | EMCON already reads `ShotsFiredThisTick`→activity (`EmconActivityProcessor`) | — detection-side (correct) |
| Energy | Efficiency (dmg/watt) | power draw → mass | ✅ | build-side: power→generator mass, funnels chassis (§0b) | — build-side (correct) |
| Energy | Medium (vacuum/atmo/water) | output/range by medium | ⏳ | space combat doesn't tag a medium | ⚙ environment modifier |
| Energy | Point-defense capability | intercept incoming | ◐ | flak anti-missile role + `MissileImpactProcessor` exist; needs PD-targeting hook | ⚙ missiles-as-targets |
| **Ballistic** | Projectile: slug/HE/sabot/flak | Nature(kinetic)+pen/splash+flak sat | ✅ | flak profile `ShipCombatValueDB.cs:221` (`Delivery.Cloud`, saturation=rof×pellets); railgun slug `:204` | ✅ (splash→Saturation; **AP/sabot→Penetration** ➕) |
| Ballistic | Muzzle velocity | Velocity → dodgeability | ✅ | railgun `ShipCombatValueDB.cs:204` finite `MuzzleVelocity_mps` → `Delivery.Slug` (dodgeable) | ✅ `Velocity` |
| Ballistic | Rate / caliber | firepower + saturation | ✅ | dps=energy×rof, saturation=rof `:204` | ✅ |
| Ballistic | **Ammo mass / runs-dry** | magazine depletion | ✅ (ground) / ➕ (space) | **built ground-side**: `GroundAmmo.cs:19` CarriesAmmo, `:23` IsDry, `MaxAmmo_kg`; space stepped resolve does **not** drain a pool | ➕ **AmmoPool** in `StepEngagementGroup` |
| Ballistic | Fuzing: proximity/airburst | saturation-floors-dodge | ✅ | the flak model | ✅ `Saturation` |
| Ballistic | Recoil → accuracy | eff. Tracking −= recoil÷mass | ➕ | resolver reads Tracking; no recoil÷chassis-mass term | ➕ recoil→Tracking term |
| Ballistic | Multi-ammo switch (AP/HE/flak) | swap active profile | ◐ | resolver reads one `WeaponProfile` at a time | ⚙ profile-swap |
| Ballistic | Recoilless (energy bleed) | range/pen reduction | ◐ | a range/pen term | ➕ small term |
| Ballistic | Indirect / arcing + spotter | over-cover fire | ⏳ | ground has terrain+hexes (partial); space has no LOS block | ⚙ LOS/terrain + spotter |
| Ballistic | Power (hyper-velocity railgun) | dual-supply cascade | ✅ | supply model rates railgun as ammo+power (build-side) | — build-side (correct) |
| **Melee** | Melee = undodgeable contact | Matchup ×1 vs evasion | ✅ | **built**: `GroundDamageMatrix.cs:63` Matchup (dodge beats aimed fire only, not melee); space = Velocity≈∞-equiv/`Delivery` | ✅ (ground matrix) |
| Melee | Damage · strike speed | firepower / rate | ✅ | firepower/saturation | ✅ |
| Melee | Reach (0/1) + must-close | range/closing gate | ✅ | closing model + `Range_m≈0`; ground H3 range | ✅ `Range_m` |
| Melee | Crushing-ignores-armour · piercing | armour-beating nature | ➕ | rides `ArmourSoak` `CombatKernel.cs:224` — same missing Penetration field | ➕ **Penetration** |
| Melee | Damage-scales-with-strength | chassis strength/mass + Enhancer | ◐ | read chassis strength/mass + Enhancer bonus into the melee damage term | ➕ strength term |
| Melee | Parry (melee) · Grapple→capture | melee-defense mod / capture | ◐ | melee-vs-melee mod; capture reuses boarding primitive (H9) | ⚙ effect bus + capture |
| Melee | Lunge / charge closer | movement burst | ◐ | movement system exists | ◐ movement-side |
| Melee | Parry RANGED (lightsaber deflect) | deflect incoming fire | ⏳ | the saber is gear; the deflect is a **People/pilot trait** (H4/H7) | ⚙ effect bus + People trait |
| **Guided** | Warhead output · seeker tracking · range | dps / Tracking / Range_m | ✅ (stub→real) | missile profile `ShipCombatValueDB.cs:269` **v1 stub** (`MissileLauncherFirepowerStub 100k`, `:38`); `OrdnanceDesign`/`MissileImpactProcessor` real | ✅ fields (wire real values) |
| Guided | Warhead types (HE/shaped/MIRV/special) | Nature + payload | ✅ | `OrdnancePayloadAtb` / `ordnance.json`; missile Nature=Explosive `:269` | ✅ `Nature` |
| Guided | Projectile = mini-assembly | engine/seeker/warhead | ✅ | `OrdnanceDesign` assembles the three missile-part templates | ✅ |
| Guided | Salvo size vs PD | Saturation vs `SaturationReference 50` | ✅ | flak saturation model (`FlakWeaponTests`); ref `CombatEngagement.cs:56` | ✅ `Saturation` (proxy) |
| Guided | Ammo / runs-dry mid-battle | magazine drain | ➕ | ground `GroundAmmo` exists; space resolve doesn't drain | ➕ **AmmoPool** |
| Guided | Sprint vs cruise (time-to-target) | interception window | ◐ | closing/interception timing — missile speed vs PD window | ◐ closing-timing wire |
| Guided | PD intercepts a missile | missiles as targets | ⏳ | missiles are a firepower stub, not resolvable projectiles; flak saturation is a proxy | ⚙ missiles-as-targets + PD-capable flag |
| Guided | Recoverable drone → fighter | launch/recover | ◐ | carrier bay, units-as-entities (H6) | ◐ carrier-bay wire |
| Guided | Seeker jamming / spoofing | vs EW | ⏳ | needs the EW door built | ⚙ EW door |
| **Exotic** | Effect (from the effect bus) | disable/drain/grab/convert/… | ⏳ | most need a status/debuff, resource-transfer, or grab mechanic | ⚙ effect bus |
| Exotic | Annihilation (pure damage) | Nature=Exotic, bypass armour+shield | ✅ | shield soak 0.0 `CombatKernel.cs:68`; anchors to joule scale | ✅ `Nature=Exotic` (+ armour-bypass) |
| Exotic | Selectivity (species-specific / anti-type) | matchup skew | ◐ | the effect-bus Selectivity dial; H9 convert = `Selectivity=convert` | ➕ Selectivity/effect-bus |
| Exotic | Warp-disruption (interdictor) | jump-inhibit | ◐ | Pulsar has `JumpPoints` — hook a jump-inhibit field | ◐ jump-inhibit wire |
| Exotic | Conversion / subversion | owner-flip (H9) | ◐ | reuse the **capture** primitive (owner-flip) | ⚙ capture + effect bus |
| Exotic | Escalation / taboo (WMD) | relation hit → casus belli | ◐ | `DiplomacyDB` casus belli exists — hook "used a WMD → relation crater" | ◐ diplomacy wire |
| **Exotic — bioweapon warhead** (E1d) | **Selectivity** (species-specific strain) | matchup vs a pop's insulation | ◐ | gated by net-new Biology & Genetics tech; effect lands `DamageProcessor.OnColonyDamage:289` + morale/legitimacy | ➕ effect-bus (population resolver, NOT combat §0f) |
| Exotic — bioweapon | **incubation** (delay to onset) | latency | ◐ | candidate new sub-dial; rides `OnColonyDamage` casualty pass | ⚙ status/timer on the population resolver |
| Exotic — bioweapon | **detectability** (deniability) | traced/caught → blowback | ◐ | blowback via `DiplomacyDB` (relation crater, casus belli); delivery rides Guided §1.4 or infiltration bay | ◐ diplomacy + espionage-detection wire |
| **Exotic — SWAY belief-effect** (E1c) | belief-pressure (flavor × zone × magnitude×radius) | `ExternalBeliefPressure` | ⏳ | **new field** on `LegitimacyInputs` (`LegitimacyDB.cs:119` — verified NOT present today, beside `GovernorCompetence:128`) → subtracted in `ComputeLegitimacy:66` → toward `CollapseThreshold 20` (`:42`) → `RebellionDB` (`LegitimacyProcessor.cs:73`) = the kill | ➕ `ExternalBeliefPressure` on the **legitimacy resolver** (never touches combat §0f) |

---

### Resolver insertion points (from the anatomy, adapted to Weapons)

The salvo resolver reads a **fixed input surface**; a Weapons dial is authentic only if it writes it.

**✅ Fields the resolver writes/reads today (the fight-deciding core — fully wired):**
- Per-weapon `WeaponProfile` (7 fields, `WeaponProfile.cs`): **DamagePerSecond** `:81` · **Velocity** `:84` · **Tracking** `:87` · **Saturation** `:91` · **Range_m** `:99` · **Nature** `:75` · **Delivery** `:78` (+ computed **`Class`** `:72` via `WeaponClassifier.Classify` `:29`).
- Per-ship: **Firepower** (`ShipCombatValueDB.cs:295`, Σ dps) · **Toughness** (`:106`, components `ComponentHitPoints_J 100k :81` + armour `:291`) · **Evasion** (`CalculateEvasion :314`) · **RoleWeight** · **ShieldCapacity_J/Regen** (`:304`, Σ `ShieldAtb`).
- Resolver math (single source of truth in `CombatKernel.cs`, ship path delegates): **HitFraction** `:147` · **LandedFraction** `:180` · **SoakFractionOf** `:188` (nature→shield: Kinetic 1.0 `:62` / Energy 0.5 `:64` / Explosive 0.75 `:66` / Exotic 0.0 `:68`) · **ResolveShield** · **ArmourSoak** `:224` (flat, `ArmourSoakPerPoint 1.5 :75`). `BuildFireMix` (`CombatEngagement.cs:1005`) buckets fire by `(Class, Nature, Delivery)` `:1013` so the matchup survives the O(buckets) aggregation. Pace dial `SalvoDamageScale 0.1` `:94`.

**➕ New `WeaponProfile` fields / resolver terms needed (the concrete backlog — ordered by payoff):**
1. **`Penetration` field + `ArmourSoak` penetration term.** ✅ **FIELD + KERNEL TERM LANDED (Weapons pilot W1a, 2026-07-10).** `WeaponProfile.Penetration` now exists (serialized, both ctors) and `CombatKernel.ArmourSoak(armour, sourceDamage, penetration)` cancels armour point-for-point before the flat soak (the 2-arg overload forwards with penetration 0 → byte-identical; gauge `CombatKernelTests.ArmourSoak_Penetration_CancelsArmourPointForPoint`). **W1b WIRED (2026-07-10):** `GroundUnit.Penetration` (snapshot) → `GroundCombatant.ToWeaponProfile` → the ground resolver passes `profile.Penetration` into `GroundDamageMatrix.ArmourSoak(def, dmg, pen)` → the kernel 3-arg (`GroundForcesProcessor.cs:348`). Every fielded unit defaults 0 → live combat byte-identical; an AP unit cracks plate a normal round bounces off (gauge `GroundKernelBridgeTests.Penetration_FlowsToTheProfile_AndCracksArmourThroughTheGroundSoak`). **W1c CRADLE-TO-GRAVE (2026-07-10) — DONE:** `GroundUnitAtb` gained a `Penetration` ctor arg + a template dial (six-point base-mod chain); it flows `template → GroundUnitDesign.Penetration → RaiseUnit → GroundUnit.Penetration`. The base-mod **Armor** unit now carries AP penetration 20 (a tank's main gun), artillery 8, infantry 0 (flagged balance numbers) — so a *player-built* armour-cracker is real end-to-end: designed → built → deployed → beats plate that stops infantry (gauge `GroundUnitBaseModTests.BuildingAnArmorUnit_CarriesPenetration_ThatCracksArmour`). **✅ The whole Penetration backlog item is now built (kernel + ground wire + cradle-to-grave).** Follow-ups: the *assembled*-design weapon path (ground-cannon/autocannon compute penetration from the part) and the ship per-source armour reconcile (so penetration reaches ship-vs-ship). **Unlocks: lance/sabot/AP/piercing (Energy, Ballistic, Melee) as real armour-crackers, Splash as the anti-swarm opposite. Highest payoff — the armour half of the matchup.** *(Note: bites on the ground/garrison path first — the ship salvo folds armour into Toughness, so penetration reaches ships only when the per-source armour reconcile lands, a separate flagged follow-up.)*
2. **`PerShotEnergy` field.** ✅ **FIELD + KERNEL TERM LANDED (Weapons pilot W2a, 2026-07-10).** `WeaponProfile.PerShotEnergy` now exists (serialized, both ctors); `CombatKernel.BurstShotCount(w)` = dps ÷ PerShotEnergy clamped [1, `BurstSoakMaxShots` 1000], and `CombatKernel.ArmourSoakBurst(armour, sourceDamage, shotCount, penetration)` splits a source into that many equal shots and soaks each flat — so a swarm of chips is mostly bounced while one alpha of EQUAL total punches through (gauge `CombatKernelTests.ArmourSoakBurst_AlphaPunches_ChipBounces`). shotCount ≤ 1 / PerShotEnergy 0 is byte-for-byte the flat soak → un-dialled weapons unchanged. **W2b WIRED (2026-07-10):** `GroundUnit.PerShotEnergy` (snapshot) → `GroundCombatant.ToWeaponProfile` → the ground resolver soaks each attacker's contribution via `GroundDamageMatrix.ArmourSoak(def, dmg, BurstShotCount(profile), pen)` → the kernel burst soak (`GroundForcesProcessor.cs:348`). Every fielded unit still has PerShotEnergy 0 → shotCount 1 → live combat byte-identical; an alpha unit cracks plate a chip unit of equal Attack bounces off (gauge `GroundKernelBridgeTests.PerShotEnergy_FlowsToTheProfile_AlphaPunchesChipBouncesThroughGroundSoak`). **W2c CRADLE-TO-GRAVE DONE (2026-07-10):** `GroundUnitAtb`/`GroundUnitDesign` carry PerShotEnergy → `GroundUnit.PerShotEnergy` at raise; the three base-mod ground-unit templates set it (infantry 10 → 10 chips, armor 140 → 1 alpha, artillery 80 → 2 shells), so a **player-built tank's main gun alphas** through plate the infantry's small-arms chip-swarm bounces off (gauge `GroundUnitBaseModTests.BuildingAnArmorUnit_CarriesAlphaPerShot_...`). **PerShotEnergy item COMPLETE** (all three slices green). **Unlocks: Linked-fire, charge-alpha.** *(Bites on the ground/garrison path first, like Penetration — the ship salvo folds armour into Toughness.)*
3. **Mid-battle `AmmoPool` drain in `StepEngagementGroup`.** Ground has it (`GroundAmmo.MaxAmmo_kg`/`IsDry` `:19,:23`); the space stepped resolve never dries a magazine. ✅ **FOUNDATION LANDED (Weapons pilot W3a, 2026-07-10):** the space echo of the ground magazine now exists — `Combat.ShipMagazineAtb` (the ammo store component, mirror of `GroundMagazineAtb`), `ShipCombatValueDB.AmmoCapacity_kg` (sum of installed magazines, health-scaled, 0 if none), and `FleetCombatStateDB.AmmoPool_kg` (-1 unseeded, mirroring the shield pool). **Byte-identical** — a ship with no magazine reads 0 capacity → the pool is disabled → the resolve is untouched (gauge `ShipAmmoTests`). **W3b WIRED (2026-07-10):** `StepEngagementGroup` now has an AMMO pass before the damage phase — each fleet's Kinetic/Explosive (ammo-fed) fire drains its `AmmoPool_kg` by `(ammo J/s × dt) × AmmoBurnKgPerJoule`; when dry, `SilenceAmmoWeapons` drops those profiles from its outgoing fire so it fights on with energy weapons only. Gated on `FleetAmmoCapacity>0` → magazine-less fleets stay byte-identical (gauge: a small-magazine kinetic attacker dries and the defender survives, while the same no-magazine attacker wipes it — ammo is the only variable). **W3c CRADLE-TO-GRAVE DONE (2026-07-10):** the buildable base-mod `ship-magazine` component (six-point registration: `weapons.json` template with an Ammo-Capacity dial → `ShipMagazineAtb` + `componentDesigns.json` + earth.json StartingItems/ComponentDesigns) mounted on a NEW example ship, the **Sabre Munitions Cruiser** (railguns + a 5000 kg magazine on a heavy hull — added as a new ship like the shield's Bastion, so no existing battle fixture is perturbed). A player-built Sabre carries a finite magazine the resolver depletes (gauge `ShipAmmoTests.TheMunitionsCruiser_CarriesARealMagazine_FromJson`). **AmmoPool item COMPLETE** (all three slices green). **Unlocks: Ballistic/Guided depletion, resupply-as-pressure.** *(Follow-up: in-combat resupply — the ground side has `GroundForces.ResupplyUnit`; the ship equivalent (a supply ship / friendly-space refill) is a flagged next step.)*
4. **Recoil → Tracking term.** Effective Tracking −= f(recoil ÷ chassis-mass). ✅ **MECHANISM LANDED (Weapons pilot W4a, 2026-07-10):** `RailgunWeaponAtb.Recoil`/`FlakWeaponAtb.Recoil` (the recoiling kinetic weapons; beams have no recoil) + `ShipCombatValueDB.RecoilTrackingFactor(recoil, chassisMass)` = `mass / (mass + recoil × RecoilTrackingReference)`, applied to the railgun/flak profile's Tracking at BUILD (where the firer's mass is known — no resolver/kernel change). **Byte-identical** — every base-mod kinetic weapon has Recoil 0 → factor 1.0 → tracking unchanged (gauge `ShipRecoilTests`: the math + a built Lancer's railgun tracking is unchanged). **W4b CRADLE-TO-GRAVE DONE (2026-07-10):** a base-mod **`siege-railgun`** (a heavy railgun template with a Recoil dial 50000, six-point registration) mounted on a NEW ship, the **Bombard Siege Cruiser** (light-ish hull — like the ammo Sabre, a new ship so no existing fixture is perturbed). A player-built Bombard's siege gun tracks worse by exactly `mass/(mass+recoil)` (gauge `ShipRecoilTests.BuildingTheBombard_...`). **Recoil item COMPLETE** (both slices green). *(Also surfaced + fixed a real engine gotcha: a reflection-bound weapon-atb ctor needs an exact-arity overload — an optional param doesn't count; `Weapons/CLAUDE.md` #0.)* **Unlocks: Ballistic recoil** — a heavy gun on a light hull tracks worse than on a battleship.
5. **Heat → sustained-rate term.** Throttle effective dps under sustained fire. **Unlocks: Energy cooling, burst-vs-sustained.**
6. **Missiles as resolvable targets + PD-capable flag.** Missiles are a firepower stub (`ShipCombatValueDB.cs:269`), not projectiles the salvo loop can shoot down. **Unlocks: real point-defense, the salvo-vs-PD duel.**
7. **`Selectivity`/effect-bus tag** (Exotic) — carries the target-facet + effect, graded against the *right* resolver per its pillar tag (§0h). Fills **H9** as `Selectivity=convert`.

**⚙ Deferred mechanics (each gates its dials):** adaptive shields (→ frequency modulation) · combat-environment/medium modifier · per-shot timing (→ charge telegraph) · self-damage rule (→ overcharge/instability) · profile-swap (→ multi-ammo) · positional/arc (→ mount traverse, or drop) · the **effect bus + capture primitive** (→ stun/convert/grab/EMP/drain/temporal, and Melee grapple-capture + lightsaber ranged-deflect).

> **The resolver-MERGE caveat (from AUTO-RESOLVER-ANATOMY §6):** ships resolve **bucketed O(buckets)** through `CombatEngagement`/`CombatKernel`; soldiers resolve through a **parallel, un-bucketed O(units²)** `GroundForcesProcessor.ResolveRegionCombat` over `GroundUnit` fields. `CombatKernel` (slices 1–2, ship side wired) is the shared home; slice 3 routes the ground side through it. **Until the merge finishes, every ➕ term above must be built TWICE** (ship `WeaponProfile` + ground `GroundUnit`). Recommendation: land the merge before/as the first slice of the extensions so each term is built once.

---

### Prerequisite fixes & dead stubs (file:line)

- **No `Penetration`/`PerShotEnergy`/`AmmoPool` on the space `WeaponProfile`** — verified absent (grep of `Combat/`+`Weapons/`). The ➕ backlog is genuinely un-built, not hidden.
- **`ExternalBeliefPressure` not yet on `LegitimacyInputs`** (`LegitimacyDB.cs:119`) — the SWAY door's one required engine field; must be added beside `GovernorCompetence` (`:128`) and subtracted in `ComputeLegitimacy` (`:66`).
- **Missile firepower is a stub** — `ShipCombatValueDB.cs:269`/`:38` (`MissileLauncherFirepowerStub 100_000`); Guided reads real geometry from `OrdnanceDesign` but its combat contribution is a flat placeholder until warhead energy is wired.
- **Colony/garrison damage energy scale is placeholder** — `OnColonyDamage` 1e8 J/unit divisor and `ApplyGroundBombardment` `GroundBombardmentDamagePerStrength 0.01` (`DamageProcessor.cs:289,:349`) — the bioweapon and orbital-bombardment magnitudes calibrate against unfinalized warhead-energy data.
- **`SpawnWreck` is a stub that just deletes the ship** (`DamageProcessor.cs:478`) — not a weapons blocker, but the salvage/recovery follow-on (out of the Weapons category) depends on finishing it.
- **Dead-code guard:** the per-pixel damage sim (`DamageComplex`/`DamageVeryComplex`) is **parked** — combat casualties are strength-math (whole-ship removal), NOT `DamageProcessor.OnTakingDamage`. Do not route weapon dials through the pixel sim (Combat gotcha #1). The one exception is colony/station/garrison hits, which *do* route through `OnColonyDamage`/`OnStationDamage` — the seam the bioweapon Projection uses.

---

### Design essence captured inline (so the external docs are NOT needed)

**Bioweapon — the deniable-warfare model (`ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` §"Covert weapons", origin/main).** A bioweapon is a **designed component like any other** — cradle-to-grave in the weapon designer (research → design its lethality/contagiousness/incubation/species-specificity/detectability → build → deliver) — but its "combat" happens in the **population / legitimacy** systems, not the fleet auto-resolve (this is why its pillar tag is Espionage·Projection graded against the population resolver, not Military). Delivery has three routes, each a decision that stacks: **(a)** fly it in on a vessel where *cover* matters (a trade/science ship is plausible, a warship screams intent); **(b)** an agent plants it (the infiltration-bay mechanic); **(c)** the **proxy route** — fund a rival's rebels/coup/terror-cell and let *them* deploy it, keeping your name off it. The balancing force is the **grave/blowback rung**: detection is a WMD-scale provocation (relations crater with *everyone*, casus belli, coalitions form), and a contagious strain can **spread back** to your own or neutral worlds (MAD). Deniability is the whole game — *traced* ("someone spied") vs *caught* ("it's you") drives a per-rival suspicion meter — which is why **detectability** is a first-class design dial, not flavor. So "should I even build this" is the real decision; it's a strategic weapon of last resort, not spam.

**SWAY / belief-pressure — the kill via `LegitimacyInputs` (`INFLUENCE-PILLAR-DESIGN.md`, origin/main).** Influence is a **fourth conquest vector — "war without a weapon"** — and its kill **reuses the built legitimacy/rebellion system** rather than adding a loop (§0i passes perfectly). A world's allegiance is a tug-of-war: the owner's **governance** (governor competence, morale, met demands) props legitimacy **up**; your influence campaign pushes it **down from the outside**. Mechanically this is one new attacker on the same battlefield: `LegitimacyInputs` already carries hook-slots like `GovernorCompetence` (`LegitimacyDB.cs:128`) that a source populates — SWAY adds **exactly one more input, `ExternalBeliefPressure`** (parallel, purely-external), subtracted in `ComputeLegitimacy` (`:66`); past `CollapseThreshold 20` (`:42`) the world secedes into `RebellionDB` (`LegitimacyProcessor.cs:73`) — and if you've been converting it, it secedes *to your faith/culture*. The SWAY emitter itself is the only buildable gear (flavor Religious/Cultural/Ideological × persistent-zone delivery × magnitude×radius; **zero effect vs a hive/machine** — the anti-dominance catch); the missionary/prophet/Influence-Minister who runs it are **Beings** (People system), not components — the gear-vs-being boundary. Its two counters are gear: **Broadcast Jamming** (EW, §3.4) and **Cultural Insulation** (Defense Hardening, §5.3). It **never touches the combat resolver** (§0f) — an Influence·Projection door is graded only against the legitimacy resolver.

---

### §0g stamp — the three acceptance criteria

- **Reachable (cradle→grave).** mineral (mined) → material (refined) → **built at a colony** → **component** in the weapon designer (e.g. `deflector-array`/`disruptor-weapon`/`railgun-weapon` via the **six-point registration**: `*Atb` C# + `weapons.json` template + `componentDesigns.json` + `earth.json` StartingItems + ComponentDesigns + a ship design — gotcha #6/#10) → gated by **research** (tech unlocks the template; bioweapon Selectivity gated by a net-new Biology & Genetics tech) → **installed** on a hull/unit → the **in-play decision** (which weapon, which posture, open fire / hold / standoff-vs-brawl) → **damaged/destroyed** (a shot-off weapon drops that `WeaponProfile`; `ShipCombatValueDB` is v1 computed-once, recalc-on-damage is the parked degraded-condition hook). Every rung is real for the four conventional doors; Exotic effect-dials name their missing rung honestly.
- **Mirrored (NPC runs it back through the identical order path).** A weapon is a **component on a shared door**, so an NPC mounting/firing it is the *same act* as the player's — the auto-trigger (`CombatEngagement.Tick`) reads only `FactionOwnerID`, and hostile fleets fire the exact same `WeaponProfile`/salvo math (`CombatSandbox.SpawnHostileFleet` spawns real rival factions that engage). The bioweapon/SWAY mirror is the same: the NPC populates `ExternalBeliefPressure`/plague-damage against *your* worlds through the same legitimacy/`OnColonyDamage` inputs (the always-on mirror — you must run counter-intel/insulation as a standing decision).
- **Observable (a gauge both sides see).** The **Battle Report / per-salvo play-by-play** (`BattleLog.cs`, `CombatEngagement.NarrateToLog`) names the weapon **classes** in the incoming fire mix, the ship-count-weighted **landed fraction** (hit-vs-dodge), **damage dealt**, and **ships destroyed by name** — e.g. *"took Railgun + Beam fire at 8.5 km — 42% on target (58% dodged), 0.82 GJ dealt; destroyed 'Cargo Courier'; 3 left."* The Fleet Combat tab shows Firepower-at-range + Shields. Bioweapon/SWAY effects surface on the target world's legitimacy/morale/rebellion readouts. Honest limit: v1 is whole-ship (no per-component hull %), the parked degraded-condition tier model is the follow-on.

---

### Cross-category shared state (Prime Directive)

- **Feeds IN → Weapons:** `ComponentInstancesDB.AllComponents` (+ `HealthPercent`) and the weapon `*Atb`s (`GenericBeamWeaponAtb`, `RailgunWeaponAtb`, `FlakWeaponAtb`, `DisruptorWeaponAtb`, `MissileLauncherAtb`, `OrdnancePayloadAtb`) → read by `ShipCombatValueDB.Calculate` (`:161`). **Power** (Generation/Storage) supplies firing draw + capacitor alpha; **Industrial/Logistical** supply ammo/ordnance (`GroundAmmo`, `OrdnanceDesign : IConstructableDesign`); **research/Tech** gates every template; **Chassis** supplies the mass budget that funnels big weapons to big frames (§0b).
- **Weapons feeds OUT →:** the **Combat** salvo resolver (`CombatEngagement`/`CombatKernel`/`AutoResolve`) via `Firepower`/`Weapons[]`; the **Damage** system for colony/station/garrison hits (`OnColonyDamage:289`, `ApplyGroundBombardment:349`, `OnStationDamage`); the **legitimacy/rebellion** resolver (SWAY `ExternalBeliefPressure`, bioweapon casualties+morale); **Diplomacy** (`DiplomacyDB` — WMD/taboo → relation crater, casus belli, and the `AreHostile` suppression that decides who even fights).
- **Shares STATE with:** **Defense** — the ⛓ counter half of every projector: `ShieldAtb`/`ShieldCapacity_J` (shield pool, nature-soaked via `SoakFractionOf`) and armour (`ArmourSoak`, folded into Toughness) are read *by the weapon salvo* — Weapons and Defense are two ends of one matchup and **must be balanced together**. **Sensors/EW** share the detection state that gates firing (fog-of-war → `RequireDetectionToEngage`; EW jams both weapon fire-control and SWAY belief-radius). **Propulsion** shares Evasion + closing state (who gets into weapon range).
- **Weapons TRIGGERS:** ship destruction (the one combat piece with live side effects), the **combat-interrupt** clock halt at first contact, `FleetRetreatDB`/engagement-end, colony population casualties + atmospheric contamination + installation/garrison damage, and (SWAY/bio) legitimacy collapse → `RebellionDB` → secession.

---

### Holes this category owns / resolves

| Hole | What it is | Status → home |
|------|-----------|----------------|
| **H7** | Hybrid / cross-category weapon — one item, two roles (staff weapon = Energy+Melee; lightsaber = weapon+defense) | **Owned by Weapons.** Fix = allow a component to carry **two doors** (a weapon spanning families) + weapon×defense cross-links (a blade that parries). Priority MEDIUM. The Melee **Parry** dial is the weapon-as-defense probe; ⏳ deferred to the effect-bus + a People/pilot trait (the saber is gear, the ranged-deflect is the *being*, H4). |
| **H9** | Conversion / assimilation weapon — turn an enemy into you; mind-control | **Resolved via the Exotic Selectivity/effect-bus.** A `Selectivity = convert` projector (reuse the **capture** owner-flip primitive) graded against the **legitimacy resolver**, tagged Espionage/Influence·Projection — so H9 finally gets a home through the projector tag (§0h/E0a). ⚙ needs the effect bus + capture primitive. |
| **H2b** | Adaptive shields (weapon-side dependency) | Weapons' **Frequency Modulation** dial ⏳ **defers on H2b** — with no adaptive defense there is nothing to modulate against; build adaptive shields (Defense) first. |
| **H1 / H8** (adjacent) | Teleport-delivery / gate-network (Guided/Exotic delivery can ride these) | NOT owned by Weapons — H1 → Logistical ▸ Transfer teleport mode; H8 → the C3 Relay / Route Works doors. Weapons only *rides* them as delivery once built. |

---
*Verified against the C# source 2026-07-09; every file:line checked by grep/read, not trusted from prose.*

---

## §2 — Propulsion

Propulsion is *how the thing moves* — and movement is not a cosmetic stat in this game, it is a **combat input**. A ship's acceleration sets how hard it is to hit (Evasion); its Δv (fuel reach) sets whether it can force or refuse an engagement (the closing/kiting fight); a ground unit's speed sets whether it reaches the enemy at all. So a propulsion dial has the same standing as a weapon dial: it must bottom out in a number the resolver already reads.

**The yardstick for propulsion dials (the §0d gate's category anchor):** the **Newtonian move + closing model**, not the joule damage scale. A propulsion dial is ✅ if it maps onto one of these live stats:
- **Acceleration** → `ShipCombatValueDB.CalculateEvasion` (`accel = ThrustInNewtons ÷ MassDry`; `agilityFactor = accel/(AgilityReference 5.0 + accel)`; `Evasion = EvasionCap 0.95 × sizeFactor × agilityFactor`) — the harder you can change your vector, the harder you are to hit.
- **Δv (delta-v)** → the Tsiolkovsky reach (`Ve × ln(wet/dry)`) → the closing model's `DeltaVFloor` / `ManeuverBudget` (a fleet with more Δv can dictate range; a burned-out kiter loses control — `docs/FLEET-COMBAT-CLOSING-DESIGN.md` P2).
- **Fuel economy** → `ShipFactory.FillFuelTanks` / the cargo fuel model — how far it can go on a tank before resupply.
- **Mass** → `MassVolumeDB` → the chassis budget (§0b) *and* back into acceleration (a heavier drive drags its own evasion down — the feedback that keeps the numbers honest).
- **(ground twin)** → `GroundLocomotionAtb.SpeedFactor` → `GroundMobility.SpeedMultForUnit` → the closing hex-march (`OrderMove` speed) — the same "can it reach the fight" decision on a surface.

The five propulsion doors (locked one at a time): **Reaction** (Newtonian rocket — this door), then Traction (surface/ground locomotion), Fluid (atmospheric/naval — lift/buoyancy), Warp (FTL / jump), Exotic (reactionless / gravitic — the open slot).

### 2.0 Shared propulsion dials (common to all propulsion doors)
On top of the universal seven (§0a), every drive has:
| Dial | Drives (real stat) |
|------|--------------------|
| **Thrust / motive force** | acceleration = force ÷ mass → **Evasion** + closing speed |
| **Efficiency (force per unit fuel/power)** | how much reach/endurance you buy per unit consumed → Δv / range |
| **Fuel / power draw** | what it burns and how fast → the resupply + supply-gate load |
| **Drive mass** (emergent) | the drive's own tonnage → chassis budget **and** drags its own acceleration |
| **Operating medium** | vacuum / surface / fluid / subspace — which door even applies, and where the drive works at all |

The doors differ in **what supplies the motive force and what medium it pushes against.** Reaction throws mass out the back (works best in vacuum); Traction pushes on the ground; Fluid pushes on air/water; Warp folds space. Door-specific dials below.

### 2.1 Propulsion ▸ REACTION  🔒 *locked*
*The Newtonian main-drive: throw reaction mass out the back, get pushed forward — everything from a chemical booster to a fusion torch to an ion cruiser falls out of these dials. This is the drive the game's physics already runs on (`NewtonionThrustAtb`: `Thrust = ExhaustVelocity × FuelBurnRate`; reach = Tsiolkovsky `Ve × ln(wet/dry)`), so every dial here is **Modelled today** — no wiring, no deferral.*

**The core decision — SPRINT vs ENDURANCE (the whole door):** a rocket cannot be great at both acceleration and reach at once, and the physics is what enforces it. High thrust (great evasion, forces the merge) is bought by throwing mass out fast — which **empties the tank fast** (low Δv, short legs). High exhaust velocity (huge Δv, kites forever, deep-strike range) is bought at **low thrust** (sluggish, can't dodge, can't force a fight). You pick where on that line you sit; you can't have both ends. That's the anti-dominance rule made physical — no free win, the trade is Tsiolkovsky itself.

**A. Thrust class — how hard it accelerates (the evasion + closing dial)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Low thrust (cruiser)** | sips propellant → **huge Δv/reach**; light, efficient | **sluggish** — low evasion (eats fire), can't force or refuse a fight; slow to build up speed |
| **Balanced** | usable evasion + usable reach | master of neither |
| **High thrust (sprinter)** | **high acceleration → high evasion** (dodges), forces the merge or runs down a kiter | **drinks propellant** → short legs (low Δv); the drive is heavy (drags its own accel back) |

**B. Exhaust velocity / specific impulse — reach per unit fuel (the Δv + kiting dial)**
| Option | Why | Catch |
|--------|-----|-------|
| **Low Ve (chemical, ~4500 m/s)** | cheap fuel, **high thrust per kg burned** → punchy acceleration | terrible Δv → **short range**, refuels constantly |
| **Mid Ve (nuclear-thermal / fusion, ~10⁴–10⁵ m/s)** | strong all-rounder — real thrust *and* real reach | needs advanced fuel + tech; hot/heavy drive |
| **High Ve (ion / plasma, ~5×10⁴–10⁵ m/s)** | **enormous Δv** → deep-strike range, can kite indefinitely (never runs out of maneuver budget first) | **tiny thrust** → near-zero evasion, cannot dodge or force an engagement |

**C. Fuel type — what it throws (the cradle-to-grave rung)**
| Option | Why | Catch |
|--------|-----|-------|
| **Common propellant (chemical/water)** | cheap, everywhere → easy resupply, simple logistics | low Ve → short legs |
| **Refined/nuclear (`ntp`, deuterium…)** | high Ve → the reach/thrust the exotic drives need | must **mine → refine → stock** it; a drive can burn a fuel the faction hasn't *unlocked* (the `ntp` gotcha) → no fuel, no move |
| **Exotic (antimatter/He-3)** | extreme Ve + thrust together (breaks the sprint/endurance wall — the payoff of deep tech) | rare, costly, high tech; storage is its own problem |

*Fuel is the door's cradle-to-grave spine: the propellant is mined, refined, stocked at a colony, burned by the drive, and **runs out** (empty tank = dead in space → resupply, `FillFuelTanks`). A drive is only as real as the fuel behind it.*

**D. Drive mass (emergent — not a free dial, a consequence)**
The drive's own tonnage scales with thrust × Ve × tech. It is **not** a knob the player sets to taste — it *falls out* of A/B/C — but it's the load-bearing feedback: a bigger drive (a) consumes chassis budget (§0b — a torch-drive's mass funnels you to a bigger hull) and (b) **drags its own acceleration down** (`accel = Thrust ÷ MassDry`, and the drive is part of MassDry), so doubling thrust never quite doubles evasion. That self-limiting loop is what stops "just dial thrust to max" from being a free win — the numbers push back.

**Derived stats (computed, shown to the player):**
`acceleration = Thrust ÷ MassDry` (m/s²) · `Δv = Ve × ln(wetMass/dryMass)` (m/s) · `Evasion` (from accel + size) · burn endurance (tank ÷ FuelBurnRate) · drive mass · fuel cost per full tank.

**Modellability audit (§0d — all four dials ✅ Modelled; the physics is already in the engine):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Thrust class** | ✅ | `NewtonThrustAbilityDB.ThrustInNewtons` → `CalculateEvasion` (`accel = Thrust ÷ MassDry`, `AgilityReference 5.0`) → **Evasion**; and the closing model's `FleetManeuver` / `AdvanceClosing` (accel decides who dictates range) |
| **Exhaust velocity / Isp** | ✅ | `NewtonionThrustAtb.ExhaustVelocity` → Δv (Tsiolkovsky `Ve × ln(wet/dry)`) → the closing model's `DeltaVFloor` → `ManeuverBudget` (P2 kiting clock — more Δv = kite longer / dictate range) |
| **Fuel type** | ✅ | `NewtonionThrustAtb.FuelType` + `FuelBurnRate` → the cargo fuel economy / `ShipFactory.FillFuelTanks` (burns a real stocked material; empty tank = no move) |
| **Drive mass** (emergent) | ✅ | `MassVolumeDB` → the chassis budget (§0b) **and** back into `accel = Thrust ÷ MassDry` (heavier drive → lower evasion — the honest feedback) |
| **(ground twin — Traction preview)** | ✅ | `GroundLocomotionAtb.SpeedFactor` → `GroundMobility.SpeedMultForUnit` → the closing hex-march speed (`OrderMove`) — the same accel/reach decision on a surface (its own door, §2.2) |

**Reading of the audit:** Reaction is the **most-ready door in the game** — unlike a weapon, nothing needs wiring or deferring, because the engine already computes Newtonian thrust, Tsiolkovsky Δv, and the evasion feedback from mass. The door is almost entirely a matter of **exposing** dials onto stats the resolver reads today. The Δv→`ManeuverBudget` seam is **confirmed wired in code** (`FleetCombat.DeltaVFloor` reads each ship's `NewtonThrustAbilityDB.DeltaV`, min-over-fleet, seeded into `FleetCombatStateDB.ManeuverBudget` at `CombatEngagement.cs:449,524`) and gauged (`ClosingTests` P2 kiting clock) — see the full propulsion insertion map in `docs/AUTO-RESOLVER-ANATOMY.md` §7.

**Numbers (calibrated to §0e's number model + the evasion/closing anchors):**
| Dial | Unit | Range (tech-scaled max) | Pins to |
|------|------|--------------------------|---------|
| **Thrust** | N | booster 10⁶ → torch 10⁷⁺; **accel that matters ≈ `AgilityReference` 5 m/s²** (half-evasion point) | `accel = Thrust ÷ MassDry` → `CalculateEvasion` |
| **Exhaust velocity (Ve)** | m/s | chemical ~4500 · NTR/fusion ~10⁴–10⁵ · ion ~5×10⁴–10⁵ · antimatter 10⁶⁺ | Δv = Ve × ln(wet/dry); Isp = Ve ÷ 9.81 |
| **Fuel burn rate** | kg/s | high on a sprinter, low on a cruiser | `Thrust = Ve × FuelBurnRate`; endurance = tank ÷ rate |
| **Δv (derived)** | m/s | short-legged sprinter ~few km/s → ion cruiser 50+ km/s | `DeltaVFloor` → `ManeuverBudget` (kiting) |
| **Evasion (derived)** | 0–0.95 | brick 0 → nimble fighter `EvasionCap` 0.95 | `EvasionCap × sizeFactor × agilityFactor` |
| **Drive mass (emergent)** | kg | scales thrust × Ve × tech | chassis budget + feedback into accel |

**Sanity-check one build through the resolver:** a light fighter with a **high-thrust chemical drive** — accel ≈ 5 m/s² (agilityFactor = 5/(5+5) = 0.5) on a small hull (sizeFactor near 1) → Evasion ≈ 0.95 × 1 × 0.5 ≈ **0.48**, a real dodge — but a **low Δv**, so it can't chase a kiter and refuels often. The same hull with a **high-Ve ion drive** — accel ≈ 0.1 m/s² (agilityFactor = 0.1/5.1 ≈ 0.02) → Evasion ≈ **0.02**, a sitting duck in a knife-fight — but **Δv in the tens of km/s**, so it dictates range and never runs out of maneuver first. The sprint/endurance trade shows up directly in the two live stats the fight reads (Evasion vs `ManeuverBudget`); neither build dominates, exactly as intended.

**Physical demands (what the dials cost — throttles and mass, never a rulebook, per §0b):**
- **Fuel** — a drive with an empty tank produces **0 thrust** (dead in space); the player refuels because the ship stops, not because a rule says so. Reach is `Ve × ln(wet/dry)` — bolt on more tankage (mass) to extend it, and the extra mass drags accel.
- **Drive mass** — the real forcer: thrust and Ve both add drive mass; that mass + tankage is the tonnage the chassis budget must physically hold, and it feeds back into `accel = Thrust ÷ MassDry` so an over-dialed torch on a small hull just accelerates like a barge.
- **Power (some drives)** — an ion/plasma drive needs electricity to accelerate the propellant (the supply gate, §0c), the same cascade a railgun triggers — appears on its own from the dial, no special rule.
- **Heat / signature** — a hot high-thrust burn spikes the thermal signature (Detection/EW), the propulsion twin of a weapon's firing-heat — a ◐ wire onto the same EMCON activity term the weapons door uses (noted for when the EMCON pass lands; not a Reaction-specific build).

**Preset coordinates — proof the dials span the archetypes (each a distinct point in the sprint↔endurance space):**
| Drive | Thrust | Ve (Isp) | Fuel | The trade it chose |
|-------|--------|----------|------|--------------------|
| **Chemical booster** | very high | low (~4500 m/s) | common | pure sprint — great accel/evasion, tiny Δv, refuels constantly |
| **Nuclear-thermal (NTR)** | high | mid (`ntp`) | refined | the workhorse — real thrust *and* real reach; needs refined fuel + tech |
| **Fusion torch** | high | high | refined/exotic | breaks the wall a little (thrust *and* Δv) — pays in tech, drive mass, heat |
| **Ion / plasma cruiser** | very low | very high (~10⁵) | common gas + **power** | pure endurance — enormous Δv, kites/deep-strikes; near-zero evasion, needs a reactor |
| **Antimatter torch** | very high | very high (10⁶⁺) | exotic | the deep-tech payoff — both ends at once; its rare fuel + drive mass funnel it to a big chassis |

The lesson mirrors the weapons doors: you never *choose* "sprinter" or "cruiser" from a menu — you dial thrust and exhaust velocity, and the archetype **falls out of where Tsiolkovsky lets you sit.** The chemical booster is a sprinter because low Ve *makes* it one; the ion cruiser kites because high Ve *makes* it one. The numbers force the identity, never a label.

---

### 2.2 Propulsion ▸ TRACTION  🔒 *locked*
*Surface locomotion: wheels, tracks, legs, hover/grav-plates — push against the **ground**. This is Reaction's ground twin (the same sprint-vs-reach decision on a surface), and it's wired to the ground closing model the resolver merge just built. Everything from an infantry squad's boots to a wheeled APC to an AT-AT walker to a hovertank falls out of these dials.*

**The core decision — CAN IT REACH THE FIGHT, and on what ground?** Ground combat is a closing fight over hexes (`GroundMobility` speed → the hex-march; H3 range-based directed combat). A unit that can't close is dead weight — a short-range or melee unit that's too slow never lands a blow; a fast unit dictates the engagement. But speed on open ground is bought against **terrain**: the fast drive bogs in the mud the slow one crawls through. So the trade is **speed ↔ all-terrain ↔ cost**, and the numbers (SpeedFactor × the terrain's roughness) decide who arrives.

**A. Drive type — what it rolls/walks/floats on (the core traction axis)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Wheeled** | **fast + cheap + light** on good ground (road/plain) → reaches the fight first | **bogs in rough/soft terrain** (low rough-handling) — the closing speed collapses off-road |
| **Tracked** | **all-terrain** — crosses mud/rubble/slope at a steady pace | slower top speed, heavier, costlier than wheels |
| **Legged (walker/mech)** | climbs **anything** — steep, broken, vertical; ignores most terrain penalties | **slow + expensive + heavy**; the AT-AT trade (goes anywhere, crawls doing it) |
| **Hover / grav** | **skims over terrain AND water** at high speed — terrain-blind | **power-hungry** (needs a reactor) + fragile; loses its skim if drained |

**B. Terrain handling** *(how little rough ground slows you — the `RoughHandling` dial)*
| Option | Why | Catch |
|--------|-----|-------|
| **Road-bound (low)** | max speed on good ground, lightest | crawls in rough — a mobility trap off the beaten path |
| **All-terrain (high)** | holds its speed across mud/slope/rubble | costs mass + money for the suspension/tracks |

**C. Amphibious / water-crossing** *(the medium-crossing dial)*
| Option | Why | Catch |
|--------|-----|-------|
| **Land-only** | lighter, cheaper | **ocean is impassable** (locked H2b) — the hex-pathfinder routes around water, so a lake or strait is a wall |
| **Amphibious** | fords rivers, crosses coast/ocean hexes — opens flanking routes others can't take | slower on land, adds mass/cost (sealing + flotation) |

**D. Motive power** *(muscle ↔ engine ↔ reactor — the supply rung)*
| Option | Why | Catch |
|--------|-----|-------|
| **Unpowered (muscle/legs)** | zero fuel, zero supply — infantry that never runs dry | capped, slow (leans on the chassis + Enhancers for speed) |
| **Engine-driven** | real speed on burned fuel | a fuel/supply load (Logistical) |
| **Reactor-driven (hover/heavy)** | powers a hover/grav skim or a heavy walker | ⛓ needs power on the platform (the supply gate) — a drained unit stops |

**E. Drive mass (emergent — the same feedback as Reaction)**
The locomotion's tonnage scales with drive type × speed × terrain-handling. It consumes the chassis budget (§0b) **and** feeds back into speed (a heavier drive on the same chassis moves slower) — so "just dial speed to max" pays for itself in mass, exactly as thrust does on a rocket. A walker that climbs anything is heavy *because* legs are heavy; the number forces it.

**Modellability audit (§0d — the ground yardstick; nearly all ✅ Modelled today):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Drive type / speed** | ✅ | `GroundLocomotionAtb.SpeedFactor` → `GroundMobility.SpeedMultForUnit` → the hex-march speed (`OrderMove`) → the closing fight (H3) |
| **Terrain handling** | ✅ | `GroundLocomotionAtb.RoughHandling` + `HexPathfinder` (terrain-weighted A\* — rough hexes cost more) |
| **Amphibious / water** | ✅ | `GroundLocomotionAtb.Amphibious` + `HexPathfinder` passability (ocean impassable → passable for amphibious; ice already passable — locked H2b) |
| **Motive power** | ✅ | the ground supply gate (`GroundUnitAssembly` power/ammo gates — a reactor-driven drive draws watts) |
| **Drive mass** (emergent) | ✅ | ground-unit mass → speed feedback + the chassis budget |
| **Terrain COMBAT bonus** (traction's edge on its own ground) | ◐ **wire** | movement-through-terrain is Modelled; a *combat* bonus for being on your preferred terrain rides **H3 hex-terrain-in-combat** (a flagged follow-on) — the hook exists, the term is the wire |

**Reading:** Traction is as ready as Reaction — the whole ground-locomotion stack (`GroundLocomotionAtb` + `GroundMobility` + terrain-weighted `HexPathfinder`) already exists and already feeds the closing fight. The absorbed `GroundLocomotionAtb` becomes the concrete dials of this universal door (per the categories doc: the parallel ground systems die into the universal ones). One ◐ wire (terrain giving a *combat* edge, not just a movement one).

**Numbers (calibrated to the ground yardstick):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **SpeedFactor** | ×mult | 0.1 (floor) · 1.0 baseline · ~2–3 hover | `GroundMobility.SpeedMultForUnit` → hex-march |
| **RoughHandling** | 0–1 | 0 (road-bound) → 1 (all-terrain) | `HexPathfinder` rough-hex cost |
| **Amphibious** | bool | land-only / amphibious | `HexPathfinder` water passability |
| **Drive mass** (emergent) | kg | scales type × speed | speed feedback + chassis budget |

**Preset coordinates — the span:**
| Unit | Drive | Speed | Terrain | The trade it chose |
|------|-------|-------|---------|--------------------|
| **Infantry** | unpowered legs | low | good | zero supply, goes most places slowly; leans on numbers |
| **Wheeled APC** | wheels | high | poor | races down roads; bogs off-road |
| **Main battle tank** | tracks | mid | high | all-terrain workhorse; heavier/costlier |
| **AT-AT walker** | legs | low | max | climbs anything; slow + expensive + a big target |
| **Hovertank** | grav | high | terrain-blind + water | skims over everything fast; power-hungry + fragile |
| **Amphibious assault** | tracks + amphib | mid | high + water | takes the coast route others can't; slower, heavier |

---

### 2.3 Propulsion ▸ FLUID  🔒 *locked*
*Lift and buoyancy: aircraft wings, jets, VTOL rotors, airships, ships, submarines — push against a **fluid medium** (air or water) instead of thrown mass or solid ground. Everything from a fighter jet to a submarine to a gas-giant cloud-skimmer falls out of these dials. This is the FIRST propulsion door with real deferrals — because the sim doesn't yet model an **air/altitude/depth layer**, so the door ships its shallow half now and names the prerequisite for the deep half.*

**The honest framing (why this door is partly deferred):** Fluid's payoff is a **plane of movement the enemy may not reach** — air superiority (only anti-air touches a flyer), a submarine's underwater ambush, a skimmer working a gas giant's cloud deck. That payoff needs a **combat medium/altitude layer** the engine doesn't have (space combat tags no medium; ground has surface hexes but no air or sea band) — the exact prerequisite the Weapons ▸ Energy "Medium performance" dial was deferred on. So we do the disciplined thing (§0d): **ship the dials that ride mobility today, defer the altitude/depth dials until that layer is built, and never ship a dead knob.**

**The structural insight — Fluid is usually a MODIFIER on another drive.** A jet is a **Reaction** drive (thrust) + wings (lift); a hovercraft is a **Traction** hover + a fluid seal. Fluid rarely stands alone — it's the *lift/buoyancy layer* that lets another propulsion door work in air or water. That's why most of its combat depth lives in the (future) medium layer, not in a new thrust model.

**A. Medium — where it operates (gates where it works AT ALL)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Air (winged/rotor)** | reach + speed over any surface terrain; the air-superiority plane | ⏳ full payoff needs the **altitude layer**; **useless in vacuum** (a medium constraint — Modelled as "can't operate here") |
| **Water surface (ship/hydrofoil)** | carries huge mass/cargo on water; naval reach | slow; surface-bound — exposed |
| **Submerged (submarine)** | **hides underwater** — ambush, stealth | ⏳ the hide needs the **depth layer**; slow, crush-depth limits |
| **Dense-atmosphere skim (gas-giant)** | works a cloud deck no lander can | niche; needs the atmosphere to be there |

**B. Lift type — how it stays up (speed ↔ hover ↔ endurance)**
| Option | Why | Catch |
|--------|-----|-------|
| **Fixed-wing** | **fast + long-range** | **can't hover / hold station**; needs room (runway/space) to get airborne |
| **Rotor / VTOL** | **hover + vertical** takeoff, holds station | slower, fuel-hungry, mechanically fragile |
| **Lighter-than-air (airship)** | enormous **lift + endurance** for its cost | very slow, big + fragile target |
| **Buoyancy (ship/sub hull)** | carries the **most mass** (heavy cargo/guns on water) | slow, medium-locked |

**C. Altitude / depth band** *(the exposure dial — mostly deferred)*
| Option | Why | Catch |
|--------|-----|-------|
| **High / deep** | out of short-range fire — safe from ground guns | ⏳ needs the altitude/depth layer; less accurate on targets below/above |
| **Nap-of-earth / periscope** | hugs terrain for cover, ambush | in range of everything |

**D. Medium transition** *(air↔ground, surface↔submerged)*
| Option | Why | Catch |
|--------|-----|-------|
| **Single-medium** | optimized, lighter | stuck in one plane |
| **Transitioning (VTOL land, sub surface/dive)** | flexes between planes — a flyer that lands, a sub that surfaces | ◐ wire; costs mass; slow in the off-medium |

**Modellability audit (§0d — the honest mixed door):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Medium access / can-cross-water** | ◐ **wire** | rides `GroundLocomotionAtb.Amphibious` + `HexPathfinder` passability (a flyer/ship "reaches a hex others can't") — the *access* is wireable now |
| **Vacuum constraint** (air drive dead in space) | ✅ | a medium tag "this drive needs atmosphere" — the same medium-constraint the categories doc calls a Propulsion medium dial (AT-AT "atmosphere-only") |
| **Lift type / cruise speed / lift capacity** | ◐ **wire** | speed + carry-mass ride the mobility + cargo stats; the read exists, the fluid framing is the wire |
| **Altitude / depth band (the combat payoff)** | ⏳ **defer** | needs the **air/altitude + depth combat layer** (HEX-GROUND future) — air-superiority, submarine stealth, over/under fire all wait on it |
| **Air-vs-ground targeting** (only AA hits a flyer) | ⏳ **defer** | same prerequisite — a target-medium gate in the resolver (who can shoot what plane) |
| **Medium transition** (VTOL land / sub dive) | ◐ **wire** | a state toggle on the mobility model (like Traction's amphibious), once the medium tag exists |

**Reading:** Fluid is **half-ready, honestly split.** Its *access* dials (cross water, reach an isolated hex, dead-in-vacuum constraint) wire onto the ground mobility + medium-tag today; its *payoff* dials (altitude bands, submarine stealth, air-only-hit-by-AA) **defer** on one named prerequisite — the **air/altitude/depth combat layer** (a HEX-GROUND future item). That prerequisite becomes an explicit build item *before* the deferred dials ship. No dead knobs: we don't offer an "altitude" slider the sim ignores.

**Numbers:** lift capacity (kg), cruise speed (m/s), ceiling/depth (m — the future altitude layer's axis); the shallow half pins to mobility + cargo, the deep half to the (future) medium layer.

**Preset coordinates — the span (✅ = shallow-shippable, ⏳ = needs the medium layer):**
| Unit | Medium | Lift | The trade it chose | State |
|------|--------|------|--------------------|-------|
| **Fighter jet** | air | fixed-wing | fast air reach, dead in vacuum | ⏳ air layer for superiority |
| **VTOL gunship** | air | rotor | hover + vertical, fuel-hungry | ⏳ |
| **Naval destroyer** | water surface | buoyancy | big guns on water, slow | ◐ water access ✅ |
| **Submarine** | submerged | buoyancy | underwater ambush | ⏳ depth layer for the hide |
| **Airship carrier** | air | lighter-than-air | huge lift/endurance, slow | ⏳ |
| **Gas-giant skimmer** | dense-atmo | fixed-wing | works a cloud deck | ⏳ |

---

### 2.4 Propulsion ▸ WARP  🔒 *locked*
*Faster-than-light: the Alcubierre warp bubble and jump-point traversal — how a ship crosses **between stars**. This is the strategic-map drive, distinct from the tactical Reaction sublight drive (a ship carries both, like Trek's warp + impulse). Everything from a plodding freighter warp to a Star Destroyer hyperdrive to a Stargate falls out of these dials — and the whole system already exists in the engine, so Warp is nearly as ready as Reaction.*

**The core decision — GO-ANYWHERE WARP vs ON-RAILS JUMP.** A self-powered **warp drive** takes you anywhere in a straight line, but it's slow and needs a big battery to open and hold the bubble. A **jump** is instant and cheap but only between fixed, surveyed **jump points** — you travel the network, not the void. It's the strategic mirror of the standoff-vs-brawl trade: **freedom vs speed**. (And the deep-tech answer is "both" — a self-opening jump drive — at a steep tech/cost.)

**A. FTL method — the core split**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Continuous warp (Alcubierre)** | go **anywhere** — no infrastructure, no lanes; arrive by a straight line | **slow** (sublight-of-FTL) + **big power draw** to create/sustain the bubble → a heavy battery; a drained ship can't warp |
| **Jump-drive** | **instant** traversal + cheap per trip | **only at jump points** (surveyed nodes) — you're on the network's rails; a self-opened jump is high-tech |
| **Gate-user (no drive)** | rides fixed infrastructure (a lane/Stargate) — **cheapest**, needs nothing aboard | ⏳ needs the **network/addressing layer** (H8 — which node reaches which); fully dependent on someone building the gate |

**B. Warp speed** *(how fast the bubble travels — the `MaxSpeed` dial)*
| Option | Why | Catch |
|--------|-----|-------|
| **Slow (freighter)** | cheap, low power, light drive | long transit times — vulnerable in the open |
| **Fast (warship/courier)** | crosses systems quickly, outruns pursuit | more engine power → bigger drive/battery → more mass |

**C. Bubble power** *(the energy to open, hold, and drop the bubble — paid in stored electricity)*
| Option | Why | Catch |
|--------|-----|-------|
| **Low-draw** | a small battery serves; light | caps speed/range; a small buffer = short hops |
| **High-draw (fast/heavy)** | powers a big fast bubble | ⛓ needs a **big charged battery** — an uncharged ship sits still (the "spawned ship won't move" gotcha: warp is paid from stored energy, not fuel) |

**D. Drive mass (emergent) + E. Fleet coupling (emergent)**
Drive mass drags `MaxSpeed` (WarpMath reads mass) and eats the chassis budget — the same feedback as every drive. And a **fleet warps as one at its slowest member** (`WarpSpeedFloor` / `DeltaVFloor`, min over ships) — mix a fast courier into a slow convoy and the whole fleet crawls. Both fall out of the existing aggregation, no new rule.

**Modellability audit (§0d — the whole warp+jump system is already live):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Continuous warp** | ✅ | `WarpAbilityDB` (`MaxSpeed`, `TotalWarpPower`, bubble costs) + `WarpMoveCommand` — the live drive |
| **Warp speed** | ✅ | `WarpMath.MaxSpeedCalc(power, mass)` — more engine power + less mass = faster |
| **Bubble power (create/sustain/collapse)** | ✅ | `WarpAbilityDB.BubbleCreationCost`/`SustainCost`/`CollapseCost`, paid from stored electricity (`ChargeReactors`) |
| **Jump-drive** | ✅ | the jump-point system (`JumpPointDB`, `JumpOrder`, `InterSystemJumpProcessor`; survey-gated via `JPSurvey`) |
| **Drive mass** (emergent) | ✅ | `MassVolumeDB` → WarpMath speed + chassis budget |
| **Fleet coupling** (emergent) | ✅ | `FleetCombat.WarpSpeedFloor` / `DeltaVFloor` (min over ships — the fleet moves as one) |
| **Gate-user / network node** (Stargate) | ⏳ **defer** | needs the **H8 network/addressing layer** (which gate connects to which) — a node is more than a drive; named prerequisite in the categories doc |

**Reading:** Warp is **nearly as ready as Reaction** — the entire warp-bubble drive AND the jump-point network already exist and run in the live game (a spawned ship warps once charged; fleets warp at their slowest member). The only ⏳ is the **gate-as-addressable-network** (Stargate/hyperlane) — the H8 hole, which needs an addressing layer built first. Everything else maps to a stat the movement system reads today.

**Numbers (from the live warp system):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **MaxSpeed** | m/s | freighter (low) → hyperdrive (high) | `WarpMath.MaxSpeedCalc(power, mass)` |
| **BubbleCreationCost** | KJ (stored energy) | e.g. alcubierre-2k = 1,000,000 KJ/bubble (a charged battery holds 2–4×) | `WarpAbilityDB` + the battery (`EnergyStoreMax`) |
| **Bubble sustain/collapse** | KJ | scales with speed/mass | the stored-energy budget |
| **Drive mass** (emergent) | kg | scales power × speed | WarpMath speed feedback + chassis |

**Preset coordinates — the span:**
| Drive | Method | Speed | Power | The trade it chose |
|-------|--------|-------|-------|--------------------|
| **Freighter warp** | continuous | slow | low | cheap, light — long, exposed transits |
| **Warship warp** | continuous | fast | high | outruns pursuit; big charged battery |
| **Hyperdrive** | continuous | very fast | very high | strategic speed; heavy drive + huge buffer |
| **Jump-drive raider** | jump | instant | per-jump | strikes across the network; on-rails to the nodes |
| **Stargate** | gate-user | instant | none aboard | rides fixed infrastructure; ⏳ needs the network layer |

---

### 2.5 Propulsion ▸ EXOTIC  🔒 *locked*
*The extensibility slot — reactionless drives, inertialess drives, gravity manipulation, the physics-breakers. Like Weapons ▸ Exotic, this door **grows as new mechanics land**; today it's the home for "a drive that violates a rule the other doors pay," and it's mostly a set of **named deferrals** — each impossible drive escapes one constraint, and each names the prerequisite mechanic it waits on, so the physics-breakers are designed-in, not bolted-on.*

**The identity — each exotic drive BREAKS a constraint the honest doors pay, and must pay for it in tech + cost + a real drawback** (or it's the free win the anti-dominance rule forbids). Reaction pays the fuel/Δv trade; Traction pays the terrain trade; an exotic drive buys its way *out* of one of those — at a price steep enough that it doesn't dominate.

**A. Reactionless thrust** *(breaks Tsiolkovsky — thrust with no propellant)*
| Why pick it | The catch |
|-------------|-----------|
| **Infinite Δv** — accelerate forever, never refuel, never run the tank dry (escapes the sprint/endurance wall on the *reach* axis) | enormous **power + tech**; the drawback is the cost, not a fuel limit |

**B. Inertialess / mass-decoupled maneuver** *(breaks `accel = Thrust ÷ MassDry`)*
| Why | Catch |
|-----|-------|
| Evasion **decoupled from mass** — a capital ship dodges like a fighter (turn on a dime regardless of tonnage) | ⏳ needs an **evasion-override term** that bypasses the mass feedback in `CalculateEvasion` — a named new field; hugely powerful, so gated behind extreme tech |

**C. Gravitic / medium-independent** *(breaks the medium requirement)*
| Why | Catch |
|-----|-------|
| Works in **any medium** — vacuum, air, water, underground — with no fuel and no fluid to push | ⏳ overlaps Fluid's medium layer; defer to that same air/depth-medium prerequisite |

**D. Teleport / instantaneous transport** *(breaks distance — the transporter/ring, H1)*
| Why | Catch |
|-----|-------|
| Instant point-to-point matter/crew move — the transporter, the Goa'uld rings, the Stargate's "through itself" | ⏳ the **H1 hole** — needs a **Transfer ▸ teleport mode** (flagged in the categories doc); not a drive per se, it's cousin to Warp ▸ gate (H8) |

**Modellability audit (§0d — the honest backlog door):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Reactionless thrust** | ◐ **wire** | it's a **Reaction drive with the fuel dial at zero** — the engine reads `ThrustInNewtons` directly and Δv is only bounded by fuel, so "no fuel + direct thrust" is nearly Modelled today; the wire is letting a drive declare no propellant + a big power/tech cost. The cheapest exotic to ship |
| **Inertialess maneuver** | ⏳ **defer** | needs an **evasion-override term** bypassing `accel = Thrust ÷ MassDry` (a named new field on `CalculateEvasion`) |
| **Gravitic / medium-independent** | ⏳ **defer** | needs the **air/depth medium layer** (shared with Fluid) |
| **Teleport (H1)** | ⏳ **defer** | needs the **Transfer ▸ teleport mechanic** (the H1 hole; categories/stress-test docs) |

**Reading:** Exotic propulsion is deliberately a **backlog holder**, exactly like Weapons ▸ Exotic — it names the franchise "impossible drives" and the one prerequisite mechanic each waits on, so they arrive designed-in. Only **reactionless thrust** is nearly free today (Reaction with no fuel + a steep power/tech cost); the other three each wait on a single named mechanic (evasion-override, the medium layer, the teleport mode). No dead knobs: we don't ship an "inertialess" slider the resolver ignores — we ship it *with* the override term, or not yet.

**Preset coordinates — the span (each breaks one rule):**
| Drive | Breaks | The payoff | State |
|-------|--------|-----------|-------|
| **Reactionless drive** | Tsiolkovsky (fuel/Δv) | infinite reach, never refuels | ◐ wire (Reaction, no fuel) |
| **Inertialess drive** | mass→evasion | a battleship dodges like a fighter | ⏳ evasion-override term |
| **Gravitic drive** | the medium requirement | works anywhere, no fuel | ⏳ medium layer |
| **Transporter / rings** | distance | instant matter/crew teleport | ⏳ Transfer ▸ teleport (H1) |

---

## ✅ §2 Propulsion — COMPLETE (5/5 doors locked)
Reaction 🔒 · Traction 🔒 · Fluid 🔒 · Warp 🔒 · Exotic 🔒. **The category's yardstick is the Newtonian move + closing model** (accel→Evasion, Δv→`ManeuverBudget`, speed→the hex-march), not the joule damage scale. Headline reading: propulsion is the **most Modelled-today category so far** — Reaction (Newtonian physics live), Traction (the ground-locomotion stack live), and Warp (the whole warp-bubble + jump-point system live) are almost entirely ✅ with nothing to defer; **Fluid** is the honest split (access dials wire now, the altitude/depth *combat* payoff defers on one named prerequisite — the air/medium layer); **Exotic** is the backlog slot (reactionless is a near-free Reaction variant, the other three each name their one prerequisite). Two prerequisite mechanics fall out for the build list: the **air/altitude/depth combat layer** (unblocks Fluid's deep half + Exotic-gravitic) and the **H8 gate-network + H1 teleport** layers (unblock Warp-gate + Exotic-teleport).

---

## ⚙ 2 — PROPULSION · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Propulsion is *how a thing moves*, and in this engine movement is a **combat input**, not decoration. It lands on a **second resolver surface** adjacent to the weapon/damage one: **Evasion** (per-ship, feeds the dodge math) + the **closing-fight state** (`Separation_m` / `ManeuverBudget`) + the **strategic transit** floors (warp/Δv) + the **ground hex-march** (speed). The headline finding, verified against the engine below: the five locked propulsion doors need **ZERO new resolver fields** except one — Exotic ▸ inertialess (an evasion-override term). The engine already runs Newtonian thrust, Tsiolkovsky Δv, the warp-bubble+jump system, and the ground-locomotion stack; nearly every dial writes a stat the resolver reads *today*. Propulsion is the cheapest category to wire so far.

Five doors, all 🔒 locked: **Reaction** (Newtonian rocket), **Traction** (surface/ground), **Fluid** (air/water lift+buoyancy), **Warp** (FTL/jump), **Exotic** (reactionless/gravitic/teleport — the physics-breaker slot).

---

### Pillar tags (§0h — PROJECTOR⛓COUNTER, pillar + skeleton-role)

Propulsion is **Military · Medium/Mobility** — it is not a projector (it fires nothing) and not a counter (it blocks nothing). Its skeleton-role is **Mobility/Medium**: it sets *reach and evasion*, the substrate every projector and counter is delivered across. In §0h terms, propulsion supplies the **Reach** and **positioning** on which a weapon's projection is graded — the fast hull dictates the range at which the weapon triangle resolves; the sluggish hull eats fire. So propulsion is graded against the **move/closing resolver** (the Newtonian/hex yardstick), NOT the joule combat resolver.

- **Pillar:** Military (primary). Secondary consumers via §0f multi-consumer rule: Logistical (transit/reach for freighters & convoys), Exploration (survey reach, network-reshaping via Warp▸gate).
- **Skeleton-role:** **Medium / Mobility** — never Projection, never Counter. (Contrast: Weapons = Projection, Defense = Counter.)
- **Graded against:** the move/closing model (`CalculateEvasion`, `AdvanceClosing`, `DeltaVFloor`/`WarpSpeedFloor`, `GroundMobility`), which §0i (count-resolvers-not-doors) names as **one of the ~7 resolver-shaped loops** — the "move/closing model." All 5 propulsion doors are **DATA on that one loop**, not 5 new loops.

---

### Complete dial → derived-stat → engine-wire table (VERIFIED)

Legend: **✅** field exists, dial writes it today · **➕** new term (one small resolver addition) · **⚙** deferred mechanic (needs a subsystem first). Every file:line checked against source.

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | Resolver insertion |
|------|------|--------------|-------|--------------------------|--------------------|
| **Reaction** | Thrust class | Acceleration → **Evasion** + who-dictates-range | ✅ | `NewtonThrustAbilityDB.ThrustInNewtons` (`Movement/NewtonMove/NewtonThrustAbilityDB.cs:10`) → `accel = thrust.ThrustInNewtons / mv.MassDry` → `CalculateEvasion` (`Combat/ShipCombatValueDB.cs:314,325`) | ✅ writes **Evasion** |
| Reaction | Exhaust velocity / Isp | **Δv** (Tsiolkovsky) → kiting clock | ✅ | `ExhaustVelocity` (`NewtonThrustAbilityDB.cs:13`) → `DeltaV = OrbitMath.TsiolkovskyRocketEquation(...)` (`:35,:46`) → `FleetCombat.DeltaVFloor` (`Combat/FleetCombat.cs:60,65`) → seeds `ManeuverBudget` (`Combat/CombatEngagement.cs:449,524`) | ✅ writes **ManeuverBudget** |
| Reaction | Fuel type / burn rate | Fuel economy / endurance | ✅ | `FuelBurnRate` (`NewtonThrustAbilityDB.cs:21`); `Thrust = Ve × FuelBurnRate`; refuel via `ShipFactory.FillFuelTanks` | — build/logistics-side (correct, not the salvo) |
| Reaction | Drive mass (emergent) | **MassDry** → feeds back into Evasion | ✅ | `MassVolumeDB.MassDry` read by `CalculateEvasion` (`ShipCombatValueDB.cs:325`) — heavier drive drags its own accel/evasion | ✅ writes **Evasion** (feedback) |
| **Traction** | Drive type / SpeedFactor | Ground march speed → H3 closing | ✅ | `GroundLocomotionAtb.SpeedFactor` → `GroundMobility.SpeedMultForUnit` (`GroundCombat/GroundMobility.cs:39,53`) → hex-march (`OrderMove`) | ✅ ground move surface |
| Traction | Terrain handling / RoughHandling | Off-road speed retention | ✅ | `GroundLocomotionAtb.RoughHandling` → `GroundMobility.RoughHandlingForUnit` (`:71,80`) → `TerrainMult(HexPathfinder.HexMoveMult,...)` (`:105`) | ✅ hex path cost |
| Traction | Amphibious / water-crossing | Passability (access) | ✅ | `GroundLocomotionAtb.Amphibious` + `HexPathfinder` water passability (ocean impassable → passable) | ✅ pathfinder gate |
| Traction | **Terrain COMBAT bonus** | Matchup edge on preferred ground | ➕ | rides **H3 hex-terrain-in-combat** (movement-through-terrain wired; a *combat* term is the add) | ➕ terrain-combat term |
| **Fluid** | Medium access / cross-water | Reach an isolated hex | ✅ (access) | rides `GroundLocomotionAtb.Amphibious` + `HexPathfinder` passability | ✅ (access half only) |
| Fluid | Vacuum/medium constraint | "needs atmosphere/water" gate | ➕ | a medium tag "this drive needs medium X" gating where it works | ➕ medium tag |
| Fluid | Lift type / cruise speed / capacity | Speed + carry-mass | ✅ (rides mobility) | speed + carry ride mobility + cargo stats; fluid framing is the wire | ✅ (shallow) |
| Fluid | **Altitude / depth band** (combat payoff) | Air-superiority / sub-stealth / over-under fire | ⚙ | needs the **air/altitude/depth combat layer** (space tags no medium; ground has no air/sea band) | ⚙ medium/altitude layer |
| Fluid | Air-vs-ground targeting (only AA hits a flyer) | Target-medium gate | ⚙ | same prerequisite — a target-medium gate in the resolver | ⚙ medium layer |
| **Warp** | FTL method (continuous / jump / gate) | Strategic transit mode | ✅ | `WarpAbilityDB` (`Movement/WarpMove/WarpAbilityDB.cs:6`) + jump-point system (`JumpOrder`, `InterSystemJumpProcessor`) | ✅ strategic map |
| Warp | MaxSpeed | **WarpSpeedFloor** (fleet moves as one) | ✅ | `WarpAbilityDB.MaxSpeed` (`WarpAbilityDB.cs:12`) → `FleetCombat.WarpSpeedFloor` (`FleetCombat.cs:44`, min over fleet) | ✅ writes transit floor |
| Warp | Bubble power (create/sustain/collapse) | Stored-electricity gate | ✅ (movement-side) | `WarpAbilityDB.BubbleCreationCost` (`WarpAbilityDB.cs:18`), paid from stored energy (`ChargeReactors`) | ✅ (movement, not salvo) |
| Warp | Drive mass (emergent) + fleet coupling | Speed drag + slowest-member | ✅ | `MassVolumeDB` → `WarpMath.MaxSpeedCalc(power,mass)`; `FleetCombat.WarpSpeedFloor`/`DeltaVFloor` min-over-fleet | ✅ aggregation |
| Warp | **Gate-user / network node** (Stargate) | Which gate reaches which | ⚙ | needs **H8 network/addressing layer**; `JPFactory.CreateConnection` is a dead stub (`JumpPoints/JPFactory.cs:124-132`) | ⚙ H8 gate-network |
| **Exotic** | Reactionless thrust | Infinite Δv, no propellant | ➕ (small) | a Reaction drive declaring `FuelBurnRate=0`; engine reads `ThrustInNewtons` directly (`NewtonThrustAbilityDB.cs:10`) | ➕ no-fuel flag (nearly free) |
| Exotic | **Inertialess maneuver** | Evasion decoupled from mass | ➕ | **the ONE genuinely new field** — an evasion-override path bypassing `accel = Thrust÷MassDry` in `CalculateEvasion` (`ShipCombatValueDB.cs:325-328`) | ➕ **new Evasion term** |
| Exotic | Gravitic / medium-independent | Works in any medium, no fuel | ⚙ | shares Fluid's air/depth medium layer | ⚙ medium layer |
| Exotic | Teleport / rings (H1) | Instant point-to-point matter move | ⚙ | **Transfer ▸ teleport mode** (H1); cousin to Warp▸gate (H8) | ⚙ H1 teleport |

---

### Resolver insertion points (from anatomy §7, adapted)

**The movement/evasion/closing surface — what a propulsion dial can touch (all live today):**
- **Evasion** (0..0.95), per-ship `ShipCombatValueDB.Evasion` — read by the dodge math (`HitFraction`). Written by Reaction thrust via `accel = ThrustInNewtons ÷ MassDry` → `CalculateEvasion` (`ShipCombatValueDB.cs:314`). Constants: `AgilityReference_mps2 = 5.0` (`:92`, accel at which agility half-contributes), `EvasionCap = 0.95` (`:96`), `SizeReference_m3 = 1000` (`:88`). Formula: `Evasion = EvasionCap × sizeFactor × agilityFactor`, `sizeFactor = SizeReference/(SizeReference+Volume)`, `agilityFactor = accel/(AgilityReference+accel)` (`:320,326,328`).
- **DeltaV** (m/s), per-ship `NewtonThrustAbilityDB.DeltaV` (`NewtonThrustAbilityDB.cs:35`, Tsiolkovsky at `:46`) → `FleetCombat.DeltaVFloor` (min over fleet, `FleetCombat.cs:60`) → seeds `FleetCombatStateDB.ManeuverBudget` at `CombatEngagement.cs:449,524`.
- **ManeuverBudget** (the kiting clock) — `AdvanceClosing` (`CombatEngagement.cs:847`): only a fleet with budget controls the range; the controller spends `ManeuverBurnRate × dt` (`:382 = 5.0`, drain at `:870`); a dry kiter loses control (`:857-858`) and the enemy closes.
- **Separation_m** (the gap) — `FleetCombatStateDB.Separation_m`, set at `CombatEngagement.cs:447-448`; read by `BuildFireMix` weapon-range gate + the `HitFraction` range term; advanced toward the controller's preferred range at `ClosingSpeedScale_mps × dt` (`:353 = 1e6`, `InitialSeparationDefault_m :360 = 1e6`).
- **Controller (who dictates range)** — `FleetManeuver(ships)` (`CombatEngagement.cs:889`): highest maneuver = min evasion picks the closing direction (`AdvanceClosing :860`).
- **WarpSpeedFloor** (m/s) — `FleetCombat.WarpSpeedFloor` (`FleetCombat.cs:44`, min over fleet) → strategic transit, fleet-moves-as-one.
- **Ground SpeedMult** — `GroundMobility.SpeedMultForUnit` (`GroundMobility.cs:39`) → the hex-march.

**The tiny extensions (3 items — vs weapons' six):**
1. **Evasion-override term** (Exotic ▸ inertialess) — a `CalculateEvasion` path (`ShipCombatValueDB.cs:314`) where Evasion is set directly, decoupled from `accel = Thrust÷MassDry`. Small, self-contained. *This is the ONLY genuinely-new resolver field the whole category needs.* Unlocks: a capital that dodges like a fighter.
2. **Reactionless no-fuel flag** (Exotic) — let a Reaction drive declare zero propellant (infinite Δv) at a big power/tech cost. Nearly free — engine already reads `ThrustInNewtons` directly.
3. **Terrain-combat term** (Traction) — the H3 hex-terrain-in-combat follow-on; a drive's preferred terrain gives a matchup edge, not just a movement one.

**The deferred mechanics (⚙ — each gates its dials, each a subsystem):**
- **Air/altitude/depth combat layer** — Fluid's deep half (altitude bands, submarine stealth, air-only-hit-by-AA) + Exotic-gravitic. Space combat tags no medium; ground has surface hexes but no air/sea band. Same prerequisite the Weapons▸Energy "Medium performance" dial deferred on.
- **H8 gate-network/addressing** — Warp ▸ gate / Stargate (which node reaches which). Same hole as the leadership C3 Relay door.
- **H1 Transfer ▸ teleport mode** — Exotic teleport / transporter / rings.

---

### Prerequisite fixes & dead stubs (file:line)

- **`JPFactory.CreateConnection` — a DEAD STUB** (`JumpPoints/JPFactory.cs:124-132`). The body is entirely commented out ("FIXME: commented out because it wasn't implemented"). This is the hook that would let survey/exploration *create* a jump route (Warp▸gate, network-reshaping) rather than only reveal one. The sibling `LinkJumpPoints` (`:134-141`) DOES work — it pairs two JPs by setting `DestinationId` both ways — so the reshaping gear calls `LinkJumpPoints` in-play once an anomaly resolves; `CreateConnection` is the stub that must first come alive. Named in the exploration essence and the E1b/E2 build order as the gate for Warp▸gate + Route Works (fills hole H8).
- **`JPSurveyProcessor` 100 km hardcode** (`JumpPoints/JPSurveyProcessor.cs:47`): `if (distance < 100000) // FIXME: needs to be an attribute of the JPSurveyAbilityDB`. Survey range is a magic constant, not a designed dial — blocks a Sensors▸Survey / Warp-adjacent reach dial from being authentic until it reads a component attribute.
- **No prerequisite fixes block the four ready cores** (Reaction / Traction / Warp / Fluid-access). Reaction's Δv→ManeuverBudget seam is confirmed wired (`FleetCombat.DeltaVFloor` → `CombatEngagement.cs:449,524`) and gauged (`ClosingTests` P2 kiting clock).

---

### Design essence captured inline (the network-reshaping / gate mechanic Warp▸gate must serve)

Deep-space exploration can **change the map, not just reveal it** — the headline of the merged `EXPLORATION-CONTENT-DESIGN.md` (origin/main). A precursor **gate** = a permanent shortcut; a studied **anomaly** = a *hidden* jump point, a **wormhole** (risky, maybe one-way), or a route you can **stabilize** — so exploring becomes *building your own strategic geography*: the shortcut that flanks a rival, the back door into their core. This is exactly what Warp▸gate (the ⚙ H8 dial) and the Route-Works megaproject exist to serve, and it is where the dead `JPFactory.CreateConnection` stub (`:124`) "comes alive" — a scientist posting resolves a site, the gear calls the working `LinkJumpPoints` (`:135`) in-play, and a route persists with a stability 0–1 that is its collapse/grave rung (a destabilized wormhole, a lost gate). A gate is therefore **an addressable network node whose destruction severs a link (H8)**, not merely a drive — the same physical shape as the leadership C3/Relay node, which is why §E2 collapses both onto the single hole H8.

---

### §0g stamp — Reachable · Mirrored · Observable

- **Reachable (cradle-to-grave, player-side):** ✅ Full chain lives. Fuel is the spine — mineral (mined) → propellant material (refined, e.g. `ntp`/deuterium) → stocked at a colony (`StartingItems`/`FillFuelTanks`) → burned by the drive → **runs dry** (empty tank = 0 thrust = dead in space → resupply). Drive is a **component** (`NewtonThrustAbilityDB`/`WarpAbilityDB`/`GroundLocomotionAtb`), designed/researched/built/installed; a destroyed drive is the grave rung (immobilized hull). No parachuted abstraction.
- **Mirrored (NPC runs it too, identical order path):** ✅ Propulsion is pure infrastructure the NPC uses *for* itself — the closing/kiting resolver (`AdvanceClosing`, `FleetManeuver`) is symmetric: an NPC fleet with more Δv dictates range and kites the player exactly as the player kites it; NPC ground units run the same `GroundMobility` hex-march. No player-only code — a fast NPC hull forces the merge, a fast NPC kiter opens the range.
- **Observable (the gauge, both sides):** ✅ Already gauged. `ShipEvasionTests` (thrust/mass → Evasion), `ClosingTests` (who-dictates-range + the P2 kiting clock), `FleetAggregationTests` (WarpSpeed/DeltaV floors), `GroundForcesTests` (hex-march). The readouts: **Evasion** (0..0.95), the **closing/Separation gap** with `ManeuverBudget` reserve (`CombatEngagement.cs:970-975,995-998` narrate "gap … reserve … IN RANGE/OUT"), Δv, warp-speed floor. INFORMATION-DELTA note: the engagement-range and Δv/ETA gauges are the exact numbers this door needs shown — the value exists (Failure-A, cheap wire), not missing.

---

### Cross-category shared state (Prime Directive)

Propulsion does not stand alone — it writes to state that other categories read/co-own:

- **⇄ Weapons / Defense (combat resolver):** Propulsion's **Evasion** feeds the weapons `HitFraction` dodge term, and **Separation_m** is the range gate `BuildFireMix` uses to drop out-of-range weapons. A fast hull's evasion is only meaningful against a weapon's tracking/saturation — the propulsion × weapon-triangle stack IS the fight. Shared blob: `ShipCombatValueDB` (holds both Evasion and Weapons), `FleetCombatStateDB` (Separation/ManeuverBudget).
- **⇄ Chassis (§11) / Power (§4):** Drive mass consumes the §0b chassis mass-budget AND feeds back into `MassDry` → Evasion. Ion/plasma/hover/warp drives draw electricity — a supply gate on Power▸Generation/Storage (`ChargeReactors`, warp bubble paid from stored energy). An uncharged ship won't warp; a drained hover unit stops.
- **⇄ Logistical (§8) / Industrial (§7):** Fuel is mined (§7 Extraction) → refined (§7 Fabrication) → stocked & moved (§8) → burned. A drive can burn a fuel the faction hasn't unlocked (the `ntp` gotcha) = no move.
- **⇄ Sensors / EW (§3):** A hot high-thrust burn spikes the thermal signature (Detection/EW) — the propulsion twin of a weapon's firing-heat, onto the same EMCON activity term. (◐ noted for the EMCON pass, not a Reaction-specific build.)
- **⇄ Exploration:** Warp▸gate + Route Works reshape the jump network (`JPFactory`), a second consumer of the survey/field-site loop.

---

### Holes owned / resolved (H8, H1, H11) → status + home

| Hole | What it is | Status | Home |
|------|-----------|--------|------|
| **H8** | Network/relay infrastructure — addressable, instant, many-to-one (Stargate network, hyperlane gates) | ⚙ **DEFERRED, designed** | **Warp ▸ gate** dial (§2.4) + a Fabrication megaproject (Route Works). Needs the addressing layer + the dead `JPFactory.CreateConnection` (`:124`) revived. Converges with the leadership **C3/Relay** door (§E2) — same physical shape, same hole. |
| **H1** | Matter teleportation — beam matter/crew point-to-point instantly (transporter, rings, Stargate-through-itself) | ⚙ **DEFERRED, designed** | **Exotic ▸ teleport** dial (§2.5 D) → a **Logistical ▸ Transfer teleport mode** (instant, ranged, capacity-limited). Cousin to H8 (both "instant point-to-point," one for matter, one for the whole ship). Most-recurring hole across franchises. |
| **H11** | Scale extremes — planet-sized (Death Star) to nanite (Replicator) | **RESOLVED elsewhere** | NOT a propulsion hole — it validates the **Chassis scale dial + tech/scale caps** span ~10 orders of magnitude (§11). Propulsion inherits it: the same thrust/speed dials scale across the chassis range (a booster to an antimatter torch) without a new door. Nothing propulsion owns. |

**Convergence (§E2):** the propulsion category surfaces exactly one net-new door-shaped hole — **H8 (Warp▸gate)** — and it lands on the *same* hole as the leadership C3/Relay. H1 rides on top of it (instant point-to-point). Everything else in the category is **dials on existing doors** (§0i: 5 doors = DATA on the one move/closing resolver, not 5 new loops). The two deferred mechanics for the build list: the **air/altitude/depth combat layer** (Fluid deep half + Exotic-gravitic) and the **H8 gate-network + H1 teleport** layers.

---

## §3 — Sensors

Sensors decide **what you know** — and knowledge drives half the game, not just the fight. Yes, in combat knowledge is a weapon (the resolver runs a fog layer: a fight only forms once someone *detects* the enemy, the side that sees first shoots first, and a blinded fleet takes fire without returning it — the `FirstStrike_SeerWipesBlindEnemy_Unscathed` gauge). But this is the category where the **multi-consumer rule (§0f)** bites hardest: the *same* sensor technology is a warship's targeting array, a **survey ship's science suite**, a **colony's deep-space observatory**, a **spaceport's traffic-control grid**, and an **explorer's tricorder**. Star Trek, Stargate, Mass Effect, and Stellaris are *exploration and science* franchises first — grading Sensors by combat alone would design half the category. So this category is graded against **all** its consumers.

**The category's consumers (per §0f — combat is one of five):**
- **Combat resolver** — Detection (the fog gate `FleetDetects` → the first-strike resolver `CanEngageTarget`), Fire Control (the `HitFraction` tracking term — how well your guns land on a dodger), EW (jam/spoof/cloak change what the enemy detects).
- **Exploration / science / research** — Survey's stellar-cartography, anomaly-hunting, and xenoarchaeology feed the **tech + discovery** loop (the Stellaris/Stargate mystery-box; the Mass Effect scan-a-planet loop).
- **Economy / industry** — geo-survey → the minerals you can mine.
- **Expansion / navigation** — grav-survey → jump points; hazard-survey → safe routes.
- **Colonization** — atmospheric/biosphere survey → which worlds are habitable.
- **Diplomacy / first-contact** — life-sign + civilization detection → who's out there, and whether that world is already someone's home.

**The load-bearing finds from the engine audit:**
- **Detection is richly built** (a real EM-signature + attenuation + threshold + band + EMCON model — `SensorReceiverAtb`, `SensorProfileDB`, `AttenuationCalc`, `DetectionRange_m`, `FleetEmcon`) → overwhelmingly **Modelled**, and it serves navigation/SAR/observatory duty as readily as targeting.
- **Survey is NOT thin** — it only *looked* thin graded against combat. Widen the lens to the whole sci-fi survey space (cartography, life, atmosphere, anomalies, xeno-ruins, hazards) and it's one of the game's richest exploration doors — with two **franchise-earning net-new systems** hiding in it (the exploration mystery-box + xenoarchaeology).
- **Fire Control is a nest of DEAD KNOBS** — `BeamFireControlAtbDB` carries `Range`/`TrackingSpeed`/`FinalFireOnly` and **nothing reads them** — the exact "fidelity nobody acts on" the audit hunts; the door's job is to CONNECT them. (And the *precision-pointing* tech generalizes past weapons — mining lasers, tractor beams, docking, comms-laser aiming.)
- **Electronic Warfare is net-new** (no jam/spoof/cloak component exists) — but the detection engine hands it a ready insertion surface (and its civilian cousin is comms/signal management).

So this category is the cleanest showcase of Keep/Cut/Add/**CONNECT**, now read across *all* consumers: Detection = keep+expose (military AND civilian), Survey = **expose the whole exploration space** + flag two net-new science loops, Fire Control = **connect the built-but-dead knobs**, EW = add onto the existing detection math.

### 3.0 Shared sensor dials (common to all four doors)
On top of the universal seven (§0a), every sensor has:
| Dial | Drives (real stat) |
|------|--------------------|
| **What it detects (waveform band)** | `SensorReceiverAtb.RecevingWaveformCapabilty` — which EM band it's tuned to (thermal / radar / visual); narrow = deep in one band, wide = shallow across many |
| **Sensitivity / reach** | `BestSensitivity_kW` (the threshold — lower = fainter targets seen) → `DetectionRange_m` (via `RangeForSignal`) |
| **Refresh rate** | `ScanTime` (s) — how often the picture updates (fast = catches movers, costs more) |
| **Active ↔ passive posture** | active = ping (see cold/hidden targets, but light yourself up via `ActivityMultiplier`); passive = listen (silent, only sees emitters) |
| **Signature contribution** | a sensor that pings *emits* — feeds the EMCON/Detection loop back on itself |

The doors differ in **what the knowledge is FOR** — spotting the enemy (Detection), mapping the world (Survey), pointing the guns (Fire Control), or denying the enemy their knowledge (EW).

### 3.1 Sensors ▸ DETECTION  🔒 *locked*
*The eyes: passive and active sensors that build the contact picture — who's out there, where, how far. Everything from a cheap thermal eyeball to a deep-space active array to a stealth-hunting multi-band suite falls out of these dials. The engine already runs the whole contact/attenuation/threshold model, so this door is overwhelmingly Modelled — it exposes dials onto a live system that already gates combat.*

**The core decision — SEE FURTHER vs STAY HIDDEN (the loud/quiet bet, the EMCON lever).** You cannot both blast active radar to see everything AND stay dark. An active ping reveals cold, silent, stealthed targets (their *reflected* return) — but it **lights you up** far louder than you can see (`AttenuationCalc` is 1/4πd² each way, so your ping is seen at ~twice your own detection range). Passive listening is silent but only catches things that *emit* (a running engine, a firing gun). The decision is *dark-vs-loud* (`docs/DETECTION-DESIGN.md`), not wavelength-tuning — the band math is hidden behind the gameplay.

**A. Sensitivity — how faint a signal it catches (the reach dial)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Coarse (high threshold)** | cheap, light, low power | **short range** — only sees loud/close targets |
| **Sensitive (low threshold)** | sees **faint + far** (fainter `BestSensitivity_kW` → longer `DetectionRange_m`) | big, power-hungry, costly; a huge dish is heavy |

**B. Waveform band — WHAT part of the spectrum it sees**
| Option | Why | Catch |
|--------|-----|-------|
| **Narrow / tuned** | **deep** in one band — sees a specific signature (heat) very far | **blind** to everything outside the band (a thermal eye misses a cold hull) |
| **Wide / multi-band** | catches many signature types — a stealth hull hiding in one band shows in another | shallower in each — **master of none**, more mass/cost |

**C. Active ↔ passive — ping or listen (the EMCON decision)**
| Option | Why | Catch |
|--------|-----|-------|
| **Passive (listen)** | **silent** — you see without being seen; the ambush/scout posture | only catches **emitters** (running/firing targets); a dark drifting hull is invisible |
| **Active (ping)** | sees **cold/stealthed** targets via their reflection (the only way to find a dark hull) | **lights you up** (`ActivityMultiplier` ↑) — seen from ~2× your own range; you find them AND announce yourself |

**D. Refresh rate — how fresh the picture is (`ScanTime`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Fast sweep** | fresh track — catches **fast movers**, tight firing solution | more power/heat; a rapidly-pinging active is even louder |
| **Slow sweep** | cheaper, can integrate for **more range** | **stale** track — a fast target has moved since the last look |

**E. Resolution — contact DETAIL (`Resolution`, MP)**
| Option | Why | Catch |
|--------|-----|-------|
| **Low-res** | a blip — "something's there" | can't tell one ship from a fleet, or a freighter from a battleship (IFF/class unknown) |
| **High-res** | reads the contact's **class + count** (is that a picket or a dreadnought?) | heavier, costlier; ◐ currently stored but not yet driving contact quality |

**Derived stats (computed, shown to the player):**
`detection range = RangeForSignal(target signature, threshold)` · self-detection range (how far *you're* seen) · refresh interval · which bands you cover · active-emission spike · mass · cost.

**Modellability audit (§0d — the detection engine is live and gates combat):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Sensitivity / reach | ✅ | `SensorReceiverAtb.BestSensitivity_kW` → `SensorTools.RangeForSignal` / `DetectionRange_m` (the reverse-solve; master knob `DetectionSensitivityScale 1e6`) |
| Waveform band | ✅ | `RecevingWaveformCapabilty` (`EMWaveForm` peak ± bandwidth) vs the target's `SensorProfileDB` spectra (`DetectonQuality` band-overlap) |
| Active ↔ passive | ✅ | `SensorProfileDB.ActivityMultiplier` (active ping raises your own signature) + reflected-vs-emitted spectra; the EMCON posture (`FleetEmcon` Full/Cruise/Silent 1.0/0.5/0.15) |
| Refresh rate | ✅ | `SensorReceiverAtb.ScanTime` → the `SensorScan` reschedule cadence |
| Resolution / contact detail | ◐ **wire** *(re-homed — see Main-Merge Delta)* | `Resolution` (MP) is stored but not yet driving contact detail. **Note (main-merge 2026-07-07): `SignalQuality` is DESIGN-CUT** (the engine field survives, but detection collapses to *strength only* — see-it-or-not + how-loud). So this dial no longer hooks `SensorReturnValues.SignalQuality`: the **coarse** read (blip vs. big) falls out of `SignalStrength_kW × cross-section` automatically (no dial), and the **fine** read (class / IFF confidence) moves OUT of the sensor into the espionage **Information Ledger** (Inferred→Confirmed→Stale). As a *detection* dial, Resolution is effectively deferred; its real home is the Survey door's survey-points mechanic |
| **→ the combat resolver** | ✅ | contacts populate `SystemSensorContacts` → the fog gate `FleetDetects` (`RequireDetectionToEngage`) → the first-strike resolver `CanEngageTarget` (see-first-shoot-first) |
| Sensors boost evasion (harder to hit if you see the shot coming) | ⏳ **defer** | a flagged v2 hook in `ShipCombatValueDB` (Evasion currently leaves sensors/crew out) |

**Reading:** Detection is **the most-built door in Sensors** — the entire contact/attenuation/threshold/band/EMCON stack exists and already *decides combat* (fog of war + first strike). Nearly everything is ✅; one ◐ wire (resolution → contact detail) and one ⏳ (sensors→evasion, a v2 combat refinement). Cradle-to-grave is closed: a sensor is a **component** (researched/built/installed), and a **shot-off** receiver blinds you — the grave rung is *already wired* (`SensorTools.SetInstances` empties the cache on damage; the `FirstStrike` gauge exploits exactly this).

**Numbers (calibrated to the live detection math):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **Sensitivity threshold** | kW | lower = better (fainter seen) | `BestSensitivity_kW` → `RangeForSignal` |
| **Waveform band** | nm (peak ± bandwidth) | tuned narrow → wide | `RecevingWaveformCapabilty` vs target spectra |
| **Active emission** | ×mult | Silent 0.15 · Cruise 0.5 · Full 1.0 (+ heat: thrust ×4, firing ×1) | `ActivityMultiplier` / `EmconActivityProcessor` |
| **Scan time** | s | fast → slow sweep | `ScanTime` reschedule |
| **Master scale** | — | `DetectionSensitivityScale 1e6` | the one tuning knob behind all ranges |

**Preset coordinates — the span:**
| Sensor | Band | Posture | Sensitivity | The trade it chose |
|--------|------|---------|-------------|--------------------|
| **Thermal eyeball** | narrow (heat) | passive | coarse | cheap, silent, sees hot engines close; blind to cold/stealth |
| **Deep-space array** | narrow | passive | very sensitive | sees emitters system-wide; huge, blind to dark hulls |
| **Active radar** | radar | active | mid | finds cold/stealthed hulls; lights you up (seen at 2× your reach) |
| **Multi-band suite** | wide | switchable | sensitive | the stealth-hunter — covers every band; heavy + costly |

**Beyond combat (§0f) — the same door builds the civilian & facility sensors:** detection isn't only "spot the enemy." The identical dials build a spaceport's **traffic-control array** (track friendly hulls to avoid collisions), a colony's **planetary early-warning grid** (a FACILITY watching the sky), a **search-and-rescue** receiver tuned to distress beacons, a **navigation** sensor that flags debris/hazards on a route, and a **solar-flare warning** watch (paired with the hazard system). A detection component on a **Structure chassis** *is* a fixed sensor installation — the observatory's military cousin. So the audit's "✅ Modelled, gates combat" doubles as "✅ Modelled, serves navigation/SAR/traffic-control," same engine, different consumer.

### 3.2 Sensors ▸ SURVEY  🔒 *locked*
*The science suite — the eyes of every EXPLORER, prospector, colonist, and first-contact mission. This is the "explore" pillar of the 4X in one door: it answers the questions that drive expansion and discovery — **what's out there** (cartography), **what's it made of** (geology), **is it alive** (biosphere), **can we live there** (habitability), **is it safe** (hazards), **where can we go** (jump points), and **what did the ancients leave behind** (xenoarchaeology). Graded against combat alone it looked thin; graded against the whole sci-fi survey space (per §0f) it's one of the richest doors in the game — and it surfaces **two franchise-earning systems the game doesn't have yet** (the exploration mystery-box + xenoarchaeology). The Enterprise, the Normandy's planet-scanner, a Stargate survey team, a Stellaris science ship, a deep-space observatory — all fall out of these dials.*

**The core decision — WHAT to look for, HOW DEEP, and from HOW FAR.** A survey suite is a bundle of scanning **modes**; a mining prospector, a science explorer, and a xeno-dig ship are the *same door* with different modes dialed up. On top of *which* modes you carry, you choose **depth** (a quick "minerals here" flag vs a deep multi-pass analysis that reads exact deposit sizes / atmosphere composition / anomaly resolution) and **reach** (scan from orbit, from a safe standoff, or land-and-dig). The trade is *breadth vs depth vs cost* — a do-everything science flagship is huge and expensive; a single-mode probe is cheap but blind to everything else.

**A. Survey mode — WHAT you're scanning for (the rich core dial; each mode feeds a different consumer, §0f)**
| Mode | Reveals → feeds | Consumer |
|------|-----------------|----------|
| **Geological** | a body's **mineral deposits** (+ ground regions) | economy / mining |
| **Gravitational** | **jump points**, gravity wells, stable lanes | expansion / navigation |
| **Stellar / cartography** | bodies + system layout **from afar** (a telescope maps a system before you send a ship) | expansion / exploration |
| **Atmospheric / planetary** | atmosphere, gravity, temperature → **habitability** | colonization |
| **Biosphere / life-signs** | **is this world alive? inhabited?** ("life signs, Captain") | colonization + first-contact |
| **Anomaly / phenomena** | spatial anomalies, derelicts, subspace echoes — **the mystery-box** | research / exploration |
| **Xenoarchaeology** | **ruins & artifacts** on a surface → ancient tech | research |
| **Hazard / navigational** | asteroid fields, radiation, nebulae, **solar-flare forecast** → safe routes | navigation / safety |

**B. Survey depth / resolution — how much you learn per pass**
| Option | Why | Catch |
|--------|-----|-------|
| **Quick / shallow** | fast "there's *something* here" flag; light, cheap | no detail — you know a deposit exists, not its size or grade |
| **Deep / analytical** | the full breakdown (exact tonnages, atmosphere mix, anomaly resolved) | slow, needs onboard analysis or a data-center to process |

**C. Survey speed — how fast a target completes (`Speed`, points/tick — the one dial the engine has today)**
| Option | Why | Catch |
|--------|-----|-------|
| **Slow** | light, cheap rig | a body/point takes many ticks |
| **Fast** | clears survey points quickly — map more, sooner | heavier, costlier |

**D. Reach / access mode — orbit vs standoff vs surface**
| Option | Why | Catch |
|--------|-----|-------|
| **Remote / long-range** (telescope, deep-space array) | survey from a safe standoff or **another system** (astronomy) — the FACILITY observatory | lower detail at extreme range; can't ground-truth |
| **Orbital** | park and scan — the workhorse | must reach the body (the grav gate is ~100 km today — ◐ a hardcoded FIXME to expose as a dial) |
| **Contact / surface** (lander, dig, away-team) | the only way to **xenoarchaeology** or ground-truth geology | slow, exposed, needs to physically land |

**E. Data processing — turning raw scans into knowledge**
| Option | Why | Catch |
|--------|-----|-------|
| **Onboard analysis** | the ship reads its own scans → instant knowledge | adds mass/power/crew (a science lab aboard) |
| **Gather-and-relay** | a light collector ships raw data home to a **survey-data center** (a FACILITY) for processing | knowledge lags until it's processed; needs the facility |

**F. Autonomy — crewed vessel vs probe/drone**
| Option | Why | Catch |
|--------|-----|-------|
| **Crewed science vessel** | reusable, adaptive — can **react to an anomaly** it finds (the Enterprise) | expensive, risks a crew |
| **Autonomous probe** | cheap, expendable, one **deep scan** then done (Voyager, a Mass Effect probe) | one-shot, can't adapt, no crew to make a judgment call |

**Modellability audit (§0d, graded against the WHOLE exploration pillar per §0f — a spectrum, honestly):**
| Dial / mode | Verdict | How the sim models it (and which consumer) |
|-------------|---------|---------------------------------------------|
| **Geological** | ✅ | `GeoSurveyAtb` → `GeoSurveyProcessor` reveals minerals/regions → **economy** |
| **Gravitational** | ✅ | `GravSurveyAtb` → `JPSurveyProcessor` discovers jump points → **expansion** |
| Survey speed | ✅ | `Speed` (points/tick) vs a `PointsRequired` pool |
| Survey range / reach | ◐ **wire** | the ~100 km grav gate is hardcoded (`JPSurveyProcessor` FIXME) — expose as a dial |
| **Stellar / cartography** | ◐ **wire** | the galaxy is generated + a fog/reveal exists (survey is world-level v1) — extend to **long-range system-reveal** (see a system's bodies before you visit) → **expansion** |
| **Atmospheric / habitability** | ◐ **wire** | `SystemBodyInfoDB` already holds atmosphere/gravity/temperature + `CanStartHere` — expose them as a **survey RESULT that gates colonization** → **colonization** |
| **Biosphere / life-signs** | ◐ **wire** | colony/population data exists — expose "is this world inhabited?" as a survey result feeding the **first-contact** path (which exists, `DiplomacyFirstContactTests`) → **diplomacy** |
| **Hazard / navigational** | ◐ **wire** | `Hazards/` exists (gas clouds, solar flares) — a survey that **reveals/forecasts** them → **navigation/safety** |
| Survey depth / resolution | ◐ **wire** | `Resolution` is stored (like Detection's) — drive the detail level of a reveal |
| Data processing (facility analysis) | ◐ **wire** | a survey-data **facility** (Structure chassis) that converts raw scans → knowledge (ties to the research/Civic consumer) |
| Autonomy (probe) | ◐ **wire** | a cheap one-shot survey entity — reuses the survey component on a minimal chassis |
| **Anomaly / phenomena (the mystery-box)** | ⏳ **defer — NET-NEW, now DESIGNED** | the Stellaris/Star Trek exploration engine. *No anomaly system in engine yet* — but the design now exists: it's a **catalog entry on the one universal field-site loop** (`docs/EXPLORATION-CONTENT-DESIGN.md`, main-merge 2026-07-07): survey finds a site → assign scientist(s) → study → yields research **+ an event chain** (can seed a late-game crisis). Not a separate engine — one loop, one data entry. Deepest build item. **Franchise-earning: this IS how you stage "explore the unknown."** |
| **Xenoarchaeology** | ⏳ **defer — NET-NEW, now DESIGNED** | Stargate/Mass Effect Prothean-ruins/Stellaris precursors. *No relic system in engine yet* — but designed as the **"Ancient ruins" catalog entry on the same field-site loop** (`EXPLORATION-CONTENT-DESIGN.md`): excavate (one-shot) → a tech windfall or a unique blueprint you can't research normally; guardians/traps can kill the team. **This is the loop's FIRST build step** — it fixes the named `SystemBodyFactory.GenerateRuins` tautology bug + wires a consumer, proving the loop end-to-end. **Franchise-earning.** |

**Reading:** Survey is **not thin — it was mis-measured.** Graded against combat it's one dial; graded against the exploration pillar it *belongs* to (§0f) it spans **eight scanning modes** feeding five different consumers, with an honest gradient: two ✅ **built** (geo/grav), a broad band of ◐ **wire** where the *data or system already exists and just needs exposing as a survey result* (cartography, habitability, life-signs, hazards — the biggest cheap-win cluster in the category, because `SystemBodyInfoDB`/`Hazards`/first-contact are all already in-engine), and two ⏳ **net-new loops that are genuinely missing AND franchise-earning** — the **exploration mystery-box** (anomalies) and **xenoarchaeology** (ancient tech). Those two aren't "thin door" filler; they're the **explore-and-discover half of the 4X** the game doesn't have yet. *(Main-merge 2026-07-07:* they are now **DESIGNED** — collapsed into **one** universal *field-site loop + catalog* (6 planetary + 5 space site types), not two engines, in `docs/EXPLORATION-CONTENT-DESIGN.md`. Naming them here is what set that design up.*)* It turns "Survey is shallow" into "Survey surfaces the science loops the north-star franchises are built on." *This is the multi-consumer rule (§0f) paying for itself.*

**Numbers:** `Speed` (points/tick) vs each mode's `PointsRequired`; `Resolution` (depth); range (m — the ~100 km gate → a dial). The two net-new loops define their own reward scales (anomaly → a research/event payout; xenoarchaeology → a tech unlock) when built. Cradle-to-grave: survey is a **component** on a science ship **or a facility** (observatory / data-center); the anomaly + xenoarchaeology loops would research-gate their capability and reward *new* tech — closing the vertical (research → build the scanner → discover → unlock more research).

**Preset coordinates — the sci-fi span (ship AND facility, all civilian):**
| Unit | Modes | Reach | The role it fills |
|------|-------|-------|-------------------|
| **Prospector** | geological | orbital | the miner's scout — finds ore to dig |
| **Science vessel / explorer** | cartography + life + atmosphere + anomaly | orbital, crewed | the Enterprise / Normandy — the do-everything discoverer |
| **Deep-space observatory** *(FACILITY)* | stellar cartography | remote (system-wide) | maps distant systems from a colony before any ship sails |
| **Survey probe** | one deep mode | orbital, autonomous | Voyager — cheap, expendable, one-shot deep scan |
| **Xeno-dig ship** | xenoarchaeology + geology | surface / away-team | Stargate/Prothean-ruins — lands to unearth ancient tech |
| **Pathfinder** | hazard + gravitational | remote | maps safe routes + jump points ahead of a fleet or colony wave |
| **Survey-data center** *(FACILITY)* | data processing | — | turns the fleet's raw scans into unlocked knowledge (the research front-end) |

The lesson mirrors the weapons/propulsion doors: you don't pick "science ship" from a menu — you dial which **modes** and how much **depth/reach/autonomy**, and the archetype falls out. A probe is a probe because you dialed autonomy up and breadth down; the Enterprise is the Enterprise because you carried every mode on a big crewed hull. And crucially, the **facility** presets (observatory, data-center) prove the door is for *infrastructure*, not just ships — the §0f corollary in action.

### 3.3 Sensors ▸ FIRE CONTROL  🔒 *locked*
*The gun-layer: the fire-control system that points your weapons — tracking a dodging target, reaching to a weapon's max range, and splitting fire across multiple targets. A good fire-control **multiplies the guns you already have** without adding a barrel. **This door's headline is a real engine find:** the fire-control component EXISTS with dials (`Range`, `TrackingSpeed`, `FinalFireOnly`) — but the resolver **doesn't read a single one of them**. Hit chance comes off the weapon, not the fire-control. So today these are **dead knobs**, and this door's entire job is to CONNECT them — the cleanest Keep/Cut/Add/**Connect** case in the game.*

**The core decision — POINT THE GUNS: reach vs tracking vs fire-splitting.** Your weapons have raw range and accuracy; fire control is the force-multiplier that decides how much of it you actually land. A fast-tracking director lands shots on nimble dodgers (beats evasion); a long-range director lets your guns open fire sooner; a multi-target director splits fire across a swarm instead of over-killing one hull. You can't max all three cheaply — the trade is *precision vs reach vs breadth*.

**A. Tracking speed — landing shots on dodgers (`TrackingSpeed`, currently INERT)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Slow tracking** | cheap, light — fine vs big slow capitals | a nimble fighter **juki your fire** (low effective tracking → the dodge wins) |
| **Fast tracking** | lands on **evasive** targets (raises the resolver's Tracking term → beats evasion) | heavier, costlier; overkill vs sluggish hulls |

**B. Fire-control range — how soon you can open fire (`Range`, currently INERT)**
| Option | Why | Catch |
|--------|-----|-------|
| **Short** | cheap, compact | you must **close** before the guns bear — even a long-range weapon is gated by the director |
| **Long** | opens fire at the **weapon's** full reach (the standoff enabler) | big, costly; the director's reach is only useful if the weapon's range matches |

**C. Fire allocation — one target or many (`FinalFireOnly` + a targets-tracked dial)**
| Option | Why | Catch |
|--------|-----|-------|
| **Single-target** | concentrates all fire — kills one hard target fast | over-kills a swarm; can't spread to multiple threats |
| **Multi-target** | **splits fire** across several contacts (feeds the multi-party fire-division) | thinner per-target — slow to kill any one |
| **Point-defense only** (`FinalFireOnly`) | dedicates the director to **intercepting incoming** ordnance | not available for anti-ship fire |

**Physical demands / the honest state:** fire control adds mass + crew + power like any component, but its real cost today is that **none of it is wired** — the dials sit on `BeamFireControlAtbDB` and the resolver reads the *weapon's* `BaseHitChance`/`MaxRange` instead. Building the door **is** building the wire.

**Modellability audit (§0d — the whole door is ◐ Wire: built component, unread by the resolver):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Tracking speed | ◐ **wire** | `BeamFireControlAtbDB.TrackingSpeed` **exists but is unread** → wire it into the `HitFraction` **Tracking** term (the field that beats evasion) |
| Fire-control range | ◐ **wire** | `BeamFireControlAtbDB.Range` **exists but is unread** → wire it into the weapon-range engagement gate (a director-gated reach) |
| Fire allocation / multi-target | ◐ **wire** | `FinalFireOnly` **exists but is unread** + a new targets-tracked dial → the multi-party **fire-split** (`StepEngagementGroup` already divides fire 1/N; make N a director property) |
| PD-only mode | ◐ **wire** | `FinalFireOnly` → the point-defense role (pairs with the weapons-door PD wire + missiles-as-targets) |

**Reading:** Fire Control is the **purest Connect door** — the component and its dials are *already built* (`BeamFireControlAtbDB`), they're just **dead** because the resolver never reads them (Failure A from `docs/INFORMATION-DELTA-DESIGN.md`: the number exists, unwired — cheap). The door's whole value is the wire, and it's a **small, concrete** one: three existing fields → three resolver terms the salvo math already has slots for (Tracking, the range gate, fire-split). This is exactly the audit's "stop feeding the pretty — connect it" — a built system currently earning nothing, turned into a real decision by wiring, not building.

**Numbers:** `TrackingSpeed` → the `HitFraction` tracking term (0..1, vs `EvasionCap` 0.95); `Range` → the weapon-range gate (m, the range ladder); targets-tracked → the fire-split N. Calibrated to the *same* dodge/range constants the weapons doors use — fire control just *feeds* them from the director instead of the gun.

**Beyond combat (§0f) — precision-pointing is a general capability, not just gun-laying:** the underlying tech — *hold a directed system precisely on a moving target at range* — is the same whether the directed thing is a weapon, a **mining laser** (hold the cut on an ore vein), a **tractor/pusher beam** (grapple a tumbling hull or nudge an asteroid), a **precision docking** guide (mate a freighter to a station), a **comms laser** (keep a tight-beam link locked on a distant relay), or a **point-to-point transfer** targeter. So the Fire Control door is really the **targeting/directing** door; its combat face is the loudest consumer, but the wire that makes it real (read the director's tracking/range instead of hard-coding the weapon's) is the same wire that lets a civilian rig aim *anything*. A tracking director on a Structure chassis aims a colony's ground-based defense grid or a station's docking control.

**Preset coordinates:** point-defense director (fast track, short range, PD-only) · main-battery director (long range, single-target, slow track) · fleet-track director (multi-target, mid everything) · fighter-killer (max tracking, short range) · **mining/utility director** (a civilian tracking rig for lasers/tractors — the same door, no weapon).

### 3.4 Sensors ▸ ELECTRONIC WARFARE  🔒 *locked*
*The information-denial suite: jamming, spoofing/decoys, cloaking, and counter-EW — the counter to Detection. If you can't out-gun the enemy, you blind them, lie to them, or hide from them. This is a **net-new** capability (no EW component exists in the engine today), and the categories doc says it **earns** its own door. It is NOT a blank slate, though: the detection engine hands every EW effect a ready insertion surface, and cloak is nearly free (EMCON already does most of it).*

**The core decision — WIN THE INFORMATION FIGHT (turn the fog against the enemy).** The combat resolver already rewards seeing first (first strike: a blinded fleet takes fire without returning it — the `FirstStrike_SeerWipesBlindEnemy` gauge). EW is how you *manufacture* that blindness on the enemy: shrink what they detect (jam), fill their plot with ghosts (spoof), or drop off it yourself (cloak). Each buys an information edge and pays in mass, power, and — for active jamming — a signature spike that can give you away.

**A. Jamming — degrade enemy detection**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Barrage jam** | **shrinks the enemy's detection range** / degrades their contact quality on you and your fleet | a loud active jammer is itself a **beacon** (you're easy to find, just hard to resolve) |
| **Targeted jam** | blinds a **specific** sensor/fire-control (break their firing solution) | narrow — one target at a time; needs to detect them first |

**B. Spoofing / decoys — inject false contacts**
| Option | Why | Catch |
|--------|-----|-------|
| **Decoy drone** | a cheap emitter that **reads as a warship** — soaks fire, pads your apparent numbers | consumable; a high-resolution sensor can tell it's a ghost |
| **Spoof / phantom fleet** | fills the enemy plot with **fake contacts** — they can't tell the real strike from the noise | the full "which contact is real" payoff needs the intelligence layer (below) |

**C. Cloak / stealth — suppress your own signature**
| Option | Why | Catch |
|--------|-----|-------|
| **Signature damping** | drops your emitted/reflected signature → seen only up close (the ambush enabler) | costs mass/power; and it's a **posture** — firing or hard-burning breaks it (heat spikes the signature) |
| **Full cloak** | near-invisible until you fire | expensive, high-tech; usually forces you dark (no active sensors while cloaked) |

**D. Counter-EW / ECCM — resist their EW**
| Option | Why | Catch |
|--------|-----|-------|
| **ECCM hardening** | burns through jamming / filters spoofed contacts — keeps your picture real | dead weight vs an enemy with no EW (most of them); needs the jam mechanic to exist first |

**Modellability audit (§0d — net-new, but every effect has a named hook on the detection engine):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Jamming** (degrade enemy detection) | ◐ **wire** | shrink the enemy's `DetectionRange_m` against you / raise the `SignalStrength_kW`-vs-sensitivity threshold they need to resolve you — the detection math exists; add a jammer term to the scan. *(Main-merge 2026-07-07: no longer routes through the design-cut `SignalQuality`; jamming works on the strength substrate — "EMCON's whole substrate" — which the design kept.)* |
| **Cloak / signature damping** | ◐ **wire** | push your own `ActivityMultiplier` / `SignatureBaseMultiplier` down — **EMCON already does this** (`FleetEmcon` Silent 0.15); a cloak is a stronger, component-based EMCON |
| **Decoy / spoof** (false contacts) | ◐ **wire** (injector) + ⏳ **defer** (the reading game) | inject a false `SensorContact` into the enemy track table (the table + `SensorContact` exist); but the "decide what's real" gameplay wants the **intelligence/information-ledger layer** (`docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`) for its full payoff |
| **Counter-EW / ECCM** | ⏳ **defer** | the mirror of jamming — needs the **jam mechanic built first**, then a resist term |
| **→ the combat resolver** | ✅ (via detection) | all of the above act on what `FleetDetects` returns → the first-strike resolver already honours a blinded fleet (`CanEngageTarget`), so EW × Detection × Weapons *stacks today* the moment the detection deltas are wired |

**Reading:** EW is the **one Sensors door that's mostly Wire/Defer** — the counter-detection layer genuinely isn't built (no jam/spoof/cloak component). But it is **not vaporware**: the rich detection engine (signature multipliers, signal strength, the contact table, EMCON) gives every EW effect a concrete hook, and **cloak is nearly free** (it's a stronger EMCON, which already runs). The one true deferral is the *decide-what's-real* spoofing game, which rightly waits on the intelligence layer. And because the resolver's fog + first-strike are already live, EW's payoff lands the instant its detection-deltas are wired — no new resolver work, it rides the same fog gate detection does.

**Numbers:** jam → a reduction on enemy `DetectionRange_m` (raise their strength-vs-sensitivity threshold); cloak → your `ActivityMultiplier` (vs EMCON's 0.15 Silent floor — a cloak pushes lower); decoy → a false `SensorContact` with a chosen apparent signature. Calibrated to the *same* attenuation/EMCON constants Detection uses — EW just pushes them the other way. Cradle-to-grave: an EW suite is a **component** (research the net-new tech → build → install → lose it and your edge evaporates).

**Beyond combat (§0f) — signal management is the civilian cousin of EW:** the same "shape what signals go where" technology runs a **comms relay** (route + boost friendly signal across a system — the constructive twin of jamming), a smuggler's **civilian stealth** (run quiet past a patrol — cloak without a war), and a colony's **signal-discipline** posture. The cloak dial is literally a stronger EMCON, and EMCON is already every ship's peacetime *and* wartime lever. So while EW's *offensive* face (jam/spoof) is a combat/espionage tool, its substrate (signature control + signal routing) is a standing civilian capability — an EW/comms suite on a Structure chassis is a colony's communications-and-countermeasures array.

**Preset coordinates — the span:** escort jammer (barrage jam, blinds the enemy screen) · stealth raider (full cloak, first-strike ambush) · decoy tender (spoof drones, pads the plot) · ECCM flagship (counter-EW, keeps the fleet's picture clean) · **comms relay** *(FACILITY/ship — the constructive cousin: routes + boosts friendly signal)*.

---

## ✅ §3 Sensors — COMPLETE (4/4 doors locked)
Detection 🔒 · Survey 🔒 · Fire Control 🔒 · Electronic Warfare 🔒. **Multi-consumer yardstick (§0f) — combat is ONE of five.** Detection → combat fog + navigation/SAR/traffic-control/observatory; Fire Control → combat targeting + precision-pointing of mining/tractor/docking/comms; EW → combat jam/spoof/cloak + civilian comms/signal-discipline; Survey → **the entire explore-and-discover pillar** (economy · expansion · colonization · research · first-contact). Headline readings, now read across all consumers:
- **Detection is the most-built door** — the whole EM-signature/attenuation/threshold/EMCON engine is live and already *gates combat*, and the same engine serves the civilian/facility sensors (nearly all ✅).
- **Survey is one of the RICHEST doors, not the thinnest** — it only looked thin measured by combat. Widened to the sci-fi survey space it spans **eight scanning modes** across five consumers: two ✅ built (geo/grav), a big ◐ **wire** cluster where the data/system already exists and just needs exposing as a survey result (cartography, habitability, life-signs, hazards — the category's biggest cheap-win pile), and **two ⏳ net-new, FRANCHISE-EARNING science loops the game lacks — the exploration mystery-box (anomalies) and xenoarchaeology (ancient tech)**. Surfacing those is the headline: they're the explore/discover half of the 4X.
- **Fire Control is the purest CONNECT door** — its dials (`Range`/`TrackingSpeed`/`FinalFireOnly`) are **built but dead** (unread by the resolver); the door's job is the wire (three existing fields → three resolver terms the salvo math already has), and that same wire generalizes to civilian precision-pointing.
- **EW is net-new** but rides the detection engine's ready surface (cloak ≈ a stronger EMCON, nearly free; jam/decoy = deltas on the live signal math; only the spoof "what's real" reading game defers to the intelligence layer).

**Build-list items that fall out:** (1) the **Fire-Control wire** — small, high-payoff; (2) the **survey-result exposure** cluster (cartography/habitability/life/hazard — cheap, the data's already in `SystemBodyInfoDB`/`Hazards`/first-contact); (3) two **net-new science systems** — the **anomaly/mystery-box** engine and the **xenoarchaeology/relic** system (both franchise-earning, both currently absent); (4) the **EW detection-delta layer** + its deferred **intelligence/information-ledger** partner for spoofing.

> **The §0f lens is now standing for every followup category** (Power, Defense, Enhancers, Industrial, Logistical, Civic, Command, Chassis): grade each door against *whatever system consumes it* — civilian, scientific, industrial, and infrastructural, on facilities as much as ships — never combat alone. Sensors is where it first changed a verdict (Survey thin → rich); expect it to change more.

---

## ⚙ 3 — SENSORS · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Sensors is the **"what you know" category**, and knowledge is a cross-pillar currency: the same EM/attenuation engine that gates combat also feeds navigation, survey/science, first-contact, and (post-merge) the espionage **Information Ledger**. The category's north-star reading: **Detection is the most-built door (nearly all ✅), Survey is one of the richest (mis-measured as thin), Fire Control is the purest CONNECT door (built-but-dead knobs), EW is net-new but rides the live detection math.** Four doors, all 🔒 locked. The detection engine is genuinely live and already decides combat — this dossier is mostly *how the existing wires run*, plus the few net-new deltas (EW deltas, the Information Ledger, the Fire-Control connect).

Verified against engine on 2026-07-09. Every file:line below was opened, not asserted.

---

### Pillar tags (§0h) — per door (note the multi-pillar Detection/EW)

Two metadata fields per door: `pillar` (Military / Espionage / Diplomacy / Influence) and `skeleton-role` (Projection / Counter / Medium / Detection). Sensors is the category that most obviously **spans pillars** — the same physical gear is graded against *different resolvers* depending on what it's pointed at.

| Door | pillar(s) | skeleton-role | Graded against (the resolver — §0d/§0h) |
|------|-----------|---------------|------------------------------------------|
| **3.1 Detection** | **Military·Detection AND Espionage·Detection** (multi-pillar) | **Detection** | Military: the combat fog gate (`FleetDetects`→`CanEngageTarget`). Espionage: the **Information Ledger** — a physical detection stamps a rival's Military facet at binary **Inferred** (ELINT flavor raises the band). Same contact, two consumers. |
| **3.2 Survey** | **Exploration** (economy·expansion·colonization·research·diplomacy consumers) | **Detection** (discovery-of-world, not of-enemy) | NOT the combat resolver. Graded against the *exploration pillar's* consumers: geo→economy, grav→expansion, atmo/life→colonization/first-contact, anomaly/xeno→the field-site loop (research). |
| **3.3 Fire Control** | **Military·Medium** (the director that *aims* a projector; civilian face = precision-pointing) | **Medium** (points the projector; grants no seat, holds no weapon of its own) | The combat resolver's `HitFraction` **tracking** term + the weapon-range gate. Civilian: aims mining lasers / tractors / docking / comms. |
| **3.4 Electronic Warfare** | **Military·Counter AND Espionage·Counter** (+ **Influence·Counter** for broadcast-jamming) | **Counter** (the counter to Detection) / partly **Detection** (ELINT-adjacent) | Military/Espionage: acts on what `FleetDetects` returns (jam/cloak/decoy push the detection deltas the *other* way). Influence: broadcast-jamming points at a belief-radius (graded against the legitimacy resolver, not combat). |

**Anti-dominance law (§0h):** every projector needs a named counter. Here **Detection IS the projector-to-be-countered, and EW IS the counter** — the two are each other's law. You cannot ship a stronger sensor (Detection) without EW being the insulation that beats it, and cloak/jam only matter because Detection gates first-strike.

---

### Complete dial → derived-stat → engine-wire table (VERIFIED)

Grades: ✅ Modelled (resolver reads an equivalent stat today) · ◐ Wire (adjacent system exists, needs a small hook) · ⏳ Defer (nothing reads it yet — name the prerequisite).

#### 3.0 Shared sensor dials (all four doors) + universal seven (§0a)
Every sensor also carries the universal seven (Mass, Size/mount, Materials/cost, Durability/HTK, Crew, Tech level, Signature contribution). On top:

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | Resolver/system insertion |
|------|------|--------------|-------|--------------------------|----------------------------|
| Shared | Waveform band (what it detects) | tuned peak ± bandwidth | ✅ | `SensorReceiverAtb.RecevingWaveformCapabilty` — `SensorReceiverAtb.cs:13` (`EMWaveForm`, built in ctor `:75`) | band-overlap scored in `SensorTools.DetectonQuality` `SensorTools.cs:51` |
| Shared | Sensitivity / reach | `DetectionRange_m` (reverse-solve) | ✅ | `SensorReceiverAtb.BestSensitivity_kW` `:19` (`WorstSensitivity_kW` `:25`) → `SensorTools.RangeForSignal` `SensorTools.cs:409` → `DetectionRange_m` `:425`; master knob `DetectionSensitivityScale = 1e6` `:365` | populates the per-faction contact track table (feeds the fog gate) |
| Shared | Refresh rate | scan cadence (s) | ✅ | `SensorReceiverAtb.ScanTime` `:35` → reschedule `SensorScan.cs:132` (`AddEntityInterupt`) | `SensorScan` IInstanceProcessor `SensorScan.cs:12` |
| Shared | Active ↔ passive posture | self-emission spike | ✅ | `SensorProfileDB.ActivityMultiplier` `SensorProfileDB.cs:91` (base `SignatureBaseMultiplier` `:102`); emitted signal scaled by it in `SensorTools.cs:308` | EMCON posture (below) |
| Shared | Signature contribution (a sensor that pings emits) | adds to own emitted spectra | ✅ | `SensorEmitter/SensorSignatureAtb.cs` writes `SensorProfileDB.EmittedEMSpectra`; active ping raises `ActivityMultiplier` | loops back into the same detection math — you find them AND announce yourself |

#### 3.1 DETECTION (Military·Detection + Espionage·Detection)
| Dial | Derived stat | Grade | Engine wire (file:line) | Resolver/system insertion |
|------|--------------|-------|--------------------------|----------------------------|
| Sensitivity / reach | detection range | ✅ | `SensorReceiverAtb.BestSensitivity_kW:19` → `RangeForSignal SensorTools.cs:409` / `DetectionRange_m:425` | contact table → fog gate |
| Waveform band | band-overlap detect | ✅ | `RecevingWaveformCapabilty:13` vs target `SensorProfileDB` spectra → `DetectonQuality SensorTools.cs:51` | — |
| Active ↔ passive (EMCON) | self-detection range ↑ when active | ✅ | `SensorProfileDB.ActivityMultiplier:91`; EMCON `FleetEmcon.cs:23-25` (Full 1.0 / Cruise 0.5 / Silent 0.15), `PostureMultiplier:29`; heat `EmconActivityProcessor.cs:52 HeatFactor` / `:57 ComputeActivityMultiplier` / writes `:92` | `EmconActivityProcessor` recomputes each tick |
| Refresh rate | track freshness | ✅ | `ScanTime:35` → `SensorScan.cs:132` | `SensorScan` |
| **Resolution / contact detail** | contact class/IFF detail | ◐ **wire → RE-HOMED** | `SensorReceiverAtb.Resolution (MP) :30` stored, **not driving contact detail**. Coarse read (blip vs. big) now falls out of `SignalStrength_kW × cross-section` automatically (no dial). Fine read (class/IFF) **moves out of the sensor** into the espionage Information Ledger. See ELINT flavor below. | Ledger, not sensor |
| **ELINT/SIGINT flavor** (essence-ext E1d) — Tactical / ELINT / Multi-role | Ledger-confidence band | ◐ **wire** | **Re-homes the SignalQuality-cut `Resolution` dial** (`SensorReceiverAtb.cs:30`): a Tactical contact stamps a rival's Military facet at binary **Inferred**; an ELINT take raises the band toward **Confirmed**. Rides the built detection→contact path `SensorScan.cs:120` → `FirstContact.OnDetection` `Factions/FirstContact.cs:40` | writes the **Information Ledger** (net-new — below) |
| → the combat resolver | fog + first-strike | ✅ | contacts → `FleetDetects CombatEngagement.cs:1312` → `CanEngageTarget:1327` (gated by `RequireDetectionToEngage:318`, checked `:177/:283/:475/:505/:584`) | THE detection × weapons seam |
| Sensors boost evasion (see the shot coming) | evasion term | ⏳ **defer** | flagged v2 hook in `ShipCombatValueDB` (Evasion currently leaves sensors/crew out) | needs the evasion term to read a sensor input first |
| Grave rung (shot-off receiver blinds you) | detection → 0 | ✅ | `SensorTools.SetInstances SensorTools.cs:208` empties the ability cache on receiver loss → scan loop iterates nothing → you go blind | ReCalcProcessor after damage; the `FirstStrike` gauge exploits this |

#### 3.2 SURVEY (Exploration pillar — combat is NOT its consumer)
| Dial / mode | Derived stat | Grade | Engine wire (file:line) | Resolver/system insertion |
|-------------|--------------|-------|--------------------------|----------------------------|
| **Geological** | reveal minerals/regions | ✅ | `GeoSurveys/GeoSurveyAtb.cs` → `GeoSurveys/GeoSurveyProcessor.cs` | economy / mining |
| **Gravitational** | reveal jump points | ✅ | `JumpPoints/GravSurveyAtb.cs` → `JumpPoints/JPSurveyProcessor.cs` | expansion / navigation |
| Survey speed | points/tick vs pool | ✅ | `Speed` (points/tick) vs `PointsRequired` | — |
| Survey range / reach | scan-from-distance | ◐ **wire** | the ~100 km grav gate is hardcoded — `JPSurveyProcessor.cs:47` (`if (distance < 100000) // FIXME: needs to be an attribute`) — expose as a dial | — |
| Stellar / cartography | long-range system reveal | ◐ **wire** | galaxy is generated + a fog/reveal exists; extend to see a system's bodies before you visit | expansion |
| Atmospheric / habitability | gate colonization | ◐ **wire** | `SystemBodyInfoDB` holds atmosphere/gravity/temperature + `CanStartHere` — expose as a survey RESULT | colonization |
| Biosphere / life-signs | "is it inhabited?" | ◐ **wire** | colony/population data exists → expose feeding first-contact (`FirstContact.cs:40`; `DiplomacyFirstContactTests`) | diplomacy / first-contact |
| Hazard / navigational | reveal/forecast hazards | ◐ **wire** | `Hazards/` exists (gas clouds, solar flares) | navigation / safety |
| **Counter-interference** (essence-ext E1b) — how much hazard the scanner sees through | shrink the sensor-cut | ◐ **wire** | `Hazards/HazardResistanceAtb.cs:20` already exists (stacks/scales by health) + `SpaceHazardTools.ApplyResistance` `SpaceHazardTools.cs:~115`; let a Survey component carry resistance so the sensor-cut shrinks | navigation/exploration (treasure hides in hazards) |
| Survey depth / resolution | detail per pass | ◐ **wire** | `Resolution` stored (like Detection's) | drive detail level of a reveal |
| Data processing (facility) | raw scans → knowledge | ◐ **wire** | a survey-data facility (Structure chassis) | research/Civic |
| Autonomy (probe) | one-shot deep scan | ◐ **wire** | survey component on a minimal chassis | — |
| **Anomaly / mystery-box** | research + event chain | ⏳ **defer — NET-NEW, now DESIGNED** | one catalog entry on the field-site loop (`docs/EXPLORATION-CONTENT-DESIGN.md`); no anomaly system in engine yet | research / late-game-crisis seed |
| **Xenoarchaeology** | tech windfall / unique blueprint | ⏳ **defer — NET-NEW, now DESIGNED** | "Ancient ruins" catalog entry on the same loop; **first build step = fix the `SystemBodyFactory.GenerateRuins` tautology bug (`SystemBodyFactory.cs:~1418`, always early-returns → ruins never generate)** | research |

#### 3.3 FIRE CONTROL (Military·Medium — the purest CONNECT door: dials built, dead)
| Dial | Derived stat | Grade | Engine wire (file:line) | Resolver/system insertion |
|------|--------------|-------|--------------------------|----------------------------|
| Tracking speed | beat evasion | ◐ **wire (DEAD KNOB)** | `BeamFireControlAtbDB.TrackingSpeed` **exists `:21`, VERIFIED unread** (0 usages in `Combat/` or `BeamWeapons/`) → wire into the `HitFraction` **Tracking** term | `HitFraction` in `Combat/CombatKernel.cs` / `CombatEngagement.cs` |
| Fire-control range | open fire sooner | ◐ **wire (DEAD KNOB)** | `BeamFireControlAtbDB.Range` **exists `:15`, unread** → wire into the weapon-range engagement gate (director-gated reach) | the range ladder / `InRange` gate |
| Fire allocation / multi-target | split fire across N | ◐ **wire** | `FinalFireOnly` **exists `:27`, unread** + a new targets-tracked dial → make N a director property (`StepEngagementGroup` already divides fire 1/N) | multi-party fire-split |
| PD-only mode | intercept ordnance | ◐ **wire** | `FinalFireOnly:27` → the point-defense role | pairs with weapons-door PD wire + missiles-as-targets |

*Honest state: the whole door is ◐ Wire — the component (`BeamFireControlAtbDB.cs:9`) and its three dials are built; the resolver reads the **weapon's** `BaseHitChance`/`MaxRange` instead. Building the door IS building the wire (Failure-A: the number exists, unwired).*

#### 3.4 ELECTRONIC WARFARE (Military·Counter + Espionage·Counter + Influence·Counter)
| Dial | Derived stat | Grade | Engine wire (file:line) | Resolver/system insertion |
|------|--------------|-------|--------------------------|----------------------------|
| **Jamming** (degrade enemy detection) | shrink enemy `DetectionRange_m` | ◐ **wire** | add a jammer term to the scan: shrink `DetectionRange_m SensorTools.cs:425` / raise the `SignalStrength_kW`-vs-sensitivity threshold. **Works on the strength substrate** (`SignalStrength_kW SensorReturnValues.cs:7`) — NOT the design-cut `SignalQuality` | acts on `FleetDetects` → the fog gate |
| **Cloak / signature damping** | push own signature down | ◐ **wire (nearly free)** | push `ActivityMultiplier SensorProfileDB.cs:91` / `SignatureBaseMultiplier:102` down — **EMCON already does this** (`FleetEmcon.cs:25 Silent 0.15`); a cloak is a stronger, component-based EMCON | same emitted-signal math (`SensorTools.cs:308`) |
| **Decoy / spoof** (false contacts) | inject a false `SensorContact` | ◐ **wire (injector)** + ⏳ **defer (reading game)** | the track table + `SensorContact` exist (`Sensors/SensorContacts/SensorContact.cs`); injector half is doable. The "decide what's real" payoff waits on the **intelligence/Information-Ledger layer** (now the Spymaster-MVP / disinformation tail) | enemy track table |
| **Counter-EW / ECCM** | resist their EW | ⏳ **defer** | the mirror of jamming — needs the **jam mechanic built first**, then a resist term | — |
| **Broadcast Jamming** (essence-ext E1c) — active counter-influence | reduce incoming belief-pressure | ◐ **wire** | EW jamming (shrink `DetectionRange_m`) pointed at an emitter's belief-radius → a reduction on summed incoming pressure in the legitimacy processor (`LegitimacyProcessor.cs:~47`); costs your own morale | **legitimacy resolver, NOT combat** (Influence pillar) |
| **Counter-intel / cipher cluster** (essence-ext E1d) — how hard your OWN faction is to read | defensive term on covert-op detection + enemy Ledger-gain vs your facets | ◐ **wire** / ⏳ **defer** | one code path, both directions (the always-on mirror); a defensive term on the net-new covert-op detection roll. Mounts on a Structure = a colony's counter-intel directorate | the **Information Ledger** (enemy's, about you) |
| → the combat resolver | via detection | ✅ | all of the above act on what `FleetDetects CombatEngagement.cs:1312` returns → `CanEngageTarget:1327` already honours a blinded fleet | **EW × Detection × Weapons stacks TODAY** the moment the detection deltas are wired |

---

### Resolver/system insertion points (the fog gate + the Information Ledger)

**A. The combat detection fog gate (BUILT, live).** This is the detection × weapons seam that makes EW and Detection matter in combat:
- Contacts land in the per-faction track table via `SensorScan` (`SensorScan.cs:12`).
- `FleetDetects(detector, target)` (`CombatEngagement.cs:1312`) asks: does the detector's faction hold a contact for any ship in the target fleet?
- `CanEngageTarget(attacker, target)` (`CombatEngagement.cs:1327`) = `!RequireDetectionToEngage || FleetDetects(…)` — **the engine of first-strike**: see first → shoot first; a blind fleet takes fire without returning it.
- Gated by the flag `RequireDetectionToEngage` (`CombatEngagement.cs:318`, default **false** so combat fixtures stay deterministic); consumed at `:177`, `:283`, `:475`, `:505`, `:584`.
- **Everything EW does is push what `FleetDetects` returns** — jam shrinks the detector's reach, cloak shrinks the target's emission, decoy adds a false contact. No new resolver work: EW rides the same gate Detection does.

**B. The Information Ledger — the ONE net-new engine object this category points into.** Not built. It is a **per-rival, per-facet intel table** (facets: disposition / military / economy / internal-politics / secrets), each at one of three bands:
- **Inferred** (default) — you see only *behavior* + a fuzzy estimate (poker default).
- **Confirmed** — you've raised intel: the estimate sharpens / the truth reveals.
- **Stale** — intel decays back toward Inferred; you must refresh (can't learn a rival once and know them forever).

It is **fog-of-war for politics**, riding the same detection substrate but keeping the graduated richness in ONE place (the Ledger), not scattered in a sensor field. **Where ELINT writes it:** a physical detection on a rival (the built path `SensorScan.cs:120` → `FirstContact.OnDetection` `Factions/FirstContact.cs:40`) stamps that rival's **Military facet at binary Inferred**. The re-homed `Resolution`/ELINT flavor (`SensorReceiverAtb.cs:30`) is what *raises the band* toward Confirmed — a Tactical contact = Inferred, an ELINT take = a push toward Confirmed. Decay and agents produce the rest of the gradient. The counter-intel/cipher cluster (EW) is the defensive term on the same table (how fast an *enemy* raises confidence about *your* facets — the mirror).

---

### Prerequisite fixes & dead stubs (file:line)

| Item | Status | file:line |
|------|--------|-----------|
| **`SignalQuality` — DESIGN-CUT** (a decision to stop using it, NOT a code removal) | field still in code, cut from use | `SensorReturnValues.cs:8` (declared); computed in `SensorTools.DetectonQuality` `:194-195`; the survey body-ID path was its only consumer. Detection now collapses to **strength only** (`SignalStrength_kW SensorReturnValues.cs:7`). Cutting it retired the old "detection-quality fix" prerequisite (now free) and deletes 3 bugs by deletion (byte-overflow at `SensorTools.cs:~185`, multi-band overwrite, range-invariance). |
| **`Resolution` dial — orphaned by the cut** | stored, unread; RE-HOME target for ELINT | `SensorReceiverAtb.cs:30` — no longer a detection dial; its real home is Ledger-confidence (ELINT) + the Survey survey-points mechanic |
| **Fire-control dials — DEAD KNOBS** | built, VERIFIED zero reads | `BeamFireControlAtbDB.cs` — `Range:15`, `TrackingSpeed:21`, `FinalFireOnly:27` (grepped `Combat/`+`BeamWeapons/`: no `.TrackingSpeed`/`.FinalFireOnly` reads). The door's whole job is to wire these. |
| **The Information Ledger** | **UNBUILT** (net-new engine object) | no file yet — every ELINT/counter-intel/spoof wire points into it; it is the Spymaster-MVP's core deliverable |
| **Grav-survey ~100 km gate** | hardcoded FIXME | `JPSurveyProcessor.cs:47` (`if (distance < 100000) // FIXME`) — expose as a dial |
| **`GenerateRuins` tautology bug** | dead stub (blocks xenoarchaeology) | `SystemBodyFactory.cs:~1418` — always early-returns → ruins never generate; the first build step of the field-site loop |
| **EW jam/spoof/cloak-as-component** | **NET-NEW** (no EW component exists) — but every effect has a named detection hook | insertion surface = `DetectionRange_m:425` / `ActivityMultiplier:91` / the contact table |

---

### Design essence captured inline

**The strength-only detection collapse (decision 2026-07-07).** Detection used to carry two outputs per contact: `SignalStrength_kW` (how loud, range-dependent) and `SignalQuality` (a 0–1 "how well resolved / what class" score). `SignalQuality` was **cut** — it was pretty-and-complicated, carried no graduated *range* information (it was range-invariant), and its only consumer was a redundant survey body-ID path. Detection now answers exactly two questions: **do I see it, and how loud is it** — which is all combat and EMCON need. "Big vs. small contact" still falls out automatically from `SignalStrength_kW × cross-section` (no dial). **The Information Ledger** is where the graduated-ness went: fine classification (a rival's class / IFF / intent) is an *espionage* question, not a sensor field — a per-rival, per-facet fog-of-war table (Inferred → Confirmed → decays to Stale) fed *binary* by passive detection (physical contact → Military facet at Inferred) and *raised* by ELINT/agents. This is cleaner in one stroke: the graduated-ness lives in one place, the SignalQuality prerequisite becomes "cut a field" (free), and three latent bugs vanish by deletion. The category's cross-pillar nature is the payoff — the same contact that gates a gunfight (Military) also seeds the poker-of-intent (Espionage), through the identical `FirstContact.OnDetection` path.

---

### §0g stamp — three acceptance criteria

- **Reachable** (cradle-to-grave, player-side): ✅ **closed for Detection/Fire-Control/EW today.** A sensor is a **component** — mineral → material → built at a colony → designed (waveform/sensitivity/scan dials) → research-gated → installed on any chassis (ship, station, colony observatory) → the in-play decision (EMCON dark-vs-loud; where to point the array) → **the grave rung is already wired**: a shot-off receiver empties the ability cache (`SensorTools.SetInstances:208`) and you go blind. EW/Fire-Control ride the same chain (net-new EW tech; Fire-Control component already exists). Survey's two net-new loops (anomaly/xeno) research-gate their own capability and reward new tech.
- **Mirrored** (opponent-side — the always-on spy mirror): ✅ by construction. Because detection is a **component on a shared door**, the NPC mounting a sensor/jammer/ELINT suite is the *same act* as the player mounting one — no player-only code. The mirror is literal: **NPCs ELINT and jam you** through the identical scan/contact path, and the counter-intel/cipher cluster is *one code path, both directions* (their confidence-gain vs your facets = your confidence-gain vs theirs). EMCON runs on every faction's fleets; `FirstContact.OnDetection` records a **mutual** relationship row on both `DiplomacyDB`s. The Information Ledger is inherently two-sided.
- **Observable** (the gauge, both sides): ✅ the **contact table** is the detection gauge (contacts drawn as contacts = fog made visible; the `FirstStrike_SeerWipesBlindEnemy_Unscathed` gauge reads it); the **intel-confidence readout** (Inferred/Confirmed/Stale per facet) is the Ledger's gauge, shown to the *reader* of a rival; the EMCON self-detection readout (`SelfDetectionRange_m SensorTools.cs:484` — "how far am I seen") shows the target's side of the loud/quiet bet. Fire-Control's gauge is the effective hit-fraction once its dials are wired.

---

### Cross-category shared state (Prime Directive)

| Seam | What crosses | Where it wires |
|------|--------------|----------------|
| **Detection × Weapons** (the load-bearing one) | contacts decide who can shoot — first-strike | `FleetDetects`/`CanEngageTarget` `CombatEngagement.cs:1312/1327`; `SalvoDamageScale:94` is the pace dial the fog rides on |
| **Detection × Damage** (the grave rung) | a shot-off receiver → you go blind | `SensorTools.SetInstances:208` empties the cache on receiver loss |
| **Detection × EMCON × Propulsion/Weapons** (heat) | thrusting/firing spikes your signature (can't burn or shoot quietly) | `EmconActivityProcessor.cs:52 HeatFactor`/`:57`/`:92`; reads `NewtonMoveDB` (thrust) + `GenericFiringWeaponsDB.ShotsFiredThisTick` (firing) |
| **Fire Control × Weapons** | the director feeds the `HitFraction` tracking term + the range gate (instead of the gun hard-coding them) | `BeamFireControlAtbDB.cs:15/21/27` → `HitFraction` in `CombatKernel.cs`/`CombatEngagement.cs` |
| **EW × Influence** (broadcast-jamming) | jamming pointed at a belief-radius reduces incoming legitimacy pressure | `LegitimacyProcessor.cs:~47` — graded against the **legitimacy** resolver, not combat (§0f/§0h) |
| **ELINT × the political layer** (Espionage pillar) | passive detection → binary Inferred on a rival's Military facet; ELINT raises the band | `SensorScan.cs:120` → `FirstContact.OnDetection Factions/FirstContact.cs:40` → the **Information Ledger** (net-new) |
| **Survey × economy/expansion/colonization/diplomacy** | each survey mode feeds a different consumer | geo→`GeoSurveyProcessor`; grav→`JPSurveyProcessor`; atmo/life→`SystemBodyInfoDB`+first-contact; hazard→`Hazards/` |
| **Survey × Hazards** (counter-interference) | a hazard-resistant scanner sees through gas/radiation | `HazardResistanceAtb.cs:20` + `SpaceHazardTools.ApplyResistance` |

---

### Holes owned/resolved → status + home

| Hole (`COMPONENT-DESIGNER-CATEGORIES.md §5`) | Status | Home |
|----------------------------------------------|--------|------|
| **H9 — Conversion / assimilation weapons** (turn an enemy into you; ties to the espionage system) | **partially routed through this category** | Homed at §1.5 Weapons▸Exotic via the projector tag `Selectivity = convert`, graded against the **legitimacy resolver** — but its *espionage* face (subversion/turn-an-agent) is fed by the **Information Ledger** and EW's counter-intel cluster this category stands up. LOW-MEDIUM priority. |
| **H8 — Network / relay infrastructure** (addressable, many-to-one) | **adjacent, not owned here** | The C3 Relay (essence-ext E1a) is a `SensorProfileDB` emitter → **found, jammed (§3.4 EW), destroyed**. EW is the sever mechanism; the door itself is NEW DOOR §10.2 Command▸Relay. MEDIUM. |
| **Detection-quality fix** (was a listed espionage prerequisite) | **RESOLVED by deletion** | `SignalQuality` cut (2026-07-07) — the prerequisite became "cut a field" (free); the gradient moved to the Information Ledger. |
| **The Information Ledger** | **OPEN — net-new, unbuilt** | The Spymaster-MVP's core deliverable; every ELINT/counter-intel/spoof wire in this category points into it. |
| Anomaly mystery-box + Xenoarchaeology (Survey §3.2) | **OPEN — designed, unbuilt** | Collapsed to the one field-site loop (`docs/EXPLORATION-CONTENT-DESIGN.md`); first build step = fix `SystemBodyFactory.GenerateRuins` (`SystemBodyFactory.cs:~1418`). |
| EW jam/spoof/cloak/ECCM | **OPEN — net-new; cloak nearly free** | Cloak ≈ stronger EMCON (`FleetEmcon.cs`); jam/decoy = deltas on the live detection math; spoof's "what's real" reading defers to the Ledger. |

---

## §4 — Power

Power is the **substrate every other component draws on** — and the category where §0b ("the numbers force the build, never a rulebook") stops being a slogan and becomes literal machinery. A beam with no reactor reads **output: 0**; a ship with a flat battery **can't warp**. Nothing else in the designer is this universal: weapons, warp, sensors, shields, industry, and a colony's whole population all bottom out in watts and stored joules. So this is the **multi-consumer rule (§0f) at maximum** — Power has no "combat vs civilian" split because *everything* is its consumer.

**The category yardstick — the SUPPLY GATE, not the salvo math.** Power doesn't write a `WeaponProfile` field; it decides whether the other doors' dials get to *function*. Two currencies, and the split is the whole category:
- **Sustained generation** (`EnergyGenAbilityDB.TotalOutputMax`, kW) — the steady baseload a reactor/solar makes, capped at load 1.0. Feeds the **ground supply gate** (`ReactorSupply_W`), **colony life-support** (`SustenanceProcessor`), and (via surplus) charges the battery.
- **Stored energy** (`EnergyGenAbilityDB.EnergyStored`, KJ) — the bank a battery/capacitor holds, for **spikes the reactor can't deliver instantly**: a warp jump (`BubbleCreationCost`) or a weapon alpha (`Energy/1000`). Every stored-energy consumer *hard-gates* on it (won't warp / won't fire when it's too low).

**Honest state of the forcing (a flagged wire).** §0b's ideal is a **soft throttle** — under-fed, a beam auto-throttles to available power. The engine today does a **hard block** instead: `WarpMoveProcessor.cs:235` won't *start* a jump under-charged, `GenericFiringWeaponsProcessor.cs:69` *skips* the shot. Same design intent (the numbers steer you to add a generator), blunter mechanism. Making under-supply *throttle* rather than *block* is a named wire, not a redesign.

**The consumer map (per §0f — this IS the category):**
| Consumer | Reads | Gate today | Status |
|----------|-------|-----------|--------|
| **Weapons** (beam fire) | `EnergyStored` (spike) | hard block — no power, no shot (`GenericFiringWeaponsProcessor:69`) | ✅ |
| **Warp** (jump) | `EnergyStored` (spike) | hard block — won't start under-charged (`WarpMoveProcessor:235`) | ✅ |
| **Ground units** (supply gate) | `ReactorSupply_W` vs `EnergyDemand_W` | hard block — design Invalid if demand > supply (`GroundUnitAssembly:137`) | ✅ |
| **Colony life-support** | `TotalOutputMax` (sustained) | `PowerShortage` → morale/starvation | ◐ inert (`PerCapitaPowerDemand` defaults 0) |
| **Sensors** (active ping) | — | none — active-ping cost is EMCON, not a power draw | ◐ wire |
| **Shields** (regen) | — | none — shield pool isn't fed from power | ◐ wire |

### §4.0 Shared power dials (both doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **Energy type** | `EnergyTypeID` — which energy good (electricity…); a consumer only draws its own type |
| **Output / capacity** | the headline number — kW made (Generation) or KJ banked (Storage) |
| **Mass** (the cascade) | output/capacity scale mass → chassis budget; **this is the Death-Star forcing** (a planet-cracker's generator won't fit under Mega) |
| **Signature / heat** | a running plant emits — feeds EMCON/Detection (a quiet ship runs cold or dark) |
| **Safety / containment** | stored energy is a bomb if the component is hit — the grave rung |

### 4.1 Power ▸ GENERATION  🔒 *locked*
*The powerplant — reactors, solar, RTGs, the exotic cores. What turns fuel (or sunlight, or decay heat) into the watts every other component spends. Everything from a chemical genset to a fusion torch-reactor to a solar wing to an antimatter core to a Death-Star generator falls out of these dials.*

**The core decision — WHAT TO BURN, and HOW MUCH: output vs fuel-dependence vs safety/signature.** A fuel-burning reactor makes big steady power but must be **fed** (mine → refine → burn, and it runs out) and it's a **hazard** (hot signature, a bomb if breached). A fuel-free source (solar, RTG) needs no logistics but is **constrained** — solar dies far from a star or in shadow; an RTG is reliable but weak. You pick where on the *output ↔ independence ↔ safety* triangle you sit; nothing gives all three.

**A. Source — what powers it (the core dial, like a weapon's Nature)**
| Option | Why pick it | The catch (anti-dominance) |
|--------|-------------|----------------------------|
| **Combustion / chemical** | cheap, low-tech, instant | **thirsty** + low output — a stopgap genset |
| **Fission reactor** | high steady output, mature tech | needs **refined fuel** (logistics, runs out) + heat/signature + breach hazard |
| **Fusion** | **higher output**, cleaner burn, less fuel per watt | higher tech; hot, heavy |
| **RTG** | **fuel-free**, tiny, reliable, huge lifetime — the deep-space/unmanned choice | **low output** — powers a probe, not a warship |
| **Solar** | **free** power, no fuel, no heat signature | **falls off with distance from the star**, zero in shadow/deep space; big fragile area |
| **Antimatter / exotic** | **enormous** output in little mass — the capital/Death-Star tier | extreme fuel cost + tech; **catastrophic if hit** |
| **Zero-point / exotic** | the free-energy endgame — output with no fuel | ⏳ deep-tech; a deferred net-new source |

**B. Output level — the raw scale (`PowerOutputMax`, kW)**
| Option | Why | Catch |
|--------|-----|-------|
| **Low** | light, cheap, sips fuel | powers little — under-feeds a hungry weapon/warp loadout (hard block today) |
| **High** | feeds a heavy weapons/warp/shield loadout | **heavy + thirsty** — the mass funnels the chassis (the §0b forcing) |

**C. Fuel & lifetime — what it eats and for how long (`FuelType` / `FuelUsedAtMax` / `Lifetime`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Thirsty / high-burn** | max output now | drains `LocalFuel` fast → frequent resupply (Logistical) |
| **Frugal / long-life** | runs for ages between refuels (the RTG/deep-mission trait) | lower output per unit mass |

**D. Response — baseload vs the battery's job (`CalcLoad`, reactor capped at load 1.0)**
| Option | Why | Catch |
|--------|-----|-------|
| **Baseload reactor** | steady, efficient at a constant load | **can't surge** — a spike (warp jump, weapon alpha) beyond `TotalOutputMax` is met by the **battery**, not by over-driving the reactor (the Generation↔Storage division of labour) |
| **Load-following** | ramps with demand | more complex/costly; still capped at load 1.0 |

**E. Signature / heat** *(the stealth tradeoff — the Detection tie-in)*
| Option | Why | Catch |
|--------|-----|-------|
| **Hot / unbaffled** | max output/mass | a running reactor **emits** — seen from far (a loud plant lights up a "dark" ship) |
| **Baffled / cold-running** | ambush-friendly, low emission | lower output; solar/RTG are naturally cold |

**F. Safety / containment** *(the grave rung)*
| Option | Why | Catch |
|--------|-----|-------|
| **Unshielded** | light, cheap, max output | a breach is a **catastrophe** — a hit reactor is stored energy going off inside your hull |
| **Contained / SCRAM** | survives damage, fails safe | heavy; caps peak output |

**Modellability audit (§0d — graded against the SUPPLY GATE, its real consumer):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Source — fission/fusion (fuel-burning) | ✅ | `EnergyGenerationAtb` → `EnergyGenAbilityDB.MaxOutputFromReactor` → `TotalOutputMax` |
| Source — solar | ✅ | `EnergySolarGenerationAtb` → `EnergyGenHotloopProcessor.ComputeSolarMax` (sums panels, **attenuated by distance from each star** via `SensorTools.AttenuatedForDistanceList`) → `MaxOutputFromSolar` |
| Source — RTG | ◐ **wire** | expressible as a low-`PowerOutputMax`, huge-`Lifetime` `EnergyGenerationAtb` today; a first-class "fuel-free reliable" framing is the dial |
| Source — antimatter / zero-point | ⏳ **defer** | high-output is expressible; the **catastrophe-on-breach** + exotic-fuel identity needs the safety/meltdown mechanic (F) |
| Output level | ✅ | `PowerOutputMax` (kW) → the reactor total → the supply gate |
| Fuel & lifetime | ✅ | `FuelType` / `FuelUsedAtMax` (kg/s) / `Lifetime` → `LocalFuel` drain (`LocalFuel -= fuelUseAtMax × load × Δt`) |
| Response / load | ✅ | `CalcLoad(demand, max)` clamps 0..1; over-demand met by battery discharge, reactor never over-driven |
| Signature / heat | ◐ **wire** | reactor `Load` → EMCON is **unblocked-but-deferred** (`EmconActivityProcessor.cs:32-33` notes it) |
| Safety / containment (breach) | ⏳ **defer** | no meltdown mechanic — a **reactor-breach-damages-ship** rule (ties to the damage system, the grave rung) |

> **Dead-legacy flag (gotcha):** `SensorReceiverAtb.IsEnergyGen` (a bool on the *sensor* attribute) looks like the solar path but is **vestigial** — its `SensorScan` branch only sets `LocalFuel`, it adds **nothing** to power output. The *working* solar is `EnergySolarGenerationAtb`. Don't build on `IsEnergyGen`; treat it as dead (a Landmine-Index L1 "dead code that looks live").

**Reading:** Generation is **mostly Modelled** — the fuel-reactor and the (distance-attenuated) solar paths both run live and already feed the supply gate + colony life-support. The gradient: fission/fusion/solar/output/fuel/response all ✅; RTG is a ◐ framing wire (a long-life low-output reactor exists, first-class it); signature is a ◐ wire onto the EMCON hook already flagged in-engine; and the **exotic sources + the meltdown grave-rung** ⏳ defer on the one net-new mechanic this door wants — a **containment/breach** rule (which is also what makes antimatter *dangerous*, not just powerful).

**Numbers (calibrated to the live power scale):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **Output** | kW | genset (low) → antimatter core (10⁶⁺) | `PowerOutputMax` → `TotalOutputMax` |
| **Fuel burn** | kg/s | frugal → thirsty | `FuelUsedAtMax` → `LocalFuel` drain |
| **Lifetime** | s | short → RTG-huge | seeds `LocalFuel = burn × Lifetime` |
| **Solar area** | m² | small wing → vast array | `Area_m2` × distance-attenuated star flux |
| **Mass** (emergent) | kg | scales output × tech | chassis budget (§0b) |

**Sanity-check (the forcing, in real units):** a beam shot costs `Energy/1000` KJ; a warp jump costs ~**1,000,000 KJ** (alcubierre-2k). A reactor making **1,000 kW** refills that jump's worth of battery in ~1,000 s of surplus — so a warship that wants to jump *and* alpha-strike needs either a big reactor (heavy) or a big battery (Storage's job). The trade between "more reactor" and "more battery" is the two Power doors talking to each other, in the same KJ currency the consumers spend.

**Preset coordinates — the span (ship + facility, all consumers):**
| Plant | Source | Output | The trade it chose |
|-------|--------|--------|--------------------|
| **Chemical genset** | combustion | low | cheap stopgap; thirsty, weak |
| **Fission reactor** | fission | high | the workhorse; needs fuel + containment |
| **Fusion core** | fusion | very high | capital-grade; hot, high-tech |
| **RTG** | decay | very low | fuel-free, reliable — the probe/beacon plant |
| **Solar wing** | solar | var. w/ distance | free near a star; dead in the deep dark |
| **Antimatter core** | antimatter | enormous | dreadnought/Death-Star tier; a bomb if hit |
| **Geothermal plant** *(FACILITY)* | planetary heat | steady | a colony's baseload — location-locked, no fuel ship |

### 4.2 Power ▸ STORAGE  🔒 *locked*
*The battery and the capacitor — the bank that lets you spend energy faster than the reactor makes it. A reactor is capped at a steady load; storage is how a warship dumps a **warp jump** or a **weapon alpha** in one instant, then recharges from the reactor's surplus. Everything from a small buffer cell to a heavy endurance bank to a fast alpha-strike capacitor to a colony's grid storage falls out of these dials.*

**The core decision — BUFFER THE SPIKES: capacity vs burst vs mass.** The reactor makes power *steadily* (load ≤ 1.0); the fight and the jump need it *all at once*. Storage banks the reactor's surplus so you can exceed `TotalOutputMax` for a moment — the warp bubble, the charge-weapon alpha. You choose **how much** you bank (capacity → how many jumps/alphas before you're dry) and **how fast you can dump it** (a capacitor's burst vs a battery's endurance). Big capacity is heavy; fast burst leaks and costs.

**A. Capacity — how much you bank (`MaxStore`, KJ)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Small buffer** | light, cheap — smooths normal draw | can't afford a spike — **won't warp / can't alpha** (the hard gate) |
| **Large bank** | banks a **jump + an alpha** with reserve | **heavy** — the mass funnels the chassis, same as a reactor |

**B. Burst / discharge type — how fast you can dump it**
| Option | Why | Catch |
|--------|-----|-------|
| **Battery (endurance)** | holds a big charge a long time, steady draw | **slow dump** — can't feed a huge one-instant alpha |
| **Capacitor (burst)** | **dumps fast** → the charge-weapon / first-strike alpha enabler | **leaks** (self-discharge), lower capacity per mass, ⛓ pairs with a charge weapon |

**C. Charge rate — how fast it refills from the reactor**
| Option | Why | Catch |
|--------|-----|-------|
| **Slow charge** | cheap, light | one big shot then a long wait — bad cadence |
| **Fast charge** | recovers **between volleys** — sustained alpha cadence | costlier; stresses the reactor (draws its surplus hard) |

**D. Safety** *(the grave rung)*
| Option | Why | Catch |
|--------|-----|-------|
| **Standard cell** | light, cheap | a fully-charged bank is **stored energy** — a bomb if the component is hit |
| **Hardened / fused** | fails safe on damage | heavier, lower peak store |

**Modellability audit (§0d — graded against the warp/weapon spike gates):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Capacity** | ✅ | `EnergyStoreAtb.MaxStore` (KJ) → `EnergyGenAbilityDB.EnergyStoreMax` → the warp gate (`WarpMoveProcessor:235`) + the beam gate (`GenericFiringWeaponsProcessor:69`). **This is what makes a ship warp/fire** — `ChargeReactors` tops it to full |
| Charge behaviour (surplus → battery) | ✅ | `EnergyGenProcessor.EnergyGen` charges the store from generation surplus, bounded by free capacity; discharges on shortfall — the passive balancer |
| **Burst / discharge rate** | ◐ **wire** | **no discharge-rate field exists** — rate is implicit today; add a `MaxDischarge_KJps` to make the **capacitor-vs-battery** (alpha vs endurance) distinction real |
| **Charge rate** | ◐ **wire** | implicit in `EnergyGenProcessor` (bounded by free capacity + surplus); expose a per-cell charge-rate dial for cadence |
| Safety / breach | ⏳ **defer** | same containment/breach mechanic Generation ▸ F wants — a charged bank that goes off when hit |

**Reading:** Storage's **core is fully Modelled** — capacity is the single most load-bearing number in the ship (a flat battery is the literal "spawned ship won't move / won't fire" bug), and the charge-from-surplus balancer runs live. The depth dials — **discharge rate** (the capacitor's fast-dump, which is what an alpha-strike/charge-weapon *needs*) and **charge rate** (volley cadence) — are ◐ **wire**: the capacity field exists, the *rate* fields don't yet, so today a battery and a capacitor differ only in size, not in how fast they dump. Adding one `MaxDischarge_KJps` field turns "storage is just capacity" into the real battery-vs-capacitor decision. Safety ⏳ shares Generation's containment mechanic.

**Numbers (calibrated to the stored-energy scale):**
| Dial | Unit | Range | Pins to |
|------|------|-------|---------|
| **Capacity** | KJ | small cell → heavy bank | `MaxStore` — reference: battery-2t = **1,000,000 KJ** = one alcubierre-2k jump |
| **Discharge rate** (new) | KJ/s | battery (slow) → capacitor (huge) | ◐ the alpha-strike gate (how big a one-instant dump) |
| **Charge rate** (new) | KJ/s | slow → fast | ◐ volley cadence |
| **Mass** (emergent) | kg | scales capacity × tech | chassis budget |

**Sanity-check:** one **battery-2t** (1,000,000 KJ) buffers exactly **one warp jump** (alcubierre-2k = 1,000,000 KJ) *or* a big beam alpha (a shot = `Energy/1000` KJ, so hundreds of KJ each → thousands of shots' worth, or one charge-weapon dump). So "how many jumps/alphas before I'm dry" is literally `MaxStore ÷ cost` — the player reads it straight off the two numbers. Want to jump *and* fight? Carry more banks (mass) or a bigger reactor to recharge between (Generation). The two doors trade in one currency.

**Preset coordinates — the span:**
| Cell | Type | Capacity | The role it fills |
|------|------|----------|-------------------|
| **Buffer cell** | battery | small | smooths normal draw on a utility hull |
| **Endurance bank** | battery | large | a long-range ship's reserve — many jumps |
| **Alpha capacitor** | capacitor | mid, fast-dump | the charge-weapon / first-strike buffer (◐ needs the rate field) |
| **Warp capacitor** | capacitor | ~1 jump | banks exactly one bubble for a jump-raider |
| **Grid storage** *(FACILITY)* | battery | huge | a colony's load-leveller — banks solar/reactor surplus for peak demand |

---

## ✅ §4 Power — COMPLETE (2/2 doors locked)
Generation 🔒 · Storage 🔒. **Yardstick = the SUPPLY GATE**, not the salvo math — Power decides whether every *other* door's dials get to function (a beam with no reactor = output 0; a flat battery = no warp). This is the **multi-consumer rule (§0f) at maximum**: Power has no combat/civilian split because everything is its consumer (weapons · warp · ground units ✅ today; colony life-support ◐ inert; sensors + shields ◐ not-yet-wired). Headline readings: **Generation is mostly Modelled** — fuel reactors + distance-attenuated solar run live and feed the gate; RTG is a framing wire, exotic sources + the meltdown grave-rung defer on one net-new **containment/breach** mechanic. **Storage's core is fully Modelled** (capacity = the literal warp/fire gate — the most load-bearing number on a ship), with the **battery-vs-capacitor** depth waiting on one ◐ wire (a `MaxDischarge_KJps` rate field — today all storage differs only by size). **The whole category is where §0b LIVES** — "the numbers force the build" is real machinery here, with one honest gap: under-supply currently **hard-blocks** (won't warp / won't fire) where §0b wants a **soft throttle** — a flagged wire, not a redesign. Build-list items: (1) the `MaxDischarge_KJps` storage-rate field (unlocks capacitor-vs-battery); (2) the **containment/breach** grave-rung (unlocks exotic sources + makes a hit reactor/bank matter); (3) the **soft-throttle** conversion (under-supply throttles, not blocks); (4) wire **sensors + shields** as power consumers (the §0f expansion); (5) calibrate **colony `PerCapitaPowerDemand`** (turn life-support from inert to a real load).

---

## ⚙ 4 — POWER · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Power is the **supply substrate**: it does not write a `WeaponProfile` field or land on the salvo math. It decides whether every *other* door's dials get to **function** — a beam with no reactor reads output 0; a ship with a flat battery can't warp. Two doors: **Generation** (reactors/solar/RTG — makes watts) and **Storage** (batteries/capacitors — banks joules for spikes). Two currencies, and the split IS the category:
- **Sustained generation** — `EnergyGenAbilityDB.TotalOutputMax` (kW), capped at load 1.0. The steady baseload.
- **Stored energy** — `EnergyGenAbilityDB.EnergyStored` (KJ). The bank for spikes the reactor can't deliver instantly (a warp jump, a weapon alpha).

Both live on ONE shared blob: `EnergyGenAbilityDB` (`Pulsar4X/GameEngine/Energy/EnergyGenAbilityDB.cs`). Both attribute install-hooks (`EnergyGenerationAtb`, `EnergyStoreAtb`, `EnergySolarGenerationAtb`) create-or-append to it, so a ship's whole power plant aggregates into a single record per entity. Consumer only draws its own `EnergyTypeID` (electricity today).

### Pillar tags (§0h)
- **PROJECTOR⛓COUNTER: NEITHER.** Power is the **enabling substrate**, not a projector. It writes no `WeaponProfile`, has no COUNTER slot — it is the wall socket every projector and counter plugs into.
- **Pillar: Military · Medium / Support** — but per §0f it has **no combat/civilian split at all**: *everything* is its consumer (weapons, warp, ground units, sensors, shields, colony life-support, industry). It is the multi-consumer rule at maximum.
- **§0i — Power's "resolver" is NOT a salvo count-resolver.** Its resolver is the **§0b supply-gate loop** — the demand/generation/store bookkeeping in `EnergyGenProcessor.EnergyGen` — a per-entity balancer, not an O(buckets) battle kernel. It never appears in `AutoResolve`/`CombatKernel`.

### Complete dial → derived-stat → engine-wire table (VERIFIED)

**GENERATION (door 4.1)** — attribute `EnergyGenerationAtb` (`Energy/EnergyGenerationAtb.cs:10-16`), install hook `:31`.

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion (the supply gate — which loop reads it) |
|------|------|--------------|-------|-------------------------|----------------------------------------------------------|
| Gen | Source (fission/fusion, fuel-burning) | `PowerOutputMax` → reactor total | ✅ | `EnergyGenerationAtb.cs:57` `genDB.MaxOutputFromReactor += PowerOutputMax` → `EnergyGenAbilityDB.cs:17-20` `TotalOutputMax = Reactor + Solar` | read by `EnergyGenProcessor.EnergyGen` (`:24 output = TotalOutputMax − Demand`); feeds ground supply gate + colony life-support |
| Gen | Source (solar) | `MaxOutputFromSolar` | ✅ | `EnergySolarGenerationAtb.cs:67` adds panel to `SolarPanels`; `EnergySolarGenProcessor.cs:37` `MaxOutputFromSolar = ComputeSolarMax(entity)` — sums panels **attenuated by distance from each star** (`:40-64`, via `SensorTools.AttenuatedForDistanceList`, `:58`) | recomputed **every hourly hotloop tick** (`EnergyGenHotloopProcessor`, `RunFrequency 1hr` `:16`, keyed to `EnergyGenAbilityDB` `:20`) → into `TotalOutputMax` |
| Gen | Source (RTG) | low-output / huge-lifetime `EnergyGenerationAtb` | ◐ wire | expressible today via the `rtg` template (`energy.json:135-254`, `Efficiency = TechData('tech-conductors')+10` `:200`); no first-class "fuel-free reliable" framing | same gate as any reactor |
| Gen | Source (antimatter / zero-point / exotic) | very-high `PowerOutputMax`, no fuel | ⏳ defer (H12) | high output is expressible; the **catastrophe-on-breach** identity needs the missing containment/meltdown mechanic | — |
| Gen | Output level | `PowerOutputMax` (kW) | ✅ | `EnergyGenerationAtb.cs:15` → `:57` | the reactor total → supply gate |
| Gen | Fuel & lifetime | `FuelType` / `FuelUsedAtMax` (kg/s) / `Lifetime` (s) | ✅ | `EnergyGenerationAtb.cs:12,13,17`; `:58-60` sets `TotalFuelUseAtMax` + seeds `LocalFuel = maxUse × Lifetime` | drained in `EnergyGenProcessor.cs:46-47` `LocalFuel -= TotalFuelUseAtMax.maxUse × Load × Δt` |
| Gen | Response / load (baseload, can't surge) | `Load` (0..1), `Output` | ✅ | `EnergyGenProcessor.CalcLoad(demand, totalOutputMax)` clamps `demand/max` to [0,1] (`:94-99`); over-demand met by **battery discharge**, reactor never over-driven | `EnergyGen` `:43-45` sets `Load`/`Output` |
| Gen | Signature / heat | reactor `Load` → EMCON | ◐ wire (unblocked, deferred) | `SensorSignatureAtb` on templates (`energy.json:70-76`); reactor-load-into-heat hook flagged at `Sensors/Emcon/EmconActivityProcessor.cs:31-34` (the `Load` inversion bug is FIXED, so it's now wireable, just not wired) | detection side, not the salvo |
| Gen | Safety / containment (breach) | — | ⏳ defer | **no meltdown mechanic exists**; a reactor-breach-damages-ship rule is the missing grave rung | — |

**STORAGE (door 4.2)** — attribute `EnergyStoreAtb` (`Energy/EnergyStoreAtb.cs:7-14`), install hook `:22`.

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion (the supply gate — which loop reads it) |
|------|------|--------------|-------|-------------------------|----------------------------------------------------------|
| Store | **Capacity** (the single most load-bearing number on a ship) | `MaxStore` (KJ) → `EnergyStoreMax` | ✅ | `EnergyStoreAtb.cs:14` `MaxStore`; `:35-43` `genDB.EnergyStoreMax[EnergyTypeID] += MaxStore` | read by the **warp gate** (`WarpMoveProcessor.cs:233,235`) + the **beam gate** (`GenericFiringWeaponsProcessor.cs:69`); topped to full by `ShipFactory.ChargeReactors` (`:195-210`) |
| Store | Charge behaviour (surplus → battery) | `EnergyStored` | ✅ | `EnergyGenProcessor.cs:26-27` `output = clamp(TotalOutputMax−Demand, −stored, freestore); EnergyStored += output` — charges from surplus, discharges on shortfall (the passive balancer) | the supply-gate loop itself |
| Store | **Burst / discharge rate** (capacitor vs battery) | — | ◐ wire | **no discharge-rate field exists** — a `MaxDischarge_KJps` would make the alpha-strike/capacitor distinction real; today battery and capacitor differ only in size | — |
| Store | Charge rate (volley cadence) | implicit | ◐ wire | rate is implicit in `EnergyGenProcessor` (bounded by free capacity + surplus, `:20,26`); expose a per-cell charge-rate dial | — |
| Store | Safety / breach | — | ⏳ defer | shares Generation's containment mechanic — a charged bank is a bomb when hit | — |

**Note — a reactor also seeds 1s of store.** `EnergyGenerationAtb.cs:63-71` adds `PowerOutputMax` KJ of `EnergyStoreMax` on install (enough energy for one second of running) even with no battery. So a bare reactor holds a sliver of buffer; a real spike still needs a `battery-bank`.

### The supply-gate loop (§0b) — how generation/storage feed EVERY other door
**This is Power's "resolver": `EnergyGenProcessor.EnergyGen(entity, atDateTime)` (`Energy/EnergyGenProcessor.cs:11-79`), an `IInstanceProcessor` (`:8`, `ProcessEntity :101`).** It is interrupt-driven, not a hotloop tick — a consumer calls `EnergyGenAbilityDB.AddDemand(demand, atDateTime)` (`EnergyGenAbilityDB.cs:45-50`), which **runs `EnergyGen` immediately** (`:48`) then `Demand += demand`, and `EnergyGen` schedules the next interrupt for when the store will fill or empty.

The bookkeeping, step by step (`EnergyGenProcessor.cs`):
1. `freestore = max(0, storeMax − stored)` (`:20`) — headroom in the battery.
2. `output = TotalOutputMax − Demand` (`:24`) — reactor surplus (negative = shortfall).
3. `output = Clamp(output, −stored, freestore)` (`:26`) — can't charge past full, can't discharge past empty.
4. `EnergyStored += output` (`:27`) — the store integrates the surplus/shortfall.
5. Schedule the next interrupt: if charging, `timeToFill = ceil(freestore/output)` → interrupt (`:29-34`); if discharging, `timeToEmpty = ceil(|stored/output|)` → interrupt (`:35-40`). So the sim wakes exactly when the tank hits full or empty.
6. `Load = CalcLoad(Demand, TotalOutputMax)` clamped 0..1 (`:43,94-99`); `LocalFuel −= maxUse × Load × Δt` (`:46-47`).
7. Rolling `Histogram` of (output, demand, store) for the power UI (`:51-78`).

**Throttle-or-hard-block behavior (the honest §0b gap).** §0b's *ideal* is a **soft throttle** — under-fed, a beam auto-throttles to available power. The engine today does a **hard BLOCK** at each stored-energy consumer instead:

| Consumer | Reads | Gate today (file:line) | Status |
|----------|-------|------------------------|--------|
| **Weapons** (beam fire) | `EnergyStored` (spike) | `GenericFiringWeaponsProcessor.cs:65-72` — deduct `beamAtb.Energy/1000` KJ; if `stored < costKJ` → `canFire = false` (skips the shot) | ✅ hard block |
| **Warp** (jump) | `EnergyStored` (spike) | `WarpMoveProcessor.cs:233,235` — `if (creationCost <= estored)` else won't start; then `AddDemand(creationCost…)` `:244-246` and collapse `:265-267` | ✅ hard block |
| **Ground units** (design-time supply gate) | `ReactorSupply_W` vs `EnergyDemand_W` | `GroundUnitAssembly.cs:131-132,137-138` — `if (energyDemand > reactorSupply)` → design **Invalid**. Supply = `WeaponSupply.ReactorOutput_W` = `EnergyGenerationAtb.PowerOutputMax × 1000` (kW→W) | ✅ hard block |
| **Colony life-support** | `TotalOutputMax` (sustained) | `Colonies/SustenanceProcessor.cs:49-51` — `powerDemand = pop × PerCapitaPowerDemand; powerSupply = EnergyGenAbilityDB.TotalOutputMax; PowerShortage = Shortage(demand,supply)` | ◐ inert (`PerCapitaPowerDemand` defaults 0) |
| **Sensors** (active ping) | — | none — active-ping cost is EMCON, not a power draw | ◐ wire |
| **Shields** (regen) | — | none — the shield pool isn't fed from power | ◐ wire |

The design intent (the numbers steer you to add a generator) is identical; only the mechanism is blunt. Converting under-supply to *throttle* rather than *block* is a **named wire, not a redesign** — flagged, not owed.

### Prerequisite fixes & dead stubs (file:line)
1. **The under-supply HARD-BLOCK vs the wanted SOFT-THROTTLE (the headline gap).** `WarpMoveProcessor.cs:235` refuses to *start* a jump when under-charged; `GenericFiringWeaponsProcessor.cs:69-72` *skips* the shot. §0b wants both to auto-throttle to available power instead. Named wire.
2. **⚠ GRAVE-RUNG GAP (found in this pass — cradle-to-grave is BROKEN on the loss rung for reactors + batteries).** `MaxOutputFromReactor` and `EnergyStoreMax` are **only ever incremented** (`EnergyGenerationAtb.cs:57`, `EnergyStoreAtb.cs:37`) and **never decremented**: both attributes' `OnComponentUninstallation` are **no-ops** (`EnergyGenerationAtb.cs:75-78`, `EnergyStoreAtb.cs:46-49`), and `EnergyGenAbilityDB` is **NOT in `ReCalcProcessor.TypeProcessorMap`** (`Engine/Processors/RecalcProcessor.cs:16-35` — only `ComponentInstancesDB` and `SensorAbilityDB` are). So when the damage system destroys a reactor or battery and calls `ReCalcProcessor.ReCalcAbilities` (`DamageProcessor.cs:156,238`), the ship's reactor output and battery capacity **do not drop**. Losing your power plant to a hit is currently free. (Solar is the partial exception — `EnergySolarGenerationAtb.OnComponentUninstallation` removes the panel `EnergySolarGenerationAtb.cs:71-75`, and `MaxOutputFromSolar` is recomputed each tick — but only if uninstall actually fires on damage-destroy.) This must be fixed for the "destroyed → re-mine/re-build" rung to be real.
3. **Dead-legacy flag (L1 "dead code that looks live").** `SensorReceiverAtb.IsEnergyGen` (a bool on the *sensor* attribute) looks like the solar path but is **vestigial** — its `SensorScan` branch only sets `LocalFuel`, adds nothing to power output. The working solar is `EnergySolarGenerationAtb`. Don't build on `IsEnergyGen`.
4. **`EnergyGenerationAtb` throws if a ship mixes energy types or fuel types** (`:50` "cannot use two different energy types", `:54` "cannot have power plants that use different fuel types"). A deliberate simplification — noted so a multi-fuel design doesn't surprise-crash.
5. **Storage depth fields don't exist** (`MaxDischarge_KJps`, per-cell charge-rate) — the ◐ wires above.

### Design essence captured inline (merged-doc relevance)
**None load-bearing.** Power carries no franchise/design-essence merge — it is pure enabling infrastructure. The only cross-doc dependency is downstream: every other category's footer bottoms out here ("the mass funnels the chassis," "output 0 with no reactor"). The one design nuance worth holding inline is the **Generation↔Storage division of labour**: a reactor is capped at load 1.0 (`CalcLoad` `:94-99`) and *cannot surge*; a spike (warp bubble ~1,000,000 KJ, or a weapon alpha) is met by the **battery discharging** (`EnergyGen` `:26` clamps output to `−stored`), then recharged from reactor surplus. The two doors trade in one KJ currency — "more reactor (heavy) vs more battery (heavy)" is the player decision, read straight off `TotalOutputMax` and `MaxStore`.

### §0g stamp
- **Reachable (cradle-to-grave):** ✅ **on the build side, ⚠ BROKEN on the loss rung.** mineral (fissile-fuels / lithium / silicon, mined) → material (refined stainless-steel / plastic / lithium / copper, `energy.json` `ResourceCost`) → production (`IndustryTypeID: component-construction`) → component (`reactor` / `battery-bank` / `rtg` / `steam-turbine-reactor` / `solarArray` templates, `energy.json`) → **research** (`TechData('tech-battery-capacity')` `:123`, `TechData('tech-conductors')` `:200`, `TechData('tech-panel-efficiency')`/`-bandwidth`/`-density` on solar) → installed (`EnergyGenAbilityDB`) → decision (reactor-vs-battery mass trade; source triangle output↔independence↔safety) → **loss: currently a no-op for reactors/batteries** (finding #2 above). Fix the loss rung and the chain closes.
- **Mirrored (NPC must generate/store power too):** ✅ **automatic and free** — power is a component on a shared door, so an NPC ship built through the same `ShipFactory`/industry rails carries the same `EnergyGenAbilityDB`, draws warp/weapon energy through the same `AddDemand` path, and is hand-charged by the same `DefaultStartFactory` (`:325-330`) / `ChargeReactors`. No player-only code; the supply gate binds identically on both sides.
- **Observable (the power-balance readout):** ✅ **BUILT — Failure-A resolved.** The gauge exists and is wired: `EnergyGenAbilityDB.Histogram` (output/demand/store over time, `:89`) drives `PowerGenWindow` (`Pulsar4X.Client/Interface/Windows/PowerGenWindow.cs:91`) and `PowerDBDisplay` (`:31`), plus the reactor total in `DebugWindow.cs:465` (`Stringify.Power(powerDB.MaxOutputFromReactor)`). The number exists AND is shown. The remaining *observability* gap is not the balance readout but the **inert consumers** (colony `PerCapitaPowerDemand` = 0, so life-support shortage never shows a real load) — a calibration wire, not a missing gauge.

### Cross-category shared state (Prime Directive — Power gates Weapons / Sensors / Propulsion / Industrial)
Everything below **shares the single `EnergyGenAbilityDB` blob** on the entity — none of them call Power; they read the same memory (`EnergyStored` / `TotalOutputMax`).
- **Weapons ▸ Beam** — reads/deducts `EnergyStored` before every shot (`GenericFiringWeaponsProcessor.cs:65-72`). No power → no shot. (Auto-resolve firepower folds weapon efficiency into **Mass**, not the salvo — `AUTO-RESOLVER-ANATOMY.md:101` "build-side, correct"; the resolver never reads power directly.)
- **Propulsion ▸ Warp** — the bubble is paid from **stored electricity**, not fuel (`WarpMoveProcessor.cs:233,244-246,265-267`). A flat battery is the literal "spawned ship won't move" bug; `ChargeReactors` is the fix. `AUTO-RESOLVER-ANATOMY.md:199`: warp power is **movement-side, not the salvo**.
- **Defense ▸ Shields** — ◐ shield regen is **not yet** fed from power (a named §0f expansion wire).
- **Sensors ▸ EMCON** — active-ping heat is EMCON, not a power draw today; the reactor-`Load`→heat hook is flagged wireable at `EmconActivityProcessor.cs:31-34`. Solar generation itself *reads* the sensor layer (`SensorProfileDB` stars) for attenuation (`EnergySolarGenProcessor.cs:46-58`).
- **Colonies ▸ Life-support** — `SustenanceProcessor.cs:49-51` reads `TotalOutputMax` as the colony power supply vs `pop × PerCapitaPowerDemand`; ◐ inert until `PerCapitaPowerDemand` is calibrated.
- **Ground ▸ Unit assembly** — the design-time hard power gate (`GroundUnitAssembly.cs:137`); supply = `WeaponSupply.ReactorOutput_W(EnergyGenerationAtb.PowerOutputMax × 1000)`.
- **Chassis (§0b Mass cascade)** — reactor/battery `Mass` (`energy.json` `Mass` dial, e.g. reactor 1000–25000 kg, `Power Output = 50 × Mass`) funnels the chassis budget — the Death-Star forcing (a planet-cracker's generator won't fit under Mega).
- **Industrial** — every power component is built through `component-construction` and consumes refined materials; `fissile-fuels` cost scales with `Fuel Consumption × 3600 × Lifetime` (`energy.json:21`).

### Holes owned/resolved (H12)
- **H12 — Exotic power source** (effectively unlimited / no-fuel; e.g. Asgard/Stargate tech). **Status: resolved as a Generation ▸ Source *dial*, LOW priority.** It maps cleanly to a very-high-`PowerOutputMax`, no/low-fuel `EnergyGenerationAtb` — the source triangle already has the slot. **Home:** this dossier, Generation door, Source row (antimatter / zero-point / exotic, ⏳ **defer**). The one thing that makes it more than "a big reactor" — the **catastrophe-on-breach identity** — waits on the single net-new mechanic this whole category defers: the **containment / meltdown grave-rung** (also what makes antimatter *dangerous*, not just powerful, and what finding #2's loss-rung fix feeds into). No new door, no new resolver — it is DATA for the existing Generation loop.

---

## §5 — Defense

Defense is the **other half of the combat matchup** — the doors that decide what *survives* the weapons doors' output. And thanks to the resolver merge (`CombatKernel`, 2026-07-08), armour + shield math is now **one definition** both a ship and a ground unit read, so two of these four doors are already deeply Modelled on both sides. The category's shape: **Armor** (passive flat soak — kinetic's friend), **Shields** (active depleting pool — the nature matchup, energy-porous), **Hardening** (resistance to the *environment*, not weapons), **Fortification** (the ground defensive position).

**The yardstick — the damage matchup + the survival systems.** Armor and Shields land squarely on the combat resolver (`Toughness`, `ArmourSoak`, `ShieldPool_J`, `SoakFractionOf`). Hardening's real consumer is the **hazard/attrition system**, not the salvo math (honest: it barely touches combat). Fortification lands on the **ground** resolve (`DefenseMult`). Per §0f, each is graded against its *actual* consumer.

**Two CONNECT findings this category surfaces (the design headlines):**
1. **Space armour's flat-per-source soak is DEAD.** Ground armour gets the swarm-bounces/alpha-punches behaviour (`CombatKernel.ArmourSoak`, 1.5/point); ship armour does **not** — the ship resolve still folds armour into the `Toughness` HP pool and never populates `Combatant.Armour` (the reconciliation flagged at `CombatKernel.cs:104-105`). So a swarm of chip-damage hits a ship's armour as if it were just more HP, instead of *bouncing*. A wire, not a rebuild.
2. **Armour has NO nature dimension to combat.** Only **shields** carry the matchup (kinetic 1.0 / energy 0.5 / exotic 0.0). Space armour's material (`armorType` / `ArmorBlueprint.SignatureResistance`) is real **only vs hazards** — dead to the auto-resolver; ground armour has no material at all. Giving armour a nature-resistance (ablative beats energy, reactive/composite beats kinetic) would **double the matchup depth** and make the armour-vs-shield choice a real rock-paper-scissors. The single richest wire in the category.

### §5.0 Shared defense dials (all four doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **What it stops** | the damage/effect it mitigates — a *weapon* (Armor/Shields), the *environment* (Hardening), or *by position* (Fortification) |
| **Mitigation strength** | how much — HP pool (Armor), joules soaked (Shields), a % or fraction (Hardening/Fortification) |
| **Mass / footprint** | armour IS mass (the cheapest protection-per-ton, drags evasion); a shield is power-hungry; a fort is fixed |
| **Grave rung** | a shot-off generator drops the shield; spalled armour is gone — you lose the protection you built |

### 5.1 Defense ▸ ARMOR  🔒 *locked*
*The passive plate — the cheapest tonnage-for-protection in the game. It soaks damage as raw hit-points AND (done right) bounces small hits while letting a big alpha punch through. Armour is **kinetic's friend and the shield's opposite**: it stops the penetrator a shield lets through, and shrugs the swarm a shield would drain against. Everything from a frigate's belt to reactive tank plating to a capital's meters of composite falls out of these dials.*

**The core decision — MASS vs PROTECTION vs the MATCHUP.** Armour is cheap and always-on (no power, no regen delay), but every centimetre is **mass** that drags your evasion and funnels the chassis — and it's **flat**: a hail of chip-damage *bounces*, but one big shell *punches through*. You choose how thick (HP), how hard (the flat soak), and — the missing dial today — what it's *made of* (which damage nature it resists).

**A. Thickness / HP — the soak pool**
| Option | Why pick it | The catch (anti-dominance) |
|--------|-------------|----------------------------|
| **Thin** | light, cheap — keeps evasion up | small HP pool — folds fast under sustained fire |
| **Thick** | a deep HP pool — capital-grade staying power | **heavy** — drags evasion, funnels the chassis (§0b) |

**B. Hardness — the flat-per-source soak (swarm-bounce vs alpha-punch)**
| Option | Why | Catch |
|--------|-----|-------|
| **Soft** | cheap; fine vs big alpha hits (they get through anyway) | chip damage adds up — a swarm chews it |
| **Hard** | **bounces** small hits (swarm-proof) — the anti-flak/anti-fighter plate | a big alpha **punches through** regardless; costlier |

**C. Material / nature-resistance — WHAT it's made for (the missing matchup dial)**
| Option | Why | Catch |
|--------|-----|-------|
| **Composite (anti-kinetic)** | shrugs slugs/sabot — the armour-cracker's counter | energy weapons still burn through |
| **Ablative (anti-energy)** | boils away to defeat beams/plasma — the Dune-lasgun answer | spent after heavy energy fire; weak vs kinetic |
| **Reactive** | defeats shaped/penetrating hits | one-shot cells; dead weight vs energy |

**D. Coverage — where the plate is**
| Option | Why | Catch |
|--------|-----|-------|
| **All-round** | protected from any angle | the most mass |
| **Belt / glacis (partial)** | heavy armour only where it matters — lighter | a flank/rear hit finds thin plate |

**E. Mass (emergent — the cascade)** — armour *is* mass; it's the cheapest protection-per-ton but the most direct evasion/chassis drag.

**Modellability audit (§0d — armour is half-Modelled, half the category's best wires):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Thickness / HP | ✅ | space: `EntityDamageProfileDB.Armor.thickness × ArmorHitPointsPerThickness_J` (100 kJ) → `Toughness`; ground: `GroundArmorAtb.HP` → `GroundUnit.MaxHealth` |
| Hardness (flat soak) — GROUND | ✅ | `GroundArmorAtb.Defense` → `GroundDamageMatrix.ArmourSoak` → `CombatKernel.ArmourSoak` (1.5/point, floor 0.1 — swarm bounces, alpha punches) |
| Hardness (flat soak) — SPACE | ◐ **wire** | **DEAD** — ship armour folds into `Toughness`; `Combatant.Armour` is never populated, so a ship gets NO swarm-bounce. Populate it (the `CombatKernel.cs:104-105` reconciliation) |
| **Material / nature-resistance** | ◐ **wire** (the big one) | space `armorType` material EXISTS but is **hazard-only, dead to the auto-resolver**; ground has none. Give armour a nature-soak table (mirror shields) → the matchup-doubler |
| Coverage | ⏳ **defer** | `ArmorVertex` geometry is per-pixel-sim only; the aggregate resolver is non-positional |
| Mass | ✅ | armour mass → `MassVolumeDB` → evasion + chassis budget |

**Reading:** Armour's **toughness core is Modelled** on both sides, and the **flat-per-source soak is live on the ground** through the shared kernel. But the two best wires in Defense live here: (1) **populate ship `Combatant.Armour`** so space armour bounces swarms like ground does (the resolver-merge already built the math — it's just unfed on the ship side), and (2) **give armour a nature dimension** so the armour-vs-shield decision becomes real rock-paper-scissors (composite walls kinetic, ablative walls energy) instead of armour being a flat scalar while shields carry the whole matchup.

**Numbers:** thickness → HP (× 100 kJ); `Defense` → flat soak (1.5/point, floor 0.1); nature-resist (new) → a soak table mirroring shields (K/E/X). Cradle-to-grave: armour is a **component** (mine ore → refine plate → install → spall off under fire).

**Preset coordinates:** light plating (thin) · composite belt (anti-kinetic) · reactive plating (anti-penetrator) · ablative armour (anti-energy, ◐ needs nature dial) · heavy capital armour (thick, all-round) · ground plating (`GroundArmorAtb`).

### 5.2 Defense ▸ SHIELDS  🔒 *locked*
*The active bubble — a regenerating energy buffer that soaks a torrent, then recharges between blows. Shields are **the armour's mirror in the matchup**: kinetic hammers straight through them (soak 1.0), energy only half-bleeds (0.5), and an exotic/ion weapon **bypasses entirely** (0.0). The cleanest Modelled door in Defense — the whole depleting-pool + nature-matchup runs live for ship AND ground through the shared kernel.*

**The core decision — REGENERATING BUFFER vs the MATCHUP.** A shield soaks *far* more than its mass in armour would — but it **depletes** (drop it and you're bare until it regens) and it's **porous to the wrong weapon** (kinetic walls it, energy leaks, exotic ignores it). You choose how big the pool (capacity), how fast it comes back (regen), and — the missing dial — what nature it's tuned against.

**A. Capacity — the pool size (`Capacity_J`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Small** | cheap, light, low power | **cracks fast** — a big alpha drops it and the hull is bare |
| **Large** | soaks a sustained torrent; buys time | heavy + **power-hungry** (a big generator → the Power doors bite) |

**B. Regen rate — how fast it recovers (`RegenRate_Jps`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Slow** | cheaper, bigger capacity per mass | one big absorb, then **down for the fight** |
| **Fast** | **shrugs sustained fire** — recovers between volleys (the attrition-tank) | lower capacity per mass; heavy power draw |

**C. Nature tuning — WHAT it's hardened against (the missing dial)**
| Option | Why | Catch |
|--------|-----|-------|
| **Kinetic-hardened** | walls slugs/flak even better | still leaks energy |
| **Energy-hardened** | closes the 0.5 energy-bleed — the anti-beam screen | costlier; kinetic still hammers |

**D. Coverage — fleet bubble vs per-unit**
| Option | Why | Catch |
|--------|-----|-------|
| **Aggregate (space v1)** | one pool protects the fleet — simple, cheap to resolve | a focused strike can't be soaked by *one* ship's shield; it's a shared pool |
| **Per-unit (ground)** | each unit's own bubble | more bookkeeping |

**Modellability audit (§0d — the cleanest door; fully live both domains):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Capacity | ✅ | `ShieldAtb.Capacity_J` → `ShipCombatValueDB.ShieldCapacity_J` → `FleetCombatStateDB.ShieldPool_J`; ground `GroundAugmentAtb.Shield` → `GroundUnit.Shield`/`CurrentShield` |
| Regen — SPACE | ✅ | `ShieldAtb.RegenRate_Jps` → the pool regen in `ApplyShield` |
| Regen — GROUND | ◐ **wire** | ground regen is a **global constant** (`ShieldRegenPerHourFraction 0.34`), not a per-component dial — make it a dial |
| **Nature tuning** | ◐ **wire** | the matchup (K 1.0/E 0.5/Exp 0.75/exotic 0.0) is a **fixed engine table** (`CombatKernel.ShieldSoakFraction`), not a per-component dial — make it tunable for a hardened shield |
| Coverage (per-ship) | ◐ **wire** | space pool is per-**fleet** aggregate (v1); a per-ship shield is a refinement |
| Grave rung | ✅ | a shot-off `ShieldAtb` generator drops the pool (cradle-to-grave, already wired) |

**Reading:** Shields is **the most-Modelled door in Defense** — capacity, regen, the nature matchup, drain-before-armour, and the depleting+regen pool all run live for ship (`ApplyShield`/`ResolveShield`) and ground (`GroundForcesProcessor`) through the *same* `CombatKernel`. The depth wires are small: make the **nature-resistance tunable** (today every shield has the identical K/E/X profile — a kinetic-hardened vs energy-hardened choice is a per-component dial away) and make **ground regen a dial** (it's a constant). Note the honest v1 stub: the space pool is per-**fleet**, not per-ship.

**Numbers:** `Capacity_J` (J pool) · `RegenRate_Jps` (J/s) · nature soak (K 1.0/E 0.5/Exp 0.75/exotic 0.0). Reference: base-mod Deflector = 5 MJ capacity, 100 kJ/s regen. Cradle-to-grave: a shield generator is a **component** (research → build → install → lose).

**Preset coordinates:** light deflector · heavy shield bank (large capacity) · fast-regen screen (attrition-tank) · kinetic/energy-hardened barrier (◐ needs the nature dial) · **planetary shield dome** *(FACILITY — the Shields door on a Structure chassis: Halo/Gungan/Trek city shield)*.

### 5.3 Defense ▸ HARDENING  🔒 *locked*
*Surviving the **environment**, not the weapon. Hardening is what keeps you working inside a radiation belt, a jamming nebula, an EMP burst, a warp-inhibitor field, a corrosive cloud. Its consumer is the **hazard/attrition system + crew and system survival** — NOT the salvo math. Honest upfront: this door barely touches combat, and that's correct — it's a different kind of protection (against the battlefield, not the enemy's guns).*

**The core decision — SURVIVE HOSTILE SPACE.** The galaxy itself is dangerous: flares, nebulae, ion storms, hard vacuum, EMP. Hardening buys operation where an unhardened hull degrades or fails. You pick *which* hazards to resist and *how much* — a deep-nebula science ship, an EMP-hardened warship, a radiation-rated colony.

**A. Resisted effect — WHICH hazard (`ResistedEffectTypeId`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Radiation / heat** | operate near a star, in a belt, after a nuke | narrow — a radiation suit doesn't stop jamming |
| **EMP / EM** | systems survive an ion storm / EMP weapon | dead weight where there's no EM threat |
| **Sensor-jam / warp-inhibit / drag** | see, jump, and move through a nebula that blinds others | situational — clear space doesn't need it |

**B. Resistance fraction — partial vs full (`ResistanceFraction`, 0..1)**
| Option | Why | Catch |
|--------|-----|-------|
| **Partial** | cheap, light — takes the edge off | still degrades in a strong hazard |
| **Full immunity** | operate freely in the worst of it | heavy, costly, single-purpose |

**C. Redundancy / damage-control — crit-resistance** *(the sci-fi "it doesn't cascade" dial)*
| Option | Why | Catch |
|--------|-----|-------|
| **Bare** | light, cheap | one hit can cascade — a critical loss |
| **Redundant / DC teams** | a hit is contained — degrades gracefully, not catastrophically | mass/crew; ⏳ needs the degraded-condition model |

**Modellability audit (§0d — honestly NOT a combat door; consumer = hazards):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Resisted effect + fraction | ✅ | `HazardResistanceAtb` (`ResistedEffectTypeId` + `ResistanceFraction`) → `SpaceHazardTools.ResistanceFraction` → consumed by `SensorScan`/`WarpMoveProcessor`/`SpaceHazardProcessor` + ground `GroundUnit.EnvResistance`. **A new counter is DATA, not code** (generic attribute) |
| — but the combat resolver reads NONE of it | — | Hardening is a **hazard/survival** dial, not a salvo dial — graded against its real consumer (§0f) |
| Radiation/EMP → crew & electronics survival | ⏳ **defer** | the hazard *damage* kinds exist, but crew-knockout / system-disable isn't a combat mechanic yet |
| Redundancy / damage-control | ⏳ **defer** | ties to the **degraded-condition** model (`docs/WEAPONS-AND-DODGE-DESIGN.md`) — a genuinely net-new hook |

**Reading:** Hardening is the honest odd-one-out — its **hazard-resistance core is Modelled** (a clean, data-driven generic attribute; a new resistance is JSON, not C#), but **the combat resolver never reads it**: this is the *survive-the-environment* door, and its consumer is the hazard/attrition system, crew survival, and system integrity. That's a legitimate protection axis (Expanse radiation, nebula ops, EMP warfare), not a flaw — you just name the right consumer. It also surfaces the **redundancy/damage-control** door (net-new), which points at the parked degraded-condition model. *(Correction, verified in the Enhancers survey: `GroundAugmentAtb.ToughnessBonus` — earlier flagged here as "dead" — is actually **read and applied** as an HP multiplier in `GroundUnitAssembly:110,129`; it's Modelled-but-not-surfaced as a standalone stat, not dead. See §6.1.)*

**Numbers:** `ResistanceFraction` (0..1) per `HazardEffectType` (Radiation/Heat/EMP/SensorJam/WarpInhibit/Drag/Corrosive/Gravimetric). Cradle-to-grave: hardening is a **component** (the base-mod `sensor-hardening-module` is the worked example).

**Preset coordinates:** radiation shielding · EMP hardening · sensor-hardening module · deep-nebula hull (multi-hazard) · damage-control suite *(redundancy, ⏳)*.

### 5.4 Defense ▸ FORTIFICATION  🔒 *locked*
*The dug-in position — trade mobility for a defensive multiplier. A fortified ground unit in cover or a bunker takes far less; a bunker network projects that protection to its neighbours. **Ground-only by nature** — a "space fortress" isn't a fortification dial, it's a **station** (Chassis ▸ Structure with the other Defense doors mounted on it), so the fortress falls out of chassis + armour + shields, not a separate knob.*

**The core decision — DIG IN.** A position is protection you can't carry: cover, entrenchment, a hardened bunker. You give up mobility (a fort doesn't move) for a multiplier on everything that hits you — and you can **project** it to adjacent friendly ground (a defensive belt). The catch: a hard fixed position is a **known target** (it occupies a war-map hex the enemy can bombard or must capture).

**A. Local fortify — dig-in bonus to your own region (`LocalFortify`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Light entrenchment** | cheap, fast to raise — some cover | modest multiplier |
| **Hardened bunker** | big local bonus (base-mod Bunker +25%) — halves incoming at the cap | costly, fixed, a bombard target |

**B. Adjacent projection — extend the bonus to neighbours (`AdjacentProjection`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Self-only** | all the protection stays home | a breakthrough next door is unprotected |
| **Projecting (bunker network)** | +12% to adjacent friendly regions — a defensive belt | thinner than a local fort; only same-faction ground |

**C. Cover / terrain — stack with the ground**
| Option | Why | Catch |
|--------|-----|-------|
| **Open ground** | mobile, no penalty | no cover bonus |
| **Dig into terrain** | stacks `CoverDefenseMult` with the fort bonus | terrain-locked; slow to reposition |

**D. Footprint — targetability (`TileFootprint`)** — a big fort **occupies a war-map hex**: it becomes a capture/bombard target. Not a combat *bonus* — the *vulnerability* side of digging in (a hard point is a fixed point).

**Modellability audit (§0d — clean, fully Modelled, ground-only):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Local fortify | ✅ | `GroundDefenseAtb.LocalFortify` → `GroundFortification.DefenseMult` → `GroundForcesProcessor:317` (`pool /= coverFort` — divides the attacker's damage) |
| Adjacent projection | ✅ | `GroundDefenseAtb.AdjacentProjection` → `SumAdjacent` (same-faction gate), capped at +100% (halves incoming) |
| Cover / terrain | ✅ | `GroundTerrain.CoverDefenseMult` stacks in the same `coverFort` term |
| Footprint / targetability | ✅ | `GroundFootprintAtb.TileFootprint` → the war-map capture/bombard system (`GroundBuildings`) — orthogonal to the combat bonus |

**Reading:** Fortification is **fully Modelled and clean** — the local + adjacent bonuses run live through `GroundFortification.DefenseMult`, capped at halving incoming, and stack with terrain cover. It's **ground-only on purpose**: space's "defensive position" is a *station*, which the Chassis + Armor + Shields doors already build. The base-mod Bunker (+25% local / +12% adjacent, both JSON data) is the worked example, and it doubles as a war-map target via its footprint — the honest tradeoff of a fixed strongpoint.

**Numbers:** `LocalFortify` / `AdjacentProjection` (fractions, e.g. 0.25 / 0.12; per-design JSON) → `DefenseMult = 1 + min(1.0, Σ)`; cap +100%. Cradle-to-grave: a fortification is a **built installation** (the Bunker) — captured with the region, destroyed by bombardment.

**Preset coordinates:** foxhole / dig-in (light) · Bunker (+25% local) · bunker network (adjacency belt) · hardened silo · fortress city (big footprint = a bombard/capture prize).

---

## ✅ §5 Defense — COMPLETE (4/4 doors locked)
Armor 🔒 · Shields 🔒 · Hardening 🔒 · Fortification 🔒. **Yardstick = the damage matchup + the survival systems.** Headline readings: **Shields is the most-Modelled door** — the depleting pool + nature matchup (K 1.0/E 0.5/exotic 0.0) run live for ship AND ground through the shared `CombatKernel`; depth wires are small (nature-tuning as a per-component dial; ground regen as a dial). **Armor is the door with the category's two best CONNECT wires:** (1) space armour's flat-per-source soak is **DEAD** (ship `Combatant.Armour` unpopulated → a ship gets no swarm-bounce; the resolver-merge math is built, just unfed), and (2) armour has **no nature dimension to combat** (its material is hazard-only) — giving armour a nature-resistance (ablative walls energy, composite walls kinetic) is **the single richest wire in the category**, doubling the matchup into real rock-paper-scissors. **Hardening is honestly NOT a combat door** — its clean, data-driven hazard-resistance is Modelled but the resolver never reads it; it's the *survive-the-environment* axis (radiation/EMP/nebula), and it surfaces the redundancy/damage-control gap (the degraded-condition tie-in). **Fortification is fully Modelled + clean** (ground-only local/adjacent bonuses, capped at halving incoming; a space "fort" is a Station, built from Chassis + the other Defense doors). Build-list: (1) **populate ship `Combatant.Armour`** (space flat-per-source soak — swarm-bounce parity with ground); (2) **armour nature-resistance dimension** (the matchup-doubler — the top item); (3) **shield nature-tuning** as a per-component dial; (4) **ground shield regen** as a dial (it's a constant); (5) the **redundancy/damage-control** door (degraded-condition, net-new).

---

## ⚙ 5 — DEFENSE · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Defense is the **survivability half of every fight** — the four ways a unit refuses to die: soak it (Armor), buffer it (Shields), endure the environment (Hardening), or dig in (Fortification). Three of the four land on the **combat resolver**; Hardening lands on the **hazard/survival** system instead (that's correct, not a flaw). The single richest un-built wire in the whole category is giving **armour a "nature" (material) dimension** so armour ↔ shield becomes real rock-paper-scissors — today shields carry the entire damage-nature matchup and armour is a flat scalar. The second-richest is **populating a ship's flat armour soak** (built math, unfed on the ship side). Both are traced below to file:line.

### Pillar tags (§0h) — note Hardening carries an Influence·Counter role (cultural insulation)
Every projector needs a counter; Defense **is** the counter slot. Tags (`pillar` + `skeleton-role`, per §0h / essence-ext E0a):

| Door | `pillar` | `skeleton-role` | Counters what |
|------|----------|------------------|---------------|
| 5.1 Armor | Military | **Counter** | the kinetic penetrator / the swarm (bounce) |
| 5.2 Shields | Military | **Counter** | the energy torrent / sustained fire (buffer) |
| 5.3 Hardening | Military | **Counter** (vs the *environment*, not the enemy — hazards) | radiation / EMP / nebula / drag |
| 5.3 Hardening ▸ **Cultural Insulation** (essence-ext E1c dial) | **Influence** | **Counter** | an enemy's incoming *belief pressure* (soft-power SWAY) — the Influence·Counter slot |
| 5.4 Fortification | Military | **Counter** (ground-only positional multiplier) | any incoming fire, by dug-in position |

So Hardening is the one door that is **dual-pillar**: its combat/hazard face is Military·Counter; its new Cultural-Insulation dial is the **Influence·Counter** slot (the "state religion / censorship / cultural insulation" cell of the pillar skeleton). Same door, two media — because both are "resist an incoming pressure the combat resolver never reads" (hazards for one, belief for the other).

### Complete dial → derived-stat → engine-wire table (VERIFIED)
Grade legend: ✅ live today · ◐ **wire** (stat/math exists, one connection missing) · ⏳ defer (needs a subsystem first).

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | Resolver insertion |
|------|------|--------------|-------|--------------------------|--------------------|
| **5.1 Armor** | Thickness / HP (space) | `Toughness` (J) | ✅ | ship: `EntityDamageProfileDB.Armor.thickness` (`Damage/DamageComplex/EntityDamageProfileDB.cs:14`) × `ArmorHitPointsPerThickness_J`=100 kJ (`Combat/ShipCombatValueDB.cs:84`) → `toughness +=` (`ShipCombatValueDB.cs:290-291`) | folded into per-ship Toughness (casualty math) |
| 5.1 | Thickness / HP (ground) | `GroundUnit.MaxHealth` | ✅ | `GroundArmorAtb.HP` (`GroundCombat/GroundArmorAtb.cs:23`) → unit health pool | seeds `Combatant.Health/MaxHealth` |
| 5.1 | **Hardness — flat-per-source soak (ground)** | `Combatant.Armour` | ✅ | `GroundArmorAtb.Defense` (`GroundArmorAtb.cs:25`) → `GroundUnit.Defense` → `GroundCombatant.ToCombatant` sets `Armour = unit.Defense` (`GroundCombat/GroundCombatant.cs:100`) → `CombatKernel.ArmourSoak` (`Combat/CombatKernel.cs:224-231`) | flat subtract per attacker-source, floored at `ArmourMinPassFraction`=0.1 (`CombatKernel.cs:78`) |
| 5.1 | **Hardness — flat-per-source soak (SPACE)** | `Combatant.Armour` (ship) | **◐ wire — DEAD** | field EXISTS (`CombatKernel.cs:106`) and math EXISTS (`ArmourSoak` `:224`), but **no ship code populates it** — `CombatEngagement.cs` never sets `.Armour` and does not build a `Combatant`; ship armour only folds into Toughness (`ShipCombatValueDB.cs:290`). **THE FIX: populate ship `Combatant.Armour`** so a ship bounces swarms like a ground unit (the "swarm-bounce" wire). | route ship salvo through `CombatKernel.ArmourSoak` per fire-mix source |
| 5.1 | **Material / nature-resistance** (the flagged **armour-NATURE dimension** — richest wire in the category) | *new:* per-armour nature-soak table | **◐ wire (top item)** | space `armorType` material exists but is **hazard/pixel-sim-only** — read only by the damage-pixel painter (`Damage/DamageComplex/ComponentPlacement.cs:243`, `DamageProcessor.cs:259`), **dead to the auto-resolver**. Ground armour has no nature axis at all. **THE FIX: give `Combatant.Armour` a per-nature soak table mirroring shields** (composite walls Kinetic, ablative walls Energy, reactive vs Explosive) → doubles the matchup into rock-paper-scissors. | new resolver term inside `ArmourSoak` keyed on `WeaponProfile.Nature` |
| 5.1 | Coverage (all-round vs belt) | `ArmorVertex` geometry | ⏳ defer | positional; the aggregate resolver is non-positional | none (drop or per-pixel-sim only) |
| 5.1 | Mass (emergent) | `MassVolumeDB` → Evasion | ✅ | armour mass drags evasion + chassis budget | `CalculateEvasion` (`ShipCombatValueDB.cs`) |
| **5.2 Shields** | Capacity (space) | `ShieldCapacity_J` → pool | ✅ | `ShieldAtb.Capacity_J` (`Combat/ShieldAtb.cs:22`) → `ShipCombatValueDB.ShieldCapacity_J` (`:127`), summed × health (`:281-283`) → `Combatant.ShieldCapacity` (`CombatKernel.cs:113`) | `ResolveShield` drains before Health (`CombatKernel.cs:205-217`) |
| 5.2 | Regen (space) | `ShieldRegen_Jps` | ✅ | `ShieldAtb.RegenRate_Jps` (`ShieldAtb.cs:25`) → `ShipCombatValueDB.ShieldRegen_Jps` (`:130`, summed `:282`) → `Combatant.ShieldRegen` (`CombatKernel.cs:116`) | recharge tail of `ResolveShield` (`CombatKernel.cs:214-216`) |
| 5.2 | Capacity / shield (ground) | `GroundUnit.Shield` | ✅ | `GroundAugmentAtb.Shield` (`GroundCombat/GroundAugmentAtb.cs:36`) → `GroundUnit.Shield` → `ToCombatant` seeds `ShieldPool`+`ShieldCapacity` (`GroundCombatant.cs:101-102`) | shared `ResolveShield` |
| 5.2 | **Regen (ground)** | *(none per-component)* | ◐ wire | ground `ShieldRegen = 0` hard-coded in `ToCombatant` (`GroundCombatant.cs:103`); regen is a global constant elsewhere — **make it a per-component dial** | feed a real regen into `ResolveShield` |
| 5.2 | **Nature tuning** (kinetic- vs energy-hardened) | shield-soak-by-Nature | **◐ wire** | the matchup is a **fixed engine table**: `ShieldSoakVsKinetic`=1.0, `VsEnergy`=0.5, `VsExplosive`=0.75, `VsExotic`=0.0 (`CombatKernel.cs:62-68`), dispatched by `ShieldSoakFraction` (`:133-139`). **THE FIX: make the per-nature fraction a per-component dial** so a hardened shield closes the 0.5 energy bleed. | override the constant with a per-`Combatant` soak vector inside `ShieldSoakFraction`/`SoakFractionOf` (`CombatKernel.cs:188-199`) |
| 5.2 | Coverage (fleet pool vs per-ship) | `FleetCombatStateDB.ShieldPool_J` | ◐ wire | space pool is per-**fleet** aggregate (honest v1 stub); a per-ship pool is a refinement | per-ship `Combatant.ShieldPool` (field already exists `:110`) |
| 5.2 | Grave rung | pool shrinks when generator shot off | ✅ | capacity scaled by `comp.HealthPercent` (`ShipCombatValueDB.cs:281-282`) — a dead generator = no pool | already wired |
| **5.3 Hardening** | Resisted effect (which hazard) | `HazardResistanceAtb.ResistedEffectTypeId` | ✅ (hazard-side) | `Hazards/HazardResistanceAtb.cs:23` → `SpaceHazardTools.ResistanceFraction` (`Hazards/SpaceHazardTools.cs:91`) → consumed by `SpaceHazardProcessor.cs:115` / SensorScan / WarpMove + ground `GroundUnit.EnvResistance`. **A new counter is JSON DATA, not code** (generic attribute). | **NOT the combat resolver** — hazard/attrition consumer (§0f) |
| 5.3 | Resistance fraction (partial↔full) | `HazardResistanceAtb.ResistanceFraction` (0..1) | ✅ (hazard-side) | `HazardResistanceAtb.cs:26` | hazard system |
| 5.3 | Radiation/EMP → crew & electronics survival | *(none)* | ⏳ defer | hazard *damage kinds* exist; crew-knockout / system-disable is not a combat mechanic yet | none yet |
| 5.3 | Redundancy / damage-control (crit-resist) | *(none)* | ⏳ defer | ties to the parked degraded-condition model (`WEAPONS-AND-DODGE-DESIGN.md`) — net-new hook. **This is hole H2's own resolution point** (see below). | none yet |
| 5.3 ▸ **CULTURAL INSULATION** (essence-ext E1c — Influence·Counter dial) | Belief-resist (light↔heavy) | *new:* resist term on `ExternalBeliefPressure` | **◐ wire** | Hardening's identity ("resist an incoming pressure the resolver never reads") pointed at the **legitimacy** input surface: a new `ExternalBeliefPressure` field on `LegitimacyInputs` (`Colonies/LegitimacyDB.cs:119` struct) — the first purely-external attacker beside the existing `GovernorCompetence` slot (`LegitimacyDB.cs:89-90`). Insulation **subtracts from** that incoming pressure before `ComputeLegitimacy` (`LegitimacyDB.cs:66`) folds it toward `CollapseThreshold`=20 (`:42`) → rebellion (`LegitimacyProcessor.cs:77`, `IsCollapsing`). Dead weight vs an enemy running no influence. | **NOT the combat resolver** — the legitimacy/rebellion resolver (§0f); mirrors Hardening's hazard-side pattern exactly |
| **5.4 Fortification** (ground-only) | Local fortify (dig-in) | `GroundFortification.DefenseMult` | ✅ | `GroundDefenseAtb.LocalFortify` (`GroundCombat/GroundDefenseAtb.cs:30`) → `GroundFortification.DefenseMult` (`GroundCombat/GroundFortification.cs`) → `coverFort` (`GroundForcesProcessor.cs:267-268`) → `pool /= coverFort` divides attacker damage (`GroundForcesProcessor.cs:317`) | ground resolver pre-divide |
| 5.4 | Adjacent projection | same-faction `DefenseMult` sum | ✅ | `GroundDefenseAtb.AdjacentProjection` (`GroundDefenseAtb.cs:32`), same-faction gate, cap +100% (halves incoming) | same `coverFort` term |
| 5.4 | Cover / terrain | `GroundTerrain.CoverDefenseMult` | ✅ | stacks in `coverFort` (`GroundForcesProcessor.cs:267`) | same term |
| 5.4 | Footprint / targetability | `GroundFootprintAtb.TileFootprint` | ✅ | `GroundCombat/GroundFootprintAtb.cs:31` → war-map capture/bombard (`GroundBuildings.cs:297`) — the *vulnerability* side, orthogonal to the bonus | war-map system, not the salvo |

### Resolver insertion points (from AUTO-RESOLVER-ANATOMY §1–§4)
The DEFENSE half of one salvo step (`CombatKernel`, byte-for-byte shared by ship `CombatEngagement` and ground `GroundForcesProcessor`):

1. **ApplyShield — SoakFractionOf by Nature (drain-before-hull).** `SoakFractionOf(incoming)` (`CombatKernel.cs:188-199`) rolls the fire mix's damage-weighted **soakable fraction** using the fixed nature table `ShieldSoakFraction` (`:133-139`): Kinetic **1.0** (walls it) · Energy **0.5** (half-bleeds) · Explosive **0.75** · Exotic **0.0** (ignores the shield). Then `ResolveShield(pool, capacity, regen, salvoDamage, soakFraction, dt)` (`:205-217`) absorbs `salvoDamage × soakFraction` up to the current charge, subtracts it, then recharges by `regen × dt` toward capacity. **Nature-tuning dial inserts here** by replacing the constant fractions with a per-`Combatant` vector.
2. **ApplyCasualties — ArmourSoak flat-per-source + ArmourSoakPerPoint 1.5.** After shields, `ArmourSoak(armour, sourceDamage)` (`CombatKernel.cs:224-231`) subtracts a **flat** `armour × ArmourSoakPerPoint` (=1.5, `:75`) from EACH attacker-source, floored so every source still lands `ArmourMinPassFraction`=0.1 (`:78`) of its damage. Because it's flat-per-source, **N chip volleys lose N×flat total but one alpha loses only flat** — armour bounces the swarm, the alpha punches through. **The ship side never calls this** (its `Combatant.Armour` is unpopulated) — that's the swarm-bounce wire.
3. **The Penetration extension = the armour half of the matchup (anatomy §4 #1).** Today armour is flat with **no per-weapon penetration**. Adding `WeaponProfile.Penetration` + reducing `ArmourSoak`'s flat term by penetration is the resolver's #1 backlog item — it makes lance/sabot/AP real armour-crackers and Splash the anti-swarm opposite. This is the **weapons-side counterpart** to the armour-nature dial: nature decides *what* armour resists, penetration decides *whether it's bypassed*.
4. **ShieldCapacity / Regen inputs** come from `ShipCombatValueDB.cs:276-285` (space, summed shield generators × health) and `GroundCombatant.cs:101-103` (ground, seeded from `GroundUnit.Shield`, regen 0).

### Prerequisite fixes & dead stubs (file:line)
- **Ship `Combatant.Armour` unpopulated (DEAD wire).** Field `CombatKernel.cs:106` + math `ArmourSoak` `CombatKernel.cs:224` are built and unit-tested, but no ship code sets `.Armour` — `CombatEngagement.cs` doesn't construct a `Combatant` and `ShipCombatValueDB.Calculate` folds armour into `Toughness` only (`ShipCombatValueDB.cs:290-291`). Until fixed, **a ship gets zero swarm-bounce**. (This is a *resolver-merge* follow-on: ground already presents a `Combatant` via `GroundCombatant.ToCombatant`; ships must too.)
- **Space `armorType` material is combat-dead.** `EntityDamageProfileDB.Armor.armorType` (`Damage/DamageComplex/EntityDamageProfileDB.cs:14`) is read ONLY by the pixel-sim painter (`ComponentPlacement.cs:243`, `DamageProcessor.cs:259`) — the auto-resolver never sees material. Needed base for the armour-nature dial.
- **Shield nature matchup is a hard-coded constant table** (`CombatKernel.cs:62-68`) — not yet a per-component dial. No bug, but the wire point for nature-tuning.
- **Ground shield regen hard-coded to 0** (`GroundCombatant.cs:103`) — make it a dial.
- **`ExternalBeliefPressure` field does not yet exist** on `LegitimacyInputs` (`LegitimacyDB.cs:119`) — must be added (beside `GovernorCompetence`) before Cultural Insulation can subtract from it. This is the ONE new engine field the whole Influence-2.0 gear seam needs (essence-ext E1c).
- **Redundancy/damage-control (H2 partial) blocked on the degraded-condition model** — no per-component partial-damage state exists in the resolver (v1 casualty model is whole-ship removal, anatomy §1).

### Design essence captured inline (cultural insulation; H2 adaptive defense)
**Cultural Insulation = resist ExternalBeliefPressure.** In the merged Influence pillar (`git show origin/main:docs/INFLUENCE-PILLAR-DESIGN.md`), a world's allegiance is a tug-of-war: the owner's governance props legitimacy up, an enemy's influence campaign pushes it down from outside, and past `CollapseThreshold`=20 the world secedes — "a planet taken without a shot." Influence's *kill* reuses the existing legitimacy/rebellion system by adding **one new input, external belief-pressure**, exactly parallel to the built `GovernorCompetence` slot. Cultural Insulation is that attack's **passive counter** — the Defense category's Hardening door pointed at belief instead of radiation: a heavy-insulation world subtracts from the incoming pressure before it hits legitimacy, and is dead weight against an enemy running no influence (the same "buy every advantage" anti-dominance law). It is the Influence·Counter slot made buildable — the only gear half of soft-power defense (the missionary/censor Being stays in People).

**H2 adaptive / self-repairing defense** (`COMPONENT-DESIGNER-CATEGORIES.md` §5, MEDIUM-HIGH): become immune to a weapon after one hit (Borg shields), regrow armour/hull (Replicator, regenerating ablative). The categories doc's resolution is a two-part split: (1) a Defense **"adaptation" dial** where resistance *grows with exposure* — nature-soak that climbs each time a given nature hits (rides the same per-nature shield/armour soak vector the nature-tuning dials add), and (2) **Enhancers ▸ Systems self-repair** — regen a Health/armour pool over time (the same shape as shield regen, `ResolveShield`'s recharge tail, applied to hull). Both are ◐/⏳: the soak-vector plumbing is the nature-dial work above; the exposure-climb and hull-regen are the net-new terms. H2 is the hole this Defense door **owns half of** (adaptive shields/armour); Enhancers owns the self-repair half.

### §0g stamp — Reachable · Mirrored · Observable
- **Reachable (cradle-to-grave, ✅ for the built dials):** armour = mine ore → refine plate → `GroundArmorAtb`/ship armour design → install → spall off under fire (Toughness/`Combatant.Health` drops). Shields = research → build `ShieldAtb` generator → install → a shot-off generator drops the pool (`ShipCombatValueDB.cs:281`, the grave rung). Fortification = build the Bunker installation → captured with the region / destroyed by bombardment. Cultural Insulation = research → build the insulation hardware → lose it (◐, pending the `ExternalBeliefPressure` field).
- **Mirrored (§0g opponent-side):** because every Defense capability is a **component on a shared door**, the NPC mounting armour/shields/a bunker is the *same act* as the player — `ShipCombatValueDB.Calculate` and `GroundCombatant.ToCombatant` are faction-agnostic (`Combatant.FactionId` `CombatKernel.cs:90`), so an NPC fleet's shields soak and its ground units bounce swarms through the identical kernel. For Cultural Insulation the mirror is automatic: the same `ExternalBeliefPressure` input runs on the NPC's worlds, so **an NPC insulates its own worlds** against the player's influence exactly as the player insulates against theirs (the always-on mirror the pillar skeleton demands).
- **Observable (the gauges):** the **shield pool** is a live number (`Combatant.ShieldPool` / `FleetCombatStateDB.ShieldPool_J`) — show charge/max + the soak-absorbed per volley (`ResolveShield` returns `absorbed`). The **armour soak** gauge is damage-blocked-per-source (the `ArmourSoak` delta). Fortification's gauge is the `DefenseMult` shown on the region. Cultural Insulation's gauge is the **legitimacy readout** with the pressure-in vs insulation-resisted delta (both sides see it). Missing gauges are cheap Failure-A (the number exists, just unwired) — except the armour-nature and belief-pressure deltas, which need their field built first (Failure-B).

### Cross-category shared state (Prime Directive)
- **Defense × Weapons — the matchup (the load-bearing coupling).** Shields read `WeaponProfile.Nature` via `ShieldSoakFraction` (`CombatKernel.cs:133`); the armour-nature dial and the weapons-side `Penetration` extension (anatomy §4 #1) are **two halves of one rock-paper-scissors** — you cannot tune one without the other. Kinetic ▸ armour's friend / shield's bane; Energy ▸ the reverse; Exotic ▸ ignores shields (0.0). Any change to the nature table or the fire-mix touches both categories.
- **Hardening × Hazards.** `HazardResistanceAtb` (`Hazards/HazardResistanceAtb.cs`) is consumed by `SpaceHazardProcessor`/SensorScan/WarpMove/ground `EnvResistance` — the combat resolver never reads it. Hardening's real blast radius is the Hazards subsystem, not Weapons.
- **Hardening × Influence (Cultural Insulation).** Shares state with the **Colonies/legitimacy** system: `LegitimacyDB`/`LegitimacyInputs` (`Colonies/LegitimacyDB.cs`), `LegitimacyProcessor` (rebellion trigger), and the Espionage `sow-unrest` action (the covert twin that feeds the *same* legitimacy input). A change to `ExternalBeliefPressure` touches Influence, Espionage, and Government/rebellion at once.
- **Defense × Propulsion (mass).** Armour mass → `MassVolumeDB` → Evasion (`CalculateEvasion`) → the closing/dodge surface — thicker plate lowers evasion, a genuine cross-category cost.
- **Shields × Power.** Shield capacity/regen draw power (the generator's build-side cost) — a big shield bites the Power doors, not the salvo.
- **Fortification × Ground war-map.** `TileFootprint` (`GroundFootprintAtb.cs:31`) feeds the capture/bombard war-map (`GroundBuildings.cs`) — a fort is both a combat bonus and a targetable objective.

### Holes owned/resolved (H2) → status + home
- **H2 (Adaptive / self-repairing defense) — Defense owns the ADAPTIVE half; ⏳ partially resolved, home named.** Resolution: a Defense **"adaptation" dial** (per-nature soak that *climbs with exposure*) rides the same per-`Combatant` nature-soak vector introduced by the armour-nature + shield-nature-tuning dials above (◐ — depends on that vector existing). The **self-repairing half** (regrow hull/armour) is delegated to **Enhancers ▸ Systems self-repair** (a pool-regen term, same shape as `ResolveShield`'s recharge). Both blocked on the parked **degraded-condition / per-component-damage** model (the v1 resolver is whole-ship removal, anatomy §1), which is also the home of the Hardening ▸ redundancy/damage-control dial. **Status: designed, not built; owner = this Defense category (adaptive) + Enhancers (self-repair); prerequisite = per-component partial-damage state.**
- No other §5 holes owned. Fortification is fully modelled/clean; Hardening's hazard core is modelled (a new resistance is JSON, not code); Shields is the most-modelled door in the category.

---

## §6 — Enhancers

Enhancers is the **force-multiplier layer — what makes a veteran ≠ a conscript.** It's one of the two genuinely **net-new categories** (the categories doc: "net-new otherwise"), and the honest headline is: *most of it isn't built yet.* Weapons/Propulsion/Sensors/Power/Defense were largely "expose + wire what exists"; Enhancers is largely "here is the layer the game is missing, defined cleanly so it can be built." That's the right job for a design doc — name the gap precisely, not paper over it.

**The load-bearing thing this category settles — the GEAR-vs-BEING boundary (hole H4).** The designer makes **gear**: a buildable component (an `IComponentDesignAttribute` the assembler/resolver reads) that you research → build → mount → lose. It does **not** make the *being* that uses the gear. Innate ability — a species trait, raw talent, veterancy earned in blood, "the Force" — is **not a component**; it lives in the **People / crew / species / morale** system. Enhancers is the **buildable bridge** across that line (power armour, a training program, an AI co-pilot — all things you *build and install*), but the line itself is firm: a Jedi's telekinesis is a pilot trait, not a part in the store. This category is where that boundary gets drawn, because it's the category most tempted to cross it.

> **The boundary, concretely (from the engine survey):**
> - **Buildable Enhancer (this category):** carries an `IComponentDesignAttribute` the assembler reads — today that's `GroundAugmentAtb` (Strength/Evasion/Toughness/Shield). Cradle-to-grave.
> - **People/species trait (NOT the store):** `SpeciesDB` (environmental tolerances only — **zero combat trait today**), `CommanderDB.Experience` (written by the academy, **never read by combat**), `BonusesDB` (has research/mining categories, **no combat category**). These are innate/attached-to-a-being and currently inert for combat — the home for veterancy/pilot-skill/species-talent, none of which is a component.

**The §0f multi-consumer angle — Enhancers multiply ANY task, not just combat.** A power-armoured marine hits harder (combat); a bio-augment lets a colonist work in high-G or vacuum (labour); a training program makes a survey crew scan faster (exploration); an AI system runs a factory with less crew (industry). So even though the examples read military, the category feeds the whole game — the same reason it's worth building.

### §6.0 Shared enhancer dials (all three doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **What it boosts** | which stat/task it multiplies — attack/evasion/toughness (combat) OR survey/build/research speed (labour) |
| **Magnitude** | how big the multiplier — mild + cheap vs radical + costly |
| **Host** | who wears/runs it — a soldier (ground), a ship's crew (net-new), a facility's workforce |
| **Cost / side-effect** | the price — mass, power, upkeep, a drug crash, a medical/ethical cost |
| **Grave rung** | destroyed/killed = the multiplier is gone (a shot-off exo, a dead veteran, a fried computer) |

### 6.1 Enhancers ▸ BIO-AUGMENTATION  🔒 *locked*
*Upgrade the body — power armour, reflex boosters, combat drugs, gene-mods, cybernetics. The one door in this category that's **actually built** (ground-side): everything from a stim to a power-armour exoskeleton to a full conversion-cyborg falls out of these dials, and power-armour + reflex-booster are buildable in the base mod today.*

**The core decision — HOW FAR to push the body, against cost and side-effect.** A mild augment is cheap and clean; a radical one (combat drugs, heavy cybernetics) is a huge boost that costs money, medical/ethical price, or a crash afterward. You pick which attribute to raise and how hard to push it.

**A. Augment type — what it boosts (maps to the real `GroundAugmentAtb` fields)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Strength** (myomer/servo) | more carry-budget + melee/heavy-weapon capacity | bulky; a strength rig doesn't help you dodge |
| **Reflex** (booster/wired) | **raises evasion** — the hardest-to-hit operative | fragile once hit; expensive neural work |
| **Constitution** (subdermal/organ) | **more HP** (a toughness multiplier) | heavy; slows you |
| **Ward** (personal shield) | a personal energy shield (the depleting pool, at unit scale) | power/mass; the shield-nature matchup still applies |

**B. Magnitude — mild ↔ radical**
| Option | Why | Catch |
|--------|-----|-------|
| **Mild** | cheap, clean, no downside | modest edge |
| **Radical** | huge multiplier (power armour, heavy cyber) | costly; ⏳ side-effects (a drug's crash, cyber-rejection) are net-new |

**C. Delivery — worn ↔ implanted ↔ chemical**
| Option | Why | Catch |
|--------|-----|-------|
| **Worn (exo/power armour)** | removable, transferable — adds mass | can be shot off (the grave rung) |
| **Implanted (cybernetic/gene)** | permanent, no external mass | medical cost; can't un-install |
| **Chemical (combat drugs)** | a cheap temporary spike | ⏳ temporary + a crash afterward (net-new timing) |

**D. Host — soldier (built) vs ship-crew (net-new)**
| Option | Why | Catch |
|--------|-----|-------|
| **Personnel (ground)** | live today — `GroundAugmentAtb` on a ground unit | ground-only |
| **Ship crew** | augmented crew = a better-fought ship | ⏳ **net-new** — no ship-crew augment exists |

**Modellability audit (§0d — the one mostly-Modelled door in the category):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Strength | ✅ | `GroundAugmentAtb.StrengthBonus` → carry-budget (`GroundUnitAssembly:72`) |
| Reflex / evasion | ✅ | `GroundAugmentAtb.EvasionBonus` → `GroundUnit.Evasion` (`GroundUnitAssembly:108`) |
| Constitution / toughness | ✅ | `GroundAugmentAtb.ToughnessBonus` → HP multiplier `HitPoints *= 1 + toughness` (`GroundUnitAssembly:110,129`) — **Modelled** (a Defense-doc note called it "dead"; that was a **false positive** — it's read and applied, just not surfaced as a standalone stat) |
| Ward / personal shield | ✅ | `GroundAugmentAtb.Shield` → `GroundUnit.Shield` (the depleting pool) |
| Magnitude / side-effects | ◐ **wire** / ⏳ | the bonus scales ✅; a drug **crash / temporary** timing is ⏳ net-new |
| **Ship-crew augment** | ⏳ **defer** | augmentation is **ground-only**; a space bio-augment (crew-quality) is net-new |

**Reading:** Bio-augmentation is **real and buildable today on the ground** — all four `GroundAugmentAtb` fields are read into the unit, and power-armour + reflex-booster ship in the base mod. The design work is (a) **surface `ToughnessBonus`** as a visible stat (it's Modelled, just invisible — the false-positive lesson), (b) add **combat-drug timing** (temporary spike + crash — net-new), and (c) a **ship-crew augment** (the space twin — net-new). Cradle-to-grave is closed on the ground: research → build the augment → install on a unit → lose it when the unit dies.

**Numbers:** `StrengthBonus`/`EvasionBonus`/`ToughnessBonus`/`Shield` (the `GroundAugmentAtb` fields, health-scaled). Reference: base-mod `reflex-booster` (evasion) + `power-armor` (strength+toughness).

**Preset coordinates:** reflex booster (evasion) · power armour (strength+toughness) · combat stims (temporary, ⏳) · personal shield generator (ward) · conversion cyborg (radical, all-round) · **labour exo** *(a civilian strength rig — work in high-G/vacuum, the §0f non-combat use)*.

### 6.2 Enhancers ▸ TRAINING / DOCTRINE (Unit Caliber)  🔒 *locked*
*The **unit QUALITY / caliber** dial — the thing that stamps "elite" onto a unit at design time. This is what lets you build a **Space Marine** instead of a Guardsman, an **ace** instead of a rookie, the **Millennium Falcon** instead of a stock freighter — the same chassis and gear, but a higher-caliber crew that hits harder, dodges better, and holds under fire. It is **not the commander** (a formation/fleet leader is a separate thing that buffs from above) and **not the gear** (armour/weapons/augments are other doors); caliber is **intrinsic to the unit** and stacks with both. Mostly net-new today — but the reframe makes it a clean build.*

**The core decision — WHAT CALIBER of unit.** You choose a **quality tier** when you design the unit and pay for it upfront: a conscript is cheap and plentiful; an elite is a big across-the-board multiplier but **expensive and FEW** (elites draw from a scarce talent pool — you can't field an army of Space Marines). That single choice is what separates two units built from the *identical* chassis + weapons + armour. Caliber can also be **earned** (a green unit that survives becomes a veteran) or **trained** (an academy program) — but the primary lever is the design-time quality dial, the same way armour thickness is a design choice.

> **Why it's the unit, not the commander (your call, and the right one):** a Space Marine squad is elite whether or not an officer is attached; the Falcon flies like the Falcon because of Han + Chewie's skill and its tuning, not because an admiral is in the system. So caliber lives **on the unit's own design**, read by the resolver directly. A commander is a *separate* modifier (the Command category) that stacks on top — "they have a commander sure, but it's the training that tells them apart."

> **The H4 line, held (the Jedi caveat):** this dial builds the **elite-warrior** part of a Jedi — the reflexes, the lightsaber skill, the discipline (a very high quality tier). The **supernatural** part — telekinesis, precognition, the Force itself — is a *being-trait*, and per the gear-vs-being boundary it lives in People/species, **not** here. So "a Jedi" is *mostly* this dial (elite martial caliber) + a lightsaber (Weapons ▸ Melee) + a small being-trait for the Force. We build the warrior; the Force stays a trait.

**A. Quality tier — the caliber (the core, design-time dial)**
| Option | Why pick it | The catch (anti-dominance) |
|--------|-------------|----------------------------|
| **Conscript** | cheap, plentiful — mass you can spend | worse at everything; folds against elites |
| **Regular** | a solid, affordable baseline | outclassed by veteran+ formations |
| **Veteran** | a strong multiplier — the professional force | costly; draws trained manpower |
| **Elite** (Space Marine / ace) | the **best** — hits, dodges, and holds far above baseline | **expensive AND scarce** (a small talent pool); irreplaceable if lost (the grave rung bites hardest) |

**B. Skill focus — what the caliber sharpens**
| Option | Why | Catch |
|--------|-----|-------|
| **Gunnery** | raises accuracy/attack — lands more fire | narrow — a crack shot who can't dodge |
| **Evasion / survival** | dodges better, holds under fire (discipline) | doesn't hit harder |
| **Operations** (non-combat) | faster survey / build / research — the §0f labour use | no combat benefit |

**C. Source — how a unit reaches its caliber**
| Option | Why | Catch |
|--------|-----|-------|
| **Designed elite** (pay upfront) | stamp the tier at design time — the Space Marine chapter, the crack regiment, the ace squadron | costs money + **scarce talent**; the primary lever |
| **Combat-earned** | free — a green unit that survives climbs the tiers | slow, and you risk the unit to level it |
| **Academy-trained** | deliberate production of high-caliber units/officers | ⏳ the `NavalAcademy` produces officers today but they have no combat effect yet |

**Modellability audit (§0d — net-new, but a CLEAN net-new: one design-time multiplier, not an XP engine):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Quality tier (design-time)** | ⏳ **net-new (clean)** | a single **`Quality` multiplier field** on the unit design (`GroundUnit` / `ShipCombatValueDB`) → the resolver multiplies hit/evasion by it. Simpler than earned-XP: it's a static stat baked at build, read like armour thickness. **Applies to ships (crew caliber — the Falcon) AND ground (the Space Marine).** |
| Skill focus | ⏳ **defer** | which stat the tier weights (attack vs evasion) — rides the quality field |
| Combat-earned | ⏳ **defer** | `CommanderDB.Experience` ticks up but is **never read**; a per-unit XP accrual that raises `Quality` is the harder second path |
| Academy (production) | ✅ (production) / ⏳ (effect) | `NavalAcademyAtb` graduates officers — **real**; but the officer→combat effect (`BonusesDB` has **no combat category**) is net-new |
| Scarcity cost | ◐ **wire** | elites draw a **scarce talent pool** — ties to the Manpower system (`ManpowerDB` talent pool already exists) so you *can't* mass-produce Space Marines |
| Operations (non-combat) | ◐ **wire** | a caliber bonus onto survey/build/research rates (the existing `BonusesDB` pattern) |

**Reading:** Reframed as **unit caliber**, this door is a *clean* net-new, not a vague one. The primary build is a **single `Quality` multiplier** stamped on a unit's design and read by the resolver — that alone makes a Space Marine ≠ a Guardsman and the Falcon ≠ a stock freighter, on the same chassis. It's simpler than an experience-grind system (that's the optional second source), and it already has an anti-dominance cost handle: **scarce talent** (the Manpower talent pool means elites are few, so quality is a real trade against quantity). The academy and `CommanderDB.Experience` are useful scaffolding for the *earned/trained* paths, but the door doesn't depend on them — the design-time quality dial is the spine. Distinct from **posture** (`FleetDoctrineDB`, the switchable stance — that's Command) and from the **commander** (a separate stacking modifier).

**Numbers:** `Quality` = a multiplier tier (e.g. conscript ×0.8 · regular ×1.0 · veteran ×1.25 · elite ×1.6) on the unit's hit/evasion, calibrated against `EvasionCap 0.95` and the `HitFraction` tracking term; cost + talent-draw scale with the tier. Cradle-to-grave: caliber is set at **design/build** (or earned), draws **scarce manpower**, and an elite lost in battle is **gone** — you re-raise from a small talent pool, which is exactly why losing a veteran formation *hurts*.

**Preset coordinates — the span (ground AND ship, the north-star units):**
| Unit | Tier | The point |
|------|------|-----------|
| **Conscript levy** | Conscript | cheap mass — quantity over quality |
| **Line regular** | Regular | the affordable baseline army |
| **Space Marine** | Elite (+ Bio-augmentation) | design-time elite caliber + power armour/gene-mods (door 6.1) — the two Enhancer doors stacked |
| **Ace squadron** | Elite (gunnery/evasion) | a few pilots worth a wing of rookies |
| **Millennium Falcon** | Elite (ship crew) | crew caliber on a freighter chassis — "she's got it where it counts" |
| **Veteran survey crew** *(civilian)* | Veteran (operations) | faster scans — the §0f labour use |

### 6.3 Enhancers ▸ SYSTEMS  🔒 *locked*
*Let the machine help — a targeting computer, a battle-management AI, an astromech co-pilot, an automation suite. Multiplies what the crew/unit can do without changing the body or the training. **Essentially net-new for the live combat engine:** the auto-resolver derives hit chance purely from weapon-vs-evasion — there is no computer term — and the one existing piece (fire-control) feeds only the parked per-pixel sim.*

**The core decision — ASSIST or REPLACE the crew.** A system either helps a crew do better (a targeting computer sharpens their fire) or does the job *instead* of a crew (an AI runs the ship / an automated factory). The first is a force-multiplier; the second bleeds into crewless operation (a Command/People question).

**A. System type — what it multiplies**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Targeting computer** | raises **hit chance** (the accuracy term) — lands fire on dodgers | ⏳ the auto-resolver has no computer term; overlaps the Sensors ▸ Fire-Control wire |
| **Battle-management** | **coordinates** the fleet/formation — a firepower/focus multiplier | ⏳ net-new — no coordination term |
| **AI co-pilot** (astromech) | reaction/evasion assist — the R2 slot | ⏳ net-new; a crew-slot filled by a machine |
| **Automation** (non-combat) | run with **less crew** / faster industry — the §0f efficiency | ◐ wire onto the crew/industry systems |

**B. Level — basic → advanced AI**
| Option | Why | Catch |
|--------|-----|-------|
| **Basic** | cheap assist | modest |
| **Advanced AI** | big multiplier, low crew | costly, high-tech; ⏳ an autonomous AI raises the crewless/Command question |

**Modellability audit (§0d — the door with the least engine hook):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Targeting computer (hit chance) | ⏳ **defer** | the auto-resolver's `HitFraction` has **no computer term** (pure weapon-vs-evasion); `BeamFireControlAtbDB` exists but feeds the **parked** per-pixel sim, not the resolver — **overlaps the Sensors ▸ Fire-Control wire** (build once, both doors light up) |
| Battle-management / coordination | ⏳ **defer** | no firepower-coordination term in the resolver |
| AI co-pilot | ⏳ **defer** | no crew-slot / reaction model the resolver reads |
| Automation (non-combat) | ◐ **wire** | a "runs with less crew / faster industry" bonus onto the manpower/industry systems (the `BonusesDB` pattern) |

**Reading:** Systems is **essentially net-new for combat** — its whole premise (a computer that makes you fight better) has no hook in the auto-resolver, which decides hits from weapon specs vs evasion alone. Its one foothold, **fire-control**, lives on the parked per-pixel sim and is the *same* dead-knob wire as Sensors ▸ Fire Control — so a **targeting-computer term in `HitFraction`** is the shared build that lights up both. The cheapest live win here is the **non-combat** half: automation as a crew-reduction / industry-speed bonus, which rides the existing bonus pattern. Honest verdict: the combat side is a build item, the labour side is a wire.

**Numbers:** a computer's accuracy/coordination bonus → the `HitFraction` tracking term + a firepower coefficient (net-new terms, calibrated once built). Cradle-to-grave: a system is a **component** (research the AI/computer tech → build → install → fried when the ship's hit).

**Preset coordinates:** targeting computer · battle-management AI · astromech co-pilot · automation suite (crewless efficiency, ◐) · tactical network · **factory AI** *(faster industry — the §0f use)*.

---

## ✅ §6 Enhancers — COMPLETE (3/3 doors locked)
Bio-augmentation 🔒 · Training/Doctrine (Unit Caliber) 🔒 · Systems 🔒. **This is the honest NET-NEW category** — the "veteran ≠ conscript" force-multiplier layer the game mostly lacks; the design job is to define it cleanly and hold the gear-vs-being line, not pretend it's built. Headline readings: **Bio-augmentation is the one mostly-Modelled door** (`GroundAugmentAtb` — strength/evasion/toughness/shield all read; power-armour + reflex-booster buildable today; ship-crew augment is the net-new twin) — *and it corrects a Defense-doc false positive: `ToughnessBonus` is Modelled (HP multiplier), not dead.* **Training/Doctrine is the unit-CALIBER door** (reframed per the developer): a **design-time quality tier** that stamps "elite" onto a unit — a **Space Marine** vs a Guardsman, an **ace** vs a rookie, the **Millennium Falcon** vs a stock freighter, on the *same* chassis and gear. It's **the unit's own stat, not the commander** (a separate stacking modifier) and not the gear (other doors). Net-new but **clean**: the spine is a single `Quality` multiplier field on the unit design, read by the resolver (hit/evasion) — simpler than an XP engine, with anti-dominance built in (elites draw a **scarce talent pool**, so you can't mass-produce Space Marines). Combat-earned XP + the academy are optional secondary sources. Holds the H4 line on Jedi: this builds the elite *warrior*; the *Force* stays a People being-trait. **Systems is essentially net-new** — the auto-resolver has no computer term (hit chance is pure weapon-vs-evasion), and its one foothold (fire-control) lives on the parked sim + overlaps the Sensors ▸ Fire-Control wire. **The load-bearing decision is the BOUNDARY (H4):** buildable **gear** (an `IComponentDesignAttribute` the resolver reads — `GroundAugmentAtb`) is this category; innate **being** (species trait, raw talent, the Force) lives in **People/species/crew** — and `SpeciesDB` has **zero combat trait today**, so even that is net-new, and it stays **out** of the component store. Build-list: (1) surface `ToughnessBonus` (Modelled, invisible) — **done: Defense-doc corrected**; (2) ship-crew bio-augment (the space twin); (3) combat-drug timing (temporary + crash); (4) a per-unit **`Quality` multiplier** field (the caliber spine — makes Space Marine ≠ Guardsman, Falcon ≠ freighter) read by the resolver + drawn from the scarce **talent pool** (`ManpowerDB`); (5) optional secondary paths — combat-earned XP raising `Quality`, and a combat `BonusCategory` for officer/commander effect; (6) a targeting/battle-computer term in `HitFraction` (Systems, shares the FC wire); (7) hold the line — innate traits (the Force) go to People, not here.

---

## ⚙ 6 — ENHANCERS · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Enhancers is the **force-multiplier layer — what makes a veteran ≠ a conscript.** Three doors: **Bio-augmentation** (upgrade the body — power armour, reflex boosters, gene-mods), **Training/Doctrine = Unit Caliber** (stamp "elite" onto a unit at design time — Space Marine ≠ Guardsman, Falcon ≠ freighter), and **Systems** (let the machine help — targeting computer, battle-AI, automation). Bio-augmentation is the one **mostly-built** door (ground-side); the other two are **net-new but clean**. The load-bearing rule the whole category enforces is the **GEAR-vs-BEING boundary (hole H4)**: the designer builds buildable **gear** (an `IComponentDesignAttribute` the assembler/resolver reads); it does NOT build the innate *being* (species trait, veterancy, the Force) — that lives in the People/species/crew system and stays OUT of the component store.

### Pillar tags (§0h)
- **Pillar:** **Military · Modifier** — Enhancers is a *force-multiplier on units*, NOT a projector itself. It has no `WeaponProfile`, launches nothing, holds no reach/cadence of its own. It multiplies the numbers OTHER doors' projectors/counters feed into (attack, evasion, toughness, hit-fraction) — so it carries **neither a PROJECTION slot nor a COUNTER slot** (§0h PROJECTOR⛓COUNTER). It is graded against the **combat auto-resolver** (`CombatEngagement`/`ShipCombatValueDB`) for its combat half, and against the **industry/economy + detection loops** for its §0f non-combat half (a labour exo, a factory-AI, a faster survey crew).
- **skeleton-role:** none (Modifier, not Projection/Counter/Medium/Detection). The tag on an Enhancer component is `pillar=Military`, `skeleton-role=Modifier`.
- **Multi-consumer (§0f):** every door multiplies ANY task, not just combat — combat (marine hits harder), labour (bio-augment works in high-G/vacuum), exploration (trained survey crew scans faster), industry (AI runs a factory with less crew).

### Complete dial → derived-stat → engine-wire table (VERIFIED)

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | Resolver insertion |
|------|------|--------------|-------|--------------------------|--------------------|
| **6.1 Bio-augment** | Strength (myomer/servo) | carry-budget +N | ✅ Modelled | `GroundAugmentAtb.StrengthBonus` (`GroundCombat/GroundAugmentAtb.cs:30`) → `capacity += StrengthBonus*c` (`GroundCombat/GroundUnitAssembly.cs:74`) | assembler pass-1: augments raise the frame's carry budget → gates what gear the unit can mount |
| **6.1 Bio-augment** | Reflex (booster/wired) | evasion +N | ✅ Modelled | `GroundAugmentAtb.EvasionBonus` (`GroundAugmentAtb.cs:32`) → `r.Evasion += EvasionBonus*c` (`GroundUnitAssembly.cs:108`) | feeds `GroundUnit.Evasion` → dodge term in the ground hit-resolve (same evasion currency as ship dodge) |
| **6.1 Bio-augment** | Constitution (subdermal/organ) | HP multiplier ×(1+t) | ✅ **Modelled** (NOT dead — Defense-doc false positive) | `GroundAugmentAtb.ToughnessBonus` (`GroundAugmentAtb.cs:34`) → accumulate `toughness += ToughnessBonus*c` (`GroundUnitAssembly.cs:110`) → applied once `if(toughness!=0) r.HitPoints *= 1 + toughness` (`GroundUnitAssembly.cs:129`) | multiplies the whole HP pool (frame + armour), order-independent. **Modelled-but-not-surfaced** as a standalone stat — the only open work is a UI readout |
| **6.1 Bio-augment** | Ward (personal shield) | depleting shield pool | ✅ Modelled | `GroundAugmentAtb.Shield` (`GroundAugmentAtb.cs:36`) → `r.Shield += g.Shield*c` (`GroundUnitAssembly.cs:109`) → `GroundUnit.Shield`/`CurrentShield` | flat incoming-damage soak at unit scale; shield-nature matchup still applies |
| **6.1 Bio-augment** | Magnitude (mild↔radical) | bonus scale | ◐ Wire | bonus scales ✅; a **drug crash / temporary timing** is ⏳ net-new (no timed-effect model) | — |
| **6.1 Bio-augment** | Host: ship-crew augment | crew quality | ⏳ Defer (net-new) | augmentation is **ground-only** — no ship-crew `GroundAugmentAtb` twin exists | the space twin lands on the same `Quality`-mult resolver hook below |
| **6.2 Unit-Caliber** | **Quality tier** (conscript/regular/veteran/elite) — the CORE design-time dial | a single **`Quality` multiplier** stamped on the unit design | ⏳ **net-new (clean)** | NO `Quality` field exists yet on `GroundUnit` or `ShipCombatValueDB` (grep confirms zero `Quality` in `Combat/` + `GroundCombat/`). The spine to add: one static multiplier field baked at build | **THE insertion point:** multiply per-ship `cv.Firepower` and `cv.Toughness` by `Quality` **exactly where the doctrine mult already lands** — `CombatEngagement.cs:707-713` (`CombatShip.FirepowerMult`/`ToughnessMult` struct), applied at `:1033`/`:1041` (firepower × `FirepowerMult`) and `:744` (`EffToughness = cv.Toughness * cs.ToughnessMult`). A unit `Quality` rides the identical multiplier slot |
| **6.2 Unit-Caliber** | Skill focus (gunnery/evasion/operations) | which stat the tier weights | ⏳ Defer | rides the `Quality` field (attack-vs-evasion weighting) | same slot, weighted |
| **6.2 Unit-Caliber** | **Scarcity cost** (elites draw a scarce talent pool) | talent drawn per elite | ◐ **Wire** | `ColonyManpowerDB.TalentPool(pop)=pop×0.005` (`Colonies/ColonyManpowerDB.cs:27,48`); `AvailableTalent` (`:54`), `CanCommitTalent` (`:57`), `CommitTalent`/`ReleaseTalent` (`:61-62`) — **purpose-built for "officers/scientists/governors," ZERO consumers today** | the anti-dominance handle: building an elite commits scarce talent → can't mass-produce Space Marines |
| **6.2 Unit-Caliber** | Source: combat-earned XP | `Quality` climbs on survival | ⏳ Defer | `CommanderDB.Experience` (`People/CommanderDB.cs:21`) is **written by the academy, never read by combat** — a per-unit XP accrual is the harder second path | — |
| **6.2 Unit-Caliber** | Source: academy-trained | academy graduates officers | ✅ (production) / ⏳ (combat effect) | `NavalAcademyAtb` design dials `ClassSize`/`TrainingPeriodInMonths` (`People/NavalAcademyAtb.cs:11-12`); `NavalAcademyProcessor` rolls `ExperienceCap` on `NextBellCurve` (`:31`) + `Experience` (`:42`) — **real production**; the officer→combat effect is net-new (`BonusesDB` has **no combat category**) | — |
| **6.2 Unit-Caliber** | Operations (non-combat) | survey/build/research +% | ◐ Wire | a caliber bonus onto the existing `BonusesDB` pattern (`People/BonusesDB.cs:43-47`) | rides the rung-4 wire below, pointed at industry/survey rates |
| **§E1a shared** | **"a person's skill modifies an outcome"** wire (Command's rung 4) | officer `BonusesDB` → a real combat number | ⏳ build-once | **the copyable pattern:** `ResearchProcessor.RefreshPointModifiers` (`Tech/ResearchProcessor.cs:246`) folds the seated officer's `BonusesDB` into a `ModifiableValue` by `FilterId` match (`:295` reads `scientist.BonusesDB`, `:302` matches `currentTech.Category == bonus.FilterId`, `:304+` `AddModifier`); target is `ResearcherDB.PointsPerDay` = `ModifiableValue<int>` (`Tech/ResearcherDB.cs:16`) | copy this shape to fold a commander's combat-`FilterId` `BonusesDB` into `FleetDoctrine.FirepowerMult`/`ToughnessMult` (`Combat/FleetDoctrine.cs:16-17`) — the **commander** side that STACKS on the **unit** `Quality` side. ONE hook lights BOTH §6.2 elites and Command competence |
| **§E1a shared** | **Academy talent draw + competence generator** | graduate `BonusesDB` gets a rolled value + commits talent | ◐ Wire | academy rolls `NextBellCurve` today (`NavalAcademyProcessor.cs:31`) but writes only an `Experience` int → the graduate's `BonusesDB` is **empty** (`People/CommanderFactory.cs:24`, `blobs.Add(new BonusesDB())`); wire = write the roll into `BonusesDB` (rung-4 reads it) + draw scarce `ColonyManpowerDB.TalentPool` | same talent handle gates §6.2 elites — build once, both light up |
| **6.3 Systems** | Targeting computer | hit-chance +N | ⏳ Defer | the auto-resolver's hit-fraction is pure weapon-vs-evasion — **no computer term**; `BeamFireControlAtbDB` feeds only the **parked** per-pixel sim | overlaps the Sensors ▸ Fire-Control wire — a targeting term added to `HitFraction`/`LandedFraction` lights both doors |
| **6.3 Systems** | Battle-management / coordination | firepower/focus mult | ⏳ Defer | no firepower-coordination term in `CombatEngagement` | — |
| **6.3 Systems** | AI co-pilot (astromech) | reaction/evasion assist | ⏳ Defer | no crew-slot/reaction model the resolver reads | — |
| **6.3 Systems** | Automation (non-combat) | run with less crew / faster industry | ◐ Wire | a crew-reduction / industry-speed bonus onto the manpower/industry systems via the `BonusesDB` pattern (`BonusesDB.cs:43-47`) | the cheapest live win in this door |

### Resolver insertion points

**1. The unit `Quality` multiplier lands where the doctrine mult already lands — a proven, per-ship slot (no new resolver).**
The auto-resolver already carries a per-ship firepower/toughness multiplier: the `CombatShip` struct (`Combat/CombatEngagement.cs:707-713`) holds `FirepowerMult` + `ToughnessMult`, collected per-ship from the fleet's doctrine at `CombatEngagement.cs:1220-1221` (`FleetDoctrine.FirepowerMult(fleet)` / `ToughnessMult(fleet)`). Those multipliers are applied:
- **Firepower:** `CombatEngagement.cs:1033` (`w.DamagePerSecond * cs.FirepowerMult`) and `:1041` (fallback `cv.Firepower * cs.FirepowerMult`).
- **Toughness:** the casualty bucket key includes `ToughnessMult` (`:736`) and `EffToughness = cv.Toughness * cs.ToughnessMult / landed` (`:744`).
A unit's `Quality` is the SAME shape — a scalar that multiplies `cv.Firepower` / `cv.Toughness` (and weights evasion) per unit. Insert it by folding `Quality` into `FirepowerMult`/`ToughnessMult` when the `CombatShip` is constructed, or by multiplying `cv.*` alongside. Because the bucket key already keys on `ToughnessMult`, distinct-caliber units naturally split into their own buckets — the O(buckets) aggregation (§0i, count-resolvers) still holds, no per-unit cost. This is **DATA for the existing combat loop, not a new loop** (§0i / §0E1c).

**2. The BonusesDB→outcome wire — the ONE hook that lights §6.2 elites AND Command competence (build once).**
This is the "a person's skill modifies an outcome" wire, designed as Command's **rung 4**. The unit `Quality` (spine above) is the unit's OWN stat; the **commander** is a separate, stacking modifier — but both land through the same machinery. Copy the ONLY working instance of the pattern:
- **The research pattern to copy (`Tech/ResearchProcessor.cs:246` `RefreshPointModifiers`):**
  1. Target is a `ModifiableValue<T>` — the number the leader moves (research: `ResearcherDB.PointsPerDay`, `Tech/ResearcherDB.cs:16`).
  2. `RefreshPointModifiers` clears + re-folds modifiers in priority order: base → funding (`:252` `ClearAllModifiers`, funding multiplier `:255+`) → the seated officer's `BonusesDB`, matched by `FilterId` (`:295` reads `scientist.BonusesDB`, `:302` `currentTech.Category.Equals(bonus.FilterId)`, `:304+` `AddModifier`).
  3. `GetValue()` reads the folded result.
- **The combat analogue:** make `FleetDoctrine.FirepowerMult`/`ToughnessMult` (`Combat/FleetDoctrine.cs:16-17`, today reading `FleetDoctrineDB`) into a `ModifiableValue`, and fold the seated commander's `BonusesDB` bonuses whose `FilterId` = a combat category. **This requires adding a combat `BonusCategory`** — today the enum is `None/ResearchPoints/ResearchCosts/Mining` only (`People/BonusesDB.cs:7-13`), zero combat category. Add e.g. `Firepower`/`Toughness`/`Accuracy`; the `Bonus.FilterId` free-string field (`:31`) already routes it.
- **The payoff (Prime Directive):** the unit `Quality` (spine #1) and the commander bonus (this wire) both terminate at `FirepowerMult`/`ToughnessMult` — they STACK. Build the rung-4 fold once and it lights §6.2's commander-effect row AND Command's whole competence layer (governor→legitimacy, admiral→fleet, minister→policy).

**3. The academy talent-pool / scarcity resolver.**
No combat resolver — the scarcity check is at *build time*: `ColonyManpowerDB.CanCommitTalent(pop, n)` (`Colonies/ColonyManpowerDB.cs:57`) gates whether an elite unit / high-competence officer can be produced; `CommitTalent` (`:61`) draws the pool (`TalentPool = pop × 0.005`, `:27,48`), `ReleaseTalent` (`:62`) returns it on death. This is the anti-dominance economy — the same handle for §6.2 elites and academy graduates.

### Prerequisite fixes & dead stubs (file:line)
- **The empty `BonusesDB` on academy graduates — the core build gap.** Every officer is created with `blobs.Add(new BonusesDB())` (`People/CommanderFactory.cs:24`) — an empty list. The academy rolls `NextBellCurve` but writes it only to `CommanderDB.Experience`/`ExperienceCap` (`People/NavalAcademyProcessor.cs:31,42`), which combat never reads. Fix: at graduation, roll `BonusesDB.Bonuses` values (reuse `NextBellCurve`) scaled by (design tier + investment) × populousness × dev level × teacher competence, and write them into the empty `BonusesDB` — the shape rung 4 already consumes.
- **The near-unreachable lab-competence default path.** The ONLY place a `BonusesDB` is ever given a real competence value is a hardcoded one-off in new-game setup: `NewGameMenu.cs:632-638` (`Pulsar4X.Client/Interface/Menus/NewGameMenu.cs`) adds a single `Bonus(…, 0.1, …, BonusCategory.ResearchPoints)` to the starting scientist. There is **no competence generator** — outside that one hardcode, every officer/scientist runs on an empty `BonusesDB`, so the rung-4 machinery has almost nothing to fold. Building the generator (above) is what makes the default path reachable.
- **`CommanderDB.Experience` written-never-read by combat** (`People/CommanderDB.cs:21,23`) — scaffolding for the earned/trained paths, inert until a consumer reads it.
- **No `Quality` field exists** on `GroundUnit` / `ShipCombatValueDB` (grep: zero `Quality` in `Combat/`+`GroundCombat/`) — the §6.2 spine is a net-new field to add (then wire into the mult slot above).
- **No combat `BonusCategory`** (`People/BonusesDB.cs:7-13`) — must be added before a commander bonus can route to a combat number.
- **`GroundAugmentAtb` is inert on install** (`OnComponentInstallation`/`OnComponentUninstallation` are empty, `GroundAugmentAtb.cs:53-54`) — correct by design; the *assembler* reads the fields at build time (`GroundUnitAssembly.cs:74,108-110,129`), not on install.

### Design essence captured inline (rung-4 competence + the gear-vs-being boundary H4)
**Rung 4 — "a person's skill modifies a real number."** A leader's competence is not why a seat exists; it is the dial on *how well* the seat's decision executes (a master governor keeps the queue full and morale up; a green one lets the colony drift). Mechanically it is proven in exactly one place — research — as a copyable three-step pattern: the target is a `ModifiableValue<T>`, a `Refresh…Modifiers()` method folds the seated officer's `BonusesDB` in by `FilterId`, and `GetValue()` reads the result (`ResearchProcessor.cs:246`, `ResearcherDB.cs:16`). The academy is the competence *generator*: one bell-curve roll at graduation writes the officer's inherent competence into the `BonusesDB` and commits a slice of the scarce talent pool — quantity vs quality is a design choice (big class + low mean = mass academy; small class + high mean = elite). **The gear-vs-being boundary (H4)** is the line this whole category walks: the designer builds buildable **gear** (an `IComponentDesignAttribute` the assembler/resolver reads — today `GroundAugmentAtb`, tomorrow a `Quality` field); the innate **being** — a species trait, veterancy earned in blood, "the Force," psionics — is NOT a component and lives in People/species/crew (`SpeciesDB` has zero combat trait today; `CommanderDB.Experience` is inert for combat). So "a Jedi" = elite martial **caliber** (§6.2 `Quality`) + a lightsaber (Weapons ▸ Melee) + a small Force **being-trait** in People — we build the warrior; the Force stays a trait, out of the store.

### §0g stamp
- **Reachable (cradle-to-grave, player-side):** ✅ for Bio-augmentation on the ground — research → build the augment → mount on a unit → the assembler reads its four fields (`GroundUnitAssembly.cs:74,108-110,129`) → the augmented unit fights → the unit dies and the augment is lost. ⏳ for §6.2 `Quality` and §6.3 Systems — the vertical chain is designed (design-time tier / researched computer → built → installed → destroyed) but the resolver field is net-new. Named mineral→material→component→research→unit→decision→loss chain is complete for augments; the missing rung for caliber is the `Quality` field itself.
- **Mirrored (NPC fields elites/veterans too):** ✅ cheap **because a capability is a component on a shared door** — an NPC mounting `GroundAugmentAtb` or stamping a `Quality` tier is the SAME act as the player doing it (no player-only code). The NPC's caliber/augment choice flows through the identical build path and lands in the same `FirepowerMult`/`ToughnessMult` resolver slot. Delegation = NPC AI: the same academy/talent-pool scarcity constrains the NPC's elites, so it cannot mass-produce Space Marines either.
- **Observable (the gauge, both sides):** the **caliber/competence readout** — the unit's `Quality` tier + effective firepower/toughness shown in the unit designer and combat readout; the officer's rolled `BonusesDB` competence shown on the commander; and the current gap: `ToughnessBonus` is Modelled but **not surfaced** as a standalone stat (`GroundUnitAssembly.cs:110,129`) — a cheap Failure-A (the number exists, just unwired to the UI). Building that readout is the first Observable task.

### Cross-category shared state (Prime Directive)
- **Enhancers × Command [the shared wire]:** both terminate at the SAME resolver multipliers (`FirepowerMult`/`ToughnessMult`, `CombatEngagement.cs:707-713,1220-1221`). Unit `Quality` (§6.2, the unit's own stat) and commander competence (§10, a stacking modifier) STACK there. The rung-4 fold (`ResearchProcessor.cs:246` pattern → `FleetDoctrine.cs:16-17`) is built ONCE and lights both. Shared state = the mult slot + the `BonusesDB` shape + the (net-new) combat `BonusCategory`.
- **Enhancers × Civic [academy / talent]:** the academy BUILDING is Civic ▸ Development (`NavalAcademyAtb`, `People/NavalAcademyAtb.cs`); its officers' combat EFFECT is consumed here. Both §6.2 elites and academy officers draw the SAME scarce `ColonyManpowerDB.TalentPool` (`Colonies/ColonyManpowerDB.cs:27,48,57,61`) — population is the shared resource that caps how many elites/officers exist. Build here, produce there.
- **Enhancers × Chassis [augment raises the budget]:** a Bio-augment's `StrengthBonus` raises the frame's carry budget (`GroundUnitAssembly.cs:74`, assembler pass-1) — the §0b "carry more" upgrade. Shared state = the chassis `BaseStrength` carry budget that the Personnel/Vehicle frame sets and the augment increases; overloading it invalidates the design.

### Holes owned/resolved (H4, H10) → status + home
- **H4 — Innate / pilot "magic"** (the Force, psionics, hand-device; `COMPONENT-DESIGNER-CATEGORIES.md` §5 line 88, boundary line 51). **Status: RESOLVED as a BOUNDARY, owned here.** This category draws the gear-vs-being line: buildable gear (`GroundAugmentAtb`, the net-new `Quality` field) is Enhancers; the innate part (species trait, veterancy, the Force) is **NOT a component** and lives in **People / crew / commander / species** (`SpeciesDB` — zero combat trait today; `CommanderDB` — `Experience` inert for combat). Home for the innate half: the People system. Enhancers is the buildable *bridge* across the line, never the crossing.
- **H10 — Autonomous / crewless / hive units** (Borg, Replicators, drones; §5 line 96). **Status: partially owned, mostly deferred.** The buildable half routes through **Enhancers ▸ Systems** automation → an AI that drives crew requirement toward zero (◐ Wire, onto the manpower/crew systems via the `BonusesDB` pattern). The **hive** half (many bodies sharing one mind, one span) is a **Command/formation variant** (shared span-of-control), not an Enhancer — home: the Command category. Both are ⏳ pending: the automation crew-reduction term and the shared-span Command variant are net-new.

---

## §7 — Industrial

Industrial is the **economic spine** — the front end of the whole game: **mine → refine → build**. Everything else in the designer is *made here*: a weapon, a reactor, a shield, a ship, a colony installation all come out of this category's output. It is the purest **non-combat** category (no combat consumer at all — §0f is trivially satisfied: Industrial *is* the backbone every other system draws on), and it's **mostly Modelled** — the extraction and fabrication loops both run live through daily processors, host-agnostic (a **colony OR a station** mines and builds identically). The honest work here isn't design gaps — it's a **cluster of real engine gaps** (a broken refining feed, missing gas harvesting, an inert slip-cap, two overlapping build paths) that the door-by-door audit surfaces.

**The yardstick — the production system** (`MineResourcesProcessor` + `IndustryProcessor`, both daily), not the combat resolver. Two distinct engine subsystems, which is exactly why there are two doors:
- **Extraction** = `MineResourcesAtbDB` → `MiningDB` → minerals pulled from a body's deposits. *Mining is NOT an industry type* — it's its own system.
- **Fabrication** = one `IndustryAbilityDB` / `IndustryJob` / `IndustryProcessor`, routed by **`IndustryTypeID`** (refining / component / installation / ordnance / ship-assembly). A refinery, a factory, and a shipyard are the *same ability* specialized to different type-rates.

### §7.0 Shared industrial dials (both doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **Output rate** | units/day (extraction) or points/day (fabrication) — the headline throughput |
| **What it makes / pulls** | the routing — which mineral (extraction) or which `IndustryTypeID` (fabrication) |
| **Input consumed** | resources drawn from `CargoStorageDB` each tick (fabrication) / deposit drawn down (extraction) |
| **Host** | colony OR station — **host-agnostic** (`MiningHelper.TryGetMiningBody`; industry ability on either) |
| **Mass / footprint** | scale the throughput → mass + a war-map footprint (a factory is a capture/bombard target) |

### 7.1 Industrial ▸ EXTRACTION  🔒 *locked*
*Pull raw resources out of the ground (or the sky). Mines, automated robo-miners, and — eventually — gas-giant skimmers. Everything from a hand-dug surface mine to a self-directing asteroid RoboMiner falls out of these dials; the one real hole is **gas/atmosphere harvesting**, which doesn't exist yet.*

**The core decision — WHAT to pull, HOW FAST, and WHETHER it can move.** A mine extracts minerals from a body's finite deposits; the deposit **depletes** and gets **harder to reach** (accessibility decays cubically as it empties — diminishing returns that push you to find fresh ground). You choose the extraction rate (bigger = faster but heavier), whether the miner is **planted** (a fixed mine) or **mobile** (an automine you drop on an asteroid and pick up later), and — the missing option — what *medium* you pull from (solid ore vs gas).

**A. Extraction rate — how fast it pulls (`ResourcesPerEconTick` / `MiningAmount`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Small** | light, cheap, low footprint | slow — a trickle of ore |
| **Large** | fast throughput — feeds a hungry industry | heavy + big footprint; **exhausts a deposit faster** (accessibility craters sooner) |

**B. Target resource — flat vs focused**
| Option | Why | Catch |
|--------|-----|-------|
| **Broad (all minerals)** | one mine pulls every mineral present (today's default) | no specialization — spread thin on a rich single-ore body |
| **Focused (per-mineral)** | pour capacity into the one rich/scarce ore you need | ◐ the rate field **is** per-mineral (`ResourcesPerEconTick`), but templates fill it **flat** — a focus dial is a small wire |

**C. Automation / mobility — planted vs mobile**
| Option | Why | Catch |
|--------|-----|-------|
| **Fixed mine** (`Area × 0.00001`) | cheap per unit of rate on a big body | can't relocate — planted where you built it |
| **Automine / RoboMiner** (`Size × 0.005`) | **transportable** (ShipCargo + PlanetInstallation) — drop on an asteroid, retrieve later; the belt-mining play | costlier per unit; smaller scale |

**D. Medium — solid vs gas (the missing option)**
| Option | Why | Catch |
|--------|-----|-------|
| **Solid (minerals)** | the built path — ore from a rocky body | — |
| **Gas / atmosphere** (skimmer) | harvest sorium/fuel from a gas giant or atmosphere — the Expanse/fuel play | ⏳ **MISSING** — no gas-harvest component/ability exists (net-new) |

**Modellability audit (§0d — mostly Modelled; one net-new medium):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Extraction rate | ✅ | `MineResourcesAtbDB.ResourcesPerEconTick` → `MiningDB.BaseMiningRate` → `MineResourcesProcessor` (daily): `actualRate = base × colonyBonus × accessibility` |
| Deposit depletion / accessibility | ✅ | `MineralsDB.MineralDeposit` (`Amount`, `Accessibility`); accessibility **decays cubically** as the deposit empties (the "move on" pressure) |
| Automine / mobility | ✅ | `automine` (RoboMiner) — same atb, `Size`-based rate, transportable mount |
| Host (colony/station) | ✅ | `MiningHelper.TryGetMiningBody` — a station mines its body exactly like a colony |
| Target resource (focus) | ◐ **wire** | the rate is per-mineral, but templates fill it flat — expose a per-mineral focus dial |
| **Gas / atmosphere harvest** | ⏳ **defer/net-new** | **no** gas/sorium harvester exists — a new extraction medium |
| Per-hex mining | ◐ **wire** | `HexMinerals` seeds deposits onto surface hexes but is **view-only**; the body-wide pool is still the source of truth |

**Reading:** Extraction is **live and solid** — the mine→cargo loop runs daily, deposits deplete with realistic diminishing returns, and it's host-agnostic (colony or station). The one genuine hole is **gas/atmosphere harvesting** (net-new — the gas-giant fuel play the north-star franchises lean on), plus two small wires (per-mineral focus; making the per-hex deposits the real source instead of a view). Cradle-to-grave is closed: the mine is a **component** (built → installed → mines → the deposit runs dry, so you re-survey and relocate).

**Numbers:** `MiningAmount` (units/mineral/day) — a 1M m² mine ≈ **10 units/mineral/day**, max ~1,000/day; automine `Size × 0.005`. Accessibility 0.1–1.0 (decays cubically). Consumer: the whole materials economy downstream.

**Preset coordinates:** surface mine (small, cheap) · deep/heavy mine (high rate) · **RoboMiner** (automine — asteroid/belt) · gas skimmer *(⏳ missing — the net-new one)* · **orbital mining station** *(host-agnostic — a station mines like a colony)*.

### 7.2 Industrial ▸ FABRICATION  🔒 *locked*
*Turn raw resources into everything — refine minerals into materials, manufacture components, construct installations, assemble ordnance, **assemble units (tanks / walkers / aircraft)**, and build ships. One production ability, routed by **industry type**: a refinery, a factory, a **vehicle foundry**, and a shipyard are the same machine specialized to different jobs. Mostly Modelled — with one load-bearing gap (the refining feed) and one **new facility the designer needs** (a dedicated unit-assembly bay, the ground/air twin of the shipyard).*

**The core decision — WHAT to make, HOW MUCH throughput, and HOW BIG a thing it can assemble.** A fabrication facility carries **points/day** for one or more **industry types**; a job only progresses if its facility has points for *its* type, the input resources are in stock, and (for a whole unit/ship) the **assembly bay is big enough to hold it**. You choose the type mix, the throughput, and the bay size — a corvette slip can't lay down a dreadnought, and a light vehicle bay can't assemble an AT-M6.

**A. Industry type — WHAT it makes (the routing dial, `IndustryAtb.IndustryPoints` per type)**
| Type | Makes | Consumer |
|------|-------|----------|
| **Refining** | minerals → **materials** (space-crete, steel…) | the whole materials economy |
| **Component-construction** | weapons, reactors, shields… | ship/unit designs |
| **Installation-construction** | colony buildings (mines, factories, defences) | the colony |
| **Ordnance-construction** | missiles/ammo → magazine | weapons |
| **Unit-assembly** *(NEW)* | **tanks · walkers · aircraft · ground units** — the whole-vehicle assembly bay | armies / air wings |
| **Ship-assembly** | ships | fleets |

**B. Throughput — points/day (`IndustryAtb.IndustryPoints`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Small** | light, cheap facility | slow builds — a long queue |
| **Large** | fast throughput — clears the queue | heavy + big footprint (a bombard/capture target) |

**C. Specialization — dedicated vs general**
| Option | Why | Catch |
|--------|-----|-------|
| **Dedicated line** (one type) | all points in one job type — a pure refinery / pure vehicle-foundry / pure shipyard | idle when there's no work of that type |
| **General factory** (many types) | one facility covers component + installation + ordnance | jack of all — splits its day across types |

**D. Assembly-bay size — the biggest whole unit/ship the facility can assemble (generalizes `Slip Size`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Small bay / slip** | cheap — builds troopers, light tanks, corvettes | can't assemble a walker/capital — the tonnage overflows the bay |
| **Heavy bay / capital slip** | lays down an **AT-M6, a Titan, a dreadnought** | huge, costly — a serious industrial commitment (a big war-foundry is itself a big build) |

*This is one gate with three faces — a **slip** (ships), a **vehicle bay** (tanks/walkers), a **hangar** (aircraft). The mass of the dialed unit picks the bay it needs; that's the AT-M6 forcing (§0b) made into a real build gate. **Today the field exists but is inert** (`Slip Size`/`MaxVolume` is stored, never read in `ConstructStuff`; the ship gate is duplicated in `LaunchComplex`), so wiring it — for ships AND generalizing it to vehicle/air bays — is the door's key build item.*

**E. Reverse operations — repair, refit, recycle (a facility works both ways)**
| Option | Why | Catch |
|--------|-----|-------|
| **Repair / refit** | restore a damaged unit, or rebuild it to a newer design — a shipyard/foundry maintains, not just builds | ⏳ repair of *partial* damage needs the degraded-condition model (combat is whole-unit today); refit is a construction job |
| **Recycle / scrap** | break down a decommissioned/captured unit → **reclaim materials** (the reverse arrow, closes the mine→build→scrap loop) | ⏳ net-new — no scrapping path exists yet |

**Modellability audit (§0d — Modelled spine, one broken feed, some redundancy to clean):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Industry-type routing | ✅ | `IndustryAtb.IndustryPoints` (points/day per type) → `IndustryJob.TypeID` → `IndustryTools.ConstructStuff` routes by type; a design declares `IConstructableDesign.IndustryTypeID` |
| Throughput / build-time | ✅ | `industryPointsRemaining[type] = rate × infraEfficiency`; job completes when points spent + resources consumed |
| Resource consumption | ✅ | `ConsumeResources` pulls `ResourceCosts` from `CargoStorageDB` each tick; no stock → `MissingResources` |
| Specialization (dedicated/general) | ✅ | refinery = 1 type, factory = 3 types, shipyard = component + ship-assembly — all just different `IndustryPoints` maps |
| Host (colony/station) | ✅ | the industry ability lives on either |
| **Refining (mechanic)** | ✅ **but dead-in-practice** | `refinery` → `ProcessedMaterial` (`refining` type) works, **BUT the mine→refine input feed isn't auto-supplied** — refining jobs stall at `MissingResources` (the quarantined `EconomyReadoutTests`). **The top build item — it blocks the materials economy.** |
| **Unit-assembly (tanks/walkers/aircraft)** *(NEW)* | ◐ **wire** | ground units build through generic `component-construction` today (no dedicated type/facility). Add a **`unit-assembly` industry type** + a foundry template granting it (mirrors `ship-assembly`) — mostly a data + routing change on the existing `IndustryTypeID` system |
| **Assembly-bay size gate** | ◐ **wire** | `Slip Size`/`MaxVolume` **exists but is never read** in `ConstructStuff` (ship gate duplicated in `LaunchComplexAtb.MaxTonnage`). Wire it into the build path AND generalize it to vehicle/air bays → the AT-M6/Titan size gate |
| **Repair / refit** | ⏳ **defer** | partial-damage repair needs the **degraded-condition** model (combat is whole-unit v1); refit-to-new-design is a construction job (◐) |
| **Recycle / scrap** | ⏳ **net-new** | no path reclaims materials from a decommissioned/captured unit — the reverse-build arrow |
| **Dual construction paths** | ◐ **wire** | `IndustryAtb` + `installation-construction` **and** the separate `LocalConstructionAtb`/`LocalConstructionProcessor` FIFO **both** build installations — a redundancy to unify |

> **Dead-code flags (don't build on them):** `InstallationsDB` is **confirmed dead** (never attached, no `[JsonProperty]`) — installations are components in `ComponentInstancesDB`, NOT this blob (Landmine L1). `Fighter Construction Points` is hardcoded 0 (defer).

**Reading:** Fabrication's **routing + rate + cost spine is fully Modelled** — one ability, one job, one processor, cleanly routed by industry type. The category's honest headline is still **the broken refining feed** (the #1 build item — every downstream build waits on refined materials). The developer's additions land cleanly on that spine: a **unit-assembly** type (tanks/walkers/aircraft) is a small routing+data change mirroring `ship-assembly`, and the **assembly-bay size gate** finally makes the AT-M6/Titan forcing a real build limit by wiring the already-present-but-inert `MaxVolume` field (and generalizing it past ships to vehicle/air bays). Repair/refit and recycle are the natural **reverse operations** (⏳ — repair waits on degraded-condition, recycle is net-new). Plus the two standing cleanups (inert slip-cap → folds into the bay-size wire; dual construction paths → unify). None are design gaps; they're plumbing to finish and one facility to add.

> **Cross-category note — "store units at reduced upkeep" is Logistical, not Industrial.** A depot/hangar/barracks that holds finished units **ready to deploy at reduced upkeep** (mothball / reserve) is a **storage** property, so it belongs in **Logistical ▸ Storage** (door 26, next). Captured here so it isn't lost: the mechanic is a *reserve state* — a stored unit's upkeep is discounted (it's not being maintained at full readiness) in exchange for a **reactivation delay** to bring it back to the line. It needs an **upkeep/maintenance cost model** to discount against (to verify when Logistical is built — it may be partial/net-new). Fabrication *builds* the unit; Logistical *stores* it; the two are different doors.

**Numbers:** refinery ≈ 500 refining-pts/day (Size 5000 × 0.1); factory Size × 0.1 per type; shipyard `CrewSize × 0.02` ship-assembly; build-time = `ProductionPointsCost ÷ (rate × infraEfficiency)`; assembly-bay size = max unit/ship mass (the AT-M6 needs a heavy bay). Cradle-to-grave: a facility is a **built installation** (a war-foundry is itself a big build) — captured with the colony, bombarded as a footprint target.

**Preset coordinates:** refinery (dedicated `refining`) · factory (general — component+installation+ordnance) · **vehicle foundry** *(unit-assembly + heavy bay — builds the AT-M6/Titan)* · **aircraft plant** *(unit-assembly + hangar)* · shipyard (+ slip tonnage) · **repair yard** *(refit/repair, ⏳)* · **local-construction yard** *(the FIFO twin — the redundant path to unify)*.

---

## ✅ §7 Industrial — COMPLETE (2/2 doors locked)
Extraction 🔒 · Fabrication 🔒. **The economic SPINE, mostly Modelled** — mine→refine→build all run live through daily processors, host-agnostic (colony OR station), and the purest non-combat category (§0f trivially satisfied: Industrial *is* the backbone everything else draws on). Two genuinely-different engine subsystems (mining is NOT an industry type), hence two doors. Headline readings: **Extraction is live + solid** — the mine→cargo loop with realistic deposit depletion (accessibility decays cubically), automine mobility, and station-or-colony hosting; the one real hole is **gas/atmosphere harvesting** (net-new — the gas-giant fuel play), plus small wires (per-mineral focus; per-hex deposits as source-of-truth). **Fabrication's routing/rate/cost spine is fully Modelled** (one ability routed by `IndustryTypeID`), but it holds the category's **one load-bearing gap: the refining feed is BROKEN** — the refinery works but its mineral inputs aren't auto-supplied, so materials never get made (the quarantined economy test). This is the **#1 build item** for the whole game economy. **Adapted per the developer** with two facility additions: a **`unit-assembly` type** (tanks/walkers/aircraft — the vehicle-foundry twin of `ship-assembly`, so the AT-M6 has a real facility to build in) and a generalized **assembly-bay size gate** (wires the inert `MaxVolume` field + extends it past ships to vehicle/air bays — the AT-M6/Titan size forcing, §0b, made a real limit); plus the natural **reverse operations** (repair/refit + recycle, ⏳). Two cleanups fold in: the inert slip-cap becomes part of the bay-size wire; the **dual construction systems** (IndustryAtb vs LocalConstruction) still want unifying. Dead-code flags held: `InstallationsDB` is dead (installations are `ComponentInstancesDB`), `Fighter Construction Points` hardcoded 0. **Cross-category:** "store units at reduced upkeep" (mothball/reserve) is **Logistical ▸ Storage** (door 26), not here — captured for the next category (needs an upkeep-cost model to discount against). Build-list: (1) **fix the refining input feed** (top — unblocks the materials economy); (2) **`unit-assembly` type + a vehicle-foundry/aircraft-plant facility** (the AT-M6 build path); (3) **wire + generalize the assembly-bay size gate** (ships slip + vehicle bay + aircraft hangar — the size forcing); (4) **gas/atmosphere harvester** (net-new extraction medium); (5) unify the **two construction paths**; (6) repair/refit (waits on degraded-condition) + recycle/scrap (net-new); (7) per-mineral extraction focus + per-hex mining as source-of-truth.

---

## ⚙ 7 — INDUSTRIAL · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Industrial is the **economic spine — the front end of the whole game: mine → refine → build.** Everything else the designer makes (a weapon, a reactor, a shield, a ship, a colony installation, a tank) comes out of *this* category's output. It is the purest **non-combat** category: it has no combat consumer at all — Industrial *is* the backbone every other system draws on. Two doors because there are two genuinely-distinct engine subsystems: **Extraction** (mining — NOT an industry type; its own processor) and **Fabrication** (one industry ability routed by type: refine / component / installation / ordnance / ship-assembly / [new] unit-assembly). Both run live through **daily processors** and are **host-agnostic** — a colony *or* a station mines and builds identically. The honest work here is not design gaps; it's a cluster of real engine gaps, the biggest being **the broken refining feed** (materials never get made).

### Pillar tags (§0h)
- **Pillar:** Military / Economy — **Medium** (the *production substrate*). Industrial is not a projector or a counter; it is the **Medium** every projector is *made from*. In PROJECTOR⛓COUNTER terms (E0a) it sits under the shared spine as the thing that manufactures both the projector and its counter — a weapon and its armor both leave an Industrial facility.
- **skeleton-role:** **Medium / infrastructure.** No seat, no officer, no target-facet; it produces the physical objects the other pillars wield.
- **§0h nuance — the pure-infrastructure NPC clause applies:** an NPC uses Industrial **FOR itself** (to mine/refine/build its fleets), not **AT** you. That still counts as Mirrored *provided the NPC actually runs the loop* — an NPC that never refines is the inert-AI trap (see §0g stamp).
- **§0i count-resolvers-not-doors:** Industrial's resolver is **the industry/economy loop** (`MineResourcesProcessor` + `IndustryProcessor`, both daily) — NOT the combat resolver. Every dial below must bottom out in a number one of those two daily processors reads. Extraction and Fabrication are the same *economy loop* specialized; the ~7 preset facilities (mine, refinery, factory, foundry, shipyard, robo-miner, gas-skimmer) are **DATA that loop reads**, not new loops.

### Complete dial → derived-stat → engine-wire table (VERIFIED)

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion (the industry loop) |
|------|------|--------------|-------|--------------------------|--------------------------------------|
| **7.1 Extraction** | Extraction rate (small↔large) | units/mineral/day | ✅ | `MineResourcesAtbDB.ResourcesPerEconTick` (`Industry/MineResourcesAtbDB.cs:11`) → `MiningDB.BaseMiningRate` (`Industry/MiningDB.cs:11`) | `MineResourcesProcessor.ProcessEntity` (`Industry/MineResourcesProcessor.cs:37`) draws down the deposit daily |
| 7.1 | Deposit depletion / accessibility | 0.1–1.0, **decays cubically** as deposit empties | ✅ | `MineResourcesProcessor.cs:98-100` — `accessability = (newAmount/HalfOriginalAmount)^3 × Accessibility`, clamped ≥0.1 | the "move on / re-survey" pressure — diminishing returns force relocation |
| 7.1 | Colony/host bonus | actual = base × miningBonus × accessibility | ✅ | `MiningHelper.CalculateActualMiningRates` (`Industry/MiningHelper.cs:37`); reads `ColonyBonusesDB.GetBonus(AbilityType.Mine)` (`:48-50`) × per-mineral `Accessibility` (`:59`); written to `MiningDB.ActualMiningRate` (`Industry/MiningDB.cs:13`) at `MineResourcesProcessor.cs:140` | actual rate the daily loop applies |
| 7.1 | Automation / mobility (fixed mine ↔ automine/RoboMiner) | fixed = Area×0.00001 · automine = Size×0.005, **transportable** | ✅ | same `MineResourcesAtbDB` atb; `automine` template is Size-based + mounts as ShipCargo/PlanetInstallation | drop a RoboMiner on an asteroid, retrieve later — the belt-mining play |
| 7.1 | Host (colony ↔ station) | host-agnostic | ✅ | `MiningHelper.TryGetMiningBody` — a station mines its body exactly like a colony | colony OR station, no code fork |
| 7.1 | Target-resource focus (broad ↔ per-mineral) | per-mineral rate | ◐ **wire** | rate field `ResourcesPerEconTick` **is** per-mineral (`MineResourcesAtbDB.cs:11`) but templates fill it flat — expose a focus dial | pour capacity into one scarce ore |
| 7.1 | **Medium — solid ↔ gas/atmosphere (skimmer)** | fuel/sorium from a gas giant | ⏳ **MISSING (net-new)** | **no** gas-harvest component/ability exists — a new extraction medium mirroring the mining atb | the Expanse/gas-giant fuel play — the one real *design* hole |
| 7.1 | Per-hex deposit source-of-truth | per-hex minerals | ◐ **wire** | `HexMinerals` (`Industry/HexMinerals.cs`) seeds surface hexes but is **view-only**; body-wide pool is still source of truth | make hexes the real deposit |
| 7.1 (ESSENCE) | **★ Excavation medium — pull a yield from a *site*** (ore/gas/**site**) | one-shot/over-time yield from `RuinsDB` | ⏳ **Defer** | mirrors `MineResourcesProcessor`; **consumes `RuinsDB` (`Galaxy/RuinsDB.cs`) ONCE the `SystemBodyFactory.cs:1418-1420` tautology bug is fixed** (ruins never generate today) | the field-site expedition arm of Extraction — a scientist posting pulls a tech-windfall/blueprint from an ancient site |
| **7.2 Fabrication** | Industry-type routing (refine/component/installation/ordnance/ship-assembly/**unit-assembly**) | points/day **per type** | ✅ | `IndustryAtb.IndustryPoints` (`Industry/IndustryAtb.cs:14`, dict type→pts) → `IndustryJob.TypeID` → `IndustryTools.ConstructStuff` routes by `designInfo.IndustryTypeID` (`Industry/IndustryTools.cs:126`) | a design declares `IConstructableDesign.IndustryTypeID`; a job only progresses if its facility has points for *its* type |
| 7.2 | Throughput (small ↔ large) | points/day × infra efficiency | ✅ | `IndustryTools.cs:121` — `industryPointsRemaining[type] = rate × infraEfficiency`; efficiency from `InfrastructureProcessor.GetEfficiency` (`:115`) | clears the queue faster; heavy = bigger bombard/capture footprint |
| 7.2 | Resource consumption (input feed) | draws `ResourceCosts` from stock each tick | ✅ | `IndustryTools.ConsumeResources` (`:173` call, `:221` def) pulls from `CargoStorageDB`; no stock → `IndustryJobStatus.MissingResources` (`:146`, `:204`) | the build stalls visibly when short — the gauge |
| 7.2 | Specialization (dedicated ↔ general) | 1-type vs multi-type points map | ✅ | just different `IndustryAtb.IndustryPoints` maps — refinery=1 type, factory=3, shipyard=component+ship-assembly | idle-when-no-work vs jack-of-all-trades |
| 7.2 | Host (colony ↔ station) | host-agnostic | ✅ | `IndustryAbilityDB` lives on either entity (`IndustryAtb.OnComponentInstallation`, `IndustryAtb.cs:46-64`) | station factory = colony factory |
| 7.2 | **Refining (mineral → material)** | `ProcessedMaterial` output added to storage | ✅ mechanic **but DEAD-IN-PRACTICE** | refinery → `refining` type → `ProcessedMaterial.OnConstructionComplete` adds cargo (`Industry/ProcessedMaterial.cs:34-40`) works — **BUT the mineral input feed isn't auto-supplied**, so refining jobs stall at `MissingResources` (`IndustryTools.cs:146/:204`). **The #1 build item for the whole game economy.** | see the dedicated section below |
| 7.2 (NEW) | **Unit-assembly (tanks/walkers/aircraft)** | a new `IndustryTypeID` + foundry template | ◐ **wire** | ground units build through generic `component-construction` today; add a **`unit-assembly` type** to the `IndustryTypeID` routing (`IndustryTools.cs:126`) + a foundry template granting `IndustryAtb.IndustryPoints["unit-assembly"]` — mirrors `ship-assembly`, a data+routing change | the vehicle-foundry / aircraft-plant so the AT-M6 has a real facility |
| 7.2 (NEW) | **Assembly-bay size gate** (slip ▸ vehicle-bay ▸ hangar) | max whole-unit mass the bay can assemble | ◐ **wire — inert field** | `IndustryAbilityDB.MaxVolume` (`Industry/IndustryAbilityDB.cs:15`) is **stored, NEVER READ** in `ConstructStuff`; set from `IndustryAtb.MaxProductionVolume` (`IndustryAtb.cs:17,50`, defaults `PositiveInfinity` at `:25`). Ship gate is **duplicated** in `LaunchComplexAtb.MaxTonnage` (`Ships/LaunchComplexAtb.cs:11`), read at `LaunchComplexProcessor.cs:58` (`shipDesign.MassPerUnit > pad.MaxTonnage`) | wire `MaxVolume` into `ConstructStuff` AND generalize past ships → the AT-M6/Titan size forcing (§0b) made a real gate |
| 7.2 | **Repair / refit** (reverse op) | restore/rebuild a unit | ⏳ **Defer** | partial-damage repair needs the **degraded-condition** model (combat is whole-unit v1); refit-to-new-design is a construction job (◐) | a shipyard/foundry maintains, not just builds |
| 7.2 | **Recycle / scrap** (reverse op) | reclaim materials from a scrapped/captured unit | ⏳ **net-new** | no path reclaims materials — the reverse-build arrow that closes mine→build→**scrap** | decommission → materials back to stock |
| 7.2 (ESSENCE) | **★ Salvage / recovery** — a wreck: strip mass / recover design / recover survivors | wreck yield (materials + a ship design + population) | ⏳ **Defer** | this is the recycle arm pointed at a **wreck**; **needs `DamageProcessor.SpawnWreck` finished** (`Damage/DamageComplex/DamageProcessor.cs:478` — a stub that just `TagEntityForRemoval`s the ship, no wreck created); the unused events `WreckSalvaged` / `WreckComponents` (`Engine/Events/EventTypes.cs:247,350`) are the ready hooks | the field-site recovery arm of Fabrication — BSG derelict salvage |
| 7.2 | Dual construction paths | installation build (redundant) | ◐ **wire (cleanup)** | `IndustryAtb` + `installation-construction` **AND** the separate `LocalConstructionAtb` (`Industry/LocalConstructionAtb.cs:7`) / `LocalConstructionProcessor` (`Industry/LocalConstructionProcessor.cs:12`) FIFO **both** build installations — unify | one build path, not two |

### The industry/economy loop — mine → refine → build (file:line); and THE BROKEN REFINING FEED

**The full loop, verified:**
1. **MINE** — `MineResourcesProcessor` (daily `IHotloopProcessor`, `Industry/MineResourcesProcessor.cs:37`) reads `MiningDB.ActualMiningRate` (computed by `MiningHelper.CalculateActualMiningRates`, `MiningHelper.cs:37`, = base × colony `AbilityType.Mine` bonus × cubic-decaying `Accessibility`) and adds raw **minerals** to the host's `CargoStorageDB`.
2. **REFINE** — `IndustryProcessor` (daily `IHotloopProcessor`, `Industry/IndustryProcessor.cs:7,23-25`) calls `IndustryTools.ConstructStuff` (`Industry/IndustryTools.cs:90`). A `refining`-type job spends `IndustryAtb.IndustryPoints["refining"]`, calls `ConsumeResources` (`:173`) to pull mineral inputs from cargo, and on completion `ProcessedMaterial.OnConstructionComplete` (`Industry/ProcessedMaterial.cs:34`) adds the **material** to storage.
3. **BUILD** — the same `ConstructStuff` runs `component`/`installation`/`ordnance`/`ship-assembly` jobs, routed by `designInfo.IndustryTypeID` (`:126`), each consuming its `ResourceCosts` (materials) from cargo.

**THE BROKEN REFINING FEED — the #1 gap.** The refining *mechanic* is whole and correct — step 2 above runs end to end **when the mineral inputs are in the refinery's `CargoStorageDB`.** The break: **nothing auto-supplies those mineral inputs to the refining job.** `ConsumeResources` (`IndustryTools.cs:221`) only draws from what is already in local cargo; there is no wire that routes mined minerals INTO the refinery's input stock. So refining jobs stall permanently at `IndustryJobStatus.MissingResources` (`IndustryTools.cs:146` / `:204`) and **materials never get made** — which starves every downstream `component`/`installation`/`ordnance`/`ship-assembly` build (all consume materials, not raw minerals). This is caught and quarantined by `EconomyReadoutTests` (`Pulsar4X.Tests/EconomyReadoutTests.cs`).
- **What to wire:** a feed that makes a mined mineral available to a refining job's `ConsumeResources` draw — either (a) the mine and refinery share the same host `CargoStorageDB` so mined minerals land where `ConsumeResources` looks, or (b) a logistics/supply link that stages the mineral inputs into the refinery's stock before `ConstructStuff` runs. The exact insertion point is between `MineResourcesProcessor` output and `IndustryTools.ConsumeResources` (`IndustryTools.cs:173,221`). Until this is fixed, the materials economy is inert — **fix it first; everything else Industrial waits on it.**

### Prerequisite fixes & dead stubs (file:line)

1. **[#1 — refining feed]** The mine→refine input feed is not auto-supplied — refining stalls at `MissingResources` (`Industry/IndustryTools.cs:146,204`; consumer `ConsumeResources` `:221`). **Top build item — blocks the whole materials economy.** (See section above.)
2. **[GenerateRuins tautology — blocks Excavation]** `SystemBodyFactory.GenerateRuins` (`Galaxy/SystemBodyFactory.cs:1411`) has a **logical tautology at `:1419-1420`**: `if (bodyType != BodyType.Terrestrial || bodyType != BodyType.Moon) return;` — a value can never equal *both* Terrestrial *and* Moon, so at least one inequality is always true → the guard **always early-returns** → `RuinsDB` (`Galaxy/RuinsDB.cs`) is never populated → nothing to excavate. Fix: change `||` to `&&`. Prerequisite for the Excavation-medium dial.
3. **[SpawnWreck stub — blocks Salvage]** `DamageProcessor.SpawnWreck` (`Damage/DamageComplex/DamageProcessor.cs:478`, called on ship death at `:152`) is a **stub that just `TagEntityForRemoval`s the destroyed ship** — it creates no wreck entity. Salvage/recovery needs it finished (spawn a wreck carrying the dead ship's design + materials + survivors). Hooks already exist: `WreckSalvaged` / `WreckComponents` events (`Engine/Events/EventTypes.cs:247,350`) — declared, unconsumed.
4. **[Dead code — don't build on]** `InstallationsDB` (`Industry/InstallationsDB.cs`) is **confirmed dead** (never attached, no `[JsonProperty]` state) — installations are components in `ComponentInstancesDB`, NOT this blob (Landmine L1). `Fighter Construction Points` hardcoded 0 (defer).
5. **[Cleanup — dual construction]** `IndustryAtb`+`installation-construction` and the separate `LocalConstructionAtb`/`LocalConstructionProcessor` FIFO (`Industry/LocalConstructionAtb.cs:7`, `Industry/LocalConstructionProcessor.cs:12`) both build installations — unify.
6. **[Cleanup — inert slip-cap folds into bay-size wire]** `IndustryAbilityDB.MaxVolume` (`Industry/IndustryAbilityDB.cs:15`) stored, never read; ship gate duplicated in `LaunchComplexAtb.MaxTonnage` (`Ships/LaunchComplexAtb.cs:11`, read `LaunchComplexProcessor.cs:58`). Wiring the bay-size gate resolves both.

### Design essence captured inline (the field-site excavation/salvage loop)
The merged Exploration essence gives Industrial two **field-site** arms — the same "assign a specialist, pull a yield" loop reused (§0i: it's the lab loop, not a new resolver). **Excavation** is Extraction pointed at a *site*: a scientist posting at ancient ruins pulls a one-shot **tech windfall or a unique blueprint you can't research normally** (Stargate/Mass Effect/Halo), with the catch that guardians/traps can kill the team. **Salvage** is Fabrication's recycle arm pointed at a *wreck*: strip it for mass, recover its **ship design**, or recover **survivors** (population — even a stranded leader), with the catch that it may be booby-trapped or something is still aboard (BSG). Both are ⏳ Defer because each sits behind a dead stub — Excavation behind the `RuinsDB` tautology (ruins never generate), Salvage behind `SpawnWreck` (wrecks never spawn). Fix those two one-liners-plus-consumer and Industrial gains the whole "exploration has something to *take*" layer for cheap, because the field-site loop machinery already exists.

### §0g stamp — three acceptance criteria
- **Reachable (cradle-to-grave, player-side):** ✅ **PASS.** A facility is a **component** the player researches → designs → builds from materials → installs on a colony/station → runs (mine/refine/build) → loses when the colony is captured or the installation is bombarded (a war-foundry is itself a big build and a footprint target). The mine's grave rung is the *deposit running dry* (cubic accessibility decay, `MineResourcesProcessor.cs:98-100`) — you re-survey and relocate. Every rung is named and wired.
- **Mirrored (opponent-side):** ◐ **CONDITIONAL — this is the live risk.** Industrial is the §0h "pure-infrastructure" case: the NPC uses it **FOR itself** (to mine/refine/build its fleets), not AT you. The Mirror passes **only if the NPC actually runs the loop.** The daily processors (`MineResourcesProcessor`, `IndustryProcessor`) are entity-driven and will fire on NPC-owned colonies/stations — but the NPC decision layer must *queue the jobs*: `NPCDecisionProcessor.cs:80` names "prioritize colony construction / refinery queues" as intent. **An NPC that never queues a refining job = the inert-AI trap** (solitaire). Verify the NPC brain actually issues `IndustryOrder2` build/refine jobs through the identical order path the player uses — no player-only build code. And note the Mirror inherits the #1 gap: an NPC's refining stalls at `MissingResources` exactly like the player's until the feed is fixed.
- **Observable (the gauge, both sides):** ✅ **PASS (player-side; extend to enemy-side).** `ColonyManagementWindow` (`Pulsar4X.Client/Interface/Windows/ColonyManagementWindow.cs`) already renders the readouts: **Summary** (`:122` — population + infrastructure efficiency + installed components + raw *and* refined stockpiles), **Production** (`:124` `DisplayIndustry`/`IndustryDisplay` — the job queue, batch/repeat/priority/cancel), **Construction** (`:125`), **Mining** (`:126` `DisplayMining` — per-mineral rates, annual production, years-to-depletion). The stall gauge is live: a job at `MissingResources` (`IndustryTools.cs:146,204`) shows in the Production tab — so the refining feed break is *visible*, not silent. The extension owed by §0g: show the same working on an *enemy* facility you've scouted (a Failure-A wire, the number exists).

### Cross-category shared state (Prime Directive)
- **Industrial × EVERYTHING** — it *builds every component in the designer.* Any door's output design declares `IConstructableDesign.IndustryTypeID` + `ResourceCosts`; those are what `ConstructStuff` (`IndustryTools.cs:90`) consumes and produces. Change the industry-type enum or the material-cost contract and every category's build path is affected.
- **Industrial × Logistical (doors 26–27, the feed):** the whole loop reads/writes `CargoStorageDB`; the refining-feed fix is fundamentally a **logistics/supply** wire (get minerals to the refinery's input stock). Also: "store finished units at reduced upkeep" (mothball/reserve) is **Logistical ▸ Storage**, NOT Industrial — Fabrication *builds* the unit, Logistical *stores* it (needs an upkeep-cost model to discount against; verify when Logistical is built).
- **Industrial × Exploration (the field-site loop):** Excavation consumes `RuinsDB` (`Galaxy/RuinsDB.cs`, gated by the `SystemBodyFactory.cs:1418` tautology); Salvage consumes wrecks (gated by `SpawnWreck`, `DamageProcessor.cs:478`). Both are the same expedition loop as the science lab (§0i).
- **Industrial × Damage/Combat (grave rung + salvage source):** `DamageProcessor.SpawnWreck` (`Damage/DamageComplex/DamageProcessor.cs:478`) is *where* a killed ship becomes salvageable stock — combat feeds Industrial's recycle arm. A bombarded factory is Industrial's loss rung on the war map.
- **Industrial × Infrastructure/Colony:** throughput scales by `InfrastructureProcessor.GetEfficiency` (`IndustryTools.cs:115`) and mining by `ColonyBonusesDB.GetBonus(AbilityType.Mine)` (`MiningHelper.cs:48-50`) — the colony's condition throttles both loops.

### Holes owned/resolved
- **H3 — Self-replication / mobile fabrication** (`COMPONENT-DESIGNER-CATEGORIES.md:91`): **PARTIALLY RESOLVED — home = Industrial.** *Mobile fabrication* (an Industrial door mounted on any chassis — a carrier building fighters, a mobile shipyard) is the **cheap half**: `IndustryAtb`/`MineResourcesAtbDB` are `IComponentDesignAttribute`s that already install on any host entity (`IndustryAtb.OnComponentInstallation`, `IndustryAtb.cs:46`; host-agnostic `MiningHelper.TryGetMiningBody`), so it is a small **universal-mount** change — let Industrial doors mount on Vehicle/Hull chassis, not just colony/station. *Self-replication* (a unit building a copy of **its own design** from raw matter) is the **deep half — still open**: a new mechanic, deferred. Status: **mobile-fabrication ◐ wire (home: Industrial, this category); self-replication ⏳ Defer (net-new).**

---

## §8 — Logistical

Logistical is the **hold-and-move backbone** — where everything the game makes gets *kept* and *carried*. It's the twin of Industrial: Industrial *builds*, Logistical *stores and ships*. Pure support/economy (no direct combat consumer, though it feeds combat through fuel → Δv, ordnance → resupply, and troops → invasion), and **mostly Modelled** — one universal cargo hold, a dedicated troop bay, a manual transfer, and a *functional automated supply market* all run live. The one honest headline is the developer's **mothball/reserve** request, which surfaces a genuine missing system: **there is no upkeep for ships or units to reduce.**

**The yardstick — the storage/transfer system** (`CargoStorageDB` + `CargoTransferProcessor` + the `Logistics/` market), not the combat resolver. Two doors, two verbs: **Storage** (hold it) and **Transfer** (move it).

### §8.0 Shared logistical dials (both doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **Capacity / rate** | volume held (m³) or throughput moved (kg/s) — the headline number |
| **What it holds / moves** | the **cargo type** — general · fuel · ordnance · passenger · (troops = a separate bay) |
| **Host** | ship, colony, OR station — a hold/transfer ability lives on any |
| **Mass / footprint** | scale the capacity → mass + a war-map footprint (a warehouse is a capture/bombard target) |
| **Grave rung** | a hit hold spills/loses its cargo; a destroyed tanker loses the fuel |

### 8.1 Logistical ▸ STORAGE  🔒 *locked*
*Hold things — cargo, fuel, ammunition, and whole units. One universal hold, partitioned by cargo type, plus a dedicated bay for carrying troops/vehicles. Everything from a freighter's cargo bay to a fuel tank to a missile magazine to a dropship's vehicle bay to a **mothball reserve yard** falls out of these dials.*

**The core decision — WHAT to hold, and HOW MUCH of each.** A hold is **volume** (m³), partitioned by **cargo type** — a bay only accepts cargo whose type matches a pool it has. You choose which types (general goods / fuel / ordnance / passengers) and how much volume of each, plus the special cases: fuel (a cargo type that becomes your Δv), ordnance (a reserve that resupplies your magazines), units (a *separate* troop/vehicle bay), and — the one that isn't built — a reserve/mothball state.

**A. Cargo type + volume — the core hold (`CargoStorageAtb.StoreTypeID` + `MaxVolume`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **General storage** | holds minerals, materials, trade goods | the bulk hold — nothing special |
| **Dedicated type** (fuel/ordnance/passenger) | a hold tuned to one cargo type | won't accept anything else — a fuel tank can't carry ore |
| **Big volume** | fewer trips, deep stockpile | heavy + a big footprint |

**B. Fuel storage — a tank IS a fuel-type hold (`"fuel-storage"`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Small tank** | light — a short-legged ship | refuels often (low Δv) |
| **Large tank** | deep Δv / long range | heavy — fuel mass drags acceleration (the Propulsion trade) |

**C. Ordnance / magazine — the reserve that resupplies the guns**
| Option | Why | Catch |
|--------|-----|-------|
| **Ship ordnance hold** (`"ordnance-storage"`) | a missile reserve → reloads the launcher's internal magazine | mass; a hit magazine is a hazard |
| **Ground magazine** (`GroundMagazineAtb`) | a unit's ammo pool (kg) → the ammo gate + `GroundAmmo` resupply | runs dry → the weapon goes silent until resupplied |

**D. Unit / troop bay — carrying whole units (a SEPARATE bay, not the cargo hold)**
| Option | Why | Catch |
|--------|-----|-------|
| **Personnel bay** | carries infantry/personnel-class units to the drop | can't hold vehicles (class-matched) |
| **Vehicle bay** | carries tanks/walkers (the AT-M6 dropship) | bigger, heavier; a bay only carries its own carry-class |

**E. Mothball / reserve — store a unit ready at reduced upkeep** *(the developer's ask)*
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Active** | fully crewed, ready to fight now | full upkeep |
| **Reserve / mothballed** | **cheaper to keep** — stored ready, not maintained at full readiness | a **reactivation delay** to bring it back to the line |

**Modellability audit (§0d — holds are Modelled; mothball needs a prerequisite):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Cargo type + volume | ✅ | `CargoStorageAtb.StoreTypeID`/`MaxVolume` → `CargoStorageDB.TypeStores` (volume pool per type) → `StorageSpaceProcessor` |
| Fuel storage | ✅ | `"fuel-storage"` pool → `CargoTransferProcessor.UpdateMassFuelAndDeltaV` → `NewtonThrustAbilityDB.SetFuel` → Δv |
| Ordnance (ship + ground) | ✅ | `"ordnance-storage"` hold → launcher magazine reload; `GroundMagazineAtb.Capacity_kg` → `GroundAmmo` |
| Unit / troop bay | ✅ | `GroundBayAtb` (`Capacity`, `CarryClass`) → `GroundTransportDB.LoadedUnits` (loading moves the unit off the planet roster) |
| **Mothball / reserve (ships/units)** | ⏳ **net-new** | **there is NO upkeep for ships/units to reduce** — nothing to discount (see the callout) |
| Mothball / reserve (stations) | ◐ **wire** | stations DO have upkeep (`StationEconomyDB.OperatingCost`) — scale it by a stored/active flag |
| Bare-hold silent no-op | ◐ **wire** (bug) | a `CargoStorageDB` with no `CargoStorageAtb`-seeded `TypeStore` silently accepts nothing — a real trap to guard |

> **🔎 The mothball finding — the game has NO fleet/army upkeep.** The developer's "store units at reduced upkeep" surfaced a genuine gap: **once built, a ship or ground unit costs nothing to keep** (only fuel when it moves). The `TransactionCategory` enum has just `InitialInvestment / Research / ColonyTax / StationUpkeep` — no ShipUpkeep, no ArmyUpkeep; Aurora's Maintenance Supply Points are confirmed absent. **Only space stations** are billed ongoing upkeep (`StationUpkeepProcessor`, every 30 days, placeholder credits). So the mothball mechanic is a **TWO-PART build**: **(1)** a **ship/unit upkeep clock** — net-new, but it *mirrors the existing `StationUpkeepProcessor`* + a new `TransactionCategory`; **(2)** then the **reserve discount** is a small Wire on top. This is a "the feature you asked for reveals a deeper missing system" case — and the deeper system is worth building on its own: **with no upkeep, a giant standing fleet has zero downside**, so there's no pressure to demobilize, no logistics-strategy depth, no reason to mothball. Building upkeep *creates* the pressure that makes mothballing (and the whole reserve/reactivation decision) matter. Stations already prove the pattern.

**Reading:** Storage's **hold layer is fully Modelled** — one universal volume-partitioned hold covers cargo, fuel (→ Δv), and ordnance (→ resupply), with a *separate* class-matched bay for carrying units (the invasion transport). The developer's mothball/reserve is the honest exception: it's **net-new for ships/units** because there's no upkeep to reduce — a two-part build (upkeep clock, then the discount), where part 1 is a valuable system the game lacks and part 2 is a cheap wire once part 1 exists (stations already have the pattern). One real bug to fix: a bare hold silently swallows nothing.

**Numbers:** `MaxVolume` (m³, "Storage Volume" 10–10,000); fuel/ordnance in the same m³ pools; `GroundBayAtb.Capacity` (carry-size units); `GroundMagazineAtb.Capacity_kg` (kg ammo). Upkeep (future) → credits/month like `StationEconomyDB` (placeholder). Cradle-to-grave: a hold is a **component** (built → installed → holds → a hit spills it).

**Preset coordinates:** cargo hold (general) · fuel tank · ordnance magazine · **troop bay** (personnel) · **vehicle bay** (the AT-M6 dropship) · warehouse *(facility)* · **mothball yard** *(⏳ needs the upkeep clock)*.

### 8.2 Logistical ▸ TRANSFER  🔒 *locked*
*Move things between holds, colonies, and orbit — by hand, by spaceport, or by a standing automated supply line. Everything from a cargo shuttle to a planet's spaceport to a fleet-wide freight network falls out of these dials. Fully Modelled — the transfer engine AND a functional profit-based supply market both run live.*

**The core decision — HOW FAST, HOW FAR, and BY HAND or AUTOMATIC.** Moving cargo is a **rate** (kg/s) over a **range** (how far apart the two holds can be, in Δv). You choose the throughput/reach, and whether hauling is a **manual** point-to-point transfer, a **spaceport** (the surface↔orbit elevator), or a **standing automated route** that hauls supply without you touching it.

**A. Transfer rate + range (`CargoTransferAtb.TransferRate_kgs` / `TransferRange_ms`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Fast / short** | shift a lot of tonnage quickly, dock-to-dock | only works close (low Δv reach) |
| **Slow / long** | reach a distant hold (a far orbit) | a trickle — slow to move bulk (rate summed, range averaged across components) |

**B. Facility — what does the moving**
| Option | Why | Catch |
|--------|-----|-------|
| **Cargo shuttle** (ship) | mobile hauling between any two holds | its own mass/fuel |
| **Spaceport** *(facility)* | the **surface↔orbit** elevator — loads ships from the colony fast | fixed to the colony; a footprint target |
| **Launch complex** *(facility)* | lifts whole **ships** to orbit (tonnage-gated pads) | ship-elevator, not cargo; costs fuel-to-orbit |

**C. Automated supply — the logistics office (`LogiBaseAtb.LogisicCapacity`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Manual only** | full control — you order every transfer | micromanagement at scale |
| **Standing route** (logistics office) | set **desired stock levels** (min/max) and a **freight market** auto-hauls to keep colonies supplied | you cede control to the market; faction-gated (needs logistics access) |

**Modellability audit (§0d — fully Modelled, including the automated network):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Transfer rate / range | ✅ | `CargoTransferAtb.TransferRate_kgs`/`TransferRange_ms` → `CargoStorageDB.TransferRate`/`TransferRangeDv_mps` → `CargoTransferProcessor` (every 1 min, escrow move) |
| Spaceport (surface↔orbit) | ✅ | `spaceport` = a `CargoTransferAtb` carrier contributing rate+range to the colony hold |
| Launch complex (ships to orbit) | ✅ | `LaunchComplexProcessor` — tonnage-gated pads, deducts fuel-to-orbit |
| Automated supply network | ✅ | `LogiBaseDB` (`Capacity`, `DesiredLevels` min/max) + `LogiShipperDB` (bidding) + `LogisticsProcessor` (6 h) — a **functional profit-based freight market** |
| Missile transfer range | ◐ **wire** (stub) | `MissileLauncherAtb.IsInRange` returns `true` — affects the ordnance-resupply picture |
| `LogiBaseDB` Clone drops levels | ◐ **wire** (bug) | copy-ctor loses `DesiredLevels`/`ItemsInTransit` — a latent save/transfer bug |

**Reading:** Transfer is **fully Modelled and surprisingly deep** — beyond the manual rate/range move and the spaceport surface↔orbit elevator, there's a **working automated supply market**: set min/max stock levels on a logistics office and a freight-bidding network hauls to keep your colonies topped up. That's real logistics-strategy depth already in the engine. Only two small fixes surface (a missile-range stub, a Clone save-bug) — no design gaps. Cradle-to-grave: a transfer facility is a **component/installation** (spaceport/shuttle/logistics office — built, installed, a bombard target).

**Numbers:** `TransferRate_kgs` (kg/s, default 1); `TransferRange_ms` (Δv m/s, default 100); `LogiBaseAtb.LogisicCapacity` (route capacity). Cradle-to-grave as above.

**Preset coordinates:** cargo shuttle · **spaceport** *(facility — surface↔orbit)* · **logistics office** *(facility — automated supply routes)* · fuel tanker · freight hauler · launch complex *(facility — ships to orbit)*.

---

## ✅ §8 Logistical — COMPLETE (2/2 doors locked)
Storage 🔒 · Transfer 🔒. **The hold-and-move backbone, mostly Modelled** — the twin of Industrial (it *builds*, Logistical *stores + ships*), pure support/economy feeding fuel→Δv, ordnance→resupply, troops→invasion. Headline readings: **Storage's hold layer is fully Modelled** — one universal volume-partitioned `CargoStorageDB` (general/fuel/ordnance/passenger) + a *separate* class-matched troop/vehicle bay (`GroundBayAtb` → `GroundTransportDB`, the invasion carry). **Transfer is fully Modelled AND deep** — manual rate/range move + spaceport surface↔orbit + a **functional automated freight market** (`LogiBaseDB` desired-levels + `LogiShipperDB` bidding). **The developer's MOTHBALL/RESERVE ask is the one honest gap** — and a valuable one: **there is NO upkeep for ships/units to reduce** (only stations have upkeep, `StationUpkeepProcessor`), so "store at reduced upkeep" is a **two-part build**: (1) a ship/unit **upkeep clock** (net-new, mirrors the station pattern + a new `TransactionCategory`) — worth building on its own, since with no upkeep a giant standing fleet has zero downside and there's no reason to demobilize; (2) the **reserve discount** on top (a cheap wire once upkeep exists; stations get it as a wire today). Build-list: (1) **ship/unit upkeep clock** (prerequisite for mothball + the missing economic pressure); (2) **reserve/mothball discount** on top; (3) fix the **bare-`CargoStorageDB` silent no-op** trap; (4) missile transfer-range stub; (5) the `LogiBaseDB` Clone save-bug. Dead/absent flags: Aurora MSP + dedicated supply ships confirmed absent (the automated market partly covers the supply-ship role).

---

## ⚙ 8 — LOGISTICAL · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Logistical is the **hold-and-move backbone** — the twin of Industrial (Industrial *builds*; Logistical *stores + ships*). Pure support/economy that FEEDS the other categories: fuel→Δv, ordnance→gun-resupply, troops→invasion, materials→colonies. Two doors: **8.1 Storage** (hold things) and **8.2 Transfer** (move things). Both LOCKED. The hold layer + the transfer engine + a working automated freight market all run live in the engine today; the one honest design gap is the developer's **mothball/reserve** ask, which is blocked on a missing system (fleet/army upkeep) — see the UPKEEP GAP section.

---

### Pillar tags (§0h) — note the covert-insertion bay is Espionage·Delivery

The universal PROJECTOR⛓COUNTER tag (§0h): what does a door PROJECT, and what COUNTERS it. Logistical is normally **Military/Economy · Medium reach** — it doesn't project force itself, it *enables* the force projector (a troop bay carries the invasion; a fuel tank extends the reach).

| Door / mode | Pillar tag (§0h) | Projects | Countered by |
|---|---|---|---|
| 8.1 Storage (cargo / fuel / ordnance hold) | **Economy · Medium** | supply throughput / Δv range / resupply depth | a hold hit spills its contents (fuel = a hazard); mass drags accel |
| 8.1 Storage ▸ troop/vehicle bay (`GroundBayAtb`) | **Military · Medium (Delivery)** | ground force onto a hostile world (the invasion lift) | must win the orbit first (`HasOrbitalControl`); a bay shot off takes its units |
| 8.2 Transfer (rate/range · spaceport · freight market) | **Economy · Medium** | keeps colonies/fleets supplied without micro | route capacity; faction-gated (needs `LogisticsAccess`); a damaged arm stops contributing |
| 8.2 Transfer ▸ **TOW/GRAPPLE mode** (E1b) | **Economy/Exploration · Medium** | recover a whole drifting entity (a derelict) | towed mass drags the tug's own accel (the honest cost) |
| 8.2 Transfer ▸ **COVERT-INSERTION bay** (E1d) | **★ Espionage · Delivery** (NOT Military — the espionage-delivery slot) | an operative into a hostile system, hidden | the live sensor loop (interceptable); a detection roll replaces the orbital-control gate |

**Why the covert-insertion bay is tagged differently:** a troop bay projects MILITARY force onto ground you control the orbit over; a covert-insertion bay projects an ESPIONAGE agent into ground you do NOT control — same physical shape (a bay that carries a passenger off-ship), opposite gate, and its "combat" lands in the Information-Ledger / population layer, never the fleet resolver (§0f).

---

### Complete dial → derived-stat → engine-wire table (VERIFIED)

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion |
|---|---|---|---|---|---|
| **8.1 Storage** | Cargo TYPE (general/fuel/ordnance/passenger) | which `TypeStore` pool the hold accepts | ✅ Modelled | `CargoStorageAtb(storeTypeID, maxVolume)` `Storage/CargoStorageAtb.cs:12`; seeds pool in `OnComponentInstallation` `:18-38` → `CargoStorageDB.TypeStores` (`SafeDictionary<string,TypeStore>`) `Storage/CargoStorageDB.cs:14` | a pool per type; a bay only accepts matching-type cargo. StoreTypeIDs live in `GameData/basemod/TemplateFiles/storage.json` (`general-storage`, `fuel-storage`, `ordnance-storage`) |
| 8.1 Storage | VOLUME (m³, "Storage Volume" 10–10,000) | `TypeStore.MaxVolume` / `FreeVolume` | ✅ | `CargoStorageAtb.MaxVolume` `Storage/CargoStorageAtb.cs:10`; `TypeStore` `Storage/CargoStorageDB.cs:79-98`; recalculated by `StorageSpaceProcessor.CalculatedMaxStorage` `Storage/StorageSpaceProcessor.cs:88` | bigger volume = fewer trips + heavier footprint |
| 8.1 Storage | FUEL tank (a `"fuel-storage"`-type hold) | fuel mass → `NewtonThrustAbilityDB` Δv | ✅ | fuel pool read in `CargoTransferProcessor.UpdateMassFuelAndDeltaV` `Storage/CargoTransferProcessor.cs:168-183` → `newtdb.SetFuel(fuelMass, massTotal)` `:182` | a fuel tank IS your Δv; fuel mass drags accel (the Propulsion trade) |
| 8.1 Storage | ORDNANCE / ship magazine (`"ordnance-storage"` hold) | reload pool for launcher magazines | ✅ | same `CargoStorageAtb`/`CargoStorageDB` typed pool; ordnance store defined `storage.json` (`ordnance-storage`) | a missile reserve → reloads the launcher's internal magazine; a hit magazine is a hazard |
| 8.1 Storage | GROUND magazine (a unit's ammo pool) | `GroundMagazineAtb.Capacity_kg` → `GroundUnit.MaxAmmo_kg` | ✅ | `GroundMagazineAtb.Capacity_kg` `GroundCombat/GroundMagazineAtb.cs:25`; snapshot `GroundUnitDesign.AmmoCapacity_kg` `GroundCombat/GroundUnitDesign.cs:59`; pool logic `GroundCombat/GroundAmmo.cs:19-47` (`CarriesAmmo`/`IsDry`/`Consume`/`Refill`) | runs dry → the weapon goes silent until resupplied (`GroundForces.ResupplyUnit` tops it on friendly ground, `GroundAmmo.cs`) |
| 8.1 Storage | **UNIT / TROOP bay** (SEPARATE bay, not the cargo hold) | `GroundBayAtb.Capacity` + `CarryClass` | ✅ | `GroundBayAtb` `GroundCombat/GroundBayAtb.cs:34`; `Capacity` `:37`, `CarryClass` `:39`, ctor(capacity, carryClass) `:45`; class enum `GroundCarryClass{Personnel,Vehicle}` `:13`; capacity summed on demand `GroundTransport.BayCapacity` `GroundCombat/GroundTransport.cs:40` | Personnel bay carries infantry, Vehicle bay carries armour/artillery; a bay carries only its own carry-class (can't cram a tank into a troop bay) |
| 8.1 Storage | **MOTHBALL / RESERVE** (developer's ask) | reduced-upkeep stored state | ⏳ **net-new** (ships/units) / ◐ **wire** (stations) | **nothing to discount for ships/units** — no ShipUpkeep/ArmyUpkeep exists (`TransactionCategory` `Factions/Ledger.cs:8-14`); stations DO have it (`StationEconomyDB.OperatingCost` `Stations/StationEconomyDB.cs:53`) | **TWO-PART build — see UPKEEP GAP section** |
| 8.1 Storage | *bare-hold no-op* (the trap) | a `CargoStorageDB` with no seeded `TypeStore` silently accepts nothing | ◐ **wire (bug)** | a fresh `new CargoStorageDB()` has empty `TypeStores` `Storage/CargoStorageDB.cs:33-36`; every `AddCargoByUnit`/`RemoveCargoByUnit` then returns 0 and no-ops (all `CargoMath` logging commented out) | guard on design; install a cargo/`CargoStorageAtb` module to make a hold real (Stations Slice F hit this) |
| **8.2 Transfer** | RATE (kg/s) + RANGE (Δv m/s) | `CargoTransferAtb.TransferRate_kgs` / `TransferRange_ms` | ✅ | `CargoTransferAtb.TransferRate_kgs` `Storage/CargoTransferAtb.cs:15`, `TransferRange_ms` `:20`, ctor(rate_kgs, rangeDV_ms) `:22`; recalc → `CargoStorageDB.TransferRate` `Storage/CargoStorageDB.cs:29` / `TransferRangeDv_mps` `:31` via `StorageSpaceProcessor.CalcRateAndRange` (**rate summed** `:77`, **range averaged** `range/i` `:85`) | fast/short vs slow/long. **Rate summed, range averaged across a ship's components; a damaged arm (below `StopWorkingAtPercent`) contributes nothing** `StorageSpaceProcessor.cs:75-80` |
| 8.2 Transfer | the actual escrow move | mass moved per tick within Δv range | ✅ | `CargoTransferProcessor` (every **1 min** `:17`) `Storage/CargoTransferProcessor.cs:45-63`; out-of-range early-out `if(dv_mps > transferRange)` `:54`; Δv gap via Hohmann `CalcDVDifference_m` `:192-227` | two holds transfer only if their Δv separation ≤ range; escrow model |
| 8.2 Transfer | SPACEPORT (surface↔orbit elevator, facility) | a colony-mounted `CargoTransferAtb` carrier | ✅ | `spaceport` template `GameData/basemod/TemplateFiles/storage.json:108` carrying `CargoTransferAtb` `:169` → contributes rate+range to the colony hold | loads ships from the colony fast; fixed to the colony, a footprint target |
| 8.2 Transfer | LAUNCH COMPLEX (ships to orbit, facility) | tonnage-gated pads, fuel-to-orbit | ✅ | `LaunchComplexProcessor` (daily `:16`) `Ships/LaunchComplexProcessor.cs:14`; `LaunchComplexAtb` / `LaunchComplexDB` (`Ships/`) | lifts whole SHIPS to orbit, deducts fuel-to-orbit; ship-elevator, not cargo |
| 8.2 Transfer | **AUTOMATED supply network** (logistics office) | `LogiBaseAtb.LogisicCapacity` + desired min/max | ✅ | `LogiBaseAtb.LogisicCapacity` `Logistics/LogiBaseAtb.cs:11`; creates `LogiBaseDB` on install `:12-29`; `LogiBaseDB.DesiredLevels` (min/max) `Logistics/LogiBaseDB.cs:17`, `ListedItems` `:18`; bidding ships `LogiShipperDB.States` `Logistics/LogiShipperDB.cs:11`; run by `LogiBaseProcessor`+`LogiShipProcessor` (every **6 h**) + `LogisticsCycle.LogiBaseBidding/LogiShipBidding` | set min/max stock; a **profit-based freight market** auto-hauls to keep colonies topped up (you cede control to the market) |
| 8.2 Transfer | faction-access gate on the market | who a freighter may service | ✅ | `LogisticsCycle.LogisticsAccessAllowed(baseFaction, shipFaction, game)` — same-faction OR base granted `RelationshipState.LogisticsAccess` (Logistics/CLAUDE.md:77; `DiplomacyDB`) | the "run your freighters through my supply network" treaty flag; defaults false = same-faction-only |
| 8.2 Transfer | **TOW / GRAPPLE mode** (E1b) | reuse transfer throughput to move a whole *entity* | ◐ **Wire** | `CargoTransferAtb` reused as tow throughput; towed mass drags the tug's `NewtonThrustAbilityDB` accel (the honest cost) — DIALS §E1b `COMPONENT-DESIGNER-DIALS.md:2513` | recover a drifting DERELICT (not just cargo). Re-parent the towed entity's `PositionDB` to the tug; blocked-on the exploration `SpawnWreck`/derelict content to have something to tow |
| 8.2 Transfer | **COVERT-INSERTION / infiltration bay** (E1d) | a bay that inserts an operative | ◐ **Wire** | mirrors `GroundTransport.TryLoadUnit/TryLandUnit` `GroundCombat/GroundTransport.cs:82/104` but gated by the **INVERSE** of `HasOrbitalControl` `:120` (insert where you DON'T hold orbit) + "attach as infiltrator" (hidden state) instead of a visible region roster — DIALS §E1d `COMPONENT-DESIGNER-DIALS.md:2535` | get an operative into a hostile system undetected (stealth/trade/diplomatic cover); interceptable by the live sensor loop; the effect lands in the espionage Information Ledger, never the fleet resolver |
| 8.2 Transfer | *missile transfer-range* | launcher in-range check | ◐ **wire (stub)** | `IFireWeaponInstr.IsInRange => true` `Weapons/IFireWeaponInstr.cs:25` (MissileLauncherAtb inherits) — always true; a proper Δv range check is deferred | affects the ordnance-resupply / firing picture; drawing a missile range ring today would lie |
| 8.2 Transfer | *`LogiBaseDB` Clone save-bug* | copy-ctor drops route state | ◐ **wire (bug)** | `LogiBaseDB` copy-ctor `Logistics/LogiBaseDB.cs:35-44` copies `ListedItems`/`ItemsWaitingPickup`/`TradeShipBids` but **NOT `DesiredLevels` (`:17`) nor `ItemsInTransit` (`:21`)** → a logistics office loses its min/max levels + in-flight cargo on Clone/save-load | latent: an automated route silently forgets what it was maintaining after a save |

---

### System insertion points

**The cargo/transfer machinery (the spine everything hangs on).**
- A hold is a `CargoStorageDB` (`Storage/CargoStorageDB.cs:11`) carrying one `TypeStore` per cargo type (`:79`). A component seeds/extends it via `CargoStorageAtb.OnComponentInstallation` (`Storage/CargoStorageAtb.cs:18`). Rate/range come from `CargoTransferAtb` components, summed/averaged by `StorageSpaceProcessor.CalcRateAndRange` (`Storage/StorageSpaceProcessor.cs:60,109`) into the DB's `TransferRate`/`TransferRangeDv_mps`.
- The move itself: `CargoTransferProcessor` runs every 1 minute (`Storage/CargoTransferProcessor.cs:17`), escrow-moves mass between two holds only when their Δv gap (Hohmann, `:192`) is inside the averaged range (`:54`), then re-derives fuel→Δv on both ends (`UpdateMassFuelAndDeltaV :168` → `newtdb.SetFuel :182`). **This is where fuel storage becomes propulsion.**

**The freight market (the automated network).** Install a logistics-office component (`LogiBaseAtb`, `Logistics/LogiBaseAtb.cs:9`) → it attaches a `LogiBaseDB` (`Logistics/LogiBaseDB.cs`) holding `DesiredLevels` (min/max). Two 6-hour hotloops (`LogiBaseProcessor`/`LogiShipProcessor`) run `LogisticsCycle.LogiBaseBidding`/`LogiShipBidding`: a base lists a shortage/surplus, a `LogiShipperDB`-bearing freighter (`Logistics/LogiShipperDB.cs`) computes profit = sell − fuel − transit, bids the most profitable run, then flies it on the normal Movement orders. Cross-faction service is gated by `LogisticsAccessAllowed` (diplomacy).

**The entity-TOW re-parent (E1b — new).** Insertion point: reuse `CargoTransferAtb` throughput as a tow rate, but instead of moving *cargo units* between `TypeStore`s, re-parent the towed entity's `PositionDB.Parent` to the tug and fold its mass into the tug's `MassVolumeDB` so `UpdateMassFuelAndDeltaV` (`CargoTransferProcessor.cs:168`) drags the tug's accel. The grave/honest cost is automatic (heavier tug = worse Δv). Prereq: exploration must actually spawn a derelict (`DamageProcessor.SpawnWreck` is a stub — DIALS §E1b).

**The infiltration "attach as hidden" mechanic (E1d — new), gated by inverse-`HasOrbitalControl`.** Troop landing (`GroundTransport.TryLandUnit`, `GroundCombat/GroundTransport.cs:104`) requires `HasOrbitalControl(ship, body) == true` (`:120` — no foreign ship over the target) and drops the unit onto the visible `GroundForcesDB` region roster (`:112`). The covert-insertion bay **removes/inverts that gate** — the whole point is inserting where you do NOT hold the orbit — and replaces the visible roster placement with an "attach as infiltrator" hidden state on the target colony (accumulating discovery heat, read by the espionage Information Ledger). (Nuance from `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md:105`: an *actively present* enemy fleet can still contest the insertion via the detection roll — so it's "you needn't own the orbit" rather than "orbit is irrelevant." The insertion roll = your stealth/agent skill vs their counter-intel; interceptable by the live `SensorScan` loop.) **The one genuinely new code is the attach-and-hide state** — a spy is a hidden *place on the map*, not a menu entry.

---

### The UPKEEP gap — the game has no fleet/army upkeep (only stations)

**The finding:** once built, a ship or ground unit costs **nothing** to keep — only fuel when it moves. The whole transaction taxonomy is `InitialInvestment / Research / ColonyTax / StationUpkeep` (`Factions/Ledger.cs:8-14`) — there is **no ShipUpkeep, no ArmyUpkeep**. Aurora's Maintenance Supply Points (MSP) are confirmed absent (Logistics/CLAUDE.md:71). So the developer's "store units at reduced upkeep" (mothball) has **nothing to discount**.

**Where station upkeep lives (the pattern to mirror):**
- `StationUpkeepProcessor` — `IHotloopProcessor` keyed on `StationEconomyDB`, runs every **30 days** (`Stations/StationUpkeepProcessor.cs:22-25`); `BillUpkeep` (`:40`) charges the owning faction `factionInfo.Money.AddExpense(..., TransactionCategory.StationUpkeep, cost)` (`:57-61`), no-op on a neutral/unowned station.
- `StationEconomyDB.OperatingCost(station)` — the pure formula (`Stations/StationEconomyDB.cs:53`): `Base 10 + PerModule 5×modules + PerFunction 25×distinctDesigns + PerPop 1×pop/1000` (`:28-31`, placeholders).

**What a ship/unit upkeep clock would hook into:** a new `IHotloopProcessor` (monthly, mirroring `StationUpkeepProcessor`) keyed on a blob every ship carries (e.g. `ShipInfoDB`) — landmine L9 forbids a second hotloop on a blob another processor already owns, so key it on its own or an unclaimed blob — that computes an operating cost from the ship's installed `ComponentInstancesDB` (same `AllComponents`/`AllDesigns` shape `OperatingCost` reads) and bills a **new `TransactionCategory.ShipUpkeep`/`ArmyUpkeep`** (add to `Factions/Ledger.cs:8-14`). The ground twin keys on `GroundForcesDB` (the planet-body roster) and bills per unit.

**The two-part mothball build:**
1. **Part 1 — the upkeep clock (net-new, but a mirror not a from-scratch).** Add the `TransactionCategory` + the processor above. This is worth building *on its own*: with no upkeep, a giant standing fleet has **zero downside**, so there's no pressure to demobilize, no logistics-strategy depth, no reason to ever mothball. Building upkeep CREATES the economic pressure that makes the whole reserve/reactivation decision matter. Stations already prove the pattern works.
2. **Part 2 — the reserve discount (a cheap wire on top).** A stored/active flag (a Storage dial → a state on the ship/unit) that scales the upkeep bill down, plus a **reactivation delay** to bring it back to the line. On a station this is a wire *today* (`OperatingCost` × a stored flag); on ships/units it becomes a wire the instant Part 1 exists.

---

### Prerequisite fixes & dead stubs (file:line)

| Fix | Where | Why it blocks |
|---|---|---|
| **Bare-hold silent no-op** (guard/bug) | fresh `new CargoStorageDB()` has empty `TypeStores` `Storage/CargoStorageDB.cs:33-36`; `CargoMath` add/remove then no-op silently (logging commented out) | a hold that "exists" but holds nothing; bit Stations Slice F (the deploy warehouse fix) |
| **`LogiBaseDB` Clone drops route state** (bug) | copy-ctor `Logistics/LogiBaseDB.cs:35-44` omits `DesiredLevels` `:17` + `ItemsInTransit` `:21` | an automated supply route silently forgets its min/max + in-flight cargo across a save/entity-transfer |
| **Missile transfer-range stub** | `IFireWeaponInstr.IsInRange => true` `Weapons/IFireWeaponInstr.cs:25` (inherited by MissileLauncherAtb) | missiles always "in range"; a real Δv range check is deferred (do not draw a missile ring — it would lie) |
| **No fleet/army upkeep** (missing system) | `TransactionCategory` `Factions/Ledger.cs:8-14` has no Ship/Army term | blocks mothball Part 1; is itself the missing economic pressure |
| **`SpawnWreck` stub** (blocks TOW content) | `DamageProcessor.SpawnWreck` (`:478`, deletes the ship) per DIALS §E1b `:2512` | nothing to tow/salvage until a real derelict exists |

---

### Design essence captured inline

**Transient-find recovery (E1b — Exploration).** The merged `EXPLORATION-CONTENT-DESIGN.md` adds things worth *going to get*: drifting derelicts, lost colonies, wrecks on decaying orbits. You recover them — strip for mass, recover a ship design, recover **survivors** (a population/leader source) — but a transient find is a *timing* decision (a decaying orbit or a periodic comet gives you a window; reuses the hazard-transience seam), and the best loot **hides in hazards** (a derelict in a gas cloud only a counter-interference scanner can find). The Transfer TOW/GRAPPLE mode is the gear that makes "recover a whole entity" real: the tug's own physics pays the cost (a heavy derelict wrecks your Δv). Almost pure CONNECT — the hazard system and the "gravitational anomaly" naming already exist; the derelict content and `SpawnWreck` are the missing rungs.

**Covert physical delivery (E1d — Espionage).** The merged `ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` makes agents PHYSICAL: pick a cover (stealth = run EMCON-dark; trade = hide in legit traffic, needs `LogisticsAccess`; diplomatic = insert via an embassy) → run the route → an **insertion roll** (your stealth + agent skill vs their counter-intel) → operate while **discovery heat builds** → extract or embed. The covert-insertion bay is the delivery hardware, and it deliberately **inverts** the troop-landing gate: a marine lands where you HOLD the orbit; a spy inserts where you DON'T. The one genuinely new mechanic is "**attach as infiltrator**" — instead of dropping onto a visible region roster, the agent attaches hidden to the target colony, becoming an embedded *place on the map* that accumulates risk. Its payload (bioweapon, cipher bomb, assassination toxin) lands in population/legitimacy/espionage, never the fleet resolver — the intersection weapon that needs a multitude of systems to pull off.

---

### §0g stamp — the three acceptance criteria

- **Reachable (cradle-to-grave).** Every Logistical capability is a **component** on the normal rails: mineral → material → a hold/bay/transfer-arm/logistics-office DESIGNED in the component designer (`CargoStorageAtb`/`CargoTransferAtb`/`GroundBayAtb`/`GroundMagazineAtb`/`LogiBaseAtb`, all `IComponentDesignAttribute`) → research-gated → built at a colony → installed → in-play decision (what to hold, how fast to move, manual vs automated route) → **a hit spills the hold / a bay shot off takes its units / a damaged transfer arm stops contributing** (`StorageSpaceProcessor.cs:75` health gate). The spaceport/logistics-office/launch-complex facilities ride the same chain. (Mothball's grave rung is blocked on the upkeep clock — the one net-new rung.)
- **Mirrored (NPC too).** NPCs haul, tow, and insert on the same code: the freight market is faction-agnostic (any `LogiShipperDB` freighter bids, gated only by `LogisticsAccess`); `GroundTransport` load/land is owner-neutral; the covert-insertion mirror is explicit in the espionage doc ("NPC gathers on you; you detect + react"). No player-only path.
- **Observable (the gauges).** The player can SEE it: `CargoStorageDB.TotalStoredMass` + per-`TypeStore` counts (the stockpile readout), fuel→Δv on `NewtonThrustAbilityDB` (the range gauge), `TransferRate`/`TransferRangeDv_mps` (the throughput/reach numbers), `LogiBaseDB.DesiredLevels`/`ItemsInTransit` (route state), `StationEconomyDB.LastOperatingCost` (the upkeep readout the ship/army clock would mirror). The one thing you must NOT show is a missile range ring (the stub would lie).

---

### Cross-category shared state (Prime Directive)

- **Logistical × Industrial [FEED].** The hold is the buffer between them: Industrial mines/refines/builds INTO a `CargoStorageDB`; Logistical moves it out. Shared state = `CargoStorageDB.TypeStores` (a mine deposits, a transfer arm withdraws) and the material stockpile the industry consume-loop (`IndustryTools.ConsumeResources`) drains — the exact same hold the freight market tops up. Ordnance storage feeds the weapon magazines; ground magazines feed `GroundAmmo`.
- **Logistical × Chassis [carrier/bay].** A troop/vehicle bay (`GroundBayAtb`) and a covert-insertion bay are components mounted on a hull — the carrier hole (H6): a chassis that HOUSES and LAUNCHES smaller units. Shared state = `ComponentInstancesDB` (the bay is an installed component summed on demand by `GroundTransport.BayCapacity`) + `GroundTransportDB.LoadedUnits` (who's aboard). Mass of the loaded units feeds the chassis `MassVolumeDB` → Δv.
- **Logistical × Espionage [insertion].** The covert-insertion bay is Espionage·Delivery: it shares the `GroundTransport` load/land primitives but writes to the espionage Information Ledger (hidden infiltrator state) instead of the visible `GroundForcesDB` roster, and its gate is the INVERSE of `HasOrbitalControl` read by the sensor/detection loop. Shared state = the target colony (a hidden attach) + the detection roll (sensors × counter-intel).

---

### Holes owned/resolved (H1, H6) → status + home

- **H1 — Matter teleportation** (transporter / ring transport / Asgard beaming / the Stargate itself; recurs in ALL THREE franchises — the most *recurring* hole). **Status: OPEN, home assigned.** Its home is a **Transfer ▸ "ranged/teleport" mode** — instant, ranged, capacity-limited (`COMPONENT-DESIGNER-CATEGORIES.md` §5, H1, priority HIGH). It is NOT normal Transfer (which is local + Δv-rate-limited via `CargoTransferProcessor`'s Hohmann gate `:192`) and NOT Warp (which moves the whole chassis). Wiring seam: a Transfer mode that bypasses the `dv_mps > transferRange` early-out (`CargoTransferProcessor.cs:54`) with an instant capacity-limited move; cousin to **H8 (gate networks)** — both are "instant point-to-point," one for matter, one for the whole ship. The covert-insertion bay is an adjacent, lower-tech instance of the same "move a passenger a way the normal hold can't" idea.
- **H6 — Carrier / nested assemblies** (Star Destroyer TIEs, Ha'tak gliders, AT-AT troops; a chassis that houses & launches smaller chassis). **Status: RESOLVED (mostly), one verify item.** Home = **Logistical ▸ Storage holds units, Transfer launches** — and it is BUILT for ground: `GroundBayAtb` holds units (`GroundCombat/GroundBayAtb.cs:34`), `GroundTransport.TryLoadUnit`/`TryLandUnit` (`:82`/`:104`) are the load/launch. `COMPONENT-DESIGNER-CATEGORIES.md` §5 grades H6 LOW-MEDIUM ("mostly covered — verify nested-chassis + launch/recover actually resolves"). **The open verify:** a bay carries ground *units* (data objects) today; carrying/launching a smaller *chassis* (a fighter as its own entity) is the un-verified nested-assembly case — the same re-parent primitive the TOW mode needs (`PositionDB.Parent`). Recommend the same wire for both.

---

## §9 — Civic

Civic is the **human backbone** — the colony and its people. It's the category everything else ultimately runs on: **population** becomes the workforce that mans your factories and the crew that flies your ships (and the scarce **talent** that makes an Enhancers elite); **research** unlocks the tech that gates every other door; **morale** decides whether people stay or flee. Two doors: **Habitation** (what lets people *live* somewhere — on a planet or sealed off-world) and **Development** (what makes a colony *better* — research, training, and improving the world itself). Pure non-combat, and a **mixed** modellability picture: Habitation is mostly Modelled and load-bearing; Development is split between a working tech/training engine and a **missing deep half** (you can't yet improve a world).

**The yardstick — the colony/population system** (`InfrastructureProcessor` + `PopulationProcessor` + `ResearchProcessor` + `ColonyMoraleDB`), not the combat resolver. And the standout finding: **infrastructure is a real, live economy multiplier** — under-build it and your whole colony works at a fraction.

### §9.0 Shared civic dials (both doors)
On top of the universal seven (§0a):
| Dial | Drives (real stat) |
|------|--------------------|
| **What it supports** | population (life-support) · efficiency (infrastructure) · morale (comfort) · output (research/training) |
| **Capacity / rate** | how much — pop ceiling, efficiency capacity, research points/day |
| **Host + world type** | colony (tolerance-gated by gravity/pressure) OR sealed station-habitat (no tolerance gate) |
| **Tolerance gating** | a hostile world (`ColonyCost > 0`) divides carrying capacity — you need MORE to hold the same pop |
| **Grave rung** | a bombed colony loses infrastructure → efficiency craters → the economy collapses |

### 9.1 Civic ▸ HABITATION  🔒 *locked*
*Let people live and work somewhere — the life-support, infrastructure, and housing that turn a barren rock or an orbital shell into a place a population survives, works efficiently, and stays content. Everything from a starter colony's infrastructure to a hostile-world life-support complex to a sealed O'Neill space-habitat falls out of these dials — and the three things habitation provides are genuinely distinct: **can they live here** (capacity), **how well do they work** (efficiency), **how content are they** (morale).*

**The core decision — CAPACITY vs EFFICIENCY vs COMFORT, against the world's hostility.** Three separate stats, and you invest in each: **life-support** raises how many people the colony can hold (and on a hostile world that ceiling is *divided* by how hostile it is); **infrastructure** raises how efficiently they produce (a live multiplier on your whole economy); **housing/comfort** raises morale (content, not more alive). A friendly homeworld needs little; a Mars or a deep-space station needs a lot.

**A. Life-support / population capacity (`PopulationSupportAtbDB.PopulationCapacity`)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Light** | cheap on a friendly world (native worlds are uncapped anyway) | on a **hostile** world the pop ceiling is `support ÷ colonyCost` — under-build it and pop over the cap **dies off (−50%)** |
| **Heavy** | holds a big population on Mars/Venus/a moon | costly, massive; and it's **tolerance-gated** (out-of-gravity/pressure → 0) |

**B. Infrastructure / efficiency (`InfrastructureCapacityAtb.Capacity`) — the economy multiplier**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Under-built** | cheap now | **your whole colony works at a fraction** — `Efficiency = Provided ÷ Required` scales EVERY industry + mining rate |
| **Well-provisioned** | full efficiency — factories and mines run at 100% | ongoing footprint; efficiency caps at 1.0 (no over-building bonus) |

**C. Housing / comfort (`HousingAtbDB.Comfort`) — morale**
| Option | Why | Catch |
|--------|-----|-------|
| **Basic** | cheap — people live, if grumbly | lower morale → emigration, lower income |
| **Comfortable** | +morale (capped +20) → people stay, income rises | doesn't raise the pop cap — "content, not alive" |

**D. Sustenance — power & food (`ColonySustenanceDB`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Provisioned** | power + food keep people alive | ⏳/◐ **currently INERT** — `PerCapitaPowerDemand`/`FoodDemand` default 0, and **no food good exists**; starvation is switched off until calibrated (+ food is net-new) |

**E. World type / medium — where the habitat sits**
| Option | Why | Catch |
|--------|-----|-------|
| **Native world** | uncapped population, no colony-cost | only your homeworld(s) |
| **Hostile world** | colonize Mars/Venus — capacity ÷ colony-cost | needs heavy life-support; die-off over cap |
| **Sealed habitat** (space-habitat) | houses population on **ANY body** — no gravity/pressure gate (the O'Neill/orbital) | every person depends on the shell; no module → cap 0 → die-off |

**Modellability audit (§0d — mostly Modelled + load-bearing; two inert/dead spots):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Infrastructure / efficiency** | ✅ **(load-bearing)** | `InfrastructureCapacityAtb.Capacity` → `InfrastructureDB.Efficiency` (Provided÷Required, cap 1.0) → **scales ALL production (`IndustryTools`) + mining (`MineResourcesProcessor`)** |
| Life-support / pop capacity | ✅ | `PopulationSupportAtbDB.PopulationCapacity` → `ColonyLifeSupportDB.MaxPopulation`; hostile-world ÷colonyCost + over-cap die-off (`PopulationProcessor`) |
| Housing / comfort | ✅ | `HousingAtbDB.Comfort` → `MoraleInputs.Comfort` → morale (cap +20) |
| Space-habitat (off-world) | ✅ | `space-habitat` carries the same attrs with **no tolerance gate** → `StationPopulationProcessor` (parallel to colony) |
| **Sustenance (power/food)** | ◐ **inert** / ⏳ | `ColonySustenanceDB` demand defaults 0; **food good doesn't exist** — starvation is off until calibrated (food = net-new) |
| **Employment (a morale input)** | ◐ **data-dead** | `EmploymentAtbDB.Jobs` is read by morale, but **no base-mod template grants it** → jobs = 0 → employment always neutral. A wire: grant jobs on the work buildings |

**Reading:** Habitation is **mostly Modelled and quietly load-bearing** — the headline is that **infrastructure is a real economy multiplier** (an under-infrastructured colony genuinely produces less across the board), population-support is real carrying capacity (with the hostile-world colony-cost division and over-cap die-off that make Mars *hard*), housing-comfort is real morale, and sealed space-habitats extend all of it off-world. Two spots are switched off: **sustenance** (power/food → starvation exists but is inert — a calibration, and food itself is net-new) and **employment** (built but data-dead — the morale system asks "are people employed?" but nothing grants jobs, so it always reads neutral; a cheap wire).

**Numbers:** `PopulationCapacity` (pop at ColonyCost 1.0; infra reference 10,000); `InfrastructureCapacityAtb.Capacity` (support units; "Support Capacity" 1000); `HousingAtbDB.Comfort` (5 default → cap +20 morale); required-capacity `= mass/1000 + crew` per installation. Cradle-to-grave: habitation is **built installations** — bomb them and efficiency/capacity collapse (the grave rung that makes a colony *takeable*).

**Preset coordinates:** colony infrastructure · arcology *(high pop-support)* · habitat domes *(comfort → morale)* · **sealed space-habitat** *(off-world, any body)* · **hostile-world life-support** *(heavy — the Mars/Venus complex)*.

### 9.2 Civic ▸ DEVELOPMENT  🔒 *locked*
*Make the colony BETTER over time — research new tech, train officers, and improve the world itself. Everything from a research lab to a naval academy to a **terraforming station** falls out of these dials. **Split status:** the research + training engines are Modelled and load-bearing, but the deep half — actually improving a WORLD (terraforming, development level) — is MISSING, and it's the franchise-earning one.*

**The core decision — WHAT to grow: knowledge, people, or the world.** A colony's development capacity can go into **research** (the tech that unlocks every other door), **officer training** (the academy that produces commanders), or **improving the planet** (terraforming a hostile world toward habitable — the one that isn't built). You choose what a colony specializes in.

**A. Research (`ResearchPointsAtbDB`: `PointsPerEconTick` · `CostPerDay` · `BonusCategory`) — the tech engine**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **General lab** | steady research → tech unlocks (which gate every door) | costs upkeep (`CostPerDay`) |
| **Specialized institute** | a **bonus category** (+10% to one field) — a weapons lab, a propulsion institute | narrow — only boosts its specialty |

**B. Officer training (`NavalAcademyAtb`: `ClassSize` · `TrainingPeriodInMonths`)**
| Option | Why | Catch |
|--------|-----|-------|
| **Academy** | produces **officers** (graduates → commanders) | colony-bound (PlanetInstallation); *(placement note: the academy BUILDING is Civic ▸ Development; the officer's combat EFFECT is Enhancers ▸ Unit-Caliber — build here, consume there)* |

**C. World development / terraforming — improve the WORLD (the missing deep half)**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Terraform** | lower a hostile world's **colony-cost** over time → hold more pop, need less life-support (Mars-green, Dune, the 4X terraform) | ⏳ **MISSING (net-new)** — `AtmosphereDB.Composition` supports it, the hook is designed, but **no `TerraformingProcessor` exists** |
| **Develop** (level up a colony) | raise a colony's baseline output/capability over time | ⏳ **MISSING** — `ColonyBonusesDB` is an empty shell (only reads faction-wide bonuses); no per-colony development ladder |

**Modellability audit (§0d — split: engine Modelled, world-improvement missing):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| **Research** | ✅ **(load-bearing)** | `ResearchPointsAtbDB` → `ResearcherDB` → `ResearchProcessor` (daily) → tech unlocks; **host-agnostic** (colony OR station), `BonusCategory` for specialties |
| Officer training (production) | ✅ | `NavalAcademyAtb` → `NavalAcademyProcessor` → graduates commanders |
| **Terraforming** | ⏳ **MISSING (net-new)** | hook designed (`AtmosphereDB.Composition`) but **no processor** — the franchise-earning world-improvement mechanic |
| **Development level / growth** | ⏳ **MISSING** | `ColonyBonusesDB` empty shell; the progression ladder is doc-only |

**Reading:** Development is **split down the middle.** The **research engine is Modelled and load-bearing** — labs produce the tech that gates literally every other category's dials, host-agnostic, with specialty bonuses; and the **academy** produces officers (its *building* is here; the officer's *combat effect* is the Enhancers net-new). But the **deep half is missing**: you can *settle* a hostile world (Habitation) but you can't *improve* it — **terraforming and colony development don't exist** (hooks designed, no processor). That's the franchise-earning gap (turning Mars green, the Dune dream, every 4X's terraform), and it's the honest headline for this door.

**Numbers:** `ResearchPointsAtbDB.PointsPerEconTick` (research/day) + `CostPerDay` + `BonusCategory` (+10%); `NavalAcademyAtb.ClassSize` (10–2500) + `TrainingPeriodInMonths` (1–48). Terraforming/development define their own rates when built. Cradle-to-grave: labs/academies are **built installations**; terraforming would be a slow world-change over years.

**Preset coordinates:** research lab · specialized institute *(bonus category)* · naval academy · **terraforming station** *(⏳ missing — the world-improver)* · development office *(⏳ missing)*.

---

## ✅ §9 Civic — COMPLETE (2/2 doors locked)
Habitation 🔒 · Development 🔒. **The human backbone — MIXED, and full of connections.** Headline readings: **Habitation is mostly Modelled and quietly load-bearing** — the standout is that **infrastructure is a REAL economy multiplier** (`InfrastructureDB.Efficiency` scales ALL industry + mining, so an under-built colony genuinely produces less), population-support is real carrying capacity (hostile-world ÷colony-cost + over-cap die-off — what makes Mars *hard*), housing-comfort is real morale, and sealed **space-habitats** house population off-world on any body; two spots are switched off — **sustenance** (power/food → starvation, inert until calibrated; food is net-new) and **employment** (built but **data-dead** — morale reads jobs, no template grants them → always neutral; a cheap wire). **Development is split** — **research** (the tech engine that gates every other door, ✅ host-agnostic + specialties) and **officer training** (academy, ✅ production; its combat *effect* is the Enhancers side) work, but the **deep half — improving the WORLD (terraforming / colony development) — is MISSING** (net-new; hooks designed, no processor). That's the franchise-earning gap (turn Mars green / Dune / the 4X terraform). **The cross-connects make Civic the hub:** population → the **manpower/talent pool** that mans industry, crews ships, AND supplies the scarce talent that gates the **Enhancers unit-caliber ELITES** (a Space Marine draws talent a colony produces); research → the tech that unlocks **every door**; morale → migration → stability → `LegitimacyDB`/rebellion. Build-list: (1) **terraforming / world-development** (the missing deep half — franchise-earning); (2) calibrate **sustenance** + add a **food good** (turn starvation on); (3) grant **`EmploymentAtbDB.Jobs`** in templates (un-dead employment → real morale); (4) a **per-colony development ladder** (`ColonyBonusesDB` is an empty shell).

---

## ⚙ 9 — CIVIC · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Civic is the **human backbone**: the colony and its people. Two doors — **Habitation** (§9.1: what lets people *live* somewhere — life-support, infrastructure, housing, sealed off-world habitats) and **Development** (§9.2: what makes a colony *better* — research, officer-training, and improving the world itself). It is the category everything else runs on: population becomes the **workforce** that mans factories and the **crew** that flies ships, plus the scarce **talent** that gates the Enhancers elites; research unlocks the **tech that gates every other door**; morale decides whether people stay or flee. Modellability is **MIXED**: Habitation is mostly Modelled and quietly load-bearing (infrastructure is a REAL economy multiplier); Development is split — research + academy work, but the deep half (terraforming / world-improvement) is MISSING.

The yardstick is **the colony/population system**, not the combat resolver: `InfrastructureProcessor` + `PopulationProcessor` + `ResearchProcessor` + `ColonyMoraleDB` / `SustenanceProcessor`.

### Pillar tags (§0h)
Per the E0a PROJECTOR⛓COUNTER schema, every door carries two metadata tags — `pillar` (Military / Espionage / Diplomacy / Influence) and `skeleton-role` (Projection / Counter / **Medium** / Detection). Civic is graded against the **population/economy resolver**, never the combat resolver.

- **`pillar` = Economy/Population (the substrate the other pillars draw on).** Civic is not a weapon-pillar; it is the **HUB the pillars are fed from**.
- **`skeleton-role` = MEDIUM.** Civic is the population/economy *medium* — the reservoir. It emits two flows the rest of the designer consumes: **talent → Enhancers** (§6.1/§6.2 elites draw the scarce `ColonyManpowerDB.TalentPool` a colony produces) and **research → every door** (tech unlocks gate every category's dials). It is not itself a Projection or a Counter; it is the tank both sides pull from.
  - Sub-role nuance: the **academy** is a *talent-Projection generator into the leader pipeline* (rung-1 competence), and **terraforming** (when built) would be a *Projection onto the WORLD* (a component that mutates the planet). Neither exists as a weapon; both are Medium-internal transforms.
- **Counter side:** Civic's "counter" is the **grave rung** — a bombed colony loses infrastructure → `Efficiency` craters → the whole economy collapses; the counter to your talent/research engine is *hitting the colony that produces it* (routes through the Damage system, `DamageProcessor.OnColonyDamage`).

### Complete dial → derived-stat → engine-wire table (VERIFIED)

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion |
|------|------|--------------|-------|--------------------------|------------------|
| §9.1 Habitation | **Life-support / pop capacity** | `PopulationSupportAtbDB.PopulationCapacity` (pop at ColonyCost 1.0; infra ref 10 000) | ✅ Modelled | `Galaxy/PopulationSupportAtbDB.cs:18` → summed on demand `Engine/Components/ComponentInstancesDBExtensions.cs` `GetPopulationSupportValue` (tolerance-gated) → `Colonies/ColonyLifeSupportDB.MaxPopulation` set in `PopulationProcessor.ReCalcMaxPopulation` `:158,167` | Sets the pop ceiling. Hostile world: ceiling `= support ÷ colonyCost` (`PopulationProcessor.cs:115`); over cap → **die-off** (`growthRate = -50.0`, `PopulationProcessor.cs:118-119` → floored at 0). This is what makes Mars *hard*. |
| §9.1 Habitation | **Infrastructure / efficiency** (the economy multiplier) | `InfrastructureCapacityAtb.Capacity` → `InfrastructureDB.Efficiency` = Provided÷Required, capped 1.0 | ✅ **Modelled (load-bearing)** | `Industry/InfrastructureCapacityAtb.cs`; `Industry/InfrastructureDB.cs:30-38` (Efficiency getter); summed in `Industry/InfrastructureProcessor.cs:38-39,53,86`; **consumed** at `Industry/IndustryTools.cs:115` (scales EVERY production rate) and `Industry/MineResourcesProcessor.cs:64,71` (scales EVERY mining rate) | THE standout: under-build infra and the *whole colony works at a fraction*. Provided sums only in-tolerance, enabled, health-scaled infra components (`InfrastructureProcessor.cs:63-83`); Required = `mass/1000 + crew` over every OTHER installation (`:86-101`). |
| §9.1 Habitation | **Housing / comfort** | `HousingAtbDB.Comfort` (5 default → cap +20 morale) | ✅ Modelled | `Colonies/HousingAtbDB.cs:22`; summed `ComponentInstancesDBExtensions.GetHousingComfort` (`:35`); read `PopulationProcessor.cs:77` → `MoraleInputs.Comfort` → `ColonyMoraleDB.ComputeMorale` | Morale (content, not more alive). Doesn't raise the pop cap. |
| §9.1 Habitation | **Sustenance — power & food** | `ColonySustenanceDB.PerCapitaPowerDemand` / `PerCapitaFoodDemand` → `PowerShortage`/`FoodShortage` | ◐ **INERT** | `Colonies/ColonySustenanceDB.cs:21,23` (both default **0.0**), `:31` MaxStarvationDeathRate 0.10; `Colonies/SustenanceProcessor.cs:49-57` computes shortage; read `PopulationProcessor.cs:52-57` (starvation), `:88-98` (morale). **No food good exists.** | Starvation is *switched off* until demand is calibrated + a food good is added (net-new). Ships neutral (demand 0 → shortage 0). |
| §9.1 Habitation | **Employment** (a morale input) | `EmploymentAtbDB.Jobs` → `EmploymentRatio` | ◐ **DATA-DEAD** | `Colonies/EmploymentAtbDB.cs:20`; summed `ComponentInstancesDBExtensions.GetTotalJobs` (`:19,24`); read `PopulationProcessor.cs:74-76` (jobs÷workforce, sentinel **-1** = no data); `ColonyMoraleDB.cs:126-133,188` (neutral when negative). **No base-mod template grants Jobs** (grep of `Pulsar4X/GameData/**.json` for `Employment`/`Jobs` = zero hits). | Morale asks "employed?" but nothing grants jobs → ratio always -1 → neutral. A cheap wire: add `Jobs` to work-building templates. |
| §9.1 Habitation | **World type / medium** (native / hostile / sealed habitat) | `space-habitat` template carries the same attrs with **no tolerance gate** | ✅ Modelled | Templates: `GameData/basemod/TemplateFiles/installations.json`, `GameData/basemod/ScenarioFiles/systems/sol/earth.json`; parallel host `Stations/StationPopulationProcessor.cs:17,26,58-60` (`GetPopulationSupportValue`, capacity by module NOT ColonyCost) | Sealed habitat houses pop on ANY body — no gravity/pressure gate. Every person depends on the shell; no module → cap 0 → die-off. |
| §9.2 Development | **Research** (general lab / specialized institute) | `ResearchPointsAtbDB.PointsPerEconTick` · `CostPerDay` · `BonusCategory` (+10%) | ✅ **Modelled (load-bearing)** | `Tech/ResearchPointsAtbDB.cs:13,17,25`; installed → `Tech/ResearcherDB.cs` (`PointsPerDay` `:16`, `BonusCategories` `:24`, `CostPerDay` `:29`, `FundingLevel` 0-5 `:41`, `ScientistId` `:53`); daily loop `Tech/ResearchProcessor.cs:87` (points), `:180-184` (scientist bonus) → tech unlocks | **Host-agnostic** (colony OR station). The tech engine that gates literally every other door's dials. Specialty = `BonusCategory`. |
| §9.2 Development | **Officer training** (academy) | `NavalAcademyAtb.ClassSize` (10-2500) · `TrainingPeriodInMonths` (1-48) | ✅ Modelled (production) | `People/NavalAcademyAtb.cs:11-12`; `People/NavalAcademyProcessor.cs:18-32` (graduate loop; `CommanderFactory.CreateAcademyGraduate` `:21`; `ExperienceCap` on `NextBellCurve` `:32`) | **The BUILDING is Civic ▸ Development; the officer's combat EFFECT is Enhancers ▸ Unit-Caliber** — build here, consume there. Gear-vs-being boundary: the school is gear, the graduate is a Being (People). |
| §9.2 Development | **Terraforming** — improve the WORLD | lower a hostile world's colony-cost over time | ⏳ **MISSING (net-new)** | Hook: `Galaxy/AtmosphereDB.cs:67` `Composition` (Dict) + `:44` `GreenhousePressure`; the number it would lower is read in `People/SpeciesDBExtensions.cs:30` `ColonyCost`. **NO `TerraformingProcessor` exists** (confirmed below). | The franchise-earning gap (Mars-green, Dune, the 4X terraform). Would be a slow world-change over years. |
| §9.2 Development | **Development level / growth** | raise a colony's baseline output over time | ⏳ **MISSING** | `Colonies/ColonyBonusesDB.cs:10-30` is an **empty shell** — `GetBonus` only reads faction-wide `FactionAbilitiesDB.AbilityBonuses` (`:14`); sector/planet/race bonuses are commented out (`:28`). No per-colony ladder. | Doc-only progression. |
| **§E1a essence-ext** — §9.2 Development | **★ Academy Type/Tier + Competence-Investment** — what leader a school makes (Navy/Ground/Civil/Science/Covert × school/college/university × mass-vs-elite) | school produces a graduate with a **competence roll** written into their `BonusesDB` | ◐ **Wire** | `NavalAcademyProcessor` already rolls `NextBellCurve` (`People/NavalAcademyProcessor.cs:32`); wire = write the roll into the empty `People/BonusesDB.cs:46` (`Bonuses` list; rung-4 competence reads it) **+ draw scarce `Colonies/ColonyManpowerDB.cs:48` `TalentPool`** — the SAME handle that gates §6.2 Enhancers elites (build once, both light up). | Rung-1 competence generator → the leader pipeline. Type = which chain (Navy/Ground/Civil/Science/Covert); Tier = ClassSize÷TrainingPeriod trade; Investment = mass-vs-elite = how much talent it burns. |
| **§E1b essence-ext** — §9.2 Development | **★ Field-Lab deployment** — lab at home vs mobile ship-lab vs droppable field-station (hosts a scientist posting at a site) | a deployment flag + a `siteId` target on the researcher | ◐ **Wire** | Reuses `Tech/ResearcherDB.cs` (has `ScientistId` `:53`, `FundingLevel` `:41`, `BonusCategories` `:24`) + funding machinery `Tech/ResearchProcessor.cs:87,180`. Wire = **add a deployment flag + a `siteId` field** (ResearcherDB has NO siteId today) so the same lab loop runs on a location. | "A lab on a location" — the field-site loop (§0i) is the lab loop reused. Hosts the scientist posting that resolves an anomaly / xeno-dig / ruins at a site. |

### System insertion points

**1. `InfrastructureDB.Efficiency` — the economy multiplier (the load-bearing seam).**
`InfrastructureProcessor.SumProvidedCapacity` sums in-tolerance, enabled, health-scaled infra components (`InfrastructureProcessor.cs:53-83`); `SumRequiredCapacity` sums `mass/1000 + crew` over every *other* installation (`:86-101`); `Efficiency = Provided÷Required` capped 1.0 (`InfrastructureDB.cs:30-38`). It is READ by exactly two consumers via `InfrastructureProcessor.GetEfficiency` (`:46`): **all production** (`IndustryTools.cs:115` — scales every `IndustryTypeRates` value) and **all mining** (`MineResourcesProcessor.cs:64,71`). Grave rung: bomb the infra components → Provided drops → Efficiency < 1 → economy craters. **This is the single most load-bearing wire in Civic — any change to how infra is summed or consumed touches the entire economy.**

**2. The research / `ResearcherDB` loop.** `ResearchPointsAtbDB` (component design) instantiates a `ResearcherDB` on the host; `ResearchProcessor` runs daily (`:87` points, `:180-184` scientist bonus, gov modulator `:91`), draining `CostPerDay` and adding `PointsPerDay` to a queued tech until unlock. Host-agnostic (colony or station). This is the loop the **field-lab dial** rides (add a `siteId`), and the loop **every other door depends on** (tech gates every dial).

**3. The academy competence generator.** `NavalAcademyProcessor` (an `IInstanceProcessor`, fires at graduation) loops `ClassSize` times, calls `CommanderFactory.CreateAcademyGraduate`, and rolls `ExperienceCap` on a bell curve (`:32`). The **Type/Tier essence-wire** hooks HERE: the roll already exists; write it into `BonusesDB` (rung-4 competence) and debit `ColonyManpowerDB.TalentPool` (`ColonyManpowerDB.cs:48`, = population × TalentFraction; `AvailableTalent` `:54` = pool − CommittedTalent).

**4. The field-lab `siteId` host.** `ResearcherDB` today has `ScientistId` (`:53`) but **no siteId** — that is the missing field. The field-lab dial adds a deployment flag + `siteId` so the ResearcherDB loop runs at a location (a lab dropped on a ruins/anomaly site), which is the exploration field-site loop (§0i) reusing the lab loop verbatim.

### The TERRAFORMING gap + the switched-off spots

**TERRAFORMING — confirmed ABSENT.** There is **no `TerraformingProcessor`** anywhere in the engine (`find -iname "*terraform*"` = nothing; the only `.cs` hits are inert): `AbilityType.Terraforming` (`Engine/DataStructures/Enums.cs:242`), a faction-wide `terraformingBonus` with no consumer (`Factions/FactionAbilitiesDB.cs:36,54`), two unused events `TerraformingCompleted`/`TerraformingReport` (`Engine/Events/EventTypes.cs:114-115`), a doc-comment link (`Galaxy/AtmosphereDBExtensions.cs:10`), and a `//do Terraforming` comment in the damage path (`Damage/DamageComplex/DamageProcessor.cs:162`). **What a terraform component would hook into:** it would mutate `AtmosphereDB.Composition` (`Galaxy/AtmosphereDB.cs:67`) + `GreenhousePressure` (`:44`) over years; that flows into `SpeciesDBExtensions.ColonyCost` (`People/SpeciesDBExtensions.cs:30`, which reads the atmosphere) → lowering ColonyCost raises the pop ceiling in `PopulationProcessor.cs:115` and lifts infra tolerance gates. So the entire *downstream* plumbing (ColonyCost → capacity → die-off) already exists; the ONLY missing piece is a processor that slowly writes `Composition`. This is the franchise-earning "improve a world" arc (Mars-green / Dune / every 4X terraform) — the honest headline for Development.

**Sustenance — INERT (calibration, + food is net-new).** `ColonySustenanceDB.PerCapitaPowerDemand`/`PerCapitaFoodDemand` default 0.0 (`ColonySustenanceDB.cs:21,23`), so `SustenanceProcessor` (`:49-57`) always computes zero shortage; starvation (`PopulationProcessor.cs:52-57`) and the power/food morale inputs (`:88-98`) read neutral. Deliberately avoids the "a default deficit tanks every colony" trap (`ColonySustenanceDB.cs:14`). Turn-on = calibrate demand + add a **food good** (net-new — no food cargo type exists).

**Employment — DATA-DEAD.** `EmploymentAtbDB.Jobs` (`EmploymentAtbDB.cs:20`) is summed (`GetTotalJobs`, `ComponentInstancesDBExtensions.cs:19`) and read by morale (`PopulationProcessor.cs:74-76` → sentinel -1 when jobs 0 → `ColonyMoraleDB.cs:128-133` neutral), but **no base-mod JSON template grants Jobs** (grep confirmed zero). Cheap wire: add `Jobs` to the work-building templates → employment morale goes live.

### Prerequisite fixes & dead stubs (file:line)
- **`ColonyBonusesDB` is an empty shell** — `Colonies/ColonyBonusesDB.cs:10-30`: `GetBonus` only reads faction-wide `FactionAbilitiesDB.AbilityBonuses`; the per-colony sector/planet/race path is a commented-out line (`:28`). The "development level" ladder must fill this (or replace it).
- **`ResearcherDB` has no `siteId`** — `Tech/ResearcherDB.cs:53` has `ScientistId` only. The field-lab dial must add a `siteId` + deployment flag.
- **No `TerraformingProcessor`** — the whole world-improvement processor is missing; hooks (`AtmosphereDB.Composition`, `AbilityType.Terraforming`, `TerraformingCompleted` event) are vestigial with no producer.
- **Sustenance demand defaults 0** — `ColonySustenanceDB.cs:21,23`; and no food cargo good exists.
- **No template grants `EmploymentAtbDB.Jobs`** — data gap, not a code stub.
- **Academy roll not persisted to competence** — `NavalAcademyProcessor.cs:32` rolls `ExperienceCap` but nothing writes a competence bonus into `BonusesDB` (the rung-4 target, `People/BonusesDB.cs:46`).

### Design essence captured inline
**The academy as rung-1 competence generator (`AI-SELF-PLAY-DESIGN.md`):** the academy BUILDING is Civic ▸ Development gear (a school), but the *graduate* is a Being who enters the leader pipeline (People). The essence adds a **Type/Tier + Competence-Investment** dial: a school is Navy/Ground/Civil/Science/Covert × school/college/university × mass-vs-elite, and the quality of graduate it stamps is a competence roll written into the officer's `BonusesDB` (which rung-4 delegation later reads as a `ModifiableValue` multiplier on a real game number). Crucially the elite tier draws the **scarce `ColonyManpowerDB.TalentPool`** — the exact same finite handle that gates the Enhancers §6.2 unit-caliber ELITES, so a Space Marine and a brilliant admiral compete for the same talent a colony produces (build the draw once, both light up).

**The field-lab-on-a-location loop (`EXPLORATION-CONTENT-DESIGN.md`):** the exploration field-site loop is *not a new engine* — it is the `ResearcherDB` lab loop deployed to a site. A field-lab dial adds a deployment flag + a `siteId` so a scientist posting (funding 0-5, specialty bonus, cost/day — all existing `ResearcherDB` machinery) runs at a ruins/anomaly/xeno-dig site instead of at home. This is §0i in action: count resolvers, not doors — all 11 exploration site-types are DATA for the one lab loop.

### §0g stamp — three acceptance criteria
- **Reachable (cradle-to-grave, player-side): ✅ for the built half.** Infra/life-support/housing/lab/academy are **built installations** (mineral → material → production → component-in-designer → research-gated → installed on a colony → the decision (how much to invest) → the loss (bombed → efficiency/capacity collapse, the grave rung that makes a colony *takeable*)). **⏳ for terraforming + development-level** (no component, no processor — a design gap, not a deferral) and for sustenance/food (food good net-new).
- **Mirrored (opponent-side): ✅ (infrastructure-FOR-itself nuance).** These are pure-infrastructure doors the NPC uses *for* its own colonies, not *at* the player — but it must still USE them: the NPC develops colonies (builds infra/labs/academies) and trains leaders through the *same* processors and component path (delegation = NPC AI; no player-only code). The academy→talent→leader pipeline is the NPC's leader-generation too. Terraforming, once built, is likewise a self-directed NPC colony action.
- **Observable (the gauge, both sides): ✅ live for the built half; data-dead/absent otherwise.** The colony economy UI EXISTS (`ColonyManagementWindow` + tabs). **Live readouts:** infrastructure efficiency — `EntityWindow.cs:594-595` ("X / Y capacity · Z% output"), `IndustryPanel.cs:176` ("OVER CAPACITY - output at X%"), `ColonyHexMapWindow.cs:112,272` (efficiency %); morale + **talent** — `SocietyReadout.Colony` (`SocietyReadout.cs:32` morale, `:60` talent pool) piped to the DevTools log (`DevToolsWindow.cs:299`); research — `ResearchWindow.cs`. **Data-dead / no readout:** employment (always neutral), sustenance/starvation (inert), terraforming/development-level (absent). Gauge-first note: employment & sustenance are cheap Failure-A (the number exists, just unfed).

### Cross-category shared state (Prime Directive — Civic is the HUB)
- **Population → talent → Enhancers.** `PopulationProcessor` grows the pop tank; `ColonyManpowerDB.TalentPool` (`:48`) = pop × TalentFraction; `AvailableTalent` (`:54`) is drawn by BOTH the §9.2 academy elite tier AND the §6.1/§6.2 Enhancers unit-caliber elites — **shared finite handle** (a Space Marine and an elite admiral compete for it).
- **Population → workforce/crew → Industrial + Fleets.** `ColonyManpowerDB.Workforce` (read `PopulationProcessor.cs:75`) mans industry; the same pop pool crews ships (Chassis/Fleets supply gate).
- **Infrastructure → Industrial + Mining.** `InfrastructureDB.Efficiency` scales `IndustryTools` (`:115`) and `MineResourcesProcessor` (`:64`) — Civic gates the entire economy's throughput.
- **Research → EVERY door.** `ResearchProcessor` unlocks the tech that gates every category's dial ranges (`TechData`).
- **Morale → migration → stability.** `HousingAtbDB`/`EmploymentAtbDB`/`ColonySustenanceDB`/tax → `ColonyMoraleDB.ComputeMorale` → `MigrationRate` (`PopulationProcessor.cs:101`) → `LegitimacyDB`/`RebellionDB` (stability, the internal-politics kill).
- **Government modulator (#30).** `GovernmentTools` re-skins Civic: `ResearchMultiplier` (`ResearchProcessor.cs:91`), `TaxCeiling`/`MoraleWeight` (`PopulationProcessor.cs:95,101`) — inert at the Mid default.
- **Damage → grave rung.** `DamageProcessor.OnColonyDamage` destroys infra/pop → the counter to Civic (bomb the colony that produces the talent/research).
- **Atmosphere ↔ ColonyCost (the terraform seam).** `AtmosphereDB.Composition` → `SpeciesDBExtensions.ColonyCost` (`:30`) → `PopulationProcessor` capacity — the pipeline a `TerraformingProcessor` would drive.
- **Field-lab → Exploration.** A deployed `ResearcherDB` (needs `siteId`) hosts a scientist posting at an exploration site (the field-site loop = the lab loop).

### Holes owned/resolved → status + home
- **Terraforming / world-development** — ⏳ **OPEN (net-new, franchise-earning).** Home: §9.2 Development; needs a `TerraformingProcessor` writing `AtmosphereDB.Composition`. Build-list #1.
- **Development-level ladder** — ⏳ **OPEN.** `ColonyBonusesDB` empty shell (`:10-30`). Home: §9.2 Development. Build-list #4.
- **Sustenance calibration + food good** — ◐ **INERT.** `ColonySustenanceDB` demand 0; no food cargo. Home: §9.1 Habitation. Build-list #2.
- **Employment jobs** — ◐ **DATA-DEAD.** No template grants `EmploymentAtbDB.Jobs`. Home: §9.1 Habitation (data fix). Build-list #3.
- **Academy Type/Tier + Competence-Investment (E1a)** — ◐ **Wire.** Write `NavalAcademyProcessor` roll into `BonusesDB` + draw `TalentPool`. Home: §9.2 Development + §10.1 Command (rung-4).
- **Field-Lab deployment (E1b)** — ◐ **Wire.** Add `siteId` + deployment flag to `ResearcherDB`. Home: §9.2 Development + Exploration field-site loop.
- **Talent-pool draw wiring** — ◐ **shared with §6.2.** `ColonyManpowerDB.AvailableTalent` exists; the *debit* by academy elites / Enhancers elites is the one-build-both-light-up wire.

---

## §10 — Command

Command is the **"play at your own altitude"** layer — the single shape that lets you either **hand-fly** a colony or a fleet yourself, or **seat a capable officer and hand it off.** It's one door because it's *one shape at different scales*: an **admin-complex** governs a colony, a **ship-command** bridge commands a fleet, and they bind the **same** component attribute (`AdminSpaceAtb`) — just at a different rung on the span-of-control ladder (Ship → TaskUnit → … → Colony → … → Empire). This is the anti-"the game feels like a job" valve: delegate the routine, micromanage the fights that matter.

**The honest headline — the chairs are built, but nobody's running anything.** This is the least-wired category so far. The **seat substrate is genuinely Modelled** (you can install a command node and seat an officer in it), and it's the right shape. But **every consequence a seat should have is stub or net-new**: a seat gates nothing, a seated officer gives no bonus, the "funded delegate post" record is **built-but-completely-dead** (and duplicated), and the whole delegation *decision loop* is unbuilt. So Command today is a set of chairs with nobody actually empowered to run a colony or fleet.

**The yardstick — the delegation/span-of-control system** (`AdminSpaceProcessor` + the seat/assign orders + the governance design), not the combat resolver. Command is a single door; its dials are the facets of one delegation decision.

### 10.1 Command ▸ COMMAND  🔒 *locked*
*The command node — a colony HQ or a ship's bridge — and the officer you seat in it. One shape governs a colony OR commands a fleet, at whatever scope you dial. You choose the **scope** (how high up the ladder this node sits), whether to **seat an officer** and delegate, how much to **fund/trust** them, and their standing **stance** — so you can run an empire at the altitude you want, hand-flying what matters and delegating the rest.*

**The core decision — HAND-FLY or DELEGATE, and at what SCALE.** Every command node answers one question: *do you run this yourself, or does an officer?* Seat a good, well-funded officer with a sensible standing stance and the colony/fleet runs itself (freeing your attention); leave the seat empty and you're hand-flying it. The scope dial (Ship → Empire) sets *what* the node runs — a bridge steers a task unit, a sector HQ oversees whole systems.

**A. Scope / admin level (`AdminSpaceAtb.AdminLevel`) — the "one shape, two altitudes" dial**
| Option | Why pick it | The catch |
|--------|-------------|-----------|
| **Low (Ship / TaskUnit)** — a bridge | commands a ship or a small task unit — the ship-command component | can't oversee a colony or a fleet-of-fleets |
| **Colony / Planet** — an admin-complex | governs a whole colony (default HQ level) | tech-gated to go higher |
| **High (Sector / Empire)** — a sector HQ | oversees whole systems/sectors — run an empire from one chair | huge, costly, tech-gated; a fat decapitation target |

**B. Capacity / span (`ConsoleSpace` / `Office Space`) — how much it oversees**
| Option | Why | Catch |
|--------|-----|-------|
| **Small** | cheap, compact node | ◐ **span isn't actually enforced today** — one component = one seat regardless of ConsoleSpace; the only live effect is the colony hex-map radius |
| **Large** | *should* let a node run more sub-units | the `ConsoleSpace → seat-count` math is **dead** (computed, never stored) — a wire to make span-of-control real |

**C. Seat an officer — delegate the post**
| Option | Why | Catch |
|--------|-----|-------|
| **Empty (hand-fly)** | full personal control | you micromanage everything at this scope |
| **Seated (delegate)** | assign a commander → the post runs without you | ◐ **the officer currently affects nothing** — a seated commander gives no bonus (competence unread) |

**D. Funding / attention (`FundingLevel` 0–5, cost curve 1×/3×/7×/13×/22×)**
| Option | Why | Catch |
|--------|-----|-------|
| **Low funding** | cheap — a skeleton post | less output from the delegate |
| **High funding** | more output — a well-resourced governor/admiral | steeply rising cost (the attention-vs-money tradeoff) — ◐/⏳ the funding record is **built-but-DEAD** for admin (see the flag) |

**E. Stance / standing orders — the post's default behaviour**
| Option | Why | Catch |
|--------|-----|-------|
| **Standing stance** | a governor's economic priority, an admiral's ROE — the post acts on your intent without micromanagement | ⏳ **NET-NEW** — the stance/standing-orders decision loop doesn't exist |

**F. Competence — a good officer beats a green one** ⏳ *net-new* — the officer's experience/skill actually giving a bonus (today `Experience` is written by the academy but **never read**).

**Modellability audit (§0d — seats Modelled; consequences stub/net-new):**
| Dial | Verdict | How the sim models it |
|------|---------|------------------------|
| Scope / admin level | ✅ | `AdminSpaceAtb.AdminLevel` (the Ship→Empire enum ladder) → one `AdminSpaceDB.CommanderSeat` per node, stamped with its level (`admin-complex` = Colony+, `ship-command` = Ship/TaskUnit — **same atb**) |
| Seat + assign an officer | ✅ (seating) / ◐ (effect) | `AssignAdministratorOrder`/`Unassign` seat/un-seat a `CommanderDB` in `AdminSpaceAbilityState`; **but the seated officer feeds nothing** |
| Capacity / span-of-control | ◐ **wire** (dead math) | `ConsoleSpace → seat-count` is computed then discarded (one component = one seat); span is unenforced; the only live effect of Office Space is `ColonyHexMapProcessor` radius |
| Funding / attention | ◐ **wire** (dead record) | `AdministratorDB` (FundingLevel + PointsPerDay + BonusCategories) exists but has **ZERO consumers** — a detached copy of the live `ResearcherDB` (see flag) |
| Competence (officer skill) | ⏳ **net-new** | `CommanderDB.Experience` is written by academies but **never read**; no bonus fields; combat reads `FleetDoctrineDB`, not a commander |
| Stance / auto-run loop | ⏳ **net-new** | the delegation decision loop (standing stance, funding-vs-attention charging money, Governor/Minister/Admiral auto-runners) is designed-only |
| Grave rung (decapitation) | ✅ **LANDED (slice 2, 2026-07-09)** | `AdminSpaceAtb.OnComponentUninstallation` now drops the lost component's seat + frees its occupant (`AdminSpaceProcessor.DropSeatForComponent`); `CommanderFactory.DestroyCommander` vacates a killed officer's seat (`VacateSeat`). Gauge: `AdminSpaceSeatReconcileTests`. *(Redundancy/demote dial H — empties today; distributed-demote is the follow-up.)* |

> **🐞 Correction to flag (and a consolidation) — the delegate record is dead + duplicated.** The governance design doc says colony/research delegation runs on "`AdministratorDB` + `ResearchProcessor`." **That's inaccurate:** `AdministratorDB` has **zero consumers** — nothing constructs or reads it. Research actually runs on a **separate, identical twin, `ResearcherDB`** (same `FundingLevel`/`PointsPerDay`/`BonusCategories` fields), which the `ResearchProcessor` + `FundingChangedOrder` really read. So the "funded officer post with competence" record exists **twice** — one live (research), one dead (admin). The build here is to **consolidate onto ONE live delegate record** the way the governance doc *intends*, and fix that doc. This is a clean example of the Landmine-L1 "dead code that looks live" trap — the design pointed at the corpse.

**Reading:** Command is the "seats exist, everything they should DO is net-new" category — and that's the honest, useful finding. The **scaffolding is real and the right shape**: one `AdminSpaceAtb` powers both a colony HQ (`admin-complex`) and a ship bridge (`ship-command`), you can seat an officer, and academies supply them — the "one shape, two altitudes" vision is genuinely built at the *seat* level. But the delegation *consequences* are all missing: **span isn't enforced** (capacity math is dead), **a seated officer does nothing** (competence unread), the **funding record is dead + duplicated** (`AdministratorDB` vs the live `ResearcherDB`), and the **decision loop** (stance, funding-vs-attention, auto-runners) is net-new. Command is where the "play at your own altitude" fantasy has its chairs bolted down but no one yet empowered to sit in them and actually run the empire. **The connective payoff:** wiring competence here overlaps the Enhancers unit-caliber wire (a commander bonus is the fleet/colony twin of a unit's quality) — build the "a person's skill modifies an outcome" hook once, and it lights up both.

**Numbers:** `AdminLevel` (the enum ladder, tech-gated ceiling); `Office Space` 10–10,000 (colony) / `Console Space` 1–20 (ship); `FundingLevel` 0–5 with the 1×/3×/7×/13×/22× cost curve (from the live `ResearcherDB` pattern). Cradle-to-grave: a command node is a **component** (built → installed → seated → **destroyed = delegation collapses** — once the grave rung is built).

**Preset coordinates — the same door at different rungs:** ship's bridge *(command a task unit)* · fleet flagship *(admiral — fleet scope)* · colony admin complex *(governor — Colony scope)* · sector HQ *(Empire scope — run it all from one chair)*.

---

## ✅ §10 Command — COMPLETE (1/1 door locked)
Command 🔒 (single door). **The "play at your own altitude" delegation layer — the least-wired category so far, and honestly so.** The **seat substrate is Modelled and the right shape**: one `AdminSpaceAtb` binds BOTH the colony `admin-complex` (governor, Colony+ scope) and the ship `ship-command` bridge (admiral, Ship/TaskUnit scope) — "one shape, two altitudes" — and `AssignAdministratorOrder` genuinely seats a `CommanderDB` officer supplied by the academies. **But every consequence is stub or net-new:** span-of-control **isn't enforced** (the `ConsoleSpace → seat-count` math is dead; the only live effect of admin capacity is the colony hex-map radius), a seated officer **gives no bonus** (`CommanderDB.Experience` is written-never-read; combat reads `FleetDoctrineDB`, not a commander), the **funding/delegate record is built-but-DEAD and duplicated** (`AdministratorDB` has zero consumers — research really runs on the identical twin `ResearcherDB`; the governance doc points at the corpse), the **grave rung is unbuilt** (`OnComponentUninstallation` throws), and the whole **delegation decision loop** (standing stance per post, funding-vs-attention charging money, Governor/Minister/Admiral auto-runners, seat-nesting) is designed-only. Build-list: (1) **wire competence** — a seated officer's skill actually modifies the colony/fleet outcome (overlaps the Enhancers unit-caliber "person modifies outcome" wire — build once, lights up both); (2) **consolidate the delegate record** onto ONE live `FundingLevel`/`BonusCategories` post (kill the dead `AdministratorDB`, unify with `ResearcherDB`) + fix the governance doc; (3) **enforce span-of-control** (make capacity gate how many sub-units a node runs; walk the AdminLevel ladder as a real hierarchy); (4) the **delegation decision loop** (stance/standing-orders, funding-vs-attention, auto-runners — the net-new heart); (5) the **grave rung** (a decapitation strike collapses delegation).

---

## ⚙ 10 — COMMAND · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

**What this category is, in one breath.** Command is the "play at your own altitude" layer: a **command node** (a colony HQ or a ship's bridge — the *same* component atb at two scales) into which you may seat an **officer** and hand off the running of that scope. Seat a good, well-funded officer with a sensible standing **stance** and the colony/fleet runs itself; leave the seat empty and you hand-fly it. The load-bearing finding: **the seats are built and the right shape, but every consequence a seat should have is stub-or-net-new.** And the single deepest idea underneath it — **delegation IS the NPC AI** (a seated officer running a colony for a lazy *player* is the exact same machinery as "the AI" running an *NPC's* colony; there is no separate AI code path) — means wiring Command is simultaneously wiring the opponent's brain.

Engine home: `Pulsar4X/GameEngine/People/` (seats + officers) and `Pulsar4X/GameEngine/Factions/NPCDecisionProcessor.cs` (the auto-runner). Category maps the old templates `admin-complex` + `ship-command` (`COMPONENT-DESIGNER-CATEGORIES.md` §2, row 10) onto **one door** with a scale dial.

---

### Pillar tags (§0h — the PROJECTOR⛓COUNTER cross-cut)

The §0h tag stamps two pieces of metadata on every door — `pillar` (Military/Espionage/Diplomacy/Influence/—) and `skeleton-role` (Projection / Counter / Medium / Detection / network-infra). Command is unusual: it is not a projector or a counter — it is the **Medium itself**.

| Object | `pillar` | `skeleton-role` | Why |
|--------|----------|-----------------|-----|
| **§10.1 Command door** (the seat) | **cross-pillar (—)** | **Medium** | Delegation is the *medium every pillar flows through* — a Governor runs Economy, an Admiral runs Military, a Spymaster runs Espionage, a Foreign Minister runs Diplomacy. One shape, all four media. It projects nothing and counters nothing; it is the conduit the other doors' gear is *operated through*. (This is why the §0i resolver below is "the delegation loop," and why §0g's **Mirror** criterion is the *central* one here — the medium and the NPC brain are literally the same object.) |
| **★ §10.2 Command▸Relay** (the C3 node, NEW DOOR) | **Military** | **network-infra** (a Counter-side target: found/jammed/killed) | Seat-less C2 plumbing. It grants no seat and holds no officer — it is the wire the *medium travels on*, and its physical consumer is EW/detection (a `SensorProfileDB` emitter the enemy grid sees). It fills hole **H8** (network/relay infrastructure). Tagged Military because the connectivity it carries is command connectivity, and its grave rung is a battlefield sever. |

---

### Complete dial → derived-stat → engine-wire table (VERIFIED)

All file:line verified against the engine on this branch. `People/*` = `Pulsar4X/GameEngine/People/`; SensorTools real path = `Pulsar4X/GameEngine/Sensors/SensorRecever/SensorTools.cs`.

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion (the delegation loop) |
|------|------|--------------|-------|-------------------------|----------------------------------------|
| **§10.1 Command** | **A. Scope / admin level** — Ship/TaskUnit → Colony/Planet → Sector/Empire | `AdminSpaceAtb.AdminLevel` (the 11-rung `AdminLevel` enum Ship..Empire) → one `AdminSpaceAbilityState` per node stamped with its level | ✅ **Modelled** | `People/AdminSpaceAtb.cs:7-20` (enum), `:24` (`AdminLevel` prop), stamped into the seat at `People/AdminSpaceProcessor.cs:45` (`new AdminSpaceAbilityState(atb.AdminLevel, …)`) | Sets *what* the node runs — a bridge steers a task unit, a sector HQ oversees whole systems. `admin-complex` = Colony+, `ship-command` = Ship/TaskUnit — **same atb**, the "one shape, two altitudes" vision, built at the seat level. |
| **§10.1 Command** | **B. Capacity / span** (`ConsoleSpace` / Office Space) — how many sub-units a node runs | `seats += atb.ConsoleSpace` — **computed then DISCARDED** | ◐ **Wire (dead math)** | `People/AdminSpaceAtb.cs:25` (`ConsoleSpace` prop), summed at `People/AdminSpaceProcessor.cs:43` (`seats += atb.ConsoleSpace`) but the local `seats` var is **never stored on `AdminSpaceDB`** — one component = one seat regardless. Only live effect of admin capacity = the colony hex-map radius (`AdminSpaceProcessor.cs:19-21` → `ColonyHexMapProcessor.ForceUpdateColonyHexMap`). | Span-of-control is **unenforced**. The wire: store the seat-count and gate how many sub-units a node may run, walking the `AdminLevel` ladder as a real hierarchy. |
| **§10.1 Command** | **C. Seat an officer** — Empty (hand-fly) vs Seated (delegate) | A `CommanderDB` bound into `AdminSpaceAbilityState.CommanderID`/`.Commander` | ✅ **(seating)** / ◐ **(effect)** | Order path: `People/Orders/AssignAdministratorOrder.cs:44-80` seats a commander (`post.CommanderID = _administratorId; post.Commander = commanderDB`), `UnassignAdministratorOrder.cs` un-seats. Seat struct: `People/AdminSpaceAbilityState.cs:9-18` (`CommanderID` default -1, `TryGetCommander`). Client UI: `Pulsar4X.Client/Interface/Windows/AdminWindow.cs`. | Seating **works**; the seated officer **feeds nothing** — no bonus is read from the seat (see D/F). Also the durable-seat bug (below) wipes the assignment on the next processor pass. |
| **§10.1 Command** | **D. Funding / attention** — `FundingLevel` 0–5, cost curve 1×/3×/7×/13×/22× | `AdministratorDB.FundingLevel` × `PointsPerDay`, `× CostPerDay` | ◐ **Wire (dead record)** | `People/AdministratorDB.cs:41` (`FundingLevel` 0–5), `:16` (`PointsPerDay`), `:23` (`BonusCategories`), `:29` (`CostPerDay`). **`AdministratorDB` has ZERO consumers — nothing constructs or reads it.** It is a detached copy of the **live twin `ResearcherDB`** (`Tech/ResearcherDB.cs`), which `ResearchProcessor` really reads: `FundingLevel` at `Tech/ResearchProcessor.cs:201,255`, `BonusCategories` at `:273`. | The attention-vs-money tradeoff. **Consolidate onto ONE live record** (kill dead `AdministratorDB`, generalize `ResearcherDB` → the universal delegate record) — the governance doc points at the corpse (Landmine-L1). |
| **§10.1 Command** | **E. Stance / standing orders** — the post's default behaviour (a governor's economic priority, an admiral's ROE) | a named preset bundle biasing what the auto-runner emits | ⏳ **NET-NEW** | No engine field today. Pattern to reuse: `GroundFormationDoctrine` stances are **data-driven JSON** (`groundStances.json` → `GroundStanceBlueprint`) — the moddable-catalog shape to copy. The NPC's stance *choice* is biased by `FactionInfoDB.Doctrine` (`DoctrineVector`, four floats). | **The stance IS the decision the leader owns**; competence (F) is only *how well* it's executed. This is the heart of the delegation loop — feeds the auto-runner (below). |
| **§10.1 Command** | **F. Competence** — a good officer beats a green one (rung 4 of the pipeline) | the officer's `BonusesDB` values fold into a real target number via `ModifiableValue` | ⏳ **NET-NEW** | `CommanderDB.Experience` written by academies (`CommanderDB.cs:21`) but **never read**. The container exists & is reusable: `BonusesDB.Bonuses` (`Bonus(Value, Type, FilterId)`) — but `CommanderFactory.Create` attaches an **empty** `BonusesDB` (`People/CommanderFactory.cs:24`). The rung-4 pattern works in exactly ONE place to copy: research folds an officer's `BonusesDB` into a `ModifiableValue` (`Tech/ResearchProcessor.cs:87` + the Refresh path). | **Wire competence** = build the "a person's skill modifies an outcome" hook once → lights up BOTH commander competence AND §6.2 Enhancers unit-caliber elites (build-list #5). Combat currently reads `FleetDoctrineDB`, not a commander. |
| **§10.1 Command** | **G. Command Reach** (essence-ext E1a) — Local / In-system / Interstellar-networked | `CommandReach_m` (computed like detection range) | ◐ **Wire** | Compute mirrors `SensorTools.DetectionRange_m` (`Sensors/SensorRecever/SensorTools.cs:425`) and `RangeForSignal` (`:409`); it is an **emission the enemy sensor grid sees**, scaled by EMCON/activity exactly as detection is (`SensorTools.cs:308,328`). | The delegate order path checks Reach before an order can reach a unit; interstellar scope is granted by a networked relay (door §10.2). Where your orders travel = where the enemy can cut them. |
| **§10.1 Command** | **H. Redundancy / Hardening** (essence-ext E1a) — single (decapitates) vs hardened (HTK) vs distributed (demotes) | on the decapitation handler: a lost node **empties** vs **demotes** the scope | ◐ **Wire** | Rides `AdminSpaceAtb.OnComponentUninstallation` — **currently throws `NotImplementedException`** (`People/AdminSpaceAtb.cs:42-45`). Implementing it *is* the grave rung. | A single cheap node = a fat decapitation target (empties the scope); a distributed net demotes to the next rung instead of collapsing. |
| **§10.1 Command** | **Contracts** (Main-Merge Delta, NEW dial) — fixed-term assignment (5-yr default + early-break cost) | a term + break-cost on the seat | ⏳ **NET-NEW (designed-only)** | No engine field; designed in `AI-SELF-PLAY-DESIGN.md`. Composes with the existing doctrine-switch **cooldown** (a leader on contract runs their stance for the term — no per-tick flip-flop, good for the AI-cost bill). | The commitment texture on a delegation: you can't cheaply re-shuffle a cabinet mid-crisis. |
| **★ §10.2 Command▸Relay** (NEW DOOR, essence-ext E1a / fills H8) | **RelayRange_m** + a network-connectivity boolean between nodes | the connectivity of your whole command net → feeds Command Reach's interstellar scope | ◐ **Wire / ⏳ Defer (addressing)** | A relay is a `SensorProfileDB` **emitter** → **found** (the live sensor loop), **jammed** (EW §3.4 — shrink its effective reach), **destroyed** (damage) → the sever. Range compute reuses `SensorTools.DetectionRange_m` (`:425`). The many-to-one *addressing* layer (which node connects to which) is the deferred half. | Earns a **door, not a dial**: it grants NO seat and holds NO officer — seat-less C2 plumbing, a different physical object with a different consumer (EW/detection). Its destruction **severs delegation** on the links it carried. |

---

### The delegation loop (§0g Mirror is CENTRAL here — delegation IS the NPC AI)

**§0i says: count resolvers, not doors.** Command has exactly ONE resolver — **the delegation loop** — and all 19 leader roles (Governor / Admiral / Foreign Minister / Spymaster / Chief Scientist / …) are *configuration* on that one pipeline, not 19 separate systems (`AI-SELF-PLAY-DESIGN.md`: "don't build 19 leaders — build one pipeline, prove it on one role, then every other role is a `BonusCategory` + a `Refresh` method"). The loop is the count-resolver §0i names.

**The chain the loop runs (seat → stance → competence → order):**

1. **Seat** — an officer (`CommanderDB`) is bound into a node's `AdminSpaceAbilityState` via `AssignAdministratorOrder.Execute` (`People/Orders/AssignAdministratorOrder.cs:44-80`, sets `post.CommanderID`/`post.Commander`).
2. **Stance** — the seat carries a standing-order preset (dial E, net-new; reuse the `GroundStanceBlueprint` JSON-catalog pattern). The stance decides *what* the delegate does.
3. **Competence** (rung 4) — the officer's `BonusesDB` folds into the target number via `ModifiableValue` + a `Refresh` (the exact pattern live only in `ResearchProcessor.cs:87`). Competence decides *how well*.
4. **Order emission** — the delegate issues the **same** `IndustryOrder2` / fleet / tax orders the player uses — there is no special AI code path. This is what fills the dead auto-runner.

**The dead auto-runner the loop fills: `NPCDecisionProcessor`.** It is a live-but-hollow `IHotloopProcessor` on `FactionInfoDB`, monthly (`FirstRunOffset` 5d / `RunFrequency` 30d), gated on `IsNPC` (`Factions/NPCDecisionProcessor.cs:17-46`). **The GlobalManager wiring gap is FIXED** (keystone, 2026-06-30 — `MasterTimePulse.SimulateTimeUntil` now iterates `GlobalManager.ManagerSubpulses`, so it genuinely fires; liveness gauge `NPCDecisionProcessor.TickCount:29`). But its **decision body is a stub**: `Tick` computes the dominant `DoctrineVector` axis then hits a `// TODO: translate dominant axis into actual orders` (`NPCDecisionProcessor.cs:71-84`). Only `RunDiplomaticDrift` (`:97-124`) does live work. **Filling this stub = the delegation loop's active arm:** `DoctrineVector` biases which stance an NPC picks → the seated delegate executes that stance → emits orders through rung 4. The player's off-switch-for-micro and the NPC's brain are the identical machinery — which is why §0g's **Mirror** criterion is not a checkbox here but the *architecture*.

---

### The decapitation substrate — the LeaderLost/CrewLosses event (unconsumed), OnComponentUninstallation (throws), the durable-seat bug

Three engine facts make "lose a command node → delegation collapses" **almost** buildable — the hooks exist, they're just unfinished:

- **The event is already published, unconsumed.** `CommanderFactory.DestroyCommander` (`People/CommanderFactory.cs:105-126`) removes the officer from the faction roster (`:112`) and **publishes `EventType.CrewLosses`** ("…has been killed", `:115-123`) then `Destroy()`s the entity (`:125`). **Nothing subscribes.** The grave rung needs one `LeaderLost` subscriber that reads this event and empties/demotes the seat it was in. (A ship's captain is the only officer that dies today — `ShipFactory.DestroyShip` — and it leaks dangling seat refs.)
- **The uninstall hook throws.** `AdminSpaceAtb.OnComponentUninstallation` is `throw new NotImplementedException()` (`People/AdminSpaceAtb.cs:42-45`). Implementing it is dial **H** (Redundancy/Hardening): a lost node either **empties** its scope (single/cheap = decapitated) or **demotes** to the next rung (distributed).
- **The durable-seat bug — the prerequisite that blocks everything.** `AdminSpaceProcessor.CalcEntityAdminSpace` **rebuilds `CommanderSeats` from scratch every pass**: it allocates `new List<AdminSpaceAbilityState>()` and assigns it to `adminSpaceDB.CommanderSeats` (`People/AdminSpaceProcessor.cs:36-37`), then repopulates from the component list (`:38-47`) with **fresh, un-seated** states — so an officer assigned by `AssignAdministratorOrder` is **wiped on the next processor run** (the new `AdminSpaceAbilityState` has `CommanderID = -1`). Its own comment flags the doubt: *"Currently this resets the list, need to check if we want that"* (`:27`). **Until seat occupancy is durable, nothing downstream — competence, stance, decapitation — can hold.**
- **The C3 Relay sever (§10.2).** A destroyed relay severs the command net on the links it carried → any seat whose Command Reach depended on that relay loses interstellar scope (demotes to in-system). The relay is a `SensorProfileDB` emitter, so it is **found → jammed (§3.4 EW) → destroyed** by the ordinary sensor/EW/damage loop — command connectivity you build is connectivity the enemy can cut.

---

### Prerequisite fixes & dead stubs (file:line)

The essence names these in build order (`AI-SELF-PLAY-DESIGN.md`; DIALS §E1a `:2503`, E3 `:2546`):

1. **Durable seats** — ✅ **LANDED 2026-07-09 (foundation slice 1).** `CalcEntityAdminSpace` now reconciles via the pure, unit-tested `AdminSpaceProcessor.ReconcileSeats(previous, current)`: existing seats (and their seated commander) are carried across a recalc, matched by `ComponentName`; a removed component's seat drops and its occupant's `AssignedTo` is cleared. Gauge: `AdminSpaceSeatReconcileTests`. *The hard prerequisite is now cleared — competence/stance/decapitation can build on held seats.*
2. **Implement `OnComponentUninstallation`** — ✅ **LANDED (slice 2, 2026-07-09):** replaced the throw with a targeted seat-drop + occupant-free (`AdminSpaceProcessor.DropSeatForComponent`); officer-death vacates its seat (`CommanderFactory.DestroyCommander` → `VacateSeat`). Currently *empties* the scope; the distributed-*demote* variant (dial H) is the follow-up.
3. **Add one `LeaderLost` subscriber** — consume the already-published `EventType.CrewLosses` (`People/CommanderFactory.cs:115-123`) to empty the seat the dead officer held.
4. **Consolidate the dead delegate record onto the live pattern** — `AdministratorDB` (`People/AdministratorDB.cs`, zero consumers) is a detached duplicate of the **live** `ResearcherDB` (`Tech/ResearcherDB.cs`, read by `ResearchProcessor.cs:201,255,273`). Kill `AdministratorDB`, generalize `ResearcherDB` into the universal funded-officer record (`FundingLevel`/`PointsPerDay`/`BonusCategories`), and **fix the governance doc that points at the corpse** (classic Landmine-L1 "dead code that looks live").
5. **Fill the auto-runner** — `NPCDecisionProcessor.Tick`'s `// TODO` (`Factions/NPCDecisionProcessor.cs:79-84`): DoctrineVector → stance → delegate order emission.
6. **Wire rung-4 competence** — copy the `ResearchProcessor.cs:87` `BonusesDB`→`ModifiableValue`→`Refresh` pattern to the seat, and populate the empty `BonusesDB` at graduation (`CommanderFactory.cs:24` currently attaches it empty; `CommanderDB.Experience:21` is written-never-read).

---

### Design essence captured inline

**Delegation = NPC AI.** The single deepest idea: a Governor running a colony to a stance for a lazy *player* is the exact same machinery as "the AI" running an *NPC's* colony — the delegate issues the same `IndustryOrder2`/tax/fleet orders the player would, so there is **no special AI code path** (`AI-SELF-PLAY-DESIGN.md`; governance doc's locked rule). Build the delegation loop once and you have built both the player's off-switch-for-micro and the opponent's brain. **The six-rung leader pipeline** (born → skilled → seated → **acts** → grows → lost) is one pipeline for all 19 roles: rungs 2–3 (the `BonusesDB` container + seat) are built and reusable, rung 4 exists exactly once as a copyable pattern (research), and rungs 1/5/6 (a competence *generator* at the academy, career growth, and the decapitation grave rung) are the real remaining work. **Rung 4 — "competence modifies outcome" — is the keystone wire:** the officer's `BonusesDB` folds into a real game number through a `ModifiableValue` + event-driven `Refresh` (not per-tick — cheap, which matters for the AI-cost bill), where the local officer's competence is the *modifier* and the empire's `GovernmentDB` regime is the *modulator* stacked on top. Crucially, the **stance is the stacking decision; competence is only the texture on how well that decision executes** — which keeps every seat honest against the realism-vs-gameplay firewall (a seat earns its keep by owning a *decision*, not by granting a +2).

---

### §0g stamp — the three acceptance criteria

- **Reachable** (cradle-to-grave, player-side) — ◐ **partial.** A command node is a **component**: designed → built from materials → installed (`AdminSpaceAtb.OnComponentInstallation`, `People/AdminSpaceAtb.cs:32-40`) → seated (`AssignAdministratorOrder`) → the decision (stance, net-new) → **destroyed** (`OnComponentUninstallation`, currently throws). The officer rides the *people* chain (academy → graduate), not the mineral chain. Missing rungs: stance decision, competence, and the grave rung.
- **Mirrored** (opponent-side) — ✅ **THE key door, by construction.** A seat *is* the NPC's brain — the same `AdminSpaceAbilityState` + delegation loop the player uses is the NPC's decision engine; the NPC "runs its gear at you" through the identical `NPCDecisionProcessor` → delegate → order path (`Factions/NPCDecisionProcessor.cs`). No player-only code, so no solitaire/inert-AI trap. This is why Command's honest headline is "seats built, everything they DO is net-new" and *not* "player-only feature."
- **Observable** (the gauge, both sides) — ◐ **wire (Failure-A: the numbers exist, unwired).** The command/delegation readout: which seats are filled and by whom (`AdminSpaceDB.CommanderSeats` + `AdminSpaceAbilityState.CommanderID`), Command Reach (computable via `SensorTools.RangeForSignal`), and the auto-runner liveness gauge already ticks (`NPCDecisionProcessor.TickCount`). The delta: a UI panel (`AdminWindow.cs` exists as the seat) surfacing *stance + competence + reach* so the player can see a delegate working — and its enemy-facing half (the relay is an emission the target's grid reads).

---

### Cross-category shared state (Prime Directive)

| Shares state with | The shared wire (file:line) | Consequence |
|-------------------|------------------------------|-------------|
| **§6 Enhancers (Unit Caliber)** | The **"a person's skill modifies an outcome"** hook — one `BonusesDB`→`ModifiableValue`→`Refresh` pattern (`Tech/ResearchProcessor.cs:87`) | Build-list #5: build the competence wire ONCE and it lights up **both** commander competence (§10 rung 4) *and* §6.2 elite unit caliber. A commander bonus is the fleet/colony twin of a unit's quality. |
| **§9 Civic (Development / Academy)** | `NavalAcademyProcessor` graduates officers on a bell curve (`NextBellCurve`) but writes only `Experience` (int) — leaves `BonusesDB` empty (`CommanderFactory.cs:24`); draws the never-used scarce `ColonyManpowerDB.TalentPool` | Academy Type/Tier + Competence-Investment dials (essence-ext E1a) live on §9.2 but **feed Command's rung 1** (the competence generator). The academy is the first consumer of both the talent pool and the empty `BonusesDB`. |
| **§3 Sensors / §3.4 EW** | Command Reach is computed like detection range (`SensorTools.cs:425`) and the **C3 Relay is a `SensorProfileDB` emitter** — found/jammed by the same sensor+EW loop (`SensorTools.cs:308,328`) | Where your orders travel = where the enemy can cut them. The relay's grave rung is delivered by the ordinary detection→jam→damage chain, not a bespoke path. |
| **§11 Chassis** | The seat is a component **mounted on a chassis** — `admin-complex` on a Structure/colony, `ship-command` on a Hull bridge — via `ComponentInstancesDB.TryGetComponentsByAttribute<AdminSpaceAtb>` (`AdminSpaceProcessor.cs:34`) | The chassis is what *grants* the seat; a wrecked chassis (decapitation strike) takes the seat and its delegation with it. Ties Command's grave rung to §11 + the damage system. |
| **Factions / Diplomacy / Government** | The empire-cabinet seats (Foreign Minister, Spymaster, Grand Admiral, Chief Scientist) are the **same seat shape at cabinet scope**; `GovernmentDB` re-skins stances, species sets leader lifespan | Command's one shape scales up to the whole 19-role, two-chain roster; the regime is the empire-wide *modulator* stacked on the local officer's competence *modifier*. |

---

### Holes owned/resolved

| Hole | Description | Resolution | Status + home |
|------|-------------|------------|---------------|
| **H8** | Network / relay infrastructure — addressable, many-to-one; destruction severs a link (`COMPONENT-DESIGNER-CATEGORIES.md` §5) | The **★ C3 / RELAY door (§10.2 Command▸Relay)** — a seat-less network node, a `SensorProfileDB` emitter, found/jammed/killed → the sever. (Shares this hole with exploration's Route Works / jump-gate, which is the same physical shape.) | **RESOLVED (design-locked, unbuilt).** Home: §10.2 Command▸Relay. Grade ◐ Wire (range) / ⏳ Defer (the many-to-one addressing layer). |
| **H10** | Autonomous / crewless / hive units — zero crew, or many sharing one mind (Borg collective, drones) | The **shared-span** case: a hive is a **Command/formation variant** where one span covers many bodies. Enhancers ▸ Systems automation drops crew→zero (AI); the *shared mind* is a Command span dial (one seat, many units) — i.e. the span-of-control wire (dial B) generalized past the dead `ConsoleSpace` math. | **PARTIALLY OWNED (LOW-MEDIUM priority).** Home split: automation → §6 Enhancers ▸ Systems; shared-span → §10.1 Command (the same `ConsoleSpace`→seat-count wire that's currently dead, `AdminSpaceProcessor.cs:43`). Not yet designed to lock. |

---

## §11 — Chassis

Chassis is the **final category and the capstone** — the container everything else has been mounting *into* for all 36 doors before it. Every category footer so far ended the same way: *"the mass funnels the chassis," "overflows everything below Mega," "the numbers force the build."* Chassis is the frame that's supposed to **catch** all that tonnage and force the decision. Five doors, one per scale: **Personnel** (a soldier) · **Vehicle** (a tank/walker) · **Hull** (a ship) · **Structure** (a station) · **Mega** (a Death-Star/Titan/Dyson). Each provides a **mass/volume budget** + the **mount class** it accepts + structural HP; the tier a build lands in *falls out of its tonnage* (§0b), never a rulebook.

### 🔑 The capstone finding — §0b is enforced in ONE of the five doors
This is the single most important line in the whole spec, so it goes first and plain: **the "mass overflows a small chassis → forces a bigger one" mechanic is real ONLY for ground units today. Ships and stations accumulate mass completely UNCAPPED.**
- **Ground (Personnel/Vehicle):** `GroundUnitAssembly.Compute` is a **real hard cap** — dial parts past the frame's `GroundChassisAtb.BaseStrength` budget and the design is **Invalid** (`GroundUnitAssembly.cs:134`). That IS §0b, live.
- **Ships (Hull):** `ShipDesign.Recalculate` sums `MassPerUnit` with **no ceiling, no hull object, no tonnage class** — a ship is just a bag of components. Bigger costs more (`IndustryPointCosts = MassPerUnit × 0.1`) but is **never rejected**. The only mass gate is the `LaunchComplex` launchpad tonnage — a *launch-site* gate, not a hull budget.
- **Stations (Structure):** a station is a **host, not a design** — modules grow onto `ComponentInstancesDB` by accretion with **no budget**; `StructuralIntegrity` is a flat 500 ("a bigger station has more to lose, not more to survive").
- **Mega:** **doesn't exist at any scale.**

So the founding promise of the entire component designer — *the numbers force the build* — is **genuinely live for one door and net-new for the rest.** Naming that, and exactly what to build to fix it, is the right way to close the blueprint.

### §11.0 Shared chassis dials (all five doors)
| Dial | Drives (real stat) |
|------|--------------------|
| **Mass / volume budget** | the ceiling everything mounted must fit under — **the §0b container** (hard cap: ground only) |
| **Mount class** | which components it accepts — `ComponentMountType` flag (ShipComponent / GroundUnit / PlanetInstallation / …) |
| **Structural HP** | how much the frame itself soaks before it's wrecked (the base toughness under the armour) |
| **Hardpoints / slots** | how many mounts (ground: a per-item weight cap; ship/station: net-new) |
| **Grave rung** | a wrecked frame spills everything mounted on it — the ultimate loss |

### 11.1 Chassis ▸ PERSONNEL  🔒 *locked*
*The soldier's own frame — a human (or xeno) body's carry budget. The lightest chassis: a rifle, a pack, an augment, and not much else. Everything from a conscript to a power-armoured marine sits on this frame (the armour + caliber ride on top, from the Defense + Enhancers doors).*
**Core decision — how much can one body haul.** A small `BaseStrength` budget; overload it and the design is invalid. This is the frame the Enhancers strength-augment *raises* (the §0b "carry more" upgrade).
**Dials:** `BaseStrength` (carry budget) · `BaseHP` · `Locomotion` (Foot) · `CarryClass` (**Personnel**).
**Modellability:** ✅ **Modelled** — `GroundChassisAtb` + the `GroundUnitAssembly` hard carry cap; augments raise the budget (the live §0b upgrade).
**Presets:** conscript body · trooper · power-armour frame *(+ Bio-augmentation)*.

### 11.2 Chassis ▸ VEHICLE  🔒 *locked*
*The machine frame — wheels, tracks, legs, hover. A far bigger carry budget than a body, so it mounts the heavy weapons, reactors, and armour a trooper can't. The AT-M6, a tank, a hovertank all sit here (legs + heavy laser + reactor + armour → the mass forces this frame, per the AT-M6 walkthrough).*
**Core decision — a vehicle-scale budget on a chosen locomotion.** Bigger `BaseStrength`; the mass of a heavy cannon + its reactor + thick armour fits here where it'd overflow a Personnel frame.
**Dials:** `BaseStrength` (big budget) · `Size` · `Locomotion` (**Tracked/Walker/Hover**) · `CarryClass` (**Vehicle**).
**Modellability:** ✅ **Modelled** (same hard cap as Personnel). *Net-new:* **walker + swarm as distinct carry classes** — today "walker" is a `GroundLocomotion` value, not a class, and there's no swarm class (a many-tiny-bodies frame).
**Presets:** wheeled APC · main battle tank · **AT-M6 walker** *(heavy Vehicle budget)* · hovertank.

### 11.3 Chassis ▸ HULL  🔒 *locked*
*The ship's frame — a spaceframe's tonnage class, hardpoints, and hull toughness. **The door where §0b is NOT yet real for space:** a ship is IMPLICIT today (a bag of components with no hull object and no tonnage cap), so a corvette and a dreadnought differ only in the emergent sum of what you bolted on — nothing forces the frame.*
**Core decision — what tonnage class of ship, and how many hardpoints.** A frigate hull vs a battleship hull vs a Star-Destroyer hull: each *should* cap the mass/volume it holds and the number of weapon mounts, so an over-gunned design overflows the hull and forces a bigger class (the Defiant-is-over-gunned lesson).
**Dials:** **tonnage/volume ceiling** · **hardpoint count** · **hull structural HP** · size class.
**Modellability:** ⏳ **NET-NEW** — "ship hulls become real designs." `ShipInfoDB.Tonnage` is commented out; `ShipDesign.Recalculate` accumulates uncapped. The build: a **hull component/attribute carrying the mass/volume ceiling + hardpoint budget + a design-time validity check** mirroring `GroundUnitAssembly.Compute` (the ground cap that already works).
**Presets:** fighter hull · frigate hull · cruiser hull · capital/dreadnought hull *(all net-new as capped frames)*.

### 11.4 Chassis ▸ STRUCTURE  🔒 *locked*
*The station's frame — an off-world platform that hosts modules, population, industry, and defences. A **real host but not yet a real design:** stations exist and work (they mine, build, house pop — the parallel to a colony we saw throughout), but they're assembled by accretion (modules grown onto `ComponentInstancesDB`) with no design class and no module budget.*
**Core decision — a station's module budget + structural class.** How many modules a platform holds before it needs a bigger frame; and its structural integrity.
**Dials:** **module budget** · **structural integrity** (today flat 500, not scaled) · mount class.
**Modellability:** ◐ **WIRE** — the host (`StationInfoDB` + `StationFactory`) is real and the industry rails build modules onto it; the wire is a **station design class + a module budget** (closing the `DESIGNER-AUDIT` "no station design class" gap) + optionally scaling `StructuralIntegrity`.
**Presets:** orbital platform · research station · **shipyard station** · defence station · O'Neill habitat *(the Civic space-habitat on a Structure frame)*.

### 11.5 Chassis ▸ MEGA  🔒 *locked*
*The super-scale frame — the Death Star, a Titan, a Dyson swarm, a world-ship. **The tier every category footer promised — and it doesn't exist.** This is §0b's ultimate payoff: a planet-cracker beam's output demands a generator whose mass demands a hull whose tonnage **overflows everything below Mega**, so the numbers put you here — except there's no Mega frame to land in yet.*
**Core decision — the impossible-scale build.** The one frame with the budget for a weapon/reactor/structure so massive nothing smaller can hold it. It's not chosen from a menu; the tonnage *forces* it (the whole point of §0b).
**Dials:** an enormous **mass/volume budget** (orders of magnitude past Structure) · a Mega mount class · structural HP at the 10⁹–10¹⁵ J scale (§0e's top tier).
**Modellability:** ⏳ **ENTIRELY NET-NEW** — no Mega tier exists at any scale (ship, station, or ground), and no `Mega` mount flag. It's the top rung of the chassis ladder and the last thing standing between a *dialed* Death Star and a *buildable* one.
**Presets:** Death Star *(planet-cracker beam + its Mega generator)* · Titan walker · Dyson swarm-node · world-ship.

---

## §11 Chassis — status (all five doors 🔒 LOCKED 2026-07-09)
Personnel 🔒 · Vehicle 🔒 · Hull 🔒 · Structure 🔒 · Mega 🔒. **The capstone — where §0b lives, and it's only one-fifth built.** The mass/volume budget that the ENTIRE 37-door designer leans on is a **real hard cap for exactly ONE door pair (Personnel/Vehicle)** — `GroundUnitAssembly.Compute` genuinely rejects an over-budget design (`GroundChassisAtb.BaseStrength`), and augments raise the budget — so §0b is *live* on the ground. But **Hull is net-new** (ships are implicit bags of components; `ShipDesign.Recalculate` accumulates `MassPerUnit` uncapped; `Tonnage` is commented out), **Structure is a wire** (a real station host with no design class or module budget), and **Mega doesn't exist at all**. Plus the mount system (`ComponentMountType` flags) is **Modelled as data but enforced only by UI filtering** — no engine-level "this component fits this chassis" gate. **The single highest-leverage build for the whole component designer:** **port the ground carry gate to ships + stations** — a chassis-supplied mass/volume ceiling + a design-time validity check (mirroring `GroundUnitAssembly.Compute`), a **Hull design class**, a **Station design class + module budget**, the **Mega tier**, and an **engine-level mount-compatibility check**. Do that, and "the numbers force the build" — the founding promise of the entire designer — becomes real across *all* scales instead of just the ground. This is the keystone that makes the §0b-forcing in the other 36 doors true. Build-list: (1) **the ship/station mass-budget cap + validity check** (the keystone — makes §0b real for space); (2) **Hull as a real design** (tonnage class + hardpoints); (3) **Station design class + module budget** (closes the DESIGNER-AUDIT gap); (4) the **Mega tier** (+ mount flag) — the Death-Star/Titan frame; (5) **engine-level mount enforcement** (today UI-filter only); (6) **walker/swarm** as distinct ground carry classes.

---

## ⚙ 11 — CHASSIS · WIRING DOSSIER (self-contained)
*Everything needed to wire this category — no other doc required.*

Chassis is the **final category and the capstone**: the container every one of the other 36 doors mounts *into*. Five doors, one per scale — **Personnel** (a soldier) · **Vehicle** (a tank/walker) · **Hull** (a ship) · **Structure** (a station) · **Mega** (a Death-Star/Titan/Dyson). Each is supposed to provide a **mass/volume budget** + the **mount class** it accepts + structural HP, and the tier a build lands in is meant to *fall out of its tonnage* (§0b), never a rulebook. **The capstone finding: §0b is enforced in exactly ONE of the five doors.**

The single most important fact in this whole blueprint lives here: the mass/volume budget that the ENTIRE 37-door designer leans on ("the mass funnels the chassis," "overflows everything below Mega," "the numbers force the build") is a **real hard cap for only the Personnel/Vehicle pair** — the ground assembler. Hull accumulates mass uncapped, Structure has no design at all, Mega doesn't exist, and mount flags are enforced only by UI filtering. Porting the ground cap to ships + stations is the highest-leverage build in the designer.

### Pillar tags (§0h)
- **`pillar` = universal / infrastructure.** Chassis is not a projector and not a counter — it is the **Medium/container** every pillar's gear sits inside. A hull holds the weapons (Military projectors) AND the sensors (Detection) AND the relay (Espionage/Command). It has no Reach·Cadence·Selectivity of its own; it *supplies the budget* that lets a mounted projector function.
- **`skeleton-role` = Medium (container).** In the §0h Projection/Counter/Medium/Detection scheme, Chassis is the pure **Medium** slot — the physical thing every other role bolts onto. Its "counter" is not a defense dial but the §0b budget wall itself (you cannot mount what the frame cannot hold) and, at the grave rung, the destruction of the chassis (which takes every mounted ability with it).
- **§0i resolver:** Chassis's "resolver" is **the §0b physical-budget loop — the ONE loop all 37 doors share** (§0i names it first: "the §0b physical-budget loop (all 37 doors)"). Chassis is not a new loop; it is the *frame the loop measures against*. Every other door's mass number is only meaningful because a Chassis provides the ceiling it's checked against. This is why fixing the cap here makes 36 other doors' forcing become real at once.

### Complete dial → derived-stat → engine-wire table (VERIFIED, all 5 doors)

| Door | Dial | Derived stat | Grade | Engine wire (file:line) | System insertion (the §0b budget loop) |
|------|------|--------------|-------|-------------------------|-----------------------------------------|
| **11.1 Personnel** | `BaseStrength` (carry budget) · `BaseHP` · `Size` · `Locomotion`=Foot · `CarryClass`=**Personnel** | carry-capacity currency; heaviest single item (via `MaxItemFraction` 0.5) | ✅ **Modelled** | `GroundChassisAtb.cs:39` (`BaseStrength`), `:41` (`BaseHP`), `:43` (`Size`), `:44` (`Locomotion`), `:46` (`CarryClass`) | **THE live §0b cap.** `GroundUnitAssembly.Compute` reads `chassis.BaseStrength` as `capacity` (`GroundUnitAssembly.cs:66`), sums mounted part carry-mass into `used` (`:123`), and **rejects over-budget → design Invalid** (`:134` `if (used > capacity)`). Per-item wall at `:125`. Augments RAISE the budget (`:72–74`, `GroundAugmentAtb.StrengthBonus`). |
| **11.2 Vehicle** | `BaseStrength` (big budget) · `Size` · `Locomotion`=**Tracked/Walker/Hover** · `CarryClass`=**Vehicle** | vehicle-scale carry budget; heavy weapon+reactor+armour fit here | ✅ **Modelled** | Same `GroundChassisAtb.cs` fields; `GroundLocomotion` enum `:10–16` (Foot/Tracked/Walker/Hover); `GroundCarryClass` Personnel/Vehicle used at `GroundChassisAtb.cs:46` | Same `GroundUnitAssembly.Compute` cap as Personnel — a larger `BaseStrength` is the only difference; the AT-M6's heavy cannon + reactor + armour **fit** the Vehicle budget where they'd overflow Personnel. `DeriveType` maps a Vehicle `CarryClass` → `GroundUnitType.Armor` (`GroundUnitAssembly.cs:214`). |
| **11.3 Hull (ship)** | `MassPerUnit` (emergent, uncapped) · tonnage class (**absent**) · hardpoint budget (**absent**) | ship mass = Σ components + armour; cost only | ⏳ **NET-NEW** | `ShipDesign.Recalculate` sums `MassPerUnit += component.MassPerUnit × count` **with no ceiling** (`ShipDesign.cs:151`); cost = `MassPerUnit × 0.1`, never rejected (`:171`). `ShipInfoDB.Tonnage` **commented out** (`ShipInfoDB.cs:50`, `:52`, `:80`). | **NO budget loop.** A ship is a bag of components; a corvette and a dreadnought differ only in the emergent sum. The ONLY mass gate is the *launch-site* pad tonnage — `LaunchComplexProcessor.cs:58` (`if (shipDesign.MassPerUnit > pad.MaxTonnage)`), a launch gate, not a hull budget. |
| **11.4 Structure (station)** | module budget (**absent**) · `StructuralIntegrity` (flat 500) · mount class | modules by accretion; no budget | ◐ **WIRE** | `StationFactory.CreateStation` grows modules onto `ComponentInstancesDB` (`StationFactory.cs:60`) with **no design class**; `StructuralIntegrity` is a flat `BaseStructuralIntegrity = 500` pool on `StationInfoDB` (not scaled by module count — "more to lose, not more to survive"). | **NO budget loop.** A station is a **host, not a design** — there is no `StationDesign` class, no `Recalculate`, no module ceiling. Modules accrete freely. The wire is a station design class + a module budget. |
| **11.5 Mega** | enormous mass/volume budget · a **Mega mount flag** (absent) · HP at 10⁹–10¹⁵ J | — | ⏳ **ENTIRELY NET-NEW** | **Nothing exists.** No Mega tier at any scale (ship/station/ground); `ComponentMountType` (`Enums.cs:126–144`) has no `Mega` flag — the flags stop at `GroundUnit = 1 << 6` (`:143`). | **The tier every category footer promised, absent.** §0b's ultimate payoff (a planet-cracker's generator mass overflows everything below Mega) has no frame to land in. The last thing between a *dialed* Death Star and a *buildable* one. |

### THE §0b MASS-BUDGET KEYSTONE — the single highest-leverage build for the WHOLE designer

**What's LIVE (the one working door pair):** `GroundUnitAssembly.Compute` (`GroundUnitAssembly.cs:54`) is a **real hard cap**. It reads the frame's `GroundChassisAtb.BaseStrength` as the carry budget (`:66`), accumulates every mounted part's carry-mass into `used` (`:123`), and at `:134` (`if (used > capacity)`) marks the whole design **Invalid** with a reason. There's a per-item wall too (`:125`, heaviest single part ≤ `MaxItemFraction` 0.5 of capacity). Augments RAISE the budget (`:72–74` — a power-armour `GroundAugmentAtb.StrengthBonus` adds to `capacity`, the live §0b "carry more" upgrade). **That IS §0b, running, for Personnel + Vehicle only.**

**What's NET-NEW / a WIRE (the other four-fifths):**
- **Hull uncapped:** `ShipDesign.Recalculate` (`ShipDesign.cs:137`) accumulates `MassPerUnit` (`:151`) with no ceiling and never rejects a design; `ShipInfoDB.Tonnage` is commented out (`ShipInfoDB.cs:50`). Ships are implicit — no hull object, no tonnage class. Only a launch-pad gate exists (`LaunchComplexProcessor.cs:58`).
- **Structure has no class:** `StationFactory.CreateStation` (`StationFactory.cs:31`) attaches a `ComponentInstancesDB` (`:60`) modules accrete onto — there is no station *design* type and no module budget anywhere.
- **Mega absent:** no tier, no `Mega` mount flag (`Enums.cs:126`).
- **Mount enforcement is UI-only:** `ComponentMountType` (`Enums.cs:126–144`) is a `[Flags]` enum stored on every design (`ComponentDesign.cs:56`, set from the template at `ComponentDesigner.cs:50`), but **no engine path gates installation on it**. Enforcement is purely the client filtering pick-lists: `ShipDesignWindow.cs:568` (only shows `ShipComponent`-flagged designs), `ConstructionDisplay.cs:60` (`PlanetInstallation` only), `IndustryDisplay.cs:415`, `PlanetViewWindow.cs:950`, `CargoListPanelComplex.cs:139/151`. Nothing in `Entity.AddComponent` / the industry install path checks "does this component fit this chassis."

**The build-list (highest leverage first):**
1. **Ship/station mass-budget cap + validity check** — *the keystone.* Mirror `GroundUnitAssembly.Compute`: give a chassis a mass/volume ceiling, sum mounted component mass, reject (mark `IsValid=false`) when the sum exceeds it — the exact shape that already works on the ground. Makes §0b real for space. **▸ SLICE A LANDED (2026-07-10):** `ShipDesign` now COMPUTES + EXPOSES `MassBudget`/`OverMassBudget` in `Recalculate` (`ShipDesign.cs`), sourced from the design's own mass at 1.0 headroom → byte-identical (nothing over budget, `IsValid` untouched, no construction path gated). Gauge `ShipMassBudgetTests` asserts the machinery + byte-identity AND prints every base-mod ship's mass (the calibration readout for the ceiling). **▸ SLICE D1 LANDED (2026-07-10):** the hull is now a real component — `ShipHullAtb` (the space echo of `GroundChassisAtb`, a clean `IComponentDesignAttribute` in `Ships/`) + a buildable `ship-hull` template (`installations.json`) + `default-design-ship-hull` (six-point registration: `earth.json` `StartingItems` + `ComponentDesigns`), carrying a GENEROUS 50,000 t `MassBudget`. `ShipDesign.Recalculate` now sources the budget from a **mounted hull** if present, else the generous self-derived fallback — so a hull-less design is still byte-identical (no ship mounts one yet). Gauge `ShipHullBaseModTests` (the gotcha-#10 JSON→atb sensor). **▸ SLICE D2a LANDED (2026-07-10):** the hull is now THREE GRADUATED TIERS off one `ship-hull` template (per-design dial overrides) — **light** (frame 0.5 t / budget 25 t — trimmed from 2.5 t in D2b so a fighter keeps its evasion edge), **medium** (10 t / 90 t, the template default), **heavy** (25 t / 180 t). The frame's own **Hull Mass** is now DECOUPLED from the budget (was a broken 5%-of-budget = 2,500 t frame; a hull's weight is the frame, not the ceiling). Budgets sized just above each ship class (measured 1–96 t fleet) so nothing current breaks, yet a big loadout on a small hull overflows → the §0b forcing bites. **Developer's call (2026-07-10): B, real mass** — the frame's weight is real and shifts evasion; "we're making a real game," existing-fleet balance may re-baseline. **▸ SLICE D2b LANDED (2026-07-10) — THE KEYSTONE IS DONE.** All 17 base-mod ships now mount their tier hull in `shipDesigns.json` (light: Sensor Sat / Wasp / drone; heavy: Leviathan / Gunship / Dropship / Bastion / Aegis; medium: the rest), so **every ship weighs its structural frame** — the "real game" mass. Verified no ship exceeds its tier budget (heaviest: Aegis 95.9 t + 25 t frame = 120.9 t < 180 t heavy budget), so `OverMassBudget` stays false and `ShipMassBudgetTests` stays green. The frame mass lowered evasion as intended; only ONE combat-calibration test needed attention — `WeaponTriangleBattleTests`'s fighter-evasion test — and it turned out to be a **pre-existing knife-edge** (60 fighters killed one Leviathan in a few salvos landing ~1 hit, so evasion couldn't separate the swarms; the hull mass merely tipped a 1-ship margin). Fixed at the root by rebuilding it on a *sustained* attacker (the `AddGun` battery pattern the green screen tests use) → robust, not weakened. Every other combat test (triangle, stress, battle sims) held. **Enforcement** (flip `IsValid` when over-budget) is wired in `Recalculate` but non-biting while budgets sit above each ship's mass — it's ready to bite the moment a player dials a loadout past its hull. Slice B (station module budget) remains optional/later.

**⏭ DEFERRED to the Chassis▸Hull door (§11.3) — the RICH hull (developer-approved 2026-07-10, "we're making a real game").** The light/medium/heavy tiers are honest **interim presets**, NOT a class system — under the hood there's already ONE `ship-hull` template with dials. When the Hull *door* is built, replace the current two free dials (Hull Mass + Mass Budget) with the parametric model: **two input dials — `Size` (capacity) + `Quality` (tech/material grade)** — from which everything EMERGES the §0b way (dial the outcome, the numbers fall out):
> - **Holds the components** → mass **Budget** = f(Size, Quality) — bigger holds more, higher quality holds more per tonne.
> - **Weighs something** → frame **Mass** = f(Size, Quality) — bigger is heavier, higher quality is lighter per unit capacity (feeds evasion/Δv).
> - **Takes the hits** → structural **HP** = f(Size, Quality) → wired into `ShipCombatValueDB.Toughness` (the hull is the backbone after armour/shields — the ground `GroundChassisAtb` already carries `BaseHP`; the ship hull should too). *This is a combat-wiring change that re-baselines combat, which is why it belongs with the door, not the foundation.*
> - **Has a shape** → signature/cross-section → sensor detectability (belongs with Sensors wiring).
> - **Hardpoints** (mount count/size) and **cost/materials** also emerge from Size × Quality.
>
> The current tiers become three points a player (or the start fleet) picks on those dials — so we build the rich hull **once, at the door, not twice.** `Size` + `Quality` gated by research (better hull tech unlocks). The foundation deliberately built only what the mass-budget cap needs (holds + weighs); HP/shape/hardpoints ride the door.
2. **Hull as a real design** — a hull component/attribute carrying the tonnage/volume ceiling + hardpoint budget; un-comment/replace `ShipInfoDB.Tonnage`; add the validity check to `ShipDesign.Recalculate`.
3. **Station design class + module budget** — a `StationDesign` (the missing type; closes the DESIGNER-AUDIT "no station design class" gap) with a module ceiling; optionally scale `StructuralIntegrity` off it.
4. **Mega tier + mount flag** — a new `ComponentMountType.Mega` (`1 << 7`) + a Mega-scale budget frame; the Death-Star/Titan/Dyson container.
5. **Engine-level mount enforcement** — gate `AddComponent`/the install path on `ComponentMountType` compatibility so the mount flags mean something without the UI (today UI-filter only).
6. **Walker/swarm carry classes** — extend `GroundCarryClass` (today only Personnel/Vehicle, `GroundChassisAtb.cs:46`) with distinct Walker/Swarm classes so a Titan-walker and a swarm-block route to their own bay/behaviour (the `Locomotion` enum already has `Walker`, `:12`).

### Prerequisite fixes & dead stubs (file:line)
- **`ShipInfoDB.Tonnage` — dead/commented** (`ShipInfoDB.cs:50`, `:52` `TCS`, `:80`). A resurrection point for the Hull budget, but as written it's a corpse — don't assume a ship has a tonnage number; it doesn't.
- **No `StationDesign` type at all** — `StationFactory.cs` builds an entity from a hardcoded blob list (`:33–67`); there is no design/blueprint to hang a budget on. Net-new class, not a wire on an existing one.
- **`ComponentMountType` enforced nowhere in the engine** — the flag is set (`ComponentDesigner.cs:50`) and read only by client filters (list above). A component with the wrong mount flag is *never* rejected engine-side; it just doesn't appear in the relevant UI pick-list. Any test that "proves a mount rule" today is asserting on the flag value (e.g. `GroundUnitBaseModTests.cs:62`), not on an enforcement path.
- **`GroundUnitAssembly.MaxItemFraction = 0.5`** (`GroundUnitAssembly.cs:49`) is a flagged NUMBER-TO-REVIEW, not a locked constant.
- **`InstallationsDB` is dead** (per root gotcha L1 / Industrial §7 footer) — installations live on `ComponentInstancesDB`; do not route a station "structure" budget through `InstallationsDB`.

### Design essence captured inline (why the mass-budget is the keystone at ALL scales)
The founding promise of the whole designer is §0b: *the numbers force the build, never a rulebook.* Nothing in the catalog says "a planet-cracker must be a Mega" — the **tonnage** of the beam plus the generator that feeds it plus the capacitor that buffers it is what won't fit anything smaller, so the physical budget *lets you find out* rather than an authored graph *telling* you. That promise is real machinery in exactly one place — the ground carry cap — and a slogan everywhere else, because Hull accumulates mass with no wall and Structure has no design to weigh at all. The keystone is not building a new idea; it is **porting an idea that already works up two scales.** Once a chassis at every tier supplies a ceiling and the design is checked against it, the mass number that every one of the other 36 doors already computes (a beam's mass, a reactor's mass, a drive's mass) suddenly *matters* everywhere — the Death Star's generator overflows the corvette, the torch-drive funnels you to a bigger hull, the sprawling station needs a bigger frame — and the §0b forcing becomes true across ships, stations, and megastructures instead of only on the battlefield. That is why one gate, mirrored from `GroundUnitAssembly.Compute`, is worth more than any other single build in the designer.

### §0g stamp
- **Reachable** — ✅ on Personnel/Vehicle: a frame is a `GroundChassisAtb` component, designed/researched/built/mounted/lost like any part (`GroundChassisAtb` is `IComponentDesignAttribute`, `GroundChassisAtb.cs:36`); the whole cradle-to-grave runs. ⏳ on Hull/Structure/Mega: a hull/station/mega frame is not yet a reachable buildable design (Hull is implicit, Structure is a host with no design, Mega is absent) — so those three fail the cradle-to-grave rung until the build-list above lands.
- **Mirrored** — the NPC designs hulls/stations against the **same budget loop** through the identical design/industry path (there is no player-only chassis code; `GroundUnitAssembly.RegisterAssembledDesign` and `ShipDesign.Initialise` are faction-agnostic). Once the cap exists, an NPC over-budget design is rejected by the same `Compute`/`Recalculate` check — delegation = NPC AI, no separate path.
- **Observable** — the gauge §0b mandates is the **live effective-vs-dialed throttle readout**: the ground assembler already surfaces it (`GroundUnitAssemblyResult.Valid` + `.Problems` list the over-budget reason, `GroundUnitAssembly.cs:29`, `:135`), shown live in the unit-designer UI. The space side needs the same readout wired onto the Hull/Structure validity check so an over-budget ship/station names *why* it won't build — without it the cap is an invisible bug (Failure-A: the number exists, just unshown).

### Cross-category shared state (Prime Directive)
Chassis is the ultimate shared-state partner: **it holds EVERY component**, so its budget gate binds all 36 other doors.
- **Weapons / Power / Propulsion / Defense / Sensors / Industrial / Logistical / Command** — each door's emergent **Mass** feeds the chassis budget (every footer says so: "the mass funnels the chassis," §1 Death-Star generator, §2 drive mass, §5 thick armour, §8 assembly-bay, §10 relay). The chassis is the single consumer that turns all those mass numbers from cosmetic into load-bearing.
- **`ComponentInstancesDB`** — the shared store where a chassis's mounted modules physically live (a station's `StationFactory.cs:60`; a ground unit's backing entity; a ship's component list `ShipDesign.cs:49`). Read the same memory the industry/damage/economy processors read.
- **`MassVolumeDB`** — the mass sink every drive/armour/generator writes into and the chassis budget reads back (§0b feedback: a heavier drive both eats budget AND drags its own accel).
- **Damage system (grave rung)** — destroying a chassis takes every mounted ability with it (`StationFactory.DestroyStation` tears down spawned sub-entities, `StationFactory.cs:101`; a ship's `ShipFactory.DestroyShip`; a ground unit's death). A decapitation strike on the frame collapses everything it carried.
- **Carrier / nesting (H6)** — a chassis housing smaller chassis (Star Destroyer TIEs, AT-AT troops, Ha'tak gliders) is shared state between Chassis and **Logistical ▸ Storage** (the bay holds units) + **Transfer** (launch/recover). Verify nested-chassis resolves through those doors, not a new Chassis knob.

### Holes owned/resolved (H5, H11, H6) → status + home
- **H5 — Modular / config / separable chassis** (saucer separation, S-foils, Replicator blocks). **Status: OPEN, MEDIUM.** Two sub-cases (`CATEGORIES.md §5`, H5 row): **(a)** chassis *config-states* — a toggle that swaps stats/mounts (S-foils cruise↔attack), cheap; **(b)** *separable sub-chassis* — one design → two independent units (saucer sep), harder. **Home: Chassis (this category)** — a config-toggle dial + a separable-sub-chassis mechanic; neither exists today (no config-state field on `GroundChassisAtb`/`ShipDesign`).
- **H11 — Scale extremes** (Death Star / Cube down to nanite / Replicator block). **Status: OPEN, LOW.** The ask is to *validate* the Chassis scale dial + tech/scale caps span ~10 orders of magnitude cleanly. **Home: Chassis** — specifically the **Mega tier (11.5)** at the top and a sub-Personnel end at the bottom. Blocked on Mega existing at all; resolve alongside build-list item 4.
- **H6 — Carrier / nested assemblies** (a chassis that houses & launches smaller chassis). **Status: MOSTLY COVERED, LOW-MEDIUM — needs verification.** Per `CATEGORIES.md §5` H6 row: Logistical ▸ Storage holds units and Transfer launches them, so the mechanism largely exists; the open item is to **verify nested-chassis + launch/recover actually resolves** end-to-end. **Home: Logistical ▸ Storage / Transfer (door 26/27), NOT a new Chassis knob** — Chassis only supplies the bay-carrying frame; the carrier behaviour is Logistical's. (Cross-ref: §8 Industrial footer's inert assembly-bay `MaxVolume` gate is the related wire.)

---

# 🏁 BLUEPRINT COMPLETE — all 11 categories / 37 doors specified

Every door of the component designer is now run through the full pipeline (dials → justified options → modellability → numbers → resolver/system insertion) and **all 37 base doors + the essence extensions are 🔒 LOCKED** (Weapons▸Exotic and the essence extensions locked 2026-07-09 — the two new-door capabilities C3 Relay §10.2 and Route Works are doors #36–#37, and every essence-extension dial is first-class in its category dossier). The 67 hand-authored templates collapse into **11 parametric categories**; specific things (phaser, submarine, AT-M6, bunker, Space Marine, Millennium Falcon, Death Star) fall out of dials; the multi-consumer rule (§0f) holds throughout; and every door names its Modelled ✅ / Wire ◐ / Defer ⏳ state honestly against the real engine.

**The top cross-category build-list the whole blueprint surfaced (highest leverage first):**
1. **The chassis mass-budget cap for ships + stations** (§11) — *the keystone.* Makes §0b ("the numbers force the build") real across all scales, not just ground. Mirror the working `GroundUnitAssembly` carry gate.
2. **Fix the refining input feed** (§7) — unblocks the entire materials economy (currently the refinery makes nothing).
3. **Armour's nature dimension + the ship flat-soak** (§5) — doubles the combat matchup (ablative vs energy, composite vs kinetic) and gives ships the swarm-bounce ground already has.
4. **A ship/unit upkeep clock** (§8) — the missing economic pressure behind mothballing, demobilization, and logistics strategy.
5. **The "a person's skill modifies an outcome" wire** (§6 + §10) — one hook lights up BOTH the Enhancers unit-caliber elites AND commander competence.
6. **Terraforming / world-development** (§9) — the missing franchise-earning "improve a world" arc.
7. **Consolidate the dead delegate record + the Fire-Control/targeting-computer wire** (§10 + §3 + §6) — connect built-but-dead knobs.
8. **The net-new science loops** — now **one** unified system, not two: the **field-site loop + catalog** (`docs/EXPLORATION-CONTENT-DESIGN.md`, main-merge 2026-07-07) — anomalies (mystery-box) and xenoarchaeology (relics) are two *catalog entries* on one shared loop, not two engines (§3.2). Build order starts with the `SystemBodyFactory.GenerateRuins` tautology-bug fix. Plus the two Mega/gas/terraform franchise systems.

The designer's founding idea is sound and mostly *there*; the blueprint's value is that it names, door by door, precisely the handful of keystone builds — led by the chassis mass-budget — that turn "95% built, 5% wired" into a designer where the numbers genuinely force every build at every scale.

---

# 📎 Main-Merge Delta — the `ai-faction-perf-planning` merge (2026-07-09)

> *Distributed 2026-07-09: the per-category corrections below now live inside each category's **⚙ Wiring Dossier**. This appendix is kept as the origin/rationale — the analysis of how the merge landed. For wiring, read the dossier.*

**What happened:** `main` merged the `claude/ai-faction-perf-planning-z5vl4w` branch (PR #77). We did a four-agent deep dive to see how it changes this blueprint's categories, doors, dials, and wires.

**The one load-bearing fact: the merge is 100% DESIGN DOCS — 9 `.md` files changed, ZERO `.cs` and ZERO `.json`.** So **not a single Modelled ✅ / Wire ◐ / Defer ⏳ verdict moves** — those grade the doors against the *engine*, and the engine is untouched. The merge can only change the **DESIGN behind the deferred and net-new doors** — it fills in what several ⏳ items were waiting on, and design-cuts one concept two of our wires cited. Nothing here re-opens a locked door; these are annotations + accuracy fixes on the design that sits behind the (unchanged) wires.

The new/changed docs: **AI-SELF-PLAY-DESIGN.md** (new, the leadership layer), **EXPLORATION-CONTENT-DESIGN.md** (new, the field-site loop), **INFLUENCE-PILLAR-DESIGN.md** (new), **LIVING-GALAXY-DESIGN.md** (new), and diffs to **DETECTION-DESIGN.md** (CUT SignalQuality), **ESPIONAGE-AND-INTELLIGENCE-DESIGN.md** (physical delivery + NPC counterparty + bioweapon), **DIPLOMACY / GOVERNMENT** (Foreign Minister).

## Per-door delta

| Door | What the merge changed | Verdict move? |
|------|------------------------|---------------|
| **§3.2 Survey** | Its two ⏳ NET-NEW deferrals — the **anomaly mystery-box** and **xenoarchaeology** — are now **DESIGNED**, and collapsed into **one** universal *field-site loop + catalog* (6 planetary + 5 space site types), not two engines. The loop reuses the `ResearcherDB` + scientist-assignment + 0–5 funding machinery (a "lab on a location"). First build step is named: fix the `SystemBodyFactory.GenerateRuins` tautology bug + wire a consumer. Also implies survey can now *create* jump routes (`JPFactory.CreateConnection`), not just reveal them, and that Hazard/Gravitational modes gain an exploration-reward second consumer. | **No.** Both rows stay ⏳ defer (still unbuilt) — but re-annotated "net-new → now DESIGNED (field-site loop, `EXPLORATION-CONTENT-DESIGN.md`)." |
| **§10.1 Command** | `AI-SELF-PLAY-DESIGN.md` **designs almost every NET-NEW item the door flagged**: the **stance** (data-driven presets, the decision the leader owns), the universal **0–5 funding** dial, **competence** (rung 4 — the officer's `BonusesDB` multiplies a real game number via `ModifiableValue`), the **auto-runner** (delegation = NPC AI; fills the dead `NPCDecisionProcessor`), and the **`AdministratorDB` consolidation** (generalize it → the universal delegate record). Adds a genuinely **NEW dial: Contracts** (fixed-term assignment, 5-yr default + early-break cost). Extends the seat model from "one shape, two altitudes" to a **19-role, two-chain roster** with **empire-cabinet seats** (Foreign Minister, Spymaster, Trade/Interior Minister, Grand Admiral, Chief Scientist). Adds two **empire-wide modulators**: race (sets lifespan/mortality) + government (re-skins stances). | **No** (still design-only, unbuilt) — but the door's headline softens from "unbuilt" to "**designed-only, concepts locked**"; add the **Contracts** dial + the modulator note + the cabinet-seat extension. |
| **§6.2 Unit Caliber** | The shared **"a person's skill modifies an outcome"** wire (§6 ∩ §10, build-list #5) is now **designed end-to-end** as Command's rung 4. The academy's **scarce talent draw** (`ColonyManpowerDB.TalentPool`) is designed, and the commander-buffs-from-above path (officer `BonusesDB` combat `FilterId` → `FleetDoctrineDB`) gets a home. **Keep the door's spine intact:** unit caliber stays the **unit's `Quality` multiplier** ("it's the unit, not the commander"); the merge designs the *separate, stacking commander* side, not the unit side. | **No.** No new dial; add cross-refs to `AI-SELF-PLAY-DESIGN.md` rung 4 on the Academy/Scarcity rows. |
| **§3.1 Detection (Dial E) + §3.4 EW (Jamming)** | **`SignalQuality` is DESIGN-CUT** (the engine field still exists — this is a decision to stop using it, not a code removal). Detection collapses to *strength only*. Our two wires cited `SignalQuality`, so they're re-described: coarse contact size → `SignalStrength_kW × cross-section` (automatic); fine class/IFF → the espionage **Information Ledger**; jamming → shrink `DetectionRange_m` / raise the strength threshold. **(Fixed inline above.)** | **No** (both were ◐ wire and stay ◐ wire) — but the wires are now factually correct against `main`. |
| **§3.4 EW Spoof/decoy** | Its deferral to "the intelligence/information-ledger layer" is **re-scoped + better-anchored, not resolved**: the target is now a locked layer (Spymaster MVP = Information Ledger + passive intel + per-faction seat + gather op, the *second* leader vertical slice). Spoofing itself = the **disinformation** build-order tail (sequenced last). The false-contact *injector* half stays a doable Wire; the *decide-what's-real* half stays deferred, now to a named layer. | **No.** Deferral pointer stays; update it to cite the Spymaster MVP / disinformation tail. |
| **§1.5 Weapons ▸ Exotic** | The new **bioweapon / covert-weapon** slice lands squarely on the **existing Exotic Bio/plague effect** — confirming the door, **not** adding one ("roles are dials, not doors"). It enriches three dials: finer **Selectivity** (species-specific strain, gated by a new **Biology & Genetics tech**), plus candidate new sub-dials **incubation** and **detectability**. The covert **delivery/proxy routes** (agent-plants-it, fund-a-proxy) are **out of scope** — espionage actions, not designer components. | **No** (§1.5 now 🔒 locked 2026-07-09; the bioweapon + SWAY dials are first-class in dossier ⚙1). |

## Out of scope (strategic layers, not the component designer)

- **Influence pillar** (4th conquest vector, religion = flavor #1) — **no door, no dial.** Leaders = People; the "kill" = the built legitimacy/rebellion system + one new belief-pressure input; religion is a **flavor on the government *modulator***, not a Civic door; holy worlds = an exploration field-site entry. Clean pass through the gear-vs-being boundary.
- **Living-Galaxy** — **touches nothing.** Its own doc declares it an OBJECTIVE/BAR, explicitly "not a system, engine, or layer." Produces no buildable component.

## Net effect on the blueprint

The merge is **good news for the blueprint's honesty**: three of the biggest ⏳ NET-NEW items we flagged as "undesigned" now have **real designs behind them** — the Survey field-site loop (build-list #8), the Command delegation/competence layer (#5 + #7), and the shared person-modifies-outcome wire (#5). One concept two wires leaned on (`SignalQuality`) was design-cut, and those wires are corrected. **No door needs re-locking; the wire audit is unchanged.** The build-list keystones (chassis mass-cap #1, refining feed #2, armour nature #3, upkeep #4) are entirely untouched by the merge.

---

# 🧭 Essence Extensions — the gear the merged-docs essence demands (2026-07-09) 🔒 *LOCKED*

> *Distributed 2026-07-09: the per-category gear (E1a–E1d) is now folded into each category's **⚙ Wiring Dossier** as first-class dial rows; the framework moves (E0a–E0c) are promoted to §0g/§0h/§0i. This section is kept as the origin/rationale — the essence synthesis and the two-hole convergence. For wiring, read the dossier + §0.*

The Main-Merge Delta (above) established the merge changes no wire. This section is the **creative follow-on the developer asked for**: with a deep read of what those five merged docs *mean*, what can be **added** to the designer — categories, doors, dials — that **matches and enhances that essence**, and how it all wires. Five essence-agents mined one doc each; a sixth synthesized. **Status: 🔒 LOCKED 2026-07-09 — first-class alongside the 37 base doors (see the Progress table's "essence extensions" block); wired from each category's ⚙ dossier.** The framework moves are locked into §0g/§0h/§0i; the per-category gear is locked as first-class dial rows in the dossiers; the two new-door capabilities (C3 Relay §10.2, Route Works) are doors #36–#37.

## E0 — The one essence, and the single highest-leverage structural move

Across all five docs the same instruction repeats in five costumes: **build ONE parametric shape, let the specifics be data/configuration/emergence, make the NPC run it too, and make both sides see it.** The pillar skeleton (`INFLUENCE-PILLAR-DESIGN.md`) = one shape, four media. Delegation = NPC AI (`AI-SELF-PLAY-DESIGN.md`) = one machinery, run both ways. The field-site loop (`EXPLORATION-CONTENT-DESIGN.md`) = one loop, every type is data. The Living Galaxy (`LIVING-GALAXY-DESIGN.md`) = don't build the emergence, build deep+connected+observable parts and refuse a director.

**The honest finding: the essence does NOT say "grow three new pillar categories for Espionage/Diplomacy/Influence."** The non-military pillars are *correctly* dominated by People + the legitimacy/relationship resolvers — that is the gear-vs-being boundary working, not a gap. Diplomacy is honestly gear-thin (its medium is *relationships*); Influence routes ~90% to non-gear systems; Espionage is thinly served but real. What every pillar in the skeleton **does** share is a **PROJECTION slot and a COUNTER slot** — and *those two slots are the physical, deliverable parts that legitimately are gear.* The designer under-serves that **seam**, not for lack of categories but because the seam is scattered and implicit.

**The keystone move (adds ZERO doors, moves ZERO boundaries, builds NO new loop):** promote the `WeaponProfile` from a combat-only footprint into a **medium-agnostic PROJECTION/COUNTER schema** — a cross-cutting **tag** (`pillar` + `skeleton-role`) over a shared dial-spine, authored once and graded against **each pillar's own resolver**. The `WeaponProfile` (nature × delivery × velocity × tracking × saturation × rate, resolved through the dodge/shield/armour matchup) already *is* a range/rate/selectivity/counter grammar — it has just been pretending it's only about weapons.

### E0a — The shared PROJECTOR ⛓ COUNTER dial-spine (the WeaponProfile, generalized)

| Shared dial | Military | Espionage | Influence | graded against… |
|---|---|---|---|---|
| **Reach** (range / insertion-depth / campaign-radius) | weapon range | agent insertion depth | culture-field radius | that pillar's resolver |
| **Cadence** (rate) | rate of fire | op tempo | conversion rate | " |
| **Selectivity** (target + what it beats + effect-bus) | nature-matchup + stun/capture | target-facet | flavor vs a pop's insulation | " |
| **Delivery** (how it arrives) | beam/bolt/missile | agent-as-hand vs remote | missionary-as-hand vs field | " |
| **⛓ Counter** (co-required, same spine) | armor/shields/PD | counter-intel/cipher | censorship/insulation | " |

Two pieces of metadata do the work on each existing projector/counter door: `pillar` (Military/Espionage/Diplomacy/Influence) and `skeleton-role` (Projection/Counter/Medium/Detection). What the tag buys: (1) **the §0d modellability test runs across pillars** — an influence projector is ✅/◐/⏳ against the *legitimacy/belief-pressure resolver*, an espionage projector against the *Information Ledger*, never against the combat resolver it doesn't belong to (this is §0f lifted from "consumers" to "media"); (2) **the anti-dominance law (§1.1: "every advantage must be bought; every projector has a counter") becomes cross-pillar** — you cannot ship a belief-projector without naming the insulation that beats it. The grammar is already latent: §1.5 Exotic carries a **Selectivity** dial (species-specific bioweapon) and the effect-bus already reaches from damage toward **capture/convert** — which is the homeless hole **H9** finally getting a home (a `Selectivity = convert` projector graded against the legitimacy resolver).

### E0b — §0g (LOCKED into §0): THREE acceptance criteria, not one

Cradle-to-grave is a solitaire, player-side, vertical test (it proves the *player* can reach and lose a thing). The essence adds the two axes it never covered:

> **§0g — Three acceptance criteria.** **Reachable** (cradle-to-grave, player-side) — miss it → a parachuted-in engine abstraction. **Mirrored** (opponent-side) — name how the NPC runs this same gear against the player *through the identical order path* (delegation = NPC AI; no player-only code). Miss it → solitaire (the inert-AI trap). *(Nuance: a pure-infrastructure door the NPC uses FOR itself, not AT you — but it must still USE it.)* **Observable** (the gauge, both sides) — name the readout that shows both sides this gear working. Miss it → `pretty` / an invisible bug (usually cheap Failure-A: the number exists, just unwired — build it). A door that passes only cradle-to-grave is a display piece.

Both criteria are nearly free because they **reuse the designer's own machinery**: the Mirror is cheap *because* a capability is a component on a shared door (the NPC mounting it is the same act as the player mounting it), and Visibility reuses §0b's live effective-vs-dialed throttle readout, extended to "shown to the target too."

### E0c — The authoring law: COUNT RESOLVERS, NOT DOORS

Exploration proved "build one loop, every type is data." Applied to the whole designer: **the 37 doors are not 37 loops — they are ~7 resolver-shaped loops, and the 37 doors + hundreds of presets are DATA those loops read** — the §0b physical-budget loop (all 37 doors), the combat resolver (Weapons+Defense), the detection math (Sensors), the move/closing model (Propulsion), the industry/economy loop (Industrial+Logistical), the field-site loop (all 11 exploration types = the lab loop reused), the delegation loop (all 19 leader roles = config on one pipeline). **The test before adding any door/flavor/site-type/leader-role: "is this a new LOOP, or DATA for an existing loop?"** A new door with no new resolver is data (tag it, table it, done). A new door claiming a new resolver must PROVE the resolver is genuinely distinct under §0d — otherwise it is a director in a costume (`LIVING-GALAXY`'s cardinal sin). `INFLUENCE-PILLAR` passes perfectly: "the rebellion model IS the kill; influence is just a new attacker on the same battlefield" — one new input, zero new loops.

## E1 — The gear, pillar by pillar (all dials on existing doors unless flagged NEW DOOR)

### E1a — Leadership essence → Command & Control gear (`AI-SELF-PLAY-DESIGN.md`)
*Finding: the leader layer is ~95% seats-and-people already scaffolded; the missing GEAR is command CONNECTIVITY.*

| Proposal | Door | Grade | Wire (file:line) |
|---|---|---|---|
| **Command Reach** — how far a seat's authority carries (Local / In-system / Interstellar-networked) | dial on §10.1 Command | ◐ Wire | `CommandReach_m` computed like detection range (`SensorTools.cs:425`, EMCON `:308`); the delegate order path checks it before reaching a unit; it's an emission the enemy sensor grid sees |
| **Academy Type/Tier + Competence Investment** — what leader a school makes, and how good (Navy/Ground/Civil/Science/Covert × school/college/university × mass-vs-elite) | dial on §9.2 Civic Development | ◐ Wire | `NavalAcademyProcessor` already rolls `NextBellCurve` (`:31`); wire writes the roll into the empty `BonusesDB` (rung-4 reads it) + draws scarce `ColonyManpowerDB.TalentPool` — the same handle that gates §6.2 elites (build once, both light up) |
| **Command-node Redundancy/Hardening** — single (cheap, decapitates) vs hardened (HTK) vs distributed (demotes instead of collapsing) | dial on §10.1 Command | ◐ Wire | on the decapitation handler: whether a lost node **empties** or **demotes** the scope; needs `AdminSpaceAtb.OnComponentUninstallation` implemented (currently throws, `People/AdminSpaceAtb.cs:42-45`) |
| **★ C3 / RELAY network node** — the connectivity of your whole command net; where orders travel = where the enemy can cut them | **NEW DOOR §10.2 Command▸Relay** | ◐ Wire / ⏳ Defer (addressing) | fills hole **H8**. `RelayRange_m` + a network-connectivity boolean between nodes → feeds Command Reach's interstellar scope; a relay is a `SensorProfileDB` emitter → **found, jammed (§3.4), destroyed** → the sever. Earns a door (not a dial) because it grants NO seat and holds NO officer — seat-less C2 plumbing, a different physical object with a different consumer (EW/detection) |

*Prerequisite bug-fixes the essence names: make seat occupancy durable (`AdminSpaceProcessor.CalcEntityAdminSpace` resets each pass, `:31-37`); implement `OnComponentUninstallation`; add one `LeaderLost` subscriber (`DestroyCommander` already publishes an unconsumed `EventType.CrewLosses`, `CommanderFactory.cs:115`).*

### E1b — Exploration essence → expedition & network gear (`EXPLORATION-CONTENT-DESIGN.md`)
*Finding: CONNECT-heavy, not build-heavy — the loop machinery exists; the gaps are three dead stubs.*

| Proposal | Door | Grade | Wire (file:line) |
|---|---|---|---|
| **Field-Lab deployment** — lab at home vs mobile ship-lab vs droppable field-station (hosts the scientist posting at a site) | dial on §9.2 Civic Development | ◐ Wire | reuses `ResearcherDB` + funding machinery (`ResearchProcessor.cs:87,180`); wire = a deployment flag + a `siteId` target |
| **Excavation medium** — pull a yield from a *site* (ore/gas/**site**) | dial on §7.1 Industrial Extraction | ⏳ Defer | mirrors `MineResourcesProcessor`; consumes `RuinsDB` once the `SystemBodyFactory.cs:1418` **tautology bug** is fixed (always early-returns → ruins never generate) |
| **Salvage / recovery** — a wreck: strip for mass / recover the design / recover survivors | dial on §7.2 Industrial Fabrication (recycle arm) | ⏳ Defer | needs `DamageProcessor.SpawnWreck` (`:478`, a stub that just deletes the ship) finished; unused events `WreckSalvaged`/`WreckComponents` (`EventTypes.cs:247,350`) are the hooks |
| **Tow / grapple** — move a whole *entity* (a drifting derelict), not just cargo | dial on §8.2 Logistical Transfer | ◐ Wire | `CargoTransferAtb` reused as tow throughput; towed mass drags the tug's `NewtonThrustAbilityDB` accel (the honest cost) |
| **Counter-interference** — how much hazard the scanner sees through (treasure hides in hazards) | dial on §3.2 Sensors Survey | ◐ Wire | `HazardResistanceAtb` (`:13`, stacks/scales by health) already exists; let a Survey component carry it so the `SpaceHazardTools.cs:112` sensor-cut shrinks |
| **★ ROUTE WORKS** — a studied anomaly → a PERMANENT route (wormhole stabilizer / precursor-gate reactivation / built jump gate) | **NEW DOOR** (or §2.4 Propulsion▸Warp▸gate + a Fabrication megaproject) | ⏳ Defer (deepest item) | fills hole **H8** (same hole as the C3 Relay). `JPFactory.CreateConnection` (`:124`) is a **dead stub**; `LinkJumpPoints` (`:135`) already pairs two JPs — the gear calls it in-play after a scientist posting resolves an anomaly. Stability 0–1 = the collapse/grave rung |

### E1c — Influence essence → soft-power projection gear (`INFLUENCE-PILLAR-DESIGN.md`)
*Finding: NO new door/category — confirms the Main-Merge Delta's "Influence = no door" for v1; the gear seam opens only at the essence's own "Influence 2.0" (the culture-field), and even then slots into existing doors. One new engine field: `ExternalBeliefPressure` on `LegitimacyInputs` (`Colonies/LegitimacyDB.cs:119`), the first purely-external attacker on that input surface, beside the existing `GovernorCompetence` slot.*

| Proposal | Door | Grade | Wire (file:line) |
|---|---|---|---|
| **SWAY (belief-pressure)** — the culture-field emitter; flavor (Religious/Cultural/Ideological) × persistent-zone delivery × magnitude×radius; zero effect vs a hive/machine | effect on §1.5 Weapons▸Exotic (the effect-bus — "never a new door") | ⏳ Defer | drives `ExternalBeliefPressure` → subtracted in `ComputeLegitimacy` (`LegitimacyDB.cs:66`) → toward `CollapseThreshold` 20 → `RebellionDB` (`LegitimacyProcessor.cs:77`) = the kill. Never touches the combat resolver (§0f) |
| **Broadcast Jamming** — active counter-influence (barrage vs targeted censor; costs your own morale) | dial on §3.4 Sensors EW | ◐ Wire | EW jamming (shrink `DetectionRange_m`) pointed at the emitter's belief-radius; a reduction on summed incoming pressure in `LegitimacyProcessor.cs:47` |
| **Cultural Insulation** — passive counter-influence (light vs heavy; dead weight vs an enemy running no influence) | dial on §5.3 Defense Hardening | ◐ Wire | Hardening's identity ("resist an incoming pressure the resolver never reads") + a belief-resist term on the defending world's `ExternalBeliefPressure` |

*Explicitly NOT gear: missionary/prophet/Influence-Minister = Beings (People); the domestic temple / state religion = a government-modulator flavor + an exploration holy-world field-site. Only the outward emitter + the insulation/censor hardware are buildable.*

### E1d — Espionage essence → intelligence & covert gear (`ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`)
*Finding: NO new door — the sensing/denial/delivery substrate is all built; the net-new engine object is the Information Ledger every wire points into. The agent stays a Being (needs a new `CommanderTypes.Agent`, `Enums.cs:263` — not gear).*

| Proposal | Door | Grade | Wire (file:line) |
|---|---|---|---|
| **ELINT/SIGINT flavor** — a firing solution vs an intelligence take (Tactical / ELINT / Multi-role) | flavor on §3.1 Sensors Detection | ◐ Wire | **re-homes the SignalQuality-cut `Resolution` dial** (`SensorReceiverAtb.cs:30`) from a dead detection field to *Information-Ledger confidence*: a Tactical contact stamps a rival's Military facet at binary **Inferred**; an ELINT take raises the band toward **Confirmed**. Rides the built `FirstContact.OnDetection` path |
| **Counter-intel / cipher cluster** — how hard your OWN faction is to read (ECCM / comms-cipher / counter-intel screen); the always-on mirror made buildable, works with no agent seated | dials on §3.4 Sensors EW | ◐ Wire / ⏳ Defer | a defensive term on the (net-new) covert-op detection roll + the enemy's Ledger-confidence gain vs your facets; one code path, both directions (the mirror). Mounts on a Structure = a colony's counter-intel directorate |
| **Covert-insertion / infiltration bay** — get an operative into a hostile system undetected (stealth-cover / trade-cover / diplomatic-cover) | dial on §8.2 Logistical Transfer | ◐ Wire | mirrors `GroundTransport.TryLoadUnit/TryLandUnit` (`:82/104`) but gated by the **inverse** of `HasOrbitalControl` (`:120`) — insert where you DON'T hold orbit — and "attach as infiltrator" (hidden state) instead of a visible region roster. Interceptable by the live sensor loop |
| **Covert-weapon warhead** — enrich the Bio/plague effect: Selectivity (species-specific strain, gated by net-new Biology tech) + incubation + detectability sub-dials | dials on §1.5 Weapons▸Exotic | ◐ Wire | effect lands via `DamageProcessor.OnColonyDamage` (`:289`, built) + morale/legitimacy; blowback via `DiplomacyDB`; delivery rides Guided (§1.4) or the infiltration bay above. Proxy route = an espionage action, out of scope for gear |

## E2 — The convergence: the essence points at exactly TWO holes, not three pillars

The two genuinely-new **doors** the whole exploration surfaced — the leadership **C3 Relay** and exploration's **Route Works** (jump gate) — **fill the same designer hole, H8 (`COMPONENT-DESIGNER-CATEGORIES.md` §5: "network/relay infrastructure — addressable, many-to-one").** A subspace command relay and a hyperlane gate are the same physical shape: an addressable network node whose destruction severs a link. And the influence/espionage **convert** effect fills the other open hole, **H9 (conversion/assimilation — ties to the espionage system)**, via the projector tag's `Selectivity = convert`. So the merged-docs essence does not sprawl the designer — it lands precisely on its two known-open holes. **Everything else is dials on existing doors: the 11-category / 37-door structure was right; the merge deepens it, it does not grow it.**

## E3 — Proposed build order (highest essence-leverage first)

1. **The three §0/framework moves** (paper, no code): stamp §0g (three acceptance criteria) + the projector/counter tag (E0a) + count-resolvers-not-doors (E0c) onto the framework. These cost nothing and make every later door land right.
2. **Fix the three dead stubs** exploration depends on, in order: `GenerateRuins` tautology (`SystemBodyFactory.cs:1418`) → the field-site loop → `SpawnWreck` (`DamageProcessor.cs:478`) → `JPFactory.CreateConnection` (`:124`). Lights exploration end-to-end (its own named order).
3. **The command-connectivity gear** (E1a): the durable-seat + `OnComponentUninstallation` + `LeaderLost` bug-fixes, then Command Reach, then the **C3 Relay door (H8)** — makes decapitation real.
4. **The ELINT re-home** (E1d): wire the now-free `Resolution` dial into the Information Ledger — the cheapest post-merge win, and it makes the SignalQuality cut *pay*.
5. **The person-modifies-outcome wire + Academy dials** (E1a) — build-list #5, lights commander competence AND §6.2 elites at once.
6. **Influence 2.0 gear** (E1c): the `ExternalBeliefPressure` input + the SWAY effect + its two counters — the "war without a weapon" made buildable.
7. **Route Works (H8) + the covert/salvage deferrals** — the deepest, most franchise-earning items last.
