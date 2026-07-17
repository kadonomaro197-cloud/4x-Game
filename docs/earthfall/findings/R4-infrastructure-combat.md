# R4 — COMBAT WITH INFRASTRUCTURE (destroy/capture) — design-ready ledger (condensed)

## Data model (a building = ONE ComponentInstance with up to THREE located records)
- Region.InstallationIds (Galaxy/PlanetRegionsDB.cs:62) — FULL economy list; Region.OwnerFactionID EXISTS :65.
- GroundHex.InstallationIds (Galaxy/GroundHex.cs:58) — FOOTPRINT-only subset (GroundFootprintAtb designs), placed at region centre hex (GroundBuildings.LocateFootprintsOnHexes :36) or global band-centre (:80). **GroundHex.OwnerFactionID EXISTS :26 — currently INERT (only writer CaptureRegionHexContents; no reader).**
- CityTile.BuildingInstanceId (Galaxy/CityTile.cs:24) — fine 1:1 tile; NO owner field (by design).
- Roll-up invariant: city tiles == GroundHex.InstallationIds (CityBuilder.cs:29,43,69,84,91 ClearBuildingFromCity clears every tile).
- id → economy: ComponentInstance.ID resolved via GroundBuildings.IndexBodyComponents (:270) walking colonies' ComponentInstancesDB on the body; removal = RemoveComponentInstance (ComponentInstancesDB.cs:151, internal). **ComponentInstance has NO owner field — owner = the holding colony entity's FactionOwnerID.**

## DESTROY — primitive EXISTS, UNWIRED
GroundBuildings.BombardHex :190 / BombardGlobalHex :206 / shared BombardResolvedHex :220: drains `strength` (denominated in ComponentInstance.HealthPercent units) across hex.InstallationIds in order (:230; per building take=min(budget,HealthPercent) :238-239); at ≤0 → RemoveComponentInstance :243 + Region.InstallationIds.Remove :244 + ClearBuildingFromCity :245; survivors kept :250; ReCalcAbilities on any destroy :254. **Callers: tests only** (GroundForcesTests:443, CityGridTests:140,194). Orbital bombardment (DamageProcessor.ApplyGroundBombardment :349) hits UNITS only, never buildings (installation damage from orbit = the unrelated random ApplyInstallationDamage :314).
Ground-unit-fired destroy needs: Attack→strength scaling (none exists), one-shot vs staged decision (BombardResolvedHex supports partial drains), and a range gate (BombardHex has none).

## CAPTURE — region-triggered only; per-hex feasible AS-IS
CaptureRegionHexContents (:166) flips hex.OwnerFactionID :177 for building hexes when the REGION flips (GroundForcesProcessor.cs:224→:227). Per-hex capture while region contested = pure data write (field exists) — **but nothing reads hex owner** (fortification reads Region.OwnerFactionID, GroundFortification.cs:43). "Owning" a captured building does NOTHING today (design comment GroundBuildings.cs:161-165: component stays in the original colony's store → keeps producing for original owner until whole-planet flip at TryCapturePlanet :594-611).
Aurora gap: "intact installations begin producing for the conqueror" (aurora/GROUND-COMBAT.md:351-355; garrison ≈ Pop(M)×Det/100×Mil/100 :353; occupation strength ≈ sqrt(Size×Units×Morale)/10000 :354) — needs ComponentInstance transfer to captor colony OR a producing-owner override (neither exists).

## Range gate to reuse
Resolver: `if (HexDist(u,t) > u.Range) continue;` GroundForcesProcessor.cs:312; HexDist = HexCoordinate(HexQ,HexR).DistanceTo (:397-398) — PER-REGION coords; unit.Range hexes (GroundForcesDB.cs:75, raise :437; defaults Inf/Armor 1 Arty 3). Footprints sit at region (0,0) → same-region gate = HexDist(unit,(0,0)) ≤ Range. Global variant compares GlobalQ/R. Co-located always in range (:262).

## Order plumbing (minimal design)
GroundOrderType byte enum {MoveToHex=0..SetEngagement=4} (GroundForcesDB.cs:238-245) — **APPEND DestroyInfrastructure=5, CaptureInfrastructure=6 (ordinal-stable, never reorder)**. GroundOrder fields reuse TargetQ/TargetR (+TargetRegion) (:253-291); add factory statics beside :272-276 + Describe cases :281. Processor: cases in ProcessFormationOrders switch (GroundForcesProcessor.cs:498), follow the kick-off-once/done/pop pattern (:532), NEVER wedge (mirror LeaderIdle :539). Destroy: range-gate → BombardHex with Attack-scaled strength; done when hex empty or budget spent. Capture: range/hold-gate → hex.OwnerFactionID = faction; instant vs contested-timer = open decision. **Inside the existing ground tick — L9, no new hotloop** (ProcessFormationOrders already runs from ProcessBody :171).
Queue entry exists: QueueFormationOrder :842 / SetFormationOrder :850 / Clear :859.

## Hooks
AI: ConquerResolver after the LAND rung (~:66) — queue DestroyInfra/CaptureInfra on the landed formation as a PlannerAction closure. Player: PlanetViewWindow city-zoom tile inspect (:598-647, button spot :640-641) + order-queue panel buttons (:1074-1086 pattern).

## Open developer decisions
1. Capture instant-on-hold vs contested timer (Aurora re-forts over 30 days).
2. Destroyed = gone (RemoveComponentInstance) vs rubble/half-state (none exists).
3. Destroy scaling: one-shot vs staged drain (both supported).
4. Captured building produces for captor BEFORE colony flips? (Aurora yes; needs transfer/override.)
5. What hex ownership DRIVES (fortification/production/victory) — stop it being inert.
6. Region-local vs global-grid addressing for the order (both primitives exist).
