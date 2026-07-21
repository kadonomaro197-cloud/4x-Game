# R1 — FORCE MANAGER (fleets + battalions) & PLANETARY RANGE RINGS (condensed ledger)

## (A) FleetWindow anatomy (the model to extend)
- FleetWindow : PulsarGuiWindow, UNIQUE instance (GetInstance :107-114); opened via hotkey (SystemMapHotKeys.cs:69), HUD click (Selector.cs:399-404), toolbar. Title "Fleet Management" (:177).
- Tree source: uiState.Faction.GetDataBlob<FleetDB>() = ROOT; GetChildren() recursion (DisplayFleetList :1308-1364, DisplayFleetItem :1366-1447). Selection: SelectedFleet + SelectFleet (:127-153); right-click ctx menu (:1409-1419).
- "Go to location": Issue Orders tab → MoveTo → GetFilteredEntities(Friendly|Neutral, [SystemBodyInfoDB, PositionDB]) → button per body → MoveToSystemBodyOrder.CreateCommand → OrderHandler.HandleOrder (:1083-1103). Other orders same pattern (:1104-1196).
- Combat tab: doctrine TrySetDoctrine :708, EMCON SetPosture :748, engagement posture :781, attack-nearest :616 — all DIRECT calls (work under engagement lock) (:578-602).
- Rename: RenameWindow.SetEntity(Entity) — ENTITY-ONLY (:1457-1461; RenameWindow.cs:30).
- Space rings in this window: BuildRangeRings :853-922 (SimpleCircle into UIWidgets; [range-ring] logs :913/:920).
- Window registry: LoadedWindows Dictionary<Type,..>; per-entity LoadedNonUniqueWindows "Type|entityId"; menu labels GlobalUIState.NamesForMenus (GlobalUIState.cs:57). **Keep class name FleetWindow** (renaming the class orphans LoadedWindows/save refs — change only title/tab strings).

## (A) Battalion engine reads — ALL EXIST (per-body)
GroundForcesDB on the PLANET BODY (:365-399). FormationsFor(forces, factionId) PER-BODY (:923-930); location = body + LeaderRegion (:867) + unit GlobalQ/R; aggregates FormationStrength :965 / FormationHealth :975 / FormationReachHexes :955 / FormationSpeedMult :940; moves OrderFormationMove :820 / ToHex :664 / ToGlobalHex :693; queue QueueFormationOrder :842 / SetFormationOrder :850 / Clear :859; stance TrySetStance (GroundFormationDoctrine.cs:43) + SetEngagementStance (:61); CRUD CreateFormation :717 / Assign :778 / Unassign :789 / SetLeader :799 / Disband :808 / SetParentFormation :738. GroundFormation.Name = [JsonProperty] internal set (:309).
**In-client precedent: PlanetViewWindow.DrawFormationPanel (:977-1050) already does form-up/list/select/march/disband + DrawStanceSelector :1118 + DrawRoeSelector :1091 + DrawOrderQueue :1053 — a working per-body battalion panel to copy.**

## (A) Gaps
1. Cross-body registry: MISSING but PURE-CLIENT — enumerate _uiState.StarSystemStates (GlobalUIState.cs:94) → StarSystem.GetAllEntitiesWithDataBlob<GroundForcesDB>() → FormationsFor (the SystemMapRendering.cs:815 ship-enumeration precedent). Optional engine helper GroundFormationTools.AllFormationsFor(game, factionId) → (body, formation) pairs (CI-testable).
2. Formation RENAME: NEEDS small engine setter GroundForces.RenameFormation(formation, name) — data object can't use RenameWindow.
3. "Go to location": on-surface = fully supported (direct calls); cross-BODY relocation = the transport orders (LoadTroops/LandTroops), a different verb (see R2 gap 2 — no UI).
4. Battalion Combat-readout tab: MISSING (GroundCombat/CLAUDE "GroundCombatWindow — MISSING ENTIRELY"); Aurora §4 card model = render POSITIONS as cards.

## (B) Space rings (mirror source)
SystemMapRendering.UpdateAllRangeRings :795-882 (each frame, gated GlobalUIState.ShowAllRangeRings): beam ring WeaponUtils.GetMaxBeamRange_m :864, sensor reach :865, detectability :866, colony DetectionRangeAgainst :874-878; drawn as SimpleCircle in UIWidgets (radius AU, keys allrange_*) :850-857; [range-ring] log :881. Engine reads CI-gauged by RangeReadoutTests (:39,198,260).
**KEY: SimpleCircle (SDL/GL) is NOT reusable on the planet map — PlanetViewWindow draws with ImGui ImDrawListPtr (AddNgonFilled etc.). "Same logic" = same engine reads → range → hexes → HIGHLIGHT with drawList.**

## (B) Planetary reads — ALL EXIST
GroundRangeTools.DefaultRangeFor :22 / HexPitchKm :34 / RealReachKm :44; GroundUnit.Range (GroundForcesDB.cs:75); FormationReachHexes :955; GroundSensorAtb.Range_km (:24); km→hex precedent GroundSensors.cs:42-48 (reachHexes = Range_km / HexPitchKm), best-radar read :90.

## (B) PlanetViewWindow render path + insertion
Display :140 → DrawTacticalMap :170 → DrawGlobalHexWindow :277 (the only surface view since G6a): terrain hexes :311-344; deposit gems :329-342; region seams :346-359; unit tokens :361-392; selection ring AddNgon :391; click :428; **range display today = TEXT ONLY** "strike range {range} hex ≈ {km} km" :415-423 (RealReachKm). **No ring/hex-highlight overlay exists.**
INSERT the highlight pass after terrain loop (:344), before unit tokens (:361), same sc/r window + HexCenterOffset; weapon radius = GroundUnit.Range / FormationReachHexes; radar = Range_km ÷ HexPitchKm (GroundSensors.cs:42 conversion); hex distance = Colonies.HexCoordinate + WrapDelta (:470); colors red=weapon, green=sensor (match SystemMapRendering :864-866); precedents: selection ring :391, deposit overlay :329-342, fog gate bandSurveyed :317. Toggle mirrors GlobalUIState.ShowAllRangeRings.
Engine adds: NONE required; optional GroundSensors.RadarReachHexes(body, unit) for CI-testability.

## Window name candidates (developer's pick)
"Force Management" · "Forces" / "Force Command" · "Order of Battle" · "Command" · "Fleets & Formations" · "Military Command" (pairs with Factions.MilitaryCommand) · "Task Forces". Keep class FleetWindow.
