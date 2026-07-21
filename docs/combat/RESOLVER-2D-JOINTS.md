# The Two Joints — Fire-Allocation + Combined-Theater Cadence (PINNED)

**Status: 🔒 pinned (Operation Earthfall, slice T0.1). Gates slices S6 (multi-party) and S5 (combined theater) of `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md`.**
**Parent design: `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` §11 (the two under-specified joints). This memo finishes the homework §11 assigned — it does NOT re-open the locked design.**

---

## Why this file exists (the point before the plumbing)

The 2D group-plane resolver was design-locked with **two joints named but not designed** (§11). Both reviewers docked the design for the same two spots, and both spots sit on the two acceptance tests that matter most — a 3-way free-for-all, and the Battle of Endor. "Named" is not "designed," so before those two build slices ship, the joints get pinned here with concrete math, pseudocode, a worked example, and a determinism proof.

Think of it like a ship's firemain and its watch bill:

- **Joint #1 is the firemain manifold.** One pump (a group's guns) feeds several outlets (the enemies it can shoot). If you open every outlet to full pressure you're pretending the pump moves more water than it does. The fix is a manifold that splits the pump's *one* output across the outlets — the total delivered equals the total pumped, no matter how many outlets are open. That is **conserved fire-allocation**.
- **Joint #2 is the watch bill for a two-front action.** Space combat stands a 5-second watch; ground combat stands a 1-hour watch. When a single battle spans both decks (a fleet in orbit *and* troops on the moon), somebody has to decide who rings the ground watch faster so the two decks stay in step — and prove that ringing it faster doesn't change the outcome depending on whether the captain is watching or fast-forwarding. That is **combined-theater cadence**.

Neither is hard. Both are exactly specifiable. This is that specification.

---

## The vocabulary this memo uses (matches the locked design)

- **Group** — the only thing with a position (§2): a sub-fleet in space, a formation on the ground, a lone capital ship, a bunker. A group carries **one DPS pool** = the total damage-per-second of all its guns (`CombatEngagement.BuildFireMix` already collapses a group's weapons into a short list of weighted `WeaponProfile` buckets; `TotalDamage(fireMix)` is that pool).
- **Engageable set `T_A`** — the enemy groups group `A` can shoot this step: hostile, both manned, in range on the plane, and (fog on) detected by `A`. This is the directed `targetsOf[A]` the resolver already builds (`CombatEngagement.cs:641-661`).
- **The step** — one fixed-cadence hotloop tick (5 s space / 1 h ground). Everything in this memo happens inside one step, on the pre-salvo snapshot, before any casualty lands (the design's simultaneity rule, §7).

---

# JOINT #1 — Conserved Fire-Allocation

## 1.1 The trap, stated exactly

The range rule (§5) is **directed and per-pair**: for every ordered enemy pair `A→B` it can build a fresh fire-mix. If each pair gets `A`'s **full** pool, then a group facing three enemies deals `3 × P_A` — it shoots as if it had three times the guns. Firepower is not conserved.

**Where this bug lives in the real code right now (the ledger):**

| Side | File:line | State | What it does |
|---|---|---|---|
| **Space** | `CombatEngagement.StepEngagementGroup`, `cs:724-730` | **Already conserved (equal split)** | Each attacker `g` divides its fire by `split = targetsOf[g].Count`: `AddScaledFire(incoming, fire[g], 1.0 / split)`. Total dealt == total owned, but the split is **equal** across targets — not target-weighted. |
| **Ground** | `GroundForcesProcessor.ResolveRegionCombat`, `cs:297-379` | **Has the double-count bug** | The pool is rebuilt **per enemy faction**: `foreach f { foreach g { foreach u in f { double pool = atk * SalvoScale; ...spread across g's reachable... }}}`. A unit `u` whose faction faces two enemy factions `g1, g2` applies its **full pool to each** — `2 × P_u` in a 3-way fight. This is exactly the trap §11 names, and §6 promises the group resolver fixes it. |

The 2D group resolver unifies both onto one allocation step. That step must:

1. **Conserve** — `Σ_{B∈T_A} dealt(A→B) == P_A`, exactly (to the arithmetic, not "about equal").
2. **Weight** — split by target priority, not blindly equal (the enhancement over today's space `1/split`).
3. **Be deterministic** — same inputs → same output; independent of iteration order; reproducible tiebreaks (fast-forward == watch, §7).

## 1.2 The algorithm (residual-exact, target-weighted)

```
// Runs once per firing group A, per step, on the pre-salvo snapshot.
// P_A      : A's total DPS pool  = TotalDamage(BuildFireMix(A, separation))
// T_A      : A's engageable enemy groups (directed targetsOf[A])
// weight(B): A's raw desire to shoot B (see 1.3); always > 0 for any B in T_A

AllocateFire(P_A, T_A, weight):
    if T_A is empty:  return {}          // A deals nothing this step (no carry — the pool is
                                         // regenerated from BuildFireMix next step, unlike the
                                         // per-defender DAMAGE pool which does carry)

    // (1) DETERMINISTIC ORDER — sort targets by ascending stable group id.
    //     Space: the sub-fleet's Entity.Id.  Ground: the formation's stable id (GroundFormation
    //     leader/UnitId — the ground echo of an entity id).  This fixes both the residual owner
    //     and any iteration-order dependence.
    order = sort(T_A) by GroupId ascending

    // (2) WEIGHTS → SHARES that sum to EXACTLY 1.0 via residual assignment.
    W = Σ_{B in order} weight(B)         // > 0 by construction (every weight > 0)
    dealt = {}
    running = 0
    for i in [0 .. count-2]:             // every target EXCEPT the last
        d = P_A * (weight(order[i]) / W)
        dealt[order[i]] = d
        running += d
    dealt[order[last]] = P_A - running   // THE RESIDUAL — the last target gets exactly what's left

    return dealt
```

**Why the residual guarantees exact conservation.** `Σ dealt = running + (P_A − running) = P_A`, in one subtraction, with **no accumulated floating-point drift**. We never sum `count` separately-rounded shares and hope they land on `P_A`; we compute `count−1` of them and *back out* the last. A group can therefore never deal more than its pool (the double-count trap is structurally impossible) nor mysteriously less (no rounding leak). The residual owner is deterministic (highest group id in `T_A`), so watch and fast-forward assign the same residual to the same target.

**Splitting the fire MIX, not just the scalar.** A group's fire is a list of `WeaponProfile` buckets, not one number. Apply the **same per-target share** `s_B = dealt(A→B) / P_A` to scale every weapon profile in `A`'s mix into `B`'s incoming pile — exactly what `AddScaledFire(incoming, fire[A], s_B)` already does, but with `s_B` = the weighted share instead of `1.0 / split`. The scalar total is conserved **exactly** (residual); the per-weapon mix is conserved to floating-point epsilon per bucket (the test asserts a tight tolerance on the mix and exact equality on the scalar). Nature/Delivery/velocity/tracking ride along unchanged — only the *amount* of each flavor is scaled, so the downstream PD-intercept / shield-nature / armour-nature / dodge-bucket pipeline is untouched.

## 1.3 The weight function (the tunable layer, with a byte-identity floor)

The **conservation machinery above is the hard invariant** and never changes. The **weight** is the tunable part. Pinned default:

```
weight(A→B) = threatPriority(B) × hittability(A→B)      // both terms > 0

threatPriority(B) = max(TotalDamage(BuildFireMix(B)), MinTargetWeight)
    // "shoot the guns that can hurt you first." A group with no guns still gets the
    // MinTargetWeight floor so an unarmed/utility group is finishable, never un-targetable.

hittability(A→B) = max(LandedFraction(fire_A, evasion_B, separation_AB), MinHitWeight)
    // "concentrate where you actually connect." Reuses the existing dodge kernel so a
    // hard-to-hit flanker draws proportionally less fire. Floored so it's never zero.
```

**The byte-identity floor (why S6 flag-off is safe).** When both terms are uniform across `T_A` — every target equally threatening and equally hittable — the weighted share collapses to `1/count`, which is **exactly today's space `1.0 / split`**. So:

- **S6 flag OFF** → the existing `StepEngagementGroup` equal-split path runs unchanged → every current combat fixture is byte-for-byte identical. (The new allocation is only reached behind the group-plane flag.)
- **S6 flag ON, uniform targets** → weighted allocation *reduces to* equal split → same numbers.
- **S6 flag ON, differentiated targets** → the new, conserved, target-weighted behavior.

**FLAGGED balance values** (the developer sets these; the agent does not tune balance):
- `MinTargetWeight` — floor weight so an unarmed group is still a valid, finishable target.
- `MinHitWeight` — floor on the hittability term so a perfect dodger still draws *some* fire (mirrors the resolver's existing `MinLandedFraction`).
- The **choice of weight terms** itself (threat-only / hittability-only / both / equal) is a doctrine-tuning decision, not a fixed law. Default = the product above.

## 1.4 Worked example — the 3-way free-for-all (§10, the PARTIAL scenario)

Three groups, one per faction, all mutually hostile and all in range (a true FFA). Pools:

| Group | Pool `P` |
|---|---|
| **A** | 100 DPS |
| **B** | 60 DPS |
| **C** | 40 DPS |

### Equal weights (the byte-identity path — matches today's space `1/split`)

Each group splits its pool evenly across its two enemies:

| Firer | → A | → B | → C | Σ dealt | == pool? |
|---|---|---|---|---|---|
| **A** (100) | — | 50 | 50 | 100 | ✅ |
| **B** (60)  | 30 | — | 30 | 60 | ✅ |
| **C** (40)  | 20 | 20 | — | 40 | ✅ |

**Incoming each group takes:** A ← 30+20 = **50**; B ← 50+20 = **70**; C ← 50+30 = **80**.

**System-wide conservation check:** total dealt = 50+70+80 = **200** = total owned = 100+60+40 = **200**. ✅ No group multiplied its guns.

**Contrast the double-count bug** (ground's current behavior): A would deal its **full 100 to B AND its full 100 to C** = 200 dealt out of a 100 pool — doubled. Scale that to a 4-way fight and A triples. The residual allocation makes this structurally impossible.

### Target-weighted (weight = target threat = target's own pool, "focus the dangerous one")

A's targets B (w=60) and C (w=40), `W=100`. Order by id — say B before C, C the residual owner:

- `dealt(A→B) = 100 × 60/100 = 60`
- `dealt(A→C) = 100 − 60 = 40` (residual)
- Σ = **100** ✅ — A concentrates on the bigger threat B while still conserving.

B's targets A (w=100), C (w=40), `W=140`: `dealt(B→A)=60/140×... ` → `B→A = 60×100/140 ≈ 42.857`, `B→C = 60 − 42.857 = 17.143`, Σ = **60** ✅. Every firer's total still equals its pool, exactly, whatever the weights.

## 1.5 Cross-plane conservation (the S6 "cross-plane fire conservation" clause)

In a combined battle a bombarding space group targets ground groups through a coupling edge (§9). Its pool is allocated by the **same** `AllocateFire` over an engageable set that spans both planes (its space targets *and* the ground groups it can reach down the bombardment edge). One pool, one allocation, one residual — so a fleet that both fights ships and shells the surface still deals exactly its pool total, never once-per-plane. The allocation step does not care which plane a target sits on; it only reads the target's group id, weight, and the pair-distance the plane hands it.

---

# JOINT #2 — Combined-Theater Cadence & Ownership

## 2.1 The problem, stated exactly

A combined battle (§9) holds two planes stepped in the same tick: a **space plane** whose native cadence is **5 s** (`BattleTriggerProcessor`) and a **ground plane** whose native cadence is **1 h** (`GroundForcesProcessor.RunFrequency`). For the Endor lock to read right — the shield generator dies on the ground and the Death Star becomes vulnerable in space on the *next* space step — the ground fight must resolve at the finer 5 s cadence for as long as the theater lasts. Two questions §11 left open:

1. **Who owns the faster ground stepping** — the new `BattleTheater`, or the standalone `GroundForcesProcessor`?
2. **If a battle dynamically forces a 5 s ground cadence, does fast-forward still equal watch?**

## 2.2 The mechanism we're standing on (verified in the code)

The determinism already present, which the answer reuses rather than replaces:

- **Every hotloop's `deltaSeconds` is derived from ITS OWN `RunFrequency` boundary, not the master step.** In `ManagerSubPulse.ProcessToNextInterupt` (`cs:346-347`), `deltaSeconds = (_subStepDateTime − _systemLocalDateTime)`, and sub-steps are placed at each processor's next-run boundary (`GetNextInterupt`, `HotLoopProcessorsNextRun`). So the **space trigger always fires with `dt = 5`** and the **ground processor always fires with `dt ≈ 3600`**, regardless of how large a chunk the player fast-forwards. A 1-hour master jump runs the space trigger 720× at 5 s and the ground processor once at 3600 s — identical to watching it. **Each plane is already fast-forward==watch *independently*, because its quantum is fixed by its RunFrequency.**
- `MasterTimePulse.CombatReactionStep = 5 s` is the same 5 s the interrupt fine-steps at (`MasterTimePulse.cs:95`) — the space combat grid.
- `3600 / 5 = 720` **exactly** — the 5 s grid nests perfectly inside the 1 h grid with no remainder. This is load-bearing (2.5).

## 2.3 The ownership decision (PINNED)

> **The `BattleTheater` owns the ground plane's fight-cadence for the duration of a combined battle. It force-steps the theater body's ground fight at a FIXED 5 s quantum (`TheaterGroundQuantum = CombatReactionStep = 5 s`) — the same fixed quantum watched or fast-forwarded — by riding the space trigger that already runs at 5 s. When the theater dissolves, the ground plane returns to its native 1 h `GroundForcesProcessor` cadence.**

The contract, in four beats:

**(1) Formation (deterministic trigger).** When `CombatEngagement.Tick` (the 5 s space trigger) finds an engagement **coupled to a surface** — a bombardment edge exists to a body's `GroundForcesDB`, or an objective-link (`GuardedByDB`, §9) points at a ground group in this system — it creates/attaches a `BattleTheater` marker binding the space engagement to that body's ground plane. The trigger is a **pure function of game state** evaluated at a 5 s boundary → it forms at the same game-time whether watched or fast-forwarded.

**(2) Ownership transfer (L9-safe).** While a `BattleTheater` is active for a body, that body's `GroundForcesDB` is flagged **theater-driven**. The native `GroundForcesProcessor.ProcessBody` **skips exactly two steps** for a theater-driven body — `ApplyEngagementManeuvers` (the ROE fight-maneuver) and `ResolveRegionCombat` (the salvo). It **still runs** movement, upkeep, radar reveal, environmental attrition, and the formation-order queue at 1 h — those are travel/economy timers that need no finer grain and stay deterministic at their own quantum. **No second hotloop is added to `GroundForcesDB`** (landmine L9): the native processor remains the only hotloop keyed to that blob; it merely yields the *fight* for a theater-owned body.

**(3) The theater step (borrows the space trigger's fixed 5 s dt).** For each active `BattleTheater`, `CombatEngagement.Tick` — already running at 5 s with `dt = 5` — steps **both** planes with that **same `dt`**: the space plane through `StepEngagementGroup`, and the theater body's ground fight by *calling* the extracted `ApplyEngagementManeuvers` + `ResolveRegionCombat` **as functions** with `deltaSeconds = 5`. The ground fight is thus stepped at exactly 5 s, from inside the space trigger, using the trigger's own fixed quantum. It never sees a variable step, never sees 3600.

**(4) Return.** When the space engagement ends (no coupled fleets remain, or the surface is captured/cleared — a deterministic condition at a 5 s boundary), the `BattleTheater` marker is removed, the theater-driven flag on `GroundForcesDB` clears, and on the next 1 h pass the native `GroundForcesProcessor` resumes stepping that body's fight — back on its native grid, which it never left (2.5).

## 2.4 The fixed-quantum proof — fast-forward == watch

**Claim.** A combined battle resolves **identically** whether watched at 5 s or fast-forwarded through in one big master step.

**Proof.**
1. `BattleTriggerProcessor` has RunFrequency 5 s. By the sub-pulse mechanism (2.2), it is invoked **exactly once per 5 s of game-time, always with `dt = 5`**, during *any* advance — 5 s or a year. (The scheduler lays a sub-step at every 5 s boundary and calls the processor with the span since its last run; the master fast-forward chunk does not change that span.)
2. The `BattleTheater` steps the ground fight **from inside** that trigger, with the trigger's `dt = 5`. So the ground fight is **also** stepped exactly once per 5 s of game-time, `dt = 5`, during any advance. Its quantum is **fixed at 5 s for the theater's whole life** — it never integrates a variable or 1 h step.
3. Theater **formation and dissolution are pure functions of game state** evaluated at 5 s boundaries (2.3 beats 1 & 4). So the *set* of 5 s ticks during which the theater is active is **identical** watched or fast-forwarded.
4. The native `GroundForcesProcessor` fight-steps are **suppressed** for the theater body while the theater is active (beat 2), so there is no interleaved 1 h coarse step and no double-step. Outside a theater, the ground plane runs its native 1 h quantum — also fixed, also fast-forward==watch by the same mechanism.
5. Damage still uses the snapshot-before-apply rule (§7), so within-tick order can't change the result.

Therefore every position/damage update in the combined battle occurs at the **same fixed 5 s quantum, in the same order**, whether the step was watched or skipped. Fast-forward == watch. ∎

## 2.5 The three rules to guard forever

1. **The theater step MUST use the space trigger's fixed 5 s `dt`.** Never pass a variable master delta into a group step (the design's iron rule, §7). If the ground fight ever receives `deltaSeconds` other than the fixed quantum, fast-forward ≠ watch.
2. **`TheaterGroundQuantum` MUST evenly divide `GroundForcesProcessor.RunFrequency`** (5 s | 3600 s → 720, exact). This is why a theater that forms and dissolves inside an hour leaves the native scheduler on its own grid with no drift or leftover partial step. If either cadence is ever retuned, keep the divisibility.
3. **A combined battle is NEVER routed through an instant "resolve the whole battle now" shortcut** (the pure `AutoResolve`). Both planes step through the *stepped* resolver at the fixed quantum, or the coupling boolean (shield-gen alive?) can't be read at the right instant and the Endor lock breaks.

## 2.6 The one subtlety — a battle that becomes combined mid-fight

If a ground battle is already running at its native 1 h cadence when a fleet arrives and forms a theater, the ground quantum **switches from 1 h to 5 s mid-battle**. Is that a determinism break? **No** — because theater formation is deterministic (2.4 step 3), the switch happens at the **same game-time** in a watched and a fast-forwarded playthrough, and the ground state at the switch instant is identical (both reached it via the same 1 h quanta). From the switch onward both use 5 s. So watch and fast-forward **switch at the same instant and stay in lockstep** — fast-forward == watch holds *across* the switch.

Note the honest limitation: a ground battle stepped at 1 h then switched to 5 s does **not** produce the same numbers as one stepped at 5 s from the start (coarse vs fine integration of the same fight). That is **accepted and deterministic**: the ground plane's native cadence is 1 h, and a battle legitimately changes character at the deterministic instant it becomes combined. Both watch and fast-forward produce the *same* switched result, which is all determinism requires.

---

## 3. What each joint hands to its slice

| Joint | Gates | The slice implements |
|---|---|---|
| **#1 Fire-allocation** (this memo §1) | **S6** (multi-party / FFA) | `AllocateFire` (residual-exact, target-weighted) replacing the `1.0/split` scale in `StepEngagementGroup`'s incoming build, behind the group-plane flag; the ground resolver's per-faction pool folds onto the same one-pool allocation (kills the double-count). Byte-identical flag-off (uniform weights → `1/count` → today's split). |
| **#2 Cadence** (this memo §2) | **S5** (combined theater / Endor) | `BattleTheater` (multi-plane marker) owning the theater body's 5 s ground fight-stepping from inside `CombatEngagement.Tick`; the native `GroundForcesProcessor` yields exactly `ApplyEngagementManeuvers` + `ResolveRegionCombat` for a theater-driven body; deterministic formation/dissolution. |

Both are additive and default-off at their slice; the existing ship + ground fixtures are the byte-identity tripwire, exactly as every other slice in §13.

## 4. Connections (Prime Directive)

- **Feeds IN:** `CombatEngagement.BuildFireMix`/`TotalDamage` (the group pool), the directed `targetsOf`/`attackersOf` engageable sets (`cs:641-661`), `LandedFraction` (the hittability weight term), `GroundForcesProcessor.ResolveRegionCombat`/`ApplyEngagementManeuvers` (the ground fight steps the theater force-calls), `BattleTriggerProcessor` (5 s) + `GroundForcesProcessor.RunFrequency` (1 h) + `MasterTimePulse.CombatReactionStep` (the cadence grid), `ManagerSubPulse.ProcessToNextInterupt` (the fixed-quantum `deltaSeconds` derivation).
- **Feeds OUT:** the conserved per-defender `incoming` mix (unchanged pipeline downstream: PD → shield-nature → armour-nature → dodge-bucket casualties); the theater's coupling boolean (shield-gen alive) read by the space plane next step.
- **Shares STATE:** the new `BattleTheater` marker (S5) binds a space engagement to a body's `GroundForcesDB` + a theater-driven flag on that blob (save-safe: `[JsonProperty]` + deep-copy line, the standing rule).
- **Triggers:** nothing new player-facing — no rendering, no new order type. Doctrine remains the entire control surface (§15).

## 5. Test coverage (this slice)

`Pulsar4X.Tests/Resolver2DJointsSpecTests.cs` — an **executable specification**: it holds a self-contained reference implementation of `AllocateFire` and the fixed-quantum stepping, and asserts the invariants so the algorithm is *pinned and proven* before S5/S6 wire it into production. It touches no production code path (it reads the `WeaponProfile` public type only), so it is byte-identical for every existing fixture. When S6/S5 implement these against `GroupPlane`/`BattleTheater`, they must reproduce this fixture's worked example.

- **Joint #1:** 3-way FFA total-dealt == total-owned (system-wide conservation); per-group Σ shares == pool exactly (residual, no group exceeds its pool); the double-count bug is caught (naive full-pool-per-target deals 2×, conserved deals 1×); target-weighting concentrates fire on the higher-weight target while still conserving; determinism under target reordering; a `WeaponProfile`-mix split conserves total DPS within tolerance and preserves Nature/Delivery.
- **Joint #2:** `720 × 5 s == 1 h` (the divisibility invariant); fast-forward == watch (N×5 s in one chunk equals N steps of 5 s for a deterministic accumulator, and the fixed quantum differs from a single variable 1 h step — proving why the fixed quantum matters); the theater-active tick set is identical for watch vs fast-forward.
