# The five plain problems the marine and the Venator both hit (2026-08-01)

> **What this is:** the Blood Angels marine and the Venator Star Destroyer builds each ran into a set of
> walls. None of those walls are really a "Space Marine problem" or a "Star Destroyer problem" — they're
> general problems in the designer that those two just *happened to show*. This file states them in plain
> English, as **five problems** you can watch for on *anything* you build next. Each one has a shop-floor
> analogy, where it showed up, a one-question check, and the fix.
>
> *(The detailed, code-level version of these — twelve fine-grained items with file names — is kept lower
> down under "The detail underneath," for when someone needs the specifics. The five below are the real
> point.)*

---

## Problem 1 — Half-finished plumbing (a knob that isn't hooked to anything)

**In plain terms:** you can turn the dial, but it doesn't change anything in an actual game — the wire from
the knob to the thing it's supposed to move was never run. And its cousin: a feature that got installed on
one kind of unit but not on its sister unit.

**The analogy:** a gauge bolted to the panel that was never piped to the sensor. The needle's there; it reads
nothing. Or a safety interlock fitted on the port engine but not the starboard one.

**Where it showed up:**
- The marine's / Venator's **firepower "elite" knob does nothing in a real fight** — it feeds a number only
  the *test rig* reads, never the live battle.
- The Venator's **hangar can hold fighters but there's no button to launch them** — you loaded the bay and
  there's no handle to get them out.
- **Ground units get a "needs a reactor" safety check; ships don't** — same idea, fitted on one, missing on
  the other.

**The check (ask this of every knob you add):** *"If I turn this, does something actually change in a real
game — and does every unit that should have this feature actually have it?"*

**The fix:** before a knob ships, follow its wire all the way to the thing it moves in a *live* game (not the
test bench). If the only thing reading it is a test, it's a dead knob. And when you add a feature to one kind
of unit, add it to all the kinds that should have it, in the same go.

---

## Problem 2 — Free to own, free to run, free to lose

**In plain terms:** the game charges you to *build* something, and then it's free forever. It costs nothing to
keep sitting there, nothing to run, needs no crew, and when it's destroyed nothing is spent or even written
down.

**The analogy:** a pump you pay to install once — then it draws no power, needs no watchstander, never needs
maintenance, and when it burns out you just make another from the scrap bin with no log entry.

**Where it showed up:**
- A **fielded marine costs nothing to keep** and never burns through ammo. A Venator's **shields and sensors
  run for free** — no power drawn to hold them up.
- A marine is **built from steel, not from people** — no recruits, no training time, no limit — so "six
  irreplaceable marines" are actually re-buildable on demand.
- A dead marine just **vanishes** — no casualty report, nothing recovered, nothing lost on the books.

**The check:** *"Once this exists, does it cost anything to keep, to run, and to lose — or is it free after I
build it?"*

**The fix:** charge for owning it (an upkeep bill), charge for running it (power/fuel/ammo draw while it
operates), require crew or scarce parts to build it, and make losing it cost something *and* get logged.

---

## Problem 3 — A whole crowd treated as one body

**In plain terms:** a group of things is modeled as a single blob with one health bar. You can't lose one man,
one fighter, or one gun at a time — it's all-or-nothing.

**The analogy:** modeling a whole damage-control party as one sailor. When "the sailor" takes damage, the
whole party's effectiveness drops together; you can't lose two men and keep the rest working.

**Where it showed up:**
- The marine **can't be a 6-man squad** — it's one health bar, not six men who fall one at a time.
- The Venator's **wing of fighters** is the same thing from the ship side — a bay full of craft, not a
  countable group that thins out as it takes losses.
- A ship's **twenty guns collapse into one gun** for the fight — you can't knock out one turret.

**The check:** *"Is this really one thing, or is it a group that should lose members one at a time?"*

**The fix:** let a "unit" be a *count* of parts that die individually, and have combat take them out one at a
time. (Keeping the guns lumped together is fine for speed in huge battles — the point is to notice when the
lumping hides a choice the player cares about.)

---

## Problem 4 — The menu is too rigid

**In plain terms:** two things. First, you can only pick the options already on the list — if the mode you
want (a jump pack, a thrown grenade) isn't there, you can't build it. Second, some picks drag a second choice
along with them that you never wanted.

**The analogy:** a valve lineup with only three fixed positions and no way to add a fourth. And two switches
ganged onto one lever, so you can't move one without the other.

**Where it showed up:**
- **No jump pack** — the movement menu only has walk / tracks / walker / hover, and there's no way to add
  "fly." Same for **thrown grenades** — that kind of weapon just isn't on the list.
- The **bolter can't be a "mass-reactive" round** — picking "bullet" forces "solid slug," and there's no way
  to also make it explode on impact. Two choices that should be separate are bolted together.

**The check:** *"Can I get the option I actually want — or is it not on the list, or chained to a choice I
didn't want?"*

**The fix:** make the option lists easy to extend, and unbolt the choices that are wrongly ganged together so
they can be set separately.

---

## Problem 5 — Everything is either perfect or scrap

**In plain terms:** a thing works at 100% right up until it's instantly destroyed. Nothing wears down, nothing
is damaged-but-still-working, nothing needs maintenance.

**The analogy:** a pump that runs at full rated flow until the exact second it seizes — no bearing wear, no
degraded performance, no "it's limping but it still turns."

**Where it showed up:**
- The marine's **power armour has no condition** — it can't wear, can't take partial damage, can't be patched
  up by a specialist. It's flawless until it's gone.
- (This is the same reason the audit had to shelve **self-repair** — you can't repair something that has no
  "damaged but alive" state to repair *from*.)

**The check:** *"Can this be damaged-but-still-working, or is it only ever full-strength or destroyed?"*

**The fix:** give things a condition somewhere between full and dead — so they can wear, take partial damage,
and be maintained or repaired.

---

## Which to fix first

**Problem 1 (half-finished plumbing) is the one to institutionalize, because it's cheap and it hides the
most.** Make it a standing habit, two parts:
1. **Every knob has to prove it moves something in a real game** — trace its wire to the live battle/economy,
   not the test bench. A knob that only a test reads is a dead knob, and that should be treated as a bug.
2. **Every feature goes on all the units that should have it, in the same job** — don't fit the safety on the
   port engine and forget the starboard.

Those two habits stop the two nastiest patterns — "the knob does nothing" and "it works on ships but not on
the ground" — from ever coming back. They're the patterns that produced the most surprises in both the audit
*and* these two builds.

After that, the order is by how much each buys you: **Problem 2** (make things cost something to own/run/lose)
and **Problem 3** (real squads and wings) are the big gameplay unlocks; **Problem 4** (a less-rigid menu) is
medium; **Problem 5** (wear and damage) is the biggest engine job and can wait.

---

## The detail underneath (the code-level breakdown, for when specifics are needed)

Each plain problem above is really a few finer issues. Kept here so nothing is lost:

| Plain problem | Breaks down into | Effort of the fix |
|---------------|------------------|-------------------|
| **1 · Half-finished plumbing** | a dial wired to a dead/test-only reader · a carry component with no deploy order · a mechanic on one entity class but not the others | S–M each (mostly wiring) |
| **2 · Free to own/run/lose** | no standing upkeep · no per-component running cost (power/fuel) · no crew/scarce-stock draw and no build cap · destruction is a silent delete | M (upkeep + power draw) · M–L (scarcity) · S–M (loss event) |
| **3 · Crowd as one body** | no model/element count on a unit · like-weapons/turrets merge to one profile · size is just mass | **L** (the count is a new core piece) · M (per-weapon resolution) · usually skip (scale) |
| **4 · Rigid menu** | fixed movement/weapon-mode lists (no jump, no grenade) · nature bolted to delivery (no mass-reactive round) | M per new mode · S–M to unbolt the axes |
| **5 · Perfect or scrap** | no condition/damage state between full and destroyed | **L** (a new state on everything) |

*The full engine-level write-up (file names, the audit's five themes, the per-item checklist) lived in the
first draft of this doc and is preserved in git history if the code-level detail is ever needed. The five
plain problems above are the version to work from.*

---

*Generalized from `docs/showcase/LITMUS-BLOOD-ANGELS-BUILD.md`, `docs/showcase/LITMUS-VENATOR-BUILD.md`,
`docs/showcase/LITMUS-TO-100-ROADMAP.md`, and the interconnection audit (`docs/designers-audit/`), 2026-08-01.*
