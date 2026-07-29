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

**Suggested next: Defense.** It is the direct counterpart — shields, armour and their four nature resistances are
already half-surfaced by §5 above, and deriving it validates the weapon derivation from the other side.

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
