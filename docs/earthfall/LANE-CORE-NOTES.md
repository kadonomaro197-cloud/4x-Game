# LANE CORE — pending dashboard rows, cross-lane requests, developer decisions

This is the CORE lane's conflict-free notes file (CAMPAIGN-PLAN.md §2.4). Lanes do NOT edit the shared
dashboards (`docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`) or a
collision-prone subsystem `CLAUDE.md` mid-flight; each pending row lands here and the integration phase (P8.2)
applies them. Sections are delimited by slice so parallel siblings can append without conflict.

---

## P0.5 — Latent concurrency fixes (SafeDictionary events-under-lock + EntityManager values-snapshot) + stale-docs (2026-07-18)

**Files changed (this slice, all inside the CORE fence):**
- `Pulsar4X/GameEngine/Engine/DataStructures/SafeDictionary.cs` — (a) `ItemAdded`/`ItemRemoved`/`OnChange` now fire
  **copy-then-notify**: the mutator captures the handler under `_lock`, releases the lock, THEN invokes (the indexer
  setter, `Add`, `Remove`). Previously they invoked while holding `_lock` (:100/:110-111/:122-123). (b) Added two
  additive members `ValuesSnapshot`/`KeysSnapshot` (a `List<T>` copied under the lock, the `GetEnumerator` :158-166
  pattern) so callers on the parallel sim threads can enumerate without racing the live `.Values`/`.Keys` collection.
- `Pulsar4X/GameEngine/Engine/Entities/EntityManager.cs` — all seven live `.Values`/`.Keys` enumeration sites now use
  `ValuesSnapshot`/`KeysSnapshot`: `GetAllDataBlobsOfType` (:393), `GetAllEntites` (:516), `GetAllEntitiesWithDataBlob`
  (:557, the H3 site named in the ledger), `GetAllEntitiesWithDataBlobs` (:593), `GetFirstEntityWithDataBlob` (:635
  `.Keys.First()`), and both branches of `GetFilteredEntities` (:758/:760).
- `Pulsar4X/GameEngine/CLAUDE.md` — corrected the stale `ReaderWriterLockSlim` claim (it's a plain `Monitor`) and
  documented the copy-then-notify event contract + the `ValuesSnapshot`/`KeysSnapshot`-vs-live-`.Values` rule.
- NEW `Pulsar4X/Pulsar4X.Tests/SafeDictionaryEventLockTests.cs` — the re-entrancy gauge for (a).

**Subscriber audit for (a) — none depend on under-lock semantics:** a repo-wide grep for `ItemAdded`/`ItemRemoved`/
`OnChange` and for `DictionaryChangedHandler` / `+=` on these events found **zero external subscribers** — the only
references anywhere are the declarations and the `?.Invoke` calls inside `SafeDictionary.cs` itself. So the events are
currently always null (every `?.Invoke` is a no-op today); the fix changes nothing observable now and makes the
contract correct for any future subscriber. **Verdict per event: SAFE (no subscriber exists).**

**`.Values`/`.Keys` exposure audit (b):**
- IN FENCE — FIXED: the seven `EntityManager.cs` sites above. `_entities`/`_datablobStores` are the SafeDictionaries
  mutated from the parallel sim loop (`MasterTimePulse` `Parallel.ForEach`), so these are the real H3 race.
- OUT OF FENCE — flagged, NOT edited (see cross-lane requests below): `ManagerSubPulse.cs` (:262 `HotLoopProcessorsNextRun.Keys.ToList()`,
  :320/:322 `.Values.Min()`), and the load-once reference libraries `CargoDefinitionsLibrary.cs` (many `_definitions`/
  `_minerals`/`_processedMaterials.Values.Where(...)`), `FactionDataStore.cs` (:98 `modDataStore.Minerals/ProcessedMaterials.Values.ToList()`),
  and `Game.cs` (:180 `AtmosphericGases.Values.Where(...)`). **Verdict:** ManagerSubPulse is per-system single-threaded
  (only that system's subpulse thread touches its `HotLoopProcessorsNextRun`), so its exposure is theoretical, not the
  freeze; the reference libraries are populated once at load and never mutated during the sim, so enumerating their
  live views cannot race. Lower risk than the entity stores; left to their owning lanes. `ValuesSnapshot`/`KeysSnapshot`
  are now available for them to adopt cheaply.

**Tests added + what they assert (`SafeDictionaryEventLockTests`, 4 tests, each `[Timeout(30000)]`):**
- `Add_RaisesItemAdded_WithoutHoldingLock`, `Remove_RaisesItemRemoved_WithoutHoldingLock`,
  `IndexerSet_RaisesOnChange_WithoutHoldingLock` — from INSIDE the notification, launch a SEPARATE thread that reads
  the dictionary (takes `_lock`) and Join it with a bounded 3 s timeout. Monitor is reentrant on the same thread, so a
  different thread is required to actually block on a held lock. Under the old under-lock behaviour the reader can
  never acquire the lock → bounded Join returns false → test FAILS; under the fix the lock is already released → reader
  completes → test PASSES. This is the discriminating gauge.
- `Add_StillNotifiesSubscribersWithCorrectArgs` — correctness backstop: the notification still delivers the right
  key/value and the mutation is already visible (`ContainsKey` true) when the handler runs.

**Byte-identity claim: (b) provably inert absent new data (no default game path is altered).** Two independent reasons:
(1) the copy-then-notify change is a no-op in every current run because the three events have zero subscribers — no
handler exists to observe under-lock vs after-lock ordering; (2) `ValuesSnapshot`/`KeysSnapshot` are purely additive
new members, and swapping the seven EntityManager reads from the live view to a same-contents copy returns the SAME
set of entities/blobs in the SAME (unspecified — the XML doc already says "DO NOT ASSUME THE ORDER") order semantics.
No flag, no gameplay logic, no scheduling, no serialization touched. Every existing green test stays green; the only
behavioural difference is the elimination of a concurrent-modification throw that could only fire under a race.

**FLAGGED balance numbers:** ONE, and it is a test-tuning constant, not a gameplay number:
`SafeDictionaryEventLockTests.ReaderJoinTimeoutMs = 3000` (carries the `// FLAGGED balance value` marker per the
standing rule; it is the bounded cross-thread Join window, not a sim value).

**Developer decisions raised:** NONE.

### Cross-lane requests (out-of-fence `.Values`/`.Keys` adoption — advisory, low priority)
- **→ whoever owns `Engine/ManagerSubPulse.cs`:** consider switching `HotLoopProcessorsNextRun.Keys.ToList()` (:262)
  and `.Values.Min()` (:320/:322) to `KeysSnapshot`/`ValuesSnapshot`. Low urgency — per-system single-threaded, not
  the freeze — but it removes the last live-view enumerations on a mutated SafeDictionary.
- **→ economy/data lanes (`CargoDefinitionsLibrary.cs`, `FactionDataStore.cs`, `Game.cs`):** these enumerate live
  `.Values` on load-once reference dictionaries; safe today (never mutated during sim). Adopt the snapshots only if a
  future change makes them mutable at runtime.

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P0.5 `SafeDictionaryEventLockTests`** · *what:* proves `SafeDictionary` raises its change events AFTER releasing
  the internal lock (copy-then-notify) · *why:* A1-freeze H2 flagged events fired under the lock — a cross-thread
  deadlock/contention hazard on the parallel sim threads · *method:* from inside a notification, a separate thread
  reads the dictionary and must finish within a bounded Join · *right-looks-like:* the concurrent reader completes; the
  notification still delivers correct args post-mutation · *likely-failure:* a future edit re-introduces `Invoke` inside
  `lock(_lock)` (the reader deadlocks → bounded Join false → red) · *mitigation:* this gauge + the corrected
  GameEngine/CLAUDE.md contract note · *unblocks:* safe use of `SafeDictionary` events by any future subscriber, and
  the H3 values-snapshot fix that removes the latent concurrent-modification throw in `EntityManager` entity queries.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `SafeDictionary` (Engine/DataStructures) ⟷ `EntityManager` entity/blob stores ⟷ the parallel sim loop
  (`MasterTimePulse.Parallel.ForEach`): entity queries now read a lock-taken snapshot, closing the H3 race where a
  concurrent `Add`/`Remove` on another sim thread invalidated a live `.Values` enumeration mid-query.

---

## P0.4 — Honest station-aware tape + stale-doc fixes (2026-07-18)

**Files changed (this slice):**
- `Pulsar4X/GameEngine/Factions/AIDecisionRecordDB.cs` — added `AIDecisionRecord.StationCount` ([JsonProperty], wired
  into the deep-copy ctor); fixed two stale "monthly Tick" doc mentions (record is taped each DAILY Tick) and the
  Capacity comment ("~5 game-years of monthly decisions" → "~2 game-months of daily decisions" — the value 60 is
  UNCHANGED, only the comment).
- `Pulsar4X/GameEngine/Factions/AIDecisionRecorder.cs` — records `rec.StationCount = factionInfoDB.Stations?.Count ?? 0`
  (mirrors the existing `Contacts` inline read); fixed the class-doc "each monthly Tick" → "each daily Tick" (the
  recorder is called ungated every Tick at `NPCDecisionProcessor.cs:239`, and `RunFrequency` is 1 day).
- `Pulsar4X/GameEngine/Factions/PlanReadout.cs` — `DecisionLine` now renders `stations {N}` alongside `colonies {N}`.
- `Pulsar4X/GameEngine/Factions/CLAUDE.md` — fixed the stale `RunFrequency 30d`/`FirstRunOffset 5d` "monthly" cadence
  claim in TWO rows (the `NPCDecisionProcessor.cs` file-map row + the GlobalManager-wiring paragraph) to the real
  DAILY cadence (`NPCDecisionProcessor.cs:30-31`), noting the slow political passes keep a monthly `Day == 1`
  sub-cadence; and added a "P0.4 STATION-AWARE" note to the recorder's SENSED-half row.
- `Pulsar4X/Pulsar4X.Tests/AIDecisionRecorderTests.cs` — extended the existing `Tick_TapesTheDecision_...` test.

**Tests added / extended + what they assert:** extended `AIDecisionRecorderTests.Tick_TapesTheDecision_WithSensedContext_AndGrows`.
It now gives the NPC one station (`info.Stations.Add(Entity.Create())`) on a faction with zero colonies — the exact
Kithrin "colonies 0 while owning Titan" scenario — and asserts (1) `rec.ColonyCount == 0` and `rec.StationCount == 1`
(the record captures BOTH), and (2) the rendered `PlanReadout.DecisionTape` line contains BOTH `colonies 0` and
`stations 1` (the readout is honest). No new fixture file (extends an existing one, per the slice brief).

**Byte-identity claim: (b) provably inert — no simulation path is touched.** This is a PURE-OBSERVABILITY change:
- No processor logic, decision, ordering, or scheduling changed. The recorder is already always-on and runs after the
  decision; it just tapes one more already-computed number (`FactionInfoDB.Stations.Count`).
- The new `StationCount` is a save-safe APPENDED `[JsonProperty]` — `TypeNameHandling.Objects` + Newtonsoft match by
  NAME, so an older save with no `StationCount` value deserialises it to the default 0.
- The only rendered-output change is `PlanReadout.DecisionLine` gaining `, stations {N}`. Every existing assertion on
  `DecisionLine`/`DecisionTape` in the suite is a substring/`Does.Contain` check (verified: only
  `AIDecisionRecorderTests` and the log-only `DevTestInvasionReadout` read the tape) — none pin the exact string — so
  all existing green tests stay green. NOT gated by a flag (a pure gauge needs none), NOT strictly "absent new data"
  (the field is always populated), but provably inert to the sim and to every existing test.

**FLAGGED balance numbers:** NONE. No gameplay number added or changed. `AIDecisionRecordDB.Capacity` stays 60 (only
its comment was corrected).

**Developer decisions raised:** NONE.

**Cross-lane requests:** NONE — every file was inside the CORE fence and named by the slice.

**Parallel-safety note (subsystem CLAUDE.md edited directly):** I edited `GameEngine/Factions/CLAUDE.md` in place
because the `RunFrequency 30d` correction IS this slice's named deliverable and P0 runs pre-fork. The concurrent P0
siblings (P0.1/P0.2/P0.3, per their sections below) are test-file + `ci.yml` only and do NOT touch Factions/CLAUDE.md.
The only theoretical collision is **P0.5's "stale-docs corrections"** — but those target the SafeDictionary/
EntityManager (Engine) docs, not the Factions AI-cadence rows. My three Factions/CLAUDE.md edits are surgical unique-
string replacements on the NPCDecisionProcessor cadence + the recorder SENSED row, so a merge with P0.5 should not
conflict. If a conflict does surface at land time, the authoritative text is: NPCDecisionProcessor is **daily**
(`FirstRunOffset` 1d / `RunFrequency` 1d, `NPCDecisionProcessor.cs:30-31`), slow political passes on a monthly
`Day == 1` sub-cadence; and the recorder's SENSED half now includes **stations** (`StationCount`).

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P0.4 `AIDecisionRecorderTests` (extended)** · *what:* the AI flight-recorder tape now records + renders station
  count alongside colony count · *why:* A6 (Q4) found the Kithrin tape read "colonies 0" while owning Titan station —
  `AIDecisionRecorder` recorded `FactionRollup.ColonyCount` (Colonies only), silently dropping station-only factions ·
  *method:* tape one decision for an NPC with 0 colonies + 1 station; assert the record and the rendered `[AI]` line
  carry both counts · *right-looks-like:* `rec.StationCount == 1`, readout contains `colonies 0` and `stations 1` ·
  *likely-failure:* a future rename of `FactionInfoDB.Stations` (would break the inline read) · *mitigation:* the
  extended test reds if the count is dropped · *unblocks:* an honest AI decision tape for station-only factions (the
  Kithrin), so the DEV lane's Kithrin work (D1/D2) reads a truthful footprint in the flight recorder.

---

## P0.2 — Per-faction self-sufficiency readout (2026-07-18)

**Files changed/created (this slice):**
- NEW `Pulsar4X/Pulsar4X.Tests/FactionSelfSufficiencyReadoutTests.cs` — the readout fixture.
- NEW/writes at runtime `TestResults/self-sufficiency-readout.txt` (repo-root TestResults, resolved by walking up
  to the `.github` folder so it lands where CI's `--results-directory TestResults` writes the TRX = the uploaded
  artifact + the readout-cat step reads).

**Byte-identity claim:** (b) provably inert. No production/engine code touched — this slice adds ONE test file
plus this notes file. The fixture only READS game state and advances the clock via the ordinary public
`MasterTimePulse.TimeStep` path; `DevTestStartFactory.CreateDevTest` does NOT flip the NPC AI action gates
(`EnableOrderEmission` & siblings default `false`), so the AI settles objectives but emits no orders — the sim is
byte-identical to a gate-off DevTest and every existing test is unaffected.

**FLAGGED balance numbers:** NONE. The fixture asserts only structural truths (factions iterated, hosts iterated,
zero readout errors, cargo mass finite, player has a colony, ledgers non-empty). No balance value is asserted, so
nothing rots when the developer tunes the economy.

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
*(Not edited directly — that table is a high-collision target shared with sibling P0 slices. Land this row at P8.2.)*

| `FactionSelfSufficiencyReadoutTests` | **P0.2 — the per-faction self-sufficiency board.** Loads the DevTest sandbox (UEF+UMF+Kithrin), advances ~120 days (`[Timeout]`-guarded), and per faction iterates `FactionInfoDB.Colonies` AND `.Stations`: per host prints food supply-vs-demand (`ColonySustenanceDB` + `GetTotalFoodOutput`), power (`EnergyGenAbilityDB`), mining rate + deposit depletion (`MiningHelper.TryGetMiningBody`→`MineralsDB`), industry line job statuses, and cargo mass delta; per faction prints the `Ledger` totals by `TransactionCategory` + balance start→end. Writes `TestResults/self-sufficiency-readout.txt`. Asserts STRUCTURAL truths only (ran for all hosts, zero readout errors, cargo finite, ledgers non-empty) — no balance numbers. **Would have caught the Kithrin station-upkeep drain on day one** (the ledger line shows StationUpkeep bleeding a treasury with no income). |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P0.2 `FactionSelfSufficiencyReadoutTests`** · *what:* per-faction/per-host economy self-sufficiency board over 120 DevTest days · *why:* A6 found the Kithrin are structurally bankrupt (station upkeep, no income) and nothing in CI watched the multi-faction economy over time · *method:* advance the DevTest sandbox 120 days, read every host + ledger · *right-looks-like:* ran for all hosts, ledgers non-empty, cargo finite · *likely-failure:* a frozen (Stasis) system → zero ledger transactions (the assert reds), or a processor throw during the advance · *mitigation:* promotes Sol out of Stasis (`IncrementExternalObserver`), `[Timeout]` guard · *unblocks:* the DEV lane's Kithrin station-income fix (D2) has a before/after gauge; a visible baseline of "self-sufficient" per faction.

### Cross-slice note → P0.3 (ci.yml readout-cat step) and CI-shard owner
- The readout is written to `TestResults/self-sufficiency-readout.txt` (repo-root). P0.3's cat step can read it there
  with `if: always()`, same pattern the A6 dossier prescribes for `econ-readout.txt`.
- **Shard caution:** `FactionSelfSufficiencyReadoutTests` is a HEAVY fixture (multi-faction 120-day sim) and lands
  in the `rest` shard by default (the complement filter). If CI shows it pushing `rest` past the `stations`
  bottleneck (read the TRX duration column), consider giving it its own shard or folding it into the
  `economy-readout` shard's filter — a `ci.yml` decision, CORE-owned (P0 only). Not done here to keep this slice a
  pure test addition; flagged for the session/P0.3.

### Cross-slice note → DEV lane (D2 station income) and P3.1 (Ceres factory data)
- This fixture is the standing gauge for A6's two findings: run it before/after D2's station-income processor to
  see the Kithrin balance stop draining, and after P3.1's Ceres factory to see Ceres' industry line appear. No
  action requested — just naming the consumers so the gauge gets used.

---

## P0.1 — Campaign-clock CI repro fixture (2026-07-18)

**Files changed/created (this slice):**
- NEW `Pulsar4X/Pulsar4X.Tests/CampaignClockReadoutTests.cs` — the campaign-clock repro fixture.
- NEW/writes at runtime `TestResults/campaign-clock-readout.txt` (repo-root TestResults, resolved by walking up to
  the `.github` folder — the SAME helper pattern P0.2 uses, so both readouts land where CI's
  `--results-directory TestResults` writes the TRX and P0.3's readout-cat step reads them).
- NO production/engine code touched.

**Byte-identity claim:** **(a) default-off flag** — the fixture FLIPS the four `NPCDecisionProcessor` gates
(`EnableOrderEmission`/`EnableDiplomaticProposals`/`EnableEspionageMirror`/`EnableIntelLedger`) AND the three
`CombatEngagement` combat flags (`InterruptTimeOnNewEngagement`/`RequireDetectionToEngage`/`RequireWeaponRangeToEngage`)
ON for the duration of its own run, then RESTORES all seven to their captured prior values in a `finally` (same
capture/restore discipline as `NPCActingSensorTests` + `CombatFleetTreeSafetyTests.StandingHostilePair`). These are
process-global statics that default OFF, so every other test in the shard stays byte-identical. The per-game `game.Settings`
mutations (`EnableMultiThreading`/`EnforceSingleThread`/`Ticklength`) are on the throwaway game instance — no leak.

**FLAGGED balance numbers (2):**
- `TransitGameDays = 40` — the transit window length (kept the strike fleet in deep transit, pre-arrival ~05-18).
- `MaxWallMsPerGameDay = 30_000` — the generous per-game-day wall ceiling that catches the PERF-freeze crawl.

**Developer decision raised (threading):** the fixture runs `EnableMultiThreading=true` (the parallel sim path the
A1-freeze ledger's MISSING section asks for, so H2/H3 parallel-race hypotheses are on the table) together with
`EnforceSingleThread=true` (so `TimeStep()` blocks and the per-day gauges are deterministic — the two flags are
orthogonal in `MasterTimePulse`). **Honest limit:** the DevTest loads Sol only → ONE active star system → the inner
`Parallel.ForEach` has one element, so this drives the multithreaded code path but yields **no cross-system
parallelism**, and therefore does NOT actually reproduce H2/H3 here. Reproducing the parallel races needs ≥2 active
systems (a second colonized/observed system in the DevTest, or a second scenario system) — a future extension, flagged
for the session/developer. The fixture's PRIMARY value (FineStepCount flat / wall bounded / zero tick errors) is fully
delivered regardless.

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
*(Not edited directly — that table is a high-collision target shared with sibling P0 slices. Land this row at P8.2.)*

| `CampaignClockReadoutTests` | **P0.1 — the campaign-clock repro gauge (findings/A1-freeze.md).** Loads the DevTest war sandbox (UEF+UMF+Kithrin), OPENS the four `NPCDecisionProcessor` gates + the three `CombatEngagement` combat flags (`InterruptTimeOnNewEngagement`/`RequireDetectionToEngage`/`RequireWeaponRangeToEngage`) in a try/finally restore, promotes Sol to Foreground, and drives the REAL master clock (`game.TimePulse.TimeStep()`, 3600 s steps, `EnableMultiThreading`+`EnforceSingleThread`) across 40 game-days. Per game-day records wall-ms + `MasterTimePulse.FineStepCount`/`CombatEngagement.TickCount`/`SensorScan.ScanCount` deltas + `NPCDecisionProcessor.TickErrorCount`/`LastTickError` + `ManagerSubPulse.GlobalCurrentProcess` to `TestResults/campaign-clock-readout.txt`. Asserts: FineStepCount FLAT on every day no hostile pair is within `CombatEngagement.EngagementRange_m` (proxied by `NewEngagementImminent`), wall-ms/day under a generous bound (the crawl catcher), and ZERO NPC tick errors. `[Timeout(600000)]`. The engine-side half of the freeze finding: fine-stepping is NOT implicated during transit — the real freeze was native-client (CI-invisible). |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P0.1 `CampaignClockReadoutTests`** · *what:* drives the real DevTest master clock 40 game-days with the AI gates + combat interrupt ARMED, gauging fine-step / wall-ms / tick-errors · *why:* A1 found the freeze was NOT a sim crawl (fine-step flat during transit; the real cause was a native-client render hang CI can't see) — this is the standing engine gauge that keeps the fine-stepper flat + the clock non-crawling under the full "everything enabled" state · *method:* `DevTestStartFactory.CreateDevTest`, open gates/flags in try/finally, promote Sol, `TimePulse.TimeStep()` in 3600 s steps, read per-day deltas · *right-looks-like:* FineStepCount delta 0 on non-imminent days, wall-ms/day ≪ 30 s, zero tick errors, readout written · *likely-failure:* the fine-stepper re-arms across advances (the chronic PERF-freeze regression → FineStepCount climbs while nothing is imminent), a processor throw (NPC tick error), or a genuine crawl (wall bound) · *mitigation:* the persistent fine-step give-up (`MasterTimePulse._fineStepGivenUp`) + `[Timeout]` guard · *unblocks:* a green baseline that the CLIENT lane's C1 freeze-fix and any future combat/clock change is measured against; proves the ENGINE half of the freeze is sound so the investigation stays pointed at the native-client render path.

### Cross-slice note → P0.3 (ci.yml readout-cat step)
- The readout lands at repo-root `TestResults/campaign-clock-readout.txt` via the same `.github`-walk helper as P0.2's
  `self-sufficiency-readout.txt`. P0.3's cat step should `cat` BOTH files with `if: always()` so a red run still prints
  the per-day table (the crawl/tick-error evidence).
- **Shard caution:** `CampaignClockReadoutTests` is a MODERATE fixture (single-system 40-day Foreground sim, ~1–3 min
  healthy) and lands in the `rest` shard by default. Combined with P0.2's heavier 120-day multi-faction fixture in the
  same shard, watch the `rest` TRX duration column; if `rest` overruns `stations`, a `ci.yml` shard rebalance is the
  CORE-owned P0.3 call.
