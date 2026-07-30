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

---

### 1a. 🔒 THE INTRINSIC TEST — what may be a dial at all (developer, 2026-07-29)

> *"We need to determine what parameters can be set regardless of the other components on an entity."*

**A component dial must be settable WITHOUT knowing anything else about the entity it will be mounted on.**

You are designing **a part**, not a ship. At design time you do not know the hull, the fuel it will carry, or what
else is bolted on. So:

| The number | Where it lives |
|---|---|
| Settable knowing only this part | ✅ **a component dial** |
| Needs to know what else is mounted, or how many | **an ASSEMBLY decision** — the Entity Assembler |
| Computed from the finished entity | **an EMERGENT readout** — shown, never set |

**Why this matters more than it looks: it is the missing test for the two-tool split.** The design has always said
*"the Component Designer makes PIECES; the Entity Assembler puts pieces on a chassis"* (**L1/L2**) — but it never gave
a way to decide which side a given number belongs on. **This is that test.**

**It bites immediately.** Two dials in the authored spec fail it:

- **Propulsion ▸ fuel load** — fuel is in **tanks** (Logistical ▸ Storage). An engine does not carry fuel. Δv cannot
  be a drive dial because Δv needs the wet and dry mass of *the whole ship*.
- **Weapons ▸ Guided ▸ "magazine size"** — a magazine is **Logistical ▸ Storage** too. Same error, same reason.

**The corollary:** the headline numbers players care about most in movement — **acceleration, Δv, evasion** — are
**all emergent**. Not one of them can be set on a drive, because every one needs the finished entity's mass or volume.
**That is not a gap. That is correct** — and the designer must show them as readouts, never as sliders.

**The test for any dial, at any time:** *which of the sim's variables does it write?* Writes one → real. Writes none
but costs mass → fine, that's the price. **Writes none and costs nothing → it is a bug in the design.** That is
`CONVENTIONS §16` and "never ship a dead knob," made checkable in one step.

**The corollary that kills doors:** if two "types" differ in no variable, they are **one type at two slider
positions** — not two entries on a menu.

---

### 1b. 🔒 EVASION IS A MULTIPLIER — the developer's reading, and the code already agrees (2026-07-29)

> *"What if the evasion is just a multiplier of sorts?"*

**It already is one. There is no to-hit roll anywhere in either resolver.** This is the single line that decides who
dies:

```csharp
// CombatEngagement.cs:897
EffToughness = cv.Toughness * cs.ToughnessMult / landed
```

Evasion divides **into toughness**. `1 ÷ landedFraction` **IS** an effective-health multiplier. The ground resolver
writes the same arithmetic the other way round — it multiplies the damage down instead of the health up
(`GroundForcesProcessor.cs:422`: `contribution = pool × share × HitFraction(...)`) — which is mathematically
identical. Nothing rolls a die; both sides just scale a number.

**So the developer read the model correctly through the fog of its own vocabulary.** The battle report's *"42% on
target (58% dodged)"* implies dice that do not exist. The honest phrasing is *"this hull is 2.4× harder to kill
against that fire mix."*

#### Why saying it out loud matters

**① It puts evasion in the SAME CURRENCY as armour** — and then the whole defence stack is one sentence:

> **effective health = Health × (evasion multiplier) × (armour multiplier)**, with **shields** as the one
> *depleting pool* on top.

All three are **MATCHUP** multipliers, and the keys are the two weapon axes: **evasion is keyed to DELIVERY**
(velocity · tracking · saturation), **armour and shields are keyed to NATURE**. 🔑 **The defence model is the weapon
model in a mirror** — which is the same finding §14 reached from the other side, now with one shared unit.

**② It exposes a balance cliff nothing in the game currently shows.** The multipliers have hard ceilings, and they
are not close to each other:

| Layer | Ceiling on effective health | Set by |
|---|---|---|
| **Evasion, point blank** | **×20** | `EvasionCap` 0.95 (`ShipCombatValueDB.cs:96`) |
| **Evasion, at range** | **×50** | `MinLandedFraction` 0.02 (`CombatKernel.cs:46`) |
| **Armour** | **×10** | `ArmourMinPassFraction` 0.1 (`CombatKernel.cs:78`) |
| **Shields** | a finite pool | designed capacity |

🔴 **Evasion is a 2×–5× LARGER multiplier than armour can ever be** — and it is the one bought at a *different door*,
in a *different currency*, against *no defence budget*. **That is the number behind §25.1's "Propulsion is the game's
largest defensive purchase."**

*(Cleanup flagged, not done: on ships the limit that bites at point blank is `EvasionCap`, but at range it is
`MinLandedFraction`. Two dials for one ceiling, and the one named "cap" is not always the one doing the work.)*

**③ It makes an honest designer readout possible.** You cannot show a "hit chance" on a part that has never met an
enemy. You *can* show **"× effective health vs a slug / vs a beam"** — which tells the truth and admits the matchup.

#### The caveat that keeps this from being a pure rename

**It is a MATCHUP multiplier, not a flat one.** ×1 against a beam — you cannot dodge light. Large against a slow slug
at long range. So any readout must be a **small curve or matrix, never one number** — the same shape §15 already
locked for the armour readout. Consistent by construction.

### 1c. 🔒 DECIDED — THE TWO CEILINGS NOW AGREE (developer chose (c), 2026-07-29)

**Plain English: there are two different safety stops on the same machine, fitted by different people, and only one of
them is labelled "the limit."**

| Dial | Value | What it actually limits |
|---|---|---|
| `EvasionCap` | 0.95 | the evasion **number** — *"no hull is more than 95% evasive"* |
| `MinLandedFraction` | 0.02 | the fraction of fire that **lands** — *"at least 2% of incoming fire always connects"* |

**They bite in different situations, which is the whole problem:**

| Situation | The maths | Which stop bites | Result |
|---|---|---|---|
| **Point blank** | `dodge = evasion × (1 − tracking)` → max **0.95**, so 5% lands | **`EvasionCap`** | **×20** effective health |
| **At range** | the range term ADDS more dodge on top, so dodge clamps at **1.0** → 0% would land | **`MinLandedFraction`** | **×50** effective health |

*(The floor is really `saturationFloor = max(sat/(sat+50), MinLandedFraction)`, so `MinLandedFraction` only bites for
**low-saturation** weapons — a flak gun's own saturation floors it far higher.)*

**Why it matters:** if you ever want to tune *"how tough can a dodgy ship get,"* you must change **both** dials **and**
know which one is active in which situation. The one named "cap" only governs half the cases. That is exactly the
shape of thing that makes a balance pass go wrong quietly.

**Three ways out:**

| | The change | Cost |
|---|---|---|
| **(a)** | **Leave it, document it.** Both are defensible — a ceiling on agility and a floor on volume-of-fire getting through. | **zero risk**, but the confusion stays |
| **(b)** | Delete `EvasionCap`; `MinLandedFraction` becomes the single ceiling → **×50 everywhere** | **changes point-blank combat** — dodgy hulls get much tougher up close |
| **(c)** | Keep both but make them **agree**: set `MinLandedFraction = 1 − EvasionCap = 0.05` → **×20 everywhere** | **changes long-range combat** — dodgy hulls get less tough at range |

**🔒 DECIDED — (c), and BUILT 2026-07-29.** `CombatKernel.MinLandedFraction` 0.02 → **0.05** (= `1 − EvasionCap`),
so the ceiling is **×20 everywhere**. One number now means one thing, and it *lowers* the largest multiplier in the
game (§1b).

**Narrow by construction:** the effective floor is `max(Saturation/(Saturation+50), MinLandedFraction)`, so this only
binds for weapons whose own saturation floor is below it — **`Saturation < ~2.6`, i.e. low rate-of-fire ballistics.**
A flak gun floors itself far higher and never sees this number. So it bites exactly the case it was meant to: **a slow
slug at long range against a nimble target.** ⚠ It is still a **balance change that moves live combat numbers** (not
an additive flag-gated slice) — CI is the gauge.

**Gauge that cannot rot:** `CombatKernelTests.TheTwoCeilings_Agree_SoEvasionCapsAtOneMultiplierEverywhere` asserts
`MinLandedFraction == 1 − EvasionCap`, that the same hull under the same fire hits the same ceiling at gap 0 and at a
gigametre, that the ceiling is ×20, and that evasion still exceeds armour's ×10 — so a future retune of either dial is
a deliberate, visible act.

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
unset until playtest. **Explained in §15.2.**

### 15.2 ⚠ WHAT "THE BAND" IS AND WHY IT NEEDS TWO NUMBERS (open decision)

**Plain English: it is a graphic equaliser with a fixed total power.** You can boost the bass, but the treble has to
come down. **The band is how far the sliders are allowed to travel** — how much boost, and how much cut.

You are tuning **four** values, one per damage nature (kinetic · energy · explosive · exotic). The zero-sum ruling
fixes their **total**. The band fixes how lopsided any **one** of them may get.

**Why it needs BOTH ends — each extreme breaks the game a different way:**

| Missing end | What happens |
|---|---|
| **No ceiling** | you build plating that is near-**immune** to one nature. Combat becomes a hard counter: whoever guessed the enemy's weapon nature wins outright. **The decision moves out of the battle and into a coin flip made before it.** |
| **No floor** | you can tune a nature to **zero**. A specialist plate is then a free win against one enemy and instant death against another — the same failure mirrored. |

**⇒ A band keeps it a LEAN, not a SWITCH.** The high end must be meaningful (a specialist genuinely walls its
matchup); the low end must still soak something (a bad matchup hurts, it does not delete you).

**In real numbers, with a total budget of 4.0 across four natures (neutral = 1.0 each):**

| Band | Extreme specialist | What it means in a fight |
|---|---|---|
| **0.25 … 2.5** *(suggested)* | 2.5 / 0.5 / 0.5 / 0.5 | **×2.5 tougher** in its matchup, **×0.5 — half as tough** outside it. A real bet, survivable when wrong. |
| 0.1 … 4.0 *(too wide)* | 4.0 / 0.1 / 0.1 / … | near-immune vs one, **paper** vs the rest — the coin-flip failure |
| 0.75 … 1.5 *(too narrow)* | 1.5 / 0.83 / … | barely worth tuning — the dial stops being a decision |

**🔴 The prerequisite (§15.1) — the band cannot be set until the ship side moves to the ground parameterisation.**
A band is a *lean around a neutral midpoint*, and **the ship's `ArmourHardeningAtb` has no midpoint**: its four
`SoakVs*` fields default to **0** and clamp to `[0, MaxSoakFraction 0.9]`, so "plain plate" is the **floor**, not the
centre. A ship cannot currently express *"weak against energy"* at all — only *"normal, or better."* The ground's
`GroundArmorAtb` **does** have the midpoint (`VsKinetic…` default **1.0**, a resistance multiplier that can go both
ways). **So §15.1's direction is not optional bookkeeping — it is what makes a band expressible at all.**

**Order of operations:** ① move the ship side to the 1.0-neutral multiplier · ② set the band · ③ playtest and
re-tune. **Doing ② before ① is not possible.**

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
| Propulsion | ✅ derived — see Part Three |
| Sensors · Power · Enhancers · Industrial · Logistical · Civic · Command · Chassis | ⏳ owed |

**Standing rule from here on (developer, 2026-07-29):** every category derived from now on ships **both** maps — the
**input surface** (what the door writes) and the **output map** (where that goes). Weapons §8b and Defense §16b are
the pattern. Connections found this way belong in `docs/SYSTEM-CONNECTION-MAP.md` too — that file owns the
system-to-system graph.

---

# PART THREE — PROPULSION (derived 2026-07-29)

**Live reference:** https://claude.ai/code/artifact/3c1d784b-df96-444a-93c5-c2c512a831d4

## 19. THE HEADLINE — this is the OPPOSITE problem from Weapons

**Weapons had too many doors with overlapping dials. Propulsion has almost no dials at all.**

The engine designer today offers **one slider and one dropdown**. Verified in `engines.json`:

| Dial | Editable? | How it is actually decided |
|---|---|---|
| **Mass** | ✅ **the only one** | bounded by two techs |
| **Fuel Type** | ✅ a dropdown | which fuels you have unlocked |
| Exhaust Velocity | ❌ display only | `ExhaustVelocityLookup(Fuel Type) + TechData(...)` |
| Fuel Consumption | ❌ display only | `Mass × 0.3 × TechData(...)` |
| Thrust | ❌ display only | `Exhaust Velocity × Fuel Consumption` |

🔴 **Follow the chain and the door empties out.** Thrust is **linear in engine mass**; exhaust velocity does not depend
on mass at all. So **a bigger engine is strictly better on both counts**, and the only real decision left is which
fuel you have unlocked. **There is no trade anywhere in the category** — which is the "if an option has no catch it is
a bug in the design" rule failing at the level of a whole door.

**The most famous trade in rocketry — high thrust with poor economy, or gentle thrust with enormous range — cannot be
expressed.** The tech tree has ion and nuclear-pulse engines, but those are **tech levels you unlock**, not **choices
you make**.

## 20. THE PHYSICS THE DOOR IS MISSING

> **For a given engine power, thrust × exhaust velocity is a constant.**
> Power is `½·ṁ·v²`; thrust is `ṁ·v`; therefore **`T · v = 2P`**.

Spend your power on a fast thin exhaust and you get economy with little push. Spend it on a slow heavy one and you
get push with little range. **This is a real zero-sum that is already true in physics** — it needs no invention, only
exposing, and it costs **no new resolver field**.

It is the exact mirror of the weapon's **shot size ↔ rate of fire** and of the locked **zero-sum plate tuning**: one
budget, split two ways, no free lunch.

**And a second tension falls out for free:** fuel load buys Δv but adds mass, and mass fights your own thrust. **Your
evasion falls as your range grows.** That tension does not exist in the game today.

## 21. THE INPUT SURFACE (verified in source)

| Variable | Read by | Decides |
|---|---|---|
| `ThrustInNewtons` | `accel = Thrust ÷ MassDry` → `CalculateEvasion` | **Evasion** |
| `ExhaustVelocity` | Tsiolkovsky | Δv per kg of fuel |
| `FuelBurnRate` · `TotalFuel_kg` | fuel drain · Δv | endurance, logistics |
| `Reactionless` (bool) | skips propellant | unbounded Δv |
| `InertialessDriveAtb.EvasionOverride` | evasion **floor** | breaks the mass↔evasion coupling |
| `WarpAbilityDB.MaxSpeed` | `FleetCombat.WarpSpeedFloor` | strategic transit |
| bubble create/sustain cost | stored electricity | needs **Power** |
| `GroundLocomotionAtb.SpeedFactor` | `GroundMobility.SpeedMultForUnit` → `Speed_kmh` | the closing march |
| `RoughHandling` | `TerrainMult` **and** `LocomotionTerrainMult` | travel time **and** a combat multiplier |
| ~~`Amphibious`~~ | 🔴 **nothing reads it** | **not an input at all** — the surface scan mis-read a *comment* for a wire (§23.3b) |
| drive mass (emergent) | chassis budget **and back into its own accel** | the feedback loop |

## 22. THE FIVE QUESTIONS

| Question | Variables |
|---|---|
| **How fast do you change direction?** | Thrust ÷ mass → **Evasion** — the combat payoff |
| **How long can you hold a position?** | Δv → `ManeuverBudget` — the kiting clock |
| **How fast do you cross the map?** | warp max speed · ground march speed |
| **Where can you go at all?** | medium · `Amphibious` · terrain handling |
| **What feeds it?** | fuel · stored power · nothing |

## 23. THE ONE CHOICE — WHAT DO YOU PUSH AGAINST

| Setting | Works in | Runs on | Combat payoff |
|---|---|---|---|
| **Reaction mass** | anywhere, vacuum included | fuel | **evasion + control of the range** |
| **The ground** | surfaces only | fuel · power · muscle | march speed + terrain edge |
| **Air or water** | that medium only | fuel | access (altitude/depth deferred) |
| **Spacetime** | interstellar | **stored power** | none — strategic only |

🔑 **Unlike Weapons, there is no second axis, and inventing one would be dishonest.** Where you can go and what feeds
you are **entailed** by what you push against — they are not independent choices. Propulsion is genuinely
**one choice and three sliders**.

> ### ⚠ CORRECTED 2026-07-29 — the first pass OVER-COLLAPSED this category
> An earlier draft of this section said *"one choice and two sliders,"* flattening dials that genuinely differ. **That
> was wrong on two counts:** it dropped Traction's and Warp's own editable dials, and it missed that **a drive's
> SIGNATURE is already wired and scales with thrust** (§23.2). The corrected shape is **one choice, and the choice
> picks your dial set** — because the sim reads different things depending on what you push against.

**The sliders for REACTION — after the intrinsic test (§1a):**

| Slider | Intrinsic? | Why |
|---|---|---|
| **How much drive you fit** | ✅ | this engine's own mass — a property of the part |
| **Push ↔ economy** | ✅ | this engine's thrust and exhaust velocity — true whatever it is bolted to |
| ~~Fuel load~~ | ❌ **CUT** | fuel is **tankage** (Logistical ▸ Storage). An engine does not carry fuel. |

Ground adds **rough-terrain handling**, which already exists and is intrinsic.

> 🔑 **The developer's reading, and it is the right one:** *"push ↔ economy, out of all the others, is the only one
> that makes sense."* It is the only **Reaction** dial that is **both intrinsic and a real trade** — drive size is
> intrinsic but has no catch beyond mass, and everything else people reach for (Δv, acceleration, evasion) is
> emergent.

### 23.2 🔴 THE TRADE IS THREE-WAY, NOT TWO — signature is already wired

**Missed in the first pass, found in `engines.json`:**

```
Sensor Signature → AtbConstrArgs(3500, PropertyValue('Thrust'))
                 → SensorSignatureAtb(temp 3500 K, magnitude = Thrust)
```

**A drive's signature magnitude IS its thrust**, and that feeds the live detection system. So push ↔ economy is not a
two-way trade:

| Push harder | Ease off |
|---|---|
| more thrust → **more evasion** | more exhaust velocity → **more Δv per kg** |
| more thrust → 🔴 **louder — seen from farther away** | quieter — you pick your moment |

**And detection decides who shoots first.** So *hard shove* costs you **stealth**, not merely economy — which makes
this the richest single trade found in any category so far, and **it is already true in the code.**

**⇒ A `Signature suppression` dial is therefore a genuine candidate:** quiet the drive at the cost of mass or
efficiency. Intrinsic ✅ · writes a variable the sim reads ✅ · has an obvious catch ✅. *(Proposed, not built.)*

### 23.2a 🔒 SIGNATURE MUST COST MORE IN THE OTHER PARAMETERS (developer, 2026-07-29)

> *"Heat signature needs to cost more in the other parameters."*

**Correct, and the source says WHY it costs so little today.** The full chain, verified end to end:

```
Thrust → SensorSignatureAtb.PartWaveFormMag          (engines.json)
       → SensorProfileDB.EmittedEMSpectra[].Magnitude (SensorProfileTools.cs:33-40)
       → DetectionRange_m = √( magnitude × activity × scale ÷ (4π × threshold) )   (SensorTools.cs:441,466)
```

🔴 **SQUARE ROOT.** Double your thrust and you are seen from only **1.41×** as far; ten times the thrust, 3.2×.
Meanwhile doubling thrust doubles acceleration, which climbs the **evasion multiplier** (§1b) directly. **So loud is
nearly free today** — inverse-square dilution eats the penalty before the player ever feels it.

**Three ways to make it bite. Take (a).**

| | The change | Verdict |
|---|---|---|
| **(a)** | **Suppression eats the SHARED POWER BUDGET** — zero-sum, exactly like the §15 armour ruling | ✅ **the ruling** |
| (b) | Signature superlinear in thrust (`magnitude = Thrust^k`, or feed *waste heat* instead of thrust) | ⚠ defensible physics, but it moves a **shipped** number and every existing detection with it — a **balance-pass** call, not a design one. Flagged, not done. |
| (c) | Weaken the √ dilution | ❌ **no.** That is the inverse-square law, and it is what makes *closing* meaningful. |

**(a) in full — the locked shape:**

```
P_drive = P_total × (1 − suppression)
T · v   = 2 · P_drive
```

**Power spent staying quiet is power not available for thrust OR exhaust velocity.** So suppression is not a third
way to split the budget — it is a **tax on the whole budget**, costing push *and* economy at once. Then the shroud's
**mass** is dead weight on the hull, dragging acceleration (and therefore the evasion multiplier) down further.
**Suppression pays twice.** No new physics needed, and it is honest: cooling and shrouding a drive really does spend
power on not-thrust.

### 23.2b 🔴 AND THE COST ALREADY THERE IS THE BIGGEST IN THE GAME — nothing says so

`Pulsar4X.Tests` → **`FirstStrike_SeerWipesBlindEnemy_Unscathed`**: two **EQUAL** fleets, one blinded, fog on. The
seeing side **wipes it taking ZERO losses.**

> **Signature does not cost you a stat. It costs you the opening exchange — and the repo's own gauge says the
> opening exchange is the whole battle.**

**One ugly interaction, named rather than hidden:** the loud ship is also the FAST ship, and speed decides who
dictates the range (`FleetManeuver` picks the controller off evasion — §25). So a hard-shove drive gives away its
position **and** controls the engagement range. Whether those cancel is a live-tuning question — but **a designer
that shows neither is lying twice.**

### 23.3 THE CHOICE PICKS YOUR DIAL SET — the corrected structure

> ⚠ **The FAMILY AXIS below was superseded 2026-07-30 — read §26d first.** The four families were treated as one
> exclusive choice ("what do you push against"), but **a ship carries a thruster AND a warp drive**, so Warp was
> never a rival to Reaction. The door asks **two** questions: ① sublight (Reaction | Traction) and ② FTL method
> (Warp | hyperspace | jump | none). The per-family **dial sets** in this table are still correct — only the
> single-axis framing was wrong.

The sim reads **different things per family**, so the doors do not share one slider wall:

| Family | Its dials | State |
|---|---|---|
| **Reaction** | drive size · **push ↔ economy** · *signature suppression* | first two exist; third proposed (§23.2) |
| **Traction** | **speed factor** · **rough handling** · ~~amphibious~~ | 🔵 first two editable **and read**. ⚠ **amphibious is read by NOTHING — §23.3b** |
| **Warp** | **max speed** · **efficiency vs power** · **startup ↔ endurance** | 🔵 first two already editable — I dropped them by over-collapsing. **Third ADDED 2026-07-29** (§26b). |
| ~~**Fluid**~~ | — | ❌ **CUT — §23.3c.** No component, no variable. |

**`RoughHandling` is worth its own note:** it is **one dial feeding two systems** — march time (`TerrainMult`) *and* a
combat multiplier (`LocomotionTerrainMult`).

### 23.3a 🔑 THE GROUPING — one door for space thrust AND ground locomotion (developer, 2026-07-29)

> *"Don't we have locomotion in another door? Why not just group everything?"*

**Yes — and the design already says group them. The code never did.**

`docs/economy/COMPONENT-DESIGNER-CATEGORIES.md:36` already lists **ground-locomotion under Propulsion**, and `:60`
already rules the parallel ground `*Atb`s are *"absorbed into the universal doors … resolved by deletion not merger."*
**But the engine carries two unrelated implementations of one verb:**

| | Space | Ground |
|---|---|---|
| Class | `Movement/NewtonMove/NewtonionThrustAtb.cs` | `GroundCombat/GroundLocomotionAtb.cs` |
| Fields | `ExhaustVelocity` · `FuelType` · `FuelBurnRate` | `SpeedFactor` · `RoughHandling` · `Amphibious` |
| **Fields in common** | 🔴 **ZERO** | 🔴 **ZERO** |

Two parameterisations of *"how does this thing move,"* sharing nothing. **That is exactly what `CLAUDE.md`'s
One Verb, Both Seats forbids** — and this derivation half-did it: making "the ground" a *family* of the one door **is**
the grouping, but §23.3 never said what grouping **COSTS**: **deleting `GroundLocomotionAtb`** and giving Traction the
same shape as Reaction.

**And they CAN share a shape — every family is ONE POWER BUDGET SPENT TWO WAYS:**

| Family | The budget split | Law |
|---|---|---|
| **Reaction** | **thrust** ↔ **exhaust velocity** | `T · v = 2P` (§20) |
| **Traction** | **speed** ↔ **tractive effort** | 🔑 **the gearbox trade** — tall gearing is fast on roads, low gearing crawls over rough. **`SpeedFactor` and `RoughHandling` are the SAME zero-sum split**, merely authored as two independent dials. |
| **Warp** | **top speed** ↔ **bubble cost** | `WarpPower` vs `BubbleCreationCost` — **already a split** |

> 🔑 **So all three are `how much drive you fit` × `how you spend it`. The same two dials; the MEDIUM changes what
> they BUY.** That is the grouping — *and it is also the answer to "the choice needs to be more evident."* Picking a
> family does not relabel a tooltip; it **re-purposes both sliders and every readout.**

**What it costs to land:** `GroundLocomotionAtb` is deleted, its three fields become (a) the shared size dial,
(b) the shared split dial, (c) a cut dial (§23.3b). `GroundMobility.RoughHandlingForUnit` /
`GroundTerrain.LocomotionTerrainMult` / `GroundMobility.TerrainMult` keep reading a rough-handling number — it just
comes from the shared split instead of its own field. **Save-load risk: L3** (renaming/removing a serialized type).

### 23.3b 🔴 CORRECTION — `Amphibious` is a dial that COSTS MASS and BUYS NOTHING

§23.3 said Traction's three dials are *"all three already editable and read."* **That was wrong about `Amphibious`.**
Every mention in the repo, grepped:

| Site | What it does |
|---|---|
| `GroundLocomotionAtb.cs:32,40,43,51` | the field, ctor, copy-ctor, description |
| `installations.json:3460,3472` | a real designer dial + `AtbConstrArgs` |
| `installations.json:3424` | 🔴 **the Mass formula MULTIPLIES BY IT — you are charged for it** |
| `HexPathfinder.cs:39` | a **comment** saying amphibious gating *"is a cradle-to-grave follow-on"* |
| **anything that READS it** | 🔴 **NOTHING** |

`HexPathfinder.IsImpassable` hard-codes **ocean impassable to every ground unit**, amphibious or not. So the dial
**takes mass and returns no capability** — the clearest §1-law violation found in any category so far, and it was
hiding behind this document's own summary. **Either wire it (`IsImpassable` becomes unit-aware) or cut it; do not
ship a knob that charges for nothing.**

### 23.3c 🔴 CORRECTION — `Fluid` is NOT a family

Grepped for airbreathing · hover · buoyancy · fluid-drive · atmospheric-thrust: **zero components.** The only
propulsion `*Atb`s that exist are `NewtonionThrustAtb`, `ReactionlessThrustAtb`, `WarpDriveAtb`,
`GroundLocomotionAtb`. **Fluid is an authored door with no implementation and no variable.**

And physically it collapses like the rest: a propeller or a jet **is** reaction mass — you **scoop it instead of
carrying it**. So:

> **Fluid = Reaction, with the propellant sourced externally and a medium requirement added.**

The same shape as Reactionless (*"a rule removed"*) and Gravitic (*"a constraint lifted"*) in §24. **Three real
families remain: Reaction · Traction · Warp.**

### 23.4 CANDIDATES CHECKED AND REJECTED

| Candidate | Verdict |
|---|---|
| Thrust vectoring / gimbal arc | ❌ the resolver is **non-positional** — writes nothing |
| Spool-up · throttle response | ❌ the resolver is **not per-shot-timed** — writes nothing |
| Restart capability · reliability / MTBF | ❌ not modelled at all |
| Ground pressure / footprint | ❌ nothing beyond `RoughHandling` reads it |
| **Drive heat** | ⚠ **symmetric and missing.** Weapons feed a fleet `HeatPool_kJ`; propulsion feeds nothing. Writes no variable **today**, so it fails test 2 as-is — but the asymmetry is real and the wire is small. |
| Fuel type | ⚠ intrinsic, but under push↔economy it becomes a **cost/availability** axis rather than a performance one — **the open question in §26.** |

### 23.1 WHAT IS SET vs WHAT EMERGES — propulsion's honest split

| Set on the part | Emerges at assembly |
|---|---|
| what it pushes against | **acceleration** = thrust ÷ the finished ship's dry mass |
| drive size → thrust · exhaust velocity | **Δv** = exhaust velocity × ln(wet ÷ dry) — needs tanks |
| push ↔ economy | **evasion** = f(volume, acceleration) — needs the whole hull |
| ground: rough-terrain handling | **march speed** — needs the unit's total mass |

**Every number a player actually cares about in movement is in the right-hand column.** The drive contributes two of
the inputs; the entity decides the outcome. A designer that pretended otherwise would be lying about the physics.

## 24. THE EXOTIC DOOR DISSOLVES — the same collapse as Weapons

| Was | Actually |
|---|---|
| **Reactionless** | **Reaction with a rule removed** — implemented as exactly that: thrust set directly, propellant burn zero. A flag, not a family. |
| **Gravitic** | any family with the **medium requirement removed**. A constraint lifted. |
| **Inertialess** | 🔴 **not propulsion at all.** It writes one thing — an **evasion floor**. It produces no thrust and moves nothing. It is a **defensive** component sitting in Propulsion because it is about dodging. |
| **Teleport** | 🔴 **not propulsion either.** It breaks distance rather than crossing it — a transfer mechanic, belongs with logistics. |

## 25. WHAT GOES OUT — the Propulsion output map, EVERY ROW MARKED (verified in source)

> 🔒 **Marked 2026-07-30 on the developer's instruction — *"mark all outputs because these really change how the game
> can be played."*** Every row now carries **① a play state** (is this true in a game today?) and **② the sentence that
> says what it changes about how the game is PLAYED.** An output map that only names the call site is a wiring diagram;
> this is the blast radius in gameplay terms, which is what a reader actually needs before touching the door.
>
> **Play-state legend — three states, and the middle ones are the honest ones:**
> `✅ LIVE` the game does this today · `🟢 NEW` this derivation wired it and it ships now ·
> `🔵 HALF` the number exists, nothing reads it · `🔴 PROPOSED` designed and costed, **not built**.

| # | What leaves the door | Goes to | Play state | **What it changes about how the game is PLAYED** |
|---|---|---|---|---|
| 1 | **Thrust ÷ mass** | `CalculateEvasion` → `ShipCombatValueDB.Evasion` | ✅ LIVE | 🔴 **The largest defensive purchase in the game is made at this door.** Evasion is an effective-health **multiplier** to ×20 (§1b) against armour's ×10 — an engine decision outweighs any plate decision, in a currency the Defense door never sees. Neither UI says so. |
| 2 | **Thrust** | `FleetManeuver` | ✅ LIVE | **Who dictates the range in a closing fight.** The faster fleet chooses whether the battle happens at beam range or missile range — i.e. it chooses which corner of the weapon triangle it fights in. |
| 3 | **Thrust, as signature** | `SensorSignatureAtb(3500 K, mag = Thrust)` → Detection | ✅ LIVE | **Pushing hard sells your position.** The same dial that buys evasion buys being seen from farther — and `FirstStrike_SeerWipesBlindEnemy_Unscathed` shows the seeing side wipes an equal *blind* fleet **taking zero losses**. Biggest cost in the game; the designer never mentions it. |
| 4 | **Δv** | `FleetCombat.DeltaVFloor` → `ManeuverBudget` | ✅ LIVE | **The kiting clock.** Run dry mid-fight and the enemy closes at will — the manoeuvre advantage in row 2 has a fuel-shaped expiry date. |
| 5 | **`FuelGrade` × drive size** | `NewtonionThrustAtb.FuelBurnRate` → thrust | 🟢 NEW | **Fuel choice became a decision instead of a ladder.** RP-1 buys push, hydrolox buys range, NTP is a reactor paid for in fissionables. Before this a better fuel won on *both* axes, so there was nothing to choose. Gauge: `FuelGradeTests`. |
| 6 | **Warp max speed** | `WarpMath.MaxSpeedCalc` → `WarpSpeedFloor` | ✅ LIVE | **Strategic tempo, and it is a FLEET number** — the group moves at its slowest drive, so one frugal ship sets everyone's pace. Refitting one hull changes the whole fleet's reach. |
| 7 | **Bubble creation cost** | `WarpMoveCommand` (departure gate) | ✅ LIVE | **A flat battery is a ship that cannot leave.** Power generation and FTL are one decision — and it is what makes the §26g lane finding bite: a lane drive has no creation lump, so **it can always set off.** |
| 8 | **Bubble sustain cost** | `WarpMoveProcessor` → Power, per second | ✅ LIVE | **Transit is a running bill, not a one-off.** A long crossing can strand a ship that could afford to leave. |
| 9 | **Bubble sustain, as signature** | `SensorSignatureAtb` on the warp drive | 🟢 NEW | **FTL stopped being silent.** A ship crossing a system at FTL used to be exactly as detectable as one parked — against a genre where warp trails are a staple. Now the endurance drive buys range *and* stealth on one slider. Gauge: `WarpBubbleTradeTests`. |
| 10 | **`Startup vs Endurance`** | creation × sustain (invariant) | 🟢 NEW | **A real fleet-composition choice.** Short-hop couriers that leave on a part charge, or long-haul hulls that need a full one then cross quietly. Both ends buildable; `alcubierre-2k-endurance` ships. |
| 11 | **Ground speed factor** | `Speed_kmh` → the closing march | ✅ LIVE | **How fast you cross a battle's real metres** — which decides whether your artillery gets its standoff or your line arrives first (`GroundRoleManeuver`). |
| 12 | **Rough-ground handling** | march time **and** `LocomotionTerrainMult` | ✅ LIVE | **One dial, two systems** — how long you take to get there *and* how well you fight when you arrive. Road-geared armour is out-fought the moment it leaves the road. |
| 13 | **`Amphibious`** | `HexPathfinder.IsImpassable` (per-unit) | 🟢 NEW | **Where you may go at all.** Ocean used to be impassable to everyone while the dial charged mass for nothing (§23.3b). It now buys **access, not speed** (water costs 4.0 a hex, worse than a mountain) and is **lost with the drive below half health** — the grave rung. Gauge: `GroundForcesTests.HexPath_Amphibious_CrossesWater_ButPaysForIt`. |
| 14 | **Locomotion mode** (named picker) | the assembler UI | 🟢 NEW | **A choice the player can finally READ** — it was a raw integer showing *"Locomotion: 1"*. ⚠ Still 🔵 HALF downstream: `SpeedMultForUnit` lets a mounted drive's factor *override* the frame mode, so the four modes are dead weight on a properly-designed unit. **Open ruling #2.** |
| 15 | **Fuel burn rate** | **Logistics** — tankers, refuelling | ✅ LIVE | **A thirsty fleet is a supply problem, not a stat.** Deep operations need tankers, which need escorts, which need fuel. |
| 16 | **Drive mass** | **Chassis** budget — *and back into its own acceleration* | ✅ LIVE | **The only door whose output fights its own input.** A bigger engine is heavier, and the weight it adds eats the acceleration it bought. There is a genuine optimum and no readout shows it. |
| 17 | **The drive destroyed** | Damage → `ReCalcAbilities` | ✅ LIVE | **Shoot the engine off and evasion collapses** — a crippled ship stops dodging, the fastest way to turn a stalemate into a rout. Cradle-to-grave, both ways. |
| 18 | **Signature suppression** | `P_drive = P_total × (1 − suppression)` | 🔴 PROPOSED | **Would make stealth a purchase instead of a freebie.** Detection range grows as **√signature** today, so doubling thrust costs only 1.41× visibility — loud is nearly free. The tax pays twice (push *and* economy) plus shroud mass. §23.2a. |
| 19 | **Drive heat** | *nothing* — weapons feed a fleet heat pool, propulsion feeds none | 🔵 HALF | **An asymmetry the player can feel but not see.** Firing heats you; running does not. The wire is small. **Open ruling #4.** |
| 20 | **Route = fixed** (lane) | would gate `WarpMoveCommand` destinations via `JumpRouter` | 🔴 PROPOSED | **The cheapest new capability in the whole derivation** (§26e.4) — and it makes the map a chokepoint game: a rival holding a node holds you. `JumpRouter.FindRoute` already walks the discovered gate graph for the AI. |
| 21 | **Transit = instant** (jump) | would need a jump order + a range limit + a cooldown | 🔴 PROPOSED | **Uninterceptable movement** — the strongest defensive property in the FTL list: you cannot be caught in transit at all. §26e. |
| 22 | **Seen = hidden** (veiled) | would need `SensorScan` **and** the battle trigger to skip a ship in transit | 🔴 PROPOSED | **Untouchable crossings** — with the nuance the demo surfaced: hidden hides the *crossing*, never the *departure*. Largest blast radius of the four. §26g. |
| 23 | **Navigator-gated** | would need a seated `CommandBerthAtb` | 🔴 PROPOSED | **An FTL you can lose to a sniper.** Cheapest reach in the game, dead without a living specialist aboard. The seat already exists. §26e.3. |
| 24 | **No FTL drive at all** | blocked by `JumpOrder:63` · `MoveToNearestAction:102` · `MoveToSystemBodyOrder:70,81` | 🔵 HALF | 🔴 **You cannot currently build a ship with no FTL drive** — so the gate-only civilisation is impossible, and a ship whose warp drive is shot off *should* be stranded but the rules never let it get there. A cradle-to-grave hole. **Open ruling #3.** §26d.4. |

**Reading the marks:** 12 rows are `✅ LIVE`, **6 are `🟢 NEW` from this derivation**, 3 are `🔵 HALF` (the number exists,
nothing reads it — each one an open ruling), and 5 are `🔴 PROPOSED`. **Nine of the twenty-four are gameplay-changing
and not yet true** — which is exactly the list to work from, and the reason the marks matter more than the wiring.

### 25.1 🔑 THE LOOP THIS CLOSES ACROSS THREE DERIVATIONS

Defense (§16) found that **evasion — the layer that stops damage from ever being rolled — is not sold at the Defense
door.** This is where it is sold.

> **Propulsion is the game's largest defensive purchase, and nothing in either category says so.**

That is exactly the class of connection step 6 exists to find, and it took three derivations meeting before it was
visible.

**§1b now puts a NUMBER on it.** Evasion is an effective-health **multiplier**, capped at **×20** point-blank and
**×50** at range, against armour's **×10**. So the sentence above is not a figure of speech — the Propulsion door
sells a defence **2×–5× larger than anything the Defense door can**, in a currency the Defense door never sees.

## 26. 🔒 DECIDED — fuel type is a MULTIPLIER ON THE BUDGET (developer, 2026-07-29)

> *"Do fuel type as a multiplier for everything. We just need to ensure that a refinery can make the different
> variations of fuel."*

**Was open** (dropdown-of-unlocked-fuels vs a position on the push↔economy slider). **Now closed as a third answer,
and a better one:** fuel type is neither a door nor a slider position — **it multiplies the POWER BUDGET the split
slider then divides.**

### 26.1 The shape

Today the fuel is an **additive lookup on exhaust velocity only** (`engines.json:44`):

```
Exhaust Velocity = ExhaustVelocityLookup(Fuel Type) + TechData('tech-conventional-engine-exhaust-velocity')
```

Under the ruling it becomes a coefficient on the whole budget, which composes cleanly with §23.3a's grouping:

```
P_total = (drive size) × FuelGrade
T · v   = 2 · P_total          ← the split slider still decides HOW you spend it
```

🔑 **Why this is the right answer and not just a different one:** a multiplier on the budget works for **every**
family, which is exactly what *"a multiplier for everything"* asks for. Reaction burns fuel, Traction burns
fuel/power/muscle, Warp burns stored power — a **grade** coefficient applies to any family that burns something,
where an exhaust-velocity lookup only ever meant anything to Reaction.

### 26.2 ✅ THE REFINERY RUNG ALREADY EXISTS — verified

The developer's condition (*"ensure that a refinery can make the different variations"*) **is already met.** All four
fuels are refinery recipes today (`materials.json:25-115`), every one `"IndustryTypeID": "refining"`:

| Fuel | Inputs | Ind. pts | Out | Credits | Exhaust vel | Volume/unit | `FuelType` class |
|---|---|---|---|---|---|---|---|
| **RP-1** | hydrocarbons 1 | 10 | 2 | 20 | 3510 | 0.000945 | conventional |
| **Methalox** | hydrocarbons 1 | 10 | 2 | 20 | **3615** | **0.000836** | conventional |
| **Hydrolox** | hydrocarbons 1 | 10 | 2 | 25 | 4462 | 0.002778 | conventional |
| **NTP** | hydrocarbons 100 **+ fissionables 1** | 100 | 100 | **2500** | **7000** | 0.002778 | **ntr** |

So the cradle-to-grave chain is **whole**: mineral (hydrocarbons / fissionables) → refinery → fuel material →
burned by a drive. Nothing to build; this rung was already there.

### 26.3 🔴 BUT A PURE MULTIPLIER IS A DOMINANCE LADDER — the counter-axis is already in the data

**If a better fuel is better at everything, "use the best fuel you've researched" is not a decision** — it is the same
disease as *"a bigger engine is strictly better"* (§19). A grade multiplier **needs a counter-axis**, and the table
above already has one: **`VolumePerUnit` — density.** Tanks are finite in VOLUME, so:

| Fuel | Units per m³ of tank | Reading |
|---|---|---|
| Methalox | **1,197** | the density king |
| RP-1 | 1,059 | — |
| Hydrolox | 360 | **23% more exhaust velocity, 3.3× bulkier** |
| NTP | 360 | 94% more exhaust velocity, bulky, and 125× the credits |

**⇒ The trade is performance × density × cost**, and it is real: Hydrolox gives more Δv per kg but you fit far less
of it in the same tank. That is a genuine choice the player can feel.

**🔴 One dead option found on the way:** **RP-1 is strictly dominated by Methalox** — identical inputs, identical
industry points, identical output and credits, but Methalox has **higher** exhaust velocity **and** better density.
There is no reason to ever refine RP-1. Either give it an edge (cheaper inputs, or an earlier tech unlock) or cut it.
Same §1-law failure as a dead dial, one level up: **a dead RECIPE.**

## 26a. 🔒 DECIDED — KEEP `Amphibious`, which means WIRING it (developer, 2026-07-29)

> *"Keep amphibious."*

**Kept. And keeping it means it cannot stay as it is** (§23.3b: it charges mass and is read by nothing). The wire is
small and well-bounded:

| Change | File | Note |
|---|---|---|
| `IsImpassable(terrain)` → **`IsImpassable(terrain, unit)`** — ocean is impassable *unless the unit is amphibious* | `GroundCombat/HexPathfinder.cs:41` | today it hard-codes `terrain == Ocean` for everyone |
| An amphibious unit crossing water pays a **rough** move cost, not a free one | `HexPathfinder.HexMoveMult` | water should be slow, not free — or amphibious becomes strictly better |
| Read the flag off the unit's locomotion, health-scaled | `GroundMobility` (beside `RoughHandlingForUnit`) | the **grave rung** — a shot-off drive should strand you |

**Blast radius (Prime Directive):** an impassable hex is **left out of the pathfinding graph entirely** (the comment
at `HexPathfinder.cs:41` says so), so making passability unit-dependent means the graph is **per-unit**, not global —
that is the one non-trivial part of this change and it must be checked before it is written. **Gauge:** an amphibious
unit paths across a water hex; a non-amphibious one still routes around it; a destination on water is reachable only
for the amphibious one.

**Not built this pass** — it is an engine change to pathfinding, and the working agreement is one slice at a time
with CI as the only compile gauge.

---

## 26b. ✅ BUILT — the WARP dial, and the GROUND labels (2026-07-29)

Two cheap additions found by asking *"what does the sim already read that has no dial?"*

### 26b.1 Warp — `Startup vs Endurance`

Warp had **one** performance dial (`Efficency vs Power`) plus Mass. Bubble **creation** and bubble **sustain** were
both computed from Engine Power with no choice — yet **both are live**:

| | Read by | When it bites |
|---|---|---|
| `BubbleCreationCost` | `WarpMoveCommand` | **gates departure** — a ship refuses to leave below it |
| `BubbleSustainCost` | `WarpMoveProcessor` `AddDemand(...)` | **charged per second in transit** |

**The dial multiplies creation and DIVIDES sustain, so their product is invariant** — zero-sum, the same shape as
the Reaction door's `T·v = 2P`:

| Setting | Creation | Sustain | The build |
|---|---|---|---|
| **0.4** | ×0.4 | ×2.5 | cheap to start, dear to hold — **short hops**, and it can leave on a part-charged battery |
| **1.0** *(default)* | ×1 | ×1 | today's numbers exactly — **byte-identical** |
| **2.5** | ×2.5 | ×0.4 | dear to start, cheap to hold — **one long haul** |

Range `[0.4, 2.5]` is **reciprocal-symmetric** around 1.0, so the two ends are equal and opposite.
**Cradle-to-grave:** the buildable `default-design-alcubierre-2k-endurance` (same mass as the 2k, dial at 2.0).
Gauge `WarpBubbleTradeTests` — asserts the product is invariant, the ratios are exactly the dial, and that the
undialled creation:sustain ratio is unchanged and power-independent.

### 26b.2 Ground — the mode dial was already there, just unreadable

`human-frame` / `vehicle-frame` / `walker-frame` / `swarm-frame` each exposed **`Locomotion` and `CarryClass` as raw
integers** (`GuiSelectionMaxMinInt`), so the designer showed *"Locomotion: 1"* — with the meaning shoved into a
description reading `'0=Foot 1=Tracked 2=Walker 3=Hover.'`. Someone already knew it was unreadable.

Both are now **`GuiEnumSelectionList`** bound to the real enums (`Pulsar4X.GroundCombat.GroundLocomotion` /
`GroundCarryClass`), so the player picks **Foot · Tracked · Walker · Hover** by name.

⚠ **The trap, for anyone adding another enum dial:** the renderer builds
`length = maxValue - minValue` entries (`ComponentDesignDisplay.GuiHintEnumSelection`), so **`MaxFormula` must be the
COUNT, not the top index** — `3` would have silently cut **Hover** off the list. Set to `4` and `2`.
`PropertyFormula` is untouched on every frame, so each still binds the same int → **byte-identical**.

---

## 26c. FTL, SURVEYED AGAINST SCI-FI — three axes, and the one thing that was cheap

The developer's ask: *"if you're doing spacetime (ie ftl) you should probably spend a moment or 2 thinking about all
the other things that go into it… just give a glance at sci fi and see what you can get and simplify and apply."*

**Every famous FTL collapses onto THREE yes/no axes.** No fourth axis was needed to place any of them:

| | Route | Transit | Reachable in transit |
|---|---|---|---|
| **Star Trek** warp | free — go anywhere | continuous | **yes** — normal space, trackable, interceptable |
| **Star Wars** hyperspace | free | continuous | **no** — you are elsewhere |
| **BSG** jump drive | free | **instant** | n/a |
| **Stellaris** hyperlanes | **fixed** — a graph | continuous | yes |
| **B5 gates · Stargate · Mass Effect relays · Aurora jump points** | **fixed** — a node you must reach | **instant** | n/a |
| **Dune fold · Andromeda slipstream** | free | instant-ish | n/a — but gated on a **navigator** (a worker, not a drive) |

### What Pulsar already has — two of the boxes, and they are the two most-used

| | Route | Transit | Reachable | Notes |
|---|---|---|---|---|
| **Alcubierre warp** | free | continuous | **yes** — a warping ship keeps a real `PositionDB` + vector | = Star Trek warp |
| **Jump points** | **fixed** (`JumpPointDB.DestinationId`) | instant transit; you must **sail to the node** | n/a | + a **discovery** layer — `IsDiscovered` per faction via grav survey, and `IsStabilized`. = the Aurora / Mass-Effect model |

**That is a genuinely good spread already**, and the jump-point *discovery* layer is the part most 4X games skip.

### What is missing, and what each would actually cost

| Missing | What it would take | Verdict |
|---|---|---|
| **Unreachable transit (hyperspace)** — the only truly absent axis | the sensor scan and the combat trigger must both skip a ship in transit | 🔴 **not simple** — real blast radius into detection *and* combat. A design decision, not a dial. |
| **Instant free jump (BSG)** | a new order + a per-jump range limit + a cooldown | ⚠ medium. A whole movement mode. |
| **Navigator-gated FTL (Dune)** | this is the **Site Engine's Command Berth** pattern pointed at a drive | ⚠ a system, not a dial — but it already has a home in the design |
| **Spool-up time (BSG)** | — | ✅ **already emergent.** Creation cost gates departure, so a high-creation drive on weak generation *is* a long spool. Nothing to build. |

### 26c.1 ✅ The one that WAS missing and WAS cheap: FTL made no noise

**A warp drive emitted nothing.** Grepped every engine template: `conventional-engine` and `scntr-engine` both carry a
`SensorSignatureAtb`; **`alcubierre-warp-drive`, `inertialess-drive` and `reactionless-drive` carry none.** So a ship
crossing a system at FTL was **exactly as detectable as one sitting still** — which contradicts essentially every
setting in the table above (warp signatures, hyperspace wakes, the jump-point flash are staples).

**Fixed with one JSON property**, and it makes the §26b dial trade **three ways** instead of two, exactly mirroring the
Reaction door's §23.2:

```
Sensor Signature → AtbConstrArgs(3500, PropertyValue('Bubble Sustain Energy Cost') * 1000)
```

Magnitude is the power you are **continuously pouring into holding the bubble** — which is what sustain *is* — so it
divides by the dial:

| Dial | Magnitude | Seen from |
|---|---|---|
| **0.4** short-hop | 5,000,000 | **3.2×** a small chemical engine's range — loud |
| **1.0** default | 2,000,000 | 2.0× |
| **2.5** long-haul | 800,000 | **1.3×** — the quiet one |

🔑 **So the endurance drive buys range AND stealth with the same slider, and pays for both at the departure gate.**

**⚠ The band is deliberately 3500 K — the same as a thruster plume — and that is a real decision, not laziness.** The
"gravimetric signature" answer (a long-wavelength band needing a dedicated sensor) is the better sci-fi, and the scan
*does* band-match (`SensorTools.DetectonQuality` penalises off-band). **But the only base-mod receiver is tuned to
600 nm ± 250**, so a 300 K warp signature at ~9,660 nm would be **invisible to every sensor in the game** — a stealth
exploit, not a feature. Putting FTL in its own band is a **two-part** change: the signature *and* a receiver that can
see it. Flagged, not done. The gauge pins the band so nobody moves it by halves.

---

## 26d. 🔒 CORRECTION — "SPACETIME" WAS A CATEGORY ERROR. The Propulsion door asks TWO questions (developer, 2026-07-30)

> *"the ftl is the ftl method like what we discussed earlier — the warp, hyperspace, etc. Survey is for sensors. And
> does the spacetime actually make sense?"*

**No, it does not, and the developer caught a real error.** §23.3 had the door as *one* question — "what do you push
against" — with four exclusive answers: **Reaction mass · A surface · Spacetime · FTL network.** Two of those four
are not alternatives to the other two.

### 26d.1 The test a door must pass, and how it failed

**A door's answers must be MUTUALLY EXCLUSIVE.** That is the whole reason a door is a door and not four sliders. Ran
that test against the base mod's own ships:

| Base-mod ship design | sublight thrusters mounted | warp drives mounted |
|---|---|---|
| Sanctum Adroit Gunship | 2 | 1 |
| Ob'enn Dropship | 2 | 1 |
| Freighter · Cargo Courier · Surveyor · Lander Troop Transport · Target Drone | 1 | 1 |
| Starship | 1 | 0 |
| Sensor Sat | 0 | 0 |

**Every warship carries BOTH.** "Reaction mass" and "spacetime" were never rivals — a ship needs a thruster to
manoeuvre *and* a warp drive to leave. A door whose options can all be true at once is **two doors wearing one label.**

### 26d.2 The fix — name what each one asks

| # | The question | Answers | Exclusive? |
|---|---|---|---|
| ① | **Sublight** — how do you move *inside* a system? | **Reaction mass** · **A surface** | ✅ yes — nothing both flies on rockets and walks |
| ② | **FTL method** — how do you cross *between* systems? | **Warp** (built) · hyperspace · jump drive · nothing-but-gates | ✅ yes among themselves, and **optional** |

The word **"spacetime" is cut.** Warp survives as **one FTL method** — which is exactly the frame §26c already
surveyed the genre with (route × transit × seen). The three-axis matrix was right; the *door label above it* was wrong.

**Note the intrinsic test (§1a) is what makes the picker still exclusive:** you are designing **one part**. A part is
a thruster or a warp drive, never both. The *ship* is where the two questions add up — which is an **Entity Assembler**
concern, not a designer one. Same boundary, applied one level up: it separates doors, not just dials.

### 26d.3 ➡ The grav surveyor LEAVES this door for Sensors (developer's call, same message)

> *"Survey is for sensors."*

**Agreed and applied.** §26c had the fourth family as "FTL network", whose entire component surface was
`GravSurveyAtb` (one dial, Survey Speed, mass `(10 × speed)²`). **Finding a jump point is not moving through one.**
It is a detection instrument with a receiver, a speed, and a mass that grows quadratically with capability — the
same shape as every other sensor. It goes on the **Sensors ▸ Survey** door's list (§29 has Sensors next), and comes
off Propulsion's.

### 26d.4 🔴 What renaming it "method" then exposed — TWO findings

Once the question is *which method*, you can go and ask the code which methods it supports. **One, and it is
compulsory.**

| Finding | Evidence | Why it matters |
|---|---|---|
| **There is no way to build a ship with NO FTL drive** | Three movement paths gate on the blob: `JumpOrder.cs:63` (`if (!ship.HasDataBlob<WarpAbilityDB>()) continue;`), `MoveToNearestAction.cs:102`, `MoveToSystemBodyOrder.cs:70,81` | A hull with a thruster and no warp drive **cannot be ordered anywhere strategically — including through a gate that would do the moving for it.** That kills the gate-only civilisation (B5 · Stargate · Mass Effect — the *cheapest* FTL flavour, since the map does the work). It is also a **Cradle-to-Grave hole**: by these three checks a ship whose warp drive is shot off should be *stranded*, which is a far more interesting loss than the game currently expresses |
| **You transit a jump point USING your warp drive** | grepped for a jump-drive component: none exists. No charge time, no jump range, no cooldown, no per-jump cost | A gate transit is **free and instant** if you happen to own a warp drive. So "Jump drive" is genuinely unbuilt — not a dial gap, a **whole movement mode** (§26c already costed it ⚠ medium) |

Plus the third piece, unchanged from §26c: `JumpPointDB.IsStabilized` is read in exactly one place and the branch
behind it is `// TODO: Introduce a random chance to stablize jumppoints.` — **a dead capability**, set once at galaxy
generation from a game setting, changeable by nothing in play. `IsDiscovered` (the per-faction discovery set) is the
genuinely good part and stays.

### 26d.5 Open for the developer — added to the door's list

- **Whether a ship may legally carry no FTL drive.** Three one-line gates to relax; the payoff is that losing a warp
  drive means something. Blast radius: fleet movement orders + the AI's `JumpRouter`/`MilitaryReach` reach maths.
- **Whether jump-point stabilisation becomes a real spend.** The obvious home for a player decision; the code for it
  is a comment.

*(Carried unchanged: the locomotion-mode override, the drive-heat feed, untouchable transit, FTL's own sensor band,
the fate of `GroundLocomotionAtb`.)*

---

## 26e. 🔒 DECIDED — ONE FTL TAB, three settings, and the boxes come out exactly saturated (developer, 2026-07-30)

> *"There should just be one FTL tab with options and sliders that make sense to make these: [the six-row
> route/transit/seen table]"*

**Applied, and the derivation is stronger than the request.** §26d had FTL as a *list of methods* (warp | hyperspace |
jump | gate) — four exclusive chips. The developer's call: **it is one part, and the methods are settings on it.**
Run the standard method (§1) on the six rows and the settings fall straight out of the table's own columns.

### 26e.1 The settings ARE the taxonomy — 2 choices and a CONDITIONAL third

| # | Setting | Options | Present when | Writes |
|---|---|---|---|---|
| ① | **Route** | Free (anywhere) · Fixed (node to node) | always | whether the move order accepts an arbitrary destination or requires a discovered jump point |
| ② | **Transit** | Continuous (you cross it) · Instant (you arrive) | always | whether there is a per-second sustain charge and an interceptable in-flight state at all |
| ③ | **Can you be found in transit** | Trackable · Hidden | **only when transit is continuous** | whether `SensorScan` and the battle trigger see a ship mid-crossing |
| ④ | **Navigator-gated** | flag | always | a Command Berth requirement (`CommandBerthAtb` — Role · Grade · Support · Survivability · Span) |

🔑 **③ is conditional, and the developer's own table is the proof.** Three of the six rows read **n/a** in the *seen*
column — because **an instant transit has no transit to be observed during.** That is a real dependency in the data,
not a gap in the survey. So it is **two settings and a conditional third, not three independent ones.**

### 26e.2 🔑 COUNT THE BOXES — 2 × (2 + 1) = 6, and the developer listed exactly 6

| Box | Route | Transit | Seen | Who lives there | Build state (grepped, not guessed) |
|---|---|---|---|---|---|
| 1 | free | continuous | trackable | **Star Trek warp** | ✅ **ships today — this is Pulsar's Alcubierre drive, byte for byte** |
| 2 | free | continuous | **hidden** | **Star Wars hyperspace** | 🔴 the sensor scan **and** the battle trigger must both skip a ship in transit |
| 3 | free | **instant** | — | **BSG jump drive · Dune fold · Andromeda slipstream** | ⚠ a whole movement mode: a jump order, a per-jump range limit, a cooldown |
| 4 | **fixed** | continuous | trackable | **Stellaris hyperlanes** | ✅✅ **the cheapest unbuilt box in the derivation** — see §26e.4 |
| 5 | **fixed** | continuous | **hidden** | 🔓 **nobody** | 🔓 **an empty box the derivation predicts** |
| 6 | **fixed** | **instant** | — | **B5 gates · Stargate · ME relays · Aurora jump points** | ⚠ the network ships (gates + discovery + router); **the drive does not** (§26d.4) |

**Exactly saturated: six settings-combinations, six rows.** Which means two things fall out for free —

### 26e.3 ① THE NAVIGATOR IS A REAL FOURTH AXIS — the developer's own table proves it

**Box 3 holds three franchises, and BSG is not the same thing as Dune.** What separates them is not route, transit or
visibility — it is that a Guild fold and a slipstream jump are **gated on a person.** Without that axis the taxonomy
calls them identical. So *"gated on a navigator — a worker, not a drive"* is **not a footnote in the survey, it is the
setting that distinguishes the box's occupants.**

It is a flag on the tab, and the seat already exists: **`CommandBerthAtb`** (`GameEngine/Sites/`) with
Role · Grade · Support · Survivability · Span. The trade: a navigator-gated drive gets **the cheapest reach in the
game** (the person does work the machine would) and is **dead without a living specialist aboard** — the only FTL you
can lose to a sniper. That is a genuine grave rung, and it lands on the Site Engine's existing posting-danger roll.

### 26e.4 ② BOX 4 IS THE CHEAPEST NEW CAPABILITY IN THIS WHOLE DERIVATION — and I did not expect it

**Stellaris-style lanes need almost no new code:**

- **`JumpRouter.FindRoute`** (`GameEngine/Factions/JumpRouter.cs`) is a **live** breadth-first walk over the
  **discovered** jump-point graph. Not a stub — the AI's `MilitaryReach` (`:113,131,143`) and `ConquerResolver`
  (`:209`) call it every cycle to plan multi-gate strikes.
- **Warp already does continuous transit**, with a real speed, a departure gate and a per-second charge.

**So box 4 = forbid off-lane destinations and route the legs through the router that already exists.** That is a
**rule**, not a system — and it is the only unbuilt box in the table with that property. Cheapest FTL flavour
available, and it is a *strategically* different game (a rival holding the node holds you).

### 26e.5 ③ BOX 5 IS EMPTY — which is the point of a saturated grid

**Fixed lanes · a real crossing · untouchable while you make it.** No franchise on the developer's list claims it.
Whether it earns building is a developer call — but **the derivation found it rather than someone having to imagine
it.** That is what a complete taxonomy is *for*, and it is the second time the method has produced an unclaimed
design (the first was Weapons reaching 1,073 named designs from 2 choices + 4 sliders).

### 26e.6 THE SLIDERS SURVIVE THE SETTINGS — one set, re-named, never re-derived

This is what makes it **one tab** rather than four templates. All four sliders are the shipped `alcubierre-warp-drive`
dials or the already-proposed suppression tax; **none is invented.**

| Slider | Continuous transit | Instant transit | Source |
|---|---|---|---|
| **Drive mass** | `Engine Power = EvP × Mass × 1000` | same | ✅ shipped dial |
| **Reach vs power** | the `Efficency vs Power` dial, 0.4–2.0 | same | ✅ shipped dial |
| **Startup ↔ endurance** → **Range ↔ tempo** | creation gates departure · sustain charged per second | creation is the **spool** (→ range per jump) · sustain is the **recharge draw** | ✅ shipped (§26b) |
| **Quietness** | taxes the whole budget, shroud adds mass | same | 🔴 proposed (§23.2a) |

🔒 **The two numbers never change, only their names:** *"energy to start"* and *"continuous draw"* are the same pair
whether you are holding a bubble or charging a jump. And **the recharge time is derivable, not invented:**

```
recharge seconds = creation ÷ sustain = (power × 0.5 × SvE) ÷ (power × 0.001 ÷ SvE) ... no — the RATIO is
                 = 500 × SvE     ← POWER CANCELS, so it is a pure dial read, independent of tech and engine size.
```

That is the same power-independence the byte-identity gauge already pins
(`WarpBubbleTradeTests.TheDefault_IsByteIdentical_ForEveryExistingDrive` asserts creation:sustain is
power-independent for two different-sized drives).

**⚠ A CORRECTION I had to make to my own mapping, caught by simulating it.** I first wrote that *range ÷ recharge* is
the invariant in instant mode. **It is not** — range ∝ creation and recharge ∝ creation ÷ sustain, so range ÷ recharge
∝ sustain ∝ 1/SvE, which *moves* (188 → 30 AU/h across the slider). That would have made "short hops, often" strictly
dominant — a ladder, not a trade, exactly the §26.3 failure. **The real invariant is `range per jump × tempo`:**

```
range × tempo ∝ creation × sustain = CONSTANT      verified across the slider: 4.2×188.6 = 15.2×52.0 = 26.2×30.2
```

So the honest trade in instant mode is **strategic reach vs operational tempo** — one long bound and you cross a gap
nothing else can, but your overall AU-per-hour drops. Same shape as `T·v = 2P`. *(Gauge lesson: the mapping was wrong
in a way that reads plausible in prose and fails in one line of arithmetic. Simulate the dial before writing the
claim.)*

### 26e.7 WHAT THIS COLLAPSES

**Four separate FTL component templates → ONE.** Warp, hyperspace, jump and gate-tug would have been *four copies of
the same four sliders with a rule changed.* This is the same collapse Weapons needed (5 doors + 41 dial groups → 2
choices + 4 sliders) and it is worth stating as a general result of the method:

> 🔒 **When two candidate "families" share their whole slider set and differ only by a RULE, they are one part with a
> setting — not two parts.** The tell is that the sliders survive the switch.

### 26e.8 Open for the developer — the FTL list, re-ranked by cost

1. **Box 4 (Stellaris lanes)** — cheapest by a wide margin; `JumpRouter` + warp + one rule. **Recommended first.**
2. **Box 3 (instant transit)** — a whole movement mode, but the *spool* is already emergent (a high-creation drive on
   weak generation IS a long spool).
3. **The navigator flag** — the seat exists; the question is whether the grave rung is worth the wiring.
4. **Boxes 2 & 5 (hidden transit)** — the largest blast radius: the scan *and* the battle trigger must both skip a
   ship in transit. **Note the nuance the demo surfaced: hidden hides the CROSSING, never the departure** — you still
   spool up in normal space, so a hyperspace ship is loud at the gate and invisible en route.

---

## 26f. 🔒 DECIDED — PROPULSION NAMING (compositional, 848 names) and NO "NOT A DIAL" DIALS (developer, 2026-07-30)

> *"Get rid of the none dials and generalize the label and names of whatever can be created like you did for the
> weapons names."*

Two rulings in one sentence. Both applied.

### 26f.1 NO GREYED "NOT A DIAL" SLIDERS — a slider you cannot set is a caption, not a dial

The Propulsion builder carried two deliberately-greyed sliders under a *"Not dials — the entity decides these"*
heading: **Fuel carried** (it is tankage → Logistical) and **Hull it must move** (chosen at the assembler). They were
teaching aids for §1a, and the developer is right that they do not belong in a designer: **the door must contain only
things you can actually set.**

**Removed.** The numbers that genuinely need them — acceleration, Δv, evasion, cruise speed, jump range, all the
`emergent` rows — are now quoted **against a stated reference**, which is what a real parts catalogue does:

```
REF_HULL = 38,000 kg      a 38 t reference hull
REF_WET  = 2.0            a 2:1 wet/dry propellant load
```

🔒 **Generalise the rule for the remaining eight doors:** *an emergent number is shown against a stated reference,
never against a slider the player cannot really set.* The §1a intrinsic test decides **whether** a number is a dial;
this decides **how to display it when it is not.** Nothing is lost — the intrinsic-test section states the boundary in
prose, which is where the teaching belongs.

### 26f.2 NAMING — the same compositional scheme as Weapons (§7), extended to drives

**`[modifier?] + [scale] + [temper] + [core noun]`** — built from the dials, never looked up.

| Slot | Comes from | Values |
|---|---|---|
| **Modifier** (at most one, priority-ordered) | the special settings | `Veiled` (hidden transit) · `Helm-Linked` (navigator) · `Amphibious` · `Baffled` (heavily muffled) |
| **Scale** | drive mass | `Compact · Light · — · Heavy · Capital` |
| **Temper** | the zero-sum split | reaction `Hard · — · Long-Burn` · ground `Road · — · All-Terrain` · FTL `Sprint · — · Endurance` (continuous) or `Rapid · — · Deep-Reach` (instant) |
| **Core noun** | the identity choice | the propellant · how it meets the ground · **route × transit**, graded by the reach dial |

**Reads as:** Heavy Kerolox Booster · Compact Endurance Warp Coil · Amphibious Capital Strider Gantry ·
Veiled Lane Drive · Helm-Linked Jump Drive · Baffled Light Hydrolox Sustainer.

**Reachable distinct names — enumerated, not estimated:**

| Family | Names |
|---|---|
| Reaction mass | 120 |
| A surface | 98 |
| FTL drive | **630** |
| **The whole door** | **848** |

🔴 **Vocabulary rule, same as Weapons: generic hardware language ONLY, no franchise IP in any design name.**
Kerolox · methalox · hydrolox · nuclear · plenum · ground-effect · gantry · coil · coupler · aperture · threshold ·
strider. **The franchise names stay as ANALYSIS of the taxonomy and never become product names** — the badge now reads
*"You have just built a **Warp Drive** — the box Star Trek warp occupies"*, so the thing you build is called what it
is and the franchise is only the reference frame. Box 5 (unclaimed) reads *"a box no franchise on the list claims."*

### 26f.3 THREE GUARDS — and the third one corrected me

1. **No stutters** — word-level de-duplication. No "Heavy Heavy Track Drive."
2. **No contradictions** — the adjective is dropped when the noun already carries it: no "All-Terrain Strider Drive",
   no "Hard Booster", no "Long-Burn Sustainer" — **and no scale word twice**, which caught
   *"Capital **Heavy** Track Drive"* (the noun list had a scale word inside it; nouns may not contain one).
3. **A length cap** — 🔴 **and this is the finding worth carrying to the other doors.** I first set the cap at **four**
   words. It *fired*, and dropping a word **collapsed 848 reachable names down to 565** — two genuinely different
   drives came out with the same name, which defeats the point of naming them at all. Fixed the right way: **shorten
   the long core nouns** ("Tracked Running Gear" → "Tracked Gear", "Nuclear Thermal Motor" → "Nuclear Motor") so every
   noun is at most two words, the cap sits at **five**, and it **never fires.**

🔒 **The general rule:** *a name guard must never be able to merge two distinct designs. If a cap fires, shorten the
vocabulary — do not drop a slot.* A guard that changes the output is a bug in the vocabulary.

*(Verified by enumerating every reachable name through the real compose function: 848 distinct, max 5 words, zero
stutters, zero double-scale names, zero cross-family collisions. And the whole builder re-rendered across 2,268
dial/setting combinations with no throw, no `undefined` and no `NaN`.)*

---

## 26g. 🔒 DECIDED — THE SETTINGS CHANGE WHICH SLIDERS EXIST (developer, 2026-07-30)

> *"The sliders should change depending on what FTL system you use. Obviously make the sliders make sense and
> actually mean something."*

**The right correction, and it exposes something §26e only half-did.** §26e made the settings *re-label* the sliders
(startup↔endurance became range↔tempo). A setting that only renames a slider is **decoration**. The developer's call:
the settings must change the slider **set** — and the reason they must is the first test in the method itself
(§1: *which sim variable does this dial write? none and it costs nothing ⇒ a bug in the design*). **Under some
settings, some of these dials write nothing.** Leaving them on screen ships the exact bug the test exists to catch.

### 26g.1 The per-box slider set — derived, with a physical reason for every removal

| Box | Drive mass | The split — and what it splits | Reach vs power | Quietness | Sliders |
|---|---|---|---|---|---|
| **1** Warp · free·cont·seen | ✅ | **startup ↔ endurance** — make a bubble, hold a bubble | ✅ | ✅ | **4** |
| **2** Veiled warp · free·cont·hidden | ✅ | **spool ↔ endurance** — how long you sit exposed before you vanish | ✅ | 🔴 **DEAD** | **3** |
| **3** Jump · free·inst | ✅ | **range ↔ tempo** — one long bound, or many quick ones | ✅ | ✅ | **4** |
| **4** Lane · fixed·cont·seen | ✅ | **speed ↔ economy** — the rocket trade, on a rail | ✅ | ✅ | **4** |
| **5** Veiled lane · fixed·cont·hidden | ✅ | **speed ↔ economy** | ✅ | 🔴 **DEAD** | **3** |
| **6** Threshold · fixed·inst | ✅ | **cycle ↔ tonnage** — how much, how often | 🔴 **DEAD** | 🔴 **DEAD** | **2** |

**The three removals, each with its reason:**

1. **Quietness dies under hidden transit.** You are *not in normal space* during the crossing, so there is no
   signature to suppress. The only exposure left is the spool — and the split slider already decides how long that
   lasts. A quietness dial here would write nothing.
2. **🔑 The bubble split dies on a lane — and this is a real gameplay finding, not a UI tidy-up.** A lane *already
   exists*: nothing to spin up, nothing to hold. So the creation lump `WarpMoveCommand` uses to **gate departure**
   does not apply, and the panel loses its "energy to start" tile entirely. Which means: **a warp ship with a flat
   battery cannot leave; a lane ship can — it just crawls.** The budget splits the *rocket* way instead (push hard
   and burn a lot per AU, or sip and take longer).
3. **Three of four die on the gate coupler.** *Reach vs power* — there is no distance to cover, so power-per-kg buys
   nothing. *Quietness* — the transit is instant, and the loud thing is the gate, not you. *The bubble split* — no
   path to make, no duration to hold. What remains is the one trade a coupler **has**: tonnage per transit against
   cycle time, with throughput fixed by drive mass. **Two sliders is the honest size of a part whose only job is to
   couple** — which is the same conclusion §26d.4 reached from the code, arrived at independently from the physics.

### 26g.2 🔒 FOUR TRADES, ONE LAW — every box keeps its own zero-sum

Each split is a different pair, and in all six the product holds flat across the slider (verified in the demo):

| Box | The invariant |
|---|---|
| 1 · 2 | `start × draw` = const |
| 3 | `range per jump × tempo` = const |
| 4 · 5 | `speed ÷ energy-per-AU` = const |
| 6 | `tonnage × cycles-per-hour` = const |

**All four are the `T·v = 2P` shape** the reaction engine (thrust ↔ exhaust velocity) and the ground gearbox
(speed ↔ tractive effort) already run on. That is now **five** places one law does the work — worth stating as a
general expectation for the remaining doors: *when a door's budget is fixed, the split slider is that law wearing
local units.*

### 26g.3 🔒 THE GENERAL RULE for the remaining eight doors

> **A door's choices must change the slider SET, not just the slider LABELS.** If flipping a setting leaves the same
> dials with new names, either the setting is cosmetic or one of those dials is now writing nothing. Both are bugs.
> **And when a slider is hidden, its value must not keep acting on the numbers behind the player's back** — the demo
> forces `quietness = 0` and `reach = 1` in the boxes where those dials are gone, so a stale slider position can never
> silently change a result the player cannot see.

*(Verified: 2,268 dial/setting combinations re-rendered with no throw, no `undefined`, no `NaN`; slider counts measured
at 4/3/4/4/3/2; and each box's invariant confirmed flat across split 0 / 50 / 100.)*

---

## 26h. RE-CHECKING THE EARLIER DERIVATIONS AGAINST §1a

| Category | Dials | Verdict |
|---|---|---|
| **Weapons** | total damage · shot size↔rate · reach · focus | ✅ **all four intrinsic.** Each is true of the gun whatever it is bolted to. *(The authored spec's Guided "magazine size" would have failed — the derivation dropped it before the test existed.)* |
| **Defense** | shield capacity↔regen · plate thickness · zero-sum nature tuning | ✅ **all intrinsic.** A plate has a thickness and a tuning; a shield has a capacity and a regen rate. None needs the hull. |
| **Propulsion** | drive size · push↔economy | ✅ after cutting fuel load (§23) |

**No earlier derivation has to be reopened** — but the test is now applied first, every time.

---

## 27. RUNNING TALLY (updated)

| Category | State | Shape |
|---|---|---|
| **Weapons** | ✅ derived | 5 doors + 41 dial groups → **2 choices + 4 sliders** |
| **Defense** | ✅ derived | 4 doors → **1 choice + 3 sliders**, two doors relocated out |
| **Propulsion** | ✅ derived *(corrected)* | 5 doors → **1 choice, and the choice picks the dial set**; Exotic dissolves; **the fix here is to ADD dials, not remove doors** — headline: the push↔economy trade is **three-way** because signature already scales with thrust |
| Sensors · Power · Enhancers · Industrial · Logistical · Civic · Command · Chassis | ⏳ owed | **build plan in PART FOUR (§28–31)** — the per-door recipe, the order and why, the predictions, and what "done" means |

---

# PART FOUR — THE REMAINING EIGHT: a build plan (2026-07-29)

Not derivations — **a plan for producing them**, using the method §1 locks. Written so a cold session can pick up any
one door and run it without re-deriving the approach.

## 28. THE PER-DOOR RECIPE — run these seven steps, in order, every time

| # | Step | Done when |
|---|---|---|
| 1 | **Find the INPUT SURFACE in source.** Grep the door's `*Atb` classes; for each field ask *what reads this?* | You can cite `file:line` for every field, and every field is marked READ or **UNREAD** |
| 2 | **Group the fields by the QUESTION each answers.** Not by class — by what a player is deciding. | Every field sits under exactly one question |
| 3 | **Separate FORCED from FREE.** A question with mutually-exclusive answers is a **choice/door**; a question with a continuum is a **slider**. | Each question is labelled door or slider |
| 4 | **Apply the two tests to every candidate dial.** ① Which sim variable does it write? *(none + costs nothing ⇒ a bug)* ② **§1a the intrinsic test** — settable knowing only this part? | Each candidate is dial · assembly decision · emergent readout · **cut** |
| 5 | **Prove it reproduces what exists.** Every shipped component of that category must fall out of the new dials. | Every base-mod design in the category is reachable |
| 6 | **Name what goes OUT — the output map.** Where does each value land, and what breaks if it changes? | A table: what leaves → where it goes → why it matters |
| 7 | **Land the cheap wins found on the way**, each as its own gauged slice. | Each shipped with a test that cannot rot |

**The two standing exit criteria:** a door is not derived until it has **both** an input surface *and* an output map
(§1). And every surviving dial passes **both** tests in step 4.

**What the four completed doors predict:** the biggest findings came from categories with **lots of authored surface
and little wiring** (Weapons' 5 doors → 2; Propulsion's whole category having almost no dials). Expect the same
signal — the dead-dial smell test is `grep -rl <Atb> --include=*.cs | grep -v /<Atb>.cs` and **zero hits means the
dial charges mass and buys nothing** (how `Amphibious` and `Fluid` were caught).

## 29. THE ORDER, AND WHY

| # | Door | Input surface (where to grep) | Size | Why here |
|---|---|---|---|---|
| ~~**1**~~ | ✅ **Sensors — DERIVED 2026-07-30 (PART FIVE §32–36).** **Five** dials fail the write-something test (`Resolution` dead AND free · `Scan Time` free · **antenna size free across its whole usable range** · the jammer's self-signature opt-out · two fire-control size dials that cost mass and write nothing, a free 16× saving). And the headline: **the band-matching gate never checks the receiver's upper edge**, so the only receiver in the game (tuned to 600 nm — visible light) detects a reactor at 1705 nm, the wavelength dial has a dominant setting, and the one honest trade (coverage × range² = 39.34) is cancelled — **and the game depends on the bug**, because fixing it alone makes a parked ship undetectable. Seven slices named in §35, **none built**; S0 is blocked on one ruling that also closes the deferred FTL-band question. | `Sensors/` SensorReceiverAtb · SensorSignatureAtb · CloakAtb · JammerAtb; `Weapons/BeamFireControlAtbDB`; `GroundCombat/GroundSensorAtb`; `GeoSurveys/GeoSurveyAtb`; `JumpPoints/GravSurveyAtb`; `Factions/IntelDirectorateAtb` | **~9 — largest** | **The context is already loaded.** Three findings this week land here (signature = thrust §23.2 · warp signature §26c.1 · detection range goes as **√magnitude**). It is also the door that decides **who shoots first**, which every combat finding has leaned on — and it holds the one thing we deliberately deferred: **FTL needs its own band AND a receiver that can see it** (§26c.1). Biggest single payoff. **➡ And it now formally inherits `GravSurveyAtb` — the developer moved the jump-point surveyor OFF Propulsion (§26d.3): finding a node is detection, not movement.** |
| ~~**2**~~ | ✅ **Power — DERIVED 2026-07-30 (PART SIX §38–44).** Six findings, and the fix was already in the codebase: **the RTG carries `power × lifetime = const × mass`** and the reactor (`50 × Mass`, linear) and turbine do not — so the door needs the law **copied, not invented**. `Lifetime` is a free dial that multiplies onboard fuel without costing mass; the turbine's `Output vs Efficency` is **named after a trade that does not exist** (fuel duration is a constant 4×10⁸ s regardless, and `GennyMass` is computed and unused); a reactor silently adds **kW into a kJ store**; the solar array runs on the **sensor** code so the S0 band ruling reaches Power too; and 🔴 **no power component mounts on a colony** while `SustenanceProcessor` already reads a colony power supply. Six slices in §44, **none blocked on a ruling**. | `Energy/` EnergyGenerationAtb · EnergyStoreAtb · EnergySolarGenerationAtb | **3 — smallest** | It just gained **two fresh consumers** — warp bubble creation/sustain (§26b) and weapon energy draw. Deriving it **closes loops we opened this week** rather than opening new ones. Fast, and the supply side of two live demands. |
| **3** | **Chassis** | `Ships/ShipHullAtb` · `GroundCombat/GroundChassisAtb` · `Stations/StationChassisAtb` · `Colonies/BuildingChassisAtb` (all four already share `IChassisAtb`) | 4 | **The door every other door mounts on.** Its budget is what every *"and it costs mass"* claim spends against — so deriving it here means the remaining four derive against a **real** budget. Mostly derivation-not-build: the interface, the mass-budget computation and an enforcement flag all exist. |
| **4** | **Logistical** | `Storage/` CargoStorageAtb · CargoTransferAtb; `Combat/ShipMagazineAtb`; `GroundCombat/` GroundMagazineAtb · GroundBayAtb; `Logistics/LogiBaseAtb` | ~6 | **It is owed two debts.** Propulsion's intrinsic test **cut fuel load and sent it here** (§23.1); Weapons and Propulsion both sent **magazines** here. Also holds the **one confirmed dead attribute found so far — `LogiBaseAtb` has ZERO readers anywhere outside its own file.** |
| **5** | **Command** | `People/AdminSpaceAtb` · `Sites/CommandBerthAtb` | 2 | Small, but it is the chassis the **Governance/Delegation** design bolts onto — and that design already says it is *mostly CONNECT, not build*. Do it **before Civic**, whose academies and admin overlap it. |
| **6** | **Enhancers** | `Combat/` UnitCaliberAtb · CrewAutomationAtb; `GroundCombat/` GroundAugmentAtb · GroundTrainingAtb | 4 | Small, and **two of the four were built recently** (caliber, automation), so this is mostly *writing down what exists* and testing the other two. Low risk, quick win. |
| **7** | **Industrial** | `Industry/` IndustryAtb · MineResourcesAtbDB · LocalConstructionAtb · InfrastructureCapacityAtb; `Construction/ConstructorAtb`; `GroundCombat/GroundConstructorAtb`; `Ships/LaunchComplexAtb` | ~7 | The **economy spine**. The sim genuinely runs on it, so expect *"healthy but under-dialled"* rather than *"dead"* — a different and slower kind of find. Higher blast radius. |
| **8** | **Civic** | `Colonies/` HousingAtbDB · EmploymentAtbDB · FoodProductionAtbDB; `Galaxy/` PopulationSupportAtbDB · GravityToleranceAtb · PressureToleranceAtb; `Tech/ResearchPointsAtbDB`; `People/` NavalAcademyAtb · ResearchAcademyAtb | **~9 — largest** | **Biggest blast radius.** Morale, population, food, housing and employment all live here and are **half-wired by an already-live separate plan** (`docs/society/MORALE-AND-POPULATION-DESIGN.md` M1–M5). Do it last, with the method most practised — and **reconcile against that plan rather than deriving over the top of it.** |

> **⚠ The one ordering to reconsider:** this order is **combat-and-structure first**. If the near-term priority is the
> **colony/population game** rather than the fight, **swap Civic to first and Sensors later** — Civic is where the
> most player-facing decisions are likely missing. That is a developer call, not a technical one.

## 30. PER-DOOR PREDICTIONS — written down NOW so they can be scored later

Marked as predictions, not findings. Recording them makes the method falsifiable.

| Door | Prediction |
|---|---|
| **Sensors** | Richest remaining door. Expect the two axes to be **what you can see × what you emit**, with EMCON as the posture. `CloakAtb`/`JammerAtb` are the likeliest dead-or-thin pair. The **band** question (§26c.1) becomes a real dial once a second receiver type exists. → **SCORED §36: two axes ✅ · richest ✅ · dead pair ❌** — cloak and jammer are among the best-built attributes in the door; the rot is in the OLDEST (the passive sensor and the fire control). 🔒 *Look hardest at the oldest attribute, not the newest.* |
| **Power** | Healthy but **under-dialled** — likely one honest trade (**output ↔ storage**, or output ↔ mass) and little else. May turn out to need *adding*, like Propulsion. → **SCORED §43: under-dialled ✅ · needs-adding-like-Propulsion ✅ · the guessed trade ❌** (it is output ↔ **endurance**, and it is **already implemented on the RTG**) · **"little else" ❌** (six findings, including an unbuildable colony power plant with a live consumer). 🔒 *And the §36 "look at the oldest attribute" lesson held on its first test.* |
| **Chassis** | Mostly already right. The finding will be about the **budget**, not the dials: whether four chassis kinds need four budget *currencies* or one. |
| **Logistical** | At least one confirmed dead dial (`LogiBaseAtb`). Expect the door to collapse to **capacity × what-it-holds**, with the two owed items (fuel tankage, magazines) landing cleanly. |
| **Command** | Thin. Expect *"this is a delegation seat, not a component dial"* — i.e. most of it belongs to the Governance design, not here. |
| **Enhancers** | Thin but honest. The two built dials work; the question is whether the other two earn their place. |
| **Industrial** | Healthy. Expect **rate ↔ efficiency** as the shape, and the finding to be about *missing costs* rather than missing dials. |
| **Civic** | Most entangled. Expect significant overlap with the morale plan and at least one thing that is **infrastructure, not a component** (the way Fortification left the Defense door). |

## 31. WHAT "DONE" LOOKS LIKE FOR A DOOR

1. A **Part N** section in this document: input surface · the questions · the doors/sliders · both tests applied · the proof it reproduces what exists · **the output map**.
2. Its row flipped in **§27 RUNNING TALLY** and in `docs/DOCS-INDEX.md`, same commit.
3. Every cheap win found on the way **shipped as its own gauged slice** — one per push, CI green between (the standing working agreement).
4. Anything found dead is either **wired or cut** — never left charging mass for nothing (the `Amphibious` rule).
5. **A compositional NAMING scheme** for whatever the door can create (§7 · §26f.2): built from the dials, generic
   vocabulary, **no franchise IP**, with the three guards — and the reachable name count **enumerated, not estimated.**
6. **No greyed "not a dial" dials** (§26f.1). Every slider in the door is settable; every emergent number is quoted
   against a **stated reference** instead. And no name guard may merge two distinct designs (§26f.3).
7. **The door's choices change the slider SET, not just the labels** (§26g.3) — if a setting leaves the same dials
   with new names, either the setting is cosmetic or a dial is now writing nothing. **And a hidden slider's value must
   not keep acting on the numbers**, or a stale position silently changes a result the player cannot see.

---

# PART FIVE — SENSORS (door 1 of the remaining eight, derived 2026-07-30)

**Live reference (the worked result, driveable):** `sensors-derived.html` — each of the five jobs opens on its **real
shipped component**, so the first number on screen is always one the game actually ships.

> ⚠ **Three of my own first-pass claims were overturned by building that page**, because it forced the arithmetic over
> the dials' whole range instead of at a point. Each is corrected in place and marked: **§34.5** (the wavelength dial is
> not a narrow trap — the band gate is **one-sided**, and the game *depends* on the bug), **§34.6** (the one honest
> trade is **cancelled** by that same bug), and **§34.7** (antenna size is **not** honest — the 90 kg constant swamps
> the quadratic term across the dial's entire usable range). 🔒 **Lesson: "the formula has the right shape" is not
> "the dial trades." Run the numbers over the dial's real range.**

Run per the §28 recipe. Steps 1–4 and the findings are below; steps 5–7 (prove it reproduces what exists · the marked
output map · land the cheap wins as gauged slices) are the next pass.

## 32. STEP 1 — THE INPUT SURFACE (nine attributes, read in source)

| Attribute | Player dials in its template | What the sim reads it for |
|---|---|---|
| `Sensors/SensorReceiverAtb` | **Antenna Size · Ideal Detection Wavelength · Detection Bandwidth · Resolution · Scan Time** | the whole detection gate — `SensorTools.DetectonQuality` |
| `Sensors/SensorSignatureAtb` | **none — it has no template of its own** | what you EMIT. Authored *by other components* (a thruster, a warp drive, a reactor) |
| `Sensors/CloakAtb` | **Signature Multiplier** | `EmconActivityProcessor` multiplies your `ActivityMultiplier` down |
| `Sensors/JammerAtb` | **Sensitivity Degrade · Range · Self Signature Boost** | divides a hostile receiver's usable signal in `GetDetectedEntites` |
| `GeoSurveys/GeoSurveyAtb` | **Survey Speed** | `GeoSurveyProcessor` — points per pass toward a body's requirement |
| `JumpPoints/GravSurveyAtb` | **Survey Speed** | `JPSurveyProcessor` — inherited from Propulsion (§26d.3) |
| `GroundCombat/GroundSensorAtb` | **Range (km)** | ground hex reveal per tick |
| `Weapons/BeamFireControlAtbDB` | **Range · Tracking Speed** *(+ two mass-only dials — see §34.4)* | the firing gate; `WeaponUtils.GetMaxBeamRange_m` |
| `Factions/IntelDirectorateAtb` | **Op Capacity · Counter-Intel Rating** | espionage ops + the counter-intel shield |

**The physics all of it runs on, in two lines:**

```
attenuation:   signal_at_range = source × 1e6 / (4π r²)          SensorTools.AttenuationCalc
the inverse:   range = √( source × 1e6 / (4π × threshold) )      SensorTools.RangeForSignal
```

🔑 **Everything in this door is that one equation read from a different end.** A receiver lowers `threshold`. A cloak
lowers `source`. A jammer raises the effective `threshold` of someone else. A survey is the same read against a fixed
target with a completion counter. **One law, five jobs** — the same shape Propulsion's `T·v = 2P` turned out to have.

## 33. STEP 2 — GROUP BY THE QUESTION: one door, five answers

**The question this door asks: *what does this part DO with the spectrum?*** The answers are mutually exclusive **per
part** (§1a — a part is a receiver or a cloak, never both), and they split cleanly on the axis §30 predicted:

| Answer | Job | Which end of the equation | Its sliders |
|---|---|---|---|
| **Listen** | see things you did not know were there | lowers **your threshold** | antenna size · band centre · **bandwidth** · scan rate |
| **Look** | resolve a thing you already know is there | the same read, against a fixed target | survey speed |
| **Track** | hold a lock good enough to shoot | threshold **plus** an angular-rate gate | range · tracking speed |
| **Hide** | be seen from less far | lowers **your own source** | signature multiplier |
| **Blind** | make *them* see from less far | raises **their** threshold | degrade · range · self-boost |

**Listen / Look / Track are the "what you can see" axis; Hide / Blind are the "what you emit" axis.** §30's prediction
that those are the two axes is **CONFIRMED.**

### 33.1 ➡ WHAT LEAVES THIS DOOR — `IntelDirectorateAtb` is not a sensor

`OpCapacity` and `CounterIntelRating` are a **seat and a staff rating**, not a reading of the spectrum: they gate how
many covert operations a faction can run and how well it resists someone else's. That is the same shape as an
administrative post — it belongs to **Command** (door 5), whose whole content is *officer + post + capacity*. Moving it
there is the mirror of Propulsion handing the grav surveyor to Sensors: **the door that owns a capability is the one
whose question it answers.**

*(`GravSurveyAtb` arrives here from Propulsion in the same pass — §26d.3. Net: Sensors takes one, gives one.)*

## 34. STEP 3–4 — BOTH TESTS APPLIED, AND **FOUR** DIALS FAIL

The §1 tests are ① *which sim variable does it write? (none + costs nothing ⇒ a bug)* and ② §1a *can it be set knowing
only this part?* **Four dials fail test ①, and one of the four is exploitable.** Every one is verified in source.
*(The count went 4 → 5 → 4. Antenna size was added on a second look and then **retracted** — §34.7 — which is where the
scale-vs-split rule in §34.7a came from. The retraction is left visible rather than edited away: the reasoning is the
useful part.)*

### 34.1 🔴 `Resolution` — DEAD **and** FREE. The worst dial found in any door so far.

```
SensorTools.cs:125     var detectionResolution = recever.Resolution;      ← and it is NEVER USED AGAIN
```

Grepped the whole repository: **that local variable is the only reader in the game.** And `Resolution` does not appear
in the mass formula (`90 + 0.01 × AntennaSize²`), so it **costs nothing either.** A player-facing slider with a
0.1–1000 range that writes nothing and costs nothing.

> This is **worse than `Amphibious`** (§23.3b), which at least charged mass for its silence. `Resolution` is the pure
> case: **a dial that is entirely decoration.** It is the clearest single violation of test ① in the project.

**It should not simply be deleted** — the concept is real and the code comments beg for it (*"resolution should play
into how much gets detected"*, *"have resolution be required to pick out multiple ships close together instead of just
one big signal"*). **The honest fix is to WIRE it**: resolution is what turns *"something is out there"* into *"it is
three destroyers"*, and `SensorReturnValues` already carries a `SignalQuality` for exactly that. See §35.

### 34.2 🔴 `Scan Time` — a FREE LADDER

It writes something real (the reschedule interval, `SensorScan.cs:205`), so it half-passes test ①. But for a *sensor*
it costs **nothing** — no mass, no power. The only energy path is the `IsEnergyGen` **solar-array** branch
(`SensorScan.cs:138`), which is not a sensor at all. So a shorter scan time is **strictly better** and every player
sets it to the 1-second minimum.

**That is the §26.3 fuel failure again: a ladder, not a choice.** The fix is the same shape — give the dial a cost so
the ends trade. Scanning faster should draw power (it is an active sweep) or cost mass, and then *"sweep often and
run hot"* against *"sweep rarely and stay cold"* becomes a real EMCON decision that composes with the whole
detection game.

### 34.3 🔴 The jammer's downside is **PLAYER-OPTIONAL**, so it is not a trade

`JammerAtb`'s own documentation states the catch plainly: *"Blind them, and paint a target on yourself."* But
`Self Signature Boost` is a **dial** (1–10, default 5) and it is **not in the mass formula**
(`200 + 100 × degrade + 50 × range`). So a player sets it to **1** and gets a jammer that blinds the enemy with **no
beacon penalty at all.**

> 🔒 **A general rule this earns:** *a PENALTY the player can dial away for free is not a cost — it is decoration.* If a
> component's downside is settable, it must either be **coupled** to the upside (one number, zero-sum) or **priced**.
> Here the coupling is obvious and physical: **a barrage jammer's noise IS its self-signature.** They should be the
> same number — `SelfSignatureBoost = f(SensitivityDegrade)` — not two dials.

### 34.4 🔴 Two fire-control dials cost mass and write NOTHING — and they are exploitable

`Size vs Range` and `Size vs TrackingSpeed` (0.25–4, default 1) appear in **exactly one place**: the mass formula.

```
Mass  = (Range + TrackingSpeed/100) × Size vs Range × Size vs TrackingSpeed
DBargs = AtbConstrArgs(Range, TrackingSpeed)          ← neither dial is passed to the atb
```

So they are **pure penalties under player control**: set both to 0.25 and fire-control mass drops to **1/16** with no
loss of range or tracking. That is not a dead dial, it is a **free 16× mass saving** — and mass is the currency the
Chassis door (door 3) is about to build its whole budget on. **Fix before Chassis, or Chassis derives against a
budget that can be cheated.**

### 34.5 🔴 CORRECTION + THE HEADLINE — the band gate is ONE-SIDED, and the game DEPENDS on the bug

**I had this wrong on the first pass.** I wrote that the wavelength dial was a *trap* with a narrow valid window.
Building the demo forced the arithmetic, and the truth is sharper and worse. `SensorTools.DetectonQuality` tests:

```csharp
if (Math.Max(receverSensitivityFreqMin, signalWaveSpectraFreqMin)
    < Math.Max(signalWaveSpectraFreqMin, signalWaveSpectraFreqMax))
```

The right-hand side is just `sigMax` (an `EMWaveForm` always has min < max), so **the receiver's UPPER edge is never
consulted.** The test reduces to *"is my band's bottom edge below the signal's top edge"*. A correct interval-overlap
test would read `max(recvMin, sigMin) < min(recvMax, sigMax)`.

**Consequence 1 — the only receiver in the game is tuned to visible light, and everything in the game emits infrared.**
Every magnitude and temperature below is from the shipped templates:

| Emitter | Temp | Peak (Wien) | Its band | Stock magnitude | In the receiver's window? |
|---|---|---|---|---|---|
| **Reactor** (`energy.json`, mass 1500) | 1700 K | **1705 nm** | 1305–2305 nm | **11,250,000** | 🔴 **NO — 580 nm clear of it** |
| Warp drive, bubble held | 3500 K | 828 nm | 428–1428 nm | 3,179,000 | its lower tail only |
| Thruster plume | 3500 K | 828 nm | 428–1428 nm | 109,103 | its lower tail only |
| **The shipped `passive-sensor`** | — | **600 nm** | **475–725 nm** | — | — *(that is the human eye)* |

**Not one emitter PEAK falls inside the receiver's window** — and the reactor, which is **100× louder than a thruster**
and therefore the number that actually sets detection range, sits a full 1000 nm outside it.

**Consequence 2 — the bug is LOAD-BEARING. Fixing the line alone breaks detection.** Computed through the real
formulas: against a stock ship the stock sensor reaches **0.397 Gm** — driven entirely by the reactor. Correct the
overlap test on its own and the reactor drops out, reach collapses to the thruster's **0.039 Gm**, and a ship with its
**engines off becomes completely undetectable.** That is exactly the *"sat at Luna, saw nothing, no battle"* failure the
whole `DetectionSensitivityScale` rebalance was written to cure.

> 🔒 **So the fix MUST be one change: correct the overlap test AND give the base mod an infrared receiver.** And that
> is the same ruling **§26c.1** was already waiting on for the FTL band — **one decision closes both.**

**Consequence 3 — the band-centre dial has a DOMINANT setting.** Because only the lower edge is enforced, tuning the
peak as short as it goes makes the test pass for every emitter in the game. Tune it *long* (2000 nm) and you go blind.
So the dial is not a narrow valid window — it is *"crank it to minimum"*, a ladder and an exploit at once.

### 34.6 ✅ The one HONEST trade already in the door — and nothing tells the player

`Detection Bandwidth` writes **two** things:

```
band width  = peak ± bandwidth/2                                        ← how many KINDS of signal you can see
Efficiency  = techMaxBandwidth / bandwidth
Sensitivity = tech / (EffectiveSize² × Efficiency)                       ← lower is BETTER
            = tech × bandwidth / (EffectiveSize² × techMaxBandwidth)
```

So **a wider band sees more kinds of thing, each from less far** — and it is *exact*: **coverage × range² = 39.34**,
constant across the whole dial (verified numerically at bandwidth 1 / 62.5 / 250 / 500 nm). A genuine zero-sum,
**already shipping**, presented as two unrelated numbers. Wiring the readout is a pure Failure-A
(the number exists, it is just unwired — `docs/combat/INFORMATION-DELTA-DESIGN.md`).

🔴 **But §34.5's gate bug CANCELS it.** If only your lower edge is enforced, a **1 nm** window still admits every
emitter in the game — so you take the narrow window's **6.27 Gm** and give up nothing. **The one honest trade in this
door is only honest once the overlap test is fixed.** Two findings, one line of code.

### 34.7 ⚠️ RETRACTED — antenna size WAS honest. I called it broken twice, and this is the more useful finding.

**First pass:** I said the dial was fine because *range ∝ √mass*. **Second pass:** I said that was wrong because the
90 kg constant swamps the quadratic term. **Both readings compared the wrong two points, and the dial is fine.**

What settled it was reading the shipped *design* rather than the template default:

```
template default   Antenna Size  1.25   →  mass  90.0156       (nobody builds this)
default-design-passive-sensor    2500   →  mass  62,590        ← the SHIPPED sensor is at the dial's MAXIMUM
```

So the quadratic term is not dormant — for the design the game actually ships it is **99.86% of the mass**. And
plotting benefit ÷ cost across the whole dial shows a **well-formed interior optimum**:

| Antenna size | Mass | Range (relative) | **Range per kg** |
|---|---|---|---|
| 10 | 91.0 | 10 | 0.110 |
| **95** | **180.3** | **95** | **0.528 ← the optimum** |
| 500 | 2,590 | 500 | 0.193 |
| 2500 | 62,590 | 2500 | 0.040 |

`d(range/mass)/ds = 0` at `s = √(90/0.01) = 95` — exactly where the fixed electronics package equals the scaling
aperture. **That is a real engineering trade with a real sweet spot**, and the shipped design deliberately sits well
past it (buying reach at a deliberately poor mass efficiency, then capping it with a hard `MaxDetectionRange_m`
horizon). Nothing to fix. **S7 is withdrawn.**

### 34.7a 🔒 THE RULE THIS EARNED — SCALE dials vs SPLIT dials, and the ladder test

Two mis-readings in a row came from applying one test to two different kinds of dial. They are not the same thing:

| Kind | Shape | Is monotonic a problem? | Example |
|---|---|---|---|
| **SCALE** dial — *buy more, pay more* | benefit ↑ and cost ↑ together | **No.** That is a purchase. It earns its keep if **benefit ÷ cost has an interior optimum**, so there is a right size rather than a biggest size. | antenna size · drive mass · total damage |
| **SPLIT** dial — *divide a fixed budget* | one thing ↑ as another ↓ | **Yes.** A split whose product is not invariant is a ladder. | push ↔ economy · startup ↔ endurance · coverage ↔ reach |

> 🔒 **THE LADDER TEST.** Plot **benefit ÷ cost** across the dial's *whole authored range*, and check the *shipped
> design's* position on that curve — not the template default.
> **A SPLIT dial is honest when the product is invariant. A SCALE dial is honest when benefit ÷ cost has an interior
> maximum.** Monotonic benefit-per-cost with no maximum ⇒ a ladder. **Benefit that rises while cost stays flat ⇒ a
> leak** — which is what the fire-control dials actually were (§34.4).

*(This is why the fire-control pair is a genuine defect and antenna size is not: the director's phantom dials cut mass
while the capability stayed **fixed**, so benefit ÷ cost rose without bound toward the minimum setting. There was no
optimum to find.)*

### 34.8 ⚠ Ground radar and space sensors price reach by DIFFERENT LAWS

`ground-radar` mass = `Range × 0.5` — **linear**. The space sensor is **quadratic** (§34.7). Same capability, two cost
laws: on the ground, doubling reach costs 2×; in space it costs 4×. One of them is wrong and it is a developer call
which — but note the ground one is also the one where `GroundSensorAtb` is a flat reveal radius with no band, no
threshold and no signature, so the ground half of this door is a **much simpler model than the space half.** Worth a
ruling before ground detection gets deepened.

## 35. STEP 4 RESULT — THE CHEAP WINS, ranked (not yet built)

Each is a §31-style gauged slice, one per push, CI green between. **Nothing below is built yet.**

| # | Slice | Why it is cheap | What it changes about PLAY |
|---|---|---|---|
| **S0** | 🔒 **BLOCKED ON A RULING — fix the overlap test AND add an infrared receiver, in ONE change** (§34.5) | the test is one line; the receiver is one template | 🔴 **The biggest single change in the door.** It makes band-matching real, which turns the wavelength dial from *"crank it to minimum"* into a counter-intelligence decision, and restores the coverage ↔ reach trade. **Must be one commit** — the fix alone makes a parked ship undetectable. Also closes the deferred FTL-band question. |
| **S1** | **Price `Self Signature Boost` into `Sensitivity Degrade`** — one number, the jammer's noise *is* its self-signature | a formula change in one template; the atb already takes both args | 🔴 **The jammer gets its downside back.** Blinding the enemy paints you, and you cannot opt out. |
| ~~**S2**~~ | ✅ **DONE 2026-07-30 — the two fire-control size dials are DELETED** (and the design's own `Size vs Range` override with them, in the same change: `ComponentDesignFromJson` indexes `ComponentDesignProperties[key]` unguarded, so a design pointing at a removed template property throws `KeyNotFoundException` on New Game — gotcha #10, check the other end). Mass is now `Range + TrackingSpeed/100`. **Byte-identical** — every shipped design used 1 for both, and 1 × 1 = 1. Gauge: `FireControlMassLeakTests` (structural: the dials cannot be re-added; byte-identity on both shipped directors; and mass is now a pure function of capability) | 🔴 **Closes a free 16× mass saving before Chassis derives against that budget.** |
| **S3** | **Give `Scan Time` a cost** (power draw or mass) | one formula; `EnergyGenAbilityDB` is already the consumer for the solar branch | **Sweep-often-and-run-hot vs sweep-rarely-and-stay-cold** becomes a real EMCON decision. |
| **S4** | **Wire `Resolution`** into `SignalQuality` — resolution is what turns *"something"* into *"three destroyers"* | `SensorReturnValues.SignalQuality` already exists and survey reveal already gates on it | **Contact fidelity becomes a purchase.** A cheap sensor sees a blob; a good one counts hulls — which is what makes a scout worth building. |
| **S5** | **Publish the bandwidth trade** as a readout (coverage ↔ reach) | Failure-A: the number exists, it is unwired | The one honest dial in the door starts reading as a decision. |
| **S6** | **Move `IntelDirectorateAtb` to Command** (§33.1) | a doc/ownership move, no code | Keeps the door's question clean; Command inherits it with the other seats. |
| ~~**S7**~~ | ⚠️ **WITHDRAWN 2026-07-30 — §34.7 retracted.** The antenna dial has a genuine interior optimum at size ≈ 95 and the shipped design sits at 2500, where the quadratic term is 99.86% of the mass. It was never a leak; I compared the wrong two points, twice. What it produced instead is the **§34.7a scale-vs-split rule and the ladder test**, which is worth more than the slice would have been. | — | — |

**Blocked on a developer ruling, not on work:** **S0** (§34.5 — correct the overlap test *and* add an infrared receiver
in the SAME change, or else compute the band and remove the dial; either way it also closes the deferred FTL-band
question) and §34.8 (one cost law for reach, or two).

## 36. SCORING THE §30 PREDICTION — half right, and wrong in an informative direction

§30 predicted, before any of this was read:

> *"Richest remaining door. Expect the two axes to be **what you can see × what you emit**, with EMCON as the posture.
> `CloakAtb`/`JammerAtb` are the likeliest dead-or-thin pair."*

- ✅ **The two axes: CONFIRMED.** Listen/Look/Track vs Hide/Blind is exactly that split, and it fell out of the input
  surface rather than being imposed on it.
- ✅ **Richest door: CONFIRMED** — four failing dials, one trap, one unpublished real trade, one cost-law conflict.
- ❌ **The dead pair: WRONG.** `CloakAtb` and `JammerAtb` are among the *best-built* attributes in the door —
  health-scaled, flag-gated, defensively clamped, gauged. **The rot is in the OLDEST component**, the passive sensor
  (`Resolution`) and the fire control (the two size dials).

🔒 **The lesson, recorded because it will repeat:** *I predicted decay in the newest code and found it in the oldest.*
Recent components were written with the conventions in hand; the long-standing ones predate them and nobody has had a
reason to re-read them. **For the remaining seven doors, look hardest at the oldest attribute, not the newest.**

---

## 37. STEP 6 — WHAT GOES OUT, EVERY ROW MARKED (verified in source)

Same two columns the Propulsion map carries (§25): **a play state** and **the sentence that says what it changes about
how the game is PLAYED.** `✅ LIVE` · `🟢 NEW` · `🔵 HALF` (the number exists, nothing reads it) · `🔴 PROPOSED`.

| # | What leaves the door | Goes to | Play state | **What it changes about how the game is PLAYED** |
|---|---|---|---|---|
| 1 | **Detected / not detected** | `FactionInfoDB.SensorContacts` → the track table | ✅ LIVE | **The entire fog of war.** Everything below is downstream of this one boolean. |
| 2 | **Detection, as the battle gate** | `CombatEngagement.cs:226,337` — `RequireDetectionToEngage` | ✅ LIVE *(flag, client-on)* | 🔴 **Whether a fight happens at all.** Two hostile fleets in weapon range do **not** engage until someone sees someone. Sensors decide *if* there is a battle, not just how it goes. |
| 3 | **Detection, as the FIRING gate** | `CombatEngagement.CanFireAt` (`:1974`) | ✅ LIVE | 🔴 **The largest asymmetry in the game.** `FirstStrike_SeerWipesBlindEnemy_Unscathed`: two **equal** fleets, one blind — the seeing side **wipes it taking zero losses.** Detection is worth more than any weapon or plate. |
| 4 | **Contact loudness** | `ThreatAssessment` → the NPC threat picture | ✅ LIVE | **What the AI thinks it is facing.** It sums the loudness of its live contacts, so *your* EMCON decisions steer *its* aggression. Note it reads signal **strength** — the `SignalQuality` path was design-cut. |
| 5 | **`SignalQuality`** | `SystemBodyInfoDB:154` / `StarInfoDB:130` — reveal at **0.20 / 0.80** | ✅ LIVE | **How much a survey tells you.** Below 0.20 you learn nothing; above 0.80 you get the full picture. The only live consumer of contact *quality* rather than *presence*. |
| 6 | **Geo-survey completion** | `GeoSurveyableDB` → mineral reveal; `ExpandResolver:68` | ✅ LIVE | **You cannot mine what you have not surveyed, and the AI will not colonise it either.** The economy's first gate. |
| 7 | **Jump-point discovery** | `JumpPointDB.IsDiscovered` → `JumpRouter:122` · `MilitaryReach` | ✅ LIVE | 🔑 **The map itself is per-faction.** An unsurveyed gate does not exist to you *or* to the AI's invasion planner — so surveying is how the strategic map grows. The layer most 4X games skip. |
| 8 | **Field-site discovery** | `SiteVisibility.IsDiscoveredBy` | ✅ LIVE | **Whether an exploration episode is even on your map.** The Site Engine's front door. |
| 9 | **Ground radar reach** | `GroundSensorAtb.Range_km` → hex reveal → the ground tactical brain | ✅ LIVE | **A ground battle has fog too**, and the brain is honest about it (an undetected enemy counts as zero). Radar is what lets a defender react instead of being flanked. |
| 10 | **Fire-control range** | `WeaponUtils.GetMaxBeamRange_m` → the weapon-range battle trigger | ✅ LIVE | **How big the battle is.** The longest reach present sets the engagement envelope, so a director out-ranging the guns is wasted and one under-ranging them throws the guns away. |
| 11 | **Fire-control tracking speed** | `BeamFireControlAtbDB.TrackingSpeed` | ✅ LIVE | **Whether you can hold a lock on something nimble** — the counter to the evasion Propulsion sells. |
| 12 | **Cloak factor** | `EmconActivityProcessor` → `SensorProfileDB.ActivityMultiplier` | ✅ LIVE | **Ambush becomes buildable.** ×0.2 signature = seen at 45% the range, so you choose where the fight starts. Health-scaled, so a shot-off cloak lights you up. |
| 13 | **Jamming divisor** | `SensorTools.GetDetectedEntites` | ✅ LIVE *(flag)* | **You can manufacture the row-3 blindness on purpose** — the strongest offensive act in the game, once it costs something (§34.3). |
| 14 | **Self-detection range** | `SelfDetectionRange_m` → the EMCON readout | ✅ LIVE | **The player can finally see how loud they are** — which is what makes the Active/Dark posture a decision instead of a guess. |
| 15 | **Intel op capacity** | `IntelDirectorateDB` → espionage ops | ✅ LIVE | **How many covert operations you can run at once, and how well you resist theirs.** ➡ **Leaves for Command** (§33.1) — it is a seat, not a sensor. |
| 16 | **Band match** | `DetectonQuality` — the overlap test | 🔵 **HALF, and BROKEN** | 🔴 **The receiver's upper edge is never checked (§34.5).** So band-matching gates nothing: the loudest emitter aboard a ship is detected by a sensor a thousand nanometres off its wavelength, the wavelength dial has a dominant setting, and the coverage ↔ reach trade is cancelled. **The game depends on the bug** — fix it alone and a parked ship becomes undetectable. |
| 17 | **`Resolution`** | *nothing* — one dead local, `SensorTools.cs:125` | 🔵 **HALF** | **Contact fidelity is not a purchase.** Every sensor tells you the same amount about what it found, so there is no reason to build a good scout over a cheap one. Wiring it (§35 S4) is what makes *"it is three destroyers"* different from *"something is out there."* |
| 18 | **`Scan Time`** | `SensorScan` reschedule interval | ✅ LIVE **but free** | **Nothing** — because it costs nothing, everyone pins it at 1 s. Priced (§35 S3) it becomes *sweep often and run hot* vs *sweep rarely and stay cold*, which composes with rows 3, 12 and 14. |
| 19 | ~~**`Size vs Range` · `Size vs Tracking`**~~ | **deleted** | 🟢 **NEW — FIXED** | ✅ **The free 16× mass saving is closed**, structurally: the dials no longer exist, so no design can re-open it. Mass is now `Range + TrackingSpeed/100` — a pure function of capability. Byte-identical (every shipped design used 1). **Chassis now derives against a budget that cannot be cheated.** Gauge: `FireControlMassLeakTests`. |
| 20 | **Jammer `Self Signature Boost`** | `SelfSignatureFactor` — but it is a free dial | 🔵 **HALF** | 🔴 **The jammer's downside is opt-out**, so blinding the enemy is currently free. 🔒 *A penalty you can dial away is decoration.* |
| 21 | **An infrared receiver band** | would let the overlap test be correct | 🔴 PROPOSED | **Makes tuning a counter-intelligence decision** — a sensor set for thruster plumes is blind to a cold hull. Same ruling as the deferred FTL band, so **one decision closes both.** |
| 22 | **The sensor destroyed** | Damage → `ReCalcAbilities` | ✅ LIVE | 🔑 **Shoot the eyes out and row 3 reverses.** The grave rung that makes detection a *target*, not a stat — and it is how the first-strike gauge blinds its victim. |

**Reading the marks:** 15 rows `✅ LIVE`, **4 `🔵 HALF`** (all four are the failing dials — the marks and §34 line up
one-to-one), 1 `🔴 PROPOSED`, and **1 `🟢 NEW`** — row 19, the one leak that was real, now closed.

🔑 **The shape of the map is itself the finding.** Propulsion's outputs mostly changed *how well* you fight. **Sensors'
outputs decide _whether_ you fight (row 2), _whether you can shoot back_ (row 3), _how big the map is_ (row 7), and
_whether the economy can start at all_ (row 6).** It is the most load-bearing door derived so far — and it is the one
with five dials that write nothing and a broken gate underneath. **The gap between what this door decides and how
carefully it is built is the widest found in the project.**

---

# PART SIX — POWER (door 2 of the remaining eight, derived 2026-07-30)

**Live reference (the worked result, driveable):** `power-derived.html` — each job opens on its **exact shipped
template** (reactor 1500 kg → 75,000 kW; RTG 1000 kg / 5 yr; turbine 2000 kg / 50%; solar 100 m²; battery 2000 kg →
1.00 M kJ), and it carries an **"apply the RTG's law"** switch so the headline is something you can watch happen:
tick it on the reactor and its flat 75,000 kW starts trading against endurance. Ticking it at the stock setting is
**byte-identical** on both generators — verified across 750 dial/setting combinations.

Smallest door on the list — three attributes — and it took the §36 lesson as its first instruction: **look hardest at
the oldest attribute.** That is exactly where the findings were.

## 38. STEP 1 — THE INPUT SURFACE (three attributes, five templates)

| Attribute | Templates | Player dials | What the sim reads it for |
|---|---|---|---|
| `Energy/EnergyGenerationAtb` | `reactor` · `rtg` · `steam-turbine-reactor` | **Mass · Lifetime** (reactor) · **Mass · Operational Lifetime** (RTG) · **Mass · Output vs Efficency** (turbine) | `EnergyGenAbilityDB.MaxOutputFromReactor`, `LocalFuel` |
| `Energy/EnergyStoreAtb` | `battery-bank` | **Mass** | `EnergyGenAbilityDB.EnergyStoreMax` |
| `Energy/EnergySolarGenerationAtb` | `solarArray` | **Area · Ideal Absorption Wavelength · Bandwidth** | solar output, via the **sensor receiver** code |

**Where it goes** — and the list is short but load-bearing:

| Consumer | What power decides there |
|---|---|
| `WarpMoveCommand:258` | **the departure gate** — stored energy below the bubble creation cost and the ship cannot leave |
| `WarpMoveProcessor:244–246` | **the transit bill** — sustain charged per second, so a long crossing can strand you |
| `MilitaryReach:156–160` | **the AI's reachability read** — it checks a ship's stored energy against its bubble cost before planning |
| `WeaponSupply` → `GroundUnitAssembly` | 🔒 **the ground supply GATE** — an energy weapon on a ground unit is refused unless mounted reactors supply its watts |
| `SustenanceProcessor:51` | colony **power shortage** → morale — *see §39.6, which is the problem* |

## 39. STEP 3–4 — BOTH TESTS APPLIED. SIX FINDINGS, AND THE FIX IS ALREADY IN THE CODEBASE.

### 39.1 🔑 THE HEADLINE — the RTG already has the trade the reactor lacks. Copy it, do not invent it.

Work the three generators' arithmetic out and they are **not three variations on one law — they are one that has the
law and two that do not.**

```
REACTOR   Power Output = 50 × Mass                      ← strictly LINEAR. bigger is better, full stop.
          Lifetime is a separate FREE dial (§39.2)

RTG       Fuel             = Mass × 0.5
          Fuel Consumption = 0.001 ÷ Operational Lifetime
          Power Output     = Fuel × Efficiency × Fuel Consumption
        ⇒ Power ∝ Mass ÷ Lifetime      i.e.   POWER × LIFETIME = const × MASS      ← the zero-sum, already built

TURBINE   Generator Output ∝ Output-vs-Efficency        ← and nothing pays for it (§39.3)
```

> 🔒 **So the Power door does not need a trade invented; it needs the RTG's law applied to the other two.**
> `output × endurance = const × size` — **run hot and refuel often, or sip and run for years.** That is the same
> `T·v = 2P` shape as thrust ↔ exhaust velocity, the gearbox, the warp bubble, the jump range and the gate cycle.
> **Six places now, one law.**

And it is a genuinely good decision because the two ends serve different fleets: a warship wants peak output for the
warp bubble and the beam batteries; a picket or an outpost wants to sit still for a decade without a tanker.

### 39.2 ⚠️ CORRECTED — `Lifetime` is NOT free. It costs FUEL; it just costs no MASS.

**I checked the `Mass` formula and stopped there. A template has more cost blocks than that**, and the reactor's
`ResourceCost` charges the dial directly:

```
ResourceCost.fissile-fuels = Fuel Consumption × 3600 × Lifetime        ← linear in the dial
```

So a ten-year reactor costs **ten times the fissile fuel** to build. The dial is paid for. **What is still wrong is
narrower and still real:** it adds **no mass**. A reactor carrying ten years of fuel weighs exactly what a
one-hour reactor weighs — **fuel with no weight** — and mass is the currency the Chassis door gates on, so the free
side of the dial is the side that matters for ship design. The RTG gets this right by construction
(`Fuel = Mass × 0.5`, so its fuel *is* part of its mass); the reactor and the turbine do not.

> 🔒 **THE RULE THIS EARNED — check EVERY cost block before calling a dial free.** A component template prices a dial
> through **seven** independent channels: `Mass` · `Volume` · `CrewReq` · `ResearchCost` · `CreditCost` ·
> `BuildPointCost` · **`ResourceCost`**. Reading one and concluding "free" is how I got this wrong. **The correct
> question is not "is it in the mass formula" but "which of the seven does it appear in, and is that the channel the
> player is actually constrained by?"** Here the answer is *"minerals yes, mass no"* — and for a ship, mass binds.

### 39.3 ⚠️ CORRECTED + 🔒 RULED — the turbine's dial was MIS-NAMED, not empty. And 12.7 years is now DELIBERATE.

**Two developer rulings landed on this one (2026-07-30):** *"stm turbine should be stuck at 12 yrs"* and *"you took
some slider options away … find a balance."* Both narrow what I had wrong.

**What I claimed:** `Output vs Efficency` is *"a dial named after a trade that does not exist"*, because
`FuelDuration = FuelMass × 40e9 ÷ (FuelMass × 100) = 4×10⁸ s` — **constant, independent of the dial and of mass** —
while `GeneratorOutput` rises straight with it. So the dial only ever added output.

**🔒 The constant duration is now RULED INTENDED.** ~12.7 years is the steam turbine's *identity*: a big fuelled plant
you install and forget for a decade. It is not a bug to be fixed; it is the thing that distinguishes it from a reactor
whose `Lifetime` you dial. **So the dial must NOT be re-purposed into output↔endurance** — that would delete the
turbine's character to satisfy a pattern.

**⚠️ And the dial was never empty — I read the output formula and stopped.** Traced through the `ResourceCost` block
(the same mistake as §39.2), it is a **materials trade**, and a well-formed one:

| Setting | Core / Generator | Output | Core-side materials | Generator-side materials | Duration |
|---|---|---|---|---|---|
| **30%** | 600 / 1400 | 28,800 kW | fissile 360 · graphite 90 · titanium 3 | **tungsten 70 · nickel 143** | 12.7 yr |
| **50%** | 1000 / 1000 | 48,000 kW | fissile 600 · graphite 150 · titanium 5 | tungsten 50 · nickel 105 | 12.7 yr |
| **70%** | 1400 / 600 | **67,200 kW** | **fissile 840 · graphite 210 · titanium 7** | tungsten 30 · nickel 67 | 12.7 yr |

**A bigger core makes more power and eats fissile fuel, graphite and titanium; a bigger generator makes less power and
eats tungsten and nickel instead.** Output rises *and the fissile bill rises proportionally* — so by §34.7a it is an
honest **scale** dial (a purchase, not a ladder), and it additionally decides **which minerals you spend**, which is a
genuine strategic axis in a game whose economy is mineral-constrained.

**So the only real defect was the NAME.** It promised *efficiency* and delivered *output plus a materials mix*.
**Renamed to `Core vs Generator`**, with a description that states the trade and states plainly that fuel duration is
fixed at ~12.7 years by design. **No dial removed, no dial re-purposed** — which is the developer's *"find a balance"*
applied: the fix was a truthful label, not a re-engineering.

🔒 **The rule this earned, and it has now bitten three times (§39.2 · §39.3 · the fire-control pair):** *before calling
a dial empty, read **every** cost block — `Mass` · `Volume` · `CrewReq` · `ResearchCost` · `CreditCost` ·
`BuildPointCost` · **`ResourceCost`**. A dial that writes no performance number may still be writing your bill of
materials, and that is a real consequence.*

### 39.4 🔴 A reactor is silently also a BATTERY, and the units do not match

`EnergyGenerationAtb.cs:65,70` — `genDB.EnergyStoreMax[EnergyTypeID] += PowerOutputMax`.

`PowerOutputMax` is **kW**; `EnergyStoreMax` is **kJ**. So installing a reactor adds storage equal to **exactly one
second of its own output**, by dimensional accident. Two consequences: the `battery-bank` is partly redundant (a
reactor is its own small battery), and the number is dimensionally wrong, so any future balance pass on storage will
be fighting a hidden term. **Either make it deliberate (`store += output × someSeconds`, a stated buffer) or remove
it.**

### 39.5 🔑 THE SOLAR ARRAY RUNS ON THE SENSOR CODE — so §34.5's bug reaches into POWER

`solarArray` builds an `EnergySolarGenerationAtb` whose constructor is the **same waveform + best/worst-efficiency
shape** as `SensorReceiverAtb`, and `SensorReceiverAtb` itself carries an `IsEnergyGen` flag with a dedicated
constructor overload. Absorption is band-matched the same way detection is.

```
Best Efficiency = tech-panel-efficiency × (tech-panel-bandwidth × 0.5 ÷ Bandwidth)     ← narrower band, better panel
```

**That is the same coverage ↔ efficiency zero-sum as the sensor's bandwidth dial (§34.6)** — the seventh place one law
turns up. And it means the ruling on **S0** (fix the overlap test + add an infrared band) **changes solar output too.**
🔒 **One decision now touches three doors: Sensors, Power, and the deferred FTL band.** Worth knowing before it is
made, not after.

### 39.6 🔴 NO POWER COMPONENT CAN BE BUILT ON A COLONY — and the colony power-shortage code reads a supply that cannot exist

Mount types, straight from `energy.json`:

| Template | Mounts |
|---|---|
| `reactor` | ShipComponent, ShipCargo, Fighter, GroundUnit, Station |
| `rtg` · `steam-turbine-reactor` | ShipComponent, ShipCargo, Fighter, GroundUnit |
| `battery-bank` | ShipComponent, ShipCargo, Fighter |
| `solarArray` | **`1`** — a raw integer that happens to equal `ComponentMountType.ShipComponent` |

**Not one of the five includes `PlanetInstallation`.** So a colony can never mount a generator — and yet
`SustenanceProcessor:51` reads `province.TryGetDataBlob<EnergyGenAbilityDB>()` for the colony's power supply, feeding
the power-shortage term that drives morale. **The consumer is built and the producer is unbuildable.**

It does not bite *today* only because colony power demand is still calibrated to zero (`SustenanceTests`: *"inert by
default — 0 demand → 0 shortage"*). **The moment M5b's demand is turned on, there is no power plant a player can
build to answer it.** That is a cradle-to-grave hole with a live consumer already waiting at the end of it.

*(And `solarArray`'s `MountType: 1` works only by numeric coincidence. Every other template names its mounts. It also
means the one generator that needs no fuel cannot be put on a colony — the most obvious thing a player would try.)*

### 39.7 🔑 THE DEVELOPER'S QUESTION — *"what about a type of power source that needs fuel?"* — and it re-orders the door

**All three burn-generators need fuel. None of them needs it to RUN.** Traced end to end:

| Rung | State |
|---|---|
| The fuel exists as a real material | ✅ `fissile-fuels` — refined from **fissionables + hydrocarbons**, `IndustryTypeID: refining`, 50,000 credits a unit |
| It is charged at BUILD time | ✅ all three: reactor `Fuel Consumption × 3600 × Lifetime` · RTG `Mass × 0.5` · turbine `FuelMass` |
| A running generator carries a fuel load | ✅ `EnergyGenerationAtb.cs:60` — `LocalFuel = maxUse × Lifetime` |
| A running generator BURNS it | ✅ `EnergyGenProcessor.cs:52` — `LocalFuel -= fueluse × t.TotalSeconds` |
| **Running out has a consequence** | 🔴 **NO. `LocalFuel` is never read as a gate — anywhere.** |

Grepped the whole repository for `LocalFuel`. **Five hits, and not one is a condition:** the setter, the decrement, the
field declaration, a `SensorScan` line that *overwrites* it for solar arrays, and **a text label in `DebugWindow`.**

> 🔴 **So `LocalFuel` runs negative and output never stops. Every reactor in the game runs forever on nothing.**
> And the reactor's own description says **"A non refuelable reactor"** — the design *intended* it to run out. The
> counter is there, the drain is there, and the consequence was never wired.

**Which means fuel is a construction material, not a logistics burden** — and three things follow:

1. **🔑 THE RTG'S LAW BUYS NOTHING TODAY.** §39.1 said copy `power × lifetime = const × mass` onto the reactor and the
   turbine. But endurance only *means* something if running dry costs you something, and it does not. **So the slice
   order flips: wire the fuel-exhaustion consequence FIRST (or in the same change), or you are giving the reactor a
   trade against a cost that does not exist.** That is a real re-ordering, and it came from the question rather than
   from the derivation.
2. **Solar's "no fuel ever" advantage is smaller than it looks** — a fuelled generator needs no resupply either. What
   solar actually saves is the **fissile-fuels build cost** (and it genuinely attenuates with distance from the star:
   `EnergySolarGenProcessor` runs `AttenuatedForDistanceList(starProfile, distance, 0.1)`, so an outer-system panel is
   honestly worse). **Solar is the cheap option, not the convenient one** — and nothing says so.
3. **It is a cradle-to-grave hole with the grave rung missing.** Mineral → material → component → installed → *and then
   nothing*. A reactor should be a **consumable with a clock**: it runs for its designed life and then the ship is
   adrift, which is exactly what "non refuelable" promises. Wiring that one gate turns `Lifetime` from a build-cost
   multiplier into **the most consequential dial in the door.**

### 39.7a ✅ BUILT — and "longer" turned out to be a UNIT BUG, not a balance call

The developer's three asks, done as one slice (they are interdependent — a gate without a real lifetime strands the
fleet, and a burn-rate dial without a gate writes nothing).

**① The gate.** `EnergyGenProcessor.EnableFuelExhaustion` (static, **default OFF** — the
`RequireDetectionToEngage`/`EnableJamming` discipline, so CI inherits nothing and the client arms it). A generator that
burns fuel and has none left produces **nothing**; `LocalFuel` now floors at 0 instead of running negative; and
`EnergyGenAbilityDB.IsFuelStarved` is a `[JsonIgnore]` computed read (no save-format change, no copy-ctor entry).
Solar is never starved — `maxUse == 0`, nothing to run out of.

**② 🔴 "Make lifetime longer" was a UNIT BUG.** `EnergyGenerationAtb` consumes `Lifetime` in **seconds**
(`LocalFuel[kg] = maxUse[kg/s] × Lifetime`, drained by `fueluse × t.TotalSeconds`). But:

| Template | Authored as | Fed to the atb as | Real endurance |
|---|---|---|---|
| `reactor` | **8760 hours** | 8760 seconds | 🔴 **2.43 hours** |
| `rtg` | **5 years** | 5 seconds | 🔴 **5 seconds** |
| `steam-turbine-reactor` | `FuelMass ÷ BurnRate` = **seconds** | seconds | ✅ 12.7 years |

**Only the turbine was dimensionally correct — and it is the only one with a shipped design, which is exactly why
nobody had ever noticed.** Both now convert explicitly (`Fuel Load Seconds = Lifetime × 3600`;
`Lifetime Seconds = Operational Lifetime × 31,557,600`), which makes the reactor's life **3600× longer without
changing a single authored number.** The reactor's `Lifetime` Max also rises 87,600 → 876,000 h (100 years) so a player
*can* build a long-life plant and watch the fissile bill scale honestly. Its `ResourceCost` is unchanged in value — it
already multiplied by 3600, so the cost block was right all along and only the atb argument was wrong.

**③ ✅ The burn rate is now a dial — `Output vs Economy` (0.5–2.0, default 1).**

```
Power Output     = 50 × Mass × OvE
Fuel Consumption = Power Output × k × OvE          ⇒  fuel per kilowatt ∝ OvE
⇒  2× the power costs 4× the fuel for the same endurance.
```

**Driving a core harder costs more fuel for every kilowatt it makes**, not merely more fuel — so it passes the §34.7a
ladder test (benefit rises, cost rises faster). At OvE 1.0 it is **byte-identical**. Two buildable designs ship it
cradle-to-grave: `default-design-fission-reactor` (75,000 kW, 73.9 kg of fuel for a year) and
`default-design-fission-reactor-derated` (37,500 kW on **18.5 kg** — half the power, a quarter of the fuel, twice as
economical per kilowatt).

**⚠ One calibration decision, stated because it is a judgement:** the reactor's specific fuel rate was
`Power × 1e-7`, which is **~3200× the steam turbine's measured 3.125e-11 kg/s per kW** and would have made a year of
fuel weigh **236 tonnes** for a 1500 kg reactor. It is now anchored on the turbine's rate — the only *calibrated*
fuelled generator in the game, being the only one with a shipped design. Nothing in the game changed: no design used
the `reactor` template before this slice.

**Gauge: `PowerFuelGateTests`** — the seconds conversion (exact), carried fuel == charged fuel (the books balance),
the dial's exact ½-power/¼-fuel ratios, the turbine anchor as a *ratio* (so a future turbine re-tune fails loudly
instead of drifting), and the gate biting on / inert off with the flag reset in a `finally`.

**Still open for the developer:** whether to **arm the flag** (a one-line client change) and whether a reactor should
be **refuelable** — its own description says *"A non refuelable reactor"*, so today `Lifetime` is a genuine service
life and a dead reactor is a dead ship. That is a good mechanic; it just wants to be a deliberate one.

**The cheapest honest version of the gate:** when `LocalFuel <= 0`, clamp `TotalOutputMax` to 0 (a dead reactor
generates nothing) and publish an event. Everything downstream already handles zero power correctly — the warp
departure gate refuses (`WarpMoveCommand:258`), the ground supply gate refuses (`WeaponSupply`), the AI stops planning
(`MilitaryReach:156`). **The consequence system is already built; only the trigger is missing.**

*(⚠ Balance note before building it: at the shipped 8760-hour lifetime a reactor dies after **one game year**, which
would strand the entire starting fleet. So the gate needs either a much longer default lifetime, a refuelling order, or
both — that is the decision, and it is the developer's, not the derivation's.)*

### 39.8 🔒 EVERY OPTION MUST HAVE A JUSTIFICATION OVER THE OTHERS (developer, 2026-07-30)

> *"Each of the types of power options should have a justification over the other."*

**The right rule, and the door was failing it on three of five.** An option a player would never choose is not an
option, it is clutter — so **no power source may be beaten on every axis the simulation reads.** Each must win at
least one outright, and *that win is its reason to exist.*

Measured, on every axis the sim actually consumes:

| Option | kW / kg | kW / m³ | Crew | Signature | Fuel | Mounts |
|---|---|---|---|---|---|---|
| **reactor** | **50.00** ← wins | 50 | 🔴 **`[Mass]` = 1500** | loudest in the game | fissile | Ship · Fighter · Ground · **Station** |
| **steam turbine** | 24.00 | **24,000** ← wins | 3 | 1700 K | fissile | Ship · Fighter · Ground |
| **rtg** | 🔴 **0.0011** | 0.0011 | 1 | 1700 K | fissile | Ship · Fighter · Ground |
| **solarArray** | 0.135 | 270 | **0** ← wins | **none** ← wins | **none** | 🔴 **`1`** — ship only |
| battery-bank | *(stores)* | — | 0 | none | none | Ship · Fighter |

**Three failures, and one thing that was already perfect:**

**① 🔴 The reactor's crew bill was CREW = KILOGRAMS.** `CrewReq: "[Mass]"` — a 1500 kg reactor demanded **1500 crew**.
Component crew sums into a ship's `CrewReq` (gated by `ManpowerTools.ResolveBuild`) and into
`InfrastructureProcessor`'s capacity demand, so **the highest-power-density generator in the game was unusable in
practice.** Its justification existed and was cancelled by a unit error — the same class of mistake as §39.7a's
hours-as-seconds. **Fixed: `Max(2, [Mass] / 500)`** — three operators at the stock 1500 kg, fifty at 25 tonnes.
*(The divisor is a judgement, anchored on the turbine's three crew for a comparable plant.)*

**② 🔴 The RTG won nothing at all.** ~22,000× worse per kilogram than a turbine **to save two crew.** A strictly
dominated recipe — precisely the shape RP-1 had before the fuel-grade fix (§26.3). It needed an axis it owns:
**crew 1 → 0**, making it the only **fuelled** generator that needs *nobody aboard*. That is a real and distinct
justification (probe · deep-space beacon · unmanned outpost) and it is physically honest — an isotope block has no
moving parts and no operators. ⚠ **Its four-order density gap is flagged, not silently tuned:** closing it means
picking a number, and the shape (output should rise, endurance is already its trade) is a developer call.

**③ 🔴 Solar could not be built anywhere but a ship.** `MountType: 1` — a raw integer that happened to equal
`ComponentMountType.ShipComponent`. So the one generator needing no fuel and no crew, the obvious choice for a colony
or a station, could go on neither. **Fixed: `ShipComponent, ShipCargo, PlanetInstallation, Station`** — which also
closes §39.6 (a colony can now build a power plant, and `SustenanceProcessor`'s waiting consumer has a producer).

**④ 🔑 And solar's real justification needed no fix — it was already there and nothing said it.** The three fuelled
generators each carry a `SensorSignatureAtb` at 1700 K, the reactor being **the loudest thing on a ship**; a solar
array emits **nothing**. **Solar is the only silent power source in the game.** Detection decides who shoots first
(§37 row 3 — an equal blind fleet is wiped taking zero losses), so silence is a first-class reason to choose it. Its
costs are honest and already wired: 370× worse per kilogram, and it attenuates with distance from the star.

### 39.8a The justification table, as it now reads

| Option | **Its one justification** | Its cost |
|---|---|---|
| **Reactor** | **most power per kilogram** — the warship core | loudest signature · most crew · most fissile fuel |
| **Steam turbine** | **most power per cubic metre** (1000×) on three crew — the station and colony plant | needs real mass; a fixed 12.7-year fuel duration |
| **RTG** | **needs nobody aboard, for decades** — probe · beacon · unmanned outpost | tiny output *(gap flagged)* |
| **Solar array** | **silent and fuel-free** — the stealth and logistics-free choice | low density; dies far from a star |
| **Battery bank** | **a different job**: buffers the burst a warp departure demands all at once | generates nothing |

🔒 **THE GENERAL RULE, for the remaining six doors.** *Every option behind a door must win at least one axis the sim
reads, outright. State which axis, in the template description.* And the corollary that caught two of these three:
**an option can have a perfectly good justification that a unit bug cancels** — the reactor's density was real and
unreachable behind 1500 crew; solar's silence was real and unreachable behind a mount flag. **Check that the
justification is actually reachable, not merely present.**

**Gauge: `PowerJustificationTests`** — the reactor wins per-kg (and its crew is no longer a kilogram count), the
turbine wins per-m³, solar is asserted to be the *only* silent one, the RTG's zero-crew axis is asserted on the
template, the battery is asserted to be a different job — and **`NoPowerType_IsDominatedOnEveryAxis` is the rule
itself**, so the next dead recipe fails CI instead of shipping.

#### 39.8c ⚠ CORRECTION 7 — the gauge went red, and **the gauge was wrong, not the data** (CI, 2026-07-30)

`NoPowerType_IsDominatedOnEveryAxis` failed on first run with *"'Fission Reactor' is beaten on every axis"* — while the
sibling test asserting the reactor wins per-kilogram **passed**. Two tests in one file disagreeing is a gauge fault, not
a finding, and it is worth writing down because of *which* kind of fault it was.

**What happened.** The domination test needed a kilowatt number for a solar array, and a panel has none: the engine
**recomputes** it every tick from the star's attenuated light (`EnergyGenHotloopProcessor.ComputeSolarMax`). So the test
did the physics itself — `Area × BestEfficiency × 1.361` — and **`BestEfficiency` is a percent, not a fraction.** The
shipped panel reads `8.0`, so the test read **eight hundred percent absorption**: a 20 kg panel at 54 kW/kg, beating the
1500 kg reactor's 50, leaving the reactor winning nothing. The engine has the conversion right — an explicit `* 0.01` at
`EnergySolarGenProcessor.cs:106`. The test had re-derived what the simulation already computes, and got it wrong by 100×.

**The fix is the campaign's own method applied to its own gauge:** *don't re-derive what the sim reads — read it.* The
test now calls **`AbsorbedPower`** with a 1 AU illumination and uses what the engine returns, so the comparison cannot
drift from the simulation again. Honest numbers, all three at the same place:

| | kW/kg | kW/m³ | crew | silent |
|---|---|---|---|---|
| **Reactor** (1500 kg, 75,000 kW) | **50** ✅ | 50 | 3 | no |
| **Turbine** (2000 kg, 48,000 kW) | 24 | **24,000** ✅ | 3 | no |
| **Solar** (20 kg, ~10.9 kW *at 1 AU*) | 0.54 | 1,089 | **0** ✅ | **yes** ✅ |

Every type wins at least one axis outright. **The rule held; the instrument was miscalibrated.**

🔑 **And the correction names a general trap for the remaining doors.** Two of the five power options have **no single
number to compare at all** — a solar array's output depends on *where it is*, and an RTG ships no design. A door's
justification table therefore cannot be built from stored attribute values alone; for a position-dependent option you
must **name the place you are measuring at** (this test names 1 AU, and says so in its own readout), and for a
design-less option you assert on the **template**. Comparing a positional quantity as though it were a constant is how
you get a confident wrong answer.

⚠ **One unrelated thing found while reading that method, recorded not fixed:** `AbsorbedPower` computes
`overlapFraction` (the share of the star's band the panel actually covers) and then **never uses it** — absorption is
`magnitude × interpolatedEff` with no overlap term. A computed value with no consumer, which is the exact shape this
campaign has flagged in seven doors, here in the *engine* rather than a template. It means a narrow-band panel is not
penalised for the light it misses, only rewarded for the efficiency the narrow band buys — so the
`Bandwidth` dial may be one-sided. Not touched in this slice; flagged for the solar pass.

### 39.8b 🔒 THE BALANCE RULE — a trade is added by SPLITTING a dial, never by DELETING one (developer, 2026-07-30)

> *"I find that you took some slider options away when you made the fuel vs output efficiency/time. Find a balance.
> Also stm turbine should be stuck at 12 yrs."*

**Both halves are corrections, and the second one saved a mechanic.** Taken together they are the sharpest statement
yet of what "fixing a dial" is allowed to cost.

**The failure being called out.** Adding `Output vs Economy` (§39.7a) gave the reactor a real fuel-per-kilowatt trade —
but in the driveable reference I let the reactor's proposed output↔endurance fix *consume* the `Lifetime` slider,
so turning the fix on **left one fewer thing to set than before.** That is a net loss of agency dressed up as a
fix: the player traded a dial for a trade. **Wrong direction.**

🔒 **THE RULE.** *A trade is added by SPLITTING a dial's meaning, never by deleting a dial.* If a proposed fix reduces
the count of things a player can set, it is not a fix — **it is a feature removal with a good excuse.** Check the
count before and after; it may rise, it must not fall.

**Applied:** the reactor now carries **three live dials, all of them shipping** — `Mass` (→ output) · `Lifetime`
(→ the fuel load it is charged for, now in real seconds) · `Output vs Economy` (→ the burn rate). The proposed P1 law
sits *on top* of those three, and takes none of them away.

**And the second half — the turbine is STUCK at 12 years, by design.** Ruled in §39.3. The point worth carrying to
every other door: **a wrong name is not evidence of a wrong mechanic.** The honest move was to rename the dial
(`Output vs Efficency` → `Core vs Generator`) and leave what it does alone. Re-purposing it into an output↔endurance
split — the "consistent" fix — would have deleted the turbine's whole character (a plant you fuel once a decade)
**to satisfy a naming complaint**, and it would have collided head-on with the rule above. 🔒 **Rename before you
re-purpose.**

## 40. STEP 2 — THE DOOR: three answers, and they are exclusive per part

| Answer | Job | Its sliders | State |
|---|---|---|---|
| **Generate** — reactor | burn fissile | size · **fuel load** · **output ↔ economy** | ⚠ three live dials; output still linear in mass (§39.1, slice P1) |
| **Generate** — RTG | decay isotopes | size · **output ↔ endurance** | ✅ **the reference law**, already built (§39.1) |
| **Generate** — turbine | burn fissile, big | size · **core ↔ generator** | ✅ honest — a materials trade, fixed 12.7-yr run by design (§39.3) |
| **Collect** | absorb starlight | area · band centre · bandwidth | ✅ honest — and it is the sensor's trade (§39.5) |
| **Store** | hold it | size | ⚠ linear, and reactors already add storage by accident (§39.4) |

🔑 **The slider SET changes with the generator, and that is the point** — the three plants are not three flavours of
one plant (§39.8). Each keeps the dials its own physics gives it, which is also what §39.8b's balance rule requires.

**Exclusive per part** (§1a: a part burns, collects or holds — never two), and the whole door is **one question: where
does the energy come from, and how long do you want it to last?**

## 41. STEP 5 — DOES IT REPRODUCE WHAT EXISTS? Yes, and it tightens two of them.

| Component today | Answer | Under the derived door |
|---|---|---|
| Reactor | Generate | size + **fuel load** + **output ↔ economy** *(three live dials; still wants the RTG's law on mass)* |
| RTG | Generate | **already exactly this** — the reference implementation, and now zero-crew |
| Steam Turbine | Generate | size + **core ↔ generator** — *unchanged, RENAMED* *(the fixed 12.7-yr run is the design)* |
| Battery Bank | Store | size — unchanged |
| Solar Array | Collect | area + band centre + bandwidth — unchanged, and honest |

**Nothing needs inventing and — per §39.8b — nothing gets deleted.** One of the three generators still wants a law the
second already runs, and the third was ruled correct as built.

## 42. STEP 6 — WHAT GOES OUT, EVERY ROW MARKED

| # | What leaves | Goes to | Play state | **What it changes about how the game is PLAYED** |
|---|---|---|---|---|
| 1 | **Stored energy** | `WarpMoveCommand:258` | ✅ LIVE | 🔴 **A flat battery is a ship that cannot leave.** Power generation and FTL reach are one decision, and the player is never told. |
| 2 | **Stored energy** | `MilitaryReach:156` | ✅ LIVE | **The AI will not plan an attack it cannot power** — so your enemy's generator sizing decides when it comes for you. |
| 3 | **Sustained output** | `WarpMoveProcessor:246` (per second) | ✅ LIVE | **How far one charge carries you.** Output sets the crossing you can afford, not just the one you can start. |
| 4 | **Sustained output** | `WeaponSupply` → `GroundUnitAssembly` | ✅ LIVE | 🔒 **The ground supply gate** — *a Titan can carry a laser, infantry cannot.* The clearest place in the game where Power decides what a unit may even be. |
| 5 | **Output, as SIGNATURE** | `SensorSignatureAtb(1700 K, output × 0.1 × mass)` | ✅ LIVE | 🔴 **The reactor is the loudest thing on a ship** — 100× a thruster plume (§34.5). **Your generator sizing is your stealth**, and nothing in either door says so. |
| 6 | **Solar output** | band-matched absorption | ✅ LIVE | **Orbit and star colour matter** — and the S0 ruling will change this number (§39.5). |
| 7 | **Colony power supply** | `SustenanceProcessor:51` → morale | 🔵 **HALF** | 🔴 **Unreachable — no generator mounts on a colony (§39.6).** The consumer is built; the producer cannot be constructed. |
| 8 | **`Lifetime`** | `LocalFuel` | ✅ LIVE **but free** | **Nothing, because it costs nothing.** Priced (§39.1) it becomes the endurance half of the door's only trade. |
| 9 | **Turbine `Output vs Efficency`** | `GeneratorOutput` only | 🔵 **HALF** | 🔴 **A dial named after a trade that does not exist** — fuel duration is a constant 4×10⁸ s regardless. |
| 10 | **Reactor-as-battery** | `EnergyStoreMax += PowerOutputMax` | 🔵 **HALF** | **A hidden dimensional term** (kW into a kJ store) that any storage balance pass will fight. |
| 11 | **output ↔ endurance** | would gate warp reach *and* ground weapons | 🔴 PROPOSED | **The door's whole decision.** A warship runs hot for its bubble and its beams; a picket sips and sits for a decade. Two fleets, one dial. |
| 12 | **The generator destroyed** | Damage → `ReCalcAbilities` | ✅ LIVE | 🔑 **Shoot the reactor out and the ship is stranded** — it cannot warp, and its energy weapons go quiet. The grave rung, and it is already real. |

**7 LIVE · 4 HALF · 1 PROPOSED · 0 NEW.** Same pattern as Sensors: the outputs are load-bearing and the dials are not.

## 43. SCORING THE §30 PREDICTION — the diagnosis right, the specifics wrong

§30 predicted: *"Healthy but **under-dialled** — likely one honest trade (**output ↔ storage**, or output ↔ mass) and
little else. May turn out to need *adding*, like Propulsion."*

- ✅ **"Under-dialled" and "needs adding, like Propulsion": CONFIRMED**, and strongly — the reactor is `50 × Mass` and
  nothing else.
- ❌ **The guessed trade was wrong.** Not output ↔ storage. **Output ↔ endurance** — and I did not predict that it
  would **already be implemented on one of the three generators.**
- ❌ **"And little else" was wrong.** Six findings, including a colony that can never build a power plant while the
  code that consumes colony power is already written.

🔒 **And the §36 lesson held on its first test.** It said *look hardest at the oldest attribute*. `EnergyGenerationAtb`
and `EnergyStoreAtb` are original, un-annotated, convention-free files — and they hold §39.1, §39.2 and §39.4. The
newest thing in the door (the solar/sensor band sharing) is the only part that is already honest.

## 44. THE CHEAP WINS, ranked (not built)

| # | Slice | Why it is cheap | What it changes about PLAY |
|---|---|---|---|
| ~~**P0**~~ | ✅ **BUILT 2026-07-30 (§39.7a)** — the gate, the unit fix, and the burn-rate dial as one slice. `EnableFuelExhaustion` (default off) · `Lifetime` reaches the atb in **seconds** at last (the reactor's real endurance was **2.43 hours**, the RTG's **5 seconds** — only the turbine was correct, and it is the only one with a shipped design) · **`Output vs Economy`** makes the burn rate settable, costing fuel-per-kilowatt so 2× power = 4× fuel · two buildable reactor designs · fuel rate anchored on the turbine's measured 3.125e-11 kg/s per kW (the old `1e-7` made a year of fuel weigh 236 t). Gauge `PowerFuelGateTests`. | ✅ **Endurance means something now**, and the burn rate is a real decision. |
| **P1** | **Give the reactor and the turbine the RTG's law** — `output × lifetime = const × size` | the formula already exists on a shipped template; it is a copy, not a design | 🔴 **The door gets its decision** — warship-hot vs outpost-frugal. **Order: after or with P0.** |
| **P1b** | **Make carried fuel weigh something** (§39.2) — the RTG already does it (`Fuel = Mass × 0.5`) | one formula per template | **Closes the one channel where `Lifetime` really is free.** Mass is what Chassis gates on, so this is the half that matters for ship design. |
| ~~**P2**~~ | ✅ **DONE 2026-07-30 — RENAMED, not replaced (§39.3).** The developer ruled the turbine's ~12.7-year fuel duration **intended**, and reading the `ResourceCost` block showed the dial was never empty: it is a **materials trade** (core-heavy → fissile + graphite + titanium; generator-heavy → tungsten + nickel), with output rising proportionally to the fissile bill. Only the *name* was wrong. Now **`Core vs Generator`**, with the fixed duration stated in its description. | ✅ **The dial finally says what it does** — and no slider was removed or re-purposed. |
| ~~**P3**~~ | ✅ **DONE 2026-07-30 (§39.8③)** — the solar array now mounts `ShipComponent, ShipCargo, PlanetInstallation, Station`. | ✅ **A colony and a station can build a power plant**, and `SustenanceProcessor`'s waiting consumer finally has a producer. |
| ~~**P4**~~ | ✅ **DONE 2026-07-30** — `MountType: 1` replaced with named flags in the same change (it worked only by numeric coincidence with `ShipComponent`). | Landmine removed. |
| **P5** | **Decide the reactor-as-battery term** (§39.4) — deliberate buffer, or remove | one line | Makes storage balanceable instead of secretly pre-loaded. |
| **P6** | **Publish output ↔ signature** as a readout (row 5 of §42) | Failure-A: the number exists, unwired | 🔴 **Tells the player their generator is their stealth.** The single biggest unstated coupling in the game. |

**One ruling is now needed** (it was none before the developer's question): **P0 needs a call on reactor lifetime and
refuelling** — wiring the gate at the shipped 8760 hours would strand the starting fleet inside a game year. Every
other slice above is decidable from the derivation. ⚠ But **S0 (Sensors) now touches
this door** (§39.5), so the band ruling should be made before P6 is calibrated.


---

# PART SEVEN — THE REMAINING SIX DOORS, DERIVED 2026-07-30

Doors 3–8 (Chassis · Logistical · Command · Enhancers · Industrial · Civic), each derived by the §1 method and each
with a driveable reference page. **This part exists so the findings survive the session that produced them** — but
see §51 first: the whole part is replaceable by one test.

## 45. Door 3 — CHASSIS: 🔒 REDERIVED to the developer's ruling, 2026-07-30

> *"Chassis will be the thing the assembler calls first since chassis sets the budget of the entity. So chassis must
> have a generalised category for whatever you intend to build but no restrictions — if I want a kaiju I should select
> planetary (stop using ground) unit then put infantry. It is based on the chassis that will set the requirements on
> what the assemblers will flag as 'this unit needs a reactor'. But it should also have specifics like what if this
> building is organic or the infantry unit is mechanical."*

**The ruling reorganises the door.** A chassis is not a budget plate — it is the **declaration the assembler reads
first**: *what kind of thing is this, what is it made of, and therefore what does it need to be legal?*

### 45.1 🔒 REDERIVED AGAIN, same day, on three further rulings

> *"The domains should be the ENVIRONMENT the entity is going to function in, then gets more specific picking
> UNIT or INFRASTRUCTURE, then substrate. This is a sci-fi game that should cover anything and everything so
> substrate should be EXPANSIVE and allow for distinct options. And hulls can be ANY SIZE — a Death Star might be
> an average station to one faction but an outpost to another."*
> *"Also aircraft can be a thing, and submarines, and both of which can function at different LEVELS and different
> complexities."*

**🔑 The first ruling is a SIMPLIFICATION, not an addition — and it explains a loose end the previous pass could not.**
The old four-valued "domain" was **two questions fused into one**. Separate them and the four shipped chassis are the
cells of a grid:

| Environment ↓ / is it a… | **UNIT** (mobile) | **INFRASTRUCTURE** (a place) |
|---|---|---|
| **Orbital / vacuum** | ✅ `ship-hull` | ✅ `station-chassis` |
| **Planetary surface** | ✅ the four frames | ✅ `building-foundation` |
| **Open water** | 🔴 empty — **and the map already has ocean regions** | 🔴 empty |
| **Atmospheric** | 🔴 empty (though `Hover` exists) | 🔴 empty |
| **Subterranean** | 🔴 empty | 🔴 empty |

**And it dissolves the "four currencies or one?" question.** The previous pass found *structure* and *footprint* are
the same currency under two names (both assemblers sum `VolumePerUnit`) and could not say why. **Now it is obvious:
they are both INFRASTRUCTURE, and infrastructure budgets in volume.** Currency is a function of **kind**, not of
environment: unit → mass or carry-strength (what it can lift); infrastructure → volume (what will fit inside).

🔑 **The kind split also hands the Civic door its missing producer.** A unit has **crew that travel with it**; a place
has **JOBS the local workforce fills**. Civic found the morale model's employment term — its widest factor, ±40 points
— **permanently zero because nothing declares a job**. *"Declares jobs" is what INFRASTRUCTURE means.* One change,
two doors.

### 45.1a The door — 4 choices + 3 sliders

| # | Choice | What it sets | Today |
|---|---|---|---|
| ① | **ENVIRONMENT** — orbital · surface · **open water** · **atmospheric** · **subterranean** | what will **kill** it — and it owns the tolerance/resistance machinery | ✅ 2 exist · ⚠ water is HALF (ocean terrain + the `Amphibious` gate exist, no chassis) · 🔴 air is THIN (`Hover` only) · 🔴 subterranean nothing |
| ② | **LEVEL / BAND** — e.g. water: Surface→Shallow→Deep→Abyssal; air: Terrain-following→…→Near-space | which **band** of that environment, and therefore how harsh | 🔴 only `infrastructure` has band dials at all |
| ③ | **UNIT \| INFRASTRUCTURE** | budget **currency** · **mount** · crew-vs-jobs · capturable · grave rung | ✅ implied by the four cells |
| ④ | **SUBSTRATE** — 8 values (below) | 🔑 **generates the requirement set** | 🔴 **NEW** — grepped `organic`/`biomass`/substrate: **zero hits** |
| slider | **Frame size** | the budget, via `efficiency × √size`. **Enormous range — no classes.** | 🔴 mass is a **constant per template** |
| slider | **Structural efficiency** | the `1128`, on the **research** tree | 🔴 new (research is `"0"` on every chassis) |
| slider | **Operating envelope** | how many **bands** it spans — narrow+cheap or broad+expensive | ⚠ **the dial shape EXISTS** (below) |

### 45.1b 🔑 "Different levels" — the envelope dial is ALREADY BUILT, in exactly the right shape

`ComponentTemplatePropertyBlueprint` supports `GuiHint.GuiSelectionMinMaxRange` with **two companion fields**:
* **`PairedPropertyName`** — *"the Name of the partner property (the upper bound when this property is the lower bound)"*
* **`MaxRangeFormula`** — *"maximum allowed gap between this property's value and its partner's value
  (e.g. `TechData('tech-infra-gravity-range') * 2 * 9.81`)"*

**A band with a floor, a ceiling, and a TECH-DRIVEN CAP on how wide it may be** — including the part that stops
"works everywhere" being free. 🔴 **Used on exactly ONE template:** `infrastructure`'s **gravity band** (8.8→10.8 m/s²)
and **pressure band** (0.9→1.1 atm), widened by **six real technologies** (`tech-infra-{gravity,pressure}-{range,
min-extension,max-extension}`). And `infrastructure`'s `Mass` is the constant `1000`, **so widening the envelope costs
nothing** — the Industrial door's finding arriving from a different direction.

🔑 **So "different levels and different complexities" needs almost nothing built.** *Levels* = the band, on a dial that
exists. *Complexity* = the size slider plus what you mount — which is why **a crop-duster and a strike fighter are the
same chassis at different settings**. What must be ADDED: bands for the four environments without them, an envelope
cost, and the two missing environments. 🔑 **And one payoff worth naming: the top of the atmospheric stack borders
orbital, so a chassis spanning near-space→low-orbit IS a spaceplane** — single-stage-to-orbit becomes a wide band
rather than a new mechanic.

### 45.1b′ ⚠ CORRECTION — the ORBITAL bands were the wrong axis (developer's question, 2026-07-30)

**The question:** *"for the low or high orbits and deep space gameplay wise when will that come into picture?"*

**The answer is that they don't, and it was my own bug.** I wrote the orbital stack as *Low → High → Deep space*
without checking whether anything reads it. **Nothing about a hull changes between low orbit and high orbit** — there is
no gravity-well rule for going to warp, no altitude-dependent hazard, no structural difference. By the rule this
campaign has applied to seven other doors, that made it **a dial writing nothing**: the exact bug being flagged
everywhere else, in the derivation itself.

**And altitude is not merely unread — it is already priced, on the other side of the ledger.** `OrbitMath.LowOrbitRadius`
is *one fixed altitude* (planet radius × 1.1) that ships park at — a destination, not a design band — and
**`OrbitMath.FuelCostToOrbit` already charges real Tsiolkovsky fuel to reach a higher one.** So altitude costs **fuel on
the launch**: the **Logistical** door's bill, not the Chassis door's. Charging for it here too would have double-counted
a cost the engine already collects.

🔑 **The axis that DOES belong to the chassis is distance from the STAR**, because that is what decides what a hull must
survive and whether its power source works at all — and it has **three consumers already running**:

| What already reads distance-from-star | Where | What it means for a chassis |
|---|---|---|
| Every star gets a **permanent corona** — heat damage following radiative flux (∝ 1/dist²) from the star's surface outward, countered by a **`HazardResistanceAtb` component** | `StarSystemFactory.cs:62` → `SpaceHazardFactory.CreateStarCorona` | ✅ **A real requirement close in** — an inner-system hull carries heat resistance or it cooks. Cradle-to-grave already: research → build → install → lose it. |
| **Solar output attenuates with distance** (`AttenuatedForDistanceList`) | `EnergySolarGenProcessor.cs:60` | ✅ **Where you operate forces your power choice** — panels past the habitable band make nothing, so you carry a reactor and its fuel. |
| **A body's base temperature** from its star and orbit | `SystemBodyFactory.cs:145` | The same axis the surface environment already reads. |

**New bands: `Inner (hot)` → `Habitable` → `Outer (cold)` → `Deep space`**, defaulting to Habitable (where every shipped
hull lives, so nothing shipped pays anything).

🔒 **And orbital turns out to be the ASYMMETRIC one, which is the finding worth keeping.** Only the **hot** end taxes the
**frame** — `BANDCOST.orbital = [1.45, 1.0, 1.0, 1.0]`. Cold and dark are **structurally free** (vacuum insulates), so
their cost lands on **power and endurance** instead. **Same axis, two different doors.** Inventing a frame multiplier for
the cold bands to make the table look symmetrical would have been the dial-writing-nothing bug a second time. **A band
whose cost lands in another door is recorded as free here, and the readout says where the bill actually goes.**

⚠ **The general lesson, because it will recur in the five remaining doors:** *an axis can be real, already modelled, and
still belong to a different door.* "Is it read?" is not the whole test — **"is it read *by the thing this door
prices*?"** is. Altitude passes the first and fails the second.

**Also fixed in the same pass** (found while re-verifying the page): the size slider spans sixteen orders of magnitude in
100 integer steps, so it moves ~1.45× per step and **the 500 kg light hull landed at 437 kg** — a size nothing in the
game actually is. It now snaps to **every real shipped chassis** (light/medium/heavy hull · station · swarm/human/walker/
vehicle frame · building) at that preset's own step, so each shipped design reads as itself. Verified: 9,216 configurations,
0 errors, all six shipped chassis exact, and every environment's default band charges nothing.

### 45.1c 🔒 SUBSTRATE, expansive — eight values, each winning an axis outright

Constraint carried from the Power door, because it is what stops "expansive" becoming "cluttered": **every option
behind a door must win at least one axis outright.**

| Substrate | Power | Feeding | Who runs it | Wins OUTRIGHT | Hooks |
|---|---|---|---|---|---|
| **Mechanical** | reactor | — | crew | *the baseline* | `ManpowerTools` ✅ |
| **Organic** | — metabolises | **biomass** | itself | **the only one that EATS** — and the only one **grown** | `SustenanceProcessor` ✅ |
| **Synthetic** | reactor | — | nobody | **a machine with no occupants** — needs no air ever | `WeaponSupply` ✅ |
| **Cybernetic** | reactor | partial | reduced crew | **the only one needing power AND food** | both ✅ |
| **Crystalline** | — stores charge | — | nobody | **needs NOTHING to sit there** — the derelict, the monolith | — |
| **Nanite** | continuous | — | nobody | **heals without a yard**; reconfigurable after build | `GroundConstructorAtb` ✅ |
| **Energy-bound** | 🔴 **or it CEASES** | — | nobody | **indestructible while powered, gone when not** | the fuel gate wired this week ✅ |
| **Psionic** | — | — | **a seated leader** | **the only one needing a PERSON, not a crew** | `CommandBerthAtb` ✅ |

✅ **Seven of the eight distinguishing requirements hook a system that is already live.** 🔴 **Genuinely new: a biomass
upkeep, and "grown" as an industry type beside "manufactured".**
⚠ **Two candidates HELD BACK, and this is the rule working:** *exotic/other-dimensional* — every profile I could write
duplicates Crystalline or Energy-bound, so it has **no distinct requirement**; *ancient/precursor* — a **provenance**,
not a substrate, and it belongs to the Site Engine as something you *find*. 🔒 **Expansive means "as many as win an
axis", not "as many as we can name."**

### 45.1d 🔒 "Any size" — no classes, relative naming, and the exponent ruling

**Consequence 1 — absolute size classes are gone.** No "Light hull" as a *thing*: a continuous size slider, and a name
whose scale word is computed **relative to what your faction typically builds**. The three shipped hulls become
**presets, not tiers**.

**Consequence 2 — the budget EXPONENT becomes a real ruling.** The shipped hulls follow `budget = 1128 × size^0.5`
(light + heavy to 0.9%) — **fitted across a 50× range**, now asked to hold across fifteen orders of magnitude:

| Exponent | Payload fraction at 10¹² kg | Ladder test | Fits the shipped hulls? |
|---|---|---|---|
| 1.0 linear | **900%** — nonsense at scale | ✅ flat | 🔴 no (would make all three ×9) |
| 2/3 (true square-cube) | 1.9% | ✅ falls | partly |
| **0.5 (√ — what ships)** | 0.09% | ✅ falls | ✅ **light + heavy to 0.9%** |

✅ **THE ANSWER: keep √, and make structural efficiency a research axis with an enormous range.** Then a faction's
**ceiling ≈ 10⁴ × efficiency²**, and **what it typically builds scales as efficiency² too** — so capability and
practice move together. At the shipped 1,128 the ceiling is ~1.3 × 10¹⁰ kg (a large asteroid station); a genuine
planet-killer needs roughly **10⁹** — about a **million times** the shipped figure, which is a tech tree, not a slider
nudge. 🔑 **So "an average station to one faction, an outpost to another" is not a special case to code — it IS the
materials-science gap between them.** Verified in the driveable page: the *same* 10 Mt orbital frame reads
**"Colossal, past your ceiling"** at shipped tech, **"the ordinary size"** at ~250× efficiency, and **"Trivial"** at
~60,000×, without one thing about the frame changing.
⚠ **Flagged:** this extrapolates a law measured over 50× across fifteen orders of magnitude. It **fails in the safe
direction** (megastructures too hard, never free) and the research dial buys them back at a pace the developer sets —
**an exponent failing the other way would need a cap, and a cap is what the ruling forbids.**

**The kaiju, worked:** *planetary* (currency = carry-strength, mount = `GroundUnit`) + *infantry* (`Locomotion = Foot`,
`CarryClass = Personnel`) + *organic* (biomass not reactor, no crew) + *size 80,000 kg* (budget ≈ 319,000 via the law).
**Four values on one attribute — no monster subsystem.** Which is what the frame's own source already claims: *"It is
NOT a rigid class … a Guardsman, a Space Marine, a mech and a walking cathedral are all just this attribute with
different values."* **The intent was written down; the size dial and the substrate were the two missing pieces.**

### 45.2 🔴 THE FINDING THE REDERIVATION SURFACED — the rules are right, nothing declares them

Every assembler already takes the chassis as its **first argument** and returns early without one. That sequencing is
correct. What is thin is what the chassis *says* — so each assembler contains the rules its author happened to write:

| Assembler | Gates it hard-codes |
|---|---|
| `GroundUnitAssembly.Compute` | **four** — carry budget · per-item cap (`capacity × 0.5`) · **power supply** · **ammo magazine** |
| `StationAssembly.Compute` · `BuildingAssembly.Compute` | **one each** — volume budget |
| `ShipDesign.Recalculate` | **one** — mass budget |

🔴 **So a planetary unit with an energy weapon and no reactor is REFUSED, and a SHIP with the same weapon and no
reactor is ACCEPTED.** Same rule, same weapon, one domain. **Not a decision — an absence.**

✅ **And the fix is a call-site change, because the checks are already domain-neutral.**
`WeaponSupply.PowerDraw_W(ComponentDesign)` · `ReactorOutput_W` · `MagazineCapacity_kg` · `DrawsAmmo` all take a plain
`ComponentDesign` and care nothing about where it is mounted. **They sit in `GroundCombat/` and are called by exactly
one assembler.** Add a **requirement set** as a fourth member of `IChassisAtb` and the ship inherits both gates.

### 45.3 The requirement set, generated from DOMAIN × SUBSTRATE

| Requirement | Mechanical | Organic | Synthetic | The check today |
|---|---|---|---|---|
| Energy weapons need power | ✅ a reactor | 🔑 **metabolism** | ✅ a reactor | ✅ **built + domain-neutral**, called once |
| Ammo weapons need a magazine | ✅ | ✅ | ✅ | ✅ **built + domain-neutral**, called once |
| Needs crew | ⚠ yes | no — it IS the crew | no | 🔴 **and all four planetary frames author `CrewReq: "0"`** — the rule has *no input* on the domain where it matters most |
| Needs feeding | no | 🔴 **biomass upkeep** | no | 🔴 new — but the food/sustenance chain it hangs off is **live and tested** |
| Environmental seal | ⚠ on a hostile world | ⚠ same | ✅ immune | ✅ built as a **stat**, never a gate |
| Repaired vs regrown | materials | 🔑 regenerates | materials | 🔴 no repair model either way |

🔑 **Substrate applies to all four domains, which is what earns it an axis:** an *organic building* is grown and needs
feeding and no work crew; an *organic ship* heals between battles and carries no complement; a *mechanical infantry*
unit is a battle droid — no crew, needs a reactor, never needs air. **Two choices, one table, and a wide slice of what
the north star asks for becomes expressible with no bespoke mechanic.**
🔑 **And the data was already reaching for it:** `swarm-frame`'s shipped description reads *"a tiny, cheap, fragile
**organism**/drone frame."* **Organism or drone — the author could not say which, because there was no field for it**,
and the two need opposite things. ⚠ **Three values, not four:** an *energy/exotic* substrate (needs neither power nor
food) has **no consumer today**, so it would be a dial writing nothing. Ship three; add the fourth when something reads it.

### 45.4 The budget is still free, and the law is still already in the data

*(unchanged from the first derivation — and it is what the size slider plugs into)*

🔴 **THE HEADLINE, and it cancels the other seven doors.** Each chassis exists to sell **a budget**, and on all four
the budget appears in **none of the seven cost channels**:

| Chassis | Budget dial | Range | `Mass` formula | Costs? |
|---|---|---|---|---|
| `ship-hull` | `Mass Budget` | 1,000 → **100,000,000** kg | `PropertyValue('Hull Mass')` | 🔴 no |
| `station-chassis` | `Structural Budget` | 500 → 10,000 | `100000` — a **constant** | 🔴 no |
| `building-foundation` | `Footprint Budget` | 500 → 10,000 | `50000` — a **constant** | 🔴 no |
| ground frames ×4 | `BaseStrength` | 1 → 1,000 (human) | `20` / `4000` / `2500` / `5` — **constants** | 🔴 no |

**The wall is real and switched ON:** `ShipDesign.cs:285-290` computes `MassBudget` → `OverMassBudget` → `IsValid =
false` when `EnforceMassBudget`, and `PulsarMainWindow.cs:144` sets that flag `true`. **49 hull references** across
`shipDesigns.json` — ships mount hulls. So a hull authored `Hull Mass: 500` + `Mass Budget: 100000000` legally carries
a hundred thousand tonnes. **Every "and it costs mass" fix this campaign shipped is enforced against a free ceiling.**

⚠ **And the budget ADDS ACROSS HULLS** — `ShipDesign.cs:281` is `hullBudget += hull.MassBudget * component.count` with
no "exactly one hull" rule. Two light hulls (1,000 kg of frame) grant 50,000 kg where one medium (10,000 kg) grants
90,000. **Stacking cheap frames beats buying the right one, with no dial-fiddling.** The other three chassis are
structurally immune (their assemblers take exactly one frame).

🔑 **AND THE LAW IS ALREADY IN THE DATA, for the second door running.** The three shipped hull tiers:

| Hull | Hull Mass | Mass Budget | budget ÷ √mass | √-law prediction |
|---|---|---|---|---|
| Light | 500 | 25,000 | **1,118** | 25,222 — **0.9% off** |
| Medium | 10,000 | 90,000 | 900 | 112,800 — 25% high |
| Heavy | 25,000 | 180,000 | **1,138** | 178,383 — **0.9% off** |

**`budget = 1128 × √(frame mass)`**, fitted to nothing — light and heavy already sit on it to within 1%. And it is
right for a *reason*: a square root means doubling capacity costs **4× the frame**, which is the square-cube law every
real structure obeys, and it explains why the ratio *falls* with size (×50 → ×9 → ×7.2). **Eighth place one law does
the work** (thrust↔ve · gearbox · bubble · jump range · gate cycle · solar bandwidth · RTG output↔endurance · this).

**Other findings:** `BaseHP` is a free **10× toughness** multiplier on every ground frame (read straight into
`GroundUnitAssembly` as `r.HitPoints`, frame mass constant) · `Size` on a ground frame is **dead AND free** (its own
source says it "feeds transport carry-size"; **zero readers**) · `TileFootprint` 1–40 is a **cost with no benefit**
(minimum dominant) · **research is `"0"` on every chassis in the game** · a lost hull sheds no budget (`MassBudget` is
design-time).

🔑 **Four currencies or one?** (the build plan's question). **Three, and two are the same.** `StationAssembly.cs:78`
and `BuildingAssembly.cs:52` both sum **`d.VolumePerUnit * count`** — identical computation, identical units, two
names, two same-ranged dials. Merging them removes a currency and changes no arithmetic. Ground's carry-strength earns
its own currency for a real reason: **it is the only budget another component can ADD to** (power armour's
`StrengthBonus`).

⚠ **And the engine advises pulling the free lever:** `BuildingAssembly.cs:75` — *"over footprint budget: 3200 / 2000 —
**raise the foundation Footprint Budget** or drop a module."* (The station assembler says the same about structure;
`GroundUnitAssembly.cs:226` says *"Add an augment (e.g. power armour)"*.) 🔒 **Three helpful messages, three free
levers — a good error message is evidence about what its author expected to be costly.**

**⚠ DEVELOPER RULING NEEDED:** forcing the √-law moves the **medium** hull's budget 90,000 → 112,800 (+25%), a live
change to every medium-hulled ship. Three options, each byte-identical for something: anchor on light+heavy (medium
gains headroom) · anchor on medium (light and heavy tighten ~20%) · keep all three authored and apply the law only to
new designs. **Which hull do you consider correctly tuned?**

### 45.5 Build order — and one HARD pairing

| Slice | What | Byte-identical? |
|---|---|---|
| **C1** | **The chassis declares its requirements.** Add the requirement set to `IChassisAtb`; all four assemblers read it instead of hard-coding. | ✅ **yes**, if each chassis declares exactly what its assembler already checks — and **the ship immediately inherits the power + ammo gates** |
| **C2** | **Substrate.** One enum + one requirement table. Default every template to `Mechanical`. | ✅ **yes**. Then flip `swarm-frame` to `Organic`, which its own description implies |
| **C3 + C4** | **The size dial AND the transport wire, TOGETHER.** A frame-size slider with the √-law budget, *and* `CarrySizeOf` reading the frame's `Size` instead of the hard-coded type table. | ⚠ C3 alone is **unsafe** |
| **C5** | Price the budget (the √ law) + put **structural efficiency** on the research tree. | ⚠ see the medium-hull note |
| **C6** | **Bands + the operating envelope.** The dial shape already exists (`GuiSelectionMinMaxRange` + `PairedPropertyName` + `MaxRangeFormula`); give the other environments bands and make envelope WIDTH cost frame. | ✅ yes with width 1 |
| **C7** | **The open-water cell** — the cheapest new environment, because its terrain (`RegionFeatureType` ocean) and its gate (`HexPathfinder` + `Amphibious`) already exist and **no chassis claims it**. | ✅ additive |

⚠ **The pairing is the one sequencing constraint in this door.** `GroundTransport.CarrySizeOf` reads a hard-coded
three-value table off the unit's **type enum** (Infantry 1 / Artillery 2 / Armor 3) and ignores the frame entirely —
so **shipping C3 without C4 means one troop bay hauls six kaiju.** "No restrictions" has to keep its consequences.
🔑 And the dial that fixes it is the **dead `Size` dial** from this door: *documented as "feeds transport carry-size",
zero readers.* **Both halves were built; nobody connected them.**

### 45.6 Rulings — CLOSED

Both questions the first rederivation left open were answered by the developer's rulings above (**expansive substrate**
⇒ eight, each winning an axis; **any size** ⇒ no classes, √ law + efficiency research). **One judgement remains inside
the door:** forcing the √-law moves the **medium** hull's budget 90,000 → 112,800 (+25%).
**Recommendation, given "any size":** treat the three shipped hulls as **presets** and let the law govern, accepting
the medium's +25% as a one-time calibration — **because under the ruling they stop being tiers anyway, and keeping a
bespoke exception for one preset is exactly the kind of restriction the ruling removes.**

⚠ **And one honesty note carried into the page:** the anchors for the three new environments (water / air /
subterranean) are **invented and labelled as such** — there is no shipped submarine to anchor on. The orbital and
surface anchors are the real shipped chassis.

### 45.7 🔒 The naming ruling — PLANETARY, not "ground"

Applied to the design vocabulary and to every page and doc from here on. **Not applied to class names**: the code says
`Ground*` in ~40 places (`GroundChassisAtb`, `GroundUnitAssembly`, `GroundLocomotion`, `ComponentMountType.GroundUnit`),
and `TypeNameHandling.Objects` embeds C# type names in every save — renaming a `*DB`/`*Atb` **breaks every existing
save** without a converter (gotcha 7 / landmine L3). **It is a real slice with a real migration and deserves its own
commit, not a rider on a design change.**

## 46. Door 4 — LOGISTICAL: thirteen templates, two attributes, and the worst-maintained door

**The collapse:** every one of the thirteen is `CargoStorageAtb(storeTypeID, maxVolume)` — *a store* — or
`CargoTransferAtb(rate, range)` — *a mover* — or both. Two attributes, four fields. **One choice (what it holds) +
two sliders (how much · rate ↔ range)** reproduces all thirteen. Ammo, troops and fuel are **cargo types with their
own consumer**, not separate systems.

🔴 **F1 — `spaceport` is defined TWICE and silently merged.** `storage.json` and `installations.json` both declare it;
`storage.json` loads second, and `ModLoader.ApplyModGeneric` takes the `Default` branch on a duplicate id and reflects
over every non-null property — **including the whole `Properties` list** (no `CollectionOperation` given). So the
`installations.json` planetary complex's **`Warehouse Size` and its `DBargsStorage` attribute are gone**: every
start's spaceport (`default-design-spaceport`, unlocked on Earth · Kithrin · UMF · devtest) can move cargo and **holds
nothing**. ⚠ **`BaseModIntegrityTests` cannot see it — a merge is not a skip:** nothing lands in `SkippedEntries`, the
template count is unchanged, and the result is a valid blueprint. **Swept all 300 template ids: 7 collisions**
(`spaceport` · `stainless-steel` · `water` · `ground-balanced` / `-offensive` / `-defensive` · `hydrogen-sulphide`
twice in one file).

🔴 **F2 — two fuel tanks; keep one, delete one.** `stainless-steel-fuel-tank` is the **best-authored template read in
eight doors**: you set the volume, it solves the radius, then masses the **4 mm steel shell** —
`1.333π(r³ − (r−0.004)³) × 8000` — so a 2,500 m³ tank is 8.42 m radius and 28.5 t dry, crew 0, four shipped designs.
`fuel-cargo-hold` sets **`CrewReq = PropertyValue('Tank Volume')` = 65,449,847,000 crew** at its default, masses
`Tank Radius` (2,500 kg for 6.5×10¹⁰ m³), has no shipped design, and **is unlocked on Earth.** 🔴 Third crew bug in
one door: `space-port` has `CrewReq: "1000000"` hard-coded.

🔴 **F3 — negative storage.** `ordnance-cargo-hold` (and the eaten spaceport): `Total Cargo Stored = Rack Size −
Size Efficiency − **Cargo Transfer Rate**` — a rate subtracted from a volume. At the template defaults that is
**−500 m³**, and `CargoStorageAtb.OnComponentInstallation` does `MaxVolume += MaxVolume` **unclamped**, so it
*removes* storage from the host. The shipped design escapes only because its rate happens to be 100.

⚠ **F4 — `Rate vs Range` fails the ladder test backwards.** rate is `base + base×RvR×0.1` (additive, saturates),
range is `base − base×RvR×0.1` (subtractive, → 0). Measured on the shipped shuttlebay: **rate × range falls 9,281 →
1,781 (−81%) while rate rises only ×1.73.** So `1` strictly dominates. The shipped design sits at `2`, near the good
end — which is why nobody noticed. **Fix: multiply both sides instead of adding, making the product flat.**

🔴 **F5 — `LogiBaseAtb` is the campaign's only fully dead attribute** — every reference is inside its own file; zero
external readers; `logistics-office` isn't even unlocked on Earth. **DEVELOPER RULING: wire it as the cap on a
colony's logistics routes, or delete it.**

🔴 **F6 — `troop-bay` `Mass = 5000` constant** ⇒ `Capacity` 1–60 and `CarryClass` both free. 🔴 **F7 — a cargo hold
weighs 1% of what it holds** (`Mass = Size Efficiency = Volume × 0.01`; the shipped *"Cargo Hold 5t"* masses **50
kg**), and a ship's only wall is a **mass** budget — so **even a correctly-priced hull cannot constrain cargo.** The
good fuel tank shows the fix.

🔑 **F8 — THE CROSS-DOOR WIRE.** `GroundTransport.CarrySizeOf` is a hard-coded switch on the unit **type** enum
(Infantry 1 / Artillery 2 / Armor 3) and **never reads the frame's `Size` dial** — the dead dial from §45. So a Titan
takes the same bay room as a rifleman, while the atb's own docs say *"a size calc … not a fixed slot count."*
**Both halves were built; nobody connected them. One line joins two doors' dead ends.**

✅ **The model to copy:** `ship-magazine` — `Mass = Ammo Capacity × 1.2` with everything downstream of it, and **the
only logistical template with a non-zero `ResearchCost`.** ⚠ Its crew coefficient (`[Mass] × 0.02` = 120 people on
the shipped 5 t magazine) is steep and worth a calibration look.

**Both owed debts land:** fuel load (Propulsion §23.1, on two templates — one unbuildable) and magazines (Weapons +
Propulsion — cleanly, with a real hard gate in `GroundUnitAssembly`). 🔒 **The intrinsic test put both on the
container rather than the consumer, and it held.**

### 46a 🔒 THE DOOR ITSELF — "what is it carrying?" (developer, 2026-07-30)

*"For cargo, you should also include things like food, people (some general term), resources both refined and
unrefined, and also ships/other entities, anything else that would be shipped, stored, or needs to be moved."*

**The mechanism, first, because it decides everything else.** Anything carryable implements `ICargoable`, whose six
fields include **`CargoTypeID`** — the KIND of compartment it needs. A hold declares the same string through
`CargoStorageAtb(storeTypeID, maxVolume)`. `CargoMath.GetFreeVolume` then looks the item's string up in the hold's
`TypeStores` dictionary — **and on a miss returns `0`. No exception, no log.** So "this ship cannot carry that" and
"this ship has no room right now" are the same reading, which is why the gaps below survived: they never announced
themselves.

#### What is ALREADY cargo — eight implementors, and two surprises

| `ICargoable` | What it is | Its class | Carryable today? |
|---|---|---|---|
| `Mineral` | **unrefined resource** | `general-storage` | ✅ |
| `ProcessedMaterial` | **refined resource** | `general` / `fuel` | ✅ |
| `ComponentDesign` · `ComponentInstance` | a part, boxed | `general-storage` | ✅ |
| `OrdnanceDesign` | a missile | `ordnance-storage` | ✅ |
| `CargoAbleTypeDB` | the generic blob-based one | authored | ✅ |
| **`ShipDesign`** | 🔑 **a whole ship** | — | 🔴 **nothing provides a berth for one** |
| **`TeamObject`** (every `Scientist`) | 🔑 **a team of PEOPLE** | `passenger-storage` | 🔴 **nothing provided it — a silent 0** |

🔑 **So "refined and unrefined resources" was already done, and "people" and "ships" were already declared** — the
engine had been reaching for both. `ShipFactory.cs:246` even carries the note
`// TODO: check for additional people on board (passengers, officers, scientists etc)`.

🔴 **Six classes were declared in `cargoTypes.json`; three had no provider at all.** Both cargo-hold templates
hard-code `AtbConstrArgs('general-storage', …)`, so **the compartment's class is not a dial** — the designer could not
build a passenger liner, a cryo ark, or a reefer, and `passenger-storage` / `cryogenic-storage` sat declared and
unbuildable while `TeamObject` asked for one of them every time it was created.

🔴 **And FOOD does not exist as a thing at all.** `SustenanceProcessor.cs:14` says so in its own words: *"food from
the — **not-yet-existing** — food cargo good, so 0 for now."* Food is an installation OUTPUT read off installed
components, so **it is produced and eaten in the same place and can never be shipped** — a colony that cannot grow
food can never be supplied by one that can. That removes the single most classic reason a supply line exists.

#### The rule that keeps the taxonomy honest

**A cargo class exists only if it needs a physically DIFFERENT compartment.** If two things can ride in the same box,
they are the same class. That is what stops this becoming thirty flavours of crate — and it is §39.8 again: each class
must win an axis outright. The axes are the requirements a compartment must meet: **pressure · temperature ·
continuous power · containment · acceleration limit · what failure costs · and whether the contents pour or take a
berth.**

| Class | The requirement that makes it its own compartment | Carries | Wins outright |
|---|---|---|---|
| **General** `general-storage` | none — the baseline | ore, materials, machinery, parts | **cheapest per m³, no power** |
| **Fuel/fluid** `fuel-storage` | sealed pressure vessel, no free surface | fuels, reaction mass, **water**, atmospheric gases | **the only one that holds a liquid or gas** |
| **Ordnance** `ordnance-storage` | blast isolation + handling gear | missiles, shells, ground ammunition | **the only one rated for cargo that detonates** |
| **Passengers** `passenger-storage` | pressure · air · water · **an acceleration limit** · draws power | passengers, **colonists**, a drafted workforce, research teams, prisoners, refugees | **the only one whose contents walk out and go to work** |
| **Cryogenic** `cryogenic-storage` | continuous refrigeration; needs no air or food | colonists at scale, casualties in stasis, seed stock, gene banks, live specimens | **cheapest per PERSON (5× a berth) and needs nobody to tend it** |
| **Refrigerated** `perishable-storage` 🆕 | powered cooling; **contents degrade with TIME** | **food**, biomass, medical stock, anything alive | **the only one whose contents are still worth having on arrival** |
| **Containment** `contained-storage` 🆕 | shielding or a field; **failure damages the CARRIER** | volatiles, radioactives, antimatter, biohazard, xeno specimens | **the only one rated for cargo dangerous to its own ship** |
| **Energy** `battery-storage` | a charge, not a mass — kJ, not m³ | electricity, charged cells | **measured in a different unit entirely** |
| **Berth** 🆕 *(not built)* | a **unit slot**, not a volume — a discrete thing that leaves under its own power | ground units, vehicles, small craft, a docked ship, drones | **the only one whose cargo departs by itself** |

#### The science-fiction cross-check — what the franchises forced

Run against the universes the north star names, as a completeness test on the nine (this is the "what's missing"
critic, not decoration):

| Trade good | Where | Lands in | Verdict |
|---|---|---|---|
| Water / ice hauling | Expanse (Ceres), BSG, Trek | **Fuel/fluid** — and `water` is *already* a `Mineral` | ✅ covered twice over |
| Tylium ore, naquadah, dilithium, eezo | BSG, Stargate, Trek, Mass Effect | **General**, or **Containment** if it is dangerous raw | ✅ and it is why Containment earns its place |
| A refugee civilian fleet | BSG — the entire premise | **Passengers** | ✅ and it is the class that was unbuildable |
| Ark ships / colonist sleepers | Andromeda, Expanse, Stellaris colony ships | **Cryogenic** | ✅ and it is why cryo is priced per person, not per m³ |
| Grain shipment, bacta, medical relief | Trek, Star Wars | **Refrigerated** | 🆕 **forced the class** |
| Antimatter, a live specimen, a containment breach | Trek, Alien, B5 | **Containment** | 🆕 **forced the class** |
| A freighter carrying starfighters; the Normandy's shuttle and Mako | Star Wars, Mass Effect, Halo drop pods | **Berth** | 🔴 **forced the class the engine cannot express** |
| Smuggling / concealed holds | Star Wars, B5 black market | *would be a compartment PROPERTY (hidden from inspection)* | ⚠ **HELD BACK — needs a customs/inspection system that does not exist** |
| Courier data, diplomatic dispatches | Trek, B5 | *massless — needs no different box* | ⚠ **HELD BACK — fails the compartment test; it is an intel transfer** (the Information Ledger's job) |
| A drop pod that deploys under fire | Halo ODST, Warhammer | *a delivery METHOD, not a container* | ⚠ **HELD BACK — Propulsion's axis, named so it is not lost** |

🔒 **Three held back, each with a stated reason.** That is the discipline: *"it needs no different box"* is a valid
reason to refuse a class; *"it sounds cool"* is not.

#### 🔑 The finding: the taxonomy is one dial away, and the dial is the CLASS

The store attribute already takes the class as its first argument. Every template hard-codes it. **So the whole
taxonomy above is reachable by authoring templates — no engine change, no new attribute, no save risk.** The one
genuinely new mechanism is **Berth**, because a berth counts *units*, not cubic metres — and the engine already has
that code, in the wrong place: **`GroundBayAtb` is a second, parallel storage system** with its own capacity, its own
class enum (`Personnel`/`Vehicle`), its own "a bay only accepts its own class" rule and its own summing helper in
`GroundTransport`. It reinvents `CargoTypeID` for one cargo. **Unifying it is what makes a ship, a shuttle, a tank and
a rifleman all just cargo with a class** — and it is the same shape as `CONVENTIONS §6` (*abilities are components — do
not invent parallel systems*) and the Chassis door's finding (checks that are domain-neutral in signature and
hard-coded in body). There are **four** parallel stores today: `CargoStorageDB`, `GroundBayAtb`, `EnergyStoreAtb`,
`GroundMagazineAtb`.

#### ✅ SHIPPED — slice L1 (data + one line), 2026-07-30

**Four compartment templates**, each on the existing 2-arg `CargoStorageAtb` (so no ctor overload, no L13 save risk):
`passenger-cabin` · `cryo-bay` · `refrigerated-hold` · `containment-hold`, plus the two new classes
`perishable-storage` and `contained-storage`, and every class's description now **states the axis it wins** (§39.8).
Relative pricing is the justification: general **0.01 kg/m³** < refrigerated 0.02 < passenger 0.04 < containment
**0.06** — the shielding IS the capability — while cryo carries **5× the people** of a cabin on **half the crew**.

**One line of code:** `TeamObject.VolumePerUnit` reported **`0.065 × teamSize` — 65 litres a head, the volume of a
human body**, which would berth seven thousand people in a 500 m³ cabin. It is now `PassengerPacking.VolumePerPerson`
— **10 m³ a berth, 2 m³ a cryo pod** — and because `CargoTypeID` is settable, freezing the same team really does
shrink what it needs. **Provably byte-identical:** nothing provided `passenger-storage`, so `GetFreeVolume` returned 0
for every team and the number was never read.

**Gauge `CargoCompartmentTests`** — and the first test is the structural rule that cannot rot: **every declared cargo
class must be providable by something**, with an allow-list where each entry states *why* (`battery-storage`: energy
lives in `EnergyStoreAtb`). Plus the bug red-before/green-after (a team reads 0 room, then real room), each class
winning an axis, the person-volume pinned, and the two shipped holds asserted unchanged.

🔑 **A registration rung the L6 chain never recorded:** a **cargo type id is itself unlocked** through
`StartingItems` — which is why `general-storage`, `fuel-storage` and `battery-storage` are already in Earth's list. So
a new compartment is a **three-part** registration: the **class id**, the **template id**, and the **design id**.
(`ordnance-storage` was missing from Earth's list and is now added.)

#### ⏭ Next, in order

1. **Food as a good** (L2, the code half) — define the material, have `food-production` produce it, and let
   `SustenanceProcessor` consume a stockpile with the installed-output as fallback. Its own TODO, and the thing that
   makes a supply line matter. *Deliberately not bundled here: a food good with no consumer wired is the exact bug
   this campaign exists to remove.*
2. **Berth** — unify `GroundBayAtb` into `CargoTypeID`, which also gives ships-carrying-ships (`ShipDesign` is already
   `ICargoable`; the only other trace is an unused `ParasiteLauncherReady` event enum).
3. **A generic component POWER DRAW** — the four powered compartments have no way to say they need power, because
   `WeaponSupply.PowerDraw_W` switches on **weapon** attribute types. Until that exists, "the cryo pods die if the
   reactor does" cannot be wired, and neither can the refrigerated hold's spoilage. Same finding as Chassis: the gate
   is domain-neutral in its signature and hard-coded in its body.
4. ~~The hold-mass unit bug (F7)~~ ✅ **RULED AND FIXED 2026-07-30 — see §46b.**

### 46c ⚠ "WHERE ARE THE FIXES FOR THE VAST VARIATIONS IN CARGO TYPE?" — the boxes shipped, the GOODS did not

**The developer's challenge, and it lands.** §46a derived nine classes and shipped four *compartments* — and then
**classified not one existing good into any of them.** The variety was in the design and not in the game.

#### The measurement that names the gap

| | Count | Share |
|---|---|---|
| **`general-storage`** | 30 | **81%** |
| `fuel-storage` | 5 | 14% |
| `battery-storage` | 2 | 5% |

**37 shippable goods and 30 of them in one box.** Eight compartments existed and 81% of all cargo still rode in the
generic one. *That* is what "vast variations" was asking for, and it had not been delivered.

#### 🔴 And the mirror test I failed to write found two goods NOTHING could hold

The structural test shipped in §46a walks **classes → providers**. That is only half the joint. Walking
**goods → providers** finds that `electricity` and `lithium-battery` both declare **`battery-storage`, which nothing
provides** — so neither can be stored or shipped by anything, reported as the usual silent `0`.

🔒 **And my own allow-list had excused it.** I allow-listed `battery-storage` on the grounds that *"energy is stored by
`EnergyStoreAtb`"*. That is true of `electricity` (a charge) and **plainly wrong for `lithium-battery`**, which is a
**manufactured object**: you can build one and then have nowhere to put it. **An allow-list entry that reasons about the
CLASS can hide a bug about a GOOD.** The lesson generalises past cargo: *a two-sided joint needs testing from both
sides, and an exemption must name the thing it exempts, not the category.*

#### ✅ Fixed

| Fix | What it was | What it is |
|---|---|---|
| **`lithium-battery`** | `battery-storage` — a manufactured item **storable nowhere** | `general-storage`. Strictly additive: from zero capacity to real capacity. |
| **`food` exists** 🆕 | **did not exist.** `SustenanceProcessor.cs:14` said so: *"food from the — not-yet-existing — food cargo good, so 0 for now"* | a `ProcessedMaterial` riding **`perishable-storage`**, refined from hydrocarbons + water + regolith, unlocked at Earth. **The first good the new taxonomy exists FOR** — a bare hold refuses it. |
| **the consumer is WIRED** | food supply was installed-component output only, so food was grown and eaten in the same place | `SustenanceProcessor.DrawImportedFood` counts a stockpile **and consumes it**. **A colony that cannot farm can now be supplied by one that can.** |
| **the mirror gauge** | classes → providers only | `EveryGood_NamesACompartmentSomethingProvides` — goods → providers, with the allow-list naming the *good*, not the class. |

**The food wiring is dimensionally careful, because this campaign has already been bitten by exactly this** (the reactor
`Lifetime` unit bug): demand and farm output are **per-day rates**, a stockpile is a **quantity**, and the processor runs
**monthly** — so a call may draw up to 30 days of shortfall and the amount drawn is converted **back to a daily rate**
before it is added. Adding a raw stockpile to a rate would have made one crate of rations look like an infinite farm.
And **it depletes**: a supply that is read but never consumed is free food, which is the "pretty" failure this campaign
exists to remove.

✅ **Byte-identical on a stock game, for a structural reason:** `PerCapitaFoodDemand` defaults to `0`, so the shortfall
is 0 and nothing is ever drawn. The test sets the coefficient itself, the way a calibrated build would, and asserts
**both** halves — nothing drawn at the stock 0, food eaten once demand is real.

#### ⏭ Still generic, and why it is NOT being bulk-reclassified in this slice

**82% of goods remain `general-storage`.** The obvious next move is to reclassify the ones that are physically something
else — `water` and `hydrocarbons` are **liquids** and belong in the fluid class; raw `fissionables` are **radioactive**
and belong in containment. **Held back deliberately, with the reason:**

🔴 **Both are MINED goods, and mining writes into cargo.** `MineResourcesProcessor` adds to the colony's hold, and if the
destination compartment has no room the loss is a **silent zero** — the exact failure this whole slice is about.
`general-storage` at the start colony is a 10,000 m³ warehouse; `fuel-storage` is a few thousand m³ of tanks, and the
start stockpiles were already tuned against the warehouse cap (see `Industry/CLAUDE.md` → ALPHA stockpiles). **Moving a
mined good to a smaller box would silently throw away production.** So it needs its own slice with a capacity gauge
first: *can this host actually hold what it mines?* — which is a good test to own regardless.

⚠ **`electricity` is the one remaining orphan and it is a real ruling, not an oversight:** *should power be shippable
cargo?* Charged cells are a genuine science-fiction trade good — but wiring it through `CargoStorageAtb` would give the
game a **second way to hold a charge**, and a fifth parallel store is not a fix.

### 46d 🔒 THE DOCK — built as its OWN AREA, and food's loop closed (developer, 2026-07-30)

*"Did you build berth for an actual dock that can be built? That should be its own area. Also you should build food."*

#### The dock, and why it is not a ninth cargo class

**Berth was listed in §46a as the one class the compartment taxonomy could not express, and the ruling is right: it
should not be in that list at all.** Every other compartment measures its contents in **cubic metres poured in**. A dock
holds a **discrete vessel that arrives and leaves under its own power**, so the question it answers is *how many, and how
big* — not *how much fits*. Pouring a frigate into a warehouse by volume would let you carry half a frigate. That is
exactly why `GroundBayAtb` had to invent its own capacity system for troops instead of using a cargo type.

**So it is its own area: `GameEngine/Docking/`, with its own template file `TemplateFiles/docking.json`.**

| Piece | What it is |
|---|---|
| **`DockBayAtb`** | The buildable component. **Two gates:** `BerthTonnage` (the budget) and `MaxHullMass` (the door — the largest SINGLE vessel that fits). The ctor clamps the door to the budget, so bad data cannot open a door wider than the bay. |
| **`DockedShipsDB`** | The carrier's registry — **ids, not references**, and an id that no longer resolves is skipped rather than throwing (a docked ship destroyed by a hit costs capacity nothing). Carries `Clone()` + a copy-ctor, because **L12** means a blob without one silently becomes a bare `System.Object` when the carrier jumps systems — the one case a dock most needs to survive. |
| **`DockTools`** | Capacity/door reads summed **on demand** and health-scaled (a shot-up bay holds less), `CanDock` with a **stated reason** per refusal, `TryDock`, `Undock`, and **`UndockAll` — the grave rung.** |

🔑 **The dial is a SPLIT, and it is the honest shape (§34.7a).** `Berth Tonnage` is carved into `Berths`, so
**`Max Hull Mass = tonnage ÷ berths`**: the **total capacity is invariant** and you choose *few wide doors* or *many
narrow ones*. Neither strictly dominates — and every extra door costs **structure and deck crew**, so a **carrier is a
real commitment beside a tender** rather than free flexibility. Two shipped designs make it visible in the data:
`docking-bay` (60 t / 4 berths → 15 t doors) and `heavy-berth` (the same 60 t / 1 berth → a 60 t door).

✅ **The consequence that makes it more than a spreadsheet row:** a docked ship is re-parented with
`PositionDB.SetParent`, which preserves absolute position across the switch — **the same mechanism that makes a moon
follow its planet** — so it travels with the carrier and stops being independently located. Undocking hands it back to
whatever the carrier itself orbits.
🔒 **And the grave rung is explicit: `UndockAll` releases everything ALIVE.** Losing a hangar must never silently delete
the ships inside it.

✅ **Stock behaviour is byte-identical by construction:** no base-mod ship mounts a bay, and **nothing in the engine calls
`DockTools`** — a ship docks only when something asks. The **order** that lets a player or the AI ask is the next slice,
and it will be **one verb both seats issue** (the developer's law) because it is one method here.

⚠ **Found while building it — a landmine worth its own entry: there are TWO `PositionDB` classes.** The one in
`Engine/Datablobs/PositionDB.cs` is **entirely commented out** (the file opens `using`s and then a `/*`), so
`using Pulsar4X.Datablobs` for it compiles and **silently gets you nothing**; the live class is
**`Pulsar4X.Movement.PositionDB`**, in `Movement/MoveState.cs`. A `^namespace` grep finds the dead one first.

#### Food's loop closed — you could not bank a surplus, so you could not export food

§46c added the `food` good and wired `SustenanceProcessor` to **consume** a stockpile. That was only half a loop: farm
output was a **per-day rate consumed the instant it was computed**, so **a colony growing ten times what it eats had
nothing to ship**, and food could only move if you manufactured it at a refinery.

**`BankFoodSurplus` closes it.** Exactly one branch runs per month, so nothing is double-counted:

| Farm output vs demand | What happens |
|---|---|
| **short** | draw STORED food and consume it → a colony that cannot farm is fed by one that can |
| **over** | **BANK the surplus** → and only now can a surplus be hauled anywhere |

🔑 **And the failure mode is the right one, not a silent loss.** Food rides `perishable-storage`, which only a
`refrigerated-hold` provides — so **a farming colony with no cold store banks nothing and the surplus spoils.** That is
what "perishable" *means*, and it is a real reason to build cold storage at a breadbasket.

✅ **Byte-identical on a stock game twice over:** no start colony has a farm installed (no surplus to bank), and none has
a refrigerated hold either (so even a surplus would bank nothing). Both halves are asserted.

### 46e 🔒 "GET THE LAST 12%" — three goods moved, and the honest floor is 74% (developer, 2026-07-30)

**The measurement that prompted it:** 38 shippable goods, **31 (82%) in `general-storage`**. The ruling: add the
features and close the gap.

#### What moved, and what deliberately did not

**Three goods were filed as dry bulk and are physically something else.** Everything else in that 82% is ore, refined
metal, plating, plastic, electronics and concrete — **which genuinely IS bulk cargo**, and moving it would be wrong.

| Good | Was | Now | Why |
|---|---|---|---|
| **`water`** | general | **`fuel-storage`** | a liquid — no shape of its own, needs a sealed vessel |
| **`hydrocarbons`** | general | **`fuel-storage`** | a liquid |
| **`fissionables`** | general | **`contained-storage`** | raw radioactive ore — dangerous to its own carrier, and it already has a real consumer (it is the input to `fissile-fuels`) |

| Class | Before | After |
|---|---|---|
| `general-storage` | 31 (82%) | **28 (74%)** |
| `fuel-storage` | 5 (13%) | **7 (18%)** |
| `contained-storage` | 0 | **1** |
| `perishable-storage` | 1 | 1 |
| `battery-storage` | 1 | 1 |

🔒 **74% is the HONEST FLOOR for reclassification, and chasing lower would be dishonest.** The remaining 28 are bulk
because they *are* bulk. **The share only falls further when the empty classes get GOODS** — and every addition still
has to earn a consumer (§39.8). Named, not smuggled: **antimatter** in containment would need
`ExhaustVelocityLookup` and `FuelGradeLookup` extended before an engine could burn it; **colonists** in
passenger/cryo need population made carryable; **biomass** and **medical stock** in refrigerated need a consumer that
does not exist yet.

#### 🔑 The gauge that had to exist FIRST, and what reading the source changed

The reclassification waited a slice because moving a **mined** good is the dangerous part. Reading
`MineResourcesProcessor` corrected the severity — and it is worth recording, because I had it worse than it is:

> `stockpile.AddCargoByUnit(mineral, minable)` returns **what actually fitted**, and only that amount is subtracted
> from the deposit.

**So you do not lose ore.** What happens is that a colony with nowhere to put a mineral **silently stops mining it** —
no error, no log, a mine that appears to do nothing. That is the same signature as the Stasis bug, and it is still
exactly the failure class this campaign exists to remove.

**Measured before moving anything:**

| Compartment | Earth's capacity | Day-one need after the move |
|---|---|---|
| `general-storage` | 100,000 m³ (10 warehouses, each **clamped** from an authored 1,000,000 to the template's 10,000 — the L7 clamp) | 60,155 m³ (60%) |
| `fuel-storage` | **1,000,000 m³** (the fuel farm, clamped from 5,000,000) | 49,382 m³ (**5%**) |
| `contained-storage` | 🔴 **ZERO** | 53 m³ |
| `perishable-storage` | 🔴 **ZERO** | 0 m³, but food is refined here |

⚠ **My earlier caution was the right shape and the wrong size.** I had assumed fuel storage was "a few thousand m³";
it is a million, at 5% use — the fluid move was never close to tight. **Containment was the real gap, and so was
refrigerated**, because Earth refines food and had nowhere cold to put it.

✅ **Fixed by giving them somewhere to go:** `containment-vault` and `cold-store` (both 10,000 m³, the template
ceiling — ~190× the fissionables Earth starts with) are now **registered AND installed** on Earth.

✅ **Blast radius verified, not assumed: Earth is the ONLY colony blueprint in the base mod.** Every scenario file was
swept for a `StartingItems` colony node; `umf`/`kithrin`/`uef-devtest` have none, so no other authored colony can be
stranded by the move.

**Gauges:** `EveryMineralThisWorldHas_HasSomewhereToGoOnTheColony` — every mineral in the ground under the colony must
have a compartment with room, and every good in the starting stockpile must really be in store rather than quietly
dropped at load. Plus `TheThreeMisfiledGoods_NowRideThePhysicallyCorrectCompartment`, which also asserts that
ore/metal/parts **stay** in bulk: the point was three moves, not a sweep.

### 46b 🔒 "MAKE THE FIELD KGS NO TONNES" — the ruling, and the design names pinned the factor at 100

**The developer's ruling, verbatim: *"make the field kgs no tonnes."*** So the field stays **kilograms** and the numbers
get corrected — rather than the field being re-read as tonnes.

**What was wrong.** Every hold's `Mass` was `Size Efficiency × 1.0`, and `Size Efficiency` is `volume × 0.01` — so a
**5,000 m³ cargo hold massed 50 kg.** The property's own description called itself *"the amount of **tonnage** taken up
by racking, office space etc."*: **the author was thinking in tonnes and the field is kilograms.**

⚠ **CORRECTION to my own estimate.** I reported the gap as "~1000×" from that *tonnage* wording. **It is exactly 100×,
and the shipped design NAMES prove it** — a piece of evidence already in the repo that I had not used:

| Design | Capacity | Mass before | **Mass now** | Its name |
|---|---|---|---|---|
| `Cargo Hold 1t` | 1,000 m³ | 10 kg | **1,000 kg** | ✅ **"1t" is now true** |
| `Cargo Hold 5t` | 5,000 m³ | 50 kg | **5,000 kg** | ✅ **"5t" is now true** |

**The names were right the whole time; the formula had lost the unit.** That is what makes this a **unit fix** rather
than a balance change, and it is why the factor needed no judgement call at all. Applied to all six hold templates
(`×1.5→×150` warehouse, `×1.0→×100` general hold, and the four new compartments `×400 / ×300 / ×200 / ×600`), and the
`Size Efficiency` description now says **kilograms** instead of tonnage.

⚠ **This is the ONE part of the cargo work that is deliberately not byte-identical.** Six base-mod ships mount a hold;
the largest change is the **Freighter** (+4,950 kg against its medium hull's 90,000 kg budget). **`ShipMassBudgetTests`
is the gauge that adjudicates it** — it asserts every base-mod ship stays under its hull budget and fails loudly if any
design is pushed over. The other five ships carry a 1t hold (+990 kg each), two of them on heavy hulls.

⚠ **And one calibration gap left FLAGGED, not folded in:** this puts a bare hold at **1.0 kg per m³** of capacity, where
a real 33 m³ shipping container masses ~2,200 kg — about **67 kg/m³**. The family is still ~67× lighter than a steel
box. **That is a balance call sitting on top of a unit fix, and they are not the same decision** — so it stays visible
in the test readout rather than being quietly bundled in.

#### ⚠ Two pre-existing data bugs found while validating this slice (reported, NOT fixed here)

1. 🔴 **Two ordnance designs point at templates that do not exist** — `default-design-missle-sensors` →
   `missle-electronics-suite` and `default-design-prox-frag-5kg` → `missle-payload`. The templates are spelled
   **`missile-`** (two s). Latent because neither design is in any colony's `ComponentDesigns` — but
   **`ordnanceDesigns/missile-250.json` references `default-design-prox-frag-5kg`**, so the moment a faction unlocks
   that missile, `ComponentDesignFromJson` throws. Same shape as the `gallicite` bug in gotcha #10.
2. ⚠ `default-design-fuel-farm-5000k` spells its design properties **lowercase** (`"key"`/`"value"`); it binds only
   because Newtonsoft is case-insensitive.

🔒 **Both are exactly what §51's one data-only test would catch, and neither is visible to `dotnet test` today.**

## 47. Door 5 — COMMAND: one component built twice, thirteen months apart

**The door:** one seat — *what does it command · how well · does the occupant survive.* `CommandBerthAtb` has all
three; `AdminSpaceAtb` has the first. **The source admits it:** `CommandBerthDB.cs:16` calls the berth's `Span`
*"the force size the berth can command — **the old ConsoleSpace idea with teeth**."*

✅ **What is right, and it is the √ law's third appearance.** `admin-complex`: `Mass = Office Space × 100` and
`ColonyHexMapDB.UpdateMaxRadius: radius = Max(1, ceil(√(officeSpace ÷ 100)))` ⇒ **radius ∝ √mass, hex AREA ∝ mass.**
Hexes per tonne 0.70 → 0.61 → 0.33 across the dial: **falls, so no dominant setting.**

🔴 **`Console Space` costs mass ×100 and, on a ship, writes nothing.** Its only engine reader is
`ColonyHexMapProcessor`, which returns early unless the entity has `ColonyInfoDB`. And `AdminSpaceProcessor.
ReconcileSeats` creates **exactly one seat per component, keyed by NAME** — never by size. **A 1-console bridge is a
20-console bridge for 100 kg instead of 2,000.**

🔴 **The berth has three free dials, and the worst deletes the door's own grave rung.** `Support` (0–50, +50% work
rate via `SiteWorkProcessor:280`) · **`Survivability` (0–100, and ≥100 ZEROES the leader-death roll in `SiteHazard`)**
· `Span` (documented, **zero readers**, deferred to SE-3). **The one component in the designer that can destroy a
named character ships with free immunity.** ✅ Contrast `Grade`: ×10 work rate for ×10 mass, crew and research —
**benefit ÷ cost flat**, no dominant setting. Put Support and Survivability in Grade's formula and the component is
finished.

⚠ **`Admin Level` names the seat and nothing else** — an 11-value ladder (Ship→Empire) stored on every seat, and the
orders that assign commanders never look at it. `AdminWindow.cs:172` prints it. **Any commander fits any post.** The
berth's `Role` already *gates* properly (`GetWorkingBerth` refuses a mismatch) — level should gate the same way, and
that is the hook the Governance design is waiting on.

✅ **The best grave rung in the designer:** `AdminSpaceAtb.OnComponentUninstallation` **drops that specific seat by
component name and frees its occupant** (with a comment explaining it must be by name, because the hook fires before
the component leaves `ComponentInstancesDB`). **A decapitation strike genuinely collapses command.**
⚠ **Hazard:** the door's only output blob, `ColonyHexMapDB`, has **zero `Clone()` references** — landmine L12.
Harmless while colonies don't move managers; not harmless once berths ride ships that jump.

## 48. Door 6 — ENHANCERS: the door where you set your own price

**The collapse:** one choice (*what does it improve*) + one slider (*by how much*). And the four ground augments are
**literally one attribute with four sets of defaults** — all four carry all five dials, each leaving the unused four
at zero (a `shield-generator` ships with a live `StrengthBonus 0..1000`).

🔴 **Six of eight templates have `Mass` on a SEPARATE dial** (`CarryMass` / `Cadre Mass` / `Automation Mass`),
independent of capability. **You declare your own weight.** Worst first:

- **`unit-caliber`** — `Firepower Caliber` and `Toughness Caliber` (both 1.0–2.0) in no cost formula. **The maximum
  cadre is CHEAPER than the shipped one**: ×2.00 at 1,000 kg vs the shipped ×1.30 at 3,000 kg. **Largest free combat
  multiplier in the designer** — ×1.54 firepower on every warship for a third of the mass.
- **`reflex-booster`** — `EvasionBonus` 0→1.0 free. **Total evasion for one kilogram**, on the stat §1b measured as
  an effective-health multiplier to **×20** (armour reaches ×10). Only saturation fire still floors it.
- **`crew-automation`** — `Crew Reduction` 0→200 free. **200 people saved for nothing**, out of a pool its own
  description calls *"scarce workforce"* and which `ManpowerTools.ResolveBuild` genuinely gates on.
- **`shield-generator` / `ward-projector`** — 1,500-point pool, or 600 with `ShieldRegenFraction` 5.0, all free. ⚠ The
  ward's description is a model of design writing (it explains the capacity↔recharge trade and **flags both dials for
  a balance pass**). The trade is right; it just is not priced, so both ends are free.

🔴🔴 **AND POWER ARMOUR IS A BOOTSTRAP, NOT A LEAK.** `GroundUnitAssembly` pass 1: `capacity += StrengthBonus * c`;
pass 2: `used += g.Mass * c` where `Mass = PropertyValue('CarryMass')`. **Two independent dials on one part — one
funds the budget, the other spends it.** At the extremes (CarryMass 1, StrengthBonus 3,000) a single part nets
**+2,999** on a frame whose base is 100, and `MaxItemWeight = capacity × 0.5` lifts the per-item cap with it. **The
ground carry budget has no ceiling, and every gate downstream of it (per-item weight, the P2b power gate, the ammo
gate) is measured against an inflatable number.**

🔑 **BOTH FIXES ARE ALREADY RUNNING IN THE REPOSITORY.**
1. For the multipliers: **`ground-training-cadre`** does it right — `Mass = 50 × (1 + (TrainingMultiplier − 1) × 4)`,
   so ×1→×2 takes the part 50 kg → 250 kg. And it says so: *"Training is EARNED: the higher you dial the multiplier,
   the steeper the mass/credits/research/build-time. Multiplier range + cost curve are **FLAGGED balance values**."*
   `sealed-systems` does the same (`Mass = 40 × (1 + Sealing × 1.5)`, also self-flagged).
2. For the bootstrap: the ground weapons' **`itemMass = Math.Max(w.Mass, w.Attack * AttackCarryFactor)`** floor.
   Applied as `Max(CarryMass, StrengthBonus × 0.1)` it is **byte-identical on the shipped Power Armour** (300 × 0.1 =
   30, exactly its authored mass) and caps leverage at a **stated 10:1** — verified 3000:1 → 10.0:1 across the dial.

✅ **Do not touch:** the stacking rules (shields and evasion **add** across parts; training and sealing take the
**best** and are documented as not stacking) and the research costs (`[Mass] × 3` on the automation suite is the
highest coefficient in any door).

🔒 **THE LESSON THAT REPLACES THE "OLDEST ATTRIBUTE" HEURISTIC.** `unit-caliber` and `crew-automation` are *recent*
(⚙6.2 / ⚙6.3, with their own CI gauges) and are the broken ones; `ground-training-cadre` and `sealed-systems` were
written in the **same month** and price their dials. **Age does not predict honesty. Whether the author was thinking
about COST does — and you can see it in the diff, because both who got it right left a "FLAGGED balance values" note.**

## 49. Door 7 — INDUSTRIAL: the healthiest door, and the rule its exceptions name

✅ **Seven of ten templates are correct.** `mine` · `automine` · `refinery` · `factory` · `shipyard` ·
`local-construction` · `launch-complex` · `constructor` · `ground-constructor`: output linear in the dial, mass linear
in the dial, **capability-per-tonne flat across the whole range.** After six doors of leaks that deserves saying.
🔒 **And the build plan's predicted "rate ↔ efficiency" shape is wrong and should be:** industry is a **pure scale
dial**, and the correct test for one is not *"is there a trade?"* but *"does benefit ÷ cost stay flat or fall?"*

✅ **Both mines satisfy the §39.8 justification rule, unprompted.** Mine: 0.0002 output/kg (**80× the automine**),
5,000 crew, colony-only. Automine: **0 crew** and a **ShipCargo mount** (droppable on an uncolonised rock). Each wins
an axis outright. ⚠ The mine's 5,000-crew figure is steep — calibration, not structure.

🔴 **`research-lab`: `Mass = 100000`, Volume 1000, Crew 20, Research 10, Credits 120 — all constants.** So
`Research Points` **1 → 100 is free**: a hundredfold difference in how fast the empire advances, same price, and
research gates every other door. **The most consequential free dial found.** Fix: `Mass = Research Points × 10,000`
(byte-identical on the shipped 10-point/100-tonne lab).
🔴 **And `Cost Per Day` (0–100,000) is DEAD** — `ResearchPointsAtbDB._costPerDay` is stored with a public getter and
**read by nothing**; the three `.CostPerDay` consumers are all on `ResearcherDB`/`AdministratorDB` (a *scientist's*
funding). **DEVELOPER RULING: bill it as the lab's operating cost, or delete the dial.**

🔴 **`Fighter Construction Points` (factory, 0–1,000) is DEAD, and it is the factory's ONLY adjustable dial besides
`Size`.** Absent from the `DataDict` that becomes the `IndustryAtb` rate table (which lists exactly
`component-construction` · `installation-construction` · `ordnance-construction`), and **there is no
`fighter-construction` industry type in the game** — the five are refining / component / installation / ordnance /
ship-assembly. Shipped default is `0`, so nothing is mis-balanced; it is a UI-honesty problem. **DEVELOPER RULING:
are fighters a production type?**

🔴 **`bunker`: `Mass = 50000` constant** ⇒ `LocalFortify` and `AdjacentProjection` (both 0–1) free. A bunker that
totally fortifies its region *and* projects full cover to every neighbour costs the shipped 0.25/0.12 price. 🔑 **This
is where the Defense door's "fortification is infrastructure, not a combat defence" ruling landed — unpriced.** Plus
`TileFootprint` 1–40, the same cost-with-no-benefit dial as the building foundation.
🔴 **`infrastructure`: `Mass = 1000` AND `BuildPointCost = 100` AND `CreditCost = 0`** ⇒ **six** free dials. Most of
them are Civic's; the Industrial one is **`Support Capacity`** — the colony's equivalent of a chassis budget, free.

✅ **The one that looks like a leak and is not:** the `shipyard`'s `Mass` reads only `Slip Size`, but
`CrewReq = PropertyValue('Crew Size')` **one for one**, and output is `Crew Size × 0.02`. So output is paid for in the
currency that binds a planet-side yard: **people.** Verified flat at 0.020 output/crew across both dials.
🔒 **The seven-channels rule working correctly: the question is never "is it in the mass formula" but "which of the
seven, and is it one the player is constrained by?"**

## 50. Door 8 — CIVIC: a forty-point morale term that can never fire

🔴🔴 **THE HEADLINE.** `ColonyMoraleDB` carries a complete employment term — `MaxEmploymentBonus = 15.0`,
`MaxUnemploymentPenalty = 25.0`, two-sided, with a documented negative sentinel so "no job data" reads *neutral*
rather than as total unemployment, and correctly denominated against **workforce** rather than headcount
(`PopulationProcessor:74-76`, with a comment saying why). It reads `GetTotalJobs()`, which sums `EmploymentAtbDB.Jobs`
across installed components. **No template in the game carries `EmploymentAtbDB`.** So jobs is always 0,
`employmentRatio` pins to `-1.0`, and **the widest single factor in the morale model contributes exactly zero, in
every game.** 🔒 **The exact mirror of `LogiBaseAtb`** — that is a producer with no consumer; this is a **consumer
with no producer.** Both invisible to every test in the project.

✅ **And the producer already exists as data.** Every installation declares a `CrewReq` — factory `Size × 5`, refinery
`[Mass] × 0.1`, mine `Area × 0.005`, lab 20. **Those figures ARE the jobs it provides**, and the engine already reads
colony crew as a demand (`InfrastructureProcessor`). ⚠ **But this is the biggest live behaviour change in any of the
eight doors** — morale feeds migration, tax income and legitimacy. **Flag-gate it and measure against the existing
`MoraleTests` baseline**, the same treatment the fuel gate got.

✅ **`food-production` is the best-priced template in the game.**
`Mass = Food Output × 0.1 + **Food Quality³ × 200** + Automation × 500` — three dials priced, one **CUBICALLY**
(0.5 → 25 kg · 1.0 → 200 kg · 3.0 → 5,400 kg: tripling quality costs 27×, a deliberate steep return on a bounded
luxury stat, and the only cubic cost curve in ~90 templates). And `CrewReq = Max(1, Output × 0.02 × (1 − Automation))`
— **automation buys crew down AND costs mass to do it.** 🔑 **That is precisely the trade `crew-automation` promises in
its description and gives away free (§48). Fourth time the fix was already in the repository.**

🔴 **`space-habitat`: `Mass = 1000` · `CreditCost = 0` · `BuildPointCost = 100` — three constants — and
`Support Colonists` runs to 1,000,000.** **A million colonists for one tonne**, and **200× its sibling
`infrastructure`'s 5,000 ceiling on an identical cost block.** Population is the resource everything else derives
from. ✅ One thing right and worth defending: the habitat deliberately carries **no gravity/pressure attribute at
all** — which is how a station escapes a planet's environment, and it **cannot** be done by setting those dials to 0
(landmine L7: template values clamp to their tech bounds, so a 0 silently will not stick).
⚠ **`Housing Comfort` runs 0–50 while `MaxComfortBonus = 20.0`** — 60% of the dial's travel is inert.

🔑 **AN EIGHTH COST CHANNEL — TIME.** The academies price `Class Size` in mass (`× 100`) but `Class Length` (1–48
months) appears in **none** of the seven channels — and it is **not** free: `NavalAcademyAtb.cs:46` sets
`graduationDate = now + TrainingPeriodInMonths × 30 days`, so a longer course buys better graduates and pays in
**throughput**. 🔒 **A dial is honest if it is priced in any currency the player is actually constrained by — mass ·
volume · crew · research · credits · build points · resources · AND TIME.**

**The reconciliation the build plan asked for:** the morale plan (M1–M5) **built the consumers correctly.** Four of
six inputs are live (crowding · comfort · food · tax); **employment is dead-wired** and **power shortage is inert**
until colony power demand is calibrated — which the Power door just unblocked by giving the solar array a colony
mount. **Civic owes the morale plan two producers, not a design.** ⚠ And every "live" row means *the wire is
connected*, not *the number is right* — **whether −35 for crowding feels like anything is a question only a played
game answers.**

## 51. 🔒 THE ONE THING TO BUILD OUT OF ALL EIGHT DOORS

Every finding in the whole campaign is one of **three mechanically-detectable shapes**:

| Shape | What it looks like | Where |
|---|---|---|
| **A — `Mass` = a constant** | every dial on the template is free at once | `research-lab` · `bunker` · `infrastructure` · `space-habitat` · `station-chassis` · `building-foundation` · 4 ground frames · `troop-bay` · `space-port` |
| **B — `Mass` = a DIFFERENT dial** | you declare your own price | all six broken Enhancers · the ship hull's `Mass Budget` beside `Hull Mass` |
| **C — no counterpart on the other side** | the dial writes nothing, or the attribute has no producer | `Fighter Construction Points` · `Cost Per Day` · `LogiBaseAtb` (no consumer) · `EmploymentAtbDB` (no producer) · ground-frame `Size` · berth `Span` · the fire-control pair · `Resolution` |

**THE TEST — data-only, no engine change, no save risk, ~a dozen lines:**
1. For every template, for every property whose `GuiHint` starts with `GuiSelection` (a real player dial): assert its
   name appears in **at least one** of the seven cost formulas **or** in an `AtbConstrArgs` argument list (so it
   reaches the simulation). Flag it if **neither**. *(Catches A, B and half of C.)*
2. For every `*Atb` type: assert it has **both** a producing template and a consuming reader outside its own file.
   *(Catches the other half of C — both `LogiBaseAtb` and `EmploymentAtbDB`.)*
3. Assert **no `UniqueID` appears in two template files** (the §46 F1 merge, which no existing gauge can see).
4. An explicit **allow-list**, where every entry carries a one-line reason. *"It costs time"* (the academies) is
   valid; *"nobody noticed"* is not. **Writing that list IS the design review.**

🔒 **It would have found every finding in this campaign, and unlike eight pages of hand audit it does not rot.** If
only one thing is built out of PART SEVEN, build this.

### 51a. Rulings owed, all eight doors — the short list

| # | Ruling | Door |
|---|---|---|
| 1 | Which shipped **hull** is correctly tuned? (the √-law anchor; medium moves +25% otherwise) | §45 |
| 2 | **`LogiBaseAtb`** — wire it as the colony logistics-route cap, or delete it? | §46 |
| 3 | Are **fighters** a production type? (add the industry type, or delete the dial) | §49 |
| 4 | Should a lab's **`Cost Per Day`** actually bill the colony? | §49 |
| 5 | Arm **employment** in morale? (the biggest live behaviour change in the campaign) | §50 |
| 6 | Arm **`EnableFuelExhaustion`**; is a reactor **refuelable**? | §39.7a |
| 7 | The **RTG's power-density** gap — pick a number, or leave it flagged? | §39.8 |
| 8 | The **Sensors S0 band** ruling (reaches Power's solar array and the deferred FTL band) | §34.5 |

---

## 52. 🔒 WATER AND UNDERGROUND — what Propulsion, Weapons and Sensors actually need (developer, 2026-07-30)

*"Make logical additions and fixes for propulsion, weapons, and sensors as needed for water and underground. **Don't
force it** — just make it make sense."*

The Chassis door added three environments (open water, atmospheric, subterranean) as declared-but-empty cells. The
follow-up question was the right one: **a chassis for an environment is useless if nothing else in the game can tell
that environment apart.** Measured across all three doors, the answer was **nothing can**:

| Door | What exists for water / underground | Measured how |
|---|---|---|
| **Propulsion** | `GroundLocomotion` = **Foot · Tracked · Walker · Hover**. No naval, no submersible, no burrowing. Water's entire representation is **one boolean**, `Amphibious`, which lets a unit *cross* an ocean hex — no depth, no speed effect, no cost. | read the enum + `GroundLocomotionAtb` |
| **Weapons** | **Nothing.** No reference to atmosphere, underwater, submerged or vacuum anywhere in `Weapons/`, `Combat/` or `Damage/`. A laser fires identically in vacuum and forty metres down. The only near-miss is the `Corrosive` damage signature, which treats a dense medium as a *hazard*, never as something a shot travels through. | grep, all three folders |
| **Sensors** | **Zero hits** for atmosphere / water / submerged / sonar / medium in the entire `Sensors/` folder. Detection is one **vacuum** law — `AttenuationCalc = source × 1e6 / (4π d²)` — applied to everything, everywhere. | grep + read the law |

🔴 **And one live bug underneath all of it: `GroundTerrain.Classify` sorts `Ocean` into `Open`** (it is in neither the
Rough nor the Cover case, so it falls through the `default`). Two consequences, both backwards:
1. `CoverDefenseMult(Open) = 0.9` — open ground *favours the attacker* — so **a submerged defender is easier to kill
   than one standing in a forest.**
2. `LocomotionTerrainMult` returns **1.0 for Open** — so **a boat's handling is ignored in water.** The dial exists and
   the medium never reads it.

`GasLayers` — the gas-giant terrain type — falls through the same way.

### 52a 🔑 THE ADDITION: one property per environment, three consumers that already exist

**Not three separate systems. One number, read three ways** — and it fills slots the design already *declared*.
`GroundTerrain`'s own header maps the terrain vocabulary onto the space-hazard vocabulary and names what is missing:
*"**Concealment** ↔ hazard `SensorJam` (forest/jungle hides units — ground fog of war; **a later slice**)"* and
*"**EnvironmentalHazard** ↔ `HeatDamage`/`Corrosive` … **later**"*. **So this was designed and left unbuilt. It is a
named hole being filled, not a new axis being invented.**

| Door | The consumer that already exists | What the medium changes |
|---|---|---|
| **Sensors** | `AttenuationCalc` / the signature path | **opacity** divides the signature → fills the declared *Concealment* slot |
| **Weapons** | `GroundWeaponMount.RangeHexes` → `ResolveRegionCombat`'s per-weapon band (the W2 slice) | **opacity** divides engagement range, **by weapon nature** |
| **Propulsion** | `LocomotionTerrainMult` + `GroundMobility.StepSecondsFor` | **drag** divides speed *unless your locomotion is rated for that medium* |

**Opacity, per medium** — the water and rock figures are physics, not balance: seawater absorbs EM over *metres*, and
rock is opaque outright.

| Medium | Opacity | What it does to play |
|---|---|---|
| Vacuum / open ground | **1** | nothing — byte-identical |
| Forest / jungle (`Cover`) | ~3 | ground fog of war, the declared *Concealment* slot |
| Mountains (`Rough`) | ~2 | line of sight broken, so you must go and look |
| **Submerged** | ~**1,000** | **a submerged unit is effectively invisible to every sensor in the game** |
| **Buried** | ~**10,000** | **a buried thing cannot be seen at all — only inferred**, which is the whole point of burying it |

**Weapons read the same number by NATURE** (already a first-class axis): **Energy** is worst in water and useless with
no line of sight · **Kinetic** is poor (drag) and blocked by rock · **Explosive** is *better* in both (water transmits
shock; a confined blast has nowhere to go) · close/melee is unaffected.

🔑 **The payoff, and it is free: THE WEAPON TRIANGLE ROTATES BY ENVIRONMENT.** Artillery's whole edge is standoff range.
Underwater and underground that edge collapses, so **the corner that dominates changes with where you fight** — and a
submarine action reads the way it should: *everything is short-range and explosive.* **No torpedo type had to be
invented; a torpedo is a slow Explosive munition, which the taxonomy already expresses.**

### 52b ✅ WATER is cheap — three of the four pieces are already built

1. ✅ **The single change that makes water real: give `Ocean` its own terrain class.** Then cover, the locomotion
   multiplier and march time — all *already built* — start reading it. **One enum value and one switch case turns three
   existing systems on at once.**
2. ✅ The pathfinder gate exists (`Ocean && !amphibious`).
3. ✅ The locomotion × terrain multiplier exists.
4. 🔴 **`Amphibious` is a free boolean, so it is not a decision** — a pass flag with no mass, no speed cost, no downside;
   nobody would ever decline it. Make it a **locomotion MODE with a land penalty**: a hull rated for water gives up
   ground speed, one rated for both gives up more. **Narrow and cheap, or broad and expensive** — the same trade as the
   Chassis operating envelope, and each mode wins an axis outright (tracked = cheapest on ground · amphibious = the only
   one that does both · submersible = **the only one that is invisible** · hover = ignores drag, and already exists).

⛔ **What is deliberately NOT being added: sonar.** This engine's sensor model is an **EM waveform in nanometres**
matched against a receiver's band. **Sound is not electromagnetic**, and giving it a wavelength in nm to squeeze it
through the existing matcher is exactly the forcing the ruling forbids. The honest version needs no new physics:
**water blocks EM, so range collapses, and the counter is to get close or put the sensor in the water with the target.**
A genuine acoustic band is a separate decision, **named here so it is not lost** — not smuggled in as a fake wavelength.

### 52c 🔒 UNDERGROUND IS NOT AN ENVIRONMENT — it is a POSITION, and its payoff is a missing READ

Ask what "underground" actually *means* in play and there is one answer: **you cannot be bombarded from orbit.**
Everything else about it (no line of sight, short ranges, slow going) is what `Rough` terrain and the range rules
already do. And **the "dug in" mechanic is almost entirely built**:

| What "dug in" needs | Exists? | Where |
|---|---|---|
| A defender takes less incoming | ✅ **built** | `GroundTerrain.CoverDefenseMult` — Rough ×1.5, Cover ×1.25 |
| A **building** that hardens its own ground and projects into neighbours | ✅ **built, design-driven** | `GroundDefenseAtb.LocalFortify` / `AdjacentProjection` → `GroundFortification.DefenseMult`, capped at ×2 |
| A grave rung — the shelter can be taken from you | ✅ **built** | a captured hex's building stops fortifying the defender |
| **Orbital fire respects any of it** | 🔴 **NO — the whole gap** | `ApplyGroundBombardment` reads the unit's own `Defense`, the artillery matchup and its explosive armour resist, and **never calls `CoverDefenseMult` or `DefenseMult`** |

🔑 **So a unit sitting in a mountain bunker takes exactly the same orbital fire as one standing in an open field.** The
dial is built, the building that provides it is built, the cap is built, the grave rung is built — **and the one attack
it was invented to resist ignores it.**

🔒 **The ruling this produces: make orbital fire read cover and fortification, and "underground" becomes the far end of a
dial that already exists.** No new terrain type, no third dimension on the hex grid, no new component. **One change, and
"dig in before they arrive" becomes a real decision** — which is the thing subterranean was interesting *for*.

⚠ **Explicitly NOT proposed:** a subterranean map layer, tunnel networks, or a depth axis on the hex grid. None is
needed to deliver that, and each is a new system to maintain. **If depth ever needs to be visible, it is a number on a
position, not a place.**

### 52d The build order, and the gauge for each

| # | Slice | Gauge |
|---|---|---|
| **W1** | `Ocean` gets its own terrain class (+ `GasLayers`) — cover stops favouring the attacker in water and locomotion starts being read | a terrain-classification test + the existing `GroundForcesTests` as the byte-identity tripwire on land |
| **W2** | Orbital fire reads `CoverDefenseMult` × `DefenseMult` — **the underground payoff, and it is one formula** | `GroundBombardmentTests` (extend: a fortified defender survives fire that kills an exposed twin) |
| **W3** | `Amphibious` becomes a locomotion **mode** with a land penalty; add `Submersible` | a locomotion-mode test; the stock four modes byte-identical |
| **W4** | Medium **opacity** on detection and on weapon range | a detection-range test at depth; a range-band test |

**W2 is the highest value per line in the whole list** — it is a single missing read, it uses only built machinery, and
it turns four existing systems (terrain · fortification · buildings · capture) into an answer to orbital bombardment.
