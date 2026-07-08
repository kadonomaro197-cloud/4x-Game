# The Component Designer — Dial Specifications (living doc)

**As of:** 2026-07-08 · the lowest-level spec: for each of the ~37 doors (`COMPONENT-DESIGNER-CATEGORIES.md`), the actual **dials** a player turns, the **derived stats** they produce, and the **dependencies** they require. Locked door-by-door; this doc is updated every time a decision is locked in.

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
| 6–37 | Propulsion · Sensors · Power · Defense · Enhancers · Industrial · Logistical · Civic · Command · Chassis | (all doors) | ⚫ pending |

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

*(Propulsion, Sensors, Power, Defense, Enhancers, Industrial, Logistical, Civic, Command, Chassis — pending, one lock at a time.)*
