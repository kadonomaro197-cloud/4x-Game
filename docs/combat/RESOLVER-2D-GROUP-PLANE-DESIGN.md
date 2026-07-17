# The 2D Group-Plane Resolver — LOCKED Design

**Status: 🔒 design-locked (2026-07-17). Build state: not started (S0 is the first slice).**
**Scope: the pure-math auto-resolver — the code that decides who shoots whom and who dies, with nothing drawn on screen.**

> **The one-line version:** give every *group* in a battle a single position on an invisible 2D map, let doctrine decide where each group sits (front / flank / hang-back), and measure the distance between groups to decide who can shoot whom. The player never sees the map — they set doctrine and read the result. One math module runs this for both space fleets and ground formations, and a battle that spans both (a fleet in orbit *and* troops on the moon below, like the Battle of Endor) is two of these maps stepped together, wired to each other by data, never by shared geometry.

This design was chosen over three rivals by an adversarial design-off (see "How we got here"). It scored highest (8/10) because it is grounded in code that already exists, it is deterministic, it is cheap at any scale, and doctrine stays the only lever.

---

## 1. Why this exists (the point before the plumbing)

Right now the auto-resolver has a blind spot, and the developer named it: **"the 2D issue is the biggest issue."**

- **Space combat** tracks range as **one single number for the whole fight** — a "how far apart are the two sides" gap that closes over time (`FleetCombatStateDB.Separation_m`). There is no such thing as a *flank* or a *rear*. Every ship on your side is exactly as far from the enemy as every other ship. It's a tug-of-war on a rope: one dimension, back and forth.
- **Ground combat** is the opposite extreme: **every single soldier** has its own hex coordinate, and the resolver compares **every soldier against every enemy soldier** to see who's in range. That's honest, but it's an `O(units²)` cost — 500 soldiers vs 500 soldiers is 250,000 comparisons *per tick* — and it has no bucketing, so it doesn't scale.

Neither is what a real battle looks like. A real battle has a **shape**: a line that holds the center, a fast wing that swings wide to hit the enemy's flank, artillery that hangs back and shells from max range while nothing can shoot back. The developer wants that shape to *matter* — a longer-ranged group should get to shoot first, a flanker should reach the guns the front line is screening — **without** the player having to push icons around a tactical map. In their words: *"there is nothing to see as far as the player is concerned. All this is being done with math in the background."*

**So the job is:** add just enough 2D geometry to produce that shape and those decisions, keep it invisible, keep it deterministic, keep it cheap, and make one version of it serve space *and* ground. That's this document.

---

## 2. The one big decision — position lives at the GROUP, not the unit

The whole design turns on a single choice: **a "group" is the only thing that has a position, and a group is one point on the map no matter how many units are inside it.**

- In **space**, a group is a **sub-fleet** — the Screen, the Line, the Artillery, the Support wings the fleet already splits into (`FleetRoleComposer`). A wing of 500 identical X-wings is **one group** — one point.
- On the **ground**, a group is a **formation** (`GroundFormation`) — a company, a battalion. A 6-man Blood Angels squad is **one group** — one point (with two weapons inside it: the bolter that shoots at range, the chainsword that only "reaches" at distance zero).
- A lone capital ship is its own one-unit group. The Death Star is its own one-unit group (a single monster weapon with enormous punch). A bunker or a planetary shield generator is a one-unit group that **cannot move** — a combatant nailed to the deck.

**Why this is the load-bearing choice:** because position never touches an individual unit, the resolver's existing money-saving trick — **bucketing** (lumping 500 identical fighters into a handful of "500 of these, 12 of those" tallies and resolving them as counts, not as 500 separate objects) — survives completely intact. Cost depends on the number of *groups* (dozens, even at Endor scale), never the number of *units* (thousands). This is the difference between a design that scales and one that chokes. It's the same reason a chief engineer tracks *"the port shaft"* as one thing with one RPM, not every individual turbine blade.

---

## 3. What we're building on (the substrate — verified in the code)

Two parallel-agent surveys read the real resolver code so this design extends what exists rather than inventing a parallel system. The findings:

**Space side (`GameEngine/Combat/`):**
- Position today = the scalar `FleetCombatStateDB.Separation_m` — one shared gap, closed over time by `AdvanceClosing` (the faster side drives the gap down). Read through `SeparationOf(fleet)`.
- The damage kernel (`CombatKernel`) is **pure and 1-dimensional** — you hand it *one scalar distance* and it returns hit-fractions and damage. It has no idea about 2D, and it must stay that way.
- Everything closing-related rides behind a **default-off flag** (`EnableClosingRange`). Flag off → gap is 0 → every existing test is byte-for-byte unchanged.
- Firing is **bucketed** (`BuildFireMix` collapses all a group's guns into a handful of weighted entries; `ApplyCasualties` buckets defenders and kills whole ships as a count). This is what keeps it `O(buckets)`, not `O(ships²)`.

**Ground side (`GameEngine/GroundCombat/`):**
- Position today = **per-unit hex coordinates** (`HexQ/HexR` per region, `GlobalQ/GlobalR` on the planet-wide cylinder). The range gate compares hex distances *per unit pair*.
- **No bucketing** — `ResolveRegionCombat` is a nested per-unit loop, `O(units²)`. This is the single biggest cost gap, and this design closes it.
- Units and formations are **data objects, not entities** — so any new field needs `[JsonProperty]` *and* a line in the deep-copy constructor (the standing save-safety rule; the `LastUpkeepBilled` clone-omission is the cautionary tale).

**The invariants both sides demand we never break:**
1. **Determinism** — no random numbers, no reading the wall clock, no iteration-order dependence. Fast-forward must equal watch (a battle resolves identically whether you watch it at 5-second steps or blow past it at max speed).
2. **Default-off / byte-identity** — the whole new layer defaults off and degrades *exactly* to today's behavior when its data is absent. The existing green tests are the tripwire.
3. **Keep the kernel pure and 1-D** — do all the 2D vector math in the *caller*, and hand the kernel a single scalar distance. The kernel never learns geometry.

---

## 4. The model — an invisible battle map, seeded once, doctrine-shaped

Picture a flat sheet of graph paper laid down over the battle, measured in meters. It is **never drawn** — it exists only as numbers. Here's how it gets built and used:

**Seed the sheet once, at the moment the battle starts.** Take each fleet's real 3D position (which the scalar model already reads, then throws away). Find the center of all of them. Pick a fixed pair of directions (an x-axis and y-axis) by a rule that always produces the same answer for the same inputs (e.g. the x-axis points from the lowest-ID fleet toward the center). Flatten every fleet's 3D position onto that sheet. **This projection is done once and frozen** — it is never recomputed from live positions, because ships die and the center would jump around. A latecomer joining the fight is placed using the *same frozen* axes.

> For the ordinary two-sides-facing-off case, the second axis comes out near zero and the whole thing **collapses back to today's one-dimensional tug-of-war** — which is exactly the byte-identical path.

**Each group is a point = its fleet's anchor + a doctrine offset.** The fleet's flattened position is its **anchor**. Every group inside that fleet sits at the anchor *plus* an offset that its **role** dictates:
- Point the offset along the line toward the nearest enemy fleet's anchor ("the enemy-facing axis" — so the formation auto-orients toward whoever it's actually fighting, no steering required).
- **Along that axis:** a brawler (Line/Front) is pushed *forward*, toward the enemy — it wants to close. Artillery is pushed *backward* by its own longest weapon range — it hangs back and shells from max distance. A rear guard sits behind the anchor.
- **Across that axis (perpendicular):** a Screen/Flank wing is swung out to the side (±90°), so it can reach around to the enemy's flank and rear groups.

All of this is **pure trigonometry from the role table** — no coordinates the player ever touches. The player sets **doctrine** (aggressive → everyone pushes in; defensive → everyone holds back at bigger standoff), and doctrine picks which role-geometry table applies. That is the *entire* control surface.

**The anchors move over time.** The existing closing integrator (`AdvanceClosing`) is generalized from "slide one number" to "slide the anchor point along the enemy-facing direction." The faster side closes the distance. So range is genuinely dynamic — the gap opens and closes as the fight develops.

---

## 5. The two rules that come out of it

### Range rule — who can shoot whom (and the "farthest range shoots first" the developer asked for)

For each pair of enemy groups A→B: measure the straight-line 2D distance `d` between their points. Then:
- **Coarse gate:** A can engage B only if `d ≤ A's longest weapon range`.
- Hand that **same `d`** to the *unchanged* firing code as the scalar `separation_m`. Each individual weapon inside A skips if its own range is less than `d`. So the missile (1000 km) > railgun (500 km) > flak (50 km) > melee (~0) **layering falls out per group-pair** — at long range only the missiles reach; as the gap closes, the shorter guns join in.
- The gate is **directed**: because A and B can have different weapon ranges, a long-range group can shoot a short-range group **that cannot shoot back yet**. That is exactly "those with the farthest range shoot first," and it is exactly the ground standoff the developer described (the clone with the longer gun hits the zerg before the zerg arrives). It reuses the directed-fire plumbing already built for the fog-of-war slice — the only change is that `d` now comes from the two points instead of the one shared number.

When the flag is off, `d` is just today's shared scalar, so every fixture is byte-identical.

### Bearing rule — doctrine assigns the shape, never the player

Bearing is assigned **at the role level, never per unit**, from a small fixed table `RoleGeometry(role) → (bearing, standoff)`:
- **Line/Front** → 0° (dead ahead), pushed forward to brawl.
- **Screen/Flank** → ±90° (swings wide), so it reaches the enemy's flank and rear.
- **Artillery/Standoff** → 0° but held back at its *longest* range — it kites.
- **Rear Guard/Support** → 180° (behind its own anchor), reachable only by an enemy flanker or a very long gun.
- **A fixed installation** (bunker, shield generator, Death Star) → locked bearing, anchor that never moves. A combatant that cannot close.

The player's doctrine posture selects *which* table and how big the standoff bias is. That's the whole lever. **No flank is ever discovered by clever maneuver — it is assigned by doctrine.** That is an accepted limitation (see §10): the plane is unseen and the design forbids micro, so "flanking" is a doctrine *fiction* that produces the right result, not emergent tactics.

---

## 6. How it stays cheap — bucketing survives, cost is O(groups²)

Because position is a *group* property and never enters a per-unit bucket key, the two existing money-savers are untouched:
- **Outgoing fire** still aggregates a group's weapons into a handful of weighted entries — it just receives the group's own pair-distance instead of the shared one.
- **Incoming casualties** still bucket defenders and kill whole ships as a count.

So a 1000-fighter swarm is **one group with ~1–5 buckets**. The number of groups is bounded by (roles × fleets × sides) — on the order of **20–40 groups per side even at Endor scale**. Per tick the work is `O(groups² · planes + edges + buckets)` — a few thousand point-distance subtractions plus a few dozen bucket resolves. **Sub-millisecond**, the same class as today's 200-ship performance test that resolves in milliseconds. And it *improves* the ground side: bucketing collapses ground from its current `O(units²)` down to `O(groups²)` too, and fixes ground firepower-conservation across 3+ factions at the same time.

---

## 7. How it stays deterministic (fast-forward == watch)

Every new number is a pure function of (the frozen seed positions, the doctrine roles, elapsed time) — no randomness, no clock reads, no order dependence:
- The map's axes are seeded once and **persisted**, so they can't drift as ships die; a latecomer is projected with the *same stored* axes.
- Group offsets are pure trig from the role table and the "nearest enemy" direction (with a tie-break on entity ID so it can't flip-flop between two equidistant enemies).
- Damage still uses the existing snapshot-before-apply rule (all groups' positions, fire, and targets are frozen at the top of the tick, before any casualty), so within-tick order can't change the result.
- **The invariant that makes fast-forward == watch:** the resolver runs inside a fixed-cadence hotloop (5 s in space, 1 h on the ground). That fixed step is the integration quantum *regardless of how big a chunk of time the player fast-forwards* — a 1-hour jump still fires the trigger 720 times at 5 s each. So the sequence of position/damage updates is identical watched or skipped. **The one rule to guard forever: never feed a variable time-step into a group step.** (Do not route the plane through the "resolve this whole battle instantly" shortcut — that would break it.)

---

## 8. One module, two domains — space and ground unified

There is **one** geometry module (`GroupPlane.cs`) and **one** damage kernel (`CombatKernel`), with two thin callers:
- **Space:** `CombatEngagement` seeds anchors from 3D fleet positions; groups = sub-fleets by role; the closing integrator is the generalized `AdvanceClosing`.
- **Ground:** `GroundForcesProcessor` seeds anchors from the planet-wide surface grid (the `GlobalQ/GlobalR` cylinder the G6b migration already started); groups = formations; the closing integrator folds into `ApplyEngagementManeuvers`.

Both compute a scalar pair-distance and hand it to the *identical* pure kernel (a ground unit already produces a `WeaponProfile` list via `GroundCombatant.ToWeaponProfile`, with range = hexes × pitch). Terrain, cover, and fortification stay ground-only modifiers *on the kernel inputs* (they change armor/soak), not changes to the plane. **The only per-domain code is how anchors are seeded** (3D fleet position vs. hex grid) and the domain flavor (terrain). Everything downstream is shared.

---

## 9. Combined battles — the Battle of Endor, done with data not geometry

This is the acceptance test: a space fleet in orbit **and** a ground fight on the moon below, resolved in the same battle, where the ground fight decides the space fight. The design handles it with a **`BattleTheater`** that holds **several planes** (one space plane + one ground plane per contested surface), stepped in the same tick, **coupled by data only — never by shared coordinates.** Two kinds of coupling:

**(1) Objective link — the Endor lock.** The Death Star is a space group carrying a marker: *"I take zero damage while my guardian (the shield generator) is alive."* In space it is invulnerable — 100% soak, no matter how hard the fleet hits it. Meanwhile the **ground** battle (the strike team + Ewoks vs. the shield-generator group) is resolved on the forest-moon plane by the *identical* resolver. When the ground team kills the shield generator, the marker clears, and on the *next* space step the Death Star starts taking damage and dies. **The two planes never share a distance — they share one boolean:** is the guardian alive? That is what makes the film's real tension come out of pure deterministic math, and it dodges the trap that shared cross-domain geometry would create.

**(2) Bombardment edge — the invasion softening.** An orbiting group targets ground groups through a coupling edge with a large *fixed* separation (orbital altitude), so only long-range/bombardment weapons reach down. The fleet softens the garrison; the drop is ground reinforcement groups *appearing* on the ground plane (a join). This is the "soften from orbit, then land" step made real.

---

## 10. The scenario battery — pressure-tested from a squad to the Death Star

The design was tested against the developer's full range — *"everything from my 6-squad Blood Angels to the battle of the 2nd Death Star in space, and everything in between."* How each resolves:

| Scenario | What it stresses | Resolves as |
|---|---|---|
| **6-man Blood Angels squad** | tiny group, two weapons (bolter + chainsword) | one group, one point, two weapon profiles (ranged + melee-at-0) ✅ |
| **Standoff / kiting** | longer range shoots first, unanswered | directed range gate: long-range group fires the short one; artillery role hangs at max range ✅ |
| **Flank with the fast wing** | a wing reaches the enemy's rear | Screen role swung ±90° reaches flank/rear groups the Line screens ✅ |
| **Terrain / cover (ground)** | ground modifiers | terrain stays a kernel-input modifier (armor/soak), plane unchanged ✅ |
| **1000-fighter swarm** | extreme unit count | one bucketed group; `O(buckets)`, sub-ms ✅ |
| **1 dreadnought vs 1000 gnats** | super-unit vs swarm | one super-unit group vs one bucketed group (existing alpha-vs-chip burst-soak) ✅ |
| **3-way free-for-all** | multi-party, no double-fire | needs the explicit fire-allocation step — see §11 (PARTIAL until pinned) |
| **Battle of Endor** | space+ground simultaneity + cross-domain lock | two planes, one boolean objective link; needs the cadence plumbing — see §11 (PARTIAL until pinned) |

Eight of ten fall out cleanly. The two that are marked PARTIAL are not architectural failures — they are two *joints* the design named but did not fully specify, and §11 is the instruction to finish them **before** those slices ship.

---

## 11. The two joints we MUST pin down (the adversarial findings — do not skip)

Two independent adversarial reviewers scored this design highest but both docked it for the **same two under-specified spots**. These are the design's homework, and they land on the two things that matter most, so they are called out here as blocking items for their slices:

1. **Multi-party firepower conservation (the double-count trap).** As written, the range rule loops over *ordered enemy pairs* and builds a fresh fire-mix for each — which hands a group its **full** firepower against **every** enemy it can reach. In a 3+-way fight that multiplies one group's guns across three targets: it shoots as if it had three times the guns. **This violates the "a group's firepower is conserved" constraint until a fire-allocation pass is inserted** — a DPS-budget step that splits a group's *one* pool of fire across its targets (target-weighted), so the total dealt equals the total it has. This is a standard, solvable combat problem, but it must be *designed*, not named. **It is the #1 thing to pin down; it gates the multi-party / FFA slice (S6).**

2. **Combined-theater cadence and ownership.** In a combined battle the space plane wants 5-second steps and the ground plane natively runs at 1 hour. For the shield-generator's death to be *seen* in space at the right moment, the ground plane must be force-stepped at the finer cadence for the theater's duration — and **who owns that stepping** (the new `BattleTheater` vs. the standalone `GroundForcesProcessor`) is currently hand-waved as "extra plumbing." If a battle dynamically forces a 5-second ground cadence, we must prove fast-forward still equals watch. **This must be designed before the combined-battle slice (S5) ships, because it sits directly on the Endor acceptance test.**

Neither is fatal and neither is architectural — but "named" is not "designed." Finish these two joints before calling the affected slices done.

---

## 12. What we rejected — and the cheaper fallback we keep in our pocket

The design-off ran four candidates. What we learned from the losers:

- **Range-Band + Flank-Slot** (score 6.5) — added Left/Center/Right lanes, but the lanes added distance *symmetrically*, so a "flanker" was just **farther away and therefore *less* effective — the opposite of a flank advantage.** Its Left/Right half was "pretty, not decision-bearing" by our own realism rule. **Rejected.** (Lesson baked into the polar bearing rule: a flank must produce an *asymmetric* effect — reduced enemy return-fire or reduced target soak — not just a bigger number.)
- **Discrete Station Lattice** (score 7.5) — assign each group to one of ~11 named stations (depth band × flank lane × layer) and read pair-distance from a constant table. Safest, does the least new work. **Kept as the fallback spine.**
- **Coordinate-Free Reach Graph** — a graph of "who can reach whom" with no coordinates at all. Elegant but its pressure test didn't complete.

**The escape hatch worth remembering:** one reviewer's sharpest point was that *"a deterministic per-pair separation matrix could be the cheaper hybrid that keeps every strength this design has while dropping the geometry it never uses."* Since the plane is never rendered, all it ever produces is a **table of distances between groups**. If, in the build, the full 2D projection proves to be more machinery than the problem needs, we can **degrade polar to the Station-Lattice per-pair matrix** without losing any player-visible behavior — same range rule, same boolean coupling, same bucketing, less trig. Polar is the spine; the per-pair matrix is the pressure-relief valve.

---

## 13. Build slices (additive, default-off, one push per CI-green)

Each slice is byte-identical when its flag is off; each ships with a gauge that is a scenario from the battery; the existing ship fixtures are the byte-identity tripwire on every slice. **Push one slice, wait for CI green (both `test` and `build-client` jobs) before stacking the next.**

- **S0 — `GroupPlane.cs`** pure static (project / offset / distance) + `GroupPlaneTests` only. Nothing calls it. Byte-identical.
- **S1 — anchors in space:** add Anchor/Frame/GroupPositions to `FleetCombatStateDB`, seed from real positions at engagement start (copy the frame to joiners), generalize `AdvanceClosing` to move anchors in 2D. Behind `EnableGroupPlane` (default off). `ClosingTests` stay green.
- **S2 — the 2D range gate:** `WithinWeaponRange`/`SeparationOf` compute the 2D pair-distance when the flag is on (per-sub-fleet gaps become real — the substrate's deferred "Phase 4"). Byte-identical flag-off.
- **S3 — role geometry:** wire `FleetRole → RoleGeometry(bearing, standoff)` and give `FleetRoleComposer.FormRoleSubFleets` its missing game-loop caller. Gauge flank-reaches-rear + artillery-kites.
- **S4 — ground onto the plane (DELIBERATE re-baseline):** `GroundFormation` gets Anchor/Bearing; `ResolveRegionCombat` groups by formation, **buckets identical units**, reads plane distance. Behind `EnableGroundGroupPlane`. **This is a deliberate ground behavior change (bucketing + firepower conservation), signed off with a written reason and new ground gauges — NOT slipped in as byte-identical.**
- **S5 — combined theater:** `BattleTheater` (multi-plane) + objective links (`GuardedByDB`) + bombardment coupling edges + cadence-align. **Pin down joint #2 (cadence ownership) first.** Gauge the invasion softening + the Endor shield-gen → Death-Star cross-link.
- **S6 — multi-party:** reinforcement-as-new-group join + 3-way FFA + cross-plane fire conservation. **Pin down joint #1 (fire-allocation) first.** Gauge FFA + siege-with-relief.

---

## 14. Known weaknesses (the honest list — kept so nobody re-discovers them the hard way)

1. The 3D→2D projection is **lossy** — a battle spread across a full sphere is flattened; bearings/flanking are a doctrine *fiction*, not emergent maneuver. Acceptable because the plane is unseen and micro is forbidden — but no clever flank is ever *discovered*, only *assigned*.
2. Determinism leans on the **fixed hotloop cadence**. The range gate makes closing nonlinear, so if any future path ever feeds a variable time-step into a group step, fast-forward ≠ watch. Guard the fixed-quantum invariant.
3. Ground's native cadence is 1 h; a combined space(5 s)+ground(1 h) battle needs the ground plane force-stepped at 5 s for the theater's duration (joint #2).
4. Objective-link invulnerability is **binary** (shield up = untouchable, shield down = normal) — no partial shield. Matches the Endor fiction but is blunt.
5. "Nearest enemy" can oscillate with 3+ equidistant sides — needs the ID tie-break and possibly hysteresis.
6. The battle frame **must be persisted and copied to joiners**; recomputing it from live positions is *wrong* once ships die (distances would jump). A real save/load + reinforcement correctness trap.
7. Slice S4 knowingly changes ground outcomes (bucketing replaces per-unit resolution; firepower becomes conserved) — it is **not** byte-identical and must be a signed-off re-baseline.
8. Standoff radii and role-bearing constants are **balance-pass defaults**; the standoff-vs-brawl and flank-vs-hold gut-check is a live-tuning question CI cannot answer (client runtime only).

---

## 15. Connections (Prime Directive)

- **Feeds IN:** `FleetCombatStateDB.Separation_m` + `AdvanceClosing` (space closing), `FleetRoleComposer` (the sub-fleet roles that become groups), `GroundFormation` + `GroundForcesProcessor` (ground groups), `PositionDB` (the 3D seed), `FleetDoctrineDB`/`EngagementPosture` (the doctrine lever that picks role geometry).
- **Feeds OUT:** one scalar distance per group-pair into the unchanged pure `CombatKernel` (`HitFraction` + `BuildFireMix` range gate). The kernel never changes.
- **Shares STATE:** `FleetCombatStateDB` (new anchor/frame fields, `[JsonProperty]` + deep-copy), `GroundForcesDB`/`GroundFormation` (new anchor/bearing fields, same save-safety rule), and a new `BattleTheater` for combined fights.
- **Triggers:** nothing new player-facing — the only new outward signal is the existing combat-interrupt (clock stops at first contact). No rendering. No new order type. **Doctrine is the entire control surface.**

**Cradle-to-grave note:** this is a *resolver* layer, not a buildable, so its "cradle to grave" is the doctrine chain — a group's shape comes from the sub-fleet/formation roles the player composes (`FleetRoleComposer` / `GroundFormation`), which come from ships/units the player designed, built, and can lose. Shoot the artillery group's hulls off and its standoff advantage is gone; kill the flank wing and nothing reaches the enemy rear. The shape is only as real as the units that hold it.

---

## How we got here (provenance)

Produced by an adversarial design-off workflow (`2d-resolver-compromise-design`, 2026-07-17): two parallel substrate surveys read the real space + ground resolver code; four candidate designs (Polar / Station-Lattice / Range-Band-Flank-Slot / Reach-Graph) were each built out concretely and then pressure-tested by independent adversarial reviewers instructed to disqualify them against the full scenario battery and the six hard constraints (pure-math / no rendering, `O(groups²)`, doctrine-only no-micro, one unified resolver on the shared kernel, multi-party with conserved fire, byte-identical-when-absent). **Polar scored highest (8/10)** and is adopted here as the spine; the Station-Lattice per-pair matrix is retained as the cheaper fallback; the Range-Band L/R lanes were rejected as inert. The synthesis (this doc) was written by hand because the workflow's auto-synthesis agent hit the session limit — the design content is the reviewers' verified output, not a fresh take.
