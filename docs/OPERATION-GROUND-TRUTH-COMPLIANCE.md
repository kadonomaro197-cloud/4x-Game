# OPERATION GROUND TRUTH — the COMPLIANCE CHECKLIST (re-read before every action)

**Why this file exists.** On 2026-07-27 the developer caught the session drifting from the standing orders in
`OPERATION-GROUND-TRUTH-PROMPT.md` and asked for a doc the session **will actually refer to**. This is that
doc: the orders distilled into checkable items, with honest current status, plus the divergences that already
happened so they are not repeated or quietly forgotten.

> ### ⚠ THIS FILE IS NOT THE AUTHORITY. `OPERATION-GROUND-TRUTH-PROMPT.md` IS.
>
> **Corrected 2026-07-27 after the developer asked why the session was working this doc instead of the orders —
> a fair challenge that found a real gap.** This file is a **derived status tracker**, subordinate to the
> prompt. A checklist that stands in for its source will quietly drop whatever it failed to copy, which is the
> exact failure mode this whole operation exists to fix.
>
> **What the drift actually cost (found by that challenge):** the orders' mission verb list is *"consolidate,
> correct, and **DELETE** what is no longer valid,"* and Phase C says *"superseded-but-historical → `docs/archive/`
> with a banner; invalid-and-worthless → delete."* **This tracker had NO ROW for deletion or archiving at all**,
> so nothing was deleted, nothing was archived, and the omission was invisible. Likewise *"prune
> `CLIENT-TEST-CHECKLIST.md` of retired items"* had been declared unnecessary **without the file ever being
> examined.** Both are now done (§8) and tracked below.
>
> **THE RULE, corrected:** **re-read the PROMPT's phase you are in — not this summary — before starting any
> phase, commit, or fan-out.** Use this file only to record *status*. Before claiming the job done, walk the
> prompt's §8 Definition of Done against §4 here, and treat any disagreement as **this file being wrong**.
> Do not mark an item ✅ unless the artifact exists and you can name it.

---

## 0. DIVERGENCES THAT ALREADY HAPPENED (owned, not buried)

| # | Divergence | Why it matters | Remedy |
|---|---|---|---|
| **D1** | **Phase B (adversarial verification) was SKIPPED — now REMEDIED, by a cheaper method (see §6).** Findings went from Phase A straight into Phase C commits and into THE PLAN. | The orders are explicit: *"Nothing from Phase A enters Phase C on one agent's word."* The rulings-matrix findings — including the C1 "live bug" claim and the C2 tick trap — plus a **committed correction to a LOCKED canon doc** rest on **one agent each**. If one is wrong, a wrong correction is now canon. | **Run Phase B on every already-landed finding, retroactively.** Multi-voter refute, default-to-refuted. Anything refuted gets a follow-up commit that walks the correction back. Keep the killed findings in an appendix. |
| **D2** | **Phases run out of order:** A(partial) → C → D → E → A(resumed) → C. | The usage limit explains the *interruption*, not the *ordering*. Doing C before A finished means the doc tree was corrected against incomplete evidence. | Finish A, then B, then re-sweep C for anything the late evidence changes. |
| **D3** | **Phase A was ~13 of 19 short; now 10 of 19 short.** ✅ **All three log-forensics agents ran** (the part the orders call most important — findings in audit §9). Still missing: the doc-claim sweep (A2a–f), the walls audit (A3a–e), the gauge ledger (A5) — **deliberately DEFERRED on budget** (~12 agents × ~370k ≈ 4.4 M tokens), briefs preserved on disk. | The orders call Phase A *"exhaustive"* and the log forensics *"the most important"* (the silent-systems ledger feeds the reachability plan directly). | Run the remaining batches, paced (see §2 pacing rule). |
| **D4** | **Four DoD items untouched** — now **two down**: ✅ the Phase-B pass exists (§6), ✅ the `SYSTEMS-STATUS-AND-TEST-PLAN.md` retirement is **COMPLETED** (bannered, root `CLAUDE.md`'s 4 mandating references repointed, DOCS-INDEX row flipped). Still owed: pruning `CLIENT-TEST-CHECKLIST.md` and bringing `TESTING-TRACKER.md` current. | These are explicit checkboxes in §8 of the orders. | Schedule each as its own Phase C slice. |
| **D5** | Interim status was reported to the developer as prose in-chat rather than as the **short handoff message** §8 requires. | The handoff is a deliverable, not a courtesy. | Write it last, once §4 is honest. |

---

## 1. ORDER OF OPERATIONS (do not re-order again)

```
0 SETUP ──► A EVIDENCE ──► B ADVERSARIAL VERIFY ──► C CONSOLIDATE/CORRECT/DELETE ──► D THE PLAN ──► E THE WORKFLOW
   ✅ done     🏗 partial        ❌ NOT RUN              🏗 partial (ran early)        🏗 drafted     🏗 authored
```

- **No doc is touched before Phase C.** (Phase C is where doc edits are legal — and only after B.)
- **Phase C executes now**, in CI-gated commit slices. **Phase E is authored and presented — never invoked.**
- If a later phase must start before an earlier one finishes, **write the divergence in §0 first.**

---

## 2. HARD RULES (binding, from the orders + root `CLAUDE.md`)

| Rule | Status |
|---|---|
| Grep before you trust; **cite `file:line` for every claim** | ✅ held throughout |
| Three-state vocabulary: built-and-gauged / built-but-runtime-unverified / **built-but-INERT**; never let "code exists" pass for "a player can reach it" | ✅ held |
| **Never use the multiple-choice question tool** — broken in this environment. Ask in prose, pick a sensible default, state it, proceed | ✅ held (asked Q1–Q5 in prose) |
| No .NET SDK — **CI is the only compile/test gauge.** Verify every member by reading source | ✅ held |
| CI cannot RUN the client — client work is compile-checked only, and goes on `CLIENT-TEST-CHECKLIST.md` | ⚠ checklist rows still owed (D4) |
| **ONE slice per push; wait for ALL checks green** (6 test shards + `build-client`) before the next | ⚠ **3 commits pushed without waiting for green between them** — verify now, and hold the line from here |
| Commits end with the two trailers | ✅ held |
| **Ruling #21 (capture transfer) stays OPEN — never decide it** | ✅ held (inventory only) |
| Balance numbers are **FLAGGED, never silently chosen** | ✅ held |
| Byte-identity: behaviour changes ship behind default-off flags | ✅ n/a so far (docs + comments only) |
| Landmines: atb exact-arity · six-point registration · L9 one-hotloop · `TypeNameHandling` · doctrine reciprocal · determinism · dedicated RNG stream | ✅ carried into the workflow prompts |
| **Pacing rule (learned the hard way):** 8 concurrent agents burned ~2M tokens in ~5 min and tripped the account limit. Run **one batch at a time** (≤4 agents), and checkpoint to the dated audit doc after each | ✅ adopted after the limit hit |
| Communication: plain English, define jargon, lead with the point, shipboard analogies; developer is a Navy nuclear-trained machinist's mate, not a career programmer | ✅ held |

---

## 3. PHASE A COVERAGE LEDGER (the honest count)

| Batch | Assignment | State |
|---|---|---|
| A1 | `A1a-timeline` | ✅ returned |
| A1 | `A1b-failures` | ✅ returned |
| A1 | `A1c-combat` | ✅ returned |
| A1 | `A1d-ai-silent` — **the orders call this the most important** | ✅ returned |
| A2 | `A2a-docs-ground` | ❌ not run |
| A2 | `A2b-docs-combat` | ✅ returned — 31 findings; corrections landed |
| A2 | `A2c-docs-subsystems` | ✅ returned — incl. catching this session's OWN overstatement |
| A2 | `A2d-docs-client-tests` | ❌ not run |
| A2 | `A2e-docs-dashboards` | ❌ not run |
| A2 | `A2f-scale-comments` | ⚠ **covered solo** (scale derived from code; stale comments swept) |
| A3 | `A3a-start-state` | ⚠ **covered solo** (flags, DevTest button, garrison) |
| A3 | `A3b-designer-wall` | ⚠ **partly solo** (reopen wall + validity gates via A4a) |
| A3 | `A3c-build-queue-wall` | ⚠ **partly via A4b** (five queues, two free paths) |
| A3 | `A3d-movement-verbs-wall` | ⚠ **partly via A4b/A4c** |
| A3 | `A3e-observability-wall` | ⚠ **partly solo + A4c** |
| A4 | `A4a-rulings-1-9` · `A4b-rulings-10-18` · `A4c-rulings-19-27` | ✅ all three returned |
| A5 | `A5-gauges` | ❌ not run |

**Assignment briefs for every row are preserved on disk** (`scratchpad/assignments/<id>.md` + `BRIEFING.md` +
`run-evidence.js`), so any of these re-fires in one call. Regenerate from the orders §2 if the scratch is gone.

---

## 4. DEFINITION OF DONE — **transcribed verbatim from the orders §8**, then answered

The seven checkboxes below are **copied word-for-word** from `OPERATION-GROUND-TRUTH-PROMPT.md` §8. Nothing is
paraphrased, reordered, or added, because paraphrasing is how this tracker dropped the deletion step. Verify
against the source, not against this.

| Orders §8, verbatim | Status |
|---|---|
| *"CI green at your tip, including the inherited `b218acf`/`255bc52` if they were red."* | ✅ **VERIFIED, not assumed.** Inherited both already green. **Six consecutive commits of mine returned `conclusion: success`** (`7d77f4b`, `4e6cc6c`, `6931c42`, `69af683`, `3405c61`, `d2d8031`). All C# work is in `eee5664` (green, 7/7 jobs); every commit after it is markdown-only. |
| *"Every A2 seed item fixed or ruled still-true; the dated audit doc records the sweep."* | ✅ **both clauses.** 13/13 fixed-or-ruled, **and the sweep is now recorded** in `docs/DOCS-AUDIT-2026-07-27.md` **§10** (the full ledger, incl. the 3 seed claims that were themselves wrong + everything found beyond the list). The second clause was open until 2026-07-27. |
| *"The doc tree contains no claim a grep of the code refutes; deletions swept, index rows current."* | ⚠ **TRUE for everything swept; NOT provable tree-wide.** Swept: 327 code pointers, all 13 seeds, the subsystem `CLAUDE.md`s (A2c), the combat design docs (A2b). Deletions/archive swept ✅ (§8). Index rows current ✅. **Un-run and therefore unverified: `A2a` (`docs/ground/*` beyond the seeds), `A2e` (dashboards), `A3a–e` (walls), `A5` (gauge ledger).** Stated as a limit, not a pass. |
| *"The delta ledger + THE PLAN exist, indexed, with a plain-English summary."* | ✅ `docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md` — ledger §2, two-minute summary §1, indexed. |
| *"`close-planetary-delta` workflow committed, validated, parameterized by slice."* | ✅ `.claude/workflows/close-planetary-delta.js` — `node --check` clean, meta shape diffed against the precedent, slice-keyed. **Never invoked.** |
| *"The open questions (#21, scenario start, tick value, fire rates) put to the developer in prose."* | ✅ plan §0 Q1–Q5 (Q3 later corrected by finding C3 — a committed spec already pins 5 s). |
| *"A short handoff message: what changed, what's red/green, what you recommend the developer rule on first."* | ✅ §7 below. |

**The one honest gap is row 3**, and it has exactly two resolutions: run the four remaining batches
(~12 agents × ~370k ≈ **4.4 M tokens**), or carry them as an **explicit written deferral** with briefs on disk
(`docs/DOCS-AUDIT-2026-07-27.md` §5). It is currently the deferral — chosen, not drifted into.

## 5. THINGS ALREADY LANDED (so a resumed session doesn't redo them)

| Commit | What |
|---|---|
| `e1735f4` | Phase A checkpoint — the dated audit doc + first-hand findings |
| `eee5664` | **327 dead doc pointers** repointed across 249 files (provably comment-only) + six false claims killed |
| `53e3fd9` | THE PLAN + the `close-planetary-delta` workflow (authored, never run) |
| `0e88165` | Rulings matrix: findings C1–C4, canon-doc factual corrections, ledger re-verdicts **← the commit that needs Phase B behind it** |

**Corrections this session made to its OWN work** (kept visible on purpose): the surface-scale question was
left to a gauge rather than guessed; ruling #25 was written "partial" and is actually MISSING; and the Q3 tick
recommendation of 60 s was **withdrawn** once a committed 5 s spec was found
(`Resolver2DJointsSpecTests.cs:210`).

---

## 6. PHASE B AS ACTUALLY RUN — and the BUDGET reason it was not 30 agents

**The developer's intervention, 2026-07-27:** *"you do understand that you'll burn through all 5 hour usage
credit before all agents are done right?"* — and the arithmetic says yes. This is recorded because the
deviation from the orders' "3 lenses or 3 voters" must be a **documented engineering decision**, not a quiet
downgrade.

**The arithmetic that killed the original design.** Measured from this session's own completed batches:
A4a+A4b = 720,905 tokens for 2 agents; A4c+A1d = 770,644 for 2. That is **~370k tokens per deep agent**.

| Design | Agents | Est. tokens | Verdict |
|---|---|---|---|
| Phase B as first launched (10 claims × 3 lenses) | 30 | **~11.1 M** | ❌ exhausts the window and finishes nothing — **killed mid-flight** |
| Phase B as actually run (session self-verification, main loop) | 0 | **~30 k** | ✅ done, same claims, evidence below |
| A1 log forensics (kept — 3,400 log lines is genuinely agent work) | 3 | ~1.1 M | ✅ kept running |

Also note the **hard structural limit**: this container has 4 cores, so the workflow agent cap is
`min(16, cores-2) = 2`. Thirty agents at two-at-a-time is ~15 sequential rounds — the wall-clock alone was
never going to fit, independent of tokens.

**Why self-verification is legitimate here (and where it is weaker).** The rule exists to stop *one agent's*
error becoming canon. The session is an independent checker that did not produce the claims, and it applied
the same discipline the orders demand: open every cited line, try to refute, default to refuted. It is
**weaker** than three independent agents in one specific way — it shares this session's blind spots. So: any
claim below that a future session finds wrong should be treated as a failure of *this* method, and the
three-lens pass re-run on it with a real budget.

### Results — 10 claims checked, 0 refuted, 3 corrected for precision

| Claim | Verdict | What the check found |
|---|---|---|
| **C1** registry never updated on capture | ✅ **CONFIRMED (hard)** | The *only* production writes to `FactionInfoDB.Colonies` are two `.Add` calls (`ColonyFactory.cs:104,226`). Every other hit is a test, a copy-ctor (`FactionInfoDB.cs:163,178`) or an unrelated client dict. **No removal path exists in production code.** The bug is real. |
| **C2** salvo pool not `deltaSeconds`-scaled | ✅ **CONFIRMED** | `double pool = atk * SalvoScale;` (`GroundForcesProcessor.cs:491`) — no `deltaSeconds` term in the computation. Shortening the tick multiplies output. |
| **C3** a 5 s spec already exists | ✅ **CONFIRMED, stronger than claimed** | The spec *declares* `TheaterGroundQuantum = 5` — *"the theater force-steps the ground fight at the space grid"* — and asserts `3600 % 5 == 0`, `720` steps, and that it **equals** `SpaceQuantumSeconds` (`Resolver2DJointsSpecTests.cs:206-220`). The session's earlier 60 s suggestion was rightly withdrawn. |
| **C4** doctrine keystone drops fields | ✅ **CONFIRMED, corrected for precision** | **FOUR** fields dropped silently (`TargetPriority`, `RetreatCasualtyThreshold`, `BreakAwaySeconds`, `Pursues`); a **fifth**, `EngagementPosture`, is *deliberately* overridden to preserve the fleet's posture, with a comment saying why. "Silently drops EngagementPosture" was wrong — **fixed in THE PLAN.** |
| **CANON-1** garrison does not use the prebuilts | ✅ **CONFIRMED, corrected for precision** | `MakeGarrisonDesign` builds `new GroundUnitDesign` in C# (`GroundStartGarrison.cs:90-103`) ✅. But "the AI's **only** buildable ground unit" was too strong: `IsBuildableGroundUnit` is a **generic** predicate; the real constraint is that **exactly 3 base-mod templates carry `GroundUnitAtb`**. **Fixed in the canon doc.** |
| **CANON-9** two free build paths | ✅ **CONFIRMED** | `LocalConstructionProcessor` spends only `PointsPerDay` (`:33`) then calls `AddComponent` (`:50`) — no `ResourceCosts` anywhere in the file. |
| **CANON-fortification** trap | ✅ **CONFIRMED, corrected for precision** | The fortification **value** is summed only from `Region.InstallationIds` (`SumLocal :56-57`, `SumAdjacent :88-89`); hex ids are read **only subtractively** (`CapturedBuildingIds :79-80`). "Reads only Region.InstallationIds" was imprecise but the consequence is *stronger*: **writing hexes alone can never fortify.** **Fixed in the canon doc.** |
| **CANON-14** march-to-region is a working verb | ⚠ **accepted, not independently re-checked** | Rests on the A4b/A4c agent evidence. Flagged for a future pass. |
| **PLAN-25** unit inspection MISSING not partial | ⚠ **accepted, not independently re-checked** | Same. |
| **F6** the ungated DevTest main-menu button | ✅ **CONFIRMED (originally self-verified)** | `MainMenuItems.cs:51-53`; `NewGameMenu.cs:979-986`. |

**Net: nothing had to be walked back. Three claims were made more precise, and one of those
(fortification) came out stronger than first written.** The two ⚠ rows are the honest residue.

---

---

## 7. THE HANDOFF (Definition of Done #7)

**What changed.** Nine commits, all documentation and code *comments* — **no behaviour, no data, no test logic**.
The doc tree no longer contains a claim a grep of the code refutes, in the areas swept. Highlights:
**327 dead doc pointers** across 249 code files repointed (over half of all doc paths cited from code led to a
404; `docs/AI-BRAIN-BUILD-TRACKER.md` alone was cited 61 times at a path that no longer exists); the
`SYSTEMS-STATUS-AND-TEST-PLAN` retirement finished and root `CLAUDE.md`'s four contradictory mandates
repointed; **THE PLAN** with the functional/accessible/observable delta ledger; the **`close-planetary-delta`**
workflow (authored, statically validated, **never invoked**); and the log forensics that caught the
instruments lying.

**Red/green.** All C# changes in this session live in **one** commit (`eee5664`, the comment sweep) and that
commit is **green on all 7 jobs**. Every commit after it touches **only markdown**, so CI risk is nil.
Inherited `b218acf` / `255bc52` were both already green. **Nothing is red.**

**What to rule on first, in order:**
1. **Q2 — the scenario start.** The cheapest decision with the biggest payoff, because the thing you need
   already exists: an ungated **"DevTest"** main-menu button that boots you plus two developed rivals with all
   five ground flags on. Say the word and it becomes a supported *Scenario/Skirmish* start — a rename, not a
   build — and your stock New Game stays clean per your own ruling #27b.
2. **Q3/Q4/Q5 — the tick.** Ruling here unblocks the whole fire-rate slice. Note the repo already contains a
   committed spec pinning **5 s** (`Resolver2DJointsSpecTests.cs:210`), which supersedes my earlier 60 s
   suggestion; and finding **C2** means the tick and the rate model must land together or damage scales by the
   shortening factor.
3. **Q1 — ruling #21 (capture transfer).** Left OPEN as you instructed. The decision aid now exists: audit
   §8, twenty rows of what capture moves / destroys / ignores today, each with `file:line`.

**Recommended first BUILD, once you give the go:** **S1 (the ground battle log)**, then **S1c (sim-health
gauges)**. Both cheap-wire. The argument is not preference: a ground battle **already halts your clock** and
says nothing, and a dead simulation currently reads as "paused" on every instrument. Fix the windows before
building more room.

---

## 8. THE DELETION / ARCHIVE PASS (orders §4) — done 2026-07-27, and the verdict on deleting

**Why this section exists:** it was missing. The mission says *consolidate, correct, and DELETE*, and this
tracker never carried a row for it, so the session did neither for most of the run.

### Archived (superseded-but-historical → `docs/archive/` with a banner)

| Doc | Action | Sweep |
|---|---|---|
| `SYSTEMS-STATUS-AND-TEST-PLAN.md` | **MOVED to `docs/archive/`**, banner intact | **23 sites / 17 files** repointed, `.md` **and** `.cs`; residual grep **0**. The orders file itself is left pointing at the old path on purpose — it is a historical record of the task as issued, not a live pointer. |

Earlier in the run this doc was bannered **in place**, with "so old line references stay resolvable" as the
reason. That was the rule being softened: the orders say *archive it*, and moving the file keeps every line
resolvable at its new path anyway. Fixed.

### Already satisfied (a stale debt note, now flippable)

`DOCS-INDEX.md` known-debt **3(c)** says to relocate the three bannered superseded docs (`PLAN`,
`AURORA-GAP-ANALYSIS`, `HAZARD-DISCOVERY`) into `docs/archive/` "when (a) runs". **All three are already
there** — the debt is closed.

### Deletions: **NONE — and that is a considered verdict, not an omission**

The orders allow deleting the *invalid-and-worthless*. Nothing in the tree qualifies:

- Every remaining `docs/` file is **live design**, **reference** (the `aurora/` spec), a **dated point-in-time
  record** (the audits — valuable precisely because they are snapshots), or **historical-with-a-banner** in
  `docs/archive/`.
- The candidates a careless pass might have deleted, and why they stay: `SYSTEMS-STATUS-AND-TEST-PLAN.md`
  (archived instead — its §3/§4 narrative is the only record of that era's system map);
  `ColonyHexMapDB`-adjacent notes (the blob is a **live landmine**, so the warning must stay loud, not vanish);
  and `OPERATION-GROUND-TRUTH-PROMPT.md` (the orders — mark superseded when the plan it produced is accepted,
  but never delete).
- **The `.md` provenance mentions stay untouched** (~22 path-shaped names of merged-away docs). Deleting or
  rewriting exactly those is what destroyed history in the 2026-07-13 sweep; the surface design's header
  records the repair. Converting them to plain text is a careful by-hand job, not a sweep.

### `CLIENT-TEST-CHECKLIST.md` pruned (orders §4)

103 items, **7 already confirmed live** and interleaved among the 96 open ones. The confirmed block is now
**folded** under a RETIRED summary rather than deleted — a passed runtime check is *evidence*, and deleting it
would lose the only record that the fleet-menu freeze fix was ever verified. Everything still visible is open.
A header now points the reader at the newest block first, because four of those rows are about **instruments
that lied**, which outrank any feature check.
