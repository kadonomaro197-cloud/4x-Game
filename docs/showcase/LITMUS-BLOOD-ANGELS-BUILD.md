# Litmus — Building a Blood Angels Space Marine (the closest the designer gets, 2026-08-01)

> **The ask:** make a Blood Angels Space Marine in the current designer and get as close as possible. This is
> the *build*, not another gap audit — every component is a real base-mod template with real dials, and the
> assembled unit's stats are worked out against the actual assembler math. The companion gap ledger is
> `docs/LITMUS-BLOOD-ANGELS-SQUAD.md`; this file shows what you *can* field today and reads the accuracy.
>
> **Headline:** it gets **a lot closer than the last time.** The designer was clearly tuned toward this exact
> test — the `power-armor` template's own description says *"the Space Marine story,"* and `sealed-systems`
> says *"the sealed power armour a Space-Marine chapter holds a vacuum world with."* The single mechanic that
> makes a marine a marine — **power armour's strength letting the frame carry a weapon a bare human couldn't**
> — is *literally* how the assembler's carry-budget gate works. What you get is a genuinely credible Blood
> Angels Assault Marine, monolithic-unit caveats aside.

---

## The target

The iconic Blood Angels 6-man is the **Assault Marine / Death Company / Sanguinary Guard** — jump-pack +
bolt pistol + chainsword, in sealed power armour, superhumanly fast, elite. I'll build that model.

---

## The build — component by component (real templates, real dials)

Everything below is a base-mod template you author in the Component Designer, then mount in the Entity
Assembler under **Ground Unit**.

### The frame — Infantry chassis (`GroundChassisAtb`)
| Dial | Value | Meaning |
|------|-------|---------|
| Locomotion | **Foot** | legs (the closest to a marine on the ground) |
| BaseStrength | **100** | the bare carry budget before augments |
| BaseHP | **200** | the frame's health before armour |

### Power Armour — the `power-armor` augment (`GroundAugmentAtb`) — Enhancers ▸ Bio-augmentation
| Dial | Value | What it does in the sim |
|------|-------|--------------------------|
| StrengthBonus | **+300** (max 3000) | **adds to the carry budget** — this is the whole marine story: it's what lets the frame lug a heavy weapon |
| ToughnessBonus | **+0.2** | multiplies the final HP pool ×1.2 |
| CarryMass | 30 | the suit's own weight against the budget |

### Sealed Systems — the `sealed-systems` augment — Enhancers ▸ Bio-augmentation
| Dial | Value | What it does |
|------|-------|--------------|
| Sealing | **1.0** | negates vacuum + toxic-atmosphere attrition — the sealed suit that holds a vacuum world (the template literally names Space Marines) |

### Reflex Booster — the `reflex-booster` augment — Enhancers ▸ Bio-augmentation
| Dial | Value | What it does |
|------|-------|--------------|
| EvasionBonus | **+0.4** | 40% harder to hit — the marine's superhuman reflexes. *Lore-accurate caveat: the template notes it's "useless against saturation fire"* — exactly right, a marine dodges a shot, not a barrage |
| CarryMass | 15 | against the budget |

### Ceramite plate — a `GroundArmorAtb` armour component — Defense ▸ (armour)
| Dial | Value | What it does |
|------|-------|--------------|
| HP | **+150** | adds to the health pool |
| Defense | **+40** | flat damage mitigation |
| VsKinetic / VsExplosive | **1.2 / 1.2** | ceramite shrugs solid rounds and blasts (nature tuning) |
| VsEnergy | 0.9 | a touch weaker to las/plasma (the tradeoff — nature tuning is zero-sum) |

### Bolter — the `ground-rifle` weapon (`GroundWeaponAtb`) — Weapons ▸ Ballistic
| Dial | Value | What it does |
|------|-------|--------------|
| Mode | **Ballistic** → resolves as **Kinetic** | a solid-round rifle |
| Attack | **80** (dialed up from the 40 base — a bolter is a heavy round) | sums into unit firepower |
| Range | **4 hexes** | fires as the marine closes |
| CarryMass | 10 | |

### Chainsword — the `claw-weapon` (`GroundWeaponAtb`) — Weapons ▸ Melee
| Dial | Value | What it does |
|------|-------|--------------|
| Mode | **Melee** (range 0) | resolves as **undodgeable** (Tracking-1) — you can't dodge a chainsword in your face |
| Attack | **60** (dialed up — a power melee weapon) | sums into firepower, fires in the melee band |
| CarryMass | 2 | |

---

## The assembled marine (worked against the real assembler math)

Running `GroundUnitAssembly.Compute` by hand with the values above:

- **Carry budget** = BaseStrength 100 + PowerArmour 300 = **400**
- **Max single item** = 400 × `MaxItemFraction` (0.5) = **200** → both weapons fit easily; and a *heavy* weapon
  (say Attack 300 → itemMass 30) that a bare frame (budget 100, max item 50) **could not lift** now fits. **This
  is the power-armour mechanic working exactly as the fiction says.**
- **Carry used** = bolter 10 + chainsword 2 + power-armour 30 + reflex 15 + sealed ~15 + plate ~20 ≈ **92 / 400**
  — comfortably within budget (room for a jump pack's mass, a grenade cadre, more plate).
- **Health** = (BaseHP 200 + plate 150) × (1 + ToughnessBonus 0.2) = 350 × 1.2 = **420 HP**
- **Defense** = plate **40** (flat mitigation, tuned ×1.2 vs kinetic/explosive)
- **Evasion** = reflex **0.40** (40% of non-saturation shots dodged)
- **Attack** = bolter 80 + chainsword 60 = **140**, resolving in **two range bands** — the bolter fires from
  4 hexes out as the marine closes, the chainsword hits (undodgeable) in contact
- **Environment** = **sealed** — fights on a vacuum or poison world where an unsealed levy bleeds out
- **Valid?** ✅ — passes the carry gate, the per-item gate, and (no energy weapon → no reactor needed) the
  supply gate. The design registers as a buildable `GroundUnitDesign` on the industry rails.

**Cradle-to-grave, all real:** mine iron → refine to stainless-steel + aluminium → author each component (the
dials above) → assemble the unit → build it at a colony (consuming steel/aluminium/BP, gauged by
`GroundUnitBaseModTests`) → field it on a planet (`RaiseUnit` snapshots the stats + folds the seal) → march it
in a formation with waypoint orders.

---

## Accuracy scorecard — how close is it?

### ✅ Captured (genuinely, in the sim — not paint)
- **Power armour as the carry-enabler** — the +300 strength that lets the marine wield heavy weapons is the
  literal assembler gate. This is the single most important marine mechanic and it's *real*.
- **Sealed suit / vacuum & toxic survival** — `sealed-systems`, a real dial, negates hostile-environment
  attrition. A marine holds an airless world; a militia doesn't.
- **Superhuman reflexes** — `reflex-booster` evasion, with the lore-perfect "useless vs saturation" caveat.
- **Toughness** — the power-armour HP multiplier + ceramite plate + per-nature soak (shrugs kinetic/blast,
  softer to plasma — a real matchup tradeoff).
- **Bolter + chainsword resolving in their own range bands** — ranged on the approach, undodgeable melee in
  contact. (Improved since the last litmus: the real-distance work means the two weapons fire at their own
  ranges, not one blended blob — though it's still one summed Attack, see ⚠️.)
- **Full cradle-to-grave** — every part mined, refined, designed, built from materials, fielded, marched.

### ⚠️ Approximated (there, but not the real thing)
- **Dual ranged+melee** — the two weapons *do* fire in separate range bands, but the unit still carries **one
  summed `Attack` and one dominant damage nature**, not two fully independent profiles. "Shoot then charge"
  reads correctly at the range level; the damage math is still pooled.
- **Elite quality** — no ground caliber/veterancy dial (`UnitCaliberAtb` is ship-only). You fake "elite" by
  dialing the augments and weapons higher, not with a proper veterancy multiplier.
- **Bolter's mass-reactive round** — Ballistic hardwires Kinetic; the bolter reads as a solid round, not a
  kinetic-round-that-detonates (no explosive-nature toggle on a kinetic weapon).
- **Jump pack** — a fast Foot (or Hover) chassis with a high SpeedFactor approximates the *speed*, but there's
  **no flight/leap/deep-strike axis** — the marine can't jump over terrain or drop into melee. Signature Blood
  Angels doctrine is only half-there.

### ❌ Still can't do (the real gaps)
- **A 6-model squad** — the unit is one monolithic HP bar (420 HP), not six marines you lose one at a time.
  A "squad" is six separate units marched together, not a squad object.
- **Population / gene-seed / training draw** — the marine builds from **steel, not people**. No recruit pool,
  no years of conversion, no gene-seed stock, no hard cap. Six marines are re-queueable, not irreplaceable.
- **Upkeep / consumption as it stands** — a fielded marine costs nothing and eats nothing; ammo never drains.
- **Psychology** — no Red Thirst, no Black Rage, no fear aura. Combat is deterministic strength-math; a marine
  never rages or breaks.
- **Consequential death** — losing the unit is a silent delete: no gene-seed recovery, no casualty event.
- **Grenades** — no thrown/consumable weapon concept.

---

## Bottom line

**You can build a Blood Angels Assault Marine today that is credible from the waist up.** The sealed,
strength-boosted, reflex-fast, ceramite-plated, bolter-and-chainsword marine assembles cleanly with real parts
and real dials, and the one defining mechanic — power armour carrying what flesh can't — is baked into the
math, not painted on. That's a real jump from the last reading ("a monolith with a marine's paint job"): the
`sealed-systems`, `reflex-booster`, and `power-armor` augments now exist and do exactly what the fiction
needs, and the range-band fix means the marine shoots on the approach and cuts in melee.

**Where it still isn't a *Blood Angels squad*:** it's one unit, not six models; it's built from metal, not
from a scarce gene-line; it pays no upkeep, feels no Red Thirst, and can't jump-pack over a wall. Those are the
same load-bearing gaps the squad audit ranked — and they're the four things (squad granularity, scarcity/upkeep,
jump flight, psychology) that turn a superb *soldier* build into an actual *Blood Angel*.

**Closeness, honestly: ~70% of one marine, ~40% of the squad.** The soldier is nearly there; the *Astartes
mythology* — the squad, the scarcity, the flight, the madness — is the remaining stretch, and every piece of it
is on the audit's build list (Design 2 settle/transport touches nothing here; but ground **squad granularity**,
**upkeep**, and a **ground caliber dial** are named, hooked gaps, not rebuilds).

---

*Built against base-mod templates in `installations.json` + `GroundUnitAssembly.Compute` (`GroundCombat/`),
2026-08-01. Companion: `docs/LITMUS-BLOOD-ANGELS-SQUAD.md` (the full cradle-to-grave gap ledger).*
