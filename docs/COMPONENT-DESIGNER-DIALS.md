# The Component Designer — Dial Specifications (living doc)

**As of:** 2026-07-08 · the lowest-level spec: for each of the ~37 doors (`COMPONENT-DESIGNER-CATEGORIES.md`), the actual **dials** a player turns, the **derived stats** they produce, and the **dependencies** they require. Locked door-by-door; this doc is updated every time a decision is locked in.

**The governing rule:** *a dial is only real if it moves a number the simulation reads.* No cosmetic knobs. Every dial bottoms out in an emergent stat a resolver consumes, and every benefit shows a cost (the `CONVENTIONS §16` transparency rule).

## Progress

| # | Category | Door | State |
|---|----------|------|-------|
| — | *(framework)* | Universal dials + Dependency model | 🔒 **LOCKED §0** |
| 1 | Weapons | **Energy** | 🟡 **proposed §1.1** (awaiting lock) |
| 2 | Weapons | Ballistic | ⚫ pending |
| 3 | Weapons | Melee | ⚫ pending |
| 4 | Weapons | Guided | ⚫ pending |
| 5 | Weapons | Exotic | ⚫ pending |
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

### 0b. The dependency model — "you can't have a tank without power"
Every component declares what it needs from the rest of the assembly. Two strengths:

- **⛓ REQUIRES (hard)** — the assembly is **invalid** without it. The supply gate refuses to build it. *Example: an Energy weapon ⛓ requires a power source supplying ≥ its draw.*
- **◐ BENEFITS-FROM (soft)** — it works without, but **degraded**. *Example: a weapon ◐ benefits from Fire Control — without it, long-range accuracy tanks.*

This is a **requirement graph** over the assembly, checked at Level-2 (assembly), the general form of the existing power/ammo/crew gates.

### 0c. The supply currency scales with the chassis
The *thing* being supplied changes with scale, but the gate is the same:
- **Ship / Vehicle / Station:** watts (power), ammo (mass), crew.
- **Personnel:** carry-weight / stamina (a soldier has no reactor; a self-powered sidearm just *weighs* something).
So "requires power" means **reactor watts** on a tank and **battery/cell + carry-weight** on a trooper — same ⛓ edge, chassis-appropriate currency.

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

**Delivery dials (how the energy arrives — the big diversity axis):**
| Dial | Range of settings | Real effect |
|------|-------------------|-------------|
| **Beam ↔ Bolt** | sustained beam · pulsed beam · bolt · burst | **beam ignores dodge; bolt is dodgeable** (sets the evasion matchup) |
| **Focus** | tight ↔ wide | tight = long-range, single-target, high per-hit; wide = short cone, splits/AoE (the phaser wide-stun, a scatter emitter) |
| **Charge-up** | instant ↔ long charge | instant = light repeater; long = big alpha (siege beam) — ⛓ a long charge needs a capacitor |
| **Dwell / sustain** | per-pulse ↔ continuous | continuous beams deal damage over dwell time on target |

**Matchup dials (what it's good/bad against):**
| Dial | Settings | Real effect |
|------|----------|-------------|
| **Energy nature** | Thermal · Particle · Plasma · **Ion (anti-shield)** · Graviton (exotic) | sets the damage-vs-defense matchup (ion bypasses shields, thermal is mirror-scattered, etc.) |
| **Penetration ↔ Splash** | pierce ↔ area | armor-piercing single-target vs soft-target/AoE |

**Creative / diversity dials (each still moves a real stat):**
| Dial | Real effect |
|------|-------------|
| **Frequency modulation** | resists **adaptive shields** (the Borg counter, H2b) — costs mass/power |
| **Overcharge headroom** | dump stored power for a bigger shot at heat/cooldown risk (risk-reward alpha) |
| **Thermal bloom** | firing spikes your **signature** (Detection/EW) — a stealth build dials this down, trading output |
| **Medium performance** | atmospheric / underwater scatter — does it work in air/water? (a laser blooms in fog; gates a submarine or atmo energy weapon) |
| **Cooling need** | sustained rate is throttled by heat unless ◐ cooled |

**Derived stats (computed, shown to the player):**
`power draw = output × rate ÷ efficiency` · damage/salvo · sustained DPS · dodge-fraction (delivery × tracking) · signature spike (output × thermal-bloom) · mass · cost.

**Dependencies:**
- ⛓ **Chassis hardpoint** (mount) — universal.
- ⛓ **Power ≥ draw** — Generation and/or Storage on the same host. *This is the "tank needs power" edge: an Energy weapon cannot exist on a chassis that can't feed it.*
- ⛓ **Energy Storage (capacitor)** — *only if* charge-up/alpha delivery is dialed up (you must buffer the shot before you can release it).
- ◐ **Fire Control** (Sensors) — long-range accuracy; without it, hit-chance at range collapses.
- ◐ **Cooling** (Systems/thermal) — sustained rate; without it, high rate-of-fire overheats and throttles.

**Preset coordinates — proof the dials span the franchises:**
| Weapon | Output | Delivery | Focus | Nature | Notable dial | Dependency bite |
|--------|--------|----------|-------|--------|--------------|-----------------|
| **Lazpistol** | tiny | bolt | tight | thermal | stun/kill mode | tiny cell — mounts on Personnel |
| **Phaser array** | med | continuous beam | adjustable (wide-stun↔tight-kill) | particle | stun/kill + wide-arc | ship reactor |
| **Turbolaser** | high | bolt | tight | plasma | high penetration | ⛓ big reactor |
| **Ion / disruptor** | med | bolt/beam | tight | **Ion** | shield-bypass | reactor |
| **Death-Star beam** | planet-scale | beam | convergent | graviton | very long charge | ⛓ **Mega chassis + huge reactor + capacitor bank** — the deps make it *only* buildable at Mega scale |

The Death Star is the point: you can *dial* a planet-cracker on any chassis, but the **dependency gate** (power + capacitor + budget) means only a Mega chassis can actually supply it — the design falls out of the dials, the *feasibility* falls out of the graph.

---

*(Ballistic, Melee, Guided, Exotic — and the other 32 doors — pending, one lock at a time.)*
