# LANE-DEV-NOTES — Operation Earthfall, DEV lane (`claude/earthfall-dev`)

Lane owner: DEV. Phases D1 → D2 → D3. Fence (CAMPAIGN-PLAN.md §3):
`GameEngine/Factions/ExpandResolver.cs`, `ConsolidateResolver.cs`,
`GameEngine/Stations/**`, `GameData/.../kithrin.json`.

This file collects (campaign rule §2.4, conflict-free, lane-owned):
- **Pending dashboard rows** — for the integration phase (P8.2) to land into
  `docs/DOCS-INDEX.md`, `docs/TESTING-TRACKER.md`, `docs/SYSTEM-CONNECTION-MAP.md`.
- **Cross-lane REQUESTs** — needs that fall outside the DEV fence.
- **Developer decisions raised** — balance numbers / policy calls for the developer.

---

## Pending dashboard rows

### D1.1 — Kithrin survey chain (2026-07-18)

**TESTING-TRACKER.md** (new engine gauge, `rest` shard):
- `EfKithrinSurveyChainTests` — the task-#35 gauge for the survey→found chain. (1) with an idle
  surveyor owned, `ExpandResolver` emits a real `GeoSurveyOrder`; drive `GeoSurveyProcessor` to
  completion and the FOUND rung then settles that world; (2) with no surveyor owned, the fallback
  rung queues ONE surveyor build; (3) the already-in-production guard blocks re-queueing a second
  surveyor while one is on the slipway. Resolver-driven (no sim advance). Status: WRITTEN (CI-unverified in-container — no local SDK).

**SYSTEM-CONNECTION-MAP.md** (new/confirmed wires):
- `ExpandResolver` → `GeoSurveyOrder`/`GeoSurveyProcessor` (AI now drives the geo-survey the player's
  FleetWindow right-click drove — the survey leg was `Execute=null` before).
- `ExpandResolver` → `IndustryTools.AddJob` (fallback: build a surveyor ship — the `default-ship-design-surveyor`/
  Kithrin `kithrin-ship-sable` cradle: geo-surveyor component → `GeoSurveyAtb` → `GeoSurveyAbilityDB`).
- `kithrin.json` "Titan Hive Guard" fleet now fields a `kithrin-ship-sable` surveyor → the Kithrin
  finally OWN a survey-capable ship (rung 2 of the three-dead-rungs A6 diagnosis).

**Factions/CLAUDE.md** — the `IObjectiveResolver.cs + *Resolver.cs` combined row is SHARED with CORE
(ConquerResolver/DefendResolver/NPCDecisionProcessor live there and CORE edits this file in P3/P4/PW),
so per PLAN §parallel-safety I did NOT edit it in-lane to avoid a collision. Row text to fold in at
integration:
> `ExpandResolver` — the survey→move→found chain is no longer FOUND-only. The survey leg is built
> (D1.1, 2026-07-18): when the best world needs a geo-survey it (b) emits a real `GeoSurveyOrder` for
> an idle owned survey-capable ship (front door = the player's own order), and (c) with no surveyor
> owned queues ONE surveyor build behind an already-in-production guard (`FactionOwnsSurveyor` +
> `SurveyorInProduction`). Byte-identical off (`EnableOrderEmission`). Helpers `IsSurveyor` /
> `FindIdleSurveyor` / `FactionOwnsSurveyor` / `SurveyorInProduction` are `internal` for the gauge.

## Cross-lane REQUESTs

### D1.1 (2026-07-18) — informational to CORE (owner of the campaign-clock P0 shard), NOT an edit request

`CampaignClockReadoutTests` (CORE-owned `campaign-clock` shard) loads `kithrin.json` with
`EnableOrderEmission = true`. My D1.1 change means that IF the Kithrin settle Expand there and own the
new Sable, the `ExpandResolver` will now EMIT a `GeoSurveyOrder` (before: `Execute=null`, no order).
Assessed low-risk and no fence edit needed:
- The survey EMISSION runs through `StandAloneOrderHandler.HandleOrder`, which is try/catch-wrapped, so
  it cannot throw out of `EmitOrders` → `NPCDecisionProcessor.TickErrorCount` (assertion 1) is unaffected.
- The `ColonyCost` call-count is unchanged (I select the FIRST unsurveyed body, no `ColonyCost` on
  unsurveyed candidates) — so a Kithrin cycle runs exactly the reads it did before + the cheap surveyor scan.
- Over the 40-game-day window the low-Speed surveyor won't complete a hundreds-of-points survey, so the
  `GeoSurveyProcessor` completion side-effects (region/hex/deposit gen) are unlikely to fire; and the
  daily non-completion path just accumulates points. Even on completion it's the exact defensive player
  survey path. Survey does not touch the combat fine-stepper (assertions 3/4 unaffected).
- Whether the Kithrin even reach Expand there depends on their financial state — with the D2 station-income
  fix NOT yet landed they are likely pinned below Expand (bankruptcy → Survive tier), so the rung may not
  fire at all until D2. Flagging so CORE knows the source if that shard moves.

## Developer decisions raised

### D1.1 (2026-07-18)

- **No new FLAGGED balance numbers** in this slice. The fallback rung builds exactly ONE surveyor
  (`InitialiseJob(1, false)`) — a structural least-commitment step, not a tunable dial (mirrors
  `ConquerResolver`'s one-transport build).
- **"nearest unsurveyed colonizeable body" interpretation.** The task said "nearest"; I survey the FIRST
  unsurveyed colonizeable candidate (deterministic iteration order) rather than a positional-distance or
  `ColonyCost` ranking. Rationale: surveying ANY colonizeable world advances the chain, and the FOUND rung
  already picks the BEST (lowest `ColonyCost`) once several are surveyed — so the survey target need not be
  ranked, AND avoiding a `ColonyCost` call on unsurveyed bodies keeps the resolver's read-set identical to
  before (byte-safety for the campaign-clock shard, above). A true positional-distance tiebreak / sailing
  the surveyor to the body (the `GeoSurveyProcessor` location-gate TODO) is a named follow-on refinement.
