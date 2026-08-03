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

## Ported constants — every number, cited to the source line it was re-verified against

These are the tuning numbers the simulator runs on. Each was read in the C# source at the line shown (Phase 2 of the
build — the whole `CombatKernel.cs` end-to-end, plus the specific lines in the other files). The simulator uses each
one **exactly** as written.

| Source | Constant | Value | What it does |
|---|---|---|---|
| `CombatKernel.cs:38` | `VelocityReference_mps` | 1,000,000 | shot speed at which a weapon half-defeats evasion (a beam is far above → always hits; a slug far below → dodgeable) |
| `CombatKernel.cs:42` | `SaturationReference` | 50 | rate-of-fire at which volume half-guarantees a hit regardless of dodge (flak is far above) |
| `CombatKernel.cs:71` | `MinLandedFraction` | 0.05 | floor on the fraction of fire that lands — enough volume kills any dodger (kept = 1 − EvasionCap, the ×20 ceiling) |
| `CombatKernel.cs:76` | `FlightTimeReference_s` | 10 | the "accuracy falls off with distance" half-time; inert at separation 0 |
| `CombatKernel.cs:82` | `RangeBaseMiss` | 0.9 | evasion-independent base miss at range (the one mutable dial — the kernel's single purity break) |
| `CombatKernel.cs:87–93` | `ShieldSoakVs` K / E / X / Exotic | 1.0 / 0.5 / 0.75 / 0.0 | the shield **nature matchup**: kinetic stopped best, energy bleeds through, explosive partly bypasses, exotic ignores the shield entirely |
| `CombatKernel.cs:100` | `ArmourSoakPerPoint` | 1.5 | damage soaked off **each** incoming source, per point of armour (flat-per-source = the swarm-vs-alpha identity) |
| `CombatKernel.cs:103` | `ArmourMinPassFraction` | 0.1 | a source always lands ≥ this fraction — armour is never total immunity (the ×10 armour ceiling) |
| `CombatKernel.cs:308` | `BurstSoakMaxShots` | 1000 | cap on how many shots a burst is split into for the per-shot soak |
| `CombatEngagement.cs:103` | `SalvoDamageScale` | 0.1 | **the combat-pace dial** — only a tenth of a salvo's raw energy counts toward kills, so a fight plays out over ~10× more salvos. Stepped path **only** |
| `CombatEngagement.cs:110` | `AmmoBurnKgPerJoule` | 1e-4 | magazine kilograms drained per joule of ammo-fed (kinetic/explosive) fire |
| `CombatEngagement.cs:115` | `HeatDissipationFraction` | 0.5 | fraction of radiator capacity shed as cooling each salvo |
| `CombatEngagement.cs:120` | `HeatThrottleFloor` | 0.1 | an overheating fleet still fires at least this fraction of its energy weapons |
| `CombatEngagement.cs:1459` | `PointDefenseMaxIntercept` | 0.95 | hard cap on the missile fraction point-defense can shoot down — a big enough swarm always leaks |
| `CombatEngagement.cs:48` | `RetreatCasualtyThreshold` | 0.5 | a fleet breaks off after losing this fraction of the ships it started with |
| `CombatEngagement.cs:81` | `FallbackBeamVelocity_mps` | 1e8 | an old-style (profile-less) ship fires as this light-speed always-hit beam |
| `AutoResolve.cs` | `RoundSeconds` / `MaxRounds` | 5.0 / 2000 | instant path: seconds of fire per round; the round cap |
| `ShipCombatValueDB.cs:38` | `MissileLauncherFirepowerStub` | 100,000 J/s | a missile launcher's flat firepower = **0.1 MJ/s** (the flagged torpedo stub) |
| `ShipCombatValueDB.cs:96` | `EvasionCap` | 0.95 | hard ceiling on how hard a ship is to hit |
| `ShipCombatValueDB.cs:100` | `LightSpeed_mps` | 299,792,458 | the real beam muzzle velocity — this is what makes a beam undodgeable |
| `GroundForcesProcessor.cs:35` | `SalvoScale` (ground) | 1.0 | ground combat-pace dial — **full**, vs space's 0.1 (ground fights resolve faster) |
| `GroundForcesProcessor.cs:40` | `AmmoPerSalvo_kg` | 1.0 | ammo a magazine-fed unit burns per salvo it fires |
| `WeaponClassifier.cs:19 / 23` | Beam / Flak thresholds | 1e7 m/s / 50 sat | split the Bolt/Slug family into the computed weapon **Class** readout |

## Ported formulas — the arithmetic, in plain English

Each of these is a one-to-one copy of the C# function at the cited line. The simulator's JavaScript is the same math.

- **Does the shot land?** `HitFraction` (`CombatKernel.cs:196`). A fast or well-tracking weapon defeats evasion (a
  beam ignores it entirely); a slow ballistic slug is dodged by a nimble target; high rate-of-fire (flak) floors the
  result so *something* always lands; and once a closing separation is in play, accuracy falls off with distance for
  anything that isn't a beam or guided. Point-blank (separation 0) the distance term is inert.
- **How much of a mixed salvo lands?** `LandedFraction` (`:223`) — the damage-weighted average of `HitFraction` over
  every weapon in the incoming fire.
- **How much does a shield stop?** `SoakFractionOf` (`:237`) rolls the nature matchup up over the salvo; `ResolveShield`
  (`:253`) drains the pool by the soakable part (up to its charge), then regenerates it toward capacity.
- **How much does armour bounce?** `ArmourSoak` (`:294`) subtracts a flat amount per source (penetration cancels armour
  first, point-for-point; an out-penned shot passes in full); `BurstShotCount` (`:315`) + `ArmourSoakBurst` (`:331`)
  split one weapon's fire into N equal shots and soak each flat — so a swarm of chips bounces where one big alpha of the
  same total punches through.
- **Which weapon reaches?** `WeaponReaches` (`:174`) — a 0-range weapon is unbounded (the beam convention); a finite
  one fires only once the gap is within its range.

## Kernel cross-check — hand-computed from the C# source, asserted against the JS on every load

The simulator runs these eight checks the instant it loads (the badge on the battlefield header shows the result — a
green `kernel ✓ 8/8`). Each expected value was worked out by hand from the C# source; if the JavaScript port ever
drifts from the engine math, the badge goes red. This is the Visibility Gate applied to the simulator itself.

| # | Call | Expected | Proves |
|---|---|---|---|
| T1 | `HitFraction(beam vel 3e8, trk 0.72, sat 34; ev 0.5)` | 0.99834 | a beam is undodgeable (light-speed ≫ the 1e6 reference) |
| T2 | `HitFraction(railgun vel 9000, trk 0.55, sat 26; ev 0.5)` | 0.77500 | a finite slug **is** dodged by an evasive target |
| T3 | `HitFraction(vel 6000, trk 0, sat 180; ev 0.95)` | 0.78261 | high saturation (flak) floors the hit even vs a perfect dodger |
| T4 | `HitFraction(railgun; ev 0, separation 100 km)` | 0.78684 | accuracy falls off with distance even against a sitting target |
| T5 | `ResolveShield(pool 25, cap 25, regen 0.5, salvo 40, soak 0.5, dt 5)` | absorbed 20, pool → 7.5 | shield drains the soakable part then regenerates |
| T6 | `ArmourSoak(armour 10, src 100)` | 85 | flat armour soaks `10 × 1.5 = 15` off one source |
| T7 | `ArmourSoak(armour 10, src 100, pen 10)` | 100 | an out-penned shot passes in full (armour cancelled) |
| T8 | `ArmourSoakBurst(armour 10, src 100, shots 10)` | 10 | the same 100 as ten chips is almost entirely bounced (alpha-vs-chip) |

## The 14-row fidelity walk — how the sim handles each combat scenario

The auto-resolver ground-truth doc (`docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §8) grades the engine against 14
"can it handle X?" scenarios. Here is how the **simulator** handles each — i.e. whether watching the sim is watching
the engine for that case:

| # | Scenario | In the sim |
|---|---|---|
| 1 | One ship vs one ship | ✅ n=2 stepped path (mirror/screen scenarios reduce to it) |
| 2 | Fleet vs fleet | ✅ the handoff battle — multi-ship fleets, bucketed casualties |
| 3 | Multi-party (3+ sides) | ✅ space fire-split (÷ targets); ground shows the **double-count BUG** (3-way scenario) |
| 4 | Dodge (evasion decides who's hit) | ✅ `HitFraction` per bucket; the Acclamators (ev 0) are hit more than the Venator (ev 0.12) |
| 5 | Shields (nature matchup) | ✅ `ApplyShield` — the Sovereign's 120 MJ shield half-bleeds vs the Republic's energy beams |
| 6 | Point-defense vs missiles | ✅ `InterceptMissiles` (0 on the presets — add PD in the Forces tab to see it engage) |
| 7 | Ammo depletion | ✅ implemented (0 magazine on the presets → no-op; engine-faithful) |
| 8 | Heat / sustained-fire throttle | ✅ implemented (0 heat on the presets → no-op; engine-faithful) |
| 9 | Doctrine multipliers | ➖ folded into firepower; per-fleet posture is a Forces-tab extension |
| 10 | Retreat (break off) | ✅ the 0.5 casualty threshold ends a fleet (space); ➖ ground has none (flagged GAP) |
| 11 | Closing range / accuracy falloff | ✅ the range term is in `HitFraction` (separation 0 by default = point-blank) |
| 12 | Instant vs stepped divergence | ✅ the two space modes on the same forces — instant resolves in 1 round, stepped over many salvos |
| 13 | Ground region combat | ✅ the ground mode — terrain/cover/fortification divisor, per-mount weapons, flat armour |
| 14 | Whole-or-dead casualties | ✅ every mode — ship bars are binary (full value or gone); only ground-unit *health* drains |

*(Verified: `CombatKernel.cs` read end-to-end; every constant/formula above re-verified at its cited line; the JS
port asserted against the C# by the T1–T8 cross-check on every load, plus a headless run of all three modes across
every scenario with zero throws, and a browser pass in both themes with zero console errors.)*
