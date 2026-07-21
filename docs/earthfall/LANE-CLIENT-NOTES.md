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

---

## C2.1 — Honest colony detection ring + held-vs-fresh [DETECT] split + age-out engine gauges + horizon memo (2026-07-18)

**Files changed/created:**
- `Pulsar4X/Pulsar4X.Client/Rendering/SystemMapRendering.cs` — **(a) the honest colony detection ring** (findings/A2
  ranked fix #2). `UpdateAllRangeRings` colony loop: the green "detection range" ring around a colony was sized against
  ONE arbitrary reference ship (`refTarget`), so a LOUDER genuinely-detected contact rendered OUTSIDE it ("saw them
  before sensor range"). Now it draws an **honest min/max BAND** sized against the ACTUAL foreign ships present:
  **outer** ring = `max` over foreign ships of `SensorTools.DetectionRangeAgainst(colony, fs)` (the loudest contact →
  the max reach → **no detected contact can fall outside it**); **inner** ring = `min` (the quietest), drawn only when
  meaningfully tighter than outer (a single loudness class collapses the band to one honest ring). Both are already
  clamped to the colony's hard 200 Gm horizon inside `DetectionRange_m`, so the band never over-promises. New
  `foreignShips` list captured in the same try; the ring-rebuild **fingerprint** now includes the foreign-ship set +
  their loudness so the band rebuilds when an enemy appears/leaves or flips EMCON. No-foreign-ship fallback = the old
  self-referential single ring (unchanged). Ring keys `_sensor_max`/`_sensor_min` (band) / `_sensor` (fallback) — all
  swept by the generic `_allRangeKeys` cleanup.
- `Pulsar4X/Pulsar4X.Client/SessionLog.cs` — **(b) the held-vs-fresh `[DETECT]` split** behind a default-off static
  `SessionLog.SplitHeldVsFresh`. When ON, the `[DETECT]` line splits "holds N contact(s)" into "(F fresh / S
  held-stale)" — fresh = a track re-snapshotted at the last scan (`SensorContact.PositionSourceLabel == "LAGGED"`),
  stale = coasting on a FROZEN last-known fix. Surfaces the held-vs-seen conflation findings/A2 named (the hostile
  engage gate counts a stale contact as detected) as a READOUT only — **the gate's gameplay is untouched**. Default OFF
  → the `[DETECT]` line is the exact original text.
- `Pulsar4X/Pulsar4X.Tests/EfClientSensorAgeOutTests.cs` — **(c) the two engine gauges the A2 dossier named as
  missing.** `TrackLost_FlipsToMemory_NotAgedOutYet`: detect a bogey at point-blank, MOVE it 500 Gm out of every
  friendly sensor's reach (past the 200 Gm horizon), scan again just after → the contact flips from LAGGED to FROZEN
  (`PositionIsMemory == true`) but is NOT yet removed. `StaleContact_AgesOut_AfterContactStaleSeconds`: the same
  setup, then a scan UNDER `SensorScan.ContactStaleSeconds` (still held) and a scan OVER it (removed) — proving the
  age-out threshold governs. Drives `SensorScan.ProcessEntity(entity, atDateTime)` at chosen game-times via
  `InternalsVisibleTo`. **New unique fixture filename (lane-distinct `EfClient…` prefix) → lands in the CI `rest`
  shard, no collision.**
- `docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md` — **(d) the 200 Gm design call** (NEW doc, in CLIENT fence). Three
  options — KEEP+honest-readouts [default, shipped] / signature-curve / EMCON-gate — each with its file:line dial
  (`electronics.json:175` the horizon literal; `SensorTools.cs GetDetectedEntites`/`DetectionRange_m` the cap) and
  gameplay consequence. **No engine value changed.**
- `docs/CLIENT-TEST-CHECKLIST.md` — new "🛰 OPERATION EARTHFALL — C2.1" section (honest ring band never lets a
  detected enemy render outside it; band tracks EMCON; never over-promises past 200 Gm; the optional held-vs-fresh
  split).

**Byte-identity claim: (a) provably inert absent new data; (b) default-off flag.**
- **(a) the ring** is a client-only render change with NO CI test observing it (CI compiles the client, can't run it),
  so every existing green test is unaffected by construction. **The band only appears when a FOREIGN ship is present**
  (the "new data"); absent any foreign ship the fallback is the **byte-identical pre-C2.1 (2026-06-28) single ring** —
  `DetectionRangeAgainst(colony, refTarget)` where `refTarget = firstForeign ?? firstOwnShip`, dropping to
  `SensorReachRange_m(colony)` ONLY when there are zero ships at all (exactly the prior fallback ladder). With the
  common single-enemy case the band collapses to one ring sized against that actual contact. Reads only CI-covered
  engine accessors (`SensorTools.DetectionRangeAgainst`/`SensorReachRange_m`/`CurrentActivityMultiplier`).
  **(REPAIR 2026-07-18: the first cut of the fallback used `SensorReachRange_m(colony)` unconditionally, which
  REGRESSED the peacetime ring — no foreign but own ships present is the normal state, and that collapsed Earth's
  ship-sized bubble to the self-referential tiny ring the 2026-06-28 fix had eliminated, refuting claim (a). Fixed to
  the ladder above, matching the comment at the `foreignShips` capture and the prior behaviour.)**
- **(b) the `[DETECT]` split** is behind `SessionLog.SplitHeldVsFresh`, default false; the `else` branch is the exact
  original `[DETECT]` line, so the log is byte-identical when off. Client-only; no CI test observes it.
- **(c)** adds only NEW test files (new fixture name) — no existing test/behaviour touched.
- **(d)** is a new doc — inert.

**FLAGGED gameplay numbers: NONE.** No new gameplay/balance number. The two ring alphas (30/55) and the `0.98`
band-collapse threshold are cosmetic render constants, not balance values (consistent with the existing ring alphas
45/70 etc.). The 200 Gm horizon literal is NOT changed by this slice (it's the developer's decision — see the memo).

**Developer decisions raised:**
1. **`SessionLog.SplitHeldVsFresh` default-off + the recency proxy.** The finding asked for "fresh = LastDetection
   within ~2 scan intervals," but the client is a SEPARATE assembly and cannot read `SensorInfoDB.LastDetection`
   (internal; no `InternalsVisibleTo("Pulsar4X.Client")`). The shipped proxy reads the client-reachable
   `SensorContact.PositionSourceLabel` (LAGGED = re-snapshotted at the last scan = fresh within one scan interval;
   FROZEN = coasting on last-known = held-stale) — which is actually what the fixed contact model already encodes.
   Kept behind a default-off static so the developer opts into the extra column, and gameplay is untouched.
2. **The 200 Gm homeworld horizon itself** — the standing campaign decision (`CAMPAIGN-PLAN.md §6`). The memo
   (`docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md`) lays out KEEP [default] / signature-curve / EMCON-gate with dials;
   C2.1 changed NO value and recommends KEEP now that the readouts are honest.

**Cross-lane request (no lane owns `GameEngine/Sensors/**`):** for a TRUER time-based held-vs-fresh recency ("within N
scan intervals"), a future engine slice should expose a public, computed, never-serialized accessor on
`SensorContact` (engine) reporting seconds-since-last-detection (e.g. `SecondsSinceDetection(DateTime now)` reading the
internal `SensorInfoDB.LastDetection`), mirroring the existing `SignalStrength_kW`/`PositionIsMemory`/
`PositionSourceLabel` pass-throughs. Then `SessionLog` can compute the true recency instead of the LAGGED/FROZEN proxy.
Not blocking — the proxy is faithful for the readout. Routed here because Sensors is outside every lane fence; PW or a
Sensors-touching slice can pick it up.

**Subsystem-CLAUDE.md row — APPLIED INLINE this commit (this CLIENT lane runs its C-slices SEQUENTIALLY, so there is
no parallel sibling to collide with on `Pulsar4X.Client/CLAUDE.md`; C1.1 already edited that file inline too).** The
row below was inserted into `Pulsar4X.Client/CLAUDE.md` under the "All-ranges always-on" section in this same commit,
per the standing "keep the subsystem CLAUDE.md current in the same commit" rule. Recorded here only for the P8.2 audit
trail (already landed — do NOT re-apply):**

> **Honest colony detection BAND (Earthfall C2.1, 2026-07-18).** The colony's green detection ring
> (`SystemMapRendering.UpdateAllRangeRings`) used to be sized against ONE arbitrary reference ship, so a LOUDER
> genuinely-detected contact rendered OUTSIDE it (the "saw them before sensor range" report, findings/A2). It now
> draws a min/max BAND sized against the ACTUAL foreign ships present: OUTER ring = max `DetectionRangeAgainst(colony,
> fs)` over foreign ships (nothing detected can fall outside it), INNER = min (drawn only when tighter). The
> ring-rebuild fingerprint now includes the foreign set + loudness so it rebuilds on enemy appear/leave/EMCON. No
> foreign ship → the byte-identical pre-C2.1 single ring sized vs a ship-like reference (`DetectionRangeAgainst(colony,
> firstForeign ?? firstOwnShip)`, falling to `SensorReachRange_m(colony)` only when there are zero ships at all).
> Companion: `SessionLog.SplitHeldVsFresh` (default off) splits the
> `[DETECT]` log into fresh (LAGGED) vs held-stale (FROZEN) contacts — readout only, the hostile gate is unchanged. No
> engine value changed; the 200 Gm horizon design call lives in `docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md`.

### Pending dashboard rows (for P8.2 to land)

**`docs/TESTING-TRACKER.md`** — append two engine-CI (Layer-1) rows:

| Row | What | Why | Method | What-right | Likely-failure | Mitigation | Unblocks |
|-----|------|-----|--------|-----------|----------------|------------|----------|
| EARTHFALL-C2.1-flip | Sensors→Memory lost-track flip fires | A2 named it untested; the fog-of-war contact must fade to last-known when its track is lost | `EfClientSensorAgeOutTests.TrackLost_FlipsToMemory_NotAgedOutYet` (drive SensorScan with a target moved 500 Gm out of range) | A lost track flips LAGGED→FROZEN (`PositionIsMemory`) but stays held (not yet stale) | The else-branch flip never fires (contact stays LAGGED / stays LIVE) | CI gauge is this test | The honest fog-of-war contact model (blips fade, don't glide) |
| EARTHFALL-C2.1-ageout | ContactStaleSeconds age-out actually removes a stale track | A2 named it untested; a track that left reach must be FORGOTTEN, not shown forever | `EfClientSensorAgeOutTests.StaleContact_AgesOut_AfterContactStaleSeconds` (scan under vs over `SensorScan.ContactStaleSeconds`) | Held just under the threshold; removed just over it | The removal never fires (contact persists forever) — the original "tracked across empty space" bug | CI gauge is this test | Fog-of-war truth; the honest colony ring's premise (detection is bounded + ages) |

**`docs/SYSTEM-CONNECTION-MAP.md`** — no new system-to-system edge (the detection→contact-blip→ring edge already
exists; C2.1 makes an existing edge's READOUT honest and adds age-out coverage).

**`docs/DOCS-INDEX.md`** — **NEW doc row:** `docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md` — *purpose:* the developer
design call on the 200 Gm homeworld sensor horizon (keep / signature-curve / EMCON-gate, with dials); *status:*
current / decision-memo (no value changed). Also: `CLIENT-TEST-CHECKLIST.md` gained a C2.1 section (status unchanged).

---

## C3.1 — the Battalions tab (Force Management window), 2026-07-18

**What shipped (CLIENT fence only — `Pulsar4X.Client/**` + `Pulsar4X.Tests/` new fixture):**
- `FleetWindow.cs` retitled to **"Force Management"** (class name KEPT — R1 ledger: renaming orphans
  `LoadedWindows`/save refs). Content split across a top-level **Fleets / Battalions** tab bar. The Fleets tab holds
  the ENTIRE existing fleet manager verbatim (byte-identical); the new **Battalions** tab is the cross-body ground
  formation manager (`DisplayBattalions` + `DrawBattalionOrders`/`DrawBattalionOrderQueue`/`DrawBattalionStance`/
  `DrawBattalionRoe`/`MarchBattalion`/`JumpToPlanetView`). All engine mutation goes through the CI-tested
  `GroundForces` / `GroundFormationDoctrine` order APIs — the same ones `PlanetViewWindow.DrawFormationPanel` uses.
- `ToolBarWindow.cs` — the toolbar button's tooltip retitled `"Fleet Management"` → `"Force Management (fleets + battalions)"` to match the window (trivial, in-fence).
- `Pulsar4X.Client/CLAUDE.md` + `docs/CLIENT-TEST-CHECKLIST.md` — the FleetWindow inventory row / new "Battalions tab" section, and the C3.1 runtime-check section (both in-fence).
- New CI fixture `Pulsar4X.Tests/EfC3BattalionRegistryTests.cs` (lands in the `rest` shard) pins the cross-body
  registry contract the tab draws: two formations on two bodies both collected + aggregated (strength/health/reach),
  enemy formation excluded.

**Byte-identity claim: (a) client-only, provably inert absent player action.** No engine code or data changed. The
Fleets tab is the pre-existing layout moved verbatim into a tab item. The Battalions tab is read-only until a player
clicks a button, and every mutation is an existing CI-tested engine call (march/queue/stance/ROE) behind an explicit
click — identical to what the planet view already does. No CI test observes the client; the new fixture only asserts
existing engine helpers. So every green test is unaffected.

**FLAGGED gameplay numbers: NONE.** The tab introduces no new gameplay/balance value. UI literals (column widths,
combo widths, the "+ Hold 6h" duration = the same `6*3600` the planet view queue uses) are cosmetic/echoed, not new
balance numbers.

**Developer decisions raised:**
1. **Window name (CAMPAIGN-PLAN §6 / R1 candidate list).** Shipped default is **"Force Management"** (the campaign's
   suggested default). The R1 candidates if you want a different one: *Forces · Force Command · Order of Battle ·
   Command · Fleets & Formations · Military Command · Task Forces.* Change only the `Window.Begin("Force Management",
   …)` title string in `FleetWindow.Display()` — keep the class name `FleetWindow`.
2. **Possible minor fleet-layout offset inside the tab.** The Fleets tab reuses the fleet layout's absolute
   `SetCursorPosY(27f)` offsets, which were tuned for the no-tab window. The 27px ≈ the tab-bar height, so it should
   look right, but if the ships/orders columns look shifted vs the fleet list, the fix is a one-line nudge of that
   constant. (Cosmetic only; no functional effect. Flagged on the CLIENT-TEST-CHECKLIST too.)

**Cross-lane requests (for PW, post-merge — CORE owns the resolver wiring / GROUND owns the engine helper):**
1. **Battalion RENAME button** — deferred to PW as planned (CAMPAIGN-PLAN §4 C3 line). It needs GROUND G2's
   `GroundForces.RenameFormation(formation, name)` helper (a `GroundFormation.Name` setter — the data object can't use
   the entity-only `RenameWindow`, R1 gap 2). Once that helper is merged, add a rename control to the selected
   battalion's order surface in `FleetWindow.DrawBattalionOrders` (mirror the stance/ROE inline-edit idiom), OR to the
   planet view's formation panel. Until then the tab intentionally has no rename.
2. **Optional engine `AllFormationsFor(game, factionId)` helper (GROUND follow-up, non-blocking).** The client sums
   per-body `FormationsFor` itself today (works, CI-pinned by `EfC3BattalionRegistryTests`). If GROUND later adds a
   CI-testable `GroundFormationTools.AllFormationsFor` cross-body helper, `DisplayBattalions` can swap its enumeration
   loop for it. Not required.

### Pending dashboard rows (for P8.2 to land)

**`docs/TESTING-TRACKER.md`** — append one engine-CI (Layer-1) row:

| Row | What | Why | Method | What-right | Likely-failure | Mitigation | Unblocks |
|-----|------|-----|--------|-----------|----------------|------------|----------|
| EARTHFALL-C3.1-registry | The cross-body battalion registry the Force Management Battalions tab draws | The client sums per-body `FormationsFor` across worlds (no engine cross-body helper); this pins the composition + aggregation + player-only filter the tab relies on | `EfC3BattalionRegistryTests.BattalionRegistry_CollectsPlayerFormationsAcrossBodies_AndAggregates` (2 formations on 2 bodies + 1 enemy formation) | Both player formations collected across 2 bodies; strength/health/reach aggregates exact; enemy excluded | An engine helper (`FormationsFor`/`FormationStrength`/…) drifts, or a body's roster isn't enumerated | CI gauge is this test | The Battalions tab (runtime gauge = the developer, CLIENT-TEST-CHECKLIST C3.1) |

**`docs/SYSTEM-CONNECTION-MAP.md`** — one new READOUT edge (no new engine coupling): **FleetWindow (client) →
GroundForcesDB / GroundFormationTools / GroundFormationDoctrine (engine)** — the Force Management Battalions tab reads
per-body ground formations across all worlds and issues formation orders (march/queue/stance/ROE), plus **FleetWindow
→ PlanetViewWindow** (jump-to-world via `SystemState.GetEntityById` + `GetInstance`). These reuse existing engine
order paths; the edge is a UI consumer of already-connected systems, not a new system-to-system dependency.

**`docs/DOCS-INDEX.md`** — no NEW doc. `Pulsar4X.Client/CLAUDE.md` gained a "Battalions tab" section + an updated
FleetWindow inventory row; `docs/CLIENT-TEST-CHECKLIST.md` gained a C3.1 section (both status: current).

---

## C4.1 — Hex-highlight range overlay + Entity Assembler rows (2026-07-19)

**What shipped (CLIENT fence only — `Pulsar4X.Client/**` + `docs/CLIENT-TEST-CHECKLIST.md`):**
- `PlanetViewWindow.cs` — **range overlay** on the globe surface view. `DrawGlobalHexWindow` gained a highlight pass
  (inserted right after the terrain loop `~:344`, before the seam lines / unit tokens): for the player's SELECTED unit
  group it tints reachable hexes — **red** = weapon reach (`GroundUnit.Range` hexes), **green** = radar reach (the
  unit's best `GroundSensorAtb.Range_km` ÷ `GroundRangeTools.HexPitchKm(region)`, read through
  `GroundUnitEntity.TryGetBacking` → `ComponentInstancesDB.TryGetComponentsByAttribute<GroundSensorAtb>`, exactly the
  path `GroundSensors.RevealFromUnits` uses). Three new private helpers: `RadarReachHexesFor` (the km→hex radar read,
  its own try/catch, returns 0 for a unit with no backing/radar), `OddRToAxial` + `GlobalHexDistance` (convert the drawn
  odd-r offset cylinder coords to axial before `Colonies.HexCoordinate.DistanceTo` and pick the short way across the
  seam via the existing `WrapDelta` — so the tinted disk is a proper hex shape, not a sheared rhombus). Toggle reuses
  `GlobalUIState.ShowAllRangeRings` (the same switch as the space map's rings). Fog-honest: own-faction selection only.
  A one-line legend added to the selected-unit caption.
- `ShipDesignWindow.cs` — **Entity Assembler readout rows.** `DisplayGroundStats` added a **Training** row
  (`TrainingMultiplier`, the cadre veterancy — computed since ⚙6.2 but never shown) and ALWAYS-ON **Power (draw /
  supply)** (`EnergyDemand_W`/`ReactorSupply_W`, red UNDER on shortfall) + **Ammo Capacity** (`AmmoCapacity_kg`) rows;
  the power/ammo gates were red "Problems" text only on violation before, so the margin is now a standing gauge.
- `Pulsar4X.Client/CLAUDE.md` — a "RANGE OVERLAY on the globe (C4.1)" subsection + a C4.1 note appended to the
  `ShipDesignWindow` inventory row (both in-fence).
- `docs/CLIENT-TEST-CHECKLIST.md` — a new "OPERATION EARTHFALL — C4.1" section (7 runtime checks) (in-fence).

**No new test fixture.** The test project references only `GameEngine` (not the client), so client-private rendering
helpers have no NUnit harness — the gauge is the CLIENT-TEST-CHECKLIST (the developer's local run). The engine reads the
overlay consumes (`GroundSensorAtb.Range_km`, `GroundRangeTools.HexPitchKm`, `GroundUnitEntity.TryGetBacking`,
`GroundUnitAssembly.Compute`) are already CI-covered by `GroundSensorsTests` / `GroundRangeTools` / `GroundUnitAssemblyTests`.

**Byte-identity claim: (a/b) client-only, provably inert absent player action + no engine value changed.** No engine code
or data was touched. The overlay is a read-only draw that mutates NO game state (`RadarReachHexesFor` only READS the
component store) and only appears when the player has a selection AND `ShowAllRangeRings` is on (its existing default) AND
the units are the player's own. The three assembler rows only DISPLAY values `GroundUnitAssembly.Compute` already
produced — the buildable/not `Problems` verdict is unchanged. CI only compiles the client (never runs it), so no green
test observes this change; every existing test is unaffected.

**FLAGGED gameplay numbers: NONE.** No new gameplay/balance value. The overlay colours + alphas
(red 0.90/0.22/0.22/0.20, green 0.20/0.80/0.35/0.13) are cosmetic UI literals, not balance numbers — no
`// FLAGGED balance value` comment warranted (matching the deposit-gem / selection-ring colour literals already in the
file). Weapon reach (`GroundUnit.Range`) and radar range (`GroundSensorAtb.Range_km`) are existing engine data.

**Developer decisions raised: NONE new.** (The C4 slice had no queued §6 decision.) Note for the developer: the overlay
shares the ONE "show all ranges" toggle with the space map (`GlobalUIState.ShowAllRangeRings`, DevTools › Detection /
Fog of War) — if a separate planet-only toggle is ever wanted, it's a one-field add.

**Cross-lane requests: NONE.** R1 flagged an optional `GroundSensors.RadarReachHexes(body, unit)` CI-testable helper as a
GROUND follow-up; it is **not merged** and **not blocking** — the client computes `Range_km ÷ HexPitchKm` inline (the same
formula). If GROUND later adds it, `RadarReachHexesFor` can swap its body for the one call. Part (b) confirmed the engine
getters already exist (all four fields public on `GroundUnitAssemblyResult`), so **no request to GROUND G4c** was needed.

### Pending dashboard rows (for P8.2 to land)

**`docs/TESTING-TRACKER.md`** — no new engine-CI row (client-only; no new fixture). Add one Layer-3 (local-runtime) row:

| Row | What | Why | Method | What-right | Likely-failure | Mitigation | Unblocks |
|-----|------|-----|--------|-----------|----------------|------------|----------|
| EARTHFALL-C4.1-overlay | The planet range overlay (weapon red / radar green) + the Training/Power/Ammo assembler rows | The ground echo of the space range rings + surfacing computed-but-invisible assembler numbers — runtime-only (CI can't run the client) | CLIENT-TEST-CHECKLIST "OPERATION EARTHFALL — C4.1" (select a unit → red/green disk; radar-only-with-radar; artillery bigger; toggle+fog; assembler rows) | Reach disk tints correctly + wraps the seam; green only for radar units; rows visible + red UNDER on power shortfall | Wrong hex-distance shape (offset-vs-axial), or the radar read throws, or the toggle doesn't gate | The overlay draws inside the window's `[RenderError]` try/catch + the radar read has its own guard; disk uses the odd-r→axial conversion | Reading a ground unit's tactical reach on the map |

**`docs/SYSTEM-CONNECTION-MAP.md`** — one new READOUT edge (no new engine coupling): **PlanetViewWindow (client) →
GroundSensorAtb / GroundRangeTools / GroundUnitEntity backing store (engine)** — the globe range overlay reads a selected
unit's weapon reach + radar reach to tint hexes; a UI consumer of already-connected systems, not a new dependency.

**`docs/DOCS-INDEX.md`** — no NEW doc. `Pulsar4X.Client/CLAUDE.md` gained a "RANGE OVERLAY on the globe (C4.1)" section +
a C4.1 note on the `ShipDesignWindow` row; `docs/CLIENT-TEST-CHECKLIST.md` gained a C4.1 section (both status: current).

## C5.1 — Embark / Land Troops order (the invasion control panel) (2026-07-19)

**What shipped.** The player half of the invasion **lift** step (MVP Stage 4). `LoadTroopsOrder`/`LandTroopsOrder` had
ZERO client surface (R2 ledger §6 — "the missing control panel"; the engine, the AI's `ConquerResolver`, and the tests
all issued them, but a player had no button). Added a new **`IssueOrderType.Troops`** ("Embark / land troops ...") to the
FleetWindow **Fleets ▸ Issue Orders** tab — the exact `MoveToSystemBodyOrder` Issue-Orders idiom (R1 §A1:
`GetFilteredEntities`/button → `CreateCommand` → `Game.OrderHandler.HandleOrder`), one verb over.

**Files changed:**
- `Pulsar4X/Pulsar4X.Client/Interface/Windows/FleetWindow.cs` — (1) `IssueOrderType.Troops` enum member (appended,
  runtime-only enum, not serialized); (2) a `HasAnyTroopBay(fleet)`-gated `Selectable("Embark / land troops ...")` in the
  Available-Orders list (mirrors the Geo/Grav-survey ability gating); (3) `case IssueOrderType.Troops → DisplayTroopOrders()`
  in the Issue-Orders switch; (4) the new draw block — `HasAnyTroopBay`, `DisplayTroopOrders` (per bay-ship collapsing
  header), `DrawShipBayCapacity` (used/free per class), `DrawEmbarkSection` (player units on the orbited body → Load
  button → `LoadTroopsOrder.CreateCommand(ship, body, unit.UnitId)`), `DrawLandSection` (loaded units + region-picker
  combo over `PlanetRegionsDB.Regions` + orbital-control gate → Land button → `LandTroopsOrder.CreateCommand(ship,
  targetBody, unit.UnitId, regionIndex)`). New fields `_landRegionChoice` (ship id → picked region) + `_troopStatus`.
- `Pulsar4X/Pulsar4X.Tests/EfC5TroopLiftOrderTests.cs` — NEW fixture (unique name), pins the button contract.
- `Pulsar4X/Pulsar4X.Client/CLAUDE.md` — `FleetWindow` inventory-row C5.1 note + a new "Embark / Land Troops" section.
- `docs/CLIENT-TEST-CHECKLIST.md` — new "OPERATION EARTHFALL — C5.1" section (the full embark-Earth-marines → land-on-Mars
  click-path).

**Order signatures verified in source (no compile available):** `LoadTroopsOrder.CreateCommand(Entity ship, Entity body,
int unitId)` (LoadTroopsOrder.cs:35) and `LandTroopsOrder.CreateCommand(Entity ship, Entity targetBody, int unitId, int
regionIndex)` (LandTroopsOrder.cs:37 — `RegionIndex` is carried on the order and lands into that region). Capacity helpers
(`GroundTransport.BayCapacity`/`UsedCapacity`/`FreeCapacity`/`CanLoad`/`CarryClassOf`/`CarrySizeOf`/`HasOrbitalControl`) all
public static, verified in `GroundTransport.cs`. `GroundForcesDB.Units` + `GroundTransportDB.LoadedUnits` public getters;
`GroundUnit.{UnitId,Name,UnitType,FactionOwnerID}` public; `PositionDB.Parent` = `Entity?` (the orbited body).
`ImGui.PushID(int)` overload confirmed in use (EntityWindow.cs:283, ResearchWindow.cs:164).

**Tests added — `EfC5TroopLiftOrderTests` (3 tests, `rest` shard):**
- `EmbarkButton_ReadoutAndOrder_LoadTheUnit` — asserts the bay-capacity-vs-unit-size readout the panel draws
  (`BayCapacity(Personnel) > 0`, `CarryClassOf(Infantry)=Personnel`, `CanLoad`/`FreeCapacity`), then the 3-arg
  `LoadTroopsOrder.CreateCommand` carries the body+unit ids and on `Execute` lifts the unit off the roster onto the ship.
- `LandButton_RegionPicker_CarriesTheChosenRegion` — asserts the 4-arg `LandTroopsOrder.CreateCommand` carries the PICKED
  region (a non-default index 2, pinning the region-picker wire), the `HasOrbitalControl` gate matches the button's
  disabled-state, and on `Execute` the unit lands in exactly the picked region.
- `VehicleUnit_OnATroopBayOnlyShip_LoadButtonIsDisabled` — asserts the per-class gating the readout draws: a
  troop-bay-only ship reports `BayCapacity(Vehicle)=0`, so `CanLoad(tank)=false` (greyed Load button) while
  `CanLoad(infantry)=true` (bay-only-carries-its-own-class).

**Byte-identity claim: (a/b) client-only + provably inert absent new data — no engine value changed.** No engine file or
data was touched. The new order type is a runtime-only enum member (appended, never serialized). The order surface is
gated on `HasAnyTroopBay` — a fleet with no troop-bay ship (every existing/default fleet) never even shows the option, and
the buttons only issue the two orders (already CI-tested + AI-issued) on an explicit player click. Both orders re-check
every precondition and are safe no-ops when stale. CI only compiles the client (never runs it), so no green test observes
the draw; every existing test is unaffected. `EfC5TroopLiftOrderTests` exercises only engine paths that already existed.

**FLAGGED gameplay numbers: NONE.** No new gameplay/balance value. The carry-size/carry-class/bay-capacity numbers the UI
displays are existing engine data (`GroundTransport.CarrySizeOf`/`CarryClassOf`, `GroundBayAtb.Capacity`). The status-text
colours (green 0.4/1/0.4, amber 1/0.6/0.3, header blue 0.8/0.9/1) are cosmetic UI literals, matching the file's existing
selector/status colour idioms — no `// FLAGGED balance value` warranted.

**Developer decisions raised: NONE new.** (The C5 slice had no queued §6 decision; C5b's infra Destroy/Capture decisions
live in findings/R4 and are PW's, not this slice's.)

**Cross-lane requests: NONE.** C5.1 (embark UI) has no cross-lane dependency and ships in-lane (CAMPAIGN-PLAN §3). C5b
(infra Destroy/Capture buttons) is deferred to PW — it needs GROUND G3's `DestroyInfrastructure`/`CaptureInfrastructure`
enum; NOT built here (per the slice brief). No new engine helper was required — the region picker reads the existing
`PlanetRegionsDB.Regions`; no `GroundTransport` change was needed.

### Pending dashboard rows (for P8.2 to land)

**`docs/TESTING-TRACKER.md`** — one new engine-CI row + one Layer-3 (local-runtime) row:

| Row | What | Why | Method | What-right | Likely-failure | Mitigation | Unblocks |
|-----|------|-----|--------|-----------|----------------|------------|----------|
| EARTHFALL-C5.1-order | `EfC5TroopLiftOrderTests` — pins the FleetWindow embark/land button contract (CreateCommand arities, per-class bay-capacity readout, region-picker RegionIndex wire) | CI can't run the client; this pins the exact engine surface the buttons draw against so a breaking engine change reds CI (the C3 registry-test role for the Battalions tab) | `dotnet test` (`rest` shard) — the 3 tests | 3 green: embark readout+order loads; land 4-arg carries picked region; troop-bay-only ship greys a tank's Load | An engine change to a CreateCommand overload or a capacity helper signature/behaviour | The fixture is the tripwire; overlap with GroundTransportTests/TroopOrderTests is intentional (different framing) | The player invasion lift step (embark → land) |
| EARTHFALL-C5.1-clickpath | The FleetWindow "Embark / land troops" order (embark garrison → fly → land region-picked) | The invasion lift step had no player button (R2 §6); runtime-only (CI can't run the client) | CLIENT-TEST-CHECKLIST "OPERATION EARTHFALL — C5.1" (order only shows with a bay ship; embark; fly; land gated on orbit; land into picked region; full Earth→Mars run) | The order appears with a bay ship; Load lifts a garrison unit; Land is orbit-gated and drops into the picked region | The order's ship enumeration / position-parent read wrong, or the region combo doesn't map, or a button no-ops silently | The draw is inside the Issue-Orders child; orders are defensive no-ops; `[troops]` SessionLog line gauges each click | MVP Stage 4 (you can invade a planet from orbit) |

**`docs/SYSTEM-CONNECTION-MAP.md`** — one new ORDER edge: **FleetWindow (client) → LoadTroopsOrder / LandTroopsOrder /
GroundTransport (engine)** — the embark/land buttons issue the two troop orders and read `GroundTransport` capacity +
`GroundForcesDB.Units` / `GroundTransportDB.LoadedUnits` / `PlanetRegionsDB.Regions`; the client consumer that closes the
"build army → LOAD → win orbit → LAND" chain's player-facing gap. Not a new engine coupling (the orders already existed
and were AI-issued); a UI front door onto them.

**`docs/DOCS-INDEX.md`** — no NEW doc. `Pulsar4X.Client/CLAUDE.md` gained an "Embark / Land Troops (C5.1)" section + a
C5.1 note on the `FleetWindow` inventory row; `docs/CLIENT-TEST-CHECKLIST.md` gained a C5.1 section (both status: current).
