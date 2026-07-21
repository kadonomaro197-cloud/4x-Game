# LANE TWOD — pending dashboard rows, cross-lane requests, developer decisions

This is the TWOD lane's conflict-free notes file (CAMPAIGN-PLAN.md §2.4). Lanes do NOT edit the shared
dashboards (`docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`) or a
collision-prone subsystem `CLAUDE.md` mid-flight; each pending row lands here and the integration phase (P8.2)
applies them. Sections are delimited by slice so parallel siblings can append without conflict.

TWOD owns (fence, CAMPAIGN-PLAN.md §3): `GameEngine/Combat/GroupPlane.cs` (new),
`GameEngine/Combat/FleetCombatStateDB.cs`, `GameEngine/Combat/CombatEngagement.cs` (flag-gated blocks only),
`docs/combat/RESOLVER-2D-JOINTS.md`. Design source: `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` §11 (T0 joints)
+ §13 slices S0/S1/S2 (T1/T2/T3). S3+ are a later campaign — NOT in this lane.

---

## T0.1 — Pin fire-allocation + theater cadence (joints memo) — DONE in working tree

**Created:** `docs/combat/RESOLVER-2D-JOINTS.md` (pins BOTH §11 joints), `Pulsar4X.Tests/Resolver2DJointsSpecTests.cs`
(executable-spec fixture; lane-distinct name, lands in the `rest` shard).

**Byte-identity claim: (b), strongest form — the slice adds only a doc + an isolated test fixture and changes ZERO
production code.** No flag, no data path touched. The test reads the `WeaponProfile` public type read-only (public
ctor + getters, no shared-state mutation), so every existing fixture is byte-for-byte unaffected.

### Pending DOCS-INDEX.md row (P8.2 lands it — do NOT edit DOCS-INDEX mid-flight)
Add under the combat-design docs:
| Doc | Purpose | Status |
|-----|---------|--------|
| `docs/combat/RESOLVER-2D-JOINTS.md` | Pins the two under-specified joints of the locked 2D group-plane resolver — (1) conserved, target-weighted, residual-exact fire-allocation (3-way FFA worked example; kills the double-count trap) and (2) combined-theater cadence (`BattleTheater` owns a fixed-5s ground fight-step from inside the space trigger; fast-forward==watch proof). Gates slices S6 (multi-party) + S5 (combined theater). | 🔒 design-pinned (2026-07-18), build-state: not started (S5/S6 consume it) |

### Pending TESTING-TRACKER.md row (P8.2 lands it)
- **Resolver2DJointsSpecTests** (engine/CI, `rest` shard) — executable specification for the two 2D-resolver joints.
  Asserts: fire-allocation conserves system-wide (total dealt == total owned) in a 3-way FFA; kills the double-count
  trap (naive 2× vs conserved 1×); target-weighting concentrates yet conserves; order-independence + tie-determinism;
  WeaponProfile-mix split conserves total DPS + preserves Nature/Delivery. Cadence: 720×5s==1h divisibility;
  fast-forward==watch at the fixed quantum (and a variable 1h step diverges); theater-active tick set identical
  watch-vs-ff. What-it-unblocks: S5 (combined theater / Endor) + S6 (multi-party / FFA).

### Pending Combat/CLAUDE.md row (put here to avoid a parallel-sibling collision on that shared file)
Add to the Combat File Map (or a "2D resolver joints" note): a row for `docs/combat/RESOLVER-2D-JOINTS.md` — the
pinned design for the two group-plane joints; and note that when S6 wires `AllocateFire` it REPLACES the
`1.0 / split` scale in `StepEngagementGroup` (`CombatEngagement.cs:729`) behind the group-plane flag (uniform weights
→ `1/count` → byte-identical to today's equal split), and folds the ground per-faction pool
(`GroundForcesProcessor.ResolveRegionCombat` `cs:297-379`, the current double-count site) onto the same one-pool
allocation.

### Cross-lane note (informational — no request, no fence breach)
- The cadence joint's ownership contract (memo §2.3 beat 2) has the native **GROUND** `GroundForcesProcessor` yield
  exactly `ApplyEngagementManeuvers` + `ResolveRegionCombat` for a theater-driven body while a `BattleTheater` is
  active. That handoff is a GROUND-lane + TWOD-lane touch point that only materializes at **S5** (a LATER campaign,
  not this lane's T0–T3). Flagging now so whoever builds S5 knows the two lanes meet there; nothing to change today.

---

## T1.1 — S0 `GroupPlane.cs` pure static + `GroupPlaneTests` — DONE in working tree

**Created:** `GameEngine/Combat/GroupPlane.cs` (pure static, NO caller) + `Pulsar4X.Tests/GroupPlaneTests.cs`
(lane-distinct fixture name; lands in the `rest` shard by the complement filter, ci.yml line 33 — no ci change).

`GroupPlane` is the invisible battle graph-paper math of RESOLVER-2D-GROUP-PLANE-DESIGN.md §13 S0:
- `SeedFrame(seeds)` → `BattleFrame` (Origin/XAxis/YAxis as 3D unit vectors). Deterministic basis: seeds sorted by
  id first (order-independent, incl. the centroid sum); XAxis = lowest-id→centre; YAxis = widest spread ⟂ XAxis
  (id tie-break); degenerate fallbacks (no seeds → UnitX/UnitY; lowest-id AT centre → farthest seed; no spread →
  `AnyPerpendicular`). Never throws.
- `Project(frame, pos)` → Vector2 (u,v) via dot with the FROZEN axes — a joiner is placed with the stored frame.
- `EnemyDirection(anchor, enemies)` → unit Vector2 toward the NEAREST enemy anchor, LOWEST-id tie-break
  (order-independent; relative-tolerance ties), Zero when none/coincident.
- `RoleOffset(enemyDir, bearingDeg, alongStandoff, perpSpread)` → the doctrine nudge (pure trig; 0°=at enemy,
  ±90°=flank, 180°=rear; negative standoff kites; perpSpread fans). `Place(anchor, offset)`, `PairDistance(a,b)`.

**Byte-identity claim: (b), strongest form — a NEW pure-static class with ZERO live callers.** No production code
invokes `GroupPlane`; no flag, no data path, no existing type touched. It cannot change any existing behaviour, so
every current green test is byte-for-byte unaffected. (No FLAGGED balance numbers: the only literals are float
epsilons — `Epsilon = 1e-9` and a `1e-6` relative tie tolerance — numerical, not gameplay.)

### Pending DOCS-INDEX.md status flip (P8.2 lands it — do NOT edit DOCS-INDEX mid-flight)
- `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` build-state: was "not started (S0 is the first slice)"; now
  **S0 built (pure `GroupPlane.cs` + `GroupPlaneTests`, no caller); S1+ pending**. (Header line 3 of that doc also
  reads "Build state: not started (S0 is the first slice)" — a future in-fence slice or P8.2 can refresh it; that doc
  is design source, not in this lane's edit fence, so leaving the flip as a pending note here.)

---

## T2.1 — S1 anchors in space (`FleetCombatStateDB` + 2D closing) behind `EnableGroupPlane` — DONE in working tree

**Changed:** `GameEngine/Combat/FleetCombatStateDB.cs` (new plane fields + Clone), `GameEngine/Combat/CombatEngagement.cs`
(new `EnableGroupPlane` flag + seeding + 2D anchor movement, all flag-gated). **Created:**
`Pulsar4X.Tests/EfGroupPlaneAnchorTests.cs` (lane-distinct fixture; lands in the `rest` shard, no ci.yml change).

Slice S1 of RESOLVER-2D-GROUP-PLANE-DESIGN.md §13 — WIRE the S0 `GroupPlane` math into the space engagement:
- `FleetCombatStateDB` gains `HasFrame`/`FrameOrigin`/`FrameXAxis`/`FrameYAxis` (the frozen `BattleFrame`, stored as three
  loose `Vector3`s — the `FleetRetreatDB.RetreatVector` save pattern), `Anchor` (this fleet's 2D point) and
  `GroupPositions` (`List<Vector2>`; S1 = one entry, the whole-fleet group at the anchor; S3 fills per-role). All
  `[JsonProperty]` + deep-copied in the copy ctor (Vector2/Vector3 copy by value; the list is rebuilt).
- `CombatEngagement.StartEngagement` seeds the plane from BOTH fleets' real positions (`GroupPlane.SeedFrame`/`Project`);
  `EnsureInCombat` COPIES the frozen frame from the lowest-id already-engaged sibling (the "copy the frame to joiners"
  rule), or seeds a fresh two-fleet board from itself + its representative opponent for the FIRST fleet in via the
  auto-trigger `Tick` path (order-independent `SeedFrame` → the sibling that copies later lands on the identical board).
- `AdvanceClosing` is generalized: after the unchanged scalar update it slides the CONTROLLER's anchor along
  `GroupPlane.EnemyDirection` by the exact scalar-gap delta ("the faster side closes the distance"); the anchor
  pair-distance tracks the scalar gap when seeded consistent. New private helpers `AdvanceAnchorPlane` /
  `FrameOf` / `StoreFrame` / `SeedPlaneForPair` / `SeedPlaneForJoiner`.
- Master switch **`CombatEngagement.EnableGroupPlane` (public static, default FALSE)**. Seeding gated on this flag alone
  (independently testable); anchor MOVEMENT additionally needs `EnableClosingRange` (AdvanceClosing only runs under it).

**Byte-identity claim: (a) default-off flag.** All behaviour is behind `EnableGroupPlane = false`. Flag off → no plane
data is EVER seeded (both StartEngagement/EnsureInCombat blocks skip), `AdvanceClosing` captures nothing and never calls
`AdvanceAnchorPlane` (its scalar loop is byte-for-byte unchanged), and the new `FleetCombatStateDB` fields stay at their
defaults (`HasFrame=false`, zero vectors, empty list). Verified by reading every flag-off path. The existing
`ClosingTests` (which never touch `EnableGroupPlane`) + all ship-combat fixtures are the tripwire; `EfGroupPlaneAnchorTests`
adds an explicit flag-OFF-leaves-state-at-default gauge. The new `[JsonProperty]` fields are additive & save-safe
(default-valued, round-trip cleanly). **No new FLAGGED balance numbers** — `EnableGroupPlane` is a boolean and the anchor
slide reuses the existing `ClosingSpeedScale_mps` + scalar gap; no gameplay constant introduced.

### Pending Combat/CLAUDE.md rows (put here to avoid a parallel-sibling collision on that shared file — same as T0.1/T1.1)
Update the `FleetCombatStateDB.cs` row in the Combat File Map to note it now also carries the S1 group-plane fields:
> `FleetCombatStateDB.cs` … **+ 2D group-plane (Operation Earthfall T2.1 / slice S1):** `HasFrame` + frozen frame
> (`FrameOrigin`/`FrameXAxis`/`FrameYAxis`, three loose Vector3s) + `Anchor` (2D point) + `GroupPositions`
> (`List<Vector2>`, S1 = one whole-fleet group). Seeded at engagement start / copied to joiners; slid by `AdvanceClosing`.
> Inert unless `CombatEngagement.EnableGroupPlane` (default off) → byte-identical scalar path when off.

Add to the `CombatEngagement.cs` row (or the "Closing distance" section): a note that **`EnableGroupPlane` (default off,
S1)** seeds the frozen 2D `GroupPlane` frame + per-fleet anchor at engagement start (`SeedPlaneForPair` /
`SeedPlaneForJoiner` — joiners COPY the frozen board) and generalizes `AdvanceClosing` to slide the controller's anchor
along `GroupPlane.EnemyDirection` by the scalar-gap delta ("the faster side closes"). Anchor movement needs BOTH
`EnableGroupPlane` and `EnableClosingRange`; seeding needs only the former. Nothing READS the 2D pair-distance yet — the
scalar `Separation_m` is still authoritative until slice **S2** rewires `SeparationOf`/`WithinWeaponRange` to the 2D gap.
Gauge `EfGroupPlaneAnchorTests`.

### Pending TESTING-TRACKER.md row (P8.2 lands it)
- **EfGroupPlaneAnchorTests** (engine/CI, `rest` shard) — the S1 group-plane wiring gauge. Asserts: **flag-on seeding**
  (StartEngagement's stored frame + anchor == the pure `GroupPlane.SeedFrame`/`Project` from the same read-back
  positions; both fleets share one frozen board; S1 one-group == anchor); **flag-off byte-identity** (across a full step,
  `HasFrame` stays false, anchor Zero, group list empty — the scalar path is untouched); **joiner copies the FROZEN
  board** (a joining fleet's frame origin is bit-identical to the existing A+B board and differs from a self-reseed —
  proves "copy, don't redraw"); **anchor movement** (controller's anchor slides toward the enemy by the exact scalar-gap
  delta, the anchor pair-distance tracks the scalar gap, the stationary side holds). What-it-unblocks: S2 (the 2D range
  gate reads the pair-distance).

### Pending DOCS-INDEX.md status flip (P8.2 lands it)
- `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` build-state: now **S0 + S1 built (pure `GroupPlane.cs` + `GroupPlaneTests`;
  `FleetCombatStateDB` anchors + 2D `AdvanceClosing` behind `EnableGroupPlane` + `EfGroupPlaneAnchorTests`); S2+ pending**.

### Cross-lane requests / developer decisions
- None. No fence breach (only `FleetCombatStateDB.cs` + `CombatEngagement.cs` flag-gated blocks + a new lane-distinct test,
  all inside the TWOD fence). No new balance number to decide. `EnableGroupPlane` defaults off (client turns on when the
  2D model is live, a later slice).

### Pending Combat/CLAUDE.md row (put here to avoid a parallel-sibling collision on that shared file — same as T0.1)
Add to the Combat File Map:
| `GroupPlane.cs` | **NEW (2D group-plane resolver, slice S0, Operation Earthfall T1.1)** Pure-static "invisible battle graph-paper" math (`docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` §13). `SeedFrame` lays a battle-local 2D `BattleFrame` (Origin + orthonormal XAxis/YAxis) down ONCE from the fighters' real 3D positions — deterministic basis (seeds sorted by id; XAxis = lowest-id→centre; YAxis = widest ⟂ spread, id tie-break; degenerate fallbacks, never throws). `Project` flattens a 3D position onto the FROZEN frame (a joiner uses the stored axes, so gaps don't jump as ships die). `EnemyDirection` gives the nearest-enemy facing with a lowest-id tie-break (no oscillation). `RoleOffset(enemyDir, bearingDeg, alongStandoff, perpSpread)` is the doctrine nudge as pure trig (0°=at enemy / ±90°=flank / 180°=rear; −standoff kites; perpSpread fans). `PairDistance` = the single scalar the plane will hand the unchanged 1-D `CombatKernel`. **NOTHING calls it (S0)** — byte-identical by construction; S1 seeds anchors in `FleetCombatStateDB`, S2 the 2D range gate. Gauge `GroupPlaneTests`. | ✅ S0 (pure math, no caller) |

### Pending TESTING-TRACKER.md row (P8.2 lands it)
- **GroupPlaneTests** (engine/CI, `rest` shard) — the S0 group-plane math gauge. Asserts: frame determinism (shuffled
  seeds → bit-identical frame); axes orthonormal; two-sides-facing-off collapse to 1-D (second axis ~0, x-axis carries
  the full gap — the byte-identical path); `RoleOffset` trig per bearing (0/±90/180, negative standoff, perpSpread,
  zero-enemyDir default facing / no-NaN); joiner projected with the STORED frame (matches hand-computed dots; a
  re-seed WITH the joiner would jump an existing fleet's point — why we freeze, design weakness #6); `EnemyDirection`
  nearest-wins + equidistant lowest-id tie-break (order-independent) + degenerate→Zero; degenerate seeding returns a
  usable frame; `PairDistance` Euclidean. What-it-unblocks: S1 (anchors in `FleetCombatStateDB`) + S2 (2D range gate).

### Pending DOCS-INDEX.md status flip (P8.2 lands it — do NOT edit DOCS-INDEX mid-flight)
- `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` build-state: was "not started (S0 is the first slice)"; now
  **S0 built (pure `GroupPlane.cs` + `GroupPlaneTests`, no caller); S1+ pending**. (Header line 3 of that doc also
  reads "Build state: not started (S0 is the first slice)" — a future in-fence slice or P8.2 can refresh it; that doc
  is design source, not in this lane's edit fence, so leaving the flip as a pending note here.)

---

## T3.1 — S2 the 2D range gate (`SeparationOf`/`WithinWeaponRange` read the plane) behind `EnableGroupPlane` — DONE in working tree

**Changed:** `GameEngine/Combat/CombatEngagement.cs` (plane-aware `SeparationOf` + new private `TryPlanePairDistance`;
plane-aware `WithinWeaponRange(Entity,Entity)` via new private `RangeBetween`), `GameEngine/Combat/FleetCombatStateDB.cs`
(the S1 `Anchor` forward-reference doc comment flipped from "the S2 range gate WILL read…" to "S2 (built): SeparationOf/
WithinWeaponRange now read…"). **Created:** `Pulsar4X.Tests/EfGroupPlaneRangeGateTests.cs` (lane-distinct fixture;
lands in the `rest` shard, no ci.yml change).

Slice S2 of RESOLVER-2D-GROUP-PLANE-DESIGN.md §13 — WIRE the range gate to the 2D plane:
- `SeparationOf(fleet)`: still gated on `EnableClosingRange` FIRST (returns 0 when closing off → the range gate is a
  no-op → byte-identical). When `EnableGroupPlane` is ALSO on and the plane is live (`HasFrame`) for BOTH this fleet
  and its representative opponent (`OpponentFleetId`), it returns the straight-line 2D `GroupPlane.PairDistance` between
  the two fleets' group anchors — the REAL per-fleet-pair gap ("per-sub-fleet gaps become real", the substrate's
  deferred "Phase 4") — else it falls back to the scalar `Separation_m`. The damage KERNEL is untouched and still 1-D:
  the plane only supplies the single scalar `d` that `BuildFireMix`'s per-weapon range gate + `ApplyCasualties`' dodge
  falloff already read. So the DIRECTED gate falls out of the existing per-weapon gate: at pair-distance `d`, fleet A's
  long guns (Range_m ≥ d) fire while fleet B's short guns (Range_m < d) are gated to an empty mix — "farthest range
  shoots first," now measured on the plane.
- `WithinWeaponRange(Entity,Entity)` (the engage-trigger gate): its distance source is the new `RangeBetween(a,b)` —
  the 2D anchor pair-distance when the plane is on AND both fleets are framed, else the real 3D `FleetSeparation`. The
  live engage-trigger runs BEFORE the plane is seeded (neither fleet framed), so it falls straight through to
  `FleetSeparation` — the live trigger behaviour is unchanged; the plane path is exercised by the gauge (and any future
  in-combat re-check). The pure `WithinWeaponRange(double,double,double)` overload is untouched.
- New private helpers only: `TryPlanePairDistance` (fleet↔opponent anchor distance, both-framed guard, never mutates,
  never throws) and `RangeBetween`. No new public/static API, no flag added (rides the S1 `EnableGroupPlane`).

**Byte-identity claim: (a) default-off flag.** All new behaviour is behind `EnableGroupPlane = false` (default). Flag
off → `SeparationOf` skips the plane branch and returns exactly the old scalar-or-0, and `RangeBetween`/`WithinWeaponRange`
return the old `FleetSeparation` — verified line-by-line. The only fixtures that set `EnableGroupPlane = true` are the
lane's own `EfGroupPlaneAnchorTests` (T2.1) + this new `EfGroupPlaneRangeGateTests`; every other CI fixture has it off,
so it is literally byte-identical for them. The one plane-ON fixture that also drives a combat step
(`EfGroupPlaneAnchorTests.AdvanceClosing_FlagOn_…`) is unaffected because at engagement seed the 2D anchor pair-distance
EQUALS the scalar gap (both seeded from the same real positions), so its single step reads the same `d` within float
epsilon — far from any weapon-range boundary, so no gate flip and its geometry/pool assertions are unchanged.
**No new FLAGGED balance numbers** — no gameplay constant introduced (the only literals are test-scenario ranges/positions).

### Pending Combat/CLAUDE.md rows (put here to avoid a parallel-sibling collision on that shared file — same as T0.1/T1.1/T2.1)
Update the `CombatEngagement.cs` row (the "Closing distance" section) to note S2:
> **`EnableGroupPlane` slice S2 (the 2D range gate, Operation Earthfall T3.1):** `SeparationOf` and the entity-level
> `WithinWeaponRange` now measure the 2D pair-distance between two fleets' group anchors (`GroupPlane.PairDistance` of
> the frozen-plane `Anchor`s) instead of the shared scalar, when `EnableGroupPlane` is on and BOTH fleets are framed —
> per-fleet-pair gaps become real (the substrate's deferred "Phase 4"). The damage kernel stays 1-D (it receives the
> scalar `d`); the directed "long-range fires, short-range can't answer" gate falls out of the existing per-weapon
> range gate in `BuildFireMix` now that each fleet's gap is its own pair-distance. `SeparationOf` is STILL gated on
> `EnableClosingRange` first (0 when closing off → no gating), and `WithinWeaponRange`'s new `RangeBetween` helper
> falls back to the real 3D `FleetSeparation` when a fleet isn't framed — so the live engage-trigger (pre-seed) is
> unchanged and every flag-off fixture is byte-identical. Gauge `EfGroupPlaneRangeGateTests`.

And in the `FleetCombatStateDB.cs` row, extend the T2.1 note: the `Anchor` field is now READ by the range gate at S2
(`SeparationOf`/`WithinWeaponRange` → `GroupPlane.PairDistance` of two anchors), not just written by `AdvanceClosing`.

### Pending TESTING-TRACKER.md row (P8.2 lands it)
- **EfGroupPlaneRangeGateTests** (engine/CI, `rest` shard) — the S2 2D-range-gate gauge. Asserts: **directed gate on
  the plane** (a 100 km fleet fires a 1 km fleet 50 km away; the short side deals ZERO even with the SCALAR gap forced
  to 500 m — proving the resolver read the 2D ANCHOR distance, not the scalar); **flag-off byte-identity** (same
  geometry, plane off → the scalar drives the gate → the short gun fires, the opposite outcome); **WithinWeaponRange
  reads the anchors** (moving a framed fleet's anchor flips the gate while the real 3D `FleetSeparation` is unchanged;
  plane-off reverts to the 3D read); **plane-on / closing-off guard** (`SeparationOf` stays 0 so the range gate is a
  no-op even with the plane seeded — the closing flag is the master switch). What-it-unblocks: S3 (role geometry — the
  per-role sub-fleet groups the pair-distance will then span).

### Pending DOCS-INDEX.md status flip — S2 (P8.2 lands it)
- `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` build-state: now **S0 + S1 + S2 built** (pure `GroupPlane.cs` +
  `GroupPlaneTests`; `FleetCombatStateDB` anchors + 2D `AdvanceClosing` behind `EnableGroupPlane` + `EfGroupPlaneAnchorTests`;
  the 2D range gate `SeparationOf`/`WithinWeaponRange` + `EfGroupPlaneRangeGateTests`); **S3+ pending** (S3–S6 are a
  later campaign — NOT this lane). Header line 3 of that doc still reads "not started" — a P8.2 refresh, that doc is
  design source, not in this lane's edit fence.

### Cross-lane requests / developer decisions
- None. No fence breach (only `CombatEngagement.cs` + `FleetCombatStateDB.cs` inside the TWOD fence + a new lane-distinct
  test). No new balance number to decide. `EnableGroupPlane` stays default-off; nothing reads the 2D role geometry yet
  (S3). Note: S3+ (role geometry, ground onto the plane, combined theater, multi-party) are explicitly a LATER campaign
  per CAMPAIGN-PLAN.md §4 — this lane (T0–T3) ends at S2.

---
