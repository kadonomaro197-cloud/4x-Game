# Operation Earthfall — Audit Resolution Plan

**As of 2026-07-22. Companion to `docs/earthfall/IMPLEMENTATION-AUDIT-2026-07-22.md`.**
The audit found **0 critical, 0 high, 4 medium, 15 low**. This plan turns those into ordered, one-commit-at-a-time slices an implementing session (Opus 4.8) can execute without further discovery. Every slice cites the exact file + the acceptance gauge.

> **STATUS (2026-07-22): IMPLEMENTED — awaiting CI.** All five slices are committed on `claude/faction-design-audit-bb3tqz` (M1 flags on the developer's confirmed "yes"). M4 remains a tracked follow-on (not built, by design). Two lows deliberately not done: the C5a orbit-view embark button (client UI — CI can't runtime-verify it; the manager-side buttons already exist) and copying the T-P4.x seven-field rows into TESTING-TRACKER (the data lives in LANE-CORE-NOTES). Slice→commit: M1 `c3eae06` · M2 `1d2339a` · M3 `caeb3ab` · low-code `2b54604` · doc/hygiene `417bd21`. CI (both jobs) is the compile gauge — the container has no .NET SDK, so every API was source-verified.

## Reading order for the implementer
1. This plan.
2. The audit report (§3 mediums, §4 lows) for the evidence behind each item.
3. `CONVENTIONS.md` + the subsystem `CLAUDE.md` for any file you touch.
4. Standing rule (repo `CLAUDE.md`): **one slice per commit; push; wait for CI green (both `test` and `build-client` jobs) before the next slice.** CI is the only correctness gauge — the container has no .NET SDK.

## One decision the developer must confirm (blocks Slice 1 only)
**M1 — activate the P3 legitimacy fixes in a live game?** The stale-morale-echo fix and the rebellion debounce ship behind two flags that default `false` and are only on inside tests, so a menu game runs the *old* behavior. **Recommendation: flip both ON** in the menu path (mirroring `EnableOrderEmission`), so your play-test exercises the fix you paid for. One-line revert if you want them off. If you'd rather leave them dormant, skip Slice 1 and record the decision in `EARTHFALL-DECISIONS.md`. *This is activation, not a balance dial — the FLAGGED balance defaults are untouched either way.*

---

## Slice 1 — M1: activate the P3 legitimacy fixes in menu games  (PRE-TEST CRITICAL)
**Why first:** without it your ground/faction play-test exercises pre-fix legitimacy/rebellion behavior; the P3 investment is dead in a live game.
**Change:** in `Pulsar4X.Client/Interface/Menus/NewGameMenu.cs`, in **both** blocks that set `NPCDecisionProcessor.EnableOrderEmission = true` (≈ lines 547 and 949), add:
```csharp
Pulsar4X.Colonies.LegitimacyProcessor.ReadCurrentMorale = true;
Pulsar4X.Colonies.LegitimacyProcessor.EnableRebellionDebounce = true;
```
(verify the exact namespace of `LegitimacyProcessor` before writing — it is `GameEngine/Colonies/LegitimacyProcessor.cs`).
**Byte-identity:** engine test suite is unaffected — factory-built test games never execute `NewGameMenu`; the existing `LegitimacyStaleEchoDebounceTests` / rebellion tests set the flags themselves.
**Gauge:** CI green (no engine test change). Client compiles (`build-client`). Runtime: on the dev PC, a menu game's colonies no longer revolt on a single bad morale sample; add a line to `docs/CLIENT-TEST-CHECKLIST.md` to check this live.
**Leaves flagged:** nothing new; this only flips existing flags.

## Slice 2 — M2: make ground posture hysteresis actually bind
**Why:** stance cooldowns (60–300 s) always expire before the hourly ground tick, so posture can flip every tick when odds sit near a threshold.
**Change (all in `GameEngine/GroundCombat/`):**
1. Add two FLAGGED constants to `GroundTactics.cs` (or `GroundTacticalBrain.cs`): `// FLAGGED balance value` `HysteresisBand = 0.15` and `MinHoldHours = 6` (developer tunes).
2. Add two save-safe fields to the ground formation record (`GroundForcesDB.cs` — the `GroundFormation`/formation struct): `[JsonProperty] public string LastStanceFamily` and `[JsonProperty] public DateTime LastStanceChange` — **and copy them in the copy-ctor/Clone** (L3/save-safety).
3. In the brain wire (`GroundTacticalBrain.cs` ~line 220–241, the non-break-glass path): keep the current stance unless the odds ratio has moved **outside** `[threshold·(1−HysteresisBand), threshold·(1+HysteresisBand)]` **or** `MinHoldHours` have elapsed since `LastStanceChange`. Preserve the break-glass bypass for survival shifts. On an accepted change, stamp `LastStanceFamily`/`LastStanceChange`.
**Byte-identity:** default-off? No — this is always-on ground-AI behavior, but it only runs under `EnableGroundTacticalAI` (already default off), so the engine suite is unaffected unless a test enables the brain.
**Gauge:** add a test to the ground fixture: a battalion with odds oscillating ±0.05 around a threshold holds its stance across ≥2 hourly ticks (was: flipped each tick). CI green.

## Slice 3 — M3: wire the fog-honest landing score + fix the connection-map claim
**Why:** `GroundThreat.DetectedDefenderStrength` is built and fog-honest but has zero consumers; the AI picks a landing site blind, and `SYSTEM-CONNECTION-MAP.md:107` overclaims "two consumers."
**Change:**
1. In `GameEngine/Factions/ConquerResolver.cs`, in the invasion landing-choice arm, source per-body (or per-hex) defender strength from `GroundThreat.DetectedDefenderStrength(body, viewerFactionId)` and prefer the lowest detected-defence landing site. Keep it **hex-first** per decision 5 (score candidate hexes; fall back to body-level if no hex detail). Do **not** read `GroundForcesDB.Units` directly — that would bypass fog.
2. Add a direct-call test: a scouted, defender-heavy site scores worse than an unscouted (reads-low) site.
3. Update `docs/SYSTEM-CONNECTION-MAP.md:107` to reflect the now-real second consumer.
**Gauge:** the new test + CI green.

## Slice 4 — low-severity CODE batch (one commit)
- `GameEngine/Factions/FactionInfoDB.cs` copy-ctor (~line 185): add `FleetMinToDeploy`, `FleetIdealSize`, `FleetPerfectSize`, `FleetTemplateName`, `GarrisonComposition` copies (latent save/clone loss — real, low).
- `Pulsar4X.Tests`: repoint the P8.1c `[Ignore]` reason (TWOD merged) to a real anchor save/load gauge, **or** add a small TWOD-owned test that seeds `FleetCombatStateDB` anchors behind `EnableGroupPlane`, `Save→Load`, asserts they survive.
- `Pulsar4X.Client`: add the C5a **orbit-view** embark/land entry point — a troop-bay-ship context-menu action (gated on `GroundTransport.BayCapacity > 0`, the `DeployStationOrder` precedent).
- `Pulsar4X.Tests/SafeDictionaryEventLockTests`: add one interleaved-mutation test (mutator thread Add/Remove while the test thread enumerates `ValuesSnapshot`/`KeysSnapshot` — asserts no throw).
**Gauge:** the two new tests green; client compiles.

## Slice 5 — DOC + hygiene batch (one commit)
- `.gitignore`: add `console_output.txt`, `game_logs/`, `imgui.ini`; `git rm --cached` the tracked copies.
- `GameEngine/Sensors/CLAUDE.md:273`: update the grave-rung bullet — contacts now age out (`SensorScan.ContactStaleSeconds`).
- `docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md` line 3: flip build-state to "S0+S1+S2 built (Earthfall T1–T3, behind `EnableGroupPlane` default-off, byte-identical); S3–S6 later."
- `docs/DOCS-INDEX.md`: refresh the SURFACE-FOG row (slices 1–4 built; 5 = GroundThreat; 6 pending) **and add rows for the two new audit docs** (`IMPLEMENTATION-AUDIT-2026-07-22.md`, `AUDIT-RESOLUTION-PLAN.md`); refresh the As-of stamp.
- `ShipDesign.cs:167-171` + `LaunchComplexProcessor.cs:125-130` comments: match the decision-3 flip ("both flags default ON as of dev decision 3, 2026-07-21").
- `docs/TESTING-TRACKER.md`: copy the four T-P4.1..T-P4.4 seven-field rows from `LANE-CORE-NOTES.md` into the Layer-1 table.
**Gauge:** no code change; `build-client` + `test` stay green.

---

## Tracked follow-on (NOT in this pass — needs its own slice)
**M4 — retire the beachhead's region gating to hex granularity.** `GroundBeachhead.cs` build/hold/enemy-free/resupply gates are region-addressed; decision 5 says hex is the unit. Non-blocking for the play-test (the beachhead works). When scheduled: key `GroundBuildSite` + the surface-part crate on `(regionIndex, Q, R)`/global-hex; replace the region-owner HOLD gate with `GroundHex.OwnerFactionID` (the mechanism G3 capture already flips) with a region fallback; make the enemy-free check hex-radius-based. Record in a new ground-lane note.

## Slice order summary
1 (decision-gated) → 2 → 3 → 4 → 5, each its own commit + CI-green gate. Slices 1–3 clear the mediums; 4–5 clear the lows; M4 is deferred and tracked.
