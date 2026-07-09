# The Component Designer — Dial Specifications (living doc)

**As of:** 2026-07-09 · the lowest-level spec: for each of the ~37 doors (`COMPONENT-DESIGNER-CATEGORIES.md`), the actual **dials** a player turns, the **derived stats** they produce, and the **dependencies** they require. Locked door-by-door; this doc is updated every time a decision is locked in.

**The governing rule:** *a dial is only real if it moves a number the simulation reads.* No cosmetic knobs. Every dial bottoms out in an emergent stat a resolver consumes, and every benefit shows a cost (the `CONVENTIONS §16` transparency rule).

## Progress

| # | Category | Door | State |
|---|----------|------|-------|
| — | *(framework)* | Universal dials + Emergent-constraint (physical budget) model | 🔒 **LOCKED §0** |
| 1 | Weapons | **Energy** | 🔒 **LOCKED §1.1** |
| 2 | Weapons | **Ballistic** | 🔒 **LOCKED §1.2** |
| 3 | Weapons | **Melee** | 🔒 **LOCKED §1.3** |
| 4 | Weapons | **Guided** | 🔒 **LOCKED §1.4** |
| 5 | Weapons | **Exotic** | 🟡 **proposed §1.5** (awaiting lock) |
| 6 | Propulsion | **Reaction** (Newtonian main-drive) | 🔒 **LOCKED §2.1** |
| 7 | Propulsion | **Traction** (surface locomotion) | 🔒 **LOCKED §2.2** |
| 8 | Propulsion | **Fluid** (atmospheric + naval) | 🔒 **LOCKED §2.3** |
| 9 | Propulsion | **Warp** (FTL — warp bubble + jump) | 🔒 **LOCKED §2.4** |
| 10 | Propulsion | **Exotic** (reactionless / gravitic) | 🔒 **LOCKED §2.5** |
| 11 | Sensors | **Detection** | 🟡 **proposed §3.1** (awaiting lock) |
| 12 | Sensors | **Survey** | 🟡 **proposed §3.2** (awaiting lock) |
| 13 | Sensors | **Fire Control** | 🟡 **proposed §3.3** (awaiting lock) |
| 14 | Sensors | **Electronic Warfare** | 🟡 **proposed §3.4** (awaiting lock) |
| 15–37 | Power · Defense · Enhancers · Industrial · Logistical · Civic · Command · Chassis | (all doors) | ⚫ pending |

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

### 1.1 Weapons ▸ ENERGY  🟡 *proposed*
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

### 1.2 Weapons ▸ BALLISTIC  🟡 *proposed*
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

### 1.3 Weapons ▸ MELEE  🟡 *proposed*
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

### 1.4 Weapons ▸ GUIDED  🟡 *proposed*
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

### 1.5 Weapons ▸ EXOTIC  🟡 *proposed*
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

## ✅ Weapons category — COMPLETE (5/5 doors locked/proposed)
Energy 🔒 · Ballistic 🔒 · Melee 🔒 · Guided 🔒 · Exotic 🟡. **The pattern for the other 32 doors is now set:** for each door — (1) dials with *every option justified* (anti-dominance), (2) the *physical forcing* (what number funnels the build — power/ammo/recoil/reach/economy/tech), (3) the *modellability audit* (§0d — Modelled / Wire / Defer), (4) a *preset table* proving the franchise span. The four concrete doors are grounded in real physics + the existing combat resolver; the fifth (Exotic) is the open extensibility slot that grows with the effect bus.

*(Sensors, Power, Defense, Enhancers, Industrial, Logistical, Civic, Command, Chassis — pending, one lock at a time.)*

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

### 3.1 Sensors ▸ DETECTION  🟡 *proposed*
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
| Resolution / contact detail | ◐ **wire** | `Resolution` (MP) is stored but **not yet** driving contact quality/IFF confidence — a small wire to `SensorReturnValues.SignalQuality` / the contact's class read-out |
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

### 3.2 Sensors ▸ SURVEY  🟡 *proposed*
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
| **Anomaly / phenomena (the mystery-box)** | ⏳ **defer — NET-NEW** | needs an **anomaly/event spawn + investigation-reward loop** — the Stellaris/Star Trek exploration engine. *The game has no anomaly system.* **Franchise-earning: this IS how you stage "explore the unknown."** |
| **Xenoarchaeology** | ⏳ **defer — NET-NEW** | needs a **ruins-on-body + research-reward** system — Stargate/Mass Effect Prothean-ruins/Stellaris precursors. *The game has no relic/archaeology system.* **Franchise-earning.** |

**Reading:** Survey is **not thin — it was mis-measured.** Graded against combat it's one dial; graded against the exploration pillar it *belongs* to (§0f) it spans **eight scanning modes** feeding five different consumers, with an honest gradient: two ✅ **built** (geo/grav), a broad band of ◐ **wire** where the *data or system already exists and just needs exposing as a survey result* (cartography, habitability, life-signs, hazards — the biggest cheap-win cluster in the category, because `SystemBodyInfoDB`/`Hazards`/first-contact are all already in-engine), and two ⏳ **net-new systems that are genuinely missing AND franchise-earning** — the **exploration mystery-box** (anomalies) and **xenoarchaeology** (ancient tech). Those two aren't "thin door" filler; they're the **explore-and-discover half of the 4X** the game doesn't have yet, and naming them here (with their prerequisite systems) is exactly the design doc's job — it turns "Survey is shallow" into "Survey surfaces the two missing science loops the north-star franchises are built on." *This is the multi-consumer rule (§0f) paying for itself.*

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

### 3.3 Sensors ▸ FIRE CONTROL  🟡 *proposed*
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

### 3.4 Sensors ▸ ELECTRONIC WARFARE  🟡 *proposed*
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
| **Jamming** (degrade enemy detection) | ◐ **wire** | push the enemy's `SensorReturnValues.SignalQuality` down / shrink their `DetectionRange_m` against you — the detection math exists; add a jammer term to the scan |
| **Cloak / signature damping** | ◐ **wire** | push your own `ActivityMultiplier` / `SignatureBaseMultiplier` down — **EMCON already does this** (`FleetEmcon` Silent 0.15); a cloak is a stronger, component-based EMCON |
| **Decoy / spoof** (false contacts) | ◐ **wire** (injector) + ⏳ **defer** (the reading game) | inject a false `SensorContact` into the enemy track table (the table + `SensorContact` exist); but the "decide what's real" gameplay wants the **intelligence/information-ledger layer** (`docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`) for its full payoff |
| **Counter-EW / ECCM** | ⏳ **defer** | the mirror of jamming — needs the **jam mechanic built first**, then a resist term |
| **→ the combat resolver** | ✅ (via detection) | all of the above act on what `FleetDetects` returns → the first-strike resolver already honours a blinded fleet (`CanEngageTarget`), so EW × Detection × Weapons *stacks today* the moment the detection deltas are wired |

**Reading:** EW is the **one Sensors door that's mostly Wire/Defer** — the counter-detection layer genuinely isn't built (no jam/spoof/cloak component). But it is **not vaporware**: the rich detection engine (signature multipliers, signal quality, the contact table, EMCON) gives every EW effect a concrete hook, and **cloak is nearly free** (it's a stronger EMCON, which already runs). The one true deferral is the *decide-what's-real* spoofing game, which rightly waits on the intelligence layer. And because the resolver's fog + first-strike are already live, EW's payoff lands the instant its detection-deltas are wired — no new resolver work, it rides the same fog gate detection does.

**Numbers:** jam → a reduction on enemy `DetectionRange_m` / `SignalQuality`; cloak → your `ActivityMultiplier` (vs EMCON's 0.15 Silent floor — a cloak pushes lower); decoy → a false `SensorContact` with a chosen apparent signature. Calibrated to the *same* attenuation/EMCON constants Detection uses — EW just pushes them the other way. Cradle-to-grave: an EW suite is a **component** (research the net-new tech → build → install → lose it and your edge evaporates).

**Beyond combat (§0f) — signal management is the civilian cousin of EW:** the same "shape what signals go where" technology runs a **comms relay** (route + boost friendly signal across a system — the constructive twin of jamming), a smuggler's **civilian stealth** (run quiet past a patrol — cloak without a war), and a colony's **signal-discipline** posture. The cloak dial is literally a stronger EMCON, and EMCON is already every ship's peacetime *and* wartime lever. So while EW's *offensive* face (jam/spoof) is a combat/espionage tool, its substrate (signature control + signal routing) is a standing civilian capability — an EW/comms suite on a Structure chassis is a colony's communications-and-countermeasures array.

**Preset coordinates — the span:** escort jammer (barrage jam, blinds the enemy screen) · stealth raider (full cloak, first-strike ambush) · decoy tender (spoof drones, pads the plot) · ECCM flagship (counter-EW, keeps the fleet's picture clean) · **comms relay** *(FACILITY/ship — the constructive cousin: routes + boosts friendly signal)*.

---

## §3 Sensors — status (all four doors proposed, awaiting lock)
Detection 🟡 · Survey 🟡 · Fire Control 🟡 · Electronic Warfare 🟡. **Multi-consumer yardstick (§0f) — combat is ONE of five.** Detection → combat fog + navigation/SAR/traffic-control/observatory; Fire Control → combat targeting + precision-pointing of mining/tractor/docking/comms; EW → combat jam/spoof/cloak + civilian comms/signal-discipline; Survey → **the entire explore-and-discover pillar** (economy · expansion · colonization · research · first-contact). Headline readings, now read across all consumers:
- **Detection is the most-built door** — the whole EM-signature/attenuation/threshold/EMCON engine is live and already *gates combat*, and the same engine serves the civilian/facility sensors (nearly all ✅).
- **Survey is one of the RICHEST doors, not the thinnest** — it only looked thin measured by combat. Widened to the sci-fi survey space it spans **eight scanning modes** across five consumers: two ✅ built (geo/grav), a big ◐ **wire** cluster where the data/system already exists and just needs exposing as a survey result (cartography, habitability, life-signs, hazards — the category's biggest cheap-win pile), and **two ⏳ net-new, FRANCHISE-EARNING science loops the game lacks — the exploration mystery-box (anomalies) and xenoarchaeology (ancient tech)**. Surfacing those is the headline: they're the explore/discover half of the 4X.
- **Fire Control is the purest CONNECT door** — its dials (`Range`/`TrackingSpeed`/`FinalFireOnly`) are **built but dead** (unread by the resolver); the door's job is the wire (three existing fields → three resolver terms the salvo math already has), and that same wire generalizes to civilian precision-pointing.
- **EW is net-new** but rides the detection engine's ready surface (cloak ≈ a stronger EMCON, nearly free; jam/decoy = deltas on the live signal math; only the spoof "what's real" reading game defers to the intelligence layer).

**Build-list items that fall out:** (1) the **Fire-Control wire** — small, high-payoff; (2) the **survey-result exposure** cluster (cartography/habitability/life/hazard — cheap, the data's already in `SystemBodyInfoDB`/`Hazards`/first-contact); (3) two **net-new science systems** — the **anomaly/mystery-box** engine and the **xenoarchaeology/relic** system (both franchise-earning, both currently absent); (4) the **EW detection-delta layer** + its deferred **intelligence/information-ledger** partner for spoofing.

> **The §0f lens is now standing for every followup category** (Power, Defense, Enhancers, Industrial, Logistical, Civic, Command, Chassis): grade each door against *whatever system consumes it* — civilian, scientific, industrial, and infrastructural, on facilities as much as ships — never combat alone. Sensors is where it first changed a verdict (Survey thin → rich); expect it to change more.
