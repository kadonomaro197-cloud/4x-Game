# A5 — GROUND-INVASION CAMPAIGN STEP-AND-CLICK LEDGER (verbatim agent output)

## Orientation
`Factions/ConquerResolver.cs` is the only AI arm driving any part. Rungs (ConquerResolver.cs:28-380), gated by EnableOrderEmission:
Rung 0 LAND → 1 STRIKE → 1b STRIKE-JUMP → 1.3 SAIL transport → 1.5 LOAD → 2 BUILD-TRANSPORT → 2.5 REBUILD-GARRISON → 3 MASS. **No bombard rung, no ground-maneuver rung.**

## Step ledger
| # | Step | Status | Anchor |
|---|------|--------|--------|
| 1 | Land into a region | EXISTS; AI hardcodes region 0 | GroundTransport.cs:104-114, GroundForcesDB.cs:495-512, ConquerResolver.cs:62 |
| 1 | Orbital-control gate | EXISTS (presence-based: no foreign ship at body) | GroundTransport.cs:120-131 |
| 2 | Bombard mechanic (soften garrison) | EXISTS (calib flagged 0.01/strength) | DamageProcessor.cs:289-377 |
| 2 | AI ORDERS a bombardment | MISSING — no rung; only manual weapon-on-colony-entity reaches OnColonyDamage (beam/missile callers) | ConquerResolver.cs; Damage/CLAUDE.md |
| 2 | Strike fleet auto-bombards/auto-engages garrison | MISSING (fleet resolve is FleetDB-only; fleet just orbits) | ConquerResolver.cs:108-116 |
| 3 | Build structure with NO colony | **MISSING — all 3 build paths colony/ship-bound**: GroundBuild.QueueBuildOnTile requires ColonyInfoDB (GroundBuild.cs:32,63, places via colony ComponentInstancesDB :139-147); PlaceInstallationInRegionOrder requires colony (:98, installs on colony :79-81); OnSiteConstructionOrder builds a space STATION from ship/fleet cargo via ship-mounted ConstructorAtb (OnSiteConstructionOrder.cs:67-69,132,161-180) |
| 3 | Ground constructor component / combat engineer | MISSING (ConstructorAtb is ship-read; LOCKED PRINCIPLE says new role = component the assembler mounts, GroundCombat/CLAUDE.md:13-36) |
| 4 | Fortification from a base | EXISTS for region owner/defender (GroundForcesProcessor.cs:270-273); captured hex buildings flip owner via CaptureRegionHexContents (:227) |
| 4 | Resupply on captured ground | EXISTS mechanic + owner-flip verified (capture sets reg.OwnerFactionID :224; ResupplyUnit checks it, GroundForcesDB.cs:476-487) — **BUT NO LIVE CALLER** (only GroundAmmoTests.cs:80,87) |
| 5 | Ground movement API (region hop, hex, formation moves, ORDER QUEUE) | EXISTS player-facing (GroundForcesDB.cs:561,591,664-708; queue :807-1030; processor :490-534) |
| 5 | AI issues ground moves after landing | **MISSING** — grep: no resolver/NPCDecisionProcessor call site. Landed unit arrives unformed (FormationId=-1, :504) so even auto-ROE maneuver (:408-453) never fires. Unit sits in region 0 forever. |
| 6 | Second-wave resolver loop | EXISTS (monthly rung repeats; LOAD guarded by WouldStripReserve, ConquerResolver.cs:216) |
| 6 | In-combat ammo drain | MISSING (GroundAmmo.Consume called only by tests; drain deferred, GroundAmmo.cs:11-14; not in ResolveRegionCombat :266-394) |
| 6 | Upkeep billing | EXISTS but dormant (UpkeepCredits=0 unset at both design-builder sites; GroundForcesProcessor.cs:83, GroundCombat/CLAUDE.md:54) |
| 6 | Supply-line concept | MISSING entirely |
| 7 | Region capture owner flip | EXISTS (GroundForcesProcessor.cs:216-229) |
| 7 | Whole-planet capture | EXISTS v1 flip only (TryCapturePlanet :594-614 requires ALL regions one holder; colony entity kept, FactionOwnerID flip :611) |
| 7 | Whole-planet reachable by AI | NEEDS-CHANGE — blocked by steps 3+5 (AI lands only region 0, never spreads; multi-region world can never fully flip) |

## Minimal-build list (agent's)
1. AI bombardment rung (point orbiting fleet fire-control at colony entity, or teach auto-resolve to target colonies).
2. Colony-free ground construction: (a) ground-mountable GroundConstructorAtb (six-point registration) = combat engineer; (b) surface component haulage (GroundTransportDB carries only GroundUnits today — extend or new surface pool for ComponentInstance parts); (c) colony-free on-site ground-build order placing footprint building into captured Region/GroundHex InstallationIds; (d) AI rung to build beachhead HQ once region captured.
3. AI ground-maneuver arm: advance-to-adjacent-enemy-region loop on existing OrderMove/QueueFormationOrder(MoveRegion). Single biggest missing behavior.
4. Wire resupply + ammo: call GroundAmmo.Consume from ResolveRegionCombat; give ResupplyUnit a live caller (player + AI).
5. Populate UpkeepCredits at ToGroundUnitDesign + GroundStartGarrison.
6. AI region targeting for landing (not always 0) so all-regions capture is satisfiable.

## Test coverage
Step1: ConquerResolverTests (land+capture :119-163), TakeAPlanetIntegrationTests, GroundTransportTests. Step2: GroundBombardmentTests (all hand-fired). Step3: GroundBuildEconomyTests (colony-bound), ConstructorTests (ship→station), CityGridTests; NO colony-free test. Step5: player API only, no AI test. Step6: GroundAmmoTests, GroundUpkeepTests, GroundReinforcementTests, Conquer_SailsTheLoadedTransport. Step7: TakeAPlanetIntegrationTests, MarsBeachheadTests (target setup).

## Notes
- Landing gates: ship AT body (ShipIsAtBody :135-139) + HasOrbitalControl = no foreign non-neutral ship at body (:120-131, presence-based not diplomacy-aware).
- LandTroopsOrder carries RegionIndex (:27) so API is choosable; AI hardcodes 0.
- Hostile drop into defended region = combat next ground tick (GroundForcesProcessor.cs:200-230).
- GroundBombardmentDamagePerStrength 0.01 flagged placeholder (DamageProcessor.cs:331).
