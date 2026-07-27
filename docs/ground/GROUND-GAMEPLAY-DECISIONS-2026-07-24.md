# Ground Gameplay — the developer's DECISIONS (2026-07-24)

**Status:** 🔒 decisions recorded, build pending. Follows the `docs/earthfall/EARTHFALL-DECISIONS.md` pattern — the
developer's answers, captured verbatim-in-essence so a future session doesn't re-ask or guess. Produced from a
two-pass survey (8 domain readers + 8 cross-system connection readers, ~4M tokens, file:line throughout).

**Read with:** `docs/ground/GROUND-SURFACE-MAP-DESIGN.md` (the board), `docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md`
(the rules), `GameEngine/GroundCombat/CLAUDE.md` (as-built).

---

## The survey's headline (what prompted these decisions)

**Ground gameplay is ~85% built engine-side, ~30% reachable by a player, and 0% observable.**

The engine is genuinely good and CI-gauged: an assembler deriving stats from parts, terrain-weighted A* on a wrapping
planetary grid, a deterministic salvo resolver with shields / armour-by-damage-nature / fortification / cover, stances,
rules of engagement, an order queue, a tactical AI, inter-world troop lift, and real-metre tactical combat. Then:

- **A stock New Game raises no troops, spawns no enemy, and builds no fleet** (`NewGameMenu.cs:52/55/60` all false).
  The ground layer runs perfectly over an empty world — *nobody has ever seen it work in a normal game.*
- **Five ground behaviour flags are process-wide statics set only on the New Game path.** Loading a save doesn't set
  them, so the same save plays differently depending on whether you started a new game earlier in the same session.
  Every CI gauge therefore runs with the behaviour OFF.
- **The entire `GroundCombat` folder emits zero log lines, zero events, zero battle records.** The clock stops, the UI
  points at the SPACE combat tab, and opens an empty space report.

---

## The decisions

### A — Designing a unit

| # | Question | **RULING** |
|---|---|---|
| 1 | Two design paths, or one? | **ONE designer for everything in the game. The prebuilt templates GET DELETED.** (Restores the locked "no labelled unit types" principle.) |
| 2 | Are Penetration / Per-Shot-Energy unit or weapon properties? | **Properties of the WEAPON.** |
| 3 | Invalid design — block or warn? | **Blocked from SAVING.** |
| 4 | Should ground parts cost research? | **YES — and the cost SCALES WITH COMPLEXITY.** |
| 5 | Hazard counters — how many components? | **Every hazard gets a counter, and the COMPONENT DEPENDS ON THE HAZARD.** The counter must make physical sense for that hazard, and a hazard may hit several stats at once. *Developer's worked example: a dust storm might need advanced radar to see through it, and it limits movement speed and reduces accuracy.* So a counter is not one generic "resistance %" — it is the specific gear that answers that specific menace. |

### B — Building and fielding a unit

| # | Question | **RULING** |
|---|---|---|
| 6 | Where does a new unit muster? | **A BUILDING SETTING, for BOTH space and planetary units.** Planetary units go to a chosen planetary coordinate; space units go to a particular orbit. (One rally-point concept, two address types.) |
| 7 | Do ground units cost people? | **YES — and there is NO return. They die.** |
| 8 | Prebuilt units carry upkeep + magazine; should ammo bite? | **YES.** *(Note: ruling #1 deletes the prebuilt templates, so this becomes "the one designer's output always carries upkeep + a magazine.")* |

### C — Buildings and the surface economy

| # | Question | **RULING** |
|---|---|---|
| 9 | Free region button vs costed tile queue? | **COSTED TILE QUEUE. NOTHING IS FREE.** (The free "Build here" path is deleted.) |
| 10 | Where does the "this building has a home" hook live? | **PENDING** — developer asked for the options with pros/cons/why. |
| 11 | Does every building occupy ground? Is "occupies a tile" the same as "war-map objective"? | **YES, every building occupies ground. And they are THE SAME attribute** (one flag, not two). |
| 12 | Fund employment + power, or cut? | **FUND THEM.** |
| 13 | What makes a tile good to build on? | **It depends on the tile type and WHAT MAKES SENSE.** A grassland tile with a food-production building gets a bonus *because it makes sense*. Different components get different bonuses for the building they're on and where it's built. (Terrain affinity is semantic, not a generic multiplier table.) |

### D — Movement

| # | Question | **RULING** |
|---|---|---|
| 14 | Which coordinate is authoritative? | **DELETE "march to region" entirely.** Planetary units move on a **TWO-LAYER COORDINATE SYSTEM** written like `(17,09)(22,47)` — the first pair is the **regional hex**, the second is the **mini hex**. One address, two layers. |
| 15 | Should walking out of contact be free? | **PENDING** — developer asked for an explanation. |
| 16 | Is mini-hex movement a player order? | **YES — mini-hex movement works JUST LIKE regional hex movement.** (Same verb, finer layer — consistent with the #14 two-layer address.) |
| 17 | What to show while marching? | **SHOW ALL FOUR** — destination, distance remaining, ETA, current speed. |

### E — The fight

| # | Question | **RULING** |
|---|---|---|
| 18 | Player target selection? | **YES — but also dependent on the DOCTRINE of the battalion.** |
| 19 | Battle scope + an engage decision? | **Battle is scoped to REAL DISTANCE** (not the region band). **And yes to an engage decision — dependent on orders or doctrine.** |
| 20 | How is a battalion composed? | **The same way it's done in SPACE.** (Ground mirrors the fleet composition model — consistent with the existing "formation is the ground echo of a fleet" parity map.) |
| 21 | What does capturing a planet transfer? | **OPEN — still thinking.** |
| 22 | Is one health scalar enough? | **ALREADY DECIDED IN THE DOCS — the question should not have been asked.** `docs/combat/UNIFIED-RESOLVER-AND-BATTLE-STATS.md` Part B designs the `CasualtyTier` wound function (returns untouched / wounded / dead, works for a model squad *and* a monolith) plus a damage ledger totalled at battle end; `docs/ground/GROUND-UNIT-VARIABLES.md` designs the shared **Training** dial (build-time + scarce talent + credits → veterancy, militia→elite on one slider). **Both designed, neither built.** Treat as settled design awaiting build. |
| 23 | How long is a battle meant to last? | **PENDING** — developer challenged the premise ("there are legitimate fire rates on units"). See the correction below. |

### F — Showing the ground war

| # | Question | **RULING** |
|---|---|---|
| 24 | What is the ground battle readout? | **A LOG, for now.** (Cheapest, and it stops the combat interrupt from lying.) |
| 25 | Ownership drawing + unit inspection surface? | **Ownership: deal with it LATER.** Inspection surface: **PENDING** — developer asked what the term means. |
| 26 | Contact memory? | **YES — a lost contact leaves a FADING LAST-KNOWN marker** (matching how space contacts behave). |

### G — Defaults and lifecycle

| # | Question | **RULING** |
|---|---|---|
| 27a | Should ground flags live in the SAVE rather than process statics? | **PENDING** — developer asked for an explanation. |
| 27b | Should a New Game raise a garrison and place an enemy by default? | **NO.** |

---

## The #23 correction (fire rates)

The developer challenged: *"there are legitimate fire rates on units what do you mean?"* **Verified against source —
there is no fire rate on a ground weapon.** `GroundWeaponAtb` carries exactly five fields: `Mass`, `Attack`, `Range`,
`Mode`, `Range_m` (`GroundWeaponAtb.cs:33-45`). No rate-of-fire, no reload, no shots-per-interval.

`Attack` is a **flat lump of damage**, and `GroundForcesProcessor` applies it **in full, once per game hour**
(`RunFrequency = TimeSpan.FromHours(1)`, `:29`). So the effective fire rate of every ground weapon in the game is
identical — *once per hour, full strength* — and it is a property of the **processor tick**, not of the weapon.

That is why battle length is currently set by the tick rather than by the guns, and why the order queue rarely matters
(most fights end before a second waypoint pops). **If per-weapon fire rates are wanted, that is a new dial** — and it
pairs naturally with ruling #2 (penetration/energy moving onto the weapon) and #8 (ammo biting), since all three are
per-weapon properties landing on the same object.

---

## Consequences worth flagging before build

- **Ruling #1 (delete the prebuilt templates) is load-bearing and wide.** The three prebuilt units are today the *only*
  path to penetration and per-shot energy, they are what `GroundStartGarrison` raises, and they are referenced from the
  base mod and several tests. Deleting them requires ruling #2 to land first (the dials must exist on the weapon before
  their only carrier is removed), plus a garrison-composition replacement. **Order: #2 → #4 → #1.**
- **The JSON binder is exact-arity** (gotcha #6/#10). Adding penetration + per-shot-energy to `GroundWeaponAtb` means
  updating **all five** ground-weapon templates in lockstep or every one of them fails to bind.
- **Ruling #7 (units cost people, no return) connects ground to population for the first time.** The templates already
  declare `CrewReq` 40–100 and it is computed onto the design and never read; the manpower machinery already works for
  ships and stations. This is the wire that makes the population system notice a war.
- **Ruling #9 (nothing is free) deletes a path players can currently use** to make unlimited instant infantry and
  buildings. It is also, today, the *only* path that produces a building which actually fortifies — so #9 and #10 must
  land together or ground defence silently breaks.
- **Ruling #14 (delete "march to region") removes a currently-broken path**, not a working one: every region-march
  button, every queued region move, and every AI move sets the region index without restamping the global position, so
  the token never moves on the map.
