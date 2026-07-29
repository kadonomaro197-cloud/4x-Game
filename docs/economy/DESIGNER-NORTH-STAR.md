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

## 25. WHAT GOES OUT — the Propulsion output map (verified in source)

| What leaves the door | Goes to | Why it matters |
|---|---|---|
| **Thrust ÷ mass** | `CalculateEvasion` | 🔴 **the best defence in the game is bought HERE** |
| **Thrust** | `FleetManeuver` | decides **who dictates the range** in a closing fight |
| **Thrust** (again) → **sensor signature** | `SensorSignatureAtb(3500 K, magnitude = Thrust)` → **Detection** | 🔴 **the third leg of the trade (§23.2)** — the same dial that buys evasion sells your position. Detection decides who shoots first. |
| **Δv** | `FleetCombat.DeltaVFloor` → `ManeuverBudget` | the **kiting clock** — run dry and the enemy closes |
| **Warp max speed** | `WarpSpeedFloor` | strategic transit; the fleet moves at its slowest ship |
| **Ground speed factor** | `Speed_kmh` → the closing march | how fast you cross a battle's real metres |
| **Rough-ground handling** | march time **and** `LocomotionTerrainMult` | **one dial, two systems** |
| ~~**`Amphibious`**~~ | 🔴 **NOTHING** | **read by no code** (§23.3b) — ocean is impassable to everyone; the dial still charges mass |
| **Fuel burn** | **Logistics** — tankers, refuelling | a thirsty fleet is a supply problem |
| **Warp bubble cost** | **Power** (stored electricity) | a flat battery is a ship that cannot leave |
| **Drive mass** | **Chassis** budget — **and back into its own acceleration** | the only door whose output fights its own input |
| **The drive destroyed** | Damage | shoot the engine off and evasion collapses |

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

## 26a. RE-CHECKING THE EARLIER DERIVATIONS AGAINST §1a

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
| Sensors · Power · Enhancers · Industrial · Logistical · Civic · Command · Chassis | ⏳ owed | |
