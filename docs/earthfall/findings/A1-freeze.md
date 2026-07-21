# A1 — FREEZE MECHANISM (agent ledger, condensed)

## HEADLINE — fine-step NOT implicated; freeze = native client crash/hang
- Strike fleet was ~21-22 Gm from Earth fleets at freeze; EngagementRange_m = 1e9 (1 Gm, CombatEngagement.cs:40) → NewEngagementImminent→InRange false ALL transit (:299-346, :1796-1810; no warp/closing/ETA lookahead — flagged v1 LIMIT :296-297). Latch never armed; !imminent branch clears it every iteration (MasterTimePulse.cs:345-358). FineStepCount flat. Fine-stepping would only begin at arrival (~05-18), after the 04-22 freeze.
- "battle-trigger passes" = CombatEngagement.TickCount (:152, Interlocked ++ per Tick :175) — ONE per system per 5s hotloop pass (BattleTriggerProcessor RunFrequency 5s :24, keyed StarInfoDB :26). 1440/game-hour = 720 × 2 active systems = NORMAL cadence, invariant to fine-stepping. Clock advanced ~30 game-hours per 3 wall-seconds steadily to the stop; only ONE slow frame the whole session (startup ui 2102ms).
- Heartbeat/[ENGINE] writer: SessionLog.cs (:133-137, :333-335); cadence = wall ~3s (PulsarMainWindow.cs:422, SDL.GetTicks).
- Log stops MID-heartbeat (DetectionSnapshot per-contact loop SessionLog.cs:365-387 cut off partway).

## Watchdog elimination (Pulsar4X.Client/CLAUDE.md gotcha #12)
[HANG] (5s stale frames, SessionLog.cs:60,85) / [SIM-STALL] (2 frozen-clock beats ≈6s, :146; names processor via ManagerSubPulse.GlobalCurrentProcess :59) / [FATAL] (managed only, Program.cs:57-72) — ALL SILENT → **native SDL/GL/ImGui crash-or-hang on client thread** (or force-kill < 5-6s).

## Ranked hypotheses
- **H1 HIGH — native client render**: contact blips (SensorContactIcon + SystemMapRendering.UpdateContactBlips) are NOT range/finite-culled (gotcha #15 fix culled only OrbitEllipseIcon; SDL RenderLine chokes on extreme coords; ImGui native-assert history). fogLag jumped to ~25,454 km at 04-19→04-22 (log :878,888,895) — blip divergence as the on-ramp. CI-invisible by construction.
- H2 MED-LOW — SafeDictionary is a plain **Monitor** (NOT ReaderWriterLockSlim — docs STALE; SafeDictionary.cs:41) and fires ItemAdded/ItemRemoved/OnChange handlers UNDER the lock (:95-128) → two-lock cycle across 2 parallel sim threads (Parallel.ForEach MasterTimePulse.cs:380) + UI possible. Would trip [HANG] though; few mutations during peaceful transit.
- H3 LOW (throw not hang) — EntityManager.GetAllEntitiesWithDataBlob enumerates live _entities.Values OUTSIDE the lock (EntityManager.cs:552-561; SafeDictionary.Values releases lock :64-70) — concurrent-modification race, would surface as [FATAL]. Latent real bug.
- H4/H5/H6 LOW: contention (no slow frames), OOM/GC (no [FATAL]/slow frames), sim wedge (no [SIM-STALL]).

## CI repro — exists/missing
- EXISTS: FineStepCount (:321), MaxConsecutiveFineSteps (:304), TickCount counters, NPCDecisionProcessor gates + TickErrorCount/LastTickError (:40,:110-111), GlobalCurrentProcess (:59), DevTestStartFactory.CreateDevTest (:51-110, leaves gates to caller :46-47).
- NPCActingSensorTests deliberately avoids the clock (drives processor directly; note :23-27 re CI hang). CombatFleetTreeSafetyTests.StandingHostilePair… is the only real-clock+fine-step test (AI off, 6×1h, single-thread, [Timeout 60s]).
- MISSING: a campaign-clock fixture — DevTest + 4 AI gates ON + client combat flags ON (InterruptTimeOnNewEngagement/RequireDetectionToEngage/RequireWeaponRangeToEngage), real SimulateTimeUntil 2050-01-01→05-20, per-game-day wall-ms + FineStepCount/TickCount/ScanCount deltas + TickError + GlobalCurrentProcess; assert FineStepCount flat during transit, rises only at arrival; wall-ms/day bounded; [Timeout] mandatory. NOTE TestScenario.AdvanceTime forces EnforceSingleThread → can't reproduce H2/H3 parallel races; drive TimePulse directly with EnableMultiThreading for that.
- DECISIVE: CI headless — if H1 (native client), NO CI fixture can reproduce; client-side fix + local runtime check is the only path.

## Fix seams
1. CLIENT (primary): finite/on-screen-size CULL for contact blips (extend gotcha-#15 orbit cull to SensorContactIcon/UpdateContactBlips/SafeDraw); per-window SafeRender breadcrumb so a native stage names itself.
2. Watchdogs: shorter hang threshold + native crash-dump hook.
3. ENGINE (latent, fix regardless): SafeDictionary — raise events AFTER releasing _lock (copy-then-notify); EntityManager :557 — snapshot values INSIDE the lock (as GetEnumerator :158-166 does).
4. NOT a target: fine-step latch + NewEngagementImminent behaved correctly. Design gap for ARRIVAL: no closing lookahead → imminent won't fire till < 1 Gm (arrival-behavior item, not the freeze).
5. Docs stale: GameEngine/CLAUDE.md claims ReaderWriterLockSlim; it's Monitor.
