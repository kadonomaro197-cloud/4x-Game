# THE DESIGNER NORTH STAR — derive the doors from what the sim reads

**🔒 LOCKED 2026-07-29 by the developer:** *"This is it, the source of truth, the north star — this is how the designer
must be."*

**Live reference (the worked result, driveable):** https://claude.ai/code/artifact/85c50703-bc30-4a83-b5c8-7a54ece2ab25

**What this is.** The method for building every door of the component designer, plus the first category worked all the
way through (**Weapons**). It supersedes the authored weapon taxonomy — five doors and forty-one dial groups become
**two choices and four sliders** — and it is the pattern every remaining category is owed.

---

## 1. THE LAW

> **A door is not a thing you invent. It is a thing you DERIVE from the numbers the simulation actually reads.**

Work in this order, always:

1. **Find the sim's input surface.** What values does the resolver read off this kind of component? Verify in source,
   not in a doc.
2. **Group them by the question they answer.** Several variables usually answer one question three different ways.
3. **Separate FORCED from FREE.** Some values are forced by physics once you choose what kind of thing it is — those
   become a **choice**. The rest are independent — those become **sliders**.
4. **The choices are the doors. The sliders are the depth.**
5. **Prove it by reproducing everything that already exists.** If the derivation can't rebuild the components in the
   game today, the derivation is wrong.
6. 🔴 **NAME WHAT GOES OUT.** For every door, list what its output actually feeds — which systems read it, and where.
   **This is not bookkeeping, it is the diagnostic** (developer's standing instruction, 2026-07-29).

**Why step 6 earns its place — it is what caught Hardening.** Two failures are invisible until you trace the output:

- **A mis-filed door.** Hardening sits in Defense, but its output (`EnvironmentalResistance`) is read in exactly one
  place — the environmental **attrition** step. It never reaches a weapon hit. **A door whose output does not arrive
  at the system it is filed under is in the wrong category**, and only an output trace shows that.
- **A dead dial.** A dial whose output reaches nothing is the "never ship a dead knob" failure. The input test
  (*which sim variable does it write?*) catches some; the output test catches the rest — a dial that writes a real
  field nothing downstream ever reads.

**So the full test for a door is two-sided:** *what does it write* (§ the input surface) **and** *where does that go*
(§ the output map). Both, every time.

**The test for any dial, at any time:** *which of the sim's variables does it write?* Writes one → real. Writes none
but costs mass → fine, that's the price. **Writes none and costs nothing → it is a bug in the design.** That is
`CONVENTIONS §16` and "never ship a dead knob," made checkable in one step.

**The corollary that kills doors:** if two "types" differ in no variable, they are **one type at two slider
positions** — not two entries on a menu.

---

## 2. WEAPONS — the input surface (verified in source, 2026-07-29)

Ten values reach the fight. Nothing else on a weapon does.

| # | Variable | Read by | Decides |
|---|---|---|---|
| 1 | `DamagePerSecond` | firepower sum + fire mix | how much hurt |
| 2 | `Velocity` | `CombatKernel.HitFraction` | dodgeable or not |
| 3 | `Tracking` | `CombatKernel.HitFraction` | whether it follows a dodge |
| 4 | `Saturation` | `HitFraction` floor | volume that lands regardless |
| 5 | `Range_m` | `BuildFireMix` gate + `WithinWeaponRange` | reach — **and the size of the battle** |
| 6 | `Nature` | `ShieldSoakFraction` · `ArmourResistFor` · `IsAmmoNature` | which defence stops it, and what feeds it |
| 7 | `Penetration` | `CombatKernel.ArmourSoak` | cancels flat armour point for point |
| 8 | `PerShotEnergy` | `BurstShotCount` → `ArmourSoakBurst` | alpha vs chip against armour |
| 9 | `HeatPerSecond` | fleet `HeatPool_kJ` throttle | sustained-fire ceiling |
| 10 | `Delivery` | `IsInterceptable` | can point-defence shoot it down |

**Per-target, for completeness:** Toughness · Evasion · ShieldCapacity/Regen · armour + its four nature resistances ·
RoleWeight · doctrine multipliers.

---

## 3. THE FIVE QUESTIONS

The ten group into five, and the grouping is what makes them designable:

| Question | Variables | Notes |
|---|---|---|
| **How hard does it hit?** | Damage per second | = damage per shot × rate of fire. Set either, the other follows. |
| **Does it land?** | Shot speed · Follows a dodge · Volume of fire | **Three ways to beat one problem.** |
| **What gets through?** | Good against · Armour pierced · Damage per shot | Shields · flat armour · the burst identity. |
| **How far?** | Reach | And the longest reach present sets the engagement's size. |
| **Can it keep it up?** | Heat build-up · Supply · Interceptable | Heat throttle, ammo-or-power, point-defence. |

**Why "does it land" is three variables.** There are exactly three ways to defeat a dodge, and the kernel takes
`max(speedTerm, tracking)` then floors the result at the saturation floor:

- **too fast to avoid** (velocity → you cannot dodge light)
- **it steers after you** (tracking)
- **so many that some land anyway** (saturation)

---

## 4. AXIS 1 — HOW IT GETS THERE (the first choice)

**Forced by physics.** What you send decides speed, correction, supply, and interceptability. You do not get a say.

| Setting | Shot speed | Follows a dodge | Runs on | Shot down in flight |
|---|---|---|---|---|
| **Contact** | — nothing travels | — | nothing | no |
| **Beam** | light speed | n/a | power | no |
| **Projectile** | finite | ~0 | ammo | no |
| **Guided** | slow | high | ammo | **yes** |

- **Beam is undodgeable because of the number**, not because someone decided so.
- **Guided is slow but corrects** — which is exactly why point-defence is the only answer to it.
- **Contact is the degenerate case:** nothing crosses the gap, so nothing supplies it. That is *why* melee has no
  logistics. Its damage also leans on the chassis's strength rather than on a magazine.

---

## 5. AXIS 2 — WHAT IT'S GOOD AGAINST (the second choice)

**Independent of Axis 1**, which is the whole point — 4 × 4 = **sixteen real weapons before you touch a slider.**

### 5.1 The numbers (verified, `CombatKernel.cs:62-68`)

| Setting | Shields stop | Best against | Worst against |
|---|---|---|---|
| **Exotic** | **0%** — passes straight through | shielded targets | cheap unshielded targets (overkill) |
| **Energy** | **50%** — half bleeds through | unshielded hulls; still half-works on shields | reflective / ablative plate |
| **Explosive** | **75%** | crowds, soft targets | hard armour, and shields |
| **Kinetic** | **100%** — shield eats all of it | armour, if concentrated | anything with its shield up |

### 5.2 🔑 THE ASYMMETRY — this is the load-bearing insight

| Defence | Who decides the number |
|---|---|
| **Shields** | **The attacker.** Fixed by the setting; identical on every target in the game. |
| **Armour** | **The defender.** Plating carries its own four resistances (`ArmourVsKinetic/Energy/Explosive/Exotic`, default 1.0 = plain plate), so ablative plate can shrug off energy and thin against slugs. |
| **Hull** | **Nobody.** Past both, damage is damage. |

**There is no fixed "Energy vs armour" number and there must never be one.** Publishing one would be a lie about how
the system works.

### 5.3 The practical reading

**Exotic is the specialist; Energy is the one you field.** Exotic ignores a shield outright but is overkill on cheap
targets. Energy is the middle that always works: half bleeds through, so it still hurts a shielded target where a
kinetic round accomplishes *literally nothing*.

---

## 6. THE FOUR SLIDERS

| Slider | Ends | Writes |
|---|---|---|
| **Total damage** | sidearm → superlaser | Damage per second |
| **Shot size ↔ rate of fire** | small & fast → big & slow | Damage per shot · Rate · Volume of fire |
| **Reach** | knife-fight → stand off | Range |
| **Focus** | spread wide → all on one point | Armour pierced · Volume of fire |

### 6.1 The two couplings (both are real, both must be visible)

**① Damage per second, damage per shot and rate of fire are NOT three dials.** They multiply. **Set two, the third is
decided for you.** This is the honest constraint the authored taxonomy hid by listing them as separate options.

**② Armour cares about shot size. Shields do not.** Armour subtracts a **flat amount from each shot**
(`ArmourSoakPerPoint 1.5`, floored at `ArmourMinPassFraction 0.1`), so a hail of tiny shots is almost entirely
absorbed while one big shot mostly punches through. A shield soaks the **same fraction** either way.

> **This single interaction is why a railgun and a flak battery are different weapons** despite being the same family,
> the same setting, and the same damage per second. Opposite ends of one slider, completely different answers to a
> tank. It also makes your anti-fighter gun useless against armour **from the maths**, not from a rule anyone wrote.
> **It was already in the engine and nothing had ever shown it.**

---

## 7. NAMING — compositional, generic, no franchise IP

The name is **built from the sliders**, not looked up. Three parts, joined and de-duplicated:

**`[scale] + [focus] + [core noun by shot size]`**

- **Scale** ← Total damage → `Compact · Light · — · Naval · Spinal` (contact weapons use `Compact · Light · — ·
  Heavy · Siege`, because "Spinal Blade" is nonsense)
- **Focus** ← Focus slider → `Scatter · — · Focused`
- **Core noun** ← Shot size, five per family×setting combination

**Two guards, both required:**
1. **No contradictions** — the adjective is dropped when the noun already carries it. No "Scatter Lance," no
   "Focused Flak Battery."
2. **No stutters** — word-level de-duplication. No "Heavy Heavy Beam."

**Result: 1,073 distinct reachable names** from 80 core nouns, up from 48 in the lookup table it replaced.

🔴 **Vocabulary rule: generic ordnance language ONLY.** Chaingun, autocannon, mortar, mass driver, lance, glaive,
ram, torpedo, kill vehicle. **No franchise IP** — the developer's explicit call. The old table's Bolter / Chainsword /
Lightsaber are deleted and must not return.

---

## 8. THE PROOF — all eleven weapons in the game today fall out

Family and setting checked against what `ShipCombatValueDB` and the base-mod ground weapons actually construct.

| Weapon in the game today | Gets there by | Good against | Sliders |
|---|---|---|---|
| Laser | Beam | Energy | small shots, fast |
| Disruptor | Beam | Exotic | — |
| Railgun | Projectile | Kinetic | big shots, long reach, concentrated |
| Flak | Projectile | Kinetic | tiny shots, huge rate, spread wide |
| Plasma | Projectile | Energy | medium both |
| Missile | Guided | Explosive | — |
| Ground rifle | Projectile | Kinetic | small shots · 500 m |
| Autocannon | Projectile | Kinetic | medium · 2 km |
| Tank cannon | Projectile | Kinetic | big shots · 4 km |
| Ground energy weapon | Beam | Energy | · 20 km |
| Claw / melee | Contact | Kinetic | contact only |

> **Plasma finally means something:** the **thrown-energy** corner. That is why it always sat oddly beside a railgun
> — it is not one. Today it borrows the railgun's range constant, which is the code admitting it never knew what
> plasma was.

---

## 8b. WHAT GOES OUT — the Weapons output map (verified in source)

**The Prime Directive applied at the door.** Change a weapon dial and this is the blast radius.

| What leaves the door | Goes to | Why it matters |
|---|---|---|
| **`WeaponProfile`** (all ten) | `CombatKernel` → hit fraction, shield drain, armour soak, casualties | the primary path — everything else below is a side effect of it existing |
| **`DamagePerSecond`** → summed as **`Firepower`** | `ShipCombatValueDB` | the ship's nameplate rating |
| **`Firepower > 0`** | 🔴 **`FleetAssembly`** (`:122`, `:247`, `:303`) | **it is the armed-vs-tender test.** A ship with no weapon is never folded into a strike fleet — so a weapon dial decides whether the AI fields the hull at all |
| **`Firepower` + `Toughness`** | **`FactionRollup`** (`:83`) | the faction's total military strength — **how threatening every rival thinks you are** |
| **`Firepower`** | `MilitaryComposition` · `AutoResolve` | fleet deployability; the instant off-screen resolver |
| **`Range_m`** → `MaxReach` | 🔴 **`WithinWeaponRange`** | **starts battles** — and under **LD-30 it sizes the arena they are fought in** |
| **`Nature`** → `IsAmmoNature` | ammo drain · heat throttle | routes the weapon to **Logistical** (magazines) or **Power** (reactors) |
| **`HeatPerSecond`** | fleet heat pool | creates demand for **radiators** — a Defense/Systems purchase |
| **`Delivery`** → `IsInterceptable` | point-defence | the only thing Delivery decides, and it is what makes PD a counter |
| **Firing itself** → `ShotsFiredThisTick` | **EMCON `ActivityMultiplier`** → `SensorTools` | **a ship that shoots is seen farther.** Weapons feed detection |
| **Mass** | **Chassis** budget → crew · cost · research · build time · mobility | the universal cost chain |
| **A space weapon on a ground chassis** | `SpaceWeaponGround` → the ground resolver | one weapon, both domains |
| **The component being destroyed** | Damage system | the grave rung — shoot the gun off and the capability is gone |

> ⚠ **One quirk worth knowing:** `Firepower` is computed **at build and cached**, so the AI's planner cannot read it
> before a ship exists — `DefendResolver.cs:130` explicitly works around this by reading the design attribute at plan
> time instead. **A weapon dial's effect on AI planning is therefore indirect**, and that is a real seam.

---

## 9. WHAT THIS KILLS

| Killed | Why |
|---|---|
| **Pulse vs Continuous Beam** | A pulse of light travels at light speed too. **No number** makes one dodgeable and the other not — the distinction was fiction. They are one weapon at two shot-size positions. |
| **The Exotic door** | Exotic is a setting on *what it's good against*, not a way of getting there. A gravitic beam is Beam+Exotic; a warp torpedo is Guided+Exotic. |
| **Exotic's non-damage effects** | Mind control, jump inhibition, conversion **write none of the ten**. They are not weapons. Calling them a weapon door hid that they need an effects system that does not exist. |
| **Four of the six delivery values** | Beam · Bolt · Slug · Cloud · Guided · Blast — the engine asks them one question: *can point-defence shoot this down?* Bolt, Slug and Cloud all answer no, identically. Three names for one row. |

**Superseded documents:** `docs/economy/COMPONENT-DESIGNER-CATEGORIES.md` §2 (the five Weapons doors) and
`docs/economy/COMPONENT-DESIGNER-DIALS.md` §1 (all of Weapons, 41 dial groups). Their *content* is preserved — every
option is captured in the live reference above — but the **five-door structure is superseded by this derivation.**

---

## 10. WHAT IS OWED

**Weapons is derived. The other ten categories are not.** Each is owed the same treatment, in this order of work:

1. Find its input surface in source (what does the sim read off a component of this kind?)
2. Group by question · separate forced from free
3. Choices become doors, the rest become sliders
4. Prove it reproduces everything that exists today

**Defense is now DERIVED — see Part Two below.** It validated the weapon derivation from the other side (§14).
Nine categories remain; Propulsion is next in the authored order.

**Nothing here is built.** This is a design lock, not a slice. The build order remains: make weapon range designable
across all six classes first (`docs/TESTING-TRACKER.md` **G-C0**) — that is the prerequisite the arena model needs
(`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` **LD-30**).

---

## 11. CONNECTIONS (Prime Directive)

- **Feeds IN:** `ComponentDesigner` + the template JSON (the dial wall) · `TechData` ceilings (what a dial may reach)
  · the mass/volume budget (`CONVENTIONS §16` — every dial must cost).
- **Feeds OUT:** `ShipCombatValueDB.Calculate` → `WeaponProfile` → `CombatKernel`. **That path is the only reason any
  of these dials matter.**
- **Shares STATE:** `WeaponNature` is read by three separate systems — shield soak, armour resistance, and ammo-vs-
  power supply. Changing its meaning touches all three.
- **Triggers:** `WithinWeaponRange` = `Max(reachA, reachB)` — **a weapon's reach starts battles and, under LD-30,
  sizes the arena they are fought in.**

**Cradle-to-grave:** unchanged and load-bearing. A weapon is a component — mined, refined, researched, designed,
built, mounted, and **lost when it is shot off**. This derivation changes only how its dials are *organised*; it
parachutes in no engine abstraction the player cannot reach.

---

# PART TWO — DEFENSE (derived 2026-07-29)

**Live reference:** https://claude.ai/code/artifact/41bb5a42-52a9-4c54-bcab-bc7680c0f84a

## 12. THE INPUT SURFACE (verified in source)

| Variable | Read by | Layer |
|---|---|---|
| `Evasion` (0..0.95) | `CombatKernel.HitFraction` | miss |
| `ShieldCapacity_J` · `ShieldRegen_Jps` | `CombatKernel.ResolveShield` | buffer |
| armour points (`Defense`) | `CombatKernel.ArmourSoak` | bounce |
| ship `ArmourSoakVs*` — a **fraction**, 0 = plain | fleet soak | bounce, per nature |
| ground `ArmourVs*` — a **multiplier**, 1.0 = plain | `ArmourResistFor` | bounce, per nature |
| `Toughness` / `Health` | `ApplyCasualties` / health drain | structure |
| `ToughnessMult` · `DamageTakenMult` | doctrine / stance | flat multiplier |
| fortification `DefenseMult` (capped ×2) | ground pool divide | flat multiplier |
| cover multiplier | terrain | flat multiplier |
| `EnvironmentalResistance` | ⚠ **the ATTRITION step only** (`GroundForcesProcessor.cs:235`) — never combat | environment |

## 13. THE FOUR LAYERS — and this is the resolver's real order

**miss → shield → armour → structure**

| Layer | Mechanic | Beaten by | Recharges | Cares about shot size |
|---|---|---|---|---|
| **Evasion** | you miss entirely | shot speed · tracking · volume | — | no |
| **Shield** | soaks a **fraction**, depletes | sustained fire · Exotic | **yes** | **no** |
| **Armour** | subtracts **flat, per shot** (floored at 10%) | big shots · penetration | no | **yes** |
| **Structure** | absorbs until gone | nothing — it just ends | no (repair) | no |

## 14. 🔑 THE MIRROR — Defense confirms the Weapons derivation from the other side

| | Who sets the number | Cares about shot size |
|---|---|---|
| **Shield** | 🔴 **the ATTACKER** — 0 / 50 / 75 / 100% by their setting | no |
| **Armour** | ✅ **the DEFENDER** — thickness and nature tuning | **yes** |
| **Structure** | nobody | no |

> **You cannot build an anti-energy shield.** A shield's response to a damage type is fixed by what is being fired at
> it. Armour is the exact opposite. **This is §5.2 seen from the receiving end, and it holds** — which is the point of
> deriving Defense second.

## 15. 🔒 LOCKED — ARMOUR NATURE TUNING IS ZERO-SUM (developer, 2026-07-29)

> *"zero-sum, tuning against one costs you against the others — and if you want better defense you fit more."*

**Two separate axes, and keeping them separate is the whole ruling:**

| Axis | What it does | Paid in |
|---|---|---|
| **How much plate you fit** | raises total protection | **mass** (and everything mass drags — crew, cost, build time, mobility) |
| **How that plate is tuned** | redistributes protection across the four damage types | **nothing — it is zero-sum** |

**The math this forces:** the four nature values share a fixed budget.

```
VsKinetic + VsEnergy + VsExplosive + VsExotic = 4.0     (plain plate = 1.0 each)
```

Tune toward energy and kinetic drops by exactly what energy gained. **There is no setting that is better against
everything** — that is the anti-dominance rule satisfied by construction rather than by a balance pass.

**Why this is a good decision, stated so it is not undone later:**
- **Tuning becomes a bet, not an upgrade.** Your plate is a wager on what the enemy fields — and they can counter it.
- **It makes intel load-bearing.** Knowing what a rival shoots is what tells you how to tune. That wires Defense to
  the sensor and espionage layers for free.
- **It keeps the two axes honest.** Want to be tougher against everything? Fit more, pay the mass. Want to be tougher
  against *one* thing for free? You cannot — you can only choose where to be weak.

**⚠ Flagged for the developer (balance, not design):** a floor and ceiling on any single value, so a design can never
sit at 0.0 against a damage type (completely naked) or absurdly high against one. Suggested band **0.25 … 2.5**,
unset until playtest.

### 15.1 The ruling DECIDES the ship↔ground divergence

Ship and ground armour are two different mechanics today (§16 finding 4). **Zero-sum settles which one survives**,
because a zero-sum budget needs a **neutral midpoint to distribute around**:

- Ground's model **has** one — `1.0 = plain`, four values summing to 4.0. ✅ **This is the correct parameterisation.**
- Ship's model **does not** — `0 = plain` is a floor, not a midpoint. A budget cannot be shared around zero.

**⇒ The ship side must move to the ground parameterisation** (multiplier, 1.0 neutral, flat-per-shot soak) — which
also closes the long-standing gap where **the flat-bounce identity the kernel is built around only ever fires on the
ground**, because the space path zeroes `PerShotEnergy` so `BurstShotCount` is always 1.

*(Cross-ref: `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §8 row 6 — "flat armour bounces many small hits" is
HANDLED on the ground and a GAP in space. Same defect, now with a decided direction.)*

## 16. WHAT THE FOUR AUTHORED DOORS GOT WRONG

| Door | Finding |
|---|---|
| **Hardening** | ⛔ **Not a combat defence at all.** It writes `EnvironmentalResistance`, read in exactly one place — the environmental **attrition** step (`GroundForcesProcessor.cs:235`). It never touches a weapon hit. **Moves to environment/survival.** |
| **Fortification** | ⛔ **Not something you wear.** A *building* that modifies a *place* — a divisor on incoming, capped at halving it, ground-only. **Moves to infrastructure.** |
| **Evasion** | 🔴 **The best defence in the game is not sold at this door.** It comes from hull volume + engine acceleration (Chassis + Propulsion); the only override is filed under Propulsion ▸ Exotic. **The Defense door cannot sell the layer that stops damage from ever being rolled.** |
| **Armour** | 🔴 **Two mechanics wearing one name** — ship: HP lump + nature *fraction* (0 = plain); ground: flat per-shot subtract + nature *multiplier* (1.0 = plain). **Opposite defaults, different maths.** Resolved by §15.1. |

## 16b. WHAT GOES OUT — the Defense output map (verified in source)

| What leaves the door | Goes to | Why it matters |
|---|---|---|
| **`ShieldCapacity_J` · `ShieldRegen_Jps`** | `FleetCombatStateDB.ShieldPool_J` (space) · `GroundUnit.CurrentShield` (ground) → `ResolveShield` | the buffer layer |
| **Armour points** | ship: folded into **`Toughness`** → `ApplyCasualties` `EffToughness` · ground: **`Defense`** → `ArmourSoak` | 🔴 **two different destinations — this is the divergence §15.1 resolves** |
| **Armour nature values** | `ArmourResistFor` → scales the flat soak | the defender's half of the matchup |
| **`Toughness`** | **`FactionRollup`** (`:83`) · `AutoResolve` · `MilitaryComposition` | **armour makes you look more threatening to every rival AI** — a defensive purchase changes diplomacy and targeting |
| **Shield power draw** | **Power** (generation + storage) | a shield you cannot feed is a shield you do not have |
| **Mass** | **Chassis** budget → the same universal cost chain | |
| **`EnvironmentalResistance`** (Hardening) | ⛔ **the environmental ATTRITION step ONLY** (`GroundForcesProcessor.cs:235`) | 🔴 **the smoking gun — it never reaches combat.** This single output trace is what proved Hardening is mis-filed |
| **Fortification `DefenseMult`** | ground combat incoming divisor → region hold → **capture** | infrastructure, not kit — and it feeds the capture loop, not the damage loop |
| **The component being destroyed** | Damage system | shoot the shield generator off and the buffer is gone |

> 🔑 **The output map surfaces something the input surface hides:** **defence is not a private matter.** Toughness
> feeds `FactionRollup`, which is how rival AIs rate your military strength — so **fitting more armour changes who
> attacks you**. A purely defensive decision has an offensive-posture consequence, and nothing in the authored
> Defense doors says so.

---

## 17. WHAT DEFENSE COLLAPSES TO

**Two real combat layers, one choice and three sliders:**

| | Choice / slider | Notes |
|---|---|---|
| **Choice** | Shield · Armour | the two layers you can actually buy |
| **Shield sliders** | **Capacity ↔ Regen** | the direct mirror of the weapon's shot-size ↔ rate: a big buffer that returns slowly, or a small one that returns fast |
| **Armour slider** | **Thickness** | more is simply better, paid in mass |
| **Armour tuning** | **Nature distribution** | 🔒 **zero-sum**, §15 |

**Structure** comes from the chassis and the components bolted to it. **Evasion** comes from the chassis and the
drive. Neither is a Defense purchase, and the doc should stop implying they are.

---

## 18. RUNNING TALLY

| Category | State |
|---|---|
| **Weapons** | ✅ derived — 5 doors + 41 dial groups → **2 choices + 4 sliders** |
| **Defense** | ✅ derived — 4 doors → **1 choice + 3 sliders**, two doors relocated out of the category |
| Propulsion · Sensors · Power · Enhancers · Industrial · Logistical · Civic · Command · Chassis | ⏳ owed |

**Standing rule from here on (developer, 2026-07-29):** every category derived from now on ships **both** maps — the
**input surface** (what the door writes) and the **output map** (where that goes). Weapons §8b and Defense §16b are
the pattern. Connections found this way belong in `docs/SYSTEM-CONNECTION-MAP.md` too — that file owns the
system-to-system graph.
