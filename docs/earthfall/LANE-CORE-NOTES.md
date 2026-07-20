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

---

## P3.1 — UMF government node + Ceres factory (data) (2026-07-18)

**Files changed/created (this slice):**
- `Pulsar4X/GameEngine/Factions/FactionFactory.cs` — `LoadFromJson` now parses an optional top-level **`"government"`**
  node (after the existing `"personality"` node) via a new public static `GovernmentFromJson(JToken)` helper (mirrors
  `PersonalityFromJson`) + a private `TryReadNotch` helper. The four dials (`authority`/`economy`/`openness`/
  `militarism`) are case-insensitive `GovNotch` (`Low`/`Mid`/`High`); an omitted/invalid dial keeps the `GovernmentDB`
  default (`Mid`). `CreateFaction` already attaches a neutral all-Mid `GovernmentDB` to every faction, so the parser
  `SetDataBlob`-**replaces** it only when the node is present (`SetDataBlob` overwrites the same-type blob,
  `EntityManager.cs:469`). VERIFIED there was no prior `"government"` parser (grep). This is A3 fix seam 1.
- `Pulsar4X/GameData/basemod/ScenarioFiles/umf.json` — (a) authored a `"government"` node: **Militarism High** (the
  load-bearing dial — flips `GovernmentDB.WarMoraleFactor()` from −0.25 to +0.5, so the legitimacy war term becomes
  +10 instead of the all-Mid default's −5) + **Authority High** (the fitting authoritarian dial, matching the UMF's
  `authoritarianism 0.7` personality); Economy/Openness left `Mid` (defaults) — deliberately minimal, only the two
  dials the slice named. (b) Ceres colony `default-design-factory` amount **0 → 1** (the DEV lane's data request folded
  into P3.1 per CAMPAIGN-PLAN §4) — Ceres can now manufacture (component/installation construction), not just mine+refine.
- NEW `Pulsar4X/Pulsar4X.Tests/EfUmfGovernmentAndCeresFactoryTests.cs` — the load-only gauge (3 tests), lands in the
  `rest` shard.

**Tests added + what they assert (`EfUmfGovernmentAndCeresFactoryTests`, 3 tests, shared `[OneTimeSetUp]` sandbox load):**
- `Umf_LoadsWith_MilitarismHigh_AndAuthorityHigh` — the UMF's `GovernmentDB` reads `Militarism == High` AND
  `Authority == High` from the authored node; `WarMoraleFactor() > 0` (a militarist takes pride) while a fresh all-Mid
  default `WarMoraleFactor() < 0` (the sign the node flips — the A3 collapse driver).
- `Umf_MilitaristAtWar_LegitimacyWarTerm_IsPositive` — precondition `DiplomacyDB.IsAtWarWithAnyone()` (opening war), then
  drives the real `LegitimacyProcessor.RecalcLegitimacy` on a UMF province and asserts `Factors["war"] > 0` AND
  `== WarMoraleFactor() × LegitimacyDB.MaxWarSwing` (= +10). The end-to-end proof that a militarist-at-war reads a
  POSITIVE war term, not the −5 that triggered the phantom rebellion.
- `Ceres_HostsAManufacturingCapableLine` — finds the Ceres colony (planet `GetDefaultName().Contains("Ceres")`) and
  asserts its `IndustryAbilityDB` has a production line offering `component-construction` or `installation-construction`
  (the factory's industry types) with rate > 0 — distinct from the refinery's `refining`-only line, so it truly proves
  the added factory. BaseModIntegrity-style: the line falls out of the real `AddComponent → IndustryAtb` install at load.

**Byte-identity claim: (b) provably inert absent new data.**
- The CODE change (the `"government"` parser) is inert for every scenario file with no `"government"` node: the parser
  is skipped and the default all-Mid `GovernmentDB` from `CreateFaction` stands (identical to today). Every existing
  scenario file (earth.json / uef-devtest.json / kithrin.json) has no such node → byte-identical. (Even an authored
  all-Mid node reproduces the default `GovernmentDB` exactly.) No flag, no scheduling, no serialization change; a new
  save embeds the same `GovernmentDB` type it already did.
- The DATA change (umf.json) is the INTENDED behaviour change (the point of the slice — the A3/A6 UMF fixes). It only
  affects the UMF *scenario* faction, loaded via the scenario/menu path — NOT the CI economy/ground harness
  (`TestScenario.CreateWithColony` uses `CreateBasicFaction`, not `LoadFromJson`), so those suites are byte-identical.
  The scenario-loading sibling tests were audited: none hard-assert a UMF morale/economy number that Authority/
  Militarism High would flip (`FactionSelfSufficiencyReadoutTests`/`CampaignClockReadoutTests`/`NpcFleetReadyToSailTests`
  assert structural truths; `NPCActingSensorTests` only benefits — the UMF is no longer locked into Defend), and
  `DevTestScenarioTests` keys the UMF off `Colonies.Count >= 4` (still 4 — Ceres stays a colony, just gains a factory).

**FLAGGED balance numbers (developer sets balance — 3 data choices, JSON so no `//` comment carrier):**
- umf.json `government.militarism = "High"` — the load-bearing A3 dial (war term +10). Chosen because the slice named it.
- umf.json `government.authority = "High"` — the "fitting Authority" (matches the UMF's `authoritarianism 0.7`
  personality). Economy/Openness left Mid — the developer may dial these (e.g. Economy High → the "Military Junta"
  iconic government) later.
- umf.json Ceres `default-design-factory` amount `1` — one manufacturing line on Ceres (the A6 data gap fix).

**Developer decisions raised:** the two `government` dial choices above (Militarism High is required by the fix;
Authority High + leaving Economy/Openness Mid is the reviewable "fitting" choice), and the Ceres factory count (1).
All reviewable — the developer can retune the regime or the factory count without any code change.

**Cross-lane note (DEV → CORE request satisfied):** the Ceres factory (`default-design-factory` 0→1) was the DEV lane's
data request, folded into P3.1 because `umf.json` is CORE-owned (CAMPAIGN-PLAN §4). No further DEV action needed.
P0.2's cross-slice note (this file, "Cross-slice note → DEV lane (D2) and P3.1") anticipated this — the
`FactionSelfSufficiencyReadoutTests` readout is the standing before/after gauge for the new Ceres industry line.

**Parallel-safety note (CLAUDE.md rows placed here, NOT edited in the shared files):** `GameEngine/Factions/CLAUDE.md`
and `Pulsar4X.Tests/CLAUDE.md` are high-collision targets (concurrent P3 CORE siblings P3.2/3.3/3.4 touch the
Legitimacy/ObjectiveTransition/ConquerResolver/DefendResolver/NPCDecisionProcessor rows; the DEV lane touches the
ExpandResolver row; every lane adds to the Tests inventory). Per CAMPAIGN-PLAN §2.4 the two pending rows below land at
P8.2 instead of being edited mid-flight.

### Pending row — `GameEngine/Factions/CLAUDE.md` (the `FactionFactory.cs` file-map row)
Append to the `FactionFactory.cs` row: **"+ P3.1 AUTHORED GOVERNMENT (Operation Earthfall, 2026-07-18):** `LoadFromJson`
reads an optional `"government"` node (`authority`/`economy`/`openness`/`militarism` → `GovNotch` Low/Mid/High) via the
public `GovernmentFromJson(JToken)` helper and `SetDataBlob`-replaces the default all-Mid `GovernmentDB` `CreateFaction`
attaches. Byte-identical with no node (the all-Mid default stands). A3 fix seam 1: the UMF now authors Militarism High
so its legitimacy war term is +10 (pride) not −5 (collapse). Gauge: `EfUmfGovernmentAndCeresFactoryTests`.**" Also add
`"government"` to the list of keys `LoadFromJson` reads.

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
| `EfUmfGovernmentAndCeresFactoryTests` | **P3.1 — the UMF government + Ceres factory data gauge (Operation Earthfall, findings A3+A6).** Loads the DevTest sandbox and asserts (1) the UMF's authored `"government"` node lands Militarism High + Authority High on its `GovernmentDB` (and a militarist yields a POSITIVE `WarMoraleFactor` where all-Mid is negative); (2) a militarist-at-war UMF province computes a POSITIVE legitimacy war term via the real `LegitimacyProcessor.RecalcLegitimacy` (`Factors["war"]` = +10 = `WarMoraleFactor 0.5 × MaxWarSwing 20`, not the all-Mid −5 that triggered the A3 phantom rebellion → 180-day Defend lock); (3) Ceres now hosts a manufacturing-capable industry line (component/installation construction from the authored `default-design-factory` 0→1), where before it could only mine+refine (A6 data gap). Load-only (no clock advance) → `rest` shard. |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P3.1 `EfUmfGovernmentAndCeresFactoryTests`** · *what:* proves the UMF's authored government (Militarism/Authority
  High) + a positive militarist-at-war legitimacy war term + the Ceres manufacturing line, all via the real
  scenario-load path · *why:* A3 found the UMF ran the all-Mid default (war term −5) → a hostile-world morale trough
  collapsed a province into a phantom rebellion → 180-day Defend lock; A6 found Ceres had factory ×0 (couldn't
  manufacture) · *method:* load the DevTest sandbox, read the UMF `GovernmentDB` + drive `RecalcLegitimacy` on a UMF
  province + inspect Ceres' `IndustryAbilityDB` production lines · *right-looks-like:* Militarism/Authority High,
  `Factors["war"] > 0` (== +10), a Ceres line with component/installation-construction rate > 0 · *likely-failure:* the
  `"government"` parser regresses (dials read Mid → war term goes negative), or the Ceres factory reverts to 0 (no
  manufacturing line) · *mitigation:* this gauge + the byte-identity audit of the sibling scenario tests · *unblocks:*
  the A3 objective-flip fix (a militarist stays on the offensive instead of collapsing) and the A6 Ceres self-
  sufficiency gap; the standing `FactionSelfSufficiencyReadoutTests` readout shows the new Ceres line over 120 days.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `FactionFactory.LoadFromJson` (`"government"` node) → `GovernmentDB` (regime dials) → `LegitimacyProcessor.WarTermFor`
  (`WarMoraleFactor × MaxWarSwing`) → `LegitimacyDB` war term → `RebellionDB`/`NeedsLadder`/`ObjectiveSelector` (the A3
  chain): authoring Militarism High makes a militarist-at-war province's war term positive, breaking the
  morale-trough → phantom-rebellion → Defend-lock cascade at its root.

---

## P3.2 — Rebellion debounce + fresh-morale legitimacy (Operation Earthfall, findings A3 seams 2-3)

**Files changed (all in-fence — CORE owns `GameEngine/Colonies/Legitimacy*` + `PopulationProcessor.cs`):**
- `GameEngine/Colonies/LegitimacyDB.cs` — added `[JsonProperty] int ConsecutiveCollapsingReads` (the debounce counter)
  + its copy-ctor line (save-safe: persisted + deep-copied per the slice mandate).
- `GameEngine/Colonies/LegitimacyProcessor.cs` — two default-off static flags (`ReadCurrentMorale`,
  `EnableRebellionDebounce`) + `const RebellionDebounceReads = 2`; `RecalcLegitimacy` now reads current-cycle morale
  via `PopulationProcessor.ComputeCurrentMorale` when `ReadCurrentMorale` is on (else the old field read);
  `UpdateRebellion` gained a debounce-aware 4-arg overload (the 3-arg one delegates to it with `legitDb = null`, so
  existing RebellionTests are byte-identical).
- `GameEngine/Colonies/PopulationProcessor.cs` — added `internal static double ComputeCurrentMorale(Entity)`: a pure,
  no-mutation reader that gathers the SAME morale inputs `GrowPopulation` uses and feeds the identical
  `ColonyMoraleDB.ComputeMorale`. `GrowPopulation` itself is UNTOUCHED (the population sim stays byte-identical); the
  two input-gatherings are kept in sync by a loud comment on both.
- `Pulsar4X.Tests/LegitimacyStaleEchoDebounceTests.cs` — NEW fixture (lands in the `rest` shard).

**Verification of the ordering question (the "verify FIRST" mandate, why option 2 was chosen over 1 and 3):**
The two monthly hotloops are keyed to DIFFERENT DataBlobs (`LegitimacyProcessor`→`LegitimacyDB`,
`PopulationProcessor`→`ColonyInfoDB`) at the same 30-day frequency/offset, so both fire in the SAME sub-step on the
shared boundary (`ManagerSubPulse.ProcessToNextInterupt:349` foreach over `HotLoopProcessorsNextRun`). Their RELATIVE
order is the insertion order of `HotLoopProcessorsNextRun`, seeded from `ProcessorManager.HotloopProcessors`, populated
by iterating `Assembly.GetTypes()` (`ProcessManager.CreateProcessors:110-118`) — an order the CLR leaves UNSPECIFIED.
So **option 1 (order legitimacy after population) is not reliably achievable** and would require editing `ManagerSubPulse`
/`ProcessManager`, both OUTSIDE this slice's fence. **Option 3 (smooth legit rate-of-change)** is a band-aid that makes
legit permanently LAG morale (the opposite of "read current") and still needs a flag for byte-identity. **Option 2
(RecalcLegitimacy reads the same current-cycle morale)** is order-INDEPENDENT, structurally kills the lag, and stays
entirely in-fence — chosen.

**BYTE-IDENTITY CLAIM: (a) default-off flags.** Both behaviour changes are gated by `ReadCurrentMorale = false` and
`EnableRebellionDebounce = false` (defaults). With both off, `RecalcLegitimacy` reads `ColonyMoraleDB.Morale` and
`UpdateRebellion` triggers on a single sample — the exact shipped behaviour; every existing LegitimacyProcessorTests /
RebellionTests / MoraleTests / economy assertion is unchanged. The new persisted `ConsecutiveCollapsingReads` field is
additive + defaulted 0 and is never read or written while the debounce flag is off (inert; no save asserts a
LegitimacyDB's exact JSON — `SaveLoadDesignRoundTripTests` checks ComponentDesigns, not colony blobs). `GrowPopulation`
is untouched, so `ComputeCurrentMorale` (only invoked by legitimacy under the flag) cannot perturb the population sim.

**FLAGGED balance number:** `LegitimacyProcessor.RebellionDebounceReads = 2` (`// FLAGGED balance value` in source) —
the number of consecutive monthly collapsing reads a rebellion needs under the debounce. The developer sets the depth.

**Developer decision raised (flag flip):** both flags ship OFF for byte-identity; the A3 game fix only takes effect once
they are flipped ON. Per CAMPAIGN-PLAN, a later CORE integration slice (PW/P8) or a client/DevTools toggle turns them
on — flagging here so the flip isn't forgotten. (No value change; the fix is proven under the flags-on tests below.)

**Parallel-safety note (CLAUDE.md rows placed here, NOT edited in the shared files):** `GameEngine/Colonies/CLAUDE.md`
and `Pulsar4X.Tests/CLAUDE.md` are collision targets (concurrent P3 CORE siblings + every lane adds to the Tests
inventory). Per CAMPAIGN-PLAN §2.4 the pending rows below land at P8.2.

### Pending row — `GameEngine/Colonies/CLAUDE.md` (file-map rows)
- Append to the `LegitimacyDB.cs` row: **"+ P3.2 (Operation Earthfall A3): added the save-safe debounce counter
  `ConsecutiveCollapsingReads` (consecutive monthly collapsing reads; inert unless `LegitimacyProcessor.EnableRebellionDebounce`)."**
- Append to the `LegitimacyProcessor.cs` row: **"+ P3.2 (Operation Earthfall A3, default-off): `ReadCurrentMorale`
  recomputes THIS cycle's morale via `PopulationProcessor.ComputeCurrentMorale` instead of the one-cycle-stale
  `ColonyMoraleDB.Morale` field (kills the A3 stale echo, order-independent — the two same-frequency hotloops run in
  `Assembly.GetTypes()` order); `EnableRebellionDebounce` requires `RebellionDebounceReads` (2) consecutive collapsing
  reads before beginning a rebellion (no more one-sample revolts). Both default OFF → byte-identical. Gauge:
  `LegitimacyStaleEchoDebounceTests`."**
- Append to the `RebellionDB.cs` row: **"+ P3.2: `UpdateRebellion` gained a debounce-aware overload driven by the new
  `LegitimacyDB.ConsecutiveCollapsingReads` (flag-gated, byte-identical off)."**
- Append to the `PopulationProcessor.cs` row: **"+ P3.2: `ComputeCurrentMorale(Entity)` — a pure no-mutation reader of
  the current-cycle morale (same inputs as `GrowPopulation`, same `ColonyMoraleDB.ComputeMorale`); consumed by
  `LegitimacyProcessor` to read fresh morale. `GrowPopulation` untouched → population sim byte-identical."**

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
| `LegitimacyStaleEchoDebounceTests` | **P3.2 — the A3 rebellion-debounce + fresh-morale-legitimacy gauge (Operation Earthfall, findings A3 seams 2-3).** Pure: under `EnableRebellionDebounce`, a transient dip (one collapsing read then recovery, counter reset) never rebels while a SUSTAINED collapse rebels on the `RebellionDebounceReads`-th consecutive read; flag-off, a single sample still rebels (byte-identical). Harness (`CreateWithColony`): with `ReadCurrentMorale` on, a stale low `ColonyMoraleDB.Morale` field no longer collapses legitimacy (reads the hospitable colony's healthy current morale — no legit cliff), and a GENUINE sustained collapse (famine + blackout + max tax drive current morale to the floor) still crashes legitimacy AND begins a rebellion on the 2nd consecutive read. All flags restored in `finally` (no static-flag leak). |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P3.2 `LegitimacyStaleEchoDebounceTests`** · *what:* proves (a) a rebellion needs 2 consecutive collapsing reads
  under `EnableRebellionDebounce` (a one-month dip is ignored, a sustained collapse still rebels) and (b) legitimacy
  reads THIS cycle's morale under `ReadCurrentMorale` (a stale morale field no longer prints a legit cliff) · *why:* A3
  found a transient hostile-world morale trough → a one-cycle-stale legit echo (legit 15 a month after morale
  recovered) → a rebellion begun on a SINGLE sample → the UMF locked into a 180-day Defend at the Survive floor ·
  *method:* drive `UpdateRebellion` with injected collapse/recovery sequences (pure) + drive `RecalcLegitimacy` on a
  real colony with a stale field vs. a real famine (harness) · *right-looks-like:* transient → no rebellion + no
  collapse under fresh-morale; sustained → collapse + rebellion on the 2nd read · *likely-failure:* a flag defaults
  on (breaks byte-identity), or `ComputeCurrentMorale` drifts from `GrowPopulation`'s morale inputs · *mitigation:*
  the flags-off byte-identity assertions + the loud keep-in-sync comment on both morale gatherings · *unblocks:* the A3
  objective-flip fix (no phantom rebellion → the AI stays on its committed Conquer instead of collapsing to Defend).

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `PopulationProcessor.ComputeCurrentMorale` → `LegitimacyProcessor.RecalcLegitimacy` (under `ReadCurrentMorale`) →
  `LegitimacyDB.Legitimacy`: legitimacy now reads the SAME current-cycle morale the population sim computes, instead of
  the one-cycle-stale `ColonyMoraleDB.Morale` field the two same-frequency (`Assembly.GetTypes()`-ordered) hotloops
  otherwise race on.
- `LegitimacyProcessor.UpdateRebellion` (debounce) → `LegitimacyDB.ConsecutiveCollapsingReads` → `RebellionDB.IsRebelling`
  → `NeedsLadder`/`ObjectiveSelector` (the A3 chain): a rebellion now requires `RebellionDebounceReads` consecutive
  collapsing reads, so a transient legitimacy trough no longer trips the Survive-floor Defend lock.

---

## P3.3 — Hysteresis break-glass (Operation Earthfall, findings A3 seam 4 + the critical Survive-floor bug)

**The problem (findings/A3-objective-flip.md §Root-cause step 7):** a crisis commit was stamped at `Tier=Survive`
(enum 0, the floor) via `NPCDecisionProcessor.cs:341`, so `ShouldReplan`'s only break-glass — `proposedTier < currentTier`
— was **mathematically unreachable** (nothing is more urgent than the floor). Result: a one-month phantom rebellion
locked the UMF into Defend for the full 180-day `DefaultCommitFor`, ignoring ~50 daily Conquer reads. This slice adds
three crisis break-glasses, none of which reuse the dead tier compare.

**Files changed (all inside the CORE fence except StrategicObjectiveDB.cs — see the note below):**
- `Pulsar4X/GameEngine/Factions/ObjectiveTransition.cs` (CORE fence, primary) —
  - NEW `[Flags] enum CrisisTrigger` (Rebellion / MoraleCollapse / LegitimacyCollapse / LosingWar — the Survive-rung
    conditions; AtWar / Insolvent / MoraleUnhealthy / LegitimacyUnhealthy — the Stabilize-rung ones). Explicit bit
    values, only-appended → save-safe.
  - NEW `CrisisCommitFor` (60d, FLAGGED) + `ContradictionReleaseCycles` (14, FLAGGED).
  - `CommitFor` gained a **crisis branch** (a): Defend/Consolidate → `CrisisCommitFor` (shorter dwell). Expansion aims
    (Expand/Conquer) keep the Ambition-scaled dwell UNCHANGED; every other objective keeps `DefaultCommitFor`.
  - `ShouldReplan` gained two optional `bool` params (`triggerCleared` b / `contradictionReleased` c, default false →
    every pre-P3.3 4-arg caller byte-identical).
  - `Advance` gained an optional `CrisisTrigger currentTriggers` param (default None → pre-P3.3 6-arg callers
    byte-identical): for a crisis commit already in place it counts the **upward** contradiction streak
    (`proposedTier > committedTier` — the reachable mirror of the dead compare), computes trigger-cleared
    (`(CommitTrigger & currentTriggers) == None`), and records the tier-masked trigger on a fresh crisis commit.
  - NEW helpers `IsCrisisObjective`, `CrisisTriggersFrom` (public; reuses NeedsLadder's own thresholds so the two never
    drift), `CrisisMaskFor` (private).
- `Pulsar4X/GameEngine/Factions/StrategicObjectiveDB.cs` (**NOT explicitly in the fence list, but CORE-domain and owned
  by no other lane** — see parallel-safety note) — two save-safe appended `[JsonProperty]` fields: `CommitTrigger`
  (`CrisisTrigger`, default None) + `ContradictionCycles` (int, default 0), both added to the deep-copy ctor.
- `Pulsar4X/GameEngine/Factions/NPCDecisionProcessor.cs` (CORE fence) — `UpdateStrategicObjective` now computes
  `currentTriggers` via `ObjectiveTransition.CrisisTriggersFrom(...)` off the same gauges it already reads
  (`FactionRollup.MeanMorale/MeanLegitimacy/Balance` + the WarStanding/InRebellion it already had) and passes it to
  `Advance`. (Refactor: `FactionRollup.MilitaryStrength` hoisted into a local `ownStrength` — same value.)
- `Pulsar4X/Pulsar4X.Tests/AmbitionCadenceTests.cs` (**existing test edited** — the intentional behaviour change
  invalidated one assertion) — the line pinning `CommitFor(Defend, extreme) == DefaultCommitFor` now asserts
  `== CrisisCommitFor` (+ an ambition-invariance line at neutral). No sibling slice touches this file.
- NEW `Pulsar4X/Pulsar4X.Tests/EfHysteresisBreakGlassTests.cs` — the P3.3 gauge (lands in the `rest` shard).

**Tests added + what they assert (`EfHysteresisBreakGlassTests`, 9 tests, pure — drives `ObjectiveTransition` directly):**
- `CrisisObjective_HoldsShorterDwell_ExpansionAndPeacefulUnchanged` — (a): Defend/Consolidate → `CrisisCommitFor` (<
  Default); Conquer/Expand → the Ambition default; GrowEconomy/AdvanceTech/None → Default (byte-identity tripwire).
- `SurviveFloorCommit_DownwardBreakGlassIsUnreachable_ButTheP33ReleasesAreNot` — the CRITICAL BUG pinned: for a
  Survive-floor commit, `ShouldReplan` returns FALSE for every proposed tier before expiry (the dead compare), but
  TRUE with `triggerCleared`/`contradictionReleased` (the reachable P3.3 paths).
- `TriggerCleared_ReleasesTheDefendCommit_Early` — (b): a Defend commit records its Survive-rung triggers; once the
  rebellion quells + morale/legit recover (only a non-mask AtWar flag remains) it releases to Conquer; a persisting
  trigger holds.
- `PersistentContradiction_ReleasesAfterN_Cycles` — (c), isolated (commit with no recorded trigger → (b) off): held
  for N-1 higher-tier cycles (counter climbing), released on the N-th.
- `Contradiction_ResetsOnANonConsecutiveRead` — a flickering read restarts the streak (no spurious release).
- `LogScenario_OneMonthRebellionThenQuell_ReturnsToConquerWithinDays` — **the A3 log's exact scenario**: Conquer →
  1-month rebellion → Defend → quell → daily Conquer reads → returns to Conquer within ≤2 days (was a 180-day lock).
- `SustainedCrisis_HoldsDefend_ThroughoutTheCrisis` — the complement: a persistent Survive crisis holds Defend across
  120 days (past a 60-day re-commit), never abandoning it; the contradiction counter stays 0.
- `NonCrisisCommit_IgnoresTheCrisisBreakGlass` — a GrowEconomy commit records no trigger, never advances the counter,
  never releases early (crisis break-glass is crisis-only — the non-crisis byte-identity guard).
- `CrisisTriggersFrom_MirrorsTheNeedsLadderPredicates` — the flag builder reuses NeedsLadder's thresholds (survive /
  healthy / stabilize cases).

**BYTE-IDENTITY CLAIM: (b) provably inert absent new data — with ONE honest, confined, intentional exception.**
- The two new `StrategicObjectiveDB` fields are additive save-safe `[JsonProperty]` defaulting to inert values
  (None / 0); old saves load unchanged; the deep-copy ctor copies them.
- The `ShouldReplan`/`Advance` new params default to None/false, so every pre-P3.3 caller (the 4-arg + 6-arg test
  callers) is byte-identical. The two release paths (b)/(c) fire ONLY for a crisis-response commit with a recorded
  trigger / a sustained upward contradiction — absent a crisis they never run; the contradiction counter only ticks
  for a crisis commit. `ObjectiveTransitionTests` + `NPCObjectiveTickTests` were traced call-by-call and stay green.
- The AI's player-visible actions remain default-off (`EnableOrderEmission` etc.), so no scenario/economy/ground test
  changes; the objective-SETTLING that calls `Advance` is always-on but only writes the (unread-by-orders) DB.
- **THE EXCEPTION (not a flag, and not strictly "absent new data"):** break-glass (a) shortens the crisis-response
  dwell (Defend/Consolidate) from 180d to 60d in EVERY game — this is the point of the slice. Its entire test-suite
  blast radius is ONE assertion in `AmbitionCadenceTests` (which directly pinned the old Defend=180d), updated in the
  same change. No other existing test asserts a crisis-commit dwell (grep-verified: the only `CommitFor(Defend/…)`
  assertions live in `AmbitionCadenceTests`; `NPCObjectiveTickTests` uses a healthy Thrive faction). So the change is
  inert to every existing green test except the one that encoded the behaviour being changed.

**FLAGGED balance numbers (2, both carry `// FLAGGED balance value`):**
- `ObjectiveTransition.CrisisCommitFor = 60 days` — the shorter crisis-commit dwell (slice suggested 60d).
- `ObjectiveTransition.ContradictionReleaseCycles = 14` — the N consecutive daily higher-tier reads that release a
  crisis commit (slice suggested 14 daily reads).

**Developer decisions raised:**
- Both FLAGGED numbers above are the developer's to tune (crisis dwell length; contradiction debounce depth).
- **Scope choice (reviewable):** "crisis objective" = **Defend AND Consolidate** (findings A3 seam 4 names both), not
  just Defend/Survive. A war-footing **Conquer picked from the Survive tier is deliberately NOT a crisis objective** —
  it keeps the Ambition-scaled dwell (the slice's "Non-crisis (Conquer/Expand) behavior unchanged"), so an aggressive
  faction pressing its war from a strained-but-winning position is untouched. If the developer wants Consolidate
  excluded (Defend-only), it's a one-line change to `IsCrisisObjective`.

**Parallel-safety note (subsystem CLAUDE.md routed here, NOT edited in the shared file):** `GameEngine/Factions/CLAUDE.md`
and `Pulsar4X.Tests/CLAUDE.md` are high-collision targets (sibling P3 CORE slices P3.1/3.2/3.4 + P4.x touch the
ObjectiveTransition / NPCDecisionProcessor / Conquer/Defend-Resolver rows and the Tests inventory). Per CAMPAIGN-PLAN
§2.4 the pending rows below land at P8.2. **`StrategicObjectiveDB.cs` was edited directly** (not routed) because it is
not in ANY lane's fence list and is squarely CORE objective-machinery — the two new fields are load-bearing save-safe
state this slice cannot live without, and no sibling lane touches Factions source files. Flagging it here for transparency.

### Pending row — `GameEngine/Factions/CLAUDE.md` (file-map rows)
- Replace the `ObjectiveTransition.cs` row body with: **"Built (Phase 2.3). The hysteresis engine that stops the brain
  thrashing. + P3.3 (Operation Earthfall, findings A3): three CRISIS break-glasses so a crisis-response commit
  (Defend/Consolidate) can't lock the brain the way a one-month phantom rebellion pinned the UMF on Defend for 180 days
  at the Survive floor — (a) crisis objectives hold the shorter `CrisisCommitFor` (60d), (b) `Advance` releases the
  commit the instant the `CrisisTrigger` that FORCED it clears (a condition read, not the `proposedTier<currentTier`
  compare that's DEAD at the Survive floor), (c) release after `ContradictionReleaseCycles` (14) consecutive
  higher-tier proposals (the reachable upward mirror). Non-crisis expansion aims unchanged. Gauge:
  `EfHysteresisBreakGlassTests`."**
- Append to the `StrategicObjectiveDB.cs` row: **"+ P3.3: two save-safe appended fields — `CommitTrigger`
  (`CrisisTrigger` flags of what forced a crisis commit, masked to its tier) + `ContradictionCycles` (the upward
  contradiction debounce counter). Both default inert (None/0), copied in the deep-copy ctor."**

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
| `EfHysteresisBreakGlassTests` | **P3.3 — the crisis hysteresis break-glass gauge (Operation Earthfall, findings A3).** Pure (drives `ObjectiveTransition` directly): (a) a crisis objective (Defend/Consolidate) holds the shorter `CrisisCommitFor` while expansion/peaceful aims keep their dwell; the CRITICAL BUG pinned — a Survive-floor commit is unreachable by the `proposedTier<currentTier` break-glass but IS released by the two P3.3 paths; (b) a Defend commit releases the instant its recorded `CrisisTrigger` clears (rebellion quelled) and holds while it persists; (c) N consecutive higher-tier proposals release it (isolated from b), a non-consecutive read resets the streak; **the log's exact scenario** — 1-month rebellion → quell → daily Conquer reads returns to Conquer within ≤2 days (was a 180-day lock) — and its complement, a sustained crisis holds Defend across 120 days; a non-crisis commit is untouched (byte-identity guard); and `CrisisTriggersFrom` mirrors the NeedsLadder predicates. |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P3.3 `EfHysteresisBreakGlassTests`** · *what:* proves the three crisis break-glasses release a crisis commit
  (shorter dwell / trigger-cleared / contradiction-debounce) while a genuine sustained crisis still holds Defend, and
  pins the Survive-floor unreachability bug · *why:* A3 found a one-month phantom rebellion locked the UMF into Defend
  for 180 days because the commit stamped Tier=Survive made the only break-glass (`proposedTier<currentTier`)
  unreachable · *method:* drive `ObjectiveTransition.Advance`/`ShouldReplan`/`CommitFor` directly with the log's
  commit→rebellion→quell→daily-Conquer sequence and the sustained-crisis complement · *right-looks-like:* returns to
  Conquer within ≤2 days of the quell; holds Defend across 120 sustained-crisis days; non-crisis commit untouched ·
  *likely-failure:* a future edit re-couples (b)/(c) to the dead tier compare (Survive-floor lock returns), or the
  crisis dwell/contradiction constant is mistuned · *mitigation:* this gauge + the non-crisis byte-identity test ·
  *unblocks:* the A3 objective-flip fix (the AI abandons a passed crisis and resumes its committed war), and P3.4
  operation-continuity (which assumes the objective can flip back off Defend once the crisis clears).

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `NeedsLadder` (crisis predicates) → `ObjectiveTransition.CrisisTriggersFrom` → `StrategicObjectiveDB.CommitTrigger`
  (recorded at commit) → `ObjectiveTransition.Advance` (trigger-cleared + contradiction releases) → the settled
  `StrategicObjective`: a crisis-response commit (Defend/Consolidate) now unwinds when the condition that forced it
  clears (or the ladder contradicts it for 14 cycles, or the shorter 60d dwell expires), instead of locking at the
  Survive floor for 180 days — the A3 objective-flip fix, feeding the P3.4 fleet-recall / operation-continuity work.

### Cross-lane requests: NONE — every production edit was inside the CORE fence except `StrategicObjectiveDB.cs`
(CORE-domain, owned by no other lane; see the parallel-safety note above).

---

## P3.4 — Operation continuity: never orphan an invasion (Operation Earthfall, findings A3 §D + seam 5)

**The two guards (findings/A3-objective-flip.md §Commitment gap + Ranked fix seam 5):**
(a) A WINNING in-flight Conquer must SURVIVE a transient internal wobble (the A3 phantom-rebellion / hostile-world
morale trough) instead of being hijacked to Defend and leaving the strike fleet coasting at the enemy with no one
driving it. (b) A GENUINE flip to Defend must actively RECALL in-flight offensive fleets home, not just re-posture.

**Files changed (all inside the CORE fence):**
- `Pulsar4X/GameEngine/Factions/ObjectiveTransition.cs` — NEW pure predicate `ShouldProtectInFlightConquest(committedObjective,
  proposedTier, atWarAndWinning, currentTriggers, homelandInvaded, hasInFlightStrikeFleet)` (six gates: committed==Conquer ·
  fleet in transit · at war & winning · proposal==Survive floor · NOT homeland-invaded · NOT LosingWar · the Survive is an
  INTERNAL shock = Rebellion|MoraleCollapse|LegitimacyCollapse). `ShouldReplan` gained an optional `bool protectCommit = false`
  (7th arg) that SUPPRESSES the `proposedTier < currentTier` preempt (`&& !protectCommit`) — expiry + the P3.3 break-glasses
  still apply, so a protected commit still re-plans on genuine expiry. `Advance` gained an optional `bool protectCommit = false`
  (8th arg) forwarded into `ShouldReplan`. Both new params default false → every pre-P3.4 4-arg `ShouldReplan` / 6-arg `Advance`
  caller (incl. `ObjectiveTransitionTests`, `EfHysteresisBreakGlassTests`) is byte-identical.
- `Pulsar4X/GameEngine/Factions/NeedsLadder.cs` — NEW `public static bool HomelandInvaded(Entity)` (the entity read for gate 5):
  scans each colony's body `GroundCombat.GroundForcesDB` for a ground unit owned by another (non-neutral) faction → an enemy on
  home soil. Read-only, defensive; co-located with the sibling `InRebellion` scan.
- `Pulsar4X/GameEngine/Factions/ConquerResolver.cs` — NEW `internal static bool HasFleetInTransit(Entity)` (the entity read for
  gate 2): any owned `Fleets.FleetDB` with a ship in warp transit (reuses the private `FleetIsMoving`, `WarpMovingDB`). Under a
  committed Conquer that moving fleet is the invasion en route.
- `Pulsar4X/GameEngine/Factions/NPCDecisionProcessor.cs` — `UpdateStrategicObjective` now computes `protectInFlightConquest`
  (cheap tier/war gates short-circuit the two entity scans → they run ONLY in the narrow war-crisis case) and passes it to
  `Advance`; the `DecisionReason` gains a "protecting in-flight conquest (winning war, transient internal crisis)" line for the
  flight-recorder (observability, no behaviour change).
- `Pulsar4X/GameEngine/Factions/DefendResolver.cs` — NEW **Rung 0 (RECALL)**, placed before Rung A (build) / Rung B (posture):
  when a home colony exists, the first owned fleet that `IsOutbound` (a ship in warp transit toward a body NOT in the faction's
  own-colony-body set) is recalled with `Movement.MoveToSystemBodyOrder.CreateCommand(factionId, fleet, homeBody)` (`Kind="RecallFleet"`).
  Helpers `HomeBody` / `OwnColonyBodyIds` / `IsOutbound`. v1 same-system (MoveToSystemBodyOrder warps within a system); a fleet
  already warping home / between our own worlds / on a target-less warp is skipped (no re-issue churn). Added `using System.Collections.Generic;`
  + `using Pulsar4X.Engine;`.
- NEW `Pulsar4X/Pulsar4X.Tests/EfOperationContinuityTests.cs` — the P3.4 gauge (unique fixture name; lands in the `rest` shard).

**Verified against source (no SDK in container):** `MoveToSystemBodyOrder.CreateCommand(int factionId, Entity commandingEntity,
Entity targetEntity)` signature + `.Target` getter; `WarpMovingDB.TargetEntity` (internal `Entity?` field, same-assembly / test-visible);
`FleetCombat.Ships(Entity)` (public, recursive, cycle-safe); `GroundForcesDB.Units` (public getter) + `GroundUnit.FactionOwnerID`;
`ColonyInfoDB.PlanetEntity`; `Game.NeutralFactionId` (-99); `FleetFactory.Create` attaches an `OrderableDB`; `TreeHierarchyDB.AddChild`
(internal, test-visible); `NeedTier` (Survive=0 … Ambition=3); `PlannerAction` (public Kind/Detail/Execute); `OrderableProcessor`
keeps a non-finished order in `ActionList` (a FlagShipID==-1 fleet's `MoveToSystemBodyOrder` early-returns → not finished → retained).

**Tests added + what they assert (`EfOperationContinuityTests`, 8 tests, drive the pure decision + resolvers directly — no sim advance):**
- `ShouldProtectInFlightConquest_EachGateBites` — all six gates met → protected; each gate individually flipped → NOT protected.
- `ProtectedCommit_HoldsThroughTransientWobble_ButNotExpiry` — `ShouldReplan`: a Survive proposal preempts a higher-tier Conquer
  WITHOUT protection, is HELD WITH it, and still re-plans on genuine expiry even protected.
- `Advance_ProtectedConquerHolds_UnprotectedPreemptsToDefend` — a protected transient wobble HOLDS Conquer; the same wobble
  unprotected preempts to Defend (the genuine-crisis / recall path).
- `HomelandInvaded_TrueOnlyWithAForeignUnitOnAHomeWorld` — false with no foreign unit / own start garrison; true once a reds
  `GroundUnit` is placed on the home world's `GroundForcesDB`.
- `HasFleetInTransit_TrueOnlyWithAMovingOwnedFleet` — false for the parked start fleets; true once a fleet has a ship carrying `WarpMovingDB`.
- `Defend_RecallsAnInFlightOffensiveFleet_ToHome` — a fleet warping at a non-home body → `DefendResolver` decides `RecallFleet`
  (Detail names the home body), and `Execute` emits a `MoveToSystemBodyOrder` (`.Target == home`) onto the fleet's `OrderableDB.ActionList`.
- `Defend_NoOutboundFleet_FallsToBuildOrPosture` — no in-flight fleet → `QueueWarship`/`SetDefensivePosture`, NOT `RecallFleet` (byte-identity tripwire).
- `Defend_DoesNotRecall_AFleetAlreadyHeadingHome` — a fleet warping toward the HOME body is not recalled (the re-issue guard).

**BYTE-IDENTITY CLAIM: (b) provably inert absent the specific in-flight-conquest configuration.**
- Part (b) — DefendResolver recall — is **doubly inert**: it runs only inside the `EnableOrderEmission`-gated `EmitOrders` (a
  **default-off flag**, so a default game never reaches it), AND it no-ops unless the faction has an OUTBOUND owned fleet + a home
  colony. Existing `DefendResolverTests` (parked start fleets, no transit) → Rung 0 skipped → still `QueueWarship`/`SetDefensivePosture`.
- Part (a) — the protection — rides the ALWAYS-ON objective settle (not flag-gated), so this is the honest **(b) inert-absent-data**
  case (like P3.3's break-glass a): it only changes the settled objective when a faction is committed to Conquer AND at war and
  winning AND reading a Survive-internal tier AND has a fleet in transit AND its homeland isn't invaded — a configuration no existing
  test creates. `NPCObjectiveTickTests` (a healthy Thrive/GrowEconomy NPC) short-circuits at `objective.Objective == Conquer` → the
  two entity scans never run. `NorthStarAcceptanceTests` pins GrowEconomy/AdvanceTech/Expand (no Conquer faction). `NPCActingSensorTests`
  asserts "an order was emitted" — protection HOLDS Conquer (which emits conquest orders), so it can only help, never break, that gauge.
  The new `ShouldReplan`/`Advance` params default false → every pre-P3.4 caller is byte-identical.

**FLAGGED balance numbers:** NONE. P3.4 adds no new gameplay/tuning number — the protection is a pure gating decision over existing
gauges + flags; the recall reuses the existing `MoveToSystemBodyOrder`. (The test-only `T0`/`InternalShock` are fixture constants.)

**Developer decisions raised (reviewable defaults, per CAMPAIGN-PLAN §6 "recall-vs-press doctrine details"):**
- **Protect scope:** protection fires only for a Conquer committed ABOVE the Survive floor (Ambition/Stabilize war-footing) — a
  Conquer already committed AT Survive is held by plain hysteresis (equal tiers, no preempt) with no change needed. Reviewable.
- **"Homeland invaded" definition:** ANY foreign (non-owned, non-neutral) ground unit on ANY of the faction's colony worlds counts
  as invaded (a conservative "an enemy on home soil = genuine Defend"). Could be narrowed to at-war owners / a threshold of units.
- **Recall target:** the faction's FIRST colony body is "home"; a fleet warping to ANY owned colony body is treated as friendly
  (not recalled). v1 is same-system only (MoveToSystemBodyOrder warps within a system); a cross-system leg-by-leg recall is deferred
  to the same multi-jump routing the Conquer strike defers.
- **Recall breadth:** Rung 0 recalls ANY outbound owned fleet (not only the "strike" fleet); under a genuine Defend crisis pulling
  every sortie home is the intended default. Reviewable if the developer wants only armed fleets recalled.

**Parallel-safety note (subsystem CLAUDE.md + Tests/CLAUDE.md rows ROUTED here, NOT edited in the shared files):**
`GameEngine/Factions/CLAUDE.md` and `Pulsar4X.Tests/CLAUDE.md` are high-collision targets (the P3 CORE siblings P3.1/P3.2/P3.3
above all routed their Factions/CLAUDE.md rows here rather than editing mid-flight). Per CAMPAIGN-PLAN §2.4 the pending rows below
land at P8.2. **`StrategicObjectiveDB.cs` is NOT touched by P3.4** (P3.3 added its fields; P3.4 only reads `.Objective`/`.Tier`).

### Pending rows — `GameEngine/Factions/CLAUDE.md` (append to the existing file-map rows)
- **`ObjectiveTransition.cs`** row — append: **"+ P3.4 (Operation Earthfall, findings A3 seam 5): `ShouldProtectInFlightConquest`
  (pure) + an optional `protectCommit` on `ShouldReplan`/`Advance` that SUPPRESSES the `proposedTier<currentTier` preempt — so a
  WINNING in-flight Conquer HOLDS through a transient internal Survive wobble (phantom rebellion / morale trough) instead of being
  hijacked to Defend; expiry + the P3.3 break-glasses still apply. Defaults false → byte-identical. Gauge: `EfOperationContinuityTests`."**
- **`NeedsLadder.cs`** (the `NeedsLadder.cs` row in the brain-layer table) — append: **"+ P3.4: `HomelandInvaded(Entity)` — a home
  colony world carrying a foreign (non-owned, non-neutral) ground unit (the genuine-external-crisis signal the in-flight-conquest
  protection reads)."**
- **`ConquerResolver.cs`** (the `*Resolver.cs` row) — append: **"+ P3.4: `HasFleetInTransit(Entity)` — any owned fleet with a ship
  in warp transit (the 'invasion en route' signal for the in-flight-conquest protection)."**
- **`DefendResolver.cs`** (the `*Resolver.cs` row) — append: **"+ P3.4: NEW Rung 0 RECALL — a genuine flip to Defend recalls an
  in-flight OFFENSIVE fleet home via `MoveToSystemBodyOrder` (`Kind=RecallFleet`) before building/posturing; skips a fleet already
  heading home / between our own worlds. Byte-identical off (gated `EmitOrders`) and with no outbound fleet."**
- **`NPCDecisionProcessor.cs`** row — append: **"+ P3.4: `UpdateStrategicObjective` protects a winning in-flight Conquer from a
  transient internal Survive wobble via `ObjectiveTransition.ShouldProtectInFlightConquest` (cheap gates short-circuit the two
  entity scans), passed as `protectCommit` to `Advance`; a genuine external crisis (lost war / invaded homeland) still preempts."**

### Pending row — `Pulsar4X.Tests/CLAUDE.md` test inventory table
| `EfOperationContinuityTests` | **P3.4 — operation continuity: never orphan an invasion (Operation Earthfall, findings A3 §D + seam 5).** Drives the pure decision + resolvers directly (no sim advance): (a) `ObjectiveTransition.ShouldProtectInFlightConquest` protects ONLY a winning in-flight Conquer facing a transient INTERNAL Survive wobble (each of six gates bites), and `protectCommit` suppresses `ShouldReplan`/`Advance`'s tier-preempt so the Conquer HOLDS (but still re-plans on genuine expiry); the two entity reads (`NeedsLadder.HomelandInvaded` — a foreign unit on a home world; `ConquerResolver.HasFleetInTransit` — an owned fleet in warp) each read true only in their real state; (b) `DefendResolver` recalls an in-flight offensive fleet home (`Kind=RecallFleet`, emits a `MoveToSystemBodyOrder` to the home body), no-ops with no outbound fleet, and skips a fleet already heading home. |

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P3.4 `EfOperationContinuityTests`** · *what:* proves a winning in-flight Conquer survives a transient internal wobble (through
  the transition engine's release logic) and a genuine Defend recalls in-flight offensive fleets home · *why:* A3 found the AI
  abandoned its own invasion when a phantom rebellion flipped it to Defend, and NOTHING recalled the coasting strike fleet — the
  invasion was orphaned · *method:* drive `ObjectiveTransition.ShouldProtectInFlightConquest`/`ShouldReplan`/`Advance` + the two
  entity readers + `DefendResolver.Resolve`/`Execute` directly · *right-looks-like:* protected Conquer holds under a Survive wobble
  but re-plans on expiry / a genuine crisis; DefendResolver emits `RecallFleet` → `MoveToSystemBodyOrder` to home · *likely-failure:*
  a future edit re-couples the protection to the dead tier compare, or the recall guard stops distinguishing outbound from homebound
  fleets · *mitigation:* this gauge + the byte-identity tripwires (`Defend_NoOutboundFleet_FallsToBuildOrPosture`, the default-false
  params) · *unblocks:* the A3 objective-flip fix end-to-end — the AI presses a winning war through a domestic shock, and a real
  crisis pulls its fleets home instead of stranding them.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `NPCDecisionProcessor.UpdateStrategicObjective` → `ObjectiveTransition.ShouldProtectInFlightConquest` (reads `NeedsLadder.HomelandInvaded`
  + `ConquerResolver.HasFleetInTransit` + the P3.3 `CrisisTrigger`s) → `ObjectiveTransition.Advance`/`ShouldReplan` (`protectCommit`):
  a winning in-flight Conquer no longer flips to Defend on a transient internal wobble.
- `DefendResolver` (Rung 0) → `Movement.MoveToSystemBodyOrder` → the fleet's `OrderableDB`: a genuine Defend switch recalls in-flight
  offensive fleets to the home colony body (reads `FactionState.OwnedFleets` + each fleet's `Movement.WarpMovingDB.TargetEntity`).

### Cross-lane requests: NONE — every production edit was inside the CORE fence.

---

## P4.1 — Rung-2 already-queued guard (stop the 4× redundant transport)

**What shipped (findings/A4-sealift.md cause 1):** `ConquerResolver` Rung 2 (BUILD TRANSPORT) gated ONLY on
`target.IsValid && !FactionOwnsTransport(state)` + a free line, so on a MULTI-LINE shipyard colony (Mars fielded 4
ship-assembly lines) it queued a heavy troop transport on EVERY free line in successive monthly cycles — 4 redundant
troopers (16 warp drives + 16 NTRs in sub-jobs from 6 factories) strangling Mars industry before a single one finished.
Added the "and none already queued" clause:

- New internal helper `ConquerResolver.FactionHasTransportQueued(FactionState state)` — scans every snapshot colony's/
  station's `IndustryAbilityDB.ProductionLines[*].Jobs` and returns true if any job's `ItemGuid` resolves (in
  `FactionInfoDB.IndustryDesigns`) to an `IsTroopTransport` `ShipDesign`. Reads the SAME `ProductionLines.Jobs` the
  free-line check reads; `ItemGuid` (from `JobBase`) is the design key. Defensive/no-throw.
- Rung 2 guard is now `target.IsValid && !FactionOwnsTransport(state) && !FactionHasTransportQueued(state)`. One
  transport in production is enough; the resolver re-checks each cycle, and a finished+launched transport flips
  `FactionOwnsTransport` (so it never over-builds). A second cycle falls through to the next rung (RebuildGarrison / MASS).

**Byte-identity:** (a) default-off flag — the whole resolver runs only inside the `EnableOrderEmission`-gated
`EmitOrders` (default false), so a default game is byte-identical; AND (b) provably inert absent the new data — the
added clause fires only when a troop-transport design is already QUEUED, a state no existing gauge constructs. The
existing ConquerResolver/MilitaryComposition gauges never reach Rung 2 with a transport queued (no war target → Rung 2
skipped; LAND/SAIL rungs fire first in the invasion gauges), so all stay green.

**Files changed:** `GameEngine/Factions/ConquerResolver.cs` (helper + guard + Rung-2 comment),
`GameEngine/Factions/CLAUDE.md` (ConquerResolver row). **Created:** `Pulsar4X.Tests/EfSealiftQueueGuardTests.cs`.

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P4.1 `EfSealiftQueueGuardTests`** · *what:* proves Rung 2 builds exactly ONE troop transport then the
  already-queued guard makes the next cycle fall through (no redundant second transport), + the `FactionHasTransportQueued`
  helper reads a queued trooper across the faction's production lines · *why:* A4 cause 1 — the 4× redundant queue that
  strangled Mars industry so the sealift never produced a launchable transport · *method:* register the base-mod trooper
  design on the start faction, clear start fleets, give the player a war target, Resolve twice through the real
  `ConquerResolver` (no sim advance): first = `BuildTransport` + Execute; second ≠ `BuildTransport` · *right-looks-like:*
  `FactionHasTransportQueued` false→true across the queue; action2.Kind ≠ `BuildTransport` · *likely-failure:* a future
  edit drops the guard clause, or Rung 2 feasibility changes so the first build never fires · *mitigation:* this gauge ·
  *unblocks:* the NPC sealift end-to-end (Mars industry no longer strangled), P4.4's end-to-end sealift gauge.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `ConquerResolver` Rung 2 → `FactionHasTransportQueued` → `FactionState.Colonies[*].Industry.ProductionLines.Jobs`
  (+ `FactionInfoDB.IndustryDesigns` for the design lookup): the BUILD-TRANSPORT rung now reads the production queue
  before queuing, so it stops at one in-flight transport instead of one-per-free-line — the industry-queue read that
  keeps the invasion carrier from crowding out the rest of the colony's build.

### Pending row — `Pulsar4X.Tests/CLAUDE.md` (test inventory — append a new row)
- **`EfSealiftQueueGuardTests`** | **Operation Earthfall P4.1 — Rung-2 already-queued guard (findings/A4 cause 1).**
  Drives `ConquerResolver` twice on a free-line colony with a war target (no sim advance): the first cycle queues ONE
  troop transport (`BuildTransport`), the second falls through (≠ `BuildTransport`) because `FactionHasTransportQueued`
  now sees it in production — the fix for the 4× redundant transport queue that strangled Mars. Registers the base-mod
  `default-ship-design-trooper` onto the start faction via `ShipDesignFromJson.Create` (the colony stocks the parts but
  no trooper ship design by default).

### Developer decisions raised (P4.1): NONE. No new gameplay/balance numbers (the guard is a pure de-duplication).

### Cross-lane requests (P4.1): NONE — the only production edit (`GameEngine/Factions/ConquerResolver.cs`) + its
subsystem `CLAUDE.md` + the new test file are all inside the CORE fence.

---

## P4.2 — charge + fuel BUILT hulls (both build paths; owner-split policy flag)

**What shipped (findings/A4-sealift.md cause 3):** `ShipFactory.ChargeReactors` + `FillFuelTanks` were only ever called
by the scenario loader (`FactionFactory` — the task-#36 fix) and `CombatSandbox`, never on the INDUSTRY build path. So a
production-built transport/warship booted with a dead reactor + empty tanks and could not spin a warp bubble (paid from
STORED electricity) the instant it existed — the massed sealift never sailed. This slice provisions a built hull at BOTH
industry build paths, gated by an owner-split policy:
- `ShipDesign.ProvisionBuiltShip(ship, ownerFaction)` (new, `Ships/ShipDesign.cs`) — the ONE policy gate both paths call.
- Wired at `ShipDesign.OnConstructionComplete` (direct-build `CreateShip` branch) and
  `LaunchComplexProcessor.TryLaunchShip` (after positioning) — both inside the CORE fence.

**Developer decision honored + RAISED for review** — *"a production ship earns its charge over game-time"* was the
deliberate PLAYER rule. Implemented as two independent static policy flags (both FLAGGED):
- `ShipDesign.ChargeBuiltNpcShips = true` (default ON) — the AI's home-built sealift boots ready to fly (fixes A4).
- `ShipDesign.ChargeBuiltPlayerShips = false` (default OFF) — the human still charges/fuels at a colony over game-time.
The developer may flip `ChargeBuiltPlayerShips` on if the "earn the charge" friction isn't wanted; the split is the
reviewable knob. (This is the P4.2 half of the CAMPAIGN-PLAN §6 "built-ships-boot-charged policy for PLAYER ships" item.)

**FLAGGED balance/policy numbers (2, both carry `// FLAGGED balance value`):**
- `ShipDesign.ChargeBuiltNpcShips = true` — NPC-built hull provisioning policy.
- `ShipDesign.ChargeBuiltPlayerShips = false` — player-built hull provisioning policy.

**Byte-identity claim — (a) default-off flag for the player half + provably inert for the NPC half in the existing suite.**
For a player-owned built hull, `ChargeBuiltPlayerShips = false` makes `ProvisionBuiltShip` an early-return no-op → literally
byte-identical. EVERY CI fixture that builds a ship does so on a `FactionFactory.CreateBasicFaction` faction, whose
`FactionInfoDB.IsNPC` is the default `false` (verified: `CreateBasicFaction` never sets it) → the whole existing green
suite is unchanged. The NPC half (flag ON) is the intended NEW sealift behavior, but it is inert in the existing suite
because an NPC only reaches the industry build path when `EnableOrderEmission` is ON (default OFF in the engine harness),
and no existing fixture completes an NPC ship build to assert on its charge/fuel state.

**Parallel-safety note (subsystem CLAUDE.md routed here, NOT edited in the shared file):** the natural home for this change
is the `GameEngine/Combat/CLAUDE.md` **spawn-parity** directive (*"when you add any new 'ship is battle-ready' path … must
charge+fuel"*) — `Ships/` has no own CLAUDE.md. `Combat/CLAUDE.md` is a high-collision target (sibling P4.3/P4.4 CORE
slices + other lanes touch it), so per CAMPAIGN-PLAN §2.4 the row text is routed here to land at P8.2.

### Pending row — `GameEngine/Combat/CLAUDE.md` (spawn-parity "ship is battle-ready" section)
- Append after the existing charge+fuel directive: **"+ P4.2 (Operation Earthfall, findings A4): the INDUSTRY build path
  now charges+fuels too. `ShipFactory.ChargeReactors`/`FillFuelTanks` were called only by the scenario loader
  (`FactionFactory`) + `CombatSandbox` — never on production-built ships — so a home-built transport booted with a dead
  reactor and empty tanks and couldn't warp, stranding the NPC sealift. `ShipDesign.ProvisionBuiltShip(ship, ownerFaction)`
  is the ONE policy gate both build paths (`ShipDesign.OnConstructionComplete` direct-build branch +
  `LaunchComplexProcessor.TryLaunchShip`) now call. Owner-split policy (both FLAGGED): `ChargeBuiltNpcShips` (default ON —
  the AI sealift boots ready to fly) / `ChargeBuiltPlayerShips` (default OFF — the player earns its charge over game-time,
  the developer's deliberate rule). Byte-identical: player half is a default-off no-op; NPC half is inert in the engine
  suite (no fixture completes an NPC ship build). Gauge: `NpcFleetReadyToSailTests.BuiltShips_ChargedForNpc_NotForPlayer_PerProvisioningPolicy`."**

### Pending row — `Pulsar4X.Tests/CLAUDE.md` (the `NpcFleetReadyToSailTests` inventory line — append to its existing row)
- Append: **"+ P4.2 `BuiltShips_ChargedForNpc_NotForPlayer_PerProvisioningPolicy` — the BUILT-hull sibling: a fresh hull
  stood up via `ShipFactory.CreateShip` (un-charged, exactly as `OnConstructionComplete` hands it) run through
  `ShipDesign.ProvisionBuiltShip` boots CHARGED (≥ its warp-bubble cost) + fuelled when NPC-owned, but stays un-charged +
  empty when player-owned (same hull, only the owner differs → isolates the owner-split provisioning policy). The industry-path
  echo of the scenario-loader gauge above."**

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P4.2 `NpcFleetReadyToSailTests.BuiltShips_ChargedForNpc_NotForPlayer_PerProvisioningPolicy`** · *what:* proves a
  production-built NPC hull boots charged+fuelled while an identical player-built hull does not · *why:* A4 cause 3 — built
  hulls were never charged, so the NPC's home-built sealift couldn't warp (invasion never landed) · *method:* build the same
  design fresh for an NPC and the player faction from the DevTest sandbox, call `ShipDesign.ProvisionBuiltShip`, compare
  stored-energy-vs-bubble-cost + cargo mass · *right-looks-like:* NPC hull stored ≥ bubble + cargo>0; player hull stored <
  bubble + cargo 0 · *likely-failure:* a future edit drops the `ProvisionBuiltShip` call from a build path, or flips a policy
  default · *mitigation:* this gauge + the try/finally flag pin · *unblocks:* the NPC sealift end-to-end (a built transport can
  warp), P4.4's end-to-end sealift gauge.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `ShipDesign.OnConstructionComplete` (direct build) + `LaunchComplexProcessor.TryLaunchShip` (launch complex) →
  `ShipDesign.ProvisionBuiltShip` → `ShipFactory.ChargeReactors` + `FillFuelTanks`: a production-built hull is charged+fuelled
  at build-complete (NPC on / player off), so `MilitaryReach.FleetHasWarpRange` (`EnergyStored ≥ BubbleCreationCost`) reads
  true for the AI's home-built strike fleet — the ConquerResolver Rung-1 STRIKE can sail a hull the faction BUILT, not just
  one the scenario placed.

### Developer decisions raised (P4.2):
- **The owner-split provisioning policy** (`ChargeBuiltNpcShips=ON` / `ChargeBuiltPlayerShips=OFF`) honors the developer's
  "a production ship earns its charge over game-time" for the human while unblocking the AI sealift. Both flags are FLAGGED
  and reviewable — flip `ChargeBuiltPlayerShips` on to remove the player-side charge/fuel friction entirely.

### Cross-lane requests (P4.2): NONE — both edited files (`Ships/ShipDesign.cs`, `Ships/LaunchComplexProcessor.cs`) are inside the CORE fence.

---

## P4.3 — launch fuel DATA + pad-tonnage audit (findings/A4-sealift.md cause 2)

**Part (a) — the fuel id + the Mars fix (SHIPPED).** `LaunchComplexProcessor.TryLaunchShip` → `TryDeductFuel` does NOT
deduct a single named fuel id — it (1) requires the colony have a `CargoStorageDB` "fuel-storage" TypeStore at all
(`storage.TypeStores.ContainsKey("fuel-storage")`, `Ships/LaunchComplexProcessor.cs:152`) then (2) deducts the
lift-to-orbit fuel MASS from whatever fuel cargoables sit in that store (`:162-175`). The store only exists if a
`CargoStorageAtb` component with `StoreTypeID == "fuel-storage"` is installed (`StorageSpaceProcessor.CalculatedMaxStorage`
+ `CargoStorageAtb.OnComponentInstallation`). The base-mod materials whose `CargoTypeID == "fuel-storage"`
(`TemplateFiles/materials.json`) are **rp-1 / methalox / hydrolox / ntp / fissile-fuels**.

Mars owned a `default-design-launch-complex` but had NEITHER a fuel-storage component NOR any fuel cargo (only raw
`hydrocarbons`, which is `general-storage`), so `TryDeductFuel` failed the ContainsKey check every day → a finished
transport sat on the pad forever. The working reference is **Earth** (`systems/sol/earth.json`): it stocks a
`default-design-fuel-farm-5000k` installation (provides the fuel-storage store via the `stainless-steel-fuel-tank`
template's `CargoStorageAtb('fuel-storage', …)`) PLUS fuel cargo (rp-1/methalox/hydrolox). Mars is now made to match:
- `umf.json` componentDesigns: added `default-design-fuel-farm-5000k` (template `stainless-steel-fuel-tank`, already
  unlocked in UMF `startingItems`; the design is defined globally in `componentDesigns.json` — GROUND's fence, NOT edited).
- Mars installations: added `default-design-fuel-farm-5000k` ×1 (creates the "fuel-storage" store at colony build,
  BEFORE `LoadCargo` runs — `Entity.AddComponent` → `CargoStorageAtb.OnComponentInstallation` adds the TypeStore
  synchronously; `ColonyFactory.CreateFromBlueprint` installs at :114-120 then loads cargo at :131).
- Mars cargo: added `methalox` 20,000,000 + `ntp` 20,000,000 (`type: byCount` — LOWERCASE key; the umf.json cargo parser
  `FactionFactory.LoadCargo` reads `toAdd["type"]`, and Newtonsoft's JToken indexer is case-SENSITIVE, so a capital `Type`
  silently falls through to the default `byMass` branch — matches the sibling `uef.json` convention). Both fuel-storage
  materials already in UMF `startingItems`. ntp matches the trooper's NTR drive; methalox is a cheap conventional reserve.
  ~72,267 m³ total, well within the fuel farm's cap.

**Audit — every UMF colony (data gauge run):** only **Mars** owns a launch complex; Luna/Venus/Ceres do not (they build,
if at all, via the direct-`CreateShip` PARK path, no fuel gate). So Mars was the only UMF host with the trap; it is fixed.

**Part (b) — trooper mass vs pad MaxTonnage 100,000 kg (AUDIT: UNDER the limit, no change required).**
The pad gate is `AssignQueueToPads`: `if (shipDesign.MassPerUnit > pad.MaxTonnage) continue;`
(`LaunchComplexProcessor.cs:58`); `pad.MaxTonnage` = the launch-complex design's `Max Tonnage` property = **100,000 kg**
for `default-design-launch-complex` (template default `PropertyFormula 100000`, `installations.json:1440`;
`LaunchComplexAtb.OnComponentInstallation` → `(long)MaxTonnage`). `ShipDesign.MassPerUnit` = Σ(component.MassPerUnit ×
count) + armor (`Ships/ShipDesign.cs:226-259`). Component masses of `default-ship-design-trooper` (from GameData
templates/designs):

| component | mass each (kg) | ×count | subtotal (kg) |
|---|---|---|---|
| default-design-ship-hull-heavy (`Hull Mass` 25000) | 25,000 | 1 | 25,000 |
| default-design-passive-sensor-s50 (`90 + 0.01·5.5²`) | ~90 | 1 | ~90 |
| default-design-troop-bay (`Mass` 5000) | 5,000 | 2 | 10,000 |
| default-design-fuel-tank-1000 (`Dry Weight`, see note) | 400 – 15,470 | 2 | 800 – 30,940 |
| default-design-alcubierre-500 (`Mass` 500) | 500 | 4 | 2,000 |
| default-design_solarpanel (`Area 100 · tech-panel-density`) | ~100 | 1 | ~100 |
| default-design-battery-2t (`Mass` 2000) | 2,000 | 3 | 6,000 |
| default-design-reactor-2t (`Mass` 2000) | 2,000 | 1 | 2,000 |
| default-design-NTR1.8 (`Mass` 1800) | 1,800 | 4 | 7,200 |
| armor (plastic, thickness 3; density = 1/0.0010638298 ≈ 940 kg/m³) | — | — | ~1,500 (≤ ~5,300 upper bound) |

The one swing factor is the `stainless-steel-fuel-tank` `Dry Weight`, which uses `Pow(3·(TankVol/(4π)), 1/3)`. NCalc's
default `int/int` for the `1/3` exponent (→ 0 → radius 1 → ~400 kg/tank) vs a double `0.333…` (→ radius ~6.2 → ~15,470
kg/tank) bracket the tank contribution. **Both brackets land the trooper UNDER 100,000 kg:** ≈ **54,700 kg**
(integer-exponent) … ≈ **84,800 kg** (double-exponent); even the absolute worst case (max armor) ≈ **89,000 kg**. So the
pad accepts the trooper → **no tonnage change is required for the launch to proceed.** (This resolves the finding's
"heavy trooper mass unverified" flag — it's heavy but under the pad ceiling.)

Note the *worst-case* margin is thin (~85–89 k vs 100 k) and the armor figure carries geometric uncertainty, so a
headroom bump is offered as an OPTIONAL developer decision + cross-lane note below (NOT a required fix, and out of the
CORE data fence — the launch-complex design lives in `componentDesigns.json`).

**FLAGGED numbers (data — JSON carries no `//` comments, so flagged here per CAMPAIGN-PLAN §2.6):**
- Mars `methalox` **20,000,000** (byCount) — FLAGGED balance value (starting launch-fuel reserve; ≈ payload×2.1 per
  launch off Mars via `OrbitMath.FuelCostToOrbit`, so ~a few hundred trooper launches — a generous, tunable reserve,
  same order as Earth's ~50 M).
- Mars `ntp` **20,000,000** (byCount) — FLAGGED balance value (NTR-drive launch/maneuver fuel reserve; same rationale).

**Byte-identity claim: (b) provably inert absent new data.** The whole change is DATA confined to `umf.json` (the UMF
scenario faction file) — no engine code, no shared template, no `earth.json`, no base-mod New-Game path. A default UEF
New Game never loads `umf.json`, so it is byte-identical. Only a scenario that fields the UMF faction (the DevTest
sandbox; a UMF-vs-UEF campaign) sees the change, which IS the intended fix (Mars can fuel its launches). No existing
green fixture asserts Mars's exact installation count or cargo totals (checked: `DevTestScenarioTests`,
`DevTestFleetRoleReadoutTests`, `FactionSelfSufficiencyReadoutTests`, `GroundGarrisonPerFactionTests`,
`FleetCompositionPerFactionTests`, `EfUmfGovernmentAndCeresFactoryTests` assert structural truths, not Mars data), so
adding a fuel-farm + fuel breaks none of them. The new fixture only READS state and steps no clock.

**Files changed:** `GameData/basemod/ScenarioFiles/umf.json` (Mars fuel-farm install + methalox/ntp cargo; UMF
componentDesigns += fuel-farm). **Created:** `Pulsar4X.Tests/EfLaunchFuelStockTests.cs`.

### Pending row — `docs/TESTING-TRACKER.md` (Layer-1 engine CI)
- **T-P4.3 `EfLaunchFuelStockTests.EveryLaunchComplexHost_StocksItsLaunchFuel`** · *what:* proves every host owning a
  `LaunchComplexDB` in the DevTest sandbox also stocks its launch fuel (a non-empty "fuel-storage" TypeStore) — the two
  preconditions `TryDeductFuel` needs · *why:* A4 cause 2 — Mars had a launch complex but no fuel-storage store and no
  fuel, so a built transport never left the pad · *method:* load the DevTest sandbox (uef-devtest/umf/kithrin, no clock
  advance), enumerate every colony+station, and for each with a `LaunchComplexDB` assert `TypeStores.ContainsKey("fuel-storage")`
  + summed `CurrentStoreInUnits` > 0; assert ≥1 launch-complex host was checked (non-vacuous — UMF Mars is the one) ·
  *right-looks-like:* Mars passes both preconditions, launchHostsChecked ≥ 1 · *likely-failure:* a scenario edit gives a
  host a launch complex without a fuel-storage component + fuel · *mitigation:* this gauge · *unblocks:* the NPC sealift
  BUILD→launch handoff (a fuelled pad), P4.4's end-to-end sealift gauge.

### Pending row — `docs/SYSTEM-CONNECTION-MAP.md`
- `LaunchComplexProcessor.TryLaunchShip` → `TryDeductFuel` → `CargoStorageDB.TypeStores["fuel-storage"]` (fed by an
  installed `CargoStorageAtb('fuel-storage', …)` component + fuel-storage cargo): a launch-complex colony consumes its
  own stored fuel to lift a hull to orbit, so a colony that fields a launch complex MUST also field a fuel-storage
  installation + stock fuel-storage cargo (rp-1/methalox/hydrolox/ntp/fissile-fuels) — the data invariant `umf.json` Mars
  now satisfies (mirroring `earth.json`).

### Pending row — `Pulsar4X.Tests/CLAUDE.md` (test inventory — append a new row)
- **`EfLaunchFuelStockTests`** | **Operation Earthfall P4.3 — launch-fuel data gauge (findings/A4 cause 2).** Loads the
  DevTest sandbox and asserts every host that owns a `LaunchComplexDB` also stocks its launch fuel — a "fuel-storage"
  `CargoStorageDB` TypeStore with a positive amount — the two preconditions `LaunchComplexProcessor.TryLaunchShip`'s
  `TryDeductFuel` needs. Guards the trap where UMF Mars had a launch complex but no fuel-storage store and no fuel (a
  built transport sat on the pad forever). No clock advance — fuel is stocked at colony build.

### Pending note — data rule (home: root `CLAUDE.md` gotcha #10 area / no `Ships/CLAUDE.md` exists — routed here per §2.4)
- **Launch-complex ⇒ fuel data rule:** any colony/station JSON that installs a `launch-complex` design MUST also install
  a fuel-storage component (a `CargoStorageAtb('fuel-storage', …)` carrier — e.g. `default-design-fuel-farm-5000k` /
  `default-design-fuel-tank-*`) AND stock a fuel-storage material (rp-1/methalox/hydrolox/ntp/fissile-fuels), or launches
  silently fail at `TryDeductFuel`. This is the launch-side twin of gotcha #10's "check the other end." Gauge:
  `EfLaunchFuelStockTests`.

### Developer decisions raised (P4.3):
1. **Mars launch-fuel reserve size** — `methalox`/`ntp` at 20 M each (byCount) are FLAGGED starting reserves (~hundreds of
   launches). Tune to taste; they only need to exceed the per-launch cost (≈ trooper-mass × ~2.1 ≈ 115–180 k kg/launch off
   Mars).
2. **OPTIONAL pad-tonnage headroom (not required — trooper is under the limit).** The trooper computes to ~85–89 k kg in
   the heaviest interpretation vs the 100 k pad ceiling — a thin worst-case margin. If the developer wants comfortable
   headroom (and to future-proof heavier authored transports), raise `default-design-launch-complex`'s `Max Tonnage`
   (e.g. 100,000 → **250,000** kg, FLAGGED). This is OUT of the CORE data fence (the design lives in
   `componentDesigns.json`, GROUND's fence) — see the cross-lane request below. Alternative: author a lighter trooper in
   `shipDesigns.json`.

### Cross-lane requests (P4.3):
1. **→ DEV (kithrin.json):** AUDIT DONE — Kithrin currently has **no** launch-complex host (its Titan station builds via a
   `default-design-shipyard`, the direct-`CreateShip` PARK path; `colonies: []`), so there is **no launch-fuel trap in
   kithrin.json today**. STANDING GUARD for DEV: **if** you ever add a `launch-complex` installation to a Kithrin
   colony/station, you MUST also add a fuel-storage component (e.g. `default-design-fuel-tank-1000`, already in Kithrin's
   componentDesigns) AND stock a fuel-storage material (methalox/ntp — already in Kithrin `startingItems`), or the launch
   fails at `TryDeductFuel`. `EfLaunchFuelStockTests` will catch a regression (it iterates all three sandbox factions).
2. **→ GROUND (componentDesigns.json), OPTIONAL / developer-gated:** consider raising `default-design-launch-complex`'s
   `Max Tonnage` from 100,000 to ~250,000 kg (FLAGGED) for headroom over the trooper's ~85–89 k worst-case mass. NOT
   required for the current base-mod trooper (it is under 100 k in every interpretation — see part (b)); it is a safety
   margin + future-proofing. This design is shared by Earth (courier launch, far lighter) so a bump is harmless there.
   Defer to the developer decision above.
