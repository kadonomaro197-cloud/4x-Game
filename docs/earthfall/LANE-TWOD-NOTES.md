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
