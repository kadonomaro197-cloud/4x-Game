# Missing design 4 — HEAT & SIGNATURE (what makes a unit loud and hot)

> **What it is, in one breath:** two systems already model "how active/hot a ship is" — a **combat heat pool**
> (fire too many energy weapons and you overheat and have to throttle) and a **detection signature** (the
> louder you run, the farther away the enemy sees you). But the **engine is blind to your engines**: a
> hard-burning drive adds *nothing* to the combat heat pool (crack **C6**), and while it *does* add to your
> signature, it adds a **flat constant** — a gentle nudge and a full-throttle burn look **equally loud**. This
> design gives the drive an intrinsic heat/loudness the way a weapon has one, so both systems finally see it,
> and makes loudness scale with how hard you're actually running.
>
> **Honest scoping:** there's no new *door* — heat and signature are **emergent** (they come from what you're
> doing *right now*, not from a menu choice). The design work is (1) confirming which numbers are per-component
> **dials** vs whole-fleet **readouts**, and (2) two specific fixes: a drive heat dial, and a proportional
> thrust-loudness term.

---

## 1. The input surface (verified in source)

Two separate "activity" systems, each with a real consumer:

**A — the combat heat pool** (`FleetCombatStateDB.HeatPool_kJ`)
- Written only by **weapons**: `CombatEngagement.cs:717 HeatPool_kJ += EnergyHeatGen(fire)*dt` (sum of each
  weapon's `HeatPerSecond`, `:1429`), dissipated by `HeatDissipationFraction` (`:718`).
- Read: overheating **throttles energy weapons** (`:714-726`).
- **Drives contribute nothing** (crack C6) — grep of `HeatPool_kJ` writers finds only the weapon term.

**B — the detection signature** (`EmconActivityProcessor.ActivityMultiplier`)
- Written by `EmconActivityProcessor`: reactor **Load** (`:64,115`, gated by `EnableReactorHeat=false`) +
  `HeatFactor(IsBurning, IsFiring)` (`:114`).
- `IsBurning` is a **boolean** (`ManuverDeltaVLen > 0`, `:82`); `HeatFactor` adds a **flat** `ThrustHeat=4.0`
  when burning at all (`:42,70-71`) — **not** proportional to thrust magnitude.
- Read: the live scan (`SensorTools.AttenuatedForDistance ~:335`) → detection range, first-contact, the
  battle trigger.

**Per-component heat dials that already exist:** a weapon's `HeatPerSecond` / `CombatHeat_kJps` (set where the
weapon profile is built, `ShipCombatValueDB.cs:359`), the reactor's `ReactorHeat` coefficient. So the *pattern*
— a component declares an intrinsic heat, the fleet sums current heat — is established; the drive just isn't
in it.

---

## 2. The unifying model (derived from the two systems)

Both systems are the same shape: **current loudness = Σ over components of (its intrinsic heat/signature × how
hard it's working right now).** Say it once and both C6 and the shape-fix fall out:

- **Intrinsic, per-component (DIALS):** a weapon's heat-per-shot, a reactor's heat-per-load, and — the missing
  one — **a drive's heat-per-thrust.** Each passes the **intrinsic test**: settable knowing only the component
  ("this fusion torch runs hot"), like a weapon's `HeatPerSecond` already is.
- **Current activity (EMERGENT):** what you're doing this instant — firing (weapon heat), burning (drive heat ×
  *thrust fraction*), reactor load. Not a dial; it's your *state*.
- **The totals (EMERGENT READOUTS):** `HeatPool_kJ` (combat throttle) and `ActivityMultiplier` (detectability).
  Shown, never set. The North Star corollary from movement applies verbatim: the numbers players watch —
  current heat, current signature range — are **emergent**, and the designer must display them, not offer them
  as sliders.
- **The DECISION (an ORDER, not a dial):** EMCON posture — Active vs Dark (`docs/combat/DETECTION-DESIGN.md`) —
  is how the player *modulates* loudness. Running dark trades capability (cold sensors, gentle burns) for
  stealth. That's the lever; heat/signature are the readouts it moves.

---

## 3. The two fixes this design specifies

### Fix A — drive heat dial (crack C6)
Give the drive an intrinsic **heat-per-thrust** dial and have current drive-burn feed **both** totals:
- into `HeatPool_kJ` (like weapons) — so a fleet that manoeuvres hard *and* fires is closer to overheating
  than one that just fires. A real combat tradeoff: burn to dodge, or stay cool to keep firing.
- into `ActivityMultiplier` — replacing the flat boolean (see Fix B).

*Intrinsic test:* PASS — heat-per-thrust is a property of the engine alone. It's a Propulsion-designer dial,
the thermal twin of the weapon's `HeatPerSecond`.

### Fix B — proportional thrust loudness (the shape fix)
Replace `IsBurning ? ThrustHeat : 0` with a **magnitude** term — scale the drive's signature contribution by
the *current thrust fraction* (from `NewtonMoveDB.ManuverDeltaVLen` relative to the drive's max). A station-
keeping nudge is quiet; a full-throttle intercept burn lights you up. This makes the EMCON decision real:
*coast quiet or burn loud* becomes a smooth tradeoff instead of an on/off cliff.

Both fixes ride the same new **heat-per-thrust dial** — one dial, two consumers — which is why they're one
design, not two.

---

## 4. The connections this lights up (why it matters beyond "engines get hot")

- **Power economy (design 1):** reactor Load *is* the standing-draw fraction — a ship running everything at
  once (fire + warp + shields) runs hot *and* draws hard. Heat and power are two readouts of the same activity.
- **Detection (existing):** proportional loudness is what makes the Dark/Active posture a real decision — the
  DETECTION-DESIGN lever needs this to not be an on/off switch.
- **Combat throttle:** drive-heat in the combat pool couples manoeuvre to firepower — the "burn to dodge vs
  stay cool to shoot" tension that a Newtonian combat model should have and currently doesn't.

---

## 5. Cradle to grave

**materials** → a **drive component** (Propulsion designer, with the heat-per-thrust dial, research-gated) →
**installed** → **the decision** (how hard do I burn — fast and loud and hot, or slow and cold and hidden; and
can I afford the heat if I also want to fire?) → **the drive is shot off** → your manoeuvre *and* your
heat/signature contribution collapse together (the grave rung, shared with the damage system). The dial is new;
every other rung already exists.

---

## 6. Gauge & blast radius

- **Gauge:** (a) a fleet burning hard gains `HeatPool_kJ` (and, near the cap, throttles its energy weapons);
  (b) the same fleet's `ActivityMultiplier` — and thus its detection range — rises *smoothly* with thrust
  fraction, not in a single step; (c) a coasting fleet is quieter than a burning one by a factor that tracks
  thrust magnitude.
- **Blast radius:** the drive-heat dial is additive (0 → byte-identical). Fix B changes every burning ship's
  signature *shape*, so it shifts detection ranges — gate it with the EMCON/reactor-heat flip (A-flip-3a) and
  re-baseline the detection gauges (`DetectionTuningTests`) together. Combat-heat coupling is the balance
  change to watch: verify a manoeuvring warship can still fire enough to matter (don't make burning a firepower
  death sentence).

---

*Design 4 of 5. Next: command agency — making a seated leader actually drive something.*
