# A4 — SEALIFT CHAIN (agent ledger, condensed)

## VERDICT
Chain died at Rung 2 BUILD → launch handoff: NO transport ever became an owned in-orbit ship in 112 days. Downstream rungs (LOAD/SAIL/LAND) verified correctly wired — starved of input. Log proof: after 4 BuildTransport emits, never a 5th, never QueueWarship, never LoadInvasion → all 4 Mars lines still grinding, none freed, none launched.

## Stacked causes
1. **4× redundant queue:** Rung 2 (ConquerResolver.cs:241-271) gates only on !FactionOwnsTransport + a free line (FreeLineFor :600-606, Jobs.Count==0). Mars has 4 ship-assembly lines (2× olympus-shipyard + 2× shipyard, umf.json:234-241; one ProductionLine per instance, IndustryAtb.cs:62) → 4 cycles fill 4 lines with 4 heavy transports → cycle 5 no free line → Rung 2/2.5/3 all need one → None forever. 4 parallel troopers demand 16 warp drives + 16 NTRs in component sub-jobs (AutoAddSubJobs :267) from 6 factories → glacial.
2. **Launch-complex FUEL gate:** Mars builds via LaunchQueue branch (ShipDesign.OnConstructionComplete :102-110). LaunchComplexProcessor.TryLaunchShip (:84-137) positions at the Mars body (:117) ONLY after TryDeductFuel (:111-114, :139-170) from colony fuel-storage — **Mars authored cargo has NO fuel** (umf.json:331-372, only raw hydrocarbons). Finished hull sits on the pad forever. Also pad MaxTonnage 100,000 kg gate (:58; heavy trooper mass unverified).
3. **Built hulls uncharged (task-#36 class, BUILD path unfixed):** FindSailableTransport (:546-564) checks WarpAbilityDB.MaxSpeed>0 but NOT stored energy; ShipFactory.ChargeReactors called only from scenario loader (FactionFactory.cs:398) + CombatSandbox — never from OnConstructionComplete/TryLaunchShip. A built+loaded transport's WarpMoveCommand can't spin a bubble until reactor charges naturally.

## Verified NON-broken (do not "fix")
- PARK: non-launch colonies CreateShip(design, faction, industryParent) → real PositionDB at the planet body in the star system (ShipDesign.cs:116-119; ShipFactory.cs:107,117; industryParent = colony SOI parent = body, ColonyFactory.cs:217, EntityExtensions.cs:52-58). FleetDB.AddChild = membership only (TreeHierarchyDB.cs:164-173), does NOT move entity to GlobalManager.
- LOAD Rung 1.5 (:196-233): FindOwnedTransport scans all systems by ShipInfoDB + BayCapacity(Personnel)>0 (:504-515; GroundTransport.cs:40-54); ShipBody = PositionDB.Parent (:568-569); garrison on body (DevTest raises 4/3/2). Reserve guard does NOT block: WouldStripReserve → 9 <= ceil(9×0.5)=5 scaled DOWN by aggressive posture → false.
- Rung 0 LAND + Rung 1 STRIKE ordering correct; warfleet flew alone because transport never existed.

## Why CI green
ConquerResolverTests PlaceLoadedTransportAt (:94-117) hand-builds a staged transport (ShipInfoDB + PositionDB(atBody) + WarpAbilityDB MaxSpeed 10000 + preloaded GroundTransportDB) → bypasses industry, launch complex, fuel, charge, FindOwnedTransport, LOAD, warp. CI tests only the LAND/SAIL decision.

## Design facts
default-ship-design-trooper "Lander Troop Transport": ship-hull-heavy + troop-bay ×2 + alcubierre-500 ×4 + NTR1.8 ×4 + reactor-2t + battery-2t ×3 + fuel-tank-1000 ×2 + solar + sensor, plastic armor t3 — heavy, long build. Shipyard points = CrewSize × 0.02 (Olympus 40000 → 800/interval).

## Fix seams
1. Rung-2 guard: "no transport owned AND none already queued in production" → commit ONE line.
2. Provision launch fuel: data (add methalox/ntp to Mars cargo umf.json:331-372) or provision-at-launch in TryLaunchShip; mirror of task-#36 fix.
3. Charge built hulls: ChargeReactors (+FillFuelTanks) in ShipDesign.OnConstructionComplete (:116) AND LaunchComplexProcessor.TryLaunchShip (:117).
4. Verify/fix pad MaxTonnage vs trooper mass; consider a lighter authored transport for campaign-window builds.
5. CI gap: end-to-end BUILD→launch→LOAD→SAIL→LAND test advancing the real sim on a launch-complex colony (no hand-placing).
