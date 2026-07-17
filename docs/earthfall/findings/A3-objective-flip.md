# A3 — UMF PHANTOM-COLLAPSE → 6-MONTH DEFEND LOCK (agent ledger, condensed)

## Correction to premise
Tape morale actually went 50 (Jan) → **20 (Jan-30–Feb-28)** → 55 (Mar+). Legitimacy is a ONE-CYCLE-LAGGED echo of morale. The lag is the whole bug.

## Root-cause chain
1. PopulationProcessor first monthly tick (~Jan-30; RunFreq=FirstRunOffset=30d, PopulationProcessor.cs:16-17) recomputes Mars/Venus morale (hostile-world conditions + food shortage before FoodProductionAtbDB supply counts) → faction mean morale 50→~20; next tick (~Mar-01) recovers →55. Transient trough = upstream driver.
2. LegitimacyProcessor (RunFreq=FirstRunOffset=30d, LegitimacyProcessor.cs:27-28) shares the boundary but reads morale ONE CYCLE STALE (RecalcLegitimacy :60-70; "morale" baseline factor LegitimacyDB.cs:71-72). Jan-30 write reads pre-drop 50 → 45; **Mar-01 write reads obsolete Feb trough 20 → 20−5=15**. April reads recovered 55 → 50.
3. War term is a FLAT −5 all game: WarTermFor (:122-133) → no government node in umf.json → default GovernmentDB all-Mid → WarMoraleFactor Mid −0.25 (GovernmentDB.cs:86) × MaxWarSwing 20 (LegitimacyDB.cs:36) = −5. **umf.json has NO "government" node.** No war-weariness ramp, no upkeep term; demands/governor/connectivity pinned neutral (LegitimacyDB.cs:133-140).
4. legit 15 < CollapseThreshold 20 (LegitimacyDB.cs:42,107) → UpdateRebellion begins rebellion on a SINGLE sample (LegitimacyProcessor.cs:97-112). April legit 50 ≥ RecoveryThreshold 35 → quelled (RebellionDB.cs:30,34). Rebellion lasted ~1 month.
5. NeedsLadder Survive iff inRebellion || meanMorale≤20 || meanLegit≤20 || (atWar && str<enemy×0.5) (NeedsLadder.cs:21,23,43-51; InRebellion :119-127). March: legit≤20 AND rebellion → Survive.
6. ObjectiveSelector.cs:46-52 — warFootingTier = Stabilize || (Survive && !homelandInRebellion). homelandInRebellion=true (NPCDecisionProcessor.cs:327-334) → Conquer override BLOCKED → Defend (:67-68).
7. ObjectiveTransition: DefaultCommitFor=180d (:20); Defend gets exactly 180d (:40-52 — only Expand/Conquer scale with Ambition). Commit ~Mar-01 → held to 2050-08-28. **Commit stamped at Tier=Survive (enum 0, the floor) via NPCDecisionProcessor.cs:341 → ShouldReplan's only break-glass (proposedTier < currentTier, :66-70) can NEVER fire.** No consecutive-contradiction escape exists. Quelling doesn't re-arm/shorten (re-arm only on actual replan :85-89). ~50 daily Conquer reads held on Defend.

## Q5 premise wrong — the brain ticks DAILY
NPCDecisionProcessor RunFrequency=FirstRunOffset=**1 DAY** (:30-31) — CLAUDE.md "30d" note is STALE. "Monthly" = internal sub-cadence isMonthlyCycle = Day==1 (:160-163) gating drift/ledger/treaty/espionage/assembly/doctrine/EMCON (:168-233). UpdateStrategicObjective + EmitOrders + AIDecisionRecorder.Record run DAILY (:180,:209-210,:239). SessionLog.AiDecisionSnapshot (:461-482) flushes new rows (dedupe by rec.When) → one [AI] line per game-day.

## Commitment gap (enumerated)
- NO campaign/operation object, NO fleet-recall path anywhere. StrategicObjectiveDB is single-slot (:56-98).
- (a) In-flight strike fleet: DefendResolver only queues warships (:37-65) + sets defensive-line doctrine (:67-83) — touches FleetDoctrineDB, NOT the warp order. Fleet 292 kept coasting to Earth.
- (b) Transport build job persists + completes (objective flips don't touch industry queues); finished transport sits — LOAD/SAIL/LAND are Conquer rungs, never run under Defend.
- (c) On ARRIVAL at Earth in Defend: MoveToSystemBodyOrder completes → fleet parks in orbit, defensive-line = fires only if fired on; no arrival/bombard/land rung → orbits idle. Invasion orphaned.
- Only continuity guard: ConquerResolver.FleetIsMoving (:591-597) — prevents re-issuing sail, doesn't model a campaign.

## Ranked fix seams
1. [DATA, smallest, highest leverage] Author "government" node in umf.json with Militarism High → WarMoraleFactor +0.5 → war term +10; March trough becomes 30 > 20 → no rebellion → no flip. Fits UMF fiction.
2. [ENGINE] Debounce rebellion: require ≥2 consecutive IsCollapsing reads (or EWMA) before IsRebelling (LegitimacyProcessor.cs:99-106).
3. [ENGINE] Kill the legitimacy lag: order legit AFTER population on the shared 30d boundary, or smooth legit rate-of-change so it reads current morale.
4. [ENGINE] Hysteresis break-glass: re-evaluate commit when triggering condition clears (rebellion quelled → allow early replan); add N-consecutive-contradiction release; shorter CommitFor for crisis objectives (Defend/Consolidate) than 180d.
5. [ENGINE, largest] Operation/Campaign object surviving objective flips: committed Conquer op (target + strike fleet + transport) either protects the commit from a transient Survive preempt while in-flight, or RECALLS the fleet on a genuine Defend switch. Minimal: DefendResolver recalls in-flight offensive fleets.
