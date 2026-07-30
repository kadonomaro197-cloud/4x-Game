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
| ~~**1**~~ | ✅ **Sensors — DERIVED 2026-07-30 (PART FIVE §32–36).** Four dials fail the write-something test (`Resolution` dead AND free · `Scan Time` a free ladder · the jammer's self-signature opt-out · two fire-control size dials that cost mass and write nothing, a free 16× saving). One trap (the wavelength dial can blind a sensor silently), one honest-but-unpublished trade (bandwidth = coverage ↔ reach), one cost-law conflict (ground linear vs space quadratic). Six cheap slices named in §35, none built yet. | `Sensors/` SensorReceiverAtb · SensorSignatureAtb · CloakAtb · JammerAtb; `Weapons/BeamFireControlAtbDB`; `GroundCombat/GroundSensorAtb`; `GeoSurveys/GeoSurveyAtb`; `JumpPoints/GravSurveyAtb`; `Factions/IntelDirectorateAtb` | **~9 — largest** | **The context is already loaded.** Three findings this week land here (signature = thrust §23.2 · warp signature §26c.1 · detection range goes as **√magnitude**). It is also the door that decides **who shoots first**, which every combat finding has leaned on — and it holds the one thing we deliberately deferred: **FTL needs its own band AND a receiver that can see it** (§26c.1). Biggest single payoff. **➡ And it now formally inherits `GravSurveyAtb` — the developer moved the jump-point surveyor OFF Propulsion (§26d.3): finding a node is detection, not movement.** |
| **2** | **Power** | `Energy/` EnergyGenerationAtb · EnergyStoreAtb · EnergySolarGenerationAtb | **3 — smallest** | It just gained **two fresh consumers** — warp bubble creation/sustain (§26b) and weapon energy draw. Deriving it **closes loops we opened this week** rather than opening new ones. Fast, and the supply side of two live demands. |
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
| **Power** | Healthy but **under-dialled** — likely one honest trade (**output ↔ storage**, or output ↔ mass) and little else. May turn out to need *adding*, like Propulsion. |
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

## 34. STEP 3–4 — BOTH TESTS APPLIED, AND FOUR DIALS FAIL

The §1 tests are ① *which sim variable does it write? (none + costs nothing ⇒ a bug)* and ② §1a *can it be set knowing
only this part?* **Four dials fail test ①, and two of those four are exploitable.** Every one is verified in source.

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

### 34.5 ⚠ The wavelength dial is a TRAP — it can blind a sensor with no warning

`Ideal Detection Wavelength` ranges **0.01 to 1e12** nm. But every signature in the game is authored at 3500 K, which
Wien's law puts at **2898000 / 3500 ≈ 828 nm**, spanning `[428, 828, 1428]`. The receiver's band is
`peak ± bandwidth/2`, so the shipped 600 nm ± 125 gives `[475, 725]` — which overlaps, and detection works.

**Tune the peak to 2000 nm and the band becomes `[1875, 2125]`, which overlaps nothing in the game. The sensor is
totally blind, and the designer says nothing.** A dial whose valid range is a narrow undocumented window inside a
1e12-wide slider is a trap, not a choice.

**Two ways out, and they point in opposite directions** — this is a developer call:
- **(a) Hide it.** If there is only ever one band worth tuning to, the dial is not a decision — compute it and remove
  the slider (the §1 test says a dial nobody can meaningfully set is not a dial).
- **(b) Make it real.** Give different emitters genuinely different bands, so tuning is a *counter-intelligence*
  decision: a sensor tuned for thruster plumes is blind to a cold hull. **This is the same deferred question §26c.1
  flagged** — FTL wants its own band, and it needs a receiver that can see it. One ruling closes both.

### 34.6 ✅ The one HONEST trade already in the door — and nothing tells the player

`Detection Bandwidth` writes **two** things:

```
band width  = peak ± bandwidth/2                                        ← how many KINDS of signal you can see
Efficiency  = techMaxBandwidth / bandwidth
Sensitivity = tech / (EffectiveSize² × Efficiency)                       ← lower is BETTER
            = tech × bandwidth / (EffectiveSize² × techMaxBandwidth)
```

So **a wider band sees more kinds of thing, each from less far.** That is a genuine zero-sum coverage ↔ reach trade,
**already shipping**, and the designer presents it as two unrelated numbers. Wiring the readout is a pure §? Failure-A
(the number exists, it is just unwired — `docs/combat/INFORMATION-DELTA-DESIGN.md`).

### 34.7 ✅ And the size dial is already honest: **range ∝ √mass**

```
Sensitivity ∝ 1 / AntennaSize²        and     range ∝ √(1/Sensitivity) ∝ AntennaSize
Mass        = 90 + 0.01 × AntennaSize²
⇒  range ∝ √mass  —  DOUBLE YOUR REACH, QUADRUPLE THE ANTENNA.
```

**The inverse-square law shows up on both the physics side and the cost side, and they agree.** Nothing to fix; it
just needs saying, because it is the reason a big sensor is a real commitment rather than a shopping choice.

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
| **S1** | **Price `Self Signature Boost` into `Sensitivity Degrade`** — one number, the jammer's noise *is* its self-signature | a formula change in one template; the atb already takes both args | 🔴 **The jammer gets its downside back.** Blinding the enemy paints you, and you cannot opt out. |
| **S2** | **Make the two fire-control size dials write something, or delete them** | they appear in exactly one formula | 🔴 **Closes a free 16× mass saving before Chassis derives against that budget.** |
| **S3** | **Give `Scan Time` a cost** (power draw or mass) | one formula; `EnergyGenAbilityDB` is already the consumer for the solar branch | **Sweep-often-and-run-hot vs sweep-rarely-and-stay-cold** becomes a real EMCON decision. |
| **S4** | **Wire `Resolution`** into `SignalQuality` — resolution is what turns *"something"* into *"three destroyers"* | `SensorReturnValues.SignalQuality` already exists and survey reveal already gates on it | **Contact fidelity becomes a purchase.** A cheap sensor sees a blob; a good one counts hulls — which is what makes a scout worth building. |
| **S5** | **Publish the bandwidth trade** as a readout (coverage ↔ reach) | Failure-A: the number exists, it is unwired | The one honest dial in the door starts reading as a decision. |
| **S6** | **Move `IntelDirectorateAtb` to Command** (§33.1) | a doc/ownership move, no code | Keeps the door's question clean; Command inherits it with the other seats. |

**Blocked on a developer ruling, not on work:** §34.5 (hide the wavelength dial, or make bands real — one ruling also
closes the deferred FTL-band question) and §34.8 (one cost law for reach, or two).

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
