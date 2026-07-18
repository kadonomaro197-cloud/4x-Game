# LANE CLIENT — pending dashboard rows, cross-lane requests, developer decisions

This is the CLIENT lane's conflict-free notes file (CAMPAIGN-PLAN.md §2.4). Lanes do NOT edit the shared
dashboards (`docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`) or a collision-prone
subsystem `CLAUDE.md` mid-flight; each pending row lands here and the integration phase (P8.2) applies them.
Sections are delimited by slice so parallel siblings can append without conflict.

---

## C1.1 — Contact-blip cull + render breadcrumbs + tighter watchdog (the 2050-04-22 freeze) (2026-07-18)

**Files changed/created (this slice, all inside the CLIENT fence `Pulsar4X.Client/**` + `docs/CLIENT-TEST-CHECKLIST.md`):**
- `Pulsar4X/Pulsar4X.Client/Rendering/Icons/SensorContactIcon.cs` — **(a) the freeze fix.** Added a finite/on-screen
  cull mirroring `OrbitEllipseIcon` (root Client CLAUDE.md gotcha #15) + `SimpleCircle`. New field `_offScreenSkip` +
  const `_maxBlipScreenCoordPx = 1_000_000` (matches `SimpleCircle.MaxSafeCoordPx`). `OnFrameUpdate` now reads the
  blip's world + on-screen position and, if either is non-finite (NaN/∞ `AbsolutePosition` from a lost track anchor)
  or the screen coordinate exceeds the bound (a `fogLag` drift parking it astronomically off-screen — the A1 on-ramp),
  sets `_offScreenSkip` and returns early (skips transform AND draw); the existing throw-guard `catch` now also sets
  the flag. `Draw` returns early on the flag. Recomputed every frame → reversible (a valid/on-screen contact redraws
  next frame). A normal on-screen finite blip takes the byte-identical base path.
- `Pulsar4X/Pulsar4X.Client/Rendering/SystemMapRendering.cs` — **(b) finer breadcrumb.** New `MapStage(name)` helper
  stamps `SessionLog.CurrentStage = "GalacticMap.Draw/<list>"` before each icon-list draw in `Draw()` (starfield /
  widgets / orbits / moves / ships / contacts / bodies / extras / labels / done). So a native hang inside the map draw
  names the exact list (e.g. `.../contacts`) instead of the coarse `'GalacticMap.Draw'` the outer SafeRender sets.
- `Pulsar4X/Pulsar4X.Client/SessionLog.cs` — **(c) tighter watchdog.** `HangThresholdMs` 5000 → 3500 (a freeze is
  named ~1.5 s sooner; the worst legit frame ever observed was ~2100 ms at startup, so no false positives). Diagnostic
  threshold, not a gameplay number.
- `Pulsar4X/Pulsar4X.Client/CLAUDE.md` — extended gotcha #15 with a note recording the contact-blip cull + the finer
  map-draw breadcrumb + the lowered watchdog threshold. *(In-fence: CLIENT owns `Pulsar4X.Client/**`. C1.1 is the only
  slice touching these gotcha lines; if a parallel C1.x sibling also edits gotcha #15, prefer this note and drop the
  duplicate.)*
- `docs/CLIENT-TEST-CHECKLIST.md` — new section "🧊 OPERATION EARTHFALL — C1.1" with the runtime checks (play past
  2050-04-22 with a fleet mid-transit, no freeze; degenerate contact doesn't draw; breadcrumb names the stage on any
  residual hang; normal play unchanged).

**Why no NUnit fixture (per the standing "add a test" rule):** all three changed types (`SensorContactIcon`,
`SystemMapRendering`, `SessionLog`) live in `Pulsar4X.Client`, which the Tests project does NOT reference (it references
only `GameEngine`). findings/A1-freeze.md is explicit: the freeze is a **native client render hang** and *no CI fixture
can reproduce it* — "client-side fix + local runtime check is the only path." So the gauge for this slice is the
runtime checklist above, not a test.

**Byte-identity claim: (b) provably inert absent the triggering condition.** No default-off flag; instead the change is
inert on every existing code path:
- The blip cull only alters behaviour for a **degenerate or far-off-screen** contact coordinate (non-finite world/screen
  position, or |screen| > 1e6 px). For a normal, finite, on-screen contact the guard is false and the render path is
  the unchanged base path — pixel-identical. The whole `SensorContactIcon` path only runs at all when fog of war is on
  (`CombatEngagement.RequireDetectionToEngage`) AND a foreign contact is held.
- The `MapStage` breadcrumb is a volatile string write with no effect on what is drawn.
- The watchdog threshold is diagnostic only (when the `[HANG]` line prints), affecting nothing rendered or simulated.
- All three types are CLIENT-only; **CI compiles the client but cannot run it**, so no CI test observes any of this —
  every existing green test is unaffected by construction.

**FLAGGED gameplay numbers: NONE.** The two numeric constants are technical render/diagnostic thresholds, consistent
with the unflagged precedent (`OrbitEllipseIcon._maxOrbitScreenRadiusPx`, `SimpleCircle.MaxSafeCoordPx`, the original
`HangThresholdMs`), not balance values:
- `SensorContactIcon._maxBlipScreenCoordPx = 1_000_000` px — off-screen cull bound (copies `SimpleCircle.MaxSafeCoordPx`).
- `SessionLog.HangThresholdMs = 3500` ms — freeze-detection latency threshold (was 5000).

**Developer decision raised (informational, no action needed):** the hang-watchdog threshold was lowered 5000 → 3500 ms.
Reversible one-liner in `SessionLog.cs`; flagged in case the developer sees spurious `[HANG]` lines on the lemon PC's
worst legit frame and wants it nudged back up.

**Cross-lane requests: NONE.** This slice is entirely inside the CLIENT fence.

### Pending dashboard rows (for P8.2 to land)

**`docs/TESTING-TRACKER.md`** — append a Layer-3 (local-runtime) row:

| Row | What | Why | Method | What-right | Likely-failure | Mitigation | Unblocks |
|-----|------|-----|--------|-----------|----------------|------------|----------|
| EARTHFALL-C1.1 | Contact-blip freeze fix — play past 2050-04-22 with a detected enemy fleet mid-transit, fog ON | The real session froze there; blips were the one unculled render path (findings/A1-freeze.md H1) | Local build → DevTest game → fog on → hostile fleet in flight → play across 2050-04-22 | Client keeps running smoothly, no freeze/white-window; degenerate blip silently skips a frame | A residual hang elsewhere in map draw | Finer `GalacticMap.Draw/<list>` breadcrumb + 3.5 s watchdog now name the wedged stage | The whole invasion play-through (can't test the campaign if the client freezes at transit) |

**`docs/SYSTEM-CONNECTION-MAP.md`** — no new system-to-system edge (this hardens an existing render path; the
detection→contact-blip edge already exists).

**`docs/DOCS-INDEX.md`** — no new doc; `CLIENT-TEST-CHECKLIST.md` gained a section (status unchanged: current/live-backlog).
