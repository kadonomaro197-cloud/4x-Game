> # 🗄 ARCHIVED 2026-08-03 — DO NOT FOLLOW AS THE LIVE DESIGNER SPEC
>
> **This is the earlier "Designer Interconnection Audit" (2026-08-01), moved to `docs/archive/` on 2026-08-03 in the designer-docs cleanup.** The CANONICAL component-designer source of truth is now the **12 door HTMLs** in `docs/Actual HTMLs Of designers/` + **`docs/economy/DESIGNER-NORTH-STAR.md`** (the derive-a-dial method) + the **assembler suite** in `docs/assembler/`. Read those to design; read this for history. **STILL LIVE from this audit:** its six open developer rulings **R1–R6** (`06-FINAL-REPORT.md` → "The open developer rulings") remain UNDECIDED — the cleanup did not resolve them. Locked decisions live in `docs/COMBAT-DESIGNER-GROUND-TRUTH-2026-07-28.md`.

# 06 — FINAL REPORT: The Designer Interconnection Audit

> **For the developer. Read this one if you read nothing else.** It says, in plain English: do the eleven
> designers work as one connected machine, where they don't, what the fixes are, what was missing and is now
> designed, and — most important — **what to build first.** Every claim here is backed by an engine
> `file:line` in `01`–`05`; this is the summary, not the evidence.

---

## The verdict, in one paragraph

**Your designer system is far more finished than it looks — and its problem isn't missing parts, it's
unlit ones.** I traced all eleven door designers against the actual engine code, adversarially (every
finding a second agent tried to disprove), and walked the whole thing through five real games on paper. The
mechanical chains are remarkably complete: you can research → design → build → transport → invade a planet in
8 of 9 steps; the colony economy's plumbing is whole; ground combat is the most finished system you have. But
the **decisions** that turn those chains into a *game* — expand to a second colony, run your fleet dark,
field elite troops, put your best admiral in charge, feel the pinch of a hungry colony — are almost all
sitting behind a switch defaulted to "off," a demand knob set to zero, a producer nobody built, or a single
missing wire. **The work ahead is mostly connecting and switching on, not building from scratch.** That's the
best possible finding: the expensive part is already done.

---

## What connects, and what doesn't

Think of it like a ship's electrical system: I checked every wire for whether it's actually landed on a
terminal at both ends.

**The load-bearing connections that WORK today** (verified live):
- The **weapon ↔ defense** fight — a weapon's nature vs shields, armour vs damage, evasion as a
  harder-to-kill multiplier. The heart of combat is wired.
- The **mass/crew budget** — every part's weight and crew is summed and gated. Building a ship is honest.
- The **build chain** — the factory actually builds the parts you design, from materials, gated by research.
- The **ground combat resolver** — the most complete system; it even enforces rules the *ship* side doesn't.
- The **colony morale plumbing** — comfort, crowding, and tax all move morale correctly.

**The connections that are BROKEN or DARK** (the work list):
- **The employment → morale wire has no producer** — the game reads "are your people employed?" but nothing
  ever fills in the jobs, and filling it naively *backfires*.
- **Nothing runs on power** except the warp drive and firing energy weapons — shields recharge free, sensors
  cost nothing, and **a colony can't make power at all.**
- **You can't move people** — passenger and cryo holds exist but carry nobody into a colony, so **you can't
  found a second colony or settle a conquered one.**
- **The firepower enhancer does nothing in a real fight** (it feeds a resolver only your tests use).
- **A seated leader drives nothing** outside a single flagship.
- **The sensor targeting math is wrong** — you detect things outside your sensor's band.
- **Engines are invisible to the heat and stealth systems** — a full burn is as "loud" as a gentle nudge.
- Three produced numbers have **zero readers** (a logistics cap, a command rank label, and — corrected — the
  research cost-per-day turned out fine after all).

---

## The five patterns that explain almost everything

1. **"Missing" is mostly "switched off."** The biggest and most hopeful finding. Most gaps are a
   default-false flag, a zeroed demand coefficient, a building on no start list, or an attribute no template
   declares. The fix is usually a value or a data line, not code.
2. **Ground is ahead of space.** Your ground units enforce "needs a reactor / needs a magazine / armour gets
   penetrated" — ships don't. The north star ("space-combat depth") is *inverted* in these spots.
3. **You have two combat resolvers, and the tidy one is test-only.** Real fights use a different path — which
   is exactly why the firepower enhancer silently does nothing.
4. **Some wires connect, just not where the design thinks.** Armour, signature, and intel all flow — to a
   different consumer than the designer names. These are documentation fixes, not broken wires.
5. **The Logistics "cargo class" list over-promises.** Ammo and troops aren't cargo classes; they're their
   own components. The taxonomy needs honest labels.

---

## The recommended build order

This merges the Phase-3 fix waves, the Phase-4 designs, and the Phase-5 "what actually blocks a game"
priorities into one sequence. **Do them in this order** — each wave stands on the one before it.

### FIRST — decide five things (a day of rulings, zero code)
These gate everything downstream and cost nothing but a decision:
- **R1 — What does taking a planet *transfer*?** You can win the ground battle and seize buildings, but
  whether that flips the *colony's* ownership (and what you inherit) is undecided. **This is the literal
  finish line of your MVP** and its last step is open. Rule it first.
- **R2 — Who owns colony housing/jobs?** (Civic, with Industrial only providing jobs.)
- **R3 — What is the employment denominator?** (Scale jobs to the workforce, or measure against a segment.)
- **R4 — Structure and Evasion belong to Chassis and Propulsion, not Defense.** (Confirm the boundary.)
- **R5 — What does a command seat's rank/span actually enforce?** (How many posts it can staff.)

### WAVE 1 — cheap wins that light real decisions (mostly data & one-line fixes)
- Fix the **sensor band-gate math** (one line — everything detection rides on it).
- Fix the **firepower enhancer** (fold the caliber into per-weapon damage — safe, no double-count).
- Fix **ship penetration** (let ship weapons pierce armour like ground ones do).
- Add the **scenario-faction mass-budget test** (mass is already enforced in-game; just untested for AI ships).
- Turn on **reactor-heat signature** (after re-baselining the detection tests).
- Put the **academy and intel directorate on a start build list** (data — makes leaders and spies reachable).

### WAVE 2 — the two builds that unblock the game (highest gameplay value)
- **Colonist transport + settle-a-world (Design 2).** *Without this, the X in 4X doesn't work* — every colony
  after your first is blocked. This is the single highest-value build.
- **The employment model (Design 3), done carefully** — scale jobs to the workforce (R3) so it doesn't tank
  your homeworld. Then turn on **food demand** (with a farm added to Earth's data in the same change).

### WAVE 3 — the power economy (the most-connected system)
- **Generic power-draw + colony power generator + shield power (Design 1 / E-build-11).** This lights up
  weapons, shields, sensors, propulsion, *and* the colony power-morale wire in one coherent system. Build the
  colony power producer first (it's the hard prerequisite), then the generic draw, then wire shields to it.

### WAVE 4 — the tension layers (make combat and crisis interesting)
- **Heat & signature (Design 4)** — drives get hot, loudness scales with burn, "run dark" becomes a real
  choice.
- **Command agency (Design 5)** — a seated leader finally drives the system they govern.
- **Ship reactor/magazine gates** — behind a default-off flag, with the start ships fixed first.
- **Fuel exhaustion** — last, and only with a fuel readout so a ship going dark is *seen*, not a silent freeze.

### PARKED (need a bigger engine change)
- Enhancer **self-repair** (needs a damaged-but-alive state — the engine is whole-or-dead).
- Enhancer **foresight/initiative** (needs a turn order — combat is simultaneous).

---

## The open developer rulings (your decisions, collected)

| # | Ruling | Why it matters |
|---|--------|----------------|
| R1 | What colony capture transfers | The MVP finish line; its last step is undecided |
| R2 | Housing/jobs door of record | Stops double-counting in morale |
| R3 | The employment denominator | Prevents the jobs fix from tanking the homeworld |
| R4 | Structure/Evasion belong to Chassis/Propulsion | Stops two doors claiming one number |
| R5 | What span-of-control enforces | Gives the command seat's rank meaning |
| R6 | Fighters: cut the dead slider, or build the channel | The slider and its stockpile are both dead today |

---

## The five most important things I found

1. **Your game is built but unlit.** The hard part — the mechanical systems — is done to a depth that
   genuinely surprised me. What's left is switching things on and connecting a handful of wires. Do not
   rebuild anything.
2. **The X in 4X doesn't work yet.** You can build one colony and never a second, because people can't be
   transported into a colony. Colonist transport (Design 2) is the highest-value single build.
3. **The finish line of your own MVP is an open question.** "You can take a planet" — but what taking it
   *transfers* was never decided. That's a ruling, and it should be first.
4. **Two combat bugs are quietly distorting the whole meta.** The firepower enhancer does nothing in real
   fights (so toughness cadres dominate), and the sensor band math lets you see things you shouldn't. Both
   are cheap to fix and both matter everywhere.
5. **The adversarial method paid for itself.** I was wrong once — I called the research cost-per-day dial dead
   and the skeptic agents proved it works. Every finding here survived a second agent trying to kill it,
   which is why you can trust the list.

---

*Audit complete. All six deliverables committed and pushed. The evidence is in `01`–`05`; this is the map.
The next move is yours: the five rulings, then Wave 1.*
