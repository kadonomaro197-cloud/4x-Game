# Docs Audit — the Repeatable Process (playbook / SOP)

**What this is:** the standing procedure for auditing *every doc in the repo against what the code actually does*, deciding what earns its keep, and reorganizing the set — so this can be **re-run cold, months from now, by a different session or model**, without reinventing it. It is the *how-to*; the *findings* of any given run go in a dated `docs/DOCS-AUDIT-YYYY-MM-DD.md`.

> **Why this exists, in one line (plain English).** Docs rot like a logbook kept by three watches at once: the design writing stays honest, but the *"is it built yet?"* notes get copied into a dozen places and every copy drifts the same way — systems ship, the docs still say "to-do," and a cold reader rebuilds something that already works. This process is the periodic inspection that finds that drift and fixes it at the source.

**When to run it:**
- The docs and the code have visibly diverged (someone hits a "the doc says X, the code does Y").
- Before a milestone, a big merge series, or handing the repo to a new contributor.
- Periodically — treat it like a scheduled inspection, not a one-time cleanup. Drift is continuous; this is the reset.

---

## The one principle

**Verify against the code. Truth before structure. Build the gauge, because CI can't help.**

- **Verify against the code** — a doc's build-claims are checked against live source (`grep`/read), never trusted from another doc or from a prior audit's line numbers. HEAD moves; re-check at the moment you edit.
- **Truth before structure** — fix the false claims *first*, reorganize *second*. A corrected claim is right no matter where the file ends up; a moved-but-still-lying doc just relocates the problem.
- **Build the gauge** — markdown can't fail CI, so "it compiled" proves nothing. The gauge is a **residual `grep`** you run *between* steps: search for the exact dead symbols/claims you're purging; **zero survivors outside where they're legitimately correct = the step is done.** This is the read-back that replaces the compiler.

---

## Phase 1 — Discover: fan out the audit (parallel agents)

Run as a background **Workflow** (see the skeleton at the bottom). Four stages:

1. **Ground truth — one agent per code subsystem** (`GameEngine/<Sub>/` + `Client` + `Tests` + base-mod data; ~25 agents). Each reads the real `.cs`, reports what is **actually built and wired** vs. **dead code / stub / unwired** (signals of dead: no `[JsonProperty]`, no processor reads it, only referenced by other dead code, empty `Tick`/`ProcessEntity`), and — if the subsystem has a `CLAUDE.md` — whether that doc matches the code. **This is the reality yardstick everything else is measured against.**
2. **Doc audit — one agent per doc** (~84: all `docs/**` + root process docs; large docs get skimmed by structure/status-markers, not read line-by-line). Each is handed the ground-truth digest, then judges: purpose, self-declared status, 2–4 headline claims spot-checked (CONFIRMED / CONTRADICTED / PARTIAL / UNVERIFIABLE), redundancy with other docs, staleness, bloat, and a verdict (KEEP / KEEP-TRIM / STALE-FIX / CONSOLIDATE / ARCHIVE / DELETE) + where it should live.
3. **Synthesize — one agent per theme cluster** (combat, ground, designer, AI, economy, society, diplomacy, explore, detection, environment, aurora, …). Reconciles its cluster against ground truth: what to keep, merge, delete, fix.
4. **Reorg plan — one agent** rolls the clusters into a global plan: folder structure, ranked consolidations, delete/archive list, stale-claims-to-fix, and the single-owner fix for the status ledgers.

Write the output to a dated `docs/DOCS-AUDIT-YYYY-MM-DD.md` (findings + per-doc verdict table). That file is disposable/historical; **this playbook is the permanent part.**

---

## Phase 2 — Execute: four waves, in this order

Do **not** do it all in one blob. Each wave is its own reviewable commit-cluster, and truth precedes structure:

1. **Flip the lies (STALE-FIX).** Correct every "not built" claim that the code contradicts — design docs, subsystem `CLAUDE.md` facts, the `docs/aurora/` "Maps to Pulsar" status columns. No moves, pure truth. Highest value, lowest risk.
2. **Archive the superseded.** Move dead snapshots to `docs/archive/`, each with a `SUPERSEDED`/`HISTORICAL` banner at the top, and flip its `DOCS-INDEX` row in the same commit. (An un-bannered archive of contradicted docs misleads as badly as a live stale one.)
3. **One status owner.** Stop the drift at the source: give each status job exactly one owner and make everyone else *link, not copy* — `DOCS-INDEX` owns doc-currency, `TESTING-TRACKER` owns test/build status, a lean connection-map owns the Prime-Directive graph. Strip private status tables out of the giant files (`SESSION_STATE`, etc.). **Order matters: extract any unique content (e.g. the connection graph) BEFORE retiring the doc that holds it.**
4. **Merges + folders (last, riskiest).** Collapse near-duplicate clusters and folder the flat pile by subject (`docs/combat|ground|economy|society|ai|…`). Renames break cross-references, so this waits until the content underneath is already correct.

---

## The verification gauges (the between-steps discipline)

These are the checks that answer *"how do I know I did the right thing?"* at every step:

1. **Per-fact:** the claim cites a symbol you grep-confirmed *at edit time*, and you read enough surrounding context to be sure it means what you wrote — not just that the string exists. (A grep for `new Foo` can hit the class's own `Clone()`; "found it" ≠ "it's alive." Read the context.)
2. **Per-edit:** read the git diff before every commit. The `Edit` tool fails if the anchor text isn't matched, so a clean edit proves the *location*; the diff read proves the *replacement*.
3. **Per-wave read-back (the load-bearing gauge):** `grep` the whole repo for the exact dead symbols/claims the wave was supposed to purge. Examples that have caught real stragglers: `GameEngine/Data/basemod` (wrong mod path), `ColonyLifeSupportDB` cited as live, `SimpleDamage` shown as the live beam path, "no code yet"/"not built" near a system the ground-truth pass confirmed built. **Any survivor outside where it's legitimately correct means the wave isn't done.** This gate must be clean before the next wave.
4. **Anti-hallucination for the fan-out:** require each agent to return the *actual matched source line* it relied on (not just `path:line`), then re-grep a random sample of those claims yourself. One failed spot-check → re-verify that whole batch. **One agent owns one file** (an agent may own several, but two agents never edit the same file — that keeps parallel writes conflict-free).

---

## Two honesty guards (do not skip)

- **Preserve three states, never flatten to two.** The failure mode of a stale-fix pass is *over*-correcting "not built" into "done and working." Reality is often **built but flag-gated off, never runtime-tested** (e.g. the NPC AI, espionage, the Site Engine all ship behind default-off `Enable*`/`AutoSpawn*` flags). Write the exact state: *not built* ≠ *built-but-gated/unwired-to-player* ≠ *built-live-and-tested*.
- **Code-exists ≠ runs-correctly.** CI cannot launch the SDL/OpenGL client, so no doc edit may claim a system "works" — only that the code is present and wired. Where runtime is unproven, the doc says so. (Runtime checks live in `docs/CLIENT-TEST-CHECKLIST.md` / `docs/TESTING-TRACKER.md`, on the developer's machine.)

---

## Appendix — the Workflow skeleton (regenerable)

The Phase-1 fan-out is a background `Workflow`. Its shape, so it can be rebuilt from scratch (the script itself lives in session scratch and does not persist — this is the durable copy of its structure):

```
meta.phases = [ Ground-Truth, Doc-Audit, Synthesize, Reorg-Plan ]

Phase 1  parallel over every code subsystem  -> GT_SCHEMA
         { subsystem, builtSystems[], deadOrStub[], claudeMdVerdict, claudeMdCorrections[], oneLineTruth }
         -> build a compact `gtDigest` string (one line per subsystem)

Phase 2  parallel over every doc (hand each the gtDigest)  -> DOC_SCHEMA
         { path, theme, purpose, docKind, selfDeclaredStatus,
           claimCheck:[{claim, verdict, evidence}], redundancyWith[],
           stalenessSignals[], bloat, utilityVerdict, verdictReason, reorgSuggestion }

Phase 3  group docRecords by theme; parallel per theme (hand each its docs + gtDigest) -> CLUSTER_SCHEMA
         { theme, headline, perDoc:[{path,verdict,action}], consolidations[], deletions[], staleFixes[] }

Phase 4  one agent over all clusterVerdicts + the flagged stale CLAUDE.md list -> PLAN_SCHEMA
         { executiveSummary, proposedStructure[], topConsolidations[], deleteArchiveList[],
           staleClaimsToFix[], indexAdvice, keepAsIs[], risks[] }

return { gtRecords, docRecords, clusterVerdicts, finalPlan }  // then write docs/DOCS-AUDIT-YYYY-MM-DD.md
```

Scale is roughly **~125 agents** for the discovery fan-out (one run ≈ 1.5 hrs wall-clock, ~12M tokens) and **~25 more** for the execution waves. Agent count isn't the bottleneck — reviewing the diffs between waves is. Use structured-output schemas so agents return validated data, not prose. Effort: `medium` for ground-truth/doc-audit, `high` for synthesize/plan.

---

**Last run:** `docs/DOCS-AUDIT-2026-07-13.md` (2026-07-13). Headline: staleness + duplication, not bad docs — 0 delete, 3 archive, ~60 stale "not built" claims where the code had shipped; folder the flat pile, merge the AI/ground/combat clusters, give the status ledgers one owner each.
