# Operation Earthfall — Implementation Audit (plan-vs-code, line-by-line)

**As of 2026-07-22. The last verification pass before DEV-PC play-testing.**
**Scope:** the ~102-commit window `92b51ae..a962475` on `claude/devtest-faction-design-xpfnhe`, merged to `main` via PR #87 (`2cb6596`). The working tree at audit time == that merged state.

## 0. What this is (plain English)

A big body of work — the Operation Earthfall campaign plus the pre-campaign ground/AI/detection tracks — was built across five parallel branches and merged into `main`. This audit compares **what was planned** (the campaign plan, the locked design docs, the lane notes, the developer-decisions record, and every commit message) against **what the code actually does now**, one planned item at a time, citing `file:line`. The goal is to catch anything missing, half-built, or built-differently **before** you spend play-testing time on it — so a green test means the feature is really there, and a red one is a real bug and not a mirage.

**Method.** 26 audit "units" (one per phase/track) each enumerated every discrete planned item and located it in source. Each non-clean finding was re-checked against the source directly (the crash-risk and the four medium findings were hand-verified for this report). No compiler was available (the container has no .NET SDK), so every claim is by source reading, not a build.

## 1. Headline verdict

**The implementation is faithful to the plan. No critical or high-severity defects.** The surface that would break a New Game or the test suite — new components' six-point registration, save-safety of new data, enum ordering, one-hotloop-per-blob, processor throw-safety, and all five lane merges — is **verified clean**. What remains is four medium items (three are "the code exists but is inert / not wired"; one is a design-model drift) and fifteen low items (mostly doc/comment drift plus a few small code fixes).

| Severity | Count | Nature |
|---|---|---|
| Critical | 0 | — |
| High | 0 | — |
| **Medium** | **4** | 1 dormant fix (flags off), 1 unbinding hysteresis, 1 orphan read (landing score unwired), 1 hex-vs-region design drift |
| **Low** | **15** | 9 doc/comment, 1 latent copy-ctor, 1 gitignore hygiene, 1 test-coverage, 1 [Ignore]-reason, 1 UI entry-point, 1 commit-msg note |

**The single most important item before you play-test is M1 (P3.2g).** The whole point of phase P3 was to fix the AI abandoning its own invasion (finding A3). The fix code shipped, but the two flags that turn it on in a *menu-started* game (`ReadCurrentMorale`, `EnableRebellionDebounce`) were never flipped — they default `false` and are only turned on inside unit tests. So a live game still runs the *old* stale-morale legitimacy read and one-sample rebellion trigger. Your play-test would be exercising the pre-fix behavior for those two seams unless you flip them (see the resolution plan). The specific A3 collapse is still blocked indirectly by P3.1's war-term and the always-on P3.3/P3.4 machinery — so it's not a regression, but seams 2–3 are dormant.

## 2. Coverage

26 units. 18 received the full line-by-line treatment with an adversarial-verifier pass queued (445 audit items enumerated). 8 units (the tail cut off by a usage-limit wall) were **hand-verified in this pass** for their crash-risk surface and headline behavior — findings below. The crash-risk surface for the 8 was already independently swept by the `cross-cutting-invariants` unit, which is why they carry no critical/high risk.

Deep-audited (18): P0-gauges · P3-objective · P4-sealift · G1-beachhead · G2-tactical-brain · G3-G4-infra-sealed · C1-C2-freeze-sensor · C3-C4-C5-force-mgmt · D1-D3-kithrin · T-2d-resolver · PW-wiring · P8-acceptance · decisions-applied · merge-integrity · cross-cutting-invariants · sensorscan-freeze · detection-snapshot · surface-fog.

Hand-verified this pass (8): ai-military-execution · w-track-squad · ground-depth-litmus · living-opponents-edge · task36-invasion-chain · ci-and-tests-state · docs-dashboards · franchise-litmus-docs.

## 3. The four MEDIUM findings (all hand-verified against source)

### M1 — P3 legitimacy fixes are DORMANT in a live menu game  (`P3-objective`, PARTIAL, medium)
**What was planned:** P3.2 makes legitimacy read the *current* cycle's morale (kills the stale-morale echo), and P3.2/P3.3 add a rebellion debounce so a single bad sample can't trigger a revolt — the core of the A3 objective-flip fix.
**What the code does:** both behaviors are gated on statics `LegitimacyProcessor.ReadCurrentMorale` and `EnableRebellionDebounce`, **both `= false`** (`GameEngine/Colonies/LegitimacyProcessor.cs:58,68`). They are turned on only inside tests. The live menu path `NewGameMenu.cs:547,949` flips `NPCDecisionProcessor.EnableOrderEmission = true` but **not** these two.
**Verified:** whole-tree grep — the only `true` assignments are in test files; `NewGameMenu` sets `EnableOrderEmission` and not the legitimacy flags. Confirmed.
**Impact:** a live game runs the pre-fix stale-morale legitimacy read and the one-sample rebellion trigger. The A3 collapse itself is still prevented indirectly (P3.1 war-term + always-on P3.3/P3.4), so no regression — but the P3.2/P3.3 *behavior* you'd want to test is off. **Needs a developer decision** (flip on vs. leave dormant); recommended flip-on, mirroring the `EnableOrderEmission` pattern in the same menu block. Engine test-suite stays byte-identical (factory-built test games never run `NewGameMenu`).

### M2 — Ground posture hysteresis never binds  (`G2-tactical-brain`, DIVERGED, medium)
**What was planned:** "a posture holds until the odds cross a band (hysteresis), respecting the stance cooldown," with a hysteresis band + minimum-hold as FLAGGED developer numbers (GROUND-TACTICAL-AI-DESIGN §5; listed among the six brain thresholds whose "defaults stand" in EARTHFALL-DECISIONS).
**What the code does:** there is no odds-band or minimum-hold. The implementation substitutes "cooldown = hysteresis" (`GroundTacticalBrain.cs:239`, recorded in LANE-GROUND-NOTES:336). But the stance-catalog cooldowns are **60/300/300 seconds** (`groundStances.json`) while the ground brain ticks **hourly** (`GroundForcesProcessor.cs:29`, `RunFrequency = FromHours(1)`). Every cooldown has expired by the next decision, so the substitute never binds.
**Verified:** cooldowns 60/300/300s vs `FromHours(1)` tick — confirmed. A battalion whose odds oscillate around a threshold can flip stance every tick.
**Impact:** ground AI stance flip-flopping; also, the "defaults stand" line in EARTHFALL-DECISIONS points at constants that don't exist in code. Medium — visible as jittery ground AI in exactly the ground test you're about to run.

### M3 — `DetectedDefenderStrength` is an orphan read; the PW landing score was never wired  (`G2-tactical-brain` G2.2a + `surface-fog` consumer, PARTIAL, medium)
**What was planned:** "one read, two consumers" — `GroundThreat.DetectedDefenderStrength(body, viewer)` feeds both the tactical brain *and* the PW "easiest-landing" score, fog-honestly (SURFACE-FOG design; SYSTEM-CONNECTION-MAP:107).
**What the code does:** the read exists and is fog-honest (`GroundThreat.cs:120-131`) but has **zero consumers** — a repo-wide grep finds only its own definition. `ConquerResolver` never calls it; the landing choice does not consult detected defender strength.
**Verified:** `grep -rn DetectedDefenderStrength` → single hit (the definition). Confirmed. `SYSTEM-CONNECTION-MAP.md:107` overclaims "two consumers."
**Impact:** the AI picks a landing site without reading (even fog-limited) enemy strength; the fog→landing wire the design promised is absent. Medium (AI quality, and a stale connection-map claim). Partially excused by decision-5 (hex redirect) but not recorded as a deliberate drop.

### M4 — Beachhead build is region-grained under the now-locked hex model  (`G1-beachhead`, DIVERGED, medium)
**What was planned (later):** decision 5 (2026-07-21) — "regions are cosmetic; the hex is the unit of everything."
**What the code does:** the beachhead build/hold/enemy-free/resupply **gates** are region-addressed (`GroundBeachhead.cs:80,92-95,156-157`; `GroundBuildSite.RegionIndex`), though the final building placement does land on hexes (`GroundBuildings.cs:168-180`). G1 landed 2026-07-18, three days *before* decision 5, and matched its own plan wording at build time; the later PW slices complied with hex (they cite "the hex is the unit (dev decision #5)"). No note flags `GroundBeachhead`'s region gating as retire-to-hex work.
**Impact:** design-model drift, not a bug — the beachhead works, but at region granularity under a model that says hex. **Non-blocking for the play-test**; recommended as a tracked follow-on slice.

## 4. The fifteen LOW findings (grouped)

**Small code (4):**
- `cross-cutting-invariants`: `FactionInfoDB` copy-ctor doesn't copy its 5 new fleet-composition/garrison fields (`FleetMinToDeploy/FleetIdealSize/FleetPerfectSize/FleetTemplateName/GarrisonComposition`) — a latent save/clone loss (negligible today because entity-move of a faction blob is rare, but a real gap). Fix: add 5 lines to the copy-ctor (~line 185).
- `P8-acceptance`: the P8.1c `[Ignore]` "battle-frame anchors survive save/load" reason says "if TWOD merged" — TWOD *did* merge, so either re-point the reason or add the small anchor save/load test.
- `C3-C4-C5`: C5a embark/land buttons are wired from the manager but the **orbit-view** entry point is absent — add a troop-bay context-menu action (the `DeployStationOrder` precedent).
- `P0-gauges`: no direct unit test for `ValuesSnapshot/KeysSnapshot` stability under concurrent mutation (the commit message overclaims the gauge). Add one interleaved-mutation test.

**Doc / comment drift (9):**
- `detection-snapshot`: `Sensors/CLAUDE.md:273` grave-rung still says "no contact-expiry pass yet" — contacts now age out; update the bullet.
- `T-2d-resolver`: `RESOLVER-2D-GROUP-PLANE-DESIGN.md` header line 3 still says "not started (S0 is the first slice)" — S0+S1+S2 are built (behind `EnableGroupPlane`, default off). One-line flip.
- `surface-fog`: `DOCS-INDEX.md:103` SURFACE-FOG row status is stale (slices 1–4 built; 5 = GroundThreat; 6 pending). Refresh row + As-of stamp.
- `P4-sealift`: `ShipDesign.cs:167-171` + `LaunchComplexProcessor.cs:125-130` comments predate the decision-3 flip; update to "both flags default ON as of dev decision 3."
- `P4-sealift`: `TESTING-TRACKER.md` lacks the per-test T-P4.1..T-P4.4 seven-field rows (they live only in LANE-CORE-NOTES).
- `P3-objective`: `eaebea4` commit message mis-describes break-glass (a) — cosmetic, add a one-line note if wanted.
- `P4-sealift`: P4.4c documents that STEP-2 (grind the build through IndustryProcessor day-by-day) was deliberately replaced by a direct completion — recorded and matches the amended plan; no action.
- `C3-C4-C5`: C3 filters are system/body only, not faction (plan text said faction too) — non-issue unless a spectator/SM mode wants it.
- `C3-C4-C5`: C3 selection→move uses the existing move path, not a dedicated region/global-hex picker — cosmetic.

**Hygiene (1):**
- `merge-integrity`: the PR merge carried committed runtime artifacts (`console_output.txt` 2170 lines, `game_logs/`, `imgui.ini`). Add to `.gitignore` + `git rm --cached` so future play sessions stop generating tracked diffs.

## 5. The 8 hand-verified tail units (this pass)

All structurally clean; no new crash risk (their new components/enums/blobs were already swept by `cross-cutting-invariants`). Behavior spot-checks:
- **ci-and-tests-state:** CI sharding is **gap-proof by construction** — the `rest` shard (ci.yml:69) is a complement filter (`!~` of every named shard), so any new fixture lands there automatically. No fixture can be excluded from all shards. Clean.
- **living-opponents-edge:** `FactionFactory.cs:95,107` reads both the `fleetComposition` and `garrison` JSON nodes; both present in `umf.json`/`kithrin.json`; `GarrisonComposition` is `[JsonProperty]`. No unread-node / unresolvable-template crash. Clean (modulo the M-low copy-ctor above).
- **task36-invasion-chain:** exactly two `ProvisionBuiltShip` callers (industry `ShipDesign.cs:172`, launch `LaunchComplexProcessor.cs:131`), both through the single `IsNPC ? ChargeBuiltNpcShips : ChargeBuiltPlayerShips` gate — **no double-charge**, coherent with P4.2. Clean.
- **ai-military-execution:** `JumpRouter`/`MilitaryReach`/`MilitaryCommand`/`MilitaryTarget` all present; multi-jump routing + reserve posture symbols exist. Structurally complete; deep routing behavior is a dev-PC runtime check.
- **w-track-squad:** per-range-band + loadout symbols present; `GroundSquadSizeTests.cs` (the bulk==individual stat-identity gauge) exists.
- **ground-depth-litmus:** `GroundUpkeep.BillIfDue` folded into `GroundForcesProcessor.cs:112` (L9-safe, byte-identical for 0-upkeep units).
- **docs-dashboards / franchise-litmus-docs:** doc-truth; residual doc-row drift folded into §4.

## 6. What this audit could NOT verify

- **Runtime behavior.** CI cannot run the client; the container has no SDK. Everything is source-verified, not built or played. The dev-PC play-test remains the only runtime gauge — that is the point of this pass, to make that test valid.
- **The adversarial-verifier pass** on the low findings did not complete (usage wall); the four mediums and the crash-risk surface were hand-verified for this report. The disk-cached audit workflow can finish the remaining verifier verdicts when budget resets, but no low finding changes the resolution plan.
