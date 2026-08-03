# The Resolver Simulator — the Arena: what it models, and every number it borrows from the engine

**Companion to `docs/combat/resolversim.html`.** *(As of 2026-08-03. Rebuilt to the LD-30 arena model on the
developer's ruling: the sim shows the opposing-sides battle at true engagement ranges — nothing else.)*

---

## What it is, in one breath

A battle is **two fleets that start on opposite sides and close**. The simulator draws exactly that: an **arena**
(`AUTO-RESOLVER-GROUND-TRUTH §5, the LD-30 model`) — the two sides open at opposite ends of a diameter, the diameter
is **the longest weapon range in the fight**, and then they **close at their real speeds**, firing each weapon only
when the enemy is inside that weapon's **true range**. You watch the gap count down from 400 km through the weapon
bands — torpedo → heavy turbolaser → ion → medium turbolaser → laser → point-defense — and see the fight decided as
they merge. The damage underneath the movement is the engine's **real** combat kernel, copied line-for-line.

**The one rule the arena is built on (LD-30, the developer's words, 2026-07-29):**
> "the circle doesn't close. It is the arena, not a gap. Inside it the resolver is a live simulation — units move at
> their own real speeds, as though it was an RTS." **The arena is bounded by RANGE; the fight is resolved by MOVEMENT.**

---

## How the arena works — the model, step by step

1. **Size the arena by range.** The diameter = the longest weapon range present. In the handoff battle that's the
   **Proton Torpedo at 400 km**, so the two fleets open 400 km apart. (A joiner with a longer weapon would *expand*
   the arena, never shrink it — LD-30. The circle never closes.)
2. **Deploy on opposite ends.** Side A's ships cluster at one end of the diameter, side B's at the other.
3. **Close at real speed.** Each fleet moves as one, at its **slowest ship's speed** (a fleet is only as fast as its
   slowest hull — the engine's `FleetCombat.DeltaVFloor`), toward the gap its **doctrine** wants:
   - **Close in** (default) — drive to knife range.
   - **Stand off** — hold at your **dominant weapon's range** (where the bulk of your firepower lives) and kite if
     you're faster.
   - **Hold** — don't move.
   - **Withdraw** — break off and run: open the gap and try to clear the fight (see step 6).
   The faster fleet dictates the range; a faster long-gun fleet can kite a slower brawler it out-ranges.
4. **Fire at true range.** Each salvo, a weapon shoots **only if the current gap is inside its range**
   (`WeaponReaches`). Long weapons open the fight; short ones wait for the merge. A **beam** (light-speed) is
   undodgeable and loses **no** accuracy at range; a **slug or torpedo** loses accuracy the longer it has to fly
   (the flight-time term). So the closing fight has a real shape: torpedoes plink from far out, turbolasers decide
   it at range, point-defense and lasers finish it up close.
5. **Resolve the damage with the real kernel** — dodge, shield nature-matchup, point-defense interception of guided
   fire, fire-split conservation (an attacker facing two targets divides its guns, never doubles them), the flat
   armour bounce, and **whole-or-dead** casualties.
6. **Break off only on an order.** There is **no automatic casualty retreat** — everyone fights until the battle is
   decided. A fleet leaves only when you set its doctrine to **Withdraw**, and then it runs from the enemy and clears
   the fight *only if it can open the gap past the longest weapon range* — i.e. **only if it is faster** than its
   pursuers. Order a slower fleet to withdraw and it gets caught and ground down as it flees. (That mirrors the
   engine: retreat is a withdraw *doctrine*, not a threshold.)

Each weapon fires at its **own** range, so as the sides close the guns light up **longest-first** — the log calls out
each one as it "opens up," and the arena draws a labelled range band per weapon. The **step size** (1 / 2 / 5 s) sets
only how finely time is sliced; because the kernel math is per-second, a finer step is smoother but changes **no**
outcome.

**The whole point is fidelity.** Every damage formula and tuning number is copied from the engine source, cited to
its file and line, and cross-checked on load. Where the engine has a **deliberate gap** (the torpedo stub, the
proportional space armour, whole-or-dead), the sim shows the engine-true result and **flags it** — it never quietly
"fixes" the engine.

---

## The one honest seam: ranges are engine-true, speed is a stand-in

Everything about *who can hit whom* is the engine's: the **weapon ranges** are the real authentic-closing ranges from
`WeaponProfile.cs` (torpedo 400 · heavy-turbo 220 · ion 160 · med-turbo 140 · railgun 80 · laser 60 · PD 12 km), and
the **damage kernel** is byte-ported. The one thing the engine does **not** expose as a spec-sheet field is a ship's
sublight **closing speed** — the engine derives it from `NewtonThrustAbilityDB` (thrust ÷ mass). Rather than fake a
`ShipCombatValueDB` field that isn't there, the sim uses an **editable per-class stand-in** (Venator 2.0, Acclamator
3.0, Sovereign 2.6 km/s), shown on every ship card and tunable in the **Forces** tab. That is the single number in the
whole tool that isn't read straight off the engine's combat contract, and it is flagged as such on screen.

---

## The unit model (read this before trusting a number)

The engine measures damage and toughness in **joules** and firepower in **joules/second**. The Entity Assembler — the
tool that produces the ship totals this sim reads — works in a game-scale abstraction: firepower in **MJ/s**, shields
in **MJ**, a compact **toughness** number. So the simulator works in that same **MJ scale**:

- **Damage, toughness, shields → MJ** (the engine's joules ÷ 1,000,000). Because every damage formula in the kernel is
  *linear* in damage, scaling all three by the same factor changes **no** outcome — it's purely a display unit. Who
  wins, the dodge fractions, the shield-drain timing, the salvo count are all identical.
- **Velocities → m/s** and **ranges → metres**, exactly as the engine reads them — **not** scaled, because the
  dodge/closing math compares them against fixed engine references (a 1,000,000 m/s velocity reference, a 10-second
  flight-time reference). Beam-delivery weapons run at engine-native **~3×10⁸ m/s** (light speed) so they sit far above
  the velocity reference and are undodgeable; the finite weapons keep their authored m/s.
- **All the pure fraction/ratio constants** — shield soak fractions, armour 1.5/0.1, the 0.95 evasion cap, the 0.1
  salvo-damage scale, the 0.5 retreat threshold, the 0.95 point-defense cap — are used **exactly** as the source has
  them.

So: **the formulas and fraction-constants are byte-ported; the absolute magnitudes are the assembler's game scale.**

---

## The honesty ledger — what the sim shows straight, and what it flags

**Shown engine-true (trust these):** the dodge/hit curve, the **range-accuracy falloff over the closing flight**
(beams immune, slugs/torpedoes degrade), the **per-weapon true-range gate** (each gun fires only inside its own
range — missiles open the fight, PD closes it), the shield nature-matchup and drain/regen, flat armour with
penetration and the burst (alpha-vs-chip) split, the multi-fleet fire-split (`1/N`, firepower conserved),
point-defense interception of guided fire, **doctrine-ordered break-off** (Withdraw — no auto-retreat; you escape
only if you outrun the enemy), doctrine (Close / Stand-off / Hold / Withdraw) driving the movement, and whole-or-dead
casualties bucketed by combat value.

**Flagged (the engine is like this — the sim shows it and says so):**
- 🔵 **MODEL — this is the arena TARGET, not the shipped default.** The sim implements LD-30, the opposing-sides arena
  the developer ruled the resolver *must* become. The shipped default resolver still fights at **point-blank** (its
  closing model collapses to one shrinking scalar and is off by default). That model is superseded here, deliberately.
  The **damage math** below the movement is the engine's real kernel, byte-for-byte.
- 🟠 **GAP — closing speed is the one non-spec-sheet input** (see the seam above).
- 🟠 **GAP — missile/torpedo damage is a stub.** 0.1 MJ/s in the auto-resolver (`MissileLauncherFirepowerStub =
  100,000 J/s`); the real GJ-scale impact is live-flight only. The torpedo-heavy Sovereign — which opens the fight at
  400 km — reads *weaker* here than it fights.
- 🟠 **GAP — space armour is proportional, not per-shot.** On a ship, armour folds into toughness and soaks a
  *fraction*; the flat per-shot bounce only actually runs on the ground (the space path hard-zeros per-shot energy). A
  deliberate v1 deferral.
- 🟠 **GAP — whole-or-dead.** No partial-hull tracking. A ship is full value or gone (combat value frozen at build).

**Never faked:** if the engine can't do a thing, the sim does not pretend it can — it flags it.

---

## How to drive it

Pick a **scenario** (default: the handoff battle), set each side's **doctrine** (Close / Stand-off / Hold), then
**Step** one salvo at a time or **Play** to watch it close and fight; **Resolve** fast-forwards to the result;
**Replay** re-runs the same seed identically. Watch the **arena** up top — the two clusters closing, the shaded
weapon-reach bars, the **gap** counting down. The **battle log** narrates each salvo with the current gap; the
**Weapons & ranges** tab shows every weapon sorted by range with an **IN RANGE** flag as the gap crosses it; the
**Forces** tab lets you edit any stat (including speed) and **Replay**. It is deterministic — Resolve and Play reach
the identical result, because the kernel has no RNG and no clock.

---

## Ported constants — every number, cited to the source line it was re-verified against

The simulator uses each one **exactly** as written in the C# source (re-verified at the cited line).

### The damage kernel (ported, verified)

| Source | Constant | Value | What it does |
|---|---|---|---|
| `CombatKernel.cs:38` | `VelocityReference_mps` | 1,000,000 | shot speed at which a weapon half-defeats evasion (beam far above → always hits; slug far below → dodgeable) |
| `CombatKernel.cs:42` | `SaturationReference` | 50 | rate-of-fire at which volume half-guarantees a hit regardless of dodge (flak far above) |
| `CombatKernel.cs:71` | `MinLandedFraction` | 0.05 | floor on the fraction of fire that lands (= 1 − EvasionCap, the ×20 ceiling) |
| `CombatKernel.cs:76` | `FlightTimeReference_s` | 10 | the range-accuracy half-falloff time over the shot's flight |
| `CombatKernel.cs:82` | `RangeBaseMiss` | 0.9 | evasion-independent base miss at range (the one mutable dial — the kernel's single purity break) |
| `CombatKernel.cs:87–93` | `ShieldSoakVs` K / E / X / Exotic | 1.0 / 0.5 / 0.75 / 0.0 | the shield **nature matchup**: kinetic stopped, energy bleeds, explosive partly bypasses, exotic ignores it |
| `CombatKernel.cs:100` | `ArmourSoakPerPoint` | 1.5 | damage soaked off **each** incoming source, per point of armour (the swarm-vs-alpha identity) |
| `CombatKernel.cs:103` | `ArmourMinPassFraction` | 0.1 | a source always lands ≥ this fraction — armour is never total immunity (the ×10 ceiling) |
| `CombatKernel.cs:308` | `BurstSoakMaxShots` | 1000 | cap on how many shots a burst is split into for the per-shot soak |
| `CombatEngagement.cs:103` | `SalvoDamageScale` | 0.1 | **the combat-pace dial** — a tenth of a salvo's raw energy counts toward kills, so a fight plays out watchably |
| `CombatEngagement.cs:1459` | `PointDefenseMaxIntercept` | 0.95 | hard cap on the missile fraction point-defense can shoot down — a big enough swarm always leaks |
| `CombatEngagement.cs:48` | `RetreatCasualtyThreshold` | 0.5 | a fleet breaks off after losing this fraction of the ships it started with |
| `ShipCombatValueDB.cs:38` | `MissileLauncherFirepowerStub` | 100,000 J/s | a missile launcher's flat firepower = **0.1 MJ/s** (the flagged torpedo stub) |
| `ShipCombatValueDB.cs:96` | `EvasionCap` | 0.95 | hard ceiling on how hard a ship is to hit |
| `ShipCombatValueDB.cs:100` | `LightSpeed_mps` | 299,792,458 | the real beam muzzle velocity — what makes a beam undodgeable |
| `WeaponClassifier.cs:19 / 23` | Beam / Flak thresholds | 1e7 m/s / 50 sat | split the Bolt/Slug family into the computed weapon **Class** readout |

### The arena model (LD-30)

| Source | What | Value | Meaning |
|---|---|---|---|
| `AUTO-RESOLVER-GROUND-TRUTH §5` | LD-30 arena | opposite ends of a diameter | the two sides open on a diameter bounded by the longest weapon range; the circle does **not** close |
| `§5 LD-30` | closing = movement | units at real speeds | the fight is resolved by units moving, not by one shrinking gap |
| `WeaponProfile.cs` | true weapon ranges | torpedo 400 / heavy-turbo 220 / ion 160 / med-turbo 140 / railgun 80 / laser 60 / PD 12 km | the engagement bands the arena closes through |
| `FleetCombat.DeltaVFloor` | fleet speed | min over ships | a fleet moves as one, bound by its slowest hull |
| `NewtonThrustAbilityDB` (stand-in) | closing speed | 2,000–3,000 m/s | per-class editable — the one input not on a spec sheet |
| `ARENA` (sim) | `MinGap_m` / `KiteMargin` | 3,000 m / 0.9 | knife-range floor; "stand off" holds at its dominant range × this |

---

## Ported formulas — the arithmetic, in plain English

Each is a one-to-one copy of the C# function at the cited line.

- **Does the shot land?** `HitFraction` (`CombatKernel.cs:196`). A fast/well-tracking weapon defeats evasion (a beam
  ignores it); a slow ballistic slug is dodged; high rate-of-fire (flak) floors the result so *something* always
  lands; and once a closing separation is in play, accuracy falls off with distance for anything that isn't a beam or
  guided.
- **How much of a mixed salvo lands?** `LandedFraction` (`:223`) — the damage-weighted average of `HitFraction`.
- **How much does a shield stop?** `SoakFractionOf` (`:237`) rolls the nature matchup over the salvo; `ResolveShield`
  (`:253`) drains the pool by the soakable part, then regenerates toward capacity.
- **How much does armour bounce?** `ArmourSoak` (`:294`) subtracts a flat amount per source (penetration cancels
  armour first; an out-penned shot passes in full); `BurstShotCount` (`:315`) + `ArmourSoakBurst` (`:331`) split one
  weapon's fire into N equal shots and soak each flat — a swarm of chips bounces where one alpha of the same total
  punches through.
- **Which weapon reaches?** `WeaponReaches` (`:174`) — a finite weapon fires only once the gap is within its range;
  this is the gate the whole arena is built on.

---

## Kernel cross-check — hand-computed from the C# source, asserted against the JS on every load

The simulator runs these eight checks the instant it loads (the badge on the battlefield header shows `kernel ✓ 8/8`).
Each expected value was worked out by hand from the C# source; if the port ever drifts from the engine math, the badge
goes red. This is the Visibility Gate applied to the simulator itself.

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

## The handoff battle, as the arena resolves it

Default-open: **Republic (1 Venator + 2 Acclamators) vs Federation (2 Sovereigns)**, both closing. The fight opens
400 km apart with **only the Proton Torpedoes reaching** (the stub — near-harmless plinking, shields holding); the
gap counts down ~18 km a salvo; at **220 km the Republic's heavy turbolasers open up** and start hammering the
Federation for ~18 MJ a salvo *while the Federation can still only answer with torpedoes* — its medium turbolasers
don't bear until 140 km. That 80 km of one-sided fire is the whole game. With no auto-retreat, the Federation **fights
to the end and is wiped** (both Sovereigns), the Republic losing one Acclamator (≈33 salvos at the 2 s step).
Now try the doctrine lever: order the **Federation to Withdraw** and — being *faster* (5 vs 4 km/s) — it breaks
contact and escapes intact; order the slower **Republic to Withdraw** and it can't shake the Federation, so it fights
anyway. The doctrine is the fight.

*(Verified 2026-08-03: `CombatKernel.cs` read end-to-end; every constant/formula re-verified at its cited line; the JS
port asserted against the C# by the T1–T8 cross-check on every load. Headless DOM-stub runs confirm **0 throws** and
kernel **8/8**, and assert the four behaviours: the default fight runs **to destruction with no auto-retreat**
(Federation wiped); each weapon **announces as it enters range** (5 "opens up" lines); a faster fleet ordered to
**Withdraw escapes intact** while a slower one is caught; and a **finer step gives more salvos with the same winner**
(dt 1 s → 67 salvos, dt 5 s → 13). A Playwright pass renders both themes and runs the battle to completion with **0
console errors**.)*
