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

### D2.1 — Station income + Consolidate station-legal step (2026-07-18)

**TESTING-TRACKER.md** (two new engine gauges, `rest` shard):
- `EfStationIncomeTests` — the A6 "structural bankruptcy" fix (station income). (1) PURE net-positive: a populated
  industrial station's `MonthlyIncome` (through the shared `ColonyEconomyDB.MonthlyTaxIncome` model) EXCEEDS its
  `OperatingCost` at a sensible rate + neutral morale — so its faction balance grows, not only decays (the direct
  refutation of the monotonic drain); (2) INERT: an UNMANNED station yields ZERO income (the byte-identity case that
  keeps the existing unmanned-drain gauge `StationUpkeep_DrainsFunds` green); (3) WIRE: advancing the clock past a
  monthly cycle, the net-operating pass books the station-income category into the faction ledger alongside upkeep.
  Status: WRITTEN (CI-unverified in-container — no local SDK).
- `EfConsolidateStationTests` — the A6 "frozen in a crisis" fix (Consolidate station-legal step). Loads the DevTest
  Kithrin (a real station-only faction) and proves the `ConsolidateResolver` now emits a REAL, EXECUTABLE action
  (queues a build on the station via the GrowEconomy fall-through) instead of the old guaranteed `None`. Byte-identity
  for a colony faction is guarded by the existing `ConsolidateResolverTests.Consolidate_ContentColony_DoesNothing`
  (a content colony still returns None — the gate fires only when NO host carries a `ColonyEconomyDB`). Status: WRITTEN.

**SYSTEM-CONNECTION-MAP.md** (new/confirmed wires):
- `StationUpkeepProcessor` → `FactionInfoDB.Money` INCOME (was expense-only): `CollectIncome` books a populated
  station's tax via the SHARED `ColonyEconomyDB.MonthlyTaxIncome` model (pop × station `TaxRate` × morale-mult, capped
  by `GovernmentDB.TaxCeiling`). A populated station now EARNS — the Kithrin outpost is no longer a monotonic drain.
- `StationEconomyDB` → `Colonies.ColonyEconomyDB.MonthlyTaxIncome` (income model reuse) + `Colonies.ColonyMoraleDB`
  (morale multiplier). New per-station `TaxRate` lever (default FLAGGED `DefaultStationTaxRate` 0.15).
- `ConsolidateResolver` → `GrowEconomyResolver` (station-legal fall-through): when no host carries a colony tax lever,
  Consolidate delegates to the host-agnostic GrowEconomy build rungs so a station-only faction isn't frozen in a crisis.

**Factions/CLAUDE.md** — the `IObjectiveResolver.cs + *Resolver.cs` combined row is SHARED with CORE (per PLAN
parallel-safety I did NOT edit it in-lane to avoid a collision — same rationale as D1.1). Row text to fold in at
integration:
> `ConsolidateResolver` — the crisis brain is now STATION-LEGAL (D2.1, 2026-07-18). Its one lever (ease tax on the
> most-restless colony) needs a `ColonyEconomyDB`, which a station lacks — so a station-only faction (Kithrin) found no
> legal step and froze (A6). When NO host carries a colony tax lever (`HasColonyTaxLever(state)` false = a pure station
> faction), it now falls through to `new GrowEconomyResolver().Resolve(...)` — the host-agnostic build rungs — so
> Consolidate is never a guaranteed no-op. Gated on "no colony tax lever anywhere", so a colony faction whose colonies
> are all content still returns None (byte-identical). `HasColonyTaxLever` is `private static`.

## Cross-lane REQUESTs

### D2.1 (2026-07-18) — REQUEST to CORE: add a dedicated `TransactionCategory.StationIncome`

`Ledger.cs` (`GameEngine/Factions/Ledger.cs`) is outside the DEV fence, so I could not add the enum member. Station
income (`StationUpkeepProcessor.CollectIncome`) is currently booked under the EXISTING `TransactionCategory.ColonyTax`
— honest (it IS per-capita population tax, the same model/formula) and in-fence, but muddy in the P0.2/P0.4 readouts
(a station-only faction shows "ColonyTax" income while owning 0 colonies).

**Ask:** APPEND `StationIncome` to the `TransactionCategory` enum in `Ledger.cs` (at the END, after `GroundForceUpkeep`,
so existing integer values stay save-stable — enums serialize by int). Then switch the ONE reference in
`StationUpkeepProcessor.CollectIncome` (the `TransactionCategory.ColonyTax` line, marked `// REQUEST: dedicated
StationIncome category`) to `TransactionCategory.StationIncome`, and update `EfStationIncomeTests`'
`GetTransactionsByCategory(TransactionCategory.ColonyTax)` reads accordingly. Small, mechanical; keeps the readouts
honest. Low risk (no test reads a station's ColonyTax today except my new fixture).

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

### D2.1 (2026-07-18) — station income + Consolidate station-legal step

**FLAGGED balance numbers (all yours to tune):**
- `StationEconomyDB.DefaultStationTaxRate = 0.15` (`// FLAGGED balance value`) — the default per-station tax rate.
  A station has no strain node to seed a rate, so it needs a nonzero default or it stays bankrupt. Chosen MODEST
  (half the UMF's authored war-strain colony tax of 0.30). **Balance impact with this default (at the Kithrin's real
  6M pop, before any habitat-cap population decay):** income = 6M × `PerCapitaTaxBase`(0.01) × 0.15 × morale-mult ≈
  9,000/mo at neutral morale vs the A6 upkeep of ~6,880/mo → net ≈ **+2,120/mo (solvent)**. Lower the default and the
  Kithrin slide back toward drain; raise it and stations become cash cows.
- **THE PLAYER OWNS STATIONS TOO.** This default applies to EVERY populated station — including the player's. A
  player who builds a big populated station now gets tax income from it (conservative: a 1M-pop station at 0.15 yields
  ~1,500/mo before morale scaling). Kept conservative per the slice. If you want player stations UNtaxed-by-default
  (like colonies, which default `TaxRate = 0`) and only NPC stations pre-taxed, that's a scenario/authoring call — the
  `TaxRate` field is per-station settable, so `ApplyOpeningStrain`-style authoring or a governor could set it, and the
  default could be dropped to 0. I chose a nonzero default because a station has no strain-node authoring path today,
  so 0 would leave the Kithrin bankrupt (the bug). **Flagging for your call.**
- The two test fixtures set `SetTaxRate = 0.25` explicitly (not the default) so the gauges are robust to you tuning the
  default — these are test scaffolding, not shipped balance.

**Design choices (reviewable):**
- **Minimal honest income = POPULATION TAX only** (through the shared `ColonyEconomyDB.MonthlyTaxIncome`), NOT a
  separate trade/commerce term. "A populated station generates income" is captured by per-capita tax; a distinct
  industry/trade-income term is a named follow-on, not built here (the slice said "minimal honest station income,
  rate from the existing taxRate model").
- **Income category = `ColonyTax` (temporary), pending a dedicated `StationIncome`** — see the cross-lane REQUEST to
  CORE above. `Ledger.cs` is out-of-fence; `ColonyTax` is the honest per-capita-tax category in the meantime.
- **Consolidate fall-through = GrowEconomy build rung** (not station-tax-ease). The slice offered either "read
  StationEconomyDB" or "fall through to GrowEconomy". I chose the fall-through: it reuses proven host-agnostic code and
  is a real stabilization move (grow your way out), and a station's morale doesn't yet read its tax the way a colony's
  does, so easing station tax wouldn't feed back into unrest without extra wiring (scope creep). Easing station
  `TaxRate` as a Consolidate lever (now that the field exists) is a named future refinement.
- **NOT gated behind a flag.** The income pass is ungated (like `ColonyEconomyProcessor`/`StationUpkeepProcessor`
  themselves) — byte-identity is via "provably inert absent new data": income is nonzero ONLY for a populated (pop>0)
  OWNED station, and every station in the existing suite is UNMANNED (pop 0 → income 0). The one populated station in
  a test (the real Kithrin, `FactionSelfSufficiencyReadoutTests`/`CampaignClockReadoutTests`) asserts only structural/
  perf truths — MORE income transactions can't flip them.

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
