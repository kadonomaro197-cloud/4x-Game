# The Resolver Simulator — what it is, what it models, and every number it borrows from the engine

**Companion to `docs/combat/resolversim.html`.** *(As of 2026-08-03. Author: the resolver-sim build pass.)*

---

## What it is, in one breath

The game's **auto-resolver** — the thing that fights fleet battles — runs **invisibly, in the background**. You press
play, and somewhere off-screen ships trade fire and die by the math in `Combat/CombatKernel.cs` and
`Combat/CombatEngagement.cs`. `resolversim.html` is a **window into that black box**: it runs the engine's *real*
combat arithmetic, on the screen, one salvo at a time, so you can watch a battle unfold, pause it, replay it, and see
exactly why one side won. It is the third tool in the chain — **the 12 door designers make components → the Entity
Assembler mounts them into a ship's totals → this simulator fights those totals** — and it reads the same handoff the
real resolver reads (`ShipCombatValueDB`: Firepower, Toughness, Evasion, Shields, per-weapon profiles).

**The whole point is fidelity.** Every formula and every tuning number in the simulator is copied from the engine
source, cited to its file and line. Where the engine has a **known bug or a deliberate gap**, the simulator shows the
engine-true (buggy) result and **flags it on screen** — it never quietly "fixes" the engine, because then you'd be
watching a game that doesn't exist. A red **BUG** or amber **GAP** badge is the honest thing.

---

## The engine in seven plain sentences (so the sim makes sense)

1. **A ship is whole-or-dead.** There's no "60%-wrecked but still fighting" — the per-component damage sim is parked, so
   a ship is either at full value or gone. This is the single biggest limitation, and the simulator honors it.
2. **There are two resolvers sharing one damage kernel.** The *arithmetic* (does the shot land, does a shield soak it,
   does armour bounce it) lives once in `CombatKernel.cs`; everything *around* it (range, fog, retreat, stats,
   multi-fleet fire-splitting) is two separate harnesses — space and ground.
3. **"AutoResolve" is a trap name.** `AutoResolve.cs` is the *stripped instant* path (no dodge, no shields, no retreat) —
   the off-screen quick-math. The watchable engine is `CombatEngagement.StepEngagementGroup`.
4. **Combat value is frozen at build.** A damaged-but-alive ship still rates at full strength — the direct consequence
   of whole-or-dead.
5. **The kernel is pure math** — no randomness, no clock — which is what makes a fast-forwarded battle come out
   identical to a watched one. The simulator is seeded and deterministic for the same reason.
6. **Missiles are stubbed.** In the auto-resolver a missile/torpedo counts for a flat **0.1 MJ/s** — the real
   GJ-scale impact only happens in the live-flight sim, which the auto-resolver doesn't run. A torpedo boat therefore
   *under-counts* here. The simulator flags this.
7. **The damage math is good; the gaps are all in the harness.** Across 14 combat scenarios, the kernel is uniformly
   correct — every GAP/PARTIAL is in the plumbing around it (range, fog, retreat, per-weapon stats, conservation,
   performance). That's the map of what to trust and what to flag.

---

## The three modes it runs

| Mode | Engine source | What it is |
|---|---|---|
| **Space — stepped** (the hero) | `CombatEngagement.StepEngagementGroup` (`:640`) | The real, watchable, salvo-by-salvo fleet battle: dodge, shields, ammo, heat, point-defense, doctrine, fire-splitting, retreat. This is "the auto-resolver" in practice. |
| **Space — instant** (compare) | `AutoResolve.Resolve` (`:74`) | The stripped off-screen quick-math: pure Firepower×time vs Toughness, combatants die first. **No dodge, no shields, and — verified — no SalvoDamageScale.** Run it on the same forces to see how much the stepped harness adds. |
| **Ground — region** | `GroundForcesProcessor.ResolveRegionCombat` (`:370`) | The planet-surface fight: terrain/cover/fortification divisors, ROE march stances, per-mount weapons. Carries the engine's **live 3-way double-count bug**, flagged. |

---

## The unit model (read this before trusting a number)

The engine measures damage and toughness in **joules** and firepower in **joules/second**, with a ship's toughness
being the sum of its components (100,000 J each). The Entity Assembler — the tool that produces the ship totals this
sim reads — works in a **game-scale abstraction**: firepower in **MJ/s**, shields in **MJ**, and a compact
**toughness** number (health + armour). So the simulator works in that same **MJ scale**:

- **Damage, toughness, shields → MJ** (this is the engine's joules ÷ 1,000,000). Because every damage formula in the
  kernel is *linear* in damage, scaling all three by the same factor changes **no** outcome — it's purely a display
  unit. Who wins, the dodge fractions, the shield-drain timing, the salvo count are all identical.
- **Velocities → m/s** and **ranges → metres**, exactly as the engine reads them. These are **not** scaled, because the
  dodge/closing math compares them against fixed engine references (a 1,000,000 m/s velocity reference, a 10-second
  flight-time reference). **One reconciliation:** the assembler writes a beam's velocity as `299792` (that's the speed
  of light in *km/s*); the kernel needs *m/s*, where a beam must sit far above the million-m/s reference to be
  undodgeable. So the sim uses engine-native **~3×10⁸ m/s** for beam-delivery weapons (light speed) and keeps the
  finite weapons (railgun 9,000, missile 14,000, plasma 40,000, flak 6,000 m/s) as authored. Source outranks the
  assembler wherever they would *behave* differently.
- **All the pure fraction/ratio constants** — the shield soak fractions, the armour 1.5/0.1, the 0.95 evasion cap, the
  0.1 salvo-damage scale, the 0.5 retreat threshold, the 0.95 point-defense cap — are used **exactly** as the source
  has them.

So: **the formulas and fraction-constants are byte-ported; the absolute magnitudes are the assembler's game scale.**
The behavior is engine-true; the numbers read in MJ.

---

## The honesty ledger — what the sim shows straight, and what it flags

**Shown engine-true (trust these):** the dodge/hit curve, the shield nature-matchup and drain/regen, flat armour with
penetration and the burst (alpha-vs-chip) split, the multi-fleet fire-split (`1/N`, firepower conserved), point-defense
interception of guided fire, ammo drain to silence, heat throttle, doctrine multipliers, the retreat threshold, the
closing-range accuracy falloff, and whole-or-dead casualties bucketed by combat value.

**Flagged GAP/BUG (the engine is like this — the sim shows it and says so):**
- 🔴 **BUG — ground 3-way double-count.** On the surface, a unit facing two enemy factions applies its *full* damage
  pool to *each* — 2× in a 3-way fight (the pool is rebuilt inside the nested faction loops). Space is conserved; ground
  is not. Flagged in ground mode.
- 🟠 **GAP — missile damage stub.** 0.1 MJ/s in the auto-resolver; the real impact is live-flight only. A torpedo-heavy
  ship (the Sovereign) reads weaker here than it fights.
- 🟠 **GAP — space armour is proportional, not per-shot.** On a ship, armour folds into toughness and soaks a *fraction*;
  the flat per-shot bounce (one big alpha punches, a swarm bounces) only actually happens on the **ground** — the space
  path hard-zeros per-shot energy. A deliberate v1 deferral.
- 🟠 **GAP — ground has no retreat and no battle log.** Its only stances are Hold / Close / Stand-off; it can't break off,
  and it records no play-by-play.
- 🟠 **GAP — ground has no detection gate.** Space can shoot a blind target that can't reply (fog-of-war first-strike);
  ground's first-strike is range-only.
- 🟠 **GAP — whole-or-dead.** No partial-hull tracking, in any mode. A ship is full value or gone.

**Never faked:** if the engine can't do a thing (crack a planet, launch a strike wing, model per-component wounds), the
sim does not pretend it can — it flags it.

---

## How to drive it

Pick a **scenario** (or the default handoff battle), pick a **mode**, then **Step** one salvo at a time or **Play** to
watch it run; **Fast-forward** resolves it; **Replay** re-runs the same seed identically. The **event log** narrates each
salvo in plain English ("Republic fleet took heavy-turbolaser + torpedo fire — 41% on target, 62 MJ dealt; destroyed
'Acclamator #2'"), the **damage ledger** totals what each side dealt / soaked / landed, and the **weapon table** shows the
per-weapon ten-field profile the kernel reads. Every gauge with a flagged caveat carries its badge.

---

## The verification ledger (constants + formulas, re-verified at file:line)

*See `## Ported constants` and `## Ported formulas` below — every number carries its `file:line`, and the kernel
cross-check table (hand-computed from the C# source, asserted against the JS port) is in `## Kernel cross-check`.*

*(These tables are filled from the source read + the extraction pass; the kernel section is verified in full against
`CombatKernel.cs` read end-to-end.)*
