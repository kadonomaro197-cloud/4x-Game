# A6 — FACTION SELF-SUFFICIENCY + KITHRIN DIAGNOSIS (agent dossier, condensed verbatim)

## UMF scenario inventory (umf.json)
- taxRate 0.30, powerDemandPerCapita 0, foodDemandPerCapita 0.00001, committedBulk 20000 (strain node :551-556). 4 colonies, no stations. War with UEF score −80 (:606-612).
- Mars (:226-372) 120M pop: mines ×6, refineries ×6, factories ×6, hydroponics-arcology ×5, solar ×10 + reactor ×5, labs ×5 + university ×3 + academy ×3, habitat ×500, warehouses, bunkers, admin/intel, launch-complex; big stockpiles (iron/steel 500k...).
- Luna (:374-433) 25M: mine/refinery/factory ×1, food ×1, NO power, NO labs, iron 6k.
- Venus (:434-493) 15M: mine/refinery/factory ×1, food ×1, NO power, NO labs.
- Ceres (:494-549) 8M: mine ×2, refinery ×1, **factory ×0** (:511-513 — cannot manufacture), food ×1.
- FOOD verdict: all colonies fed (food-production ≈5000/day/unit vs demand pop×0.00001: Mars 1200/d vs 25000). No starvation. Hostile-world morale = conditions penalty ColonyCost×12 (Mars/Venus), not food.
- UMF is genuinely self-sufficient; balance ~504k stable from 4 tax-paying colonies. Gaps: Ceres factory ×0; Luna/Venus/Ceres no research, no authored power (survivable only because powerDemandPerCapita=0).

## Kithrin inventory (kithrin.json)
- doctrine exp .45 leads → pinned on Expand. colonies:[] EMPTY (:218) — station-only. NO strain node → demands 0.
- Titan station (:219-327) 6M pop: factory ×3, refinery ×3, mine ×3, kithrin-deep-mine ×2, shipyard, local-construction, labs ×3 + academy ×2 + kithrin-nexus ×2, intel ×1, hydroponics ×2, habitat ×40, solar ×4 + reactor ×2, city-hall, command-berth, bunkers ×2, warehouse ×5, passive-sensor. Cargo: iron 120k, steel 80k...
- Fleet: "Titan Hive Guard" = 2× kithrin-ship-radiance ONLY (:329-347) — pure warships, NO GeoSurveyAbilityDB (shipDesigns.json:924-940).
- Survey-capable designs EXIST but never spawned: kithrin-ship-sable (:964-981 geo+grav), kithrin-ship-seedship (:986-1000 geo+cargo).
- INCOME: NONE. StationUpkeepProcessor bills OperatingCost = 10 + 5×modules(79) + 25×distinctDesigns(19) + 1×pop/1000(6000) ≈ 6880/mo (StationUpkeepProcessor.cs:40-62; StationEconomyDB.cs:53-72, coeff :28-31). No station-income processor exists; ColonyEconomyProcessor (tax) is colony-only → monotonic drain → bal −7890 ≈ 1+ months.

## Kithrin survey stall — THE DEAD LINK (task #35)
- ExpandResolver.Resolve emits the literal "N colonizeable world(s) await a geo-survey — survey leg pending" at ExpandResolver.cs:73-74 with **Execute=null** — a message-only PlannerAction. v1 doc :22-25 admits only the FOUND leg is built.
- GeoSurveyOrder.CreateCommand (GeoSurveyOrder.cs:84) + GeoSurveyProcessor (:25-93, reads GeoSurveyAbilityDB.Speed :95-101) chain is COMPLETE — but the ONLY caller is client FleetWindow.cs:1121 (player right-click). No AI path.
- Dead at THREE rungs: (1) no GeoSurveyOrder emission; (2) no surveyor ship in play (Radiance has no survey ability; Sable/Seedship registered but never spawned; no rung builds one); (3) no rung sails a surveyor.
- FOUND rung IS built+executable: ExpandResolver.cs:59-69 → CreateColonyOrder.CreateCommand → ColonyFactory.CreateColony (instant, no ship). Gated on GeoSurveyableDB.IsSurveyComplete (:47-57). All 23 candidates unsurveyed → unreachable.
- The "23 colonizeable worlds" count PROVES station fold-in works (FactionState.cs:64-69 folds Titan into Colonies; CandidateBodies :83-97 walks Sol bodies).

## Kithrin Consolidate no-op
- ConsolidateResolver's ONE lever (ease tax on most-restless colony) requires ColonyMoraleDB AND ColonyEconomyDB (ConsolidateResolver.cs:48); a station has StationEconomyDB not ColonyEconomyDB → skipped → None (:64). Station-only faction has NO legal Consolidate step.
- "[AI] tape colonies 0" is cosmetic: AIDecisionRecorder.cs:46 uses FactionRollup.ColonyCount which counts FactionInfoDB.Colonies only (FactionRollup.cs:45), not Stations. Resolver snapshot ≠ tape rollup.
- GrowEconomyResolver Rung C (queue build on free line, :32-63) + Rung B (mine for stalled mineral :75-113) ARE live and would grow the station — but only fire under GrowEconomy objective; Kithrin pinned on Expand (doctrine .45).

## Economy machinery + gauges (for the self-sufficiency readout fixture)
- MineResourcesProcessor on MiningDB (rates; body MineralsDB amount/accessibility). IndustryProcessor on IndustryAbilityDB.ProductionLines (job Status: Queued/Processing/MissingResources). SustenanceProcessor on ColonySustenanceDB (FoodShortage/PowerShortage; demand from strain node, 0 when absent) reading GetTotalFoodOutput + EnergyGenAbilityDB. ColonyEconomyProcessor on ColonyEconomyDB → TransactionCategory.ColonyTax (COLONY-ONLY). PopulationProcessor → ColonyMoraleDB factors. LegitimacyProcessor on LegitimacyDB. StationUpkeepProcessor (StationUpkeep) + GroundUpkeep.BillIfDue (GroundForceUpkeep).
- FactionRollup: Balance/TotalPopulation/ColonyCount(Colonies only :45)/MeanMorale/MeanLegitimacy/MilitaryStrength.
- No per-faction 1-year readout exists; template = EconomyReadoutTests.cs (single colony). Needed fixture: iterate Colonies + Stations; per host print food supply/demand, power, mining vs depletion, industry job statuses, cargo delta; faction Ledger.GetTotalsByCategory() income-vs-expense + Balance start→end. The ledger line would have caught the Kithrin drain instantly.

## CI visibility (confirmed)
- ci.yml test step: dotnet test --logger console;verbosity=normal + trx (:76-82); dorny/test-reporter renders TRX table (:84-91); upload-artifact TestResults/** (:93-99). TestContext.Progress lands in TRX only, NOT job log. No cat step exists.
- Fix options: (a) fixture writes plain file TestResults/econ-readout.txt + ci.yml step `cat` with if:always() [ALSO survives as artifact]; (b) Console.WriteLine → job log. Prefer (a).

## Headline verdicts
- UMF: self-sufficient; paralysis is political (war/legit), not economic. Data fixes: Ceres factory, (optional) Luna/Venus/Ceres labs+power.
- Kithrin: three-rung-dead survey leg + structural bankruptcy (no station income) + Consolidate has no station lever + Expand-pinned so GrowEconomy never runs.
