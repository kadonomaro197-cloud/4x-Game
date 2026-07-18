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
