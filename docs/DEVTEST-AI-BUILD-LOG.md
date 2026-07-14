# DevTest AI Build Log — the running journal

**What this is.** The organic, continually-updated journal for the DevTest "make the AI a real player" build plan (Stage 0 → Stage 6, batches B2→B17+). It records, in order: what we built each slice, **errors incurred + their fixes**, **near misses**, **lessons learned**, and the **wide-implication check** for every change (the Prime Directive made a habit — "adding a food processor might be an issue"). The design *specs* live elsewhere (`docs/ai/AI-BRAIN-BUILD-TRACKER.md` is the hub, the `docs/ai/AI-*.md` suite is the design); the per-run playtest *findings* live in `docs/DEVTEST-RUNTIME-FINDINGS.md`. **This doc is the build diary** — read it to know where the plan stands and what bit us on the way.

> **Cadence rule (self-imposed):** one slice at a time → push → **wait for CI green** before stacking the next (CI is the only compiler; the SDK isn't installed here). A slice isn't "done" until its CI shard is green. Client-runtime + feel are the 🖥️ PC checkpoints — those wait for the developer's machine; everything else is CI-gated here.

---

## The plan (batches → CI gate → 🖥️ PC checkpoint)

| Stage | Batch | CI gate (verifiable here) | 🖥️ PC checkpoint (needs the dev's machine) |
|-------|-------|---------------------------|---------------------------------------------|
| **0 world** | **B2** scenario stands up | scenario-loads test | PC-1: DevTest boots, clock runs, Sol fogged, UMF at war+strained |
| | **B3** materiel (UMF+Kithrin designs) | each faction builds its designs | PC-2: a rival's fleet/colonies look right |
| **1 cockpit+core** | **B4** flight recorder + loop core (record + `[AI]` tape + Inspector; fog-perception read + shared `DecisionScorer` best-not-first) | scorer best-not-first, fog respected, record renders | PC-3: turn AI on, watch each faction pick an objective + read why |
| **2 close the loop** | **B5** AI ground war keystone (raise→load→land→capture), each step logged | AI takes a defended colony | PC-4: watch Mars stage transports, land on Earth, read the chain |
| | **B6** sustain + aggression + expand (refuel/rearm, proactive declare-war, seeded in-Sol expansion) | per-capability tests | PC-5 (**batch-7 line**): hands-off run, two AIs contest Sol, you can lose Earth |
| **3 Phase A: play sensibly** | **B7** unified utility brain (guns-vs-butter one budget); Inspector ranked options | scorer table | PC-6: read the score table |
| | **B8** fog-limited threat assessment (fight/flee/concentrate) | logged read behind combat moves | PC-7: stops charging losing fights |
| | **B9…** weight-tuning slices | per-fix tests | driven by watching PC runs + the tape |
| **4 Phase B: strategic** | **B10** multi-step planning · **B11** force coordination · **B12** reactive tempo | per-slice | watch a campaign unfold |
| **5 Phase C: character** | **B13** personality-as-weights · **B14** adaptation · **B15** mission-command org · **B16** emergent politics | per-slice | see *why* the militarist chose war |
| **6 Phase D: polish verbs** | **B17+** EMCON / espionage / logistics / diplomacy-as-strategy / research beelining | per-slice | reads as cleverness, not noise |

**Where we are (2026-07-14):** Stage 0 done (B2 ✅, B3 ✅ for UMF — Kithrin materiel deferred, see below). Stage 1 B4 largely built (recorder + tape + Inspector + `AIDecisionRecorder`); the shared `DecisionScorer` best-not-first is the piece to confirm/finish. **Next active target: B5** (close the conquest loop — the AI ground-war keystone). The food-supply system + defended-worlds work this session are B5 *enablers* (a colony worth taking must be fed and defended).

---

## Standing checklists (run these every slice)

**Wide-implication check (the Prime Directive, every change):**
1. What feeds INTO it? (blobs/processors/events/JSON read)
2. What does it feed INTO? (who reads its output)
3. What shares STATE? (same blob/entity/global/JSON)
4. What does it TRIGGER? (events/processors/orders) — then look one hop further.

**Engine landmines to check before adding code (root `CLAUDE.md` Landmine Index):**
- L4 — a processor `Tick`/ctor that throws crashes STARTUP silently → trivial ctor + try/catch body.
- L9 — **one hotloop processor per DataBlob type**; a second keyed to the same blob silently never runs → key a new processor to a blob nobody else owns.
- L6/L8/#10 — a new component/design is a multi-point registration (template + design + colony `ComponentDesigns` + `StartingItems` + materials defined); JSON drift crashes New Game, not `dotnet test` (unless `BaseModIntegrityTests` covers that colony — it covers the base `colony-earth`, NOT the scenario colonies, so scenario data needs its own gauge).
- L5 — faction-level processors only fire because the GlobalManager is now iterated; key to a blob that's actually iterated.
- Namespaces ≠ folders (e.g. `ComponentMountType` is in `Pulsar4X.DataStructures`, `ComponentInstancesDB` is in `Pulsar4X.Datablobs`) — verify the `using`, not just that the type exists (bit us — see 2026-07-14 #2).

**Test-harness reality:** `BaseModIntegrityTests` exercises the base `colony-earth` blueprint, NOT the DevTest scenario colonies (those load via `DevTestStartFactory`/`FactionFactory.LoadFromJson`). So scenario-data correctness needs a `DevTestScenarioTests`-style gauge, and material-unlock checks for scenario factions must be verified by hand (all four food materials confirmed in UMF/Kithrin `startingItems`, 2026-07-14).

---

## Progress log (newest first)

### 2026-07-14 — B5-1.5 + B5-2: UMF shipyard + the AI's BUILD-TRANSPORT rung

**Wide-implication FIND (the check paid off — exactly the "adding X breaks Y" the developer asked me to watch):** while designing the BuildTransport rung I checked whether UMF can actually *build* a ship, and found it **can't** — a ship's `IndustryTypeID` is `ship-assembly`, and only a **shipyard** installation provides a "Ship Assembly Points" line (`factory`/`local-construction` provide `installation-construction`, NOT ship-assembly). UMF's colonies had mine/refinery/factory/local-construction/research-lab/warehouse/arcology — **no shipyard**. So UMF could neither build the transport NOR replace/mass warships (its only navy was its 3 start gunships). B5-2's rung — and B6's "sustain" — would have been silently inert for UMF. **Not visible in any test** (BaseModIntegrityTests only checks `colony-earth`).

**Built (B5-1.5, data):** added `default-design-shipyard` to UMF's `componentDesigns` + a shipyard installation on Mars (materials iron/aluminium/copper/plastic/stainless-steel/electronics all already in UMF `startingItems` — verified). UMF now has a ship-assembly line → it can build the transport and mass/replace warships.

**Built (B5-2, engine):** `ConquerResolver` gains **Rung 2 — BuildTransport**: at war with a scored target AND owning no troop-carrier → queue a transport (repeat: false). Two `internal` helpers: `IsTroopTransport(ship)` (mounts a `GroundBayAtb` — the transport twin of `IsWarship`, so it finds ANY buildable transport design, not the hardcoded UMF id) and `FactionOwnsTransport(state)` (scans ALL owned ships across systems, not just fleet members — a freshly-built ship isn't auto-fleeted, so a fleet-only scan would miss it and rebuild forever). `PlannerAction Kind="BuildTransport"` → auto-logged to the AI record by `EmitOrders`.

**Placement byte-identity (verified):** Rung 2 sits AFTER the sail rung (Rung 1) and BEFORE mass-warships (now Rung 3). `ConquerResolverTests` (no war → QueueWarship) unaffected (gated on target). `MilitaryCompositionTests` (war + massed+charged fleet → StrikeFleet) unaffected (Rung 1 fires first). The new rung only runs when Rung 1 doesn't (no ready fleet / fleet en route / no warp range).

**Gauge:** `DevTest_ConquerResolver_TransportDetection_Helpers` (IsTroopTransport trooper→true/gunship→false; FactionOwnsTransport UMF→false post-load) + `DevTest_UMF_AtWar_GroundedFleet_BuildsATransport` (ground UMF's fleet by clearing its stored warp energy so Rung 1 can't fire → resolve Conquer → **BuildTransport**). The grounding trick is what makes the BuildTransport path DETERMINISTIC despite UMF's start fleet.

**Wide-implication check (this slice):** rung runs only inside the `EnableOrderEmission`-gated `EmitOrders` → byte-identical off; reads only + one queued job via `IndustryTools.AddJob` (the same lever Rung 3 uses); no new blob/processor; `FactionOwnsTransport`'s ship scan is O(ships) monthly (cheap). Cross-namespace (`Factions` → `GroundCombat`/`Ships`) is same-assembly, no circular ref. Shipyard-on-Mars means UMF's AI will now actively build ships when the gate is on (intended — a real navy); byte-identical off. **Residual risk (flagged for CI):** the BuildTransport gauge assumes `FeasibilityOracle.CanQueue` passes for UMF (funds/crew/materials) — if it doesn't, the resolve falls to QueueWarship and the test names it (then I fix UMF's economy or the assertion). CI is the behavior-check here.

**Next (B5-3):** the keystone — load → sail → land → capture. AI rungs: LOAD troops onto the transport at a staging colony; LAND on the target when the transport `ShipIsAtBody` + `HasOrbitalControl`; capture rides the existing `GroundForcesProcessor.TryCapturePlanet` (needs ALL regions held — so land enough). Wide-implication watch-list from the B5 ledger: orbital-control gating (poll+retry, no diplomacy filter), Load/Land are SHIP-issued (not fleet — dodge the engagement lock), `ShipIsAtBody` needs real orbit-insertion after warp (the likeliest headless surprise), loaded troops survive save/load (OK). Gauge: an AI-driven `TakeAPlanet` sibling that flips `EnableOrderEmission`.

### 2026-07-14 — B5-1: the troop-transport ship design (the conquest-loop prerequisite)

**Built:** `default-ship-design-trooper` ("Lander Troop Transport") in `shipDesigns.json` — heavy hull + NTR engine + alcubierre warp + reactor/3×battery/2×fuel + passive sensor + **2× troop-bay** (Capacity 6 each = 12 carry-size ≈ 12 infantry or 4 armor). Added to UMF's `shipDesigns`. All 9 part designs pre-exist and are in UMF's `componentDesigns` (verified). Gauge: `DevTestScenarioTests.DevTest_UMF_CanBuildATroopTransport_ThatCarriesABay` (UMF's `ShipDesigns` holds it — the gotcha-#10 sensor that every component id resolved — AND it mounts a `GroundBayAtb`). This is the missing piece #5a from the B5 ledger: the engine load/land chain was complete but **no base-mod ship mounted a troop-bay**, so troops could never leave the ground.

**Wide-implication check:** the new design is only instantiated by a faction that LISTS it → UMF only. Other factions (player DevTest, Kithrin, default New Game `uef.json`) don't reference it → byte-identical for them. Not covered by `BaseModIntegrityTests` (that checks colony ComponentDesigns, not ship designs) → the new `DevTestScenarioTests` case IS the gauge. The transport is UNARMED, so `ConquerResolver.IsWarship`/`MilitaryComposition.ReadyStrikeFleet` won't count it as a strike hull — **intended**; the coming B5 rungs must track the transport SEPARATELY from the strike fleet (bay-capacity, not warship-count). No processor, no new blob, no combat-balance change.

**Design decision (stated, proceeding):** the player DevTest faction does NOT get a pre-made transport — its `componentDesigns` is intentionally short (installations + food only); the player has every TEMPLATE unlocked and designs their own ship in-game (the DevTest philosophy). B5's invader is the NPC (UMF), so UMF is where the transport lives. Player-invasion tooling is a 🖥️ PC/client concern.

**Next (B5-2):** AI `ConquerResolver` rungs to RAISE troops + BUILD the transport when it holds a reachable war target (mirrors the existing Rung-2 build-warship pattern; `PlannerAction` Kind auto-logs to the AI record). Then B5-3: load → land → capture + an AI-driven `TakeAPlanet` integration test (flips `EnableOrderEmission`).

**B5-2 DESIGN (worked out 2026-07-14, ready to execute — captured so it's not rushed):**
- **Placement (byte-identity is the constraint):** insert the new BuildTransport rung **AFTER** the existing Rung 1 (sail) and **BEFORE** Rung 2 (mass warships). Verified against the two existing gauges: `ConquerResolverTests` (no war → target invalid → new rung gated-out → still `QueueWarship`) and `MilitaryCompositionTests.Conquer_WithATargetAndAMassedFleet_SailsTheStrike` (war + massed **+ charged** fleet, not moving, same-system reachable → Rung 1 fires `StrikeFleet` FIRST → new rung never reached). Both stay byte-identical. The new rung only fires when Rung 1 does NOT (no ready fleet yet, or fleet already moving/unreachable).
- **The rung:** if `MilitaryTarget.BestEnemyTarget(faction).IsValid` AND the faction owns NO troop transport AND a colony has a free ship line AND a transport design is buildable → queue it (repeat=**false** — build ONE, unlike the repeat-true warship mass). `PlannerAction Kind="BuildTransport"`.
- **Two gotchas that shape the code (do NOT hardcode the ship id):**
  1. **`default-ship-design-trooper` is UMF-only** — it is NOT on the base-mod `colony-earth` blueprint that `TestScenario.CreateWithColony()` uses. So the rung must **SEARCH `state.Info.IndustryDesigns` for a `ShipDesign` that `TryGetComponentsByAttribute<GroundBayAtb>`** (the transport-detector, mirroring `ConquerResolver.IsWarship`'s weapon search), NOT hardcode the id. That also makes it work for any future faction with any transport design.
  2. **"Does the faction already own a transport?" must scan ALL owned ships, not just fleet members** — a freshly-built ship isn't auto-added to a fleet, so `FactionState.OwnedFleets()`→ships would MISS it. Scan `state.Game.Systems → GetAllEntitiesWithDataBlob<ShipInfoDB>()` filtered to `FactionOwnerID == state.FactionId`, and test each with `GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel) > 0`.
- **RaiseTroops rung:** deferred/optional for B5-2 — UMF already has a 24-unit home garrison (defended-worlds slice), so it has troops to load; a dedicated RaiseTroops rung (raise beyond the garrison) is a refinement. For v1, B5-3 loads garrison units (accept the strategic tension of stripping a garrison; Phase-B force-coordination B11 adds "keep a reserve").
- **The GAUGE (the tricky part):** the resolver decision must be tested with a faction that HAS a transport design buildable. The base-mod `colony-earth` harness faction does NOT. Options: (a) a `DevTestScenarioTests`-style test that loads UMF (has the trooper), declares its war (already authored), snapshots `FactionState`, resolves Conquer, and asserts `BuildTransport` when UMF owns no transport yet — BUT UMF's decision also depends on its garrison/fleet state, so control the setup; (b) simpler unit test of the two helpers in isolation (`FindBuildableTransportDesign` + `FactionOwnsTransport`) which needs no war setup. **Plan: do BOTH** — helper unit tests (robust, harness-light) + one UMF-scenario resolver test. This is why B5-2 wasn't rushed at the tail of the 2026-07-14 session: the gauge needs the UMF scenario, not the colony-earth harness, and that deserves a clean build.
- **Wide-implication pre-check (before writing):** the rung runs only inside the `EnableOrderEmission`-gated `EmitOrders` → byte-identical off. It reads (no writes except the queued job via `IndustryTools.AddJob`, the same lever Rung 2 uses). No new blob/processor. The one behavioral change when the gate is ON: a belligerent with no transport builds one — which is the intended B5 behavior. Watch: the `FactionOwnsTransport` scan is O(ships) per cycle (monthly) — cheap. Confirm `system.GetAllEntitiesWithDataBlob<ShipInfoDB>()` is the right enumeration (StarSystem extends EntityManager).

### 2026-07-14 — Session: food supply + B3 UMF strike fleet + defended worlds

**Built (all pushed to `claude/devtest-faction-design-xpfnhe`):**
- **Food-production system (B5 enabler).** New `FoodProductionAtbDB` component (Civic ▸ Development door) with dials **Food Output** / **Food Quality** (cubic cost) / **Automation**; `SustenanceProcessor` reads real food supply from installed food buildings (was hardcoded 0 → a permanent −40 morale floor on hostile worlds); `ColonyMoraleDB` gains a food-quality bonus. Base-mod `agri-complex` (q1.0) + `hydroponics-arcology` (q2.5). Wired into DevTest player (Earth farms + covered demand), UMF (each colony an arcology + restored food demand), Kithrin (Titan station arcology). CI ✅.
- **B3 UMF strike fleet.** Mars Home Guard 2→3 gunships so `MilitaryComposition.ReadyStrikeFleet` (min 3) fires → `ConquerResolver` Rung 1 sails the fleet at Earth (the "phony war" goes hot in space). Registered UMF's buildable ground/troop designs (infantry/armor/artillery/troop-bay). CI ✅.
- **Defended colony worlds (B5 enabler).** `DevTestStartFactory` third pass raises a home garrison for EVERY loaded faction (was player-only + gated off), so UEF Earth + UMF worlds start defended — a war scenario needs a defended planet to take. CI: compiled green; test running.
- **Integration tests.** Food: whole-pipeline (advance the clock → morale tracks food) + grave rung (destroy the farm → starvation returns). Garrison: `DevTest_ColonyWorlds_StartWithAHomeGarrison`.

**Errors incurred + fixes:**
1. **First food commit RED — 40 s fail across all 4 test shards, build-client green.** Diagnosis: build-client (client+engine) passing but every engine/test shard failing fast = a **test-project compile error**. Log grep found exactly one: `FoodProductionTests.cs(101,63): error CS0103: 'ComponentMountType' does not exist`. **Fix:** `ComponentMountType` lives in `Pulsar4X.DataStructures`, not `Pulsar4X.Components` — added the using. **Lesson:** build-client green + all test shards fast-red = test-project compile break; and *namespace ≠ folder* — verify the `using` by grepping the enum's real namespace, don't assume it sits with the types it's used alongside.

**Near misses (caught before/by CI):**
- **Ceres-scale morale numbers.** Chose food demand per-capita (1e-5) so ONE building covers any colony; verified each colony's pop × per-capita ≪ 5000/day supply, so no colony starts starving. Had I set demand too high, every colony would read total shortage on load (the "default-deficit tanks every colony" trap the sustenance system is explicitly guarded against).
- **Resilience dial almost added blind.** Wanted a bombardment-resistance food dial; checked `DamageProcessor.ApplyInstallationDamage` first — it drains health at a flat rate that **ignores building HTK/durability**, so the dial would've been inert. Deferred it as a shared-infrastructure combat-balance change (needs the dev's PC to feel), rather than ship a do-nothing dial. **Lesson:** a dial only earns its weight if the system it feeds actually reads it — verify the consumer before adding the knob.
- **Scenario-data not covered by `BaseModIntegrityTests`.** The food designs on UMF/Kithrin aren't auto-checked by the base-mod integrity test (it only builds `colony-earth`). Verified all four food materials (stainless-steel/plastic/water/aluminium) are in each faction's `startingItems` by hand + the `DevTestScenarioTests` load test exercises the scenario build.

**Wide-implication checks done:**
- **`FoodProductionAtbDB` (new component):** feeds INTO `SustenanceProcessor` (food supply) + `ColonyMoraleDB` (quality bonus, via `PopulationProcessor`); shares STATE with `ComponentInstancesDB` (summed on demand, no bookkeeping — the `HousingAtbDB` pattern, so no install/uninstall hook needed); triggers nothing. **No new processor** (deliberately — reused `SustenanceProcessor`, avoiding the L9 one-hotloop-per-blob trap the user flagged). Health-scaled supply means the grave rung (bombard → HealthPercent drops → supply falls) works for free.
- **Garrison third pass:** feeds off each faction's `Colonies` → planet body `PlanetRegionsDB` (confirmed generated by `StarSystemFactory.LoadFromBlueprint`); idempotent; DevTest-only (not the barebones New Game). No processor, no new blob — pure factory-time raise.
- **UMF fleet size:** pure scenario data; the gunship design is already proven buildable; `ReadyStrikeFleet` reads fleet warship count — 3 clears the min. No engine change.

**Lessons banked:**
- Build-client green + fast-red test shards ⇒ test compile error; grep the CI log for `error CS`.
- Namespace ≠ folder — always grep the type's real namespace.
- Verify a dial's consumer reads it before adding the dial (the "pretty" trap).
- Prefer reusing an existing processor over adding one (L9); food supply rode `SustenanceProcessor`, no new hotloop.
- Scenario-data (DevTest/UMF/Kithrin) is NOT under `BaseModIntegrityTests` — gauge it with a scenario-load test + hand-check material unlocks.

---

## Open blockers / scoped-next (the honest edge)

- **B5 keystone — the conquest loop can't take a planet yet.** `ConquerResolver` only SAILS the strike fleet to the enemy world (Rung 1); there is **no troop load / land / capture step**, and there is **no troop-transport ship design** in the base mod. NPCs now have a home garrison (defenders) but no path to move offensive troops. This is the active target. Sub-slices (each CI-gauged, each logged to the AI record): (1) author a troop-transport ship design; (2) AI builds troops + a transport when it has a reachable war target; (3) AI loads troops (`LoadTroopsOrder`); (4) transport rides the strike fleet (already sails); (5) AI lands on arrival (`LandTroopsOrder`); (6) capture via the existing `GroundForcesProcessor.TryCapturePlanet`. Gauge: an AI-driven `TakeAPlanetIntegrationTests` sibling.
- **Kithrin materiel (B3 remainder) deferred by design.** Kithrin is a station-expansionist ("spreads on its own"), not a Mars-style invader — forcing it a warfleet contradicts its role. Its real gap is the `ExpandResolver` station-presence dead-end (a station faction with 0 colonies can't expand) + its bankruptcy — an engine slice, not materiel data. Parked with a note.
- **Resilience/bombardment-durability dial** — parked (needs the shared installation-damage path to read HTK; a combat-balance change for the dev's PC).
